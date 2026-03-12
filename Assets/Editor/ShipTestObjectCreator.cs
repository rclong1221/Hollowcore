using UnityEngine;
using UnityEditor;
using DIG.Ship.LocalSpace;
using DIG.Ship.Stations;
using DIG.Ship.Airlocks;
using DIG.Ship.Power;
using DIG.Survival.Environment;
using DIG.Survival.Authoring;
using DIG.Swimming.Authoring;
using DIG.Ship.Cargo.Authoring;
using Unity.NetCode;
using ZoneShapeType = DIG.Survival.Environment.ZoneShapeType;

namespace DIG.Editor
{
    /// <summary>
    /// Editor utility for creating test ship objects with all required components.
    /// Menu: GameObject > DIG - Test Objects > Ships
    /// </summary>
    public static class ShipTestObjectCreator
    {
        private const string MENU_PATH = "GameObject/DIG - Test Objects/Ships/";

        
        // Colors

        private static readonly Color HullColor = new Color(0.3f, 0.35f, 0.4f);
        private static readonly Color FloorColor = new Color(0.4f, 0.4f, 0.35f);
        private static readonly Color StationColor = new Color(0.2f, 0.5f, 0.7f);
        private static readonly Color AirlockColor = new Color(0.7f, 0.3f, 0.2f);
        private static readonly Color DoorColor = new Color(0.5f, 0.5f, 0.55f);

        [MenuItem(MENU_PATH + "Complete Test Ship", priority = 0)]
        public static void CreateCompleteTestShip()
        {
            var ship = CreateShipWithComponents("TestShip", 1);
            Selection.activeGameObject = ship;
            Undo.RegisterCreatedObjectUndo(ship, "Create Test Ship");
        }

        [MenuItem(MENU_PATH + "Basic Ship (No Stations)", priority = 1)]
        public static void CreateBasicShip()
        {
            var ship = CreateBasicShipOnly("BasicShip", 2);
            Selection.activeGameObject = ship;
            Undo.RegisterCreatedObjectUndo(ship, "Create Basic Ship");
        }

        [MenuItem(MENU_PATH + "Ship Hull Only", priority = 2)]
        public static void CreateShipHullOnly()
        {
            var ship = CreateHullOnly("ShipHull", 3);
            Selection.activeGameObject = ship;
            Undo.RegisterCreatedObjectUndo(ship, "Create Ship Hull");
        }



        /// <summary>
        /// Creates a complete test ship with all systems.
        /// </summary>
        private static GameObject CreateShipWithComponents(string name, int shipId)
        {
            Vector3 position = GetSceneViewPosition();

            // Root
            var root = new GameObject(name);
            root.transform.position = position;

            // Add ShipRootAuthoring
            var shipAuthoring = root.AddComponent<ShipRootAuthoring>();
            shipAuthoring.ShipId = shipId;
            shipAuthoring.ShipName = name;
            shipAuthoring.MaxLinearSpeed = 30f;
            shipAuthoring.MaxAngularSpeed = 1.5f;

            // Add Ship Cargo
            var cargoAuthoring = root.AddComponent<ShipCargoAuthoring>();
            cargoAuthoring.MaxWeight = 1000f;

            // Add Networking
            var ghostAuth = root.AddComponent<GhostAuthoringComponent>();

            // Hull
            CreateHull(root.transform);

            // Interior (with environment zone)
            var interior = CreateInterior(root.transform);

            // Interior Environment Zone (pressurized by default)
            var interiorZone = CreateInteriorZone(root.transform);

            // Power Producer (Reactor)
            CreatePowerProducer(interior.transform, new Vector3(-2f, 0, -1f));

            // Life Support System
            CreateLifeSupport(interior.transform, new Vector3(2f, 0, -1f), interiorZone);

            // Helm Station (StableId = 1)
            CreateHelmStation(interior.transform, new Vector3(0, 0, 3f), 1);

            // Drill Station (StableId = 2)
            CreateDrillStation(interior.transform, new Vector3(-2f, 0, 1f), 2);

            // Systems Panel (StableId = 3)
            CreateSystemsPanel(interior.transform, new Vector3(2f, 0, 1f), 3);

            // Cargo Terminal (StableId = 10)
            CreateCargoTerminal(interior.transform, new Vector3(0, 0, -2f), 10);

            // Airlock (StableId = 1)
            CreateAirlock(root.transform, new Vector3(0, 0, -4f), 1);

            return root;
        }

