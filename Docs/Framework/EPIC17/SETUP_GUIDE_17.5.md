# EPIC 17.5: Dynamic Music System — Setup Guide

## Overview

The Dynamic Music System provides layered stem-based music playback driven by combat intensity, zone-based track switching, boss music overrides, and one-shot stingers. It is fully client-side — no ECS ghosts, no server load, zero bytes on the player entity archetype.

**Key design**: All music state lives on dedicated ECS singletons (`MusicState`, `MusicConfig`). The system reads AI `AlertState` to compute combat intensity, which drives 4-layer stem mixing (Base / Percussion / Melody / Intensity). Zone volumes in subscenes trigger track changes via crossfade.

---

## Quick Start

### 1. Create Default Assets

1. **Menu** → **DIG → Music Workstation → Create Default Assets**
2. This creates two ScriptableObjects:
   - `Assets/Resources/MusicConfig.asset`
   - `Assets/Resources/MusicDatabase.asset`
3. Both **must** remain in `Assets/Resources/` (loaded via `Resources.Load`)

### 2. Create Music Config

If not using the auto-create above:

1. **Right-click** in Project → **Create → DIG → Music → Music Config**
2. Name it `MusicConfig`, place in `Assets/Resources/`
3. Configure values:

| Field | Default | Description |
|-------|---------|-------------|
| DefaultTrackId | 0 | Fallback track when no zone overlaps the player |
| CombatFadeSpeed | 2.0 | Intensity smoothing lerp speed |
| ZoneFadeSpeed | 1.5 | Default crossfade duration between zones (seconds) |
| BossOverrideFadeSpeed | 4.0 | Fast crossfade speed for boss music entry |
| StemTransitionSpeed | 3.0 | Per-stem volume lerp speed |
| StingerVolume | 0.8 | Master stinger playback volume (0–1) |
| StingerCooldown | 3.0 | Min seconds between consecutive stingers |
| MaxCombatIntensityRange | 40.0 | Max distance (meters) to read AI AlertState |
| IntensityWeightCombat | 1.0 | Weight for COMBAT-level enemies |
| IntensityWeightSearching | 0.6 | Weight for SEARCHING-level enemies |
| IntensityWeightSuspicious | 0.3 | Weight for SUSPICIOUS-level enemies |
| IntensityWeightCurious | 0.1 | Weight for CURIOUS-level enemies |
| MaxIntensityContributors | 8 | Cap on AI entities counted per frame |

### 3. Create Music Tracks

1. **Right-click** → **Create → DIG → Music → Music Track**
2. Create one asset per unique music piece
3. Recommended folder: `Assets/Resources/Tracks/` (or any project folder)
4. Fill in fields:

| Field | Default | Description |
|-------|---------|-------------|
| TrackId | — | **Unique integer**. Referenced by zones and boss overrides |
| TrackName | — | Display name (e.g., "Forest Exploration") |
| Category | Exploration | One of: Exploration, Combat, Boss, Ambient, Town, Dungeon |
| BPM | 120 | Beats per minute (for future beat-synced transitions) |
| BaseStem | — | **Required**. Always-playing foundation layer AudioClip |
| PercussionStem | — | Rhythmic layer, activates at low intensity |
| MelodyStem | — | Melodic layer, activates at medium intensity |
| IntensityStem | — | Full combat layer, activates at high intensity |
| IntroClip | null | Optional one-shot intro (plays once before looping stems) |
| BaseVolume | 1.0 | Master volume for this track (0–1) |
| CombatIntensityThresholds | (0.2, 0.5, 0.8) | x=Percussion on, y=Melody on, z=Intensity on |
| StemFadeInTime | 0.5 | Stem fade-in duration (seconds) |
| StemFadeOutTime | 1.0 | Stem fade-out duration (seconds) |
| LoopStartSample | 0 | Custom loop start in samples (0 = clip start) |
| LoopEndSample | 0 | Custom loop end in samples (0 = clip end) |

