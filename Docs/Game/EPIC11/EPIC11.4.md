# EPIC 11.4: Meta-Expedition Rivals

**Status**: Planning
**Epic**: EPIC 11 — Rival Operators
**Priority**: Low — Cross-run feature, requires multiple play sessions
**Dependencies**: EPIC 11.1 (Rival Definition & Simulation), EPIC 12.1 (Scar Map Data Model), Framework: Persistence/ (RunHistorySaveModule), Roguelite/ (RunStatistics)

---

## Overview

Past expeditions echo forward as rival teams in future runs. The player's own previous expedition data — routes taken, equipment carried, objectives completed or failed, districts where they died — is loaded from the Scar Map persistence layer and converted into a RivalOperatorSO-compatible simulation. The result: you encounter ghosts of your past decisions. Your old corpses wear your old gear, your old routes are cleared paths, your old failures are echoes. This creates a deeply personal meta-narrative where improvement is visible through the lens of rivalry with your former self.

---

## Component Definitions

### PastRunRivalTag (IComponentData)

```csharp
// File: Assets/Scripts/Rivals/Components/PastRunRivalComponents.cs
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Tag marking a rival entity as generated from a past player expedition.
    /// Enables special handling: custom dialogue, gear matching, ghost visual effects.
    /// </summary>
    public struct PastRunRivalTag : IComponentData { }
}
```

### PastRunRivalData (IComponentData)

```csharp
// File: Assets/Scripts/Rivals/Components/PastRunRivalComponents.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Extended data for a past-run rival, linking back to the source expedition.
    /// Stored alongside RivalSimState on the rival entity.
    /// </summary>
    public struct PastRunRivalData : IComponentData
    {
        /// <summary>Expedition ID from which this rival was generated.</summary>
        public int SourceExpeditionId;

        /// <summary>Run number (1-indexed) for display: "Run #14 Ghost".</summary>
        public int RunNumber;

        /// <summary>Total districts visited in the source run.</summary>
        public int SourceDistrictsVisited;

        /// <summary>How the source run ended.</summary>
        public PastRunEndReason EndReason;

        /// <summary>Equipment tier derived from what the player had mid-run.</summary>
        public int DerivedEquipmentTier;

        /// <summary>Display name: "Ghost of Run #N" or custom if player named their operator.</summary>
        public FixedString64Bytes GhostName;
    }

    public enum PastRunEndReason : byte
    {
        Death = 0,          // Player died — rival follows same path and dies at same point
        Extraction = 1,     // Player extracted — rival extracts at same district
        Abandonment = 2     // Player quit — rival wanders then extracts early
    }
}
```

### PastRunRouteEntry (IBufferElementData)

```csharp
// File: Assets/Scripts/Rivals/Components/PastRunRivalComponents.cs
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Ordered sequence of districts the past player visited.
    /// Used by PastRunRivalSystem to drive simulation along the historical route
    /// instead of the probabilistic graph traversal used by normal rivals.
    /// </summary>
    [InternalBufferCapacity(12)]
    public struct PastRunRouteEntry : IBufferElementData
    {
        /// <summary>District ID in visit order.</summary>
        public int DistrictId;

        /// <summary>Gate transition number when this district was entered.</summary>
        public int TransitionIndex;

        /// <summary>What happened here in the source run.</summary>
        public PastRunDistrictOutcome Outcome;

        /// <summary>Equipment snapshot hash at this point in the run (for body loot generation).</summary>
        public int EquipmentSnapshotHash;
    }

    public enum PastRunDistrictOutcome : byte
    {
        Traversed = 0,       // Passed through
        Explored = 1,        // Spent significant time
        CompletedObjective = 2,
        FailedObjective = 3, // This becomes a rival echo
        Died = 4,            // Final district for death runs
        Extracted = 5        // Final district for extraction runs
    }
}
```

---

## Systems

### PastRunRivalSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/PastRunRivalSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
// UpdateAfter: RivalSpawnSystem (EPIC 11.1)
//
// Generates past-run rivals at expedition start:
//   1. Query RunHistorySaveModule for completed expedition records
//   2. Filter candidates:
//      a. Exclude current expedition seed (no self-rivalry on same seed)
//      b. Prefer recent runs (weighted by recency)
//      c. Prefer runs with interesting outcomes (deaths > extractions > abandonments)
//      d. Cap at 1 past-run rival per expedition (prevents clutter)
//   3. For selected past expedition:
//      a. Load ScarMapState from persistence
//      b. Extract district visit order → PastRunRouteEntry buffer
//      c. Derive equipment tier from average gear level across the run
//      d. Map PastRunEndReason from how the run ended
//      e. Generate RivalOperatorSO-equivalent data:
//         - TeamName: "Ghost of Run #N"
//         - MemberCount: 1 (solo ghost)
//         - BuildStyle: inferred from equipped limb types
//         - PreferredDistricts: districts visited in source run
//         - RiskTolerance: derived from Front exposure in source run
//         - EquipmentTier: derived from gear snapshot
//   4. Create rival entity with:
//      - RivalSimState (populated from derived data)
//      - PastRunRivalTag
//      - PastRunRivalData
//      - PastRunRouteEntry buffer (historical route)
//      - RivalOutcomeEntry buffer (empty — filled by simulation)
//   5. Register in RivalTeamEntry buffer on expedition singleton
```

### PastRunRivalSimOverrideSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/PastRunRivalSimOverrideSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: RivalSimulationSystem (EPIC 11.1)
//
// Overrides normal rival simulation for past-run rivals:
//   1. For each rival with PastRunRivalTag:
//      a. Instead of probabilistic graph traversal, follow PastRunRouteEntry sequence
//      b. On gate transition, advance to next entry in route buffer
//      c. Outcome determined by historical data:
//         - Traversed/Explored → ClearedEnemies + possible LootedPOIs
//         - FailedObjective → RivalEcho (guaranteed, not probabilistic)
//         - Died → TeamWiped at the exact district/zone from source run
//         - Extracted → Rival exits at the source extraction point
//      d. Write RivalOutcomeEntry matching historical events
//   2. Set LastSimulatedTransition on RivalSimState to prevent
//      RivalSimulationSystem from double-processing
//   3. Past-run rival bodies carry equipment matching the player's
//      historical loadout at that point (EquipmentSnapshotHash → loot table)
```

### PastRunRivalEncounterModifierSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/PastRunRivalEncounterModifierSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: RivalEncounterSystem (EPIC 11.3)
//
// Modifies encounter behavior for past-run rivals:
//   1. When RivalEncounterSystem would trigger for a PastRunRivalTag entity:
//      a. Override encounter type based on PastRunEndReason:
//         - Death: always hostile (Desperate) — ghost is reliving its final fight
//         - Extraction: neutral (Intel) — ghost shares "what it learned"
//         - Abandonment: competitive (Race) — ghost is trying to do better this time
//      b. Override dialogue tree to past-run-specific dialogue:
//         - References the source run number and how it ended
//         - "I died here last time. I won't make that mistake again."
//      c. Override visual: ghost shader effect on rival NPC model
//   2. Past-run rival combat uses player's historical build:
//      a. Limbs from EquipmentSnapshotHash at encounter point
//      b. Abilities matching what player had unlocked
//      c. AI behavior tuned to mimic player's historical risk pattern
```

---

## Setup Guide

1. **Add PastRunRivalComponents.cs** to `Assets/Scripts/Rivals/Components/`
2. **Ensure RunHistorySaveModule** (Roguelite/ framework) stores:
   - Expedition ID, run number, district visit sequence
   - Equipment snapshots at each gate transition
   - Run end reason and final district
   - ScarMapState reference (EPIC 12.1 persistence)
3. **Create ghost dialogue trees**: `Assets/Data/Dialogue/Rivals/PastRunGhost_Death.asset`, `PastRunGhost_Extract.asset`, `PastRunGhost_Abandon.asset`
4. **Create ghost visual material**: `Assets/Materials/Rivals/GhostShader.mat` — translucent, chromatic aberration effect indicating past-run origin
5. **Add ghost NPC prefab variant**: `Assets/Prefabs/Rivals/RivalNPC_Ghost.prefab` — uses ghost shader, single-member team
6. **Wire persistence**: PastRunRivalSystem reads from `RunHistorySaveModule.GetCompletedExpeditions()`
7. **Configure selection weights** in PastRunRivalConfig authoring:
   - RecencyWeight: 0.6 (prefer recent runs)
   - DeathRunWeight: 0.4 (prefer runs that ended in death)
   - MinRunsBeforeGhosts: 3 (don't show ghosts until player has enough history)
8. **Add assembly references** to `Hollowcore.Persistence`, `Hollowcore.Roguelite`

---

## Verification

- [ ] No past-run rivals spawn on first 2 expeditions (MinRunsBeforeGhosts)
- [ ] Past-run rival correctly selected from expedition history (recency + interest weighted)
- [ ] PastRunRouteEntry buffer accurately reflects source expedition's district visit order
- [ ] Past-run rival follows historical route instead of probabilistic traversal
- [ ] Ghost dies at same district as source run (Death end reason)
- [ ] Ghost extracts at same district as source run (Extraction end reason)
- [ ] Failed objectives from source run generate guaranteed rival echoes
- [ ] Ghost bodies carry equipment matching player's historical loadout at that point
- [ ] DerivedEquipmentTier correctly computed from gear snapshots
- [ ] Encounter type override correct: Death→Desperate, Extraction→Intel, Abandonment→Race
- [ ] Ghost-specific dialogue references run number and end reason
- [ ] Ghost visual shader applied to past-run rival NPC models
- [ ] Only 1 past-run rival per expedition (cap enforced)
- [ ] Same expedition seed excluded from ghost candidate pool
- [ ] RivalSimulationSystem does not double-process past-run rivals

---

## Validation

```csharp
// File: Assets/Scripts/Rivals/Components/PastRunRivalComponents.cs (append validation)
// Add to PastRunRivalConfigAuthoring MonoBehaviour:

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (MinRunsBeforeGhosts < 1)
            Debug.LogError($"[PastRunRival] MinRunsBeforeGhosts must be >= 1.", this);
        if (RecencyWeight < 0f || RecencyWeight > 1f)
            Debug.LogError($"[PastRunRival] RecencyWeight must be 0-1.", this);
        if (DeathRunWeight < 0f || DeathRunWeight > 1f)
            Debug.LogError($"[PastRunRival] DeathRunWeight must be 0-1.", this);
    }
