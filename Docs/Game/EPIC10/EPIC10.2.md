# EPIC 10.2: District Currency System

**Status**: Planning
**Epic**: EPIC 10 — Reward Economy
**Priority**: High — Core vendor and spending loop
**Dependencies**: Framework: Economy/ (CurrencyTransactionSystem), Trading/; EPIC 4 (Districts), EPIC 10.1 (RewardCategory)

---

## Overview

Each of Hollowcore's 15 districts has its own local currency, plus one universal expedition currency ("Creds"). District currencies are earned by exploring, fighting, and completing objectives within that district. They are spent at local vendors for district-specific goods. Currency converts between districts at the gate screen, but at a punishing exchange rate — incentivizing players to spend locally rather than hoard. The system extends the framework's CurrencyTransactionSystem with district-specific currency types and vendor inventory scaling.

---

## Component Definitions

### DistrictCurrencyId (IComponentData)

```csharp
// File: Assets/Scripts/Economy/Components/DistrictCurrencyComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace Hollowcore.Economy
{
    /// <summary>
    /// Identifies which currency a wallet entry or transaction uses.
    /// CurrencyId 0 = universal Creds. CurrencyId 1-15 = district-specific.
    /// </summary>
    public struct CurrencyId
    {
        public const int Universal = 0;    // "Creds" — accepted everywhere, poor conversion rate
        public const int MaxDistricts = 15;

        public int Value;

        public bool IsUniversal => Value == Universal;
        public bool IsDistrictSpecific => Value > Universal && Value <= MaxDistricts;
    }
}
```

### PlayerWallet (IBufferElementData)

```csharp
// File: Assets/Scripts/Economy/Components/DistrictCurrencyComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Economy
{
    /// <summary>
    /// One entry per currency type the player currently holds.
    /// Buffer lives on a child entity via EconomyLink to avoid 16KB player archetype limit.
    /// </summary>
    [InternalBufferCapacity(4)] // Most players carry 2-4 currency types at a time
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerWallet : IBufferElementData
    {
        [GhostField] public int CurrencyId;
        [GhostField] public int Amount;
        [GhostField] public int MaxAmount; // Per-currency cap. 0 = no cap (universal)
    }
}
```

### EconomyLink (IComponentData)

```csharp
// File: Assets/Scripts/Economy/Components/DistrictCurrencyComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Economy
{
    /// <summary>
    /// Link from player entity to economy child entity holding the wallet buffer.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct EconomyLink : IComponentData
    {
        [GhostField] public Entity EconomyEntity;
    }
}
```

### CurrencyTransaction (IComponentData)

```csharp
// File: Assets/Scripts/Economy/Components/DistrictCurrencyComponents.cs
using Unity.Entities;

namespace Hollowcore.Economy
{
    /// <summary>
    /// Transient entity requesting a currency operation.
    /// Created by loot pickup, vendor purchase, quest reward, or conversion systems.
    /// Consumed by DistrictCurrencyTransactionSystem.
    /// </summary>
    public struct CurrencyTransaction : IComponentData
    {
        public Entity TargetPlayer;
        public int CurrencyId;
        public int Amount;               // Positive = grant, negative = spend
        public CurrencyTransactionSource Source;
    }

    public enum CurrencyTransactionSource : byte
    {
        LootPickup = 0,
        VendorPurchase = 1,
        QuestReward = 2,
        Conversion = 3,
        EchoReward = 4,
        BossKill = 5,
        EventReward = 6
    }
}
```

### VendorInventoryState (IBufferElementData)

```csharp
// File: Assets/Scripts/Economy/Components/DistrictCurrencyComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Economy
{
    /// <summary>
    /// One item available at a district vendor. Buffer on the vendor entity.
    /// Inventory contents scale with Front phase — higher danger = better stock.
    /// </summary>
    [InternalBufferCapacity(0)] // Dynamic — vendors carry variable stock
    public struct VendorInventoryState : IBufferElementData
    {
        public int ItemDefinitionId;       // References ItemDefinitionSO or RewardDefinitionSO
        public int CurrencyId;             // Which currency this item costs
        public int Price;
        public byte QuantityAvailable;     // 0 = sold out
        public byte MinFrontPhase;         // Only appears when Front >= this phase
    }
}
```

