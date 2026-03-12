using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Handles spawning of visual damage popups.
    /// Implements 13.16.5 using generic HealthChangedEvents.
    /// Runs on Client/Predicted groups to prevent server-side visual spam (unless dedicated server needs to know? Popups are usually local).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct DamagePopupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DamagePopupConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            var random = Random.CreateFromIndex((uint)SystemAPI.Time.ElapsedTime);
            
            // Query events
            foreach (var (evt, config, transform) in 
                     SystemAPI.Query<RefRO<HealthChangedEvent>, RefRO<DamagePopupConfig>, RefRO<LocalTransform>>()
                     .WithAll<HealthChangedEvent>())
            {
                // Only negatives are damage
                float delta = evt.ValueRO.Delta;
                if (delta >= -0.1f) continue; // Ignore negligible or healing
                
                float damage = -delta;
                
                // Spawn Popup
                if (config.ValueRO.PopupPrefab != Entity.Null)
                {
                    Entity popup = ecb.Instantiate(config.ValueRO.PopupPrefab);
                    
                    // Calculate Position with Jitter
                    float3 pos = transform.ValueRO.Position;
                    pos.y += config.ValueRO.SpawnHeightOffset;
                    pos.x += random.NextFloat(-config.ValueRO.RandomJitter, config.ValueRO.RandomJitter);
                    pos.z += random.NextFloat(-config.ValueRO.RandomJitter, config.ValueRO.RandomJitter);
                    
                    ecb.SetComponent(popup, LocalTransform.FromPosition(pos));

                    // Note: Actual number display logic relies on the Popup Prefab having a managed component or
                    // a system that pushes the 'damage' value to a UI element.
                    // For now, we spawn the prefab which is the ECS part.
                    // MVP: We assume the prefab handles its own lifetime/animation.
                    // If we need to pass the damage number:
                    // ecb.AddComponent(popup, new DamageValue { Value = damage });
                }
            }
        }
    }
}