**Stem rules**:
- All stem AudioClips for a track should be the **same length** and **same sample rate**
- Base stem is always playing at full volume
- Percussion activates when `SmoothedIntensity >= threshold.x`
- Melody activates when `SmoothedIntensity >= threshold.y`
- Intensity activates when `SmoothedIntensity >= threshold.z`
- Missing stems are simply silent (no errors)

### 4. Build the Music Database

1. Open your `Assets/Resources/MusicDatabase.asset`
2. Drag all MusicTrackSO assets into the **Tracks** list
3. Set **DefaultTrackId** to a valid track ID (your ambient/exploration track)
4. Set **SilenceTrackId** if you want a dedicated "no music" track (0 = true silence)
5. Add stinger definitions to the **Stingers** list (see section below)

### 5. Place Music Zones in Scenes

1. Create an empty GameObject in your client subscene (e.g., "MusicZone_Forest")
2. Add a **Collider** (BoxCollider recommended), set **Is Trigger = true**
3. Add **PhysicsShapeAuthoring** if using ECS physics (the system reads `PhysicsCollider` AABB)
4. Add component: **DIG → Music → Music Zone**
5. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| TrackId | — | Must match a MusicTrackSO.TrackId in the database |
| Priority | 0 | Higher priority overrides lower (e.g., boss arena=10 > overworld=0) |
| FadeInDuration | 0 | Zone entry crossfade (seconds). 0 = use global ZoneFadeSpeed |
| FadeOutDuration | 0 | Zone exit crossfade (seconds). 0 = use global ZoneFadeSpeed |

**Overlap rules**: When the player is inside multiple zones, the **highest priority** zone wins. Use the Zone Mapper module in Music Workstation to visualize coverage and detect same-priority conflicts.

### 6. Set Up Boss Music Overrides

1. Find the boss encounter entity (has `EncounterProfileAuthoring`)
2. Add component: **DIG → Music → Boss Music Override**
3. Set **BossTrackId** to the boss music track ID from MusicDatabase
4. In the encounter's trigger list, add a trigger with action **PlayMusic** and ActionValue = the TrackId

When the encounter fires `PlayMusic`, the music system forces the boss track with `BossOverrideFadeSpeed`, locks intensity to 1.0, and ignores zone changes until the override is deactivated.

---

## Stingers (One-Shot Audio)

Stingers are short musical cues (level-up fanfare, death sting, rare item drop) that play over the music with optional ducking.

### Defining Stingers

Stingers are defined inline in `MusicDatabaseSO.Stingers` — they are not separate ScriptableObjects.

| Field | Default | Description |
|-------|---------|-------------|
| StingerId | — | Unique integer, referenced by gameplay systems |
| StingerName | — | Display name (e.g., "Level Up Fanfare") |
| Clip | — | One-shot AudioClip |
| DuckMusicDB | -6.0 | Music volume reduction in dB while stinger plays |
| DuckDuration | 0 | How long to duck (0 = auto-match clip length) |
| DefaultPriority | 50 | Higher priority stingers override lower during cooldown |
| Category | — | One of: LevelUp, QuestComplete, Death, RareItem, Achievement, BossIntro, Discovery |

### Stinger Priority Reference

| Category | Constant | Value |
|----------|----------|-------|
| Death | `StingerPriority.Death` | 100 |
| BossIntro | `StingerPriority.BossIntro` | 90 |
| LevelUp | `StingerPriority.LevelUp` | 80 |
| QuestComplete | `StingerPriority.QuestComplete` | 70 |
| Achievement | `StingerPriority.Achievement` | 60 |
| RareItem | `StingerPriority.RareItem` | 50 |
| Discovery | `StingerPriority.Discovery` | 40 |

### Requesting Stingers from Code

Use `MusicStingerAPI` (static helper, namespace `DIG.Music`):

```csharp
// From a Burst/ISystem — use ECB variant
MusicStingerAPI.RequestStinger(ecb, stingerId: 1, priority: StingerPriority.LevelUp);

// From a managed SystemBase — use EntityManager variant
MusicStingerAPI.RequestStinger(EntityManager, stingerId: 3, priority: StingerPriority.Death);
```

