# EPIC 1.4 Setup Guide: Enemy Limb Rip System

**Status:** Planned
**Requires:** EPIC 1.1 (ChassisState, LimbInstance), EPIC 1.3 (LimbPickup, LimbEquipRequest), Framework Combat/ (stagger system), Framework AI/ (enemy state machine), Framework Interaction/ system

---

## Overview

The Limb Rip system is the "Cyborg Justice" mechanic: players can rip a limb off a staggered or downed enemy during a limited time window. The rip is a long, interruptible channeled action that leaves the player fully exposed to damage. Ripped limb quality depends on enemy tier -- common enemies yield temporary limbs (30-60 seconds), elites yield district-life limbs, and bosses yield permanent legendary limbs. Some ripped limbs carry curses with negative effects.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| Player Prefab (Subscene) | `ChassisAuthoring` (EPIC 1.1) | Chassis system for equipping ripped limbs |
| Player Prefab (Subscene) | `RipInProgress` (baked disabled) | Tracks active rip channel state |
| Enemy Prefabs (Subscene) | `RippableLimb` + `RipTarget` (baked disabled) | Marks enemy as rip-eligible |
| Framework | Combat/ stagger system | Triggers the rip window on enemy stagger |
| Framework | Interaction/ system | Rip prompt when near valid target |
| Data | LimbDefinitionSO assets (EPIC 1.1) | Defines what limb the enemy yields |

### New Setup Required

1. Add `RippableLimb` + `RipTarget` to rip-eligible enemy prefabs
2. Add `RipInProgress` (baked disabled) to player prefab
3. Create `RipRuntimeConfig` singleton asset for global rip tuning
4. Create `LimbCurseDefinitionSO` assets for cursed limb variants
5. Create rip animations (start, progress loop, success, cancel)
6. Create rip VFX prefabs (sparks, hydraulic fluid, limb separation)
7. Add rip input binding to player input configuration

---

## 1. Enemy Prefab Setup -- RippableLimb

**Add Component:** `RippableLimbAuthoring` on rip-eligible enemy prefab root
**Not all enemies are rippable** -- only humanoid/mechanical with distinct limbs (GDD: "Slag Walkers yes, Adware Swarms no").

### 1.1 RippableLimb Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **LimbDefinitionId** | ID referencing the `LimbDefinitionSO` yielded on rip | (required) | Must match a valid asset |
| **SlotType** | Which `ChassisSlot` the ripped limb fits | LeftArm | Enum: Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg |
| **ResultDurability** | How long the ripped limb lasts | Temporary | Enum: Temporary, DistrictLife, Permanent |
| **TemporaryDuration** | Seconds the limb lasts (Temporary only) | 45 | 30-60 |
| **CanBeCursed** | Whether this limb can roll a curse on rip | false | bool |
| **CurseDefinitionId** | Specific curse to apply (0 = random from pool) | 0 | Valid curse ID or 0 |

### 1.2 Durability by Enemy Tier

| Enemy Tier | ResultDurability | TemporaryDuration | Rarity |
|------------|-----------------|-------------------|--------|
| Common | Temporary | 30-60s | Junk-Common |
| Elite | DistrictLife | N/A (lasts until district exit) | Uncommon-Rare |
| Boss / Mini-boss | Permanent | N/A (never expires) | Epic-Legendary |

**Tuning tip:** Common enemy temporary limbs are "combat power-ups" -- short bursts of power. Set TemporaryDuration to 45s as baseline. If players report they never use temporary limbs, increase to 60s. If temporary limbs feel too safe, decrease to 30s.

### 1.3 RipTarget (IEnableableComponent)

`RipTarget` is baked disabled on the enemy prefab. The `RipWindowSystem` enables it when the enemy enters stagger state and disables it when the window expires.

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **WindowRemaining** | Set by RipWindowSystem at runtime | 0 | 0-8 (seconds) |

No designer configuration needed -- this is controlled entirely by `RipRuntimeConfig`.

---

## 2. Player Prefab Setup

### 2.1 Add RipInProgress

1. Open your player prefab in the subscene
2. Add Component: `RipInProgressAuthoring`
3. Baker creates `RipInProgress` as an IEnableableComponent, baked disabled

`RipInProgress` is 20 bytes (Entity + 2 floats) and is an enableable component, so it adds no archetype overhead when disabled. Safe for the player entity.

### 2.2 Rip Input Binding

