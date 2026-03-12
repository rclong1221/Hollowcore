# EPIC 17.9: Cinematic & Cutscene System

**Status:** PLANNED
**Priority:** Low (Polish / Narrative)
**Dependencies:**
- `EncounterTriggerSystem` (existing -- `DIG.AI.Systems.EncounterTriggerSystem.cs`, processes TriggerActionType enum, PlayCinematic slot available at value 16)
- `TriggerActionType` enum (existing -- `DIG.AI.Components.EncounterTrigger.cs`, values 0-15 defined, next available = 16)
- `DialogueCameraSystem` (existing -- `DIG.Dialogue.Systems.DialogueCameraSystem.cs`, Client|Local PresentationSystemGroup, camera handoff pattern)
- `DialogueActionSystem` (existing -- `DIG.Dialogue.Systems.DialogueActionSystem.cs`, EPIC 16.16, executes dialogue node actions)
- `DialogueEventQueue` static queue (existing -- `DIG.Dialogue`, ECS-to-UI event bridge)
- `PlayerCameraControlSystem` (existing -- `DIG.Player`, main camera driver, PredictedFixedStepSimulation)
- `AudioBusType.Dialogue` = 3 (existing -- `DIG.Audio.Config.AudioBusConfig.cs`, dedicated mixer bus, priority=200)
- `AudioBusType.Music` = 1 (existing -- `DIG.Audio.Config.AudioBusConfig.cs`, music mixer bus)
- `SoundEventRequest` IComponentData (existing -- `DIG.Aggro`, transient entity: Position/SourceEntity/Loudness/MaxRange/Category)
- `GhostOwnerIsLocal` IComponentData (existing -- Unity.NetCode, identifies local player entity)
- `GhostOwner.NetworkId` (existing -- Unity.NetCode, player identity for skip voting)
- `CharacterAttributes` IComponentData (existing -- `DIG.Combat.Components.CombatStatComponents.cs`, Ghost:All, 20 bytes)
- `CombatUIRegistry` / `CombatUIBridgeSystem` (existing -- managed bridge pattern reference)
- `ProceduralMotionProfile` BlobAsset pattern (existing -- `DIG.ProceduralMotion`, BlobBuilder singleton reference)
- `ItemRegistryBootstrapSystem` (existing -- bootstrap singleton pattern reference)
- Unity Timeline / PlayableDirector (existing -- Unity package `com.unity.timeline`, managed API)
- Unity Playables API (existing -- `UnityEngine.Playables`, PlayableGraph for camera blending)

**Feature:** A server-triggered cinematic system that wraps Unity Timeline/PlayableDirector for scripted sequences, providing ECS-to-Timeline bridging, player input lockout, camera handoff with smooth blending, vote-to-skip multiplayer support, and dialogue integration. Three cinematic types (FullCinematic, InWorldEvent, TextOverlay) cover all narrative presentation needs. The server decides when to play cinematics; clients execute them locally via PlayableDirector. Only a singleton component on the client world -- zero impact on player entity archetype.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `TriggerActionType` enum (16 values) | `EncounterTrigger.cs` | Values 0-15 used | Next available = 16, can add PlayCinematic |
| `EncounterTriggerSystem` | `EncounterTriggerSystem.cs` | Fully implemented | Processes triggers, PlayDialogue (=7) stub exists as pattern |
| `DialogueCameraSystem` | `DialogueCameraSystem.cs` | Fully implemented | Client|Local, PresentationSystemGroup, camera handoff via DialogueEventQueue |
| `DialogueActionSystem` | `DialogueActionSystem.cs` | Fully implemented | Executes 11 action types from dialogue nodes |
| `PlayerCameraControlSystem` | `PlayerCameraControlSystem.cs` | Fully implemented | PredictedFixedStep, main camera driver |
| `AudioBusType.Dialogue` (=3) | `AudioBusConfig.cs` | Configured | Dedicated mixer bus, priority=200 (highest) |
| `AudioBusType.Music` (=1) | `AudioBusConfig.cs` | Configured | Music mixer bus for stingers |
| `SoundEventRequest` | `SoundEventRequest.cs` | Fully implemented | Transient entity pattern for audio |
| `GhostOwnerIsLocal` | Unity.NetCode | Fully implemented | Identifies local player on client |
| `DialogueEventQueue` | `DialogueEventQueue.cs` | Fully implemented | Static queue bridge pattern |

### What's Missing

- **No cinematic playback** -- no system drives Unity PlayableDirector from ECS triggers
- **No server-to-client cinematic RPC** -- no mechanism for server to command clients to play a cinematic
- **No player lockout during cinematics** -- no input disable, HUD hide, or camera takeover
- **No skip/vote-to-skip** -- no multiplayer skip coordination
- **No cinematic camera blending** -- no smooth transition from gameplay camera to cinematic camera
- **No NPC animation bridge** -- Timeline cannot drive ECS entity animations
- **No EncounterTrigger.PlayCinematic** -- enum value not defined, no handler in EncounterTriggerSystem
- **No cinematic data layer** -- no ScriptableObjects mapping CinematicId to TimelineAsset
- **No editor tooling** -- no cinematic preview, timeline linking, or debug overlay

---

## Problem

DIG has boss encounters with phase transitions, a dialogue system with camera handoff, and a full audio pipeline -- but no way to play scripted cinematic sequences. Boss introductions, story beats, zone reveals, and tutorial sequences all require camera-controlled narrative moments that lock player input and play authored animations. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `EncounterTriggerSystem` with 16 action types | No `PlayCinematic` trigger action |
| `DialogueCameraSystem` camera handoff pattern | No cinematic camera blend system |
| `DialogueActionSystem` triggers dialogue from events | No way to trigger dialogue FROM a cinematic |
| `AudioBusType.Dialogue` + `AudioBusType.Music` | No cinematic-specific audio orchestration |
| Unity Timeline package installed | No ECS bridge to PlayableDirector |
| `GhostOwner.NetworkId` player identity | No skip vote coordination |
| `PlayerCameraControlSystem` main camera | No mechanism to yield camera control to cinematic |

