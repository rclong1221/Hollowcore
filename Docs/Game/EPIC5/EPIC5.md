# EPIC 5: Echo Missions

**Status**: Planning
**Priority**: High — Core consequence mechanic
**Dependencies**: Framework: Quest/, Rewards/, Loot/; EPIC 3 (Front), EPIC 4 (District Graph)
**GDD Sections**: 6.1-6.4 Echo Missions

---

## Problem

Skipped or failed side goals don't disappear — they mutate into harder, weirder versions in the same district. Echoes introduce "wrongness" — fundamental changes to encounters, not just stat inflation. This is one of Hollowcore's core design pillars: "Consequence Echoes — what you skip or fail comes back harder, in the same run."

---

## Overview

When a player leaves a district with uncompleted side goals, those goals mutate into Echo Missions. If the player backtracks, the echoes are waiting — harder enemies, altered mechanics, distorted layouts, but significantly better rewards. Echoes that are never resolved persist into future expeditions visiting the same district, becoming legendary challenges.

---

## Sub-Epics

### 5.1: Echo Generation
Transforming skipped goals into echoes.

- **EchoTrigger**: on district exit, check uncompleted side goals → generate echo per skipped goal
- **EchoDefinitionSO**: per-quest echo template
  - Original QuestDefinitionSO reference
  - Mutation type: EnemyUpgrade, MechanicChange, LayoutDistortion, FactionSwap, TemporalAnomaly
  - Difficulty multiplier (base ~1.5x-2x original)
  - Reward multiplier (~2x-3x original)
  - Thematic "wrongness" description per district
- **EchoState** (IComponentData per echo): EchoId, SourceQuestId, DistrictId, ZoneId, MutationType, IsCompleted, ExpeditionsPersisted
- **Echo mutation types per district** (GDD §6.1):
  - Necrospire: rotting memories — enemies speak in dead voices, objectives inverted
  - Wetmarket: drowned versions — encounters flooded, enemies mutated
  - Glitch Quarter: time-looped — enemies reset on death once, objectives shift
  - Chrome Cathedral: blessed corruption — enemies have faith armor, objective is defiled
  - (Each district has its own thematic echo flavor)

### 5.2: Echo Encounters
How echoes play differently from originals.

- **"Wrongness" not stat inflation** — echoes are mechanically distinct:
  - New enemy variants (echo-specific prefabs or modifier sets)
  - Altered objectives (rescue becomes escort, kill becomes survive, etc.)
  - Layout distortions (new paths, blocked routes, environmental changes)
  - Audio/visual wrongness (reversed audio, color shifts, impossible geometry)
- **EchoEncounterDefinitionSO**: extends EncounterPoolSO with echo-specific overrides
  - Enemy replacements/upgrades
  - Objective mutation rules
  - Environmental modifiers
  - Duration/timer changes
- **Echo difficulty scaling**:
  - Base: 1.5x original
  - Per-expedition-persisted: +0.25x per expedition (stacks across runs)
  - Front phase amplification: echo in Phase 3+ zone is even harder

### 5.3: Echo Rewards
The payoff for facing wrongness.

- **Echo reward tiers** (GDD §6.2):
  - Higher-tier loot and augments (rare/epic guaranteed)
  - Compendium entries unavailable elsewhere (unique unlock)
  - Boss counter tokens for upcoming districts
  - Unique limb salvage with strong memory bonuses (EPIC 1)
  - Premium revival bodies in echo-guarded zones (EPIC 2)
- **EchoRewardPoolSO**: separate from normal reward pools, exclusively better
- **Risk/reward calculation**: echo rewards are always worth it IF you can survive
  - The question is timing: echo in Phase 1-2 = manageable. Echo in Phase 4 = suicide run

### 5.4: Cross-Expedition Echo Persistence
Legends that grow across runs.

- **EchoPersistenceModule** (ISaveModule): tracks unresolved echoes across expeditions
- **Cross-expedition echoes** (GDD §6.3):
  - Never-resolved echoes persist into future expeditions visiting same district
  - Each expedition they persist: +difficulty, +rewards
  - Become "legends other operators whisper about"
  - After 3+ expeditions: classified as Legendary Echo — unique boss-tier encounter
