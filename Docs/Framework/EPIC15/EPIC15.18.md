# EPIC 15.18: Input Scheme Switching & Cursor Hover Targeting

**Status:** ✅ Tier 1 Complete, Tier 2 Deferred (requires Isometric camera gameplay)
**Priority:** Medium
**Dependencies:**
- ✅ `InputContextManager` / `PlayerInputReader` — Input context stack & action callbacks (exists)
- ✅ `PlayerInputSystem` / `PlayerInputState` — ECS input bridge (exists)
- ✅ `PlayerCameraControlSystem` — ECS camera rotation from `LookDelta` (exists)
- ✅ `CursorAimTargeting` / `CameraAwareTargetingBase` — Cursor-to-world projection (exists, EPIC 14.9)
- ✅ `TargetingConfig` / `TargetingMode` — Data-driven targeting mode selection (exists)
- ✅ `CameraInputUtility` / `CameraModeProvider` — Camera-relative input transforms (exists, EPIC 14.9)
- ✅ `IsometricFixedCamera` / `IsometricRotatableCamera` / `TopDownFixedCamera` — Mouse-free camera modes (exist)
- ✅ `TargetData` — Shared ECS targeting output (exists)
- ⚠️ `IInteractable` interface / Interaction system — Required for hover tooltips (TBD)

**Feature:** Switchable Input Paradigms (Shooter vs. Tactical Mouse vs. Hybrid)

---

## Overview

DIG supports multiple gameplay perspectives (TPS, isometric, tactical) which each demand fundamentally different mouse behavior. This epic introduces a runtime-switchable **Input Scheme** layer that sits between the existing `InputContextManager` (Gameplay/UI) and the downstream systems (`PlayerInputSystem`, targeting, camera). The scheme dictates how mouse input is interpreted: as camera rotation, as a free cursor for hover-targeting, or a hybrid of both.

**Design Principle:** This is *not* a replacement for the existing input or targeting architectures. It is a thin routing layer that reconfigures existing systems based on the active scheme. No existing system APIs change — only which systems receive mouse data and how cursor state is managed.

---

## Camera-Mode Coupling Constraint

**Input schemes and camera modes are not independent — they are paired.** The mouse serves double duty: it either rotates the camera (TPS/FPS) or moves a free cursor (isometric/tactical). It cannot do both simultaneously. This constraint shapes the entire design.

### The Coupling

```
┌──────────────────────────────────────────────────────────────────────┐
│                    Camera Mode ←→ Input Scheme                       │
│                                                                      │
│  ┌─────────────────────┐         ┌─────────────────────────────┐    │
│  │ ThirdPersonFollow   │ ◄─────► │ ShooterDirect               │    │
│  │ FirstPerson         │         │ Mouse delta → camera orbit   │    │
│  │ (SupportsOrbit=true)│         │ Cursor locked, hidden        │    │
│  └─────────────────────┘         └─────────────────────────────┘    │
│           │                                                          │
│           │ hold modifier                                            │
│           ▼                                                          │
│  ┌─────────────────────┐         ┌─────────────────────────────┐    │
│  │ Same TPS camera,    │ ◄─────► │ HybridToggle (Alt held)     │    │
│  │ orbit PAUSED         │         │ Camera freezes, cursor frees │    │
│  │ (lookInputScale=0)  │         │ Brief hover/select window    │    │
│  └─────────────────────┘         └─────────────────────────────┘    │
│                                                                      │
│  ┌─────────────────────┐         ┌─────────────────────────────┐    │
│  │ IsometricFixed      │ ◄─────► │ TacticalCursor              │    │
│  │ IsometricRotatable  │         │ Mouse moves free cursor      │    │
│  │ TopDownFixed        │         │ Camera rotation via Q/E or   │    │
│  │ (SupportsOrbit=false│         │ edge-scroll — NOT mouse      │    │
│  │  UsesCursorAim=true)│         │                              │    │
│  └─────────────────────┘         └─────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
```

### What This Means

| Combination | Viable? | Reason |
|-------------|---------|--------|
| ShooterDirect + TPS camera | ✅ Yes | Current default. Mouse rotates camera, crosshair at center. |
| HybridToggle + TPS camera | ✅ Yes | TPS normally; hold Alt to briefly free cursor, camera pauses orbit. Release to resume. Standard MMO pattern (WoW, FFXIV). |
| TacticalCursor + TPS camera | ❌ No | Mouse freed permanently, but TPS camera requires mouse delta for orbit. Camera becomes stuck. No fallback rotation method exists for TPS. |
| TacticalCursor + Isometric camera | ✅ Yes | Isometric cameras already ignore mouse delta (`HandleRotationInput()` is empty). Q/E keys handle rotation. Cursor is free for hover/click. |
| ShooterDirect + Isometric camera | ❌ No | Locking cursor and using mouse delta for camera serves no purpose — isometric cameras don't orbit. |

### Design Decision

**InputScheme is constrained by the active camera mode:**
- `ShooterDirect` — requires `SupportsOrbitRotation == true` (TPS, FPS)
- `TacticalCursor` — requires `SupportsOrbitRotation == false` (Isometric, TopDown)
- `HybridToggle` — requires `SupportsOrbitRotation == true` (TPS with temporary cursor)

The `InputSchemeManager` enforces this: switching camera mode auto-selects a compatible scheme. Manual scheme override is only allowed within compatible camera modes.

---

## Implementation Tiers

Given the coupling constraint, implementation is split into two tiers based on the current TPS camera system.

### Tier 1: HybridToggle (TPS-Compatible) — Implement Now

The immediate, practical feature. Works with the existing TPS camera without any camera system changes.

