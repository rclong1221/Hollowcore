using System.IO;
using UnityEditor;
using UnityEngine;

namespace Audio.Editor
{
    /// <summary>
    /// Editor helper to create sample SurfaceMaterial assets and a registry.
    /// Use: Tools/DIG Audio/Create Sample SurfaceMaterials
    /// </summary>
    public static class SurfaceMaterialSampleCreator
    {
        [MenuItem("Tools/DIG Audio/Create Sample SurfaceMaterials")]
        public static void CreateSamples()
        {
            string folder = "Assets/Audio/Samples";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "Audio/Samples"));
                AssetDatabase.Refresh();
            }

            // Create two sample materials
            var mat1 = ScriptableObject.CreateInstance<Audio.Systems.SurfaceMaterial>();
            mat1.Id = 1;
            mat1.DisplayName = "Concrete";
            mat1.FootstepVolume = 1.0f;
            AssetDatabase.CreateAsset(mat1, folder + "/SurfaceMaterial_Concrete.asset");

            var mat2 = ScriptableObject.CreateInstance<Audio.Systems.SurfaceMaterial>();
            mat2.Id = 2;
            mat2.DisplayName = "Metal";
            mat2.FootstepVolume = 1.0f;
            AssetDatabase.CreateAsset(mat2, folder + "/SurfaceMaterial_Metal.asset");

            // Create registry and assign default and materials
            var registry = ScriptableObject.CreateInstance<Audio.Systems.SurfaceMaterialRegistry>();
            registry.DefaultMaterial = mat1;
            registry.Materials.Add(mat1);
            registry.Materials.Add(mat2);
            AssetDatabase.CreateAsset(registry, folder + "/SurfaceMaterialRegistry_Samples.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Created sample SurfaceMaterials and registry at Assets/Audio/Samples");
        }
    }
}
