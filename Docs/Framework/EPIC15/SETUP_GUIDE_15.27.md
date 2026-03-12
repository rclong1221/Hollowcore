# EPIC 15.27 Setup Guide: Dynamic Audio Ecosystem

**Status:** Implemented (Phases 1-8)
**Last Updated:** February 14, 2026
**Requires:** Existing `AudioManager` in scene (from EPIC 5.1). Unity AudioMixer with bus groups. Combat systems (EPIC 15.22+) for combat ducking. Player vitals components for heartbeat/breathing audio.

This guide covers the Unity Editor setup for the **Dynamic Audio Ecosystem** — the comprehensive audio pipeline including bus routing, source pooling, entity-linked audio, occlusion, reverb zones, priority/LOD, combat audio polish, and accessibility features.

---

## Overview

The Dynamic Audio Ecosystem upgrades the existing audio pipeline with:

- **Bus Architecture** — 6 audio buses (Combat, Ambient, Music, Dialogue, UI, Footstep) with per-bus routing and sidechain ducking
- **Source Pool** — Priority-aware pool of 32 AudioSources with eviction scoring and per-bus allocation
- **Entity-Linked Audio** — ECS AudioEmitter component for sounds that track entity positions
- **Occlusion & Obstruction** — Frame-spread raycasts driving low-pass filter and volume attenuation
- **Reverb Zones** — Stack-based trigger volumes that crossfade AudioMixer snapshots
- **Priority & LOD** — Voice budget enforcement with distance-based quality tiers
- **Combat Audio Polish** — Tinnitus feedback, weapon reverb tails, music ducking during combat
- **Accessibility** — Sound radar, directional subtitles, visual sound indicators

---

## Audio Workstation

The Audio Workstation has 10 module tabs for managing the audio ecosystem.

**Menu: DIG > Audio Workstation**

| Tab | Purpose |
|-----|---------|
| Sound Banks | Sound bank management |
| Impact Surfaces | Impact surface material configuration |
| Randomization | Audio clip randomization settings |
| Distance Atten | Distance attenuation curves |
| Batch Assign | Bulk audio assignment |
| Audio Preview | Clip preview and testing |
| **Bus Monitor** | Real-time per-bus VU meters, pool status, sidechain indicators |
| **Occlusion Debug** | Raycast budget display, per-source occlusion status, scene view legend |
| **Reverb Zones** | Active zone state, scene zone list with overlap warnings |
| **Telemetry** | Event counts, voice management, pool usage, error tracking |

The last four tabs (bold) are new in EPIC 15.27. The Bus Monitor and Telemetry tabs require Play Mode to display real-time data.

---

## Step 1: Create ScriptableObject Assets

Create the following configuration assets. All use Unity's Create menu.

### 1.1 Audio Bus Config

**Create:** `Assets > Create > DIG/Audio/Bus Config`
**Recommended path:** `Assets/Settings/Audio/AudioBusConfig.asset`

Defines per-bus defaults and sidechain ducking rules.

#### Per-Bus Settings

Each bus has the following configurable fields:

| Field | Description | Default |
|-------|-------------|---------|
| **Mixer Group** | AudioMixerGroup to route this bus to | None (wire manually) |
| **Default Volume** | Base volume (0-1) | Varies by bus |
| **Default Spatial Blend** | 2D (0) to 3D (1) | Varies by bus |
| **Default Max Distance** | Max audible distance in meters | Varies by bus |
| **Rolloff Mode** | 0=Logarithmic, 1=Linear, 2=Custom | 0 |

**Bus defaults:**

| Bus | Volume | Spatial Blend | Max Distance | Use Case |
|-----|--------|---------------|-------------|----------|
| **Combat** | 1.0 | 1.0 | 100 | Weapon fire, impacts, abilities |
| **Ambient** | 0.8 | 0.6 | 80 | Environment sounds |
| **Music** | 0.7 | 0.0 | 0 | Non-spatial music |
| **Dialogue** | 1.0 | 0.8 | 60 | NPC speech, barks |
| **UI** | 1.0 | 0.0 | 0 | Non-spatial UI sounds |
| **Footstep** | 0.9 | 1.0 | 60 | Player/NPC footsteps |

