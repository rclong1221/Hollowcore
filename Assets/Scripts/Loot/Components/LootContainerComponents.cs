using Unity.Entities;

namespace DIG.Loot.Components
{
    /// <summary>
    /// EPIC 16.6: Type of loot container in the world.
    /// </summary>
    public enum ContainerType : byte
    {
        Chest = 0,
        Crate = 1,
        Barrel = 2,
        BossChest = 3
    }

    /// <summary>
    /// EPIC 16.6: Lifecycle phase of a loot container.
    /// </summary>
    public enum LootContainerPhase : byte
    {
        Sealed = 0,
        Opening = 1,
        Open = 2,
        Looted = 3,
        Destroyed = 4
    }

    /// <summary>
    /// EPIC 16.6: State tracking for loot containers (chests, crates, barrels).
    /// </summary>
    public struct LootContainerState : IComponentData
    {
        public ContainerType Type;
        public LootContainerPhase Phase;
        public int LootTableId;
        public float OpenDuration;
        public bool IsReusable;
        public float RespawnTime;
        public float LastOpenedTime;
        public bool RequiresKey;
        public int RequiredKeyItemId;
    }
}
