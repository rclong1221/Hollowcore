# EPIC 2.3 Setup Guide: Revival System

**Status:** Planned
**Requires:** EPIC 2.1 (SoulChipState, EjectedSoulChip), EPIC 2.2 (DeadBodyState, body persistence), EPIC 1.1 (ChassisState), Framework Interaction/ system, Framework Persistence/, Framework Party/ (co-op), EPIC 4.1 (ExpeditionGraphState, district zone topology)

---

## Overview

Revival in Hollowcore is a spatial search problem, not a menu button. When a player dies, the system spawns revival bodies at locations throughout the expedition based on tier, distance, and danger. The first death is forgiving (junky body nearby, free). Each subsequent death pushes viable bodies further away into more hostile territory. Solo players can rely on drone insurance, revival terminals, or rare continuity caches. In co-op, a teammate physically carries the soul chip to a body and channels the revival. The quality of the revival body determines stats, limb slots, and starting loadout for the remainder of the run.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab (Subscene) | `SoulChipAuthoring` (EPIC 2.1) | Soul chip ejection on death |
| Player Prefab (Subscene) | `DroneInsuranceState` | Drone auto-recovery charges |
| Player Prefab (Subscene) | `RevivalSelectionActive` (baked disabled) | Prevents alive-player systems during selection |
| Framework | Party/ system | Co-op chip carry coordination |
| Framework | Persistence/ | Cross-district body persistence |
| EPIC 4.1 | ExpeditionGraphState | District/zone topology for spawn point selection |

### New Setup Required

1. Create `RevivalBodyDefinitionSO` assets per tier per district theme
2. Create the revival body world prefab (visual pod/frame entity)
3. Add `DroneInsuranceState` + `RevivalSelectionActive` to player prefab
4. Create the Revival Selection UI panel
5. Place revival terminal prefabs at designated locations in district subscenes
6. (Optional) Create continuity cache consumable item
7. Create drone recovery VFX (drone carrying chip)
8. Configure co-op carry channel duration and speed penalty

---

## 1. Revival Body Definition Assets

**Create:** `Assets > Create > Hollowcore/Revival/Body Definition`
**Recommended location:** `Assets/Data/Revival/Bodies/`
**Naming convention:** `RevivalBody_[Tier]_[DistrictTheme].asset` -- e.g., `RevivalBody_Cheap_Necrospire.asset`

### 1.1 Identity & Tier

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **DefinitionId** | Unique identifier | (required) | Must be globally unique |
| **BodyName** | Display name in revival selection UI | (required) | Max 32 chars |
| **Tier** | `RevivalBodyTier` enum | Mid | Cheap, Mid, Premium |

### 1.2 Chassis Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **AvailableLimbSlots** | Number of functional limb slots | 5 | 1-6 |
| **FunctionalSlots** | Which ChassisSlots are usable | All 6 | Array of ChassisSlot |

### 1.3 Base Stats

| Field | Description | Cheap | Mid | Premium |
|-------|-------------|-------|-----|---------|
| **HealthMultiplier** | Applied to base health | 0.7 | 1.0 | 1.15 |
| **SpeedMultiplier** | Applied to base move speed | 0.85 | 1.0 | 1.1 |
| **CombatStatMultiplier** | Applied to all combat stats | 0.75 | 1.0 | 1.15 |

### 1.4 Location & Economy

| Field | Description | Cheap | Mid | Premium |
|-------|-------------|-------|-----|---------|
| **MinLocationDanger** | Minimum danger rating of spawn area | 0.0 | 0.2 | 0.6 |
| **MaxLocationDanger** | Maximum danger rating of spawn area | 0.3 | 0.6 | 1.0 |
| **BaseCost** | Currency cost to use this body | 0 | 200 | 800 |

### 1.5 Visual Prefab

| Field | Description |
|-------|-------------|
| **BodyPrefab** | World entity prefab: glowing pod/frame with tier-colored VFX |

**Tuning tip:** Create at least 1 Cheap, 1 Mid, and 1 Premium definition per district aesthetic. Cheap bodies should look visibly damaged (missing arm panel, exposed wiring). Premium bodies should look military/specialized with district-themed augments pre-installed. The visual quality gap between tiers should communicate the stat difference before the player reads numbers.

---

## 2. Revival Body World Prefab

**Create:** Prefab per tier
**Recommended location:** `Assets/Prefabs/Revival/`

### 2.1 Prefab Variants

| Prefab | Tier | Visual Description |
|--------|------|-------------------|
| `RevivalBody_Cheap.prefab` | Cheap | Rusty pod, flickering lights, partially open |
| `RevivalBody_Mid.prefab` | Mid | Clean medical pod, steady blue glow |
| `RevivalBody_Premium.prefab` | Premium | Armored pod, gold/district-color glow, holographic stats |