Parameters: `stingerId`, `priority` (byte), `allowOverlap` (bool, ignores cooldown), `volumeScale` (float multiplier).

---

## Optional: Now Playing UI Widget

1. Create a UI Panel on your HUD Canvas
2. Add a **Text** child (e.g., TextMeshPro) for the track name
3. Add a **CanvasGroup** to the panel
4. Add the `MusicNowPlayingView` component
5. Wire references:

| Field | Default | Description |
|-------|---------|-------------|
| TrackNameText | — | Text component to display track name |
| CanvasGroup | — | For alpha fade control |
| DisplayDuration | 4.0 | Seconds the widget stays visible |
| FadeInSpeed | 3.0 | Alpha lerp speed on appearance |
| FadeOutSpeed | 1.5 | Alpha lerp speed on disappearance |

The widget automatically shows "Now Playing: {TrackName}" when the zone changes, then fades out.

---

## Debug Overlay

Press **F8** during Play Mode to toggle the music debug overlay. Shows:
- Current track name and ID
- Target track and crossfade progress
- Combat intensity (raw and smoothed)
- Per-stem volumes (Base / Perc / Melody / Intensity)
- Boss override status
- Stinger cooldown remaining

Only available in Editor and when `DEBUG_LOG_AUDIO` is defined.

---

## Editor Tooling: Music Workstation

**Menu**: DIG → Music Workstation

| Module | Description |
|--------|-------------|
| **Track Browser** | Search, filter, and preview all MusicTrackSO assets. Shows stems, BPM, thresholds |
| **Stem Previewer** | Play individual stems with per-stem volume sliders and intensity simulation |
| **Zone Mapper** | Scene View overlay showing zone coverage with color-coded wire cubes. Detects same-priority overlap conflicts |
| **Stinger Tester** | List all stingers with Play/Stop preview buttons. Shows priority and duck levels |
| **Live Debug** | Play Mode only. Real-time view of MusicState singleton: track, intensity, stem volumes, crossfade, telemetry |
| **Intensity Curve** | Edit intensity weights with sliders. Simulator: adjust enemy counts per alert level, see resulting intensity and stem activation |

---

## Architecture

```
CLIENT-ONLY ECS SYSTEMS (no server, no ghosts)
├── MusicBootstrapSystem (InitializationSystemGroup, runs once)
│   └── Loads MusicConfigSO + MusicDatabaseSO from Resources/
│       Creates MusicConfig, MusicState, MusicDatabaseManaged singletons
│
├── MusicZoneSystem (SimulationSystemGroup)
│   └── Player-in-AABB test → sets MusicState.TargetTrackId
│
├── MusicCombatIntensitySystem (SimulationSystemGroup, after Zone)
│   └── Reads AlertState from nearby AI → computes SmoothedIntensity
│       Processes MusicBossOverride transient entities
│
├── MusicTransitionSystem (PresentationSystemGroup, ISystem/Burst)
│   └── Drives CrossfadeProgress 0→1 when TargetTrack ≠ CurrentTrack
│
├── MusicStemMixSystem (PresentationSystemGroup, ISystem/Burst)
│   └── Reads SmoothedIntensity + thresholds → per-stem volume targets
│
├── MusicStingerSystem (PresentationSystemGroup, managed)
│   └── Processes MusicStingerRequest transient entities → highest priority wins
│
└── MusicPlaybackSystem (PresentationSystemGroup, managed)
    └── Drives 9 AudioSources (4 main + 4 crossfade + 1 stinger)
        Handles loop points, intro→loop, crossfade source swapping
```

---

## Combat Intensity

The intensity value (0.0–1.0) is computed from nearby AI `AlertState`:

1. All AI entities with AlertState within `MaxCombatIntensityRange` meters are collected
2. Sorted by distance (nearest first)
3. Top `MaxIntensityContributors` entities contribute weighted intensity
4. Weights: COMBAT=1.0, SEARCHING=0.6, SUSPICIOUS=0.3, CURIOUS=0.1
5. Raw sum normalized by MaxIntensityContributors, clamped to 0–1
6. Smoothed via lerp at `CombatFadeSpeed`

**Boss override** forces intensity to 1.0 (all stems active).

**Fallback**: If no AlertState entities exist (e.g., remote client), the system uses the player's binary `CombatState.IsInCombat` flag (intensity = 0.8 in combat, 0.0 out).

Use the **Intensity Curve** module in Music Workstation to visualize and tune weights.

---

## 16KB Archetype Impact

**Zero bytes** on player entity. All music data lives on dedicated singleton entities (`MusicState`, `MusicConfig`, `MusicDatabaseManaged`). `MusicStingerRequest` and `MusicBossOverride` are transient entities created and destroyed within the same frame.

---

## Audio Routing

All stem and stinger AudioSources are routed to the **Music** mixer group on `AudioManager.MasterMixer`. The `CombatMusicDuckSystem` reads `MusicState.IsInCombat` to duck the Music bus (-3dB + 8kHz low-pass) and Ambient bus (-4dB) during combat, with a 5-second grace period on combat exit.

Ensure your AudioMixer has exposed parameters: `MusicVolume`, `AmbientVolume`, `MusicCutoff`.

---

## Setup Checklist

- [ ] Run **DIG → Music Workstation → Create Default Assets** (creates MusicConfig + MusicDatabase in Resources/)
- [ ] Configure `MusicConfig` values (fade speeds, intensity weights, stinger cooldown)
- [ ] Create MusicTrackSO assets (one per track, assign stems)
- [ ] Drag all tracks into `MusicDatabase.Tracks` list
- [ ] Set `MusicDatabase.DefaultTrackId` to your fallback track
- [ ] Add stinger definitions to `MusicDatabase.Stingers` list
- [ ] Place MusicZoneAuthoring + trigger colliders in client subscenes
- [ ] Set zone TrackIds and priorities (use Zone Mapper to verify coverage)
- [ ] Add MusicBossOverrideAuthoring to boss encounters
- [ ] Add PlayMusic triggers in encounter profiles (ActionValue = TrackId)
- [ ] (Optional) Add MusicNowPlayingView to HUD canvas
- [ ] Open **DIG → Music Workstation** and verify:
  - [ ] Track Browser shows all tracks with correct stems
  - [ ] Zone Mapper shows no overlap conflicts
  - [ ] Stinger Tester plays all stingers correctly
- [ ] Enter Play Mode → press **F8** → verify Live Debug shows expected state
- [ ] AudioMixer has exposed `MusicVolume`, `AmbientVolume`, `MusicCutoff` parameters

---

## Common Patterns

### Exploration Area with Combat Escalation

- Place a large MusicZoneAuthoring (Priority=0) covering the area
- Assign an exploration track with 4 stems
- As enemies engage (AlertState escalates), percussion/melody/intensity layers fade in automatically
- No additional setup needed — the intensity system reads AlertState

### Dungeon with Boss Arena

- Dungeon zone: MusicZoneAuthoring, TrackId=5, Priority=1
- Boss arena zone (nested inside dungeon): MusicZoneAuthoring, TrackId=20, Priority=10
- Boss encounter: MusicBossOverrideAuthoring, BossTrackId=21
- Player enters dungeon → dungeon music. Enters arena → arena music (higher priority). Boss triggered → boss music override (ignores zones entirely). Boss dies → zone music resumes.

### Town Safe Zone

- Town zone: MusicZoneAuthoring, TrackId=3 (Town category), Priority=5
- Higher priority than surrounding wilderness zones ensures town music plays when entering
- Set FadeInDuration=2.0 for a slow, relaxed crossfade on entry

---

## File Manifest

