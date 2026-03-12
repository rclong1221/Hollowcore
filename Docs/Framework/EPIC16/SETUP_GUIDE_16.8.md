# SETUP GUIDE 16.8: Player Resource Framework

**Status:** Implemented
**Last Updated:** February 22, 2026
**Requires:** Entity with PlayerTag or AIBrain (EPIC 15.31), Ability system (EPIC 15.32)

This guide covers Unity Editor setup for the player and AI resource pool framework. After setup, entities have up to 2 resource slots (Mana, Stamina, Energy, Rage, Combo, or custom) with automatic regeneration, decay, equipment modifiers, ability cost gating, and UI display.

---

## What Changed

Previously, abilities were free to cast with no resource cost, stamina was a separate hardcoded system, and there was no generic resource model. Equipment had no way to modify resource pools.

Now:

- **ResourcePool** (2 slots) on any entity — predicted, ghost-replicated, 64 bytes
- **8 resource types** — Stamina, Mana, Energy, Rage, Combo, Custom0, Custom1 (plus None)
- **Automatic regeneration** with configurable delay after drain
- **Decay modes** — idle decay (rage/combo drain when not fighting), overflow drain (excess drains back to max)
- **Ability cost gating** — abilities check resource before casting, deduct on cast/tick/complete/hit
- **Equipment modifiers** — gear bonuses to max and regen for Mana, Energy, Stamina
- **Channel resource drain** — channeled weapons drain resources per tick, force-stop when depleted
- **Generation on hit/take** — rage builds when dealing or taking damage
- **UI pipeline** — ResourceBarViewModel + ShaderResourceBarSync for resource bar display
- **Integer resources** — combo counters clamp to whole numbers

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Regeneration | ResourceTickSystem ticks regen after RegenDelay expires |
| Idle decay | Resources flagged DecaysWhenIdle drain at DecayRate per second |
| Overflow drain | Resources flagged DecaysWhenFull drain back to Max |
| Integer clamping | Resources flagged IsInteger floor to whole numbers |
| Equipment application | ResourceModifierApplySystem reads PlayerEquippedStats each frame |
| Ability blocking | PlayerAbilityCostSystem prevents cast if insufficient resource |
| AI ability costs | AbilityCostDeductionSystem handles AI ability resource deduction |
| Hit/take generation | ResourceGenerationSystem reads CombatResultEvents to generate resources |

---

## 1. Resource Pool Setup

### 1.1 Add the Component

1. Select your player or enemy prefab root
2. Click **Add Component** > search for **Resource Pool Authoring**

### 1.2 Inspector Fields

#### Slot 0

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Slot 0 Type** | ResourceType | Mana | Resource type for this slot |
| **Slot 0 Max** | float | 100 | Maximum capacity |
| **Slot 0 Start** | float | 100 | Starting value (typically equal to Max) |
| **Slot 0 Regen Rate** | float | 5 | Per-second regeneration rate |
| **Slot 0 Regen Delay** | float | 2 | Seconds after last drain before regen starts |
| **Slot 0 Decay Rate** | float | 0 | Per-second decay when DecaysWhenIdle flag is set |
| **Slot 0 Generate Amount** | float | 0 | Amount generated per trigger (hit/take damage) |
| **Slot 0 Flags** | ResourceFlags | None | Behavior flags (see below) |

#### Slot 1

Same fields as Slot 0, all defaulting to zero/None. Leave Slot 1 Type as `None` if you only need one resource.

### 1.3 Resource Types

| Type | Value | Typical Use |
|------|-------|-------------|
| **None** | 0 | Slot disabled |
| **Stamina** | 1 | Sprint, dodge, physical actions |
| **Mana** | 2 | Spellcasting, magical abilities |
| **Energy** | 3 | Tech/sci-fi abilities, shields |
| **Rage** | 4 | Builds on hit/take, decays when idle |
| **Combo** | 5 | Builds on hit, integer counter, decays when idle |
| **Custom0** | 6 | Project-specific resource |
| **Custom1** | 7 | Project-specific resource |

### 1.4 Resource Flags

Flags are a bitmask — multiple can be combined:

| Flag | Description |
|------|-------------|
| **CanOverflow** | Allows current to exceed Max (buffs, temporary boosts) |
| **DecaysWhenFull** | Overflow drains back to Max over time |
| **PausedRegen** | No regeneration (useful for rage/combo — they generate, not regen) |
| **GenerateOnHit** | Generate resource when dealing damage (rage, combo) |
| **GenerateOnTake** | Generate resource when taking damage (rage, revenge mechanics) |
| **DecaysWhenIdle** | Decays at DecayRate per second after RegenDelay (rage/combo) |
| **IsInteger** | Clamp to whole numbers (combo counters) |

---

## 2. Preset Configurations

### 2.1 Mage (Mana)

| Field | Value |
|-------|-------|
| Slot 0 Type | Mana |
| Slot 0 Max | 200 |
| Slot 0 Start | 200 |
| Slot 0 Regen Rate | 8 |
| Slot 0 Regen Delay | 3 |
| Slot 0 Flags | None |

### 2.2 Warrior (Rage)

| Field | Value |
|-------|-------|
| Slot 0 Type | Rage |
| Slot 0 Max | 100 |
| Slot 0 Start | 0 |
| Slot 0 Regen Rate | 0 |
| Slot 0 Regen Delay | 5 |
| Slot 0 Decay Rate | 3 |
| Slot 0 Generate Amount | 10 |
| Slot 0 Flags | PausedRegen, GenerateOnHit, GenerateOnTake, DecaysWhenIdle |

### 2.3 Rogue (Energy + Combo)

| Field | Value | |
|-------|-------|-|
| Slot 0 Type | Energy | |
| Slot 0 Max | 100 | |
| Slot 0 Start | 100 | |
| Slot 0 Regen Rate | 10 | |
| Slot 0 Regen Delay | 1.5 | |
| Slot 0 Flags | None | |
| Slot 1 Type | Combo | |
| Slot 1 Max | 5 | |
| Slot 1 Start | 0 | |
| Slot 1 Regen Rate | 0 | |
| Slot 1 Regen Delay | 4 | |
| Slot 1 Decay Rate | 1 | |
| Slot 1 Generate Amount | 1 | |
| Slot 1 Flags | PausedRegen, GenerateOnHit, DecaysWhenIdle, IsInteger |

### 2.4 AI Enemy (Simple Mana)

| Field | Value |
|-------|-------|
| Slot 0 Type | Mana |
| Slot 0 Max | 50 |
| Slot 0 Start | 50 |
| Slot 0 Regen Rate | 3 |
| Slot 0 Regen Delay | 5 |
| Slot 0 Flags | None |
| Slot 1 Type | None |

---

## 3. Ability Resource Costs

### 3.1 AI Abilities (AbilityDefinitionSO)

Open any `AbilityDefinitionSO` ScriptableObject and look for the **Resource Cost** header:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Resource Cost Type** | ResourceType | None | Which resource this ability costs. None = free |
| **Resource Cost Timing** | CostTiming | OnCast | When the cost is deducted |
| **Resource Cost Amount** | float | 0 | Amount consumed |

### 3.2 Cost Timing

| Timing | When Deducted | Use Case |
|--------|---------------|----------|
| **OnCast** | When ability enters Casting phase | Standard spells, attacks |
| **PerTick** | Every tick during Active phase | Channels, beams, drains. Interrupts ability if resource depleted |
| **OnComplete** | When ability enters Recovery phase | Delayed-cost abilities |
| **OnHit** | When ability deals damage | Pay-on-impact abilities |

### 3.3 Player Abilities

Player ability definitions (in `AbilityComponents.cs`) have:

| Field | Type | Description |
|-------|------|-------------|
| **ResourceCostType** | ResourceType | Which resource to check/deduct |
| **ResourceCostAmount** | float | Amount required and deducted |

Player abilities always deduct OnCast. If the player lacks sufficient resource, the ability is blocked from casting.

### 3.4 Channel Weapon Resource Drain

Channeled weapons (`ChannelActionSystem`) drain resources per tick via `ResourcePool.TryDeduct()`. When the resource is fully depleted, the channel force-stops and the weapon returns to idle.

**Action required:** Set the resource cost fields on the channel weapon's ability definition. The channel system reads these automatically.

