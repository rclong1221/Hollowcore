# EPIC 16.14: Player Progression & XP System

**Status:** PLANNED
**Priority:** High (Core RPG Loop Foundation)
**Dependencies:**
- `CharacterAttributes` IComponentData with `Level` field (existing -- `DIG.Combat.Components`, ghost-replicated `[GhostField]`, 20 bytes)
- `CharacterAttributesAuthoring` MonoBehaviour + Baker (existing -- `DIG.Combat.Authoring`)
- `CombatStatProfile` ScriptableObject with `GetAttackStatsForLevel(int)` / `GetDefenseStatsForLevel(int)` (existing -- `DIG.Combat.Definitions`, infrastructure ready but unused at runtime)
- `AttackStats` / `DefenseStats` / `ElementalResistances` IComponentData (existing -- `DIG.Combat.Components`, all Ghost:All, quantized floats)
- `KillCredited` IComponentData event (existing -- `Player.Components`, ephemeral on killer entity, created by `DeathTransitionSystem` via EndSimulationECB at line 174)
- `DiedEvent` IEnableableComponent (existing -- `Player.Components`, enabled by DeathTransitionSystem)
- `LootContext.Level` int field (existing -- `DIG.Loot.Systems`, currently hardcoded to `1` with TODO comment)
- `LootTableCondition.MinLevel / MaxLevel` (existing -- `DIG.Loot.Definitions`, condition types ready but always match level 1)
- `DeathLootSystem` (existing -- `DIG.Loot.Systems`, reads DiedEvent, creates PendingLootSpawn)
- `Health.Max` (existing -- `Player.Components`, ghost-replicated)
- `ResourcePool` / `ResourcePoolBase` (existing -- `DIG.Combat.Resources`, EPIC 16.8, 80 bytes AllPredicted)
- `ResourceModifierApplySystem` (existing -- `DIG.Combat.Resources`, applies equipment bonuses to resource pools)
- `PlayerEquippedStats` (existing -- `DIG.Items`, EPIC 16.6, 32 bytes on player)
- `ItemStatBlock` (existing -- `DIG.Items`, EPIC 16.6, per-item stat modifiers on ITEM entities)
- `EquippedStatsSystem` (existing -- `DIG.Items`, aggregates ItemStatBlock from equipped items each frame)
- `CurrencyInventory` (existing -- `DIG.Economy`, EPIC 16.6, Gold/Premium/Crafting)
- `CombatUIBridgeSystem` / `ResourceUIBridgeSystem` (existing -- managed bridge pattern reference)
- `DamageVisualQueue` (existing -- static NativeQueue bridge for ECS → UI events)
- `ItemRegistryBootstrapSystem` (existing -- bootstrap singleton pattern reference)
- `SurfaceGameplayConfigSystem` (existing -- BlobAsset bootstrap pattern reference: loads SO, builds BlobAsset via BlobBuilder, creates singleton, self-disables)

**Feature:** A server-authoritative player progression system providing XP accumulation from multiple sources (kills, quests, crafting, exploration), data-driven leveling curves with diminishing returns, per-level stat scaling, level rewards (recipes, abilities, content gates), stat point allocation, rested XP, equipment XP bonuses, and multiplayer-safe level-up events. Reuses existing `CharacterAttributes.Level` -- zero new ghost components needed for level data.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `CharacterAttributes` (Str/Dex/Int/Vit/Level) | `CombatStatComponents.cs` | Fully implemented, Ghost:All | Level = 1 everywhere (nothing writes it) |
| `CombatStatProfile` SO | `CombatStatProfile.cs` | Infrastructure ready | `GetAttackStatsForLevel(level)` method exists with `BaseAttackPower + AttackPowerPerLevel * (level - 1)` formula. **Nothing calls it at runtime.** |
| `AttackStats` / `DefenseStats` | `CombatStatComponents.cs` | Fully implemented, Ghost:All | Quantized floats, not recalculated on level change |
| `ElementalResistances` | `CombatStatComponents.cs` | Fully implemented, Ghost:All | 8 elemental types |
| `PlayerEquippedStats` | `PlayerEquippedStats.cs` | Fully implemented | 32 bytes, aggregated by EquippedStatsSystem every frame |
| `ItemStatBlock` | `ItemStatBlock.cs` | Fully implemented | Per-item stats on ITEM entities (not player) |
| `EquippedStatsSystem` | `EquippedStatsSystem.cs` | Fully implemented | Sums ItemStatBlock from 2-3 equipped items |
| `ResourcePool` / `ResourceModifierApplySystem` | `DIG.Combat.Resources` | Fully implemented | 80 bytes AllPredicted, Burst regen/decay |
| `DeathLootSystem` | `DeathLootSystem.cs` | Fully implemented | `Level = 1, // TODO: pull from enemy level component when available` |
| `KillCredited` event | `DeathTransitionSystem.cs:174` | Fully implemented | Created via EndSimulationECB — XPAwardSystem must run AFTER ECB playback |
| `LootTableCondition.MinLevel/MaxLevel` | `LootTableResolver.cs` | Ready | Always matches because level is always 1 |

