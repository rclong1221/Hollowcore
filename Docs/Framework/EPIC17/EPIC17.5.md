# EPIC 17.5: Dynamic Music System

**Status:** PLANNED
**Priority:** Medium (Audio Polish & Atmosphere)
**Dependencies:**
- `CombatMusicDuckSystem` (existing -- `Audio.Systems`, EPIC 15.27 Phase 6, `Assets/Scripts/Audio/Systems/CombatMusicDuckSystem.cs`, reads `CombatState.IsInCombat`, ducks MusicBus -3dB + AmbientBus -4dB with 5s grace period)
- `AudioManager` MonoBehaviour (existing -- `Audio.Systems`, `Assets/Scripts/Audio/AudioManager.cs`, global singleton, `MasterMixer` exposed with MusicVolume/AmbientVolume/MusicCutoff params)
- `AudioBusType` enum (existing -- `Audio.Config`, `Assets/Scripts/Audio/Config/AudioBusType.cs`, Combat=0/Ambient=1/Music=2/Dialogue=3/UI=4/Footstep=5)
- `AudioSourcePool` MonoBehaviour (existing -- `Audio.Systems`, pool-based AudioSource management)
- `AudioTelemetry` static class (existing -- `Audio.Systems`, `Assets/Scripts/Audio/AudioTelemetry.cs`, debug counters)
- `SoundEventRequest` IComponentData (existing -- `DIG.Aggro.Components`, `Assets/Scripts/Aggro/Components/SoundEventRequest.cs`, transient entity: Position/SourceEntity/Loudness/MaxRange/Category)
- `AlertState` IComponentData (existing -- `DIG.Aggro.Components`, `Assets/Scripts/Aggro/Components/AlertState.cs`, AlertLevel int with constants IDLE=0/CURIOUS=1/SUSPICIOUS=2/SEARCHING=3/COMBAT=4)
- `CombatState` IComponentData (existing -- `DIG.Combat.Components`, `Assets/Scripts/Combat/Components/CombatStateComponents.cs`, IsInCombat bool [GhostField])
- `EncounterTriggerSystem` (existing -- `DIG.AI.Systems`, `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs`, evaluates TriggerConditionType, fires TriggerActionType)
- `TriggerActionType` enum (existing -- `DIG.AI.Components`, `Assets/Scripts/AI/Components/EncounterTrigger.cs`, values 0-15 including PlayVFX=6)
- `GhostOwnerIsLocal` tag (existing -- `Unity.NetCode`, filters queries to local player)

**Feature:** A client-only layered stem music system with zone-based track switching, combat intensity-driven stem mixing, boss music overrides, one-shot stingers, and configurable crossfading. Music state lives in a singleton (zero player archetype impact). Reads AI `AlertState` from nearby enemies to compute real-time combat intensity. Integrates with existing `CombatMusicDuckSystem` by providing a centralized `MusicState.IsInCombat` flag. All music systems run exclusively on `ClientSimulation | LocalSimulation`.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `CombatMusicDuckSystem` | `CombatMusicDuckSystem.cs` | Fully implemented | Reads `CombatState.IsInCombat` directly, ducks MusicBus/AmbientBus. Will be modified to read `MusicState.IsInCombat` instead |
| `AudioManager` (MasterMixer) | `AudioManager.cs` | Fully implemented | Exposed params: MusicVolume, AmbientVolume, MusicCutoff. Legacy pool + AudioSourcePool integration |
| `AudioBusType.Music` | `AudioBusType.cs` | Enum value=2 | Bus routing exists, no music playback system uses it |
| `AudioSourcePool` | `AudioSourcePool.cs` | Fully implemented | Pool-based acquisition with bus/priority. Will be used for stinger playback |
| `AudioTelemetry` | `AudioTelemetry.cs` | Fully implemented | Debug counters for audio events. Will add music telemetry fields |
| `AlertState.AlertLevel` | `AlertState.cs` | Fully implemented | IDLE(0) through COMBAT(4) on all AI entities. Ghost:ServerSimulation only |
| `CombatState.IsInCombat` | `CombatStateComponents.cs` | Ghost:All | On player, replicated to all clients |
| `EncounterTriggerSystem` | `EncounterTriggerSystem.cs` | Fully implemented | `TriggerActionType.PlayVFX=6` exists; music override needs new action type |
| `SoundEventRequest` | `SoundEventRequest.cs` | Transient entity | Spatial audio requests; music system does not use spatial audio but shares the transient entity pattern |

### What's Missing

- **No music track definitions** -- no ScriptableObject for multi-stem music tracks with loop points and BPM
- **No music database** -- no registry of available tracks, stinger definitions, or zone-to-track mappings
- **No music playback system** -- no system drives AudioSource playback for looping stems
- **No zone-based switching** -- no trigger volumes that assign a music track to a region
- **No combat intensity computation** -- no system reads AlertState from nearby AI to produce a 0-1 intensity value
- **No stem mixing** -- no system independently adjusts per-stem volumes based on combat intensity
- **No crossfade logic** -- no interpolation between current and target tracks
- **No boss music override** -- encounter triggers cannot force a specific track
- **No stinger system** -- no one-shot musical events (level-up fanfare, quest complete, death sting)
- **No music editor tooling** -- no track previewer, intensity curve editor, zone visualizer, or live debug

---

## Problem

DIG has a complete spatial audio pipeline (AudioSourcePool, occlusion, reverb zones, surface-aware footsteps), a combat duck system that mutes music during fights, and a 5-level AI alert system -- but there is no actual music playback, no tracks, no stems, and no adaptive music behavior. The `MusicBus` on the mixer receives silence. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `AudioManager.MasterMixer` with MusicVolume/AmbientVolume params | No system writes to MusicVolume for actual music playback |
| `AudioBusType.Music` (value=2) bus routing | No AudioSource routes to the Music bus |
| `CombatMusicDuckSystem` ducks music during combat | Ducks silence -- no music plays to be ducked |
| `AlertState.AlertLevel` (5 levels) on all AI entities | Not read by any music system for intensity |
| `CombatState.IsInCombat` ghost-replicated on player | Read only by duck system, not centralized |
| `EncounterTriggerSystem` with 16 action types | No music override action type (PlayMusic) |
| `AudioSourcePool` with bus-aware acquisition | Not used for music stems or stingers |

