# EPIC 17.7: Achievement System

**Status:** PLANNED
**Priority:** Medium (Engagement & Retention)
**Dependencies:**
- `KillCredited` IComponentData event (existing -- `Player.Components`, ephemeral on killer entity, created by `DeathTransitionSystem` via EndSimulationECB)
- `DiedEvent` IEnableableComponent (existing -- `Player.Components`, enabled by DeathTransitionSystem)
- `LevelUpEvent` IComponentData + IEnableableComponent (existing -- `DIG.Progression.Components`, EPIC 16.14, baked disabled, toggled by LevelUpSystem)
- `CharacterAttributes` IComponentData with `Level` field (existing -- `DIG.Combat.Components`, Ghost:All, 20 bytes)
- `PlayerProgression` IComponentData (existing -- `DIG.Progression.Components`, Ghost:AllPredicted, 16 bytes)
- `QuestEventQueue` static NativeQueue (existing -- `DIG.Quest.UI.QuestEventQueue`, EPIC 16.12, quest completion events)
- `CraftOutputGenerationSystem` (existing -- `DIG.Crafting.Systems`, EPIC 16.13, crafting output events)
- `DeathLootSystem` (existing -- `DIG.Loot.Systems`, EPIC 16.6, reads DiedEvent, creates PendingLootSpawn)
- `CombatResultEvent` IComponentData (existing -- `DIG.Combat`, transient entities with damage data)
- `DamageVisualQueue` static NativeQueue (existing -- `DIG.Combat`, ECS-to-UI damage bridge)
- `DialogueSessionState` IComponentData (existing -- `DIG.Dialogue`, NPC interaction tracking)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, EPIC 16.6, Gold/Premium/Crafting)
- `XPGrantAPI` static helper (existing -- `DIG.Progression.Systems`, EPIC 16.14, cross-system XP grants)
- `CombatUIBridgeSystem` / `CombatUIRegistry` pattern (existing -- static registry + provider interface)
- `ISaveModule` interface (existing -- `DIG.Persistence.Core`, EPIC 16.15, modular save/load)
- `SaveStateLink` child entity pattern (existing -- `DIG.Persistence.Components`, 8 bytes on player)
- `TargetingModuleLink` child entity pattern (existing -- child entity reference pattern)
- `GhostOwner.NetworkId` (existing -- NetCode player identity)

**Feature:** A data-driven, server-authoritative achievement system that tracks player milestones across combat, exploration, crafting, social, collection, progression, and challenge categories. Uses a listener pattern to observe existing game events (kills, level-ups, quest completions, crafting, loot, dialogue) without modifying any source system. Multi-tier achievements (Bronze/Silver/Gold/Platinum) with increasing thresholds and escalating rewards. Achievement progress stored on a child entity via `AchievementLink` (8 bytes on player) to maintain zero impact on the player archetype beyond the link. Hidden achievements remain invisible until unlocked. Toast notifications on unlock with icon, description, and reward preview.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `KillCredited` event | `DeathTransitionSystem.cs` | Fully implemented | Ephemeral on killer entity, created via EndSimulationECB |
| `DiedEvent` (player death) | `DeathTransitionSystem.cs` | Fully implemented | IEnableableComponent on player |
| `LevelUpEvent` | `LevelUpSystem.cs` | Fully implemented | IEnableableComponent, toggled per level-up (EPIC 16.14) |
| `QuestEventQueue` | `QuestEventQueue.cs` | Fully implemented | Static NativeQueue, quest completion/accept/fail events (EPIC 16.12) |
| `CraftOutputGenerationSystem` | `CraftOutputGenerationSystem.cs` | Fully implemented | Generates crafted items (EPIC 16.13) |
| `DeathLootSystem` | `DeathLootSystem.cs` | Fully implemented | Reads DiedEvent, creates PendingLootSpawn (EPIC 16.6) |
| `CombatResultEvent` pipeline | `CombatResolutionSystem.cs` | Fully implemented | Transient entities with resolved damage data |
| `DamageVisualQueue` | `DamageVisualQueue.cs` | Fully implemented | Static NativeQueue bridge for damage UI |
| `DialogueSessionState` | `DialogueInitiationSystem.cs` | Fully implemented | Tracks active NPC dialogue (EPIC 16.16) |
| `CurrencyInventory` | `CurrencyInventory.cs` | Fully implemented | Gold/Premium/Crafting currency (EPIC 16.6) |
| `XPGrantAPI` | `QuestXPBridgeSystem.cs` | Fully implemented | Static helper for cross-system XP grants (EPIC 16.14) |
| `ISaveModule` pattern | `ISaveModule.cs` | Fully implemented | Modular save/load with TypeId registry (EPIC 16.15) |
| `SaveStateLink` child entity | `SaveStateComponents.cs` | Fully implemented | 8-byte link to save data child entity |
| `CombatUIBridgeSystem` pattern | `CombatUIBridgeSystem.cs` | Fully implemented | Static registry + provider interface |

### What's Missing

- **No achievement definitions** -- no data model for milestones, tiers, thresholds, or rewards
- **No achievement progress tracking** -- no system observes game events to increment counters
- **No achievement unlock logic** -- no threshold comparison, tier progression, or unlock events
- **No achievement reward distribution** -- no pipeline to grant gold/XP/titles/cosmetics on unlock
- **No achievement UI** -- no toast notifications, achievement panel, or progress bars
- **No hidden achievement support** -- no metadata to hide achievements until unlocked
- **No achievement persistence** -- no save module for achievement progress
- **No editor tooling** -- no achievement definition editor, progress inspector, or validator

---

## Problem

DIG has a rich event ecosystem -- kills, quest completions, level-ups, crafting, loot drops, NPC dialogue, damage milestones -- but no system observes these events to track player milestones. There is no achievement framework to reward long-term engagement, encourage exploration of game systems, or provide meta-progression goals beyond leveling. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `KillCredited` event on killer after enemy death | No system counts lifetime kills |
| `QuestEventQueue` with quest completion events | No system tracks quests completed |
| `LevelUpEvent` on player after level-up | No milestone tracking for level thresholds |
| `CraftOutputGenerationSystem` creates items | No system counts items crafted |
| `DeathLootSystem` creates loot | No system tracks loot rarity milestones |
| `DiedEvent` on player death | No death counter for "survive X without dying" challenges |
| `DialogueSessionState` for NPC interactions | No NPC interaction counter |
| `CombatResultEvent` with damage data | No cumulative damage milestone tracking |
| `CurrencyInventory` gold/premium | No reward distribution for achievements |
| `XPGrantAPI` for cross-system XP | No XP rewards for achievement unlocks |

