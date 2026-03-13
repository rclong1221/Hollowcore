# EPIC 3.5 Setup Guide: Pulses

**Status:** Planned
**Requires:** EPIC 3.1 (FrontState, FrontPhaseChangedTag, FrontPhase, FrontZoneData), Framework Roguelite/ (RunModifierStack for temporary modifiers), Framework AI/ (EnemySpawnRequest for enemy surge), Optional: EPIC 3.3 (zone restrictions during pulses)

---

## Overview

Pulses are district-wide dramatic events triggered at Front phase thresholds. When the Front crosses a phase boundary (or a mid-phase progress trigger), the `PulseSystem` selects and fires a Pulse from the district's pool. Pulses are announced with readable warnings (screen flash, audio cue, countdown timer), then apply temporary zone-wide effects: enemy surges, hazard intensification, route closures, boss previews, or communication jams. Pulses are designed to be "oh shit" moments -- memorable, impactful, and district-themed. Each district has 6-10 unique pulse types across its 4 phases.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| District Entity (Subscene) | `FrontAuthoring` (EPIC 3.1) | FrontState + FrontPhaseChangedTag |
| Framework | AI/ (EnemySpawnRequest) | Enemy surge spawning |
| Framework | Roguelite/ (RunModifierStack) | Temporary modifier application |
| Framework | VFX/ pipeline | Pulse ambient VFX |

### New Setup Required

1. Create `PulseComponents.cs` with enums and component structs
2. Create `PulseDefinitionSO.cs` and `DistrictPulseConfigSO.cs`
3. Create pulse definition assets per district (6-10 per district)
4. Create one `DistrictPulseConfigSO` per district
5. Update `FrontAuthoring` baker to add PulseState, PulseActiveTag, PulseHistory
6. Create the 3 core systems (PulseSystem, PulseEffectExecutionSystem, PulseUIBridgeSystem)
7. Create pulse VFX and audio assets
8. Configure pulse spawn points in district scenes

---

## 1. Pulse Definition Assets

**Create:** `Assets > Create > Hollowcore/Front/Pulse Definition`
**Recommended location:** `Assets/Data/Front/Pulses/[DistrictName]/`
**Naming convention:** `Pulse_[DistrictName]_[Name].asset` -- e.g., `Pulse_Necrospire_ScreamingBroadcast.asset`

### 1.1 Identity

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **PulseId** | Unique ID within the district's pulse pool | (required) | Unique per DistrictPulseConfigSO |
| **DisplayName** | Name shown in warning banner | (required) | Max 32 chars |
| **WarningText** | Detailed warning message | (required) | Max 128 chars |
| **WarningIcon** | Sprite for warning UI | (recommended) | 64x64 |

### 1.2 Trigger Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **TriggerPhase** | Phase at which this pulse can fire | Phase2_Escalation | Phase1-Phase4 |
| **TriggerProgress** | SpreadProgress within the phase (0-1) | 0.5 | 0.0-1.0 |
| **Priority** | Selection priority when multiple pulses qualify | 1 | 1-10 (higher = fires first) |
| **Repeatable** | Can fire again in a later phase | false | bool |

**Tuning tip:** Assign at least 1 pulse per phase per district. Phase 1 pulses should be introductory (EnemySurge, AmbienceShift). Phase 3-4 pulses should be dramatic (BossPreview, RouteClosure + HazardIntensification). TriggerProgress of 0.5 means the pulse fires halfway through the phase's SpreadProgress range.

### 1.3 Timing

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **WarningDuration** | Countdown before effects begin (seconds) | 8 | 3-15 |
| **EffectDuration** | How long effects last (seconds) | 30 | 10-120 |

**Tuning tip:** WarningDuration must be long enough for players to read the warning and reposition (minimum 5s for complex pulses like RouteClosure). EffectDuration should match the intensity -- short and sharp (15s) for EnemySurge, longer (45-60s) for HazardIntensification.

### 1.4 Effects Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Effects** | Bitmask of `PulseEffectType` flags | (required) | At least one flag set |

### 1.5 Effect-Specific Fields

