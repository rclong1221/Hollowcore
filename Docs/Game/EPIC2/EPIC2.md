# EPIC 2: Soul Chip, Death & Revival

**Status**: Planning
**Priority**: Critical — Core death loop
**Dependencies**: Framework: Combat/CorpseLifecycle, Persistence/, Party/; EPIC 1 (Chassis)
**GDD Sections**: 3.4 The Soul Chip, 13.1-13.7 Death & Revival

---

## Problem

Death in Hollowcore is not a reset — it's a spatial, narrative, and economic event. Your body stays where it fell with all gear. The district claims it and may reanimate it against you. Revival means finding a new body somewhere in the expedition. The quality of that body depends on what's available and how far you're willing to go. Each death compounds: bodies scatter across districts, revival options thin out, and your soul chip degrades.

---

## Overview

The death system turns every death into a persistent world event. The Soul Chip is the player's consciousness — transferable between bodies. On death, the chip must be recovered and installed in a new body. The framework's existing CorpseLifecycle (EPIC 16.3) handles the physical corpse; this epic builds the consciousness transfer, revival search, body reanimation, and death spiral mechanics on top.

---

## Sub-Epics

### 2.1: Soul Chip Core
The consciousness persistence layer.

- **SoulChipState** (IComponentData): TransferCount, DegradationLevel, ChipEntity (the actual persistent identity)
- **SoulChipDegradation**: after 3+ transfers, stat penalties and "memory glitches" (GDD §13.6)
  - Transfer 1-2: no penalty
  - Transfer 3: -5% all stats
  - Transfer 4: -10% all stats + occasional input delay
  - Transfer 5+: -15% all stats + visual glitches + memory loss (lose some Compendium pages)
- **SoulChipRecovery**: on death, chip drops at body location as interactable entity
  - Solo: auto-eject to nearest safe point (drone insurance) OR stays at body
  - Co-op: teammate picks up chip from body (GDD §13.2)

### 2.2: Body Persistence & Inventory
Dead bodies as world objects with full state.

- **DeadBodyState** (IComponentData): extends existing CorpseState
  - Full inventory snapshot: weapons, limbs, consumables, currency
  - ChassisState snapshot: which limbs were equipped
  - Position, district, zone
- **DeadBodyMarker**: persists in district state across gate transitions (written to district save data)
- **Body interaction**: approach dead body → UI shows inventory preview → retrieve items individually or bulk
- **Body degradation**: over time/Front advancement, body becomes harder to reach but loot persists
- Integration with Persistence/ ISaveModule for cross-district body tracking

### 2.3: Revival System
Finding and inhabiting a new body.

- **RevivalBodyDefinitionSO**: body quality tier, available limb slots, base stats, location difficulty
- **RevivalBodySpawner**: on player death, spawns revival opportunities in the expedition
  - **Tier 1 (Cheap/Close)**: junky body, possibly missing limbs, safe-ish location. Free
  - **Tier 2 (Mid)**: functional body, standard limbs, contested territory. Moderate cost
  - **Tier 3 (Premium)**: military-grade or district-specialized, deep in hostile zone. Expensive
- **Revival location logic**:
  - First death: nearby revival body in current district
  - Subsequent deaths: revival bodies may spawn in PREVIOUS districts (GDD §13.6)
  - Each death forces deeper revival searches — the spatial death spiral
- **Solo revival methods** (GDD §13.3):
  - Drone insurance: automated recovery to nearest Tier 1 body (limited uses per expedition)
  - Revival terminals: fixed locations, activated pre-death or on timer
  - Continuity cache: rare pre-placed backup (premium)
- **Co-op revival** (GDD §13.2):
  - Teammate recovers soul chip, carries to body shop / scavenged frame / premium chassis
  - Carry mechanic: downed player model on teammate's back, reduced movement speed
  - Revive interaction at body: long channel, interruptible

### 2.4: Body Reanimation
The district turns your corpse against you.

- **ReanimationDefinitionSO** per district: what the district does with your body
  - Necrospire: Recursive Specter wearing your face, knows your loadout
  - Old Growth: Root Runner assimilated into the Garden, your augments become forest
  - The Nursery: AI learns your combat patterns from neural data
  - The Quarantine: plague mutates through your body, wearing your armor
  - Chrome Cathedral: faithful "ascend" your corpse, fights alongside Seraphim
  - Mirrortown: Hollow One takes your face, co-op teammates may not recognize it
  - Glitch Quarter: body caught in loop, multiple slightly-wrong copies
- **ReanimationSystem**: on body claim timer (or Front reaching body), converts DeadBody into enemy entity
  - Enemy has YOUR equipped weapons and limb stats (scaled by difficulty)
  - Classified as mini-boss
  - Defeating reanimated body: recover gear + bonus loot (district modifications)
