using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using Player.Authoring;
using Player.Bridges;

namespace DIG.Editor
{
    /// <summary>
    /// Editor utility to create Blitz mount prefabs with proper Client/Server ghost setup.
    /// - Server: GhostAuthoringComponent + RideableAuthoring + physics
    /// - Client: Visual presentation only (no ghost)
    /// </summary>
    public class BlitzPrefabCreator : EditorWindow
    {
        // Asset paths
        private const string BLITZ_MODEL = "Assets/Art/Models/Characters/Blitz/Blitz.fbx";
        private const string BLITZ_CONTROLLER = "Assets/Art/Animations/Opsive/Animator/Characters/BlitzDemo.controller";
        
        // Output paths
        private const string PREFAB_OUTPUT_FOLDER = "Assets/Prefabs/Characters";
        private const string CLIENT_PREFAB_NAME = "Blitz_Client.prefab";
        private const string SERVER_PREFAB_NAME = "Blitz_Server.prefab";
        
        // Rideable settings
        private float interactionRadius = 2f;
        private Vector3 mountOffsetLeft = new Vector3(-1f, 0f, 0f);
        private Vector3 mountOffsetRight = new Vector3(1f, 0f, 0f);
        private Vector3 seatOffset = new Vector3(0f, 1.5f, 0f);
        
        // Physics settings
        private float mass = 500f;
        private float drag = 1f;
        private float colliderHeight = 2f;
        private float colliderRadius = 0.5f;
        
        // Movement settings
        private float walkSpeed = 5f;
        private float runSpeed = 12f;
        private float turnSpeed = 120f;
        
