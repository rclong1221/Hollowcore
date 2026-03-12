# EPIC 18.13: Death Camera & Post-Death Experience — Setup Guide

## Prerequisites

- Unity 2022.3+ with Entities, NetCode, Input System, and Cinemachine 3 packages installed
- TextMeshPro package installed (UI views use `TextMeshProUGUI`)
- DIG project with existing ECS infrastructure, `CameraModeProvider`, and `CameraTransitionManager`

---

## 1. Config Asset Setup

The system loads its configuration from a ScriptableObject at a **fixed Resources path**.

1. Check if `Assets/Resources/DeathCameraConfig.asset` already exists
2. If not: Right-click in Project > **Create > DIG > Death Camera > Config**
3. Name the asset `DeathCameraConfig`
4. Move it to **`Assets/Resources/`**

> The orchestrator loads this via `Resources.Load<DeathCameraConfigSO>("DeathCameraConfig")`. If the asset is missing, a default in-memory config is created at runtime — but you should always have the asset so you can tune values.

---

## 2. Config Inspector Reference

### General

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Config Name | string | `"Default"` | Label for this config (organizational only) |
| Phase Sequence | `DeathCameraPhaseType[]` | KillCam, DeathRecap, Spectator | Ordered list of phases to run after death. Reorder, add, or remove phases here. |
| Skip All Input | KeyCode | `Space` | Key to skip the current skippable phase |

### Kill Cam

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Kill Cam Enabled | bool | `true` | Master toggle for the kill cam phase |
| Kill Cam Duration | float | `3` | How long the kill cam plays (seconds) |
| Kill Cam Orbit Radius | float | `5` | Starting orbit radius around the kill position |
| Kill Cam Orbit Height | float | `3` | Starting orbit height above the kill position |
| Kill Cam Orbit Speed | float | `30` | Orbit rotation speed (degrees/sec) |
| Kill Cam End Radius | float | `2` | Final orbit radius at end of kill cam (zoom-in effect) |
| Kill Cam End Height | float | `1.5` | Final orbit height at end of kill cam |
| Kill Cam Slow Motion | bool | `true` | Whether the orbit animation plays in slow motion |
| Kill Cam Time Scale | float | `0.25` | Slow-motion multiplier (local only — does NOT modify `Time.timeScale`) |
| Kill Cam Transition In | float | `1.5` | Blend-in duration from gameplay camera to orbit (seconds) |

> **Important:** Slow motion is cosmetic and local only. The orbit just spins slower. `Time.timeScale` is never modified — other players on a listen server are unaffected.

### Death Recap

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Death Recap Enabled | bool | `true` | Master toggle for the death recap overlay |
| Death Recap Duration | float | `5` | Max display time in seconds (0 = manual skip only) |
| Show Damage Breakdown | bool | `true` | Whether to show per-attacker damage list |
| Show Respawn Timer | bool | `true` | Whether to show respawn countdown |

### Spectator

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Spectator Enabled | bool | `true` | Master toggle for the spectator phase |
| Allow TPS Orbit | bool | `true` | Enable TPS Orbit + TPS Locked camera styles in the TAB cycle |
| Allow Isometric | bool | `true` | Enable Isometric Fixed + Iso Locked camera styles in the TAB cycle |
| Allow Top Down | bool | `true` | Enable Top-Down + TD Locked camera styles in the TAB cycle |
| Allow Isometric Rotatable | bool | `true` | Enable Iso Rotatable + Iso Rot Locked camera styles (Q/E rotation) |
| Allow Free Cam | bool | `true` | Enable Free Cam in the TAB cycle. Disable for anti-cheat. |
| Show Spectator HUD | bool | `true` | Whether to display the spectator HUD overlay |
| Transition Between Players | float | `0.5` | Blend duration when switching followed player (seconds) |
| Spectator Transition In | float | `0.5` | Blend-in duration from previous phase (seconds) |

