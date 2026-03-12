# EPIC 18.10: Replay & Spectator System

**Status:** PLANNED
**Priority:** Medium (Competitive games, content creation, bug reproduction)
**Dependencies:**
- `GhostOwner` / NetworkId (existing — Unity NetCode, player identity)
- `CharacterAttributes` (existing — `DIG.Combat.Components`, Ghost replicated, HP/stats)
- `PlayerProgression` (existing — `DIG.Progression.Components`, XP/level)
- `LocalTransform` (existing — Unity Transforms, entity positions)
- `PhysicsVelocity` (existing — Unity Physics)
- `InputSchemeState` (existing — `DIG.Core.Input`, input state)
- Cinemachine (existing — camera system)
- Unity NetCode (`Unity.NetCode`)
- Unity Entities (`Unity.Entities`)

**Feature:** A server-authoritative replay recording system that captures ECS game state snapshots at configurable intervals, serializes them to a compact binary format, and provides a playback engine with timeline scrubbing, speed control, and multiple camera modes. Includes a spectator mode for live games with free-cam, player-follow, and cinematic orbit cameras. Designed for competitive analysis, content creation, and bug reproduction.

---

## Codebase Audit Findings

### What Already Exists

| System | Status | Notes |
|--------|--------|-------|
| NetCode Ghost snapshots | Fully implemented | Server already serializes entity state per tick for network replication — this is the data source for replay |
| Cinemachine | Fully implemented | Camera system with virtual cameras, blending — reusable for spectator cameras |
| `LocalTransform` on all entities | Standard ECS | Position/rotation data available |
| `CharacterAttributes` | Ghost-replicated | HP, stats, level — available for HUD overlay in replay |

### What's Missing

- **No replay recording** — NetCode snapshots are network-only; nothing writes them to disk
- **No replay file format** — no binary format for storing game state over time
- **No playback engine** — no system to deserialize and apply recorded state
- **No timeline UI** — no scrub bar, play/pause, speed controls
- **No spectator camera system** — no free-cam, follow-cam, or orbit-cam for observers
- **No kill-cam** — no automatic camera replay of kills/deaths
- **No replay compression** — raw state snapshots would be enormous without delta encoding

---

## Problem

Players, content creators, and developers need to review gameplay after it happens. Competitive games need kill-cams and match replays. Content creators need cinematic replay cameras for highlight videos. QA needs to reproduce bugs by replaying the exact game state. Currently, none of this is possible — game state exists only in-memory during the session and is lost when it ends.

---

## Architecture Overview

```
                    RECORDING LAYER
  ReplayRecorder (ServerSimulation SystemGroup)
  (captures selected component data at configurable tick interval,
   delta-encodes against previous snapshot, writes to ring buffer)
        |
  ReplaySerializer
  (flushes ring buffer to disk as .digreplay binary file,
   async I/O via background thread — follows SaveFileWriter pattern)
        |
                    PLAYBACK LAYER
  ReplayPlayer (MonoBehaviour)
  (loads .digreplay file, deserializes snapshots,
   interpolates between keyframes, drives entity transforms)
        |
  ReplayTimelineController
  (play, pause, rewind, scrub, speed control,
   frame-accurate stepping, bookmark system)
        |
  ReplayCamera (Cinemachine-based)
  (free-cam, follow-player, orbit, cinematic rail,
   auto kill-cam, smooth transitions between modes)
        |
                    SPECTATOR LAYER
  SpectatorSystem (NetCode ClientSimulation)
  (connects as observer — receives ghost snapshots
   without owning a player entity, drives spectator cameras)
        |
                    UI & EDITOR
  ReplayTimelineView (UI Toolkit)
  (timeline bar, play/pause, speed, player list,
   event markers for kills/deaths/objectives)
        |
  ReplayWorkstationModule (Editor)
  (replay file browser, metadata inspector,
   recording config, camera preset editor)
```

---

## Replay Recording

### What Gets Recorded

Only essential gameplay state — NOT every component:

| Component | Frequency | Delta-Encoded | Purpose |
|-----------|-----------|---------------|---------|
| `LocalTransform` | Every tick | Yes (position delta) | Entity positions and rotations |
| `PhysicsVelocity` | Every 5 ticks | Yes | Movement prediction for interpolation |
| `CharacterAttributes` | On change | Full snapshot | HP, stats for HUD overlay |
| Input buffer (GhostInput) | Every tick | Yes | Player inputs for deterministic replay |
| Custom events | On fire | Full | Kills, deaths, abilities, chat |

