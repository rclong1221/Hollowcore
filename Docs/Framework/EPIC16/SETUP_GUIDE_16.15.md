# SETUP GUIDE 16.15: Save/Load & Persistence System

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Player entity with PlayerTag (existing), SaveConfig SO in Resources (new)

This guide covers Unity Editor setup for the modular save/load and persistence system. After setup, player progress (stats, inventory, equipment, quests, crafting knowledge, survival stats, status effects, progression, and world terrain) persists across sessions with automatic autosaving, checkpoint saves, crash-safe file I/O, and version migration.

---

## What Changed

Previously, all player progress was lost on quit. There was no save file, no autosave, no load screen, and no character portability between sessions.

Now:

- **10 save modules** -- each game subsystem (stats, inventory, equipment, quests, crafting, world terrain, settings, status effects, survival, progression) has its own serializer
- **Binary .dig files** with CRC32 validation, crash-safe write-ahead protocol, and per-module schema versioning
- **3 save slots** per player -- slot 0 reserved for autosave, slots 1-2 for manual saves
- **Autosave** every 5 minutes (configurable), plus checkpoint saves on quest completion
- **Async file I/O** -- writes happen on a background thread, zero frame hitches during gameplay
- **Crash recovery** -- write-ahead `.tmp` files with atomic rename prevent corruption from unexpected shutdowns
- **Version migration** -- per-module schema versions allow independent field evolution without breaking old saves
- **Multiplayer** -- server saves per-player `.dig` files plus a shared `.digw` world file. Players auto-restore state on reconnect
- **Character portability** -- player `.dig` files are self-contained and can be copied between server and single-player
- **Editor tooling** -- Persistence Workstation window with file browser, binary inspector, migration tester, and live state viewer

---

## What's Automatic (No Setup Required)

Once `SaveStateAuthoring` is on the player prefab and `SaveConfig` exists in Resources, all of the following works automatically:

| Feature | How It Works |
|---------|-------------|
| Module registration | PersistenceBootstrapSystem registers all 10 ISaveModule implementations at startup |
| Save directory creation | PersistenceBootstrapSystem creates `{persistentDataPath}/saves/` if missing |
| Background I/O thread | SaveFileWriter starts its thread on bootstrap, drains write queue asynchronously |
| Dirty tracking | SaveDirtyTrackingSystem monitors ECS change filters and marks modules dirty per frame |
| Player ID assignment | SaveIdAssignmentSystem reads GhostOwner.NetworkId and writes PlayerSaveId to the save child entity |
| Autosave | AutosaveSystem ticks a timer and creates SaveRequest entities at the configured interval |
| Checkpoint saves | CheckpointTriggerSystem watches for quest completions, creates SaveRequest with cooldown deduplication |
| Shutdown save | SaveOnQuitSystem.OnDestroy performs a blocking save before Unity exits |
| Reconnect restore | PlayerReconnectSaveSystem detects returning players and auto-loads their most recent save |
| CRC32 validation | LoadSystem validates file integrity before deserializing any module |
| Forward compatibility | Unknown module TypeIds in save files are silently skipped |
| Backward compatibility | Missing modules in old save files are silently ignored |
| Transient cleanup | SaveComplete and LoadComplete entities are created for UI feedback, then destroyed by SaveUIBridgeSystem |

---

## 1. SaveConfig Setup

The persistence system reads its configuration from a `SaveConfig` ScriptableObject that must exist in a Resources folder.

### 1.1 Create the Asset

1. In the Project window, navigate to (or create) `Assets/Resources/`
2. Right-click > **Create** > **DIG** > **Persistence** > **Save Config**
3. Name it `SaveConfig`

The file MUST be named `SaveConfig` and MUST be in a `Resources/` folder. PersistenceBootstrapSystem loads it via `Resources.Load<SaveConfig>("SaveConfig")`. If the asset is missing, a default configuration is created in memory and a warning is logged.

### 1.2 Inspector Fields

