# EPIC 14.1 Setup Guide: Boss Definition & Variant Clause System

**Status:** Planned
**Requires:** Framework: Combat/ (EncounterState, EncounterTriggerDefinition), AI/, Roguelite/ (RuntimeDifficultyScale); EPIC 7 (Strife system), EPIC 8 (Trace system), EPIC 10 (Side Goals)

---

## Overview

Every district boss in Hollowcore is defined by a `BossDefinitionSO` ScriptableObject. This asset is the single source of truth for a boss's identity, phase structure, attack patterns, health scaling, and variant clause matrix. The variant clause system is the core differentiator: side goals function as "insurance" (completing them disables boss mechanics), Strife cards add mechanics, Front phase scales difficulty, and Trace level adds reinforcements. This guide covers creating boss definition assets from scratch through to verification in the Boss Workstation.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| `Hollowcore.Boss.asmdef` | Assembly definition | References `DIG.Shared`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`, `Hollowcore.Roguelite` |
| `Assets/Scripts/Boss/` | Folder structure | Subfolders: `Components/`, `Definitions/`, `Systems/`, `Authoring/`, `Debug/` |
| Boss prefab (e.g. Grandmother Null) | Enemy prefab with AI/ components | The NPC entity that the definition describes |
| Arena subscene | Per-boss arena (EPIC 14.3) | Referenced by ArenaDefinition field |
| Side goal definitions (EPIC 10) | SideGoalDefinitionSO assets | Required for SideGoalSkipped/Completed clause triggers |

### New Setup Required

1. Create `BossDefinitionSO` asset via `Assets > Create > Hollowcore/Boss/Boss Definition`.
2. Create one or more `BossVariantClauseSO` assets via `Assets > Create > Hollowcore/Boss/Variant Clause`.
3. Add `BossDefinitionAuthoring` component to the boss prefab root.
4. Configure phases, attacks, health scaling, and variant clauses on the SO.
5. Open the Boss Workstation (`Window > Hollowcore > Boss Workstation`) to validate and tune.

---

## 1. Creating a Boss Definition Asset

**Create:** `Assets > Create > Hollowcore/Boss/Boss Definition`
**Recommended location:** `Assets/Data/Boss/Definitions/`
**Naming convention:** `Boss_[DistrictName]_[BossName].asset` (e.g., `Boss_Necrospire_GrandmotherNull.asset`)

### 1.1 Identity Section

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `BossId` | Unique integer identifier | 0 | Must be globally unique across all bosses. Use sequential: 1-15 for district bosses, 101-103 for final bosses |
| `DisplayName` | Name shown in UI (boss HP bar, pre-fight screen, Compendium) | empty | Keep under 30 characters |
| `Description` | Lore/flavor text for pre-fight UI and Compendium | empty | 2-4 sentences describing the boss's role in the district |
| `Portrait` | Sprite for pre-fight UI and boss preview screens | null | 256x256 recommended. Assign from `Assets/Art/UI/Boss/Portraits/` |
| `Prefab` | The boss NPC prefab (must have `BossDefinitionAuthoring`) | null | Must exist in a subscene. Must have BossTag, BossEncounterLink, BossInvulnerable (baked disabled) |

**Tuning tip:** BossId values are baked into blob assets and referenced by counter tokens, clauses, and save data. Changing a BossId after content is wired will break all references. Assign IDs early and lock them.

### 1.2 Arena Section

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `ArenaDefinition` | Reference to `ArenaDefinitionSO` | null | See SETUP_GUIDE_14.3 for arena creation. Every boss MUST have an arena |

### 1.3 Phase Configuration

Phases define the multi-stage structure of the boss fight. Phase 0 is the starting phase (implicit threshold 1.0). Each subsequent phase activates at a health threshold.

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `Phases` | Ordered list of `BossPhaseDefinition` | empty | Minimum 2 phases (start + one transition). Maximum 4 recommended |