**Behavior:** Player holds a modifier key (default: `Left Alt`) to temporarily free the cursor. While held:
- `PlayerCameraControlSystem` sees `lookInputScale → 0.0` (camera orbit pauses)
- Cursor unlocks and becomes visible (`CursorLockMode.Confined`)
- `CursorHoverSystem` activates — raycasts under cursor, writes `CursorHoverResult`
- `HoverHighlightSystem` outlines hovered entity
- `CursorIconController` swaps cursor texture based on hover category
- Player can click to set `TargetData.TargetEntity` (soft lock-on)

On release:
- Cursor re-locks, hides
- Camera orbit resumes from where it paused (no snap)
- Hover highlight clears
- Selected target persists in `TargetData` (sticky selection)

**Why this works with TPS:** The camera doesn't need an alternative rotation method. It just freezes for the brief modifier window. The player's last yaw/pitch are preserved in `PlayerCameraSettings` and resume on release.

### Tier 2: TacticalCursor (Isometric-Dependent) — Implement When Isometric Gameplay Is Active

Full-time free cursor mode. Only valid when an isometric or top-down camera mode is active.

**Behavior:** Mouse permanently moves a visible cursor. Camera rotation via Q/E keys (`IsometricRotatableCamera`) or fixed (`IsometricFixedCamera`, `TopDownFixedCamera`). `CursorAimTargeting` (already exists) handles cursor-to-world projection. `CursorHoverSystem` runs continuously.

**Why this is deferred:** The existing gameplay loop is TPS. Activating TacticalCursor requires the game to also switch to an isometric camera mode, which is a broader gameplay context switch beyond just input scheme. The infrastructure for TacticalCursor is largely already in place (isometric cameras exist, `CursorAimTargeting` exists) — the gap is the scheme-switching glue and hover feedback.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Input Pipeline                                    │
│                                                                            │
│  ┌──────────────────┐    ┌──────────────────────┐    ┌──────────────────┐  │
│  │  Unity Input Sys  │───▶│  PlayerInputReader    │───▶│ PlayerInputState │  │
│  │  (DIGInputActions)│    │  (callbacks, gamepad) │    │ (static bridge)  │  │
│  └──────────────────┘    └──────────┬───────────┘    └────────┬─────────┘  │
│                                     │                         │            │
│           ┌─────────────────────────▼─────────────────────────▼──────┐     │
│           │              InputSchemeManager (NEW)                     │     │
│           │  Enforces scheme ←→ camera mode compatibility            │     │
│           │  ┌────────────┬────────────────────────┐                 │     │
│           │  │  Shooter   │  Hybrid                │ Tier 1 (TPS)   │     │
│           │  │  Direct    │  Toggle (hold to free) │                 │     │
│           │  └────┬───────┴───────────┬────────────┘                 │     │
│           │       │                   │                               │     │
│           │  ┌────┴──────────────┐    │                               │     │
│           │  │  Tactical Cursor  │    │              Tier 2 (Iso)     │     │
│           │  │  (iso cameras)    │    │                               │     │
│           │  └────┬──────────────┘    │                               │     │
│           └───────┼───────────────────┼──────────────────────────────┘     │
│                   │                   │                                     │
└───────────────────┼───────────────────┼─────────────────────────────────────┘
                    │                   │
     ┌──────────────▼───────────────────▼──────────────────────┐
     │           When cursor is free (either path)              │
     │                                                          │
     │  ┌─────────────────┐  ┌──────────────┐  ┌────────────┐ │
     │  │ CursorHover     │  │ HoverHighlight│  │ CursorIcon │ │
     │  │ System (NEW)    │  │ System (NEW) │  │ Controller │ │
     │  │ Raycast → class │  │ Outline/tint │  │ (NEW)      │ │
     │  └────────┬────────┘  └──────────────┘  └────────────┘ │
     │           │                                              │
     │  ┌────────▼─────────────────────────────────────────┐   │
     │  │           CursorHoverResult (ECS)                │   │
     │  │  HoveredEntity · HitPoint · Category · IsValid   │   │
     │  └──────────────────────────────────────────────────┘   │
     └─────────────────────────────────────────────────────────┘
                    │
     ┌──────────────▼──────────────────────────────────────────┐
     │                 TargetData (ECS)                         │
     │  TargetEntity · AimDirection · TargetPoint               │
     ├─────────────────────────────────────────────────────────┤
     │  Consumers: Weapons, Abilities, AI, UI                  │
     └─────────────────────────────────────────────────────────┘
