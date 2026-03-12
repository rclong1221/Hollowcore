# EPIC 23.2: Meta-Progression & Permanent Unlocks

**Status:** PLANNED
**Priority:** High (retention hook — makes players want to do "one more run")
**Dependencies:**
- EPIC 23.1 (RunState, RunPhase, RunEndSystem)
- `ISaveModule` + `SaveModuleTypeIds` (existing — `Assets/Scripts/Persistence/Core/`, EPIC 16.15)
- `CurrencyTransactionSystem` (existing — `Assets/Scripts/Economy/`, EPIC 16.6)
- `EquippedStatsSystem` (existing — `Assets/Scripts/Progression/Systems/`, EPIC 16.14)

**Feature:** Persistent account-level progression that survives death. Meta-currency earned from runs, spent on a designer-authored unlock tree (stat boosts, starter items, abilities, cosmetics). Saved via ISaveModule pattern.

---

## Problem

Rogue-lites need a reason to keep playing after death. Without persistent unlocks, each run feels disconnected. The meta-progression layer converts run performance into permanent power growth, creating the "one more run" loop.

---

## Core Types

```csharp
// File: Assets/Scripts/Roguelite/Components/MetaBank.cs
namespace DIG.Roguelite.Meta
{
    /// <summary>
    /// Persistent account state on a dedicated entity. Loaded from save on startup.
    /// </summary>
    public struct MetaBank : IComponentData
    {
        public int MetaCurrency;
        public int LifetimeMetaEarned;
        public int TotalRunsAttempted;
        public int TotalRunsWon;
        public int BestScore;
        public int BestZoneReached;
        public float TotalPlaytime;
    }

    public enum MetaUnlockCategory : byte
    {
        StatBoost = 0,
        StarterItem = 1,
        NewAbility = 2,
        Cosmetic = 3,
        RunModifier = 4,      // Unlock new modifiers (23.4)
        ZoneAccess = 5,       // Unlock new zone types (23.3)
        ShopUpgrade = 6,      // Better shop offerings (23.5)
        CurrencyBonus = 7
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/MetaUnlockEntry.cs
namespace DIG.Roguelite.Meta
{
    /// <summary>
    /// Buffer on MetaBank entity. Heap-allocated to support large unlock trees.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct MetaUnlockEntry : IBufferElementData
    {
        public int UnlockId;
        public MetaUnlockCategory Category;
        public int Cost;
        public int PrerequisiteUnlockId;      // -1 = root node
        public bool IsUnlocked;
        public float FloatValue;              // e.g., +5% = 0.05
        public int IntValue;                  // e.g., item ID for StarterItem
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Definitions/MetaUnlockTreeSO.cs
namespace DIG.Roguelite.Meta
{
    [CreateAssetMenu(menuName = "DIG/Roguelite/Meta Unlock Tree")]
    public class MetaUnlockTreeSO : ScriptableObject
    {
        public string TreeName;
        public List<MetaUnlockNodeSO> Nodes;
    }

    [CreateAssetMenu(menuName = "DIG/Roguelite/Meta Unlock Node")]
    public class MetaUnlockNodeSO : ScriptableObject
    {
        public int UnlockId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public MetaUnlockCategory Category;
        public int Cost;
        public MetaUnlockNodeSO Prerequisite;   // null = root
        public float FloatValue;
        public int IntValue;
    }
}
```

---

## Save Modules

```csharp
// File: Assets/Scripts/Roguelite/Persistence/MetaProgressionSaveModule.cs
// ISaveModule, TypeId = 16
// Serializes: MetaBank fields + MetaUnlockEntry[].IsUnlocked bit array
// Pattern: identical to ProgressionSaveModule (TypeId=10)
```

```csharp
// File: Assets/Scripts/Roguelite/Persistence/RunHistorySaveModule.cs
// ISaveModule, TypeId = 17
// Serializes: last 100 RunHistoryEntry records (ring buffer)
```

**SaveModuleTypeIds.cs additions:**
```csharp
public const int MetaProgression = 16;
public const int RunHistory      = 17;
public const int RunState        = 18; // Mid-run save/resume (future)
```

---

## Systems

| System | Group | World Filter | Burst | Purpose |
|--------|-------|--------------|-------|---------|
| `MetaBootstrapSystem` | InitializationSystemGroup | All | No | Loads `MetaUnlockTreeSO`, creates MetaBank entity + buffer. Applies saved state from save module |
| `MetaCurrencyConversionSystem` | SimulationSystemGroup | Server\|Local | Partial | On `RunPhase == MetaScreen`: converts RunCurrency + Score → MetaCurrency using conversion rate + ascension bonus |
| `MetaUnlockPurchaseSystem` | SimulationSystemGroup | Server\|Local | No | Processes `MetaUnlockRequest` (transient entity). Validates cost + prerequisite, deducts, sets IsUnlocked, marks save dirty |
| `MetaStatApplySystem` | SimulationSystemGroup, UpdateBefore(RunInitSystem) | Server\|Local | Yes | On `Preparation`: iterates unlocked StatBoost entries, writes modifier entries via `EquippedStatsSystem` pipeline |

---

## Integration

- **ISaveModule**: `MetaProgressionSaveModule` serializes MetaBank + 1-bit-per-unlock (200 nodes = 25 bytes). Follows existing binary format with CRC32 + migration versioning
- **CurrencyTransactionSystem**: MetaCurrency optionally routes through existing transactions for analytics
- **EquippedStatsSystem**: Stat boosts injected as modifier entries — no new stat system needed

---

## Performance

- MetaUnlockEntry buffer: ~32 bytes/entry. 200 nodes = 6.4KB (heap-allocated, not on player)
- `MetaCurrencyConversionSystem`: runs once per run end
- `MetaStatApplySystem`: runs once per run start, Burst-compiled
- Save I/O: `IsUnlocked` bit array = 25 bytes for 200 nodes

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Scripts/Roguelite/Components/MetaBank.cs` | IComponentData + Enum | **NEW** |
| `Assets/Scripts/Roguelite/Components/MetaUnlockEntry.cs` | IBufferElementData | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/MetaUnlockTreeSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Systems/MetaBootstrapSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/MetaCurrencyConversionSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/MetaUnlockPurchaseSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/MetaStatApplySystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Persistence/MetaProgressionSaveModule.cs` | ISaveModule | **NEW** |
| `Assets/Scripts/Roguelite/Persistence/RunHistorySaveModule.cs` | ISaveModule | **NEW** |
| `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs` | Constants | Modified (add 16-18) |

---

## Verification

1. **Meta-currency conversion**: Run ends with 100 RunCurrency → MetaBank receives 50 at 0.5 rate
2. **Unlock purchase**: Spend 30 MetaCurrency → `IsUnlocked = true`, balance decremented
3. **Prerequisite validation**: Cannot unlock node whose prerequisite is still locked
4. **Stat application**: Unlocked StatBoost entries apply at run start via EquippedStatsSystem
5. **Save persistence**: Quit → restart → MetaBank and unlock states restored
6. **History cap**: RunHistoryEntry buffer stays at 100 max (oldest removed)
