# EPIC 10.4 Setup Guide: Reward Presentation

**Status:** Planned
**Requires:** EPIC 10.1 (RewardCategoryType, RewardRarity), EPIC 10.3 (RewardGrantEvent); Framework: VFX/, UI toolkit

---

## Overview

Reward presentation controls how the player perceives value. This sub-epic covers the reward chest UI (inspect before taking), the rarity visual language (consistent color and VFX across all categories), the post-district summary screen, and the limb-specific preview panel. All data flows through `RewardPresentationBridge` (managed SystemBase) to `RewardUIRegistry`, following the `CombatUIBridgeSystem` pattern.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 10.1 | RewardCategoryDefinitionSO | Category icons and colors |
| EPIC 10.3 | RewardGrantEvent entities | Reward data source |
| Framework VFX/ | VFXRequest pipeline | World-space VFX spawning |
| Framework Interaction/ | InteractableAuthoring | Chest interaction |

### New Setup Required
1. Create `RewardRarityVisualSO` asset with 5 rarity entries
2. Create `RewardUIRegistry` static managed singleton
3. Build UI prefabs: chest panel, summary panel, limb preview, floating text
4. Create per-rarity VFX prefabs (5 pickup + 5 chest + 5 idle)
5. Wire `RewardPresentationBridge` managed system

---

## 1. RewardRarityVisualSO
**Create:** `Assets > Create > Hollowcore/Rewards/Rarity Visuals`
**Recommended location:** `Assets/Data/Rewards/RarityVisuals.asset`

### 1.1 Per-Rarity Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Rarity | RewardRarity enum | — | 0-4 |
| DisplayLabel | "Common" through "Legendary" | — | string |
| PrimaryColor | Main rarity color | see table | Color |
| GlowColor | Emission tint | see table | Color |
| BackgroundColor | Card background tint | see table | Color |
| PickupVFXPrefab | World pickup VFX | — | GameObject |
| ChestVFXPrefab | Chest reveal VFX | — | GameObject |
| IdleVFXPrefab | Looping uncollected VFX | — | GameObject |
| PickupSFXKey | Pickup audio key | — | string |
| RevealSFXKey | Chest reveal audio key | — | string |
| FrameSprite | Reward card border | — | Sprite |
| RarityBadgeSprite | Small badge icon | — | Sprite |
| CardScaleMultiplier | Card size scaling | 1.0 | 0.8-1.2 |

### 1.2 Rarity Color Table
| Rarity | Color | Hex | VFX | SFX |
|--------|-------|-----|-----|-----|
| Common | White | #CCCCCC | Subtle sparkle | Soft chime |
| Uncommon | Green | #4CAF50 | Green burst | Rising tone |
| Rare | Blue | #2196F3 | Blue energy swirl | Crystal resonance |
| Epic | Purple | #9C27B0 | Purple lightning arc | Deep harmonic |
| Legendary | Gold | #FFD700 | Golden pillar + sparks | Triumphant fanfare |

**Tuning tip:** CardScaleMultiplier: 1.0 (Common) through 1.15 (Legendary) creates subtle size hierarchy without breaking grid layout.

---

## 2. UI Prefabs
**Recommended location:** `Assets/Prefabs/UI/Rewards/`

| Prefab | Layout | Data Source |
|--------|--------|-------------|
| RewardChestPanel | Title + 1-4 card grid + "Take All"/"Leave" | RewardUIRegistry.CurrentChestContents |
| PostDistrictSummaryPanel | 7-row category breakdown + "Best Find" | RewardUIRegistry.DistrictSummary |
| LimbPreviewPanel | Slot + stat comparison + equip/stash/discard | RewardUIRegistry.LimbPreview |
| RewardFloatingText | Rarity-colored text, fades over 1.5s | Direct spawn from bridge |

---

## 3. VFX Prefabs
**Recommended location:** `Assets/Prefabs/VFX/Rewards/`

Create 15 VFX prefabs total: 5 pickup, 5 chest reveal, 5 idle per rarity tier.

---

## 4. Presentation Styles
| Style | Trigger | Visual |
|-------|---------|--------|
| Pickup | Enemy drops, small containers | Floating text + brief VFX |
| ChestReveal | Chest interaction | Full chest UI with inspect-before-take |
| QuestReward | Quest completion | Banner with reward callout |
| BossReward | Boss kill | Dramatic fanfare sequence |
| ExtractionBonus | District extraction | End-of-district bonus |

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| UI Canvas | RewardChestPanel (inactive) | Activated by bridge |
| UI Canvas | PostDistrictSummaryPanel (inactive) | Shown at extraction |
| UI Canvas | LimbPreviewPanel (inactive) | Shown for limb rewards |
| World space | RewardFloatingText pool (10 instances) | Object pool |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| RarityVisuals != 5 entries | Index out of bounds | Exactly 5 entries required |
| Entry[i].Rarity != i | Wrong colors for wrong tier | Match index to enum value |
| CardScaleMultiplier <= 0 | Invisible cards | Keep > 0 |
| Missing PickupVFXPrefab | No world VFX | Assign all 5 rarities |
| Direct ECS query from UI MonoBehaviour | Thread safety violation | Use RewardPresentationBridge only |

---

## Verification
- [ ] `RewardPresentationBridge` routes `RewardGrantEvent` to correct style
- [ ] Pickup: floating text with rarity color at collection point
- [ ] Chest: UI opens with all contents as inspectable cards
- [ ] "Leave" closes chest without looting -- chest remains interactable
- [ ] Colors consistent: Common=white, Uncommon=green, Rare=blue, Epic=purple, Legendary=gold
- [ ] VFX scales with rarity (Legendary pillar, Common sparkle)
- [ ] PostDistrictSummaryPanel shows per-category breakdown at extraction
- [ ] LimbPreviewPanel shows stat comparison with green/red deltas
- [ ] Boss reward triggers dramatic sequence distinct from normal chest
- [ ] All UI data flows through bridge -- no direct ECS queries
