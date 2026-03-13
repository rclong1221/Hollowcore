# EPIC 13.8: Vertical Slice — THE LATTICE

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: Critical — Third vertical slice district, showcases verticality
**Dependencies**: 13.1-13.5 (all pipeline sub-epics), EPIC 3 (Front system), EPIC 5 (Echoes, optional), EPIC 14 (Boss: King of Heights)

---

## Overview

The Lattice is District 8 — a vertical slum of abandoned construction where height equals both danger and value. The defining mechanic is **verticality**: the district is organized along a vertical axis (base shadow, mid scaffolds, apex bunkers) with ziplines, gliders, and fall punishment as traversal mechanics. The Front type is Structural Failure Spiral — demolition charges trigger cascading collapses that reshape the playable space. Four factions occupy different vertical strata. 5 side goals centered on structural stability and vertical mastery.

---

## Component Definitions

### VerticalState (IComponentData)

```csharp
// File: Assets/Scripts/District/Lattice/VerticalComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.District.Lattice
{
    /// <summary>
    /// Tracks the player's vertical stratum in the Lattice.
    /// Determines ambient danger level, loot quality, and faction encounters.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct VerticalState : IComponentData
    {
        /// <summary>Current height tier: 0=Base, 1=Mid, 2=Apex.</summary>
        [GhostField] public byte HeightTier;

        /// <summary>Absolute Y position used for fall damage calculation.</summary>
        [GhostField(Quantization = 100)] public float CurrentHeight;

        /// <summary>Highest Y reached this run (for "height record" tracking).</summary>
        [GhostField(Quantization = 100)] public float MaxHeightReached;

        /// <summary>Accumulated fall damage modifier (stacks from echo curses).</summary>
        [GhostField(Quantization = 100)] public float FallDamageMultiplier;
    }

    /// <summary>
    /// Lattice district singleton config.
    /// </summary>
    public struct LatticeConfig : IComponentData
    {
        public float BaseTierCeiling;       // Y threshold for Base → Mid
        public float MidTierCeiling;        // Y threshold for Mid → Apex
        public float FallDamagePerMeter;    // Damage per meter fallen beyond safe threshold
        public float SafeFallDistance;      // Meters of fall before damage starts
        public float ZiplineSpeed;          // Movement speed on ziplines
        public float GliderDescentRate;     // Vertical descent rate while gliding
        public float GliderHorizontalSpeed; // Horizontal speed while gliding
        public float WindForce;             // Lateral wind push at Apex tier
    }

    /// <summary>
    /// Per-zone structural integrity. Buffer on DistrictState entity.
    /// When integrity reaches 0, the zone collapses and becomes inaccessible.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ZoneIntegrityEntry : IBufferElementData
    {
        public int ZoneIndex;
        public float Integrity;        // 1.0 = stable, 0.0 = collapsed
        public bool HasActiveCharges;  // Collapse Engineers have placed charges
        public bool IsCollapsed;       // Zone is destroyed and inaccessible
    }
}
```

### Traversal Components

```csharp
// File: Assets/Scripts/District/Lattice/TraversalComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.District.Lattice
{
    /// <summary>
    /// Zipline endpoint entity. Two endpoints define a zipline route.
    /// Player attaches at one end and travels to the other.
    /// </summary>
    public struct ZiplineEndpoint : IComponentData
    {
        public Entity OtherEnd;
        public float3 Position;
        public bool IsStartPoint;
    }

    /// <summary>
    /// Glider launch point. Player can deploy glider from these positions.
    /// Glider allows controlled horizontal movement with slow descent.
    /// </summary>
    public struct GliderLaunchPoint : IComponentData
    {
        public float3 Position;
        public float3 LaunchDirection;
        public float MinHeightForLaunch;  // Must be at least this high to glide
    }

    /// <summary>
    /// Wind zone. Applies lateral force to players and gliders.
    /// Present at Apex tier, intensity varies.
    /// </summary>
    public struct WindZone : IComponentData
    {
        public float3 Direction;
        public float Force;
        public float3 BoundsMin;
        public float3 BoundsMax;
    }
}
```

