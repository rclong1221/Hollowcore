# EPIC 18.7: Spawn & Placement System with Editor Preview

**Status:** PLANNED
**Priority:** Medium-High (Level design velocity — designers place content daily)
**Dependencies:**
- `SpawnPointAuthoring` (existing — `DIG.Core.Spawning`, `Assets/Scripts/Core/Spawning/SpawnPointAuthoring.cs`, basic ECS spawn point with GroupID and gizmo)
- `PvPSpawnPointAuthoring` / `PvPSpawnSystem` (existing — `DIG.PvP`, `Assets/Scripts/PvP/Authoring/PvPSpawnPointAuthoring.cs`, team-based spawn points)
- `EnemySpawnerAuthoring` (existing — `DIG.AI`, `Assets/Scripts/AI/Authoring/EnemySpawnerAuthoring.cs`, AI enemy spawner)
- `SwarmSpawnerAuthoring` (existing — `DIG.Swarm`, `Assets/Scripts/Swarm/Authoring/SwarmSpawnerAuthoring.cs`, swarm spawner)
- `EncounterProfileSO` (existing — `DIG.AI`, `Assets/Scripts/AI/Authoring/EncounterProfileSO.cs`, encounter definitions)
- `LootSpawnSystem` (existing — `DIG.Loot.Systems`, `Assets/Scripts/Loot/Systems/LootSpawnSystem.cs`)
- `PlayerSpawnerAuthoring` (existing — `DIG.Player`, `Assets/Scripts/Player/Authoring/PlayerSpawnerAuthoring.cs`)
- `RespawnSystem` (existing — `DIG.Player.Systems`, `Assets/Scripts/Player/Systems/RespawnSystem.cs`)
- `DecoratorSpawnSystem` (existing — `DIG.Voxel`, `Assets/Scripts/Voxel/Systems/Generation/DecoratorSpawnSystem.cs`)

**Feature:** A unified, data-driven spawn and placement system that standardizes how all entity types (players, enemies, loot, decorations, encounters) are placed in the world. Provides a SpawnTable/SpawnRule ScriptableObject framework for weighted random spawning, wave-based spawning, and conditional spawning. Includes rich editor tooling with Scene view gizmos, spawn density heatmaps, placement brushes, and live spawn visualization.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `SpawnPointAuthoring` | `Assets/Scripts/Core/Spawning/SpawnPointAuthoring.cs` | Basic | GroupID + IsUsed flag + wire sphere gizmo. Minimal |
| `PvPSpawnPointAuthoring` | `Assets/Scripts/PvP/Authoring/PvPSpawnPointAuthoring.cs` | Implemented | Team-based spawn points for PvP |
| `PvPSpawnSystem` | `Assets/Scripts/PvP/Systems/PvPSpawnSystem.cs` | Implemented | Spawn protection, team assignment |
| `EnemySpawnerAuthoring` | `Assets/Scripts/AI/Authoring/EnemySpawnerAuthoring.cs` | Implemented | AI enemy spawn configuration |
| `SwarmSpawnerAuthoring` | `Assets/Scripts/Swarm/Authoring/SwarmSpawnerAuthoring.cs` | Implemented | Mass swarm entity spawning |
| `EncounterProfileSO` | `Assets/Scripts/AI/Authoring/EncounterProfileSO.cs` | Implemented | Encounter wave definitions |
| `LootSpawnSystem` | `Assets/Scripts/Loot/Systems/LootSpawnSystem.cs` | Implemented | Loot drop spawning |
| `PlayerSpawnerAuthoring` | `Assets/Scripts/Player/Authoring/PlayerSpawnerAuthoring.cs` | Implemented | Player spawn configuration |
| `RespawnSystem` | `Assets/Scripts/Player/Systems/RespawnSystem.cs` | Implemented | Player respawn logic |
| `DecoratorSpawnSystem` | `Assets/Scripts/Voxel/Systems/Generation/DecoratorSpawnSystem.cs` | Implemented | Voxel world decoration placement |

### What's Missing

- **No unified spawn table** — each subsystem defines its own spawn data format (PvP has teams, AI has encounters, Loot has drop tables, Voxel has decorators) with no shared abstraction
- **No weighted random spawning** — no reusable "pick one from weighted list" utility
- **No wave spawning framework** — encounter waves are EncounterProfileSO-specific, not reusable for other wave scenarios
- **No spawn condition system** — no "spawn only at night", "spawn only when quest active", "spawn only if player count > 2"
- **No spawn budget** — no performance limiter that caps total spawned entities per area
- **No editor placement brush** — designers must manually place individual spawn point GameObjects
- **No spawn density visualization** — no heatmap showing spawn concentration across the map
- **No spawn preview** — no way to see what entities would spawn at a point without entering Play mode
- **No spawn group management** — no way to enable/disable spawn groups by region or phase

---

## Problem

