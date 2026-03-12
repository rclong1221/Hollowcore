using UnityEngine;
using UnityEditor;
using System.IO;

namespace DIG.Editor.OpsiveExtractor
{
    /// <summary>
    /// Quick utility to batch convert specific Opsive weapons that were missed.
    /// Run via: Tools > DIG > Quick Convert Missing Weapons
    /// </summary>
    public static class QuickConvertMissingWeapons
    {
        private static readonly string[] WeaponsToConvert = new[]
        {
            "Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/AssaultRifle/AssaultRifleWeapon.prefab",
            "Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/Pistol/PistolWeaponBase.prefab",
            "Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/Shotgun/ShotgunWeapon.prefab",
            "Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/SniperRifle/SniperRifleWeapon.prefab",
            "Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/RocketLauncher/RocketLauncherWeapon.prefab",
            "Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/Bow/BowWeapon.prefab",
        };

        private const string OutputFolder = "Assets/Prefabs/Items/Converted";

        [MenuItem("Tools/DIG/Quick Convert Missing Weapons")]
        public static void ConvertMissingWeapons()
        {
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }

            int converted = 0;
            foreach (var path in WeaponsToConvert)
            {
                var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (sourcePrefab == null)
                {
                    Debug.LogWarning($"[QuickConvert] Could not find: {path}");
                    continue;
                }

                string outputName = Path.GetFileNameWithoutExtension(path) + "_ECS.prefab";
                string outputPath = Path.Combine(OutputFolder, outputName);

                // Check if already converted
                if (File.Exists(outputPath))
                {
                    Debug.Log($"[QuickConvert] Already exists: {outputName}");
                    continue;
                }

                // Instantiate, strip Opsive components, add WeaponAuthoring
                var instance = Object.Instantiate(sourcePrefab);
                instance.name = Path.GetFileNameWithoutExtension(outputName);

                // Remove Opsive components (anything in Opsive namespace)
                var allComponents = instance.GetComponentsInChildren<Component>(true);
                foreach (var comp in allComponents)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();
                    if (type.Namespace != null && type.Namespace.StartsWith("Opsive"))
                    {
                        Object.DestroyImmediate(comp);
                    }
                }

                // Add WeaponAuthoring if not present
                if (instance.GetComponent<DIG.Weapons.Authoring.WeaponAuthoring>() == null)
                {
                    instance.AddComponent<DIG.Weapons.Authoring.WeaponAuthoring>();
                }

                // Save as prefab
                PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
                Object.DestroyImmediate(instance);

                Debug.Log($"[QuickConvert] Created: {outputPath}");
                converted++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[QuickConvert] Done! Converted {converted} weapons.");
        }
    }
}
