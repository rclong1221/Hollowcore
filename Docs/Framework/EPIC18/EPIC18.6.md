# EPIC 18.6: Scene Management & Level Flow System

**Status:** IMPLEMENTED
**Priority:** High (Core infrastructure — every game needs scene transitions)
**Dependencies:**
- `GameBootstrap` (existing — `Assets/Scripts/Core/GameBootstrap.cs`, NetCode bootstrap, manual world creation, subscene loading)
- `SubsceneLoadHelper` (existing — `Assets/Scripts/Core/GameBootstrap.cs`, coroutine-based subscene loading with timeout)
- `LobbyToGameTransition` (existing — `DIG.Lobby`, `Assets/Scripts/Lobby/LobbyToGameTransition.cs`, lobby → game scene flow)
- `TransitionLoadingPanel` (existing — `DIG.Lobby.UI`, `Assets/Scripts/Lobby/UI/TransitionLoadingPanel.cs`, loading screen)
- `LobbyManager` (existing — `DIG.Lobby`, game phase management)
- `InputContextManager` (existing — `DIG.Core.Input`, context switching on scene transitions)
- Unity Addressables (`UnityEngine.AddressableAssets`)
- Unity Scenes (`Unity.Scenes`)
- Unity NetCode (`Unity.NetCode`)

**Feature:** A modular scene management system with a state-machine-driven game flow, async scene loading with progress reporting, configurable loading screens, additive scene support, deterministic scene lifecycle hooks (OnSceneWillLoad/DidLoad/WillUnload/DidUnload), network-aware scene transitions for multiplayer, and designer tooling for defining level sequences, transition rules, and loading screen variants.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `GameBootstrap` | `Assets/Scripts/Core/GameBootstrap.cs` | Working | NetCode bootstrap, creates Server/Client worlds, deferred connection. No scene flow management |
| `SubsceneLoadHelper` | `Assets/Scripts/Core/GameBootstrap.cs` | Working | Coroutine loads subscenes with 100-frame timeout, blocks on import. Hardcoded port 7979 |
| `LobbyToGameTransition` | `Assets/Scripts/Lobby/LobbyToGameTransition.cs` | Implemented | Lobby-specific transition logic |
| `TransitionLoadingPanel` | `Assets/Scripts/Lobby/UI/TransitionLoadingPanel.cs` | Implemented | Loading screen UI for lobby → game |
| `InputContextManager` | `Assets/Scripts/Core/Input/InputContextManager.cs` | Fully implemented | Switches input context on transitions |

### What's Missing

- **No game flow state machine** — transitions between MainMenu → Lobby → Loading → Gameplay → Results → MainMenu are hardcoded in `LobbyManager` and `GameBootstrap`
- **No generic scene loader** — `SubsceneLoadHelper` is embedded in `GameBootstrap`, tightly coupled to NetCode world creation
- **No loading screen framework** — `TransitionLoadingPanel` is lobby-specific; no generic loading screen with progress bar, tips, art
- **No additive scene support** — no way to load environment scenes additively (base terrain + dungeon interior + weather overlay)
- **No scene lifecycle hooks** — no standardized WillLoad/DidLoad/WillUnload/DidUnload events for systems to react to
- **No scene transition animations** — no fade-to-black, crossfade, or wipe transitions between scenes
- **No level sequence definition** — no designer-configurable "campaign" or "level order" asset
- **No network-synchronized transitions** — no protocol for ensuring all clients are ready before scene transition completes

---

## Problem

