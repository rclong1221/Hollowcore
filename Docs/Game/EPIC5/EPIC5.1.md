# EPIC 5.1: Echo Generation

**Status**: Planning
**Epic**: EPIC 5 — Echo Missions
**Dependencies**: EPIC 4 (district exit triggers); Framework: Quest/

---

## Overview

When a player leaves a district with uncompleted side goals, each skipped goal mutates into an Echo Mission in that same district. Echoes are generated deterministically from the original quest definition + district echo flavor + expedition seed. They're stored in district persistence and await the player's return.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Echo/Components/EchoComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace Hollowcore.Echo
{
    public enum EchoMutationType : byte
    {
        EnemyUpgrade = 0,       // Harder enemy variants replace originals
        MechanicChange = 1,     // Objective type changes (rescue→escort, kill→survive)
        LayoutDistortion = 2,   // Zone paths altered, new hazards
        FactionSwap = 3,        // Different enemy faction takes over the encounter
        TemporalAnomaly = 4     // Enemies reset on death once, time-loop effects
    }

    /// <summary>
    /// An active echo mission in a district. Stored on district entity or as buffer.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct EchoMissionEntry : IBufferElementData
    {
        /// <summary>Unique echo ID (derived from source quest + seed).</summary>
        public int EchoId;
        /// <summary>Original quest that was skipped.</summary>
        public int SourceQuestId;
        /// <summary>Zone where the echo manifests.</summary>
        public int ZoneId;
        /// <summary>How this echo mutated.</summary>
        public EchoMutationType MutationType;
        /// <summary>Difficulty multiplier over original (1.5-2.0 base).</summary>
        public float DifficultyMultiplier;
        /// <summary>Reward multiplier over original (2.0-3.0 base).</summary>
        public float RewardMultiplier;
        /// <summary>How many expeditions this echo has persisted (0 = same run).</summary>
        public int ExpeditionsPersisted;
        /// <summary>Whether this echo has been completed.</summary>
        public bool IsCompleted;
    }

    /// <summary>
    /// Tag on district entity indicating it has active echoes.
    /// Enableable — toggled when echoes are generated or all completed.
    /// </summary>
    public struct HasActiveEchoes : IComponentData, IEnableableComponent { }
}
```

---

## ScriptableObject Definitions

```csharp
// File: Assets/Scripts/Echo/Definitions/EchoFlavorSO.cs
using UnityEngine;
using System.Collections.Generic;

namespace Hollowcore.Echo.Definitions
{
    /// <summary>
    /// Per-district echo flavor. Defines how echoes mutate thematically.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEchoFlavor", menuName = "Hollowcore/Echo/Echo Flavor")]
    public class EchoFlavorSO : ScriptableObject
    {
        [Header("Identity")]
        public int DistrictId;
        public string ThemeName;           // e.g., "Rotting memories"
        [TextArea] public string ThemeDescription; // e.g., "identity drift debuffs; pristine intel rewards"

        [Header("Mutation Weights")]
        [Tooltip("Weighted probability of each mutation type for this district")]
        public List<MutationWeight> MutationWeights;

        [Header("Visual/Audio")]
        [Tooltip("Post-process profile applied in echo zones")]
        public string EchoPostProcessProfile;
        [Tooltip("Ambient audio loop for echo zones")]
        public string EchoAmbientAudio;
        [Tooltip("Enemy visual modifier (shader variant, color shift)")]
        public string EchoEnemyVisualOverride;
    }

