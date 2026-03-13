# EPIC 10.1 Setup Guide: Reward Categories

**Status:** Planned
**Requires:** Framework: Rewards/RewardDefinitionSO, Items/, Loot/; EPIC 1 (Chassis/LimbSalvage), EPIC 9 (CompendiumPage)

---

## Overview

Seven distinct reward categories define Hollowcore's player-facing economy. Each category serves a different strategic purpose, creating the "always short of something" tension that drives the greed-vs-survival loop. This sub-epic defines the `RewardCategoryType` enum (renamed from `RewardCategory` to avoid collision with `Hollowcore.District.DistrictRewardFocus` in EPIC 13), per-category gameplay roles, the `RewardCategoryDefinitionSO` for display metadata and balancing hooks, and a BlobAsset pipeline for Burst-compatible runtime access.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| `DIG.Shared` asmdef | Assembly reference | Shared types |
| `Unity.Entities` | ECS runtime | IComponentData support |
| `Unity.Collections` | Native containers | BlobAsset building |
| Framework Rewards/ | RewardDefinitionSO | Base reward types |

### New Setup Required
1. Create folder structure `Assets/Scripts/Economy/` with subfolders: `Components/`, `Definitions/`, `Blobs/`, `Authoring/`, `Utilities/`, `Debug/`
2. Create assembly definition `Hollowcore.Economy.asmdef`
3. Implement the `RewardCategoryType` enum and `RewardCategoryTag` IComponentData
4. Create 7 `RewardCategoryDefinitionSO` assets
5. Implement blob pipeline and database authoring
6. Place `RewardCategoryDatabaseAuthoring` in the economy subscene

---

## 1. Assembly Definition
**Create:** `Assets/Scripts/Economy/Hollowcore.Economy.asmdef`

### 1.1 References
| Reference | Purpose |
|-----------|---------|
| `DIG.Shared` | Shared types |
| `Unity.Entities` | ECS components |
| `Unity.Collections` | NativeArray, BlobAsset |
| `Unity.Burst` | Burst-compiled systems |

---

## 2. RewardCategoryType Enum
**Create:** `Assets/Scripts/Economy/Components/RewardCategoryType.cs`
**Namespace:** `Hollowcore.Economy`

### 2.1 Enum Values
| Value | Name | Strategic Role |
|-------|------|---------------|
| 0 | Face | Infiltration / social tools (toll skips, disguise checks) |
| 1 | Memory | Tempo / combat consumables (reflex loops, borrowed skills) |
| 2 | Augment | Movement / weapon equipment (grapples, heat shielding) |
| 3 | CompendiumPage | Tactical run modifiers (EPIC 9) |
| 4 | Currency | District-specific currencies (Secrets, Memories, Contracts) |
| 5 | BossCounterToken | Boss mechanic counters (EPIC 14) |
| 6 | LimbSalvage | District-specific prosthetics with memory bonuses (EPIC 1) |

**Tuning tip:** The enum rename from `RewardCategory` to `RewardCategoryType` in `Hollowcore.Economy` is intentional. EPIC 13 uses `DistrictRewardFocus` in `Hollowcore.District` for district-level theming. Use `RewardCategoryMapping` utility to bridge the two.

---

## 3. RewardCategoryDefinitionSO
**Create:** `Assets > Create > Hollowcore/Rewards/Category Definition`
**Recommended location:** `Assets/Data/Rewards/Categories/`

### 3.1 Identity Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Category | RewardCategoryType enum | — | 0-6 |
| DisplayName | Player-facing name | — | non-empty string |
| Description | Tooltip/lore text | — | TextArea |
| Icon | Category sprite | — | required |
| CategoryColor | Tint for UI elements | — | Color |

### 3.2 Gameplay Context Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| PlayerFacingRole | Short description of strategic purpose | — | TextArea(2,5) |
| PrimaryDistricts | District IDs where this is the primary reward focus | — | int[] |

### 3.3 Scarcity Design Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| BaseDropWeight | Relative weight in general loot tables | 0.5 | 0.0-1.0 |
| AvailableInRandomEvents | Can appear in random event rewards | true | bool |
| ShowOnGateCards | Appears in gate reward tag display | true | bool |

### 3.4 Inventory Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| CarryLimit | Max items of this category (-1 = unlimited) | -1 | -1 to 999 |
| PersistsBetweenDistricts | Whether items persist across district transitions | true | bool |

