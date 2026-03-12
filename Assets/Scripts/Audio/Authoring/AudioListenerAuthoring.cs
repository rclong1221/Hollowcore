using Unity.Entities;
using UnityEngine;
using Audio.Components;

namespace Audio.Authoring
{
    public class AudioListenerAuthoring : MonoBehaviour
    {
        [Header("Vital Audio Sources")]
        public AudioSource BreathSource;
        public AudioSource HeartbeatSource;
        
        [Header("Defaults (Optional)")]
        public AudioClip DefaultBreathClip;
        public AudioClip DefaultHeartbeatClip;
        
        public class Baker : Baker<AudioListenerAuthoring>
        {
            public override void Bake(AudioListenerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new AudioListenerState
                {
                    CurrentZoneType = DIG.Survival.Environment.EnvironmentZoneType.Pressurized,
                    PressureFactor = 1.0f,
                    IsDeafened = false,
                    DeafenTimer = 0f
                });
                
                AddComponent(entity, new VitalAudioSource
                {
                    BreathIntensity = 0f,
                    HeartbeatIntensity = 0f,
                    TimeSinceLastBreath = 0f,
                    LastGruntTime = 0.0
                });

                // Validation
                if (authoring.BreathSource != null)
                {
                     if (authoring.DefaultBreathClip != null && authoring.BreathSource.clip == null)
                         authoring.BreathSource.clip = authoring.DefaultBreathClip;
                         
                     authoring.BreathSource.loop = true;
                     authoring.BreathSource.playOnAwake = false;
                }
                
                if (authoring.HeartbeatSource != null)
                {
                     if (authoring.DefaultHeartbeatClip != null && authoring.HeartbeatSource.clip == null)
                         authoring.HeartbeatSource.clip = authoring.DefaultHeartbeatClip;
                         
                     authoring.HeartbeatSource.loop = false; // Heartbeat handled by PlayOneShot usually, or loop?
                     // System uses PlayOneShot for heartbeat in VitalAudioSystem.
                     authoring.HeartbeatSource.playOnAwake = false;
                }

                AddComponentObject(entity, new AudioSourceReference
                {
                    BreathSource = authoring.BreathSource,
                    HeartbeatSource = authoring.HeartbeatSource
                });
            }
        }
    }
}
