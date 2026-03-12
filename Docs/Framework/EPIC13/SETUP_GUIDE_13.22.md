# Setup Guide: EPIC 13.22 (Surface & Attribute Systems)

## Overview
This epic introduces two major systems for Opsive parity:
1.  **Unified Surface System**: Data-driven, material-based Audio/VFX (Footsteps, Impacts).
2.  **Generic Attribute System**: Buffer-based stats (Health, Stamina, Hunger) with auto-regen.

## 1. Unified Surface System Setup

### Data Assets (Surface Definitions)
Create `SurfaceDefinition` assets for each material type (e.g., Wood, Metal, Concrete).
1.  **Create Enum/Asset**: Right-click > Create > DIG > Surface System > Surface Definition.
2.  **Configure**:
    -   **Physic Material Names**: Add strings like "Wood", "Metal", "Default". matches the `PhysicMaterial.name` (exact match, case sensitive).
    -   **Audio**: Assign `ImpactSounds` and `FootstepSounds`.
    -   **VFX**: Assign `ImpactEffectPrefab` (particles) and `DecalMaterial` (bullet holes).

### Surface Resolve
The `SurfaceManager` singleton manages lookups.
1.  Ensure a GameObject with `SurfaceManager` exists in the scene (or let it auto-create).
2.  **Populate List**: Drag your created `SurfaceDefinition` assets into the `Surfaces` list on the Manager.
3.  **Set Default**: Assign a fallback surface (e.g., Concrete) to `DefaultSurface`.

## 2. Generic Attribute Setup

### Player Configuration
Attributes are configured on the `PlayerAuthoring` component Inspector.
1.  Locate **Attributes (Epic 13.22)** header.
2.  Add/Remove items in the list.
    -   **Name**: Unique ID (e.g., "Health", "Energy").
    -   **Start/Max Value**: Initial pool size (e.g., 100).
    -   **RegenRate**: Units per second (Negative for decay/poison).
    -   **RegenDelay**: Seconds to wait after modification before regen resumes.

### Code Access
To modify attributes from other systems:
```csharp
// Get Buffer
var attributes = SystemAPI.GetBuffer<AttributeData>(entity);

// Modify (Clean Wrapper)
AttributeHelper.ModifyAttribute(ref attributes, "Health", -10, SystemAPI.Time.ElapsedTime);

// Read
float currentHealth = AttributeHelper.GetAttributeValue(attributes, "Health");
```

## Troubleshooting
-   **No Impact Sound?** Check that the Collider has a `PhysicMaterial` assigned, and its name matches a `SurfaceDefinition` in the `SurfaceManager`.
-   **Attributes Not Regenerating?** Ensure `RegenRate` is non-zero, and `RegenDelay` hasn't paused it (check `LastChangeTime`).