    [System.Serializable]
    public struct MutationWeight
    {
        public EchoMutationType Type;
        [Range(0f, 1f)] public float Weight;
    }
}
```

---

## Systems

### EchoGenerationSystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoGenerationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Triggered on district exit (gate transition).
//
// On district exit event:
//   1. Query all QuestDefinitionSOs for the district
//   2. For each quest that is NOT completed AND NOT the main chain:
//      a. Generate echo from quest:
//         - EchoId = hash(SourceQuestId, ExpeditionSeed, DistrictId)
//         - Select MutationType from district's EchoFlavorSO weights (seed-deterministic)
//         - DifficultyMultiplier = 1.5 + (0.25 * ExpeditionsPersisted)
//         - RewardMultiplier = 2.0 + (0.5 * ExpeditionsPersisted)
//         - ZoneId = original quest zone (echo spawns where the quest was)
//      b. Add EchoMissionEntry to district entity's buffer
//   3. Enable HasActiveEchoes on district entity
//   4. Write echo markers to district persistence (for Scar Map)
//   5. Log echo generation for analytics
```

### EchoActivationSystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoActivationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// When player enters a district with active echoes:
//   1. Read EchoMissionEntry buffer from district entity
//   2. For each non-completed echo:
//      a. Spawn echo encounter in its zone:
//         - Apply MutationType modifications to encounter template
//         - Scale enemy stats by DifficultyMultiplier
//         - Apply district's EchoFlavorSO visual/audio overrides to zone
//      b. Register as active quest in Quest/ system (echo variant)
//   3. Echo zones have visible "wrongness" markers on approach
```

---

## Setup Guide

1. Create `Assets/Scripts/Echo/` folder with Components/, Definitions/, Systems/ subfolders
2. Create `Hollowcore.Echo.asmdef` referencing DIG.Shared, Hollowcore.Chassis
3. Create EchoFlavorSO assets per district in `Assets/Data/Echo/Flavors/`
4. Hook EchoGenerationSystem to district exit event (EPIC 4.3 transition flow)
5. Each QuestDefinitionSO needs an `EchoTemplate` field pointing to echo override data
6. Configure mutation weights per district (Necrospire: heavy on TemporalAnomaly, Burn: heavy on EnemyUpgrade, etc.)

---

## Verification

- [ ] Leaving district with 3 uncompleted side goals generates 3 echoes
- [ ] Main chain quest does NOT generate an echo
- [ ] Echo mutation type matches district's weighted probabilities
- [ ] Echo difficulty scales with ExpeditionsPersisted
- [ ] Re-entering district: echoes active in correct zones
- [ ] HasActiveEchoes correctly enabled/disabled
- [ ] Echo generation is seed-deterministic (same seed = same echoes)

---

## BlobAsset Pipeline

EchoFlavorSO is read at runtime during echo generation. Converting mutation weights to a BlobAsset allows Burst-compatible seed-weighted selection without managed allocations.

```csharp
// File: Assets/Scripts/Echo/Blob/EchoFlavorBlob.cs
using Unity.Entities;

namespace Hollowcore.Echo.Blob
{
    public struct EchoFlavorBlob
    {
        public int DistrictId;
        public BlobString ThemeName;
        public BlobString ThemeDescription;
        /// <summary>Parallel arrays: MutationType + Weight. Sorted by weight descending for fast CDF lookup.</summary>
        public BlobArray<byte> MutationTypes;
        public BlobArray<float> MutationWeights;
        /// <summary>Precomputed prefix sum of weights for O(1) weighted random selection.</summary>
        public BlobArray<float> MutationCDF;
        public BlobString PostProcessProfile;
        public BlobString AmbientAudio;
        public BlobString EnemyVisualOverride;
    }
}
```

```csharp
// File: Assets/Scripts/Echo/Definitions/EchoFlavorSO.cs (append)
namespace Hollowcore.Echo.Definitions
{
    public partial class EchoFlavorSO
    {
        public BlobAssetReference<Blob.EchoFlavorBlob> BakeToBlob(BlobBuilder builder)
        {
            ref var root = ref builder.ConstructRoot<Blob.EchoFlavorBlob>();
            root.DistrictId = DistrictId;
            builder.AllocateString(ref root.ThemeName, ThemeName ?? "");
            builder.AllocateString(ref root.ThemeDescription, ThemeDescription ?? "");
            builder.AllocateString(ref root.PostProcessProfile, EchoPostProcessProfile ?? "");
            builder.AllocateString(ref root.AmbientAudio, EchoAmbientAudio ?? "");
            builder.AllocateString(ref root.EnemyVisualOverride, EchoEnemyVisualOverride ?? "");

            int count = MutationWeights?.Count ?? 0;
            var types = builder.Allocate(ref root.MutationTypes, count);
            var weights = builder.Allocate(ref root.MutationWeights, count);
            var cdf = builder.Allocate(ref root.MutationCDF, count);
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                types[i] = (byte)MutationWeights[i].Type;
                weights[i] = MutationWeights[i].Weight;
                sum += MutationWeights[i].Weight;
                cdf[i] = sum;
            }
            // Normalize CDF
            if (sum > 0f)
                for (int i = 0; i < count; i++) cdf[i] /= sum;

