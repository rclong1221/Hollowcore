using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;

namespace DIG.CameraSystem.Cinemachine.Editor
{
    /// <summary>
    /// EPIC 14.18 - Cinemachine Camera Rig Setup
    /// Editor utility to create a properly configured Cinemachine camera rig.
    /// </summary>
    public static class CinemachineCameraRigSetup
    {
        [MenuItem("DIG/Camera/Create Cinemachine Camera Rig")]
        public static void CreateCameraRig()
        {
            // Create root object
            var rigRoot = new GameObject("CinemachineCameraRig");
            Undo.RegisterCreatedObjectUndo(rigRoot, "Create Cinemachine Camera Rig");
            
            // Add controller
            var controller = rigRoot.AddComponent<CinemachineCameraController>();
            
            // Add shake bridge
            rigRoot.AddComponent<CinemachineShakeBridge>();
            
            // Create follow targets
            var followTarget = new GameObject("FollowTarget");
            followTarget.transform.SetParent(rigRoot.transform);
            followTarget.transform.localPosition = new Vector3(0f, 1.5f, 0f); // Shoulder height for TPS
            
            var fpsTarget = new GameObject("FPSTarget");
            fpsTarget.transform.SetParent(rigRoot.transform);
            fpsTarget.transform.localPosition = new Vector3(0f, 1.7f, 0f); // Eye height for FPS
            
            // Find or create main camera
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraGO = new GameObject("Main Camera");
                cameraGO.tag = "MainCamera";
                mainCamera = cameraGO.AddComponent<Camera>();
                cameraGO.AddComponent<AudioListener>();
                Undo.RegisterCreatedObjectUndo(cameraGO, "Create Main Camera");
            }
            
            // Add Cinemachine Brain to main camera
            var brain = mainCamera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
                Debug.Log("[CameraRigSetup] Added CinemachineBrain to Main Camera");
            }
            
