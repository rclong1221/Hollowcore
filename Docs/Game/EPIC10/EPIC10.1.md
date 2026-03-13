# EPIC 10.1: Reward Category Definitions

**Status**: Planning
**Epic**: EPIC 10 — Reward Economy
**Priority**: High — Foundation for all reward systems
**Dependencies**: Framework: Rewards/ (RewardDefinitionSO), Items/, Loot/; EPIC 1 (Chassis/LimbSalvage), EPIC 9 (CompendiumPage)

---

## Overview

Seven distinct reward categories define the economy of Hollowcore. Each category serves a different strategic purpose and creates the "always short of something" tension the game's greed-vs-survival loop depends on. This sub-epic defines the category enum, per-category gameplay roles, and the RewardCategoryDefinitionSO that provides display metadata and balancing hooks. All downstream systems (distribution, presentation, loot tables) reference these definitions.

---

## Component Definitions

### RewardCategory Enum

```csharp
// File: Assets/Scripts/Rewards/Components/RewardCategoryComponents.cs
namespace Hollowcore.Rewards
{
    /// <summary>
    /// The seven reward categories in Hollowcore's economy.
    /// Each serves a distinct strategic purpose — players can never satisfy all needs simultaneously.
    /// </summary>
    public enum RewardCategory : byte
    {
        /// <summary>
        /// Infiltration / social tools. Toll skips, disguise checks, identity gate passes.
        /// Primary in: Mirrortown, Chrome Cathedral, The Auction.
        /// </summary>
        Face = 0,

        /// <summary>
        /// Tempo / combat consumables. Reflex loops, dream hacks, borrowed skills.
        /// Temporary combat buffs and skill unlocks for the current district.
        /// </summary>
        Memory = 1,

        /// <summary>
        /// Movement / weapon equipment. Grapples, sonar lungs, heat shielding, analog kit.
        /// Persistent equipment upgrades that change traversal and combat options.
        /// </summary>
        Augment = 2,

        /// <summary>
        /// Tactical run modifiers (EPIC 9). Scout, Suppression, Insight.
        /// Single-use pages consumed from the compendium inventory.
        /// </summary>
        CompendiumPage = 3,

        /// <summary>
        /// District-specific currencies. Secrets, memories, contracts, organs.
        /// Spent at local vendors; converts poorly to universal currency.
        /// </summary>
        Currency = 4,

        /// <summary>
        /// Boss mechanic counters. "Warden Override Key," "Hymn Scrambler," etc.
        /// Found as side goal rewards in thematically linked districts.
        /// Disable specific boss mechanics when used (EPIC 14).
        /// </summary>
        BossCounterToken = 5,

        /// <summary>
        /// District-specific prosthetics with memory bonuses (EPIC 1).
        /// The most Hollowcore-specific reward type — modular body parts.
        /// </summary>
        LimbSalvage = 6
    }
}
```

### RewardCategoryTag (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardCategoryComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Tags a reward entity with its category. Used by distribution, presentation,
    /// and loot filter systems to route rewards to the correct pipeline.
    /// </summary>
    public struct RewardCategoryTag : IComponentData
    {
        public RewardCategory Category;
    }
}
```

### RewardRarity Enum

```csharp
// File: Assets/Scripts/Rewards/Components/RewardCategoryComponents.cs
namespace Hollowcore.Rewards
{
    /// <summary>
    /// Rarity tiers shared across all reward categories.
    /// Drives visual language (color, VFX) and loot table weighting.
    /// </summary>
    public enum RewardRarity : byte
    {
        Common = 0,      // White   — baseline drops
        Uncommon = 1,    // Green   — minor stat boosts or better effects
        Rare = 2,        // Blue    — significant upgrades
        Epic = 3,        // Purple  — build-defining items
        Legendary = 4    // Gold    — boss exclusives, expedition-changing
    }
}
```

### RewardRarityTag (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardCategoryComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Tags a reward entity with its rarity tier.
    /// </summary>
    public struct RewardRarityTag : IComponentData
    {
        public RewardRarity Rarity;
    }
}
```

---

## ScriptableObject Definitions

### RewardCategoryDefinitionSO

