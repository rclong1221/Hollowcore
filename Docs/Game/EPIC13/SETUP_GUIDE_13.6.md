# EPIC 13.6 Setup Guide: Necrospire Zone Setup & Corruption Hazard Prefabs

**Status:** Planned
**Requires:** 13.1-13.5 (all pipeline sub-epics), EPIC 3 (Front system), EPIC 14 (Boss: Grandmother Null)

---

## Overview

Set up the Necrospire -- District 1, the vertical slice anchor. A towering data necropolis with Corruption Bloom as its Front type, 4 factions (Mourning Collective, Recursive Specters, Archive Wardens, The Inheritors), 10 zones in concentric rings, 8 side goals, and a main chain leading to Grandmother Null. This guide covers the district-specific config singleton, corruption hazard prefabs, and zone-by-zone setup.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| DistrictDefinitionSO for Necrospire | EPIC 13.1 complete | Master definition |
| 4 FactionDefinitionSOs | EPIC 13.3 complete | Mourning Collective, Recursive Specters, Archive Wardens, The Inheritors |
| FrontDefinitionSO | EPIC 3 | Corruption Bloom, 4 phases |
| 12+ enemy prefabs | EPIC 13.3 | 3 per faction minimum |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| NecrospireConfig.cs | `Assets/Scripts/District/Necrospire/` | C# (ECS singleton) |
| CorruptionBloomComponents.cs | `Assets/Scripts/District/Necrospire/` | C# (ECS) |
| DistrictDefinitionSO | `Assets/Data/Districts/Necrospire/Necrospire_District.asset` | ScriptableObject |
| 8 QuestDefinitionSOs | `Assets/Data/Districts/Necrospire/Goals/` | ScriptableObject |
| 5 Landmark composition prefabs | `Assets/Prefabs/Districts/Necrospire/Landmarks/` | Prefab |
| Corruption hazard prefabs | `Assets/Prefabs/Districts/Necrospire/Hazards/` | Prefab |

---

## 1. Create NecrospireConfig Singleton

### 1.1 Singleton Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| BaseCorruptionSpreadRate | Zones per minute at phase 1 | 0.5 | 0.1-2.0 |
| HologramOcclusionAlpha | Hologram surface opacity | 0.2 | 0.0-1.0 |
| BiometricLockDuration | Hack time in seconds | 4.0 | 1.0-10.0 |
| PhaseVentDPS | Damage per second from phase vents | 10.0 | 1.0-50.0 |

**Tuning tip:** BaseCorruptionSpreadRate = 0.5 means it takes ~2 minutes per zone at phase 1. At phase 3 (specter multiplication), the effective rate doubles. Keep phase 1 gentle so players can explore, then ramp pressure.

---

## 2. Configure the 10-Zone Graph

### 2.1 Zone Layout Table
| Zone | Name | Type | Faction | Connections | Notes |
|------|------|------|---------|-------------|-------|
| 0 | Pilgrim's Gate | Combat | MourningCollective(0) | 1, 3 | Outer ring start |
| 1 | Relay Corridor | Combat | ArchiveWardens(2) | 0, 2, 4 | Outer ring, Warden patrol |
| 2 | Clone Market | Shop | TheInheritors(3) | 1, 5 | Outer ring, limb trade |
| 3 | Shrine Hall | Elite | MourningCollective(0) | 0, 4, 6 | Middle ring, synchronized widows |
| 4 | Data Nexus | Combat | RecursiveSpecters(1) | 1, 3, 5, 7 | Middle ring hub, 4 connections |
| 5 | Inheritance Ward | Combat | TheInheritors(3) | 2, 4, 8 | Middle ring, gene thieves |
| 6 | Warden Bastion | Elite | ArchiveWardens(2) | 3, 7 | Inner ring, security HQ |
| 7 | Echo Chamber | Event | RecursiveSpecters(1) | 4, 6, 8 | Inner ring, echo encounter |
| 8 | Upload Sanctum | Rest | MourningCollective(0) | 5, 7, 9 | Inner ring, safe zone |
| 9 | Corruption Nexus | Boss | All | 8 | Core, boss arena |

### 2.2 Entry Points by Variant
| Variant | Name | Entry Zones | Key Difference |
|---------|------|-------------|----------------|
| A | Clockwise | [0] | Standard spiral inward |
| B | Split | [0, 2] | Zone 4 blocked until zone 7 cleared |
| C | Inverted | [6] | Start mid-ring, push outward and inward |

---

## 3. Create Corruption Hazard Prefabs

### 3.1 Data Conduit (Corruption Spreader)
```
DataConduit_Hazard (root)
  Visual/
    ConduitPipe (MeshRenderer, emissive corruption material)
    CorruptionParticles (ParticleSystem, dark purple)
  Logic/
    CorruptionConduitAuthoring (ZoneCorruptionEntry.HasActiveConduits)
  Collider/
    (no damage collider -- corruption is zone-level, not contact-based)
```

### 3.2 Phase Vent (Environmental DPS)
```
PhaseVent_Hazard (root)
  Visual/
    VentGrate (MeshRenderer)
    SteamFX (ParticleSystem, purple-tinted steam)
  Damage/
    HazardZone (trigger collider, applies PhaseVentDPS to players inside)
    HazardIndicator (visual warning ring before activation)
  Logic/
    PhaseVentAuthoring (activates at Phase 4, or per-zone at Phase 2+)
```

