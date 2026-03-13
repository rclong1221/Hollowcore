# EPIC 6.3 Setup Guide: Gate Scan & Reroll

**Status:** Planned
**Requires:** EPIC 6.1 (ForwardGateOption, GateDefinitionBlob, GateLiveTuning), EPIC 8 (Trace — optional scan cost), EPIC 9 (Compendium — optional scan cost)

---

## Overview

The scan and reroll mechanics let players trade resources for information or new options at the gate screen. Scanning reveals the hidden Unknown Clause on a forward gate. Rerolling regenerates the entire forward gate set using a deterministic seed chain. Both create a resource tension that is central to the strategic layer.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Gate Subscene | GateDefinitionAuthoring (EPIC 6.1) | Bakes scan/reroll cost defaults into GateDefinitionBlob |
| Gate Subscene | GateScanConfig singleton (via authoring) | Runtime cost configuration |
| Gate Subscene | RerollChainState singleton (via authoring) | Tracks reroll seed position |
| ForwardGateOption entities | Created by ForwardGateGenerationSystem | Targets for scan/reroll operations |

### New Setup Required
1. Create `GateScanConfig` singleton via authoring in Gate subscene
2. Create `RerollChainState` singleton via authoring in Gate subscene
3. Set up Scan UI buttons on the Gate Card prefab
4. Set up Reroll UI button on the Gate Screen prefab
5. Wire cost deduction to resource systems (Currency, Compendium, Trace)

---

## 1. GateScanConfig Singleton

**Create:** Add a `GateScanConfigAuthoring` component to a GameObject in the Gate subscene.
**Recommended:** Place on the same `GateConfig` GameObject that holds `GateDefinitionAuthoring`.

### 1.1 Inspector Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **CurrencyCost** | Expedition currency cost per scan | 50 | 1-500 |
| **CompendiumPageCost** | Compendium Page cost per scan | 1 | 1-5 |
| **TracePenaltyCost** | Trace increment per scan (always 1) | 1 | 1 (fixed) |
| **RerollCurrencyCost** | Currency cost per reroll | 150 | 50-1000 |
| **MaxRerollsPerExpedition** | Hard cap on rerolls per expedition | 3 | 1-5 |

**Tuning tip:** These values initialize from `GateDefinitionBlob` but are overridden at runtime by `GateLiveTuning` when present. Change blob defaults for baseline, use live tuning for playtest iteration.

---

## 2. RerollChainState Singleton

**Create:** Add a `RerollChainStateAuthoring` component to the same `GateConfig` GameObject.

### 2.1 Inspector Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **CurrentSeedOffset** | Current position in the reroll chain | 0 | (auto-managed) |
| **RerollsUsedThisExpedition** | Number of rerolls used so far | 0 | (auto-managed) |

Both fields reset to 0 at expedition start. The system manages them automatically. No manual configuration needed beyond ensuring the authoring component exists.

---

## 3. Scan UI Setup

### 3.1 Add Scan Button to Gate Card Prefab

Open `Assets/Prefabs/UI/Gate/GateCard.prefab` and add inside the `UnknownClauseRow`:

```
UnknownClauseRow
  +-- ScanButton (Button)
  |     +-- ScanButtonText ("Scan")
  +-- ScanCostDropdown (TMP_Dropdown)
  |     Options: "50 Currency", "1 Compendium Page", "+1 Trace"
  +-- ScanPromptText ("[? Scan to reveal]")
  +-- RevealedClauseText (hidden until scanned)
```

### 3.2 Scan Button Logic

The scan button MonoBehaviour should:
1. Read the selected `ScanCostType` from the dropdown
2. Create a `GateScanRequest` entity:
   - `GateIndex` = this card's index (0, 1, or 2)
   - `CostType` = selected cost type
   - `RequestingPlayerId` = local player's NetworkId
3. Disable the button after sending (re-enable if `GateScanResult.Success == false`)
4. On `GateScanResult` with `Success == true`:
   - Hide `ScanPromptText`
   - Show `RevealedClauseText` with the `UnknownClauseType` display name
   - Permanently disable scan button for this gate

### 3.3 Cost Affordability Check

Before enabling the scan button, check resource availability:

| ScanCostType | Check | System |
|-------------|-------|--------|
| Currency | Player expedition currency >= `GateScanConfig.CurrencyCost` | EPIC 4 currency |
| CompendiumPage | Compendium inventory page count >= `CompendiumPageCost` | EPIC 9 Compendium |
| TracePenalty | Always affordable (Trace can always increase) | EPIC 8 Trace |

Gray out cost options the player cannot afford. TracePenalty is always available as a last resort.

---

## 4. Reroll UI Setup

### 4.1 Add Reroll Button to Gate Screen