> **Note:** The initial spectator camera style is automatically determined by the gameplay paradigm at death time (e.g., ARPG → Isometric Fixed, Shooter → TPS Orbit). If that style is disabled, the first available style in the list is used instead.

### Follow Cam

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Follow Distance | float | `8` | Default camera distance behind the followed player |
| Follow Height | float | `1.6` | Camera pivot height offset |
| Follow Smooth Time | float | `0.15` | SmoothDamp response time (lower = snappier) |
| Look At Height | float | `1.6` | Vertical offset for LookAt target (chest level) |
| Default Pitch | float | `20` | Default pitch angle in degrees (0 = horizontal, 90 = overhead) |
| Orbit Sensitivity | float | `0.15` | Mouse orbit sensitivity. Range: 0.01–0.5 |

### Follow Cam — Zoom

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Zoom Distance Min | float | `2` | Closest zoom distance (scroll wheel all the way in) |
| Zoom Distance Max | float | `15` | Farthest zoom distance (scroll wheel all the way out) |
| Zoom Scroll Sensitivity | float | `0.08` | Scroll wheel sensitivity. Range: 0.01–1.0 |

### Follow Cam — Collision

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enable Collision | bool | `true` | Prevent camera from clipping through walls |
| Collision Layers | LayerMask | Everything | Layers the camera collides with |
| Collision Radius | float | `0.25` | SphereCast radius for collision probes. Range: 0.1–0.5 |

> **Note:** Camera collision is currently **disabled at runtime** due to SphereCast oscillation issues (see EPIC 18.14). These fields are reserved for the future collision system. Changing them has no effect until EPIC 18.14 is implemented.

### Isometric Fallback

These values are used in two situations: (1) when the gameplay `CameraConfig` is unavailable, and (2) when the spectator TABs to an isometric style from a different gameplay paradigm (e.g., a Shooter player TABbing to Isometric in spectator). If the player dies while already using an isometric camera, the system captures the exact Cinemachine output and uses that instead.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Isometric Angle | float | `50` | Pitch angle for isometric death camera view. Range: 30–75 |
| Isometric Rotation | float | `0` | Camera Y-rotation (0=faces north, 90=faces east, 180=faces south, 270=faces west). Range: 0–360 |
| Isometric Height | float | `15` | Height above target |

### Top-Down Fallback

Same as isometric fallback — used when gameplay CameraConfig is unavailable or when TABbing to top-down from a different gameplay paradigm.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Top Down Angle | float | `85` | Near-vertical angle. Range: 60–90 |
| Top Down Height | float | `20` | Height above target |

### Free Cam

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Free Cam Speed | float | `10` | Base fly speed (units/sec) |
| Free Cam Fast Multiplier | float | `3` | Speed multiplier when holding Shift |
| Free Cam Sensitivity | float | `2` | Mouse look sensitivity |

### Respawn Transition

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Respawn Transition Duration | float | `0.5` | Duration of the camera blend back to gameplay (seconds) |

### General Camera

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| FOV | float | `90` | Field of view for death cameras |
| Near Clip | float | `0.1` | Near clip plane distance |

---

## 3. Game Mode Presets (Optional)

Presets let you override specific config values per game mode without duplicating the entire config.

1. Right-click in Project > **Create > DIG > Death Camera > Game Mode Preset**
2. Name it descriptively (e.g., `Preset_Competitive`, `Preset_Casual`)
3. For each field you want to override, check the **Override** checkbox and set the value

