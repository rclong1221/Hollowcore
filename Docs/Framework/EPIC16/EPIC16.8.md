# EPIC 16.8: Player Resource Framework

**Status:** IMPLEMENTED
**Priority:** High (Ability System Foundation)
**Dependencies:**
- `PlayerStamina` IComponentData + `PlayerStaminaSystem` (existing -- `Player.Components`)
- `AbilityDefinition` IBufferElementData (existing -- `DIG.AI.Components`, EPIC 15.32)
- `AbilityDefinition` IBufferElementData (existing -- `DIG.Player.Abilities`, player ability buffer)
- `AbilityExecutionSystem` / `AbilitySelectionSystem` (existing -- `DIG.AI.Systems`, EPIC 15.32)
- `AbilityLifecycleSystem` / `AbilityTriggerSystem` (existing -- `DIG.Player.Systems.Abilities`)
- `ChannelAction` IComponentData with `ResourcePerTick` field (existing -- `DIG.Weapons`, stubbed)
- `CombatUIBridgeSystem` (existing -- managed bridge pattern reference)
- `ItemStatBlock` IComponentData (existing -- `DIG.Items`, EPIC 16.6)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, EPIC 16.6)
- `AttributeData` IBufferElementData + `AttributeRegenSystem` (existing -- `Traits`, generic attributes)
- `PlayerAuthoring` Baker (existing -- player entity composition)

**Feature:** A lightweight, genre-agnostic combat resource framework that provides multiple resource pools (mana, energy, rage, combo points, custom), integrates resource costs into both player and AI ability pipelines, supports passive regeneration and decay, and bridges to UI -- all without exceeding the player entity's 16KB archetype budget.

---

## Overview

### Problem

The ability systems (both player and AI) currently have no resource cost mechanism. Every ability is free to cast, every channel has no resource drain, and there is no way for designers to gate ability usage behind resource management. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `PlayerStamina` with Current/Max/DrainRate/RegenRate/RegenDelay | No generic resource pool (mana, energy, rage) |
| `PlayerStaminaSystem` drains on sprint/climb, regens with delay | No resource cost framework for abilities |
| `ChannelAction.ResourcePerTick` field on weapons | Field exists but is never read (no resource system to deduct from) |
| `AbilityDefinition` (AI) with full timing/damage/targeting data | No resource cost fields on AI ability definitions |
| `AbilityDefinition` (Player) with priority/blocking/trigger config | No resource cost fields on player ability definitions |
| `AttributeData` buffer with generic regen (Health, Stamina, Energy entries) | Generic but string-keyed, no Burst-compatible type enum, no ability cost integration |
| `ItemStatBlock` on item entities (damage, armor, crit, etc.) | No resource modifier fields (+max mana, +mana regen from gear) |
| `StaminaViewModel` / `ShaderStaminaBarSync` for stamina UI | No mana/energy bar UI, no generic resource bar |
| `CurrencyInventory` for gold/premium/crafting (economy) | Economy currency is NOT combat resources (separate systems) |

**The gap:** Designers cannot make a fireball cost 30 mana, a rage ability require 50 rage, or a combo finisher consume 3 combo points. There is no data-driven pipeline from ability cast request -> resource availability check -> resource deduction -> regeneration tick -> UI update.

### Solution

Introduce a **compact ResourcePool IComponentData** (single component, 4 resource slots, 64 bytes) on the player entity that models any combination of combat resources. Extend `AbilityDefinition` (both AI and player variants) with resource cost fields. Build a `ResourceTickSystem` for regeneration/decay and an `AbilityCostValidationSystem` for gating ability casts behind resource checks. Bridge to UI via the established `CombatUIBridgeSystem` managed bridge pattern.

**Key architectural decision:** Instead of using `IBufferElementData` (which would add dynamic buffer overhead to the already-pressured player archetype), we use a **fixed-slot struct** with 4 resource slots inside a single `IComponentData`. This costs exactly 64 bytes of archetype space -- far less than a dynamic buffer header (40+ bytes) plus per-element storage.

### Principles

1. **Compact over flexible** -- 4 fixed resource slots (64 bytes) beats a dynamic buffer. Covers 99% of game designs (stamina + mana + energy + rage/combo simultaneously). Games needing 5+ combat resources on a single entity are vanishingly rare.
2. **Extend, don't replace** -- `PlayerStamina` continues to work. Migration to `ResourcePool[Stamina]` is opt-in and gradual. Both systems can coexist during transition.
3. **Zero-cost when unused** -- Abilities without resource costs work identically to today. `ResourcePool` absence means abilities are free. No behavior change for entities without the component.
4. **Burst all hot paths** -- `ResourceTickSystem` and `AbilityCostValidationSystem` are `[BurstCompile]` ISystem in `PredictedFixedStepSimulationSystemGroup`. No managed allocations in tick loops.
5. **Ghost-predicted** -- Resource pools are `[GhostComponent(AllPredicted)]` for responsive client-side prediction. Mispredictions resolve via server rollback (same pattern as `PlayerStamina`).
6. **Genre-agnostic** -- The same `ResourcePool` models FPS stamina, RPG mana, action-game rage, fighting-game super meter, MOBA energy, and roguelike charges.

---

## Integration with Existing Systems

### Keep Unchanged (No Modifications)

| System | File | Why It Stays |
|--------|------|-------------|
| `PlayerStaminaSystem` | `Player/Systems/PlayerStaminaSystem.cs` | Continues functioning for sprint/climb stamina. Migration to ResourcePool is Phase 4 (optional, gradual). |
| `PlayerStamina` | `Player/Components/PlayerStamina.cs` | Ghost-replicated stamina component. Untouched until explicit migration. |
| `StaminaViewModel` | `Player/UI/ViewModels/StaminaViewModel.cs` | Reads `PlayerStamina` directly. Works as-is. New `ResourceBarViewModel` handles generic pools. |
| `ShaderStaminaBarSync` | `Player/UI/ShaderStaminaBarSync.cs` | Shader-driven stamina bar. Untouched. |
| `AttributeRegenSystem` | `Traits/AttributeRegenSystem.cs` | Generic attribute regen for non-combat attributes (hunger, thirst, oxygen). Independent system. |
| `AttributeData` | `Traits/AttributeData.cs` | String-keyed generic buffer. Not Burst-friendly for combat hot paths. Kept for survival attributes. |
| `CurrencyInventory` | `Economy/Components/CurrencyInventory.cs` | Gold/Premium/Crafting -- economy currency, not combat resources. Completely separate domain. |
| `CurrencyTransactionSystem` | `Economy/Systems/CurrencyTransactionSystem.cs` | Economy transactions. Unrelated to combat resource costs. |
| `AbilityExecutionSystem` (AI) | `AI/Systems/AbilityExecutionSystem.cs` | Phase lifecycle unchanged. Resource deduction hooks into existing phase transitions. |
| `CombatUIBridgeSystem` | `Combat/UI/CombatUIBridgeSystem.cs` | Pattern reference only. Not modified. New `ResourceUIBridgeSystem` follows same pattern. |

### Modify (Extend Existing)