DIG has 6+ independent spawning systems, each with its own authoring component, data format, and spawn logic. Adding a new spawnable entity type (e.g., resource nodes, environmental hazards, friendly NPCs) requires building a new spawner from scratch. Designers cannot visualize spawn distributions, test spawn tables, or place spawn points with paintbrush-style tools. There is no way to set spawn budgets, conditions, or wave timing without code changes.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  SpawnTableSO              SpawnRuleSO              SpawnBudgetSO
  (weighted entries:        (conditions: time of     (max entities per
   prefab + weight +         day, quest state,        region, per type,
   min/max count,            player count, random     respawn cooldown,
   level range)              chance, cooldown)        priority ordering)
        |                       |                         |
        └──── SpawnManager (MonoBehaviour singleton) ─────┘
              (evaluates rules, resolves tables,
               enforces budgets, manages spawn groups)
                         |
        ┌────────────────┼────────────────┐
        |                |                |
  SpawnPointAuthoring  SpawnZoneAuthoring SpawnWaveController
  (enhanced: now has    (area-based spawn (timed waves with
   SpawnTableSO ref,     with density,     escalation, cooldown,
   rule ref, group ID,   radius, scatter)  trigger conditions)
   preview mesh)
        |                |                |
        └────────────────┼────────────────┘
                         |
              ECS Spawn Pipeline
                         |
  SpawnRequestSystem (SimulationSystemGroup)
  (processes SpawnRequest components,
   instantiates prefabs via ECB,
   respects budget constraints)
                         |
                 EDITOR TOOLING
                         |
  SpawnWorkstationModule ── placement & preview
  (SceneView placement brush, density heatmap,
   spawn table preview, budget monitor, group toggles)
```

---

## ScriptableObjects

### SpawnTableSO

**File:** `Assets/Scripts/Spawning/Config/SpawnTableSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| TableId | string | "" | Unique identifier |
| DisplayName | string | "" | Editor label |
| Entries | SpawnEntry[] | empty | Weighted spawn entries |
| GuaranteedEntries | SpawnEntry[] | empty | Always spawn these (not weighted) |
| RollCount | RangeInt | (1,1) | How many rolls from weighted table |
| AllowDuplicates | bool | true | Can same entry be rolled multiple times |

### SpawnEntry

```csharp
[Serializable]
public class SpawnEntry
{
    public GameObject Prefab;              // Entity prefab to spawn
    public int Weight;                     // Relative weight (higher = more likely)
    public RangeInt Count;                 // Min/max count per roll
    public RangeInt LevelRange;           // Only spawn if player level in range
    public float ScatterRadius;           // Random position offset from spawn point
    public SpawnRuleSO OverrideRule;      // Per-entry condition override
    public string Tag;                     // For filtering/grouping
}
```

### SpawnRuleSO

**File:** `Assets/Scripts/Spawning/Config/SpawnRuleSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Conditions | SpawnCondition[] | empty | All must be true to spawn |
| CooldownSeconds | float | 0 | Time between respawns |
| MaxActiveInstances | int | 0 | 0 = unlimited |
| SpawnChance | float [0-1] | 1.0 | Probability per spawn attempt |
| TimeOfDayRange | Vector2 | (0,24) | Hour range (0-24) |
| RequiredQuestState | string | "" | Quest that must be active/completed |
| MinPlayerCount | int | 0 | Minimum players in area |

### SpawnCondition

```csharp
[Serializable]
public class SpawnCondition
{
    public ConditionType Type;  // TimeOfDay, QuestState, PlayerCount, Random, CustomEvent
    public string Parameter;
    public Comparator Comparator;
    public float Value;
}
```

### SpawnBudgetSO

**File:** `Assets/Scripts/Spawning/Config/SpawnBudgetSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| MaxTotalEntities | int | 100 | Hard cap on total spawned entities in this budget zone |
| PerTypeLimits | TypeLimit[] | empty | Per-prefab caps |
| PriorityMode | BudgetPriority enum | FIFO | FIFO, Priority, Distance (despawn farthest first) |
| DespawnDistance | float | 100 | Auto-despawn entities beyond this distance from nearest player |
| WarningThreshold | float [0-1] | 0.8 | Editor warning when budget reaches this % |

---

## Enhanced Authoring Components

### SpawnPointAuthoring (Enhanced)

**File:** `Assets/Scripts/Spawning/Authoring/SpawnPointAuthoring.cs` (replaces existing minimal version)

- Keeps existing `GroupID` and gizmo
- Adds: `SpawnTableSO` reference, `SpawnRuleSO` reference, `SpawnBudgetSO` reference
- Adds: Preview mesh rendering (shows ghost of spawnable entity in Scene view)
- Adds: Custom inspector with "Test Spawn" button and table preview

### SpawnZoneAuthoring (New)

**File:** `Assets/Scripts/Spawning/Authoring/SpawnZoneAuthoring.cs`

- Area-based spawning: box, sphere, or custom polygon volume
- Scatter entities within zone with density parameter (entities per unit area)
- Surface snapping: raycast down to place on terrain/voxel surface
- Can reference multiple `SpawnTableSO` entries with area-based weights

