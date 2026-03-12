// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · EnemyHealthBarBridgeSystem
// Optimized bridge between ECS health data and world-space health bar UI
// Queries ServerWorld on hosts, ClientWorld on pure clients
// Updated in EPIC 15.15 to include combat state data
// Updated in EPIC 15.16 to integrate target lock for WhenTargeted visibility modes
// Updated in EPIC 15.16 (Optimization) to use data-driven config values
// Updated in EPIC 15.17 to integrate vision system LOS for WhenInLineOfSight mode
// Updated in EPIC 15.19 to integrate aggro system for WhenAggroed visibility mode
// Updated in EPIC 15.18 to integrate cursor hover and click-select for visibility modes
// Updated in EPIC 15.26 to defer when widget framework is active
// Fixed: Now works on pure clients by querying ClientWorld when ServerWorld unavailable
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;
using Player.Components;
using DIG.Combat.UI.WorldSpace; // EnemyHealthBarPool
using DIG.Combat.UI; // HasAggroOn
using DIG.Combat.Components;
using DIG.Targeting;
using DIG.Targeting.Components;
using DIG.Vision.Core;
namespace DIG.Combat.Bridges
{
    /// <summary>
    /// Optimized bridge system for enemy health bars.
    /// Queries ServerWorld on hosts, ClientWorld on pure clients.
    /// Note: Cannot be Burst-compiled due to managed UI interactions.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct EnemyHealthBarBridgeSystem : ISystem
    {
        private NativeHashMap<Entity, float> _lastHealthValues;

        // Lightweight struct for main thread UI updates
        private struct HealthBarData
        {
            public Entity Entity;
            public float3 Position;
            public float CurrentHealth;
            public float MaxHealth;
            public bool IsInCombat;
            public float TimeSinceCombatEnded;
            public bool IsInLineOfSight; // EPIC 15.17
            public bool HasAggroOnPlayer; // EPIC 15.19
        }
        
        public void OnCreate(ref SystemState state)
        {
            _lastHealthValues = new NativeHashMap<Entity, float>(128, Allocator.Persistent);
            // Distance values now read from pool config each frame
        }
        
        public void OnDestroy(ref SystemState state)
        {
            if (_lastHealthValues.IsCreated)
                _lastHealthValues.Dispose();
        }

        private int _diagFrameCounter;

        public void OnUpdate(ref SystemState state)
        {
            // EPIC 15.26: When widget framework is active, it handles projection,
            // culling, budget enforcement, and calls HealthBarWidgetAdapter which
            // drives the pool. Skip standalone processing to avoid double-updates.
            if (DIG.Widgets.Systems.WidgetProjectionSystem.FrameworkActive)
                return;

            var pool = EnemyHealthBarPool.Instance;
            if (pool == null)
            {
                #if UNITY_EDITOR
                if (_diagFrameCounter++ % 300 == 0)
                    UnityEngine.Debug.LogWarning("[HealthBarBridge] Pool is null — EnemyHealthBarPool not in scene?");
                #endif
                return;
            }

            // Data-driven config values from pool (EPIC 15.16 optimization)
            float maxShowDistanceSq = pool.MaxShowDistanceSq;
            float positionMatchToleranceSq = pool.PositionMatchToleranceSq;
            
            // Reset combat state counters at start of frame
            pool.BeginFrame();
            
            // Epic 15.16: Get the targeted entity from lock-on state
            // Since enemies don't use NetCode ghosts, we'll match by position
            Entity clientTargetEntity = Entity.Null;
            float3 clientTargetPosition = float3.zero;
            bool hasTargetPosition = false;
            var clientEM = state.EntityManager;
            
            // Query for local player's lock state
            foreach (var (lockState, entity) in
                SystemAPI.Query<RefRO<CameraTargetLockState>>()
                .WithAll<Unity.NetCode.GhostOwnerIsLocal>()
                .WithEntityAccess())
            {
                if (lockState.ValueRO.IsLocked && lockState.ValueRO.TargetEntity != Entity.Null)
                {
                    clientTargetEntity = lockState.ValueRO.TargetEntity;
                    // Get the target's position from the lock state (already tracked there)
                    clientTargetPosition = lockState.ValueRO.LastTargetPosition;
                    hasTargetPosition = true;
                }
                break; // Only one local player
            }

            // EPIC 15.18: Get cursor hover state for WhenHovered visibility mode
            float3 hoverHitPoint = float3.zero;
            bool hasHoverPosition = false;
            foreach (var (hoverResult, entity) in
                SystemAPI.Query<RefRO<CursorHoverResult>>()
                .WithAll<Unity.NetCode.GhostOwnerIsLocal>()
                .WithEntityAccess())
            {
                var hover = hoverResult.ValueRO;
                if (hover.IsValid && hover.HoveredEntity != Entity.Null
                    && hover.Category != HoverCategory.Ground
                    && hover.Category != HoverCategory.None)
                {
                    hoverHitPoint = hover.HitPoint;
                    hasHoverPosition = true;
                }
                break;
            }

            // EPIC 15.18: Get click-select target for WhenTargeted visibility mode
            // Falls back to TargetData when no lock-on target exists
            float3 clickTargetPosition = float3.zero;
            bool hasClickTargetPosition = false;
            if (!hasTargetPosition)
            {
                foreach (var (targetData, entity) in
                    SystemAPI.Query<RefRO<TargetData>>()
                    .WithAll<Unity.NetCode.GhostOwnerIsLocal>()
                    .WithEntityAccess())
                {
                    var td = targetData.ValueRO;
                    if (td.HasValidTarget && td.TargetEntity != Entity.Null)
                    {
                        clickTargetPosition = td.TargetPoint;
                        hasClickTargetPosition = true;
                    }
                    break;
                }
            }
            
            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            float3 cameraPos = mainCamera.transform.position;

            // EPIC 15.17: Get client physics world for line-of-sight checks
            // Only fetch physics world if visibility mode actually uses LOS (avoids 100+ raycasts/frame waste)
            bool needsLOS = pool.NeedsLineOfSight;
            bool hasPhysicsWorld = needsLOS && SystemAPI.HasSingleton<PhysicsWorldSingleton>();
            PhysicsWorld clientPhysicsWorld = default;
            if (hasPhysicsWorld)
            {
                clientPhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            }
            var occlusionFilter = needsLOS ? VisionLayers.OcclusionFilter : default;
            
            // Find the appropriate world (ServerWorld on host, ClientWorld on pure client)
            World serverWorld = GetServerWorld(out bool isClientWorld);
            if (serverWorld == null)
            {
                #if UNITY_EDITOR
                if (_diagFrameCounter++ % 300 == 0)
                {
                    var worldNames = new System.Text.StringBuilder();
                    foreach (var w in World.All) worldNames.Append(w.Name).Append(", ");
                    UnityEngine.Debug.LogWarning($"[HealthBarBridge] GetServerWorld returned null! Available worlds: {worldNames}");
                }
                #endif
                return;
            }

            var serverEM = serverWorld.EntityManager;

            // Complete all pending jobs on the world to safely read components
            serverEM.CompleteAllTrackedJobs();

            // EPIC 15.19: Find the local player entity for aggro comparison
            // On clients, this is the ghost entity marked with GhostOwnerIsLocal
            // On host/server, this is the player entity in the queried world
            Entity localPlayerEntity = Entity.Null;

            // First try to find local player via GhostOwnerIsLocal (works on clients)
            using var localPlayerQuery = serverEM.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>()
            );
            if (!localPlayerQuery.IsEmpty)
            {
                using var localPlayerEntities = localPlayerQuery.ToEntityArray(Allocator.Temp);
                if (localPlayerEntities.Length > 0)
                {
                    localPlayerEntity = localPlayerEntities[0];
                }
            }

            // Fallback: just use any player entity (for single player / LocalWorld)
            if (localPlayerEntity == Entity.Null)
            {
                using var playerQuery = serverEM.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>()
                );
                if (!playerQuery.IsEmpty)
                {
                    using var playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
                    if (playerEntities.Length > 0)
                    {
                        localPlayerEntity = playerEntities[0];
                    }
                }
            }