---

## 4. Equipment Resource Modifiers

Equipment items can modify resource pools through `ItemStatBlock`.

### 4.1 Item Stat Block Fields

These fields are set on item definitions (via `ItemStatBlock` on the item entity):

| Field | Type | Description |
|-------|------|-------------|
| **Max Mana Bonus** | float | Added to Mana slot max capacity |
| **Mana Regen Bonus** | float | Added to Mana slot regen rate |
| **Max Energy Bonus** | float | Added to Energy slot max capacity |
| **Energy Regen Bonus** | float | Added to Energy slot regen rate |
| **Max Stamina Bonus** | float | Added to Stamina slot max capacity |
| **Stamina Regen Bonus** | float | Added to Stamina slot regen rate |

### 4.2 How It Works

1. `EquippedStatsSystem` sums all equipped items' `ItemStatBlock` fields into `PlayerEquippedStats`
2. `ResourceModifierApplySystem` reads `ResourcePoolBase` (bake-time values) and `PlayerEquippedStats` (gear bonuses)
3. Effective Max = Base Max + Equipment Bonus
4. Effective Regen = Base Regen + Equipment Bonus
5. If max decreases (unequipping gear), current is clamped to the new max

**Example:** A staff with `MaxManaBonus = 50` and `ManaRegenBonus = 2` increases a mage's mana pool from 200 → 250 and regen from 8 → 10/sec.

---

## 5. UI Setup

### 5.1 Resource Bar ViewModel

Attach `ResourceBarViewModel` to your resource bar UI GameObject:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Resource Type** | ResourceType | Mana | Which resource this bar displays |
| **Low Threshold** | float | 0.2 | Percentage below which IsLow becomes true |
| **Empty Threshold** | float | 0.05 | Percentage below which IsEmpty becomes true |

The ViewModel exposes read-only properties for UI binding:
- `Current`, `Max`, `Percent` (0–1)
- `IsDraining`, `IsRecovering`, `IsLow`, `IsEmpty`
- `OnChanged` event for reactive UI updates

### 5.2 Shader Resource Bar Sync

For shader-driven resource bars (custom fill materials), attach `ShaderResourceBarSync`:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **View Model** | ResourceBarViewModel | — | Reference to the ViewModel component |
| **Bar Renderer** | Renderer | — | The Renderer with the fill material |
| **Fill Property Name** | string | `_FillAmount` | Shader property for fill percentage |
| **Color Property Name** | string | `_BarColor` | Shader property for bar color |
| **Bar Color** | Color | (0.2, 0.4, 1.0) | Default blue (mana). Change per resource type |

### 5.3 Suggested Colors

| Resource Type | Color | Hex |
|---------------|-------|-----|
| Stamina | Green | (0.3, 0.9, 0.3) |
| Mana | Blue | (0.3, 0.5, 1.0) |
| Energy | Yellow | (1.0, 0.9, 0.2) |
| Rage | Red | (1.0, 0.2, 0.2) |
| Combo | Purple | (0.7, 0.3, 1.0) |

### 5.4 UI Pipeline

The UI bridge works automatically:

1. `ResourceUIBridgeSystem` queries the local player's `ResourcePool` each frame
2. Calls `ResourceUIRegistry.Instance.UpdateBars(pool)` to broadcast values
3. All registered `IResourceBarProvider` implementations receive updated values
4. `ResourceBarViewModel` fires `OnChanged` event for reactive UI elements

---

## 6. Debug Overlay

### 6.1 Enable

Set `ResourceDebugSystem.ShowOverlay = true` in editor (static field).

### 6.2 What It Shows

Color-coded resource bars rendered above entities in Scene view (3 units above entity):
- Shows resource type name, current/max values
- Color matches resource type (see Suggested Colors above)
- Integer display for IsInteger-flagged resources, float otherwise

### 6.3 AI Workstation Integration

The **Brain Inspector** tab in the AI Workstation (DIG > AI Workstation) includes a **Resource Pool** section showing two progress bars for the selected entity's resource slots — with current/max values and resource type labels.

---

## 7. System Execution Order

