# EPIC 14.4: Boss Counter Token System

**Status**: Planning
**Epic**: EPIC 14 — Boss System & Variant Clauses
**Dependencies**: EPIC 14.1 (BossVariantClauseSO, ClauseId); Framework: Items/, Loot/; EPIC 10 (Side Goals), EPIC 13 (Districts)

---

## Overview

Counter tokens are special items found as side goal rewards in thematically linked districts. Each token is tied to a specific boss variant clause -- possessing the token at fight start disables that clause, making the boss easier. The strategic twist: some tokens are found in District A but apply to District B's boss, rewarding players who plan their route through the expedition graph. Tokens integrate with the framework Items/ system and are consumed on boss fight entry.

---

## Component Definitions

### BossCounterToken (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/CounterTokenComponents.cs
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    /// <summary>
    /// Marks an inventory item entity as a boss counter token.
    /// Lives on the item entity within the player's inventory (Items/ framework).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BossCounterToken : IComponentData
    {
        /// <summary>Unique token ID.</summary>
        [GhostField] public int TokenId;

        /// <summary>Which boss this token affects.</summary>
        [GhostField] public int TargetBossId;

        /// <summary>Which variant clause this token disables on the target boss.</summary>
        [GhostField] public int ClauseIdDisabled;

        /// <summary>Display name for inventory UI.</summary>
        [GhostField] public FixedString64Bytes DisplayName;
    }

    /// <summary>
    /// Tag component on token pickup entities in the world.
    /// Extends LootPickup from the framework Loot/ system.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct CounterTokenPickup : IComponentData
    {
        public int TokenDefinitionId;
    }
}
```

### CounterTokenConsumedEvent (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/CounterTokenComponents.cs (continued)
namespace Hollowcore.Boss
{
    /// <summary>
    /// Transient entity created when a token is consumed at boss fight start.
    /// Read by UI bridge to show "clause disabled" feedback.
    /// </summary>
    public struct CounterTokenConsumedEvent : IComponentData
    {
        public int TokenId;
        public int BossId;
        public int ClauseIdDisabled;
        public Entity PlayerEntity;
    }
}
```

---

## ScriptableObject Definitions

### BossCounterTokenDefinitionSO

```csharp
// File: Assets/Scripts/Boss/Definitions/BossCounterTokenDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Boss.Definitions
{
    [CreateAssetMenu(fileName = "NewCounterToken", menuName = "Hollowcore/Boss/Counter Token")]
    public class BossCounterTokenDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int TokenId;
        public string DisplayName;
        [TextArea] public string Description;
        [TextArea] public string FlavorText;
        public Sprite Icon;

        [Header("Target")]
        [Tooltip("BossDefinitionSO this token targets")]
        public BossDefinitionSO TargetBoss;
        [Tooltip("ClauseId on the target boss that this token disables")]
        public int ClauseIdDisabled;

        [Header("Source")]
        [Tooltip("Which district's side goal awards this token")]
        public int SourceDistrictId;
        [Tooltip("Specific side goal ID that awards this token")]
        public int SourceSideGoalId;

        [Header("Cross-District")]
        [Tooltip("If true, this token is found in a different district than the boss it targets")]
        public bool IsCrossDistrict;
        [Tooltip("Display hint for the Scar Map showing which boss this token affects")]
        [TextArea] public string ScarMapHint;

        [Header("Items Integration")]
        [Tooltip("Item definition ID in the framework Items/ system")]
        public int ItemDefinitionId;
    }
}
```

---

## Systems

### CounterTokenCheckSystem

```csharp
// File: Assets/Scripts/Boss/Systems/CounterTokenCheckSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: BossClauseEvaluationSystem
//
// Queries player inventory for counter tokens when a boss encounter begins.
// Called by BossClauseEvaluationSystem during clause evaluation.
//
// For each boss encounter starting (BossPhaseState.EncounterPhase == PreFight):
//   1. Read BossVariantState.BossId
//   2. Query player inventory (Items/ framework) for all BossCounterToken components
//   3. For each token where TargetBossId matches current boss:
//      a. Record ClauseIdDisabled → pass to BossClauseEvaluationSystem
//      b. Create CounterTokenConsumedEvent transient entity
//      c. Destroy (consume) the token item entity from inventory
//   4. BossClauseEvaluationSystem uses the disabled clause list to skip those clauses
```

### CounterTokenAwardSystem

```csharp
// File: Assets/Scripts/Boss/Systems/CounterTokenAwardSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Awards counter tokens when side goals are completed.
// Hooks into framework reward pipeline from EPIC 10.
//
// When a side goal completion event fires:
//   1. Look up side goal ID in counter token definitions
//   2. If a BossCounterTokenDefinitionSO has SourceSideGoalId matching:
//      a. Create item entity in player inventory via Items/ framework
//      b. Add BossCounterToken component to the item entity
//      c. Fire UI notification: "Counter Token acquired: [DisplayName]"
//      d. If IsCrossDistrict: add Scar Map hint for the target boss's district
```

### CounterTokenPreviewBridge

```csharp
// File: Assets/Scripts/Boss/Bridges/CounterTokenPreviewBridge.cs
// Managed MonoBehaviour — bridges token state to boss preview UI.
//
// Boss Preview Screen (accessible from Scar Map or Gate Screen):
//   1. List all variant clauses for the target boss
//   2. For each clause:
//      a. Check if player has a matching counter token in inventory
//      b. If yes: show clause as "DISABLED" with token icon
//      c. If no: show clause as "ACTIVE" (will be harder)
//   3. Show total active clause count and difficulty estimate
//
// Inventory Token View:
//   1. For each BossCounterToken in inventory:
//      a. Show token name, icon, description
//      b. Show target boss name and which clause it disables
//      c. If cross-district: show Scar Map hint
```