### What's Missing

- **No XP accumulation** — no system reads `KillCredited` to award XP
- **No level-up logic** — `CharacterAttributes.Level` is permanently 1
- **No stat scaling at runtime** — `CombatStatProfile.GetAttackStatsForLevel()` never called
- **No level rewards** — no ability/recipe/content unlocks
- **No stat point allocation** — no UI or system for spending stat points
- **No XP UI** — no XP bar, level-up popup, XP gain floating numbers
- **No rested XP** — no offline bonus accumulation
- **No editor tooling** — no XP curve visualizer, balance tools, or play-mode progression inspector

---

## Problem

DIG has combat stats, item affixes, resource pools, loot tables with level conditions, and a `CombatStatProfile` with per-level scaling formulas -- but there is no XP source, no XP pool on the player, no level-up transition, and `CharacterAttributes.Level` is permanently 1 because nothing writes to it. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `CharacterAttributes.Level` on player entity (ghost-replicated) | No system ever writes Level (always 1) |
| `CombatStatProfile.GetAttackStatsForLevel(int)` per-level scaling | No system recalculates stats when level changes |
| `KillCredited` event on killer entity after enemy death | No system reads kills to award XP |
| `LootContext.Level` in loot resolution | Hardcoded to 1 in `DeathLootSystem` (TODO comment) |
| `LootTableCondition.MinLevel / MaxLevel` condition types | Never evaluated against real player level |
| `AttackStats`, `DefenseStats` on entities | Not recalculated on level change |
| `Health.Max` on player | Not scaled by level |
| `ResourcePool.Slot*.Max` / `RegenRate` on player | Not scaled by level |
| `DamageVisualQueue` static queue bridge for UI | No XP/level-up visual event pipeline |

**The gap:** A player kills 50 enemies and remains level 1. There is no power curve, no progression loop. Loot tables cannot gate content by level because level never changes. Designers cannot create "reach level 10 to unlock this recipe" gates.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  ProgressionCurveSO     LevelRewardsSO     LevelStatScalingSO
  (XP per level,         (Per-level          (Base stats per
   max level, diminish)   unlock list)        level: HP, ATK, DEF)
           |                    |                    |
           └────── ProgressionBootstrapSystem ───────┘
                   (loads from Resources/, builds BlobAssets,
                    creates ProgressionConfigSingleton,
                    follows SurfaceGameplayConfigSystem pattern)
                              |
                    ECS DATA LAYER
  PlayerProgression          CharacterAttributes.Level
  (CurrentXP, StatPoints)    (EXISTING, ghost-replicated)
           |                           |
  ProgressionConfigSingleton    LevelStatScalingSingleton
  (BlobRef to curve data)       (BlobRef to per-level stats)
                              |
                    SYSTEM PIPELINE (SimulationSystemGroup, Server|Local)
                              |
  XPAwardSystem ─── reads KillCredited + enemy level
  (computes XP with diminishing returns + gear bonus + rested XP)
           |
  LevelUpSystem (Burst) ─── checks CurrentXP >= threshold
  (increments CharacterAttributes.Level, fires LevelUpEvent)
           |
  LevelRewardSystem ─── processes level rewards
  (gold, recipe unlocks, ability unlocks, bonus stat points)
           |
  StatAllocationSystem ─── processes stat point spend requests
  (validates points available, modifies CharacterAttributes)
           |
  LevelStatScalingSystem (Burst) ─── recalculates base stats
  (AttackStats, DefenseStats, Health.Max, ResourcePool.Max)
  [UpdateAfter EquippedStatsSystem]
  [UpdateBefore ResourceModifierApplySystem]
                              |
                    PRESENTATION LAYER (PresentationSystemGroup)
                              |
  ProgressionUIBridgeSystem → ProgressionUIRegistry → IProgressionUIProvider
  (managed, reads local player data, dequeues LevelUpVisualQueue)
           |
  XPBarView / LevelUpPopupView / StatAllocationView (MonoBehaviours)
