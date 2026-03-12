using UnityEngine;
using UnityEditor;
using Audio.Systems;

namespace DIG.Editor
{
    /// <summary>
    /// Editor utility for creating EPIC 13.18 Surface Effects test objects.
    /// Menu: GameObject > DIG - Test Objects > Environment > Surface Tests
    /// </summary>
    public static class SurfaceTestObjectCreator
    {
        private const string MENU_PATH = "GameObject/DIG - Test Objects/Environment/Surface Tests/";

        // Surface material IDs (should match SurfaceMaterialRegistry)
        private static readonly int MATERIAL_DIRT = 1;
        private static readonly int MATERIAL_METAL = 2;
        private static readonly int MATERIAL_WOOD = 3;
        private static readonly int MATERIAL_CONCRETE = 4;
        private static readonly int MATERIAL_GLASS = 5;
        private static readonly int MATERIAL_WATER = 6;
        private static readonly int MATERIAL_STONE = 7;
        private static readonly int MATERIAL_GRASS = 8;

        // Colors for visual distinction
        private static readonly Color DirtColor = new Color(0.45f, 0.35f, 0.2f);
        private static readonly Color MetalColor = new Color(0.6f, 0.6f, 0.65f);
        private static readonly Color WoodColor = new Color(0.55f, 0.4f, 0.25f);
        private static readonly Color ConcreteColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GlassColor = new Color(0.7f, 0.85f, 0.9f, 0.5f);
        private static readonly Color WaterColor = new Color(0.2f, 0.4f, 0.8f, 0.6f);
        private static readonly Color StoneColor = new Color(0.4f, 0.4f, 0.42f);
        private static readonly Color GrassColor = new Color(0.3f, 0.5f, 0.2f);
        private static readonly Color SandColor = new Color(0.85f, 0.75f, 0.5f);
        private static readonly Color WhiteColor = new Color(0.95f, 0.95f, 0.95f);

        #region Menu Items

        [MenuItem(MENU_PATH + "Complete Surface Test Area", priority = 0)]
        public static void CreateCompleteSurfaceTestArea()
        {
            var root = new GameObject("Surface Tests");
            root.transform.position = GetSceneViewPosition();

            CreateShootingRange(root.transform, Vector3.zero);
            CreateDecalStressWall(root.transform, new Vector3(15f, 0, 0));
            CreateFootstepPath(root.transform, new Vector3(0, 0, -15f));
            CreateAudioStressTest(root.transform, new Vector3(15f, 0, -15f));

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Surface Test Area");
        }

        [MenuItem(MENU_PATH + "13.18.T1 Material Shooting Range", priority = 10)]
        public static void CreateShootingRangeMenuItem()
        {
            var root = CreateShootingRange(null, GetSceneViewPosition());
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Shooting Range");
        }

        [MenuItem(MENU_PATH + "13.18.T2 Decal Stress Test Wall", priority = 11)]
        public static void CreateDecalStressWallMenuItem()
        {
            var root = CreateDecalStressWall(null, GetSceneViewPosition());
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Decal Wall");
        }

        [MenuItem(MENU_PATH + "13.18.T3 Footstep Path", priority = 12)]
        public static void CreateFootstepPathMenuItem()
        {
            var root = CreateFootstepPath(null, GetSceneViewPosition());
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Footstep Path");
        }

        [MenuItem(MENU_PATH + "13.18.T5 Audio Pool Stress Test", priority = 14)]
        public static void CreateAudioStressTestMenuItem()
        {
            var root = CreateAudioStressTest(null, GetSceneViewPosition());
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Audio Stress Test");
        }

        #endregion

        #region Test Environment Creators

