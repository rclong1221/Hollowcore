# EPIC 4.6: Expedition Seed & Determinism

**Status**: Planning
**Epic**: EPIC 4 — District Graph & Expedition Structure
**Priority**: High — Reproducibility and fairness foundation
**Dependencies**: EPIC 4.1 (Graph Data Model), EPIC 4.4 (Zone Structure), Framework: Roguelite/ (RunSeedUtility)

---

## Overview

Every procedural element of an expedition derives from a single master seed via a deterministic hash chain. The framework's RunSeedUtility provides the `math.hash` chain primitive; Hollowcore extends it with a structured derivation tree that produces sub-seeds for district assignment, zone topology, encounter placement, loot tables, and seeded events. Same seed guarantees same expedition graph, same zone layouts, and same initial enemy/loot placements. Player actions (kills, deaths, loot taken, Front counterplay) create divergence. When re-entering a previously visited district, the zone layout regenerates identically from its seed, then the saved delta (EPIC 4.2) applies on top.

---

## Component Definitions

### ExpeditionSeedState (IComponentData — Singleton)

```csharp
// File: Assets/Scripts/Expedition/Components/SeedComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Singleton holding the master expedition seed and derived sub-seeds.
    /// Created at expedition start, lives on the ExpeditionGraphEntity.
    /// All procedural generation reads from this — never from System.Random or UnityEngine.Random.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct ExpeditionSeedState : IComponentData
    {
        /// <summary>Master seed for the entire expedition. Set by player input or daily challenge.</summary>
        public uint MasterSeed;

        /// <summary>Derived seed for graph topology (which districts, which edges).</summary>
        public uint GraphSeed;

        /// <summary>Derived seed for global loot table rolls.</summary>
        public uint LootSeed;

        /// <summary>Derived seed for global event placement.</summary>
        public uint EventSeed;

        /// <summary>Derived seed for encounter composition.</summary>
        public uint EncounterSeed;
    }
}
```

### DistrictSeedData (IBufferElementData)

```csharp
// File: Assets/Scripts/Expedition/Components/SeedComponents.cs (continued)
namespace Hollowcore.Expedition
{
    /// <summary>
    /// Per-district derived seeds. Stored as a buffer on ExpeditionGraphEntity,
    /// indexed by node index (parallel to GraphNodeState buffer).
    /// </summary>
    public struct DistrictSeedData : IBufferElementData
    {
        /// <summary>Node index this seed data corresponds to.</summary>
        public int NodeIndex;

        /// <summary>Master seed for this specific district (all district generation derives from this).</summary>
        public uint DistrictSeed;

        /// <summary>Zone layout seed (topology variant selection + zone placement).</summary>
        public uint ZoneLayoutSeed;

        /// <summary>Enemy spawn seed (which enemies, where, patrol routes).</summary>
        public uint SpawnSeed;

        /// <summary>Loot seed (container contents, drop tables within this district).</summary>
        public uint LootSeed;

        /// <summary>Event seed (merchants, vaults, rare spawns, echoes).</summary>
        public uint EventSeed;

        /// <summary>Front seed (initial spread pattern variation).</summary>
        public uint FrontSeed;
    }
}
```

### SeedDerivationUtility (Static Helper)

