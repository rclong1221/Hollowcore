using System.IO;
using UnityEditor;
using UnityEngine;
using Audio.Systems;

public static class SurfaceMaterialFallbackGenerator
{
    [MenuItem("Assets/Audio/Generate Fallback SurfaceMaterials from Materials")]
    public static void GenerateFallbacks()
    {
        var mappingPath = EditorUtility.OpenFilePanel("Select Mapping Asset (optional)", Application.dataPath, "asset");
        SurfaceMaterialMapping mapping = null;
        if (!string.IsNullOrEmpty(mappingPath))
        {
            var relative = "Assets" + mappingPath.Substring(Application.dataPath.Length).Replace("\\", "/");
            mapping = AssetDatabase.LoadAssetAtPath<SurfaceMaterialMapping>(relative);
        }

        var guids = AssetDatabase.FindAssets("t:Material");
        string outputFolder = "Assets/Audio/GeneratedSurfaceMaterials";
        if (!AssetDatabase.IsValidFolder(outputFolder)) AssetDatabase.CreateFolder("Assets/Audio", "GeneratedSurfaceMaterials");

        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            bool alreadyMapped = false;
            if (mapping != null)
                alreadyMapped = mapping.mappings.Exists(e => e.materialName == mat.name && e.surfaceMaterial != null);

            var generatedPath = Path.Combine(outputFolder, mat.name + "_SurfaceMaterial.asset").Replace("\\", "/");
            var existing = AssetDatabase.LoadAssetAtPath<SurfaceMaterial>(generatedPath);
            if (existing != null)
            {
                if (!alreadyMapped && mapping != null)
                {
                    mapping.mappings.Add(new SurfaceMaterialMapping.Entry { materialName = mat.name, surfaceMaterial = existing });
                    EditorUtility.SetDirty(mapping);
                }
                continue;
            }

            var sm = ScriptableObject.CreateInstance<SurfaceMaterial>();
            // Fallback id: leave 0 so authors can assign meaningful ids in the registry/editor.
            sm.Id = 0;
            sm.DisplayName = mat.name;
            AssetDatabase.CreateAsset(sm, generatedPath);

            if (mapping != null)
            {
                mapping.mappings.Add(new SurfaceMaterialMapping.Entry { materialName = mat.name, surfaceMaterial = sm });
                EditorUtility.SetDirty(mapping);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated fallback SurfaceMaterials and updated mapping (if provided).");
    }
}
