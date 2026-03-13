# EPIC 14.1: Boss Definition Template

**Status**: Planning
**Epic**: EPIC 14 — Boss System & Variant Clauses
**Dependencies**: Framework: Combat/ (EncounterState, EncounterTriggerDefinition), AI/, Roguelite/ (RuntimeDifficultyScale); EPIC 7 (Strife), EPIC 8 (Trace)

---

## Overview

Universal data template for every boss in the game. Each boss is defined by a ScriptableObject that describes its identity, phase structure, attack patterns, health scaling, and arena reference. On top of this base sits the variant clause system: a matrix of conditional modifiers that activate or deactivate based on side goals, Strife cards, Front phase, and Trace level. Side goals function as "insurance" -- completing them disables the corresponding boss clause, skipping them enables it, making the fight harder.

---

## Component Definitions

### BossVariantTriggerType Enum

```csharp
// File: Assets/Scripts/Boss/Components/BossComponents.cs
namespace Hollowcore.Boss
{
    /// <summary>
    /// What condition activates a boss variant clause.
    /// </summary>
    public enum BossVariantTriggerType : byte
    {
        SideGoalSkipped = 0,    // Clause active when player did NOT complete the linked side goal
        SideGoalCompleted = 1,  // Clause active when player DID complete (rare — reward-type clauses)
        StrifeCard = 2,         // Clause active when a specific Strife card is in play
        FrontPhase = 3,         // Clause active when district Front has reached a phase threshold
        TraceLevel = 4          // Clause active when player Trace is at or above a threshold
    }
}
```

### BossVariantState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/BossComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss
{
    /// <summary>
    /// Runtime state for the active boss's variant configuration.
    /// Stored on the boss encounter entity (not the boss prefab — resolved at fight start).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BossVariantState : IComponentData
    {
        /// <summary>BossDefinitionSO ID.</summary>
        [GhostField] public int BossId;

        /// <summary>Bitmask of active variant clauses (up to 32 clauses per boss).</summary>
        [GhostField] public uint ActiveClauseMask;

        /// <summary>Health multiplier from Front phase scaling (1.0 = base, 1.5 = phase 4).</summary>
        [GhostField(Quantization = 100)] public float HealthMultiplier;

        /// <summary>Whether Trace-based reinforcements are enabled (Trace 4+).</summary>
        [GhostField] public bool ReinforcementsEnabled;

        /// <summary>Number of active variant clauses (cached for UI display).</summary>
        [GhostField] public byte ActiveClauseCount;

        public bool IsClauseActive(int clauseIndex) =>
            (ActiveClauseMask & (1u << clauseIndex)) != 0;

        public void SetClauseActive(int clauseIndex) =>
            ActiveClauseMask |= (1u << clauseIndex);

        public void SetClauseInactive(int clauseIndex) =>
            ActiveClauseMask &= ~(1u << clauseIndex);
    }
}
```

### ActiveBossClauseBuffer (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/BossComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Boss
{
    /// <summary>
    /// Buffer of currently active variant clauses on the boss encounter entity.
    /// Each element describes one active clause for UI display and system queries.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ActiveBossClauseBuffer : IBufferElementData
    {
        /// <summary>Index into the BossDefinitionSO's variant clause list.</summary>
        public byte ClauseIndex;

        /// <summary>What triggered this clause.</summary>
        public BossVariantTriggerType TriggerType;

        /// <summary>Short display name for the modifier UI overlay.</summary>
        public FixedString64Bytes DisplayName;
    }
}
```

---

## ScriptableObject Definitions

### BossDefinitionSO

