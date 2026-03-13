# EPIC 7.1 Setup Guide: Strife Card Data Model

**Status:** Planned
**Requires:** None (definition-only sub-epic, no runtime dependencies)

---

## Overview

Defines all 12 Strife cards as ScriptableObject assets with their complete effect data: Map Rule, Enemy Mutation, Boss Clause, and 3 District Interactions each. Also bakes the Strife Card Database blob asset for Burst-compatible runtime access. This guide covers creating the folder structure, the 12 card SO assets, card art asset preparation, and the database authoring setup.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| None | -- | This is a standalone data definition sub-epic |

### New Setup Required
1. Create Strife folder structure and assembly definition
2. Create all enum and component files
3. Create the StrifeCardDefinitionSO script
4. Create 12 card SO assets with complete data
5. Create the StrifeCardDatabaseAuthoring for blob baking
6. Prepare card art and audio assets

---

## 1. Folder Structure

Create the following directory hierarchy:

```
Assets/Scripts/Strife/
  +-- Components/
  |     StrifeCardId.cs
  |     StrifeMapRule.cs
  |     StrifeEnemyMutation.cs
  |     StrifeBossClause.cs
  |     StrifeDistrictInteraction.cs
  |     StrifeCardBlob.cs
  +-- Definitions/
  |     StrifeCardDefinitionSO.cs
  +-- Systems/           (empty, populated by EPIC 7.2)
  +-- Authoring/
  |     StrifeCardDatabaseAuthoring.cs
  +-- Bridges/           (empty, populated by EPIC 7.4)
  +-- Debug/             (empty, populated by EPIC 7.2)

Assets/Data/Strife/
  +-- Cards/             (12 StrifeCardDefinitionSO assets)

Assets/Art/UI/Strife/
  +-- CardArt/           (12 card illustration sprites)
  +-- Icons/             (12 card icon sprites)
  +-- Borders/           (12 UI border sprites)

Assets/Prefabs/VFX/Strife/   (12 particle effect prefabs)

Assets/Audio/Strife/
  +-- Ambient/           (12 ambient audio clips)
  +-- Stings/            (12 reveal sting clips)
```

### 1.1 Assembly Definition

**Create:** `Assets/Scripts/Strife/Hollowcore.Strife.asmdef`

References:
- `DIG.Shared`
- `Unity.Entities`
- `Unity.NetCode`
- `Unity.Collections`
- `Unity.Burst`
- `Unity.Mathematics`

---

## 2. Create StrifeCardDefinitionSO Assets

**Create:** `Assets > Create > Hollowcore/Strife/Card Definition`
**Recommended location:** `Assets/Data/Strife/Cards/`

### 2.1 Naming Convention

| # | CardId | Asset Name |
|---|--------|------------|
| 1 | SuccessionWar | `StrifeCard_01_SuccessionWar.asset` |
| 2 | SignalSchism | `StrifeCard_02_SignalSchism.asset` |
| 3 | PlagueArmada | `StrifeCard_03_PlagueArmada.asset` |
| 4 | GravityStorm | `StrifeCard_04_GravityStorm.asset` |
| 5 | QuietCrusade | `StrifeCard_05_QuietCrusade.asset` |
| 6 | DataFamine | `StrifeCard_06_DataFamine.asset` |
| 7 | BlackBudget | `StrifeCard_07_BlackBudget.asset` |
| 8 | MarketPanic | `StrifeCard_08_MarketPanic.asset` |
| 9 | MemeticWild | `StrifeCard_09_MemeticWild.asset` |
| 10 | NanoforgeBloom | `StrifeCard_10_NanoforgeBloom.asset` |
| 11 | SovereignRaid | `StrifeCard_11_SovereignRaid.asset` |
| 12 | TimeFracture | `StrifeCard_12_TimeFracture.asset` |

### 2.2 Inspector Field Reference

#### Identity Section

