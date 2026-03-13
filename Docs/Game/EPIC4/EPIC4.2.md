# EPIC 4.2: District Persistence

**Status**: Planning
**Epic**: EPIC 4 — District Graph & Expedition Structure
**Priority**: Critical — Districts must remember everything across transitions
**Dependencies**: EPIC 4.1 (Graph Data Model), Framework: Persistence/ (ISaveModule), Roguelite/ (RunSeedUtility)

---

## Overview

Every district in the expedition graph persists its full state when the player leaves. Dead enemies, collected loot, echo missions, Front progress, merchant inventories, and quest completions all survive across district transitions. Persistence is lightweight: zone geometry regenerates deterministically from the seed, so only the delta (events at zone coordinates) needs serialization. A dedicated DistrictPersistenceModule implements the framework's ISaveModule interface with TypeId=22.

---

## Component Definitions

### DistrictSaveState

```csharp
// File: Assets/Scripts/Expedition/Persistence/DistrictSaveState.cs
using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Hollowcore.Expedition.Persistence
{
    /// <summary>
    /// Full serializable snapshot of a single district's runtime state.
    /// NOT an ECS component — managed data stored in the DistrictPersistenceModule.
    /// Geometry regenerates from seed; this stores only events and deltas.
    /// </summary>
    [Serializable]
    public class DistrictSaveState
    {
        /// <summary>DistrictDefinitionSO.DistrictId for validation on load.</summary>
        public int DistrictDefinitionId;

        /// <summary>Seed used to generate this district's zone layout.</summary>
        public uint DistrictSeed;

        // --- Front State ---
        public byte FrontPhase;
        public float FrontSpreadProgress;
        public float FrontAdvanceRate;
        public int FrontBleedCounter;

        // --- Per-Zone Delta ---
        public List<ZoneDeltaState> ZoneDeltas = new();

        // --- Entity Events ---
        public List<KillRecord> EnemyKills = new();
        public List<BodyRecord> PlayerBodies = new();
        public List<EchoRecord> ActiveEchoes = new();
        public List<SeededEventRecord> SeededEvents = new();
        public List<LootRecord> CollectedLoot = new();

        // --- Quest/Objective ---
        public byte CompletionMask;
        public List<QuestStateRecord> QuestStates = new();

        // --- Visit Metadata ---
        public short VisitCount;
        public float TimeSpentSeconds;
        public short DeathCount;
    }

    [Serializable]
    public struct ZoneDeltaState
    {
        /// <summary>Deterministic zone ID from seed.</summary>
        public int ZoneId;

        /// <summary>Front zone state: 0=Safe, 1=Contested, 2=Hostile, 3=Overrun.</summary>
        public byte FrontZoneState;

        /// <summary>Number of enemies killed in this zone.</summary>
        public int KillCount;

        /// <summary>Number of enemies still alive (for respawn suppression).</summary>
        public int AliveCount;
    }

    [Serializable]
    public struct KillRecord
    {
        public int ZoneId;
        public int EnemySpawnId;  // Deterministic from seed — identifies which spawner
        public float KillTime;    // Seconds since district entry
    }

    [Serializable]
    public struct BodyRecord
    {
        public int ZoneId;
        public float3Surrogate Position;  // Zone-relative coordinates
        public int InventorySnapshotId;   // Index into separate inventory serialization
        public float DeathTime;
    }

    [Serializable]
    public struct EchoRecord
    {
        public int EchoMissionId;
        public int ZoneId;
        public byte EchoPhase;    // 0=undiscovered, 1=active, 2=complete
    }

    [Serializable]
    public struct SeededEventRecord
    {
        public int EventId;
        public int ZoneId;
        public byte EventType;    // 0=merchant, 1=vault, 2=rare_spawn, 3=npc
        public bool Consumed;     // True if player has interacted/looted
    }

    [Serializable]
    public struct LootRecord
    {
        public int ZoneId;
        public int LootSpawnId;   // Deterministic from seed
    }

    [Serializable]
    public struct QuestStateRecord
    {
        public int QuestId;
        public byte Phase;        // Quest-specific progress
        public bool Complete;
    }

    /// <summary>Serializable float3 surrogate (Unity.Mathematics.float3 is not Serializable).</summary>
    [Serializable]
    public struct float3Surrogate
    {
        public float x, y, z;
    }
}
```

### DistrictSaveTag (IComponentData)

```csharp
// File: Assets/Scripts/Expedition/Components/DistrictPersistenceComponents.cs
using Unity.Entities;

namespace Hollowcore.Expedition
{
    /// <summary>
    /// Marks the active district entity as needing a save snapshot before unload.
    /// Enabled by DistrictTransitionSystem, consumed by DistrictSaveSystem.
    /// </summary>
    public struct DistrictSaveTag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Marks a district entity whose saved state has been loaded and needs delta application.
    /// Enabled by DistrictLoadSystem after regeneration, consumed by DistrictDeltaApplySystem.
    /// </summary>
    public struct DistrictDeltaApplyTag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Singleton that tracks which districts have saved state available.
    /// </summary>
    public struct DistrictPersistenceRegistry : IComponentData
    {
        /// <summary>Bitmask of district node indices that have persisted state (max 8 districts).</summary>
        public byte HasSavedStateMask;
    }
}
```

