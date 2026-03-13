# EPIC 7.3 Setup Guide: Strife-District Interaction

**Status:** Planned
**Requires:** EPIC 7.1 (StrifeCardDefinitionSO), EPIC 7.2 (ActiveStrifeState singleton), EPIC 4 (Districts); Optional: EPIC 6 (Gate Screen tags)

---

## Overview

Each Strife card amplifies or mitigates 3 specific districts. This guide covers creating the 36 StrifeDistrictEffectSO assets (one per card-district pair), setting up the district entry/exit modifier pipeline, gate screen Strife tag display, and reward multiplier configuration. Players see Strife interaction tags on forward gates for informed routing decisions.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Expedition subscene | ActiveStrifeState (EPIC 7.2) | Active card identification |
| Expedition subscene | StrifeCardDatabase (EPIC 7.1) | Blob lookup for card interactions |
| District system | DistrictTransitionEvent (EPIC 4) | Triggers district entry/exit |
| Gate system | ForwardGateOption (EPIC 6.1) | Gate entities to tag |

### New Setup Required
1. Create 36 StrifeDistrictEffectSO assets
2. Reference each set of 3 from the parent StrifeCardDefinitionSO
3. Create StrifeDistrictModifier and StrifeGateTag components
4. Create StrifeDistrictEntrySystem, StrifeGateTagSystem, StrifeDistrictRewardSystem
5. Add StrifeDistrictActive to expedition-state authoring
6. Configure StrifeDistrictLiveTuning defaults

---

## 1. Create StrifeDistrictEffectSO Assets

**Create:** `Assets > Create > Hollowcore/Strife/District Effect`
**Recommended location:** `Assets/Data/Strife/DistrictEffects/`

### 1.1 Naming Convention

Pattern: `StrifeDistrict_[CardNumber]_[DistrictName].asset`

Example set for Succession War:
- `StrifeDistrict_01_Auction.asset`
- `StrifeDistrict_01_Garrison.asset`
- `StrifeDistrict_01_Bazaar.asset`

### 1.2 Inspector Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **SourceCard** | StrifeCardId enum | (must match parent card) | -- |
| **DistrictId** | Target district type ID | (must match a valid district) | -- |
| **InteractionType** | Amplify or Mitigate | Amplify | -- |
| **EffectDescription** | Full effect text (2-4 sentences) | "" | -- |
| **GateScreenTooltip** | Short text for gate card display | "" | max 80 chars |
| **ModifierDefinition** | RunModifierDefinitionSO reference | (required) | -- |
| **BonusRewardMultiplier** | Completion reward multiplier | 1.0 | 1.0-2.5 |
| **GrantsUniqueReward** | Whether completing grants a special item | false | -- |
| **UniqueRewardItemId** | Item ID for unique reward (0 = none) | 0 | -- |
| **OverrideParticleEffect** | VFX override (null = use card default) | null | -- |
| **MarkerTint** | Color for district Strife markers | white | -- |
| **InteractionIcon** | Small icon for gate card tag | (required) | -- |

**Tuning tip:** Keep `GateScreenTooltip` under 80 characters. It must fit on a single line within the gate card layout. Example: "Plague stacks 2x faster. Immunity on completion."

---

## 2. Reward Multiplier Guidelines

### 2.1 Amplify Interactions (Harder)

| Reward Multiplier | Difficulty Increase | Example |
|-------------------|-------------------|---------|
| 1.3x | Moderate increase | Quiet Crusade + Arsenal (mods disabled) |
| 1.4x-1.5x | Significant increase | Succession War + Garrison (double patrols) |
| 1.6x-1.8x | Major increase | Plague Armada + Quarantine (2x stacks, but grants immunity) |

**Tuning tip:** Amplify multipliers should always be >= 1.2x. If the difficulty increase is meaningful, the reward must compensate or players will always avoid amplified districts.

### 2.2 Mitigate Interactions (Easier/Relief)

| Reward Multiplier | Difficulty Change | Example |
|-------------------|------------------|---------|
| 1.0x | Full relief (no bonus) | Sovereign Raid + Bazaar (raiders avoid, safe zone) |
| 1.1x | Slight advantage + small bonus | Quiet Crusade + Deadwave (analog +25% damage) |

**Tuning tip:** Mitigate multipliers should stay at 1.0-1.2x. Over-rewarding easy content removes the Amplify risk/reward tension.

---

## 3. Complete Asset Creation Checklist

Create all 36 assets. Use this table as a creation checklist:

| # | Card | District | Type | Mult | Tooltip |
|---|------|----------|------|------|---------|
| 1 | Succession War | Auction | Amplify | 1.5x | "Auditors ambush on purchase. Loot +1 tier." |
| 2 | Succession War | Garrison | Amplify | 1.4x | "Double patrols. Military loot drops." |
| 3 | Succession War | Bazaar | Mitigate | 1.0x | "War refugees sell supplies at -30%." |
| 4 | Signal Schism | Cathedral | Amplify | 1.4x | "Hymn pulses wider, shorter interval." |
| 5 | Signal Schism | Neon Strip | Mitigate | 1.0x | "Ad noise reduces HUD hack frequency." |
| 6 | Signal Schism | Datawell | Amplify | 1.5x | "Data corruption shuffles item stats." |
| 7 | Plague Armada | Quarantine | Amplify | 1.8x | "2x plague stacks. Immunity on completion." |
| 8 | Plague Armada | Garden | Mitigate | 1.0x | "Herbs remove stacks. Entry clears 3." |
| 9 | Plague Armada | Clinic | Amplify | 1.3x | "30% chance healing adds plague stack." |
| 10 | Gravity Storm | Skyfall | Amplify | 1.6x | "Permanent low-gravity. Gravity Boots reward." |
| 11 | Gravity Storm | Foundry | Mitigate | 1.0x | "Gravity anchors negate float pockets." |
| 12 | Gravity Storm | Lattice | Amplify | 1.5x | "Moving gravity fields. Fall damage x2." |
| 13 | Quiet Crusade | Deadwave | Mitigate | 1.1x | "No penalty. Analog weapons +25% damage." |
| 14 | Quiet Crusade | Cathedral | Amplify | 1.4x | "Hymn pulses extend dead zones +3s." |
| 15 | Quiet Crusade | Arsenal | Amplify | 1.3x | "Active-ability weapon mods disabled." |
| 16 | Data Famine | Bazaar | Amplify | 1.5x | "20% vendor closure chance. Remaining -50%." |
| 17 | Data Famine | Datawell | Amplify | 1.4x | "Less intel/hack. Full-room reveals map." |
| 18 | Data Famine | Clinic | Mitigate | 1.0x | "Full vendor stock. Normal healing." |
| 19 | Black Budget | Garrison | Amplify | 1.5x | "Cameras, lasers, automated lockdowns." |
| 20 | Black Budget | Neon Strip | Mitigate | 1.0x | "Crowds block ambushes." |
| 21 | Black Budget | Auction | Amplify | 1.3x | "Black market items. Powerful but cursed." |
| 22 | Market Panic | Auction | Amplify | 1.6x | "Prices double. Rare items 3x more." |
| 23 | Market Panic | Bazaar | Amplify | 1.3x | "Flash sales every 30s. 5s window." |
| 24 | Market Panic | Foundry | Mitigate | 1.0x | "Stable economy. No loot volatility." |
| 25 | Memetic Wild | Cathedral | Amplify | 1.4x | "Hymn pulses carry memetic payloads." |
| 26 | Memetic Wild | Neon Strip | Amplify | 1.5x | "Ad holograms become thought-zone traps." |
| 27 | Memetic Wild | Datawell | Mitigate | 1.1x | "Hacking grants 30s zone immunity." |
| 28 | Nanoforge Bloom | Foundry | Amplify | 1.5x | "Surface shifts 2x speed. Double hazards." |
| 29 | Nanoforge Bloom | Garden | Amplify | 1.4x | "Vegetation attacks. Vines, spores, roots." |
| 30 | Nanoforge Bloom | Quarantine | Mitigate | 1.1x | "Containment slows shifts. Reassemble 6s." |
| 31 | Sovereign Raid | Garrison | Amplify | 1.7x | "Full-scale battles. Exceptional loot." |
| 32 | Sovereign Raid | Bazaar | Mitigate | 1.0x | "Raiders avoid bazaar. Safe restocking." |
| 33 | Sovereign Raid | Skyfall | Amplify | 1.5x | "Drop pod invasion. Falling debris." |
| 34 | Time Fracture | Datawell | Amplify | 1.4x | "25% hack reset. Double intel on success." |
| 35 | Time Fracture | Clinic | Amplify | 1.3x | "20% healing rewound after 5s." |
| 36 | Time Fracture | Deadwave | Mitigate | 1.0x | "Rewind pockets cannot form here." |

---

## 4. Link Effects to Parent Cards

After creating all 36 assets, open each `StrifeCardDefinitionSO` and assign its 3 district interactions:

1. Open `StrifeCard_01_SuccessionWar.asset`
2. In the **District Interactions** array (size 3):
   - Element 0: Set `DistrictId` = Auction ID, `InteractionType` = Amplify, `ModifierSetId` = hash, `BonusRewardMultiplier` = 1.5
   - Element 1: Set `DistrictId` = Garrison ID, `InteractionType` = Amplify, `ModifierSetId` = hash, `BonusRewardMultiplier` = 1.4
   - Element 2: Set `DistrictId` = Bazaar ID, `InteractionType` = Mitigate, `ModifierSetId` = hash, `BonusRewardMultiplier` = 1.0
3. Repeat for all 12 cards

---

## 5. StrifeDistrictLiveTuning

The `StrifeDistrictLiveTuning` singleton provides global scaling for all district interactions.

### 5.1 RunWorkstation Sliders

| Field | Label | Default | Min | Max |
|-------|-------|---------|-----|-----|
| GlobalRewardMultiplierScale | Reward Scale | 1.0 | 0.5 | 2.0 |
| GlobalDifficultyScale | Difficulty Scale | 1.0 | 0.5 | 2.0 |

