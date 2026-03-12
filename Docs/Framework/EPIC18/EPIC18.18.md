# EPIC 18.18: Targeting & Attack Coverage for All Game Modes

**Status:** PLANNED
**Priority:** High (core gameplay — targeting/attack must work correctly per paradigm)
**Dependencies:**
- EPIC 15.20: Input Paradigm Framework (ParadigmStateMachine, profiles, IParadigmConfigurable)
- EPIC 15.21: Input Action Layer (ParadigmInputManager, action maps)
- EPIC 15.18: Cursor Hover & Click-to-Select (CursorHoverSystem, CursorClickTargetSystem)
- `TargetData` / `TargetingMode` (existing — `Assets/Scripts/Targeting/`)
- `PlayerFacingSystem` (existing — writes `TargetData.AimDirection` for CameraForward/CursorDirection/MovementDirection)
- `WeaponFireSystem` (existing — reads `TargetData.AimDirection`, `TargetData.TargetEntity`)
- `MeleeActionSystem` (existing — melee hitbox timing, combo chains)
- Input profiles in `Assets/Data/Input/Profiles/`

**Feature:** Ensure targeting and attack behavior correctly covers all game modes defined by input profiles. Each paradigm has distinct targeting semantics (crosshair, click-select, cursor-aim, lock-on) and attack flows. The current implementation has paradigm–targeting decoupling, cursor-gating that excludes some paradigms, and potential LMB conflicts in ARPG/MOBA.

---

## Problem

Targeting and attack systems are not explicitly tied to input paradigms. `InputParadigmProfile` has no targeting mode field. `TargetingConfig` is a separate ScriptableObject not referenced by profiles. `CursorHoverSystem` and `CursorClickTargetSystem` only run when `InputSchemeManager.IsCursorFree` is true — which excludes Shooter (cursor locked) but correctly includes MMO, ARPG, MOBA, TwinStick (cursor free). However, the targeting mode used at runtime is baked into `TargetDataAuthoring.InitialMode` (default CameraRaycast) and can be overwritten by `CursorClickTargetSystem` when the user clicks to select. There is no paradigm-driven selection of targeting behavior, and several edge cases may cause incorrect or conflicting behavior across paradigms.

---

## Codebase Audit

### Input Profiles (`Assets/Data/Input/Profiles/`)

| Profile | Paradigm | cursorFreeByDefault | Combat Map | Primary Attack/Select Actions |
|---------|----------|--------------------|------------|------------------------------|
| Profile_Shooter | 0 | false | Combat_Shooter | Attack, AimDownSights |
| Profile_ShooterHybrid | 0 | false | Combat_Shooter | Attack, AimDownSights (Alt frees cursor) |
| Profile_MMO | 1 | true | Combat_MMO | SelectTarget (LMB), CameraOrbit (RMB) |
| Profile_ARPG_Classic | 2 | true | Combat_ARPG | AttackAtCursor (LMB), MoveToClick (RMB); click-to-move LMB |
| Profile_ARPG_Hybrid | 2 | true | Combat_ARPG | AttackAtCursor (LMB), MoveToClick (RMB); WASD only, no click-to-move |
| Profile_MOBA | 3 | true | Combat_MOBA | AttackAtCursor (LMB), Move (RMB), AttackMove, Stop, HoldPosition |
| Profile_TwinStick | 4 | true | Combat_Shooter | Attack (LMB), Aim (RMB) |

### Targeting Systems

| System | File | When Active | Writes |
|--------|------|-------------|--------|
| `CursorHoverSystem` | `Assets/Scripts/Targeting/Systems/CursorHoverSystem.cs` | `IsCursorFree` only | `CursorHoverResult` |
| `CursorClickTargetSystem` | `Assets/Scripts/Targeting/Systems/CursorClickTargetSystem.cs` | `IsCursorFree` only | `TargetData` (Mode=ClickSelect) on LMB/RMB click |
| `PlayerFacingSystem` | `Assets/Scripts/Player/Systems/PlayerFacingSystem.cs` | Always | `TargetData.AimDirection` for CameraForward, CursorDirection, MovementDirection |
| `CameraRaycastTargeting` | `Assets/Scripts/Targeting/Implementations/CameraRaycastTargeting.cs` | MonoBehaviour (scene-placed) | `TargetData` (screen center raycast) |
| `CursorAimTargeting` | `Assets/Scripts/Targeting/Implementations/CursorAimTargeting.cs` | MonoBehaviour (scene-placed) | `TargetData` (cursor-to-world) |
| `LockOnTargeting` | `Assets/Scripts/Targeting/Implementations/LockOnTargeting.cs` | MonoBehaviour | `TargetData` (Mode=LockOn) |
| `AutoTargetTargeting` | `Assets/Scripts/Targeting/Implementations/AutoTargetTargeting.cs` | MonoBehaviour | `TargetData` (Mode=AutoTarget) |