**The gap:** A boss encounter cannot play an intro cinematic before combat begins. A player entering a new zone cannot see a camera flyover. Story moments have no presentation layer. There is no way to lock player input during a scripted sequence, and no multiplayer skip coordination.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  CinematicDefinitionSO        CinematicDatabaseSO        CinematicConfig
  (CinematicId, Name,          (List<CinematicDef>,        (DefaultSkipPolicy,
   TimelineAsset, Type,         loaded via Resources/)      BlendIn/Out durations,
   SkipPolicy, Duration,                                    HUD fade timing)
   DialogueTreeId, MusicId)
           |                           |                          |
           └─────── CinematicBootstrapSystem ─────────────────────┘
                    (InitializationSystemGroup, Client|Local,
                     loads from Resources/, creates CinematicConfig singleton,
                     builds CinematicRegistry managed singleton)
                              |
                    ECS DATA LAYER
  CinematicState              CinematicConfig              CinematicRegistryManaged
  (Client|Local singleton,    (Client|Local singleton,     (managed singleton,
   24 bytes: IsPlaying,        unmanaged, 20 bytes)         Dict<int, CinematicDef>)
   CurrentId, Elapsed,
   CanSkip, SkipVotes,
   TotalPlayers, Type)
                              |
                    SERVER TRIGGER LAYER (SimulationSystemGroup, Server|Local)
                              |
  CinematicTriggerSystem ── detects zone entry / encounter trigger / quest event
  (reads CinematicTrigger on entities, fires CinematicPlayRpc to clients)
           |
  CinematicSkipSystem ── receives CinematicSkipRpc from clients
  (tallies votes, fires CinematicEndRpc when threshold met)
           |
  CinematicEndSystem ── cleanup, fires CinematicEndRpc
  (timeout or skip threshold reached)
                              |
                    CLIENT PLAYBACK LAYER (Client|Local)
                              |
  CinematicRpcReceiveSystem (SimulationSystemGroup)
  ── receives Play/End RPCs, writes CinematicState singleton
           |
  CinematicInputLockSystem (SimulationSystemGroup)
  ── reads CinematicState.IsPlaying, disables player input
           |
  CinematicCameraSystem (PresentationSystemGroup)
  ── smooth blend from gameplay camera to cinematic camera and back
           |
  CinematicPlaybackSystem (PresentationSystemGroup, managed)
  ── drives PlayableDirector, reads Timeline signals, bridges NPC animation
           |
  CinematicUIBridgeSystem (PresentationSystemGroup, managed)
  ── pushes state to CinematicUIRegistry -> ICinematicUIProvider
     (skip prompt, subtitle text, letterbox bars, HUD fade)
```

### Data Flow (Encounter Trigger -> Cinematic -> End)

```
Frame N (Server):
  1. EncounterTriggerSystem: Boss encounter condition met
     → TriggerActionType.PlayCinematic fires
     → CinematicTriggerSystem reads CinematicTrigger component
     → Creates CinematicPlayRpc (broadcast to all connected clients)
     → If PlayOnce: marks CinematicTrigger.HasPlayed = true

Frame N+1 (Clients):
  2. CinematicRpcReceiveSystem: Receives CinematicPlayRpc
     → Sets CinematicState.IsPlaying = true
     → Sets CinematicState.CurrentCinematicId, CinematicType, CanSkip
     → CinematicState.ElapsedTime = 0

  3. CinematicInputLockSystem: Reads CinematicState.IsPlaying == true
     → Disables player input (writes InputLockTag or sets flag)
     → Only for FullCinematic and TextOverlay types

  4. CinematicCameraSystem: CinematicState.IsPlaying transitions true
     → Begins camera blend from gameplay camera to cinematic camera
     → BlendIn over CinematicConfig.BlendInDuration (default 0.5s)
     → Only for FullCinematic type

  5. CinematicPlaybackSystem: CinematicState.IsPlaying == true
     → Looks up CinematicDefinitionSO via CinematicRegistryManaged
     → Creates/starts PlayableDirector with TimelineAsset
     → If DialogueTreeId > 0: fires DialogueActionSystem trigger
     → If MusicStingerId > 0: creates SoundEventRequest on Music bus

Frame N+K (During Playback):
  6. CinematicPlaybackSystem: Each frame
     → CinematicState.ElapsedTime += deltaTime
     → Reads Timeline SignalReceiver for NPC animation events
     → Bridges events to ECS via CinematicAnimEventQueue

Frame N+D (Cinematic Ends -- timeout or skip):
  7a. Natural end: CinematicState.ElapsedTime >= Duration
      → CinematicPlaybackSystem: stops PlayableDirector
      → CinematicState.IsPlaying = false

  7b. Skip: Player presses skip key
      → Client sends CinematicSkipRpc to server
      → CinematicSkipSystem: tallies votes against SkipPolicy
      → If threshold met: broadcasts CinematicEndRpc to all clients
      → CinematicRpcReceiveSystem: sets CinematicState.IsPlaying = false

  8. CinematicCameraSystem: CinematicState.IsPlaying transitions false
     → Begins camera blend back to gameplay camera
     → BlendOut over CinematicConfig.BlendOutDuration (default 0.5s)

  9. CinematicInputLockSystem: CinematicState.IsPlaying == false
     → Restores player input

  10. CinematicUIBridgeSystem: Fades HUD back in
      → Hides skip prompt, removes letterbox bars