```csharp
// File: Assets/Scripts/Rewards/Definitions/RewardCategoryDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Rewards.Definitions
{
    [CreateAssetMenu(fileName = "NewRewardCategory", menuName = "Hollowcore/Rewards/Category Definition")]
    public class RewardCategoryDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public RewardCategory Category;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public Color CategoryColor;

        [Header("Gameplay Context")]
        [TextArea(2, 5)] public string PlayerFacingRole;
        [Tooltip("District IDs where this category is primary reward focus")]
        public int[] PrimaryDistricts;

        [Header("Scarcity Design")]
        [Tooltip("Relative weight in general loot tables (lower = rarer overall)")]
        [Range(0f, 1f)] public float BaseDropWeight = 0.5f;
        [Tooltip("Whether this category can appear in random event rewards")]
        public bool AvailableInRandomEvents = true;
        [Tooltip("Whether this category appears in gate reward tags")]
        public bool ShowOnGateCards = true;

        [Header("Inventory")]
        [Tooltip("Max items of this category the player can carry (-1 = unlimited)")]
        public int CarryLimit = -1;
        [Tooltip("Whether items in this category persist between districts")]
        public bool PersistsBetweenDistricts = true;

        [Header("Visual")]
        [Tooltip("Rarity color overrides (indexed by RewardRarity)")]
        public Color[] RarityColors = new Color[5];
        [Tooltip("Category-specific pickup VFX prefab")]
        public GameObject PickupVFXPrefab;
        [Tooltip("Category-specific pickup SFX key")]
        public string PickupSFXKey;
    }
}
```

---

## Per-Category Gameplay Roles

### Face (Infiltration / Social)

- **Usage**: consumed at social encounters, identity checks, toll gates
- **Districts**: Mirrortown, Chrome Cathedral, The Auction
- **Scarcity**: moderate drop rate, high consumption in social districts
- **Carry limit**: 10 (encourages spending before moving on)
- **Persistence**: within expedition only — lost on wipe

### Memory (Tempo / Combat)

- **Usage**: activate for temporary combat buffs — reflex boost, borrowed enemy skills, dream hacks
- **Districts**: any combat-heavy district, but strongest variants from Necrospire, Signal Graveyard
- **Scarcity**: common drops, but powerful memories are rare
- **Carry limit**: 5 active at once (slot-based like pages)
- **Persistence**: current district only — consumed or lost on exit

### Augment (Movement / Weapon)

- **Usage**: equip for persistent upgrades — grapple hook, sonar lungs, heat shielding
- **Districts**: Lattice (movement), Wetmarket (bio-augments), Forge Quarter (weapon)
- **Scarcity**: rare drops, primary from side goals and echo rewards
- **Carry limit**: unlimited (equipped on chassis or in inventory)
- **Persistence**: persists across districts within expedition

### CompendiumPage

- **Usage**: consumed from compendium inventory (EPIC 9.1)
- **Districts**: any — found as exploration and quest rewards
- **Scarcity**: uncommon, targeted page types are rare
- **Carry limit**: governed by CompendiumPageConfig (6 active, 10 total)
- **Persistence**: within expedition

### Currency

- **Usage**: spent at district vendors for goods and services
- **Districts**: every district has its own currency (see EPIC 10.2)
- **Scarcity**: earned freely within district, converts poorly cross-district
- **Carry limit**: per-currency cap (encourages local spending)
- **Persistence**: within expedition (universal currency persists, district currency converts at loss)

### BossCounterToken

- **Usage**: consumed at boss encounter to disable a specific mechanic
- **Districts**: found in thematically linked districts (e.g., Hymn Scrambler in Signal Graveyard)
- **Scarcity**: one per side goal completion in the correct district, never random drops
- **Carry limit**: 3 per token type
- **Persistence**: within expedition

### LimbSalvage

- **Usage**: equip to chassis slot (EPIC 1)
- **Districts**: all districts drop district-specific limbs with affinity bonuses
- **Scarcity**: common limbs from enemies, rare/epic from bosses and echoes
- **Carry limit**: governed by chassis inventory (EPIC 1)
- **Persistence**: permanent (Permanent durability) or time-limited (Temporary durability)

---

## Setup Guide