**The gap:** Players explore a rich voxel world with spatial audio (footsteps, impacts, combat sounds) but in complete musical silence. There is no exploration music, no combat escalation through layered stems, no boss fight themes, and no musical feedback for progression milestones. The mixer's Music bus receives zero input.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  MusicTrackSO           MusicDatabaseSO          MusicConfigSO
  (stems[], BPM,         (all tracks +             (fade speeds,
   loop points,           stinger defs)             intensity range,
   intensity thresholds)                            default track)
           |                    |                        |
           └────── MusicBootstrapSystem ─────────────────┘
                   (loads from Resources/, creates
                    MusicDatabaseManaged singleton,
                    initializes MusicState singleton)
                              |
                    ECS SINGLETON LAYER
  MusicState                 MusicConfig
  (CurrentTrackId,           (DefaultTrackId,
   TargetTrackId,             CombatFadeSpeed,
   CombatIntensity,           ZoneFadeSpeed,
   StemVolumes float4,        StingerVolume,
   BossOverrideTrackId)       MaxCombatIntensityRange)
                              |
                    SYSTEM PIPELINE
                              |
  MusicZoneSystem (Simulation, Client|Local)
  └── player trigger overlap → set TargetTrackId
                              |
  MusicCombatIntensitySystem (Simulation, Client|Local)
  └── read AlertState from nearby AI → compute CombatIntensity (0-1)
  └── read CombatState.IsInCombat → write MusicState.IsInCombat
                              |
  MusicTransitionSystem (Presentation, Client|Local)
  └── crossfade CurrentTrackId → TargetTrackId
                              |
  MusicStemMixSystem (Presentation, Client|Local)
  └── adjust per-stem volumes from CombatIntensity + thresholds
                              |
  MusicStingerSystem (Presentation, Client|Local)
  └── play one-shot stingers (level-up, quest complete, death)
                              |
  MusicPlaybackSystem (Presentation, Client|Local, MANAGED)
  └── drives 4 AudioSources (one per stem) + 1 stinger source
  └── sets clip, volume, loop points, crossfade
                              |
  CombatMusicDuckSystem (Presentation, Client|Local, MODIFIED)
  └── reads MusicState.IsInCombat instead of CombatState.IsInCombat
```

### Data Flow (Enter Combat Zone → Stem Escalation → Boss Override)

```
Frame N (Client):
  1. MusicZoneSystem: Player LocalTransform overlaps MusicZone trigger volume
     - Read MusicZone.TrackId, compare Priority to current zone
     - If higher priority: MusicState.TargetTrackId = zone.TrackId
     - MusicState.ZoneFadeInDuration = zone.FadeInDuration

Frame N+1..N+K (Client):
  2. MusicCombatIntensitySystem: Reads AlertState from all AI within MaxCombatIntensityRange
     - Count AI at each alert level: IDLE, CURIOUS, SUSPICIOUS, SEARCHING, COMBAT
     - Intensity = weighted sum: COMBAT*1.0 + SEARCHING*0.6 + SUSPICIOUS*0.3 + CURIOUS*0.1
     - Clamp to [0, 1], apply smoothing (lerp toward target at CombatFadeSpeed)
     - Write MusicState.CombatIntensity
     - Write MusicState.IsInCombat = (any AI at COMBAT within range, or CombatState.IsInCombat)

  3. MusicTransitionSystem: CrossfadeProgress lerps toward 1.0
     - Old track stems fade out, new track stems fade in
     - When CrossfadeProgress >= 1.0: CurrentTrackId = TargetTrackId, reset

  4. MusicStemMixSystem: Reads CombatIntensity + MusicTrackSO.CombatIntensityThresholds
     - Base stem: always 1.0 volume
     - Percussion stem: enabled when intensity >= thresholds[0] (default 0.2)
     - Melody stem: enabled when intensity >= thresholds[1] (default 0.5)
     - Intensity stem: enabled when intensity >= thresholds[2] (default 0.8)
     - Per-stem volume = smooth lerp to target (no sudden cuts)
     - Write MusicState.StemVolumes (float4)

  5. MusicPlaybackSystem (managed): Reads MusicState
     - Resolve TrackId → MusicTrackSO via MusicDatabaseManaged
     - Assign AudioClips to stem AudioSources
     - Set volumes from StemVolumes float4
     - Handle loop points (schedule next play at loopEnd - crossfadeTime)

Boss Override:
  6. EncounterTriggerSystem fires TriggerActionType.PlayMusic (new value=16)
     - Ghost-replicated via EncounterState on boss entity
     - MusicCombatIntensitySystem detects BossOverrideTrackId != 0
     - Forces TargetTrackId = BossOverrideTrackId, CombatIntensity = 1.0
     - On boss death: BossOverrideTrackId = 0, natural zone track resumes
```

### Critical System Ordering Chain

```
SimulationSystemGroup (Client|Local):
  MusicZoneSystem [UpdateAfter(typeof(PhysicsSystemGroup))]
      |
  MusicCombatIntensitySystem [UpdateAfter(typeof(MusicZoneSystem))]

PresentationSystemGroup (Client|Local):
  MusicTransitionSystem [UpdateBefore(typeof(MusicStemMixSystem))]
      |
  MusicStemMixSystem [UpdateBefore(typeof(MusicStingerSystem))]
      |
  MusicStingerSystem [UpdateBefore(typeof(MusicPlaybackSystem))]
      |
  MusicPlaybackSystem [UpdateBefore(typeof(CombatMusicDuckSystem))]
      |
  CombatMusicDuckSystem (MODIFIED) [UpdateAfter(typeof(MusicPlaybackSystem))]
