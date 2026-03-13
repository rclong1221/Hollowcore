# EPIC 14: Boss System & Variant Clauses

**Status**: Planning
**Priority**: High — District climax encounters
**Dependencies**: Framework: Combat/, AI/, Roguelite/ (EncounterState); EPIC 3 (Front), EPIC 13 (Districts)
**GDD Sections**: Per-district boss entries in §14, boss clauses in §8.1 (Strife)

---

## Problem

Every district has a unique boss that serves as the climactic encounter. Bosses aren't static — their mechanics change based on which side goals the player completed, which Strife card is active, how far the Front has advanced, and the player's Trace level. This means each boss fight is different depending on how you played the district. The framework has EncounterState/EncounterTriggerDefinition for boss encounters; this epic builds the variant clause system on top.

---

## Overview

15 district bosses (one per district) + 3 final bosses (EPIC 15). Each boss has a base encounter design plus a matrix of variant clauses that activate/deactivate based on player choices. Side goals disable boss mechanics (insurance), Strife cards add mechanics (challenge), Front phase scales difficulty, and Trace level adds reinforcements.

---

## Sub-Epics

### 14.1: Boss Definition Template
Universal boss structure.

- **BossDefinitionSO**: BossId, DisplayName, Description, Prefab, ArenaDefinition
  - Base mechanics: phase list, attack patterns, health scaling
  - **VariantClauses**: list of BossVariantClauseSO
  - ArenaDefinition: scene/prefab for boss arena, environmental hazards
- **BossVariantClauseSO**: ClauseId, TriggerType, TriggerCondition, MechanicModification
  - **TriggerType enum**: SideGoalSkipped, SideGoalCompleted, StrifeCard, FrontPhase, TraceLevel
  - **SideGoal clauses** (the "insurance" mechanic):
    - Example: Necrospire — "Skipped Grief-Link → boss has real stun mechanic"
    - Example: Necrospire — "High Trace → extra Wardens in fight"
    - Completing side goal DISABLES the corresponding boss clause
    - Skipping it ENABLES it — making the boss harder
  - **Strife clauses**: active Strife card adds a mechanic
    - Example: Signal Schism on Grandmother Null → UI possession during fight
    - Example: Gravity Storm on King of Heights → brutal fall damage in arena
  - **Front phase scaling**: boss stats scale with district Front phase
    - Phase 1-2: base difficulty
    - Phase 3: +25% health, +1 additional mechanic active
    - Phase 4: +50% health, all optional mechanics active
  - **Trace scaling**: Trace 4+ → boss gets reinforcements mid-fight

### 14.2: Boss Encounter Flow
How boss fights are structured.

