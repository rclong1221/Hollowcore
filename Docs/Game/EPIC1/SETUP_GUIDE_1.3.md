# EPIC 1.3 Setup Guide: Limb Salvage & Equipping

**Status:** Planned
**Requires:** EPIC 1.1 (ChassisState, LimbInstance, ChassisLink), Framework Interaction/ system, Framework Items/ system, Framework Loot/ pipeline

---

## Overview

Limb Salvage & Equipping is how players acquire and install replacement limbs into their chassis. Limbs come from environment pickups, enemy corpse drops, quest rewards, and body shop vendors. When a limb is equipped into an already-occupied slot, the current limb is ejected as a world pickup. The system bridges the existing Interaction/ and Loot/ frameworks into the chassis pipeline via transient request entities (LimbEquipRequest, LimbDropRequest).

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab (Subscene) | `ChassisAuthoring` (EPIC 1.1) | Chassis child entity with LimbInstance buffer |
| Player Prefab (Subscene) | `PlayerTag` | Identifies player entities |
| Framework | Interaction/ system | Handles proximity interaction prompts |
| Framework | Loot/ pipeline (LootTableSO) | Enemy death loot rolls |
| Framework | Trading/ system | Body shop vendor purchases |
| Data | LimbDefinitionSO assets (EPIC 1.1) | At least 6 Common limbs for starting set |

### New Setup Required

1. Create LimbPickup prefab for world-placed limb entities
2. Add limb entries to enemy loot tables (LootTableSO)
3. Configure body shop vendor stock with LimbDefinitionSO references
4. Add LimbPickup interaction definition to Interaction/ config
5. Create limb stats comparison UI prefab
6. (Optional) Place pre-authored LimbPickup entities in zone subscenes

---

## 1. LimbPickup Prefab

**Create:** `Assets > Create > Prefab` (manual), then add authoring components
**Recommended location:** `Assets/Prefabs/Chassis/LimbPickup.prefab`

This is the world entity representing a salvageable limb on the ground.

### 1.1 Required Components

| Component | Field | Description | Default |
|-----------|-------|-------------|---------|
| `LimbPickupAuthoring` | LimbDefinitionId | ID referencing a `LimbDefinitionSO` | (required) |
| | SlotType | `ChassisSlot` this limb fits | LeftArm |
| | Rarity | Loot rarity tier | Common |
| | IntegrityPercent | Starting integrity as fraction of max | 1.0 |
| `InteractableAuthoring` | InteractionType | Set to `LimbPickup` | LimbPickup |
| | Range | Interaction distance (meters) | 2.0 |
| `PhysicsShapeAuthoring` | Shape | Collision shape for world presence | Box (small) |
| `GhostAuthoringComponent` | PrefabType | Server-only (clients see via ghost) | Server |

### 1.2 Visual Setup

| Field | Description | Default |
|-------|-------------|---------|
| **Mesh** | Generic limb crate/container or the limb mesh itself | Crate mesh |
| **Rarity Glow Material** | Emission color by rarity (white/green/blue/purple/gold) | White |
| **Pickup VFX** | Particle system on interact (consumed on pickup) | Sparkle burst |

**Tuning tip:** Use a generic crate mesh for environment pickups and the actual limb mesh for enemy drops and body shop displays. This gives visual variety while keeping the prefab pipeline simple.

---

## 2. Enemy Loot Table Integration

**Edit:** Existing `LootTableSO` assets in `Assets/Data/Loot/`

### 2.1 Adding Limb Drops to Loot Tables

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **EntryType** | Set to `LimbDrop` | (select) | Enum |
| **LimbDefinitionSO** | Reference to the limb asset to drop | (required) | Any LimbDefinitionSO |
| **DropWeight** | Relative weight in the loot table | 10 | 1-100 |
| **IntegrityRange** | Min/max integrity percent for salvaged limbs | 0.5-1.0 | 0.1-1.0 |

**Tuning tip:** Keep limb drop weight at 15-25% of total table weight for standard enemies. Higher makes limbs feel disposable; lower makes chassis upgrades too rare. Boss tables should have 100% limb drop with Epic+ rarity.

### 2.2 Salvage Quality by Source

| Source | Integrity Range | Rarity Range | Notes |
|--------|----------------|--------------|-------|
| Environment (crates) | 0.3-0.7 | Junk-Uncommon | Pre-placed in zone generation |
| Enemy corpse drops | 0.5-1.0 | Common-Rare | Rolled from LootTableSO |
| Quest rewards | 0.8-1.0 | Uncommon-Epic | Fixed reward, not random |
| Body shop vendor | 1.0 | Common-Epic | Full price = full integrity |
| Echo mission rewards | 0.9-1.0 | Rare-Legendary | EPIC 5 |
| Boss drops | 1.0 | Epic-Legendary | Guaranteed unique limb |

---

## 3. Body Shop Vendor Setup

**Edit:** Vendor NPC prefab using Trading/ system
**Recommended data location:** `Assets/Data/Trading/BodyShop/`

### 3.1 Vendor Inventory Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **VendorType** | Set to `BodyShop` | BodyShop | Enum |
| **StockEntries** | Array of LimbDefinitionSO references | (required) | 6-12 per district |
| **PriceMultiplier** | District-specific price scaling | 1.0 | 0.5-3.0 |
| **RefreshOnDistrictEntry** | Restock when player enters district | true | bool |

