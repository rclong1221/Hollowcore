# EPIC 16.15: Save/Load & Persistence System

**Status:** IMPLEMENTED
**Priority:** High (Core Infrastructure)
**Dependencies:**
- `Health` IComponentData (existing -- `Player.Components`, Ghost:All, 8 bytes)
- `PlayerStamina` IComponentData (existing -- `Player.Components`, AllPredicted, Current/Max/RegenRate/DrainRate)
- `ShieldComponent` IComponentData (existing -- `Player.Components`, AllPredicted, Current/Max)
- `ResourcePool` IComponentData (existing -- `DIG.Combat.Resources`, EPIC 16.8, AllPredicted, 64 bytes)
- `PlayerHunger` / `PlayerThirst` / `PlayerOxygen` / `PlayerSanity` / `PlayerInfection` (existing -- survival stats)
- `StatusEffect` IBufferElementData (existing -- `DIG.StatusEffects`, InternalBufferCapacity=8)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, EPIC 16.6, AllPredicted, Gold/Premium/Crafting)
- `InventoryItem` IBufferElementData (existing -- `DIG.Shared`, AllPredicted, InternalBufferCapacity=8)
- `InventoryCapacity` IComponentData (existing -- `DIG.Shared`, MaxWeight/CurrentWeight)
- `EquippedItemElement` IBufferElementData (existing -- `DIG.Items`, Ghost:All, equipment slots)
- `CharacterItem` IComponentData (existing -- `DIG.Items`, on item entities, ItemTypeId/SlotId/OwnerEntity/State)
- `ItemStatBlock` IComponentData (existing -- `DIG.Items`, ~60 bytes of stats per item entity)
- `ItemAffix` IBufferElementData (existing -- `DIG.Items`, AffixTypeId/StatType/Value on item entities)
- `WeaponDurability` IComponentData (existing -- `Player.Components`, on item entities, Current/IsBroken)
- `CharacterAttributes` IComponentData (existing -- `DIG.Combat.Components`, Ghost:All, Str/Dex/Int/Vit/Level)
- `VoxelModificationHistory` managed class (existing -- `DIG.Voxel.Core`, server-side terrain delta log)
- `PlayerProgression` IComponentData (existing -- EPIC 16.14, CurrentXP/TotalXPEarned/UnspentStatPoints/RestedXP)
- `QuestProgress` / `CompletedQuestEntry` / `ObjectiveProgress` (existing -- EPIC 16.12)
- `CraftingKnowledgeLink` / `KnownRecipeElement` (existing -- EPIC 16.13)
- `TargetingModuleLink` (existing -- child entity pattern reference)
- `ItemRegistryBootstrapSystem` (existing -- bootstrap singleton pattern reference)
- `LootSpawnSystem` (existing -- item entity creation via ECB pattern reference)
- `GhostOwner.NetworkId` (existing -- NetCode player identity)

**Feature:** A modular, version-aware binary save/load system where each game subsystem owns its own `ISaveModule` serializer. Uses `BinaryWriter`/`BinaryReader` (System.IO) for managed serialization, async file I/O to prevent frame hitches, CRC32 validation for corruption detection, crash-safe write-ahead protocol, and per-module schema versioning for independent migration. The server saves world state and per-player data; characters are portable to single-player. Only 8 bytes added to player archetype via `SaveStateLink` pointing to a child entity.

---

## Architecture Overview

```
Designer configures SaveConfig SO (autosave interval, slot count, compression)
                                        |
                         PersistenceBootstrapSystem (InitializationSystemGroup)
                         +-- registers all ISaveModule implementations (10 built-in)
                         +-- creates SaveManagerSingleton
                         +-- starts SaveFileWriter background thread
                         +-- ensures save directory exists
                                        |
Runtime triggers:
  Manual (menu/key) --------------------+
  Autosave timer (AutosaveSystem) ------+---> SaveRequest (transient entity)
  Checkpoint (CheckpointTriggerSystem) -+
  App quit (SaveOnQuitSystem.OnDestroy) +
                                        |
                         SaveSystem (SimulationSystemGroup, Server|Local)
                         +-- foreach ISaveModule: module.Serialize(context, writer) -> byte[]
                         +-- writes header: Magic + Version + CRC32 + Timestamp + PlayerName
                         +-- writes module blocks: [TypeId + ModuleVersion + DataLength + Data] * N
                         +-- writes EOF marker: 0x44454E44 ("DEND")
                         +-- hands byte[] to SaveFileWriter (async background thread)
                         +-- creates SaveComplete entity
                                        |
                         LoadSystem (SimulationSystemGroup, Server|Local)
                         +-- reads binary, validates Magic + CRC32
                         +-- runs SaveMigrationRunner if FormatVersion < current
                         +-- foreach module block: dispatch to ISaveModule.Deserialize()
                         +-- creates LoadComplete entity
                                        |
                         SaveUIBridgeSystem (PresentationSystemGroup, Client|Local)
                         -> SaveUIRegistry -> ISaveUIProvider -> MonoBehaviour UI
```

### Key Design: ISaveModule Pattern

No single system knows how to serialize the entire game state. Each subsystem owns an `ISaveModule` implementation that reads ECS components and writes a self-describing binary block. The `SaveSystem` is an orchestrator -- it calls every registered module and wraps results in a versioned envelope. **Adding a new system's save support requires only one new ISaveModule -- no changes to SaveSystem, LoadSystem, or any other module.**

