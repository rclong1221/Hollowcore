# EPIC 7.3: Strife-District Interaction

**Status**: Planning
**Epic**: EPIC 7 — Strife System
**Priority**: High — District-level Strife differentiation
**Dependencies**: EPIC 7.1 (Strife Card Data Model), EPIC 4 (Districts); Optional: EPIC 6 (Gate Screen tags)

---

## Overview

Each Strife card amplifies or mitigates 3 specific districts. This sub-epic defines the per-card per-district override data, the runtime system that applies district-scoped modifiers on entry, gate screen integration for strategic planning, and the amplification/mitigation classification that drives reward scaling. Players can read upcoming district interactions on forward gates and make informed routing decisions: seek amplified districts for bonus rewards or avoid them to reduce difficulty.

---

## Component Definitions

### StrifeDistrictModifier (IComponentData)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeDistrictModifier.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Applied to the active district entity when the player enters a district
    /// that has a Strife interaction. Removed on district exit.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct StrifeDistrictModifier : IComponentData
    {
        /// <summary>Which Strife card produced this modifier.</summary>
        [GhostField] public StrifeCardId SourceCardId;

        /// <summary>District ID this modifier applies to.</summary>
        [GhostField] public int DistrictId;

        /// <summary>Whether this interaction amplifies or mitigates the Strife.</summary>
        [GhostField] public StrifeInteractionType InteractionType;

        /// <summary>
        /// Modifier set hash referencing the RunModifierDefinitionSO for this interaction.
        /// Resolved by RunModifierApplySystem.
        /// </summary>
        [GhostField] public int ModifierSetHash;

        /// <summary>
        /// Bonus reward multiplier for completing the district under this interaction.
        /// Applied by the district completion reward system.
        /// </summary>
        [GhostField(Quantization = 100)] public float BonusRewardMultiplier;
    }
}
```

### StrifeGateTag (IComponentData)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeGateTag.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Applied to gate entities whose destination district has a Strife interaction.
    /// Read by the gate screen UI to display the Strife tag.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct StrifeGateTag : IComponentData
    {
        /// <summary>Active Strife card ID for icon display.</summary>
        [GhostField] public StrifeCardId CardId;

        /// <summary>Amplify or Mitigate — determines warning vs opportunity display.</summary>
        [GhostField] public StrifeInteractionType InteractionType;

        /// <summary>Destination district ID (for tooltip lookup).</summary>
        [GhostField] public int DestinationDistrictId;
    }
}
```

### StrifeDistrictActive (IEnableableComponent)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeDistrictActive.cs
using Unity.Entities;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Enableable flag on the expedition-state entity. Baked disabled.
    /// Enabled when the player is inside a district that has a Strife interaction.
    /// Used by reward systems to check if the bonus multiplier applies.
    /// </summary>
    public struct StrifeDistrictActive : IComponentData, IEnableableComponent { }
}
```

---

## ScriptableObject Definitions

### StrifeDistrictEffectSO

```csharp
// File: Assets/Scripts/Strife/Definitions/StrifeDistrictEffectSO.cs
using UnityEngine;

namespace Hollowcore.Strife.Definitions
{
    /// <summary>
    /// Per-card, per-district override. Defines what happens when the active Strife card
    /// meets a specific district. Each StrifeCardDefinitionSO references 3 of these.
    /// Can also be created standalone for modding/extensibility.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStrifeDistrictEffect", menuName = "Hollowcore/Strife/District Effect")]
    public class StrifeDistrictEffectSO : ScriptableObject
    {
        [Header("Targeting")]
        public StrifeCardId SourceCard;
        public int DistrictId;

        [Header("Classification")]
        public StrifeInteractionType InteractionType;
        [TextArea(2, 4)] public string EffectDescription;
        [TextArea(1, 2)] public string GateScreenTooltip;

        [Header("Modifier")]
        [Tooltip("RunModifierDefinitionSO implementing this district interaction")]
        public ScriptableObject ModifierDefinition;

        [Header("Rewards")]
        [Tooltip("Multiplier applied to district completion rewards (1.0 = no bonus)")]
        public float BonusRewardMultiplier = 1.0f;
        [Tooltip("Whether completing the district under this interaction grants a unique reward")]
        public bool GrantsUniqueReward;
        [Tooltip("Unique reward item ID (0 = none)")]
        public int UniqueRewardItemId;

