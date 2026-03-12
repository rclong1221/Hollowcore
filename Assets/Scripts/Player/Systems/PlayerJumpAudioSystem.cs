using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.NetCode;
using Player.Components;

/// <summary>
/// Detects when a player jumps and emits a JumpEvent for audio playback.
/// Runs in predicted simulation to ensure events fire on both client and server.
/// </summary>
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerStateSystem))]
public partial struct PlayerJumpAudioSystem : ISystem
{
    private ComponentLookup<PlayerState> _playerStateLookup;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        _playerStateLookup = state.GetComponentLookup<PlayerState>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _playerStateLookup.Update(ref state);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Detect jump transitions: MovementState changed to Jumping
        foreach (var (playerState, transform, entity) in 
                 SystemAPI.Query<RefRO<PlayerState>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
        {
            var pState = playerState.ValueRO;
            
            // Only emit event when just started jumping (check for IsGrounded=false helps avoid repeat triggers)
            // Fix: Check WasGrounded ensures we only trigger on the FRAME we leave the ground.
            if (pState.MovementState == PlayerMovementState.Jumping && !pState.IsGrounded && pState.WasGrounded)
            {
                // Check if we already have a JumpEvent (prevent duplicate emissions in same frame)
                if (state.EntityManager.HasComponent<JumpEvent>(entity))
                    continue;
                
                // Get material ID from ground or default to 0
                int materialId = 0;
                if (state.EntityManager.HasComponent<SurfaceMaterialId>(entity))
                {
                    materialId = state.EntityManager.GetComponentData<SurfaceMaterialId>(entity).Id;
                }
                
                var jumpEvent = new JumpEvent
                {
                    MaterialId = materialId,
                    Position = transform.ValueRO.Position,
                    Intensity = 1.0f
                };
                
                ecb.AddComponent(entity, jumpEvent);
            }
        }
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