#### Sidechain Ducking Rules

Define automatic volume ducking relationships between buses.

| Field | Description | Default |
|-------|-------------|---------|
| **Source Bus** | Bus that triggers ducking when active | — |
| **Target Bus** | Bus that gets ducked | — |
| **Duck Amount (dB)** | Volume reduction (-20 to 0 dB) | -6 to -9 |
| **Attack Time** | How fast ducking engages (0.01-1s) | 0.2-0.3 |
| **Release Time** | How fast ducking releases (0.1-5s) | 1.0-1.5 |

**Default rules:**

| Rule | Effect |
|------|--------|
| Combat → Ambient | Ambient ducks -6dB when combat sounds play |
| Dialogue → Music | Music ducks -9dB when dialogue plays |

### 1.2 Audio Clip Bank

**Create:** `Assets > Create > DIG/Audio/Clip Bank`
**Recommended path:** `Assets/Settings/Audio/AudioClipBank.asset`

Registers audio clips with integer IDs for the ECS PlayAudioRequest system.

| Field | Description |
|-------|-------------|
| **Entries** | Array of clip entries |

Each entry:

| Field | Description |
|-------|-------------|
| **Id** | Unique integer ID (used by PlayAudioRequest.ClipId in ECS) |
| **Name** | Human-readable name for editor display |
| **Category** | Category enum: Combat, Ambient, Creature, Ability, UI, Movement, Environment |
| **Clips** | Array of AudioClip variations (one selected randomly on play) |

### 1.3 Occlusion Profile

**Create:** `Assets > Create > DIG/Audio/Occlusion Profile`
**Recommended path:** `Assets/Settings/Audio/OcclusionProfile.asset`

Controls audio occlusion raycast behavior and filter parameters.

#### Raycast Settings

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Spread Frames** | Distribute raycasts across N frames (higher = cheaper) | 6 | 1-12 |
| **Occlusion Layers** | LayerMask for occlusion raycasts | Everything | — |
| **Max Occlusion Distance** | Max raycast distance (meters) | 80 | — |
| **Min Priority For Occlusion** | Only raycast sources above this priority | 20 | 0-255 |

#### Transition

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Transition Speed** | How fast occlusion changes blend (seconds) | 0.15 | 0.05-1.0 |

#### Occlusion Factors

| Field | Description | Default |
|-------|-------------|---------|
| **Clear Factor** | Attenuation when no hits (fully clear) | 1.0 |
| **Partial Factor** | Attenuation when 1 hit (partially occluded) | 0.5 |
| **Heavy Factor** | Attenuation when 2+ hits (heavily occluded) | 0.15 |

#### Audio Application

| Field | Description | Default |
|-------|-------------|---------|
| **Occluded Cutoff** | Low-pass cutoff frequency when heavily occluded (Hz) | 500 |
| **Clear Cutoff** | Low-pass cutoff frequency when clear (Hz) | 22000 |
| **Occluded Volume** | Volume when heavily occluded (0-1) | 0.15 |
| **Clear Volume** | Volume when clear (0-1) | 1.0 |

**Tuning tip:** Start with SpreadFrames=6. Lower to 3-4 if occlusion transitions feel laggy. Raise to 10-12 on console for performance.

### 1.4 Audio LOD Config

**Create:** `Assets > Create > DIG/Audio/LOD Config`
**Recommended path:** `Assets/Settings/Audio/AudioLODConfig.asset`

Controls voice budget and distance-based quality tiers.

#### LOD Distance Thresholds

| Field | Description | Default |
|-------|-------------|---------|
| **Full Quality Distance** | Sources closer than this get full processing | 20m |
| **Reduced Quality Distance** | Sources closer than this get reduced quality | 40m |
| **Minimal Quality Distance** | Sources closer than this get minimal quality | 60m |

Sources beyond Minimal distance are **culled** (stopped entirely).

#### LOD Tier Behavior