**Per-phase fields:**

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `PhaseName` | Display name (e.g., "Defensive", "Enraged") | empty | For designer clarity and debug overlay |
| `HealthThreshold` | Health fraction (0-1) to transition INTO this phase | 0.5 | Must be strictly decreasing: Phase 1 < 1.0, Phase 2 < Phase 1, etc. |
| `PhaseAttackPatterns` | Additional `BossAttackPatternSO` references unlocked in this phase | empty | These ADD to base patterns, not replace |
| `ArenaEvents` | `ArenaEventSO` references triggered on phase entry | empty | Arena hazard activations, layout changes, VFX |
| `PhaseDialogue` | Boss dialogue line on phase transition | empty | Displayed during phase transition cinematic |

**Tuning tip:** Standard phase threshold pattern: Phase 1 starts at 1.0 (implicit), Phase 2 at 0.65, Phase 3 at 0.30. This gives a 35% / 35% / 30% health split. For bosses with 4 phases, use 1.0 / 0.75 / 0.50 / 0.25.

**Tuning tip:** Phase thresholds MUST be strictly monotonically decreasing. The validator will error if any threshold >= the previous one. Phase 0's threshold is implicitly 1.0 and does not need to be set.

### 1.4 Base Attack Patterns

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `BaseAttackPatterns` | List of `BossAttackPatternSO` | empty | Available in ALL phases. Phase-specific patterns are additive |

Each `BossAttackPatternSO` defines one attack or attack sequence. These are separate ScriptableObjects created via `Assets > Create > Hollowcore/Boss/Attack Pattern` and stored in `Assets/Data/Boss/Attacks/`.

### 1.5 Health Scaling

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `BaseHealth` | Starting HP at difficulty 0, single player | 10000 | 5000-50000 typical. District bosses: 8000-15000. Final bosses: 20000-40000 |
| `CoopHealthScale` | Additional HP per extra player, as fraction of base | 0.6 | 0.4-0.8. At 0.6, a 4-player boss has BaseHealth * (1 + 3*0.6) = 2.8x HP |
| `DifficultyHealthCurve` | AnimationCurve mapping RuntimeDifficultyScale (0-1) to health multiplier | Linear(0,1,1,2) | X-axis: difficulty [0..1]. Y-axis: multiplier. All sampled values must be > 0 |

**Tuning tip:** The DifficultyHealthCurve is sampled at 64 uniform points and baked into a BlobArray. Use gentle curves -- sharp spikes create difficulty cliffs. A good starting curve: ease-in from 1.0 at x=0 to 2.0 at x=1.0.

**Tuning tip:** Effective boss HP formula: `BaseHealth * DifficultyHealthCurve(difficultyScale) * (1 + (playerCount - 1) * CoopHealthScale) * AscensionState.EnemyHealthMultiplier * BossVariantState.HealthMultiplier`. Use the DPS Check Calculator in the Boss Workstation to verify kill times at different configurations.

### 1.6 Variant Clauses

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `VariantClauses` | List of `BossVariantClauseSO` references | empty | Minimum 3 for vertical slice (1 SideGoal, 1 Strife, 1 Front). Maximum 32 (bitmask limit) |

### 1.7 Loot Table

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `BossLootTable` | Reference to framework Loot/ `LootTableSO` via `LootTableReference.LootTableId` | 0 | Must reference a valid LootTableSO. See SETUP_GUIDE_14.5 |

---

## 2. Creating Variant Clause Assets

**Create:** `Assets > Create > Hollowcore/Boss/Variant Clause`
**Recommended location:** `Assets/Data/Boss/Clauses/[BossName]/`
**Naming convention:** `Clause_[BossName]_[ShortDescription].asset` (e.g., `Clause_GrandmotherNull_WardenGarrison.asset`)

### 2.1 Identity Section

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `ClauseId` | Unique integer within this boss's clause list | 0 | 0-31 (maps to bit position in ActiveClauseMask). Must be unique per boss |
| `DisplayName` | Name shown in pre-fight modifier UI | empty | Short and descriptive: "Warden Garrison", "Full Network" |
| `Description` | What this clause does to the fight | empty | 1-2 sentences explaining the gameplay effect |
| `Icon` | Sprite for modifier UI overlay | null | 64x64 recommended. Assign from `Assets/Art/UI/Boss/Clauses/` |