        [Header("Visual")]
        [Tooltip("Override particle effect for this specific interaction (null = use card default)")]
        public GameObject OverrideParticleEffect;
        [Tooltip("Color tint for district-specific Strife markers")]
        public Color MarkerTint = Color.white;
        public Sprite InteractionIcon;
    }
}
```

---

## Systems

### StrifeDistrictEntrySystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeDistrictEntrySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DistrictTransitionSystem (EPIC 4)
//
// Triggered on district entry (DistrictTransitionEvent or equivalent).
//
// Algorithm:
//   1. Read ActiveStrifeState singleton → ActiveCardId
//   2. Resolve StrifeCardBlob from StrifeCardDatabase
//   3. Check if the entered district matches any of the card's 3 DistrictInteractions
//   4. If NO match → ensure StrifeDistrictModifier is removed, disable StrifeDistrictActive
//   5. If MATCH:
//      a. Create/update StrifeDistrictModifier on the district entity
//      b. Enable StrifeDistrictActive on the expedition-state entity
//      c. Inject district-scoped RunModifier entry via StrifeModifierBridge
//         (same pipeline as map-rule modifiers but scoped to current district)
//      d. Fire StrifeDistrictEnteredEvent for UI notification
//
// On district EXIT (detected via DistrictTransitionEvent):
//   1. Remove StrifeDistrictModifier from previous district entity
//   2. Strip district-scoped RunModifier entries
//   3. Disable StrifeDistrictActive
```

### StrifeGateTagSystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeGateTagSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: GateDiscoverySystem (EPIC 6)
//
// Tags forward gate entities with Strife interaction info for the gate screen.
//
// Algorithm:
//   1. Read ActiveStrifeState singleton → ActiveCardId
//   2. Resolve StrifeCardBlob → 3 DistrictInteractions
//   3. Query all gate entities with a DestinationDistrictId component
//   4. For each gate:
//      a. Check if gate's destination district matches any of the 3 interactions
//      b. If MATCH → add StrifeGateTag { CardId, InteractionType, DestinationDistrictId }
//      c. If NO match → remove StrifeGateTag if present
//   5. On card rotation (StrifeRotatedEvent): re-evaluate all gates
//
// The gate screen UI reads StrifeGateTag to display:
//   - Amplify: red skull icon + "Strife Amplified" label + tooltip
//   - Mitigate: blue shield icon + "Strife Mitigated" label + tooltip
```

### StrifeDistrictRewardSystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeDistrictRewardSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DistrictCompletionSystem (EPIC 4)
//
// Applies bonus rewards when completing a district with an active Strife interaction.
//
// Algorithm:
//   1. On DistrictCompletionEvent:
//   2. Check if StrifeDistrictActive is enabled
//   3. If enabled, read StrifeDistrictModifier → BonusRewardMultiplier
//   4. Multiply base district rewards by BonusRewardMultiplier
//   5. If interaction grants a unique reward → spawn unique reward item
//   6. Fire StrifeBonusRewardEvent for UI display
//
// Amplify interactions: higher multiplier (1.3x–2.0x) to compensate for difficulty
// Mitigate interactions: lower or no multiplier (1.0x–1.1x) since difficulty is reduced
```

---

## Complete District Interaction Table

All 36 interactions (12 cards x 3 districts each) from GDD §8.1:

| Card | District | Type | Effect | Reward Mult |
|---|---|---|---|---|
| Succession War | Auction | Amplify | Auditors hyperactive, purchases trigger ambush, prices +20%, loot quality +1 tier | 1.5x |
| Succession War | Garrison | Amplify | Double faction patrols, military-grade loot drops | 1.4x |
| Succession War | Bazaar | Mitigate | War refugees sell discounted supplies, prices -30% | 1.0x |
| Signal Schism | Cathedral | Amplify | Hymn pulses stronger (wider AoE, shorter interval) | 1.4x |
| Signal Schism | Neon Strip | Mitigate | Ad noise reduces HUD hack frequency | 1.0x |
| Signal Schism | Datawell | Amplify | Data corruption shuffles random item stats on entry | 1.5x |
| Plague Armada | Quarantine | Amplify | Plague stacks 2x faster, completion grants Plague immunity | 1.8x |
| Plague Armada | Garden | Mitigate | Herbs reduce stacks, entry removes 3 stacks | 1.0x |
| Plague Armada | Clinic | Amplify | Healing items 30% chance to add Plague stack | 1.3x |
| Gravity Storm | Skyfall | Amplify | Permanent low-gravity, completion grants Gravity Boots | 1.6x |
| Gravity Storm | Foundry | Mitigate | Gravity anchors negate float pockets entirely | 1.0x |
| Gravity Storm | Lattice | Amplify | Moving gravity fields on platforming, fall damage x2 | 1.5x |
| Quiet Crusade | Deadwave | Mitigate | No additional penalty, analog weapons +25% damage | 1.1x |
| Quiet Crusade | Cathedral | Amplify | Hymn pulses extend dead zone duration +3s | 1.4x |
| Quiet Crusade | Arsenal | Amplify | Active-ability weapon mods disabled | 1.3x |
| Data Famine | Bazaar | Amplify | 20% vendor closure chance per purchase, remaining stock -50% | 1.5x |
| Data Famine | Datawell | Amplify | Less intel per hack, full-room hack reveals floor map | 1.4x |
| Data Famine | Clinic | Mitigate | Full vendor stock, normal healing rates | 1.0x |
| Black Budget | Garrison | Amplify | Camera drones, laser grids, automated lockdowns | 1.5x |
| Black Budget | Neon Strip | Mitigate | Crowds provide cover, cloaked enemies cannot ambush in crowds | 1.0x |
| Black Budget | Auction | Amplify | Black market items (powerful but cursed), no Auditor triggers | 1.3x |
| Market Panic | Auction | Amplify | Prices double, rare items 3x frequency, Auditor protection available | 1.6x |
| Market Panic | Bazaar | Amplify | Flash sales every 30s (90% off, 5s window) | 1.3x |
| Market Panic | Foundry | Mitigate | Stable economy, standard loot, no volatility | 1.0x |
| Memetic Wild | Cathedral | Amplify | Hymn pulses carry memetic payloads, random 5s status per pulse | 1.4x |
| Memetic Wild | Neon Strip | Amplify | Ad holograms become memetic traps (thought zones) | 1.5x |
| Memetic Wild | Datawell | Mitigate | Hacking grants 30s thought-zone immunity | 1.1x |
| Nanoforge Bloom | Foundry | Amplify | Surface shifts 2x speed (30s), doubled hazards | 1.5x |
| Nanoforge Bloom | Garden | Amplify | Vegetation attacks (vine traps, spores, root barriers) | 1.4x |
| Nanoforge Bloom | Quarantine | Mitigate | Containment slows shifts (120s), enemy reassemble 6s | 1.1x |
| Sovereign Raid | Garrison | Amplify | Full-scale battles, massive firefights, exceptional loot | 1.7x |
| Sovereign Raid | Bazaar | Mitigate | Raiders avoid bazaar (neutral zone), safe restocking | 1.0x |
| Sovereign Raid | Skyfall | Amplify | Drop pod invasion, falling debris hazard | 1.5x |
| Time Fracture | Datawell | Amplify | 25% hack reset chance, successful hacks grant double intel | 1.4x |
| Time Fracture | Clinic | Amplify | 20% chance healing is rewound (undone after 5s) | 1.3x |
| Time Fracture | Deadwave | Mitigate | Rewind pockets cannot form in dead zones | 1.0x |

---

## Gate Screen Integration

```
┌───────────────────────────────────────┐
│  FORWARD GATE — QUARANTINE DISTRICT   │
│                                       │
│  Difficulty: ██████░░ (Hard)          │
│  Enemies: Infected, Toxic Brutes      │
│                                       │
│  ⚠ STRIFE AMPLIFIED: Plague Armada    │
│  ┌─────────────────────────────────┐  │
│  │ 🔴 Plague stacks accumulate 2x  │  │
│  │    faster. Completion grants     │  │
│  │    permanent Plague immunity.    │  │
│  │    Reward bonus: +80%           │  │
│  └─────────────────────────────────┘  │
│                                       │
│  [Enter District]    [Choose Another] │
└───────────────────────────────────────┘
```

- **Amplify**: red border, skull icon, warning text, shows reward bonus
- **Mitigate**: blue border, shield icon, opportunity text, shows benefit
- **No interaction**: no Strife tag on gate (majority of districts)

