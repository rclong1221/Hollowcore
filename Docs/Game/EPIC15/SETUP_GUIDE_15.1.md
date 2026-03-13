# EPIC 15.1 Setup Guide: Influence Meter System & Faction Contribution

**Status:** Planned
**Requires:** EPIC 4 (District Graph, district completion tracking); EPIC 14 (Boss System); EPIC 12 (Scar Map)

---

## Overview

Three influence meters -- Infrastructure, Transmission, and Market -- track which power faction the player has most disrupted during the expedition. Each district cleared contributes to one or two meters. The highest meter at expedition end determines which of three final bosses the player faces (The Architect, The Signal, or The Board). Players can see their influence balance on the Scar Map and Gate Screen, enabling strategic district selection.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Roguelite/ RunLifecycleSystem | Run state entity | Hosts InfluenceMeterState component |
| EPIC 14.5 | DistrictCompletionEvent | Triggers influence updates |
| EPIC 12 | Scar Map UI | Displays meter overlay |

### New Setup Required

1. Create `DistrictInfluenceMapSO` via `Assets > Create > Hollowcore/Boss/District Influence Map`.
2. Configure all 15 districts with primary/secondary faction contributions.
3. Add `InfluenceMeterState` and `InfluenceContribution` buffer to run state entity.
4. Create influence meter UI elements for Scar Map and Gate Screen.
5. Wire final boss selection to EPIC 15.2/15.3/15.4 encounter triggers.

---

## 1. Creating the District Influence Map

**Create:** `Assets > Create > Hollowcore/Boss/District Influence Map`
**Recommended location:** `Assets/Data/Boss/InfluenceMap.asset` (singleton -- one per project)

### 1.1 District-to-Influence Mapping

Configure the `Entries` list with one entry per district:

| District | Primary Faction | Primary Amt | Secondary Faction | Secondary Amt |
|----------|----------------|-------------|-------------------|---------------|
| Lattice | Infrastructure | 1.0 | -- | 0 |
| Burn | Infrastructure | 1.0 | -- | 0 |
| Auction | Infrastructure | 0.7 | Market | 0.5 |
| Chrome Cathedral | Transmission | 1.0 | -- | 0 |
| Nursery | Transmission | 1.0 | -- | 0 |
| Synapse Row | Transmission | 1.0 | -- | 0 |
| Wetmarket | Market | 1.0 | -- | 0 |
| Mirrortown | Market | 1.0 | -- | 0 |
| Necrospire | Transmission | 0.5 | Infrastructure | 0.5 |
| Glitch Quarter | Transmission | 0.7 | Market | 0.3 |
| Shoals | Market | 0.7 | Infrastructure | 0.3 |
| Quarantine | Infrastructure | 0.5 | Transmission | 0.5 |
| Old Growth | Infrastructure | 0.7 | Market | 0.3 |
| Deadwave | Transmission | 0.5 | Market | 0.5 |
| Skyfall | Infrastructure | 1.0 | -- | 0 |

### 1.2 Final Boss Threshold

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `MinDistrictsForFinalBoss` | Minimum districts cleared before final boss available | 5 | 3-10 |
| `MaxDistrictsForFinalBoss` | Maximum districts before final boss is forced | 7 | >= MinDistricts |

### 1.3 Per-Entry Fields

| Field | Description | Range |
|-------|-------------|-------|
| `DistrictId` | Integer ID from EPIC 13 | Must match district data |
| `DistrictName` | Display name | For editor clarity |
| `PrimaryFaction` | `InfluenceFaction` enum | Infrastructure, Transmission, or Market |
| `PrimaryAmount` | Influence contribution | (0, 5.0]. Warn if > 3.0 |
| `SecondaryFaction` | Optional second faction | Only if district overlaps two factions |
| `SecondaryAmount` | Secondary contribution | [0, 5.0]. 0 = no secondary |