1. **Create `Assets/Scripts/Rewards/` folder** with subfolders: Components/, Definitions/
2. **Create assembly definition** `Hollowcore.Rewards.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.Collections`
3. Create 7 RewardCategoryDefinitionSO assets in `Assets/Data/Rewards/Categories/` — one per RewardCategory enum value
4. Configure each SO with appropriate icon, color, drop weight, carry limit, and primary districts
5. Set RarityColors array on each SO: `[White, Green, Blue, Purple, Gold]`
6. Create pickup VFX prefabs per category (can share a template with tint override initially)
7. Reference RewardCategoryDefinitionSO database from loot table system and gate card system

---

## Verification

- [ ] RewardCategory enum has 7 values matching GDD reward types
- [ ] RewardRarity enum has 5 tiers with correct naming
- [ ] RewardCategoryTag and RewardRarityTag are lightweight IComponentData (no ghost fields needed — server-only)
- [ ] All 7 RewardCategoryDefinitionSO assets created with distinct icons and colors
- [ ] PrimaryDistricts arrays populated correctly for Face, Memory, Augment categories
- [ ] CarryLimit values set: Face=10, Memory=5, Currency=per-type, BossCounterToken=3, others=-1
- [ ] PersistsBetweenDistricts correct: Memory=false, all others=true
- [ ] BaseDropWeight tuned: BossCounterToken=0 (never random), others=0.1-0.8 range
- [ ] RarityColors consistent across all categories (same 5-color language)
- [ ] Framework Rewards/RewardDefinitionSO can reference RewardCategory via extension or wrapper

---

## CRITICAL: Enum Rename — RewardCategory → RewardCategoryType

> **Collision**: EPIC 13 defines `RewardCategory` in `Hollowcore.District` (Limbs, Currency, Weapons, Chassis, Recipes, Augments, Intel) for district-level reward theming. EPIC 10's enum serves the **player-facing economy** (what the player collects, carries, spends). To avoid ambiguity, EPIC 10's enum is renamed to `RewardCategoryType` in the `Hollowcore.Economy` namespace. All EPIC 10 components, SOs, and systems reference `RewardCategoryType`. A `RewardCategoryMapping` utility bridges the two enums where cross-epic queries are needed (e.g., district reward focus → economy category).

```csharp
// File: Assets/Scripts/Economy/Components/RewardCategoryType.cs
namespace Hollowcore.Economy
{
    /// <summary>
    /// The seven reward categories in Hollowcore's player-facing economy.
    /// Renamed from RewardCategory to avoid collision with Hollowcore.District.RewardCategory (EPIC 13).
    /// </summary>
    public enum RewardCategoryType : byte
    {
        Face = 0,
        Memory = 1,
        Augment = 2,
        CompendiumPage = 3,
        Currency = 4,
        BossCounterToken = 5,
        LimbSalvage = 6
    }
}
```

```csharp
// File: Assets/Scripts/Economy/Utilities/RewardCategoryMapping.cs
namespace Hollowcore.Economy
{
    /// <summary>
    /// Maps between Hollowcore.Economy.RewardCategoryType (EPIC 10, player economy)
    /// and Hollowcore.District.RewardCategory (EPIC 13, district reward theming).
    /// Not all values map 1:1 — some economy categories have no district equivalent and vice versa.
    /// </summary>
    public static class RewardCategoryMapping
    {
        public static RewardCategoryType? FromDistrictCategory(District.RewardCategory dc)
        {
            return dc switch
            {
                District.RewardCategory.Limbs => RewardCategoryType.LimbSalvage,
                District.RewardCategory.Currency => RewardCategoryType.Currency,
                District.RewardCategory.Augments => RewardCategoryType.Augment,
                _ => null // Weapons, Chassis, Recipes, Intel have no direct economy equivalent
            };
        }
    }
}
```

---

## BlobAsset Pipeline

RewardCategoryDefinitionSO is read at runtime by distribution, presentation, and loot systems. Convert to blob for Burst-compatible access.

```csharp
// File: Assets/Scripts/Economy/Blobs/RewardCategoryBlob.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Economy
{
    /// <summary>
    /// Burst-compatible blob representation of RewardCategoryDefinitionSO.
    /// One blob per RewardCategoryType, stored in a BlobArray on a singleton.
    /// </summary>
    public struct RewardCategoryBlob
    {
        public RewardCategoryType Category;
        public BlobString DisplayName;
        public float BaseDropWeight;
        public int CarryLimit;               // -1 = unlimited
        public bool PersistsBetweenDistricts;
        public bool AvailableInRandomEvents;
        public bool ShowOnGateCards;
        public BlobArray<int> PrimaryDistricts;
    }

    public struct RewardCategoryDatabase
    {
        /// <summary>Indexed by (int)RewardCategoryType. Length = 7.</summary>
        public BlobArray<RewardCategoryBlob> Categories;
    }

    /// <summary>Singleton holding the blob reference. Created by RewardCategoryBaker.</summary>
    public struct RewardCategoryDatabaseRef : IComponentData
    {
        public BlobAssetReference<RewardCategoryDatabase> Value;
    }
}
```

