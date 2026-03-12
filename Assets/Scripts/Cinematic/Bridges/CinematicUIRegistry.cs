namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Static provider registry for cinematic UI.
    /// MonoBehaviours register on enable, unregister on disable.
    /// Same pattern as CombatUIRegistry, AchievementUIRegistry.
    /// </summary>
    public static class CinematicUIRegistry
    {
        private static ICinematicUIProvider _provider;

        public static bool HasProvider => _provider != null;
        public static ICinematicUIProvider Provider => _provider;

        public static void Register(ICinematicUIProvider provider)
        {
            _provider = provider;
        }

        public static void Unregister(ICinematicUIProvider provider)
        {
            if (_provider == provider)
                _provider = null;
        }

        public static void OnCinematicStart(int cinematicId, CinematicType type)
        {
            _provider?.OnCinematicStart(cinematicId, type);
        }

        public static void OnCinematicEnd(int cinematicId, bool wasSkipped)
        {
            _provider?.OnCinematicEnd(cinematicId, wasSkipped);
        }

        public static void UpdateSkipPrompt(bool canSkip, int votesReceived, int totalPlayers)
        {
            _provider?.UpdateSkipPrompt(canSkip, votesReceived, totalPlayers);
        }

        public static void UpdateSubtitle(string text, float duration)
        {
            _provider?.UpdateSubtitle(text, duration);
        }

        public static void SetLetterbox(float targetHeight, float fadeDuration)
        {
            _provider?.SetLetterbox(targetHeight, fadeDuration);
        }

        public static void SetHUDVisible(bool visible, float fadeDuration)
        {
            _provider?.SetHUDVisible(visible, fadeDuration);
        }

        public static void UpdateProgress(float normalizedTime)
        {
            _provider?.UpdateProgress(normalizedTime);
        }
    }
}
