using Unity.Entities;
using Audio.Config;

namespace Audio.Components
{
    /// <summary>
    /// Marks an entity as an audio source that should be tracked by the spatial audio pipeline.
    /// Baked by AudioEmitterAuthoring. The AudioSourcePoolSystem reads this to
    /// acquire/release pooled AudioSources and link them to the entity.
    /// EPIC 15.27 Phase 2.
    /// </summary>
    public struct AudioEmitter : IComponentData
    {
        /// <summary>Audio bus for mixer routing.</summary>
        public AudioBusType Bus;

        /// <summary>Voice priority. Higher = harder to cull (0=Ambient, 50=Footstep, 100=Weapon, 200=Dialogue).</summary>
        public byte Priority;

        /// <summary>3D spatial blend (0=2D, 1=full 3D). Set by paradigm or emitter config.</summary>
        public float SpatialBlend;

        /// <summary>Max audible distance in meters. Beyond this, source is culled.</summary>
        public float MaxDistance;

        /// <summary>Rolloff mode index (0=Logarithmic, 1=Linear, 2=Custom).</summary>
        public byte RolloffMode;

        /// <summary>Whether this emitter should follow the entity position each frame.</summary>
        public bool TrackPosition;

        /// <summary>Whether occlusion raycasts should be performed for this source.</summary>
        public bool UseOcclusion;
    }
}