        /// <summary>
        /// Creates a basic ship without stations.
        /// </summary>
        private static GameObject CreateBasicShipOnly(string name, int shipId)
        {
            Vector3 position = GetSceneViewPosition();

            var root = new GameObject(name);
            root.transform.position = position;

            var shipAuthoring = root.AddComponent<ShipRootAuthoring>();
            shipAuthoring.ShipId = shipId;
            shipAuthoring.ShipName = name;

            var ghostAuth = root.AddComponent<GhostAuthoringComponent>();

            CreateHull(root.transform);
            CreateInterior(root.transform);

            return root;
        }

        /// <summary>
        /// Creates just the hull for testing.
        /// </summary>
        private static GameObject CreateHullOnly(string name, int shipId)
        {
            Vector3 position = GetSceneViewPosition();

            var root = new GameObject(name);
            root.transform.position = position;

            var shipAuthoring = root.AddComponent<ShipRootAuthoring>();
            shipAuthoring.ShipId = shipId;
            shipAuthoring.ShipName = name;

            var ghostAuth = root.AddComponent<GhostAuthoringComponent>();

            CreateHull(root.transform);

            return root;
        }

        /// <summary>
        /// Creates the ship hull (exterior walls).
        /// </summary>
        private static GameObject CreateHull(Transform parent)
        {
            var hull = new GameObject("Hull");
            hull.transform.SetParent(parent);
            hull.transform.localPosition = Vector3.zero;

            // Floor
            var floor = CreatePrimitive(PrimitiveType.Cube, "Floor", hull.transform);
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(6f, 0.2f, 10f);
            SetMaterialColor(floor, FloorColor);
            AddHullSection(floor, 500f);

            // Left wall
            var leftWall = CreatePrimitive(PrimitiveType.Cube, "LeftWall", hull.transform);
            leftWall.transform.localPosition = new Vector3(-3f, 1.5f, 0);
            leftWall.transform.localScale = new Vector3(0.2f, 3f, 10f);
            SetMaterialColor(leftWall, HullColor);
            AddHullSection(leftWall, 250f);

            // Right wall
            var rightWall = CreatePrimitive(PrimitiveType.Cube, "RightWall", hull.transform);
            rightWall.transform.localPosition = new Vector3(3f, 1.5f, 0);
            rightWall.transform.localScale = new Vector3(0.2f, 3f, 10f);
            SetMaterialColor(rightWall, HullColor);
            AddHullSection(rightWall, 250f);

            // Front wall
            var frontWall = CreatePrimitive(PrimitiveType.Cube, "FrontWall", hull.transform);
            frontWall.transform.localPosition = new Vector3(0, 1.5f, 5f);
            frontWall.transform.localScale = new Vector3(6f, 3f, 0.2f);
            SetMaterialColor(frontWall, HullColor);
            AddHullSection(frontWall, 200f);

            // Back wall (with cutout for airlock - represented as two pieces)
            var backWallLeft = CreatePrimitive(PrimitiveType.Cube, "BackWallLeft", hull.transform);
            backWallLeft.transform.localPosition = new Vector3(-1.75f, 1.5f, -5f);
            backWallLeft.transform.localScale = new Vector3(2.5f, 3f, 0.2f);
            SetMaterialColor(backWallLeft, HullColor);
            AddHullSection(backWallLeft, 100f);

            var backWallRight = CreatePrimitive(PrimitiveType.Cube, "BackWallRight", hull.transform);
            backWallRight.transform.localPosition = new Vector3(1.75f, 1.5f, -5f);
            backWallRight.transform.localScale = new Vector3(2.5f, 3f, 0.2f);
            SetMaterialColor(backWallRight, HullColor);
            AddHullSection(backWallRight, 100f);

            var backWallTop = CreatePrimitive(PrimitiveType.Cube, "BackWallTop", hull.transform);
            backWallTop.transform.localPosition = new Vector3(0, 2.75f, -5f);
            backWallTop.transform.localScale = new Vector3(1f, 0.5f, 0.2f);
            SetMaterialColor(backWallTop, HullColor);
            AddHullSection(backWallTop, 50f);

            // Ceiling
            var ceiling = CreatePrimitive(PrimitiveType.Cube, "Ceiling", hull.transform);
            ceiling.transform.localPosition = new Vector3(0, 3f, 0);
            ceiling.transform.localScale = new Vector3(6f, 0.2f, 10f);
            SetMaterialColor(ceiling, HullColor);
            AddHullSection(ceiling, 500f);

            return hull;
        }