```

---

## ECS Components

### MusicState Singleton

**File:** `Assets/Scripts/Music/Components/MusicState.cs`

```
MusicState (IComponentData, NOT ghost-replicated -- client-only singleton)
  CurrentTrackId        : int       // Currently playing track (0 = none)
  TargetTrackId         : int       // Track to crossfade toward
  CrossfadeProgress     : float     // 0.0 = fully old track, 1.0 = fully new track
  CrossfadeDirection    : byte      // 0 = idle, 1 = fading in, 2 = fading out
  CombatIntensity       : float     // 0.0 (peaceful) to 1.0 (maximum combat)
  SmoothedIntensity     : float     // Lerped CombatIntensity (avoids jitter)
  BossOverrideTrackId   : int       // Non-zero = boss music forced. 0 = normal zone music
  IsInCombat            : bool      // Centralized combat flag (replaces direct CombatState reads)
  StemVolumes           : float4    // x=Base, y=Percussion, z=Melody, w=Intensity
  CurrentZonePriority   : int       // Priority of the active music zone
  ZoneFadeInDuration    : float     // Current zone's fade-in speed override
  ZoneFadeOutDuration   : float     // Current zone's fade-out speed override
  StingerCooldown       : float     // Remaining cooldown before another stinger can play
```

**Byte size:** 4+4+4+1+4+4+4+1+16+4+4+4+4 = **58 bytes** (padded to 60 with alignment).

**Archetype impact:** Zero on player entity. This is a singleton on a dedicated entity.

### MusicConfig Singleton

**File:** `Assets/Scripts/Music/Components/MusicConfig.cs`

```
MusicConfig (IComponentData, NOT ghost-replicated -- client-only singleton)
  DefaultTrackId             : int     // Fallback track when no zone active
  CombatFadeSpeed            : float   // Lerp speed for CombatIntensity smoothing (default 2.0)
  ZoneFadeSpeed              : float   // Default crossfade speed between zones (default 1.5)
  StingerVolume              : float   // Master volume for stinger playback (default 0.8)
  StingerCooldown            : float   // Min seconds between stingers (default 3.0)
  MaxCombatIntensityRange    : float   // Max distance to read AlertState from AI (default 40.0)
  StemTransitionSpeed        : float   // Lerp speed for per-stem volume changes (default 3.0)
  BossOverrideFadeSpeed      : float   // Fast fade speed for boss music entry (default 4.0)
  IntensityWeightCombat      : float   // Weight for COMBAT alert level (default 1.0)
  IntensityWeightSearching   : float   // Weight for SEARCHING alert level (default 0.6)
  IntensityWeightSuspicious  : float   // Weight for SUSPICIOUS alert level (default 0.3)
  IntensityWeightCurious     : float   // Weight for CURIOUS alert level (default 0.1)
  MaxIntensityContributors   : int     // Cap on AI entities counted (default 8)
```

**Byte size:** 4+4+4+4+4+4+4+4+4+4+4+4+4 = **52 bytes**.

### MusicZone (on Trigger Volume Entities)

**File:** `Assets/Scripts/Music/Components/MusicZone.cs`

```
MusicZone (IComponentData, NOT ghost-replicated -- baked on zone volumes, client subscene)
  TrackId          : int     // MusicTrackSO.TrackId reference
  Priority         : int     // Higher priority overrides lower (boss arenas > overworld)
  FadeInDuration   : float   // Seconds to crossfade into this zone's track (0 = use default)
  FadeOutDuration  : float   // Seconds to crossfade out when leaving (0 = use default)
```

**Byte size:** 4+4+4+4 = **16 bytes**. On zone volume entities, not player.

### MusicStingerRequest (Transient Entity)

**File:** `Assets/Scripts/Music/Components/MusicStingerRequest.cs`

```
MusicStingerRequest (IComponentData -- transient entity, created via ECB, destroyed same frame)
  StingerId      : int     // References MusicDatabaseSO stinger definition
  Priority       : byte    // Higher priority interrupts lower (death > loot > quest)
  AllowOverlap   : bool    // If true, plays alongside current stinger
  VolumeScale    : float   // Multiplier on StingerVolume (default 1.0)
```

**Byte size:** 4+1+1+4 = **10 bytes** (padded to 12).

### MusicBossOverride (Transient Entity)

**File:** `Assets/Scripts/Music/Components/MusicBossOverride.cs`

```
MusicBossOverride (IComponentData -- transient entity, created by encounter system)
  TrackId     : int     // Boss music track ID
  Activate    : bool    // true = start override, false = clear override
