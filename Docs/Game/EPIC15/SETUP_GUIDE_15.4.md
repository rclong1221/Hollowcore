# EPIC 15.4 Setup Guide: The Board Boss & Budget/Contract System

**Status:** Planned
**Requires:** EPIC 15.1 (InfluenceMeterState, Market faction); EPIC 14 (Boss Definition, Variant Clauses, Arena System); Framework: Combat/, AI/

---

## Overview

The Board is the Market faction's final boss -- a collective of corporate executives in an impossible boardroom. The fight uses economic warfare: a visible budget resource funds mercenary waves, buyout attacks (that lock player abilities), and contract traps (that penalize specific actions). The Board is a multi-target boss with multiple members, each with their own health pool and specialty. Three phases progress from negotiation (deal offers that are traps), to hostile takeover (budget-driven combat), to liquidation (scorched earth as the arena burns). Clearing market districts enhances the Board's economic capabilities.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| InfluenceMeterState (15.1) | Market faction dominant | Triggers Board encounter |
| EPIC 14 Boss system | BossDefinitionSO, ArenaDefinitionSO | Base boss framework |
| AI/ framework | Boss behavior tree | Combat AI for each board member |

### New Setup Required

1. Create `BoardDefinitionSO` via `Assets > Create > Hollowcore/Boss/Final/The Board`.
2. Build the Impossible Boardroom arena subscene.
3. Create 3 board member sub-prefabs with individual health pools.
4. Place arena asset entities (SafeDeposit, MercContract, InsurancePolicy, GoldenParachute).
5. Create contract UI overlay and Budget display bar.
6. Configure budget economy (costs, regen rate).

---

## 1. Creating the Board Definition

**Create:** `Assets > Create > Hollowcore/Boss/Final/The Board`
**Location:** `Assets/Data/Boss/Final/TheBoard.asset`

### 1.1 Board Member Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `BoardMemberCount` | Number of targets (each with own HP) | 3 | Minimum 2 (multi-target is core mechanic) |
| `MemberHealth` | HP per member | 3000 | Total HP = MemberCount * MemberHealth. Warn if differs from BaseHealth by > 10% |
| `MemberProfiles` | Name, personality, preferred attack, specialty contract per member | -- | One profile per member |

### 1.2 Budget Economy

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `StartingBudget` | Initial budget at fight start | 100 | |
| `BudgetRegenRate` | Budget recovered per second (scales with members alive) | 2.0 | Effective regen = rate * (MembersAlive / TotalMembers) |
| `MercWaveCost` | Cost to summon a mercenary wave | 30 | BudgetRegen * 60s should not exceed MercWaveCost * 3 |
| `BuyoutCost` | Cost for a buyout attack (locks player ability) | 20 | |
| `ContractCost` | Cost to impose a contract trap | 15 | |

**Tuning tip:** The budget is visible to the player. This creates strategic reads: "They're about to afford another merc wave -- destroy that safe deposit!" Balance regen rate so the Board can use abilities regularly but not infinitely. Killing board members reduces regen, creating a power swing.

### 1.3 Phase Configuration

| Phase | Name | Threshold | Trigger | Key Mechanics |
|-------|------|-----------|---------|---------------|
| 0 (Negotiation) | "Terms of Service" | 1.0 | Start | Deal offers (contract traps), protected members |
| 1 (Takeover) | "Hostile Acquisition" | First member killed | Member death | Budget-driven attacks, mercs, buyouts |
| 2 (Liquidation) | "Total Liquidation" | One member remaining | Member death | Arena burns, direct combat, no economy |

### 1.4 Liquidation

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `LiquidationRate` | Fire expansion per second in Phase 3 | 0.04 | 1.0/rate >= 30s. Warn if < 30s (too fast) or > 120s (trivial) |

---

## 2. Contract System

### 2.1 Contract Types

| Type | Penalty | How to Break |
|------|---------|-------------|
| `MovementPenalty` (0) | Damage when taking certain paths ("Non-compete") | Destroy contract document entity |
| `AttackTax` (1) | Each player attack costs a small amount of health ("Revenue share") | Destroy contract document entity |
| `HealingBlock` (2) | Healing items locked for duration ("Exclusivity clause") | Destroy contract document entity or wait for expiry |
| `AreaRestriction` (3) | Damage when player leaves allowed arena section ("Zoning permit") | Destroy contract document entity |
| `TimeBomb` (4) | Massive damage if not broken within timer ("Deadline clause") | MUST destroy document before timer expires |

### 2.2 Contract Configuration

| Field | Description | Range |
|-------|-------------|-------|
| `TimeRemaining` | Duration before natural expiry | 5-60s. TimeBomb: warn if < 10s (unfair) or > 45s (trivial) |
| `PenaltyValue` | Damage per tick or effect magnitude | Per-type tuning |
| `ContractDocumentEntity` | Destroyable arena object | Must be reachable and visible |

**Tuning tip:** Contracts are the Board's signature mechanic. They should feel like puzzle pressure -- the player must decide between attacking the boss or breaking the contract. TimeBomb contracts create the most urgency and should be used sparingly.

---

