using UnityEditor;
using UnityEngine;
using Player.Settings;

// Creates default ScriptableObject assets for designers if they are missing.
// Run manually via menu instead of on every script reload.
static class EnsureSettingsAssets
{
    [MenuItem("DIG/Setup/Ensure Settings Assets")]
    public static void EnsureAssets()
    {
        // Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        CreateIfMissing<LeanSettings>("Assets/Resources/LeanSettings.asset");
    }

    static void CreateIfMissing<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return;

        var inst = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(inst, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"EnsureSettingsAssets: Created default asset at {path}");
    }
}
