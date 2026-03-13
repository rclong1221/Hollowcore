# EPIC 6.1 Setup Guide: Forward Gate Presentation

**Status:** Planned
**Requires:** EPIC 4.1 (ExpeditionGraphState), Framework Roguelite/ (RunSeedUtility)

---

## Overview

Forward gates are the 2-3 new district options shown to players after extracting from a district. Each gate card displays a district preview with reward focus, known threat, Front forecast, Strife interaction tag, and a hidden Unknown Clause. This guide covers creating the GateDefinitionSO, setting up the gate card UI prefab, and configuring scan cost tables.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Expedition State prefab | ExpeditionGraphState (EPIC 4.1) | Provides forward edges for gate generation |
| Gate Subscene | GateDefinitionAuthoring | Bakes GateDefinitionBlob singleton |
| Strife Card Database (EPIC 7) | StrifeCardDatabase singleton | Optional: Strife interaction tags on gates |

### New Setup Required
1. Create the `GateDefinitionSO` ScriptableObject asset
2. Create the `GateDefinitionAuthoring` GameObject in the Gate subscene
3. Build the Gate Card UI prefab
4. Configure GateLiveTuning defaults

---

## 1. Create GateDefinitionSO

**Create:** `Assets > Create > Hollowcore/Gate/Gate Definition`
**Recommended location:** `Assets/Data/Gate/GateDefinition.asset`

### 1.1 Gate Count Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **BaseForwardGateCount** | Number of forward gates at low Trace | 3 | 2-3 |
| **TraceGateReductionThreshold** | Trace level that reduces gate count by 1 | 3 | 1-5 |
| **MinForwardGateCount** | Minimum gates shown regardless of Trace | 2 | 1-3 |

**Tuning tip:** Setting `TraceGateReductionThreshold` to 3 means players see 3 gates for the first 2 Trace levels and 2 gates after. Lower values increase pressure earlier.

### 1.2 Unknown Clause Weight Pool

Weights control how often each clause type appears behind the "?" scan slot. The pool is indexed by `(byte)UnknownClauseType`.

| Index | UnknownClauseType | Description | Recommended Weight |
|-------|-------------------|-------------|-------------------|
| 0 | HiddenThreat | Additional dangerous faction | 25 |
| 1 | SpecialEvent | Rare merchant, vault, legendary limb | 15 |
| 2 | RewardModifier | Bonus loot multiplier | 20 |
| 3 | Trap | Front starts at Phase 2 | 15 |
| 4 | AllyPresence | Rival operator team present (EPIC 11) | 10 |
| 5 | EchoCarryover | Persistent echoes from past expeditions | 15 |

**Tuning tip:** Keep Trap weight under 30% of total to avoid scan-aversion. SpecialEvent should be at least 5% so scanning feels occasionally rewarding.

### 1.3 Scan Cost Configuration

| Index | ScanCostType | Description | Default | Range |
|-------|-------------|-------------|---------|-------|
| 0 | Currency | Standard expedition currency | 50 | 1-500 |
| 1 | CompendiumPage | Rare knowledge resource (EPIC 9) | 1 | 1-5 |
| 2 | TracePenalty | +1 Trace (always costs exactly 1) | 1 | 1 (fixed) |

### 1.4 Reroll Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **RerollCurrencyCost** | Currency cost to regenerate all forward gates | 150 | 50-1000 |
| **MaxRerolls** | Maximum rerolls per expedition | 3 | 1-5 |

**Tuning tip:** Reroll cost should be significantly higher than scan cost (at least 3x) to make scanning the preferred intel-gathering method.

---

## 2. Gate Subscene Setup

### 2.1 Add GateDefinitionAuthoring

1. Open your **Gate Subscene** (or create one in the expedition scene)
2. Create an empty GameObject named `GateConfig`
3. **Add Component > GateDefinitionAuthoring**
4. Assign the `GateDefinitionSO` asset created in Step 1 to the **Definition** field
5. This bakes the `GateDefinitionDatabase` singleton with the blob asset and creates the `GateLiveTuning` singleton with defaults from the blob

### 2.2 Verify Baking

After entering Play Mode or reimporting the subscene:
- Check ECS EntityDebugger for `GateDefinitionDatabase` singleton entity
- Verify `GateLiveTuning` singleton exists with correct default values
- Verify blob data: `BaseForwardGateCount`, `UnknownClauseWeights` array length = 6

---

## 3. Gate Card UI Prefab

**Recommended location:** `Assets/Prefabs/UI/Gate/GateCard.prefab`

### 3.1 Card Layout Structure

