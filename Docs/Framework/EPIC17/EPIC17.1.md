# EPIC 17.1: Skill Trees & Talent System

**Status:** PLANNED
**Priority:** High (Core Progression Loop)
**Dependencies:**
- `CharacterAttributes` IComponentData (existing — `DIG.Combat.Components.CombatStatComponents.cs`, Ghost:All, Str/Dex/Int/Vit/Level, 20 bytes)
- `PlayerProgression` IComponentData (existing — `DIG.Progression.Components.PlayerProgression.cs`, Ghost:AllPredicted, CurrentXP/UnspentStatPoints, 16 bytes)
- `LevelRewardType.TalentPoint` enum value (existing — `DIG.Progression.Config.LevelRewardsSO.cs`, value=6)
- `LevelRewardSystem` (existing — `DIG.Progression.Systems.LevelRewardSystem.cs`, distributes per-level rewards)
- `AbilityDefinition` IBufferElementData (existing — `DIG.Player.Abilities.AbilityComponents.cs`, player abilities with AbilityTypeId/Priority/InputActionId)
- `StatAllocationSystem` (existing — `DIG.Progression.Systems.StatAllocationSystem.cs`, spends stat points, server-authoritative)
- `StatAllocationRpc` + `StatAllocationRpcReceiveSystem` (existing — server-validated RPC pattern reference)
- `EquippedStatsSystem` (existing — `DIG.Items.Systems.EquippedStatsSystem.cs`, aggregates equipment bonuses)
- `ProgressionConfigSingleton` BlobAsset (existing — `DIG.Progression.Config.ProgressionBlob.cs`, loads from Resources/)
- `ProgressionBootstrapSystem` (existing — `DIG.Progression.Systems.ProgressionBootstrapSystem.cs`, bootstrap singleton pattern)
- `CombatUIRegistry` / `CombatUIBridgeSystem` pattern (existing — static registry + provider interface pattern)
- `SaveStateLink` child entity pattern (existing — `DIG.Persistence.Components.SaveStateComponents.cs`, 8 bytes on player)

**Feature:** A data-driven skill tree system enabling branching ability progression with prerequisite gates, multi-tree support (class/general/prestige), passive stat modifiers, active ability unlocks, respec support, and visual editor tooling. Uses child entity pattern for talent storage (zero player archetype impact beyond an 8-byte link). Designers author trees via ScriptableObjects with a visual node editor.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `CharacterAttributes` (Str/Dex/Int/Vit/Level) | `CombatStatComponents.cs` | Ghost:All | Level drives talent point acquisition |
| `PlayerProgression` (XP, StatPoints, RestedXP) | `PlayerProgression.cs` | Ghost:AllPredicted | `UnspentStatPoints` tracked, no talent points yet |
| `LevelRewardType.TalentPoint` | `LevelRewardsSO.cs` | Enum defined | Value=6, never consumed |
| `AbilityDefinition` (player) | `AbilityComponents.cs` | IBufferElementData | Flat list, no prerequisite tree |
| `StatAllocationSystem` | `StatAllocationSystem.cs` | Server-authoritative | Spends stat points, writes CharacterAttributes |
| `EquippedStatsSystem` | `EquippedStatsSystem.cs` | Runs every frame | Aggregates equipment bonuses |
| `LevelStatScalingSystem` | `LevelStatScalingSystem.cs` | Burst, per-level | Base stats from level + allocation + equipment |
| `SaveStateLink` child entity | `SaveStateComponents.cs` | 8 bytes on player | Child entity pattern for data offloading |
| `CraftingKnowledgeLink` child entity | `CraftingKnowledgeLinkSystem.cs` | Same pattern | Recipe knowledge on child entity |

### What's Missing

- **No talent point currency** — `LevelRewardType.TalentPoint` is defined but nothing awards or tracks talent points
- **No skill tree data model** — no nodes, edges, prerequisites, or branching logic
- **No skill tree ScriptableObjects** — no designer-authored tree definitions
- **No talent allocation system** — no mechanism to spend talent points on nodes
- **No passive modifier pipeline** — no way for unlocked passives to modify stats
- **No active ability unlock from talents** — no link between talent nodes and `AbilityDefinition`
- **No respec system** — no way to refund talent points
- **No multi-tree support** — no class or specialization tree switching
- **No visual editor** — no node graph editor for designers

