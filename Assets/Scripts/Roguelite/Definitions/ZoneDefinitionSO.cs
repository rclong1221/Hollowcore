using UnityEngine;

namespace DIG.Roguelite.Zones
{
    public enum ZoneType : byte
    {
        Combat = 0,
        Elite = 1,
        Boss = 2,
        Shop = 3,
        Event = 4,
        Rest = 5,
        Treasure = 6,
        Exploration = 7,
        Arena = 8,
        Secret = 9
    }

    public enum ZoneClearMode : byte
    {
        AllEnemiesDead = 0,
        PlayerTriggered = 1,
        TimerSurvival = 2,
        BossKill = 3,
        TriggerThenBoss = 4,
        Objective = 5,
        Manual = 6
    }

    public enum ZoneSizeHint : byte
    {
        Tiny = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        Massive = 4
    }

    /// <summary>
    /// Designer-authored zone definition. Configures a zone's encounters, rewards,
    /// interactables, clear condition, and spawn director behaviour.
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneDefinition", menuName = "DIG/Roguelite/Zone Definition", order = 10)]
    public class ZoneDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int ZoneId;
        public string DisplayName;
        public ZoneType Type;
        public Sprite Icon;
        [TextArea(2, 4)]
        public string Description;

        [Header("Difficulty")]
        [Tooltip("Multiplier applied on top of the run's base difficulty for this zone.")]
        public float DifficultyMultiplier = 1f;
        [Tooltip("Earliest zone index where this can appear. 0 = any.")]
        public int MinZoneIndex;
        [Tooltip("Latest zone index where this can appear. 0 = any.")]
        public int MaxZoneIndex;

        [Header("Encounters")]
        [Tooltip("Pool of enemies for this zone. Null = no framework-spawned enemies.")]
        public EncounterPoolSO EncounterPool;
        [Tooltip("How the spawn director operates in this zone.")]
        public SpawnDirectorConfigSO SpawnDirectorConfig;

        [Header("Rewards")]
        [Tooltip("Reward choices on zone clear (RewardPoolSO). Null = no framework reward screen.")]
        public ScriptableObject ClearRewardPool;
        [Tooltip("Loot table for bonus drops (LootTableSO). Uses existing loot pipeline.")]
        public ScriptableObject BonusLootTable;

        [Header("Interactables")]
        [Tooltip("How many interactable nodes to populate. 0 = none / game handles it.")]
        public int InteractableBudget;
        [Tooltip("Interactable pool (chests, shrines, etc). Null = game handles placement.")]
        public InteractablePoolSO InteractablePool;

        [Header("Clear Condition")]
        [Tooltip("How this zone is considered cleared.")]
        public ZoneClearMode ClearMode = ZoneClearMode.AllEnemiesDead;
        [Tooltip("Timer duration for TimerSurvival clear mode, in seconds.")]
        public float SurvivalTimer;

        [Header("Environment")]
        [Tooltip("Size hint for spawn density and interactable count scaling.")]
        public ZoneSizeHint SizeHint = ZoneSizeHint.Medium;
        [Tooltip("Environmental hazard profile. Null = no framework-managed hazards.")]
        public ScriptableObject HazardProfile;

        [Header("Extension")]
        [Tooltip("Game-specific data. Cast to your own SO types in IZoneProvider.")]
        public ScriptableObject[] ExtensionData;
    }
}
