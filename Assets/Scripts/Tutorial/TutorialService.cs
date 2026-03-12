using System;
using System.Collections.Generic;
using DIG.Tutorial.Config;
using DIG.Tutorial.Detectors;
using DIG.Tutorial.UI;
using UnityEngine;

namespace DIG.Tutorial
{
    /// <summary>
    /// EPIC 18.4: Singleton MonoBehaviour managing tutorial state machine, step progression,
    /// completion detection, and persistence. All tutorial state is managed-only (no ECS on player).
    /// </summary>
    public class TutorialService : MonoBehaviour
    {
        public static TutorialService Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        // ── State machine ────────────────────────────────────────
        public enum TutorialState { Idle, StepActive, Advancing }

        public TutorialState CurrentState { get; private set; } = TutorialState.Idle;
        public TutorialSequenceSO ActiveSequence { get; private set; }
        public TutorialStepSO ActiveStep { get; private set; }
        public int ActiveStepIndex { get; private set; } = -1;

        // ── Events ───────────────────────────────────────────────
        public event Action<TutorialSequenceSO> OnTutorialStarted;
        public event Action<TutorialStepSO, int, int> OnStepShown; // step, index, totalSteps
        public event Action<TutorialStepSO> OnStepCompleted;
        public event Action<TutorialSequenceSO> OnTutorialCompleted;
        public event Action<TutorialSequenceSO> OnTutorialSkipped;

        // ── Config & data ────────────────────────────────────────
        private TutorialConfigSO _config;
        private TutorialOverlayController _overlay;
        private AudioSource _audioSource;

        private readonly Dictionary<string, TutorialSequenceSO> _sequenceMap = new();
        private readonly HashSet<string> _completedCache = new();
        private bool _completedCacheDirty = true;
        private TutorialSequenceSO[] _allSequences;

        // ── Completion detection ─────────────────────────────────
        private ICompletionDetector _activeDetector;
        private float _stepTimeout;
        private float _stepElapsed;
        private float _advanceTimer;

        // ── Auto-start debounce ──────────────────────────────────
        private float _autoStartCooldown;
        private const float AutoStartEvalInterval = 0.5f;