```

### Data Flow (Kill Enemy → XP → Level Up → Stat Scaling)

```
Frame N (Server):
  1. DeathTransitionSystem: Enemy dies, fires DiedEvent + KillCredited on killer via EndSimulationECB

Frame N+1 (Server):
  2. XPAwardSystem: Reads KillCredited (exists after ECB playback)
     - Lookup enemy CharacterAttributes.Level
     - rawXP = BaseKillXP * pow(KillXPPerEnemyLevel, enemyLevel - 1)
     - Apply diminishing returns: XP *= DiminishFactor(playerLevel - enemyLevel)
     - Apply rested bonus: XP *= (1 + RestedXPMultiplier) if rested pool > 0
     - Apply gear bonus: XP *= (1 + PlayerEquippedStats.TotalXPBonusPercent)
     - Write PlayerProgression.CurrentXP += finalXP
     - Deduct from rested pool if used
     - Enqueue XP gain event to LevelUpVisualQueue

Frame N+2 (Server):
  3. LevelUpSystem (Burst): Reads CurrentXP
     - While CurrentXP >= XPPerLevel[currentLevel - 1] && level < MaxLevel:
       - CurrentXP -= threshold (carry excess)
       - CharacterAttributes.Level++ (ghost-replicated)
       - UnspentStatPoints += StatPointsPerLevel
       - Enable LevelUpEvent (transient flag)

  4. LevelRewardSystem: Reads LevelUpEvent
     - Award gold, recipe unlocks, ability unlocks per LevelRewardsBlob
     - Enqueue level-up visual event

  5. LevelStatScalingSystem (Burst): Reads CharacterAttributes.Level
     - AttackStats = baseStats[level] + equipmentBonus
     - DefenseStats = baseStats[level] + equipmentBonus
     - Health.Max = baseHealth[level] + equipmentBonus
     - ResourcePool base values updated

Frame N+2 (Client):
  6. ProgressionUIBridgeSystem: Reads PlayerProgression
     - Push XP/Level data to UI registry
     - Dequeue visual events for popup/animations
```

### Critical System Ordering Chain

```
DeathTransitionSystem (EndSimulationECB)
    ↓ (next frame, ECB playback)
XPAwardSystem [UpdateAfter(typeof(DeathTransitionSystem))]
    ↓
LevelUpSystem (Burst) [UpdateAfter(typeof(XPAwardSystem))]
    ↓
LevelRewardSystem [UpdateAfter(typeof(LevelUpSystem))]
    ↓
StatAllocationSystem [UpdateAfter(typeof(LevelRewardSystem))]
    ↓
EquippedStatsSystem (existing, unchanged)
    ↓
LevelStatScalingSystem (Burst) [UpdateAfter(typeof(EquippedStatsSystem))]
    ↓
ResourceModifierApplySystem (existing, unchanged) [UpdateAfter(typeof(LevelStatScalingSystem))]
```

---

## ECS Components

### On Player Entity

**File:** `Assets/Scripts/Progression/Components/PlayerProgression.cs`

```
PlayerProgression (IComponentData, Ghost:AllPredicted)
  CurrentXP        : int      // XP toward next level (resets on level-up, excess carries)
  TotalXPEarned    : int      // Lifetime XP (never decreases, for stats/achievements)
  UnspentStatPoints: int      // From level-ups, for stat allocation
  RestedXP         : float    // Bonus XP pool (depleted as kills award XP, accumulates offline)
```

**Archetype impact:** 16 bytes. Combined with existing `CharacterAttributes` (20 bytes), total progression footprint is 36 bytes.

**File:** `Assets/Scripts/Progression/Components/LevelUpEvent.cs`

```
LevelUpEvent (IComponentData + IEnableableComponent, Ghost:AllPredicted)
  NewLevel      : int
  PreviousLevel : int
```

Baked disabled by `ProgressionAuthoring`. Same pattern as `DiedEvent` — LevelUpSystem enables it, consuming systems disable it. Zero structural change to toggle.

**File:** `Assets/Scripts/Progression/Components/XPSourceType.cs`

```
XPSourceType enum: Kill(0), Quest(1), Crafting(2), Exploration(3), Interaction(4), Bonus(5)
```

**File:** `Assets/Scripts/Progression/Components/StatAllocationRequest.cs`

```
StatAllocationRequest (IBufferElementData, Capacity=4, NOT ghost-replicated)
  Attribute : StatAttributeType enum (Strength=0, Dexterity=1, Intelligence=2, Vitality=3)
  Points    : int                      // How many points to spend
