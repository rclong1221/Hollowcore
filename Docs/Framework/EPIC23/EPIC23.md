# EPIC 23 - Modular Rogue-Lite Core Loop Framework

**Status:** PLANNED
**Dependencies:** DIG.Shared assembly, EPIC 16.6 (Loot/Economy), EPIC 16.14 (Progression), EPIC 16.15 (Persistence), EPIC 17.4 (Lobby/Difficulty)
**Goal:** Provide a self-contained, game-agnostic rogue-lite framework module that any DIG game can opt into. Games plug in their own level technology, art, and content.

---

## Overview

The DIG framework has robust combat, AI, loot, progression, and persistence — but no rogue-lite game loop infrastructure. EPIC 23 adds a `DIG.Roguelite` assembly module that handles run lifecycle, meta-progression, zone orchestration, difficulty scaling, rewards, and designer tooling. Games implement one interface (`IZoneProvider`) to plug in their level generation.

**Assembly:** `DIG.Roguelite` — references only `DIG.Shared`. No other assembly references it. To exclude from a non-rogue-lite game: delete the folder or add `defineConstraints: ["DIG_ROGUELITE"]`.

---

## What Existing Systems Cover (Not Repeated Here)

| System | EPIC | Used By |
|--------|------|---------|
| DifficultyDefinitionSO (health/damage/spawn scaling) | 17.4 | 23.4 wraps with runtime modifiers |
| EncounterState + EncounterTriggerDefinition | 15.32 | 23.3 reuses for boss zones |
| EnemySpawnerComponents (frame-budgeted, seeded) | 15.32 | 23.3 creates spawners per zone |
| LootTableSO + LootPoolEntry (weighted, rarity, conditions) | 16.6 | 23.5 resolves item rewards |
| ISaveModule (binary, CRC32, migration) | 16.15 | 23.2 adds TypeIds 16-18 |
| CurrencyTransactionSystem | 16.6 | 23.2 routes meta-currency |
| EquippedStatsSystem (modifier pipeline) | 16.14 | 23.2/23.4 applies stat modifiers |
| PlayerProgression + XPAwardSystem | 16.14 | 23.1 run XP integration |
| LobbyState | 17.4 | 23.1 adds RunConfigId |

---

## What EPIC 23 Adds

| System | Description | Sub-EPIC |
|--------|-------------|----------|
| Run Lifecycle | State machine, permadeath, seed determinism, timers | 23.1 |
| Meta-Progression | Permanent unlocks, meta-currency, run history persistence | 23.2 |
| Zone Generation | IZoneProvider interface, zone sequences, encounter direction | 23.3 |
| Difficulty Modifiers | Stackable modifiers, ascension/heat system, runtime scaling | 23.4 |
| Rewards & Choices | Choose-N-of-pool, shops, risk-reward events | 23.5 |
| Analytics & Tooling | Run statistics, history, editor workstation, run simulator | 23.6 |

---

## Sub-EPICs

### EPIC 23.1 - Run Lifecycle & State Machine

**Goal:** Core state machine that drives any rogue-lite game loop.

| Component | Description |
|-----------|-------------|
| `RunState` | Singleton IComponentData: RunId, Seed, Phase, ZoneIndex, Score, RunCurrency |
| `RunPhase` | Enum: None → Lobby → Preparation → ZoneLoading → Active → BossEncounter → ZoneTransition → RunEnd → MetaScreen |
| `RunConfigSO` → `RunConfigBlob` | Designer-authored config baked to BlobAsset (zone count, difficulty curve, time limits, economy) |
| `RunSeedUtility` | Burst-native deterministic seed chain: master → zone → encounter → reward |

---

### EPIC 23.2 - Meta-Progression & Permanent Unlocks

**Goal:** Persistent account-level progression that survives across runs.

| Component | Description |
|-----------|-------------|
| `MetaBank` | IComponentData: MetaCurrency, lifetime stats, best scores |
| `MetaUnlockEntry[]` | Buffer: unlock tree nodes (stat boosts, starter items, abilities, cosmetics) |
| `MetaUnlockTreeSO` | Designer-authored node graph with costs and prerequisites |
| `MetaProgressionSaveModule` | ISaveModule TypeId=16: persists unlocks + currency |
| `RunHistorySaveModule` | ISaveModule TypeId=17: persists last 100 run summaries |

---

### EPIC 23.3 - Zone Generation & Encounter Direction

**Goal:** Framework orchestrates WHEN zones load and WHAT spawns. Games control HOW zones are built.