```

**Byte size:** 4+1 = **5 bytes** (padded to 8).

### MusicTelemetry Additions

**File:** `Assets/Scripts/Audio/AudioTelemetry.cs` (MODIFY -- add music fields)

```
// New static fields:
public static int TrackTransitionsThisSession { get; set; }
public static int StingersPlayedThisSession { get; set; }
public static float CurrentCombatIntensity { get; set; }
public static int CurrentTrackId { get; set; }
public static int ActiveStemCount { get; set; }
```

---

## ScriptableObjects

### MusicTrackSO

**File:** `Assets/Scripts/Music/Definitions/MusicTrackSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Music/Music Track")]
```

| Field | Type | Purpose |
|-------|------|---------|
| TrackId | int | Unique identifier (must match MusicZone.TrackId references) |
| TrackName | string | Display name for editor tooling |
| Category | MusicTrackCategory | Exploration/Combat/Boss/Ambient/Town/Dungeon |
| BPM | float | Beats per minute (for beat-synced transitions) |
| BaseStem | AudioClip | Always-playing foundation layer |
| PercussionStem | AudioClip | Rhythmic layer, activated at low combat intensity |
| MelodyStem | AudioClip | Melodic layer, activated at medium combat intensity |
| IntensityStem | AudioClip | Full combat layer, activated at high combat intensity |
| LoopStartSample | int | Sample offset for loop start (0 = beginning) |
| LoopEndSample | int | Sample offset for loop end (0 = clip length) |
| IntroClip | AudioClip | Optional non-looping intro (plays once before loop) |
| BaseVolume | float [0-1] | Master volume for this track (default 1.0) |
| CombatIntensityThresholds | float3 | x=Percussion threshold, y=Melody threshold, z=Intensity threshold |
| StemFadeInTime | float | Per-stem fade-in duration override (default 0.5s) |
| StemFadeOutTime | float | Per-stem fade-out duration override (default 1.0s) |

### MusicStingerDefinition

**File:** `Assets/Scripts/Music/Definitions/MusicStingerDefinition.cs`

```
[Serializable]
```

| Field | Type | Purpose |
|-------|------|---------|
| StingerId | int | Unique identifier |
| StingerName | string | Display name (e.g., "Level Up Fanfare") |
| Clip | AudioClip | One-shot audio clip |
| DuckMusicDB | float | How much to duck music during stinger (default -6dB) |
| DuckDuration | float | Duration of music duck (default = clip length) |
| DefaultPriority | byte | Default priority (higher = more important) |
| Category | StingerCategory | LevelUp/QuestComplete/Death/RareItem/Achievement/BossIntro/Discovery |

### MusicDatabaseSO

**File:** `Assets/Scripts/Music/Definitions/MusicDatabaseSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Music/Music Database")]
```

| Field | Type | Purpose |
|-------|------|---------|
| Tracks | List\<MusicTrackSO\> | All available music tracks |
| Stingers | List\<MusicStingerDefinition\> | All stinger definitions |
| DefaultTrackId | int | Fallback track when no zone is active |
| SilenceTrackId | int | Special "silence" track for areas with no music |

### MusicConfigSO

**File:** `Assets/Scripts/Music/Definitions/MusicConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Music/Music Config")]
```

| Field | Type | Purpose |
|-------|------|---------|
| DefaultTrackId | int | Default exploration track |
| CombatFadeSpeed | float | Intensity smoothing lerp speed (default 2.0) |
| ZoneFadeSpeed | float | Zone crossfade speed (default 1.5) |
| StingerVolume | float [0-1] | Master stinger volume (default 0.8) |
| StingerCooldown | float | Min seconds between stingers (default 3.0) |
| MaxCombatIntensityRange | float | AlertState read range (default 40.0) |
| StemTransitionSpeed | float | Per-stem volume lerp speed (default 3.0) |
| BossOverrideFadeSpeed | float | Boss music transition speed (default 4.0) |
| IntensityWeightCombat | float | COMBAT alert weight (default 1.0) |
| IntensityWeightSearching | float | SEARCHING alert weight (default 0.6) |
| IntensityWeightSuspicious | float | SUSPICIOUS alert weight (default 0.3) |
| IntensityWeightCurious | float | CURIOUS alert weight (default 0.1) |
| MaxIntensityContributors | int | AI entity count cap for intensity (default 8) |

### Supporting Enums

**File:** `Assets/Scripts/Music/Definitions/MusicEnums.cs`

```
MusicTrackCategory enum: Exploration(0), Combat(1), Boss(2), Ambient(3), Town(4), Dungeon(5)

StingerCategory enum: LevelUp(0), QuestComplete(1), Death(2), RareItem(3),
                       Achievement(4), BossIntro(5), Discovery(6)

StingerPriority: Death=100, BossIntro=90, LevelUp=80, QuestComplete=70,
                 Achievement=60, RareItem=50, Discovery=40
```

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Client|Local):
  MusicBootstrapSystem                       -- loads SOs from Resources/, creates singletons (runs once)

SimulationSystemGroup (Client|Local):
  [after PhysicsSystemGroup]
  MusicZoneSystem                            -- detects player in zone trigger volumes, sets TargetTrackId
  MusicCombatIntensitySystem                 -- reads AlertState from nearby AI, computes CombatIntensity
  [after MusicCombatIntensitySystem]

PresentationSystemGroup (Client|Local):
  MusicTransitionSystem                      -- handles crossfading between tracks
  MusicStemMixSystem                         -- adjusts per-stem volumes based on combat intensity
  MusicStingerSystem                         -- processes one-shot stinger requests
  MusicPlaybackSystem (managed SystemBase)   -- drives AudioSource playback
  CombatMusicDuckSystem (MODIFIED)           -- reads MusicState.IsInCombat
  MusicDebugSystem (optional)                -- debug overlay with intensity, track, stems
```

### MusicBootstrapSystem

**File:** `Assets/Scripts/Music/Systems/MusicBootstrapSystem.cs`

Managed SystemBase, `InitializationSystemGroup`, `ClientSimulation | LocalSimulation`. Runs once.

1. Load `MusicConfigSO` from `Resources/MusicConfig`
2. Load `MusicDatabaseSO` from `Resources/MusicDatabase`
3. Create `MusicConfig` singleton from config SO fields
4. Create `MusicState` singleton with `CurrentTrackId = config.DefaultTrackId`
5. Create `MusicDatabaseManaged` singleton (managed, holds SO references + Dictionary lookups)
6. `Enabled = false` (self-disables after first run)

### MusicZoneSystem

**File:** `Assets/Scripts/Music/Systems/MusicZoneSystem.cs`

Managed SystemBase, `SimulationSystemGroup`, `ClientSimulation | LocalSimulation`.

1. Query local player position via `SystemAPI.Query<RefRO<LocalTransform>>().WithAll<GhostOwnerIsLocal>()`
2. Query all `MusicZone` entities with `PhysicsCollider` (trigger volumes)
3. For each zone: point-in-AABB test against zone collider bounds
4. Track highest-priority overlapping zone
5. If new zone differs from current:
   - `MusicState.TargetTrackId = zone.TrackId`
   - `MusicState.CurrentZonePriority = zone.Priority`
   - `MusicState.ZoneFadeInDuration = zone.FadeInDuration > 0 ? zone.FadeInDuration : MusicConfig.ZoneFadeSpeed`
6. If no zone overlaps and current != default:
   - `MusicState.TargetTrackId = MusicConfig.DefaultTrackId`
   - `MusicState.CurrentZonePriority = 0`

### MusicCombatIntensitySystem

**File:** `Assets/Scripts/Music/Systems/MusicCombatIntensitySystem.cs`

Managed SystemBase, `SimulationSystemGroup`, `ClientSimulation | LocalSimulation`.

