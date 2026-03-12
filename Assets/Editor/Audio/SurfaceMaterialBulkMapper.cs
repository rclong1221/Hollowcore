using System.Linq;
using UnityEditor;
using UnityEngine;
using Audio.Systems;

namespace Audio.Editor
{
    /// <summary>
    /// Editor window for bulk-mapping materials, tags, and layers to SurfaceMaterial assets.
    /// </summary>
    public class SurfaceMaterialBulkMapper : EditorWindow
    {
        private SurfaceMaterialMapping mappingAsset;
        private Vector2 scroll;
        private int selectedTab = 0;
        private readonly string[] tabNames = { "Materials", "Tags", "Layers" };

        [MenuItem("Window/DIG/Audio/Surface Material Bulk Mapper")]
        public static void ShowWindow()
        {
            GetWindow<SurfaceMaterialBulkMapper>("Surface Material Mapper");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Surface Material Bulk Mapper", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            mappingAsset = (SurfaceMaterialMapping)EditorGUILayout.ObjectField("Mapping Asset", mappingAsset, typeof(SurfaceMaterialMapping), false);

            if (mappingAsset == null)
            {
                EditorGUILayout.HelpBox("Create or assign a SurfaceMaterialMapping asset to get started.", MessageType.Info);
                if (GUILayout.Button("Create Mapping Asset"))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Create Mapping", "SurfaceMaterialMapping", "asset", "Create mapping asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var a = CreateInstance<SurfaceMaterialMapping>();
                        AssetDatabase.CreateAsset(a, path);
                        AssetDatabase.SaveAssets();
                        mappingAsset = a;
                    }
                }
                return;
            }

            EditorGUILayout.Space();
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            EditorGUILayout.Space();

            switch (selectedTab)
            {
                case 0: DrawMaterialsTab(); break;
                case 1: DrawTagsTab(); break;
                case 2: DrawLayersTab(); break;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Changes"))
            {
                EditorUtility.SetDirty(mappingAsset);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawMaterialsTab()
        {
            if (GUILayout.Button("Scan Project Materials"))
            {
                ScanProjectMaterials();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            
            var materialEntries = mappingAsset.mappings.Where(e => !string.IsNullOrEmpty(e?.materialName)).ToList();
            foreach (var entry in materialEntries)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.materialName, GUILayout.Width(200));
                entry.surfaceMaterial = (SurfaceMaterial)EditorGUILayout.ObjectField(entry.surfaceMaterial, typeof(SurfaceMaterial), false);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    mappingAsset.mappings.Remove(entry);
                    EditorUtility.SetDirty(mappingAsset);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawTagsTab()
        {
            if (GUILayout.Button("Scan Project Tags"))
            {
                ScanProjectTags();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            
            var tagEntries = mappingAsset.mappings.Where(e => !string.IsNullOrEmpty(e?.tag)).ToList();
            foreach (var entry in tagEntries)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Tag: {entry.tag}", GUILayout.Width(200));
                entry.surfaceMaterial = (SurfaceMaterial)EditorGUILayout.ObjectField(entry.surfaceMaterial, typeof(SurfaceMaterial), false);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    mappingAsset.mappings.Remove(entry);
                    EditorUtility.SetDirty(mappingAsset);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Add Tag Entry"))
            {
                mappingAsset.mappings.Add(new SurfaceMaterialMapping.Entry { tag = "Untagged" });
                EditorUtility.SetDirty(mappingAsset);
            }
        }

        private void DrawLayersTab()
        {
            if (GUILayout.Button("Scan Project Layers"))
            {
                ScanProjectLayers();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            
            var layerEntries = mappingAsset.mappings.Where(e => e != null && e.layer >= 0).ToList();
            foreach (var entry in layerEntries)
            {
                EditorGUILayout.BeginHorizontal();
                string layerName = LayerMask.LayerToName(entry.layer);
                EditorGUILayout.LabelField($"Layer {entry.layer}: {layerName}", GUILayout.Width(200));
                entry.surfaceMaterial = (SurfaceMaterial)EditorGUILayout.ObjectField(entry.surfaceMaterial, typeof(SurfaceMaterial), false);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    mappingAsset.mappings.Remove(entry);
                    EditorUtility.SetDirty(mappingAsset);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Add Layer Entry"))
            {
                mappingAsset.mappings.Add(new SurfaceMaterialMapping.Entry { layer = 0 });
                EditorUtility.SetDirty(mappingAsset);
            }
        }

        private void ScanProjectMaterials()
        {
            var guids = AssetDatabase.FindAssets("t:Material");
            var names = guids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Select(p => AssetDatabase.LoadAssetAtPath<Material>(p))
                .Where(m => m != null)
                .Select(m => m.name)
                .Distinct()
                .ToArray();

            foreach (var n in names)
            {
                if (!mappingAsset.mappings.Exists(e => e.materialName == n))
                {
                    mappingAsset.mappings.Add(new SurfaceMaterialMapping.Entry { materialName = n });
                }
            }

            EditorUtility.SetDirty(mappingAsset);
            Debug.Log($"[SurfaceMaterialMapper] Scanned {names.Length} unique materials");
        }

        private void ScanProjectTags()
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            foreach (var tag in tags)
            {
                if (!mappingAsset.mappings.Exists(e => e.tag == tag))
                {
                    mappingAsset.mappings.Add(new SurfaceMaterialMapping.Entry { tag = tag });
                }
            }
            EditorUtility.SetDirty(mappingAsset);
            Debug.Log($"[SurfaceMaterialMapper] Scanned {tags.Length} tags");
        }

        private void ScanProjectLayers()
        {
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    if (!mappingAsset.mappings.Exists(e => e.layer == i))
                    {
                        mappingAsset.mappings.Add(new SurfaceMaterialMapping.Entry { layer = i });
                    }
                }
            }
            EditorUtility.SetDirty(mappingAsset);
            Debug.Log("[SurfaceMaterialMapper] Scanned project layers");
        }
    }
}
