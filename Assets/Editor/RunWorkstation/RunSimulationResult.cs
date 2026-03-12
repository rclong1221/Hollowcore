#if UNITY_EDITOR
using System.Collections.Generic;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.6: Data classes for run simulation output.
    /// Used by RunSimulator (dry-run) and RunMonteCarloSimulator (statistical).
    /// </summary>
    public class RunSimulationResult
    {
        public uint Seed;
        public int ZoneCount;
        public List<SimulatedZone> Zones = new();
        public int FinalScore;
        public int TotalCurrencyEarned;
        public int TotalCurrencySpent;
        public List<int> RewardIdsChosen = new();
        public List<int> ModifierIdsAcquired = new();
    }

    public class SimulatedZone
    {
        public int ZoneIndex;
        public byte ZoneType;
        public string ZoneTypeName;
        public string ZoneDisplayName;
        public string ClearModeName;
        public float EffectiveDifficulty;
        public int EnemyCount;
        public List<string> EnemyNames = new();
        public List<string> RewardOptions = new();
        public int ShopItemCount;
        public int SpawnBudget;
        public int InteractableBudget;
    }

    public class MonteCarloResult
    {
        public int RunCount;
        public float AverageScore;
        public float AverageCurrencyEarned;
        public float AverageZonesCleared;
        public Dictionary<int, int> RewardFrequency = new();
        public Dictionary<int, int> ModifierFrequency = new();
        public float[] DifficultyPerZone;
        public float[] CurrencyPerZone;
        public float EstimatedRunsToFullUnlock;
    }
}
#endif
