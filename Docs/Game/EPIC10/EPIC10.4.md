# EPIC 10.4: Reward Presentation

**Status**: Planning
**Epic**: EPIC 10 — Reward Economy
**Priority**: Medium — Player-facing feedback for reward economy
**Dependencies**: EPIC 10.1 (RewardCategory, RewardRarity), EPIC 10.3 (RewardGrantEvent); Framework: VFX/, UI toolkit

---

## Overview

Reward presentation is how the player perceives value. A well-presented Common item feels satisfying; a poorly-presented Legendary feels like nothing happened. This sub-epic covers the reward chest UI (inspect before taking), the rarity visual language (consistent color and VFX across all categories), the post-district summary screen, and the limb-specific preview panel. All presentation reads from RewardGrantEvent entities through a managed bridge system, following the CombatUIBridgeSystem pattern.

---

## Component Definitions

### RewardPresentationRequest (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardPresentationComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Transient entity requesting a reward be presented to the player (chest open, pickup, etc.).
    /// Created by loot/container interaction systems. Consumed by RewardPresentationBridge.
    /// </summary>
    public struct RewardPresentationRequest : IComponentData
    {
        public Entity TargetPlayer;
        public RewardCategory Category;
        public RewardRarity Rarity;
        public int ItemDefinitionId;
        public int Amount;
        public RewardPresentationStyle Style;
    }

    public enum RewardPresentationStyle : byte
    {
        Pickup = 0,           // Floating text + brief VFX (enemy drops, small containers)
        ChestReveal = 1,      // Full chest UI with inspect-before-take
        QuestReward = 2,      // Quest completion banner with reward callout
        BossReward = 3,       // Dramatic boss loot drop with fanfare
        ExtractionBonus = 4   // End-of-district extraction bonus
    }
}
```

### RewardChestState (IComponentData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardPresentationComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// State for an interactable reward chest in the world.
    /// Player inspects contents before deciding to take.
    /// </summary>
    public struct RewardChestState : IComponentData
    {
        public bool IsOpen;
        public bool IsLooted;

        /// <summary>Number of reward items inside (1-4 typically).</summary>
        public byte ItemCount;
    }
}
```

### RewardChestContents (IBufferElementData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardPresentationComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Buffer on chest entity listing the pre-rolled rewards inside.
    /// Generated when the chest spawns, displayed when the player opens it.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct RewardChestContents : IBufferElementData
    {
        public RewardCategory Category;
        public RewardRarity Rarity;
        public int ItemDefinitionId;
        public int Amount;
        public FixedString64Bytes DisplayName;
    }
}
```

### DistrictRewardSummary (IBufferElementData)

```csharp
// File: Assets/Scripts/Rewards/Components/RewardPresentationComponents.cs
using Unity.Entities;

namespace Hollowcore.Rewards
{
    /// <summary>
    /// Accumulated reward log for the current district visit.
    /// Buffer on a player child entity, populated by RewardSummaryTrackingSystem.
    /// Displayed at extraction in the post-district summary screen.
    /// </summary>
    [InternalBufferCapacity(0)] // Dynamic — grows as rewards are earned
    public struct DistrictRewardSummary : IBufferElementData
    {
        public RewardCategory Category;
        public RewardRarity Rarity;
        public int ItemDefinitionId;
        public int Amount;
        public RewardSourceType Source;
    }
}
```

---

## ScriptableObject Definitions

### RewardRarityVisualSO

```csharp
// File: Assets/Scripts/Rewards/Definitions/RewardRarityVisualSO.cs
using UnityEngine;

namespace Hollowcore.Rewards.Definitions
{
    [CreateAssetMenu(fileName = "RewardRarityVisuals", menuName = "Hollowcore/Rewards/Rarity Visuals")]
    public class RewardRarityVisualSO : ScriptableObject
    {
        [Header("Per-Rarity Visuals (indexed by RewardRarity)")]
        public RarityVisualEntry[] Entries;
    }

    [System.Serializable]
    public struct RarityVisualEntry
    {
        public RewardRarity Rarity;
        public string DisplayLabel;       // "Common", "Uncommon", "Rare", "Epic", "Legendary"

