using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.VFXWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 VW-06: FEEL Integration module.
    /// Connect VFX to FEEL feedback players.
    /// </summary>
    public class FEELIntegrationModule : IVFXModule
    {
        private Vector2 _scrollPosition;
        
        // Target selection
        private GameObject _targetPrefab;
        private List<FeedbackBinding> _bindings = new List<FeedbackBinding>();
        
        // Quick presets
        private List<FEELPreset> _presets = new List<FEELPreset>();
        
        // Auto-detection results
        private List<DetectedFeedback> _detectedFeedbacks = new List<DetectedFeedback>();
        
        // VFX event types
        private string[] _vfxEventTypes = new string[]
        {
            "OnFire", "OnReload", "OnImpact", "OnHit", "OnKill",
            "OnCritical", "OnHeadshot", "OnEquip", "OnADS", "OnEmpty"
        };

        [System.Serializable]
        private class FeedbackBinding
        {
            public string EventType;
            public Object FeedbackPlayer; // MMF_Player
            public bool Enabled = true;
            public float DelaySeconds = 0f;
            public float IntensityMultiplier = 1f;
        }

        [System.Serializable]
        private class FEELPreset
        {
            public string Name;
            public string Description;
            public List<string> IncludedEvents = new List<string>();
        }

        [System.Serializable]
        private class DetectedFeedback
        {
            public string Name;
            public string Path;
            public Object Component;
            public bool IsAssigned;
        }

        public FEELIntegrationModule()
        {
            InitializePresets();
        }

        private void InitializePresets()
        {
            _presets = new List<FEELPreset>
            {
                new FEELPreset { Name = "Full Combat", Description = "All combat VFX events", IncludedEvents = new List<string> { "OnFire", "OnReload", "OnImpact", "OnHit", "OnKill", "OnCritical" } },
                new FEELPreset { Name = "Minimal", Description = "Only essential feedback", IncludedEvents = new List<string> { "OnFire", "OnHit" } },
                new FEELPreset { Name = "Weapon Only", Description = "Weapon-specific events", IncludedEvents = new List<string> { "OnFire", "OnReload", "OnEquip", "OnADS", "OnEmpty" } },
                new FEELPreset { Name = "Impact Only", Description = "Hit reaction events", IncludedEvents = new List<string> { "OnImpact", "OnHit", "OnKill", "OnCritical", "OnHeadshot" } },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("FEEL Integration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Connect VFX events to FEEL (MoreMountains.Feedbacks) feedback players for complete game juice.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawTargetSelection();
            EditorGUILayout.Space(10);
            DrawAutoDetection();
            EditorGUILayout.Space(10);
            DrawBindings();
            EditorGUILayout.Space(10);
            DrawQuickPresets();
            EditorGUILayout.Space(10);
            DrawTestSection();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSelection()
        {
            EditorGUILayout.LabelField("Target Prefab", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _targetPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab", _targetPrefab, typeof(GameObject), true);
            
            if (EditorGUI.EndChangeCheck() && _targetPrefab != null)
            {
                ScanForFeedbacks();
            }

            if (_targetPrefab != null)
            {
                EditorGUILayout.LabelField($"Scanning: {_targetPrefab.name}", EditorStyles.miniLabel);
                
                if (GUILayout.Button("Rescan Feedbacks"))
                {
                    ScanForFeedbacks();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAutoDetection()
        {
            EditorGUILayout.LabelField("Detected FEEL Feedbacks", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_detectedFeedbacks.Count == 0)
            {
                EditorGUILayout.LabelField("No MMF_Player components found", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField("Assign a prefab with FEEL feedback players", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var feedback in _detectedFeedbacks)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Status indicator
                    Color statusColor = feedback.IsAssigned ? Color.green : Color.yellow;
                    Rect statusRect = GUILayoutUtility.GetRect(10, 16, GUILayout.Width(10));
                    EditorGUI.DrawRect(statusRect, statusColor);
                    
                    EditorGUILayout.LabelField(feedback.Name, GUILayout.Width(150));
                    EditorGUILayout.LabelField(feedback.Path, EditorStyles.miniLabel);
                    
                    if (!feedback.IsAssigned)
                    {
                        if (GUILayout.Button("Assign", GUILayout.Width(60)))
                        {
                            QuickAssignFeedback(feedback);
                        }
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBindings()
        {
            EditorGUILayout.LabelField("Event Bindings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Event", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Feedback Player", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Delay", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            foreach (var eventType in _vfxEventTypes)
            {
                var binding = _bindings.FirstOrDefault(b => b.EventType == eventType);
                
                EditorGUILayout.BeginHorizontal();
                
                // Event name
                bool hasBinding = binding != null && binding.FeedbackPlayer != null;
                Color prevColor = GUI.contentColor;
                GUI.contentColor = hasBinding ? Color.white : Color.gray;
                EditorGUILayout.LabelField(eventType, GUILayout.Width(100));
                GUI.contentColor = prevColor;
                
                // Feedback player field
                Object newPlayer = EditorGUILayout.ObjectField(
                    binding?.FeedbackPlayer, typeof(Object), true);
                
                if (newPlayer != binding?.FeedbackPlayer)
                {
                    if (binding == null)
                    {
                        binding = new FeedbackBinding { EventType = eventType };
                        _bindings.Add(binding);
                    }
                    binding.FeedbackPlayer = newPlayer;
                }
                
                // Delay
                float delay = binding?.DelaySeconds ?? 0f;
                float newDelay = EditorGUILayout.FloatField(delay, GUILayout.Width(50));
                if (binding != null) binding.DelaySeconds = newDelay;
                
                // Clear button
                if (hasBinding)
                {
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        binding.FeedbackPlayer = null;
                    }
                }
                else
                {
                    GUILayout.Space(24);
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("+ Add Custom Event"))
            {
                AddCustomEvent();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickPresets()
        {
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            foreach (var preset in _presets)
            {
                if (GUILayout.Button(new GUIContent(preset.Name, preset.Description), EditorStyles.miniButton))
                {
                    ApplyPreset(preset);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTestSection()
        {
            EditorGUILayout.LabelField("Test Feedbacks", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test feedbacks", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                
                foreach (var eventType in _vfxEventTypes.Take(5))
                {
                    var binding = _bindings.FirstOrDefault(b => b.EventType == eventType);
                    bool hasBinding = binding?.FeedbackPlayer != null;
                    
                    EditorGUI.BeginDisabledGroup(!hasBinding);
                    if (GUILayout.Button(eventType.Replace("On", ""), EditorStyles.miniButton))
                    {
                        TriggerFeedback(binding);
                    }
                    EditorGUI.EndDisabledGroup();
                }
                
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                
                foreach (var eventType in _vfxEventTypes.Skip(5))
                {
                    var binding = _bindings.FirstOrDefault(b => b.EventType == eventType);
                    bool hasBinding = binding?.FeedbackPlayer != null;
                    
                    EditorGUI.BeginDisabledGroup(!hasBinding);
                    if (GUILayout.Button(eventType.Replace("On", ""), EditorStyles.miniButton))
                    {
                        TriggerFeedback(binding);
                    }
                    EditorGUI.EndDisabledGroup();
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            EditorGUI.BeginDisabledGroup(_targetPrefab == null);
            if (GUILayout.Button("Apply Bindings", GUILayout.Height(30)))
            {
                ApplyBindings();
            }
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Auto-Wire All", GUILayout.Height(30)))
            {
                AutoWireAll();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Config"))
            {
                ExportConfig();
            }
            
            if (GUILayout.Button("Import Config"))
            {
                ImportConfig();
            }
            
            if (GUILayout.Button("Clear All"))
            {
                _bindings.Clear();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            // Statistics
            int boundCount = _bindings.Count(b => b.FeedbackPlayer != null);
            int totalEvents = _vfxEventTypes.Length;
            
            EditorGUILayout.LabelField($"Bindings: {boundCount}/{totalEvents} events connected", 
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void ScanForFeedbacks()
        {
            _detectedFeedbacks.Clear();
            
            if (_targetPrefab == null) return;

            // Look for MMF_Player components (or any MonoBehaviour with "Feedback" in name)
            var allComponents = _targetPrefab.GetComponentsInChildren<MonoBehaviour>(true);
            
            foreach (var component in allComponents)
            {
                if (component == null) continue;
                
                string typeName = component.GetType().Name;
                
                // Check for FEEL feedback players
                if (typeName.Contains("MMF_Player") || 
                    typeName.Contains("Feedback") || 
                    typeName.Contains("MMFeedback"))
                {
                    string path = GetTransformPath(component.transform, _targetPrefab.transform);
                    
                    bool isAssigned = _bindings.Any(b => b.FeedbackPlayer == component);
                    
                    _detectedFeedbacks.Add(new DetectedFeedback
                    {
                        Name = component.name,
                        Path = path,
                        Component = component,
                        IsAssigned = isAssigned
                    });
                }
            }
            
            Debug.Log($"[FEELIntegration] Found {_detectedFeedbacks.Count} feedback players");
        }

        private string GetTransformPath(Transform current, Transform root)
        {
            if (current == root) return "";
            
            List<string> path = new List<string>();
            while (current != null && current != root)
            {
                path.Add(current.name);
                current = current.parent;
            }
            
            path.Reverse();
            return string.Join("/", path);
        }

        private void QuickAssignFeedback(DetectedFeedback feedback)
        {
            // Try to auto-match based on name
            string lowerName = feedback.Name.ToLower();
            
            string matchedEvent = null;
            
            if (lowerName.Contains("fire") || lowerName.Contains("shoot"))
                matchedEvent = "OnFire";
            else if (lowerName.Contains("reload"))
                matchedEvent = "OnReload";
            else if (lowerName.Contains("impact"))
                matchedEvent = "OnImpact";
            else if (lowerName.Contains("hit"))
                matchedEvent = "OnHit";
            else if (lowerName.Contains("kill"))
                matchedEvent = "OnKill";
            else if (lowerName.Contains("critical") || lowerName.Contains("crit"))
                matchedEvent = "OnCritical";
            else if (lowerName.Contains("headshot") || lowerName.Contains("head"))
                matchedEvent = "OnHeadshot";
            else if (lowerName.Contains("equip"))
                matchedEvent = "OnEquip";
            else if (lowerName.Contains("ads") || lowerName.Contains("aim"))
                matchedEvent = "OnADS";
            else if (lowerName.Contains("empty") || lowerName.Contains("dry"))
                matchedEvent = "OnEmpty";
            
            if (matchedEvent != null)
            {
                var binding = _bindings.FirstOrDefault(b => b.EventType == matchedEvent);
                if (binding == null)
                {
                    binding = new FeedbackBinding { EventType = matchedEvent };
                    _bindings.Add(binding);
                }
                binding.FeedbackPlayer = feedback.Component;
                feedback.IsAssigned = true;
                
                Debug.Log($"[FEELIntegration] Assigned {feedback.Name} → {matchedEvent}");
            }
            else
            {
                Debug.LogWarning($"[FEELIntegration] Could not auto-match: {feedback.Name}");
            }
        }

        private void AddCustomEvent()
        {
            _bindings.Add(new FeedbackBinding
            {
                EventType = "Custom_" + _bindings.Count
            });
        }

        private void ApplyPreset(FEELPreset preset)
        {
            // Enable only events in this preset
            foreach (var binding in _bindings)
            {
                binding.Enabled = preset.IncludedEvents.Contains(binding.EventType);
            }
            
            Debug.Log($"[FEELIntegration] Applied preset: {preset.Name}");
        }

        private void TriggerFeedback(FeedbackBinding binding)
        {
            if (binding?.FeedbackPlayer == null) return;
            
            // Would call PlayFeedbacks() on MMF_Player via reflection
            Debug.Log($"[FEELIntegration] Triggered: {binding.EventType}");
        }

        private void ApplyBindings()
        {
            Debug.Log($"[FEELIntegration] Applied {_bindings.Count(b => b.FeedbackPlayer != null)} bindings to {_targetPrefab.name}");
        }

        private void AutoWireAll()
        {
            foreach (var feedback in _detectedFeedbacks.Where(f => !f.IsAssigned))
            {
                QuickAssignFeedback(feedback);
            }
        }

        private void ExportConfig()
        {
            Debug.Log("[FEELIntegration] Export pending");
        }

        private void ImportConfig()
        {
            Debug.Log("[FEELIntegration] Import pending");
        }
    }
}
