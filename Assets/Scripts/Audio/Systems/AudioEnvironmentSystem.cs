using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;
using Audio.Zones;
using DIG.Survival.Environment;

namespace Audio.Systems
{
    /// <summary>
    /// Manages audio environment based on player location.
    /// Handles vacuum low-pass filtering, reverb zone state, indoor factor, and tinnitus recovery.
    /// EPIC 5.1 (vacuum), EPIC 15.27 Phase 4 (reverb zones + indoor), Phase 6 (tinnitus), Phase 8 (quality).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)] // EPIC 15.27 Phase 8
    public partial class AudioEnvironmentSystem : SystemBase
    {
        private AudioManager _audioManager;
        private bool _audioManagerCached; // EPIC 15.27 Phase 8: avoid FindFirstObjectByType every frame

        protected override void OnUpdate()
        {
            // EPIC 15.27 Phase 8: Cache AudioManager reference instead of FindFirstObjectByType every frame
            if (!_audioManagerCached)
            {
                _audioManager = Object.FindFirstObjectByType<AudioManager>();
                if (_audioManager == null) return;
                _audioManagerCached = true;
            }

            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (listenerState, currentZone) in
                     SystemAPI.Query<RefRW<AudioListenerState>, RefRO<CurrentEnvironmentZone>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                // --- Vacuum Pressure ---
                float targetPressure = (currentZone.ValueRO.ZoneType == EnvironmentZoneType.Vacuum) ? 0.0f : 1.0f;
                float current = listenerState.ValueRO.PressureFactor;
                float speed = 2.0f; // 0.5s transition
                float newPressure = math.lerp(current, targetPressure, dt * speed);
                listenerState.ValueRW.PressureFactor = newPressure;

                // Apply vacuum filtering to Mixer
                if (_audioManager.MasterMixer != null)
                {
                    float cutoff = math.lerp(400f, 22000f, newPressure);
                    _audioManager.MasterMixer.SetFloat(_audioManager.VacuumCutoffParam, cutoff);
                }

                // --- EPIC 15.27 Phase 4: Reverb Zone + Indoor Factor ---
                var reverbMgr = AudioReverbZoneManager.Instance;
                if (reverbMgr != null)
                {
                    var activeZone = reverbMgr.CurrentZone;
                    listenerState.ValueRW.ReverbZoneId = activeZone != null ? activeZone.GetInstanceID() : -1;
                    listenerState.ValueRW.IndoorFactor = reverbMgr.IndoorFactor;
                }

                // --- EPIC 15.27 Phase 6: Tinnitus Recovery ---
                if (listenerState.ValueRO.IsDeafened)
                {
                    listenerState.ValueRW.DeafenTimer -= dt;
                    if (listenerState.ValueRW.DeafenTimer <= 0f)
                    {
                        listenerState.ValueRW.IsDeafened = false;
                        listenerState.ValueRW.DeafenTimer = 0f;
                    }

                    // Duck master bus during tinnitus: -20dB, fade back over last 1.5s
                    if (_audioManager.MasterMixer != null)
                    {
                        float timer = listenerState.ValueRO.DeafenTimer;
                        float duckDB = (timer > 1.5f) ? -20f : math.lerp(0f, -20f, timer / 1.5f);
                        _audioManager.MasterMixer.SetFloat("MasterVolume", duckDB);
                    }
                }
            }
        }
    }
}
