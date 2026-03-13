# EPIC 2.3: Revival System

**Status**: Planning
**Epic**: EPIC 2 — Soul Chip, Death & Revival
**Dependencies**: EPIC 2.1 (SoulChip), EPIC 2.2 (Body Persistence); EPIC 1.1 (ChassisState); Framework: Interaction/, Persistence/, Party/

---

## Overview

When a player dies, revival is not a menu button — it is a spatial search problem. The system spawns revival bodies at locations throughout the expedition based on tier, distance, and danger. The first death is forgiving: a junky body nearby. Each subsequent death pushes viable bodies further away, deeper into hostile territory, or back into previous districts. Solo players rely on drone insurance, revival terminals, or rare continuity caches. In co-op, a teammate physically carries the soul chip to a body and channels the revival. The quality of the body determines your stats, available limb slots, and starting loadout for the remainder of the run.

---

## Component Definitions

```csharp
// File: Assets/Scripts/SoulChip/Components/RevivalComponents.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Quality tier for revival bodies. Determines stats, limb slots, and spawn danger.
    /// </summary>
    public enum RevivalBodyTier : byte
    {
        /// <summary>Junky frame, possibly missing limbs, safe-ish location. Free.</summary>
        Cheap = 0,
        /// <summary>Functional body, standard limbs, contested territory. Moderate cost.</summary>
        Mid = 1,
        /// <summary>Military-grade or district-specialized, deep hostile zone. Expensive.</summary>
        Premium = 2,
    }

    /// <summary>
    /// Placed on a revival body entity in the world. Represents a body the player
    /// can transfer their soul chip into after death.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct RevivalBodyState : IComponentData
    {
        /// <summary>Quality tier of this body.</summary>
        public RevivalBodyTier Tier;

        /// <summary>Definition SO id for body stats and limb configuration.</summary>
        public int BodyDefinitionId;

        /// <summary>District where this body is located.</summary>
        public int DistrictId;

        /// <summary>Zone within the district.</summary>
        public int ZoneId;

        /// <summary>World position of the revival body.</summary>
        public float3 WorldPosition;

        /// <summary>Danger rating of the area (0.0 = safe, 1.0 = extreme).</summary>
        public float LocationDanger;

        /// <summary>Currency cost to use this body (0 for Cheap tier).</summary>
        public int Cost;

        /// <summary>Whether this body has been claimed by a player.</summary>
        public bool IsClaimed;

        /// <summary>SoulId of the player this body was spawned for (-1 = available to any).</summary>
        public int SpawnedForSoulId;
    }

    /// <summary>
    /// Tracks drone insurance state on the player entity.
    /// Drone insurance auto-recovers the chip to the nearest Tier 1 body.
    /// Limited charges per expedition.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DroneInsuranceState : IComponentData
    {
        /// <summary>Remaining drone recovery charges this expedition.</summary>
        [GhostField] public int ChargesRemaining;

        /// <summary>Max charges purchased/earned for this expedition.</summary>
        [GhostField] public int MaxCharges;

        /// <summary>Whether drone recovery is currently in progress.</summary>
        [GhostField] public bool RecoveryInProgress;

        /// <summary>Target revival body entity for active recovery.</summary>
        public Entity TargetBody;
    }

    /// <summary>
    /// Placed on a teammate entity when they are carrying another player's soul chip.
    /// Imposes movement penalties and exposes a revival interaction when near a body.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ChipCarrier : IComponentData
    {
        /// <summary>SoulId of the chip being carried.</summary>
        [GhostField] public int CarriedSoulId;

        /// <summary>Movement speed multiplier while carrying (e.g. 0.6 = 40% slower).</summary>
        [GhostField] public float SpeedMultiplier;

        /// <summary>Whether the carrier is currently channeling a revival.</summary>
        [GhostField] public bool IsChanneling;

        /// <summary>Elapsed channel time (seconds). Revival completes at threshold.</summary>
        [GhostField] public float ChannelProgress;
    }

    /// <summary>
    /// Enableable tag on the player entity. Enabled when revival selection is active
    /// (player is dead and choosing a body). Prevents other systems from processing
    /// the player as alive.
    /// </summary>
    public struct RevivalSelectionActive : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Transient request entity. Created when player selects a revival body.
    /// Consumed by SoulChipTransferSystem (EPIC 2.1).
    /// </summary>
    public struct RevivalRequest : IComponentData
    {
        public int SoulId;
        public Entity TargetBody;
    }
}
```

