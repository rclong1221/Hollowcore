# EPIC 13.4: Side Goal & Mission Design

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: High — Goals drive player exploration and boss preparation
**Dependencies**: 13.1 (DistrictDefinitionSO), Framework: Quest/ (QuestDefinitionSO, QuestCompletionSystem, ObjectiveType), EPIC 5 (Echo system, optional), EPIC 14 (Boss variants, optional)

---

## Overview

Each district has 6-8 side goals and 1 main quest chain. Side goals serve triple duty: they are content encounters in their own right, insurance against boss mechanics (completing specific goals disables boss abilities), and Front counterplay (some goals slow or redirect the Front). Skipped side goals produce Echoes (EPIC 5) that haunt subsequent runs. The main chain is a multi-step quest that unlocks the boss fight and is always completable regardless of side goal progress. All goals are authored as `QuestDefinitionSO` instances from the framework Quest/ system, extended with district-specific metadata.

---

## Component Definitions

### GoalType Enum

```csharp
// File: Assets/Scripts/District/Definitions/GoalEnums.cs
namespace Hollowcore.District
{
    /// <summary>
    /// High-level goal archetype. Maps to specific objective configurations
    /// and encounter setups. Informs the goal generator which templates to use.
    /// </summary>
    public enum GoalType : byte
    {
        Rescue = 0,     // Find and escort/free an NPC
        Destroy = 1,    // Destroy a target object or enemy group
        Collect = 2,    // Gather N items from the district
        Escort = 3,     // Protect an NPC moving between zones
        Survive = 4,    // Survive N seconds in a danger zone
        Stealth = 5,    // Complete objective without raising alarm
        Puzzle = 6,     // Solve environmental puzzle (interaction sequence)
        Assassinate = 7 // Kill a specific named target (miniboss)
    }

    /// <summary>
    /// How completing a goal affects the boss fight.
    /// </summary>
    public enum BossInsuranceType : byte
    {
        None = 0,               // No effect on boss
        DisableAbility = 1,     // Removes a specific boss ability
        ReduceHealth = 2,       // Boss starts with reduced max health
        DisablePhase = 3,       // Boss skips a phase entirely
        RevealWeakpoint = 4,    // Exposes a hidden weakpoint
        RemoveAdd = 5           // Removes an add wave from the fight
    }

    /// <summary>
    /// How a goal interacts with the Front system.
    /// </summary>
    public enum FrontCounterplay : byte
    {
        None = 0,
        SlowSpread = 1,     // Reduces Front spread rate while goal is active
        RedirectFront = 2,  // Shifts Front origin to a different zone
        PurgeZone = 3,      // Fully clears Front from one zone
        DelayPhase = 4      // Delays next Front phase transition
    }
}
```

### DistrictGoalExtensionSO

```csharp
// File: Assets/Scripts/District/Definitions/DistrictGoalExtensionSO.cs
using UnityEngine;

namespace Hollowcore.District
{
    /// <summary>
    /// District-specific metadata for a quest. Attached to QuestDefinitionSO.ExtensionData
    /// (or stored as a parallel SO). Connects goals to boss insurance, Front counterplay,
    /// Echo generation, and Trace mechanics.
    /// </summary>
    [CreateAssetMenu(fileName = "DistrictGoalExtension", menuName = "Hollowcore/District/Goal Extension")]
    public class DistrictGoalExtensionSO : ScriptableObject
    {
        [Header("Classification")]
        public GoalType Type;
        public bool IsMainChain;
        [Tooltip("Step index within the main chain (0-based). Ignored for side goals.")]
        public int MainChainStepIndex;

        [Header("Boss Insurance (GDD S4.2)")]
        public BossInsuranceType InsuranceType;
        [Tooltip("Boss ability/phase ID disabled by completing this goal. 0 = N/A.")]
        public int BossTargetId;
        [TextArea(1, 3)]
        public string InsuranceDescription;

        [Header("Front Counterplay")]
        public FrontCounterplay CounterplayType;
        [Tooltip("Magnitude of counterplay effect (zone index for Redirect, seconds for Delay, etc).")]
        public float CounterplayValue;

        [Header("Echo Generation (EPIC 5)")]
        [Tooltip("Echo definition created when this goal is skipped. Null = no echo.")]
        public ScriptableObject SkippedEchoDefinition;

        [Header("Trace")]
        [Tooltip("Trace reduction on completion. Negative = reduces trace (stealth goals).")]
        public float TraceModifier;

        [Header("Zone Requirement")]
        [Tooltip("Zone index where this goal must be completed. -1 = any zone.")]
        public int RequiredZoneIndex = -1;
        [Tooltip("Zone indices where goal objectives can be found.")]
        public int[] ObjectiveZoneIndices;
    }
}
```

