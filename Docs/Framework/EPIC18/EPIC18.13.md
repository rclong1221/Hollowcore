# EPIC 18.13: Death Camera & Post-Death Experience System

**Status:** PLANNED
**Priority:** High (Core multiplayer feel — directly impacts player experience every death)
**Dependencies:**
- `DeathState` / `DeathPhase` (existing — `Player.Components`, `Assets/Scripts/Player/Components/DeathState.cs`, Ghost replicated)
- `DeathPresentationSystem` (existing — `Player.Systems`, `Assets/Scripts/Player/Systems/DeathPresentationSystem.cs`, client-side death detection)
- `KillCredited` / `RecentAttackerElement` (existing — `Player.Components`, `Assets/Scripts/Player/Components/KillAttribution.cs`, kill attribution)
- `ICameraMode` (existing — `DIG.CameraSystem`, `Assets/Scripts/Camera/ICameraMode.cs`, camera mode interface)
- `CameraModeProvider` (existing — `DIG.CameraSystem`, `Assets/Scripts/Camera/CameraModeProvider.cs`, camera registry)
- `CameraTransitionManager` (existing — `DIG.CameraSystem`, `Assets/Scripts/Camera/CameraTransitionManager.cs`, smooth blends)
- `CameraManager` (existing — `Assets/Scripts/Systems/Camera/CameraManager.cs`, fallback camera)
- `CinemachineCameraController` (existing — `DIG.CameraSystem.Cinemachine`, `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs`)
- `InputContextManager` (existing — `DIG.Core.Input`, `Assets/Scripts/Core/Input/InputContextManager.cs`, input context stack)
- `RespawnSystem` (existing — `Player.Systems`, `Assets/Scripts/Player/Systems/RespawnSystem.cs`, Dead → Alive transition)
- `DownedRulesSystem` (existing — `Player.Systems`, `Assets/Scripts/Player/Systems/DownedRulesSystem.cs`, Downed → Dead/Alive)
- Unity NetCode (`Unity.NetCode`)
- Unity Entities (`Unity.Entities`)

**Feature:** A modular, phase-based death camera system that orchestrates the full post-death experience: kill cam, death recap screen, teammate spectating, and smooth respawn transition. Each phase is a pluggable module controlled by a data-driven configuration ScriptableObject. Integrates cleanly with DIG's existing camera infrastructure (`ICameraMode`, `CameraModeProvider`, `CameraTransitionManager`) instead of hacking around it. Supports per-game-mode presets for different death experiences (team deathmatch, elimination, battle royale). Includes designer-facing editor tooling for rapid configuration.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `DeathState` / `DeathPhase` | `Assets/Scripts/Player/Components/DeathState.cs` | Fully implemented | Ghost replicated (All), 4 phases: Alive/Downed/Dead/Respawning |
| `DeathPresentationSystem` | `Assets/Scripts/Player/Systems/DeathPresentationSystem.cs` | Fully implemented | Client-only Burst ISystem, detects Alive→Dead/Downed, sets `TriggerDeathEffect` one-frame flag |
| `DeathTransitionSystem` | `Assets/Scripts/Player/Systems/DeathTransitionSystem.cs` | Fully implemented | Server-only, Health≤0 → Downed, awards KillCredited/AssistCredited |
| `RespawnSystem` | `Assets/Scripts/Player/Systems/RespawnSystem.cs` | Fully implemented | Server-only, Dead→Alive after 5s delay, restores health, 3s invulnerability |
| `DownedRulesSystem` | `Assets/Scripts/Player/Systems/DownedRulesSystem.cs` | Fully implemented | Server-only, Downed→Dead (60s bleedout) or Downed→Alive (revive) |
| `KillCredited` | `Assets/Scripts/Player/Components/KillAttribution.cs` | Fully implemented | AllPredicted ghost, has Victim entity + VictimPosition + ServerTick |
| `RecentAttackerElement` | `Assets/Scripts/Player/Components/KillAttribution.cs` | Fully implemented | Buffer on player, Attacker + DamageDealt + Time (15s window) |
| `CombatState.LastAttacker` | `Assets/Scripts/Player/Components/KillAttribution.cs` | Fully implemented | AllPredicted ghost, entity reference to last attacker |
| `ICameraMode` | `Assets/Scripts/Camera/ICameraMode.cs` | Fully implemented | Polymorphic camera interface: Initialize, UpdateCamera, SetTarget, SetZoom, Shake |
| `CameraModeProvider` | `Assets/Scripts/Camera/CameraModeProvider.cs` | Fully implemented | Singleton registry, `SetActiveCamera(ICameraMode)`, events |
| `CameraTransitionManager` | `Assets/Scripts/Camera/CameraTransitionManager.cs` | Fully implemented | Smooth position/rotation/FOV blends with easing curves |
| `CameraManager` | `Assets/Scripts/Systems/Camera/CameraManager.cs` | Fully implemented | MonoBehaviour fallback, drives Camera.main, defers to Cinemachine |
| `CinemachineCameraController` | `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs` | Fully implemented | Singleton, manages Cinemachine virtual cameras from ECS data |
| `InputContextManager` | `Assets/Scripts/Core/Input/InputContextManager.cs` | Fully implemented | Context stack: Gameplay, UI, Spectator (EPIC 18.10) |
| `SpectatorCamera` (EPIC 18.10) | `Assets/Scripts/Replay/Spectator/SpectatorCamera.cs` | Implemented (replay/observer) | Multi-mode camera for replay playback and observer spectator — separate lifecycle from death cam |
| `DeathSpectatorTransitionSystem` | `Assets/Scripts/Replay/Spectator/DeathSpectatorTransitionSystem.cs` | **Prototype** | Quick hack — disables cameras via reflection, no kill cam, no UI, no transitions, wrong location |

