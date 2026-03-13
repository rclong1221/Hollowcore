# EPIC 14.3: Boss Arena System

**Status**: Planning
**Epic**: EPIC 14 — Boss System & Variant Clauses
**Dependencies**: EPIC 14.1 (BossDefinitionSO, ArenaDefinitionSO); Framework: Combat/ (EncounterState)

---

## Overview

Each boss fights in a unique arena that is itself a gameplay element. Arenas have environmental hazards that affect both the player and the boss, interactable objects (cover, traps, levers), and can reconfigure between boss phases. The 15 arena types are drawn from the GDD's district themes. Arena state is tracked in ECS and driven by the boss encounter flow.

---

## Component Definitions

### ArenaState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/ArenaComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    /// <summary>
    /// Runtime state of the boss arena. Lives on the arena root entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ArenaState : IComponentData
    {
        /// <summary>ArenaDefinitionSO ID.</summary>
        [GhostField] public int ArenaId;

        /// <summary>Current arena configuration index (arenas can reconfigure between phases).</summary>
        [GhostField] public byte CurrentLayoutIndex;

        /// <summary>Bitmask of active hazard slots (up to 16 hazards per arena).</summary>
        [GhostField] public ushort ActiveHazardMask;

        /// <summary>Bitmask of destroyed/consumed interactables.</summary>
        [GhostField] public ushort DestroyedInteractableMask;

        /// <summary>Global arena danger level (0-1). Some arenas escalate over time.</summary>
        [GhostField(Quantization = 100)] public float DangerLevel;

        public bool IsHazardActive(int index) => (ActiveHazardMask & (1 << index)) != 0;
        public void SetHazardActive(int index) => ActiveHazardMask |= (ushort)(1 << index);
        public void SetHazardInactive(int index) => ActiveHazardMask &= (ushort)~(1 << index);
    }
}
```

### ArenaHazardElement (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/ArenaComponents.cs (continued)
using Unity.Mathematics;

namespace Hollowcore.Boss
{
    public enum ArenaHazardType : byte
    {
        DamageZone = 0,       // Standing in area deals periodic damage
        KnockbackZone = 1,    // Pushes entities away from center
        SlowZone = 2,         // Reduces movement speed
        FallingPlatform = 3,  // Platform collapses after weight timer
        RisingWater = 4,      // Water level rises, drowning damage
        MovingWall = 5,       // Walls shift, crushing damage on contact
        HeatVent = 6,         // Periodic burst of heat damage
        ElectricField = 7,    // Continuous lightning damage in area
        CollapsingFloor = 8,  // Floor sections break, fall = damage
        GravityShift = 9      // Gravity direction changes
    }

    /// <summary>
    /// Describes one hazard in the arena. Hazards apply to BOTH player and boss.
    /// Buffer on the arena root entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ArenaHazardElement : IBufferElementData
    {
        public byte HazardIndex;
        public ArenaHazardType HazardType;

        /// <summary>Center of the hazard zone in arena-local space.</summary>
        public float3 Position;

        /// <summary>Radius or half-extents of the hazard zone.</summary>
        public float3 Extents;

        /// <summary>Damage per second (for damage-type hazards).</summary>
        public float DamagePerSecond;

        /// <summary>Whether this hazard activates on a timer cycle.</summary>
        public bool IsCyclic;
        public float CycleOnDuration;
        public float CycleOffDuration;
        public float CycleTimer;
    }
}
```

### ArenaInteractableElement (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/ArenaComponents.cs (continued)
namespace Hollowcore.Boss
{
    public enum ArenaInteractableType : byte
    {
        Cover = 0,            // Destructible cover object
        Trap = 1,             // Can be triggered to damage boss or enemies
        Lever = 2,            // Activates/deactivates arena hazards
        ExplosiveBarrel = 3,  // AOE damage on destruction
        HealingStation = 4,   // One-time heal pickup
        PlatformControl = 5,  // Raises/lowers platforms
        EnvironmentOverride = 6 // Changes arena configuration
    }

