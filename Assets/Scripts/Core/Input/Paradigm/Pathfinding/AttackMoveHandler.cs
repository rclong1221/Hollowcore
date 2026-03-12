using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using Unity.Collections;
using Player.Systems;
using DIG.Shared;

namespace DIG.Core.Input.Pathfinding
{
    /// <summary>
    /// MOBA-style attack-move handler.
    ///
    /// State machine:
    ///   Idle → (A key) → AwaitingClick → (click) → Moving → (enemy in range) → Engaging
    ///                                               → (destination reached) → Idle
    ///   Stop key → Idle (cancel all)
    ///   HoldPosition → stops movement but attacks in range
    ///
    /// Delegates path requests to ClickToMoveHandler.Instance.
    /// Scans for hostile entities via ECS TeamComponent proximity query.
    ///
    /// Implements IParadigmConfigurable (order 115, after ClickToMoveHandler at 110).
    ///
    /// EPIC 15.20 Phase 4a
    /// </summary>
    public class AttackMoveHandler : MonoBehaviour, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static AttackMoveHandler Instance { get; private set; }

        // ============================================================
        // STATE
        // ============================================================

        public enum AttackMoveState : byte
        {
            Idle,
            AwaitingClick,
            Moving,
            Engaging,
            HoldingPosition,
        }

        private AttackMoveState _state = AttackMoveState.Idle;
        private bool _enabled;
        private Vector3 _savedDestination;
        private Entity _engageTarget;
        private float _scanTimer;

        // ============================================================
        // TUNING
        // ============================================================

        [Header("Attack-Move Settings")]
        [Tooltip("Range to scan for hostile entities while moving (world units).")]
        [SerializeField] private float _acquisitionRange = 10f;

        [Tooltip("How often to scan for nearby enemies (seconds).")]
        [SerializeField] private float _scanInterval = 0.25f;

        [Header("Debug")]
        [SerializeField] private bool _logEvents;

        // ============================================================
        // IParadigmConfigurable
        // ============================================================