Add to player input configuration (InputActionAsset or PlayerInputState):

| Input | Binding | Context |
|-------|---------|---------|
| **RipInput** | Same as interact key (context-sensitive) | Only active when near RipTarget-enabled enemy |

The `RipExecutionSystem` checks for interact input + proximity to a RipTarget-enabled enemy to differentiate rip from normal interaction.

---

## 3. RipRuntimeConfig Singleton

**Create:** `Assets > Create > Hollowcore/Chassis/RipRuntimeConfig`
**Recommended location:** `Assets/Data/Chassis/RipRuntimeConfig.asset`

Place a GameObject with `RipRuntimeConfigAuthoring` in your global config subscene.

### 3.1 Rip Window Timing

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **RipWindowCommon** | Stagger window for common enemies (seconds) | 4.0 | 2-8 |
| **RipWindowElite** | Stagger window for elite enemies (seconds) | 3.0 | 2-6 |
| **RipWindowBoss** | Stagger window for boss enemies (seconds) | 5.0 | 3-8 |

### 3.2 Rip Execution Timing

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **RipTimeCommon** | Channel time to rip a common enemy (seconds) | 2.0 | 1-5 |
| **RipTimeElite** | Channel time to rip an elite enemy (seconds) | 3.0 | 2-5 |
| **RipTimeBoss** | Channel time to rip a boss enemy (seconds) | 4.0 | 3-6 |
| **RipInteractRange** | Max distance to initiate rip (meters) | 2.5 | 1.5-4.0 |

### 3.3 Temporary Limb & Curse

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **TempLimbDurationMult** | Multiplier on TemporaryDuration | 1.0 | 0.5-3.0 |
| **CurseChance** | Probability a curse-eligible limb actually has a curse | 0.3 | 0.0-1.0 |

**Tuning tip:** RipTimeCommon at 2.0s with RipWindowCommon at 4.0s gives a 70% success rate in Monte Carlo simulations with 2 nearby enemies attacking every 1.5s. If success rate feels too low, reduce RipTimeCommon to 1.5s rather than extending the window.

---

## 4. Curse Definitions

**Create:** `Assets > Create > Hollowcore/Chassis/CurseDefinition`
**Recommended location:** `Assets/Data/Chassis/Curses/`
**Naming convention:** `Curse_[Name].asset` -- e.g., `Curse_FeedbackLoop.asset`

### 4.1 LimbCurseDefinitionSO Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **CurseDefinitionId** | Unique identifier | (required) | Must be globally unique |
| **DisplayName** | Curse name shown in UI | (required) | Max 32 chars |
| **Description** | Flavor text explaining the curse effect | (required) | Max 128 chars |
| **Duration** | How long the curse lasts (-1 = until limb removed) | -1 | -1 or 10-300 |
| **CurseType** | Category: DOT, Debuff, ZoneRestriction, Contagion | DOT | Enum |
| **EffectMagnitude** | Strength of the curse (DPS, stat reduction %, etc.) | 5.0 | 1-50 |

### 4.2 Example Curses

| Curse | CurseType | EffectMagnitude | Duration | Source Enemy |
|-------|-----------|-----------------|----------|--------------|
| Feedback Loop | DOT (nearby ally damage) | 3 DPS | -1 (permanent) | Feedback Loop enemies |
| Plague Strain | Contagion (contamination meter) | 2% per second | 120s | Patient Zero's Children |
| Faith-Lock | ZoneRestriction (non-Cathedral zones) | N/A | -1 (permanent) | Chrome Cathedral converts |
| Neural Rot | Debuff (stat reduction) | 10% all stats | 60s | Nursery pattern learners |

**Tuning tip:** Curses should feel like a meaningful tradeoff, not a punishment. The strongest ripped limbs should have the highest curse chance. Players should sometimes choose to equip a cursed limb because the stats are worth it.

---

## 5. Animation & VFX Setup

### 5.1 Animation Requirements

Create these animation clips and add to the player's Animator Controller:

| Animation | Duration | Layer | Notes |
|-----------|----------|-------|-------|
| `Rip_Start` | 0.5s | UpperBody | Player grabs enemy limb |
| `Rip_Loop` | Looping | UpperBody | Pulling motion, plays during channel |
| `Rip_Success` | 1.0s | FullBody | Triumphant tear, limb removed |
| `Rip_Cancel` | 0.3s | UpperBody | Stagger back on interrupt |

