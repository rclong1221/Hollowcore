# EPIC 6.1: Forward Gate Presentation

**Status**: Planning
**Epic**: EPIC 6 — Gate Selection & Navigation
**Dependencies**: EPIC 4.1 (ExpeditionGraphState); Framework: Roguelite/ (RunSeedUtility)

---

## Overview

After extraction, 2-3 forward gate options are generated from the expedition graph. Each gate shows a district preview with rich information: name, reward focus, known threat, Front forecast, Strife interaction, and a mystery Unknown Clause. Gate offers are seed-deterministic. High Trace reduces available gates.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Gate/Components/GateComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace Hollowcore.Gate
{
    public enum FrontForecast : byte
    {
        Slow = 0,      // Front advances slowly — more exploration time
        Steady = 1,    // Normal advance rate
        Volatile = 2   // Fast advance — squeeze comes early
    }

    /// <summary>
    /// A forward gate option presented to the player.
    /// Transient — created during gate screen, destroyed on selection.
    /// </summary>
    public struct ForwardGateOption : IComponentData
    {
        public int GateIndex;                  // 0, 1, or 2
        public int TargetDistrictId;
        public FixedString64Bytes DistrictName;
        public int RewardFocusCategory;        // RewardCategory enum value
        public int KnownThreatFactionId;       // 1 confirmed faction
        public FrontForecast Forecast;
        public int StrifeInteractionId;        // -1 = no special interaction
        public int UnknownClauseId;            // Hidden until scanned
        public bool UnknownClauseRevealed;
    }

    /// <summary>
    /// Singleton: current gate selection state.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct GateSelectionState : IComponentData
    {
        public int AvailableForwardGates;  // 2 or 3 (reduced by Trace)
        public int RerollsRemaining;
        public bool IsActive;              // True when gate screen is showing
    }

    /// <summary>
    /// Unknown clause types hidden behind scan.
    /// </summary>
    public enum UnknownClauseType : byte
    {
        HiddenThreat = 0,      // Additional dangerous faction
        SpecialEvent = 1,      // Rare merchant, vault, legendary limb
        RewardModifier = 2,    // Bonus loot multiplier
        Trap = 3,              // Front starts at Phase 2
        AllyPresence = 4,      // Rival operator team present (EPIC 11)
        EchoCarryover = 5      // Persistent echoes from past expeditions
    }
}
```

---

## Systems

### ForwardGateGenerationSystem

```csharp
// File: Assets/Scripts/Gate/Systems/ForwardGateGenerationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// On extraction event (district complete):
//   1. Read ExpeditionGraphState — find all unlocked, unvisited forward edges
//   2. Determine gate count: 3 (default) or 2 (if Trace >= 3)
//   3. Select districts from available forward edges (seed-deterministic):
//      - If more options than gates: weighted selection based on graph position
//      - If fewer: show all available
//   4. For each selected district:
//      a. DistrictName from DistrictDefinitionSO
//      b. RewardFocusCategory from district definition
//      c. KnownThreatFactionId: randomly reveal 1 of 4 factions (seed-deterministic)
//      d. FrontForecast: derived from FrontDefinitionSO advance curve steepness
//      e. StrifeInteractionId: check if current Strife card (EPIC 7) has interaction with this district
//      f. UnknownClauseId: roll from UnknownClauseType pool (seed-deterministic)
//   5. Create ForwardGateOption entities
//   6. Set GateSelectionState.IsActive = true
```

---

## Gate Card UI Layout

```
┌─────────────────────────────────┐
│ [District Thumbnail]            │
│ THE BURN                        │
│─────────────────────────────────│
│ Reward Focus: Augment           │
│ Known Threat: Slag Walkers      │
│ Front: Volatile ⚡              │
│ Strife: Nanoforge Bloom         │
│   → Furnaces counter bloom      │
│─────────────────────────────────│
│ Unknown: [? Scan to reveal]     │
│ Cost: 1 Compendium Page or +1T  │
└─────────────────────────────────┘
```

---

## Setup Guide

1. Create `Assets/Scripts/Gate/` folder with Components/, Systems/, Bridges/, UI/
2. Create `Hollowcore.Gate.asmdef`
3. ForwardGateGenerationSystem hooks to extraction event from EPIC 4.3
4. Gate UI prefab: card layout per gate option with all info fields
5. Strife interaction lookup: cross-reference StrifeCardDefinitionSO (EPIC 7) with district ID
6. Unknown clause pool: configure weights per clause type

---

## Verification

- [ ] 3 forward gates generated at Trace 0-2
- [ ] 2 forward gates generated at Trace 3+
- [ ] Gate info matches target district's actual properties
- [ ] Known threat reveals exactly 1 of 4 factions
- [ ] Strife interaction shown when applicable (-1 when not)
- [ ] Unknown clause hidden until scanned
- [ ] Gate generation is seed-deterministic
- [ ] No duplicate districts across gates

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Gate/Components/GateDefinitionBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Gate
{
    /// <summary>
    /// Blob asset baked from GateDefinitionSO for Burst-compatible runtime access.
    /// Contains scan cost tables, unknown clause weight pools, and gate count thresholds.
    /// </summary>
    public struct GateDefinitionBlob
    {
        /// <summary>Base number of forward gates (default 3).</summary>
        public int BaseForwardGateCount;

        /// <summary>Trace threshold at which gate count reduces by 1.</summary>
        public int TraceGateReductionThreshold;

        /// <summary>Minimum forward gates (never fewer than this).</summary>
        public int MinForwardGateCount;

        /// <summary>Weights for each UnknownClauseType when rolling hidden clauses.</summary>
        public BlobArray<float> UnknownClauseWeights; // Indexed by (byte)UnknownClauseType

        /// <summary>Sum of all clause weights (cached for normalized sampling).</summary>
        public float UnknownClauseWeightSum;

        /// <summary>Scan cost values per ScanCostType.</summary>
        public BlobArray<int> ScanCosts; // Indexed by (byte)ScanCostType

        /// <summary>Reroll currency cost.</summary>
        public int RerollCurrencyCost;

        /// <summary>Max rerolls per expedition.</summary>
        public int MaxRerolls;
    }

    public struct GateDefinitionDatabase : IComponentData
    {
        public BlobAssetReference<GateDefinitionBlob> Blob;
    }
}
```