            // Create query for health bar entities.
            // On ServerWorld/LocalWorld: require HasHitboxes to filter phantom ghost duplicates
            //   (CHILD entities have HasHitboxes + Health from DamageableFixupSystem).
            // On ClientWorld (remote client): query ROOT entities directly — HasHitboxes is NOT
            //   ghost-replicated and DamageableFixupSystem doesn't run on ClientSimulation.
            //   ROOT entities have Health + ShowHealthBarTag (both ghost PrefabType=All).
            // Uses LocalToWorld (not LocalTransform) because CHILD entities are parented to ROOT —
            //   their LocalTransform is in local space (0,0,0), but LocalToWorld is always world space.
            using var serverQuery = isClientWorld
                ? serverEM.CreateEntityQuery(
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<ShowHealthBarTag>()
                )
                : serverEM.CreateEntityQuery(
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<ShowHealthBarTag>(),
                    ComponentType.ReadOnly<HasHitboxes>()
                );

            if (serverQuery.IsEmpty)
            {
                #if UNITY_EDITOR
                if (_diagFrameCounter++ % 300 == 0)
                    UnityEngine.Debug.LogWarning($"[HealthBarBridge] Query empty on {serverWorld.Name} (isClient={isClientWorld}). " +
                        $"Need: Health+LocalToWorld+ShowHealthBarTag" + (isClientWorld ? "" : "+HasHitboxes"));
                #endif
                return;
            }