### SpawnWaveController (New)

**File:** `Assets/Scripts/Spawning/Runtime/SpawnWaveController.cs`

- MonoBehaviour attached to encounter/arena areas
- Drives timed wave spawning: configurable wave count, interval, escalation
- Each wave references a `SpawnTableSO` (can change per wave for escalation)
- Trigger modes: zone entry, event, timer, manual
- Events: OnWaveStart, OnWaveComplete, OnAllWavesComplete

---

## ECS Components

### SpawnRequest

```
SpawnRequest (IComponentData)
  TableHash      : int       // SpawnTableSO hash
  Position       : float3    // Spawn position
  Rotation       : quaternion // Spawn rotation
  GroupId        : int       // Spawn group
  BudgetHash     : int       // SpawnBudgetSO hash

Total: 36 bytes
```

### SpawnBudgetTracker

```
SpawnBudgetTracker (IComponentData singleton per budget zone)
  CurrentCount   : int       // Current spawned count
  MaxCount       : int       // Budget max
  BudgetHash     : int       // Links to SpawnBudgetSO

Total: 12 bytes
```

---

## Editor Tooling

### SpawnWorkstationModule

**File:** `Assets/Editor/SpawnWorkstation/Modules/SpawnWorkstationModule.cs`

- **Placement Brush:** Paint spawn points in Scene view with configurable brush size, density, and surface snapping
- **Density Heatmap:** Overlay showing spawn concentration across the map (per entity type, color-coded)
- **Table Preview:** Select a SpawnTableSO and see weighted probability bars, simulated roll results
- **Budget Monitor:** Per-zone budget usage bars with warning thresholds
- **Group Manager:** Enable/disable spawn groups, batch-edit group properties
- **Spawn Simulator:** Run N simulated spawns and show statistical distribution

### SpawnPointGizmoDrawer

**File:** `Assets/Editor/SpawnWorkstation/SpawnPointGizmoDrawer.cs`

- Enhanced Scene view gizmos: color-coded by group, prefab icon preview, connection lines to zone boundaries
- Budget zone visualization: wireframe volumes with fill color based on usage percentage
- Spawn radius visualization: dashed circles around scatter radius

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Spawning/Config/SpawnTableSO.cs` | ScriptableObject | ~60 |
| `Assets/Scripts/Spawning/Config/SpawnRuleSO.cs` | ScriptableObject | ~50 |
| `Assets/Scripts/Spawning/Config/SpawnBudgetSO.cs` | ScriptableObject | ~35 |
| `Assets/Scripts/Spawning/SpawnManager.cs` | MonoBehaviour | ~250 |
| `Assets/Scripts/Spawning/Authoring/SpawnPointAuthoring.cs` | Authoring (enhanced) | ~80 |
| `Assets/Scripts/Spawning/Authoring/SpawnZoneAuthoring.cs` | Authoring | ~100 |
| `Assets/Scripts/Spawning/Runtime/SpawnWaveController.cs` | MonoBehaviour | ~150 |
| `Assets/Scripts/Spawning/Runtime/SpawnTableResolver.cs` | Class | ~80 |
| `Assets/Scripts/Spawning/Systems/SpawnRequestSystem.cs` | ISystem | ~120 |
| `Assets/Scripts/Spawning/Systems/SpawnBudgetSystem.cs` | ISystem | ~80 |
| `Assets/Scripts/Spawning/Systems/DespawnDistanceSystem.cs` | ISystem | ~60 |
| `Assets/Editor/SpawnWorkstation/Modules/SpawnWorkstationModule.cs` | Editor | ~350 |
| `Assets/Editor/SpawnWorkstation/SpawnPointGizmoDrawer.cs` | Editor | ~120 |
| `Assets/Editor/SpawnWorkstation/SpawnBrushTool.cs` | Editor | ~150 |

**Total estimated:** ~1,685 lines

---

## Performance Considerations

- `SpawnRequestSystem` is Burst-compiled — processes spawn requests without managed allocations
- `SpawnBudgetSystem` is a simple counter check per frame — O(budgetZoneCount) ≈ O(10)
- `DespawnDistanceSystem` uses squared distance checks — no sqrt, Burst parallel job
- `SpawnTableResolver` pre-computes cumulative weight arrays on SpawnTableSO load — O(1) weighted random selection via binary search
- Editor heatmap computed on-demand (button press), not continuously — no Scene view performance impact during normal editing

---

## Testing Strategy

- Unit test weighted random: verify statistical distribution matches weights over 10,000 rolls
- Unit test spawn conditions: time-of-day, player count, quest state
- Unit test budget enforcement: exceed budget → verify spawn rejected
- Unit test despawn distance: move player away → verify distant entities despawned
- Integration test: place SpawnZoneAuthoring → enter Play mode → verify entities spawn within zone
- Integration test: SpawnWaveController → trigger waves → verify escalation
- Editor test: placement brush creates SpawnPointAuthoring at correct positions
- Editor test: density heatmap renders correctly
