using Unity.Burst;
using Unity.Mathematics;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Deterministic seed derivation. Burst-compatible, stateless.
    /// Identical seeds produce identical runs regardless of system execution order.
    /// </summary>
    [BurstCompile]
    public static class RunSeedUtility
    {
        [BurstCompile]
        public static uint DeriveZoneSeed(uint masterSeed, byte zoneIndex)
            => math.hash(new uint2(masterSeed, zoneIndex));

        [BurstCompile]
        public static uint DeriveEncounterSeed(uint zoneSeed, int encounterIndex)
            => math.hash(new uint2(zoneSeed, (uint)encounterIndex));

        [BurstCompile]
        public static uint DeriveRewardSeed(uint zoneSeed, int rewardIndex)
            => math.hash(new uint2(zoneSeed, (uint)(rewardIndex + 10000)));

        [BurstCompile]
        public static uint DeriveShopSeed(uint zoneSeed)
            => math.hash(new uint2(zoneSeed, 20000u));

        [BurstCompile]
        public static uint DeriveEventSeed(uint zoneSeed)
            => math.hash(new uint2(zoneSeed, 30000u));

        /// <summary>EPIC 23.4: Seed for deterministic modifier choice selection.</summary>
        [BurstCompile]
        public static uint DeriveModifierSeed(uint zoneSeed)
            => math.hash(new uint2(zoneSeed, 40000u));

        /// <summary>EPIC 23.3: Seed for spawn director per-spawn determinism.</summary>
        [BurstCompile]
        public static uint DeriveSpawnSeed(uint zoneSeed, int spawnIndex)
            => math.hash(new uint2(zoneSeed, (uint)(spawnIndex + 50000)));

        /// <summary>EPIC 23.3: Seed for interactable placement.</summary>
        [BurstCompile]
        public static uint DeriveInteractableSeed(uint zoneSeed)
            => math.hash(new uint2(zoneSeed, 60000u));
    }
}