- **Legendary Echo markers**: visible on Scar Map (EPIC 12) and Gate Selection (EPIC 6)
  - Players can seek them out or avoid them
  - Community-shared "there's a Legendary Echo in District 7 Sector 3"

### 5.5: Echo UI & Discovery
How players learn about and track echoes.

- **Gate Screen integration** (EPIC 6): backtrack gates show active echoes and rewards
  - "The Glitch Quarter has 2 echoes: guarding [Rare Limb] and [Boss Counter Token]"
  - Current Front phase shown alongside — risk assessment at a glance
- **In-district markers**: echo zones have visual/audio distortion on approach
  - Wrongness is audible before visible — creepy audio cues
  - Zone map markers show echo locations
- **Scar Map integration** (EPIC 12): echo spiral icons at zone coordinates
- **Echo log**: tracks available echoes, their sources, and estimated difficulty

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Quest/ | Echo source = uncompleted QuestDefinitionSO; Echo IS a quest variant |
| Rewards/ | EchoRewardPoolSO follows existing RewardPoolSO pattern |
| Loot/ | Echo loot tables are enhanced versions of normal tables |
| Roguelite/ (RunModifierStack) | Echo difficulty modifiers stack with run modifiers |
| Persistence/ (ISaveModule) | EchoPersistenceModule for cross-expedition echoes |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 5.1 (Generation) | EPIC 4 (district exit triggers) | — |
| 5.2 (Encounters) | 5.1 | EPIC 3 (Front amplifies echoes) |
| 5.3 (Rewards) | 5.1 | EPIC 1 (limb rewards), EPIC 2 (revival body rewards) |
| 5.4 (Cross-Expedition) | 5.1 | — |
| 5.5 (UI) | 5.1 | EPIC 6 (gate screen), EPIC 12 (Scar Map) |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Echo per skipped goal vs batch | One echo per skipped goal | Each goal's echo is thematically unique |
| Wrongness approach | Mechanic + aesthetic changes, not stat inflation | GDD §6.1: "fundamental changes, not stat inflation" |
| Cross-expedition scaling | +0.25x per expedition | Grows meaningful but doesn't become impossible |
| Legendary echo threshold | 3+ expeditions unresolved | Enough to feel legendary, not immediately |

---

## Vertical Slice Scope

- 5.1 (generation), 5.2 (encounters), 5.3 (rewards) required for GDD §17.4
- 5.4 (cross-expedition) deferred — requires multiple play sessions
- 5.5 (UI) basic markers required, full echo log deferred

---

## Tooling & Quality

| Sub-Epic | BlobAsset | Validation | Editor Tool | Live Tuning | Debug Viz | Simulation |
|---|---|---|---|---|---|---|
| 5.1 Generation | EchoFlavorBlob (mutation weights + CDF) | Mutation weight sum > 0, type coverage check, district coverage build validator | Echo config panel (mutation weight pie chart, seed preview) | BaseDifficultyMultiplier, RewardPerPersistence, MaxEchoesPerDistrict | Echo zone markers on minimap, proximity rings, wrongness gradient | Echo distribution Monte Carlo (1000 exits), persistence tier distribution (100 expeditions) |
| 5.2 Encounters | -- | Encounter definition completeness per mutation type | Encounter preview (original vs echo side-by-side), objective mutation matrix | EnemyUpgradeSpawnMultiplier, DeathResetInvulnSeconds, AggroRadiusMultiplier | Echo zone boundary overlay, enemy mutation icons, DeathReset status | -- |
| 5.3 Rewards | -- | Reward pool weight sum > 0, guaranteed reward validity, quest coverage | Reward distribution pie chart, value estimator, reward comparison tool | -- | -- | Reward Monte Carlo (10000 completions), economy balance check |
| 5.4 Persistence | -- | PersistentEchoRecord uniqueness, tier/persistence consistency | Persistence inspector (live records, promote/inject/clear) | LegendaryThreshold, MythicThreshold, MaxDifficultyMultiplier | -- | Persistence tier distribution (100 expeditions), legendary encounter rate, save size projection |
| 5.5 UI & Discovery | -- | -- | UI preview panel (gate card mock, zone marker, proximity preview) | AudioWrongnessRadius, VisualDistortionRadius, MaxDistortionIntensity | Proximity rings (30m/15m), distance readout, audio layer status | -- |