**Critical:** This system reads `AlertState` from AI entities. On a listen server, the client world has access to `AlertState` via ghost replication (AlertState is on server-simulated entities). On a dedicated server remote client, `AlertState` is NOT ghost-replicated -- the system falls back to reading the local player's `CombatState.IsInCombat` as a binary 0/1 intensity.

1. Get local player position
2. Query all entities with `AlertState` component within `MaxCombatIntensityRange`
3. Compute weighted intensity:
   ```
   rawIntensity = 0
   contributorCount = 0
   foreach AI with AlertState within range (capped at MaxIntensityContributors):
       weight = alertLevel switch {
           COMBAT    => IntensityWeightCombat,     // 1.0
           SEARCHING => IntensityWeightSearching,  // 0.6
           SUSPICIOUS=> IntensityWeightSuspicious, // 0.3
           CURIOUS   => IntensityWeightCurious,    // 0.1
           _ => 0
       }
       rawIntensity += weight
       contributorCount++

   // Normalize: single COMBAT enemy = 1.0, many lower = gradual buildup
   targetIntensity = saturate(rawIntensity / MaxIntensityContributors)
   ```
4. Smooth: `SmoothedIntensity = lerp(SmoothedIntensity, targetIntensity, dt * CombatFadeSpeed)`
5. Write `MusicState.CombatIntensity = SmoothedIntensity`
6. Write `MusicState.IsInCombat = (anyCombat || localPlayerCombatState.IsInCombat)`
7. Handle boss override: if `MusicBossOverride` entity exists:
   - If `Activate`: `MusicState.BossOverrideTrackId = trackId`, force `TargetTrackId`, `CombatIntensity = 1.0`
   - If `!Activate`: `MusicState.BossOverrideTrackId = 0`, resume zone track
   - Destroy transient entity

**Fallback for remote client (no AlertState):**
```
if (alertStateQuery.CalculateEntityCount() == 0):
    // No server AI data available -- use binary combat flag
    targetIntensity = localPlayerIsInCombat ? 0.8 : 0.0
```

### MusicTransitionSystem

**File:** `Assets/Scripts/Music/Systems/MusicTransitionSystem.cs`

ISystem, `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`.

1. Read `MusicState` singleton
2. If `TargetTrackId != CurrentTrackId` and `CrossfadeDirection == 0`:
   - Set `CrossfadeDirection = 1` (fading)
   - Reset `CrossfadeProgress = 0`
3. If `CrossfadeDirection == 1`:
   - `fadeSpeed = BossOverrideTrackId != 0 ? BossOverrideFadeSpeed : ZoneFadeInDuration`
   - `CrossfadeProgress = min(1.0, CrossfadeProgress + dt / fadeSpeed)`
   - When `CrossfadeProgress >= 1.0`:
     - `CurrentTrackId = TargetTrackId`
     - `CrossfadeDirection = 0`
     - `CrossfadeProgress = 0`
     - Increment `AudioTelemetry.TrackTransitionsThisSession`

### MusicStemMixSystem

**File:** `Assets/Scripts/Music/Systems/MusicStemMixSystem.cs`

ISystem, `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`.

1. Read `MusicState.SmoothedIntensity`
2. Resolve current track's `CombatIntensityThresholds` (float3: x=perc, y=melody, z=intensity)
3. Compute target stem volumes:
   ```
   targetBase = 1.0  // always on
   targetPerc = SmoothedIntensity >= thresholds.x ? 1.0 : 0.0
   targetMelody = SmoothedIntensity >= thresholds.y ? 1.0 : 0.0
   targetIntensity = SmoothedIntensity >= thresholds.z ? 1.0 : 0.0
   ```
4. Smooth each stem: `StemVolumes.x = lerp(StemVolumes.x, targetBase, dt * StemTransitionSpeed)`
5. During crossfade: multiply all stem volumes by `(1 - CrossfadeProgress)` for old track, `CrossfadeProgress` for new track
6. Write `MusicState.StemVolumes`
7. Update `AudioTelemetry.ActiveStemCount`

### MusicStingerSystem

**File:** `Assets/Scripts/Music/Systems/MusicStingerSystem.cs`

Managed SystemBase, `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`.

1. Query `MusicStingerRequest` transient entities
2. Sort by `Priority` (descending)
3. If `MusicState.StingerCooldown > 0` and `!AllowOverlap`: skip, destroy request
4. Resolve `StingerId` → `MusicStingerDefinition` via `MusicDatabaseManaged`
5. Dispatch to `MusicPlaybackSystem` for one-shot playback
6. Set `MusicState.StingerCooldown = MusicConfig.StingerCooldown`
7. Destroy transient entity
8. Increment `AudioTelemetry.StingersPlayedThisSession`

### MusicPlaybackSystem

**File:** `Assets/Scripts/Music/Systems/MusicPlaybackSystem.cs`

Managed SystemBase, `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`.

This is the only system that touches Unity's AudioSource API. It manages 5 persistent AudioSources:
- 4 stem sources (Base, Percussion, Melody, Intensity) -- looping
- 1 stinger source -- one-shot

1. On first update: create 5 AudioSources on a persistent `MusicPlayer` GameObject
   - Route stem sources to `AudioBusType.Music` mixer group
   - Route stinger source to `AudioBusType.Music` mixer group
2. Each frame:
   - Read `MusicState`
   - If `CurrentTrackId` changed: resolve via `MusicDatabaseManaged`, assign clips to stem sources
   - Set stem source volumes from `MusicState.StemVolumes * track.BaseVolume`
   - Handle loop points: if `audioSource.timeSamples >= LoopEndSample`, seek to `LoopStartSample`
   - Handle intro: if intro clip playing and finished, switch to loop clip
   - Tick `StingerCooldown -= dt`
3. Stinger playback (called by MusicStingerSystem):
   - Set stinger source clip, volume = `MusicConfig.StingerVolume * request.VolumeScale`
   - Temporarily duck stem volumes by `stinger.DuckMusicDB` for `stinger.DuckDuration`
   - Play one-shot