Each ISaveModule declares:
- `TypeId` -- stable numeric identifier written into save block headers, MUST NOT CHANGE across versions
- `DisplayName` -- human-readable name for editor tooling and logging
- `ModuleVersion` -- per-module schema version, incremented when fields change
- `Serialize(SaveContext, BinaryWriter)` -- reads ECS state, writes bytes
- `Deserialize(LoadContext, BinaryReader, blockVersion)` -- reads bytes, applies ECS state via ECB
- `IsDirty(SaveContext)` -- optional dirty check (default: always dirty)

### Entity Handle Problem

ECS `Entity` values are unstable across sessions (IDs are reassigned at startup). The save system never writes raw Entity values. Instead:
- Equipment is serialized as `(ItemTypeId, SlotId, QuickSlot, ItemStatBlock, WeaponDurability, ItemAffix[])` tuples
- On load, `EquipmentSaveModule` recreates item entities via ECB (same path as `LootSpawnSystem`)
- Quest instance entities, crafting knowledge entities follow the same recreate-on-load pattern

### Child Entity Pattern

The persistence system follows the `TargetingModuleLink` pattern to stay within the player entity's 16KB archetype limit. A `SaveStateLink` (8 bytes) on the player entity points to a child entity that holds all save metadata (`SaveStateTag`, `SaveDirtyFlags`, `PlayerSaveId`, `SaveStateOwner`). This keeps the player archetype impact to exactly 8 bytes.

---

## Binary File Format Specification

### Player Save File (.dig)

```
Offset  Size    Field                Description
------  ----    -----                -----------
0x00    4       Magic                0x44 0x49 0x47 0x53 ("DIGS")
0x04    4       FormatVersion        int32, monotonically increasing
0x08    8       Timestamp            int64, Unix epoch UTC milliseconds
0x10    64      PlayerName           FixedString64Bytes (UTF-8, null-padded)
0x50    4       Checksum             uint32, CRC32 of all bytes AFTER this field
0x54    2       ModuleCount          int16, number of module blocks following
0x56    ...     ModuleBlocks[]       sequential module blocks (see below)
...     4       EOF                  0x44 0x45 0x4E 0x44 ("DEND")

Per ModuleBlock:
Offset  Size    Field                Description
------  ----    -----                -----------
+0x00   4       TypeId               int32, ISaveModule.TypeId (stable across versions)
+0x04   2       ModuleVersion        int16, per-module schema version
+0x06   4       DataLength           int32, byte count of Data (excludes this header)
+0x0A   N       Data                 module-specific binary payload
```

Total header overhead: 86 bytes + 10 bytes per module block header.

### World Save File (.digw)

Identical header structure but Magic = `0x44 0x49 0x47 0x57` ("DIGW"). Contains only `WorldSaveModule` (TypeId=6) block. Optional GZip compression (flag byte `0x01` prepended to Data if compressed, `0x00` if raw).

### Metadata Sidecar (.json)

Written alongside every `.dig`/`.digw` via `JsonUtility.ToJson()`:

```
SaveMetadata ([Serializable] class)
  SlotIndex          : int
  PlayerName         : string
  CharacterLevel     : int
  PlaytimeSeconds    : float
  SaveTimestampUtcMs : long
  GameVersion        : string     // Application.version
  SaveFormatVersion  : int
  ModuleCount        : int
  ModuleNames        : string[]   // human-readable, for editor display
  ThumbnailBase64    : string     // 64x36 JPEG, optional
```

### CRC32 Validation

Standard polynomial `0xEDB88320`. Checksum stored at offset `0x50` in the file header. Computed over all bytes AFTER the checksum field. Validated on load -- if the computed CRC32 does not match the stored value, the load is rejected with a corruption error.

### Crash Safety: Write-Ahead Protocol

1. Serialize to `byte[]` on main thread (fast -- <1ms for typical player data)
2. Hand to `SaveFileWriter.EnqueueWrite()` (background thread via ConcurrentQueue)
3. Background: write to `{filename}.tmp`
4. Background: compute CRC32, patch into header at offset 0x50
5. Background: validate by re-reading CRC32
6. Background: `File.Move("{filename}.tmp", "{filename}.dig")` -- atomic rename
7. Background: write `.json` sidecar (non-critical, informational only)
8. Background: delete `.tmp` on success

On load: if `.dig` is missing but `.tmp` exists, attempt recovery (validate CRC32, promote if valid).

---

## ECS Components

### On Player Entity (8 bytes total)

**File:** `Assets/Scripts/Persistence/Components/SaveStateComponents.cs`

```
SaveStateLink (IComponentData, Ghost:AllPredicted)
  SaveChildEntity : Entity    // link to child entity with save metadata
```

### On Save State Child Entity (not on player archetype)

```
SaveStateTag (IComponentData)
  -- marker tag, zero bytes

SaveDirtyFlags (IComponentData)
  Flags : uint    // bitmask, bit N = module TypeId (N+1) is dirty

PlayerSaveId (IComponentData)
  PlayerId : FixedString64Bytes    // stable ID from GhostOwner.NetworkId

SaveStateOwner (IComponentData)
  Owner : Entity    // back-reference to player entity
```

