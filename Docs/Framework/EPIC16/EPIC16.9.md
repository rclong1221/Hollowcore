# EPIC 16.9: Knockback & Physics Force System

**Status:** Planning
**Priority:** High (Core Combat Feel)
**Dependencies:**
- `PlayerCollisionState` (EPIC 7.3 -- Existing: stagger/knockdown state)
- `ExternalForceSystem` (EPIC 13.1 -- Existing: player force pipeline)
- `CombatResolutionSystem` (EPIC 15.29 -- Existing: modifier processing)
- `ProjectileExplosionSystem` (EPIC 15.10 -- Existing: explosion triggers)
- `ExplosiveStats.PhysicsForce` (EPIC 2.5 -- Existing: 500-2000 Newton values, unused)
- `EnemySeparationSystem` (EPIC 15.23 -- Existing: kinematic enemy position writes)
- `SurfaceDatabaseBlob` (EPIC 15.24 -- Existing: surface ID system)
- `WeaponModifier` (EPIC 15.29 -- Existing: `ModifierType.Knockback` + `Force` field)
- `CharacterControllerSystem` (Existing: player kinematic movement)
- `Unity.NetCode` (Prediction/rollback)
- `Unity.Physics` (Collision queries, raycasts)

**Feature:** A unified, genre-agnostic knockback framework that provides satisfying, predictable physics displacement for both players (kinematic character controller) and enemies (kinematic AI bodies). Any system in the game can request knockback by creating a `KnockbackRequest` entity -- explosions, melee hits, abilities, environmental hazards, boss slams -- and the knockback pipeline handles resistance checks, easing curves, interrupt integration, surface friction, and predicted movement for responsive network feel.

**Supersedes:** The orphaned `ModifierType.Knockback` case in `CombatResolutionSystem` (line 169: `// Knockback requires kinematic body displacement system (future)`) and the unused `ExplosiveStats.PhysicsForce` field (500-2000N stored but never applied as knockback).

---

## Overview

### Problem

DIG has multiple systems that **should** apply knockback but currently cannot:

| Source System | What It Has | What's Missing |
|---------------|------------|----------------|
| `CombatResolutionSystem` | `ModifierType.Knockback` case + `WeaponModifier.Force` field | No-op comment: "requires kinematic body displacement system (future)" |
| `ExplosiveStats` | `PhysicsForce` (500-2000 Newtons) per explosive type | No system reads this field for entity displacement |
| `ProjectileExplosionSystem` | Detonation position + blast radius | No radial knockback on entities in radius |
| `ModifierExplosionRequest` | `KnockbackForce` field | `ModifierExplosionSystem` applies damage but ignores knockback |
| `PlayerCollisionResponseSystem` | Stagger velocity + knockdown state | Only player-vs-player collisions; no ability/explosion knockback |
| `TackleSystem` | Cone detection + knockdown application | Hardcoded to player collision state; enemies cannot be tackled back |
| `ExternalForceSystem` | `AddExternalForceRequest` + force buffer + decay | Players only; no enemy support; no easing curves; linear decay only |

The result: explosions deal damage but don't push anything. Melee weapons with knockback modifiers proc the flag but produce zero displacement. Enemies cannot be knocked back at all. The game feels flat -- hits lack physicality.

### Solution

A **single knockback pipeline** that any system can feed into:

```
[Any Source System]
       |
       v  Creates KnockbackRequest entity
[KnockbackResolveSystem] -- PredictedFixedStepSimulation
       |
       |  Reads KnockbackResistance on target
       |  Checks immunity windows
       |  Applies SuperArmor threshold
       |  Computes final velocity from force + direction + type
       |  Writes KnockbackState on target
       |  Optionally fires InterruptRequest (EPIC 16.1)
       |  Destroys request entity
       v
[KnockbackMovementSystem] -- PredictedFixedStepSimulation, after movement
       |
       |  Reads KnockbackState.Velocity
       |  For PLAYERS: writes ExternalForceRequest -> ExternalForceSystem -> CharacterController
       |  For ENEMIES: writes LocalTransform.Position directly (kinematic pattern)
       |  Applies easing curve to velocity over duration
       |  Optionally reads SurfaceDatabaseBlob for friction modifier
       v
[KnockbackCleanupSystem] -- PredictedFixedStepSimulation
       |  Clears expired KnockbackState
       |  Resets immunity timers
```

### Principles

1. **One request, one pipeline** -- Every knockback source creates the same `KnockbackRequest` entity. No special-casing per source.
2. **Works on everything** -- Players (CharacterController velocity), enemies (kinematic LocalTransform writes), destructibles (future), ragdolls (future).
3. **Predicted** -- Client predicts knockback for instant feel. Server validates. Rollback-safe via `[GhostField]` on `KnockbackState`.
4. **Modular** -- Remove `KnockbackState` from an entity's archetype and it simply ignores all knockback. Zero displacement, zero cost.
5. **Burst-compatible** -- All hot-path systems are `[BurstCompile]` with `ISystem`. Managed bridges only for debug visualization.
6. **Extend, don't replace** -- Existing `PlayerCollisionState` stagger/knockdown is untouched. Knockback is a separate, parallel displacement channel. They can coexist (stagger animation + knockback slide).

---

## The 4 Knockback Types

| Type | Direction | Vertical | Use Case | Feel Reference |
|------|-----------|----------|----------|----------------|
| **Push** | Horizontal away from source | None | Explosion blast, shield bash, melee knockback | FPS: COD explosive, Destiny stomp |
| **Launch** | Horizontal + upward | Yes (configurable arc) | Boss slam, uppercut, geyser trap | ARPG: Diablo boss ground slam |
| **Pull** | Toward source | None | Vortex grenade, grapple hook, black hole ability | Overwatch: Orisa halt, Destiny tractor cannon |
| **Stagger** | Small push from hit direction | None | Heavy melee hit, sniper impact, parry recoil | Fighting game: hitstun, Dark Souls stagger |

---

## Phase 1: Core Knockback Pipeline

### 1.1 KnockbackType Enum

```csharp
namespace DIG.Combat.Knockback
{
    /// <summary>
    /// Determines knockback direction calculation and vertical behavior.
    /// </summary>
    public enum KnockbackType : byte
    {
        /// <summary>Horizontal away from source. Standard explosion/hit knockback.</summary>
        Push = 0,

        /// <summary>Horizontal + upward arc. Boss slams, uppercuts, geysers.</summary>
        Launch = 1,

        /// <summary>Toward source. Vortex grenades, grapple pulls, gravity wells.</summary>
        Pull = 2,

        /// <summary>Brief freeze + small push from hit direction. Heavy hit stagger.</summary>
        Stagger = 3
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackType.cs` (NEW)

### 1.2 KnockbackEasing Enum

```csharp
namespace DIG.Combat.Knockback
{
    /// <summary>
    /// Easing curve for knockback velocity decay over duration.
    /// Controls how the knockback "feels" -- sharp burst vs gradual slide.
    /// </summary>
    public enum KnockbackEasing : byte
    {
        /// <summary>Constant deceleration. Functional but feels mechanical.</summary>
        Linear = 0,

        /// <summary>Fast start, gradual stop. DEFAULT. Feels responsive and natural.</summary>
        EaseOut = 1,

        /// <summary>Slides past target, bounces back slightly. Cartoonish, fun.</summary>
        Bounce = 2,

        /// <summary>Near-instant burst, very fast decay. Snappy hit reactions.</summary>
        Sharp = 3
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackEasing.cs` (NEW)

### 1.3 KnockbackFalloff Enum

```csharp
namespace DIG.Combat.Knockback
{
    /// <summary>
    /// Distance-based force falloff for area knockback (explosions, shockwaves).
    /// Single-target knockback (melee hit) uses None.
    /// </summary>
    public enum KnockbackFalloff : byte
    {
        /// <summary>No falloff. Full force at any distance. For single-target knockback.</summary>
        None = 0,

        /// <summary>Force = Base * (1 - distance/radius). Gentle falloff.</summary>
        Linear = 1,

        /// <summary>Force = Base * (1 - (distance/radius)^2). Sharp close, gentle far. DEFAULT for explosions.</summary>
        Quadratic = 2,

        /// <summary>Force = Base * (1 - (distance/radius)^3). Very sharp close, almost zero at edge.</summary>
        Cubic = 3
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackFalloff.cs` (NEW)