### What's Missing

- **No kill cam** — player dies and immediately sees another player's view; no dramatic replay of the kill event
- **No death recap UI** — no information about who killed you, with what, or damage breakdown
- **No respawn timer UI** — no countdown to respawn visible during spectate
- **No smooth camera transitions** — hard cut between gameplay camera and spectator camera on death/respawn
- **No camera authority protocol** — current system hacks around `CameraManager` and `CinemachineCameraController` by disabling them via `FindFirstObjectByType` and reflection instead of using `CameraModeProvider`
- **No phase system** — death experience is a single monolithic state; can't configure kill cam duration, skip phases, or change behavior per game mode
- **No game mode awareness** — same death experience for team deathmatch, elimination, and battle royale
- **No configuration** — all timings hardcoded; designers can't tune the death experience without code changes
- **No editor tooling** — no workstation for previewing or configuring death camera sequences
- **No spectator HUD** — no indication of who you're watching, their health, or how to switch players
- **Wrong file location** — death-to-spectator code lives in `Replay/Spectator/` (a replay system concern) instead of its own feature directory

---

## Problem

The death experience is the moment players feel the game most. In AAA multiplayer games, death is a cinematic event: a dramatic kill cam shows what happened, a recap screen tells you who killed you and how, spectator mode lets you watch teammates, and a smooth transition brings you back on respawn. DIG currently hard-cuts to a teammate's view with no kill cam, no information, no UI, and no configurability. The existing prototype (`DeathSpectatorTransitionSystem`) bypasses the camera system via reflection hacks, lives in the wrong directory, and is a single monolithic class that can't be extended or configured without code changes. Designers cannot tune kill cam duration, toggle phases, or create game-mode-specific death experiences.

---

## Architecture Overview

```
                    DETECTION LAYER (existing)
  DeathPresentationSystem (ClientSimulation, PresentationSystemGroup)
  (detects Alive → Dead/Downed, sets TriggerDeathEffect one-frame flag)
        |
                    ORCHESTRATION LAYER (new)
  DeathCameraOrchestrator (SystemBase, ClientSimulation, PresentationSystemGroup)
  (reads DeathPresentationState.TriggerDeathEffect,
   acquires camera authority via CameraAuthorityGate,
   runs phase state machine: KillCam → DeathRecap → Spectator → RespawnTransition,
   releases authority on respawn)
        |
  ┌─────┴──────────────────────────────────────────────┐
  |                                                     |
  IDeathCameraPhase                              DeathCameraConfigSO
  (pluggable phase interface)                    (per-game-mode presets)
  |                                                     |
  ├── KillCamPhase                               DeathCameraPresetSO
  |   (orbit around kill position,               (elimination preset,
  |    slow-motion, skippable)                    battle royale preset,
  |                                               team deathmatch preset)
  ├── DeathRecapPhase
  |   (killer info UI, damage breakdown,
  |    respawn countdown, skip-to-spectate)
  |
  ├── SpectatorPhase
  |   (multi-mode camera: free/follow/FP/orbit,
  |    alive player list, Tab/1-9 cycling)
  |
  └── RespawnTransitionPhase
      (smooth blend back to gameplay camera
       via CameraTransitionManager)
        |
                    CAMERA LAYER
  DeathKillCam / DeathFollowCam / DeathFreeCam
  (implement ICameraMode — registered with CameraModeProvider,
   transitioned via CameraTransitionManager)
        |
                    AUTHORITY LAYER (new)
  CameraAuthorityGate (static)
  (CameraManager + CinemachineCameraController check this gate
   in their LateUpdate; if overridden, they yield control)
        |
                    UI LAYER (new, uGUI)
  DeathRecapView      SpectatorHUDView
  (killer info,       (followed player name,
   damage recap,       health bar, player list,
   respawn timer)      camera mode indicator)
        |
                    EDITOR LAYER (new)
  DeathCameraWorkstationWindow
  (config editor, phase sequence visualizer,
   game mode presets, preview simulation)
```

### Data Flow: Death Event

```
SERVER: Health ≤ 0
  → DeathTransitionSystem: Alive → Downed, awards KillCredited
  → RespawnSystem: starts respawn timer (5s default)

CLIENT: Ghost replication delivers DeathState.Phase change
  → DeathPresentationSystem: detects Alive → Dead/Downed
     sets TriggerDeathEffect = true (one-frame)
     sets IsDead = true (persistent until respawn)
  → DeathCameraOrchestrator.OnUpdate():
     1. Reads TriggerDeathEffect → enters death flow
     2. CameraAuthorityGate.Acquire(DeathCamera, priority=10)
     3. Reads KillCredited from local player → gets killer entity + kill position
     4. Reads RecentAttackerElement buffer → gets damage contributors
     5. Starts phase sequence from DeathCameraConfigSO

PHASE 1 — KillCam (if enabled, 3s default):
  → Instantiates DeathKillCam (ICameraMode)
  → CameraModeProvider.SetActiveCamera(killCam)
  → CameraTransitionManager.TransitionToCamera(killCam, 0.3s)
  → Orbits kill position, optional Time.timeScale slow-mo
  → Any button press → skip to next phase

PHASE 2 — DeathRecap (if enabled, until skip or timeout):
  → DeathRecapView.Show(killerName, weaponName, damageContributors, respawnCountdown)
  → Camera holds on kill cam position (or transitions to overhead)
  → "Skip to Spectate" button or timeout → next phase

PHASE 3 — Spectator (until respawn):
  → Queries alive players (GhostOwner + DeathState.Phase == Alive)
  → Instantiates DeathFollowCam (ICameraMode)
  → CameraModeProvider.SetActiveCamera(followCam)
  → CameraTransitionManager.TransitionToCamera(followCam, 0.5s)
  → SpectatorHUDView.Show(playerList, followedPlayerInfo)
  → Tab cycles camera mode, 1-9 follows specific player
  → InputContextManager.SetContext(DeathSpectator)
```