```

### Cinematic Type Behavior Matrix

```
Feature              | FullCinematic | InWorldEvent | TextOverlay
---------------------+---------------+--------------+-------------
Camera takeover      | YES           | NO           | NO
Player input locked  | YES           | NO           | YES
HUD hidden           | YES           | NO           | Partial
Letterbox bars       | YES           | NO           | NO
NPC animations       | YES           | YES          | NO
Voice/dialogue       | YES           | Optional     | YES
Music stinger        | YES           | Optional     | Optional
Skip prompt          | If CanSkip    | N/A          | If CanSkip
PlayableDirector     | YES           | YES          | NO
```

---

## ECS Components

### Client Singleton

**File:** `Assets/Scripts/Cinematic/Components/CinematicState.cs`

```
CinematicType enum : byte
  FullCinematic  = 0    // Full camera control + animations + letterbox
  InWorldEvent   = 1    // NPC animations in world, no camera takeover
  TextOverlay    = 2    // Text + audio overlay, no camera change

SkipPolicy enum : byte
  AnyoneCanSkip  = 0    // First skip request ends cinematic
  MajorityVote   = 1    // >50% of players must skip
  AllMustSkip    = 2    // Every player must skip
  NoSkip         = 3    // Unskippable (tutorials, critical story)

CinematicState (IComponentData, NOT ghost-replicated -- client singleton only)
  IsPlaying            : bool       // 1 byte
  CurrentCinematicId   : int        // 4 bytes -- maps to CinematicDefinitionSO
  ElapsedTime          : float      // 4 bytes -- seconds since playback started
  CanSkip              : bool       // 1 byte
  SkipVotesReceived    : byte       // 1 byte -- server broadcasts updated count
  TotalPlayersInScene  : byte       // 1 byte -- for skip UI "2/4 voted"
  CinematicType        : CinematicType  // 1 byte
  Duration             : float      // 4 bytes -- total cinematic duration
  BlendProgress        : float      // 4 bytes -- 0-1 camera blend interpolant
  // Padding: 3 bytes
  // Total: 24 bytes
```

**Archetype impact:** ZERO on player entity. CinematicState is a singleton entity created by CinematicBootstrapSystem on the client world. No ghost components, no buffers on player.

### Trigger Component (on encounter/zone entities)

**File:** `Assets/Scripts/Cinematic/Components/CinematicTrigger.cs`

```
CinematicTrigger (IComponentData, Server|Local only)
  CinematicId    : int             // 4 bytes -- references CinematicDefinitionSO
  PlayOnce       : bool            // 1 byte -- if true, trigger deactivates after first play
  HasPlayed      : bool            // 1 byte -- runtime flag, set by CinematicTriggerSystem
  CinematicType  : CinematicType   // 1 byte
  SkipPolicy     : SkipPolicy      // 1 byte
  TriggerRadius  : float           // 4 bytes -- overlap sphere radius for zone triggers
  // Total: 12 bytes
```

### RPCs

**File:** `Assets/Scripts/Cinematic/Components/CinematicRpcs.cs`

```
CinematicPlayRpc (IRpcCommand)
  CinematicId        : int             // 4 bytes
  CinematicType      : CinematicType   // 1 byte
  SkipPolicy         : SkipPolicy      // 1 byte
  Duration           : float           // 4 bytes
  TotalPlayers       : byte            // 1 byte
  // Padding: 1 byte
  // Total: 12 bytes

CinematicSkipRpc (IRpcCommand)
  CinematicId        : int             // 4 bytes
  NetworkId          : int             // 4 bytes -- player who voted to skip
  // Total: 8 bytes

CinematicEndRpc (IRpcCommand)
  CinematicId        : int             // 4 bytes
  WasSkipped         : bool            // 1 byte
  // Padding: 3 bytes
  // Total: 8 bytes

CinematicSkipUpdateRpc (IRpcCommand)
  CinematicId        : int             // 4 bytes
  SkipVotesReceived  : byte            // 1 byte -- broadcast to all clients for UI
  // Padding: 3 bytes
  // Total: 8 bytes
```

### Config Singleton

**File:** `Assets/Scripts/Cinematic/Components/CinematicConfigSingleton.cs`

```
CinematicConfigSingleton (IComponentData, Client|Local singleton)
  DefaultSkipPolicy  : SkipPolicy     // 1 byte
  BlendInDuration    : float           // 4 bytes -- camera blend in (default 0.5s)
  BlendOutDuration   : float           // 4 bytes -- camera blend out (default 0.5s)
  HUDFadeDuration    : float           // 4 bytes -- HUD fade in/out (default 0.3s)
  LetterboxHeight    : float           // 4 bytes -- letterbox bar height (default 0.12)
  // Padding: 3 bytes
  // Total: 20 bytes
```

### Managed Singleton

**File:** `Assets/Scripts/Cinematic/Components/CinematicRegistryManaged.cs`

```
CinematicRegistryManaged (class, IComponentData)
  Definitions          : Dictionary<int, CinematicDefinitionSO>
  ActiveDirector       : PlayableDirector    // currently playing director (null if idle)
  CinematicCamera      : Camera              // dedicated cinematic camera (disabled when idle)
  IsInitialized        : bool
```

### Animation Event Bridge

**File:** `Assets/Scripts/Cinematic/Components/CinematicAnimEvent.cs`

```
CinematicAnimEventType enum : byte
  PlayAnimation   = 0    // Drive NPC entity animation state
  SpawnVFX        = 1    // Spawn VFX at marker position
  PlaySound       = 2    // Play sound effect
  TriggerDialogue = 3    // Start dialogue node
  FadeToBlack     = 4    // Screen fade

CinematicAnimEvent (struct, NOT an IComponentData -- used in static queue)
  EventType    : CinematicAnimEventType  // 1 byte
  TargetId     : int                      // 4 bytes -- NPC entity identifier or VFX type
  IntParam     : int                      // 4 bytes -- animation hash, dialogue tree id, etc.
  FloatParam   : float                    // 4 bytes -- duration, intensity
  Position     : float3                   // 12 bytes -- world position for VFX/sound
  // Total: 25 bytes (padded to 28)