```

**Key Insight:** `CursorHoverSystem`, `HoverHighlightSystem`, and `CursorIconController` are shared infrastructure. They activate whenever the cursor is free — whether from HybridToggle (modifier held in TPS) or TacticalCursor (permanent in isometric). The systems don't know or care *why* the cursor is free.

---

## Existing Systems Audit

| System | Status | Role in 15.18 |
|--------|--------|---------------|
| `InputContextManager` | ✅ Exists | Manages Gameplay/UI context stack. **No changes** — scheme layer sits alongside it |
| `PlayerInputReader` | ✅ Exists | Bridges Input System → `PlayerInputState`. Cursor lock/unlock already here. **Modify** `UpdateCursorState()` to respect active scheme |
| `PlayerCameraControlSystem` | ✅ Exists | Applies `LookDelta` to yaw/pitch. Already supports `lookInputScale = 0.0` for lock modes. **Reuse** — no changes needed. HybridToggle piggybacks on the existing lock-scale mechanism |
| `PlayerInputSystem` | ✅ Exists | ECS input sampling. **No direct changes** — routing handled by new `InputSchemeRoutingSystem` |
| `CursorAimTargeting` | ✅ Exists | Cursor-to-world projection + entity raycast. **Activate** when scheme = TacticalCursor (Tier 2) |
| `IsometricFixedCamera` | ✅ Exists | `SupportsOrbitRotation = false`, `UsesCursorAiming = true`, empty `HandleRotationInput()`. **Ready** for TacticalCursor pairing |
| `IsometricRotatableCamera` | ✅ Exists | Q/E discrete rotation, no mouse dependency. **Ready** for TacticalCursor pairing |
| `TopDownFixedCamera` | ✅ Exists | Fixed angle, no mouse dependency. **Ready** for TacticalCursor pairing |
| `CameraInputUtility` | ✅ Exists | `ProjectCursorToGroundPlane()`, `CalculateAimDirection()`, etc. **Reuse** — no changes needed |
| `TargetingConfig` | ✅ Exists | Already supports `TargetingMode.CursorAim`. **Use** preset switching tied to scheme |
| `IInteractable` | ⚠️ TBD | Needed for hover tooltips on non-combat objects. Can stub initially |

---

## Components

### 1. `InputScheme` (Enum)

Defines the mouse interpretation paradigm. Separate from `InputContext` (Gameplay/UI) — a scheme only applies while in the `Gameplay` context.

```csharp
// Assets/Scripts/Core/Input/InputScheme.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Defines how mouse input is routed during Gameplay context.
    /// Orthogonal to InputContext (Gameplay/UI) — schemes only apply during Gameplay.
    ///
    /// CONSTRAINT: Schemes are paired with camera modes.
    /// ShooterDirect/HybridToggle require SupportsOrbitRotation cameras (TPS/FPS).
    /// TacticalCursor requires UsesCursorAiming cameras (Isometric/TopDown).
    /// </summary>
    public enum InputScheme : byte
    {
        /// <summary>
        /// TPS/FPS default. Mouse delta rotates camera. Cursor locked and hidden.
        /// Targeting via CameraRaycast (crosshair center).
        /// Compatible cameras: ThirdPersonFollow, FirstPerson.
        /// </summary>
        ShooterDirect = 0,

        /// <summary>
        /// Hybrid. ShooterDirect by default; holding a modifier key (e.g., Alt)
        /// temporarily frees the cursor and pauses camera orbit.
        /// Compatible cameras: ThirdPersonFollow, FirstPerson.
        /// </summary>
        HybridToggle = 1,

        /// <summary>
        /// ARPG/Tactical. Mouse moves a visible cursor permanently.
        /// Camera rotation via Q/E keys or edge-scroll — NOT mouse.
        /// Compatible cameras: IsometricFixed, IsometricRotatable, TopDownFixed.
        /// </summary>
        TacticalCursor = 2,
    }
}
```

### 2. `InputSchemeState` (ECS Component)

Networked component so the server knows which scheme the client is using (affects aim direction interpretation and validation).

```csharp
// Assets/Scripts/Core/Input/Components/InputSchemeState.cs
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Core.Input
{
    /// <summary>
    /// Replicated input scheme state. Server needs this to correctly interpret
    /// aim direction (camera-forward vs cursor-projected).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InputSchemeState : IComponentData
    {
        /// <summary>Active input scheme for this player.</summary>
        [GhostField] public InputScheme ActiveScheme;

        /// <summary>True when hybrid modifier key is held (HybridToggle only).</summary>
        [GhostField] public bool IsTemporaryCursorActive;
    }
}
```

### 3. `CursorHoverResult` (ECS Component)

Output component written by the hover system. Downstream systems (UI, tooltips, outlines) read this without coupling to input logic.

```csharp
// Assets/Scripts/Targeting/Components/CursorHoverResult.cs
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting
{
    /// <summary>
    /// Result of cursor hover raycasting. Written by CursorHoverSystem,
    /// read by UI (tooltip, cursor icon) and highlighting systems.
    ///
    /// Only populated when cursor is free (HybridToggle+modifier or TacticalCursor).
    /// When cursor is locked (ShooterDirect), IsValid is always false.
    /// </summary>
    public struct CursorHoverResult : IComponentData
    {
        /// <summary>Entity currently under the cursor (Entity.Null if none).</summary>
        public Entity HoveredEntity;

        /// <summary>World position of the hover hit point.</summary>
        public float3 HitPoint;

        /// <summary>What category the hovered object falls into.</summary>
        public HoverCategory Category;

        /// <summary>True if the raycast hit anything valid this frame.</summary>
        public bool IsValid;
    }

    /// <summary>
    /// Classification of what the cursor is hovering over.
    /// Drives cursor icon selection and available interactions.
    /// </summary>
    public enum HoverCategory : byte
    {
        None = 0,
        Enemy = 1,
        Friendly = 2,
        Interactable = 3,
        Lootable = 4,
        Ground = 5,
    }
}
```

---

## Systems

### System 1: `InputSchemeManager` (MonoBehaviour — Singleton)

**Purpose:** Runtime scheme switching. Enforces scheme ↔ camera mode compatibility. Configures cursor state and notifies downstream systems. Works alongside `InputContextManager`, not inside it.

**Responsibilities:**
- Store and switch the active `InputScheme`
- Query `CameraModeProvider.Instance.ActiveCamera.SupportsOrbitRotation` to validate scheme compatibility
- Update cursor lock/visibility via `PlayerInputReader`
- Synchronize `InputSchemeState` ECS component for netcode
- Fire `OnSchemeChanged` event for UI/systems that need immediate notification
- Listen for camera mode changes and auto-switch scheme if current becomes incompatible

```csharp
// Assets/Scripts/Core/Input/InputSchemeManager.cs
namespace DIG.Core.Input
{
    public class InputSchemeManager : MonoBehaviour
    {
        public static InputSchemeManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private InputScheme _defaultScheme = InputScheme.ShooterDirect;
        [SerializeField] private KeyCode _hybridModifierKey = KeyCode.LeftAlt;

        public InputScheme ActiveScheme { get; private set; }
        public bool IsTemporaryCursorActive { get; private set; }