| Component | Description |
|-----------|-------------|
| `IZoneProvider` | Interface: Initialize(seed, index, definition), IsReady, Activate(), Deactivate() |
| `IZoneClearCondition` | Interface: IsCleared(). Default: all enemies dead |
| `ZoneDefinitionSO` | Zone template: type, difficulty, encounter pool, reward pool, extension data |
| `ZoneSequenceSO` | Ordered/weighted zone list per run config (fixed, random, or player-choice) |
| `EncounterPoolSO` | Weighted enemy templates with difficulty gating |
| `ZoneType` | Enum: Combat, Elite, Boss, Shop, Event, Rest, Treasure |

---

### EPIC 23.4 - Run Modifiers & Difficulty Scaling

**Goal:** Stackable modifiers that alter run parameters. Enables heat/ascension systems.

| Component | Description |
|-----------|-------------|
| `RunModifierDefinitionSO` | Polarity (positive/negative/neutral), target (stat/mechanic/economy), stacking rules |
| `RunModifierStack[]` | Buffer on RunState entity: active modifiers with stack counts |
| `AscensionDefinitionSO` | Difficulty tiers with forced modifiers + reward multipliers |
| `RuntimeDifficultyScale` | Singleton: effective difficulty after zone curve + all modifiers |

---

### EPIC 23.5 - Reward & Choice Systems

**Goal:** Rewards on zone clear, shops, risk-reward events. The dopamine loop.

| Component | Description |
|-----------|-------------|
| `RewardDefinitionSO` | Type (Item/Currency/StatBoost/Ability/Modifier/Healing), rarity, values |
| `RewardPoolSO` | Weighted entries with zone/rarity constraints, configurable choice count |
| `PendingRewardChoice[]` | Buffer: generated options awaiting player selection |
| `ShopInventoryEntry[]` | Buffer: purchasable items with run-currency prices |
| `RunEventDefinitionSO` | Narrative text, choices with rewards/curses/probabilities |

---

### EPIC 23.6 - Run Analytics, History & Editor Tooling

**Goal:** Tracking, persistence, and designer-facing balance tools.

| Component | Description |
|-----------|-------------|
| `RunStatistics` | IComponentData: kills, damage, items, zones cleared, timing |
| `RunHistoryEntry[]` | Buffer on MetaBank: completed run summaries |
| **RunWorkstation** | Editor window with 5 modules: Zone Sequence, Encounter Pool, Rewards, Modifiers, Meta Tree |
| **RunSimulator** | Dry-run + Monte Carlo (1000 runs) balance testing without play mode |

---

## Key Design Principles

1. **Framework, not game** — no references to specific themes, art, or level technology
2. **Self-contained assembly** — `DIG.Roguelite` asmdef. Delete folder to exclude. Zero inbound references
3. **Interface-based zones** — `IZoneProvider` is the only contract. Voxel, prefab, tile, scene — all valid
4. **Dedicated entity, NOT player** — RunState + buffers on separate singleton (avoids 16KB archetype limit)
5. **Networking-agnostic** — `[GhostComponent]` attributes harmless without NetCode, free replication with it
6. **Seed determinism** — all random selection derived from master seed via `math.hash` chain
7. **RequireForUpdate independence** — missing sub-epic = systems never run. Zero compile-time cross-deps

---

## Module Independence

| Sub-Epic | Requires | Optional Enhancement From |
|----------|----------|---------------------------|
| 23.1 (Run Lifecycle) | None (core) | 23.3, 23.4, 23.5, 23.6 |
| 23.2 (Meta-Progression) | 23.1 | 23.4, 23.6 |
| 23.3 (Zone Generation) | 23.1 | 23.4, 23.5 |
| 23.4 (Modifiers) | 23.1 | 23.3, 23.5 |
| 23.5 (Rewards) | 23.1 | 23.3, 23.4 |
| 23.6 (Analytics/Tooling) | 23.1 | 23.2, 23.3, 23.4, 23.5 |

---

## Implementation Order

| Order | Sub-Epic | Notes |
|-------|----------|-------|
| 1 | 23.1 — Run Lifecycle | Foundation, must come first |
| 2a | 23.3 — Zone Generation | Parallel with 23.5 |
| 2b | 23.5 — Rewards | Parallel with 23.3 |
| 3 | 23.4 — Modifiers | After core loop works |
| 4 | 23.2 — Meta-Progression | After runs are playable |
| 5 | 23.6 — Analytics & Tooling | Incremental alongside all |
