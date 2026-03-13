# EPIC 2.1 Setup Guide: Soul Chip Core

**Status:** Planned
**Requires:** Framework Combat/DeathState (DeathTransitionSystem), Framework Persistence/ (ISaveModule), Framework Interaction/ system

---

## Overview

The Soul Chip is the player's consciousness -- the one persistent element across body destruction. It tracks transfer count, degradation level, and identity. On death, the chip ejects from the body as a world entity. Degradation accumulates after 3+ transfers, applying escalating stat penalties, input delay, memory glitches, and Compendium page loss. The Soul Chip is the core identity thread that makes death meaningful without being punitive in the first few deaths.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab (Subscene) | `DamageableAuthoring` | Health/death pipeline |
| Player Prefab (Subscene) | `PlayerTag` | Identifies player entities |
| Framework | `DeathTransitionSystem` | Detects player death state change |
| Framework | `EquippedStatsSystem` | Receives degradation penalty modifiers |
| Framework | Persistence/ (ISaveModule) | Saving chip state across sessions |

### New Setup Required

1. Add `SoulChipAuthoring` to the player prefab in the subscene
2. Create the `EjectedSoulChip` prefab (world pickup entity)
3. Create a `SoulChipConfig` singleton asset for degradation tuning
4. Set up degradation visual effects (screen distortion, audio warping)
5. Create the Soul Chip status UI indicator
6. (Optional) Configure drone insurance as a consumable item

---

## 1. Player Prefab Setup -- SoulChipAuthoring

**Add Component:** `SoulChipAuthoring` on the player prefab root in the subscene

### 1.1 Baked Components

The baker creates the following on the player entity:

| Component | Size | Ghost Config | Notes |
|-----------|------|-------------|-------|
| `SoulChipState` | 16 bytes | AllPredicted | SoulId, TransferCount, DegradationTier, IsEmbodied |
| `SoulChipDegradation` | 16 bytes | None (computed locally) | StatMultiplier, MemoryGlitches, InputDelay, CompendiumPagesLost |

Total: 32 bytes added to player archetype. Within safe budget.

### 1.2 SoulChipAuthoring Inspector Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **InitialSoulId** | Override for testing (0 = auto-generate from session) | 0 | 0+ |

**Tuning tip:** Leave InitialSoulId at 0 for production. The `SoulChipBootstrapSystem` generates a unique SoulId from the player's network session at spawn. Set to a fixed value only for deterministic testing.

---

## 2. Ejected Soul Chip Prefab

**Create:** Prefab for the world entity representing an ejected soul chip
**Recommended location:** `Assets/Prefabs/SoulChip/EjectedSoulChip.prefab`

### 2.1 Required Components

| Component | Field | Description | Default |
|-----------|-------|-------------|---------|
| `EjectedSoulChipAuthoring` | SoulId | Set at runtime by ejection system | 0 |
| | TransferCount | Copied from player at death | 0 |
| | SourceBody | Entity ref to dead body | Entity.Null |
| `InteractableAuthoring` | InteractionType | Set to `SoulChipPickup` | SoulChipPickup |
| | Range | Interaction distance (meters) | 3.0 |
| `GhostAuthoringComponent` | PrefabType | Server | Server |
| `PhysicsShapeAuthoring` | Shape | Small sphere for world collision | Sphere r=0.15 |

### 2.2 Visual Setup

| Element | Description |
|---------|-------------|
| **Mesh** | Small glowing orb/crystal (the "chip") |
| **Emission Material** | Bright glow, color tinted by degradation tier (green/yellow/orange/red) |
| **Particle System** | Ambient floating particles around the chip |
| **Point Light** | Low-range light matching emission color |
| **Audio Source** | Looping hum/resonance sound, pitch shifts with degradation tier |

**Tuning tip:** The ejected chip should be impossible to miss visually. Use high emission intensity and a generous point light radius (3-5m). Players who die in chaotic combat need to find their chip quickly.

---

## 3. SoulChipConfig Singleton

**Create:** `Assets > Create > Hollowcore/SoulChip/SoulChipConfig`
**Recommended location:** `Assets/Data/SoulChip/SoulChipConfig.asset`

