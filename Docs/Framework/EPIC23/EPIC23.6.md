# EPIC 23.6: Run Analytics, History & Editor Tooling

**Status:** PLANNED
**Priority:** Medium (designer velocity — makes balancing fast instead of guesswork)
**Dependencies:**
- EPIC 23.1 (RunState, RunPhase)
- EPIC 23.2 (MetaBank entity for history storage)
- `ISaveModule` (existing — `Assets/Scripts/Persistence/Core/`, EPIC 16.15)
- Existing Workstation pattern (`Assets/Editor/CombatWorkstation/`)

**Feature:** Per-run statistics tracking, persistent run history, and a comprehensive editor workstation with 5 modules for zone sequences, encounter pools, rewards, modifiers, and meta-tree design. Includes a RunSimulator for dry-run and Monte Carlo balance testing without play mode.

---

## Problem

Designers can't balance a rogue-lite by feel alone. They need data: how long do zones take? What's the average kill count? Are rewards too generous at zone 3? Without analytics, balancing is guesswork. Without editor tooling, iteration requires play-testing every change. A Monte Carlo simulator lets designers test 1000 runs in seconds.

---

## Runtime Types

```csharp
// File: Assets/Scripts/Roguelite/Components/RunStatistics.cs
namespace DIG.Roguelite.Analytics
{
    /// <summary>
    /// Tracks per-run performance metrics. On RunState entity.
    /// Optional ghost replication for spectator/co-op HUD.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RunStatistics : IComponentData
    {
        [GhostField] public int TotalKills;
        [GhostField] public int EliteKills;
        [GhostField] public int BossKills;
        [GhostField(Quantization = 1)] public float DamageTaken;
        [GhostField(Quantization = 1)] public float DamageDealt;
        [GhostField] public int ItemsCollected;
        [GhostField] public int RunCurrencySpent;
        [GhostField] public int RunCurrencyEarned;
        [GhostField] public byte ZonesCleared;
        [GhostField] public byte ModifiersAcquired;
        [GhostField(Quantization = 100)] public float FastestZoneTime;
        [GhostField(Quantization = 100)] public float SlowestZoneTime;
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/ZoneTimingEntry.cs
namespace DIG.Roguelite.Analytics
{
    /// <summary>
    /// Per-zone breakdown on RunState entity.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ZoneTimingEntry : IBufferElementData
    {
        public byte ZoneIndex;
        public byte ZoneType;                       // Cast from ZoneType enum
        public float Duration;
        public int Kills;
        public int DamageTaken;
        public int CurrencyEarned;
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/RunHistoryEntry.cs
namespace DIG.Roguelite.Analytics
{
    /// <summary>
    /// Completed run summaries on MetaBank entity. Ring buffer, max 100.
    /// Persisted via RunHistorySaveModule (TypeId=17).
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct RunHistoryEntry : IBufferElementData
    {
        public uint RunId;
        public uint Seed;
        public byte AscensionLevel;
        public byte EndReason;                      // Cast from RunEndReason
        public byte ZonesCleared;
        public int Score;
        public float Duration;
        public int TotalKills;
        public long Timestamp;                      // Unix seconds
    }
}
```

---

## Runtime Systems

| System | Group | World Filter | Burst | Purpose |
|--------|-------|--------------|-------|---------|
| `RunStatisticsTrackingSystem` | SimulationSystemGroup | Server\|Local | Partial | Listens to kill events, damage events, currency changes. Updates `RunStatistics` incrementally. Records `ZoneTimingEntry` on zone clear |
| `RunHistoryRecordSystem` | SimulationSystemGroup, UpdateAfter(RunEndSystem) | Server\|Local | No | On `RunPhase == MetaScreen`: creates `RunHistoryEntry` from `RunState` + `RunStatistics`. Maintains ring buffer cap (100). Marks save dirty |
| `RunAnalyticsBridgeSystem` | PresentationSystemGroup | Client\|Local | No | Reads `RunStatistics` + `ZoneTimingEntry`. Pushes to `RunAnalyticsRegistry` for game HUD (post-run stats screen) |

---

## Editor Tooling — RunConfigurationWorkstation

**File:** `Assets/Editor/RunWorkstation/RunWorkstationWindow.cs`

Follows existing Workstation pattern (sidebar tabs, module interface). EditorWindow with 5 tab modules.

