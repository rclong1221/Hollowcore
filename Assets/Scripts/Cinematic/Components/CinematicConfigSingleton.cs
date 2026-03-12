using Unity.Entities;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Runtime cinematic configuration singleton (Client|Local).
    /// Values loaded from CinematicDatabaseSO at bootstrap.
    /// </summary>
    public struct CinematicConfigSingleton : IComponentData
    {
        public SkipPolicy DefaultSkipPolicy; // 1 byte
        public float BlendInDuration;        // 4 bytes -- camera blend in (default 0.5s)
        public float BlendOutDuration;       // 4 bytes -- camera blend out (default 0.5s)
        public float HUDFadeDuration;        // 4 bytes -- HUD fade in/out (default 0.3s)
        public float LetterboxHeight;        // 4 bytes -- letterbox bar height fraction (default 0.12)
        // Total: ~20 bytes
    }
}
