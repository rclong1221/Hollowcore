# EPIC 1.2: Limb Loss & Degradation

**Status**: Planning
**Epic**: EPIC 1 — Chassis & Limb System
**Dependencies**: EPIC 1.1 (ChassisState); Framework: Combat/DamageEvent, Targeting/Hitbox

---

## Overview

Limbs take damage independently of player HP. When a limb's integrity hits zero, it's destroyed — leaving an empty slot with gameplay penalties. Head destruction is always fatal. Missing arms restrict weapons. Missing legs restrict movement. The system routes regional damage from the existing hitbox/damage pipeline into limb integrity.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Chassis/Components/LimbDamageComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Maps a hitbox region to a chassis slot for limb-specific damage routing.
    /// Buffer on the chassis child entity.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct LimbDamageZone : IBufferElementData
    {
        /// <summary>Hitbox region identifier (matches HitboxElement.RegionId from framework).</summary>
        public int HitboxRegionId;

        /// <summary>Which chassis slot takes damage from this region.</summary>
        public ChassisSlot TargetSlot;

        /// <summary>Damage multiplier for this region (headshots = 2x, torso = 1x, limbs = 0.7x).</summary>
        public float DamageMultiplier;
    }

    /// <summary>
    /// Fired when a limb is destroyed. Enableable component, toggled for one frame.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct LimbLostEvent : IComponentData, IEnableableComponent
    {
        [GhostField] public ChassisSlot LostSlot;
        [GhostField] public int LimbDefinitionId; // What was lost
    }

    /// <summary>
    /// Gameplay penalties from missing limbs. Recalculated when chassis changes.
    /// On chassis child entity, read by movement/weapon systems.
    /// </summary>
    public struct ChassisPenaltyState : IComponentData
    {
        /// <summary>True if both arms are missing — cannot attack.</summary>
        public bool NoArms;
        /// <summary>True if exactly one arm is missing — one-handed weapons only.</summary>
        public bool OneArm;
        /// <summary>Which arm remains (if OneArm is true).</summary>
        public ChassisSlot RemainingArm;
        /// <summary>True if both legs are missing — crawling only.</summary>
        public bool NoLegs;
        /// <summary>True if exactly one leg is missing — half speed, no sprint.</summary>
        public bool OneLeg;
        /// <summary>Movement speed multiplier from leg state (1.0 = normal, 0.5 = one leg, 0.1 = crawling).</summary>
        public float MoveSpeedMultiplier;
        /// <summary>True if head is missing — should be impossible (instant death).</summary>
        public bool HeadDestroyed;
    }
}
```

---

## Systems

### LimbDamageRoutingSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbDamageRoutingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: DamageSystemGroup (PredictedFixedStepSimulation)
// UpdateBefore: DamageApplySystem
//
// Routes regional damage from DamageEvent buffer to limb integrity.
//
// For each entity with ChassisLink + DamageEvent buffer:
//   1. Resolve ChassisLink → ChassisState + LimbDamageZone buffer
//   2. For each DamageEvent:
//      a. Match event's HitboxRegionId against LimbDamageZone entries
//      b. If match found and slot has a limb equipped:
//         - Reduce LimbInstance.CurrentIntegrity by (damage * DamageMultiplier * limbArmorFactor)
//         - If CurrentIntegrity <= 0: queue limb destruction
//      c. Normal player HP damage still applies (this is ADDITIONAL limb damage)
//   3. Process limb destructions:
//      a. Set ChassisState slot to Entity.Null
//      b. Set DestroyedSlotsMask bit
//      c. Enable LimbLostEvent with slot info
//      d. If slot == Head: apply lethal damage to player HP (instant death)
//      e. Spawn dropped limb pickup at player position (if limb was Permanent rarity)
```

### ChassisPenaltySystem