        private static void AddHullSection(GameObject go, float health)
        {
            var authoring = go.AddComponent<DIG.Ship.Hull.Authoring.HullSectionAuthoring>();
            authoring.MaxHealth = health;
            authoring.StartFullHealth = true;
        }

        /// <summary>
        /// Creates interior elements.
        /// </summary>
        private static GameObject CreateInterior(Transform parent)
        {
            var interior = new GameObject("Interior");
            interior.transform.SetParent(parent);
            interior.transform.localPosition = Vector3.zero;

            // Console (decorative)
            var console = CreatePrimitive(PrimitiveType.Cube, "Console", interior.transform);
            console.transform.localPosition = new Vector3(0, 0.5f, 4f);
            console.transform.localScale = new Vector3(4f, 1f, 0.5f);
            SetMaterialColor(console, new Color(0.2f, 0.2f, 0.25f));

            // Side consoles
            var leftConsole = CreatePrimitive(PrimitiveType.Cube, "LeftConsole", interior.transform);
            leftConsole.transform.localPosition = new Vector3(-2.5f, 0.4f, 0);
            leftConsole.transform.localScale = new Vector3(0.5f, 0.8f, 4f);
            SetMaterialColor(leftConsole, new Color(0.25f, 0.25f, 0.3f));

            var rightConsole = CreatePrimitive(PrimitiveType.Cube, "RightConsole", interior.transform);
            rightConsole.transform.localPosition = new Vector3(2.5f, 0.4f, 0);
            rightConsole.transform.localScale = new Vector3(0.5f, 0.8f, 4f);
            SetMaterialColor(rightConsole, new Color(0.25f, 0.25f, 0.3f));

            return interior;
        }

        /// <summary>
        /// Creates a helm station.
        /// </summary>
        private static GameObject CreateHelmStation(Transform parent, Vector3 localPosition, int stableId)
        {
            var station = new GameObject("Helm");
            station.transform.SetParent(parent);
            station.transform.localPosition = localPosition;

            // Visual
            var visual = CreatePrimitive(PrimitiveType.Cylinder, "Visual", station.transform);
            visual.transform.localPosition = new Vector3(0, 0.25f, 0);
            visual.transform.localScale = new Vector3(0.8f, 0.25f, 0.8f);
            SetMaterialColor(visual, StationColor);

            // Seat
            var seat = CreatePrimitive(PrimitiveType.Cube, "Seat", station.transform);
            seat.transform.localPosition = new Vector3(0, 0.3f, -0.5f);
            seat.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            SetMaterialColor(seat, new Color(0.3f, 0.3f, 0.35f));

            // Interaction point
            var interactionPoint = new GameObject("InteractionPoint");
            interactionPoint.transform.SetParent(station.transform);
            interactionPoint.transform.localPosition = new Vector3(0, 0, -0.7f);
            interactionPoint.transform.localRotation = Quaternion.identity;

            // Add StationAuthoring
            var authoring = station.AddComponent<StationAuthoring>();
            authoring.Type = StationType.Helm;
            authoring.InteractionPoint = interactionPoint.transform;
            authoring.InteractionRange = 2f;
            authoring.PromptEnter = "Press E: Pilot Ship";
            authoring.PromptExit = "Press E: Exit Helm";
            authoring.StableId = stableId;

            return station;
        }