            // Configure brain
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, 
                0.5f
            );
            
            // Create Third-Person Camera
            var tpCameraGO = new GameObject("CM ThirdPerson");
            tpCameraGO.transform.SetParent(rigRoot.transform);
            var tpCamera = tpCameraGO.AddComponent<CinemachineCamera>();
            
            // Configure third-person camera
            tpCamera.Priority = 10;
            tpCamera.Follow = followTarget.transform;
            tpCamera.LookAt = null; // Don't use LookAt - we drive rotation via Follow target
            
            // Add Third Person Follow component
            var tpFollow = tpCameraGO.AddComponent<CinemachineThirdPersonFollow>();
            tpFollow.ShoulderOffset = new Vector3(0.5f, 0.3f, 0f);
            tpFollow.VerticalArmLength = 0.4f;
            tpFollow.CameraDistance = 5f;
            tpFollow.CameraSide = 0.5f; // Centered — lateral offset via ShoulderOffset.x
            tpFollow.Damping = new Vector3(0.5f, 0.5f, 0.5f);
            // Note: CameraCollisionFilter is configured via CinemachineDeoccluder in CM3
            
            // Add Deoccluder for collision avoidance
            var deoccluder = tpCameraGO.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = LayerMask.GetMask("Default", "Environment");
            
            // Note: We don't use RotationComposer - our controller drives Follow target rotation
            // which ThirdPersonFollow inherits for orbiting
            
            // Add impulse listener
            var tpImpulse = tpCameraGO.AddComponent<CinemachineImpulseListener>();
            tpImpulse.Gain = 1f;
            
            // Create First-Person Camera
            var fpsCameraGO = new GameObject("CM FirstPerson");
            fpsCameraGO.transform.SetParent(rigRoot.transform);
            var fpsCamera = fpsCameraGO.AddComponent<CinemachineCamera>();
            
            // Configure first-person camera
            fpsCamera.Priority = 0;
            fpsCamera.Follow = fpsTarget.transform;
            fpsCamera.LookAt = null;
            
            // Add Hard Lock for position
            var fpsHardLock = fpsCameraGO.AddComponent<CinemachineHardLockToTarget>();
            
            // Add POV for rotation (mouse look)
            // In Cinemachine 3.x, POV is now CinemachinePanTilt
            var fpsPanTilt = fpsCameraGO.AddComponent<CinemachinePanTilt>();
            // Tilt range limits (vertical) - clamped between -89 and 89 degrees
            fpsPanTilt.TiltAxis.Range = new Vector2(-89f, 89f);
            // Pan wraps around (horizontal)
            fpsPanTilt.PanAxis.Wrap = true;
            
            // Add impulse listener
            var fpsImpulse = fpsCameraGO.AddComponent<CinemachineImpulseListener>();
            fpsImpulse.Gain = 1f;
            
            // Create Isometric Camera (for ARPG, MOBA, TwinStick)
            var isoCameraGO = new GameObject("CM Isometric");
            isoCameraGO.transform.SetParent(rigRoot.transform);
            var isoCamera = isoCameraGO.AddComponent<CinemachineCamera>();
            
            // Configure isometric camera
            isoCamera.Priority = 0;
            isoCamera.Follow = followTarget.transform;
            isoCamera.LookAt = followTarget.transform;
            
            // Add Position Composer for follow behavior
            var isoFollow = isoCameraGO.AddComponent<CinemachineFollow>();
            isoFollow.FollowOffset = new Vector3(0f, 15f, -15f); // Height and distance for 45° angle
            isoFollow.TrackerSettings.PositionDamping = new Vector3(0.5f, 0.5f, 0.5f);
            
            // Add Rotation Composer to look at target
            var isoRotation = isoCameraGO.AddComponent<CinemachineRotationComposer>();
            isoRotation.Damping = new Vector2(0.5f, 0.5f);
            
            // Add impulse listener
            var isoImpulse = isoCameraGO.AddComponent<CinemachineImpulseListener>();
            isoImpulse.Gain = 0.5f; // Less shake for isometric
            
            // Wire up controller references via SerializedObject
            var so = new SerializedObject(controller);
            so.FindProperty("_thirdPersonCamera").objectReferenceValue = tpCamera;
            so.FindProperty("_firstPersonCamera").objectReferenceValue = fpsCamera;
            so.FindProperty("_isometricCamera").objectReferenceValue = isoCamera;
            so.FindProperty("_followTarget").objectReferenceValue = followTarget.transform;
            so.FindProperty("_fpsTarget").objectReferenceValue = fpsTarget.transform;
            so.ApplyModifiedProperties();
            
            // Select the created rig
            Selection.activeGameObject = rigRoot;
            
            Debug.Log("[CameraRigSetup] Created Cinemachine Camera Rig with Isometric support.");
            
            EditorUtility.DisplayDialog(
                "Cinemachine Camera Rig Created",
                "Camera rig created successfully!\n\n" +
                "The rig includes:\n" +
                "• Third-Person Camera (over-shoulder)\n" +
                "• First-Person Camera\n" +
                "• Isometric Camera (ARPG/MOBA/TwinStick)\n" +
                "• Smooth blending via CinemachineBrain\n" +
                "• Shake support via Impulse system\n\n" +
                "The FollowTarget will automatically track the local player at runtime.\n\n" +
                "Use CinemachineCameraController.SetCameraMode() to switch views.",
                "OK"
            );
        }
        
        [MenuItem("DIG/Camera/Add Brain to Main Camera")]
        public static void AddBrainToMainCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                EditorUtility.DisplayDialog("Error", "No Main Camera found in scene!", "OK");
                return;
            }
            
            if (mainCamera.GetComponent<CinemachineBrain>() != null)
            {
                EditorUtility.DisplayDialog("Info", "Main Camera already has CinemachineBrain!", "OK");
                return;
            }
            
            var brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, 
                0.5f
            );
            
            Undo.RegisterCreatedObjectUndo(brain, "Add CinemachineBrain");
            
            Debug.Log("[CameraRigSetup] Added CinemachineBrain to Main Camera");
        }
    }
}