| System | File | What Changes |
|--------|------|-------------|
| `AbilityDefinition` (AI) | `AI/Components/AbilityDefinition.cs` | Add `ResourceType ResourceCostType` + `float ResourceCostAmount` + `CostTiming ResourceCostTiming` fields (12 bytes, backward compatible -- defaults to ResourceType.None / 0 / OnCast) |
| `AbilityDefinition` (Player) | `Player/Abilities/AbilityComponents.cs` | Add `ResourceType ResourceCostType` + `float ResourceCostAmount` fields (8 bytes, backward compatible -- defaults to None/0) |
| `AbilitySelectionSystem` (AI) | `AI/Systems/AbilitySelectionSystem.cs` | Add resource availability check after existing cooldown/range/phase/HP checks. Skip ability if insufficient resource. |
| `AbilityDefinitionSO` | `AI/Authoring/AbilityDefinitionSO.cs` | Add inspector fields for resource cost type, amount, timing. Baked into AbilityDefinition. |
| `ItemStatBlock` | `Items/Components/ItemStatBlock.cs` | Add `float MaxManaBonus`, `float ManaRegenBonus`, `float MaxEnergyBonus`, `float EnergyRegenBonus` fields (16 bytes). |
| `EquippedStatsSystem` | `Items/Systems/EquippedStatsSystem.cs` | Aggregate new resource modifier fields from equipped items into `PlayerEquippedStats`. |
| `PlayerEquippedStats` | `Items/Components/PlayerEquippedStats.cs` | Add matching resource modifier aggregate fields. |
| `PlayerAuthoring` | `Player/Authoring/PlayerAuthoring.cs` | Add `ResourcePool` component baking with designer-configurable initial values. |
| `ChannelActionSystem` | `Weapons/Systems/ChannelActionSystem.cs` | Implement existing `ResourcePerTick` drain by reading `ResourcePool` on the weapon owner. |

### Create New (No Overlap)

Everything else in EPIC 16.8 is purely additive:
- `ResourcePool` IComponentData -- new compact 4-slot resource component
- `ResourceType` enum -- typed resource identifiers
- `CostTiming` enum -- when to deduct resources
- `ResourceTickSystem` -- regeneration/decay tick
- `AbilityCostValidationSystem` -- resource gating for ability casts
- `ResourceModificationRequest` -- transactional resource changes
- `ResourceUIBridgeSystem` -- managed bridge to resource bar UI
- `ResourceBarViewModel` -- generic resource bar view model
- `ResourceBarView` -- generic resource bar MonoBehaviour
- `ResourcePoolAuthoring` -- standalone authoring for non-player entities
- `ResourceDebugSystem` -- editor debug overlay

---

## Architecture Overview

```
+-------------------------------------------------------------------+
|                    DESIGNER TOOLING LAYER                          |
|  AbilityDefinitionSO    ResourcePoolAuthoring    ItemStatBlock     |
|  (ResourceCost fields)  (Initial pool config)   (+Resource mods)  |
+----------------------------------+--------------------------------+
                                   | Bake
+----------------------------------v--------------------------------+
|                    ECS DATA LAYER                                  |
|                                                                    |
|  ResourcePool [4 slots]    AbilityDefinition.ResourceCost*        |
|  (on player/AI entity)     (on ability buffer elements)            |
|                                                                    |
|  PlayerEquippedStats       ResourceModificationRequest             |
|  (.MaxManaBonus, etc.)     (transactional changes from any system) |
+----------------------------------+--------------------------------+
                                   |
+----------------------------------v--------------------------------+
|              SYSTEM PIPELINE (PredictedFixedStep)                  |
|                                                                    |
|  EquippedStatsSystem -----> ResourceModifierApplySystem            |
|  (aggregate gear bonuses)   (apply max/regen from gear)            |
|                                    |                               |
|  AbilityCostValidationSystem <-----+                               |
|  (check resource, gate cast, deduct on cast/tick)                  |
|                                    |                               |
|  ResourceTickSystem -----> ResourcePool.Current updated            |
|  (regen, decay, overflow)                                          |
|                                    |                               |
|  ChannelActionSystem               |                               |
|  (deduct ResourcePerTick) ---------+                               |
+----------------------------------+--------------------------------+
                                   |
+----------------------------------v--------------------------------+
|              PRESENTATION LAYER (PresentationSystemGroup)          |
|                                                                    |
|  ResourceUIBridgeSystem (managed, reads ResourcePool)              |
|       |                                                            |
|  ResourceBarViewModel --> ResourceBarView (shader/UI)              |
+-------------------------------------------------------------------+
```

### Data Flow (Ability Cast with Resource Cost)

```
Frame N (PredictedFixedStep):
  1. AbilityTriggerSystem: Player presses ability input
  2. AbilityCostValidationSystem: Reads AbilityDefinition[i].ResourceCostType/Amount
     - Looks up ResourcePool.Slots[type].Current >= Amount
     - If insufficient: blocks cast (sets AbilityDefinition[i].CanStart = false)
     - If sufficient AND CostTiming == OnCast: deducts Amount, marks LastDrainTime
  3. AbilityLifecycleSystem: Ability starts (or is blocked)

Frame N+1..N+K (PredictedFixedStep, channeled ability):
  4. ChannelActionSystem: Reads ResourcePerTick
     - Deducts from ResourcePool.Slots[type] per tick interval
     - If resource depleted: forces channel cancel

Frame N+K+1 (PredictedFixedStep):
  5. ResourceTickSystem: No active drain detected
     - Checks LastDrainTime + RegenDelay < CurrentTime
     - Applies RegenRate * DeltaTime to Current (clamped to Max)

Frame N+K+1 (PresentationSystemGroup):
  6. ResourceUIBridgeSystem: Reads ResourcePool, pushes to ResourceBarViewModel
  7. ResourceBarView: Updates shader/UI fill amounts
```

---

## Phase 0: Core Resource Data Model

### 0.1 ResourceType Enum

- [x] Create `ResourceType` byte enum -- typed identifiers for all combat resource pools

```csharp
public enum ResourceType : byte
{
    None     = 0,   // No resource (ability is free)
    Stamina  = 1,   // Movement/physical actions (existing PlayerStamina maps here)
    Mana     = 2,   // Magical/spell resource
    Energy   = 3,   // Tech/sci-fi resource, fast regen
    Rage     = 4,   // Generated by dealing/taking damage, decays when idle
    Combo    = 5,   // Integer charges built by specific actions, spent on finishers
    Custom0  = 6,   // Game-specific resource slot
    Custom1  = 7    // Game-specific resource slot
}
```

**File:** `Assets/Scripts/Combat/Resources/ResourceType.cs` (NEW)

### 0.2 CostTiming Enum

- [x] Create `CostTiming` byte enum -- when resource deduction occurs during ability execution

```csharp
public enum CostTiming : byte
{
    OnCast     = 0,  // Deduct full cost when ability begins (default, most common)
    PerTick    = 1,  // Deduct per tick during channel/DOT (ChannelAction pattern)
    OnComplete = 2,  // Deduct when ability successfully completes (combo finisher)
    OnHit      = 3   // Deduct per successful hit delivered (multi-hit abilities)
}
```

**File:** `Assets/Scripts/Combat/Resources/CostTiming.cs` (NEW)

### 0.3 ResourceFlags

- [x] Create `ResourceFlags` byte flags -- per-slot behavioral modifiers

```csharp
[System.Flags]
public enum ResourceFlags : byte
{
    None           = 0,
    CanOverflow    = 1 << 0,  // Current can exceed Max (temporary buffs)
    DecaysWhenFull = 1 << 1,  // Decays toward Max when overflowed (overflow is temporary)
    PausedRegen    = 1 << 2,  // Regen paused by external effect (silence, drain)
    GenerateOnHit  = 1 << 3,  // Generates resource when hitting enemies (rage)
    GenerateOnTake = 1 << 4,  // Generates resource when taking damage (rage)
    DecaysWhenIdle = 1 << 5,  // Decays toward 0 when not being generated (rage/combo)
    IsInteger      = 1 << 6   // Treat as integer charges (combo points, ammo)
}
```

**File:** `Assets/Scripts/Combat/Resources/ResourceFlags.cs` (NEW)

### 0.4 ResourceSlot Struct (Blittable, Burst-Safe)

- [x] Create `ResourceSlot` blittable struct -- data for a single resource pool

```csharp
public struct ResourceSlot
{
    public float Current;        // 4 bytes - current value
    public float Max;            // 4 bytes - maximum value (before gear bonuses)
    public float RegenRate;      // 4 bytes - per-second regen (negative = decay)
    public float RegenDelay;     // 4 bytes - seconds after drain before regen starts
    public float LastDrainTime;  // 4 bytes - timestamp of last deduction
    public float DecayRate;      // 4 bytes - per-second decay when DecaysWhenIdle
    public float GenerateAmount; // 4 bytes - amount generated per trigger (hit/take)
    public ResourceFlags Flags;  // 1 byte  - behavioral flags
    public ResourceType Type;    // 1 byte  - which resource this slot represents
    // 2 bytes padding (struct alignment)
}                                // = 32 bytes per slot
```