        /// <summary>
        /// Creates a drill control station.
        /// </summary>
        private static GameObject CreateDrillStation(Transform parent, Vector3 localPosition, int stableId)
        {
            var station = new GameObject("DrillControl");
            station.transform.SetParent(parent);
            station.transform.localPosition = localPosition;

            // Visual - control panel
            var panel = CreatePrimitive(PrimitiveType.Cube, "Panel", station.transform);
            panel.transform.localPosition = new Vector3(0, 0.75f, 0);
            panel.transform.localScale = new Vector3(1f, 1.5f, 0.2f);
            panel.transform.localRotation = Quaternion.Euler(0, 90, 0);
            SetMaterialColor(panel, new Color(0.4f, 0.5f, 0.3f));

            // Interaction point
            var interactionPoint = new GameObject("InteractionPoint");
            interactionPoint.transform.SetParent(station.transform);
            interactionPoint.transform.localPosition = new Vector3(0.5f, 0, 0);
            interactionPoint.transform.localRotation = Quaternion.Euler(0, -90, 0);

            // Add StationAuthoring
            var authoring = station.AddComponent<StationAuthoring>();
            authoring.Type = StationType.DrillControl;
            authoring.InteractionPoint = interactionPoint.transform;
            authoring.InteractionRange = 2f;
            authoring.PromptEnter = "Press E: Control Drill";
            authoring.PromptExit = "Press E: Exit Controls";
            authoring.StableId = stableId;

            return station;
        }

        /// <summary>
        /// Creates a systems panel station.
        /// </summary>
        private static GameObject CreateSystemsPanel(Transform parent, Vector3 localPosition, int stableId)
        {
            var station = new GameObject("SystemsPanel");
            station.transform.SetParent(parent);
            station.transform.localPosition = localPosition;

            // Visual - panel on wall
            var panel = CreatePrimitive(PrimitiveType.Cube, "Panel", station.transform);
            panel.transform.localPosition = new Vector3(0, 0.75f, 0);
            panel.transform.localScale = new Vector3(1f, 1.5f, 0.1f);
            panel.transform.localRotation = Quaternion.Euler(0, -90, 0);
            SetMaterialColor(panel, new Color(0.5f, 0.4f, 0.3f));

            // Buttons (decorative)
            for (int i = 0; i < 4; i++)
            {
                var button = CreatePrimitive(PrimitiveType.Cube, $"Button_{i}", panel.transform);
                button.transform.localPosition = new Vector3((i - 1.5f) * 0.2f, 0.1f, -0.6f);
                button.transform.localScale = new Vector3(0.15f, 0.05f, 0.15f);
                SetMaterialColor(button, i == 0 ? Color.green : Color.red);
            }

            // Interaction point
            var interactionPoint = new GameObject("InteractionPoint");
            interactionPoint.transform.SetParent(station.transform);
            interactionPoint.transform.localPosition = new Vector3(-0.5f, 0, 0);
            interactionPoint.transform.localRotation = Quaternion.Euler(0, 90, 0);

            // Add StationAuthoring
            var authoring = station.AddComponent<StationAuthoring>();
            authoring.Type = StationType.SystemsPanel;
            authoring.InteractionPoint = interactionPoint.transform;
            authoring.InteractionRange = 2f;
            authoring.PromptEnter = "Press E: Access Systems";
            authoring.PromptExit = "Press E: Close Panel";
            authoring.StableId = stableId;

            return station;
        }