---

## ScriptableObject Definition

```csharp
// File: Assets/Scripts/SoulChip/Definitions/RevivalBodyDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Defines a revival body type. Referenced by RevivalBodySpawnSystem when
    /// populating the expedition with revival options.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollowcore/Revival/Body Definition")]
    public class RevivalBodyDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int DefinitionId;
        public string BodyName;
        public RevivalBodyTier Tier;

        [Header("Chassis")]
        [Tooltip("Number of usable limb slots (Cheap: 3-4, Mid: 5, Premium: 6)")]
        public int AvailableLimbSlots = 5;
        [Tooltip("Which chassis slots are functional. Missing slots = missing limbs.")]
        public ChassisSlot[] FunctionalSlots;

        [Header("Base Stats")]
        [Tooltip("Multiplier applied to base health (1.0 = standard)")]
        public float HealthMultiplier = 1.0f;
        [Tooltip("Multiplier applied to base movement speed")]
        public float SpeedMultiplier = 1.0f;
        [Tooltip("Multiplier applied to all combat stats")]
        public float CombatStatMultiplier = 1.0f;

        [Header("Location")]
        [Tooltip("Minimum danger rating of spawn area (0.0-1.0)")]
        public float MinLocationDanger = 0.0f;
        [Tooltip("Maximum danger rating of spawn area")]
        public float MaxLocationDanger = 0.5f;

        [Header("Economy")]
        public int BaseCost;
        [Tooltip("Prefab for the revival body entity in-world")]
        public GameObject BodyPrefab;
    }
}
```

---

## Revival Body Tiers

```
// Tier definitions (from GDD 13.3):
//
// CHEAP (Tier 0):
//   - 3-4 limb slots, may be missing arm or leg
//   - HealthMultiplier: 0.7, SpeedMultiplier: 0.85, CombatStatMultiplier: 0.75
//   - Spawns in safe zones, near player hubs, cleared areas
//   - Cost: 0 (free)
//   - Availability: always at least 1 within current district on first death
//
// MID (Tier 1):
//   - 5 limb slots, all functional
//   - HealthMultiplier: 1.0, SpeedMultiplier: 1.0, CombatStatMultiplier: 1.0
//   - Spawns in contested territory, patrol routes, near objectives
//   - Cost: moderate (scales with expedition depth)
//   - Availability: 1-2 per district, may require traversal through enemies
//
// PREMIUM (Tier 2):
//   - 6 limb slots, district-specialized augments pre-installed
//   - HealthMultiplier: 1.15, SpeedMultiplier: 1.1, CombatStatMultiplier: 1.15
//   - Spawns deep in hostile zones, near Front boundary, behind mini-bosses
//   - Cost: expensive (significant currency investment)
//   - Availability: 0-1 per district, always in dangerous territory
```

---

## Revival Location Logic

```
// File: Assets/Scripts/SoulChip/Systems/RevivalBodySpawnSystem.cs
// Deterministic from expedition seed + death count:
//
// Death 1 (first death in expedition):
//   - 1x Cheap body: same district, nearby (within 100m), safe zone
//   - 1x Mid body: same district, moderate distance (200-400m), contested
//   - 0-1x Premium body: same district, far (500m+), hostile
//
// Death 2:
//   - Cheap body: same district but further out (200-300m)
//   - Mid body: may be in PREVIOUS district (if applicable)
//   - Previous Cheap body location: now empty (used up)
//
// Death 3+:
//   - Cheap bodies spawn in previous districts only
//   - Mid bodies may require backtracking 1-2 districts
//   - Premium bodies only in current district (if any remain)
//   - Spawned bodies from prior deaths that were NOT selected are removed
//
// Cross-district revival:
//   - Player must gate-travel back to the district containing the body
//   - Body persists in district save state via RevivalBodyPersistenceEntry
//   - District enemies remain (not cleared) — backtracking is dangerous
```