**File:** `Assets/Scripts/Combat/Resources/ResourceSlot.cs` (NEW)

### 0.5 ResourcePool IComponentData (4-Slot Compact Design)

- [x] Create `ResourcePool` IComponentData -- the core component added to player/AI entities

```csharp
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct ResourcePool : IComponentData
{
    [GhostField] public ResourceSlot Slot0;  // 32 bytes
    [GhostField] public ResourceSlot Slot1;  // 32 bytes
    // Total: 64 bytes on player archetype

    // Slot count is fixed at 2 for the player entity to minimize archetype pressure.
    // Enemies needing more slots use ResourcePoolExtended (child entity pattern).
    // Most games need at most 2 simultaneous combat resources on a player
    // (e.g., Stamina + Mana, Energy + Rage, Health + Shields).

    /// <summary>
    /// Get current value for a resource type. Returns 0 if type not found.
    /// </summary>
    public readonly float GetCurrent(ResourceType type)
    {
        if (Slot0.Type == type) return Slot0.Current;
        if (Slot1.Type == type) return Slot1.Current;
        return 0f;
    }

    /// <summary>
    /// Get max value for a resource type. Returns 0 if type not found.
    /// </summary>
    public readonly float GetMax(ResourceType type)
    {
        if (Slot0.Type == type) return Slot0.Max;
        if (Slot1.Type == type) return Slot1.Max;
        return 0f;
    }

    /// <summary>
    /// Check if entity has enough of a resource. Returns true if type is None (free ability).
    /// </summary>
    public readonly bool HasResource(ResourceType type, float amount)
    {
        if (type == ResourceType.None) return true;
        return GetCurrent(type) >= amount;
    }

    /// <summary>
    /// Try to deduct resource. Returns true if successful, false if insufficient.
    /// Caller must write back the modified struct.
    /// </summary>
    public bool TryDeduct(ResourceType type, float amount, float currentTime)
    {
        if (type == ResourceType.None) return true;
        if (Slot0.Type == type)
        {
            if (Slot0.Current < amount) return false;
            Slot0.Current -= amount;
            Slot0.LastDrainTime = currentTime;
            return true;
        }
        if (Slot1.Type == type)
        {
            if (Slot1.Current < amount) return false;
            Slot1.Current -= amount;
            Slot1.LastDrainTime = currentTime;
            return true;
        }
        return false; // Type not found -- treat as insufficient
    }

    /// <summary>
    /// Add resource (generation, regen). Respects Max unless CanOverflow.
    /// </summary>
    public void Add(ResourceType type, float amount)
    {
        if (type == ResourceType.None) return;
        if (Slot0.Type == type)
        {
            float cap = (Slot0.Flags & ResourceFlags.CanOverflow) != 0
                ? float.MaxValue : Slot0.Max;
            Slot0.Current = Unity.Mathematics.math.min(Slot0.Current + amount, cap);
            return;
        }
        if (Slot1.Type == type)
        {
            float cap = (Slot1.Flags & ResourceFlags.CanOverflow) != 0
                ? float.MaxValue : Slot1.Max;
            Slot1.Current = Unity.Mathematics.math.min(Slot1.Current + amount, cap);
            return;
        }
    }

    public static ResourcePool Default => new ResourcePool
    {
        Slot0 = new ResourceSlot { Type = ResourceType.None },
        Slot1 = new ResourceSlot { Type = ResourceType.None }
    };
}
```

**File:** `Assets/Scripts/Combat/Resources/ResourcePool.cs` (NEW)

**Archetype budget:** 64 bytes total. The player entity (per `PlayerAuthoring.cs`) already includes `PlayerStamina` (24 bytes), `Health` (8 bytes), `CurrencyInventory` (12 bytes), `PlayerEquippedStats` (~32 bytes), plus dozens of other components. Adding 64 bytes for `ResourcePool` is well within the 16KB archetype limit. For comparison, `TargetingModuleLink` was moved to a child entity at ~200+ bytes; `ResourcePool` at 64 bytes is safe on the player entity.

### 0.6 ResourcePoolExtended (Child Entity Pattern for AI)

- [x] Create `ResourcePoolExtended` IComponentData for entities needing more than 2 resource slots

```csharp
/// <summary>
/// Extended resource pool on a child entity for AI bosses or entities needing 3-4 resource types.
/// Linked from parent via ResourcePoolLink. NOT on ghost-replicated player entities.
/// Only used for server-side AI entities where archetype pressure is lower.
/// </summary>
public struct ResourcePoolExtended : IComponentData
{
    public ResourceSlot Slot2;  // 32 bytes
    public ResourceSlot Slot3;  // 32 bytes
}

/// <summary>
/// Link from parent entity to child entity holding ResourcePoolExtended.
/// Only present on entities that need more than 2 resource slots.
/// </summary>
public struct ResourcePoolLink : IComponentData
{
    public Entity ExtendedEntity;
}
```

**File:** `Assets/Scripts/Combat/Resources/ResourcePoolExtended.cs` (NEW)

---

## Phase 1: Resource Tick System

### 1.1 ResourceTickSystem

- [x] Create `ResourceTickSystem` -- handles regeneration, decay, overflow drain, and integer clamping
- [x] `[BurstCompile]` ISystem in `PredictedFixedStepSimulationSystemGroup`
- [x] `[UpdateBefore(typeof(PlayerStaminaSystem))]` -- resource tick before stamina (stamina migration can read ResourcePool later)
- [x] `[WorldSystemFilter(ServerSimulation | LocalSimulation | ClientSimulation)]` -- predicted on all worlds

```csharp
[BurstCompile]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerStaminaSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                   WorldSystemFilterFlags.LocalSimulation |
                   WorldSystemFilterFlags.ClientSimulation)]
public partial struct ResourceTickSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkTime>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float currentTime = (float)SystemAPI.Time.ElapsedTime;

        foreach (var pool in SystemAPI.Query<RefRW<ResourcePool>>()
            .WithAll<Simulate>())
        {
            TickSlot(ref pool.ValueRW.Slot0, deltaTime, currentTime);
            TickSlot(ref pool.ValueRW.Slot1, deltaTime, currentTime);
        }
    }

    private static void TickSlot(ref ResourceSlot slot, float deltaTime, float currentTime)
    {
        if (slot.Type == ResourceType.None) return;

        // === Decay when idle (rage/combo) ===
        if ((slot.Flags & ResourceFlags.DecaysWhenIdle) != 0 && slot.DecayRate > 0f)
        {
            float timeSinceDrain = currentTime - slot.LastDrainTime;
            // Only decay if not recently used (reuse RegenDelay as "activity window")
            if (timeSinceDrain >= slot.RegenDelay)
            {
                slot.Current -= slot.DecayRate * deltaTime;
                slot.Current = math.max(0f, slot.Current);
            }
        }

        // === Overflow decay (temporary buffs draining back to max) ===
        if ((slot.Flags & ResourceFlags.DecaysWhenFull) != 0 && slot.Current > slot.Max)
        {
            float decayAmount = slot.DecayRate > 0f ? slot.DecayRate : slot.Max * 0.1f;
            slot.Current -= decayAmount * deltaTime;
            slot.Current = math.max(slot.Max, slot.Current);
        }

        // === Regeneration ===
        if ((slot.Flags & ResourceFlags.PausedRegen) == 0 &&
            (slot.Flags & ResourceFlags.DecaysWhenIdle) == 0 && // Rage doesn't regen, it decays
            slot.RegenRate > 0f && slot.Current < slot.Max)
        {
            float timeSinceDrain = currentTime - slot.LastDrainTime;
            if (timeSinceDrain >= slot.RegenDelay)
            {
                slot.Current += slot.RegenRate * deltaTime;
                slot.Current = math.min(slot.Current, slot.Max);
            }
        }

        // === Integer clamping (combo points) ===
        if ((slot.Flags & ResourceFlags.IsInteger) != 0)
        {
            slot.Current = math.floor(slot.Current);
        }
    }
}
```