| Field | Description | Example (Succession War) |
|-------|-------------|--------------------------|
| **CardId** | Enum value | SuccessionWar |
| **DisplayName** | Human-readable name | "Succession War" |
| **FlavorText** | Lore/atmosphere text | "The old admiral's death left a power vacuum..." |
| **MechanicalDescription** | Player-facing effect summary | "Faction crossfire events. Enemies in strike teams. Boss buys reinforcements." |
| **Icon** | Small icon sprite (64x64) | Assign from `Assets/Art/UI/Strife/Icons/` |
| **CardArt** | Full card illustration (512x768) | Assign from `Assets/Art/UI/Strife/CardArt/` |

#### Map Rule Section

| Field | Description | Example |
|-------|-------------|---------|
| **MapRule** | Enum value | CrossfireEvents |
| **MapRuleDescription** | Effect description | "Random NPC faction skirmishes erupt in rooms." |
| **MapRuleModifierDef** | RunModifierDefinitionSO reference | Assign from `Assets/Data/Modifiers/` |

#### Enemy Mutation Section

| Field | Description | Example |
|-------|-------------|---------|
| **EnemyMutation** | Enum value | StrikeTeams |
| **EnemyMutationDescription** | Effect description | "Enemies attack in coordinated 3-packs." |
| **EnemyMutationModifierDef** | RunModifierDefinitionSO reference | Assign from `Assets/Data/Modifiers/` |

#### Boss Clause Section

| Field | Description | Example |
|-------|-------------|---------|
| **BossClause** | Enum value | Reinforcements |
| **BossClauseDescription** | Effect description | "Boss buys reinforcement waves with phase damage." |
| **BossClauseModifierDef** | RunModifierDefinitionSO reference | Assign from `Assets/Data/Modifiers/` |

#### District Interactions Section (Exactly 3)

Each entry:

| Field | Description | Example (Succession War + Auction) |
|-------|-------------|-------------------------------------|
| **DistrictId** | Target district type ID | (Auction district ID) |
| **EffectDescription** | Full effect text | "Auditors hyperactive, purchases trigger ambush, prices +20%, loot +1 tier" |
| **InteractionType** | Amplify or Mitigate | Amplify |
| **ModifierSetId** | RunModifierDefinitionSO hash | (from modifier SO) |
| **BonusRewardMultiplier** | Completion reward multiplier | 1.5 |

#### Visual Identity Section

| Field | Description | Example (Succession War) |
|-------|-------------|--------------------------|
| **ThemeColor** | Card theme color (linear) | #FF4444 (Crimson) |
| **ParticleEffectPrefab** | VFX prefab | Sparks + bullet tracers prefab |
| **UIBorderSprite** | Border sprite | Jagged metallic border |

#### Audio Section

| Field | Description | Notes |
|-------|-------------|-------|
| **AmbientLayer** | Looping ambient clip | Non-positional, -6dB below master |
| **RevealSting** | One-shot on card reveal | 1-3 seconds, high impact |

---

## 3. Complete Card Data Table

All 12 cards with their complete effect assignments:

| # | Card | MapRule | EnemyMutation | BossClause | ThemeColor |
|---|------|---------|---------------|------------|------------|
| 1 | Succession War | CrossfireEvents | StrikeTeams | Reinforcements | #FF4444 |
| 2 | Signal Schism | HudHacks | SharedAwareness | SystemPossession | #00FFCC |
| 3 | Plague Armada | InfectionClouds | AdaptiveResistance | AdaptiveSkin | #88FF00 |
| 4 | Gravity Storm | FloatPockets | MobilityBursts | GravityShifts | #AA66FF |
| 5 | Quiet Crusade | DeadZones | EmpWeapons | CooldownTax | #888888 |
| 6 | Data Famine | LootScarcity | TougherElites | FewerAddsDeadlier | #FF8800 |
| 7 | Black Budget | StealthRoutes | Ambushers | PreparedDefenses | #222244 |
| 8 | Market Panic | LootVolatility | MercSideSwaps | BuyoutDeals | #FFD700 |
| 9 | Memetic Wild | ThoughtZones | StatusViaAudioVisual | FakePhases | #FF00FF |
| 10 | Nanoforge Bloom | SurfaceReconfigure | Reassemble | RegenNodes | #00FF88 |
| 11 | Sovereign Raid | ThirdPartyRaiders | MixedFactions | RaidInterruption | #FF6600 |
| 12 | Time Fracture | RewindPockets | ResetOnce | PhaseRewind | #4488FF |