### Data Flow: Respawn Event

```
CLIENT: Ghost replication delivers DeathState.Phase → Alive
  → DeathPresentationSystem: IsDead becomes false
  → DeathCameraOrchestrator.OnUpdate():
     1. Detects Dead → Alive transition
     2. Starts RespawnTransitionPhase
     3. Hides all death UI (DeathRecapView, SpectatorHUDView)

PHASE 4 — RespawnTransition (0.5s):
  → Retrieves saved gameplay ICameraMode reference
  → CameraTransitionManager.TransitionToCamera(gameplayCamera, 0.5s)
  → On complete: CameraAuthorityGate.Release(DeathCamera)
  → InputContextManager.SetContext(Gameplay)
  → Normal camera systems resume automatically
```

---

## Architecture Decisions

### Why Phase-Based State Machine?

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| Monolithic system (current) | Simple, one file | Can't configure per game mode, can't skip phases, can't extend, can't reorder | **Rejected** |
| Phase-based with interface | Pluggable, configurable sequence, each phase testable in isolation, designers control via SO | Slightly more files | **Chosen** |
| Timeline/sequencer | Most flexible, visual editor | Over-engineered for 4 phases, complex runtime | Deferred to future |

### Why CameraAuthorityGate Instead of Direct Disable?

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| Disable CameraManager via reflection (current) | Works | Fragile, breaks if class names change, doesn't integrate with camera system | **Rejected** |
| ICameraMode + CameraModeProvider handoff | Clean integration, uses existing APIs | CameraManager doesn't use CameraModeProvider — drives Camera.main directly | Insufficient alone |
| CameraAuthorityGate (static flag) | Minimal change to CameraManager (1 line), works with CinemachineCameraController, no reflection | Adds a static dependency | **Chosen** |

The gate is a 3-line static class. `CameraManager.LateUpdate()` adds one `if` check at the top. Zero performance cost. Zero reflection.

### Why Separate ICameraMode Implementations (Not Reusing SpectatorCamera)?

`SpectatorCamera` (EPIC 18.10) is designed for **replay playback and observer spectating** — it reads ghost data directly from ECS, manages its own player list, and bypasses the camera infrastructure. The death camera needs to **integrate** with `ICameraMode`/`CameraModeProvider`/`CameraTransitionManager` for smooth transitions and proper camera authority. These are different lifecycles and different contracts. The existing `SpectatorCamera` remains untouched for replay/observer use.

---

## Core Types

### CameraAuthorityGate

**File:** `Assets/Scripts/DeathCamera/CameraAuthorityGate.cs`

Static utility that provides a clean protocol for overriding gameplay cameras.

```csharp
public static class CameraAuthorityGate
{
    public static bool IsOverridden { get; private set; }
    public static string CurrentOwner { get; private set; }
    public static int CurrentPriority { get; private set; }

    public static bool Acquire(string owner, int priority);
    public static void Release(string owner);
    public static void ForceRelease(); // Editor/debug only
}
```

| Method | Behavior |
|--------|----------|
| `Acquire("DeathCamera", 10)` | Sets `IsOverridden = true` if priority ≥ current. Returns false if higher priority already holds. |
| `Release("DeathCamera")` | Clears override only if caller matches current owner. |
| `ForceRelease()` | Unconditional clear — for editor reset and error recovery. |

**Integration points (1-line additions):**

`CameraManager.LateUpdate()`:
```csharp
if (CameraAuthorityGate.IsOverridden) return; // line 99, before any camera work
```

`CinemachineCameraController.LateUpdate()`:
```csharp
if (CameraAuthorityGate.IsOverridden) return; // line 268, before any camera work
```

Priority levels:
- `0` — Normal gameplay (implicit, no acquisition needed)
- `10` — Death camera
- `20` — Cutscene camera (future)
- `30` — Editor override (future)

---

### IDeathCameraPhase

**File:** `Assets/Scripts/DeathCamera/IDeathCameraPhase.cs`

```csharp
public interface IDeathCameraPhase
{
    DeathCameraPhaseType PhaseType { get; }
    void Enter(DeathCameraContext context);
    void Update(float deltaTime);
    void Exit();
    bool IsComplete { get; }
    bool CanSkip { get; }
    void Skip();
}
```

`DeathCameraContext` carries all data a phase might need:

```csharp
public class DeathCameraContext
{
    public Entity LocalPlayerEntity;
    public Entity KillerEntity;
    public float3 KillPosition;
    public float3 KillerPosition;
    public ushort KillerGhostId;
    public string KillerName;       // Resolved from ghost metadata
    public List<DamageContributor> DamageContributors;
    public float RespawnDelay;
    public float DeathTime;
    public DeathCameraConfigSO Config;
    public Camera TargetCamera;
    public ICameraMode PreviousCamera; // Saved for respawn restore
}

public struct DamageContributor
{
    public string Name;
    public float DamageDealt;
    public float TimeAgo;
}
```

