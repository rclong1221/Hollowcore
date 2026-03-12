// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.26 · WidgetProjectionSystem
// Centralized world-to-screen projection, LOD, importance scoring, and budget
// enforcement for all widget-bearing entities. Managed SystemBase because it
// accesses Camera.main and ParadigmWidgetConfig.Instance. With <=200 entities
// the VP multiply is trivial; can Burstify via camera data singleton later.
//
// Reuses the Server/Client world query pattern from EnemyHealthBarBridgeSystem.
// Output: static ProjectedWidgets HashMap read by WidgetBridgeSystem.
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Player.Components;
using CombatState = DIG.Combat.Components.CombatState;
using DIG.Combat.UI;
using DIG.CameraSystem;
using DIG.Targeting;
using DIG.Targeting.Components;
using DIG.Widgets.Config;
using DIG.Widgets.Rendering;

namespace DIG.Widgets.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class WidgetProjectionSystem : SystemBase
    {
        // ── Static output ──────────────────────────────────────────
        /// <summary>
        /// Projected widget data for the current frame. Read by WidgetBridgeSystem.
        /// Disposed and recreated each frame.
        /// </summary>
        public static NativeHashMap<Entity, WidgetProjection> ProjectedWidgets;

        /// <summary>
        /// Off-screen tracked entities (failed frustum but have OffScreenTracker).
        /// </summary>
        public static NativeList<WidgetProjection> OffScreenWidgets;

        /// <summary>
        /// Whether the widget framework ran this frame and has valid data.
        /// When false, EnemyHealthBarBridgeSystem runs its standalone logic.
        /// </summary>
        public static bool FrameworkActive;

        /// <summary>Total projected count before budget cull (for debug).</summary>
        public static int TotalProjectedCount;

        /// <summary>Count visible after budget enforcement (for debug).</summary>
        public static int VisibleCount;

        /// <summary>Server-side entity matched as the player's current target (for pool visibility).</summary>
        public static Entity TargetedEntity;

        /// <summary>Server-side entity matched as cursor hover (for pool visibility).</summary>
        public static Entity HoveredEntity;

        // ── Instance state ─────────────────────────────────────────
        private NativeHashMap<Entity, float> _previousHealth;
        private NativeHashMap<Entity, double> _lastDamageTime;

        // ── Default values when no paradigm profile is set ─────────
        private const int DefaultMaxActiveWidgets = 40;
        private const float DefaultDistanceFalloff = 3f;
        private const float DefaultLODDistanceMultiplier = 1f;
        private const float DefaultWidgetScaleMultiplier = 1f;
        private const float DefaultHealthBarYOffset = 2.5f;

        // LOD base distances (multiplied by profile's LODDistanceMultiplier)
        private const float LODFullBase = 15f;
        private const float LODReducedBase = 35f;
        private const float LODMinimalBase = 60f;

        protected override void OnCreate()
        {
            _previousHealth = new NativeHashMap<Entity, float>(128, Allocator.Persistent);
            _lastDamageTime = new NativeHashMap<Entity, double>(128, Allocator.Persistent);

            // Don't run until ShowHealthBarTag entities exist in this world.
            // Prevents cross-world queries during ghost initialization / subscene loading.
            RequireForUpdate<ShowHealthBarTag>();
        }

        protected override void OnDestroy()
        {
            if (_previousHealth.IsCreated) _previousHealth.Dispose();
            if (_lastDamageTime.IsCreated) _lastDamageTime.Dispose();
            DisposeStaticData();
        }

        private static void DisposeStaticData()
        {
            if (ProjectedWidgets.IsCreated) ProjectedWidgets.Dispose();
            if (OffScreenWidgets.IsCreated) OffScreenWidgets.Dispose();
            FrameworkActive = false;
        }

        protected override void OnUpdate()
        {
            // ── 1. Camera data ─────────────────────────────────────
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                FrameworkActive = false;
                return;
            }

            UpdateCameraData(mainCamera);

            // ── 2. Paradigm profile (null-safe) ────────────────────
            var profile = ParadigmWidgetConfig.HasInstance
                ? ParadigmWidgetConfig.Instance.ActiveProfile
                : null;

            int maxWidgets = profile != null ? profile.MaxActiveWidgets : DefaultMaxActiveWidgets;
            float distanceFalloff = profile != null ? profile.DistanceFalloff : DefaultDistanceFalloff;
            float lodMultiplier = profile != null ? profile.LODDistanceMultiplier : DefaultLODDistanceMultiplier;
            float scaleMultiplier = profile != null ? profile.WidgetScaleMultiplier : DefaultWidgetScaleMultiplier;
            float yOffset = profile != null ? profile.HealthBarYOffset : DefaultHealthBarYOffset;
            bool healthBarEnabled = profile == null || profile.HealthBarEnabled;

            // ── 3. Get Server/Client world ─────────────────────────
            World queryWorld = GetQueryWorld(out bool isClientWorld);
            if (queryWorld == null)
            {
                FrameworkActive = false;
                return;
            }

            var queryEM = queryWorld.EntityManager;
            queryEM.CompleteAllTrackedJobs();

            // ── 4. Build entity query ──────────────────────────────
            using var entityQuery = isClientWorld
                ? queryEM.CreateEntityQuery(
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<ShowHealthBarTag>())
                : queryEM.CreateEntityQuery(
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<ShowHealthBarTag>(),
                    ComponentType.ReadOnly<HasHitboxes>());

            int entityCount = entityQuery.CalculateEntityCount();
            if (entityCount == 0)
            {
                FrameworkActive = false;
                return;
            }

            // ── 5. Get targeted/hovered entities ───────────────────
            Entity targetedEntity = Entity.Null;
            float3 targetedPosition = float3.zero;
            bool hasTargetPos = false;

            foreach (var (lockState, _) in
                SystemAPI.Query<RefRO<CameraTargetLockState>>()
                    .WithAll<Unity.NetCode.GhostOwnerIsLocal>()
                    .WithEntityAccess())
            {
                if (lockState.ValueRO.IsLocked && lockState.ValueRO.TargetEntity != Entity.Null)
                {
                    targetedEntity = lockState.ValueRO.TargetEntity;
                    targetedPosition = lockState.ValueRO.LastTargetPosition;
                    hasTargetPos = true;
                }
                break;
            }

            Entity hoveredEntity = Entity.Null;
            float3 hoveredPosition = float3.zero;
            bool hasHoverPos = false;

            foreach (var (hoverResult, _) in
                SystemAPI.Query<RefRO<CursorHoverResult>>()
                    .WithAll<Unity.NetCode.GhostOwnerIsLocal>()
                    .WithEntityAccess())
            {
                var hover = hoverResult.ValueRO;
                if (hover.IsValid && hover.HoveredEntity != Entity.Null
                    && hover.Category != HoverCategory.Ground
                    && hover.Category != HoverCategory.None)
                {
                    hoveredPosition = hover.HitPoint;
                    hasHoverPos = true;
                }
                break;
            }

            // Fallback: TargetData for click-select
            if (!hasTargetPos)
            {
                foreach (var (targetData, _) in
                    SystemAPI.Query<RefRO<TargetData>>()
                        .WithAll<Unity.NetCode.GhostOwnerIsLocal>()
                        .WithEntityAccess())
                {
                    var td = targetData.ValueRO;
                    if (td.HasValidTarget && td.TargetEntity != Entity.Null)
                    {
                        targetedPosition = td.TargetPoint;
                        hasTargetPos = true;
                    }
                    break;
                }
            }

            // ── 6. Project entities ────────────────────────────────
            DisposeStaticData();
            ProjectedWidgets = new NativeHashMap<Entity, WidgetProjection>(entityCount, Allocator.TempJob);
            OffScreenWidgets = new NativeList<WidgetProjection>(16, Allocator.TempJob);

            var projectionList = new NativeList<WidgetProjection>(entityCount, Allocator.Temp);
            var seenEntities = new NativeHashSet<Entity>(entityCount, Allocator.Temp);

            // Entity matching tolerance for target/hover position matching
            const float positionMatchTolSq = 9f; // 3m squared

            // Track closest matches for targeted/hovered server entities (for pool API)
            Entity matchedTargetEntity = Entity.Null;
            float closestTargetDistSq = positionMatchTolSq;
            Entity matchedHoverEntity = Entity.Null;
            float closestHoverDistSq = positionMatchTolSq;

            var entityType = queryEM.GetEntityTypeHandle();
            var healthType = queryEM.GetComponentTypeHandle<Health>(true);
            var transformType = queryEM.GetComponentTypeHandle<LocalToWorld>(true);
            var combatStateType = queryEM.GetComponentTypeHandle<CombatState>(true);

            using var chunks = entityQuery.ToArchetypeChunkArray(Allocator.Temp);
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            for (int c = 0; c < chunks.Length; c++)
            {
                var chunk = chunks[c];
                var entities = chunk.GetNativeArray(entityType);
                var healths = chunk.GetNativeArray(ref healthType);
                var transforms = chunk.GetNativeArray(ref transformType);
                bool hasCombatState = chunk.Has(ref combatStateType);
                var combatStates = hasCombatState ? chunk.GetNativeArray(ref combatStateType) : default;

                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    float currentHP = healths[i].Current;
                    float maxHP = healths[i].Max;

                    // Skip dead entities
                    if (currentHP <= 0f) continue;

                    float3 worldPos = transforms[i].Position;

                    // Skip uninitialized ghost entities whose transform hasn't replicated yet
                    if (math.lengthsq(worldPos) < 0.001f) continue;

                    float3 widgetWorldPos = worldPos + new float3(0f, yOffset, 0f);

                    seenEntities.Add(entity);

                    // Track damage recency
                    if (_previousHealth.TryGetValue(entity, out float prevHP) && currentHP < prevHP)
                    {
                        _lastDamageTime[entity] = elapsedTime;
                    }
                    _previousHealth[entity] = currentHP;

                    float timeSinceDamage = 999f;
                    if (_lastDamageTime.TryGetValue(entity, out double dmgTime))
                    {
                        timeSinceDamage = (float)(elapsedTime - dmgTime);
                    }

                    // Combat state
                    bool isInCombat = false;
                    float timeSinceCombatEnded = 100f;
                    if (hasCombatState)
                    {
                        var cs = combatStates[i];
                        isInCombat = cs.IsInCombat;
                        timeSinceCombatEnded = cs.IsInCombat ? 0f : (float)(elapsedTime - cs.CombatExitTime);
                    }

                    // Target/hover matching by position (closest match wins)
                    bool isTargeted = false;
                    bool isHovered = false;
                    if (hasTargetPos)
                    {
                        float3 posWithOffset = worldPos;
                        posWithOffset.y += 1.5f;
                        float tDistSq = math.distancesq(targetedPosition, posWithOffset);
                        if (tDistSq < closestTargetDistSq)
                        {
                            closestTargetDistSq = tDistSq;
                            matchedTargetEntity = entity;
                        }
                        isTargeted = tDistSq < positionMatchTolSq;
                    }
                    if (hasHoverPos)
                    {
                        float2 hoverXZ = new float2(hoveredPosition.x, hoveredPosition.z);
                        float2 entityXZ = new float2(worldPos.x, worldPos.z);
                        float hDistSq = math.distancesq(hoverXZ, entityXZ);
                        if (hDistSq < closestHoverDistSq)
                        {
                            closestHoverDistSq = hDistSq;
                            matchedHoverEntity = entity;
                        }
                        isHovered = hDistSq < positionMatchTolSq;
                    }

                    // Distance from camera
                    float distance = math.distance(WidgetCameraData.CameraPosition, worldPos);

                    // Screen projection
                    bool onScreen = WidgetCameraData.WorldToScreen(widgetWorldPos, out float2 screenPos);

                    // LOD tier
                    float fullDist = LODFullBase * lodMultiplier;
                    float reducedDist = LODReducedBase * lodMultiplier;
                    float minimalDist = LODMinimalBase * lodMultiplier;

                    WidgetLODTier lod;
                    if (distance <= fullDist) lod = WidgetLODTier.Full;
                    else if (distance <= reducedDist) lod = WidgetLODTier.Reduced;
                    else if (distance <= minimalDist) lod = WidgetLODTier.Minimal;
                    else lod = WidgetLODTier.Culled;

                    // Targeted/boss/party override: promote LOD
                    if (isTargeted) lod = WidgetLODTier.Full;
                    if (timeSinceDamage < 2f && lod > WidgetLODTier.Full) lod = WidgetLODTier.Full;

                    // Importance scoring (entityTier=0 = Normal for v1)
                    float importance = WidgetImportanceComputer.Compute(
                        distance, 0, isTargeted, isHovered, isInCombat, timeSinceDamage, distanceFalloff);

                    // Active widget flags
                    WidgetFlags flags = WidgetFlags.None;
                    if (healthBarEnabled) flags |= WidgetFlags.HealthBar;

                    var proj = new WidgetProjection
                    {
                        Entity = entity,
                        WorldPos = worldPos,
                        ScreenPos = screenPos,
                        Distance = distance,
                        Importance = importance,
                        LOD = lod,
                        IsVisible = false, // set by budget enforcement below
                        ActiveFlags = flags,
                        Scale = scaleMultiplier,
                        Health01 = maxHP > 0f ? currentHP / maxHP : 0f,
                        MaxHealth = maxHP,
                        CurrentHealth = currentHP,
                        YOffset = yOffset,
                        IsOffScreen = !onScreen,
                        IsInCombat = isInCombat,
                        TimeSinceCombatEnded = timeSinceCombatEnded
                    };

                    // Culled by LOD = skip entirely
                    if (lod == WidgetLODTier.Culled && !isTargeted)
                        continue;

                    // Off-screen: skip for normal rendering but track if needed
                    if (!onScreen && !isTargeted)
                    {
                        // Off-screen tracking can be added in Phase 5
                        continue;
                    }

                    projectionList.Add(proj);
                }
            }

            // ── 7. Sort by importance (descending) ─────────────────
            projectionList.Sort(new ImportanceComparer());

            // ── 8. Budget enforcement ──────────────────────────────
            int visibleCount = 0;
            for (int i = 0; i < projectionList.Length; i++)
            {
                var proj = projectionList[i];
                bool exempt = WidgetImportanceComputer.IsExemptFromBudget(
                    proj.Importance >= 200f, 0); // targeted entities have importance >= 200

                if (exempt || visibleCount < maxWidgets)
                {
                    proj.IsVisible = true;
                    visibleCount++;
                }

                ProjectedWidgets[proj.Entity] = proj;
            }

            TotalProjectedCount = projectionList.Length;
            VisibleCount = visibleCount;
            TargetedEntity = matchedTargetEntity;
            HoveredEntity = matchedHoverEntity;
            // Only activate framework (disabling standalone EnemyHealthBarBridgeSystem)
            // when widget adapters are actually registered in the scene.
            FrameworkActive = WidgetRendererRegistry.HasAnyRenderers;

            // ── 9. Cleanup stale tracking data ─────────────────────
            CleanupStaleEntities(ref seenEntities);

            projectionList.Dispose();
            seenEntities.Dispose();
        }

        // ── Helpers ────────────────────────────────────────────────

        private static void UpdateCameraData(Camera cam)
        {
            var t = cam.transform;
            WidgetCameraData.VPMatrix = (float4x4)(cam.projectionMatrix * cam.worldToCameraMatrix);
            WidgetCameraData.ScreenSize = new float2(Screen.width, Screen.height);
            WidgetCameraData.CameraPosition = t.position;
            WidgetCameraData.CameraForward = t.forward;
            WidgetCameraData.IsOrthographic = cam.orthographic;
            WidgetCameraData.CameraMode = CameraModeProvider.HasInstance
                ? CameraModeProvider.Instance.CurrentMode
                : CameraMode.ThirdPersonFollow;
            WidgetCameraData.IsValid = true;
        }

        /// <summary>
        /// Find the world to query for health bar entities.
        /// ServerWorld on host, ClientWorld on pure client, LocalWorld/Default fallback.
        /// Reuses the pattern from EnemyHealthBarBridgeSystem.
        /// </summary>
        private static World GetQueryWorld(out bool isClientWorld)
        {
            foreach (var world in World.All)
            {
                if (world.Name == "ServerWorld" && world.IsCreated)
                {
                    isClientWorld = false;
                    return world;
                }
            }

            foreach (var world in World.All)
            {
                if (world.Name == "ClientWorld" && world.IsCreated)
                {
                    isClientWorld = true;
                    return world;
                }
            }

            foreach (var world in World.All)
            {
                if (world.Name == "LocalWorld" && world.IsCreated)
                {
                    isClientWorld = false;
                    return world;
                }
            }

            if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                isClientWorld = false;
                return World.DefaultGameObjectInjectionWorld;
            }

            isClientWorld = false;
            return null;
        }

        private void CleanupStaleEntities(ref NativeHashSet<Entity> seenEntities)
        {
            using var healthKeys = _previousHealth.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < healthKeys.Length; i++)
            {
                if (!seenEntities.Contains(healthKeys[i]))
                {
                    _previousHealth.Remove(healthKeys[i]);
                    _lastDamageTime.Remove(healthKeys[i]);
                }
            }
        }

        // Sort by importance descending
        private struct ImportanceComparer : System.Collections.Generic.IComparer<WidgetProjection>
        {
            public int Compare(WidgetProjection a, WidgetProjection b)
            {
                // Descending: higher importance first
                return b.Importance.CompareTo(a.Importance);
            }
        }
    }
}
