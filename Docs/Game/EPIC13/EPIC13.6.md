# EPIC 13.6: Vertical Slice — THE NECROSPIRE

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: Critical — First playable district, vertical slice anchor
**Dependencies**: 13.1-13.5 (all pipeline sub-epics), EPIC 3 (Front system), EPIC 5 (Echoes, optional), EPIC 14 (Boss: Grandmother Null)

---

## Overview

The Necrospire is District 1 — a towering data necropolis where corrupted uploaded consciousnesses wander holographic shrines and grief-mad pilgrims worship dead code. This is the vertical slice anchor district: the first complete implementation of the 13.1-13.5 pipeline. It demonstrates zone graph topology, 4 factions with distinct AI behaviors, a multi-phase Front (Corruption Bloom), 8 side goals with boss insurance and Front counterplay, a main quest chain, and the Grandmother Null boss fight. The Echo theme is "rotting memories" — identity drift debuffs and pristine intel rewards. Reanimation manifests as Recursive Specters wearing the player's face.

---

## Component Definitions

### NecrospireDistrictConfig

```csharp
// File: Assets/Scripts/District/Necrospire/NecrospireConfig.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.District.Necrospire
{
    /// <summary>
    /// District-specific runtime config for the Necrospire.
    /// Stored as a singleton on a district config entity.
    /// </summary>
    public struct NecrospireConfig : IComponentData
    {
        /// <summary>Corruption Bloom spread rate (zones per minute at phase 1).</summary>
        public float BaseCorruptionSpreadRate;

        /// <summary>Hologram occlusion opacity (0=transparent, 1=fully obscuring).</summary>
        public float HologramOcclusionAlpha;

        /// <summary>Biometric lock hack time in seconds.</summary>
        public float BiometricLockDuration;

        /// <summary>Phase vent damage per second.</summary>
        public float PhaseVentDPS;
    }
}
```

### CorruptionBloomState

```csharp
// File: Assets/Scripts/District/Necrospire/CorruptionBloomComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.District.Necrospire
{
    /// <summary>
    /// Per-zone corruption state. Buffer on the DistrictState entity.
    /// Tracks how far the Corruption Bloom Front has spread into each zone.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ZoneCorruptionEntry : IBufferElementData
    {
        public int ZoneIndex;

        /// <summary>0.0 = clean, 1.0 = fully corrupted.</summary>
        public float CorruptionLevel;

        /// <summary>True if this zone has active data conduits spreading corruption.</summary>
        public bool HasActiveConduits;

        /// <summary>True if player has purged this zone via side goal.</summary>
        public bool IsPurged;
    }

    /// <summary>
    /// Corruption Bloom Front phases for the Necrospire.
    /// Maps to the 4 universal Front phases with district-specific effects.
    /// </summary>
    public enum CorruptionPhase : byte
    {
        /// <summary>Phase 1: Flicker onset — hologram interference, minor enemy buffs.</summary>
        FlickerOnset = 0,

        /// <summary>Phase 2: Lockdown protocols — biometric doors lock, Warden patrols increase.</summary>
        LockdownProtocols = 1,

        /// <summary>Phase 3: Specter multiplication — Recursive Specters clone, double spawns.</summary>
        SpecterMultiplication = 2,

        /// <summary>Phase 4: Full purge — catastrophic data wipe, all zones hostile.</summary>
        FullPurge = 3
    }
}
```

---

## District Topology

**10 zones** arranged as stacked concentric rings (3 rings of 3 + 1 boss core):

| Zone | Name | Type | Primary Faction | Connections |
|------|------|------|-----------------|-------------|
| 0 | Outer Ring — Pilgrim's Gate | Combat | Mourning Collective | 1, 3 |
| 1 | Outer Ring — Relay Corridor | Combat | Archive Wardens | 0, 2, 4 |
| 2 | Outer Ring — Clone Market | Shop | The Inheritors | 1, 5 |
| 3 | Middle Ring — Shrine Hall | Elite | Mourning Collective | 0, 4, 6 |
| 4 | Middle Ring — Data Nexus | Combat | Recursive Specters | 1, 3, 5, 7 |
| 5 | Middle Ring — Inheritance Ward | Combat | The Inheritors | 2, 4, 8 |
| 6 | Inner Ring — Warden Bastion | Elite | Archive Wardens | 3, 7 |
| 7 | Inner Ring — Echo Chamber | Event | Recursive Specters | 4, 6, 8 |
| 8 | Inner Ring — Upload Sanctum | Rest | Mourning Collective | 5, 7, 9 |
| 9 | Core — Corruption Nexus | Boss | All factions | 8 |