        /// <summary>
        /// Creates a cargo terminal.
        /// </summary>
        private static GameObject CreateCargoTerminal(Transform parent, Vector3 localPosition, int stableId)
        {
            var terminal = new GameObject("CargoTerminal");
            terminal.transform.SetParent(parent);
            terminal.transform.localPosition = localPosition;

            // Visual - cargo console
            var console = CreatePrimitive(PrimitiveType.Cube, "Console", terminal.transform);
            console.transform.localPosition = new Vector3(0, 0.5f, 0);
            console.transform.localScale = new Vector3(1.2f, 1f, 0.4f);
            SetMaterialColor(console, new Color(0.7f, 0.5f, 0.2f));

            // Screen
            var screen = CreatePrimitive(PrimitiveType.Cube, "Screen", terminal.transform);
            screen.transform.localPosition = new Vector3(0, 1.1f, -0.15f);
            screen.transform.localScale = new Vector3(0.8f, 0.4f, 0.05f);
            SetMaterialColor(screen, new Color(0.1f, 0.3f, 0.5f));

            // Cargo icon (decorative boxes)
            for (int i = 0; i < 3; i++)
            {
                var box = CreatePrimitive(PrimitiveType.Cube, $"CargoBox_{i}", terminal.transform);
                box.transform.localPosition = new Vector3(-0.8f + i * 0.4f, 0.15f, 0.5f);
                box.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                SetMaterialColor(box, new Color(0.5f + i * 0.1f, 0.4f, 0.2f));
            }

            // Add CargoTerminalAuthoring
            var authoring = terminal.AddComponent<DIG.Ship.Cargo.CargoTerminalAuthoring>();
            authoring.InteractionRange = 2.5f;
            authoring.PromptText = "Press E: Access Cargo";
            authoring.StableId = stableId;

            return terminal;
        }

