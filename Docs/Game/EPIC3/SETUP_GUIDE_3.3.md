# EPIC 3.3 Setup Guide: Zone Restrictions & Traversal Changes

**Status:** Planned
**Requires:** EPIC 3.1 (FrontState, FrontZoneState, FrontZoneData, FrontPhaseEvaluationSystem), Framework Environment/ (hazard damage pipeline), Framework Roguelite/ (zone topology, zone boundaries), Optional: EPIC 1 (chassis limbs for gear-based bypass)

---

## Overview

As the Front converts zones, it imposes traversal restrictions that fundamentally change navigation. Each district has a unique restriction type (RadStorm, AcidicFlood, NanobotSwarm, EMPZone, etc.) that determines what gear, abilities, or tactics are needed to survive in Hostile and Overrun zones. The `ZoneRestrictionSystem` checks the player's current zone state against restriction definitions and applies escalating penalties: damage over time, ability lockout, movement reduction, or hard gates that prevent entry entirely. Players can bypass restrictions with specific equipment or abilities, creating gear-gating that rewards preparation.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| District Entity (Subscene) | `FrontAuthoring` (EPIC 3.1) | FrontState + FrontZoneData |
| Player Prefab (Subscene) | `PlayerTag` | Player identification |
| Framework | Environment/ hazard system | DamageEvent pipeline for zone DOT |
| Framework | Zone tracking system | Determines which zone the player is in |

### New Setup Required

1. Create `FrontRestrictionComponents.cs` with enums and components
2. Create `FrontRestrictionDefinitionSO.cs` ScriptableObject
3. Create one `FrontRestrictionDefinitionSO` per restriction type (10 total)
4. Add `PlayerFrontExposure` + `ZoneRestrictionWarning` to player prefab
5. Link restriction definitions to district `FrontDefinitionSO` assets
6. Create the 3 core systems (ZoneRestriction, ZoneRestrictionWarning, FrontHazardDamage)
7. Create hazard VFX and audio per restriction type

---

## 1. Restriction Definition Assets

**Create:** `Assets > Create > Hollowcore/Front/Restriction Definition`
**Recommended location:** `Assets/Data/Front/Restrictions/`
**Naming convention:** `Restriction_[Type].asset` -- e.g., `Restriction_RadStorm.asset`

### 1.1 Identity

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **RestrictionType** | `FrontRestriction` enum value | (required) | 1-10 (see enum) |
| **DisplayName** | UI-facing name | (required) | Max 32 chars |
| **WarningMessage** | Text shown when approaching restricted zone | (required) | Max 128 chars |
| **WarningIcon** | Sprite for warning UI | (required) | 64x64 recommended |

### 1.2 Gating Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **IsHardGate** | True = cannot enter without bypass | false | bool |
| **ActivationThreshold** | Zone state at which restriction activates | Hostile | Contested, Hostile, or Overrun |

**Hard Gate vs Soft Gate:**

| Type | Behavior | Examples |
|------|----------|---------|
| **Hard Gate** | Player physically cannot enter zone without bypass equipment | CollapseDebris (need grapple), FloodedZone (need swim gear) |
| **Soft Gate** | Player can enter but suffers escalating penalties | RadStorm (DOT), EMPZone (augments disabled), CognitiveStatic (hallucinations) |

### 1.3 Bypass Conditions

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **RequiredEquipmentTag** | Equipment tag that grants bypass | "" (empty) | String tag from Items/ system |
| **RequiredAbilityTag** | Ability tag that grants bypass | "" (empty) | String tag from Abilities/ system |
| **RequiredLimbDefinitionId** | Specific limb that grants bypass | -1 | Valid LimbDefinitionSO ID or -1 |

A player meets bypass conditions if **any** condition is satisfied (OR logic). Players with bypass receive reduced penalties (25% of normal DPS, no lockouts).

### 1.4 Penalties Per Zone State

#### Contested Zone (Front has reached but not fully converted)

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **ContestedPenalties** | Penalty flags active in Contested zones | DamageOverTime | Bitmask |
| **ContestedDPS** | Damage per second in Contested zones | 2 | 0-10 |
| **ContestedMoveSpeedMultiplier** | Movement speed multiplier | 0.85 | 0.3-1.0 |

#### Hostile Zone (Significantly converted)

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **HostilePenalties** | Penalty flags active in Hostile zones | DamageOverTime, MovementReduction | Bitmask |
| **HostileDPS** | Damage per second | 8 | 0-30 |
| **HostileMoveSpeedMultiplier** | Movement speed multiplier | 0.6 | 0.2-1.0 |

