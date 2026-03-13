# EPIC 13.8 Setup Guide: Lattice Vertical Traversal, Integrity System & Ziplines

**Status:** Planned
**Requires:** 13.1-13.5 (all pipeline sub-epics), EPIC 3 (Front system), EPIC 14 (Boss: King of Heights)

---

## Overview

Set up The Lattice -- District 8, the third vertical slice district. A vertical slum of abandoned construction where height equals danger and value. The defining mechanic is verticality: three height tiers (Base, Mid, Apex), ziplines, gliders, wind zones, and fall punishment. The Front type is Structural Failure Spiral -- demolition charges trigger cascading zone collapses. This guide covers the VerticalState component, LatticeConfig singleton, structural integrity system, zipline/glider prefabs, and wind zone setup.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| DistrictDefinitionSO for Lattice | EPIC 13.1 complete | Master definition |
| 4 FactionDefinitionSOs | EPIC 13.3 complete | The Climbers, Collapse Engineers, Apex Dwellers, The Foundation |
| FrontDefinitionSO | EPIC 3 | Structural Failure Spiral, 4 phases |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| VerticalComponents.cs | `Assets/Scripts/District/Lattice/` | C# (ECS) |
| TraversalComponents.cs | `Assets/Scripts/District/Lattice/` | C# (ECS) |
| VerticalTierSystem.cs | `Assets/Scripts/District/Lattice/Systems/` | C# (ISystem) |
| FallDamageSystem.cs | `Assets/Scripts/District/Lattice/Systems/` | C# (ISystem) |
| ZiplineSystem.cs | `Assets/Scripts/District/Lattice/Systems/` | C# (ISystem) |
| GliderSystem.cs | `Assets/Scripts/District/Lattice/Systems/` | C# (ISystem) |
| WindForceSystem.cs | `Assets/Scripts/District/Lattice/Systems/` | C# (ISystem) |
| PlatformIntegritySystem.cs | `Assets/Scripts/District/Lattice/Systems/` | C# (ISystem) |
| Lattice_District.asset | `Assets/Data/Districts/Lattice/` | ScriptableObject |
| Traversal prefabs (zipline, glider launch) | `Assets/Prefabs/Districts/Lattice/Traversal/` | Prefab |

---

## 1. Create LatticeConfig Singleton

### 1.1 LatticeConfig Fields
| Field | Description | Default | Range | Notes |
|-------|-------------|---------|-------|-------|
| BaseTierCeiling | Y threshold for Base -> Mid | 20.0 | 10-50m | World-space Y |
| MidTierCeiling | Y threshold for Mid -> Apex | 50.0 | 30-100m | World-space Y |
| FallDamagePerMeter | Damage per meter fallen beyond safe threshold | 5.0 | 1.0-20.0 | Linear scaling |
| SafeFallDistance | Meters of fall before damage starts | 5.0 | 2.0-10.0 | Below this = 0 damage |
| ZiplineSpeed | Movement speed on ziplines (m/s) | 15.0 | 5.0-30.0 | -- |
| GliderDescentRate | Vertical descent rate while gliding (m/s) | 2.0 | 0.5-5.0 | Slow = more range |
| GliderHorizontalSpeed | Horizontal speed while gliding (m/s) | 10.0 | 5.0-20.0 | -- |
| WindForce | Lateral wind push at Apex tier (N) | 5.0 | 0.0-20.0 | Affects player + projectiles |

**Tuning tip:** FallDamagePerMeter=5.0 with SafeFallDistance=5.0 means a 15m fall deals (15-5)*5 = 50 damage. A 25m fall (Base to Mid-tier) deals 100 damage. This makes falls dangerous but survivable with good HP. Lattice-affinity limbs should reduce FallDamagePerMeter.

---

## 2. Create VerticalState Component (Player)

Added to player entity when entering The Lattice; removed on exit.

### 2.1 VerticalState Fields
| Field | Description | Default | Ghost |
|-------|-------------|---------|-------|
| HeightTier | Current tier: 0=Base, 1=Mid, 2=Apex | 0 | Yes |
| CurrentHeight | Absolute Y position | 0.0 | Yes (Q=100) |
| MaxHeightReached | Highest Y this run (stat tracking) | 0.0 | Yes (Q=100) |
| FallDamageMultiplier | Stacks from echo curses (1.0 = normal) | 1.0 | Yes (Q=100) |

### 2.2 Tier Determination
```
HeightTier = CurrentHeight < BaseTierCeiling ? 0
           : CurrentHeight < MidTierCeiling  ? 1
           : 2
```

### 2.3 Tier Gameplay Effects
| Tier | Ambient Danger | Loot Quality | Factions |
|------|----------------|-------------|----------|
| Base (0) | Low (darkness, ambush) | Basic | The Foundation |
| Mid (1) | Medium (structural instability, charges) | Standard | Climbers, Collapse Engineers |
| Apex (2) | High (wind, drones, exposed) | Premium | Apex Dwellers |

