using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DIG.Editor
{
    /// <summary>
    /// Editor wizard for quickly setting up networked character prefabs from an FBX model.
    /// Creates both Client (presentation) and Server (ECS) prefab variants with all required components.
    /// Menu: Tools > Player > Character Prefab Setup Wizard
    /// </summary>
    public class CharacterPrefabSetupWizard : EditorWindow
    {
        #region Wizard State
        private GameObject sourceFBX;
        private string characterName = "";
        private RuntimeAnimatorController animatorController;
        private string outputFolder = "Assets/Prefabs";
        
        // Component toggles
        private bool foldoutLocomotion = true;
        private bool foldoutAbilities = true;
        private bool foldoutClimbing = true;
        private bool foldoutSwimming = true;
        private bool foldoutSurvival = true;
        private bool foldoutRagdoll = true;
        private bool foldoutAudio = true;
        
        private bool enableJump = true;
        private bool enableCrouch = true;
        private bool enableSprint = true;
        private bool enableRoll = true;
        private bool enableDive = true;
        private bool enableSlide = true;
        private bool enableMantle = true;
        private bool enableClimbing = true;
        private bool enableSwimming = true;
        private bool enableSurvival = true;
        private bool enableRagdoll = true;
        private bool enableAudio = true;
        
        private Vector2 scrollPosition;
        #endregion
        
        #region Menu Items
        [MenuItem("Tools/Player/Character Prefab Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<CharacterPrefabSetupWizard>("Character Prefab Setup");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }
        
        [MenuItem("GameObject/DIG - Setup/Character Prefabs", false, 10)]
        public static void ShowWindowFromGameObject()
        {
            ShowWindow();
        }
        #endregion
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Character Prefab Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates Client and Server prefab variants from an FBX model with all required components for networked characters.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Input Section
            DrawInputSection();
            
            EditorGUILayout.Space(10);
            
            // Component Configuration
            DrawComponentConfiguration();
            
            EditorGUILayout.Space(20);
            
            // Create Button
            DrawCreateButton();
            
            EditorGUILayout.EndScrollView();
        }
        
        #region UI Sections
        private void DrawInputSection()
        {
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            sourceFBX = (GameObject)EditorGUILayout.ObjectField(
                "Source FBX Model", 
                sourceFBX, 
                typeof(GameObject), 
                false);
            
            if (EditorGUI.EndChangeCheck() && sourceFBX != null)
            {
                // Auto-fill character name from FBX
                characterName = sourceFBX.name.Replace("_", " ").Trim();
            }
            
            characterName = EditorGUILayout.TextField("Character Name", characterName);
            
            animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                "Animator Controller",
                animatorController,
                typeof(RuntimeAnimatorController),
                false);
            
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                    {
                        outputFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawComponentConfiguration()
        {
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            
            // Locomotion
            foldoutLocomotion = EditorGUILayout.Foldout(foldoutLocomotion, "Locomotion", true);
            if (foldoutLocomotion)
            {
                EditorGUI.indentLevel++;
                enableJump = EditorGUILayout.Toggle("Jump", enableJump);
                enableCrouch = EditorGUILayout.Toggle("Crouch", enableCrouch);
                enableSprint = EditorGUILayout.Toggle("Sprint", enableSprint);
                EditorGUI.indentLevel--;
            }
            
            // Abilities
            foldoutAbilities = EditorGUILayout.Foldout(foldoutAbilities, "Movement Abilities", true);
            if (foldoutAbilities)
            {
                EditorGUI.indentLevel++;
                enableRoll = EditorGUILayout.Toggle("Dodge Roll", enableRoll);
                enableDive = EditorGUILayout.Toggle("Dodge Dive", enableDive);
                enableSlide = EditorGUILayout.Toggle("Slide", enableSlide);
                enableMantle = EditorGUILayout.Toggle("Mantle/Vault", enableMantle);
                EditorGUI.indentLevel--;
            }
            
            // Climbing
            foldoutClimbing = EditorGUILayout.Foldout(foldoutClimbing, "Climbing System", true);
            if (foldoutClimbing)
            {
                EditorGUI.indentLevel++;
                enableClimbing = EditorGUILayout.Toggle("Enable Climbing", enableClimbing);
                EditorGUI.indentLevel--;
            }
            
            // Swimming
            foldoutSwimming = EditorGUILayout.Foldout(foldoutSwimming, "Swimming System", true);
            if (foldoutSwimming)
            {
                EditorGUI.indentLevel++;
                enableSwimming = EditorGUILayout.Toggle("Enable Swimming", enableSwimming);
                EditorGUI.indentLevel--;
            }
            
            // Survival
            foldoutSurvival = EditorGUILayout.Foldout(foldoutSurvival, "Survival Systems", true);
            if (foldoutSurvival)
            {
                EditorGUI.indentLevel++;
                enableSurvival = EditorGUILayout.Toggle("Enable Survival (EVA, Oxygen)", enableSurvival);
                EditorGUI.indentLevel--;
            }
            
            // Ragdoll
            foldoutRagdoll = EditorGUILayout.Foldout(foldoutRagdoll, "Ragdoll Physics", true);
            if (foldoutRagdoll)
            {
                EditorGUI.indentLevel++;
                enableRagdoll = EditorGUILayout.Toggle("Enable Ragdoll", enableRagdoll);
                EditorGUILayout.HelpBox("Ragdoll will be auto-configured if model has standard Humanoid rig.", MessageType.Info);
                EditorGUI.indentLevel--;
            }
            
            // Audio
            foldoutAudio = EditorGUILayout.Foldout(foldoutAudio, "Audio Systems", true);
            if (foldoutAudio)
            {
                EditorGUI.indentLevel++;
                enableAudio = EditorGUILayout.Toggle("Enable Audio", enableAudio);
                EditorGUI.indentLevel--;
            }
        }
        
        private void DrawCreateButton()
        {
            bool canCreate = sourceFBX != null && !string.IsNullOrEmpty(characterName);
            
            EditorGUI.BeginDisabledGroup(!canCreate);
            if (GUILayout.Button("Create Prefabs", GUILayout.Height(40)))
            {
                CreatePrefabs();
            }
            EditorGUI.EndDisabledGroup();
            
            if (!canCreate)
            {
                EditorGUILayout.HelpBox("Please provide a source FBX model and character name.", MessageType.Warning);
            }
        }
        #endregion
        
        #region Prefab Creation
        private void CreatePrefabs()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Creating Prefabs", "Setting up...", 0f);
                
                // Ensure output folder exists
                if (!AssetDatabase.IsValidFolder(outputFolder))
                {
                    System.IO.Directory.CreateDirectory(outputFolder);
                    AssetDatabase.Refresh();
                }
                
                string cleanName = characterName.Replace(" ", "_");
                string clientPath = $"{outputFolder}/{cleanName}_Client.prefab";
                string serverPath = $"{outputFolder}/{cleanName}_Server.prefab";
                
                // Create Client Prefab
                EditorUtility.DisplayProgressBar("Creating Prefabs", "Creating Client prefab...", 0.3f);
                GameObject clientPrefab = CreateClientPrefab(clientPath);
                
                // Create Server Prefab
                EditorUtility.DisplayProgressBar("Creating Prefabs", "Creating Server prefab...", 0.6f);
                GameObject serverPrefab = CreateServerPrefab(serverPath, clientPrefab);
                
                EditorUtility.DisplayProgressBar("Creating Prefabs", "Saving assets...", 0.9f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                EditorUtility.ClearProgressBar();
                
                UnityEngine.Debug.Log($"[CharacterPrefabSetupWizard] Created prefabs:\n  - {clientPath}\n  - {serverPath}");
                EditorUtility.DisplayDialog("Success", 
                    $"Created prefabs:\n• {cleanName}_Client\n• {cleanName}_Server\n\nLocation: {outputFolder}", 
                    "OK");
                
                // Select the server prefab in the project window
                Selection.activeObject = serverPrefab;
                EditorGUIUtility.PingObject(serverPrefab);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.LogError($"[CharacterPrefabSetupWizard] Error creating prefabs: {e}");
                EditorUtility.DisplayDialog("Error", $"Failed to create prefabs:\n{e.Message}", "OK");
            }
        }
        
        private GameObject CreateClientPrefab(string path)
        {
            // Instantiate from source FBX
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceFBX);
            instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
            instance.tag = "Player";
            
            // Set animator controller
            var animator = instance.GetComponent<Animator>();
            if (animator != null && animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
            }
            
            // Add client components
            AddClientComponents(instance);
            
            // Create prefab variant
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            DestroyImmediate(instance);
            
            return prefab;
        }
        
        private GameObject CreateServerPrefab(string path, GameObject clientPrefab)
        {
            // Instantiate from source FBX
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceFBX);
            instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
            instance.tag = "Player";
            
            // Disable the renderer on server
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                renderer.gameObject.SetActive(false);
            }
            
            // Add server components
            AddServerComponents(instance, clientPrefab);
            
            // Create prefab variant
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            DestroyImmediate(instance);
            
            return prefab;
        }
        #endregion
        
        #region Client Components
        private void AddClientComponents(GameObject root)
        {
            // Core presentation components
            AddComponentByName(root, "GhostPresentationGameObjectEntityOwner", "Unity.NetCode.Hybrid");
            AddComponentByName(root, "AnimatorEventBridge", "Audio.Systems");
            AddComponentByName(root, "AnimatorRigBridge", "Player.Bridges");
            AddComponentByName(root, "KinematicCharacterController", "Player.Controllers");
            
            // Landing
            AddComponentByName(root, "LandingAnimationAdapter");
            AddComponentByName(root, "LandingAnimatorBridge", "Player.Bridges");
            
            // Dodge Roll
            if (enableRoll)
            {
                AddComponentByName(root, "DodgeRollAuthoring", "Player.Authoring");
                AddComponentByName(root, "DodgeRollAnimatorBridge", "Player.Bridges");
            }
            
            // Prone
            AddComponentByName(root, "ProneAuthoring", "Player.Authoring");
            AddComponentByName(root, "ProneAnimatorBridge", "Player.Bridges");
            
            // Dodge Dive
            if (enableDive)
            {
                AddComponentByName(root, "DodgeDiveAuthoring", "Player.Authoring");
                AddComponentByName(root, "DodgeDiveAnimatorBridge", "Player.Bridges");
            }
            
            // Climbing
            if (enableClimbing)
            {
                var climbBridge = AddComponentByName(root, "ClimbAnimatorBridge", "Player.Bridges");
                if (climbBridge != null)
                {
                    SetupClimbIKTargets(root, climbBridge);
                }
            }
            
            // Slide
            if (enableSlide)
            {
                AddComponentByName(root, "SlideAuthoring", "Player.Authoring");
                AddComponentByName(root, "SlideAnimatorBridge", "Player.Bridges");
            }
            
            // Mantle
            if (enableMantle)
            {
                AddComponentByName(root, "MantleAuthoring", "Player.Authoring");
                AddComponentByName(root, "MantleAnimatorBridge", "Player.Bridges");
            }
            
            // Combat/Knockdown
            AddComponentByName(root, "KnockdownAnimatorBridge", "Player.Animation");
            AddComponentByName(root, "TackleAnimatorBridge", "Player.Animation");
            
            // Audio/VFX
            if (enableAudio)
            {
                AddComponentByName(root, "CollisionAudioBridge", "Player.Bridges");
                AddComponentByName(root, "CollisionVFXBridge", "Player.Bridges");
            }
            
            // Tools/Throwables
            AddComponentByName(root, "ThrowableAuthoring", "DIG.Survival.Throwables.Authoring");
            AddComponentByName(root, "ToolAuthoring", "DIG.Survival.Tools.Authoring");
            
            // Ragdoll presentation
            if (enableRagdoll)
            {
                AddComponentByName(root, "RagdollPresentationBridge", "Player.Animation");
            }
            
            // Locomotion/IK
            AddComponentByName(root, "LocomotionAbilityAuthoring", "DIG.Player.Authoring.Abilities");
            AddComponentByName(root, "IKAuthoring", "DIG.Player.Authoring.IK");
            AddComponentByName(root, "PlayerIKBridge", "DIG.Player.View");
            AddComponentByName(root, "MovementPolishAuthoring", "DIG.Player.Authoring.Abilities");
            
            // Child objects
            CreateChildObject(root, "AudioManager", "AudioManager", "Audio.Systems");
            CreateChildObject(root, "FlashlightMount", null, null);
            
            // Ragdoll bones
            if (enableRagdoll)
            {
                SetupRagdollBones(root);
            }
        }
        
        private void SetupClimbIKTargets(GameObject root, Component climbBridge)
        {
            // Find or create IK target transforms
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;
            
            // Create IK targets as children of the corresponding bones
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            
            Transform leftHandIK = CreateIKTarget(leftHand, "LeftHandIK");
            Transform rightHandIK = CreateIKTarget(rightHand, "RightHandIK");
            Transform leftFootIK = CreateIKTarget(leftFoot, "LeftFootIK");
            Transform rightFootIK = CreateIKTarget(rightFoot, "RightFootIK");
            
            // Set references via reflection
            SetFieldValue(climbBridge, "LeftHandIKTarget", leftHandIK);
            SetFieldValue(climbBridge, "RightHandIKTarget", rightHandIK);
            SetFieldValue(climbBridge, "LeftFootIKTarget", leftFootIK);
            SetFieldValue(climbBridge, "RightFootIKTarget", rightFootIK);
        }
        
        private Transform CreateIKTarget(Transform parent, string name)
        {
            if (parent == null) return null;
            
            var existing = parent.Find(name);
            if (existing != null) return existing;
            
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }
        
        private void SetupRagdollBones(GameObject root)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                UnityEngine.Debug.LogWarning("[CharacterPrefabSetupWizard] Cannot setup ragdoll - model is not Humanoid.");
                return;
            }
            
            // Key bones for ragdoll
            var boneMappings = new Dictionary<HumanBodyBones, (float mass, float radius, float height)>
            {
                { HumanBodyBones.Hips, (12.5f, 0, 0) },
                { HumanBodyBones.Spine, (12.5f, 0, 0) },
                { HumanBodyBones.Head, (5f, 0.18f, 0) },
                { HumanBodyBones.LeftUpperArm, (5f, 0.12f, 0.49f) },
                { HumanBodyBones.LeftLowerArm, (7.5f, 0.11f, 0.47f) },
                { HumanBodyBones.RightUpperArm, (5f, 0.12f, 0.49f) },
                { HumanBodyBones.RightLowerArm, (7.5f, 0.11f, 0.47f) },
                { HumanBodyBones.LeftUpperLeg, (7.5f, 0.11f, 0.38f) },
                { HumanBodyBones.LeftLowerLeg, (5f, 0.16f, 0.78f) },
                { HumanBodyBones.RightUpperLeg, (7.5f, 0.11f, 0.38f) },
                { HumanBodyBones.RightLowerLeg, (5f, 0.16f, 0.78f) }
            };
            
            foreach (var kvp in boneMappings)
            {
                var bone = animator.GetBoneTransform(kvp.Key);
                if (bone == null) continue;
                
                var go = bone.gameObject;
                var (mass, radius, height) = kvp.Value;
                
                // Add Rigidbody
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.isKinematic = true;
                rb.angularDamping = 0.05f;
                
                // Add Collider
                if (kvp.Key == HumanBodyBones.Hips || kvp.Key == HumanBodyBones.Spine)
                {
                    var box = go.GetComponent<BoxCollider>();
                    if (box == null) box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.73f, 0.4f, 0.41f);
                    box.center = new Vector3(0, 0.09f, 0);
                }
                else if (kvp.Key == HumanBodyBones.Head)
                {
                    var sphere = go.GetComponent<SphereCollider>();
                    if (sphere == null) sphere = go.AddComponent<SphereCollider>();
                    sphere.radius = radius;
                    sphere.center = new Vector3(0, radius, 0);
                }
                else if (height > 0)
                {
                    var capsule = go.GetComponent<CapsuleCollider>();
                    if (capsule == null) capsule = go.AddComponent<CapsuleCollider>();
                    capsule.radius = radius;
                    capsule.height = height;
                    capsule.direction = 1; // Y-axis
                    capsule.center = new Vector3(0, height / 2f, 0);
                }
            }
        }
        #endregion
        
        #region Server Components
        private void AddServerComponents(GameObject root, GameObject clientPrefab)
        {
            // NetCode/ECS Core
            AddComponentByName(root, "LinkedEntityGroupAuthoring", "Unity.Entities.Hybrid.Baking");
            var ghostAuth = AddComponentByName(root, "GhostAuthoringComponent", "Unity.NetCode");
            SetFieldValue(ghostAuth, "DefaultGhostMode", 2); // Predicted
            SetFieldValue(ghostAuth, "HasOwner", true);
            SetFieldValue(ghostAuth, "SupportAutoCommandTarget", true);
            
            var presentationAuth = AddComponentByName(root, "GhostPresentationGameObjectAuthoring", "Unity.NetCode.Hybrid");
            if (presentationAuth != null && clientPrefab != null)
            {
                SetFieldValue(presentationAuth, "ClientPrefab", clientPrefab);
                SetFieldValue(presentationAuth, "ServerPrefab", root);
            }
            
            // Player Core
            AddComponentByName(root, "PlayerAuthoring");
            AddComponentByName(root, "PlayerInputAuthoring", "Player.Authoring");
            
            // Physics
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) cc = root.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            
            AddComponentByName(root, "CharacterControllerAuthoring", "Player.Authoring");
            
            // Fall Damage
            AddComponentByName(root, "FallDamageAuthoring");
            
            // Input
            var inputReader = AddComponentByName(root, "PlayerInputReader", "Player.Systems");
            if (inputReader != null)
            {
                ((MonoBehaviour)inputReader).enabled = false;
            }
            
            // Survival
            if (enableSurvival)
            {
                AddComponentByName(root, "SurvivalAuthoring", "DIG.Survival.Authoring");
                AddComponentByName(root, "VisorAuthoring", "Visuals.Authoring");
                AddComponentByName(root, "StressAuthoring", "Player.Authoring");
            }
            
            // Audio
            if (enableAudio)
            {
                var audioAuth = AddComponentByName(root, "AudioListenerAuthoring", "Audio.Authoring");
                var breathSource = CreateAudioSourceChild(root, "BreathSource");
                var heartbeatSource = CreateAudioSourceChild(root, "HeartbeatSource");
                SetFieldValue(audioAuth, "BreathSource", breathSource);
                SetFieldValue(audioAuth, "HeartbeatSource", heartbeatSource);
            }
            
            // Horror
            AddComponentByName(root, "HallucinationReceiverAuthoring", "Horror.Authoring");
            
            // Swimming
            if (enableSwimming)
            {
                AddComponentByName(root, "SwimmingAuthoring", "DIG.Swimming.Authoring");
            }
            
            // Physics Interaction
            AddComponentByName(root, "PushInteractionAuthoring", "DIG.Survival.Physics.Authoring");
            
            // Ragdoll
            if (enableRagdoll)
            {
                AddComponentByName(root, "RagdollAuthoring", "DIG.Survival.Physics.Authoring");
            }
            
            // Climbing
            if (enableClimbing)
            {
                AddComponentByName(root, "FreeClimbSettingsAuthoring", "Player.Authoring");
            }
            
            // Locomotion
            AddComponentByName(root, "LocomotionAbilityAuthoring", "DIG.Player.Authoring.Abilities");
            AddComponentByName(root, "IKAuthoring", "DIG.Player.Authoring.IK");
            AddComponentByName(root, "MovementPolishAuthoring", "DIG.Player.Authoring.Abilities");
            
            // Death/Misc
            AddComponentByName(root, "DeathSpawnAuthoring", "Player.Authoring");
            AddComponentByName(root, "GodModeAuthoring", "Player.Authoring");
        }
        
        private AudioSource CreateAudioSourceChild(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D for breath/heartbeat
            return source;
        }
        #endregion
        
        #region Utility Methods
        private Component AddComponentByName(GameObject target, string className, string namespaceName = null)
        {
            // Try common assembly combinations
            string[] assemblies = { "Assembly-CSharp", "Unity.NetCode", "Unity.NetCode.Hybrid", "Unity.Entities.Hybrid" };
            string[] namespaces = namespaceName != null 
                ? new[] { namespaceName, "" }
                : new[] { "", "Player", "Player.Authoring", "Player.Bridges", "Audio.Systems", "DIG.Survival" };
            
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
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == className && typeof(Component).IsAssignableFrom(type))
                    {
                        var existing = target.GetComponent(type);
                        if (existing != null) return existing;
                        
                        try
                        {
                            return target.AddComponent(type);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            
            UnityEngine.Debug.LogWarning($"[CharacterPrefabSetupWizard] Could not find component: {className}");
            return null;
        }
        
        private void CreateChildObject(GameObject parent, string name, string componentName, string namespaceName)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            
            if (!string.IsNullOrEmpty(componentName))
            {
                AddComponentByName(child, componentName, namespaceName);
            }
        }
        
        private void SetFieldValue(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            
            var type = obj.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            var field = type.GetField(fieldName, flags);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }
            
            var prop = type.GetProperty(fieldName, flags);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
            }
        }
        #endregion
    }
}
