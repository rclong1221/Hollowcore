using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Horror.Components;

namespace Horror.Systems
{
    /// <summary>
    /// Presentation system that processes HorrorEventRequest entities and 
    /// triggers actual audio/visual effects through the HorrorEventManager.
    /// Runs on client only.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class HorrorEventPresentationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var manager = HorrorEventManager.Instance;
            if (manager == null) return;
            
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (request, entity) in 
                SystemAPI.Query<RefRO<HorrorEventRequest>>()
                    .WithEntityAccess())
            {
                // Process the event through the manager
                manager.ProcessEvent(
                    request.ValueRO.EventType,
                    request.ValueRO.Intensity,
                    request.ValueRO.Duration,
                    request.ValueRO.Position,
                    request.ValueRO.IsPrivate
                );
                
                // Destroy the request entity
                ecb.DestroyEntity(entity);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
