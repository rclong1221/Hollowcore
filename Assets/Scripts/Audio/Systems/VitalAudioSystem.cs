using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;
using Player.Components;

namespace Audio.Systems
{
    /// <summary>
    /// Vital feedback audio: breathing based on stamina, heartbeat based on health/stress severity.
    /// EPIC 5.1, EPIC 15.27 Phase 8 quality upgrades:
    ///   - Uses dedicated TimeSinceLastHeartbeat field (was reusing TimeSinceLastBreath)
    ///   - Added WorldSystemFilter (Client/Local only)
    ///   - Removed debug log spam
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class VitalAudioSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (
                         vital,
                         stamina,
                         health,
                         refs,
                         entity) in
                     SystemAPI.Query<
                         RefRW<VitalAudioSource>,
                         RefRO<PlayerStamina>,
                         RefRO<Health>,
                         AudioSourceReference>()
                     .WithEntityAccess()
                     .WithAll<GhostOwnerIsLocal>())
            {
                // -- Breathing --
                float staminaRatio = math.clamp(stamina.ValueRO.Current / stamina.ValueRO.Max, 0f, 1f);
                float breathIntensity = 1.0f - staminaRatio;
                vital.ValueRW.BreathIntensity = breathIntensity;

                // Stress Logic (Optional)
                float stressRatio = 0f;
                if (SystemAPI.HasComponent<PlayerStressState>(entity))
                {
                    var stress = SystemAPI.GetComponent<PlayerStressState>(entity);
                    if (stress.MaxStress > 0)
                        stressRatio = math.clamp(stress.CurrentStress / stress.MaxStress, 0f, 1f);
                }

                // Calculate severity from Low Health OR High Stress
                float healthRatio = math.clamp(health.ValueRO.Current / health.ValueRO.Max, 0f, 1f);
                float healthSev = (healthRatio < 0.3f && health.ValueRO.Current > 0) ? (1.0f - (healthRatio / 0.3f)) : 0f;
                float stressSev = (stressRatio > 0.5f) ? (stressRatio - 0.5f) * 2.0f : 0f;

                float severity = math.max(healthSev, stressSev);
                vital.ValueRW.HeartbeatIntensity = severity;

                if (severity > 0f)
                {
                    float beatInterval = math.lerp(1.2f, 0.4f, severity);

                    // EPIC 15.27 Phase 8: Use dedicated TimeSinceLastHeartbeat (was reusing TimeSinceLastBreath)
                    vital.ValueRW.TimeSinceLastHeartbeat += dt;

                    if (vital.ValueRW.TimeSinceLastHeartbeat >= beatInterval)
                    {
                        vital.ValueRW.TimeSinceLastHeartbeat = 0;
                        if (refs.HeartbeatSource != null && refs.HeartbeatSource.clip != null)
                        {
                            refs.HeartbeatSource.pitch = 1.0f + (severity * 0.1f);
                            refs.HeartbeatSource.PlayOneShot(refs.HeartbeatSource.clip);
                        }
                    }
                }
                else
                {
                    vital.ValueRW.TimeSinceLastHeartbeat = 0;
                }
            }
        }
    }
}