---

## Problem

DIG has a complete progression system (XP, leveling, stat allocation) but no branching talent trees. Players reach max level with identical builds — there's no specialization or meaningful choice beyond stat point distribution. `LevelRewardType.TalentPoint` is defined but never consumed. The ability system is a flat list with no prerequisite hierarchy. Designers cannot create "unlock Fireball at tier 3 after spending 10 points in Fire tree" gates.

| What Exists | What's Missing |
|-------------|----------------|
| Level-up awards stat points | No talent point currency |
| `AbilityDefinition` buffer (flat list) | No prerequisite tree structure |
| `LevelRewardType.TalentPoint` (=6) | No system consumes this reward type |
| `StatAllocationSystem` (stat points) | No equivalent for talent points |
| Equipment stat bonuses | No passive talent stat bonuses |
| `ProgressionBootstrapSystem` pattern | No talent tree bootstrap |

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  SkillTreeSO              SkillNodeSO              SkillTreeDatabaseSO
  (TreeId, Name,           (NodeId, Tier, Cost,     (List<SkillTreeSO>,
   ClassRestriction,        Prerequisites[],          Resources/SkillTreeDatabase)
   MaxPoints, Nodes[])      NodeType, Reward)

           └────── SkillTreeBootstrapSystem ────────┘
                   (loads from Resources/, builds BlobAssets,
                    creates SkillTreeRegistrySingleton)
                              |
                    ECS DATA LAYER
  TalentState               TalentAllocation         SkillTreeRegistrySingleton
  (on CHILD entity)          (IBufferElementData      (BlobRef to all trees)
   TotalTalentPoints,         on CHILD entity,
   SpentTalentPoints,         NodeId + TreeId)
   ActiveTreeIds)
           |
  Player ──→ TalentLink (8 bytes, Entity ref to child)
                              |
                    SYSTEM PIPELINE (SimulationSystemGroup, Server|Local)
                              |
  TalentPointAwardSystem ── reads LevelUpEvent, grants TalentPoints
  TalentRpcReceiveSystem ── validates allocation RPCs (ServerSimulation)
  TalentAllocationSystem ── spends points, validates prerequisites
  TalentPassiveSystem (Burst) ── sums passive modifiers from unlocked nodes
  TalentAbilityUnlockSystem ── adds/enables AbilityDefinition entries
  TalentRespecSystem ── refunds all points on respec request
                              |
                    [Existing: EquippedStatsSystem → LevelStatScalingSystem]
                              |
                    PRESENTATION LAYER (PresentationSystemGroup)
                              |
  TalentUIBridgeSystem → TalentUIRegistry → ITalentUIProvider
  (managed, reads local player talent state + tree blobs)
```

### Data Flow (Level Up → Talent Point → Allocate → Stat Boost)

```
Frame N (Server):
  1. LevelRewardSystem: Level-up reward includes TalentPoint(count=1)
     → TalentPointAwardSystem: TalentState.TotalTalentPoints += 1

Frame N+K (Client sends allocation):
  2. Player opens talent UI, clicks "Fire Mastery" node
     → Client sends TalentAllocationRpc(TreeId=1, NodeId=5)

Frame N+K+1 (Server):
  3. TalentRpcReceiveSystem: Validates
     - Node exists in tree
     - Player has unspent talent points >= node cost
     - All prerequisites met (check TalentAllocation buffer)
     - Node not already allocated
     → Writes TalentAllocationRequest to player's child entity

  4. TalentAllocationSystem: Processes request
     - Deducts TalentState.SpentTalentPoints += cost
     - Adds TalentAllocation(TreeId, NodeId) to buffer
     - Marks dirty for save system

  5. TalentPassiveSystem (Burst): Reads all TalentAllocation entries
     - For each unlocked passive node: accumulate stat modifiers
     - Writes TalentPassiveStats on child entity
     → TalentStatBridgeSystem copies to player's base stats

  6. TalentAbilityUnlockSystem: Reads TalentAllocation
     - For each unlocked ability node: enable/add AbilityDefinition
