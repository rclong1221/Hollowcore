using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Physics.Systems;
using Player.Components;
using DIG.Survival.Physics;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// Diagnostics for EPIC 13.19 Ragdoll Hips Sync.
    /// Enable via code or inspector to debug ragdoll sync issues.
    /// </summary>
    public static class RagdollHipsSyncDiagnostics
    {
        /// <summary>
        /// Enable to log server-side sync updates (runs on server).
        /// </summary>
        public static bool ServerLogging = false;

        /// <summary>
        /// Enable to log client-side sync reads (runs on client).
        /// </summary>
        public static bool ClientLogging = false;

        /// <summary>
        /// Enable to log position comparison between server sync and client visual.
        /// Useful for testing if bodies land in the same spot.
        /// </summary>
        public static bool PositionComparisonLogging = false;

        /// <summary>
        /// Log interval in frames to prevent spam (0 = every frame when state changes).
        /// </summary>
        public static int LogIntervalFrames = 30;

        private static int _lastServerLogFrame = -999;
        private static int _lastClientLogFrame = -999;
        private static int _lastComparisonLogFrame = -999;

        // Track positions per entity for multi-ragdoll support
        private static System.Collections.Generic.Dictionary<int, float3> _lastServerPositions = new();
        private static System.Collections.Generic.Dictionary<int, float3> _lastClientVisualPositions = new();

        public static bool ShouldLogServer()
        {
            if (!ServerLogging) return false;
            if (Time.frameCount - _lastServerLogFrame < LogIntervalFrames) return false;
            _lastServerLogFrame = Time.frameCount;
            return true;
        }

        public static bool ShouldLogClient()
        {
            if (!ClientLogging) return false;
            if (Time.frameCount - _lastClientLogFrame < LogIntervalFrames) return false;
            _lastClientLogFrame = Time.frameCount;
            return true;
        }

        public static bool ShouldLogComparison()
        {
            if (!PositionComparisonLogging) return false;
            if (Time.frameCount - _lastComparisonLogFrame < LogIntervalFrames) return false;
            _lastComparisonLogFrame = Time.frameCount;
            return true;
        }

        /// <summary>
        /// Force log regardless of interval (for state changes).
        /// </summary>
        public static bool ForceLog => ServerLogging || ClientLogging;

        /// <summary>
        /// Track server position per entity and log movement (body being pushed/moved).
        /// </summary>
        public static void TrackServerPosition(float3 position, int entityIndex)
        {
            if (!_lastServerPositions.TryGetValue(entityIndex, out var lastPos))
            {
                _lastServerPositions[entityIndex] = position;
                return;
            }

            float moved = math.distance(lastPos, position);
            if (moved > 0.1f && ServerLogging)
            {
                // Debug.Log($"[RagdollSync:Server] Entity {entityIndex} moved {moved:F2}m: {lastPos} -> {position}");
            }
            _lastServerPositions[entityIndex] = position;
        }

        /// <summary>
        /// Track client visual position and compare with server sync position.
        /// </summary>
        public static void TrackClientPosition(int entityIndex, float3 serverPos, Vector3 visualPos)
        {
            float desync = math.distance(serverPos, new float3(visualPos.x, visualPos.y, visualPos.z));

            // Always log significant desync
            if (desync > 0.5f)
            {
                // Debug.LogWarning($"[RagdollSync:DESYNC] Entity {entityIndex} desync={desync:F2}m! Server={serverPos} Visual={visualPos}");
            }
            else if (PositionComparisonLogging && ShouldLogComparison())
            {
                // Debug.Log($"[RagdollSync:Compare] Entity {entityIndex} Server={serverPos} Visual={visualPos} Diff={desync:F3}m");
            }

            _lastClientVisualPositions[entityIndex] = new float3(visualPos.x, visualPos.y, visualPos.z);
        }

        /// <summary>
        /// Reset tracking for a specific entity when ragdoll ends.
        /// </summary>
        public static void ResetTracking(int entityIndex)
        {
            _lastServerPositions.Remove(entityIndex);
            _lastClientVisualPositions.Remove(entityIndex);
        }

        /// <summary>
        /// Reset all tracking.
        /// </summary>
        public static void ResetTracking()
        {
            _lastServerPositions.Clear();
            _lastClientVisualPositions.Clear();
        }
    }

    /// <summary>
    /// SERVER-ONLY: Copies Pelvis LocalTransform to RagdollHipsSync component (EPIC 13.19).
    ///
    /// Runs after physics so Pelvis position reflects the latest simulation.
    /// The RagdollHipsSync component is then replicated to clients via GhostField.
    ///
    /// This is the optimal pattern for Unity NetCode:
    /// - Server simulates physics (already happening in RagdollTransitionSystem)
    /// - Server writes to GhostField component
    /// - NetCode replicates with delta compression
    /// - Clients read and apply to presentation
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class RagdollHipsSyncServerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<RagdollController>();
        }

        protected override void OnUpdate()
        {
            int activeCount = 0;
            int totalCount = 0;

            foreach (var (ragdollCtrl, hipsSync, entity) in
                SystemAPI.Query<RefRO<RagdollController>, RefRW<RagdollHipsSync>>()
                    .WithEntityAccess())
            {
                totalCount++;
                
                // FORCED DEBUG: Log every player's ragdoll state and detect stale prefab references
                /*
                if (totalCount <= 3) // Limit spam
                {
                    var dbgPelvis = ragdollCtrl.ValueRO.Pelvis;
                    bool ltwExists = SystemAPI.HasComponent<LocalToWorld>(dbgPelvis);
                    Debug.Log($"[RagdollSync:Server:DEBUG] Entity {entity.Index}:{entity.Version} | IsRagdolled={ragdollCtrl.ValueRO.IsRagdolled} | Pelvis={dbgPelvis.Index}:{dbgPelvis.Version} LtwExists={ltwExists}");
                }
                */

                // Not ragdolling - clear sync state
                if (!ragdollCtrl.ValueRO.IsRagdolled)
                {
                    if (hipsSync.ValueRO.IsActive)
                    {
                        hipsSync.ValueRW.IsActive = false;
                        RagdollHipsSyncDiagnostics.ResetTracking(entity.Index);

                        if (RagdollHipsSyncDiagnostics.ForceLog)
                        {
                            // Debug.Log($"[RagdollSync:Server] Entity {entity.Index} ragdoll ENDED");
                        }
                    }
                    continue;
                }

                // Get Pelvis entity from RagdollController
                Entity pelvis = ragdollCtrl.ValueRO.Pelvis;
                if (pelvis == Entity.Null)
                {
                    if (RagdollHipsSyncDiagnostics.ForceLog)
                    {
                        // Debug.LogWarning($"[RagdollHipsSync:Server] Entity {entity.Index} IsRagdolled=true but Pelvis=NULL!");
                    }
                    continue;
                }

                // Read Pelvis LocalTransform (updated by physics simulation)
                // NOTE: Use LocalToWorld for actual world position, since LocalTransform may be relative to parent
                if (!SystemAPI.HasComponent<LocalToWorld>(pelvis))
                {
                    if (RagdollHipsSyncDiagnostics.ForceLog)
                    {
                        // Debug.LogWarning($"[RagdollHipsSync:Server] Entity {entity.Index} Pelvis {pelvis.Index} has no LocalToWorld!");
                    }
                    continue;
                }

                var pelvisLtw = SystemAPI.GetComponent<LocalToWorld>(pelvis);
                var pelvisPosition = pelvisLtw.Position;
                var pelvisRotation = pelvisLtw.Rotation;
                
                // Read Pelvis velocity for momentum sync
                var pelvisVelocity = SystemAPI.HasComponent<Unity.Physics.PhysicsVelocity>(pelvis)
                    ? SystemAPI.GetComponent<Unity.Physics.PhysicsVelocity>(pelvis)
                    : new Unity.Physics.PhysicsVelocity();

                // Detect first activation
                bool wasActive = hipsSync.ValueRO.IsActive;

                // Write to sync component (will replicate via GhostField)
                hipsSync.ValueRW.Position = pelvisPosition;
                hipsSync.ValueRW.Rotation = pelvisRotation;
                hipsSync.ValueRW.LinearVelocity = pelvisVelocity.Linear;
                hipsSync.ValueRW.IsActive = true;

                activeCount++;

                // Track server position for movement detection
                RagdollHipsSyncDiagnostics.TrackServerPosition(pelvisPosition, entity.Index);

                // Log on state change or periodic interval
                if (!wasActive && RagdollHipsSyncDiagnostics.ForceLog)
                {
                    // Debug.Log($"[RagdollSync:Server] Entity {entity.Index} ragdoll STARTED, Pelvis={pelvis.Index}, WorldPos={pelvisPosition}, Velocity={pelvisVelocity.Linear}");
                }
                else if (RagdollHipsSyncDiagnostics.ShouldLogServer())
                {
                    // Debug.Log($"[RagdollSync:Server] Entity {entity.Index} Pelvis WorldPos={pelvisPosition} Vel={pelvisVelocity.Linear}");
                }
            }

            // Periodic summary log
            if (RagdollHipsSyncDiagnostics.ShouldLogServer() && totalCount > 0)
            {
                // Debug.Log($"[RagdollHipsSync:Server] Summary: {activeCount}/{totalCount} players ragdolling");
            }
        }
    }

    /// <summary>
    /// CLIENT-ONLY: Reads RagdollHipsSync and passes to RagdollPresentationBridge (EPIC 13.19).
    ///
    /// For non-owned players, this provides the server-authoritative hips position
    /// that the kinematic presentation ragdoll should follow.
    ///
    /// For owned players, this system does nothing - they run local physics.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class RagdollHipsSyncReaderSystem : SystemBase
    {
        private Unity.NetCode.Hybrid.GhostPresentationGameObjectSystem _presentationSystem;

        protected override void OnCreate()
        {
            RequireForUpdate<RagdollHipsSync>();
        }

        protected override void OnUpdate()
        {
            // FORCED DEBUG: One-time log to confirm system is running
            /*
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            if ((int)(currentTime * 2) % 10 == 0)
            {
                int ragdollCount = 0;
                foreach (var _ in SystemAPI.Query<RefRO<RagdollHipsSync>>())
                    ragdollCount++;
                Debug.Log($"[RagdollSync:Client:HEARTBEAT] t={currentTime:F1} RagdollHipsSync entities={ragdollCount}");
            }
            */
            
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<Unity.NetCode.Hybrid.GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                {
                    // Debug.LogWarning("[RagdollHipsSync:Client] GhostPresentationGameObjectSystem not found!");
                    return;
                }
            }

            int processedCount = 0;
            int skippedOwned = 0;
            int skippedNotDead = 0;
            int skippedNotActive = 0;
            int skippedNoGO = 0;
            int skippedNoBridge = 0;

            // Query ALL players with ragdoll sync data
            foreach (var (deathState, hipsSync, ghost, entity) in
                SystemAPI.Query<RefRO<DeathState>, RefRO<RagdollHipsSync>, RefRO<GhostInstance>>()
                    .WithEntityAccess())
            {
                // FORCED DEBUG: Log every entity's DeathPhase to find replication issue
                /*
                float currentTimeDbg = (float)SystemAPI.Time.ElapsedTime;
                if ((int)currentTimeDbg % 5 == 0)
                {
                    // GhostOwnerIsLocal is an enableable component - must check IsComponentEnabled, not just HasComponent!
                    bool hasGhostOwnerDbg = EntityManager.HasComponent<GhostOwnerIsLocal>(entity);
                    bool isOwnedDbg = hasGhostOwnerDbg && EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity);
                    Debug.Log($"[RagdollSync:Client:ENTITY] E={entity.Index} GhostId={ghost.ValueRO.ghostId} Phase={deathState.ValueRO.Phase} IsActive={hipsSync.ValueRO.IsActive} Owned={isOwnedDbg} (HasComponent={hasGhostOwnerDbg})");
                }
                */
                
                // Skip if not in ragdoll state
                bool shouldRagdoll = deathState.ValueRO.Phase == DeathPhase.Dead ||
                                     deathState.ValueRO.Phase == DeathPhase.Downed;
                if (!shouldRagdoll)
                {
                    // Log what phase we actually see for debugging replication issues
                    if (RagdollHipsSyncDiagnostics.ShouldLogClient() && hipsSync.ValueRO.IsActive)
                    {
                        // Debug.LogWarning($"[RagdollHipsSync:Client] Entity {entity.Index} has IsActive=TRUE but DeathState.Phase={deathState.ValueRO.Phase} (expected Dead/Downed). DeathState replication issue?");
                    }
                    skippedNotDead++;
                    continue;
                }

                // Skip owned players - they run local physics simulation
                // GhostOwnerIsLocal is an ENABLEABLE component - all ghosts have it but only the local one has it ENABLED!
                bool hasGhostOwner = EntityManager.HasComponent<GhostOwnerIsLocal>(entity);
                bool isOwned = hasGhostOwner && EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity);
                if (isOwned)
                {
                    skippedOwned++;
                    continue;
                }

                // Check if sync is active
                if (!hipsSync.ValueRO.IsActive)
                {
                    skippedNotActive++;

                    // This is a key diagnostic - dead but no sync data from server
                    if (RagdollHipsSyncDiagnostics.ShouldLogClient())
                    {
                        // Debug.LogWarning($"[RagdollHipsSync:Client] Entity {entity.Index} is Dead/Downed but RagdollHipsSync.IsActive=FALSE! Server not sending sync?");
                    }
                    continue;
                }

                // Get presentation GameObject
                var go = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (go == null)
                {
                    skippedNoGO++;
                    // ALWAYS LOG THIS FAILURE for now
                    // Debug.LogWarning($"[RagdollHipsSync:Client] Entity {entity.Index} (GhostId={ghost.ValueRO.ghostId}) has no presentation GameObject! Cannot sync.");
                    continue;
                }

                var bridge = go.GetComponent<Player.Animation.RagdollPresentationBridge>();
                if (bridge == null)
                {
                    skippedNoBridge++;
                    // ALWAYS LOG THIS FAILURE for now
                    // Debug.LogWarning($"[RagdollHipsSync:Client] Entity {entity.Index} GameObject '{go.name}' [ID:{go.GetInstanceID()}] has no RagdollPresentationBridge!");
                    continue;
                }

                // Pass server-authoritative sync data to presentation (with velocity and ghostId for push detection)
                var pos = hipsSync.ValueRO.Position;
                var rot = hipsSync.ValueRO.Rotation;
                var vel = hipsSync.ValueRO.LinearVelocity;
                int ghostId = ghost.ValueRO.ghostId;
                bridge.SetRemoteSyncData(
                    new Vector3(pos.x, pos.y, pos.z),
                    new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                    new Vector3(vel.x, vel.y, vel.z),
                    hipsSync.ValueRO.IsActive,
                    ghostId
                );

                processedCount++;

                // Track and compare positions - this will auto-log significant desyncs
                var visualPos = bridge.RagdollRoot != null ? bridge.RagdollRoot.position : Vector3.zero;
                RagdollHipsSyncDiagnostics.TrackClientPosition(entity.Index, pos, visualPos);

                // Unconditional log for now
                if (RagdollHipsSyncDiagnostics.ClientLogging)
                {
                    // Debug.Log($"[RagdollSync:Client] Entity {entity.Index} sent sync Pos={pos} to '{go.name}' [ID:{go.GetInstanceID()}] (visual at {visualPos})");
                }
            }

            // Periodic summary - FORCED when there are any skipped entities
            int total = processedCount + skippedOwned + skippedNotDead + skippedNotActive + skippedNoGO + skippedNoBridge;
            if (total > 0)
            {
                // Log every ~5 seconds if there are any skipped entities
                float currentTimeForSummary = (float)SystemAPI.Time.ElapsedTime;
                if ((int)currentTimeForSummary % 5 == 0 && RagdollHipsSyncDiagnostics.ClientLogging)
                {
                    // Debug.Log($"[RagdollHipsSync:Client:SUMMARY] Processed={processedCount}, SkippedOwned={skippedOwned}, SkippedAlive={skippedNotDead}, SkippedNoSync={skippedNotActive}, SkippedNoGO={skippedNoGO}, SkippedNoBridge={skippedNoBridge}");
                }
            }
        }
    }
}
