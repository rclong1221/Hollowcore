# EPIC 15.28: Unified Combat Resolution Pipeline

**Status:** Planned
**Priority:** High (Combat Core)
**Dependencies:**
- EPIC 15.22 (Floating Damage Text System — Phase 1-3 complete)
- `CombatResolutionSystem` + 4 resolver implementations
- `DamageEventVisualBridgeSystem` + `CombatUIBridgeSystem`
- `ProjectileSystem`, `SweptMeleeHitboxSystem`, `WeaponFireSystem`

**Feature:** Route all weapon damage through the combat resolution pipeline so the UI automatically displays rich combat feedback — crits, headshots, backstabs, elemental theming, and hit severity — without modifying the health/damage pipeline.

---

## Overview

Currently, weapon systems (`ProjectileSystem`, `SweptMeleeHitboxSystem`, `WeaponFireSystem`) bypass the combat resolution pipeline entirely. They create raw `DamageEvent` buffers directly on target entities. This means damage numbers only show plain "hit" text with no crits, headshots, backstabs, elemental theming, or hit severity.

The codebase already has a fully-built combat resolution pipeline:
```
PendingCombatHit → CombatResolutionSystem → Resolvers → CombatResultEvent
```

This includes 4 resolver implementations, stat-based crit rolls, and a UI pipeline that handles all `HitType`/`ResultFlags` values. **It's just not wired to actual weapons.**

This epic routes all weapon damage through combat resolution so the UI automatically gets rich feedback without changing the health pipeline.

---

## Architecture

### Current Flow (Weapons bypass combat resolution)
```
ProjectileSystem ──→ DamageEvent ──→ DamageApplySystem (health)
                                  ──→ DamageEventVisualBridgeSystem (simple UI)

SweptMeleeSystem ──→ DamageEvent ──→ DamageApplySystem (health)
                                  ──→ DamageEventVisualBridgeSystem (simple UI)

WeaponFireSystem ──→ (PLACEHOLDER — server hitscan not implemented)
```

### Target Flow (Weapons go through combat resolution)
```
ProjectileSystem ──→ DamageEvent (health, unchanged)
                 ──→ PendingCombatHit ──→ CombatResolutionSystem ──→ CombatResultEvent
                                          (crit roll, headshot,       ↓
                                           backstab, flags)    CombatUIBridgeSystem (rich UI)

SweptMeleeSystem ──→ DamageEvent (health, unchanged)
                 ──→ PendingCombatHit ──→ [same pipeline]

WeaponFireSystem ──→ DamageEvent + PendingCombatHit ──→ [same pipeline]
```

### Key Principle

Weapon systems continue creating `DamageEvent` for health (zero risk to existing pipeline). They ALSO create `PendingCombatHit` for combat resolution (UI enrichment only). `DamageApplicationSystem` skips health when damage was pre-applied via `DamageEvent`.

### Duplicate Suppression

When a weapon hit flows through both pipelines, two damage numbers would appear. A `CombatResolvedTargets` HashSet in `DamageVisualQueue` prevents this — `CombatResolutionSystem` registers resolved targets, `DamageEventVisualBridgeSystem` skips them.

---

## What We DO NOT Touch

- **`DamageApplySystem.cs`** — Burst-compiled, ghost-aware, predicted. Never modify.
- **`DamageEvent.cs`** — Ghost-replicated buffer. Never modify.
- No new `IBufferElementData` on ghost entities.
- No ghost component changes.
- No entity archetype modifications on networked entities.
- Health pipeline via `DamageEvent` → `DamageApplySystem` is completely untouched.

---

## Phase 1: Extend Data Structures

**Risk:** Low — all additive struct field changes, no ghost/network impact

### Task 1.1: Add hitbox fields to PendingCombatHit
**File:** `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs`