```

---

## ECS Components

### On Player Entity (MINIMAL — 8 bytes only)

**File:** `Assets/Scripts/SkillTree/Components/TalentLink.cs`

```csharp
// 8 bytes on player entity — child entity holds all talent data
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct TalentLink : IComponentData
{
    [GhostField] public Entity TalentChild; // Child entity with TalentState + buffers
}
```

### On Talent Child Entity

**File:** `Assets/Scripts/SkillTree/Components/TalentComponents.cs`

```csharp
// Tag to identify talent child entities
public struct TalentChildTag : IComponentData { }

// Back-reference to owning player
public struct TalentOwner : IComponentData
{
    public Entity Owner; // 4 bytes
}

// Core talent state — 16 bytes
public struct TalentState : IComponentData
{
    public int TotalTalentPoints;    // Lifetime talent points earned
    public int SpentTalentPoints;    // Points currently allocated
    public byte ActiveTreeCount;     // How many trees this player has (1-4)
    public byte RespecCount;         // Times respecced (for cost scaling)
    // Padding: 2 bytes
}

// Individual talent allocation — 8 bytes per entry
[InternalBufferCapacity(32)]
public struct TalentAllocation : IBufferElementData
{
    public ushort TreeId;   // Which skill tree
    public ushort NodeId;   // Which node in tree
    public int AllocatedTick; // Server tick when allocated (for ordering)
}

// Pending allocation request — processed by TalentAllocationSystem
[InternalBufferCapacity(2)]
public struct TalentAllocationRequest : IBufferElementData
{
    public ushort TreeId;
    public ushort NodeId;
    public TalentRequestType RequestType; // Allocate or Respec
}

public enum TalentRequestType : byte
{
    Allocate = 0,
    Respec = 1
}

// Computed passive stat bonuses from all unlocked talent nodes — 48 bytes
public struct TalentPassiveStats : IComponentData
{
    public float BonusMaxHealth;
    public float BonusAttackPower;
    public float BonusSpellPower;
    public float BonusDefense;
    public float BonusArmor;
    public float BonusCritChance;
    public float BonusCritDamage;
    public float BonusMovementSpeed;
    public float BonusCooldownReduction;
    public float BonusResourceRegen;
    public float BonusDamagePercent;      // Generic % damage increase
    public float BonusHealingPercent;
}

// Per-tree point tracking — 8 bytes per tree
[InternalBufferCapacity(4)]
public struct TalentTreeProgress : IBufferElementData
{
    public ushort TreeId;
    public ushort PointsSpent;  // Points spent in this specific tree
    public ushort HighestTier;  // Highest tier unlocked (for tier gates)
    public ushort Padding;
}
```

### RPCs

**File:** `Assets/Scripts/SkillTree/Components/TalentRpcs.cs`

```csharp
public struct TalentAllocationRpc : IRpcCommand
{
    public ushort TreeId;
    public ushort NodeId;
}

public struct TalentRespecRpc : IRpcCommand
{
    public ushort TreeId;  // 0 = respec all trees
}
```

### Singleton (BlobAssets)

**File:** `Assets/Scripts/SkillTree/Data/SkillTreeBlobs.cs`

```csharp
public struct SkillTreeRegistrySingleton : IComponentData
{
    public BlobAssetReference<SkillTreeRegistryBlob> Registry;
}

public struct SkillTreeRegistryBlob
{
    public BlobArray<SkillTreeBlob> Trees;
}

public struct SkillTreeBlob
{
    public int TreeId;
    public BlobString Name;
    public int MaxPoints;           // Max points allocable in this tree
    public byte ClassRestriction;   // 0 = any class, >0 = specific class
    public BlobArray<SkillNodeBlob> Nodes;
}

public struct SkillNodeBlob
{
    public int NodeId;
    public int Tier;                // Row/depth in tree (0 = root)
    public int PointCost;           // Talent points required
    public int TierPointsRequired;  // Total points in tree before this tier unlocks
    public SkillNodeType NodeType;
    public int MaxRanks;            // For multi-rank passives (1 = single unlock)

    // Passive bonuses (per rank)
    public SkillPassiveBonus PassiveBonus;

    // Ability unlock
    public int AbilityTypeId;       // 0 = no ability, >0 = unlock this ability

    // Prerequisites (up to 3)
    public int PrereqNodeId0;       // -1 = none
    public int PrereqNodeId1;
    public int PrereqNodeId2;

    // Editor metadata (for Workstation visualization)
    public float EditorX;
    public float EditorY;
}