        /// <summary>
        /// 13.18.T1: Material Shooting Range - Targets with different surface materials.
        /// </summary>
        private static GameObject CreateShootingRange(Transform parent, Vector3 position)
        {
            var root = new GameObject("Shooting Range");
            if (parent != null) root.transform.SetParent(parent);
            root.transform.position = position;

            // Floor
            var floor = CreatePrimitive(PrimitiveType.Cube, "Floor", root.transform);
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(12f, 0.2f, 8f);
            SetMaterialColor(floor, ConcreteColor);

            // Back wall (white for contrast)
            var backWall = CreatePrimitive(PrimitiveType.Cube, "BackWall", root.transform);
            backWall.transform.localPosition = new Vector3(0, 2f, 4f);
            backWall.transform.localScale = new Vector3(12f, 4f, 0.2f);
            SetMaterialColor(backWall, WhiteColor);

            // Create targets
            CreateSurfaceTarget(root.transform, "Target_Dirt", new Vector3(-5f, 1.5f, 3.5f), DirtColor, MATERIAL_DIRT);
            CreateSurfaceTarget(root.transform, "Target_Metal", new Vector3(-3f, 1.5f, 3.5f), MetalColor, MATERIAL_METAL);
            CreateSurfaceTarget(root.transform, "Target_Wood", new Vector3(-1f, 1.5f, 3.5f), WoodColor, MATERIAL_WOOD);
            CreateSurfaceTarget(root.transform, "Target_Concrete", new Vector3(1f, 1.5f, 3.5f), ConcreteColor, MATERIAL_CONCRETE);
            CreateSurfaceTarget(root.transform, "Target_Glass", new Vector3(3f, 1.5f, 3.5f), GlassColor, MATERIAL_GLASS);
            CreateSurfaceTarget(root.transform, "Target_Water", new Vector3(5f, 1.5f, 3.5f), WaterColor, MATERIAL_WATER);

            // Labels
            CreateWorldLabel(root.transform, "Dirt", new Vector3(-5f, 0.1f, 2.5f));
            CreateWorldLabel(root.transform, "Metal", new Vector3(-3f, 0.1f, 2.5f));
            CreateWorldLabel(root.transform, "Wood", new Vector3(-1f, 0.1f, 2.5f));
            CreateWorldLabel(root.transform, "Concrete", new Vector3(1f, 0.1f, 2.5f));
            CreateWorldLabel(root.transform, "Glass", new Vector3(3f, 0.1f, 2.5f));
            CreateWorldLabel(root.transform, "Water", new Vector3(5f, 0.1f, 2.5f));

            // Firing line
            var firingLine = CreatePrimitive(PrimitiveType.Cube, "FiringLine", root.transform);
            firingLine.transform.localPosition = new Vector3(0, 0.01f, -2f);
            firingLine.transform.localScale = new Vector3(10f, 0.02f, 0.1f);
            SetMaterialColor(firingLine, Color.yellow);

            return root;
        }

        /// <summary>
        /// 13.18.T2: Decal Stress Test Wall - Large white wall for testing decal limits.
        /// </summary>
        private static GameObject CreateDecalStressWall(Transform parent, Vector3 position)
        {
            var root = new GameObject("Decal Wall");
            if (parent != null) root.transform.SetParent(parent);
            root.transform.position = position;

            // Floor
            var floor = CreatePrimitive(PrimitiveType.Cube, "Floor", root.transform);
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(10f, 0.2f, 6f);
            SetMaterialColor(floor, ConcreteColor);

            // Large white target wall
            var targetWall = CreatePrimitive(PrimitiveType.Cube, "TargetWall", root.transform);
            targetWall.transform.localPosition = new Vector3(0, 3f, 3f);
            targetWall.transform.localScale = new Vector3(8f, 6f, 0.2f);
            SetMaterialColor(targetWall, WhiteColor);
            AddSurfaceMaterial(targetWall, MATERIAL_CONCRETE);

            // UI Panel placeholder
            var uiPanel = new GameObject("UI_DecalCounter");
            uiPanel.transform.SetParent(root.transform);
            uiPanel.transform.localPosition = new Vector3(-5f, 2f, 0);
            
            // Add a text mesh for in-scene visibility
            var textMesh = uiPanel.AddComponent<TextMesh>();
            textMesh.text = "Decal Count: 0/100";
            textMesh.fontSize = 24;
            textMesh.characterSize = 0.1f;
            textMesh.color = Color.white;

            // Reset button (visual only)
            var resetButton = CreatePrimitive(PrimitiveType.Cube, "ResetButton", root.transform);
            resetButton.transform.localPosition = new Vector3(-5f, 1f, 0);
            resetButton.transform.localScale = new Vector3(1f, 0.5f, 0.3f);
            SetMaterialColor(resetButton, Color.red);

            CreateWorldLabel(root.transform, "RESET", new Vector3(-5f, 0.7f, 0));

            return root;
        }

