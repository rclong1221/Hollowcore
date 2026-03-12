using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to find and report missing script references on prefabs.
/// Use Tools/Debug/Find Missing Scripts on Prefabs to scan all prefabs.
/// </summary>
public static class FindMissingScripts
{
    [MenuItem("Tools/Debug/Find Missing Scripts on Prefabs")]
    public static void FindMissingScriptsOnPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int totalPrefabs = 0;
        int prefabsWithMissing = 0;
        int totalMissingScripts = 0;

        Debug.Log($"[FindMissingScripts] Scanning {prefabGuids.Length} prefabs...");

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null)
                continue;

            totalPrefabs++;

            // Check all GameObjects in the prefab hierarchy
            GameObject[] allObjects = new GameObject[] { prefab };
            var children = prefab.GetComponentsInChildren<Transform>(true);
            
            bool prefabHasMissing = false;
            int missingInThisPrefab = 0;

            foreach (Transform child in children)
            {
                Component[] components = child.GetComponents<Component>();
                
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        string objPath = GetGameObjectPath(child.gameObject);
                        Debug.LogError($"Missing script on: {path} -> {objPath} (component index {i})", prefab);
                        
                        prefabHasMissing = true;
                        missingInThisPrefab++;
                        totalMissingScripts++;
                    }
                }
            }

            if (prefabHasMissing)
            {
                prefabsWithMissing++;
                Debug.LogWarning($"[FindMissingScripts] '{path}' has {missingInThisPrefab} missing script(s)", prefab);
            }
        }

        if (totalMissingScripts > 0)
        {
            Debug.LogError($"[FindMissingScripts] FOUND {totalMissingScripts} missing script reference(s) across {prefabsWithMissing} prefab(s) out of {totalPrefabs} total.");
        }
        else
        {
            Debug.Log($"[FindMissingScripts] No missing scripts found! Scanned {totalPrefabs} prefabs.");
        }
    }

    [MenuItem("Tools/Debug/Find Missing Scripts in Scene")]
    public static void FindMissingScriptsInScene()
    {
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int totalMissing = 0;

        Debug.Log($"[FindMissingScripts] Scanning {allObjects.Length} GameObjects in scene...");

        foreach (GameObject go in allObjects)
        {
            Component[] components = go.GetComponents<Component>();
            
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    string objPath = GetGameObjectPath(go);
                    Debug.LogError($"Missing script on scene object: {objPath} (component index {i})", go);
                    totalMissing++;
                }
            }
        }

        if (totalMissing > 0)
        {
            Debug.LogError($"[FindMissingScripts] FOUND {totalMissing} missing script reference(s) in scene.");
        }
        else
        {
            Debug.Log($"[FindMissingScripts] No missing scripts found in scene!");
        }
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
}
