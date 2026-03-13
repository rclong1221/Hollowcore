# EPIC 11.1: Rival Definition & Simulation

**Status**: Planning
**Epic**: EPIC 11 — Rival Operators
**Priority**: Critical — Foundation for all rival mechanics
**Dependencies**: Framework: AI/, Loot/; EPIC 4 (Districts & graph structure), EPIC 3 (Front system)

---

## Overview

Defines who rival operator teams are and how they behave across the expedition. Each rival is a ScriptableObject-driven definition with team composition, build preferences, risk tolerance, and personality. A lightweight simulation system advances rival positions through the district graph on each gate transition, resolving outcomes probabilistically without running full AI gameplay. Rivals exist in one of three states: Alive (actively exploring), Dead (bodies left in districts), or Extracted (successfully left the expedition).

---

## Component Definitions

### RivalState Enum

```csharp
// File: Assets/Scripts/Rivals/Components/RivalComponents.cs
namespace Hollowcore.Rivals
{
    public enum RivalState : byte
    {
        Alive = 0,      // Actively exploring districts
        Dead = 1,        // Team wiped — bodies remain as loot
        Extracted = 2    // Successfully left the expedition
    }

    public enum BuildStyle : byte
    {
        Heavy = 0,       // High armor, slow, front-line fighters
        Stealth = 1,     // Low profile, avoids Front, back-route preference
        Balanced = 2,    // Mixed loadout, moderate risk
        Specialist = 3   // Focused build (e.g., all-melee, all-ranged, hacker)
    }

    public enum RivalPersonality : byte
    {
        Aggressive = 0,  // Picks fights, pushes deep, high risk
        Cautious = 1,    // Avoids confrontation, backtrack-heavy
        Mercantile = 2,  // Trades first, fights last
        Desperate = 3,   // Low resources, unpredictable
        Professional = 4 // Efficient, neutral, business-only
    }
}
```

### RivalSimState (IComponentData)

Tracks the runtime simulation state of a single rival team within an expedition. Stored on a dedicated rival entity (one per team).

```csharp
// File: Assets/Scripts/Rivals/Components/RivalComponents.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Runtime simulation state for a rival operator team.
    /// One entity per rival team, created at expedition start.
    /// NOT ghost-replicated — server-only simulation.
    /// </summary>
    public struct RivalSimState : IComponentData
    {
        /// <summary>Unique ID referencing the RivalOperatorSO definition.</summary>
        public int RivalDefinitionId;

        /// <summary>Current district the rival team occupies (-1 = not yet entered).</summary>
        public int CurrentDistrictId;

        /// <summary>Previous district (for trail marker placement direction).</summary>
        public int PreviousDistrictId;

        /// <summary>Current lifecycle state.</summary>
        public RivalState State;

        /// <summary>Number of surviving team members (0 = Dead).</summary>
        public byte SurvivingMembers;

        /// <summary>Cumulative risk taken this expedition (affects death probability).</summary>
        public float AccumulatedRisk;

        /// <summary>How many districts the rival has visited this expedition.</summary>
        public int DistrictsVisited;

        /// <summary>Expedition-local seed for deterministic outcome rolls.</summary>
        public uint SimulationSeed;

        /// <summary>Gate transition count when rival last moved (prevents double-sim).</summary>
        public int LastSimulatedTransition;

        /// <summary>Display name cached from SO for UI/debug.</summary>
        public FixedString64Bytes TeamName;
    }
}
```

### RivalTeamEntry (IBufferElementData)

Buffer on the expedition singleton entity listing all active rival teams.

```csharp
// File: Assets/Scripts/Rivals/Components/RivalComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Buffer element listing all rival teams in the current expedition.
    /// Stored on the expedition singleton entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct RivalTeamEntry : IBufferElementData
    {
        /// <summary>Entity carrying RivalSimState for this team.</summary>
        public Entity RivalEntity;

        /// <summary>Cached rival definition ID for fast lookup.</summary>
        public int RivalDefinitionId;

        /// <summary>Cached state for query filtering without component lookup.</summary>
        public RivalState CachedState;

        /// <summary>Cached district for proximity checks.</summary>
        public int CachedDistrictId;
    }
}
```

