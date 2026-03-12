# EPIC 14.7 - Targeting System Setup Guide

## Overview

The Targeting System provides a unified interface for different aiming modes (camera raycast, cursor aim, lock-on, etc.) that can be swapped via configuration.

---

## Quick Setup

### 1. Add TargetDataAuthoring to Player

1. Open your **Player Ghost Prefab** (e.g., `Atlas_Server.prefab`).
2. Add Component → search for `TargetDataAuthoring`.
3. Set `Initial Mode` to your desired targeting mode.

> [!IMPORTANT]
> Add `TargetDataAuthoring` to the **ghost prefab** (the one with `GhostAuthoringComponent`), not the client visual prefab.

---

### 2. Create Targeting Config (Optional)

1. **Right-click** in Project → `Create > DIG > Targeting > Targeting Config`.
2. Save to `Assets/Resources/Targeting/` folder.
3. Configure:
   - `TargetingMode`: CameraRaycast, CursorAim, AutoTarget, LockOn, ClickSelect
   - `MaxTargetRange`: How far to detect targets
   - `ValidTargetLayers`: LayerMask for targetable objects
   - `AimAssistStrength`: 0 = none, 1 = full snap

**Or use presets programmatically:**
```csharp
var config = TargetingConfig.CreateDIGPreset();   // TPS shooter
var config = TargetingConfig.CreateARPGPreset();  // Isometric ARPG
```

---

## Targeting Modes

| Mode | Best For | Description |
|------|----------|-------------|
| CameraRaycast | TPS/FPS | Aim at screen center (crosshair) |
| CursorAim | ARPG/Isometric | Aim toward mouse cursor |
| AutoTarget | ARPG | Auto-lock nearest enemy |
| LockOn | Souls-like | Tab to lock, cycle targets |
| ClickSelect | Diablo-style | Click enemy to select |

---

## Make Enemies Targetable

Add **EntityLink** component to enemy prefabs so targeting can detect them:

1. Open enemy prefab.
2. Add `EntityLink` component (found in `DIG.Targeting.Implementations`).
3. Set the `Entity` field at runtime when the enemy spawns.

> [!NOTE]
> Without `EntityLink`, targeting implementations cannot identify which ECS entity the collider belongs to.

---

## UI Setup (Optional)

### Target Highlight
1. Create Canvas → Image.
2. Add `TargetHighlightUI` component.
3. Assign sprites for soft/locked target icons.
4. Initialize with player entity at runtime.

### Lock-On Reticle (Souls-like)
1. Create Canvas → Image.
2. Add `LockOnReticleUI` component.
3. Assign rotating reticle sprite.

### Cursor Aim Indicator (ARPG)
1. Create world-space sprite/decal.
2. Add `CursorAimIndicator` component.
3. Assign ground offset.

### VFX Variants (Premium)
Use VFX Graph for animated particles and glow:
- `VFXTargetHighlight` — Attach VisualEffect, set property names
- `VFXCursorIndicator` — Attach VisualEffect for ground circle
- `DecalCursorIndicator` — URP Decal Projector for terrain-conform circle

All implement `ITargetIndicator` interface for swappable visuals.

---

## Conditional Theming (Advanced)

Indicators can adapt based on target faction, damage type, player class, etc.

### 1. Create Theme Profile
1. **Right-click** in Project → `Create > DIG > Targeting > Theme Profile`
2. Configure faction colors (enemy=red, ally=green)
3. Configure damage type colors (fire=orange, ice=blue)
4. Set category colors (boss=magenta, elite=yellow)

### 2. Add to Player Prefab
1. Open Player Ghost Prefab
2. Add `ThemeProfileAuthoring` component
3. Assign your `ThemeProfile` asset

### 3. Use DefaultThemeResolver
```csharp
var resolver = GetComponent<DefaultThemeResolver>();
resolver.Initialize(entityManager, playerEntity);

// Build context from targeting state
var context = resolver.BuildContext(targetData);

// Get themed color
Color color = resolver.ResolveColor(context);
float size = resolver.ResolveSizeMultiplier(context);
```

---

## Integrating 3rd-Party UI Assets

To use any 3rd-party targeting UI with our system:

### 1. Create an Adapter
Implement `IThemedTargetIndicator` and wrap the 3rd-party API:

```csharp
public class MyAssetAdapter : MonoBehaviour, IThemedTargetIndicator
{
    [SerializeField] private ThirdPartyUI _assetUI;
    
    public void UpdateIndicatorThemed(IndicatorThemeContext context)
    {
        _assetUI.SetPosition(context.TargetPosition);
        _assetUI.SetColor(GetColorFromContext(context));
    }
    // ...implement other interface methods
}
```

### 2. Use TargetIndicatorBridge
1. Add `TargetIndicatorBridge` to your UI Canvas
2. Assign your adapter to the `Indicator Components` list
3. Initialize with player entity at runtime

See `Assets/Scripts/Targeting/UI/Examples/ThirdPartyUIAdapter.cs` for a full template.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `TargetDataAuthoring` not found | Ensure scripts compiled (no errors in Console) |
| `TargetData` missing on entity | Add `TargetDataAuthoring` to ghost prefab |
| Targeting not finding enemies | Add `EntityLink` to enemy prefabs |
| UI not showing target | Call `Initialize(EntityManager, playerEntity)` on UI scripts |