4. Update `AudioTelemetry.CurrentTrackId` and `AudioTelemetry.CurrentCombatIntensity`

### MusicStingerAPI

**File:** `Assets/Scripts/Music/Systems/MusicStingerAPI.cs`

Static helper for cross-system stinger requests (follows `XPGrantAPI` pattern):

```
MusicStingerAPI.RequestStinger(EntityCommandBuffer ecb, int stingerId, byte priority = 50, bool allowOverlap = false)
```

Called by:
- `LevelUpSystem` (EPIC 16.14) -- level-up fanfare
- `QuestCompletionSystem` (EPIC 16.12) -- quest complete sting
- `DeathTransitionSystem` -- player death sting
- `CraftOutputCollectionSystem` (EPIC 16.13) -- rare item craft sting

---

## Authoring

### MusicZoneAuthoring

**File:** `Assets/Scripts/Music/Authoring/MusicZoneAuthoring.cs`

```
[AddComponentMenu("DIG/Music/Music Zone")]
```

- Fields: TrackId (int), Priority (int, default 0), FadeInDuration (float, default 0), FadeOutDuration (float, default 0)
- Baker adds: `MusicZone` IComponentData
- Place on trigger volume GameObjects with a Collider (IsTrigger=true) in client subscene
- Requires `PhysicsShapeAuthoring` or legacy Box/Sphere Collider for zone detection

### MusicBossOverrideAuthoring

**File:** `Assets/Scripts/Music/Authoring/MusicBossOverrideAuthoring.cs`

```
[AddComponentMenu("DIG/Music/Boss Music Override")]
```

- Field: BossTrackId (int)
- Baker adds: `MusicBossOverride` with `TrackId = BossTrackId, Activate = false`
- Place on boss encounter entities alongside `EncounterProfileAuthoring`
- `EncounterTriggerSystem` creates `MusicBossOverride` transient entity with `Activate = true` on encounter start

---

## UI Bridge

### MusicUIBridgeSystem

**File:** `Assets/Scripts/Music/Bridges/MusicUIBridgeSystem.cs`

Managed SystemBase, `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`.

Reads `MusicState` singleton each frame and pushes to `MusicUIRegistry`:
- Current track name (resolved via MusicDatabaseManaged)
- Combat intensity percentage
- Active stem names
- Current zone info
- Stinger notifications

### MusicUIRegistry + IMusicUIProvider

**File:** `Assets/Scripts/Music/Bridges/MusicUIRegistry.cs`
**File:** `Assets/Scripts/Music/Bridges/IMusicUIProvider.cs`

```
IMusicUIProvider
  void OnTrackChanged(string trackName, MusicTrackCategory category)
  void OnCombatIntensityChanged(float intensity)
  void OnStingerPlayed(string stingerName, StingerCategory category)
```

Static singleton MonoBehaviour + provider interface. Follows `CombatUIRegistry` / `ProgressionUIRegistry` pattern.

### MusicNowPlayingView

**File:** `Assets/Scripts/Music/UI/MusicNowPlayingView.cs`

Optional MonoBehaviour for "Now Playing" HUD widget. Shows track name on zone transition with fade-out.

---

## Editor Tooling

### MusicWorkstationWindow

**File:** `Assets/Editor/MusicWorkstation/MusicWorkstationWindow.cs`
- Menu: `DIG/Music Workstation`
- Sidebar + `IMusicWorkstationModule` pattern

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Track Browser | `Modules/TrackBrowserModule.cs` | List/search/filter all MusicTrackSO assets, preview stems individually, waveform visualization |
| Stem Previewer | `Modules/StemPreviewerModule.cs` | Play all 4 stems simultaneously with individual volume sliders, intensity slider to simulate combat |
| Zone Mapper | `Modules/ZoneMapperModule.cs` | Scene view overlay showing MusicZone volumes color-coded by track, priority labels, overlap detection warnings |
| Stinger Tester | `Modules/StingerTesterModule.cs` | List all stingers with "Play" button, priority/overlap testing, duck preview |
| Live Debug | `Modules/LiveDebugModule.cs` | Play-mode: current track, intensity meter, active stems, zone stack, transition state, stinger queue |
| Intensity Curve | `Modules/IntensityCurveModule.cs` | Visual editor for intensity weight configuration, preview intensity output for N enemies at each alert level |

---

## Modification to CombatMusicDuckSystem

**File:** `Assets/Scripts/Audio/Systems/CombatMusicDuckSystem.cs` (MODIFY)

Replace the direct `CombatState.IsInCombat` read with `MusicState.IsInCombat`:

**Before (current):**
```csharp
// Read local player's CombatState (DIG.Combat.Components)
foreach (var combatState in SystemAPI.Query<RefRO<CombatState>>().WithAll<GhostOwnerIsLocal>())
{
    isInCombat = combatState.ValueRO.IsInCombat;
}
```

**After (modified):**
```csharp
// Read centralized combat state from MusicState singleton
if (SystemAPI.HasSingleton<MusicState>())
{
    var musicState = SystemAPI.GetSingleton<MusicState>();
    isInCombat = musicState.IsInCombat;
}
else
{
    // Fallback: direct CombatState read if music system not initialized
    foreach (var combatState in SystemAPI.Query<RefRO<CombatState>>().WithAll<GhostOwnerIsLocal>())
    {
        isInCombat = combatState.ValueRO.IsInCombat;
    }
}
```

Add `[UpdateAfter(typeof(MusicPlaybackSystem))]` ordering attribute.

This centralizes the combat detection so both the music system and the duck system agree on combat state. `MusicCombatIntensitySystem` writes `MusicState.IsInCombat` by combining AI AlertState proximity checks with the player's own `CombatState.IsInCombat`.

---

## Encounter System Integration

**File:** `Assets/Scripts/AI/Components/EncounterTrigger.cs` (MODIFY -- add 1 enum value)

Add `TriggerActionType.PlayMusic = 16` to the existing enum. EncounterTriggerSystem creates a `MusicBossOverride` transient entity when this action fires.