#### Overrun Zone (Fully converted)

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **OverrunPenalties** | Penalty flags active in Overrun zones | DamageOverTime, MovementReduction, VisionReduction | Bitmask |
| **OverrunDPS** | Damage per second | 20 | 0-50 |
| **OverrunMoveSpeedMultiplier** | Movement speed multiplier | 0.3 | 0.1-1.0 |

**Tuning tip:** DPS must escalate: Contested < Hostile < Overrun. Movement speed must decrease: Contested > Hostile > Overrun. The validator enforces this. Contested penalties should be "annoying but survivable." Hostile should be "dangerous without preparation." Overrun should be "lethal without specific gear."

### 1.5 Type-Specific Fields

| Field | Applies To | Description | Default | Range |
|-------|-----------|-------------|---------|-------|
| **ContaminationDuration** | BiohazardFog only | Timer before forced retreat (seconds) | 120 | 30-300 |
| **InputDelaySeconds** | CognitiveStatic only | Input lag in seconds | 0.15 | 0.05-0.5 |
| **HallucinationRate** | CognitiveStatic only | False enemies spawned per minute | 6 | 1-20 |

---

## 2. Restriction Type Reference

| Restriction | IsHardGate | Bypass Equipment | Primary Penalty | District Examples |
|------------|-----------|-----------------|----------------|-------------------|
| RadStorm | No (Hard at Overrun) | SealedArmor | DOT | Blacksite Omega |
| AcidicFlood | Yes at Overrun | ChemSuit | Movement, DOT | Canal District |
| NanobotSwarm | No | SealedArmor, Stealth | DOT, VisionReduction | The Nursery |
| HunterPacks | No | (none -- skill based) | EliteSpawn, MovementReduction | Rustfield |
| Firestorm | No | HeatShield | DOT, StaminaDrain | The Undergrid |
| EMPZone | No | AnalogGear | AugmentDisable | Chrome Cathedral |
| FloodedZone | Yes | SwimGear | Movement, DOT | Canal District |
| CollapseDebris | Yes | Grapple | RouteClosure | The Sink |
| CognitiveStatic | No | NeuralShield | InputDelay, Hallucinations | Mirrortown |
| BiohazardFog | No | Inoculation | ContaminationTimer, HealingReduction | The Quarantine |

---

## 3. Player Prefab Additions

### 3.1 PlayerFrontExposure

**Add Component:** `PlayerFrontExposureAuthoring` on player prefab root

| Component | Size | Ghost Config | Notes |
|-----------|------|-------------|-------|
| `PlayerFrontExposure` | 20 bytes | AllPredicted | ActiveRestriction, CurrentZoneState, ActivePenalties, HasBypass, ContaminationTimer, HazardDPS |

Safe for the player archetype (20 bytes).

### 3.2 ZoneRestrictionWarning

**Add Component:** `ZoneRestrictionWarningAuthoring` on player prefab root (baked disabled)

| Component | Size | Ghost Config | Notes |
|-----------|------|-------------|-------|
| `ZoneRestrictionWarning` | 8 bytes | AllPredicted | IEnableableComponent, baked disabled |

Enabled when player approaches a restricted zone boundary (~15m detection). UI reads this for warning prompts.

---

## 4. Linking Restrictions to Districts

### 4.1 FrontDefinitionSO Addition

Add a field to `FrontDefinitionSO`:

| Field | Description | Default |
|-------|-------------|---------|
| **RestrictionDefinition** | Reference to `FrontRestrictionDefinitionSO` for this district | (required) |

Each district's `FrontDefinitionSO` references one `FrontRestrictionDefinitionSO` that defines how its Front restricts traversal.

### 4.2 Multiple Districts Can Share a Restriction Type

Several districts might use the same restriction type (e.g., multiple districts with RadStorm) but with different DPS values. Create separate `FrontRestrictionDefinitionSO` assets if DPS or thresholds differ, or share one asset if values are identical.

---

## 5. Hazard VFX & Audio

**Recommended location:** `Assets/Prefabs/VFX/Front/Restrictions/`

### 5.1 Per-Restriction VFX

| Restriction | VFX | Description |
|-------------|-----|-------------|
| RadStorm | Green particle storm, Geiger counter particles | Radiation visual |
| AcidicFlood | Rising green liquid, splash particles | Acid flooding |
| NanobotSwarm | Tiny metallic particles, swarm clouds | Nanobot visual |
| EMPZone | Blue electrical arcs, flickering lights | EMP field |
| BiohazardFog | Dense green fog, spore particles | Biological hazard |
| CognitiveStatic | Screen distortion, phantom enemy outlines | Hallucination |

### 5.2 Audio Events

| Field | Per Phase | Notes |
|-------|-----------|-------|
| **HazardAudioEvent** | One per restriction type | Ambient loop playing while in restricted zone |

---

## 6. Zone Boundary Warning UI