---

## Systems

### RevivalBodySpawnSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/RevivalBodySpawnSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: SoulChipEjectionSystem
//
// Triggered when a player dies (SoulChipState.IsEmbodied transitions to false):
//   1. Read expedition seed + player's DeathCounter (EPIC 2.5)
//   2. Determine available spawn points from district zone graph (EPIC 4)
//   3. For each tier, select spawn location based on:
//      - Death count (more deaths = further/more dangerous locations)
//      - District topology (safe zones, contested, hostile)
//      - Previously used revival locations (excluded)
//   4. Instantiate RevivalBodyState entities at selected positions
//   5. Set SpawnedForSoulId to the dead player's SoulId
//   6. Enable RevivalSelectionActive on the dead player entity
//   7. Notify RevivalSelectionPanel (UI) with available body list
```

### RevivalSelectionSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/RevivalSelectionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Processes RevivalRequest entities:
//   1. Validate request: SoulId matches, TargetBody exists and !IsClaimed
//   2. Check cost: player has sufficient currency (deduct on confirm)
//   3. Mark RevivalBodyState.IsClaimed = true
//   4. Solo mode: create RevivalRequest → consumed by SoulChipTransferSystem
//   5. Co-op mode: validate ChipCarrier is near the body → create RevivalRequest
//   6. On successful transfer (from SoulChipTransferSystem callback):
//      - Apply body stats from RevivalBodyDefinitionSO
//      - Set ChassisState to body's FunctionalSlots
//      - Spawn player at RevivalBodyState.WorldPosition
//      - Destroy the RevivalBodyState entity
//      - Disable RevivalSelectionActive on the player
//   7. Destroy unclaimed RevivalBodyState entities (cleanup after selection)
```

### DroneRecoverySystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/DroneRecoverySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: SoulChipEjectionSystem
//
// Solo automatic revival path (drone insurance):
//   1. On player death, check DroneInsuranceState.ChargesRemaining > 0
//   2. If charges available:
//      a. Decrement ChargesRemaining
//      b. Set RecoveryInProgress = true
//      c. Find nearest Cheap-tier RevivalBodyState (prefer same district)
//      d. Set TargetBody to that entity
//      e. After recovery delay (configurable, ~3-5 seconds):
//         - Create RevivalRequest for the target body
//         - Set RecoveryInProgress = false
//      f. Skip RevivalSelectionActive — player auto-revives
//   3. If no charges: fall through to normal revival selection flow
//
// Drone recovery is interruptible:
//   - If the target body is destroyed (reanimation, Front advancement)
//     before recovery completes, cancel and fall back to manual selection
```

### CoopCarrySystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/CoopCarrySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Manages the physical chip-carry mechanic in co-op:
//
// Pickup phase:
//   1. Teammate interacts with EjectedSoulChip entity (EPIC 2.1)
//   2. Add ChipCarrier to teammate: CarriedSoulId, SpeedMultiplier = 0.6
//   3. Destroy EjectedSoulChip world entity
//   4. Attach visual: downed player model on carrier's back (presentation)
//
// Carry phase:
//   5. Apply movement penalty via SpeedMultiplier → fed into movement system
//   6. Carrier cannot sprint, dodge, or use two-handed weapons
//   7. Carrier can still shoot one-handed weapons and use abilities
//   8. If carrier dies while carrying:
//      a. Drop carried chip as new EjectedSoulChip at carrier death location
//      b. Remove ChipCarrier from (now dead) carrier
//
// Revival phase:
//   9. When carrier approaches unclaimed RevivalBodyState:
//      a. Show interaction prompt: "Revive [PlayerName]"
//      b. On interact: begin channel (IsChanneling = true)
//      c. Channel duration: 4 seconds (configurable), interruptible by damage
//      d. On channel complete: create RevivalRequest, remove ChipCarrier
//      e. On interrupt: reset ChannelProgress, remain carrying
```

