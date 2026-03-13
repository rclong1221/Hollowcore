# EPIC 1.4: Enemy Limb Theft (Rip System)

**Status**: Planning
**Epic**: EPIC 1 — Chassis & Limb System
**Dependencies**: EPIC 1.1, 1.3; Framework: Combat/, AI/, Interaction/

---

## Overview

The Cyborg Justice mechanic. Players can rip a limb off a staggered or downed enemy during a time window. The rip is a long, interruptible animation that leaves the player fully exposed. Ripped limbs have quality tiers based on enemy type: common enemies yield temporary limbs (30-60s), elites yield district-life limbs, bosses yield permanent legendary limbs. Some ripped limbs carry curses.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Chassis/Components/RipComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Marks an enemy as having a rippable limb. Baked on enemy prefab.
    /// Defines WHAT can be ripped and the quality tier.
    /// </summary>
    public struct RippableLimb : IComponentData
    {
        public int LimbDefinitionId;
        public ChassisSlot SlotType;
        public LimbDurability ResultDurability;  // Temporary, DistrictLife, or Permanent
        public float TemporaryDuration;          // Only for Temporary (30-60s)
        public bool CanBeCursed;                 // Some limbs carry negative effects
        public int CurseDefinitionId;            // 0 = no curse
    }

    /// <summary>
    /// Enableable: toggled ON when enemy enters stagger/downed state.
    /// While enabled, player can initiate rip interaction.
    /// Toggled OFF when stagger window expires or enemy recovers.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct RipTarget : IComponentData, IEnableableComponent
    {
        /// <summary>Time remaining in rip window (seconds).</summary>
        [GhostField(Quantization = 100)]
        public float WindowRemaining;
    }

    /// <summary>
    /// Active rip in progress. On the PLAYER entity.
    /// Created when player begins rip, destroyed on completion/interruption.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct RipInProgress : IComponentData, IEnableableComponent
    {
        [GhostField] public Entity TargetEnemy;
        [GhostField(Quantization = 100)] public float Progress;      // 0 → 1
        [GhostField(Quantization = 100)] public float RequiredTime;  // Total time to complete rip
    }

    /// <summary>
    /// Curse applied from a ripped limb. On the limb entity, checked on equip.
    /// </summary>
    public struct LimbCurse : IComponentData
    {
        public int CurseDefinitionId;
        public float DurationOrPermanent; // -1 = permanent until limb removed
    }
}
```

---

## Systems

### RipWindowSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/RipWindowSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Monitors enemy stagger/downed state → enables RipTarget for a window.
//
// For each enemy with RippableLimb + RipTarget (disabled):
//   If enemy is in stagger state (check AIState or StaggerComponent):
//     Enable RipTarget, set WindowRemaining (e.g., 3-5 seconds)
//
// For each enemy with RipTarget (enabled):
//   Decrement WindowRemaining by deltaTime
//   If WindowRemaining <= 0 OR enemy recovered from stagger:
//     Disable RipTarget
```

### RipExecutionSystem

```csharp
// File: Assets/Scripts/Chassis/Systems/RipExecutionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Manages the rip interaction from start to completion.
//
// INITIATION (player presses rip input near RipTarget-enabled enemy):
//   1. Verify: enemy has RipTarget enabled, player in range, player not already ripping
//   2. Enable RipInProgress on player: TargetEnemy, Progress=0, RequiredTime (2-4s based on enemy tier)
//   3. Lock player movement and combat (set animation state to ripping)
//   4. Play rip start animation
//
// PROGRESS (each frame while RipInProgress enabled):
//   1. Increment Progress by deltaTime / RequiredTime
//   2. If player takes damage (DamageEvent buffer non-empty):
//      a. CANCEL: disable RipInProgress, unlock player, play cancel animation
//      b. RipTarget on enemy remains enabled if window hasn't expired
//   3. If target enemy dies or RipTarget disabled:
//      a. CANCEL: disable RipInProgress, unlock player
//   4. If Progress >= 1.0:
//      a. SUCCESS: spawn LimbPickup from RippableLimb definition
//      b. Set LimbPickup.IntegrityPercent = 1.0 (fresh rip)
//      c. Auto-equip into matching slot (or show slot selection if ambiguous)
//      d. Kill the enemy (rip is lethal to the limb source)
//      e. Disable RipInProgress, unlock player
//      f. Play rip success animation + VFX
//
// RequiredTime by tier:
//   Common enemy: 2.0s
//   Elite enemy: 3.0s
//   Boss/mini-boss: 4.0s
```