    /// <summary>
    /// Describes one interactable object in the arena.
    /// Buffer on the arena root entity. Maps to world entities via ArenaInteractableLink.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ArenaInteractableElement : IBufferElementData
    {
        public byte InteractableIndex;
        public ArenaInteractableType InteractableType;
        public Entity WorldEntity;

        /// <summary>Number of uses remaining (-1 = infinite).</summary>
        public int UsesRemaining;

        /// <summary>Cooldown between uses (seconds).</summary>
        public float Cooldown;
        public float CooldownTimer;

        /// <summary>Whether this interactable is active in the current layout.</summary>
        public bool ActiveInCurrentLayout;
    }
}
```

---

## ScriptableObject Definitions

### ArenaDefinitionSO

```csharp
// File: Assets/Scripts/Boss/Definitions/ArenaDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Boss.Definitions
{
    [CreateAssetMenu(fileName = "NewArena", menuName = "Hollowcore/Boss/Arena Definition")]
    public class ArenaDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int ArenaId;
        public string ArenaName;
        [TextArea] public string Description;

        [Header("Layout")]
        [Tooltip("Scene or prefab for the arena geometry")]
        public GameObject ArenaPrefab;
        [Tooltip("Multiple layouts for phase-based reconfiguration")]
        public List<ArenaLayoutSO> Layouts = new();

        [Header("Hazards")]
        public List<ArenaHazardDefinition> Hazards = new();

        [Header("Interactables")]
        public List<ArenaInteractableDefinition> Interactables = new();

        [Header("Bounds")]
        [Tooltip("Arena bounding box — entities outside are pushed back or take damage")]
        public Bounds ArenaBounds;
        [Tooltip("Kill plane Y coordinate")]
        public float KillPlaneY = -50f;
    }

    [System.Serializable]
    public class ArenaHazardDefinition
    {
        public string HazardName;
        public ArenaHazardType HazardType;
        public Vector3 Position;
        public Vector3 Extents;
        public float DamagePerSecond;
        public bool IsCyclic;
        public float CycleOnDuration = 3f;
        public float CycleOffDuration = 5f;
        [Tooltip("Which layout indices this hazard is active in (-1 = all)")]
        public List<int> ActiveInLayouts = new() { -1 };
    }

    [System.Serializable]
    public class ArenaInteractableDefinition
    {
        public string InteractableName;
        public ArenaInteractableType InteractableType;
        public GameObject Prefab;
        public Vector3 Position;
        public int MaxUses = 1;
        public float Cooldown;
        public List<int> ActiveInLayouts = new() { -1 };
    }
}
```

---

## Systems

### ArenaHazardSystem

```csharp
// File: Assets/Scripts/Boss/Systems/ArenaHazardSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossEncounterSystem
//
// Processes active arena hazards each frame.
//
// For each ArenaState entity with ArenaHazardElement buffer:
//   1. For each hazard where IsHazardActive(hazardIndex):
//      a. Update CycleTimer for cyclic hazards (toggle on/off)
//      b. If hazard is in "on" phase:
//         - Query all entities (player + boss + enemies) within hazard Extents
//         - Apply effect based on HazardType:
//           DamageZone: add DamageEvent with DPS * deltaTime
//           KnockbackZone: apply knockback force away from center
//           SlowZone: apply movement speed debuff
//           FallingPlatform: check weight timer, disable platform entity
//           RisingWater: increment DangerLevel, apply drowning at threshold
//           MovingWall: translate wall entities, crush damage on overlap
//           HeatVent: burst damage on cycle start
//           ElectricField: continuous damage + visual
//           CollapsingFloor: disable floor sections progressively
//           GravityShift: modify physics gravity direction
//      c. Hazards damage the BOSS too — boss AI should avoid hazards
```

### ArenaLayoutSystem

```csharp
// File: Assets/Scripts/Boss/Systems/ArenaLayoutSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossPhaseTransitionSystem
//
// Handles arena reconfiguration between boss phases.
//
// When ArenaState.CurrentLayoutIndex changes:
//   1. Load new ArenaLayoutSO for the layout index
//   2. Activate/deactivate hazards based on ActiveInLayouts
//   3. Activate/deactivate interactables based on ActiveInLayouts
//   4. Trigger layout transition effects (walls moving, platforms shifting)
//   5. Update ArenaState.ActiveHazardMask
```

### ArenaInteractableSystem

```csharp
// File: Assets/Scripts/Boss/Systems/ArenaInteractableSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Processes player interaction with arena objects.
//
// For each ArenaInteractableElement where ActiveInCurrentLayout == true:
//   1. Check for interaction requests (framework Interaction/ system)
//   2. If UsesRemaining != 0 and CooldownTimer <= 0:
//      a. Execute interactable effect:
//         Cover: already handled by physics (destructible)
//         Trap: deal damage in area, apply status effect
//         Lever: toggle linked hazard active/inactive
//         ExplosiveBarrel: AOE damage, destroy entity
//         HealingStation: heal player, set UsesRemaining = 0
//         PlatformControl: move linked platform entity
//         EnvironmentOverride: change ArenaState.CurrentLayoutIndex
//      b. Decrement UsesRemaining (if not -1)
//      c. Reset CooldownTimer
//   3. Update CooldownTimer -= deltaTime for all interactables
```

---

## Arena Type Reference

| Arena | Boss | District | Key Mechanic |
|---|---|---|---|
| Cathedral of Screens | Grandmother Null | Necrospire | Holographic interference, false walls |
| Living Factory | The Foreman | Burn | Conveyors, pistons, slag pools |
| Multi-Level | King of Heights | Lattice | Collapsing platforms, fall damage |
| Flooded Theater | The Surgeon General | Wetmarket | Rising water levels, drowning |
| Shifting Absence | 404 Entity Not Found | Glitch Quarter | Geometry changes mid-fight |
| Cable-Web Heart | The Archbishop Algorithm | Cathedral | Cable terrain, web traversal |
| Flooding Arena | The Leviathan Empress | Shoals | Staged flooding, swim combat |
| Hall of Mirrors | The Prime Reflection | Mirrortown | Visual deception, clone spawns |
| Mood-Shifting | The Overmind | Synapse | Emotional environment effects |
| Building-Mass | Patient Zero | Quarantine | Evolving organic terrain |
| Living Garden | The Gardener-Prime | Old Growth | Environment IS the boss |
| Trading Floor | The Broker | Auction | Buyout mechanics, price traps |
| Analog Arena | The Silence | Deadwave | Tech disabled, analog combat |
| Everywhere | The Collective Unconscious | Nursery | Distributed boss, shifting focus |
| Gravity Arena | Commander Echo | Skyfall | Gravity direction as mechanic |

---

## Setup Guide

1. Create ArenaComponents.cs in `Assets/Scripts/Boss/Components/`
2. Create ArenaDefinitionSO assets in `Assets/Data/Boss/Arenas/`
3. For vertical slice: build arena subscenes for Grandmother Null, The Foreman, King of Heights
   - Place hazard trigger volumes matching ArenaHazardDefinition positions
   - Place interactable prefabs matching ArenaInteractableDefinition positions
4. Add ArenaAuthoring to arena root entity (bakes ArenaState + buffers)
5. Create ArenaLayoutSO variants for phase-based reconfiguration
6. Wire ArenaHazardSystem damage to use framework DamageEvent pipeline (not CombatResultEvent)
7. Verify hazards hit both player and boss entities

---

## Verification

- [ ] ArenaState initializes correctly from ArenaDefinitionSO
- [ ] ArenaHazardElement buffer populates with all defined hazards
- [ ] Cyclic hazards toggle on/off with correct timing
- [ ] DamageZone hazards deal damage to player
- [ ] DamageZone hazards deal damage to boss (not just player)
- [ ] Arena layout changes when CurrentLayoutIndex updates
- [ ] Hazards activate/deactivate based on layout transitions
- [ ] Interactables respond to player interaction
- [ ] Lever interactables toggle linked hazards
- [ ] ExplosiveBarrel deals AOE damage and destroys itself
- [ ] Arena bounds push entities back or deal damage at edges
- [ ] Kill plane destroys entities that fall below threshold
- [ ] ArenaInteractableElement.UsesRemaining depletes correctly
- [ ] Cooldown prevents rapid re-use of interactables

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/ArenaDefinitionValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules for ArenaDefinitionSO:
    //
    // 1. Arena dimension bounds:
    //    ArenaBounds.size per axis: min 5m, max 500m.
    //    Error if any axis outside range.
    //    Warning if total volume < 500 m^3 (cramped) or > 500,000 m^3 (enormous).
    //
    // 2. KillPlaneY must be below ArenaBounds.min.y by at least 5m.
    //    Error if KillPlaneY >= ArenaBounds.min.y.
    //
    // 3. Hazard positions within bounds:
    //    Every ArenaHazardDefinition.Position must be inside ArenaBounds (with Extents).
    //    Warning if hazard extends outside arena bounds.
    //
    // 4. Interactable positions within bounds:
    //    Every ArenaInteractableDefinition.Position must be inside ArenaBounds.
    //    Error if outside (unreachable interactable).
    //
    // 5. Cyclic hazard timing:
    //    CycleOnDuration > 0 and CycleOffDuration > 0 for IsCyclic hazards.
    //    Warning if CycleOnDuration > 10s (long active period) or CycleOffDuration < 1s (no safe window).
    //
    // 6. Layout consistency:
    //    Every hazard/interactable's ActiveInLayouts indices must be valid layout indices
    //    (0 to Layouts.Count - 1, or -1 for all). Error on out-of-range index.
    //
    // 7. DamagePerSecond range:
    //    Warning if DamagePerSecond > 500 (likely kills player in <2s) or == 0 for damage-type hazards.
    //
    // 8. At least one layout defined per arena. Error if Layouts empty.
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/BossWorkstation/ArenaDesignerModule.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // ArenaDesignerModule : IWorkstationModule — tab in BossDesignerWorkstation
    //
    // [1] Arena Layout Viewer
    //     - Top-down 2D preview of ArenaBounds with grid overlay
    //     - Hazard zones drawn as colored rectangles/circles (red = damage, blue = knockback, etc.)
    //     - Interactable positions as icons (cover, trap, lever, barrel, etc.)
    //     - Layout selector dropdown: switch between layouts, see which hazards activate/deactivate
    //     - Drag-to-reposition hazards and interactables (writes back to SO)
    //
    // [2] Hazard Timeline
    //     - Horizontal timeline per hazard showing cyclic on/off pattern over 30s
    //     - Overlapping hazard windows highlighted (player may face multiple hazards at once)
    //     - "Downtime windows" highlighted green (safe moments for player)
    //
    // [3] Arena Danger Heatmap
    //     - Spatial heatmap of cumulative DPS at each arena position
    //     - Accounts for all hazards in current layout
    //     - Red = high danger, green = safe, blue = interactable zones
    //     - Toggle per-phase to see how danger changes across boss phases
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/ArenaLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Arena live tuning fields (stored in ArenaLiveTuning singleton):
    //
    //   float HazardDamageMultiplier     // global scale on all hazard DPS (default 1.0)
    //   float HazardCycleSpeedMultiplier // scales cycle on/off durations (0.5 = twice as fast)
    //   bool DisableAllHazards           // suppress all hazard damage (arena traversal testing)
    //   bool ForceAllHazardsActive       // activate every hazard regardless of layout/phase
    //   int ForceLayoutIndex             // -1 = normal, 0+ = lock to specific layout
    //   bool ShowHazardGizmos            // always render hazard volumes in game view
    //   float KillPlaneYOverride         // -999 = use definition, other = override
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/ArenaDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Arena debug visualization (enabled via `arena_debug 1`):
    //
    // [1] Hazard Volume Wireframes
    //     - Semi-transparent colored volumes for all hazard zones
    //     - Color pulse: bright when active, dim when in off-cycle
    //     - DPS label floating above each hazard
    //
    // [2] Interactable Status Icons
    //     - World-space icons above each interactable
    //     - Shows uses remaining, cooldown timer, active/inactive state
    //
    // [3] Layout State
    //     - Screen-corner badge: "Layout 2/3" with layout name
    //     - Transition preview: wireframe outline of next layout's changes
    //
    // [4] Arena Bounds
    //     - Yellow wireframe box showing ArenaBounds
    //     - Red plane at KillPlaneY
    //     - Entities near bounds edge get a warning arrow pointing inward
}