```

Client sends `StatAllocationRpc` → server writes into this buffer → `StatAllocationSystem` processes.

**File:** `Assets/Scripts/Progression/Components/StatAllocationRpcs.cs`

```
StatAllocationRpc (IRpcCommand)
  Attribute : byte
  Points    : int
```

### Singleton (BlobAssets)

**File:** `Assets/Scripts/Progression/Config/ProgressionBlob.cs`

```
ProgressionConfigSingleton (IComponentData)
  Curve      : BlobAssetReference<ProgressionBlob>
  StatScaling: BlobAssetReference<LevelStatScalingBlob>
  Rewards    : BlobAssetReference<LevelRewardsBlob>

ProgressionBlob
  MaxLevel                   : int
  StatPointsPerLevel         : int
  BaseKillXP                 : float
  KillXPPerEnemyLevel        : float
  DiminishStartDelta         : int
  DiminishFactorPerLevel     : float
  DiminishFloor              : float
  QuestXPBase                : float
  CraftXPBase                : float
  ExplorationXPBase          : float
  InteractionXPBase          : float
  RestedXPMultiplier         : float   // e.g., 1.0 = double XP while rested
  RestedXPAccumRatePerHour   : float   // rested pool grows while offline
  RestedXPMaxDays            : float   // cap on offline accumulation
  XPPerLevel                 : BlobArray<int>

LevelStatScalingBlob
  StatsPerLevel : BlobArray<LevelStatEntryBlob>
    (MaxHealth, AttackPower, SpellPower, Defense, Armor, MaxMana, ManaRegen, MaxStamina, StaminaRegen)

LevelRewardsBlob
  Rewards : BlobArray<LevelRewardEntryBlob>
    (Level, RewardType, IntValue, FloatValue)
```

---

## ScriptableObjects

### ProgressionCurveSO

**File:** `Assets/Scripts/Progression/Config/ProgressionCurveSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Progression/Progression Curve")]
```

| Field | Type | Purpose |
|-------|------|---------|
| MaxLevel | int [Min(1)] | Maximum achievable level (default 50) |
| StatPointsPerLevel | int | Stat points per level-up (default 3) |
| XPPerLevel[] | int[] | XP required per level (auto-generated geometric curve if empty) |
| BaseKillXP | float | Base XP for killing a level 1 enemy (default 100) |
| KillXPPerEnemyLevel | float | XP multiplier per enemy level (default 1.15) |
| DiminishStartDelta | int | Level gap before diminishing returns (default 3) |
| DiminishFactorPerLevel | float [0-1] | XP reduction per level below threshold (default 0.8) |
| DiminishFloor | float [0-1] | Minimum XP multiplier floor (default 0.1) |
| QuestXPBase | float | Base quest completion XP (default 200) |
| CraftXPBase | float | Base crafting XP (default 50) |
| ExplorationXPBase | float | Base zone discovery XP (default 150) |
| InteractionXPBase | float | Base NPC interaction XP (default 25) |
| RestedXPMultiplier | float | Bonus multiplier when rested (default 1.0 = 2x total) |
| RestedXPAccumRatePerHour | float | Rested pool accumulation per offline hour (default 500) |
| RestedXPMaxDays | float | Max offline days counted (default 3) |

### LevelStatScalingSO

**File:** `Assets/Scripts/Progression/Config/LevelStatScalingSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Progression/Level Stat Scaling")]
```

Per-level base stats array: MaxHealth, AttackPower, SpellPower, Defense, Armor, MaxMana, ManaRegen, MaxStamina, StaminaRegen. Separates level scaling from `CombatStatProfile` (which defines initial setup per entity type). `LevelStatScalingSystem` reads from the BlobAsset, not from `CombatStatProfile` directly.

### LevelRewardsSO

**File:** `Assets/Scripts/Progression/Config/LevelRewardsSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Progression/Level Rewards")]
```

```
LevelRewardType enum: StatPoints(0), CurrencyGold(1), RecipeUnlock(2), AbilityUnlock(3),
                       ContentGate(4), ResourceMaxUp(5), TalentPoint(6), Title(7)