### Transient Request Components

**File:** `Assets/Scripts/Persistence/Components/PersistenceRequestComponents.cs`

```
SaveTriggerSource enum: Manual(0), Autosave(1), Checkpoint(2), Shutdown(3), Reconnect(4)

SaveRequest (IComponentData)
  SlotIndex       : int
  TriggerSource   : SaveTriggerSource
  TargetPlayerId  : FixedString64Bytes    // empty = save all connected players

LoadRequest (IComponentData)
  SlotIndex       : int
  TargetPlayerId  : FixedString64Bytes

SaveComplete (IComponentData)
  SlotIndex       : int
  Success         : bool
  ErrorMessage    : FixedString128Bytes
  TriggerSource   : SaveTriggerSource

LoadComplete (IComponentData)
  SlotIndex       : int
  Success         : bool
  ErrorMessage    : FixedString128Bytes
```

### Managed Singleton

**File:** `Assets/Scripts/Persistence/Components/SaveManagerSingleton.cs`

```
SaveManagerSingleton (class, IComponentData)
  RegisteredModules     : List<ISaveModule>
  ModuleByTypeId        : Dictionary<int, ISaveModule>
  SaveDirectory         : string
  Config                : SaveConfig
  ElapsedPlaytime       : float
  TimeSinceLastSave     : float
  TimeSinceLastCheckpoint : float    // for 30s dedup cooldown
  IsInitialized         : bool
```

---

## ISaveModule Interface

**File:** `Assets/Scripts/Persistence/Core/ISaveModule.cs`

```csharp
public interface ISaveModule
{
    /// Stable numeric identifier in save block headers. MUST NOT CHANGE across versions.
    int TypeId { get; }

    /// Human-readable name for editor tooling and logging.
    string DisplayName { get; }

    /// Current schema version. Increment when adding/removing/reordering fields.
    int ModuleVersion { get; }

    /// Read ECS state, write bytes. Returns byte count (0 = skip this module).
    int Serialize(in SaveContext context, BinaryWriter writer);

    /// Read bytes, apply ECS state via ECB. blockVersion may differ from current ModuleVersion.
    void Deserialize(in LoadContext context, BinaryReader reader, int blockVersion);

    /// Optional: true if unsaved changes exist. Default returns true (always dirty).
    bool IsDirty(in SaveContext context) => true;
}

public readonly struct SaveContext
{
    public readonly EntityManager EntityManager;
    public readonly Entity PlayerEntity;
    public readonly int FormatVersion;
    public readonly float ElapsedPlaytime;
    public readonly uint ServerTick;
}

public readonly struct LoadContext
{
    public readonly EntityManager EntityManager;
    public readonly Entity PlayerEntity;
    public readonly EntityCommandBuffer ECB;
    public readonly int FormatVersion;
}
```

---

## ISaveModule Implementations (10 Modules)

### Module TypeId Registry

**File:** `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs`

```
PlayerStats     = 1    // Health, Stamina, Shield, ResourcePool, Currency, Attributes
Inventory       = 2    // InventoryItem buffer + InventoryCapacity
Equipment       = 3    // EquippedItemElement + item entities + WeaponDurability
Quests          = 4    // CompletedQuestEntry + active QuestInstance entities
Crafting        = 5    // KnownRecipeElement via child entity
World           = 6    // VoxelModificationHistory deltas (GZip optional)
Settings        = 7    // Input preferences, audio volumes
StatusEffects   = 8    // Active StatusEffect buffer entries
Survival        = 9    // Hunger, Thirst, Oxygen, Sanity, Infection
Progression     = 10   // XP, stat points, rested XP (EPIC 16.14)
// 11-127: reserved for future modules
```

### 1. PlayerStatsSaveModule (TypeId=1, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/PlayerStatsSaveModule.cs`

Serializes core player combat stats, currency, character attributes, playtime, and position.

| Order | Field | Source | Type | Bytes |
|-------|-------|--------|------|-------|
| 1 | Health.Current | `Health` | float32 | 4 |
| 2 | Health.Max | `Health` | float32 | 4 |
| 3 | Stamina.Current | `PlayerStamina` | float32 | 4 |
| 4 | Stamina.Max | `PlayerStamina` | float32 | 4 |
| 5 | Stamina.RegenRate | `PlayerStamina` | float32 | 4 |
| 6 | Stamina.DrainRate | `PlayerStamina` | float32 | 4 |
| 7 | Shield.Current | `ShieldComponent` | float32 | 4 |
| 8 | Shield.Max | `ShieldComponent` | float32 | 4 |
| 9 | ResourcePool.Slot0 (Type, Current, Max, RegenRate) | `ResourcePool` | byte + 3xfloat32 | 13 |
| 10 | ResourcePool.Slot1 | `ResourcePool` | byte + 3xfloat32 | 13 |
| 11 | CurrencyInventory (Gold, Premium, Crafting) | `CurrencyInventory` | 3xint32 | 12 |
| 12 | CharacterAttributes (Str, Dex, Int, Vit, Level) | `CharacterAttributes` | 5xint32 | 20 |
| 13 | ElapsedPlaytime | `SaveContext` | float32 | 4 |
| 14 | SpawnPosition | `LocalTransform.Position` | 3xfloat32 | 12 |
| **Total** | | | | **~106 bytes** |