**File:** `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` (MODIFY -- ~15 lines)

Add case handler for `TriggerActionType.PlayMusic`:
```csharp
case TriggerActionType.PlayMusic:
    ecb.CreateEntity();
    ecb.AddComponent(entity, new MusicBossOverride
    {
        TrackId = (int)trigger.ActionValue,
        Activate = true
    });
    break;
```

On encounter end (boss death or reset), create `MusicBossOverride { Activate = false }`.

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `MusicBootstrapSystem` | N/A | No | Runs once at startup, loads SOs |
| `MusicZoneSystem` | < 0.02ms | No | AABB overlap test, typically 5-10 zones |
| `MusicCombatIntensitySystem` | < 0.05ms | No | Reads AlertState from up to MaxIntensityContributors (8) AI |
| `MusicTransitionSystem` | < 0.01ms | No (ISystem) | Single singleton read/write, lerp |
| `MusicStemMixSystem` | < 0.01ms | No (ISystem) | 4 float lerps |
| `MusicStingerSystem` | < 0.01ms | No | Only processes transient entities (rare) |
| `MusicPlaybackSystem` | < 0.03ms | No | Managed, 5 AudioSource volume/clip sets |
| `MusicUIBridgeSystem` | < 0.01ms | No | Managed, singleton read + dispatch |
| `MusicDebugSystem` | < 0.01ms | No | Conditional, editor/debug only |
| **Total** | **< 0.15ms** | | All systems combined per client frame |

**Memory:** 5 AudioSources + AudioClip references (clips loaded by Unity asset system, not duplicated). Stem clips are typically 2-4 MB each x 4 stems = 8-16 MB per active track. Crossfade holds 2 tracks briefly = 16-32 MB peak.

---

## Backward Compatibility

| System | Impact | Mitigation |
|--------|--------|------------|
| `CombatMusicDuckSystem` | Modified to read `MusicState.IsInCombat` | Fallback to direct `CombatState.IsInCombat` if `MusicState` singleton absent |
| `EncounterTriggerSystem` | New `TriggerActionType.PlayMusic=16` | Additive enum value, no existing triggers affected |
| `AudioTelemetry` | New static fields added | Additive, no existing fields changed |
| `AudioBusType.Music` | Now actually used | Was always defined, no change to enum |
| Existing subscenes | No changes required | MusicZone entities are new additions to client subscenes |
| Player archetype | Zero bytes added | MusicState is a singleton, not on player |
| Ghost serialization | No changes | All music components are client-only, non-ghost |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `MusicState` singleton | 60 bytes | Dedicated entity (not player) |
| `MusicConfig` singleton | 52 bytes | Dedicated entity (not player) |
| `MusicZone` | 16 bytes | Zone volume entities (not player) |
| `MusicStingerRequest` | 12 bytes | Transient entities (not player) |
| `MusicBossOverride` | 8 bytes | Transient entities (not player) |
| **Total on player** | **0 bytes** | |

Zero impact on player entity archetype. All music state lives on dedicated singleton and volume entities.

---

## File Summary

### New Files (27)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Music/Components/MusicState.cs` | IComponentData singleton |
| 2 | `Assets/Scripts/Music/Components/MusicConfig.cs` | IComponentData singleton |
| 3 | `Assets/Scripts/Music/Components/MusicZone.cs` | IComponentData on zone volumes |
| 4 | `Assets/Scripts/Music/Components/MusicStingerRequest.cs` | IComponentData transient |
| 5 | `Assets/Scripts/Music/Components/MusicBossOverride.cs` | IComponentData transient |
| 6 | `Assets/Scripts/Music/Definitions/MusicTrackSO.cs` | ScriptableObject |
| 7 | `Assets/Scripts/Music/Definitions/MusicDatabaseSO.cs` | ScriptableObject |
| 8 | `Assets/Scripts/Music/Definitions/MusicConfigSO.cs` | ScriptableObject |
| 9 | `Assets/Scripts/Music/Definitions/MusicStingerDefinition.cs` | Serializable class |
| 10 | `Assets/Scripts/Music/Definitions/MusicEnums.cs` | Enums |
| 11 | `Assets/Scripts/Music/Systems/MusicBootstrapSystem.cs` | SystemBase (runs once) |
| 12 | `Assets/Scripts/Music/Systems/MusicZoneSystem.cs` | SystemBase |
| 13 | `Assets/Scripts/Music/Systems/MusicCombatIntensitySystem.cs` | SystemBase |
| 14 | `Assets/Scripts/Music/Systems/MusicTransitionSystem.cs` | ISystem |
| 15 | `Assets/Scripts/Music/Systems/MusicStemMixSystem.cs` | ISystem |
| 16 | `Assets/Scripts/Music/Systems/MusicStingerSystem.cs` | SystemBase |
| 17 | `Assets/Scripts/Music/Systems/MusicPlaybackSystem.cs` | SystemBase (managed) |
| 18 | `Assets/Scripts/Music/Systems/MusicStingerAPI.cs` | Static helper |
| 19 | `Assets/Scripts/Music/Authoring/MusicZoneAuthoring.cs` | Baker |
| 20 | `Assets/Scripts/Music/Authoring/MusicBossOverrideAuthoring.cs` | Baker |
| 21 | `Assets/Scripts/Music/Bridges/MusicUIBridgeSystem.cs` | SystemBase (managed) |
| 22 | `Assets/Scripts/Music/Bridges/MusicUIRegistry.cs` | MonoBehaviour singleton |
| 23 | `Assets/Scripts/Music/Bridges/IMusicUIProvider.cs` | Interface |
| 24 | `Assets/Scripts/Music/UI/MusicNowPlayingView.cs` | MonoBehaviour |
| 25 | `Assets/Scripts/Music/Debug/MusicDebugSystem.cs` | SystemBase (optional) |
| 26 | `Assets/Scripts/Music/DIG.Music.asmdef` | Assembly definition |
| 27 | `Assets/Editor/MusicWorkstation/MusicWorkstationWindow.cs` | EditorWindow |

### Editor Modules (6)

