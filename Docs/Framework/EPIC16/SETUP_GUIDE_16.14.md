# SETUP GUIDE 16.14: Progression & XP System

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Combat Stats (CharacterAttributes), Loot & Economy (EPIC 16.6), Quest System (EPIC 16.12), Crafting System (EPIC 16.13)

This guide covers Unity Editor setup for the player progression and XP system. After setup, players earn XP from kills, quests, and crafting, level up with stat scaling, and spend stat points on attributes.

---

## What Changed

Previously, `CharacterAttributes.Level` existed on every player entity but was always 1. Kill XP, leveling, and stat scaling had no implementation.

Now:

- **XP from kills** — enemy kills award XP based on enemy level, with diminishing returns for outleveling
- **XP from quests** — quest completion rewards XP through QuestRewardSystem
- **XP from crafting** — recipe completion grants XP scaled by recipe tier
- **Level-up** — XP thresholds from a designer-configurable curve, with multi-level carry-over
- **Stat scaling** — AttackStats, DefenseStats, Health.Max, and ResourcePoolBase recalculated each level
- **Stat allocation** — players spend earned stat points on Strength, Dexterity, Intelligence, Vitality
- **Rested XP** — bonus XP pool that depletes as kills are awarded
- **Level rewards** — gold, recipe unlocks, bonus stat points, etc. per level threshold
- **XP bonus from gear** — equipment with XPBonusPercent increases all XP gains
- **Progression Workstation** — editor window with player inspector, XP curve visualization, and XP simulator

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Blob config bootstrap | Loads 3 ScriptableObjects from `Resources/` on initialization, builds BlobAssets |
| Kill XP awards | XPAwardSystem reads KillCredited (set by DeathTransitionSystem), computes XP with diminishing returns |
| Level-up detection | LevelUpSystem loops XP against threshold, increments CharacterAttributes.Level, awards stat points |
| Stat scaling | LevelStatScalingSystem writes AttackStats, DefenseStats, Health.Max, ResourcePoolBase from level + gear |
| Health preservation | On level-up, Health.Current is preserved as a proportion of Max (health bar stays proportionally full) |
| Quest XP | QuestRewardSystem calls XPGrantAPI for Experience rewards (EPIC 16.12 integration) |
| Craft XP | CraftOutputGenerationSystem grants tier-scaled XP on craft completion (EPIC 16.13 integration) |
| Loot level gating | DeathLootSystem reads killer's CharacterAttributes.Level for loot table MinLevel/MaxLevel (EPIC 16.6 integration) |
| Equipment XP bonus | EquippedStatsSystem aggregates ItemStatBlock.XPBonusPercent into PlayerEquippedStats.TotalXPBonusPercent |
| Visual queue | XP gain and level-up events are enqueued to LevelUpVisualQueue for UI consumption |

---

## 1. ScriptableObject Configuration

The progression system requires **three** ScriptableObject assets placed in `Assets/Resources/`. The bootstrap system loads them automatically on game start.

### 1.1 Progression Curve

Controls XP thresholds, kill formula, diminishing returns, and rested XP.

1. Right-click in Project window > **Create > DIG > Progression > Progression Curve**
2. Name it `ProgressionCurve`
3. Place at `Assets/Resources/ProgressionCurve.asset`

#### Level Caps

| Field | Default | Description |
|-------|---------|-------------|
| **Max Level** | 50 | Maximum player level |
| **Stat Points Per Level** | 3 | Points awarded per level-up |

#### XP Per Level

The **XP Per Level** array defines explicit XP thresholds. Index 0 = XP needed for level 1 to 2, index 1 = level 2 to 3, etc.

If the array is empty or shorter than Max Level, remaining levels use the geometric fallback formula:

| Field | Default | Description |
|-------|---------|-------------|
| **Geometric Base XP** | 100 | XP for level 1→2 when not explicitly defined |
| **Geometric Multiplier** | 1.12 | Each level requires this multiple of the previous |

> Tip: Use the **XP Curve** tab in the Progression Workstation to visualize the curve and adjust values visually.

#### Kill XP Formula

| Field | Default | Description |
|-------|---------|-------------|
| **Base Kill XP** | 100 | XP for killing a level 1 enemy |
| **Kill XP Per Enemy Level** | 1.15 | Multiplier per enemy level: `rawXP = BaseKillXP * pow(this, enemyLevel - 1)` |

#### Diminishing Returns

Kicks in when the player outlevel enemies by more than the start delta:

| Field | Default | Description |
|-------|---------|-------------|
| **Diminish Start Delta** | 3 | Level gap before diminishing applies |
| **Diminish Factor Per Level** | 0.8 | XP multiplier reduction per level beyond threshold |
| **Diminish Floor** | 0.1 | Minimum XP multiplier (never below 10%) |

Example: Player level 15, enemy level 10. Delta = 5, threshold = 3. Penalty levels = 2. Multiplier = 0.8^2 = 0.64.

#### Other XP Sources

| Field | Default | Description |
|-------|---------|-------------|
| **Quest XP Base** | 200 | Base XP from quest completions |
| **Craft XP Base** | 50 | Base XP per craft (multiplied by recipe tier) |
| **Exploration XP Base** | 150 | Base XP from exploration events |
| **Interaction XP Base** | 25 | Base XP from interactions |

> Note: Quest and craft XP are granted through XPGrantAPI by their respective systems. These base values are stored in the blob for reference but the actual amounts come from quest reward definitions and recipe tiers.

#### Rested XP

| Field | Default | Description |
|-------|---------|-------------|
| **Rested XP Multiplier** | 1.0 | Bonus multiplier while rested (1.0 = 100% bonus = double XP) |
| **Rested XP Accum Rate Per Hour** | 500 | Rested pool accumulated per offline hour |
| **Rested XP Max Days** | 3 | Maximum offline days that count toward rested accumulation |

---

### 1.2 Level Stat Scaling

Controls base stat values per level. These are the stats a player gets from leveling alone (before equipment bonuses).

1. Right-click > **Create > DIG > Progression > Level Stat Scaling**
2. Name it `LevelStatScaling`
3. Place at `Assets/Resources/LevelStatScaling.asset`

#### Per-Level Stats Array

The **Stats Per Level** array lets designers define exact stat values for each level. Index 0 = level 1 stats, etc.

Each entry contains:

| Field | Description |
|-------|-------------|
| Max Health | Base max health at this level |
| Attack Power | Base attack power |
| Spell Power | Base spell power |
| Defense | Base defense |
| Armor | Base armor |
| Max Mana | Base mana pool |
| Mana Regen | Base mana regen per second |
| Max Stamina | Base stamina pool |
| Stamina Regen | Base stamina regen per second |

#### Linear Scaling Fallback

For levels not covered by the array, linear formulas are used:

| Field | Default | Description |
|-------|---------|-------------|
| **Base Max Health** | 100 | Health at level 1 |
| **Max Health Per Level** | 15 | Health gained per level |
| **Base Attack Power** | 5 | Attack power at level 1 |
| **Attack Power Per Level** | 2 | Attack power gained per level |
| **Base Spell Power** | 5 | Spell power at level 1 |
| **Spell Power Per Level** | 2 | Spell power gained per level |
| **Base Defense** | 2 | Defense at level 1 |
| **Defense Per Level** | 1 | Defense gained per level |
| **Base Armor** | 0 | Armor at level 1 |
| **Armor Per Level** | 0.5 | Armor gained per level |
| **Base Max Mana** | 50 | Mana at level 1 |
| **Max Mana Per Level** | 5 | Mana gained per level |
| **Base Mana Regen** | 2 | Mana regen at level 1 |
| **Mana Regen Per Level** | 0.2 | Mana regen gained per level |
| **Base Max Stamina** | 100 | Stamina at level 1 |
| **Max Stamina Per Level** | 5 | Stamina gained per level |
| **Base Stamina Regen** | 5 | Stamina regen at level 1 |
| **Stamina Regen Per Level** | 0.3 | Stamina regen gained per level |

> Tip: Use the Per-Level Stats array for hand-tuned early game (levels 1-10), then rely on the linear fallback for later levels. Or leave the array empty and use only linear scaling.

---

### 1.3 Level Rewards

Defines special rewards granted when reaching specific levels.

1. Right-click > **Create > DIG > Progression > Level Rewards**
2. Name it `LevelRewards`
3. Place at `Assets/Resources/LevelRewards.asset`

#### Rewards Array

Each entry in the **Rewards** array has:

| Field | Description |
|-------|-------------|
| **Level** | Level at which this reward is granted (minimum 2) |
| **Reward Type** | Type of reward (see table below) |
| **Int Value** | Context-dependent integer (gold amount, recipe ID, etc.) |
| **Float Value** | Context-dependent float (modifier amount, etc.) |
| **Description** | Text shown in UI notification |

