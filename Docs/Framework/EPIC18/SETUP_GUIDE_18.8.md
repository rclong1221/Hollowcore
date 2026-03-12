# EPIC 18.8 Setup Guide: Audio Event System — Designer Layer

**Status:** Implemented
**Last Updated:** February 25, 2026
**Requires:** EPIC 15.27 (AudioSourcePool, AudioBusConfig, AudioMixer bus groups). Existing `AudioManager` in scene.

This guide covers the Unity Editor setup for the **Audio Event System** — the designer-facing abstraction layer for audio events, music state machines, and ambient soundscapes. This system sits on top of the existing ECS audio pipeline from EPIC 15.27 and provides ScriptableObject-driven workflows for all non-surface audio.

---

## Overview

The Audio Event System adds three designer-facing subsystems:

- **Audio Events** — `AudioEventSO` ScriptableObjects that encapsulate clip variations, randomization, cooldowns, bus routing, spatial settings, and fading into a single drag-and-drop asset
- **Music State Machine** — `MusicStateMachineSO` for defining music states (Explore, Combat, Boss, Victory) with crossfade transitions, playlist modes, and intensity layers (stems)
- **Ambient Soundscapes** — `AmbientSoundscapeSO` for defining layered ambient loops with time-of-day blending and volume variance, activated by trigger-volume zones

All three are managed through the **Audio Events** tab in the Audio Workstation window (`DIG > Audio Workstation`).

---

## Step 1: Scene Setup — AudioEventService

The `AudioEventService` is the central singleton that manages all audio events, music, and ambient audio at runtime.

### 1.1 Create the Service GameObject

1. In your persistent scene (alongside AudioManager / AudioSourcePool), create an empty GameObject named `AudioEventService`
2. Add the `AudioEventService` component

The service auto-creates child `MusicController` and `AmbientZoneManager` components if they are not already assigned. You can also wire them manually for more control.

| Field | Description | Default |
|-------|-------------|---------|
| **Music Controller** | Reference to a MusicController (auto-created if null) | None (auto) |
| **Ambient Zone Manager** | Reference to an AmbientZoneManager (auto-created if null) | None (auto) |

The service uses `DontDestroyOnLoad` — it persists across scene loads.

### 1.2 MusicController Configuration

If you need to configure the MusicController directly (e.g., to assign a mixer group or state machine at startup), expand the auto-created child or add your own:

| Field | Description | Default |
|-------|-------------|---------|
| **State Machine** | Reference to a `MusicStateMachineSO` asset | None |
| **Music Mixer Group** | AudioMixerGroup to route all music through. Falls back to the Music bus in AudioBusConfig if null | None (auto) |
| **Max Intensity Layers** | Number of pre-allocated intensity layer AudioSources | 4 |

The controller pre-allocates its AudioSources on Awake (2 crossfade sources + 1 stinger + N intensity layers). No runtime GameObject creation occurs during state transitions.

### 1.3 AmbientZoneManager Configuration

| Field | Description | Default |
|-------|-------------|---------|
| **Time Of Day** | Current hour (0-24) for day/night ambient blending. Set this from your weather/time system each frame | 12.0 |
| **Pool Size** | Number of pre-allocated ambient AudioSources | 12 |

The pool size should cover your maximum concurrent ambient layers across current + fading-out soundscapes. A typical worst case is 2 soundscapes crossfading with 4 layers each = 8 sources. Default of 12 provides headroom.

### 1.4 Scene Hierarchy