**Topology Variants** (3):
- **Variant A (Clockwise)**: Entry at zone 0, zones spiral clockwise inward
- **Variant B (Split)**: Entry at zones 0 and 2, middle ring has a collapsed bridge (zone 4 unreachable until zone 7 cleared)
- **Variant C (Inverted)**: Entry at zone 6, player starts mid-ring and must push both outward and inward

---

## Factions (Detailed)

### Faction 1: Mourning Collective (FactionId = 10)

```
Aggression: Defensive
Patrol: Stationary (prayer circles)
Alarm Radius: 20m (synchronized grief-howl)

Enemies:
  - Grief Pilgrim (Common, Cost 1): Slow melee, attacks in synchronized groups.
    When one is hit, adjacent pilgrims enter rage state.
  - Mourning Herald (Common, Cost 2): Ranged, throws grief-totems that create
    slow fields. Retreats when health < 50%.
  - Synchronized Widow (Elite, Cost 4): Links to 2 nearby pilgrims. Damage
    shared across linked group. Must kill all 3 simultaneously or they resurrect.
  - Grief Engine (Special, Cost 6): Stationary turret, channels grief-beam
    that stacks identity-drift debuff. Vulnerable during reload.
```

### Faction 2: Recursive Specters (FactionId = 11)

```
Aggression: Aggressive
Patrol: Roaming (phasing through walls)
Alarm Radius: 30m (data-echo propagation)

Enemies:
  - Echo Fragment (Common, Cost 1): Fast melee, phases through obstacles on
    approach. 50% miss chance on first attack (afterimage).
  - Data Ghost (Common, Cost 2): Ranged, fires corrupted data packets.
    On death: splits into 2 Echo Fragments (below 50% of original stats).
  - Recursive Clone (Elite, Cost 5): Creates a copy of itself every 15s.
    Copies have 30% stats. Must kill original (marked with subtle visual tell).
  - Memory Leech (Special, Cost 4): Latches onto player, drains identity
    (stacking debuff). Must be removed via interaction or damage threshold.
```

### Faction 3: Archive Wardens (FactionId = 12)

```
Aggression: Passive (until security breach)
Patrol: Waypoint (fixed patrol routes)
Alarm Radius: 40m (network alert system)

Enemies:
  - Patrol Drone (Common, Cost 1): Flying, detection scanner. Low damage but
    triggers faction-wide alert on contact. Destroying silently = no alert.
  - Warden Sentinel (Common, Cost 3): Heavy melee, riot shield. Blocks
    frontal damage, vulnerable from behind. Slow but devastating.
  - Nerve-Gas Dispenser (Elite, Cost 4): Deploys gas clouds (DOT + vision
    impairment). Stationary, must be flanked and destroyed.
  - Lockdown Coordinator (Special, Cost 6): Non-combat. Activates biometric
    locks and security barriers in the zone. Kill to unlock doors.
```

### Faction 4: The Inheritors (FactionId = 13)

```
Aggression: Aggressive (territorial)
Patrol: Roaming (scavenging routes)
Alarm Radius: 15m (tight-knit crews)

Enemies:
  - Clone Blank (Common, Cost 1): Fast, fragile. Attacks in packs of 3-5.
    Drops genetic material for chassis repair.
  - Gene Thief (Common, Cost 2): Stealth approach, backstab damage bonus.
    Visible shimmer when moving. Rippable limbs (arm variants).
  - Body Broker (Elite, Cost 5): Carries limb stock. On death, drops 1-2
    random limbs. Heavily armored, uses clone blanks as shields.
  - Splice Surgeon (Special, Cost 4): Healer. Repairs nearby Inheritor
    allies. If not killed first, faction fights become wars of attrition.
```

---

## Front: Corruption Bloom

### Phase Definitions

```csharp
// Authored in FrontDefinitionSO for Necrospire
// Phase 1 — Flicker Onset (0:00 - 3:00)
//   - Hologram interference: 20% opacity on hologram occlusion surfaces
//   - Enemy buff: Recursive Specters +10% speed
//   - Corruption spreads from zone 9 outward along data conduits
//   - Pulse: Purge Countdown (warning klaxon every 60s)

// Phase 2 — Lockdown Protocols (3:00 - 6:00)
//   - Biometric doors in Warden zones lock (require hack minigame)
//   - Archive Wardens double patrol frequency
//   - Corruption reaches middle ring
//   - Pulse: Warden Lockdown Wave (all doors in 1 zone lock simultaneously)

// Phase 3 — Specter Multiplication (6:00 - 9:00)
//   - Recursive Specters spawn at 2x rate in corrupted zones
//   - Data Ghost split now creates 3 fragments instead of 2
//   - Corruption reaches outer ring
//   - Pulse: Screaming Broadcast (district-wide, stacks identity-drift on all players)

// Phase 4 — Full Purge (9:00+)
//   - All zones fully corrupted
//   - All factions aggression = Berserker
//   - Phase vent damage active in all zones (DPS from NecrospireConfig)
//   - Boss zone (9) unlocks regardless of main chain progress
//   - Pulse: Continuous Purge (escalating DPS, must reach boss or die)
```