### 2.2 Required Components

| Component | Field | Description |
|-----------|-------|-------------|
| `RevivalBodyAuthoring` | BodyDefinitionSO | Reference to the definition asset |
| `InteractableAuthoring` | InteractionType = `RevivalBody` | Interaction for selection/channeling |
| | Range | 3.0m |
| `GhostAuthoringComponent` | PrefabType = Server | Network replication |
| `PhysicsShapeAuthoring` | Shape | Capsule or box for collision |

---

## 3. Player Prefab Additions

### 3.1 DroneInsuranceState

**Add Component:** `DroneInsuranceStateAuthoring` on player prefab root

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **MaxCharges** | Max drone recovery charges per expedition | 0 | 0-5 |
| **ChargesRemaining** | Set at expedition start (purchased or earned) | 0 | 0-5 |

Size: 16 bytes, AllPredicted ghost. Safe for player archetype.

### 3.2 RevivalSelectionActive

**Add Component:** `RevivalSelectionActiveAuthoring` on player prefab root

Baked disabled. Enabled when player dies and is choosing a revival body. While enabled, movement/combat/interaction systems skip this player.

Size: 0 bytes (enableable component, no data). Safe for player archetype.

### 3.3 ChipCarrier (Co-op)

**Add Component:** `ChipCarrierAuthoring` on player prefab root (baked disabled)

| Field | Description | Default |
|-------|-------------|---------|
| **SpeedMultiplier** | Movement penalty while carrying | 0.6 |
| **ChannelDuration** | Seconds to channel revival at a body | 4.0 |

Size: 16 bytes, AllPredicted ghost. Enableable component, baked disabled.

---

## 4. Revival Selection UI

**Create:** UI prefab in `Assets/Prefabs/UI/Revival/RevivalSelectionPanel.prefab`

### 4.1 Panel Layout

| Section | Description |
|---------|-------------|
| **Header** | "Choose a Revival Body" + death count indicator |
| **Body Cards** | Scrollable list of available revival bodies |
| **Map Preview** | Mini-map showing body locations with danger indicators |
| **Drone Recovery Button** | "Auto-Recover (X charges remaining)" if available |

### 4.2 Body Card Contents

| Element | Description |
|---------|-------------|
| **Tier Badge** | Color-coded: grey (Cheap), blue (Mid), gold (Premium) |
| **Body Name** | From RevivalBodyDefinitionSO.BodyName |
| **Stat Bars** | Health/Speed/Combat multipliers relative to 1.0 baseline |
| **Limb Slots** | Visual 6-slot chassis diagram with functional slots highlighted |
| **Location** | District name + zone name + danger rating bar |
| **Distance** | Meters from death location |
| **Cost** | Currency amount (0 for Cheap) |
| **Select Button** | Commits to this body (deducts currency, creates RevivalRequest) |

**Tuning tip:** Sort body cards by distance (nearest first) within each tier. Highlight the recommended option (nearest Mid-tier body) with a subtle glow. Players in a panic after death should be able to select quickly without over-analyzing.

---

## 5. Revival Terminals (Fixed Locations)

**Create:** Prefab in `Assets/Prefabs/Revival/RevivalTerminal.prefab`
**Placement:** Specific points in each district (body shops, clinics, safe rooms)

### 5.1 Terminal Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **TerminalId** | Unique ID per terminal placement | (required) | Per-district unique |
| **StoredBodyTier** | Tier of body available at this terminal | Cheap | Cheap only |
| **RequiresActivation** | Must player interact to activate before death? | true | bool |
| **ActivationCost** | Currency cost to activate the terminal | 0 | 0-100 |

### 5.2 Terminal States

| State | Visual | Interaction |
|-------|--------|-------------|
| **Undiscovered** | Dim, no indicator | "Activate Terminal [Interact]" |
| **Activated** | Glowing, green light | Already ready (no interaction needed post-death) |
| **Used** | Dark, red light | "Terminal Depleted" (single use) |

**Tuning tip:** Place 2-3 terminals per district at memorable locations (body shops, hub areas). Activation should be trivial (walk up and press interact, no cost by default). The investment is remembering to activate them before you die.

---

## 6. Continuity Cache (Rare Consumable)

**Create:** Via Items/ framework
**Recommended location:** `Assets/Data/Items/Consumables/ContinuityCache.asset`

| Field | Description | Default |
|-------|-------------|---------|
| **ItemType** | Consumable (active placement) | Consumable |
| **PlacedBodyTier** | Mid | Mid |
| **UsageMode** | Place at current location (like a save point) | ActivePlace |
| **SingleUse** | Consumed on next death regardless of selection | true |

When used, places a revival body at the player's current position. On death, this body appears as an option. Consumed on death whether selected or not.

---

## 7. Co-op Chip Carry Configuration