### TargetingMode Enum

| Mode | Description | Intended Paradigm |
|------|-------------|-------------------|
| CameraRaycast | Fire toward screen center / crosshair | Shooter, TwinStick |
| CursorAim | Fire toward mouse cursor in world | ARPG, MOBA |
| AutoTarget | Auto-lock nearest enemy | Fast action |
| LockOn | Manual lock-on, tab cycle | Souls-like |
| ClickSelect | Click enemy to select, then ability | MMO, ARPG |

### Paradigm → Combat Map Mapping (`ParadigmInputManager.ApplyParadigmMaps`)

- **Shooter, TwinStick** → Combat_Shooter (Attack, AimDownSights)
- **MMO** → Combat_MMO (SelectTarget, CameraOrbit)
- **ARPG** → Combat_ARPG (AttackAtCursor, MoveToClick)
- **MOBA** → Combat_MOBA (AttackAtCursor, AttackMove, Stop, HoldPosition)

### PlayerInputState Mapping (from `PlayerInputReader`)

| Action | State Field | Paradigm |
|--------|-------------|----------|
| Attack | Fire | Shooter, TwinStick |
| SelectTarget | Select | MMO |
| AttackAtCursor | Fire | ARPG, MOBA |
| MoveToClick | Aim | ARPG |
| CameraOrbit | CameraOrbit | MMO |

### CursorClickTargetSystem Button Logic

- `_selectButton == 0` (LMB): `Fire || Select` — works for Shooter (Fire), MMO (Select), ARPG (Fire), MOBA (Fire)
- `_clearButton == 1` (RMB): `Aim || CameraOrbit` — works for MMO (CameraOrbit), ARPG (Aim = MoveToClick)

**Conflict:** In ARPG, RMB = MoveToClick. When user holds RMB to move, `CursorClickTargetSystem` treats it as clear-button held → clears selection. That may be correct (moving = deselect). But LMB = AttackAtCursor = Fire. So LMB click on enemy → select + fire. LMB click on ground → `CursorClickTargetSystem` clears selection (ground click). `ClickToMoveHandler` (ARPG_Classic: LMB for move) would also process LMB. So LMB on ground = move + clear. Order of operations matters.

---

## Per-Paradigm Targeting Behavior (Expected)

### Shooter (Profile_Shooter)

- **Cursor:** Locked (center of screen)
- **Targeting:** CameraRaycast or crosshair-based. `PlayerFacingSystem` writes `AimDirection` from camera yaw/pitch (MovementFacingMode.CameraForward)
- **Attack:** LMB = Attack → Fire. `WeaponFireSystem` reads `TargetData.AimDirection`
- **Click-select:** N/A — `CursorHoverSystem` / `CursorClickTargetSystem` do not run (IsCursorFree = false)

### Shooter Hybrid (Profile_ShooterHybrid)

- **Cursor:** Locked by default, Alt frees it
- **When locked:** Same as Shooter
- **When free (Alt held):** `CursorHoverSystem` and `CursorClickTargetSystem` run. LMB = Fire → click can select. Behavior should match MMO during free-cursor phase

### MMO (Profile_MMO)

- **Cursor:** Free
- **Targeting:** ClickSelect. LMB = SelectTarget → select enemy. RMB = CameraOrbit (hold to rotate camera)
- **Attack:** Abilities/auto-attack use `TargetData.TargetEntity` (selected target). No direct "Attack" action in Combat_MMO — abilities triggered by hotkeys
- **Flow:** Click enemy → `CursorClickTargetSystem` sets `TargetData` with Mode=ClickSelect, TargetEntity, AimDirection from player→target

### ARPG Classic (Profile_ARPG_Classic)

- **Cursor:** Free
- **Movement:** Click-to-move, LMB (clickToMoveButton=LeftButton)
- **Targeting:** CursorAim or ClickSelect. LMB = AttackAtCursor (Fire). RMB = MoveToClick (Aim)
- **Conflict:** LMB is used for BOTH click-to-move AND AttackAtCursor. Resolution: click on ground → move; click on enemy → attack + select. `ClickToMoveHandler` raycasts; if ground, starts path. `CursorClickTargetSystem` on LMB: if entity, select + Fire. Both can fire on same click — need to ensure ground vs entity discrimination (e.g. entity layer blocks move, or move has lower priority when entity under cursor)

