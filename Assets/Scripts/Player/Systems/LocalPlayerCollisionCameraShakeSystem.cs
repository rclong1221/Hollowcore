using Unity.Entities;
using Unity.NetCode;
using DIG.Player.Components;
using DIG.Performance;
using DIG.Core.Feedback; // Added for Feedback Manager

namespace DIG.Player.Systems
{
    /// <summary>
    /// Triggers gameplay feedback (Damage/HeavyHit) on local player collisions.
    /// Replaces legacy CameraShake logic with FEEL integration.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CollisionEventClearSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct LocalPlayerCollisionCameraShakeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerCollisionSettings>();
            state.RequireForUpdate<GhostOwnerIsLocal>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Epic 7.7.1: Profile camera shake system
            using (CollisionProfilerMarkers.CameraShake.Auto())
            {
            var settings = SystemAPI.GetSingleton<PlayerCollisionSettings>();
            
            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<CollisionEvent>>()
                         .WithAll<PlayerTag, GhostOwnerIsLocal>()
                         .WithEntityAccess())
            {
                if (events.Length == 0)
                    continue;

                float maxForce = 0f;
                for (int i = 0; i < events.Length; i++)
                {
                    var ev = events[i];
                    if (ev.HitDirection == 3) // evaded
                        continue;
                    if (ev.ImpactForce > maxForce)
                        maxForce = ev.ImpactForce;
                }

                if (maxForce <= settings.CameraShakeForceThreshold)
                    continue;

                // Calculate intensity (0-1)
                float t = (maxForce - settings.CameraShakeForceThreshold) / (settings.CollisionAudioMaxForce - settings.CameraShakeForceThreshold);
                t = Unity.Mathematics.math.clamp(t, 0f, 1f);

                // Trigger FEEL Feedback
                // Use GameplayFeedbackManager global bridge
                if (t > 0.8f) 
                {
                    GameplayFeedbackManager.TriggerHeavyHit();
                }
                else
                {
                    GameplayFeedbackManager.TriggerDamage(t);
                }
            }
            } // End CameraShake profiler marker
        }
    }
}
