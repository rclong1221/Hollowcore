# EPIC 7.2 Setup Guide: Strife Application System

**Status:** Planned
**Requires:** EPIC 7.1 (StrifeCardDefinitionSO, StrifeCardDatabase blob), Framework: Roguelite/RunModifierStack

---

## Overview

The Strife Application System selects a Strife card on expedition start, translates its effects into RunModifierStack entries, and handles card rotation in higher ascension loops. This guide covers setting up the ActiveStrifeState singleton, configuring the StrifeApplicationConfig, wiring modifier injection through the RunModifierStack pipeline, and live tuning integration.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Expedition subscene | StrifeCardDatabase (EPIC 7.1) | Blob database of all 12 cards |
| Roguelite framework | RunModifierStack / RunModifierApplySystem | Existing modifier pipeline |
| Expedition state entity | ExpeditionBootstrapSystem | Creates expedition state on start |

### New Setup Required
1. Create ActiveStrifeState + StrifeBossClauseReady components
2. Create StrifeStateAuthoring on the expedition-state prefab
3. Create the 4 Strife systems
4. Create StrifeLiveTuning defaults
5. Wire modifier bridge to RunModifierStack

---

## 1. Expedition State Prefab Setup

### 1.1 Add StrifeStateAuthoring

1. Open the **expedition-state prefab** (the entity that holds expedition-scoped singletons)
2. **Add Component > StrifeStateAuthoring**

### 1.2 StrifeStateAuthoring Inspector

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **DefaultRotationInterval** | Maps between card rotations in ascension loop 1+ | 2 | 0-6 |
| **Loop0RotationInterval** | Maps between rotations in first playthrough (0 = no rotation) | 0 | 0 (fixed) |

**What it bakes:**
- `ActiveStrifeState` singleton (CardId=None, UsedCardsMask=0, MapsSinceLastRotation=0)
- `StrifeBossClauseReady` (IEnableableComponent, baked **disabled**)
- `StrifeModifierTag` is NOT baked -- it is added dynamically to modifier entities

**Tuning tip:** Setting `DefaultRotationInterval` to 2 means the card changes every 2 completed maps in ascension loop 1+. Setting to 0 disables rotation entirely (single card for the whole expedition).

---

## 2. StrifeApplicationConfig

**Create:** `Assets > Create > Hollowcore/Strife/Application Config`
**Recommended location:** `Assets/Data/Strife/StrifeApplicationConfig.asset`

### 2.1 Inspector Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **RotationInterval_Loop0** | Card rotation interval for first playthrough | 0 | 0 (no rotation) |
| **RotationInterval_Loop1Plus** | Rotation interval for ascension loops 1+ | 2 | 1-6 |
| **AllowRepeatAfterExhaustion** | Reset used-card mask when all 12 used | true | true/false |

**Tuning tip:** For testing, set `RotationInterval_Loop1Plus = 1` to see card rotation every map. For release, 2 is the GDD-specified value.

---

## 3. System Setup

All systems go in `Assets/Scripts/Strife/Systems/`. Create the following files:

### 3.1 StrifeActivationSystem

**File:** `Assets/Scripts/Strife/Systems/StrifeActivationSystem.cs`
- **WorldSystemFilter:** ServerSimulation | LocalSimulation
- **UpdateInGroup:** InitializationSystemGroup
- **UpdateAfter:** ExpeditionBootstrapSystem

Runs once on expedition start when `ActiveStrifeState` singleton is created with `ActiveCardId == None`.

| Step | Action |
|------|--------|
| 1 | Read `ExpeditionSeed` from ActiveStrifeState |
| 2 | Create `Unity.Mathematics.Random(seed)` |
| 3 | Determine ascension loop -> set RotationInterval |
| 4 | Build candidate list: all 12 cards minus UsedCardsMask |
| 5 | Select card: `random.NextInt(candidateCount)` |
| 6 | Call `StrifeModifierBridge.ApplyCard()` |
| 7 | Store boss clause in StrifeBossClauseReady (enable it) |

### 3.2 StrifeRotationSystem

**File:** `Assets/Scripts/Strife/Systems/StrifeRotationSystem.cs`
- **WorldSystemFilter:** ServerSimulation | LocalSimulation
- **UpdateInGroup:** SimulationSystemGroup
- **UpdateAfter:** DistrictTransitionSystem (EPIC 4)

Triggered when `ActiveStrifeState.ShouldRotate` returns true.

