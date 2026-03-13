# EPIC 1.1 Setup Guide: Chassis State & Limb Stats

**Status:** Planned
**Requires:** Player prefab with DamageableAuthoring, existing DIG framework (ISaveModule, combat pipeline)

This guide covers the Unity Editor setup for the **Chassis & Limb System** — the modular body system where players equip salvaged limbs into chassis slots, each with integrity, stats, and memory bonuses.

---

## Overview

The Chassis system gives each player a multi-slot body. Each slot (LeftArm, RightArm, LeftLeg, RightLeg, Torso, Head) holds a **Limb** — a salvaged component with its own stat block, integrity, rarity, and faction origin. Limbs degrade, can be lost, and are replaced from loot. The system lives on a **child entity** (via `ChassisLink`) to avoid the 16KB player archetype limit.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab (Subscene) | `DamageableAuthoring` | Health/damage pipeline already set up |
| Player Prefab (Subscene) | `PlayerTag` | Identifies player entities |
| Combat Systems | `DamageApplySystem` | Existing damage pipeline (EPIC framework) |
| Save System | `ISaveModule` pipeline | Persistence framework (TypeId 1-18 used by framework) |

### New Setup Required

1. **Create LimbDefinition assets** (one per limb variant in the game)
2. **Add `ChassisAuthoring`** to your player prefab in the subscene
3. **Create a `ChassisConfig`** singleton asset for global tuning
4. **(Optional)** Set up the Chassis Workstation editor window
5. **Reimport subscenes** after adding authoring components

All ECS systems (`ChassisBootstrapSystem`, `ChassisStatAggregatorSystem`, `LimbExpirationSystem`) are auto-created by the world. No manual system registration needed.

---

## 1. Creating Limb Definition Assets

**Create:** `Assets > Create > Hollowcore/Chassis/LimbDefinition`

**Recommended location:** `Assets/Data/Chassis/Limbs/`

**Naming convention:** `Limb_[Faction]_[Slot]_[Variant].asset` — e.g., `Limb_Necrospire_LeftArm_Bone.asset`

### 1.1 Identity

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **LimbId** | Unique `FixedString64Bytes` identifier | (required) | Must be globally unique |
| **DisplayName** | UI-facing name | (required) | Max 32 chars |
| **Slot** | Which `ChassisSlot` this limb fits | LeftArm | Enum: Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg |
| **Rarity** | Loot rarity tier | Common | Common, Uncommon, Rare, Epic, Legendary |
| **FactionOrigin** | Which district faction produced this limb | None | Enum from EPIC 13 factions |

### 1.2 Stat Block

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **MaxIntegrity** | HP of the limb before destruction | 100 | 10–500 |
| **BonusHealth** | Added to player max HP when equipped | 0 | 0–200 |
| **BonusDamage** | Flat damage bonus | 0 | 0–50 |
| **BonusArmor** | Damage resistance | 0 | 0–100 |
| **BonusSpeed** | Movement speed modifier (%) | 0 | -30–30 |
| **BonusCritChance** | Critical hit chance bonus (%) | 0 | 0–25 |

**Tuning tip:** Legs should focus on BonusSpeed, Arms on BonusDamage/BonusCritChance, Torso on BonusHealth/BonusArmor. Head is the "wildcard" slot — exotic effects here. Keep total stat budget per rarity consistent: Common ~50 points, Uncommon ~80, Rare ~120, Epic ~170, Legendary ~230.

### 1.3 Visual Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **MeshPrefab** | The visual mesh swapped onto the chassis socket | (required) | Must have `SkinnedMeshRenderer` |
| **MaterialOverride** | Optional material for faction coloring | null | |
| **IconSprite** | Inventory/UI icon | (required) | 128×128 recommended |

### 1.4 Memory Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **MemoryBonusType** | What memory bonus this limb contributes to | None | Enum: None, DamageUp, ArmorUp, SpeedUp, CritUp, RegenUp, XPUp |
| **MemoryAttunementRate** | How fast this limb builds memory | 1.0 | 0.1–5.0 |