```
Scene Root
 ├── AudioManager                     (existing EPIC 5.1)
 ├── AudioFramework                   (existing EPIC 15.27)
 │   ├── AudioSourcePool
 │   ├── AudioClipBankHolder
 │   └── ...
 │
 ├── AudioEventService                ← NEW (EPIC 18.8)
 │   ├── MusicController              (auto-created child)
 │   │   ├── MusicSource_A            (auto-created, hidden)
 │   │   ├── MusicSource_B            (auto-created, hidden)
 │   │   ├── MusicStinger             (auto-created, hidden)
 │   │   └── MusicLayer_0..N          (auto-created, hidden)
 │   └── AmbientZoneManager           (auto-created child)
 │       └── AmbientSrc_0..N          (auto-created, hidden)
 │
 └── Environment
     ├── AmbientZone_Forest           (AmbientZoneAuthoring + BoxCollider trigger)
     ├── AmbientZone_Cave             (AmbientZoneAuthoring + SphereCollider trigger)
     └── ...
```

---

## Step 2: Create AudioEventSO Assets

AudioEventSO is the core designer asset. One asset = one logical sound (e.g., "DoorOpen", "FootstepWood", "UIClick", "AbilityFireball").

### 2.1 Create an Audio Event

**Create:** `Assets > Create > DIG/Audio/Audio Event`
**Recommended path:** `Assets/Audio/Events/` organized by category

### 2.2 Configure the Event

#### Identity

| Field | Description | Default |
|-------|-------------|---------|
| **Event Id** | Unique identifier (auto-populated from asset name) | Asset name |

#### Clips

| Field | Description | Default |
|-------|-------------|---------|
| **Clips** | Array of AudioClip variations. One is selected per play | Empty |
| **Selection Mode** | How clips are chosen from the array | Random |

Available selection modes:

| Mode | Behavior |
|------|----------|
| **Random** | Pure random selection |
| **Sequential** | Plays clips in order (0, 1, 2, 0, 1, 2...) |
| **Shuffle** | Plays all clips once in random order, then reshuffles |
| **RandomNoRepeat** | Random selection that never plays the same clip twice in a row |

#### Volume & Pitch

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Volume Min** | Minimum random volume | 0.8 | 0-1 |
| **Volume Max** | Maximum random volume | 1.0 | 0-1 |
| **Pitch Min** | Minimum random pitch | 0.95 | — |
| **Pitch Max** | Maximum random pitch | 1.05 | — |

Each play rolls a random value within these ranges for natural variation.

#### Bus & Priority

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Bus** | Audio bus routing (matches EPIC 15.27 buses) | Combat | Enum |
| **Priority** | Pool priority. Higher = survives eviction | 128 | 0-255 |

Use the same priority guidelines from EPIC 15.27:

| Priority | Use Case |
|----------|----------|
| 0-30 | Ambient loops, background |
| 30-60 | Footsteps, minor impacts |
| 60-100 | Weapon fire, abilities |
| 100-150 | Enemy attacks, important combat |
| 150-200 | Dialogue, critical feedback |
| 200-255 | Never culled (boss, player critical) |

#### Cooldown & Instances

| Field | Description | Default |
|-------|-------------|---------|
| **Cooldown** | Minimum seconds between plays of this event. Prevents machine-gun repeats | 0 (no cooldown) |
| **Max Instances** | Maximum concurrent plays of this event. 0 = unlimited | 0 |

**Example:** A rapid-fire weapon sound might use `Cooldown = 0.05` and `MaxInstances = 3`.

#### Spatial

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Spatial Blend** | 0 = 2D (UI/music), 1 = fully 3D positional | 1.0 | 0-1 |
| **Min Distance** | Distance at which volume is at full | 1.0 | 0.01+ |
| **Max Distance** | Distance beyond which the source is inaudible | 50.0 | 0.1+ |
| **Rolloff Mode** | Logarithmic, Linear, or Custom | Logarithmic | Enum |
| **Custom Rolloff** | AnimationCurve for custom falloff (only when Rolloff Mode = Custom) | None | — |

#### Looping & Fading

| Field | Description | Default |
|-------|-------------|---------|
| **Loop** | Loop the clip continuously until stopped | false |
| **Fade In** | Fade-in duration in seconds. 0 = instant | 0 |
| **Fade Out** | Fade-out duration when stopped via `Stop()` | 0 |