---

## Solo Revival Methods

```
// DRONE INSURANCE:
//   - Purchased before expedition or found as consumable
//   - Limited charges (default: 2 per expedition)
//   - Auto-selects nearest Cheap body, no player input needed
//   - Recovery takes 3-5 seconds (visual: drone flying chip to body)
//   - Does NOT skip chip degradation — TransferCount still increments
//
// REVIVAL TERMINALS (fixed locations):
//   - Placed at specific points in each district (body shops, clinics)
//   - Must be discovered/activated BEFORE death to be usable
//   - Activated terminal stores a Cheap body permanently
//   - On death near activated terminal: appears as revival option
//   - Terminal body is free but Cheap tier
//
// CONTINUITY CACHE (rare):
//   - Found as rare loot or purchased at extreme cost
//   - Pre-placed backup body at current location
//   - On death: cache activates, spawning a Mid-tier body exactly where placed
//   - Single use, consumed on death regardless of whether selected
//   - The premium "save point" — plan your deaths
```

---

## Setup Guide

1. Create `RevivalBodyDefinitionSO` assets for each tier (Cheap, Mid, Premium) per district theme
2. Add `DroneInsuranceState` to player entity baker (default: ChargesRemaining=0, purchased in hub)
3. Add `RevivalSelectionActive` (IEnableableComponent) to player entity baker, baked disabled
4. Create revival body prefab: glowing pod/frame with interactable trigger, tier-colored VFX
5. Place revival terminal prefabs at designated locations in district subscenes
6. Configure `CoopCarrySystem` channel duration and speed penalty in gameplay settings
7. Hook `RevivalBodySpawnSystem` to district zone graph for spawn point selection (EPIC 4)
8. Register revival body persistence entries with district save module
9. Create drone recovery VFX: small drone carrying glowing chip entity

---

## Verification

- [ ] Player death spawns correct number of revival bodies per tier
- [ ] First death: at least 1 Cheap body nearby in same district
- [ ] Second death: Cheap body further away, previous location empty
- [ ] Third+ death: bodies may appear in previous districts
- [ ] RevivalBodyDefinitionSO stats correctly applied to new body
- [ ] Cheap body: reduced limb slots, lower stat multipliers
- [ ] Premium body: bonus slots, higher stats, expensive
- [ ] Drone insurance auto-revives to nearest Cheap body
- [ ] Drone insurance charges decrement correctly
- [ ] Drone recovery cancels if target body is destroyed
- [ ] Revival terminal: must be activated before death to appear as option
- [ ] Continuity cache: spawns Mid body at placed location on death
- [ ] Co-op: teammate picks up chip, gets ChipCarrier component
- [ ] Co-op: carrier has 40% movement penalty, no sprint/dodge
- [ ] Co-op: carrier channels revival at body (4s, interruptible)
- [ ] Co-op: carrier death drops chip as new EjectedSoulChip
- [ ] Revival selection UI shows all available bodies with tier/distance/danger
- [ ] Currency deducted on Mid/Premium body selection
- [ ] Unclaimed bodies cleaned up after revival completes
- [ ] Cross-district bodies persist in save data

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/SoulChip/Definitions/RevivalBodyBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Burst-compatible baked data from RevivalBodyDefinitionSO.
    /// One BlobAssetReference per revival body tier/variant.
    /// </summary>
    public struct RevivalBodyBlob
    {
        public int DefinitionId;
        public byte Tier; // RevivalBodyTier cast
        public int AvailableLimbSlots;
        public float HealthMultiplier;
        public float SpeedMultiplier;
        public float CombatStatMultiplier;
        public float MinLocationDanger;
        public float MaxLocationDanger;
        public int BaseCost;
        public BlobArray<int> FunctionalSlotIds; // ChassisSlot values
        public BlobString BodyName;
    }
}

