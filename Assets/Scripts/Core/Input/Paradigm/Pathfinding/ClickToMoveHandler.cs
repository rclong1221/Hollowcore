using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Physics;
using Player.Systems;
using Pathfinding;

namespace DIG.Core.Input.Pathfinding
{
    /// <summary>
    /// Bridge between A* Pathfinding Project and the DIG ECS input pipeline.
    ///
    /// Handles: click detection, world-space raycast, A* path request,
    /// waypoint following, and synthetic input injection into PlayerInputState.
    ///
    /// When following a path, writes camera-relative direction to
    /// PlayerInputState.PathMoveDirection, which flows through the existing
    /// NetCode input pipeline (PlayerInputSystem -> PlayerInput -> PlayerMovementSystem).
    /// The server never needs A* — it just sees movement input identical to WASD.
    ///
    /// Implements IParadigmConfigurable so it auto-enables/disables
    /// based on the active paradigm's clickToMoveEnabled setting.
    ///
    /// EPIC 15.20 Phase 3
    /// </summary>
    public class ClickToMoveHandler : MonoBehaviour, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static ClickToMoveHandler Instance { get; private set; }

        // ============================================================
        // CONFIGURATION (set by IParadigmConfigurable.Configure)
        // ============================================================

        private bool _clickToMoveEnabled;
        private ClickToMoveButton _activeButton = ClickToMoveButton.None;
        private bool _usePathfinding;

        // ============================================================
        // TUNING
        // ============================================================

        [Header("Path Following")]
        [Tooltip("Distance at which an intermediate waypoint is considered 'reached'.")]
        [SerializeField] private float _waypointReachDistance = 0.5f;

        [Tooltip("Distance at which the final destination is considered 'reached'.")]
        [SerializeField] private float _destinationReachDistance = 0.3f;

        [Tooltip("Maximum raycast distance for click-to-move ground detection.")]
        [SerializeField] private float _maxRaycastDistance = 200f;

        [Header("Hold-to-Move")]
        [Tooltip("Minimum interval between repath requests while holding the click button.")]
        [SerializeField] private float _repathInterval = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool _logPathEvents;
        [SerializeField] private bool _drawPathGizmos;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        private Vector3[] _pathWaypoints;
        private int _currentWaypointIndex;
        private Vector3 _destination;
        private bool _hasActivePath;
        private bool _pathPending;
        private float _holdRepathTimer;

        // ============================================================
        // IParadigmConfigurable IMPLEMENTATION
        // ============================================================