Add to `PendingCombatHit` struct:
```csharp
public HitboxRegion HitRegion;       // Head, Torso, Arms, Legs, etc.
public float HitboxMultiplier;       // 2.0 for head, 0.5 for legs, etc.
public bool DamagePreApplied;        // true = DamageEvent already handles health
public float3 AttackDirection;       // For backstab detection
```

### Task 1.2: Add hitbox fields to CombatContext
**File:** `Assets/Scripts/Combat/Resolvers/CombatContext.cs`

Add to `CombatContext` struct:
```csharp
public HitboxRegion HitRegion;
public float HitboxMultiplier;
public float3 AttackDirection;       // Normalized direction from attacker to target
```

### Task 1.3: Add DamagePreApplied to CombatResultEvent
**File:** `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs`

Add to `CombatResultEvent`:
```csharp
public bool DamagePreApplied;        // Skip health subtraction in DamageApplicationSystem
```

### Task 1.4: Update BuildContext to pass hitbox data
**File:** `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs`

In `BuildContext()`, copy new fields from `PendingCombatHit` → `CombatContext`:
```csharp
context.HitRegion = hit.HitRegion;
context.HitboxMultiplier = hit.HitboxMultiplier;
context.AttackDirection = hit.AttackDirection;
```

Also copy `DamagePreApplied` through to `CombatResultEvent` creation.

### Task 1.5: Add duplicate suppression to DamageVisualQueue
**File:** `Assets/Scripts/Combat/UI/DamageVisualQueue.cs`

Add a static `HashSet<int>` for tracking entities with combat resolution pending:
```csharp
public static readonly HashSet<int> CombatResolvedTargets = new();
```

---

## Phase 2: Enrich Resolvers with Headshot/Backstab/Flags

**Risk:** Low — resolver changes are internal logic only, no system group or ECS changes

### Task 2.1: Add headshot detection to all resolvers
**Files:** All 4 resolvers in `Assets/Scripts/Combat/Resolvers/Implementations/`

After calculating damage, check:
```csharp
if (context.HitRegion == HitboxRegion.Head)
    result.Flags |= ResultFlags.Headshot;
```

Resolver-specific behavior:
| Resolver | Headshot Behavior |
|----------|-------------------|
| `PhysicsHitboxResolver` | Flag only (damage already multiplied by weapon) |
| `StatBasedDirectResolver` | Flag + bonus crit chance on headshots |
| `StatBasedRollResolver` | Flag + guaranteed crit on headshots |
| `HybridResolver` | Flag + bonus crit chance |

### Task 2.2: Add backstab detection
**Files:** `HybridResolver.cs`, `StatBasedRollResolver.cs`

Using `context.AttackDirection` and target facing:
```csharp
// Backstab: attacker behind target (dot product > 0.5 means same facing direction)
if (math.lengthsq(context.AttackDirection) > 0.01f)
{
    float dot = math.dot(context.AttackDirection, targetForward);
    if (dot > 0.5f)
        result.Flags |= ResultFlags.Backstab;
}
```

**Note:** Backstab requires target's forward direction. `CombatResolutionSystem.BuildContext()` reads `LocalTransform` for the target entity and provides it via `AttackDirection`.

### Task 2.3: Apply hitbox multiplier in resolvers
**Files:** All resolvers

The weapon system already applies the hitbox multiplier to `DamageEvent.Amount` for health. Resolvers apply it to their damage calculation to keep UI numbers consistent:
```csharp
rawDamage *= context.HitboxMultiplier;
```

### Task 2.4: Set elemental weakness/resistance flags
**Files:** `StatBasedRollResolver.cs`, `HybridResolver.cs`

After applying elemental resistance:
```csharp
float resistance = context.TargetStats.GetResistance(damageType);
if (resistance > 0.25f) result.Flags |= ResultFlags.Resistance;
if (resistance < -0.1f) result.Flags |= ResultFlags.Weakness;
```

---

## Phase 3: Reroute ProjectileSystem

**Risk:** Medium — modifying a core weapon system. Mitigated by keeping existing DamageEvent path untouched.

