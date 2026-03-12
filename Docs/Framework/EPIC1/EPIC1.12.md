### Epic 1.12: Leaning System (Continued)
**Priority**: LOW  
**Goal**: Tactical positioning for peeking around corners

**Tasks**:
 - [X] Define `LeanState` component:
  - [X] `CurrentLean` (-1 to 1), `TargetLean`, `LeanSpeed` (5.0)
 - [X] Define `LeanSettings` component:
  - [X] `MaxLeanAngle` (30°), `LeanDistance` (0.5m)
  - [X] `CanLeanWhileMoving` (false)
 - [X] Create `Player/Systems/LeanSystem.cs`
 - [X] Read lean input (Q = left, E = right) — hybrid and networked input paths implemented
 - [X] Smoothly shift camera position sideways (lateral offset applied in `PlayerCameraControlSystem`)
 - [X] Tilt camera angle (small roll applied in `PlayerCameraControlSystem`)
 - [X] Prevent leaning while moving (honored by `LeanSystem` via `CanLeanWhileMoving` and `DeadMoveThreshold`)
 - [ ] Test leaning useful for peeking around corners (PlayMode QA pending)

**Status (Nov 30, 2025): Completed work and outstanding items**

- [X] `LeanState` component implemented (`CurrentLean`, `TargetLean`, `LeanSpeed`).
- [X] `LeanSettings` ScriptableObject implemented and safe runtime fallback supported (editor helper will create Resources entry when missing).
- [X] `Player/Systems/LeanSystem.cs` implemented and updated to prefer NetCode `PlayerInput` when present, falling back to hybrid `PlayerInputComponent`. Smooths `CurrentLean` → `TargetLean` and respects settings.
- [X] Lean input wiring completed:
  - [X] Hybrid/local path: `PlayerInputState` contains `LeanLeft`/`LeanRight` and `PlayerInputReader` maps `Q`/`E` to them; `PlayerInputWriterSystem` writes into `PlayerInputComponent`.
  - [X] Networked path: `PlayerInput` (NetCode sampling) records both `LeanLeft` and `LeanRight` `InputEvent`s for prediction.
- [X] Enforcement to block leaning while moving is implemented in `LeanSystem` (honors `CanLeanWhileMoving` and `DeadMoveThreshold`).
- [X] Editor helper `Assets/Editor/EnsureSettingsAssets.cs` or equivalent was added to create default `LeanSettings` in `Assets/Resources` at domain reload (or a default asset may be committed).

Recent implementation notes (added during Nov 2025 work):
- [X] `LeanSystem` change: unified input processing loop that prefers `PlayerInput` then `PlayerInputComponent` and adds conditional `LEAN_DEBUG` per-entity logging. (see commit `8ac628b`).
- [X] `PlayerCameraControlSystem` change: guarded reads for `PlayerInput`, re-applied lean lateral offset and roll, with throttled logging for diagnostics. (see commit `66d661f`).

Outstanding / Follow-ups:
- [ ] PlayMode QA: create a small test scene to verify peeking behavior against cover, and add a unit test for lean smoothing math.
- [ ] (Optional) Commit a designer-default `Assets/Resources/LeanSettings.asset` if you want the asset present without opening the Editor. The editor helper will create one on domain reload if missing.

Notes:
- The lean implementation is feature-complete for input and smoothing. Camera visuals are applied and guarded; QA/testing remains to validate behavior in scenes and ensure hybrid/netcode parity across host/client runs.