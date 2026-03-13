# EPIC 1.5: Limb Memory System

**Status**: Planning
**Epic**: EPIC 1 — Chassis & Limb System
**Dependencies**: EPIC 1.1 (LimbInstance.DistrictAffinityId); EPIC 4 (District Graph for current district)

---

## Overview

Salvaged limbs retain muscle memory from their previous environment. A limb salvaged from the Burn gives heat resistance when you return to the Burn. A Climber's grapple-leg from the Lattice gives movement bonuses there. Individual bonuses are small (5-10%), but a full chassis of matching limbs is significant (25-40%). This creates a strategic loop: salvage from district A, benefit when returning to A.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Chassis/Components/LimbMemoryComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Memory bonus entry on a limb entity. A limb can have multiple memory entries
    /// (e.g., boss limbs may have affinity for multiple districts).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct LimbMemoryEntry : IBufferElementData
    {
        /// <summary>District ID this memory applies to.</summary>
        public int DistrictId;

        /// <summary>Type of bonus granted.</summary>
        public MemoryBonusType BonusType;

        /// <summary>Bonus value (additive or multiplicative depending on BonusType).</summary>
        public float BonusValue;
    }

    public enum MemoryBonusType : byte
    {
        DamageResistance = 0,    // Flat damage reduction in matching district
        MoveSpeed = 1,           // Additive move speed bonus
        ResourceEfficiency = 2,  // Currency/loot find bonus
        AttackDamage = 3,        // Bonus damage dealt
        StaminaEfficiency = 4,   // Reduced stamina cost
        HazardResistance = 5     // Resistance to district-specific Front hazards
    }

    /// <summary>
    /// Aggregated memory bonuses for the current district.
    /// Computed by LimbMemorySystem, read by stat/modifier systems.
    /// On chassis child entity.
    /// </summary>
    public struct ActiveMemoryBonuses : IComponentData
    {
        public float DamageResistanceBonus;
        public float MoveSpeedBonus;
        public float ResourceEfficiencyBonus;
        public float AttackDamageBonus;
        public float StaminaEfficiencyBonus;
        public float HazardResistanceBonus;
        public int MatchingLimbCount;    // How many limbs have affinity for current district
        public int TotalEquippedLimbs;   // Total equipped (for UI: "3/6 matching")
    }
}
```

---

## Systems

### LimbMemorySystem

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbMemorySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: ChassisStatAggregatorSystem
//
// Computes active memory bonuses based on current district.
//
// For each player with ChassisLink:
//   1. Get current district ID from expedition state (EPIC 4)
//   2. Resolve ChassisLink → ChassisState
//   3. For each occupied slot:
//      a. Read LimbMemoryEntry buffer from limb entity
//      b. For each entry where DistrictId == currentDistrictId:
//         - Accumulate bonus into ActiveMemoryBonuses by BonusType
//         - Increment MatchingLimbCount
//   4. Write ActiveMemoryBonuses to chassis entity
//   5. Push bonuses as modifiers into EquippedStatsSystem pipeline
//
// Memory bonuses are ONLY active when in the matching district.
// Entering a different district: ActiveMemoryBonuses reset to zero.
// Entering a matching district: bonuses activate immediately.
```

---

## Bonus Magnitudes

| Limbs Matching | Per-Limb Bonus | Total Bonus | Feel |
|---|---|---|---|
| 1 of 6 | 5-8% | 5-8% | Barely noticeable but measurable |
| 2 of 6 | 5-8% each | 10-16% | "Hm, I'm tougher here" |
| 3 of 6 | 5-8% each | 15-24% | Meaningful advantage |
| 4 of 6 | 5-8% each | 20-32% | Clearly specialized |
| 5-6 of 6 | 5-8% each | 25-48% | District specialist build |

Boss limbs have 10-15% per limb (strongest memory bonuses, GDD §3.2).

---

## ScriptableObject Extension

```csharp
// In LimbDefinitionSO (from EPIC 1.1), the memory entries are authored:
// [Header("District Memory")]
// public List<LimbMemoryEntryData> MemoryEntries;
//
// [System.Serializable]
// public struct LimbMemoryEntryData
// {
//     public int DistrictId;
//     public MemoryBonusType BonusType;
//     public float BonusValue;
// }
```

---

## Setup Guide

1. Add `LimbMemoryEntry` buffer and `ActiveMemoryBonuses` to chassis child entity baker
2. Configure memory entries on LimbDefinitionSO assets:
   - Necrospire limbs: DistrictId=1, HazardResistance (data corruption resistance)
   - Burn limbs: DistrictId=6, DamageResistance (heat), HazardResistance
   - Lattice limbs: DistrictId=8, MoveSpeed (climbing), DamageResistance (fall)
3. LimbMemorySystem needs read access to current district ID (shared singleton from EPIC 4)
4. UI: show memory bonus indicator when entering a district with matching limbs
   - "3/6 limbs attuned to The Burn — +18% heat resistance"