**Create:** UI prefab in `Assets/Prefabs/UI/Front/ZoneRestrictionWarning.prefab`

| Element | Description |
|---------|-------------|
| **Warning Banner** | Top-of-screen banner with restriction icon + name |
| **Warning Text** | WarningMessage from definition ("Radiation levels critical ahead") |
| **Penalty Preview** | Icon row showing active penalties if player enters |
| **Bypass Status** | Green checkmark if player has bypass gear, red X if not |
| **Hard Gate Indicator** | "IMPASSABLE" label if IsHardGate at current zone state |

The banner appears when `ZoneRestrictionWarning` is enabled on the player entity (within ~15m of restricted zone boundary) and disappears when disabled.

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Subscene | `PlayerFrontExposureAuthoring` on player root | 20 bytes, AllPredicted |
| Player Subscene | `ZoneRestrictionWarningAuthoring` on player root | Baked disabled, 8 bytes |
| District Subscenes | `FrontDefinitionSO` updated with RestrictionDefinition ref | Per district |
| Global Config Subscene | Restriction database blob singleton (auto-baked) | All 10 restriction definitions |
| VFX Assets | 10 hazard VFX prefabs | Per restriction type |
| UI Canvas | `ZoneRestrictionWarning` UI prefab | Warning banner on HUD |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| DPS not escalating (OverrunDPS < HostileDPS) | Overrun zones feel safer than Hostile | Validator enforces: Contested <= Hostile <= Overrun |
| MoveSpeed not decreasing (OverrunMoveSpeed > HostileMoveSpeed) | Overrun zones feel faster than Hostile | Validator enforces: Contested >= Hostile >= Overrun |
| ActivationThreshold set to Safe | Penalties active in Safe zones (unfair) | Minimum threshold is Contested |
| IsHardGate=true but no bypass condition defined | Zone is permanently impassable | Set RequiredEquipmentTag or RequiredAbilityTag |
| BiohazardFog ContaminationDuration = 0 | Contamination timer never ticks | Set > 0 for BiohazardFog type |
| CognitiveStatic InputDelaySeconds > 0.5 | Player controls feel broken, rage-quit risk | Cap at 0.3s maximum; 0.15s recommended |
| Bypass equipment not tagged correctly in Items/ system | Player has equipment but bypass not recognized | Verify equipment item has matching tag string |
| PlayerFrontExposure not on player entity | ZoneRestrictionSystem cannot apply penalties | Add authoring, reimport subscene |
| Hard gate pushback sends player into another restricted zone | Infinite pushback loop | Pushback direction should always point toward nearest Safe zone |
| AI enemies not taking hazard damage in restricted zones | Enemies unaffected by Front hazards | Verify FrontHazardDamageSystem queries AI entities with Health in restricted zones |

---

## Verification

1. **Safe Zone** -- Player in a Safe zone. `PlayerFrontExposure.ActiveRestriction` should be None, no penalties active.

2. **Zone Transition** -- Move player into a Contested zone. Console:
   ```
   [ZoneRestrictionSystem] Player E:XX entered Contested zone (RadStorm): DPS=2.0, MoveSpeed=0.85
   ```

3. **Penalty Escalation** -- Move into Hostile zone. DPS and movement reduction should increase per definition values.

4. **Overrun Zone** -- Move into Overrun zone. Maximum penalties applied. Verify health dropping at OverrunDPS rate.

5. **Hard Gate** -- Approach an IsHardGate=true zone without bypass gear. Player should be pushed back to boundary.

6. **Bypass Equipment** -- Equip the required bypass gear. Enter same zone. Penalties should be reduced to 25% of normal.

7. **Warning Banner** -- Walk toward a restricted zone boundary. At ~15m, warning UI should appear with restriction type, penalties, and bypass status.

8. **Warning Dismissal** -- Move away from boundary. Warning should disappear.

9. **BiohazardFog Timer** -- Enter a BiohazardFog zone. `ContaminationTimer` should count down from `ContaminationDuration`.

10. **CognitiveStatic** -- Enter a CognitiveStatic zone. Input delay should be perceptible. Hallucination enemies should spawn at configured rate.

11. **EMPZone** -- Enter an EMPZone. Augment abilities should be disabled (AugmentDisable penalty).

12. **AI Hazard Damage** -- Place an enemy in a Hostile zone. Enemy should take hazard DPS. Check that faction-immune enemies skip damage.

13. **Debug Overlay** -- Toggle `Front/Restrictions/ShowOverlay`. Zones should color by restriction state, player exposure HUD should display real-time penalty values.

14. **Live Tuning** -- Modify DPS values in the `FrontRestrictionDefinitionSO` inspector during play mode. Changes should propagate within one frame via the tuning bridge.