        /// <summary>
        /// 13.18.T3: Footstep Path - Walkway with different floor materials.
        /// </summary>
        private static GameObject CreateFootstepPath(Transform parent, Vector3 position)
        {
            var root = new GameObject("Footstep Path");
            if (parent != null) root.transform.SetParent(parent);
            root.transform.position = position;

            float sectionWidth = 3f;
            float sectionLength = 4f;
            float x = 0;

            // Create path sections
            CreateFloorSection(root.transform, "Path_Dirt", new Vector3(x, 0, 0), sectionWidth, sectionLength, DirtColor, MATERIAL_DIRT);
            CreateWorldLabel(root.transform, "DIRT", new Vector3(x, 0.01f, 1.5f));
            x += sectionWidth;

            CreateFloorSection(root.transform, "Path_Metal", new Vector3(x, 0, 0), sectionWidth, sectionLength, MetalColor, MATERIAL_METAL);
            CreateWorldLabel(root.transform, "METAL", new Vector3(x, 0.01f, 1.5f));
            x += sectionWidth;

            CreateFloorSection(root.transform, "Path_Wood", new Vector3(x, 0, 0), sectionWidth, sectionLength, WoodColor, MATERIAL_WOOD);
            CreateWorldLabel(root.transform, "WOOD", new Vector3(x, 0.01f, 1.5f));
            x += sectionWidth;

            CreateFloorSection(root.transform, "Path_Stone", new Vector3(x, 0, 0), sectionWidth, sectionLength, StoneColor, MATERIAL_STONE);
            CreateWorldLabel(root.transform, "STONE", new Vector3(x, 0.01f, 1.5f));
            x += sectionWidth;

            CreateFloorSection(root.transform, "Path_Grass", new Vector3(x, 0, 0), sectionWidth, sectionLength, GrassColor, MATERIAL_GRASS);
            CreateWorldLabel(root.transform, "GRASS", new Vector3(x, 0.01f, 1.5f));
            x += sectionWidth;

            CreateFloorSection(root.transform, "Path_Water", new Vector3(x, 0, 0), sectionWidth, sectionLength, WaterColor, MATERIAL_WATER);
            CreateWorldLabel(root.transform, "WATER", new Vector3(x, 0.01f, 1.5f));

            // Side walls for guidance
            var leftWall = CreatePrimitive(PrimitiveType.Cube, "LeftWall", root.transform);
            leftWall.transform.localPosition = new Vector3((x - sectionWidth + sectionWidth) / 2f, 0.5f, -sectionLength / 2f - 0.1f);
            leftWall.transform.localScale = new Vector3(x + sectionWidth, 1f, 0.1f);
            SetMaterialColor(leftWall, ConcreteColor);

            var rightWall = CreatePrimitive(PrimitiveType.Cube, "RightWall", root.transform);
            rightWall.transform.localPosition = new Vector3((x - sectionWidth + sectionWidth) / 2f, 0.5f, sectionLength / 2f + 0.1f);
            rightWall.transform.localScale = new Vector3(x + sectionWidth, 1f, 0.1f);
            SetMaterialColor(rightWall, ConcreteColor);

            return root;
        }

        /// <summary>
        /// 13.18.T5: Audio Pool Stress Test - Multiple targets for rapid fire testing.
        /// </summary>
        private static GameObject CreateAudioStressTest(Transform parent, Vector3 position)
        {
            var root = new GameObject("Audio Stress Test");
            if (parent != null) root.transform.SetParent(parent);
            root.transform.position = position;

            // Floor
            var floor = CreatePrimitive(PrimitiveType.Cube, "Floor", root.transform);
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
            SetMaterialColor(floor, ConcreteColor);

            // Multiple target walls for rapid hitting
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * 4f;
                float z = Mathf.Cos(angle * Mathf.Deg2Rad) * 4f;

                var target = CreatePrimitive(PrimitiveType.Cube, $"Target_{i}", root.transform);
                target.transform.localPosition = new Vector3(x, 1.5f, z);
                target.transform.localScale = new Vector3(2f, 3f, 0.3f);
                target.transform.LookAt(root.transform.position + Vector3.up * 1.5f);
                SetMaterialColor(target, MetalColor);
                AddSurfaceMaterial(target, MATERIAL_METAL);
            }

