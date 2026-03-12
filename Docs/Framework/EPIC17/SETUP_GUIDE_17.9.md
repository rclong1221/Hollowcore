# EPIC 17.9: Cinematic & Cutscene System — Setup Guide

## Overview

The Cinematic & Cutscene System provides server-authoritative cinematic triggers with client-side Timeline playback, camera blending, input lockout, letterbox bars, subtitles, and multiplayer skip voting. It is designed for boss intros, story cutscenes, zone entry events, and text overlays.

**Key design**: The server detects trigger conditions (zone proximity or encounter scripting) and broadcasts RPCs to all connected clients. Each client independently drives PlayableDirector, camera blend, and UI presentation. Skip votes are collected server-side and evaluated against a configurable policy.

**Zero player archetype impact** — all cinematic state lives on dedicated singleton entities. No components are added to the player entity.

---

## Quick Start

### 1. Create the Cinematic Database

1. **Right-click** in the Project window: **Create > DIG > Cinematic > Cinematic Database**
2. Name it `CinematicDatabase` and place it in `Assets/Resources/`
3. Configure global defaults:

| Field | Default | Description |
|-------|---------|-------------|
| DefaultSkipPolicy | AnyoneCanSkip | Fallback skip policy for all cinematics |
| BlendInDuration | 0.5 | Camera blend-in time (seconds) for FullCinematic type |
| BlendOutDuration | 0.5 | Camera blend-out time (seconds) |
| HUDFadeDuration | 0.3 | HUD fade in/out time (seconds) |
| LetterboxHeight | 0.12 | Letterbox bar height as screen fraction (0-0.25) |

> **Important:** The asset must be named exactly `CinematicDatabase` and placed in a `Resources/` folder. The bootstrap system loads it via `Resources.Load<CinematicDatabaseSO>("CinematicDatabase")`.

### 2. Create Cinematic Definitions

For each cinematic sequence in your game:

1. **Right-click** > **Create > DIG > Cinematic > Cinematic Definition**
2. Recommended folder: `Assets/Resources/Cinematics/` (or any project folder)
3. Fill in the fields:

| Field | Default | Description |
|-------|---------|-------------|
| **CinematicId** | — | **Unique integer**. Referenced by triggers and encounter scripting |
| **Name** | — | Display name (e.g., "Boss Intro - Dragon King") |
| Description | — | Designer notes (not shown in-game) |
| **TimelineAsset** | null | Unity Timeline asset to play. Null for TextOverlay type |
| **CinematicType** | FullCinematic | See Cinematic Types below |
| Duration | 10 | Fallback duration in seconds (used when TimelineAsset is null) |
| SkipPolicy | AnyoneCanSkip | Per-cinematic override. See Skip Policies below |
| DialogueTreeId | 0 | Optional dialogue tree to trigger at start (0 = none, EPIC 16.16) |
| MusicStingerId | 0 | Optional music stinger to play (0 = none, EPIC 17.5) |
| VoiceLineClip | null | Optional 2D voice AudioClip played during cinematic |
| CinematicCameraPrefab | null | Camera rig prefab for FullCinematic (null = use Timeline camera binding) |
| SubtitleKeys | [] | Localization keys for subtitle lines (parallel arrays) |
| SubtitleTimings | [] | Timestamp in seconds for each subtitle line |

### 3. Register Cinematics in the Database

1. Open `Assets/Resources/CinematicDatabase.asset`
2. Drag all CinematicDefinitionSO assets into the **Cinematics** list
3. Verify no duplicate CinematicId values (the bootstrap logs warnings for duplicates)

### 4. Place Cinematic Triggers in Scenes

For zone-based triggers (player walks into area):

1. Create an empty GameObject in your server subscene (e.g., "CinematicTrigger_BossIntro")
2. **Add Component > DIG > Cinematic > Cinematic Trigger**
3. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| CinematicId | — | Must match a CinematicDefinitionSO.CinematicId in the database |
| PlayOnce | true | If true, trigger fires only once per session |
| CinematicType | FullCinematic | Determines camera/input/UI behavior |
| SkipPolicy | AnyoneCanSkip | Skip rule for multiplayer |
| TriggerRadius | 5.0 | Overlap sphere radius for player proximity detection (meters) |

