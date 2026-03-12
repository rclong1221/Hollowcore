using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.AnimationWorkstation
{
    /// <summary>
    /// Animation Event Generator module for Animation Workstation.
    /// Adds animation events to clips with presets for weapon animations.
    /// </summary>
    public class AnimationEventModule : IWorkstationModule
    {
        private enum AnimationType
        {
            // Shootable
            Fire,
            Reload,
            MagazineReload,
            DryFire,
            // Melee
            MeleeBasic,
            MeleeCombo,
            // Shield
            ShieldBlock,
            ShieldParry,
            // Bow
            BowShot,
            // Throwable
            Throw,
            // Custom
            Custom
        }

        private enum FramePositionMode
        {
            Manual,
            Percentage
        }

        [System.Serializable]
        private class EventConfig
        {
            public string EventName;
            public string StringParameter;
            public FramePositionMode PositionMode = FramePositionMode.Percentage;
            public int ManualFrame = 0;
            public float Percentage = 0f;
            public bool Enabled = true;
        }

        private List<AnimationClip> _selectedClips = new List<AnimationClip>();
        private AnimationType _selectedType = AnimationType.Fire;
        private List<EventConfig> _eventConfigs = new List<EventConfig>();
        private Vector2 _scrollPosition;
        private bool _showPreview = true;

        public AnimationEventModule()
        {
            ApplyPreset(AnimationType.Fire);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Animation Event Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select animation clips in Project window, then add events using presets.", MessageType.Info);
            EditorGUILayout.Space(10);

            // Refresh selection button
            if (GUILayout.Button("Refresh Selection from Project", EditorStyles.miniButton))
            {
                RefreshSelection();
            }

            // Selected clips
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Selected Clips: {_selectedClips.Count}", EditorStyles.boldLabel);
            
            if (_selectedClips.Count == 0)
            {
                EditorGUILayout.HelpBox("No animation clips selected. Select .anim files in the Project window.", MessageType.Warning);
            }
            else
            {
                EditorGUI.indentLevel++;
                foreach (var clip in _selectedClips.Take(5))
                {
                    EditorGUILayout.LabelField($"• {clip.name} ({clip.length:F2}s)");
                }
                if (_selectedClips.Count > 5)
                {
                    EditorGUILayout.LabelField($"... and {_selectedClips.Count - 5} more");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Preset selection
            EditorGUILayout.LabelField("Animation Type Preset", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _selectedType = (AnimationType)EditorGUILayout.EnumPopup("Preset", _selectedType);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreset(_selectedType);
            }

            EditorGUILayout.Space(10);

            // Event configuration
            EditorGUILayout.LabelField("Events to Add", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(200));

            for (int i = 0; i < _eventConfigs.Count; i++)
            {
                DrawEventConfig(_eventConfigs[i], i);
            }

            EditorGUILayout.EndScrollView();

            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Event", EditorStyles.miniButton))
            {
                _eventConfigs.Add(new EventConfig
                {
                    EventName = "ExecuteEvent",
                    StringParameter = "CustomEvent",
                    PositionMode = FramePositionMode.Percentage,
                    Percentage = 50f
                });
            }
            if (_eventConfigs.Count > 0 && GUILayout.Button("- Remove Last", EditorStyles.miniButton))
            {
                _eventConfigs.RemoveAt(_eventConfigs.Count - 1);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Preview
            _showPreview = EditorGUILayout.Foldout(_showPreview, "Preview Timeline");
            if (_showPreview && _selectedClips.Count > 0)
            {
                DrawPreview();
            }

            EditorGUILayout.Space(10);

            // Apply button
            EditorGUI.BeginDisabledGroup(_selectedClips.Count == 0 || _eventConfigs.Count == 0);
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply Events to Selected Clips", GUILayout.Height(30)))
            {
                ApplyEventsToClips();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            // Clear events button
            if (_selectedClips.Count > 0)
            {
                EditorGUILayout.Space(5);
                GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
                if (GUILayout.Button("Clear All Events from Selected Clips"))
                {
                    if (EditorUtility.DisplayDialog("Clear Events",
                        $"Remove ALL animation events from {_selectedClips.Count} clip(s)?",
                        "Clear", "Cancel"))
                    {
                        ClearEventsFromClips();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void RefreshSelection()
        {
            _selectedClips.Clear();
            foreach (var obj in Selection.objects)
            {
                if (obj is AnimationClip clip)
                {
                    _selectedClips.Add(clip);
                }
            }
        }

        private void DrawEventConfig(EventConfig config, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            config.Enabled = EditorGUILayout.Toggle(config.Enabled, GUILayout.Width(20));
            EditorGUILayout.LabelField($"{config.StringParameter}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (config.Enabled)
            {
                EditorGUI.indentLevel++;
                config.StringParameter = EditorGUILayout.TextField("Event ID", config.StringParameter);
                config.PositionMode = (FramePositionMode)EditorGUILayout.EnumPopup("Position Mode", config.PositionMode);

                if (config.PositionMode == FramePositionMode.Manual)
                {
                    config.ManualFrame = EditorGUILayout.IntField("Frame", config.ManualFrame);
                }
                else
                {
                    config.Percentage = EditorGUILayout.Slider("Position %", config.Percentage, 0f, 100f);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            if (_selectedClips.Count == 0) return;

            var clip = _selectedClips[0];
            int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Preview: {clip.name} ({totalFrames} frames)", EditorStyles.miniLabel);

            // Draw timeline
            Rect timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(30));
            EditorGUI.DrawRect(timelineRect, new Color(0.2f, 0.2f, 0.2f));

            // Draw new event markers (green)
            foreach (var config in _eventConfigs.Where(c => c.Enabled))
            {
                int frame = config.PositionMode == FramePositionMode.Manual
                    ? config.ManualFrame
                    : Mathf.RoundToInt((config.Percentage / 100f) * totalFrames);

                float x = timelineRect.x + (frame / (float)totalFrames) * timelineRect.width;
                EditorGUI.DrawRect(new Rect(x - 1, timelineRect.y, 2, timelineRect.height), Color.green);
            }

            // Draw existing events (yellow)
            if (clip.events != null)
            {
                foreach (var evt in clip.events)
                {
                    int frame = Mathf.RoundToInt(evt.time * clip.frameRate);
                    float x = timelineRect.x + (frame / (float)totalFrames) * timelineRect.width;
                    EditorGUI.DrawRect(new Rect(x - 1, timelineRect.y + timelineRect.height - 8, 2, 8), Color.yellow);
                }
            }

            EditorGUILayout.LabelField("Green = New | Yellow = Existing", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void ApplyPreset(AnimationType type)
        {
            _eventConfigs.Clear();

            switch (type)
            {
                case AnimationType.Fire:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorItemFire", Percentage = 5f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorShellEject", Percentage = 15f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorItemFireComplete", Percentage = 95f });
                    break;

                case AnimationType.Reload:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorReloadStart", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorReloadInsertAmmo", Percentage = 70f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorItemReloadComplete", Percentage = 95f });
                    break;

                case AnimationType.MagazineReload:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorReloadStart", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorItemReloadDetachClip", Percentage = 15f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorItemReloadDropClip", Percentage = 25f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorReloadInsertAmmo", Percentage = 65f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorItemReloadAttachClip", Percentage = 70f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorItemReloadComplete", Percentage = 95f });
                    break;

                case AnimationType.DryFire:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorDryFire", Percentage = 5f });
                    break;

                // === MELEE PRESETS ===
                case AnimationType.MeleeBasic:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorMeleeStart", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorMeleeHitFrame", Percentage = 35f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorMeleeComplete", Percentage = 95f });
                    break;

                case AnimationType.MeleeCombo:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorMeleeStart", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorMeleeHitFrame", Percentage = 35f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorMeleeCombo", Percentage = 50f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorMeleeComplete", Percentage = 95f });
                    break;

                // === SHIELD PRESETS ===
                case AnimationType.ShieldBlock:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorBlockStart", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorBlockEnd", Percentage = 95f });
                    break;

                case AnimationType.ShieldParry:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorBlockStart", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorParryWindow", Percentage = 5f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorParryComplete", Percentage = 25f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorBlockEnd", Percentage = 95f });
                    break;

                // === BOW PRESETS ===
                case AnimationType.BowShot:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorBowDraw", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorArrowNock", Percentage = 15f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorBowRelease", Percentage = 85f });
                    break;

                // === THROWABLE PRESETS ===
                case AnimationType.Throw:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorThrowChargeStart", Percentage = 0f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorThrowRelease", Percentage = 60f });
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "OnAnimatorThrowComplete", Percentage = 95f });
                    break;

                case AnimationType.Custom:
                    _eventConfigs.Add(new EventConfig { EventName = "ExecuteEvent", StringParameter = "CustomEvent", Percentage = 50f });
                    break;
            }
        }

        private void ApplyEventsToClips()
        {
            int totalAdded = 0;

            foreach (var clip in _selectedClips)
            {
                var existingEvents = clip.events.ToList();

                foreach (var config in _eventConfigs.Where(c => c.Enabled))
                {
                    int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);
                    int frame = config.PositionMode == FramePositionMode.Manual
                        ? config.ManualFrame
                        : Mathf.RoundToInt((config.Percentage / 100f) * totalFrames);

                    float time = frame / clip.frameRate;

                    var newEvent = new AnimationEvent
                    {
                        time = time,
                        functionName = config.EventName,
                        stringParameter = config.StringParameter
                    };

                    existingEvents.Add(newEvent);
                    totalAdded++;
                }

                AnimationUtility.SetAnimationEvents(clip, existingEvents.ToArray());
                EditorUtility.SetDirty(clip);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimEventGenerator] Added {totalAdded} events to {_selectedClips.Count} clip(s)");
        }

        private void ClearEventsFromClips()
        {
            foreach (var clip in _selectedClips)
            {
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                EditorUtility.SetDirty(clip);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimEventGenerator] Cleared events from {_selectedClips.Count} clip(s)");
        }
    }
}