| Step | Action |
|------|--------|
| 1 | Call `StrifeModifierBridge.RemoveCard(currentActiveCardId)` |
| 2 | Select new card from remaining pool |
| 3 | If all 12 used: reset UsedCardsMask |
| 4 | Call `StrifeModifierBridge.ApplyCard(newCardId)` |
| 5 | Update StrifeBossClauseReady with new clause |
| 6 | Reset MapsSinceLastRotation = 0 |
| 7 | Fire StrifeRotatedEvent for UI notification |

### 3.3 StrifeModifierBridge

**File:** `Assets/Scripts/Strife/Systems/StrifeModifierBridge.cs`
- **WorldSystemFilter:** ServerSimulation | LocalSimulation
- **UpdateInGroup:** SimulationSystemGroup
- **UpdateBefore:** RunModifierApplySystem

Translates Strife card data into RunModifierStack entries.

#### ApplyCard creates 5 modifier entities:

| # | Modifier | Scope | Source |
|---|----------|-------|--------|
| 1 | Map Rule | Global | blob.MapRuleModifierHash |
| 2 | Enemy Mutation | EnemyAI | blob.EnemyMutationModifierHash |
| 3 | District Interaction 1 | DistrictScope | interaction[0].ModifierSetHash |
| 4 | District Interaction 2 | DistrictScope | interaction[1].ModifierSetHash |
| 5 | District Interaction 3 | DistrictScope | interaction[2].ModifierSetHash |

All 5 entities get a `StrifeModifierTag { SourceCardId = cardId }` for cleanup on rotation.

#### RemoveCard destroys all entities matching `StrifeModifierTag.SourceCardId`.

### 3.4 StrifeEnemyMutationApplySystem

**File:** `Assets/Scripts/Strife/Systems/StrifeEnemyMutationApplySystem.cs`
- **WorldSystemFilter:** ServerSimulation | LocalSimulation
- **UpdateInGroup:** SimulationSystemGroup
- **UpdateAfter:** EnemySpawnSystem

Applies active Enemy Mutation AI parameter overrides to newly spawned enemies.

#### Mutation Parameter Reference

| Mutation | AI Parameter | Value |
|----------|-------------|-------|
| StrikeTeams | PackSize | 3 |
| SharedAwareness | AlertRadius | Room-wide |
| AdaptiveResistance | DamageTypeResistanceGain | 0.15 per hit, max 3 stacks |
| MobilityBursts | DashInterval | 6-10s random |
| EmpWeapons | OnHitCooldownDrain | 2s (4s melee) |
| TougherElites | EliteHPMultiplier / DamageMultiplier | 1.5x HP, 1.3x damage |
| Ambushers | CloakChance | 0.4 |
| MercSideSwaps | SideSwapChance | 0.15 |
| Reassemble | ReviveCount / ReviveHP | 1 revive, 0.4 HP |
| MixedFactions | FactionMixEnabled | true |
| ResetOnce | HPResetCount | 1 |

**Tuning tip:** These values are overridden at runtime by `StrifeLiveTuning` when present. Use the RunWorkstation sliders for rapid iteration without touching the SO.

---

## 4. StrifeLiveTuning Setup

The `StrifeLiveTuning` singleton is created automatically by `StrifeStateAuthoring` with defaults from the blob. It provides runtime-overridable values for the RunWorkstation (EPIC 23.7).

### 4.1 Enemy Mutation Sliders

| Field | Label | Default | Min | Max |
|-------|-------|---------|-----|-----|
| AdaptiveResistanceGain | Adaptive Resist Gain | 0.15 | 0.05 | 0.50 |
| AdaptiveResistanceMaxStacks | Resist Max Stacks | 3 | 1 | 10 |
| EliteHPMultiplier | Elite HP Mult | 1.5 | 1.0 | 3.0 |
| EliteDamageMultiplier | Elite Damage Mult | 1.3 | 1.0 | 2.5 |
| AmbusherCloakChance | Cloak Chance | 0.4 | 0.1 | 0.8 |
| MercSideSwapChance | Side Swap Chance | 0.15 | 0.05 | 0.5 |
| ReassembleHPFraction | Reassemble HP | 0.4 | 0.1 | 0.8 |
| EmpCooldownDrain | EMP Drain (sec) | 2.0 | 0.5 | 6.0 |

### 4.2 Map Rule Sliders

| Field | Label | Default | Min | Max |
|-------|-------|---------|-----|-----|
| LootScarcityMultiplier | Loot Scarcity | 0.6 | 0.2 | 1.0 |
| VendorStockMultiplier | Vendor Stock | 0.5 | 0.1 | 1.0 |
| PatrolDensityMultiplier | Patrol Density | 1.5 | 1.0 | 3.0 |