```csharp
// File: Assets/Scripts/Boss/Definitions/BossDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Boss.Definitions
{
    [CreateAssetMenu(fileName = "NewBoss", menuName = "Hollowcore/Boss/Boss Definition")]
    public class BossDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int BossId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Portrait;
        public GameObject Prefab;

        [Header("Arena")]
        [Tooltip("ArenaDefinitionSO for this boss's fighting space")]
        public ArenaDefinitionSO ArenaDefinition;

        [Header("Phases")]
        [Tooltip("Ordered list of boss phases. Phase 0 is the starting phase.")]
        public List<BossPhaseDefinition> Phases = new();

        [Header("Attack Patterns")]
        [Tooltip("Base attack patterns available across all phases")]
        public List<BossAttackPatternSO> BaseAttackPatterns = new();

        [Header("Health Scaling")]
        public float BaseHealth = 10000f;
        [Tooltip("Health multiplier per player in co-op (1.0 = no scaling)")]
        public float CoopHealthScale = 0.6f;
        [Tooltip("Health multiplier curve indexed by RuntimeDifficultyScale")]
        public AnimationCurve DifficultyHealthCurve = AnimationCurve.Linear(0f, 1f, 1f, 2f);

        [Header("Variant Clauses")]
        public List<BossVariantClauseSO> VariantClauses = new();

        [Header("Loot")]
        [Tooltip("Guaranteed drops on boss kill")]
        public LootTableReference BossLootTable;
    }

    [System.Serializable]
    public class BossPhaseDefinition
    {
        public string PhaseName;
        [Tooltip("Health threshold (0-1) to transition INTO this phase. Phase 0 starts at 1.0.")]
        [Range(0f, 1f)] public float HealthThreshold = 0.5f;
        [Tooltip("Additional attack patterns unlocked in this phase")]
        public List<BossAttackPatternSO> PhaseAttackPatterns = new();
        [Tooltip("Arena changes triggered on phase entry (hazards, layout shifts)")]
        public List<ArenaEventSO> ArenaEvents = new();
        [TextArea] public string PhaseDialogue;
    }

    [System.Serializable]
    public struct LootTableReference
    {
        [Tooltip("Reference to Loot/ framework LootTableSO")]
        public int LootTableId;
    }
}
```

### BossVariantClauseSO

```csharp
// File: Assets/Scripts/Boss/Definitions/BossVariantClauseSO.cs
using UnityEngine;

namespace Hollowcore.Boss.Definitions
{
    [CreateAssetMenu(fileName = "NewBossClause", menuName = "Hollowcore/Boss/Variant Clause")]
    public class BossVariantClauseSO : ScriptableObject
    {
        [Header("Identity")]
        public int ClauseId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Trigger")]
        public BossVariantTriggerType TriggerType;

        [Tooltip("For SideGoal triggers: the side goal ID that disables/enables this clause.\n" +
                 "For StrifeCard: the Strife card ID.\n" +
                 "For FrontPhase: the phase threshold (1-4).\n" +
                 "For TraceLevel: the Trace threshold (1-5).")]
        public int TriggerCondition;

        [Header("Mechanic Modification")]
        [Tooltip("What this clause adds to the boss fight when active")]
        public BossClauseEffect Effect;

        [Header("Counter Token")]
        [Tooltip("If non-zero, this clause can be disabled by a BossCounterToken with this ClauseId")]
        public int CounterTokenClauseId;
    }

    [System.Serializable]
    public class BossClauseEffect
    {
        public BossClauseEffectType EffectType;
        [Tooltip("For AddAttack: BossAttackPatternSO reference")]
        public BossAttackPatternSO AddedAttack;
        [Tooltip("For StatMultiplier: multiplier applied (e.g. 1.25 = +25% health)")]
        public float StatMultiplier = 1f;
        [Tooltip("For SpawnReinforcements: enemy wave definition")]
        public ReinforcementWaveSO ReinforcementWave;
        [Tooltip("For EnvironmentHazard: hazard to activate in arena")]
        public ArenaEventSO EnvironmentHazard;
        [TextArea] public string EffectDescription;
    }

    public enum BossClauseEffectType : byte
    {
        AddAttack = 0,           // Boss gains an additional attack pattern
        StatMultiplier = 1,      // Boss health/damage scaled
        SpawnReinforcements = 2, // Mid-fight enemy wave
        EnvironmentHazard = 3,   // Arena hazard activated
        DisableWeakpoint = 4,    // A weakness the player could exploit is removed
        ModifyPhaseThreshold = 5 // Phase transitions happen earlier/later
    }
}
```

---

## Systems

### BossClauseEvaluationSystem

```csharp
// File: Assets/Scripts/Boss/Systems/BossClauseEvaluationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: BossEncounterSystem
//
// Evaluates variant clauses when a boss encounter begins.
// Runs ONCE at encounter start (triggered by EncounterState transition to PreFight).
//
// For each BossVariantState entity (created at encounter start):
//   1. Load BossDefinitionSO variant clause list
//   2. For each clause, evaluate trigger:
//      a. SideGoalSkipped: check RunHistorySaveModule for side goal completion → if NOT completed, activate
//      b. SideGoalCompleted: check RunHistorySaveModule → if completed, activate
//      c. StrifeCard: check active Strife card ID → if matches, activate
//      d. FrontPhase: check district Front phase → if >= threshold, activate
//      e. TraceLevel: check player Trace → if >= threshold, activate
//   3. For each active clause, check BossCounterTokens in player inventory
//      → if matching CounterTokenClauseId found, deactivate clause and consume token
//   4. Set ActiveClauseMask and populate ActiveBossClauseBuffer
//   5. Calculate HealthMultiplier from FrontPhase clauses
//   6. Set ReinforcementsEnabled from TraceLevel clauses
```

