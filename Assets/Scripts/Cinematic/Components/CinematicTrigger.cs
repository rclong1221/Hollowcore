using Unity.Entities;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Trigger component on encounter/zone entities.
    /// When a player enters TriggerRadius, the server broadcasts CinematicPlayRpc.
    /// Server|Local only -- not placed on player entities.
    /// </summary>
    public struct CinematicTrigger : IComponentData
    {
        public int CinematicId;             // 4 bytes -- references CinematicDefinitionSO
        public bool PlayOnce;               // 1 byte -- if true, trigger deactivates after first play
        public bool HasPlayed;              // 1 byte -- runtime flag, set by CinematicTriggerSystem
        public CinematicType CinematicType; // 1 byte
        public SkipPolicy SkipPolicy;       // 1 byte
        public float TriggerRadius;         // 4 bytes -- overlap sphere radius for zone triggers
        // Total: 12 bytes
    }
}
