using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Hazards
{
    /// <summary>
    /// Updates client-side temperature effects state for VFX.
    /// Triggers shivering, heat distortion, etc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct TemperatureEffectsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (bodyTemp, effects) in
                     SystemAPI.Query<RefRO<BodyTemperature>, RefRW<TemperatureEffects>>())
            {
                var temp = bodyTemp.ValueRO;
                ref var fx = ref effects.ValueRW;

                fx.IsCold = temp.IsCold;
                fx.IsHot = temp.IsHot;
                fx.Severity = temp.Severity;
            }
        }
    }

    /// <summary>
    /// Updates client-side suit crack visual state.
    /// Animates crack overlay transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SuitCrackVisualSystem : ISystem
    {
        private const float TransitionSpeed = 2f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (suit, visual) in
                     SystemAPI.Query<RefRO<SuitIntegrity>, RefRW<SuitCrackVisualState>>())
            {
                var suitData = suit.ValueRO;
                ref var vis = ref visual.ValueRW;

                // Update displayed crack level (instant for now)
                vis.DisplayedCrackLevel = suitData.CrackLevel;

                // Calculate target alpha based on crack level
                float targetAlpha = suitData.CrackLevel switch
                {
                    0 => 0f,
                    1 => 0.3f,
                    2 => 0.6f,
                    3 => 0.9f,
                    _ => 0f
                };

                // Smooth transition to target alpha
                float diff = targetAlpha - vis.CrackOverlayAlpha;
                vis.CrackOverlayAlpha += math.sign(diff) * math.min(math.abs(diff), TransitionSpeed * deltaTime);
            }
        }
    }

    /// <summary>
    /// Event for temperature warning sound/notification.
    /// </summary>
    public struct TemperatureWarningEvent : IComponentData
    {
        /// <summary>
        /// True if cold warning, false if heat warning.
        /// </summary>
        public bool IsCold;

        /// <summary>
        /// Severity level (0-1).
        /// </summary>
        public float Severity;

        /// <summary>
        /// Entity receiving warning.
        /// </summary>
        public Entity TargetEntity;
    }

    /// <summary>
    /// Generates temperature warning events when entering danger zone.
    /// Client-side for audio/UI feedback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct TemperatureWarningSystem : ISystem
    {
        private ComponentLookup<BodyTemperature> _tempLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _tempLookup = state.GetComponentLookup<BodyTemperature>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _tempLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (effects, entity) in
                     SystemAPI.Query<RefRW<TemperatureEffects>>()
                     .WithEntityAccess())
            {
                if (!_tempLookup.HasComponent(entity))
                    continue;

                var temp = _tempLookup[entity];
                ref var fx = ref effects.ValueRW;

                // Check for state transitions that warrant warnings
                bool nowDanger = temp.IsTakingDamage;
                bool wasDanger = fx.IsCold || fx.IsHot;

                // Create warning event on entering danger state
                if (nowDanger && !wasDanger)
                {
                    var warningEntity = ecb.CreateEntity();
                    ecb.AddComponent(warningEntity, new TemperatureWarningEvent
                    {
                        IsCold = temp.IsCold,
                        Severity = temp.Severity,
                        TargetEntity = entity
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up temperature warning events after one frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct TemperatureWarningCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<TemperatureWarningEvent>>()
                     .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Event for suit damage warning sound/notification.
    /// </summary>
    public struct SuitDamageWarningEvent : IComponentData
    {
        /// <summary>
        /// New crack level (1-3).
        /// </summary>
        public int CrackLevel;

        /// <summary>
        /// Entity with damaged suit.
        /// </summary>
        public Entity TargetEntity;
    }

    /// <summary>
    /// Generates suit damage warning events when crack level increases.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SuitDamageWarningSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (suit, visual, entity) in
                     SystemAPI.Query<RefRO<SuitIntegrity>, RefRW<SuitCrackVisualState>>()
                     .WithEntityAccess())
            {
                var suitData = suit.ValueRO;
                ref var vis = ref visual.ValueRW;

                // Check for crack level increase
                if (suitData.CrackLevel > vis.DisplayedCrackLevel && suitData.CrackLevel > 0)
                {
                    var warningEntity = ecb.CreateEntity();
                    ecb.AddComponent(warningEntity, new SuitDamageWarningEvent
                    {
                        CrackLevel = suitData.CrackLevel,
                        TargetEntity = entity
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up suit damage warning events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SuitDamageWarningCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<SuitDamageWarningEvent>>()
                     .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
