using UnityEngine;
using UnityEditor;
using System;
using Player.Authoring;
using DIG.Combat.Authoring;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    public class DamageableSetupModule : ICharacterModule
    {
        public void OnGUI()
        {
            EditorGUILayout.LabelField("Damageable Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Utilities for setting up damage and health components on characters.", MessageType.Info);
            EditorGUILayout.Space(10);

            DrawFallDamageSection();
            EditorGUILayout.Space(10);
            DrawGeneralSection();
        }

        private void DrawFallDamageSection()
        {
            EditorGUILayout.LabelField("Fall Damage", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Add FallDamageAuthoring to All Players", GUILayout.Height(30)))
            {
                ApplyFallDamageToAllPlayers();
            }
        }

        private void DrawGeneralSection()
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Damageable to Selected", GUILayout.Height(25)))
            {
                if (Selection.activeGameObject != null)
                {
                    if (Selection.activeGameObject.GetComponent<DamageableAuthoring>() == null)
                    {
                        var comp = Selection.activeGameObject.AddComponent<DamageableAuthoring>();
                        Undo.RegisterCreatedObjectUndo(comp, "Add DamageableAuthoring");
                        Debug.Log($"Added DamageableAuthoring to {Selection.activeGameObject.name}");
                    }
                }
            }
        }

        private void ApplyFallDamageToAllPlayers()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            int added = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                // Heuristic: prefab name contains "player" (case-insensitive)
                if (!go.name.ToLower().Contains("player")) continue;

                // Skip if already has the component
                var has = go.GetComponent<FallDamageAuthoring>() != null;
                if (has) continue;

                // Add component and set defaults
                Undo.RecordObject(go, "Add FallDamageAuthoring");
                var comp = go.AddComponent<FallDamageAuthoring>();
                
                // Defaults
                comp.SafeFallHeight = 2.5f;
                // Note: Other fields are protected/private in the original script, assuming defaults handled by component or baking
                
                EditorUtility.SetDirty(go);
                added++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"AddFallDamageAuthoring: added to {added} prefabs.");
        }
    }
}