            return builder.CreateBlobAssetReference<Blob.EchoFlavorBlob>(Allocator.Persistent);
        }
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Echo/Definitions/EchoFlavorSO.cs (OnValidate)
namespace Hollowcore.Echo.Definitions
{
    public partial class EchoFlavorSO
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Must have at least one mutation weight
            if (MutationWeights == null || MutationWeights.Count == 0)
                Debug.LogError($"[EchoFlavor '{ThemeName}'] No mutation weights defined", this);

            // Weights must sum > 0
            float sum = 0f;
            foreach (var mw in MutationWeights) sum += mw.Weight;
            if (sum <= 0f)
                Debug.LogError($"[EchoFlavor '{ThemeName}'] Mutation weights sum to {sum} — must be > 0", this);

            // No duplicate mutation types
            var seen = new HashSet<EchoMutationType>();
            foreach (var mw in MutationWeights)
                if (!seen.Add(mw.Type))
                    Debug.LogWarning($"[EchoFlavor '{ThemeName}'] Duplicate mutation type: {mw.Type}", this);

            // Mutation type coverage: warn if any EchoMutationType is missing
            foreach (EchoMutationType mt in System.Enum.GetValues(typeof(EchoMutationType)))
                if (!seen.Contains(mt))
                    Debug.LogWarning($"[EchoFlavor '{ThemeName}'] Missing mutation type: {mt} (weight=0 is fine, absence may be intentional)", this);

            // DistrictId must be positive
            if (DistrictId <= 0)
                Debug.LogError($"[EchoFlavor '{ThemeName}'] Invalid DistrictId={DistrictId}", this);
        }
#endif
    }
}
```

```csharp
// File: Assets/Editor/Validation/EchoFlavorBuildValidator.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;

namespace Hollowcore.Editor.Validation
{
    public class EchoFlavorBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Verify every DistrictDefinitionSO has a matching EchoFlavorSO
            var districtGuids = AssetDatabase.FindAssets("t:DistrictDefinitionSO");
            var flavorGuids = AssetDatabase.FindAssets("t:EchoFlavorSO");

            var flavorDistrictIds = new HashSet<int>();
            foreach (var guid in flavorGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var flavor = AssetDatabase.LoadAssetAtPath<Hollowcore.Echo.Definitions.EchoFlavorSO>(path);
                if (flavor != null) flavorDistrictIds.Add(flavor.DistrictId);
            }

            foreach (var guid in districtGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var district = AssetDatabase.LoadAssetAtPath<Hollowcore.Expedition.Definitions.DistrictDefinitionSO>(path);
                if (district != null && !flavorDistrictIds.Contains(district.DistrictId))
                    Debug.LogWarning($"[EchoBuildValidator] District '{district.DisplayName}' (id={district.DistrictId}) has no EchoFlavorSO");
            }
        }
    }
}
#endif
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/EchoWorkstation/EchoConfigPanel.cs
// Echo configuration panel in the Expedition Workstation (or standalone window).
//
// Features:
//   - EchoFlavorSO selector: dropdown for all district flavors
//   - Mutation type weight editor: slider per type with live-updating pie chart
//     - Pie chart shows weighted probability of each mutation type
//     - Hover slice: shows exact % and mutation description
//   - "Normalize Weights" button: scales all weights to sum to 1.0
//   - Preview panel: given a sample quest, shows what the echo would look like
//     - Mutation type, difficulty multiplier, reward multiplier
//     - Simulated echo for each mutation type side-by-side
//   - District coverage matrix: table showing which districts have flavors,
//     which mutation types are represented per district, gaps highlighted in red
//
// Seed-based echo preview:
//   - Enter expedition seed + district ID
//   - Shows which quests generate echoes, mutation type per echo, zone assignments
//   - "Regenerate" with different seed for comparison

