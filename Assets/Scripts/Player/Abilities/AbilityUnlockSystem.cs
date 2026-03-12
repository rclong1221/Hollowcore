using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using DIG.Core.Zones;

namespace DIG.Player.Abilities
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct AbilityUnlockSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var unlockJob = new AbilityUnlockJob
            {
                AbilityGroup = SystemAPI.GetComponentLookup<AbilityUnlockComponent>(false),
                PlayerGroup = SystemAPI.GetComponentLookup<PlayerTag>(true),
                AbilitiesGroup = SystemAPI.GetBufferLookup<AbilityDefinition>(false)
            };

            state.Dependency = unlockJob.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        struct AbilityUnlockJob : ITriggerEventsJob
        {
            public ComponentLookup<AbilityUnlockComponent> AbilityGroup;
            [ReadOnly] public ComponentLookup<PlayerTag> PlayerGroup;
            public BufferLookup<AbilityDefinition> AbilitiesGroup;

            public void Execute(TriggerEvent triggerEvent)
            {
                Entity entityA = triggerEvent.EntityA;
                Entity entityB = triggerEvent.EntityB;

                bool isPlayerA = PlayerGroup.HasComponent(entityA);
                bool isPlayerB = PlayerGroup.HasComponent(entityB);

                if (!isPlayerA && !isPlayerB) return;

                Entity triggerEntity = Entity.Null;
                Entity playerEntity = Entity.Null;

                if (AbilityGroup.HasComponent(entityA)) { triggerEntity = entityA; playerEntity = entityB; }
                else if (AbilityGroup.HasComponent(entityB)) { triggerEntity = entityB; playerEntity = entityA; }

                if (triggerEntity == Entity.Null) return;

                var abilityUnlock = AbilityGroup[triggerEntity];
                if (abilityUnlock.Triggered) return;

                // Unlock Ability Logic
                if (AbilitiesGroup.HasBuffer(playerEntity))
                {
                    DynamicBuffer<AbilityDefinition> abilities = AbilitiesGroup[playerEntity];
                    for (int i = 0; i < abilities.Length; i++)
                    {
                        var ability = abilities[i];
                        if (ability.AbilityTypeId == (int)abilityUnlock.AbilityToUnlock)
                        {
                            if (!ability.IsActive)
                            {
                                ability.IsActive = true;
                                abilities[i] = ability;
                                // UnityEngine.Debug.Log($"[ABILITY UNLOCK] Unlocked Ability ID: {ability.AbilityTypeId}");
                            }
                            break;
                        }
                    }
                }

                // Mark triggered
                abilityUnlock.Triggered = true;
                AbilityGroup[triggerEntity] = abilityUnlock;
            }
        }
    }
}
