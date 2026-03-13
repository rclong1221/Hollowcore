# EPIC 15: Final Bosses & Endgame

**Status**: Planning
**Priority**: Medium — Endgame (post-vertical slice)
**Dependencies**: EPIC 4 (District Graph), EPIC 14 (Boss System), EPIC 7 (Strife), EPIC 8 (Trace), EPIC 9 (Compendium)
**GDD Sections**: 15.1-15.3 Final Bosses, 4.7 Expedition End

---

## Problem

After clearing 5-7 districts, the player faces one of three final bosses. Which boss you face depends on which districts you cleared — an influence meter system that creates different endgame paths. This is the expedition's culmination and needs to feel like a narrative consequence of the player's choices throughout the entire run.

---

## Overview

Three final bosses represent three power factions in the city. An influence meter tracks which faction the player has most disrupted based on which districts they cleared. The dominant faction's boss is the final encounter. Each final boss has unique mechanics, arena, and narrative weight. Victory ends the expedition successfully; defeat triggers the death system with the usual consequences.

---

## Sub-Epics

### 15.1: Influence Meter System
Which final boss you face.

- **InfluenceMeterState**: 3 meters — Infrastructure, Transmission, Market
  - Each district cleared contributes to 1-2 meters
- **District-to-influence mapping** (GDD §15):
  - **Infrastructure** (The Architect): Lattice, Burn, Auction
  - **Transmission** (The Signal): Chrome Cathedral, Nursery, Synapse Row
  - **Market** (The Board): Auction, Wetmarket, Mirrortown
  - Note: some districts contribute to multiple meters (Auction → Infrastructure AND Market)
  - Additional districts contribute lesser amounts (configurable)
- **Final boss selection**: highest meter determines boss
  - Tie-breaking: most recently cleared district's primary faction wins
- **Player visibility**: influence meters shown on Scar Map (EPIC 12), gate screen hints at which boss you're trending toward
- **Strategic implications**: players can target specific final bosses by choosing districts

### 15.2: The Architect
Infrastructure faction final boss.

- **Concept**: the city planner — you're fighting the city itself
  - Triggered by clearing infrastructure districts (Lattice, Burn, Auction)
- **Arena**: the city's control room — walls move, floors shift, infrastructure as weapon
- **Mechanics**: environmental control (conveyor traps, heat vents, structural collapse)
  - Phases: defensive → offensive → desperate (city starts destroying itself)
  - Uses elements from cleared infrastructure districts against you
- **Variant clauses**: scaled by which infrastructure districts were cleared + Strife + Trace
- **Narrative**: "You dismantled my city. Now you'll see what it can really do."

### 15.3: The Signal
Transmission faction final boss.

- **Concept**: an AI trying to save humanity by absorbing everyone
  - Triggered by clearing transmission districts (Cathedral, Nursery, Synapse)
- **Arena**: network space — digital environment, signals as terrain
- **Mechanics**: psychic/digital attacks, minion possession, reality distortion
  - Phases: persuasion (tries to convince you) → force (tries to absorb you) → desperation (lashes out)
  - Uses elements from cleared transmission districts
- **Variant clauses**: scaled by transmission districts cleared + Strife + Trace
- **Narrative**: "I only wanted to save everyone. You just wouldn't listen."

### 15.4: The Board
Market faction final boss.

- **Concept**: corporate executives in a network-only space — no good options
  - Triggered by clearing market districts (Auction, Wetmarket, Mirrortown)
- **Arena**: corporate boardroom that defies physics — impossible geometry, contractual traps
- **Mechanics**: economic warfare — buyout attempts, contract traps, merc waves
  - Phases: negotiation (offers deals) → hostile takeover (combat) → liquidation (scorched earth)
  - Uses elements from cleared market districts
- **Variant clauses**: scaled by market districts cleared + Strife + Trace
- **Narrative**: "Everything has a price. Even you. Especially you."

### 15.5: Expedition Victory & Summary
What happens after winning.

- **Victory sequence**: final boss death → cinematic → full expedition summary
- **Expedition summary screen**:
  - Full Scar Map review with timeline (EPIC 12)
  - Statistics: districts cleared, deaths, echoes completed, Trace peak, time
  - Compendium entries awarded
  - Limbs collected / chassis state
  - Rival encounters summary
- **Meta-progression rewards**:
  - Compendium entries for final boss (permanent unlock)
  - Meta-currency (Roguelite/ MetaBank)
  - Unlocks for higher ascension tiers
- **New Game+ / Ascension**: repeat with harder modifiers
  - Strife cards rotate every 2 maps
  - Fronts advance faster
  - Bosses gain additional variant clauses
  - New enemy variants appear
  - Meta-expedition rivals from this run appear in future runs

### 15.6: Ascension Loop
Higher difficulty tiers for replayability.

- **AscensionLevel**: escalating difficulty tiers (Roguelite/ AscensionDefinitionSO)
- **Per-level modifiers**:
  - Ascension 1-3: Front speed increase, elite frequency
  - Ascension 4-6: Strife rotation (card changes every 2 maps), boss clause additions
  - Ascension 7-10: multiple simultaneous Strife cards, new enemy variants, reduced revival options
