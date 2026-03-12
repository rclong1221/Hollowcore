using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;

namespace Audio.Accessibility
{
    /// <summary>
    /// Gathers active audio sources and their relative direction to the listener,
    /// then provides data to SoundRadarRenderer for HUD display.
    /// Only active when AudioAccessibilityConfig.EnableSoundRadar is true.
    /// EPIC 15.27 Phase 7.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Audio.Systems.CombatMusicDuckSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class SoundRadarSystem : SystemBase
    {
        public struct RadarPip
        {
            public float Angle;       // 0-360 degrees relative to camera forward
            public float Distance;     // Distance to source (meters)
            public float Intensity;    // 0-1 based on volume and proximity
            public RadarPipType Type;  // Color coding
        }

        public enum RadarPipType : byte
        {
            Danger = 0,   // Red: weapons, explosions
            Friendly = 1, // Blue: allies
            Neutral = 2,  // Yellow: environment, dialogue
            Ambient = 3   // White: ambient sounds
        }

        // Static access for the renderer
        public static readonly List<RadarPip> ActivePips = new List<RadarPip>(32);
        public static bool IsActive { get; private set; }

        private AudioAccessibilityConfig _config;

        protected override void OnUpdate()
        {
            if (_config == null)
            {
                _config = Object.FindAnyObjectByType<AudioAccessibilityConfigHolder>()?.Config;
                if (_config == null) return;
            }

            if (!_config.EnableSoundRadar)
            {
                IsActive = false;
                ActivePips.Clear();
                return;
            }

            IsActive = true;
            ActivePips.Clear();

            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null) return;

            Vector3 listenerPos = listener.transform.position;
            Vector3 listenerFwd = listener.transform.forward;

            foreach (var (state, emitter, ltw) in
                     SystemAPI.Query<AudioSourceState, RefRO<AudioEmitter>, RefRO<LocalToWorld>>())
            {
                if (state.Source == null || !state.Source.isPlaying) continue;
                if (emitter.ValueRO.Priority < _config.RadarMinPriority) continue;

                Vector3 sourcePos = ltw.ValueRO.Position;
                Vector3 toSource = sourcePos - listenerPos;
                float distance = toSource.magnitude;

                if (distance < 0.5f) continue; // Too close to show

                // Calculate angle relative to listener forward (2D, XZ plane)
                Vector3 flatDir = new Vector3(toSource.x, 0, toSource.z).normalized;
                Vector3 flatFwd = new Vector3(listenerFwd.x, 0, listenerFwd.z).normalized;
                float angle = Vector3.SignedAngle(flatFwd, flatDir, Vector3.up);
                if (angle < 0) angle += 360f;

                // Intensity based on proximity and volume
                float intensity = Mathf.Clamp01(1f - distance / emitter.ValueRO.MaxDistance) * state.Source.volume;

                // Determine pip type from bus
                RadarPipType pipType;
                switch (emitter.ValueRO.Bus)
                {
                    case Audio.Config.AudioBusType.Combat:
                        pipType = RadarPipType.Danger;
                        break;
                    case Audio.Config.AudioBusType.Dialogue:
                        pipType = RadarPipType.Neutral;
                        break;
                    case Audio.Config.AudioBusType.Ambient:
                        pipType = RadarPipType.Ambient;
                        break;
                    default:
                        pipType = RadarPipType.Neutral;
                        break;
                }

                ActivePips.Add(new RadarPip
                {
                    Angle = angle,
                    Distance = distance,
                    Intensity = intensity,
                    Type = pipType
                });
            }
        }
    }

    /// <summary>
    /// MonoBehaviour holder for AudioAccessibilityConfig reference.
    /// </summary>
    public class AudioAccessibilityConfigHolder : MonoBehaviour
    {
        public AudioAccessibilityConfig Config;
    }
}