        /// <summary>True when cursor is currently free (either TacticalCursor or Hybrid+modifier held).</summary>
        public bool IsCursorFree => ActiveScheme == InputScheme.TacticalCursor
                                 || (ActiveScheme == InputScheme.HybridToggle && IsTemporaryCursorActive);

        public event System.Action<InputScheme> OnSchemeChanged;

        /// <summary>
        /// Returns true if LookDelta should be suppressed (cursor is free).
        /// Called by PlayerInputReader.OnLook().
        /// </summary>
        public bool ShouldSuppressLookDelta() => IsCursorFree;

        /// <summary>
        /// Attempt to switch scheme. Validates against current camera mode.
        /// Returns false and logs warning if incompatible.
        /// </summary>
        public bool TrySetScheme(InputScheme scheme)
        {
            // Validate against active camera mode:
            //   ShooterDirect/HybridToggle → requires SupportsOrbitRotation
            //   TacticalCursor → requires UsesCursorAiming && !SupportsOrbitRotation
            // If incompatible, reject and log warning.
            // If compatible, apply and fire OnSchemeChanged.
            return false; // placeholder
        }

        // Camera mode change listener:
        //   When CameraModeProvider switches to isometric → auto-switch to TacticalCursor
        //   When CameraModeProvider switches to TPS → auto-switch to ShooterDirect (or HybridToggle)
    }
}
```

**Scheme Behavior Table:**

| Property | ShooterDirect | HybridToggle (default) | HybridToggle (Alt held) | TacticalCursor |
|----------|---------------|------------------------|-------------------------|----------------|
| Compatible cameras | TPS, FPS | TPS, FPS | TPS, FPS | Isometric, TopDown |
| Cursor locked | Yes | Yes | **No** (Confined) | No (Confined) |
| Cursor visible | No | No | **Yes** | Yes |
| `LookDelta` | Mouse delta | Mouse delta | **Zero** | Zero |
| Camera rotation | Mouse orbit | Mouse orbit | **Paused** (`lookInputScale=0`) | Q/E keys or fixed |
| `AimDirection` source | Camera forward | Camera forward | **Cursor → world** | Cursor → world |
| Active `TargetingMode` | CameraRaycast | CameraRaycast | **CursorAim** | CursorAim |
| Hover system active | No | No | **Yes** | Yes |
| `CursorHoverResult` valid | No | No | **Yes** | Yes |

---

### System 2: `CursorHoverSystem` (MonoBehaviour)

**Purpose:** When cursor is free (any path — HybridToggle+modifier or TacticalCursor), raycast under the mouse to identify what's being hovered. Write results to `CursorHoverResult`.

**Activation:** Checks `InputSchemeManager.Instance.IsCursorFree`. Early-outs when false. This means the system is camera-mode-agnostic — it doesn't care *why* the cursor is free, only *that* it is.

**Update Order:** After `PlayerInputReader`, before UI systems.

```csharp
// Assets/Scripts/Targeting/Systems/CursorHoverSystem.cs
namespace DIG.Targeting
{
    /// <summary>
    /// Raycasts under the free mouse cursor to identify hovered entities.
    /// Only active when InputSchemeManager.IsCursorFree is true.
    ///
    /// Camera-mode agnostic: works identically whether cursor was freed by
    /// HybridToggle modifier (TPS) or TacticalCursor (isometric).
    ///
    /// Reuses CameraInputUtility.ProjectCursor() for world-space projection
    /// and EntityLink for ECS entity resolution.
    /// </summary>
    public class CursorHoverSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float _maxHoverRange = 100f;
        [SerializeField] private LayerMask _hoverableLayers = ~0;
        [SerializeField] private float _hoverRayRadius = 0.1f; // SphereCast for forgiving hover

        // Pipeline per frame (when IsCursorFree):
        //   1. Early-out if !InputSchemeManager.Instance.IsCursorFree
        //   2. Camera.ScreenPointToRay(Input.mousePosition)
        //   3. SphereCast with _hoverRayRadius for forgiving selection
        //   4. Classify hit: EntityLink → check for team/faction tag → HoverCategory
        //   5. Write CursorHoverResult to player entity
        //   6. Raise OnHoverChanged event if entity changed (for UI debounce)

        // When IsCursorFree transitions false → clear CursorHoverResult (IsValid = false)
    }
}
```

**Entity Classification Logic:**

```
Hit collider
  └─ GetComponentInParent<EntityLink>()
       ├─ null → HoverCategory.Ground (if ground layer) or None
       └─ entity found
            ├─ Has HealthComponent + hostile faction → Enemy
            ├─ Has HealthComponent + friendly faction → Friendly
            ├─ Has IInteractable → Interactable
            ├─ Has LootContainer → Lootable
            └─ else → Ground
