# EPIC 15.19: Aggro & Threat System

**Status:** ✅ Implemented  
**Priority:** High  
**Dependencies:**  
- ✅ `VisionDetectionSystem` / `SeenTargetElement` (EPIC 15.17) — Vision awareness  
- ✅ `CombatState` / `CombatStateSystem` (EPIC 15.15) — Combat state management  
- ✅ `PlayerNoiseStatus` / `StealthSystem` (EPIC 15.3) — Hearing detection  
- ✅ `TargetData` — Shared targeting output component  
- ✅ `RecentAttackerElement` buffer — Damage attribution tracking  

**Feature:** AI Threat Tables, Target Selection, and Aggro Management

---

## Overview

The Aggro & Threat System serves as the "brain" of AI target selection. Rather than simple "attack nearest" logic, AI entities maintain a **Threat Table** (hate list) that tracks cumulative threat from multiple sources. This enables:

- **Tank gameplay** — High-threat players draw aggro from damage dealers
- **Taunt abilities** — Instant threat spikes to force target switches  
- **Threat decay** — Passive threat reduction enables aggro drops and tank swaps
- **Memory persistence** — AI remembers last-known positions when targets break LOS
- **Hysteresis** — Prevents rapid target flickering via threshold-based switching

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         AI Entity (Enemy)                               │
│  ┌─────────────┐  ┌─────────────────┐  ┌───────────────────────────┐    │
│  │ VisionSensor│  │   AggroConfig   │  │       TargetData          │    │
│  │ (EPIC 15.17)│  │ (threat params) │  │ (output: current target)  │    │
│  └──────┬──────┘  └────────┬────────┘  └─────────────▲─────────────┘    │
│         │                  │                         │                  │
│  ┌──────▼──────┐  ┌────────▼────────┐  ┌─────────────┴─────────────┐    │
│  │SeenTarget   │  │ ThreatEntry     │  │  AggroTargetSelectorSystem│    │
│  │Element[]    │  │ Buffer[]        │  │  (picks highest threat)   │    │
│  └──────┬──────┘  └────────▲────────┘  └─────────────▲─────────────┘    │
│         │                  │                         │                  │
└─────────┼──────────────────┼─────────────────────────┼──────────────────┘
          │                  │                         │
┌─────────▼──────────────────▼─────────────────────────┼──────────────────┐
│                     System Pipeline                  │                  │
│  ┌──────────────────┐  ┌────────────────────┐  ┌─────┴────────────────┐ │
│  │ VisionDetection  │  │ ThreatManagement   │  │ AggroTargetSelector  │ │
│  │ System           │──▶   System           │──▶   System             │ │
│  │ (sees targets)   │  │ (adds threat)      │  │ (selects target)     │ │
│  └──────────────────┘  └────────────────────┘  └──────────────────────┘ │
│           ▲                      ▲                                      │
│           │                      │                                      │
│  ┌────────┴────────┐  ┌──────────┴──────────┐  ┌──────────────────────┐ │
│  │ HearingDetection│  │ CombatResolution    │  │ ThreatDecaySystem    │ │
│  │ System (future) │  │ System (damage)     │  │ (passive reduction)  │ │
│  └─────────────────┘  └─────────────────────┘  └──────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Components

### 1. `ThreatEntry` (Buffer Element)

Per-target threat tracking on AI entities.

```csharp
// Assets/Scripts/Aggro/Components/ThreatEntry.cs
[InternalBufferCapacity(8)]
public struct ThreatEntry : IBufferElementData
{
    /// <summary>Entity generating threat (player, turret, etc.)</summary>
    public Entity SourceEntity;
    
    /// <summary>Cumulative threat value (damage * multiplier + modifiers)</summary>
    public float ThreatValue;
    
    /// <summary>Last world position seen (for memory/search behavior)</summary>
    public float3 LastKnownPosition;
    
    /// <summary>Time since this source was visible (0 = currently visible)</summary>
    public float TimeSinceVisible;
    
    /// <summary>Whether this source is currently in SeenTargetElement buffer</summary>
    public bool IsCurrentlyVisible;
}
```

### 2. `AggroConfig` (Component)

Per-entity configuration for threat behavior.