**File:** `Assets/Scripts/Combat/Resources/Systems/ResourceTickSystem.cs` (NEW)

### 1.2 ResourceGenerationSystem

- [x] Create `ResourceGenerationSystem` -- generates resources on hit/take damage (rage generation)
- [x] Reads `CombatResultEvent` entities to detect hits dealt/received
- [x] Writes to `ResourcePool` slots with `GenerateOnHit` / `GenerateOnTake` flags
- [x] `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- [x] `[UpdateAfter(typeof(CombatResolutionSystem))]`
- [x] Server-authoritative (no client prediction for generation -- avoids misprediction on hit confirmation)

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CombatResolutionSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct ResourceGenerationSystem : ISystem
{
    // Reads CombatResultEvent, finds attacker/target entities with ResourcePool,
    // adds GenerateAmount to slots with GenerateOnHit (attacker) / GenerateOnTake (target)
}
```

**File:** `Assets/Scripts/Combat/Resources/Systems/ResourceGenerationSystem.cs` (NEW)

---

## Phase 2: Ability Cost Integration

### 2.1 AI AbilityDefinition Extension

- [x] Add resource cost fields to existing `AbilityDefinition` (AI variant, `DIG.AI.Components`)

```csharp
// Added to AbilityDefinition struct (AI):
public ResourceType ResourceCostType;    // 1 byte
public CostTiming ResourceCostTiming;    // 1 byte
public float ResourceCostAmount;         // 4 bytes
// Total addition: 6 bytes + 2 padding = 8 bytes per buffer element
// With InternalBufferCapacity(4): +32 bytes worst case, acceptable
```

- [x] Update `AbilityDefinition.DefaultMelee()` to default `ResourceCostType = ResourceType.None` (backward compatible)
- [x] Update `AbilityDefinitionSO` inspector to expose resource cost fields

**File:** `Assets/Scripts/AI/Components/AbilityDefinition.cs` (MODIFY)
**File:** `Assets/Scripts/AI/Authoring/AbilityDefinitionSO.cs` (MODIFY)

### 2.2 Player AbilityDefinition Extension

- [x] Add resource cost fields to existing `AbilityDefinition` (Player variant, `DIG.Player.Abilities`)

```csharp
// Added to AbilityDefinition struct (Player):
public ResourceType ResourceCostType;    // 1 byte (padding to 4)
public float ResourceCostAmount;         // 4 bytes
// Total addition: 8 bytes per buffer element
```

**File:** `Assets/Scripts/Player/Abilities/AbilityComponents.cs` (MODIFY)

### 2.3 AbilityCostValidationSystem (AI)

- [x] Create system that checks resource availability before AI ability selection
- [x] Integrates into existing `AbilitySelectionSystem` validation chain (after cooldown/range/phase/HP checks)
- [x] Approach: **Modify `AbilitySelectionSystem.OnUpdate`** to add a resource check in the existing validation loop

```csharp
// In AbilitySelectionSystem.OnUpdate, existing validation loop (line ~101):
// After: if (hpPercent < ability.HPThresholdMin || hpPercent > ability.HPThresholdMax) continue;
// Add:
// Resource check
if (ability.ResourceCostType != ResourceType.None)
{
    if (!SystemAPI.HasComponent<ResourcePool>(entity)) continue;
    var pool = SystemAPI.GetComponent<ResourcePool>(entity);
    if (!pool.HasResource(ability.ResourceCostType, ability.ResourceCostAmount)) continue;
}
```

**File:** `Assets/Scripts/AI/Systems/AbilitySelectionSystem.cs` (MODIFY)

### 2.4 AbilityCostDeductionSystem (AI)

- [x] Create system that deducts resources at the appropriate timing during AI ability execution
- [x] Hooks into existing `AbilityExecutionSystem` phase transitions:
  - `OnCast`: Deduct when entering `Casting` phase from `Telegraph` (or `Casting` from `Idle`)
  - `PerTick`: Deduct each `TickInterval` during `Active` phase
  - `OnComplete`: Deduct when transitioning from `Active` to `Recovery`
  - `OnHit`: Deduct when `DamageDealt` is set to true