---

## 3. Configure the 9-Zone Graph

### 3.1 Zone Layout Table
| Zone | Name | Type | Tier | Faction | Connections |
|------|------|------|------|---------|-------------|
| 0 | Foundation Depths | Combat | Base | TheFoundation(3) | 1, 3 |
| 1 | Shadow Market | Shop | Base | TheFoundation(3) | 0, 2, 4 |
| 2 | Rubble Warren | Combat | Base | TheFoundation(3) | 1, 5 |
| 3 | Scaffold Junction | Combat | Mid | TheClimbers(0) | 0, 4, 6 |
| 4 | Charge Control | Elite | Mid | CollapseEngineers(1) | 1, 3, 5, 7 |
| 5 | Wind Bridge | Event | Mid | TheClimbers(0) | 2, 4, 8 |
| 6 | Apex Approach | Combat | Apex | ApexDwellers(2) | 3, 7 |
| 7 | Crown Platform | Elite | Apex | ApexDwellers(2) | 4, 6, 8 |
| 8 | King's Spire | Boss | Apex | All | 5, 7 |

### 3.2 Topology Variants
| Variant | Name | Entry | Key Difference |
|---------|------|-------|----------------|
| A | Standard Climb | [0] | Straightforward ascent with branching paths |
| B | Broken Elevator | [1] | Central shaft (1-4-7) broken, must use scaffolds/ziplines |
| C | Apex Drop | [6] | Start at Apex, descend to Foundation, then climb back up |

---

## 4. Create Structural Integrity System

### 4.1 ZoneIntegrityEntry Buffer
Populated at district load. One entry per zone:

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Integrity | Structural health (1.0 = stable, 0.0 = collapsed) | 1.0 | 0.0-1.0 |
| HasActiveCharges | Collapse Engineer charges present | false | -- |
| IsCollapsed | Zone destroyed and inaccessible | false | -- |

### 4.2 Integrity Reduction Events
| Event | Integrity Loss | Source |
|-------|---------------|--------|
| Sapper charge detonation | -0.25 | Collapse Engineer enemy (30s disarm window) |
| Foundation Brute ground-pound | -0.05 | Foundation faction elite |
| Front Phase 2 | -0.20 to all mid-tier zones | Structural Failure Spiral |
| Front Phase 3 | One mid-tier zone -> 0.0 (collapse) | Random selection |
| Front Phase 4 | Two additional zones -> 0.0 | Cascading collapse |

### 4.3 Zone Collapse Effects
When `Integrity` reaches 0.0:
1. Set `IsCollapsed = true`
2. Lock all ZoneGate entries leading to this zone
3. Destroy glider launch points in this zone
4. Play collapse VFX/audio
5. Any entities in the zone take massive fall damage

---

## 5. Create Zipline Prefabs

### 5.1 Zipline Endpoint Prefab
```
Zipline_Endpoint (root)
  Visual/
    Pole (MeshRenderer, metal pole)
    Cable (LineRenderer, connects to OtherEnd at runtime)
    Mount_FX (ParticleSystem, plays when player attaches)
  Logic/
    ZiplineEndpointAuthoring
      OtherEnd: Entity (paired endpoint)
      IsStartPoint: bool
  Interaction/
    InteractableAuthoring (instant, press E to attach)
  Collider/
    ProximityTrigger (sphere, radius 2.0m)
```

### 5.2 Zipline Configuration
| Setting | Default | Range |
|---------|---------|-------|
| Speed | Uses LatticeConfig.ZiplineSpeed (15.0) | -- |
| Cable snap chance (Phase 2+) | 10% per use | 0.0-0.3 |
| Travel direction | One-way (start -> end, determined by height) | -- |
| Detach option | Press Jump to detach mid-travel | -- |

### 5.3 Zipline Placement Guidelines
- **8 ziplines across the district** connecting tiers
- Always travel from higher to lower endpoint (gravity assist)
- Place endpoints near zone edges for cross-zone travel
- During Phase 2+, 10% cable snap chance drops player (fall damage)

---

## 6. Create Glider Launch Point Prefabs

### 6.1 Glider Launch Prefab
```
GliderLaunch_Point (root)
  Visual/
    LaunchPad (MeshRenderer, platform with ramp)
    WindSock (animated mesh, shows wind direction)
    Beacon (Light, visible from distance)
  Logic/
    GliderLaunchAuthoring
      LaunchDirection: float3 (unit vector, forward from ramp)
      MinHeightForLaunch: 40.0m (must be at Apex tier)
  Interaction/
    InteractableAuthoring (instant, press E to deploy glider)
```

### 6.2 Glider Controls (While Airborne)
| Input | Action |
|-------|--------|
| W/S | Pitch (steeper descent = more speed, shallow = more range) |
| A/D | Bank left/right |
| Space | Detach glider (drop, take fall damage) |