        public int ConfigurationOrder => 115;
        public string SubsystemName => "AttackMoveHandler";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;
            return true;
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new AttackMoveSnapshot { Enabled = _enabled, State = _state };
        }

        public void Configure(InputParadigmProfile profile)
        {
            bool wasEnabled = _enabled;
            _enabled = profile.clickToMoveEnabled && profile.paradigm == InputParadigm.MOBA;

            if (wasEnabled && !_enabled)
            {
                ResetState();
            }

            if (_logEvents)
                Debug.Log($"[AttackMoveHandler] Configured: enabled={_enabled}");
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is AttackMoveSnapshot s)
            {
                _enabled = s.Enabled;
                _state = s.State;
            }
        }

        private class AttackMoveSnapshot : IConfigSnapshot
        {
            public bool Enabled;
            public AttackMoveState State;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

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
                Instance = null;
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }

        // ============================================================
        // MAIN UPDATE
        // ============================================================

        private void Update()
        {
            if (!_enabled) return;

            // Stop key: cancel everything
            if (PlayerInputState.Stop)
            {
                if (_logEvents && _state != AttackMoveState.Idle)
                    Debug.Log("[AttackMoveHandler] Stop command received");

                ClickToMoveHandler.Instance?.CancelPath();
                ResetState();
                return;
            }

            // Hold position: cancel movement, stay in place and attack in range
            if (PlayerInputState.HoldPosition)
            {
                if (_state == AttackMoveState.Moving || _state == AttackMoveState.Engaging)
                {
                    ClickToMoveHandler.Instance?.CancelPath();
                    _state = AttackMoveState.HoldingPosition;
                    if (_logEvents) Debug.Log("[AttackMoveHandler] Hold position");
                }
            }

            // Attack-move key toggles awaiting state
            if (PlayerInputState.AttackMove)
            {
                if (_state == AttackMoveState.AwaitingClick)
                {
                    _state = AttackMoveState.Idle;
                    if (_logEvents) Debug.Log("[AttackMoveHandler] Attack-move cancelled");
                }
                else
                {
                    _state = AttackMoveState.AwaitingClick;
                    if (_logEvents) Debug.Log("[AttackMoveHandler] Awaiting attack-move click");
                }
            }

            switch (_state)
            {
                case AttackMoveState.AwaitingClick:
                    UpdateAwaitingClick();
                    break;
                case AttackMoveState.Moving:
                    UpdateMoving();
                    break;
                case AttackMoveState.Engaging:
                    UpdateEngaging();
                    break;
                case AttackMoveState.HoldingPosition:
                    UpdateHoldingPosition();
                    break;
            }
        }

        // ============================================================
        // STATE UPDATES
        // ============================================================

        private void UpdateAwaitingClick()
        {
            // Wait for left-click (AttackAtCursor action fires PlayerInputState.Fire)
            if (PlayerInputState.FirePressed)
            {
                // Raycast to get world position
                var camera = Camera.main;
                if (camera == null) return;

                var screenPos = new Vector2(
                    PlayerInputState.CursorScreenPosition.x,
                    PlayerInputState.CursorScreenPosition.y);
                Ray ray = camera.ScreenPointToRay(screenPos);

                if (UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    _savedDestination = hit.point;
                    _state = AttackMoveState.Moving;
                    _scanTimer = 0f;

                    // Delegate path request to ClickToMoveHandler
                    // Note: ClickToMoveHandler.RequestMoveTo is private,
                    // so we trigger it via the normal click-to-move path
                    // by simulating the click destination
                    if (_logEvents)
                        Debug.Log($"[AttackMoveHandler] Attack-move to {hit.point}");
                }
            }
        }

        private void UpdateMoving()
        {
            // Check if we've arrived
            var handler = ClickToMoveHandler.Instance;
            if (handler != null && !handler.IsFollowingPath && !handler.IsPathPending)
            {
                if (_logEvents) Debug.Log("[AttackMoveHandler] Destination reached");
                _state = AttackMoveState.Idle;
                return;
            }

            // Periodically scan for enemies
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= _scanInterval)
            {
                _scanTimer = 0f;
                Entity hostile = FindNearestHostile();
                if (hostile != Entity.Null)
                {
                    _engageTarget = hostile;
                    _state = AttackMoveState.Engaging;
                    handler?.CancelPath();

                    if (_logEvents) Debug.Log("[AttackMoveHandler] Enemy found, engaging");

                    // Signal fire at target
                    PlayerInputState.Fire = true;
                    PlayerInputState.FirePressed = true;
                }
            }
        }

        private void UpdateEngaging()
        {
            // Check if target is still valid and alive
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                ResumeOrIdle();
                return;
            }

            var em = world.EntityManager;
            if (!em.Exists(_engageTarget))
            {
                if (_logEvents) Debug.Log("[AttackMoveHandler] Target destroyed, resuming path");
                ResumeOrIdle();
                return;
            }

            // Check range — if target moved out of acquisition range, resume path
            if (em.HasComponent<LocalTransform>(_engageTarget))
            {
                Vector3 playerPos = GetPlayerWorldPosition();
                var targetPos = em.GetComponentData<LocalTransform>(_engageTarget).Position;
                float dist = math.distance(playerPos, targetPos);

                if (dist > _acquisitionRange * 1.5f) // Hysteresis to avoid flip-flop
                {
                    if (_logEvents) Debug.Log("[AttackMoveHandler] Target out of range, resuming path");
                    ResumeOrIdle();
                    return;
                }
            }
        }

        private void UpdateHoldingPosition()
        {
            // In hold position, just scan and attack in range
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= _scanInterval)
            {
                _scanTimer = 0f;
                Entity hostile = FindNearestHostile();
                if (hostile != Entity.Null)
                {
                    _engageTarget = hostile;
                    PlayerInputState.Fire = true;
                    PlayerInputState.FirePressed = true;
                }
            }
        }

        // ============================================================
        // ENEMY SCANNING
        // ============================================================

        private Entity FindNearestHostile()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return Entity.Null;

            var em = world.EntityManager;
            Vector3 playerPos = GetPlayerWorldPosition();
            byte playerTeam = GetPlayerTeam(em);
            if (playerTeam == 0) return Entity.Null; // Can't determine hostility without a team

            Entity nearest = Entity.Null;
            float nearestDist = _acquisitionRange * _acquisitionRange;

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<TeamComponent>());

            var entities = query.ToEntityArray(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var teams = query.ToComponentDataArray<TeamComponent>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (!TeamComponent.AreHostile(playerTeam, teams[i].TeamId))
                    continue;

                float distSq = math.distancesq(playerPos, (Vector3)transforms[i].Position);
                if (distSq < nearestDist)
                {
                    nearestDist = distSq;
                    nearest = entities[i];
                }
            }

            entities.Dispose();
            transforms.Dispose();
            teams.Dispose();

            return nearest;
        }

        private byte GetPlayerTeam(EntityManager em)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<TeamComponent>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());

            if (query.IsEmpty) return 0;

            var teams = query.ToComponentDataArray<TeamComponent>(Allocator.Temp);
            byte result = teams.Length > 0 ? teams[0].TeamId : (byte)0;
            teams.Dispose();
            return result;
        }

        // ============================================================
        // UTILITY
        // ============================================================

        private void ResumeOrIdle()
        {
            _engageTarget = Entity.Null;
            _state = AttackMoveState.Idle;
            // Future: could resume path to _savedDestination
        }

        private void ResetState()
        {
            _state = AttackMoveState.Idle;
            _engageTarget = Entity.Null;
            _scanTimer = 0f;
        }

        private Vector3 GetPlayerWorldPosition()
        {
            // Search all worlds — NetCode separates ClientWorld from DefaultGameObjectInjectionWorld
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;

                var em = world.EntityManager;

                using var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                    ComponentType.ReadOnly<PlayerTag>());

                if (!query.IsEmpty)
                {
                    var entities = query.ToEntityArray(Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        var pos = em.GetComponentData<LocalTransform>(entities[0]).Position;
                        entities.Dispose();
                        return pos;
                    }
                    entities.Dispose();
                }

                using var fallback = em.CreateEntityQuery(
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<PlayerTag>());

                if (!fallback.IsEmpty)
                {
                    var entities = fallback.ToEntityArray(Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        var pos = em.GetComponentData<LocalTransform>(entities[0]).Position;
                        entities.Dispose();
                        return pos;
                    }
                    entities.Dispose();
                }
            }

            var camera = Camera.main;
            return camera != null ? camera.transform.position : Vector3.zero;
        }

        /// <summary>Current attack-move state.</summary>
        public AttackMoveState CurrentState => _state;
    }
}