**The gap:** A player who has killed 1000 enemies, completed 50 quests, crafted 200 items, and reached level 30 has no acknowledgment of these milestones. There are no meta-goals, no collection incentives, no challenge objectives. Designers cannot create "Kill 100 enemies" or "Reach level 25" progression goals that reward titles, gold, or XP bonuses.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  AchievementDatabaseSO       AchievementDefinitionSO       AchievementConfigSO
  (List<AchievementDef>,      (Id, Category, Condition,     (MaxTracked, toast
   Resources/AchievementDB)    Tiers[], Hidden, Icon)        duration, save freq)
           |                         |                            |
           └────── AchievementBootstrapSystem ────────────────────┘
                   (loads from Resources/, builds BlobAssets,
                    creates AchievementRegistrySingleton,
                    follows ProgressionBootstrapSystem pattern)
                              |
                    ECS DATA LAYER
  AchievementProgress          AchievementRegistrySingleton
  (IBufferElementData          (BlobRef to all definitions
   on CHILD entity,             + tier thresholds)
   Id + Value + Unlocked)
           |
  Player ──> AchievementLink (8 bytes, Entity ref to child)
                              |
                    EVENT SOURCES (READ-ONLY -- no modifications)
  KillCredited ───────────────+
  QuestEventQueue ────────────+
  LevelUpEvent ───────────────+
  CraftOutputGenerationSystem ┤
  DeathLootSystem ────────────+──> AchievementTrackingSystem
  DiedEvent ──────────────────+    (reads events, increments progress)
  CombatResultEvent ──────────+
  DialogueSessionState ───────+
                              |
                    SYSTEM PIPELINE (SimulationSystemGroup, Server|Local)
                              |
  AchievementTrackingSystem ── reads all event sources, increments counters
  (UpdateLast in SimulationSystemGroup, after all event producers)
           |
  AchievementUnlockSystem ── checks thresholds, creates unlock events
  (detects tier completions, creates AchievementUnlockEvent transient entities)
           |
  AchievementRewardSystem ── distributes rewards on unlock
  (gold via CurrencyInventory, XP via XPGrantAPI, titles/cosmetics via flags)
           |
  AchievementCleanupSystem ── destroys transient unlock entities
                              |
                    PRESENTATION LAYER (PresentationSystemGroup)
                              |
  AchievementUIBridgeSystem → AchievementUIRegistry → IAchievementUIProvider
  (managed, reads unlock events + progress, dequeues AchievementVisualQueue)
           |
  AchievementToastView / AchievementPanelView (MonoBehaviours)
```

### Data Flow (Kill Enemy --> Achievement Progress --> Unlock --> Reward)

```
Frame N (Server):
  1. DeathTransitionSystem: Enemy dies, fires KillCredited on killer via EndSimulationECB

Frame N+1 (Server):
  2. XPAwardSystem: Reads KillCredited, awards XP (existing, unchanged)
  3. AchievementTrackingSystem (UpdateLast): Reads KillCredited on player entities
     - Lookup AchievementLink → child entity → AchievementProgress buffer
     - Find all achievements with ConditionType == EnemyKill
     - Increment AchievementProgress[i].CurrentValue += 1
     - Also reads: QuestEventQueue, LevelUpEvent, CraftOutput, etc.

Frame N+2 (Server):
  4. AchievementUnlockSystem: Iterates AchievementProgress buffer
     - For each entry where CurrentValue >= tier threshold AND not yet unlocked:
       - Set IsUnlocked = true, UnlockTick = serverTick
       - Create AchievementUnlockEvent transient entity (AchievementId, PlayerId, Tier)
       - Enqueue to AchievementVisualQueue for client toast

  5. AchievementRewardSystem: Reads AchievementUnlockEvent entities
     - Lookup reward from AchievementRegistrySingleton blob
     - Gold: CurrencyInventory.Gold += reward.GoldAmount
     - XP: XPGrantAPI.GrantXP(player, reward.XPAmount, XPSourceType.Bonus)
     - Title: set TitleUnlockFlag on player (future title system)
     - TalentPoints: PlayerProgression.UnspentStatPoints += reward.TalentPoints

  6. AchievementCleanupSystem: Destroys AchievementUnlockEvent entities

Frame N+2 (Client):
  7. AchievementUIBridgeSystem: Dequeues AchievementVisualQueue
     - Pushes toast notification data to AchievementUIRegistry
     - IAchievementUIProvider.ShowToast(achievementName, icon, tier, rewardText)
```

### Critical System Ordering Chain

```
[All event producer systems run first -- unchanged]
DeathTransitionSystem (EndSimulationECB)
XPAwardSystem [reads KillCredited]
LevelUpSystem [reads XP threshold]
QuestCompletionSystem [fires QuestEventQueue]
CraftOutputGenerationSystem [creates crafted items]
    |
    v (UpdateLast in SimulationSystemGroup)
AchievementTrackingSystem [reads ALL events -- must run after all producers]
    |
AchievementUnlockSystem [UpdateAfter(AchievementTrackingSystem)]
    |
AchievementRewardSystem [UpdateAfter(AchievementUnlockSystem)]
    |
AchievementCleanupSystem [UpdateAfter(AchievementRewardSystem)]
```

---

## ECS Components

### On Player Entity (8 bytes total)

**File:** `Assets/Scripts/Achievement/Components/AchievementLink.cs`

```csharp
// 8 bytes on player entity -- child entity holds all achievement data
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct AchievementLink : IComponentData
{
    [GhostField] public Entity AchievementChild; // Child entity with progress buffers
}
```

### On Achievement Child Entity (not on player archetype)

**File:** `Assets/Scripts/Achievement/Components/AchievementComponents.cs`

```csharp
// Tag to identify achievement child entities -- 0 bytes
public struct AchievementChildTag : IComponentData { }

// Back-reference to owning player -- 8 bytes
public struct AchievementOwner : IComponentData
{
    public Entity Owner; // Player entity
}

// Per-achievement progress entry -- 12 bytes per element
[InternalBufferCapacity(64)]
public struct AchievementProgress : IBufferElementData
{
    public ushort AchievementId;   // Stable ID matching AchievementDefinitionSO
    public int CurrentValue;       // Accumulated counter (kills, quests, etc.)
    public bool IsUnlocked;        // True once highest completed tier reached
    public byte HighestTierUnlocked; // 0=none, 1=Bronze, 2=Silver, 3=Gold, 4=Platinum
    public uint UnlockTick;        // Server tick of most recent tier unlock
}