        public int ConfigurationOrder => 110; // After MovementRouter (100), before FacingController (200)
        public string SubsystemName => "ClickToMoveHandler";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;
            if (profile.clickToMoveEnabled && profile.usePathfinding &&
                (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0))
            {
                Debug.LogWarning("[ClickToMoveHandler] A* graph not configured. " +
                    "Click-to-move will use direct movement fallback.");
            }
            return true;
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new ClickToMoveSnapshot
            {
                Enabled = _clickToMoveEnabled,
                Button = _activeButton,
                UsePathfinding = _usePathfinding,
            };
        }

        public void Configure(InputParadigmProfile profile)
        {
            bool wasEnabled = _clickToMoveEnabled;
            _clickToMoveEnabled = profile.clickToMoveEnabled;
            _activeButton = profile.clickToMoveButton;
            _usePathfinding = profile.usePathfinding;

            // Cancel any active path when click-to-move gets disabled
            if (wasEnabled && !_clickToMoveEnabled)
            {
                CancelPath();
            }

            if (_logPathEvents)
            {
                Debug.Log($"[ClickToMoveHandler] Configured: enabled={_clickToMoveEnabled}, " +
                    $"button={_activeButton}, pathfinding={_usePathfinding}");
            }
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is ClickToMoveSnapshot s)
            {
                _clickToMoveEnabled = s.Enabled;
                _activeButton = s.Button;
                _usePathfinding = s.UsePathfinding;
            }
        }

        private class ClickToMoveSnapshot : IConfigSnapshot
        {
            public bool Enabled;
            public ClickToMoveButton Button;
            public bool UsePathfinding;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[ClickToMoveHandler]");
            go.AddComponent<ClickToMoveHandler>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ParadigmStateMachine.Instance?.RegisterConfigurable(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CancelPath();
                Instance = null;
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }

        // ============================================================
        // MAIN UPDATE
        // ============================================================

        private void Update()
        {
            if (!_clickToMoveEnabled) return;

            // WASD INTERRUPTION: If player presses any movement key, cancel path.
            // EPIC 18.15: Only check if WASD is enabled — in MOBA/ARPG, Move callbacks still fire
            // from Core map but should not interrupt click-to-move paths.
            bool wasdEnabled = MovementRouter.Instance == null || MovementRouter.Instance.IsWASDEnabled;
            if (wasdEnabled && _hasActivePath && math.lengthsq(PlayerInputState.Move) > 0.01f)
            {
                if (_logPathEvents) Debug.Log("[ClickToMoveHandler] Path cancelled by WASD input");
                CancelPath();
                return;
            }

            ProcessClickInput();

            if (_hasActivePath)
            {
                FollowPath();
            }
            else
            {
                // Ensure clean state when not following
                PlayerInputState.IsPathFollowing = false;
                PlayerInputState.PathMoveDirection = float2.zero;
            }
        }

        // ============================================================
        // CLICK DETECTION
        // ============================================================

        private void ProcessClickInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool clicked = _activeButton switch
            {
                ClickToMoveButton.LeftButton => mouse.leftButton.wasPressedThisFrame,
                ClickToMoveButton.RightButton => mouse.rightButton.wasPressedThisFrame,
                _ => false,
            };

            bool held = _activeButton switch
            {
                ClickToMoveButton.LeftButton => mouse.leftButton.isPressed,
                ClickToMoveButton.RightButton => mouse.rightButton.isPressed,
                _ => false,
            };

            // Don't process if pointer is over UI
            if ((clicked || held) &&
                UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (clicked)
            {
                _holdRepathTimer = 0f;
                TryRaycastAndMove();
            }
            else if (held)
            {
                // Hold-to-move: continuously repath at interval
                _holdRepathTimer += Time.deltaTime;
                if (_holdRepathTimer >= _repathInterval)
                {
                    _holdRepathTimer = 0f;
                    TryRaycastAndMove();
                }
            }
        }

        private void TryRaycastAndMove()
        {
            var camera = Camera.main;
            if (camera == null) return;

            var screenPos = new Vector2(
                PlayerInputState.CursorScreenPosition.x,
                PlayerInputState.CursorScreenPosition.y);
            UnityEngine.Ray ray = camera.ScreenPointToRay(screenPos);

            float3 rayStart = ray.origin;
            float3 rayEnd = ray.origin + ray.direction * _maxRaycastDistance;

            // EPIC 18.15: Use Unity.Physics (ECS) raycasting — ground geometry is baked into
            // the ECS physics world and has no legacy Collider.
            if (TryECSRaycast(rayStart, rayEnd, out float3 hitPos))
            {
                RequestMoveTo(hitPos);
            }
        }

        /// <summary>
        /// Raycast against the ECS Unity.Physics world. Ground, walls, and most geometry
        /// exist only as ECS PhysicsCollider entities (baked from subscenes), not legacy Colliders.
        /// </summary>
        private bool TryECSRaycast(float3 start, float3 end, out float3 hitPosition)
        {
            hitPosition = float3.zero;

            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;

                var em = world.EntityManager;
                using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
                if (query.IsEmpty) continue;

                var physicsWorldSingleton = query.GetSingleton<PhysicsWorldSingleton>();
                var physicsWorld = physicsWorldSingleton.PhysicsWorld;

                var rayInput = new RaycastInput
                {
                    Start = start,
                    End = end,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                if (physicsWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
                {
                    hitPosition = hit.Position;
                    return true;
                }

                // Only try the first world that has physics
                return false;
            }

            return false;
        }

        // ============================================================
        // PATH REQUEST
        // ============================================================

        private void RequestMoveTo(Vector3 worldTarget)
        {
            _destination = worldTarget;
            CancelPath();

            Vector3 playerPos = GetPlayerWorldPosition();

            if (_usePathfinding && AstarPath.active != null && AstarPath.active.graphs != null && AstarPath.active.graphs.Length > 0)
            {
                _pathPending = true;
                var path = ABPath.Construct(playerPos, worldTarget, OnPathComplete);
                AstarPath.StartPath(path);

                if (_logPathEvents)
                    Debug.Log($"[ClickToMoveHandler] Path requested: {playerPos} -> {worldTarget}");
            }
            else
            {
                // Direct movement fallback (no pathfinding graph available)
                _pathWaypoints = new Vector3[] { worldTarget };
                _currentWaypointIndex = 0;
                _hasActivePath = true;
                _pathPending = false;

                if (_logPathEvents)
                    Debug.Log($"[ClickToMoveHandler] Direct move to {worldTarget} (no pathfinding)");
            }
        }

        private void OnPathComplete(Path path)
        {
            _pathPending = false;

            if (path.error)
            {
                Debug.LogWarning($"[ClickToMoveHandler] Path error: {path.errorLog}");

                // Fallback to direct movement on path error
                _pathWaypoints = new Vector3[] { _destination };
                _currentWaypointIndex = 0;
                _hasActivePath = true;
                return;
            }

            var vectorPath = path.vectorPath;
            _pathWaypoints = new Vector3[vectorPath.Count];
            for (int i = 0; i < vectorPath.Count; i++)
                _pathWaypoints[i] = vectorPath[i];

            _currentWaypointIndex = 0;
            _hasActivePath = true;

            if (_logPathEvents)
                Debug.Log($"[ClickToMoveHandler] Path complete: {_pathWaypoints.Length} waypoints");
        }

        // ============================================================
        // PATH FOLLOWING
        // ============================================================

        private void FollowPath()
        {
            if (_pathWaypoints == null || _currentWaypointIndex >= _pathWaypoints.Length)
            {
                CancelPath();
                return;
            }

            Vector3 playerPos = GetPlayerWorldPosition();
            Vector3 target = _pathWaypoints[_currentWaypointIndex];

            // Flatten to XZ for distance check
            Vector3 toTarget = target - playerPos;
            toTarget.y = 0;

            bool isFinalWaypoint = (_currentWaypointIndex == _pathWaypoints.Length - 1);
            float reachDist = isFinalWaypoint ? _destinationReachDistance : _waypointReachDistance;

            // Advance through reached waypoints
            while (toTarget.sqrMagnitude <= reachDist * reachDist)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _pathWaypoints.Length)
                {
                    if (_logPathEvents) Debug.Log("[ClickToMoveHandler] Destination reached");
                    CancelPath();
                    return;
                }
                target = _pathWaypoints[_currentWaypointIndex];
                toTarget = target - playerPos;
                toTarget.y = 0;
                isFinalWaypoint = (_currentWaypointIndex == _pathWaypoints.Length - 1);
                reachDist = isFinalWaypoint ? _destinationReachDistance : _waypointReachDistance;
            }

            // Compute camera-relative input direction
            Vector3 worldDir = toTarget.normalized;
            float2 cameraRelativeDir = WorldToCameraRelativeInput(worldDir);

            PlayerInputState.PathMoveDirection = cameraRelativeDir;
            PlayerInputState.IsPathFollowing = true;
        }

        // ============================================================
        // WORLD -> CAMERA-RELATIVE CONVERSION
        // ============================================================

        /// <summary>
        /// Converts a world-space XZ direction into camera-relative input values.
        ///
        /// PlayerMovementSystem's screen-relative branch computes:
        ///   moveDir = (screenUp * Vertical) + (screenRight * Horizontal)
        ///
        /// We compute the inverse via dot products:
        ///   x = dot(worldDir, cameraRight)   -> maps to Horizontal/PathMoveX
        ///   y = dot(worldDir, cameraForward)  -> maps to Vertical/PathMoveY
        /// </summary>
        private float2 WorldToCameraRelativeInput(Vector3 worldDir)
        {
            var camera = Camera.main;
            if (camera == null) return float2.zero;

            Vector3 camForward = camera.transform.forward;
            camForward.y = 0;

            // EPIC 18.15: For top-down (MOBA) and steep isometric (ARPG) cameras,
            // camera.forward is nearly vertical — flattened XZ is near-zero.
            // Fall back to camera's yaw rotation to get a stable horizontal heading.
            // This matches PlayerMovementSystem's CameraYaw fallback.
            if (camForward.sqrMagnitude < 0.001f)
            {
                float yaw = camera.transform.eulerAngles.y * Mathf.Deg2Rad;
                camForward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            }
            camForward.Normalize();

            Vector3 camRight = camera.transform.right;
            camRight.y = 0;
            if (camRight.sqrMagnitude < 0.001f)
            {
                // Derive right from forward (90° rotation in XZ)
                camRight = new Vector3(camForward.z, 0f, -camForward.x);
            }
            camRight.Normalize();

            float x = Vector3.Dot(worldDir, camRight);
            float y = Vector3.Dot(worldDir, camForward);

            float2 result = new float2(x, y);
            float mag = math.length(result);
            if (mag > 1f) result /= mag;

            return result;
        }

        // ============================================================
        // UTILITY
        // ============================================================

        /// <summary>
        /// Cancel the current path and clear all path-following state.
        /// </summary>
        public void CancelPath()
        {
            _hasActivePath = false;
            _pathPending = false;
            _pathWaypoints = null;
            _currentWaypointIndex = 0;

            PlayerInputState.IsPathFollowing = false;
            PlayerInputState.PathMoveDirection = float2.zero;
        }

        /// <summary>Whether a path is currently being followed.</summary>
        public bool IsFollowingPath => _hasActivePath;

        /// <summary>Whether a path request is pending (waiting for A* callback).</summary>
        public bool IsPathPending => _pathPending;

        /// <summary>
        /// Get the local player's world position from ECS.
        /// Searches ClientWorld first (NetCode), then all worlds as fallback.
        /// Falls back to camera position if ECS query fails.
        /// </summary>
        private Vector3 GetPlayerWorldPosition()
        {
            // Find the ClientWorld (NetCode separates worlds — player lives here, not DefaultGameObjectInjectionWorld)
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;

                var em = world.EntityManager;

                // Primary: local ghost (client-side networked)
                using var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                    ComponentType.ReadOnly<PlayerTag>());

                if (!query.IsEmpty)
                {
                    var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        var pos = em.GetComponentData<LocalTransform>(entities[0]).Position;
                        entities.Dispose();
                        return pos;
                    }
                    entities.Dispose();
                }

                // Fallback: non-networked (no GhostOwnerIsLocal)
                using var fallback = em.CreateEntityQuery(
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<PlayerTag>());

                if (!fallback.IsEmpty)
                {
                    var entities = fallback.ToEntityArray(Unity.Collections.Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        var pos = em.GetComponentData<LocalTransform>(entities[0]).Position;
                        entities.Dispose();
                        return pos;
                    }
                    entities.Dispose();
                }
            }

            // Last resort: camera position (likely wrong — should not reach here)
            if (_logPathEvents)
                Debug.LogWarning("[ClickToMoveHandler] ECS player query failed across ALL worlds, falling back to camera position");
            var camera = Camera.main;
            return camera != null ? camera.transform.position : Vector3.zero;
        }

        // ============================================================
        // DEBUG GIZMOS
        // ============================================================

        private void OnDrawGizmos()
        {
            if (!_drawPathGizmos || !_hasActivePath || _pathWaypoints == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _pathWaypoints.Length - 1; i++)
            {
                Gizmos.DrawLine(_pathWaypoints[i], _pathWaypoints[i + 1]);
                Gizmos.DrawWireSphere(_pathWaypoints[i], 0.15f);
            }

            if (_pathWaypoints.Length > 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_pathWaypoints[_pathWaypoints.Length - 1], 0.25f);
            }

            if (_currentWaypointIndex < _pathWaypoints.Length)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_pathWaypoints[_currentWaypointIndex], 0.3f);
            }
        }
    }
}