```csharp
// File: Assets/Editor/RunWorkstation/IRunWorkstationModule.cs
namespace DIG.Roguelite.Editor
{
    public interface IRunWorkstationModule
    {
        string TabName { get; }
        Texture2D Icon { get; }
        void OnGUI(Rect area);
        void OnEnable();
        void OnDisable();
    }
}
```

### Module 1: Zone Sequence Builder

**File:** `Assets/Editor/RunWorkstation/Modules/ZoneSequenceModule.cs`

- Visual timeline of zones per `RunConfigSO`
- Drag `ZoneDefinitionSO` assets into sequence layers
- Per-layer mode selector: Fixed / WeightedRandom / PlayerChoice
- Weight sliders for random pools
- Difficulty curve preview via embedded AnimationCurve editor
- Zone-type distribution pie chart (how many combat vs elite vs boss vs shop)
- Validation: warns if no boss zone, if shop never appears, if sequence too short

### Module 2: Encounter Pool Editor

**File:** `Assets/Editor/RunWorkstation/Modules/EncounterPoolModule.cs`

- Edit `EncounterPoolSO` assets inline
- Enemy prefab picker with thumbnail preview
- Count range sliders (min/max per entry)
- Weight normalization display (shows effective %)
- Difficulty-gated entries highlighted by zone index
- "Preview Spawn" button: given a seed, shows exact enemy composition for each zone
- Total enemy count estimate per zone

### Module 3: Reward Configurator

**File:** `Assets/Editor/RunWorkstation/Modules/RewardModule.cs`

- Edit `RewardPoolSO` and `RewardDefinitionSO` assets
- Rarity distribution pie chart across pool
- Expected value calculator per zone (accounts for zone constraints)
- Choice count configuration with preview
- Event pool editor: narrative text, choice text, probability sliders
- "Roll Rewards" button: given a seed + zone index, shows exact choices generated

### Module 4: Modifier Designer

**File:** `Assets/Editor/RunWorkstation/Modules/ModifierModule.cs`

- Create/edit `RunModifierDefinitionSO` assets
- Polarity color-coding (green/red/yellow)
- Stacking rules visualizer
- Ascension tier builder: drag modifiers into tiers, preview cumulative difficulty
- `AscensionDefinitionSO` editor with tier list and forced modifier assignment
- Cumulative difficulty graph: X = ascension level, Y = effective multiplier

### Module 5: Meta Tree Editor

**File:** `Assets/Editor/RunWorkstation/Modules/MetaTreeModule.cs`

- Visual node graph for `MetaUnlockTreeSO`
- Drag-and-drop node creation from `MetaUnlockNodeSO` assets
- Prerequisite links drawn as arrows
- Cost progression timeline view
- Category color-coding (stat = blue, item = green, ability = purple, cosmetic = gold)
- Validation: orphan detection (nodes with missing prerequisites), cost sanity check, duplicate ID detection
- "Simulate Unlock Path" button: given meta-currency per run, shows how many runs to unlock all nodes

---

## RunSimulator (Editor-Only)

**File:** `Assets/Editor/RunWorkstation/RunSimulator.cs`

### Dry Run Mode

Given `RunConfigSO` + seed + ascension level:
1. Resolve zone sequence from `ZoneSequenceSO` using seed
2. For each zone: roll encounter composition from `EncounterPoolSO`
3. For each zone clear: roll reward choices from `RewardPoolSO`
4. Apply modifier effects to `RuntimeDifficultyScale`
5. Calculate shop prices at each shop zone
6. Output: complete run timeline with all random decisions resolved

No play mode needed. Pure data simulation.

### Monte Carlo Mode

**File:** `Assets/Editor/RunWorkstation/RunMonteCarloSimulator.cs`

Given `RunConfigSO` + ascension level + run count (default 1000):
1. Generate N seeds (sequential or random)
2. Dry-run each seed
3. Aggregate statistics:
   - Reward distribution histograms (by type, by rarity, by zone)
   - Difficulty curve graph (effective difficulty at each zone across all runs)
   - Currency earned/spent distributions
   - Zone-type frequency at each layer (for WeightedRandom sequences)
   - Modifier acquisition frequency
   - Expected time to unlock all meta-nodes (given average meta-currency per run)

Async via `EditorCoroutine` to avoid blocking the editor. Progress bar with cancel support.

### Output