**Tuning tip:** Dual-faction districts (Auction, Necrospire, etc.) create strategic ambiguity. Players clearing Auction push both Infrastructure and Market meters. Keep dual-faction contributions lower than pure-faction districts to maintain clear faction identity.

---

## 2. Tie-Breaking Rules

When two or more meters are tied at final boss selection:
1. The most recently cleared district's primary faction wins.
2. Stored in `InfluenceMeterState.LastClearedDistrictId`.
3. Resolved via `DistrictInfluenceMapSO.GetPrimaryFaction(LastClearedDistrictId)`.

**Tuning tip:** Ties are rare with the default contribution values but can occur with specific 5-district paths involving dual-faction districts. The Path Simulator in the Boss Workstation helps identify tie-prone paths.

---

## 3. Faction-to-Final Boss Mapping

| Faction | Final Boss | Triggered When |
|---------|-----------|----------------|
| Infrastructure | The Architect (EPIC 15.2) | Infrastructure meter highest |
| Transmission | The Signal (EPIC 15.3) | Transmission meter highest |
| Market | The Board (EPIC 15.4) | Market meter highest |

---

## 4. UI Integration

### 4.1 Scar Map Display

- Three colored meter bars: Infrastructure (orange), Transmission (blue), Market (green).
- Dominant faction highlighted with crown/star icon.
- Per-district nodes show contribution breakdown on hover.
- Contribution history timeline accessible.

### 4.2 Gate Screen Display

- Small meter preview showing current balance.
- Per-gate: projected influence change if that district is chosen next.
- "Trending toward: [Boss Name]" hint text.

---

## 5. Balance Validation

The Boss Workstation provides automated balance checking:

| Check | Target | Warning Threshold |
|-------|--------|-------------------|
| Faction reachability | Each faction reachable in 25-40% of random paths | Any faction < 15% or > 50% |
| Total influence per faction | Roughly balanced across all 15 districts | Any faction total < 50% or > 200% of another |
| All districts represented | All 15 in Entries list | Any missing district |

---

## Scene & Subscene Checklist

- [ ] DistrictInfluenceMapSO created with all 15 districts
- [ ] InfluenceMeterState added to run state entity (RunLifecycleSystem init)
- [ ] InfluenceContribution buffer added to run state entity
- [ ] InfluenceMapAuthoring on singleton prefab in run state subscene
- [ ] Scar Map UI: three meter bars with contribution overlay
- [ ] Gate Screen UI: meter preview with projected changes
- [ ] Final boss selection wired to EPIC 15.2/15.3/15.4 triggers

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| District missing from Entries | Validator warning, clearing that district adds no influence | Add entry for all 15 districts |
| PrimaryAmount = 0 | District contributes nothing to primary faction | Set > 0 |
| One faction unreachable | Validator error, players can never face that final boss | Rebalance contributions |
| Severe faction imbalance | One boss appears far more often than others | Use Path Simulator to check distribution |
| MinDistrictsForFinalBoss too high | Players never reach final boss in short runs | Set 5-7 range |
| Forgetting to update LastClearedDistrictId | Tie-breaking defaults to Infrastructure always | InfluenceUpdateSystem must update on each DistrictCompletionEvent |

---

## Verification

- [ ] InfluenceMeterState initializes to zero on expedition start
- [ ] District completion adds correct primary influence
- [ ] District completion adds correct secondary influence (e.g., Auction adds both)
- [ ] InfluenceContribution history buffer records each district
- [ ] GetDominantFaction returns correct faction for clear winner
- [ ] Tie-breaking uses LastClearedDistrictId primary faction
- [ ] Scar Map displays three meter bars with correct values
- [ ] Gate Screen shows projected influence changes for available districts
- [ ] "Trending toward" hint updates as meters change
- [ ] Final boss selection triggers at MinDistrictsForFinalBoss threshold
- [ ] Final boss selection matches dominant faction
- [ ] InfluenceMeterChangedEvent fires only on actual change
- [ ] Normalized meter values work for UI (0-1 range)
