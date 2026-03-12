# EPIC 18.8: Audio Event System — Designer Layer

**Status:** IMPLEMENTED
**Priority:** Medium-High (Audio polish drives game feel)
**Dependencies:**
- `AudioManager` (existing — `Audio.Systems`, `Assets/Scripts/Audio/AudioManager.cs`, surface-material-based footstep/impact playback, AudioMixer control, legacy pool + AudioSourcePool integration)
- `AudioSourcePool` / `AudioSourcePoolSystem` (existing — `Audio.Systems`, `Assets/Scripts/Audio/AudioSourcePool.cs`, bus-aware pool)
- `AudioClipBank` (existing — `Audio.Config`, `Assets/Scripts/Audio/Config/AudioClipBank.cs`, clip collections)
- `AudioBusConfig` / `AudioBusType` (existing — `Audio.Config`, `Assets/Scripts/Audio/Config/AudioBusConfig.cs`, bus routing)
- `AudioEmitterAuthoring` / `AudioEmitter` (existing — `Assets/Scripts/Audio/Authoring/AudioEmitterAuthoring.cs`, ECS audio emitters)
- `PlayAudioRequest` (existing — `Assets/Scripts/Audio/Components/PlayAudioRequest.cs`, ECS audio play request)
- `AudioOcclusionSystem` (existing — `Audio.Systems`, `Assets/Scripts/Audio/Systems/AudioOcclusionSystem.cs`, raycast-based occlusion)
- `AudioEnvironmentSystem` (existing — `Audio.Systems`, `Assets/Scripts/Audio/Systems/AudioEnvironmentSystem.cs`)
- `AudioPrioritySystem` (existing — `Audio.Systems`, `Assets/Scripts/Audio/Systems/AudioPrioritySystem.cs`)
- `ImpactAudioSystem` (existing — `Audio.Systems`, `Assets/Scripts/Audio/Systems/ImpactAudioSystem.cs`)
- `SurfaceMaterial` / `SurfaceMaterialRegistry` (existing — `Audio.Systems`, complete surface audio pipeline)
- `ReverbZoneAuthoring` / `AudioReverbZoneManager` (existing — `Audio.Zones`, reverb zones)
- `CombatMusicDuckSystem` (existing — `Audio.Systems`, music ducking during combat)
- `NetworkedAudioSystem` (existing — `Audio.Systems`, network-replicated audio)
- `AudioTelemetry` (existing — `Audio.Systems`, `Assets/Scripts/Audio/AudioTelemetry.cs`, debug counters)
- `AudioAccessibilityConfig` (existing — `Audio.Accessibility`, subtitle/radar settings)

**Feature:** A designer-facing audio event abstraction layer built on top of the existing ECS audio pipeline. Introduces `AudioEventSO` (a ScriptableObject representing a logical audio event with multiple clip variations, randomization, cooldowns, and spatial settings), a visual mixer routing editor, ambient soundscape zones, music state machine, and editor tooling for auditioning/previewing sounds without entering Play mode.

---

## Codebase Audit Findings

### What Already Exists

DIG has one of its most mature audio systems with 48 files covering:

| System | Status | Notes |
|--------|--------|-------|
| `AudioManager` | Working | Surface-material playback, variance, no-repeat, legacy + ECS pool |
| `AudioSourcePool` + `AudioSourcePoolSystem` | Fully implemented | Bus-aware pooling with priority eviction |
| `AudioClipBank` | Implemented | Simple clip collections |
| `AudioEmitterAuthoring` + `PlayAudioRequest` | Implemented | ECS audio request pipeline |
| `AudioOcclusionSystem` | Implemented | Raycast-based occlusion with profiles |
| `AudioPrioritySystem` | Implemented | Priority-based voice management |
| `ImpactAudioSystem` | Implemented | Physics impact sounds |
| `SurfaceMaterial` pipeline | Fully implemented | Per-surface footstep/impact clips with VFX |
| `ReverbZoneAuthoring` | Implemented | Reverb zone volumes |
| `CombatMusicDuckSystem` | Implemented | Music ducking during combat |
| `NetworkedAudioSystem` | Implemented | Replicated audio events |
| Accessibility (subtitles, sound radar) | Implemented | Directional subtitles, sound radar HUD |

### What's Missing

- **No AudioEventSO abstraction** — designers assign raw `AudioClip` references everywhere; no reusable "audio event" concept with built-in variation, cooldown, attenuation, and bus routing
- **No ambient soundscape system** — no way to define layered ambient loops per zone (forest = birds + wind + leaves + creek)
- **No music state machine** — `CombatMusicDuckSystem` ducks music but there's no system for transitioning between music tracks based on game state (exploration → combat → boss → victory)
- **No visual mixer routing** — bus routing is defined in code; designers cannot see or adjust the mixer graph
- **No editor audition tool** — cannot preview an audio event without entering Play mode
- **No 3D audio preview** — no Scene view visualization of audio ranges, falloff curves, occlusion
- **No music playlist** — no shuffled/sequential music track management
- **No one-shot event API** — playing a sound requires either raw AudioSource management or ECS component creation; no simple `AudioEventSO.Play(position)` API

