# EPIC 13.25 - Procedural Climbing IK System

**Status:** NOT STARTED  
**Priority:** HIGH  
**Dependencies:** EPIC 13.20 (Free Climb Core)

---

## Overview

This epic implements a **procedural inverse kinematics (IK) solver** specifically designed for humanoid climbing animations. Rather than relying on Unity's built-in IK hints (which provide weak directional influence), this system directly calculates and applies bone rotations to achieve anatomically correct limb poses.

### Why Procedural IK?

Unity's `SetIKHintPosition` only *suggests* bend directions - it cannot force proper articulation. For climbing, we need:
- Knees to bend **outward** (away from wall), not straight back
- Elbows to bend **outward and down**, not collapsing inward
- Feet to orient with soles **toward the wall**
- Hands to grip with palms **toward the wall**

### Core Algorithm: Two-Bone Analytical IK

For each limb (upper arm → forearm → hand, thigh → shin → foot), we use analytical two-bone IK - a closed-form mathematical solution that directly computes joint angles given target positions.

---

## Humanoid Anatomy Constraints

Proper climbing IK must respect anatomical limits. Violating these creates unnatural, broken-looking poses.

### Joint Reference Table

| Joint | Degrees of Freedom | Primary Motion | Range | Climbing Notes |
|-------|-------------------|----------------|-------|----------------|
| **Shoulder** | 3 DoF | Flexion/Extension | 0-180° | Arms reaching up/out for holds |
| | | Abduction/Adduction | 0-180° | Spreading arms wide |
| | | Internal/External Rotation | Combined ≤176° | Avoid hyperrotation |
| **Elbow** | 1 DoF (hinge) | Flexion | 0-150° | **Bend direction: outward/down** |
| | | Extension | 0° (no hyperextend) | Lock at full extension |
| **Hip** | 3 DoF (ball-socket) | Flexion | 0-120° | Lifting legs for high footholds |
| | | Abduction | 0-50° | Spreading legs for stemming |
| | | External Rotation | 0-45° | Turning feet outward |
| **Knee** | 1 DoF (hinge) | Flexion | 0-150° | **Bend direction: outward/forward** |
| | | Extension | 0° (slight hyperextend possible) | |

### Critical Insight: Hinge Joint Orientation

**Knees and elbows are HINGE joints** - they can only bend along ONE axis. The orientation of this axis determines where the joint "points" when bent:

```
ELBOW (Right Arm):
- Hinge axis runs roughly left-to-right through the elbow
- Bending rotates the forearm DOWN and INWARD naturally
- For climbing: we want forearm to drop DOWN while elbow points OUTWARD

KNEE (Right Leg):
- Hinge axis runs left-to-right through the knee
- Bending brings shin BACKWARD naturally (walking)
- For climbing: we want knee to point OUTWARD, not straight back
```

**The Problem:**
Standard IK puts the knee/elbow bend in the default plane. For climbing, we need to **pre-rotate the upper limb bone** so that when the hinge bends, it points in the desired direction (outward from wall).

---

## Algorithm Design

### Two-Bone Analytical IK

Given:
- `root` = shoulder or hip position (from skeleton)
- `target` = hand or foot target position (from FreeClimbIKController)
- `poleTarget` = hint position for bend direction
- `upperLength` = length of upper arm or thigh
- `lowerLength` = length of forearm or shin

Calculate:
1. **Distance to target**: `d = |target - root|`
2. **Check reachability**: If `d > upperLength + lowerLength`, target is unreachable → extend limb straight
3. **Calculate bend angle** using law of cosines:
   ```
   cosAngle = (upperLength² + lowerLength² - d²) / (2 * upperLength * lowerLength)
   bendAngle = acos(clamp(cosAngle, -1, 1))
   ```
4. **Calculate upper bone rotation**:
   - Point toward target
   - Twist so that bend plane aligns with pole target
5. **Calculate lower bone rotation**:
   - Apply bend angle as local rotation around hinge axis

### Pole Target (Bend Direction) Calculation

For climbing, the pole target determines WHERE the knee/elbow points when bent:

```csharp
// Knee pole target: forward from wall + above foot midpoint
Vector3 kneeHint = footPos + wallNormal * 0.5f + Vector3.up * 0.3f;

// Elbow pole target: outward from body + slightly down
Vector3 elbowHint = handPos + bodySide * 0.4f + Vector3.down * 0.2f;
```

The **pole target** lies in the plane where bending should occur. The limb calculates its twist rotation to align the bend plane toward this point.

---

## Implementation Phases

### Phase 1: Bone Discovery and Caching

**Goal:** Identify and cache Transform references for all IK-controlled bones.

```csharp
public class ClimbingProceduralIK : MonoBehaviour
{
    // Cached bone transforms
    private Transform _leftUpperArm, _leftForearm, _leftHand;
    private Transform _rightUpperArm, _rightForearm, _rightHand;
    private Transform _leftThigh, _leftShin, _leftFoot;
    private Transform _rightThigh, _rightShin, _rightFoot;
    private Transform _hips;
    
    // Bone lengths (measured at runtime)
    private float _upperArmLength, _forearmLength;
    private float _thighLength, _shinLength;
}
```

**Tasks:**
- [ ] Cache bone transforms from `Animator.GetBoneTransform(HumanBodyBones.X)`
- [ ] Calculate bone lengths on initialization
- [ ] Store initial local rotations for reset

---

### Phase 2: Two-Bone IK Solver Core

**Goal:** Implement the mathematical IK solver as a reusable utility.

```csharp
public static class TwoBoneIK
{
    /// <summary>
    /// Solve two-bone IK chain and apply rotations.
    /// </summary>
    /// <param name="root">Root bone transform (shoulder/hip)</param>
    /// <param name="mid">Middle joint transform (elbow/knee)</param>
    /// <param name="tip">End effector transform (hand/foot)</param>
    /// <param name="target">Target position for tip</param>
    /// <param name="poleTarget">Hint for bend direction</param>
    /// <returns>True if target was reachable</returns>
    public static bool Solve(
        Transform root, 
        Transform mid, 
        Transform tip,
        Vector3 target,
        Vector3 poleTarget)
    {
        // Implementation here
    }
}
```

**Tasks:**
- [ ] Implement distance-based reachability check
- [ ] Implement law of cosines angle calculation
- [ ] Implement root bone rotation (aim + twist)
- [ ] Implement mid bone rotation (bend angle on hinge axis)
- [ ] Add joint angle clamping for anatomical limits

---

### Phase 3: Climbing-Specific Pole Calculation

**Goal:** Calculate anatomically correct pole targets for climbing poses.

**Key Insight:** The pole target should be calculated based on:
1. **Wall normal** - bend AWAY from the wall
2. **Body orientation** - elbows out to the side, not behind
3. **Gravity** - knees typically above feet, elbows typically at/below shoulder level

```csharp
Vector3 CalculateKneePole(Vector3 hipPos, Vector3 footPos, Vector3 wallNormal, bool isLeft)
{
    // Midpoint between hip and foot
    Vector3 mid = Vector3.Lerp(hipPos, footPos, 0.5f);
    
    // Push forward (away from wall)
    mid += wallNormal * 0.5f;
    
    // Add slight outward offset for natural stance
    Vector3 side = Vector3.Cross(wallNormal, Vector3.up);
    mid += side * (isLeft ? -0.1f : 0.1f);
    
    return mid;
}

Vector3 CalculateElbowPole(Vector3 shoulderPos, Vector3 handPos, Vector3 wallNormal, bool isLeft)
{
    Vector3 mid = Vector3.Lerp(shoulderPos, handPos, 0.5f);
    
    // Push forward and down
    mid += wallNormal * 0.3f;
    mid += Vector3.down * 0.2f;
    
    // Outward offset
    Vector3 side = Vector3.Cross(wallNormal, Vector3.up);
    mid += side * (isLeft ? -0.15f : 0.15f);
    
    return mid;
}
```