LevelRewardEntry: { Level, RewardType, IntValue, FloatValue, Description }
```

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Client|Local):
  ProgressionBootstrapSystem                — loads SOs from Resources/, builds BlobAssets (runs once)

SimulationSystemGroup (Server|Local):
  [after DeathTransitionSystem ECB playback]
  XPAwardSystem                             — reads KillCredited, computes XP, writes CurrentXP
  StatAllocationRpcReceiveSystem            — receives StatAllocationRpc, writes buffer
  LevelUpSystem (Burst ISystem)             — checks threshold, increments Level, fires LevelUpEvent
  LevelRewardSystem                         — processes rewards, disables LevelUpEvent
  StatAllocationSystem                      — processes stat point spend requests
  [existing] EquippedStatsSystem            — aggregates equipment bonuses (unchanged)
  LevelStatScalingSystem (Burst ISystem)    — recalculates base stats from level + allocations + equipment
  [existing] ResourceModifierApplySystem    — applies ResourcePoolBase + equipment (unchanged)
  [existing] DeathLootSystem                — MODIFIED: reads killer Level into LootContext

PresentationSystemGroup (Client|Local):
  ProgressionUIBridgeSystem                 — managed bridge → UI registry
  ProgressionDebugSystem                    — debug overlay (optional)
```

### XP Formula

```
rawXP = BaseKillXP * pow(KillXPPerEnemyLevel, enemyLevel - 1)

levelDelta = playerLevel - enemyLevel
if levelDelta > DiminishStartDelta:
    diminish = pow(DiminishFactorPerLevel, levelDelta - DiminishStartDelta)
    diminish = max(diminish, DiminishFloor)
    rawXP *= diminish

restedBonus = 0
if RestedXP > 0:
    restedBonus = rawXP * RestedXPMultiplier
    RestedXP = max(0, RestedXP - restedBonus)

finalXP = (rawXP + restedBonus) * (1 + equipXPBonus)
```

**Example:** Level 10 player kills level 10 enemy: 100 * 1.15^9 ≈ 352 XP (no diminish).
Level 10 player kills level 5 enemy: 100 * 1.15^4 ≈ 175 * 0.8^2 ≈ 112 XP (diminished).
Level 10 player with +10% gear bonus: 352 * 1.1 ≈ 387 XP.
Level 10 player with full rested pool: 352 + 352 = 704 XP (2x).

### Key Design: Reuse CharacterAttributes.Level

`CharacterAttributes.Level` is ALREADY ghost-replicated on the player entity. `LevelUpSystem` writes directly to it. All existing systems that read Level (CombatStatProfile, LootTableResolver) work without modification. Zero new ghost components for level data.

### Stat Scaling Formula

```
BaseStat[level] = LevelStatScalingBlob.StatsPerLevel[level - 1].StatField
AllocBonus = CharacterAttributes.AttributeValue * ScalingCoefficient
EquipBonus = PlayerEquippedStats.TotalStatField

EffectiveStat = BaseStat[level] + AllocBonus + EquipBonus
```

`LevelStatScalingSystem` writes level-scaled + allocation base stats. `EquippedStatsSystem` (unchanged) adds gear bonuses. Equipment is ADDITIVE, not multiplicative. Stat allocation is additive per point.

### Stat Allocation

Players spend `UnspentStatPoints` to increase Str/Dex/Int/Vit:
1. Client sends `StatAllocationRpc(Attribute, Points)`
2. `StatAllocationRpcReceiveSystem` validates: `Points <= UnspentStatPoints`, writes to `StatAllocationRequest` buffer
3. `StatAllocationSystem`: decrements `UnspentStatPoints`, increments `CharacterAttributes.Strength` (etc.)
4. `LevelStatScalingSystem` picks up changed attributes next frame, recalculates effective stats

### Rested XP

- `RestedXP` accumulates based on time elapsed since last logout (tracked via save system, EPIC 16.15)
- On load: `RestedXP += min(offlineHours * RestedXPAccumRatePerHour, RestedXPMaxDays * 24 * AccumRate)`
- During gameplay: `XPAwardSystem` deducts from `RestedXP` pool as bonus XP is granted
- UI shows rested state and remaining rested pool

---

## Loot System Integration

**File:** `Assets/Scripts/Loot/Systems/DeathLootSystem.cs` (MODIFY -- ~10 lines)

Replace `Level = 1 // TODO` with killer's `CharacterAttributes.Level`. This immediately activates all existing `LootTableCondition.MinLevel / MaxLevel` conditions. Designers can gate epic drops behind "MinLevel = 20".

---

## Equipment XP Bonus

**File:** `Assets/Scripts/Items/Components/ItemStatBlock.cs` (MODIFY -- +4 bytes)

Add `float XPBonusPercent` field. On ITEM entities, not player.

**File:** `Assets/Scripts/Items/Components/PlayerEquippedStats.cs` (MODIFY -- +4 bytes)