---

## District Topology

**9 zones** arranged vertically (3 tiers of 3):

| Zone | Name | Type | Tier | Primary Faction | Connections |
|------|------|------|------|-----------------|-------------|
| 0 | Foundation Depths | Combat | Base | The Foundation | 1, 3 |
| 1 | Shadow Market | Shop | Base | The Foundation | 0, 2, 4 |
| 2 | Rubble Warren | Combat | Base | The Foundation | 1, 5 |
| 3 | Scaffold Junction | Combat | Mid | The Climbers | 0, 4, 6 |
| 4 | Charge Control | Elite | Mid | Collapse Engineers | 1, 3, 5, 7 |
| 5 | Wind Bridge | Event | Mid | The Climbers | 2, 4, 8 |
| 6 | Apex Approach | Combat | Apex | Apex Dwellers | 3, 7 |
| 7 | Crown Platform | Elite | Apex | Apex Dwellers | 4, 6, 8 |
| 8 | King's Spire | Boss | Apex | All factions | 5, 7 |

**Topology Variants** (3):
- **Variant A (Standard Climb)**: Entry at zone 0, straightforward ascent with branching paths
- **Variant B (Broken Elevator)**: Entry at zone 1, central elevator shaft (zones 1-4-7) is broken — must use scaffolds and ziplines for lateral access
- **Variant C (Apex Drop)**: Entry at zone 6, player starts at Apex and must descend to Foundation then climb back up. Fall punishment is the early-game pressure

**Traversal Mechanics**:
- **Ziplines**: One-way travel between vertically separated zones. 8 ziplines across the district connecting tiers
- **Glider**: Deployable from Apex-tier launch points. Allows controlled descent with horizontal movement. Wind zones push gliders laterally
- **Fall punishment**: Falling more than SafeFallDistance meters deals escalating damage. Lattice-affinity limbs reduce fall damage
- **Wind mapping**: Apex tier has persistent wind that affects projectiles, gliders, and player movement

---

## Factions (Detailed)

### Faction 1: The Climbers (FactionId = 80)

```
Aggression: Aggressive
Patrol: Roaming (vertical traversal, grapple routes)
Alarm Radius: 15m (whistle signals, vertical propagation)

Enemies:
  - Grapple Runner (Common, Cost 1): Fast, uses grapple hooks to attack
    from unexpected angles (above, below, flanking). Drops grapple-limb
    salvage on death.
  - Cliff Spotter (Common, Cost 2): Ranged, fires from elevated positions.
    Has vertical advantage bonus (+20% damage when above target).
    Repositions via zipline if flanked.
  - Swing Raider (Elite, Cost 4): Grapple-based melee. Swings on cables for
    momentum attacks. Impact knocks player backward (fall risk).
    Chain 3 swings for devastating combo.
  - Ascension Captain (Special, Cost 5): Commander. Buffs Climber speed +30%.
    Can cut ziplines (disables traversal route until repaired).
    Calls reinforcements from adjacent vertical zones.
```

### Faction 2: Collapse Engineers (FactionId = 81)

```
Aggression: Defensive (territorial, trap-focused)
Patrol: Stationary (guarding charges)
Alarm Radius: 20m (detonator signals)

Enemies:
  - Demo Rigger (Common, Cost 2): Places proximity mines and tripwires.
    Low direct combat ability. Mines deal high damage + knockback (fall risk).
  - Blast Specialist (Common, Cost 2): Ranged, explosive launcher. Shots
    damage structural integrity of platforms (can create holes to fall through).
  - Sapper (Elite, Cost 5): Plants structural charges on zone supports.
    If not disarmed within 30s, zone integrity drops by 25%. Always carries
    a detonator — killing them mid-detonation triggers partial collapse.
  - Demolition Master (Special, Cost 6): Miniboss-tier. Can trigger zone-wide
    collapse event (15s warning). Must be killed or detonator destroyed.
    Wears blast-proof armor (explosion immunity).
```

