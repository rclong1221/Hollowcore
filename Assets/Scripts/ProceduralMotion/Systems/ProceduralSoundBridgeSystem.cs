using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Core.Feedback;

namespace DIG.ProceduralMotion.Systems
{
    /// <summary>
    /// EPIC 15.25 Phase 6: Bridges weapon spring velocity to foley audio events.
    /// When the weapon spring velocity exceeds a threshold, triggers weapon rattle/clank sounds
    /// via GameplayFeedbackManager. Silent when weapon is at rest.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WeaponSpringSolverSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class ProceduralSoundBridgeSystem : SystemBase
    {
        private float _cooldown;

        private const float RattleVelocityThreshold = 2f;
        private const float HeavyRattleThreshold = 5f;
        private const float MinCooldown = 0.08f; // Max ~12 rattles/sec

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            _cooldown -= dt;

            if (!GameplayFeedbackManager.Instance) return;

            foreach (var (spring, motionState) in
                     SystemAPI.Query<RefRO<WeaponSpringState>, RefRO<ProceduralMotionState>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                if (motionState.ValueRO.FPMotionWeight < 0.001f) continue;

                // Total spring velocity magnitude
                float posVel = math.length(spring.ValueRO.PositionVelocity);
                float rotVel = math.length(spring.ValueRO.RotationVelocity);
                float totalVel = posVel * 10f + rotVel; // Weight position more (meters vs degrees)

                if (totalVel > RattleVelocityThreshold && _cooldown <= 0f)
                {
                    float intensity = math.saturate((totalVel - RattleVelocityThreshold) /
                                                    (HeavyRattleThreshold - RattleVelocityThreshold));

                    // Use the feedback manager's foley system
                    // OnFire is the closest existing trigger for weapon movement sounds.
                    // A dedicated weapon foley trigger would be ideal but we work with what exists.
                    if (intensity > 0.5f)
                        GameplayFeedbackManager.Instance.OnHeavyHit();
                    else
                        GameplayFeedbackManager.Instance.OnFire();

                    _cooldown = MinCooldown;
                }
            }
        }
    }
}