| Field | Header | Default | Description |
|-------|--------|---------|-------------|
| **Save Slot Count** | Save Slots | 3 | Maximum save slots per player (1-10). Slot 0 is reserved for autosave. |
| **Autosave Slot** | Save Slots | 0 | Which slot receives autosaves. Do not change unless you have a specific reason. |
| **Quicksave Slot** | Save Slots | -1 | Slot for quicksave. -1 means quicksave is disabled. |
| **Autosave Interval Seconds** | Autosave | 300 | Seconds between autosaves. Set to 0 to disable autosave entirely. |
| **Enable Checkpoint Save** | Checkpoint Save | true | Whether quest completions trigger a save. |
| **Checkpoint Cooldown** | Checkpoint Save | 30 | Minimum seconds between checkpoint saves. Prevents rapid-fire saves from quick quest completions. |
| **Shutdown Save Enabled** | Shutdown | true | Save when the application quits. Strongly recommended to leave enabled. |
| **Save Format Version** | Format | 1 | Only bump this when the binary file header layout changes. Module schemas version independently. |
| **Max World Delta Records** | World Data | 50000 | Maximum voxel terrain modification records. Older records are pruned before save. Min 1000. |
| **Compress World Data** | World Data | true | Apply GZip compression to the world save module block. Achieves 3-5x compression. |
| **Save Directory** | Directory | "saves" | Folder name relative to `Application.persistentDataPath`. |

### 1.3 Recommended Settings for Development

During development, lower the autosave interval for faster iteration:

| Field | Dev Value | Why |
|-------|-----------|-----|
| Autosave Interval Seconds | 30 | Frequent saves so you lose less progress when testing |
| Checkpoint Cooldown | 5 | Faster checkpoint saves when testing quest completion triggers |

Remember to restore production values before shipping.

---

## 2. Player Prefab Setup

The persistence system uses a child entity pattern to keep the player archetype under 16KB. You need to add one component to the player prefab.

### 2.1 Add SaveStateAuthoring

1. Open the player prefab: `Warrok_Server`
2. Select the **root** GameObject
3. Click **Add Component** > search for **Save State** (listed under DIG/Persistence)
4. The component has no inspector fields -- it is configuration-free

### 2.2 What the Baker Creates

At bake time, `SaveStateAuthoring` creates:

- **On the player entity:** `SaveStateLink` (8 bytes) -- a single Entity reference to the child
- **On a new child entity:** `SaveStateTag`, `SaveDirtyFlags`, `PlayerSaveId`, `SaveStateOwner`

This keeps the player archetype impact to exactly 8 bytes. All save metadata lives on the child entity.

### 2.3 After Adding

Right-click the SubScene containing the player prefab > **Reimport** to rebake. Ghost prefab changes require SubScene reimport to regenerate ghost serialization variants.

---

## 3. Save Slot Management

### 3.1 Slot Layout

| Slot | Purpose | Notes |
|------|---------|-------|
| Slot 0 | Autosave | Written automatically by AutosaveSystem, CheckpointTriggerSystem, and SaveOnQuitSystem. Also used for reconnect loads. |
| Slot 1 | Manual Save 1 | Written when the player explicitly saves from the menu or UI. |
| Slot 2 | Manual Save 2 | Second manual slot. |

The slot count is configurable via `SaveConfig.SaveSlotCount` (default 3, max 10).

### 3.2 File Naming

Save files follow this pattern:

```
{persistentDataPath}/{SaveDirectory}/player_{networkId}_slot{slotIndex}.dig
{persistentDataPath}/{SaveDirectory}/player_{networkId}_slot{slotIndex}.json
```

For example: `saves/player_1_slot0.dig` and `saves/player_1_slot0.json`.

In single-player (no NetworkId): `saves/player_local_slot0.dig`.

World saves use: `saves/world_{sessionId}.digw`.

### 3.3 Metadata Sidecars

Each `.dig` file has a companion `.json` sidecar containing human-readable metadata: player name, character level, playtime, save timestamp, game version, format version, module count, and module names. The editor tooling and any load screen UI reads this sidecar to display slot information without parsing the binary file.

---

## 4. What Gets Saved

The 10 built-in save modules cover all player-facing state:

| Module | Persisted Data |
|--------|---------------|
| **PlayerStats** | Health, Stamina, Shield, ResourcePool (2 slots), Currency (Gold/Premium/Crafting), CharacterAttributes (Str/Dex/Int/Vit/Level), playtime, position |
| **Inventory** | All InventoryItem entries (ResourceType + Quantity). Weight recalculated on load. |
| **Equipment** | Equipped items as content tuples: slot, item type, full ItemStatBlock (15 stats), optional WeaponDurability, ItemAffix buffer. Item entities are recreated on load. |
| **Quests** | CompletedQuestEntry buffer + active QuestInstance entities with QuestProgress and ObjectiveProgress |
| **Crafting** | KnownRecipeElement buffer (known recipe IDs) via CraftingKnowledgeLink child entity |
| **World** | Voxel terrain modification history (chunk/local position, density, material, tick). Optional GZip compression. |
| **Settings** | Placeholder for mouse sensitivity, volumes, input toggles (wired when GameSettings singleton exists) |
| **StatusEffects** | Active StatusEffect buffer entries (type, severity, time remaining, tick timer) |
| **Survival** | Hunger, Thirst, Oxygen, Sanity (+ DistortionIntensity), Infection current values |
| **Progression** | XP (current + total earned), UnspentStatPoints, RestedXP. Calculates offline rested XP accumulation on load. |