        /// <summary>
        /// Creates an airlock.
        /// </summary>
        private static GameObject CreateAirlock(Transform parent, Vector3 localPosition, int stableId)
        {
            var airlock = new GameObject("Airlock");
            airlock.transform.SetParent(parent);
            airlock.transform.localPosition = localPosition;

            // Airlock chamber
            var chamber = new GameObject("Chamber");
            chamber.transform.SetParent(airlock.transform);
            chamber.transform.localPosition = Vector3.zero;

            // Floor
            var floor = CreatePrimitive(PrimitiveType.Cube, "Floor", chamber.transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(2f, 0.1f, 2f);
            SetMaterialColor(floor, AirlockColor);

            // Left wall
            var leftWall = CreatePrimitive(PrimitiveType.Cube, "LeftWall", chamber.transform);
            leftWall.transform.localPosition = new Vector3(-1f, 1.25f, 0);
            leftWall.transform.localScale = new Vector3(0.1f, 2.5f, 2f);
            SetMaterialColor(leftWall, HullColor);

            // Right wall
            var rightWall = CreatePrimitive(PrimitiveType.Cube, "RightWall", chamber.transform);
            rightWall.transform.localPosition = new Vector3(1f, 1.25f, 0);
            rightWall.transform.localScale = new Vector3(0.1f, 2.5f, 2f);
            SetMaterialColor(rightWall, HullColor);

            // Ceiling
            var ceiling = CreatePrimitive(PrimitiveType.Cube, "Ceiling", chamber.transform);
            ceiling.transform.localPosition = new Vector3(0, 2.5f, 0);
            ceiling.transform.localScale = new Vector3(2f, 0.1f, 2f);
            SetMaterialColor(ceiling, HullColor);

            // Interior door (towards ship interior)
            var interiorDoor = CreateAirlockDoor(airlock.transform, "InteriorDoor", new Vector3(0, 0, 1f), DoorSide.Interior);
            interiorDoor.transform.localRotation = Quaternion.Euler(0, 180, 0);

            // Exterior door (towards space)
            var exteriorDoor = CreateAirlockDoor(airlock.transform, "ExteriorDoor", new Vector3(0, 0, -1f), DoorSide.Exterior);

            // Spawn points
            var interiorSpawn = new GameObject("InteriorSpawnPoint");
            interiorSpawn.transform.SetParent(airlock.transform);
            interiorSpawn.transform.localPosition = new Vector3(0, 0, 2f);
            interiorSpawn.transform.localRotation = Quaternion.Euler(0, 180, 0);

            var exteriorSpawn = new GameObject("ExteriorSpawnPoint");
            exteriorSpawn.transform.SetParent(airlock.transform);
            exteriorSpawn.transform.localPosition = new Vector3(0, 0, -2f);

            // Interior interaction point
            var interiorInteract = new GameObject("InteriorInteractionPoint");
            interiorInteract.transform.SetParent(airlock.transform);
            interiorInteract.transform.localPosition = new Vector3(0, 0, 1.5f);

            // Exterior interaction point
            var exteriorInteract = new GameObject("ExteriorInteractionPoint");
            exteriorInteract.transform.SetParent(airlock.transform);
            exteriorInteract.transform.localPosition = new Vector3(0, 0, -1.5f);

            // Add AirlockAuthoring
            var authoring = airlock.AddComponent<AirlockAuthoring>();
            authoring.InteriorDoor = interiorDoor;
            authoring.ExteriorDoor = exteriorDoor;
            authoring.InteriorSpawnPoint = interiorSpawn.transform;
            authoring.ExteriorSpawnPoint = exteriorSpawn.transform;
            authoring.InteractionRange = 2f;
            authoring.CycleTime = 3f;
            authoring.StableId = stableId;

            // Set ParentAirlock references on doors (must be done after authoring is added)
            interiorDoor.GetComponent<AirlockDoorAuthoring>().ParentAirlock = authoring;
            exteriorDoor.GetComponent<AirlockDoorAuthoring>().ParentAirlock = authoring;

            return airlock;
        }

        /// <summary>
        /// Creates an airlock door.
        /// </summary>
        private static GameObject CreateAirlockDoor(Transform parent, string name, Vector3 localPosition, DoorSide doorSide)
        {
            var door = new GameObject(name);
            door.transform.SetParent(parent);
            door.transform.localPosition = localPosition;

            // Door panels (sliding)
            var leftPanel = CreatePrimitive(PrimitiveType.Cube, "LeftPanel", door.transform);
            leftPanel.transform.localPosition = new Vector3(-0.5f, 1.25f, 0);
            leftPanel.transform.localScale = new Vector3(1f, 2.5f, 0.1f);
            SetMaterialColor(leftPanel, DoorColor);

            var rightPanel = CreatePrimitive(PrimitiveType.Cube, "RightPanel", door.transform);
            rightPanel.transform.localPosition = new Vector3(0.5f, 1.25f, 0);
            rightPanel.transform.localScale = new Vector3(1f, 2.5f, 0.1f);
            SetMaterialColor(rightPanel, DoorColor);

            // Add door authoring
            var doorAuthoring = door.AddComponent<AirlockDoorAuthoring>();
            doorAuthoring.DoorSide = doorSide;
            doorAuthoring.AnimationType = DoorAnimationType.Slide;
            doorAuthoring.OpenDirection = Vector3.right;
            doorAuthoring.OpenDistance = 1f;
            doorAuthoring.AnimationSpeed = 5f;

            return door;
        }

        /// <summary>
        /// Creates a primitive with mesh collider removed and standard material.
        /// </summary>
        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);

            return obj;
        }

