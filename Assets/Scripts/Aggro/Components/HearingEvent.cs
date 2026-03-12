using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.19: Buffer element for sounds that can be heard by AI.
    /// Events are consumed by HearingDetectionSystem and generate threat.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct HearingEvent : IBufferElementData
    {
        /// <summary>World position of the sound source.</summary>
        public float3 Position;
        
        /// <summary>Entity that caused the sound (attacker, runner, etc.)</summary>
        public Entity SourceEntity;
        
        /// <summary>Loudness of the sound. 1.0 = normal combat, 0.5 = sneaking, 2.0 = explosion.</summary>
        public float Loudness;
        
        /// <summary>Maximum range at which this sound can be heard (before loudness multiplier).</summary>
        public float MaxRange;
    }
    
    /// <summary>
    /// Tag component indicating this entity emits sounds that AI can hear.
    /// Add to players, combat entities, or anything that makes noise.
    /// </summary>
    public struct SoundEmitter : IComponentData
    {
        /// <summary>Base loudness multiplier for this entity's sounds. 1.0 = normal.</summary>
        public float BaseLoudness;
    }
}