### 1.4 KnockbackRequest IComponentData (Transient Entity)

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Request entity for knockback application.
    /// Any system creates this as a standalone entity. KnockbackResolveSystem consumes and destroys it.
    ///
    /// Usage:
    ///   var reqEntity = ecb.CreateEntity();
    ///   ecb.AddComponent(reqEntity, new KnockbackRequest { ... });
    ///
    /// Lifetime: 1 frame. Created by source systems, consumed by KnockbackResolveSystem.
    /// </summary>
    public struct KnockbackRequest : IComponentData
    {
        /// <summary>Entity to apply knockback to. Must have KnockbackState component.</summary>
        public Entity TargetEntity;

        /// <summary>Entity that caused the knockback (for immunity tracking, kill credit). Entity.Null if environmental.</summary>
        public Entity SourceEntity;

        /// <summary>
        /// Normalized direction of knockback force in world space.
        /// For Push/Launch: away from source toward target.
        /// For Pull: from target toward source.
        /// For Stagger: hit direction (weapon impact vector).
        /// System normalizes this if not already unit length.
        /// </summary>
        public float3 Direction;

        /// <summary>
        /// Knockback force magnitude in Newtons.
        /// Reference values: 200 = light shove, 500 = grenade, 1000 = heavy melee, 2000 = breaching charge.
        /// Scaled by target's KnockbackResistance before application.
        /// </summary>
        public float Force;

        /// <summary>Knockback behavior type. Determines direction calculation and vertical component.</summary>
        public KnockbackType Type;

        /// <summary>Distance-based falloff. Only relevant for area knockback (explosions). Single-target uses None.</summary>
        public KnockbackFalloff Falloff;

        /// <summary>
        /// Distance from source to target at time of request.
        /// Used with Falloff to compute effective force.
        /// Set to 0 for single-target (melee) knockback.
        /// </summary>
        public float Distance;

        /// <summary>
        /// Maximum radius for falloff calculation.
        /// Force reaches zero at this distance (for Linear/Quadratic/Cubic falloff).
        /// Ignored when Falloff = None.
        /// </summary>
        public float MaxRadius;

        /// <summary>
        /// Override easing curve for this knockback. Default (EaseOut) if not specified.
        /// </summary>
        public KnockbackEasing Easing;

        /// <summary>
        /// Override duration in seconds. 0 = use default from KnockbackConfig.
        /// Stagger type uses shorter duration (0.15-0.3s). Push/Launch uses longer (0.3-0.8s).
        /// </summary>
        public float DurationOverride;

        /// <summary>
        /// For Launch type: vertical force multiplier (0-1).
        /// 0 = no vertical, 0.5 = 45-degree arc, 1.0 = straight up.
        /// Ignored for Push/Pull/Stagger.
        /// </summary>
        public float LaunchVerticalRatio;

        /// <summary>
        /// If true, this knockback ignores SuperArmor threshold (guaranteed knockback).
        /// Used for boss abilities, environmental hazards, forced displacement.
        /// </summary>
        public bool IgnoreSuperArmor;

        /// <summary>
        /// If true, this knockback triggers an InterruptRequest on the target (EPIC 16.1).
        /// Used for heavy hits that should cancel ability casts.
        /// </summary>
        public bool TriggersInterrupt;
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackRequest.cs` (NEW)

### 1.5 KnockbackState IComponentData (On Target Entity)

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Runtime knockback state on any knockback-capable entity.
    /// Written by KnockbackResolveSystem, read by KnockbackMovementSystem.
    /// Ghost-replicated for prediction/rollback.
    ///
    /// Entities WITHOUT this component simply ignore all KnockbackRequests targeting them.
    /// This is the modularity mechanism -- remove this component to make an entity knockback-immune.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct KnockbackState : IComponentData
    {
        /// <summary>True while knockback velocity is being applied.</summary>
        [GhostField]
        public bool IsActive;

        /// <summary>
        /// Current knockback velocity in world space (m/s).
        /// Decays over Duration according to Easing curve.
        /// KnockbackMovementSystem applies this to position each tick.
        /// </summary>
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public float3 Velocity;

        /// <summary>Initial velocity magnitude at knockback start. Used for easing curve evaluation.</summary>
        [GhostField(Quantization = 100)]
        public float InitialSpeed;

        /// <summary>Total knockback duration in seconds.</summary>
        [GhostField(Quantization = 100)]
        public float Duration;

        /// <summary>Time elapsed since knockback started.</summary>
        [GhostField(Quantization = 100)]
        public float Elapsed;

        /// <summary>Easing curve for velocity decay.</summary>
        [GhostField]
        public KnockbackEasing Easing;

        /// <summary>Knockback type that produced this state (for animation selection).</summary>
        [GhostField]
        public KnockbackType Type;

        /// <summary>
        /// If true, knockback only applies while entity is grounded.
        /// Prevents double-knockback in air (already launched).
        /// Set automatically for Launch type after initial impulse.
        /// </summary>
        [GhostField]
        public bool GroundedOnly;

        /// <summary>
        /// Source entity that caused this knockback (for kill credit, damage attribution).
        /// </summary>
        public Entity SourceEntity;

        /// <summary>Normalized progress (0-1). Computed as Elapsed / Duration.</summary>
        public float Progress => Duration > 0f ? math.saturate(Elapsed / Duration) : 1f;

        /// <summary>True when knockback has completed its duration.</summary>
        public bool IsExpired => Elapsed >= Duration;
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackState.cs` (NEW)

### 1.6 KnockbackResistance IComponentData (On Target Entity)

```csharp
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Knockback resistance and immunity configuration.
    /// Optional component -- entities without it receive full knockback (zero resistance).
    ///
    /// Design reference values:
    ///   Player (default):    Resistance=0.0, SuperArmor=0, ImmunityDuration=0.2
    ///   Heavy enemy:         Resistance=0.5, SuperArmor=300, ImmunityDuration=0.5
    ///   Boss (normal):       Resistance=0.8, SuperArmor=800, ImmunityDuration=1.0
    ///   Boss (enraged):      IsImmune=true (no knockback during phase)
    ///   Stationary turret:   IsImmune=true (never knocked back)
    ///   Light enemy:         Resistance=0.0, SuperArmor=0, ImmunityDuration=0.1
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct KnockbackResistance : IComponentData
    {
        /// <summary>
        /// Percentage of knockback force absorbed (0-1).
        /// 0 = no resistance (full knockback). 0.5 = half knockback. 1.0 = immune.
        /// Final force = RequestForce * (1 - ResistancePercent).
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float ResistancePercent;

        /// <summary>
        /// Force threshold below which knockback is completely ignored.
        /// Prevents light hits from interrupting heavy enemies.
        /// 0 = any force causes knockback. 500 = only explosions+ cause knockback.
        /// Bypassed by KnockbackRequest.IgnoreSuperArmor.
        /// </summary>
        [GhostField(Quantization = 10)]
        public float SuperArmorThreshold;

        /// <summary>
        /// Seconds of knockback immunity after a knockback ends.
        /// Prevents stunlock chains. 0 = no immunity window.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ImmunityDuration;

        /// <summary>
        /// Remaining immunity time. Decremented each tick.
        /// While > 0, all knockback requests are rejected.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ImmunityTimeRemaining;

        /// <summary>
        /// Hard immunity flag. When true, ALL knockback is rejected regardless of force.
        /// Used for boss phase transitions, invulnerability frames, cutscenes.
        /// </summary>
        [GhostField]
        public bool IsImmune;

        /// <summary>True if entity is currently in immunity window or hard immune.</summary>
        public bool IsCurrentlyImmune => IsImmune || ImmunityTimeRemaining > 0f;
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackResistance.cs` (NEW)

### 1.7 KnockbackConfig IComponentData (Singleton)

```csharp
using Unity.Entities;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Global knockback tuning parameters.
    /// Singleton entity, baked from KnockbackConfigAuthoring in SubScene.
    /// </summary>
    public struct KnockbackConfig : IComponentData
    {
        // === Duration Defaults (seconds) ===

        /// <summary>Default duration for Push knockback. Recommended: 0.4</summary>
        public float PushDuration;

        /// <summary>Default duration for Launch knockback. Recommended: 0.6</summary>
        public float LaunchDuration;

        /// <summary>Default duration for Pull knockback. Recommended: 0.5</summary>
        public float PullDuration;

        /// <summary>Default duration for Stagger knockback. Recommended: 0.2</summary>
        public float StaggerDuration;

        // === Force-to-Velocity Conversion ===

        /// <summary>
        /// Divider to convert Newtons to m/s velocity.
        /// Velocity = Force / ForceDivisor.
        /// Higher = slower knockback. Recommended: 100.
        /// 1000N / 100 = 10 m/s initial velocity.
        /// </summary>
        public float ForceDivisor;

        /// <summary>
        /// Maximum knockback velocity magnitude (m/s).
        /// Prevents extreme knockback from launching entities off the map.
        /// Recommended: 25.
        /// </summary>
        public float MaxVelocity;

        /// <summary>
        /// Minimum force (after resistance) required to produce any knockback.
        /// Forces below this are discarded. Prevents imperceptible micro-knockbacks.
        /// Recommended: 50.
        /// </summary>
        public float MinimumEffectiveForce;

        // === Launch Tuning ===

        /// <summary>
        /// Default vertical ratio for Launch type when not overridden per-request.
        /// 0.3 = gentle arc. 0.5 = 45-degree. 0.7 = high arc.
        /// Recommended: 0.4.
        /// </summary>
        public float DefaultLaunchVerticalRatio;

        /// <summary>
        /// Gravity multiplier applied to vertical velocity during Launch.
        /// Higher = faster arc descent. 1.0 = normal gravity. 2.0 = heavy.
        /// Recommended: 1.5 (snappy arcs, not floaty).
        /// </summary>
        public float LaunchGravityMultiplier;

        // === Stagger Tuning ===

        /// <summary>
        /// Force multiplier for Stagger type (typically much lower than Push).
        /// Stagger is a brief hitch, not a full send.
        /// Recommended: 0.2 (20% of Push force).
        /// </summary>
        public float StaggerForceMultiplier;

        /// <summary>
        /// Freeze frames at stagger start (in fixed timesteps, not seconds).
        /// 0 = no freeze. 2-3 = subtle hitch. 5+ = heavy hitstop.
        /// Recommended: 2.
        /// </summary>
        public int StaggerFreezeFrames;

        // === Surface Friction ===

        /// <summary>
        /// If true, knockback slide distance is affected by surface material friction.
        /// Requires SurfaceDatabaseBlob singleton to be present.
        /// </summary>
        public bool EnableSurfaceFriction;

        // === Interrupt ===

        /// <summary>
        /// Force threshold above which knockback automatically triggers InterruptRequest.
        /// Only applies when KnockbackRequest.TriggersInterrupt is also true.
        /// Recommended: 300 (medium hits and above).
        /// </summary>
        public float InterruptForceThreshold;

        /// <summary>Default configuration values.</summary>
        public static KnockbackConfig Default => new KnockbackConfig
        {
            PushDuration = 0.4f,
            LaunchDuration = 0.6f,
            PullDuration = 0.5f,
            StaggerDuration = 0.2f,
            ForceDivisor = 100f,
            MaxVelocity = 25f,
            MinimumEffectiveForce = 50f,
            DefaultLaunchVerticalRatio = 0.4f,
            LaunchGravityMultiplier = 1.5f,
            StaggerForceMultiplier = 0.2f,
            StaggerFreezeFrames = 2,
            EnableSurfaceFriction = true,
            InterruptForceThreshold = 300f
        };
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackConfig.cs` (NEW)

### 1.8 KnockbackResolveSystem

The core system that consumes `KnockbackRequest` entities and writes `KnockbackState` on targets.

```csharp
// KnockbackResolveSystem
// [BurstCompile]
// [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
// [UpdateBefore(typeof(KnockbackMovementSystem))]
// [UpdateAfter(typeof(ExternalForceSystem))]
// [WorldSystemFilter(ServerSimulation | ClientSimulation | LocalSimulation)]

// For each KnockbackRequest entity:
//   1. Validate target exists and has KnockbackState
//   2. Check KnockbackResistance (if present):
//      a. IsImmune or ImmunityTimeRemaining > 0 → reject (destroy request)
//      b. Apply SuperArmorThreshold check (unless IgnoreSuperArmor)
//      c. Scale force by (1 - ResistancePercent)
//   3. Apply falloff: effectiveForce *= FalloffMultiplier(Distance, MaxRadius, Falloff)
//   4. Skip if effectiveForce < KnockbackConfig.MinimumEffectiveForce
//   5. Convert force to velocity: velocity = direction * (effectiveForce / ForceDivisor)
//   6. Apply type-specific modifiers:
//      - Push: horizontal only (zero Y)
//      - Launch: add vertical = velocity.magnitude * LaunchVerticalRatio * up
//      - Pull: negate direction (toward source)
//      - Stagger: multiply force by StaggerForceMultiplier, set freeze frames
//   7. Clamp velocity to MaxVelocity
//   8. Write KnockbackState:
//      - IsActive = true
//      - Velocity = computed velocity
//      - InitialSpeed = length(velocity)
//      - Duration = type default or override
//      - Elapsed = 0
//      - Easing = request easing
//      - Type = request type
//      - SourceEntity = request source
//   9. If TriggersInterrupt and force >= InterruptForceThreshold:
//      - Create InterruptRequest on target (EPIC 16.1 integration)
//  10. Destroy request entity
```

**Pipeline rules:**
- If target already has an active knockback (`KnockbackState.IsActive`), the **stronger** knockback wins. Compare `InitialSpeed` of existing vs incoming. If incoming is stronger, override. If existing is stronger, reject incoming.
- Multiple knockback requests on the same target in the same frame: highest force wins, rest discarded.
- Stagger type does NOT override Push/Launch/Pull (those are displacement-dominant). Stagger only applies if no active displacement knockback.

**File:** `Assets/Scripts/Combat/Knockback/Systems/KnockbackResolveSystem.cs` (NEW)

### 1.9 KnockbackMovementSystem

Applies `KnockbackState` velocity to entity position each tick.

```csharp
// KnockbackMovementSystem
// [BurstCompile]
// [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
// [UpdateAfter(typeof(PlayerMovementSystem))]
// [UpdateAfter(typeof(CharacterControllerSystem))]
// [WorldSystemFilter(ServerSimulation | ClientSimulation | LocalSimulation)]

// For each entity with KnockbackState.IsActive:
//   1. Advance elapsed: Elapsed += deltaTime
//   2. Compute easing factor: t = EasingFunction(Progress, Easing)
//      - Linear:  factor = 1 - Progress
//      - EaseOut: factor = 1 - Progress^2
//      - Bounce:  factor = BounceEval(Progress) -- see math below
//      - Sharp:   factor = (1 - Progress)^3
//   3. Compute frame velocity: frameVelocity = Velocity * factor
//   4. Surface friction (optional):
//      - Raycast down from entity position to get SurfaceID
//      - Lookup friction modifier from SurfaceDatabaseBlob
//      - Apply: frameVelocity *= frictionModifier
//   5. Apply displacement based on entity type:
//      a. PLAYER (has PlayerTag + ExternalForceState):
//         - Write AddExternalForceRequest with frameVelocity * deltaTime
//         - ExternalForceSystem handles CharacterController integration
//      b. ENEMY (has AIBrain, no PlayerTag):
//         - Write LocalTransform.Position += frameVelocity * deltaTime directly
//         - Same pattern as EnemySeparationSystem (kinematic position writes)
//      c. GENERIC (neither PlayerTag nor AIBrain):
//         - Write LocalTransform.Position += frameVelocity * deltaTime
//   6. For Launch type: apply gravity to vertical velocity
//      - Velocity.y -= 9.81 * LaunchGravityMultiplier * deltaTime
//   7. If IsExpired: set IsActive = false
```

**Easing math (Burst-compatible, no allocations):**

```csharp
public static float EvaluateEasing(float progress, KnockbackEasing easing)
{
    float t = math.saturate(progress);
    return easing switch
    {
        KnockbackEasing.Linear  => 1f - t,
        KnockbackEasing.EaseOut => 1f - (t * t),
        KnockbackEasing.Bounce  => EvaluateBounce(t),
        KnockbackEasing.Sharp   => (1f - t) * (1f - t) * (1f - t),
        _                       => 1f - (t * t) // Default to EaseOut
    };
}

private static float EvaluateBounce(float t)
{
    // Primary deceleration (0-0.7), then small bounce (0.7-1.0)
    if (t < 0.7f)
        return 1f - (t / 0.7f) * (t / 0.7f);
    float bt = (t - 0.7f) / 0.3f;
    return 0.15f * math.sin(bt * math.PI); // Small bounce at end
}
```

**File:** `Assets/Scripts/Combat/Knockback/Systems/KnockbackMovementSystem.cs` (NEW)

### 1.10 KnockbackCleanupSystem

```csharp
// KnockbackCleanupSystem
// [BurstCompile]
// [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
// [UpdateAfter(typeof(KnockbackMovementSystem))]
// [WorldSystemFilter(ServerSimulation | ClientSimulation | LocalSimulation)]

// For each entity with KnockbackState.IsActive == false and KnockbackResistance:
//   - Start immunity timer: ImmunityTimeRemaining = ImmunityDuration
// For each entity with KnockbackResistance.ImmunityTimeRemaining > 0:
//   - Decrement: ImmunityTimeRemaining -= deltaTime
//   - Clamp to 0
```

**File:** `Assets/Scripts/Combat/Knockback/Systems/KnockbackCleanupSystem.cs` (NEW)

### Implementation Tasks -- Phase 1

- [ ] Create `KnockbackType`, `KnockbackEasing`, `KnockbackFalloff` enums
- [ ] Create `KnockbackRequest` IComponentData
- [ ] Create `KnockbackState` IComponentData with `[GhostComponent(AllPredicted)]`
- [ ] Create `KnockbackResistance` IComponentData with `[GhostComponent(All)]`
- [ ] Create `KnockbackConfig` IComponentData singleton with `Default` static property
- [ ] Implement `KnockbackResolveSystem` (consume requests, resistance checks, velocity computation)
- [ ] Implement `KnockbackMovementSystem` (easing curves, player vs enemy dispatch, surface friction)
- [ ] Implement `KnockbackCleanupSystem` (immunity timers, state reset)
- [ ] Implement `KnockbackEasingMath` static utility (Burst-compatible easing functions)
- [ ] **Test:** Create KnockbackRequest with Push type targeting player -> player slides backward with EaseOut
- [ ] **Test:** Create KnockbackRequest with Launch type -> entity arcs upward then falls
- [ ] **Test:** Create KnockbackRequest with Pull type -> entity moves toward source
- [ ] **Test:** Entity with KnockbackResistance.IsImmune = true -> no displacement
- [ ] **Test:** SuperArmorThreshold = 500, force = 300 -> no knockback; force = 600 -> knockback applied
- [ ] **Test:** ImmunityDuration = 0.5 -> second knockback within 0.5s rejected
- [ ] **Test:** Stronger knockback overrides weaker active knockback

---

## Phase 2: Source System Integration

### 2.1 Explosion Knockback (ExplosiveStats.PhysicsForce)

Wire `ExplosiveDetonationSystem` and `ProjectileExplosionSystem` to create `KnockbackRequest` entities for all damageable entities within blast radius.

```csharp
// In ProjectileExplosionSystem.TriggerDetonation() or new ExplosionKnockbackSystem:
//
// For each entity within ExplosiveStats.BlastRadius:
//   float3 direction = normalize(targetPos - explosionPos);
//   float distance = length(targetPos - explosionPos);
//
//   ecb.CreateEntity() with KnockbackRequest:
//     TargetEntity = hitEntity
//     SourceEntity = explosive.PlacerEntity
//     Direction = direction
//     Force = ExplosiveStats.PhysicsForce  // 500-2000N, already tuned per type
//     Type = KnockbackType.Push
//     Falloff = KnockbackFalloff.Quadratic
//     Distance = distance
//     MaxRadius = ExplosiveStats.BlastRadius
//     Easing = KnockbackEasing.EaseOut
//     TriggersInterrupt = true
```

**New system:** `ExplosionKnockbackSystem` (or extension of `ExplosiveDetonationSystem`)
- `[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]`
- `[UpdateAfter(typeof(ExplosiveDetonationSystem))]`
- Uses `CollisionWorld.OverlapAabb` or `CalculateDistance` for radius query
- Creates one `KnockbackRequest` per entity in radius
- Filters: must have `KnockbackState` component (modularity -- entities without it are unaffected)

**File:** `Assets/Scripts/Combat/Knockback/Systems/ExplosionKnockbackSystem.cs` (NEW)

### 2.2 Weapon Modifier Knockback (CombatResolutionSystem)

Replace the no-op `ModifierType.Knockback` case in `CombatResolutionSystem` (line 169).

```csharp
// In CombatResolutionSystem, ModifierType.Knockback case:
// BEFORE (current):
//   case ModifierType.Knockback:
//       // Knockback requires kinematic body displacement system (future)
//       break;
//
// AFTER:
//   case ModifierType.Knockback:
//   {
//       float3 hitDir = math.normalizesafe(
//           pendingHit.HitPoint - attackerPos, new float3(0, 0, 1));
//       ecb.CreateEntity() with KnockbackRequest:
//         TargetEntity = pendingHit.TargetEntity
//         SourceEntity = pendingHit.AttackerEntity
//         Direction = hitDir
//         Force = mod.Force  // WeaponModifier.Force field (already exists)
//         Type = KnockbackType.Push
//         Falloff = KnockbackFalloff.None  // Single-target, no falloff
//         Easing = KnockbackEasing.EaseOut
//         TriggersInterrupt = (mod.Force >= 500f)  // Heavy hits interrupt
//       break;
//   }
```

**File:** `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` (MODIFY -- replace no-op case)

### 2.3 Modifier Explosion Knockback

`ModifierExplosionRequest` already has a `KnockbackForce` field that `ModifierExplosionSystem` ignores. Wire it to the knockback pipeline.

```csharp
// In ModifierExplosionSystem, after applying damage to entities in radius:
// For each damaged entity:
//   if (request.KnockbackForce > 0)
//     ecb.CreateEntity() with KnockbackRequest:
//       Force = request.KnockbackForce
//       Type = KnockbackType.Push
//       Falloff = KnockbackFalloff.Quadratic
//       MaxRadius = request.Radius
//       ...
```

**File:** `Assets/Scripts/Combat/Systems/ModifierExplosionSystem.cs` (MODIFY -- add knockback request creation)

### 2.4 Tackle Knockback (Enemy Support)

Extend `TackleCollisionSystem` to create `KnockbackRequest` on tackled enemies, not just players.

```csharp
// Currently TackleCollisionSystem only sets PlayerCollisionState.KnockdownTimeRemaining on players.
// Extension: also create KnockbackRequest for entities with KnockbackState + AIBrain.
//
// For each entity hit by tackle cone:
//   if (hasKnockbackState)
//     ecb.CreateEntity() with KnockbackRequest:
//       TargetEntity = hitEntity
//       Force = TackleSettings.KnockbackForce  // new field
//       Type = KnockbackType.Push
//       Direction = tackleDirection
//       Easing = KnockbackEasing.Sharp
```

**File:** `Assets/Scripts/Player/Systems/TackleCollisionSystem.cs` (MODIFY -- add enemy knockback)
**File:** `Assets/Scripts/Player/Components/TackleSettings.cs` (MODIFY -- add `KnockbackForce` field)

### Implementation Tasks -- Phase 2

- [ ] Implement `ExplosionKnockbackSystem` (radius query, create KnockbackRequest per entity)
- [ ] Modify `CombatResolutionSystem` line 169: replace no-op with KnockbackRequest creation
- [ ] Modify `ModifierExplosionSystem`: create KnockbackRequest when `KnockbackForce > 0`
- [ ] Modify `TackleCollisionSystem`: create KnockbackRequest for enemies hit by tackle
- [ ] Add `KnockbackForce` float field to `TackleSettings`
- [ ] **Test:** Grenade explosion knocks back all entities in radius with quadratic falloff
- [ ] **Test:** Weapon with Knockback modifier pushes target on hit
- [ ] **Test:** Modifier explosion (fire nova proc) applies knockback from `ModifierExplosionRequest.KnockbackForce`
- [ ] **Test:** Tackle knocks back both players (existing) and enemies (new)
- [ ] **Test:** Entity at edge of blast radius receives less knockback than entity at center

---

## Phase 3: Surface-Dependent Knockback

### 3.1 Surface Friction Integration

Extend `SurfaceEntry` in `SurfaceDatabaseBlob` with a `KnockbackFrictionModifier` field, or compute it from existing `Hardness` property.

```csharp
// Option A: Add field to SurfaceEntry (preferred, explicit control)
public struct SurfaceEntry
{
    // ... existing fields ...

    /// <summary>
    /// EPIC 16.9: Multiplier for knockback slide distance on this surface.
    /// 1.0 = normal. 0.5 = high friction (rubber, mud). 1.5 = low friction (ice, wet metal).
    /// </summary>
    public float KnockbackFrictionModifier;
}

// Option B: Derive from existing Hardness (zero new data, less designer control)
// float frictionMod = surfaceEntry.SurfaceId switch {
//     SurfaceID.Ice        => 1.8f,
//     SurfaceID.Snow       => 1.3f,
//     SurfaceID.Mud        => 0.5f,
//     SurfaceID.Sand       => 0.6f,
//     SurfaceID.Water      => 1.2f,
//     SurfaceID.Rubber     => 0.3f,
//     SurfaceID.Concrete   => 0.9f,
//     SurfaceID.Metal_Thin => 1.1f,
//     _                    => 1.0f
// };
```

### 3.2 Surface Detection in KnockbackMovementSystem

```csharp
// During knockback movement tick:
// 1. Raycast down from entity position (short ray, 0.5m)
// 2. Read SurfaceID from hit collider's SurfaceTag component (EPIC 15.24)
// 3. Lookup KnockbackFrictionModifier from SurfaceDatabaseBlob
// 4. Apply: frameVelocity *= frictionModifier
//
// If no surface database or raycast misses: frictionModifier = 1.0 (no effect)
// Cache surface ID per entity to avoid raycasting every tick (raycast every 0.1s, lerp between)
```

### 3.3 Reference Friction Values

| Surface | KnockbackFrictionModifier | Feel |
|---------|--------------------------|------|
| Ice | 1.8 | Long slide, almost no deceleration |
| Snow | 1.3 | Noticeable extra slide |
| Water (shallow) | 1.2 | Slight hydroplane |
| Metal (wet) | 1.1 | Slight extra slide |
| Concrete | 0.9 | Standard, slightly grippy |
| Wood | 0.95 | Nearly standard |
| Dirt | 0.85 | Slightly more grip |
| Sand | 0.6 | Significant drag |
| Mud | 0.5 | Heavy drag, short slide |
| Grass | 0.8 | Moderate grip |
| Rubber | 0.3 | Almost no slide |
| Gravel | 0.7 | Good grip, rough surface |

### Implementation Tasks -- Phase 3

- [ ] Add `KnockbackFrictionModifier` float to `SurfaceEntry` struct in `SurfaceDatabaseBlob.cs`
- [ ] Update `SurfaceDatabaseInitSystem` to populate friction values per surface material
- [ ] Add surface raycast + friction application to `KnockbackMovementSystem`
- [ ] Implement surface ID caching (raycast every N ticks, not every frame)
- [ ] **Test:** Player knocked back on ice slides 1.8x farther than on concrete
- [ ] **Test:** Player knocked back on mud slides 0.5x normal distance
- [ ] **Test:** Surface without friction data defaults to 1.0 (no effect)
- [ ] **Test:** No SurfaceDatabaseBlob singleton present -> system gracefully skips friction

---

## Phase 4: Interrupt Integration (EPIC 16.1)

### 4.1 Knockback-Triggered Interrupts

When knockback force exceeds `KnockbackConfig.InterruptForceThreshold` AND the request has `TriggersInterrupt = true`, create an `InterruptRequest` on the target entity.

```csharp
// In KnockbackResolveSystem, after computing effectiveForce:
//
// if (request.TriggersInterrupt && effectiveForce >= config.InterruptForceThreshold)
// {
//     // EPIC 16.1 integration: interrupt ability casts, channels, interactions
//     if (hasInterruptableComponent(targetEntity))
//     {
//         ecb.AddComponent(targetEntity, new InterruptRequest
//         {
//             Reason = InterruptReason.Knockback,
//             SourceEntity = request.SourceEntity,
//             Force = effectiveForce
//         });
//     }
// }
```

### 4.2 Interaction Interruption

Knockback that exceeds `InterruptResistance.High` should cancel active interactions (stations, channels).

```csharp
// InteractionInterruptConfig.CancelOnKnockback (new field):
//   When true, knockback above High resistance cancels the interaction.
//   Default: true for channels, false for stations.
```

### 4.3 SuperArmor During Ability Casts

Some abilities grant temporary SuperArmor to prevent knockback during their animation:

```csharp
// AbilityCastState could set KnockbackResistance.SuperArmorThreshold temporarily:
//   On cast start: save original threshold, set to ability's SuperArmorValue
//   On cast end: restore original threshold
//
// This is a system-level behavior, not a new component.
// AbilityExecutionSystem reads ability definition's SuperArmorDuringCast field.
```

### Implementation Tasks -- Phase 4

- [ ] Add `InterruptRequest` creation logic to `KnockbackResolveSystem` (conditional on TriggersInterrupt + force threshold)
- [ ] Define `InterruptReason.Knockback` enum value (extend existing `InterruptReason` if present, or create)
- [ ] Add `CancelOnKnockback` field to `InteractionInterruptConfig` (EPIC 16.1)
- [ ] Document SuperArmor-during-cast pattern for ability system integration
- [ ] **Test:** Knockback with TriggersInterrupt=true and force=600 (above 300 threshold) creates InterruptRequest
- [ ] **Test:** Knockback with TriggersInterrupt=false does NOT create InterruptRequest regardless of force
- [ ] **Test:** Active channel interaction cancelled by knockback interrupt

---

## Phase 5: Authoring & Configuration

### 5.1 KnockbackStateAuthoring

```csharp
// MonoBehaviour on any entity prefab that should be knockback-capable.
// Baker adds KnockbackState (zeroed) to the entity.
//
// Fields:
//   (none -- KnockbackState starts zeroed, runtime-only)
//
// Why a separate authoring? So designers can opt-in per prefab.
// Entities without KnockbackState ignore all knockback (the modularity mechanism).
```

**File:** `Assets/Scripts/Combat/Knockback/Authoring/KnockbackStateAuthoring.cs` (NEW)

### 5.2 KnockbackResistanceAuthoring

```csharp
// MonoBehaviour for entities with knockback resistance.
// Baker adds KnockbackResistance with designer-configured values.
//
// Inspector fields:
//   [Range(0, 1)] float ResistancePercent = 0;
//   float SuperArmorThreshold = 0;
//   float ImmunityDuration = 0.2f;
//   bool StartImmune = false;  // For boss prefabs that start immune
```

**File:** `Assets/Scripts/Combat/Knockback/Authoring/KnockbackResistanceAuthoring.cs` (NEW)

### 5.3 KnockbackConfigAuthoring

```csharp
// MonoBehaviour for singleton SubScene entity.
// Baker creates KnockbackConfig singleton with tunable parameters.
//
// Inspector fields mirror KnockbackConfig struct.
// Defaults populated from KnockbackConfig.Default.
// Custom editor with tooltips and reference value labels.
```

**File:** `Assets/Scripts/Combat/Knockback/Authoring/KnockbackConfigAuthoring.cs` (NEW)

### 5.4 KnockbackSourceAuthoring (Optional Convenience)

```csharp
// MonoBehaviour for entities that produce knockback on collision/trigger enter.
// Baker adds KnockbackSourceConfig component.
// Useful for environmental hazards: steam vents, push traps, conveyor belt ends.
//
// Inspector fields:
//   float Force = 500;
//   KnockbackType Type = Push;
//   KnockbackEasing Easing = EaseOut;
//   KnockbackFalloff Falloff = None;
//   float Radius = 0;  // 0 = contact only, >0 = area
//   bool TriggersInterrupt = false;
//   float Cooldown = 1.0f;  // Seconds between knockbacks on same target
```

**File:** `Assets/Scripts/Combat/Knockback/Authoring/KnockbackSourceAuthoring.cs` (NEW)

### 5.5 KnockbackSourceConfig Component

```csharp
using Unity.Entities;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Configuration for environmental knockback sources.
    /// Steam vents, push traps, geyser tiles, conveyor belt ends.
    /// KnockbackTriggerSystem creates KnockbackRequests when entities enter trigger volume.
    /// </summary>
    public struct KnockbackSourceConfig : IComponentData
    {
        public float Force;
        public KnockbackType Type;
        public KnockbackEasing Easing;
        public KnockbackFalloff Falloff;
        public float Radius;
        public bool TriggersInterrupt;
        public float Cooldown;
        public float LastTriggerTime;
    }
}
```

**File:** `Assets/Scripts/Combat/Knockback/Components/KnockbackSourceConfig.cs` (NEW)

### 5.6 KnockbackTriggerSystem

```csharp
// KnockbackTriggerSystem
// [BurstCompile]
// [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
// [UpdateBefore(typeof(KnockbackResolveSystem))]
// [WorldSystemFilter(ServerSimulation | ClientSimulation | LocalSimulation)]
//
// Detects trigger events (Unity.Physics ITriggerEventsJob) between
// KnockbackSourceConfig entities and KnockbackState entities.
// Creates KnockbackRequest for each qualifying trigger event.
// Respects per-target cooldown to prevent spam.
```

**File:** `Assets/Scripts/Combat/Knockback/Systems/KnockbackTriggerSystem.cs` (NEW)

### Implementation Tasks -- Phase 5

- [ ] Create `KnockbackStateAuthoring` MonoBehaviour + Baker
- [ ] Create `KnockbackResistanceAuthoring` MonoBehaviour + Baker
- [ ] Create `KnockbackConfigAuthoring` MonoBehaviour + Baker (singleton)
- [ ] Create `KnockbackSourceAuthoring` MonoBehaviour + Baker
- [ ] Create `KnockbackSourceConfig` IComponentData
- [ ] Implement `KnockbackTriggerSystem` (trigger event detection, cooldown, request creation)
- [ ] Add `KnockbackStateAuthoring` to player prefab (Atlas_Client, Warrok_Client)
- [ ] Add `KnockbackStateAuthoring` + `KnockbackResistanceAuthoring` to enemy prefab (BoxingJoe)
- [ ] Add `KnockbackConfigAuthoring` to gameplay SubScene
- [ ] **Test:** Player with KnockbackStateAuthoring receives knockback from all sources
- [ ] **Test:** Enemy with KnockbackResistanceAuthoring (SuperArmor=500) ignores light hits
- [ ] **Test:** Environmental push trap with KnockbackSourceAuthoring pushes players entering trigger
- [ ] **Test:** Cooldown on KnockbackSourceConfig prevents rapid re-triggering
- [ ] **Test:** Entity WITHOUT KnockbackStateAuthoring is completely unaffected by knockback

---

## Phase 6: Animation & Presentation

### 6.1 KnockbackAnimationBridgeSystem

```csharp
// Managed system in PresentationSystemGroup, ClientSimulation only.
// Reads KnockbackState and drives animator parameters.
//
// Animator parameters set:
//   "KnockbackActive"  (bool)   - triggers knockback blend tree
//   "KnockbackType"    (int)    - 0=Push, 1=Launch, 2=Pull, 3=Stagger
//   "KnockbackSpeed"   (float)  - normalized speed for blend tree intensity (0-1)
//   "KnockbackDirX"    (float)  - local-space knockback direction X
//   "KnockbackDirZ"    (float)  - local-space knockback direction Z
//
// Animation blend tree structure (per entity's AnimatorController):
//   KnockbackLayer (additive or override):
//     Idle -> KnockbackPush (DirectionX/Z blend)
//     Idle -> KnockbackLaunch (single clip, airborne)
//     Idle -> KnockbackStagger (short hit react)
//
// Existing KnockdownAnimatorBridge is NOT replaced.
// Knockback animation is separate from knockdown (stagger/knockdown are PlayerCollisionState).
// An entity can be in knockback (sliding) without being knocked down.
```

**File:** `Assets/Scripts/Combat/Knockback/Presentation/KnockbackAnimationBridgeSystem.cs` (NEW)

### 6.2 KnockbackVFXBridgeSystem

```csharp
// Managed system in PresentationSystemGroup, ClientSimulation only.
// Creates visual feedback for active knockback:
//
// On knockback start (IsActive transitions false->true):
//   - Spawn dust/impact VFX at entity feet (uses SurfaceID for material-appropriate particles)
//   - Play impact sound (intensity scaled by InitialSpeed)
//   - Camera shake on local player (magnitude = InitialSpeed / MaxVelocity)
//
// During knockback (IsActive && moving):
//   - Spawn trail particles behind entity (dust trail on ground)
//   - Trail density scaled by speed
//
// On knockback end:
//   - Spawn stopping dust puff
//   - Optional: screen-edge directional indicator showing knockback source direction
//
// Uses existing SurfaceImpactPresenterSystem pattern for material-aware VFX.
```

**File:** `Assets/Scripts/Combat/Knockback/Presentation/KnockbackVFXBridgeSystem.cs` (NEW)

### 6.3 Camera Knockback Response

```csharp
// In existing PlayerCameraSystem or new KnockbackCameraSystem:
//
// When local player enters knockback state:
//   - Additive camera shake (not override -- stacks with existing hit shake)
//   - Magnitude: min(InitialSpeed / MaxVelocity, 1.0) * ShakeMultiplier
//   - Duration: matches KnockbackState.Duration
//   - Frequency: 8-12 Hz (earthquake rumble, not jitter)
//
// For Launch type specifically:
//   - Slight camera pull-back (FOV +2-5 degrees) during ascent
//   - Snap back on landing
//
// Uses existing CameraShakeRequest pattern if available.
```

### Implementation Tasks -- Phase 6

- [ ] Create `KnockbackAnimationBridgeSystem` (managed, PresentationSystemGroup, client)
- [ ] Define animator parameter hashes for knockback blend tree
- [ ] Create `KnockbackVFXBridgeSystem` (managed, PresentationSystemGroup, client)
- [ ] Integrate with `SurfaceImpactPresenterSystem` for material-aware knockback VFX
- [ ] Add camera shake on local player knockback (integrate with existing camera shake pipeline)
- [ ] Add FOV punch for Launch type knockback
- [ ] Create placeholder knockback animation clips (push directional, stagger hit react, launch airborne)
- [ ] **Test:** Player knocked back shows dust trail + impact VFX
- [ ] **Test:** Animator transitions to knockback state during active knockback
- [ ] **Test:** Camera shakes proportionally to knockback force
- [ ] **Test:** VFX material matches surface (dust on dirt, sparks on metal, snow puff on snow)

---

## Phase 7: Designer Tooling & Debug

### 7.1 Knockback Debug Overlay

```csharp
// Toggle via debug console: /knockback_debug
//
// When active, draws in Scene view:
//   - Cyan arrow from entity center in KnockbackState.Velocity direction
//   - Arrow length = velocity magnitude (scaled)
//   - Arrow color: cyan=Push, yellow=Launch, magenta=Pull, red=Stagger
//   - Wire sphere around entity showing ImmunityTimeRemaining (fades as timer expires)
//   - Text label: "KB: Push 8.5m/s [0.3/0.4s] EaseOut"
//
// Displayed for all entities with active KnockbackState in the server world.
```

**File:** `Assets/Scripts/Combat/Knockback/Debug/KnockbackDebugSystem.cs` (NEW)

### 7.2 Knockback Workstation Tab

If the AI Workstation (EPIC 16.1 Phase 9) exists, add a "Knockback" tab:

```
Knockback Inspector:
  Selected Entity: [BoxingJoe #42]
  KnockbackState:
    Active: true
    Type: Push
    Velocity: (3.2, 0, -1.8)  |  Speed: 3.7 m/s
    Progress: [========--] 75%
    Easing: EaseOut
    Duration: 0.4s / Elapsed: 0.3s
  KnockbackResistance:
    Resistance: 50%
    SuperArmor: 300N
    Immunity: 0.0s / 0.5s
    IsImmune: false
  Recent Requests (last 5):
    [0.2s ago] Push 1000N from Player#1 -> Resolved: 500N (50% resist)
    [1.1s ago] Stagger 200N from Player#2 -> Rejected: SuperArmor (200 < 300)
```

**File:** `Assets/Editor/CombatWorkstation/Modules/KnockbackInspectorModule.cs` (NEW)

### 7.3 Knockback Test Tool

```csharp
// Editor window: DIG > Combat > Knockback Tester
//
// 1. Select entity in scene view
// 2. Configure test knockback:
//    - Type dropdown (Push/Launch/Pull/Stagger)
//    - Force slider (0-5000N)
//    - Direction (from camera, specific vector, or "from selected entity")
//    - Easing dropdown
// 3. "Fire Knockback" button -> creates KnockbackRequest in play mode
// 4. "Rapid Fire" toggle -> continuous knockback requests (for stress testing)
// 5. "Reset All" button -> clears all active KnockbackState in world
```

**File:** `Assets/Editor/KnockbackTester/KnockbackTesterWindow.cs` (NEW)

### Implementation Tasks -- Phase 7

- [ ] Create `KnockbackDebugSystem` (Scene view gizmo overlay, toggle via console)
- [ ] Create `KnockbackInspectorModule` for Combat Workstation (live state display)
- [ ] Create `KnockbackTesterWindow` EditorWindow (force/type/direction, fire button)
- [ ] Add `/knockback_debug` console command registration
- [ ] **Test:** Debug overlay shows arrows for all actively knocked-back entities
- [ ] **Test:** Knockback tester can push any selected entity in play mode
- [ ] **Test:** Workstation shows real-time KnockbackState + resistance values

---

## System Execution Order

```
PredictedFixedStepSimulationSystemGroup:
  ...
  ExternalForceSystem               [EXISTING, before PlayerMovementSystem]
  PlayerMovementSystem              [EXISTING]
  CharacterControllerSystem         [EXISTING]
  ...
  KnockbackTriggerSystem            [NEW, creates requests from trigger volumes]
  KnockbackResolveSystem            [NEW, consumes requests, writes KnockbackState]
  KnockbackMovementSystem           [NEW, applies velocity to position]
  KnockbackCleanupSystem            [NEW, resets expired state, immunity timers]
  ...
  PlayerCollisionResponseSystem     [EXISTING, stagger/knockdown -- independent of knockback]

SimulationSystemGroup:
  ...
  ExplosionKnockbackSystem          [NEW, creates requests from explosions]
  ...
  CombatResolutionSystem            [MODIFIED, creates requests from weapon modifiers]
  ModifierExplosionSystem           [MODIFIED, creates requests from modifier explosions]

PresentationSystemGroup (Client):
  KnockbackAnimationBridgeSystem    [NEW, drives animator parameters]
  KnockbackVFXBridgeSystem          [NEW, spawns VFX, triggers audio]
  KnockbackDebugSystem              [NEW, debug only, gizmo overlay]
```

---

## File Summary

### New Files

| # | File | Type | Phase |
|---|------|------|-------|
| 1 | `Combat/Knockback/Components/KnockbackType.cs` | Enum | 1 |
| 2 | `Combat/Knockback/Components/KnockbackEasing.cs` | Enum | 1 |
| 3 | `Combat/Knockback/Components/KnockbackFalloff.cs` | Enum | 1 |
| 4 | `Combat/Knockback/Components/KnockbackRequest.cs` | IComponentData | 1 |
| 5 | `Combat/Knockback/Components/KnockbackState.cs` | IComponentData | 1 |
| 6 | `Combat/Knockback/Components/KnockbackResistance.cs` | IComponentData | 1 |
| 7 | `Combat/Knockback/Components/KnockbackConfig.cs` | IComponentData (Singleton) | 1 |
| 8 | `Combat/Knockback/Systems/KnockbackResolveSystem.cs` | ISystem, Burst | 1 |
| 9 | `Combat/Knockback/Systems/KnockbackMovementSystem.cs` | ISystem, Burst | 1 |
| 10 | `Combat/Knockback/Systems/KnockbackCleanupSystem.cs` | ISystem, Burst | 1 |
| 11 | `Combat/Knockback/Math/KnockbackEasingMath.cs` | Static Utility, Burst | 1 |
| 12 | `Combat/Knockback/Systems/ExplosionKnockbackSystem.cs` | ISystem, Burst | 2 |
| 13 | `Combat/Knockback/Components/KnockbackSourceConfig.cs` | IComponentData | 5 |
| 14 | `Combat/Knockback/Authoring/KnockbackStateAuthoring.cs` | Baker | 5 |
| 15 | `Combat/Knockback/Authoring/KnockbackResistanceAuthoring.cs` | Baker | 5 |
| 16 | `Combat/Knockback/Authoring/KnockbackConfigAuthoring.cs` | Baker (Singleton) | 5 |
| 17 | `Combat/Knockback/Authoring/KnockbackSourceAuthoring.cs` | Baker | 5 |
| 18 | `Combat/Knockback/Systems/KnockbackTriggerSystem.cs` | ISystem, Burst | 5 |
| 19 | `Combat/Knockback/Presentation/KnockbackAnimationBridgeSystem.cs` | Managed SystemBase | 6 |
| 20 | `Combat/Knockback/Presentation/KnockbackVFXBridgeSystem.cs` | Managed SystemBase | 6 |
| 21 | `Combat/Knockback/Debug/KnockbackDebugSystem.cs` | Managed SystemBase | 7 |
| 22 | `Editor/CombatWorkstation/Modules/KnockbackInspectorModule.cs` | EditorWindow Module | 7 |
| 23 | `Editor/KnockbackTester/KnockbackTesterWindow.cs` | EditorWindow | 7 |

All paths relative to `Assets/Scripts/` (runtime) or `Assets/` (editor).

### Modified Files

| # | File | Changes | Phase |
|---|------|---------|-------|
| 1 | `Combat/Systems/CombatResolutionSystem.cs` | Replace `ModifierType.Knockback` no-op (line ~169) with KnockbackRequest creation | 2 |
| 2 | `Combat/Systems/ModifierExplosionSystem.cs` | Add KnockbackRequest creation when `KnockbackForce > 0` | 2 |
| 3 | `Player/Systems/TackleCollisionSystem.cs` | Add KnockbackRequest creation for enemy targets | 2 |
| 4 | `Player/Components/TackleSettings.cs` | Add `float KnockbackForce` field | 2 |
| 5 | `Surface/Data/SurfaceDatabaseBlob.cs` | Add `float KnockbackFrictionModifier` to `SurfaceEntry` | 3 |
| 6 | `Surface/Data/SurfaceDatabaseInitSystem.cs` | Populate friction modifier values per surface | 3 |

### Unchanged Files (Referenced But Not Modified)

| File | Why It Stays |
|------|-------------|
| `Player/Components/PlayerCollisionState.cs` | Stagger/knockdown state is independent. Knockback is a separate displacement channel. |
| `Player/Systems/PlayerCollisionResponseSystem.cs` | Player-vs-player collision response untouched. Operates on different components. |
| `Player/Systems/ExternalForceSystem.cs` | Consumed by KnockbackMovementSystem via AddExternalForceRequest. Not modified. |
| `Player/Components/ExternalForceComponents.cs` | Existing force pipeline reused for player knockback integration. |
| `Player/Systems/PlayerMovementSystem.cs` | Reads ExternalForceState.AccumulatedForce. Already handles external forces. |
| `Runtime/Survival/Explosives/Components/ExplosiveComponents.cs` | ExplosiveStats.PhysicsForce read by ExplosionKnockbackSystem but not modified. |
| `AI/Systems/EnemySeparationSystem.cs` | Pattern referenced for kinematic position writes but not modified. |
| `Player/Animation/KnockdownAnimatorBridge.cs` | Knockdown animation is separate from knockback animation. Both coexist. |

---

## Architecture: Knockback vs Existing Displacement Systems

### Relationship to PlayerCollisionState Stagger/Knockdown

These are **parallel, independent systems** that can coexist:

| System | Trigger | Component | Displacement | Animation |
|--------|---------|-----------|--------------|-----------|
| **Player Collision (EPIC 7)** | Player-vs-player physics collision | `PlayerCollisionState.StaggerVelocity` | Via friction decay in `PlayerMovementSystem` | `KnockdownAnimatorBridge` |
| **Knockback (EPIC 16.9)** | Any source (explosion, ability, trap, melee) | `KnockbackState.Velocity` | Via `KnockbackMovementSystem` (player: ExternalForce, enemy: LocalTransform) | `KnockbackAnimationBridgeSystem` |

An entity CAN be in both states simultaneously:
- Stagger from player collision (brief movement lockout + stagger animation)
- Knockback from explosion (displacement slide)
- The stagger animation plays while the entity slides from knockback
- Displacement stacks additively (stagger velocity + knockback velocity both move the entity)

### Relationship to ExternalForceSystem

`ExternalForceSystem` is the **delivery mechanism** for player knockback, not a replacement:

```
KnockbackMovementSystem
   |
   |  For players: writes AddExternalForceRequest
   |  (knockback velocity packaged as an external force)
   v
ExternalForceSystem
   |
   |  Accumulates all external forces (wind + knockback + conveyor + ...)
   v
PlayerMovementSystem
   |
   |  Reads ExternalForceState.AccumulatedForce
   |  Adds to CharacterController velocity
   v
CharacterControllerSystem
   |
   |  Final position integration
   v
[Player moves]
```

For **enemies**, `KnockbackMovementSystem` writes `LocalTransform.Position` directly, bypassing `ExternalForceSystem` entirely (enemies don't use it -- they're kinematic with direct position writes).

### Relationship to ModifierExplosionRequest.KnockbackForce

`ModifierExplosionRequest.KnockbackForce` currently stores the desired knockback force but `ModifierExplosionSystem` does not use it. After EPIC 16.9:

```
WeaponModifier (Explosion type, Force=500)
   |
   v
CombatResolutionSystem -> creates ModifierExplosionRequest with KnockbackForce=500
   |
   v
ModifierExplosionSystem -> applies damage + NOW creates KnockbackRequest with Force=500
   |
   v
KnockbackResolveSystem -> processes request -> writes KnockbackState on targets
```

---

## Kinematic Body Handling (Critical: Enemy Knockback)

### The Problem

Enemies in DIG are **kinematic bodies** (`BodyMotionType.Kinematic`). They have `PhysicsVelocity` (for collision response) but physics does **not** integrate velocity into position. AI behavior systems write `LocalTransform.Position` directly.

From MEMORY.md:
> **Kinematic bodies HAVE `PhysicsVelocity`** (for collision response) but physics does NOT integrate velocity into position
> **Kinematic bodies**: must write `LocalTransform.Position` directly

### The Solution

`KnockbackMovementSystem` detects entity type and applies displacement differently:

```csharp
// Entity type detection (Burst-compatible, no managed lookups):
//
// Player detection:  has PlayerTag component
//   -> Write AddExternalForceRequest (goes through ExternalForceSystem -> CharacterController)
//
// Enemy detection:   has AIBrain component, no PlayerTag
//   -> Write LocalTransform.Position += frameVelocity * deltaTime (direct position write)
//   -> Same pattern as EnemySeparationSystem (proven safe for kinematic bodies)
//
// Generic entity:    neither PlayerTag nor AIBrain
//   -> Write LocalTransform.Position += frameVelocity * deltaTime
//   -> For future: destructibles, props, etc.
```

### Collision During Knockback

When an enemy is being knocked back (sliding), it may collide with walls or other obstacles:

```csharp
// Wall collision detection during enemy knockback:
// 1. Before writing new position, raycast in movement direction (length = frameVelocity * deltaTime)
// 2. If raycast hits environment collider within movement distance:
//    a. Clamp position to hit point - small offset (0.01m from wall)
//    b. Reflect or zero velocity based on KnockbackType:
//       - Push/Pull: zero velocity (stop against wall)
//       - Launch: reflect vertical only (bounce off ceiling, slide along wall)
//       - Stagger: zero velocity (stop)
//    c. Optionally: deal "impact damage" proportional to remaining velocity (wall splat)
// 3. If no hit: apply full displacement
//
// This prevents enemies from being knocked through walls or into geometry.
```

---

## NetCode Prediction & Rollback

### Prediction Strategy

Knockback is **fully predicted** on the owning client:

```
Frame N (Client):
  1. Explosion system creates KnockbackRequest (predicted)
  2. KnockbackResolveSystem computes velocity (predicted)
  3. KnockbackMovementSystem applies displacement (predicted)
  -> Client sees instant knockback, zero latency

Frame N+RTT (Server):
  1. Server processes same KnockbackRequest
  2. If server result matches client prediction: no correction
  3. If server result differs (resistance changed, immunity triggered):
     -> NetCode rollback corrects KnockbackState
     -> Client smoothly interpolates to correct position
```

### Ghost Configuration

```
KnockbackState:
  PrefabType = AllPredicted
  Velocity:     Quantization=1000, Smoothing=InterpolateAndExtrapolate
  InitialSpeed: Quantization=100
  Duration:     Quantization=100
  Elapsed:      Quantization=100
  Easing:       No quantization (byte enum)
  Type:         No quantization (byte enum)
  GroundedOnly: No quantization (bool)

KnockbackResistance:
  PrefabType = All  (visible to all clients for UI display)
  ResistancePercent:    Quantization=1000
  SuperArmorThreshold:  Quantization=10
  ImmunityDuration:     Quantization=100
  ImmunityTimeRemaining: Quantization=100
  IsImmune:             No quantization (bool)
```

### Bandwidth Analysis

| Component | Size | PrefabType | When Replicated |
|-----------|------|------------|-----------------|
| `KnockbackState` | 40 bytes | AllPredicted | Only during active knockback |
| `KnockbackResistance` | 20 bytes | All | Changes infrequently (delta-compressed near-zero) |

**Per-entity overhead during knockback:** ~40 bytes/snapshot for `KnockbackState`. Knockback lasts 0.2-0.8 seconds at 60 tick rate = 12-48 snapshots. Total: ~480-1920 bytes per knockback event per entity. Negligible.

**Idle overhead:** `KnockbackState.IsActive = false` -- delta-compressed to near-zero by NetCode.

---

## Performance Budget

| System | Target | Notes |
|--------|--------|-------|
| `KnockbackResolveSystem` | < 0.02ms | Per-request processing, typically 0-5 requests/frame |
| `KnockbackMovementSystem` | < 0.05ms | Per-entity with active knockback, simple math + position write |
| `KnockbackCleanupSystem` | < 0.01ms | Timer decrements only |
| `ExplosionKnockbackSystem` | < 0.1ms | Physics overlap query + request creation, only on explosion frames |
| `KnockbackTriggerSystem` | < 0.02ms | Trigger event processing, sparse |
| **Total Knockback Budget** | < 0.2ms | All knockback systems combined |

### Memory

- `KnockbackRequest` entities: transient, destroyed same frame. Max ~32 per frame (explosion worst case).
- `KnockbackState`: 40 bytes per knockback-capable entity (always present, mostly zeroed).
- `KnockbackResistance`: 20 bytes per entity with resistance (optional component).
- `KnockbackConfig`: single singleton entity, 52 bytes.
- Zero managed allocations in hot paths. All Burst-compatible.

### 16KB Archetype Awareness

- `KnockbackState` (40 bytes) added to player/enemy archetypes. Well within budget.
- `KnockbackResistance` (20 bytes) optional, only on entities that need it.
- `KnockbackRequest` entities are standalone (own archetype), destroyed same frame.
- No `IBufferElementData` added to ghost-replicated entities (respects MEMORY.md rule).

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| `KnockbackState` absent | No knockback | Entity ignores all KnockbackRequests targeting it |
| `KnockbackResistance` absent | Zero resistance | Entity receives full knockback force (no mitigation) |
| `KnockbackConfig` absent | Uses `KnockbackConfig.Default` | System falls back to hardcoded defaults |
| `KnockbackFrictionModifier` absent on surface | 1.0 | No surface friction effect (standard slide distance) |
| `KnockbackSourceConfig` absent | No environmental knockback | Trigger volumes don't produce knockback |
| `WeaponModifier.Force` = 0 | No weapon knockback | Existing weapons without knockback modifier unchanged |
| `ExplosiveStats.PhysicsForce` = 0 | No explosion knockback | Explosive types with zero force unchanged |

All existing systems (`PlayerCollisionResponseSystem`, `ExternalForceSystem`, `TackleSystem`, `PlayerMovementSystem`) continue to function without modification. EPIC 16.9 is purely additive except for three targeted integrations (CombatResolutionSystem, ModifierExplosionSystem, TackleCollisionSystem) that replace no-ops with working code.

---

## Genre Flexibility

| Genre | Push | Launch | Pull | Stagger | Resistance | Surface |
|-------|------|--------|------|---------|------------|---------|
| **FPS/Survival** | Explosions, melee | Mines, jump pads | Grapple hook | Heavy caliber | Armor-based | Full (ice/mud/metal) |
| **ARPG** | Boss shockwaves | Uppercut, geyser | Vortex spell | Parry recoil | Boss phases immune | Indoor/outdoor |
| **Fighting** | Heavy punch | Launcher combo | Command grab | Jab hitstun | SuperArmor on heavies | Arena surfaces |
| **Shooter** | Rocket launcher | Concussion grenade | Gravity grenade | Sniper bodyshot | Heavy class resist | Minimal |
| **Roguelike** | Trap spikes | Spring tile | Magnet enemy | Any hit | Scaling per floor | Tile-based |

The system is genre-agnostic by design. Unused knockback types have zero cost -- they're just different `KnockbackType` enum values processed by the same pipeline.

---

## Modularity & Removal

To **completely remove** the knockback system without breaking anything:

1. Delete all files in `Assets/Scripts/Combat/Knockback/`
2. Delete editor files in `Assets/Editor/CombatWorkstation/Modules/KnockbackInspectorModule.cs` and `Assets/Editor/KnockbackTester/`
3. Revert 3 modified files to pre-EPIC 16.9 state (restore no-op case in CRS, remove KnockbackForce usage in ModifierExplosionSystem, remove enemy knockback in TackleCollisionSystem)
4. Remove `KnockbackStateAuthoring` / `KnockbackResistanceAuthoring` from prefabs
5. Remove `KnockbackConfigAuthoring` from SubScene

Result: hits deal damage with zero displacement. All other systems (damage, combat, AI, physics) are unaffected. No compile errors, no runtime exceptions.

To **disable knockback per-entity** at runtime without removing the system:

```csharp
// Option A: Set resistance to 100%
knockbackResistance.ResistancePercent = 1.0f;

// Option B: Set hard immunity
knockbackResistance.IsImmune = true;

// Option C: Remove KnockbackState component (no knockback capability at all)
ecb.RemoveComponent<KnockbackState>(entity);
```

---

## Integration Points

| System | EPIC | Integration |
|--------|------|-------------|
| `CombatResolutionSystem` | 15.29 | `ModifierType.Knockback` case creates `KnockbackRequest` |
| `ModifierExplosionSystem` | 15.29 | `KnockbackForce > 0` creates `KnockbackRequest` per target in radius |
| `ExplosiveDetonationSystem` | 2.5 | `ExplosiveStats.PhysicsForce` fed to `ExplosionKnockbackSystem` |
| `ProjectileExplosionSystem` | 15.10 | Detonation triggers `ExplosionKnockbackSystem` radius query |
| `TackleCollisionSystem` | 7.x | Tackle creates `KnockbackRequest` for enemies in cone |
| `ExternalForceSystem` | 13.1 | Player knockback delivered via `AddExternalForceRequest` |
| `SurfaceDatabaseBlob` | 15.24 | Surface friction modifiers for knockback slide distance |
| `InterruptRequest` | 16.1 | Knockback above threshold creates interrupt |
| `PlayerCollisionResponseSystem` | 7.3 | Coexists independently -- stagger/knockdown + knockback are separate channels |
| `EnemySeparationSystem` | 15.23 | Pattern reference for kinematic enemy position writes |
| `KnockdownAnimatorBridge` | 7.x | Coexists -- knockback has its own animator bridge |

---

## Verification Checklist

### Core Pipeline
- [ ] `KnockbackRequest` entity created -> consumed by `KnockbackResolveSystem` -> destroyed same frame
- [ ] `KnockbackState` written with correct velocity, duration, easing, type
- [ ] Player slides smoothly with EaseOut curve for 0.4s
- [ ] Enemy slides smoothly with direct position writes for 0.4s
- [ ] Knockback velocity decays to zero at end of duration (not abrupt stop)

### Resistance & Immunity
- [ ] Entity with no `KnockbackResistance` receives full knockback
- [ ] `ResistancePercent = 0.5` -> half the knockback velocity
- [ ] `SuperArmorThreshold = 500`, force 300 -> no knockback
- [ ] `SuperArmorThreshold = 500`, force 600 -> knockback applied (at 600 * (1-resist))
- [ ] `IgnoreSuperArmor = true` -> bypasses SuperArmor regardless of threshold
- [ ] `IsImmune = true` -> zero knockback regardless of force
- [ ] `ImmunityDuration = 0.5` -> second knockback within 0.5s after first ends: rejected
- [ ] Immunity timer decrements correctly and expires

### Knockback Types
- [ ] Push: horizontal displacement away from source, no vertical
- [ ] Launch: horizontal + vertical arc, gravity pulls back down
- [ ] Pull: displacement toward source (not away)
- [ ] Stagger: brief small push with freeze frames

### Easing Curves
- [ ] Linear: constant deceleration, mechanical feel
- [ ] EaseOut: fast start, gradual stop -- feels natural
- [ ] Bounce: primary deceleration + small bounce at end
- [ ] Sharp: near-instant burst, very fast decay

### Source Integration
- [ ] Grenade explosion (500N PhysicsForce) knocks back all entities in radius
- [ ] Weapon with `ModifierType.Knockback` + `Force=300` pushes target on hit
- [ ] `ModifierExplosionRequest.KnockbackForce > 0` creates knockback in AOE
- [ ] Tackle pushes enemies backward (new behavior)
- [ ] Environmental trigger volume (KnockbackSourceConfig) pushes on enter

### Surface Friction
- [ ] Ice surface: 1.8x slide distance
- [ ] Mud surface: 0.5x slide distance
- [ ] No surface database: friction defaults to 1.0

### Kinematic Bodies
- [ ] Player knockback goes through ExternalForceSystem -> CharacterController
- [ ] Enemy knockback writes LocalTransform.Position directly
- [ ] Enemy does not pass through walls during knockback (raycast collision)
- [ ] Enemy stops at wall contact point

### Network
- [ ] Client predicts knockback instantly (zero-latency feel)
- [ ] Server validates -- if server disagrees, smooth rollback correction
- [ ] `KnockbackState` replicates via ghost system with InterpolateAndExtrapolate smoothing
- [ ] Remote clients see smooth knockback on other entities

### Coexistence
- [ ] Player collision stagger + explosion knockback occur simultaneously (both displace)
- [ ] Knockdown animation (PlayerCollisionState) + knockback slide (KnockbackState) stack correctly
- [ ] Existing `ExternalForceSystem` wind zones still work alongside knockback

### Performance
- [ ] 32 simultaneous knockback requests (mass explosion) < 0.2ms total
- [ ] 100 entities with KnockbackState (idle) < 0.01ms overhead
- [ ] Zero managed allocations in KnockbackResolveSystem and KnockbackMovementSystem
- [ ] All core systems pass Burst compilation

### Modularity
- [ ] Entity WITHOUT KnockbackStateAuthoring -> completely unaffected by all knockback
- [ ] Removing all Knockback files -> project compiles (after reverting 3 integrations)
- [ ] `KnockbackResistance.IsImmune = true` at runtime -> entity ignores all future knockback