DIG's scene management is procedural: `GameBootstrap.CreateHost()` creates worlds, `SubsceneLoadHelper` loads subscenes in a coroutine, `LobbyToGameTransition` handles one specific transition. Adding a new game phase (e.g., character select, results screen, world map) requires modifying multiple files and understanding the exact NetCode world lifecycle. There's no way for a designer to configure the game flow (Main Menu → Character Select → Lobby → Loading → Gameplay → Results) without code changes.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  GameFlowDefinitionSO         SceneDefinitionSO          LoadingScreenProfileSO
  (state machine graph:        (scene asset ref,           (background art,
   states + transitions,        load mode: single/         tip pool, progress bar
   conditions, data passing)    additive/subscene,         style, min display time)
                                required subscenes)
        |                         |                          |
        └──── SceneService (singleton MonoBehaviour) ────────┘
              (state machine execution, async scene loading,
               loading screen management, lifecycle events)
                         |
        ┌────────────────┼────────────────────┐
        |                |                    |
  GameFlowSM         SceneLoader          LoadingScreenManager
  (evaluates          (SceneManager +       (shows/hides loading
   transitions,        SubScene loading,    screen, progress
   fires events,       progress callback,   reporting, tip
   carries data        timeout/retry)       rotation, animation)
   between states)
        |                |                    |
        └────────────────┼────────────────────┘
                         |
              Network Synchronization Layer
                         |
  SceneTransitionNetworkSystem (NetCode)
  (server broadcasts scene change → all clients ACK →
   server waits for all ACKs → transition proceeds)
                         |
                 EDITOR TOOLING
                         |
  SceneWorkstationWindow (DIG > Scene Workstation)
  ├── SetupWizardModule      — create assets, Refresh Status
  ├── FlowGraphModule        — visual state machine
  ├── SceneAssignmentModule  — scene validation
  ├── LoadingScreenPreviewModule
  └── TransitionTesterModule  — Play mode testing
```

---

## Core Types

### GameFlowState

```csharp
[Serializable]
public class GameFlowState
{
    public string StateId;                  // "MainMenu", "Lobby", "Loading", "Gameplay", "Results"
    public SceneDefinitionSO Scene;        // Scene to load for this state
    public SceneDefinitionSO[] AdditiveScenes; // Additional scenes loaded alongside
    public LoadingScreenProfileSO LoadingScreen; // Loading screen config (null = no loading screen)
    public bool RequiresNetwork;           // Must have server/client worlds active
    public InputContext InputContext;       // Input context to activate
    public string OnEnterEvent;            // Event string fired on enter
    public string OnExitEvent;             // Event string fired on exit
}
```

### GameFlowTransition

```csharp
[Serializable]
public class GameFlowTransition
{
    public string FromState;
    public string ToState;
    public TransitionCondition Condition;  // Event-based, timer, or scripted
    public string TriggerEvent;            // Event that triggers this transition
    public TransitionAnimation Animation;  // Fade, CrossFade, Wipe, Cut
    public float AnimationDuration;        // Transition visual duration
}
```

---

## ScriptableObjects

### GameFlowDefinitionSO

**File:** `Assets/Scripts/SceneManagement/Config/GameFlowDefinitionSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| FlowName | string | "Default" | Display name |
| InitialState | string | "MainMenu" | State to enter on boot |
| States | GameFlowState[] | empty | All possible game states |
| Transitions | GameFlowTransition[] | empty | Valid state transitions |
| DefaultLoadingScreen | LoadingScreenProfileSO | null | Fallback loading screen |
| DefaultTransitionAnimation | TransitionAnimation | Fade | Default visual transition |
| DefaultTransitionDuration | float | 0.5 | Default transition time |

### SceneDefinitionSO

**File:** `Assets/Scripts/SceneManagement/Config/SceneDefinitionSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| SceneId | string | "" | Unique identifier |
| DisplayName | string | "" | Shown in loading screen |
| SceneName | string | "" | Scene name as in Build Settings (used with SceneManager) |
| LoadMode | SceneLoadMode enum | Single | Single, Additive, SubScene |
| SubSceneGuids | string[] | empty | SubScene GUIDs for ECS scenes |
| RequiredSubscenes | string[] | empty | Must be loaded before scene is "ready" |
| UnloadPrevious | bool | true | Unload previous scene on load |
| MinLoadTimeSeconds | float | 0 | Minimum loading screen display time |

### LoadingScreenProfileSO

**File:** `Assets/Scripts/SceneManagement/Config/LoadingScreenProfileSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| BackgroundSprites | Sprite[] | empty | Random background art per load |
| Tips | string[] | empty | Tip text pool, random selection |
| ShowProgressBar | bool | true | Display loading progress |
| ProgressBarStyle | ProgressBarStyle enum | Continuous | Continuous, Stepped, Indeterminate |
| MinDisplaySeconds | float | 1.0 | Minimum time to show loading screen |
| FadeInDuration | float | 0.3 | Loading screen fade-in time |
| FadeOutDuration | float | 0.3 | Loading screen fade-out time |
| MusicClip | AudioClip | null | Music during loading |

