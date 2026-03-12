using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Pathfinding;

namespace DIG.Core.Input.Pathfinding
{
    /// <summary>
    /// Managed MonoBehaviour bridge for NPC A* pathfinding.
    ///
    /// Queries ECS for entities with NPCPathfindingTag and manages their
    /// A* path requests. Writes resulting movement direction back to
    /// NPC movement components.
    ///
    /// This is scaffolding — the actual AI decision-making (target selection,
    /// aggro response, patrol routes) is deferred to the combat system EPIC.
    /// This class provides the pathfinding infrastructure.
    ///
    /// EPIC 15.20 Phase 4c
    /// </summary>
    public class NPCPathfindingBridge : MonoBehaviour
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static NPCPathfindingBridge Instance { get; private set; }

        // ============================================================
        // STATE
        // ============================================================

        private struct NPCPathState
        {
            public Vector3[] Waypoints;
            public int CurrentWaypointIndex;
            public float RepathTimer;
            public bool HasPath;
            public bool PathPending;
            public Vector3 Destination;
        }

        private Dictionary<Entity, NPCPathState> _npcPaths = new Dictionary<Entity, NPCPathState>();

        // ============================================================
        // TUNING
        // ============================================================

        [Header("NPC Pathfinding")]
        [Tooltip("Distance at which a waypoint is considered reached.")]
        [SerializeField] private float _waypointReachDistance = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _logEvents;

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ============================================================
        // PUBLIC API (for AI systems to call)
        // ============================================================

        /// <summary>
        /// Request an NPC to pathfind to a world position.
        /// Called by AI decision systems (aggro, patrol, flee, etc.).
        /// </summary>
        public void RequestPath(Entity npcEntity, Vector3 destination)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            if (!em.Exists(npcEntity) || !em.HasComponent<NPCPathfindingTag>(npcEntity))
                return;

            Vector3 npcPos = em.GetComponentData<LocalTransform>(npcEntity).Position;

            if (AstarPath.active != null && AstarPath.active.graphs != null && AstarPath.active.graphs.Length > 0)
            {
                var state = GetOrCreateState(npcEntity);
                state.PathPending = true;
                state.Destination = destination;
                _npcPaths[npcEntity] = state;

                var path = ABPath.Construct(npcPos, destination, p => OnPathComplete(npcEntity, p));
                AstarPath.StartPath(path);

                if (_logEvents)
                    Debug.Log($"[NPCPathfindingBridge] Path requested for {npcEntity}: {npcPos} -> {destination}");
            }
            else
            {
                // Direct movement fallback
                var state = GetOrCreateState(npcEntity);
                state.Waypoints = new Vector3[] { destination };
                state.CurrentWaypointIndex = 0;
                state.HasPath = true;
                state.PathPending = false;
                state.Destination = destination;
                _npcPaths[npcEntity] = state;

                if (_logEvents)
                    Debug.Log($"[NPCPathfindingBridge] Direct path for {npcEntity} (no A* graph)");
            }
        }

        /// <summary>
        /// Cancel any active path for an NPC.
        /// </summary>
        public void CancelPath(Entity npcEntity)
        {
            if (_npcPaths.ContainsKey(npcEntity))
            {
                var state = _npcPaths[npcEntity];
                state.HasPath = false;
                state.PathPending = false;
                state.Waypoints = null;
                _npcPaths[npcEntity] = state;
            }
        }

        /// <summary>
        /// Check if an NPC is currently following a path.
        /// </summary>
        public bool IsFollowingPath(Entity npcEntity)
        {
            return _npcPaths.TryGetValue(npcEntity, out var state) && state.HasPath;
        }

        /// <summary>
        /// Get the movement direction for an NPC entity (normalized XZ direction to next waypoint).
        /// Returns float3.zero if no active path.
        /// AI systems should call this each frame to get the movement input for their NPC.
        /// </summary>
        public float3 GetMovementDirection(Entity npcEntity)
        {
            if (!_npcPaths.TryGetValue(npcEntity, out var state) || !state.HasPath)
                return float3.zero;

            if (state.Waypoints == null || state.CurrentWaypointIndex >= state.Waypoints.Length)
            {
                CancelPath(npcEntity);
                return float3.zero;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return float3.zero;

            var em = world.EntityManager;
            if (!em.Exists(npcEntity)) return float3.zero;

            Vector3 npcPos = em.GetComponentData<LocalTransform>(npcEntity).Position;
            Vector3 target = state.Waypoints[state.CurrentWaypointIndex];

            Vector3 toTarget = target - npcPos;
            toTarget.y = 0;

            // Advance through reached waypoints
            while (toTarget.sqrMagnitude <= _waypointReachDistance * _waypointReachDistance)
            {
                state.CurrentWaypointIndex++;
                if (state.CurrentWaypointIndex >= state.Waypoints.Length)
                {
                    state.HasPath = false;
                    _npcPaths[npcEntity] = state;

                    if (_logEvents)
                        Debug.Log($"[NPCPathfindingBridge] NPC {npcEntity} reached destination");
                    return float3.zero;
                }
                target = state.Waypoints[state.CurrentWaypointIndex];
                toTarget = target - npcPos;
                toTarget.y = 0;
            }

            _npcPaths[npcEntity] = state;
            return ((float3)toTarget.normalized);
        }

        // ============================================================
        // INTERNAL
        // ============================================================

        private void OnPathComplete(Entity npcEntity, Path path)
        {
            if (!_npcPaths.ContainsKey(npcEntity)) return;

            var state = _npcPaths[npcEntity];
            state.PathPending = false;

            if (path.error)
            {
                Debug.LogWarning($"[NPCPathfindingBridge] Path error for {npcEntity}: {path.errorLog}");
                // Fallback to direct movement
                state.Waypoints = new Vector3[] { state.Destination };
                state.CurrentWaypointIndex = 0;
                state.HasPath = true;
            }
            else
            {
                var vectorPath = path.vectorPath;
                state.Waypoints = new Vector3[vectorPath.Count];
                for (int i = 0; i < vectorPath.Count; i++)
                    state.Waypoints[i] = vectorPath[i];
                state.CurrentWaypointIndex = 0;
                state.HasPath = true;

                if (_logEvents)
                    Debug.Log($"[NPCPathfindingBridge] Path complete for {npcEntity}: {state.Waypoints.Length} waypoints");
            }

            _npcPaths[npcEntity] = state;
        }

        private NPCPathState GetOrCreateState(Entity npcEntity)
        {
            if (_npcPaths.TryGetValue(npcEntity, out var existing))
                return existing;
            return new NPCPathState();
        }

        private void Update()
        {
            // Clean up paths for destroyed entities
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            var toRemove = new List<Entity>();
            foreach (var kvp in _npcPaths)
            {
                if (!em.Exists(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var e in toRemove)
                _npcPaths.Remove(e);
        }
    }
}