```csharp
// File: Assets/Scripts/Gate/Authoring/GateDefinitionAuthoring.cs
using Unity.Entities;
using UnityEngine;

namespace Hollowcore.Gate.Authoring
{
    public class GateDefinitionAuthoring : MonoBehaviour
    {
        public GateDefinitionSO Definition;

        class Baker : Baker<GateDefinitionAuthoring>
        {
            public override void Bake(GateDefinitionAuthoring authoring)
            {
                var def = authoring.Definition;
                var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
                ref var root = ref builder.ConstructRoot<GateDefinitionBlob>();

                root.BaseForwardGateCount = def.BaseForwardGateCount;
                root.TraceGateReductionThreshold = def.TraceGateReductionThreshold;
                root.MinForwardGateCount = def.MinForwardGateCount;
                root.RerollCurrencyCost = def.RerollCurrencyCost;
                root.MaxRerolls = def.MaxRerolls;

                // Bake unknown clause weights
                var clauseCount = def.UnknownClauseWeights.Length;
                var clauseArr = builder.Allocate(ref root.UnknownClauseWeights, clauseCount);
                float weightSum = 0f;
                for (int i = 0; i < clauseCount; i++)
                {
                    clauseArr[i] = def.UnknownClauseWeights[i];
                    weightSum += def.UnknownClauseWeights[i];
                }
                root.UnknownClauseWeightSum = weightSum;

                // Bake scan costs
                var scanArr = builder.Allocate(ref root.ScanCosts, def.ScanCosts.Length);
                for (int i = 0; i < def.ScanCosts.Length; i++)
                    scanArr[i] = def.ScanCosts[i];

                var entity = GetEntity(TransformUsageFlags.None);
                AddBlobAsset(ref builder, out var blobRef);
                AddComponent(entity, new GateDefinitionDatabase { Blob = blobRef });
            }
        }
    }
}
```

---

## Validation

```csharp
// File: Assets/Editor/GateWorkstation/GateDefinitionValidator.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Gate.Editor
{
    public static class GateDefinitionValidator
    {
        [MenuItem("Hollowcore/Validation/Gate Definitions")]
        public static void ValidateAll()
        {
            // 1. Gate count constraints
            //    - BaseForwardGateCount must be 2 or 3
            //    - MinForwardGateCount must be >= 1 and <= BaseForwardGateCount
            //    - TraceGateReductionThreshold must be >= 1

            // 2. Unknown clause weight sums
            //    - All weights must be > 0
            //    - Weight array length must match UnknownClauseType enum count
            //    - Sum must be > 0 (no degenerate pools)
            //    - Warn if any single weight > 60% of total (over-concentration)

            // 3. Scan cost ranges
            //    - Currency cost: 1..500 (warn outside range)
            //    - CompendiumPage cost: 1..5
            //    - TracePenalty cost: always 1 (error if != 1)
            //    - RerollCurrencyCost > CurrencyCost (reroll must be more expensive)

            // 4. Cross-reference: verify all RewardCategory values used in
            //    ForwardGateOption match values defined in the reward system

            Debug.Log("[GateValidator] Validation complete.");
        }
    }
}
```

---

## Editor Tooling

### Gate Card Preview Tool