---

## SceneService

**File:** `Assets/Scripts/SceneManagement/SceneService.cs`

- MonoBehaviour singleton, `DontDestroyOnLoad`, `[DefaultExecutionOrder(-350)]`
- Loads `GameFlowDefinitionSO` from Resources at Awake
- API:
  - `void RequestTransition(string toState, object data = null)` — trigger a state change
  - `void FireEvent(string eventName)` — fire event that may trigger transitions
  - `string CurrentState` — active state ID
  - `bool IsLoading` — true during scene load
  - `float LoadProgress` — 0-1 loading progress
  - `event Action<string, string> OnStateChanged` — (fromState, toState)
  - `event Action<string> OnSceneWillLoad` — before scene starts loading
  - `event Action<string> OnSceneDidLoad` — after scene fully loaded
  - `event Action<string> OnSceneWillUnload` — before scene unloads
  - `event Action<string> OnSceneDidUnload` — after scene unloaded

### Scene Loading Pipeline

```
RequestTransition("Gameplay")
  |
  ├─ Validate transition (fromState → toState exists in GameFlowDefinitionSO)
  ├─ Fire OnSceneWillUnload(currentState)
  ├─ Play transition-out animation (fade to black)
  ├─ Show loading screen (if configured)
  |
  ├─ SceneLoader.LoadAsync(sceneDefinition)
  |    ├─ If Single: SceneManager.LoadSceneAsync + unload previous
  |    ├─ If Additive: SceneManager.LoadSceneAsync (additive)
  |    ├─ If SubScene: SceneSystem.LoadSceneAsync per GUID
  |    ├─ Progress reported via callback → loading screen progress bar
  |    └─ Wait for all required subscenes to be loaded
  |
  ├─ Fire OnSceneDidLoad(newState)
  ├─ Wait for MinLoadTime if not yet elapsed
  ├─ Fade out loading screen
  ├─ Play transition-in animation
  ├─ Switch InputContext
  ├─ Fire OnStateChanged(oldState, newState)
  └─ Update CurrentState
```

### Network-Aware Transitions

- For states with `RequiresNetwork = true`:
  - Server sends `SceneTransitionRPC` to all clients
  - Each client loads scene, sends `SceneReadyRPC` back
  - Server waits for all connected clients to ACK (with configurable timeout)
  - Server broadcasts `SceneTransitionCompleteRPC`
  - All clients simultaneously remove loading screen
- This ensures all players enter gameplay at the same time

---

## Network Components

### SceneTransitionRPCs

**File:** `Assets/Scripts/SceneManagement/Network/SceneTransitionRPCs.cs`

```
SceneTransitionRequest (IRpcCommand)
  TargetStateHash : int    // Hash of target state ID
  TransitionId    : uint   // Unique ID for this transition

SceneReadyAck (IRpcCommand)
  TransitionId    : uint   // Matches the request

SceneTransitionComplete (IRpcCommand)
  TransitionId    : uint
```

---

## Integration Points

### GameBootstrap Integration

- `GameBootstrap.CreateHost()` and `CreateClient()` become internal to `SceneService`
- When transitioning to a `RequiresNetwork` state, `SceneService` calls bootstrap logic
- Existing `GameBootstrap` preserved as a compatibility layer; `SceneService` wraps its functionality

### LobbyManager Integration

- `LobbyToGameTransition` replaced by a `GameFlowTransition` from "Lobby" to "Gameplay"
- `LobbyManager` calls `SceneService.RequestTransition("Gameplay")` instead of direct scene loading

---

## Editor Tooling

**Window:** `DIG > Scene Workstation` — `Assets/Editor/SceneWorkstation/SceneWorkstationWindow.cs`

