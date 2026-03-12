# EPIC 18.10: Replay & Spectator System — Setup Guide

## Prerequisites

- Unity 2022.3+ with Entities, NetCode, Physics, and Input System packages installed
- DIG project with existing ECS infrastructure and NetCode multiplayer

---

## 1. Create the Replay Config Asset

The recording system loads its configuration from a `ReplayConfigSO` ScriptableObject in a `Resources/` folder.

1. In the Project window, right-click **Assets/Resources/** (create the folder if it doesn't exist)
2. Select **Create > DIG > Replay > Replay Config**
3. Name the asset **ReplayConfig** (this exact name is required for auto-loading)

> Alternatively, open **DIG > Replay Workstation** and click **Create Default Config** in the Recording Config tab — this creates the asset automatically.

### Config Settings

| Field | Default | Description |
|-------|---------|-------------|
| **Recording Enabled** | `true` | Master toggle — disables all recording when off |
| **Auto Record** | `false` | Start recording automatically when ghost entities appear |
| **Tick Interval** | `1` | Record every N server ticks (1 = every tick, higher = smaller files) |
| **Max Duration Minutes** | `30` | Recording stops automatically after this many minutes |
| **Ring Buffer Seconds** | `60` | Seconds of data held in memory before flush |
| **Flush Interval Seconds** | `10` | How often the ring buffer flushes to disk |
| **Delta Encoding** | `true` | Only store changed entities between keyframes (70-80% file size reduction) |
| **Keyframe Interval** | `60` | Full snapshot every N frames (enables seeking — lower = faster seeks, larger files) |
| **Save Subdirectory** | `Replays` | Folder under `Application.persistentDataPath` for .digreplay files |
| **Kill Cam Buffer Seconds** | `5` | Seconds of gameplay to replay for kill-cam |
| **Kill Cam Playback Speed** | `0.25` | Slow-motion multiplier for kill-cam |

### File Size Estimation

The Recording Config tab in the Replay Workstation includes a file size estimator. Enter your expected entity count to see projected file sizes based on your current settings.

**Rule of thumb:** With delta encoding ON, 50 entities, tick interval 1, a 10-minute recording is roughly 2-4 MB.

---

## 2. Create Camera Preset Assets

Camera presets define behavior for each spectator camera mode. You can create multiple presets and swap between them.

1. Right-click in the Project window
2. Select **Create > DIG > Replay > Camera Preset**
3. Configure the preset in the Inspector

> You can also create presets from **DIG > Replay Workstation > Camera Presets** tab.

### Preset Fields

| Section | Field | Default | Description |
|---------|-------|---------|-------------|
| **General** | Preset Name | `Default` | Display name for the preset |
| | Mode | `FreeCam` | Default camera mode for this preset |
| | FOV | `90` | Field of view in degrees |
| | Near Clip | `0.1` | Near clipping plane distance |
| **Free Cam** | Move Speed | `10` | WASD movement speed (units/sec) |
| | Fast Move Multiplier | `3` | Speed multiplier when holding Shift |
| | Mouse Sensitivity | `2` | Mouse look sensitivity |
| **Follow** | Follow Distance | `5` | 3rd-person camera distance from target |
| | Follow Height | `2` | Camera height offset above target |
| | Follow Smooth Time | `0.15` | SmoothDamp time — lower = snappier |
| **Orbit** | Orbit Speed | `15` | Auto-orbit rotation speed (degrees/sec) |
| | Orbit Radius | `8` | Orbit distance from center |
| | Orbit Height | `3` | Orbit camera height |

---

## 3. Scene Setup — Recording

Recording happens automatically via the ECS system. No scene objects are needed.

- `ReplayRecorderSystem` runs in **ServerSimulation** and **LocalSimulation** worlds
- It auto-discovers the ReplayConfig asset from `Resources/ReplayConfig`
- If **Auto Record** is enabled, recording starts when the first ghost entity appears

**For manual recording control**, call from any system or MonoBehaviour:

```csharp
// Get the system from the server/local world
var recorder = World.DefaultGameObjectInjectionWorld
    .GetExistingSystemManaged<DIG.Replay.ReplayRecorderSystem>();

recorder.StartRecording();  // Begin recording
recorder.StopRecording();   // Stop and finalize the .digreplay file
```

### Recorded Files

Replay files are saved to:
```
{Application.persistentDataPath}/{SaveSubdirectory}/replay_YYYYMMDD_HHMMSS.digreplay
```

On Windows: `%USERPROFILE%/AppData/LocalLow/{CompanyName}/{ProductName}/Replays/`
On macOS: `~/Library/Application Support/{CompanyName}/{ProductName}/Replays/`

---

## 4. Scene Setup — Playback

To enable replay playback, add a persistent GameObject to your boot scene:

1. Create an empty GameObject, name it `[ReplaySystem]`
2. Add these three components:
   - **ReplayPlayer** — loads and plays .digreplay files
   - **ReplayTimelineController** — manages play/pause/speed state
   - **ReplayTimelineView** — creates the timeline UI overlay

The GameObject auto-calls `DontDestroyOnLoad` and persists across scene loads.

### Optional: Entity Proxy Prefab

By default, replayed entities appear as Unity capsule primitives. To use a custom visual:

1. Create a prefab for the replay entity proxy (e.g., a simple mesh with a material)
2. Assign it to **ReplayPlayer > Entity Proxy Prefab** in the Inspector

> Each recorded entity gets one instance of this prefab. Its transform is driven by the replay data.

### Speed Presets

The `ReplayTimelineController` has a **Speed Presets** array in the Inspector. Default values: `0.25x, 0.5x, 1x, 2x, 4x`. Add or modify entries to customize available playback speeds.

---

## 5. Scene Setup — Spectator Camera

For live spectator mode or replay camera control:

1. On the same `[ReplaySystem]` GameObject (or a separate persistent one), add:
   - **SpectatorCamera** — multi-mode camera controller

2. Assign the fields in the Inspector:
   - **Default Preset** — drag a `CameraPresetSO` asset here (optional — uses built-in defaults if empty)
   - **Camera** — assign the scene camera (auto-finds `Camera.main` if left empty)

### Camera Modes

| Mode | Behavior | Controls |
|------|----------|----------|
| **FreeCam** | Fly camera with WASD + mouse look | W/A/S/D = move, Q/E = down/up, Mouse = look, Shift = fast |
| **FollowPlayer** | 3rd-person orbit around followed entity | Mouse X = orbit angle, auto-smoothed |
| **FirstPerson** | Locked to entity position at eye-level | No manual control — follows entity rotation |
| **Orbit** | Auto-rotates around followed entity or world center | Automatic rotation, configurable speed |
| **KillCam** | Slow-motion orbit around kill position | Triggered automatically, returns to previous mode |

---

## 6. Spectator Mode — Multiplayer Setup

To connect as a spectator (observer-only, no player entity spawned):

1. Before connecting to the server, set the spectator flag:
   ```csharp
   GameBootstrap.IsSpectatorMode = true;
   ```
2. Connect normally — the client sends a `SpectatorJoinRequest` instead of `GoInGameRequest`
3. The server marks the connection with `SpectatorTag` and does **not** spawn a player entity
4. The client receives all ghost snapshots and can view the game via the spectator camera

### Spectator Controls (Live)

| Key | Action |
|-----|--------|
| **Tab** | Cycle camera mode (FreeCam → Follow → FirstPerson → Orbit) |
| **1-9** | Follow specific player (by ghost entity order) |
| **WASD** | Move (FreeCam mode) |
| **Mouse** | Look / orbit |
| **Shift** | Fast movement (FreeCam mode) |

---

## 6b. Death Spectator Mode — Automatic

When a player dies in multiplayer, they automatically enter a spectator view to watch remaining alive players. **No setup required** — the `DeathSpectatorTransitionSystem` handles this automatically if the `[ReplaySystem]` prefab is in the scene.

### How It Works

1. Local player's `DeathState.Phase` transitions to `Dead` or `Downed`
2. The system enables `SpectatorCamera` and switches to **FollowPlayer** mode
3. The camera follows the first alive player; use controls below to switch
4. When the local player respawns (`DeathState.Phase` → `Alive`), spectator mode exits and the normal camera resumes

### Death Spectator Controls

| Key | Action |
|-----|--------|
| **Tab** | Cycle camera mode (FreeCam → Follow → FirstPerson → Orbit) |
| **1-9** | Follow specific alive player |
| **WASD** | Move (FreeCam mode) |
| **Mouse** | Look / orbit |
| **Shift** | Fast movement (FreeCam mode) |

### Integration Notes

- The system reads `DeathState` (replicated to all clients) and `GhostOwnerIsLocal` to detect local player death
- Dead/downed players are excluded from the alive player list
- `SpectatorCamera.enabled` is toggled: **enabled** during death spectator, **disabled** on respawn so the normal player camera regains control
- Other camera systems should check `DeathSpectatorTransitionSystem.IsDeathSpectating` to avoid fighting over the camera transform

---

## 7. Replay Timeline UI

The timeline overlay appears at the bottom of the screen during replay playback.

### Controls

| Element | Description |
|---------|-------------|
| **Timeline slider** | Drag to scrub to any point in the replay |
| **<< / >>** | Step one frame backward / forward (pauses playback) |
| **Play / Pause** | Toggle playback |
| **- / +** | Cycle playback speed down / up through presets |
| **Speed label** | Shows current speed (e.g., "1.00x") |
| **Time label** | Current playback time (MM:SS) |
| **Frame label** | Current frame / total frames |
| **Free / Follow / FP / Orbit** | Switch camera mode |
| **Player list** | Click a player name to follow them with the camera |

### Event Markers

Colored markers appear on the timeline for key events:

| Color | Event |
|-------|-------|
| Red | Kill |
| Gray | Death |
| Gold | Objective |

---

## 8. Editor Tooling — Replay Workstation

Open **DIG > Replay Workstation** from the menu bar. The window has three tabs:

### Browser Tab

- Lists all `.digreplay` files in the Replays directory, sorted by date
- Shows file metadata: size, last modified, version, duration, frame count, entity count, player count
- **Refresh** — re-scan the replay directory
- **Open Folder** — reveal the Replays directory in Finder/Explorer
- **Delete** — remove a replay file (with confirmation dialog)

### Recording Config Tab

- Displays and edits the `ReplayConfigSO` asset (all serialized properties)
- **Create Default Config** — creates a new `ReplayConfig.asset` in `Assets/Resources/` if none exists
- **File Size Estimator** — enter an estimated entity count to see projected file sizes based on current settings

### Camera Presets Tab

- Lists all `CameraPresetSO` assets in the project
- Shows key fields: mode, move speed, FOV, follow distance, orbit speed
- **Select** — ping the asset in the Project window for editing in the Inspector
- **Delete** — remove the asset (with confirmation dialog)
- **Create New Preset** — creates a new preset in `Assets/Resources/`
- **Refresh** — re-scan for preset assets

---

## 9. Loading and Playing Replays via Script

```csharp
var player = ReplayPlayer.Instance;

// Synchronous load (blocks main thread — use for small files or editor)
bool success = player.Load("/path/to/replay.digreplay");

// Async load (recommended for runtime — does not block UI)
player.LoadAsync("/path/to/replay.digreplay");
player.OnLoadComplete += () => Debug.Log("Ready to play!");
player.OnLoadFailed += (error) => Debug.LogError(error);

// Playback control
player.Play();
player.Pause();
player.Stop();
player.SetSpeed(2f);          // 2x speed
player.Seek(0.5f);            // Seek to 50% through the replay
player.StepForward();         // Advance one frame
player.StepBackward();        // Go back one frame

// Timeline UI setup
var controller = GetComponent<ReplayTimelineController>();
ReplayTimelineView.Instance.Initialize(controller);
ReplayTimelineView.Instance.PopulateEventMarkers(player.GetEvents(), player.Duration);
ReplayTimelineView.Instance.PopulatePlayerList(player.Players);
```

---

## 10. File Format Reference

Replay files use the `.digreplay` extension with a binary format:

| Section | Size | Description |
|---------|------|-------------|
| Header | 64 bytes | Magic (`DIGR`), version, tick rate, timestamp, duration, frame count, peak entities, player count, map hash, CRC32 |
| Player table | Variable | Player info entries (NetworkId, GhostId, TeamId) |
| Frames | Variable | Sequence of frame headers + entity snapshots + events |

Each entity snapshot is 49 bytes: position (12) + rotation (16) + velocity (12) + health current (4) + health max (4) + death phase (1).

Delta frames only contain entities that changed since the last keyframe.

---

## 11. Verification Checklist

After setup, verify everything works:

1. [ ] `ReplayConfig` asset exists in `Assets/Resources/` with desired settings
2. [ ] At least one `CameraPresetSO` asset exists (optional but recommended)
3. [ ] `[ReplaySystem]` GameObject in boot scene with **ReplayPlayer**, **ReplayTimelineController**, **ReplayTimelineView**
4. [ ] **SpectatorCamera** component added (on same or separate persistent GameObject)
5. [ ] Enter Play Mode with **Auto Record** enabled — check console for `[ReplayRecorder] Recording started:`
6. [ ] Play for 30+ seconds, then stop Play Mode — check console for `[ReplayRecorder] Recording stopped.`
7. [ ] Verify `.digreplay` file exists in `Application.persistentDataPath/Replays/`
8. [ ] Open **DIG > Replay Workstation > Browser** — replay file appears with correct metadata
9. [ ] Load the replay via script — proxy entities appear and move correctly
10. [ ] Timeline UI: play/pause, scrub slider, step forward/backward all work
11. [ ] Speed controls: cycle through 0.25x, 0.5x, 1x, 2x, 4x
12. [ ] Camera modes: cycle with Tab — FreeCam (WASD), Follow, FirstPerson, Orbit all work
13. [ ] Spectator mode: set `GameBootstrap.IsSpectatorMode = true`, connect to server — no player spawned, camera works
14. [ ] Death spectator: die in multiplayer — camera switches to follow an alive player, console shows `[DeathSpectator] Local player died`
15. [ ] Death spectator controls: Tab cycles modes, 1-9 follows specific alive players
16. [ ] Death spectator exit: respawn — camera returns to normal, console shows `[DeathSpectator] Local player respawned`

---

## 12. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| No recording starts | `ReplayConfig` asset not in Resources/ or `RecordingEnabled` is off | Create asset named `ReplayConfig` in `Assets/Resources/`, enable recording |
| Recording starts but no file appears | Flush hasn't happened yet, or path permissions | Wait for `FlushIntervalSeconds`, check `persistentDataPath` is writable |
| `.digreplay` file is 0 bytes or corrupt | Play mode stopped before flush completed | Let recording run for at least one flush interval before stopping |
| Replay Workstation shows "No .digreplay files found" | Files not in expected directory | Click **Open Folder** to verify path, check `SaveSubdirectory` setting |
| Proxy entities don't appear on playback | No `ReplayPlayer` component in scene | Add `ReplayPlayer` to a persistent GameObject |
| Proxy entities are capsules | No Entity Proxy Prefab assigned | Assign a custom prefab to `ReplayPlayer > Entity Proxy Prefab` |
| Timeline UI doesn't show | `ReplayTimelineView.Initialize()` not called | Call `Initialize(controller)` after loading a replay |
| Spectator camera doesn't move | No camera assigned and `Camera.main` is null | Assign a camera to `SpectatorCamera > Camera` field |
| Spectator can't see entities | Client not receiving ghost snapshots | Verify server added `NetworkStreamInGame` to the connection |
| "No local player found" in spectator | Expected — spectators don't have a player entity | This is correct behavior; use SpectatorCamera for viewing |
| Camera mode buttons don't work | `SpectatorCamera` not in scene | Add `SpectatorCamera` component to a persistent GameObject |
| Large replay files | Delta encoding disabled or tick interval too low | Enable **Delta Encoding**, increase **Tick Interval** to 2-3 |
| Seek is slow | Keyframe interval too high | Lower **Keyframe Interval** (60 is default, 30 for faster seeks) |
| Death spectator doesn't activate on death | `[ReplaySystem]` prefab missing from scene, or `SpectatorCamera` component not present | Ensure `[ReplaySystem]` prefab is in the boot scene with SpectatorCamera |
| Camera fights between player cam and spectator cam | Normal camera system not yielding during death spectator | Check `DeathSpectatorTransitionSystem.IsDeathSpectating` in your camera controller and skip updates when true |
| No alive players to follow after death | All other players are also dead | System falls back to FreeCam mode automatically |