```csharp
// Assets/Scripts/Aggro/Components/AggroConfig.cs
public struct AggroConfig : IComponentData
{
    // === Threat Multipliers ===
    /// <summary>Multiplier applied to damage for threat calculation. Default 1.0</summary>
    public float DamageThreatMultiplier;
    
    /// <summary>Base threat added when first seeing a target (sight aggro). Default 5.0</summary>
    public float SightThreatValue;
    
    /// <summary>Base threat added when hearing a target. Default 3.0</summary>
    public float HearingThreatValue;
    
    // === Decay Settings ===
    /// <summary>Threat reduction per second for visible targets. Default 1.0</summary>
    public float VisibleDecayRate;
    
    /// <summary>Threat reduction per second for non-visible targets. Default 5.0</summary>
    public float HiddenDecayRate;
    
    /// <summary>Time before forgetting a hidden target entirely (seconds). Default 30.0</summary>
    public float MemoryDuration;
    
    // === Target Selection ===
    /// <summary>Only switch targets if new threat exceeds current by this ratio. Default 1.1 (110%)</summary>
    public float HysteresisRatio;
    
    /// <summary>Maximum number of targets to track. Default 8</summary>
    public int MaxTrackedTargets;
    
    /// <summary>Minimum threat to remain in table. Default 0.1</summary>
    public float MinimumThreat;
    
    public static AggroConfig Default => new AggroConfig
    {
        DamageThreatMultiplier = 1.0f,
        SightThreatValue = 5.0f,
        HearingThreatValue = 3.0f,
        VisibleDecayRate = 1.0f,
        HiddenDecayRate = 5.0f,
        MemoryDuration = 30.0f,
        HysteresisRatio = 1.1f,
        MaxTrackedTargets = 8,
        MinimumThreat = 0.1f
    };
}
```

### 3. `AggroState` (Component)

Runtime state for the aggro system.

```csharp
// Assets/Scripts/Aggro/Components/AggroState.cs
public struct AggroState : IComponentData
{
    /// <summary>Current highest-threat target (may differ from TargetData.TargetEntity during transition)</summary>
    public Entity CurrentThreatLeader;
    
    /// <summary>Total threat of CurrentThreatLeader (for hysteresis comparison)</summary>
    public float CurrentLeaderThreat;
    
    /// <summary>Whether AI is actively engaged (has any threat entries)</summary>
    public bool IsAggroed;
    
    /// <summary>Time spent without any valid targets (for de-aggro)</summary>
    public float TimeSinceLastValidTarget;
}
```

### 4. `ThreatModifierEvent` (Enableable Tag + Data)

For taunt/detaunt abilities and threat wipes.

```csharp
// Assets/Scripts/Aggro/Components/ThreatModifierEvent.cs
public struct ThreatModifierEvent : IComponentData, IEnableableComponent
{
    public Entity TargetAI;       // Which AI to modify
    public Entity ThreatSource;   // Who the threat applies to
    public float FlatThreatAdd;   // Flat threat to add (taunt = +1000)
    public float ThreatMultiplier;// Multiplicative modifier (0 = wipe, 0.5 = halve)
    public ThreatModifierType Type;
}

public enum ThreatModifierType : byte
{
    Add,        // Add flat threat
    Multiply,   // Multiply existing threat
    Set,        // Set to exact value
    Wipe        // Remove from threat table entirely
}
```

---

## Systems

### System 1: `ThreatFromVisionSystem`

**Purpose:** Add initial "sight aggro" when a target first appears in `SeenTargetElement`.

**Update Order:** After `VisionDetectionSystem`

```csharp
// Assets/Scripts/Aggro/Systems/ThreatFromVisionSystem.cs
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(VisionDetectionSystem))]
[BurstCompile]
public partial struct ThreatFromVisionSystem : ISystem
{
    // For each entity with VisionSensor + ThreatEntry buffer:
    //   1. Iterate SeenTargetElement buffer
    //   2. For newly visible targets (was TimeSinceVisible > 0, now IsVisibleNow = true):
    //      - Add SightThreatValue to ThreatEntry (create if new)
    //   3. Update ThreatEntry.IsCurrentlyVisible and LastKnownPosition from SeenTargetElement
}
```

### System 2: `ThreatFromDamageSystem`

**Purpose:** Add threat when AI takes damage. Reads existing damage attribution data.

**Update Order:** After `CombatResolutionSystem`

```csharp
// Assets/Scripts/Aggro/Systems/ThreatFromDamageSystem.cs
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CombatResolutionSystem))]
[BurstCompile]
public partial struct ThreatFromDamageSystem : ISystem
{
    // For each entity with ThreatEntry buffer + RecentAttackerElement buffer:
    //   1. Check for new entries in RecentAttackerElement since last tick
    //   2. For each new damage source:
    //      - threat += DamageDealt * AggroConfig.DamageThreatMultiplier
    //   3. Update ThreatEntry or create new entry
}
```