### Scripts (19 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Music/Components/MusicState.cs` | Runtime state singleton |
| `Assets/Scripts/Music/Components/MusicConfig.cs` | Configuration singleton |
| `Assets/Scripts/Music/Components/MusicZone.cs` | Zone trigger component |
| `Assets/Scripts/Music/Components/MusicStingerRequest.cs` | Transient stinger request |
| `Assets/Scripts/Music/Components/MusicBossOverride.cs` | Transient boss override |
| `Assets/Scripts/Music/Definitions/MusicEnums.cs` | Enums and priority constants |
| `Assets/Scripts/Music/Definitions/MusicTrackSO.cs` | Track ScriptableObject |
| `Assets/Scripts/Music/Definitions/MusicDatabaseSO.cs` | Database ScriptableObject |
| `Assets/Scripts/Music/Definitions/MusicConfigSO.cs` | Config ScriptableObject |
| `Assets/Scripts/Music/Definitions/MusicStingerDefinition.cs` | Stinger definition class |
| `Assets/Scripts/Music/Systems/MusicBootstrapSystem.cs` | Bootstrap + MusicDatabaseManaged |
| `Assets/Scripts/Music/Systems/MusicZoneSystem.cs` | Zone detection |
| `Assets/Scripts/Music/Systems/MusicCombatIntensitySystem.cs` | Intensity computation |
| `Assets/Scripts/Music/Systems/MusicTransitionSystem.cs` | Crossfade driver (Burst) |
| `Assets/Scripts/Music/Systems/MusicStemMixSystem.cs` | Stem volume mixer (Burst) |
| `Assets/Scripts/Music/Systems/MusicStingerSystem.cs` | Stinger processor |
| `Assets/Scripts/Music/Systems/MusicPlaybackSystem.cs` | AudioSource driver |
| `Assets/Scripts/Music/Systems/MusicStingerAPI.cs` | Static stinger request helper |
| `Assets/Scripts/Music/Authoring/MusicZoneAuthoring.cs` | Zone baker |
| `Assets/Scripts/Music/Authoring/MusicBossOverrideAuthoring.cs` | Boss override baker |

### UI & Bridges (4 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Music/Bridges/IMusicUIProvider.cs` | UI provider interface |
| `Assets/Scripts/Music/Bridges/MusicUIRegistry.cs` | Static provider registry |
| `Assets/Scripts/Music/Bridges/MusicUIBridgeSystem.cs` | ECS→UI bridge |
| `Assets/Scripts/Music/UI/MusicNowPlayingView.cs` | Now Playing widget |

### Debug (1 file)
| File | Description |
|------|-------------|
| `Assets/Scripts/Music/Debug/MusicDebugSystem.cs` | F8 debug overlay |

### Editor (8 files)
| File | Description |
|------|-------------|
| `Assets/Editor/MusicWorkstation/IMusicWorkstationModule.cs` | Module interface |
| `Assets/Editor/MusicWorkstation/MusicWorkstationWindow.cs` | Editor window + Create Default Assets |
| `Assets/Editor/MusicWorkstation/Modules/TrackBrowserModule.cs` | Track browser |
| `Assets/Editor/MusicWorkstation/Modules/StemPreviewerModule.cs` | Stem previewer |
| `Assets/Editor/MusicWorkstation/Modules/ZoneMapperModule.cs` | Zone visualizer |
| `Assets/Editor/MusicWorkstation/Modules/StingerTesterModule.cs` | Stinger tester |
| `Assets/Editor/MusicWorkstation/Modules/LiveDebugModule.cs` | Live debug |
| `Assets/Editor/MusicWorkstation/Modules/IntensityCurveModule.cs` | Intensity curve editor |

### Modified (4 files)
| File | Change |
|------|--------|
| `Assets/Scripts/Audio/Systems/CombatMusicDuckSystem.cs` | Reads MusicState.IsInCombat instead of direct CombatState |
| `Assets/Scripts/Audio/AudioTelemetry.cs` | Added music telemetry fields |
| `Assets/Scripts/AI/Components/EncounterTrigger.cs` | Added PlayMusic=16 action type |
| `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` | PlayMusic handler creates MusicBossOverride entity |