### ARPG Hybrid (Profile_ARPG_Hybrid)

- **Cursor:** Free
- **Movement:** WASD only (clickToMoveEnabled=false). No LMB move conflict.
- **Targeting:** Same as ARPG Classic for attack (CursorAim / ClickSelect). LMB = AttackAtCursor, RMB = MoveToClick

### MOBA (Profile_MOBA)

- **Cursor:** Free
- **Movement:** RMB click-to-move (clickToMoveButton=RightButton)
- **Targeting:** LMB = AttackAtCursor (Fire). RMB = move. So LMB = attack/select, RMB = move. `CursorClickTargetSystem` select = Fire (LMB), clear = Aim (RMB). But in MOBA, RMB = move, not clear. So RMB click would both move AND clear target. That may be acceptable (move = deselect).
- **AttackMove:** A-move — move to point, auto-attack enemies along the way

### TwinStick (Profile_TwinStick)

- **Cursor:** Free
- **Combat map:** Combat_Shooter (same as Shooter)
- **Targeting:** CursorDirection (MovementFacingMode) — `PlayerFacingSystem` syncs AimDirection from character rotation. For cursor-aim style, would need CursorAimTargeting or similar
- **Attack:** LMB = Attack (Fire). Cursor free → `CursorClickTargetSystem` runs. LMB = Fire → click selects. So click enemy = attack + select. Aim direction from character facing (WASD + cursor for rotation?)

---

## Gaps & Risks

### Gap 1: InputParadigmProfile Has No Targeting Mode

`InputParadigmProfile` defines cursor, movement, camera — but not targeting. `TargetingConfig` exists separately. At runtime, `TargetDataAuthoring.InitialMode` (baked) or `TargetingModeTester` (debug) set the mode. There is no automatic paradigm → targeting mode mapping.

**Risk:** Wrong targeting mode for paradigm (e.g. ARPG using CameraRaycast instead of CursorAim).

### Gap 2: Cursor-Gated Systems Exclude Shooter When Cursor Freed

When Shooter Hybrid holds Alt, cursor frees. `CursorHoverSystem` and `CursorClickTargetSystem` then run. But Shooter uses Fire for LMB — so click = Fire + select. That is consistent. When cursor locks again, those systems stop; `PlayerFacingSystem` / CameraRaycast continue. OK.

### Gap 3: ARPG LMB Dual Use (Move vs Attack)

ARPG_Classic: LMB = click-to-move AND AttackAtCursor. `ClickToMoveHandler` and `CursorClickTargetSystem` both react to LMB. If user clicks ground: move starts, CursorClickTarget clears selection. If user clicks enemy: CursorClickTarget selects + Fire, WeaponFireSystem fires. Does `ClickToMoveHandler` also try to move when clicking enemy? It raycasts — if the ray hits the enemy first (depending on layers), it might not hit ground. Need to verify layer/raycast order so ground vs entity is unambiguous.

### Gap 4: MOBA RMB = Move vs Clear

MOBA: RMB = move. `CursorClickTargetSystem` uses RMB as clear button. So RMB click = move + clear target. Acceptable if moving implies deselect.

### Gap 5: No Combat_TwinStick Map

TwinStick reuses Combat_Shooter. That is intentional (same Attack/Aim bindings) but means TwinStick has no paradigm-specific actions. If future TwinStick needs different bindings, a dedicated map would be needed.

### Gap 6: MonoBehaviour Targeting Systems Not Paradigm-Aware

`CameraRaycastTargeting`, `CursorAimTargeting`, etc. are MonoBehaviours. They are not invoked by a central coordinator that switches based on paradigm. They run if present in the scene. `PlayerFacingSystem` provides AimDirection for CameraForward and CursorDirection — so the main flow may not depend on those MonoBehaviours. Need to confirm which systems actually drive `TargetData` per paradigm.

### Gap 7: Melee vs Ranged Targeting

`WeaponFireSystem` reads `TargetData.AimDirection` for ranged. `MeleeActionSystem` uses hitbox and combo state — it does not explicitly read `TargetData` for aim. Melee typically uses character facing or lock-on. Ensure melee flows use correct targeting for each paradigm.