| Tier | Distance Range | Behavior |
|------|---------------|----------|
| **Full** | 0 – 20m | Full spatial processing, stereo, occlusion |
| **Reduced** | 20 – 40m | Optionally downmixed to mono, reduced spread |
| **Minimal** | 40 – 60m | Minimal processing, low reverbZoneMix |
| **Culled** | 60m+ | Source stopped, voice freed |

#### Voice Budget

| Field | Description | Default |
|-------|-------------|---------|
| **PC Voice Budget** | Maximum simultaneous voices on PC | 48 |
| **Console Voice Budget** | Maximum simultaneous voices on console | 32 |

When the voice count exceeds the budget, lowest-scoring voices are culled.

#### Scoring & Exemptions

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Distance Falloff** | How much distance reduces priority score | 0.1 | 0.01-1.0 |
| **Exempt Priority Threshold** | Sources at or above this priority are never culled | 200 | 0-255 |
| **Paradigm Distance Multiplier** | Scales all LOD distances per paradigm | 1.0 | 0.5-3.0 |
| **Downmix At Reduced** | Downmix stereo to mono at Reduced tier | true | — |

### 1.5 Audio Accessibility Config

**Create:** `Assets > Create > DIG/Audio/Accessibility Config`
**Recommended path:** `Assets/Settings/Audio/AudioAccessibilityConfig.asset`

Configures all audio accessibility features. Settings are persisted via PlayerPrefs at runtime.

#### Sound Radar

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Enable Sound Radar** | Show directional sound indicator overlay | false | — |
| **Radar Size** | Radar size multiplier | 1.0 | 0.5-2.0 |
| **Radar Min Priority** | Minimum priority for a sound to appear on radar | 40 | 0-200 |

#### Directional Subtitles

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Enable Directional Subtitles** | Show directional arrows with speaker subtitles | false | — |
| **Subtitle Font Scale** | Font size multiplier | 1.0 | 1.0-2.0 |

#### Visual Sound Indicators

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Enable Visual Sound Indicators** | Flash screen edges for off-screen sounds | false | — |
| **Visual Indicator Intensity** | Flash intensity | 0.7 | 0-1.0 |

#### Tinnitus

| Field | Description | Default |
|-------|-------------|---------|
| **Disable Tinnitus Audio** | Disable the 12kHz tinnitus sine tone (keep visual) | false |

---

## Step 2: AudioMixer Setup

The audio bus system requires an AudioMixer with specific groups and exposed parameters.

### 2.1 Create Mixer Groups