- [x] If resource runs out mid-cast with `PerTick` timing: interrupt the ability (set Phase = Recovery)

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AbilityExecutionSystem))]
[UpdateAfter(typeof(AbilitySelectionSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct AbilityCostDeductionSystem : ISystem
{
    // Reads AbilityExecutionState phase transitions
    // Reads AbilityDefinition[SelectedAbilityIndex].ResourceCost*
    // Writes ResourcePool via TryDeduct
    // On PerTick insufficient: sets exec.Phase = Recovery (interrupt)
}
```

**File:** `Assets/Scripts/Combat/Resources/Systems/AbilityCostDeductionSystem.cs` (NEW)

### 2.5 PlayerAbilityCostSystem

- [x] Create system that validates and deducts resource costs for player abilities
- [x] `[UpdateInGroup(typeof(AbilitySystemGroup))]`
- [x] `[UpdateBefore(typeof(AbilityLifecycleSystem))]`
- [x] Reads player `AbilityDefinition` buffer + `AbilityState`
- [x] On ability start request: check `ResourcePool.HasResource()`, set `CanStart = false` if insufficient
- [x] On ability confirmed start: deduct via `ResourcePool.TryDeduct()`

```csharp
[BurstCompile]
[UpdateInGroup(typeof(AbilitySystemGroup))]
[UpdateBefore(typeof(AbilityLifecycleSystem))]
public partial struct PlayerAbilityCostSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float currentTime = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (abilityState, abilities, pool, entity) in
            SystemAPI.Query<RefRO<AbilityState>, DynamicBuffer<DIG.Player.Abilities.AbilityDefinition>,
                           RefRW<ResourcePool>>()
            .WithAll<AbilitySystemTag, Simulate>()
            .WithEntityAccess())
        {
            int pending = abilityState.ValueRO.PendingAbilityIndex;
            if (pending < 0 || pending >= abilities.Length) continue;

            var ability = abilities[pending];
            if (ability.ResourceCostType == ResourceType.None) continue;

            // Block if insufficient
            if (!pool.ValueRO.HasResource(ability.ResourceCostType, ability.ResourceCostAmount))
            {
                ability.CanStart = false;
                abilities[pending] = ability;
            }
            // Deduct on successful start (LifecycleSystem will check CanStart)
            else if (ability.CanStart)
            {
                pool.ValueRW.TryDeduct(ability.ResourceCostType, ability.ResourceCostAmount, currentTime);
            }
        }
    }
}
```

**File:** `Assets/Scripts/Combat/Resources/Systems/PlayerAbilityCostSystem.cs` (NEW)

### 2.6 ChannelActionSystem Resource Integration

- [x] Implement the existing `ChannelAction.ResourcePerTick` drain that is currently stubbed
- [x] In `ChannelActionSystem.OnUpdate`, after the per-tick effect application:
  - Read `ResourcePool` on the weapon owner entity
  - Deduct `ResourcePerTick` from the appropriate resource type
  - If resource depleted: force channel cancel (set channel state to inactive)
- [x] Resource type for channels: determined by a new `ResourceType ChannelResourceType` field on `ChannelAction`
- [x] Default: `ResourceType.None` (channels that don't cost resources continue to work as-is)

**File:** `Assets/Scripts/Weapons/Systems/ChannelActionSystem.cs` (MODIFY)
**File:** `Assets/Scripts/Weapons/Components/WeaponActionComponents.cs` (MODIFY -- add `ResourceType ChannelResourceType` to `ChannelAction`)

---

## Phase 3: Equipment Resource Modifiers

### 3.1 ItemStatBlock Extension

- [x] Add resource modifier fields to existing `ItemStatBlock`

```csharp
// Added to ItemStatBlock:
public float MaxManaBonus;          // +50 max mana from equipment
public float ManaRegenBonus;        // +2.0 mana/sec from equipment
public float MaxEnergyBonus;        // +30 max energy from equipment
public float EnergyRegenBonus;      // +1.5 energy/sec from equipment
public float MaxStaminaBonus;       // +20 max stamina from equipment
public float StaminaRegenBonus;     // +3.0 stamina/sec from equipment
// 24 bytes addition. ItemStatBlock is on ITEM entities, not player -- no archetype pressure.
```

**File:** `Assets/Scripts/Items/Components/ItemStatBlock.cs` (MODIFY)

### 3.2 PlayerEquippedStats Extension

- [x] Add matching aggregate fields to `PlayerEquippedStats`

```csharp
// Added to PlayerEquippedStats:
public float MaxManaBonus;
public float ManaRegenBonus;
public float MaxEnergyBonus;
public float EnergyRegenBonus;
public float MaxStaminaBonus;
public float StaminaRegenBonus;
```

**File:** `Assets/Scripts/Items/Components/PlayerEquippedStats.cs` (MODIFY)

### 3.3 EquippedStatsSystem Update

- [x] Update `EquippedStatsSystem` to aggregate new resource fields from equipped items
- [x] Sum all resource bonuses from equipped item `ItemStatBlock` components into `PlayerEquippedStats`

**File:** `Assets/Scripts/Items/Systems/EquippedStatsSystem.cs` (MODIFY)

### 3.4 ResourceModifierApplySystem

- [x] Create system that applies equipment bonuses to `ResourcePool` max/regen values
- [x] `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- [x] `[UpdateAfter(typeof(EquippedStatsSystem))]`
- [x] `[UpdateBefore(typeof(ResourceTickSystem))]`
- [x] Reads `PlayerEquippedStats` resource bonuses, writes to `ResourcePool` slot Max/RegenRate
- [x] Uses base values from authoring + equipment bonuses (additive)
- [x] Must track base values separately to avoid compounding (store base in `ResourcePoolBase` component or use ResourceSlot.Max as base and compute effective max each frame)

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EquippedStatsSystem))]
public partial struct ResourceModifierApplySystem : ISystem
{
    // Reads PlayerEquippedStats.MaxManaBonus / ManaRegenBonus / etc.
    // Writes ResourcePool.Slot*.Max = BaseMax + EquipBonus
    // Writes ResourcePool.Slot*.RegenRate = BaseRegen + EquipBonus
    // Clamps Current to new Max if Max decreased (unequipping gear)
}
```

**File:** `Assets/Scripts/Combat/Resources/Systems/ResourceModifierApplySystem.cs` (NEW)

### 3.5 ResourcePoolBase Component

- [x] Create `ResourcePoolBase` IComponentData to store authoring-time base values (before gear modifiers)

```csharp
/// <summary>
/// Stores the base (pre-equipment) Max and RegenRate for each resource slot.
/// Set once at bake time. ResourceModifierApplySystem reads this + equipment bonuses
/// to compute effective ResourcePool values each frame.
/// </summary>
public struct ResourcePoolBase : IComponentData
{
    public float Slot0BaseMax;
    public float Slot0BaseRegen;
    public float Slot1BaseMax;
    public float Slot1BaseRegen;
}
// 16 bytes on player entity. Safe.
```

**File:** `Assets/Scripts/Combat/Resources/ResourcePoolBase.cs` (NEW)

---

## Phase 4: PlayerStamina Migration Path

### 4.1 Migration Strategy

The existing `PlayerStamina` component and `PlayerStaminaSystem` continue to function throughout this EPIC. Migration to `ResourcePool[Stamina]` is a gradual, opt-in process:

**Step 1 (This EPIC):** `ResourcePool` is added alongside `PlayerStamina`. Both exist. `ResourcePool.Slot0` can be configured as `ResourceType.Stamina` but `PlayerStaminaSystem` continues to manage sprint/climb drain independently.

**Step 2 (Future EPIC):** Create `StaminaToResourcePoolSyncSystem` that copies `ResourcePool.Slot[Stamina].Current` <-> `PlayerStamina.Current` bidirectionally. This allows both systems to read/write stamina during the transition period.

**Step 3 (Future EPIC):** Migrate sprint/climb stamina drain from `PlayerStaminaSystem` to `ResourceTickSystem` + ability cost pattern. `PlayerStaminaSystem` becomes a thin wrapper reading from `ResourcePool`.

**Step 4 (Final):** Remove `PlayerStamina` component, `PlayerStaminaSystem`, and sync system. All stamina logic lives in `ResourcePool`.

### 4.2 Backward Compatibility During Transition

- [x] `ResourcePool` absence means abilities are free (no resource check)
- [x] `PlayerStamina` continues to work exactly as before for sprint/climb
- [x] `StaminaViewModel` and `ShaderStaminaBarSync` continue reading `PlayerStamina` directly
- [x] New `ResourceBarViewModel` reads `ResourcePool` for mana/energy bars
- [x] No existing system is broken by adding `ResourcePool` to the player entity

### 4.3 Tasks

- [x] Add `ResourcePool` baking to `PlayerAuthoring` baker with configurable initial slot types
- [x] Document migration path in setup guide
- [x] Ensure `PlayerStaminaSystem` and `ResourceTickSystem` do not double-drain stamina (both systems only touch their own data)

---

## Phase 5: Resource UI Bridge

### 5.1 ResourceUIBridgeSystem

- [x] Create managed `ResourceUIBridgeSystem` following the `CombatUIBridgeSystem` pattern
- [x] `[UpdateInGroup(typeof(PresentationSystemGroup))]`
- [x] Reads `ResourcePool` from local player entity (via `GhostOwnerIsLocal` query)
- [x] Pushes data to registered `IResourceBarProvider` implementations
- [x] One-way data flow: ECS -> Managed (never managed -> ECS)

```csharp
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class ResourceUIBridgeSystem : SystemBase
{
    private EntityQuery _localPlayerQuery;

    protected override void OnCreate()
    {
        _localPlayerQuery = GetEntityQuery(
            ComponentType.ReadOnly<ResourcePool>(),
            ComponentType.ReadOnly<GhostOwnerIsLocal>()
        );
    }

    protected override void OnUpdate()
    {
        if (_localPlayerQuery.IsEmpty) return;
        if (ResourceUIRegistry.Instance == null) return;

        var entity = _localPlayerQuery.GetSingletonEntity();
        var pool = EntityManager.GetComponentData<ResourcePool>(entity);

        ResourceUIRegistry.Instance.UpdateBars(pool);
    }
}
```

**File:** `Assets/Scripts/Combat/Resources/UI/ResourceUIBridgeSystem.cs` (NEW)

### 5.2 ResourceUIRegistry

- [x] Create `ResourceUIRegistry` static singleton for provider registration (same pattern as `CombatUIRegistry`)
- [x] MonoBehaviours register themselves on enable, unregister on disable
- [x] Thread-safe registration (main thread only, presentation group guarantee)

```csharp
public class ResourceUIRegistry : MonoBehaviour
{
    public static ResourceUIRegistry Instance { get; private set; }

    private readonly Dictionary<ResourceType, IResourceBarProvider> _bars = new();

    public void RegisterBar(ResourceType type, IResourceBarProvider provider) { ... }
    public void UnregisterBar(ResourceType type) { ... }

    public void UpdateBars(ResourcePool pool)
    {
        UpdateBar(pool.Slot0);
        UpdateBar(pool.Slot1);
    }

