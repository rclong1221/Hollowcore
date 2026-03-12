using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.NetCode;
using Unity.Physics;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Executes mantle and vault actions by smoothly interpolating player position from start to ledge top.
    /// Locks player input during execution and restores control when complete.
    /// Runs in predicted simulation for deterministic client/server behavior.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(MantleDetectionSystem))]
    public partial struct MantleExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Start new mantles from candidates
            foreach (var (candidate, mantleState, playerState, transform, entity) in
                     SystemAPI.Query<RefRO<MantleCandidate>, RefRW<MantleState>, RefRW<PlayerState>, RefRO<LocalTransform>>()
                         .WithAll<Simulate>()
                         .WithEntityAccess())
            {
                var cand = candidate.ValueRO;
                ref var mState = ref mantleState.ValueRW;
                ref var pState = ref playerState.ValueRW;
                
                // Start mantle
                var settings = MantleSettings.Default;
                if (state.EntityManager.HasComponent<MantleSettings>(entity))
                {
                    settings = state.EntityManager.GetComponentData<MantleSettings>(entity);
                }
                
                mState.IsActive = (byte)(cand.IsVault ? 2 : 1);
                mState.Progress = 0f;
                mState.Elapsed = 0f;
                mState.Duration = cand.IsVault ? settings.VaultDuration : settings.MantleDuration;
                mState.StartPosition = transform.ValueRO.Position;
                mState.EndPosition = cand.LedgePosition + new float3(0, 0.1f, 0); // Slightly above ledge
                mState.ObstacleHeight = cand.LedgeHeight;
                mState.CooldownRemaining = 0f;
                
                // Calculate vault direction (forward over obstacle)
                var towardsLedge = cand.LedgePosition - transform.ValueRO.Position;
                towardsLedge.y = 0;
                mState.VaultDirection = math.normalizesafe(towardsLedge);
                
                // For vault, extend end position forward
                if (cand.IsVault)
                {
                    mState.EndPosition += mState.VaultDirection * 0.5f;
                }
                
                // Set movement state
                pState.MovementState = cand.IsVault ? PlayerMovementState.Rolling : PlayerMovementState.Climbing;
                
                // Consume stamina if present
                if (state.EntityManager.HasComponent<PlayerStamina>(entity))
                {
                    var stamina = state.EntityManager.GetComponentData<PlayerStamina>(entity);
                    float cost = cand.IsVault ? settings.VaultStaminaCost : settings.MantleStaminaCost;
                    stamina.Current = math.max(0f, stamina.Current - cost);
                    ecb.SetComponent(entity, stamina);
                }
                
                // Remove candidate
                ecb.RemoveComponent<MantleCandidate>(entity);
            }
            
            // Update active mantles
            foreach (var (mantleState, transform, velocity, playerState, entity) in
                     SystemAPI.Query<RefRW<MantleState>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerState>>()
                         .WithAll<Simulate>()
                         .WithEntityAccess())
            {
                ref var mState = ref mantleState.ValueRW;
                
                if (mState.IsActive == 0)
                {
                    // Update cooldown
                    if (mState.CooldownRemaining > 0f)
                    {
                        var settings = MantleSettings.Default;
                        if (state.EntityManager.HasComponent<MantleSettings>(entity))
                        {
                            settings = state.EntityManager.GetComponentData<MantleSettings>(entity);
                        }
                        mState.CooldownRemaining = math.max(0f, mState.CooldownRemaining - dt);
                    }
                    continue;
                }
                
                ref var trans = ref transform.ValueRW;
                ref var vel = ref velocity.ValueRW;
                ref var pState = ref playerState.ValueRW;
                
                // Update progress
                mState.Elapsed += dt;
                mState.Progress = math.saturate(mState.Elapsed / mState.Duration);
                
                if (mState.Progress >= 1.0f)
                {
                    // Mantle/vault complete
                    trans.Position = mState.EndPosition;
                    mState.IsActive = 0;
                    mState.Progress = 0f;
                    mState.Elapsed = 0f;
                    
                    // Set cooldown
                    var settings = MantleSettings.Default;
                    if (state.EntityManager.HasComponent<MantleSettings>(entity))
                    {
                        settings = state.EntityManager.GetComponentData<MantleSettings>(entity);
                    }
                    mState.CooldownRemaining = settings.MantleCooldown;
                    
                    // Restore movement state
                    pState.MovementState = PlayerMovementState.Idle;
                    
                    // Zero velocity to prevent sudden movement
                    vel.Linear = float3.zero;
                    vel.Angular = float3.zero;
                }
                else
                {
                    // Interpolate position with ease-in-out curve
                    float t = SmoothStep(mState.Progress);
                    
                    // For vaults, use arc trajectory
                    if (mState.IsActive == 2) // Vault
                    {
                        var flatStart = mState.StartPosition;
                        var flatEnd = mState.EndPosition;
                        flatEnd.y = flatStart.y; // Ignore vertical for horizontal lerp
                        
                        var horizontalPos = math.lerp(flatStart, flatEnd, t);
                        
                        // Add parabolic arc for height
                        float arcHeight = mState.ObstacleHeight + 0.3f; // Clear obstacle with margin
                        float verticalOffset = 4f * arcHeight * t * (1f - t); // Parabola peaks at t=0.5
                        
                        trans.Position = new float3(
                            horizontalPos.x,
                            math.lerp(mState.StartPosition.y, mState.EndPosition.y, t) + verticalOffset,
                            horizontalPos.z
                        );
                    }
                    else // Mantle
                    {
                        // Simple smooth interpolation to ledge top
                        trans.Position = math.lerp(mState.StartPosition, mState.EndPosition, t);
                    }
                    
                    // Zero velocity during mantle to prevent physics interference
                    vel.Linear = float3.zero;
                    vel.Angular = float3.zero;
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        /// <summary>
        /// Smooth step interpolation (ease-in-out cubic)
        /// </summary>
        [BurstCompile]
        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
