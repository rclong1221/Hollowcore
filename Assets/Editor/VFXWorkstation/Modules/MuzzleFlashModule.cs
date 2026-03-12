using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.VFXWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 VW-01: Muzzle Flash module.
    /// Muzzle flash slot assignment, timing config, variants.
    /// </summary>
    public class MuzzleFlashModule : IVFXModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _presetListScroll;
        
        // Target weapon
        private GameObject _targetWeapon;
        private Transform _muzzlePoint;
        
        // Muzzle flash config
        private List<MuzzleFlashPreset> _presets = new List<MuzzleFlashPreset>();
        private int _selectedPresetIndex = -1;
        
        // Current preset editing
        private string _presetName = "NewMuzzleFlash";
        private GameObject _flashPrefab;
        private List<GameObject> _flashVariants = new List<GameObject>();
        private float _flashDuration = 0.05f;
        private float _flashScale = 1f;
        private Vector3 _flashOffset = Vector3.zero;
        private Vector3 _flashRotation = Vector3.zero;
        private bool _randomRotation = true;
        private bool _randomScale = false;
        private float _scaleVariance = 0.2f;
        private Color _flashTint = Color.white;
        private bool _useLightFlash = true;
        private float _lightIntensity = 5f;
        private float _lightRange = 10f;
        private Color _lightColor = new Color(1f, 0.9f, 0.7f);

        [System.Serializable]
        private class MuzzleFlashPreset
        {
            public string Name;
            public GameObject FlashPrefab;
            public List<GameObject> Variants = new List<GameObject>();
            public float Duration;
            public float Scale;
            public Vector3 Offset;
            public bool RandomRotation;
            public bool UseLightFlash;
            public float LightIntensity;
            public Color LightColor;
        }

        public MuzzleFlashModule()
        {
            InitializeDefaultPresets();
        }

        private void InitializeDefaultPresets()
        {
            _presets = new List<MuzzleFlashPreset>
            {
                new MuzzleFlashPreset { Name = "Pistol Flash", Duration = 0.04f, Scale = 0.5f, RandomRotation = true, UseLightFlash = true, LightIntensity = 3f, LightColor = new Color(1f, 0.9f, 0.7f) },
                new MuzzleFlashPreset { Name = "Rifle Flash", Duration = 0.05f, Scale = 0.8f, RandomRotation = true, UseLightFlash = true, LightIntensity = 5f, LightColor = new Color(1f, 0.85f, 0.6f) },
                new MuzzleFlashPreset { Name = "Shotgun Flash", Duration = 0.08f, Scale = 1.2f, RandomRotation = true, UseLightFlash = true, LightIntensity = 8f, LightColor = new Color(1f, 0.7f, 0.4f) },
                new MuzzleFlashPreset { Name = "SMG Flash", Duration = 0.03f, Scale = 0.6f, RandomRotation = true, UseLightFlash = true, LightIntensity = 4f, LightColor = new Color(1f, 0.9f, 0.7f) },
                new MuzzleFlashPreset { Name = "Heavy Flash", Duration = 0.1f, Scale = 1.5f, RandomRotation = true, UseLightFlash = true, LightIntensity = 10f, LightColor = new Color(1f, 0.6f, 0.3f) },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Muzzle Flash", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure muzzle flash effects, variants, timing, and light emission.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - preset list
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            DrawPresetList();
            EditorGUILayout.EndVertical();

            // Right panel - preset editor
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawWeaponSelection();
            EditorGUILayout.Space(10);
            DrawFlashPrefab();
            EditorGUILayout.Space(10);
            DrawTimingSettings();
            EditorGUILayout.Space(10);
            DrawTransformSettings();
            EditorGUILayout.Space(10);
            DrawLightSettings();
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
            EditorGUILayout.LabelField("Flash Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            if (GUILayout.Button("+ New Preset"))
            {
                CreateNewPreset();
            }

            EditorGUILayout.Space(5);
            
            _presetListScroll = EditorGUILayout.BeginScrollView(_presetListScroll);

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

            EditorGUILayout.EndScrollView();

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
                _muzzlePoint = (Transform)EditorGUILayout.ObjectField(
                    "Muzzle Point", _muzzlePoint, typeof(Transform), true);

                if (_muzzlePoint == null)
                {
                    if (GUILayout.Button("Auto-Find Muzzle Point"))
                    {
                        FindMuzzlePoint();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFlashPrefab()
        {
            EditorGUILayout.LabelField("Flash Prefab", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _presetName = EditorGUILayout.TextField("Preset Name", _presetName);
            
            EditorGUILayout.Space(5);
            
            _flashPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Main Flash Prefab", _flashPrefab, typeof(GameObject), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Variants (for randomization)", EditorStyles.miniLabel);

            for (int i = 0; i < _flashVariants.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                _flashVariants[i] = (GameObject)EditorGUILayout.ObjectField(
                    _flashVariants[i], typeof(GameObject), false);
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _flashVariants.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Variant"))
            {
                _flashVariants.Add(null);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTimingSettings()
        {
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _flashDuration = EditorGUILayout.Slider("Duration (seconds)", _flashDuration, 0.01f, 0.5f);
            
            // Duration visualization
            Rect durationRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(20), GUILayout.ExpandWidth(true));
            DrawDurationBar(durationRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawDurationBar(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float normalizedDuration = _flashDuration / 0.5f;
            float barWidth = normalizedDuration * rect.width;
            
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, barWidth, rect.height), 
                new Color(1f, 0.8f, 0.3f));
            
            GUI.Label(rect, $"{_flashDuration * 1000:F0}ms", 
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawTransformSettings()
        {
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _flashScale = EditorGUILayout.Slider("Base Scale", _flashScale, 0.1f, 3f);
            _flashOffset = EditorGUILayout.Vector3Field("Position Offset", _flashOffset);
            _flashRotation = EditorGUILayout.Vector3Field("Rotation Offset", _flashRotation);
            
            EditorGUILayout.Space(5);
            
            _randomRotation = EditorGUILayout.Toggle("Random Z Rotation", _randomRotation);
            
            _randomScale = EditorGUILayout.Toggle("Random Scale", _randomScale);
            if (_randomScale)
            {
                EditorGUI.indentLevel++;
                _scaleVariance = EditorGUILayout.Slider("Scale Variance", _scaleVariance, 0f, 0.5f);
                EditorGUILayout.LabelField($"Scale range: {_flashScale * (1 - _scaleVariance):F2} - {_flashScale * (1 + _scaleVariance):F2}", 
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            
            _flashTint = EditorGUILayout.ColorField("Tint Color", _flashTint);

            EditorGUILayout.EndVertical();
        }

        private void DrawLightSettings()
        {
            EditorGUILayout.LabelField("Light Flash", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _useLightFlash = EditorGUILayout.Toggle("Enable Light Flash", _useLightFlash);

            if (_useLightFlash)
            {
                EditorGUI.indentLevel++;
                _lightIntensity = EditorGUILayout.Slider("Intensity", _lightIntensity, 0f, 20f);
                _lightRange = EditorGUILayout.Slider("Range", _lightRange, 1f, 30f);
                _lightColor = EditorGUILayout.ColorField("Light Color", _lightColor);
                EditorGUI.indentLevel--;

                // Light preview
                Rect lightRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                    GUILayout.Height(30), GUILayout.ExpandWidth(true));
                DrawLightPreview(lightRect);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLightPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f));
            
            float centerX = rect.center.x;
            float normalizedIntensity = _lightIntensity / 20f;
            float gradientWidth = rect.width * 0.4f * normalizedIntensity;
            
            // Draw gradient
            for (int i = 0; i < gradientWidth; i++)
            {
                float t = 1f - (i / gradientWidth);
                Color c = _lightColor * t * normalizedIntensity;
                c.a = 1f;
                EditorGUI.DrawRect(new Rect(centerX - i, rect.y, 1, rect.height), c);
                EditorGUI.DrawRect(new Rect(centerX + i, rect.y, 1, rect.height), c);
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_flashPrefab != null)
            {
                // Show prefab preview
                Rect previewRect = GUILayoutUtility.GetRect(100, 80, GUILayout.ExpandWidth(true));
                var previewTexture = AssetPreview.GetAssetPreview(_flashPrefab);
                if (previewTexture != null)
                {
                    GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                    GUI.Label(previewRect, _flashPrefab.name, 
                        new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
                }
            }
            else
            {
                EditorGUILayout.LabelField("Assign a flash prefab to preview", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Test Flash (Scene)", GUILayout.Height(25)))
            {
                TestFlashInScene();
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

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Presets"))
            {
                ExportPresets();
            }
            
            if (GUILayout.Button("Import Presets"))
            {
                ImportPresets();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CreateNewPreset()
        {
            _presets.Add(new MuzzleFlashPreset
            {
                Name = $"Flash_{_presets.Count + 1}",
                Duration = 0.05f,
                Scale = 1f,
                RandomRotation = true,
                UseLightFlash = true,
                LightIntensity = 5f,
                LightColor = new Color(1f, 0.9f, 0.7f)
            });
            _selectedPresetIndex = _presets.Count - 1;
        }

        private void LoadPreset(MuzzleFlashPreset preset)
        {
            _presetName = preset.Name;
            _flashPrefab = preset.FlashPrefab;
            _flashVariants = new List<GameObject>(preset.Variants);
            _flashDuration = preset.Duration;
            _flashScale = preset.Scale;
            _flashOffset = preset.Offset;
            _randomRotation = preset.RandomRotation;
            _useLightFlash = preset.UseLightFlash;
            _lightIntensity = preset.LightIntensity;
            _lightColor = preset.LightColor;
        }

        private void SaveCurrentPreset()
        {
            if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _presets.Count)
            {
                CreateNewPreset();
            }

            var preset = _presets[_selectedPresetIndex];
            preset.Name = _presetName;
            preset.FlashPrefab = _flashPrefab;
            preset.Variants = new List<GameObject>(_flashVariants.Where(v => v != null));
            preset.Duration = _flashDuration;
            preset.Scale = _flashScale;
            preset.Offset = _flashOffset;
            preset.RandomRotation = _randomRotation;
            preset.UseLightFlash = _useLightFlash;
            preset.LightIntensity = _lightIntensity;
            preset.LightColor = _lightColor;
            
            Debug.Log($"[MuzzleFlash] Saved preset: {preset.Name}");
        }

        private void FindMuzzlePoint()
        {
            if (_targetWeapon == null) return;
            
            string[] muzzleNames = { "Muzzle", "MuzzlePoint", "Muzzle_Point", "FirePoint", "BarrelEnd" };
            
            foreach (var name in muzzleNames)
            {
                var found = _targetWeapon.transform.Find(name);
                if (found != null)
                {
                    _muzzlePoint = found;
                    Debug.Log($"[MuzzleFlash] Found muzzle point: {name}");
                    return;
                }
            }
            
            // Deep search
            foreach (Transform child in _targetWeapon.GetComponentsInChildren<Transform>())
            {
                foreach (var name in muzzleNames)
                {
                    if (child.name.ToLower().Contains(name.ToLower()))
                    {
                        _muzzlePoint = child;
                        Debug.Log($"[MuzzleFlash] Found muzzle point: {child.name}");
                        return;
                    }
                }
            }
            
            Debug.LogWarning("[MuzzleFlash] No muzzle point found");
        }

        private void ApplyToWeapon()
        {
            Debug.Log($"[MuzzleFlash] Applied preset '{_presetName}' to {_targetWeapon.name}");
        }

        private void TestFlashInScene()
        {
            if (_flashPrefab == null)
            {
                Debug.LogWarning("[MuzzleFlash] No flash prefab assigned");
                return;
            }
            
            Debug.Log("[MuzzleFlash] Scene test pending - would instantiate flash at scene view camera");
        }

        private void ExportPresets()
        {
            Debug.Log("[MuzzleFlash] Export pending");
        }

        private void ImportPresets()
        {
            Debug.Log("[MuzzleFlash] Import pending");
        }
    }
}