    private void UpdateBar(ResourceSlot slot)
    {
        if (slot.Type == ResourceType.None) return;
        if (_bars.TryGetValue(slot.Type, out var provider))
        {
            provider.UpdateResourceBar(slot.Current, slot.Max,
                slot.Current / math.max(slot.Max, 0.001f));
        }
    }
}
```

**File:** `Assets/Scripts/Combat/Resources/UI/ResourceUIRegistry.cs` (NEW)

### 5.3 IResourceBarProvider Interface

- [x] Create interface for resource bar UI implementations

```csharp
public interface IResourceBarProvider
{
    void UpdateResourceBar(float current, float max, float percent);
    void SetDraining(bool isDraining);
    void SetRegenerating(bool isRegenerating);
    void OnResourceDepleted();
    void OnResourceFull();
}
```

**File:** `Assets/Scripts/Combat/Resources/UI/IResourceBarProvider.cs` (NEW)

### 5.4 ResourceBarViewModel

- [x] Create `ResourceBarViewModel` MonoBehaviour (same pattern as `StaminaViewModel`)
- [x] Reads from `ResourceUIRegistry` updates
- [x] Exposes UI-friendly properties: Current, Max, Percent, IsDraining, IsRecovering, IsLow, IsEmpty
- [x] Fires `OnChanged` event for reactive UI
- [x] Configurable `ResourceType` field in inspector to select which resource this bar displays

```csharp
public class ResourceBarViewModel : MonoBehaviour, IResourceBarProvider
{
    [SerializeField] private ResourceType _resourceType = ResourceType.Mana;
    [SerializeField] private float _lowThreshold = 0.2f;
    [SerializeField] private float _emptyThreshold = 0.05f;

    public float Current { get; private set; }
    public float Max { get; private set; }
    public float Percent { get; private set; }
    public bool IsDraining { get; private set; }
    public bool IsRecovering { get; private set; }
    public bool IsLow { get; private set; }
    public bool IsEmpty { get; private set; }

    public event System.Action<ResourceBarViewModel> OnChanged;

    // IResourceBarProvider implementation...
}
```

**File:** `Assets/Scripts/Combat/Resources/UI/ResourceBarViewModel.cs` (NEW)

### 5.5 ShaderResourceBarSync

- [x] Create `ShaderResourceBarSync` MonoBehaviour (same pattern as `ShaderStaminaBarSync`)
- [x] Driven by `ResourceBarViewModel` events
- [x] Reuses the `DIG/UI/ProceduralStaminaBar` shader (or a configurable shader reference)
- [x] Configurable color per resource type (mana = blue, energy = yellow, rage = red)

**File:** `Assets/Scripts/Combat/Resources/UI/ShaderResourceBarSync.cs` (NEW)

---

## Phase 6: Authoring & Baking

### 6.1 ResourcePoolAuthoring

- [x] Create `ResourcePoolAuthoring` MonoBehaviour for standalone resource pool configuration
- [x] Inspector fields for each slot: ResourceType, Max, RegenRate, RegenDelay, DecayRate, Flags
- [x] Can be placed on player prefab OR enemy prefabs
- [x] Baker creates `ResourcePool` + `ResourcePoolBase` components

```csharp
public class ResourcePoolAuthoring : MonoBehaviour
{
    [Header("Slot 0")]
    public ResourceType Slot0Type = ResourceType.Mana;
    public float Slot0Max = 100f;
    public float Slot0Start = 100f;
    public float Slot0RegenRate = 5f;
    public float Slot0RegenDelay = 2f;
    public float Slot0DecayRate = 0f;
    public ResourceFlags Slot0Flags = ResourceFlags.None;

    [Header("Slot 1")]
    public ResourceType Slot1Type = ResourceType.None;
    public float Slot1Max = 0f;
    // ... same fields

    class Baker : Baker<ResourcePoolAuthoring>
    {
        public override void Bake(ResourcePoolAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ResourcePool
            {
                Slot0 = new ResourceSlot
                {
                    Type = authoring.Slot0Type,
                    Current = authoring.Slot0Start,
                    Max = authoring.Slot0Max,
                    RegenRate = authoring.Slot0RegenRate,
                    RegenDelay = authoring.Slot0RegenDelay,
                    DecayRate = authoring.Slot0DecayRate,
                    Flags = authoring.Slot0Flags
                },
                Slot1 = new ResourceSlot { /* ... */ }
            });
            AddComponent(entity, new ResourcePoolBase
            {
                Slot0BaseMax = authoring.Slot0Max,
                Slot0BaseRegen = authoring.Slot0RegenRate,
                Slot1BaseMax = authoring.Slot1Max,
                Slot1BaseRegen = authoring.Slot1RegenRate
            });
        }
    }
}
```

**File:** `Assets/Scripts/Combat/Resources/Authoring/ResourcePoolAuthoring.cs` (NEW)

### 6.2 PlayerAuthoring Integration

- [x] Add `ResourcePool` baking to existing `PlayerAuthoring` baker
- [x] Use `AttributeConfig` list entries named "Mana"/"Energy" to auto-configure ResourcePool slots
- [x] Fallback: if no ResourcePoolAuthoring is present on player prefab, bake a default `ResourcePool` from the Attributes list

**File:** `Assets/Scripts/Player/Authoring/PlayerAuthoring.cs` (MODIFY)

### 6.3 AbilityProfileAuthoring Extension

- [x] Extend existing `AbilityProfileAuthoring` / `AbilityDefinitionSO` to include resource cost fields
- [x] Inspector: dropdown for ResourceType, float for cost amount, dropdown for CostTiming
- [x] Baked into `AbilityDefinition` buffer elements

**File:** `Assets/Scripts/AI/Authoring/AbilityProfileAuthoring.cs` (MODIFY)
**File:** `Assets/Scripts/AI/Authoring/AbilityDefinitionSO.cs` (MODIFY)

---

## Phase 7: Debug Tooling

### 7.1 ResourceDebugSystem

- [x] Create `ResourceDebugSystem` -- runtime debug overlay for resource pools
- [x] `[WorldSystemFilter(ClientSimulation | LocalSimulation)]`
- [x] `[UpdateInGroup(typeof(PresentationSystemGroup))]`
- [x] Toggle via debug console command `dig.resource.debug`
- [x] Displays per-entity resource bars above characters (similar to health bars)
- [x] Color-coded by resource type: Stamina=green, Mana=blue, Energy=yellow, Rage=red, Combo=purple
- [x] Shows numeric values and regen/decay state

**File:** `Assets/Scripts/Combat/Resources/Debug/ResourceDebugSystem.cs` (NEW)

### 7.2 Resource Inspector (AI Workstation Integration)

- [x] Extend existing `BrainInspectorModule` in AI Workstation to show resource pool state
- [x] Display current/max for each active resource slot
- [x] Show regen state (active/paused/delayed), generation rate, decay state
- [x] Highlight resource-gated abilities (mark abilities that are blocked by insufficient resources)

**File:** `Assets/Editor/AIWorkstation/Modules/BrainInspectorModule.cs` (MODIFY)

---

## System Execution Order

```
PredictedFixedStepSimulationSystemGroup:
  ResourceModifierApplySystem    [NEW, applies gear bonuses to pool]
  ResourceTickSystem             [NEW, regen/decay tick]
  PlayerStaminaSystem            [EXISTING, sprint/climb drain]
  PlayerAbilityCostSystem        [NEW, player ability resource gate]
  (Player ability systems...)

SimulationSystemGroup (Server|Local):
  EquippedStatsSystem            [EXISTING, aggregates gear stats]
  ResourceGenerationSystem       [NEW, rage on hit/take]
  AIStateTransitionSystem        [EXISTING]
  AbilitySelectionSystem         [MODIFIED, +resource check]
  AbilityCostDeductionSystem     [NEW, AI ability resource deduction]
  AbilityExecutionSystem         [EXISTING, phase lifecycle]
  CombatResolutionSystem         [EXISTING]

PredictedSimulationSystemGroup:
  ChannelActionSystem            [MODIFIED, +ResourcePerTick drain]

PresentationSystemGroup (Client|Local):
  ResourceUIBridgeSystem         [NEW, managed bridge]
  ResourceDebugSystem            [NEW, debug overlay]
