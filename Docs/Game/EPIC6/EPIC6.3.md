# EPIC 6.3: Gate Scan & Reroll

**Status**: Planning
**Epic**: EPIC 6 — Gate Selection & Navigation
**Dependencies**: 6.1 (ForwardGateOption, GateSelectionState); EPIC 8 (Trace as scan cost option); EPIC 9 (Compendium Page as scan cost); Framework: Roguelite/ (RunSeedUtility)

---

## Overview

Players can spend resources to gain information (scan) or change their options (reroll). Scanning reveals the Unknown Clause on a forward gate — the hidden modifier that could be a trap, a bonus, or a rare event. Rerolling regenerates the entire forward gate set using the next seed in the deterministic chain, preventing save-scum exploits. Both mechanics create a resource tension: spend now for better intel, or save resources for the district itself.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Gate/Components/GateScanComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Gate
{
    /// <summary>
    /// Cost types accepted for gate scanning.
    /// </summary>
    public enum ScanCostType : byte
    {
        Currency = 0,        // Standard expedition currency
        CompendiumPage = 1,  // Rare knowledge resource (EPIC 9)
        TracePenalty = 2     // +1 Trace — information at the cost of time pressure
    }

    /// <summary>
    /// Request to scan a specific forward gate's Unknown Clause.
    /// Transient — consumed by GateScanSystem in the same frame.
    /// </summary>
    public struct GateScanRequest : IComponentData
    {
        [GhostField] public int GateIndex;       // Which forward gate (0-2)
        [GhostField] public ScanCostType CostType;
        [GhostField] public int RequestingPlayerId;  // NetworkId of requesting player
    }

    /// <summary>
    /// Result of a scan attempt. Transient — read by UI bridge, then destroyed.
    /// </summary>
    public struct GateScanResult : IComponentData
    {
        public int GateIndex;
        public bool Success;                     // False if insufficient resources
        public UnknownClauseType RevealedClause; // Only valid if Success=true
        public int RequestingPlayerId;
    }

    /// <summary>
    /// Request to reroll the entire forward gate set.
    /// Transient — consumed by GateRerollSystem in the same frame.
    /// </summary>
    public struct GateRerollRequest : IComponentData
    {
        [GhostField] public int RequestingPlayerId;
    }

    /// <summary>
    /// Per-cost-type pricing configuration. Singleton with buffer.
    /// </summary>
    public struct GateScanConfig : IComponentData
    {
        public int CurrencyCost;             // e.g., 50
        public int CompendiumPageCost;       // e.g., 1
        public int TracePenaltyCost;         // Always 1 (Trace increment)
        public int RerollCurrencyCost;       // e.g., 150 (higher than scan)
        public int MaxRerollsPerExpedition;  // 2-3
    }

    /// <summary>
    /// Tracks reroll chain position for deterministic seed advancement.
    /// Stored on GateSelectionState entity or as singleton.
    /// </summary>
    public struct RerollChainState : IComponentData
    {
        public uint CurrentSeedOffset;       // Incremented on each reroll
        public int RerollsUsedThisExpedition;
    }
}
```

---

## Systems

### GateScanSystem

```csharp
// File: Assets/Scripts/Gate/Systems/GateScanSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ForwardGateGenerationSystem
//
// Each frame while GateSelectionState.IsActive:
//   1. Query all GateScanRequest entities
//   2. For each request:
//      a. Validate GateIndex is within AvailableForwardGates range
//      b. Find the ForwardGateOption entity matching GateIndex
//      c. Check if UnknownClauseRevealed is already true → reject (duplicate scan)
//      d. Check cost affordability based on ScanCostType:
//         - Currency: read player's expedition currency >= GateScanConfig.CurrencyCost
//         - CompendiumPage: read Compendium inventory (EPIC 9) >= CompendiumPageCost
//         - TracePenalty: always affordable (Trace can always increase)
//      e. If affordable:
//         - Deduct cost from appropriate resource
//         - Set ForwardGateOption.UnknownClauseRevealed = true
//         - Create GateScanResult entity with Success=true, RevealedClause
//      f. If not affordable:
//         - Create GateScanResult entity with Success=false
//   3. Destroy all GateScanRequest entities
```

### GateRerollSystem

```csharp
// File: Assets/Scripts/Gate/Systems/GateRerollSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: GateScanSystem
//
// Each frame while GateSelectionState.IsActive:
//   1. Query all GateRerollRequest entities
//   2. For each request (process only the first — one reroll per frame):
//      a. Check RerollChainState.RerollsUsedThisExpedition < MaxRerollsPerExpedition
//      b. Check player expedition currency >= GateScanConfig.RerollCurrencyCost
//      c. If both pass:
//         - Deduct currency
//         - Increment RerollChainState.CurrentSeedOffset
//         - Increment RerollChainState.RerollsUsedThisExpedition
//         - Destroy all existing ForwardGateOption entities
//         - Compute new seed: RunSeedUtility.Hash(baseSeed, CurrentSeedOffset)
//         - Re-run ForwardGateGenerationSystem logic with new seed
//           (call shared static method, NOT re-trigger system)
//         - Any previously scanned clauses are lost (new gates = new unknowns)
//         - Update GateSelectionState.RerollsRemaining
//      d. If rerolls exhausted or insufficient currency:
//         - No-op (UI should gray out button; system is defensive)
//   3. Destroy all GateRerollRequest entities
```

### GateScanResultCleanupSystem

```csharp
// File: Assets/Scripts/Gate/Systems/GateScanResultCleanupSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup (after UI bridge reads)
//
// Destroy all GateScanResult entities each frame (single-frame lifetime).
```

---

## Seed Chain Diagram

```
Expedition Base Seed
        │
        ├──→ Hash(base, offset=0) → Initial forward gates
        │
        ├──→ Hash(base, offset=1) → Reroll #1 forward gates
        │
        ├──→ Hash(base, offset=2) → Reroll #2 forward gates
        │
        └──→ Hash(base, offset=3) → Reroll #3 (if allowed)

