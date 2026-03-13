# EPIC 5.2 Setup Guide: Echo Encounters

**Status:** Planned
**Requires:** EPIC 5.1 (Echo Generation), Framework Quest/, AI/, Combat/

---

## Overview

Echoes are mechanically distinct from their originals — "wrongness, not stat inflation." Each mutation type transforms encounters differently: new enemy variants, altered objectives, layout distortions, faction swaps, or temporal anomalies. Setting up echo encounters involves creating variant prefabs, configuring objective mutations, and wiring the visual/audio wrongness layer per district.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 5.1 | EchoMissionEntry, EchoFlavorSO | Echo data driving encounter configuration |
| Framework AI/ | Enemy prefabs, spawn system | Base enemies that get upgraded/swapped |
| Framework Quest/ | QuestDefinitionSO, objective tracking | Original objectives that get mutated |
| Framework Combat/ | Stat scaling, damage pipeline | Applies DifficultyMultiplier to enemy stats |
| District scenes | Zone entities, environmental objects | Layout distortion targets |

### New Setup Required
1. Create echo-variant enemy prefabs (or runtime echo visual modifier)
2. Configure EchoEncounterDefinitionSO per mutation type
3. Create echo zone visual effects (distortion post-process, wrongness particles)
4. Define EchoObjective quest variants for each base side goal type
5. Set up TemporalAnomaly VFX (rewind particles, time-warp audio)

---

## 1. Echo Enemy Prefab Setup

### Option A: Dedicated Echo Prefabs
Create variant prefabs for echo encounters at `Assets/Prefabs/Enemies/Echo/`.

| Naming Convention | Example |
|-------------------|---------|
| `{EnemyName}_Echo.prefab` | `Scavenger_Grunt_Echo.prefab` |
| `{EnemyName}_Echo_Elite.prefab` | `Scavenger_Grunt_Echo_Elite.prefab` |

Echo prefabs should have:
- `EchoEnemy` component (baked by authoring)
- Visual modifications (echo shader, color shift, distortion particles)
- Same AI behavior tree as original (difficulty comes from stats, not AI changes)

### Option B: Runtime Echo Modifier (Preferred for Scalability)
Instead of separate prefabs, apply echo modifications at runtime:

1. EchoEncounterSpawnSystem adds `EchoEnemy` tag to spawned enemies
2. Echo visual shader applied via `EchoEnemyVisualOverride` from EchoFlavorSO
3. Stats scaled by `DifficultyMultiplier` at spawn time

**Tuning tip:** Option B is recommended unless echo enemies need fundamentally different meshes or animations. It reduces prefab maintenance and allows DifficultyMultiplier to scale smoothly.

### 1.1 EchoEnemy Component
| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `EchoId` | int | Links to parent EchoMissionEntry | — |
| `SourceMutation` | EchoMutationType | Which mutation created this enemy | — |
| `HasDeathReset` | bool | TemporalAnomaly: can reset to full HP once | false |
| `DeathResetUsed` | bool | Whether the death reset has been consumed | false |

---

## 2. EchoEncounterDefinitionSO

**Create:** `Assets > Create > Hollowcore/Echo/Encounter Definition` (custom, if implemented)
**Recommended location:** `Assets/Data/Echo/Encounters/`

One definition per base quest type per mutation type. Configure how the encounter transforms.

### 2.1 Per-Mutation Configuration

#### EnemyUpgrade
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Enemy replacement map | Original prefab to echo variant mapping | — | — |
| Spawn count multiplier | Additional enemies (on top of DifficultyMultiplier) | 1.5 | 1.0-3.0 |
| Aggro radius multiplier | Echo enemies are more aggressive | 1.5 | 1.0-3.0 |
| Echo visual shader | Applied to all echo enemies in this encounter | — | — |

#### MechanicChange
| Field | Description | Default |
|-------|-------------|---------|
| Objective mutation | Original type to echo type mapping | See EchoObjectiveType |
| Timer modification | Add, remove, or change timer duration | — |
| Completion condition | New win condition for the echo variant | — |

