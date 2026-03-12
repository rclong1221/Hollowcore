using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Audio.Components;

namespace Audio.Systems
{
    /// <summary>
    /// Triggers tinnitus (temporary deafness) when the player takes a large explosion.
    /// Sets AudioListenerState.IsDeafened + DeafenTimer. AudioEnvironmentSystem handles
    /// the mixer ducking and recovery.
    /// Plays a high-pitched sine tone (12kHz) at low volume during deafness.
    /// EPIC 15.27 Phase 6.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AudioPrioritySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class TinnitusFeedbackSystem : SystemBase
    {
        private AudioSource _tinnitusSource;
        private AudioClip _tinnitusClip;

        private const float kDamageThreshold = 50f;
        private const float kDeafenDuration = 3.0f;
        private const float kTinnitusFrequency = 12000f;
        private const float kTinnitusVolume = 0.3f;

        protected override void OnCreate()
        {
            // Generate tinnitus tone clip (12kHz sine with envelope fade-out)
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * kDeafenDuration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = (t > kDeafenDuration - 1.5f) ? (kDeafenDuration - t) / 1.5f : 1f;
                data[i] = Mathf.Sin(2f * Mathf.PI * kTinnitusFrequency * t) * envelope * 0.5f;
            }
            _tinnitusClip = AudioClip.Create("TinnitusTone", samples, 1, sampleRate, false);
            _tinnitusClip.SetData(data, 0);
        }

        protected override void OnUpdate()
        {
            foreach (var (listenerState, entity) in
                     SystemAPI.Query<RefRW<AudioListenerState>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                // Check for new explosion damage on the player
                if (SystemAPI.HasBuffer<Player.Components.DamageEvent>(entity))
                {
                    var dmgBuffer = SystemAPI.GetBuffer<Player.Components.DamageEvent>(entity);
                    for (int i = 0; i < dmgBuffer.Length; i++)
                    {
                        var evt = dmgBuffer[i];
                        if (evt.Type == Player.Components.DamageType.Explosion && evt.Amount >= kDamageThreshold)
                        {
                            listenerState.ValueRW.IsDeafened = true;
                            listenerState.ValueRW.DeafenTimer = kDeafenDuration;
                            PlayTinnitusTone();
                            break;
                        }
                    }
                }

                // Stop tinnitus source when recovered
                if (!listenerState.ValueRO.IsDeafened && _tinnitusSource != null && _tinnitusSource.isPlaying)
                {
                    _tinnitusSource.Stop();
                }
            }
        }

        private void PlayTinnitusTone()
        {
            if (_tinnitusSource == null)
            {
                var go = new GameObject("TinnitusToneSource");
                _tinnitusSource = go.AddComponent<AudioSource>();
                _tinnitusSource.spatialBlend = 0f; // 2D — always on top
                _tinnitusSource.playOnAwake = false;
                _tinnitusSource.loop = false;
                Object.DontDestroyOnLoad(go);
            }

            _tinnitusSource.clip = _tinnitusClip;
            _tinnitusSource.volume = kTinnitusVolume;
            _tinnitusSource.Play();
        }

        protected override void OnDestroy()
        {
            if (_tinnitusSource != null)
                Object.Destroy(_tinnitusSource.gameObject);
        }
    }
}
