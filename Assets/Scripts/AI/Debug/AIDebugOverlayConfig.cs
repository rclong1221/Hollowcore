namespace DIG.AI.Overlay
{
    /// <summary>
    /// Static configuration for the AI debug overlay.
    /// Set from the AI Workstation Overlay tab, read by AIDebugOverlaySystem.
    /// </summary>
    public static class AIDebugOverlayConfig
    {
        public static bool Enabled;
        public static bool ShowState = true;
        public static bool ShowSubState = true;
        public static bool ShowThreatValue = true;
        public static bool ShowTargetName;
        public static bool ShowActiveAbility = true;
        public static bool ShowHealthPercent;

        // Filters
        public static bool OnlyCombat;
        public static bool OnlyAggroed;
        public static float MaxCameraDistance = 50f;

        // Visual
        public static int FontSize = 12;
        public static float BackgroundAlpha = 0.6f;
    }
}
