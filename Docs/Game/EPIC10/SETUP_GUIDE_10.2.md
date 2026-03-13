# EPIC 10.2 Setup Guide: District Currency

**Status:** Planned
**Requires:** Framework: Economy/CurrencyTransactionSystem, Trading/; EPIC 4 (Districts), EPIC 10.1 (RewardCategoryType)

---

## Overview

Each of Hollowcore's 15 districts has its own local currency, plus one universal expedition currency ("Creds"). District currencies are earned locally and spent at local vendors. Cross-district conversion is punishing (40-60% loss), incentivizing local spending. The wallet buffer lives on a child entity via `EconomyLink` to avoid the 16KB player archetype limit.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| `Hollowcore.Economy` asmdef | From EPIC 10.1 | Economy namespace |
| Player prefab | EconomyAuthoring | Creates child entity with wallet |
| Economy subscene | CurrencyConversionConfigAuthoring | Global conversion rates |
| Economy subscene | DistrictCurrencyDatabaseAuthoring | All 16 currency definitions |

### New Setup Required
1. Create `DistrictCurrencyDefinitionSO` assets (16 total: 1 universal + 15 district)
2. Add `EconomyAuthoring` to player prefab
3. Place `CurrencyConversionConfigAuthoring` singleton in economy subscene
4. Place `DistrictCurrencyDatabaseAuthoring` in economy subscene
5. Create vendor definitions for vertical slice districts

---

## 1. DistrictCurrencyDefinitionSO
**Create:** `Assets > Create > Hollowcore/Economy/District Currency`
**Recommended location:** `Assets/Data/Economy/Currencies/`

### 1.1 Identity Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| CurrencyId | Unique ID (0=universal, 1-15=district) | — | 0-15 |
| DisplayName | Player-facing name (e.g., "Memory Fragments") | — | non-empty |
| Description | Flavor text | — | TextArea |
| Icon | Currency sprite | — | required |
| TintColor | UI tint | — | Color |

### 1.2 District Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| DistrictId | Owning district (-1 = universal) | -1 | -1 to 15 |

### 1.3 Limits
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| MaxCarry | Max amount player can carry (0 = no cap) | 999 | 0-9999 |

### 1.4 Conversion Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| CustomToUniversalRate | Override rate for this currency (0 = use global) | 0 | 0.0-1.0 |
| ConversionFlavorText | UI text for conversion screen | — | string |

### 1.5 Vendor Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| VendorPriceMultiplier | Base price multiplier at this district's vendors | 1.0 | 0.1-5.0 |

**Tuning tip:** Universal Creds should have MaxCarry=9999 and DistrictId=-1. District currencies should have MaxCarry=999 to pressure local spending.

---

## 2. Currency Assets to Create

| Asset File | CurrencyId | DistrictId | DisplayName | MaxCarry |
|-----------|-----------|-----------|-------------|---------|
| `Creds.asset` | 0 | -1 | Creds | 9999 |
| `MemoryFragments.asset` | 1 | 1 | Memory Fragments | 999 |
| `BioTokens.asset` | 2 | 2 | Bio-Tokens | 999 |
| (one per remaining district) | 3-15 | 3-15 | district-themed | 999 |

---

## 3. CurrencyConversionConfig Singleton
**Add to:** Economy subscene via `CurrencyConversionConfigAuthoring`

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| DistrictToUniversalRate | District to Creds conversion rate | 0.4 | 0.1-0.9 |
| UniversalToDistrictRate | Creds to district conversion rate | 0.5 | 0.1-0.9 |
| CrossDistrictRate | District A to District B (compounds) | 0.2 | 0.05-0.5 |

**Tuning tip:** The cross-district rate should roughly equal `DistrictToUniversalRate * UniversalToDistrictRate`. A rate of 0.2 means 80% value loss going between districts -- this is intentional to prevent hoarding.

---

## 4. EconomyAuthoring on Player Prefab
**Add to:** Player prefab root

The baker creates a child entity with:
- `PlayerWallet` buffer (InternalBufferCapacity=4)
- Sets `EconomyLink.EconomyEntity` on the player entity pointing to the child

`EconomyLink` is only 8 bytes on the player entity -- safe for the 16KB archetype limit.

---

## 5. DistrictVendorDefinitionSO
**Create:** `Assets > Create > Hollowcore/Economy/District Vendor`
**Recommended location:** `Assets/Data/Economy/Vendors/`

### 5.1 Key Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| VendorId | Unique vendor ID | — | int |
| DistrictId | Which district this vendor belongs to | — | 1-15 |
| RequiresBossKill | Only spawns after district boss defeated | false | bool |
| BaseInventory | Starting item list | — | VendorItemEntry[] |
| FrontPhase2Additions | Items added at Front phase 2 | — | VendorItemEntry[] |
| FrontPhase3Additions | Items added at Front phase 3 | — | VendorItemEntry[] |
| PersonalityPriceModifier | Price markup/discount | 1.0 | 0.5-2.0 |

**Tuning tip:** Set PersonalityPriceModifier < 1.0 for friendly vendors (discount) and > 1.0 for hostile or rare-item vendors.

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Economy subscene | `CurrencyConversionConfigAuthoring` singleton | Default rates: 0.4 / 0.5 / 0.2 |
| Economy subscene | `DistrictCurrencyDatabaseAuthoring` with all 16 SOs | All currency assets assigned |
| Player prefab | `EconomyAuthoring` | Creates child entity with wallet |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| CurrencyId outside 0-15 range | Blob index out of bounds | Validate in OnValidate |
| No universal currency (CurrencyId=0) | Build validator error, no starting wallet | Create Creds.asset with CurrencyId=0 |
| MaxCarry=0 on district currency | Players cannot earn currency | Set to 999+ |
| VendorPriceMultiplier <= 0 | Items are free | Clamp to > 0 |
| Missing EconomyAuthoring on player | No wallet buffer, currency transactions fail | Add to player prefab |
| DistrictId=-1 on district currency | Treated as universal | Set DistrictId to 1-15 |

---

## Verification
- [ ] `EconomyLink` on player entity references valid child entity with `PlayerWallet` buffer
- [ ] Universal currency (Creds, CurrencyId=0) wallet entry created on player spawn
- [ ] District currency granted when picking up loot in that district
- [ ] `PlayerWallet.MaxAmount` enforced -- excess currency rejected
- [ ] `CurrencyTransaction` with negative amount fails if insufficient funds
- [ ] `CurrencyChangedEvent` fires on every wallet modification for UI updates
- [ ] District vendors spawn with correct inventory based on unlock conditions
- [ ] Vendor inventory scales with Front phase (new items at higher phases)
- [ ] Purchase request deducts correct currency and grants item
- [ ] Currency conversion at gate screen applies correct rate (40% loss district to universal)
- [ ] Cross-district conversion compounds losses correctly
- [ ] All 16 `DistrictCurrencyDefinitionSO` assets created with distinct icons and names
- [ ] 16KB archetype safe -- EconomyLink is only 8 bytes on player entity