#### Environment

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Occlusion Enabled** | Apply occlusion filtering from EPIC 15.27 | true | — |
| **Reverb Send** | Reverb zone send level | 0.5 | 0-1 |

### 2.3 Example Configurations

| Event | Clips | Selection | Volume | Pitch | Bus | Priority | Cooldown | Spatial |
|-------|-------|-----------|--------|-------|-----|----------|----------|---------|
| DoorOpen | 3 variations | RandomNoRepeat | 0.8-1.0 | 0.95-1.05 | Ambient | 80 | 0.5 | 1.0 (3D) |
| UIButtonClick | 1 clip | Random | 0.9-1.0 | 1.0-1.0 | UI | 200 | 0.05 | 0.0 (2D) |
| ExplosionLarge | 5 variations | Random | 0.9-1.0 | 0.9-1.1 | Combat | 180 | 0 | 1.0 (3D) |
| AmbienceWind | 1 loop | Random | 0.4-0.6 | 0.98-1.02 | Ambient | 20 | 0 | 0.5 (partial 3D) |

---

## Step 3: Create MusicStateMachineSO Assets

The Music State Machine defines all music states, their playlists, transitions, and optional intensity layers (stems).

### 3.1 Create a Music State Machine

**Create:** `Assets > Create > DIG/Audio/Music State Machine`
**Recommended path:** `Assets/Audio/Music/MusicStateMachine.asset`

### 3.2 Configure the State Machine

| Field | Description | Default |
|-------|-------------|---------|
| **States** | Array of music states | Empty |
| **Default State** | State ID to enter on initialization | "Explore" |
| **Global Crossfade Duration** | Default crossfade time between states (seconds) | 2.0 |

### 3.3 Configure Each State

Each state in the `States` array has:

| Field | Description | Default |
|-------|-------------|---------|
| **State Id** | Unique string identifier (e.g., "Explore", "Combat", "Boss") | — |
| **Tracks** | AudioClip playlist for this state | Empty |
| **Mode** | Sequential, Shuffle, or Single | Sequential |
| **Volume** | Base volume for this state | 0.7 |
| **Crossfade In** | Override crossfade-in duration. 0 = use global | 0 |
| **Crossfade Out** | Override crossfade-out duration. 0 = use global | 0 |
| **Intensity Layers** | Optional intensity stem layers | Empty |
| **Transitions** | Outgoing transition rules | Empty |

**Playlist modes:**

| Mode | Behavior |
|------|----------|
| **Sequential** | Plays tracks in order, advancing when each finishes |
| **Shuffle** | Plays all tracks in random order, reshuffles when exhausted |
| **Single** | Loops the first track indefinitely |

### 3.4 Configure Intensity Layers (Optional)

Intensity layers are additional AudioClips (stems) that fade in/out based on a 0-1 intensity parameter. Use these for adaptive music that builds tension.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Layer Name** | Editor label (e.g., "Drums", "Strings", "Bass") | — | — |
| **Clip** | AudioClip for this layer (should match main track duration) | None | — |
| **Activate Threshold** | Intensity value (0-1) above which this layer fades in | 0.5 | 0-1 |
| **Fade Time** | Fade-in/out duration when layer activates/deactivates | 1.0 | 0.05+ |

**Example setup for a Combat state with 3 intensity levels:**

| Layer | Clip | Threshold | Fade | Effect |
|-------|------|-----------|------|--------|
| Percussion | combat_drums.wav | 0.2 | 1.0s | Light combat — drums kick in |
| Strings | combat_strings.wav | 0.5 | 1.5s | Medium combat — strings layer |
| Brass | combat_brass.wav | 0.8 | 2.0s | Intense combat — full orchestra |

At runtime, call `MusicController.SetIntensity(0.6f)` — Percussion and Strings play, Brass is silent.

### 3.5 Configure Transitions

