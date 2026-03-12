using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player.Components;
using DIG.Performance;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DIG.Player.Systems
{
    /// <summary>
    /// Simple controller vibration for local player collisions (Epic 7.4.4).
    /// Uses Unity Input System if enabled.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CollisionEventClearSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class LocalPlayerCollisionHapticsSystem : SystemBase
    {
#if ENABLE_INPUT_SYSTEM
        private float _timer;
        private float _duration;
        private float _low;
        private float _high;
#endif

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerCollisionSettings>();
            RequireForUpdate<GhostOwnerIsLocal>();
        }

        protected override void OnUpdate()
        {
            // Epic 7.7.1: Profile haptics system
            using (CollisionProfilerMarkers.Haptics.Auto())
            {
#if ENABLE_INPUT_SYSTEM
            var settings = SystemAPI.GetSingleton<PlayerCollisionSettings>();

            // Apply ongoing rumble.
            if (_timer > 0f)
            {
                _timer -= SystemAPI.Time.DeltaTime;
                var pad = Gamepad.current;
                if (pad != null)
                {
                    pad.SetMotorSpeeds(_low, _high);
                }

                if (_timer <= 0f)
                {
                    if (pad != null)
                    {
                        pad.SetMotorSpeeds(0f, 0f);
                    }
                }
            }

            // Start/refresh rumble on heavy collision events.
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

                float t = Mathf.InverseLerp(settings.CameraShakeForceThreshold, settings.CollisionAudioMaxForce, maxForce);
                t = Mathf.Clamp01(t);

                _duration = Mathf.Max(0.05f, settings.CameraShakeDuration);
                _timer = _duration;

                // Light low-frequency + stronger high-frequency motor.
                _low = Mathf.Lerp(0.05f, 0.25f, t);
                _high = Mathf.Lerp(0.15f, 0.65f, t);

                var pad = Gamepad.current;
                if (pad != null)
                {
                    pad.SetMotorSpeeds(_low, _high);
                }
            }
#else
            // Input System not enabled; no-op.
#endif
            } // End Haptics profiler marker
        }

        protected override void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                pad.SetMotorSpeeds(0f, 0f);
            }
#endif
        }
    }
}