### 3.2 Price Scaling by Rarity

| Rarity | Base Price (District Currency) | Notes |
|--------|-------------------------------|-------|
| Common | 50 | Affordable in district 1 |
| Uncommon | 150 | Mid-district earnings |
| Rare | 400 | Late-district savings |
| Epic | 1000 | ~2 districts of savings |
| Legendary | N/A | Never sold (boss drop only) |

**Tuning tip:** Set PriceMultiplier higher in later districts (1.5-2.0) to maintain upgrade tension. District 1 should feel generous; district 5+ should feel like a real investment.

---

## 4. Limb Pickup Interaction Bridge

**Create:** `Assets/Scripts/Chassis/Bridges/LimbPickupInteractionBridge.cs` (MonoBehaviour)
**Attach to:** UI Canvas root or interaction manager GameObject

### 4.1 Configuration

| Field | Description | Default |
|-------|-------------|---------|
| **StatsComparisonPrefab** | UI prefab showing current vs pickup limb stats | (required) |
| **SlotSelectionPrefab** | UI prefab for ambidextrous limb slot choice | (required) |
| **AutoEquipIfEmpty** | Skip confirmation if target slot is empty | true |

The bridge listens for Interaction/ events on LimbPickup entities and spawns the comparison UI. On player confirmation, it creates a `LimbEquipRequest` transient entity consumed by `LimbEquipSystem`.

---

## 5. Stats Comparison UI

**Create:** UI prefab in `Assets/Prefabs/UI/Chassis/LimbComparisonPanel.prefab`

The panel shows side-by-side stats:
- Current equipped limb (left column) vs pickup limb (right column)
- Green/red delta arrows on each stat line (BonusHealth, BonusDamage, BonusArmor, BonusSpeed, BonusCritChance)
- Integrity bar for both limbs
- Rarity badge and faction origin icon
- Memory bonus comparison (current district + overall)
- "Equip" and "Discard" buttons

---

## 6. Pre-Placed Pickup Entities (Zone Generation)

For environment limb pickups placed in subscenes by zone generation:

1. Drag `LimbPickup.prefab` into the zone subscene
2. Set `LimbPickupAuthoring` fields: LimbDefinitionId, SlotType, Rarity, IntegrityPercent
3. Position at loot-appropriate location (crate, shelf, rubble)
4. Run validator: `Hollowcore > Validation > Chassis` to check placement density

**Tuning tip:** Target 2-4 limb pickups per district zone. Cluster near points of interest, not randomly scattered. No two pickups within 1 meter of each other.

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Subscene | `ChassisAuthoring` (EPIC 1.1 prerequisite) | Already set up if EPIC 1.1 done |
| Zone Subscenes | Pre-placed `LimbPickup` entities | 2-4 per zone, environment sources |
| Global Config Subscene | Nothing new (uses ChassisConfig from 1.1) | |
| UI Canvas | `LimbPickupInteractionBridge` on interaction manager | Bridges Interaction/ to equip flow |
| Trading NPCs | Body shop vendor with LimbDefinitionSO stock | Per-district vendor inventory |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| LimbDefinitionId on LimbPickup does not match any LimbDefinitionSO | Pickup interaction fails silently, no equip | Run `Hollowcore > Validation > Chassis` to find orphan references |
| IntegrityPercent set to 0 | Limb equips but immediately counts as destroyed | Clamp minimum to 0.1 in authoring OnValidate |
| Loot table limb drop weight > 50% | Every enemy drops a limb, no upgrade tension | Keep at 15-25% of total table weight |
| Missing InteractableAuthoring on LimbPickup prefab | Player walks over pickup with no interaction prompt | Add InteractableAuthoring with type=LimbPickup |
| Body shop vendor missing PriceMultiplier | All limbs cost base price regardless of district | Set PriceMultiplier per-district (1.0 for district 1, scale up) |
| Two LimbPickup entities within 1m in subscene | Looks like duplicate, confuses player | Validator warns; spread out placements |
| Forgetting to add SlotType to ambidextrous limb entries | Auto-slot detection fails, equip goes to wrong slot | Set SlotType explicitly or mark as ambidextrous in LimbDefinitionSO |

---

## Verification

1. **Environment Pickup** -- Place a LimbPickup in a test subscene, enter play mode. Walk near it:
   ```
   [Interaction] LimbPickup detected: Limb_Necrospire_LeftArm_Bone (Common)
   ```
2. **Stats Comparison UI** -- Interact with pickup. Panel should show current vs new stats with green/red deltas.
3. **Equip into empty slot** -- Confirm equip on an empty slot. Console:
   ```
   [LimbEquipSystem] Equipped Limb_Necrospire_LeftArm_Bone into LeftArm for player E:XX
   ```
4. **Equip into occupied slot** -- Confirm equip on an occupied slot. Old limb should drop as a new LimbPickup entity at player position.
5. **Enemy loot drop** -- Kill an enemy with limb entries in loot table. Check that LimbPickup entity spawns at death position.
6. **Body shop purchase** -- Interact with vendor, buy a limb. Currency should deduct, limb should appear in equip flow.
7. **Entity Debugger** -- After equip, check ChassisLink child entity. LimbInstance buffer should show the new limb in the correct slot.
8. **Validator** -- Run `Hollowcore > Validation > Chassis`. Should report 0 errors for all placed pickups.
