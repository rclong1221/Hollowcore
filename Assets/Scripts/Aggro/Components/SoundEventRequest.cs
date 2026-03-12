using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Lightweight request-entity for sound propagation.
    /// Source systems create transient entities with this component.
    /// SoundDistributionSystem reads all requests, distributes HearingEvents
    /// to nearby AI, then destroys the request entities.
    ///
    /// Uses the request-entity pattern (like PendingCombatHit) to avoid
    /// adding buffers to player entities (16KB archetype limit).
    /// </summary>
    public struct SoundEventRequest : IComponentData
    {
        public float3 Position;
        public Entity SourceEntity;
        public float Loudness;
        public float MaxRange;
        public SoundCategory Category;
    }

    /// <summary>
    /// Sound categories for AI hearing differentiation.
    /// Different categories may produce different alert behaviors.
    /// </summary>
    public enum SoundCategory : byte
    {
        Gunfire       = 0,
        Explosion     = 1,
        Movement      = 2,
        Combat        = 3,
        Ability       = 4,
        Environmental = 5
    }
}