### Task 3.1: Add PendingCombatHit creation to ProjectileSystem
**File:** `Assets/Scripts/Weapons/Systems/ProjectileSystem.cs`

After the existing `DamageEvent` creation, add `PendingCombatHit` creation **server-only**:

```csharp
// Existing DamageEvent creation stays unchanged
ecb.AppendToBuffer(targetEntity, new DamageEvent { ... });

// NEW: Create PendingCombatHit for combat resolution (server only)
if (state.WorldUnmanaged.IsServer())
{
    var combatHit = ecb.CreateEntity();
    ecb.AddComponent(combatHit, new PendingCombatHit
    {
        AttackerEntity = projRef.Owner,
        TargetEntity = targetEntity,
        WeaponEntity = entity,
        HitPoint = hit.Position,
        HitNormal = hit.SurfaceNormal,
        HitDistance = math.distance(rayInput.Start, hit.Position),
        WasPhysicsHit = true,
        ResolverType = CombatResolverType.Hybrid,
        HitRegion = hitboxLookup.HasComponent(hitEntityToProcess)
            ? hitboxLookup[hitEntityToProcess].Region : HitboxRegion.Torso,
        HitboxMultiplier = multiplier,
        DamagePreApplied = true,
        AttackDirection = math.normalizesafe(hit.Position - rayInput.Start),
        WeaponData = new WeaponStats { BaseDamage = projRef.Damage, DamageType = DamageType.Physical }
    });
}
```

**System constraints:**
- `[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]`
- NOT Burst-compiled (commented out)
- Runs on both client+server but `PendingCombatHit` created server-only
- `DamageEvent` continues being created on both (prediction)

### Task 3.2: Populate WeaponStats from weapon entity
The `Projectile` component only carries `Damage`. For Phase 1, using `BaseDamage = projRef.Damage` is sufficient — stat scaling happens via `AttackerStats` in `BuildContext`.

---

## Phase 4: DamageApplicationSystem Guard

**Risk:** Low — single guard clause, no structural changes

### Task 4.1: Skip health for pre-applied damage
**File:** `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs`

Add early-out for pre-applied damage:
```csharp
if (combat.DamagePreApplied)
    continue; // Health already handled by DamageEvent pipeline
```

This is a 2-line change. `DamageApplicationSystem` only applies health for hits that come purely through the `CombatResolution` pipeline (e.g., future stat-based targeted attacks without physics).

---

## Phase 5: Duplicate Visual Suppression

**Risk:** Low — static HashSet, cleared each frame, no ECS/archetype impact

### Task 5.1: CombatResolutionSystem registers resolved targets
**File:** `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs`

After creating `CombatResultEvent`:
```csharp
DamageVisualQueue.CombatResolvedTargets.Add(pendingHit.TargetEntity.Index);
```

### Task 5.2: DamageEventVisualBridgeSystem skips resolved targets
**File:** `Assets/Scripts/Combat/Systems/DamageEventVisualBridgeSystem.cs`

```csharp
var suppressedTargets = DamageVisualQueue.CombatResolvedTargets;

// Inside entity loop, before enqueueing:
if (suppressedTargets.Contains(entity.Index))
    continue; // This entity's damage is handled by CombatResultEvent pipeline

// At end of OnUpdate:
suppressedTargets.Clear();
```

### Frame Timing (Verified)
1. **Frame N, PredictedSimulation:** ProjectileSystem creates `DamageEvent` + `PendingCombatHit`
2. **Frame N, SimulationGroup:** CombatResolutionSystem resolves → adds to `CombatResolvedTargets`
3. **Frame N, Presentation:** CombatUIBridgeSystem shows rich number from `CombatResultEvent`
4. **Frame N+1, PredictedFixedStep:** DamageEventVisualBridgeSystem reads `DamageEvent` → checks set → **skips** → clears set

---