---

## Verification

- [ ] Limbs with matching DistrictAffinityId activate bonuses when in that district
- [ ] Bonuses deactivate when leaving the district
- [ ] Multiple matching limbs stack additively
- [ ] Boss limbs give larger bonuses than common limbs
- [ ] ActiveMemoryBonuses correctly fed into EquippedStatsSystem
- [ ] UI shows "X/6 limbs attuned" indicator
- [ ] Limbs with no affinity (DistrictAffinityId = -1) contribute zero memory bonus

---

## Live Tuning

### Memory Bonus Runtime Config

Memory bonus magnitudes are the primary balance lever for the "build around districts" strategic loop. These must be tunable at runtime.

```csharp
// File: Assets/Scripts/Chassis/Components/LimbMemoryConfig.cs
using Unity.Entities;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Runtime-tunable memory bonus parameters. Singleton.
    /// </summary>
    public struct LimbMemoryConfig : IComponentData
    {
        /// <summary>Global multiplier on all memory bonuses (1.0 = as authored).</summary>
        public float GlobalBonusMultiplier;     // Default: 1.0

        /// <summary>Bonus multiplier for boss limbs specifically.</summary>
        public float BossLimbBonusMultiplier;   // Default: 1.0 (stacks with global)

        /// <summary>Cap per bonus type (0 = uncapped). Prevents stacking exploits.</summary>
        public float MaxDamageResistanceBonus;  // Default: 0.5 (50% cap)
        public float MaxMoveSpeedBonus;         // Default: 0.3 (30% cap)
        public float MaxResourceEfficiency;     // Default: 0.5
        public float MaxAttackDamageBonus;      // Default: 0.4
        public float MaxStaminaEfficiency;      // Default: 0.4
        public float MaxHazardResistance;       // Default: 0.6

        /// <summary>Minimum matching limbs before any bonus activates (0 = always active).</summary>
        public int MinLimbsForActivation;       // Default: 0
    }
}
```

| Parameter | Default | Notes |
|---|---|---|
| `GlobalBonusMultiplier` | 1.0 | Dial up/down entire memory system strength |
| `MaxDamageResistanceBonus` | 0.5 | Hard cap prevents 6-limb matching from trivializing damage |
| `MinLimbsForActivation` | 0 | Set to 2+ if single-limb bonuses feel too easy |

**Propagation**: `LimbMemorySystem` reads singleton each frame during aggregation. Cheap read, no dirty flag.

---

## Debug Visualization

### Memory Bonus Overlay

```
// Toggle: console command `chassis.memory` or key binding
//
// Draws:
// 1. Per-limb slot indicator: glow color = district affinity color, gray = no affinity
// 2. "X/6 ATTUNED" text with current district name
// 3. Active bonus breakdown list:
//    "DamageResist: +18% (3 limbs × 6%)"
//    "MoveSpeed: +5% (1 limb × 5%)"
// 4. When entering a new district: flash showing bonus change
//    "+12% HazardResist activated" / "-8% DamageResist deactivated"
//
// Implementation: LimbMemoryDebugSystem (ClientSimulation | LocalSimulation, PresentationSystemGroup)
```

---

## Simulation & Testing

### Memory Bonus Balance Simulation

```
// Test: MemoryBonusBalanceSimulation
// Setup: simulate all possible chassis loadouts for a target district:
//   - 0 matching limbs through 6 matching limbs
//   - Mix of common (5% per) and boss (12% per) limbs
// Verify:
//   - 0 matching: all bonuses = 0
//   - 1 common matching: total bonus 5-8% (one stat)
//   - 6 common matching: total bonus 30-48% across stats, all under caps
//   - 3 boss matching: total bonus 36-45% (should approach but not exceed caps)
//   - Full boss set (6): bonuses hit caps, not exceed
// Purpose: validate memory bonus curve creates desired "specialist" power fantasy
//          without breaking difficulty curve
```

### District Transition Bonus Test

```
// Test: DistrictTransitionBonusTest
// Setup: Player with 3 Burn-affinity limbs + 3 Lattice-affinity limbs
// Scenario:
//   1. Enter Burn district → ActiveMemoryBonuses reflects 3 matching (Burn bonuses active)
//   2. Enter Lattice district → Bonuses switch to 3 matching (Lattice bonuses active)
//   3. Enter neutral district → ActiveMemoryBonuses all zero
// Verify: bonuses activate/deactivate correctly on district transition, no stale data
```

### Performance Test

```
// Test: LimbMemorySystemPerformanceTest
// Setup: 100 players, each with 6 limbs, 2 memory entries per limb
// Target: LimbMemorySystem completes in < 0.1ms per frame
// Note: System only recalculates on district change (not every frame in production),
//       but stress test measures worst case (all players change district simultaneously)
```