---

### DeathCameraOrchestrator

**File:** `Assets/Scripts/DeathCamera/DeathCameraOrchestrator.cs`

The brain of the system. SystemBase (managed, ClientSimulation) that reads ECS death state and drives the phase state machine.

```csharp
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(global::Player.Systems.DeathPresentationSystem))]
public partial class DeathCameraOrchestrator : SystemBase
```

**State machine:**

| State | Entry Condition | Exit Condition |
|-------|----------------|----------------|
| `Inactive` | Default | `DeathPresentationState.TriggerDeathEffect == true` |
| `RunningPhases` | Death detected | All phases complete OR respawn detected |
| `WaitingForRespawn` | All phases complete but still dead | `DeathState.Phase == Alive` |
| `Inactive` | Respawn transition complete | — |

**Key responsibilities:**
1. Detect local player death (reads `DeathPresentationState` via `GhostOwnerIsLocal` query)
2. Build `DeathCameraContext` from `KillCredited`, `RecentAttackerElement`, `CombatState.LastAttacker`
3. Acquire camera authority via `CameraAuthorityGate.Acquire("DeathCamera", 10)`
4. Save reference to current `CameraModeProvider.ActiveCamera` for respawn restore
5. Instantiate phase sequence from `DeathCameraConfigSO.PhaseSequence`
6. Advance phases: call `Enter()`, `Update()`, check `IsComplete`, call `Exit()`, advance to next
7. Handle skip input (any key during `CanSkip` phases)
8. Detect respawn: `DeathState.Phase` transitions to `Alive`
9. Run `RespawnTransitionPhase`, then release authority

**Cached queries (OnCreate):**
- `_localPlayerQuery`: `DeathState + GhostOwnerIsLocal + GhostInstance`
- `_deathPresentationQuery`: `DeathPresentationState + GhostOwnerIsLocal`
- `_killCreditQuery`: `KillCredited + GhostOwnerIsLocal`
- `_alivePlayersQuery`: `GhostOwner + GhostInstance + DeathState`

**Zero per-frame allocation:** reuses `_context`, `_alivePlayerIds`, `_damageContributors` lists.

---

## Phase Specifications

### KillCamPhase

**File:** `Assets/Scripts/DeathCamera/Phases/KillCamPhase.cs`

Dramatic camera around the kill event. Orbits the kill position with optional slow motion.

| Field (from config) | Type | Default | Purpose |
|---------------------|------|---------|---------|
| `KillCamEnabled` | bool | `true` | Master toggle |
| `KillCamDuration` | float | `3.0` | Duration in seconds |
| `KillCamOrbitRadius` | float | `5.0` | Orbit distance from kill position |
| `KillCamOrbitHeight` | float | `3.0` | Camera height above kill position |
| `KillCamOrbitSpeed` | float | `30.0` | Degrees per second |
| `KillCamSlowMotion` | bool | `true` | Enable slow motion |
| `KillCamTimeScale` | float | `0.25` | Time.timeScale during kill cam |
| `KillCamTransitionIn` | float | `0.3` | Blend-in duration (seconds) |

**Behavior:**
1. `Enter()`: Create `DeathKillCam` (ICameraMode) targeting kill position. `CameraTransitionManager.TransitionToCamera(killCam, TransitionIn)`. If `SlowMotion`, set `Time.timeScale = KillCamTimeScale`.
2. `Update()`: Timer counts down. Camera orbits kill position.
3. `IsComplete`: Timer ≤ 0.
4. `Skip()`: Set timer to 0.
5. `Exit()`: Restore `Time.timeScale = 1.0`. Destroy `DeathKillCam`.

**CanSkip:** `true` — any key press advances to next phase.

---

### DeathRecapPhase

**File:** `Assets/Scripts/DeathCamera/Phases/DeathRecapPhase.cs`

Displays the death recap UI overlay with killer information and respawn countdown.

| Field (from config) | Type | Default | Purpose |
|---------------------|------|---------|---------|
| `DeathRecapEnabled` | bool | `true` | Master toggle |
| `DeathRecapDuration` | float | `5.0` | Max time before auto-advancing (0 = wait for skip) |
| `ShowDamageBreakdown` | bool | `true` | Show damage contributors list |
| `ShowRespawnTimer` | bool | `true` | Show countdown to respawn |

**Behavior:**
1. `Enter()`: `DeathRecapView.Instance.Show(context)`. Camera holds at kill cam position or gentle overhead drift.
2. `Update()`: Updates respawn countdown display. Timer counts down (if configured).
3. `IsComplete`: Timer ≤ 0, or player pressed skip, or respawn is imminent (< 1s remaining).
4. `Skip()`: Hide recap, advance to spectator.
5. `Exit()`: `DeathRecapView.Instance.Hide()`.

**CanSkip:** `true`.

**DeathRecapView layout (uGUI, bottom-center overlay):**
```
┌─────────────────────────────────────────────────┐
│                ELIMINATED BY                     │
│           [PlayerName]                           │
│         weapon/ability icon                      │
│                                                  │
│  Damage Contributors:                            │
│    PlayerA  ████████████  68%                    │
│    PlayerB  ████          32%                    │
│                                                  │
│         Respawning in 3...                       │
│                                                  │
│  [Skip to Spectate]                              │
└─────────────────────────────────────────────────┘
```

---

### SpectatorPhase

