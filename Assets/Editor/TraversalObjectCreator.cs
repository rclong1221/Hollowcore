using UnityEngine;
using UnityEditor;
using Player.Authoring;
using Player.Components;
using DIG.Survival.Environment;
using DIG.Survival.Authoring;
using DIG.Swimming.Authoring;
using DIG.Survival.Physics.Authoring;
using Unity.NetCode;
using Unity.NetCode.Authoring;
using DIG.Testing;
using ZoneShapeType = DIG.Survival.Environment.ZoneShapeType;

namespace DIG.Editor
{
    /// <summary>
    /// Professional editor utility for creating test objects for player traversal systems.
    /// Creates objects with grid materials, height labels, and DOTS Physics components.
    /// Menu: GameObject > DIG - Traversal Objects
    /// </summary>
    public static class TraversalObjectCreator
    {
        private const string MenuRoot = "GameObject/DIG - Test Objects/Traversal/";
        private static UnityEngine.Material s_GridMaterial;
        private static UnityEngine.Material s_LadderMaterial;
        
        #region Test Course
        
        [MenuItem(MenuRoot + "Complete Test Course", false, 0)]
        private static void CreateCompleteTestCourse()
        {
            GameObject parent = new GameObject("Traversal_Test_Course");
            
            Vector3 basePos = Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
            {
                basePos = SceneView.lastActiveSceneView.camera.transform.position + 
                         SceneView.lastActiveSceneView.camera.transform.forward * 15f;
            }
            
            parent.transform.position = basePos;
            
            // Ground plane with grid - enlarged to 80x60m for spread out objects
            CreateGroundPlane(parent, Vector3.zero, new Vector3(80f, 1f, 60f));
            
            // Section 1: Mantle Heights (Far left)
            CreateMantleSection(parent, new Vector3(-30f, 0f, -15f));
            
            // Section 2: Vaults (Left center)
            CreateVaultSection(parent, new Vector3(-10f, 0f, -15f));
            
            // Section 3: Ladders (Right center)
            CreateLadderSection(parent, new Vector3(10f, 0f, -15f));
            
            // Section 4: Ramps (Far right)
            CreateRampSection(parent, new Vector3(30f, 0f, -15f));
            
            // Section 5: FREE CLIMB TEST SHAPES (Center - main testing area)
            CreateClimbingWallSection(parent, new Vector3(0f, 0f, 10f));
            
            // Section 6: Mixed Obstacles (Left of center)
            CreateMixedObstacleSection(parent, new Vector3(-30f, 0f, 10f));
            
            // Section 7: Advanced Climbables (Far back - giant shapes)
            CreateAdvancedClimbablesSection(parent, new Vector3(0f, 0f, 50f));

            // Section 8: Swimming (Back left)
            CreateSwimmingSection(parent, new Vector3(-30f, 0f, 50f));

            // Section 9: Hazards (Back right)
            CreateHazardSection(parent, new Vector3(30f, 0f, 50f));

            // Section 10: Horror (Far back right)
            CreateHorrorSection(parent, new Vector3(30f, 0f, 80f));
            
            // Section 11: Pushables (Far back center)
            CreatePushableSection(parent, new Vector3(0f, 0f, 90f));

            // Section 12: Ragdoll Test (Far back left)
            CreateRagdollTest(parent, new Vector3(-30f, 0f, 80f));
            
            // Section 13: Moving Platforms (Epic 13.1.1 - Far left back)
            CreateMovingPlatformSection(parent, new Vector3(-30f, 0f, 110f));
            
            // Section 14: External Forces (Epic 13.1.3 - Center far back)
            CreateExternalForceSection(parent, new Vector3(0f, 0f, 110f));

            // Section 15: Fall Tests (Epic 13.14 - Far right back)
            CreateFallTestSection(parent, new Vector3(30f, 0f, 110f));

            // Section 16: Crouch Tests (Epic 13.15 - Far left far back)
            CreateCrouchTestSection(parent, new Vector3(-30f, 0f, 140f));

            // Section 17: Gap Crossing (Epic 13.20 - Far right far back)
            CreateGapCrossingSection(parent, new Vector3(30f, 0f, 140f));
            
            // Add course title
            CreateTitleLabel(parent, "DIG TRAVERSAL TEST COURSE", new Vector3(0f, 8f, -20f), 1.5f);
            
            Selection.activeGameObject = parent;
            Undo.RegisterCreatedObjectUndo(parent, "Create Complete Test Course");
            
            UnityEngine.Debug.Log("✓ Created Complete Traversal Test Course\nSections spread across 80x60m area\nMain Climbing Shapes at center (0, 0, 10)\n+ Moving Platforms & External Forces (Epic 13.1)\n+ Crouch Tests (Epic 13.15)");
        }
        
        #endregion
        
        #region Course Sections
        
        private static void CreateMantleSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Mantle_Heights");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            // Section title
            CreateSectionLabel(section, "MANTLE HEIGHTS", new Vector3(0f, 3f, -1f));
            
            // Create mantleable obstacles at incrementing heights
            float[] heights = { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 1.75f, 2.0f };
            for (int i = 0; i < heights.Length; i++)
            {
                float height = heights[i];
                Vector3 pos = new Vector3(0f, height / 2f, i * 1.5f);
                
                GameObject box = CreateBox(section, $"Mantle_{height:F2}m", pos, 
                                          new Vector3(1.5f, height, 0.8f), 
                                          new Color(0.2f, 0.6f, 1f));
                
                CreateHeightLabel(box, $"{height:F2}m", new Vector3(0f, height / 2f + 0.3f, 0f));
            }
        }
        
        private static void CreateVaultSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Vaults");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "VAULTS", new Vector3(0f, 3f, -1f));
            
            // Wide vaults at different heights
            float[] heights = { 0.6f, 0.8f, 1.0f, 1.2f };
            for (int i = 0; i < heights.Length; i++)
            {
                float height = heights[i];
                Vector3 pos = new Vector3(0f, height / 2f, i * 2.0f);
                
                GameObject box = CreateBox(section, $"Vault_{height:F2}m", pos,
                                          new Vector3(2.5f, height, 0.5f),
                                          new Color(1f, 0.8f, 0.2f));
                
                CreateHeightLabel(box, $"{height:F2}m", new Vector3(0f, height / 2f + 0.3f, 0f));
            }
            
            // Double vault obstacle
            GameObject doubleVault = CreateBox(section, "Vault_Double_0.8m", new Vector3(0f, 0.4f, 9f),
                                               new Vector3(2.5f, 0.8f, 0.5f),
                                               new Color(1f, 0.7f, 0.1f));
            CreateHeightLabel(doubleVault, "0.80m x2", new Vector3(0f, 0.7f, 0f));
            