### Faction 3: Apex Dwellers (FactionId = 82)

```
Aggression: Passive (until territory breached)
Patrol: Waypoint (drone perimeters)
Alarm Radius: 50m (surveillance network)

Enemies:
  - Sentinel Drone (Common, Cost 1): Flying, long detection range. Marks
    intruders for targeting by other Apex units. Fragile.
  - Apex Guard (Common, Cost 3): Heavy armor, precision rifle. Fires from
    extreme range with wind compensation. Retreats inward when pushed.
  - Wind Lancer (Elite, Cost 5): Uses wind manipulation tech. Can push
    players off platforms (instant fall damage scenario). Creates wind
    barriers that block projectiles.
  - Apex Overseer (Special, Cost 6): Non-combat controller. Activates
    defense turrets and barrier fields. Turrets track targets automatically.
    Destroying Overseer disables all turrets in zone.
```

### Faction 4: The Foundation (FactionId = 83)

```
Aggression: Berserker
Patrol: Swarm (shadow ambush)
Alarm Radius: 8m (close-quarters only)

Enemies:
  - Shadow Crawler (Common, Cost 1): Melee, low-visibility in dark base
    zones. Ambush attack from darkness deals 2x damage. Weak in lit areas.
  - Pit Dweller (Common, Cost 2): Ranged, throws debris. Can grab players
    and drag them downward (fall damage). Breakable grab (damage threshold).
  - Foundation Brute (Elite, Cost 4): Massive, slow. Ground-pound attack
    damages platform integrity. Can collapse small platforms under players.
    Immune to fall damage (adapted to falls).
  - Depth Warden (Special, Cost 5): Controls base-tier darkness. Extinguishes
    light sources in radius. All Foundation enemies in darkness gain +50%
    damage. Killing restores lights for 60s.
```

---

## Front: Structural Failure Spiral

### Phase Definitions

```
Phase 1 — Tense (0:00 - 3:00)
  - Collapse Engineers become active (previously dormant/patrol only)
  - Structural integrity visible on UI for all zones
  - Minor tremors: screen shake, loose debris falls
  - Pulse: Collapse Event (one platform section in random zone destroyed)

Phase 2 — Unstable (3:00 - 6:00)
  - All mid-tier zones lose 20% structural integrity
  - Ziplines have 10% failure chance (cable snap, player falls)
  - Collapse Engineers place charges in 2 additional zones
  - Pulse: Apex Drone Sweep (Apex Dwellers scan all zones, mark all players)

Phase 3 — Collapse (6:00 - 9:00)
  - One mid-tier zone collapses entirely (becomes inaccessible)
  - Remaining zones: 50% integrity, visible cracks, falling debris hazard
  - Glider launch points at collapsed zones destroyed
  - Pulse: Foundation Surge (Foundation enemies flood upward from base)

Phase 4 — Freefall Zones (9:00+)
  - Two additional zones collapse
  - Remaining zones at 25% integrity, constant debris damage
  - All factions: Berserker aggression
  - Wind at all tiers (not just Apex)
  - Boss zone (8) unlocks regardless of main chain
  - Pulse: Cascading Collapse (continuous, one platform per 30s)
```

---

## Side Goals (5)

| # | Name | GoalType | Objectives | Boss Insurance | Front Counterplay |
|---|------|----------|------------|----------------|-------------------|
| 1 | Disable the Charges | Destroy | Disarm 5 structural charges in zones 3,4,5 | DisableAbility: Arena collapse phase in boss fight | DelayPhase (60s) |
| 2 | Grapple-Limb Salvage | Collect | Collect 2 grapple-limbs from Climber elites | RevealWeakpoint: King's cable tether | None |
| 3 | The Long Fall | Survive | Survive 60s in Foundation Depths (zone 0) during tremor event | ReduceHealth: Boss -15% max HP | None |
| 4 | Bridge Builder | Puzzle | Repair 3 broken zipline connections in zones 1,3,5 | RemoveAdd: Removes Climber reinforcement wave in boss fight | PurgeZone (zone 4) |
| 5 | Clip the Wings | Assassinate | Destroy the Apex Overseer's control node in zone 7 | DisablePhase: Drone bombardment phase in boss fight | SlowSpread (45s) |