### 5.2 VFX Prefabs

**Recommended location:** `Assets/Prefabs/VFX/Chassis/`

| VFX Prefab | When | Duration |
|------------|------|----------|
| `VFX_Rip_Sparks` | During Rip_Loop channel | Looping |
| `VFX_Rip_HydraulicFluid` | On Rip_Success | 2s burst |
| `VFX_Rip_LimbSeparation` | On Rip_Success | 1.5s burst |
| `VFX_Rip_CurseAura` | When cursed limb spawns | 3s |

All VFX should go through the framework VFX pipeline (VFXRequest entities).

---

## 6. Rip Progress UI

**Create:** UI prefab in `Assets/Prefabs/UI/Chassis/RipProgressBar.prefab`

| Element | Description |
|---------|-------------|
| **Progress Bar** | Fills from 0% to 100% during channel |
| **"EXPOSED" Warning** | Red text flashing during channel |
| **Rip Prompt** | "Hold [Interact] to Rip" when near valid target |
| **Curse Warning** | Skull icon if limb CanBeCursed (shown before initiation) |
| **Limb Preview** | Small tooltip showing ripped limb stats on prompt |

---

## Scene & Subscene Checklist

| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Player Subscene | `RipInProgressAuthoring` on player root | Baked disabled, 20 bytes |
| Enemy Subscenes | `RippableLimbAuthoring` + `RipTargetAuthoring` on eligible enemies | Not all enemies -- humanoid/mechanical only |
| Global Config Subscene | `RipRuntimeConfigAuthoring` on config GO | Singleton for rip tuning |
| Bootstrap Scene | Nothing | Systems auto-register |
| UI Canvas | Rip progress bar + rip prompt prefab | Reads RipInProgress state |

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Adding RippableLimb to non-humanoid enemies (swarms, blobs) | Rip animation looks absurd, no limb socket | Only add to humanoid/mechanical enemies per GDD |
| RipTimeCommon longer than RipWindowCommon | Impossible to complete rip before window closes | Ensure RipTime < RipWindow for each tier |
| CurseDefinitionId = 0 on a CanBeCursed enemy with no curse pool | Null reference on rip success | Set CurseDefinitionId to a valid ID or ensure curse pool is populated |
| Missing RipInProgress on player prefab | Player can never initiate rip, no error logged | Add `RipInProgressAuthoring` to player, reimport subscene |
| Forgetting to reimport subscene after adding RippableLimb | RipTarget never enables, enemy cannot be ripped | Right-click subscene, Reimport |
| RipInteractRange too large (>4m) | Player initiates rip from too far, animation clips through enemy | Keep at 2.5m default, max 4.0m |
| TemporaryDuration set to 0 | Ripped limb expires instantly on equip | Minimum 30s; OnValidate should clamp |

---

## Verification

1. **Stagger Window** -- Stagger a rippable enemy. Console should show:
   ```
   [RipWindowSystem] Enemy E:42 STAGGERED, RipTarget ENABLED (window=4.0s)
   ```
2. **Window Expiry** -- Wait without ripping. Console:
   ```
   [RipWindowSystem] Enemy E:42 RipTarget EXPIRED (window=0.0s)
   ```
3. **Rip Initiation** -- Press interact near staggered enemy. Console:
   ```
   [RipExecutionSystem] Player E:1 RIP START on Enemy E:42 (requiredTime=2.0s)
   ```
4. **Rip Progress** -- Progress bar should fill over RequiredTime seconds.
5. **Rip Interruption** -- Take damage during rip. Console:
   ```
   [RipExecutionSystem] Player E:1 RIP INTERRUPTED (took 15.0 damage)
   ```
6. **Rip Success** -- Complete the channel. Console:
   ```
   [RipExecutionSystem] Player E:1 RIP SUCCESS on Enemy E:42 -> LimbPickup spawned (BurnArm, Temporary 45s)
   ```
7. **Enemy Death** -- Rip success should kill the target enemy.
8. **Temporary Limb Expiry** -- Equip a Temporary ripped limb. After TemporaryDuration seconds, it should auto-unequip and be destroyed.
9. **Cursed Limb** -- Rip from a CanBeCursed enemy. Stats comparison UI should show curse warning with description before equip confirmation.
10. **Debug Overlay** -- Toggle `chassis.rip` in console. Green circles around rippable staggered enemies, yellow around rippable but not staggered, progress bar during channel.
