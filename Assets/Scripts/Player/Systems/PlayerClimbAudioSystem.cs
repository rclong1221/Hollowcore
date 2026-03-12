using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.NetCode;
using Player.Components;

/// <summary>
/// Detects when a player starts climbing and emits a ClimbStartEvent for audio playback.
/// Runs in predicted simulation to ensure events fire on both client and server.
/// </summary>
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
public partial struct PlayerClimbAudioSystem : ISystem
{
    private ComponentLookup<FreeClimbState> _climbStateLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        _climbStateLookup = state.GetComponentLookup<FreeClimbState>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _climbStateLookup.Update(ref state);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var currentTime = SystemAPI.Time.ElapsedTime;
        
        // Detect climb start: FreeClimbState.IsClimbing == true and recently mounted
        foreach (var (climbState, transform, entity) in 
                 SystemAPI.Query<RefRO<FreeClimbState>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
        {
            var climb = climbState.ValueRO;
            
            // Only emit event at climb start (IsClimbing and recently mounted within 0.1s)
            if (climb.IsClimbing && (currentTime - climb.MountTime) < 0.1)
            {
                // Check if we already have a ClimbStartEvent (prevent duplicate emissions)
                if (state.EntityManager.HasComponent<ClimbStartEvent>(entity))
                    continue;
                
                // Get material ID from climb target or default to 0
                int materialId = 0;
                if (state.EntityManager.HasComponent<SurfaceMaterialId>(entity))
                {
                    materialId = state.EntityManager.GetComponentData<SurfaceMaterialId>(entity).Id;
                }
                
                var climbEvent = new ClimbStartEvent
                {
                    MaterialId = materialId,
                    Position = transform.ValueRO.Position
                };
                
                ecb.AddComponent(entity, climbEvent);
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