---

## Main Chain: Anchor the Heart

| Step | Objective | Zone |
|------|-----------|------|
| 0 | Reach the Scaffold Junction (zone 3) — assess structural damage | 3 |
| 1 | Retrieve the Stabilizer Core from Charge Control (zone 4) | 4 |
| 2 | Install stabilizers at Wind Bridge (zone 5) to secure the ascent | 5 |
| 3 | Climb to King's Spire and confront the King of Heights | 8 |

---

## Boss: THE KING OF HEIGHTS

Defined fully in EPIC 14. Summary:
- Arena: King's Spire (zone 8) — multi-level tower platform, open sky, extreme wind
- Phase 1: Ranged combat across platforms, wind manipulation, drone support
- Phase 2: Close quarters on shrinking platforms (arena collapse, disabled by goal 1)
- Phase 3: Cable tether combat — King swings between support cables (tether revealed by goal 2)
- The arena is at maximum height — all knockback attacks risk lethal falls
- Insurance effects simplify the fight by removing environmental hazards

---

## Echo Theme: The Fall Remembers

- **Debuff echoes**: Fall Curse — increased fall damage multiplier (+50%) on subsequent runs in any district with vertical elements
- **Reward echoes**: Height Sense — shortcut paths between vertical zones revealed on minimap in future runs, zipline speed +20%
- **Skipped goal echoes**: Unrepaired bridges collapse in future runs; disabled charges re-arm and detonate unexpectedly

---

## Reanimation

When the player dies in The Lattice, the Climbers claim the body:
- Body is rigged as a vertical ambush trap — hanging from cables at key traversal points
- On approach, the trap activates: drops the body to create a knockback explosion (fall risk)
- Defeating the trap drops the player's lost equipment
- Location: always at a zipline endpoint or glider launch point in the zone of death

---

## POIs

| Landmark | Zone | Interaction | Description |
|----------|------|-------------|-------------|
| Charge Control | 4 | QuestGiver | Collapse Engineer defector NPC. Offers intel on charge locations |
| Wind Beacon | 5 | None | Atmospheric set piece — massive wind generator, visible from all tiers |
| Bridge Station | 1 | Workbench | Zipline repair workshop. Craft traversal equipment |
| Apex Node | 7 | Vendor | Apex Dweller trading post. Premium gear at premium prices |
| Foundation Pit | 0 | LoreTerminal | Ancient construction records. Lore about why the Lattice was abandoned |

---

## Setup Guide

1. **Create `Assets/Data/Districts/Lattice/` folder** with subfolders: Factions/, Goals/, POIs/, Encounters/, Rooms/
2. **Implement VerticalState system**: `VerticalTierSystem` (tracks height tier), `FallDamageSystem` (calculates and applies fall damage), `WindForceSystem` (applies wind to player and projectiles)
3. **Implement traversal systems**: `ZiplineSystem` (player attachment, travel, cable snap), `GliderSystem` (deployment, descent, wind interaction), `PlatformIntegritySystem` (tracks zone structural health, collapse events)
4. **Author DistrictDefinitionSO**: `Lattice_District.asset`
5. **Author 4 FactionDefinitionSOs** per faction detail above
6. **Create 12+ enemy prefabs** (3 per faction):
   - Climbers: grapple attack AI, vertical repositioning
   - Collapse Engineers: mine/charge placement AI
   - Apex Dwellers: long-range precision AI, drone control
   - Foundation: ambush AI, darkness dependence
7. **Author 5 QuestDefinitionSOs** for side goals + 1 main chain (4 steps)
8. **Author FrontDefinitionSO**: Structural Failure Spiral with 4 phases
9. **Create zipline and glider prefabs** with traversal authoring components
10. **Create 3 TopologyVariants** with zone graph overrides

---

## Verification