---

## 5. Manual Save/Load from Game Code

To trigger a save or load from gameplay code (e.g., a pause menu button), create a transient entity with the appropriate request component.

### 5.1 Triggering a Manual Save

```csharp
var entity = EntityManager.CreateEntity();
EntityManager.AddComponentData(entity, new SaveRequest
{
    SlotIndex = 1,                           // manual slot 1
    TriggerSource = SaveTriggerSource.Manual,
    TargetPlayerId = default                  // empty = save all connected players
});
```

The `SaveSystem` picks up the request next frame, serializes all modules, enqueues the write to the background thread, and creates a `SaveComplete` entity.

### 5.2 Triggering a Load

```csharp
var entity = EntityManager.CreateEntity();
EntityManager.AddComponentData(entity, new LoadRequest
{
    SlotIndex = 1,
    TargetPlayerId = default
});
```

The `LoadSystem` reads the file, validates CRC32, runs any needed migrations, deserializes all module blocks, and creates a `LoadComplete` entity.

### 5.3 Save Trigger Sources

| Source | When | Slot |
|--------|------|------|
| `Manual` | Player-initiated from UI | Any (typically 1 or 2) |
| `Autosave` | Timer interval reached | `SaveConfig.AutosaveSlot` (default 0) |
| `Checkpoint` | Quest completion (with cooldown dedup) | `SaveConfig.AutosaveSlot` |
| `Shutdown` | Application quit | `SaveConfig.AutosaveSlot` |
| `Reconnect` | Player reconnects after disconnect | (load only, not a save trigger) |

---

## 6. UI Integration

The persistence system bridges to MonoBehaviour UI through three provider interfaces and a static registry, following the same pattern as `CombatUIRegistry`.

### 6.1 Provider Interfaces

Implement whichever interfaces your UI needs:

**ISaveNotificationProvider** -- toast-style save/load feedback

| Method | Purpose |
|--------|---------|
| `ShowNotification(SaveNotification notification)` | Display a save/load notification. `notification.Type` tells you what happened (SaveCompleted, AutosaveCompleted, LoadCompleted, LoadFailed, etc.) |
| `HideNotification()` | Hide the currently visible notification |

**ISaveSlotProvider** -- load screen slot selection

| Method | Purpose |
|--------|---------|
| `RefreshSlots(SaveSlotInfo[] slots)` | Update the displayed save slots. Each `SaveSlotInfo` has: SlotIndex, IsOccupied, PlayerName, CharacterLevel, PlaytimeSeconds, LastSavedTimestamp, IsAutosaveSlot |
| `Show()` | Show the save slot selection panel |
| `Hide()` | Hide the save slot selection panel |

**ISaveProgressProvider** -- progress spinner during save/load

| Method | Purpose |
|--------|---------|
| `ShowProgress(string message)` | Show a progress indicator with a message (e.g., "Saving...") |
| `HideProgress()` | Hide the progress indicator |

### 6.2 Registration

In your MonoBehaviour's `OnEnable`, register with the static registry:

```csharp
SaveUIRegistry.RegisterNotifications(this);
```

In `OnDisable`, unregister:

```csharp
SaveUIRegistry.UnregisterNotifications(this);
```

Available registration methods:

| Method | Interface |
|--------|-----------|
| `SaveUIRegistry.RegisterNotifications(provider)` | ISaveNotificationProvider |
| `SaveUIRegistry.RegisterSlots(provider)` | ISaveSlotProvider |
| `SaveUIRegistry.RegisterProgress(provider)` | ISaveProgressProvider |

Corresponding `Unregister` methods exist for each.

### 6.3 Notification Types

The `SaveNotificationType` enum tells your UI what happened:

| Type | Meaning |
|------|---------|
| `SaveStarted` | Save operation began |
| `SaveCompleted` | Manual save succeeded |
| `SaveFailed` | Save operation failed |
| `LoadStarted` | Load operation began |
| `LoadCompleted` | Load succeeded |
| `LoadFailed` | Load failed (CRC error, missing file, etc.) |
| `AutosaveCompleted` | Autosave succeeded (use for subtle spinner) |
| `CheckpointSaved` | Checkpoint save succeeded |

### 6.4 Diagnostic Warning

If no `ISaveNotificationProvider` is registered after ~2 seconds of play, the console logs a one-time warning: `"[Persistence] No ISaveNotificationProvider registered."` This is safe to ignore if you haven't built save UI yet.

---

## 7. Editor Tooling

### 7.1 Opening the Workstation

Menu: **DIG** > **Persistence Workstation**

The window has 4 tabs across the top: Browser, Inspector, Migration, Live.

### 7.2 Save Browser Tab

Lists all `.dig` files in a configurable save directory. For each file:

- Filename (bold)
- File size and last modified timestamp

Actions per file:
- **Inspect** -- switches to the Inspector tab with the file loaded
- **Delete** -- deletes the `.dig` file and its `.json` sidecar (with confirmation dialog)

When a file is selected, the **Metadata (JSON Sidecar)** section displays the full JSON contents below the file list.

Controls:
- **Save Directory** text field with **Browse** button to pick a folder
- **Refresh** button to rescan the directory

### 7.3 Save Inspector Tab

Parses and displays the binary structure of a selected `.dig` file:

- **CRC32 status** -- green "VALID" or red "INVALID / CORRUPTED" label
- **Header section** -- Magic bytes (DIGS/DIGW), format version, timestamp (UTC), player name, stored CRC32, module count
- **Module Blocks** -- each module listed with TypeId, human-readable name, version, and data length in bytes
- **EOF Marker** -- VALID (DEND) or INVALID

To inspect a file, click **Inspect** from the Browser tab, or load one directly.

### 7.4 Migration Tester Tab

For testing save version migration:

1. Select a `.dig` file using the file picker
2. Click **Test Migration (Dry Run)** to see what would change without writing anything
3. Click **Apply Migration** to run the migration and overwrite the file

The result log shows: file version, target version, output size, and success/failure status.

### 7.5 Live State Tab (Play Mode Only)

Active only during Play Mode. Shows:

- **Configuration** -- save directory, initialization status, slot count, autosave slot and interval
- **Timing** -- elapsed playtime, time since last save, time since last checkpoint
- **Registered Modules** -- list of all 10 modules with TypeId, display name, and version
- **Save State Entities** -- count of active SaveState child entities
- **Quick Actions:**
  - **Force Save (Slot 0)** -- creates a SaveRequest entity immediately
  - **Force Load (Slot 0)** -- creates a LoadRequest entity immediately
  - **Open Save Directory** -- opens the save folder in Finder/Explorer

---

## 8. Version Migration

When you change the serialized field layout of a save module, you need to handle old saves.

### 8.1 Module-Level Migration (Most Common)

For changes within a single module:

1. Increment the module's `ModuleVersion` property
2. In the module's `Deserialize` method, check the `blockVersion` parameter
3. Read fields according to the old schema, fill defaults for new fields

Example: Adding a new `RestedXPMultiplier` field to ProgressionSaveModule:

```csharp
public int ModuleVersion => 2;  // was 1

public void Deserialize(in LoadContext context, BinaryReader reader, int blockVersion)
{
    var progression = new PlayerProgression
    {
        CurrentXP = reader.ReadInt32(),
        TotalXPEarned = reader.ReadInt32(),
        UnspentStatPoints = reader.ReadInt32(),
        RestedXP = reader.ReadSingle(),
    };

    // New in V2:
    if (blockVersion >= 2)
        progression.RestedXPMultiplier = reader.ReadSingle();
    else
        progression.RestedXPMultiplier = 1.0f;  // default for old saves

    context.EntityManager.SetComponentData(context.PlayerEntity, progression);
}
```

### 8.2 File-Level Migration (Rare)

For changes to the file header structure or module block layout:

1. Create a class implementing `IMigrationStep` with `FromVersion`, `ToVersion`, and `Migrate(byte[])` method
2. Register the step in PersistenceBootstrapSystem via `SaveMigrationRunner.Register()`
3. LoadSystem calls `SaveMigrationRunner.MigrateToLatest()` which chains all applicable steps in order (V1->V2->V3...)