#endif
```

```csharp
// File: Assets/Editor/Rivals/PastRunRivalBuildValidator.cs
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hollowcore.Rivals.Editor
{
    public class PastRunRivalBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 12;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Validate ghost dialogue trees exist
            var dialogueIds = new[] { "PastRunGhost_Death", "PastRunGhost_Extract", "PastRunGhost_Abandon" };
            foreach (var id in dialogueIds)
            {
                var path = $"Assets/Data/Dialogue/Rivals/{id}.asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                    Debug.LogWarning($"[PastRunRivalBuildValidation] Missing ghost dialogue: {path}");
            }

            // Validate ghost NPC prefab exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rivals/RivalNPC_Ghost.prefab") == null)
                Debug.LogError("[PastRunRivalBuildValidation] Missing RivalNPC_Ghost.prefab");

            // Validate ghost shader material exists
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rivals/GhostShader.mat") == null)
                Debug.LogWarning("[PastRunRivalBuildValidation] Missing GhostShader.mat — ghost visuals will be default");

            // Equipment snapshot integrity: validate RunHistorySaveModule schema version compatibility
            Debug.Log("[PastRunRivalBuildValidation] Past-run rival validation complete.");
        }
    }
}
```

---

## Editor Tooling — Ghost Route Visualizer

```csharp
// File: Assets/Editor/RivalWorkstation/GhostRouteVisualizer.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Rivals.Editor
{
    /// <summary>
    /// Ghost Route Visualizer — shows past expedition routes on the district graph.
    /// Features:
    ///   - Load any completed expedition from RunHistorySaveModule
    ///   - Draw route as colored path on expedition graph (district nodes + edges)
    ///   - Mark death/extraction point with icon
    ///   - Show equipment snapshots at each gate transition (tooltip on hover)
    ///   - Preview derived RivalOperatorSO-equivalent stats (survival rate, risk tolerance)
    ///   - "Preview as Ghost" button: show what trail markers this ghost would generate
    ///   - Side-by-side comparison: current expedition graph + ghost route overlay
    ///
    /// Integrated into Rival Designer as "Ghost" tab.
    /// </summary>
    public class GhostRouteVisualizer : EditorWindow
    {
        [MenuItem("Hollowcore/Ghost Route Visualizer")]
        public static void Open() => GetWindow<GhostRouteVisualizer>("Ghost Routes");
    }
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Rivals/Debug/PastRunRivalLiveTuning.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Runtime-tunable ghost rival parameters:
    ///   - RecencyWeight (float, 0-1): preference for recent runs
    ///   - DeathRunWeight (float, 0-1): preference for death runs over extractions
    ///   - MinRunsBeforeGhosts (int): minimum expedition count before ghosts appear
    ///   - Force-spawn specific ghost: /debug ghost spawn [expeditionId]
    ///   - Disable ghosts: /debug ghost disable
    ///
    /// Changes apply on next expedition start.
    /// </summary>
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Rivals/Debug/PastRunRivalDebugOverlay.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Debug overlay for past-run ghost rivals (development builds):
    /// - Ghost route overlay on Scar Map: translucent path showing historical route
    ///   with district-by-district outcome icons
    /// - Equipment snapshot popup: at each route waypoint, show what gear the ghost carries
    /// - Death/extraction marker: large icon at route terminus
    /// - Ghost encounter preview: when player enters ghost's current district,
    ///   show encounter type and probability
    /// - Source run metadata: run number, total districts, end reason, derived stats
    /// - Toggle: /debug rivals ghost
    /// </summary>
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/RivalWorkstation/GhostQualityTester.cs
namespace Hollowcore.Rivals.Editor
{
    /// <summary>
    /// Ghost encounter quality metrics (integrated into Rival Designer):
    ///
    /// Given a set of sample expedition histories (real or synthetic):
    ///   - Ghost selection diversity: across 100 expeditions, how many unique source runs
    ///     are selected as ghosts. Target: high diversity (avoid same ghost repeatedly)
    ///   - Route overlap analysis: percentage of districts where ghost route overlaps
    ///     with current expedition. Too high = predictable, too low = irrelevant
    ///   - Equipment tier fairness: distribution of ghost equipment tier vs expected
    ///     player tier at encounter point. Warn if ghost consistently outgears player
    ///   - Encounter quality score: composite of (meaningful interaction + appropriate
    ///     difficulty + narrative resonance). Heuristic scoring based on end reason
    ///     match, route overlap, and tier delta
    ///   - Ghost body loot value: average value of ghost body loot vs district baseline.
    ///     Ghost bodies should be slightly better than normal rival bodies (personal stakes)
    /// </summary>
}
```
