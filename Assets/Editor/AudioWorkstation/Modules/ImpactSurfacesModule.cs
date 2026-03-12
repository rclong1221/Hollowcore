using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.AudioWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 AW-02: Impact Surfaces module.
    /// Surface type mapping (metal/flesh/wood/stone), material detection.
    /// </summary>
    public class ImpactSurfacesModule : IAudioModule
    {
        private Vector2 _scrollPosition;
        
        // Surface types
        private List<SurfaceType> _surfaceTypes = new List<SurfaceType>();
        private int _selectedSurfaceIndex = -1;
        
        // Current surface being edited
        private string _surfaceName = "NewSurface";
        private Color _surfaceColor = Color.gray;
        private PhysicsMaterial _physicsMaterial;
        private List<AudioClip> _impactSounds = new List<AudioClip>();
        private List<AudioClip> _bulletHitSounds = new List<AudioClip>();
        private List<AudioClip> _footstepSounds = new List<AudioClip>();
        private float _volumeMultiplier = 1f;
        private float _pitchVariation = 0.1f;

        [System.Serializable]
        private class SurfaceType
        {
            public string Name;
            public Color Color = Color.gray;
            public PhysicsMaterial PhysicsMaterial;
            public List<string> MaterialKeywords = new List<string>();
            public List<AudioClip> ImpactSounds = new List<AudioClip>();
            public List<AudioClip> BulletHitSounds = new List<AudioClip>();
            public List<AudioClip> FootstepSounds = new List<AudioClip>();
            public float VolumeMultiplier = 1f;
            public float PitchVariation = 0.1f;
        }

        public ImpactSurfacesModule()
        {
            // Initialize with default surface types
            InitializeDefaultSurfaces();
        }

        private void InitializeDefaultSurfaces()
        {
            _surfaceTypes = new List<SurfaceType>
            {
                new SurfaceType { Name = "Concrete", Color = Color.gray, MaterialKeywords = new List<string> { "concrete", "cement", "stone" } },
                new SurfaceType { Name = "Metal", Color = new Color(0.6f, 0.6f, 0.7f), MaterialKeywords = new List<string> { "metal", "steel", "iron" } },
                new SurfaceType { Name = "Wood", Color = new Color(0.6f, 0.4f, 0.2f), MaterialKeywords = new List<string> { "wood", "plank", "timber" } },
                new SurfaceType { Name = "Flesh", Color = new Color(0.8f, 0.3f, 0.3f), MaterialKeywords = new List<string> { "flesh", "body", "skin" } },
                new SurfaceType { Name = "Dirt", Color = new Color(0.4f, 0.3f, 0.2f), MaterialKeywords = new List<string> { "dirt", "mud", "soil", "ground" } },
                new SurfaceType { Name = "Glass", Color = new Color(0.8f, 0.9f, 1f), MaterialKeywords = new List<string> { "glass", "window" } },
                new SurfaceType { Name = "Water", Color = new Color(0.2f, 0.5f, 0.8f), MaterialKeywords = new List<string> { "water", "liquid" } },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Impact Surfaces", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Define surface types and map audio clips for impacts, bullet hits, and footsteps.",
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
            DrawMaterialDetection();
            EditorGUILayout.Space(10);
            DrawImpactSounds();
            EditorGUILayout.Space(10);
            DrawBulletHitSounds();
            EditorGUILayout.Space(10);
            DrawFootstepSounds();
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

            for (int i = 0; i < _surfaceTypes.Count; i++)
            {
                var surface = _surfaceTypes[i];
                
                EditorGUILayout.BeginHorizontal();
                
                // Color indicator
                Rect colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
                EditorGUI.DrawRect(colorRect, surface.Color);
                
                bool selected = i == _selectedSurfaceIndex;
                Color prevColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = Color.cyan;
                
                if (GUILayout.Button(surface.Name, EditorStyles.miniButton))
                {
                    _selectedSurfaceIndex = i;
                    LoadSurface(surface);
                }
                
                GUI.backgroundColor = prevColor;

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _surfaceTypes.RemoveAt(i);
                    if (_selectedSurfaceIndex >= _surfaceTypes.Count)
                        _selectedSurfaceIndex = _surfaceTypes.Count - 1;
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSurfaceProperties()
        {
            EditorGUILayout.LabelField("Surface Properties", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _surfaceName = EditorGUILayout.TextField("Name", _surfaceName);
            _surfaceColor = EditorGUILayout.ColorField("Display Color", _surfaceColor);
            _physicsMaterial = (PhysicsMaterial)EditorGUILayout.ObjectField(
                "Physics Material", _physicsMaterial, typeof(PhysicsMaterial), false);
            
            EditorGUILayout.Space(5);
            _volumeMultiplier = EditorGUILayout.Slider("Volume Multiplier", _volumeMultiplier, 0f, 2f);
            _pitchVariation = EditorGUILayout.Slider("Pitch Variation", _pitchVariation, 0f, 0.5f);

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialDetection()
        {
            EditorGUILayout.LabelField("Material Detection Keywords", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Materials containing these keywords will use this surface type:", 
                EditorStyles.wordWrappedMiniLabel);
            
            if (_selectedSurfaceIndex >= 0 && _selectedSurfaceIndex < _surfaceTypes.Count)
            {
                var keywords = _surfaceTypes[_selectedSurfaceIndex].MaterialKeywords;
                
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

        private void DrawImpactSounds()
        {
            DrawSoundList("Impact Sounds (Melee/Objects)", _impactSounds);
        }

        private void DrawBulletHitSounds()
        {
            DrawSoundList("Bullet Hit Sounds", _bulletHitSounds);
        }

        private void DrawFootstepSounds()
        {
            DrawSoundList("Footstep Sounds", _footstepSounds);
        }

        private void DrawSoundList(string label, List<AudioClip> clips)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < clips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                clips[i] = (AudioClip)EditorGUILayout.ObjectField(
                    clips[i], typeof(AudioClip), false);
                
                if (clips[i] != null)
                {
                    EditorGUILayout.LabelField($"{clips[i].length:F2}s", 
                        EditorStyles.miniLabel, GUILayout.Width(40));
                    
                    if (GUILayout.Button("▶", GUILayout.Width(25)))
                    {
                        PlayClipPreview(clips[i]);
                    }
                }
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    clips.RemoveAt(i);
                    i--;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (clips.Count == 0)
            {
                EditorGUILayout.LabelField("No clips assigned", EditorStyles.centeredGreyMiniLabel);
            }

            if (GUILayout.Button("+ Add Clip"))
            {
                clips.Add(null);
            }

            // Drag and drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Audio Clips Here", EditorStyles.helpBox);
            
            HandleDragDrop(dropArea, clips);

            EditorGUILayout.EndVertical();
        }

        private void HandleDragDrop(Rect dropArea, List<AudioClip> clips)
        {
            Event evt = Event.current;
            
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition)) return;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is AudioClip clip)
                        {
                            clips.Add(clip);
                        }
                    }
                }
                
                evt.Use();
            }
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

            if (GUILayout.Button("Test Detection", GUILayout.Height(30)))
            {
                TestMaterialDetection();
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
            
            if (GUILayout.Button("Reset Defaults"))
            {
                InitializeDefaultSurfaces();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void AddNewSurface()
        {
            _surfaceTypes.Add(new SurfaceType
            {
                Name = $"Surface_{_surfaceTypes.Count + 1}",
                Color = Random.ColorHSV(0, 1, 0.5f, 1, 0.5f, 1)
            });
            _selectedSurfaceIndex = _surfaceTypes.Count - 1;
            LoadSurface(_surfaceTypes[_selectedSurfaceIndex]);
        }

        private void LoadSurface(SurfaceType surface)
        {
            _surfaceName = surface.Name;
            _surfaceColor = surface.Color;
            _physicsMaterial = surface.PhysicsMaterial;
            _volumeMultiplier = surface.VolumeMultiplier;
            _pitchVariation = surface.PitchVariation;
            _impactSounds = new List<AudioClip>(surface.ImpactSounds);
            _bulletHitSounds = new List<AudioClip>(surface.BulletHitSounds);
            _footstepSounds = new List<AudioClip>(surface.FootstepSounds);
        }

        private void SaveCurrentSurface()
        {
            if (_selectedSurfaceIndex < 0 || _selectedSurfaceIndex >= _surfaceTypes.Count)
            {
                AddNewSurface();
            }

            var surface = _surfaceTypes[_selectedSurfaceIndex];
            surface.Name = _surfaceName;
            surface.Color = _surfaceColor;
            surface.PhysicsMaterial = _physicsMaterial;
            surface.VolumeMultiplier = _volumeMultiplier;
            surface.PitchVariation = _pitchVariation;
            surface.ImpactSounds = new List<AudioClip>(_impactSounds.Where(c => c != null));
            surface.BulletHitSounds = new List<AudioClip>(_bulletHitSounds.Where(c => c != null));
            surface.FootstepSounds = new List<AudioClip>(_footstepSounds.Where(c => c != null));
            
            Debug.Log($"[ImpactSurfaces] Saved surface: {surface.Name}");
        }

        private void TestMaterialDetection()
        {
            if (Selection.activeGameObject != null)
            {
                var renderer = Selection.activeGameObject.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    string matName = renderer.sharedMaterial.name.ToLower();
                    
                    foreach (var surface in _surfaceTypes)
                    {
                        foreach (var keyword in surface.MaterialKeywords)
                        {
                            if (!string.IsNullOrEmpty(keyword) && matName.Contains(keyword.ToLower()))
                            {
                                Debug.Log($"[ImpactSurfaces] Material '{renderer.sharedMaterial.name}' → Surface '{surface.Name}'");
                                return;
                            }
                        }
                    }
                    
                    Debug.Log($"[ImpactSurfaces] Material '{renderer.sharedMaterial.name}' → No matching surface");
                }
            }
        }

        private void ExportConfig()
        {
            Debug.Log("[ImpactSurfaces] Export pending");
        }

        private void ImportConfig()
        {
            Debug.Log("[ImpactSurfaces] Import pending");
        }

        private void PlayClipPreview(AudioClip clip)
        {
            if (clip == null) return;
            
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtilClass.GetMethod("PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            
            method?.Invoke(null, new object[] { clip, 0, false });
        }
    }
}