---

## Problem

Despite having a robust ECS audio pipeline, DIG lacks a designer-friendly abstraction layer. Designers currently work with raw `AudioClip` arrays on components and `SurfaceMaterial` ScriptableObjects, but for non-surface-related audio (UI sounds, ability SFX, ambient loops, music transitions, narrative stingers), there's no standardized workflow. A designer wanting to add a "door opening" sound must understand `PlayAudioRequest` components, bus types, and pool configuration. The goal is a simple `AudioEventSO` that encapsulates all audio behavior in a single asset that can be dragged onto any component.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  AudioEventSO              AmbientSoundscapeSO      MusicStateMachineSO
  (clip variations,         (layered ambient loops    (states: Explore/Combat/Boss,
   randomization params,     per zone, crossfade,     transitions with crossfade,
   cooldown, bus, spatial,   time-of-day variants,    intensity layers,
   attenuation, priority)    weather modulation)       stinger triggers)
        |                       |                          |
        └──── AudioEventService (MonoBehaviour singleton) ─┘
              (plays AudioEventSOs, manages music state,
               drives ambient zones, provides simple API)
                         |
        ┌────────────────┼────────────────┐
        |                |                |
  AudioEventPlayer    AmbientZoneManager   MusicController
  (resolves clip from   (tracks player     (state machine,
   AudioEventSO,         position, blends   crossfade engine,
   routes to pool,       ambient layers,    intensity layers,
   enforces cooldown,    day/night mix)     stinger queue)
   handles variations)
        |                |                |
        └────────────────┼────────────────┘
                         |
              Existing ECS Audio Pipeline
              (AudioSourcePool, occlusion,
               priority, network sync — unchanged)
                         |
                 EDITOR TOOLING
                         |
  AudioEventWorkstationModule ── audition & preview
  (play events in editor, visualize 3D ranges,
   mixer routing diagram, ambient zone preview,
   music state machine viewer)
```

---

## Core Types

### AudioEventSO

**File:** `Assets/Scripts/Audio/Events/AudioEventSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| EventId | string | "" | Unique identifier |
| DisplayName | string | "" | Editor label |
| Clips | AudioClip[] | empty | Clip variations |
| SelectionMode | ClipSelection enum | Random | Random, Sequential, Shuffle, RandomNoRepeat |
| Volume | RangeFloat | (0.8, 1.0) | Random volume range |
| Pitch | RangeFloat | (0.95, 1.05) | Random pitch range |
| Bus | AudioBusType | SFX | Audio bus routing |
| Priority | byte | 128 | Pool priority (0=lowest, 255=highest) |
| Cooldown | float | 0 | Minimum seconds between plays |
| MaxInstances | int | 0 | 0 = unlimited concurrent |
| SpatialBlend | float [0-1] | 1.0 | 0 = 2D, 1 = 3D |
| MinDistance | float | 1 | 3D rolloff min distance |
| MaxDistance | float | 50 | 3D rolloff max distance |
| RolloffMode | AudioRolloffMode | Logarithmic | Distance rolloff curve |
| CustomRolloff | AnimationCurve | null | Custom falloff curve |
| Loop | bool | false | Loop the clip |
| FadeIn | float | 0 | Fade-in duration |
| FadeOut | float | 0 | Fade-out duration |
| OcclusionEnabled | bool | true | Apply occlusion filtering |
| ReverbSend | float [0-1] | 0.5 | Reverb zone send level |

### AudioEventSO API

```csharp
public AudioEventHandle Play(Vector3 position);
public AudioEventHandle Play2D();
public AudioEventHandle PlayAttached(Transform parent);
public void Stop(AudioEventHandle handle, float fadeOut = 0);
public bool IsPlaying(AudioEventHandle handle);
```

The `Play()` methods are convenience wrappers that route through `AudioEventService`.

---

## Music System

### MusicStateMachineSO

