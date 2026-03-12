using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Player.Components;
using DIG.Weapons.Systems;

namespace DIG.ProceduralMotion.Systems
{
    /// <summary>
    /// EPIC 15.25 Phase 4: Camera-level procedural forces (landing + hit reaction).
    /// Runs in PredictedFixedStep because CameraSpringState is ghost-replicated.
    /// Camera forces work in ALL paradigms (ARPG players still feel damage through camera flinch).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponRecoilSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ProceduralCameraForceSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            // Read intensity
            float globalIntensity = 1f;
            float cameraScale = 1f;
            if (SystemAPI.HasSingleton<ProceduralMotionIntensity>())
            {
                var intensity = SystemAPI.GetSingleton<ProceduralMotionIntensity>();
                globalIntensity = intensity.GlobalIntensity;
                cameraScale = intensity.CameraMotionScale;
            }

            foreach (var (cameraSpring, config, playerState, velocity) in
                     SystemAPI.Query<RefRW<CameraSpringState>, RefRO<ProceduralMotionConfig>,
                             RefRO<PlayerState>, RefRO<PhysicsVelocity>>()
                         .WithAll<Simulate>())
            {
                if (!config.ValueRO.ProfileBlob.IsCreated) continue;
                ref var blob = ref config.ValueRO.ProfileBlob.Value;

                float masterScale = globalIntensity * cameraScale;
                if (masterScale < 0.001f) continue;

                // ── Camera Landing Impact ─────────────────────
                // Detect landing via PlayerState.IsGrounded transitioning from false to true.
                // PlayerState.WasGrounded tracks previous tick state (prediction-safe).
                if (playerState.ValueRO.IsGrounded && !playerState.ValueRO.WasGrounded)
                {
                    float fallSpeed = math.abs(velocity.ValueRO.Linear.y);
                    if (fallSpeed > 0.5f)
                    {
                        float impulseNorm = math.saturate(fallSpeed / math.max(blob.LandingSpeedThreshold, 0.1f));
                        float impulse = impulseNorm * blob.HitReactionCameraScale * masterScale;

                        cameraSpring.ValueRW.PositionVelocity.y -= impulse * blob.LandingPositionImpulse;
                        cameraSpring.ValueRW.RotationVelocity.x += impulse * blob.LandingRotationImpulse * 0.3f;
                    }
                }
            }
        }
    }
}