public struct SkillPassiveBonus
{
    public SkillBonusType BonusType;
    public float Value;             // Flat or percent per rank
}

public enum SkillNodeType : byte
{
    Passive = 0,        // Stat modifier (stackable per rank)
    ActiveAbility = 1,  // Unlocks an AbilityDefinition
    Keystone = 2,       // Major passive (one per tree, mutually exclusive per tier)
    Gateway = 3         // Unlocks next tier (no direct bonus)
}

public enum SkillBonusType : byte
{
    None = 0,
    MaxHealth = 1,
    AttackPower = 2,
    SpellPower = 3,
    Defense = 4,
    Armor = 5,
    CritChance = 6,
    CritDamage = 7,
    MovementSpeed = 8,
    CooldownReduction = 9,
    ResourceRegen = 10,
    DamagePercent = 11,
    HealingPercent = 12,
    ElementalDamage = 13,    // Specific element boost
    StatusDuration = 14,
    LifeSteal = 15,
    DodgeChance = 16,
    BlockChance = 17,
    AttackSpeed = 18
}
```

---

## ScriptableObjects

### SkillTreeSO

**File:** `Assets/Scripts/SkillTree/Definitions/SkillTreeSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Skill Tree/Skill Tree")]
```

| Field | Type | Purpose |
|-------|------|---------|
| TreeId | int | Unique identifier |
| TreeName | string | Display name |
| Description | string | Tree description for UI |
| IconPath | string | Path to tree icon sprite |
| ClassRestriction | byte | 0 = any, >0 = class-specific |
| MaxPoints | int | Max talent points allocable in this tree |
| Nodes | SkillNodeDefinition[] | All nodes in the tree |
| NodeEditorPositions | Vector2[] | Editor node positions (parallel to Nodes[]) |

### SkillNodeDefinition

```
[Serializable]
```

| Field | Type | Purpose |
|-------|------|---------|
| NodeId | int | Unique within tree |
| Name | string | Display name |
| Description | string | Tooltip text |
| IconPath | string | Node icon sprite path |
| Tier | int | Row depth (0 = root) |
| PointCost | int | Talent points per rank |
| TierPointsRequired | int | Total tree points before this tier unlocks |
| MaxRanks | int | Multi-rank passives (1 = single) |
| NodeType | SkillNodeType | Passive/ActiveAbility/Keystone/Gateway |
| Prerequisites | int[] | NodeIds that must be allocated first |
| BonusType | SkillBonusType | Which stat to modify |
| BonusValue | float | Amount per rank |
| AbilityTypeId | int | Ability to unlock (ActiveAbility nodes only) |

### SkillTreeDatabaseSO

**File:** `Assets/Scripts/SkillTree/Definitions/SkillTreeDatabaseSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Skill Tree/Skill Tree Database")]
```

| Field | Type | Purpose |
|-------|------|---------|
| Trees | List\<SkillTreeSO\> | All skill trees |
| TalentPointsPerLevel | int | Base talent points per level-up (default 1) |
| RespecBaseCost | int | Gold cost for first respec |
| RespecCostMultiplier | float | Cost multiplier per subsequent respec |
| RespecCostCap | int | Maximum respec cost |

### SkillTreeConfigSO

**File:** `Assets/Scripts/SkillTree/Definitions/SkillTreeConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Skill Tree/Config")]
```

| Field | Type | Purpose |
|-------|------|---------|
| MaxTreesPerPlayer | int | Max simultaneous trees (default 3) |
| MaxTotalTalentPoints | int | Lifetime cap (default 60 at level 50) |
| AllowRespec | bool | Enable/disable respec (default true) |
| PreviewUnlockedAbilities | bool | Show locked abilities grayed in action bar |

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Client|Local):
  SkillTreeBootstrapSystem              — loads SOs, builds BlobAssets, creates singleton (runs once)

SimulationSystemGroup (Server|Local):
  TalentPointAwardSystem                — reads LevelUpEvent, grants TalentPoints via TalentState
  TalentRpcReceiveSystem (ServerOnly)   — validates TalentAllocationRpc/TalentRespecRpc
  TalentAllocationSystem                — processes TalentAllocationRequest buffer
  TalentRespecSystem                    — refunds points, clears allocations
  TalentPassiveSystem (Burst)           — sums passive bonuses from all allocations
  TalentAbilityUnlockSystem             — enables/disables AbilityDefinition entries
  TalentStatBridgeSystem                — copies TalentPassiveStats to player base stats
  [existing] EquippedStatsSystem        — adds equipment bonuses (unchanged)
  [existing] LevelStatScalingSystem     — recalculates with new base (unchanged)

PresentationSystemGroup (Client|Local):
  TalentUIBridgeSystem                  — managed, reads talent state + tree blobs for UI
```