Place a GameObject with `SoulChipConfigAuthoring` in your global config subscene.

### 3.1 Degradation Tier Thresholds

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Tier1TransferThreshold** | Transfer count at which Tier 1 begins | 3 | 2-5 |
| **Tier2TransferThreshold** | Transfer count at which Tier 2 begins | 4 | 3-6 |
| **Tier3TransferThreshold** | Transfer count at which Tier 3 begins | 5 | 4-8 |

### 3.2 Degradation Penalties Per Tier

| Field | Tier 0 | Tier 1 | Tier 2 | Tier 3 |
|-------|--------|--------|--------|--------|
| **StatMultiplier** | 1.0 | 0.95 | 0.90 | 0.85 |
| **InputDelay** | false | false | true | true |
| **MemoryGlitches** | false | false | false | true |
| **CompendiumPagesLost** | 0 | 0 | 0 | TransferCount - 4 |

### 3.3 Ejection Config

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **EjectionDelay** | Seconds after death before chip entity spawns | 1.0 | 0.5-3.0 |
| **ChipWorldLifetime** | Seconds ejected chip persists before auto-recovery fallback | 300 | 60-600 |
| **DroneRecoveryDelay** | Seconds for drone insurance recovery animation | 4.0 | 2-8 |

**Tuning tip:** Keep Tier1TransferThreshold at 3 to give new players two "free" deaths. If playtesting shows players hitting Tier 3 too often, increase thresholds. The goal is that most players stay at Tier 0-1 and only careless play reaches Tier 3.

---

## 4. Degradation Visual Effects

### 4.1 Screen Distortion (Tier 3 -- MemoryGlitches)

**Create:** Post-processing volume profile override
**Recommended location:** `Assets/Data/SoulChip/SoulChipGlitchProfile.asset`

| Effect | Description | Intensity |
|--------|-------------|-----------|
| **Chromatic Aberration** | Color fringing at screen edges | 0.3 |
| **Film Grain** | Digital noise overlay | 0.15 |
| **Screen Distortion** | Subtle wave/warp effect (custom shader) | 0.1 |
| **VHS Scanlines** | Horizontal scanlines flickering (custom shader) | 0.05 |

The `SoulChipDegradationVisualBridge` (managed MonoBehaviour) enables this profile when `SoulChipDegradation.MemoryGlitches == true`.

### 4.2 Audio Warping (Tier 3 -- MemoryGlitches)

| Effect | Description |
|--------|-------------|
| **Pitch Shift** | Random micro-pitch shifts on SFX (0.95-1.05 range) |
| **Echo** | Subtle echo/reverb added to dialogue audio |
| **Static Burst** | Random brief static bursts every 10-30 seconds |

### 4.3 Input Delay (Tier 2+ -- InputDelay)

| Parameter | Value | Notes |
|-----------|-------|-------|
| **Delay Duration** | 0.1s (100ms) | Applied to movement and combat inputs |
| **Visual Indicator** | Subtle input lag icon in HUD corner | Communicates the penalty to player |

**Tuning tip:** Input delay of 100ms is noticeable but not rage-inducing. Do not exceed 150ms. The delay should feel like "sluggish augments," not broken controls.

---

## 5. Soul Chip Status UI

**Create:** UI widget in `Assets/Prefabs/UI/SoulChip/SoulChipStatusWidget.prefab`
**Placement:** Player HUD, near health bar

| Element | Description |
|---------|-------------|
| **Chip Icon** | Small soul chip icon with tier color glow |
| **Transfer Counter** | Number showing current TransferCount |
| **Degradation Bar** | Color-coded bar: green (T0), yellow (T1), orange (T2), red (T3) |
| **Penalty Icons** | Small icons for active penalties (input delay, glitch, page loss) |

The widget reads `SoulChipState` and `SoulChipDegradation` from the player entity via a managed bridge.

---

## 6. Drone Insurance Setup