```

**File:** `Assets/Scripts/Cinematic/Bridges/CinematicAnimEventQueue.cs`

Static `NativeQueue<CinematicAnimEvent>` bridge (same pattern as `DamageVisualQueue`, `LevelUpVisualQueue`). CinematicPlaybackSystem enqueues from Timeline SignalReceiver callbacks. NPC animation systems and VFX systems dequeue.

---

## ScriptableObjects

### CinematicDefinitionSO

**File:** `Assets/Scripts/Cinematic/Definitions/CinematicDefinitionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Cinematic/Cinematic Definition")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| CinematicId | int | (unique) | Stable identifier referenced by triggers |
| Name | string | "" | Display name for editor and debug |
| Description | string | "" | Designer notes |
| TimelineAsset | TimelineAsset | null | Unity Timeline asset to play (null for TextOverlay) |
| CinematicType | CinematicType | FullCinematic | Determines behavior matrix |
| SkipPolicy | SkipPolicy | AnyoneCanSkip | Multiplayer skip behavior |
| Duration | float | 10.0 | Total duration in seconds (fallback if Timeline is null) |
| DialogueTreeId | int | 0 | Optional dialogue tree to trigger (0 = none, EPIC 16.16) |
| MusicStingerId | int | 0 | Optional music stinger to play (0 = none) |
| VoiceLineClip | AudioClip | null | Optional voice line (played on AudioBusType.Dialogue) |
| CinematicCameraPrefab | GameObject | null | Camera rig prefab for FullCinematic (null = use Timeline binding) |
| SubtitleKeys | string[] | empty | Localization keys for subtitle text (TextOverlay type) |
| SubtitleTimings | float[] | empty | Timestamp per subtitle line (parallel to SubtitleKeys) |

### CinematicDatabaseSO

**File:** `Assets/Scripts/Cinematic/Definitions/CinematicDatabaseSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Cinematic/Cinematic Database")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Cinematics | List\<CinematicDefinitionSO\> | empty | All cinematic definitions |
| DefaultSkipPolicy | SkipPolicy | AnyoneCanSkip | Fallback policy if not specified per-cinematic |
| BlendInDuration | float | 0.5 | Default camera blend in seconds |
| BlendOutDuration | float | 0.5 | Default camera blend out seconds |
| HUDFadeDuration | float | 0.3 | HUD fade in/out seconds |
| LetterboxHeight | float | 0.12 | Letterbox bar screen fraction (0-0.25) |

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Client|Local):
  CinematicBootstrapSystem              -- loads CinematicDatabaseSO from Resources/,
                                           creates CinematicState singleton,
                                           creates CinematicConfigSingleton,
                                           creates CinematicRegistryManaged,
                                           self-disables after first run

SimulationSystemGroup (Server|Local):
  CinematicTriggerSystem                -- detects trigger conditions (zone entry,
                                           encounter trigger, quest event),
                                           broadcasts CinematicPlayRpc to clients
  CinematicSkipSystem                   -- receives CinematicSkipRpc from clients,
                                           tallies votes against SkipPolicy,
                                           broadcasts CinematicEndRpc when threshold met
  CinematicEndSystem                    -- timeout detection (server tracks elapsed time),
                                           broadcasts CinematicEndRpc on natural end

SimulationSystemGroup (ClientSimulation):
  CinematicRpcReceiveSystem             -- receives CinematicPlayRpc, CinematicEndRpc,
                                           CinematicSkipUpdateRpc,
                                           writes CinematicState singleton

SimulationSystemGroup (Client|Local):
  CinematicInputLockSystem              -- reads CinematicState.IsPlaying,
                                           disables/enables player input,
                                           only for FullCinematic + TextOverlay types

PresentationSystemGroup (Client|Local):
  CinematicCameraSystem                 -- camera blend to/from cinematic camera,
                                           uses PlayableGraph for smooth interpolation,
                                           only for FullCinematic type
  CinematicPlaybackSystem (managed)     -- drives PlayableDirector lifecycle,
                                           reads Timeline SignalReceiver for events,
                                           enqueues CinematicAnimEvent to static queue,
                                           triggers dialogue via DialogueActionSystem
  CinematicUIBridgeSystem (managed)     -- pushes CinematicState to UI registry,
                                           letterbox bars, skip prompt, subtitle text,
                                           HUD fade control
```

### CinematicBootstrapSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicBootstrapSystem.cs`

- Managed SystemBase, `InitializationSystemGroup`, `Client|Local`
- Runs once, self-disables after initialization
- Loads `CinematicDatabaseSO` from `Resources/CinematicDatabase`
- Creates `CinematicState` singleton entity (24 bytes, IsPlaying=false)
- Creates `CinematicConfigSingleton` with database defaults
- Creates `CinematicRegistryManaged` with Definition lookup dictionary
- Same pattern as `ItemRegistryBootstrapSystem`, `ProgressionBootstrapSystem`

### CinematicTriggerSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicTriggerSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`
- Two trigger sources:

**Source 1 -- CinematicTrigger entities (zone triggers):**
- Query: `CinematicTrigger` + `LocalToWorld` where `HasPlayed == false` (or `PlayOnce == false`)
- Overlap sphere (`TriggerRadius`) against player entities
- When player enters radius: broadcast `CinematicPlayRpc` to all connected clients
- If `PlayOnce`: set `HasPlayed = true`

**Source 2 -- EncounterTriggerSystem integration:**
- `TriggerActionType.PlayCinematic = 16` added to enum
- `EncounterTriggerSystem` case handler calls `CinematicTriggerSystem.TriggerCinematic(int cinematicId)` static method
- Static method creates `CinematicPlayRpc` entity via ECB

### CinematicRpcReceiveSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicRpcReceiveSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `ClientSimulation` only
- Receives `CinematicPlayRpc`:
  - Writes `CinematicState` singleton: IsPlaying=true, CurrentCinematicId, CinematicType, CanSkip (derived from SkipPolicy != NoSkip), Duration, TotalPlayersInScene, ElapsedTime=0, SkipVotesReceived=0