### 2.2 Trigger Configuration

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `TriggerType` | `BossVariantTriggerType` enum | SideGoalSkipped | See trigger type table below |
| `TriggerCondition` | Integer whose meaning depends on TriggerType | 0 | See trigger type table below |

**Trigger Type Reference:**

| TriggerType | TriggerCondition Meaning | Activation Logic |
|-------------|-------------------------|------------------|
| `SideGoalSkipped` (0) | Side goal ID from EPIC 10 | Active when player did NOT complete the linked side goal. This is the "insurance" mechanic |
| `SideGoalCompleted` (1) | Side goal ID from EPIC 10 | Active when player DID complete the goal. Rare -- used for reward-type clauses |
| `StrifeCard` (2) | Strife card ID from EPIC 7 | Active when the matching Strife card is currently in play |
| `FrontPhase` (3) | Phase threshold (1-4) | Active when the district's Front has reached this phase or higher |
| `TraceLevel` (4) | Trace threshold (1-5) | Active when player Trace is at or above this value |

**Tuning tip:** Most bosses should have at least one `SideGoalSkipped` clause per linked side goal. This is the core "insurance" loop -- players who do side content get an easier boss. Skipping side goals means facing the full boss.

### 2.3 Effect Configuration

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `Effect.EffectType` | `BossClauseEffectType` enum | AddAttack | See effect type table below |
| `Effect.AddedAttack` | `BossAttackPatternSO` reference | null | Required when EffectType = AddAttack |
| `Effect.StatMultiplier` | Multiplier value | 1.0 | Required when EffectType = StatMultiplier. 1.25 = +25% |
| `Effect.ReinforcementWave` | `ReinforcementWaveSO` reference | null | Required when EffectType = SpawnReinforcements |
| `Effect.EnvironmentHazard` | `ArenaEventSO` reference | null | Required when EffectType = EnvironmentHazard |
| `Effect.EffectDescription` | Human-readable description | empty | Shown in debug overlay and workstation |

**Effect Type Reference:**

| EffectType | What It Does | Required Fields |
|------------|-------------|-----------------|
| `AddAttack` (0) | Boss gains an additional attack pattern when clause is active | `AddedAttack` |
| `StatMultiplier` (1) | Boss health/damage scaled by multiplier | `StatMultiplier` (warn if exactly 1.0) |
| `SpawnReinforcements` (2) | Mid-fight enemy wave triggered at health thresholds | `ReinforcementWave` |
| `EnvironmentHazard` (3) | Arena hazard activated for duration of fight | `EnvironmentHazard` |
| `DisableWeakpoint` (4) | A boss weakness the player could exploit is removed | No additional fields |
| `ModifyPhaseThreshold` (5) | Phase transitions happen at different health values | `StatMultiplier` (used as threshold modifier) |

### 2.4 Counter Token Configuration

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `CounterTokenClauseId` | If non-zero, this clause can be disabled by a matching BossCounterToken | 0 | Must match a `BossCounterTokenDefinitionSO.ClauseIdDisabled`. See SETUP_GUIDE_14.4 |

**Tuning tip:** Not every clause should be disableable by counter tokens. Strife and FrontPhase clauses are designed to always be active when conditions are met. Side goal clauses are the primary candidates for counter token disabling.

---

## 3. Wiring the Boss Prefab

### 3.1 Add BossDefinitionAuthoring

1. Select the boss NPC prefab root entity (the one with `DamageableAuthoring`, `GhostAuthoringComponent`).
2. Add `BossDefinitionAuthoring` component.
3. Drag the `BossDefinitionSO` asset into the `BossDefinition` field.

**The baker will:**
- Read the BossDefinitionSO reference.
- Bake phase list into `BlobArray<BossPhaseBlob>`.
- Bake variant clause IDs into `BlobArray<int>`.
- Add `BossTag` IComponentData for query filtering.
- Add `BossVariantState` with default values (populated at runtime by `BossClauseEvaluationSystem`).
- Add `ActiveBossClauseBuffer` (empty, populated at runtime).
- Add `BossEncounterLink` (populated at runtime by encounter trigger).
- Add `BossInvulnerable` as IEnableableComponent (baked disabled).