            // UI Panel placeholder
            var uiPanel = new GameObject("UI_AudioCounter");
            uiPanel.transform.SetParent(root.transform);
            uiPanel.transform.localPosition = new Vector3(0, 4f, 0);
            
            var textMesh = uiPanel.AddComponent<TextMesh>();
            textMesh.text = "Audio Pool: 0/16 active";
            textMesh.fontSize = 24;
            textMesh.characterSize = 0.1f;
            textMesh.color = Color.green;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;

            // FPS counter placeholder
            var fpsPanel = new GameObject("UI_FPSCounter");
            fpsPanel.transform.SetParent(root.transform);
            fpsPanel.transform.localPosition = new Vector3(0, 3.5f, 0);
            
            var fpsText = fpsPanel.AddComponent<TextMesh>();
            fpsText.text = "FPS: 60";
            fpsText.fontSize = 24;
            fpsText.characterSize = 0.08f;
            fpsText.color = Color.yellow;
            fpsText.alignment = TextAlignment.Center;
            fpsText.anchor = TextAnchor.MiddleCenter;

            return root;
        }

        #endregion

        #region Helper Methods

        private static GameObject CreateSurfaceTarget(Transform parent, string name, Vector3 position, Color color, int materialId)
        {
            var target = CreatePrimitive(PrimitiveType.Cube, name, parent);
            target.transform.localPosition = position;
            target.transform.localScale = new Vector3(1.5f, 2f, 0.3f);
            SetMaterialColor(target, color);
            AddSurfaceMaterial(target, materialId);
            return target;
        }

        private static GameObject CreateFloorSection(Transform parent, string name, Vector3 position, float width, float length, Color color, int materialId)
        {
            var section = CreatePrimitive(PrimitiveType.Cube, name, parent);
            section.transform.localPosition = position;
            section.transform.localScale = new Vector3(width, 0.2f, length);
            SetMaterialColor(section, color);
            AddSurfaceMaterial(section, materialId);
            return section;
        }

        private static void CreateWorldLabel(Transform parent, string text, Vector3 position)
        {
            var label = new GameObject($"Label_{text}");
            label.transform.SetParent(parent);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(90, 0, 0);

            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.05f;
            textMesh.color = Color.black;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
        }

        private static void AddSurfaceMaterial(GameObject go, int materialId)
        {
            var authoring = go.AddComponent<SurfaceMaterialAuthoring>();
            
            // Try to find material in registry
            var registry = Resources.Load<SurfaceMaterialRegistry>("Audio/SurfaceMaterialRegistry");
            if (registry != null && registry.TryGetById(materialId, out var material))
            {
                authoring.Material = material;
            }
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);
            return obj;
        }

        private static void SetMaterialColor(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            string matName = $"SurfaceMat_{color.GetHashCode():X}";
            Material material = GetOrCreateMaterial(matName, color);
            
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material GetOrCreateMaterial(string matName, Color color)
        {
            string folderPath = "Assets/Generated/Materials";
            string fullPath = $"{folderPath}/{matName}.mat";

            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Generated", "Materials");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                material = new Material(shader);
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);

                // Handle transparency
                if (color.a < 1f)
                {
                    material.SetFloat("_Surface", 1); // Transparent
                    material.SetFloat("_Blend", 0); // Alpha
                    material.renderQueue = 3000;
                }

                AssetDatabase.CreateAsset(material, fullPath);
            }

            return material;
        }

        private static Vector3 GetSceneViewPosition()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                var cam = SceneView.lastActiveSceneView.camera;
                return cam.transform.position + cam.transform.forward * 10f;
            }
            return Vector3.zero;
        }

        #endregion
    }
}