Each state can define outgoing transitions triggered by named events.

| Field | Description | Default |
|-------|-------------|---------|
| **Trigger Event** | Event name that triggers this transition | — |
| **Target State Id** | State to transition to | — |
| **Crossfade Duration** | Override crossfade. 0 = use global | 0 |

**Example transition setup:**

| From State | Trigger Event | To State | Crossfade |
|------------|---------------|----------|-----------|
| Explore | "combat_start" | Combat | 1.5s |
| Combat | "combat_end" | Explore | 3.0s |
| Combat | "boss_encounter" | Boss | 2.0s |
| Boss | "boss_defeated" | Victory | 1.0s |
| Victory | "return_explore" | Explore | 4.0s |

Trigger transitions at runtime via `MusicController.FireEvent("combat_start")`.

### 3.6 Wire the State Machine

Assign the `MusicStateMachineSO` asset to the MusicController's **State Machine** field in the Inspector. The controller will enter the Default State automatically on Awake.

---

## Step 4: Create AmbientSoundscapeSO Assets

Ambient Soundscapes define layered ambient loops for environment zones (forest = birds + wind + leaves + creek).

### 4.1 Create an Ambient Soundscape

**Create:** `Assets > Create > DIG/Audio/Ambient Soundscape`
**Recommended path:** `Assets/Audio/Ambient/` organized by biome

### 4.2 Configure the Soundscape

| Field | Description | Default |
|-------|-------------|---------|
| **Soundscape Id** | Unique identifier | — |
| **Layers** | Array of ambient sound layers | Empty |
| **Crossfade Duration** | Zone transition crossfade time (seconds) | 3.0 |
| **Priority** | Zone priority. Higher overrides lower when overlapping | 0 |

### 4.3 Configure Each Layer

Each layer in the `Layers` array defines one ambient sound source:

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Layer Name** | Editor label (e.g., "Wind", "Birds", "Water") | — | — |
| **Clips** | Looping AudioClips. One selected randomly on zone enter | Empty | — |
| **Volume** | Base volume | 0.5 | 0-1 |
| **Volume Variance** | Random volume modulation per second (creates organic drift) | 0 | 0-0.2 |
| **Is 3D** | True = positioned at zone center. False = 2D background | false | — |

#### Time of Day Blend

Each layer has a `Day Blend` struct that scales volume by time of day:

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Dawn** | Volume multiplier during dawn (4:00-7:00) | 1.0 | 0-1 |
| **Day** | Volume multiplier during day (7:00-17:00) | 1.0 | 0-1 |
| **Dusk** | Volume multiplier during dusk (17:00-20:00) | 1.0 | 0-1 |
| **Night** | Volume multiplier during night (20:00-4:00) | 1.0 | 0-1 |

Transitions between time periods are interpolated smoothly.

**Example:** A "Birds" layer with `Dawn = 1.0, Day = 0.7, Dusk = 0.3, Night = 0.0` — birds are loud at dawn, moderate during day, quiet at dusk, silent at night.

#### Weather Blend

Each layer has a `Weather Blend` struct (for future weather system integration):

| Field | Description | Default |
|-------|-------------|---------|
| **Clear** | Volume multiplier during clear weather | 1.0 |
| **Rain** | Volume multiplier during rain | 1.0 |
| **Storm** | Volume multiplier during storm | 1.0 |
| **Snow** | Volume multiplier during snow | 1.0 |

### 4.4 Example Soundscape: Forest

| Layer | Clips | Volume | Variance | 3D | Dawn | Day | Dusk | Night |
|-------|-------|--------|----------|-----|------|-----|------|-------|
| Wind | wind_loop_01, wind_loop_02 | 0.4 | 0.05 | No | 0.8 | 0.6 | 0.9 | 1.0 |
| Birds | birds_loop_01, birds_loop_02, birds_loop_03 | 0.5 | 0.08 | No | 1.0 | 0.7 | 0.3 | 0.0 |
| Leaves | leaves_rustle_loop | 0.3 | 0.03 | No | 1.0 | 1.0 | 1.0 | 0.5 |
| Creek | creek_loop_01 | 0.35 | 0 | Yes | 1.0 | 1.0 | 1.0 | 1.0 |

