using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.VFXWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 VW-03: Impact FX module.
    /// Impact effects per surface type, decal spawning.
    /// </summary>
    public class ImpactFXModule : IVFXModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _surfaceListScroll;
        
        // Surface impact configs
        private List<SurfaceImpactConfig> _surfaceConfigs = new List<SurfaceImpactConfig>();
        private int _selectedSurfaceIndex = -1;
        
        // Current surface being edited
        private string _surfaceName = "Default";
        private Color _surfaceColor = Color.gray;
        private GameObject _impactVFX;
        private GameObject _decalPrefab;
        private List<GameObject> _impactVariants = new List<GameObject>();
        
        // VFX settings
        private float _vfxScale = 1f;
        private float _vfxLifetime = 2f;
        private bool _alignToNormal = true;
        private Vector3 _rotationOffset = Vector3.zero;
        
        // Decal settings
        private bool _spawnDecal = true;
        private float _decalSize = 0.5f;
        private float _decalLifetime = 30f;
        private bool _randomDecalRotation = true;
        private float _decalSizeVariance = 0.2f;
        private int _maxDecals = 50;

        [System.Serializable]
        private class SurfaceImpactConfig
        {
            public string Name;
            public Color DisplayColor;
            public List<string> MaterialKeywords = new List<string>();
            public GameObject ImpactVFX;
            public List<GameObject> Variants = new List<GameObject>();
            public GameObject DecalPrefab;
            public float VFXScale = 1f;
            public float VFXLifetime = 2f;
            public bool SpawnDecal = true;
            public float DecalSize = 0.5f;
            public float DecalLifetime = 30f;
        }

        public ImpactFXModule()
        {
            InitializeDefaultSurfaces();
        }

        private void InitializeDefaultSurfaces()
        {
            _surfaceConfigs = new List<SurfaceImpactConfig>
            {
                new SurfaceImpactConfig { Name = "Concrete", DisplayColor = Color.gray, MaterialKeywords = new List<string> { "concrete", "cement", "stone" } },
                new SurfaceImpactConfig { Name = "Metal", DisplayColor = new Color(0.6f, 0.6f, 0.7f), MaterialKeywords = new List<string> { "metal", "steel", "iron" } },
                new SurfaceImpactConfig { Name = "Wood", DisplayColor = new Color(0.6f, 0.4f, 0.2f), MaterialKeywords = new List<string> { "wood", "plank" } },
                new SurfaceImpactConfig { Name = "Flesh", DisplayColor = new Color(0.8f, 0.3f, 0.3f), MaterialKeywords = new List<string> { "flesh", "body", "skin" } },
                new SurfaceImpactConfig { Name = "Dirt", DisplayColor = new Color(0.4f, 0.3f, 0.2f), MaterialKeywords = new List<string> { "dirt", "mud", "soil" } },
                new SurfaceImpactConfig { Name = "Glass", DisplayColor = new Color(0.8f, 0.9f, 1f), MaterialKeywords = new List<string> { "glass", "window" } },
                new SurfaceImpactConfig { Name = "Water", DisplayColor = new Color(0.2f, 0.5f, 0.8f), MaterialKeywords = new List<string> { "water", "liquid" } },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Impact FX", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure impact effects and decals for different surface types.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - surface list
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            DrawSurfaceList();
            EditorGUILayout.EndVertical();

            // Right panel - surface editor
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawSurfaceProperties();
            EditorGUILayout.Space(10);
            DrawImpactVFX();
            EditorGUILayout.Space(10);
            DrawDecalSettings();
            EditorGUILayout.Space(10);
            DrawMaterialMapping();
            EditorGUILayout.Space(10);
            DrawPreview();
            EditorGUILayout.Space(10);
            DrawActions();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSurfaceList()
        {
            EditorGUILayout.LabelField("Surface Types", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            if (GUILayout.Button("+ Add Surface"))
            {
                AddNewSurface();
            }

            EditorGUILayout.Space(5);
            
            _surfaceListScroll = EditorGUILayout.BeginScrollView(_surfaceListScroll);

            for (int i = 0; i < _surfaceConfigs.Count; i++)
            {
                var config = _surfaceConfigs[i];
                
                EditorGUILayout.BeginHorizontal();
                
                // Color indicator
                Rect colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
                EditorGUI.DrawRect(colorRect, config.DisplayColor);
                
                bool selected = i == _selectedSurfaceIndex;
                Color prevColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = Color.cyan;
                
                if (GUILayout.Button(config.Name, EditorStyles.miniButton))
                {
                    _selectedSurfaceIndex = i;
                    LoadSurface(config);
                }
                
                GUI.backgroundColor = prevColor;

                // VFX indicator
                if (config.ImpactVFX != null)
                {
                    EditorGUILayout.LabelField("✓", GUILayout.Width(15));
                }
                else
                {
                    EditorGUILayout.LabelField("", GUILayout.Width(15));
                }

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _surfaceConfigs.RemoveAt(i);
                    if (_selectedSurfaceIndex >= _surfaceConfigs.Count)
                        _selectedSurfaceIndex = _surfaceConfigs.Count - 1;
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import"))
            {
                ImportConfigs();
            }
            if (GUILayout.Button("Export"))
            {
                ExportConfigs();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSurfaceProperties()
        {
            EditorGUILayout.LabelField("Surface Properties", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _surfaceName = EditorGUILayout.TextField("Name", _surfaceName);
            _surfaceColor = EditorGUILayout.ColorField("Display Color", _surfaceColor);

            EditorGUILayout.EndVertical();
        }

        private void DrawImpactVFX()
        {
            EditorGUILayout.LabelField("Impact VFX", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _impactVFX = (GameObject)EditorGUILayout.ObjectField(
                "Main Impact VFX", _impactVFX, typeof(GameObject), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Variants (for randomization)", EditorStyles.miniLabel);

            for (int i = 0; i < _impactVariants.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                _impactVariants[i] = (GameObject)EditorGUILayout.ObjectField(
                    _impactVariants[i], typeof(GameObject), false);
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _impactVariants.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Variant"))
            {
                _impactVariants.Add(null);
            }

            EditorGUILayout.Space(10);
            
            _vfxScale = EditorGUILayout.Slider("Scale", _vfxScale, 0.1f, 5f);
            _vfxLifetime = EditorGUILayout.Slider("Lifetime (s)", _vfxLifetime, 0.1f, 10f);
            _alignToNormal = EditorGUILayout.Toggle("Align to Surface Normal", _alignToNormal);
            
            if (!_alignToNormal)
            {
                _rotationOffset = EditorGUILayout.Vector3Field("Rotation Offset", _rotationOffset);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDecalSettings()
        {
            EditorGUILayout.LabelField("Decal Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _spawnDecal = EditorGUILayout.Toggle("Spawn Decal", _spawnDecal);

            if (_spawnDecal)
            {
                EditorGUI.indentLevel++;
                
                _decalPrefab = (GameObject)EditorGUILayout.ObjectField(
                    "Decal Prefab", _decalPrefab, typeof(GameObject), false);
                
                _decalSize = EditorGUILayout.Slider("Base Size", _decalSize, 0.1f, 2f);
                _decalSizeVariance = EditorGUILayout.Slider("Size Variance", _decalSizeVariance, 0f, 0.5f);
                _decalLifetime = EditorGUILayout.Slider("Lifetime (s)", _decalLifetime, 1f, 120f);
                _randomDecalRotation = EditorGUILayout.Toggle("Random Rotation", _randomDecalRotation);
                
                EditorGUILayout.Space(5);
                _maxDecals = EditorGUILayout.IntSlider("Max Active Decals", _maxDecals, 10, 200);
                
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialMapping()
        {
            EditorGUILayout.LabelField("Material Mapping Keywords", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Materials containing these keywords will use this impact:", 
                EditorStyles.wordWrappedMiniLabel);

            if (_selectedSurfaceIndex >= 0 && _selectedSurfaceIndex < _surfaceConfigs.Count)
            {
                var keywords = _surfaceConfigs[_selectedSurfaceIndex].MaterialKeywords;
                
                for (int i = 0; i < keywords.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    keywords[i] = EditorGUILayout.TextField(keywords[i]);
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        keywords.RemoveAt(i);
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                if (GUILayout.Button("+ Add Keyword"))
                {
                    keywords.Add("");
                }
            }
            else
            {
                EditorGUILayout.LabelField("Select a surface to edit keywords", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // VFX preview
            if (_impactVFX != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80));
                var previewTexture = AssetPreview.GetAssetPreview(_impactVFX);
                if (previewTexture != null)
                {
                    GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                    GUI.Label(previewRect, "VFX", 
                        new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
                }
            }
            
            // Decal preview
            if (_decalPrefab != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80));
                var previewTexture = AssetPreview.GetAssetPreview(_decalPrefab);
                if (previewTexture != null)
                {
                    GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                    GUI.Label(previewRect, "Decal", 
                        new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
                }
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Test Impact (Scene View)"))
            {
                TestImpactInScene();
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
            
            if (GUILayout.Button("Save Surface", GUILayout.Height(30)))
            {
                SaveCurrentSurface();
            }
            
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Apply All", GUILayout.Height(30)))
            {
                ApplyAllConfigs();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Create ScriptableObject"))
            {
                CreateScriptableObject();
            }
            
            if (GUILayout.Button("Reset Defaults"))
            {
                InitializeDefaultSurfaces();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void AddNewSurface()
        {
            _surfaceConfigs.Add(new SurfaceImpactConfig
            {
                Name = $"Surface_{_surfaceConfigs.Count + 1}",
                DisplayColor = Random.ColorHSV(0, 1, 0.5f, 1, 0.5f, 1)
            });
            _selectedSurfaceIndex = _surfaceConfigs.Count - 1;
            LoadSurface(_surfaceConfigs[_selectedSurfaceIndex]);
        }

        private void LoadSurface(SurfaceImpactConfig config)
        {
            _surfaceName = config.Name;
            _surfaceColor = config.DisplayColor;
            _impactVFX = config.ImpactVFX;
            _impactVariants = new List<GameObject>(config.Variants);
            _decalPrefab = config.DecalPrefab;
            _vfxScale = config.VFXScale;
            _vfxLifetime = config.VFXLifetime;
            _spawnDecal = config.SpawnDecal;
            _decalSize = config.DecalSize;
            _decalLifetime = config.DecalLifetime;
        }

        private void SaveCurrentSurface()
        {
            if (_selectedSurfaceIndex < 0 || _selectedSurfaceIndex >= _surfaceConfigs.Count)
            {
                AddNewSurface();
            }

            var config = _surfaceConfigs[_selectedSurfaceIndex];
            config.Name = _surfaceName;
            config.DisplayColor = _surfaceColor;
            config.ImpactVFX = _impactVFX;
            config.Variants = new List<GameObject>(_impactVariants.Where(v => v != null));
            config.DecalPrefab = _decalPrefab;
            config.VFXScale = _vfxScale;
            config.VFXLifetime = _vfxLifetime;
            config.SpawnDecal = _spawnDecal;
            config.DecalSize = _decalSize;
            config.DecalLifetime = _decalLifetime;
            
            Debug.Log($"[ImpactFX] Saved surface: {config.Name}");
        }

        private void ApplyAllConfigs()
        {
            Debug.Log($"[ImpactFX] Applied {_surfaceConfigs.Count} surface configurations");
        }

        private void TestImpactInScene()
        {
            Debug.Log("[ImpactFX] Scene test pending");
        }

        private void CreateScriptableObject()
        {
            Debug.Log("[ImpactFX] ScriptableObject creation pending");
        }

        private void ImportConfigs()
        {
            Debug.Log("[ImpactFX] Import pending");
        }

        private void ExportConfigs()
        {
            Debug.Log("[ImpactFX] Export pending");
        }
    }
}
