using Unity.Entities;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Cinematic playback type determining behavior matrix.
    /// </summary>
    public enum CinematicType : byte
    {
        FullCinematic = 0,  // Full camera control + animations + letterbox
        InWorldEvent  = 1,  // NPC animations in world, no camera takeover
        TextOverlay   = 2   // Text + audio overlay, no camera change
    }

    /// <summary>
    /// EPIC 17.9: Multiplayer skip behavior policy.
    /// </summary>
    public enum SkipPolicy : byte
    {
        AnyoneCanSkip = 0,  // First skip request ends cinematic
        MajorityVote  = 1,  // >50% of players must skip
        AllMustSkip   = 2,  // Every player must skip
        NoSkip        = 3   // Unskippable (tutorials, critical story)
    }

    /// <summary>
    /// EPIC 17.9: Client-world singleton tracking active cinematic playback state.
    /// NOT ghost-replicated -- client singleton only.
    /// Zero impact on player entity archetype.
    /// </summary>
    public struct CinematicState : IComponentData
    {
        public bool IsPlaying;              // 1 byte
        public int CurrentCinematicId;      // 4 bytes -- maps to CinematicDefinitionSO
        public float ElapsedTime;           // 4 bytes -- seconds since playback started
        public bool CanSkip;                // 1 byte
        public byte SkipVotesReceived;      // 1 byte -- server broadcasts updated count
        public byte TotalPlayersInScene;    // 1 byte -- for skip UI "2/4 voted"
        public CinematicType CinematicType; // 1 byte
        public float Duration;              // 4 bytes -- total cinematic duration
        public float BlendProgress;         // 4 bytes -- 0-1 camera blend interpolant
        // Total: ~24 bytes
    }
}