### DistrictGoalState (IComponentData)

```csharp
// File: Assets/Scripts/District/Components/GoalComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.District
{
    /// <summary>
    /// Runtime state for a district goal. Created per active goal in the district.
    /// Extends framework QuestInstance with district-specific tracking.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DistrictGoalState : IComponentData
    {
        [GhostField] public int QuestId;
        [GhostField] public byte GoalType;          // GoalType cast
        [GhostField] public byte InsuranceType;      // BossInsuranceType cast
        [GhostField] public int BossTargetId;
        [GhostField] public byte CounterplayType;    // FrontCounterplay cast
        [GhostField] public bool IsMainChain;
        [GhostField] public byte MainChainStepIndex;
    }

    /// <summary>
    /// Buffer of all district goals on the DistrictState entity.
    /// Populated at district load, read by goal tracking and boss insurance systems.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DistrictGoalEntry : IBufferElementData
    {
        public int QuestId;
        public byte GoalType;
        public byte InsuranceType;
        public int BossTargetId;
        public byte CounterplayType;
        public bool IsMainChain;
        public byte MainChainStepIndex;
        public bool IsCompleted;
        public bool IsSkipped;
    }
}
```

---

## Systems

### DistrictGoalInitSystem

```csharp
// File: Assets/Scripts/District/Systems/DistrictGoalInitSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DistrictLoadSystem
//
// Reads: DistrictDefinitionSO.Goals, DistrictGoalExtensionSO
// Writes: DistrictGoalEntry buffer on DistrictState entity, QuestAccept requests
//
// On district load:
//   1. Iterate DistrictDefinitionSO.Goals array
//   2. For each goal, resolve DistrictGoalExtensionSO metadata
//   3. Populate DistrictGoalEntry buffer with all goals
//   4. Auto-accept main chain step 0 via framework QuestAcceptSystem
//   5. Make side goals available (QuestState.Available) for player discovery
```

### BossInsuranceSystem

```csharp
// File: Assets/Scripts/District/Systems/BossInsuranceSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: QuestCompletionSystem (framework)
//
// Reads: DistrictGoalEntry buffer, QuestState changes
// Writes: BossInsuranceState (accumulated insurance effects for boss fight)
//
// Each frame:
//   1. Check for newly completed goals with InsuranceType != None
//   2. Record insurance effect: { InsuranceType, BossTargetId }
//   3. When boss fight begins (EPIC 14), pass accumulated insurance effects
//      to the boss spawn system to disable abilities/phases/adds
```

### GoalFrontCounterplaySystem

```csharp
// File: Assets/Scripts/District/Systems/GoalFrontCounterplaySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: QuestCompletionSystem (framework)
//
// Reads: DistrictGoalEntry buffer, QuestState changes, FrontState
// Writes: FrontState modifiers (spread rate, origin shift, phase delay)
//
// On goal completion with CounterplayType != None:
//   1. SlowSpread: multiply FrontState.SpreadRate by 0.5 for CounterplayValue seconds
//   2. RedirectFront: move FrontOriginMarker to zone at index CounterplayValue
//   3. PurgeZone: clear Front status from zone at index CounterplayValue
//   4. DelayPhase: add CounterplayValue seconds to next phase timer
```

### SkippedGoalEchoSystem

```csharp
// File: Assets/Scripts/District/Systems/SkippedGoalEchoSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: DistrictGoalEntry buffer, DistrictState
// Writes: Echo generation requests (EPIC 5 integration)
//
// On district completion (boss defeated or player exits):
//   1. Iterate all DistrictGoalEntry entries
//   2. For each non-completed, non-main-chain goal with SkippedEchoDefinition:
//      mark IsSkipped = true
//   3. Generate Echo entities from SkippedEchoDefinition for next run
//   4. Store in meta-persistence (EPIC 4) so echoes appear on future visits
```

