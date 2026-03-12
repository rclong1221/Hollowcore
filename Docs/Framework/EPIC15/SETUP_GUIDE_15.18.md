# EPIC 15.18 Setup Guide: Input Scheme Switching & Cursor Hover Targeting

This guide covers the Unity Editor setup for the **Input Scheme System**, which enables players to temporarily free the cursor (hold Alt) for hover-targeting and click-to-select in third-person gameplay.

---

## Overview

EPIC 15.18 provides switchable mouse input behavior:

- **HybridToggle Mode** – Hold Alt to free cursor, release to resume camera control
- **Cursor Hover Detection** – Raycast under cursor identifies enemies, friendlies, interactables
- **Click-to-Select** – Left-click while cursor is free to select a target
- **Visual Feedback** – Hover highlights and cursor icon changes by target type
- **Sticky Targeting** – Selected target persists after releasing Alt

---

## Quick Start

### 1. Scene Setup (Required Once)

Create the Input Scheme Manager singleton:

1. Create empty GameObject: `_InputSchemeManager`
2. Add Component: **Input Scheme Manager**
3. (Optional) Add Component: **Input Scheme Debug Overlay** – for development testing

**Recommended Hierarchy:**
```
_Managers
└── _InputSchemeManager
    ├── InputSchemeManager
    └── InputSchemeDebugOverlay (optional)
```

### 2. Player Prefab Setup (ECS)

Add to your **Player prefab** (the one baked to ECS):

| Component | Purpose |
|-----------|---------|
| `Input Scheme Authoring` | Bakes scheme state + hover result components |

**Menu Path:** `Add Component > DIG > Core > Input > Input Scheme Authoring`

### 3. Cursor Hover System (Auto-Created)

The `CursorHoverSystem` is typically already in your scene on the player camera or input manager. If not:

1. Add to any persistent GameObject (e.g., `_InputSchemeManager`)
2. Add Component: **Cursor Hover System**

> **Note:** CursorHoverSystem auto-creates `CursorClickTargetSystem`, `HoverHighlightSystem`, and `CursorIconController` at runtime if they don't exist.

---

## Component Reference

### Input Scheme Manager (Singleton)

Place on **one GameObject per scene**. Controls input scheme behavior.

| Property | Description | Default |
|----------|-------------|---------|
| **Default Scheme** | Starting input scheme | HybridToggle |
| **Hybrid Modifier Key** | Key to hold for temporary cursor | Left Alt |
| **Log Scheme Changes** | Debug logging for scheme transitions | false |

**Input Scheme Options:**

| Scheme | Description | Compatible Cameras |
|--------|-------------|-------------------|
| ShooterDirect | Mouse rotates camera, cursor locked | TPS, FPS |
| HybridToggle | Hold modifier to free cursor temporarily | TPS, FPS |
| TacticalCursor | Permanent free cursor (Tier 2, future) | Isometric, TopDown |

---

### Input Scheme Authoring (Player Prefab)

Add to your **Player prefab** for ECS baking.

| Property | Description | Default |
|----------|-------------|---------|
| **Default Scheme** | Initial scheme for this player | ShooterDirect |

This bakes `InputSchemeState` and `CursorHoverResult` components onto the player entity.

---

### Cursor Hover System

Performs raycasting under the cursor when it's free.

| Property | Description | Default |
|----------|-------------|---------|
| **Max Hover Range** | Maximum raycast distance (meters) | 100 |
| **Hoverable Layers** | LayerMask for what can be hovered | Everything |
| **Hover Ray Radius** | SphereCast radius for forgiving selection | 0.15 |
| **Ground Layers** | Layers considered "ground" (not entities) | Default |

**Layer Setup Tips:**
- Create an "Enemy" layer and add enemies to it
- Create an "Interactable" layer for clickable objects
- Exclude UI layers from Hoverable Layers

---

### Hover Highlight System

Applies visual feedback to hovered entities. Auto-created by CursorHoverSystem.