### RivalOutcomeEntry (IBufferElementData)

Tracks what happened to a rival in each district they visited.

```csharp
// File: Assets/Scripts/Rivals/Components/RivalComponents.cs
namespace Hollowcore.Rivals
{
    public enum RivalOutcome : byte
    {
        Passed = 0,          // Traversed without incident
        ClearedEnemies = 1,  // Killed enemies in the district
        LootedPOIs = 2,      // Opened containers/bought vendors
        TriggeredAlarm = 3,  // Advanced the Front
        LostMember = 4,      // A team member died here
        TeamWiped = 5,       // Entire team died here
        Extracted = 6,       // Left expedition from this district
        CompletedObjective = 7 // Finished a side objective
    }

    /// <summary>
    /// History buffer on the rival entity recording outcomes per district.
    /// Read by TrailMarkerSystem (11.2) to stamp evidence.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RivalOutcomeEntry : IBufferElementData
    {
        public int DistrictId;
        public RivalOutcome Outcome;
        public byte MembersLostHere;

        /// <summary>Zone ID where the key event occurred (body placement, alarm trigger).</summary>
        public int EventZoneId;
    }
}
```

---

## ScriptableObject Definitions

### RivalOperatorSO

```csharp
// File: Assets/Scripts/Rivals/Definitions/RivalOperatorSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Rivals.Definitions
{
    [CreateAssetMenu(fileName = "NewRival", menuName = "Hollowcore/Rivals/Rival Operator")]
    public class RivalOperatorSO : ScriptableObject
    {
        [Header("Identity")]
        public int RivalId;
        public string TeamName;
        [TextArea] public string Description;
        public Sprite TeamIcon;

        [Header("Composition")]
        [Range(1, 4)]
        public int MemberCount = 3;
        public BuildStyle BuildStyle;

        [Header("Behavior")]
        [Tooltip("District type IDs this team prefers to explore")]
        public List<int> PreferredDistricts;
        [Range(0f, 1f)]
        [Tooltip("0 = conservative (avoids Front), 1 = reckless (dives deep)")]
        public float RiskTolerance = 0.5f;
        public RivalPersonality Personality;

        [Header("Equipment")]
        [Range(1, 5)]
        [Tooltip("Determines loot quality on their bodies and combat effectiveness")]
        public int EquipmentTier = 2;
        [Tooltip("Limb definitions their members carry (for body loot)")]
        public List<int> EquippedLimbIds;

        [Header("Simulation Tuning")]
        [Tooltip("Base probability of surviving a district traversal (modified by risk, Front, tier)")]
        [Range(0f, 1f)]
        public float BaseSurvivalRate = 0.85f;
        [Tooltip("Probability of triggering an alarm per district")]
        [Range(0f, 1f)]
        public float AlarmTriggerRate = 0.15f;
        [Tooltip("Probability of looting POIs in a district")]
        [Range(0f, 1f)]
        public float LootRate = 0.4f;
        [Tooltip("Average districts visited before attempting extraction")]
        public int TargetExpeditionDepth = 5;

        [Header("Encounter")]
        [Tooltip("Dialogue tree ID for neutral encounters")]
        public int NeutralDialogueId;
        [Tooltip("Dialogue tree ID for hostile encounters")]
        public int HostileDialogueId;
        [Tooltip("AI behavior profile for live combat encounters")]
        public int CombatBehaviorId;
    }
}
```

### RivalPoolSO

```csharp
// File: Assets/Scripts/Rivals/Definitions/RivalPoolSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Rivals.Definitions
{
    /// <summary>
    /// Pool of possible rival teams for expedition generation.
    /// The expedition picks 1-3 rivals from this pool based on seed.
    /// </summary>
    [CreateAssetMenu(fileName = "RivalPool", menuName = "Hollowcore/Rivals/Rival Pool")]
    public class RivalPoolSO : ScriptableObject
    {
        public List<RivalOperatorSO> AvailableRivals;

        [Tooltip("Min rival teams per expedition")]
        [Range(1, 4)]
        public int MinRivals = 1;

        [Tooltip("Max rival teams per expedition")]
        [Range(1, 4)]
        public int MaxRivals = 3;
    }
}
```

---

## Systems

### RivalSpawnSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/RivalSpawnSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// Runs once per expedition start:
//   1. Read RivalPoolSO via blob/singleton config
//   2. Using expedition seed, select 1-3 rival teams from pool
//   3. For each selected rival:
//      a. Create rival entity with RivalSimState (State=Alive, CurrentDistrictId=-1)
//      b. Add RivalOutcomeEntry buffer (empty)
//   4. Populate RivalTeamEntry buffer on expedition singleton
//   5. Log rival roster to Analytics/ for run telemetry
```

### RivalSimulationSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/RivalSimulationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: GateTransitionSystem (EPIC 4)
//
// Core simulation loop — runs on each player gate transition:
//   1. Detect gate transition event (GateTransitionCount changed)
//   2. For each rival with State == Alive and LastSimulatedTransition < current:
//      a. Advance rival position through district graph:
//         - Weight edges toward PreferredDistricts
//         - Avoid districts with Front phase > (RiskTolerance * MaxFrontPhase)
//         - Deterministic choice using SimulationSeed + transition count
//      b. Resolve district outcome via probability roll:
//         - Survival: BaseSurvivalRate * (EquipmentTier / 5) * (1 - FrontPhasePenalty)
//         - Alarm: AlarmTriggerRate * (1 + FrontPhase * 0.1)
//         - Loot: LootRate
//         - Member loss: (1 - survival) per member
//      c. Update RivalSimState (district, members, risk accumulation)
//      d. Append RivalOutcomeEntry for the district
//      e. If SurvivingMembers == 0: State = Dead
//      f. If DistrictsVisited >= TargetExpeditionDepth: probability of extraction
//      g. Update cached fields in RivalTeamEntry buffer
//   3. Fire RivalSimulationEvent for downstream systems (11.2, 11.3)
//
// Seed determinism: all rolls use Unity.Mathematics.Random(SimulationSeed ^ transitionCount)
```

### RivalStateQuerySystem

```csharp
// File: Assets/Scripts/Rivals/Systems/RivalStateQuerySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: RivalSimulationSystem
//
// Utility system providing query helpers:
//   1. GetRivalsInDistrict(districtId) → NativeList<Entity>
//   2. GetAliveRivals() → NativeList<Entity>
//   3. GetRivalStateForDistrict(districtId) → list of outcomes
//   4. Updates RivalTeamEntry cache for fast buffer scans
//
// Used by: TrailMarkerSystem (11.2), RivalEncounterSystem (11.3),
//          ScarMapMarkerSystem (12.1)
```

---

## Setup Guide