---

## 4. District Interaction Quick-Reference

Each card has exactly 3 district interactions (2 Amplify + 1 Mitigate is typical):

| Card | District 1 (Type) | District 2 (Type) | District 3 (Type) |
|------|-------------------|-------------------|-------------------|
| Succession War | Auction (Amplify, 1.5x) | Garrison (Amplify, 1.4x) | Bazaar (Mitigate, 1.0x) |
| Signal Schism | Cathedral (Amplify, 1.4x) | Neon Strip (Mitigate, 1.0x) | Datawell (Amplify, 1.5x) |
| Plague Armada | Quarantine (Amplify, 1.8x) | Garden (Mitigate, 1.0x) | Clinic (Amplify, 1.3x) |
| Gravity Storm | Skyfall (Amplify, 1.6x) | Foundry (Mitigate, 1.0x) | Lattice (Amplify, 1.5x) |
| Quiet Crusade | Deadwave (Mitigate, 1.1x) | Cathedral (Amplify, 1.4x) | Arsenal (Amplify, 1.3x) |
| Data Famine | Bazaar (Amplify, 1.5x) | Datawell (Amplify, 1.4x) | Clinic (Mitigate, 1.0x) |
| Black Budget | Garrison (Amplify, 1.5x) | Neon Strip (Mitigate, 1.0x) | Auction (Amplify, 1.3x) |
| Market Panic | Auction (Amplify, 1.6x) | Bazaar (Amplify, 1.3x) | Foundry (Mitigate, 1.0x) |
| Memetic Wild | Cathedral (Amplify, 1.4x) | Neon Strip (Amplify, 1.5x) | Datawell (Mitigate, 1.1x) |
| Nanoforge Bloom | Foundry (Amplify, 1.5x) | Garden (Amplify, 1.4x) | Quarantine (Mitigate, 1.1x) |
| Sovereign Raid | Garrison (Amplify, 1.7x) | Bazaar (Mitigate, 1.0x) | Skyfall (Amplify, 1.5x) |
| Time Fracture | Datawell (Amplify, 1.4x) | Clinic (Amplify, 1.3x) | Deadwave (Mitigate, 1.0x) |

---

## 5. Card Art Asset Specifications

### 5.1 Sprite Requirements

| Asset Type | Dimensions | Format | Location |
|-----------|-----------|--------|----------|
| Card Art | 512x768 px | PNG, RGBA | `Assets/Art/UI/Strife/CardArt/` |
| Card Icon | 64x64 px | PNG, RGBA | `Assets/Art/UI/Strife/Icons/` |
| UI Border | 128x128 px (9-slice) | PNG, RGBA | `Assets/Art/UI/Strife/Borders/` |

### 5.2 Naming Convention

- Card Art: `StrifeArt_01_SuccessionWar.png` through `StrifeArt_12_TimeFracture.png`
- Icons: `StrifeIcon_01_SuccessionWar.png` through `StrifeIcon_12_TimeFracture.png`
- Borders: `StrifeBorder_01_SuccessionWar.png` through `StrifeBorder_12_TimeFracture.png`

### 5.3 Audio Requirements

| Asset Type | Format | Duration | Location |
|-----------|--------|----------|----------|
| Ambient Layer | WAV/OGG, looping | 30-60s loop | `Assets/Audio/Strife/Ambient/` |
| Reveal Sting | WAV/OGG, one-shot | 1-3s | `Assets/Audio/Strife/Stings/` |

