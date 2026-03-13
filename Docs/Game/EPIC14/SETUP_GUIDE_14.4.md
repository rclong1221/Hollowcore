# EPIC 14.4 Setup Guide: Boss Counter Token System

**Status:** Planned
**Requires:** EPIC 14.1 (BossVariantClauseSO, ClauseId); Framework: Items/, Loot/; EPIC 10 (Side Goals), EPIC 13 (Districts)

---

## Overview

Counter tokens are special items found as side goal rewards in thematically linked districts. Each token targets a specific boss variant clause -- possessing the token at fight start disables that clause, making the boss easier. Some tokens are cross-district (found in District A, useful for District B's boss), rewarding players who plan their route. Tokens integrate with the Items/ framework and are consumed on boss fight entry.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| BossDefinitionSO (14.1) | Target boss with variant clauses | Token must reference a valid boss |
| BossVariantClauseSO (14.1) | Clause with CounterTokenClauseId > 0 | Token disables this clause |
| SideGoalDefinitionSO (EPIC 10) | Side goal that awards this token | Source for token acquisition |
| Items/ framework | Item system | Token is an inventory item entity |

### New Setup Required

1. Create `BossCounterTokenDefinitionSO` via `Assets > Create > Hollowcore/Boss/Counter Token`.
2. Configure target boss, clause to disable, and source side goal.
3. Register the token as an item in the Items/ framework.
4. Add the token to the side goal's reward table.
5. Create icon sprite in `Assets/Art/UI/Boss/Tokens/`.

---

## 1. Creating a Counter Token Asset

**Create:** `Assets > Create > Hollowcore/Boss/Counter Token`
**Recommended location:** `Assets/Data/Boss/Tokens/`
**Naming convention:** `Token_[BossName]_[Effect].asset` (e.g., `Token_GrandmotherNull_WardenOverride.asset`)

### 1.1 Identity Section

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `TokenId` | Unique integer identifier | 0 | Globally unique across all tokens |
| `DisplayName` | Inventory and UI display name | empty | Short and evocative: "Warden Override Key" |
| `Description` | What the token does gameplay-wise | empty | "Disables Warden reinforcements in Grandmother Null fight" |
| `FlavorText` | Lore text | empty | Optional world-building text |
| `Icon` | Inventory sprite | null | 64x64, assign from `Assets/Art/UI/Boss/Tokens/` |

### 1.2 Target Configuration

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `TargetBoss` | Reference to BossDefinitionSO | null | Must not be null (validator error) |
| `ClauseIdDisabled` | ClauseId on the target boss that this token disables | 0 | Must match a BossVariantClauseSO.CounterTokenClauseId on TargetBoss |

### 1.3 Source Configuration

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `SourceDistrictId` | Which district's side goal awards this token | 0 | District ID from EPIC 13 |
| `SourceSideGoalId` | Specific side goal ID from EPIC 10 | 0 | Must reference a valid SideGoalDefinitionSO (warn if not found yet) |
| `IsCrossDistrict` | Token found in different district than boss | false | Auto-validate: if true, SourceDistrictId must differ from TargetBoss's district |
| `ScarMapHint` | Hint text shown on Scar Map for cross-district tokens | empty | "This key affects a boss in Necrospire" |

### 1.4 Items Integration

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `ItemDefinitionId` | Item ID in the Items/ framework | 0 | Must be unique across all token definitions |

---

## 2. Cross-District Token Network

Cross-district tokens create strategic depth in expedition routing. Reference table:

| Token | Found In | Targets Boss | Disables Clause |
|-------|----------|-------------|-----------------|
| Warden Override Key | Necrospire | Grandmother Null | Warden reinforcements |
| Hymn Scrambler | Cathedral | Archbishop Algorithm | Hymn resonance attack |
| Slag Coolant | Burn | The Foreman | Slag pool expansion |
| Gravity Anchor | Skyfall | King of Heights | Gravity shift mechanic |
| Signal Jammer | Synapse | The Signal (final) | Possession attack |
| Flood Valve Key | Wetmarket | Leviathan Empress | Rising water speed |
| Mirror Shard | Mirrortown | Prime Reflection | Clone multiplication |
| Market Insider | Auction | The Board (final) | Merc wave summon |

**Tuning tip:** Ensure the cross-district flow is discoverable. The Scar Map hint should tell players which district the token affects without being too explicit about the mechanic.

---

## 3. Wiring Token Acquisition

### 3.1 Side Goal Reward Table

In the EPIC 10 side goal reward configuration:
1. Add a loot entry for the `BossCounterTokenDefinitionSO.ItemDefinitionId`.
2. Set the reward as guaranteed (not random roll).
3. `CounterTokenAwardSystem` listens for side goal completion events.

### 3.2 Token Pickup Prefab

If the token drops as a world pickup (not direct inventory grant):
1. Create a pickup prefab with `CounterTokenPickup` component.
2. Set `TokenDefinitionId` to match the `BossCounterTokenDefinitionSO.TokenId`.
3. Add visual mesh and interaction collider.
4. Place at designated reward location in the district subscene.

---

## 4. Boss Preview UI

The boss preview screen (accessible from Scar Map or Gate Screen) shows clause/token status:

| Element | Display |
|---------|---------|
| Active clause (no token) | Red icon, clause name, "ACTIVE" badge |
| Disabled clause (token in inventory) | Green icon, clause name, "DISABLED" badge with token icon |
| Missing token (clause could be disabled) | Gray icon, "Token available in [District]" hint |

---

## Scene & Subscene Checklist

- [ ] Counter token icon sprites created in `Assets/Art/UI/Boss/Tokens/`
- [ ] Token pickup prefab created (if using world pickup)
- [ ] Side goal reward tables updated with token references
- [ ] Boss preview UI panel shows clause/token status
- [ ] Scar Map hint displays for cross-district tokens
- [ ] Token registered as valid item in Items/ framework

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| TargetBoss is null | Validator error (orphaned token) | Assign a valid BossDefinitionSO |
| ClauseIdDisabled doesn't match any clause on TargetBoss | Token has no effect, validator error | Verify clause exists with matching CounterTokenClauseId |
| Duplicate ItemDefinitionId across tokens | Inventory system confusion | Ensure globally unique ItemDefinitionId |
| IsCrossDistrict = true but SourceDistrictId == target boss district | Validator warning (not actually cross-district) | Fix IsCrossDistrict flag or district assignment |
| Token consumed in wrong boss fight | Token disappears without effect | CounterTokenCheckSystem only consumes tokens where TargetBossId matches current boss |
| Boss drops its own counter token | Players get token after the fight they needed it for | Validator error: cross-district token pool must not include self-targeting tokens |

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
- [ ] Token does not consume if boss fight is not entered
- [ ] Multiple tokens for same boss all apply correctly
- [ ] Token for a different boss is NOT consumed in wrong fight