| Override Field | Description |
|----------------|-------------|
| Phase Sequence Override | Custom phase order (empty = use base config) |
| Kill Cam Enabled | Override the kill cam toggle |
| Kill Cam Duration | Override kill cam length |
| Kill Cam Slow Motion | Override slow-mo toggle |
| Kill Cam Time Scale | Override slow-mo speed |
| Death Recap Enabled | Override recap toggle |
| Death Recap Duration | Override recap length |
| Show Damage Breakdown | Override damage breakdown visibility |
| Spectator Enabled | Override spectator toggle |
| Allow TPS Orbit | Override TPS camera style availability |
| Allow Isometric | Override Isometric camera style availability |
| Allow Top Down | Override Top-Down camera style availability |
| Allow Isometric Rotatable | Override Iso Rotatable camera style availability |
| Allow Free Cam | Override free cam availability |
| Transition Between Players | Override player-switch blend time |

Fields without **Override** checked use the base config value.

> **Example:** A competitive preset might disable Free Cam (`AllowFreeCam = false`) and TPS Orbit (`AllowTPSOrbit = false`) to restrict spectators to fixed-angle views only.

---

## 4. Scene Requirements

The death camera system is an **ECS SystemBase** — it does not require any GameObjects in the scene. It creates all camera GameObjects and UI programmatically at runtime.

However, these singletons from other systems **must exist** for full functionality:

| Singleton | Required? | Purpose | What Happens If Missing |
|-----------|-----------|---------|------------------------|
| `CameraModeProvider` | Yes | Death camera registers itself as the active camera mode | Death camera cannot control `Camera.main` — spectator sees nothing |
| `CameraTransitionManager` | Recommended | Smooth blends between phases and back to gameplay | Instant camera cuts instead of blends |
| `InputContextManager` | Recommended | Pushes `InputContext.DeathSpectator` to suppress gameplay input | Player input may leak through during death |
| `SpectatorHUDView` | Recommended | Displays spectator overlay (mode, player list, zoom) | No spectator HUD — camera still works |
| `DeathRecapView` | Recommended | Displays death recap overlay (killer, damage breakdown) | No recap overlay — phase still runs as a timer |

### Setting Up UI Singletons

Both `SpectatorHUDView` and `DeathRecapView` are singleton MonoBehaviours that build their UI programmatically. No prefab or canvas setup is needed — just the component.

1. In your boot/persistent scene, create an empty GameObject named `[DeathCameraUI]`
2. Add two components:
   - **SpectatorHUDView**
   - **DeathRecapView**
3. Both components call `DontDestroyOnLoad` in `Awake()` — they persist across scene loads

> These components have **no serialized fields** in the Inspector. All UI (canvas, text elements, panels) is created programmatically when `Show()` is called.

---

## 5. Player Prefab Requirements

For the death camera to activate and for locked-follow spectating to work, the player ghost prefab needs these components:

| Component | Purpose | Already Present? |
|-----------|---------|-----------------|
| `DeathState` | Orchestrator detects death via `DeathState.IsDead` | Yes — added by `DamageableAuthoring` |
| `GhostOwnerIsLocal` | Identifies the local player's ghost entity | Yes — NetCode standard |
| `GhostInstance` | Ghost ID used for spectator target identification | Yes — NetCode standard |
| `PlayerCameraSettings` | Replicated Yaw/Pitch/Distance for locked-follow mode | Yes — `[GhostField]` on Yaw, Pitch, CurrentDistance |
| `CombatState` | Kill cam reads `LastAttacker` to find the killer | Yes — from combat system |
| `RecentAttackerElement` | Death recap reads damage contributor list | Yes — from combat system |

No additional components need to be added to the player prefab.

---

## 6. Phase Sequence Flow

```
Player Dies (DeathState.IsDead transitions to true)
    |
    v
Orchestrator activates
    |-- Builds context (kill position, killer, alive teammates, camera state)
    |-- Acquires camera authority (priority 10)
    |-- Captures Camera.main position/rotation/FOV
    |-- Pushes InputContext.DeathSpectator
    |
    v
Phase sequence (configurable in PhaseSequence array):

[KillCam] ---Space---> [DeathRecap] ---Space---> [Spectator] -------> [RespawnTransition]
 3s orbit               5s overlay               Indefinite           0.5s blend back
 Slow-mo orbit          Killer name              TAB = cycle style    Blends back to
 Zoom-in effect         Damage breakdown         1-9 = select player  gameplay camera
 Skippable              Respawn countdown         Scroll = zoom
                        Skippable                Q/E = iso rotate
                                                 9 camera styles

    |
    v
Respawn detected (DeathState.IsDead returns to false)
    |-- Releases camera authority
    |-- Pops InputContext.DeathSpectator
    |-- Gameplay camera resumes
```