```

---

## File Summary

### New Files

| # | File | Type | Phase |
|---|------|------|-------|
| 1 | `Combat/Resources/ResourceType.cs` | Enum | 0 |
| 2 | `Combat/Resources/CostTiming.cs` | Enum | 0 |
| 3 | `Combat/Resources/ResourceFlags.cs` | Flags Enum | 0 |
| 4 | `Combat/Resources/ResourceSlot.cs` | Blittable Struct | 0 |
| 5 | `Combat/Resources/ResourcePool.cs` | IComponentData | 0 |
| 6 | `Combat/Resources/ResourcePoolExtended.cs` | IComponentData + Link | 0 |
| 7 | `Combat/Resources/ResourcePoolBase.cs` | IComponentData | 3 |
| 8 | `Combat/Resources/Systems/ResourceTickSystem.cs` | ISystem, Burst | 1 |
| 9 | `Combat/Resources/Systems/ResourceGenerationSystem.cs` | ISystem | 1 |
| 10 | `Combat/Resources/Systems/AbilityCostDeductionSystem.cs` | ISystem | 2 |
| 11 | `Combat/Resources/Systems/PlayerAbilityCostSystem.cs` | ISystem, Burst | 2 |
| 12 | `Combat/Resources/Systems/ResourceModifierApplySystem.cs` | ISystem, Burst | 3 |
| 13 | `Combat/Resources/UI/ResourceUIBridgeSystem.cs` | SystemBase, Managed | 5 |
| 14 | `Combat/Resources/UI/ResourceUIRegistry.cs` | MonoBehaviour | 5 |
| 15 | `Combat/Resources/UI/IResourceBarProvider.cs` | Interface | 5 |
| 16 | `Combat/Resources/UI/ResourceBarViewModel.cs` | MonoBehaviour | 5 |
| 17 | `Combat/Resources/UI/ShaderResourceBarSync.cs` | MonoBehaviour | 5 |
| 18 | `Combat/Resources/Authoring/ResourcePoolAuthoring.cs` | Baker | 6 |
| 19 | `Combat/Resources/Debug/ResourceDebugSystem.cs` | SystemBase | 7 |

### Modified Files

| # | File | Changes | Phase |
|---|------|---------|-------|
| 1 | `AI/Components/AbilityDefinition.cs` | +ResourceCostType, +ResourceCostAmount, +ResourceCostTiming (8 bytes) | 2 |
| 2 | `AI/Authoring/AbilityDefinitionSO.cs` | +Resource cost inspector fields | 2 |
| 3 | `AI/Authoring/AbilityProfileAuthoring.cs` | +Resource cost baking | 2 |
| 4 | `AI/Systems/AbilitySelectionSystem.cs` | +Resource availability check in validation loop | 2 |
| 5 | `Player/Abilities/AbilityComponents.cs` | +ResourceCostType, +ResourceCostAmount (8 bytes) | 2 |
| 6 | `Weapons/Systems/ChannelActionSystem.cs` | +ResourcePerTick drain implementation | 2 |
| 7 | `Weapons/Components/WeaponActionComponents.cs` | +ChannelResourceType field on ChannelAction | 2 |
| 8 | `Items/Components/ItemStatBlock.cs` | +Resource modifier fields (24 bytes) | 3 |
| 9 | `Items/Components/PlayerEquippedStats.cs` | +Resource modifier aggregate fields | 3 |
| 10 | `Items/Systems/EquippedStatsSystem.cs` | +Resource modifier aggregation | 3 |
| 11 | `Player/Authoring/PlayerAuthoring.cs` | +ResourcePool baking | 6 |
| 12 | `Editor/AIWorkstation/Modules/BrainInspectorModule.cs` | +Resource pool display | 7 |

---

## Design Considerations

### Why Fixed-Slot Struct Over Dynamic Buffer

The player entity archetype is near the 16KB limit. Previous systems (`TargetingModuleAuthoring`, `SoundEventRequest`) already moved data to child entities to stay under budget. A dynamic buffer (`IBufferElementData`) adds:
- 40+ byte buffer header to the archetype
- Per-element storage (variable, in chunk or heap)
- Ghost serialization overhead per element

A fixed 2-slot `ResourcePool` costs exactly **64 bytes** on the archetype with no buffer overhead. This is less than the buffer header alone would cost. The tradeoff is a hard cap of 2 resource types per entity, which covers the vast majority of game designs:

| Game | Resource 1 | Resource 2 | Need 3+? |
|------|------------|------------|----------|
| WoW Warrior | Health (separate) | Rage | No |
| WoW Mage | Health (separate) | Mana | No |
| Diablo | Health (separate) | Resource (class-specific) | No |
| Overwatch | Health (separate) | Ability charges (per-ability) | No |
| Dark Souls | Stamina | FP (Focus Points) | No |
| Monster Hunter | Stamina | Sharpness (weapon-specific) | No |
| DOTA 2 | Health (separate) | Mana | No |
| Street Fighter | Health (separate) | Super meter | No |
| DIG (typical) | Stamina (existing) | Mana/Energy | No |

For the rare AI boss that needs 3-4 resource types (e.g., mana + rage + shield energy + combo), `ResourcePoolExtended` on a child entity provides overflow capacity without impacting the player archetype.

### NetCode Ghost Replication

```
ResourcePool : [GhostComponent(PrefabType = AllPredicted)]
  Slot0.Current : [GhostField]  -- predicted, rollback on misprediction
  Slot0.Max     : [GhostField]  -- rarely changes, delta-compressed to near-zero
  Slot1.Current : [GhostField]  -- predicted
  Slot1.Max     : [GhostField]  -- rarely changes
```

- **AllPredicted** means the component exists on the entity in prediction worlds. Client predicts resource deduction on ability cast (responsive feel), server validates and rolls back on misprediction (authoritative).
- **Delta compression** means unchanged fields (Max, RegenRate, etc.) cost near-zero bandwidth per snapshot. Only `Current` changes frequently, at 4 bytes per slot per snapshot.
- **Worst case bandwidth**: 2 slots x 4 bytes Current = 8 bytes per entity per snapshot. Negligible.

### Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `ResourceTickSystem` | < 0.02ms | Yes | 2 slots per entity, simple math, Burst vectorizable |
| `ResourceGenerationSystem` | < 0.03ms | Yes | Only processes entities with GenerateOnHit/Take flags |
| `AbilityCostDeductionSystem` | < 0.01ms | Yes | Single lookup + deduct per active cast |
| `PlayerAbilityCostSystem` | < 0.01ms | Yes | Single player entity, trivial |
| `ResourceModifierApplySystem` | < 0.01ms | Yes | Only on equipment change (dirty flag) |
| `ResourceUIBridgeSystem` | < 0.05ms | No (managed) | Single player, 2 slots, one Dictionary lookup each |
| **Total Resource Budget** | < 0.13ms | | All resource systems combined |

### 16KB Archetype Impact Analysis

Components added to the **player entity** by this EPIC:

| Component | Size | Notes |
|-----------|------|-------|
| `ResourcePool` | 64 bytes | 2 slots, ghost-replicated |
| `ResourcePoolBase` | 16 bytes | Base values for gear modifier math |
| **Total** | **80 bytes** | |

For comparison, components already on the player entity:
- `PlayerStamina`: 24 bytes
- `Health`: 8 bytes
- `CurrencyInventory`: 12 bytes
- `PlayerEquippedStats`: ~32 bytes
- `PlayerState`: ~24 bytes
- `CameraViewConfig`: ~64 bytes
- `PhysicsMass`: ~64 bytes

Adding 80 bytes is safe. The player archetype has headroom for ~800-1200 more bytes based on typical entity counts per chunk.

### Modularity & Swappability

**Removing the resource system entirely:**
1. Remove `ResourcePool` from `PlayerAuthoring` baker
2. `AbilityCostValidationSystem` / `PlayerAbilityCostSystem` find no `ResourcePool` -> skip all entities -> zero overhead
3. `ResourceTickSystem` finds no `ResourcePool` -> no-op
4. All abilities become free to cast (original behavior)
5. No existing system breaks

**Replacing the resource model:**
- `ResourcePool` is a plain IComponentData with public helper methods. Any system can read/write it.
- `ResourceTickSystem` is the only system that modifies `Current` for regen/decay. Replace it to change regen behavior.
- `AbilityCostDeductionSystem` is the only system that deducts resources. Replace it to change cost behavior.
- `ResourceUIBridgeSystem` reads `ResourcePool` through a standard singleton query. Replace the bridge to change UI pipeline.

**Adding new resource types:**
1. Add a new value to `ResourceType` enum (e.g., `Faith = 8`)
2. If using slots 0-1: configure via `ResourcePoolAuthoring`
3. If need slot 2-3: use `ResourcePoolExtended` on child entity
4. All systems automatically handle the new type (they iterate slots, not hardcoded types)

### Genre Configuration Examples

**FPS (DIG default):**
```
ResourcePool:
  Slot0: Type=Stamina, Max=100, RegenRate=10, RegenDelay=1.0, Flags=None
  Slot1: Type=Energy, Max=100, RegenRate=5, RegenDelay=2.0, Flags=None