```csharp
// File: Assets/Scripts/Chassis/Systems/ChassisPenaltySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: LimbDamageRoutingSystem
//
// Recalculates gameplay penalties when chassis state changes.
//
// For each ChassisState:
//   1. Count empty/destroyed arm slots → set NoArms, OneArm, RemainingArm
//   2. Count empty/destroyed leg slots → set NoLegs, OneLeg
//   3. Calculate MoveSpeedMultiplier:
//      - Both legs: 1.0
//      - One leg: 0.5 (no sprint)
//      - No legs: 0.1 (crawling)
//   4. Write ChassisPenaltyState
//   5. Push MoveSpeedMultiplier as modifier into EquippedStatsSystem
```

### LimbIntegrityRegenSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/LimbIntegrityRegenSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Slow passive regen for equipped limbs (out of combat only).
// For each LimbInstance where CurrentIntegrity < MaxIntegrity:
//   If player not in combat state (check CombatState component):
//     CurrentIntegrity += regenRate * deltaTime
//     Clamp to MaxIntegrity
```

---

## Integration with Framework Damage Pipeline

The framework has two damage pipelines:
1. **DamageEvent → DamageApplySystem** (low-level, Burst, server-only)
2. **PendingCombatHit → CombatResolutionSystem → CombatResultEvent** (combat resolution)

LimbDamageRoutingSystem hooks into pipeline #1 by running **before** DamageApplySystem in the same group. It reads DamageEvent entries, applies limb damage, but does NOT consume the events — DamageApplySystem still processes normal HP damage.

The HitboxRegionId comes from the existing hitbox system (HitboxElement buffer on the hitbox owner child entity). The mapping from region → chassis slot is defined in LimbDamageZone buffer, authored per prefab.

---

## Setup Guide

1. Add `LimbDamageZone` buffer to ChassisAuthoring baker (populate from hitbox region config)
2. Add `ChassisPenaltyState` to chassis child entity in baker
3. Add `LimbLostEvent` (baked disabled) to chassis child entity
4. Configure default zone mappings:
   - Head hitbox region → ChassisSlot.Head (multiplier 2.0)
   - Torso hitbox region → ChassisSlot.Torso (multiplier 1.0)
   - Left arm region → ChassisSlot.LeftArm (multiplier 0.7)
   - Right arm region → ChassisSlot.RightArm (multiplier 0.7)
   - Left leg region → ChassisSlot.LeftLeg (multiplier 0.7)
   - Right leg region → ChassisSlot.RightLeg (multiplier 0.7)
5. Movement systems must read ChassisPenaltyState.MoveSpeedMultiplier
6. Weapon systems must check ChassisPenaltyState.OneArm / NoArms for weapon restrictions

---

## Verification

- [ ] Damage to head hitbox reduces Head limb integrity
- [ ] Limb at 0 integrity is destroyed — slot becomes Entity.Null, DestroyedSlotsMask set
- [ ] Head destruction causes instant player death
- [ ] Missing one arm: only one-handed weapons usable
- [ ] Missing both arms: cannot attack
- [ ] Missing one leg: 50% move speed, no sprint
- [ ] Missing both legs: crawling movement (10% speed)
- [ ] LimbLostEvent fires for one frame on destruction
- [ ] Permanent-rarity destroyed limbs drop as pickups
- [ ] Normal HP damage still applies alongside limb damage

---

## Live Tuning

### Runtime-Tunable Parameters

The following values should be adjustable at runtime without restarting play mode. Read from `ChassisRuntimeConfig` singleton (defined in EPIC 1.1).

| Parameter | Default | Description |
|---|---|---|
| `LimbDamageMultiplier` | 1.0 | Global multiplier on all limb integrity damage |
| `LimbRegenRate` | 2.0 | HP/sec out-of-combat limb integrity regen |
| `HeadDamageMultiplier` | 2.0 | Zone multiplier override for head region |
| `LimbZoneDamageMultiplier` | 0.7 | Zone multiplier override for arm/leg regions |
| `MoveSpeedOneLeg` | 0.5 | Speed multiplier when one leg missing |
| `MoveSpeedNoLegs` | 0.1 | Speed multiplier when both legs missing (crawling) |

Additional singleton for penalty tuning:

```csharp
// File: Assets/Scripts/Chassis/Components/LimbDamageConfig.cs
using Unity.Entities;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Runtime-tunable damage routing and penalty config. Singleton.
    /// </summary>
    public struct LimbDamageConfig : IComponentData
    {
        public float HeadDamageMultiplier;
        public float TorsoDamageMultiplier;
        public float LimbZoneDamageMultiplier;
        public float MoveSpeedOneLeg;
        public float MoveSpeedNoLegs;
        public float LimbRegenRate;
        public float LimbRegenCombatCooldown; // Seconds after last damage before regen starts
    }
}
```

**Propagation**: `LimbDamageRoutingSystem` and `ChassisPenaltySystem` read the singleton each frame (cheap — single component read). No dirty flag needed for these hot-path values.

---

## Debug Visualization

### Limb Integrity Debug HUD

```
// Toggle: console command `chassis.damage` or key binding
//
// Draws per-slot:
// 1. Integrity bar (current/max) with numeric value
// 2. Flash red on damage event (pulse for 0.3s when LimbDamageRoutingSystem routes damage)
// 3. Damage-per-second ticker per slot (rolling 3s average)
// 4. Zone mapping visualization: colored overlay on player model showing which
//    hitbox regions map to which chassis slots (head=red, torso=yellow, arms=blue, legs=green)
//
// Implementation: LimbDamageDebugSystem (ClientSimulation | LocalSimulation, PresentationSystemGroup)
// Reads: LimbInstance (integrity), LimbDamageZone buffer, ChassisPenaltyState
```

### Damage Routing Trace

```
// Console command: `chassis.trace on`
// Logs every damage event routing decision:
//   "[LimbDamage] DamageEvent(src=Entity:42, dmg=35.0) → Region:Head → Slot:Head → Integrity 100→65 (mult=2.0)"
//   "[LimbDamage] DamageEvent(src=Entity:42, dmg=35.0) → Region:LeftArm → Slot:LeftArm → DESTROYED (integrity 12→0)"
// Useful for verifying hitbox-to-slot mapping is correct.
```

---

## Simulation & Testing

### Limb Damage Distribution Test

```
// Test: LimbDamageDistributionTest
// Setup: Player with full chassis (6 limbs, all 100 integrity)
// Input: 1000 random DamageEvents with uniform random HitboxRegionId distribution
// Seed: deterministic (42)
// Expected:
//   - Head receives ~16.7% of hits at 2.0x multiplier → highest total damage
//   - Torso receives ~16.7% at 1.0x
//   - Each arm/leg receives ~16.7% at 0.7x
//   - Head should be first limb destroyed
//   - Verify limb destruction count matches expected given uniform damage (25 dmg per hit)
```

### Penalty State Machine Test

```
// Test: ChassisPenaltyStateTest
// Verify all penalty state transitions:
//   - Destroy LeftLeg → OneLeg=true, MoveSpeedMultiplier=0.5
//   - Destroy RightLeg → NoLegs=true, MoveSpeedMultiplier=0.1
//   - Re-equip LeftLeg → OneLeg=true, MoveSpeedMultiplier=0.5
//   - Re-equip RightLeg → all clear, MoveSpeedMultiplier=1.0
//   - Destroy Head → HeadDestroyed=true, player death triggered
```

### Performance Test

```
// Test: LimbDamageRoutingPerformanceTest
// Setup: 100 players, each with 6 limbs, 6 LimbDamageZone entries
// Input: 50 DamageEvents per player per frame (stress test)
// Target: LimbDamageRoutingSystem completes in < 0.5ms
// Target: ChassisPenaltySystem completes in < 0.1ms
```
