using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CW-03: Animation Binding module.
    /// Attack window timing, hitbox activation frames, animation event insertion.
    /// </summary>
    public class AnimBindingModule : ICharacterModule
    {
        private AnimationClip _selectedClip;
        private Animator _selectedAnimator;
        private Vector2 _scrollPosition;
        
        // Attack window settings
        private float _attackStartTime = 0.2f;
        private float _attackEndTime = 0.6f;
        private float _damageMultiplier = 1.0f;
        private bool _canCancel = false;
        private float _cancelWindowStart = 0.7f;
        
        // Preview
        private bool _isPreviewing = false;
        private float _previewTime = 0f;
        
        // Events list
        private List<AnimEventData> _events = new List<AnimEventData>();
        
        // Event presets
        private enum EventPreset { None, MeleeAttack, RangedFire, FootstepL, FootstepR, VFXSpawn, SFXPlay }
        private EventPreset _selectedPreset = EventPreset.None;

        private class AnimEventData
        {
            public float Time;
            public string FunctionName;
            public string StringParam;
            public float FloatParam;
            public int IntParam;
            public bool IsNew;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Animation Binding", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bind attack windows to animations, configure hitbox activation, and insert animation events.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawClipSelection();
            EditorGUILayout.Space(10);
            DrawTimelinePreview();
            EditorGUILayout.Space(10);
            DrawAttackWindowConfig();
            EditorGUILayout.Space(10);
            DrawEventManager();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawClipSelection()
        {
            EditorGUILayout.LabelField("Animation Selection", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _selectedClip = (AnimationClip)EditorGUILayout.ObjectField(
                "Animation Clip", _selectedClip, typeof(AnimationClip), false);
            
            if (EditorGUI.EndChangeCheck() && _selectedClip != null)
            {
                AnalyzeClip();
            }

            _selectedAnimator = (Animator)EditorGUILayout.ObjectField(
                "Preview Animator", _selectedAnimator, typeof(Animator), true);

            if (_selectedClip != null)
            {
                EditorGUILayout.LabelField($"Length: {_selectedClip.length:F2}s | FPS: {_selectedClip.frameRate}");
                EditorGUILayout.LabelField($"Frames: {Mathf.RoundToInt(_selectedClip.length * _selectedClip.frameRate)}");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTimelinePreview()
        {
            if (_selectedClip == null) return;

            EditorGUILayout.LabelField("Timeline Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Timeline scrubber
            Rect timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            
            DrawTimeline(timelineRect);

            // Preview controls
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(_isPreviewing ? "⏸" : "▶", GUILayout.Width(30)))
            {
                _isPreviewing = !_isPreviewing;
            }
            
            if (GUILayout.Button("⏮", GUILayout.Width(30)))
            {
                _previewTime = 0f;
                UpdatePreview();
            }
            
            EditorGUI.BeginChangeCheck();
            _previewTime = EditorGUILayout.Slider(_previewTime, 0f, _selectedClip.length);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreview();
            }
            
            EditorGUILayout.LabelField($"{_previewTime:F2}s", GUILayout.Width(50));
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTimeline(Rect rect)
        {
            if (_selectedClip == null) return;

            // Background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            float clipLength = _selectedClip.length;
            
            // Attack window
            float startX = rect.x + (rect.width * (_attackStartTime / clipLength));
            float endX = rect.x + (rect.width * (_attackEndTime / clipLength));
            Rect attackRect = new Rect(startX, rect.y, endX - startX, rect.height * 0.4f);
            EditorGUI.DrawRect(attackRect, new Color(1f, 0.3f, 0.3f, 0.5f));
            
            // Cancel window
            if (_canCancel)
            {
                float cancelStartX = rect.x + (rect.width * (_cancelWindowStart / clipLength));
                Rect cancelRect = new Rect(cancelStartX, rect.y + rect.height * 0.4f, 
                    rect.x + rect.width - cancelStartX, rect.height * 0.2f);
                EditorGUI.DrawRect(cancelRect, new Color(0.3f, 1f, 0.3f, 0.5f));
            }

            // Events
            foreach (var evt in _events)
            {
                float evtX = rect.x + (rect.width * (evt.Time / clipLength));
                Rect evtRect = new Rect(evtX - 2, rect.y + rect.height * 0.6f, 4, rect.height * 0.4f);
                EditorGUI.DrawRect(evtRect, evt.IsNew ? Color.yellow : Color.cyan);
            }

            // Playhead
            float playheadX = rect.x + (rect.width * (_previewTime / clipLength));
            EditorGUI.DrawRect(new Rect(playheadX - 1, rect.y, 2, rect.height), Color.white);

            // Labels
            GUI.Label(new Rect(rect.x + 5, rect.y + 2, 100, 16), "Attack", EditorStyles.miniLabel);
            if (_canCancel)
            {
                GUI.Label(new Rect(rect.x + 5, rect.y + rect.height * 0.4f + 2, 100, 16), 
                    "Cancel", EditorStyles.miniLabel);
            }
        }

        private void DrawAttackWindowConfig()
        {
            if (_selectedClip == null) return;

            EditorGUILayout.LabelField("Attack Window", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float maxTime = _selectedClip.length;

            EditorGUILayout.LabelField("Hitbox Active Window", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start", GUILayout.Width(40));
            _attackStartTime = EditorGUILayout.Slider(_attackStartTime, 0f, maxTime);
            EditorGUILayout.LabelField($"({_attackStartTime / maxTime * 100:F0}%)", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("End", GUILayout.Width(40));
            _attackEndTime = EditorGUILayout.Slider(_attackEndTime, _attackStartTime, maxTime);
            EditorGUILayout.LabelField($"({_attackEndTime / maxTime * 100:F0}%)", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            _damageMultiplier = EditorGUILayout.Slider("Damage Multiplier", _damageMultiplier, 0.1f, 3f);

            EditorGUILayout.Space(5);
            _canCancel = EditorGUILayout.Toggle("Enable Cancel Window", _canCancel);
            if (_canCancel)
            {
                _cancelWindowStart = EditorGUILayout.Slider("Cancel Start", _cancelWindowStart, 
                    _attackEndTime, maxTime);
            }

            // Presets
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Light Attack"))
            {
                _attackStartTime = maxTime * 0.15f;
                _attackEndTime = maxTime * 0.4f;
                _damageMultiplier = 1.0f;
                _canCancel = true;
                _cancelWindowStart = maxTime * 0.6f;
            }
            if (GUILayout.Button("Heavy Attack"))
            {
                _attackStartTime = maxTime * 0.3f;
                _attackEndTime = maxTime * 0.7f;
                _damageMultiplier = 1.5f;
                _canCancel = true;
                _cancelWindowStart = maxTime * 0.85f;
            }
            if (GUILayout.Button("Combo Finisher"))
            {
                _attackStartTime = maxTime * 0.25f;
                _attackEndTime = maxTime * 0.65f;
                _damageMultiplier = 2.0f;
                _canCancel = false;
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawEventManager()
        {
            if (_selectedClip == null) return;

            EditorGUILayout.LabelField("Animation Events", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Event list
            EditorGUILayout.LabelField($"Events ({_events.Count})", EditorStyles.miniLabel);
            
            for (int i = 0; i < _events.Count; i++)
            {
                var evt = _events[i];
                EditorGUILayout.BeginHorizontal();
                
                Color prevColor = GUI.backgroundColor;
                if (evt.IsNew) GUI.backgroundColor = Color.yellow;
                
                evt.Time = EditorGUILayout.FloatField(evt.Time, GUILayout.Width(60));
                evt.FunctionName = EditorGUILayout.TextField(evt.FunctionName, GUILayout.Width(150));
                evt.StringParam = EditorGUILayout.TextField(evt.StringParam, GUILayout.ExpandWidth(true));
                
                GUI.backgroundColor = prevColor;
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _events.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            // Add event
            EditorGUILayout.BeginHorizontal();
            _selectedPreset = (EventPreset)EditorGUILayout.EnumPopup(_selectedPreset, GUILayout.Width(120));
            
            if (GUILayout.Button("Add Event"))
            {
                AddEventFromPreset(_selectedPreset);
            }
            
            if (GUILayout.Button("Add at Playhead"))
            {
                AddEventFromPreset(_selectedPreset, _previewTime);
            }
            
            EditorGUILayout.EndHorizontal();

            // Quick add buttons
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("+ Hitbox Start"))
            {
                _events.Add(new AnimEventData
                {
                    Time = _attackStartTime,
                    FunctionName = "OnHitboxActivate",
                    IsNew = true
                });
            }
            
            if (GUILayout.Button("+ Hitbox End"))
            {
                _events.Add(new AnimEventData
                {
                    Time = _attackEndTime,
                    FunctionName = "OnHitboxDeactivate",
                    IsNew = true
                });
            }
            
            if (GUILayout.Button("+ Attack Sound"))
            {
                _events.Add(new AnimEventData
                {
                    Time = _attackStartTime,
                    FunctionName = "PlayAttackSound",
                    StringParam = "Slash",
                    IsNew = true
                });
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            if (_selectedClip == null) return;

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply Events to Clip", GUILayout.Height(30)))
            {
                ApplyEventsToClip();
            }
            
            if (GUILayout.Button("Generate Combo Data", GUILayout.Height(30)))
            {
                GenerateComboData();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Timing JSON"))
            {
                ExportTiming();
            }
            
            if (GUILayout.Button("Clear New Events"))
            {
                _events.RemoveAll(e => e.IsNew);
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void AnalyzeClip()
        {
            _events.Clear();

            if (_selectedClip == null) return;

            // Load existing events
            var existingEvents = AnimationUtility.GetAnimationEvents(_selectedClip);
            foreach (var evt in existingEvents)
            {
                _events.Add(new AnimEventData
                {
                    Time = evt.time,
                    FunctionName = evt.functionName,
                    StringParam = evt.stringParameter,
                    FloatParam = evt.floatParameter,
                    IntParam = evt.intParameter,
                    IsNew = false
                });
            }

            // Auto-detect attack window from existing events
            var hitboxStart = _events.FirstOrDefault(e => 
                e.FunctionName.Contains("HitboxActivate") || e.FunctionName.Contains("AttackStart"));
            var hitboxEnd = _events.FirstOrDefault(e => 
                e.FunctionName.Contains("HitboxDeactivate") || e.FunctionName.Contains("AttackEnd"));

            if (hitboxStart != null) _attackStartTime = hitboxStart.Time;
            if (hitboxEnd != null) _attackEndTime = hitboxEnd.Time;

            Debug.Log($"[AnimBinding] Loaded {_events.Count} events from {_selectedClip.name}");
        }

        private void AddEventFromPreset(EventPreset preset, float? time = null)
        {
            float eventTime = time ?? (_selectedClip.length * 0.5f);

            var evt = new AnimEventData
            {
                Time = eventTime,
                IsNew = true
            };

            switch (preset)
            {
                case EventPreset.MeleeAttack:
                    evt.FunctionName = "OnMeleeAttack";
                    evt.FloatParam = _damageMultiplier;
                    break;
                case EventPreset.RangedFire:
                    evt.FunctionName = "OnRangedFire";
                    break;
                case EventPreset.FootstepL:
                    evt.FunctionName = "OnFootstep";
                    evt.StringParam = "Left";
                    break;
                case EventPreset.FootstepR:
                    evt.FunctionName = "OnFootstep";
                    evt.StringParam = "Right";
                    break;
                case EventPreset.VFXSpawn:
                    evt.FunctionName = "SpawnVFX";
                    evt.StringParam = "Effect_Name";
                    break;
                case EventPreset.SFXPlay:
                    evt.FunctionName = "PlaySFX";
                    evt.StringParam = "Sound_Name";
                    break;
                default:
                    evt.FunctionName = "CustomEvent";
                    break;
            }

            _events.Add(evt);
        }

        private void ApplyEventsToClip()
        {
            if (_selectedClip == null) return;

            var animEvents = new AnimationEvent[_events.Count];
            for (int i = 0; i < _events.Count; i++)
            {
                var data = _events[i];
                animEvents[i] = new AnimationEvent
                {
                    time = data.Time,
                    functionName = data.FunctionName,
                    stringParameter = data.StringParam,
                    floatParameter = data.FloatParam,
                    intParameter = data.IntParam
                };
            }

            AnimationUtility.SetAnimationEvents(_selectedClip, animEvents);
            EditorUtility.SetDirty(_selectedClip);
            AssetDatabase.SaveAssets();

            // Mark all as not new
            foreach (var evt in _events)
            {
                evt.IsNew = false;
            }

            Debug.Log($"[AnimBinding] Applied {_events.Count} events to {_selectedClip.name}");
        }

        private void GenerateComboData()
        {
            Debug.Log($"[AnimBinding] Generated combo data: Start={_attackStartTime:F2}, End={_attackEndTime:F2}, " +
                      $"Damage={_damageMultiplier:F1}, Cancel={(_canCancel ? _cancelWindowStart.ToString("F2") : "None")}");
            
            EditorGUIUtility.systemCopyBuffer = 
                $"new ComboData {{ Duration = {_selectedClip.length}f, InputWindowStart = {_attackStartTime}f, " +
                $"InputWindowEnd = {_attackEndTime}f, DamageMultiplier = {_damageMultiplier}f }}";
            
            Debug.Log("[AnimBinding] Combo data copied to clipboard.");
        }

        private void ExportTiming()
        {
            string json = JsonUtility.ToJson(new
            {
                ClipName = _selectedClip.name,
                Length = _selectedClip.length,
                AttackStart = _attackStartTime,
                AttackEnd = _attackEndTime,
                DamageMultiplier = _damageMultiplier,
                CanCancel = _canCancel,
                CancelStart = _cancelWindowStart,
                EventCount = _events.Count
            }, true);

            string path = EditorUtility.SaveFilePanel("Export Timing", "", 
                $"{_selectedClip.name}_timing", "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[AnimBinding] Exported timing to {path}");
            }
        }

        private void UpdatePreview()
        {
            if (_selectedAnimator == null || _selectedClip == null) return;

            _selectedClip.SampleAnimation(_selectedAnimator.gameObject, _previewTime);
            SceneView.RepaintAll();
        }
    }
}
