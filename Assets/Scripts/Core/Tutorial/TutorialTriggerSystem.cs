using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using DIG.Core.Zones;

namespace DIG.UI.Tutorial
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct TutorialTriggerSystem : ISystem
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
            var tutorialJob = new TutorialTriggerJob
            {
                TutorialGroup = SystemAPI.GetComponentLookup<TutorialTriggerComponent>(false),
                PlayerGroup = SystemAPI.GetComponentLookup<PlayerTag>(true) // Check if player
            };

            state.Dependency = tutorialJob.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        struct TutorialTriggerJob : ITriggerEventsJob
        {
            public ComponentLookup<TutorialTriggerComponent> TutorialGroup;
            [ReadOnly] public ComponentLookup<PlayerTag> PlayerGroup;

            public void Execute(TriggerEvent triggerEvent)
            {
                Entity entityA = triggerEvent.EntityA;
                Entity entityB = triggerEvent.EntityB;

                bool isPlayerA = PlayerGroup.HasComponent(entityA);
                bool isPlayerB = PlayerGroup.HasComponent(entityB);

                // If neither is player, ignore
                if (!isPlayerA && !isPlayerB) return;

                // Identify Trigger
                Entity triggerEntity = Entity.Null;
                if (TutorialGroup.HasComponent(entityA)) triggerEntity = entityA;
                else if (TutorialGroup.HasComponent(entityB)) triggerEntity = entityB;

                if (triggerEntity == Entity.Null) return;

                var tutorial = TutorialGroup[triggerEntity];
                if (tutorial.OneTime && tutorial.Triggered) return;

                // Fire Event (For now, just Log, in future generic UI event)
                // Note: Debug.Log in Burst is fine for development
                // UnityEngine.Debug.Log($"[TUTORIAL] {tutorial.Message}");

                // Mark triggered
                tutorial.Triggered = true;
                TutorialGroup[triggerEntity] = tutorial;
            }
        }
    }
}