In your master AudioMixer, create these groups (if they don't exist):

```
Master
 ├── Combat
 ├── Ambient
 ├── Music
 ├── Dialogue
 ├── UI
 └── Footstep
```

### 2.2 Expose Parameters

Right-click each parameter in the AudioMixer to "Expose to script". The combat ducking system controls these:

| Exposed Parameter | Used By | Purpose |
|-------------------|---------|---------|
| **MusicVolume** | CombatMusicDuckSystem | Ducks music -3dB during combat |
| **AmbientVolume** | CombatMusicDuckSystem | Ducks ambient -4dB during combat |
| **MusicCutoff** | CombatMusicDuckSystem | Low-pass music to 8kHz during combat |
| **MasterVolume** | AudioEnvironmentSystem | Ducks master during tinnitus recovery |

### 2.3 Create Reverb Snapshots

Create one AudioMixerSnapshot per reverb preset. These are crossfaded by the reverb zone system.

| Snapshot Name | Preset | Use Case |
|---------------|--------|----------|
| **OpenField** | Open Field | Default outdoor fallback |
| **Forest** | Forest | Dense vegetation areas |
| **SmallRoom** | Small Room | Interiors, buildings |
| **LargeHall** | Large Hall | Cathedrals, hangars |
| **Tunnel** | Tunnel | Narrow corridors, pipes |
| **Cave** | Cave | Underground, caves |
| **Underwater** | Underwater | Submerged areas |
| **ShipInterior** | Ship Interior | Inside vehicles/ships |
| **ShipExterior** | Ship Exterior | On ship decks |

Each snapshot should configure the appropriate reverb send levels, wet/dry mix, and EQ for its environment type.

---

## Step 3: Scene Setup

### 3.1 AudioSourcePool

Add the `AudioSourcePool` MonoBehaviour to your AudioManager GameObject (or a dedicated AudioPool GameObject).

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Pool Size** | Maximum pooled AudioSources | 32 | 16-64 |
| **Bus Config** | Reference to AudioBusConfig asset | None (wire manually) |

The pool creates child GameObjects with AudioSource + AudioLowPassFilter + AudioHighPassFilter at startup. It replaces the old inline 8-source pool in AudioManager.

### 3.2 AudioClipBankHolder

Add `AudioClipBankHolder` to the same GameObject (or any persistent object).

| Field | Description |
|-------|-------------|
| **Bank** | Reference to AudioClipBank asset |

This provides the clip bank reference to the ECS `AudioSourcePoolSystem`.

### 3.3 OcclusionProfileHolder

Add `OcclusionProfileHolder` to the same GameObject.

| Field | Description |
|-------|-------------|
| **Profile** | Reference to OcclusionProfile asset |

### 3.4 AudioLODConfigHolder

Add `AudioLODConfigHolder` to the same GameObject.

| Field | Description |
|-------|-------------|
| **Config** | Reference to AudioLODConfig asset |

### 3.5 AudioAccessibilityConfigHolder

Add `AudioAccessibilityConfigHolder` to the same GameObject.

| Field | Description |
|-------|-------------|
| **Config** | Reference to AudioAccessibilityConfig asset |

### 3.6 AudioReverbZoneManager

Add `AudioReverbZoneManager` to the same GameObject (or a dedicated ReverbZones holder).

| Field | Description |
|-------|-------------|
| **Mixer** | Reference to your master AudioMixer |
| **Open Field Snapshot** | Fallback outdoor snapshot |
| **Forest Snapshot** | Forest reverb snapshot |
| **Small Room Snapshot** | Small room reverb snapshot |
| **Large Hall Snapshot** | Large hall reverb snapshot |
| **Tunnel Snapshot** | Tunnel reverb snapshot |
| **Cave Snapshot** | Cave reverb snapshot |
| **Underwater Snapshot** | Underwater reverb snapshot |
| **Ship Interior Snapshot** | Ship interior reverb snapshot |
| **Ship Exterior Snapshot** | Ship exterior reverb snapshot |

Wire each snapshot field to the corresponding AudioMixerSnapshot from Step 2.3.

### 3.7 SoundRadarRenderer (Optional)

Add `SoundRadarRenderer` to any persistent GameObject (or a UI holder).

| Field | Description | Default |
|-------|-------------|---------|
| **Screen Position** | Radar center offset (screen-space fraction 0-1) | (0.12, 0.15) |
| **Base Radius** | Radar circle radius in pixels | 60 |
| **Pip Size** | Individual pip size in pixels | 8 |
| **Danger Color** | Color for danger pips | Red (1, 0.2, 0.2, 0.9) |
| **Friendly Color** | Color for friendly pips | Blue (0.3, 0.5, 1, 0.9) |
| **Neutral Color** | Color for neutral pips | Yellow (1, 0.9, 0.3, 0.9) |
| **Ambient Color** | Color for ambient pips | White (1, 1, 1, 0.5) |
| **Background Color** | Radar background | Black (0, 0, 0, 0.3) |

Only renders when `AudioAccessibilityConfig.EnableSoundRadar = true`.

### 3.8 DirectionalSubtitleManager (Optional)

Add `DirectionalSubtitleManager` to any persistent GameObject.

| Field | Description | Default |
|-------|-------------|---------|
| **Subtitle Duration** | How long each subtitle displays (seconds) | 4.0 |
| **Fade Out Time** | Fade-out duration at end of life (seconds) | 0.5 |
| **Max Visible Subtitles** | Maximum simultaneous subtitles on screen | 4 |
| **Y Spacing** | Vertical spacing between stacked subtitles (pixels) | 30 |
| **Bottom Margin** | Distance from screen bottom (pixels) | 150 |

Only renders when `AudioAccessibilityConfig.EnableDirectionalSubtitles = true`.

---

## Step 4: Scene Hierarchy Example

```
Scene Root
 ├── Player Prefab                    (existing)
 ├── AudioManager                     (existing EPIC 5.1)
 │
 ├── AudioFramework                   ← NEW
 │   ├── AudioSourcePool              (wire BusConfig)
 │   ├── AudioClipBankHolder          (wire ClipBank)
 │   ├── OcclusionProfileHolder       (wire OcclusionProfile)
 │   ├── AudioLODConfigHolder         (wire LODConfig)
 │   ├── AudioAccessibilityConfigHolder (wire AccessibilityConfig)
 │   ├── AudioReverbZoneManager       (wire Mixer + 9 snapshots)
 │   ├── SoundRadarRenderer           (optional — accessibility)
 │   └── DirectionalSubtitleManager   (optional — accessibility)
 │
 └── Subscenes
     └── Environment
         ├── ReverbZone_Cave           (ReverbZoneAuthoring + BoxCollider, trigger)
         ├── ReverbZone_Interior       (ReverbZoneAuthoring + BoxCollider, trigger)
         └── ...
```

---

## Step 5: Reverb Zone Setup

Place reverb zones as trigger volumes in your environment scenes/subscenes.

### 5.1 Create a Reverb Zone

1. Create an empty GameObject where you want the zone
2. Add a **Collider** (Box, Sphere, or Mesh) and set **Is Trigger = true**
3. Add the `ReverbZoneAuthoring` component

### 5.2 Configure the Zone

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Zone Name** | Display name (for debug/editor) | "Unnamed Zone" | — |
| **Preset** | Which reverb preset to apply | Open Field | Enum |
| **Transition Duration** | How long the crossfade takes (seconds) | 1.5 | 0.1-5.0 |
| **Priority** | Higher-priority zones override lower when overlapping | 0 | — |
| **Is Interior** | Marks this zone as an interior (affects IndoorFactor) | false | — |
| **Custom Snapshot** | Custom AudioMixerSnapshot (only used when Preset = Custom) | None | — |

### 5.3 Available Presets

| Preset | Typical Use |
|--------|-------------|
| **Open Field** | Default outdoor areas |
| **Forest** | Dense tree areas |
| **Small Room** | Buildings, rooms, small interiors |
| **Large Hall** | Cathedrals, warehouses, hangars |
| **Tunnel** | Corridors, pipes, narrow passages |
| **Cave** | Underground, caverns |
| **Underwater** | Submerged areas |
| **Ship Interior** | Inside vehicles or ships |
| **Ship Exterior** | On ship decks, vehicle exteriors |
| **Custom** | Uses the Custom Snapshot field |

### 5.4 Overlapping Zones

When the player enters overlapping zones, the **highest Priority** zone wins. The manager maintains a stack — exiting the top zone crossfades back to the next zone in the stack. If no zones are active, it falls back to the Open Field snapshot.

**Gizmo colors:**
- Selected zone: cyan wireframe
- Interior zones: orange wireframe
- Exterior zones: green wireframe

---

## Step 6: Entity Audio Emitter Setup (Subscene Prefabs)

For entities (enemies, projectiles, interactables) that emit sounds tracked by ECS:

### 6.1 Add AudioEmitterAuthoring

On the entity's root GameObject in the subscene prefab, add `AudioEmitterAuthoring`.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Bus** | Which audio bus to route to | Combat | Enum |
| **Priority** | Voice priority (higher = harder to cull) | 100 | 0-255 |
| **Spatial Blend** | 2D (0) to fully 3D (1) | 1.0 | 0-1 |
| **Max Distance** | Max audible distance in meters | 50 | — |
| **Rolloff Mode** | 0=Logarithmic, 1=Linear | 0 | 0-1 |
| **Track Position** | Update AudioSource position every frame | true | — |
| **Use Occlusion** | Perform occlusion raycasts for this emitter | true | — |

### 6.2 Priority Guidelines

| Priority | Use Case |
|----------|----------|
| 0-30 | Ambient environment sounds |
| 30-60 | Footsteps, minor impacts |
| 60-100 | Weapon fire, abilities |
| 100-150 | Enemy attacks, important combat |
| 150-200 | Dialogue, critical gameplay feedback |
| 200-255 | Never culled (boss abilities, player feedback) |

### 6.3 Bus Routing Guidelines

| Bus | Use For |
|-----|---------|
| **Combat** | Weapon fire, impacts, abilities, explosions |
| **Ambient** | Environment loops, wind, water, wildlife |
| **Music** | Background music, combat music stingers |
| **Dialogue** | NPC speech, barks, player callouts |
| **UI** | Menu clicks, notifications, HUD feedback |
| **Footstep** | Player/NPC footsteps, landing sounds |

---

## Step 7: Combat Audio Features

These systems work automatically with no additional setup beyond what's described above. They rely on existing combat components.

### 7.1 Tinnitus Feedback

When the player takes an explosion hit dealing 50+ damage, the system:
1. Plays a 12kHz sine tone (simulated tinnitus)
2. Ducks all other audio
3. Gradually recovers over the deafen duration

Disable the audio component (keep visual effects) via `AudioAccessibilityConfig.DisableTinnitusAudio`.

### 7.2 Weapon Reverb Tails

When the player fires a weapon, the system plays an indoor or outdoor reverb tail clip based on the current `IndoorFactor` (set by reverb zones).

**To configure tail clips:** Set the static AudioClip references on `WeaponAudioTailSystem` via a bootstrap MonoBehaviour or initialization script:
- `WeaponAudioTailSystem.IndoorTailClip` — short reverb tail for interiors
- `WeaponAudioTailSystem.OutdoorTailClip` — longer, more diffuse tail for outdoors

### 7.3 Combat Music Ducking

When any local player entity has `CombatState.IsInCombat = true`, the system:
- Ducks Music volume by -3dB
- Ducks Ambient volume by -4dB
- Applies an 8kHz low-pass on Music
- Uses a 5-second grace period after combat ends before releasing
- Crossfades over 2 seconds

No configuration needed — uses the exposed AudioMixer parameters from Step 2.2.

---

## Step 8: Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Workstation | Open DIG > Audio Workstation | Window opens with 10 tabs |
| 3 | Bus Monitor | Enter Play Mode, open Bus Monitor tab | Per-bus VU meters, pool status visible |
| 4 | Telemetry | Enter Play Mode, open Telemetry tab | Event counts, voice counts, pool bar visible |
| 5 | Pool active | Enter Play Mode, check AudioSourcePool Inspector | ActiveCount shows allocated sources |
| 6 | Occlusion | Stand behind a wall from an active sound | Sound muffled (low-pass + volume reduction) |
| 7 | Occlusion Debug | Open Occlusion Debug tab in Play Mode | Per-source occlusion status displayed |
| 8 | Reverb zone | Walk into a configured reverb zone | AudioMixer crossfades to zone's snapshot |
| 9 | Reverb Zones tab | Open Reverb Zones tab in Play Mode | Current zone name, transition progress shown |
| 10 | Voice budget | Spawn many sound-emitting entities | Voices capped at budget, lowest-priority culled |
| 11 | LOD tiers | Walk away from a sound source | Quality reduces at each tier threshold, culled beyond Minimal |
| 12 | Tinnitus | Take 50+ explosion damage | 12kHz tone plays, audio ducks, recovers gradually |
| 13 | Music ducking | Enter combat (attack an enemy) | Music and ambient duck, release 5s after combat ends |
| 14 | Sound radar | Enable in AudioAccessibilityConfig, enter Play Mode | Radar overlay shows directional pips for active sounds |
| 15 | Subtitles | Enable directional subtitles, trigger NPC dialogue | Subtitle with direction arrows and distance tag appears |
| 16 | No console errors | Play for 60 seconds | No exceptions or warnings from audio systems |

---

## Step 9: Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| No pooled sources active | AudioSourcePool not in scene | Add AudioSourcePool MonoBehaviour, wire BusConfig |
| Sources play but no bus routing | BusConfig.MixerGroup fields not wired | Open BusConfig asset, assign AudioMixerGroups to each bus |
| Entity sounds don't follow movement | AudioEmitterAuthoring.TrackPosition = false | Enable TrackPosition on the entity's AudioEmitterAuthoring |
| No occlusion effect | OcclusionProfileHolder missing or Profile not wired | Add OcclusionProfileHolder, wire OcclusionProfile asset |
| Occlusion is choppy | SpreadFrames too high | Lower OcclusionProfile.SpreadFrames (try 3-4) |
| Occlusion too aggressive | OccludedVolume/OccludedCutoff too low | Raise OccludedVolume (try 0.3) and OccludedCutoff (try 1000) |
| No reverb zone transitions | AudioReverbZoneManager missing or snapshots not wired | Add manager, wire Mixer + all 9 snapshot fields |
| Reverb zone not triggering | Collider not set to Is Trigger | Enable Is Trigger on the zone's Collider component |
| Reverb zone wrong order | Priority not set on overlapping zones | Set higher Priority on the zone that should take precedence |
| Too many voices / distortion | Voice budget too high or pool too large | Lower AudioLODConfig voice budgets or AudioSourcePool pool size |
| Important sounds culled | Priority too low on the emitter | Raise AudioEmitterAuthoring.Priority above ExemptPriorityThreshold (200) |
| No tinnitus effect | DisableTinnitusAudio is checked | Uncheck AudioAccessibilityConfig.DisableTinnitusAudio |
| No combat music ducking | AudioMixer parameters not exposed | Expose MusicVolume, AmbientVolume, MusicCutoff in AudioMixer |
| Sound radar not showing | EnableSoundRadar not checked | Enable in AudioAccessibilityConfig asset |
| Radar shows no pips | RadarMinPriority too high | Lower AudioAccessibilityConfig.RadarMinPriority |
| No subtitles | EnableDirectionalSubtitles not checked | Enable in AudioAccessibilityConfig asset |
| Bus Monitor shows nothing | Not in Play Mode | Bus Monitor requires Play Mode to display real-time data |
| Telemetry counters stuck at 0 | Not in Play Mode | Telemetry requires Play Mode |
| Clip Bank lookup fails | ClipId doesn't match any entry | Verify PlayAudioRequest.ClipId matches an entry in the AudioClipBank |

---

## Step 10: Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Base audio manager, footsteps | EPIC 5.1 (AudioManager) |
| Combat state, damage events | SETUP_GUIDE_15.22, SETUP_GUIDE_15.29 |
| Input paradigm framework | SETUP_GUIDE_15.20 |
| Smart HUD & Widget Ecosystem | SETUP_GUIDE_15.26 |
| Procedural motion, camera spring | SETUP_GUIDE_15.25 |
| Physics optimization | SETUP_GUIDE_15.23 |
| **Dynamic Audio Ecosystem** | **This guide (15.27)** |

---

## Step 11: File Reference

### Config (ScriptableObjects)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Audio/Config/AudioBusConfig.cs` | Per-bus defaults and sidechain ducking rules |
| `Assets/Scripts/Audio/Config/AudioClipBank.cs` | Clip registry with integer IDs for ECS requests |
| `Assets/Scripts/Audio/Config/OcclusionProfile.cs` | Occlusion raycast and audio filter settings |
| `Assets/Scripts/Audio/Config/AudioLODConfig.cs` | Voice budget and LOD distance thresholds |
| `Assets/Scripts/Audio/Accessibility/AudioAccessibilityConfig.cs` | Accessibility feature toggles and settings |
| `Assets/Scripts/Audio/Config/AudioBusType.cs` | Bus enum (Combat, Ambient, Music, Dialogue, UI, Footstep) |

### Runtime (MonoBehaviours — place in scene)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Audio/AudioSourcePool.cs` | Priority-aware AudioSource pool singleton |
| `Assets/Scripts/Audio/Zones/AudioReverbZoneManager.cs` | Stack-based reverb zone crossfade manager |
| `Assets/Scripts/Audio/Zones/ReverbZoneAuthoring.cs` | Reverb zone trigger volume (place on colliders) |
| `Assets/Scripts/Audio/Accessibility/SoundRadarRenderer.cs` | OnGUI radar overlay renderer |
| `Assets/Scripts/Audio/Accessibility/DirectionalSubtitleManager.cs` | OnGUI directional subtitle renderer |

### Runtime (MonoBehaviour Holders — wire SO references)

| File | Component | Wires To |
|------|-----------|----------|
| `Assets/Scripts/Audio/Systems/AudioSourcePoolSystem.cs` | AudioClipBankHolder | AudioClipBank asset |
| `Assets/Scripts/Audio/Systems/AudioOcclusionSystem.cs` | OcclusionProfileHolder | OcclusionProfile asset |
| `Assets/Scripts/Audio/Systems/AudioPrioritySystem.cs` | AudioLODConfigHolder | AudioLODConfig asset |
| `Assets/Scripts/Audio/Accessibility/SoundRadarSystem.cs` | AudioAccessibilityConfigHolder | AudioAccessibilityConfig asset |

### Authoring (add to subscene prefabs)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Audio/Authoring/AudioEmitterAuthoring.cs` | Baker that creates AudioEmitter ECS component on entity |

### ECS Components (auto-managed, no setup required)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Audio/Components/AudioEmitter.cs` | ECS component: bus, priority, spatial settings |
| `Assets/Scripts/Audio/Components/AudioSourceState.cs` | Managed component: tracks AudioSource per entity |
| `Assets/Scripts/Audio/Components/PlayAudioRequest.cs` | Buffer element: queued audio play requests |

### ECS Systems (auto-created, no setup required)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Audio/Systems/AudioSourcePoolSystem.cs` | Consumes PlayAudioRequests, manages entity-linked sources |
| `Assets/Scripts/Audio/Systems/AudioTransformSyncSystem.cs` | Syncs AudioSource position to entity transform |
| `Assets/Scripts/Audio/Systems/AudioOcclusionSystem.cs` | Frame-spread occlusion raycasts + low-pass filter |
| `Assets/Scripts/Audio/Systems/AudioPrioritySystem.cs` | Voice budget enforcement + LOD quality tiers |
| `Assets/Scripts/Audio/Systems/TinnitusFeedbackSystem.cs` | Explosion tinnitus effect |
| `Assets/Scripts/Audio/Systems/WeaponAudioTailSystem.cs` | Indoor/outdoor weapon reverb tails |
| `Assets/Scripts/Audio/Systems/CombatMusicDuckSystem.cs` | Music/ambient ducking during combat |
| `Assets/Scripts/Audio/Accessibility/SoundRadarSystem.cs` | Directional sound radar pip generation |
| `Assets/Scripts/Audio/Systems/AudioEnvironmentSystem.cs` | Vacuum filtering, reverb sync, tinnitus recovery |
| `Assets/Scripts/Audio/Systems/ImpactAudioSystem.cs` | Physics collision impact sounds with distance culling |
| `Assets/Scripts/Audio/Systems/VitalAudioSystem.cs` | Heartbeat and breathing audio feedback |

### Editor

| File | Purpose |
|------|---------|
| `Assets/Editor/AudioWorkstation/AudioWorkstationWindow.cs` | Audio Workstation window (DIG > Audio Workstation) |
| `Assets/Editor/AudioWorkstation/Modules/BusMonitorModule.cs` | Bus Monitor tab — per-bus VU meters |
| `Assets/Editor/AudioWorkstation/Modules/OcclusionDebugModule.cs` | Occlusion Debug tab — raycast visualization |
| `Assets/Editor/AudioWorkstation/Modules/ReverbZoneModule.cs` | Reverb Zones tab — active zone state |
| `Assets/Editor/AudioWorkstation/Modules/TelemetryModule.cs` | Telemetry tab — event counts, pool usage |