### DistrictVendorTag (IComponentData)

```csharp
// File: Assets/Scripts/Economy/Components/DistrictCurrencyComponents.cs
using Unity.Entities;

namespace Hollowcore.Economy
{
    /// <summary>
    /// Tags a vendor entity with its district and unlock conditions.
    /// </summary>
    public struct DistrictVendorTag : IComponentData
    {
        public int DistrictId;
        public bool RequiresBossKill;      // Only spawns after district boss defeated
        public bool RequiresEchoComplete;  // Only spawns after echo completed
        public int RequiredCompendiumEntryId; // -1 = no compendium unlock required
    }
}
```

### CurrencyConversionConfig (IComponentData)

```csharp
// File: Assets/Scripts/Economy/Components/DistrictCurrencyComponents.cs
using Unity.Entities;

namespace Hollowcore.Economy
{
    /// <summary>
    /// Singleton config for cross-district currency conversion rates.
    /// Baked from CurrencyConversionConfigAuthoring.
    /// </summary>
    public struct CurrencyConversionConfig : IComponentData
    {
        /// <summary>District → Universal rate (e.g., 0.4 = lose 60% converting to Creds).</summary>
        public float DistrictToUniversalRate;

        /// <summary>Universal → District rate (e.g., 0.5 = lose 50% converting from Creds).</summary>
        public float UniversalToDistrictRate;

        /// <summary>District → District rate (goes through universal: A→Creds→B, compounding loss).</summary>
        public float CrossDistrictRate; // Typically DistrictToUniversal * UniversalToDistrict
    }
}
```

---

## ScriptableObject Definitions

### DistrictCurrencyDefinitionSO

```csharp
// File: Assets/Scripts/Economy/Definitions/DistrictCurrencyDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Economy.Definitions
{
    [CreateAssetMenu(fileName = "NewDistrictCurrency", menuName = "Hollowcore/Economy/District Currency")]
    public class DistrictCurrencyDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int CurrencyId;
        public string DisplayName;         // e.g., "Memory Fragments", "Bio-Tokens", "Creds"
        [TextArea] public string Description;
        public Sprite Icon;
        public Color TintColor;

        [Header("District")]
        [Tooltip("District this currency belongs to. -1 = universal")]
        public int DistrictId = -1;

        [Header("Limits")]
        [Tooltip("Max amount a player can carry. 0 = no cap")]
        public int MaxCarry = 999;

        [Header("Conversion")]
        [Tooltip("Override conversion rate for this currency (0 = use global config)")]
        public float CustomToUniversalRate;
        [Tooltip("Flavor text for conversion UI")]
        public string ConversionFlavorText;

        [Header("Vendor")]
        [Tooltip("Base price multiplier at this district's vendors (1.0 = standard)")]
        public float VendorPriceMultiplier = 1f;
    }
}
```

### DistrictVendorDefinitionSO

```csharp
// File: Assets/Scripts/Economy/Definitions/DistrictVendorDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Economy.Definitions
{
    [CreateAssetMenu(fileName = "NewVendor", menuName = "Hollowcore/Economy/District Vendor")]
    public class DistrictVendorDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int VendorId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Portrait;

        [Header("Location")]
        public int DistrictId;
        public bool RequiresBossKill;
        public bool RequiresEchoComplete;
        public int RequiredCompendiumEntryId = -1;

        [Header("Inventory")]
        public VendorItemEntry[] BaseInventory;
        public VendorItemEntry[] FrontPhase2Additions;
        public VendorItemEntry[] FrontPhase3Additions;

        [Header("Personality")]
        [TextArea(2, 4)] public string GreetingDialogue;
        [TextArea(2, 4)] public string FarewellDialogue;
        [Tooltip("Price markup/discount personality (0.8 = 20% discount, 1.2 = 20% markup)")]
        public float PersonalityPriceModifier = 1f;
    }

    [System.Serializable]
    public struct VendorItemEntry
    {
        public int ItemDefinitionId;
        public int BasePrice;
        public byte BaseQuantity;
        public byte MinFrontPhase; // 0 = always available
    }
}
```

---

## Systems

### DistrictCurrencyTransactionSystem