// File: Assets/Scripts/SoulChip/Definitions/RevivalBodyDefinitionSO.cs (addition)
// public BlobAssetReference<RevivalBodyBlob> BakeToBlob(BlobBuilder builder)
// {
//     ref var root = ref builder.ConstructRoot<RevivalBodyBlob>();
//     root.DefinitionId = DefinitionId;
//     root.Tier = (byte)Tier;
//     root.AvailableLimbSlots = AvailableLimbSlots;
//     root.HealthMultiplier = HealthMultiplier;
//     root.SpeedMultiplier = SpeedMultiplier;
//     root.CombatStatMultiplier = CombatStatMultiplier;
//     root.MinLocationDanger = MinLocationDanger;
//     root.MaxLocationDanger = MaxLocationDanger;
//     root.BaseCost = BaseCost;
//     var slots = builder.Allocate(ref root.FunctionalSlotIds, FunctionalSlots.Length);
//     for (int i = 0; i < FunctionalSlots.Length; i++)
//         slots[i] = (int)FunctionalSlots[i];
//     builder.AllocateString(ref root.BodyName, BodyName);
//     return builder.CreateBlobAssetReference<RevivalBodyBlob>(Allocator.Persistent);
// }
```

Baker registers all `RevivalBodyDefinitionSO` assets via `Resources.LoadAll` at bake time, creating a singleton entity with a `BlobArray<RevivalBodyBlob>` for Burst-compatible lookup by `RevivalBodySpawnSystem`.

---

## Validation

```csharp
// File: Assets/Editor/Validation/RevivalBodyValidation.cs
// OnValidate() rules for RevivalBodyDefinitionSO:
//
// - DefinitionId must be unique across all RevivalBodyDefinitionSO assets
// - BodyName must not be null or empty
// - AvailableLimbSlots must be in [1, 6]
// - FunctionalSlots must not be null, length must equal AvailableLimbSlots
// - FunctionalSlots must not contain duplicate ChassisSlot values
// - HealthMultiplier must be in [0.1, 5.0]
// - SpeedMultiplier must be in [0.1, 3.0]
// - CombatStatMultiplier must be in [0.1, 3.0]
// - MinLocationDanger must be < MaxLocationDanger
// - MinLocationDanger and MaxLocationDanger must be in [0.0, 1.0]
// - BaseCost must be >= 0
// - BodyPrefab must not be null
// - Tier must match expected stat ranges:
//     Cheap: HealthMultiplier <= 1.0
//     Premium: HealthMultiplier >= 1.0
//
// Build-time scan: find all RevivalBodyDefinitionSO in project,
//   verify no duplicate DefinitionId values,
//   verify at least 1 asset per tier (Cheap, Mid, Premium)
```

---

## Editor Tooling

**Revival Body Inspector**:
- Custom PropertyDrawer for `RevivalBodyDefinitionSO`: color-coded header by tier (grey/blue/gold)
- Stat preview: bar graph showing Health/Speed/Combat multipliers relative to baseline (1.0)
- Slot visualization: 6-slot chassis diagram with functional slots highlighted

---

## Simulation & Testing

**Revival Placement Distribution**:
- Automated test: for expedition seeds 0..99 and death counts 1..5, record revival body tier distribution per death count
- Expected: death 1 always has >= 1 Cheap body in same district
- Expected: death 3+ has 0 Cheap bodies in current district for >= 80% of seeds
- Expected: Premium body availability decreases monotonically with death count

**Economy Simulation**:
- Monte Carlo (N=1000): simulate expeditions with random death patterns (1-6 deaths), track total currency spent on revival bodies
- Balance target: average revival cost per expedition should be 15-25% of expected currency income