---

## Rip Quality Tiers (from GDD §3.3)

| Enemy Type | Ripped Limb Duration | Quality | Examples |
|---|---|---|---|
| Common | Temporary (30-60s) | Junk-Common | Drowner's arm as makeshift weapon |
| Elite | District life | Uncommon-Rare | Seraphim drone wing (glide 30s) |
| Boss / Mini-boss | Permanent | Epic-Legendary | Gravity Wraith's phase-limb |

---

## Curse System

Some ripped limbs carry negative effects:

```csharp
// File: Assets/Scripts/Chassis/Definitions/LimbCurseDefinitionSO.cs
// Examples:
// - Feedback Loop's neural arm: contagious recursion (DOT to nearby allies)
// - Patient Zero's Children limb: plague strain (contamination meter ticks up)
// - Converted's chrome arm: faith-lock (can't use in non-Cathedral zones)
//
// Curses are shown in limb preview UI before equipping.
// Player can choose to equip cursed limb or discard it.
```

---

## Setup Guide

1. Add `RippableLimb` + `RipTarget` (baked disabled) to enemy prefabs that support ripping
   - Not all enemies: humanoid/mechanical with distinct limbs only (GDD: "Slag Walkers yes, Adware Swarms no")
2. Configure `RippableLimb` per enemy type: which limb, quality tier, curse chance
3. Add rip input binding to PlayerInputState (e.g., `RipInput` bool, mapped to interact key when near RipTarget)
4. Create rip animations: start, progress loop, success, cancel
5. Add rip VFX: sparks, hydraulic fluid, limb separation
6. Create LimbCurseDefinitionSO assets for cursed limb variants
7. UI: rip progress bar, rip prompt when near valid target

---

## Verification

- [ ] RipTarget enables when enemy enters stagger state
- [ ] RipTarget disables when window expires or enemy recovers
- [ ] Player can initiate rip on enabled RipTarget within range
- [ ] Rip progress bar advances over RequiredTime
- [ ] Taking damage during rip cancels it
- [ ] Successful rip spawns correct LimbPickup based on enemy's RippableLimb
- [ ] Common enemy rip → Temporary limb (expires after duration)
- [ ] Elite enemy rip → DistrictLife limb
- [ ] Boss rip → Permanent Legendary limb
- [ ] Cursed limbs show warning in preview UI
- [ ] Rip kills the target enemy on success
- [ ] Co-op: teammates can defend the ripper during animation

---

## BlobAsset Pipeline

`LimbCurseDefinitionSO` needs Burst-accessible data for curse application during rip completion.

### Blob Struct

```csharp
// File: Assets/Scripts/Chassis/Definitions/LimbCurseBlob.cs
using Unity.Entities;

namespace Hollowcore.Chassis.Definitions
{
    public struct LimbCurseBlob
    {
        public int CurseDefinitionId;
        public BlobString DisplayName;
        public BlobString Description;
        public float Duration;           // -1 = permanent until limb removed
        public byte CurseType;           // DOT, debuff, zone restriction, etc.
        public float EffectMagnitude;    // Damage per tick, stat reduction %, etc.
    }

    public struct LimbCurseDatabase
    {
        public BlobArray<LimbCurseBlob> Curses;
    }

    public struct LimbCurseDatabaseReference : IComponentData
    {
        public BlobAssetReference<LimbCurseDatabase> Value;
    }
}
```

Bootstrap follows the same pattern as `LimbDatabaseBootstrapSystem` (EPIC 1.1) — load all `LimbCurseDefinitionSO`, bake to blob, create singleton.

---

## Live Tuning

### Rip Runtime Config