On deserialize: `EntityManager.SetComponentData<T>()` directly. No entity creation needed.

### 2. InventorySaveModule (TypeId=2, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/InventorySaveModule.cs`

```
Count : int16 (max 8)
foreach:
  ResourceType : byte (DIG.Shared.ResourceType -- Stone/Metal/BioMass/Crystal/etc.)
  Quantity     : int32
```

Max: 2 + 8 * 5 = 42 bytes. On deserialize: clear `InventoryItem` buffer, re-add. Recompute `InventoryCapacity.CurrentWeight`.

### 3. EquipmentSaveModule (TypeId=3, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/EquipmentSaveModule.cs`

Most complex module. Entity handles replaced with stable content identifiers.

```
ItemCount      : byte (typically 2-6)
foreach item:
  SlotIndex    : int32
  QuickSlot    : int32
  ItemTypeId   : int32
  ItemCategory : byte
  ItemState    : byte
  --- ItemStatBlock: 15 x float32 = 60 bytes ---
  HasDurability: byte (0 or 1)
  if HasDurability:
    DurabilityCurrent : float32
    IsBroken          : byte
  AffixCount   : byte (0-4)
  foreach affix:
    AffixId    : int32
    Slot       : byte (AffixSlot)
    Value      : float32
    Tier       : int32
```

On deserialize (critical path):
1. Require `ItemRegistryManaged` singleton (defer one frame if missing)
2. Clear `EquippedItemElement` buffer on player
3. For each item: create entity via ECB (same path as `LootSpawnSystem`)
4. Add `CharacterItem`, `ItemStatBlock`, optional `WeaponDurability`, `ItemAffix` buffer
5. Append `EquippedItemElement { ItemEntity, QuickSlot }` to player buffer
6. `EquippedStatsSystem` auto-recomputes `PlayerEquippedStats` next frame

### 4. QuestSaveModule (TypeId=4, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/QuestSaveModule.cs`

Requires EPIC 16.12. Graceful no-op if types not registered in world.

```
CompletedCount : int16
foreach: QuestId(int32) + CompletionTimestamp(int64)
ActiveCount    : int16
foreach: QuestId(int32) + QuestProgress fields + ObjectiveProgress buffer
```

On deserialize: restore buffer on player, recreate quest instance entities via ECB.

### 5. CraftingSaveModule (TypeId=5, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/CraftingSaveModule.cs`

Requires EPIC 16.13. Graceful skip if `CraftingKnowledgeLink` absent.

```
Count : int16
foreach: RecipeId (int32)
```

On deserialize: locate child entity via `CraftingKnowledgeLink`, clear buffer, re-add.

### 6. WorldSaveModule (TypeId=6, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/WorldSaveModule.cs`

Reads `VoxelModificationHistory`. Written to `.digw` world file (not player file).

```
CompressionFlag : byte (0x00=raw, 0x01=GZip)
RecordCount     : int32
foreach record:
  ChunkPos      : int3 (3xint32)
  LocalPos      : int3 (3xint32)
  NewDensity    : byte
  NewMaterial   : byte
  ServerTick    : uint32
```

Per record: 30 bytes. 50K records = ~1.5 MB uncompressed. GZip achieves 3-5x compression.

Cap: `SaveConfig.MaxWorldDeltaRecords` triggers `VoxelModificationHistory.Prune()` before serialize.

On deserialize: call `VoxelModificationHistory.RecordModification()` per record, flag chunks for remesh.

### 7. SettingsSaveModule (TypeId=7, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/SettingsSaveModule.cs`

Does NOT interact with ECS ghost components. Reads/writes managed `GameSettings` class. Placeholder for future expansion.

```
MouseSensitivityX/Y : 2xfloat32
MasterVolume/MusicVolume/SFXVolume : 3xfloat32
InvertYAxis/CrouchToggle/ProneToggle : 3xbyte
KeybindCount : int16
foreach: ActionName(FixedString64) + KeyCode(int32)
```

### 8. StatusEffectsSaveModule (TypeId=8, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/StatusEffectsSaveModule.cs`

Persists active status effects so players who quit mid-bleed resume with bleed.

```
Count          : byte (0-8, matches InternalBufferCapacity)
foreach effect:
  Type         : byte (StatusEffectType)
  Severity     : float32
  TimeRemaining: float32
  TickTimer    : float32
```

Max: 1 + 8 * 13 = 105 bytes. On deserialize: clear buffer, re-add each element.

### 9. SurvivalSaveModule (TypeId=9, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/SurvivalSaveModule.cs`

Only `Current` values persisted (Max/rates are design-time constants from authoring).

| Order | Field | Source | Type | Bytes |
|-------|-------|--------|------|-------|
| 1 | Hunger.Current | `PlayerHunger` | float32 | 4 |
| 2 | Thirst.Current | `PlayerThirst` | float32 | 4 |
| 3 | Oxygen.Current | `PlayerOxygen` | float32 | 4 |
| 4 | Sanity.Current | `PlayerSanity` | float32 | 4 |
| 5 | Sanity.DistortionIntensity | `PlayerSanity` | float32 | 4 |
| 6 | Infection.Current | `PlayerInfection` | float32 | 4 |
| **Total** | | | | **24 bytes** |