#### Enemy Surge

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **SurgeEnemyCount** | Number of enemies to spawn | 8 | 3-30 |
| **SurgeSpawnTable** | Spawn table for surge enemies (null = district default) | null | |

#### Elite Spawn

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **EliteCount** | Number of elites/mini-bosses to spawn | 1 | 1-3 |

#### Route Changes

| Field | Description | Default |
|-------|-------------|---------|
| **ClosedRouteZoneIds** | Zone IDs that become impassable | [] |
| **OpenedRouteZoneIds** | Zone IDs that temporarily open as shortcuts | [] |

#### Hazard Intensification

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **HazardDPSMultiplier** | Multiplier on existing zone hazard DPS | 2.0 | 1.0-5.0 |

#### Boss Preview

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **BossPreviewPrefab** | Boss variant prefab | null | |
| **BossRetreatThreshold** | HP fraction at which boss retreats | 0.3 | 0.1-0.5 |

#### Run Modifier

| Field | Description | Default |
|-------|-------------|---------|
| **RunModifierOverride** | Temporary RunModifierSO applied during pulse | null |

### 1.6 Visual/Audio

| Field | Description | Default |
|-------|-------------|---------|
| **ScreenFlashColor** | Color of screen flash on pulse trigger | Red |
| **WarningAudioEvent** | Audio event during warning countdown | (required) |
| **ActiveAudioEvent** | Audio event during active effects | (required) |
| **PulseVFXPrefab** | Ambient VFX prefab active during pulse | (recommended) |

---

## 2. District Pulse Config Assets

**Create:** `Assets > Create > Hollowcore/Front/District Pulse Config`
**Recommended location:** `Assets/Data/Front/Pulses/`
**Naming convention:** `PulseConfig_[DistrictName].asset` -- e.g., `PulseConfig_Necrospire.asset`

### 2.1 Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Pulses** | Array of `PulseDefinitionSO` references | (required) | 6-10 entries |
| **MaxSimultaneousPulses** | Max active pulses at once | 1 | 1-2 (2 for boss districts) |
| **PulseCooldown** | Minimum seconds between pulses | 45 | 20-120 |
| **EscalateOnRevisit** | Increase intensity on repeat district visits | false | bool |

### 2.2 Pulse Distribution Guideline

| Phase | Recommended Pulse Count | Typical Effects |
|-------|------------------------|-----------------|
| Phase 1 (Onset) | 1-2 | AmbienceShift, minor EnemySurge |
| Phase 2 (Escalation) | 2-3 | EnemySurge, HazardIntensification, EliteSpawn |
| Phase 3 (Crisis) | 2-3 | RouteClosure + HazardIntensification, BossPreview, CommunicationJam |
| Phase 4 (Overrun) | 1-2 | All-out: combined effects, highest intensity |

**Tuning tip:** Every phase must have at least 1 pulse. Phase 2-3 should have the most variety (2-3 each) since players spend the most time there. Phase 4 pulses should feel apocalyptic -- combine multiple effect types.

### 2.3 Linking to FrontDefinitionSO

Add a field to `FrontDefinitionSO`:

| Field | Description |
|-------|-------------|
| **DistrictPulseConfig** | Reference to this district's `DistrictPulseConfigSO` |

---

## 3. Example Pulse Configurations

### Necrospire District

| Pulse | Phase | Effects | Duration | Notes |
|-------|-------|---------|----------|-------|
| Screaming Broadcast | Phase 2 | EnemySurge + AmbienceShift | 25s | 8 ghost enemies + eerie audio |
| Grief-Link Resonance | Phase 3 | HazardIntensification + CommunicationJam | 40s | 2x DPS + no minimap |
| Specter Convergence | Phase 3 | EliteSpawn + RouteClosure | 35s | 2 elite specters + blocked routes |
| The Recursion | Phase 4 | BossPreview + EnemySurge + HazardIntensification | 60s | Boss variant + 15 enemies + 3x DPS |

### The Burn District