```

**RPG / MMO:**
```
ResourcePool:
  Slot0: Type=Mana, Max=500, RegenRate=3, RegenDelay=5.0, Flags=None
  Slot1: Type=None (unused -- stamina handled by PlayerStamina)
```

**Action / Beat-em-up:**
```
ResourcePool:
  Slot0: Type=Rage, Max=100, RegenRate=0, DecayRate=5, Flags=DecaysWhenIdle|GenerateOnHit|GenerateOnTake
  Slot1: Type=Combo, Max=5, Flags=IsInteger|DecaysWhenIdle, DecayRate=1 (lose 1 point per second)
```

**MOBA:**
```
ResourcePool:
  Slot0: Type=Mana, Max=300, RegenRate=1.5, RegenDelay=0, Flags=None
  Slot1: Type=Energy, Max=200, RegenRate=10, RegenDelay=0, Flags=None
```

**Souls-like:**
```
ResourcePool:
  Slot0: Type=Stamina, Max=120, RegenRate=30, RegenDelay=0.5, Flags=None
  Slot1: Type=Mana, Max=80, RegenRate=0, RegenDelay=0, Flags=None (no regen -- use items)
```

---

## Integration Points

| System | EPIC | Integration |
|--------|------|-------------|
| `AbilityExecutionSystem` | 15.32 | AI ability lifecycle reads resource cost from AbilityDefinition, deduction at phase transitions |
| `AbilitySelectionSystem` | 15.32 | AI ability selection skips abilities with insufficient resources |
| `AbilityLifecycleSystem` | Player abilities | Player ability start blocked by PlayerAbilityCostSystem if insufficient resources |
| `ChannelActionSystem` | 15.7 | Channel drain reads ResourcePool instead of hardcoded values |
| `EquippedStatsSystem` | 16.6 | Aggregates resource modifiers from ItemStatBlock on equipped items |
| `PlayerStaminaSystem` | Core | Coexists with ResourcePool[Stamina] during migration period |
| `CombatResolutionSystem` | 15.x | CombatResultEvent consumed by ResourceGenerationSystem for rage/hit generation |
| `CombatUIBridgeSystem` | 15.9/15.22 | Pattern reference for ResourceUIBridgeSystem (same architecture) |
| `AttributeRegenSystem` | Traits | Independent system for non-combat attributes. Not replaced. |
| `AI Workstation` | 16.1 | BrainInspectorModule extended with resource pool display |

---

## Verification Checklist

### Core Resource Pool

- [x] Entity with `ResourcePool` configured for Mana: Current starts at Max
- [x] `ResourceTickSystem`: Mana regens at RegenRate after RegenDelay seconds of no drain
- [x] `ResourceTickSystem`: Rage decays at DecayRate when DecaysWhenIdle and no recent activity
- [x] `ResourceTickSystem`: Overflow Current decays back to Max when DecaysWhenFull
- [x] `ResourceTickSystem`: Combo points remain integers (IsInteger flag)
- [x] `ResourceTickSystem`: PausedRegen flag prevents regeneration
- [x] Entity without `ResourcePool`: no errors, no resource processing (zero-cost)

### Ability Cost Integration (AI)

- [x] AI ability with ResourceCostType=Mana, Amount=30: skipped by AbilitySelectionSystem when mana < 30
- [x] AI ability with CostTiming=OnCast: mana deducted when entering Casting phase
- [x] AI ability with CostTiming=PerTick: mana deducted each TickInterval during Active phase
- [x] AI ability with CostTiming=PerTick: ability interrupted when resource depleted mid-cast
- [x] AI ability with CostTiming=OnComplete: mana deducted when entering Recovery phase
- [x] AI ability with ResourceCostType=None: no resource check (free, backward compatible)
- [x] AI entity without ResourcePool: all abilities treated as free (backward compatible)

### Ability Cost Integration (Player)

- [x] Player ability with ResourceCostType=Energy, Amount=25: blocked when energy < 25
- [x] Player ability successfully deducts energy on cast start
- [x] Player ability with no resource cost: works identically to pre-EPIC behavior
- [x] Player entity without ResourcePool: all abilities work (free)

### Channel Resource Drain

- [x] ChannelAction with ChannelResourceType=Mana: drains ResourcePerTick per interval
- [x] Channel cancels when resource depleted
- [x] ChannelAction with ChannelResourceType=None: works as before (no drain)

### Equipment Modifiers

- [x] Equipping item with MaxManaBonus=50: ResourcePool.Slot[Mana].Max increases by 50
- [x] Equipping item with ManaRegenBonus=2: ResourcePool.Slot[Mana].RegenRate increases by 2
- [x] Unequipping item: Max/Regen return to base values
- [x] Unequipping item that reduces Max below Current: Current clamped to new Max

### Resource Generation

- [x] Entity with ResourceFlags.GenerateOnHit: resource increases by GenerateAmount when dealing damage
- [x] Entity with ResourceFlags.GenerateOnTake: resource increases by GenerateAmount when taking damage
- [x] Generation respects Max cap (no overflow unless CanOverflow flag set)

### NetCode

- [x] Resource deduction predicted on client: immediate feel on ability cast
- [x] Server validates resource cost: misprediction rolled back (ability cancelled client-side)
- [x] ResourcePool ghost-replicated: remote clients see correct resource values (for spectator/party frames)
- [x] Delta compression: stable ResourcePool (no changes) costs near-zero bandwidth

### UI

- [x] ResourceBarViewModel receives updates from ResourceUIBridgeSystem
- [x] Mana bar fills/drains correctly when resources change
- [x] Multiple resource bars (mana + energy) display simultaneously and independently
- [x] Resource bar shows low/empty visual states at configured thresholds
- [x] StaminaViewModel + ShaderStaminaBarSync continue working (no regression)

### Performance

- [x] 100 entities with ResourcePool: ResourceTickSystem < 0.02ms
- [x] 50 AI entities with resource-costed abilities: AbilitySelectionSystem overhead < 0.01ms
- [x] Single player entity: PlayerAbilityCostSystem < 0.01ms
- [x] ResourceUIBridgeSystem (managed): < 0.05ms per frame

### Backward Compatibility

- [x] Existing PlayerStamina sprint/climb drain: no behavior change
- [x] Existing stamina UI (StaminaViewModel, ShaderStaminaBarSync): no regression
- [x] Existing AI abilities without resource costs: no behavior change
- [x] Existing player abilities without resource costs: no behavior change
- [x] Existing ChannelAction without ChannelResourceType: no behavior change
- [x] Existing ItemStatBlock without resource modifiers: no behavior change (new fields default 0)
- [x] Enemies without ResourcePool component: AbilitySelectionSystem treats all abilities as free

### Migration Path

- [x] PlayerStamina and ResourcePool coexist on same entity without conflict
- [x] Both systems write to their own data only (no cross-write)
- [x] ResourcePool[Stamina] can be configured independently of PlayerStamina
- [x] Migration steps documented in setup guide