```
GateCard (RectTransform)
  +-- DistrictThumbnail (Image)
  +-- DistrictNameText (TextMeshProUGUI)
  +-- Divider (Image)
  +-- RewardFocusRow
  |     +-- Label ("Reward Focus:")
  |     +-- ValueText
  +-- KnownThreatRow
  |     +-- Label ("Known Threat:")
  |     +-- ValueText
  +-- FrontForecastRow
  |     +-- Label ("Front:")
  |     +-- ForecastIcon (Image)
  |     +-- ValueText
  +-- StrifeInteractionRow (hidden if StrifeInteractionId == -1)
  |     +-- StrifeIcon (Image)
  |     +-- StrifeNameText
  |     +-- StrifeDescriptionText
  +-- Divider2 (Image)
  +-- UnknownClauseRow
  |     +-- RevealedText (hidden until scanned)
  |     +-- ScanPromptText ("[? Scan to reveal]")
  |     +-- ScanCostText ("Cost: 1 Compendium Page or +1T")
  +-- SelectButton (Button)
```

### 3.2 UI Bridge Script

Create a MonoBehaviour `GateCardAdapter` that reads `ForwardGateOption` component data and populates the UI fields. It should:
- Map `FrontForecast` enum to display strings: Slow / Steady / Volatile
- Map `FrontForecast` to icons: green arrow / yellow dash / red lightning
- Show/hide Strife row based on `StrifeInteractionId != -1`
- Show/hide Unknown Clause revealed text based on `UnknownClauseRevealed`

---

## 4. Gate Screen Layout

**Recommended location:** `Assets/Prefabs/UI/Gate/GateScreen.prefab`

### 4.1 Full Screen Structure

```
GateScreen (Canvas, ScreenSpace-Overlay)
  +-- Header ("CHOOSE YOUR PATH")
  +-- ForwardGateRow (HorizontalLayoutGroup)
  |     +-- GateCard_0 (instantiated GateCard prefab)
  |     +-- GateCard_1
  |     +-- GateCard_2 (hidden if AvailableForwardGates == 2)
  +-- BacktrackSection (vertical, populated by EPIC 6.2)
  +-- RerollButton
  |     +-- RerollCostText
  |     +-- RerollsRemainingText
  +-- VoteOverlay (EPIC 6.4, hidden in solo)
```

---

## 5. GateLiveTuning (RunWorkstation Integration)

The `GateLiveTuning` singleton is created automatically by `GateDefinitionAuthoring`. It exposes runtime-overridable fields for the RunWorkstation (EPIC 23.7):

| Field | Slider Label | Default | Min | Max |
|-------|-------------|---------|-----|-----|
| ScanCurrencyCost | Scan Cost | 50 | 1 | 500 |
| RerollCurrencyCost | Reroll Cost | 150 | 50 | 1000 |
| TraceGateReductionThreshold | Trace Threshold | 3 | 1 | 5 |
| ForwardGateCountOverride | Gate Count Override | 0 | 0 | 3 |

When `ForwardGateCountOverride` is 0, the system uses the blob default. Any non-zero value overrides the base count.

---

## 6. Scene & Subscene Checklist

- [ ] Gate Subscene exists in the expedition scene hierarchy
- [ ] `GateConfig` GameObject has `GateDefinitionAuthoring` with assigned SO
- [ ] `GateDefinitionSO` asset exists at `Assets/Data/Gate/GateDefinition.asset`
- [ ] `GateCard.prefab` exists at `Assets/Prefabs/UI/Gate/GateCard.prefab`
- [ ] `GateScreen.prefab` exists at `Assets/Prefabs/UI/Gate/GateScreen.prefab`
- [ ] Unknown clause weights array has exactly 6 entries (matches `UnknownClauseType` enum)
- [ ] Scan costs array has exactly 3 entries (matches `ScanCostType` enum)
- [ ] Assembly definition `Hollowcore.Gate.asmdef` exists at `Assets/Scripts/Gate/`

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| UnknownClauseWeights array wrong length | Baking crash or index-out-of-range at runtime | Ensure exactly 6 entries matching UnknownClauseType enum count |
| ScanCosts array wrong length | GateScanSystem reads garbage values | Ensure exactly 3 entries matching ScanCostType enum count |
| RerollCurrencyCost < CurrencyCost | Players reroll instead of scanning (design intent violated) | Set reroll cost to at least 3x scan cost |
| Missing GateDefinitionSO reference | NullReference in baker, no blob created | Assign the SO asset on the GateDefinitionAuthoring inspector |
| Gate subscene not reimported after changes | Old blob data used at runtime | Close and reopen the subscene to force reimport |
| Strife row always visible | Visual clutter on non-interacting gates | Gate UI adapter must check `StrifeInteractionId != -1` before showing |
| All unknown clause weights zero | Division by zero in weighted random selection | Run `Hollowcore > Validation > Gate Definitions` to catch this |

---

## Verification

- [ ] Enter Play Mode and trigger extraction (or use debug command `gate.open`)
- [ ] 3 forward gate cards appear at Trace 0-2
- [ ] 2 forward gate cards appear at Trace 3+
- [ ] Each card shows: district name, reward focus, known threat, Front forecast
- [ ] Strife interaction row visible only on gates with active interaction
- [ ] Unknown clause shows "[? Scan to reveal]" until scanned
- [ ] Gate generation is deterministic: same seed produces same gates
- [ ] Run `Hollowcore > Validation > Gate Definitions` with zero errors
- [ ] Run `Hollowcore > Simulation > Gate Diversity (100 seeds)` with no 3-identical results