### System 3: `ThreatModifierSystem`

**Purpose:** Process taunt/detaunt abilities via `ThreatModifierEvent`.

**Update Order:** After `ThreatFromDamageSystem`

```csharp
// Assets/Scripts/Aggro/Systems/ThreatModifierSystem.cs
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ThreatFromDamageSystem))]
[BurstCompile]
public partial struct ThreatModifierSystem : ISystem
{
    // Query all enabled ThreatModifierEvent components:
    //   1. Find target AI's ThreatEntry buffer
    //   2. Apply modifier based on Type (Add/Multiply/Set/Wipe)
    //   3. Disable the event component
}
```

### System 4: `ThreatDecaySystem`

**Purpose:** Passive threat reduction over time. Faster decay for hidden targets.

**Update Order:** Late in SimulationSystemGroup

```csharp
// Assets/Scripts/Aggro/Systems/ThreatDecaySystem.cs
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[BurstCompile]
public partial struct ThreatDecaySystem : ISystem
{
    // For each entity with ThreatEntry buffer + AggroConfig:
    //   1. For each ThreatEntry:
    //      - If IsCurrentlyVisible: threat -= VisibleDecayRate * deltaTime
    //      - Else: threat -= HiddenDecayRate * deltaTime
    //      - TimeSinceVisible += deltaTime (if not visible)
    //   2. Remove entries where:
    //      - ThreatValue < MinimumThreat, OR
    //      - TimeSinceVisible > MemoryDuration, OR
    //      - SourceEntity is destroyed
}
```

### System 5: `AggroTargetSelectorSystem`

**Purpose:** Select the highest-threat target and write to `TargetData`.

**Update Order:** After `ThreatDecaySystem`

```csharp
// Assets/Scripts/Aggro/Systems/AggroTargetSelectorSystem.cs
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(ThreatDecaySystem))]
[BurstCompile]
public partial struct AggroTargetSelectorSystem : ISystem
{
    // For each entity with ThreatEntry buffer + AggroState + TargetData:
    //   1. Find entry with highest ThreatValue
    //   2. Apply hysteresis:
    //      - Only switch if newThreat > currentLeaderThreat * HysteresisRatio
    //   3. Update AggroState.CurrentThreatLeader
    //   4. Write to TargetData:
    //      - TargetEntity = CurrentThreatLeader
    //      - TargetPoint = ThreatEntry.LastKnownPosition
    //      - HasValidTarget = (ThreatLeader != Entity.Null)
    //   5. Update AggroState.IsAggroed
}
```

### System 6: `AggroCombatStateIntegration`

**Purpose:** Bridge aggro state to combat state system.

```csharp
// Assets/Scripts/Aggro/Systems/AggroCombatStateIntegration.cs
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(AggroTargetSelectorSystem))]
public partial struct AggroCombatStateIntegration : ISystem
{
    // For each entity with AggroState + CombatState:
    //   1. If AggroState.IsAggroed && !CombatState.IsInCombat:
    //      - Set CombatState.IsInCombat = true
    //      - Enable EnteredCombatTag
    //   2. If !AggroState.IsAggroed:
    //      - Let CombatStateSystem handle timeout-based exit
}
```

---

## Authoring

### `AggroAuthoring.cs`

```csharp
// Assets/Scripts/Aggro/Authoring/AggroAuthoring.cs
public class AggroAuthoring : MonoBehaviour
{
    [Header("Threat Multipliers")]
    public float DamageThreatMultiplier = 1.0f;
    public float SightThreatValue = 5.0f;
    public float HearingThreatValue = 3.0f;
    
    [Header("Decay Settings")]
    public float VisibleDecayRate = 1.0f;
    public float HiddenDecayRate = 5.0f;
    public float MemoryDuration = 30.0f;
    
    [Header("Target Selection")]
    [Range(1.0f, 2.0f)]
    public float HysteresisRatio = 1.1f;
    public int MaxTrackedTargets = 8;
    
    class Baker : Baker<AggroAuthoring>
    {
        public override void Bake(AggroAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new AggroConfig { /* from authoring */ });
            AddComponent(entity, new AggroState());
            AddBuffer<ThreatEntry>(entity);
            
            // Ensure TargetData exists for output
            if (!HasComponent<TargetData>(entity))
                AddComponent(entity, new TargetData());
        }
    }
}
```

---

## Integration Points