| Property | Description | Default |
|----------|-------------|---------|
| **Enemy Color** | Highlight tint for enemies | Red (1, 0.2, 0.2) |
| **Friendly Color** | Highlight tint for friendlies | Green (0.2, 1, 0.2) |
| **Interactable Color** | Highlight tint for interactables | Yellow (1, 1, 0.2) |
| **Lootable Color** | Highlight tint for loot | Purple (0.6, 0.4, 1) |
| **Emission Intensity** | Strength of the highlight glow | 0.3 |

**Requirements:**
- Target materials must support emission (`_EmissionColor` property)
- Works best with Standard/URP Lit shaders

---

### Cursor Icon Controller

Swaps cursor texture based on hover target type. Auto-created by CursorHoverSystem.

| Property | Description |
|----------|-------------|
| **Cursor Map** | Array of HoverCategory → Texture2D mappings |
| **Default Cursor** | Fallback cursor when hovering nothing |
| **Default Hotspot** | Click point offset for default cursor |

**Setting Up Cursor Icons:**

1. Import cursor textures (PNG, 32x32 or 64x64 recommended)
2. Set texture **Texture Type** to "Cursor" in import settings
3. Add entries to **Cursor Map**:

| Category | Suggested Icon |
|----------|----------------|
| Enemy | Crosshair / Sword |
| Friendly | Speech bubble / Shield |
| Interactable | Hand / Gear |
| Lootable | Bag / Sparkle |
| Ground | Default arrow |

---

### Cursor Click Target System

Handles click-to-select when cursor is free. Auto-created by CursorHoverSystem.

| Property | Description | Default |
|----------|-------------|---------|
| **Select Button** | Mouse button for selection (0=Left, 1=Right, 2=Middle) | 0 (Left) |
| **Clear Button** | Mouse button to clear selection (-1 to disable) | 1 (Right) |

---

### Input Scheme Debug Overlay

Development tool for visualizing scheme state. Toggle with backtick (`) key.

| Property | Description | Default |
|----------|-------------|---------|
| **Show Overlay** | Whether overlay is visible | true |
| **Toggle Key** | Key to show/hide overlay | BackQuote (`) |

**Overlay Shows:**
- Active input scheme
- Cursor free state
- Modifier key held state
- Cursor lock/visibility
- Current hover result (entity, category, hit point)

---

## Gameplay Flow

### HybridToggle (Default TPS Mode)

1. **Normal Play:** Mouse rotates camera, cursor hidden
2. **Hold Alt:** Camera freezes, cursor appears
3. **Hover Enemy:** Enemy gets highlight, cursor changes to crosshair
4. **Left-Click:** Enemy selected as target
5. **Release Alt:** Camera control resumes, target persists
6. **Right-Click (while Alt held):** Clears target selection

---

## Troubleshooting

### Cursor doesn't appear when holding Alt

- Check `InputSchemeManager` exists in scene
- Verify **Hybrid Modifier Key** is set correctly
- Ensure **Default Scheme** is HybridToggle
- Check no UI menus are intercepting input

### Hover doesn't detect enemies

