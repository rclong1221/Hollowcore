using Unity.Entities;
using UnityEngine;
using DIG.CameraSystem.Implementations;

namespace DIG.CameraSystem.Authoring
{
    /// <summary>
    /// Authoring component to configure the camera system on a player prefab.
    /// Automatically sets up the appropriate camera mode based on configuration.
    ///
    /// Usage:
    /// 1. Add to your player prefab (or camera GameObject)
    /// 2. Assign a CameraConfig asset (or use "Create Default" button)
    /// 3. The camera system will initialize on spawn
    ///
    /// For ghost prefabs, add to the same GameObject that has GhostAuthoringComponent.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraSystemAuthoring : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [Tooltip("Camera configuration asset. Create one via Create > DIG > Camera > Camera Config")]
        public CameraConfig CameraConfig;

        [Tooltip("If true, automatically registers with CameraModeProvider on Start.")]
        public bool RegisterWithProvider = true;

        [Tooltip("If true, creates camera mode component if not present.")]
        public bool AutoCreateCameraMode = true;

        [Header("Runtime References")]
        [Tooltip("Reference to the Unity Camera. If null, uses Camera.main.")]
        public Camera TargetCamera;

        [Tooltip("Transform to follow. If null, uses this GameObject's transform.")]
        public Transform FollowTarget;

        // Runtime state
        private ICameraMode _cameraMode;
        private bool _initialized;

        /// <summary>
        /// The active camera mode instance.
        /// </summary>
        public ICameraMode CameraMode => _cameraMode;

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the camera system.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            // Get or create camera mode component
            _cameraMode = GetCameraMode();
            if (_cameraMode == null)
            {
                Debug.LogWarning("[CameraSystemAuthoring] No camera mode found or created.");
                return;
            }

            // Initialize with config
            if (CameraConfig != null)
            {
                _cameraMode.Initialize(CameraConfig);
            }

            // Set target
            var target = FollowTarget != null ? FollowTarget : transform;
            _cameraMode.SetTarget(Entity.Null, target);

            // Register with provider
            if (RegisterWithProvider && CameraModeProvider.HasInstance)
            {
                CameraModeProvider.Instance.SetActiveCamera(_cameraMode);
            }

            _initialized = true;
        }

        private ICameraMode GetCameraMode()
        {
            // First, try to find existing camera mode
            var existing = GetComponent<ICameraMode>() as MonoBehaviour;
            if (existing != null)
            {
                return existing as ICameraMode;
            }

            // Try to find in children (camera might be on child object)
            existing = GetComponentInChildren<ICameraMode>() as MonoBehaviour;
            if (existing != null)
            {
                return existing as ICameraMode;
            }

            // Auto-create if enabled
            if (AutoCreateCameraMode && CameraConfig != null)
            {
                return CreateCameraMode();
            }

            return null;
        }

        private ICameraMode CreateCameraMode()
        {
            // Determine which camera component to create based on config
            GameObject cameraObj = TargetCamera != null ? TargetCamera.gameObject : gameObject;

            MonoBehaviour cameraComponent = null;

                switch (CameraConfig.CameraMode)
                {
                    case DIG.CameraSystem.CameraMode.ThirdPersonFollow:
                        cameraComponent = cameraObj.AddComponent<ThirdPersonFollowCamera>();
                        break;

                    case DIG.CameraSystem.CameraMode.IsometricFixed:
                        cameraComponent = cameraObj.AddComponent<IsometricFixedCamera>();
                        break;

                    case DIG.CameraSystem.CameraMode.TopDownFixed:
                        cameraComponent = cameraObj.AddComponent<TopDownFixedCamera>();
                        break;

                    case DIG.CameraSystem.CameraMode.IsometricRotatable:
                        cameraComponent = cameraObj.AddComponent<IsometricRotatableCamera>();
                        break;

                    default:
                        cameraComponent = cameraObj.AddComponent<ThirdPersonFollowCamera>();
                        break;
                }

                return cameraComponent as ICameraMode;
            }

        /// <summary>
        /// Change camera configuration at runtime.
        /// </summary>
        public void SetConfig(CameraConfig config)
        {
            CameraConfig = config;

            if (_cameraMode != null)
            {
                _cameraMode.Initialize(config);
            }
        }

        /// <summary>
        /// Update follow target at runtime.
        /// </summary>
        public void SetFollowTarget(Transform target, Entity entity = default)
        {
            FollowTarget = target;

            if (_cameraMode != null)
            {
                _cameraMode.SetTarget(entity, target);
            }
        }

        private void LateUpdate()
        {
            // Update camera if we have one
            if (_cameraMode != null && _initialized)
            {
                _cameraMode.UpdateCamera(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            // Unregister from provider
            if (RegisterWithProvider && CameraModeProvider.HasInstance)
            {
                if (CameraModeProvider.Instance.ActiveCamera == _cameraMode)
                {
                    CameraModeProvider.Instance.ClearActiveCamera();
                }
            }
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-find camera if not set
            if (TargetCamera == null)
            {
                TargetCamera = GetComponentInChildren<Camera>();
            }
        }

        /// <summary>
        /// Editor helper to create default config.
        /// </summary>
        [ContextMenu("Create Default DIG Config")]
        private void CreateDefaultDIGConfig()
        {
            CameraConfig = CameraConfig.CreateDIGPreset();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Editor helper to create ARPG config.
        /// </summary>
        [ContextMenu("Create Default ARPG Config")]
        private void CreateDefaultARPGConfig()
        {
            CameraConfig = CameraConfig.CreateARPGPreset();
            UnityEditor.EditorUtility.SetDirty(this);
        }
        #endif

        /// <summary>
        /// Baker for ECS integration (optional - camera system is primarily MonoBehaviour-based).
        /// This allows camera config reference to be available on the entity if needed.
        /// </summary>
        public class Baker : Baker<CameraSystemAuthoring>
        {
            public override void Bake(CameraSystemAuthoring authoring)
            {
                // Camera system is MonoBehaviour-based, but we can add a marker component
                // to indicate this entity has camera system configuration
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new CameraSystemTag
                {
                    Mode = authoring.CameraConfig != null
                        ? authoring.CameraConfig.CameraMode
                        : DIG.CameraSystem.CameraMode.ThirdPersonFollow
                });
            }
        }
    }

    /// <summary>
    /// Marker component indicating an entity has camera system configuration.
    /// </summary>
    public struct CameraSystemTag : IComponentData
    {
        public DIG.CameraSystem.CameraMode Mode;
    }
}