### 3.5 Visual Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| RarityColors | Per-rarity color overrides (5 entries) | White/Green/Blue/Purple/Gold | Color[5] |
| PickupVFXPrefab | Category-specific pickup VFX | — | GameObject |
| PickupSFXKey | Audio key for pickup sound | — | string |

**Tuning tip:** Set `BaseDropWeight = 0` for BossCounterToken -- these should never appear from random rolls, only from specific side goal completions.

---

## 4. Per-Category Configuration

Create one SO per category in `Assets/Data/Rewards/Categories/`:

| Category | CarryLimit | PersistsBetweenDistricts | BaseDropWeight |
|----------|-----------|------------------------|---------------|
| Face | 10 | false (expedition only) | 0.4 |
| Memory | 5 | false (current district) | 0.6 |
| Augment | -1 | true | 0.3 |
| CompendiumPage | 10 (governed by CompendiumPageConfig) | true | 0.2 |
| Currency | per-type cap | true (universal) / false (district) | 0.8 |
| BossCounterToken | 3 per token type | true | 0.0 |
| LimbSalvage | -1 (governed by chassis) | true | 0.5 |

---

## 5. BlobAsset Pipeline
**Create:** `Assets/Scripts/Economy/Blobs/RewardCategoryBlob.cs`

### 5.1 RewardCategoryDatabaseAuthoring
**Add to:** Economy subscene root entity
**Component:** `RewardCategoryDatabaseAuthoring` MonoBehaviour

| Field | Description | Default |
|-------|-------------|---------|
| Categories | Array of all 7 RewardCategoryDefinitionSO assets | Assign in inspector |

The baker creates a `RewardCategoryDatabaseRef` singleton with a `BlobAssetReference<RewardCategoryDatabase>` containing 7 `RewardCategoryBlob` entries indexed by `(int)RewardCategoryType`.

---

## 6. RewardCategoryMapping Utility
**Create:** `Assets/Scripts/Economy/Utilities/RewardCategoryMapping.cs`

Bridges `Hollowcore.Economy.RewardCategoryType` (EPIC 10) to `Hollowcore.District.DistrictRewardFocus` (EPIC 13). Not all values map 1:1 -- `Weapons`, `Chassis`, `Recipes`, `Intel` from district theming have no direct economy equivalent.

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Economy subscene | `RewardCategoryDatabaseAuthoring` on root entity | Assign all 7 SO assets |
| — | — | No player prefab changes needed (components are server-only) |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Using `RewardCategory` instead of `RewardCategoryType` | Namespace collision with EPIC 13 | Use `Hollowcore.Economy.RewardCategoryType` |
| Setting CarryLimit = 0 | Players cannot carry any items of this category | Use -1 for unlimited |
| BaseDropWeight < 0 | Negative weight crashes distribution system | Clamp to [0, 1] |
| RarityColors array != 5 entries | Index out of bounds in presentation | Always exactly 5 entries |
| BossCounterToken BaseDropWeight > 0 | Tokens appear from random rolls (should be side-goal only) | Set to 0.0 |
| Missing RewardCategoryDatabaseAuthoring in subscene | `RewardCategoryDatabaseRef` singleton missing at runtime | Add to economy subscene |

---

## Verification
- [ ] `RewardCategoryType` enum has 7 values matching GDD reward types
- [ ] `RewardRarity` enum has 5 tiers (Common, Uncommon, Rare, Epic, Legendary)
- [ ] `RewardCategoryTag` and `RewardRarityTag` are lightweight IComponentData (no ghost fields -- server-only)
- [ ] All 7 `RewardCategoryDefinitionSO` assets created with distinct icons and colors
- [ ] `PrimaryDistricts` arrays populated correctly for Face, Memory, Augment categories
- [ ] `CarryLimit` values set: Face=10, Memory=5, Currency=per-type, BossCounterToken=3, others=-1
- [ ] `PersistsBetweenDistricts` correct: Memory=false, all others=true
- [ ] `BaseDropWeight` tuned: BossCounterToken=0, others in 0.1-0.8 range
- [ ] `RarityColors` consistent across all categories (same 5-color language)
- [ ] `RewardCategoryDatabaseRef` singleton exists at runtime with 7 blob entries
- [ ] `RewardCategoryMapping` correctly bridges economy and district enums
- [ ] OnValidate warnings fire for CarryLimit=0, empty DisplayName, null Icon
- [ ] Build validator catches duplicate categories and missing definitions