**Tasks:**
- [ ] Implement `CalculateKneePole` with wall-aware positioning
- [ ] Implement `CalculateElbowPole` with natural arm pose
- [ ] Add debug visualization for pole targets
- [ ] Make pole offsets configurable in Inspector

---

### Phase 4: Foot and Hand Orientation

**Goal:** Calculate proper end-effector rotations for climbing grip.

**Foot Orientation:**
- Sole should face the wall (ball of foot against surface)
- Toes point roughly downward
- Ankle flexion matches foot placement angle

```csharp
Quaternion CalculateFootRotation(Vector3 footPos, Vector3 wallNormal, Vector3 surfaceUp)
{
    // Forward = into the wall
    Vector3 forward = -wallNormal;
    // Up = along the surface (roughly upward)
    Vector3 up = surfaceUp;
    
    return Quaternion.LookRotation(forward, up);
}
```

**Hand Orientation:**
- Palm faces the wall/hold
- Fingers wrap around (if applicable)
- Wrist maintains neutral position to avoid strain

```csharp
Quaternion CalculateHandRotation(Vector3 handPos, Vector3 wallNormal, bool isLeft)
{
    // Palm forward into wall
    Vector3 forward = -wallNormal;
    // Thumb side up (natural grip)
    Vector3 up = Vector3.up;
    
    Quaternion grip = Quaternion.LookRotation(forward, up);
    
    // Slight wrist rotation for natural feel
    float wristTilt = isLeft ? -15f : 15f;
    grip *= Quaternion.Euler(0, 0, wristTilt);
    
    return grip;
}
```

**Tasks:**
- [ ] Implement foot rotation based on wall normal
- [ ] Implement hand rotation with natural grip orientation
- [ ] Add wrist articulation for varied hold types
- [ ] Test with multiple wall orientations (vertical, overhang, slab)

---

### Phase 5: Integration with FreeClimbIKController

**Goal:** Replace `UnityAnimatorIKSolver` with `ProceduralIKSolver` in the solver abstraction.

**Integration Points:**
1. `ClimbAnimatorBridge` receives IK targets from `FreeClimbIKController`
2. `ProceduralIKSolver.ApplyClimbingIK()` is called from `LateUpdate` (NOT `OnAnimatorIK`)
3. Bone rotations are applied AFTER animation update but BEFORE rendering

```csharp
// In ClimbAnimatorBridge or separate component
void LateUpdate()
{
    if (!_isClimbing || _ikSolver == null) return;
    
    // Get bone transforms
    var skeleton = GetCachedSkeleton();
    
    // Solve each limb
    TwoBoneIK.Solve(
        skeleton.LeftUpperArm, skeleton.LeftForearm, skeleton.LeftHand,
        _targets.LeftHandPosition,
        CalculateElbowPole(skeleton.LeftUpperArm.position, _targets.LeftHandPosition, _wallNormal, true)
    );
    
    // Repeat for other limbs...
    
    // Apply end-effector rotations
    skeleton.LeftHand.rotation = CalculateHandRotation(_targets.LeftHandPosition, _wallNormal, true);
    // etc.
}
```

**Tasks:**
- [ ] Create `ProceduralClimbingIK` MonoBehaviour
- [ ] Cache skeleton references on enable
- [ ] Wire up to `ClimbAnimatorBridge` solver selection
- [ ] Ensure execution order (after Animator, before rendering)

---

### Phase 6: Hip and Spine Adjustment

**Goal:** Adjust body core to support limb positions naturally.

When all four limbs are attached to the wall, the hips and spine must:
- Position the pelvis at a comfortable distance from wall
- Rotate hips to align with leg angles
- Maintain slight forward lean for climbing posture

```csharp
void AdjustHipsAndSpine(ClimbIKTargets targets, Vector3 wallNormal)
{
    // Calculate average position of feet
    Vector3 feetCenter = (targets.LeftFootPosition + targets.RightFootPosition) * 0.5f;
    
    // Calculate desired hip position (above feet, offset from wall)
    Vector3 desiredHipPos = feetCenter + Vector3.up * _hipHeightAboveFeet + wallNormal * _hipWallOffset;
    
    // Apply as offset (coordinate with movement system)
    // ...
    
    // Orient hips to face mostly toward wall
    Vector3 hipForward = -wallNormal;
    Vector3 hipUp = Vector3.up;
    _hips.rotation = Quaternion.LookRotation(hipForward, hipUp);
}
```