---

## Setup Guide

1. **Create `Assets/Scripts/District/Definitions/GoalEnums.cs`** and `DistrictGoalExtensionSO.cs`
2. **Create `Assets/Data/Districts/Necrospire/Goals/`** folder
3. **Author 8 QuestDefinitionSOs** for Necrospire side goals using framework Quest/ format:
   - "Sever the Grief-Link" — Destroy type, Kill objective targeting Grief-Link Node enemies
   - "Recover the Intact Upload" — Collect type, Collect 1 Intact Upload item
   - "Data Vampire Cache" — Collect type, Collect 3 Data Caches from Inheritor territory
   - "Silence the Screaming Server" — Destroy type, Interact with 1 server terminal
   - "The Living Will" — Rescue type, Escort NPC to exit
   - "Debug the Widow" — Puzzle type, Custom interaction sequence
   - "Black Mass Disruption" — Assassinate type, Kill named Mourning Collective leader
   - "Mercy Protocol" — Stealth type, Interact with 4 terminals without alerting Wardens
4. **Author 1 main chain QuestDefinitionSO**: "Purge the Core Corruption" with 3-4 sequential objectives
5. **Create DistrictGoalExtensionSO** for each quest — fill BossInsurance, FrontCounterplay, SkippedEcho
6. **Wire goals** into DistrictDefinitionSO.Goals array

---

## Verification

- [ ] DistrictGoalEntry buffer populated at district load with 8+ entries
- [ ] Main chain step 0 auto-accepted on district load
- [ ] Side goals discoverable (Available state) at appropriate zone NPCs/terminals
- [ ] Completing a goal with BossInsuranceType != None records insurance effect
- [ ] Completing a goal with FrontCounterplay != None modifies Front behavior
- [ ] Skipped goals generate Echo entities on district exit
- [ ] Main chain completion sets DistrictState.BossUnlocked = true
- [ ] Main chain sequential: step N+1 becomes Available only after step N completes
- [ ] Trace modifier applied on goal completion (stealth goals reduce trace)
- [ ] Goal→Zone requirement respected (objective only progresses in correct zone)

---

## Validation

```csharp
// File: Assets/Editor/District/GoalValidator.cs
// Validates all DistrictGoalExtensionSO assets:
//
//   1. Quest chain completeness:
//      [ERROR] IsMainChain=true but no QuestDefinitionSO parent found
//      [ERROR] Main chain steps have gaps (e.g., step 0 and 2 but no 1)
//   2. Boss insurance validity:
//      [ERROR] InsuranceType != None but BossTargetId == 0
//      [WARNING] InsuranceDescription is empty for non-None InsuranceType
//   3. Zone index bounds:
//      [ERROR] RequiredZoneIndex >= zone graph length
//      [ERROR] ObjectiveZoneIndices contains index >= zone graph length
//   4. Front counterplay sanity:
//      [WARNING] CounterplayValue <= 0 for non-None CounterplayType
//      [ERROR] RedirectFront CounterplayValue references invalid zone index
//   5. Echo generation:
//      [WARNING] Non-main-chain goal with null SkippedEchoDefinition
```

---

## Live Tuning

| Parameter | Source | Effect |
|-----------|--------|--------|
| CounterplayValue | DistrictGoalExtensionSO | Magnitude of Front counterplay effect |
| TraceModifier | DistrictGoalExtensionSO | Trace change on goal completion |
| SlowSpread duration | CounterplayValue (seconds) | How long Front spread rate is halved |
| DelayPhase duration | CounterplayValue (seconds) | How long next phase transition is delayed |

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Debug/GoalDebugOverlay.cs
// Development builds, toggle: Ctrl+F11
//   - Floating goal markers at RequiredZoneIndex centroid
//   - Goal progress text: "Sever the Grief-Link: 2/3"
//   - Boss insurance icon next to completed goals with InsuranceType != None
//   - Front counterplay effect: pulsing zone overlay when SlowSpread/PurgeZone active
//   - Main chain step indicator: numbered waypoints (1→2→3→Boss)
//   - Skipped goal warning: red X on goals that will generate echoes
```