### 6.3 Glider Physics
| Parameter | Value | Source |
|-----------|-------|--------|
| Descent rate | LatticeConfig.GliderDescentRate (2.0 m/s) | Singleton |
| Horizontal speed | LatticeConfig.GliderHorizontalSpeed (10.0 m/s) | Singleton |
| Wind effect | LatticeConfig.WindForce pushes laterally | WindZone entities |
| Maximum range | ~250m horizontal at Apex height | Calculated |

---

## 7. Create Wind Zone Entities

### 7.1 WindZone Component
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Direction | Wind direction (normalized float3) | (1,0,0) | Unit vector |
| Force | Push strength (N) | 5.0 | 0.0-20.0 |
| BoundsMin | AABB minimum corner | -- | World space |
| BoundsMax | AABB maximum corner | -- | World space |

### 7.2 Wind Zone Placement
- Place in all Apex-tier zones (6, 7, 8)
- At Phase 4, wind extends to all tiers
- Wind affects: player movement, glider trajectory, projectile paths
- Wind direction should be consistent per zone (prevailing wind)

**Tuning tip:** WindForce=5.0 pushes a player ~0.5m per second sideways. On narrow platforms, this creates fall risk. On gliders, it creates interesting steering challenges. For boss zone (8), use stronger wind (10.0) to increase difficulty.

---

## 8. Configure Fall Damage

### 8.1 Fall Damage Formula
```
fallDistance = startHeight - landHeight
if (fallDistance > SafeFallDistance):
    damage = (fallDistance - SafeFallDistance) * FallDamagePerMeter * FallDamageMultiplier
```

### 8.2 Example Damage Values
| Fall | Distance | Safe | Excess | Damage (default) |
|------|----------|------|--------|-------------------|
| Small ledge | 3m | 5m | 0m | 0 |
| Platform gap | 8m | 5m | 3m | 15 |
| Tier transition (Base->Mid) | 20m | 5m | 15m | 75 |
| Full height (Apex->Base) | 50m | 5m | 45m | 225 (likely fatal) |

---

## Scene & Subscene Checklist

- [ ] `Lattice_District.asset` created with 9 zones, 3 variants, 4 factions, Front, 5 goals, boss
- [ ] LatticeConfig singleton entity in subscene with default values
- [ ] ZoneIntegrityEntry buffer populated with per-zone integrity (all 1.0 at start)
- [ ] 8 zipline endpoint pairs placed connecting tiers
- [ ] Glider launch points placed at Apex-tier zones (6, 7)
- [ ] Wind zone entities placed at Apex-tier zones
- [ ] VerticalState added to player entity on district entry
- [ ] Fall damage system configured with SafeFallDistance and FallDamagePerMeter

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| BaseTierCeiling and MidTierCeiling too close | Tiny mid-tier zone, player teleports between tiers | Ensure 30m+ gap between tier ceilings |
| SafeFallDistance too low | Every jump takes damage, frustrating gameplay | Increase to 5.0m+ (normal jump height should be safe) |
| Zipline endpoints not paired | Player attaches but goes nowhere | Ensure every ZiplineEndpoint.OtherEnd references the paired endpoint entity |
| Glider MinHeightForLaunch too high | Player can never reach launch point | Set to MidTierCeiling (50m) minus some margin |
| Wind force applied inside buildings | Indoor Apex zones have wind | Use WindZone bounds to limit wind to outdoor/exposed areas |
| Zone collapse doesn't lock gates | Players enter collapsed zones and fall through void | PlatformIntegritySystem must set ZoneGate.IsLocked=true for all gates into collapsed zones |
| FallDamageMultiplier not read from echo curses | Lattice echo debuff ("Fall Curse") has no effect | FallDamageSystem must multiply by VerticalState.FallDamageMultiplier |
| Cable snap at Phase 2 kills player without warning | Feels unfair, no counterplay | Add visual cable fraying + audio creak warning 1s before snap |

---

## Verification

- [ ] VerticalState correctly tracks player height tier (Base/Mid/Apex)
- [ ] Fall damage applies above SafeFallDistance threshold (formula correct)
- [ ] Fall damage respects FallDamageMultiplier from limbs and echo curses
- [ ] Ziplines transport player between endpoints at configured speed
- [ ] Zipline cable snap at Phase 2+ drops player (with warning)
- [ ] Glider deploys from launch points, controlled descent with wind
- [ ] Wind zones push player and projectiles at Apex tier
- [ ] Zone collapse makes zone inaccessible (gates locked, VFX plays)
- [ ] Structural integrity degrades from charges, ground-pounds, and Front phases
- [ ] All 5 side goals completable
- [ ] Main chain unlocks King's Spire boss zone
- [ ] Topology variant C starts at zone 6 (Apex) correctly
- [ ] Height-tier loot quality scaling works (Apex > Mid > Base)