## Phase 6: Reroute SweptMeleeHitboxSystem

**Risk:** Medium — Burst-compiled system. Must verify all types used are blittable.

### Task 6.1: Add PendingCombatHit creation to SweptMeleeHitboxSystem
**File:** `Assets/Scripts/Weapons/Systems/SweptMeleeHitboxSystem.cs`

Same pattern as ProjectileSystem. After existing DamageEvent creation (server-only), add:

```csharp
if (isServer)
{
    var combatHit = ecb.CreateEntity();
    ecb.AddComponent(combatHit, new PendingCombatHit
    {
        AttackerEntity = owner,
        TargetEntity = targetEntity,
        WeaponEntity = weaponEntity,
        HitPoint = hitPos,
        HitNormal = hit.SurfaceNormal,
        HitDistance = sweepDist,
        WasPhysicsHit = true,
        ResolverType = CombatResolverType.Hybrid,
        HitRegion = hitboxLookup.HasComponent(hitEntity)
            ? hitboxLookup[hitEntity].Region : HitboxRegion.Torso,
        HitboxMultiplier = damageMultiplier,
        DamagePreApplied = true,
        AttackDirection = sweepDir,
        WeaponData = new WeaponStats { BaseDamage = baseDamage, DamageType = DamageType.Physical }
    });
}
```

**Burst constraints:**
- `[BurstCompile]` — `ECB.CreateEntity` + `AddComponent` works in Burst (`PendingCombatHit` is blittable)
- Already server-only for `DamageEvent` creation (`if (isServer)`)
- `CombatResolverType` (enum), `WeaponStats` (struct with primitives + enum), `HitboxRegion` (byte enum) — all blittable

---

## Phase 7: Complete WeaponFireSystem Hitscan (Server)

**Risk:** Medium — new server-side logic in an existing system. HitConfirmationSystem (client-side) continues working independently for client prediction feedback.

### Task 7.1: Implement server-side hitscan hit logic
**File:** `Assets/Scripts/Weapons/Systems/WeaponFireSystem.cs`

Replace the placeholder comment with actual hit logic:

```csharp
if (config.UseHitscan && isServer)
{
    if (physicsWorld.CastRay(rayInput, out var hit))
    {
        Entity targetEntity = hit.Entity;
        float multiplier = 1.0f;
        HitboxRegion region = HitboxRegion.Torso;

        // Resolve hitbox
        if (hitboxLookup.HasComponent(hit.Entity))
        {
            var hitbox = hitboxLookup[hit.Entity];
            targetEntity = hitbox.OwnerEntity;
            multiplier = hitbox.DamageMultiplier;
            region = hitbox.Region;
        }

        float finalDamage = config.Damage * multiplier;

        // Create DamageEvent for health
        if (finalDamage > 0 && damageBufferLookup.HasBuffer(targetEntity))
            ecb.AppendToBuffer(targetEntity, new DamageEvent { ... });

        // Create PendingCombatHit for combat resolution
        var combatHit = ecb.CreateEntity();
        ecb.AddComponent(combatHit, new PendingCombatHit { ... });
    }
}
```

**Note:** `WeaponFireSystem` needs additional component lookups (`hitboxLookup`, `damageBufferLookup`) that don't currently exist in its `OnUpdate`. These must be added.

---

## Phase 8: Remove Debug Logging

**Risk:** None

### Task 8.1: Remove [DNP] debug logs from production code
| File | Log to Remove |
|------|---------------|
| `DamageEventVisualBridgeSystem.cs` | `Debug.Log($"[DNP] DmgEvtBridge...")` |
| `CombatResolutionSystem.cs` | `Debug.Log($"[DNP] Resolution...")` |
| `DamageApplicationSystem.cs` | `Debug.Log($"[DNP] DmgApply...")` |

---

## Files Modified Summary