        /// <summary>
        /// Sets the material color of an object by getting or creating a shared material asset.
        /// </summary>
        private static void SetMaterialColor(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;

            // Generate a name based on color hash to reuse materials
            string matName = $"ShipMat_{color.GetHashCode():X}";
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

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Generated", "Materials");

            // Check if asset exists
            Material material = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
            if (material == null)
            {
                // Create new material
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                material = new Material(shader);
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);

                AssetDatabase.CreateAsset(material, fullPath);
            }
            else
            {
                // Ensure color is correct (in case of hash collision or update)
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>
        /// Gets a position in front of the scene view camera.
        /// </summary>
        private static Vector3 GetSceneViewPosition()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                var cam = SceneView.lastActiveSceneView.camera;
                return cam.transform.position + cam.transform.forward * 15f;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Creates the interior environment zone for the ship.
        /// Note: EnvironmentZoneAuthoring now defines its shape directly - no Unity Collider needed!
        /// </summary>
        private static GameObject CreateInteriorZone(Transform parent)
        {
            var zone = new GameObject("InteriorZone");
            zone.transform.SetParent(parent);
            zone.transform.localPosition = new Vector3(0, 1.5f, 0);

            // NOTE: Do NOT add a BoxCollider! 
            // EnvironmentZoneAuthoring defines the trigger shape directly to avoid 
            // conflicts with Unity Physics' built-in bakers.

            // Add environment zone authoring with shape defined directly
            var zoneAuth = zone.AddComponent<EnvironmentZoneAuthoring>();
            zoneAuth.ZoneType = EnvironmentZoneType.Pressurized;
            zoneAuth.OxygenRequired = false;
            zoneAuth.OxygenDepletionMultiplier = 0f;
            zoneAuth.Temperature = 20f;
            zoneAuth.RadiationRate = 0f;
            
            // Define the trigger shape directly (no Unity Collider needed)
            zoneAuth.Shape = ZoneShapeType.Box;
            zoneAuth.BoxSize = new Vector3(5.5f, 2.5f, 9.5f); // Slightly smaller than interior
            zoneAuth.Center = Vector3.zero;

            return zone;
        }

        /// <summary>
        /// Creates a power producer (reactor) for the ship.
        /// </summary>
        private static GameObject CreatePowerProducer(Transform parent, Vector3 localPosition)
        {
            var reactor = new GameObject("Reactor");
            reactor.transform.SetParent(parent);
            reactor.transform.localPosition = localPosition;

            // Visual - reactor core
            var core = CreatePrimitive(PrimitiveType.Cylinder, "Core", reactor.transform);
            core.transform.localPosition = new Vector3(0, 0.4f, 0);
            core.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
            SetMaterialColor(core, new Color(0.2f, 0.8f, 0.3f)); // Green glow

            // Base
            var baseObj = CreatePrimitive(PrimitiveType.Cube, "Base", reactor.transform);
            baseObj.transform.localPosition = Vector3.zero;
            baseObj.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
            SetMaterialColor(baseObj, new Color(0.3f, 0.3f, 0.35f));

            // Add power producer authoring
            var producer = reactor.AddComponent<PowerProducerAuthoring>();
            producer.MaxOutput = 100f;
            producer.StartOnline = true;

            return reactor;
        }

        /// <summary>
        /// Creates a life support system for the ship.
        /// </summary>
        private static GameObject CreateLifeSupport(Transform parent, Vector3 localPosition, GameObject interiorZone)
        {
            var lifeSupport = new GameObject("LifeSupport");
            lifeSupport.transform.SetParent(parent);
            lifeSupport.transform.localPosition = localPosition;

            // Visual - life support unit
            var unit = CreatePrimitive(PrimitiveType.Cube, "Unit", lifeSupport.transform);
            unit.transform.localPosition = new Vector3(0, 0.5f, 0);
            unit.transform.localScale = new Vector3(0.8f, 1f, 0.4f);
            SetMaterialColor(unit, new Color(0.2f, 0.5f, 0.8f)); // Blue

            // Vents
            var vent1 = CreatePrimitive(PrimitiveType.Cube, "Vent1", lifeSupport.transform);
            vent1.transform.localPosition = new Vector3(0, 0.8f, 0.25f);
            vent1.transform.localScale = new Vector3(0.6f, 0.3f, 0.1f);
            SetMaterialColor(vent1, new Color(0.15f, 0.15f, 0.2f));

            var vent2 = CreatePrimitive(PrimitiveType.Cube, "Vent2", lifeSupport.transform);
            vent2.transform.localPosition = new Vector3(0, 0.4f, 0.25f);
            vent2.transform.localScale = new Vector3(0.6f, 0.3f, 0.1f);
            SetMaterialColor(vent2, new Color(0.15f, 0.15f, 0.2f));

            // Add life support authoring
            var lsAuth = lifeSupport.AddComponent<LifeSupportAuthoring>();
            lsAuth.PowerRequired = 50f;
            lsAuth.OxygenGenerationRate = 1f;
            lsAuth.StartOnline = true;
            lsAuth.InteriorZone = interiorZone;

            return lifeSupport;
        }
    }
}