### ReplayRecorder

**File:** `Assets/Scripts/Replay/Recording/ReplayRecorder.cs`

- ISystem, `SimulationSystemGroup`, `ServerSimulation`
- Configurable via `ReplayConfigSO`:
  - Recording enabled/disabled toggle
  - Tick interval (default: every tick for competitive, every 3 ticks for casual)
  - Max recording duration (default: 30 minutes)
  - Component filter (which components to capture)
- Delta encoding: stores only changed values from previous snapshot
- Ring buffer: 60 seconds of uncompressed data in memory (configurable)
- Flush trigger: every 10 seconds, background thread serializes buffer to disk

### Replay File Format (.digreplay)

```
Header (64 bytes):
  Magic          : uint32    // "DIGR" = 0x52474944
  Version        : uint16    // Format version
  TickRate       : uint16    // Server tick rate
  StartTimestamp : int64     // Unix epoch start time
  Duration       : float32   // Total duration in seconds
  TotalFrames    : uint32    // Total snapshot frames
  EntityCount    : uint16    // Peak entity count
  PlayerCount    : byte      // Player count
  MapHash        : uint32    // Map identifier
  Reserved       : bytes[21] // Future use

Per-Frame:
  FrameHeader:
    Tick           : uint32
    DeltaFlags     : bitfield  // Which entities changed
    EventCount     : uint16    // Number of events this frame

  EntityDeltas[]:
    EntityId       : uint16
    ComponentMask  : uint16    // Which components changed
    ComponentData  : variable  // Only changed component bytes

  Events[]:
    EventType      : byte
    Timestamp      : uint32
    Payload        : variable
```

Estimated file size: ~2-5 MB per 10 minutes of 8-player gameplay (with delta encoding).

---

## Replay Playback

### ReplayPlayer

**File:** `Assets/Scripts/Replay/Playback/ReplayPlayer.cs`

- MonoBehaviour, creates a local "replay world" with visual-only entities
- Reads `.digreplay` file, deserializes frame by frame
- Interpolates entity positions between keyframes for smooth playback
- No physics simulation — positions are applied directly from recorded data
- API:
  - `void Load(string filePath)` — load replay file
  - `void Play()` / `void Pause()` / `void Stop()`
  - `void Seek(float normalizedTime)` — scrub to position (0-1)
  - `void SetSpeed(float speed)` — 0.25x, 0.5x, 1x, 2x, 4x
  - `void StepForward()` / `void StepBackward()` — frame-by-frame
  - `float CurrentTime` / `float Duration`
  - `ReplayEvent[] GetEvents()` — all events (kills, deaths) for timeline markers

### Frame Interpolation

```
Given: Frame at tick 100 and frame at tick 103 (3-tick interval)
Playback at tick 101:
  t = (101 - 100) / (103 - 100) = 0.333
  position = lerp(frame100.pos, frame103.pos, t)
  rotation = slerp(frame100.rot, frame103.rot, t)
```

---

## Spectator System

### SpectatorSystem

**File:** `Assets/Scripts/Replay/Spectator/SpectatorSystem.cs`

- NetCode client connects as "spectator" (no player entity spawned)
- Receives all ghost snapshots (full world view)
- Drives spectator cameras using received entity transforms
- Can switch between any player's perspective

### SpectatorCamera

**File:** `Assets/Scripts/Replay/Spectator/SpectatorCamera.cs`

- Cinemachine-based camera controller with multiple modes:
  - **Free Cam:** WASD + mouse fly camera, no clipping
  - **Follow Player:** Third-person follow with orbit, switches between players with number keys
  - **First Person:** Locks to player's view
  - **Cinematic Orbit:** Smooth auto-orbit around action focus point
  - **Kill Cam:** Auto-triggered slow-motion replay of last N seconds around a kill event
- Mode switching: Tab cycles modes, number keys select players
- Smooth blending between modes via Cinemachine blending

---

## ScriptableObjects

### ReplayConfigSO

