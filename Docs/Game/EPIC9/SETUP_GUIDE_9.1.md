# EPIC 9.1 Setup Guide: Compendium Pages

**Status:** Planned
**Requires:** Framework Roguelite/ (RunModifierStack), Items/, EPIC 4 (Districts), EPIC 6 (Gate Screen)

---

## Overview

Compendium Pages are run-level consumable modifiers the player earns during exploration and spends for immediate tactical advantage. Three page types -- Scout (information), Suppression (threat mitigation), and Insight (weakness reveal) -- each address a different tactical axis. Pages slot into a limited inventory (6 active slots) and are consumed on use, injecting temporary entries into the framework's RunModifierStack.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Framework Roguelite/ | RunModifierStack | Page effects compose as run modifiers |
| Player prefab | CompendiumAuthoring | Creates child entity with page buffer |
| Persistent subscene | CompendiumPageConfigAuthoring | Slot limits and carry caps |

### New Setup Required
1. Create `Assets/Scripts/Compendium/` folder with Components/, Definitions/, Systems/, Authoring/, Bridges/ subfolders
2. Create `Hollowcore.Compendium.asmdef` referencing DIG.Shared, Unity.Entities, Unity.NetCode, Unity.Collections, Unity.Burst
3. Create CompendiumPageDefinitionSO assets at `Assets/Data/Compendium/Pages/`
4. Add `CompendiumAuthoring` to the player prefab
5. Add `CompendiumPageConfigAuthoring` singleton to the persistent subscene
6. Reimport subscene after adding authoring components

---

## 1. Creating Page Definitions

**Create:** `Assets > Create > Hollowcore/Compendium/Page Definition`
**Recommended location:** `Assets/Data/Compendium/Pages/`

Naming convention: `Page_{Type}_{Name}.asset`
Example: `Page_Scout_GateReveal.asset`, `Page_Suppression_FrontSlow.asset`, `Page_Insight_BossWeakness.asset`

### 1.1 Identity Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `PageId` | Unique ID across all page definitions | -- | > 0, unique |
| `DisplayName` | Player-facing name | "" | -- |
| `Description` | Effect description text | "" | -- |
| `Icon` | Sprite for inventory display | null | Required |
| `PageType` | Scout, Suppression, or Insight | Scout | CompendiumPageType enum |

### 1.2 CompendiumPageType Enum
| Value | Tactical Axis | Color | Examples |
|-------|--------------|-------|----------|
| `Scout` (0) | Information | Blue | Reveal gates, Front patterns, hidden POIs, echo locations |
| `Suppression` (1) | Threat mitigation | Red | Slow Front, weaken factions, reduce hazards |
| `Insight` (2) | Weakness reveal | Gold | Boss weaknesses, echo mutation types, reward contents |

### 1.3 Effect Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `ModifierKey` | RunModifierStack key injected on consumption | "" | Non-empty (required) |
| `ModifierValue` | Effect value (duration for Suppression, radius for Scout) | 1.0 | > 0 |
| `EffectDuration` | Seconds the modifier stays active (0 = permanent for district) | 0 | 0-600 |

### 1.4 Scout-Specific Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `RevealGates` | Reveals forward gate destinations | false | -- |
| `RevealHiddenPOIs` | Reveals hidden POIs in current district | false | -- |
| `RevealFrontPattern` | Reveals the Front's current advance pattern | false | -- |
| `RevealEchoLocations` | Reveals echo zone locations on minimap | false | -- |

### 1.5 Suppression-Specific Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `FrontSlowMultiplier` | Multiplier on Front advance speed (0.5 = half speed) | 1.0 | 0.1-1.0 |
| `WeakenFactionId` | Faction ID to weaken (-1 = all factions) | -1 | -1 or valid FactionId |
| `FactionDamageReduction` | Damage reduction on weakened faction | 0.0 | 0.0-1.0 |

### 1.6 Insight-Specific Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `RevealBossWeakness` | Shows next boss's elemental weakness | false | -- |
| `RevealEchoMutations` | Shows echo mutation types before entering zones | false | -- |
| `RevealRewardContents` | Shows reward chest contents before opening | false | -- |

### 1.7 Rarity
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `Rarity` | Loot table weighting tier | Common | Common(0), Uncommon(1), Rare(2) |

**Tuning tip:** Start with one page per type at Common rarity (ScoutBasic, SuppressionBasic, InsightBasic). Scout pages that reveal gates are the most universally useful. Suppression pages that slow the Front are strongest in longer districts. Insight pages shine before boss encounters.

---

## 2. CompendiumAuthoring (Player Prefab)

**File:** `Assets/Scripts/Compendium/Authoring/CompendiumAuthoring.cs`
**Add to:** Player prefab (Warrok_Server or equivalent)

The baker:
1. Creates an additional child entity (TransformUsageFlags.None)
2. Adds `DynamicBuffer<CompendiumPageState>` to the child entity
3. Adds `CompendiumLink` to the player entity pointing to the child

