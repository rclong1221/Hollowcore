# EPIC 14.5: Boss Reward & Extraction

**Status**: Planning
**Epic**: EPIC 14 — Boss System & Variant Clauses
**Dependencies**: EPIC 14.2 (BossEncounterSystem, Victory state); Framework: Loot/, Items/, Roguelite/ (MetaBank); EPIC 1 (Boss Limbs), EPIC 4 (District Graph), EPIC 9 (Compendium)

---

## Overview

After defeating a district boss, players receive guaranteed high-quality rewards and enter an extraction sequence. Boss loot includes a unique limb drop, counter tokens for other districts, Compendium entries, and a large currency dump. The extraction sequence transitions through a loot room into a gate back to the Gate Selection screen. District completion effects propagate: forward gates unlock, the district's Front pauses, and power-vacuum events seed in previously cleared districts.

---

## Component Definitions

### BossRewardState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/BossRewardComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    public enum ExtractionPhase : byte
    {
        Inactive = 0,
        RewardDump = 1,    // Loot spawning in loot room
        LootRoom = 2,      // Player collecting loot, managing inventory
        ExtractionGate = 3, // Player at exit gate, ready to leave
        Extracting = 4      // Transition to Gate Selection screen
    }

    /// <summary>
    /// Tracks the reward and extraction sequence after boss victory.
    /// Lives on the boss encounter entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BossRewardState : IComponentData
    {
        [GhostField] public ExtractionPhase Phase;

        /// <summary>Boss ID for loot table lookup.</summary>
        [GhostField] public int BossId;

        /// <summary>Timer for extraction sequence pacing.</summary>
        [GhostField(Quantization = 100)] public float PhaseTimer;

        /// <summary>Number of reward items spawned in loot room.</summary>
        [GhostField] public byte RewardCount;

        /// <summary>Whether the guaranteed limb drop has been spawned.</summary>
        [GhostField] public bool LimbDropSpawned;

        /// <summary>Whether Compendium entries have been awarded.</summary>
        [GhostField] public bool CompendiumAwarded;

        /// <summary>Currency amount awarded.</summary>
        [GhostField] public int CurrencyAwarded;
    }
}
```

### DistrictCompletionEvent (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/BossRewardComponents.cs (continued)
namespace Hollowcore.Boss
{
    /// <summary>
    /// Transient entity created when a district boss is defeated.
    /// Read by multiple systems: graph unlocking, Front pausing, event seeding.
    /// </summary>
    public struct DistrictCompletionEvent : IComponentData
    {
        public int DistrictId;
        public int BossId;
        public Entity PlayerEntity;
    }
}
```