| Setting | Default | Range |
|---------|---------|-------|
| Vent Radius | 3.0m | 1.0-5.0 |
| Activation Delay | 1.5s | 0.5-3.0 |
| DPS | Uses NecrospireConfig.PhaseVentDPS | -- |

### 3.3 Hologram Occlusion Surface
```
HologramOcclusion_Surface (root)
  Visual/
    OcclusionPlane (MeshRenderer, semi-transparent hologram shader)
  Logic/
    HologramOcclusionAuthoring (alpha from NecrospireConfig.HologramOcclusionAlpha)
    Scales with Front phase: Phase 1 = 0.2, Phase 2 = 0.5, Phase 3 = 0.8
```

### 3.4 Biometric Lock Door
```
BiometricLock_Door (root)
  Visual/
    DoorMesh (MeshRenderer, security door)
    Scanner (MeshRenderer, biometric panel)
    StatusLight (green=open, red=locked)
  Interaction/
    InteractableAuthoring (hold-to-hack, duration from BiometricLockDuration)
  Logic/
    BiometricLockAuthoring (locked during Phase 2+, hackable)
```

---

## 4. Wire Side Goals

### 4.1 Side Goal Summary
| # | Name | Zone(s) | Insurance | Counterplay |
|---|------|---------|-----------|-------------|
| 1 | Sever the Grief-Link | 0, 3, 8 | DisableAbility: Grief Resonance | SlowSpread (30s) |
| 2 | Recover the Intact Upload | 7 | RevealWeakpoint: Core memory | None |
| 3 | Data Vampire Cache | 2, 5 | RemoveAdd: Clone Blank wave | None |
| 4 | Silence the Screaming Server | 4 | DisableAbility: Screaming Broadcast | DelayPhase (60s) |
| 5 | The Living Will | 7 -> 2 | ReduceHealth: -15% boss HP | None |
| 6 | Debug the Widow | 3 | DisablePhase: Widow summon | PurgeZone (zone 3) |
| 7 | Black Mass Disruption | 0 | RemoveAdd: Collective reinforcements | RedirectFront (zone 0) |
| 8 | Mercy Protocol | 1, 6 | DisableAbility: Nerve-Gas | SlowSpread (45s) |

---

## 5. Wire Main Chain

| Step | Objective | Zone | Trigger |
|------|-----------|------|---------|
| 0 | Reach the Data Nexus | 4 | Zone entry |
| 1 | Retrieve Purge Key (kill Lockdown Coordinator) | 6 | Kill target |
| 2 | Activate Upload Sanctum override terminal | 8 | Interact |
| 3 | Enter Corruption Nexus, confront Grandmother Null | 9 | Boss fight |

---

## Scene & Subscene Checklist

- [ ] `Necrospire_District.asset` created with all 10 zones, 3 variants, 4 factions, Front, 8 goals, boss
- [ ] NecrospireConfig singleton entity in subscene with default values
- [ ] Corruption hazard prefabs (conduit, phase vent, hologram surface, biometric lock) created
- [ ] 12+ enemy prefabs across 4 factions (see EPIC 13.3)
- [ ] 5 landmark composition prefabs for POIs (see EPIC 13.5)
- [ ] MicroPOIPoolSO for Necrospire with broken terminals, grief totems, scanners, drone nests
- [ ] FrontDefinitionSO: Corruption Bloom with 4 phases timed at 180s each
- [ ] 8 QuestDefinitionSOs + 1 main chain (4 steps) wired into Goals array

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Corruption spreads too fast at Phase 1 | Player overwhelmed before exploring | Reduce BaseCorruptionSpreadRate (try 0.3) |
| Phase vent damage too high | Players avoid entire zones | Reduce PhaseVentDPS or add warning indicator |
| Biometric lock duration too long | Frustrating during combat (Wardens attack while hacking) | Reduce to 2-3s, or allow combat-interrupt with resume |
| Hologram occlusion too opaque at Phase 1 | Players confused by invisible walls early | Keep HologramOcclusionAlpha = 0.2 at Phase 1 |
| Zone 4 (Data Nexus) has 4 connections but is Combat type | AI gets overwhelmed by multi-directional player approach | This is intended; adjust enemy count via encounter pool |
| Boss zone only connects to zone 8 | Player has single approach path | Intentional by design for Necrospire; variants may add alternate access |

---

## Verification

- [ ] 3 topology variants produce visibly different layouts for different seeds
- [ ] Corruption Bloom spreads from zone 9 outward along data conduits
- [ ] Phase transitions at 180s, 360s, 540s (within 1s tolerance)
- [ ] Phase vents activate at correct phases
- [ ] Biometric locks lock at Phase 2+, hackable within configured duration
- [ ] Hologram occlusion scales with phase
- [ ] All 8 side goals completable and tracked
- [ ] Main chain sequential completion unlocks boss zone (zone 9)
- [ ] Boss insurance effects recorded on goal completion
- [ ] Front counterplay effects modify corruption spread correctly