### BossDefinitionBaker

```csharp
// File: Assets/Scripts/Boss/Authoring/BossDefinitionAuthoring.cs
// Attached to boss prefab root. Bakes BossDefinitionSO into blob asset.
//
// Baker:
//   1. Read BossDefinitionSO reference
//   2. Bake phase list into BlobArray<BossPhaseBlob>
//   3. Bake variant clause IDs into BlobArray<int>
//   4. Add BossTag IComponentData for query filtering
//   5. Add BossVariantState with default values (populated at runtime by BossClauseEvaluationSystem)
//   6. Add ActiveBossClauseBuffer (empty, populated at runtime)
```

---

## Setup Guide

1. **Create `Assets/Scripts/Boss/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/
2. **Create assembly definition** `Hollowcore.Boss.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`, `Hollowcore.Roguelite`
3. Create BossDefinitionSO and BossVariantClauseSO assets in `Assets/Data/Boss/`
4. For vertical slice: create definitions for Grandmother Null, The Foreman, King of Heights
5. Add BossDefinitionAuthoring to each boss prefab
6. Create at least 3 BossVariantClauseSO per boss:
   - 1x SideGoalSkipped clause (insurance mechanic)
   - 1x StrifeCard clause
   - 1x FrontPhase clause
7. Wire BossClauseEvaluationSystem to read from RunHistorySaveModule for side goal state

---

## Verification

- [ ] BossDefinitionSO serializes all fields (phases, clauses, health scaling)
- [ ] BossVariantClauseSO correctly stores trigger type and condition
- [ ] BossVariantState ActiveClauseMask bit operations work for all 32 clause indices
- [ ] ActiveBossClauseBuffer populates with correct clause data at encounter start
- [ ] SideGoalSkipped clause activates when side goal NOT completed
- [ ] SideGoalSkipped clause deactivates when side goal completed (insurance works)
- [ ] StrifeCard clause activates only when matching Strife card is active
- [ ] FrontPhase clause activates at correct phase threshold
- [ ] TraceLevel clause activates at correct Trace threshold
- [ ] Counter tokens in inventory disable matching clauses and are consumed
- [ ] HealthMultiplier correctly reflects Front phase scaling (1.0 / 1.25 / 1.5)
- [ ] BossTag query filters work to distinguish boss entities from regular enemies

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Boss/Authoring/BossDefinitionBlobBaker.cs
// Baked by BossDefinitionAuthoring on boss prefab root.
namespace Hollowcore.Boss.Authoring
{
    // BossDefinitionSO → BossBlob
    //   BlobArray<BossPhaseBlob> Phases
    //     - FixedString32Bytes Name
    //     - float HealthThreshold
    //     - BlobArray<int> AttackPatternIds (indices into base + phase patterns)
    //     - BlobArray<int> ArenaEventIds
    //   BlobArray<BossClauseBlob> Clauses
    //     - int ClauseId
    //     - BossVariantTriggerType TriggerType
    //     - int TriggerCondition
    //     - BossClauseEffectType EffectType
    //     - float StatMultiplier
    //     - int CounterTokenClauseId
    //   BlobArray<float2> DifficultyHealthCurve  // AnimationCurve sampled at 64 points → (t, value)
    //   float BaseHealth, CoopHealthScale
    //   int BossId
    //
    // BossVariantClauseSO → BossClauseBlob (embedded in BossBlob.Clauses)
    //   Each clause bakes TriggerType, TriggerCondition, EffectType, StatMultiplier, CounterTokenClauseId.
    //   BossAttackPatternSO and ReinforcementWaveSO bake to int IDs (resolved at runtime via registries).
    //   ArenaEventSO references bake to int ArenaEventId.
    //
    // AnimationCurve (DifficultyHealthCurve) → BlobArray<float2>
    //   Sampled at 64 uniform points [0..1]. Runtime: lerp between nearest two samples.
    //   Evaluated by BossClauseEvaluationSystem to compute HealthMultiplier from RuntimeDifficultyScale.
    //   Constraint: all sampled values must be > 0 (a zero-health boss is invalid).
}
```

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/BossDefinitionValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Validation rules executed on BossDefinitionSO save / import / workstation refresh:
    //
    // 1. Phase threshold monotonic: Phases[i].HealthThreshold must be strictly decreasing
    //    (Phase 0 = 1.0 implicit, Phase 1 < 1.0, Phase 2 < Phase 1, etc.)
    //    Error if any threshold >= previous threshold or outside (0, 1) range.
    //
    // 2. Clause effect type matches payload:
    //    - AddAttack → AddedAttack != null
    //    - StatMultiplier → StatMultiplier != 1.0 (warn if exactly 1.0 — no-op)
    //    - SpawnReinforcements → ReinforcementWave != null
    //    - EnvironmentHazard → EnvironmentHazard != null
    //    - DisableWeakpoint / ModifyPhaseThreshold → no additional payload required
    //    Error on null reference where required.
    //
    // 3. Counter token references valid boss IDs:
    //    For each BossVariantClauseSO in VariantClauses where CounterTokenClauseId != 0:
    //    → Verify a BossCounterTokenDefinitionSO exists with matching ClauseIdDisabled.
    //    Warning if orphaned (clause expects token that doesn't exist).
    //
    // 4. Arena dimension bounds:
    //    ArenaDefinitionSO.ArenaBounds.size all axes > 5m, < 500m.
    //    KillPlaneY < ArenaBounds.min.y.
    //    Error on out-of-range.
    //
    // 5. Health scaling curve:
    //    Sample DifficultyHealthCurve at 64 points. All values must be > 0.
    //    Warn if any value < 0.5 (boss trivially easy) or > 5.0 (likely data error).
    //
    // 6. Duplicate IDs:
    //    No two BossVariantClauseSO in the same BossDefinitionSO may share ClauseId.
    //    No two BossPhaseDefinition in the same boss may share HealthThreshold (float tolerance 0.001).
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/BossWorkstation/BossWorkstationWindow.cs
// Follows DIG workstation pattern: EditorWindow, sidebar tabs, IWorkstationModule.
namespace Hollowcore.Editor.BossWorkstation
{
    // BossDesignerWorkstation — central editor for boss design and tuning.
    // Sidebar tabs:
    //
    // [1] Phase Timeline Editor (BossPhaseTimelineModule : IWorkstationModule)
    //     - Horizontal bar representing boss HP (100% → 0%)
    //     - Phase boundaries as draggable handles on the bar
    //     - Each phase region color-coded, labeled with phase name
    //     - Click a phase region to expand: shows attack patterns, arena events, dialogue
    //     - Drag to reorder or resize phases (auto-recalculates thresholds)
    //     - Validation overlay: red outline if thresholds non-monotonic
    //
    // [2] Attack Pattern Configurator (BossAttackPatternModule : IWorkstationModule)
    //     - Per-phase attack list: left column = phase selector, right = attack table
    //     - Each attack row: name, timing (cooldown/cast/recovery), damage, telegraph duration
    //     - Drag-reorder priority within phase
    //     - "Add from base" button to pull base attacks into a phase
    //     - Preview: text-based timeline showing attack rotation over 60s
    //
    // [3] Clause Matrix Viewer (BossClauseMatrixModule : IWorkstationModule)
    //     - Rows = side goals (from EPIC 10 SideGoalDefinitionSO references)
    //     - Columns = variant clauses on this boss
    //     - Checkmarks showing which goals disable which clauses
    //     - Color: green = disabled by goal, red = active (skipped), yellow = Strife/Front/Trace
    //     - Click cell to navigate to clause SO or goal SO
    //
    // [4] Health Scaling Curve Editor (BossHealthScalingModule : IWorkstationModule)
    //     - AnimationCurve editor for DifficultyHealthCurve
    //     - Overlay: co-op player count lines (1P, 2P, 3P, 4P)
    //     - Each line = BaseHealth * DifficultyHealthCurve(x) * (1 + (playerCount-1) * CoopHealthScale)
    //     - Y-axis = effective HP, X-axis = RuntimeDifficultyScale [0..1]
    //     - Horizontal markers at phase threshold HP values
    //
    // [5] Difficulty Score Panel (BossDifficultyScoreModule : IWorkstationModule)
    //     - Computed: phaseCount * clauseCount * healthScalingMax → "Boss Difficulty Score"
    //     - Color-coded: green (easy), yellow (medium), orange (hard), red (extreme)
    //     - Comparison row: shows score for all bosses in project
    //
    // [6] DPS Check Calculator (BossDPSCheckModule : IWorkstationModule)
    //     - Input: player DPS (slider 100-2000), number of players, difficulty scale
    //     - Output per phase: "Phase 2 reached in X seconds", "Boss killed in Y seconds"
    //     - Warning if time-to-phase exceeds enrage or structural integrity limits
    //     - "Minimum viable DPS" auto-calc: DPS where enrage timer is barely met
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/BossLiveTuningConfig.cs
namespace Hollowcore.Boss.Debug
{
    // Runtime-tunable values for playtesting. Exposed via BossWorkstation Live Tuning tab
    // and in-game debug console. Changes apply immediately to active boss encounter.
    //
    // Tunable fields (stored in BossLiveTuning singleton, not persisted):
    //   float HealthOverride          // -1 = use definition, >0 = override BaseHealth
    //   float HealthMultiplierOverride // -1 = use computed, >0 = override
    //   float[] PhaseThresholdOverrides // per-phase, -1 = use definition
    //   float AttackDamageMultiplier   // global multiplier on all boss attack damage (default 1.0)
    //   float AttackTimingMultiplier   // scales cooldowns/cast times (0.5 = twice as fast)
    //   float EnrageTimerOverride      // -1 = use definition, >0 = override seconds
    //   bool ForceAllClausesActive     // ignore side goal state, activate every clause
    //   bool ForceNoClausesActive      // deactivate all clauses
    //   int ForcePhaseIndex            // -1 = normal, 0+ = jump to phase immediately
    //
    // BossLiveTuningSystem (ServerSimulation|LocalSimulation, SimulationSystemGroup):
    //   Reads BossLiveTuning singleton each frame.
    //   Applies overrides to BossVariantState, BossPhaseState, attack pattern timing.
    //   Logs override deltas to console for designer awareness.
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/BossDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // In-game debug visualization (enabled via debug console `boss_debug 1`).
    //
    // [1] Boss HP Bar with Phase Markers
    //     - Full-width bar at screen top showing boss HP percentage
    //     - Vertical markers at each phase threshold (labeled "P1", "P2", "P3")
    //     - Current phase region highlighted
    //     - Persistent HP (across death/respawn) shown as ghost bar behind current
    //
    // [2] Active Clause Indicators
    //     - Row of icons below HP bar, one per active variant clause
    //     - Tooltip on hover: clause name, trigger type, effect description
    //     - Clauses disabled by counter tokens shown as struck-through
    //
    // [3] Enrage Timer
    //     - Countdown timer (if boss has enrage mechanic)
    //     - Flashes red in final 30 seconds
    //     - Shows "ENRAGED" when timer expires
    //
    // [4] DPS Meter
    //     - Rolling 10-second DPS window (player damage to boss)
    //     - Displayed below boss HP bar
    //     - Color: green if on pace for kill before enrage, red if behind
    //
    // [5] Attack Telegraph Debug Wireframes
    //     - Gizmo-style wireframes showing active attack telegraph zones
    //     - Color-coded by damage type (red = damage, blue = knockback, yellow = slow)
    //     - Shows remaining telegraph time as shrinking outline
    //     - Arena hazard zones rendered as semi-transparent volumes
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/BossWorkstation/BossFightSimulator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Automated boss fight simulator for balance validation.
    // Runs headless (no rendering) in editor play mode.
    //
    // BossFightSimulator:
    //   Inputs (per simulation batch):
    //     BossDefinitionSO target boss
    //     int simulationCount = 1000
    //     float playerDPS (configurable, default 500)
    //     float dodgeSuccessRate (0-1, default 0.7)
    //     float positioningQuality (0-1, affects hazard avoidance, default 0.6)
    //     int playerCount (1-4)
    //     float difficultyScale (0-1)
    //     uint activeClauseMask (which clauses to simulate active)
    //
    //   Simulated player agent:
    //     - Deals playerDPS damage per second to boss (with variance +/- 20%)
    //     - Dodges attacks with dodgeSuccessRate probability
    //     - Avoids hazards with positioningQuality probability
    //     - Takes damage from un-dodged attacks and hazards
    //     - Dies at 0 HP, respawns after 5s delay (boss HP persists)
    //
    //   Outputs (BossSimulationReport):
    //     float meanTimeToKill, medianTimeToKill, stdDevTimeToKill
    //     float[] phaseTransitionTimes  // mean time to reach each phase
    //     float wipeRate                // fraction of runs where boss was not killed (enrage/collapse)
    //     float meanDeathsPerRun
    //     Dictionary<uint, float> wipeRateByClauseMask  // wipe rate for each clause combo
    //     float minimumViableDPS        // lowest DPS that achieves < 5% wipe rate
    //     bool enrageTimerViable        // true if meanTimeToKill < enrageTimer at given DPS
    //     float[] dpsDistributionBuckets // histogram of effective DPS across runs
    //
    //   Report displayed in BossWorkstation Simulation tab:
    //     - Time-to-kill distribution chart (histogram)
    //     - Phase transition timing waterfall
    //     - Wipe rate by clause combination heatmap
    //     - "DPS Check" pass/fail badge
    //     - Enrage timer viability indicator
    //     - Export to CSV for external analysis
}