```

---

### System 3: `InputSchemeRoutingSystem` (ISystem — ECS)

**Purpose:** Modify `PlayerInput` component values based on the active scheme *before* downstream systems read them. This is the ECS-side scheme enforcement.

**Update Order:** In `GhostInputSystemGroup`, after `PlayerInputSystem`.

```csharp
// Assets/Scripts/Core/Input/Systems/InputSchemeRoutingSystem.cs
namespace DIG.Core.Input
{
    /// <summary>
    /// Reads InputSchemeState and adjusts PlayerInput accordingly:
    /// - ShooterDirect: no modification (pass-through)
    /// - HybridToggle + modifier held: zeros LookDelta, writes cursor AimDirection
    /// - TacticalCursor: zeros LookDelta, writes cursor AimDirection
    ///
    /// Runs AFTER PlayerInputSystem so it overrides only the fields that differ.
    /// </summary>
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [UpdateAfter(typeof(PlayerInputSystem))]
    [BurstCompile]
    public partial struct InputSchemeRoutingSystem : ISystem
    {
        // Per-entity logic:
        //   1. Read InputSchemeState
        //   2. If ShooterDirect → return (no-op)
        //   3. If HybridToggle && !IsTemporaryCursorActive → return (no-op, normal shooter)
        //   4. If cursor is free (HybridToggle+modifier OR TacticalCursor):
        //      a. Set PlayerInput.LookDelta = float2.zero
        //      b. Read cursor world position from CursorHoverResult.HitPoint
        //      c. Calculate AimDirection = normalize(HitPoint - PlayerPosition)
        //      d. Write to PlayerInput.AimDirection
    }
}
```

---

### System 4: `CursorIconController` (MonoBehaviour — UI)

**Purpose:** Swap the hardware/software cursor texture based on `CursorHoverResult.Category`. Only active when cursor is visible.

```csharp
// Assets/Scripts/UI/Cursor/CursorIconController.cs
namespace DIG.UI
{
    /// <summary>
    /// Reads CursorHoverResult and swaps cursor icon accordingly.
    /// Self-activates/deactivates based on InputSchemeManager.IsCursorFree.
    /// </summary>
    public class CursorIconController : MonoBehaviour
    {
        [System.Serializable]
        public struct CursorEntry
        {
            public HoverCategory Category;
            public Texture2D Texture;
            public Vector2 Hotspot;
        }

        [SerializeField] private CursorEntry[] _cursorMap;
        [SerializeField] private Texture2D _defaultCursor;

        // Each frame (when IsCursorFree):
        //   1. Read CursorHoverResult from player entity
        //   2. If category changed → Cursor.SetCursor(texture, hotspot, CursorMode.Auto)
        //
        // When IsCursorFree transitions false → Cursor.SetCursor(null) to restore default
    }
}
```

**Default Cursor Map:**

| HoverCategory | Icon | Description |
|---------------|------|-------------|
| None / Ground | Default arrow | Standard pointer |
| Enemy | Crosshair / sword | Attack intent |
| Friendly | Shield / speech | Interaction intent |
| Interactable | Hand / gear | Use intent |
| Lootable | Bag / sparkle | Pickup intent |

---

### System 5: `HoverHighlightSystem` (MonoBehaviour)

**Purpose:** Apply visual feedback (outline, tint, shader effect) to the entity under the cursor.

```csharp
// Assets/Scripts/Targeting/Systems/HoverHighlightSystem.cs
namespace DIG.Targeting
{
    /// <summary>
    /// Applies/removes highlight effects on the hovered entity.
    /// Reads CursorHoverResult, manages a single active highlight.
    /// Decoupled from input — any system that writes CursorHoverResult drives this.
    ///
    /// Self-cleans when IsCursorFree transitions false (removes active highlight).
    /// </summary>
    public class HoverHighlightSystem : MonoBehaviour
    {
        [Header("Highlight Settings")]
        [SerializeField] private Color _enemyOutlineColor = Color.red;
        [SerializeField] private Color _friendlyOutlineColor = Color.green;
        [SerializeField] private Color _interactableOutlineColor = Color.yellow;
        [SerializeField] private float _outlineWidth = 2f;

        // Per frame (when IsCursorFree):
        //   1. Read CursorHoverResult
        //   2. If HoveredEntity changed:
        //      a. Remove highlight from previous entity (MaterialPropertyBlock reset)
        //      b. Apply highlight to new entity (outline color based on category)
        //   3. If HoveredEntity == Entity.Null: clear all highlights
        //
        // When IsCursorFree transitions false:
        //   Remove active highlight, reset state
    }
}
```

---

### System 6: `CursorClickTargetSystem` (MonoBehaviour)

**Purpose:** Bridge between cursor hover and target selection. On click while `IsCursorFree`, reads `CursorHoverResult` and writes `TargetData` — no redundant raycast.

**Key Design Decisions:**
- Uses `LateUpdate` to write `TargetData`, ensuring it overrides the regular targeting system's `Update` writes while cursor is free.
- Only writes while `IsCursorFree` is true. Once the cursor locks (Alt released), regular targeting resumes and takes back ownership of `TargetData`.
- Left-click on a valid entity → selects target. Left-click on ground → clears selection. Right-click → clears selection.
- Fires `OnTargetSelected` / `OnTargetCleared` events for UI or other reactive systems.

**Sticky Target Limitation (Future Work):** Currently, the selected target only persists in `TargetData` while the cursor is free. Once Alt is released and the regular targeting system resumes writing, the click-selection is overwritten. True "sticky targeting" (selection persists after Alt release) requires teaching the active targeting system to respect a pinned target entity — that's a follow-up task involving targeting orchestration changes.

```csharp
// Assets/Scripts/Targeting/Systems/CursorClickTargetSystem.cs
namespace DIG.Targeting
{
    public class CursorClickTargetSystem : MonoBehaviour
    {
        [SerializeField] private int _selectButton = 0;   // Left click
        [SerializeField] private int _clearButton = 1;     // Right click

        public event System.Action<Entity> OnTargetSelected;
        public event System.Action OnTargetCleared;

        // Update: listen for clicks when IsCursorFree
        // LateUpdate: write TargetData (overrides regular targeting)
    }
}
```

---

## Modifications to Existing Systems

### `PlayerInputReader.cs` — Cursor State

The existing `UpdateCursorState()` hard-locks the cursor during gameplay. Modify to respect the active scheme.

```csharp
// MODIFY: Assets/Scripts/Core/Input/PlayerInputReader.cs
// UpdateCursorState() changes:

private void UpdateCursorState()
{
    if (DIG.UI.MenuState.IsAnyMenuOpen())
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return;
    }

    // NEW: Defer to InputSchemeManager for cursor state during gameplay
    var scheme = InputSchemeManager.Instance;
    if (scheme == null)
    {
        // Fallback: original behavior (locked cursor)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        return;
    }

    if (scheme.IsCursorFree)
    {
        Cursor.lockState = CursorLockMode.Confined; // Keep in window, but free
        Cursor.visible = true;
    }
    else
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
```

### `PlayerInputReader.cs` — LookDelta Suppression

When the cursor is free, `OnLook` should still capture the raw delta (for potential edge-scroll use) but zero out `LookDelta` so the camera doesn't rotate.

```csharp
// MODIFY: Assets/Scripts/Core/Input/PlayerInputReader.cs
// OnLook() changes:

public void OnLook(InputAction.CallbackContext context)
{
    var raw = context.ReadValue<Vector2>();
    _isGamepad = context.control?.device is Gamepad;

    if (_isGamepad)
    {
        raw = ApplyAimAcceleration(raw) * _gamepadLookSensitivity * Time.deltaTime;
    }

    // NEW: Always store raw delta, but suppress LookDelta when cursor is free
    PlayerInputState.RawLookDelta = new float2(raw.x, raw.y);

    bool suppressLook = InputSchemeManager.Instance != null
        && InputSchemeManager.Instance.ShouldSuppressLookDelta();

    PlayerInputState.LookDelta = suppressLook ? float2.zero : new float2(raw.x, raw.y);
}
```

### `PlayerInputState.cs` — New Fields

```csharp
// MODIFY: Assets/Scripts/Player/Systems/PlayerInputState.cs
// Add:
public static float2 RawLookDelta;        // Unfiltered mouse delta (for edge-scroll in Tier 2)
public static float2 CursorScreenPosition; // Updated by PlayerInputReader each frame
```

---

## Tier 1 Implementation: HybridToggle Detail

This section specifies the exact behavior for the near-term TPS-compatible implementation.

### Modifier Key Flow

```
Update() each frame:
│
├─ ActiveScheme != HybridToggle → skip
│
├─ Input.GetKeyDown(hybridModifierKey):
│   ├─ IsTemporaryCursorActive = true
│   ├─ Cursor.lockState = Confined, Cursor.visible = true
│   ├─ Sync InputSchemeState.IsTemporaryCursorActive = true to ECS
│   └─ CursorHoverSystem begins raycasting
│
├─ Input.GetKeyUp(hybridModifierKey):
│   ├─ IsTemporaryCursorActive = false
│   ├─ Cursor.lockState = Locked, Cursor.visible = false
│   ├─ Sync InputSchemeState.IsTemporaryCursorActive = false to ECS
│   ├─ CursorHoverSystem stops, clears CursorHoverResult
│   ├─ HoverHighlightSystem clears highlight
│   └─ If player clicked a target while Alt held → TargetData persists (sticky)
│
└─ While modifier held:
    ├─ PlayerInputState.LookDelta = zero (camera frozen)
    ├─ PlayerCameraControlSystem sees zero delta → no yaw/pitch change
    ├─ Camera position/angle preserved in PlayerCameraSettings
    ├─ CursorHoverSystem raycasts each frame
    ├─ Click → write TargetData.TargetEntity (soft lock-on)
    └─ Right-click → optional: context action on hovered entity
```

### Camera Freeze Mechanism

The existing `PlayerCameraControlSystem` already supports `lookInputScale = 0.0` for lock modes. HybridToggle leverages this:

```
PlayerCameraControlSystem existing code path:
  lookDelta = netInp.LookDelta;  // ← will be float2.zero when modifier held
  ...
  settings.Yaw += lookDelta.x * sensitivity * lookInputScale;  // 0 * anything = 0
  settings.Pitch -= lookDelta.y * sensitivity * lookInputScale; // 0 * anything = 0
```

No changes to `PlayerCameraControlSystem` needed. Zeroing `LookDelta` in `PlayerInputReader` is sufficient — the camera naturally freezes because its input is zero.

### Target Persistence on Release

When the player clicks an entity while the modifier is held, `TargetData.TargetEntity` is set. On modifier release, `CursorHoverResult` clears but `TargetData` is **not** cleared. This gives "sticky targeting" — the player briefly frees cursor, clicks a target, releases Alt, and continues shooting at that target with camera-forward aiming. This matches the MMO pattern where Alt-click selects a target that persists.

Clearing the target is a separate action (e.g., `Escape` key, or clicking empty space while modifier held).

---

## Authoring

### `InputSchemeAuthoring.cs`

Bakes `InputSchemeState` onto the player entity so it's available in ECS.

```csharp
// Assets/Scripts/Core/Input/Authoring/InputSchemeAuthoring.cs
namespace DIG.Core.Input
{
    public class InputSchemeAuthoring : MonoBehaviour
    {
        [Tooltip("Default input scheme for this player prefab.")]
        public InputScheme DefaultScheme = InputScheme.ShooterDirect;

        class Baker : Baker<InputSchemeAuthoring>
        {
            public override void Bake(InputSchemeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new InputSchemeState
                {
                    ActiveScheme = authoring.DefaultScheme,
                    IsTemporaryCursorActive = false,
                });

