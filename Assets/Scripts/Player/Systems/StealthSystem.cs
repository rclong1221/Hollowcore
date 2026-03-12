using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Player.Components;
using DIG.Surface;

namespace Player.Systems
{
    /// <summary>
    /// Calculates player noise level based on:
    /// 1. Movement Speed
    /// 2. Physical Stance (Prone/Crouch/Stand)
    /// 3. Surface Material (EPIC 16.10 — reads GroundSurfaceState + SurfaceGameplayBlob)
    ///
    /// Emits PlayerNoiseStatus updates for AI/UI.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct StealthSystem : ISystem
    {
        private ComponentLookup<GroundSurfaceState> _groundSurfaceLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
            _groundSurfaceLookup = state.GetComponentLookup<GroundSurfaceState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _groundSurfaceLookup.Update(ref state);

            // Read surface gameplay config blob (graceful fallback if absent)
            bool hasConfig = SystemAPI.TryGetSingleton<SurfaceGameplayConfigSingleton>(out var configSingleton);

            foreach (var (noiseStatus, stealthSettings, pState, velocity, transform, entity) in
                     SystemAPI.Query<RefRW<PlayerNoiseStatus>, RefRO<StealthSettings>, RefRO<PlayerState>, RefRO<PhysicsVelocity>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                var settings = stealthSettings.ValueRO;
                var stateRO = pState.ValueRO;
                float speed = math.length(velocity.ValueRO.Linear);
                float3 position = transform.ValueRO.Position;

                // 1. Threshold Check
                if (speed < settings.SpeedThreshold)
                {
                    noiseStatus.ValueRW.CurrentNoiseLevel = 0f;
                    noiseStatus.ValueRW.IsEmittingNoise = false;
                    continue;
                }

                // 2. Base Noise from Speed
                float speedFactor = math.clamp(speed / 4.0f, 0f, 2.0f);
                float baseNoise = speedFactor;

                // 3. Stance Multiplier
                float stanceMultiplier = 1.0f;

                if (stateRO.MovementState == PlayerMovementState.Sprinting)
                {
                    stanceMultiplier = 2.0f;
                }
                else
                {
                    switch (stateRO.Stance)
                    {
                        case PlayerStance.Prone:
                            stanceMultiplier = settings.ProneNoiseMultiplier;
                            break;
                        case PlayerStance.Crouching:
                            stanceMultiplier = settings.CrouchNoiseMultiplier;
                            break;
                        case PlayerStance.Standing:
                        default:
                            stanceMultiplier = 1.0f;
                            break;
                    }
                }

                // 4. Surface Multiplier (EPIC 16.10)
                float surfaceMultiplier = 1.0f;
                if (hasConfig && _groundSurfaceLookup.HasComponent(entity))
                {
                    var groundSurface = _groundSurfaceLookup[entity];
                    if (groundSurface.IsGrounded)
                    {
                        int idx = (int)groundSurface.SurfaceId;
                        ref var blob = ref configSingleton.Config.Value;
                        if (idx >= 0 && idx < blob.Modifiers.Length)
                            surfaceMultiplier = blob.Modifiers[idx].NoiseMultiplier;
                    }
                }

                // 5. Final Calculation
                float finalNoise = baseNoise * stanceMultiplier * surfaceMultiplier;

                // Update Status
                noiseStatus.ValueRW.CurrentNoiseLevel = finalNoise;
                noiseStatus.ValueRW.IsEmittingNoise = finalNoise > 0.01f;
                if (noiseStatus.ValueRW.IsEmittingNoise)
                {
                    noiseStatus.ValueRW.LastNoisePosition = position;
                }
            }
        }
    }
}