Open `Assets/Prefabs/UI/Gate/GateScreen.prefab` and locate the `RerollButton`:

```
RerollButton (Button)
  +-- RerollIcon (Image)
  +-- RerollCostText ("Reroll: 150 Currency")
  +-- RerollsRemainingText ("2 remaining")
```

### 4.2 Reroll Button Logic

The reroll button MonoBehaviour should:
1. Create a `GateRerollRequest` entity with `RequestingPlayerId`
2. Disable the button while processing
3. On successful reroll (new `ForwardGateOption` entities appear):
   - Update all gate cards with new data
   - Reset scan states (new gates = new unknowns)
   - Decrement the remaining count display
4. Gray out when `GateSelectionState.RerollsRemaining == 0`
5. Gray out when player currency < `GateScanConfig.RerollCurrencyCost`

**Tuning tip:** Show the reroll count prominently. Players should feel the scarcity of rerolls as a resource.

---

## 5. Shared Gate Generation Utility

The gate generation logic must be callable from both `ForwardGateGenerationSystem` (initial generation) and `GateRerollSystem` (reroll). Extract it as a static utility:

**File:** `Assets/Scripts/Gate/Utility/GateGenerationUtility.cs`

```
GateGenerationUtility.GenerateForwardGates(
    uint seed,
    int gateCount,
    ref ExpeditionGraphState graph,
    ref StrifeCardBlob activeStrife,
    EntityCommandBuffer ecb
)
```

The reroll system computes a new seed via `RunSeedUtility.Hash(baseSeed, CurrentSeedOffset)` and calls the same utility.

---

## 6. Seed Chain Diagram

```
Expedition Base Seed
    |
    +-- Hash(base, offset=0) --> Initial forward gates
    |
    +-- Hash(base, offset=1) --> Reroll #1 forward gates
    |
    +-- Hash(base, offset=2) --> Reroll #2 forward gates
    |
    +-- Hash(base, offset=3) --> Reroll #3 (if allowed)
```

Each offset produces completely different gates. Same base seed + same offset = identical gates. Offset only advances forward; no way to revisit offset=0 after reroll.

---

## 7. Scene & Subscene Checklist

- [ ] `GateScanConfigAuthoring` on Gate subscene `GateConfig` GameObject
- [ ] `RerollChainStateAuthoring` on Gate subscene `GateConfig` GameObject
- [ ] Gate Card prefab has scan button + cost dropdown in `UnknownClauseRow`
- [ ] Gate Screen prefab has reroll button with cost and remaining displays
- [ ] `GateGenerationUtility.cs` exists in `Assets/Scripts/Gate/Utility/`
- [ ] Cost deduction wired: Currency (EPIC 4), CompendiumPage (EPIC 9), Trace (EPIC 8)
- [ ] `GateScanSystem.cs`, `GateRerollSystem.cs`, `GateScanResultCleanupSystem.cs` in `Assets/Scripts/Gate/Systems/`

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| No GateScanConfig singleton in subscene | GateScanSystem silently skips (no config to read) | Add GateScanConfigAuthoring to Gate subscene |
| RerollChainState not reset on expedition start | Reroll count carries over between expeditions | Ensure initialization system resets to 0 on new expedition |
| Scanning already-revealed gate | Double charge (costs resources, no effect) | GateScanSystem rejects if `UnknownClauseRevealed == true`; UI should also disable button |
| Reroll cost not reading from GateLiveTuning | Live tuning changes have no effect on reroll price | System must check for `GateLiveTuning` singleton first, fallback to `GateScanConfig` |
| Gate generation logic duplicated | Rerolled gates use different algorithm than originals | Extract shared utility method; both systems call it |
| Previously scanned clauses shown after reroll | Stale scan data on new gate entities | Reroll destroys old ForwardGateOption entities; new ones start with `UnknownClauseRevealed = false` |
| GateScanResult entities accumulate | Memory leak, stale results read by UI | GateScanResultCleanupSystem must destroy all results each frame |

---

## Verification

- [ ] Scanning a gate reveals the Unknown Clause text
- [ ] Scanning deducts the correct resource amount
- [ ] Scanning an already-scanned gate is rejected (no double charge)
- [ ] Insufficient resources: scan button grayed out per cost type
- [ ] TracePenalty scan increments Trace by 1
- [ ] Reroll replaces all forward gates with new ones
- [ ] Rerolled gates have different districts/properties
- [ ] Previously scanned clauses do NOT carry to rerolled gates
- [ ] Reroll count limited by MaxRerollsPerExpedition
- [ ] Reroll button grayed out when remaining = 0 or insufficient currency
- [ ] Seed chain is deterministic: same base seed + same reroll count = same gates
- [ ] Run `Hollowcore > Validation > Gate Scan & Reroll Config` with zero errors
