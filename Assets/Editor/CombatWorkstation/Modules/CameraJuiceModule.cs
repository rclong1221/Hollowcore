using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CB-02: Camera Juice module.
    /// Cinemachine impulse presets, shake library, FOV punch config.
    /// </summary>
    public class CameraJuiceModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        
        // Impulse Source reference
        private CinemachineImpulseSource _selectedSource;
        private GameObject _cameraRig;
        
        // Shake presets
        private List<ShakePreset> _presets = new List<ShakePreset>();
        private int _selectedPresetIndex = -1;
        private string _newPresetName = "NewShake";
        
        // Current shake settings
        private float _shakeAmplitude = 1f;
        private float _shakeDuration = 0.2f;
        private float _shakeFrequency = 10f;
        private Vector3 _shakeDirection = Vector3.up;
        private AnimationCurve _shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        // FOV Punch settings
        private bool _enableFovPunch = true;
        private float _fovPunchAmount = 5f;
        private float _fovPunchDuration = 0.1f;
        private AnimationCurve _fovCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1);
        
        // Preview
        private bool _isPreviewing = false;

        [System.Serializable]
        private class ShakePreset
        {
            public string Name;
            public float Amplitude;
            public float Duration;
            public float Frequency;
            public Vector3 Direction;
            public ShakeType Type;
        }

        private enum ShakeType { WeaponFire, Hit, Explosion, Landing, Melee, Custom }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Camera Juice", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure camera shake presets, FOV punch, and Cinemachine impulse settings.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawCameraSetup();
            EditorGUILayout.Space(10);
            DrawShakePresets();
            EditorGUILayout.Space(10);
            DrawShakeConfig();
            EditorGUILayout.Space(10);
            DrawFovPunch();
            EditorGUILayout.Space(10);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCameraSetup()
        {
            EditorGUILayout.LabelField("Camera Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _cameraRig = (GameObject)EditorGUILayout.ObjectField(
                "Camera Rig", _cameraRig, typeof(GameObject), true);

            _selectedSource = (CinemachineImpulseSource)EditorGUILayout.ObjectField(
                "Impulse Source", _selectedSource, typeof(CinemachineImpulseSource), true);

            if (_cameraRig != null && _selectedSource == null)
            {
                _selectedSource = _cameraRig.GetComponentInChildren<CinemachineImpulseSource>();
            }

            if (_selectedSource == null)
            {
                EditorGUILayout.HelpBox(
                    "No CinemachineImpulseSource found. Add one to your camera rig.",
                    MessageType.Warning);
                
                EditorGUI.BeginDisabledGroup(_cameraRig == null);
                if (GUILayout.Button("Add Impulse Source"))
                {
                    AddImpulseSource();
                }
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.LabelField($"Impulse Source: {_selectedSource.gameObject.name}", 
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShakePresets()
        {
            EditorGUILayout.LabelField("Shake Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Built-in presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Weapon Fire"))
            {
                _shakeAmplitude = 0.3f;
                _shakeDuration = 0.1f;
                _shakeFrequency = 15f;
                _shakeDirection = new Vector3(0, 1, 0.2f);
            }
            if (GUILayout.Button("Hit Received"))
            {
                _shakeAmplitude = 0.8f;
                _shakeDuration = 0.15f;
                _shakeFrequency = 12f;
                _shakeDirection = Vector3.right;
            }
            if (GUILayout.Button("Explosion"))
            {
                _shakeAmplitude = 2f;
                _shakeDuration = 0.5f;
                _shakeFrequency = 8f;
                _shakeDirection = Vector3.one;
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Landing"))
            {
                _shakeAmplitude = 0.5f;
                _shakeDuration = 0.2f;
                _shakeFrequency = 10f;
                _shakeDirection = Vector3.down;
            }
            if (GUILayout.Button("Melee Swing"))
            {
                _shakeAmplitude = 0.4f;
                _shakeDuration = 0.12f;
                _shakeFrequency = 20f;
                _shakeDirection = new Vector3(1, 0.5f, 0);
            }
            if (GUILayout.Button("Headshot"))
            {
                _shakeAmplitude = 1.2f;
                _shakeDuration = 0.18f;
                _shakeFrequency = 18f;
                _shakeDirection = Vector3.up;
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Custom presets
            EditorGUILayout.LabelField($"Saved Presets ({_presets.Count})", EditorStyles.miniLabel);
            
            for (int i = 0; i < _presets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button(_presets[i].Name, EditorStyles.miniButton))
                {
                    LoadPreset(_presets[i]);
                    _selectedPresetIndex = i;
                }
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _presets.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            _newPresetName = EditorGUILayout.TextField(_newPresetName);
            if (GUILayout.Button("Save Current", GUILayout.Width(100)))
            {
                SaveCurrentAsPreset(_newPresetName);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawShakeConfig()
        {
            EditorGUILayout.LabelField("Shake Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _shakeAmplitude = EditorGUILayout.Slider("Amplitude", _shakeAmplitude, 0f, 5f);
            _shakeDuration = EditorGUILayout.Slider("Duration", _shakeDuration, 0.01f, 2f);
            _shakeFrequency = EditorGUILayout.Slider("Frequency", _shakeFrequency, 1f, 30f);
            _shakeDirection = EditorGUILayout.Vector3Field("Direction", _shakeDirection);
            _shakeCurve = EditorGUILayout.CurveField("Decay Curve", _shakeCurve);

            // Visual representation
            Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            DrawShakePreview(previewRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawShakePreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            // Draw shake waveform
            Handles.color = Color.cyan;
            
            float midY = rect.center.y;
            float prevY = midY;
            
            for (int i = 0; i < rect.width; i++)
            {
                float t = i / rect.width;
                float curveValue = _shakeCurve.Evaluate(t);
                float wave = Mathf.Sin(t * _shakeFrequency * Mathf.PI * 2) * _shakeAmplitude * curveValue;
                float y = midY + wave * (rect.height * 0.4f);
                
                if (i > 0)
                {
                    Handles.DrawLine(
                        new Vector3(rect.x + i - 1, prevY, 0),
                        new Vector3(rect.x + i, y, 0)
                    );
                }
                prevY = y;
            }

            // Draw center line
            Handles.color = new Color(1, 1, 1, 0.3f);
            Handles.DrawLine(
                new Vector3(rect.x, midY, 0),
                new Vector3(rect.xMax, midY, 0)
            );
        }

        private void DrawFovPunch()
        {
            EditorGUILayout.LabelField("FOV Punch", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableFovPunch = EditorGUILayout.Toggle("Enable FOV Punch", _enableFovPunch);

            if (_enableFovPunch)
            {
                EditorGUI.indentLevel++;
                _fovPunchAmount = EditorGUILayout.Slider("Punch Amount (deg)", _fovPunchAmount, 0f, 20f);
                _fovPunchDuration = EditorGUILayout.Slider("Duration", _fovPunchDuration, 0.01f, 0.5f);
                _fovCurve = EditorGUILayout.CurveField("Punch Curve", _fovCurve);
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    $"FOV will briefly change by ±{_fovPunchAmount:F1}° over {_fovPunchDuration:F2}s",
                    MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(_selectedSource == null);
            
            if (GUILayout.Button("Apply to Impulse Source", GUILayout.Height(30)))
            {
                ApplyToImpulseSource();
            }
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isPreviewing ? Color.red : Color.green;
            
            if (GUILayout.Button(_isPreviewing ? "Stop Preview" : "Preview Shake", GUILayout.Height(30)))
            {
                TogglePreview();
            }
            
            GUI.backgroundColor = prevColor;
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Presets"))
            {
                ExportPresets();
            }
            
            if (GUILayout.Button("Import Presets"))
            {
                ImportPresets();
            }
            
            if (GUILayout.Button("Create Shake Library SO"))
            {
                CreateShakeLibrary();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void AddImpulseSource()
        {
            if (_cameraRig == null) return;

            var source = _cameraRig.AddComponent<CinemachineImpulseSource>();
            _selectedSource = source;
            
            Debug.Log($"[CameraJuice] Added CinemachineImpulseSource to {_cameraRig.name}");
        }

        private void LoadPreset(ShakePreset preset)
        {
            _shakeAmplitude = preset.Amplitude;
            _shakeDuration = preset.Duration;
            _shakeFrequency = preset.Frequency;
            _shakeDirection = preset.Direction;
        }

        private void SaveCurrentAsPreset(string name)
        {
            _presets.Add(new ShakePreset
            {
                Name = name,
                Amplitude = _shakeAmplitude,
                Duration = _shakeDuration,
                Frequency = _shakeFrequency,
                Direction = _shakeDirection,
                Type = ShakeType.Custom
            });
            
            Debug.Log($"[CameraJuice] Saved preset: {name}");
        }

        private void ApplyToImpulseSource()
        {
            if (_selectedSource == null) return;

            Undo.RecordObject(_selectedSource, "Apply Shake Settings");

            _selectedSource.ImpulseDefinition.ImpulseDuration = _shakeDuration;
            _selectedSource.DefaultVelocity = _shakeDirection.normalized * _shakeAmplitude;

            EditorUtility.SetDirty(_selectedSource);
            Debug.Log("[CameraJuice] Applied settings to impulse source");
        }

        private void TogglePreview()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[CameraJuice] Preview requires Play mode");
                return;
            }

            if (_selectedSource != null)
            {
                _selectedSource.GenerateImpulse(_shakeDirection.normalized * _shakeAmplitude);
                Debug.Log("[CameraJuice] Generated test impulse");
            }
        }

        private void ExportPresets()
        {
            var data = new ShakePresetCollection { Presets = _presets };
            string json = JsonUtility.ToJson(data, true);
            
            string path = EditorUtility.SaveFilePanel("Export Shake Presets", "", "ShakePresets", "json");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[CameraJuice] Exported {_presets.Count} presets");
            }
        }

        private void ImportPresets()
        {
            string path = EditorUtility.OpenFilePanel("Import Shake Presets", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                string json = System.IO.File.ReadAllText(path);
                var data = JsonUtility.FromJson<ShakePresetCollection>(json);
                _presets = data.Presets ?? new List<ShakePreset>();
                Debug.Log($"[CameraJuice] Imported {_presets.Count} presets");
            }
        }

        private void CreateShakeLibrary()
        {
            // Would create a ScriptableObject for shake presets
            Debug.Log("[CameraJuice] ShakeLibrary ScriptableObject creation pending");
        }

        [System.Serializable]
        private class ShakePresetCollection
        {
            public List<ShakePreset> Presets;
        }
    }
}