```csharp
// File: Assets/Scripts/Economy/Authoring/RewardCategoryDatabaseAuthoring.cs
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Hollowcore.Economy.Authoring
{
    public class RewardCategoryDatabaseAuthoring : MonoBehaviour
    {
        public RewardCategoryDefinitionSO[] Categories; // Assign all 7 in inspector
    }

    public class RewardCategoryDatabaseBaker : Baker<RewardCategoryDatabaseAuthoring>
    {
        public override void Bake(RewardCategoryDatabaseAuthoring authoring)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<RewardCategoryDatabase>();
            var arr = builder.Allocate(ref root.Categories, 7);

            for (int i = 0; i < authoring.Categories.Length && i < 7; i++)
            {
                var so = authoring.Categories[i];
                arr[i].Category = (RewardCategoryType)so.Category;
                builder.AllocateString(ref arr[i].DisplayName, so.DisplayName);
                arr[i].BaseDropWeight = so.BaseDropWeight;
                arr[i].CarryLimit = so.CarryLimit;
                arr[i].PersistsBetweenDistricts = so.PersistsBetweenDistricts;
                arr[i].AvailableInRandomEvents = so.AvailableInRandomEvents;
                arr[i].ShowOnGateCards = so.ShowOnGateCards;

                var districts = builder.Allocate(ref arr[i].PrimaryDistricts, so.PrimaryDistricts.Length);
                for (int d = 0; d < so.PrimaryDistricts.Length; d++)
                    districts[d] = so.PrimaryDistricts[d];
            }

            var blobRef = builder.CreateBlobAssetReference<RewardCategoryDatabase>(Allocator.Persistent);
            builder.Dispose();

            var entity = GetEntity(TransformUsageFlags.None);
            AddBlobAsset(ref blobRef, out _);
            AddComponent(entity, new RewardCategoryDatabaseRef { Value = blobRef });
        }
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Economy/Definitions/RewardCategoryDefinitionSO.cs (append to class)
// Add inside RewardCategoryDefinitionSO class body:

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (CarryLimit == 0)
            Debug.LogWarning($"[RewardCategory] {name}: CarryLimit is 0 — players cannot carry any. Use -1 for unlimited.", this);
        if (BaseDropWeight < 0f)
            Debug.LogError($"[RewardCategory] {name}: BaseDropWeight cannot be negative.", this);
        if (string.IsNullOrEmpty(DisplayName))
            Debug.LogError($"[RewardCategory] {name}: DisplayName is empty.", this);
        if (RarityColors == null || RarityColors.Length != 5)
            Debug.LogWarning($"[RewardCategory] {name}: RarityColors should have exactly 5 entries.", this);
        if (Icon == null)
            Debug.LogWarning($"[RewardCategory] {name}: Missing Icon sprite.", this);
    }
#endif
```

```csharp
// File: Assets/Editor/Economy/RewardCategoryBuildValidator.cs
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Hollowcore.Economy.Editor
{
    /// <summary>
    /// Build-time validation: duplicate categories, weight sanity, carry limit checks.
    /// </summary>
    public class RewardCategoryBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:RewardCategoryDefinitionSO");
            var categories = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<RewardCategoryDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(so => so != null).ToList();

            // Duplicate category detection
            var seen = new HashSet<int>();
            foreach (var so in categories)
            {
                if (!seen.Add((int)so.Category))
                    Debug.LogError($"[EconomyBuildValidation] Duplicate RewardCategoryType: {so.Category} in {so.name}");
            }

            // All 7 categories must be defined
            if (categories.Count < 7)
                Debug.LogWarning($"[EconomyBuildValidation] Only {categories.Count}/7 RewardCategoryDefinitionSO assets found.");

            // Carry limit sanity
            foreach (var so in categories)
            {
                if (so.CarryLimit == 0)
                    Debug.LogError($"[EconomyBuildValidation] {so.name} has CarryLimit=0 — unreachable category.");
            }
        }
    }
}
```

