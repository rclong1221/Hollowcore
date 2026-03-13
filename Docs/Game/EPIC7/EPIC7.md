# EPIC 7: Strife System

**Status**: Planning
**Priority**: High — Expedition-level variety
**Dependencies**: Framework: Roguelite/ (RunModifierStack); EPIC 3 (Front), EPIC 4 (Districts), EPIC 14 (Boss)
**GDD Sections**: 8.1 The 12 Strife Cards

---

## Problem

Every expedition needs to feel different even with the same district layout. Strife cards are macro-scale conflicts that ripple into every district — changing map rules, mutating enemies, and adding boss clauses. One card per expedition (rotating every 2 maps in higher loops). This is the expedition-level modifier system that creates replayability beyond procedural generation.

---

## Overview

12 Strife cards, each representing a galactic or local crisis. The active Strife card applies a Map Rule (global gameplay modifier), Enemy Mutation (faction behavior change), Boss Clause (boss fight alteration), and district-specific interactions for 3 thematically linked districts. In higher ascension loops, the card rotates every 2 maps for compounding complexity.

---

## Sub-Epics

### 7.1: Strife Card Data Model
The 12 cards and their effects.

- **StrifeCardDefinitionSO**: CardId, DisplayName, Description, Icon
  - **MapRule**: global modifier (crossfire events, HUD hacks, infection clouds, float pockets, dead zones, loot volatility, etc.)
  - **EnemyMutation**: faction behavior change (shared awareness, adaptive resistance, mobility bursts, EMP weapons, ambushers, etc.)
  - **BossClause**: boss fight modification (reinforcements, system possession, adaptive skin, gravity shifts, prepared defenses, etc.)
  - **DistrictInteractions**: list of (DistrictId, EffectDescription, ModifierSet) — 3 districts per card get amplified effects
- **The 12 cards** (GDD §8.1):
  1. Succession War — crossfire, strike teams, boss buys reinforcements
  2. Signal Schism — HUD hacks, shared awareness, boss possession
  3. Plague Armada — infection, adaptive enemies, boss adaptive resistance
  4. Gravity Storm — float pockets, mobility bursts, arena gravity shifts
  5. Quiet Crusade — dead zones, EMP weapons, boss cooldown taxes
  6. Data Famine — scarce vendors, tougher elites, boss deadlier but fewer adds
  7. Black Budget — stealth routes, ambushers, boss prepared defenses
  8. Market Panic — loot volatility, merc side-swaps, boss buyout deals
  9. Memetic Wild — thought zones, status via audio/visual, boss fake phases
  10. Nanoforge Bloom — surfaces reconfigure, enemies reassemble, boss regen nodes
  11. Sovereign Raid — third-party raiders, mixed factions, boss raid interruption
  12. Time Fracture — rewind pockets, enemies reset once, boss phase rewind

### 7.2: Strife Application System
How the active card modifies gameplay.

- **StrifeActivationSystem**: on expedition start, select Strife card (deterministic from seed)
- **StrifeModifierBridge**: translates Strife card into RunModifierStack entries
  - Map Rule → global modifier applied to all districts
  - Enemy Mutation → enemy behavior modifier (AI parameter overrides)
  - Boss Clause → boss encounter modifier (stored, applied on boss fight)
  - District Interactions → district-specific modifier amplifiers
- **Strife rotation** (higher loops): every 2 maps, active card changes
  - New card drawn from remaining pool (no repeats within expedition)
  - Previous card's modifiers removed, new ones applied
  - Creates compounding complexity in high-ascension runs

### 7.3: Strife-District Interaction
Each card amplifies 3 specific districts.

- **StrifeDistrictEffectSO**: per-card, per-district override
  - Examples from GDD:
    - Succession War + Auction = Auditors hyperactive
    - Signal Schism + Cathedral = stronger hymn pulses
    - Plague Armada + Quarantine = harder + cure rewards
    - Gravity Storm + Skyfall = signature buff
    - Quiet Crusade + Deadwave = easier analog
  - Each interaction: modifier set + description + potential reward bonus