**Tuning tip:** Memory bonuses reward keeping a limb equipped long-term. Set MemoryAttunementRate higher for Common limbs (rewards loyalty to weak gear) and lower for Legendary (they're already strong).

---

## 2. Player Prefab Setup

### 2.1 Add ChassisAuthoring

1. Open your **player prefab** in the subscene
2. Select the root player GameObject
3. **Add Component** → `ChassisAuthoring` (from `Hollowcore.Chassis`)
4. Configure:

| Field | Description | Default |
|-------|-------------|---------|
| **Starting Limbs** | Array of `LimbDefinitionSO` — one per slot for new characters | 6 Common limbs |
| **Create Child Entity** | Must be `true` — chassis lives on child entity | true |

**CRITICAL:** The `ChassisAuthoring` baker creates a child entity with `ChassisState` + `LimbInstance` buffers and stores a `ChassisLink` on the parent player entity. This avoids the 16KB archetype limit on the ghost entity.

### 2.2 Socket Setup (for Visuals)

On the player's **armature/skeleton**, tag these transforms:

| Transform Name | Purpose |
|----------------|---------|
| `Socket_Head` | Head limb mesh attachment point |
| `Socket_Torso` | Torso limb mesh attachment point |
| `Socket_LeftArm` | Left arm limb mesh attachment point |
| `Socket_RightArm` | Right arm limb mesh attachment point |
| `Socket_LeftLeg` | Left leg limb mesh attachment point |
| `Socket_RightLeg` | Right leg limb mesh attachment point |

The `ChassisVisualBridge` MonoBehaviour (EPIC 1.6) auto-finds sockets by name.

---

## 3. Chassis Config Singleton

**Create:** `Assets > Create > Hollowcore/Chassis/ChassisConfig`

**Recommended location:** `Assets/Data/Chassis/ChassisConfig.asset`

Place a GameObject with `ChassisConfigAuthoring` in your **global config subscene**.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **IntegrityRegenRate** | HP/sec limbs regenerate out of combat | 2.0 | 0–10 |
| **IntegrityRegenDelay** | Seconds after last damage before regen starts | 5.0 | 1–30 |
| **EmptySlotPenaltyPercent** | Stat penalty per empty slot (%) | 15 | 0–50 |
| **LimbDestroyThreshold** | Integrity at which limb is destroyed | 0 | 0 (always 0) |
| **MemoryBonusCap** | Max bonus from memory system per limb | 25 | 5–100 |

---

## 4. Asset Checklist for Ship

For a **minimum viable chassis system**, you need:

| Asset Type | Minimum Count | Location |
|------------|--------------|----------|
| LimbDefinitionSO | 6 (one per slot, Common tier) | `Assets/Data/Chassis/Limbs/` |
| LimbDefinitionSO | 18+ recommended (3 per slot across rarities) | `Assets/Data/Chassis/Limbs/` |
| ChassisConfig | 1 | `Assets/Data/Chassis/` |
| Limb Mesh Prefabs | 1 per LimbDefinitionSO | `Assets/Prefabs/Chassis/Limbs/` |
| Limb Icons | 1 per LimbDefinitionSO | `Assets/Art/UI/Chassis/` |

---

## 5. Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Subscene | `ChassisAuthoring` on player root | Creates child entity at bake time |
| Global Config Subscene | `ChassisConfigAuthoring` on config GO | Creates singleton |
| Bootstrap Scene | Nothing | Systems auto-register |

**Reimport subscenes** after adding authoring components.

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Adding ChassisState directly to player entity | "Entity archetype component data is too large" crash | Use `ChassisAuthoring` which creates a child entity via `ChassisLink` |
| Duplicate LimbId across assets | Second limb never appears in loot/equip | Run build-time validator: `Hollowcore > Validation > Chassis` |
| Missing MeshPrefab on LimbDefinitionSO | Invisible limb, socket shows nothing | Validation warning in inspector — check Console |
| Forgetting to reimport subscene | Old component layout, ghosts fail to spawn | Right-click subscene → Reimport |
| Setting BonusSpeed > 30 | Player moves faster than physics can handle | Clamped in OnValidate but check manually |
| Empty StartingLimbs array | Player spawns with all slots empty, -90% stats | Set 6 Common limbs as defaults |

---

## Verification

1. **Enter Play Mode** — Console should show:
   ```
   [ChassisBootstrapSystem] Initialized chassis for player entity E:XX with 6 limbs
   ```
2. **Open Entity Debugger** → Find player entity → Verify `ChassisLink` component exists
3. **Follow ChassisLink** → Child entity should have `ChassisState` + `LimbInstance` buffer with 6 entries
4. **Take damage** → Limb integrity should decrease (check `LimbInstance[slot].CurrentIntegrity`)
5. **Wait 5s** → Integrity should regenerate at `IntegrityRegenRate`
6. **Run validator:** `Hollowcore > Validation > Chassis` — should report 0 errors