---

## 6. StrifeCardDatabase Authoring

### 6.1 Setup

1. In the expedition subscene, create a GameObject named `StrifeCardDatabase`
2. **Add Component > StrifeCardDatabaseAuthoring**
3. Assign all 12 `StrifeCardDefinitionSO` assets to the **Cards** array (order must match `StrifeCardId` enum: index 0 = SuccessionWar through index 11 = TimeFracture)

### 6.2 What It Bakes

The authoring bakes:
- `StrifeCardDatabase` singleton with `BlobAssetReference<StrifeCardDatabaseBlob>`
- Blob contains all 12 `StrifeCardBlob` entries indexed by `(StrifeCardId - 1)`
- Each blob entry stores: CardId, MapRule, EnemyMutation, BossClause, 3 DistrictInteractions, modifier hashes

### 6.3 Verify Baking

After entering Play Mode:
- Check ECS EntityDebugger for `StrifeCardDatabase` singleton entity
- Verify blob has 12 card entries
- Verify each entry's `DistrictInteractions` array has length 3

---

## 7. Scene & Subscene Checklist

- [ ] `Hollowcore.Strife.asmdef` at `Assets/Scripts/Strife/`
- [ ] All 5 enum files in `Assets/Scripts/Strife/Components/`
- [ ] `StrifeCardDefinitionSO.cs` in `Assets/Scripts/Strife/Definitions/`
- [ ] `StrifeCardBlob.cs` in `Assets/Scripts/Strife/Components/`
- [ ] `StrifeCardDatabaseAuthoring.cs` in `Assets/Scripts/Strife/Authoring/`
- [ ] 12 card SO assets in `Assets/Data/Strife/Cards/` with complete data
- [ ] StrifeCardDatabaseAuthoring in expedition subscene with all 12 cards assigned
- [ ] Each card SO has: CardId set, MapRule set, EnemyMutation set, BossClause set, 3 DistrictInteractions
- [ ] Each card SO has: Icon, CardArt assigned (placeholder OK for initial setup)
- [ ] No new components added to the player entity (all Strife data is singleton/expedition-scoped)

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Card array order wrong in database authoring | Blob index mismatch: CardId 3 returns card 5 data | Ensure array order matches StrifeCardId enum (1-indexed: index 0 = SuccessionWar) |
| District interaction count != 3 | Validation error, blob array index out of bounds | Every card must have exactly 3 entries in DistrictInteractions |
| ThemeColor left as default white | All cards look identical in UI | Set unique theme color per card (see table in Section 3) |
| MapRuleModifierDef not assigned | Modifier hash = 0, map rule never applies at runtime | Assign a RunModifierDefinitionSO for each effect |
| Duplicate CardId across SOs | Database contains duplicate entries, one card unreachable | Run `Hollowcore > Validation > Strife Card Definitions` to detect |
| Enum values do not match 1:1 | StrifeMapRule count != StrifeCardId count | Each enum must have exactly 12 entries (plus None=0) |
| Ambient audio clip not set to loop | Audio plays once and stops | Set AudioClip import settings to Loop in the Inspector |

---

## Verification

- [ ] All 12 `StrifeCardId` enum values compile
- [ ] All 12 `StrifeMapRule`, `StrifeEnemyMutation`, `StrifeBossClause` enums compile and align 1:1
- [ ] 12 card SO assets exist with unique CardId values
- [ ] Each card has exactly 3 DistrictInteractions
- [ ] Each card has non-None MapRule, EnemyMutation, BossClause
- [ ] StrifeCardDatabase singleton bakes successfully
- [ ] Blob database indexed by `(StrifeCardId - 1)` returns correct card for all 12 IDs
- [ ] Run `Hollowcore > Validation > Strife Card Definitions` with zero errors
- [ ] Run `Hollowcore > Simulation > Strife Card Balance` to check difficulty distribution