| File | Change | Risk |
|------|--------|------|
| `CombatResolutionSystem.cs` | Add fields to `PendingCombatHit` + `CombatResultEvent`, register resolved targets | Low |
| `CombatContext.cs` | Add `HitRegion`, `HitboxMultiplier`, `AttackDirection` fields | Low |
| `CombatResult.cs` | Ensure `Flags` field is set by resolvers | Low |
| `PhysicsHitboxResolver.cs` | Add headshot flag, apply hitbox multiplier | Low |
| `StatBasedDirectResolver.cs` | Add headshot/crit enrichment | Low |
| `StatBasedRollResolver.cs` | Add headshot/backstab/weakness flags | Low |
| `HybridResolver.cs` | Add headshot/backstab/weakness flags | Low |
| `DamageVisualQueue.cs` | Add `CombatResolvedTargets` HashSet | Low |
| `DamageEventVisualBridgeSystem.cs` | Add suppression check | Low |
| `DamageApplicationSystem.cs` | Add `DamagePreApplied` guard | Low |
| `ProjectileSystem.cs` | Add `PendingCombatHit` creation (server-only) | Medium |
| `SweptMeleeHitboxSystem.cs` | Add `PendingCombatHit` creation (server-only, Burst) | Medium |
| `WeaponFireSystem.cs` | Implement server hitscan + `PendingCombatHit` | Medium |

---

## Implementation Order

Recommended order for incremental testing:

1. **Phase 1** (data structures) → compile check
2. **Phase 4** (DamageApplicationSystem guard) → compile check
3. **Phase 5** (duplicate suppression) → compile check
4. **Phase 2** (resolver enrichment) → compile check
5. **Phase 3** (reroute ProjectileSystem) → **TEST: shoot enemy, verify rich damage numbers**
6. **Phase 6** (reroute SweptMelee) → **TEST: melee enemy, verify crits/headshots**
7. **Phase 7** (complete hitscan) → **TEST: hitscan weapon, verify full pipeline**
8. **Phase 8** (cleanup debug logs)

---

## Verification Plan

1. **Compile check**: Project builds with no errors after each phase
2. **Host test**: Click Host → enter game → no console errors, no host-time errors
3. **Projectile test**: Throw grenade at enemy → damage numbers appear (normal hit, via DamageEvent path — should still work as before)
4. **Projectile hit test**: Shoot enemy with projectile weapon → rich damage number with HitType from resolver
5. **Headshot test**: Shoot enemy in head → "HEADSHOT" tag appears, larger/different color number
6. **Crit test**: Multiple hits → occasional crits with larger golden numbers
7. **Melee test**: Melee attack → rich damage numbers with headshot detection
8. **No double numbers**: Each hit shows exactly ONE damage number (not two from both pipelines)
9. **Health consistency**: Damage number displayed matches actual health subtracted
10. **No regression**: Grenade/hazard damage still shows simple damage numbers via `DamageEventVisualBridgeSystem`

---

## Edge Cases & Risks

| Risk | Mitigation |
|------|------------|
| **Burst compatibility (Phase 6)** | `PendingCombatHit`, `CombatResolverType` (enum), `WeaponStats` (blittable struct), `HitboxRegion` (byte enum) — all blittable. Verify no managed references. |
| **Double health subtraction** | Prevented by `DamagePreApplied` flag on `CombatResultEvent`. `DamageApplicationSystem` skips when true. |
| **Double visual display** | Prevented by `CombatResolvedTargets` HashSet. `DamageEventVisualBridgeSystem` skips entities in the set. |
| **Hazard + weapon same frame** | If a hazard and weapon hit the same entity in the same frame, the hazard visual may be suppressed. Rare and acceptable for Phase 1. Refine later with (entity, tick) pairs. |
| **Prediction mismatch** | `PendingCombatHit` is server-only. `DamageEvent` is predicted (client+server). Rich damage number appears after server confirmation, while client-side `HitConfirmationSystem` provides immediate feedback (hitmarker). Correct behavior for server-authoritative combat. |