            // Pre-allocate with expected capacity
            int estimatedCount = serverQuery.CalculateEntityCount();
            #if UNITY_EDITOR
            if (_diagFrameCounter++ % 300 == 0)
                UnityEngine.Debug.Log($"[HealthBarBridge] Found {estimatedCount} entities on {serverWorld.Name} (isClient={isClientWorld})");
            #endif
            var healthBarDataList = new NativeList<HealthBarData>(estimatedCount, Allocator.Temp);
            var seenEntities = new NativeHashSet<Entity>(estimatedCount, Allocator.Temp);
            
            // Process chunks
            var entityType = serverEM.GetEntityTypeHandle();
            var healthType = serverEM.GetComponentTypeHandle<Health>(true);
            var transformType = serverEM.GetComponentTypeHandle<LocalToWorld>(true);
            var combatStateType = serverEM.GetComponentTypeHandle<DIG.Combat.Components.CombatState>(true);
            var hasAggroOnType = serverEM.GetComponentTypeHandle<HasAggroOn>(true); // EPIC 15.19
            
            // Check if CombatState exists in this query (optional component)
            using var chunks = serverQuery.ToArchetypeChunkArray(Allocator.Temp);
            
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            
            // Epic 15.16: Find targeted server entity by matching position
            // Since enemies don't use NetCode ghosts, we match by position proximity
            Entity targetedServerEntity = Entity.Null;
            float closestDistSq = positionMatchToleranceSq; // From config

            // EPIC 15.18: Find hovered and click-targeted server entities by position matching
            Entity hoveredServerEntity = Entity.Null;
            float closestHoverDistSq = positionMatchToleranceSq;
            Entity clickTargetedServerEntity = Entity.Null;
            float closestClickTargetDistSq = positionMatchToleranceSq;
            
