# EPIC 14.22 - Fix Climbing Edge Cases: Hang→Vault & Horizontal Loop

## Overview

This document addresses two critical climbing system bugs:
1. Player cannot vault after reaching a "free hang" state at ledge top
2. Player hits an "invisible wall" when attempting to climb horizontally around objects (360° loop)

It also includes fixes for **Corner Ping-Pong Effect** and **Diagnostic Logging Control**.

---

## Tasks

- [x] Fix hang→vault: Reduce upward input instead of blocking in `FreeClimbMovementSystem.cs`
- [x] Fix hang→vault: Add grip offset compensation in `FreeClimbLedgeSystem.cs`
- [x] Fix horizontal loop: Remove 90° rotation rejection threshold
- [x] Fix horizontal loop: Widen outer corner acceptance range
- [x] **Fix corner ping-pong**: Implement corner transition cooldown and derive surface normal from player rotation.
- [x] **Fix corner ping-pong**: Update `TryResolveSurface` to accept parameterized surface normal.
- [x] **Logging Control**: Add toggleable logging to `PlayerIKBridge`, `FreeClimbDetectionSystem`, `FreeClimbMountSystem`, `FreeClimbDiagnosticSystem`.
- [x] **Logging Control**: Silence excessively verbose logs in `FreeClimbMovementSystem`.
- [ ] Test: Hang→Vault at ledge top
- [ ] Test: 360° horizontal loop around pillar
- [ ] Test: 90° corner traversal (verify smooth transition without ping-pong)
- [ ] Test: Regression (normal climb, wall jump, dismount)

---

## Issue 1: Cannot Hang Then Vault at Ledge Top

### Observed Behavior
When the player reaches the top of a climbable surface and enters "free hang" state (dangling with no foot support), they cannot vault over the ledge by pressing W or Jump.

### Root Cause Analysis

The system has **conflicting logic** between movement and vault triggering:

#### 1. Movement Blocks Upward Input (`FreeClimbMovementSystem.cs`, line 86-88)
```csharp
// CRITICAL FIX: Block vertical movement if hanging
if (climb.IsFreeHanging && inputY > 0)
{
    inputY = 0;  // ← BLOCKS W-key movement entirely
}
```
This was added to prevent "slipping out" of hang state, but it has the side effect of preventing the player from positioning themselves for vault.

#### 2. Vault Trigger Logic (`FreeClimbLedgeSystem.cs`, line 93)
```csharp
bool isValidTrigger = playerInput.Jump.IsSet || (climb.IsFreeHanging && playerInput.Vertical > 0.5f);
```
The W-key trigger path (`IsFreeHanging && Vertical > 0.5f`) is correct, but...

#### 3. Raycast Uses Current Position (`FreeClimbLedgeSystem.cs`, line 106)
```csharp
float3 effectiveGripPosition = actualGripPosition.y > climb.GripWorldPosition.y 
    ? actualGripPosition 
    : climb.GripWorldPosition;

bool canClimb = CanClimbUp(entity, effectiveGripPosition, ...);
```
Uses `effectiveGripPosition` which might be too low if the player can't move upward.

#### 4. Distance Check Rejection (`FreeClimbLedgeSystem.cs`, line 114-117)
```csharp
if (distanceToLedge > 3.3f)
{
    return;  // ← Rejects vault if ledge is too far above
}
```

### Failure Chain
```
Player enters FreeHang at ledge top
    ↓
Player presses W to vault
    ↓
FreeClimbMovementSystem BLOCKS upward movement (inputY = 0)
    ↓
FreeClimbLedgeSystem receives W-key input → isValidTrigger = true
    ↓
CanClimbUp raycasts from current position (too low)
    ↓
Ledge detection may fail OR distanceToLedge check rejects (> 3.3m)
    ↓
No vault occurs
```

### Fix

**Approach:** Allow minimal upward "vault intent" movement when hanging instead of blocking entirely.

#### File: `Assets/Scripts/Player/Systems/FreeClimbMovementSystem.cs`
```diff
- // CRITICAL FIX: Block vertical movement if hanging
- if (climb.IsFreeHanging && inputY > 0)
- {
-     inputY = 0;
- }
+ // Allow reduced upward movement when hanging to enable vault positioning
+ if (climb.IsFreeHanging && inputY > 0)
+ {
+     inputY *= 0.3f;  // Reduce to 30% instead of blocking entirely
+ }
```

