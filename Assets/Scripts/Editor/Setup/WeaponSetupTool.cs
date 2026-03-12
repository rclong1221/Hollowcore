using UnityEngine;
using UnityEditor;
using DIG.Items.Bridges;

namespace DIG.Editor.Setup
{
    /// <summary>
    /// [SETUP] Tool - Pre-EPIC 14
    /// 
    /// Purpose: Configures WeaponEquipVisualBridge on player prefab with attach points and weapon models.
    /// When to use: One-time when setting up a new player prefab.
    /// Safe to remove: After player prefab is correctly configured.
    /// </summary>
    public class Setup_WeaponVisuals : EditorWindow
    {
        [MenuItem("DIG/Setup/Weapon Visuals")]
        public static void Setup()
    {
        string prefabPath = "Assets/Prefabs/Warrok_Client.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab == null)
        {
            Debug.LogError($"Could not find prefab at {prefabPath}");
            return;
        }
        
        // Open for editing
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        using (var editScope = new PrefabUtility.EditPrefabContentsScope(assetPath))
        {
            GameObject root = editScope.prefabContentsRoot;
            
            var bridge = root.GetComponent<WeaponEquipVisualBridge>();
            if (bridge == null)
            {
                bridge = root.AddComponent<WeaponEquipVisualBridge>();
                Debug.Log("Added WeaponEquipVisualBridge component");
            }
            
            // Enable debug logging
            bridge.DebugLogging = true;
            
            var animator = root.GetComponent<Animator>();
            
            // 1. Try finding Right Hand
            if (bridge.HandAttachPoint == null)
            {
                // Method A: Unity Humanoid (Best/Standard)
                if (animator != null && animator.isHuman)
                {
                    var bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (bone != null)
                    {
                        bridge.HandAttachPoint = bone;
                        Debug.Log($"Found Hand Attach Point (Humanoid): {bone.name}");
                    }
                }

                // Method B: Name Search (Refined Fallback)
                if (bridge.HandAttachPoint == null)
                {
                    string[] handNames = new[] { 
                        "RightHand", "Right Hand", "hand_r", "Hand_R", 
                        "mixamorig:RightHand", "Bip001 R Hand", "R_Hand", "Character1_RightHand" 
                    };
                    
                    foreach (var name in handNames)
                    {
                        var hand = FindDeepChild(root.transform, name);
                        if (hand != null)
                        {
                            bridge.HandAttachPoint = hand;
                            Debug.Log($"Found Hand Attach Point (ByName): {hand.name}");
                            break;
                        }
                    }
                }
            }

            // 2. Try finding Back/Spine
            if (bridge.BackAttachPoint == null)
            {
                // Method A: Unity Humanoid
                if (animator != null && animator.isHuman)
                {
                    // Prefer UpperChest, fall back to Chest/Spine
                    var bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                    if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
                    
                    if (bone != null)
                    {
                        bridge.BackAttachPoint = bone;
                        Debug.Log($"Found Back Attach Point (Humanoid): {bone.name}");
                    }
                }

                // Method B: Name Search
                if (bridge.BackAttachPoint == null)
                {
                    string[] spineNames = new[] { 
                        "Spine2", "Spine1", "Spine", 
                        "mixamorig:Spine2", "mixamorig:Spine1", "Bip001 Spine2" 
                    };
                    
                    foreach (var name in spineNames)
                    {
                        var spine = FindDeepChild(root.transform, name);
                        if (spine != null)
                        {
                            bridge.BackAttachPoint = spine;
                            Debug.Log($"Found Back Attach Point (ByName): {spine.name}");
                            break;
                        }
                    }
                }
            }
                
            // Try to find weapon models
            // Assuming standard naming or searching
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            
            foreach (Transform t in children)
            {
                if (t.name.Contains("Assault"))
                {
                    Debug.Log($"Found Assault Rifle: {t.name}");
                    bridge.WeaponModels[1] = t.gameObject;
                    t.gameObject.SetActive(false); // Ensure hidden by default
                }
                else if (t.name.Contains("SMG") || t.name.Contains("SubMachine"))
                {
                    Debug.Log($"Found SMG: {t.name}");
                    bridge.WeaponModels[2] = t.gameObject;
                    t.gameObject.SetActive(false);
                }
                else if (t.name.Contains("Pistol") || t.name.Contains("Handgun"))
                {
                    Debug.Log($"Found Pistol: {t.name}");
                    bridge.WeaponModels[3] = t.gameObject;
                    t.gameObject.SetActive(false);
                }
                else if (t.name.Contains("Shotgun"))
                {
                    Debug.Log($"Found Shotgun: {t.name}");
                    bridge.WeaponModels[4] = t.gameObject;
                    t.gameObject.SetActive(false);
                }
            }
            
            Debug.Log($"Weapon Setup Complete. Bridge Attached. Logging Enabled.");
        }
    }
    
    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
}