Each offset produces a completely different gate set.
Same base seed + same offset = identical gates (deterministic).
Offset only advances forward — no way to revisit offset=0 after reroll.
```

---

## Setup Guide

1. Add `GateScanComponents.cs` to `Assets/Scripts/Gate/Components/`
2. Add `GateScanSystem.cs`, `GateRerollSystem.cs`, `GateScanResultCleanupSystem.cs` to `Assets/Scripts/Gate/Systems/`
3. Create `GateScanConfig` singleton entity via authoring on the Gate subscene — configure default costs
4. Create `RerollChainState` singleton entity alongside `GateSelectionState` — initialize `CurrentSeedOffset=0` and `RerollsUsedThisExpedition=0` on expedition start
5. Wire scan cost deduction to appropriate resource systems:
   - Currency: EPIC 4 expedition currency
   - CompendiumPage: EPIC 9 Compendium inventory
   - Trace: EPIC 8 TraceState.CurrentTrace += 1
6. Gate UI scan button: create `GateScanRequest` entity on click with selected cost type
7. Gate UI reroll button: create `GateRerollRequest` entity on click; gray out when `RerollsRemaining == 0`
8. Shared generation logic: extract gate generation into a static utility method callable by both `ForwardGateGenerationSystem` and `GateRerollSystem`

---

## Verification

- [ ] Scanning a gate reveals the Unknown Clause and sets `UnknownClauseRevealed = true`
- [ ] Scanning an already-revealed gate is rejected (no double charge)
- [ ] Currency scan deducts correct amount; fails if insufficient
- [ ] CompendiumPage scan deducts 1 page; fails if player has 0 pages
- [ ] TracePenalty scan adds +1 Trace to TraceState
- [ ] Reroll destroys all current ForwardGateOption entities and creates new ones
- [ ] Rerolled gates have different districts/properties than originals (different seed)
- [ ] Reroll count limited to MaxRerollsPerExpedition; further rerolls rejected
- [ ] RerollsRemaining on GateSelectionState decrements correctly
- [ ] Previously scanned clauses are NOT carried over to rerolled gates
- [ ] Seed chain is deterministic: same base seed + same reroll count = same gates
- [ ] GateScanResult entities are cleaned up after one frame
- [ ] No scan or reroll possible when GateSelectionState.IsActive is false

---

## BlobAsset Pipeline

Gate scan and reroll costs are baked into the `GateDefinitionBlob` defined in EPIC 6.1. The `GateScanConfig` singleton at runtime is initialized from the blob, then overridden by `GateLiveTuning` when present. See EPIC 6.1 BlobAsset Pipeline for the full blob structure.

---

## Validation

```csharp
// File: Assets/Editor/GateWorkstation/GateScanValidator.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Gate.Editor
{
    public static class GateScanValidator
    {
        [MenuItem("Hollowcore/Validation/Gate Scan & Reroll Config")]
        public static void Validate()
        {
            // 1. Scan cost range checks:
            //    - CurrencyCost: must be in [1, 500]
            //    - CompendiumPageCost: must be in [1, 5]
            //    - TracePenaltyCost: must be exactly 1
            //    - Error if any cost <= 0

            // 2. Reroll cost checks:
            //    - RerollCurrencyCost must be > CurrencyCost (reroll is more expensive)
            //    - RerollCurrencyCost must be in [50, 1000]
            //    - MaxRerollsPerExpedition must be in [1, 5]

            // 3. Unknown clause weight validation (cross-ref with EPIC 6.1):
            //    - Verify weight array length == UnknownClauseType enum count (6)
            //    - Verify no weight is negative
            //    - Verify sum > 0
            //    - Warn if Trap weight > 30% of total (too punishing)
            //    - Warn if SpecialEvent weight < 5% (too rare to feel rewarding)

            // 4. Seed chain validation:
            //    - Run 20 seeds through reroll chain (3 offsets each)
            //    - Verify no two offsets produce identical gate sets for same base seed

            Debug.Log("[GateScanValidator] Validation complete.");
        }
    }
}
```

---

## Live Tuning

Scan and reroll costs are exposed via `GateLiveTuning` (defined in EPIC 6.1). Systems in this sub-epic read from `GateLiveTuning` singleton when present, falling back to `GateScanConfig` defaults:

- `GateScanSystem`: reads `GateLiveTuning.ScanCurrencyCost` for currency scan checks
- `GateRerollSystem`: reads `GateLiveTuning.RerollCurrencyCost` for reroll affordability
- RunWorkstation sliders are defined in EPIC 6.1 Live Tuning section