---

## Cross-District Token Examples

| Token | Found In | Targets Boss | Disables Clause |
|---|---|---|---|
| Warden Override Key | Necrospire side goal | Grandmother Null | Warden reinforcements |
| Hymn Scrambler | Cathedral side goal | Archbishop Algorithm | Hymn resonance attack |
| Slag Coolant | Burn side goal | The Foreman | Slag pool expansion |
| Gravity Anchor | Skyfall side goal | King of Heights | Gravity shift mechanic |
| Signal Jammer | Synapse side goal | The Signal (final) | Possession attack |
| Flood Valve Key | Wetmarket side goal | Leviathan Empress | Rising water speed |
| Mirror Shard | Mirrortown side goal | Prime Reflection | Clone multiplication |
| Market Insider | Auction side goal | The Board (final) | Merc wave summon |

---

## Setup Guide

1. Create CounterTokenComponents.cs in `Assets/Scripts/Boss/Components/`
2. Create BossCounterTokenDefinitionSO assets in `Assets/Data/Boss/Tokens/`
3. For each boss in vertical slice: create 1-2 token definitions
4. Wire token definitions to side goal reward tables in EPIC 10
5. Register BossCounterToken as a valid item component in framework Items/ system
6. Add counter token icon sprites to `Assets/Art/UI/Boss/Tokens/`
7. Create boss preview UI panel showing clause/token status
8. Wire CounterTokenAwardSystem to side goal completion events
9. Test cross-district flow: complete side goal in District A, verify token in inventory, enter District B boss fight, verify clause disabled

---

## Verification

- [ ] BossCounterTokenDefinitionSO serializes all fields correctly
- [ ] Counter token appears in inventory after completing linked side goal
- [ ] CounterTokenCheckSystem finds tokens matching current boss
- [ ] Matching token disables the correct variant clause
- [ ] Token is consumed (removed from inventory) on boss fight start
- [ ] CounterTokenConsumedEvent fires for UI feedback
- [ ] Boss preview UI shows clause status (active/disabled by token)
- [ ] Inventory UI shows token with target boss and clause info
- [ ] Cross-district tokens work: found in one district, used in another
- [ ] Scar Map hint displays for cross-district tokens
- [ ] Token does not consume if boss fight is not entered (inventory persistence)
- [ ] Multiple tokens for same boss all apply correctly
- [ ] Token for a different boss is NOT consumed in wrong fight

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/CounterTokenValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules for BossCounterTokenDefinitionSO:
    //
    // 1. TargetBoss reference is non-null.
    //    Error if TargetBoss == null (orphaned token).
    //
    // 2. ClauseIdDisabled matches a clause on TargetBoss:
    //    Scan TargetBoss.VariantClauses for any clause with matching CounterTokenClauseId.
    //    Error if no matching clause found (token has no effect).
    //
    // 3. TargetBoss.BossId matches a valid BossDefinitionSO in the project.
    //    Error if BossId not found in any BossDefinitionSO.
    //
    // 4. SourceSideGoalId references a valid SideGoalDefinitionSO.
    //    Warning if side goal not found (may not be created yet).
    //
    // 5. ItemDefinitionId is unique across all counter token definitions.
    //    Error on duplicate ItemDefinitionId.
    //
    // 6. Cross-district consistency:
    //    If IsCrossDistrict == true, SourceDistrictId must differ from TargetBoss's district.
    //    Warning if IsCrossDistrict but source == target district.
    //
    // 7. Coverage check (project-wide):
    //    For each BossDefinitionSO, count how many clauses have matching counter tokens.
    //    Warning if a boss has clauses with CounterTokenClauseId > 0 but no token exists.
    //    Warning if a boss has zero counter tokens (no insurance path for any clause).
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/BossWorkstation/CounterTokenMatrixModule.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // CounterTokenMatrixModule : IWorkstationModule — tab in BossDesignerWorkstation
    //
    // [1] Token Coverage Matrix
    //     - Rows = all bosses in project
    //     - Columns = all counter tokens
    //     - Cells: checkmark if token targets that boss, X if not
    //     - Color: green = token exists for clause, red = clause has no token
    //     - Click cell to navigate to token or boss SO
    //
    // [2] Cross-District Flow Diagram
    //     - Graph view: district nodes connected by token arrows
    //     - Arrow from District A to District B = "token found in A, used in B"
    //     - Node color: red if district has clauses with no incoming token arrows
    //     - Helps designers verify the "insurance" network is complete
    //
    // [3] Token Acquisition Timeline
    //     - Given an expedition path (sequence of districts), show which tokens
    //       are available before each boss fight
    //     - Highlights "too late" tokens (acquired after the boss they target was already fought)
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/CounterTokenDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Counter token debug display (enabled via `token_debug 1`):
    //
    // [1] Inventory Token Panel
    //     - Lists all BossCounterToken components in player inventory
    //     - Shows target boss name, clause it disables, source district
    //
    // [2] Boss Preview Token Overlay
    //     - During PreFight UI: shows which clauses are disabled by tokens
    //     - Tokens about to be consumed highlighted in gold
    //     - Clauses that COULD have been disabled (token exists but not acquired) shown in gray
}