---

## Setup Guide

1. **Requires** EPIC 7.1 complete and EPIC 7.2 `ActiveStrifeState` singleton operational
2. Create `StrifeDistrictModifier.cs`, `StrifeGateTag.cs`, `StrifeDistrictActive.cs` in `Assets/Scripts/Strife/Components/`
3. Create `StrifeDistrictEffectSO.cs` in `Assets/Scripts/Strife/Definitions/`
4. Create 36 district effect assets in `Assets/Data/Strife/DistrictEffects/` (one per card-district pair), named `StrifeDistrict_01_Auction.asset` pattern
5. Reference each set of 3 district effects from the parent `StrifeCardDefinitionSO`
6. Create `StrifeDistrictEntrySystem.cs` in `Assets/Scripts/Strife/Systems/`
7. Create `StrifeGateTagSystem.cs` in `Assets/Scripts/Strife/Systems/`
8. Create `StrifeDistrictRewardSystem.cs` in `Assets/Scripts/Strife/Systems/`
9. Add `StrifeDistrictActive` (baked disabled) to the expedition-state authoring alongside `ActiveStrifeState`
10. Wire gate screen UI to read `StrifeGateTag` for display (EPIC 6 integration)

---

## Verification

- [ ] Entering a district with a Strife interaction creates `StrifeDistrictModifier` on the district entity
- [ ] `StrifeDistrictActive` enabled only while inside an interacting district
- [ ] District-scoped RunModifier entries injected on entry, stripped on exit
- [ ] Exiting a Strife-interacting district fully cleans up modifiers (no leaking)
- [ ] Forward gates correctly tagged with `StrifeGateTag` for interacting destinations
- [ ] Gates without Strife interaction have no `StrifeGateTag`
- [ ] On card rotation, all gate tags re-evaluated for new card's interactions
- [ ] Amplify interactions show red/skull in gate screen UI
- [ ] Mitigate interactions show blue/shield in gate screen UI
- [ ] District completion rewards multiplied by `BonusRewardMultiplier` when interaction active
- [ ] Unique rewards granted for qualifying interactions
- [ ] All 36 district interactions from GDD §8.1 represented in asset data
- [ ] `StrifeDistrictEffectSO` assets reference correct `RunModifierDefinitionSO`
- [ ] Ghost replication: `StrifeDistrictModifier` and `StrifeGateTag` visible on all clients

---

## Validation

```csharp
// File: Assets/Editor/StrifeWorkstation/StrifeDistrictInteractionValidator.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Hollowcore.Strife.Editor
{
    public static class StrifeDistrictInteractionValidator
    {
        [MenuItem("Hollowcore/Validation/Strife District Interactions")]
        public static void ValidateAll()
        {
            // 1. District over-concentration check:
            //    - Build district → card count map from all 36 interactions
            //    - Warn if any district is referenced by > 3 cards
            //    - Error if any district is referenced by > 5 cards
            //    - Report: "District {name}: {count} interactions ({list of cards})"

            // 2. Modifier hash existence:
            //    - For each of the 36 StrifeDistrictEffectSO assets:
            //      a. Verify ModifierDefinition is not null
            //      b. Verify the SO's computed hash matches a registered RunModifierDefinitionSO
            //    - Error if any hash resolves to nothing (runtime crash)

            // 3. Reward multiplier ranges:
            //    - Amplify interactions: BonusRewardMultiplier must be in [1.2, 2.5]
            //    - Mitigate interactions: BonusRewardMultiplier must be in [1.0, 1.2]
            //    - Error if Amplify multiplier < 1.0 (no penalty for harder content)
            //    - Error if Mitigate multiplier > 1.5 (over-rewarding easy content)

            // 4. Interaction type consistency:
            //    - Cross-reference with the GDD table (36 entries)
            //    - Verify exactly 36 assets exist
            //    - Verify each card has exactly 3 interactions (2 Amplify + 1 Mitigate typical)
            //    - Warn if a card has 3 Amplify and 0 Mitigate (no relief path)

            // 5. Gate screen tooltip:
            //    - Each StrifeDistrictEffectSO must have non-empty GateScreenTooltip
            //    - Tooltip length must be <= 80 chars (fits gate card layout)

            Debug.Log("[StrifeDistrictValidator] Validation complete.");
        }
    }
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Strife/Components/StrifeDistrictLiveTuning.cs
using Unity.Entities;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Singleton for runtime-tunable district interaction multipliers.
    /// Modified by the RunWorkstation live tuning panel (EPIC 23.7).
    /// </summary>
    public struct StrifeDistrictLiveTuning : IComponentData
    {
        /// <summary>
        /// Global multiplier applied to all district interaction BonusRewardMultiplier values.
        /// Default 1.0. Set to 0.5 to halve all bonuses, 2.0 to double.
        /// </summary>
        public float GlobalRewardMultiplierScale;

        /// <summary>
        /// Global multiplier applied to all district interaction difficulty modifiers.
        /// Default 1.0. Controls how much Amplify interactions increase difficulty.
        /// </summary>
        public float GlobalDifficultyScale;

        /// <summary>
        /// Per-interaction-type override. 0 = use default from SO.
        /// Indexed by a flat index (cardId-1)*3 + interactionIndex.
        /// Stored in a companion buffer for per-interaction granularity.
        /// </summary>
        public bool UsePerInteractionOverrides;
    }
}

// Live tuning integration:
// - StrifeDistrictEntrySystem multiplies modifier strength by GlobalDifficultyScale
// - StrifeDistrictRewardSystem multiplies BonusRewardMultiplier by GlobalRewardMultiplierScale
// - RunWorkstation (EPIC 23.7) exposes:
//     Reward Scale:     [0.5 ──●── 2.0]
//     Difficulty Scale: [0.5 ──●── 2.0]
```