### ExtractionTrigger (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/BossRewardComponents.cs (continued)
namespace Hollowcore.Boss
{
    /// <summary>
    /// Marks the extraction gate entity in the loot room.
    /// Player interaction triggers the extraction sequence.
    /// </summary>
    public struct ExtractionTrigger : IComponentData
    {
        public int LinkedDistrictId;
        public Entity EncounterEntity;
    }
}
```

---

## Systems

### BossRewardSystem

```csharp
// File: Assets/Scripts/Boss/Systems/BossRewardSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossEncounterSystem
//
// Spawns rewards after boss victory and manages the loot room phase.
//
// When BossPhaseState.EncounterPhase == Victory AND BossRewardState.Phase == Inactive:
//   1. Transition to RewardDump phase
//   2. Look up BossDefinitionSO.BossLootTable
//   3. Roll loot table via framework Loot/ system
//   4. Spawn guaranteed rewards:
//      a. Boss limb drop (LimbPickup entity with boss-unique LimbDefinitionSO) — EPIC 1
//         - Boss limbs are always Legendary rarity, Permanent durability
//         - Strongest memory bonuses of any limb
//      b. Counter tokens for OTHER district bosses (BossCounterTokenDefinitionSO)
//         - 1-2 tokens per boss kill, selected from cross-district pool
//      c. Compendium entries (EPIC 9) — boss entry + any first-time mechanics
//      d. Currency dump (Roguelite/ MetaBank):
//         - Base amount from BossDefinitionSO
//         - Bonus for low death count (0 deaths = +50%, 1 death = +25%)
//         - Bonus for active variant clauses (more clauses = harder = more reward)
//   5. Spawn all reward entities in loot room at designated spawn points
//   6. Set RewardCount, LimbDropSpawned, CompendiumAwarded, CurrencyAwarded
//   7. Transition to LootRoom phase
//
// LootRoom phase:
//   1. Safe period — no enemies, no timer pressure
//   2. Player can pick up loot, manage inventory, equip limbs
//   3. Extraction gate entity activated (ExtractionTrigger)
//   4. Phase persists until player interacts with extraction gate
```

### ExtractionTriggerSystem

```csharp
// File: Assets/Scripts/Boss/Systems/ExtractionTriggerSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Handles the extraction gate interaction and district completion effects.
//
// When player interacts with ExtractionTrigger:
//   1. Set BossRewardState.Phase = ExtractionGate
//   2. Create DistrictCompletionEvent transient entity
//   3. Apply district completion effects:
//      a. Unlock forward gates in expedition graph (EPIC 4)
//         - Districts connected forward from this one become available
//      b. Pause district Front (EPIC 3)
//         - Front timer frozen for this district (not reversed)
//         - Front effects already applied remain
//      c. Seed power-vacuum events in previously cleared districts (GDD §4.3)
//         - New side quests, NPC spawns, resource availability changes
//   4. Update Scar Map with boss completion marker (EPIC 12)
//   5. After brief extraction animation: transition to Extracting phase
//   6. Fire screen transition to Gate Selection screen (Roguelite/ RunLifecycleSystem)
```

### BossRewardScalingSystem

```csharp
// File: Assets/Scripts/Boss/Systems/BossRewardScalingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: BossRewardSystem
//
// Calculates reward multipliers based on fight performance.
//
// Inputs:
//   - BossPhaseState.PlayerDeathCount
//   - BossVariantState.ActiveClauseCount
//   - RuntimeDifficultyScale (Roguelite/ framework)
//   - AscensionDefinitionSO tier (Roguelite/ framework)
//
// Outputs (stored as BossRewardMultipliers on encounter entity):
//   - CurrencyMultiplier: base * deathBonus * clauseBonus * difficultyBonus
//   - LimbQualityBonus: higher active clauses → better integrity roll on boss limb
//   - BonusTokenChance: chance for an extra counter token
```

---

## Reward Table Reference

| Reward Type | Source | Quantity | Conditions |
|---|---|---|---|
| Boss Limb | BossDefinitionSO | 1 guaranteed | Always — unique per boss |
| Counter Tokens | Cross-district pool | 1-2 | Random from available pool |
| Compendium Entry | Boss entry | 1 guaranteed | First kill only |
| Currency (meta) | MetaBank | Large dump | Scaled by performance |
| Currency (run) | Run economy | Medium dump | Scaled by difficulty |
| Bonus Materials | LootTableSO roll | 0-3 | RNG from loot table |

---

## Setup Guide

1. Create BossRewardComponents.cs in `Assets/Scripts/Boss/Components/`
2. Create loot room prefab with:
   - Reward spawn points (3-5 positions for loot entities)
   - Extraction gate entity with ExtractionTrigger component
   - Safe zone (no enemy spawns, ambient lighting)
3. Configure boss loot tables in `Assets/Data/Boss/LootTables/`
   - Each boss: 1 LootTableSO with guaranteed + random rolls
4. Create boss-unique limb definitions in `Assets/Data/Chassis/Limbs/Boss/`
   - Legendary rarity, Permanent durability, strong memory bonuses
5. Wire BossRewardSystem to Loot/ framework spawn pipeline
6. Wire ExtractionTriggerSystem to EPIC 4 graph unlocking and EPIC 3 Front pausing
7. Configure currency amounts in BossDefinitionSO (base + scaling curves)
8. Add extraction gate interaction definition to framework Interaction/ system
9. Create reward summary UI showing all awarded items/currency

---

## Verification

- [ ] Boss victory transitions to RewardDump phase
- [ ] Guaranteed boss limb spawns in loot room (Legendary, Permanent)
- [ ] Counter tokens spawn from cross-district pool
- [ ] Compendium entry awarded on first boss kill
- [ ] Currency scales with death count (0 deaths = +50%)
- [ ] Currency scales with active variant clause count
- [ ] Loot room is safe (no enemies, no timer)
- [ ] Extraction gate interaction triggers district completion
- [ ] Forward gates unlock in expedition graph
- [ ] District Front pauses (frozen, not reversed)
- [ ] Power-vacuum events seed in previously cleared districts
- [ ] Scar Map updates with boss completion marker
- [ ] Screen transitions to Gate Selection after extraction
- [ ] Reward scaling respects RuntimeDifficultyScale and AscensionDefinitionSO
- [ ] Multiple boss kills in same expedition award tokens correctly (no duplicates)

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/BossRewardValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules for boss reward and extraction:
    //
    // 1. BossDefinitionSO.BossLootTable.LootTableId references a valid LootTableSO.
    //    Error if LootTableId not found in Loot/ framework registry.
    //
    // 2. Boss limb drop: every BossDefinitionSO should have a corresponding
    //    LimbDefinitionSO in Assets/Data/Chassis/Limbs/Boss/ with matching BossId.
    //    Warning if no boss-specific limb exists (missing unique reward).
    //
    // 3. Currency base amount > 0 in BossDefinitionSO.
    //    Warning if 0 (no currency reward on boss kill).
    //
    // 4. Cross-district token pool: verify that BossRewardSystem's token pool
    //    for each boss does not include tokens targeting the same boss.
    //    Error if a boss can drop its own counter token.
    //
    // 5. Extraction gate: every boss arena subscene must contain an ExtractionTrigger entity.
    //    Error if arena subscene has no ExtractionTrigger.
    //
    // 6. Reward spawn points: loot room prefab must have >= RewardCount spawn positions.
    //    Warning if spawn points < expected reward count (loot stacks on single point).
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/BossRewardLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Reward and extraction live tuning:
    //
    //   float CurrencyMultiplierOverride  // -1 = use computed, >0 = override
    //   bool ForceMaxRewards              // spawn maximum possible rewards (guaranteed + all RNG)
    //   bool SkipLootRoom                 // jump directly to extraction gate
    //   bool SkipExtractionSequence       // instant transition to Gate Selection
    //   int ForceBonusTokenCount          // -1 = use RNG, 0+ = force exact count
    //   bool DisableDistrictCompletion    // suppress district completion effects (graph unlock, Front pause)
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/BossRewardDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Reward debug overlay (enabled via `reward_debug 1`):
    //
    // [1] Reward Breakdown Panel
    //     - Lists all spawned rewards: type, name, quantity
    //     - Shows reward multipliers: death bonus, clause bonus, difficulty bonus
    //     - Currency calculation breakdown (base * multiplier chain)
    //
    // [2] Extraction Status
    //     - ExtractionPhase badge (RewardDump / LootRoom / ExtractionGate / Extracting)
    //     - Extraction gate entity position marker
    //
    // [3] District Completion Effects
    //     - Lists all effects triggered: gates unlocked, Front paused, events seeded
    //     - Scar Map marker update confirmation
}
