# EPIC 1: Chassis & Limb System

**Status**: Planning
**Priority**: Critical — Core identity mechanic
**Dependencies**: Framework: Items/, Traits/, Combat/, Animation/
**GDD Sections**: 3.1 Limb Loss & Salvage, 3.2 Limb Memory, 3.3 Enemy Limb Theft

---

## Problem

The player's body IS the build system. Limbs are modular hardware — blown off in combat, salvaged from the environment, ripped from enemies mid-fight. Every limb changes your capabilities, movement, and combat options. This is Hollowcore's central identity mechanic (Cyborg Justice meets roguelite itemization) and nothing in the framework handles body-part-level modularity.

---

## Overview

The Chassis system manages the player's modular body. Six slots (LeftArm, RightArm, LeftLeg, RightLeg, Torso, Head) each hold a `LimbDefinition` that contributes stats, abilities, and district-affinity bonuses. Limbs can be destroyed by damage, swapped at body shops, salvaged from the environment, or ripped from staggered enemies mid-combat.

---

## Sub-Epics

### 1.1: Chassis State & Slot Architecture
Core data model for the modular body.

- **ChassisSlot enum**: LeftArm, RightArm, LeftLeg, RightLeg, Torso, Head
- **ChassisState** (IComponentData on player or child entity): 6 Entity references (one per slot), integrity flags
- **LimbDefinitionSO** (ScriptableObject): SlotType, StatBlock (damage, armor, speed, etc.), DistrictAffinity, Rarity, Durability, SpecialAbilityId, VisualPrefab, LimbMemoryEntries
- **LimbInstance** (IComponentData on limb entity): runtime state — current durability, memory bonus active, temporary flag, expiration time
- **ChassisStatAggregator**: sums all equipped limb stats → feeds into existing EquippedStatsSystem modifier pipeline
- Respect 16KB player archetype limit — ChassisState on child entity if needed

### 1.2: Limb Loss & Degradation
Damage to specific body parts.

- **LimbDamageZone**: maps hitbox regions (Head, Torso, Arms, Legs) to ChassisSlots
- **LimbIntegrity** tracking: limbs have HP independent of player HP
- **LimbDestructionSystem**: when limb integrity hits 0, limb is destroyed — empty slot
- **Gameplay penalties per missing limb**:
  - Missing arm: one-handed weapons only, reduced melee damage
  - Missing both arms: cannot attack, can only kick/headbutt
  - Missing leg: movement speed halved, cannot sprint
  - Missing both legs: crawling movement only (GDD §3.1)
  - Missing head: instant death (head is always fatal)
- **Visual state**: destroyed limbs show stump/sparking model variant
- Integration with existing DamageEvent pipeline (route limb zone damage through LimbDamageZone)

### 1.3: Limb Salvage & Equipping
Finding and equipping replacement limbs.

- **LimbPickup** (IComponentData): world-placed salvageable limbs with LimbDefinitionSO reference
- **LimbEquipSystem**: swap limb into chassis slot, drop current limb as pickup
- **Salvage sources**:
  - Environment: scattered limbs, body shops, crates
  - Enemy corpses: loot table drops LimbDefinitionSO items
  - Quest rewards
- **Body Shop interaction**: NPC vendor for higher-quality limb swaps (uses existing Trading/ system)
- **Quick-swap UI**: radial menu showing available limbs per slot

### 1.4: Enemy Limb Theft (Rip System)
The Cyborg Justice mechanic — rip limbs off staggered enemies.

- **RipTarget** (IEnableableComponent): enabled on enemies when staggered/downed
- **RipWindowSystem**: monitors enemy stagger state → enables RipTarget for N seconds
- **RipInteraction**: player interacts with RipTarget → long, interruptible animation
  - Player fully exposed during rip (GDD §3.3)
  - Getting hit cancels the rip
  - Co-op: teammates can cover you
- **Ripped limb quality tiers** (GDD §3.3):
  - Common enemies: temporary (30-60 seconds, then breaks)
  - Elite enemies: lasts rest of district
  - Bosses/mini-bosses: permanent salvage
- **RipLimbTable**: per-enemy-type definition of what limb is rippable and from which slot
- **Risks**: some limbs carry curses/instabilities (plague strains, feedback loops)

### 1.5: Limb Memory System
Salvaged limbs retain their previous owner's district knowledge.

- **LimbMemoryEntry**: DistrictId, BonusType (DamageResist, MoveSpeed, ResourceEfficiency), BonusValue
- **LimbMemoryActivation**: when in the matching district, bonus activates
  - Small individual bonus (~5-10% per limb)
  - Full chassis of matching limbs = significant advantage (~25-40%)
- **Memory bonus stacking**: additive, fed through EquippedStatsSystem
- **Boss limbs**: strongest memory bonuses, always worth keeping
- **Strategic loop**: salvage from district A → advantage when returning to district A → encourages backtracking with purpose

### 1.6: Chassis Visual & Animation Integration
Visual representation of the modular body.

- **Per-slot mesh swapping**: each limb slot has a socket on the character model
- **LimbVisualBridge** (managed MonoBehaviour): reads ChassisState → swaps mesh/material per slot
- **Stump visuals**: empty slot shows damaged stump with sparks/exposed wiring
- **Crawling locomotion**: animation state for missing both legs
- **One-arm animations**: weapon hold/fire animations for single-arm state
- Integration with existing Animation/ workstation for authoring limb-specific animation overrides