```csharp
// File: Assets/Scripts/Economy/Systems/DistrictCurrencyTransactionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: CurrencyTransaction (transient entities)
// Writes: PlayerWallet buffer on economy child entity
//
// Flow:
//   1. Query all CurrencyTransaction entities (manual EntityQuery)
//   2. For each transaction:
//      a. Resolve TargetPlayer → EconomyLink → economy child entity
//      b. Find or create PlayerWallet entry for CurrencyId
//      c. If Amount > 0 (grant): add to wallet, clamp to MaxAmount
//      d. If Amount < 0 (spend): check sufficient funds, deduct or reject
//      e. Fire CurrencyChangedEvent for UI notification
//   3. Destroy transaction entities via ECB
//   4. Integrates with framework CurrencyTransactionSystem — extends, does not replace
```

### DistrictVendorSystem

```csharp
// File: Assets/Scripts/Economy/Systems/DistrictVendorSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: DistrictVendorTag, VendorInventoryState, current Front phase
// Writes: VendorInventoryState (restock on phase change)
//
// Flow:
//   1. On district entry: spawn vendor entities from DistrictVendorDefinitionSO for current district
//   2. Filter vendors by unlock conditions (boss kill, echo complete, compendium entry)
//   3. Populate VendorInventoryState buffer with BaseInventory
//   4. On Front phase change: append phase-specific additions
//   5. Apply vendor personality price modifier to all items
//   6. Handle purchase requests (VendorPurchaseRequest transient entities):
//      a. Validate item exists and QuantityAvailable > 0
//      b. Create CurrencyTransaction (negative Amount) for cost
//      c. Grant item to player via framework Items/ system
//      d. Decrement QuantityAvailable
```

### CurrencyConversionSystem

```csharp
// File: Assets/Scripts/Economy/Systems/CurrencyConversionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: CurrencyConversionRequest (transient entities), CurrencyConversionConfig singleton
// Writes: Creates CurrencyTransaction entities for both debit and credit
//
// Flow:
//   1. Query CurrencyConversionRequest entities
//   2. For each request:
//      a. Determine conversion rate from CurrencyConversionConfig
//      b. If custom rate on DistrictCurrencyDefinitionSO, use that instead
//      c. Calculate output amount = inputAmount * rate (floor, always lose fractional)
//      d. Create CurrencyTransaction (negative) for source currency
//      e. Create CurrencyTransaction (positive) for target currency
//      f. Fire ConversionCompletedEvent with loss amount for UI
//   3. Conversion only available at gate screen (validated by checking game state)
//   4. Destroy request entities via ECB
```

### EconomyBootstrapSystem

```csharp
// File: Assets/Scripts/Economy/Systems/EconomyBootstrapSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
//
// On player spawn:
//   1. Create economy child entity with PlayerWallet buffer
//   2. Set EconomyLink on player entity
//   3. Add starting universal currency (Creds) wallet entry with Amount from expedition config
//   4. If returning from previous district: restore wallet state from expedition save
```

---

## Setup Guide

1. **Create `Assets/Scripts/Economy/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/
2. **Create assembly definition** `Hollowcore.Economy.asmdef` referencing `DIG.Shared`, `DIG.Economy`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`
3. Create 16 DistrictCurrencyDefinitionSO assets in `Assets/Data/Economy/Currencies/`:
   - `Creds.asset` (CurrencyId=0, universal, DistrictId=-1, MaxCarry=9999)
   - One per district: `MemoryFragments.asset` (Necrospire), `BioTokens.asset` (Wetmarket), etc.
4. Create `CurrencyConversionConfigAuthoring` singleton in subscene with default rates:
   - DistrictToUniversalRate = 0.4, UniversalToDistrictRate = 0.5, CrossDistrictRate = 0.2
5. Add `EconomyAuthoring` to the player prefab (creates child entity with wallet buffer)
6. Create at least 2 DistrictVendorDefinitionSO assets for vertical slice districts
7. Wire loot pickup system to create CurrencyTransaction entities with appropriate CurrencyId
8. Wire gate screen UI to CurrencyConversionSystem for cross-district exchange

---

## Verification