        [Header("Colors")]
        public Color PrimaryColor;         // White, Green, Blue, Purple, Gold
        public Color GlowColor;            // Lighter tint for glow/emission
        public Color BackgroundColor;      // Card/frame background tint

        [Header("VFX")]
        public GameObject PickupVFXPrefab;  // Spawned on pickup (scaled by rarity)
        public GameObject ChestVFXPrefab;   // Spawned on chest open reveal
        public GameObject IdleVFXPrefab;    // Looping effect on uncollected world item

        [Header("SFX")]
        public string PickupSFXKey;
        public string RevealSFXKey;

        [Header("UI")]
        public Sprite FrameSprite;          // Border frame for reward cards
        public Sprite RarityBadgeSprite;    // Small badge icon (star, diamond, etc.)
        public float CardScaleMultiplier;   // 1.0 for Common, 1.15 for Legendary (bigger = rarer)
    }
}
```

---

## Systems

### RewardPresentationBridge

```csharp
// File: Assets/Scripts/Rewards/Bridges/RewardPresentationBridge.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Managed SystemBase — reads ECS reward events, pushes to UI.
//
// Flow:
//   1. Query RewardGrantEvent entities targeting the local player
//   2. For each event, determine RewardPresentationStyle:
//      - Enemy drop → Pickup (floating text)
//      - Chest interaction → ChestReveal (if chest UI open)
//      - Quest complete → QuestReward (banner)
//      - Boss kill → BossReward (fanfare)
//      - Extraction → ExtractionBonus
//   3. Push RewardPresentationData to RewardUIRegistry (static managed singleton)
//   4. RewardUIRegistry notifies active UI panels:
//      - Pickup: spawn floating damage-number-style text with rarity color
//      - ChestReveal: populate chest UI with item cards
//      - QuestReward: show quest completion banner with reward callout
//      - BossReward: trigger dramatic loot sequence
//   5. Spawn world VFX via VFXRequest entities (EPIC 16.7 pipeline):
//      - Rarity-appropriate pickup VFX from RewardRarityVisualSO
//      - Category-specific tint from RewardCategoryDefinitionSO
//   6. Play SFX via audio system
```

### RewardChestInteractionSystem

```csharp
// File: Assets/Scripts/Rewards/Systems/RewardChestInteractionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Reads: Player interaction input, RewardChestState, RewardChestContents
// Writes: RewardChestState (open/loot), creates RewardGrantEvent entities
//
// Flow:
//   1. On player interact with chest entity:
//      a. Set IsOpen = true → chest UI opens on client
//      b. Client displays RewardChestContents as preview cards (inspect before taking)
//   2. On player confirm take:
//      a. For each RewardChestContents element: create RewardGrantEvent
//      b. Set IsLooted = true
//      c. Fire chest loot VFX/SFX
//   3. On player decline / walk away:
//      a. Set IsOpen = false, chest remains for later
```

### RewardSummaryTrackingSystem

```csharp
// File: Assets/Scripts/Rewards/Systems/RewardSummaryTrackingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: RewardDistributionSystem
//
// Reads: RewardGrantEvent entities
// Writes: DistrictRewardSummary buffer on player child entity
//
// Flow:
//   1. On district entry: clear DistrictRewardSummary buffer
//   2. Each frame: for every RewardGrantEvent targeting local player, append to summary buffer
//   3. At extraction: DistrictRewardSummary provides complete log for post-district screen
//   4. Summary includes total count, per-category breakdown, highest rarity earned
```

### LimbSalvagePreviewSystem

```csharp
// File: Assets/Scripts/Rewards/Systems/LimbSalvagePreviewSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Managed SystemBase — generates preview data for limb rewards.
//
// Flow:
//   1. When a LimbSalvage reward is presented (chest or pickup):
//      a. Resolve LimbDefinitionSO from ItemDefinitionId
//      b. Build preview data: slot type, stat block, rarity, district affinity, memory bonus
//      c. Compare with currently equipped limb in that slot (stat diff)
//      d. Push LimbPreviewData to RewardUIRegistry
//   2. Limb preview panel shows:
//      - Slot compatibility icon (head, arm, leg, torso)
//      - Stat comparison (green = better, red = worse, yellow = different)
//      - District memory bonus description
//      - Rarity visual treatment
//      - "Equip" / "Stash" / "Discard" action buttons
```

---

## UI Panels

### RewardChestPanel

```csharp
// File: Assets/Scripts/Rewards/UI/RewardChestPanel.cs
// MonoBehaviour on the chest UI prefab.
//
// Layout:
//   - Title: "Supply Cache" / "Echo Reward" / "Boss Spoils" (from chest type)
//   - Card grid: 1-4 reward cards, each showing:
//     - Category icon (from RewardCategoryDefinitionSO)
//     - Item name and icon
//     - Rarity frame (from RewardRarityVisualSO)
//     - Quantity badge (for stackable items)
//     - Hover: expanded tooltip with full description
//   - Action bar: "Take All" / "Leave" buttons
//   - For LimbSalvage cards: inline stat preview (compact version of LimbPreviewPanel)
//
// Data source: RewardUIRegistry.CurrentChestContents
```

### PostDistrictSummaryPanel

```csharp
// File: Assets/Scripts/Rewards/UI/PostDistrictSummaryPanel.cs
// MonoBehaviour shown at extraction.
//
// Layout:
//   - Header: "District Complete — [District Name]"
//   - Category breakdown: 7 rows, each showing category icon + count + best rarity earned
//     - Categories with 0 items shown greyed out
//   - Highlight section: "Best Find" — the highest rarity item earned, shown with full card
//   - Currency summary: district currency earned, universal currency, conversion option
//   - Compendium entries: "New Discoveries: [count]" (links to EPIC 9.3 extraction summary)
//   - Total item count and "Expedition Progress" bar
//
// Data source: RewardUIRegistry.DistrictSummary (built from DistrictRewardSummary buffer)
```

### LimbPreviewPanel

```csharp
// File: Assets/Scripts/Rewards/UI/LimbPreviewPanel.cs
// MonoBehaviour for detailed limb salvage inspection.
//
// Layout:
//   - Limb visual: 3D preview or 2D icon with rarity glow
//   - Slot indicator: which body part this fits (with silhouette)
//   - Stat comparison table:
//     - Left column: current limb stats (or "Empty Slot")
//     - Right column: new limb stats
//     - Delta column: green up-arrows / red down-arrows
//   - District affinity: "[District Name] Memory: +5% all stats in [District]"
//   - Special ability: if limb grants one, show ability icon + description
//   - Rarity + durability: "Rare — Permanent" or "Common — Temporary (45s)"
//   - Actions: "Equip Now" / "Add to Inventory" / "Discard"
//
// Data source: RewardUIRegistry.LimbPreview
```

---

## Rarity Visual Language

| Rarity | Color | Glow | Frame | VFX | SFX |
|---|---|---|---|---|---|
| Common | White (#CCCCCC) | Soft white | Thin grey border | Subtle sparkle | Soft chime |
| Uncommon | Green (#4CAF50) | Light green | Green tinted frame | Green particle burst | Rising tone |
| Rare | Blue (#2196F3) | Cyan glow | Blue ornate frame | Blue energy swirl | Crystal resonance |
| Epic | Purple (#9C27B0) | Violet bloom | Purple filigree frame | Purple lightning arc | Deep harmonic |
| Legendary | Gold (#FFD700) | Golden radiance | Gold animated frame | Golden pillar + sparks | Triumphant fanfare |

---

## Setup Guide

1. **Create `Assets/Scripts/Rewards/UI/`** and `Assets/Scripts/Rewards/Bridges/` folders
2. Create `RewardUIRegistry` as a static managed class (follows CombatUIRegistry pattern)
3. Create `RewardPresentationBridge` managed system in Bridges/
4. Create `RewardRarityVisualSO` asset in `Assets/Data/Rewards/RarityVisuals.asset` with all 5 rarity entries
5. Build UI prefabs under `Assets/Prefabs/UI/Rewards/`:
   - `RewardChestPanel.prefab` — inspect-before-take chest UI
   - `PostDistrictSummaryPanel.prefab` — extraction summary
   - `LimbPreviewPanel.prefab` — detailed limb stat comparison
   - `RewardFloatingText.prefab` — rarity-colored floating pickup text
6. Create per-rarity VFX prefabs in `Assets/Prefabs/VFX/Rewards/` (5 pickup VFX, 5 chest VFX, 5 idle VFX)
7. Wire `RewardChestInteractionSystem` to the interaction input pipeline
8. Wire `PostDistrictSummaryPanel` to the extraction flow (after district completion)
9. Configure `RewardRarityVisualSO` colors to match the table above
10. Test: spawn a chest with mixed rarities → verify correct colors, VFX, and preview data

---

## Verification

- [ ] RewardPresentationBridge reads RewardGrantEvent and routes to correct presentation style
- [ ] Pickup style: floating text with rarity color appears at collection point
- [ ] ChestReveal style: chest UI opens with all contents displayed as cards
- [ ] Chest contents inspectable before taking (no forced pickup)
- [ ] "Leave" option closes chest without looting — chest remains interactable
- [ ] Rarity colors consistent: Common=white, Uncommon=green, Rare=blue, Epic=purple, Legendary=gold
- [ ] VFX scales with rarity (Legendary has visible pillar effect, Common has subtle sparkle)
- [ ] SFX plays on pickup and chest reveal with rarity-appropriate sound
- [ ] PostDistrictSummaryPanel shows per-category breakdown with correct counts
- [ ] "Best Find" highlight displays the highest-rarity item earned in district
- [ ] LimbPreviewPanel shows stat comparison with green/red delta indicators
- [ ] Limb district affinity and special ability displayed when present
- [ ] RewardSummaryTrackingSystem logs all RewardGrantEvents to DistrictRewardSummary buffer
- [ ] Summary buffer cleared on district entry, accumulated through visit, displayed at extraction
- [ ] Boss reward presentation triggers dramatic sequence (distinct from normal chest)
- [ ] UI performs no direct ECS queries — all data flows through RewardPresentationBridge → RewardUIRegistry

---

## BlobAsset Pipeline

RewardRarityVisualSO is read by the presentation bridge and VFX spawning. Blob enables Burst-compatible rarity lookups.

```csharp
// File: Assets/Scripts/Economy/Blobs/RarityVisualBlob.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.Economy
{
    public struct RarityVisualBlob
    {
        public RewardRarity Rarity;
        public BlobString DisplayLabel;
        public float4 PrimaryColor;   // RGBA
        public float4 GlowColor;
        public float4 BackgroundColor;
        public float CardScaleMultiplier;
        // Prefab/SFX references resolved via managed lookup (not in blob)
    }

    public struct RarityVisualDatabase
    {
        /// <summary>Indexed by (int)RewardRarity. Length = 5.</summary>
        public BlobArray<RarityVisualBlob> Rarities;
    }

    public struct RarityVisualDatabaseRef : IComponentData
    {
        public BlobAssetReference<RarityVisualDatabase> Value;
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Rewards/Definitions/RewardRarityVisualSO.cs (append to class)

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Entries == null || Entries.Length != 5)
        {
            Debug.LogError($"[RarityVisuals] {name}: Must have exactly 5 entries (one per RewardRarity).", this);
            return;
        }

        for (int i = 0; i < Entries.Length; i++)
        {
            var e = Entries[i];
            if ((int)e.Rarity != i)
                Debug.LogError($"[RarityVisuals] {name}: Entry {i} has Rarity={e.Rarity}, expected {(RewardRarity)i}.", this);
            if (string.IsNullOrEmpty(e.DisplayLabel))
                Debug.LogWarning($"[RarityVisuals] {name}: Entry {i} missing DisplayLabel.", this);
            if (e.PickupVFXPrefab == null)
                Debug.LogWarning($"[RarityVisuals] {name}: Entry {i} ({e.Rarity}) missing PickupVFXPrefab.", this);
            if (e.CardScaleMultiplier <= 0f)
                Debug.LogWarning($"[RarityVisuals] {name}: Entry {i} CardScaleMultiplier <= 0.", this);
        }
    }
#endif
```