---

## Side Goals (8)

| # | Name | GoalType | Objectives | Boss Insurance | Front Counterplay |
|---|------|----------|------------|----------------|-------------------|
| 1 | Sever the Grief-Link | Destroy | Kill 3 Grief Engines in zones 0,3,8 | DisableAbility: Grief Resonance attack | SlowSpread (30s) |
| 2 | Recover the Intact Upload | Collect | Find 1 Intact Upload in zone 7 (Echo Chamber) | RevealWeakpoint: Core memory address | None |
| 3 | Data Vampire Cache | Collect | Loot 3 Data Caches from Inheritor zones (2,5) | RemoveAdd: Clone Blank wave in boss fight | None |
| 4 | Silence the Screaming Server | Destroy | Interact with server terminal in zone 4 | DisableAbility: Screaming Broadcast pulse | DelayPhase (60s) |
| 5 | The Living Will | Rescue | Escort Upload NPC from zone 7 to zone 2 | ReduceHealth: Boss -15% max HP | None |
| 6 | Debug the Widow | Puzzle | 4-step interaction sequence in zone 3 | DisablePhase: Synchronized Widow summon in phase 2 | PurgeZone (zone 3) |
| 7 | Black Mass Disruption | Assassinate | Kill Grief Matriarch (miniboss) in zone 0 | RemoveAdd: Mourning Collective reinforcements | RedirectFront (zone 0) |
| 8 | Mercy Protocol | Stealth | Interact with 4 terminals in zones 1,6 without alerting Wardens | DisableAbility: Nerve-Gas phase in boss fight | SlowSpread (45s) |

---

## Main Chain: Purge the Core Corruption

| Step | Objective | Zone |
|------|-----------|------|
| 0 | Reach the Data Nexus (zone 4) — discover the corruption source | 4 |
| 1 | Retrieve the Purge Key from the Warden Bastion (kill Lockdown Coordinator) | 6 |
| 2 | Activate the Upload Sanctum override terminal | 8 |
| 3 | Enter the Corruption Nexus and confront Grandmother Null | 9 |

---

## Boss: GRANDMOTHER NULL

Defined fully in EPIC 14. Summary:
- Multi-phase fight in the Corruption Nexus (zone 9)
- Phase 1: Data tendrils + clone summons
- Phase 2: Grief resonance waves (disabled by goal 1)
- Phase 3: Full corruption — arena transforms, purge DPS
- Insurance effects from side goals directly remove mechanics
- Defeating Grandmother Null completes the district

---

## Echo Theme: Rotting Memories

- **Debuff echoes**: Identity Drift — stacking debuff that scrambles UI elements (minimap jitter, inventory icon shuffle, NPC name corruption)
- **Reward echoes**: Pristine Intel — finding uncorrupted data fragments grants bonus XP and lore
- **Skipped goal echoes**: Each skipped side goal manifests as a Recursive Specter in subsequent runs that speaks dialogue from the quest NPC you failed to save

---

## Reanimation

When the player dies in the Necrospire, their dying moments are "uploaded" into a Recursive Specter. On subsequent runs, this Specter:
- Wears the player's chassis configuration at time of death
- Uses abilities the player had equipped
- Appears in the zone where the player died
- Drops the player's lost limbs on defeat

---

## POIs

| Landmark | Zone | Interaction | Description |
|----------|------|-------------|-------------|
| Hologram Shrine Plaza | 0 | LoreTerminal | Central gathering of holographic memorials. Lore about the uploaded dead |
| Relay Node Chapel | 3 | HealStation | Mourning Collective prayer site. Restores HP but costs identity (small debuff) |
| Credential Forge | 5 | BodyShop | Inheritor black market. Buy/sell limbs and genetic material |
| Purge Corridor | 7 | None | Environmental set piece — flickering holograms, data waterfalls, ambient storytelling |
| Upload Vault | 8 | Vendor | Secure archive terminal. Purchase data-themed weapons and augments |

---

## Setup Guide

1. **Create `Assets/Data/Districts/Necrospire/` folder** with subfolders: Factions/, Goals/, POIs/, Encounters/, Rooms/
2. **Author DistrictDefinitionSO**: `Necrospire_District.asset` with all fields populated
3. **Author 4 FactionDefinitionSOs** per faction detail above
4. **Create 12+ enemy prefabs** (3 per faction minimum):
   - Each prefab: model, AnimatorController, AIBrainAuthoring, DamageableAuthoring, PhysicsShapeAuthoring (BelongsTo=Creature), LootTableAuthoring
   - Special AI behaviors: Synchronized Widow link system, Recursive Clone split system, Patrol Drone alert system