- **Pre-fight**: boss arena entrance, variant clause evaluation, UI shows active modifiers
- **Phase system**: multi-phase fights (2-4 phases per boss)
  - Phase transitions at health thresholds
  - New attacks/mechanics per phase
  - Arena may change between phases (e.g., Foreman's living factory reconfigures)
- **Mid-fight events**: Strife-triggered interruptions, reinforcement waves
- **Victory**: boss death → major reward dump, district extraction opens
- **Failure**: death in boss fight → standard death system (EPIC 2), can re-attempt
  - Boss health does NOT reset between attempts (within same district visit)
  - BUT: boss may gain awareness of your tactics (variant clause for "seen you before")
- **Integration with EncounterState**: uses framework's phase/trigger system for state machine

### 14.3: Boss Arena System
Per-boss fighting spaces.

- **ArenaDefinitionSO**: layout template, environmental hazards, interactable elements
- **Arena types** (from GDD):
  - Grandmother Null: Cathedral of screens (Necrospire) — holographic interference
  - The Foreman: Living factory (Burn) — conveyors, pistons, slag as weapons
  - King of Heights: Multi-level arena (Lattice) — collapsing platforms, fall = damage
  - The Surgeon General: Flooded operating theater (Wetmarket) — rising water levels
  - 404 Entity Not Found: Shifting absence-space (Glitch Quarter) — geometry changes
  - The Archbishop Algorithm: Cable-web heart (Cathedral) — cable terrain
  - The Leviathan Empress: Flooding arena (Shoals) — staged flooding
  - The Prime Reflection: Hall of mirrors (Mirrortown) — visual deception
  - The Overmind: Mood-shifting arena (Synapse) — emotional environment
  - Patient Zero: Building-mass (Quarantine) — evolving organic terrain
  - The Gardener-Prime: Environment fight (Old Growth) — everything is the boss
  - The Broker: Trading floor (Auction) — buyout mechanics
  - The Silence: Analog arena (Deadwave) — tech disabled
  - The Collective Unconscious: Everywhere-at-once (Nursery) — distributed boss
  - Commander Echo: Gravity-shifting arena (Skyfall) — gravity as mechanic
- **Arena hazards**: environmental threats that apply to player AND boss
- **Arena interactables**: objects that can be used tactically (cover, traps, levers)

### 14.4: Boss Counter Token System
Side goal rewards that disable boss mechanics.

- **BossCounterTokenDefinitionSO**: TokenId, DisplayName, Description, Icon, BossId, ClauseIdDisabled
- **Token acquisition**: specific side goals in thematically linked districts
  - Example: "Warden Override Key" found in Necrospire side goal → disables Warden reinforcements in Grandmother Null fight
  - Example: "Hymn Scrambler" found in another district → disables Cathedral boss's hymn attack
- **Cross-district tokens**: some tokens found in District A are useful for District B's boss
  - Creates strategic gate selection decisions: "I should go to District A first to get the token for District B's boss"
- **Token UI**: inventory shows collected tokens, boss preview shows which clauses are disabled
- **Maps to**: Items/ system — tokens are inventory items consumed on boss fight entry

### 14.5: Boss Reward & Extraction
What happens after the boss.

- **Boss loot table**: guaranteed high-quality rewards
  - Unique limb drop (boss limb with strongest memory bonuses — EPIC 1)
  - Boss counter tokens for other districts
  - Compendium entries
  - Large currency dump
- **Extraction sequence**: boss death → loot room → gate to Gate Selection screen
  - Brief safe period for inventory management
  - Scar Map update with boss completion marker
- **District completion effects**:
  - Forward gates unlocked in expedition graph
  - District Front paused (not reversed — just frozen)
  - New events seeded in previous districts (power vacuum — GDD §4.3)

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Combat/ (EncounterState) | Boss phases use existing encounter state machine |
| AI/ | Boss behavior tree / state machine |
| Roguelite/ (EncounterTriggerDefinition) | Phase transitions use framework triggers |
| Roguelite/ (RuntimeDifficultyScale) | Boss stats scale with difficulty |
| Items/ | Boss counter tokens are items |
| Loot/ | Boss loot tables follow existing pattern |
| EPIC 7 (Strife) | Strife boss clauses add mechanics |
| EPIC 8 (Trace) | Trace level adds reinforcements |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 14.1 (Template) | None — definition | — |
| 14.2 (Encounter Flow) | 14.1 | 14.1 variant clauses |
| 14.3 (Arena) | 14.1 | — |
| 14.4 (Counter Tokens) | 14.1, EPIC 10 (rewards) | EPIC 13 (cross-district placement) |
| 14.5 (Rewards) | 14.2 | EPIC 1 (boss limbs), EPIC 4 (graph unlock) |

---

## Vertical Slice Scope

- 14.1 (template) + 14.2 (flow) for 3 bosses (Grandmother Null, The Foreman, King of Heights)
- 14.3 (arena) basic arena for each boss
- 14.4 (counter tokens) at least 1-2 tokens per boss
- 14.5 (rewards) boss loot tables functional

---

## Tooling & Quality

| Sub-Epic | BlobAsset Pipeline | Validation | Editor Tooling | Live Tuning | Debug Viz | Simulation |
|---|---|---|---|---|---|---|
| 14.1 (Template) | BossDefinitionSO -> BossBlob (phases, clauses, health curve) | Phase thresholds, clause payloads, counter token refs, arena bounds, health curve | **Boss Designer Workstation**: Phase timeline, attack configurator, clause matrix, health scaling curve, difficulty score, DPS calculator | HP, thresholds, attack damage/timing, enrage timer, clause overrides | HP bar + phase markers, clause indicators, enrage timer, DPS meter, telegraph wireframes | **Boss Fight Simulator**: 1000-run automated agent (DPS, dodge, positioning), TTK distribution, wipe rate by clause combo, DPS check |
| 14.2 (Encounter Flow) | Mid-fight event blobs, phase transition duration, enrage timer | Phase threshold ordering, event overlap, transition duration, enrage timer | (Shared with 14.1 workstation) | Phase transition speed, skip pre-fight/cinematics, infinite HP, instant kill, disable reinforcements | Encounter state, persistent HP indicator, mid-fight event queue | Phase timing analysis, pacing balance, reinforcement wipe rate |
| 14.3 (Arena) | (ArenaDefinitionSO baked via 14.1 blob) | Arena bounds, hazard positions, cycle timing, layout indices, DPS ranges | Arena designer: layout viewer, hazard timeline, danger heatmap | Hazard damage/speed multiplier, disable/force hazards, force layout | Hazard volume wireframes, interactable status, layout state, arena bounds | (Covered by 14.1 simulator with hazard avoidance parameter) |
| 14.4 (Counter Tokens) | (Token data embedded in clause blobs) | Token-to-boss reference integrity, clause matching, cross-district consistency, coverage check | Token coverage matrix, cross-district flow diagram, acquisition timeline | (N/A — tokens are inventory items) | Inventory token panel, boss preview token overlay | (N/A — token logic is deterministic) |
| 14.5 (Rewards) | (Loot table references in BossBlob) | Loot table refs, limb definitions, currency amounts, extraction gate presence | (Shared with 14.1 workstation) | Currency multiplier, force max rewards, skip loot room/extraction | Reward breakdown, extraction status, district completion effects | (N/A — reward distribution is deterministic) |
