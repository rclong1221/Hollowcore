namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Interface for MonoBehaviour UI providers that display
    /// cinematic overlays (letterbox, skip prompt, subtitles, HUD fade).
    /// Follows ICombatUIProvider / IAchievementUIProvider pattern.
    /// </summary>
    public interface ICinematicUIProvider
    {
        void OnCinematicStart(int cinematicId, CinematicType type);
        void OnCinematicEnd(int cinematicId, bool wasSkipped);
        void UpdateSkipPrompt(bool canSkip, int votesReceived, int totalPlayers);
        void UpdateSubtitle(string text, float duration);
        void SetLetterbox(float targetHeight, float fadeDuration);
        void SetHUDVisible(bool visible, float fadeDuration);
        void UpdateProgress(float normalizedTime);
    }
}
