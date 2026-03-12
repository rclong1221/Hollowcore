# EPIC 23.7: Advanced Editor Tooling — Content Pipeline, Live Tuning & Balance Intelligence

**Status:** IMPLEMENTED
**Priority:** High (designer velocity — transforms weeks of manual playtesting into minutes of data-driven iteration)
**Dependencies:**
- EPIC 23.1 (RunState, RunPhase, RunConfigSO)
- EPIC 23.2 (MetaBank, MetaUnlockTreeSO)
- EPIC 23.3 (ZoneDefinitionSO, EncounterPoolSO, SpawnDirectorConfigSO, ZoneSequenceSO)
- EPIC 23.4 (RunModifierPoolSO, AscensionDefinitionSO, RuntimeDifficultyScale)
- EPIC 23.5 (RewardPoolSO, RewardDefinitionSO, RunEventDefinitionSO)
- EPIC 23.6 (RunSimulator, MonteCarloSimulator, RunWorkstation modules, RunStatistics)
- Existing Workstation pattern (`Assets/Editor/CombatWorkstation/`)
- Existing `EditorWindowUtilities` (`Assets/Editor/Utilities/EditorWindowUtilities.cs`)

**Feature:** A professional-grade content pipeline and balance intelligence suite for rogue-lite designers. Six new workstation modules, a live tuning overlay for play mode, a content coverage analyzer, a run blueprint visual editor, a data dependency graph, and a template/preset library. Turns guesswork into data-driven design. Fully modular — each tool is independent, uses the `IRunWorkstationModule` interface, and can be removed without breaking others.

---

## Problem

EPIC 23.6 delivered the foundation: 5 workstation modules, dry-run simulator, Monte Carlo batching. But several critical gaps remain before the tooling reaches AAA production quality:

1. **No visual run design** — Designers edit zone sequences as flat lists of ScriptableObjects. There's no visual timeline, no branching path visualization, no drag-and-drop layout. This is acceptable for programmers, hostile for level designers.

2. **No cross-asset intelligence** — Each module edits one SO type in isolation. "Which enemies never spawn before zone 4?" requires manually opening every EncounterPoolSO and cross-referencing with every ZoneDefinitionSO. There's no automated coverage analysis.

3. **No live tuning** — Adjusting difficulty mid-session requires stopping play mode, editing the SO, re-entering play mode. The feedback loop is 30-60 seconds per tweak. AAA rogue-lites (Hades, Dead Cells) iterate 10x faster with runtime sliders.

4. **No data dependency awareness** — Deleting or renaming a ZoneDefinitionSO silently orphans references from ZoneSequenceSO layers. No tool warns about broken links until runtime null-refs appear.

5. **No configuration templates** — Every new run configuration starts from scratch. Common patterns (5-zone corridor, 3-arena escalation, endless loop) should be one-click presets.

6. **No comparative balance analysis** — Designers can Monte Carlo one config at a time, but can't A/B compare two configs side-by-side to see how a change affects reward curves or difficulty spikes.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     EPIC 23.7 — Editor Tooling Layer                        │
│                                                                             │
│  ┌─────────────────────┐  ┌──────────────────┐  ┌───────────────────────┐  │
│  │  Run Blueprint       │  │  Balance          │  │  Content Coverage     │  │
│  │  Visual Editor       │  │  Dashboard        │  │  Analyzer             │  │
│  │  (drag-drop zones,   │  │  (A/B compare,    │  │  (gap detection,      │  │
│  │   branching paths,   │  │   cross-SO charts, │  │   orphan refs,        │  │
│  │   zone type palette) │  │   heatmaps)       │  │   completeness score) │  │
│  └────────┬────────────┘  └────────┬─────────┘  └────────┬──────────────┘  │
│           │                        │                      │                 │
│  ┌────────┴────────────┐  ┌───────┴──────────┐  ┌───────┴──────────────┐  │
│  │  Live Tuning         │  │  Data Dependency  │  │  Template & Preset   │  │
│  │  Overlay             │  │  Graph            │  │  Library             │  │
│  │  (play-mode sliders, │  │  (SO→SO refs,     │  │  (one-click configs, │  │
│  │   real-time feedback) │  │   impact preview) │  │   clone & modify)    │  │
│  └─────────────────────┘  └──────────────────┘  └──────────────────────┘  │
│                                                                             │
│                 All implement IRunWorkstationModule                          │
│                 All use shared RogueliteDataContext                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                     Shared Infrastructure                                    │
│                                                                             │
│  RogueliteDataContext: cached index of all roguelite SOs in project          │
│  SODependencyGraph: tracks which SOs reference which SOs                     │
│  ContentCoverageReport: reusable analysis output                             │
│  LiveTuningBridge: ECS singleton ↔ editor overlay connection                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Shared Infrastructure

### RogueliteDataContext — Cached Project Index