**File:** `Assets/Scripts/Audio/Music/MusicStateMachineSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| States | MusicState[] | empty | Music states |
| DefaultState | string | "Explore" | Initial state |
| GlobalCrossfadeDuration | float | 2.0 | Default crossfade time |

### MusicState

```csharp
[Serializable]
public class MusicState
{
    public string StateId;
    public AudioClip[] Tracks;          // Playlist for this state
    public PlaylistMode Mode;           // Sequential, Shuffle, Single
    public float Volume;                // State volume
    public float CrossfadeIn;           // Override crossfade in
    public float CrossfadeOut;          // Override crossfade out
    public MusicLayer[] IntensityLayers; // Optional intensity layers
    public MusicTransition[] Transitions; // Outgoing transitions
}
```

### MusicLayer (Intensity Stacking)

```csharp
[Serializable]
public class MusicLayer
{
    public string LayerName;        // "Bass", "Drums", "Strings"
    public AudioClip Clip;          // Layer audio
    public float ActivateThreshold; // Intensity 0-1 above which layer fades in
    public float FadeTime;          // Layer crossfade duration
}
```

### MusicController

**File:** `Assets/Scripts/Audio/Music/MusicController.cs`

- Drives `MusicStateMachineSO` at runtime
- API:
  - `SetState(string stateId)` — transition to music state
  - `SetIntensity(float intensity)` — control intensity layers (0=calm, 1=intense)
  - `PlayStinger(AudioClip stinger)` — one-shot layered stinger (doesn't interrupt main music)
  - `FadeOut(float duration)` — fade all music to silence
- Crossfade engine: two AudioSources pingpong, one fading out while other fades in
- Intensity layers: additional AudioSources per layer, volume driven by intensity parameter

---

## Ambient Soundscape

### AmbientSoundscapeSO

**File:** `Assets/Scripts/Audio/Ambient/AmbientSoundscapeSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| SoundscapeId | string | "" | Unique identifier |
| Layers | AmbientLayer[] | empty | Ambient sound layers |
| CrossfadeDuration | float | 3.0 | Zone transition crossfade |

### AmbientLayer

```csharp
[Serializable]
public class AmbientLayer
{
    public string LayerName;           // "Wind", "Birds", "Water"
    public AudioClip[] Clips;         // Looping clips (random selection)
    public float Volume;              // Base volume
    public float VolumeVariance;      // Random volume modulation
    public TimeOfDayBlend DayBlend;   // Volume multiplier by time of day
    public WeatherBlend WeatherBlend; // Volume multiplier by weather
    public bool Is3D;                 // True = positioned, False = 2D ambient
}
```

### AmbientZoneAuthoring

**File:** `Assets/Scripts/Audio/Ambient/AmbientZoneAuthoring.cs`

- MonoBehaviour with trigger collider
- References `AmbientSoundscapeSO`
- When player enters zone, crossfades to this soundscape
- Supports overlapping zones with priority blending

---

## Editor Tooling

### AudioEventWorkstationModule

**File:** `Assets/Editor/AudioWorkstation/Modules/AudioEventWorkstationModule.cs`

- **Audition Panel:** Select any `AudioEventSO` and click "Play" to hear it in-editor (no Play mode needed)
- **Variation Preview:** See all clip variations, weights, and hear each individually
- **3D Range Visualizer:** Scene view overlay showing min/max distance spheres for selected emitter
- **Mixer Routing Diagram:** Visual graph of bus routing (SFX → SubMix → Master, etc.)
- **Music State Machine Viewer:** Visual state graph of `MusicStateMachineSO` with current state highlight
- **Ambient Zone Preview:** Toggle ambient zones on/off in Scene view with volume visualization
- **Cooldown Monitor:** Live view of active cooldowns per AudioEventSO

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Audio/Events/AudioEventSO.cs` | ScriptableObject | ~100 |
| `Assets/Scripts/Audio/Events/AudioEventService.cs` | MonoBehaviour | ~200 |
| `Assets/Scripts/Audio/Events/AudioEventPlayer.cs` | Class | ~150 |
| `Assets/Scripts/Audio/Events/AudioEventHandle.cs` | Struct | ~20 |
| `Assets/Scripts/Audio/Music/MusicStateMachineSO.cs` | ScriptableObject | ~50 |
| `Assets/Scripts/Audio/Music/MusicController.cs` | MonoBehaviour | ~200 |
| `Assets/Scripts/Audio/Ambient/AmbientSoundscapeSO.cs` | ScriptableObject | ~40 |
| `Assets/Scripts/Audio/Ambient/AmbientZoneAuthoring.cs` | MonoBehaviour | ~80 |
| `Assets/Scripts/Audio/Ambient/AmbientZoneManager.cs` | MonoBehaviour | ~120 |
| `Assets/Editor/AudioWorkstation/Modules/AudioEventWorkstationModule.cs` | Editor | ~300 |

**Total estimated:** ~1,260 lines

---

## Performance Considerations

- `AudioEventSO.Play()` resolves to a single `AudioSourcePool.Acquire()` call — same performance as existing raw AudioSource usage
- Cooldown tracking uses `Dictionary<int, float>` with event hash — O(1) lookup
- Music crossfade uses exactly 2 AudioSources (pingpong) — no pool allocation
- Intensity layers use pre-allocated AudioSources (max 4 per state) — no runtime allocation
- Ambient zone crossfade blends volume parameters only — no AudioSource creation/destruction
- All systems are managed (MonoBehaviour) — no impact on ECS performance

---

## Testing Strategy

- Unit test AudioEventSO clip selection: Random, Sequential, Shuffle, RandomNoRepeat
- Unit test cooldown: play event → verify blocked during cooldown → verify allowed after
- Unit test max instances: exceed MaxInstances → verify oldest stopped
- Integration test: MusicController state transition → verify crossfade
- Integration test: ambient zone entry → verify soundscape crossfade
- Integration test: AudioEventSO.Play(position) → verify 3D spatialization
- Editor test: audition panel plays sound in edit mode