### TalentPointAwardSystem

**File:** `Assets/Scripts/SkillTree/Systems/TalentPointAwardSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- Reads `LevelUpEvent` (IEnableableComponent) on player entities
- Looks up `TalentLink.TalentChild` → writes `TalentState.TotalTalentPoints += pointsPerLevel`
- Also handles `LevelRewardType.TalentPoint` awards from `LevelRewardSystem`

### TalentRpcReceiveSystem

**File:** `Assets/Scripts/SkillTree/Systems/TalentRpcReceiveSystem.cs`

- `[WorldSystemFilter(ServerSimulation)]`
- Receives `TalentAllocationRpc`, resolves `ReceiveRpcCommandRequest.SourceConnection → CommandTarget → player`
- Validates: tree exists, node exists, player has points, prerequisites met, not already allocated
- Writes `TalentAllocationRequest` to talent child entity's buffer
- Same pattern as `StatAllocationRpcReceiveSystem`

### TalentAllocationSystem

**File:** `Assets/Scripts/SkillTree/Systems/TalentAllocationSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- Processes `TalentAllocationRequest` buffer entries
- For each valid request:
  - Deducts from `TalentState` (TotalTalentPoints - SpentTalentPoints = available)
  - Appends `TalentAllocation(TreeId, NodeId, tick)` to buffer
  - Updates `TalentTreeProgress` for the tree
  - Marks save dirty

### TalentPassiveSystem (Burst)

**File:** `Assets/Scripts/SkillTree/Systems/TalentPassiveSystem.cs`

- `[BurstCompile]`, `ISystem`
- Iterates talent child entities with `TalentAllocation` buffer + `TalentPassiveStats`
- For each allocation: look up node in `SkillTreeRegistryBlob`, accumulate `SkillPassiveBonus`
- Writes final `TalentPassiveStats` (sum of all passive bonuses)
- Only recalculates when allocation buffer changes (dirty flag)

### TalentStatBridgeSystem

**File:** `Assets/Scripts/SkillTree/Systems/TalentStatBridgeSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- Reads `TalentPassiveStats` from child entity via `TalentLink`
- Adds talent bonuses to `AttackStats`, `DefenseStats`, `Health.Max` on player entity
- Runs BEFORE `EquippedStatsSystem` so equipment bonuses stack on top

### TalentAbilityUnlockSystem

**File:** `Assets/Scripts/SkillTree/Systems/TalentAbilityUnlockSystem.cs`

- Reads `TalentAllocation` buffer, cross-references with tree blob
- For `ActiveAbility` nodes: ensures `AbilityDefinition` with matching `AbilityTypeId` is in player's buffer
- Uses `IEnableableComponent` on `AbilityDefinition` if available, otherwise adds to buffer

### TalentRespecSystem

**File:** `Assets/Scripts/SkillTree/Systems/TalentRespecSystem.cs`

- Processes `TalentAllocationRequest` with `RequestType == Respec`
- Validates gold cost: `RespecBaseCost * RespecCostMultiplier^RespecCount`
- Clears `TalentAllocation` buffer (or specific tree if TreeId != 0)
- Refunds: `SpentTalentPoints = 0` (full respec) or per-tree
- Increments `TalentState.RespecCount`
- `TalentPassiveSystem` automatically recalculates next frame (zeros out removed bonuses)
- `TalentAbilityUnlockSystem` removes unlocked abilities

---

## Authoring

**File:** `Assets/Scripts/SkillTree/Authoring/TalentAuthoring.cs`

```
[AddComponentMenu("DIG/Skill Tree/Player Talent")]
```

- Place on player prefab alongside `ProgressionAuthoring`
- Baker creates child entity with: `TalentChildTag`, `TalentOwner`, `TalentState`, `TalentAllocation` buffer, `TalentAllocationRequest` buffer, `TalentTreeProgress` buffer, `TalentPassiveStats`
- Baker adds `TalentLink` to parent player entity (8 bytes)
- Follows `SaveStateLink` + `CraftingKnowledgeLink` child entity pattern

---

## UI Bridge

**File:** `Assets/Scripts/SkillTree/Bridges/TalentUIBridgeSystem.cs`

- Managed SystemBase, PresentationSystemGroup, Client|Local
- Reads local player `TalentLink → TalentChild → TalentState + TalentAllocation`
- Reads `SkillTreeRegistrySingleton` blob for tree structure
- Pushes to `TalentUIRegistry` → `ITalentUIProvider`

**File:** `Assets/Scripts/SkillTree/Bridges/TalentUIRegistry.cs`

Static registry (same pattern as `CombatUIRegistry`):
- `ITalentUIProvider`: `OpenTalentTree(TalentTreeUIState)`, `UpdateNodeStates(NodeStateUI[])`, `CloseTalentTree()`, `ShowRespecConfirm(cost)`
- `HasTalentUI` / `TalentUI` properties

**File:** `Assets/Scripts/SkillTree/Bridges/TalentUIState.cs`

```csharp
public struct TalentTreeUIState
{
    public int TreeId;
    public string TreeName;
    public int AvailablePoints;
    public int SpentInTree;
    public TalentNodeUIState[] Nodes;
}