---

## Editor Tooling — Economy Dashboard Workstation

The Economy Dashboard follows the DIG workstation pattern (sidebar tabs, `IWorkstationModule`). Provides sink/source flow diagrams, accumulation curve projections, and carry limit utilization charts — the same tooling Riot and Bungie use internally for economy tuning.

```csharp
// File: Assets/Editor/EconomyWorkstation/EconomyWorkstationWindow.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Economy.Editor
{
    /// <summary>
    /// Economy Dashboard — EditorWindow following DIG workstation pattern.
    /// Sidebar tabs: Overview, Sink/Source Flow, Accumulation Curves, Carry Limits, Monte Carlo Sim.
    /// </summary>
    public class EconomyWorkstationWindow : EditorWindow
    {
        [MenuItem("Hollowcore/Economy Dashboard")]
        public static void Open() => GetWindow<EconomyWorkstationWindow>("Economy Dashboard");

        private IEconomyWorkstationModule[] _modules;
        private int _selectedTab;
        private string[] _tabNames;

        private void OnEnable()
        {
            _modules = new IEconomyWorkstationModule[]
            {
                new EconomyOverviewModule(),
                new SinkSourceFlowModule(),
                new AccumulationCurveModule(),
                new CarryLimitModule(),
                new MonteCarloSimModule()
            };
            _tabNames = new[] { "Overview", "Sink/Source", "Accumulation", "Carry Limits", "Monte Carlo" };
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            // Sidebar
            EditorGUILayout.BeginVertical(GUILayout.Width(160));
            for (int i = 0; i < _tabNames.Length; i++)
                if (GUILayout.Toggle(_selectedTab == i, _tabNames[i], "Button"))
                    _selectedTab = i;
            EditorGUILayout.EndVertical();
            // Content
            EditorGUILayout.BeginVertical();
            _modules[_selectedTab]?.DrawGUI();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }

    public interface IEconomyWorkstationModule
    {
        void DrawGUI();
    }
}
```

```csharp
// File: Assets/Editor/EconomyWorkstation/SinkSourceFlowModule.cs
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Hollowcore.Economy.Editor
{
    /// <summary>
    /// Visualizes reward sources (exploration, side goals, boss, echo, enemy, events)
    /// and sinks (vendor purchase, conversion loss, district exit loss, carry limit overflow)
    /// per RewardCategoryType. Draws a flow diagram with arrow widths proportional to volume.
    /// Highlights imbalances: net positive = inflation warning, net negative = deflation warning.
    /// </summary>
    public class SinkSourceFlowModule : IEconomyWorkstationModule
    {
        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Sink / Source Flow Diagram", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a RewardCategoryType to see all sources (green, left) and sinks (red, right). "
                + "Arrow width = relative volume. Net balance shown at center.",
                MessageType.Info);
            // Implementation: load all DistrictRewardProfileSO + RewardDistributionConfig,
            // compute expected sources per district, sum sinks from conversion rates + exit loss.
            // Draw Handles.DrawAAPolyLine flow arrows in the Rect.
        }
    }
}
```