- Verify enemies are on layers included in **Hoverable Layers**
- Check enemies have colliders
- Verify `InputSchemeAuthoring` is on player prefab
- Enable debug overlay (`) to see hover state

### Click-select doesn't work

- Ensure **Select Button** matches your intended mouse button
- Check console for "ClickTarget" debug logs (enable with `ENABLE_CLICK_DEBUG`)
- Verify not clicking through UI (UI blocking is automatic)

### Highlight doesn't show

- Ensure enemy materials support emission
- Check **Emission Intensity** is > 0
- Materials may need emission enabled in shader

### Wrong cursor icon

- Verify **Cursor Map** has entry for the HoverCategory
- Check cursor textures have **Texture Type = Cursor**
- Ensure hotspot is set correctly (typically top-left or center)

---

## Integration with Other Systems

### Health Bar Visibility (EPIC 15.16)

The health bar system reads hover/target state:

| Visibility Mode | Data Source |
|-----------------|-------------|
| WhenHovered | `CursorHoverResult.HoveredEntity` |
| WhenTargeted | `TargetData.TargetEntity` (from click-select) |

### Targeting System (EPIC 14.9)

Click-select writes directly to `TargetData`:
- `TargetData.TargetEntity` – The selected entity
- `TargetData.TargetPoint` – World position of the click

### Vision System (EPIC 15.17)

No direct integration – vision is AI-side, hover is player-side.

---

## Performance Notes

- Hover raycast only runs when cursor is free (modifier held)
- Single SphereCast per frame – negligible cost
- Highlight uses MaterialPropertyBlock (no material instancing)
- Cursor icon only changes on category change

---

## File Locations

| System | Path |
|--------|------|
| InputSchemeManager | `Assets/Scripts/Core/Input/InputSchemeManager.cs` |
| InputSchemeAuthoring | `Assets/Scripts/Core/Input/Authoring/InputSchemeAuthoring.cs` |
| CursorHoverSystem | `Assets/Scripts/Targeting/Systems/CursorHoverSystem.cs` |
| CursorClickTargetSystem | `Assets/Scripts/Targeting/Systems/CursorClickTargetSystem.cs` |
| HoverHighlightSystem | `Assets/Scripts/Targeting/Systems/HoverHighlightSystem.cs` |
| CursorIconController | `Assets/Scripts/Targeting/UI/CursorIconController.cs` |
| InputSchemeDebugOverlay | `Assets/Scripts/Core/Input/Debugging/InputSchemeDebugOverlay.cs` |

---

## Multiplayer Setup (NetCode)

For health bars and damage to sync correctly across clients, enemy prefabs **must be configured as ghosts**.

### Enemy Prefab Ghost Setup

Add to **each enemy prefab** (e.g., BoxingJoe):

1. Open the prefab in Unity
2. Add Component: **Ghost Authoring Component** (from Unity NetCode package)
3. Configure:

| Property | Value | Reason |
|----------|-------|--------|
| **Default Ghost Mode** | Interpolated | Enemies don't need client prediction |
| **Supported Ghost Modes** | Interpolated | Keep it simple |
| **Has Owner** | ❌ Unchecked | Enemies aren't owned by players |
| **Support Auto Command Target** | ❌ Unchecked | Not player-controlled |

4. Save the prefab
5. **Re-bake any SubScenes** containing this enemy (right-click SubScene → Reimport)

### Why This Is Required

Without `GhostAuthoringComponent`:
- Server has Enemy1 entity with `Health.Current = 50`
- Client has a **separate** Enemy1 entity with `Health.Current = 100`
- Damage applied on server never reaches the client's copy

With `GhostAuthoringComponent`:
- Server's enemy entity becomes a **ghost**
- Client receives a **ghost replica** that auto-syncs `Health`, `CombatState`, etc.
- All `[GhostField]` marked component values replicate automatically

### Components That Replicate

These components are already marked for ghost sync:

| Component | Replicated Fields |
|-----------|------------------|
| `Health` | `Current`, `Max` |
| `CombatState` | `InCombat`, `LastCombatTime`, `InCombatFor` |
| `ShowHealthBarTag` | (presence only) |
| `HasAggroOn` | `TargetPlayer` |

### Troubleshooting Multiplayer

**Health bars show but damage doesn't sync:**
- Enemy prefab missing `GhostAuthoringComponent`
- SubScene not re-baked after adding ghost component

**Health bars don't appear on clients:**
- Check `ShowHealthBarTag` has `[GhostComponent]` attribute
- Verify `EnemyHealthBarBridgeSystem` is querying correctly

**Aggro indicator shows on wrong player:**
- Check `HasAggroOn.TargetPlayer` references the correct entity
- Verify `GhostOwnerIsLocal` query finds local player correctly