### 1.7: Chassis Persistence
Save/load chassis state across districts and sessions.

- **ChassisSaveModule** (ISaveModule): serializes equipped limb IDs, durability, memory state
- Chassis persists across district transitions within an expedition
- Chassis lost on full wipe (new expedition = new body)
- Temporary ripped limbs expire on district exit (commons) or expedition end (elites)

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Items/ (EquippedStatsSystem) | LimbStatAggregator feeds modifiers into existing pipeline |
| Combat/ (DamageEvent) | LimbDamageZone routes regional damage to limb integrity |
| Traits/ (CharacterAttributes) | Limb stats contribute to base attributes |
| Animation/ | Limb-specific animation overrides, crawling/one-arm states |
| Trading/ | Body shop vendor uses existing trade flow |
| Persistence/ (ISaveModule) | ChassisSaveModule follows existing pattern |
| Loot/ | Enemy corpse limb drops go through loot tables |
| Interaction/ | RipInteraction uses existing interactable system |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 1.1 (Chassis State) | None — foundation | — |
| 1.2 (Limb Loss) | 1.1 | 1.6 (visuals) |
| 1.3 (Salvage) | 1.1 | 1.5 (memory) |
| 1.4 (Rip System) | 1.1, 1.3 | 1.2 (loss creates need) |
| 1.5 (Limb Memory) | 1.1, EPIC 4 (districts) | — |
| 1.6 (Visuals) | 1.1 | 1.2, 1.3 |
| 1.7 (Persistence) | 1.1 | All others |

---

## Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Chassis on player vs child entity | Child entity (ChassisLink) | 16KB archetype limit on player ghost prefab |
| Limb as entity vs component | Entity (with LimbInstance) | Each limb has independent state (durability, memory, expiration) |
| Rip as interaction vs ability | Interaction system | Long interruptible action fits interact pattern, not instant ability |
| Memory bonus magnitude | 5-10% per limb | GDD: "small enough not mandatory, meaningful enough to influence" |
| Temporary limb expiration | Timer-based | Commons: 30-60s fixed. Elites: flag cleared on district exit |

---

## Vertical Slice Scope (GDD §17.4)

For the 2-3 district vertical slice:
- 1.1 (state), 1.2 (loss), 1.3 (salvage) are required
- 1.4 (rip) is a marquee feature — include at least basic version
- 1.5 (memory) needs at least 2 districts to matter
- 1.6 (visuals) can use placeholder mesh swaps initially
- 1.7 (persistence) required for cross-district play

---

## Tooling & Quality

Summary of production tooling, validation, and testing infrastructure across sub-epics.

| Sub-Epic | Editor Tool | Blob Pipeline | Validation | Live Tuning | Debug Viz |
|---|---|---|---|---|---|
| 1.1 Chassis State | Chassis Workstation (Limb Browser, Loadout Previewer, Balance Matrix, ID Audit), Custom LimbDefinitionSO Inspector | LimbDefinitionDatabase blob (all limb defs) | LimbDefinitionSO.OnValidate + build-time duplicate ID / coverage checks | ChassisRuntimeConfig singleton (regen rate, damage mult, memory mult, temp duration) | Chassis debug overlay (integrity bars, slot labels, penalty state, memory bonuses), Scene gizmos |
| 1.2 Limb Loss | -- | -- | -- | LimbDamageConfig singleton (zone multipliers, speed penalties, regen cooldown) | Limb integrity HUD (per-slot bars, DPS ticker), damage routing trace log |
| 1.3 Salvage | Salvage Source Map (workstation module), Limb Comparison Popup | -- | LimbPickup bounds validation, loot table limb entry validation | -- | -- |
| 1.4 Rip System | -- | LimbCurseDatabase blob (curse definitions) | -- | RipRuntimeConfig singleton (window durations, rip times, range, curse chance) | Rip target indicators (stagger circles, window timers), rip state trace log |
| 1.5 Limb Memory | -- | -- | -- | LimbMemoryConfig singleton (global multiplier, per-type caps, activation threshold) | Memory bonus overlay (attunement count, bonus breakdown, district transition flash) |
| 1.6 Visuals | Chassis Visual Preview (workstation module), ChassisVisualBridge Inspector | -- | -- | -- | Visual state debug overlay (slot labels at sockets, animation layer readout, dirty flag) |
| 1.7 Persistence | -- | -- | Save module TypeId collision check, round-trip byte validation, graceful degradation on unknown IDs | -- | -- |

**Chassis Workstation** (`Assets/Editor/ChassisWorkstation/ChassisWorkstationWindow.cs`) is the central editor tool, following the DIG workstation pattern (sidebar tabs, IWorkstationModule interface). It hosts: Limb Browser, Loadout Previewer, Balance Matrix, ID Audit, Salvage Source Map, and Visual Preview as modular tabs.

**Simulation & Testing** coverage: EPIC 1.2 (damage distribution, penalty state machine, routing performance), EPIC 1.3 (limb economy Monte Carlo, body shop pricing), EPIC 1.4 (rip success rate Monte Carlo, curse distribution, rip timing performance), EPIC 1.5 (memory bonus balance, district transition bonuses, memory system performance), EPIC 1.7 (district transition persistence, save round-trip, file size budget)