- **Ascension rewards**: exclusive Compendium entries, cosmetic rewards, leaderboard ranking
- **Leaderboard**: seed + ascension level + completion time + deaths + score

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (RunLifecycleSystem) | Expedition victory → RunEnd → MetaScreen |
| Roguelite/ (AscensionDefinitionSO) | Ascension tiers use existing modifier system |
| Roguelite/ (MetaBank) | Meta-currency awards on victory |
| Roguelite/ (RunHistorySaveModule) | Expedition summary persisted |
| EPIC 14 (Boss System) | Final bosses use same variant clause system |
| EPIC 12 (Scar Map) | Victory summary centers on Scar Map |
| EPIC 9 (Compendium) | Final boss entries are meta-progression rewards |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 15.1 (Influence Meters) | EPIC 4 (district completion tracking) | — |
| 15.2 (The Architect) | 15.1, EPIC 14 (boss system) | — |
| 15.3 (The Signal) | 15.1, EPIC 14 | — |
| 15.4 (The Board) | 15.1, EPIC 14 | — |
| 15.5 (Victory Summary) | 15.1, any of 15.2-15.4 | EPIC 12, EPIC 9 |
| 15.6 (Ascension) | 15.5 | EPIC 7 (Strife rotation) |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Final boss count | 3 (not 1, not 15) | GDD §15: three factions, three endings |
| Selection method | Influence meter from districts cleared | Player choice determines ending — not random |
| Ascension loop | Modifier stacking, not new content | Replayability from system interaction, not content volume |
| When unlocked | After 5-7 districts cleared | GDD §4.1: configurable threshold |

---

## Vertical Slice Scope

- 15.1 (influence meters) basic version if vertical slice reaches 3 districts
- 15.2-15.4: ONE final boss for vertical slice (whichever matches the 3 chosen districts)
- 15.5 (summary) basic version
- 15.6 (ascension) deferred — post-launch content

---

## Tooling & Quality

| Sub-Epic | BlobAsset Pipeline | Validation | Editor Tooling | Live Tuning | Debug Viz | Simulation |
|---|---|---|---|---|---|---|
| 15.1 (Influence Meters) | DistrictInfluenceMapSO -> InfluenceMapBlob, InfluenceMeterSO -> InfluenceBlob (threshold curve) | Meter threshold bounds, all 15 districts represented, all 3 factions reachable via valid paths, balance check | Influence map overview table, threshold visualization, path simulator (1000 random paths), balance dashboard | Force meter values, force final boss faction, override districts cleared | Meter bars, contribution history, final boss prediction | Path simulator: faction reachability % across random expedition paths |
| 15.2 (The Architect) | ArchitectDefinitionSO -> BossBlob + ArchitectBlob (phase infra configs, dialogues) | Phase thresholds, infra system coverage, integrity drain rate, district enhancement IDs | (Shared with EPIC 14 Boss Designer Workstation) | Structural integrity, drain rate, command cooldown, infra damage, disable district scaling | Infra system status grid, integrity gauge, ShieldWall HP, command cooldown | Architect simulator: integrity at kill, arena collapse wipe rate, infra damage contribution, shield wall timing |
| 15.3 (The Signal) | SignalDefinitionSO -> BossBlob + SignalBlob (node configs, corruption thresholds, dialogues) | Corruption thresholds ordered, decay rate bounds, node counts, dialogue count, district enhancement IDs | (Shared with EPIC 14 Boss Designer Workstation) | Corruption level, decay rate, possession cooldown, signal strength, disable UI distortion/inversion | Corruption meter, node map, possession status, signal state machine | Signal simulator: peak corruption, corruption death rate, nodes destroyed/phase, possession frequency |
| 15.4 (The Board) | BoardDefinitionSO -> BossBlob + BoardBlob (budget costs, member profiles, asset configs) | Budget economy balance, contract durations, member count, liquidation rate, district enhancement IDs | (Shared with EPIC 14 Boss Designer Workstation) | Budget, regen rate, liquidation progress, disable contracts/mercs/buyouts, contract duration scale | Budget meter, board member status, active contracts, arena assets, liquidation progress | Board simulator: budget at transitions, merc waves spawned, contract active time, liquidation wipe rate |
| 15.5 (Victory Summary) | (No additional blobs — reads existing run data) | Score formula range, grade boundaries, meta-currency amounts, cinematic/Compendium references | (N/A — summary is UI-driven) | Currency multiplier, skip cinematic/map review, force score, force ascension unlock | Score breakdown, reward distribution log, summary phase state | (N/A — summary is presentation, not gameplay) |
| 15.6 (Ascension) | AscensionTierSO collection -> AscensionBlob (all 11 tiers, Burst-accessible) | Monotonic difficulty increase across all fields, revival decrease, Strife slot bounds, tier 0 = identity, tier coverage | **Ascension Tier Builder**: visual ladder, forced modifier overlay, cumulative difficulty graph, reward multiplier curve, economy projection | Force tier, override multipliers, unlock all tiers, override revivals | Modifier stack display, tier badge, revival counter, Strife slot display | **Difficulty Curve Analyzer**: composite difficulty score, player vs enemy power ratio, economy projection (hours to unlock), revival impact analysis |