**Tasks:**
- [ ] Implement hip positioning based on foot placement
- [ ] Add spine twist toward reaching arm
- [ ] Ensure body core follows limbs, not vice versa
- [ ] Coordinate with `FreeClimbMovementSystem` position updates

---

### Phase 7: Polish and Blending

**Goal:** Smooth blending and edge case handling.

**Blending In/Out:**
- When starting climb: lerp from animation pose to IK pose over 0.3s
- When releasing: lerp back to falling/walk animation

**Transition Handling:**
- Partial IK weights during transitions
- Override weight for each limb independently

**Edge Cases:**
- Unreachable targets: extend limb straight toward target
- Behind-body targets: clamp to reachable area
- Collision with self: prevent elbow/knee intersecting body

**Tasks:**
- [ ] Implement per-limb weight blending
- [ ] Add unreachable target handling
- [ ] Add self-collision prevention
- [ ] Test climbing start/stop transitions

---

## Anatomical Validation Checklist

Before considering this system complete, verify:

### Knee Behavior
- [ ] Knees point OUTWARD from wall, not backward
- [ ] Knee flexion stays within 0-150° range
- [ ] Both knees don't collapse inward simultaneously
- [ ] Feet flat on wall with toes pointing down

### Elbow Behavior
- [ ] Elbows point OUTWARD and SLIGHTLY DOWN
- [ ] Elbow flexion stays within 0-150° range
- [ ] Arms don't hyperextend when reaching
- [ ] Hands grip with palms toward wall

### Hip Behavior
- [ ] Hips stay at comfortable distance from wall
- [ ] Hip rotation follows leg spread naturally
- [ ] No "twisted pelvis" appearance
- [ ] Climbing looks stable, not dangling

### Shoulder Behavior
- [ ] Shoulders don't exceed natural rotation ranges
- [ ] Arms can reach up, out, and slightly behind
- [ ] No "dislocated shoulder" appearance
- [ ] Weight distribution looks believable

---

## Files to Create/Modify

### New Files
| File | Description |
|------|-------------|
| `TwoBoneIK.cs` | Static utility class for two-bone analytical IK solver |
| `ProceduralClimbingIK.cs` | MonoBehaviour applying IK to climbing skeleton |
| `ClimbingSkeleton.cs` | Bone reference cache and length measurements |
| `JointConstraints.cs` | Anatomical angle limits for each joint |

### Modified Files
| File | Changes |
|------|---------|
| `ClimbAnimatorBridge.cs` | Add `ProceduralClimbingIK` execution path |
| `ClimbIKSolvers.cs` | Implement `ProceduralIKSolver` properly |
| `FreeClimbIKController.cs` | Pass additional context (body orientation, etc.) |

---

## Testing Scenarios

1. **Vertical Wall** - Standard climbing, knees/elbows should point outward
2. **Overhang** - Arms extended up, legs hanging, proper shoulder rotation
3. **Slab (low angle)** - More bent knees, arms less extended
4. **Corner (inside)** - Legs spread wide for stemming
5. **Corner (outside)** - Rotation while maintaining holds
6. **Horizontal Traverse** - Lateral movement, weight shifts
7. **Mantle/Top-out** - Transition from vertical to horizontal

---

## Performance Considerations

- **CPU Cost:** Two-bone IK is O(1) per limb - very fast
- **Total per frame:** ~4 IK solves + pole calculations + rotation blends
- **Target:** < 0.5ms total impact
- **Optimization:** Cache bone lengths, pre-compute pole offsets

---

## References

- FABRIK Algorithm: "FABRIK: A fast, iterative solver for the Inverse Kinematics problem" (Aristidou & Lasenby, 2011)
- Unity Animation Rigging: Two Bone IK constraint documentation
- Humanoid Joint Ranges: Medical anatomy references for ROM values