#### LayoutDistortion
| Field | Description | Default |
|-------|-------------|---------|
| Blocked paths | Zone connection indices to block | — |
| New paths | New connections to open (shortcuts, hidden routes) | — |
| Hazard additions | Environmental hazard prefabs to spawn | — |
| Trap repositions | Existing traps moved to new positions | — |

#### FactionSwap
| Field | Description | Default |
|-------|-------------|---------|
| Replacement faction | FactionId to use instead of zone's primary | — |
| Cross-contamination feel | "Why are Cathedral enemies in the Necrospire?" | — |

#### TemporalAnomaly
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Death reset count | Times an enemy can reset (MVP: 1) | 1 | 1-3 |
| Post-reset invulnerability | Seconds of invulnerability after reset | 0.5 | 0.0-2.0 |
| Time distortion VFX | Slowdown pockets, rewind particles | — | — |
| Objective loop | Whether objective must be completed twice | false | — |

---

## 3. EchoObjective Types

**File:** `Assets/Scripts/Echo/Components/EchoEncounterComponents.cs`

| Value | Description | Example Mutation |
|-------|-------------|-----------------|
| `Original` (0) | Same objective, harder enemies | Kill quest with elite variants |
| `Escort` (1) | Rescue becomes escort to extraction | Rescue prisoner to echo zone exit |
| `Survive` (2) | Kill becomes survive for duration | Hold position against waves |
| `Reverse` (3) | Objective inverted (destroy to protect, collect to disperse) | Protect the artifact instead of stealing it |
| `Stealth` (4) | Must complete without alerting echo entities | Stealth through echo zone |
| `Purge` (5) | Kill ALL echo entities in zone (no survivors) | Total extermination |

**Tuning tip:** Not every base quest needs every echo objective type. Focus on mutations that create interesting tactical choices. A "kill all enemies" quest becoming "survive for 2 minutes" is more interesting than just "kill harder enemies."

---

## 4. Echo Zone Visual/Audio Setup

### 4.1 Post-Processing
Create or assign post-process profiles per district echo flavor:

| District | Echo Post-Process | Description |
|----------|------------------|-------------|
| Necrospire | `PP_Echo_Necrospire` | Desaturated, grain, chromatic aberration |
| Wetmarket | `PP_Echo_Wetmarket` | Blue tint, blur, underwater distortion |
| Glitch Quarter | `PP_Echo_Glitch` | Screen tearing, color banding, static |
| Chrome Cathedral | `PP_Echo_Cathedral` | Gold bloom, lens flare, vignette |

### 4.2 Audio Layers
Configure per-district echo ambient audio (referenced by key in EchoFlavorSO):

| District | Echo Ambient | Description |
|----------|-------------|-------------|
| Necrospire | `Echo_Ambient_Necro` | Dead voices, data corruption static |
| Wetmarket | `Echo_Ambient_Wet` | Bubbling, distorted sonar |
| Glitch Quarter | `Echo_Ambient_Glitch` | Temporal stuttering, reversed tones |

### 4.3 Enemy Visual Modifier
The `EchoEnemyVisualOverride` string in EchoFlavorSO is passed to the rendering system:
- Shader keyword or material property override
- Applied at spawn time by EchoEncounterSpawnSystem
- Common effects: color shift, distortion outline, emissive pulse

---

## 5. TemporalAnomaly Death Reset

**File:** `Assets/Scripts/Echo/Systems/EchoDeathResetSystem.cs`

Special system for TemporalAnomaly echoes. Requires:

1. Enemy prefabs must support health reset (Health component writable)
2. Rewind VFX prefab at `Assets/Prefabs/VFX/Echo/VFX_Echo_DeathReset.prefab`
3. Time-warp audio clip assigned to echo audio system

### Reset Flow
1. EchoEnemy enters DeathState
2. If `HasDeathReset && !DeathResetUsed`:
   - Set `DeathResetUsed = true`
   - Reset Health to max
   - Clear DeathState (back to Alive)
   - Play rewind VFX
   - Apply 0.5s invulnerability window
3. If `DeathResetUsed`: normal death proceeds

---

## 6. Live Tuning (Runtime)