Add `float TotalXPBonusPercent`. Aggregated by `EquippedStatsSystem`.

**File:** `Assets/Scripts/Items/Systems/EquippedStatsSystem.cs` (MODIFY -- 1 line)

Add: `stats.TotalXPBonusPercent += block.XPBonusPercent;`

---

## UI Bridge

**File:** `Assets/Scripts/Progression/UI/LevelUpVisualQueue.cs`
- Static NativeQueue bridge (same pattern as `DamageVisualQueue`)
- `XPGainEvent`: Amount, Source (XPSourceType), FinalAmount (after bonuses)
- `LevelUpVisualEvent`: NewLevel, PreviousLevel, StatPointsAwarded

**File:** `Assets/Scripts/Progression/UI/ProgressionUIBridgeSystem.cs`
- Managed SystemBase, PresentationSystemGroup, Client|Local
- Reads local player's `PlayerProgression` + `CharacterAttributes.Level`
- Computes XPPercent toward next level, pushes to `ProgressionUIRegistry`
- Dequeues visual events for popup/animations/floating XP numbers

**File:** `Assets/Scripts/Progression/UI/ProgressionUIRegistry.cs` + `IProgressionUIProvider.cs`
- Static singleton MonoBehaviour + provider interface
- Methods: `UpdateProgressionData(level, xp, xpToNext, percent, unspentPoints, restedXP)`, `OnXPGain(amount, source)`, `OnLevelUp(newLevel, rewards)`

**File:** `Assets/Scripts/Progression/UI/XPBarView.cs` / `LevelUpPopupView.cs` / `StatAllocationView.cs`
- MonoBehaviour UI implementations
- StatAllocationView: shows Str/Dex/Int/Vit with +/- buttons, sends `StatAllocationRpc`

---

## Authoring

**File:** `Assets/Scripts/Progression/Authoring/ProgressionAuthoring.cs`

```
[AddComponentMenu("DIG/Progression/Player Progression")]
```

- Fields: StartingXP (0), StartingStatPoints (0), StartingRestedXP (0)
- Baker adds: `PlayerProgression` + `LevelUpEvent` (disabled) + `StatAllocationRequest` buffer
- Place on player prefab alongside `CharacterAttributesAuthoring`

---

## Editor Tooling

### ProgressionWorkstationWindow

**File:** `Assets/Editor/ProgressionWorkstation/ProgressionWorkstationWindow.cs`
- Menu: `DIG/Progression Workstation`
- Sidebar + `IProgressionWorkstationModule` pattern

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Player Inspector | `Modules/PlayerInspectorModule.cs` | Live Level, XP, stat points, base vs allocated vs equipped stats, rested XP pool |
| XP Curve | `Modules/XPCurveModule.cs` | Animated graph of XP curve with current position marker. Shows time-to-level estimates at current kill rate |
| Level Rewards | `Modules/LevelRewardsModule.cs` | Table of all rewards by level, earned/upcoming markers, recipe/ability unlock preview |
| XP Simulator | `Modules/XPSimulatorModule.cs` | "Grant XP" button, "Set Level" slider, kill simulation (N enemies at level X), stat allocation sandbox |
| Balance Analyzer | `Modules/BalanceAnalyzerModule.cs` | Monte Carlo: simulate 1000 players, graph XP distribution, time-to-max-level, stat budget analysis |

---

## Multiplayer

- **Server-authoritative**: XP awards and level-ups on server only. No client-side XP prediction (prevents exploits).
- **Ghost-replicated**: `CharacterAttributes.Level` is `Ghost:All` — all clients see level. `PlayerProgression` is `Ghost:AllPredicted` — owning client sees XP bar.
- **Level-up stat changes replicate**: `AttackStats`, `DefenseStats`, `Health` are `Ghost:All` — remote clients see effects immediately.
- **Stat allocation RPC**: Client sends `StatAllocationRpc`, server validates and applies. No direct client writes to `CharacterAttributes`.
- **No new IBufferElementData** on ghost-replicated entities (StatAllocationRequest is NOT ghost-replicated).

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `PlayerProgression` | 16 bytes | Player entity |
| `LevelUpEvent` | 8 bytes | Player entity (IEnableableComponent, baked disabled) |
| `StatAllocationRequest` buffer header | ~16 bytes | Player entity (Capacity=4, NOT ghost) |
| `PlayerEquippedStats.TotalXPBonusPercent` | 4 bytes | Player entity (field addition) |
| **Total on player** | **~44 bytes** | |