            GameObject doubleVault2 = CreateBox(section, "Vault_Double_0.8m_2", new Vector3(0f, 0.4f, 10.5f),
                                                new Vector3(2.5f, 0.8f, 0.5f),
                                                new Color(1f, 0.7f, 0.1f));
            CreateHeightLabel(doubleVault2, "0.80m x2", new Vector3(0f, 0.7f, 0f));
        }
        
        private static void CreateLadderSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Ladders");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "LADDERS", new Vector3(0f, 3f, -1f));
            
            // Various ladder heights
            float[] heights = { 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
            for (int i = 0; i < heights.Length; i++)
            {
                float height = heights[i];
                Vector3 pos = new Vector3(0f, 0f, i * 2.5f);
                
                GameObject ladder = CreateLadder(section, $"Ladder_{height:F1}m", pos, height);
                CreateHeightLabel(ladder, $"{height:F1}m", new Vector3(0f, height + 0.5f, 0f));
            }
        }
        
        private static void CreateRampSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Ramps");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "RAMPS", new Vector3(0f, 3f, -1f));
            
            // Ramps with different angles
            float[] angles = { 15f, 30f, 45f };
            string[] labels = { "15°", "30°", "45°" };
            
            for (int i = 0; i < angles.Length; i++)
            {
                float angle = angles[i];
                Vector3 pos = new Vector3(0f, 0f, i * 3.0f);
                
                GameObject ramp = CreateRamp(section, $"Ramp_{angle}deg", pos, 3f, angle);
                CreateHeightLabel(ramp, labels[i], new Vector3(0f, 1.5f, 0f));
            }
        }
        
        private static void CreateClimbingWallSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Climbing_Walls");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "FREE CLIMB TEST SHAPES", new Vector3(0f, 8f, -1f));
            
            // Giant shapes spread out for easy testing
            // Each shape is 8m apart to prevent overlap
            
            // 1. Giant Sphere (5m diameter) - tests convex curved surfaces
            var sphere = CreateClimbableSphere(section, "Giant_Sphere_5m", new Vector3(-12f, 2.5f, 0f), 5f);
            CreateHeightLabel(sphere, "SPHERE 5m\n(Convex)", new Vector3(0f, 3f, 0f));
            
            // 2. Giant Cylinder (6m tall, 2m radius) - tests vertical curved surfaces
            var cylinder = CreateClimbableCylinder(section, "Giant_Cylinder_6m", new Vector3(0f, 0f, 0f), 6f, 2f);
            CreateHeightLabel(cylinder, "CYLINDER 6m\n(Vertical Curve)", new Vector3(0f, 7f, 0f));
            
            // 3. Giant Cube (5m) - tests flat surfaces at different angles
            var cube = CreateBox(section, "Giant_Cube_5m", new Vector3(12f, 2.5f, 0f), new Vector3(5f, 5f, 5f), new Color(0.4f, 0.7f, 0.4f));
            AddClimbableTag(cube);
            CreateHeightLabel(cube, "CUBE 5m\n(Flat Faces)", new Vector3(0f, 3f, 0f));
            
            // 4. Concave Wall (inside corner) - tests concave navigation
            var concave = CreateCurvedClimbWall(section, "Giant_Concave_6m", new Vector3(-12f, 0f, 12f), 6f, 5f, true);
            CreateHeightLabel(concave, "CONCAVE 6m\n(Inside Corner)", new Vector3(0f, 7f, 0f));
            
            // 5. Convex Wall (outside corner) - tests convex edge handling
            var convex = CreateCurvedClimbWall(section, "Giant_Convex_6m", new Vector3(0f, 0f, 12f), 6f, 5f, false);
            CreateHeightLabel(convex, "CONVEX 6m\n(Outside Corner)", new Vector3(0f, 7f, 0f));
            
            // 6. Overhang (110 degrees) - tests negative angle climbing
            var overhang = CreateAngledClimbWall(section, "Giant_Overhang_5m", new Vector3(12f, 0f, 12f), 5f, 4f, 110f);
            CreateHeightLabel(overhang, "OVERHANG 5m\n(110°)", new Vector3(0f, 6f, 0f));
        }
        
        private static void AddClimbableTag(GameObject obj)
        {
            // Add Climbable layer/tag for physics detection
            // The actual authoring component will be added by the climbing system
            obj.layer = LayerMask.NameToLayer("Default"); // Will be detected by physics raycast
        }
        
        private static void CreateMixedObstacleSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Mixed_Obstacles");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "MIXED OBSTACLES", new Vector3(0f, 3f, -1f));
            
            // Stepped platforms
            for (int i = 0; i < 5; i++)
            {
                float height = (i + 1) * 0.5f;
                Vector3 pos = new Vector3(0f, height / 2f, i * 1.2f);
                
                GameObject step = CreateBox(section, $"Step_{i + 1}", pos,
                                           new Vector3(1.2f, height, 1.0f),
                                           new Color(0.6f, 0.4f, 0.8f));
                
                CreateHeightLabel(step, $"{height:F2}m", new Vector3(0f, height / 2f + 0.3f, 0f));
            }
        }
        
        private static void CreateAdvancedClimbablesSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Advanced_Climbables");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "ADVANCED CLIMBABLES", new Vector3(0f, 8f, -1f));
            
            // Row 1: Spheres and Cylinders (10m spacing)
            CreateClimbableSphere(section, "Climb_Sphere_2m", new Vector3(-15f, 2f, 0f), 2f);
            CreateClimbableSphere(section, "Climb_Sphere_3m", new Vector3(-5f, 3f, 0f), 3f);
            CreateClimbableCylinder(section, "Climb_Cylinder_4m", new Vector3(5f, 0f, 0f), 4f, 1f);
            CreateClimbablePipe(section, "Climb_Pipe_5m", new Vector3(15f, 3f, 0f), 5f, 0.6f);
            
            // Row 2: Angled and Overhang Walls (10m spacing, 12m back)
            CreateAngledClimbWall(section, "Climb_Wall_75deg", new Vector3(-15f, 0f, 12f), 5f, 3f, 75f);
            CreateAngledClimbWall(section, "Climb_Wall_60deg", new Vector3(-5f, 0f, 12f), 5f, 3f, 60f);
            CreateAngledClimbWall(section, "Climb_Overhang_100deg", new Vector3(5f, 0f, 12f), 4f, 3f, 100f);
            CreateAngledClimbWall(section, "Climb_Overhang_110deg", new Vector3(15f, 0f, 12f), 4f, 3f, 110f);
            
            // Row 3: Curved Walls (10m spacing, 24m back)
            CreateCurvedClimbWall(section, "Climb_Wall_Concave", new Vector3(-10f, 0f, 24f), 5f, 4f, true);
            CreateCurvedClimbWall(section, "Climb_Wall_Convex", new Vector3(10f, 0f, 24f), 5f, 4f, false);
            
            // Row 4: Composite Structures (10m spacing, 36m back)
            CreateClimbableArch(section, "Climb_Arch", new Vector3(-10f, 0f, 36f), 5f, 4f);
            CreateClimbableColumn(section, "Climb_Column", new Vector3(0f, 0f, 36f), 6f, 1.2f);
            CreateClimbableTower(section, "Climb_Tower", new Vector3(10f, 0f, 36f), 8f, 3f);
        }

        private static void CreateSwimmingSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Swimming");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;

            CreateSectionLabel(section, "SWIMMING", new Vector3(0f, 4f, -1f));

            var pool = CreatePool("Swimming_Pool");
            pool.transform.SetParent(section.transform);
            pool.transform.localPosition = Vector3.zero;
        }

        private static void CreateHazardSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Hazards");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;

            CreateSectionLabel(section, "HAZARDS", new Vector3(0f, 4f, -1f));

            var rad = CreateHazardChamber("Radiation_Chamber", EnvironmentZoneType.Radioactive, new Color(0.4f, 0.8f, 0.2f, 0.3f));
            rad.transform.SetParent(section.transform);
            rad.transform.localPosition = Vector3.zero;
        }

        private static void CreateHorrorSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Horror");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;

            CreateSectionLabel(section, "HORROR", new Vector3(0f, 4f, -1f));

            var horror = CreateDarkCorridor("Horror_Corridor");
            horror.transform.SetParent(section.transform);
            horror.transform.localPosition = Vector3.zero;
        }

        private static void CreatePushableSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Pushables");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "PUSHABLES", new Vector3(0f, 4f, -1f));
            
            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = section.transform;
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
            SetupGridMaterial(floor, new Color(0.6f, 0.6f, 0.65f));

            // Crates
            CreatePushableCrate(section, "Crate_50kg", new Vector3(-2, 0.5f, 0), 50f, new Color(0.5f, 1f, 0.5f));
            CreatePushableCrate(section, "Crate_100kg", new Vector3(0, 0.75f, 0), 100f, new Color(1f, 1f, 0f), 1.5f);
            CreatePushableCrate(section, "Crate_200kg", new Vector3(2, 1.0f, 0), 200f, new Color(1f, 0.5f, 0.5f), 2.0f);
        }

        private static void CreateRagdollTest(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Ragdoll");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;

            CreateSectionLabel(section, "RAGDOLL TEST", new Vector3(0f, 4f, -1f));

            // Kill Zone
            var killZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            killZone.name = "KillZone";
            killZone.transform.parent = section.transform;
            killZone.transform.localPosition = new Vector3(0, 0.5f, 0);
            killZone.transform.localScale = new Vector3(4f, 1f, 4f);
            killZone.GetComponent<UnityEngine.Collider>().isTrigger = true;
            
            // Material (Red transparent) - Use URP Lit for SRP Batcher compatibility
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            
            var imgMat = new UnityEngine.Material(shader);
            var color = new Color(1f, 0f, 0f, 0.3f);
            
            // Set common colors
            if (imgMat.HasProperty("_BaseColor")) imgMat.SetColor("_BaseColor", color);
            if (imgMat.HasProperty("_Color")) imgMat.SetColor("_Color", color);
            
            if (shader.name.Contains("Universal Render Pipeline"))
            {
                // URP Transparent Setup
                imgMat.SetFloat("_Surface", 1); // Transparent
                imgMat.SetFloat("_Blend", 0);   // Alpha
                imgMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                imgMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                imgMat.SetInt("_ZWrite", 0);
                imgMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                imgMat.renderQueue = 3000;
            }
            else
            {
                // Standard Shader Fallback
                imgMat.SetFloat("_Mode", 3);
                imgMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                imgMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                imgMat.SetInt("_ZWrite", 0);
                imgMat.DisableKeyword("_ALPHATEST_ON");
                imgMat.DisableKeyword("_ALPHABLEND_ON");
                imgMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                imgMat.renderQueue = 3000;
            }
            
            killZone.GetComponent<Renderer>().sharedMaterial = imgMat;

            // KillZone Component
            var kzAuth = killZone.AddComponent<DIG.Survival.Hazards.Authoring.KillZoneAuthoring>();
            kzAuth.DamagePerSecond = 500f;

            // Label
            CreateHeightLabel(killZone, "KILL ZONE", new Vector3(0, 1.5f, 0));

            // Ragdoll Dummy (Suspended)
            CreateRagdollDummy(section, new Vector3(0, 3f, 0));
        }

        private static void CreateRagdollDummy(GameObject parent, Vector3 localPos)
        {
            GameObject root = new GameObject("RagdollDummy");
            root.transform.parent = parent.transform;
            root.transform.localPosition = localPos;

            // Authoring for Entity/Health
            root.AddComponent<global::Player.Authoring.RagdollTestAuthoring>();
            var ragAuth = root.AddComponent<DIG.Survival.Physics.Authoring.RagdollAuthoring>();
            
            // Ghost
            var ghostAuth = root.AddComponent<GhostAuthoringComponent>();
            ghostAuth.DefaultGhostMode = GhostMode.Predicted;
            ghostAuth.SupportedGhostModes = (GhostModeMask)(GhostMode.Predicted | GhostMode.Interpolated);

            // Create Limbs
            // Pelvis
            GameObject pelvis = CreateLimb("Pelvis", root, Vector3.zero, new Vector3(0.5f, 0.5f, 0.3f));
            ragAuth.Pelvis = pelvis;
            
            // Spine
            GameObject spine = CreateLimb("Spine", root, new Vector3(0, 0.6f, 0), new Vector3(0.4f, 0.6f, 0.3f));
            ConnectLimbs(pelvis, spine);

            // Head
            GameObject head = CreateLimb("Head", root, new Vector3(0, 1.1f, 0), new Vector3(0.3f, 0.3f, 0.3f));
            ConnectLimbs(spine, head);
            
            // Arms
            GameObject leftArm = CreateLimb("LeftArm", root, new Vector3(-0.6f, 0.6f, 0), new Vector3(0.2f, 0.7f, 0.2f));
            ConnectLimbs(spine, leftArm);
            
            GameObject rightArm = CreateLimb("RightArm", root, new Vector3(0.6f, 0.6f, 0), new Vector3(0.2f, 0.7f, 0.2f));
            ConnectLimbs(spine, rightArm);
            
            // Legs
            GameObject leftLeg = CreateLimb("LeftLeg", root, new Vector3(-0.3f, -0.6f, 0), new Vector3(0.25f, 0.7f, 0.25f));
            ConnectLimbs(pelvis, leftLeg);
            
            GameObject rightLeg = CreateLimb("RightLeg", root, new Vector3(0.3f, -0.6f, 0), new Vector3(0.25f, 0.7f, 0.25f));
            ConnectLimbs(pelvis, rightLeg);
        }

        private static GameObject CreateLimb(string name, GameObject parent, Vector3 pos, Vector3 size)
        {
            GameObject limb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            limb.name = name;
            // Parent to ROOT logic for Ragdoll? 
            // In Unity Ragdolls, bones are heirarchy.
            // But we instantiate them relative to root for now.
            // If I parent them to `parent` (root), then `pos` is relative to root.
            limb.transform.parent = parent.transform;
            limb.transform.localPosition = pos;
            limb.transform.localScale = size;
            
            // Physics
            var rb = limb.AddComponent<Rigidbody>();
            rb.isKinematic = true; // Start Kinematic
            
            return limb;
        }

        private static void ConnectLimbs(GameObject parentLimb, GameObject childLimb)
        {
            // CharacterJoint is better for Ragdoll
            var joint = childLimb.AddComponent<CharacterJoint>();
            joint.connectedBody = parentLimb.GetComponent<Rigidbody>();
            
            // Setup default limits
            joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
            joint.highTwistLimit = new SoftJointLimit { limit = 20f };
            joint.swing1Limit = new SoftJointLimit { limit = 20f };
            joint.swing2Limit = new SoftJointLimit { limit = 20f };
        }

        [MenuItem(MenuRoot + "Pushables/Crate - 50kg", false, 700)]
        private static void CreateCrate50kg()
        {
            Vector3 pos = GetSceneViewPosition();
            var crate = CreatePushableCrate(null, "Crate_50kg", pos + Vector3.up * 0.5f, 50f, Color.green);
            Selection.activeGameObject = crate;
            Undo.RegisterCreatedObjectUndo(crate, "Create Pushable Crate");
        }
        
        [MenuItem(MenuRoot + "Pushables/Crate - 100kg", false, 701)]
        private static void CreateCrate100kg()
        {
             Vector3 pos = GetSceneViewPosition();
             var crate = CreatePushableCrate(null, "Crate_100kg", pos + Vector3.up * 0.75f, 100f, Color.yellow, 1.5f);
             Selection.activeGameObject = crate;
             Undo.RegisterCreatedObjectUndo(crate, "Create Pushable Crate");
        }

        private static GameObject CreatePushableCrate(GameObject parent, string name, Vector3 pos, float mass, Color color, float size = 1.0f)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = name;
            
            if (parent != null)
            {
                crate.transform.parent = parent.transform;
                crate.transform.localPosition = pos;
            }
            else
            {
                crate.transform.position = pos;
            }
            
            crate.transform.localScale = Vector3.one * size;
            
            // Visuals
            SetupMaterial(crate, color);
            
            // Physics
            var rb = crate.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            
            // Pushable Component
            var pushAuth = crate.AddComponent<PushableObjectAuthoring>();
            pushAuth.Mass = mass;
            pushAuth.Friction = 0.6f;
            
            // Unity Physics Authoring (Rely on Rigidbody/Collider Baking)
            // var bodyAuth = crate.AddComponent<PhysicsBodyAuthoring>();
            // bodyAuth.MotionType = BodyMotionType.Dynamic;
            
            // var shapeAuth = crate.AddComponent<PhysicsShapeAuthoring>();
            // shapeAuth.ShapeType = ShapeType.Box;
            // shapeAuth.CollisionFilter = CollisionFilter.Default;

            // NetCode Ghost
            var ghostAuth = crate.AddComponent<GhostAuthoringComponent>();
            ghostAuth.DefaultGhostMode = GhostMode.Predicted;
            ghostAuth.SupportedGhostModes = (GhostModeMask)(GhostMode.Predicted | GhostMode.Interpolated);

            // Label
            CreateHeightLabel(crate, $"{mass}kg", new Vector3(0, size/2f + 0.5f, 0));

            return crate;
        }

        private static GameObject CreatePool(string name)
        {
            var root = new GameObject(name);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = root.transform;
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
            SetupGridMaterial(floor, new Color(0.8f, 0.8f, 0.9f));

            var water = new GameObject("WaterVolume");
            water.transform.SetParent(root.transform);
            water.transform.localPosition = new Vector3(0, 2f, 0);

            var zoneAuth = water.AddComponent<EnvironmentZoneAuthoring>();
            zoneAuth.ZoneType = EnvironmentZoneType.Underwater;
            zoneAuth.OxygenRequired = true;
            zoneAuth.OxygenDepletionMultiplier = 1f;
            zoneAuth.Shape = ZoneShapeType.Box;
            zoneAuth.BoxSize = new Vector3(10f, 4f, 10f);
            zoneAuth.Center = Vector3.zero;

            var waterAuth = water.AddComponent<WaterZoneAuthoring>();
            waterAuth.Density = 1000f;
            waterAuth.Viscosity = 1.0f; // Medium-High viscosity
            waterAuth.BuoyancyModifier = 0.5f; // Stronger buoyancy
            waterAuth.AutoCalculateSurface = true;

            var waterVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject.DestroyImmediate(waterVis.GetComponent<UnityEngine.Collider>());
            waterVis.name = "Visual";
            waterVis.transform.parent = water.transform;
            waterVis.transform.localPosition = Vector3.zero;
            waterVis.transform.localScale = new Vector3(10f, 4f, 10f);
            
            var renderer = waterVis.GetComponent<Renderer>();
            if (renderer)
            {
                // Use URP Lit for SRP Batcher compatibility
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                var mat = new UnityEngine.Material(shader);
                var color = new Color(0f, 0.5f, 1f, 0.3f);
                
                // Set common colors
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

                if (shader.name.Contains("Universal Render Pipeline"))
                {
                    // URP Transparent Setup
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0);   // Alpha
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = 3000;
                }
                else
                {
                    // Standard Shader Fallback
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
                
                renderer.sharedMaterial = mat;
            }
            if (waterVis.GetComponent<UnityEngine.Collider>()) 
                Object.DestroyImmediate(waterVis.GetComponent<UnityEngine.Collider>());

            return root;
        }

        private static GameObject CreateHazardChamber(string name, EnvironmentZoneType type, Color color)
        {
            var root = new GameObject(name);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = root.transform;
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(6f, 0.2f, 6f);
            SetupGridMaterial(floor, color);

            var zone = new GameObject("HazardZone");
            zone.transform.SetParent(root.transform);
            zone.transform.localPosition = new Vector3(0, 1.5f, 0);

            var zoneAuth = zone.AddComponent<EnvironmentZoneAuthoring>();
            zoneAuth.ZoneType = type;
            zoneAuth.OxygenRequired = false;
            zoneAuth.RadiationRate = type == EnvironmentZoneType.Radioactive ? 10f : 0f;
            zoneAuth.OxygenDepletionMultiplier = type == EnvironmentZoneType.Toxic ? 2f : 0f;
            zoneAuth.Shape = ZoneShapeType.Box;
            zoneAuth.BoxSize = new Vector3(6f, 3f, 6f);
            zoneAuth.Center = Vector3.zero;

            return root;
        }

        private static GameObject CreateDarkCorridor(string name)
        {
            var root = new GameObject(name);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = root.transform;
            floor.transform.localPosition = new Vector3(0, -0.1f, 10f);
            floor.transform.localScale = new Vector3(3f, 0.2f, 20f);
            SetupGridMaterial(floor, new Color(0.1f, 0.1f, 0.1f));

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.parent = root.transform;
            ceiling.transform.localPosition = new Vector3(0, 3.1f, 10f);
            ceiling.transform.localScale = new Vector3(3f, 0.2f, 20f);
            SetupGridMaterial(ceiling, new Color(0.1f, 0.1f, 0.1f));

            var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "LeftWall";
            leftWall.transform.parent = root.transform;
            leftWall.transform.localPosition = new Vector3(-1.6f, 1.5f, 10f);
            leftWall.transform.localScale = new Vector3(0.2f, 3f, 20f);
            SetupGridMaterial(leftWall, new Color(0.1f, 0.1f, 0.1f));

            var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "RightWall";
            rightWall.transform.parent = root.transform;
            rightWall.transform.localPosition = new Vector3(1.6f, 1.5f, 10f);
            rightWall.transform.localScale = new Vector3(0.2f, 3f, 20f);
            SetupGridMaterial(rightWall, new Color(0.1f, 0.1f, 0.1f));

            return root;
        }
        
        private static void CreateMovingPlatformSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Moving_Platforms");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "MOVING PLATFORMS (Epic 13.1.1)", new Vector3(0f, 5f, -1f));
            
            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = section.transform;
            floor.transform.localPosition = new Vector3(0, -0.1f, 5f);
            floor.transform.localScale = new Vector3(20f, 0.2f, 15f);
            SetupGridMaterial(floor, new Color(0.5f, 0.5f, 0.55f));
            
            // Platform 1: Horizontal Moving Platform
            var platform1 = CreateMovingPlatform(section, "MovingPlatform_Horizontal", 
                new Vector3(-5f, 0.5f, 0f), new Vector3(3f, 0.3f, 3f), new Color(0.2f, 0.8f, 0.3f));
            var mover1 = platform1.AddComponent<DIG.Testing.TestPlatformMover>();
            mover1.Motion = DIG.Testing.TestPlatformMover.MoveType.Horizontal;
            mover1.MoveDistance = 4f;
            mover1.MoveSpeed = 0.3f;
            CreateHeightLabel(platform1, "MOVES LEFT/RIGHT", new Vector3(0f, 1f, 0f));
            
            // Platform 2: Vertical Moving Platform
            var platform2 = CreateMovingPlatform(section, "MovingPlatform_Vertical", 
                new Vector3(0f, 0.5f, 0f), new Vector3(3f, 0.3f, 3f), new Color(0.3f, 0.3f, 0.9f));
            var mover2 = platform2.AddComponent<DIG.Testing.TestPlatformMover>();
            mover2.Motion = DIG.Testing.TestPlatformMover.MoveType.Vertical;
            mover2.MoveDistance = 3f;
            mover2.MoveSpeed = 0.25f;
            CreateHeightLabel(platform2, "MOVES UP/DOWN", new Vector3(0f, 1f, 0f));
            
            // Platform 3: Rotating Platform
            var platform3 = CreateMovingPlatform(section, "MovingPlatform_Rotating", 
                new Vector3(5f, 0.5f, 0f), new Vector3(4f, 0.3f, 4f), new Color(0.9f, 0.4f, 0.2f));
            var mover3 = platform3.AddComponent<DIG.Testing.TestPlatformMover>();
            mover3.Motion = DIG.Testing.TestPlatformMover.MoveType.Rotating;
            mover3.RotationSpeed = 20f;
            CreateHeightLabel(platform3, "ROTATING", new Vector3(0f, 1f, 0f));
            
            // Platform 4: Combined Path
            var platform4 = CreateMovingPlatform(section, "MovingPlatform_Path", 
                new Vector3(0f, 2f, 6f), new Vector3(3f, 0.3f, 3f), new Color(0.8f, 0.8f, 0.2f));
            var mover4 = platform4.AddComponent<DIG.Testing.TestPlatformMover>();
            mover4.Motion = DIG.Testing.TestPlatformMover.MoveType.Path;
            mover4.MoveDistance = 3f;
            mover4.MoveSpeed = 0.2f;
            CreateHeightLabel(platform4, "FIGURE-8 PATH", new Vector3(0f, 1f, 0f));
        }
        
        private static GameObject CreateMovingPlatform(GameObject parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = name;
            
            if (parent != null)
            {
                platform.transform.parent = parent.transform;
                platform.transform.localPosition = pos;
            }
            else
            {
                platform.transform.position = pos;
            }
            
            platform.transform.localScale = size;
            SetupGridMaterial(platform, color);
            
            // Add MovingPlatformAuthoring
            platform.AddComponent<MovingPlatformAuthoring>();
            
            return platform;
        }

        private static void CreateFallTestSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Fall_Tests");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;

            CreateSectionLabel(section, "FALL TESTS (Epic 13.14)", new Vector3(0f, 12f, -1f));

            // 13.14.T1: Height Tower
            CreateFallHeightTower(section, new Vector3(-8f, 0f, 0f));

            // 13.14.T2: Landing Surface Pads
            CreateLandingSurfacePads(section, new Vector3(8f, 0f, 0f));

            // 13.14.T3: Velocity Threshold Test
            CreateVelocityThresholdTest(section, new Vector3(0f, 0f, 35f));

            // 13.14.T4: Teleport Mid-Fall Test
            CreateTeleportMidFallTest(section, new Vector3(0f, 0f, 55f));
        }

        // 13.14.T1: Fall Height Test Tower
        private static void CreateFallHeightTower(GameObject parent, Vector3 localPos)
        {
            GameObject tower = new GameObject("FallHeightTower");
            tower.transform.parent = parent.transform;
            tower.transform.localPosition = localPos;

            CreateHeightLabel(tower, "HEIGHT TOWER\n(Step off to test fall)", new Vector3(0f, 22f, 10f));

            // Ground platform
            var ground = CreateBox(tower, "Platform_Ground", new Vector3(0f, 0.15f, 0f),
                new Vector3(5f, 0.3f, 5f), new Color(0.3f, 0.3f, 0.3f));
            CreateHeightLabel(ground, "GROUND", new Vector3(0f, 0.5f, 0f));

            // Platforms at various heights
            float[] heights = { 0.5f, 1f, 2f, 3f, 5f, 7f, 10f, 15f, 20f };
            Color[] colors = {
                new Color(0.2f, 0.8f, 0.2f), // Green - safe
                new Color(0.4f, 0.9f, 0.2f),
                new Color(0.6f, 0.9f, 0.2f),
                new Color(0.9f, 0.9f, 0.2f), // Yellow - caution
                new Color(0.9f, 0.7f, 0.2f),
                new Color(0.9f, 0.5f, 0.2f), // Orange - danger
                new Color(0.9f, 0.3f, 0.2f),
                new Color(0.9f, 0.1f, 0.1f), // Red - lethal
                new Color(0.6f, 0.1f, 0.1f)
            };

            float zSpacing = 3.5f;
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                Vector3 pos = new Vector3(0f, h + 0.15f, (i + 1) * zSpacing);

                var platform = CreateBox(tower, $"Platform_{h:F1}m", pos,
                    new Vector3(3f, 0.3f, 2.5f), colors[i]);
                CreateHeightLabel(platform, $"{h:F1}m", new Vector3(0f, 0.5f, 0f));

                // Add telepad to get up to higher platforms (>3m)
                if (h > 3f)
                {
                    CreateTelepad(tower, $"TelePad_To_{h:F0}m",
                        new Vector3(-3f, 0.1f, (i + 1) * zSpacing),
                        pos + new Vector3(0f, 0.2f, 0f),
                        new Color(0.2f, 0.5f, 1f));
                }
            }

            // Kill volume at the bottom (respawn trigger)
            var killZone = new GameObject("KillVolume");
            killZone.transform.parent = tower.transform;
            killZone.transform.localPosition = new Vector3(0f, -5f, 18f);
            var killCol = killZone.AddComponent<UnityEngine.BoxCollider>();
            killCol.size = new Vector3(15f, 2f, 40f);
            killCol.isTrigger = true;
        }

        // 13.14.T2: Landing Surface Test Pads
        private static void CreateLandingSurfacePads(GameObject parent, Vector3 localPos)
        {
            GameObject pads = new GameObject("LandingSurfacePads");
            pads.transform.parent = parent.transform;
            pads.transform.localPosition = localPos;

            CreateHeightLabel(pads, "LANDING SURFACES\n(Different materials)", new Vector3(0f, 8f, 0f));

            // Elevated platform to jump from
            var jumpPlatform = CreateBox(pads, "JumpPlatform", new Vector3(0f, 5f, -3f),
                new Vector3(14f, 0.3f, 3f), new Color(0.5f, 0.5f, 0.5f));
            CreateHeightLabel(jumpPlatform, "JUMP FROM HERE (5m)", new Vector3(0f, 1f, 0f));

            // Telepad to get up
            CreateTelepad(pads, "TelePad_ToJump",
                new Vector3(7f, 0.1f, -3f),
                new Vector3(0f, 5.3f, -3f),
                new Color(0.2f, 0.5f, 1f));

            // Different surface pads
            string[] surfaceNames = { "Dirt", "Metal", "Wood", "Water", "Concrete" };
            Color[] surfaceColors = {
                new Color(0.6f, 0.4f, 0.2f), // Dirt - brown
                new Color(0.7f, 0.7f, 0.8f), // Metal - silver
                new Color(0.5f, 0.35f, 0.2f), // Wood - tan
                new Color(0.2f, 0.4f, 0.8f), // Water - blue
                new Color(0.5f, 0.5f, 0.5f)  // Concrete - gray
            };

            for (int i = 0; i < surfaceNames.Length; i++)
            {
                float x = (i - 2) * 4f;
                var pad = CreateBox(pads, $"Pad_{surfaceNames[i]}", new Vector3(x, 0.1f, 3f),
                    new Vector3(3f, 0.2f, 3f), surfaceColors[i]);
                CreateHeightLabel(pad, surfaceNames[i].ToUpper(), new Vector3(0f, 0.5f, 0f));
            }
        }

        // 13.14.T3: Velocity Threshold Test
        private static void CreateVelocityThresholdTest(GameObject parent, Vector3 localPos)
        {
            GameObject test = new GameObject("VelocityThresholdTest");
            test.transform.parent = parent.transform;
            test.transform.localPosition = localPos;

            CreateHeightLabel(test, "VELOCITY TEST\n(Soft vs Hard landings)", new Vector3(0f, 12f, 0f));

            // Ground
            CreateBox(test, "Ground", new Vector3(0f, -0.1f, 0f),
                new Vector3(30f, 0.2f, 12f), new Color(0.4f, 0.4f, 0.4f));

            // Gentle slide ramp (slow landing - should NOT trigger VFX)
            var ramp = CreateRamp(test, "GentleSlide_15deg", new Vector3(-10f, 0f, 0f), 8f, 15f);
            CreateHeightLabel(ramp, "GENTLE SLIDE\n15° (No VFX)", new Vector3(0f, 3f, 0f));

            // Moderate fall (2m - might trigger light VFX)
            var platform2m = CreateBox(test, "Platform_2m", new Vector3(-4f, 2f, 0f),
                new Vector3(3f, 0.3f, 3f), new Color(0.5f, 0.8f, 0.5f));
            CreateHeightLabel(platform2m, "2m DROP\n(Light VFX)", new Vector3(0f, 1f, 0f));

            // Hard fall (5m - strong VFX)
            var platform5m = CreateBox(test, "Platform_5m", new Vector3(2f, 5f, 0f),
                new Vector3(3f, 0.3f, 3f), new Color(0.9f, 0.6f, 0.2f));
            CreateHeightLabel(platform5m, "5m DROP\n(Strong VFX)", new Vector3(0f, 1f, 0f));

            // Telepad to 5m platform
            CreateTelepad(test, "TelePad_To5m",
                new Vector3(5f, 0.1f, 0f),
                new Vector3(2f, 5.3f, 0f),
                new Color(0.2f, 0.5f, 1f));

            // Extreme fall (10m - heavy VFX + damage)
            var platform10m = CreateBox(test, "Platform_10m", new Vector3(9f, 10f, 0f),
                new Vector3(3f, 0.3f, 3f), new Color(0.9f, 0.2f, 0.2f));
            CreateHeightLabel(platform10m, "10m DROP\n(Heavy VFX + DMG)", new Vector3(0f, 1f, 0f));

            // Telepad to 10m platform
            CreateTelepad(test, "TelePad_To10m",
                new Vector3(12f, 0.1f, 0f),
                new Vector3(9f, 10.3f, 0f),
                new Color(0.2f, 0.5f, 1f));
        }

        // 13.14.T4: Teleport Mid-Fall Test
        private static void CreateTeleportMidFallTest(GameObject parent, Vector3 localPos)
        {
            GameObject test = new GameObject("TeleportMidFallTest");
            test.transform.parent = parent.transform;
            test.transform.localPosition = localPos;

            CreateHeightLabel(test, "TELEPORT MID-FALL\n(Tests 13.14.5)", new Vector3(0f, 18f, 0f));

            // Ground
            CreateBox(test, "Ground", new Vector3(0f, -0.1f, 0f),
                new Vector3(25f, 0.2f, 12f), new Color(0.4f, 0.4f, 0.4f));

            // High platform to fall from (15m)
            var startPlatform = CreateBox(test, "StartPlatform_15m", new Vector3(0f, 15f, 0f),
                new Vector3(4f, 0.3f, 4f), new Color(0.4f, 0.4f, 0.9f));
            CreateHeightLabel(startPlatform, "START (15m)\nStep off edge", new Vector3(0f, 1f, 0f));

            // Telepad to get to start
            CreateTelepad(test, "TelePad_ToStart",
                new Vector3(-6f, 0.1f, 0f),
                new Vector3(0f, 15.3f, 0f),
                new Color(0.2f, 0.5f, 1f));

            // Mid-air teleport trigger (at 8m height)
            var midAirTrigger = new GameObject("TeleportTrigger_MidAir");
            midAirTrigger.transform.parent = test.transform;
            midAirTrigger.transform.localPosition = new Vector3(0f, 8f, 0f);
            var triggerCol = midAirTrigger.AddComponent<UnityEngine.BoxCollider>();
            triggerCol.size = new Vector3(6f, 4f, 6f);
            triggerCol.isTrigger = true;

            // Visual for trigger zone
            var triggerVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(triggerVisual.GetComponent<UnityEngine.Collider>());
            triggerVisual.name = "TriggerVisual";
            triggerVisual.transform.parent = midAirTrigger.transform;
            triggerVisual.transform.localPosition = Vector3.zero;
            triggerVisual.transform.localScale = new Vector3(6f, 4f, 6f);
            SetupTransparentMaterial(triggerVisual, new Color(0.8f, 0.2f, 0.8f, 0.2f));
            CreateHeightLabel(midAirTrigger, "TELEPORT ZONE\n(8m height)", new Vector3(0f, 3f, 0f));

            // Destination A: Ground level
            var destGroundMarker = CreateBox(test, "Destination_Ground", new Vector3(-8f, 0.15f, 0f),
                new Vector3(3f, 0.3f, 3f), new Color(0.2f, 0.9f, 0.2f));
            CreateHeightLabel(destGroundMarker, "DEST A\n(Ground)", new Vector3(0f, 1f, 0f));

            // Destination B: Another high platform (10m)
            var destHighPlatform = CreateBox(test, "Destination_10m", new Vector3(8f, 10f, 0f),
                new Vector3(3f, 0.3f, 3f), new Color(0.9f, 0.9f, 0.2f));
            CreateHeightLabel(destHighPlatform, "DEST B\n(10m - still in air)", new Vector3(0f, 1f, 0f));
        }

        // Helper: Create a telepad (visual marker + teleport trigger)
        private static GameObject CreateTelepad(GameObject parent, string name, Vector3 localPos, Vector3 destination, Color color)
        {
            GameObject telepad = new GameObject(name);
            telepad.transform.parent = parent.transform;
            telepad.transform.localPosition = localPos;

            // Base pad visual
            var padVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padVisual.name = "PadVisual";
            padVisual.transform.parent = telepad.transform;
            padVisual.transform.localPosition = Vector3.zero;
            padVisual.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);
            SetupGridMaterial(padVisual, color);

            // Ring visual
            var ringVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(ringVisual.GetComponent<UnityEngine.Collider>());
            ringVisual.name = "RingVisual";
            ringVisual.transform.parent = telepad.transform;
            ringVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ringVisual.transform.localScale = new Vector3(1.8f, 0.02f, 1.8f);
            SetupGridMaterial(ringVisual, Color.white);

            // Trigger collider - DOTS physics will bake this as a trigger
            var trigger = telepad.AddComponent<UnityEngine.BoxCollider>();
            trigger.size = new Vector3(1.5f, 2f, 1.5f);
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.isTrigger = true;

            // Destination marker
            var destMarker = new GameObject("DestinationPoint");
            destMarker.transform.parent = telepad.transform;
            destMarker.transform.position = parent.transform.TransformPoint(destination);

            // Add TeleportPadAuthoring
            var teleportAuth = telepad.AddComponent<TeleportPadAuthoring>();
            teleportAuth.Destination = destMarker.transform;
            teleportAuth.Cooldown = 0.5f;

            // Label
            CreateHeightLabel(telepad, "TELEPAD", new Vector3(0f, 0.5f, 0f));

            return telepad;
        }

        private static void SetupTransparentMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new UnityEngine.Material(shader);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            if (shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            else
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
            }

            renderer.sharedMaterial = mat;
        }

        private static void CreateExternalForceSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_External_Forces");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;

            CreateSectionLabel(section, "EXTERNAL FORCES (Epic 13.1.3)", new Vector3(0f, 5f, -1f));
            
            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = section.transform;
            floor.transform.localPosition = new Vector3(0, -0.1f, 5f);
            floor.transform.localScale = new Vector3(20f, 0.2f, 15f);
            SetupGridMaterial(floor, new Color(0.5f, 0.55f, 0.5f));
            
            // Zone 1: Wind Tunnel (Directional Continuous)
            CreateExternalForceZone(section, "WindTunnel_Forward", 
                new Vector3(-6f, 1.5f, 3f), new Vector3(4f, 3f, 8f), 
                new Vector3(0f, 0f, 10f), true, true, new Color(0.3f, 0.7f, 1f, 0.3f));
            CreateHeightLabel(section, "WIND TUNNEL\n(Continuous)", new Vector3(-6f, 4f, 3f));
            
            // Zone 2: Launch Pad (Directional Impulse)
            CreateExternalForceZone(section, "LaunchPad_Up", 
                new Vector3(0f, 0.25f, 3f), new Vector3(3f, 0.5f, 3f), 
                new Vector3(0f, 15f, 0f), true, false, new Color(1f, 0.5f, 0.2f, 0.5f));
            CreateHeightLabel(section, "LAUNCH PAD\n(Impulse)", new Vector3(0f, 2f, 3f));
            
            // Zone 3: Radial Push (Explosion-like)
            CreateExternalForceZone(section, "RadialPush", 
                new Vector3(6f, 1.5f, 3f), new Vector3(5f, 3f, 5f), 
                new Vector3(8f, 0f, 0f), false, true, new Color(1f, 0.2f, 0.2f, 0.3f));
            CreateHeightLabel(section, "RADIAL PUSH\n(From Center)", new Vector3(6f, 4f, 3f));
            
            // Zone 4: Conveyor Belt (Horizontal Push)
            CreateExternalForceZone(section, "ConveyorBelt", 
                new Vector3(0f, 0.25f, 9f), new Vector3(12f, 0.5f, 2f), 
                new Vector3(5f, 0f, 0f), true, true, new Color(0.6f, 0.6f, 0.2f, 0.5f));
            CreateHeightLabel(section, "CONVEYOR BELT", new Vector3(0f, 1.5f, 9f));
        }
        
        private static void CreateExternalForceZone(GameObject parent, string name, Vector3 pos, Vector3 size, 
            Vector3 force, bool isDirectional, bool isContinuous, Color color)
        {
            var zone = new GameObject(name);
            
            if (parent != null)
            {
                zone.transform.parent = parent.transform;
                zone.transform.localPosition = pos;
            }
            else
            {
                zone.transform.position = pos;
            }
            
            // Trigger collider
            var collider = zone.AddComponent<UnityEngine.BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            
            // Visual
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(visual.GetComponent<UnityEngine.Collider>());
            visual.name = "Visual";
            visual.transform.parent = zone.transform;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = size;
            
            // Transparent material
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new UnityEngine.Material(shader);
            
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            
            if (shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            else
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
            
            visual.GetComponent<Renderer>().sharedMaterial = mat;
            
            // Add ExternalForceZoneAuthoring
            var authoring = zone.AddComponent<ExternalForceZoneAuthoring>();
            authoring.Force = force;
            authoring.IsDirectional = isDirectional;
            authoring.Mode = isContinuous 
                ? ExternalForceZoneAuthoring.ForceMode.Continuous 
                : ExternalForceZoneAuthoring.ForceMode.Impulse;
            authoring.ExitDamping = 5f;
        }
        
        private static void CreateGapCrossingSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Gap_Crossing");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;
            
            CreateSectionLabel(section, "GAP CROSSING (Epic 13.20)", new Vector3(0f, 6f, -1f));
            
            // Requirement: "With climbable objects next to each other, we should able to chain climb."
            
            // 1. Horizontal Gaps (Parallel Walls)
            // Testing gaps of 0.5m, 1.0m, 1.5m, 2.0m
            float[] gaps = { 0.5f, 1.0f, 1.5f, 2.0f };
            float xOffset = -15f;
            
            for (int i = 0; i < gaps.Length; i++)
            {
                float gap = gaps[i];
                Vector3 pos = new Vector3(xOffset + (i * 8f), 0f, 0f); // Spread out by 8m
                
                // Left Wall
                var leftWall = CreateBox(section, $"Gap_{gap:F1}m_Left", pos + new Vector3(-gap/2f - 0.25f, 2.5f, 0f), 
                                        new Vector3(0.5f, 5f, 3f), new Color(0.3f, 0.6f, 0.9f));
                
                // Right Wall
                var rightWall = CreateBox(section, $"Gap_{gap:F1}m_Right", pos + new Vector3(gap/2f + 0.25f, 2.5f, 0f), 
                                         new Vector3(0.5f, 5f, 3f), new Color(0.3f, 0.6f, 0.9f));
                
                CreateHeightLabel(leftWall, $"GAP {gap:F1}m", new Vector3(gap/2f + 0.25f, 1f, -1.6f));
            }
            
            // 2. Vertical Gaps (Reach Up)
            // Testing vertical reach/jump
            xOffset = -15f;
            float zOffset = 10f;
            
            // Wall with a hole in the middle
            var reachWall = CreateBox(section, "Vertical_Gap_Reach", new Vector3(xOffset, 1f, zOffset), 
                                     new Vector3(3f, 2f, 0.5f), new Color(0.8f, 0.4f, 0.2f));
            
            var upperReachWall = CreateBox(section, "Vertical_Gap_Upper", new Vector3(xOffset, 4.5f, zOffset), 
                                          new Vector3(3f, 2f, 0.5f), new Color(0.8f, 0.4f, 0.2f));
                                          
            CreateHeightLabel(reachWall, "VERTICAL GAP\n(1.5m)", new Vector3(0f, 2f, -0.6f));

            // 3. Corner Transitions (Inner/Outer)
            // L-Shape for Inner Corner
            var innerCornerPos = new Vector3(0f, 0f, 10f);
            var innerSide1 = CreateBox(section, "Inner_Wall_1", innerCornerPos + new Vector3(-1.5f, 2.5f, 0f), 
                                      new Vector3(3f, 5f, 0.5f), new Color(0.5f, 0.8f, 0.5f));
            var innerSide2 = CreateBox(section, "Inner_Wall_2", innerCornerPos + new Vector3(-2.75f, 2.5f, 1.25f), 
                                      new Vector3(0.5f, 5f, 3f), new Color(0.5f, 0.8f, 0.5f));
            CreateHeightLabel(innerSide1, "INNER CORNER", new Vector3(0f, 1f, -0.6f));

            // Column for Outer Corner
            var outerCornerPos = new Vector3(8f, 0f, 10f);
            var column = CreateBox(section, "Outer_Corner_Column", outerCornerPos, 
                                  new Vector3(2f, 5f, 2f), new Color(0.9f, 0.7f, 0.4f));
            CreateHeightLabel(column, "OUTER CORNER", new Vector3(0f, 2f, -1.1f));
        }
        
        #endregion
        
        #region Individual Objects Menu
        
        [MenuItem(MenuRoot + "Obstacles/Box - 0.5m Height", false, 100)]
        private static void CreateBox_05m() => CreateSingleBox("Box_0.5m", 0.5f);
        
        [MenuItem(MenuRoot + "Obstacles/Box - 1.0m Height", false, 101)]
        private static void CreateBox_10m() => CreateSingleBox("Box_1.0m", 1.0f);
        
        [MenuItem(MenuRoot + "Obstacles/Box - 1.5m Height", false, 102)]
        private static void CreateBox_15m() => CreateSingleBox("Box_1.5m", 1.5f);
        
        [MenuItem(MenuRoot + "Obstacles/Box - 2.0m Height", false, 103)]
        private static void CreateBox_20m() => CreateSingleBox("Box_2.0m", 2.0f);
        
        [MenuItem(MenuRoot + "Ladders/Ladder - 2m", false, 200)]
        private static void CreateLadder_2m() => CreateSingleLadder("Ladder_2m", 2.0f);
        
        [MenuItem(MenuRoot + "Ladders/Ladder - 3m", false, 201)]
        private static void CreateLadder_3m() => CreateSingleLadder("Ladder_3m", 3.0f);
        
        [MenuItem(MenuRoot + "Ladders/Ladder - 4m", false, 202)]
        private static void CreateLadder_4m() => CreateSingleLadder("Ladder_4m", 4.0f);
        
        [MenuItem(MenuRoot + "Ladders/Ladder - 5m", false, 203)]
        private static void CreateLadder_5m() => CreateSingleLadder("Ladder_5m", 5.0f);
        
        [MenuItem(MenuRoot + "Ladders/Ladder - 6m", false, 204)]
        private static void CreateLadder_6m() => CreateSingleLadder("Ladder_6m", 6.0f);
        
        [MenuItem(MenuRoot + "Ramps/Ramp - 15° Angle", false, 300)]
        private static void CreateRamp_15deg() => CreateSingleRamp("Ramp_15deg", 3f, 15f);
        
        [MenuItem(MenuRoot + "Ramps/Ramp - 30° Angle", false, 301)]
        private static void CreateRamp_30deg() => CreateSingleRamp("Ramp_30deg", 3f, 30f);
        
        [MenuItem(MenuRoot + "Ramps/Ramp - 45° Angle", false, 302)]
        private static void CreateRamp_45deg() => CreateSingleRamp("Ramp_45deg", 3f, 45f);
        
        [MenuItem(MenuRoot + "Walls/Climbing Wall - 3m", false, 400)]
        private static void CreateClimbWall_3m() => CreateSingleClimbingWall("Climbing_Wall_3m", 3f, 3f);
        
        [MenuItem(MenuRoot + "Walls/Climbing Wall - 4m", false, 401)]
        private static void CreateClimbWall_4m() => CreateSingleClimbingWall("Climbing_Wall_4m", 4f, 3f);
        
        [MenuItem(MenuRoot + "Walls/Climbing Wall - 5m", false, 402)]
        private static void CreateClimbWall_5m() => CreateSingleClimbingWall("Climbing_Wall_5m", 5f, 3f);
        
        // Advanced Climbable Surfaces - Spheres & Cylinders
        [MenuItem(MenuRoot + "Advanced Climbables/Sphere - 2m Radius", false, 450)]
        private static void CreateClimbSphere_2m() => CreateSingleClimbableSphere("Climb_Sphere_2m", 2f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Sphere - 3m Radius", false, 451)]
        private static void CreateClimbSphere_3m() => CreateSingleClimbableSphere("Climb_Sphere_3m", 3f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Sphere - 4m Radius", false, 452)]
        private static void CreateClimbSphere_4m() => CreateSingleClimbableSphere("Climb_Sphere_4m", 4f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Cylinder - 3m Height", false, 460)]
        private static void CreateClimbCylinder_3m() => CreateSingleClimbableCylinder("Climb_Cylinder_3m", 3f, 0.5f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Cylinder - 5m Height", false, 461)]
        private static void CreateClimbCylinder_5m() => CreateSingleClimbableCylinder("Climb_Cylinder_5m", 5f, 0.6f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Pipe Horizontal - 4m", false, 465)]
        private static void CreateClimbPipe_4m() => CreateSingleClimbablePipe("Climb_Pipe_4m", 4f, 0.4f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Pipe Horizontal - 6m", false, 466)]
        private static void CreateClimbPipe_6m() => CreateSingleClimbablePipe("Climb_Pipe_6m", 6f, 0.5f);
        
        // Advanced Climbable Walls - Angled & Curved
        [MenuItem(MenuRoot + "Advanced Climbables/Angled Wall - 75° (4m)", false, 470)]
        private static void CreateAngledWall_75() => CreateSingleAngledClimbWall("Climb_Wall_75deg", 4f, 3f, 75f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Angled Wall - 60° (4m)", false, 471)]
        private static void CreateAngledWall_60() => CreateSingleAngledClimbWall("Climb_Wall_60deg", 4f, 3f, 60f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Overhang Wall - 100° (3m)", false, 475)]
        private static void CreateOverhang_100() => CreateSingleAngledClimbWall("Climb_Overhang_100deg", 3f, 3f, 100f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Overhang Wall - 110° (3m)", false, 476)]
        private static void CreateOverhang_110() => CreateSingleAngledClimbWall("Climb_Overhang_110deg", 3f, 3f, 110f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Curved Wall Concave - 4m", false, 480)]
        private static void CreateCurvedWall_Concave() => CreateSingleCurvedClimbWall("Climb_Wall_Concave", 4f, 3f, true);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Curved Wall Convex - 4m", false, 481)]
        private static void CreateCurvedWall_Convex() => CreateSingleCurvedClimbWall("Climb_Wall_Convex", 4f, 3f, false);
        
        // Composite Structures
        [MenuItem(MenuRoot + "Advanced Climbables/Arch - 4m", false, 490)]
        private static void CreateClimbArch_4m() => CreateSingleClimbableArch("Climb_Arch_4m", 4f, 3f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Column - 5m", false, 491)]
        private static void CreateClimbColumn_5m() => CreateSingleClimbableColumn("Climb_Column_5m", 5f, 0.8f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Tower - 8m", false, 492)]
        private static void CreateClimbTower_8m() => CreateSingleClimbableTower("Climb_Tower_8m", 8f, 2f);
        
        [MenuItem(MenuRoot + "Advanced Climbables/Bridge - 6m", false, 493)]
        private static void CreateClimbBridge_6m() => CreateSingleClimbableBridge("Climb_Bridge_6m", 6f, 1.5f, 3f);
        
        [MenuItem(MenuRoot + "Ground/Ground Plane - Small (10x10)", false, 500)]
        private static void CreateGroundSmall() => CreateSingleGroundPlane("Ground_Small", new Vector3(10f, 1f, 10f));
        
        [MenuItem(MenuRoot + "Ground/Ground Plane - Medium (20x20)", false, 501)]
        private static void CreateGroundMedium() => CreateSingleGroundPlane("Ground_Medium", new Vector3(20f, 1f, 20f));
        
        [MenuItem(MenuRoot + "Ground/Ground Plane - Large (40x40)", false, 502)]
        private static void CreateGroundLarge() => CreateSingleGroundPlane("Ground_Large", new Vector3(40f, 1f, 40f));

        [MenuItem(MenuRoot + "Swimming Pool", false, 600)]
        private static void CreatePoolMenu()
        {
            var pool = CreatePool("SwimmingPool");
            Vector3 pos = GetSceneViewPosition();
            pool.transform.position = pos;
            Selection.activeGameObject = pool;
            Undo.RegisterCreatedObjectUndo(pool, "Create Swimming Pool");
        }

        [MenuItem(MenuRoot + "Radiation Chamber", false, 601)]
        private static void CreateRadiationMenu()
        {
            var rad = CreateHazardChamber("RadiationChamber", EnvironmentZoneType.Radioactive, new Color(0.4f, 0.8f, 0.2f, 0.3f));
            Vector3 pos = GetSceneViewPosition();
            rad.transform.position = pos;
            Selection.activeGameObject = rad;
            Undo.RegisterCreatedObjectUndo(rad, "Create Radiation Chamber");
        }

        [MenuItem(MenuRoot + "Horror Corridor", false, 602)]
        private static void CreateHorrorMenu()
        {
            var horror = CreateDarkCorridor("HorrorCorridor");
            Vector3 pos = GetSceneViewPosition();
            horror.transform.position = pos;
            Selection.activeGameObject = horror;
            Undo.RegisterCreatedObjectUndo(horror, "Create Horror Corridor");
        }
        
        // Epic 13.1.1 - Moving Platforms
        [MenuItem(MenuRoot + "Moving Platforms/Platform - 3x3m", false, 800)]
        private static void CreateMovingPlatformMenu_3m()
        {
            Vector3 pos = GetSceneViewPosition();
            var platform = CreateMovingPlatform(null, "MovingPlatform_3m", pos + Vector3.up * 0.15f, 
                new Vector3(3f, 0.3f, 3f), new Color(0.2f, 0.8f, 0.3f));
            CreateHeightLabel(platform, "MOVING PLATFORM", new Vector3(0f, 0.5f, 0f));
            Selection.activeGameObject = platform;
            Undo.RegisterCreatedObjectUndo(platform, "Create Moving Platform");
        }
        
        [MenuItem(MenuRoot + "Moving Platforms/Platform - 4x4m", false, 801)]
        private static void CreateMovingPlatformMenu_4m()
        {
            Vector3 pos = GetSceneViewPosition();
            var platform = CreateMovingPlatform(null, "MovingPlatform_4m", pos + Vector3.up * 0.15f, 
                new Vector3(4f, 0.3f, 4f), new Color(0.3f, 0.3f, 0.9f));
            CreateHeightLabel(platform, "MOVING PLATFORM", new Vector3(0f, 0.5f, 0f));
            Selection.activeGameObject = platform;
            Undo.RegisterCreatedObjectUndo(platform, "Create Moving Platform");
        }
        
        [MenuItem(MenuRoot + "Moving Platforms/Platform - 5x5m", false, 802)]
        private static void CreateMovingPlatformMenu_5m()
        {
            Vector3 pos = GetSceneViewPosition();
            var platform = CreateMovingPlatform(null, "MovingPlatform_5m", pos + Vector3.up * 0.15f, 
                new Vector3(5f, 0.3f, 5f), new Color(0.9f, 0.4f, 0.2f));
            CreateHeightLabel(platform, "MOVING PLATFORM", new Vector3(0f, 0.5f, 0f));
            Selection.activeGameObject = platform;
            Undo.RegisterCreatedObjectUndo(platform, "Create Moving Platform");
        }
        
        // Epic 13.1.3 - External Force Zones
        [MenuItem(MenuRoot + "External Forces/Wind Tunnel", false, 810)]
        private static void CreateWindTunnelMenu()
        {
            Vector3 pos = GetSceneViewPosition();
            CreateExternalForceZone(null, "WindTunnel", pos + Vector3.up * 1.5f, 
                new Vector3(4f, 3f, 8f), new Vector3(0f, 0f, 10f), true, true, 
                new Color(0.3f, 0.7f, 1f, 0.3f));
            var zone = GameObject.Find("WindTunnel");
            CreateHeightLabel(zone, "WIND TUNNEL", new Vector3(0f, 2f, 0f));
            Selection.activeGameObject = zone;
            Undo.RegisterCreatedObjectUndo(zone, "Create Wind Tunnel");
        }
        
        [MenuItem(MenuRoot + "External Forces/Launch Pad", false, 811)]
        private static void CreateLaunchPadMenu()
        {
            Vector3 pos = GetSceneViewPosition();
            CreateExternalForceZone(null, "LaunchPad", pos + Vector3.up * 0.25f, 
                new Vector3(3f, 0.5f, 3f), new Vector3(0f, 15f, 0f), true, false, 
                new Color(1f, 0.5f, 0.2f, 0.5f));
            var zone = GameObject.Find("LaunchPad");
            CreateHeightLabel(zone, "LAUNCH PAD", new Vector3(0f, 1f, 0f));
            Selection.activeGameObject = zone;
            Undo.RegisterCreatedObjectUndo(zone, "Create Launch Pad");
        }
        
        [MenuItem(MenuRoot + "External Forces/Radial Push Zone", false, 812)]
        private static void CreateRadialPushMenu()
        {
            Vector3 pos = GetSceneViewPosition();
            CreateExternalForceZone(null, "RadialPush", pos + Vector3.up * 1.5f, 
                new Vector3(5f, 3f, 5f), new Vector3(8f, 0f, 0f), false, true, 
                new Color(1f, 0.2f, 0.2f, 0.3f));
            var zone = GameObject.Find("RadialPush");
            CreateHeightLabel(zone, "RADIAL PUSH", new Vector3(0f, 2f, 0f));
            Selection.activeGameObject = zone;
            Undo.RegisterCreatedObjectUndo(zone, "Create Radial Push Zone");
        }
        
        [MenuItem(MenuRoot + "External Forces/Conveyor Belt", false, 813)]
        private static void CreateConveyorBeltMenu()
        {
            Vector3 pos = GetSceneViewPosition();
            CreateExternalForceZone(null, "ConveyorBelt", pos + Vector3.up * 0.25f, 
                new Vector3(10f, 0.5f, 2f), new Vector3(5f, 0f, 0f), true, true, 
                new Color(0.6f, 0.6f, 0.2f, 0.5f));
            var zone = GameObject.Find("ConveyorBelt");
            CreateHeightLabel(zone, "CONVEYOR BELT", new Vector3(0f, 1f, 0f));
            Selection.activeGameObject = zone;
            Undo.RegisterCreatedObjectUndo(zone, "Create Conveyor Belt");
        }
        
        #endregion
        
        #region Creation Helpers
        
        private static void CreateSingleBox(string name, float height)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject box = CreateBox(null, name, pos + Vector3.up * height / 2f, 
                                      new Vector3(1.5f, height, 0.8f), 
                                      new Color(0.2f, 0.6f, 1f));
            
            CreateHeightLabel(box, $"{height:F2}m", new Vector3(0f, height / 2f + 0.3f, 0f));
            
            Selection.activeGameObject = box;
            Undo.RegisterCreatedObjectUndo(box, $"Create {name}");
        }
        
        private static void CreateSingleLadder(string name, float height)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject ladder = CreateLadder(null, name, pos, height);
            
            CreateHeightLabel(ladder, $"{height:F1}m", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = ladder;
            Undo.RegisterCreatedObjectUndo(ladder, $"Create {name}");
        }
        
        private static void CreateSingleRamp(string name, float length, float angle)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject ramp = CreateRamp(null, name, pos, length, angle);
            
            CreateHeightLabel(ramp, $"{angle:F0}°", new Vector3(0f, 1f, 0f));
            
            Selection.activeGameObject = ramp;
            Undo.RegisterCreatedObjectUndo(ramp, $"Create {name}");
        }
        
        private static void CreateSingleClimbingWall(string name, float height, float width)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject wall = CreateClimbingWall(null, name, pos, height, width);
            
            CreateHeightLabel(wall, $"{height:F1}m WALL", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = wall;
            Undo.RegisterCreatedObjectUndo(wall, $"Create {name}");
        }
        
        private static void CreateSingleClimbableSphere(string name, float radius)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject sphere = CreateClimbableSphere(null, name, pos + Vector3.up * radius, radius);
            
            CreateHeightLabel(sphere, $"{radius:F1}m SPHERE", new Vector3(0f, radius + 0.5f, 0f));
            
            Selection.activeGameObject = sphere;
            Undo.RegisterCreatedObjectUndo(sphere, $"Create {name}");
        }
        
        private static void CreateSingleClimbableCylinder(string name, float height, float radius)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject cylinder = CreateClimbableCylinder(null, name, pos, height, radius);
            
            CreateHeightLabel(cylinder, $"{height:F1}m CYLINDER", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = cylinder;
            Undo.RegisterCreatedObjectUndo(cylinder, $"Create {name}");
        }
        
        private static void CreateSingleClimbablePipe(string name, float length, float radius)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject pipe = CreateClimbablePipe(null, name, pos + Vector3.up * 3f, length, radius);
            
            CreateHeightLabel(pipe, $"{length:F1}m PIPE", new Vector3(0f, radius + 0.5f, 0f));
            
            Selection.activeGameObject = pipe;
            Undo.RegisterCreatedObjectUndo(pipe, $"Create {name}");
        }
        
        private static void CreateSingleAngledClimbWall(string name, float height, float width, float angle)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject wall = CreateAngledClimbWall(null, name, pos, height, width, angle);
            
            CreateHeightLabel(wall, $"{angle:F0}° WALL", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = wall;
            Undo.RegisterCreatedObjectUndo(wall, $"Create {name}");
        }
        
        private static void CreateSingleCurvedClimbWall(string name, float height, float width, bool concave)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject wall = CreateCurvedClimbWall(null, name, pos, height, width, concave);
            
            string type = concave ? "CONCAVE" : "CONVEX";
            CreateHeightLabel(wall, $"{height:F1}m {type}", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = wall;
            Undo.RegisterCreatedObjectUndo(wall, $"Create {name}");
        }
        
        private static void CreateSingleClimbableArch(string name, float height, float width)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject arch = CreateClimbableArch(null, name, pos, height, width);
            
            CreateHeightLabel(arch, $"{height:F1}m ARCH", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = arch;
            Undo.RegisterCreatedObjectUndo(arch, $"Create {name}");
        }
        
        private static void CreateSingleClimbableColumn(string name, float height, float radius)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject column = CreateClimbableColumn(null, name, pos, height, radius);
            
            CreateHeightLabel(column, $"{height:F1}m COLUMN", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = column;
            Undo.RegisterCreatedObjectUndo(column, $"Create {name}");
        }
        
        private static void CreateSingleClimbableTower(string name, float height, float size)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject tower = CreateClimbableTower(null, name, pos, height, size);
            
            CreateHeightLabel(tower, $"{height:F1}m TOWER", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = tower;
            Undo.RegisterCreatedObjectUndo(tower, $"Create {name}");
        }
        
        private static void CreateSingleClimbableBridge(string name, float length, float width, float height)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject bridge = CreateClimbableBridge(null, name, pos, length, width, height);
            
            CreateHeightLabel(bridge, $"{length:F1}m BRIDGE", new Vector3(0f, height + 0.5f, 0f));
            
            Selection.activeGameObject = bridge;
            Undo.RegisterCreatedObjectUndo(bridge, $"Create {name}");
        }
        
        private static void CreateSingleGroundPlane(string name, Vector3 scale)
        {
            Vector3 pos = GetSceneViewPosition();
            GameObject ground = CreateGroundPlane(null, pos, scale);
            
            Selection.activeGameObject = ground;
            Undo.RegisterCreatedObjectUndo(ground, $"Create {name}");
        }
        
        private static GameObject CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            
            if (parent != null)
            {
                obj.transform.parent = parent.transform;
                obj.transform.localPosition = localPos;
            }
            else
            {
                obj.transform.position = localPos;
            }
            
            obj.transform.localScale = scale;
            
            SetupGridMaterial(obj, color);
            SetupPhysicsAuthoring(obj);
            
            return obj;
        }
        
        private static GameObject CreateLadder(GameObject parent, string name, Vector3 localPos, float height)
        {
            GameObject ladder = new GameObject(name);
            
            if (parent != null)
            {
                ladder.transform.parent = parent.transform;
                ladder.transform.localPosition = localPos;
            }
            else
            {
                ladder.transform.position = localPos;
            }
            
            // Create ladder backing
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Ladder_Back";
            back.transform.parent = ladder.transform;
            back.transform.localPosition = Vector3.zero;
            back.transform.localScale = new Vector3(1.0f, height, 0.1f);
            back.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            
            SetupLadderMaterial(back, new Color(0.7f, 0.5f, 0.3f));
            SetupPhysicsAuthoring(back);
            
            // Add ClimbableObjectAuthoring with proper BottomPoint/TopPoint
            // Ladder back: center at height/2, scale.y = height, so local ±0.5 spans the full height
            SetupClimbableAuthoring(back, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.Ladder, 1.5f);
            
            // Create rungs
            int rungCount = Mathf.CeilToInt(height / 0.3f);
            for (int i = 0; i <= rungCount; i++)
            {
                float rungHeight = (height / rungCount) * i;
                GameObject rung = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rung.name = $"Rung_{i}";
                rung.transform.parent = ladder.transform;
                rung.transform.localPosition = new Vector3(0f, rungHeight, 0.08f);
                rung.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
                rung.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                
                SetupLadderMaterial(rung, new Color(0.6f, 0.4f, 0.2f));
                
                // Rungs don't need physics, they're visual only
                Object.DestroyImmediate(rung.GetComponent<UnityEngine.Collider>());
            }
            
            return ladder;
        }
        
        private static GameObject CreateRamp(GameObject parent, string name, Vector3 localPos, float length, float angle)
        {
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            
            if (parent != null)
            {
                ramp.transform.parent = parent.transform;
                ramp.transform.localPosition = localPos;
            }
            else
            {
                ramp.transform.position = localPos;
            }
            
            // Calculate height based on angle
            float heightAtEnd = Mathf.Tan(angle * Mathf.Deg2Rad) * length;
            float avgHeight = heightAtEnd / 2f;
            
            ramp.transform.localScale = new Vector3(2f, 0.2f, length);
            ramp.transform.localPosition += new Vector3(0f, avgHeight, length / 2f);
            ramp.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            
            SetupGridMaterial(ramp, new Color(0.5f, 0.8f, 0.3f));
            SetupPhysicsAuthoring(ramp);
            
            return ramp;
        }
        
        private static GameObject CreateClimbingWall(GameObject parent, string name, Vector3 localPos, float height, float width)
        {
            GameObject wall = new GameObject(name);
            
            if (parent != null)
            {
                wall.transform.parent = parent.transform;
                wall.transform.localPosition = localPos;
            }
            else
            {
                wall.transform.position = localPos;
            }
            
            // Main wall surface
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Wall_Surface";
            surface.transform.parent = wall.transform;
            surface.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            surface.transform.localScale = new Vector3(width, height, 0.1f);
            
            SetupGridMaterial(surface, new Color(0.4f, 0.4f, 0.4f));
            SetupPhysicsAuthoring(surface);
            
            // Add ClimbableObjectAuthoring with proper BottomPoint/TopPoint
            // Wall surface: center at height/2, scale.y = height, so local ±0.5 spans the full height
            SetupClimbableAuthoring(surface, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.RockWall, 2.0f);
            
            // Create handholds (visual)
            int holdRows = Mathf.CeilToInt(height / 0.5f);
            int holdCols = Mathf.CeilToInt(width / 0.5f);
            
            for (int row = 0; row < holdRows; row++)
            {
                for (int col = 0; col < holdCols; col++)
                {
                    if (Random.value > 0.6f) // 40% chance for handhold
                    {
                        GameObject hold = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        hold.name = $"Handhold_{row}_{col}";
                        hold.transform.parent = wall.transform;
                        
                        float x = (col - holdCols / 2f) * 0.5f;
                        float y = row * 0.5f + 0.25f;
                        hold.transform.localPosition = new Vector3(x, y, 0.08f);
                        hold.transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
                        
                        SetupMaterial(hold, new Color(0.8f, 0.6f, 0.2f));
                        
                        // Handholds don't need physics
                        Object.DestroyImmediate(hold.GetComponent<UnityEngine.Collider>());
                    }
                }
            }
            
            return wall;
        }
        
        private static GameObject CreateClimbableSphere(GameObject parent, string name, Vector3 localPos, float radius)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            
            if (parent != null)
            {
                sphere.transform.parent = parent.transform;
                sphere.transform.localPosition = localPos;
            }
            else
            {
                sphere.transform.position = localPos;
            }
            
            sphere.transform.localScale = Vector3.one * radius * 2f;
            
            SetupGridMaterial(sphere, new Color(0.5f, 0.3f, 0.2f)); // Rocky brown
            SetupPhysicsAuthoring(sphere);
            
            // Add ClimbableObjectAuthoring with proper climb path (bottom to top of sphere)
            // For a sphere, bottom is at -radius, top is at +radius in local Y
            SetupClimbableAuthoring(sphere, 
                new Vector3(0f, -0.5f, 0f),  // Bottom of sphere (local, scaled)
                new Vector3(0f, 0.5f, 0f),   // Top of sphere (local, scaled)
                ClimbableType.RockWall, 
                radius + 1.0f);  // Interaction radius slightly larger than sphere
            
            // Add visual grip points scattered on sphere surface
            int gripCount = Mathf.CeilToInt(radius * 8);
            for (int i = 0; i < gripCount; i++)
            {
                float theta = Random.Range(0f, Mathf.PI * 2f);
                float phi = Random.Range(0.2f, Mathf.PI - 0.2f); // Avoid poles
                
                // Calculate point on unit sphere surface
                Vector3 unitSpherePoint = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi),
                    Mathf.Sin(phi) * Mathf.Sin(theta)
                );
                
                // Unity's default sphere is scale 1 = diameter 1, centered at origin
                // So we place grips at 0.5 * unitSpherePoint (on the surface of the unit sphere)
                // The parent sphere is scaled, so local coords are in unit sphere space
                GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                grip.name = $"Grip_{i}";
                grip.transform.parent = sphere.transform;
                grip.transform.localPosition = unitSpherePoint * 0.5f; // On surface of unit sphere
                grip.transform.localScale = Vector3.one * 0.04f;
                
                SetupMaterial(grip, new Color(0.8f, 0.6f, 0.3f));
                Object.DestroyImmediate(grip.GetComponent<UnityEngine.Collider>());
            }
            
            return sphere;
        }
        
        private static GameObject CreateClimbableCylinder(GameObject parent, string name, Vector3 localPos, float height, float radius)
        {
            GameObject cylinder = new GameObject(name);
            
            if (parent != null)
            {
                cylinder.transform.parent = parent.transform;
                cylinder.transform.localPosition = localPos;
            }
            else
            {
                cylinder.transform.position = localPos;
            }
            
            // Main cylinder body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Cylinder_Body";
            body.transform.parent = cylinder.transform;
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            body.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            
            SetupGridMaterial(body, new Color(0.4f, 0.35f, 0.3f));
            SetupPhysicsAuthoring(body);
            
            // Add ClimbableObjectAuthoring with proper climb path (bottom to top of cylinder)
            SetupClimbableAuthoring(cylinder, 
                Vector3.zero,                    // Bottom at ground level
                new Vector3(0f, height, 0f),     // Top at full height
                ClimbableType.Pipe, 
                radius + 1.0f);
            
            // Add visual grip bands
            int bandCount = Mathf.CeilToInt(height / 0.8f);
            for (int i = 0; i < bandCount; i++)
            {
                float bandHeight = (height / bandCount) * (i + 0.5f);
                
                // Create grip points around the band
                int pointsPerBand = 8;
                for (int j = 0; j < pointsPerBand; j++)
                {
                    float angle = (j / (float)pointsPerBand) * Mathf.PI * 2f;
                    Vector3 gripPos = new Vector3(
                        Mathf.Cos(angle) * (radius + 0.05f),
                        bandHeight,
                        Mathf.Sin(angle) * (radius + 0.05f)
                    );
                    
                    GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    grip.name = $"Grip_{i}_{j}";
                    grip.transform.parent = cylinder.transform;
                    grip.transform.localPosition = gripPos;
                    grip.transform.localScale = Vector3.one * 0.08f;
                    
                    SetupMaterial(grip, new Color(0.7f, 0.5f, 0.2f));
                    Object.DestroyImmediate(grip.GetComponent<UnityEngine.Collider>());
                }
            }
            
            return cylinder;
        }
        
        private static GameObject CreateClimbablePipe(GameObject parent, string name, Vector3 localPos, float length, float radius)
        {
            GameObject pipe = new GameObject(name);
            
            if (parent != null)
            {
                pipe.transform.parent = parent.transform;
                pipe.transform.localPosition = localPos;
            }
            else
            {
                pipe.transform.position = localPos;
            }
            
            // Horizontal pipe body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Pipe_Body";
            body.transform.parent = pipe.transform;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            body.transform.localScale = new Vector3(radius * 2f, length / 2f, radius * 2f);
            
            SetupMaterial(body, new Color(0.6f, 0.6f, 0.7f)); // Metal grey
            SetupPhysicsAuthoring(body);
            
            // Add ClimbableObjectAuthoring (Pipe type) - horizontal traversal from left to right
            SetupClimbableAuthoring(pipe, 
                new Vector3(-length / 2f, 0f, 0f),  // Left end
                new Vector3(length / 2f, 0f, 0f),   // Right end
                ClimbableType.Pipe, 
                radius + 1.0f);
            
            // Add support brackets
            int bracketCount = Mathf.CeilToInt(length / 2f);
            for (int i = 0; i <= bracketCount; i++)
            {
                float xPos = (i / (float)bracketCount - 0.5f) * length;
                
                GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bracket.name = $"Bracket_{i}";
                bracket.transform.parent = pipe.transform;
                bracket.transform.localPosition = new Vector3(xPos, -radius - 0.1f, 0f);
                bracket.transform.localScale = new Vector3(0.15f, 0.2f, 0.15f);
                
                SetupMaterial(bracket, new Color(0.3f, 0.3f, 0.35f));
                Object.DestroyImmediate(bracket.GetComponent<UnityEngine.Collider>());
            }
            
            return pipe;
        }
        
        private static GameObject CreateAngledClimbWall(GameObject parent, string name, Vector3 localPos, float height, float width, float angle)
        {
            GameObject wall = new GameObject(name);
            
            if (parent != null)
            {
                wall.transform.parent = parent.transform;
                wall.transform.localPosition = localPos;
            }
            else
            {
                wall.transform.position = localPos;
            }
            
            // Calculate offset based on angle
            float angleRad = (angle - 90f) * Mathf.Deg2Rad;
            float horizontalOffset = Mathf.Sin(angleRad) * height / 2f;
            
            // Main wall surface
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Wall_Surface";
            surface.transform.parent = wall.transform;
            surface.transform.localPosition = new Vector3(0f, height / 2f, horizontalOffset / 2f);
            surface.transform.localScale = new Vector3(width, height, 0.15f);
            surface.transform.localRotation = Quaternion.Euler(90f - angle, 0f, 0f);
            
            Color wallColor = angle > 90f ? new Color(0.6f, 0.3f, 0.3f) : new Color(0.4f, 0.5f, 0.4f);
            SetupGridMaterial(surface, wallColor);
            SetupPhysicsAuthoring(surface);
            
            // Add ClimbableObjectAuthoring with proper climb path
            float topZ = Mathf.Sin(angleRad) * height;
            SetupClimbableAuthoring(wall, 
                Vector3.zero,                        // Bottom at ground
                new Vector3(0f, height, topZ),       // Top follows angle
                ClimbableType.RockWall, 
                2.0f);
            
            // Create handholds
            int holdRows = Mathf.CeilToInt(height / 0.5f);
            int holdCols = Mathf.CeilToInt(width / 0.5f);
            
            for (int row = 0; row < holdRows; row++)
            {
                for (int col = 0; col < holdCols; col++)
                {
                    if (Random.value > 0.5f)
                    {
                        float rowProgress = row / (float)holdRows;
                        float x = (col - holdCols / 2f) * 0.5f;
                        float y = row * 0.5f + 0.25f;
                        float z = Mathf.Sin(angleRad) * height * rowProgress + 0.1f;
                        
                        GameObject hold = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        hold.name = $"Handhold_{row}_{col}";
                        hold.transform.parent = wall.transform;
                        hold.transform.localPosition = new Vector3(x, y, z);
                        hold.transform.localScale = new Vector3(0.12f, 0.12f, 0.08f);
                        
                        SetupMaterial(hold, new Color(0.9f, 0.7f, 0.3f));
                        Object.DestroyImmediate(hold.GetComponent<UnityEngine.Collider>());
                    }
                }
            }
            
            // Add angle label
            CreateHeightLabel(wall, $"{angle:F0}°", new Vector3(0f, height + 0.3f, 0f));
            
            return wall;
        }
        
        private static GameObject CreateCurvedClimbWall(GameObject parent, string name, Vector3 localPos, float height, float width, bool concave)
        {
            GameObject wall = new GameObject(name);
            
            if (parent != null)
            {
                wall.transform.parent = parent.transform;
                wall.transform.localPosition = localPos;
            }
            else
            {
                wall.transform.position = localPos;
            }
            
            // Create curved wall using multiple segments
            int segments = 8;
            float curveDepth = concave ? -1.5f : 1.5f;
            
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1);
                float yPos = t * height;
                
                // Parabolic curve
                float zOffset = curveDepth * (4f * t * (1f - t));
                
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Segment_{i}";
                segment.transform.parent = wall.transform;
                segment.transform.localPosition = new Vector3(0f, yPos, zOffset);
                segment.transform.localScale = new Vector3(width, height / segments + 0.05f, 0.15f);
                
                // Calculate rotation to follow curve
                if (i < segments - 1)
                {
                    float nextT = (i + 1) / (float)(segments - 1);
                    float nextZ = curveDepth * (4f * nextT * (1f - nextT));
                    float deltaY = height / segments;
                    float deltaZ = nextZ - zOffset;
                    float segmentAngle = Mathf.Atan2(deltaZ, deltaY) * Mathf.Rad2Deg;
                    segment.transform.localRotation = Quaternion.Euler(segmentAngle, 0f, 0f);
                }
                
                Color segColor = concave ? new Color(0.3f, 0.4f, 0.5f) : new Color(0.5f, 0.4f, 0.3f);
                SetupGridMaterial(segment, segColor);
                SetupPhysicsAuthoring(segment);
                // Note: Climbable is on parent wall, not segments
                
                // Add handholds to segment
                int holdsPerSegment = 3;
                for (int h = 0; h < holdsPerSegment; h++)
                {
                    if (Random.value > 0.4f)
                    {
                        float hx = (h - holdsPerSegment / 2f) * 0.6f;
                        float hz = concave ? 0.12f : -0.12f;
                        
                        GameObject hold = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        hold.name = $"Hold_{i}_{h}";
                        hold.transform.parent = segment.transform;
                        hold.transform.localPosition = new Vector3(hx, 0f, hz);
                        hold.transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
                        
                        SetupMaterial(hold, new Color(0.85f, 0.65f, 0.25f));
                        Object.DestroyImmediate(hold.GetComponent<UnityEngine.Collider>());
                    }
                }
            }
            
            // Calculate the top Z offset based on curve apex (at t=0.5)
            float topZ = curveDepth * (4f * 0.5f * (1f - 0.5f)); // Parabola apex
            SetupClimbableAuthoring(wall, Vector3.zero, new Vector3(0f, height, topZ), ClimbableType.RockWall, 2.0f);
            
            return wall;
        }
        
        private static GameObject CreateClimbableArch(GameObject parent, string name, Vector3 localPos, float height, float width)
        {
            GameObject arch = new GameObject(name);
            
            if (parent != null)
            {
                arch.transform.parent = parent.transform;
                arch.transform.localPosition = localPos;
            }
            else
            {
                arch.transform.position = localPos;
            }
            
            // Left pillar
            GameObject leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPillar.name = "Left_Pillar";
            leftPillar.transform.parent = arch.transform;
            leftPillar.transform.localPosition = new Vector3(-width / 2f, height / 2f, 0f);
            leftPillar.transform.localScale = new Vector3(0.5f, height, 0.5f);
            
            SetupGridMaterial(leftPillar, new Color(0.45f, 0.4f, 0.35f));
            SetupPhysicsAuthoring(leftPillar);
            // Pillar climb path: local bottom to local top (pillar local space: center at 0, scale Y = height)
            SetupClimbableAuthoring(leftPillar, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.RockWall, 1.5f);
            
            // Right pillar
            GameObject rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPillar.name = "Right_Pillar";
            rightPillar.transform.parent = arch.transform;
            rightPillar.transform.localPosition = new Vector3(width / 2f, height / 2f, 0f);
            rightPillar.transform.localScale = new Vector3(0.5f, height, 0.5f);
            
            SetupGridMaterial(rightPillar, new Color(0.45f, 0.4f, 0.35f));
            SetupPhysicsAuthoring(rightPillar);
            SetupClimbableAuthoring(rightPillar, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.RockWall, 1.5f);
            
            // Arch top (curved using segments)
            int archSegments = 12;
            for (int i = 0; i < archSegments; i++)
            {
                float angle = (i / (float)(archSegments - 1)) * Mathf.PI;
                float x = Mathf.Cos(angle) * (width / 2f - 0.25f);
                float y = height + Mathf.Sin(angle) * 1.5f;
                
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Arch_Segment_{i}";
                segment.transform.parent = arch.transform;
                segment.transform.localPosition = new Vector3(x, y, 0f);
                segment.transform.localScale = new Vector3(0.4f, 0.4f, 0.5f);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg + 90f);
                
                SetupGridMaterial(segment, new Color(0.5f, 0.45f, 0.4f));
                SetupPhysicsAuthoring(segment);
                // Arch segments are decorative - pillars are the climbable parts
            }
            
            return arch;
        }
        
        private static GameObject CreateClimbableColumn(GameObject parent, string name, Vector3 localPos, float height, float radius)
        {
            GameObject column = new GameObject(name);
            
            if (parent != null)
            {
                column.transform.parent = parent.transform;
                column.transform.localPosition = localPos;
            }
            else
            {
                column.transform.position = localPos;
            }
            
            // Main column body (octagonal approximation using cylinder)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Column_Body";
            body.transform.parent = column.transform;
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            body.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            
            SetupGridMaterial(body, new Color(0.55f, 0.5f, 0.45f));
            SetupPhysicsAuthoring(body);
            // Column climb path: bottom to top (cylinder local space: center at 0, scale Y = height/2 so half extent is ±0.5)
            SetupClimbableAuthoring(body, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.Pipe, 1.5f);
            
            // Decorative base
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Column_Base";
            baseObj.transform.parent = column.transform;
            baseObj.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            baseObj.transform.localScale = new Vector3(radius * 2.5f, 0.2f, radius * 2.5f);
            
            SetupMaterial(baseObj, new Color(0.4f, 0.35f, 0.3f));
            SetupPhysicsAuthoring(baseObj);
            
            // Decorative capital
            GameObject capital = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            capital.name = "Column_Capital";
            capital.transform.parent = column.transform;
            capital.transform.localPosition = new Vector3(0f, height - 0.15f, 0f);
            capital.transform.localScale = new Vector3(radius * 2.5f, 0.15f, radius * 2.5f);
            
            SetupMaterial(capital, new Color(0.4f, 0.35f, 0.3f));
            SetupPhysicsAuthoring(capital);
            
            // Add spiral grip pattern
            int gripSpirals = 3;
            int gripsPerSpiral = Mathf.CeilToInt(height * 2);
            for (int s = 0; s < gripSpirals; s++)
            {
                float spiralOffset = (s / (float)gripSpirals) * Mathf.PI * 2f;
                
                for (int g = 0; g < gripsPerSpiral; g++)
                {
                    float t = g / (float)gripsPerSpiral;
                    float angle = spiralOffset + t * Mathf.PI * 4f;
                    float y = t * height + 0.3f;
                    
                    Vector3 gripPos = new Vector3(
                        Mathf.Cos(angle) * (radius + 0.05f),
                        y,
                        Mathf.Sin(angle) * (radius + 0.05f)
                    );
                    
                    GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    grip.name = $"Grip_{s}_{g}";
                    grip.transform.parent = column.transform;
                    grip.transform.localPosition = gripPos;
                    grip.transform.localScale = Vector3.one * 0.1f;
                    
                    SetupMaterial(grip, new Color(0.75f, 0.55f, 0.25f));
                    Object.DestroyImmediate(grip.GetComponent<UnityEngine.Collider>());
                }
            }
            
            return column;
        }
        
        private static GameObject CreateClimbableTower(GameObject parent, string name, Vector3 localPos, float height, float size)
        {
            GameObject tower = new GameObject(name);
            
            if (parent != null)
            {
                tower.transform.parent = parent.transform;
                tower.transform.localPosition = localPos;
            }
            else
            {
                tower.transform.position = localPos;
            }
            
            // Create tower with multiple levels
            int levels = Mathf.CeilToInt(height / 2f);
            float levelHeight = height / levels;
            
            for (int i = 0; i < levels; i++)
            {
                float levelY = i * levelHeight;
                float levelSize = size * (1f - i * 0.1f); // Taper slightly
                
                // Level platform
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = $"Level_{i}_Platform";
                platform.transform.parent = tower.transform;
                platform.transform.localPosition = new Vector3(0f, levelY + levelHeight - 0.1f, 0f);
                platform.transform.localScale = new Vector3(levelSize, 0.2f, levelSize);
                
                SetupGridMaterial(platform, new Color(0.4f, 0.4f, 0.45f));
                SetupPhysicsAuthoring(platform);
                
                // Level walls (4 sides)
                for (int side = 0; side < 4; side++)
                {
                    float angle = side * 90f * Mathf.Deg2Rad;
                    Vector3 wallPos = new Vector3(
                        Mathf.Cos(angle) * (levelSize / 2f - 0.05f),
                        levelY + levelHeight / 2f,
                        Mathf.Sin(angle) * (levelSize / 2f - 0.05f)
                    );
                    
                    GameObject wallSide = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallSide.name = $"Level_{i}_Wall_{side}";
                    wallSide.transform.parent = tower.transform;
                    wallSide.transform.localPosition = wallPos;
                    wallSide.transform.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                    wallSide.transform.localScale = new Vector3(levelSize * 0.8f, levelHeight, 0.1f);
                    
                    SetupGridMaterial(wallSide, new Color(0.5f, 0.45f, 0.4f));
                    SetupPhysicsAuthoring(wallSide);
                    // Wall climb path: bottom to top of each wall segment (cube local space: ±0.5 extent in Y)
                    SetupClimbableAuthoring(wallSide, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.RockWall, 1.5f);
                    
                    // Add handholds
                    int holdsPerWall = 4;
                    for (int h = 0; h < holdsPerWall; h++)
                    {
                        if (Random.value > 0.3f)
                        {
                            float hx = (h - holdsPerWall / 2f) * 0.4f;
                            float hy = Random.Range(-levelHeight / 3f, levelHeight / 3f);
                            
                            GameObject hold = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            hold.name = $"Hold_{i}_{side}_{h}";
                            hold.transform.parent = wallSide.transform;
                            hold.transform.localPosition = new Vector3(hx, hy, 0.08f);
                            hold.transform.localScale = new Vector3(0.12f, 0.12f, 0.08f);
                            
                            SetupMaterial(hold, new Color(0.85f, 0.6f, 0.2f));
                            Object.DestroyImmediate(hold.GetComponent<UnityEngine.Collider>());
                        }
                    }
                }
            }
            
            return tower;
        }
        
        private static GameObject CreateClimbableBridge(GameObject parent, string name, Vector3 localPos, float length, float width, float height)
        {
            GameObject bridge = new GameObject(name);
            
            if (parent != null)
            {
                bridge.transform.parent = parent.transform;
                bridge.transform.localPosition = localPos;
            }
            else
            {
                bridge.transform.position = localPos;
            }
            
            // Bridge deck
            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Bridge_Deck";
            deck.transform.parent = bridge.transform;
            deck.transform.localPosition = new Vector3(0f, height, 0f);
            deck.transform.localScale = new Vector3(length, 0.2f, width);
            
            SetupGridMaterial(deck, new Color(0.5f, 0.4f, 0.3f));
            SetupPhysicsAuthoring(deck);
            
            // Support pillars
            GameObject leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPillar.name = "Left_Pillar";
            leftPillar.transform.parent = bridge.transform;
            leftPillar.transform.localPosition = new Vector3(-length / 2f + 0.3f, height / 2f, 0f);
            leftPillar.transform.localScale = new Vector3(0.4f, height, 0.4f);
            
            SetupGridMaterial(leftPillar, new Color(0.45f, 0.4f, 0.35f));
            SetupPhysicsAuthoring(leftPillar);
            // Pillar climb path: bottom to top (cube local space: ±0.5 extent in Y)
            SetupClimbableAuthoring(leftPillar, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.RockWall, 1.5f);
            
            GameObject rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPillar.name = "Right_Pillar";
            rightPillar.transform.parent = bridge.transform;
            rightPillar.transform.localPosition = new Vector3(length / 2f - 0.3f, height / 2f, 0f);
            rightPillar.transform.localScale = new Vector3(0.4f, height, 0.4f);
            
            SetupGridMaterial(rightPillar, new Color(0.45f, 0.4f, 0.35f));
            SetupPhysicsAuthoring(rightPillar);
            SetupClimbableAuthoring(rightPillar, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), ClimbableType.RockWall, 1.5f);
            
            // Underside handholds (for hanging traverse)
            int underHolds = Mathf.CeilToInt(length / 0.6f);
            for (int i = 0; i < underHolds; i++)
            {
                float x = (i / (float)(underHolds - 1) - 0.5f) * (length - 1f);
                
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject hold = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    hold.name = $"UnderHold_{i}_{side}";
                    hold.transform.parent = bridge.transform;
                    hold.transform.localPosition = new Vector3(x, height - 0.2f, side * (width / 3f));
                    hold.transform.localScale = new Vector3(0.15f, 0.1f, 0.15f);
                    
                    SetupMaterial(hold, new Color(0.7f, 0.5f, 0.2f));
                    Object.DestroyImmediate(hold.GetComponent<UnityEngine.Collider>());
                }
            }
            
            // Side railings (climbable - horizontal traverse)
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject railing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                railing.name = side < 0 ? "Left_Railing" : "Right_Railing";
                railing.transform.parent = bridge.transform;
                railing.transform.localPosition = new Vector3(0f, height + 0.5f, side * (width / 2f - 0.1f));
                railing.transform.localScale = new Vector3(length, 0.8f, 0.1f);
                
                SetupGridMaterial(railing, new Color(0.4f, 0.35f, 0.3f));
                SetupPhysicsAuthoring(railing);
                // Railing climb path: horizontal from left to right end (cube local space: ±0.5 extent in X)
                SetupClimbableAuthoring(railing, new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), ClimbableType.Pipe, 1.5f);
            }
            
            return bridge;
        }
        
        private static GameObject CreateGroundPlane(GameObject parent, Vector3 localPos, Vector3 scale)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            
            if (parent != null)
            {
                ground.transform.parent = parent.transform;
                ground.transform.localPosition = localPos;
            }
            else
            {
                ground.transform.position = localPos;
            }
            
            ground.transform.localScale = scale;
            
            SetupGridMaterial(ground, Color.white);
            SetupPhysicsAuthoring(ground);
            
            // Make it reflective
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                renderer.sharedMaterial.SetFloat("_Smoothness", 0.9f);
                renderer.sharedMaterial.SetFloat("_Metallic", 0.3f);
            }
            
            return ground;
        }
        
        #endregion
        
        #region Labels
        
        private static void CreateTitleLabel(GameObject parent, string text, Vector3 localPos, float scale)
        {
            GameObject labelObj = new GameObject($"{text}_Label");
            labelObj.transform.parent = parent.transform;
            labelObj.transform.localPosition = localPos;
            labelObj.transform.localScale = Vector3.one * scale;
            
            // Use TextMesh (legacy) - avoids SRP Batcher issues with DOTS/Entities Graphics
            TextMesh textMesh = labelObj.AddComponent<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = text;
                textMesh.fontSize = 72;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.color = Color.white;
                textMesh.fontStyle = FontStyle.Bold;
                textMesh.characterSize = 0.05f;
            }
        }
        
        private static void CreateSectionLabel(GameObject parent, string text, Vector3 localPos)
        {
            GameObject labelObj = new GameObject($"{text}_Label");
            labelObj.transform.parent = parent.transform;
            labelObj.transform.localPosition = localPos;
            
            TextMesh textMesh = labelObj.AddComponent<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = text;
                textMesh.fontSize = 48;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.color = Color.cyan;
                textMesh.fontStyle = FontStyle.Bold;
                textMesh.characterSize = 0.04f;
            }
        }
        
        private static void CreateHeightLabel(GameObject parent, string text, Vector3 localOffset)
        {
            GameObject labelObj = new GameObject("Height_Label");
            labelObj.transform.parent = parent.transform;
            labelObj.transform.localPosition = localOffset;
            labelObj.transform.localScale = Vector3.one * 0.5f;
            
            TextMesh textMesh = labelObj.AddComponent<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = text;
                textMesh.fontSize = 60;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.color = Color.yellow;
                textMesh.fontStyle = FontStyle.Bold;
                textMesh.characterSize = 0.06f;
            }
        }
        
        #endregion
        
        #region Material Setup
        
        private static UnityEngine.Material GetOrCreateGridMaterial()
        {
            if (s_GridMaterial != null) return s_GridMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader != null)
            {
                s_GridMaterial = new UnityEngine.Material(shader);
                
                // Create procedural grid texture
                Texture2D gridTex = CreateGridTexture(512, 512, 16);
                s_GridMaterial.mainTexture = gridTex;
                
                return s_GridMaterial;
            }
            
            return null;
        }
        
        private static UnityEngine.Material GetOrCreateLadderMaterial()
        {
            if (s_LadderMaterial != null) return s_LadderMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader != null)
            {
                s_LadderMaterial = new UnityEngine.Material(shader);
                s_LadderMaterial.name = "Ladder_Material";
                
                // Create wood-like texture
                Texture2D woodTex = CreateWoodTexture(256, 256);
                s_LadderMaterial.mainTexture = woodTex;
                
                return s_LadderMaterial;
            }
            
            return null;
        }
        
        private static void SetupGridMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            
            UnityEngine.Material gridMat = GetOrCreateGridMaterial();
            if (gridMat != null)
            {
                UnityEngine.Material instanceMat = new UnityEngine.Material(gridMat);
                instanceMat.color = color;
                renderer.sharedMaterial = instanceMat;
            }
            else
            {
                SetupMaterial(obj, color);
            }
        }
        
        private static void SetupLadderMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            
            UnityEngine.Material ladderMat = GetOrCreateLadderMaterial();
            if (ladderMat != null)
            {
                UnityEngine.Material instanceMat = new UnityEngine.Material(ladderMat);
                instanceMat.color = color;
                renderer.sharedMaterial = instanceMat;
            }
            else
            {
                SetupMaterial(obj, color);
            }
        }
        
        private static void SetupMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            
            if (shader != null)
            {
                UnityEngine.Material mat = new UnityEngine.Material(shader);

                // Set color for both URP and Standard shaders
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);

                renderer.sharedMaterial = mat;
            }
        }
        
        private static Texture2D CreateGridTexture(int width, int height, int gridSize)
        {
            Texture2D tex = new Texture2D(width, height);
            Color backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            Color lineColor = new Color(0.4f, 0.4f, 0.4f);
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isGridLine = (x % gridSize == 0) || (y % gridSize == 0);
                    tex.SetPixel(x, y, isGridLine ? lineColor : backgroundColor);
                }
            }
            
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }
        
        private static Texture2D CreateWoodTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height);
            
            for (int y = 0; y < height; y++)
            {
                float noise = Mathf.PerlinNoise(y * 0.1f, 0) * 0.2f;
                Color woodColor = new Color(0.4f + noise, 0.25f + noise * 0.5f, 0.1f);
                
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, woodColor);
                }
            }
            
            tex.Apply();
            return tex;
        }
        
        #endregion
        
        #region Physics Setup
        
        private static void SetupPhysicsAuthoring(GameObject obj)
        {
            StaticPhysicsObjectAuthoring physicsAuth = obj.AddComponent<StaticPhysicsObjectAuthoring>();
            physicsAuth.belongsTo = 1u;
            physicsAuth.collidesWith = ~0u;
        }

        // ============================================================================
        // SECTION 16: CROUCH TESTS (Epic 13.15)
        // ============================================================================

        private static void CreateCrouchTestSection(GameObject parent, Vector3 basePos)
        {
            GameObject section = new GameObject("Section_Crouch_Tests");
            section.transform.parent = parent.transform;
            section.transform.localPosition = basePos;

            CreateSectionLabel(section, "CROUCH TESTS (Epic 13.15)", new Vector3(0f, 5f, -1f));

            // 13.15.T1: Low Ceiling Tunnel
            CreateLowCeilingTunnel(section, new Vector3(-12f, 0f, 0f));

            // 13.15.T2: Vent Shaft System
            CreateVentShaftSystem(section, new Vector3(12f, 0f, 0f));

            // 13.15.T3: Crouch Cover Wall
            CreateCrouchCoverWall(section, new Vector3(0f, 0f, 20f));

            // 13.15.T4: Standup Trap (placeholder - requires moving ceiling)
            CreateStandupTrap(section, new Vector3(-12f, 0f, 35f));

            // 13.15.T5: Collider Visualization Chamber
            CreateColliderVisualizationChamber(section, new Vector3(12f, 0f, 35f));
        }

        // 13.15.T1: Low Ceiling Tunnel
        private static void CreateLowCeilingTunnel(GameObject parent, Vector3 localPos)
        {
            GameObject tunnel = new GameObject("LowCeilingTunnel");
            tunnel.transform.parent = parent.transform;
            tunnel.transform.localPosition = localPos;

            CreateHeightLabel(tunnel, "LOW CEILING TUNNEL\n(Stay crouched)", new Vector3(0f, 4f, 6f));

            // Floor
            CreateBox(tunnel, "Floor", new Vector3(0f, -0.1f, 6f),
                new Vector3(4f, 0.2f, 14f), new Color(0.4f, 0.4f, 0.4f));

            // Entrance: 2m ceiling (standing allowed)
            CreateBox(tunnel, "Entrance_Ceiling_2m", new Vector3(0f, 2f, 0f),
                new Vector3(4f, 0.3f, 2f), new Color(0.5f, 0.7f, 0.5f));
            CreateBox(tunnel, "Entrance_LeftWall", new Vector3(-2f, 1f, 0f),
                new Vector3(0.3f, 2f, 2f), new Color(0.5f, 0.5f, 0.5f));
            CreateBox(tunnel, "Entrance_RightWall", new Vector3(2f, 1f, 0f),
                new Vector3(0.3f, 2f, 2f), new Color(0.5f, 0.5f, 0.5f));
            CreateHeightLabel(tunnel, "2m\n(Stand OK)", new Vector3(0f, 2.5f, 0f));

            // Main tunnel: 1.2m ceiling (crouch required)
            CreateBox(tunnel, "Tunnel_Ceiling_1.2m", new Vector3(0f, 1.2f, 5f),
                new Vector3(4f, 0.3f, 6f), new Color(0.8f, 0.6f, 0.3f));
            CreateBox(tunnel, "Tunnel_LeftWall", new Vector3(-2f, 0.6f, 5f),
                new Vector3(0.3f, 1.2f, 6f), new Color(0.5f, 0.5f, 0.5f));
            CreateBox(tunnel, "Tunnel_RightWall", new Vector3(2f, 0.6f, 5f),
                new Vector3(0.3f, 1.2f, 6f), new Color(0.5f, 0.5f, 0.5f));
            CreateHeightLabel(tunnel, "1.2m\n(CROUCH REQUIRED)", new Vector3(0f, 1.8f, 5f));

            // Alcove: 1.0m ceiling (even lower)
            CreateBox(tunnel, "Alcove_Ceiling_1m", new Vector3(-2f, 1f, 10f),
                new Vector3(2f, 0.3f, 2f), new Color(0.9f, 0.4f, 0.3f));
            CreateHeightLabel(tunnel, "1m\n(LOW)", new Vector3(-2f, 1.5f, 10f));

            // Exit: back to 2m
            CreateBox(tunnel, "Exit_Ceiling_2m", new Vector3(0f, 2f, 12f),
                new Vector3(4f, 0.3f, 2f), new Color(0.5f, 0.7f, 0.5f));
            CreateHeightLabel(tunnel, "2m\n(Stand OK)", new Vector3(0f, 2.5f, 12f));
        }

        // 13.15.T2: Vent Shaft System
        private static void CreateVentShaftSystem(GameObject parent, Vector3 localPos)
        {
            GameObject vents = new GameObject("VentShaftSystem");
            vents.transform.parent = parent.transform;
            vents.transform.localPosition = localPos;

            CreateHeightLabel(vents, "VENT SHAFT SYSTEM\n(1m x 1m cross-section)", new Vector3(0f, 4f, 5f));

            float ventHeight = 1f;
            float ventWidth = 1f;
            Color ventColor = new Color(0.3f, 0.3f, 0.35f);
            Color grateColor = new Color(0.4f, 0.4f, 0.5f);

            // Entry grate (on floor level)
            CreateBox(vents, "Vent_Entry_Grate", new Vector3(0f, 0.05f, -1f),
                new Vector3(ventWidth, 0.1f, ventWidth), grateColor);
            CreateHeightLabel(vents, "ENTRY", new Vector3(0f, 0.5f, -1f));

            // Straight section 1
            CreateVentSegment(vents, "Vent_Straight_1", new Vector3(0f, ventHeight/2, 2f), 5f, ventWidth, ventHeight, ventColor);

            // Junction (cross intersection)
            CreateBox(vents, "Junction_Floor", new Vector3(0f, -0.05f, 6f),
                new Vector3(ventWidth * 2, 0.1f, ventWidth * 2), ventColor);
            CreateBox(vents, "Junction_Ceiling", new Vector3(0f, ventHeight + 0.05f, 6f),
                new Vector3(ventWidth * 2, 0.1f, ventWidth * 2), ventColor);
            CreateHeightLabel(vents, "JUNCTION", new Vector3(0f, ventHeight + 0.8f, 6f));

            // Straight section 2 (continues forward)
            CreateVentSegment(vents, "Vent_Straight_2", new Vector3(0f, ventHeight/2, 10f), 4f, ventWidth, ventHeight, ventColor);

            // Exit with low ceiling (can't stand)
            CreateBox(vents, "Exit_LowCeiling", new Vector3(0f, ventHeight + 0.05f, 13f),
                new Vector3(2f, 0.1f, 2f), new Color(0.9f, 0.4f, 0.3f));
            CreateHeightLabel(vents, "EXIT (LOW)\nSTAY CROUCHED", new Vector3(0f, ventHeight + 0.8f, 13f));

            // Side exit with high ceiling (can stand)
            CreateBox(vents, "Exit_HighCeiling_Floor", new Vector3(3f, -0.05f, 6f),
                new Vector3(4f, 0.1f, ventWidth * 2), new Color(0.4f, 0.6f, 0.4f));
            CreateHeightLabel(vents, "EXIT (HIGH)\nCAN STAND", new Vector3(5f, 0.8f, 6f));
        }

        private static void CreateVentSegment(GameObject parent, string name, Vector3 center, float length, float width, float height, Color color)
        {
            // Floor
            CreateBox(parent, name + "_Floor", center + new Vector3(0f, -height/2 - 0.05f, 0f),
                new Vector3(width, 0.1f, length), color);
            // Ceiling
            CreateBox(parent, name + "_Ceiling", center + new Vector3(0f, height/2 + 0.05f, 0f),
                new Vector3(width, 0.1f, length), color);
            // Left wall
            CreateBox(parent, name + "_LeftWall", center + new Vector3(-width/2 - 0.05f, 0f, 0f),
                new Vector3(0.1f, height, length), color);
            // Right wall
            CreateBox(parent, name + "_RightWall", center + new Vector3(width/2 + 0.05f, 0f, 0f),
                new Vector3(0.1f, height, length), color);
        }

        // 13.15.T3: Crouch Cover Wall
        private static void CreateCrouchCoverWall(GameObject parent, Vector3 localPos)
        {
            GameObject cover = new GameObject("CrouchCoverWall");
            cover.transform.parent = parent.transform;
            cover.transform.localPosition = localPos;

            CreateHeightLabel(cover, "CROUCH COVER TEST\n(Hide behind walls)", new Vector3(0f, 4f, 0f));

            // Floor
            CreateBox(cover, "Floor", new Vector3(0f, -0.1f, 0f),
                new Vector3(16f, 0.2f, 10f), new Color(0.4f, 0.4f, 0.4f));

            // Low wall (1m) - crouching fully hides player
            var lowWall = CreateBox(cover, "Wall_Low_1m", new Vector3(-4f, 0.5f, 0f),
                new Vector3(4f, 1f, 0.3f), new Color(0.5f, 0.5f, 0.5f));
            CreateHeightLabel(lowWall, "1m WALL\n(Crouch = HIDDEN)", new Vector3(0f, 1.2f, 0f));

            // Medium wall (1.5m) - standing exposes head
            var medWall = CreateBox(cover, "Wall_Medium_1.5m", new Vector3(4f, 0.75f, 0f),
                new Vector3(4f, 1.5f, 0.3f), new Color(0.6f, 0.6f, 0.6f));
            CreateHeightLabel(medWall, "1.5m WALL\n(Stand = HEAD EXPOSED)", new Vector3(0f, 1.5f, 0f));

            // Window cutout for visibility test
            var windowFrame = CreateBox(cover, "WindowFrame", new Vector3(0f, 1.25f, -4f),
                new Vector3(2f, 0.8f, 0.3f), new Color(0.45f, 0.45f, 0.5f));
            CreateHeightLabel(windowFrame, "WINDOW\n(Crouch to hide)", new Vector3(0f, 1.2f, 0f));
            // Frame top
            CreateBox(cover, "WindowFrame_Top", new Vector3(0f, 1.8f, -4f),
                new Vector3(2f, 0.3f, 0.3f), new Color(0.5f, 0.5f, 0.5f));
            // Frame bottom
            CreateBox(cover, "WindowFrame_Bottom", new Vector3(0f, 0.45f, -4f),
                new Vector3(2f, 0.9f, 0.3f), new Color(0.5f, 0.5f, 0.5f));

            // Shooter spawn point indicator
            var shooterPoint = CreateBox(cover, "ShooterPoint", new Vector3(0f, 0.5f, 8f),
                new Vector3(1f, 1f, 1f), new Color(0.9f, 0.2f, 0.2f));
            CreateHeightLabel(shooterPoint, "SHOOTER\nPOSITION", new Vector3(0f, 1.5f, 0f));
        }

        // 13.15.T4: Standup Trap (lowering ceiling)
        private static void CreateStandupTrap(GameObject parent, Vector3 localPos)
        {
            GameObject trap = new GameObject("StandupTrap");
            trap.transform.parent = parent.transform;
            trap.transform.localPosition = localPos;

            CreateHeightLabel(trap, "STANDUP TRAP\n(Ceiling lowers to 1.2m)", new Vector3(0f, 5f, 0f));

            // Floor with pressure plate visual
            CreateBox(trap, "Floor", new Vector3(0f, -0.1f, 0f),
                new Vector3(6f, 0.2f, 6f), new Color(0.4f, 0.4f, 0.4f));

            // Pressure plate (trigger zone marker)
            var plate = CreateBox(trap, "PressurePlate", new Vector3(0f, 0.02f, 0f),
                new Vector3(2f, 0.05f, 2f), new Color(0.8f, 0.3f, 0.3f));
            CreateHeightLabel(plate, "PRESSURE\nPLATE", new Vector3(0f, 0.5f, 0f));

            // Walls
            CreateBox(trap, "LeftWall", new Vector3(-3f, 1.5f, 0f),
                new Vector3(0.3f, 3f, 6f), new Color(0.5f, 0.5f, 0.5f));
            CreateBox(trap, "RightWall", new Vector3(3f, 1.5f, 0f),
                new Vector3(0.3f, 3f, 6f), new Color(0.5f, 0.5f, 0.5f));
            CreateBox(trap, "BackWall", new Vector3(0f, 1.5f, -3f),
                new Vector3(6f, 3f, 0.3f), new Color(0.5f, 0.5f, 0.5f));
            CreateBox(trap, "FrontWall", new Vector3(0f, 1.5f, 3f),
                new Vector3(6f, 3f, 0.3f), new Color(0.5f, 0.5f, 0.5f));

            // Movable ceiling (starts high)
            // NOTE: This is a static ceiling for visual representation.
            // Actual moving ceiling would require a runtime system.
            var ceiling = CreateBox(trap, "Ceiling_Movable", new Vector3(0f, 2.5f, 0f),
                new Vector3(5.8f, 0.3f, 5.8f), new Color(0.7f, 0.5f, 0.3f));
            CreateHeightLabel(ceiling, "CEILING\n(Would lower to 1.2m)", new Vector3(0f, 0.8f, 0f));

            // Exit door
            CreateBox(trap, "ExitDoor_Frame", new Vector3(0f, 1f, 3.2f),
                new Vector3(1.5f, 2f, 0.1f), new Color(0.3f, 0.6f, 0.3f));
            CreateHeightLabel(trap, "EXIT\n(After reset)", new Vector3(0f, 2.5f, 3.2f));
        }

        // 13.15.T5: Collider Visualization Chamber
        private static void CreateColliderVisualizationChamber(GameObject parent, Vector3 localPos)
        {
            GameObject chamber = new GameObject("ColliderVisualizationChamber");
            chamber.transform.parent = parent.transform;
            chamber.transform.localPosition = localPos;

            CreateHeightLabel(chamber, "COLLIDER VISUALIZATION\n(Height markers)", new Vector3(0f, 5f, 0f));

            // Floor
            CreateBox(chamber, "Floor", new Vector3(0f, -0.1f, 0f),
                new Vector3(8f, 0.2f, 8f), new Color(0.35f, 0.35f, 0.4f));

            // Standing height marker (1.8m)
            var standingMarker = CreateBox(chamber, "StandingHeightMarker", new Vector3(-2f, 0.9f, 0f),
                new Vector3(0.1f, 1.8f, 0.1f), new Color(0.2f, 0.8f, 0.2f));
            CreateHeightLabel(standingMarker, "STANDING\n1.8m", new Vector3(0f, 1.2f, 0f));

            // Crouching height marker (1.0m)
            var crouchMarker = CreateBox(chamber, "CrouchingHeightMarker", new Vector3(0f, 0.5f, 0f),
                new Vector3(0.1f, 1.0f, 0.1f), new Color(0.8f, 0.8f, 0.2f));
            CreateHeightLabel(crouchMarker, "CROUCHING\n1.0m", new Vector3(0f, 0.8f, 0f));

            // Prone height marker (0.4m)
            var proneMarker = CreateBox(chamber, "ProneHeightMarker", new Vector3(2f, 0.2f, 0f),
                new Vector3(0.1f, 0.4f, 0.1f), new Color(0.8f, 0.4f, 0.2f));
            CreateHeightLabel(proneMarker, "PRONE\n0.4m", new Vector3(0f, 0.5f, 0f));

            // Reference lines at key heights
            // 1.8m line
            CreateBox(chamber, "Line_1.8m", new Vector3(0f, 1.8f, -3f),
                new Vector3(6f, 0.02f, 0.1f), new Color(0.2f, 0.8f, 0.2f));
            // 1.0m line
            CreateBox(chamber, "Line_1.0m", new Vector3(0f, 1.0f, -3f),
                new Vector3(6f, 0.02f, 0.1f), new Color(0.8f, 0.8f, 0.2f));
            // 0.4m line
            CreateBox(chamber, "Line_0.4m", new Vector3(0f, 0.4f, -3f),
                new Vector3(6f, 0.02f, 0.1f), new Color(0.8f, 0.4f, 0.2f));

            // Test ceiling at 1.5m (blocks standing, allows crouch)
            var testCeiling = CreateBox(chamber, "TestCeiling_1.5m", new Vector3(0f, 1.5f, 3f),
                new Vector3(4f, 0.1f, 3f), new Color(0.6f, 0.3f, 0.3f));
            CreateHeightLabel(testCeiling, "1.5m CEILING\n(Crouch only)", new Vector3(0f, 0.5f, 0f));
        }
        
        #endregion
        
        #region Utilities
        
        private static Vector3 GetSceneViewPosition()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                return SceneView.lastActiveSceneView.camera.transform.position +
                       SceneView.lastActiveSceneView.camera.transform.forward * 5f;
            }
            return Vector3.zero;
        }
        
        /// <summary>
        /// Sets up ClimbableObjectAuthoring with proper BottomPoint and TopPoint transforms.
        /// This is required for the climbing detection system to work properly.
        /// </summary>
        private static void SetupClimbableAuthoring(GameObject obj, Vector3 bottomOffset, Vector3 topOffset, ClimbableType type = ClimbableType.RockWall, float interactionRadius = 2.0f)
        {
            var climbable = obj.AddComponent<ClimbableObjectAuthoring>();
            climbable.Type = type;
            climbable.InteractionRadius = interactionRadius;
            climbable.ClimbSpeed = 2.0f;
            
            // Create bottom point
            GameObject bottomPoint = new GameObject("BottomPoint");
            bottomPoint.transform.SetParent(obj.transform, false);
            bottomPoint.transform.localPosition = bottomOffset;
            climbable.BottomPoint = bottomPoint.transform;
            
            // Create top point
            GameObject topPoint = new GameObject("TopPoint");
            topPoint.transform.SetParent(obj.transform, false);
            topPoint.transform.localPosition = topOffset;
            climbable.TopPoint = topPoint.transform;
        }
        
        #endregion
    }
}