| Pulse | Phase | Effects | Duration | Notes |
|-------|-------|---------|----------|-------|
| Core Venting | Phase 2 | HazardIntensification + RouteClosure | 20s | Fire waves close corridors |
| Thermal Cascade | Phase 3 | EnemySurge + EnvironmentShift | 30s | 12 enemies + gravity shift |
| Meltdown Preview | Phase 4 | BossPreview + ResourceDrain | 45s | Boss variant + resource drain |

---

## 4. FrontAuthoring Baker Updates

Update the `FrontAuthoring` baker to add pulse components to the district entity:

| Component | Size | Ghost Config | Notes |
|-----------|------|-------------|-------|
| `PulseState` | 40 bytes | All | IsActive, IsWarning, ActivePulseId, durations, etc. |
| `PulseActiveTag` | 0 bytes | All | IEnableableComponent, baked disabled |
| `PulseHistory` buffer | 12 bytes x entry | None | External buffer (InternalBufferCapacity=0) |

---

## 5. Pulse Spawn Points

**Setup:** Tagged transform markers in district scenes where surge enemies appear

### 5.1 Spawn Point Prefab

**Create:** `Assets/Prefabs/Front/PulseSpawnPoint.prefab`

| Component | Field | Description |
|-----------|-------|-------------|
| **PulseSpawnPointAuthoring** | SpawnPointId | Unique per district |
| | ValidForPhases | Bitmask of phases this point is active |
| | MaxEnemiesAtPoint | Cap on simultaneous spawns at this point |
| | SpawnRadius | Spread radius around the point |

### 5.2 Placement Guidelines

| Guideline | Details |
|-----------|---------|
| **Count** | 4-8 spawn points per district |
| **Distribution** | Spread across Contested and Hostile zones |
| **Visibility** | Players should be able to see surge enemies approaching (no invisible spawns behind the player) |
| **Accessibility** | Spawn points near chokepoints create dramatic holdout moments |

**Tuning tip:** Place 2-3 spawn points near the player's likely path through each phase. Avoid spawning enemies directly on top of the player -- use spawn points 20-40m away so the player sees them coming. This turns surges into tactical moments, not cheap deaths.

---

## 6. System Execution Order

| System | Update Group | Order | Purpose |
|--------|-------------|-------|---------|
| `PulseSystem` | SimulationSystemGroup | After FrontPhaseEvaluationSystem | Selects and triggers pulses |
| `PulseEffectExecutionSystem` | SimulationSystemGroup | After PulseSystem | Executes active pulse effects |
| `PulseUIBridgeSystem` | PresentationSystemGroup | Client/Local only | Warning banners, countdowns, VFX |

---

## 7. Pulse Warning UI

**Create:** UI prefab in `Assets/Prefabs/UI/Front/PulseWarningBanner.prefab`

### 7.1 Warning Phase Elements

| Element | Description |
|---------|-------------|
| **Warning Banner** | Full-width top banner with pulse name + countdown |
| **Warning Icon** | WarningIcon from PulseDefinitionSO |
| **Countdown Timer** | Large countdown numbers (8... 7... 6...) |
| **Screen Flash** | ScreenFlashColor tint on trigger |
| **Warning Audio** | WarningAudioEvent plays during countdown |

### 7.2 Active Phase Elements

| Element | Description |
|---------|-------------|
| **Active Indicator** | Compact HUD element with pulse name + duration remaining |
| **Effect Icons** | Row of icons for active effects (lit = active) |
| **Duration Bar** | Depleting bar showing remaining effect time |
| **Active Audio** | ActiveAudioEvent loop during effects |
| **Ambient VFX** | PulseVFXPrefab instantiated in world |

### 7.3 End Phase