        [MenuItem("DIG/Create Blitz Prefabs", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<BlitzPrefabCreator>("Blitz Prefab Creator");
            window.minSize = new Vector2(450, 600);
        }
        
        void OnGUI()
        {
            GUILayout.Label("Blitz Horse Mount Prefab Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Creates properly configured Client/Server ghost prefabs:\n\n" +
                "• Blitz_Server: Ghost + RideableAuthoring + Physics\n" +
                "• Blitz_Client: Visual presentation only",
                MessageType.Info);
            
            EditorGUILayout.Space();
            
            // Check for required assets
            bool modelExists = File.Exists(BLITZ_MODEL);
            bool controllerExists = File.Exists(BLITZ_CONTROLLER);
            
            EditorGUILayout.LabelField("Required Assets:", EditorStyles.boldLabel);
            DrawAssetStatus("Blitz.fbx", BLITZ_MODEL, modelExists);
            DrawAssetStatus("BlitzDemo.controller", BLITZ_CONTROLLER, controllerExists);
            
            EditorGUILayout.Space();
            
            if (!modelExists)
            {
                EditorGUILayout.HelpBox(
                    "Blitz.fbx not found!\n\n" +
                    "Copy from OPSIVE folder to:\n" + BLITZ_MODEL,
                    MessageType.Error);
                
                if (GUILayout.Button("Copy Blitz Assets from OPSIVE"))
                {
                    CopyBlitzAssets();
                }
                EditorGUILayout.Space();
            }
            
            EditorGUILayout.Space();
            GUILayout.Label("Rideable Settings", EditorStyles.boldLabel);
            
            interactionRadius = EditorGUILayout.FloatField("Interaction Radius", interactionRadius);
            mountOffsetLeft = EditorGUILayout.Vector3Field("Mount Offset Left", mountOffsetLeft);
            mountOffsetRight = EditorGUILayout.Vector3Field("Mount Offset Right", mountOffsetRight);
            seatOffset = EditorGUILayout.Vector3Field("Seat Offset", seatOffset);
            
            EditorGUILayout.Space();
            GUILayout.Label("Movement Settings", EditorStyles.boldLabel);
            
            walkSpeed = EditorGUILayout.FloatField("Walk Speed (trot)", walkSpeed);
            runSpeed = EditorGUILayout.FloatField("Run Speed (gallop, Shift)", runSpeed);
            turnSpeed = EditorGUILayout.FloatField("Turn Speed (deg/sec)", turnSpeed);
            
            EditorGUILayout.Space();
            GUILayout.Label("Physics Settings (Server only)", EditorStyles.boldLabel);
            
            mass = EditorGUILayout.FloatField("Mass", mass);
            drag = EditorGUILayout.FloatField("Drag", drag);
            colliderHeight = EditorGUILayout.FloatField("Collider Height", colliderHeight);
            colliderRadius = EditorGUILayout.FloatField("Collider Radius", colliderRadius);
            
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            
            GUI.enabled = modelExists;
            if (GUILayout.Button("Create Blitz Prefabs", GUILayout.Height(40)))
            {
                CreateBlitzPrefabs();
            }
            GUI.enabled = true;
            
            EditorGUILayout.Space();
            
            // Status
            string clientPath = Path.Combine(PREFAB_OUTPUT_FOLDER, CLIENT_PREFAB_NAME);
            string serverPath = Path.Combine(PREFAB_OUTPUT_FOLDER, SERVER_PREFAB_NAME);
            
            EditorGUILayout.LabelField("Prefab Status:", EditorStyles.boldLabel);
            DrawAssetStatus("Blitz_Client (Visual)", clientPath, File.Exists(clientPath));
            DrawAssetStatus("Blitz_Server (Ghost)", serverPath, File.Exists(serverPath));
        }
        
        void DrawAssetStatus(string label, string path, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(exists ? "✓" : "✗", GUILayout.Width(20));
            EditorGUILayout.LabelField(label);
            GUI.enabled = exists;
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
        
        void CopyBlitzAssets()
        {
            string opsiveBase = "Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo";
            
            CreateFolderIfNeeded("Assets/Art/Models/Characters/Blitz");
            CreateFolderIfNeeded("Assets/Art/Materials/Characters/Blitz");
            CreateFolderIfNeeded("Assets/Art/Textures/Characters/Blitz");
            
            string srcModel = $"{opsiveBase}/Models/Characters/Blitz/Blitz.fbx";
            if (File.Exists(srcModel))
            {
                AssetDatabase.CopyAsset(srcModel, BLITZ_MODEL);
                Debug.Log($"Copied: {srcModel} → {BLITZ_MODEL}");
            }
            
            CopyFolderContents($"{opsiveBase}/Materials/Characters/Blitz", "Assets/Art/Materials/Characters/Blitz", "*.mat");
            CopyFolderContents($"{opsiveBase}/Textures/Characters/Blitz", "Assets/Art/Textures/Characters/Blitz", "*.*");
            
            AssetDatabase.Refresh();
            Repaint();
        }
        
        void CopyFolderContents(string src, string dest, string pattern)
        {
            if (!Directory.Exists(src)) return;
            
            string[] files = Directory.GetFiles(src, pattern);
            foreach (string file in files)
            {
                if (file.EndsWith(".meta")) continue;
                string fileName = Path.GetFileName(file);
                string srcPath = file.Replace("\\", "/");
                string destPath = $"{dest}/{fileName}";
                AssetDatabase.CopyAsset(srcPath, destPath);
            }
        }
        
        void CreateFolderIfNeeded(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            
            string[] parts = path.Split('/');
            string current = parts[0];
            
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
        
        void CreateBlitzPrefabs()
        {
            CreateFolderIfNeeded(PREFAB_OUTPUT_FOLDER);
            
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BLITZ_MODEL);
            if (modelAsset == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not load: {BLITZ_MODEL}", "OK");
                return;
            }
            
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BLITZ_CONTROLLER);
            
            // Create CLIENT prefab first (visual only)
            string clientPath = Path.Combine(PREFAB_OUTPUT_FOLDER, CLIENT_PREFAB_NAME);
            GameObject clientPrefab = CreateClientPrefab(modelAsset, controller, clientPath);
            
            // Create SERVER prefab (ghost + logic)
            string serverPath = Path.Combine(PREFAB_OUTPUT_FOLDER, SERVER_PREFAB_NAME);
            CreateServerPrefab(modelAsset, controller, serverPath, clientPrefab);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Success", 
                "Created Blitz prefabs:\n\n" +
                "• Blitz_Client (Visual presentation)\n" +
                "• Blitz_Server (Ghost + RideableAuthoring)\n\n" +
                "IMPORTANT: Don't place Blitz directly in the scene!\n" +
                "Use BlitzSpawnerAuthoring in a SubScene instead.\n" +
                "See SETUP_GUIDE_14.14.md Part 5.5",
                "OK");
            
            var serverAsset = AssetDatabase.LoadAssetAtPath<GameObject>(serverPath);
            if (serverAsset != null)
            {
                Selection.activeObject = serverAsset;
                EditorGUIUtility.PingObject(serverAsset);
            }
        }
        
        GameObject CreateClientPrefab(GameObject modelAsset, RuntimeAnimatorController controller, string savePath)
        {
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            root.name = "Blitz_Client";
            
            try
            {
                // Configure Animator
                Animator animator = root.GetComponent<Animator>();
                if (animator == null)
                    animator = root.AddComponent<Animator>();
                    
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
                
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                
                // Add BlitzAnimatorBridge for visual animation sync (direct type)
                var bridge = root.AddComponent<BlitzAnimatorBridge>();
                bridge.blitzAnimator = animator;
                
                // Delete existing and save
                if (File.Exists(savePath))
                    AssetDatabase.DeleteAsset(savePath);
                
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
                Debug.Log($"Created CLIENT prefab: {savePath}");
                return prefab;
            }
            finally
            {
                DestroyImmediate(root);
            }
        }
        