```csharp
// File: Assets/Scripts/Chassis/Components/RipRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Chassis
{
    /// <summary>
    /// Runtime-tunable rip parameters. Singleton.
    /// </summary>
    public struct RipRuntimeConfig : IComponentData
    {
        /// <summary>Stagger window duration for common/elite/boss enemies (seconds).</summary>
        public float RipWindowCommon;      // Default: 4.0
        public float RipWindowElite;       // Default: 3.0
        public float RipWindowBoss;        // Default: 5.0

        /// <summary>Time required to complete rip per enemy tier (seconds).</summary>
        public float RipTimeCommon;        // Default: 2.0
        public float RipTimeElite;         // Default: 3.0
        public float RipTimeBoss;          // Default: 4.0

        /// <summary>Max distance from enemy to initiate rip.</summary>
        public float RipInteractRange;     // Default: 2.5

        /// <summary>Temporary limb duration multiplier (1.0 = as authored).</summary>
        public float TempLimbDurationMult; // Default: 1.0

        /// <summary>Probability a cursed-eligible limb actually has a curse (0-1).</summary>
        public float CurseChance;          // Default: 0.3
    }
}
```

| Parameter | Default | Range | Impact |
|---|---|---|---|
| `RipWindowCommon` | 4.0s | 2-8s | How forgiving the rip window is on weak enemies |
| `RipTimeCommon` | 2.0s | 1-5s | Risk/reward: faster rip = less exposure |
| `CurseChance` | 0.3 | 0-1 | Frequency of negative rip outcomes |
| `TempLimbDurationMult` | 1.0 | 0.5-3.0 | How long temporary ripped limbs last |

**Propagation**: `RipWindowSystem` and `RipExecutionSystem` read singleton each frame. No dirty flag needed.

---

## Debug Visualization

### Rip Target Indicators

```
// Toggle: console command `chassis.rip` or key binding
//
// Draws:
// 1. Green circle around enemies with RipTarget enabled (rippable now)
// 2. Yellow circle around enemies with RippableLimb but RipTarget disabled (can be staggered)
// 3. Window timer countdown text above rippable enemies
// 4. Line from player to nearest rip target when in range
// 5. During rip: progress bar + "EXPOSED" warning text + damage interrupt flash
//
// Implementation: RipDebugOverlaySystem (ClientSimulation | LocalSimulation, PresentationSystemGroup)
```

### Rip State Machine Trace

```
// Console command: `chassis.riptrace on`
// Logs state transitions:
//   "[Rip] Enemy:42 → STAGGERED, RipTarget ENABLED (window=4.0s)"
//   "[Rip] Player:1 → RIP START on Enemy:42 (requiredTime=2.0s)"
//   "[Rip] Player:1 → RIP INTERRUPTED (took 15.0 damage)"
//   "[Rip] Player:1 → RIP SUCCESS on Enemy:42 → LimbPickup spawned (BurnArm, Temporary 45s)"
//   "[Rip] Enemy:42 → RipTarget EXPIRED (window=0.0s)"
```

---

## Simulation & Testing

### Rip Success Rate Simulation

```
// Test: RipSuccessRateSimulation
// Monte Carlo: 1000 rip attempts per enemy tier (seeds 0-999)
// Model: player initiates rip, enemies in vicinity attack at configurable rate
// Input: 2 nearby enemies attacking every 1.5s (common combat scenario)
// Measure per tier:
//   - Success rate (target: Common ~70%, Elite ~45%, Boss ~30%)
//   - Average interruption point (how far through the rip before cancel)
//   - Average damage taken during successful rips
// Purpose: validate rip timing creates appropriate risk/reward per tier
```

### Curse Distribution Test

```
// Test: CurseDistributionTest
// Setup: 10000 rips from curse-eligible enemies (CurseChance=0.3)
// Verify:
//   - ~30% of rips produce cursed limbs (within 2% tolerance)
//   - Curse type distribution matches LimbCurseDefinitionSO weights
//   - No curse appears on non-curse-eligible enemies (CanBeCursed=false)
//   - Deterministic seed produces identical curse sequence
```

### Rip Timing Performance Test

```
// Test: RipSystemPerformanceTest
// Setup: 50 players, each with RipInProgress active, 200 enemies with RipTarget
// Target: RipWindowSystem + RipExecutionSystem combined < 0.2ms
```