Every module currently calls `AssetDatabase.FindAssets()` independently, which is slow on large projects and duplicates work. A shared context caches all roguelite SO references once and lets modules query instantly.

```csharp
// File: Assets/Editor/RunWorkstation/RogueliteDataContext.cs
namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// Cached index of all roguelite ScriptableObjects in the project.
    /// Built once on demand, invalidated by AssetPostprocessor callback.
    /// All RunWorkstation modules share a single instance via RunWorkstationWindow.
    /// </summary>
    public class RogueliteDataContext
    {
        // Indexed collections — populated on Build(), never during OnGUI
        public RunConfigSO[] RunConfigs;
        public ZoneDefinitionSO[] ZoneDefinitions;
        public ZoneSequenceSO[] ZoneSequences;
        public EncounterPoolSO[] EncounterPools;
        public SpawnDirectorConfigSO[] SpawnDirectorConfigs;
        public InteractablePoolSO[] InteractablePools;
        public RewardDefinitionSO[] RewardDefinitions;
        public RewardPoolSO[] RewardPools;
        public RunEventDefinitionSO[] RunEvents;
        public RunModifierPoolSO ModifierPool;            // Singleton from Resources
        public AscensionDefinitionSO AscensionDefinition; // Singleton from Resources
        public MetaUnlockTreeSO MetaUnlockTree;           // Singleton from Resources

        // Quick lookups
        public Dictionary<int, ZoneDefinitionSO> ZoneById;
        public Dictionary<int, RewardDefinitionSO> RewardById;

        // Dependency graph (built lazily)
        public SODependencyGraph DependencyGraph;

        public bool IsBuilt { get; private set; }
        public double BuildTimestamp;

        /// <summary>Scans AssetDatabase and populates all arrays. ~50-200ms on large projects.</summary>
        public void Build();

        /// <summary>Invalidate cache — next access triggers rebuild.</summary>
        public void Invalidate();
    }

    /// <summary>
    /// AssetPostprocessor that invalidates RogueliteDataContext when roguelite SOs change.
    /// </summary>
    public class RogueliteAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom);
    }
}
```

### SODependencyGraph — Reference Tracking

```csharp
// File: Assets/Editor/RunWorkstation/SODependencyGraph.cs
namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// Tracks which roguelite SOs reference which other SOs.
    /// Built by walking serialized fields of all indexed SOs.
    /// Used by: Content Coverage Analyzer (orphan detection),
    ///          Data Dependency Graph (visualization),
    ///          Delete/rename safety warnings.
    /// </summary>
    public class SODependencyGraph
    {
        /// <summary>Forward edges: source → list of targets it references.</summary>
        public Dictionary<ScriptableObject, List<ScriptableObject>> References;

        /// <summary>Reverse edges: target → list of sources that reference it.</summary>
        public Dictionary<ScriptableObject, List<ScriptableObject>> ReferencedBy;

        /// <summary>Walk all serialized fields of indexed SOs and build edges.</summary>
        public void Build(RogueliteDataContext context);

        /// <summary>All SOs that directly or transitively depend on the given SO.</summary>
        public List<ScriptableObject> GetDependents(ScriptableObject so);

        /// <summary>All SOs that the given SO directly or transitively references.</summary>
        public List<ScriptableObject> GetDependencies(ScriptableObject so);

        /// <summary>SOs that no other SO references (potential orphans).</summary>
        public List<ScriptableObject> GetOrphans();

        /// <summary>Checks if deleting 'so' would break any references.</summary>
        public List<ScriptableObject> GetImpactedByDeletion(ScriptableObject so);
    }
}
```

---

## Module 6: Run Blueprint Visual Editor

**File:** `Assets/Editor/RunWorkstation/Modules/RunBlueprintModule.cs`

The centerpiece module. Replaces flat-list zone sequence editing with a visual node-based timeline.

### Features

- **Zone Timeline Canvas** — Horizontal timeline where each column is a zone index. ZoneDefinitionSO nodes are dragged from a palette onto columns. Multiple nodes per column = WeightedRandom layer. Single node = Fixed layer.

- **Branching Path Visualization** — When a layer uses `PlayerChoice` mode, the timeline forks into parallel tracks (like a Slay the Spire map). Visual arrows show possible paths.

- **Zone Type Palette** — Side panel with all `ZoneDefinitionSO` assets grouped by type (Combat, Elite, Boss, Shop, Event, Rest). Color-coded tiles. Drag onto timeline to add.

- **Quick-Create Inline** — Right-click timeline → "New Zone Definition" creates a ZoneDefinitionSO asset and adds it to the layer in one action.

- **Difficulty Overlay** — Toggle to show difficulty curve as a gradient bar beneath the timeline (green → yellow → red). Each zone node shows its effective difficulty badge.

- **Connection Lines** — Lines connecting encounter pools and spawn director configs to zone nodes. Dashed lines for missing references (red highlight).

- **Run Summary Strip** — Bottom bar showing: total zones, zone type distribution, estimated currency earned, estimated total enemies (from simulator).

