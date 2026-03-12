using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.Utils
{
    public class MaterialCleanerTool : EditorWindow
    {
        [MenuItem("Tools/DIG/Materials/Log Missing Materials in Selection")]
        public static void LogMissingInSelection()
        {
            var gameObjects = Selection.gameObjects;
            if (gameObjects.Length == 0)
            {
                Debug.LogWarning("Please select GameObjects (prefabs or scene objects) to scan.");
                return;
            }

            int count = 0;
            foreach (var go in gameObjects)
            {
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    // Check main sharedMaterial
                    if (r.sharedMaterial == null)
                    {
                        Debug.LogError($"[MaterialCleaner] Null Material on: {GetPath(r.transform)}", r.gameObject);
                        count++;
                    }
                    else
                    {
                        // Check for nulls in the array (multi-material objects)
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null)
                            {
                                Debug.LogError($"[MaterialCleaner] Null Material (Element {i}) on: {GetPath(r.transform)}", r.gameObject);
                                count++;
                            }
                        }
                    }
                }
            }
            
            Debug.Log($"Scan complete. Found {count} renderers with null materials.");
        }

        [MenuItem("Tools/DIG/Materials/Fix Missing Materials (Assign Default Lit)")]
        public static void FixMissingInSelection()
        {
            var gameObjects = Selection.gameObjects;
            if (gameObjects.Length == 0)
            {
                Debug.LogWarning("Please select GameObjects to fix.");
                return;
            }

            // Find a valid default material (URP Lit) or fallback
            Material defaultMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prototyping/Prototype_Grey.mat"); // Example path, adjust if needed
            if (defaultMat == null)
            {
                // Try to find ANY built-in material or creating one isn't great, let's try to find standard URP
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    defaultMat = new Material(shader);
                    defaultMat.name = "Generated_Default_Lit";
                    // We don't save this to disk, just memory for now, which is risky for prefabs.
                    // Better to find a default Unity material.
                    defaultMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                }
            }

            if (defaultMat == null)
            {
                Debug.LogError("Could not find a default material to assign!");
                return;
            }

            int fixedCount = 0;
            foreach (var go in gameObjects)
            {
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    bool changed = false;
                    var mats = r.sharedMaterials;
                    // Check if the array itself is empty/null or contains nulls
                    if (mats.Length == 0)
                    {
                        // If it has no materials, assign one
                         mats = new Material[] { defaultMat };
                         changed = true;
                    }
                    else
                    {
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null)
                            {
                                mats[i] = defaultMat;
                                changed = true;
                            }
                        }
                    }

                    if (changed || r.sharedMaterial == null)
                    {
                        // Record undo
                        Undo.RecordObject(r, "Fix Missing Materials");
                        r.sharedMaterials = mats;
                        EditorUtility.SetDirty(r.gameObject);
                        fixedCount++;
                        Debug.Log($"[Fixed] Assigned '{defaultMat.name}' to {GetPath(r.transform)}", r.gameObject);
                    }
                }
            }
            
            Debug.Log($"Fix complete. Fixed {fixedCount} renderers.");
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