### 4.3 Boss Clause Sliders

| Field | Label | Default | Min | Max |
|-------|-------|---------|-----|-----|
| BossFewerAddsBonus | Boss Damage Bonus | 0.4 | 0.1 | 1.0 |
| BossCooldownTax | Cooldown Tax | 0.3 | 0.1 | 0.8 |

---

## 5. Rotation Timeline

```
Ascension Loop 0 (first playthrough):
  RotationInterval = 0 -> single card for entire expedition
  [Card A] ---------------------------------------------------------------->

Ascension Loop 1+:
  RotationInterval = 2 -> card rotates every 2 maps
  [Card A] -- Map 1 -- Map 2 --| [Card B] -- Map 3 -- Map 4 --| [Card C] ...
                                 ^ rotation                      ^ rotation
```

On rotation:
- Previous card modifiers fully stripped (5 entities destroyed)
- New card modifiers applied (5 new entities created)
- Boss clause updated to new card's clause
- No stacking of old + new effects

---

## 6. Scene & Subscene Checklist

- [ ] `StrifeStateAuthoring` on the expedition-state prefab
- [ ] `StrifeApplicationConfig.asset` at `Assets/Data/Strife/`
- [ ] `ActiveStrifeState.cs`, `StrifeModifierTag.cs`, `StrifeBossClauseReady.cs` in `Assets/Scripts/Strife/Components/`
- [ ] `StrifeActivationSystem.cs` in `Assets/Scripts/Strife/Systems/`
- [ ] `StrifeRotationSystem.cs` in `Assets/Scripts/Strife/Systems/`
- [ ] `StrifeModifierBridge.cs` in `Assets/Scripts/Strife/Systems/`
- [ ] `StrifeEnemyMutationApplySystem.cs` in `Assets/Scripts/Strife/Systems/`
- [ ] `StrifeLiveTuning.cs` in `Assets/Scripts/Strife/Components/`
- [ ] RunModifierStack framework supports `StrifeModifierTag`-tagged entries
- [ ] System ordering: StrifeActivationSystem after ExpeditionBootstrapSystem
- [ ] System ordering: StrifeRotationSystem after DistrictTransitionSystem
- [ ] System ordering: StrifeModifierBridge before RunModifierApplySystem

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| StrifeStateAuthoring missing from expedition prefab | No ActiveStrifeState singleton, systems never run | Add StrifeStateAuthoring to the expedition-state prefab |
| StrifeBossClauseReady not baked disabled | Boss clause applies immediately on expedition start before card selection | Verify authoring bakes it as disabled IEnableableComponent |
| Modifier entities not tagged with StrifeModifierTag | Card rotation cannot find and remove old modifiers | Ensure StrifeModifierBridge.ApplyCard adds the tag to every created modifier entity |
| StrifeActivationSystem runs before StrifeCardDatabase exists | NullReference reading blob | Ensure system ordering: after ExpeditionBootstrapSystem and after blob baking |
| Enemy mutation applied retroactively on rotation | Old enemies get new mutation behavior | StrifeEnemyMutationApplySystem only applies to newly spawned enemies (NewEnemyTag) |
| UsedCardsMask not reset on marathon runs | After 12 rotations, no cards available | Enable AllowRepeatAfterExhaustion in config |
| RunModifierApplySystem runs before StrifeModifierBridge | Strife modifiers not applied until next frame | Set StrifeModifierBridge to UpdateBefore RunModifierApplySystem |
| ActiveStrifeState not ghost-replicated | Clients don't know which card is active (UI blank) | Verify [GhostComponent(PrefabType = All)] on ActiveStrifeState |

---

## Verification

- [ ] ActiveStrifeState singleton created on expedition start with correct seed
- [ ] Deterministic: same seed always selects same card
- [ ] 5 RunModifier entities created with StrifeModifierTag on card activation
- [ ] Map Rule modifier active globally across all districts
- [ ] Enemy Mutation parameters applied to newly spawned enemies
- [ ] Boss Clause stored in StrifeBossClauseReady (enabled)
- [ ] Ascension loop 0: no rotation (single card)
- [ ] Ascension loop 1+: card rotates every 2 completed maps
- [ ] On rotation: old 5 modifiers destroyed, new 5 created, no stacking
- [ ] UsedCardsMask prevents repeat cards within expedition
- [ ] All 12 cards used -> mask resets
- [ ] ActiveStrifeState ghost-replicated to all clients
- [ ] Run `Hollowcore > Simulation > Strife Application` with all tests passing