**File:** `Assets/Scripts/DeathCamera/Phases/SpectatorPhase.cs`

Multi-mode camera spectating alive teammates. Runs until respawn.

| Field (from config) | Type | Default | Purpose |
|---------------------|------|---------|---------|
| `SpectatorEnabled` | bool | `true` | Master toggle |
| `DefaultSpectatorMode` | DeathSpectatorMode | `Follow` | Initial camera mode |
| `AllowFreeCam` | bool | `true` | Allow free cam mode (may disable for anti-cheat) |
| `AllowFirstPerson` | bool | `true` | Allow first person view |
| `ShowSpectatorHUD` | bool | `true` | Show spectator overlay |
| `TransitionBetweenPlayers` | float | `0.5` | Blend duration when switching followed player |
| `SpectatorTransitionIn` | float | `0.5` | Blend-in duration from previous phase |

**Camera modes (DeathSpectatorMode enum):**
- `Follow` — Third-person orbit around followed player (uses `DeathFollowCam : ICameraMode`)
- `FirstPerson` — Locked to player position at eye level (reuses `DeathFollowCam` with distance=0)
- `FreeCam` — Free fly camera (uses `DeathFreeCam : ICameraMode`)
- `Orbit` — Auto-orbit around followed player

**Behavior:**
1. `Enter()`: Query alive players. Create appropriate `ICameraMode`. `CameraModeProvider.SetActiveCamera()`. `CameraTransitionManager.TransitionToCamera()`. Show `SpectatorHUDView`. `InputContextManager.SetContext(DeathSpectator)`.
2. `Update()`: Handle Tab (cycle mode), 1-9 (follow player), mouse/WASD (camera control). Refresh alive player list periodically (every 30 frames). Update HUD.
3. `IsComplete`: Never self-completes — runs until orchestrator detects respawn.
4. `Skip()`: No-op (can't skip spectator, must wait for respawn).
5. `Exit()`: Hide `SpectatorHUDView`. Destroy camera modes.

**CanSkip:** `false`.

**SpectatorHUDView layout (uGUI):**
```
┌────────────────────────────────┐  (top-center)
│  Spectating: [PlayerName]      │
│  ████████████████  HP 78/100   │
└────────────────────────────────┘

┌──────────┐  (bottom-left)           ┌──────────────┐  (bottom-right)
│ Players: │                          │ [Tab] Mode   │
│ 1. Alice │                          │ [1-9] Player │
│ 2. Bob   │                          │ [WASD] Move  │
│ 3. Carol │                          └──────────────┘
└──────────┘

                ┌─────────────────────────┐  (bottom-center)
                │   Respawning in 2...     │
                └─────────────────────────┘
```

---

### RespawnTransitionPhase

**File:** `Assets/Scripts/DeathCamera/Phases/RespawnTransitionPhase.cs`

Smooth camera blend back to gameplay camera on respawn.

| Field (from config) | Type | Default | Purpose |
|---------------------|------|---------|---------|
| `RespawnTransitionDuration` | float | `0.5` | Blend duration back to gameplay camera |
| `RespawnTransitionCurve` | AnimationCurve | EaseInOut | Easing curve for blend |

**Behavior:**
1. `Enter()`: Retrieve saved `context.PreviousCamera`. `CameraTransitionManager.TransitionToCamera(previousCamera, duration)`.
2. `Update()`: Wait for transition to complete.
3. `IsComplete`: `!CameraTransitionManager.Instance.IsTransitioning`.
4. `Exit()`: `CameraAuthorityGate.Release("DeathCamera")`. `InputContextManager.SetContext(Gameplay)`.

**CanSkip:** `false`.

---

## Camera Mode Implementations

### DeathKillCam

**File:** `Assets/Scripts/DeathCamera/Cameras/DeathKillCam.cs`

`MonoBehaviour` implementing `ICameraMode`. Orbits a fixed world position (the kill location).

```csharp
public class DeathKillCam : MonoBehaviour, ICameraMode
{
    public CameraMode Mode => CameraMode.ThirdPersonFollow; // Compatible mode for input system

    private float3 _targetPosition;
    private float _orbitAngle;
    private float _orbitRadius;
    private float _orbitHeight;
    private float _orbitSpeed;

    public void SetKillPosition(float3 position, float radius, float height, float speed);
}
```

- `UpdateCamera()`: Orbits `_targetPosition` at `_orbitRadius` distance, `_orbitHeight` above
- `GetCameraTransform()`: Returns this transform
- `SetTarget()`: Ignored (fixed on kill position)
- `SetZoom()` / `GetZoom()`: No-op
- `TransformMovementInput()`: Returns zero (no player movement during kill cam)
- `HandleRotationInput()`: Allows manual orbit override
- `SupportsOrbitRotation`: `true`
- `UsesCursorAiming`: `false`

---

### DeathFollowCam

**File:** `Assets/Scripts/DeathCamera/Cameras/DeathFollowCam.cs`

`MonoBehaviour` implementing `ICameraMode`. Third-person follow around a ghost entity, with first-person mode at distance 0.

```csharp
public class DeathFollowCam : MonoBehaviour, ICameraMode
{
    public CameraMode Mode => CameraMode.ThirdPersonFollow;

    private ushort _followedGhostId;
    private float _followDistance;
    private float _followHeight;
    private float _followSmoothTime;
    private float _orbitAngle;
    private float _pitch;
    private Vector3 _velocity;

    // Ghost data source — set by SpectatorPhase
    public System.Func<ushort, float3> GetGhostPosition;
    public System.Func<ushort, quaternion> GetGhostRotation;

    public void SetFollowTarget(ushort ghostId);
    public void SetFirstPerson(bool enabled); // distance = 0 + eye offset
}
```

- `UpdateCamera()`: Reads ghost position via delegate, applies orbit offset, SmoothDamp
- `HandleRotationInput()`: Updates orbit angle and pitch
- `SetTarget()`: Accepts entity for visual bridge (future)
- `SupportsOrbitRotation`: `true`
- `UsesCursorAiming`: `false`

**Ghost data access:** The `SpectatorPhase` sets the `GetGhostPosition`/`GetGhostRotation` delegates, which query the cached ghost NativeArrays (same pattern as existing `SpectatorCamera.RefreshGhostData()`). This keeps the camera mode pure (no ECS dependency) while allowing efficient ghost data access.

---

### DeathFreeCam

**File:** `Assets/Scripts/DeathCamera/Cameras/DeathFreeCam.cs`

`MonoBehaviour` implementing `ICameraMode`. Free fly camera with WASD + mouse look.

```csharp
public class DeathFreeCam : MonoBehaviour, ICameraMode
{
    public CameraMode Mode => CameraMode.ThirdPersonFollow;

    private float _moveSpeed;
    private float _fastMultiplier;
    private float _mouseSensitivity;
    private float _yaw, _pitch;
    private float3 _position;
}
```

- `UpdateCamera()`: WASD movement (world-relative via yaw), mouse look, Shift for fast
- `TransformMovementInput()`: Camera-relative direction
- `SupportsOrbitRotation`: `true` (mouse look)
- `UsesCursorAiming`: `false`

---

## ScriptableObjects

### DeathCameraConfigSO

**File:** `Assets/Scripts/DeathCamera/Config/DeathCameraConfigSO.cs`

Master configuration for the death camera system.

```csharp
[CreateAssetMenu(fileName = "DeathCameraConfig", menuName = "DIG/Death Camera/Config")]
public class DeathCameraConfigSO : ScriptableObject
```

| Section | Field | Type | Default | Purpose |
|---------|-------|------|---------|---------|
| **General** | ConfigName | string | `"Default"` | Display name |
| | PhaseSequence | DeathCameraPhaseType[] | `{KillCam, DeathRecap, Spectator}` | Ordered phase list |
| | SkipAllInput | KeyCode | `Space` | Key to skip current phase |
| **Kill Cam** | KillCamEnabled | bool | `true` | Phase toggle |
| | KillCamDuration | float | `3.0` | Duration (seconds) |
| | KillCamOrbitRadius | float | `5.0` | Orbit distance |
| | KillCamOrbitHeight | float | `3.0` | Orbit height |
| | KillCamOrbitSpeed | float | `30.0` | Degrees/second |
| | KillCamSlowMotion | bool | `true` | Enable slow-mo |
| | KillCamTimeScale | float | `0.25` | Time.timeScale |
| | KillCamTransitionIn | float | `0.3` | Blend-in (seconds) |
| **Death Recap** | DeathRecapEnabled | bool | `true` | Phase toggle |
| | DeathRecapDuration | float | `5.0` | Max display time (0 = manual skip only) |
| | ShowDamageBreakdown | bool | `true` | Show damage contributors |
| | ShowRespawnTimer | bool | `true` | Show countdown |
| **Spectator** | SpectatorEnabled | bool | `true` | Phase toggle |
| | DefaultSpectatorMode | DeathSpectatorMode | `Follow` | Initial mode |
| | AllowFreeCam | bool | `true` | Enable free cam (anti-cheat toggle) |
| | AllowFirstPerson | bool | `true` | Enable first person |
| | ShowSpectatorHUD | bool | `true` | Show overlay |
| | TransitionBetweenPlayers | float | `0.5` | Player switch blend |
| | SpectatorTransitionIn | float | `0.5` | Phase entry blend |
| **Follow Cam** | FollowDistance | float | `5.0` | 3rd person distance |
| | FollowHeight | float | `2.0` | Height offset |
| | FollowSmoothTime | float | `0.15` | SmoothDamp time |
| **Free Cam** | FreeCamSpeed | float | `10.0` | Movement speed |
| | FreeCamFastMultiplier | float | `3.0` | Shift speed boost |
| | FreeCamSensitivity | float | `2.0` | Mouse look sensitivity |
| **Respawn** | RespawnTransitionDuration | float | `0.5` | Blend-back duration |
| **General Camera** | FOV | float | `90.0` | Field of view |
| | NearClip | float | `0.1` | Near clip plane |

### DeathCameraPresetSO

**File:** `Assets/Scripts/DeathCamera/Config/DeathCameraPresetSO.cs`

Per-game-mode override preset. Overrides specific fields from the base config.

```csharp
[CreateAssetMenu(fileName = "DeathCameraPreset", menuName = "DIG/Death Camera/Game Mode Preset")]
public class DeathCameraPresetSO : ScriptableObject
```

| Preset | Kill Cam | Death Recap | Spectator | Notes |
|--------|----------|-------------|-----------|-------|
| **Team Deathmatch** | 3s, slow-mo | 5s | Follow teammates | Standard experience |
| **Elimination** | 5s, dramatic | 8s, full breakdown | Spectate all, no free cam | Competitive — longer kill cam |
| **Battle Royale** | 3s | 3s | Squad only, no free cam | Restricted spectator for anti-cheat |
| **Casual / PvE** | Disabled | 2s, timer only | Free cam only | Quick respawn, no competitive info |

---

## Enums

**File:** `Assets/Scripts/DeathCamera/Data/DeathCameraEnums.cs`

```csharp
public enum DeathCameraPhaseType : byte
{
    KillCam = 0,
    DeathRecap = 1,
    Spectator = 2,
    RespawnTransition = 3
}

public enum DeathSpectatorMode : byte
{
    Follow = 0,
    FirstPerson = 1,
    FreeCam = 2,
    Orbit = 3
}

public enum DeathCameraState : byte
{
    Inactive = 0,
    RunningPhases = 1,
    WaitingForRespawn = 2
}
```

---

## UI

### DeathRecapView

**File:** `Assets/Scripts/DeathCamera/UI/DeathRecapView.cs`

Singleton MonoBehaviour, DontDestroyOnLoad. uGUI Canvas (ScreenSpaceOverlay, 1920x1080 CanvasScaler). Displays killer information, damage breakdown, and respawn timer.

**API:**
```csharp
public void Show(DeathCameraContext context);
public void Hide();
public void UpdateRespawnCountdown(float secondsRemaining);
```

**Built programmatically** (follows `PowerHUDBuilder` pattern) — no prefab dependency. Creates Canvas, panels, TMP_Text elements, and Image components at runtime.

**Zero-alloc updates:** Caches TMP_Text references, only updates string when countdown value changes (integer comparison).

### SpectatorHUDView

**File:** `Assets/Scripts/DeathCamera/UI/SpectatorHUDView.cs`

Singleton MonoBehaviour, DontDestroyOnLoad. Shows followed player info, player list, camera mode, and controls help.

**API:**
```csharp
public void Show(List<PlayerListEntry> players, string followedPlayerName, float health, float maxHealth);
public void Hide();
public void UpdateFollowedPlayer(string name, float health, float maxHealth);
public void UpdatePlayerList(List<PlayerListEntry> players);
public void UpdateCameraMode(DeathSpectatorMode mode);
public void UpdateRespawnCountdown(float secondsRemaining);
```

```csharp
public struct PlayerListEntry
{
    public ushort GhostId;
    public string Name;
    public bool IsAlive;
}
```

---

## Editor Tooling

### DeathCameraWorkstationWindow

**File:** `Assets/Editor/DeathCameraWorkstation/DeathCameraWorkstationWindow.cs`

`[MenuItem("DIG/Death Camera Workstation")]` — EditorWindow with tabs.

**Tabs:**

#### Config Tab
- Full SerializedObject editor for `DeathCameraConfigSO`
- **Create Default Config** button (creates in `Assets/Resources/DeathCameraConfig.asset`)
- Phase sequence visualizer: horizontal timeline showing phases with durations
- Per-phase enable/disable toggles with visual indication

#### Presets Tab
- Lists all `DeathCameraPresetSO` assets in project
- Shows key fields per preset (phases enabled, kill cam duration, spectator mode)
- **Create New Preset** button with template dropdown (TDM, Elimination, BR, Casual)
- **Select** / **Delete** buttons per preset

#### Preview Tab
- **Simulate Death** button (in Play Mode only)
- Sends `DeathDebugRpc` with `RequestedPhase = Dead` to trigger the full death flow
- Shows current phase, elapsed time, state machine status in real-time
- **Skip Phase** button for rapid iteration
- **Reset** button to force respawn

---

## Modifications to Existing Files

### CameraManager.cs

**File:** `Assets/Scripts/Systems/Camera/CameraManager.cs`

**Change:** Add 1 line at the top of `LateUpdate()`.

```csharp
private void LateUpdate()
{
    // EPIC 18.13: Yield to death camera (or any higher-priority camera override)
    if (DIG.DeathCamera.CameraAuthorityGate.IsOverridden) return;

    // ... existing code unchanged ...
}
```

### CinemachineCameraController.cs

**File:** `Assets/Scripts/Camera/Cinemachine/CinemachineCameraController.cs`

**Change:** Add 1 line at the top of `LateUpdate()`.

```csharp
private void LateUpdate()
{
    // EPIC 18.13: Yield to death camera (or any higher-priority camera override)
    if (DIG.DeathCamera.CameraAuthorityGate.IsOverridden) return;

    if (!_isInitialized) { ... }
    // ... existing code unchanged ...
}
```

### InputContextManager.cs

**File:** `Assets/Scripts/Core/Input/InputContextManager.cs`

**Change:** Add `DeathSpectator` to `InputContext` enum.

```csharp
public enum InputContext
{
    Gameplay,
    UI,
    Spectator,       // EPIC 18.10: Replay/observer spectator
    DeathSpectator   // EPIC 18.13: Post-death spectating (WASD camera + UI + skip input)
}
```

Handler: enables Core action map (WASD) + UI action map (skip/interact) + frees cursor.

### DeathSpectatorTransitionSystem.cs (REMOVED)

**File:** `Assets/Scripts/Replay/Spectator/DeathSpectatorTransitionSystem.cs`

**Change:** Delete entirely. Replaced by `DeathCameraOrchestrator` + phase system.

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/DeathCamera/CameraAuthorityGate.cs` | Static class | ~40 |
| `Assets/Scripts/DeathCamera/IDeathCameraPhase.cs` | Interface + context class | ~60 |
| `Assets/Scripts/DeathCamera/DeathCameraOrchestrator.cs` | SystemBase | ~300 |
| `Assets/Scripts/DeathCamera/Data/DeathCameraEnums.cs` | Enums | ~30 |
| `Assets/Scripts/DeathCamera/Config/DeathCameraConfigSO.cs` | ScriptableObject | ~100 |
| `Assets/Scripts/DeathCamera/Config/DeathCameraPresetSO.cs` | ScriptableObject | ~80 |
| `Assets/Scripts/DeathCamera/Phases/KillCamPhase.cs` | Class | ~120 |
| `Assets/Scripts/DeathCamera/Phases/DeathRecapPhase.cs` | Class | ~100 |
| `Assets/Scripts/DeathCamera/Phases/SpectatorPhase.cs` | Class | ~250 |
| `Assets/Scripts/DeathCamera/Phases/RespawnTransitionPhase.cs` | Class | ~60 |
| `Assets/Scripts/DeathCamera/Cameras/DeathKillCam.cs` | MonoBehaviour + ICameraMode | ~120 |
| `Assets/Scripts/DeathCamera/Cameras/DeathFollowCam.cs` | MonoBehaviour + ICameraMode | ~180 |
| `Assets/Scripts/DeathCamera/Cameras/DeathFreeCam.cs` | MonoBehaviour + ICameraMode | ~100 |
| `Assets/Scripts/DeathCamera/UI/DeathRecapView.cs` | MonoBehaviour + uGUI | ~250 |
| `Assets/Scripts/DeathCamera/UI/SpectatorHUDView.cs` | MonoBehaviour + uGUI | ~220 |
| `Assets/Editor/DeathCameraWorkstation/DeathCameraWorkstationWindow.cs` | EditorWindow | ~250 |
| **Total estimated:** | | **~2,260 lines** |

**Modifications:** ~10 lines across 3 existing files.
**Removed:** `DeathSpectatorTransitionSystem.cs` (~186 lines).

---

## Performance Considerations

- **CameraAuthorityGate:** Single static bool check per frame — effectively zero cost
- **DeathCameraOrchestrator:** Only runs when player is dead (`RequireForUpdate` on local player query). Zero cost when alive. When active: 1 entity query per frame for alive players, cached `NativeArray` access with `Allocator.Temp` (stack allocation)
- **Ghost data access:** Same `ToComponentDataArray` + scan pattern as optimized `SpectatorCamera` from EPIC 18.10 — zero managed allocations
- **Camera modes:** Pure `LateUpdate()` with math operations — no physics, no allocations
- **UI updates:** String formatting only when values change (integer countdown comparison). Cached `TMP_Text` references. Canvas rebuilds only on show/hide, not per-frame
- **Phase transitions:** One-time `CameraTransitionManager` calls — zero per-frame cost during steady state
- **RecentAttackerElement read:** One-time buffer copy on death, not per-frame. Buffer is max 8 elements
- **Player list refresh:** Every 30 frames during spectator phase, not every frame. `Allocator.Temp` for NativeArrays

**Memory footprint when inactive:** Zero — orchestrator skips `OnUpdate` entirely via `RequireForUpdate`.

---

## Testing Strategy

- **Unit:** `CameraAuthorityGate.Acquire/Release` — priority semantics, double-acquire, release by wrong owner
- **Unit:** Phase state machine — verify `Enter → Update → IsComplete → Exit` lifecycle for each phase type
- **Unit:** `DeathCameraContext` construction — verify `KillCredited` and `RecentAttackerElement` data extraction
- **Integration:** Kill player → verify kill cam activates with correct orbit position
- **Integration:** Kill cam → death recap → verify UI shows correct killer name and damage breakdown
- **Integration:** Death recap → spectator → verify camera follows alive player, Tab cycles modes
- **Integration:** Spectator → respawn → verify smooth camera transition back to gameplay
- **Integration:** Skip kill cam → verify immediate advance to death recap
- **Integration:** All teammates dead → verify free cam fallback
- **Integration:** `CameraAuthorityGate` → verify `CameraManager.LateUpdate()` yields when override active
- **Integration:** Rapid death/respawn → verify no leaked camera modes or UI elements
- **Editor:** `DIG > Death Camera Workstation` opens, shows config editor
- **Editor:** `DeathCameraConfigSO` creates via `Create > DIG > Death Camera > Config`
- **Editor:** Preview tab simulates death flow in Play Mode

---

## Migration Path

### Phase 0 — This Epic (EPIC 18.13)
- All 17 new files + 3 modifications
- Delete `DeathSpectatorTransitionSystem.cs`
- `ReplayConfigSO.KillCamBufferSeconds` and `KillCamPlaybackSpeed` remain for replay kill cam (different feature)
- `SpectatorCamera` (EPIC 18.10) remains untouched for replay/observer use

### Phase 1 — Future: Kill Cam Replay (requires EPIC 18.10 integration)
- Use `ReplayRecorderSystem` ring buffer to replay actual last N seconds from killer's perspective
- `DeathKillCam` gets a `ReplayMode` that drives from recorded data instead of orbit
- Requires ring buffer read API on `ReplayRecorderSystem`

### Phase 2 — Future: Game Mode System
- When a game mode system is implemented, `DeathCameraOrchestrator` reads the active game mode
- Auto-applies the corresponding `DeathCameraPresetSO`
- Override rules: game mode preset → base config → hardcoded defaults

### Phase 3 — Future: Competitive Spectator Restrictions
- Anti-cheat: delay spectator feed by N seconds (configurable)
- Fog of war: hide enemy positions not visible to followed teammate
- Free cam boundaries: limit movement to map bounds
- X-ray toggle: outline followed player through walls (requires outline shader)