// Cumulative stat counters for complex conditions -- 48 bytes
public struct AchievementCumulativeStats : IComponentData
{
    public int TotalKills;           // Lifetime enemy kills
    public int TotalDeaths;          // Lifetime player deaths
    public int TotalQuestsCompleted; // Lifetime quests completed
    public int TotalItemsCrafted;    // Lifetime items crafted
    public int TotalNPCsInteracted;  // Lifetime NPC dialogues
    public long TotalDamageDealt;    // Lifetime damage dealt (long for large values)
    public int TotalLootCollected;   // Lifetime loot pickups
    public int HighestKillStreak;    // Best kill streak without dying
    public int CurrentKillStreak;    // Active kill streak (reset on death)
    public int ConsecutiveLoginDays; // Future: daily login tracking
}

// Dirty flag for save system -- 4 bytes
public struct AchievementDirtyFlags : IComponentData
{
    public uint Flags; // Bitmask: bit 0 = progress changed, bit 1 = stats changed
}
```

### Transient Unlock Event

**File:** `Assets/Scripts/Achievement/Components/AchievementUnlockEvent.cs`

```csharp
// Created as transient entity by AchievementUnlockSystem, destroyed same frame
public struct AchievementUnlockEvent : IComponentData
{
    public ushort AchievementId;  // Which achievement
    public Entity PlayerId;       // Which player
    public byte Tier;             // 1=Bronze, 2=Silver, 3=Gold, 4=Platinum
    public uint ServerTick;       // When unlocked
}
```

### Enums

**File:** `Assets/Scripts/Achievement/Components/AchievementEnums.cs`

```csharp
public enum AchievementCategory : byte
{
    Combat      = 0,   // Kill counts, damage milestones, kill streaks
    Exploration = 1,   // Zone discovery, distance traveled, locations visited
    Crafting    = 2,   // Items crafted, recipes known, rare crafts
    Social      = 3,   // NPC interactions, dialogue trees completed, party play
    Collection  = 4,   // Loot collected, rarity milestones, item sets
    Progression = 5,   // Level milestones, stat points allocated, skills unlocked
    Challenge   = 6    // No-death runs, timed kills, special conditions
}

public enum AchievementConditionType : byte
{
    EnemyKill           = 0,   // Total enemies killed
    EnemyKillByType     = 1,   // Kill specific enemy type (EnemyTypeId match)
    QuestComplete       = 2,   // Total quests completed
    LevelReached        = 3,   // Player level >= threshold
    ItemCrafted         = 4,   // Total items crafted
    ItemCraftedByRecipe = 5,   // Craft specific recipe N times
    LootCollected       = 6,   // Total loot items picked up
    LootByRarity        = 7,   // Collect N items of specific rarity
    DamageDealt         = 8,   // Cumulative damage dealt
    PlayerDeath         = 9,   // Total player deaths
    KillStreak          = 10,  // Kill N enemies without dying
    NPCInteraction      = 11,  // Talk to N unique NPCs
    GoldEarned          = 12,  // Lifetime gold earned
    DialogueComplete    = 13,  // Complete N dialogue trees
    SurvivalTime        = 14,  // Survive N seconds without dying
    BossKill            = 15,  // Kill specific boss (EnemyTypeId match)
    CraftRareItem       = 16,  // Craft item of rare+ quality
    ReachZone           = 17,  // Discover specific zone (future)
    StatPointsAllocated = 18,  // Total stat points spent
    TalentPointsSpent   = 19   // Total talent points spent (EPIC 17.1)
}

public enum AchievementTier : byte
{
    None     = 0,
    Bronze   = 1,
    Silver   = 2,
    Gold     = 3,
    Platinum = 4
}

public enum AchievementRewardType : byte
{
    Gold          = 0,   // CurrencyInventory.Gold += amount
    XP            = 1,   // XPGrantAPI.GrantXP(player, amount, Bonus)
    Title         = 2,   // Unlock display title (TitleId)
    Cosmetic      = 3,   // Unlock cosmetic item (CosmeticId)
    TalentPoints  = 4,   // PlayerProgression.UnspentStatPoints += amount
    RecipeUnlock  = 5,   // Unlock crafting recipe (RecipeId)
    StatBonus     = 6    // Permanent stat increase
}
```

### Singleton (BlobAssets)

**File:** `Assets/Scripts/Achievement/Data/AchievementBlobs.cs`

```csharp
public struct AchievementRegistrySingleton : IComponentData
{
    public BlobAssetReference<AchievementRegistryBlob> Registry;
}

public struct AchievementRegistryBlob
{
    public int TotalAchievements;
    public BlobArray<AchievementDefinitionBlob> Definitions;
}

public struct AchievementDefinitionBlob
{
    public ushort AchievementId;       // Stable ID
    public AchievementCategory Category;
    public AchievementConditionType ConditionType;
    public int ConditionParam;          // EnemyTypeId, RecipeId, RarityLevel, etc.
    public bool IsHidden;               // Hidden until unlocked
    public BlobString Name;             // Display name
    public BlobString Description;      // Tooltip description
    public BlobString IconPath;         // Sprite asset path
    public BlobArray<AchievementTierBlob> Tiers; // 1-4 tiers
}

public struct AchievementTierBlob
{
    public AchievementTier Tier;        // Bronze/Silver/Gold/Platinum
    public int Threshold;               // Value required for this tier
    public AchievementRewardType RewardType;
    public int RewardIntValue;          // Gold amount, XP amount, TitleId, etc.
    public float RewardFloatValue;      // Stat bonus percentage, etc.
    public BlobString RewardDescription; // "500 Gold" or "+5% Attack Power"
}
```

---

## ScriptableObjects

### AchievementDefinitionSO

**File:** `Assets/Scripts/Achievement/Definitions/AchievementDefinitionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Achievement/Achievement Definition")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| AchievementId | ushort | auto | Stable identifier (MUST NOT change across versions) |
| AchievementName | string | "" | Display name ("Slayer", "Master Crafter") |
| Description | string | "" | Tooltip text ("Kill {0} enemies") |
| Category | AchievementCategory | Combat | Category for UI grouping |
| ConditionType | AchievementConditionType | EnemyKill | What event to track |
| ConditionParam | int | 0 | Subtype filter (EnemyTypeId, RecipeId, RarityLevel) |
| IsHidden | bool | false | Hide from UI until unlocked |
| Icon | Sprite | null | Achievement icon for panel and toast |
| Tiers | AchievementTierDefinition[] | 1 entry | Bronze/Silver/Gold/Platinum thresholds + rewards |