1. **Create `Assets/Scripts/Rivals/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/
2. **Create assembly definition** `Hollowcore.Rivals.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.Collections`, `Unity.Mathematics`, `Hollowcore.Districts` (EPIC 4)
3. **Create rival definitions**: `Assets/Data/Rivals/` with 4-6 starting RivalOperatorSO assets covering each BuildStyle
4. **Create RivalPoolSO**: `Assets/Data/Rivals/DefaultRivalPool.asset` referencing the above rivals
5. **Create RivalConfigAuthoring** MonoBehaviour on the expedition manager prefab, referencing the RivalPoolSO
6. Baker converts RivalPoolSO into a blob asset for runtime access
7. Verify RivalSpawnSystem creates rival entities on expedition start
8. Verify RivalSimulationSystem advances rivals on gate transitions

---

## Verification

- [ ] RivalOperatorSO assets created with valid data for all BuildStyle variants
- [ ] RivalPoolSO correctly references available rivals
- [ ] RivalSpawnSystem creates 1-3 rival entities on expedition start (seed-deterministic)
- [ ] Each rival entity has RivalSimState + RivalOutcomeEntry buffer
- [ ] RivalTeamEntry buffer populated on expedition singleton
- [ ] RivalSimulationSystem advances rival position on gate transition
- [ ] Rival district selection respects PreferredDistricts weighting
- [ ] Rival avoids high-Front districts when RiskTolerance is low
- [ ] Survival probability correctly factors EquipmentTier and Front phase
- [ ] Member loss tracked correctly; State transitions to Dead when SurvivingMembers == 0
- [ ] Extraction probability increases as DistrictsVisited approaches TargetExpeditionDepth
- [ ] Same expedition seed produces identical rival behavior across runs
- [ ] RivalOutcomeEntry history records each district visit with correct outcome
- [ ] No simulation occurs for Dead or Extracted rivals

---

## BlobAsset Pipeline

RivalOperatorSO is read by simulation, encounter, and trail marker systems. Convert to blob for Burst-compatible AI simulation.

```csharp
// File: Assets/Scripts/Rivals/Blobs/RivalOperatorBlob.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Burst-compatible blob of RivalOperatorSO data.
    /// Used by RivalSimulationSystem for deterministic probability rolls without managed access.
    /// </summary>
    public struct RivalOperatorBlob
    {
        public int RivalId;
        public BlobString TeamName;
        public int MemberCount;
        public BuildStyle BuildStyle;
        public float RiskTolerance;
        public RivalPersonality Personality;
        public int EquipmentTier;
        public float BaseSurvivalRate;
        public float AlarmTriggerRate;
        public float LootRate;
        public int TargetExpeditionDepth;
        public BlobArray<int> PreferredDistricts;
        public BlobArray<int> EquippedLimbIds;
        public int NeutralDialogueId;
        public int HostileDialogueId;
        public int CombatBehaviorId;
    }

    public struct RivalOperatorDatabase
    {
        /// <summary>All rival definitions. Indexed by lookup, not by RivalId directly.</summary>
        public BlobArray<RivalOperatorBlob> Rivals;
    }

    public struct RivalOperatorDatabaseRef : IComponentData
    {
        public BlobAssetReference<RivalOperatorDatabase> Value;
    }
}
```

```csharp
// File: Assets/Scripts/Rivals/Authoring/RivalDatabaseAuthoring.cs
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Hollowcore.Rivals.Authoring
{
    public class RivalDatabaseAuthoring : MonoBehaviour
    {
        public Definitions.RivalPoolSO RivalPool;
    }

    public class RivalDatabaseBaker : Baker<RivalDatabaseAuthoring>
    {
        public override void Bake(RivalDatabaseAuthoring authoring)
        {
            if (authoring.RivalPool == null || authoring.RivalPool.AvailableRivals == null) return;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<RivalOperatorDatabase>();
            var rivals = authoring.RivalPool.AvailableRivals;
            var arr = builder.Allocate(ref root.Rivals, rivals.Count);

            for (int i = 0; i < rivals.Count; i++)
            {
                var so = rivals[i];
                arr[i].RivalId = so.RivalId;
                builder.AllocateString(ref arr[i].TeamName, so.TeamName);
                arr[i].MemberCount = so.MemberCount;
                arr[i].BuildStyle = so.BuildStyle;
                arr[i].RiskTolerance = so.RiskTolerance;
                arr[i].Personality = so.Personality;
                arr[i].EquipmentTier = so.EquipmentTier;
                arr[i].BaseSurvivalRate = so.BaseSurvivalRate;
                arr[i].AlarmTriggerRate = so.AlarmTriggerRate;
                arr[i].LootRate = so.LootRate;
                arr[i].TargetExpeditionDepth = so.TargetExpeditionDepth;
                arr[i].NeutralDialogueId = so.NeutralDialogueId;
                arr[i].HostileDialogueId = so.HostileDialogueId;
                arr[i].CombatBehaviorId = so.CombatBehaviorId;

                var pd = builder.Allocate(ref arr[i].PreferredDistricts, so.PreferredDistricts.Count);
                for (int d = 0; d < so.PreferredDistricts.Count; d++) pd[d] = so.PreferredDistricts[d];

                var el = builder.Allocate(ref arr[i].EquippedLimbIds, so.EquippedLimbIds.Count);
                for (int l = 0; l < so.EquippedLimbIds.Count; l++) el[l] = so.EquippedLimbIds[l];
            }

            var blobRef = builder.CreateBlobAssetReference<RivalOperatorDatabase>(Allocator.Persistent);
            builder.Dispose();

            var entity = GetEntity(TransformUsageFlags.None);
            AddBlobAsset(ref blobRef, out _);
            AddComponent(entity, new RivalOperatorDatabaseRef { Value = blobRef });
        }
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Rivals/Definitions/RivalOperatorSO.cs (append to class)

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(TeamName))
            Debug.LogError($"[RivalOperator] {name}: TeamName is empty.", this);
        if (RivalId < 0)
            Debug.LogError($"[RivalOperator] {name}: RivalId cannot be negative.", this);
        if (MemberCount < 1 || MemberCount > 4)
            Debug.LogError($"[RivalOperator] {name}: MemberCount must be 1-4.", this);
        if (BaseSurvivalRate <= 0f || BaseSurvivalRate > 1f)
            Debug.LogWarning($"[RivalOperator] {name}: BaseSurvivalRate {BaseSurvivalRate} outside valid range (0,1].", this);
        if (AlarmTriggerRate < 0f || AlarmTriggerRate > 1f)
            Debug.LogError($"[RivalOperator] {name}: AlarmTriggerRate must be 0-1.", this);
        if (EquipmentTier < 1 || EquipmentTier > 5)
            Debug.LogError($"[RivalOperator] {name}: EquipmentTier must be 1-5.", this);
        if (PreferredDistricts == null || PreferredDistricts.Count == 0)
            Debug.LogWarning($"[RivalOperator] {name}: No PreferredDistricts — rival will choose randomly.", this);
        if (NeutralDialogueId <= 0)
            Debug.LogWarning($"[RivalOperator] {name}: NeutralDialogueId not set — neutral encounters will have no dialogue.", this);
        if (CombatBehaviorId <= 0)
            Debug.LogWarning($"[RivalOperator] {name}: CombatBehaviorId not set — combat encounters will use default AI.", this);
    }