// File: Assets/Editor/EchoWorkstation/MutationTypePreview.cs
// For each mutation type, shows:
//   - Description of what changes
//   - Example enemy replacements (from EchoEncounterDefinitionSO)
//   - Objective mutation (original → echo)
//   - Visual preview: before/after screenshots from configured echo post-process
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Echo/Components/EchoRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Echo
{
    /// <summary>
    /// Runtime-tunable echo generation parameters.
    /// </summary>
    public struct EchoRuntimeConfig : IComponentData
    {
        /// <summary>Base difficulty multiplier for new echoes. Default 1.5.</summary>
        public float BaseDifficultyMultiplier;

        /// <summary>Difficulty increment per expedition persisted. Default 0.25.</summary>
        public float DifficultyPerPersistence;

        /// <summary>Base reward multiplier. Default 2.0.</summary>
        public float BaseRewardMultiplier;

        /// <summary>Reward increment per expedition persisted. Default 0.5.</summary>
        public float RewardPerPersistence;

        /// <summary>If true, generate echoes for main chain quests too (debug). Default false.</summary>
        public bool DebugEchoMainChain;

        /// <summary>Maximum echoes per district. Default 8.</summary>
        public int MaxEchoesPerDistrict;

        public static EchoRuntimeConfig Default => new()
        {
            BaseDifficultyMultiplier = 1.5f,
            DifficultyPerPersistence = 0.25f,
            BaseRewardMultiplier = 2.0f,
            RewardPerPersistence = 0.5f,
            DebugEchoMainChain = false,
            MaxEchoesPerDistrict = 8,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Echo/Debug/EchoDebugOverlay.cs
// In-game overlay for echo state:
//
//   - Echo zone markers on minimap: spiral icons colored by mutation type
//     - EnemyUpgrade=red, MechanicChange=blue, LayoutDistortion=purple,
//       FactionSwap=orange, TemporalAnomaly=cyan
//   - Proximity radius rings: 30m (audio) and 15m (visual) shown as dashed circles on minimap
//   - Per-echo tooltip: EchoId, source quest, mutation type, difficulty, reward preview
//   - Wrongness intensity gradient: screen-space shader that intensifies as player approaches echo zone
//     (debug mode shows the raw intensity value as a number overlay)
//   - Echo generation log: lists echoes generated on last district exit with full details
//   - Toggle: F7 key or debug menu
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/EchoWorkstation/EchoGenerationSimulator.cs
// IExpeditionWorkstationModule — "Echo Simulation" tab.
//
// "Echo Distribution Monte Carlo" (1000 district exits):
//   Input: EchoFlavorSO, number of side quests (3-8), completion rate (0-100%)
//   For each simulation:
//     - Random seed, random quest completion (based on rate)
//     - Generate echoes for uncompleted quests
//     - Record: echo count, mutation type distribution, difficulty range
//   Output:
//     - Average echoes per district exit
//     - Mutation type distribution pie chart
//     - Difficulty histogram
//     - "If players complete 60% of side quests, expect ~2.4 echoes per district"
//
// "Echo Persistence Tier Distribution" (100 expedition simulations):
//   Simulate 100 expeditions × 6 districts each:
//     - Each expedition: random completion of 50-80% side goals
//     - Unresolved echoes persist, ExpeditionsPersisted increments
//   Output:
//     - Tier distribution over time: % Normal / Persistent / Legendary / Mythic
//     - "After 5 expeditions, expect ~2 Legendary echoes across visited districts"
//     - Time-to-legendary histogram
```
