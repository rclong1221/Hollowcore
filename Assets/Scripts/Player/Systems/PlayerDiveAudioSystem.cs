using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.NetCode;
using Player.Components;

/// <summary>
/// Detects when a player starts a dodge dive and emits a DiveEvent for audio playback.
/// Runs in predicted simulation to ensure events fire on both client and server.
/// </summary>
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(Player.Systems.DodgeDiveSystem))]
public partial struct PlayerDiveAudioSystem : ISystem
{
    private ComponentLookup<DodgeDiveState> _diveStateLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        _diveStateLookup = state.GetComponentLookup<DodgeDiveState>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _diveStateLookup.Update(ref state);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Detect dive start: DodgeDiveState.IsActive == 1 and Elapsed near 0
        foreach (var (diveState, transform, entity) in 
                 SystemAPI.Query<RefRO<DodgeDiveState>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
        {
            var dive = diveState.ValueRO;
            
            // Only emit event at dive start (IsActive and Elapsed very small)
            if (dive.IsActive == 1 && dive.Elapsed < 0.05f)
            {
                // Check if we already have a DiveEvent (prevent duplicate emissions)
                if (state.EntityManager.HasComponent<DiveEvent>(entity))
                    continue;
                
                // Get material ID from ground or default to 0
                int materialId = 0;
                if (state.EntityManager.HasComponent<SurfaceMaterialId>(entity))
                {
                    materialId = state.EntityManager.GetComponentData<SurfaceMaterialId>(entity).Id;
                }
                
                var diveEvent = new DiveEvent
                {
                    MaterialId = materialId,
                    Position = transform.ValueRO.Position,
                    Intensity = 1.0f
                };
                
                ecb.AddComponent(entity, diveEvent);
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
