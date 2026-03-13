# EPIC 11: Rival Operators

**Status**: Planning
**Priority**: Medium — World-building & emergent content
**Dependencies**: Framework: AI/, Dialogue/, Trading/, Loot/; EPIC 4 (Districts), EPIC 2 (Death), EPIC 12 (Scar Map)
**GDD Sections**: 16.1-16.4 Rival Operators

---

## Problem

The player isn't the only team running the city. NPC operator squads run their own expeditions — leaving bodies, clearing paths, triggering alarms, failing quests. Rivals create emergent world narrative: you find their bodies (lootable), their cleared paths (easier traversal), their failures (new echoes), and occasionally encounter them alive. This makes the world feel populated and consequential beyond the player's actions.

---

## Overview

Rival operators are AI-driven NPC teams that exist within the expedition simulation. They don't literally play the game — they're simulated at a high level (preferred districts, risk tolerance, build style) and their effects are stamped into districts as trail markers, bodies, cleared zones, and encounter opportunities. Occasionally the player meets them alive for trade, competition, or combat.

---

## Sub-Epics

### 11.1: Rival Definition & Simulation
Who the rivals are and how they behave.

- **RivalOperatorSO**: RivalId, TeamName, MemberCount, BuildStyle (Heavy/Stealth/Balanced/Specialist)
  - PreferredDistricts: list of district types they favor
  - RiskTolerance: low (conservative pathing) to high (deep Front dives)
  - EquipmentTier: determines loot quality on their bodies
  - Personality tags: affects dialogue and encounter behavior
- **RivalSimulationSystem**: lightweight per-gate-transition simulation
  - Each rival team has a position in the expedition graph
  - On gate transition: simulate rival movement (which district they entered/left)
  - Simulate rival outcomes: did they complete objectives? Die? Trigger alarms?
  - Probability-based, seed-deterministic
- **Rival state**: Alive (exploring), Dead (bodies in district), Extracted (left expedition)

### 11.2: Rival Trail Markers
Evidence of rival activity.

- **Trail marker types** (GDD §16.1-16.2):
  - Bodies: rival team members who died — full loot (gear, limbs, currency)
  - Cleared paths: areas where enemies are already dead, doors already open
  - Looted POIs: containers already opened, vendors already bought out
  - Failures: uncompleted side goals that became echoes (THEIR echoes, not yours)
  - Trail signs: spent ammo, abandoned campsites, graffiti tags
- **TrailMarkerSystem**: on district entry, check if any rival has been here → stamp markers
  - Marker placement at zone coordinates (deterministic from rival sim + seed)
  - Bodies are interactable (loot) — EPIC 2 body persistence pattern
  - Cleared paths reduce enemy count in zones rival passed through
  - Rival failures seed THEIR echoes (separate from player echoes)
- **Front impact**: rivals can trigger alarms and advance the Front in districts they pass through (GDD §16.2)

### 11.3: Live Rival Encounters
Meeting rivals in person.

- **Neutral encounters** (GDD §16.3):
  - **Trade**: swap limbs, ammo, intel about upcoming districts
  - **Intel**: they share Front status of districts they've been to (updates Scar Map)
  - **Body shop**: rival medic offers revival services for a price
- **Competitive encounters**:
  - **Race**: both teams going for same echo reward or boss counter token
  - **Territory**: rival claims a vendor or safe zone — negotiate or fight
  - **Loot**: they found your old body first and took your gear
- **Hostile encounters** (rare, high Trace):
  - **Contracted**: rival team paid to hunt you (Trace too high)
  - **Desperate**: rival team at death's door tries to take your supplies by force
- **Encounter trigger**: probability roll on district entry, modified by Trace, rival proximity, and district events
- **Dialogue integration**: uses existing Dialogue/ system for trade/negotiation conversations

### 11.4: Meta-Expedition Rivals (Cross-Run)
Your past becomes someone else's rival.

- **PastRunRivalSystem** (GDD §16.4): echoes of YOUR previous expeditions appear as rival teams
  - Use Scar Map data from past expedition as the rival's "AI playbook"
  - Past-you follows your old routes, wearing your old gear
  - Fight ghosts of your own past decisions
- **Implementation**: load previous expedition's Scar Map → convert to RivalOperatorSO
  - Route = district visit order
  - Equipment = what you had at each point
  - Failures = your uncompleted objectives
- **Requires**: expedition history persistence (Roguelite/ RunHistorySaveModule)

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| AI/ | Rival live encounters use AI behavior for combat |
| Dialogue/ | Trade/negotiation conversations |
| Trading/ | Rival trade uses existing commerce system |
| Loot/ | Rival bodies are loot sources |
| EPIC 2 (Death) | Rival bodies follow same persistence as player bodies |
| EPIC 5 (Echoes) | Rival failures generate echoes in districts |
| EPIC 8 (Trace) | High Trace triggers hostile rival encounters |
| EPIC 12 (Scar Map) | Rival markers shown on Scar Map |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 11.1 (Definition & Sim) | EPIC 4 (graph for simulation) | — |
| 11.2 (Trail Markers) | 11.1 | EPIC 5 (rival echoes) |
| 11.3 (Live Encounters) | 11.1 | EPIC 8 (Trace for hostile), EPIC 10 (trade rewards) |
| 11.4 (Meta-Expedition) | 11.1, EPIC 12 (Scar Map data) | — |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Rival simulation fidelity | High-level probability, not real gameplay | Performance — can't simulate full AI teams |
| Rival encounter frequency | ~1 live encounter per 2-3 districts | Frequent enough to matter, rare enough to be memorable |
| Hostile encounter trigger | Trace 4+ only | Should be rare escalation, not constant PvE |
| Meta-expedition rivals | Load from Scar Map save data | Elegant reuse of existing persistence |

---

## Vertical Slice Scope

- 11.1 (definition), 11.2 (trail markers) — minimum for world-building
- 11.3 (live encounters) at least 1 neutral trade encounter
- 11.4 (meta-expedition) deferred — requires multiple play sessions

---

## Tooling & Quality

| Sub-Epic | BlobAsset Pipeline | Validation | Editor Tooling | Live Tuning | Debug Visualization | Simulation & Testing |
|---|---|---|---|---|---|---|
| 11.1 (Definition & Sim) | RivalOperatorBlob | Name uniqueness, behavior tree refs, rate bounds | Rival Operator Designer (personality sliders, behavior preview) | Aggression levels, survival rates | Rival position on district map, state badges | Encounter frequency distribution |
| 11.2 (Trail Markers) | TrailMarkerConfigBlob | Position bounds, marker density limits | Trail marker placement tool on district map | Marker density, enemy reduction % | Marker type icons on zone map | Coverage analysis per district |
| 11.3 (Live Encounters) | RivalEncounterConfigBlob | Encounter probability bounds, dialogue tree refs | Encounter behavior preview | Encounter frequency, hostile thresholds | Encounter trigger radius, aggression state | Player-vs-rival win rate projections |
| 11.4 (Meta-Expedition) | — (loads from persistence) | Source run validity, equipment snapshot integrity | Ghost route visualizer on expedition graph | Recency/death weights | Ghost route overlay on Scar Map | Ghost encounter quality metrics |