5. **Author 8 QuestDefinitionSOs** for side goals + 1 main chain (4 steps)
6. **Create DistrictGoalExtensionSOs** for each quest with insurance and counterplay data
7. **Author FrontDefinitionSO**: Corruption Bloom with 4 phases and 3 pulse types
8. **Create 5 landmark composition prefabs** and wire into DistrictDefinitionSO.LandmarkPOIs
9. **Create MicroPOIPoolSO** for Necrospire: broken terminals, grief totems, biometric scanners, drone nests
10. **Author 3 TopologyVariants** (Clockwise, Split, Inverted) with zone graph overrides
11. **Create DistrictGeneratorConfigSO**: Mode = HandCraftedProcedural, wire scene paths for 10 zones

---

## Verification

- [ ] DistrictDefinitionSO has all 10 zones in zone graph with correct connections
- [ ] 3 topology variants produce visibly different layouts
- [ ] 4 factions spawn correctly in their assigned zones
- [ ] Mourning Collective synchronized grief mechanic works (linked pilgrims)
- [ ] Recursive Specters phase through obstacles and split on death
- [ ] Archive Wardens trigger faction-wide alert on detection
- [ ] Inheritors drop genetic material and rippable limbs
- [ ] Corruption Bloom spreads from zone 9 outward along conduits
- [ ] Phase transitions occur at correct timing (3:00, 6:00, 9:00)
- [ ] All 8 side goals completable and tracked in DistrictGoalEntry buffer
- [ ] Boss insurance effects recorded on goal completion
- [ ] Front counterplay effects modify corruption spread
- [ ] Main chain sequential completion unlocks boss zone
- [ ] Grandmother Null fight respects insurance (disabled abilities/phases)
- [ ] Echo theme: identity drift debuff stacks correctly
- [ ] Reanimation: player death creates Recursive Specter with player's chassis
- [ ] All 5 landmark POIs instantiated with correct interaction types
- [ ] Micro-POIs placed at appropriate density per zone

---

## Live Tuning

| Parameter | Singleton | Range | Default |
|-----------|-----------|-------|---------|
| BaseCorruptionSpreadRate | NecrospireConfig | 0.1–2.0 zones/min | 0.5 |
| HologramOcclusionAlpha | NecrospireConfig | 0.0–1.0 | 0.2 (Phase 1) |
| BiometricLockDuration | NecrospireConfig | 1.0–10.0s | 4.0 |
| PhaseVentDPS | NecrospireConfig | 1.0–50.0 | 10.0 |
| Phase timing (1→2→3→4) | FrontDefinitionSO | 60–600s per phase | 180s each |
| Specter clone rate (Phase 3) | FactionDefinitionSO FrontPhaseOverrides | 1x–4x | 2x |

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Necrospire/Debug/NecrospireDebugOverlay.cs
// Development builds:
//   - Corruption level heat map: per-zone overlay (green=0, red=1.0)
//   - Active data conduits: glowing lines between zones with HasActiveConduits
//   - Purged zones: blue overlay
//   - Phase indicator: large text "PHASE 2: LOCKDOWN PROTOCOLS" in HUD
//   - Biometric lock status: icons on locked doors (green=open, red=locked)
//   - Synchronized Widow link lines: yellow lines between linked pilgrims
//   - Recursive Specter clone count: number above each Specter group
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/District/NecrospireTest.cs
// [Test] CorruptionBloom_PhaseTimingAccuracy
//   Start Necrospire, verify CorruptionPhase transitions at 180s, 360s, 540s
//   (within 1s tolerance).
//
// [Test] CorruptionBloom_SpreadFromCore
//   Verify corruption spreads from zone 9 outward:
//   at Phase 1 end, zone 9 corruption > 0.8, zone 0 corruption < 0.3.
//
// [Test] FactionZoneAssignment_Correct
//   Load Necrospire, verify zone 0 spawns Mourning Collective,
//   zone 1 spawns Archive Wardens, etc. per topology table.
//
// [Test] SideGoal_BossInsurance_Integration
//   Complete "Sever the Grief-Link", verify BossInsuranceState contains
//   { DisableAbility, BossTargetId=GriefResonance }.
//
// [Test] TopologyVariants_3Distinct
//   Generate Necrospire with seeds that select variants A, B, C,
//   verify zone connectivity differs (Variant B: zone 4 unreachable initially).
```
