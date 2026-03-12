using UnityEngine;
using UnityEditor;
using System.Linq;

namespace DIG.Editor
{
    /// <summary>
    /// EPIC 14.18 - Quick setup for IK and Camera components on Atlas prefabs.
    /// </summary>
    public static class AtlasIKSetup
    {
        [MenuItem("DIG/Player/Setup Atlas IK Components")]
        public static void SetupAtlasIKComponents()
        {
            // Find Atlas prefabs
            var serverGuids = AssetDatabase.FindAssets("Atlas_Server t:Prefab");
            var clientGuids = AssetDatabase.FindAssets("Atlas_Client t:Prefab");
            
            bool anyChanges = false;
            
            // Setup Server prefab (ECS authoring)
            foreach (var guid in serverGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    anyChanges |= SetupServerPrefab(prefab, path);
                }
            }
            
            // Setup Client prefab (MonoBehaviours)
            foreach (var guid in clientGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    anyChanges |= SetupClientPrefab(prefab, path);
                }
            }
            
            if (!anyChanges)
            {
                EditorUtility.DisplayDialog(
                    "Atlas IK Setup",
                    "All Atlas prefabs already have IK components configured!",
                    "OK"
                );
            }
            else
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    "Atlas IK Setup Complete",
                    "IK components have been added/configured.\n\n" +
                    "Remember to:\n" +
                    "1. Open 'memuanim' Animator Controller\n" +
                    "2. Select Base Layer\n" +
                    "3. Enable IK Pass checkbox",
                    "OK"
                );
            }
        }
        
        private static bool SetupServerPrefab(GameObject prefab, string path)
        {
            bool changed = false;
            
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = editScope.prefabContentsRoot;
                
                // Add IKAuthoring if missing
                var ikAuthoring = root.GetComponent<DIG.Player.Authoring.IK.IKAuthoring>();
                if (ikAuthoring == null)
                {
                    ikAuthoring = root.AddComponent<DIG.Player.Authoring.IK.IKAuthoring>();
                    Debug.Log($"[AtlasIKSetup] Added IKAuthoring to {prefab.name}");
                    changed = true;
                }
                
                // Configure IKAuthoring for head look
                if (ikAuthoring.LookAtMode != DIG.Player.IK.LookAtMode.MouseAim)
                {
                    ikAuthoring.LookAtMode = DIG.Player.IK.LookAtMode.MouseAim;
                    changed = true;
                }
                
                // Ensure proper values
                if (ikAuthoring.MaxHeadAngle < 1f)
                {
                    ikAuthoring.MaxHeadAngle = 70f;
                    ikAuthoring.MaxSpineAngle = 30f;
                    ikAuthoring.MaxTotalAngle = 120f;
                    changed = true;
                }
                
                if (changed)
                {
                    EditorUtility.SetDirty(root);
                }
            }
            
            return changed;
        }
        
        private static bool SetupClientPrefab(GameObject prefab, string path)
        {
            bool changed = false;
            
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = editScope.prefabContentsRoot;
                
                // Find the Animator
                var animator = root.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"[AtlasIKSetup] No Animator found in {prefab.name}");
                    return false;
                }
                
                // Add PlayerIKBridge to the same GameObject as Animator
                var playerIKBridge = animator.GetComponent<DIG.Player.View.PlayerIKBridge>();
                if (playerIKBridge == null)
                {
                    animator.gameObject.AddComponent<DIG.Player.View.PlayerIKBridge>();
                    Debug.Log($"[AtlasIKSetup] Added PlayerIKBridge to {animator.gameObject.name} in {prefab.name}");
                    changed = true;
                }
                
                if (changed)
                {
                    EditorUtility.SetDirty(root);
                }
            }
            
            return changed;
        }
        
        [MenuItem("DIG/Player/Verify Atlas IK Setup")]
        public static void VerifyAtlasIKSetup()
        {
            var issues = new System.Collections.Generic.List<string>();
            
            // Check Server prefabs
            var serverGuids = AssetDatabase.FindAssets("Atlas_Server t:Prefab");
            foreach (var guid in serverGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var ikAuthoring = prefab.GetComponent<DIG.Player.Authoring.IK.IKAuthoring>();
                    if (ikAuthoring == null)
                        issues.Add($"❌ {prefab.name}: Missing IKAuthoring");
                    else if (ikAuthoring.LookAtMode == DIG.Player.IK.LookAtMode.Disabled)
                        issues.Add($"⚠️ {prefab.name}: LookAtMode is Disabled");
                    else if (ikAuthoring.MaxTotalAngle < 1f)
                        issues.Add($"⚠️ {prefab.name}: MaxTotalAngle is 0 (head won't turn)");
                    else
                        issues.Add($"✅ {prefab.name}: IKAuthoring configured");
                }
            }
            
            // Check Client prefabs
            var clientGuids = AssetDatabase.FindAssets("Atlas_Client t:Prefab");
            foreach (var guid in clientGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var animator = prefab.GetComponentInChildren<Animator>();
                    if (animator == null)
                    {
                        issues.Add($"❌ {prefab.name}: No Animator found");
                    }
                    else
                    {
                        var playerIKBridge = animator.GetComponent<DIG.Player.View.PlayerIKBridge>();
                        if (playerIKBridge == null)
                            issues.Add($"❌ {prefab.name}: Missing PlayerIKBridge on Animator GameObject");
                        else
                            issues.Add($"✅ {prefab.name}: PlayerIKBridge present");
                    }
                }
            }
            
            // Check Animator Controller IK Pass (can't easily check this via script)
            issues.Add("");
            issues.Add("MANUAL CHECK REQUIRED:");
            issues.Add("• Open 'memuanim' Animator Controller");
            issues.Add("• Select each Layer → verify IK Pass is checked");
            
            EditorUtility.DisplayDialog(
                "Atlas IK Verification",
                string.Join("\n", issues),
                "OK"
            );
        }
    }
}