---

## Architecture: Proposed Solution

### 1. Add Targeting Mode to InputParadigmProfile

```csharp
// InputParadigmProfile.cs
[Header("Targeting")]
public TargetingMode defaultTargetingMode = TargetingMode.CameraRaycast;
```

Per-profile defaults:

| Profile | defaultTargetingMode |
|---------|----------------------|
| Shooter, ShooterHybrid | CameraRaycast |
| MMO | ClickSelect |
| ARPG Classic, ARPG Hybrid | CursorAim or ClickSelect (designer choice) |
| MOBA | CursorAim or ClickSelect |
| TwinStick | CameraRaycast or CursorAim |

### 2. Paradigm → Targeting Sync

On paradigm switch, a `ParadigmTargetingSyncSystem` or `TargetingBridge` updates the local player's `TargetData.Mode` from the active profile's `defaultTargetingMode`. Optionally, when `CursorClickTargetSystem` writes Mode=ClickSelect on user click, that overrides until cleared.

### 3. Cursor-Aim for ARPG/MOBA

When `defaultTargetingMode == CursorAim`, ensure `TargetData.AimDirection` and `TargetData.TargetPoint` are updated from cursor-to-world projection each frame. `CursorAimTargeting` (MonoBehaviour) or an ECS `CursorAimTargetingSystem` could do this. If `PlayerFacingSystem` overwrites for MovementDirection, ordering must be correct: cursor-aim should take precedence when attacking.

### 4. ARPG LMB Discrimination

Document and enforce: LMB on entity (enemy, interactable) → attack + select; LMB on ground → move. Ensure `ClickToMoveHandler` and `CursorClickTargetSystem` use consistent raycast/layer logic. Option: `ClickToMoveHandler` only moves when ray hits walkable ground and no entity is under cursor (from `CursorHoverResult`).

### 5. Optional: TargetingConfig Reference on Profile

Allow `InputParadigmProfile` to reference a `TargetingConfig` ScriptableObject for advanced settings (LockOn cycle key, AutoTarget priority, etc.) instead of a single enum. Keeps flexibility.

---

## Implementation Phases

### Phase 1: Profile Field & Sync (Minimal)

- Add `defaultTargetingMode` to `InputParadigmProfile`
- Set values on existing profiles
- On paradigm switch, write `TargetData.Mode` from profile (via existing configurable flow or new sync system)

### Phase 2: Cursor-Aim for ARPG/MOBA

- Ensure CursorAim mode correctly updates `TargetData` when paradigm is ARPG/MOBA
- Verify `WeaponFireSystem` and melee use `TargetData` correctly for cursor-aim

### Phase 3: ARPG LMB Discrimination

- Audit `ClickToMoveHandler` vs `CursorClickTargetSystem` for LMB on ground vs entity
- Add logic to avoid move when clicking entity (or document current behavior)

### Phase 4: Full Paradigm Matrix Test

- Test matrix: each profile × attack, select, move, clear
- Document expected behavior and fix any mismatches

---

## Verification Checklist

| Paradigm | Cursor | LMB | RMB | Targeting Mode | Attack Flow |
|----------|--------|-----|-----|----------------|-------------|
| Shooter | Locked | Attack | Aim | CameraRaycast | AimDirection from camera |
| Shooter Hybrid (locked) | Locked | Attack | Aim | CameraRaycast | Same as Shooter |
| Shooter Hybrid (Alt) | Free | Attack+Select | Aim | CameraRaycast or ClickSelect | — |
| MMO | Free | Select | CameraOrbit | ClickSelect | Abilities use TargetEntity |
| ARPG Classic | Free | Attack+Move | Move | CursorAim/ClickSelect | Ground=move, Entity=attack |
| ARPG Hybrid | Free | Attack | MoveToClick | CursorAim/ClickSelect | WASD move, LMB=attack only |
| MOBA | Free | Attack+Select | Move | CursorAim/ClickSelect | LMB=attack, RMB=move |
| TwinStick | Free | Attack | Aim | CameraRaycast/CursorAim | Aim from facing or cursor |

---

## References

- EPIC 15.18: Cursor Hover & Click-to-Select
- EPIC 15.20: Input Paradigm Framework
- EPIC 15.21: Input Action Layer
- EPIC 18.15: Click-to-Move & WASD Gating
- `Assets/Scripts/Targeting/` — targeting implementations
- `Assets/Scripts/Core/Input/Paradigm/` — paradigm system