### 7.1 CarrySystem Settings

| Parameter | Description | Default | Range |
|-----------|-------------|---------|-------|
| **CarrySpeedMultiplier** | Movement penalty while carrying chip | 0.6 | 0.3-0.8 |
| **CarrierCanSprint** | Whether carrier can sprint | false | bool |
| **CarrierCanDodge** | Whether carrier can dodge | false | bool |
| **CarrierCanUse2H** | Whether carrier can use two-handed weapons | false | bool |
| **ChannelDuration** | Seconds to channel revival at a body | 4.0 | 2-8 |
| **ChannelInterruptible** | Whether damage interrupts channel | true | bool |

### 7.2 Carrier Death Handling

If the carrier dies while holding a chip:
- Chip drops as a new `EjectedSoulChip` at carrier's death position
- `ChipCarrier` component removed from (now dead) carrier
- Both chips (carrier's own + carried) are now ejected

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Subscene | `DroneInsuranceStateAuthoring` on player root | 16 bytes, AllPredicted |
| Player Subscene | `RevivalSelectionActiveAuthoring` on player root | Baked disabled, 0 bytes |
| Player Subscene | `ChipCarrierAuthoring` on player root | Baked disabled, 16 bytes |
| Global Config Subscene | Revival config singleton (optional tuning) | Carry speed, channel duration |
| District Subscenes | Revival terminal prefabs at designated locations | 2-3 per district |
| Ghost Prefab Registry | Revival body prefabs (Cheap, Mid, Premium) | For network ghost spawning |
| UI Canvas | `RevivalSelectionPanel` prefab | Shows on death |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| No Cheap revival body defined for a district | Player dies with no free body option | Ensure at least 1 Cheap RevivalBodyDefinitionSO exists per district |
| RevivalSelectionActive not baked disabled | Player stuck in revival selection from spawn | Verify authoring bakes the enableable component as disabled |
| Drone insurance charges not reset between expeditions | Charges carry over from previous run | Reset DroneInsuranceState.ChargesRemaining in expedition init |
| Revival body spawns inside geometry | Player revives stuck in wall | Spawn system must validate spawn points against physics colliders |
| Premium body BaseCost = 0 | Players always pick Premium (no tradeoff) | Set appropriate cost scaling with expedition depth |
| Co-op carrier speed multiplier too low (< 0.3) | Carrier effectively cannot move, unfun | Keep at 0.5-0.7 for meaningful penalty without frustration |
| Channel duration too long for co-op (> 6s) | Carrier almost always gets interrupted | Keep at 3-5s; test with 2 enemies nearby |
| FunctionalSlots array does not match AvailableLimbSlots count | Chassis setup breaks on revival | Validator enforces array length == AvailableLimbSlots |
| Forgetting to clean up unclaimed bodies after selection | Orphan revival body entities accumulate | RevivalSelectionSystem destroys unclaimed bodies on selection commit |

---

## Verification

1. **First Death** -- Kill the player (death #1). Console:
   ```
   [RevivalBodySpawnSystem] Spawned 3 revival bodies for SoulId=12345 (death #1): Cheap@(X1,Y1,Z1), Mid@(X2,Y2,Z2), Premium@(X3,Y3,Z3)
   ```

2. **Revival Selection UI** -- UI panel shows all available bodies with tier/distance/cost/stats.

3. **Cheap Body** -- Select the Cheap body. Verify: no cost, 3-4 limb slots, stat multipliers at 0.7/0.85/0.75. Player spawns at body location.

4. **Mid Body** -- Die again, select Mid body. Verify: currency deducted, 5 limb slots, stat multipliers at 1.0.

5. **Escalating Distance** -- Death #2 Cheap body should be further from death location than death #1.

6. **Death #3+** -- Cheap bodies should appear in previous districts. Mid bodies may require backtracking.

7. **Drone Insurance** -- Set ChargesRemaining > 0. Die. Auto-recovery should begin:
   ```
   [DroneRecoverySystem] Drone recovering SoulId=12345 to nearest Cheap body (3.5s)
   ```

8. **Revival Terminal** -- Activate a terminal. Die nearby. Terminal body should appear in selection UI.

9. **Co-op Carry** -- Teammate picks up ejected chip. Verify ChipCarrier added, speed reduced to 60%.

10. **Co-op Channel** -- Carrier approaches revival body, channels for 4s. On complete: player revives, ChipCarrier removed.

11. **Co-op Interrupt** -- Carrier takes damage during channel. Channel cancels, carrier retains chip.

12. **Carrier Death** -- Kill the carrier while carrying. Chip drops as new EjectedSoulChip.

13. **Cross-District** -- Exit district with a revival body in it. Re-enter. Body should persist from save data.

14. **Cleanup** -- After revival completes, unclaimed body entities should be destroyed.
