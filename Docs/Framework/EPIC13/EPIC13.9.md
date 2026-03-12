# EPIC 13.9: Interaction System Unification

> **Status:** IMPLEMENTED ✓
> **Priority:** MEDIUM  
> **Dependencies:** EPIC 13.8 (Interaction System)  
> **Reference:** `Runtime/Survival/Resources/Systems/InteractionDetectionSystem.cs`

> [!IMPORTANT]
> **Goal:** Migrate legacy resource interaction system to use unified EPIC 13.8 framework.
> This eliminates duplicate detection systems and provides consistent interaction UX.

## Overview

The codebase currently has two interaction systems:
- **Legacy:** `DIG.Survival.Resources.InteractionDetectionSystem` - Resource gathering only
- **New:** `DIG.Interaction.InteractableDetectionSystem` - Generic doors, levers, etc.

This epic unifies them into a single framework.

---

## Sub-Tasks

### 13.9.1 Extend EPIC 13.8 for Resources
**Status:** NOT STARTED  
**Priority:** HIGH

Add resource-specific components to the unified framework.

#### Components to Add

```csharp
// In DIG.Interaction.InteractableComponents.cs

public struct ResourceInteractable : IComponentData
{
    public ResourceType ResourceType;
    public int CurrentAmount;
    public int MaxAmount;
    public float CollectionTime;
}

public struct CollectionProgress : IComponentData
{
    public float ElapsedTime;
    public float RequiredTime;
    public bool IsCollecting;
    public Entity CollectorEntity;
}
```

#### Files to Modify
- `Assets/Scripts/Interaction/Components/InteractableComponents.cs`

---

### 13.9.2 Create ResourceInteractionSystem
**Status:** NOT STARTED  
**Priority:** HIGH

Handle resource-specific collection logic using base `Interactable` component.

#### System Behavior

```
1. Query entities with Interactable + ResourceInteractable
2. When player starts timed interaction:
   - Create/update CollectionProgress
   - Play collection animation
3. When interaction completes:
   - Decrement ResourceInteractable.CurrentAmount
   - Add to player inventory
   - Check if depleted
```

#### Files to Create
- `Assets/Scripts/Interaction/Systems/ResourceInteractionSystem.cs`

---

### 13.9.3 Create ResourceAuthoring
**Status:** NOT STARTED  
**Priority:** MEDIUM

Authoring component for resource nodes.

#### Files to Create
- `Assets/Scripts/Interaction/Authoring/ResourceAuthoring.cs`

---

### 13.9.4 Delete Legacy Interaction Code
**Status:** NOT STARTED  
**Priority:** HIGH

Remove duplicate systems and components.

#### Files to Delete

| File | Reason |
|------|--------|
| `Runtime/Survival/Resources/Systems/InteractionDetectionSystem.cs` | Replaced by `DIG.Interaction.InteractableDetectionSystem` |

#### Components to Remove from ResourceComponents.cs

| Component | Replacement |
|-----------|-------------|
| `Interactable` | `DIG.Interaction.Interactable` |
| `InteractionTarget` | `DIG.Interaction.InteractAbility` |
| `InteractionDisplayState` | `DIG.Interaction.InteractionPrompt` |

---

### 13.9.5 Update References
**Status:** NOT STARTED  
**Priority:** MEDIUM

Update all code referencing legacy interaction components.

#### Files to Update
- Any prefabs using legacy `Interactable`
- UI systems reading `InteractionDisplayState`
- Resource collection systems

---

### 13.9.6 Integration Testing
**Status:** NOT STARTED  
**Priority:** HIGH

Verify all interaction types work correctly.

#### Test Matrix

| Scenario | Expected Behavior |
|----------|-------------------|
| Mine rock | Approach → "Hold E to Mine" → collect resources |
| Open door | Approach → "Press E to Open" → door swings |
| Lever → Door | Pull lever → linked door opens |
| Cancel collection | Release E → progress resets |
| Depleted resource | No interaction prompt shown |

---

## Files Summary

| Action | File |
|--------|------|
| MODIFY | `Interaction/Components/InteractableComponents.cs` |
| NEW | `Interaction/Systems/ResourceInteractionSystem.cs` |
| NEW | `Interaction/Authoring/ResourceAuthoring.cs` |
| DELETE | `Runtime/Survival/Resources/Systems/InteractionDetectionSystem.cs` |
| MODIFY | `Runtime/Survival/Resources/Components/ResourceComponents.cs` |

---

## Benefits

1. **Single detection system** - One Burst-compiled detection for all interactions
2. **Consistent UI** - Same prompt system for resources, doors, NPCs
3. **Extensible** - Easy to add new interaction types
4. **Less code duplication** - Shared components and logic
5. **Easier maintenance** - One system to debug/optimize

---

## Effort Estimate

| Phase | Effort |
|-------|--------|
| 13.9.1 Extend Components | 15 min |
| 13.9.2 ResourceInteractionSystem | 30 min |
| 13.9.3 ResourceAuthoring | 10 min |
| 13.9.4 Delete Legacy | 5 min |
| 13.9.5 Update References | 20 min |
| 13.9.6 Testing | 15 min |

**Total:** ~1.5 hours