For reference, `ResourcePool` (EPIC 16.8) added 80 bytes. 44 bytes is well within headroom.

---

## Extensibility

- **Prestige:** Add `int PrestigeLevel` to `PlayerProgression`. Reset Level to 1, increment prestige, multiply XP requirements.
- **Respec:** Set `UnspentStatPoints = (Level - 1) * StatPointsPerLevel`, reset CharacterAttributes to base. Future `RespecSystem` + confirmation UI.
- **Talent trees:** `LevelRewardType.TalentPoint` already defined. Future `TalentTreeSO` + `TalentAllocationSystem`.
- **Party XP sharing:** `XPAwardSystem` checks for future `PartyMemberElement` buffer on player. If present, splits XP among party members within range. Foundation: `XPAwardSystem` already receives `SourcePlayer` entity — expanding to a party list is a single query addition.

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `ProgressionBootstrapSystem` | N/A | No | Runs once at startup |
| `XPAwardSystem` | < 0.01ms | No | Only processes kill events (rare, ~1-5 per second) |
| `LevelUpSystem` | < 0.01ms | Yes | Single entity, BlobAsset lookup |
| `LevelRewardSystem` | < 0.02ms | No | Only on level-up (very rare) |
| `StatAllocationSystem` | < 0.01ms | No | Only on player input (very rare) |
| `LevelStatScalingSystem` | < 0.01ms | Yes | Single entity, 8 struct field writes |
| `ProgressionUIBridgeSystem` | < 0.03ms | No | Managed, queue drain |
| **Total** | **< 0.10ms** | | All systems combined |

---

## File Summary

### New Files (31)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Progression/Components/PlayerProgression.cs` | IComponentData |
| 2 | `Assets/Scripts/Progression/Components/LevelUpEvent.cs` | IEnableableComponent |
| 3 | `Assets/Scripts/Progression/Components/XPSourceType.cs` | Enum |
| 4 | `Assets/Scripts/Progression/Components/StatAllocationRequest.cs` | IBufferElementData + RPC |
| 5 | `Assets/Scripts/Progression/Config/ProgressionCurveSO.cs` | ScriptableObject |
| 6 | `Assets/Scripts/Progression/Config/LevelRewardsSO.cs` | ScriptableObject |
| 7 | `Assets/Scripts/Progression/Config/LevelStatScalingSO.cs` | ScriptableObject |
| 8 | `Assets/Scripts/Progression/Config/ProgressionBlob.cs` | BlobAsset structs + Singleton |
| 9 | `Assets/Scripts/Progression/Systems/ProgressionBootstrapSystem.cs` | SystemBase |
| 10 | `Assets/Scripts/Progression/Systems/XPAwardSystem.cs` | SystemBase |
| 11 | `Assets/Scripts/Progression/Systems/QuestXPBridgeSystem.cs` | Static API for external XP grants |
| 12 | `Assets/Scripts/Progression/Systems/LevelUpSystem.cs` | ISystem (Burst) |
| 13 | `Assets/Scripts/Progression/Systems/LevelRewardSystem.cs` | SystemBase |
| 14 | `Assets/Scripts/Progression/Systems/StatAllocationSystem.cs` | SystemBase |
| 15 | `Assets/Scripts/Progression/Systems/StatAllocationRpcReceiveSystem.cs` | SystemBase (Server) |
| 16 | `Assets/Scripts/Progression/Systems/LevelStatScalingSystem.cs` | ISystem (Burst) |
| 17 | `Assets/Scripts/Progression/UI/LevelUpVisualQueue.cs` | Static NativeQueue |
| 18 | `Assets/Scripts/Progression/UI/ProgressionUIBridgeSystem.cs` | SystemBase |
| 19 | `Assets/Scripts/Progression/UI/ProgressionUIRegistry.cs` | MonoBehaviour |
| 20 | `Assets/Scripts/Progression/UI/IProgressionUIProvider.cs` | Interface |
| 21 | `Assets/Scripts/Progression/UI/XPBarView.cs` | MonoBehaviour |
| 22 | `Assets/Scripts/Progression/UI/LevelUpPopupView.cs` | MonoBehaviour |
| 23 | `Assets/Scripts/Progression/UI/StatAllocationView.cs` | MonoBehaviour |
| 24 | `Assets/Scripts/Progression/Authoring/ProgressionAuthoring.cs` | Baker |
| 25 | `Assets/Scripts/Progression/Debug/ProgressionDebugSystem.cs` | SystemBase |
| 26 | `Assets/Scripts/Progression/DIG.Progression.asmdef` | Assembly def |
| 27 | `Assets/Editor/ProgressionWorkstation/ProgressionWorkstationWindow.cs` | EditorWindow |
| 28 | `Assets/Editor/ProgressionWorkstation/IProgressionWorkstationModule.cs` | Interface |
| 29 | `Assets/Editor/ProgressionWorkstation/Modules/PlayerInspectorModule.cs` | Module |
| 30 | `Assets/Editor/ProgressionWorkstation/Modules/XPCurveModule.cs` | Module |
| 31 | `Assets/Editor/ProgressionWorkstation/Modules/XPSimulatorModule.cs` | Module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Loot/Systems/DeathLootSystem.cs` | Replace `Level = 1` with killer's CharacterAttributes.Level (~10 lines) |
| 2 | `Assets/Scripts/Items/Components/ItemStatBlock.cs` | Add `float XPBonusPercent` (+4 bytes) |
| 3 | `Assets/Scripts/Items/Components/PlayerEquippedStats.cs` | Add `float TotalXPBonusPercent` (+4 bytes) |
| 4 | `Assets/Scripts/Items/Systems/EquippedStatsSystem.cs` | Aggregate XPBonusPercent (1 line) |
| 5 | Player prefab (Warrok_Server) | Add `ProgressionAuthoring` |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/ProgressionCurve.asset` |
| 2 | `Resources/LevelRewards.asset` |
| 3 | `Resources/LevelStatScaling.asset` |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `QuestRewardSystem` | 16.12 | Calls `XPGrantAPI.GrantXP(player, amount, XPSourceType.Quest)` |
| `CraftOutputGenerationSystem` | 16.13 | Grants craft XP via `XPGrantAPI.GrantXP()` |
| `RecipeUnlockCondition.PlayerLevel` | 16.13 | Checks `CharacterAttributes.Level` for recipe gates |
| `DeathLootSystem` | 16.6 | Reads killer level into LootContext (modified) |
| `LootTableResolver` | 16.6 | Evaluates MinLevel/MaxLevel (unchanged, now activated) |
| `ResourceModifierApplySystem` | 16.8 | Applies updated ResourcePoolBase from level scaling |
| `PersistenceSaveModule` | 16.15 | Saves/loads PlayerProgression + CharacterAttributes |
| `DialogueConditionType.PlayerLevel` | 16.16 | Reads Level for dialogue branch gating |

