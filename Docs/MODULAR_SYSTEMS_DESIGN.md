# Modular Systems Design: Optional Feature Architecture

> **Status:** Draft  
> **Created:** 2025-12-13  
> **Scope:** Epic 2 (EVA Foundation) and future optional game systems  
> **Goal:** Design systems that are game-specific (Oxygen, Jetpack, Tools) to be fully optional and reusable across different game types.

---

## Table of Contents

1. [Problem Statement](#problem-statement)
2. [Design Principles](#design-principles)
3. [Trade-offs & Performance](#trade-offs--performance)
4. [Architecture Overview](#architecture-overview)
5. [Generic Resource Framework](#generic-resource-framework)
6. [Feature Module Pattern](#feature-module-pattern)
7. [Epic 2 Module Breakdown](#epic-2-module-breakdown)
8. [Opt-In Registration Pattern](#opt-in-registration-pattern)
9. [Assembly Definition Strategy](#assembly-definition-strategy)
10. [Configuration & ScriptableObjects](#configuration--scriptableobjects)
11. [Example: Oxygen as Optional Module](#example-oxygen-as-optional-module)
12. [Example: Tool System as Optional Module](#example-tool-system-as-optional-module)
13. [Integration Patterns](#integration-patterns)
14. [Testing Strategy](#testing-strategy)

---

## Problem Statement

Epic 2 defines systems that are **essential for DIG** but **irrelevant for many other games**:

| System | DIG Usage | Other Games |
|--------|-----------|-------------|
| **Oxygen** | Core survival mechanic | Not needed in fantasy, modern shooters, etc. |
| **Jetpack** | EVA traversal | Only for sci-fi/space games |
| **Magnetic Boots** | Zero-G walking | Very niche |
| **Radiation** | Environmental hazard | Only for nuclear/space themes |
| **Temperature** | Survival pressure | Only survival games |
| **Suit Integrity** | Damage proxy | Space games only |
| **Drill Tool** | Voxel mining | Only voxel/mining games |
| **Explosives** | Voxel destruction | Many games, but not all |

If we hardcode these into the runtime, we ship bloated, DIG-specific code to Asset Store customers who don't need it.

### Requirements

1. **Zero overhead** — If a game doesn't use Oxygen, no Oxygen systems should run or compile
2. **No code changes** — Enabling/disabling features shouldn't require editing core systems
3. **Clean dependencies** — Core systems shouldn't reference optional modules
4. **Easy integration** — Adding Oxygen to a project should be trivial
5. **Testable in isolation** — Each module should be unit-testable without the full game

---

## Design Principles

### Principle 1: Inversion of Control

**Bad:** Core systems know about optional features
```csharp
// ❌ PlayerMovementSystem references Oxygen directly
if (oxygenTank.Current <= 0)
    velocity *= 0.5f; // Slowed when suffocating
```

**Good:** Optional features hook into core systems via events/interfaces
```csharp
// ✅ OxygenMovementModifierSystem adds a modifier
// Core movement system reads from generic modifier buffer
foreach (var modifier in movementModifiers)
    velocity *= modifier.SpeedMultiplier;
```

### Principle 2: Component Presence = Feature Enabled

If an entity doesn't have `OxygenTank`, no oxygen systems query it. This is natural in ECS — systems only run on entities with matching components.

```csharp
// Only runs on entities that HAVE OxygenTank
[BurstCompile]
partial struct OxygenDepletionJob : IJobEntity
{
    void Execute(ref OxygenTank tank, in EVAState eva) { ... }
}
```

### Principle 3: Assembly Isolation

Each optional feature lives in its own assembly. Games only reference the assemblies they need.

```
DIG.Runtime.Core           # Always included
DIG.Runtime.Character      # Always included
DIG.Runtime.Survival.Oxygen    # Optional
DIG.Runtime.Survival.Temperature   # Optional
DIG.Runtime.EVA.Jetpack        # Optional
DIG.Runtime.Tools              # Optional
```

### Principle 4: Configuration over Code

Feature behavior controlled by ScriptableObjects or BlobAssets, not hardcoded values.

```csharp
// ❌ Hardcoded
public const float OxygenDepletionRate = 1.0f;

// ✅ Configurable
[CreateAssetMenu]
public class OxygenSettings : ScriptableObject
{
    public float DepletionRatePerSecond = 1.0f;
    public float WarningThreshold = 25f;
    public float CriticalThreshold = 10f;
}
```

---

## Trade-offs & Performance

This modular architecture has real trade-offs. Here's an honest assessment:

### ✅ Pros

| Benefit | Description |
|---------|-------------|
| **Reusability** | Sell modules on Asset Store; use in other games without DIG baggage |
| **Compile-time isolation** | Unused modules don't compile into builds; smaller binaries |
| **Testability** | Unit test each module in isolation without full game |
| **Team scaling** | Different devs can own different modules with clear boundaries |
| **Maintenance** | Bug in Oxygen? Fix it in one place; all games benefit |
| **Onboarding** | New devs can understand one module without learning entire codebase |
| **Dependency clarity** | `.asmdef` files enforce direction; no accidental coupling |

### ❌ Cons & Performance Costs

| Cost | Description | Severity |
|------|-------------|----------|
| **System scheduling overhead** | More systems = more `OnUpdate` calls, even if they early-out. ECS optimizes this, but it's not zero. | Low |
| **Generic vs specialized** | `ConsumableResource` is generic; can't Burst-optimize for oxygen-specific math. Marker tags (`OxygenTag`) add component lookups. | Low-Medium |
| **Modifier buffer iteration** | Reading from `DynamicBuffer<MovementSpeedModifier>` is slower than direct field access. Adds cache misses. | Medium |
| **Archetype fragmentation** | Many small components from different modules = larger archetypes = more memory per entity, worse cache coherency. | Medium |
| **Blob asset dereferences** | Getting settings from `BlobAssetReference<T>` adds pointer chase vs hardcoded constants. | Low |
| **Assembly loading** | Many `.asmdef` files = more assemblies to load at startup. No runtime cost after load. | Low (startup only) |
| **Indirection** | Events, buffers, and capability tags add layers of indirection vs direct component access. | Low-Medium |
| **Complexity** | More abstractions to learn; "where does this modifier come from?" debugging is harder. | Medium |

### Performance Deep Dive

#### 1. System Scheduling Overhead

Each system in ECS has:
- `OnCreate` (once)
- `OnUpdate` (every frame, even if query matches nothing)
- Chunk iteration setup

**Mitigation:**
```csharp
// Use RequireForUpdate to skip entirely when no entities match
[RequireMatchingQueriesForUpdate]
public partial struct OxygenDepletionSystem : ISystem { }
```

With `RequireMatchingQueriesForUpdate`, if no entities have `OxygenTag`, the system's `OnUpdate` is never called. **Cost: ~0**.

#### 2. Generic Resource vs Specialized Component

**Generic approach (this doc):**
```csharp
public struct ConsumableResource : IComponentData
{
    public float Current, Max, DepletionRate, RegenRate; // 16 bytes
}
public struct OxygenTag : IComponentData { } // 0 bytes (tag)
```

**Specialized approach:**
```csharp
public struct OxygenTank : IComponentData
{
    public float Current, Max, DepletionRate, LeakMultiplier; // 16 bytes
}
```

**Performance difference:**
- Generic requires two components per entity → archetype has more components
- Systems need `WithAll<OxygenTag>()` filter → extra comparison per chunk
- But: Both are Burst-compiled, both fit in cache line, difference is ~nanoseconds

**Verdict:** Negligible for <10,000 entities. Profile if you have more.

#### 3. Modifier Buffers

**Direct access (hardcoded):**
```csharp
void Execute(ref Velocity vel, in OxygenTank oxygen)
{
    if (oxygen.Current <= 0)
        vel.Value *= 0.5f; // 1 branch, 1 multiply
}
```

**Modifier buffer (modular):**
```csharp
void Execute(ref Velocity vel, in DynamicBuffer<SpeedModifier> modifiers)
{
    foreach (var mod in modifiers)
        vel.Value *= mod.Multiplier; // N iterations, N multiplies
}
```

**Performance difference:**
- Buffer iteration: ~5-10ns per modifier
- Memory: Buffer pointer chase + element iteration
- Typical case: 0-3 modifiers per entity → 15-30ns overhead per entity

**Mitigation:**
- Use `[ChangeFilter]` to only process entities whose modifiers changed
- For hot paths, consider hybrid: core modifiers inline, extensible modifiers in buffer

#### 4. Archetype Fragmentation

Each unique component combination = new archetype. 

**Example:**
- Player with just Health: `Archetype A`
- Player with Health + OxygenTag: `Archetype B`
- Player with Health + OxygenTag + JetpackTag: `Archetype C`

More archetypes = smaller chunk utilization = more cache misses when iterating.

**Mitigation:**
- Combine related tags into a single flags component:
  ```csharp
  public struct SurvivalFeatures : IComponentData
  {
      public SurvivalFlags Flags; // Oxygen | Radiation | Temperature
  }
  ```
- Use `IEnableableComponent` for toggleable features (same archetype, different enabled state)

#### 5. When to Use This Architecture

| Scenario | Recommendation |
|----------|----------------|
| Asset Store package | ✅ Use modular design — customers expect pick-and-choose |
| Single game, small team | ⚠️ Consider simpler approach; modularity has overhead |
| Single game, large team | ✅ Use modules for team boundaries |
| Performance-critical (1000s of entities) | ⚠️ Profile; may need specialized hot paths |
| Rapid prototyping | ❌ Hardcode first, modularize later |

### Benchmarks (Estimated)

| Operation | Direct Access | Modular Approach | Delta |
|-----------|---------------|------------------|-------|
| Oxygen depletion (1000 entities) | ~50 μs | ~55 μs | +10% |
| Movement with 2 modifiers (1000 entities) | ~40 μs | ~60 μs | +50% |
| System scheduling (20 systems, 0 entities) | ~5 μs | ~5 μs | 0% |
| Startup assembly load (15 assemblies) | N/A | +50-100 ms | One-time |

*Benchmarks are estimates. Profile your specific use case.*

### Hybrid Approach: Best of Both

For DIG specifically, consider:

1. **Core hot paths stay specialized** — `OxygenTank` as a dedicated component, not generic `ConsumableResource`
2. **Modules use assembly isolation** — Still get compile-time separation
3. **Extension points use modifiers** — Non-hot paths (UI, audio triggers) use the generic patterns

This gives you:
- ✅ Maximum performance for survival systems (runs every frame on every player)
- ✅ Modularity for tooling, editor, and non-critical paths
- ✅ Asset Store viability (modules are still separable)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Game Layer (DIG)                               │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ DIGBootstrap: Registers which optional modules are active       │    │
│  │ DIGPlayerAuthoring: Adds OxygenTank, Jetpack, Tools to player   │    │
│  └─────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
          ┌─────────────────────────┼─────────────────────────┐
          ▼                         ▼                         ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  Optional Module │    │  Optional Module │    │  Optional Module │
│    EVA.Oxygen    │    │   EVA.Jetpack    │    │      Tools       │
│  ┌────────────┐  │    │  ┌────────────┐  │    │  ┌────────────┐  │
│  │ Components │  │    │  │ Components │  │    │  │ Components │  │
│  │ Systems    │  │    │  │ Systems    │  │    │  │ Systems    │  │
│  │ Authoring  │  │    │  │ Authoring  │  │    │  │ Authoring  │  │
│  └────────────┘  │    │  └────────────┘  │    │  └────────────┘  │
└────────┬─────────┘    └────────┬─────────┘    └────────┬─────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         Core Runtime Layer                               │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐        │
│  │ Character  │  │   Health   │  │   Input    │  │ Resources  │        │
│  │ Controller │  │   System   │  │   System   │  │ Framework  │        │
│  └────────────┘  └────────────┘  └────────────┘  └────────────┘        │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Generic Resource Framework

Instead of building separate Oxygen, Fuel, Battery, Ammo systems, we create a **generic consumable resource framework** that all of these can use.

### Core Resource Components (in `Runtime/Resources/`)

```csharp
/// <summary>
/// Generic consumable resource. Used for Oxygen, Fuel, Battery, Ammo, etc.
/// </summary>
public struct ConsumableResource : IComponentData
{
    public float Current;
    public float Max;
    public float DepletionRate;      // Per-second drain when active
    public float RegenRate;          // Per-second regen when conditions met
    public float WarningThreshold;   // Percent (0-1) to trigger warning
    public float CriticalThreshold;  // Percent (0-1) to trigger critical
}

/// <summary>
/// Tag indicating which resource type this is. Allows systems to filter.
/// </summary>
public struct ResourceTypeId : IComponentData
{
    public int TypeId; // Matches ResourceDefinition.TypeId
}

/// <summary>
/// Current state flags for UI/audio.
/// </summary>
public struct ResourceWarningState : IComponentData
{
    public bool IsWarning;
    public bool IsCritical;
    public bool IsDepleted;
}

/// <summary>
/// Optional: Defines what happens when depleted.
/// </summary>
public struct ResourceDepletionEffect : IComponentData
{
    public DepletionEffectType EffectType; // None, Damage, Disable, Kill
    public float DamagePerSecond;          // If EffectType == Damage
    public Entity DisableSystem;           // If EffectType == Disable
}
```

### Resource Definition (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "DIG/Resources/Resource Definition")]
public class ResourceDefinition : ScriptableObject
{
    public int TypeId;           // Unique ID for this resource type
    public string DisplayName;
    public Sprite Icon;
    
    [Header("Defaults")]
    public float DefaultMax = 100f;
    public float DefaultDepletionRate = 1f;
    public float DefaultRegenRate = 0f;
    public float WarningThreshold = 0.25f;
    public float CriticalThreshold = 0.1f;
    
    [Header("Depletion Effects")]
    public DepletionEffectType DepletionEffect;
    public float DepletionDamagePerSecond;
}

public enum DepletionEffectType { None, Damage, Disable, Kill }
```

### Generic Resource Systems

```csharp
/// <summary>
/// Depletes resources when conditions are met.
/// Runs on all entities with ConsumableResource + ResourceDepleting tag.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ResourceDepletionSystem : ISystem
{
    [BurstCompile]
    partial struct DepletionJob : IJobEntity
    {
        public float DeltaTime;
        
        void Execute(ref ConsumableResource resource, in ResourceDepleting _)
        {
            resource.Current = math.max(0, resource.Current - resource.DepletionRate * DeltaTime);
        }
    }
}

/// <summary>
/// Updates warning/critical/depleted flags.
/// </summary>
[BurstCompile]
public partial struct ResourceWarningSystem : ISystem { ... }

/// <summary>
/// Applies depletion effects (damage, etc.) when resource is empty.
/// </summary>
[BurstCompile]
public partial struct ResourceDepletionEffectSystem : ISystem { ... }
```

### How Oxygen Uses the Framework

**Oxygen-specific components** (in optional `DIG.Runtime.Survival.Oxygen` assembly):

```csharp
/// <summary>
/// Marker tag: This resource is oxygen. Allows oxygen-specific systems to query.
/// </summary>
public struct OxygenTag : IComponentData { }

/// <summary>
/// Oxygen-specific: leak multiplier from suit damage.
/// </summary>
public struct OxygenLeakModifier : IComponentData
{
    public float Multiplier; // 1.0 = normal, 2.0 = double drain
}
```

**Oxygen-specific systems:**

```csharp
/// <summary>
/// Modifies oxygen depletion rate based on suit integrity.
/// </summary>
[UpdateBefore(typeof(ResourceDepletionSystem))]
public partial struct OxygenLeakSystem : ISystem
{
    void Execute(ref ConsumableResource resource, in OxygenTag _, in OxygenLeakModifier leak)
    {
        // Temporarily boost depletion rate based on leak
        resource.DepletionRate *= leak.Multiplier;
    }
}

/// <summary>
/// Adds ResourceDepleting tag when in EVA (oxygen drains in EVA only).
/// </summary>
public partial struct OxygenEVAActivationSystem : ISystem
{
    void OnUpdate(ref SystemState state)
    {
        // Add ResourceDepleting to entities with OxygenTag + EVAState.IsInEVA
        // Remove ResourceDepleting when not in EVA
    }
}
```

---

## Feature Module Pattern

Each optional feature follows this structure:

```
Runtime/
└── Survival/
    └── Oxygen/
        ├── Oxygen.asmdef              # Assembly definition
        ├── Components/
        │   ├── OxygenTag.cs           # Marker for oxygen resources
        │   └── OxygenLeakModifier.cs  # Oxygen-specific modifier
        ├── Systems/
        │   ├── OxygenLeakSystem.cs
        │   ├── OxygenEVAActivationSystem.cs
        │   └── OxygenWarningUISystem.cs
        ├── Authoring/
        │   └── OxygenAuthoring.cs     # Adds oxygen to entity
        └── Config/
            └── OxygenSettings.asset   # Default settings
```

### Assembly Definition

```json
{
    "name": "DIG.Runtime.Survival.Oxygen",
    "rootNamespace": "DIG.Runtime.Survival.Oxygen",
    "references": [
        "DIG.Runtime.Core",
        "DIG.Runtime.Resources",
        "DIG.Runtime.Character",
        "Unity.Entities",
        "Unity.Burst"
    ],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

---

## Epic 2 Module Breakdown

### Module Map

| Epic 2 Subepic | Module Name | Depends On |
|----------------|-------------|------------|
| 2.1 EVA State & Oxygen | `Survival.EVA`, `Survival.Oxygen`, `Survival.Radiation` | Core, Resources, Character |
| 2.2 EVA Movement | `EVA.Jetpack`, `EVA.MagneticBoots` | Core, Character, EVA |
| 2.3 Basic Tools | `Tools.Core`, `Tools.Welder`, `Tools.Drill` | Core, Resources |
| 2.4 Throwables | `Tools.Throwables` | Core, Tools.Core |
| 2.5 Explosives | `Tools.Explosives` | Core, Tools.Core, Physics |
| 2.6 Inventory | `Inventory` | Core, Resources |
| 2.7 Hazards | `Survival.Temperature`, `Survival.SuitIntegrity` | Core, Resources, Character |

### Dependency Graph

```
                      ┌──────────────┐
                      │     Core     │
                      └──────┬───────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
        ┌──────────┐   ┌──────────┐   ┌──────────┐
        │Resources │   │Character │   │ Inventory│
        └────┬─────┘   └────┬─────┘   └──────────┘
             │              │
    ┌────────┼────────┬─────┼─────┬────────────────┐
    ▼        ▼        ▼     ▼     ▼                ▼
┌───────┐┌───────┐┌───────┐┌───────┐┌───────┐┌──────────┐
│Oxygen ││ Fuel  ││Battery││ Temp  ││Radiat.││SuitInteg.│
│       ││(Jetpk)││(Tools)││       ││       ││          │
└───────┘└───────┘└───────┘└───────┘└───────┘└──────────┘
                      │
              ┌───────┴───────┐
              ▼               ▼
        ┌──────────┐    ┌──────────┐
        │Tools.Core│    │Throwables│
        └────┬─────┘    └──────────┘
             │
    ┌────────┼────────┐
    ▼        ▼        ▼
┌───────┐┌───────┐┌──────────┐
│Welder ││ Drill ││Explosives│
└───────┘└───────┘└──────────┘
```

---

## Opt-In Registration Pattern

For systems that need to know which optional modules are active at runtime, use a **Feature Registry**.

### Feature Registry (Singleton)

```csharp
/// <summary>
/// Singleton that tracks which optional features are enabled.
/// Populated at bootstrap time.
/// </summary>
public struct FeatureRegistry : IComponentData
{
    public FeatureFlags EnabledFeatures;
}

[Flags]
public enum FeatureFlags : ulong
{
    None = 0,
    Oxygen = 1 << 0,
    Radiation = 1 << 1,
    Temperature = 1 << 2,
    SuitIntegrity = 1 << 3,
    Jetpack = 1 << 4,
    MagneticBoots = 1 << 5,
    Tools = 1 << 6,
    Explosives = 1 << 7,
    Inventory = 1 << 8,
    // ... up to 64 features
}
```

### Bootstrap Registration

```csharp
// In DIG's game-specific bootstrap
public class DIGBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        // Create feature registry
        var registry = new FeatureRegistry
        {
            EnabledFeatures = 
                FeatureFlags.Oxygen |
                FeatureFlags.Radiation |
                FeatureFlags.Temperature |
                FeatureFlags.Jetpack |
                FeatureFlags.Tools |
                FeatureFlags.Explosives |
                FeatureFlags.Inventory
        };
        
        // ... create world with registry singleton
    }
}
```

### Conditional System Execution

```csharp
[BurstCompile]
public partial struct OxygenDepletionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Early out if oxygen not enabled
        if (!SystemAPI.TryGetSingleton<FeatureRegistry>(out var registry))
            return;
        if ((registry.EnabledFeatures & FeatureFlags.Oxygen) == 0)
            return;
        
        // ... normal processing
    }
}
```

**Note:** For maximum efficiency, prefer using **component presence** over runtime flags. If `OxygenTank` component doesn't exist on any entities, `OxygenDepletionSystem` naturally does nothing. The FeatureRegistry is mainly for:
- UI systems that need to know what to display
- Systems that create entities (should they add oxygen?)
- Editor tooling

---

## Configuration & ScriptableObjects

### Per-Module Settings Pattern

Each module has a settings ScriptableObject:

```csharp
// Runtime/Survival/Oxygen/Config/OxygenSettings.cs
[CreateAssetMenu(menuName = "DIG/Survival/Oxygen Settings")]
public class OxygenSettings : ScriptableObject
{
    [Header("Tank Defaults")]
    public float MaxOxygen = 100f;
    public float DepletionRatePerSecond = 1f;
    
    [Header("Thresholds")]
    [Range(0, 1)] public float WarningThreshold = 0.25f;
    [Range(0, 1)] public float CriticalThreshold = 0.10f;
    
    [Header("Depletion Effects")]
    public float SuffocationDamagePerSecond = 10f;
    
    [Header("Audio")]
    public AudioClip WarningSound;
    public AudioClip CriticalSound;
    public AudioClip SuffocatingLoop;
}
```

### Baking Settings to BlobAsset

```csharp
public class OxygenSettingsAuthoring : MonoBehaviour
{
    public OxygenSettings Settings;
}

public class OxygenSettingsBaker : Baker<OxygenSettingsAuthoring>
{
    public override void Bake(OxygenSettingsAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        
        // Bake to blob for Burst-compatible access
        var builder = new BlobBuilder(Allocator.Temp);
        ref var blob = ref builder.ConstructRoot<OxygenSettingsBlob>();
        blob.MaxOxygen = authoring.Settings.MaxOxygen;
        blob.DepletionRate = authoring.Settings.DepletionRatePerSecond;
        // ...
        
        var blobRef = builder.CreateBlobAssetReference<OxygenSettingsBlob>(Allocator.Persistent);
        AddBlobAsset(ref blobRef, out _);
        AddComponent(entity, new OxygenSettingsBlobComponent { Value = blobRef });
    }
}
```

---

## Example: Oxygen as Optional Module

### Files

```
Runtime/Survival/Oxygen/
├── Oxygen.asmdef
├── Components/
│   ├── OxygenTag.cs
│   ├── OxygenLeakModifier.cs
│   └── OxygenSettingsBlobComponent.cs
├── Systems/
│   ├── OxygenEVAActivationSystem.cs
│   ├── OxygenLeakSystem.cs
│   ├── OxygenWarningSystem.cs
│   └── OxygenSuffocationDamageSystem.cs
├── Authoring/
│   ├── OxygenTankAuthoring.cs
│   └── OxygenSettingsAuthoring.cs
└── Config/
    └── DefaultOxygenSettings.asset
```

### Adding Oxygen to a Player

```csharp
// In PlayerAuthoring (game-specific)
public class DIGPlayerAuthoring : MonoBehaviour
{
    public OxygenSettings OxygenSettings;
    public bool EnableOxygen = true;
}

public class DIGPlayerBaker : Baker<DIGPlayerAuthoring>
{
    public override void Bake(DIGPlayerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        // Core components
        AddComponent<PlayerTag>(entity);
        AddComponent<Health>(entity, new Health { Current = 100, Max = 100 });
        
        // Optional: Oxygen
        if (authoring.EnableOxygen)
        {
            AddComponent(entity, new ConsumableResource
            {
                Current = authoring.OxygenSettings.MaxOxygen,
                Max = authoring.OxygenSettings.MaxOxygen,
                DepletionRate = authoring.OxygenSettings.DepletionRatePerSecond,
                WarningThreshold = authoring.OxygenSettings.WarningThreshold,
                CriticalThreshold = authoring.OxygenSettings.CriticalThreshold
            });
            AddComponent<OxygenTag>(entity);
            AddComponent<ResourceWarningState>(entity);
        }
    }
}
```

### Using Oxygen in Another Game

Another game can use the Oxygen module without any DIG code:

```csharp
// SpaceGame's player authoring
public class SpaceMarineAuthoring : MonoBehaviour
{
    public bool HasOxygenTank = true;
}

public class SpaceMarineBaker : Baker<SpaceMarineAuthoring>
{
    public override void Bake(SpaceMarineAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        if (authoring.HasOxygenTank)
        {
            // Just add the components - oxygen systems will pick them up
            AddComponent(entity, new ConsumableResource { Current = 100, Max = 100, DepletionRate = 0.5f });
            AddComponent<OxygenTag>(entity);
        }
    }
}
```

---

## Example: Tool System as Optional Module

### Generic Tool Framework

```
Runtime/Tools/Core/
├── Tools.Core.asmdef
├── Components/
│   ├── Tool.cs                    # Base tool component
│   ├── ToolDurability.cs
│   ├── ToolUsageState.cs
│   ├── ActiveTool.cs              # On player, ref to equipped tool
│   └── ToolOwnership.cs           # Buffer on player
├── Systems/
│   ├── ToolSwitchingSystem.cs
│   ├── ToolRaycastSystem.cs
│   ├── ToolDurabilitySystem.cs
│   └── ToolUsageInputSystem.cs
└── Authoring/
    └── ToolAuthoring.cs
```

### Tool-Specific Modules

```
Runtime/Tools/Welder/
├── Tools.Welder.asmdef            # References Tools.Core
├── Components/
│   └── WelderTool.cs
├── Systems/
│   └── WelderUsageSystem.cs
└── Authoring/
    └── WelderAuthoring.cs

Runtime/Tools/Drill/
├── Tools.Drill.asmdef             # References Tools.Core, Voxel (optional)
├── Components/
│   └── DrillTool.cs
├── Systems/
│   └── DrillUsageSystem.cs
└── Authoring/
    └── DrillAuthoring.cs
```

### Using Tools Without DIG

A game can use just the Welder without Drill:

```json
// Their project's assembly references
{
    "references": [
        "DIG.Runtime.Core",
        "DIG.Runtime.Tools.Core",
        "DIG.Runtime.Tools.Welder"
        // Note: No Drill reference
    ]
}
```

---

## Integration Patterns

### Pattern 1: Event-Based Integration

Optional modules fire events that other modules can listen to:

```csharp
// Oxygen fires depleted event
public struct OxygenDepletedEvent : IComponentData
{
    public Entity PlayerEntity;
    public float TimeOxygenWasZero;
}

// Health system (core) listens for damage requests
// Oxygen suffocation system writes damage requests
```

### Pattern 2: Modifier Buffers

Core systems read from modifier buffers that optional modules write to:

```csharp
// Core movement reads all modifiers
public struct MovementSpeedModifier : IBufferElementData
{
    public float Multiplier;
    public FixedString32 Source; // "Oxygen", "Temperature", "Encumbered"
}

// Oxygen adds modifier when low
// Temperature adds modifier when cold
// Inventory adds modifier when overencumbered
```

### Pattern 3: Capability Tags

Optional modules add capability tags that core systems check:

```csharp
// Added by EVA module
public struct CanEVA : IComponentData { }

// Added by Jetpack module  
public struct HasJetpack : IComponentData { }

// Core movement can check for these
if (SystemAPI.HasComponent<HasJetpack>(entity))
{
    // Allow vertical thrust input
}
```

---

## Testing Strategy

### Unit Testing Optional Modules

Each module can be tested in isolation:

```csharp
[TestFixture]
public class OxygenSystemTests
{
    private World _world;
    private EntityManager _em;
    
    [SetUp]
    public void Setup()
    {
        _world = new World("Test");
        _em = _world.EntityManager;
        
        // Only add oxygen systems - no DIG dependencies
        var group = _world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        group.AddSystemToUpdateList(_world.CreateSystem<ResourceDepletionSystem>());
        group.AddSystemToUpdateList(_world.CreateSystem<OxygenLeakSystem>());
    }
    
    [Test]
    public void Oxygen_Depletes_When_ResourceDepleting_Tag_Present()
    {
        var entity = _em.CreateEntity();
        _em.AddComponentData(entity, new ConsumableResource { Current = 100, Max = 100, DepletionRate = 10 });
        _em.AddComponent<OxygenTag>(entity);
        _em.AddComponent<ResourceDepleting>(entity);
        
        _world.Update();
        
        var oxygen = _em.GetComponentData<ConsumableResource>(entity);
        Assert.Less(oxygen.Current, 100);
    }
}
```

### Integration Testing

For testing multiple modules together, create a minimal test world with only the relevant systems.

---

## Summary

| Concept | Implementation |
|---------|---------------|
| **Generic primitives** | `ConsumableResource`, `ResourceWarningState`, `MovementSpeedModifier` |
| **Marker tags** | `OxygenTag`, `JetpackTag`, `WelderToolTag` identify which resource/tool |
| **Separate assemblies** | Each feature in its own `.asmdef` with explicit dependencies |
| **Component presence** | Systems only query entities that have the relevant components |
| **Feature Registry** | Optional singleton for runtime feature detection |
| **ScriptableObject config** | Settings defined in assets, baked to BlobAssets |
| **Event-based integration** | Optional modules fire events, core systems listen |
| **Modifier buffers** | Optional modules write modifiers, core systems apply them |

This architecture ensures:
- ✅ DIG gets all its systems working together
- ✅ Other games can pick only the modules they need
- ✅ Asset Store packages are clean and focused
- ✅ No compile-time or runtime overhead for unused features
- ✅ Each module is testable in isolation

---

*Review and adjust based on team preferences.*