### 3.2 Prefab Component Checklist

| Component | Source | Notes |
|-----------|--------|-------|
| `BossTag` | Baked by BossDefinitionAuthoring | Query filter for boss-specific systems |
| `BossVariantState` | Baked by BossDefinitionAuthoring | Runtime clause evaluation results |
| `ActiveBossClauseBuffer` | Baked by BossDefinitionAuthoring | InternalBufferCapacity(8) |
| `BossEncounterLink` | Baked by BossDefinitionAuthoring | Links NPC to encounter entity at runtime |
| `BossInvulnerable` | Baked disabled by BossDefinitionAuthoring | Toggled during phase transitions |
| `BossPhaseState` | Added by BossEncounterSystem at encounter start | NOT on prefab -- created at runtime |
| Health, DamageableTag, etc. | Existing from DamageableAuthoring | Standard enemy components |

---

## 4. BlobAsset Pipeline

The BossDefinitionSO bakes into a `BossBlob` via `BossDefinitionBlobBaker`:

| Blob Field | Source | Notes |
|------------|--------|-------|
| `BlobArray<BossPhaseBlob> Phases` | `BossDefinitionSO.Phases` | HealthThreshold, AttackPatternIds, ArenaEventIds per phase |
| `BlobArray<BossClauseBlob> Clauses` | `BossDefinitionSO.VariantClauses` | TriggerType, TriggerCondition, EffectType, StatMultiplier, CounterTokenClauseId |
| `BlobArray<float2> DifficultyHealthCurve` | `BossDefinitionSO.DifficultyHealthCurve` | AnimationCurve sampled at 64 uniform points |
| `float BaseHealth` | `BossDefinitionSO.BaseHealth` | |
| `float CoopHealthScale` | `BossDefinitionSO.CoopHealthScale` | |
| `int BossId` | `BossDefinitionSO.BossId` | |

**Constraint:** All sampled DifficultyHealthCurve values must be > 0.

---

## 5. Runtime Clause Evaluation Flow

When a boss encounter begins (`BossPhaseState.EncounterPhase == PreFight`), `BossClauseEvaluationSystem` runs:

1. Load BossDefinitionSO variant clause list from blob.
2. For each clause, evaluate trigger condition:
   - **SideGoalSkipped:** Check RunHistorySaveModule -- if side goal NOT completed, clause ACTIVE.
   - **SideGoalCompleted:** Check RunHistorySaveModule -- if side goal completed, clause ACTIVE.
   - **StrifeCard:** Check active Strife card ID -- if matches, clause ACTIVE.
   - **FrontPhase:** Check district Front phase -- if >= threshold, clause ACTIVE.
   - **TraceLevel:** Check player Trace -- if >= threshold, clause ACTIVE.
3. For each active clause, check player inventory for BossCounterTokens with matching `CounterTokenClauseId` -- if found, DEACTIVATE clause, CONSUME token.
4. Set `BossVariantState.ActiveClauseMask` and populate `ActiveBossClauseBuffer`.
5. Calculate `HealthMultiplier` from active FrontPhase clauses.
6. Set `ReinforcementsEnabled` from active TraceLevel clauses.

---

## 6. Boss Workstation Overview

Open via `Window > Hollowcore > Boss Workstation`.

| Tab | Purpose |
|-----|---------|
| Phase Timeline | Horizontal HP bar with draggable phase boundary handles. Click phase to expand attack/event details |
| Attack Configurator | Per-phase attack list with timing (cooldown, cast, recovery, telegraph). 60s rotation preview |
| Clause Matrix | Rows = side goals, columns = variant clauses. Green = disabled, red = active, yellow = Strife/Front/Trace |
| Health Scaling Curve | AnimationCurve editor with co-op player count overlay lines (1P, 2P, 3P, 4P) and phase threshold markers |
| Difficulty Score | Composite score comparing all bosses in project. Color-coded: green/yellow/orange/red |
| DPS Calculator | Input player DPS, player count, difficulty. Output: time per phase, total kill time, minimum viable DPS |