```csharp
// File: Assets/Editor/EconomyWorkstation/AccumulationCurveModule.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Economy.Editor
{
    /// <summary>
    /// Projects reward accumulation curves over N districts for each RewardCategoryType.
    /// X-axis = districts visited, Y-axis = total accumulated.
    /// Overlays carry limits as horizontal lines. Shows where players cap out.
    /// Uses DistrictRewardProfileSO weights + RewardDistributionConfig multipliers.
    /// </summary>
    public class AccumulationCurveModule : IEconomyWorkstationModule
    {
        private int _districtCount = 8;

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Accumulation Curve Projection", EditorStyles.boldLabel);
            _districtCount = EditorGUILayout.IntSlider("Districts", _districtCount, 1, 15);
            // Implementation: for each category, compute expected rewards per district
            // from DistrictRewardProfileSO weights * source multipliers * front phase scaling.
            // Plot cumulative curve. Overlay CarryLimit horizontal line.
            // Highlight district where curve hits carry limit (saturation point).
        }
    }
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Economy/Debug/RewardCategoryLiveTuning.cs
using Unity.Entities;

namespace Hollowcore.Economy.Debug
{
    /// <summary>
    /// Runtime tuning for reward categories. Modifies RewardCategoryDatabaseRef blob values
    /// via a shadow NativeArray overlay (blob is immutable — shadow overrides are checked first).
    /// Exposed via EconomyWorkstationWindow's Live Tuning tab at runtime.
    /// </summary>
    // Tunable parameters per category:
    //   - BaseDropWeight (float, 0-1)
    //   - CarryLimit (int, -1 to 999)
    //   - PersistsBetweenDistricts (bool)
    //
    // Global scarcity multiplier (float, 0.1-3.0): scales all drop weights uniformly.
    // Changes take effect next reward roll. No server restart required.
    //
    // Pattern: RewardLiveTuningSystem reads from static RewardTuningOverrides,
    // distribution systems check overrides before blob values.
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Economy/Debug/RewardFlowDebugOverlay.cs
namespace Hollowcore.Economy.Debug
{
    /// <summary>
    /// In-game debug overlay (development builds only) showing:
    /// - Floating icons at reward source points (color-coded by RewardCategoryType)
    /// - Arrow particles flowing from source → player inventory
    /// - Sink indicators at vendors, conversion points, district exits
    /// - Per-category balance bars in corner HUD: green = gaining, red = losing
    /// - Toggle via console command: /debug economy overlay
    ///
    /// Implementation: RewardFlowDebugSystem (PresentationSystemGroup, #if UNITY_EDITOR || DEVELOPMENT_BUILD)
    /// reads RewardGrantEvent + CurrencyTransaction entities, pushes to managed overlay renderer.
    /// </summary>
}
```

---

## Simulation & Testing — Monte Carlo Economy Simulation

```csharp
// File: Assets/Editor/EconomyWorkstation/MonteCarloSimModule.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Hollowcore.Economy.Editor
{
    /// <summary>
    /// Monte Carlo economy simulation — runs N expeditions offline and plots results.
    ///
    /// Configuration:
    ///   - Expedition count (default 1000)
    ///   - Districts per expedition (default 5-8, randomized)
    ///   - District pool (all 15 or subset)
    ///   - Source frequency per district (from DistrictRewardProfileSO + distribution config)
    ///
    /// Per expedition, simulates:
    ///   1. District selection sequence (random from pool)
    ///   2. Per district: roll rewards from weighted category distribution
    ///      - Apply source type multipliers (exploration, side goal, boss, etc.)
    ///      - Apply front phase scaling
    ///   3. Apply sinks: carry limit overflow → currency conversion, district exit loss
    ///   4. Track per-category accumulation at each step
    ///
    /// Output plots:
    ///   - Accumulation curves per category (mean + std dev bands)
    ///   - Inflation/deflation detection: categories trending unbounded vs capped
    ///   - Breakpoint analysis: district count where 50%/90% of players cap out
    ///   - Expected value table: mean reward per district per category
    ///   - Sink-source equilibrium: net flow per category at steady state
    ///   - Histogram: reward distribution at expedition end (shows spread)
    ///
    /// Export: CSV for external analysis, clipboard-friendly summary table.
    /// </summary>
    public class MonteCarloSimModule : IEconomyWorkstationModule
    {
        private int _expeditionCount = 1000;
        private int _minDistricts = 5;
        private int _maxDistricts = 8;
        private bool _isRunning;

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("Monte Carlo Economy Simulation", EditorStyles.boldLabel);
            _expeditionCount = EditorGUILayout.IntField("Expeditions", _expeditionCount);
            _minDistricts = EditorGUILayout.IntSlider("Min Districts", _minDistricts, 1, 15);
            _maxDistricts = EditorGUILayout.IntSlider("Max Districts", _maxDistricts, _minDistricts, 15);

            EditorGUI.BeginDisabledGroup(_isRunning);
            if (GUILayout.Button("Run Simulation"))
                RunSimulation();
            EditorGUI.EndDisabledGroup();

            // Results area: AnimationCurve fields for each category, summary stats
        }

        private void RunSimulation()
        {
            // Load all DistrictRewardProfileSO + RewardDistributionConfig + RewardCategoryDefinitionSO
            // For each expedition: random district sequence, per-district reward rolls,
            // sink application, accumulate totals.
            // Aggregate across expeditions: mean, stddev, percentiles.
            // Populate EditorWindow curves + summary table.
        }
    }
}
```