### AchievementTierDefinition

```
[Serializable]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Tier | AchievementTier | Bronze | Which tier this entry represents |
| Threshold | int | 10 | Counter value required (e.g., 10 kills) |
| RewardType | AchievementRewardType | Gold | What to grant on unlock |
| RewardIntValue | int | 100 | Amount for Gold/XP/TitleId/CosmeticId |
| RewardFloatValue | float | 0 | Amount for stat bonuses |
| RewardDescription | string | "" | Human-readable reward text |

### AchievementDatabaseSO

**File:** `Assets/Scripts/Achievement/Definitions/AchievementDatabaseSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Achievement/Achievement Database")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Achievements | List\<AchievementDefinitionSO\> | empty | All achievement definitions |
| ValidateOnBuild | bool | true | Run validator in build pipeline |

### AchievementConfigSO

**File:** `Assets/Scripts/Achievement/Definitions/AchievementConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Achievement/Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| MaxTrackedAchievements | int | 128 | Max achievements in progress buffer |
| ToastDisplayDuration | float | 5.0 | Seconds to show unlock toast |
| ToastQueueMaxSize | int | 5 | Max queued toasts before dropping |
| SaveOnUnlock | bool | true | Auto-save on achievement unlock |
| EnableHiddenAchievements | bool | true | Global toggle for hidden system |
| ProgressUpdateInterval | int | 1 | Frames between tracking updates (1=every frame) |
| KillStreakResetOnDeath | bool | true | Reset kill streak counter on player death |
| EnableToastNotifications | bool | true | Global toggle for toast popups |

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Client|Local):
  AchievementBootstrapSystem              -- loads SOs, builds BlobAssets, creates singleton (runs once)

SimulationSystemGroup (Server|Local):
  [All event producer systems run first -- unchanged]
  DeathTransitionSystem (EndSimulationECB)
  XPAwardSystem
  LevelUpSystem
  QuestCompletionSystem
  CraftOutputGenerationSystem
  ...
  [UpdateLast]
  AchievementTrackingSystem              -- reads ALL events, increments progress counters
  AchievementUnlockSystem                -- checks thresholds, creates unlock events
  AchievementRewardSystem                -- distributes rewards via existing pipelines
  AchievementCleanupSystem               -- destroys transient unlock entities

PresentationSystemGroup (Client|Local):
  AchievementUIBridgeSystem              -- managed bridge, dequeues visual events -> UI
```

### AchievementBootstrapSystem

**File:** `Assets/Scripts/Achievement/Systems/AchievementBootstrapSystem.cs`

- Managed SystemBase, `InitializationSystemGroup`, `Server|Client|Local`
- Runs once, then `Enabled = false` (self-disables)
- Load `AchievementDatabaseSO` from `Resources/AchievementDatabase`
- Load `AchievementConfigSO` from `Resources/AchievementConfig`
- Build `BlobAssetReference<AchievementRegistryBlob>` via BlobBuilder
  - For each `AchievementDefinitionSO`: bake Id, Category, ConditionType, ConditionParam, IsHidden, tiers
- Create `AchievementRegistrySingleton` entity
- Log achievement count: `"[Achievement] Registered {count} achievements with {tierCount} total tiers"`
- Follows `ProgressionBootstrapSystem` / `SurfaceGameplayConfigSystem` pattern

### AchievementTrackingSystem

**File:** `Assets/Scripts/Achievement/Systems/AchievementTrackingSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`, `[UpdateLast]`
- Requires `CompleteDependency()` at top of OnUpdate (async job safety from combat/damage systems)
- Reads event sources and increments `AchievementProgress` + `AchievementCumulativeStats`:

**Event Observation (read-only, zero modifications to source systems):**

| Event Source | Component / Queue | Read Pattern | Achievement Types |
|-------------|-------------------|--------------|-------------------|
| Enemy kills | `KillCredited` on player | EntityQuery, `WithAll<KillCredited, PlayerTag>` | EnemyKill, EnemyKillByType, KillStreak |
| Player death | `DiedEvent` on player | IEnableableComponent check | PlayerDeath, KillStreak reset |
| Level up | `LevelUpEvent` on player | IEnableableComponent check | LevelReached |
| Quest complete | `QuestEventQueue` | Static NativeQueue.TryDequeue() peek | QuestComplete |
| Item crafted | Craft output entities | EntityQuery `WithAll<CraftedItemTag>` | ItemCrafted, ItemCraftedByRecipe, CraftRareItem |
| Loot collected | Loot pickup events | EntityQuery, pending loot transitions | LootCollected, LootByRarity |
| Damage dealt | `CombatResultEvent` entities | EntityQuery, sum DamageAmount | DamageDealt |
| NPC dialogue | `DialogueSessionState` changes | Change filter on DialogueSessionState | NPCInteraction, DialogueComplete |

**Per-frame logic:**
1. Query all players with `AchievementLink`
2. For each player: resolve `AchievementLink.AchievementChild`
3. Get `DynamicBuffer<AchievementProgress>` + `AchievementCumulativeStats` on child
4. For each event type detected this frame:
   - Increment relevant cumulative stat (TotalKills, TotalDeaths, etc.)
   - Iterate progress buffer, find matching `ConditionType` entries
   - Increment `CurrentValue` for matching achievements
5. Set `AchievementDirtyFlags.Flags |= 0x1` if any progress changed

**Kill streak tracking:**
- On `KillCredited`: `CumulativeStats.CurrentKillStreak++`
- If `CurrentKillStreak > HighestKillStreak`: update HighestKillStreak
- On `DiedEvent`: `CurrentKillStreak = 0` (if `Config.KillStreakResetOnDeath`)
- KillStreak achievements check `HighestKillStreak` against threshold

**Key pattern:** Uses manual `EntityQuery.ToEntityArray()` + `EntityQuery.ToComponentDataArray<T>()` -- NEVER `SystemAPI.Query` for transient types (lesson from CombatResultEvent/PendingCombatHit source-gen issues).

### AchievementUnlockSystem

**File:** `Assets/Scripts/Achievement/Systems/AchievementUnlockSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`
- `[UpdateAfter(typeof(AchievementTrackingSystem))]`
- Iterates achievement child entities with `AchievementProgress` buffer
- For each progress entry:
  - Lookup definition from `AchievementRegistrySingleton` blob by AchievementId
  - Iterate tiers in ascending order (Bronze -> Platinum)
  - If `CurrentValue >= tier.Threshold` AND `HighestTierUnlocked < tier.Tier`:
    - Update `HighestTierUnlocked` to new tier
    - Set `UnlockTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick`
    - Create `AchievementUnlockEvent` transient entity via ECB
    - Enqueue to `AchievementVisualQueue` for client toast
    - Set `AchievementDirtyFlags.Flags |= 0x3` (progress + stats dirty)
  - If all tiers unlocked: set `IsUnlocked = true`