| # | Path | Type |
|---|------|------|
| 28 | `Assets/Editor/MusicWorkstation/IMusicWorkstationModule.cs` | Interface |
| 29 | `Assets/Editor/MusicWorkstation/Modules/TrackBrowserModule.cs` | Module |
| 30 | `Assets/Editor/MusicWorkstation/Modules/StemPreviewerModule.cs` | Module |
| 31 | `Assets/Editor/MusicWorkstation/Modules/ZoneMapperModule.cs` | Module |
| 32 | `Assets/Editor/MusicWorkstation/Modules/StingerTesterModule.cs` | Module |
| 33 | `Assets/Editor/MusicWorkstation/Modules/LiveDebugModule.cs` | Module |
| 34 | `Assets/Editor/MusicWorkstation/Modules/IntensityCurveModule.cs` | Module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Audio/Systems/CombatMusicDuckSystem.cs` | Read `MusicState.IsInCombat` instead of `CombatState.IsInCombat` (fallback preserved). Add `[UpdateAfter(typeof(MusicPlaybackSystem))]` |
| 2 | `Assets/Scripts/Audio/AudioTelemetry.cs` | Add 5 music telemetry fields (+20 bytes static) |
| 3 | `Assets/Scripts/AI/Components/EncounterTrigger.cs` | Add `TriggerActionType.PlayMusic = 16` enum value |
| 4 | `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` | Add `PlayMusic` case handler (~15 lines) |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/MusicConfig.asset` |
| 2 | `Resources/MusicDatabase.asset` |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `LevelUpSystem` | 16.14 | Calls `MusicStingerAPI.RequestStinger(ecb, StingerIds.LevelUp, priority: 80)` on level-up |
| `QuestCompletionSystem` | 16.12 | Calls `MusicStingerAPI.RequestStinger(ecb, StingerIds.QuestComplete, priority: 70)` |
| `CraftOutputCollectionSystem` | 16.13 | Calls `MusicStingerAPI.RequestStinger()` on rare/epic craft output |
| `EncounterTriggerSystem` | 15.32 | New `PlayMusic` action type creates `MusicBossOverride` for boss themes |
| `CombatMusicDuckSystem` | 15.27 | Modified to read `MusicState.IsInCombat` (centralized combat flag) |
| `PersistenceSaveModule` (Settings) | 16.15 | `SettingsSaveModule` already saves MusicVolume -- no change needed |
| `DeathTransitionSystem` | Core | Player death stinger via `MusicStingerAPI` |
| `AlertStateSystem` | 15.33 | `MusicCombatIntensitySystem` reads `AlertState.AlertLevel` for intensity computation |
| `DialogueActionSystem` | 16.16 | Future: dialogue-triggered music zone override |

---

## Verification Checklist

- [ ] `MusicBootstrapSystem` creates `MusicState` and `MusicConfig` singletons on client startup
- [ ] `MusicDatabaseManaged` singleton resolves TrackId to MusicTrackSO correctly
- [ ] Default track plays on game start when no zone is active
- [ ] Player enters MusicZone trigger volume: crossfade to zone's track
- [ ] Player exits MusicZone: crossfade back to default track
- [ ] Overlapping zones: higher priority zone wins
- [ ] Zone fade durations: per-zone FadeInDuration/FadeOutDuration respected
- [ ] Base stem always audible at volume 1.0 regardless of combat intensity
- [ ] Percussion stem activates when intensity >= threshold (default 0.2)
- [ ] Melody stem activates when intensity >= threshold (default 0.5)
- [ ] Intensity stem activates when intensity >= threshold (default 0.8)
- [ ] Stem volume transitions are smooth (no sudden cuts)
- [ ] Combat intensity: single COMBAT enemy nearby = high intensity
- [ ] Combat intensity: multiple CURIOUS enemies = gradual buildup
- [ ] Combat intensity: enemies leave range = intensity smoothly decreases
- [ ] Boss override: encounter trigger fires PlayMusic action, boss track starts
- [ ] Boss override: boss dies, zone track resumes after crossfade
- [ ] Boss override: overrides zone priority (boss music always wins)
- [ ] Stinger: level-up fanfare plays on level-up (via MusicStingerAPI)
- [ ] Stinger: quest complete sting plays on quest completion
- [ ] Stinger: death sting plays on player death
- [ ] Stinger: cooldown prevents stinger spam (3s default)
- [ ] Stinger: priority system -- death stinger interrupts discovery stinger
- [ ] Stinger: music ducks during stinger playback by configured dB amount
- [ ] Loop points: stems loop correctly at LoopStartSample/LoopEndSample boundaries
- [ ] Intro clip: plays once before looping stems begin
- [ ] Crossfade: old track fades out while new track fades in simultaneously
- [ ] `CombatMusicDuckSystem` reads `MusicState.IsInCombat` (not CombatState directly)
- [ ] `CombatMusicDuckSystem` fallback: works without MusicState singleton (backward compat)
- [ ] Remote client without AlertState access: falls back to binary CombatState intensity
- [ ] Listen server: full AlertState-based intensity works
- [ ] `AudioTelemetry` reports TrackTransitions, StingersPlayed, CombatIntensity, ActiveStemCount
- [ ] No new components on player entity (zero archetype impact)
- [ ] No ghost-replicated components added (all client-only)
- [ ] `TriggerActionType.PlayMusic = 16` added without breaking existing trigger actions 0-15
- [ ] Music Workstation: Track Browser lists all MusicTrackSO with stem preview
- [ ] Music Workstation: Stem Previewer plays 4 stems with intensity slider
- [ ] Music Workstation: Zone Mapper shows color-coded volumes in scene view
- [ ] Music Workstation: Stinger Tester plays stingers with priority/overlap testing
- [ ] Music Workstation: Live Debug shows real-time intensity, track, stem state
- [ ] Performance: all music systems combined < 0.15ms per client frame
- [ ] Memory: peak 2 tracks loaded during crossfade (~32 MB), 1 track steady state (~16 MB)
- [ ] No regression: existing combat audio, footsteps, occlusion, reverb unchanged