### 8.3 Important Rules

- **NEVER** change a module's TypeId. TypeIds are the stable key linking save blocks to their deserializers.
- **ALWAYS** increment ModuleVersion when changing a module's serialized field layout.
- **ALWAYS** handle old versions in Deserialize with `blockVersion` checks.
- **NEVER** remove fields from the Serialize path without incrementing the version and handling the old format in Deserialize.
- Test migrations using the **Migration Tester** tab in the Persistence Workstation.

---

## 9. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Bootstrap | Enter Play Mode | Console: "Persistence system initialized with 10 modules". Save directory created. |
| 3 | Save file creation | Force save via Live State tab or manual SaveRequest | `player_{id}_slot0.dig` and `.json` appear in saves directory |
| 4 | Load restores health | Set health to 50, save, set to 100, load | Health restored to 50 |
| 5 | Inventory persistence | Collect 20 Stone + 10 Metal, save, drop all, load | Quantities restored |
| 6 | Equipment persistence | Equip weapon with affixes and 70% durability, save, quit, reload | Item entity recreated with correct stats, affixes, durability |
| 7 | Status effects | Apply Bleed, save, clear effects, load | Bleed restored with correct remaining duration |
| 8 | Survival stats | Set Hunger=50, save, reset to max, load | Hunger restored to 50 |
| 9 | Autosave fires | Set interval to 10s in SaveConfig, wait 10s in Play Mode | Autosave slot updated |
| 10 | Checkpoint save | Complete a quest | Save triggered (if cooldown has passed) |
| 11 | Shutdown save | Exit Play Mode | Save file written before Unity stops |
| 12 | CRC32 validation | Hex-edit 1 byte in a .dig file, try to load | Load rejected; console error about CRC32 |
| 13 | Persistence Workstation | Open DIG > Persistence Workstation | All 4 tabs render without errors |
| 14 | Inspector tab | Select a .dig file in Browser, click Inspect | Header, CRC status, and module list displayed |

---

## 10. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| "SaveConfig not found" warning on startup | SaveConfig asset missing from Resources folder | Create via **Create > DIG > Persistence > Save Config** in a `Resources/` folder. Must be named exactly `SaveConfig`. |
| Save files not appearing | Save directory doesn't exist or permissions error | Check `SaveConfig.SaveDirectory`. PersistenceBootstrapSystem should create it automatically. Check console for I/O errors. |
| Player state not saved | SaveStateAuthoring missing from player prefab | Add **Save State** component to Warrok_Server root. Reimport SubScene. |
| PlayerSaveId stays empty | GhostOwner not yet assigned | Normal for first frame. SaveIdAssignmentSystem retries each frame. If never populated, check that the player has GhostOwner component. |
| Equipment not restored on load | ItemRegistryManaged singleton not yet created | EquipmentSaveModule skips gracefully. Ensure ItemRegistryBootstrapSystem is running. |
| CRC32 validation fails on valid save | File was modified outside the game, or write was interrupted | Delete the corrupted file. If a `.tmp` file exists alongside it, the system will attempt automatic recovery on next load. |
| Autosave not firing | AutosaveIntervalSeconds set to 0 | Set to a positive value (default 300). |
| No save/load UI feedback | No ISaveNotificationProvider registered | Implement and register a MonoBehaviour with `SaveUIRegistry.RegisterNotifications(this)` in OnEnable. |
| Crafting knowledge not saved | CraftingKnowledgeLink missing on player | CraftingSaveModule gracefully skips if the link is absent. Ensure CraftingKnowledgeAuthoring is on a child of the player prefab. |
| Quest progress not saved | Quest entities not found | QuestSaveModule gracefully skips. Ensure EPIC 16.12 quest system is set up. |

---

## 11. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Player health, stamina, shields, resource pools | SETUP_GUIDE_16.8 (Resource Framework) |
| Inventory and equipment items | SETUP_GUIDE_16.6 (Loot, Economy, Item Affix) |
| Quest progress and completion | SETUP_GUIDE_16.12 (Quests) |
| Crafting knowledge and recipes | SETUP_GUIDE_16.13 (Crafting) |
| XP, levels, stat points | SETUP_GUIDE_16.14 (Progression) |
| Voxel terrain modifications | Voxel system documentation |
| **Save/Load & Persistence** | **This guide (16.15)** |