---

## Verification Checklist

- [ ] Player kills level 5 enemy: XP awarded (BaseKillXP * pow(1.15, 4))
- [ ] Player kills enemy 5 levels below: diminishing returns applied
- [ ] Player with +10% XP gear: 10% more XP
- [ ] Player with rested XP: double XP until pool depleted
- [ ] XP exceeds threshold: `CharacterAttributes.Level` increments
- [ ] Multi-level-up from massive XP: loop handles correctly
- [ ] XP carry-over: excess rolls into next level
- [ ] Max level cap: stops at MaxLevel, excess XP discarded
- [ ] Stat points awarded per level-up
- [ ] Stat allocation: spend 3 points in Strength, verify CharacterAttributes.Strength increased
- [ ] Stat allocation: reject if UnspentStatPoints insufficient
- [ ] Level-up: AttackStats, DefenseStats, Health.Max recalculated
- [ ] Level-up: ResourcePool.Max increases via ResourcePoolBase
- [ ] Level rewards: gold, recipe unlocks, ability unlocks distributed
- [ ] Loot integration: DeathLootSystem reads killer level, MinLevel/MaxLevel conditions work
- [ ] Equipment change: stats recalculate with current level base + allocations + new gear
- [ ] Multiplayer: XP server-authoritative (no client prediction)
- [ ] Multiplayer: Level replicates to all clients
- [ ] Multiplayer: remote clients see level on name plates
- [ ] Multiplayer: stat allocation RPC validated server-side
- [ ] UI: XP bar updates on kill with floating XP number
- [ ] UI: "LEVEL UP!" popup on level transition
- [ ] UI: Stat allocation panel shows available points and +/- buttons
- [ ] UI: Rested XP indicator visible when pool > 0
- [ ] Progression Workstation: player inspector shows live data
- [ ] Progression Workstation: XP curve graph renders correctly
- [ ] Progression Workstation: XP simulator grants XP during play mode
- [ ] Progression Workstation: balance analyzer runs Monte Carlo simulation
- [ ] Entity without PlayerProgression: zero cost
- [ ] No regression: existing combat, loot, equipment unchanged
- [ ] Archetype: only +44 bytes on player entity
