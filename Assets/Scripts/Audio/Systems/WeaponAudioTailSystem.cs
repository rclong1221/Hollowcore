using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;
using Audio.Config;
using DIG.Weapons;

namespace Audio.Systems
{
    /// <summary>
    /// Adds reverb tail samples after weapon fire events based on the current reverb zone.
    /// Outdoor: long-decay tail (2-3s). Indoor: short metallic reflection (0.5s).
    /// The tail is played on the AmbientBus so it persists after the gunshot.
    /// EPIC 15.27 Phase 6.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AudioPrioritySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class WeaponAudioTailSystem : SystemBase
    {
        private AudioSourcePool _pool;
        private bool _wasFiring;

        // Assign via WeaponAudioTailConfig holder MonoBehaviour in scene
        public static AudioClip OutdoorTailClip;
        public static AudioClip IndoorTailClip;

        protected override void OnUpdate()
        {
            if (_pool == null)
            {
                _pool = AudioSourcePool.Instance;
                if (_pool == null) return;
            }

            // Read the local player's listener state for indoor factor
            float indoorFactor = 0f;
            foreach (var listenerState in SystemAPI.Query<RefRO<AudioListenerState>>().WithAll<GhostOwnerIsLocal>())
            {
                indoorFactor = listenerState.ValueRO.IndoorFactor;
            }

            // Detect weapon fire on local player using existing WeaponFireState (DIG.Weapons)
            foreach (var fireState in SystemAPI.Query<RefRO<WeaponFireState>>().WithAll<GhostOwnerIsLocal>())
            {
                bool isFiring = fireState.ValueRO.IsFiring;

                // Only trigger on rising edge (was not firing, now firing)
                if (isFiring && !_wasFiring)
                {
                    var clip = indoorFactor > 0.5f ? IndoorTailClip : OutdoorTailClip;
                    if (clip != null)
                    {
                        var pooled = _pool.Acquire(AudioBusType.Ambient, 30);
                        pooled.Source.clip = clip;
                        pooled.Source.volume = indoorFactor > 0.5f ? 0.4f : 0.25f;
                        pooled.Source.spatialBlend = 0.3f;
                        pooled.Source.Play();
                    }
                }

                _wasFiring = isFiring;
            }
        }
    }
}