### 10. ProgressionSaveModule (TypeId=10, ModuleVersion=1)

**File:** `Assets/Scripts/Persistence/Modules/ProgressionSaveModule.cs`

Separated from PlayerStats for independent version lifecycle.

```
CurrentXP         : int32
TotalXPEarned     : int32
UnspentStatPoints : int32
RestedXP          : float32
LastLogoutTime    : int64 (for rested XP accumulation on load)
```

On deserialize: `SetComponentData<PlayerProgression>` if component exists, else skip. Computes offline rested XP accumulation from `LastLogoutTime` to `DateTime.UtcNow`.

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Local):
  PersistenceBootstrapSystem               -- registers ISaveModules, creates singleton (runs once)

SimulationSystemGroup (Server|Local):
  [OrderFirst]
  SaveDirtyTrackingSystem                  -- change filter monitors -> dirty flags
  SaveIdAssignmentSystem                   -- populates PlayerSaveId from GhostOwner.NetworkId
  SaveSystem                               -- processes SaveRequest, orchestrates serialize
  LoadSystem (after SaveSystem)            -- processes LoadRequest, orchestrates deserialize
  AutosaveSystem (after SaveSystem)        -- ticks timer, creates SaveRequest(Autosave)
  CheckpointTriggerSystem (after Autosave) -- observes quest/zone events -> SaveRequest(Checkpoint)
  PlayerReconnectSaveSystem                -- auto-loads save on player reconnect
  SaveOnQuitSystem                         -- OnDestroy -> blocking save

PresentationSystemGroup (Client|Local):
  SaveUIBridgeSystem                       -- SaveComplete/LoadComplete -> UI feedback
```

### Key System Details

**PersistenceBootstrapSystem** (managed SystemBase, InitializationSystemGroup, runs once):
1. Load `SaveConfig` from `Resources/SaveConfig`
2. Create `SaveManagerSingleton` with all 10 ISaveModule implementations
3. Build `ModuleByTypeId` dictionary
4. Ensure save directory exists: `Directory.CreateDirectory(path)`
5. Start `SaveFileWriter` background thread
6. `Enabled = false` (self-disables after first run)

**SaveDirtyTrackingSystem** (managed SystemBase, OrderFirst):
- Uses ECS change filters to detect modifications to Health, PlayerStamina, ResourcePool, InventoryItem, EquippedItemElement, StatusEffect, survival components, and progression
- Sets corresponding bits in `SaveDirtyFlags.Flags` on the save state child entity
- AutosaveSystem and CheckpointTriggerSystem read dirty flags to decide whether a save is needed

**SaveIdAssignmentSystem** (managed SystemBase, Server):
- Query: `PlayerTag` + `SaveStateLink` where `PlayerSaveId.PlayerId` is empty
- Reads `GhostOwner.NetworkId`, formats as `"player_{networkId}"`, writes to child entity

**SaveSystem** (managed SystemBase, Server|Local):
1. Query `SaveRequest` entities
2. For each player: serialize to `MemoryStream` + `BinaryWriter` (<1ms)
3. Write header, module blocks, EOF marker
4. Compute CRC32, patch at offset 0x50
5. Hand `byte[]` to `SaveFileWriter.EnqueueWrite()` (async)
6. Create `SaveComplete` entity, destroy `SaveRequest`

**LoadSystem** (managed SystemBase, Server|Local):
1. Query `LoadRequest` entities
2. Read file to `byte[]` (synchronous -- <1ms for <10KB files)
3. Validate magic bytes, CRC32
4. If `FormatVersion < current`: run `SaveMigrationRunner.MigrateToLatest()`
5. For each module block: lookup ISaveModule by TypeId, call `Deserialize()`
6. Unknown TypeIds: skip `DataLength` bytes, log warning (forward compatibility)
7. Missing modules: silently skip (backward compatibility)
8. Create `LoadComplete` entity, destroy `LoadRequest`

**AutosaveSystem** (managed SystemBase):
- Accumulates timer (`TimeSinceLastSave += deltaTime`)
- Creates `SaveRequest(TriggerSource=Autosave, SlotIndex=Config.AutosaveSlot)` when interval reached
- Resets timer on save

**CheckpointTriggerSystem** (managed SystemBase):
- Observes `QuestCompletionEvent` (EPIC 16.12), boss kills, zone transitions
- Deduplication: `TimeSinceLastCheckpoint` > `Config.CheckpointCooldown` (default 30s)
- Creates `SaveRequest(TriggerSource=Checkpoint)` when triggered

**SaveOnQuitSystem** (managed SystemBase):
- `OnDestroy()`: performs blocking save, calls `SaveFileWriter.FlushBlocking()` -- the only synchronous I/O path

**PlayerReconnectSaveSystem** (managed SystemBase, Server):
- Detects newly spawned players matching known disconnected `PlayerSaveId`
- Checks for `player_{id}_slot{autosave}.dig`
- If found: creates `LoadRequest` for that player

**SaveUIBridgeSystem** (managed SystemBase, PresentationSystemGroup, Client|Local):
- Reads `SaveComplete` / `LoadComplete` transient entities
- Dispatches to `SaveUIRegistry` providers (ISaveNotificationProvider, ISaveSlotProvider, ISaveProgressProvider)
- Destroys transient entities after dispatch

---

## Async I/O Architecture

**File:** `Assets/Scripts/Persistence/IO/SaveFileWriter.cs`

```
SaveFileWriter (static class)
  ConcurrentQueue<WriteRequest> -- thread-safe write queue
  EnqueueWrite(filePath, data, metadataJson, metadataPath)
  FlushBlocking() -- drains queue synchronously (shutdown only)