| Module | File | Purpose |
|--------|------|---------|
| **Setup Wizard** | `SetupWizardModule.cs` | Create GameFlowDefinition, LoadingScreenProfile, prefab, SceneDefinitions. Refresh Status button; status refreshes every 2s or on demand |
| **Flow Graph** | `FlowGraphModule.cs` | Visual state machine (states as nodes, transitions as arrows). Cached layout for performance |
| **Scene Assignment** | `SceneAssignmentModule.cs` | Inspect and validate scene assignments, Build Settings |
| **Loading Preview** | `LoadingScreenPreviewModule.cs` | Preview loading screen profile (background, tips, progress bar style) |
| **Transition Tester** | `TransitionTesterModule.cs` | Trigger transitions and fire events in Play mode |

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/SceneManagement/SceneService.cs` | MonoBehaviour | ~350 |
| `Assets/Scripts/SceneManagement/SceneLoader.cs` | Class | ~200 |
| `Assets/Scripts/SceneManagement/LoadingScreenManager.cs` | MonoBehaviour | ~150 |
| `Assets/Scripts/SceneManagement/GameFlowStateMachine.cs` | Class | ~120 |
| `Assets/Scripts/SceneManagement/Config/GameFlowDefinitionSO.cs` | ScriptableObject | ~60 |
| `Assets/Scripts/SceneManagement/Config/SceneDefinitionSO.cs` | ScriptableObject | ~40 |
| `Assets/Scripts/SceneManagement/Config/LoadingScreenProfileSO.cs` | ScriptableObject | ~35 |
| `Assets/Scripts/SceneManagement/Network/SceneTransitionRPCs.cs` | RPCs | ~30 |
| `Assets/Scripts/SceneManagement/Network/SceneTransitionNetworkSystem.cs` | ISystem | ~120 |
| `Assets/Scripts/SceneManagement/UI/LoadingScreenView.cs` | UIView | ~100 |
| `Assets/Editor/SceneWorkstation/SceneWorkstationWindow.cs` | Editor | ~85 |
| `Assets/Editor/SceneWorkstation/Modules/SetupWizardModule.cs` | Editor | ~570 |
| `Assets/Editor/SceneWorkstation/Modules/FlowGraphModule.cs` | Editor | ~225 |
| `Assets/Editor/SceneWorkstation/Modules/SceneAssignmentModule.cs` | Editor | ~190 |
| `Assets/Editor/SceneWorkstation/Modules/LoadingScreenPreviewModule.cs` | Editor | ~120 |
| `Assets/Editor/SceneWorkstation/Modules/TransitionTesterModule.cs` | Editor | ~130 |

**Total estimated:** ~1,505 lines

---

## Optimization Report

### Summary

The EPIC 18.6 implementation is appropriate for its role: a managed orchestrator that bridges MonoBehaviour, Unity SceneManager, and ECS. Most components cannot use Burst or DOTS jobs because they require main-thread access (coroutines, UI, MonoBehaviour singletons). The main optimization opportunities are: **reducing Debug.Log in builds**, **editor OnGUI performance**, **throttling UI progress updates**, and **reducing allocations in the network systems**.

### Priority Matrix

| # | Optimization | Impact | Effort | Priority |
|---|--------------|--------|--------|----------|
| 1 | Debug.Log conditional compile | High | Low | **P0** |
| 2 | SetupWizard RefreshStatus caching | High | Low | **P0** |
| 3 | FlowGraphModule Dictionary cache | Medium | Low | **P1** |
| 4 | Progress update throttling | Low–Medium | Low | **P1** |
| 5 | SceneTransitionNetworkSystem allocation reduction | Medium | Medium | **P2** |
| 6 | SceneAssignmentModule IsSceneInBuild cache | Low | Low | **P2** |
| 7 | SceneLoader world.Update frequency | Medium | Medium | **P2** |
| 8 | GameFlowStateMachine lookup | Very Low | Low | **P3** (skip) |
| 9 | Async migration | Low | High | **P3** (skip) |

### 1. Debug.Log Removal for Production Builds

**Impact: High** (enables future Burst, reduces string allocs, avoids log I/O)

| File | Location | Issue |
|------|----------|-------|
| SceneService.cs | Lines 65, 84, 90, 97, 159, 212, 221, 309, 318 | Logs on init, transitions, errors |
| SceneLoader.cs | Lines 76, 85, 184 | Logs on errors |
| SceneTransitionServerSystem.cs | Lines 58, 66, 91 | Logs in OnUpdate hot path |
| SceneTransitionClientSystem.cs | Lines 141, 174, 194 | Logs in OnUpdate hot path |

**Recommendation:** Wrap all `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` or a custom `[Conditional("SCENE_DEBUG")]` wrapper. This:
- Removes string allocations in release builds
- Allows SceneTransitionServerSystem/ClientSystem to be considered for Burst (they still have managed SceneService access, but fewer barriers)
- Reduces I/O and log formatting cost

**Risk:** None if wrapped correctly. Debugging production issues would require a development build.

### 2. Editor: RefreshStatus() Called Every OnGUI Repaint

**Impact: High** (editor UI can feel sluggish when Scene Workstation is open)

**File:** `SetupWizardModule.cs`
**Issue:** `RefreshStatus()` is called at the start of every `OnGUI()`. It performs:
- `Resources.Load<GameFlowDefinitionSO>("GameFlowDefinition")`
- `Resources.Load<LoadingScreenProfileSO>("DefaultLoadingScreen")`
- `File.Exists(...)` for the prefab path

These run on every repaint (often 60+ times per second when the window is focused or during animations).

**Recommendation:**
- Cache `_hasFlowDef`, `_hasDefaultProfile`, `_hasLoadingPrefab` and only refresh when:
  - Window gains focus (`OnFocus`)
  - User clicks a "Refresh" button
  - After `CreateFlowDefinition`, `CreateDefaultLoadingProfile`, or `CreateLoadingScreenPrefab` completes
- Use `EditorApplication.delayCall` or `EditorApplication.update` (one-shot) to refresh after asset creation, then unsubscribe.

**Risk:** Low. Stale status is acceptable for a few seconds; user can click Refresh if needed.

### 3. Editor: FlowGraphModule Allocates Dictionary Every Frame

**Impact: Medium** (GC pressure when Flow Graph tab is visible)

**File:** `FlowGraphModule.cs` — `DrawTransitions()`
**Issue:** `var stateIndexMap = new Dictionary<string, int>()` is created and populated every time transitions are drawn (every repaint).

**Recommendation:**
- Cache `Dictionary<string, int> _stateIndexMap` as a field
- Rebuild only when `_flowDef` or `_flowDef.States` changes
- Or use a `List`/array indexed by state if order is stable

**Risk:** Low. Dictionary is small (typically <20 entries).

### 4. LoadingScreenManager / LoadingScreenView: Progress Update Throttling

**Impact: Low–Medium** (reduces Canvas rebuilds during loading)

**Files:** `SceneService.cs`, `LoadingScreenManager.cs`, `LoadingScreenView.cs`
**Issue:** `loadingMgr?.UpdateProgress(_loadProgress)` is invoked every frame during scene load. Each call updates `Slider.value`, which can trigger Canvas rebuild and layout.

**Recommendation:**
- Throttle progress updates to e.g. every 50–100 ms or when progress changes by >1%
- In `LoadingScreenManager.UpdateProgress()`, only call `_view.SetProgress()` if `Mathf.Abs(progress - _lastProgress) > 0.01f` or `Time.unscaledTime - _lastProgressTime > 0.05f`

**Risk:** Low. Progress bar may feel slightly less smooth; acceptable for loading screens.

### 5. SceneTransitionNetworkSystem: Allocations in OnUpdate

**Impact: Medium** (GC allocations every frame while waiting for acks)

**File:** `SceneTransitionNetworkSystem.cs`
**Issue:** When `_waitingForAcks` or when processing requests/completes:
- `ToComponentDataArray` and `ToEntityArray` allocate `Allocator.Temp` every frame
- `Allocator.Temp` is disposed at end of frame, but allocations still occur

**Recommendation:**
- Use `Entities.ForEach` with `EntityCommandBuffer` to destroy entities without materializing arrays
- Or use a singleton component to store "pending acks" and process via a single query with `ToEntityArray` only when needed
- NetCode RPC receive patterns often require iterating entities; consider whether a different receive pattern (e.g., `IRpcCommandRequest` handling) can avoid per-frame array allocation

**Risk:** Low. Pattern change must preserve RPC semantics.

### 6. Editor: SceneAssignmentModule IsSceneInBuild Per State

**Impact: Low–Medium** (minor editor lag with many states)

**File:** `SceneAssignmentModule.cs` — `DrawStateEntry()`
**Issue:** `IsSceneInBuild(state.Scene.SceneName)` is called for each state on every repaint. It iterates `EditorBuildSettings.scenes` each time.

**Recommendation:**
- Cache `EditorBuildSettings.scenes` and a `HashSet<string>` of scene names
- Rebuild cache when `EditorBuildSettings.scenes` changes (via `EditorBuildSettings.sceneListChanged`)
- Or compute once per `OnGUI` and pass results into `DrawStateEntry`

**Risk:** Low.

### 7. SceneLoader: world.Update() in SubScene Load Loop

**Impact: Medium** (runs full ECS world every frame during load)

**File:** `SceneLoader.cs` — `LoadSingleSubScene()`
**Issue:** `world.Update()` is called every frame while waiting for the SubScene to load. This runs the entire ECS simulation (all systems) for that world.

**Recommendation:**
- Consider `SceneSystem.GetLoadProgress()` if available (Unity 2022+) to avoid polling
- Or run only the minimal systems needed for scene loading (e.g., a custom system group)
- A lighter approach: call `world.Update()` less frequently (e.g., every 3rd frame) to reduce cost while still making progress.

**Risk:** Medium. Changing update frequency could affect load time. Needs profiling.

### Non-Goals (Do Not Optimize)

- **Burst for SceneTransitionServerSystem/ClientSystem:** They require managed access (SceneService, EntityManager.CreateEntity, RPC send). Burst is not feasible without a significant architectural change.
- **DOTS for SceneService/LoadingScreenManager:** These are MonoBehaviour singletons by design. Converting to ECS would not improve performance and would complicate the API.
- **Job scheduling for GameFlowStateMachine:** It runs on explicit calls (RequestTransition, FireEvent), not per-frame. No job parallelism applies.
- **SceneTransitionClientSystem managed access:** `SceneService.Instance` access makes Burst impossible. This is an inherent bridge between ECS and MonoBehaviour. An ECS singleton (`SceneTransitionState : IComponentData`) could replace it, but the added indirection isn't worth it unless the system needs Burst for other reasons.
- **Async migration from coroutines:** Coroutines are appropriate for this orchestration pattern. Migration to `async`/`await` would be a large refactor with no clear performance win.

---

## Performance Considerations

- Scene loading is fully async — never blocks the main thread
- Loading screen renders at minimal cost (single Canvas with static elements + progress bar)
- `SceneLoader` aggregates progress from multiple async operations into a single 0-1 float
- Network ACK system uses simple RPC exchange — no heavy serialization
- `GameFlowStateMachine` evaluates transitions only on event fire (not per-frame polling)
- **Debug.Log** — wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; no logs in release builds
- **LoadingScreenManager.UpdateProgress** — throttled (min 50ms interval or 1% delta) to reduce Canvas rebuilds
- **Setup Wizard** — status refreshes every 2 seconds or on "Refresh Status" click (not every repaint)
- **Flow Graph** — state index map cached; rebuilt only when flow definition changes

---

## Optimization Verification Checklist

- [ ] Debug.Log removal: Run a Development Build and Release Build; verify no logs in Release, logs still work in Dev
- [ ] Editor RefreshStatus: Open Scene Workstation, leave it focused for 10 seconds — no lag. Create an asset — status updates
- [ ] Progress throttling: Load a large scene; progress bar still animates smoothly; no visible stutter
- [ ] Network systems: Host + 2 clients; transition to Gameplay; all clients reach gameplay; no errors

---

## Testing Strategy

- Unit test `GameFlowStateMachine`: validate transitions, invalid transition rejection, data passing
- Unit test `SceneLoader` progress aggregation with mock async operations
- Integration test: MainMenu → Lobby → Gameplay flow with loading screen
- Integration test: network-synchronized transition with 2 clients
- Integration test: additive scene loading (base + overlay scenes)
- Editor test: `GameFlowWorkstationModule` graph editor creates valid flow definitions