        internal void Initialize(TutorialConfigSO config, TutorialOverlayController overlay)
        {
            _config = config;
            _overlay = overlay;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            // Load all sequences from Resources
            _allSequences = Resources.LoadAll<TutorialSequenceSO>("TutorialSequences");
            foreach (var seq in _allSequences)
            {
                if (!string.IsNullOrEmpty(seq.SequenceId))
                    _sequenceMap[seq.SequenceId] = seq;
            }

            Instance = this;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            switch (CurrentState)
            {
                case TutorialState.Idle:
                    _autoStartCooldown -= dt;
                    if (_autoStartCooldown <= 0f)
                    {
                        EvaluateAutoStarts();
                        _autoStartCooldown = AutoStartEvalInterval;
                    }
                    break;

                case TutorialState.StepActive:
                    TickActiveStep(dt);
                    break;

                case TutorialState.Advancing:
                    _advanceTimer -= dt;
                    if (_advanceTimer <= 0f)
                        AdvanceToNextStep();
                    break;
            }

            // Tick timer-based detectors
            if (_activeDetector is TimerCompletionDetector timer && CurrentState == TutorialState.StepActive)
                timer.Tick(dt);

            // Tick step timeout
            if (CurrentState == TutorialState.StepActive && _stepTimeout > 0f)
            {
                _stepElapsed += dt;
                if (_stepElapsed >= _stepTimeout)
                    OnDetectorCompleted();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Public API ───────────────────────────────────────────

        public bool StartTutorial(string sequenceId)
        {
            if (CurrentState != TutorialState.Idle) return false;
            if (!_sequenceMap.TryGetValue(sequenceId, out var sequence)) return false;
            if (IsTutorialCompleted(sequenceId)) return false;

            ActiveSequence = sequence;
            ActiveStepIndex = -1;
            OnTutorialStarted?.Invoke(sequence);

            AdvanceToNextStep();
            return true;
        }

        public void AdvanceStep()
        {
            if (CurrentState != TutorialState.StepActive) return;
            if (_activeDetector is ManualCompletionDetector manual)
                manual.Complete();
            else
                OnDetectorCompleted();
        }

        public void SkipTutorial()
        {
            if (ActiveSequence == null || !ActiveSequence.CanSkip) return;

            StopActiveDetector();
            _overlay?.HideAll();

            var seq = ActiveSequence;
            MarkCompleted(seq);
            ActiveSequence = null;
            ActiveStep = null;
            ActiveStepIndex = -1;
            CurrentState = TutorialState.Idle;

            OnTutorialSkipped?.Invoke(seq);
        }

        public void FireEvent(string eventKey)
        {
            if (_activeDetector is CustomEventCompletionDetector custom)
                custom.OnEventFired(eventKey);
        }

        public void NotifyScreenChanged(string screenName)
        {
            if (_activeDetector is UIScreenCompletionDetector screen)
                screen.CheckScreen(screenName);
        }

        public bool IsTutorialCompleted(string sequenceId)
        {
            if (!_sequenceMap.TryGetValue(sequenceId, out var seq)) return false;

            if (_completedCacheDirty)
            {
                _completedCache.Clear();
                foreach (var s in _allSequences)
                {
                    if (s != null && PlayerPrefs.GetInt(s.GetSaveKey(), 0) == 1)
                        _completedCache.Add(s.SequenceId);
                }
                _completedCacheDirty = false;
            }

            return _completedCache.Contains(sequenceId);
        }

        public void ResetTutorial(string sequenceId)
        {
            if (_sequenceMap.TryGetValue(sequenceId, out var seq))
            {
                PlayerPrefs.DeleteKey(seq.GetSaveKey());
                _completedCacheDirty = true;
            }
        }

        public void ResetAll()
        {
            foreach (var seq in _allSequences)
                PlayerPrefs.DeleteKey(seq.GetSaveKey());
            _completedCacheDirty = true;
        }

        // ── Internal ─────────────────────────────────────────────

        private void EvaluateAutoStarts()
        {
            if (_allSequences == null) return;

            TutorialSequenceSO best = null;
            int bestPriority = int.MinValue;

            foreach (var seq in _allSequences)
            {
                if (!seq.AutoStart) continue;
                if (IsTutorialCompleted(seq.SequenceId)) continue;
                if (seq.Prerequisite != null && !seq.Prerequisite.Evaluate(this)) continue;
                if (seq.Priority > bestPriority)
                {
                    best = seq;
                    bestPriority = seq.Priority;
                }
            }

            if (best != null)
                StartTutorial(best.SequenceId);
        }

        private void AdvanceToNextStep()
        {
            StopActiveDetector();

            // Determine next step
            int nextIndex = -1;

            if (ActiveStep != null && !string.IsNullOrEmpty(ActiveStep.NextStepId))
            {
                // Explicit next step override
                nextIndex = FindStepIndex(ActiveStep.NextStepId);
            }
            else
            {
                nextIndex = ActiveStepIndex + 1;
            }

            // Check if sequence is complete
            if (nextIndex < 0 || nextIndex >= ActiveSequence.Steps.Length)
            {
                CompleteTutorial();
                return;
            }

            ActiveStepIndex = nextIndex;
            ActiveStep = ActiveSequence.Steps[nextIndex];

            // Handle Branch steps
            if (ActiveStep.StepType == TutorialStepType.Branch)
            {
                HandleBranchStep();
                return;
            }

            // Handle Delay steps
            if (ActiveStep.StepType == TutorialStepType.Delay)
            {
                CurrentState = TutorialState.Advancing;
                _advanceTimer = ActiveStep.TimeoutSeconds > 0 ? ActiveStep.TimeoutSeconds : 1f;
                return;
            }

            // Show step
            ShowStep(ActiveStep);
        }

        private void HandleBranchStep()
        {
            bool conditionMet = ActiveStep.BranchCondition != null &&
                                ActiveStep.BranchCondition.Evaluate(this);

            string targetStepId = conditionMet ? ActiveStep.TrueStepId : ActiveStep.FalseStepId;

            if (!string.IsNullOrEmpty(targetStepId))
            {
                int idx = FindStepIndex(targetStepId);
                if (idx >= 0)
                {
                    ActiveStepIndex = idx - 1; // Will be incremented by AdvanceToNextStep
                }
            }
            AdvanceToNextStep();
        }

        private void ShowStep(TutorialStepSO step)
        {
            CurrentState = TutorialState.StepActive;
            _stepElapsed = 0f;
            _stepTimeout = step.TimeoutSeconds;

            // Play sound
            var clip = step.Sound ?? _config?.DefaultStepSound;
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, _config?.SoundVolume ?? 0.5f);

            // Create and start completion detector
            _activeDetector = CompletionDetectorFactory.Create(step.CompletionCondition);
            _activeDetector.OnCompleted -= OnDetectorCompleted; // Guard against double-subscription
            _activeDetector.OnCompleted += OnDetectorCompleted;
            _activeDetector.Start(step);

            // Show UI
            _overlay?.ShowStep(step, ActiveStepIndex, ActiveSequence.Steps.Length, ActiveSequence.CanSkip);

            OnStepShown?.Invoke(step, ActiveStepIndex, ActiveSequence.Steps.Length);
        }

        private void TickActiveStep(float dt)
        {
            // Overlay can update per-frame (spotlight tracking, world marker projection)
            _overlay?.Tick(dt);
        }

        private void OnDetectorCompleted()
        {
            if (CurrentState != TutorialState.StepActive) return;

            var completedStep = ActiveStep;
            StopActiveDetector();
            _overlay?.HideAll();

            OnStepCompleted?.Invoke(completedStep);

            // Brief delay before next step
            CurrentState = TutorialState.Advancing;
            _advanceTimer = _config?.StepTransitionDelay ?? 0.3f;
        }

        private void CompleteTutorial()
        {
            _overlay?.HideAll();

            var seq = ActiveSequence;
            MarkCompleted(seq);
            ActiveSequence = null;
            ActiveStep = null;
            ActiveStepIndex = -1;
            CurrentState = TutorialState.Idle;

            OnTutorialCompleted?.Invoke(seq);
        }

        private void MarkCompleted(TutorialSequenceSO seq)
        {
            PlayerPrefs.SetInt(seq.GetSaveKey(), 1);
            PlayerPrefs.Save();
            _completedCacheDirty = true;
        }

        private void StopActiveDetector()
        {
            if (_activeDetector != null)
            {
                _activeDetector.OnCompleted -= OnDetectorCompleted;
                _activeDetector.Stop();
                _activeDetector = null;
            }
        }

        private int FindStepIndex(string stepId)
        {
            if (ActiveSequence?.Steps == null) return -1;
            for (int i = 0; i < ActiveSequence.Steps.Length; i++)
            {
                if (ActiveSequence.Steps[i] != null &&
                    string.Equals(ActiveSequence.Steps[i].StepId, stepId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
