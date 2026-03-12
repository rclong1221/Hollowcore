# EPIC 23.5: Reward & Choice Systems

**Status:** IMPLEMENTED
**Priority:** High (dopamine loop — the reason players push deeper)
**Dependencies:**
- EPIC 23.1 (RunState, RunPhase, RunCurrency)
- `LootTableSO` (existing — `Assets/Scripts/Loot/Definitions/LootTableSO.cs`, EPIC 16.6)
- `CurrencyTransactionSystem` (existing — `Assets/Scripts/Economy/`, EPIC 16.6)

**Feature:** Rewards on zone clear (choose-N-of-pool), mid-run shops with run-currency pricing, and risk-reward narrative events. All reward generation is seed-deterministic. Presentation is abstract — games provide their own UI/3D visualization.

---

## Problem

Clearing a zone with no reward feels empty. Rogue-lites need meaningful choices between runs: "do I take the damage buff or the healing?" Shops convert run-currency into power. Events add narrative spice and risk. Without these, the loop is just combat → combat → combat.

---

## Core Types

```csharp
// File: Assets/Scripts/Roguelite/Definitions/RewardDefinitionSO.cs
namespace DIG.Roguelite.Rewards
{
    public enum RewardType : byte
    {
        Item = 0,              // Resolve via LootTableSO
        RunCurrency = 1,       // Add to RunState.RunCurrency
        MetaCurrency = 2,      // Add to MetaBank (rare)
        StatBoost = 3,         // Temporary (this run only)
        AbilityUnlock = 4,     // Unlock ability for this run
        Modifier = 5,          // Add RunModifier (23.4)
        Healing = 6,           // Restore % of max health
        MaxHPUp = 7            // Increase max health for this run
    }

    [CreateAssetMenu(menuName = "DIG/Roguelite/Reward Definition")]
    public class RewardDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int RewardId;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public RewardType Type;
        public byte Rarity;                         // 0 = Common, 1 = Uncommon, 2 = Rare, 3 = Epic, 4 = Legendary

        [Header("Values")]
        public int IntValue;                        // Currency amount, item ID, ability ID
        public float FloatValue;                    // Stat multiplier, heal %, etc.

        [Header("References")]
        public LootTableSO LootTable;               // For Item type (nullable)
        public RunModifierDefinitionSO Modifier;     // For Modifier type (nullable)

        [Header("Constraints")]
        public int MinZoneIndex;                    // 0 = any
        public int MaxZoneIndex;                    // 0 = any
        public int RequiredAscensionLevel;          // 0 = always available
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Definitions/RewardPoolSO.cs
namespace DIG.Roguelite.Rewards
{
    [CreateAssetMenu(menuName = "DIG/Roguelite/Reward Pool")]
    public class RewardPoolSO : ScriptableObject
    {
        public string PoolName;
        public int ChoiceCount = 3;                 // How many options to present
        public bool AllowDuplicates;
        public List<RewardPoolEntry> Entries;
    }

    [Serializable]
    public struct RewardPoolEntry
    {
        public RewardDefinitionSO Reward;
        public float Weight;
        public byte MinRarity;                      // Filter: only include if rarity >= this
        public byte MaxRarity;                      // Filter: only include if rarity <= this
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/PendingRewardChoice.cs
namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// Buffer on RunState entity. Generated options awaiting player selection.
    /// Cleared after selection or timeout.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingRewardChoice : IBufferElementData
    {
        public int RewardId;
        public RewardType Type;
        public byte Rarity;
        public int IntValue;
        public float FloatValue;
        public int SlotIndex;                       // Which choice slot (0, 1, 2...)
        public bool IsSelected;
    }

    /// <summary>
    /// Transient request entity. Created by UI when player picks a reward.
    /// </summary>
    public struct RewardSelectionRequest : IComponentData
    {
        public int SlotIndex;
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/ShopInventoryEntry.cs
namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// Buffer on RunState entity. Populated when entering a Shop zone.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ShopInventoryEntry : IBufferElementData
    {
        public int RewardId;
        public RewardType Type;
        public byte Rarity;
        public int IntValue;
        public float FloatValue;
        public int Price;                           // In RunCurrency
        public bool IsSoldOut;
    }

    /// <summary>
    /// Transient request entity. Created by UI when player buys from shop.
    /// </summary>
    public struct ShopPurchaseRequest : IComponentData
    {
        public int ShopSlotIndex;
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Definitions/RunEventDefinitionSO.cs
namespace DIG.Roguelite.Rewards
{
    [CreateAssetMenu(menuName = "DIG/Roguelite/Run Event")]
    public class RunEventDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int EventId;
        public string DisplayName;
        [TextArea(3, 6)] public string NarrativeText;
        public Sprite Illustration;

        [Header("Choices")]
        public List<EventChoice> Choices;

        [Header("Constraints")]
        public int MinZoneIndex;
        public int MaxZoneIndex;
        public float Weight;                        // Selection weight in event pool
    }

    [Serializable]
    public class EventChoice
    {
        public string ChoiceText;
        [TextArea(2, 4)] public string OutcomeText;
        public RewardDefinitionSO Reward;            // Positive outcome (nullable)
        public RunModifierDefinitionSO Curse;        // Negative outcome (nullable)
        public float SuccessProbability;             // 0-1. 1 = guaranteed
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Definitions/EventPoolSO.cs
namespace DIG.Roguelite.Rewards
{
    [CreateAssetMenu(menuName = "DIG/Roguelite/Event Pool")]
    public class EventPoolSO : ScriptableObject
    {
        public List<RunEventDefinitionSO> Events;
    }
}
```

