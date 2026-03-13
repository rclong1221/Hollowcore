# EPIC 13.7 Setup Guide: Burn Heat System & Environmental Hazard Prefabs

**Status:** Planned
**Requires:** 13.1-13.5 (all pipeline sub-epics), EPIC 3 (Front system), EPIC 14 (Boss: The Foreman)

---

## Overview

Set up The Burn -- District 6, the second vertical slice district. A perpetual industrial hell with heat management as the core mechanic. Players accumulate heat from ambient environment, furnaces, and enemy attacks. The Front type is Overheat Cascade. This guide covers the HeatState component, BurnConfig singleton, heat hazard prefabs (slag rivers, coolant gates, conveyor belts, vent timing), and zone-by-zone setup.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| DistrictDefinitionSO for Burn | EPIC 13.1 complete | Master definition |
| 4 FactionDefinitionSOs | EPIC 13.3 complete | Slag Walkers, Waste Management, The Ashborn, Scrap Hives |
| FrontDefinitionSO | EPIC 3 | Overheat Cascade, 4 phases |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| HeatComponents.cs | `Assets/Scripts/District/Burn/` | C# (ECS) |
| HeatAccumulationSystem.cs | `Assets/Scripts/District/Burn/Systems/` | C# (ISystem) |
| HeatDamageSystem.cs | `Assets/Scripts/District/Burn/Systems/` | C# (ISystem) |
| HeatDissipationSystem.cs | `Assets/Scripts/District/Burn/Systems/` | C# (ISystem) |
| Burn_District.asset | `Assets/Data/Districts/Burn/` | ScriptableObject |
| Heat hazard prefabs | `Assets/Prefabs/Districts/Burn/Hazards/` | Prefab |
| Conveyor belt prefab | `Assets/Prefabs/Districts/Burn/Traversal/` | Prefab |

---

## 1. Create BurnConfig Singleton

### 1.1 BurnConfig Fields
| Field | Description | Default | Range | Phase Scaling |
|-------|-------------|---------|-------|---------------|
| AmbientHeatRate | Base heat gain per second in all zones | 0.05 | 0.01-0.2 | Phase 1: +50% in furnace-adjacent zones |
| FurnaceHeatRate | Heat gain near active furnaces | 0.3 | 0.1-1.0 | Phase 3: lethal in zone 5 without gear |
| SlagRiverDPS | Direct health damage from slag rivers | 50.0 | 10-100 | Constant, not phase-scaled |
| CoolantGateReduction | Heat removed per gate activation | 0.3 | 0.1-0.5 | Phase 2: charges halved |
| ConveyorSpeed | Moving belt speed (m/s) | 3.0 | 1.0-10.0 | Phase 3: doubles to 6.0 |
| VentCycleDuration | Seconds per open/close cycle | 5.0 | 2.0-15.0 | Constant |

**Tuning tip:** AmbientHeatRate of 0.05 means it takes 20 seconds to accumulate 1.0 heat if standing still. With OverheatThreshold at 0.8, a player has 16 seconds before they start taking damage. This creates a timer for exploration before the player must find cooling.

---

## 2. Create HeatState Component (Player)

Added to player entity when entering The Burn district; removed on exit.

### 2.1 HeatState Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| CurrentHeat | Current heat level (0.0 = cool, 1.0 = overheating) | 0.0 | 0.0-1.0+ |
| DissipationRate | Passive cooling per second | 0.05 | 0.01-0.2 |
| OverheatThreshold | Heat level above which damage starts | 0.8 | 0.5-1.0 |
| OverheatDPS | Health damage per second when overheating | 5.0 | 1.0-20.0 |

### 2.2 Heat System Pipeline
```
HeatAccumulationSystem (accumulates from environment + enemies)
  |
  v
HeatDissipationSystem (passive cooling, coolant gate reduction)
  |
  v
HeatDamageSystem (applies health damage if CurrentHeat > OverheatThreshold)
```