```csharp
// File: Assets/Editor/RunWorkstation/RunSimulationResult.cs
namespace DIG.Roguelite.Editor
{
    public class RunSimulationResult
    {
        public uint Seed;
        public List<SimulatedZone> Zones;
        public int FinalScore;
        public int TotalCurrencyEarned;
        public int TotalCurrencySpent;
        public List<int> RewardIdsChosen;
        public List<int> ModifierIdsAcquired;
    }

    public class SimulatedZone
    {
        public int ZoneIndex;
        public ZoneType Type;
        public float EffectiveDifficulty;
        public List<EncounterPoolEntry> EnemyComposition;
        public List<RewardDefinitionSO> RewardOptions;
        public int ShopItemCount;
    }

    public class MonteCarloResult
    {
        public int RunCount;
        public float AverageScore;
        public float AverageDuration;
        public float AverageZonesCleared;
        public Dictionary<int, int> RewardFrequency;
        public Dictionary<int, int> ModifierFrequency;
        public float[] DifficultyPerZone;           // Average across all runs
        public float[] CurrencyPerZone;             // Average earned per zone
        public float EstimatedRunsToFullUnlock;
    }
}
```

---

## Integration

- **ISaveModule**: `RunHistorySaveModule` (TypeId=17) serializes `RunHistoryEntry` ring buffer. Binary format, CRC32, migration versioned
- **MetaBank entity**: History buffer lives on MetaBank entity (23.2) — co-located with persistent account data
- **Existing Workstation pattern**: `IRunWorkstationModule` follows `ICombatWorkstationModule` convention. Sidebar tabs, module lifecycle
- **Kill events**: `RunStatisticsTrackingSystem` reads existing `KillCredited` component (EPIC 16.14) for kill counting
- **Damage events**: Reads `CombatResultEvent` for damage dealt/taken aggregation

---

## Performance

- `RunStatistics`: ~48 bytes, one entity, ghost-replicated. Updated per-event (not per-frame)
- `ZoneTimingEntry`: heap-allocated. Typical: 5-15 entries per run (one per zone)
- `RunHistoryEntry`: heap-allocated. Capped at 100 entries (ring buffer). ~40 bytes each = 4KB
- `RunStatisticsTrackingSystem`: event-driven, not per-frame polling. Burst partial for increment operations
- **RunSimulator**: editor-only, no runtime cost. Monte Carlo 1000 runs completes in <2s (pure math, no ECS)

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Scripts/Roguelite/Components/RunStatistics.cs` | IComponentData | **NEW** |
| `Assets/Scripts/Roguelite/Components/ZoneTimingEntry.cs` | IBufferElementData | **NEW** |
| `Assets/Scripts/Roguelite/Components/RunHistoryEntry.cs` | IBufferElementData | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunStatisticsTrackingSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunHistoryRecordSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunAnalyticsBridgeSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Bridges/RunAnalyticsRegistry.cs` | Static registry | **NEW** |
| `Assets/Editor/RunWorkstation/RunWorkstationWindow.cs` | EditorWindow | **NEW** |
| `Assets/Editor/RunWorkstation/IRunWorkstationModule.cs` | Interface | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/ZoneSequenceModule.cs` | Editor module | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/EncounterPoolModule.cs` | Editor module | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/RewardModule.cs` | Editor module | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/ModifierModule.cs` | Editor module | **NEW** |
| `Assets/Editor/RunWorkstation/Modules/MetaTreeModule.cs` | Editor module | **NEW** |
| `Assets/Editor/RunWorkstation/RunSimulator.cs` | Editor utility | **NEW** |
| `Assets/Editor/RunWorkstation/RunMonteCarloSimulator.cs` | Editor utility | **NEW** |
| `Assets/Editor/RunWorkstation/RunSimulationResult.cs` | Data classes | **NEW** |

---

## Verification

1. **Statistics tracking**: Kill enemy → TotalKills incremented. Take damage → DamageTaken incremented
2. **Zone timing**: Zone clear records correct duration, kill count, currency earned
3. **Fastest/slowest**: FastestZoneTime and SlowestZoneTime update correctly across zones
4. **History recording**: Run end creates RunHistoryEntry with correct aggregate data
5. **History cap**: 101st run entry replaces oldest (ring buffer)
6. **History persistence**: Quit → restart → run history restored from save
7. **Dry-run simulator**: Same seed produces identical zone sequence, encounters, and rewards as live run
8. **Monte Carlo**: 1000 runs completes in <2s. Reward frequency distribution matches pool weights within statistical tolerance
9. **Workstation modules**: Each tab loads, edits, and saves corresponding ScriptableObject assets
10. **Meta tree validation**: Detects orphan nodes, duplicate IDs, circular prerequisites
