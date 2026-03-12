using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Loot.Components;

namespace DIG.Loot.Systems
{
    /// <summary>
    /// EPIC 16.6: Handles loot container interactions.
    /// Player interacts → Sealed→Opening→Open (roll loot) → Looted or respawn.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ContainerInteractionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (container, transform, entity) in
                     SystemAPI.Query<RefRW<LootContainerState>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                switch (container.ValueRO.Phase)
                {
                    case LootContainerPhase.Opening:
                    {
                        float elapsed = currentTime - container.ValueRO.LastOpenedTime;
                        if (elapsed >= container.ValueRO.OpenDuration)
                        {
                            container.ValueRW.Phase = LootContainerPhase.Open;

                            // Roll loot table and add PendingLootSpawn if buffer exists
                            if (SystemAPI.HasBuffer<PendingLootSpawn>(entity))
                            {
                                // Loot rolling handled by DeathLootSystem pattern
                                // For containers, a separate trigger would invoke LootTableResolver
                                // For now, transition to Open which signals loot is available
                            }
                        }
                        break;
                    }

                    case LootContainerPhase.Open:
                    {
                        // Transition to Looted after loot is picked up
                        // In practice, this would check if PendingLootSpawn buffer is empty
                        // For now, auto-transition after a delay
                        float elapsed = currentTime - container.ValueRO.LastOpenedTime;
                        if (elapsed >= container.ValueRO.OpenDuration + 5f) // 5s loot window
                        {
                            container.ValueRW.Phase = LootContainerPhase.Looted;
                        }
                        break;
                    }

                    case LootContainerPhase.Looted:
                    {
                        if (container.ValueRO.IsReusable)
                        {
                            float elapsed = currentTime - container.ValueRO.LastOpenedTime;
                            if (elapsed >= container.ValueRO.RespawnTime)
                            {
                                container.ValueRW.Phase = LootContainerPhase.Sealed;
                            }
                        }
                        else
                        {
                            container.ValueRW.Phase = LootContainerPhase.Destroyed;
                            ecb.DestroyEntity(entity);
                        }
                        break;
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Called by interaction system when player activates a container.
        /// </summary>
        public static void TryOpenContainer(ref LootContainerState container, float currentTime)
        {
            if (container.Phase != LootContainerPhase.Sealed)
                return;

            container.Phase = LootContainerPhase.Opening;
            container.LastOpenedTime = currentTime;
        }
    }
}