| Element | Description |
|---------|-------------|
| **End Sting** | Brief "pulse ended" audio cue |
| **VFX Cleanup** | Destroy ambient VFX instances |
| **UI Fade** | Banner fades out over 0.5s |

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| District Subscenes | `FrontDefinitionSO` updated with DistrictPulseConfig ref | Per district |
| District Subscenes | Pulse spawn point prefabs (4-8 per district) | Tagged transforms |
| FrontAuthoring Baker | PulseState + PulseActiveTag + PulseHistory | On district entity |
| Ghost Prefab Registry | Boss preview prefabs (for BossPreview pulses) | Per district that has BossPreview |
| VFX Assets | Pulse ambient VFX prefabs | Per pulse that has PulseVFXPrefab |
| UI Canvas | `PulseWarningBanner` prefab | Warning + active indicators |
| Audio | Warning + active audio events per pulse | Per PulseDefinitionSO |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| DistrictPulseConfigSO.Pulses array has fewer than 6 entries | Validator error; some phases have no pulses | GDD requires 6-10 per district |
| No pulse assigned to Phase 1 | Phase 1 passes silently with no dramatic moment | Assign at least 1 introductory pulse to Phase 1 |
| WarningDuration < 3s | Players cannot read warning text | Minimum 3s enforced by validator |
| EffectDuration = 0 | Pulse triggers but has no visible effect | Must be > 0 |
| EnemySurge set but SurgeEnemyCount = 0 | No enemies spawn despite surge effect | Set SurgeEnemyCount > 0 when EnemySurge flag is set |
| BossPreview set but BossPreviewPrefab is null | Null reference on pulse execution | Assign prefab when BossPreview effect is used |
| ClosedRouteZoneIds empty when RouteClosure set | No routes actually close | Populate zone IDs when RouteClosure effect is used |
| Duplicate PulseId within a DistrictPulseConfigSO | History check fails, pulse fires repeatedly | PulseId must be unique within the district pool |
| PulseCooldown too short (<20s) | Pulses fire back-to-back, overwhelming | Minimum 20s recommended; 45s is comfortable |
| MaxSimultaneousPulses > 1 on non-boss district | Overlapping pulses create chaos beyond design intent | Use 1 for standard districts, 2 only for boss districts |
| Spawn points not in converted zones | Surge enemies spawn in Safe zones (breaks immersion) | Place spawn points in zones with ConversionOrder early enough to be converted |
| HazardDPSMultiplier < 1.0 | Hazard actually becomes less dangerous during pulse | Minimum 1.0 enforced by validator |

---

## Verification

1. **Pulse Trigger** -- Advance Front to Phase 2 boundary. Console:
   ```
   [PulseSystem] District E:XX triggering pulse: "Screaming Broadcast" (Phase2, Priority=5)
   ```

2. **Warning Phase** -- Warning banner appears with countdown timer. Audio plays. Screen flash on trigger.

3. **Warning Countdown** -- Timer counts down from WarningDuration. At 0:
   ```
   [PulseSystem] Pulse "Screaming Broadcast" WARNING -> ACTIVE
   ```

4. **PulseActiveTag** -- Tag enables when effects begin, disables when duration expires.

5. **Enemy Surge** -- SurgeEnemyCount enemies spawn at pulse spawn points during active phase:
   ```
   [PulseEffectExecutionSystem] EnemySurge: spawning 8 enemies at 3 spawn points
   ```

6. **Hazard Intensification** -- Existing zone hazard DPS multiplied by HazardDPSMultiplier during pulse.

7. **Route Closure** -- Specified zones become impassable. Player attempting entry is blocked.

8. **Route Opening** -- Specified zones temporarily become accessible (shortcuts).

9. **Boss Preview** -- Boss variant spawns, fights until BossRetreatThreshold HP, then retreats (despawns).

10. **Communication Jam** -- Minimap and markers disabled during pulse.

11. **Pulse End** -- Duration expires. Effects removed, VFX cleaned up, UI fades:
    ```
    [PulseSystem] Pulse "Screaming Broadcast" ENDED after 25s
    ```

12. **No Repeat** -- Same pulse should not fire again (unless Repeatable=true). Check PulseHistory buffer.

13. **Cooldown** -- Another pulse should not fire until PulseCooldown has elapsed.

14. **Priority** -- When multiple pulses qualify at the same progress point, highest Priority fires first.

15. **Cross-District** -- Pulses in one district should not affect another district's entities.

16. **Debug Overlay** -- Toggle `Front/Pulses/ShowState`. HUD shows active pulse name, countdown/duration, effect flags, PulsesFired counter.