**All three systems:** `[BurstCompile]`, `ServerSimulation | LocalSimulation`, `PredictedFixedStepSimulationSystemGroup`

---

## 3. Configure the 9-Zone Graph

### 3.1 Zone Layout Table
| Zone | Name | Type | Faction | Connections | Heat Mult |
|------|------|------|---------|-------------|-----------|
| 0 | Loading Docks | Combat | WasteManagement(1) | 1 | 1.0 |
| 1 | Conveyor Alpha | Combat | SlagWalkers(0) | 0, 2, 3 | 1.5 |
| 2 | Smelting Floor | Elite | SlagWalkers(0) | 1, 5 | 2.0 |
| 3 | Worker Barracks | Rest | WasteManagement(1) | 1, 4 | 0.5 |
| 4 | Recycler Pit | Combat | ScrapHives(3) | 3, 5 | 1.0 |
| 5 | Central Furnace | Elite | TheAshborn(2) | 2, 4, 6 | 3.0 |
| 6 | Coolant Reservoir | Shop | WasteManagement(1) | 5, 7 | 0.3 |
| 7 | Ashborn Temple | Event | TheAshborn(2) | 6, 8 | 1.5 |
| 8 | Furnace Heart | Boss | All | 7 | 2.0 |

### 3.2 ZoneHeatEntry Buffer
Populated at district load. One entry per zone with:
- `HeatMultiplier`: multiplied with AmbientHeatRate for zone-specific heat
- `HasCoolantGate`: true for zones 3, 6
- `FurnaceActive`: true for zones 2, 5 (disabled by "Cool the Core" quest)

---

## 4. Create Heat Hazard Prefabs

### 4.1 Slag River (Lethal Terrain)
```
SlagRiver_Hazard (root)
  Visual/
    SlagSurface (MeshRenderer, animated lava/slag shader)
    SteamParticles (ParticleSystem, rising steam)
    GlowEmission (Point Lights, orange-red)
  Damage/
    SlagTrigger (trigger collider, flat plane at slag surface)
    SlagDamageZone (applies SlagRiverDPS on contact)
  Audio/
    BubblingAmbience (AudioSource, loop)
```

| Setting | Default | Range |
|---------|---------|-------|
| DPS | Uses BurnConfig.SlagRiverDPS (50) | -- |
| Heat gain on contact | 0.2/s (additional to DPS) | 0.1-0.5 |

### 4.2 Coolant Gate (Heat Reduction Interactable)
```
CoolantGate_Interactable (root)
  Visual/
    GateFrame (MeshRenderer, industrial gate)
    CoolantSpray (ParticleSystem, blue mist, plays on activation)
    ChargeIndicator (3D text: "Charges: 3/5")
  Interaction/
    InteractableAuthoring (instant, press E)
  Logic/
    CoolantGateAuthoring
      MaxCharges: 5
      HeatReduction: BurnConfig.CoolantGateReduction (0.3)
```

| Setting | Default | Range |
|---------|---------|-------|
| MaxCharges | 5 | 3-10 |
| HeatReduction per use | 0.3 | 0.1-0.5 |
| Phase 2 charge reduction | 50% (MaxCharges halved) | -- |

### 4.3 Conveyor Belt (Moving Platform)
```
ConveyorBelt_Platform (root)
  Visual/
    BeltSurface (MeshRenderer, animated UV scroll)
    Rollers (MeshRenderer)
  Logic/
    ConveyorAuthoring
      Speed: BurnConfig.ConveyorSpeed (3.0)
      Direction: float3 (unit vector)
  Physics/
    PhysicsShapeAuthoring (box, kinematic)
    MovingPlatformAuthoring (framework EPIC 13.1 moving platform)
```

| Setting | Default | Range |
|---------|---------|-------|
| Speed | 3.0 m/s | 1.0-10.0 |
| Phase 3 speed | 6.0 m/s (doubled) | -- |
| Belt Width | 3.0m | 2.0-5.0 |

