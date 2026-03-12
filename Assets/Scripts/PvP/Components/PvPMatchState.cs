using Unity.Entities;
using Unity.NetCode;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Singleton controlling the active PvP match.
    /// Ghost-replicated to all clients so every player sees match phase, timer, and scores.
    /// 32 bytes.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PvPMatchState : IComponentData
    {
        [GhostField] public PvPMatchPhase Phase;
        [GhostField] public PvPGameMode GameMode;
        [GhostField] public byte MapId;
        [GhostField] public byte OvertimeEnabled;
        [GhostField(Quantization = 100)] public float Timer;
        [GhostField] public float MatchDuration;
        [GhostField] public int MaxScore;
        [GhostField] public int TeamScore0;
        [GhostField] public int TeamScore1;
        [GhostField] public int TeamScore2;
        [GhostField] public int TeamScore3;

        public int GetTeamScore(int teamId)
        {
            switch (teamId)
            {
                case 0: return TeamScore0;
                case 1: return TeamScore1;
                case 2: return TeamScore2;
                case 3: return TeamScore3;
                default: return 0;
            }
        }

        public void AddTeamScore(int teamId, int points)
        {
            switch (teamId)
            {
                case 0: TeamScore0 += points; break;
                case 1: TeamScore1 += points; break;
                case 2: TeamScore2 += points; break;
                case 3: TeamScore3 += points; break;
            }
        }
    }
}