public struct TalentNodeUIState
{
    public int NodeId;
    public string Name;
    public string Description;
    public string IconPath;
    public int Tier;
    public int CurrentRank;
    public int MaxRanks;
    public int PointCost;
    public SkillNodeType NodeType;
    public TalentNodeStatus Status; // Locked, Available, Allocated, Maxed
    public int[] PrerequisiteNodeIds;
    public string BonusText; // "Attack Power +5%"
}

public enum TalentNodeStatus : byte
{
    Locked = 0,      // Prerequisites not met or tier not unlocked
    Available = 1,   // Can allocate (has points + prerequisites met)
    Allocated = 2,   // At least 1 rank allocated
    Maxed = 3        // All ranks allocated
}
```

---

## Save Integration

**File:** `Assets/Scripts/Persistence/Modules/TalentSaveModule.cs`

```
ISaveModule implementation:
  TypeId = 11
  DisplayName = "Talents"
  ModuleVersion = 1
```

Serializes:
- `TalentState` (TotalTalentPoints, SpentTalentPoints, RespecCount)
- `TalentAllocation` buffer (TreeId + NodeId pairs)
- `TalentTreeProgress` buffer

Deserialize restores allocations → `TalentPassiveSystem` + `TalentAbilityUnlockSystem` recalculate automatically.

---

## Editor Tooling

### SkillTreeWorkstationWindow

**File:** `Assets/Editor/SkillTreeWorkstation/SkillTreeWorkstationWindow.cs`

- Menu: `DIG/Skill Tree Workstation`
- Sidebar + `ISkillTreeWorkstationModule` pattern (matches ProgressionWorkstation)

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Tree Editor | `Modules/TreeEditorModule.cs` | Visual node graph: colored rects by NodeType (Passive=blue, Active=green, Keystone=gold, Gateway=gray), Bezier prerequisite lines, right-click context menu (Add/Delete/Duplicate node), drag to reposition. Zoom+pan. Saves to SkillTreeSO.NodeEditorPositions |
| Node Inspector | `Modules/NodeInspectorModule.cs` | Edit selected node: type, tier, cost, prerequisites, bonus type/value, ability link. SerializedObject editing of SkillNodeDefinition |
| Player Inspector | `Modules/PlayerInspectorModule.cs` | Play-mode: live talent state, allocated nodes per tree, passive bonus totals, ability unlocks |
| Simulator | `Modules/SimulatorModule.cs` | "Grant Talent Points" button, "Allocate Node" dropdown, "Respec" button. Stat impact preview before/after allocation |
| Validator | `Modules/ValidatorModule.cs` | 8 validation checks: orphan nodes, circular prerequisites, unreachable tiers, duplicate NodeIds, missing ability links, tier point gaps, keystone conflicts, zero-cost nodes |
| Balance Analyzer | `Modules/BalanceAnalyzerModule.cs` | DPS/EHP curves for different tree paths, optimal path calculation, point efficiency analysis |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `TalentLink` | 8 bytes | Player entity |
| **Total on player** | **8 bytes** | |

All talent data lives on the child entity (TalentState=16, TalentAllocation buffer, TalentPassiveStats=48, etc.). Same pattern as `SaveStateLink` (8 bytes) and `CraftingKnowledgeLink`.

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `SkillTreeBootstrapSystem` | N/A | No | Runs once at startup |
| `TalentPointAwardSystem` | < 0.01ms | No | Only on level-up (rare) |
| `TalentRpcReceiveSystem` | < 0.01ms | No | Only on player input |
| `TalentAllocationSystem` | < 0.01ms | No | Only on player input |
| `TalentPassiveSystem` | < 0.02ms | Yes | Iterates allocations, sums bonuses. Dirty-flag skip |
| `TalentAbilityUnlockSystem` | < 0.01ms | No | Only after allocation change |
| `TalentStatBridgeSystem` | < 0.01ms | No | Single entity, 12 float copies |
| `TalentRespecSystem` | < 0.01ms | No | Very rare |
| `TalentUIBridgeSystem` | < 0.03ms | No | Managed, only when UI open |
| **Total** | **< 0.10ms** | | |

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| Entity without TalentLink | No talent data | Zero overhead — systems skip |
| Empty SkillTreeDatabase | No trees loaded | TalentPointAwardSystem still awards points (banked for future trees) |
| No ITalentUIProvider registered | Warning at frame 120 | Systems run, UI just doesn't display |

---

## File Summary

### New Files (28)

| # | Path | Type | Phase |
|---|------|------|-------|
| 1 | `Assets/Scripts/SkillTree/Components/TalentLink.cs` | IComponentData | 0 |
| 2 | `Assets/Scripts/SkillTree/Components/TalentComponents.cs` | IComponentData + Buffers | 0 |
| 3 | `Assets/Scripts/SkillTree/Components/TalentRpcs.cs` | IRpcCommand | 0 |
| 4 | `Assets/Scripts/SkillTree/Data/SkillTreeBlobs.cs` | BlobAsset structs | 0 |
| 5 | `Assets/Scripts/SkillTree/Definitions/SkillTreeSO.cs` | ScriptableObject | 0 |
| 6 | `Assets/Scripts/SkillTree/Definitions/SkillTreeDatabaseSO.cs` | ScriptableObject | 0 |
| 7 | `Assets/Scripts/SkillTree/Definitions/SkillTreeConfigSO.cs` | ScriptableObject | 0 |
| 8 | `Assets/Scripts/SkillTree/Definitions/SkillNodeDefinition.cs` | Serializable struct | 0 |
| 9 | `Assets/Scripts/SkillTree/Systems/SkillTreeBootstrapSystem.cs` | SystemBase | 1 |
| 10 | `Assets/Scripts/SkillTree/Systems/TalentPointAwardSystem.cs` | SystemBase | 1 |
| 11 | `Assets/Scripts/SkillTree/Systems/TalentRpcReceiveSystem.cs` | SystemBase (Server) | 1 |
| 12 | `Assets/Scripts/SkillTree/Systems/TalentAllocationSystem.cs` | SystemBase | 1 |
| 13 | `Assets/Scripts/SkillTree/Systems/TalentPassiveSystem.cs` | ISystem (Burst) | 1 |
| 14 | `Assets/Scripts/SkillTree/Systems/TalentAbilityUnlockSystem.cs` | SystemBase | 1 |
| 15 | `Assets/Scripts/SkillTree/Systems/TalentStatBridgeSystem.cs` | SystemBase | 1 |
| 16 | `Assets/Scripts/SkillTree/Systems/TalentRespecSystem.cs` | SystemBase | 1 |
| 17 | `Assets/Scripts/SkillTree/Authoring/TalentAuthoring.cs` | Baker | 2 |
| 18 | `Assets/Scripts/SkillTree/Bridges/TalentUIBridgeSystem.cs` | SystemBase | 3 |
| 19 | `Assets/Scripts/SkillTree/Bridges/TalentUIRegistry.cs` | Static class | 3 |
| 20 | `Assets/Scripts/SkillTree/Bridges/TalentUIState.cs` | Structs | 3 |
| 21 | `Assets/Scripts/SkillTree/Bridges/ITalentUIProvider.cs` | Interface | 3 |
| 22 | `Assets/Scripts/Persistence/Modules/TalentSaveModule.cs` | ISaveModule | 4 |
| 23 | `Assets/Scripts/SkillTree/DIG.SkillTree.asmdef` | Assembly def | 0 |
| 24 | `Assets/Editor/SkillTreeWorkstation/SkillTreeWorkstationWindow.cs` | EditorWindow | 5 |
| 25 | `Assets/Editor/SkillTreeWorkstation/ISkillTreeWorkstationModule.cs` | Interface | 5 |
| 26 | `Assets/Editor/SkillTreeWorkstation/Modules/TreeEditorModule.cs` | Module | 5 |
| 27 | `Assets/Editor/SkillTreeWorkstation/Modules/NodeInspectorModule.cs` | Module | 5 |
| 28 | `Assets/Editor/SkillTreeWorkstation/Modules/ValidatorModule.cs` | Module | 5 |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Progression/Systems/LevelRewardSystem.cs` | Handle `LevelRewardType.TalentPoint` → call `TalentPointAwardSystem` |
| 2 | Player prefab (Warrok_Server) | Add `TalentAuthoring` |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/SkillTreeDatabase.asset` |
| 2 | `Resources/SkillTreeConfig.asset` |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `LevelRewardSystem` | 16.14 | Awards talent points via TalentPointAwardSystem |
| `StatAllocationSystem` | 16.14 | Parallel system for stat points (talent points are separate currency) |
| `EquippedStatsSystem` | 16.6 | Equipment bonuses stack with talent passive bonuses |
| `LevelStatScalingSystem` | 16.14 | Level base stats + talent bonuses + equipment = final stats |
| `AbilityDefinition` buffer | 16.14 | Talent nodes can unlock abilities |
| `CurrencyInventory.Gold` | 16.6 | Respec costs gold |
| `ProgressionSaveModule` | 16.15 | TalentSaveModule (TypeId=11) adds talent persistence |
| `DialogueConditionType` | 16.16 | Future: could add HasTalent condition type |
| `QuestRewardSystem` | 16.12 | Future: quest rewards could grant bonus talent points |

---

## Verification Checklist

### Core Pipeline
- [ ] Level up awards talent points (TalentState.TotalTalentPoints increments)
- [ ] Talent UI shows available trees and nodes
- [ ] Click available node → point deducted, node allocated
- [ ] Click locked node (missing prerequisite) → rejected
- [ ] Click node with insufficient points → rejected
- [ ] Already-allocated node → rejected (or increments rank for multi-rank)
- [ ] Multi-rank passive: allocate 3/5 ranks, tooltip shows current bonus

### Prerequisite Validation
- [ ] Node with 2 prerequisites: both must be allocated before available
- [ ] Tier gate: "Spend 10 points in tree" required before tier 3 nodes
- [ ] Keystone: only 1 keystone per tree allocable
- [ ] Gateway node: unlocks next tier row

### Stat Integration
- [ ] Passive "Attack Power +5" shows in combat stats
- [ ] Multiple passives stack additively
- [ ] Talent bonuses + equipment bonuses = correct total
- [ ] Talent bonuses + level scaling = correct total

### Ability Unlocks
- [ ] ActiveAbility talent node: ability appears in action bar
- [ ] Ability works in combat after unlock
- [ ] Respec removes ability from action bar

### Respec
- [ ] Respec refunds all points in tree
- [ ] Respec costs gold (escalating cost)
- [ ] After respec: passive bonuses removed, abilities removed
- [ ] Re-allocate after respec works correctly

### Multiplayer
- [ ] Talent allocation server-authoritative (RPC validated)
- [ ] Remote clients don't see talent details (not ghost-replicated)
- [ ] Stat changes from talents replicate via AttackStats/DefenseStats (Ghost:All)

### Persistence
- [ ] Save: talent allocations persisted
- [ ] Load: allocations restored, passives recalculated
- [ ] Version migration: old saves without talents load cleanly

### Editor
- [ ] Workstation: tree editor shows node graph
- [ ] Workstation: drag nodes to reposition
- [ ] Workstation: validator catches circular prerequisites
- [ ] Workstation: play-mode inspector shows live talent state
