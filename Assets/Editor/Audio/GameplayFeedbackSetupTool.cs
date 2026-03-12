using UnityEngine;
using UnityEditor;
using DIG.Core.Feedback;
using MoreMountains.Feedbacks;
using Audio.Systems;

namespace DIG.Editor.Audio
{
    public class GameplayFeedbackSetupTool
    {
        [MenuItem("Tools/DIG/Setup Gameplay Feedback", false, 100)]
        public static void SetupGameplayFeedback()
        {
            // 1. Find or Create Manager
            var manager = Object.FindFirstObjectByType<GameplayFeedbackManager>();
            if (manager == null)
            {
                var go = new GameObject("GameplayFeedbackManager");
                manager = go.AddComponent<GameplayFeedbackManager>();
                Undo.RegisterCreatedObjectUndo(go, "Create GameplayFeedbackManager");
                Debug.Log("Created new GameplayFeedbackManager.");
            }
            else
            {
                Debug.Log($"Found existing GameplayFeedbackManager on {manager.gameObject.name}");
            }

            var serializedObj = new SerializedObject(manager);
            serializedObj.Update();

            // 2. Assign Registry
            var registryProp = serializedObj.FindProperty("_surfaceRegistry");
            if (registryProp.objectReferenceValue == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:SurfaceMaterialRegistry");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var reg = AssetDatabase.LoadAssetAtPath<SurfaceMaterialRegistry>(path);
                    registryProp.objectReferenceValue = reg;
                    Debug.Log($"Assigned SufaceMaterialRegistry: {reg.name}");
                }
                else
                {
                    Debug.LogWarning("Could not find any SurfaceMaterialRegistry asset in the project. Please assign manually.");
                }
            }

            // 3. Create/Assign Feedbacks
            SetupFeedbackSlot(manager, serializedObj, "_footstepFeedback", "Footstep Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_jumpFeedback", "Jump Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_landFeedback", "Land Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_rollFeedback", "Roll Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_diveFeedback", "Dive Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_slideFeedback", "Slide Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_climbStartFeedback", "Climb Start Feedback");
            
            // Optional/Other feedbacks
            SetupFeedbackSlot(manager, serializedObj, "_wallRunFeedback", "WallRun Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_fireFeedback", "Fire Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_damageFeedback", "Damage Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_heavyHitFeedback", "HeavyHit Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_pickupFeedback", "Pickup Feedback");
            SetupFeedbackSlot(manager, serializedObj, "_interactFeedback", "Interact Feedback");

            // 4. Setup Camera Juice (Impulse Listeners)
            SetupCameraJuice();

            serializedObj.ApplyModifiedProperties();
            
            Debug.Log("Gameplay Feedback Setup Complete!");
            Selection.activeGameObject = manager.gameObject;
        }

        private static void SetupCameraJuice()
        {
            // Detect Cinemachine Version in Scene using Reflection to avoid Compilation Errors
            bool useLegacy = false;
            
            // Check for Legacy Brain (Cinemachine.CinemachineBrain)
            System.Type legacyBrainType = System.Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
            if (legacyBrainType != null && Object.FindFirstObjectByType(legacyBrainType) != null)
            {
                useLegacy = true; 
                Debug.Log("[SetupTool] Detected Legacy Cinemachine (2.x). Using Legacy components.");
            }
            else if (Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>() != null)
            {
                useLegacy = false;
                Debug.Log("[SetupTool] Detected Modern Cinemachine (3.x). Using Unity.Cinemachine components.");
            }
            else
            {
                // Fallback: Check if VCam existing implies usage?
                System.Type legacyVCamType = System.Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");
                if (legacyVCamType != null && Object.FindFirstObjectByType(legacyVCamType) != null)
                {
                    useLegacy = true;
                    Debug.Log("[SetupTool] Detected Legacy Virtual Cameras. Using Legacy components.");
                }
                else
                {
                    Debug.LogWarning("[SetupTool] No Cinemachine Brain found. defaulting to Modern check.");
                }
            }

            if (useLegacy)
            {
                // LEGACY SETUP via Reflection
                System.Type vcamType = System.Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");
                System.Type impulseListenerType = System.Type.GetType("Cinemachine.CinemachineImpulseListener, Cinemachine");
                
                if (impulseListenerType != null)
                {
                    // 1. Attach to Brain (Global Listener)
                    var brain = Object.FindFirstObjectByType(legacyBrainType); // We know it exists from check above
                    if (brain != null)
                    {
                        var brainGO = (GameObject)legacyBrainType.GetProperty("gameObject").GetValue(brain);
                        if (brainGO.GetComponent(impulseListenerType) == null)
                        {
                            Undo.AddComponent(brainGO, impulseListenerType);
                            Debug.Log($"[SetupTool] Added Legacy CinemachineImpulseListener to Brain {brainGO.name}");
                        }
                    }

                    // 2. Attach to VCams (Extension style - optional but good for local checks)
                    if (vcamType != null)
                    {
                        var cameras = Object.FindObjectsByType(vcamType, FindObjectsSortMode.None);
                        foreach (var cam in cameras)
                        {
                            var go = (GameObject)vcamType.GetProperty("gameObject").GetValue(cam);
                            if (go.GetComponent(impulseListenerType) == null)
                            {
                                Undo.AddComponent(go, impulseListenerType);
                                // Debug.Log($"[SetupTool] Added Legacy CinemachineImpulseListener to VCam {go.name}");
                            }
                        }
                        if (cameras.Length == 0) Debug.LogWarning("[SetupTool] No Legacy Virtual Cameras found to attach listener.");
                    }
                }
            }
            else
            {
                // MODERN SETUP (Unity.Cinemachine)
                // 1. Attach to Brain (Global Listener)
                var brain = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
                if (brain != null)
                {
                    if (brain.GetComponent<Unity.Cinemachine.CinemachineImpulseListener>() == null)
                    {
                        Undo.AddComponent<Unity.Cinemachine.CinemachineImpulseListener>(brain.gameObject);
                        Debug.Log($"[SetupTool] Added Unity.CinemachineImpulseListener to Brain {brain.name}");
                    }
                }

                // 2. Attach to CinemachineCameras
                var cameras = Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
                foreach (var cam in cameras)
                {
                    if (cam.GetComponent<Unity.Cinemachine.CinemachineImpulseListener>() == null)
                    {
                        Undo.AddComponent<Unity.Cinemachine.CinemachineImpulseListener>(cam.gameObject);
                        Debug.Log($"[SetupTool] Added Unity.CinemachineImpulseListener to {cam.name}");
                    }
                }
                
                if (cameras.Length == 0) Debug.LogWarning("[SetupTool] No Unity.Cinemachine.CinemachineCamera found. Listener only attached to Brain.");
            }
        }