**File:** `Assets/Scripts/Replay/Config/ReplayConfigSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| RecordingEnabled | bool | true | Master toggle |
| TickInterval | int | 1 | Record every N ticks |
| MaxDurationMinutes | float | 30 | Maximum recording length |
| RingBufferSeconds | float | 60 | In-memory buffer size |
| FlushIntervalSeconds | float | 10 | Disk write interval |
| DeltaEncoding | bool | true | Enable delta compression |
| SavePath | string | "Replays/" | Subdirectory in persistentDataPath |
| AutoRecord | bool | false | Start recording automatically |
| RecordedComponents | ComponentFilter[] | defaults | Which components to capture |

### CameraPresetSO

**File:** `Assets/Scripts/Replay/Config/CameraPresetSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| PresetName | string | "" | Display name |
| Mode | SpectatorCameraMode | FreeCam | Camera mode |
| MoveSpeed | float | 10 | Free cam movement speed |
| FollowDistance | float | 5 | Follow cam distance |
| FollowHeight | float | 2 | Follow cam height |
| OrbitSpeed | float | 15 | Orbit rotation speed |
| FOV | float | 90 | Camera field of view |
| SmoothTime | float | 0.1 | Damping factor |

---

## UI

### ReplayTimelineView

**File:** `Assets/Scripts/Replay/UI/ReplayTimelineView.cs`

- Timeline scrub bar with frame markers
- Event markers on timeline (kills = red, deaths = skull, objectives = star)
- Play/Pause/Stop buttons
- Speed selector: 0.25x, 0.5x, 1x, 2x, 4x
- Frame counter display
- Player list with camera-follow buttons
- Minimap with entity positions

---

## Editor Tooling

### ReplayWorkstationModule

**File:** `Assets/Editor/ReplayWorkstation/Modules/ReplayWorkstationModule.cs`

- **Replay Browser:** List .digreplay files with metadata (date, duration, players, map)
- **Recording Config:** Edit ReplayConfigSO, toggle recording
- **Camera Presets:** Manage CameraPresetSO collection
- **Replay Inspector:** Open replay in editor, view frame-by-frame data
- **File Size Estimator:** Estimate file size based on config and entity count

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Replay/Recording/ReplayRecorder.cs` | ISystem | ~250 |
| `Assets/Scripts/Replay/Recording/ReplaySerializer.cs` | Class | ~200 |
| `Assets/Scripts/Replay/Recording/DeltaEncoder.cs` | Class | ~100 |
| `Assets/Scripts/Replay/Playback/ReplayPlayer.cs` | MonoBehaviour | ~250 |
| `Assets/Scripts/Replay/Playback/FrameInterpolator.cs` | Class | ~80 |
| `Assets/Scripts/Replay/Playback/ReplayTimelineController.cs` | Class | ~100 |
| `Assets/Scripts/Replay/Spectator/SpectatorSystem.cs` | SystemBase | ~100 |
| `Assets/Scripts/Replay/Spectator/SpectatorCamera.cs` | MonoBehaviour | ~200 |
| `Assets/Scripts/Replay/Config/ReplayConfigSO.cs` | ScriptableObject | ~35 |
| `Assets/Scripts/Replay/Config/CameraPresetSO.cs` | ScriptableObject | ~30 |
| `Assets/Scripts/Replay/Data/ReplayFileFormat.cs` | Structs | ~60 |
| `Assets/Scripts/Replay/UI/ReplayTimelineView.cs` | UIView | ~150 |
| `Assets/Editor/ReplayWorkstation/Modules/ReplayWorkstationModule.cs` | Editor | ~200 |

**Total estimated:** ~1,755 lines

---

## Performance Considerations

- Recording runs on server only — zero client overhead
- Delta encoding reduces data by ~70-90% (most entities don't move every tick)
- Disk writes are async via background thread — zero main thread I/O cost
- Ring buffer prevents unbounded memory growth — caps at configured seconds
- Playback uses pure transform interpolation — no physics simulation cost
- Spectator mode uses standard NetCode ghost receiving — same cost as a regular client minus player input
- File format designed for sequential read (no random access needed for normal playback)

---

## Testing Strategy

- Unit test delta encoding: encode → decode → verify lossless
- Unit test frame interpolation: verify lerp/slerp accuracy at various t values
- Unit test replay file read/write: serialize → deserialize → compare frame data
- Integration test: record 30 seconds → play back → verify entity positions match
- Integration test: spectator connects → verify receives all entity data
- Integration test: seek to arbitrary position → verify correct frame displayed
- Integration test: camera mode switching → verify smooth transitions
- Editor test: replay browser lists files with correct metadata
