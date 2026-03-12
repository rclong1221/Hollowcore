using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class ScanForMissingScripts
{
    [MenuItem("Tools/Debug/Scan Project For Missing Scripts")]
    public static void ScanProject()
    {
        int totalPrefabs = 0;
        int prefabsWithMissing = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            totalPrefabs++;
            var missing = ScanGameObjectForMissing(go);
            if (missing.Count > 0)
            {
                prefabsWithMissing++;
                Debug.LogWarning($"Prefab '{path}' has {missing.Count} missing scripts/components:", go);
                foreach (var info in missing)
                    Debug.LogWarning(info, go);
            }
        }

        // Optionally scan scenes in Assets (without opening them) by parsing text, but that can be slow.

        Debug.Log($"Scan complete. Prefabs scanned: {totalPrefabs}. Prefabs with missing scripts: {prefabsWithMissing}.");
    }

    static List<string> ScanGameObjectForMissing(GameObject root)
    {
        var results = new List<string>();
        var allTransforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            var components = t.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    string path = GetHierarchyPath(t, root.name);
                    results.Add($"Missing script at '{path}' on prefab root '{root.name}' (child: '{t.name}')");
                }
            }
        }
        return results;
    }

    static string GetHierarchyPath(Transform t, string rootName)
    {
        var parts = new System.Collections.Generic.List<string>();
        var cur = t;
        while (cur != null && cur.name != rootName)
        {
            parts.Add(cur.name);
            cur = cur.parent;
        }
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }
}