---

## Step 5: Place Ambient Zones in the Scene

### 5.1 Create an Ambient Zone

1. Create an empty GameObject where you want the ambient zone
2. Add a **Collider** (Box or Sphere) and set **Is Trigger = true**
3. Add the `AmbientZoneAuthoring` component
4. Assign an `AmbientSoundscapeSO` to the **Soundscape** field

### 5.2 AmbientZoneAuthoring Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Soundscape** | The AmbientSoundscapeSO to activate when a player enters | None |
| **Gizmo Color** | Debug visualization color in Scene view | Light blue |

The zone's Priority is read from the assigned Soundscape asset.

### 5.3 Overlapping Zones

When the player is inside multiple ambient zones simultaneously, the **highest Priority** soundscape wins. When the player exits the top-priority zone, the system falls back to the next highest in the stack. If no zones are active, ambient audio fades to silence.

### 5.4 Detection Requirements

The trigger zone detects any collider with:
- The `"Player"` tag, **or**
- The `"Player"` layer

Ensure your player character's collider meets one of these conditions.

### 5.5 Gizmo Visualization

Ambient zones draw semi-transparent colored volumes in the Scene view:
- **Box colliders** render as filled + wireframe cubes
- **Sphere colliders** render as filled + wireframe spheres

The gizmo color is configurable per zone to help distinguish overlapping zones.

---

## Step 6: Audio Workstation — Audio Events Tab

The Audio Events tab is the first tab in the Audio Workstation window.

**Menu:** `DIG > Audio Workstation` → **Audio Events** tab

### 6.1 Sub-tabs

| Sub-tab | Purpose |
|---------|---------|
| **Audition** | Preview AudioEventSO clips in-editor without entering Play Mode |
| **Music** | Inspect MusicStateMachineSO states, transitions, and control playback at runtime |
| **Ambient** | Inspect AmbientSoundscapeSO layers and preview clips |
| **Monitor** | Live runtime dashboard — active instances, telemetry, pool stats |

### 6.2 Audition Tab

- Drag any `AudioEventSO` into the object field, or click "Select" from the asset list below
- View all event properties (bus, priority, volume range, cooldown, etc.)
- Click **Play** next to any individual clip variation to hear it
- Click **Play Event (Random)** to hear a random variation as the game would
- Click **Stop** to end playback
- Click **Refresh** to update the asset list after creating new events

### 6.3 Music Tab

- Assign a `MusicStateMachineSO` to inspect its states
- View each state's tracks, intensity layers, and transitions
- **In Play Mode:** Click **Transition To** to force a state change, and use the **Intensity** slider to test intensity layers in real-time

### 6.4 Ambient Tab

- Assign an `AmbientSoundscapeSO` to inspect its layers
- View layer configuration (volume, variance, time-of-day, clips)
- Click **Play** next to any clip to preview it

### 6.5 Monitor Tab (Play Mode Only)

Displays live runtime data:

| Section | Data Shown |
|---------|------------|
| **Active Instances** | Number of currently playing audio event instances |
| **Audio Telemetry** | Throttled events, playback failures |
| **Music** | Current state, intensity, playing status |
| **Ambient** | Active soundscape, time of day |
| **Pool** | Active AudioSources, peak count, evictions this frame |

---

## Step 7: Runtime Usage for Programmers

Designers configure the ScriptableObjects; programmers trigger them from gameplay code. The key APIs are:

### 7.1 Playing Audio Events

From any MonoBehaviour or system, reference an `AudioEventSO` field and call:

```csharp
// In a MonoBehaviour:
[SerializeField] private AudioEventSO doorOpenSound;

// Play at a world position
AudioEventHandle handle = doorOpenSound.Play(transform.position);

// Play as 2D (UI, music stinger)
AudioEventHandle handle = doorOpenSound.Play2D();

// Play attached to a transform (follows it)
AudioEventHandle handle = doorOpenSound.PlayAttached(enemyTransform);

// Stop with optional fade-out
doorOpenSound.Stop(handle, fadeOut: 0.5f);

// Check if still playing
bool playing = AudioEventService.Instance.IsPlaying(handle);
```

### 7.2 Music Control

```csharp
var music = AudioEventService.Instance.Music;

// Transition to a state
music.SetState("Combat");

// Fire a named event (triggers transitions defined in the SO)
music.FireEvent("boss_encounter");

// Set intensity for adaptive layers (0 = calm, 1 = intense)
music.SetIntensity(combatIntensity);

// Play a one-shot stinger on top of current music
music.PlayStinger(victoryStingerClip, volume: 0.8f);

// Fade all music to silence
music.FadeOut(duration: 3f);
```

### 7.3 Ambient Control

```csharp
var ambient = AudioEventService.Instance.Ambient;

// Force a soundscape (bypasses zone triggers — useful for cutscenes)
ambient.SetSoundscape(caveAmbience);

// Update time of day from your weather system
ambient.TimeOfDay = currentHour;

// Stop all ambient audio
ambient.StopAll();
```

---

## Step 8: Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Service exists | Enter Play Mode | `AudioEventService` singleton created, child MusicController and AmbientZoneManager visible |
| 3 | Workstation tab | Open DIG > Audio Workstation | "Audio Events" tab appears first in sidebar |
| 4 | Audition preview | Select an AudioEventSO, click Play | Clip plays in editor without entering Play Mode |
| 5 | Event playback | Call `audioEvent.Play(position)` at runtime | Sound plays at position with correct bus routing |
| 6 | Cooldown | Play an event with Cooldown > 0 rapidly | Subsequent plays blocked during cooldown |
| 7 | Max instances | Play an event with MaxInstances = 2 three times | Third play blocked |
| 8 | Music state | Assign MusicStateMachineSO, enter Play Mode | Default state music plays automatically |
| 9 | Music transition | Call `FireEvent("combat_start")` | Music crossfades to Combat state |
| 10 | Intensity layers | Call `SetIntensity(0.8f)` | Layers above 0.8 threshold fade in |
| 11 | Music stinger | Call `PlayStinger(clip)` | Stinger plays over current music without interruption |
| 12 | Music fade | Call `FadeOut(3f)` | All music fades linearly to silence over 3 seconds |
| 13 | Ambient zone | Walk into a zone with AmbientZoneAuthoring | Soundscape crossfades in with correct layers |
| 14 | Ambient exit | Walk out of all zones | Ambient audio crossfades to silence |
| 15 | Zone priority | Overlap two zones | Higher-priority soundscape plays |
| 16 | Time of day | Set `AmbientZoneManager.TimeOfDay = 22` | Night-configured layers adjust volume |
| 17 | Monitor tab | Open Monitor sub-tab in Play Mode | Active instances, pool stats, music state visible |
| 18 | Mixer routing | Music and intensity layers play | Audio appears in the Music bus in AudioMixer |
| 19 | No console errors | Play for 60 seconds with events, music, and ambient | No exceptions or warnings |

---