- [ ] EconomyLink on player entity references valid child entity with PlayerWallet buffer
- [ ] Universal currency (Creds, CurrencyId=0) wallet entry created on player spawn
- [ ] District currency granted when picking up loot in that district
- [ ] PlayerWallet.MaxAmount enforced — excess currency rejected
- [ ] CurrencyTransaction with negative amount fails if insufficient funds (no negative balance)
- [ ] CurrencyChangedEvent fires on every wallet modification for UI updates
- [ ] District vendors spawn with correct inventory based on unlock conditions
- [ ] Vendor inventory scales with Front phase (new items appear at higher phases)
- [ ] Purchase request deducts correct currency and grants item
- [ ] Sold-out items (QuantityAvailable=0) cannot be purchased
- [ ] Currency conversion at gate screen applies correct rate (40% loss district→universal)
- [ ] Cross-district conversion compounds losses correctly (district A→Creds→district B)
- [ ] ConversionCompletedEvent shows loss amount for player feedback
- [ ] All 16 DistrictCurrencyDefinitionSO assets created with distinct icons and names
- [ ] 16KB archetype safe — EconomyLink is only 8 bytes on player entity

---

## BlobAsset Pipeline

DistrictCurrencyDefinitionSO is read by transaction, vendor, and conversion systems. Blob enables Burst-compiled currency lookups.

```csharp
// File: Assets/Scripts/Economy/Blobs/DistrictCurrencyBlob.cs
using Unity.Collections;
using Unity.Entities;

namespace Hollowcore.Economy
{
    public struct DistrictCurrencyBlob
    {
        public int CurrencyId;
        public BlobString DisplayName;
        public int DistrictId;           // -1 = universal
        public int MaxCarry;
        public float CustomToUniversalRate; // 0 = use global
        public float VendorPriceMultiplier;
    }

    public struct DistrictCurrencyDatabase
    {
        /// <summary>Indexed by CurrencyId (0=universal, 1-15=district). Length = 16.</summary>
        public BlobArray<DistrictCurrencyBlob> Currencies;
    }

    public struct DistrictCurrencyDatabaseRef : IComponentData
    {
        public BlobAssetReference<DistrictCurrencyDatabase> Value;
    }
}
```

```csharp
// File: Assets/Scripts/Economy/Authoring/DistrictCurrencyDatabaseAuthoring.cs
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Hollowcore.Economy.Authoring
{
    public class DistrictCurrencyDatabaseAuthoring : MonoBehaviour
    {
        public DistrictCurrencyDefinitionSO[] Currencies; // All 16 (universal + 15 districts)
    }

    public class DistrictCurrencyDatabaseBaker : Baker<DistrictCurrencyDatabaseAuthoring>
    {
        public override void Bake(DistrictCurrencyDatabaseAuthoring authoring)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<DistrictCurrencyDatabase>();
            var arr = builder.Allocate(ref root.Currencies, 16);

            foreach (var so in authoring.Currencies)
            {
                if (so.CurrencyId < 0 || so.CurrencyId >= 16) continue;
                arr[so.CurrencyId].CurrencyId = so.CurrencyId;
                builder.AllocateString(ref arr[so.CurrencyId].DisplayName, so.DisplayName);
                arr[so.CurrencyId].DistrictId = so.DistrictId;
                arr[so.CurrencyId].MaxCarry = so.MaxCarry;
                arr[so.CurrencyId].CustomToUniversalRate = so.CustomToUniversalRate;
                arr[so.CurrencyId].VendorPriceMultiplier = so.VendorPriceMultiplier;
            }

            var blobRef = builder.CreateBlobAssetReference<DistrictCurrencyDatabase>(Allocator.Persistent);
            builder.Dispose();

            var entity = GetEntity(TransformUsageFlags.None);
            AddBlobAsset(ref blobRef, out _);
            AddComponent(entity, new DistrictCurrencyDatabaseRef { Value = blobRef });
        }
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Economy/Definitions/DistrictCurrencyDefinitionSO.cs (append to class)

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (CurrencyId < 0 || CurrencyId > 15)
            Debug.LogError($"[DistrictCurrency] {name}: CurrencyId must be 0-15, got {CurrencyId}.", this);
        if (MaxCarry <= 0 && CurrencyId != CurrencyId) // universal can be 0 (no cap)
            Debug.LogWarning($"[DistrictCurrency] {name}: MaxCarry <= 0 — players cannot earn this currency.", this);
        if (string.IsNullOrEmpty(DisplayName))
            Debug.LogError($"[DistrictCurrency] {name}: DisplayName is empty.", this);
        if (DistrictId != -1 && (DistrictId < 1 || DistrictId > 15))
            Debug.LogError($"[DistrictCurrency] {name}: DistrictId must be -1 (universal) or 1-15.", this);
        if (CustomToUniversalRate < 0f)
            Debug.LogError($"[DistrictCurrency] {name}: CustomToUniversalRate cannot be negative.", this);
        if (VendorPriceMultiplier <= 0f)
            Debug.LogWarning($"[DistrictCurrency] {name}: VendorPriceMultiplier <= 0 makes items free.", this);
    }
#endif
```