#endif
```

```csharp
// File: Assets/Editor/Rivals/RivalBuildValidator.cs
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Hollowcore.Rivals.Editor
{
    public class RivalBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:RivalOperatorSO");
            var rivals = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<Definitions.RivalOperatorSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(so => so != null).ToList();

            // Name uniqueness
            var names = new HashSet<string>();
            foreach (var r in rivals)
                if (!names.Add(r.TeamName))
                    Debug.LogError($"[RivalBuildValidation] Duplicate TeamName: '{r.TeamName}' in {r.name}");

            // RivalId uniqueness
            var ids = new HashSet<int>();
            foreach (var r in rivals)
                if (!ids.Add(r.RivalId))
                    Debug.LogError($"[RivalBuildValidation] Duplicate RivalId: {r.RivalId} in {r.name}");

            // Dialogue tree reference validity (check asset existence)
            foreach (var r in rivals)
            {
                if (r.NeutralDialogueId > 0)
                {
                    // Validate dialogue tree ID exists in dialogue database
                    // (cross-reference with Hollowcore.Dialogue asset database)
                }
            }

            // Pool coverage: at least one rival per BuildStyle
            var styles = rivals.Select(r => r.BuildStyle).Distinct().ToList();
            if (styles.Count < 4)
                Debug.LogWarning($"[RivalBuildValidation] Only {styles.Count}/4 BuildStyles covered by rival definitions.");

            Debug.Log($"[RivalBuildValidation] Validated {rivals.Count} rival operator definitions.");
        }
    }
}
```

---

## Editor Tooling — Rival Operator Designer

```csharp
// File: Assets/Editor/RivalWorkstation/RivalOperatorDesigner.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Rivals.Editor
{
    /// <summary>
    /// Rival Operator Designer — custom inspector and preview window.
    /// Features:
    ///   - Personality slider preview: drag RiskTolerance/Personality sliders,
    ///     see predicted behavior summary ("This team will dive deep, trigger alarms,
    ///     and die in ~3 districts")
    ///   - Encounter behavior preview: given personality + equipment tier + Trace level,
    ///     show probability breakdown of encounter types (Trade 40%, Intel 35%, Combat 25%)
    ///   - Simulation preview: "Run 100 expeditions" button shows survival curve,
    ///     average districts visited, alarm trigger rate, body placement distribution
    ///   - Preferred district highlighting on expedition graph minimap
    /// </summary>
    public class RivalOperatorDesigner : EditorWindow
    {
        [MenuItem("Hollowcore/Rival Designer")]
        public static void Open() => GetWindow<RivalOperatorDesigner>("Rival Designer");

        private Definitions.RivalOperatorSO _selectedRival;
        private int _simExpeditions = 100;

        private void OnGUI()
        {
            _selectedRival = (Definitions.RivalOperatorSO)EditorGUILayout.ObjectField(
                "Rival", _selectedRival, typeof(Definitions.RivalOperatorSO), false);
            if (_selectedRival == null) { EditorGUILayout.HelpBox("Select a RivalOperatorSO.", MessageType.Info); return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Behavior Prediction", EditorStyles.boldLabel);

            // Compute predicted metrics from SO values
            float expectedDistricts = _selectedRival.TargetExpeditionDepth *
                                       _selectedRival.BaseSurvivalRate;
            float alarmRate = _selectedRival.AlarmTriggerRate;
            string riskDesc = _selectedRival.RiskTolerance > 0.7f ? "Reckless diver" :
                              _selectedRival.RiskTolerance > 0.4f ? "Moderate explorer" : "Conservative backtracker";

            EditorGUILayout.LabelField($"Profile: {riskDesc} ({_selectedRival.Personality})");
            EditorGUILayout.LabelField($"Expected survival: {expectedDistricts:F1} districts");
            EditorGUILayout.LabelField($"Alarm rate: {alarmRate:P0} per district");

            EditorGUILayout.Space();
            _simExpeditions = EditorGUILayout.IntField("Sim Expeditions", _simExpeditions);
            if (GUILayout.Button("Run Behavior Simulation"))
            {
                // Offline simulation: run N expeditions using RivalSimulationSystem logic
                // Plot survival curve, district distribution, outcome histogram
            }
        }
    }
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Rivals/Debug/RivalLiveTuning.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Runtime-tunable rival simulation parameters:
    ///   - Per-rival: BaseSurvivalRate, AlarmTriggerRate, LootRate, RiskTolerance
    ///   - Global: encounter frequency multiplier, hostile threshold override
    ///   - RivalSimulationSystem reads from static RivalTuningOverrides before blob values
    ///
    /// Exposed via Rival Designer window during play mode.
    /// Changes apply on next gate transition (next simulation tick).
    /// </summary>
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Rivals/Debug/RivalDebugOverlay.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Debug overlay for rival simulation (development builds):
    /// - District map overlay: rival positions shown as colored team icons
    ///   - Green = Alive, Red = Dead (with body icon), Grey = Extracted
    /// - Aggression state badge: personality + current risk accumulation
    /// - Path history: dotted line showing districts visited in order
    /// - Outcome log: per-district outcome listed on hover
    /// - Encounter trigger radius: when player enters rival's district,
    ///   show probability ring (radius = encounter chance * visual scale)
    /// - Toggle: /debug rivals overlay
    ///
    /// Implementation: RivalDebugOverlaySystem (PresentationSystemGroup, #if DEVELOPMENT_BUILD)
    /// reads RivalSimState + RivalTeamEntry buffer, pushes to managed map overlay renderer.
    /// </summary>
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/RivalWorkstation/RivalSimulationTester.cs
namespace Hollowcore.Rivals.Editor
{
    /// <summary>
    /// Offline rival simulation testing (integrated into Rival Designer window):
    ///
    /// Encounter frequency distribution:
    ///   - Run 1000 expeditions with N rival teams
    ///   - Plot histogram: encounters per expedition (expected ~1 per 2-3 districts)
    ///   - Breakdown by type: neutral vs competitive vs hostile
    ///   - Validate hostile encounters only appear at Trace >= threshold
    ///
    /// Player-vs-rival win rate projections:
    ///   - Given player equipment tier vs rival equipment tier
    ///   - Estimate combat outcome probability (uses AI combat simulation heuristic)
    ///   - Plot win rate curve: player tier 1-5 vs each rival tier
    ///   - Highlight unfair matchups (win rate < 20% or > 80%)
    ///
    /// Rival lifecycle analysis:
    ///   - Average districts visited before death/extraction per rival definition
    ///   - Body distribution: which districts accumulate the most rival bodies
    ///   - Alarm cascade: how many Front phases rivals advance on average per expedition
    ///   - Trail marker density: average markers per district per expedition
    ///
    /// Export: CSV summary, clipboard-friendly tables.
    /// </summary>
}
```