#### File: `Assets/Scripts/Player/Systems/FreeClimbLedgeSystem.cs`
```diff
  // Use character's actual world position for vault detection
  float3 actualGripPosition = lt.Position + new float3(0, HAND_HEIGHT_OFFSET, 0);
  
- // Only use actual position if it's higher than the stored grip
- float3 effectiveGripPosition = actualGripPosition.y > climb.GripWorldPosition.y 
-     ? actualGripPosition 
-     : climb.GripWorldPosition;
+ // When hanging, use a higher offset to compensate for blocked movement
+ float3 effectiveGripPosition;
+ if (climb.IsFreeHanging)
+ {
+     effectiveGripPosition = lt.Position + new float3(0, HAND_HEIGHT_OFFSET + 0.3f, 0);
+ }
+ else
+ {
+     effectiveGripPosition = actualGripPosition.y > climb.GripWorldPosition.y 
+         ? actualGripPosition 
+         : climb.GripWorldPosition;
+ }
```

---

## Issue 2: Horizontal Loop "Invisible Wall" & Visual Glitch

### Observed Behavior
When climbing horizontally around an object (e.g., a pillar), the player hits an "invisible wall" partway around and weird visual artifacts occur. They cannot complete a full 360° loop.

### Root Cause Analysis

#### 1. Rotation Rejection Threshold (`FreeClimbMovementSystem.cs`, lines 273-297)
```csharp
const float REJECT_ROTATION_THRESHOLD = 90f;

if (rotationChange > REJECT_ROTATION_THRESHOLD)
{
    // CRITICAL: Reject rotations > 90° completely
    // ← THIS PREVENTS CORNER WRAPPING
}
else if (rotationChange > MAX_ROTATION_PER_FRAME)
{
    float t = MAX_ROTATION_PER_FRAME / rotationChange;
    lt.Rotation = math.slerp(lt.Rotation, targetRot, t);
}
```

**Issue:** When rounding a corner, the surface normal changes gradually. At ~90° corners (or curved surfaces with cumulative rotation), the system rejects the new facing direction, leaving the player stuck facing the wrong way while the grip updates.

#### 2. Narrow Corner Acceptance Range (`FreeClimbMovementSystem.cs`, line 361-374)
```csharp
float turnAngle = math.dot(hit.SurfaceNormal, surfaceNormal);
if (turnAngle > -0.3f && turnAngle < 0.5f) return true;
```
- `turnAngle > -0.3f` means the new surface can only be ~107° rotated from current
- `turnAngle < 0.5f` means it must be at least ~60° different
- This narrow band **rejects many valid corners**

### Failure Chain
```
Player climbs horizontally around pillar
    ↓
Surface normal changes gradually (5-10° per grip update)
    ↓
After multiple updates, cumulative rotation approaches 90°
    ↓
Next movement triggers > 90° rotation change from original facing
    ↓
REJECT_ROTATION_THRESHOLD blocks the rotation change
    ↓
Player body faces wrong direction, grip updates, animation breaks
    ↓
"Invisible wall" effect as character can't properly navigate
```

### Fix

**Approach 1:** Remove the hard rejection threshold and use gradual clamping only.

#### File: `Assets/Scripts/Player/Systems/FreeClimbMovementSystem.cs`
```diff
- const float REJECT_ROTATION_THRESHOLD = 90f;
  const float MAX_ROTATION_PER_FRAME = 30f;

  float3 currentForward = math.forward(lt.Rotation);
  float3 targetForward = math.forward(targetRot);
  float rotationDot = math.dot(currentForward, targetForward);
  float rotationChange = math.degrees(math.acos(math.clamp(rotationDot, -1f, 1f)));

- if (rotationChange > REJECT_ROTATION_THRESHOLD)
- {
-     // CRITICAL: Reject rotations > 90° completely
- }
- else if (rotationChange > MAX_ROTATION_PER_FRAME)
+ if (rotationChange > MAX_ROTATION_PER_FRAME)
  {
-     // Medium rotation (30-90°) - clamp using slerp
      float t = MAX_ROTATION_PER_FRAME / rotationChange;
      lt.Rotation = math.slerp(lt.Rotation, targetRot, t);
  }
  else
  {
-     // Small rotation (<30°) - apply directly
      lt.Rotation = targetRot;
  }
```

**Approach 2:** Perpendicular raycast for 90° corners (THE ACTUAL FIX).

