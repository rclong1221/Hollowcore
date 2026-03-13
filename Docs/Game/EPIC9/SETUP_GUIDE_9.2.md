# EPIC 9.2 Setup Guide: Compendium Entries

**Status:** Planned
**Requires:** EPIC 9.1 (Compendium child entity, CompendiumLink), Framework Roguelite/ (MetaUnlockTreeSO, MetaBank), Persistence/ (ISaveModule)

---

## Overview

Compendium Entries are permanent meta-progression unlocks that survive expedition wipes. Each entry represents discovered Hollowcore knowledge -- a new mission type, vendor, traversal shortcut, bestiary entry, or lore fragment with embedded gameplay hints. Entries use the framework's MetaUnlockTreeSO pattern with unlock conditions evaluated at extraction. The CompendiumEntrySaveModule (TypeId=24) persists entries permanently across all expeditions.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 9.1 | CompendiumAuthoring, CompendiumLink | Shared child entity hosts entry buffers |
| Framework Roguelite/ | MetaUnlockTreeSO, MetaBank | Meta-progression infrastructure |
| Framework Persistence/ | ISaveModule, SaveManager | Permanent persistence pipeline |
| EPIC 4 | ExtractionEvent | Triggers unlock condition evaluation |

### New Setup Required
1. Create `Assets/Scripts/Compendium/Persistence/` subfolder
2. Create CompendiumEntryDefinitionSO assets at `Assets/Data/Compendium/Entries/`
3. Add `CompendiumEntryState` and `CompendiumEntryNewFlag` buffers to CompendiumAuthoring baker
4. Register `CompendiumEntrySaveModule` (TypeId=24) in SaveManager
5. Wire ExtractionEvent from EPIC 4 to trigger CompendiumEntrySystem
6. Create at least 3 vertical-slice entries for testing

---

## 1. Creating Entry Definitions

**Create:** `Assets > Create > Hollowcore/Compendium/Entry Definition`
**Recommended location:** `Assets/Data/Compendium/Entries/{Category}/`

Organize by category subfolder: `Mission/`, `Vendor/`, `Traversal/`, `Lore/`, `Enemy/`, `District/`, `Boss/`

### 1.1 Identity Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `EntryId` | Unique ID across all entry definitions | -- | > 0, unique |
| `DisplayName` | Player-facing name | "" | Non-empty |
| `Description` | What this entry represents | "" | -- |
| `Icon` | Sprite for unlocked display | null | Required |
| `LockedIcon` | Silhouette sprite for locked display | null | Required |
| `Category` | Entry category for UI tab filtering | Mission | CompendiumEntryCategory enum |

### 1.2 CompendiumEntryCategory Enum
| Value | Description | Content Unlocked |
|-------|-------------|-----------------|
| `Mission` (0) | New mission types | Quest pool entries |
| `Vendor` (1) | NPC vendors | Vendor spawns in districts |
| `Traversal` (2) | Movement abilities/shortcuts | Player abilities |
| `Lore` (3) | Flavor text with mechanical hints | Information |
| `Enemy` (4) | Bestiary entries | Weakness icons on health bars |
| `District` (5) | District knowledge | Secret area map markers |
| `Boss` (6) | Boss dossiers | Phase transition hints |

### 1.3 Unlock Condition Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `ConditionType` | Type of unlock condition | DistrictComplete | CompendiumEntryUnlockCondition enum |
| `ConditionTargetId` | Target ID: district, boss, quest, enemy type, etc. | 0 | Context-dependent |
| `ConditionThreshold` | Required count or level for threshold conditions | 1 | >= 1 |

### 1.4 CompendiumEntryUnlockCondition Enum
| Value | Trigger | ConditionTargetId Meaning | ConditionThreshold Meaning |
|-------|---------|--------------------------|---------------------------|
| `DistrictComplete` (0) | Complete a district | District ID | 1 (complete once) |
| `BossDefeated` (1) | Kill a boss | Boss definition ID | 1 (kill once) |
| `EchoComplete` (2) | Finish an echo encounter | Echo encounter ID | 1 (complete once) |
| `TraceThreshold` (3) | Reach a Trace level | -- (ignored) | Trace level required |
| `HiddenContentFound` (4) | Discover hidden area | Hidden area ID | 1 (discover once) |
| `QuestComplete` (5) | Complete quest chain | Quest chain ID | 1 (complete once) |
| `EnemyKillCount` (6) | Kill N of enemy type | Enemy type ID | Kill count required |
| `ItemAcquired` (7) | Acquire specific item | Item definition ID | 1 (acquire once) |
| `MultiCondition` (8) | All sub-conditions met | -- | -- |

### 1.5 Multi-Condition Setup
For `MultiCondition` entries, populate the `SubConditions` array:

| Sub-Field | Description |
|-----------|-------------|
| `ConditionType` | Same enum as parent |
| `TargetId` | Condition-specific ID |
| `Threshold` | Required count |

All sub-conditions must be satisfied simultaneously.

**Tuning tip:** MultiCondition entries make good "achievement-style" unlocks (e.g., "Complete Necrospire AND defeat the boss AND find the hidden vault"). Use sparingly -- most entries should have simple single conditions.

### 1.6 Unlock Reward Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `RewardType` | What becomes available | None | CompendiumUnlockReward enum |
| `RewardTargetId` | ID of the content unlocked | 0 | Context-dependent |
| `RewardDescription` | Human-readable reward text for UI | "" | -- |