### 4.4 Steam Vent (Timed Hazard)
```
SteamVent_Hazard (root)
  Visual/
    VentGrate (MeshRenderer)
    SteamBlast (ParticleSystem, plays during active phase)
    WarningLight (blinking before activation)
  Damage/
    VentTrigger (trigger collider, cylindrical)
    VentDamage (DPS during active phase)
  Logic/
    SteamVentAuthoring
      CycleDuration: BurnConfig.VentCycleDuration (5.0)
      ActiveRatio: 0.4 (vent active for 40% of cycle)
      DPS: 15.0
```

| Setting | Default | Range |
|---------|---------|-------|
| CycleDuration | 5.0s | 2.0-15.0 |
| ActiveRatio | 0.4 | 0.2-0.6 |
| DPS during blast | 15.0 | 5.0-30.0 |
| Warning time | 1.0s before blast | 0.5-2.0 |

---

## 5. Wire Side Goals and Main Chain

### 5.1 Side Goals
| # | Name | Zones | Key Mechanic |
|---|------|-------|-------------|
| 1 | Free the Chain Gang | 0, 1, 3 | Interact with 3 prisoner locks |
| 2 | Heat-Cracked Chassis | Elite zones | Collect 2 heat-resistant limbs from Slag Walker elites |
| 3 | The Firewalker's Arm | 7 | Kill Reforged, rip arm |
| 4 | Cool the Core | 2, 4, 5, 6 | Activate 4 emergency coolant valves |
| 5 | The Whistleblower | 3 | Stealth retrieve corporate data |

### 5.2 Main Chain
| Step | Objective | Zone |
|------|-----------|------|
| 0 | Reach Smelting Floor | 2 |
| 1 | Retrieve Override Key from Quota Master | 0 or 3 |
| 2 | Sabotage Central Furnace coolant regulators | 5 |
| 3 | Enter Furnace Heart, confront The Foreman | 8 |

---

## Scene & Subscene Checklist

- [ ] `Burn_District.asset` created with 9 zones, 2 variants, 4 factions, Front, 5 goals, boss
- [ ] BurnConfig singleton entity in subscene with default values
- [ ] ZoneHeatEntry buffer populated with per-zone heat multipliers
- [ ] Slag river prefabs placed in zones 2, 5 (Smelting Floor, Central Furnace)
- [ ] Coolant gate prefabs placed in zones 3, 6
- [ ] Conveyor belt prefabs placed in zones 1, 2 (Conveyor Alpha, Smelting Floor)
- [ ] Steam vent prefabs placed in corridor connections
- [ ] HeatState added to player entity on district entry, removed on exit

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| AmbientHeatRate too high | Players overheat in Rest zones | Check zone 3 HeatMultiplier (should be 0.5) and base rate |
| Coolant gate charges not halved at Phase 2 | Players have too many charges in late game | Front phase system must modify MaxCharges on CoolantGate entities |
| Conveyor speed not doubled at Phase 3 | Traversal difficulty doesn't escalate | ConveyorAuthoring must read FrontPhase and adjust speed |
| Slag river DPS too low | Players walk through slag instead of routing around | Increase SlagRiverDPS to 50+ (should be near-instant kill) |
| HeatState not removed on district exit | Heat accumulates in non-Burn districts | DistrictTransition cleanup must remove HeatState from player entity |
| Vent timing not visible | Players take damage without warning | Add 1.0s warning (blinking light, sound) before vent activates |

---

## Verification

- [ ] HeatState accumulates heat at correct rate per zone (AmbientHeatRate * HeatMultiplier)
- [ ] Overheat threshold triggers continuous health damage
- [ ] Coolant gates reduce heat and consume charges
- [ ] Conveyor belts move player at configured speed
- [ ] Vent timing blocks corridors on correct cycle with warning
- [ ] Slag rivers deal lethal DPS on contact
- [ ] Phase transitions modify environment (slag expansion, belt speed, coolant reduction)
- [ ] All 5 side goals completable
- [ ] Main chain unlocks Furnace Heart boss zone
- [ ] Heat-themed echo effects apply on subsequent runs