The original `TryOuterCorner` cast backwards along movement direction. But for 90° corners on rectangular prisms, the adjacent face is **perpendicular** - the raycast literally can't see it:

```
     Current Face (normal: -Z)
     ┌─────────┐
     │    ●────┼──→ moveDirection (X)
     │         │
     └─────────┤
               │ Adjacent Face (normal: -X)
               │ ← Old raycast shot backwards, missing this entirely!
```

**Fix:** Cast perpendicular to movement direction (where the adjacent face actually is):

```csharp
// Calculate perpendicular direction (90° to movement)
float3 perpDir = math.cross(worldUp, moveDirection);

// Cast perpendicular to find adjacent 90° face
if (TryCast(cornerOrigin, -perpDir, castDepth, filter, entity, out hit))
{
    if (normalDot > -0.7f && normalDot < 0.7f) return true;
}

// Also try opposite perpendicular direction
if (TryCast(cornerOrigin, perpDir, castDepth, filter, entity, out hit))
{
    if (normalDot > -0.7f && normalDot < 0.7f) return true;
}

// Keep original backwards cast for inside corners
if (TryCast(backOrigin, -moveDirection, 0.8f, filter, entity, out hit))
{
    if (normalDot > -0.7f && normalDot < 0.7f) return true;
}
```

---

## Issue 3: Corner "Ping-Pong" Effect

### Observed Behavior
When negotiating an outer corner, the player would often "vibrate" or ping-pong back and forth between the two surfaces.

### Root Cause
1. **NetCode Rollback:** `GripWorldNormal` is a `[GhostField]`. Upon successful corner transition client-side, the player attaches to the new wall. However, server reconciliation or prediction rollback would revert `GripWorldNormal` to the old wall's normal for a few frames.
2. **Dependent Logic:** The movement logic relied on `GripWorldNormal` to calculate movement direction. When it reverted, the player would "move" relative to the old wall, pushing them back off the new wall or into invalid space, triggering another corner detection, causing a loop.

### Fix
1. **Corner Transition Cooldown:** Added a 0.15s cooldown (`CornerTransitionTime`) during which `TryOuterCorner` cannot trigger again.
2. **Derived Surface Normal:** During this cooldown, the system **ignores** `GripWorldNormal` and instead derives the surface normal from the player's rotation (`-playerForward`). Since player rotation is snapped immediately on corner transition and is client-authoritative for prediction, this remains stable even if the ghost field rolls back.

```csharp
// Inside FreeClimbMovementJob
bool inCornerCooldown = (Time - climb.CornerTransitionTime) < 0.15f;
if (inCornerCooldown)
{
    // Use derived normal from rotation to survive rollback
    surfaceNormal = -playerForward; 
}
else
{
    surfaceNormal = climb.GripWorldNormal;
}
```

---

## Files Modified

| File | Changes |
|------|---------|
| `Assets/Scripts/Player/Systems/FreeClimbMovementSystem.cs` | Fixes for hang vault, corner detection, ping-pong cooldown, derived normal logic. |
| `Assets/Scripts/Player/Systems/FreeClimbLedgeSystem.cs` | Add grip offset compensation when `IsFreeHanging`. |
| `Assets/Scripts/Player/Systems/FreeClimbDetectionSystem.cs` | Added `EnableLogging` toggle. |
| `Assets/Scripts/Player/Systems/FreeClimbMountSystem.cs` | Added `EnableLogging` toggle. |
| `Assets/Scripts/Player/View/PlayerIKBridge.LookAtIK.cs` | Added `EnableLogging` toggle. |
| `Assets/Scripts/Player/Systems/FreeClimbDiagnosticSystem.cs` | Added `EnableLogging` toggle. |

---

## Verification Plan

### Manual Testing (Required)

1. **Hang→Vault Test:**
   - Climb to the top of a ~3m tall wall until player enters hanging state
   - Press W or Jump while hanging at ledge
   - **Expected:** Player should vault over the ledge

2. **Horizontal Loop Test:**
   - Create/use a climbable pillar (~2m diameter)
   - Climb onto one side, then hold A or D to traverse horizontally
   - **Expected:** Player should complete full 360° loop without getting stuck

3. **Corner Wrap Test:**
   - Use an L-shaped corner (90°)
   - Climb from one face, traverse around the corner
   - **Expected:** Smooth transition without visual glitches

4. **Regression Tests:**
   - Ensure normal climbing (up/down) still works
   - Ensure wall jump still functions
   - Ensure dismount (drop/jump-off) still functions