#### Reward Type Reference

| Reward Type | Int Value means | Float Value means |
|-------------|----------------|-------------------|
| **StatPoints** | Bonus stat points to award | Unused |
| **CurrencyGold** | Gold amount | Unused |
| **RecipeUnlock** | Recipe ID to unlock | Unused |
| **AbilityUnlock** | Ability ID | Unused (stub) |
| **ContentGate** | Gate identifier | Unused (stub) |
| **ResourceMaxUp** | Unused | Max increase amount (stub) |
| **TalentPoint** | Talent points | Unused (stub) |
| **Title** | Title string ID | Unused (stub) |

> Currently only StatPoints, CurrencyGold, and RecipeUnlock are fully functional. Others are stubs for future EPICs.

#### Example Rewards

| Level | Reward Type | Int Value | Description |
|-------|------------|-----------|-------------|
| 5 | CurrencyGold | 100 | "Level 5 bonus: 100 gold" |
| 5 | RecipeUnlock | 101 | "New recipe: Iron Sword" |
| 10 | StatPoints | 2 | "Bonus: 2 extra stat points" |
| 10 | CurrencyGold | 500 | "Level 10 bonus: 500 gold" |
| 20 | RecipeUnlock | 205 | "New recipe: Mithril Plate" |

> Multiple rewards can share the same level — all are granted when that level is reached.

---

## 2. Player Prefab Setup

### 2.1 Add Progression Authoring

1. Open the player prefab (e.g., `Warrok_Server`)
2. Select the **root** GameObject
3. Click **Add Component** > search for **Player Progression**
4. Configure starting values:

| Field | Default | Description |
|-------|---------|-------------|
| **Starting XP** | 0 | Initial XP (usually 0 for new characters) |
| **Starting Stat Points** | 0 | Free stat points at spawn (for testing, set higher) |
| **Starting Rested XP** | 0 | Initial rested XP pool |

> This goes on the root player entity (not a child). It adds ~40 bytes total: PlayerProgression (16) + LevelUpEvent (8) + StatAllocationRequest buffer header (16). This is within the 16KB archetype budget.

### 2.2 Reimport SubScene

After modifying the player prefab:

1. Right-click the SubScene containing the player spawn point > **Reimport**
2. Wait for baking to complete

> Ghost prefab changes require SubScene reimport to regenerate ghost serialization variants.

---

## 3. XP Bonus on Equipment

Items can grant bonus XP through the `XPBonusPercent` field on `ItemStatBlock`.

### 3.1 Setting Up an XP Bonus Item

When creating item definitions (EPIC 16.6), set the **XP Bonus Percent** field:

| Field | Type | Description |
|-------|------|-------------|
| **XP Bonus Percent** | float | Percentage bonus to all XP gains (0.1 = +10% XP) |

The `EquippedStatsSystem` automatically aggregates `XPBonusPercent` from all equipped items into `PlayerEquippedStats.TotalXPBonusPercent`. The XP award formula then multiplies by `(1 + TotalXPBonusPercent)`.

> Example: Two equipped items with XPBonusPercent 0.05 and 0.10 → total +15% XP on all kills.

---

## 4. Stat Allocation (Client → Server)

Players spend unspent stat points on four attributes:

| Attribute | Effect |
|-----------|--------|
| **Strength** | Increases AttackPower |
| **Dexterity** | Increases Defense |
| **Intelligence** | Increases SpellPower |
| **Vitality** | Increases MaxHealth |

### 4.1 How It Works

1. Client UI sends a `StatAllocationRpc` with the attribute type and number of points
2. Server `StatAllocationRpcReceiveSystem` validates the request and writes to the `StatAllocationRequest` buffer
3. `StatAllocationSystem` decrements `UnspentStatPoints`, increments the corresponding `CharacterAttributes` field
4. `LevelStatScalingSystem` recalculates all derived stats on the same frame

### 4.2 Sending Stat Allocation from UI

From your UI MonoBehaviour, create and send the RPC:

1. Get the local player's connection entity
2. Create a `StatAllocationRpc` with `Attribute` (byte cast of StatAttributeType) and `Points` (int)
3. Add as `SendRpcCommandRequest` on a new entity

> The server validates that `Points <= UnspentStatPoints` and that the attribute byte is in range (0-3).

---

## 5. UI Integration

The progression system provides a bridge layer but no built-in UI panels. Four provider interfaces are available:

### 5.1 Provider Interfaces