- [ ] VerticalState correctly tracks player height tier (Base/Mid/Apex)
- [ ] Fall damage applies above SafeFallDistance threshold
- [ ] Fall damage respects FallDamageMultiplier from limbs and echo curses
- [ ] Ziplines transport player between endpoints at configured speed
- [ ] Zipline cable snap during Phase 2+ drops player (fall damage)
- [ ] Glider deploys from launch points, wind affects trajectory
- [ ] Wind zones push player and projectiles at Apex tier
- [ ] 4 factions spawn at correct vertical tiers
- [ ] Climbers attack from unexpected vertical angles
- [ ] Collapse Engineer charges reduce zone integrity on detonation
- [ ] Zone collapse makes zone inaccessible (gates blocked)
- [ ] Foundation Brute ground-pound damages platform integrity
- [ ] Structural Failure Spiral phases collapse zones on schedule
- [ ] All 5 side goals completable
- [ ] Boss insurance effects recorded correctly
- [ ] Main chain unlocks King's Spire boss zone
- [ ] Reanimation produces cable-trap at traversal points
- [ ] Height-tier loot quality scaling: Apex drops better than Base

---

## Live Tuning

| Parameter | Singleton | Range | Default |
|-----------|-----------|-------|---------|
| BaseTierCeiling | LatticeConfig | 10–50m | 20.0 |
| MidTierCeiling | LatticeConfig | 30–100m | 50.0 |
| FallDamagePerMeter | LatticeConfig | 1.0–20.0 | 5.0 |
| SafeFallDistance | LatticeConfig | 2.0–10.0m | 5.0 |
| ZiplineSpeed | LatticeConfig | 5.0–30.0 | 15.0 |
| GliderDescentRate | LatticeConfig | 0.5–5.0 | 2.0 |
| GliderHorizontalSpeed | LatticeConfig | 5.0–20.0 | 10.0 |
| WindForce | LatticeConfig | 0.0–20.0 | 5.0 |
| ZiplineFailureChance (Phase 2+) | FrontDefinitionSO | 0.0–0.3 | 0.1 |

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Lattice/Debug/LatticeDebugOverlay.cs
// Development builds:
//   - Structural integrity per zone: bar + percentage floating at zone centroid
//   - Tier boundaries: horizontal plane at BaseTierCeiling and MidTierCeiling (cyan wireframe)
//   - Player height tier: large "BASE / MID / APEX" label in HUD
//   - Wind vectors: animated arrows at Apex tier showing direction and force
//   - Zipline routes: green lines between endpoints (red = failed/snapped)
//   - Glider launch points: blue cones showing LaunchDirection
//   - Active charges: red pulsing spheres at Collapse Engineer charge positions
//   - Collapsed zones: red X overlay, all gates into zone shown as blocked
//   - Fall damage preview: dotted line from player to ground with damage estimate
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/District/LatticeTest.cs
// [Test] FallDamage_AboveSafeDistance
//   Drop player from 15m, SafeFallDistance=5m, FallDamagePerMeter=5.0,
//   verify damage = (15-5)*5 = 50.
//
// [Test] FallDamage_BelowSafeDistance_NoDamage
//   Drop player from 4m (below SafeFallDistance=5m), verify 0 damage.
//
// [Test] ZoneCollapse_MakesInaccessible
//   Set zone 4 integrity to 0, verify ZoneIntegrityEntry.IsCollapsed=true,
//   verify ZoneGate entries leading to zone 4 are locked.
//
// [Test] StructuralFailure_PhaseProgression
//   Verify Phase 2 reduces mid-tier integrity by 20%,
//   Phase 3 collapses one mid-tier zone, Phase 4 collapses two more.
//
// [Test] TopologyVariants_3Distinct
//   Generate Lattice with seeds for variants A/B/C, verify Variant C
//   has entry at zone 6 (Apex) instead of zone 0 (Base).
//
// [Test] ZiplineTravel_Speed
//   Attach player to zipline, verify arrival at other endpoint
//   within expected time (distance / ZiplineSpeed +/- 10%).
```