```
PredictedFixedStepSimulationSystemGroup:
  ResourceTickSystem               ← Regen, decay, overflow, integer clamping

SimulationSystemGroup:
  EquippedStatsSystem              ← Sum all equipped item stat bonuses
  ResourceModifierApplySystem      ← Apply equipment bonuses to ResourcePool
  CombatResolutionSystem           ← (existing) Creates CombatResultEvents
  ResourceGenerationSystem         ← Generate resource from hits/takes via CREs
  AbilityCostDeductionSystem       ← AI ability cost deduction (OnCast/PerTick/OnComplete/OnHit)

AbilitySystemGroup (Player only):
  AbilityPrioritySystem            ← (existing) Selects pending ability
  PlayerAbilityCostSystem          ← Block cast if insufficient, deduct on cast
  AbilityLifecycleSystem           ← (existing) Executes ability

PresentationSystemGroup (Client):
  ResourceUIBridgeSystem           ← Push local player pool to UI
  ResourceDebugSystem              ← Editor overlay
```

---

## 8. After Setup: Reimport SubScene

After adding or modifying ResourcePoolAuthoring on prefabs in a SubScene:

1. Right-click the SubScene > **Reimport**
2. Wait for baking to complete

---

## 9. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Resource displays | Enter Play Mode with ResourcePool on player | Resource bar shows correct max/current |
| 3 | Regeneration | Spend resource, wait for RegenDelay | Resource begins regenerating at RegenRate |
| 4 | Ability cost check | Try to cast ability with insufficient resource | Ability is blocked |
| 5 | Ability deduction | Cast ability with sufficient resource | Resource deducted by cost amount |
| 6 | Channel drain | Use channeled weapon | Resource drains per tick, channel stops when empty |
| 7 | Rage generation | Hit enemy with GenerateOnHit rage | Rage increases by GenerateAmount |
| 8 | Rage decay | Stop fighting with DecaysWhenIdle rage | Rage decays at DecayRate after delay |
| 9 | Combo integer | Generate combo points | Counter increments by whole numbers |
| 10 | Equipment bonus | Equip item with MaxManaBonus | Max mana increases, regen updates |
| 11 | Unequip clamp | Unequip item that gave max bonus while above new max | Current clamps to new max |
| 12 | AI ability cost | AI enemy casts ability with Mana cost | AI mana decreases, ability executes |
| 13 | AI Workstation | Open DIG > AI Workstation, pick entity | Resource bars visible in Brain Inspector |
| 14 | Debug overlay | Set ResourceDebugSystem.ShowOverlay = true | Color-coded bars above entities in Scene view |

---

## 10. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Resource not regenerating | PausedRegen flag set, or RegenDelay too long | Check flags. For rage/combo, regen is intentionally paused |
| Ability still casts without resource | ResourceCostType set to None on AbilityDefinitionSO | Set to the correct ResourceType |
| Equipment bonus not applied | ResourcePoolBase missing | Ensure ResourcePoolAuthoring is on the entity (bakes both ResourcePool and ResourcePoolBase) |
| Channel doesn't stop when empty | Channel ability missing resource cost fields | Set ResourceCostType and ResourceCostAmount on the ability |
| Rage doesn't build | GenerateOnHit/GenerateOnTake flags not set, or GenerateAmount = 0 | Set flags and GenerateAmount > 0 |
| UI bar not updating | ResourceBarViewModel.ResourceType doesn't match pool slot | Set ViewModel to the correct ResourceType |
| Combo shows decimals | IsInteger flag not set | Add IsInteger to Slot flags |
| Overflow not draining | DecaysWhenFull flag missing | Add flag. Also requires CanOverflow to exceed max in the first place |
| Resource at zero on spawn | Start value set to 0 | Set Start equal to Max for full resources at spawn |
| AI Workstation shows empty resource | Entity missing ResourcePoolAuthoring | Add to enemy prefab |

---

## 11. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| AI abilities, ability execution | SETUP_GUIDE_15.32 |
| Channel weapon implementation | SETUP_GUIDE_16.5 |
| Item stat blocks, equipment | SETUP_GUIDE_16.6 |
| Codebase hygiene (channel stubs completed) | SETUP_GUIDE_16.5 |
| **Player resource framework** | **This guide (16.8)** |