```csharp
// File: Assets/Editor/Economy/DistrictCurrencyBuildValidator.cs
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Hollowcore.Economy.Editor
{
    public class DistrictCurrencyBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:DistrictCurrencyDefinitionSO");
            var currencies = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<DistrictCurrencyDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(so => so != null).ToList();

            // CurrencyId uniqueness
            var seen = new HashSet<int>();
            foreach (var so in currencies)
                if (!seen.Add(so.CurrencyId))
                    Debug.LogError($"[EconomyBuildValidation] Duplicate CurrencyId: {so.CurrencyId} in {so.name}");

            // Universal currency must exist
            if (!currencies.Any(c => c.CurrencyId == 0))
                Debug.LogError("[EconomyBuildValidation] No universal currency (CurrencyId=0) defined.");

            // All 16 slots filled
            if (currencies.Count < 16)
                Debug.LogWarning($"[EconomyBuildValidation] Only {currencies.Count}/16 DistrictCurrencyDefinitionSO assets found.");

            // MaxCarry > 0 for district currencies
            foreach (var so in currencies.Where(c => c.CurrencyId > 0))
                if (so.MaxCarry <= 0)
                    Debug.LogError($"[EconomyBuildValidation] {so.name}: district currency must have MaxCarry > 0.");
        }
    }
}
```

---

## Editor Tooling

Currency flow visualization is part of the Economy Dashboard (see EPIC 10.1). The Sink/Source Flow Module includes a per-currency sub-view showing:
- Inflow sources per district (exploration, side goals, boss kills)
- Outflow sinks (vendor purchases, conversion loss, district exit loss)
- Net accumulation rate per district visit
- Cross-district conversion loss waterfall chart

---

## Live Tuning

```csharp
// File: Assets/Scripts/Economy/Debug/CurrencyLiveTuning.cs
namespace Hollowcore.Economy.Debug
{
    /// <summary>
    /// Runtime-tunable currency parameters (no server restart):
    ///   - CurrencyConversionConfig.DistrictToUniversalRate (float, 0-1)
    ///   - CurrencyConversionConfig.UniversalToDistrictRate (float, 0-1)
    ///   - Per-currency MaxCarry override (int)
    ///   - DistrictExitCurrencyLoss override (float, 0-1)
    ///
    /// Pattern: CurrencyLiveTuningSystem reads static CurrencyTuningOverrides,
    /// writes to CurrencyConversionConfig singleton each frame when dirty.
    /// Exposed via Economy Dashboard "Live Tuning" tab during play mode.
    /// </summary>
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Economy/Debug/CurrencyDebugOverlay.cs
namespace Hollowcore.Economy.Debug
{
    /// <summary>
    /// Debug overlay for currency flow (development builds):
    /// - Corner HUD: per-currency balance bars (current / max, colored by district)
    /// - Transaction log: scrolling list of recent CurrencyTransactions (source, amount, currency)
    /// - Conversion loss indicator: red flash when currency converts at loss
    /// - Vendor spending tracker: pie chart of spending by vendor
    /// - Toggle: /debug economy currency
    /// </summary>
}
```

---

## Simulation & Testing

Currency balance is validated by the Monte Carlo simulation in EPIC 10.1's Economy Dashboard. Per-currency metrics:
- **Sink-source equilibrium**: net currency flow per district per currency type
- **Conversion loss impact**: total value destroyed by cross-district conversion over N districts
- **MaxCarry saturation**: district at which 50%/90% of simulated players hit currency cap
- **Vendor affordability curve**: percentage of vendor stock affordable at each district count