- Receives `CinematicEndRpc`:
  - Writes `CinematicState` singleton: IsPlaying=false
- Receives `CinematicSkipUpdateRpc`:
  - Updates `CinematicState.SkipVotesReceived` for skip UI display

### CinematicPlaybackSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicPlaybackSystem.cs`

- **Managed SystemBase** (must be managed -- PlayableDirector is UnityEngine.Object)
- `PresentationSystemGroup`, `Client|Local`
- Core responsibilities:
  1. **Start playback**: When `CinematicState.IsPlaying` transitions false->true:
     - Lookup `CinematicDefinitionSO` via `CinematicRegistryManaged`
     - If TimelineAsset != null: instantiate PlayableDirector, assign TimelineAsset, Play()
     - If CinematicCameraPrefab != null: instantiate camera rig
     - If DialogueTreeId > 0: enqueue dialogue trigger event
     - If MusicStingerId > 0: create `SoundEventRequest` on Music bus
     - If VoiceLineClip != null: play on Dialogue bus via AudioSource
  2. **Update playback**: Each frame while IsPlaying:
     - `CinematicState.ElapsedTime += SystemAPI.Time.DeltaTime`
     - Read Timeline SignalReceiver callbacks: enqueue `CinematicAnimEvent` to `CinematicAnimEventQueue`
     - Process subtitle timing: push current subtitle to UI bridge
  3. **End playback**: When `CinematicState.IsPlaying` transitions true->false:
     - Stop PlayableDirector, destroy instance
     - Destroy camera rig instance
     - Clear `CinematicAnimEventQueue`

### CinematicInputLockSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicInputLockSystem.cs`

- Unmanaged ISystem (no managed dependencies), `SimulationSystemGroup`, `Client|Local`
- Reads `CinematicState` singleton each frame
- When `IsPlaying == true` AND `CinematicType != InWorldEvent`:
  - Disables player input by writing to existing input enable flag (same mechanism as death/stun)
- When `IsPlaying == false`:
  - Restores player input (only if this system disabled it -- tracks via `_wasLocked` bool)
- **Skip input exception**: Even when input is locked, the Skip key binding is still processed. `CinematicInputLockSystem` passes skip input through to `CinematicSkipInputSystem`.

### CinematicCameraSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicCameraSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`
- Camera blend follows `DialogueCameraSystem` handoff pattern
- **Blend In** (IsPlaying transitions true, FullCinematic type):
  1. Cache current gameplay camera transform
  2. Enable cinematic camera (from PlayableDirector or CinematicCameraPrefab)
  3. Interpolate via `PlayableGraph` weight blending over `BlendInDuration`
  4. At blend complete: fully disable `PlayerCameraControlSystem` output
  5. Write `CinematicState.BlendProgress = 1.0`
- **Blend Out** (IsPlaying transitions false):
  1. Begin blending cinematic camera weight down, gameplay camera weight up
  2. Interpolate over `BlendOutDuration`
  3. At blend complete: destroy cinematic camera, re-enable `PlayerCameraControlSystem`
  4. Write `CinematicState.BlendProgress = 0.0`
- **InWorldEvent / TextOverlay**: No camera blend (gameplay camera stays active)

### CinematicSkipSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicSkipSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`
- Maintains server-side skip state: `NativeHashSet<int> _skipVoters` (NetworkId of players who voted)
- Receives `CinematicSkipRpc`:
  1. Validate `CinematicId` matches active cinematic
  2. Add `NetworkId` to `_skipVoters` (dedup -- same player can't double-vote)
  3. Broadcast `CinematicSkipUpdateRpc` with updated vote count
  4. Evaluate against SkipPolicy:
     - `AnyoneCanSkip`: 1 vote sufficient
     - `MajorityVote`: votes > totalPlayers / 2
     - `AllMustSkip`: votes == totalPlayers
     - `NoSkip`: always reject
  5. If threshold met: broadcast `CinematicEndRpc(WasSkipped=true)`
  6. Clear `_skipVoters` on cinematic end

### CinematicEndSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicEndSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`
- Tracks server-side elapsed time for active cinematic
- When elapsed >= Duration: broadcast `CinematicEndRpc(WasSkipped=false)`
- Clears server-side cinematic state
- Handles edge cases:
  - Player disconnect during cinematic: recount TotalPlayers, re-evaluate skip threshold
  - Server shutdown: immediate end, no RPC (clients detect via disconnect)

### CinematicSkipInputSystem

**File:** `Assets/Scripts/Cinematic/Systems/CinematicSkipInputSystem.cs`

- Unmanaged ISystem, `SimulationSystemGroup`, `Client|Local`
- Only active when `CinematicState.IsPlaying == true` AND `CinematicState.CanSkip == true`
- Reads skip key input (default: Escape or Space)
- Creates `CinematicSkipRpc` entity via ECB (sends to server)
- Solo play (LocalSimulation): immediately sets `CinematicState.IsPlaying = false` (no RPC roundtrip)

---

## Authoring

### CinematicTriggerAuthoring

**File:** `Assets/Scripts/Cinematic/Authoring/CinematicTriggerAuthoring.cs`

```
[AddComponentMenu("DIG/Cinematic/Cinematic Trigger")]
```

- Fields: CinematicId (int), PlayOnce (bool), CinematicType (enum), SkipPolicy (enum), TriggerRadius (float, default 5.0)
- Baker adds: `CinematicTrigger` (12 bytes)
- Place on zone entry GameObjects, boss arena triggers, or story checkpoint entities
- No player prefab modification required

### CinematicTimelineBridge (MonoBehaviour)

**File:** `Assets/Scripts/Cinematic/Authoring/CinematicTimelineBridge.cs`

```
[AddComponentMenu("DIG/Cinematic/Timeline Bridge")]
[RequireComponent(typeof(PlayableDirector))]
```

- Placed on GameObjects with PlayableDirector in the scene
- `SignalReceiver` callback handler: converts Timeline Signal emissions to `CinematicAnimEvent` structs
- Enqueues to `CinematicAnimEventQueue`
- Signal asset types (custom SignalAsset subclasses):
  - `CinematicAnimSignal`: drives NPC animation
  - `CinematicVFXSignal`: spawns VFX
  - `CinematicSoundSignal`: plays sound
  - `CinematicDialogueSignal`: triggers dialogue node
  - `CinematicFadeSignal`: screen fade

---

## UI Bridge

**File:** `Assets/Scripts/Cinematic/Bridges/ICinematicUIProvider.cs`

```
ICinematicUIProvider
  void OnCinematicStart(int cinematicId, CinematicType type)
  void OnCinematicEnd(int cinematicId, bool wasSkipped)
  void UpdateSkipPrompt(bool canSkip, int votesReceived, int totalPlayers)
  void UpdateSubtitle(string text, float duration)
  void SetLetterbox(float targetHeight, float fadeDuration)
  void SetHUDVisible(bool visible, float fadeDuration)
  void UpdateProgress(float normalizedTime)
```

**File:** `Assets/Scripts/Cinematic/Bridges/CinematicUIRegistry.cs`

- Static provider registry (same pattern as `CombatUIRegistry`, `SaveUIRegistry`)
- MonoBehaviours register on enable, unregister on disable
- `HasCinematicUI` / `CinematicUI` properties

**File:** `Assets/Scripts/Cinematic/Systems/CinematicUIBridgeSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`
- Reads `CinematicState` singleton each frame
- On state transitions (IsPlaying changes): calls `OnCinematicStart` / `OnCinematicEnd`
- Each frame during playback:
  - `UpdateSkipPrompt(CanSkip, SkipVotesReceived, TotalPlayersInScene)`
  - `UpdateProgress(ElapsedTime / Duration)`
  - Subtitle timing: reads `CinematicDefinitionSO.SubtitleKeys[i]` when `ElapsedTime >= SubtitleTimings[i]`
- HUD control:
  - FullCinematic: `SetHUDVisible(false, HUDFadeDuration)` on start, `SetHUDVisible(true, HUDFadeDuration)` on end
  - TextOverlay: partial HUD hide (crosshair + action bar hidden, minimap stays)
  - InWorldEvent: no HUD change
- Letterbox:
  - FullCinematic: `SetLetterbox(LetterboxHeight, BlendInDuration)` on start, `SetLetterbox(0, BlendOutDuration)` on end
  - Others: no letterbox

---

## Editor Tooling

### CinematicWorkstationWindow

**File:** `Assets/Editor/CinematicWorkstation/CinematicWorkstationWindow.cs`

- Menu: `DIG/Cinematic Workstation`
- Sidebar + `ICinematicWorkstationModule` pattern (matches Progression/Persistence/SkillTree Workstations)

### Modules (4 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| Cinematic Browser | `Modules/CinematicBrowserModule.cs` | List all CinematicDefinitionSO assets with Type, SkipPolicy, Duration, DialogueTreeId. Filter by type. Drag-select to preview. |
| Timeline Linker | `Modules/TimelineLinkerModule.cs` | Assign TimelineAsset to CinematicDefinitionSO. Validate Timeline track bindings match scene NPC entities. Show signal asset summary per Timeline. |
| Playback Preview | `Modules/PlaybackPreviewModule.cs` | Play-mode only: "Play Cinematic" dropdown (all registered IDs). Preview camera blend, letterbox, skip prompt. Timeline scrub slider. Force skip button. |
| Debug Overlay | `Modules/DebugOverlayModule.cs` | Play-mode only: live CinematicState values (IsPlaying, ElapsedTime, BlendProgress, SkipVotes). Server-side cinematic tracker. RPC log. |

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `CinematicBootstrapSystem` | N/A | No | Runs once at startup, loads from Resources |
| `CinematicTriggerSystem` | < 0.02ms | No | Overlap sphere check, rare (zone entry only) |
| `CinematicRpcReceiveSystem` | < 0.01ms | No | RPC receive, singleton write |
| `CinematicInputLockSystem` | < 0.01ms | Yes | Single bool read + conditional write |
| `CinematicCameraSystem` | < 0.05ms | No | Managed, PlayableGraph weight interpolation |
| `CinematicPlaybackSystem` | < 0.10ms | No | Managed, drives PlayableDirector + signal callbacks |
| `CinematicSkipSystem` | < 0.01ms | No | HashSet lookup, rare (skip events only) |
| `CinematicEndSystem` | < 0.01ms | No | Timer comparison |
| `CinematicUIBridgeSystem` | < 0.03ms | No | Managed, provider dispatch |
| `CinematicSkipInputSystem` | < 0.01ms | Yes | Input read, conditional RPC |
| **Total (during playback)** | **< 0.25ms** | | All systems combined |
| **Total (idle)** | **< 0.02ms** | | Most systems early-out when not playing |

---

## File Summary

### New Files (26)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Cinematic/Components/CinematicState.cs` | IComponentData + enums (CinematicType, SkipPolicy) |
| 2 | `Assets/Scripts/Cinematic/Components/CinematicTrigger.cs` | IComponentData |
| 3 | `Assets/Scripts/Cinematic/Components/CinematicRpcs.cs` | IRpcCommand (Play, Skip, End, SkipUpdate) |
| 4 | `Assets/Scripts/Cinematic/Components/CinematicConfigSingleton.cs` | IComponentData singleton |
| 5 | `Assets/Scripts/Cinematic/Components/CinematicRegistryManaged.cs` | Managed singleton |
| 6 | `Assets/Scripts/Cinematic/Components/CinematicAnimEvent.cs` | Struct + enum (event types) |
| 7 | `Assets/Scripts/Cinematic/Definitions/CinematicDefinitionSO.cs` | ScriptableObject |
| 8 | `Assets/Scripts/Cinematic/Definitions/CinematicDatabaseSO.cs` | ScriptableObject |
| 9 | `Assets/Scripts/Cinematic/Systems/CinematicBootstrapSystem.cs` | SystemBase (Client|Local, runs once) |
| 10 | `Assets/Scripts/Cinematic/Systems/CinematicTriggerSystem.cs` | SystemBase (Server|Local) |
| 11 | `Assets/Scripts/Cinematic/Systems/CinematicRpcReceiveSystem.cs` | SystemBase (ClientSimulation) |
| 12 | `Assets/Scripts/Cinematic/Systems/CinematicPlaybackSystem.cs` | SystemBase (managed, Client|Local) |
| 13 | `Assets/Scripts/Cinematic/Systems/CinematicInputLockSystem.cs` | ISystem (Client|Local) |
| 14 | `Assets/Scripts/Cinematic/Systems/CinematicCameraSystem.cs` | SystemBase (managed, Client|Local) |
| 15 | `Assets/Scripts/Cinematic/Systems/CinematicSkipSystem.cs` | SystemBase (Server|Local) |
| 16 | `Assets/Scripts/Cinematic/Systems/CinematicEndSystem.cs` | SystemBase (Server|Local) |
| 17 | `Assets/Scripts/Cinematic/Systems/CinematicSkipInputSystem.cs` | ISystem (Client|Local) |
| 18 | `Assets/Scripts/Cinematic/Systems/CinematicUIBridgeSystem.cs` | SystemBase (managed, Client|Local) |
| 19 | `Assets/Scripts/Cinematic/Bridges/ICinematicUIProvider.cs` | Interface |
| 20 | `Assets/Scripts/Cinematic/Bridges/CinematicUIRegistry.cs` | Static provider registry |
| 21 | `Assets/Scripts/Cinematic/Bridges/CinematicAnimEventQueue.cs` | Static NativeQueue bridge |
| 22 | `Assets/Scripts/Cinematic/Authoring/CinematicTriggerAuthoring.cs` | Baker |
| 23 | `Assets/Scripts/Cinematic/Authoring/CinematicTimelineBridge.cs` | MonoBehaviour (SignalReceiver handler) |
| 24 | `Assets/Scripts/Cinematic/DIG.Cinematic.asmdef` | Assembly definition |
| 25 | `Assets/Editor/CinematicWorkstation/CinematicWorkstationWindow.cs` | EditorWindow |
| 26 | `Assets/Editor/CinematicWorkstation/ICinematicWorkstationModule.cs` | Interface |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/AI/Components/EncounterTrigger.cs` | Add `PlayCinematic = 16` to `TriggerActionType` enum |
| 2 | `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` | Add case handler for `PlayCinematic` -- calls `CinematicTriggerSystem.TriggerCinematic(cinematicId)` |
| 3 | `Assets/Scripts/AI/Editor/EncounterSimulator.cs` | Add simulation case for `PlayCinematic` |
| 4 | `Assets/Scripts/AI/Editor/EncounterValidator.cs` | Add validation for PlayCinematic trigger (CinematicId exists in database) |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/CinematicDatabase.asset` |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `CinematicState` | 24 bytes | Dedicated singleton entity (Client world) |
| `CinematicConfigSingleton` | 20 bytes | Dedicated singleton entity (Client world) |
| `CinematicTrigger` | 12 bytes | Trigger zone entities (not player) |
| **Total on player** | **0 bytes** | |

CinematicState is a client-world singleton -- not a component on the player entity. CinematicTrigger lives on encounter/zone entities. Zero impact on the player ghost archetype.

---

## Multiplayer

### Server-Authoritative Trigger Model

- ALL cinematic triggers evaluated on server. Server decides when to play, broadcasts `CinematicPlayRpc`.
- Clients NEVER self-trigger cinematics (prevents desync, exploit via trigger skipping).
- `CinematicTriggerSystem`: `WorldSystemFilterFlags.ServerSimulation | LocalSimulation`.
- `CinematicSkipSystem`: `WorldSystemFilterFlags.ServerSimulation | LocalSimulation`.

### Skip Vote Coordination

- Client sends `CinematicSkipRpc` to server with `NetworkId`.
- Server validates: active cinematic, player not already voted, SkipPolicy != NoSkip.
- Server broadcasts `CinematicSkipUpdateRpc` with updated vote count (all clients update UI).
- Threshold evaluation per SkipPolicy:
  - `AnyoneCanSkip`: 1 vote, immediate end.
  - `MajorityVote`: > 50% of `TotalPlayersInScene`.
  - `AllMustSkip`: 100% of players.
  - `NoSkip`: skip RPCs silently discarded.
- On threshold: server broadcasts `CinematicEndRpc(WasSkipped=true)`.

### Player Disconnect During Cinematic

- Server detects disconnect, decrements `TotalPlayersInScene`.
- Re-evaluates skip threshold with new total.
- Broadcasts updated `CinematicSkipUpdateRpc`.
- If all remaining players have voted: end cinematic.

### Late Join During Cinematic

- New player joins while cinematic is active.
- Server sends `CinematicPlayRpc` with `Duration` reduced by elapsed time.
- Client picks up cinematic mid-playback (PlayableDirector.time = elapsed).
- Late joiner gets CanSkip based on active SkipPolicy.

---

## Cross-EPIC Integration

| Source | EPIC | Integration |
|--------|------|-------------|
| `EncounterTriggerSystem` | 15.33 | `TriggerActionType.PlayCinematic = 16` triggers cinematic from boss encounter phases |
| `DialogueActionSystem` | 16.16 | Cinematics can trigger dialogue nodes via `CinematicDefinitionSO.DialogueTreeId` |
| `DialogueCameraSystem` | 16.16 | Pattern reference for camera handoff (CinematicCameraSystem follows same approach) |
| `AudioBusType.Dialogue` | Core Audio | Voice lines during cinematics play on Dialogue bus |
| `AudioBusType.Music` | Core Audio | Music stingers triggered by `MusicStingerId` |
| `SoundEventRequest` | 15.33 | VFX/sound events from Timeline signals create `SoundEventRequest` entities |
| `VFXRequest` | 16.7 | Timeline VFX signals create `VFXRequest` entities via `CinematicAnimEventQueue` |
| `QuestRewardSystem` | 16.12 | Quest completion can trigger celebration cinematic via CinematicTrigger |
| `PlayerCameraControlSystem` | Core | Camera control yielded during FullCinematic, restored on end |
| `SaveSystem` | 16.15 | NO autosave during cinematic (prevents saving locked input state) |
| `CinematicAnimEventQueue` | 17.9 | Bridges Timeline signals to NPC animation and VFX systems |

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| No CinematicDatabaseSO in Resources | Warning at bootstrap | All systems early-out, zero overhead |
| Entity without CinematicTrigger | No trigger | Zero cost -- systems only query CinematicTrigger entities |
| No ICinematicUIProvider registered | Warning at frame 120 | Systems run, UI not displayed (playback still works) |
| Existing EncounterTrigger SOs | Unchanged | PlayCinematic (=16) is new -- no existing triggers use it |
| Single-player (LocalSimulation) | Skip is instant | No RPC roundtrip, CinematicSkipInputSystem sets state directly |

---

## Verification Checklist

### Bootstrap
- [ ] CinematicBootstrapSystem creates CinematicState singleton on client world
- [ ] CinematicBootstrapSystem creates CinematicConfigSingleton with database values
- [ ] CinematicRegistryManaged populated with all CinematicDefinitionSO entries
- [ ] Bootstrap self-disables after first run

### Trigger
- [ ] CinematicTriggerAuthoring bakes CinematicTrigger (12 bytes) on entity
- [ ] Zone trigger: player enters radius -> CinematicPlayRpc broadcast
- [ ] PlayOnce: trigger fires once, second entry ignored (HasPlayed=true)
- [ ] EncounterTrigger.PlayCinematic (=16): boss phase transition triggers cinematic
- [ ] Server-authoritative: client cannot self-trigger cinematic

### Playback -- FullCinematic
- [ ] CinematicPlayRpc received -> CinematicState.IsPlaying = true
- [ ] PlayableDirector instantiated and started with correct TimelineAsset
- [ ] Camera blends from gameplay to cinematic over BlendInDuration
- [ ] Player input disabled during playback
- [ ] HUD fades out over HUDFadeDuration
- [ ] Letterbox bars animate to LetterboxHeight
- [ ] Subtitle text appears at correct timings
- [ ] Voice line plays on AudioBusType.Dialogue
- [ ] Music stinger plays on AudioBusType.Music
- [ ] Natural end: PlayableDirector stops, CinematicState.IsPlaying = false
- [ ] Camera blends back to gameplay over BlendOutDuration
- [ ] Player input restored
- [ ] HUD fades back in
- [ ] Letterbox bars animate to zero

### Playback -- InWorldEvent
- [ ] NPC animations play via Timeline
- [ ] Player camera NOT taken over
- [ ] Player input NOT locked
- [ ] HUD remains visible
- [ ] No letterbox bars

### Playback -- TextOverlay
- [ ] Text overlay with subtitles displayed
- [ ] Player input locked
- [ ] Partial HUD hide (crosshair + action bar hidden, minimap stays)
- [ ] No camera takeover
- [ ] Voice line plays

### Skip -- Single Player
- [ ] Press skip key -> cinematic ends immediately (no RPC roundtrip)
- [ ] Camera blend out begins
- [ ] Unskippable (NoSkip): skip key ignored

### Skip -- Multiplayer
- [ ] AnyoneCanSkip: 1 vote -> cinematic ends for all players
- [ ] MajorityVote: <50% voted -> cinematic continues, UI shows "1/4 voted to skip"
- [ ] MajorityVote: >50% voted -> cinematic ends for all players
- [ ] AllMustSkip: all players vote -> cinematic ends
- [ ] NoSkip: skip RPCs silently discarded
- [ ] Double-vote prevention: same player voting twice doesn't double-count
- [ ] Skip vote UI updates in real-time on all clients

### Dialogue Integration
- [ ] CinematicDefinitionSO.DialogueTreeId > 0: dialogue triggered during cinematic
- [ ] Dialogue nodes execute actions (EPIC 16.16 DialogueActionSystem)
- [ ] Dialogue camera mode respected within cinematic (if applicable)

### Timeline Signal Bridge
- [ ] CinematicAnimSignal: NPC entity animation driven from Timeline
- [ ] CinematicVFXSignal: VFX spawned at correct position/time
- [ ] CinematicSoundSignal: sound effect played
- [ ] CinematicDialogueSignal: dialogue node triggered mid-cinematic
- [ ] CinematicFadeSignal: screen fade to/from black

### Multiplayer Edge Cases
- [ ] Player disconnect during cinematic: TotalPlayers updated, skip re-evaluated
- [ ] Late join during cinematic: player catches up mid-playback
- [ ] All players disconnect: server cleans up cinematic state

### Performance
- [ ] Idle (no cinematic): total system cost < 0.02ms
- [ ] During playback: total system cost < 0.25ms
- [ ] No GC allocations during steady-state playback
- [ ] PlayableDirector pooled/destroyed correctly (no leaked GameObjects)

### Archetype
- [ ] Zero bytes added to player entity archetype
- [ ] CinematicState is client-world singleton only
- [ ] No new ghost components, no IBufferElementData on player
- [ ] Existing ghost bake unaffected

### Editor
- [ ] Cinematic Workstation: browser lists all CinematicDefinitionSO assets
- [ ] Cinematic Workstation: Timeline Linker validates track bindings
- [ ] Cinematic Workstation: Playback Preview plays cinematic in play-mode
- [ ] Cinematic Workstation: Debug Overlay shows live CinematicState values