## 3. Arena Assets

Destroyable objects that weaken the Board:

| Asset Type | Effect When Destroyed | Recommended Count |
|-----------|----------------------|-------------------|
| `SafeDeposit` (0) | Drains Board's Budget (BudgetDrainAmount) | 4-6 per phase |
| `MercContract` (1) | Prevents next mercenary wave | 2-3 |
| `InsurancePolicy` (2) | Strips board member defenses | 1 per member |
| `GoldenParachute` (3) | Must destroy to prevent member from self-healing | 1 per member |

---

## 4. Arena Construction: Impossible Boardroom

### 4.1 Required Entities

| Entity | Count | Purpose |
|--------|-------|---------|
| Central boardroom table | 1 | Board member positions |
| Board member positions | 3 | Seated positions for each member |
| SafeDeposit entities | 4-6 | Destructible. Scattered around room edges |
| MercContract entities | 2-3 | Destroyable documents on pedestals |
| InsurancePolicy entities | 3 | Glowing shields near board members |
| GoldenParachute entities | 3 | Escape pods behind each member |
| Contract document spawn points | 4-5 | Locations where imposed contracts materialize |
| Fire zone expansion volumes | -- | Phase 3 progressive fire coverage |

### 4.2 Board Member Sub-Prefabs

Each board member is a separate entity with:
- Own health pool (MemberHealth from SO)
- Preferred attack pattern (from MemberProfiles)
- Specialty contract type
- Visual distinction (unique model or color variant)
- Linked InsurancePolicy and GoldenParachute entities

---

## 5. District Enhancement Scaling

| Cleared District | Enhancement |
|-----------------|-------------|
| Auction | Budget regen rate +25%, higher quality mercs |
| Wetmarket | Contract penalties more severe |
| Mirrortown | Board members can "mirror" player attacks back (reflect 20% damage) |

---

## 6. Variant Clause Examples

| Clause Name | Trigger | Effect |
|------------|---------|--------|
| Market Monopoly | SideGoalSkipped (Auction) | Starting Budget doubled |
| Black Market Ties | SideGoalSkipped (Wetmarket) | Contracts cannot expire naturally (must be broken) |
| Mirror Defense | StrifeCard (Economic Pressure) | Board members reflect 20% of damage taken |
| Deep Reserves | FrontPhase >= 3 | +25% health, Budget regen +50% |
| Private Military | TraceLevel >= 4 | Merc waves include elite enemies |
| Market Insider Token | CounterToken | Disables one board member's specialty contract |

---

## Scene & Subscene Checklist

- [ ] BoardDefinitionSO created in `Assets/Data/Boss/Final/`
- [ ] Impossible Boardroom arena subscene built
- [ ] 3 board member sub-prefabs with individual health pools
- [ ] SafeDeposit, MercContract, InsurancePolicy, GoldenParachute entities placed
- [ ] Contract document spawn points positioned
- [ ] Fire zone expansion volumes for Phase 3
- [ ] Budget UI bar created (visible to player)
- [ ] Contract UI overlay created (active contracts with timers)
- [ ] Merc wave prefab created (3-5 generic enemies)
- [ ] Variant clauses created for market districts

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| BoardMemberCount < 2 | Validator error (multi-target is core mechanic) | Set to 2 or more |
| MemberHealth * MemberCount != BaseHealth | Validator warning (HP mismatch) | Align values within 10% tolerance |
| BudgetRegenRate too high | Board affords unlimited merc waves | Reduce regen; BudgetRegen*60 < MercWaveCost*3 |
| BudgetRegenRate too low | Board never uses abilities | Increase so Board affords 1 action per 10-15s |
| TimeBomb duration < 10s | Unfair, player cannot reach document in time | Set minimum 10s |
| LiquidationRate too high | Phase 3 ends in < 30s | Reduce rate so 1.0/rate >= 30 |
| PhaseAssetConfigs missing a phase | Validator error | Add config for all 3 BoardPhase values |
| No InsurancePolicy linked to member | Board member always has full defenses | Create and link InsurancePolicy per member |

---

## Verification

- [ ] Board spawns when Market is dominant faction
- [ ] Phase 1: Board members make deal offers with hidden penalties
- [ ] Accepting a deal imposes a contract with correct penalty
- [ ] Refusing a deal triggers telegraphed attack
- [ ] Destroying InsurancePolicy strips board member defenses
- [ ] Phase 1 to 2 transition when first member defeated
- [ ] Phase 2: Budget drives merc waves, buyouts, contracts
- [ ] Destroying SafeDeposit drains Budget
- [ ] Destroying MercContract prevents merc wave
- [ ] Buyout attack locks player ability for correct duration
- [ ] Contract penalties apply correctly per type
- [ ] Contract breaks when document destroyed OR timer expires
- [ ] TimeBomb contract deals massive damage if not broken in time
- [ ] Phase 2 to 3 transition when one member remains
- [ ] Phase 3: LiquidationProgress rises, fire zones expand
- [ ] Budget UI visible to player throughout fight
- [ ] Board member death reduces Budget regen rate
- [ ] District enhancement: clearing Auction boosts Budget regen