Background thread:
  1. Write to {filename}.tmp
  2. Atomic rename to {filename}.dig
  3. Write .json sidecar
  4. Delete .tmp on success
```

**File:** `Assets/Scripts/Persistence/IO/SaveFileReader.cs`

```
SaveFileReader (static class)
  ReadFile(filePath) -> byte[] or null
  ValidateMagic(bytes) -> bool
  ValidateCRC32(bytes) -> bool
```

### Performance Budget

| Operation | Budget | Actual |
|-----------|--------|--------|
| Serialize all modules (main thread) | < 1ms | ~0.1ms |
| Write to disk (background thread) | N/A | 2-10ms for <10KB |
| Serialize world module | < 5ms | ~2ms for 50K records |
| GZip compress world | < 10ms | ~5ms for 1.5MB |
| Load player file (main thread) | < 2ms | ~0.5ms |
| Load world file (main thread) | < 20ms | ~10ms for 50K records |

---

## Version Migration System

**File:** `Assets/Scripts/Persistence/Core/IMigrationStep.cs`

```csharp
public interface IMigrationStep
{
    int FromVersion { get; }
    int ToVersion { get; }
    byte[] Migrate(byte[] fileBytes);
}
```

**File:** `Assets/Scripts/Persistence/Systems/SaveMigrationRunner.cs`

Two-level migration:
1. **File-level**: `SaveMigrationRunner.MigrateToLatest()` applies `IMigrationStep` chain (V1->V2->V3...) before module deserialization. Operates on raw bytes, rewriting headers and reshuffling module blocks as needed.
2. **Module-level**: Each module's `Deserialize` receives `blockVersion`. Module handles old schemas internally (reads fewer fields, fills defaults for newly added fields).

---

## Data Layer (ScriptableObjects)

### SaveConfig

**File:** `Assets/Scripts/Persistence/Definitions/SaveConfig.cs`

```
[CreateAssetMenu(menuName = "DIG/Persistence/Save Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| SaveSlotCount | int [Range(1,10)] | 3 | Max save slots per player |
| AutosaveSlot | int | 0 | Dedicated autosave slot index |
| QuicksaveSlot | int | -1 | Quicksave slot (-1 = disabled) |
| AutosaveIntervalSeconds | float | 300 | Autosave timer. 0 = disabled |
| EnableCheckpointSave | bool | true | Quest/zone/boss triggers save |
| CheckpointCooldown | float | 30 | Min seconds between checkpoints |
| ShutdownSaveEnabled | bool | true | Save on application quit |
| SaveFormatVersion | int | 1 | Bumped by programmer on binary layout changes |
| MaxWorldDeltaRecords | int | 50000 | Cap on voxel records |
| CompressWorldData | bool | true | GZip world module block |
| SaveDirectory | string | "saves" | Relative to persistentDataPath |

### SaveMetadata

**File:** `Assets/Scripts/Persistence/Definitions/SaveMetadata.cs`

Plain `[Serializable]` class for JSON sidecar. Not a ScriptableObject.

---

## Authoring

### SaveStateAuthoring

**File:** `Assets/Scripts/Persistence/Authoring/SaveStateAuthoring.cs`

```
[AddComponentMenu("DIG/Persistence/Save State")]
```

Baker creates child entity (same pattern as `TargetingModuleAuthoring`):
- Player entity: `SaveStateLink` (8 bytes)
- Child entity: `SaveStateTag` + `SaveDirtyFlags` + `PlayerSaveId` + `SaveStateOwner`

Place on Player prefab (Warrok_Server) alongside existing `PlayerAuthoring`.

---

## UI Bridge

**File:** `Assets/Scripts/Persistence/Bridges/ISaveUIProvider.cs`

Three provider interfaces for different UI concerns:

```
ISaveNotificationProvider
  void OnSaveStarted(int slotIndex, SaveTriggerSource source)
  void OnSaveComplete(int slotIndex, bool success, string error)
  void OnLoadStarted(int slotIndex)
  void OnLoadComplete(int slotIndex, bool success, string error)

ISaveSlotProvider
  void UpdateSlotInfo(int slotIndex, SaveMetadata metadata)
  void ClearSlot(int slotIndex)

ISaveProgressProvider
  void OnProgress(float percent)
```

**File:** `Assets/Scripts/Persistence/Bridges/SaveUIRegistry.cs` -- static provider registry. MonoBehaviours register on enable, unregister on disable.

**File:** `Assets/Scripts/Persistence/Systems/SaveUIBridgeSystem.cs`
- Managed SystemBase, PresentationSystemGroup, Client|Local
- Reads `SaveComplete` / `LoadComplete` entities, dispatches to registry, destroys entities

---

## Multiplayer

### Server-Authoritative Model

- ALL save/load operations run on server (or host in listen-server). Clients NEVER write saves.
- `WorldSystemFilterFlags.ServerSimulation | LocalSimulation` on all persistence systems.
- `SaveUIBridgeSystem` runs on client for UI feedback only.

### File Layout

```
{persistentDataPath}/saves/
  world_{sessionId}.digw           -- WorldSaveModule only (shared)
  world_{sessionId}.json           -- world metadata sidecar
  player_1_slot0.dig               -- player 1, autosave
  player_1_slot0.json
  player_1_slot1.dig               -- player 1, manual slot 1
  player_2_slot0.dig               -- player 2, autosave
  ...
```

### Player Reconnection Flow

1. Client disconnects
2. Server `AutosaveSystem` writes current state on next interval
3. Client reconnects -> `GoInGameServerSystem` spawns fresh player
4. `PlayerReconnectSaveSystem`: matches `PlayerSaveId`, finds existing `.dig`
5. Creates `LoadRequest` -> `LoadSystem` restores all state next frame

### Character Portability

Player `.dig` files are self-contained. Copy from server to single-player -- `LoadSystem` format is identical.

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `SaveStateLink` | 8 bytes | Player entity |
| `SaveStateTag` | 0 bytes | Child entity |
| `SaveDirtyFlags` | 4 bytes | Child entity |
| `PlayerSaveId` | 64 bytes | Child entity |
| `SaveStateOwner` | 8 bytes | Child entity |
| **Total on player** | **8 bytes** | |

---

## Editor Tooling

### PersistenceWorkstationWindow

**File:** `Assets/Editor/PersistenceWorkstation/PersistenceWorkstationWindow.cs`
- Menu: `DIG/Persistence Workstation`
- Sidebar + `IPersistenceModule` pattern (same as AI Workstation)

### Modules (4 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| Save Browser | `Modules/SaveBrowserModule.cs` | List `.dig` files with metadata (level, timestamp, playtime). Open folder, delete, copy path. |
| Save Inspector | `Modules/SaveInspectorModule.cs` | Parse binary header, CRC32 validation (green/red indicator), module foldouts with decoded fields, raw hex view toggle, file size breakdown per module. |
| Migration Tester | `Modules/MigrationTesterModule.cs` | Load old `.dig`, run migration chain, before/after diff, "Save Migrated" button. |
| Live State | `Modules/LiveStateModule.cs` | Play-mode only: Health/Stamina/Inventory/Currency/Survival/StatusEffects live values. Dirty-flag indicators per module. Manual save/load buttons. |

---

## Cross-EPIC Integration

| Source | Event | Persistence Response |
|--------|-------|---------------------|
| EPIC 16.6 (Loot/Items) | Equipment change | Dirty flag set, autosave writes EquipmentSaveModule |
| EPIC 16.8 (Resources) | ResourcePool change | PlayerStats dirty flag |
| EPIC 16.12 (Quests) | Quest completion | CheckpointTriggerSystem -> SaveRequest(Checkpoint) |
| EPIC 16.13 (Crafting) | Recipe unlocked | Crafting dirty flag |
| EPIC 16.14 (Progression) | Level up | PlayerStats + Progression dirty flags |
| EPIC 16.16 (Dialogue) | NPC dialogue flags | Future DialogueSaveModule (TypeId=11) |
| Voxel Edits | RecordModification() | WorldSaveModule reads accumulated history at save time |
| Player Death | DeathState.Dead | NO autosave (would save dead state) |

---

## File Summary

### New Files (36)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Persistence/Core/ISaveModule.cs` | Interface + context structs |
| 2 | `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs` | TypeId constants |
| 3 | `Assets/Scripts/Persistence/Core/SaveBinaryConstants.cs` | Magic bytes, EOF marker, offsets |
| 4 | `Assets/Scripts/Persistence/Core/IMigrationStep.cs` | Migration interface |
| 5 | `Assets/Scripts/Persistence/IO/SaveFileWriter.cs` | Async file writer (background thread) |
| 6 | `Assets/Scripts/Persistence/IO/SaveFileReader.cs` | Sync file reader + CRC32 validation |
| 7 | `Assets/Scripts/Persistence/Components/SaveStateComponents.cs` | SaveStateLink + child components |
| 8 | `Assets/Scripts/Persistence/Components/PersistenceRequestComponents.cs` | Request/result transients |
| 9 | `Assets/Scripts/Persistence/Components/SaveManagerSingleton.cs` | Managed singleton |
| 10 | `Assets/Scripts/Persistence/Definitions/SaveConfig.cs` | ScriptableObject |
| 11 | `Assets/Scripts/Persistence/Definitions/SaveMetadata.cs` | JSON sidecar class |
| 12 | `Assets/Scripts/Persistence/Authoring/SaveStateAuthoring.cs` | Player baker (child entity pattern) |
| 13 | `Assets/Scripts/Persistence/Modules/PlayerStatsSaveModule.cs` | ISaveModule: stats + currency + attributes |
| 14 | `Assets/Scripts/Persistence/Modules/InventorySaveModule.cs` | ISaveModule: inventory resources |
| 15 | `Assets/Scripts/Persistence/Modules/EquipmentSaveModule.cs` | ISaveModule: equipment + durability + affixes |
| 16 | `Assets/Scripts/Persistence/Modules/QuestSaveModule.cs` | ISaveModule: quest progress |
| 17 | `Assets/Scripts/Persistence/Modules/CraftingSaveModule.cs` | ISaveModule: known recipes |
| 18 | `Assets/Scripts/Persistence/Modules/WorldSaveModule.cs` | ISaveModule: voxel terrain deltas |
| 19 | `Assets/Scripts/Persistence/Modules/SettingsSaveModule.cs` | ISaveModule: player preferences |
| 20 | `Assets/Scripts/Persistence/Modules/StatusEffectsSaveModule.cs` | ISaveModule: active effects |
| 21 | `Assets/Scripts/Persistence/Modules/SurvivalSaveModule.cs` | ISaveModule: hunger/thirst/oxygen/sanity/infection |
| 22 | `Assets/Scripts/Persistence/Modules/ProgressionSaveModule.cs` | ISaveModule: XP/level/stat points/rested XP |
| 23 | `Assets/Scripts/Persistence/Systems/PersistenceBootstrapSystem.cs` | Bootstrap + module registration |
| 24 | `Assets/Scripts/Persistence/Systems/SaveSystem.cs` | Serialize orchestrator |
| 25 | `Assets/Scripts/Persistence/Systems/LoadSystem.cs` | Deserialize orchestrator |
| 26 | `Assets/Scripts/Persistence/Systems/AutosaveSystem.cs` | Timer -> SaveRequest |
| 27 | `Assets/Scripts/Persistence/Systems/CheckpointTriggerSystem.cs` | Event -> SaveRequest |
| 28 | `Assets/Scripts/Persistence/Systems/SaveOnQuitSystem.cs` | Shutdown blocking save |
| 29 | `Assets/Scripts/Persistence/Systems/SaveDirtyTrackingSystem.cs` | Change filter -> dirty flags |
| 30 | `Assets/Scripts/Persistence/Systems/SaveIdAssignmentSystem.cs` | NetworkId -> PlayerSaveId |
| 31 | `Assets/Scripts/Persistence/Systems/PlayerReconnectSaveSystem.cs` | Auto-load on reconnect |
| 32 | `Assets/Scripts/Persistence/Systems/SaveMigrationRunner.cs` | Migration chain executor |
| 33 | `Assets/Scripts/Persistence/Systems/SaveUIBridgeSystem.cs` | ECS -> UI bridge |
| 34 | `Assets/Scripts/Persistence/Bridges/SaveUIRegistry.cs` | Static provider registry |
| 35 | `Assets/Scripts/Persistence/Bridges/ISaveUIProvider.cs` | UI callback interfaces |
| 36 | `Assets/Editor/PersistenceWorkstation/PersistenceWorkstationWindow.cs` | Editor window (4 tabs) |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | Player prefab (Warrok_Server) | Add `SaveStateAuthoring` |
| 2 | `Assets/Scripts/Voxel/Core/VoxelModificationHistory.cs` | Add `GetAllRecords()` accessor + `Prune(int maxRecords)` method |

---

## Verification Checklist

- [ ] `SaveStateAuthoring` on player: child entity created, `PlayerSaveId` populated from NetworkId
- [ ] Manual save: `.dig` + `.json` files appear in saves directory
- [ ] Binary header: magic `44 49 47 53`, format version, CRC32 valid, EOF `44454E44`
- [ ] CRC32 corruption: corrupt 1 byte -> load rejected with CRC error
- [ ] Health save/load: modify to 50, save, modify to 100, load -> restored to 50
- [ ] Inventory: add 20 Stone + 10 Metal, save, clear, load -> quantities restored
- [ ] Equipment: weapon with 2 affixes + durability 0.7, save, quit, load -> entity recreated correctly
- [ ] Status effects: apply Bleed, save, clear buffer, load -> bleed restored
- [ ] Survival stats: Hunger=50, save, reset, load -> restored
- [ ] Autosave: interval=10s, wait 10s -> autosave slot updated
- [ ] Checkpoint: quest complete -> save within same frame (if cooldown passed)
- [ ] Checkpoint dedup: rapid quests within 30s -> only one checkpoint
- [ ] Shutdown save: quit play mode -> file written before Unity stops
- [ ] Crash safety: `.tmp` -> `.dig` atomic rename, no leftover `.tmp` files
- [ ] Async I/O: save during gameplay -> zero frame hitch (verify in Profiler)
- [ ] Unknown TypeId: inject TypeId=99 -> load skips block, other modules load
- [ ] Missing module: old save without StatusEffects -> load succeeds, buffer unchanged
- [ ] Module version: bump PlayerStats to V2, load V1 save -> old fields read, new fields default
- [ ] Format migration: V1->V2 step registered, old save loads correctly
- [ ] Multiplayer: 2 players -> 2 separate `.dig` files + 1 shared `.digw`
- [ ] Reconnect: disconnect, reconnect -> saved state restored
- [ ] Character portability: copy `.dig` to different install -> loads without error
- [ ] World save: dig 3 locations, save, reset, load -> modifications restored + chunks remeshed
- [ ] World compression: enable GZip, 1000+ records -> compressed block smaller
- [ ] Archetype: only +8 bytes on player entity, no ghost bake errors
- [ ] Persistence Workstation: Save Browser lists files with metadata
- [ ] Persistence Workstation: Save Inspector shows modules with decoded fields + hex view
- [ ] Persistence Workstation: Live State shows dirty flags + manual save button