**Create:** Consumable item via Items/ framework
**Recommended location:** `Assets/Data/Items/Consumables/DroneInsurance.asset`

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **ItemType** | Consumable | Consumable | Fixed |
| **MaxCharges** | Charges per expedition | 2 | 1-5 |
| **DroneRecoveryDelay** | Matches SoulChipConfig | 4.0 | 2-8 |
| **TargetTier** | Revival body tier selected by drone | Cheap | Cheap only |

Drone insurance is purchased before an expedition or found as rare loot. The `DroneRecoverySystem` (EPIC 2.3) reads `DroneInsuranceState` on the player entity.

### 6.1 DroneInsuranceState on Player

Add to player entity baker:

| Component | Size | Ghost Config |
|-----------|------|-------------|
| `DroneInsuranceState` | 16 bytes | AllPredicted |

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Subscene | `SoulChipAuthoring` on player root | Bakes SoulChipState + SoulChipDegradation |
| Global Config Subscene | `SoulChipConfigAuthoring` on config GO | Degradation thresholds and ejection config |
| Ghost Prefab Registry | `EjectedSoulChip.prefab` | Register for network ghost spawning |
| UI Canvas | `SoulChipStatusWidget` on HUD | Transfer count + degradation indicator |
| Post-Processing | `SoulChipGlitchProfile` volume | Enabled when MemoryGlitches active |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| SoulId left at 0 after spawn | Chip ejection fails, recovery system cannot match chip to player | Verify `SoulChipBootstrapSystem` runs; check console for init log |
| Adding SoulChipState to child entity instead of player root | `SoulChipEjectionSystem` cannot find component on death | Must be on the root player entity (same entity with DeathState) |
| EjectedSoulChip prefab missing InteractableAuthoring | Chip appears in world but cannot be picked up | Add InteractableAuthoring with type=SoulChipPickup |
| StatMultiplier fed as additive instead of multiplicative | Tier 1 gives 95% bonus instead of 5% penalty | EquippedStatsSystem modifier must be multiplicative (value = 0.95) |
| InputDelay applied to UI navigation, not just gameplay | Menu navigation becomes sluggish at Tier 2 | InputDelay should only affect in-game movement and combat inputs |
| Forgetting to reimport subscene after adding SoulChipAuthoring | SoulChipState not baked, systems cannot find it | Right-click subscene, Reimport |
| MemoryGlitches post-processing always on in editor | Distracting during development | Glitch profile should only activate via bridge when MemoryGlitches==true |
| DroneInsuranceState.ChargesRemaining not reset between expeditions | Drone charges carry over from previous run | Reset in expedition init system |

---

## Verification

1. **Initialization** -- Enter play mode. Console should show:
   ```
   [SoulChipBootstrapSystem] Initialized SoulChip for player E:XX (SoulId=12345, TransferCount=0, Tier=0)
   ```

2. **Entity Debugger** -- Find player entity. Verify `SoulChipState` exists with SoulId > 0, TransferCount=0, DegradationTier=0, IsEmbodied=true.

3. **Death Ejection** -- Kill the player. Console:
   ```
   [SoulChipEjectionSystem] Player E:XX died. Ejecting SoulChip (SoulId=12345) at position (X, Y, Z)
   ```

4. **Ejected Chip Visible** -- After death, glowing chip entity should appear at death location. Verify it is interactable.

5. **Transfer Increment** -- After revival (EPIC 2.3), check `SoulChipState.TransferCount` has incremented by 1.

6. **Degradation Tier 0** -- Transfers 1-2: verify StatMultiplier=1.0, no penalties. Soul Chip UI shows green.

7. **Degradation Tier 1** -- Transfer 3: verify StatMultiplier=0.95, UI shows yellow. Check EquippedStatsSystem receives the 5% reduction.

8. **Degradation Tier 2** -- Transfer 4: verify StatMultiplier=0.90, InputDelay=true. Test input delay is perceptible (~100ms).

9. **Degradation Tier 3** -- Transfer 5+: verify StatMultiplier=0.85, MemoryGlitches=true, CompendiumPagesLost > 0. Screen distortion visible, audio warped.

10. **Debug Overlay** -- Toggle `Front/SoulChip/ShowChipState` in debug menu. HUD overlay should show SoulId, TransferCount, DegradationTier, IsEmbodied with tier color band.