---

## Systems

### DistrictSaveSystem

```csharp
// File: Assets/Scripts/Expedition/Systems/DistrictSaveSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: DistrictLoadSystem
//
// Triggered when DistrictSaveTag is enabled on the active district entity.
//
// 1. CompleteDependency() — ensure all async systems have flushed
// 2. Read current district entity's FrontState → populate DistrictSaveState front fields
// 3. Query all enemies in current district scene:
//    - Dead (DeathState.IsDead): write KillRecord (ZoneId from zone assignment, EnemySpawnId)
//    - Alive: write ZoneDeltaState.AliveCount per zone
// 4. Query all player body entities: write BodyRecord with zone-relative position
// 5. Query active echo missions: write EchoRecord
// 6. Query seeded events (merchants, vaults, rare spawns): write SeededEventRecord
// 7. Query collected loot markers: write LootRecord
// 8. Read quest state from QuestTracker → write QuestStateRecord list
// 9. Copy visit metadata from GraphNodeState
// 10. Pass DistrictSaveState to DistrictPersistenceModule.SaveDistrict(nodeIndex, state)
// 11. Disable DistrictSaveTag
// 12. Set bit in DistrictPersistenceRegistry.HasSavedStateMask
```

### DistrictDeltaApplySystem

```csharp
// File: Assets/Scripts/Expedition/Systems/DistrictDeltaApplySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Triggered when DistrictDeltaApplyTag is enabled (after zone regeneration for a revisited district).
//
// 1. Read DistrictSaveState for the current node from DistrictPersistenceModule
// 2. Apply Front state: set FrontState phase, spread, rate from saved values
// 3. Apply per-zone deltas:
//    - Set FrontZoneState on each zone entity
//    - For each KillRecord: find matching spawner by EnemySpawnId, mark as killed (suppress respawn)
//    - For each ZoneDeltaState: validate alive counts
// 4. Restore player bodies: spawn body entities at saved positions with inventory snapshots
// 5. Restore echo missions: create/update echo entities from EchoRecord
// 6. Restore seeded events: mark consumed events as inactive, restore active ones
// 7. Suppress collected loot: for each LootRecord, find matching loot spawner, mark as collected
// 8. Restore quest state: update QuestTracker from QuestStateRecord list
// 9. Disable DistrictDeltaApplyTag
```

### DistrictPersistenceModule (ISaveModule)

```csharp
// File: Assets/Scripts/Expedition/Persistence/DistrictPersistenceModule.cs
// Implements framework ISaveModule with TypeId = 22
//
// Managed class — holds Dictionary<int, DistrictSaveState> keyed by node index.
//
// ISaveModule.TypeId => 22
// ISaveModule.Serialize(BinaryWriter):
//   1. Write district count
//   2. For each saved district: write nodeIndex, then DistrictSaveState fields
//      - Front fields (5 values)
//      - ZoneDeltas list (count + per-entry)
//      - KillRecords, BodyRecords, EchoRecords, SeededEventRecords, LootRecords, QuestStateRecords
//      - Visit metadata (3 values)
//
// ISaveModule.Deserialize(BinaryReader):
//   1. Read district count
//   2. For each: read nodeIndex + DistrictSaveState, insert into dictionary
//
// Public API:
//   void SaveDistrict(int nodeIndex, DistrictSaveState state)
//   DistrictSaveState LoadDistrict(int nodeIndex)  // returns null if not saved
//   bool HasSavedState(int nodeIndex)
//   void ClearAll()  // on expedition end
```

---

## Setup Guide

1. **Create `Assets/Scripts/Expedition/Persistence/` folder**
2. **Register DistrictPersistenceModule** with the framework's SaveManager (add to module list, TypeId=22)
3. **Add DistrictSaveTag + DistrictDeltaApplyTag** as baked-disabled IEnableableComponents on the district entity prefab
4. **Add DistrictPersistenceRegistry** singleton to a persistent subscene entity
5. **Wire DistrictSaveSystem** to fire before scene unload in the transition flow (EPIC 4.3)
6. **Wire DistrictDeltaApplySystem** to fire after zone generation completes for revisited districts
7. **Verify determinism**: enter a district, collect loot, kill enemies, transition out, transition back — killed enemies should not respawn, loot should be gone, Front should be at saved phase

---

## Verification

- [ ] DistrictPersistenceModule registers with TypeId=22 in SaveManager
- [ ] DistrictSaveSystem captures all kill records when leaving a district
- [ ] DistrictSaveSystem captures collected loot IDs
- [ ] DistrictSaveSystem captures Front phase and spread progress
- [ ] DistrictSaveSystem captures active echo missions and quest states
- [ ] DistrictDeltaApplySystem suppresses killed enemy respawns on revisit
- [ ] DistrictDeltaApplySystem suppresses collected loot on revisit
- [ ] DistrictDeltaApplySystem restores Front to saved phase
- [ ] DistrictDeltaApplySystem restores player bodies at correct positions
- [ ] Zone geometry regenerates identically from same seed (visual layout unchanged)
- [ ] Serialize/Deserialize round-trips correctly (save to file, load from file, state matches)
- [ ] DistrictPersistenceRegistry bitmask accurately tracks which nodes have saved state
- [ ] ClearAll() wipes all district states on expedition end