- **Co-op moment**: teammates fight "you" — the game uses your actual loadout and appearance

### 2.5: Death Spiral & Full Wipe
Escalating consequences of repeated death.

- **DeathCounter** per expedition: tracks total deaths and per-district deaths
- **Spiral mechanics** (GDD §13.6):
  - Nearby revival bodies used up — each death forces deeper search
  - Previous bodies reanimated and hostile
  - Soul chip degradation accumulates
  - The expedition becomes a body-recovery roguelike within the roguelite
- **Full Wipe conditions** (GDD §13.7):
  - Solo: death with no revival options remaining
  - Co-op: total party kill with no chips recoverable
  - Result: expedition ends, run-level progress lost
  - Compendium entries from completed districts are kept (meta-progression survives)
- **Near-death tension**: UI shows remaining revival options, chip degradation level, body locations on Scar Map

### 2.6: Death UI & Feedback
Player-facing death experience.

- **Death screen**: shows where you died, what you had, where revival options are
- **Scar Map integration**: skull icons at body locations with gear preview on hover
- **Revival selection UI**: shows available bodies sorted by quality/distance/danger
- **Soul chip transfer cinematic**: brief visual of consciousness moving to new body
- **Chip degradation warning**: visual/audio feedback as degradation increases
- **Co-op death screen**: shows teammate carrying your chip, revival body options near them

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Combat/ CorpseLifecycle (EPIC 16.3) | DeadBodyState extends CorpseState, body stays visible |
| Persistence/ ISaveModule | DeadBodySaveModule persists bodies across districts |
| Party/ | Co-op chip recovery, carry mechanic, group revival |
| AI/ | Reanimated bodies use AI systems with player loadout data |
| EPIC 1 (Chassis) | Dead body stores full chassis snapshot, new body has different chassis |
| EPIC 12 (Scar Map) | Skull markers, body locations, revival node positions |
| EPIC 4 (District Graph) | Revival bodies may spawn in previous districts |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 2.1 (Soul Chip) | None — foundation | — |
| 2.2 (Body Persistence) | 2.1 | EPIC 1 (chassis snapshot) |
| 2.3 (Revival) | 2.1, 2.2 | EPIC 4 (cross-district revival) |
| 2.4 (Reanimation) | 2.2 | EPIC 3 (Front triggers reanimation) |
| 2.5 (Death Spiral) | 2.1, 2.2, 2.3 | 2.4 |
| 2.6 (Death UI) | 2.1, 2.3 | 2.4, EPIC 12 |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Soul chip as entity vs component | Component on persistent player identity entity | Chip IS the player — survives body destruction |
| Body persistence storage | ISaveModule (TypeId TBD) | Must survive district transitions + sessions |
| Reanimation trigger | Timer + Front proximity | Bodies in Front path get claimed faster |
| Revival body spawning | Deterministic from expedition seed + death count | Reproducible, seed-fair |
| Co-op chip carry | Physics-based carry (reduced speed) | Creates tension — teammates must escort |

---

## Vertical Slice Scope

- 2.1 (soul chip), 2.2 (body persistence), 2.3 (revival) required
- 2.4 (reanimation) is a GDD §17.4 explicit requirement — at least 1 district's reanimation type
- 2.5 (death spiral) emerges naturally from 2.1-2.4 working together
- 2.6 (death UI) can be minimal initially (text-based revival selection)

---

## Tooling & Quality

| Sub-Epic | Editor Tool | Blob Pipeline | Validation | Live Tuning | Debug Viz |
|---|---|---|---|---|---|
| 2.1 Soul Chip Core | -- | -- | Tier/transfer-count invariants | -- | Chip state overlay, degradation tier color band |
| 2.2 Body Persistence | -- | -- | Save module field validation, inventory integrity | -- | In-world skull gizmos, Scar Map debug layer |
| 2.3 Revival System | Revival Body Inspector (tier-colored header, stat bars, slot diagram) | RevivalBodyBlob from RevivalBodyDefinitionSO | Unique IDs, stat ranges, slot consistency, per-tier constraints | -- | -- |
| 2.4 Body Reanimation | Reanimation Preview Inspector (timeline, enemy stat preview, loot table) | ReanimationDatabase blob (15 entries) | Per-district definition completeness, prefab component checks | -- | Reanimation progress ring, phase colors, timer |
| 2.5 Death Spiral | -- | -- | Cross-field consistency (deaths, resources, tier mirror) | -- | Spiral state HUD, timeline graph, border tint |
| 2.6 Death UI | Death UI Preview Window (panel preview, mock data, cinematic scrubber) | -- | -- | -- | Phase state machine overlay, co-op carrier debug |