```csharp
// File: Assets/Editor/GateWorkstation/GateCardPreviewWindow.cs
// EditorWindow that renders a gate card with sample data — no play mode required.
//
// Features:
//   - Dropdown: select any DistrictDefinitionSO
//   - Dropdown: select FrontForecast, UnknownClauseType
//   - Dropdown: select active StrifeCardDefinitionSO (or None)
//   - Toggle: UnknownClauseRevealed on/off
//   - Live IMGUI rendering of the gate card using the same layout as EPIC 6.1 diagram
//   - Export: "Copy as JSON" for test fixture data
//
// Location: Window > Hollowcore > Gate Card Preview

// File: Assets/Editor/GateWorkstation/GateLayoutEditor.cs
// EditorWindow for visualizing the full gate selection screen layout.
//
// Features:
//   - Configure number of forward gates (2 or 3)
//   - Add/remove backtrack gates with mock DistrictSaveState data
//   - Drag-arrange card positions (forward row, backtrack row)
//   - Preview vote overlay (player portraits on cards)
//   - Preview Scar Map mini-view with adjustable visited districts
//   - Respects GateDefinitionSO constraints (auto-validates on change)
//
// Follows DIG workstation pattern:
//   - Sidebar tabs: "Forward Gates", "Backtrack Gates", "Scar Map", "Vote Preview"
//   - IWorkstationModule interface per tab
//
// Location: Window > Hollowcore > Gate Layout Editor
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Gate/Components/GateLiveTuning.cs
using Unity.Entities;

namespace Hollowcore.Gate
{
    /// <summary>
    /// Singleton for runtime-tunable gate parameters.
    /// Modified by the RunWorkstation live tuning panel (EPIC 23.7).
    /// </summary>
    public struct GateLiveTuning : IComponentData
    {
        /// <summary>Currency cost for scanning a gate. Default from GateDefinitionBlob.</summary>
        public int ScanCurrencyCost;

        /// <summary>Reroll currency cost. Default from GateDefinitionBlob.</summary>
        public int RerollCurrencyCost;

        /// <summary>Trace threshold that reduces forward gate count. Default 3.</summary>
        public int TraceGateReductionThreshold;

        /// <summary>Override for base forward gate count (0 = use blob default).</summary>
        public int ForwardGateCountOverride;
    }
}

// Live tuning integration:
// - GateLiveTuning singleton created by GateDefinitionAuthoring with blob defaults
// - ForwardGateGenerationSystem reads GateLiveTuning instead of blob when singleton exists
// - GateScanSystem reads GateLiveTuning.ScanCurrencyCost for cost checks
// - RunWorkstation (EPIC 23.7) exposes sliders:
//     Scan Cost:       [1 ──●────── 500]  (int)
//     Reroll Cost:     [50 ──●───── 1000] (int)
//     Trace Threshold: [1 ──●────── 5]    (int)
//     Gate Count:      [2 ──●── 3]        (int, 0=auto)
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Gate/Debug/GateDebugOverlay.cs
// Managed SystemBase, ClientSimulation | LocalSimulation, PresentationSystemGroup
//
// Gate Selection Screen State Overlay — toggled via debug console: `gate.debug`
//
// Displays:
//   1. Available forward gates:
//      - GateIndex, TargetDistrictId, RewardFocusCategory, FrontForecast
//      - UnknownClauseId + revealed status
//      - StrifeInteractionId
//   2. Backtrack gates:
//      - DistrictId, CurrentFrontPhase vs FrontPhaseWhenLeft, DangerDelta
//      - ActiveEchoCount, PendingRewardValue
//      - SeededEvents bitmask (expanded)
//   3. Scan status:
//      - Which gates have been scanned
//      - Remaining scan resources per cost type
//   4. Vote state (co-op):
//      - Per-player vote (NetworkId → Direction + GateIndex)
//      - Timer remaining
//      - Tally per gate
//   5. GateSelectionState: AvailableForwardGates, RerollsRemaining, IsActive
//   6. RerollChainState: CurrentSeedOffset, RerollsUsedThisExpedition
//
// Rendered as IMGUI overlay in top-left corner, semi-transparent background.
// Color coding: green=selected, yellow=scanned, grey=unscanned.
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/GateWorkstation/GateDiversitySimulation.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Gate.Editor
{
    /// <summary>
    /// Gate diversity simulation: verifies gate generation across many seeds.
    /// Menu: Hollowcore > Simulation > Gate Diversity
    /// </summary>
    public static class GateDiversitySimulation
    {
        [MenuItem("Hollowcore/Simulation/Gate Diversity (100 seeds)")]
        public static void RunDiversityTest()
        {
            // Test: "Given 100 seeds, verify gate diversity"
            //
            // For each seed 1..100:
            //   1. Run ForwardGateGenerationSystem logic (static utility) with Trace=0
            //   2. Record the 3 gate RewardFocusCategory values
            //   3. Flag if all 3 categories are identical
            //
            // Report:
            //   - Seeds with 3 identical reward focus categories (FAIL if any)
            //   - Category distribution histogram across all 300 gates
            //   - Most/least common district pairings
            //   - Reroll chain test: verify 3 consecutive rerolls produce unique gate sets
            //
            // Expected: zero seeds produce 3 identical reward focus categories.
            // Threshold: each category appears in at least 10% of total gates.
        }
    }
}
```