- `GlobalRewardMultiplierScale` multiplies all `BonusRewardMultiplier` values. Set to 0.5 to halve all bonuses, 2.0 to double.
- `GlobalDifficultyScale` multiplies all modifier strength values for Amplify interactions.

**Tuning tip:** During early playtesting, set `GlobalDifficultyScale = 0.5` to preview interactions without overwhelming new testers. Ramp up to 1.0 once combat balance is stable.

---

## 6. Gate Screen Integration

### 6.1 StrifeGateTagSystem

When the gate screen opens, `StrifeGateTagSystem` checks each forward gate's destination district against the active card's 3 interactions:

| Gate Match | Tag Added | UI Display |
|-----------|-----------|------------|
| Amplify interaction | StrifeGateTag (red) | Red skull icon + "Strife Amplified" + tooltip |
| Mitigate interaction | StrifeGateTag (blue) | Blue shield icon + "Strife Mitigated" + tooltip |
| No interaction | No tag | No Strife indicator on gate card |

### 6.2 Gate Card UI Addition

Add to the `GateCard.prefab` (EPIC 6.1):

```
StrifeInteractionRow (hidden if no StrifeGateTag)
  +-- StrifeIcon (Image: skull or shield)
  +-- StrifeCardNameText ("Plague Armada")
  +-- InteractionLabel ("Amplified" or "Mitigated")
  +-- TooltipText (from StrifeDistrictEffectSO.GateScreenTooltip)
  +-- RewardBonusText ("+80% rewards" for Amplify, hidden for 1.0x)
```

---

## 7. Scene & Subscene Checklist

- [ ] 36 `StrifeDistrictEffectSO` assets in `Assets/Data/Strife/DistrictEffects/`
- [ ] Each of the 12 `StrifeCardDefinitionSO` assets references its 3 district effects
- [ ] `StrifeDistrictModifier.cs`, `StrifeGateTag.cs`, `StrifeDistrictActive.cs` in `Assets/Scripts/Strife/Components/`
- [ ] `StrifeDistrictEffectSO.cs` in `Assets/Scripts/Strife/Definitions/`
- [ ] `StrifeDistrictEntrySystem.cs`, `StrifeGateTagSystem.cs`, `StrifeDistrictRewardSystem.cs` in `Assets/Scripts/Strife/Systems/`
- [ ] `StrifeDistrictActive` (baked disabled) on expedition-state authoring
- [ ] `StrifeDistrictLiveTuning.cs` in `Assets/Scripts/Strife/Components/`
- [ ] Gate card UI has StrifeInteractionRow (EPIC 6.1 integration)

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| District effect count != 36 | Missing interactions; some districts never get Strife effects | Run `Hollowcore > Validation > Strife District Interactions` to find gaps |
| SourceCard on effect SO doesn't match parent card | Effect applies to wrong card; modifier appears under wrong Strife | Verify SourceCard field matches the parent StrifeCardDefinitionSO.CardId |
| ModifierDefinition not assigned | ModifierSetHash = 0; district modifier never applies | Assign a RunModifierDefinitionSO to every effect asset |
| GateScreenTooltip > 80 chars | Tooltip text overflows gate card layout | Shorten text; validation warns if too long |
| Amplify BonusRewardMultiplier < 1.0 | Players penalized for harder content (design intent violated) | Amplify must be >= 1.2x; validation flags this |
| Mitigate BonusRewardMultiplier > 1.5 | Easy content over-rewarded; removes Amplify risk/reward tension | Mitigate should be 1.0-1.2x; validation warns |
| StrifeDistrictActive not disabled at bake time | System thinks district interaction is always active | Verify baked as disabled IEnableableComponent |
| District-scoped modifiers not stripped on exit | Modifier effects leak into next district | StrifeDistrictEntrySystem must strip modifiers on DistrictTransitionEvent (exit) |
| Over-concentration: district referenced by > 3 cards | Player fatigue in that district (Strife always active there) | Run validation; check interaction matrix heatmap in Strife Workstation |

---

## Verification

- [ ] Entering a district with Strife interaction creates StrifeDistrictModifier
- [ ] StrifeDistrictActive enabled only while inside interacting district
- [ ] Modifiers injected on entry, stripped on exit (no leaking)
- [ ] Forward gates tagged with StrifeGateTag when destination has interaction
- [ ] Gates without interaction have no StrifeGateTag
- [ ] Amplify gates show red skull + tooltip
- [ ] Mitigate gates show blue shield + tooltip
- [ ] District completion rewards multiplied by BonusRewardMultiplier
- [ ] Unique rewards granted for qualifying interactions
- [ ] All 36 interactions represented in assets
- [ ] On card rotation, gate tags re-evaluated for new card
- [ ] Run `Hollowcore > Validation > Strife District Interactions` with zero errors
- [ ] Run `Hollowcore > Simulation > Strife District Interactions` for balance analysis
