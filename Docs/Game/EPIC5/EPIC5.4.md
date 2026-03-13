# EPIC 5.4: Cross-Expedition Echo Persistence

**Status**: Planning
**Epic**: EPIC 5 — Echo Missions
**Dependencies**: EPIC 5.1; Framework: Persistence/ (ISaveModule)

---

## Overview

Echoes never resolved across an entire expedition persist into future expeditions visiting the same district. Each expedition they persist, they grow harder and more rewarding. After 3+ expeditions, they become Legendary Echoes — boss-tier encounters with unique rewards that become community-shared legends.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Echo/Components/EchoPersistenceComponents.cs
using Unity.Collections;

namespace Hollowcore.Echo
{
    /// <summary>
    /// Persistent echo record saved across expeditions.
    /// NOT an ECS component — this is save data.
    /// </summary>
    [System.Serializable]
    public struct PersistentEchoRecord
    {
        public int EchoId;
        public int SourceQuestId;
        public int DistrictId;
        public int ZoneId;
        public int ExpeditionsPersisted;
        public EchoMutationType MutationType;
        public bool IsLegendary;  // true when ExpeditionsPersisted >= 3
    }

    public enum EchoLegendaryTier : byte
    {
        Normal = 0,          // 0 expeditions persisted (same run)
        Persistent = 1,      // 1-2 expeditions
        Legendary = 2,       // 3-4 expeditions
        Mythic = 3           // 5+ expeditions
    }
}
```

---

## Persistence Module

```csharp
// File: Assets/Scripts/Echo/Persistence/EchoPersistenceSaveModule.cs
// ISaveModule TypeId: 23
//
// Serializes all unresolved echoes across expeditions.
//
// On expedition end (victory or wipe):
//   For each district with HasActiveEchoes:
//     For each EchoMissionEntry where !IsCompleted:
//       1. Increment ExpeditionsPersisted
//       2. If ExpeditionsPersisted >= 3: mark IsLegendary
//       3. Save as PersistentEchoRecord
//
// On new expedition start:
//   For each PersistentEchoRecord:
//     If expedition visits the same district:
//       Inject as pre-existing echo with boosted difficulty/rewards
```

---

## Legendary Echo Scaling

| Tier | Expeditions | Difficulty Mult | Reward Mult | Special |
|---|---|---|---|---|
| Normal | 0 | 1.5x | 2.0x | Standard echo |
| Persistent | 1-2 | 2.0-2.5x | 3.0-3.5x | Enhanced rewards |
| Legendary | 3-4 | 3.0-3.5x | 5.0x | Boss-tier encounter, unique drops |
| Mythic | 5+ | 4.0x | 7.0x | One-of-a-kind, community legend |

---

## Setup Guide

1. Register EchoPersistenceSaveModule (TypeId=23) with SaveModuleRegistry
2. Persistent echo records stored in player profile save (not expedition save)
3. On expedition generation: query persistent records → inject matching district echoes
4. Legendary echoes get unique Scar Map markers (distinct from normal echo spirals)
5. Gate Screen shows legendary echo indicators on backtrack gates

---

## Verification

- [ ] Unresolved echoes increment ExpeditionsPersisted on expedition end
- [ ] New expedition correctly loads persistent echoes for matching districts
- [ ] Difficulty and rewards scale per tier table
- [ ] Legendary threshold at 3 expeditions
- [ ] Legendary echoes have distinct visual markers
- [ ] Completing a persistent echo removes it from save data

---

## Validation

```csharp
// File: Assets/Scripts/Echo/Persistence/EchoPersistenceSaveModule.cs (validation)
namespace Hollowcore.Echo.Persistence
{
    public partial class EchoPersistenceSaveModule
    {
        /// <summary>
        /// Validates persistent echo records after deserialization.
        /// </summary>
        public bool ValidateRecords(out string error)
        {
            error = null;
            var echoIds = new HashSet<int>();
            foreach (var record in _records)
            {
                if (!echoIds.Add(record.EchoId))
                    { error = $"Duplicate EchoId {record.EchoId}"; return false; }
                if (record.DistrictId <= 0)
                    { error = $"Invalid DistrictId in echo {record.EchoId}"; return false; }
                if (record.ExpeditionsPersisted < 0)
                    { error = $"Negative ExpeditionsPersisted in echo {record.EchoId}"; return false; }
                if (record.IsLegendary && record.ExpeditionsPersisted < 3)
                    { error = $"Echo {record.EchoId} marked Legendary but only persisted {record.ExpeditionsPersisted}x"; return false; }
            }
            return true;
        }
    }
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/EchoWorkstation/EchoPersistenceInspector.cs
// Debug inspector for cross-expedition echo persistence:
//
// Features:
//   - Live view of all PersistentEchoRecords in current save profile
//   - Table: EchoId, District, Zone, MutationType, ExpeditionsPersisted, IsLegendary, Tier
//   - Sort/filter by: district, tier, mutation type
//   - "Promote to Legendary" button: force-set ExpeditionsPersisted to 3 (debug)
//   - "Clear All Persistent Echoes" button: wipe save data
//   - "Inject Test Echo" button: create a synthetic persistent echo for testing
//   - Tier distribution summary: "Normal: 5, Persistent: 3, Legendary: 1, Mythic: 0"
//   - Timeline: visual history of echo persistence across expeditions (if save data tracks it)
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Echo/Components/EchoPersistenceRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Echo
{
    /// <summary>
    /// Runtime-tunable persistence scaling parameters.
    /// </summary>
    public struct EchoPersistenceRuntimeConfig : IComponentData
    {
        /// <summary>Expeditions required for Legendary tier. Default 3.</summary>
        public int LegendaryThreshold;

        /// <summary>Expeditions required for Mythic tier. Default 5.</summary>
        public int MythicThreshold;

        /// <summary>Difficulty multiplier cap (prevents impossible echoes). Default 4.0.</summary>
        public float MaxDifficultyMultiplier;

        /// <summary>Reward multiplier cap. Default 7.0.</summary>
        public float MaxRewardMultiplier;

        /// <summary>If true, persistent echoes can appear in districts they didn't originate from (migration). Default false.</summary>
        public bool AllowEchoMigration;

        public static EchoPersistenceRuntimeConfig Default => new()
        {
            LegendaryThreshold = 3,
            MythicThreshold = 5,
            MaxDifficultyMultiplier = 4.0f,
            MaxRewardMultiplier = 7.0f,
            AllowEchoMigration = false,
        };
    }
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/EchoWorkstation/EchoPersistenceSimulator.cs
// IExpeditionWorkstationModule — "Persistence Simulation" tab.
//
// "Echo Persistence Tier Distribution" (100 expedition simulations):
//   Input: side quests per district, completion rate, districts per expedition
//   Simulate 100 sequential expeditions:
//     - Each expedition visits 5-7 districts
//     - Uncompleted side quests become echoes
//     - Echoes that persist get ExpeditionsPersisted++
//     - Completed echoes removed
//   Output per expedition N:
//     - Active echo count by tier (Normal, Persistent, Legendary, Mythic)
//     - Cumulative legendary echo creation rate
//     - "After 10 expeditions: avg 4.2 Legendaries exist across all districts"
//   Chart: stacked area chart of tier distribution over expedition number
//
// "Legendary Encounter Rate":
//   Given player visits 3 districts per expedition, how often do they encounter a Legendary?
//   P(legendary in any visited district) over expeditions
//   Target: first legendary encounter around expedition 5-7
//
// "Save Size Projection":
//   After N expeditions, how many PersistentEchoRecords exist?
//   Estimate save file size growth
//   Recommend: max persistent echo cap if growth is unbounded
```