- **Gate Screen integration**: Strife interaction tag shown on forward gates (EPIC 6)
  - Players can see which districts are amplified by current Strife card
  - Strategic: avoid amplified districts or seek them for bonus rewards

### 7.4: Strife Visual & Audio Identity
Each Strife card has a presence.

- **Per-card visual theme**: color tint, particle effect, UI border style
- **Per-card audio cue**: ambient layer that plays throughout expedition
- **District interaction markers**: visual indicators in affected districts
- **Boss clause preview**: Strife effect shown in boss arena pre-fight

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (RunModifierStack) | Strife effects ARE run modifiers — same pipeline |
| Roguelite/ (RunModifierDefinitionSO) | Each card's effects defined as modifier sets |
| AI/ | Enemy mutation overrides AI parameters |
| EPIC 14 (Boss) | Boss clause stored and applied on boss encounter |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 7.1 (Data Model) | None — definition only | — |
| 7.2 (Application) | 7.1 | EPIC 4 (districts), EPIC 3 (Front modifiers) |
| 7.3 (District Interaction) | 7.1, EPIC 4 | EPIC 6 (gate screen tags) |
| 7.4 (Visual/Audio) | 7.1, 7.2 | — |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Card selection | Seed-deterministic | Reproducible, shareable expedition seeds |
| Card rotation in high loops | Every 2 maps | GDD §8: "changes every 2 maps in higher loops" |
| Interaction count per card | 3 districts | Consistent across all 12 cards in GDD |
| Implementation via modifiers | RunModifierStack | Reuses existing framework infrastructure |

---

## Tooling & Quality

| Category | Sub-Epic | Details |
|---|---|---|
| **BlobAsset Pipeline** | 7.1 | `StrifeCardBlob` / `StrifeCardDatabaseBlob` — all 12 cards baked for Burst-compatible runtime access |
| **Validation** | 7.1 | District interaction matrix over-concentration (>3 cards per district), modifier hash existence, card completeness, enum alignment |
| **Validation** | 7.3 | District over-concentration check, modifier hash existence, reward multiplier ranges, interaction type consistency, gate tooltip length |
| **Editor Tooling** | 7.1 | **Strife Card Designer Workstation** — visual card editor, interaction matrix heatmap, balance difficulty rating, card gallery. Follows DIG IWorkstationModule pattern |
| **Live Tuning** | 7.2 | `StrifeLiveTuning` singleton — enemy mutation values (adaptive resistance, elite HP, cloak chance), map rule values (loot scarcity, patrol density), boss clause values |
| **Live Tuning** | 7.3 | `StrifeDistrictLiveTuning` singleton — global reward multiplier scale, global difficulty scale |
| **Debug Visualization** | 7.2 | Active Strife effects HUD — all active modifiers, district interaction status, rotation forecast, modifier entity list |
| **Debug Visualization** | 7.3 | Strife gate tag debug overlay — per-gate interaction status during gate screen |
| **Debug Visualization** | 7.4 | Strife visual debug HUD — visual state, audio crossfade, district markers, boss preview. Gameplay-facing active effects HUD |
| **Simulation** | 7.1 | Expected difficulty delta per card across all districts, compound effect analysis (Strife + Front), rotation compound analysis, reward balance |
| **Simulation** | 7.2 | Selection determinism, rotation no-repeat, modifier injection/cleanup, boss clause storage |
| **Simulation** | 7.3 | Strife + Front phase interaction analysis, player routing simulation, modifier stacking edge cases, gate tag accuracy |

---

## Vertical Slice Scope

- 7.1 (data model) + 7.2 (application) for at least 2-3 Strife cards
- 7.3 (district interaction) for vertical slice districts
- 7.4 (visual) can be placeholder
