using UnityEngine;
using UnityEditor;
using DIG.Combat.Authoring;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    public class TestingModule : ICharacterModule
    {
        private Vector2 _scrollPosition;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Testing Utilities", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Spawn test objects and damage sources.", MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawDamageTestSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawDamageTestSection()
        {
            EditorGUILayout.LabelField("Damage Tests", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Spawn Hitbox Dummy", GUILayout.Height(30)))
            {
                CreateHitboxDummy();
            }
            
            if (GUILayout.Button("Spawn Test Turret", GUILayout.Height(30)))
            {
                CreateTestTurret();
            }
            
            if (GUILayout.Button("Spawn Damage Zone", GUILayout.Height(30)))
            {
                CreateDamageZone();
            }
            
            if (GUILayout.Button("Spawn Heal Station", GUILayout.Height(30)))
            {
                CreateHealStation();
            }
        }

        private void CreateHitboxDummy()
        {
            GameObject go = new GameObject("Hitbox Dummy");
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            // Updated to use the correct DamageableAuthoring
            var damageable = go.AddComponent<DamageableAuthoring>();
            damageable.MaxHealth = 100f;
            damageable.ShowHealthBar = true;
            damageable.ShowDamageNumbers = true;

            // Basic visuals
            CreatePrimitiveChild(go, "Head", PrimitiveType.Sphere, new Vector3(0, 1.7f, 0), 0.3f);
            CreatePrimitiveChild(go, "Torso_Spine", PrimitiveType.Capsule, new Vector3(0, 1.2f, 0), 0.5f);
            CreatePrimitiveChild(go, "Arm_L", PrimitiveType.Capsule, new Vector3(-0.6f, 1.3f, 0), 0.2f);
            CreatePrimitiveChild(go, "Arm_R", PrimitiveType.Capsule, new Vector3(0.6f, 1.3f, 0), 0.2f);
            
            Debug.Log($"Created Hitbox Dummy with DamageableAuthoring.");
        }

        private void CreateTestTurret()
        {
            GameObject go = new GameObject("Test Turret");
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
            
            // Note: Keeping minimal as original component imports were ambiguous
            Debug.Log("Created Test Turret Base.");
            CreatePrimitiveChild(go, "Model", PrimitiveType.Cube, Vector3.zero, 1.0f);
        }

        private void CreateDamageZone()
        {
            GameObject go = new GameObject("Damage Zone");
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            var vis = CreatePrimitiveChild(go, "Visuals", PrimitiveType.Sphere, Vector3.zero, 1.0f);
            vis.transform.localScale = Vector3.one * 5.0f; 
            
            Debug.Log("Created Damage Zone Base.");
        }

        private void CreateHealStation()
        {
            GameObject go = new GameObject("Heal Station");
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;

            CreatePrimitiveChild(go, "Model", PrimitiveType.Cube, Vector3.zero, 1.0f);
            Debug.Log("Created Heal Station Base.");
        }

        private GameObject CreatePrimitiveChild(GameObject parent, string name, PrimitiveType type, Vector3 localPos, float scale)
        {
            GameObject child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = localPos;
            child.transform.localScale = Vector3.one * scale;
            return child;
        }
    }
}
