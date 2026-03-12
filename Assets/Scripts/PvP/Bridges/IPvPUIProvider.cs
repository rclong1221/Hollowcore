namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Interface for MonoBehaviour UI providers that display
    /// PvP match state (scoreboard, timer, kill feed, ranking).
    /// Follows ICombatUIProvider / ICinematicUIProvider pattern.
    /// </summary>
    public interface IPvPUIProvider
    {
        void UpdateMatchState(PvPMatchUIState state);
        void UpdateScoreboard(PvPScoreboardEntry[] entries);
        void OnKillFeedEvent(PvPKillFeedUIEntry entry);
        void OnMatchPhaseChange(PvPMatchPhase oldPhase, PvPMatchPhase newPhase);
        void OnMatchResult(PvPMatchResultUI result);
        void UpdateRanking(PvPRankingUI ranking);
    }
}