```csharp
// File: Assets/Scripts/Expedition/Utility/SeedDerivationUtility.cs
using Unity.Mathematics;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Extends framework's RunSeedUtility with Hollowcore-specific seed derivation.
    /// All derivation is pure functions — no state, no side effects.
    /// </summary>
    public static class SeedDerivationUtility
    {
        // --- Expedition-Level Derivation ---

        public static uint DeriveGraphSeed(uint masterSeed)
            => math.hash(new uint2(masterSeed, 0x47524150)); // "GRAP"

        public static uint DeriveLootSeed(uint masterSeed)
            => math.hash(new uint2(masterSeed, 0x4C4F4F54)); // "LOOT"

        public static uint DeriveEventSeed(uint masterSeed)
            => math.hash(new uint2(masterSeed, 0x45564E54)); // "EVNT"

        public static uint DeriveEncounterSeed(uint masterSeed)
            => math.hash(new uint2(masterSeed, 0x454E434F)); // "ENCO"

        // --- District-Level Derivation ---

        public static uint DeriveDistrictSeed(uint masterSeed, int nodeIndex)
            => math.hash(new uint2(masterSeed, (uint)(0x44495354 + nodeIndex))); // "DIST"+i

        public static uint DeriveZoneLayoutSeed(uint districtSeed)
            => math.hash(new uint2(districtSeed, 0x5A4F4E45)); // "ZONE"

        public static uint DeriveSpawnSeed(uint districtSeed)
            => math.hash(new uint2(districtSeed, 0x5350574E)); // "SPWN"

        public static uint DeriveDistrictLootSeed(uint districtSeed)
            => math.hash(new uint2(districtSeed, 0x444C4F54)); // "DLOT"

        public static uint DeriveDistrictEventSeed(uint districtSeed)
            => math.hash(new uint2(districtSeed, 0x44455654)); // "DEVT"

        public static uint DeriveFrontSeed(uint districtSeed)
            => math.hash(new uint2(districtSeed, 0x46524E54)); // "FRNT"

        // --- Zone-Level Derivation ---

        public static uint DeriveZoneId(uint districtSeed, int zoneIndex)
            => math.hash(new uint2(districtSeed, (uint)(0x5A494400 + zoneIndex))); // "ZID\0"+i

        public static uint DeriveZoneEncounterSeed(uint spawnSeed, int zoneIndex)
            => math.hash(new uint2(spawnSeed, (uint)(0x5A454E00 + zoneIndex))); // "ZEN\0"+i

        public static uint DeriveZoneLootSeed(uint lootSeed, int zoneIndex)
            => math.hash(new uint2(lootSeed, (uint)(0x5A4C5400 + zoneIndex))); // "ZLT\0"+i

        // --- Spawner-Level Derivation ---

        public static uint DeriveEnemySpawnId(uint zoneEncounterSeed, int spawnerIndex)
            => math.hash(new uint2(zoneEncounterSeed, (uint)spawnerIndex));
    }
}
```

---

## Systems

### SeedInitializationSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/SeedInitializationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
// UpdateBefore: ExpeditionGraphGenerationSystem
//
// Runs once at expedition start. Populates the seed hierarchy.
//
// 1. Read master seed from expedition start parameters:
//    - Player-entered seed (shared runs) OR
//    - Server-generated random seed (normal runs) OR
//    - Daily challenge seed (rotates daily)
//
// 2. Populate ExpeditionSeedState on ExpeditionGraphEntity:
//    MasterSeed = input
//    GraphSeed = DeriveGraphSeed(MasterSeed)
//    LootSeed = DeriveLootSeed(MasterSeed)
//    EventSeed = DeriveEventSeed(MasterSeed)
//    EncounterSeed = DeriveEncounterSeed(MasterSeed)
//
// 3. After ExpeditionGraphGenerationSystem creates nodes:
//    For each active node (0..N):
//      districtSeed = DeriveDistrictSeed(MasterSeed, nodeIndex)
//      Populate DistrictSeedData buffer element:
//        DistrictSeed = districtSeed
//        ZoneLayoutSeed = DeriveZoneLayoutSeed(districtSeed)
//        SpawnSeed = DeriveSpawnSeed(districtSeed)
//        LootSeed = DeriveDistrictLootSeed(districtSeed)
//        EventSeed = DeriveDistrictEventSeed(districtSeed)
//        FrontSeed = DeriveFrontSeed(districtSeed)
```

### SeededRandomProvider

```csharp
// File: Assets/Scripts/Expedition/Utility/SeededRandomProvider.cs
// NOT a system — utility struct for deterministic random number generation within systems.
//
// Usage pattern in any system that needs random values:
//
//   var rng = new Unity.Mathematics.Random(SeedDerivationUtility.DeriveZoneEncounterSeed(spawnSeed, zoneIndex));
//   int enemyType = rng.NextInt(0, enemyPool.Length);
//   float3 offset = rng.NextFloat3(-spread, spread);
//
// CRITICAL: Always derive a new Random from a deterministic seed.
// NEVER use a persistent Random across frames — prediction rollback will desync it.
// NEVER use UnityEngine.Random or System.Random for gameplay-affecting rolls.
```

---

## Setup Guide

1. **Add ExpeditionSeedState** to the ExpeditionGraphEntity (same entity as ExpeditionGraphState)
2. **Add DynamicBuffer\<DistrictSeedData\>** to the ExpeditionGraphEntity
3. **Create SeedDerivationUtility.cs** at `Assets/Scripts/Expedition/Utility/`
4. **Wire seed input** into the expedition start flow:
   - Normal run: server generates `(uint)UnityEngine.Random.Range(1, int.MaxValue)` at run creation
   - Shared run: player enters seed string, hash it: `math.hash(new uint4(...))`
   - Daily challenge: `math.hash(new uint2(yearDay, year))`
5. **Replace all `UnityEngine.Random` and `System.Random` calls** in procedural generation systems with `Unity.Mathematics.Random` seeded from SeedDerivationUtility
6. **Verify determinism** by running the same seed twice and comparing:
   - Graph topology (district assignments, edge states)
   - Zone layouts (zone count, connectivity, types)
   - Enemy spawn positions
   - Loot container contents

---

## Verification

- [ ] Same MasterSeed produces identical ExpeditionSeedState derived seeds
- [ ] Same GraphSeed produces identical district assignments and edge topology
- [ ] Same DistrictSeed produces identical zone layouts (count, types, connectivity)
- [ ] Same SpawnSeed produces identical enemy placements per zone
- [ ] Same LootSeed produces identical container contents
- [ ] Same EventSeed produces identical merchant/vault/rare spawn placements
- [ ] Different MasterSeeds produce meaningfully different expeditions
- [ ] Re-entering a district regenerates identical geometry from stored DistrictSeed
- [ ] Player actions (kills, loot) do NOT affect seed-derived generation on revisit (delta applies on top)
- [ ] No UnityEngine.Random or System.Random used in any procedural generation system
- [ ] Seed sharing: two players with same seed get same graph and initial placements
- [ ] Daily challenge seed rotates correctly and is consistent across clients
- [ ] DistrictSeedData buffer is parallel to GraphNodeState buffer (same indices)
- [ ] Burst-compatible: all SeedDerivationUtility methods are static, no managed types

---

## Validation

```csharp
// File: Assets/Scripts/Expedition/Utility/SeedDerivationUtility.cs (validation)
namespace Hollowcore.Expedition
{
    public static partial class SeedDerivationUtility
    {
#if UNITY_EDITOR
        /// <summary>
        /// Editor-only validation: verify that derived seeds do not collide for a sample of master seeds.
        /// Called by build-time validator.
        /// </summary>
        public static bool ValidateSeedUniqueness(uint masterSeed, out string report)
        {
            var seeds = new HashSet<uint>();
            seeds.Add(DeriveGraphSeed(masterSeed));
            seeds.Add(DeriveLootSeed(masterSeed));
            seeds.Add(DeriveEventSeed(masterSeed));
            seeds.Add(DeriveEncounterSeed(masterSeed));

            // Per-district seeds (8 districts max)
            for (int i = 0; i < 8; i++)
            {
                uint ds = DeriveDistrictSeed(masterSeed, i);
                if (!seeds.Add(ds)) { report = $"Collision at district {i}"; return false; }
                if (!seeds.Add(DeriveZoneLayoutSeed(ds))) { report = $"Zone layout collision at district {i}"; return false; }
                if (!seeds.Add(DeriveSpawnSeed(ds))) { report = $"Spawn collision at district {i}"; return false; }
                if (!seeds.Add(DeriveDistrictLootSeed(ds))) { report = $"Loot collision at district {i}"; return false; }
                if (!seeds.Add(DeriveDistrictEventSeed(ds))) { report = $"Event collision at district {i}"; return false; }
                if (!seeds.Add(DeriveFrontSeed(ds))) { report = $"Front collision at district {i}"; return false; }
            }
            report = $"All {seeds.Count} derived seeds unique for master seed {masterSeed}";
            return true;
        }
#endif
    }
}
```

```csharp
// File: Assets/Editor/Validation/SeedDeterminismValidator.cs
#if UNITY_EDITOR
namespace Hollowcore.Editor.Validation
{
    public static class SeedDeterminismValidator
    {
        [UnityEditor.MenuItem("Hollowcore/Validation/Verify Seed Determinism (1000 seeds)")]
        public static void Verify()
        {
            int collisions = 0;
            for (uint seed = 1; seed <= 1000; seed++)
            {
                if (!SeedDerivationUtility.ValidateSeedUniqueness(seed, out string report))
                {
                    Debug.LogError($"Seed {seed}: {report}");
                    collisions++;
                }
            }
            Debug.Log($"Seed determinism check: {1000 - collisions}/1000 passed, {collisions} collisions");
        }
    }
}
#endif
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/ExpeditionWorkstation/SeedExplorerModule.cs
// IExpeditionWorkstationModule — "Seed Explorer" tab.
//
// The primary tool for understanding seed-driven generation:
//
// Input panel:
//   - Seed field: uint text input OR string (auto-hashed)
//   - "Random Seed" button
//   - ExpeditionGraphSO template selector
//
// Output panel:
//   - Full seed derivation tree: expandable tree view
//     MasterSeed → GraphSeed, LootSeed, EventSeed, EncounterSeed
//     Per-district: DistrictSeed → ZoneLayoutSeed, SpawnSeed, LootSeed, EventSeed, FrontSeed
//   - Each seed shown as hex value
//   - Click any seed: copies to clipboard
//
// Graph preview:
//   - Same node-graph canvas as GraphEditorModule but shows GENERATED result
//   - Active nodes labeled with selected DistrictDefinitionSO
//   - Edge pruning visualized (removed edges shown faded)
//
// "Compare Seeds" mode:
//   - Enter two seeds side by side
//   - Highlights differences: which districts differ, which edges differ
//   - Useful for daily challenge verification
//
// "Seed Sharing" preview:
//   - Shows what a shared seed reproduces vs what diverges
//   - Green: deterministic (same for all players), Yellow: player-dependent (kills, loot)
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Expedition/Debug/SeedDebugOverlay.cs
// In-game debug overlay:
//
//   - Display: "Expedition Seed: 0xABCD1234" (top-right, small text)
//   - Display: "District Seed: 0x5678EFAB" for current district
//   - Per-zone on minimap: zone ID shown as hex (hover for full seed chain)
//   - "Copy Seed" button in debug menu
//   - Toggle: F11 key
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/ExpeditionWorkstation/SeedDeterminismTest.cs
// IExpeditionWorkstationModule — "Determinism" tab.
//
// "Determinism Verification" button:
//   For 100 random seeds:
//     1. Run full graph generation twice with same seed
//     2. Compare: node count, node district assignments, edge states, zone layouts
//     3. Any mismatch = FAIL (determinism broken)
//   Report: pass/fail count, first failure details
//
// "Seed Distribution" analysis:
//   For 10000 seeds:
//     - District selection distribution: how often each DistrictDefinitionSO appears per slot
//     - Edge pruning distribution: % of time each edge survives
//     - Node count distribution: histogram of active node counts
//   Detects: bias in hash function, over/under-pruning, stuck distributions
//
// "Daily Challenge Seed" preview:
//   Enter date → shows derived seed + full expedition preview
//   Calendar view: next 7 days' seeds pre-computed
```