| Interface | Method | When Called |
|-----------|--------|------------|
| `IXPBarProvider` | `UpdateXPBar(level, currentXP, xpToNextLevel, percent, unspentStatPoints, restedXP)` | Every frame while local player has PlayerProgression |
| `ILevelUpPopupProvider` | `ShowLevelUp(newLevel, previousLevel, statPointsAwarded)` | On level-up event |
| `IXPGainProvider` | `ShowXPGain(amount, source)` | On XP gain (kill, quest, craft) |
| `IStatAllocationProvider` | `UpdateStatAllocation(unspentPoints, strength, dexterity, intelligence, vitality)` | Every frame while local player exists |

### 5.2 Registering a Provider

Create a MonoBehaviour implementing the desired interface, then register on enable:

```
OnEnable:  ProgressionUIRegistry.RegisterXPBar(this);
OnDisable: ProgressionUIRegistry.UnregisterXPBar(this);
```

Stub views (`XPBarView`, `LevelUpPopupView`, `StatAllocationView`) are provided as starting points in `Assets/Scripts/Progression/UI/`.

### 5.3 XP Source Types

The `XPSourceType` enum passed to `IXPGainProvider.ShowXPGain()`:

| Value | Source |
|-------|--------|
| Kill | Enemy kill |
| Quest | Quest reward |
| Crafting | Craft completion |
| Exploration | Zone/discovery event |
| Interaction | NPC interaction |
| Bonus | System-granted bonus |

---

## 6. Editor Tooling

### 6.1 Progression Workstation

**Menu:** DIG > Progression Workstation

A three-tab editor window:

#### Player Inspector Tab (Play Mode only)

- **Level** and **XP** — current level, XP progress bar, XP to next level
- **Total XP Earned** — lifetime stat
- **Unspent Stat Points** — available for allocation
- **Rested XP** — current pool remaining
- **Character Attributes** — Strength, Dexterity, Intelligence, Vitality breakdown
- **Combat Stats** — derived AttackPower, SpellPower, CritChance, Defense, Armor
- **Health** — Current / Max

#### XP Curve Tab

- **Curve Graph** — visual plot of XP per level
- **Per-Level Table** — each level showing:
  - XP required for that level
  - Cumulative total XP
  - Estimated kills-to-level (based on kill formula)
- Works with both explicit XP Per Level array and geometric fallback

#### XP Simulator Tab (Play Mode only)

- **Grant XP** — text field + button to instantly grant XP to the local player
- **Set Level** — slider to jump to any level (sets XP to 0 at that level)
- **Kill Simulation** — enter enemy count and level, calculates total XP and expected levels gained
- **Offline Monte Carlo** — simulates kill sequences to validate XP curve feel

---

## 7. Example: Full Progression Setup

### Step 1: Create ScriptableObjects

1. Create `ProgressionCurve.asset` at `Assets/Resources/ProgressionCurve`
   - Set Max Level = 30, Stat Points Per Level = 3
   - Fill XP Per Level for levels 1-10: [100, 150, 225, 340, 500, 750, 1100, 1600, 2400, 3500]
   - Set Geometric Multiplier = 1.15 for levels 11+
   - Set Diminish Start Delta = 3, Diminish Floor = 0.1
2. Create `LevelStatScaling.asset` at `Assets/Resources/LevelStatScaling`
   - Set Base Max Health = 100, Max Health Per Level = 20
   - Set Base Attack Power = 5, Attack Power Per Level = 3
3. Create `LevelRewards.asset` at `Assets/Resources/LevelRewards`
   - Add reward: Level 5, CurrencyGold, IntValue = 200
   - Add reward: Level 10, StatPoints, IntValue = 3

### Step 2: Player Prefab

1. Open `Warrok_Server` prefab
2. Add Component > **Player Progression** (leave defaults at 0)
3. Reimport SubScene

### Step 3: Verify

1. Enter Play Mode
2. Open DIG > Progression Workstation > Player Inspector tab
3. Kill enemies — XP bar should fill, level should increment
4. On level-up: stat points awarded, stats recalculated, health bar stays proportionally full
5. Open XP Curve tab — verify the curve looks right

---