### 1.7 CompendiumUnlockReward Enum
| Value | Effect |
|-------|--------|
| `None` (0) | Pure lore, no mechanical unlock |
| `QuestPoolEntry` (1) | Adds mission type to quest pool |
| `VendorSpawn` (2) | Enables NPC vendor in target district |
| `TraversalAbility` (3) | Unlocks movement ability or shortcut |
| `EnemyWeakness` (4) | Permanently shows weakness icon on enemy health bars |
| `BossPhaseHint` (5) | Shows boss phase transitions in dossier |
| `DistrictSecret` (6) | Marks secret area on district map |

### 1.8 Lore Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `LoreText` | Full lore text (shown in detail panel) | "" | -- |
| `MechanicalHint` | Gameplay hint embedded in lore | "" | Optional |

---

## 2. Persistence Module

**File:** `Assets/Scripts/Compendium/Persistence/CompendiumEntrySaveModule.cs`

### 2.1 Registration
```
SaveManager.RegisterModule(new CompendiumEntrySaveModule());
```

### 2.2 Module Configuration
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `TypeId` | ISaveModule identifier | 24 | Unique |
| Persistence scope | Permanent -- survives expedition wipes | -- | -- |
| Format | Binary (.dig), CRC32, write-ahead .tmp safety | -- | -- |

### 2.3 Save Data
Serializes `List<CompendiumEntrySaveEntry>` containing `(EntryDefinitionId, Category)` per unlocked entry.

---

## 3. Entry Evaluation Flow

### 3.1 At Extraction (CompendiumEntrySystem)
1. ExtractionEvent triggers evaluation
2. Gather expedition stats: districts completed, bosses killed, echoes finished, Trace level, hidden areas found, quests completed, enemy kill counts, items acquired
3. For each definition NOT already in CompendiumEntryState buffer:
   - Evaluate unlock condition against stats
   - For MultiCondition: ALL sub-conditions must pass
   - If met: append to CompendiumEntryState + CompendiumEntryNewFlag
4. Apply unlock rewards (quest pool entries, vendor spawns, etc.)
5. Fire CompendiumUnlockEvent per new entry for UI
6. Mark CompendiumEntrySaveModule dirty

### 3.2 At Expedition Start (CompendiumEntryBootstrapSystem)
1. Load persisted entries from CompendiumEntrySaveModule
2. Populate CompendiumEntryState buffer
3. Clear CompendiumEntryNewFlag buffer
4. Apply all previously unlocked rewards

---

## 4. Recommended Starter Entries (Vertical Slice)

| EntryId | Category | Condition | Target | Reward |
|---------|----------|-----------|--------|--------|
| 1001 | District | DistrictComplete | Necrospire (ID=1) | DistrictSecret (reveals hidden vault) |
| 2001 | Boss | BossDefeated | First Boss (ID=1) | BossPhaseHint (phase breakdown) |
| 3001 | Lore | HiddenContentFound | Terminal A (ID=10) | None (lore with mechanical hint) |

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| CompendiumAuthoring baker | Add `CompendiumEntryState` + `CompendiumEntryNewFlag` buffers to child entity | Shared child with page buffers |
| Persistent subscene | CompendiumEntryDatabaseAuthoring (bakes all entry blobs) | For Burst-compatible condition evaluation |
| SaveManager bootstrap | Register CompendiumEntrySaveModule (TypeId=24) | Permanent persistence |
| `Assets/Data/Compendium/Entries/` | Entry definitions organized by category subfolders | At least 3 for vertical slice |
| Extraction flow | Wire ExtractionEvent to CompendiumEntrySystem | EPIC 4 integration |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| EntryId not unique | Wrong entry unlocked or duplicate shown | Validation enforces uniqueness across all entry definitions |
| CompendiumEntrySaveModule not registered | Entries lost on game restart | Register in SaveManager bootstrap (TypeId=24) |
| ConditionTargetId references nonexistent content | Entry never unlockable | Cross-reference validation checks district/boss/quest IDs |
| MultiCondition with < 2 sub-conditions | Unnecessary complexity (use single condition) | Validation warns; refactor to single condition |
| Entries evaluated before ExtractionEvent | Empty expedition stats, nothing unlocks | CompendiumEntrySystem must trigger ON ExtractionEvent, not every frame |
| CompendiumEntryNewFlag not cleared on expedition start | Stale "new" badges from previous expedition | CompendiumEntryBootstrapSystem clears the buffer |
| Reward not applied on expedition start | Previously unlocked vendor doesn't appear | CompendiumEntryBootstrapSystem must apply all stored rewards |
| 16KB archetype concern | -- | All buffers on child entity via CompendiumLink (no impact) |

---

## Verification

- [ ] CompendiumEntryState buffer exists on compendium child entity
- [ ] CompendiumEntryNewFlag buffer is empty at expedition start
- [ ] CompendiumEntrySystem fires on ExtractionEvent
- [ ] DistrictComplete condition unlocks entry after completing target district
- [ ] BossDefeated condition unlocks entry after killing target boss
- [ ] MultiCondition requires ALL sub-conditions satisfied
- [ ] Duplicate entries never added (already-owned check)
- [ ] CompendiumEntryNewFlag tracks entries unlocked this expedition only
- [ ] QuestPoolEntry reward adds quest to available pool for future expeditions
- [ ] VendorSpawn reward enables NPC in target district on next expedition
- [ ] CompendiumEntrySaveModule round-trip: serialize, deserialize, entries match (TypeId=24)
- [ ] Entries persist across expedition wipes (permanent meta-progression)
- [ ] Run Compendium simulation (50 expeditions) shows expected completion curve
