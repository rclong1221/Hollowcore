using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.NetCode;
using Player.Components;
using Player.Systems;

/// <summary>
/// Detects when a player starts a dodge roll and emits a RollEvent for audio playback.
/// Runs in predicted simulation to ensure events fire on both client and server.
/// </summary>
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(DodgeRollSystem))]
public partial struct PlayerRollAudioSystem : ISystem
{
    private ComponentLookup<DodgeRollState> _rollStateLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        _rollStateLookup = state.GetComponentLookup<DodgeRollState>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _rollStateLookup.Update(ref state);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Detect roll start: DodgeRollState.IsActive == 1 and Elapsed near 0
        foreach (var (rollState, transform, entity) in 
                 SystemAPI.Query<RefRO<DodgeRollState>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
        {
            var roll = rollState.ValueRO;
            
            // Only emit event at roll start (IsActive and Elapsed very small)
            if (roll.IsActive == 1 && roll.Elapsed < 0.05f)
            {
                // Check if we already have a RollEvent (prevent duplicate emissions)
                if (state.EntityManager.HasComponent<RollEvent>(entity))
                    continue;
                
                // Get material ID from ground or default to 0
                int materialId = 0;
                if (state.EntityManager.HasComponent<SurfaceMaterialId>(entity))
                {
                    materialId = state.EntityManager.GetComponentData<SurfaceMaterialId>(entity).Id;
                }
                
                var rollEvent = new RollEvent
                {
                    MaterialId = materialId,
                    Position = transform.ValueRO.Position,
                    Intensity = 1.0f
                };
                
                ecb.AddComponent(entity, rollEvent);
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
