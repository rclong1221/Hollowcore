using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 AW-04: Distance Attenuation module.
    /// Attenuation curve preview, spatial blend settings.
    /// </summary>
    public class DistanceAttenModule : IAudioModule
    {
        private Vector2 _scrollPosition;
        
        // Target audio source
        private AudioSource _targetSource;
        
        // Attenuation settings
        private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;
        private float _minDistance = 1f;
        private float _maxDistance = 50f;
        private AnimationCurve _customRolloff = AnimationCurve.Linear(0, 1, 1, 0);
        
        // Spatial settings
        private float _spatialBlend = 1f;
        private float _spread = 0f;
        private float _dopplerLevel = 1f;
        
        // Reverb settings
        private float _reverbZoneMix = 1f;
        
        // Presets
        private List<AttenuationPreset> _presets = new List<AttenuationPreset>();
        private string _newPresetName = "NewPreset";

        [System.Serializable]
        private class AttenuationPreset
        {
            public string Name;
            public AudioRolloffMode RolloffMode;
            public float MinDistance;
            public float MaxDistance;
            public float SpatialBlend;
            public float Spread;
        }

        public DistanceAttenModule()
        {
            InitializeDefaultPresets();
        }

        private void InitializeDefaultPresets()
        {
            _presets = new List<AttenuationPreset>
            {
                new AttenuationPreset { Name = "Pistol", RolloffMode = AudioRolloffMode.Logarithmic, MinDistance = 2f, MaxDistance = 30f, SpatialBlend = 1f, Spread = 0f },
                new AttenuationPreset { Name = "Rifle", RolloffMode = AudioRolloffMode.Logarithmic, MinDistance = 5f, MaxDistance = 100f, SpatialBlend = 1f, Spread = 10f },
                new AttenuationPreset { Name = "Shotgun", RolloffMode = AudioRolloffMode.Logarithmic, MinDistance = 3f, MaxDistance = 60f, SpatialBlend = 1f, Spread = 20f },
                new AttenuationPreset { Name = "Explosion", RolloffMode = AudioRolloffMode.Linear, MinDistance = 10f, MaxDistance = 200f, SpatialBlend = 1f, Spread = 180f },
                new AttenuationPreset { Name = "Footstep", RolloffMode = AudioRolloffMode.Logarithmic, MinDistance = 1f, MaxDistance = 15f, SpatialBlend = 1f, Spread = 0f },
                new AttenuationPreset { Name = "Ambient", RolloffMode = AudioRolloffMode.Linear, MinDistance = 5f, MaxDistance = 50f, SpatialBlend = 0.5f, Spread = 180f },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Distance Attenuation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure audio falloff curves and spatial blend settings for 3D positioned sounds.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawTargetSelection();
            EditorGUILayout.Space(10);
            DrawAttenuationSettings();
            EditorGUILayout.Space(10);
            DrawAttenuationPreview();
            EditorGUILayout.Space(10);
            DrawSpatialSettings();
            EditorGUILayout.Space(10);
            DrawPresets();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSelection()
        {
            EditorGUILayout.LabelField("Target Audio Source", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _targetSource = (AudioSource)EditorGUILayout.ObjectField(
                "Audio Source", _targetSource, typeof(AudioSource), true);

            if (_targetSource == null && Selection.activeGameObject != null)
            {
                var source = Selection.activeGameObject.GetComponent<AudioSource>();
                if (source != null)
                {
                    EditorGUILayout.LabelField($"Found AudioSource on selection: {Selection.activeGameObject.name}", 
                        EditorStyles.miniLabel);
                    
                    if (GUILayout.Button("Use Selection"))
                    {
                        _targetSource = source;
                        LoadFromSource(source);
                    }
                }
            }

            if (_targetSource != null)
            {
                if (GUILayout.Button("Load Settings From Source"))
                {
                    LoadFromSource(_targetSource);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAttenuationSettings()
        {
            EditorGUILayout.LabelField("Attenuation Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _rolloffMode = (AudioRolloffMode)EditorGUILayout.EnumPopup("Rolloff Mode", _rolloffMode);
            
            EditorGUILayout.Space(5);
            
            _minDistance = EditorGUILayout.FloatField("Min Distance", _minDistance);
            _maxDistance = EditorGUILayout.FloatField("Max Distance", _maxDistance);
            
            _minDistance = Mathf.Max(0.1f, _minDistance);
            _maxDistance = Mathf.Max(_minDistance + 0.1f, _maxDistance);

            if (_rolloffMode == AudioRolloffMode.Custom)
            {
                EditorGUILayout.Space(5);
                _customRolloff = EditorGUILayout.CurveField("Custom Rolloff", _customRolloff);
            }

            // Distance info
            float range = _maxDistance - _minDistance;
            EditorGUILayout.HelpBox(
                $"Full volume within {_minDistance:F1}m, " +
                $"inaudible beyond {_maxDistance:F1}m ({range:F1}m falloff range)",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private void DrawAttenuationPreview()
        {
            EditorGUILayout.LabelField("Attenuation Curve Preview", EditorStyles.boldLabel);
            
            Rect curveRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(120), GUILayout.ExpandWidth(true));
            
            DrawAttenuationCurve(curveRect);
        }

        private void DrawAttenuationCurve(Rect rect)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            // Grid lines
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            for (int i = 1; i < 4; i++)
            {
                float y = rect.y + rect.height * (i / 4f);
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }

            // Distance markers
            float minX = rect.x + (_minDistance / _maxDistance) * rect.width;
            
            // Min distance zone (full volume)
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, minX - rect.x, rect.height), 
                new Color(0.3f, 0.5f, 0.3f, 0.3f));

            // Draw attenuation curve
            Handles.color = Color.green;
            
            int segments = 100;
            Vector3 prevPoint = Vector3.zero;
            
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float distance = t * _maxDistance;
                float volume = CalculateAttenuation(distance);
                
                float x = rect.x + t * rect.width;
                float y = rect.yMax - volume * rect.height;
                
                Vector3 point = new Vector3(x, y, 0);
                
                if (i > 0)
                {
                    Handles.DrawLine(prevPoint, point);
                }
                
                prevPoint = point;
            }

            // Min distance line
            Handles.color = Color.yellow;
            Handles.DrawLine(new Vector3(minX, rect.y), new Vector3(minX, rect.yMax));

            // Labels
            GUI.Label(new Rect(rect.x + 5, rect.y + 2, 50, 16), "100%", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 5, rect.yMax - 16, 50, 16), "0%", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 5, rect.yMax + 2, 50, 16), "0m", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.xMax - 45, rect.yMax + 2, 50, 16), $"{_maxDistance:F0}m", EditorStyles.miniLabel);
            
            // Min distance label
            GUI.Label(new Rect(minX - 15, rect.y + 2, 60, 16), $"{_minDistance:F1}m", EditorStyles.miniLabel);
        }

        private float CalculateAttenuation(float distance)
        {
            if (distance <= _minDistance) return 1f;
            if (distance >= _maxDistance) return 0f;

            float normalizedDistance = (distance - _minDistance) / (_maxDistance - _minDistance);

            switch (_rolloffMode)
            {
                case AudioRolloffMode.Logarithmic:
                    return 1f / (1f + normalizedDistance * 9f);
                    
                case AudioRolloffMode.Linear:
                    return 1f - normalizedDistance;
                    
                case AudioRolloffMode.Custom:
                    return _customRolloff.Evaluate(normalizedDistance);
                    
                default:
                    return 1f - normalizedDistance;
            }
        }

        private void DrawSpatialSettings()
        {
            EditorGUILayout.LabelField("Spatial Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _spatialBlend = EditorGUILayout.Slider("Spatial Blend", _spatialBlend, 0f, 1f);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("2D", EditorStyles.miniLabel, GUILayout.Width(20));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("3D", EditorStyles.miniLabel, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            _spread = EditorGUILayout.Slider("Spread (degrees)", _spread, 0f, 360f);
            _dopplerLevel = EditorGUILayout.Slider("Doppler Level", _dopplerLevel, 0f, 5f);
            _reverbZoneMix = EditorGUILayout.Slider("Reverb Zone Mix", _reverbZoneMix, 0f, 1.1f);

            // Visual representation of spread
            if (_spread > 0)
            {
                Rect spreadRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(60), GUILayout.ExpandWidth(true));
                DrawSpreadVisualization(spreadRect);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpreadVisualization(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.4f;
            
            // Draw spread cone
            float halfSpread = _spread * 0.5f * Mathf.Deg2Rad;
            
            Handles.color = new Color(0.3f, 0.6f, 0.3f, 0.5f);
            Handles.DrawSolidArc(center, Vector3.forward, 
                Quaternion.Euler(0, 0, -_spread * 0.5f) * Vector3.up, 
                _spread, radius);
            
            // Draw direction line
            Handles.color = Color.green;
            Handles.DrawLine(center, center + Vector2.up * radius);
        }

        private void DrawPresets()
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            foreach (var preset in _presets)
            {
                if (GUILayout.Button(preset.Name, EditorStyles.miniButton))
                {
                    ApplyPreset(preset);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            _newPresetName = EditorGUILayout.TextField(_newPresetName);
            if (GUILayout.Button("Save Preset", GUILayout.Width(100)))
            {
                SaveCurrentAsPreset();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            EditorGUI.BeginDisabledGroup(_targetSource == null);
            if (GUILayout.Button("Apply to Source", GUILayout.Height(30)))
            {
                ApplyToSource();
            }
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Apply to Selection", GUILayout.Height(30)))
            {
                ApplyToSelection();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void LoadFromSource(AudioSource source)
        {
            _rolloffMode = source.rolloffMode;
            _minDistance = source.minDistance;
            _maxDistance = source.maxDistance;
            _spatialBlend = source.spatialBlend;
            _spread = source.spread;
            _dopplerLevel = source.dopplerLevel;
            _reverbZoneMix = source.reverbZoneMix;
            
            if (_rolloffMode == AudioRolloffMode.Custom)
            {
                _customRolloff = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
            }
        }

        private void ApplyPreset(AttenuationPreset preset)
        {
            _rolloffMode = preset.RolloffMode;
            _minDistance = preset.MinDistance;
            _maxDistance = preset.MaxDistance;
            _spatialBlend = preset.SpatialBlend;
            _spread = preset.Spread;
            
            Debug.Log($"[DistanceAtten] Applied preset: {preset.Name}");
        }

        private void SaveCurrentAsPreset()
        {
            _presets.Add(new AttenuationPreset
            {
                Name = _newPresetName,
                RolloffMode = _rolloffMode,
                MinDistance = _minDistance,
                MaxDistance = _maxDistance,
                SpatialBlend = _spatialBlend,
                Spread = _spread
            });
            
            Debug.Log($"[DistanceAtten] Saved preset: {_newPresetName}");
        }

        private void ApplyToSource()
        {
            if (_targetSource == null) return;

            Undo.RecordObject(_targetSource, "Apply Attenuation Settings");
            
            _targetSource.rolloffMode = _rolloffMode;
            _targetSource.minDistance = _minDistance;
            _targetSource.maxDistance = _maxDistance;
            _targetSource.spatialBlend = _spatialBlend;
            _targetSource.spread = _spread;
            _targetSource.dopplerLevel = _dopplerLevel;
            _targetSource.reverbZoneMix = _reverbZoneMix;
            
            if (_rolloffMode == AudioRolloffMode.Custom)
            {
                _targetSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _customRolloff);
            }
            
            EditorUtility.SetDirty(_targetSource);
            Debug.Log($"[DistanceAtten] Applied settings to {_targetSource.gameObject.name}");
        }

        private void ApplyToSelection()
        {
            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                var source = go.GetComponent<AudioSource>();
                if (source != null)
                {
                    Undo.RecordObject(source, "Apply Attenuation Settings");
                    
                    source.rolloffMode = _rolloffMode;
                    source.minDistance = _minDistance;
                    source.maxDistance = _maxDistance;
                    source.spatialBlend = _spatialBlend;
                    source.spread = _spread;
                    source.dopplerLevel = _dopplerLevel;
                    source.reverbZoneMix = _reverbZoneMix;
                    
                    EditorUtility.SetDirty(source);
                    count++;
                }
            }
            
            Debug.Log($"[DistanceAtten] Applied settings to {count} AudioSources");
        }
    }
}