> **TAB shortcut:** During KillCam or DeathRecap, pressing TAB skips directly to Spectator phase.

---

## 7. Spectator Mode Controls

| Key | Action | Available In |
|-----|--------|--------------|
| **TAB** | Cycle through all enabled camera styles | All modes |
| **1–9** | Select specific teammate to follow | All follow modes (not Free Cam) |
| **Scroll Wheel** | Zoom in/out | All follow modes (not Free Cam) |
| **Mouse Move** | Orbit camera around followed player | TPS Orbit only |
| **Q / E** | Rotate isometric camera 45 degrees | Iso Rotatable + Iso Rot Locked only |
| **WASD** | Fly movement | Free Cam only |
| **Shift** | Fast fly speed | Free Cam only |
| **Mouse Move** | Look direction | Free Cam only |

### Camera Styles

TAB cycles through styles grouped by type. Each enabled style adds its unlocked + locked variant. The default cycle order (all enabled):

**TPS Orbit → TPS Locked → Isometric → Iso Locked → Top Down → TD Locked → Iso Rotate → Iso Rot Locked → Free Cam → (back to TPS Orbit)**

| Style | HUD Label | Behavior |
|-------|-----------|----------|
| **TPS Orbit** | TPS ORBIT | Third-person orbit camera. Mouse controls orbit angle and pitch. Scroll zooms. |
| **TPS Locked** | TPS LOCKED | Third-person locked to watched player's camera direction. Mouse orbit disabled. Scroll zoom still works. |
| **Isometric Fixed** | ISOMETRIC | Fixed isometric angle overhead. Scroll zooms via height multiplier. |
| **Iso Locked** | ISO LOCKED | Isometric locked to watched player's view. |
| **Top Down** | TOP DOWN | Near-vertical top-down view. Scroll zooms. |
| **TD Locked** | TD LOCKED | Top-down locked to watched player's view. |
| **Iso Rotatable** | ISO ROTATE | Isometric with Q/E rotation (45-degree increments). |
| **Iso Rot Locked** | ISO ROT LOCKED | Isometric rotatable locked to watched player's view. Q/E still works. |
| **Free Cam** | FREE CAM | Camera detaches from the player. Fly freely with WASD + mouse look. |

> Disabling a style in the config (e.g., `AllowTopDown = false`) removes both its unlocked and locked variants from the TAB cycle.

---

## 8. Paradigm-Aware Behavior

### Initial Mode Selection

When the player dies, the spectator camera **starts in the style matching the gameplay paradigm**:

| Paradigm | Gameplay Camera | Initial Spectator Style |
|----------|----------------|------------------------|
| Shooter | ThirdPersonFollow | TPS Orbit |
| MMO | ThirdPersonFollow | TPS Orbit |
| ARPG | IsometricFixed | Isometric Fixed |
| MOBA | TopDownFixed | Top Down |
| TwinStick | IsometricRotatable | Iso Rotatable |
| First Person | FirstPerson | TPS Orbit (fallback) |

If the matching style is disabled in the config, the first enabled style is used.

### TAB Cycling — Universal Access

After entering spectator, **all enabled camera styles are available via TAB** regardless of the gameplay paradigm. A Shooter player can TAB to Isometric or Top-Down views. An ARPG player can TAB to TPS Orbit. This gives spectators full flexibility.

### Kill Cam