                // Ensure CursorHoverResult exists for hover system output
                AddComponent(entity, new CursorHoverResult());
            }
        }
    }
}
```

---

## Integration Points

| System | Integration |
|--------|-------------|
| **InputContextManager** | `InputSchemeManager` only operates when `CurrentContext == Gameplay`. Scheme is irrelevant during UI context. |
| **PlayerCameraControlSystem** | Receives zeroed `LookDelta` when cursor is free. Existing behavior handles this — no changes needed. Yaw/pitch preserved in `PlayerCameraSettings`, resume seamlessly on modifier release. |
| **CameraModeProvider** | `InputSchemeManager` listens for camera mode switches. Auto-selects compatible scheme when camera changes (TPS → ShooterDirect/HybridToggle, Isometric → TacticalCursor). |
| **Targeting (EPIC 14.9)** | HybridToggle+modifier temporarily switches active targeting to `CursorAim`. On release, reverts to `CameraRaycast` (but selected target persists). TacticalCursor permanently uses `CursorAim`. |
| **Vision (EPIC 15.17)** | No direct dependency. Vision operates on AI entities regardless of player input scheme. |
| **Aggro (EPIC 15.19)** | No direct dependency. Aggro reads `TargetData` which is written by whichever targeting mode is active. |
| **Netcode** | `InputSchemeState` is `[GhostField]` replicated. Server knows whether to interpret aim as camera-forward or cursor-projected. `InputSchemeRoutingSystem` runs in `GhostInputSystemGroup`. |
| **UI** | `CursorIconController` and `HoverHighlightSystem` read `CursorHoverResult`. Self-activate/deactivate based on `IsCursorFree`. No coupling to input code. |
| **Health Bar Visibility (EPIC 15.16)** | `EnemyHealthBarBridgeSystem` reads `CursorHoverResult.HitPoint` and `TargetData.TargetPoint` to feed `WhenHovered` and `WhenTargeted` visibility modes. Uses XZ position matching (ignores Y) to find the corresponding server entity since cursor hit points can be anywhere on the collider surface. |

---

## Prerequisites & Implementation Order

### Tier 1 (HybridToggle) — No Blockers

| Dependency | Status | Notes |
|------------|--------|-------|
| `EntityLink` on combat entities | ✅ Exists | Used by `CursorAimTargeting` for entity resolution |
| `PlayerCameraControlSystem` lock scaling | ✅ Exists | `lookInputScale = 0.0` path already works for HardLock/IsometricLock |
| `CameraModeProvider` | ✅ Exists | Query `SupportsOrbitRotation` for scheme validation |
| `Faction` / team identification | ⚠️ Stub OK | Hover classification can use layer-based Enemy/Friendly detection initially |

### Tier 2 (TacticalCursor) — Camera Mode Dependent

| Dependency | Status | Notes |
|------------|--------|-------|
| Active isometric/top-down camera mode | ✅ Exists | `IsometricFixedCamera`, `IsometricRotatableCamera`, `TopDownFixedCamera` all implemented |
| `CursorAimTargeting` | ✅ Exists | Full cursor-to-world projection ready |
| Camera mode switching at runtime | ⚠️ Verify | Need to confirm `CameraModeProvider` supports runtime mode swaps cleanly |
| Edge-scroll camera panning | ⚠️ Optional | Nice-to-have for TacticalCursor mode. `RawLookDelta` + screen-edge detection. Defer to future extension. |

### Can Be Stubbed / Deferred (Both Tiers)

| Dependency | Notes |
|------------|-------|
| `IInteractable` interface | Hover tooltip for interactables. Stub: classify as `HoverCategory.Ground` |
| `LootContainer` component | Hover classification for loot. Stub: classify as `Interactable` |
| Outline/highlight shader | Stub: use simple color tint via `MaterialPropertyBlock` |
| Settings UI for scheme selection | Stub: use `InputSchemeAuthoring` default + runtime keybind |

---

## Debug Tools

### `InputSchemeDebugOverlay`

Runtime overlay showing current scheme state for development.

```csharp
// Assets/Scripts/Core/Input/Debug/InputSchemeDebugOverlay.cs
namespace DIG.Core.Input.Debug
{
    /// <summary>
    /// OnGUI overlay displaying:
    /// - Active InputScheme + compatible camera mode
    /// - IsCursorFree state
    /// - Cursor lock/visibility state
    /// - Active TargetingMode
    /// - CursorHoverResult (entity, category, hit point)
    /// - HybridToggle modifier held state + hold duration
    /// - Camera SupportsOrbitRotation flag
    ///
    /// Toggle with backtick (`) key in development builds.
    /// </summary>
    public class InputSchemeDebugOverlay : MonoBehaviour { }
}
```

---

## Verification Plan

### Tier 1 Tests (HybridToggle — TPS Camera)

#### Test 1.1: Modifier Hold/Release

1. Start game in TPS mode with HybridToggle scheme.
2. Verify normal gameplay: mouse rotates camera, cursor locked.
3. Hold `Alt` key.
4. **Expected:** Cursor appears (confined to window). Camera stops rotating. Mouse movement has no effect on yaw/pitch.
5. Move mouse around.
6. **Expected:** Cursor moves freely over the game world.
7. Release `Alt`.
8. **Expected:** Cursor locks and hides. Camera resumes from exact yaw/pitch it was at (no snap/jump). Normal TPS mouse-look.

#### Test 1.2: Hover Feedback During Modifier

1. Hold `Alt` in TPS mode.
2. Move cursor over an enemy entity.
3. **Expected:** `CursorHoverResult.Category == Enemy`. Cursor icon changes. Enemy gets outline highlight.
4. Move cursor to empty ground.
5. **Expected:** Highlight removed. Cursor returns to default.
6. Release `Alt`.
7. **Expected:** All highlights cleared immediately.

#### Test 1.3: Click-to-Select During Modifier

1. Hold `Alt`, hover over enemy, left-click.
2. **Expected:** `TargetData.TargetEntity` set to that enemy.
3. Release `Alt`.
4. **Expected:** Target persists. Weapon systems aim at selected entity. Camera resumes mouse-look.
5. Press `Escape` (or click empty space while Alt held).
6. **Expected:** `TargetData.TargetEntity` clears.

#### Test 1.4: Rapid Toggle

1. Tap `Alt` quickly (press and release within ~100ms).
2. **Expected:** Cursor briefly appears and re-hides. No camera drift. No leftover highlights. Clean state.

#### Test 1.5: UI Menu Interaction

1. Hold `Alt` (cursor free).
2. Press `Escape` to open UI menu.
3. **Expected:** `InputContextManager` switches to UI context. Scheme becomes irrelevant. Cursor behavior is UI-standard.
4. Close menu.
5. **Expected:** Returns to HybridToggle. If `Alt` is still held, cursor stays free. If `Alt` was released during menu, cursor locks.

### Tier 2 Tests (TacticalCursor — Isometric Camera)

#### Test 2.1: Automatic Scheme Switch on Camera Change

1. Start in TPS mode (ShooterDirect).
2. Switch camera to `IsometricFixedCamera`.
3. **Expected:** `InputSchemeManager` auto-switches to TacticalCursor. Cursor appears. Mouse moves cursor, not camera.
4. Switch camera back to TPS.
5. **Expected:** Auto-switches to ShooterDirect. Cursor locks.

#### Test 2.2: Permanent Hover in Isometric

1. Active camera: IsometricFixed. Scheme: TacticalCursor.
2. Move cursor over enemy.
3. **Expected:** Hover highlight active. Cursor icon changes. `CursorHoverResult` populated.
4. Move cursor to ground.
5. **Expected:** Highlight clears. `AimDirection` updated to point from player toward cursor world position.

#### Test 2.3: Invalid Scheme Rejection

1. Active camera: TPS (ThirdPersonFollow).
2. Attempt to force `TrySetScheme(TacticalCursor)`.
3. **Expected:** Returns `false`. Logs warning. Scheme remains ShooterDirect. Cursor stays locked.

### Netcode Tests (Both Tiers)

#### Test N.1: Scheme Replication

1. Two clients connected.
2. Client A holds `Alt` (HybridToggle), clicks an enemy.
3. **Expected:** Server receives `InputSchemeState.IsTemporaryCursorActive = true`. Server correctly interprets `AimDirection` as cursor-projected (not camera-forward). Client B sees Client A aiming at the selected target.

#### Test N.2: Aim Direction Consistency

1. Client in HybridToggle, holding Alt, cursor aimed at point to the right.
2. Fire weapon.
3. **Expected:** Projectile travels toward cursor world position, not camera forward. Server validates aim direction against `InputSchemeState`.

---

## Performance Considerations

| Aspect | Strategy |
|--------|----------|
| **Hover Raycast** | Single `Physics.SphereCast` per frame — negligible cost. Only runs when `IsCursorFree`. In TPS (HybridToggle), this is only during modifier-held windows, not continuous. |
| **Highlight Management** | Track single `previousEntity` — no per-frame allocation. `MaterialPropertyBlock` for outline to avoid material instancing. |
| **Scheme Switching** | Event-driven (`OnSchemeChanged`), not polled. Downstream systems cache scheme state. |
| **ECS Routing** | `InputSchemeRoutingSystem` is Burst-compiled. Per-entity cost is a branch + optional `float2` write. |
| **Cursor Icon** | `Cursor.SetCursor` only called on category change, not every frame. |
| **Modifier Polling** | Single `Input.GetKey()` check per frame in `InputSchemeManager.Update()`. Negligible. |

---

## File Structure

```
Assets/Scripts/Core/Input/
├── InputScheme.cs                          (NEW — enum)
├── InputSchemeManager.cs                   (NEW — MonoBehaviour singleton)
├── InputContextManager.cs                  (EXISTS — no changes)
├── PlayerInputReader.cs                    (EXISTS — modify UpdateCursorState, OnLook)
├── Components/
│   └── InputSchemeState.cs                 (NEW — ECS component)
├── Systems/
│   └── InputSchemeRoutingSystem.cs         (NEW — ECS system)
├── Authoring/
│   └── InputSchemeAuthoring.cs             (NEW — baker)
└── Debug/
    └── InputSchemeDebugOverlay.cs           (NEW — debug UI)