**File:** `Assets/Scripts/Echo/Components/EchoEncounterRuntimeConfig.cs`

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `EnemyUpgradeSpawnMultiplier` | Extra spawn count for EnemyUpgrade type | 1.5 | 1.0-3.0 |
| `DeathResetInvulnSeconds` | Post-reset invulnerability window | 0.5 | 0.0-2.0 |
| `AllowPrimaryPathBlock` | Can LayoutDistortion block the main path | true | — |
| `EchoAggroRadiusMultiplier` | Echo enemies detect player at larger range | 1.5 | 1.0-3.0 |
| `EchoEnemiesDropNormalLoot` | Echo enemies also drop their normal loot table | false | — |

**Tuning tip:** If playtesters report echoes feel "unfair" rather than "hard but interesting," reduce EnemyUpgradeSpawnMultiplier and increase EchoAggroRadiusMultiplier. The goal is tactical pressure, not overwhelming numbers.

---

## 7. Debug Visualization

**File:** `Assets/Scripts/Echo/Debug/EchoEncounterDebugOverlay.cs`

Visible when inside an echo zone in debug mode:

| Element | Description |
|---------|-------------|
| Zone boundary | Purple translucent area overlay |
| Enemy markers | Mutation-type icon above health bar (red up-arrow, blue gear, orange swap) |
| DeathReset status | "RESET AVAILABLE" / "RESET USED" per TemporalAnomaly enemy |
| Objective tracker | Echo type + progress (e.g., "SURVIVE: 45s remaining") |
| Wrongness intensity | 0-100% meter based on distance to zone center |
| Layout changes | Blocked paths=red X, new paths=green arrow |
| Active modifiers | List of all stat multipliers on echo enemies |

---

## 8. Scene & Subscene Checklist

- [ ] Echo variant enemy prefabs at `Assets/Prefabs/Enemies/Echo/` (if using Option A)
- [ ] Echo visual shader/material ready for runtime application (if using Option B)
- [ ] Post-process profiles per district echo flavor
- [ ] Rewind VFX prefab for TemporalAnomaly death reset
- [ ] EchoEncounterDefinitionSO assets at `Assets/Data/Echo/Encounters/`
- [ ] EchoObjective quest variants configured for each base side goal type
- [ ] Audio clips for echo ambient and temporal effects

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| No enemy replacements in EchoEncounterDefinitionSO | EnemyUpgrade spawns original enemies | Add replacement map entries |
| ObjectiveMutation set to Original | "No actual mutation" warning | Choose a different EchoObjectiveType |
| TemporalAnomaly without DeathReset | Enemies die normally, no temporal feel | Ensure EchoEnemy.HasDeathReset=true for TemporalAnomaly encounters |
| FactionSwap using same faction as district | No noticeable difference | Reference a different FactionId than the zone's primary |
| LayoutDistortion blocking all paths | Player trapped in zone | Set AllowPrimaryPathBlock=false or ensure alternate routes |
| DifficultyMultiplier not applied to spawned enemies | Echo enemies same strength as originals | EchoEncounterSpawnSystem must scale stats on spawn |
| Echo zone VFX missing post-process profile | Zone looks normal, no wrongness feel | Assign EchoPostProcessProfile in EchoFlavorSO |
| Rewind VFX prefab not assigned | Death reset works but no visual feedback | Create and assign VFX_Echo_DeathReset.prefab |

---

## Verification

- [ ] EnemyUpgrade: enemies visually distinct, harder stats, ~1.5x more spawns
- [ ] MechanicChange: objective changes from original (e.g., rescue to escort)
- [ ] LayoutDistortion: zone paths/hazards differ from original visit
- [ ] FactionSwap: wrong faction's enemies appear in zone
- [ ] TemporalAnomaly: enemies reset once after death with rewind VFX
- [ ] Echo zone has visible wrongness on approach (post-process, audio)
- [ ] Echo completion clears zone effects and marks mission done
- [ ] DifficultyMultiplier scales enemy stats correctly
- [ ] EchoAggroRadiusMultiplier makes echo enemies more aggressive
- [ ] All five mutation types produce distinct gameplay experiences