The kill cam always matches the gameplay paradigm and does not offer style switching:
- **TPS paradigms**: Orbit spin around the kill position with zoom-in effect
- **Isometric/TopDown paradigms**: Fixed-angle overhead view (no orbit spin)

### Camera Config Sources

When spectating in a style that matches the gameplay paradigm, the system uses the gameplay `CameraConfig` values (e.g., IsometricAngle from the ARPG camera config) and captured Cinemachine camera state for pixel-perfect matching.

When TABbing to a **different** style (e.g., Shooter player → Isometric), the system uses the `DeathCameraConfigSO` fallback values (`IsometricAngle`, `IsometricRotation`, `IsometricHeight`, `TopDownAngle`, `TopDownHeight`). Tune these fallback values for a good spectator experience across all styles.

---

## 9. Editor Tooling — Death Camera Workstation

Open via **DIG > Death Camera Workstation** from the menu bar.

| Tab | Description |
|-----|-------------|
| **Config Editor** | Edit the `DeathCameraConfig` asset directly. Same fields as the Inspector but organized by phase. |
| **Presets** | Browse and edit game mode presets. Preview which fields are overridden. |
| **Runtime Debug** | (Play Mode only) Shows live orchestrator state: current phase, phase timer, respawn countdown, followed ghost ID, spectator mode, alive player count, camera authority status, and full context dump. |
| **Authority Gate** | Shows the current camera authority stack (which system owns the camera). |

Runtime Debug provides two buttons during play mode:
- **Skip Phase** — forces the current phase to complete (same as pressing Space)
- **Force End** — immediately exits the entire death camera flow and returns to gameplay

---

## 10. Verification Checklist

After setup, verify in play mode:

1. [ ] `Assets/Resources/DeathCameraConfig.asset` exists and is configured
2. [ ] `[DeathCameraUI]` GameObject exists in boot scene with `SpectatorHUDView` + `DeathRecapView`
3. [ ] `CameraModeProvider` singleton exists in the scene
4. [ ] Enter play mode — die (take damage or use `god off` + damage)
5. [ ] **Kill Cam**: Camera orbits the kill position with zoom-in effect. Duration matches config.
6. [ ] **Death Recap**: Overlay appears showing killer name, damage breakdown, respawn timer
7. [ ] **Spectator**: Camera follows an alive teammate. HUD shows correct mode label for paradigm (e.g., "TPS ORBIT" for Shooter, "ISOMETRIC" for ARPG).
8. [ ] Press **TAB** — mode cycles through all enabled styles: TPS ORBIT → TPS LOCKED → ISOMETRIC → ISO LOCKED → TOP DOWN → TD LOCKED → ISO ROTATE → ISO ROT LOCKED → FREE CAM → back to start
9. [ ] In TPS Orbit: mouse orbit works, scroll zoom works
10. [ ] In TPS Locked: camera stays behind the followed player, mouse orbit disabled, scroll zoom works
11. [ ] In Isometric/TopDown styles: fixed overhead angle, scroll zoom works
12. [ ] In Iso Rotatable: Q/E rotates 45 degrees, scroll zoom works
13. [ ] In Free Cam: WASD moves the camera, mouse looks around
14. [ ] Press **1–9** — camera switches to different teammate with smooth transition
15. [ ] Press **Space** during Kill Cam or Death Recap — phase skips to next
16. [ ] On respawn: camera blends back to gameplay smoothly
17. [ ] **Paradigm start**: Die as Shooter → starts in TPS Orbit. Die as ARPG → starts in Isometric. Die as MOBA → starts in Top Down.
18. [ ] **Cross-paradigm TAB**: Die as Shooter → TAB to Isometric → verify correct overhead angle (not TPS-like)
19. [ ] Open **DIG > Death Camera Workstation > Runtime Debug** — verify live state displays correctly
20. [ ] Set `AllowFreeCam = false` in config → verify TAB skips Free Cam mode
21. [ ] Set `AllowTPSOrbit = false` → verify TPS Orbit and TPS Locked are both skipped
22. [ ] Set `AllowIsometric = false` → verify Isometric and Iso Locked are both skipped
23. [ ] HUD controls hint updates per mode (e.g., "[Mouse] Orbit" only for TPS Orbit, "[Q/E] Rotate" only for Iso Rotate)

