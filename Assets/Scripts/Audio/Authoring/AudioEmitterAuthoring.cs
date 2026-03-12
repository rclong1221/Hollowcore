using Unity.Entities;
using UnityEngine;
using Audio.Components;
using Audio.Config;

namespace Audio.Authoring
{
    /// <summary>
    /// Baker for AudioEmitter component. Add to any entity prefab in a subscene
    /// that should emit spatial audio tracked by the audio pipeline.
    /// EPIC 15.27 Phase 2.
    /// </summary>
    public class AudioEmitterAuthoring : MonoBehaviour
    {
        [Header("Bus Routing")]
        [Tooltip("Which audio bus this emitter routes to")]
        public AudioBusType Bus = AudioBusType.Combat;

        [Header("Priority & Spatial")]
        [Tooltip("Voice priority (0=Ambient, 50=Footstep, 100=Weapon, 200=Dialogue)")]
        [Range(0, 255)]
        public int Priority = 100;

        [Tooltip("3D spatial blend (0=2D, 1=full 3D)")]
        [Range(0f, 1f)]
        public float SpatialBlend = 1f;

        [Tooltip("Max audible distance in meters")]
        public float MaxDistance = 50f;

        [Tooltip("Rolloff mode (0=Logarithmic, 1=Linear)")]
        [Range(0, 1)]
        public int RolloffMode = 0;

        [Header("Behavior")]
        [Tooltip("Follow entity position each frame")]
        public bool TrackPosition = true;

        [Tooltip("Perform occlusion raycasts for this source")]
        public bool UseOcclusion = true;

        class Baker : Baker<AudioEmitterAuthoring>
        {
            public override void Bake(AudioEmitterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new AudioEmitter
                {
                    Bus = authoring.Bus,
                    Priority = (byte)authoring.Priority,
                    SpatialBlend = authoring.SpatialBlend,
                    MaxDistance = authoring.MaxDistance,
                    RolloffMode = (byte)authoring.RolloffMode,
                    TrackPosition = authoring.TrackPosition,
                    UseOcclusion = authoring.UseOcclusion
                });
            }
        }
    }
}