        void CreateServerPrefab(GameObject modelAsset, RuntimeAnimatorController controller, string savePath, GameObject clientPrefab)
        {
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            root.name = "Blitz_Server";
            
            try
            {
                // Configure Animator
                Animator animator = root.GetComponent<Animator>();
                if (animator == null)
                    animator = root.AddComponent<Animator>();
                    
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
                
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                
                // Add GhostAuthoringComponent (via reflection - same pattern as CharacterPrefabSetupWizard)
                var ghostAuth = AddComponentByName(root, "GhostAuthoringComponent", "Unity.NetCode");
                if (ghostAuth != null)
                {
                    SetFieldValue(ghostAuth, "DefaultGhostMode", 2); // Interpolated
                    SetFieldValue(ghostAuth, "SupportedGhostModes", 3); // All
                    SetFieldValue(ghostAuth, "HasOwner", false);
                    SetFieldValue(ghostAuth, "Importance", 1);
                }
                else
                {
                    Debug.LogError("Failed to add GhostAuthoringComponent!");
                }
                
                // Add GhostPresentationGameObjectAuthoring to link client prefab
                var presentationAuth = AddComponentByName(root, "GhostPresentationGameObjectAuthoring", "Unity.NetCode.Hybrid");
                if (presentationAuth != null && clientPrefab != null)
                {
                    SetFieldValue(presentationAuth, "ClientPrefab", clientPrefab);
                    SetFieldValue(presentationAuth, "ServerPrefab", root);
                }
                
                // Add RideableAuthoring (direct type - it's in our codebase)
                var rideable = root.AddComponent<RideableAuthoring>();
                rideable.canBeRidden = true;
                rideable.interactionRadius = interactionRadius;
                rideable.mountOffsetLeft = mountOffsetLeft;
                rideable.mountOffsetRight = mountOffsetRight;
                rideable.seatOffset = seatOffset;
                rideable.walkSpeed = walkSpeed;
                rideable.runSpeed = runSpeed;
                rideable.turnSpeed = turnSpeed;
                
                // Add BlitzAnimatorBridge (direct type)
                var bridge = root.AddComponent<BlitzAnimatorBridge>();
                bridge.blitzAnimator = animator;
                
                // Add Physics - Kinematic so position is controlled by code only
                Rigidbody rb = root.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.linearDamping = drag;
                rb.useGravity = false;  // Don't fall
                rb.isKinematic = true;  // Position controlled by code, not physics
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                
                CapsuleCollider col = root.AddComponent<CapsuleCollider>();
                col.height = colliderHeight;
                col.radius = colliderRadius;
                col.center = new Vector3(0, colliderHeight / 2f, 0);
                col.direction = 1;
                
                // Create mount point transforms
                CreateChildTransform(root.transform, "MountLeft", new Vector3(-1f, 0f, 0.5f));
                CreateChildTransform(root.transform, "MountRight", new Vector3(1f, 0f, 0.5f));
                CreateChildTransform(root.transform, "SeatPosition", seatOffset);
                
                // Delete existing and save
                if (File.Exists(savePath))
                    AssetDatabase.DeleteAsset(savePath);
                
                PrefabUtility.SaveAsPrefabAsset(root, savePath);
                Debug.Log($"Created SERVER prefab: {savePath}");
            }
            finally
            {
                DestroyImmediate(root);
            }
        }
        
        void CreateChildTransform(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }
        
        #region Reflection Helpers (same pattern as CharacterPrefabSetupWizard)
        
        private Component AddComponentByName(GameObject target, string className, string namespaceName = null)
        {
            string[] assemblies = { "Assembly-CSharp", "Unity.NetCode", "Unity.NetCode.Authoring.Hybrid", "Unity.Entities.Hybrid" };
            string[] namespaces = namespaceName != null 
                ? new[] { namespaceName, "" }
                : new[] { "", "Player", "Player.Authoring", "Player.Bridges" };
            
            foreach (var assembly in assemblies)
            {
                foreach (var ns in namespaces)
                {
                    string fullName = string.IsNullOrEmpty(ns) ? className : $"{ns}.{className}";
                    Type type = Type.GetType($"{fullName}, {assembly}");
                    
                    if (type != null)
                    {
                        var existing = target.GetComponent(type);
                        if (existing != null) return existing;
                        
                        return target.AddComponent(type);
                    }
                }
            }
            
            // Fallback: search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == className && typeof(Component).IsAssignableFrom(type))
                        {
                            var existing = target.GetComponent(type);
                            if (existing != null) return existing;
                            
                            return target.AddComponent(type);
                        }
                    }
                }
                catch
                {
                    // Some assemblies may not be accessible
                }
            }
            
            Debug.LogWarning($"Could not find component type: {className}");
            return null;
        }
        
        private void SetFieldValue(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            
            var type = obj.GetType();
            
            // Try field
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }
            
            // Try property
            var prop = type.GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
            }
        }
        
        #endregion
    }
}