### AchievementRewardSystem

**File:** `Assets/Scripts/Achievement/Systems/AchievementRewardSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`
- `[UpdateAfter(typeof(AchievementUnlockSystem))]`
- Reads `AchievementUnlockEvent` entities via manual EntityQuery
- For each unlock event:
  - Lookup tier reward from blob: `Registry.Definitions[id].Tiers[tier-1]`
  - Dispatch by `RewardType`:
    - `Gold`: `EntityManager.SetComponentData<CurrencyInventory>(player, gold + reward.RewardIntValue)`
    - `XP`: `XPGrantAPI.GrantXP(player, reward.RewardIntValue, XPSourceType.Bonus)`
    - `Title`: add `TitleUnlock` element to player's title buffer (future title system, no-op if absent)
    - `TalentPoints`: `PlayerProgression.UnspentStatPoints += reward.RewardIntValue`
    - `RecipeUnlock`: add RecipeId to `KnownRecipeElement` buffer via `CraftingKnowledgeLink`
    - `StatBonus`: apply permanent stat modifier (future, logged as TODO)
    - `Cosmetic`: add CosmeticId to cosmetic buffer (future, logged as TODO)

### AchievementCleanupSystem

**File:** `Assets/Scripts/Achievement/Systems/AchievementCleanupSystem.cs`

- Unmanaged ISystem, `SimulationSystemGroup`, `Server|Local`
- `[UpdateAfter(typeof(AchievementRewardSystem))]`
- Destroys all `AchievementUnlockEvent` entities via ECB
- Same lifecycle pattern as `CombatEventCleanupSystem`

---

## Authoring

**File:** `Assets/Scripts/Achievement/Authoring/AchievementAuthoring.cs`

```
[AddComponentMenu("DIG/Achievement/Player Achievement")]
```

- Place on player prefab (Warrok_Server) alongside existing `ProgressionAuthoring`, `SaveStateAuthoring`
- Baker creates child entity (same pattern as `TargetingModuleLink`, `SaveStateLink`, `TalentLink`):
  - Player entity: `AchievementLink` (8 bytes)
  - Child entity: `AchievementChildTag` + `AchievementOwner` + `AchievementProgress` buffer (Capacity=64) + `AchievementCumulativeStats` + `AchievementDirtyFlags`
- AchievementProgress buffer initialized empty -- `AchievementBootstrapSystem` populates entries on first connection

### AchievementInitializationSystem

**File:** `Assets/Scripts/Achievement/Systems/AchievementInitializationSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`
- Detects new players with `AchievementLink` but empty `AchievementProgress` buffer
- Reads `AchievementRegistrySingleton` to get all achievement IDs
- Populates buffer with one `AchievementProgress` entry per achievement (CurrentValue=0, IsUnlocked=false, HighestTierUnlocked=0)
- Runs after `AchievementBootstrapSystem` has created the singleton

---

## UI Bridge

**File:** `Assets/Scripts/Achievement/Bridges/AchievementVisualQueue.cs`

Static NativeQueue bridge (same pattern as `DamageVisualQueue`, `LevelUpVisualQueue`):

```csharp
public static class AchievementVisualQueue
{
    private static NativeQueue<AchievementUnlockVisualEvent> _queue;

    public static void Initialize() { _queue = new NativeQueue<...>(Allocator.Persistent); }
    public static void Enqueue(AchievementUnlockVisualEvent evt) { _queue.Enqueue(evt); }
    public static bool TryDequeue(out AchievementUnlockVisualEvent evt) { return _queue.TryDequeue(out evt); }
    public static void Dispose() { if (_queue.IsCreated) _queue.Dispose(); }
}

public struct AchievementUnlockVisualEvent
{
    public ushort AchievementId;
    public AchievementTier Tier;
    public FixedString64Bytes AchievementName;
    public FixedString128Bytes Description;
    public AchievementRewardType RewardType;
    public int RewardAmount;
}
```

**File:** `Assets/Scripts/Achievement/Bridges/AchievementUIBridgeSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`
- Reads local player `AchievementLink -> AchievementChild -> AchievementProgress` buffer
- Dequeues `AchievementVisualQueue` entries for toast notifications
- Pushes full progress data to `AchievementUIRegistry` for panel UI
- Reads `AchievementRegistrySingleton` blob for definition metadata (names, icons, tiers)
- Hidden achievements: only pushes to UI if `IsHidden == false` OR `IsUnlocked == true`

**File:** `Assets/Scripts/Achievement/Bridges/AchievementUIRegistry.cs`

Static provider registry (same pattern as `CombatUIRegistry`, `ProgressionUIRegistry`):

```csharp
public static class AchievementUIRegistry
{
    private static IAchievementUIProvider _provider;
    public static bool HasProvider => _provider != null;
    public static void Register(IAchievementUIProvider provider) { _provider = provider; }
    public static void Unregister(IAchievementUIProvider provider) { if (_provider == provider) _provider = null; }

    public static void ShowToast(AchievementToastData data) { _provider?.ShowToast(data); }
    public static void UpdatePanel(AchievementPanelData data) { _provider?.UpdatePanel(data); }
    public static void UpdateProgress(ushort achievementId, int current, int threshold) { _provider?.UpdateProgress(achievementId, current, threshold); }
}
```

**File:** `Assets/Scripts/Achievement/Bridges/IAchievementUIProvider.cs`

```csharp
public interface IAchievementUIProvider
{
    void ShowToast(AchievementToastData data);
    void UpdatePanel(AchievementPanelData data);
    void UpdateProgress(ushort achievementId, int currentValue, int nextThreshold);
    void HideToast();
}

public struct AchievementToastData
{
    public string AchievementName;
    public string Description;
    public string RewardText;
    public Sprite Icon;
    public AchievementTier Tier;
    public float DisplayDuration;
}

public struct AchievementPanelData
{
    public AchievementEntryUI[] Entries;
    public int TotalUnlocked;
    public int TotalAchievements;
    public float CompletionPercent;
}

public struct AchievementEntryUI
{
    public ushort AchievementId;
    public string Name;
    public string Description;
    public Sprite Icon;
    public AchievementCategory Category;
    public AchievementTier HighestTier;
    public int CurrentValue;
    public int NextThreshold;      // Threshold for next unearned tier (0 if all complete)
    public float ProgressPercent;  // Toward next tier
    public bool IsHidden;          // True if hidden and not yet unlocked
    public bool IsComplete;        // All tiers unlocked
    public AchievementTierRewardUI[] Tiers;
}

public struct AchievementTierRewardUI
{
    public AchievementTier Tier;
    public int Threshold;
    public string RewardText;
    public bool IsUnlocked;
}
```