            for (int c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var entities = chunk.GetNativeArray(entityType);
                var healths = chunk.GetNativeArray(ref healthType);
                var transforms = chunk.GetNativeArray(ref transformType);
                
                // CombatState is optional - entities may not have it
                bool chunkHasCombatState = chunk.Has(ref combatStateType);
                var combatStates = chunkHasCombatState ? chunk.GetNativeArray(ref combatStateType) : default;
                
                // EPIC 15.19: HasAggroOn is optional - only enemies with aggro system have it
                bool chunkHasAggroOn = chunk.Has(ref hasAggroOnType);
                var aggroOns = chunkHasAggroOn ? chunk.GetNativeArray(ref hasAggroOnType) : default;
                
                int count = chunk.Count;
                
                for (int i = 0; i < count; i++)
                {
                    float3 position = transforms[i].Position;
                    
                    // Distance culling (squared comparison - no sqrt, uses config value)
                    if (math.distancesq(cameraPos, position) > maxShowDistanceSq)
                        continue;
                    
                    float currentHealth = healths[i].Current;
                    
                    // Skip dead entities
                    if (currentHealth <= 0f)
                        continue;
                    
                    Entity entity = entities[i];
                    seenEntities.Add(entity);
                    
                    // Epic 15.16: Match by position - find server entity closest to client target position
                    // The lock state stores LastTargetPosition which includes the height offset
                    if (hasTargetPosition && targetedServerEntity == Entity.Null)
                    {
                        // Get position with typical lock-on height offset for comparison
                        float3 serverPosWithOffset = position;
                        serverPosWithOffset.y += 1.5f; // Approximate height offset
                        
                        float distSq = math.distancesq(clientTargetPosition, serverPosWithOffset);
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            targetedServerEntity = entity;
                        }
                    }
                    
                    // EPIC 15.18: Match hovered entity by XZ distance (more reliable since hit point
                    // can be anywhere on the collider vertically)
                    if (hasHoverPosition)
                    {
                        // Use XZ distance only - hit point Y varies based on collider shape
                        float2 hoverXZ = new float2(hoverHitPoint.x, hoverHitPoint.z);
                        float2 entityXZ = new float2(position.x, position.z);
                        float hDistSq = math.distancesq(hoverXZ, entityXZ);
                        if (hDistSq < closestHoverDistSq)
                        {
                            closestHoverDistSq = hDistSq;
                            hoveredServerEntity = entity;
                        }
                    }

                    // EPIC 15.18: Match click-selected target by XZ distance
                    if (hasClickTargetPosition)
                    {
                        float2 clickXZ = new float2(clickTargetPosition.x, clickTargetPosition.z);
                        float2 entityXZ = new float2(position.x, position.z);
                        float ctDistSq = math.distancesq(clickXZ, entityXZ);
                        if (ctDistSq < closestClickTargetDistSq)
                        {
                            closestClickTargetDistSq = ctDistSq;
                            clickTargetedServerEntity = entity;
                        }
                    }

                    _lastHealthValues[entity] = currentHealth;
                    
                    // Get combat state if available
                    bool isInCombat = false;
                    float timeSinceCombatEnded = 100f;
                    if (chunkHasCombatState)
                    {
                        var combatState = combatStates[i];
                        isInCombat = combatState.IsInCombat;
                        timeSinceCombatEnded = combatState.IsInCombat ? 0f : elapsedTime - combatState.CombatExitTime;
                    }
                    
                    // EPIC 15.17: LOS check from camera to enemy (center mass offset)
                    // Shorten ray to stop before the enemy's own collider (~0.75m margin).
                    // This avoids self-hits when the enemy uses BelongsTo=Everything,
                    // while still detecting walls/objects on the Default layer.
                    bool isInLineOfSight = true;
                    if (hasPhysicsWorld)
                    {
                        float3 targetCenter = position + new float3(0f, 1.0f, 0f);
                        float3 toTarget = targetCenter - cameraPos;
                        float dist = math.length(toTarget);
                        if (dist > 1.0f)
                        {
                            float3 shortenedEnd = cameraPos + (toTarget / dist) * (dist - 0.75f);
                            isInLineOfSight = DetectionQueryUtility.HasLineOfSight(
                                in clientPhysicsWorld, cameraPos, shortenedEnd, occlusionFilter);
                        }
                    }
                    
                    // EPIC 15.19: Check if enemy has aggro on the local player
                    bool hasAggroOnPlayer = false;
                    if (chunkHasAggroOn && localPlayerEntity != Entity.Null)
                    {
                        var aggroOn = aggroOns[i];
                        hasAggroOnPlayer = aggroOn.TargetPlayer == localPlayerEntity;
                    }

                    // Always add to list - position needs updating every frame
                    healthBarDataList.Add(new HealthBarData
                    {
                        Entity = entity,
                        Position = position,
                        CurrentHealth = currentHealth,
                        MaxHealth = healths[i].Max,
                        IsInCombat = isInCombat,
                        TimeSinceCombatEnded = timeSinceCombatEnded,
                        IsInLineOfSight = isInLineOfSight,
                        HasAggroOnPlayer = hasAggroOnPlayer // EPIC 15.19
                    });
                }
            }
            
            // Epic 15.16: Set the targeted server entity (matched by position)
            // EPIC 15.18: Fall back to click-selected target if no lock-on target
            Entity finalTargeted = targetedServerEntity != Entity.Null
                ? targetedServerEntity
                : clickTargetedServerEntity;
            pool.SetTargetedEntity(finalTargeted);

            // EPIC 15.18: Set the hovered server entity (matched by position)
            pool.SetHoveredEntity(hoveredServerEntity);
            
            // Cleanup stale entries and hide their bars
            CleanupStaleEntries(pool, ref seenEntities);
            
            // Update UI (main thread only)
            for (int i = 0; i < healthBarDataList.Length; i++)
            {
                var data = healthBarDataList[i];
                pool.ShowHealthBar(data.Entity, data.Position, data.CurrentHealth, data.MaxHealth, null,
                    data.IsInCombat, data.TimeSinceCombatEnded, data.IsInLineOfSight, data.HasAggroOnPlayer);
            }
            
            healthBarDataList.Dispose();
            seenEntities.Dispose();
        }
        
        private World GetServerWorld(out bool isClientWorld)
        {
            // First try to find ServerWorld (listen server / host mode)
            foreach (var world in World.All)
            {
                if (world.Name == "ServerWorld" && world.IsCreated)
                {
                    isClientWorld = false;
                    return world;
                }
            }

            // No ServerWorld means we're a pure client - use ClientWorld instead
            // Ghost entities with Health component will be queried there
            foreach (var world in World.All)
            {
                if (world.Name == "ClientWorld" && world.IsCreated)
                {
                    isClientWorld = true;
                    return world;
                }
            }

            // Fallback to LocalWorld for editor play mode without netcode
            foreach (var world in World.All)
            {
                if (world.Name == "LocalWorld" && world.IsCreated)
                {
                    isClientWorld = false;
                    return world;
                }
            }

            // Final fallback: DefaultGameObjectInjectionWorld (handles "Default World" in editor)
            if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                isClientWorld = false;
                return World.DefaultGameObjectInjectionWorld;
            }

            isClientWorld = false;
            return null;
        }
                
        private void CleanupStaleEntries(EnemyHealthBarPool pool, ref NativeHashSet<Entity> seenEntities)
        {
            using var keys = _lastHealthValues.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < keys.Length; i++)
            {
                if (!seenEntities.Contains(keys[i]))
                {
                    _lastHealthValues.Remove(keys[i]);
                    pool.HideHealthBar(keys[i]);
                }
            }
        }
    }
}
