using System;
using System.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: Central orchestrator for scene transitions.
    /// MonoBehaviour singleton, DontDestroyOnLoad.
    /// Loads GameFlowDefinitionSO from Resources at Awake.
    /// Wraps GameBootstrap for network states; wraps SceneManager for standard states.
    ///
    /// Public API:
    ///   RequestTransition(toState, data)  — trigger a state change
    ///   FireEvent(eventName)              — fire event that may trigger transitions
    ///   CurrentState, IsLoading, LoadProgress
    ///   OnStateChanged, OnSceneWillLoad/DidLoad, OnSceneWillUnload/DidUnload
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public class SceneService : MonoBehaviour
    {
        public static SceneService Instance { get; private set; }

        private GameFlowStateMachine _stateMachine;
        private GameFlowDefinitionSO _flowDef;
        private bool _isLoading;
        private float _loadProgress;
        private object _transitionData;
        private Coroutine _activeTransition;

        // Public read-only state
        public string CurrentState => _stateMachine?.CurrentStateId;
        public bool IsLoading => _isLoading;
        public float LoadProgress => _loadProgress;

        // Lifecycle events
        public event Action<string, string> OnStateChanged;   // fromState, toState
        public event Action<string> OnSceneWillLoad;
        public event Action<string> OnSceneDidLoad;
        public event Action<string> OnSceneWillUnload;
        public event Action<string> OnSceneDidUnload;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _flowDef = Resources.Load<GameFlowDefinitionSO>("GameFlowDefinition");
            if (_flowDef == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SceneService] No GameFlowDefinition in Resources/. " +
                    "Scene flow management disabled. Create one via DIG > Scene Management > Game Flow Definition.");
#endif
                return;
            }

            _stateMachine = new GameFlowStateMachine();
            _stateMachine.Initialize(_flowDef);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SceneService] Initialized: flow='{_flowDef.FlowName}', initial='{_flowDef.InitialState}'");
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ========== PUBLIC API ==========

        /// <summary>
        /// Request a transition to a named state.
        /// <paramref name="data"/> is optional context accessible in lifecycle hooks.
        /// </summary>
        public void RequestTransition(string toState, object data = null)
        {
            if (_isLoading)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SceneService] Cannot transition to '{toState}': already loading.");
#endif
                return;
            }

            if (_stateMachine == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SceneService] State machine not initialized.");
#endif
                return;
            }

            var targetState = _stateMachine.FindState(toState);
            if (targetState == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SceneService] Unknown state '{toState}'.");
#endif
                return;
            }

            // Look for a defined transition (optional — allows defaults)
            var transition = _stateMachine.TryTransition(toState);
            _transitionData = data;

            if (_activeTransition != null)
                StopCoroutine(_activeTransition);

            _activeTransition = StartCoroutine(TransitionCoroutine(toState, targetState, transition));
        }

        /// <summary>
        /// Fire a named event. If a transition with a matching TriggerEvent exists
        /// from the current state, that transition executes.
        /// </summary>
        public void FireEvent(string eventName)
        {
            if (_isLoading || _stateMachine == null) return;

            string targetState = _stateMachine.ProcessEvent(eventName);
            if (!string.IsNullOrEmpty(targetState))
                RequestTransition(targetState);
        }

        /// <summary>
        /// Tear down network worlds and return to a non-network state.
        /// </summary>
        public void ReturnToState(string stateId)
        {
            var currentState = _stateMachine?.CurrentState;
            if (currentState != null && currentState.RequiresNetwork)
            {
                DIG.Lobby.LobbyToGameTransition.ReturnToLobby();
            }
            RequestTransition(stateId);
        }

        // ========== TRANSITION PIPELINE ==========

        private IEnumerator TransitionCoroutine(
            string toStateId,
            GameFlowState targetState,
            GameFlowTransition transition)
        {
            _isLoading = true;
            _loadProgress = 0f;

            string fromStateId = _stateMachine.CurrentStateId;
            var fromState = _stateMachine.CurrentState;

            // Resolve transition parameters (explicit transition > flow defaults)
            var animation = transition?.Animation ?? _flowDef.DefaultTransitionAnimation;
            float animDuration = transition != null && transition.AnimationDuration > 0f
                ? transition.AnimationDuration
                : _flowDef.DefaultTransitionDuration;
            var loadingProfile = targetState.LoadingScreen ?? _flowDef.DefaultLoadingScreen;

            // --- Step 1: Fire exit event ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (fromState != null && !string.IsNullOrEmpty(fromState.OnExitEvent))
                Debug.Log($"[SceneService] OnExitEvent: {fromState.OnExitEvent}");
#endif

            // --- Step 2: Fire OnSceneWillUnload ---
            if (fromState?.Scene != null)
                OnSceneWillUnload?.Invoke(fromState.Scene.SceneId);

            // --- Step 3: Show loading screen ---
            var loadingMgr = LoadingScreenManager.Instance;
            if (loadingMgr != null && loadingProfile != null)
            {
                loadingMgr.SetPhaseText("Loading...");
                yield return loadingMgr.Show(loadingProfile);
            }

            // --- Step 4: Switch input to UI during transition ---
            var inputMgr = DIG.Core.Input.InputContextManager.Instance;
            inputMgr?.SetContext(DIG.Core.Input.InputContext.UI);

            // --- Step 5: Load scene(s) ---
            if (targetState.RequiresNetwork)
            {
                yield return HandleNetworkTransition(targetState, loadingMgr);
            }
            else
            {
                yield return HandleStandardTransition(targetState, loadingMgr);
            }

            // --- Step 6: Fire OnSceneDidLoad ---
            if (targetState.Scene != null)
                OnSceneDidLoad?.Invoke(targetState.Scene.SceneId);

            // --- Step 7: Fire OnSceneDidUnload for previous scene ---
            if (fromState?.Scene != null)
                OnSceneDidUnload?.Invoke(fromState.Scene.SceneId);

            // --- Step 8: Wait for MinLoadTime ---
            if (loadingMgr != null)
                yield return loadingMgr.WaitForMinDisplayTime();

            // --- Step 9: Hide loading screen ---
            if (loadingMgr != null && loadingMgr.IsVisible)
                yield return loadingMgr.Hide();

            // --- Step 10: Switch input context for target state ---
            inputMgr?.SetContext(targetState.InputContext);

            // --- Step 11: Commit state ---
            string previousState = _stateMachine.CurrentStateId;
            _stateMachine.SetState(toStateId);

            // --- Step 12: Fire enter event ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(targetState.OnEnterEvent))
                Debug.Log($"[SceneService] OnEnterEvent: {targetState.OnEnterEvent}");
#endif

            // --- Step 13: Fire OnStateChanged ---
            OnStateChanged?.Invoke(previousState, toStateId);

            _isLoading = false;
            _loadProgress = 1f;
            _activeTransition = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SceneService] Transition complete: {previousState} -> {toStateId}");