## Step 9: Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| `AudioEventService not found` log | Service not in scene | Add AudioEventService MonoBehaviour to a persistent GameObject |
| Events don't play | AudioSourcePool not in scene | Ensure EPIC 15.27 AudioSourcePool is set up per SETUP_GUIDE_15.27 |
| Events play but no bus routing | AudioBusConfig not wired to AudioSourcePool | Wire BusConfig on AudioSourcePool, assign MixerGroups per bus |
| Cooldown/MaxInstances not working | Using raw AudioSource instead of AudioEventSO.Play() | Always use the AudioEventSO convenience methods or AudioEventService |
| Music doesn't play on start | No MusicStateMachineSO assigned | Assign a MusicStateMachineSO to MusicController's State Machine field |
| Music not routed through mixer | Music Mixer Group field is null and no AudioBusConfig | Assign a Music AudioMixerGroup to MusicController, or ensure AudioBusConfig has a Music MixerGroup |
| Intensity layers silent | SetIntensity never called | Call `MusicController.SetIntensity()` from gameplay code |
| Intensity layers not matching track | Layer clips have different duration than main track | Ensure stem clips are the same length as the main track |
| Ambient zone not triggering | Collider not set to Is Trigger | Enable Is Trigger on the zone's Collider |
| Ambient zone not detecting player | Player missing tag or layer | Add `"Player"` tag or set layer to `"Player"` on the player's collider |
| Ambient pool exhausted warning | Too many simultaneous layers | Increase Pool Size on AmbientZoneManager (default 12) |
| Audition tab shows no events | No AudioEventSO assets in project | Create at least one via Assets > Create > DIG/Audio/Audio Event |
| Asset list stale in Audition | Cache not refreshed | Click the "Refresh" button in the Audition tab |
| Monitor tab blank | Not in Play Mode | Monitor requires Play Mode for live data |
| Music fade sounds wrong | Using old code with multiplicative fade | Ensure you have the latest MusicController (absolute interpolation) |

---

## Step 10: Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| AudioSourcePool, bus routing, occlusion, reverb zones | SETUP_GUIDE_15.27 |
| AudioBusConfig, AudioBusType | SETUP_GUIDE_15.27 Step 1.1 |
| AudioMixer setup (bus groups, exposed parameters) | SETUP_GUIDE_15.27 Step 2 |
| Combat music ducking | SETUP_GUIDE_15.27 Step 7.3 |
| Entity-linked audio (ECS AudioEmitter) | SETUP_GUIDE_15.27 Step 6 |
| **Audio Event System (this guide)** | **SETUP_GUIDE_18.8** |

---

## Step 11: File Reference

### Config (ScriptableObjects — create via Assets menu)

| File | Create Menu | Purpose |
|------|-------------|---------|
| `Assets/Scripts/Audio/Events/AudioEventSO.cs` | DIG/Audio/Audio Event | Audio event with clip variations, randomization, cooldown, spatial |
| `Assets/Scripts/Audio/Music/MusicStateMachineSO.cs` | DIG/Audio/Music State Machine | Music states, playlists, intensity layers, transitions |
| `Assets/Scripts/Audio/Ambient/AmbientSoundscapeSO.cs` | DIG/Audio/Ambient Soundscape | Layered ambient loops with day/night and weather blending |

### Runtime (MonoBehaviours — place in scene)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Audio/Events/AudioEventService.cs` | Central singleton — play events, manage music and ambient. DontDestroyOnLoad |
| `Assets/Scripts/Audio/Music/MusicController.cs` | Two-source crossfade engine, intensity layers, stinger playback |
| `Assets/Scripts/Audio/Ambient/AmbientZoneManager.cs` | Manages ambient zone stack, crossfades between soundscapes |
| `Assets/Scripts/Audio/Ambient/AmbientZoneAuthoring.cs` | Trigger volume that activates an ambient soundscape on player enter |

### Internal (no setup required)

| File | Purpose |
|------|---------|
| `Assets/Scripts/Audio/Events/AudioEventHandle.cs` | Lightweight handle struct for tracking playing instances |
| `Assets/Scripts/Audio/Events/AudioEventPlayer.cs` | Clip resolution, cooldown enforcement, fade management |

### Editor

| File | Purpose |
|------|---------|
| `Assets/Editor/AudioWorkstation/Modules/AudioEventModule.cs` | Audio Events tab — audition, music preview, ambient preview, monitor |