This follows the TargetingModuleLink child entity pattern to avoid the 16KB player archetype limit. The `CompendiumLink` component is only 8 bytes on the player entity.

---

## 3. CompendiumPageConfig Singleton

**Add:** `CompendiumPageConfigAuthoring` to a singleton GameObject in the persistent subscene.

### 3.1 Inspector Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `MaxActiveSlots` | Active page slots available to the player | 6 | 1-10 |
| `MaxTotalPages` | Maximum pages carried including overflow | 10 | 6-20 |

**Tuning tip:** 6 active slots and 10 total gives players room to hold a few extras while creating inventory pressure. Players who hoard without using pages should feel the cap.

---

## 4. Page Lifecycle

### 4.1 Acquisition (PageAcquisitionSystem)
1. Loot/reward systems create `PageGrantEvent` transient entities
2. System resolves player's `CompendiumLink` to child entity
3. Checks buffer length against `MaxTotalPages` -- rejects if full
4. Appends new `CompendiumPageState` element
5. Auto-assigns `SlotIndex` if active slots available, else `SlotIndex = -1` (overflow)

### 4.2 Activation (PageActivationSystem)
1. Client UI creates `PageActivationRequest` entity (via RPC)
2. System resolves player's `CompendiumLink` to child entity
3. Validates page exists in buffer by `PageDefinitionId + SlotIndex`
4. Looks up `CompendiumPageDefinitionSO` -- injects modifier into RunModifierStack
5. Applies type-specific effects (Scout reveals, Suppression modifiers, Insight flags)
6. Removes the `CompendiumPageState` element from buffer
7. Fires `PageConsumedEvent` for UI notification

### 4.3 Expiration (PageConsumeSystem)
1. Tracks active page effects with remaining duration
2. Decrements duration each frame
3. Removes expired RunModifierStack entries
4. Permanent effects (Scout reveals) persist for the entire district

---

## 5. Page UI Integration

Pages are displayed in two places:
1. **Page Management Panel** (EPIC 9.3) -- slot management, activation
2. **HUD Quick Bar** (optional) -- shows active slots with hotkey activation

The `CompendiumUIBridgeSystem` (EPIC 9.3) pushes page state to the UI registry.

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player prefab | `CompendiumAuthoring` | Creates child entity with page buffer |
| Persistent subscene | `CompendiumPageConfigAuthoring` singleton | MaxActiveSlots=6, MaxTotalPages=10 |
| `Assets/Data/Compendium/Pages/` | At minimum 3 page definitions (one per type) | ScoutBasic, SuppressionBasic, InsightBasic |
| `Hollowcore.Compendium.asmdef` | Assembly definition | References DIG.Shared, Unity.Entities, Unity.NetCode |
| Subscene reimport | After adding CompendiumAuthoring | Required for child entity baking |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| CompendiumAuthoring not on player prefab | No CompendiumLink on player entity, pages don't work | Add CompendiumAuthoring to player prefab |
| Subscene not reimported after adding authoring | Child entity not baked, CompendiumLink points to Entity.Null | Reimport subscene |
| PageId not unique | Wrong page activated/consumed | Ensure all PageIds are unique across all definitions |
| ModifierKey empty | Page consumed but no RunModifierStack effect | Set ModifierKey to a valid non-empty string |
| Scout page with no reveal flags | Page consumed with no visible effect | Set at least one reveal flag true |
| Suppression FrontSlowMultiplier = 0 | Front stops entirely (too powerful) | Validation enforces (0, 1]; recommend 0.3-0.7 |
| MaxTotalPages < MaxActiveSlots | Overflow impossible but logic expects it | Set MaxTotalPages >= MaxActiveSlots |
| 16KB archetype violation | Ghost baking fails, no ghosts load | Verify CompendiumLink is only 8 bytes; buffer lives on CHILD entity |

---

## Verification

- [ ] CompendiumLink on player entity references a valid child entity
- [ ] Child entity has `DynamicBuffer<CompendiumPageState>` with capacity 6
- [ ] CompendiumPageConfig singleton baked with correct MaxActiveSlots and MaxTotalPages
- [ ] PageAcquisitionSystem adds pages to buffer from PageGrantEvent
- [ ] Page inventory respects MaxTotalPages cap; overflow rejected with event
- [ ] Auto-slot assignment fills empty active slots before overflow
- [ ] PageActivationSystem consumes page and injects RunModifierStack entry
- [ ] Scout page reveals forward gate info (RevealGates flag set)
- [ ] Suppression page slows Front advance speed for configured duration
- [ ] Insight page reveals boss weakness flags
- [ ] PageConsumeSystem expires time-limited effects correctly
- [ ] 16KB archetype limit not exceeded on player entity
- [ ] No ghost replication errors (buffer on child entity, not player ghost)