**Gizmo**: Selecting the trigger in Scene View shows a purple wire sphere at the trigger radius.

> **Note:** Cinematic triggers run on the **server** (ServerSimulation | LocalSimulation). Place them in server-accessible subscenes, not client-only subscenes.

### 5. Set Up Timeline Bridge (Advanced)

For cinematics with Timeline signals that drive NPC animations, VFX, sound, dialogue, or screen fades:

1. On the GameObject with your **PlayableDirector**, add component: **DIG > Cinematic > Timeline Bridge**
2. Add a **SignalReceiver** component to the same GameObject
3. Create Timeline Signal assets and wire them to the bridge methods:

| Signal Method | Parameters | Effect |
|---------------|------------|--------|
| `OnAnimationSignal` | targetId (int), animationHash (int) | Drives NPC entity animation state |
| `OnVFXSignal` | vfxTypeId (int), position (Vector3) | Spawns VFX at marker position |
| `OnSoundSignal` | soundId (int), position (Vector3), volume (float) | Plays sound effect at position |
| `OnDialogueSignal` | dialogueTreeId (int) | Triggers dialogue node mid-cinematic |
| `OnFadeSignal` | duration (float), fadeToBlack (bool) | Screen fade to/from black |

These enqueue events to the `CinematicAnimEventQueue` for consumption by ECS systems.

---

## Cinematic Types

| Type | Camera | Input | HUD | Letterbox | Use Case |
|------|--------|-------|-----|-----------|----------|
| **FullCinematic** | Blends to cinematic camera | Locked (all zeroed) | Hidden | Yes | Boss intros, story cutscenes, major events |
| **InWorldEvent** | No change (gameplay camera) | Not locked | No change | No | NPC animations, environmental events, ambient scripted moments |
| **TextOverlay** | No change | Locked (all zeroed) | Hidden | No | Text narration, tutorial overlays, loading lore |

---

## Skip Policies

| Policy | Behavior | Use Case |
|--------|----------|----------|
| **AnyoneCanSkip** | First player to press Escape/Space ends the cinematic for everyone | Repeatable boss intros, optional cutscenes |
| **MajorityVote** | >50% of connected players must vote skip | Important story cinematics |
| **AllMustSkip** | Every player must vote skip | Critical story beats, raid cinematics |
| **NoSkip** | Cinematic cannot be skipped | Tutorials, mandatory story sequences |

Skip keys: **Escape** or **Space**. In solo play (LocalSimulation), skip is instant with no RPC roundtrip.

---

## Encounter Integration (Boss Triggers)

To trigger a cinematic from a boss encounter:

1. Open the encounter's trigger list (EncounterProfileAuthoring)
2. Add a trigger with action type **PlayCinematic**
3. Set **ActionParam** = CinematicId (must match a registered definition)
4. Set **ActionValue** = Duration in seconds (0 = use definition's duration)

Example: Boss reaches 50% HP > PlayCinematic (ActionParam=5, ActionValue=15) > triggers a 15-second phase transition cinematic.

The encounter system calls `CinematicTriggerSystem.TriggerCinematicFromEncounter()` on the server, which broadcasts the play RPC to all clients.

---

## UI Provider Setup

The cinematic system communicates with your UI through the `ICinematicUIProvider` interface. You need to create a MonoBehaviour that implements this interface:

### Required Interface Methods

| Method | Called When |
|--------|------------|
| `OnCinematicStart(cinematicId, type)` | Cinematic begins playing |
| `OnCinematicEnd(cinematicId, wasSkipped)` | Cinematic ends (natural or skipped) |
| `UpdateSkipPrompt(canSkip, votesReceived, totalPlayers)` | Every frame during playback — update "Press Escape to skip (2/4)" |
| `UpdateSubtitle(text, duration)` | New subtitle line triggered |
| `SetLetterbox(targetHeight, fadeDuration)` | Letterbox bars should animate to target height |
| `SetHUDVisible(visible, fadeDuration)` | HUD should fade in/out |
| `UpdateProgress(normalizedTime)` | Every frame — progress bar value (0-1) |

### Registering the Provider

In your MonoBehaviour:

```csharp
void OnEnable()
{
    CinematicUIRegistry.Register(this);
}

void OnDisable()
{
    CinematicUIRegistry.Unregister(this);
}
```

If no provider is registered after 120 frames, a warning is logged. The system continues to function without a UI provider; you just won't see letterbox, skip prompts, or subtitles.

---

## Editor Tooling: Cinematic Workstation

**Menu**: DIG > Cinematic Workstation

| Module | Description |
|--------|-------------|
| **Cinematic Browser** | Lists all CinematicDefinitionSO assets in the project. Filter by cinematic type. Shows CinematicId, Name, Type, Skip Policy, Duration, Dialogue, and Timeline columns. Click to select/ping the asset |
| **Cinematic Debug** (Play Mode) | Live view of CinematicState singleton: IsPlaying, CinematicId, ElapsedTime, Type, CanSkip, Votes, Duration, BlendProgress. Config display. Registry info. UI provider status. Preview controls: Play button (enter CinematicId + click Play), Force Stop button. Scene View overlay: purple wire spheres around all CinematicTriggerAuthoring objects showing their trigger radius |

---

## Architecture

```
SERVER-SIDE ECS SYSTEMS (authoritative)
+-- CinematicTriggerSystem (SimulationSystemGroup)
|   +-- Zone proximity: PlayerTag entities vs CinematicTrigger radius
|   +-- Encounter entry: TriggerCinematicFromEncounter() called by EncounterTriggerSystem
|   +-- Broadcasts CinematicPlayRpc to all clients
|
+-- CinematicEndSystem (SimulationSystemGroup, after Trigger)
|   +-- Elapsed timer -> broadcasts CinematicEndRpc on natural end
|
+-- CinematicSkipSystem (SimulationSystemGroup)
    +-- Receives CinematicSkipRpc -> tallies votes in NativeHashSet<int>
    +-- Evaluates SkipPolicy threshold -> broadcasts CinematicEndRpc

CLIENT-SIDE ECS SYSTEMS (presentation)
+-- CinematicBootstrapSystem (InitializationSystemGroup, runs once)
|   +-- Loads CinematicDatabaseSO from Resources/
|   +-- Creates CinematicState + CinematicConfigSingleton + CinematicRegistryManaged
|
+-- CinematicRpcReceiveSystem (SimulationSystemGroup, ClientSimulation only)
|   +-- Processes Play/End/SkipUpdate RPCs -> writes CinematicState singleton
|
+-- CinematicInputLockSystem (SimulationSystemGroup)
|   +-- Zeros PlayerInputComponent fields during FullCinematic/TextOverlay
|
+-- CinematicSkipInputSystem (SimulationSystemGroup)
|   +-- Reads Escape/Space keys -> sends CinematicSkipRpc (or immediate skip in solo)
|
+-- CinematicPlaybackSystem (PresentationSystemGroup)
|   +-- Drives PlayableDirector lifecycle, voice audio, subtitle timing
|
+-- CinematicCameraSystem (PresentationSystemGroup)
|   +-- SmoothStep blend between gameplay camera and cinematic camera
|
+-- CinematicUIBridgeSystem (PresentationSystemGroup, after Playback)
    +-- Pushes CinematicState to CinematicUIRegistry -> ICinematicUIProvider
```

---

## Multiplayer Considerations

- **Server-authoritative**: The server decides when cinematics start, when they end, and whether skip threshold is met. Clients cannot start cinematics independently.
- **RPC-based**: Four RPC types handle communication: `CinematicPlayRpc` (server > clients), `CinematicSkipRpc` (client > server), `CinematicSkipUpdateRpc` (server > clients), `CinematicEndRpc` (server > clients).
- **Vote deduplication**: The server uses a `NativeHashSet<int>` to prevent the same player from voting skip twice.
- **Player disconnect**: If a player disconnects during a cinematic, the skip system adjusts the total player count and re-evaluates the threshold.
- **Solo play**: In LocalSimulation, skip is instant (no RPC roundtrip needed).

---

## Common Patterns

### Boss Intro Cutscene

1. Create a CinematicDefinitionSO: Id=1, Type=FullCinematic, SkipPolicy=AnyoneCanSkip
2. Author a Timeline with camera cuts, NPC animation signals, and voice line
3. Assign the CinematicCameraPrefab (camera rig with animation)
4. In the encounter profile, add trigger: HP drops below 100% > PlayCinematic (ActionParam=1)
5. Place a music stinger (MusicStingerId) for dramatic effect

### Story Zone Entry

1. Create a CinematicDefinitionSO: Id=10, Type=FullCinematic, SkipPolicy=MajorityVote
2. Create an empty GameObject in the subscene at the zone entry point
3. Add **DIG > Cinematic > Cinematic Trigger**: CinematicId=10, PlayOnce=true, TriggerRadius=3
4. First time any player enters the radius, the cinematic plays for everyone

### NPC Ambient Event

1. Create a CinematicDefinitionSO: Id=20, Type=InWorldEvent, Duration=8
2. Place trigger in subscene: CinematicId=20, PlayOnce=false, TriggerRadius=10
3. Player walks near, NPC performs animation — no camera change, no input lock, no letterbox

### Tutorial Text Overlay

1. Create a CinematicDefinitionSO: Id=100, Type=TextOverlay, SkipPolicy=NoSkip, Duration=12
2. Set SubtitleKeys: ["tutorial_welcome", "tutorial_movement", "tutorial_combat"]
3. Set SubtitleTimings: [0.0, 4.0, 8.0]
4. Place trigger: CinematicId=100, PlayOnce=true, TriggerRadius=5

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| No cinematics play at all | Missing CinematicDatabase asset | Create `CinematicDatabase` in `Assets/Resources/` and register definitions |
| Console: "CinematicId X not found in registry" | Definition not in database list | Open CinematicDatabase, drag the definition into the Cinematics list |
| Console: "Duplicate CinematicId" at bootstrap | Two definitions share the same ID | Change one definition's CinematicId to a unique value |
| Trigger never fires | Trigger in client-only subscene | Move trigger to a server-accessible subscene |
| Trigger never fires | No PlayerTag entities in range | Verify player prefab has PlayerTag component |
| Camera doesn't blend | CinematicCameraPrefab not set | Assign a camera rig prefab on the CinematicDefinitionSO, or use InWorldEvent type |
| Input not locked during cinematic | CinematicType is InWorldEvent | InWorldEvent intentionally does not lock input. Use FullCinematic or TextOverlay |
| No letterbox/subtitles/skip prompt | No ICinematicUIProvider registered | Create a MonoBehaviour implementing ICinematicUIProvider and register it |
| Console: "No ICinematicUIProvider registered after 120 frames" | UI MonoBehaviour not active in scene | Ensure your cinematic UI panel is active and calls `CinematicUIRegistry.Register(this)` |
| Skip doesn't work in multiplayer | SkipPolicy is NoSkip | Change the definition's SkipPolicy to AnyoneCanSkip or MajorityVote |
| Cinematic Workstation shows no state | Not in Play Mode | Enter Play Mode; the Debug module requires live ECS worlds |

---

## Setup Checklist

- [ ] Create `Assets/Resources/CinematicDatabase.asset` (**Create > DIG > Cinematic > Cinematic Database**)
- [ ] Configure database defaults (blend durations, letterbox height, default skip policy)
- [ ] Create CinematicDefinitionSO assets for each cinematic (**Create > DIG > Cinematic > Cinematic Definition**)
- [ ] Assign unique CinematicId to each definition
- [ ] Assign TimelineAsset, CinematicCameraPrefab, VoiceLineClip, subtitles as needed
- [ ] Drag all definitions into the CinematicDatabase **Cinematics** list
- [ ] Place CinematicTriggerAuthoring GameObjects in server subscenes
- [ ] Set TriggerRadius, PlayOnce, CinematicType, SkipPolicy on each trigger
- [ ] (Optional) Add PlayCinematic actions to encounter trigger profiles
- [ ] (Optional) Set up CinematicTimelineBridge + SignalReceiver on Timeline GameObjects
- [ ] Create and register an ICinematicUIProvider MonoBehaviour for letterbox/subtitles/skip UI
- [ ] Open **DIG > Cinematic Workstation** and verify:
  - [ ] Cinematic Browser lists all definitions with correct fields
  - [ ] No duplicate CinematicId warnings
- [ ] Enter Play Mode and test:
  - [ ] Walk into trigger radius > cinematic plays
  - [ ] Camera blends smoothly (FullCinematic type)
  - [ ] Input is locked (FullCinematic / TextOverlay)
  - [ ] Skip key works (Escape or Space)
  - [ ] Subtitles appear at correct timings
  - [ ] Cinematic ends naturally when duration is reached

---

## File Manifest

### Components (6 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Cinematic/Components/CinematicState.cs` | Client singleton: playback state, skip votes, blend progress |
| `Assets/Scripts/Cinematic/Components/CinematicTrigger.cs` | Server-side zone trigger component (12 bytes) |
| `Assets/Scripts/Cinematic/Components/CinematicRpcs.cs` | 4 RPC structs: Play, Skip, End, SkipUpdate |
| `Assets/Scripts/Cinematic/Components/CinematicConfigSingleton.cs` | Runtime config: blend durations, letterbox height |
| `Assets/Scripts/Cinematic/Components/CinematicRegistryManaged.cs` | Managed class: definition lookup + active playback refs |
| `Assets/Scripts/Cinematic/Components/CinematicAnimEvent.cs` | Event struct + enum for Timeline signal bridge |

### Definitions (2 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Cinematic/Definitions/CinematicDefinitionSO.cs` | Per-cinematic ScriptableObject |
| `Assets/Scripts/Cinematic/Definitions/CinematicDatabaseSO.cs` | Central registry ScriptableObject (Resources/) |

### Bridges (3 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Cinematic/Bridges/CinematicAnimEventQueue.cs` | Static NativeQueue bridge for Timeline events |
| `Assets/Scripts/Cinematic/Bridges/ICinematicUIProvider.cs` | UI provider interface |
| `Assets/Scripts/Cinematic/Bridges/CinematicUIRegistry.cs` | Static provider registry |

### Systems (10 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Cinematic/Systems/CinematicBootstrapSystem.cs` | Loads database, creates singletons (runs once) |
| `Assets/Scripts/Cinematic/Systems/CinematicTriggerSystem.cs` | Server: zone proximity detection + RPC broadcast |
| `Assets/Scripts/Cinematic/Systems/CinematicRpcReceiveSystem.cs` | Client: processes RPCs, writes CinematicState |
| `Assets/Scripts/Cinematic/Systems/CinematicPlaybackSystem.cs` | Client: PlayableDirector, voice audio, subtitles |
| `Assets/Scripts/Cinematic/Systems/CinematicInputLockSystem.cs` | Client: zeros PlayerInputComponent during cinematic |
| `Assets/Scripts/Cinematic/Systems/CinematicCameraSystem.cs` | Client: SmoothStep camera blend |
| `Assets/Scripts/Cinematic/Systems/CinematicSkipSystem.cs` | Server: skip vote tallying + threshold evaluation |
| `Assets/Scripts/Cinematic/Systems/CinematicEndSystem.cs` | Server: elapsed timer + natural end broadcast |
| `Assets/Scripts/Cinematic/Systems/CinematicSkipInputSystem.cs` | Client: Escape/Space key detection + skip RPC |
| `Assets/Scripts/Cinematic/Systems/CinematicUIBridgeSystem.cs` | Client: pushes state to ICinematicUIProvider |

### Authoring (2 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/Cinematic/Authoring/CinematicTriggerAuthoring.cs` | Zone trigger baker with gizmo |
| `Assets/Scripts/Cinematic/Authoring/CinematicTimelineBridge.cs` | Timeline SignalReceiver callback bridge |

### Editor (4 files)
| File | Description |
|------|-------------|
| `Assets/Editor/CinematicWorkstation/ICinematicWorkstationModule.cs` | Module interface |
| `Assets/Editor/CinematicWorkstation/CinematicWorkstationWindow.cs` | Editor window (DIG > Cinematic Workstation) |
| `Assets/Editor/CinematicWorkstation/Modules/CinematicBrowserModule.cs` | Definition browser with type filter |
| `Assets/Editor/CinematicWorkstation/Modules/CinematicDebugModule.cs` | Live debug + preview controls |

### Modified (4 files)
| File | Change |
|------|--------|
| `Assets/Scripts/AI/Components/EncounterTrigger.cs` | Added `PlayCinematic = 17` to TriggerActionType |
| `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` | PlayCinematic handler calls CinematicTriggerSystem |
| `Assets/Scripts/AI/Editor/EncounterSimulator.cs` | PlayCinematic simulation case |
| `Assets/Scripts/AI/Editor/EncounterValidator.cs` | PlayCinematic validation checks |