---

## 11. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Death camera never activates | Config asset not at `Assets/Resources/DeathCameraConfig.asset` | Move or recreate the asset at the correct Resources path |
| Death camera never activates | No local player entity with `DeathState` + `GhostOwnerIsLocal` | Ensure player prefab has `DamageableAuthoring` and is a ghost with local ownership |
| Camera snaps to origin on death | `Camera.main` is null at death time | Ensure a camera tagged MainCamera exists. Check Cinemachine brain is active. |
| No spectator HUD | `SpectatorHUDView` singleton doesn't exist | Add `SpectatorHUDView` component to a persistent GameObject |
| No death recap overlay | `DeathRecapView` singleton doesn't exist | Add `DeathRecapView` component to a persistent GameObject |
| Camera doesn't follow anyone (shows kill position) | No alive teammates (all dead or solo) | Expected — camera orbits the kill position instead. Verify with 2+ players. |
| Locked Follow doesn't track player's camera | Watched player's `PlayerCameraSettings` has default values (Yaw=0, Pitch=25, Dist=8) | Ensure `CinemachineCameraController` writes back to `PlayerCameraSettings` on the watched player's entity. The values must replicate via `[GhostField]`. |
| Isometric death camera jumps vs gameplay | Captured camera state not matching | Check that `Camera.main` is the Cinemachine output camera. If using a separate render camera, the capture may read the wrong transform. |
| Kill cam has no slow-motion feel | `KillCamSlowMotion` unchecked or `KillCamTimeScale` set to 1.0 | Enable slow motion and set time scale to 0.2–0.3 |
| TABbing to Isometric shows wrong angle | Fallback values in DeathCameraConfigSO not tuned | Adjust `IsometricAngle`, `IsometricRotation`, `IsometricHeight` in the config asset. These are used when TABbing to a style different from the gameplay paradigm. |
| A camera style is missing from TAB cycle | That style's toggle is disabled in config | Enable `AllowTPSOrbit`, `AllowIsometric`, `AllowTopDown`, `AllowIsometricRotatable`, or `AllowFreeCam` as needed |
| Free Cam mode not available | `AllowFreeCam` is `false` in config | Enable it — or check if a game mode preset is overriding it |
| Camera doesn't blend back on respawn | `CameraTransitionManager` singleton missing | Add `CameraTransitionManager` to the scene. Without it, the camera cuts back instantly. |
| Spectator input leaks to gameplay | `InputContextManager` not in scene | Add `InputContextManager`. The orchestrator pushes `InputContext.DeathSpectator` to suppress gameplay input during death. |
| Console shows `[DCam]` logs | Expected — these are functional logs | Use `[DCam]` as a filter in the console to isolate death camera messages |

---

## 12. Known Limitations

1. **Camera collision is disabled.** The follow cam does not avoid walls or terrain. This is planned for EPIC 18.14.

2. **Killer name resolution is not implemented.** The death recap shows "Unknown" for the killer name. A name resolution system (reading from identity/profile components) is needed.

3. **Click-to-move is broken in MOBA/ARPG paradigms.** This is a separate issue tracked in EPIC 18.15 — not related to the death camera.

4. **No alive players = static orbit.** When no teammates are alive, the spectator follow cam orbits the dead player's body position with a slow auto-orbit. There is no "free roam to find enemies" behavior.

5. **Spectator cannot see enemy outlines or through walls.** No spectator-specific rendering features are implemented. The spectator sees exactly what the camera shows.