Assets/Scripts/Targeting/
├── Components/
│   └── CursorHoverResult.cs               (NEW — ECS component + HoverCategory enum)
├── Systems/
│   ├── CursorHoverSystem.cs               (NEW — hover raycast)
│   ├── CursorClickTargetSystem.cs         (NEW — click-to-select → TargetData)
│   └── HoverHighlightSystem.cs            (NEW — outline/tint)
└── UI/
    ├── CursorIconController.cs            (NEW — cursor icon swap)
    └── CursorAimIndicator.cs              (EXISTS — no changes)

Assets/Scripts/Player/Systems/
├── PlayerInputState.cs                    (EXISTS — add RawLookDelta, CursorScreenPosition)
└── PlayerInputSystem.cs                   (EXISTS — no direct changes)
```

---

## Future Extensions

| Feature | Tier | Description |
|---------|------|-------------|
| **Edge-Scroll Camera** | T2 | Camera pans when cursor reaches screen edges in TacticalCursor mode. Uses `RawLookDelta` or screen-edge detection. |
| **Click-to-Move** | T2 | TacticalCursor + right-click sends movement command (ARPG-style navigation) |
| **Ability Ground Targeting** | T1/T2 | Hold ability key → show AoE indicator at cursor → release to cast. Works in both HybridToggle (hold Alt+ability) and TacticalCursor. |
| **Per-Weapon Scheme Override** | T2 | Sniper rifle forces ShooterDirect; staff forces TacticalCursor. Requires camera mode co-switch. |
| **Gamepad Virtual Cursor** | T1/T2 | Right stick drives a virtual cursor when `IsCursorFree`. Allows hover/select without mouse. |
| **Tooltip System Integration** | T1/T2 | Hover over interactable shows contextual tooltip with keybind hints |
| **Minimap Click Targeting** | T2 | Click on minimap sets `TargetData.TargetPoint` — reuses `CursorHoverResult` pattern |