        private static void SetupFeedbackSlot(GameplayFeedbackManager manager, SerializedObject serializedManager, string propertyName, string childName)
        {
            var prop = serializedManager.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"Could not find property '{propertyName}' on GameplayFeedbackManager.");
                return;
            }

            // If already assigned, check if valid
            if (prop.objectReferenceValue != null)
            {
                return; 
            }

            // Check if child exists even if not assigned
            Transform child = manager.transform.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(manager.transform);
                go.transform.localPosition = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
                child = go.transform;
            }

            var mmf = child.GetComponent<MMF_Player>();
            if (mmf == null)
            {
                mmf = Undo.AddComponent<MMF_Player>(child.gameObject);
                if (mmf.FeedbacksList == null)
                {
                    mmf.FeedbacksList = new System.Collections.Generic.List<MMF_Feedback>();
                }
                
                // Initialize defaults for Audio/VFX injection if it's a movement feedback
                // We use a heuristic based on name
                if (childName.Contains("Footstep") || childName.Contains("Jump") || childName.Contains("Land") || 
                    childName.Contains("Roll") || childName.Contains("Dive") || childName.Contains("Slide") || 
                    childName.Contains("Climb"))
                {
                    // Add Sound Feedback
                    if (mmf.GetFeedbackOfType<MMF_Sound>() == null)
                    {
                        var sound = (MMF_Sound)mmf.AddFeedback(typeof(MMF_Sound));
                        sound.Active = true;
                        sound.Label = "Dynamic Surface Audio";
                    }

                    // Add Particles Feedback
                    if (mmf.GetFeedbackOfType<MMF_ParticlesInstantiation>() == null)
                    {
                        var vfx = (MMF_ParticlesInstantiation)mmf.AddFeedback(typeof(MMF_ParticlesInstantiation));
                        vfx.Active = true;
                        vfx.Label = "Dynamic Surface VFX";
                    }
                }

                // --- JUICE: Screen Shake & Wiggle ---
                bool isJump = childName.Contains("Jump");
                bool isLand = childName.Contains("Land");
                bool isDamage = childName.Contains("Damage") || childName.Contains("HeavyHit");

                if (isJump || isLand || isDamage)
                {
                    // Add Cinemachine Impulse (Screen Shake)
                    // Configured via reflection to support both Legacy and Modern Cinemachine
                    
                    bool hasModernImpulse = System.Type.GetType("MoreMountains.FeedbacksForThirdParty.MMF_CinemachineImpulse, MoreMountains.Feedbacks.Cinemachine") != null;
                    bool hasLegacyImpulse = System.Type.GetType("MoreMountains.FeedbacksForThirdParty.MMF_CinemachineImpulse, MoreMountains.Feedbacks.Cinemachine") != null; // Same type name usually?
                    
                    // Actually, let's just use GetFeedbackOfType with the type reference we have.
                    // If the project doesn't have the third party package, this line might fail to compile depending on assembly defs.
                    // But assuming it compiles:
                    
                     if (mmf.GetFeedbackOfType<MoreMountains.FeedbacksForThirdParty.MMF_CinemachineImpulse>() == null)
                    {
                        var impulse = (MoreMountains.FeedbacksForThirdParty.MMF_CinemachineImpulse)mmf.AddFeedback(typeof(MoreMountains.FeedbacksForThirdParty.MMF_CinemachineImpulse));
                        impulse.Active = true;
                        impulse.Label = "Screen Shake";
                        // Default velocity
                        if (isLand) impulse.Velocity = new Vector3(0, -1f, 0);
                        else if (isJump) impulse.Velocity = new Vector3(0, 1f, 0);
                        else if (isDamage) impulse.Velocity = new Vector3(1f, 1f, 0) * 2f; 
                    }

                    // Add Wiggle (Squash & Stretch) - Only for Movement
                    if ((isJump || isLand) && mmf.GetFeedbackOfType<MMF_Wiggle>() == null)
                    {
                        var wiggle = (MMF_Wiggle)mmf.AddFeedback(typeof(MMF_Wiggle));
                        wiggle.Active = true;
                        wiggle.Label = "Squash & Stretch";
                        
                        // Setup Wiggle properties
                        wiggle.WiggleScale = true;
                        wiggle.WiggleScaleDuration = 0.2f;
                        
                        // Note: The user refers to MMWiggle component on the target (e.g. Player Model).
                        // MMF_Wiggle triggers that component. We cannot assign it automatically here as we don't know the player.
                    }
                }
            }

            prop.objectReferenceValue = mmf;
        }
    }
}