---

## Validation

```csharp
// File: Assets/Scripts/Expedition/Persistence/DistrictSaveState.cs (OnValidate-equivalent)
// DistrictSaveState is NOT a ScriptableObject — validation runs at serialization time.
namespace Hollowcore.Expedition.Persistence
{
    public partial class DistrictSaveState
    {
        /// <summary>
        /// Validates state integrity before serialization or after deserialization.
        /// Called by DistrictPersistenceModule.SaveDistrict() and LoadDistrict().
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;
            if (DistrictDefinitionId <= 0) { error = "Invalid DistrictDefinitionId"; return false; }
            if (DistrictSeed == 0) { error = "DistrictSeed is zero"; return false; }
            if (FrontPhase > 4) { error = $"FrontPhase {FrontPhase} out of range [0..4]"; return false; }
            if (FrontSpreadProgress < 0f) { error = "Negative FrontSpreadProgress"; return false; }

            // ZoneDeltas: no duplicate ZoneIds
            var zoneIds = new HashSet<int>();
            foreach (var zd in ZoneDeltas)
                if (!zoneIds.Add(zd.ZoneId)) { error = $"Duplicate ZoneId {zd.ZoneId} in ZoneDeltas"; return false; }

            // KillRecords: EnemySpawnId > 0
            foreach (var kr in EnemyKills)
                if (kr.EnemySpawnId <= 0) { error = $"Invalid EnemySpawnId in KillRecord"; return false; }

            // LootRecords: no duplicate LootSpawnIds within same zone
            var lootSet = new HashSet<(int, int)>();
            foreach (var lr in CollectedLoot)
                if (!lootSet.Add((lr.ZoneId, lr.LootSpawnId)))
                    { error = $"Duplicate LootSpawnId {lr.LootSpawnId} in zone {lr.ZoneId}"; return false; }

            return true;
        }
    }
}
```

```csharp
// File: Assets/Editor/Validation/DistrictPersistenceValidator.cs
// Editor utility: validates all .dig save files in a directory
#if UNITY_EDITOR
namespace Hollowcore.Editor.Validation
{
    public static class DistrictPersistenceValidator
    {
        [UnityEditor.MenuItem("Hollowcore/Validation/Validate Save Files")]
        public static void ValidateAllSaveFiles()
        {
            // Scan Application.persistentDataPath for .dig files
            // Deserialize each via DistrictPersistenceModule.Deserialize()
            // Run DistrictSaveState.Validate() on each
            // Report errors to console with file path context
        }
    }
}
#endif
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/ExpeditionWorkstation/DistrictPersistenceInspector.cs
// Custom inspector panel in the Expedition Workstation for debugging persistence.
//
// Features:
//   - Live view of DistrictPersistenceModule dictionary contents during play mode
//   - Per-district expandable: node index, kill count, loot count, echo count, Front phase
//   - "Dump to JSON" button for debugging save state
//   - "Clear District" button to wipe a single district's saved state
//   - "Force Save Current" button to trigger DistrictSaveTag manually
//   - Bitmask visualizer for DistrictPersistenceRegistry.HasSavedStateMask (8 toggles)
//   - Byte size estimate per district (helps profile save file size)
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Expedition/Debug/DistrictPersistenceDebugOverlay.cs
// Debug HUD overlay showing persistence state for the current district:
//
//   - Kill markers: red X at each KillRecord position (screen-space projected)
//   - Loot markers: yellow dot at collected loot positions
//   - Body markers: blue skull at PlayerBody positions
//   - Echo markers: purple spiral at active echo zone centers
//   - Event markers: green diamond at seeded event positions (merchant/vault/etc.)
//   - Toggle: F9 key or debug menu
//   - Text overlay: "Kills: 47 | Loot: 12 | Bodies: 2 | Echoes: 3 | Events: 5"
//   - When transitioning OUT: briefly flashes "SAVING..." with byte count estimate
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/ExpeditionWorkstation/PersistenceSimulationModule.cs
// IExpeditionWorkstationModule — "Persistence" tab.
//
// "Round-Trip Test" button:
//   1. Generate a district from seed
//   2. Simulate N kills, M loot collections, K quest completions (randomized)
//   3. Serialize to DistrictSaveState
//   4. Serialize to binary via ISaveModule.Serialize()
//   5. Deserialize from binary
//   6. Compare original vs deserialized field-by-field
//   7. Report: pass/fail, byte size, serialization time
//
// "Stress Test" button:
//   Generate 100 districts with max-size save states (15 zones, 200 kills, 50 loot, 8 echoes)
//   Serialize/deserialize all, report total byte size and time.
//   Validates no data loss at scale.
//
// "Delta Apply Fidelity" test:
//   Generate district → simulate gameplay → save → regenerate from seed → apply delta
//   Verify: killed enemies absent, loot absent, Front phase matches, echoes present
```