**File:** `Assets/Scripts/Achievement/UI/AchievementToastView.cs`

- MonoBehaviour implementing `IAchievementUIProvider.ShowToast()`
- Animated popup: slides in from right, icon + name + tier badge + reward text
- Queue system: max `Config.ToastQueueMaxSize` pending toasts, display one at a time
- Auto-dismiss after `Config.ToastDisplayDuration` seconds
- Tier-colored border: Bronze=#CD7F32, Silver=#C0C0C0, Gold=#FFD700, Platinum=#E5E4E2

**File:** `Assets/Scripts/Achievement/UI/AchievementPanelView.cs`

- MonoBehaviour implementing `IAchievementUIProvider.UpdatePanel()`
- Full-screen panel with category tabs (Combat, Exploration, Crafting, Social, Collection, Progression, Challenge)
- Grid of achievement cards with progress bars
- Hidden achievements show as "???" with locked icon until unlocked
- Filter: All / In Progress / Completed / Hidden (unlocked)
- Sort: Category / Completion % / Most Recent
- Completion counter: "47 / 120 Achievements (39%)"

---

## Save Integration

### AchievementSaveModule (ISaveModule, TypeId=14)

**File:** `Assets/Scripts/Persistence/Modules/AchievementSaveModule.cs`

```
ISaveModule implementation:
  TypeId = 14
  DisplayName = "Achievements"
  ModuleVersion = 1
```

**Serialization format:**

```
CumulativeStats block:
  TotalKills           : int32
  TotalDeaths          : int32
  TotalQuestsCompleted : int32
  TotalItemsCrafted    : int32
  TotalNPCsInteracted  : int32
  TotalDamageDealt     : int64
  TotalLootCollected   : int32
  HighestKillStreak    : int32
  CurrentKillStreak    : int32
  ConsecutiveLoginDays : int32

ProgressCount : int16 (number of achievement entries)
foreach entry:
  AchievementId        : ushort
  CurrentValue         : int32
  IsUnlocked           : byte (0 or 1)
  HighestTierUnlocked  : byte
  UnlockTick           : uint32
```

**Per entry: 12 bytes. Max: 2 + 44 + 128 * 12 = 1,582 bytes.**

**Serialize path:**
1. Resolve `AchievementLink.AchievementChild` from player entity
2. Read `AchievementCumulativeStats`, write 44 bytes
3. Read `DynamicBuffer<AchievementProgress>`, write count + entries

**Deserialize path:**
1. Resolve child entity via `AchievementLink`
2. Read `AchievementCumulativeStats`, set on child entity
3. Clear `AchievementProgress` buffer, re-add each entry
4. `AchievementTrackingSystem` resumes from restored counters next frame

**Backward compatibility:** If `AchievementLink` component is absent on player, module returns 0 from `Serialize()` (skip). On deserialize of old saves without TypeId=14 block, `LoadSystem` silently skips (standard ISaveModule pattern).

**IsDirty check:** Reads `AchievementDirtyFlags.Flags != 0` on child entity. Resets flags to 0 after serialize.

---

## Editor Tooling

### AchievementWorkstationWindow

**File:** `Assets/Editor/AchievementWorkstation/AchievementWorkstationWindow.cs`

- Menu: `DIG/Achievement Workstation`
- Sidebar + `IAchievementWorkstationModule` pattern (matches ProgressionWorkstation, PersistenceWorkstation)

### Modules (4 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| Definition Editor | `Modules/DefinitionEditorModule.cs` | Create/edit `AchievementDefinitionSO` assets. Category dropdown, condition type selector, tier editor (add/remove tiers with threshold + reward), hidden toggle, icon picker. Batch creation: "Generate tier set" fills Bronze(10)/Silver(50)/Gold(100)/Platinum(500) from template. Duplicate achievement button. |
| Progress Inspector | `Modules/ProgressInspectorModule.cs` | Play-mode only: select player entity, view live `AchievementProgress` buffer. Columns: Name, Category, CurrentValue/NextThreshold, Tier, % Complete. Color-coded by tier. "Grant Progress" button to increment counter. "Unlock Achievement" button to force-complete. Cumulative stats panel. |
| Statistics Module | `Modules/StatisticsModule.cs` | Aggregate statistics across all achievements: total defined, by category breakdown, tier distribution chart, average completion rate (play-mode), most/least completed achievements, estimated time-to-complete per category. |
| Validator | `Modules/ValidatorModule.cs` | 10 validation checks: duplicate AchievementIds, missing tiers (achievement with 0 tiers), non-ascending thresholds (Bronze > Silver), missing icons, unreferenced achievements (in database but no definition SO), orphan definitions (SO exists but not in database), invalid ConditionParam (EnemyTypeId/RecipeId not found), zero thresholds, duplicate names, hidden achievements without description. Green/yellow/red severity. "Fix All" auto-repair for trivial issues. |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `AchievementLink` | 8 bytes | Player entity |
| `AchievementChildTag` | 0 bytes | Child entity |
| `AchievementOwner` | 8 bytes | Child entity |
| `AchievementProgress` buffer header | ~16 bytes (header) + 768 bytes (64 * 12) | Child entity |
| `AchievementCumulativeStats` | 48 bytes | Child entity |
| `AchievementDirtyFlags` | 4 bytes | Child entity |
| **Total on player** | **8 bytes** | |

All achievement data lives on the child entity. The player archetype impact is exactly 8 bytes (`AchievementLink`), identical to `SaveStateLink`, `TalentLink`, and `CraftingKnowledgeLink`. No new `IBufferElementData` on the player entity. No new ghost-replicated buffers.

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `AchievementBootstrapSystem` | N/A | No | Runs once at startup |
| `AchievementInitializationSystem` | < 0.02ms | No | Only on new player join (rare) |
| `AchievementTrackingSystem` | < 0.05ms | No | Iterates event queries + progress buffer. Events are sparse (kills ~1-5/sec, quests/crafts very rare). Manual EntityQuery, not SystemAPI.Query |
| `AchievementUnlockSystem` | < 0.02ms | No | Only checks dirty achievements. Most frames: zero unlocks |
| `AchievementRewardSystem` | < 0.01ms | No | Only on unlock (very rare) |
| `AchievementCleanupSystem` | < 0.01ms | No | Destroys 0-1 entities per frame |
| `AchievementUIBridgeSystem` | < 0.03ms | No | Managed, queue drain + panel data push |
| **Total** | **< 0.15ms** | | All systems combined, worst case |

