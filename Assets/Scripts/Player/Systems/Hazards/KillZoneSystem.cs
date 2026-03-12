using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using Player.Components;
using DIG.Survival.Hazards;

namespace Player.Systems.Hazards
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct KillZoneSystem : ISystem
    {
        private ComponentLookup<KillZone> _killZoneLookup;
        private ComponentLookup<Health> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            _killZoneLookup = state.GetComponentLookup<KillZone>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _killZoneLookup.Update(ref state);
            _healthLookup.Update(ref state);

            state.Dependency = new KillZoneJob
            {
                KillZoneLookup = _killZoneLookup,
                HealthLookup = _healthLookup,
                Time = SystemAPI.Time.DeltaTime
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        struct KillZoneJob : ITriggerEventsJob
        {
            [ReadOnly] public ComponentLookup<KillZone> KillZoneLookup;
            public ComponentLookup<Health> HealthLookup;
            public float Time;

            public void Execute(TriggerEvent triggerEvent)
            {
                Entity entityA = triggerEvent.EntityA;
                Entity entityB = triggerEvent.EntityB;

                bool aIsZone = KillZoneLookup.HasComponent(entityA);
                bool bIsZone = KillZoneLookup.HasComponent(entityB);

                if (aIsZone && HealthLookup.HasComponent(entityB))
                {
                    ApplyDamage(entityA, entityB);
                }
                else if (bIsZone && HealthLookup.HasComponent(entityA))
                {
                    ApplyDamage(entityB, entityA);
                }
            }

            void ApplyDamage(Entity zone, Entity victim)
            {
                var damage = KillZoneLookup[zone].DamagePerSecond * Time;
                if (damage <= 0) damage = 1000f; 

                var health = HealthLookup[victim];
                if (health.Current > 0)
                {
                    health.Current -= damage;
                    if (health.Current < 0) health.Current = 0;
                    HealthLookup[victim] = health;
                }
            }
        }
    }
}