## 8. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Bootstrap | Enter Play Mode | Console: "[ProgressionBootstrap] Loaded progression config" (or similar) |
| 3 | Player entity | Entity Inspector | Player has PlayerProgression + LevelUpEvent + StatAllocationRequest buffer |
| 4 | Kill XP | Kill an enemy | XPGain visual event fires, PlayerProgression.CurrentXP increases |
| 5 | Diminishing returns | Kill enemy 5+ levels below player | XP gain is reduced (check with Workstation) |
| 6 | Level-up | Accumulate enough XP | CharacterAttributes.Level increments, UnspentStatPoints increases |
| 7 | Multi-level-up | Grant large XP amount | Multiple levels gained in one frame, excess XP carries over |
| 8 | Max level cap | Reach max level | CurrentXP clamped to 0, no further leveling |
| 9 | Stat scaling | Level up | AttackStats, DefenseStats, Health.Max all recalculated |
| 10 | Health preservation | Level up at half health | Health bar stays at ~50% (proportional to new Max) |
| 11 | Stat allocation | Send StatAllocationRpc(Strength, 1) | CharacterAttributes.Strength increases, UnspentStatPoints decreases |
| 12 | Stat allocation validation | Send more points than available | Request rejected, no change |
| 13 | Quest XP | Complete a quest with Experience reward | XP granted via XPGrantAPI |
| 14 | Craft XP | Complete a craft | XP granted (tier * 50) |
| 15 | XP bonus gear | Equip item with XPBonusPercent = 0.1 | Kill XP increased by 10% |
| 16 | Level rewards | Reach a level with rewards configured | Gold granted, recipes unlocked |
| 17 | Rested XP | Set rested XP > 0 via simulator | Kill XP includes rested bonus, pool depletes |
| 18 | Ghost replication | Remote client | CharacterAttributes.Level visible on other players |
| 19 | Workstation - Inspector | Play Mode, open Player Inspector | Shows level, XP, stats, attributes |
| 20 | Workstation - XP Curve | Open XP Curve tab | Graph renders, per-level table shows cumulative XP |
| 21 | Workstation - Simulator | Play Mode, grant XP | Player levels up, stats update |

---

## 9. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| "ProgressionConfigSingleton not found" | Missing ScriptableObjects in Resources | Create all 3 SOs at `Assets/Resources/` (ProgressionCurve, LevelStatScaling, LevelRewards) |
| Player doesn't gain XP from kills | Missing ProgressionAuthoring on player prefab | Add **Player Progression** component to player root, reimport SubScene |
| Level stays at 1 despite XP | XP Per Level array is empty AND geometric formula produces very high thresholds | Check ProgressionCurve settings, use XP Curve tab to visualize |
| Stats don't change on level-up | LevelStatScaling SO not at `Assets/Resources/LevelStatScaling` | Create and place the asset correctly |
| Health drops to zero on level-up | Health.Current not preserved | This is handled automatically — check that LevelStatScalingSystem is running (WorldSystemFilter) |
| Stat allocation doesn't work | RPC not reaching server | Verify client sends StatAllocationRpc with valid Attribute (0-3) and Points > 0 |
| Quest XP not awarding | QuestRewardSystem not calling XPGrantAPI | Verify quest reward has Type = Experience with Value = XP amount |
| Craft XP not awarding | CraftOutputGenerationSystem not running | Check crafting system is set up (SETUP_GUIDE_16.13) |
| XP bonus gear not working | ItemStatBlock.XPBonusPercent = 0 on item | Set the field on the item definition (value is a decimal: 0.1 = 10%) |
| Level rewards not granting gold | Player missing CurrencyTransaction buffer | Ensure economy system is set up (EPIC 16.6) |
| Recipe unlock reward fails | Player missing CraftingKnowledgeLink | Ensure crafting system is set up (SETUP_GUIDE_16.13) |
| Progression Workstation shows no data | Not in Play Mode (Player Inspector/Simulator) | Enter Play Mode first; XP Curve tab works outside Play Mode |
| "Burst error BC1028" on LevelUpSystem | Old code using ComponentType[] in Burst | Fixed — uses EntityQueryBuilder. Pull latest code |

---

## 10. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Character attributes (Str/Dex/Int/Vit/Level) | Part of Combat Stats (existing) |
| Loot level gating, items, currency | SETUP_GUIDE_16.6 |
| Combat resources (Mana, Stamina pools) | SETUP_GUIDE_16.8 |
| Quest XP rewards | SETUP_GUIDE_16.12 |
| Crafting XP rewards, recipe unlock rewards | SETUP_GUIDE_16.13 |
| Persistence of XP/level/stat points | EPIC 16.15 (planned) |
| **Progression & XP System** | **This guide (16.14)** |