---

## Scene & Subscene Checklist

- [ ] Boss prefab exists in a subscene
- [ ] Boss prefab has `BossDefinitionAuthoring` with SO reference
- [ ] Boss prefab has `DamageableAuthoring` (standard enemy setup)
- [ ] Boss arena subscene exists and is referenced by ArenaDefinitionSO
- [ ] Boss arena has encounter trigger volume with `EncounterTriggerDefinition`
- [ ] Ghost serialization variant includes BossVariantState and BossPhaseState
- [ ] Subscene reimported after any ghost component changes

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Phase thresholds not strictly decreasing | Validator error, phases fire out of order | Ensure Phase 1 < 1.0, Phase 2 < Phase 1, etc. |
| Duplicate ClauseId within same boss | Bitmask collision, wrong clauses activate | Assign unique 0-31 values per clause |
| EffectType = AddAttack but AddedAttack is null | Clause activates but no attack added | Assign a BossAttackPatternSO to Effect.AddedAttack |
| StatMultiplier = 1.0 (no-op) | Validator warning, clause has no effect | Set a meaningful multiplier (e.g., 1.25 for +25%) |
| CounterTokenClauseId set but no matching token SO exists | Validator warning (orphaned clause) | Create a BossCounterTokenDefinitionSO with matching ClauseIdDisabled |
| DifficultyHealthCurve has value <= 0 at any sample point | Validator error, zero-health boss | Ensure all curve values are positive |
| Missing BossTag on prefab | Boss not found by boss-specific systems | Add BossDefinitionAuthoring (bakes BossTag automatically) |
| BossId changed after wiring counter tokens | Tokens reference wrong boss | Lock BossId early; if changed, update all referencing token SOs |
| More than 32 variant clauses | Bitmask overflow, clauses silently ignored | Keep VariantClauses list at 32 or fewer |
| Forgetting subscene reimport after adding ghost components | "BlobAssetReference is null" crash | Always reimport subscene after modifying ghost-replicated components |

---

## Verification

- [ ] BossDefinitionSO serializes all fields (phases, clauses, health scaling)
- [ ] BossVariantClauseSO correctly stores trigger type and condition
- [ ] BossVariantState.ActiveClauseMask bit operations work for all 32 clause indices
- [ ] ActiveBossClauseBuffer populates with correct clause data at encounter start
- [ ] SideGoalSkipped clause activates when side goal NOT completed
- [ ] SideGoalSkipped clause deactivates when side goal completed (insurance works)
- [ ] StrifeCard clause activates only when matching Strife card is active
- [ ] FrontPhase clause activates at correct phase threshold
- [ ] TraceLevel clause activates at correct Trace threshold
- [ ] Counter tokens in inventory disable matching clauses and are consumed
- [ ] HealthMultiplier correctly reflects Front phase scaling (1.0 / 1.25 / 1.5)
- [ ] BossTag query filters work to distinguish boss entities from regular enemies
- [ ] Boss Workstation Phase Timeline renders all phases correctly
- [ ] Boss Workstation Clause Matrix shows correct clause-to-goal mapping
- [ ] Boss Workstation DPS Calculator produces valid kill time estimates
- [ ] Boss Workstation validation passes with no errors

---

## Vertical Slice Targets

For vertical slice, create definitions for these 3 bosses:

| Boss | District | BossId | Phases | Minimum Clauses |
|------|----------|--------|--------|-----------------|
| Grandmother Null | Necrospire | 1 | 3 (Cathedral, Hymn, Desperation) | SideGoalSkipped(WardenGarrison), StrifeCard, FrontPhase |
| The Foreman | Burn | 2 | 3 (Assembly, Overdrive, Meltdown) | SideGoalSkipped(SlagExpansion), StrifeCard, FrontPhase |
| King of Heights | Lattice | 3 | 3 (Overlook, Collapse, Freefall) | SideGoalSkipped(GravityShift), StrifeCard, FrontPhase |
