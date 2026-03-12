# EPIC 17.1: Skill Trees & Talent System — Setup Guide

This guide covers Unity Editor setup for designers and developers working with the Skill Trees & Talent System.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Player Prefab Setup](#2-player-prefab-setup)
3. [Creating the Skill Tree Database](#3-creating-the-skill-tree-database)
4. [Creating the Config Asset](#4-creating-the-config-asset)
5. [Designing Trees with the Workstation](#5-designing-trees-with-the-workstation)
6. [Configuring Level-Up Talent Rewards](#6-configuring-level-up-talent-rewards)
7. [Implementing the UI Provider](#7-implementing-the-ui-provider)
8. [Respec Cost Configuration](#8-respec-cost-configuration)
9. [Save/Load Integration](#9-saveload-integration)
10. [Validation & Testing](#10-validation--testing)
11. [Troubleshooting](#11-troubleshooting)

---

## 1. Prerequisites

Before setting up skill trees, ensure these systems are already working:

- **EPIC 16.14 (Progression & XP)** — Level-up events drive talent point awards
- **EPIC 16.15 (Save/Load)** — Talent allocations are persisted via TalentSaveModule (TypeId=11)
- **Economy system** — Respec costs deduct gold via `CurrencyTransaction`

The player prefab must already have:
- `CharacterAttributes` (for level)
- `PlayerProgression` (for XP/level-up)
- `LevelUpEvent` (IEnableableComponent, baked disabled)

---

## 2. Player Prefab Setup

### Adding TalentAuthoring to the Player Prefab

1. Open the player ghost prefab (e.g., `Warrok_Server`) in the Inspector
2. Click **Add Component** and search for **TalentAuthoring**
3. No fields need configuration — the baker automatically:
   - Creates a child entity with all talent data components
   - Adds a `TalentLink` (8 bytes) on the player entity pointing to the child
   - The child entity pattern avoids the 16KB archetype limit

> **Important**: After adding TalentAuthoring, **reimport the subscene** containing the player prefab to regenerate ghost serialization data.

### What Gets Baked

The baker creates a child entity with:
- `TalentState` — tracks total/spent points, respec count
- `TalentPassiveStats` — accumulated passive bonuses from allocations
- `TalentAllocation` buffer (capacity 32) — individual point allocations
- `TalentAllocationRequest` buffer (capacity 2) — pending allocation/respec requests
- `TalentTreeProgress` buffer (capacity 4) — per-tree spending summary

No additional components are added to the player entity beyond the 8-byte `TalentLink`.

---

## 3. Creating the Skill Tree Database

The `SkillTreeDatabaseSO` is the root asset that defines all skill trees.

### Creating via Skill Tree Workstation (Recommended)

1. Open **DIG > Skill Tree Workstation** from the menu bar
2. If no database exists, click **Create Database**
3. The asset is created at `Assets/Resources/SkillTreeDatabase.asset`

### Creating Manually

1. Right-click in `Assets/Resources/`
2. Select **Create > DIG > Skill Tree Database**
3. Name it exactly `SkillTreeDatabase` (the bootstrap system loads from `Resources/SkillTreeDatabase`)

### Database Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Trees** | List of `SkillTreeSO` references | Empty |
| **Talent Points Per Level** | Points granted per level-up (via LevelRewardSystem) | 1 |
| **Respec Base Cost** | Gold cost for first respec | 100 |
| **Respec Cost Multiplier** | Multiplier applied per previous respec | 1.5 |
| **Respec Cost Cap** | Maximum gold cost for a respec | 10000 |

### Creating Individual Skill Trees

1. Right-click in Project window
2. Select **Create > DIG > Skill Tree**
3. Configure the tree's properties:

| Field | Description |
|-------|-------------|
| **Tree Id** | Unique integer identifier (must be unique across all trees) |
| **Tree Name** | Display name shown in UI |
| **Description** | Tooltip/flavor text |
| **Icon Path** | Path to tree icon sprite |
| **Class Restriction** | 0 = available to all classes, >0 = restricted to specific class ID |
| **Max Points** | Maximum talent points spendable in this tree |
| **Nodes** | Array of node definitions (edit via Workstation for visual graph) |

4. Drag the SkillTreeSO into the database's **Trees** list

---

## 4. Creating the Config Asset

1. Right-click in `Assets/Resources/`
2. Select **Create > DIG > Skill Tree Config**
3. Name it exactly `SkillTreeConfig`

### Config Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Max Trees Per Player** | Maximum number of trees a player can have active | 3 |
| **Max Total Talent Points** | Hard cap on lifetime talent points | 100 |
| **Allow Respec** | Whether respec is available | true |
| **Preview Unlocked Abilities** | Show locked ability previews in UI | true |

> If no config asset exists, the bootstrap system creates defaults automatically.

---

## 5. Designing Trees with the Workstation

### Opening the Workstation

Menu: **DIG > Skill Tree Workstation**

### Tabs

#### Tree Editor
Visual node graph showing all nodes in the selected tree.

**Controls:**
- **Left-click** on a node to select it
- **Left-click + drag** to move a node
- **Middle mouse + drag** to pan the canvas
- **Scroll wheel** to zoom in/out
- **Right-click** to open context menu (add/delete nodes)
- **"+ Node"** toolbar button to add a node at default position

**Node Colors:**
- Blue = Passive
- Orange = Active Ability
- Purple = Keystone
- Green = Gateway

**Bezier lines** show prerequisite connections between nodes.

#### Node Inspector
Detailed property editor for the selected node. Fields:

| Field | Description |
|-------|-------------|
| **Node Id** | Unique ID within the tree |
| **Tier** | Talent tier (0 = entry-level) |
| **Node Type** | Passive, ActiveAbility, Keystone, or Gateway |
| **Point Cost** | Talent points per rank |
| **Max Ranks** | Maximum times this node can be allocated |
| **Tier Points Required** | Total points spent in tree to unlock this tier |
| **Bonus Type** | Which stat this passive modifies (AttackPower, CritChance, etc.) |
| **Bonus Value** | Amount added per rank |
| **Ability Type Id** | For ActiveAbility nodes: which ability to unlock |
| **Prereq 1/2/3** | Node IDs that must be allocated before this one (-1 = none) |
| **Editor X/Y** | Canvas position (updated by dragging in Tree Editor) |

#### Player Inspector (Play Mode Only)
Shows live ECS data for all players:
- Talent state (total/spent/available points, respec count)
- All allocations (tree ID, node ID, tick)
- Passive stat bonuses being applied
- Per-tree progress summary

#### Validator
Runs 8 automated checks on the database:

1. **Empty Trees** — Trees with no nodes
2. **Duplicate Tree IDs** — Multiple trees sharing the same ID
3. **Duplicate Node IDs** — Nodes within a tree sharing the same ID
4. **Missing Prereq Targets** — Prerequisites referencing non-existent nodes
5. **Circular Prerequisites** — DFS cycle detection in prerequisite chains
6. **Orphan Nodes** — Non-tier-0 nodes with no prerequisites and not referenced
7. **Tier Gaps** — Missing tiers (e.g., tier 0 and tier 3 but no tier 1 or 2)
8. **Cost/Rank Validation** — PointCost <= 0, MaxRanks <= 0, negative tier requirements
9. **Keystone Limits** — More than 1 keystone per tier (warning)

Click **"Run All Checks"** to validate. Results show as green (pass), yellow (warning), or red (error).

---

## 6. Configuring Level-Up Talent Rewards

Talent points are awarded through the existing `LevelRewardsSO` system.

### Adding Talent Point Rewards

1. Open your `LevelRewardsSO` asset (in `Assets/Resources/` or wherever configured)
2. Add entries with:
   - **Reward Type**: `TalentPoint`
   - **Level**: The level at which the reward is granted
   - **Int Value**: Number of talent points to award

### Example Configuration

| Level | Reward Type | Int Value |
|-------|-------------|-----------|
| 2 | TalentPoint | 1 |
| 3 | TalentPoint | 1 |
| 4 | TalentPoint | 1 |
| 5 | TalentPoint | 2 |
| 10 | TalentPoint | 2 |

The `LevelRewardSystem` reads `TalentLink` on the player entity, resolves the child, and increments `TalentState.TotalTalentPoints`.

---

## 7. Implementing the UI Provider

The talent UI follows the same adapter pattern as combat/progression UI.

### Steps

1. Create a MonoBehaviour that implements `ITalentUIProvider`:

```csharp
public class TalentTreePanel : MonoBehaviour, DIG.SkillTree.ITalentUIProvider
{
    public void OpenTalentTree(DIG.SkillTree.TalentTreeUIState state) { /* Show panel, populate nodes */ }
    public void UpdateNodeStates(DIG.SkillTree.TalentTreeUIState state) { /* Refresh node visuals */ }
    public void CloseTalentTree() { /* Hide panel */ }
    public void ShowRespecConfirm(int goldCost, int treeId) { /* Show respec confirmation dialog */ }
}
```

2. Register in `OnEnable` / unregister in `OnDisable`:

```csharp
void OnEnable() => DIG.SkillTree.TalentUIRegistry.RegisterTalentUI(this);
void OnDisable() => DIG.SkillTree.TalentUIRegistry.UnregisterTalentUI(this);
```

3. Place the MonoBehaviour on an active GameObject in the UI scene

### TalentTreeUIState Data

The `UpdateNodeStates` callback receives:

| Field | Type | Description |
|-------|------|-------------|
| TreeId | int | Tree identifier |
| TreeName | string | Display name |
| AvailablePoints | int | Unspent talent points |
| SpentInTree | int | Points spent in this specific tree |
| TotalSpent | int | Points spent across all trees |
| Nodes | TalentNodeUIState[] | Per-node state array |

Each `TalentNodeUIState` includes:

| Field | Description |
|-------|-------------|
| NodeId | Unique node identifier |
| Status | Locked, Available, Allocated, or Maxed |
| CurrentRank / MaxRanks | Current and maximum allocation count |
| PointCost | Cost per rank |
| NodeType | Passive, ActiveAbility, Keystone, Gateway |
| PrerequisiteNodeIds | Array of prerequisite node IDs |
| BonusText | Formatted bonus description |
| EditorX / EditorY | Position for visual layout |

### Sending Allocation Requests

To allocate a talent point from UI, create and send a `TalentAllocationRpc`:

```csharp
// In your UI click handler:
var rpcEntity = EntityManager.CreateEntity();
EntityManager.AddComponentData(rpcEntity, new DIG.SkillTree.TalentAllocationRpc
{
    TreeId = selectedTreeId,
    NodeId = selectedNodeId
});
EntityManager.AddComponent<Unity.NetCode.SendRpcCommandRequest>(rpcEntity);
```

For respec, use `TalentRespecRpc`:

```csharp
var rpcEntity = EntityManager.CreateEntity();
EntityManager.AddComponentData(rpcEntity, new DIG.SkillTree.TalentRespecRpc
{
    TreeId = treeIdToRespec  // 0 = full respec of all trees
});
EntityManager.AddComponent<Unity.NetCode.SendRpcCommandRequest>(rpcEntity);
```

---

## 8. Respec Cost Configuration

Respec costs are configured on the `SkillTreeDatabaseSO`:

**Formula**: `Cost = RespecBaseCost * (RespecCostMultiplier ^ RespecCount)`

Capped at `RespecCostCap`.

| Respec # | Base=100, Mult=1.5, Cap=10000 |
|----------|-------------------------------|
| 1st | 100 gold |
| 2nd | 150 gold |
| 3rd | 225 gold |
| 4th | 337 gold |
| 10th | 3,844 gold |

**Full Respec** (TreeId=0): Clears all allocations across all trees, refunds all spent points.

**Per-Tree Respec** (TreeId=N): Clears only the specified tree's allocations, refunds that tree's spent points.

---

## 9. Save/Load Integration

Talent data is automatically saved/loaded by `TalentSaveModule` (TypeId=11). No additional setup is required beyond the standard persistence configuration.

### What Gets Saved

- `TalentState`: TotalTalentPoints, SpentTalentPoints, RespecCount
- `TalentAllocation` buffer: All individual TreeId/NodeId allocation pairs
- `TalentTreeProgress` buffer: Per-tree PointsSpent and HighestTier

### Verification

1. Enter play mode
2. Allocate talent points
3. Save the game
4. Reload — allocations should be restored and passive stats recalculated

---

## 10. Validation & Testing

### Quick Smoke Test

1. Add `TalentAuthoring` to player prefab
2. Create `SkillTreeDatabase` and `SkillTreeConfig` in `Assets/Resources/`
3. Create at least one `SkillTreeSO` with 3+ nodes
4. Add `LevelRewardType.TalentPoint` entries to `LevelRewardsSO`
5. Enter Play Mode

**Expected console output:**
```
[SkillTreeBootstrap] Loaded N trees, M total nodes
```

### Play Mode Verification Checklist

- [ ] Bootstrap log appears with correct tree/node counts
- [ ] Level up awards talent points (check Player Inspector tab)
- [ ] Sending `TalentAllocationRpc` allocates the node
- [ ] Passive stats update in AttackStats/DefenseStats after allocation
- [ ] Prerequisites block allocation when not met
- [ ] Tier point requirements block nodes correctly
- [ ] Respec clears allocations and refunds points
- [ ] Respec deducts gold
- [ ] Save/load preserves allocations
- [ ] Validator reports no errors on your tree data
- [ ] Keystone nodes enforce mutual exclusivity per tree

### Common Validation Issues

Run **DIG > Skill Tree Workstation > Validator** before testing. Fix all errors before entering play mode.

---

## 11. Troubleshooting

### "No SkillTreeDatabase found" warning at startup
- Ensure the asset is named `SkillTreeDatabase` and located in `Assets/Resources/`
- Check it's a `SkillTreeDatabaseSO` type, not a generic ScriptableObject

### Talent points not awarded on level up
- Verify `LevelRewardsSO` has entries with `RewardType = TalentPoint`
- Check that `TalentAuthoring` is on the player prefab
- Confirm the subscene was reimported after adding TalentAuthoring

### Allocations rejected silently
- Check console for validation messages from `TalentAllocationSystem`
- Verify the node exists in the blob (correct TreeId/NodeId)
- Ensure prerequisites are met and tier point requirements satisfied
- Ensure the player has available talent points

### Passive stats not applying
- Confirm `TalentStatBridgeSystem` is running (it logs on first application)
- Check that the node's `BonusType` is not `None`
- Verify `TalentPassiveSystem` is calculating non-zero `TalentPassiveStats`
- Use the Player Inspector tab in the Workstation to verify passive stat values

### Respec fails
- Verify `AllowRespec = true` in SkillTreeConfig
- Check the player has enough gold (respec cost formula above)
- Confirm `CurrencyTransaction` buffer exists on the player entity

### "BlobAssetReference is null" crash
- This is the 16KB archetype limit. `TalentAuthoring` uses a child entity specifically to avoid this. If you added other components to the player entity recently, check total archetype size

### Ghost serialization errors after adding TalentAuthoring
- Reimport the subscene containing the player prefab
- `TalentLink` has `[GhostComponent(PrefabType = AllPredicted)]` — this is correct for player-only data
