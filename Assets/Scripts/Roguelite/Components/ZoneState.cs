using Unity.Entities;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Runtime state for the current zone. Stored on the RunState entity.
    /// Read by SpawnDirectorSystem, ZoneClearDetectionSystem, ZoneUIBridgeSystem.
    /// </summary>
    public struct ZoneState : IComponentData
    {
        public int ZoneIndex;
        public int ZoneId;
        public ZoneType Type;
        public ZoneClearMode ClearMode;
        public float TimeInZone;
        public float EffectiveDifficulty;
        public int EnemiesSpawned;
        public int EnemiesAlive;
        public int EnemiesKilled;
        public float SpawnBudget;
        public float SpawnTimer;
        public bool BossSpawned;
        public bool ExitActivated;
        public byte LoopCount;
        public bool IsCleared;
    }

    /// <summary>
    /// Enableable component on RunState entity. Game code enables this
    /// when the player activates the zone exit (teleporter, door, etc).
    /// Read by PlayerTriggeredCondition and TriggerThenBossCondition.
    /// </summary>
    public struct ZoneExitActivated : IComponentData, IEnableableComponent { }
}