---

## Systems

| System | Group | World Filter | Burst | Purpose |
|--------|-------|--------------|-------|---------|
| `RewardRegistryBootstrapSystem` | InitializationSystemGroup | All | No | Loads reward/event pools from Resources/. Managed registry |
| `ChoiceGenerationSystem` | SimulationSystemGroup | Server\|Local | No | On `ZoneTransition` start: rolls ChoiceCount rewards from pool using reward seed. Writes `PendingRewardChoice` buffer |
| `RewardSelectionSystem` | SimulationSystemGroup, UpdateAfter(ChoiceGenerationSystem) | Server\|Local | No | Processes `RewardSelectionRequest`. Applies reward effect: currency, stat, modifier, item, healing |
| `ShopGenerationSystem` | SimulationSystemGroup | Server\|Local | No | On Shop zone `Active`: rolls shop inventory from reward pool, applies price scaling from `RuntimeDifficultyScale` |
| `ShopPurchaseSystem` | SimulationSystemGroup, UpdateAfter(ShopGenerationSystem) | Server\|Local | No | Processes `ShopPurchaseRequest`. Validates RunCurrency balance, deducts, applies reward, marks `IsSoldOut` |
| `EventPresentationSystem` | SimulationSystemGroup | Server\|Local | No | On Event zone `Active`: selects event from pool using event seed. Stores on managed registry for UI |
| `RewardUIBridgeSystem` | PresentationSystemGroup | Client\|Local | No | Reads pending choices, shop inventory, event state. Pushes to `RewardUIRegistry` for game HUD |

---

## Reward Application Flow

```
Zone Clear / Shop Purchase / Event Choice
    ↓
RewardSelectionSystem / ShopPurchaseSystem / EventPresentationSystem
    ↓ (switch on RewardType)
    Item         → Create LootDropRequest entity → existing loot pipeline
    RunCurrency  → RunState.RunCurrency += IntValue
    MetaCurrency → MetaBank.MetaCurrency += IntValue (23.2)
    StatBoost    → Write modifier entry via EquippedStatsSystem
    AbilityUnlock→ Enable ability component (game-specific)
    Modifier     → Create AddModifierRequest entity (23.4)
    Healing      → Health.Current += MaxHealth × FloatValue
    MaxHPUp      → Health.MaxHealth += IntValue
```

---

## Shop Pricing

```
BasePrice = RewardDefinitionSO.IntValue
ZoneMultiplier = 1 + (CurrentZoneIndex × 0.15)
DifficultyMultiplier = RuntimeDifficultyScale.CurrencyMultiplier (inverse)
FinalPrice = ceil(BasePrice × ZoneMultiplier × DifficultyMultiplier)
```

Games can override pricing by implementing a custom `IShopPriceProvider` interface (optional — default formula used when absent).

---

## Integration

- **LootTableSO**: Item rewards resolve through existing loot pipeline. `ChoiceGenerationSystem` creates `LootDropRequest` entities
- **CurrencyTransactionSystem**: RunCurrency and MetaCurrency changes optionally route through existing transaction pipeline for analytics
- **RunModifierStack**: Modifier rewards create `AddModifierRequest` entities processed by `ModifierAcquisitionSystem` (23.4)
- **EquippedStatsSystem**: StatBoost rewards write through existing modifier pipeline — temporary, cleared on run end

---

## Performance

- `PendingRewardChoice`: heap-allocated (InternalBufferCapacity=0). Typical: 3-5 entries per zone transition
- `ShopInventoryEntry`: heap-allocated. Typical: 4-8 entries per shop
- `ChoiceGenerationSystem`: runs once per zone transition. Seed-deterministic weighted selection
- `RewardUIBridgeSystem`: reads buffers, no allocation. Early-exit when no pending choices

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Scripts/Roguelite/Definitions/RewardDefinitionSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/RewardPoolSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/RunEventDefinitionSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/EventPoolSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Components/PendingRewardChoice.cs` | IBufferElementData | **NEW** |
| `Assets/Scripts/Roguelite/Components/ShopInventoryEntry.cs` | IBufferElementData | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RewardRegistryBootstrapSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ChoiceGenerationSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RewardSelectionSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ShopGenerationSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/ShopPurchaseSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/EventPresentationSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RewardUIBridgeSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Bridges/RewardUIRegistry.cs` | Static registry | **NEW** |

---

## Verification

1. **Choice generation**: Zone clear with ChoiceCount=3 produces 3 distinct rewards from pool, deterministic for same seed
2. **Reward selection**: Player picks slot 1 → correct reward applied, buffer cleared
3. **Rarity constraints**: Pool entry with MinRarity=2 never appears in zone 0 (low difficulty)
4. **Shop pricing**: Price scales correctly with zone index and difficulty
5. **Shop purchase**: Insufficient RunCurrency → request rejected. Sufficient → deducted, reward applied, slot marked sold out
6. **Event probability**: EventChoice with SuccessProbability=0.5 → reward ~50% of the time, curse ~50% (seed-deterministic)
7. **Item rewards**: Item type reward creates LootDropRequest → existing loot pipeline resolves correctly
8. **Modifier rewards**: Modifier type reward creates AddModifierRequest → stacks correctly via 23.4