#endif
        }

        // ---------- Standard (non-network) scene loading ----------

        private IEnumerator HandleStandardTransition(
            GameFlowState targetState,
            LoadingScreenManager loadingMgr)
        {
            // Load primary scene
            if (targetState.Scene != null)
            {
                OnSceneWillLoad?.Invoke(targetState.Scene.SceneId);
                loadingMgr?.SetPhaseText($"Loading {targetState.Scene.DisplayName}...");

                yield return SceneLoader.LoadAsync(targetState.Scene, progress =>
                {
                    _loadProgress = progress * 0.8f; // Primary = 80%
                    loadingMgr?.UpdateProgress(_loadProgress);
                });
            }

            // Load additive scenes
            if (targetState.AdditiveScenes != null && targetState.AdditiveScenes.Length > 0)
            {
                float additiveWeight = 0.2f / targetState.AdditiveScenes.Length;
                for (int i = 0; i < targetState.AdditiveScenes.Length; i++)
                {
                    var addScene = targetState.AdditiveScenes[i];
                    if (addScene == null) continue;

                    loadingMgr?.SetPhaseText($"Loading {addScene.DisplayName}...");
                    int idx = i;
                    yield return SceneLoader.LoadAsync(addScene, progress =>
                    {
                        _loadProgress = 0.8f + (idx * additiveWeight) + (progress * additiveWeight);
                        loadingMgr?.UpdateProgress(_loadProgress);
                    });
                }
            }

            _loadProgress = 1f;
            loadingMgr?.UpdateProgress(1f);
        }

        // ---------- Network-aware transition ----------

        private IEnumerator HandleNetworkTransition(
            GameFlowState targetState,
            LoadingScreenManager loadingMgr)
        {
            // For network states, LobbyManager.StartGame() has already called
            // LobbyToGameTransition.BeginTransition(). We monitor its events
            // and pipe progress through to the loading screen.

            bool transitionError = false;
            string errorMessage = null;

            void OnProgress(string phase, float progress)
            {
                _loadProgress = progress;
                loadingMgr?.SetPhaseText(phase);
                loadingMgr?.UpdateProgress(progress);
            }

            void OnError(string msg)
            {
                transitionError = true;
                errorMessage = msg;
            }

            DIG.Lobby.LobbyToGameTransition.OnProgressUpdated += OnProgress;
            DIG.Lobby.LobbyToGameTransition.OnTransitionError += OnError;

            // Wait for GameBootstrap to finish (with timeout)
            float timeout = 20f;
            float elapsed = 0f;
            while (!GameBootstrap.HasInitialized && !transitionError && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            DIG.Lobby.LobbyToGameTransition.OnProgressUpdated -= OnProgress;
            DIG.Lobby.LobbyToGameTransition.OnTransitionError -= OnError;

            if (transitionError)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SceneService] Network transition error: {errorMessage}");
#endif
                _isLoading = false;
                if (loadingMgr != null && loadingMgr.IsVisible)
                    yield return loadingMgr.Hide();
                yield break;
            }

            if (!GameBootstrap.HasInitialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SceneService] Network transition timed out (20s).");
#endif
                _isLoading = false;
                if (loadingMgr != null && loadingMgr.IsVisible)
                    yield return loadingMgr.Hide();
                yield break;
            }

            _loadProgress = 1f;
            loadingMgr?.UpdateProgress(1f);
        }

        // ========== STATIC RESET ==========

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }
    }
}
