namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Static provider registry for PvP UI.
    /// MonoBehaviours register on enable, unregister on disable.
    /// Same pattern as CombatUIRegistry, CinematicUIRegistry.
    /// </summary>
    public static class PvPUIRegistry
    {
        private static IPvPUIProvider _provider;

        public static bool HasProvider => _provider != null;
        public static IPvPUIProvider Provider => _provider;

        public static void Register(IPvPUIProvider provider)
        {
            _provider = provider;
        }

        public static void Unregister(IPvPUIProvider provider)
        {
            if (_provider == provider)
                _provider = null;
        }

        public static void UpdateMatchState(PvPMatchUIState state)
        {
            _provider?.UpdateMatchState(state);
        }

        public static void UpdateScoreboard(PvPScoreboardEntry[] entries)
        {
            _provider?.UpdateScoreboard(entries);
        }

        public static void OnKillFeedEvent(PvPKillFeedUIEntry entry)
        {
            _provider?.OnKillFeedEvent(entry);
        }

        public static void OnMatchPhaseChange(PvPMatchPhase oldPhase, PvPMatchPhase newPhase)
        {
            _provider?.OnMatchPhaseChange(oldPhase, newPhase);
        }

        public static void OnMatchResult(PvPMatchResultUI result)
        {
            _provider?.OnMatchResult(result);
        }

        public static void UpdateRanking(PvPRankingUI ranking)
        {
            _provider?.UpdateRanking(ranking);
        }
    }
}