| System | Integration |
|--------|-------------|
| **Vision (EPIC 15.17)** | `ThreatFromVisionSystem` reads `SeenTargetElement` for sight aggro |
| **Combat (EPIC 15.15)** | `ThreatFromDamageSystem` reads `RecentAttackerElement` for damage threat |
| **Combat State** | `AggroCombatStateIntegration` sets `IsInCombat` when aggroed |
| **Health Bar UI** | `HasAggroOn` component set by `AggroTargetSelectorSystem` for `WhenAggroed` visibility mode |
| **AI Behavior** | AI reads `TargetData.TargetEntity` for navigation/attack decisions |
| **Stealth (EPIC 15.3)** | Future: `ThreatFromHearingSystem` reads `PlayerNoiseStatus` |

---

## Debug Tools

### `AggroDebugTester.cs`

Inspector panel for runtime testing:

```csharp
[Header("Runtime State (Read Only)")]
[SerializeField] private bool _isAggroed;
[SerializeField] private string _currentTargetName;
[SerializeField] private float _currentTargetThreat;
[SerializeField] private int _threatTableSize;

[Header("Actions")]
[ContextMenu("Add Threat to Local Player")]
void AddThreatToPlayer() { /* Implementation */ }

[ContextMenu("Wipe Threat Table")]
void WipeThreatTable() { /* Implementation */ }

[ContextMenu("Taunt (+1000 Threat)")]
void Taunt() { /* Implementation */ }
```

### Gizmo Visualization

- **Red line** to current target
- **Yellow lines** to all threat table entries (thickness = threat level)
- **Fading lines** to remembered-but-not-visible entries

---

## Verification Plan

### Test Scenario 1: Basic Threat Accumulation
1. Spawn AI with `VisionSensor` + `AggroAuthoring`
2. Spawn Player in view
3. **Expected:** AI gains `SightThreatValue` threat, targets player

### Test Scenario 2: Damage-Based Threat
1. P1 deals 10 damage → Threat(P1) = 10
2. P2 deals 20 damage → Threat(P2) = 20
3. **Expected:** AI switches to P2 (highest threat)

### Test Scenario 3: Hysteresis
1. P1 has 100 threat (current target)
2. P2 gains 105 threat
3. **Expected:** AI stays on P1 (105 < 100 * 1.1 = 110)
4. P2 gains 111 threat
5. **Expected:** AI switches to P2

### Test Scenario 4: Taunt
1. AI targeting P2 (highest threat)
2. P1 uses Taunt (+1000 threat)
3. **Expected:** AI immediately switches to P1

### Test Scenario 5: Memory & Decay
1. AI targeting player
2. Player breaks LOS for 10 seconds
3. **Expected:** Threat decays at `HiddenDecayRate`
4. Player hidden for `MemoryDuration` seconds
5. **Expected:** Player removed from threat table, AI de-aggros

### Test Scenario 6: Death/Despawn
1. AI targeting P1
2. P1 dies/despawns
3. **Expected:** AI switches to next highest threat or de-aggros

---

## Performance Considerations

| Aspect | Strategy |
|--------|----------|
| **Buffer Size** | `InternalBufferCapacity(8)` — most encounters have <8 attackers |
| **Burst Compilation** | All systems `[BurstCompile]` |
| **Job Scheduling** | `IJobEntity` with `ScheduleParallel` where possible |
| **Entity Validation** | Use `EntityManager.Exists()` before accessing threat sources |
| **Throttling** | Threat decay can run at reduced frequency (0.2s intervals) |

---

## File Structure

```
Assets/Scripts/Aggro/
├── Components/
│   ├── ThreatEntry.cs
│   ├── AggroConfig.cs
│   ├── AggroState.cs
│   └── ThreatModifierEvent.cs
├── Systems/
│   ├── ThreatFromVisionSystem.cs
│   ├── ThreatFromDamageSystem.cs
│   ├── ThreatModifierSystem.cs
│   ├── ThreatDecaySystem.cs
│   ├── AggroTargetSelectorSystem.cs
│   └── AggroCombatStateIntegration.cs
├── Authoring/
│   └── AggroAuthoring.cs
└── Debug/
    └── AggroDebugTester.cs
```

---

## Future Extensions

| Feature | Description |
|---------|-------------|
| **Hearing System** | `ThreatFromHearingSystem` uses `PlayerNoiseStatus` for sound-based aggro |
| **Threat Transfer** | "Redirect" abilities that move threat between players |
| **Threat Meters (UI)** | Party UI showing relative threat levels per player |
| **Aggro Radius** | Nearby AI entities share threat tables (pack aggro) |
| **Priority Targets** | Override threat for specific target types (healers > DPS) |
| **Leash Distance** | Maximum chase distance before forced de-aggro |
