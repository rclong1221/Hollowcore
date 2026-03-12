namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: UI state structs passed from PvPUIBridgeSystem to IPvPUIProvider.
    /// Plain C# structs (not ECS) for managed UI consumption.
    /// </summary>
    public struct PvPMatchUIState
    {
        public PvPMatchPhase Phase;
        public PvPGameMode GameMode;
        public float TimeRemaining;
        public int TeamScore0;
        public int TeamScore1;
        public int TeamScore2;
        public int TeamScore3;
        public int MaxScore;
        public int LocalPlayerKills;
        public int LocalPlayerDeaths;
        public int LocalPlayerAssists;

        public int GetTeamScore(int index)
        {
            switch (index)
            {
                case 0: return TeamScore0;
                case 1: return TeamScore1;
                case 2: return TeamScore2;
                case 3: return TeamScore3;
                default: return 0;
            }
        }
    }

    public struct PvPScoreboardEntry
    {
        public string PlayerName;
        public byte TeamId;
        public short Kills;
        public short Deaths;
        public short Assists;
        public float DamageDealt;
        public float HealingDone;
        public int MatchScore;
        public bool IsLocalPlayer;
    }

    public struct PvPKillFeedUIEntry
    {
        public string KillerName;
        public string VictimName;
        public byte KillerTeam;
        public byte VictimTeam;
        public bool IsLocalPlayerKiller;
        public bool IsLocalPlayerVictim;
    }

    public struct PvPMatchResultUI
    {
        public PvPGameMode GameMode;
        public byte WinningTeam;
        public bool LocalPlayerWon;
        public PvPScoreboardEntry[] FinalScoreboard;
        public int EloDelta;
        public PvPTier NewTier;
        public PvPTier OldTier;
        public bool TierChanged;
        public float BonusXP;
    }

    public struct PvPRankingUI
    {
        public int Elo;
        public PvPTier Tier;
        public int Wins;
        public int Losses;
        public int WinStreak;
        public int HighestElo;
        public float WinRate;
    }
}
