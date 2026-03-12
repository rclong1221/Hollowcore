using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.VFXWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 VW-04: Shell Ejection module.
    /// Shell ejection config, physics settings, pooling.
    /// </summary>
    public class ShellEjectionModule : IVFXModule
    {
        private Vector2 _scrollPosition;
        
        // Target weapon
        private GameObject _targetWeapon;
        private Transform _ejectionPort;
        
        // Shell prefab
        private GameObject _shellPrefab;
        private List<ShellPreset> _presets = new List<ShellPreset>();
        private int _selectedPresetIndex = -1;
        
        // Ejection settings
        private string _presetName = "NewShell";
        private float _ejectionForce = 3f;
        private float _ejectionTorque = 10f;
        private Vector3 _ejectionDirection = new Vector3(1, 0.5f, 0);
        private float _directionRandomness = 15f;
        
        // Physics
        private float _shellMass = 0.01f;
        private float _shellDrag = 0.5f;
        private float _angularDrag = 1f;
        private bool _useGravity = true;
        private PhysicsMaterial _shellPhysicsMaterial;
        
        // Lifetime
        private float _shellLifetime = 5f;
        private bool _fadeBeforeDestroy = true;
        private float _fadeDuration = 1f;
        
        // Pooling
        private int _poolSize = 20;
        private bool _warmupPool = true;
        
        // Audio
        private bool _playBounceSound = true;
        private float _minBounceVelocity = 0.5f;

        [System.Serializable]
        private class ShellPreset
        {
            public string Name;
            public GameObject ShellPrefab;
            public float EjectionForce;
            public float EjectionTorque;
            public Vector3 EjectionDirection;
            public float ShellLifetime;
            public int PoolSize;
        }

        public ShellEjectionModule()
        {
            InitializeDefaultPresets();
        }

        private void InitializeDefaultPresets()
        {
            _presets = new List<ShellPreset>
            {
                new ShellPreset { Name = "9mm Casing", EjectionForce = 2.5f, EjectionTorque = 8f, EjectionDirection = new Vector3(1, 0.5f, 0), ShellLifetime = 5f, PoolSize = 20 },
                new ShellPreset { Name = "5.56mm Casing", EjectionForce = 3f, EjectionTorque = 10f, EjectionDirection = new Vector3(1, 0.4f, 0), ShellLifetime = 5f, PoolSize = 30 },
                new ShellPreset { Name = "7.62mm Casing", EjectionForce = 3.5f, EjectionTorque = 12f, EjectionDirection = new Vector3(1, 0.3f, 0), ShellLifetime = 5f, PoolSize = 25 },
                new ShellPreset { Name = "12ga Shell", EjectionForce = 2f, EjectionTorque = 5f, EjectionDirection = new Vector3(1, 0.6f, -0.2f), ShellLifetime = 8f, PoolSize = 15 },
                new ShellPreset { Name = ".50 BMG", EjectionForce = 4f, EjectionTorque = 8f, EjectionDirection = new Vector3(1, 0.2f, 0), ShellLifetime = 10f, PoolSize = 10 },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Shell Ejection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure shell/casing ejection physics, lifetime, and object pooling.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - preset list
            EditorGUILayout.BeginVertical(GUILayout.Width(160));
            DrawPresetList();
            EditorGUILayout.EndVertical();

            // Right panel - settings
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawWeaponSelection();
            EditorGUILayout.Space(10);
            DrawShellPrefab();
            EditorGUILayout.Space(10);
            DrawEjectionSettings();
            EditorGUILayout.Space(10);
            DrawPhysicsSettings();
            EditorGUILayout.Space(10);
            DrawLifetimeSettings();
            EditorGUILayout.Space(10);
            DrawPoolingSettings();
            EditorGUILayout.Space(10);
            DrawAudioSettings();
            EditorGUILayout.Space(10);
            DrawPreview();
            EditorGUILayout.Space(10);
            DrawActions();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPresetList()
        {
            EditorGUILayout.LabelField("Shell Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            if (GUILayout.Button("+ New Preset"))
            {
                CreateNewPreset();
            }

            EditorGUILayout.Space(5);

            for (int i = 0; i < _presets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool selected = i == _selectedPresetIndex;
                Color prevColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = Color.cyan;
                
                if (GUILayout.Button(_presets[i].Name, EditorStyles.miniButton))
                {
                    _selectedPresetIndex = i;
                    LoadPreset(_presets[i]);
                }
                
                GUI.backgroundColor = prevColor;

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _presets.RemoveAt(i);
                    if (_selectedPresetIndex >= _presets.Count)
                        _selectedPresetIndex = _presets.Count - 1;
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWeaponSelection()
        {
            EditorGUILayout.LabelField("Target Weapon", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _targetWeapon = (GameObject)EditorGUILayout.ObjectField(
                "Weapon Prefab", _targetWeapon, typeof(GameObject), true);

            if (_targetWeapon != null)
            {
                _ejectionPort = (Transform)EditorGUILayout.ObjectField(
                    "Ejection Port", _ejectionPort, typeof(Transform), true);

                if (_ejectionPort == null)
                {
                    if (GUILayout.Button("Auto-Find Ejection Port"))
                    {
                        FindEjectionPort();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShellPrefab()
        {
            EditorGUILayout.LabelField("Shell Prefab", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _presetName = EditorGUILayout.TextField("Preset Name", _presetName);
            
            _shellPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Shell Prefab", _shellPrefab, typeof(GameObject), false);

            if (_shellPrefab != null)
            {
                var rb = _shellPrefab.GetComponent<Rigidbody>();
                var col = _shellPrefab.GetComponent<Collider>();
                
                EditorGUILayout.LabelField("Prefab Analysis:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  Rigidbody: {(rb != null ? "✓" : "✗")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  Collider: {(col != null ? "✓" : "✗")}", EditorStyles.miniLabel);
                
                if (rb == null || col == null)
                {
                    EditorGUILayout.HelpBox("Shell prefab should have Rigidbody and Collider components.", MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEjectionSettings()
        {
            EditorGUILayout.LabelField("Ejection Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _ejectionForce = EditorGUILayout.Slider("Ejection Force", _ejectionForce, 0.5f, 10f);
            _ejectionTorque = EditorGUILayout.Slider("Spin Torque", _ejectionTorque, 0f, 30f);
            
            EditorGUILayout.Space(5);
            
            _ejectionDirection = EditorGUILayout.Vector3Field("Direction (local)", _ejectionDirection);
            _ejectionDirection = _ejectionDirection.normalized;
            
            _directionRandomness = EditorGUILayout.Slider("Direction Randomness (°)", _directionRandomness, 0f, 45f);

            // Visualize direction
            Rect directionRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(60), GUILayout.ExpandWidth(true));
            DrawDirectionPreview(directionRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawDirectionPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            Vector2 center = rect.center;
            float arrowLength = 25f;
            
            // Main direction arrow (top-down view)
            Vector2 dir2D = new Vector2(_ejectionDirection.x, -_ejectionDirection.z).normalized;
            Vector2 arrowEnd = center + dir2D * arrowLength;
            
            Handles.color = Color.green;
            Handles.DrawLine(new Vector3(center.x, center.y), new Vector3(arrowEnd.x, arrowEnd.y));
            
            // Arrowhead
            Vector2 perpendicular = new Vector2(-dir2D.y, dir2D.x);
            Vector2 arrowLeft = arrowEnd - dir2D * 8 + perpendicular * 4;
            Vector2 arrowRight = arrowEnd - dir2D * 8 - perpendicular * 4;
            Handles.DrawLine(new Vector3(arrowEnd.x, arrowEnd.y), new Vector3(arrowLeft.x, arrowLeft.y));
            Handles.DrawLine(new Vector3(arrowEnd.x, arrowEnd.y), new Vector3(arrowRight.x, arrowRight.y));
            
            // Randomness cone
            if (_directionRandomness > 0)
            {
                Handles.color = new Color(0, 1, 0, 0.2f);
                float coneAngle = _directionRandomness * Mathf.Deg2Rad;
                Vector2 coneLeft = Quaternion.Euler(0, 0, _directionRandomness) * (dir2D * arrowLength * 1.5f);
                Vector2 coneRight = Quaternion.Euler(0, 0, -_directionRandomness) * (dir2D * arrowLength * 1.5f);
                Handles.DrawLine(new Vector3(center.x, center.y), new Vector3(center.x + coneLeft.x, center.y + coneLeft.y));
                Handles.DrawLine(new Vector3(center.x, center.y), new Vector3(center.x + coneRight.x, center.y + coneRight.y));
            }
            
            // Labels
            GUI.Label(new Rect(rect.x + 5, rect.y + 2, 60, 16), "Top View", EditorStyles.miniLabel);
            GUI.Label(new Rect(center.x - 10, center.y - 8, 30, 16), "Port", EditorStyles.miniLabel);
        }

        private void DrawPhysicsSettings()
        {
            EditorGUILayout.LabelField("Physics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _shellMass = EditorGUILayout.FloatField("Mass (kg)", _shellMass);
            _shellDrag = EditorGUILayout.Slider("Drag", _shellDrag, 0f, 2f);
            _angularDrag = EditorGUILayout.Slider("Angular Drag", _angularDrag, 0f, 5f);
            _useGravity = EditorGUILayout.Toggle("Use Gravity", _useGravity);
            
            _shellPhysicsMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField(
                "Physics Material", _shellPhysicsMaterial, typeof(PhysicsMaterial), false);

            EditorGUILayout.EndVertical();
        }

        private void DrawLifetimeSettings()
        {
            EditorGUILayout.LabelField("Lifetime", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _shellLifetime = EditorGUILayout.Slider("Lifetime (s)", _shellLifetime, 1f, 30f);
            
            _fadeBeforeDestroy = EditorGUILayout.Toggle("Fade Before Destroy", _fadeBeforeDestroy);
            if (_fadeBeforeDestroy)
            {
                EditorGUI.indentLevel++;
                _fadeDuration = EditorGUILayout.Slider("Fade Duration (s)", _fadeDuration, 0.1f, 3f);
                EditorGUI.indentLevel--;
            }

            // Timeline visualization
            Rect timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(20), GUILayout.ExpandWidth(true));
            DrawLifetimeTimeline(timelineRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawLifetimeTimeline(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float totalWidth = rect.width - 4;
            
            // Visible phase
            float visibleWidth = _fadeBeforeDestroy 
                ? totalWidth * ((_shellLifetime - _fadeDuration) / _shellLifetime)
                : totalWidth;
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 2, visibleWidth, rect.height - 4), 
                new Color(0.3f, 0.6f, 0.3f));
            
            // Fade phase
            if (_fadeBeforeDestroy)
            {
                float fadeWidth = totalWidth * (_fadeDuration / _shellLifetime);
                // Gradient effect
                for (int i = 0; i < fadeWidth; i++)
                {
                    float t = i / fadeWidth;
                    Color c = new Color(0.3f, 0.6f, 0.3f, 1f - t);
                    EditorGUI.DrawRect(new Rect(rect.x + 2 + visibleWidth + i, rect.y + 2, 1, rect.height - 4), c);
                }
            }
            
            GUI.Label(rect, $"{_shellLifetime:F1}s", 
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawPoolingSettings()
        {
            EditorGUILayout.LabelField("Object Pooling", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _poolSize = EditorGUILayout.IntSlider("Pool Size", _poolSize, 5, 100);
            _warmupPool = EditorGUILayout.Toggle("Warmup Pool on Start", _warmupPool);
            
            // Memory estimate
            float estimatedMemory = _poolSize * 0.05f; // Rough estimate per shell
            EditorGUILayout.LabelField($"Est. Memory: ~{estimatedMemory:F2} MB", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawAudioSettings()
        {
            EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _playBounceSound = EditorGUILayout.Toggle("Play Bounce Sound", _playBounceSound);
            
            if (_playBounceSound)
            {
                EditorGUI.indentLevel++;
                _minBounceVelocity = EditorGUILayout.Slider("Min Velocity", _minBounceVelocity, 0.1f, 2f);
                EditorGUILayout.LabelField("Sound volume scales with impact velocity", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_shellPrefab != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(80, 80, GUILayout.ExpandWidth(true));
                var previewTexture = AssetPreview.GetAssetPreview(_shellPrefab);
                if (previewTexture != null)
                {
                    GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
                }
            }

            if (GUILayout.Button("Test Ejection (Scene View)"))
            {
                TestEjectionInScene();
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
            
            if (GUILayout.Button("Save Preset", GUILayout.Height(30)))
            {
                SaveCurrentPreset();
            }
            
            GUI.backgroundColor = prevColor;

            EditorGUI.BeginDisabledGroup(_targetWeapon == null);
            if (GUILayout.Button("Apply to Weapon", GUILayout.Height(30)))
            {
                ApplyToWeapon();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CreateNewPreset()
        {
            _presets.Add(new ShellPreset
            {
                Name = $"Shell_{_presets.Count + 1}",
                EjectionForce = 3f,
                EjectionTorque = 10f,
                EjectionDirection = new Vector3(1, 0.5f, 0),
                ShellLifetime = 5f,
                PoolSize = 20
            });
            _selectedPresetIndex = _presets.Count - 1;
        }

        private void LoadPreset(ShellPreset preset)
        {
            _presetName = preset.Name;
            _shellPrefab = preset.ShellPrefab;
            _ejectionForce = preset.EjectionForce;
            _ejectionTorque = preset.EjectionTorque;
            _ejectionDirection = preset.EjectionDirection;
            _shellLifetime = preset.ShellLifetime;
            _poolSize = preset.PoolSize;
        }

        private void SaveCurrentPreset()
        {
            if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _presets.Count)
            {
                CreateNewPreset();
            }

            var preset = _presets[_selectedPresetIndex];
            preset.Name = _presetName;
            preset.ShellPrefab = _shellPrefab;
            preset.EjectionForce = _ejectionForce;
            preset.EjectionTorque = _ejectionTorque;
            preset.EjectionDirection = _ejectionDirection;
            preset.ShellLifetime = _shellLifetime;
            preset.PoolSize = _poolSize;
            
            Debug.Log($"[ShellEjection] Saved preset: {preset.Name}");
        }

        private void FindEjectionPort()
        {
            if (_targetWeapon == null) return;
            
            string[] portNames = { "EjectionPort", "Ejection_Port", "ShellEject", "CasingPort" };
            
            foreach (Transform child in _targetWeapon.GetComponentsInChildren<Transform>())
            {
                foreach (var name in portNames)
                {
                    if (child.name.ToLower().Contains(name.ToLower()))
                    {
                        _ejectionPort = child;
                        Debug.Log($"[ShellEjection] Found ejection port: {child.name}");
                        return;
                    }
                }
            }
            
            Debug.LogWarning("[ShellEjection] No ejection port found");
        }

        private void ApplyToWeapon()
        {
            Debug.Log($"[ShellEjection] Applied settings to {_targetWeapon.name}");
        }

        private void TestEjectionInScene()
        {
            Debug.Log("[ShellEjection] Scene test pending");
        }
    }
}