---

## Debug Visualization

See EPIC 7.2 Debug Visualization — the StrifeActiveDebugOverlay includes district interaction status (section 3). Additionally:

```csharp
// File: Assets/Scripts/Strife/Debug/StrifeGateTagDebugOverlay.cs
// Managed SystemBase, ClientSimulation | LocalSimulation, PresentationSystemGroup
//
// Strife Gate Tag Debug — toggled via debug console: `strife.gate.debug`
//
// Active only during gate screen (GateSelectionState.IsActive):
//   - Lists all ForwardGateOption entities
//   - For each gate, shows:
//     * StrifeGateTag present? (yes/no)
//     * If yes: CardId, InteractionType, DestinationDistrictId
//     * Interaction description (from StrifeDistrictEffectSO tooltip)
//   - Color: red border = Amplify, blue border = Mitigate, none = no interaction
//
// Also shows the current StrifeCardBlob's 3 DistrictInteractions for quick reference.
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/StrifeWorkstation/StrifeDistrictSimulation.cs
using UnityEditor;

namespace Hollowcore.Strife.Editor
{
    /// <summary>
    /// District interaction balance simulation.
    /// Menu: Hollowcore > Simulation > Strife District Interactions
    /// </summary>
    public static class StrifeDistrictSimulation
    {
        [MenuItem("Hollowcore/Simulation/Strife District Interactions")]
        public static void RunDistrictSimulation()
        {
            // 1. Strife + Front phase interaction analysis:
            //    - For each of the 36 interactions:
            //      a. Compute effective difficulty at Front Phase 1, 2, 3, 4
            //      b. Flag interactions where Phase 4 + Amplify exceeds danger threshold
            //      c. Report: "Plague Armada × Quarantine at Phase 4: danger score 9.2/10"

            // 2. Player routing simulation:
            //    - Simulate 100 expeditions with 6 districts each
            //    - At each gate screen, compute which forward gate is optimal:
            //      * Mitigate interactions always preferred? → Amplify rewards too low
            //      * Amplify interactions chosen ~30-40% of time? → balance is good
            //    - Report: Amplify selection rate per card

            // 3. Modifier stacking edge cases:
            //    - Verify district-scoped modifiers do not stack with map-rule modifiers
            //      that affect the same parameter (e.g., loot rates)
            //    - If they do stack: verify combined value is within sane bounds
            //    - Data Famine map rule (0.6x loot) + Bazaar Amplify (vendor closure):
            //      verify combined loot availability is still > 0

            // 4. Gate tag accuracy:
            //    - For 20 seeds × all 12 cards, generate forward gates
            //    - Verify StrifeGateTag is set if and only if destination district
            //      matches one of the card's 3 DistrictInteractions
        }
    }
}
```