### Core Types

```csharp
// File: Assets/Editor/RunWorkstation/Modules/RunBlueprintModule.cs
namespace DIG.Roguelite.Editor.Modules
{
    public class RunBlueprintModule : IRunWorkstationModule
    {
        public string TabName => "Run Blueprint";

        // Canvas state
        private RunConfigSO _runConfig;
        private ZoneSequenceSO _sequence;
        private Vector2 _canvasOffset;
        private float _zoom = 1f;

        // Layout constants
        private const float NodeWidth = 120f;
        private const float NodeHeight = 80f;
        private const float ColumnSpacing = 160f;
        private const float RowSpacing = 100f;

        // Drag state
        private ZoneDefinitionSO _draggedZone;
        private int _dragTargetColumn;

        // Cached data from RogueliteDataContext
        private RogueliteDataContext _context;

        public void OnGUI();
        public void OnEnable();
        public void OnDisable();
    }
}
```

### Node Rendering

Each zone node renders as a rounded rect with:
- **Header bar** color-coded by ZoneType (Combat=red, Elite=orange, Boss=dark red, Shop=gold, Event=purple, Rest=green, Exploration=teal)
- **Zone name** (truncated to fit)
- **Difficulty badge** (bottom-left, e.g., "2.4x")
- **Encounter pool icon** or "!" warning if missing
- **Weight label** (for WeightedRandom layers, e.g., "40%")
- **Selection highlight** — click to select, inspector shows full ZoneDefinitionSO details in side panel

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Delete | Remove selected zone from sequence layer |
| Ctrl+D | Duplicate selected zone node (creates new entry, same zone def) |
| F | Frame all nodes in view |
| Space | Toggle difficulty overlay |
| Ctrl+Z | Undo (uses Unity's Undo system) |

---

## Module 7: Balance Dashboard

**File:** `Assets/Editor/RunWorkstation/Modules/BalanceDashboardModule.cs`

Cross-SO analytical dashboard with comparative analysis.

### Features

- **Difficulty Curve Comparison** — Select 2 RunConfigSOs. Line chart shows both difficulty curves overlaid. Divergence highlighted in red/green.

- **Enemy Spawn Heatmap** — Grid: rows = enemy types (from all EncounterPools), columns = zone indices. Cell color = expected spawn frequency (from Monte Carlo). Instantly shows "Skeleton Warrior never appears before zone 4" patterns.

- **Reward Distribution Charts** — Stacked bar chart per zone: what reward types are available at each zone index. Accounts for MinZoneIndex/MaxZoneIndex constraints on RewardDefinitionSO.

- **Economy Flow Graph** — Line chart: X = zone index, Y = cumulative run currency. Shows expected currency at each point with min/max bands (from Monte Carlo).

- **A/B Comparison Panel** — Split view: Config A vs Config B. Run Monte Carlo on both (same seeds). Side-by-side results: average score, currency, kill count, zone clear times. Delta column highlights differences with arrows.

- **Elite Frequency Tracker** — Given ascension level and encounter pool, shows expected elite spawn rate per zone. Graph shows how EliteChance × difficulty thresholds interact.

### Core Types

```csharp
// File: Assets/Editor/RunWorkstation/Modules/BalanceDashboardModule.cs
namespace DIG.Roguelite.Editor.Modules
{
    public class BalanceDashboardModule : IRunWorkstationModule
    {
        public string TabName => "Balance Dashboard";

        // A/B comparison
        private RunConfigSO _configA;
        private RunConfigSO _configB;
        private MonteCarloResult _resultsA;
        private MonteCarloResult _resultsB;
        private int _monteCarloRunCount = 500;
        private byte _compareAscension;

        // Heatmap data — [enemyIndex, zoneIndex] = spawn frequency
        private float[,] _spawnHeatmap;
        private string[] _enemyNames;
        private int _heatmapZoneCount;

        // View mode
        private DashboardView _activeView; // enum: DifficultyComparison, SpawnHeatmap, RewardDistribution, EconomyFlow, ABComparison, EliteFrequency
    }

    public enum DashboardView
    {
        DifficultyComparison,
        SpawnHeatmap,
        RewardDistribution,
        EconomyFlow,
        ABComparison,
        EliteFrequency
    }
}
```

### Spawn Heatmap Construction

```
For each EncounterPool referenced by any ZoneDefinition in the sequence:
  For each zone index 0..N:
    effective_difficulty = RunConfig.GetDifficultyAtZone(i) × zoneDef.DifficultyMultiplier
    For each EncounterPoolEntry:
      if MinDifficulty <= effective_difficulty <= MaxDifficulty:
        heatmap[entry_index][zone_index] += entry.Weight / totalWeight
```

Rendered as color grid: white (0%) → blue (low) → red (high). Tooltip shows exact percentage on hover.

---

## Module 8: Content Coverage Analyzer

**File:** `Assets/Editor/RunWorkstation/Modules/ContentCoverageModule.cs`

Automated QA for data completeness. Finds gaps, orphans, and misconfigured assets.

### Checks

| Check | Severity | Description |
|-------|----------|-------------|
| **Orphaned ZoneDefinitions** | Warning | ZoneDefinitionSO assets not referenced by any ZoneSequenceSO |
| **Orphaned EncounterPools** | Warning | EncounterPoolSO assets not referenced by any ZoneDefinitionSO |
| **Orphaned RewardDefinitions** | Warning | RewardDefinitionSO assets not in any RewardPoolSO |
| **Missing EncounterPool** | Error | Combat/Elite/Arena/Boss ZoneDefinitions with null EncounterPool |
| **Missing SpawnDirector** | Error | ZoneDefinitions with EncounterPool but no SpawnDirectorConfig |
| **Empty EncounterPool** | Error | EncounterPoolSO with zero entries |
| **Zero-Weight Entries** | Warning | Pool entries with Weight = 0 (dead entries) |
| **Unreachable Rewards** | Warning | RewardDefinitions with MinZoneIndex > RunConfig.ZoneCount |
| **Unreachable Modifiers** | Warning | Modifiers with RequiredAscensionLevel > max AscensionDefinition tier |
| **Orphaned MetaUnlocks** | Error | MetaUnlockEntry with PrerequisiteId pointing to nonexistent UnlockId |
| **Duplicate IDs** | Error | Multiple ZoneDefinitions/Rewards/Modifiers/Unlocks with same ID |
| **No Boss Zone** | Warning | ZoneSequence with no Boss-type zone (run may feel unfinished) |
| **No Shop Zone** | Info | ZoneSequence with no Shop zone (economy has no spend opportunity) |
| **Difficulty Gap** | Warning | Consecutive zones where difficulty drops (non-monotonic curve) |
| **Elite Impossible** | Info | EncounterPool has elite-capable entries but EliteMinDifficulty is unreachable |
| **Budget Underflow** | Warning | SpawnDirectorConfig.InitialBudget < cheapest entry's SpawnCost (nothing can spawn) |
| **Event Probability Sum** | Warning | RunEventDefinition choices where no choice has SuccessProbability = 1 (always-fail possible) |

### Output

```csharp
// File: Assets/Editor/RunWorkstation/ContentCoverageReport.cs
namespace DIG.Roguelite.Editor
{
    public enum CoverageSeverity { Info, Warning, Error }

    public struct CoverageIssue
    {
        public CoverageSeverity Severity;
        public string Category;       // "Zones", "Encounters", "Rewards", "Meta", "Economy"
        public string Message;
        public ScriptableObject Asset; // Clickable — ping in Project window
    }

    /// <summary>
    /// Reusable coverage report. Generated by ContentCoverageAnalyzer.
    /// Displayed by ContentCoverageModule. Also used by RogueliteSetupWizard validation.
    /// </summary>
    public class ContentCoverageReport
    {
        public List<CoverageIssue> Issues;
        public int ErrorCount;
        public int WarningCount;
        public int InfoCount;
        public float CompletenessScore;   // 0-100%. Errors = -20 each, Warnings = -5 each
        public double GeneratedTimestamp;
    }

    /// <summary>
    /// Stateless analyzer. Takes RogueliteDataContext, produces ContentCoverageReport.
    /// </summary>
    public static class ContentCoverageAnalyzer
    {
        public static ContentCoverageReport Analyze(RogueliteDataContext context);
    }
}
```

### UI Layout

- **Completeness Score** — Large circular gauge (0-100%) at top. Green >80%, Yellow 50-80%, Red <50%.
- **Issue List** — Filterable by severity and category. Columns: Severity icon, Category, Message, Asset (clickable).
- **"Fix" Buttons** — Where possible: "Assign EncounterPool" opens asset picker inline. "Remove dead entry" deletes zero-weight entries.
- **"Re-scan" Button** — Rebuilds RogueliteDataContext and re-analyzes. Shows timestamp of last scan.

---

## Module 9: Live Tuning Overlay

**File:** `Assets/Editor/RunWorkstation/Modules/LiveTuningModule.cs`

Play-mode only. Modifies ECS singletons in real-time via editor sliders.

### Features

- **Difficulty Multiplier Slider** — Writes directly to `RuntimeDifficultyScale.ZoneDifficultyMultiplier`. Range 0.1x–10x. Changes take effect next frame.

- **Spawn Rate Override** — Modifies `ZoneState.SpawnBudget` directly. Buttons: "Pause Spawns" (set to 0), "Max Budget" (set to MaxBudget), slider for CreditsPerSecond override.

- **Currency Grant** — "+100 Run Currency", "+1000 Meta Currency" buttons. Writes directly to `RunState.RunCurrency` and `MetaBank.MetaCurrency`.

- **Phase Skip** — "Skip to Next Zone", "Trigger Boss", "End Run (Win)", "End Run (Death)" buttons. Writes `RunState.Phase` directly.

- **Modifier Injection** — Dropdown of all modifiers from `RunModifierPoolSO`. "Add Modifier" button creates `ModifierAcquisitionRequest` entity.

- **Zone State Inspector** — Read-only display of current `ZoneState` fields: TimeInZone, EnemiesAlive, SpawnBudget, EffectiveDifficulty. Updates every frame.

- **Kill All Enemies** — Button that finds all entities with `Health` + enemy tag and sets Health to 0. Instant zone clear for testing.

### ECS Bridge

```csharp
// File: Assets/Scripts/Roguelite/Bridges/LiveTuningBridge.cs
namespace DIG.Roguelite
{
    /// <summary>
    /// Singleton component for editor-to-ECS live tuning communication.
    /// LiveTuningModule writes override values; DifficultyScalingSystem reads them.
    /// Only present in UNITY_EDITOR builds.
    /// </summary>
    public struct LiveTuningOverrides : IComponentData
    {
        public float DifficultyMultiplierOverride; // 0 = no override, >0 = override value
        public float SpawnRateOverride;            // 0 = no override
        public bool PauseSpawning;
        public int GrantRunCurrency;               // Consumed by system, reset to 0 after grant
        public int GrantMetaCurrency;
        public RunPhase ForcePhase;                // None = no override
    }

    /// <summary>
    /// System that applies LiveTuningOverrides to actual game state.
    /// Runs in SimulationSystemGroup, UpdateBefore(DifficultyScalingSystem).
    /// Only compiled in UNITY_EDITOR.
    /// </summary>
    #if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DifficultyScalingSystem))]
    public partial class LiveTuningApplySystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<LiveTuningOverrides>();
        }

        protected override void OnUpdate()
        {
            // Read overrides, apply to RunState/ZoneState/RuntimeDifficultyScale,
            // consume one-shot grants (reset to 0 after applying)
        }
    }
    #endif
}
```

### Safety

- All overrides are transient — they don't modify ScriptableObjects
- "Reset All Overrides" button clears LiveTuningOverrides to defaults
- Overrides display in yellow to remind designer they're active
- Overrides auto-clear when exiting play mode (OnDisable)

---

## Module 10: Data Dependency Graph

**File:** `Assets/Editor/RunWorkstation/Modules/DependencyGraphModule.cs`

Visual graph showing how roguelite SOs connect.

### Features

- **Auto-Layout Graph** — Force-directed or hierarchical layout. Nodes = SOs. Edges = serialized field references. Color-coded by type.

- **Node Types & Colors**:

| SO Type | Color | Shape |
|---------|-------|-------|
| RunConfigSO | White | Diamond |
| ZoneSequenceSO | Cyan | Rectangle |
| ZoneDefinitionSO | Blue | Rectangle |
| EncounterPoolSO | Red | Circle |
| SpawnDirectorConfigSO | Orange | Circle |
| RewardPoolSO | Gold | Hexagon |
| RewardDefinitionSO | Yellow | Small circle |
| InteractablePoolSO | Green | Circle |
| RunModifierPoolSO | Purple | Rectangle |
| AscensionDefinitionSO | Dark purple | Diamond |
| MetaUnlockTreeSO | Teal | Rectangle |

- **Focus Mode** — Click a node to highlight its direct references (outgoing = blue arrows) and dependents (incoming = green arrows). All other nodes dim.

- **Impact Preview** — Right-click a node → "What breaks if I delete this?" Shows all transitively dependent SOs highlighted in red.

- **Navigate to Asset** — Double-click a node to ping the SO in the Project window and open it in Inspector.

- **Filter by Type** — Toggle visibility of each SO type. E.g., hide all RewardDefinitions to see structural overview.

- **Orphan Highlight** — Nodes with zero incoming edges (not referenced by anything) show a yellow warning border.

### Layout Algorithm

Layered graph (Sugiyama-style):
1. Root layer: RunConfigSO
2. Layer 1: ZoneSequenceSO
3. Layer 2: ZoneDefinitionSO nodes
4. Layer 3: EncounterPoolSO, SpawnDirectorConfigSO, RewardPoolSO, InteractablePoolSO
5. Layer 4: RewardDefinitionSO, RunEventDefinitionSO
6. Parallel column: MetaUnlockTreeSO, RunModifierPoolSO, AscensionDefinitionSO

Positions computed once on data change, cached. Panning and zooming via mouse scroll + middle-click drag.

---

## Module 11: Template & Preset Library

**File:** `Assets/Editor/RunWorkstation/Modules/TemplateLibraryModule.cs`

One-click creation of complete run configurations from proven templates.

### Built-In Templates

| Template | Zones | Pattern | Director Style | Description |
|----------|-------|---------|----------------|-------------|
| **Corridor Run** | 5 | Fixed (C, C, E, C, B) | Burst (high InitialBudget, 0 CreditsPerSecond) | Classic Hades-style: clear room, move on |
| **Arena Escalation** | 3 | Fixed (A, A, B) | Accelerating (low initial, high acceleration) | Vampire Survivors-style: survive escalating waves |
| **Exploration Run** | 7 | Fixed (Ex, C, Sh, C, Ev, E, B) | Continuous (steady CreditsPerSecond) | Risk of Rain-style: explore, shop, events |
| **Branching Paths** | 5 | WeightedRandom per layer (2-3 choices) | Mixed | Slay the Spire-style: choose your route |
| **Endless Loop** | 4 + loop | Looping from index 1, 1.5x multiplier | Continuous | Infinite run with escalating difficulty |
| **Boss Rush** | 5 | Fixed (B, B, B, B, B) | Burst (massive budget, high elite chance) | All bosses, no filler |
| **Tutorial** | 3 | Fixed (C, Sh, C) | Gentle (low budget, slow rate, no elites) | Onboarding run with low stakes |

### Template Application

```csharp
// File: Assets/Editor/RunWorkstation/RunConfigTemplate.cs
namespace DIG.Roguelite.Editor
{
    [Serializable]
    public class RunConfigTemplate
    {
        public string TemplateName;
        public string Description;
        public Texture2D Icon;

        // Zone structure
        public int ZoneCount;
        public ZoneTemplateEntry[] Zones;
        public bool EnableLooping;
        public int LoopStartIndex;
        public float LoopDifficultyMultiplier;

        // Difficulty curve keyframes
        public Keyframe[] DifficultyCurve;

        // Economy
        public int StartingCurrency;
        public int CurrencyPerZoneClear;

        // Director defaults
        public float DefaultInitialBudget;
        public float DefaultCreditsPerSecond;
        public float DefaultAcceleration;
        public int DefaultMaxAliveEnemies;
    }

    [Serializable]
    public struct ZoneTemplateEntry
    {
        public ZoneType Type;
        public ZoneClearMode ClearMode;
        public float DifficultyMultiplier;
        public ZoneSelectionMode SelectionMode;
        public int ChoiceCount; // For WeightedRandom/PlayerChoice
    }
}
```

### Workflow

1. Designer selects template from grid (shows template name, description, zone count, pattern diagram)
2. "Apply Template" creates:
   - `RunConfigSO` with name `RunConfig_[TemplateName]_[timestamp]`
   - `ZoneSequenceSO` with layers matching template structure
   - `ZoneDefinitionSO` per unique zone (with type and clear mode pre-set)
   - `SpawnDirectorConfigSO` with template's director defaults
3. All assets created in `Assets/Data/Roguelite/[TemplateName]/`
4. Template assets are NOT created if matching SOs already exist — reuses by name
5. **Clone & Modify** — Select existing RunConfigSO, click "Clone as Template" to create an editable copy with all referenced SOs deep-cloned

### Custom Templates

- "Save as Template" button on any RunConfigSO → serializes current config structure as `.asset` in `Assets/Data/Roguelite/Templates/`
- Custom templates appear alongside built-in templates in the grid
- Templates are ScriptableObjects — version-controllable, shareable between team members

---

## Updated IRunWorkstationModule Interface

```csharp
// File: Assets/Editor/RunWorkstation/IRunWorkstationModule.cs — MODIFIED
namespace DIG.Roguelite.Editor
{
    public interface IRunWorkstationModule
    {
        string TabName { get; }
        void OnGUI();
        void OnEnable();
        void OnDisable();

        /// <summary>
        /// Optional: receive shared data context. Called after Build().
        /// Modules that don't need cross-SO data can ignore this.
        /// </summary>
        void SetContext(RogueliteDataContext context) { }  // Default interface method (C# 8)
    }
}
```

---

## Systems Table

| System/Class | Location | Type | Purpose |
|-------------|----------|------|---------|
| `RogueliteDataContext` | Editor/RunWorkstation/ | Editor utility | Cached index of all roguelite SOs. Shared across modules |
| `RogueliteAssetPostprocessor` | Editor/RunWorkstation/ | AssetPostprocessor | Invalidates RogueliteDataContext on asset changes |
| `SODependencyGraph` | Editor/RunWorkstation/ | Editor utility | Forward/reverse reference graph between SOs |
| `ContentCoverageAnalyzer` | Editor/RunWorkstation/ | Static analyzer | Produces ContentCoverageReport from RogueliteDataContext |
| `ContentCoverageReport` | Editor/RunWorkstation/ | Data class | Reusable analysis output with issues, severity, completeness score |
| `RunConfigTemplate` | Editor/RunWorkstation/ | ScriptableObject | Serializable run configuration template |
| `RunBlueprintModule` | Editor/RunWorkstation/Modules/ | IRunWorkstationModule | Visual zone sequence timeline editor |
| `BalanceDashboardModule` | Editor/RunWorkstation/Modules/ | IRunWorkstationModule | Cross-SO analytics with A/B comparison |
| `ContentCoverageModule` | Editor/RunWorkstation/Modules/ | IRunWorkstationModule | Automated data completeness checker |
| `LiveTuningModule` | Editor/RunWorkstation/Modules/ | IRunWorkstationModule | Play-mode real-time parameter adjustment |
| `DependencyGraphModule` | Editor/RunWorkstation/Modules/ | IRunWorkstationModule | Visual SO reference graph |
| `TemplateLibraryModule` | Editor/RunWorkstation/Modules/ | IRunWorkstationModule | One-click run configuration presets |
| `LiveTuningOverrides` | Scripts/Roguelite/Bridges/ | IComponentData | ECS singleton for editor↔runtime bridge |
| `LiveTuningApplySystem` | Scripts/Roguelite/Systems/ | SystemBase | Applies editor overrides to game state (UNITY_EDITOR only) |

---

## Integration

- **RogueliteSetupWizard**: Replace per-section `AssetDatabase.FindAssets` calls with `RogueliteDataContext` queries. Validation section uses `ContentCoverageAnalyzer` instead of hand-coded checks.

- **Existing RunWorkstation modules**: Receive `RogueliteDataContext` via `SetContext()`. EncounterPoolModule can use `context.EncounterPools` instead of requiring manual assignment. ZoneSequenceModule can auto-populate zone definitions from context.

- **RunSimulator**: Used by BalanceDashboardModule for Monte Carlo comparison. Same `RunSimulator.SimulateAggregate()` path — no duplication.

- **Unity Undo**: RunBlueprintModule and TemplateLibraryModule register all SO modifications via `Undo.RecordObject()`. Full undo/redo support.

- **EditorApplication.update**: LiveTuningModule reads ECS world state via `World.DefaultGameObjectInjectionWorld` during play mode. BalanceDashboardModule runs Monte Carlo async via existing `RunMonteCarloSimulator` pattern.

---

## Performance

- **RogueliteDataContext.Build()**: ~50-200ms (single `AssetDatabase.FindAssets` per SO type, cached). Invalidated by AssetPostprocessor, not per-frame.
- **SODependencyGraph.Build()**: ~10-50ms (walks serialized fields via `SerializedObject` iteration). Built lazily on first DependencyGraphModule open.
- **ContentCoverageAnalyzer.Analyze()**: ~5-20ms (iterates cached arrays, no asset loading).
- **RunBlueprintModule.OnGUI()**: O(zones × entries) for node rendering. Cached layout positions. No allocations per frame beyond IMGUI internals.
- **LiveTuningModule**: Reads ECS singletons via `EntityManager.GetComponentData()` — one structural lookup per frame, <0.1ms.
- **BalanceDashboardModule Monte Carlo**: Runs via existing async `EditorApplication.update` batching (50 runs/frame). 500 runs ≈ 1 second.
- **DependencyGraphModule**: Positions computed once on data change. OnGUI only draws visible nodes (frustum culling against scroll view).

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Editor/RunWorkstation/RogueliteDataContext.cs` | Editor utility | **NEW** |
| `Assets/Editor/RunWorkstation/RogueliteAssetPostprocessor.cs` | AssetPostprocessor | **NEW** |
| `Assets/Editor/RunWorkstation/SODependencyGraph.cs` | Editor utility | **NEW** |
| `Assets/Editor/RunWorkstation/ContentCoverageReport.cs` | Data classes | **NEW** |
| `Assets/Editor/RunWorkstation/ContentCoverageAnalyzer.cs` | Static analyzer | **NEW** |
| `Assets/Editor/RunWorkstation/RunConfigTemplate.cs` | ScriptableObject | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/RunBlueprintModule.cs` | IRunWorkstationModule | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/BalanceDashboardModule.cs` | IRunWorkstationModule | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/ContentCoverageModule.cs` | IRunWorkstationModule | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/LiveTuningModule.cs` | IRunWorkstationModule | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/DependencyGraphModule.cs` | IRunWorkstationModule | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/TemplateLibraryModule.cs` | IRunWorkstationModule | **NEW** |
| `Assets/Scripts/Roguelite/Bridges/LiveTuningBridge.cs` | IComponentData + SystemBase | **NEW** |
| `Assets/Editor/RunWorkstation/IRunWorkstationModule.cs` | Interface | Modified (add `SetContext` default method) |
| `Assets/Editor/RunWorkstation/RunWorkstationWindow.cs` | EditorWindow | Modified (add context sharing, new module registration) |
| `Assets/Editor/RogueliteWorkstation/RogueliteSetupWizard.cs` | EditorWindow | Modified (use ContentCoverageAnalyzer for validation) |
| `Assets/Data/Roguelite/Templates/` | Folder | **NEW** (template assets) |

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Shared `RogueliteDataContext` instead of per-module queries | Eliminates 15+ `AssetDatabase.FindAssets` calls across modules. Single scan, shared cache. |
| `SODependencyGraph` as separate class, not embedded in context | Graph is expensive to build (serialized field walking). Only DependencyGraphModule and ContentCoverageModule need it. Lazy construction. |
| `LiveTuningOverrides` as ECS component, not static fields | Follows existing framework patterns (RunState, RuntimeDifficultyScale). Works correctly with NetCode worlds. Automatically cleared on world disposal. |
| `LiveTuningApplySystem` guarded by `#if UNITY_EDITOR` | Zero runtime cost in builds. No stripping needed. |
| Templates as ScriptableObjects, not JSON | Consistent with project's SO-based content pipeline. Version-controllable. Inspector-editable. |
| `ContentCoverageReport` as reusable class, not module-internal | Can be consumed by both ContentCoverageModule (detailed view) and RogueliteSetupWizard (summary view). Avoids duplicate analysis logic. |
| Default interface method for `SetContext()` | Backward-compatible with existing modules that don't need cross-SO data. No forced implementation. C# 8 default methods work in Unity 2021+. |
| RunBlueprintModule uses IMGUI canvas, not UI Toolkit | Consistent with all existing workstation modules. IMGUI is proven for node-graph editors (Shader Graph, VFX Graph started IMGUI). Lower risk. |
| Built-in templates ship as code, not assets | Always available. Can't be accidentally deleted. Custom templates are SO assets. |

---

## Module Independence Matrix

| Module | Requires | Optional Enhancement From |
|--------|----------|---------------------------|
| Run Blueprint (6) | RogueliteDataContext | ContentCoverageAnalyzer (inline warnings) |
| Balance Dashboard (7) | RogueliteDataContext, RunSimulator | ContentCoverageAnalyzer (data quality badges) |
| Content Coverage (8) | RogueliteDataContext, SODependencyGraph | None |
| Live Tuning (9) | LiveTuningOverrides component | None (standalone play-mode tool) |
| Dependency Graph (10) | RogueliteDataContext, SODependencyGraph | ContentCoverageAnalyzer (orphan highlight) |
| Template Library (11) | RunConfigTemplate | RogueliteDataContext (duplicate detection) |

---

## Implementation Sequence

1. **Phase 1: Infrastructure** — RogueliteDataContext, SODependencyGraph, ContentCoverageReport/Analyzer, IRunWorkstationModule.SetContext()
2. **Phase 2: Analysis Tools** — ContentCoverageModule, DependencyGraphModule (consume Phase 1 infrastructure)
3. **Phase 3: Visual Editor** — RunBlueprintModule (the most complex module, benefits from context)
4. **Phase 4: Balance Intelligence** — BalanceDashboardModule (requires Phase 1, builds on existing RunSimulator)
5. **Phase 5: Rapid Content** — TemplateLibraryModule (standalone, creates assets)
6. **Phase 6: Live Iteration** — LiveTuningModule + LiveTuningOverrides/ApplySystem (requires play mode, tested last)

---

## Verification

1. **RogueliteDataContext**: Build() indexes all roguelite SOs correctly. AssetPostprocessor invalidates on SO create/delete/rename. Modules see same shared instance.
2. **SODependencyGraph**: Forward and reverse edges match. GetOrphans() returns SOs with zero incoming references. GetImpactedByDeletion() includes transitive dependents.
3. **ContentCoverageAnalyzer**: All 17 check types fire correctly. CompletenessScore = 100% for a fully configured project. Missing encounter pool = Error. Zero-weight entry = Warning.
4. **RunBlueprintModule**: Drag ZoneDefinition onto timeline → creates ZoneSequenceEntry. Delete node → removes entry. Undo restores. Difficulty overlay matches RunConfig curve.
5. **BalanceDashboardModule**: A/B comparison runs Monte Carlo on both configs with same seeds. Heatmap correctly shows enemy frequency distribution. Economy graph matches single-run simulation.
6. **LiveTuningModule**: Difficulty slider changes RuntimeDifficultyScale in same frame. Currency grant increases RunState.RunCurrency. Phase skip transitions correctly. All overrides clear on play mode exit.
7. **DependencyGraphModule**: All edges match manual inspection of SO serialized fields. Focus mode dims unrelated nodes. Impact preview highlights correct dependents.
8. **TemplateLibraryModule**: "Corridor Run" template creates RunConfig with 5 zones (C, C, E, C, B). All assets saved to correct folder. "Clone" deep-copies all referenced SOs.
9. **Performance**: RogueliteDataContext.Build() < 200ms. ContentCoverageAnalyzer.Analyze() < 20ms. RunBlueprintModule.OnGUI() maintains 60fps with 20 zone nodes. LiveTuningModule reads < 0.1ms per frame.
10. **Modularity**: Removing any single module .cs file does not cause compilation errors in other modules. LiveTuningOverrides component removal does not break non-editor builds.