**Optimization notes:**
- `AchievementTrackingSystem` only iterates progress buffer entries matching the detected event type (not all 128 entries per event)
- Most frames: zero events detected -> early-out after EntityQuery.CalculateEntityCount() == 0
- `AchievementUnlockSystem` skips entries where `IsUnlocked == true` (fully complete)
- `AchievementDirtyFlags` prevents unnecessary save system work

---

## File Summary

### New Files (26)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Achievement/Components/AchievementLink.cs` | IComponentData (8 bytes on player) |
| 2 | `Assets/Scripts/Achievement/Components/AchievementComponents.cs` | IComponentData + IBufferElementData (child entity) |
| 3 | `Assets/Scripts/Achievement/Components/AchievementUnlockEvent.cs` | IComponentData (transient entity) |
| 4 | `Assets/Scripts/Achievement/Components/AchievementEnums.cs` | Enums (Category, ConditionType, Tier, RewardType) |
| 5 | `Assets/Scripts/Achievement/Data/AchievementBlobs.cs` | BlobAsset structs + Singleton |
| 6 | `Assets/Scripts/Achievement/Definitions/AchievementDefinitionSO.cs` | ScriptableObject |
| 7 | `Assets/Scripts/Achievement/Definitions/AchievementDatabaseSO.cs` | ScriptableObject |
| 8 | `Assets/Scripts/Achievement/Definitions/AchievementConfigSO.cs` | ScriptableObject |
| 9 | `Assets/Scripts/Achievement/Authoring/AchievementAuthoring.cs` | Baker (child entity pattern) |
| 10 | `Assets/Scripts/Achievement/Systems/AchievementBootstrapSystem.cs` | SystemBase (runs once) |
| 11 | `Assets/Scripts/Achievement/Systems/AchievementInitializationSystem.cs` | SystemBase (populates buffer on new player) |
| 12 | `Assets/Scripts/Achievement/Systems/AchievementTrackingSystem.cs` | SystemBase (reads events, increments progress) |
| 13 | `Assets/Scripts/Achievement/Systems/AchievementUnlockSystem.cs` | SystemBase (threshold checks, unlock events) |
| 14 | `Assets/Scripts/Achievement/Systems/AchievementRewardSystem.cs` | SystemBase (distributes rewards) |
| 15 | `Assets/Scripts/Achievement/Systems/AchievementCleanupSystem.cs` | ISystem (destroys transient entities) |
| 16 | `Assets/Scripts/Achievement/Bridges/AchievementVisualQueue.cs` | Static NativeQueue bridge |
| 17 | `Assets/Scripts/Achievement/Bridges/AchievementUIBridgeSystem.cs` | SystemBase (PresentationSystemGroup) |
| 18 | `Assets/Scripts/Achievement/Bridges/AchievementUIRegistry.cs` | Static provider registry |
| 19 | `Assets/Scripts/Achievement/Bridges/IAchievementUIProvider.cs` | Interface + UI data structs |
| 20 | `Assets/Scripts/Achievement/UI/AchievementToastView.cs` | MonoBehaviour (toast popup) |
| 21 | `Assets/Scripts/Achievement/UI/AchievementPanelView.cs` | MonoBehaviour (full panel) |
| 22 | `Assets/Scripts/Persistence/Modules/AchievementSaveModule.cs` | ISaveModule (TypeId=14) |
| 23 | `Assets/Scripts/Achievement/DIG.Achievement.asmdef` | Assembly definition |
| 24 | `Assets/Editor/AchievementWorkstation/AchievementWorkstationWindow.cs` | EditorWindow (4 tabs) |
| 25 | `Assets/Editor/AchievementWorkstation/IAchievementWorkstationModule.cs` | Interface |
| 26 | `Assets/Editor/AchievementWorkstation/Modules/DefinitionEditorModule.cs` | Module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | Player prefab (Warrok_Server) | Add `AchievementAuthoring` |
| 2 | `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs` | Add `Achievements = 14` constant |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/AchievementDatabase.asset` |
| 2 | `Resources/AchievementConfig.asset` |

---

## Cross-EPIC Integration

| Source | EPIC | Event | Achievement Response |
|--------|------|-------|---------------------|
| `DeathTransitionSystem` | Core | `KillCredited` on killer | Increment EnemyKill/EnemyKillByType/KillStreak achievements |
| `DeathTransitionSystem` | Core | `DiedEvent` on player | Increment PlayerDeath achievements, reset KillStreak |
| `LevelUpSystem` | 16.14 | `LevelUpEvent` on player | Check LevelReached achievements |
| `QuestCompletionSystem` | 16.12 | `QuestEventQueue` completion events | Increment QuestComplete achievements |
| `CraftOutputGenerationSystem` | 16.13 | Crafted item entities | Increment ItemCrafted/ItemCraftedByRecipe/CraftRareItem |
| `DeathLootSystem` | 16.6 | Loot creation events | Increment LootCollected/LootByRarity |
| `CombatResolutionSystem` | Core | `CombatResultEvent` entities | Accumulate DamageDealt |
| `DialogueInitiationSystem` | 16.16 | `DialogueSessionState` changes | Increment NPCInteraction/DialogueComplete |
| `XPGrantAPI` | 16.14 | XP reward distribution | Achievement rewards grant XP via existing API |
| `CurrencyInventory` | 16.6 | Gold reward distribution | Achievement rewards add gold via existing component |
| `CraftingKnowledgeLink` | 16.13 | Recipe unlock rewards | Achievement rewards unlock recipes via existing child entity |
| `ISaveModule` | 16.15 | TypeId=14 save/load | Achievement progress persisted across sessions |
| `TalentLink` | 17.1 | TalentPointsSpent condition | Track talent allocation for progression achievements |

---

## Multiplayer

### Server-Authoritative Model

- ALL achievement tracking and unlock logic runs on server (`ServerSimulation | LocalSimulation`). Clients NEVER write achievement progress.
- `AchievementLink` is `Ghost:AllPredicted` -- owning client can read progress for UI panel.
- Achievement progress buffer lives on a non-ghost child entity -- progress data is NOT replicated to remote clients.
- Remote clients see toast notifications via `AchievementVisualQueue` (local client only).
- `AchievementUIBridgeSystem` runs on `Client|Local` for UI feedback only.

### Anti-Exploit Considerations

- Progress increments validated server-side -- clients cannot send "increment achievement" RPCs.
- Kill streak counter resets on death (server-verified `DiedEvent`).
- `KillCredited` is created by `DeathTransitionSystem` (server authority) -- cannot be spoofed by client.
- XP and gold rewards go through existing server-authoritative pipelines (`XPGrantAPI`, `CurrencyInventory`).

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| Entity without `AchievementLink` | No achievement data | Zero overhead -- all systems skip |
| Empty `AchievementDatabaseSO` | No achievements loaded | Bootstrap logs warning, systems no-op |
| Old save without TypeId=14 block | Module skipped on load | Progress starts fresh (standard ISaveModule pattern) |
| No `IAchievementUIProvider` registered | Warning at frame 120 | Systems run, UI just doesn't display |
| New achievements added to database | Auto-initialized | `AchievementInitializationSystem` detects missing entries, appends to buffer |

---

## Verification Checklist

### Core Pipeline
- [ ] `AchievementAuthoring` on player prefab: child entity created with `AchievementProgress` buffer
- [ ] `AchievementBootstrapSystem` loads database SO, creates blob singleton with correct achievement count
- [ ] `AchievementInitializationSystem` populates progress buffer for new player with all achievement entries
- [ ] Kill enemy: `AchievementTrackingSystem` increments EnemyKill achievements by 1
- [ ] Kill specific enemy type: EnemyKillByType with matching ConditionParam incremented, others not
- [ ] Complete quest: QuestComplete achievements incremented after `QuestEventQueue` dequeue
- [ ] Level up: LevelReached achievements checked against new level
- [ ] Craft item: ItemCrafted achievements incremented after `CraftOutputGenerationSystem`
- [ ] Collect loot: LootCollected achievements incremented
- [ ] Deal damage: DamageDealt cumulative stat accumulates from `CombatResultEvent`
- [ ] Talk to NPC: NPCInteraction incremented on `DialogueSessionState` change

### Tier Progression
- [ ] Bronze threshold reached: `HighestTierUnlocked` set to 1, `AchievementUnlockEvent` created
- [ ] Silver threshold reached after Bronze: tier upgrades to 2, new unlock event
- [ ] Gold threshold reached: tier upgrades to 3
- [ ] Platinum threshold reached: tier upgrades to 4, `IsUnlocked` set to true
- [ ] Progress below Bronze: no unlock event, HighestTierUnlocked remains 0
- [ ] Multi-tier skip: if progress jumps from 0 to Gold threshold, all intermediate tiers unlock in order

### Rewards
- [ ] Gold reward: `CurrencyInventory.Gold` increases by reward amount on unlock
- [ ] XP reward: `XPGrantAPI.GrantXP()` called with correct amount and `XPSourceType.Bonus`
- [ ] TalentPoints reward: `PlayerProgression.UnspentStatPoints` increases
- [ ] RecipeUnlock reward: recipe added to `KnownRecipeElement` buffer via `CraftingKnowledgeLink`
- [ ] Each tier grants its own reward (Bronze reward != Gold reward)
- [ ] Reward distribution does not duplicate on re-login (persisted unlock state)

### Kill Streak
- [ ] Kill 5 enemies without dying: KillStreak achievements check `HighestKillStreak`
- [ ] Die: `CurrentKillStreak` resets to 0
- [ ] Kill 10, die, kill 7: `HighestKillStreak` remains 10
- [ ] KillStreak achievement with threshold 10 unlocks after first 10-streak

### Hidden Achievements
- [ ] Hidden achievement not visible in UI panel until unlocked
- [ ] Hidden achievement shows as "???" with locked icon
- [ ] Once unlocked: hidden achievement reveals name, description, icon
- [ ] `Config.EnableHiddenAchievements = false`: all achievements visible regardless

### Save/Load
- [ ] Save: `AchievementSaveModule` serializes progress buffer + cumulative stats
- [ ] Load: progress restored, counters resume from saved values
- [ ] Kill 50 enemies, save, restart, load: TotalKills = 50, EnemyKill progress = 50
- [ ] Old save without achievements: loads cleanly, progress starts fresh
- [ ] `AchievementDirtyFlags` cleared after save
- [ ] Achievement unlocked before save: `IsUnlocked` and `HighestTierUnlocked` persist

### UI
- [ ] Toast notification appears on achievement unlock with correct name, icon, tier badge
- [ ] Toast auto-dismisses after `Config.ToastDisplayDuration` seconds
- [ ] Multiple rapid unlocks: toasts queue and display sequentially
- [ ] Achievement panel shows all categories with correct counts
- [ ] Progress bars show CurrentValue / NextThreshold for each achievement
- [ ] Category filter works: selecting "Combat" shows only Combat achievements
- [ ] Completion counter: "X / Y Achievements (Z%)" accurate

### Multiplayer
- [ ] Achievement tracking server-authoritative: no client-side progress writes
- [ ] Remote clients do not see other players' achievement progress
- [ ] Local client sees own achievements via `AchievementLink` (AllPredicted)
- [ ] Achievement rewards (gold, XP) go through server-authoritative pipelines

### Performance
- [ ] Zero events frame: `AchievementTrackingSystem` early-outs (< 0.01ms)
- [ ] Kill event: tracking + unlock check < 0.05ms total
- [ ] 128 achievements in buffer: iteration completes within budget
- [ ] No allocation per frame (NativeQueue pre-allocated, buffer pre-sized)

### Editor Tooling
- [ ] Achievement Workstation: Definition Editor creates valid `AchievementDefinitionSO`
- [ ] Achievement Workstation: Progress Inspector shows live progress in play mode
- [ ] Achievement Workstation: Statistics Module shows category breakdown
- [ ] Achievement Workstation: Validator detects duplicate IDs, missing tiers, zero thresholds
- [ ] Achievement Workstation: Validator "Fix All" repairs trivial issues

### Archetype & Safety
- [ ] Only +8 bytes on player entity (`AchievementLink`), no ghost bake errors
- [ ] No new `IBufferElementData` on ghost-replicated entities
- [ ] No modification to any existing system (pure listener pattern)
- [ ] `CompleteDependency()` called in `AchievementTrackingSystem` (async job safety)
- [ ] Manual `EntityQuery` used for transient types (not `SystemAPI.Query`)
- [ ] Entity without `AchievementLink`: zero cost, all systems skip
