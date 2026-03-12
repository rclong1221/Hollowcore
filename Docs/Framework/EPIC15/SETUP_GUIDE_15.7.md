# EPIC 15.7 Setup Guide: Opsive Parity Features

This guide covers Unity Editor setup for the features implemented in EPIC 15.7. Follow these instructions to configure shields, channeled weapons, dual-wield setups, and melee combos.

---

## Table of Contents

1. [Shield Block System](#1-shield-block-system)
2. [Channeled Weapons](#2-channeled-weapons)
3. [Dual Wield Setup](#3-dual-wield-setup)
4. [Melee Combo Configuration](#4-melee-combo-configuration)
5. [Opsive Parity Analysis Tool](#5-opsive-parity-analysis-tool)

---

## 1. Shield Block System

Shields now provide damage reduction when blocking with right-click (Aim input).

### 1.1 Creating a Shield Prefab

1. **Create a new GameObject** for your shield
2. **Add Component:** `WeaponAuthoring`
3. **Set Weapon Type:** `Shield`

### 1.2 Shield Settings

In the `WeaponAuthoring` component, configure these Shield Settings:

| Setting | Description | Recommended Value |
|---------|-------------|-------------------|
| **Block Damage Reduction** | Percentage of damage blocked (0-1). 0.7 = 70% reduction | 0.5 - 0.8 |
| **Parry Window** | Seconds after block start where perfect parry is active (0 damage) | 0.1 - 0.2 |
| **Block Angle** | Frontal arc in degrees where blocking works | 90 - 180 |
| **Stamina Cost Per Block** | Stamina consumed per blocked hit (future feature) | 10 - 25 |

### 1.3 Equipping Shields

- Shields must be equipped in **Off-Hand Slot (Slot 1)**
- Main hand weapon goes in **Slot 0**
- Right-click (Aim) activates blocking when shield is equipped

### 1.4 Testing Shield Blocking

1. Enter Play Mode
2. Equip a shield in off-hand
3. Hold right-click to block
4. Observe in Console:
   - `[SHIELD_DEBUG] BLOCKING_START` when block begins
   - `[DAMAGE] BLOCKED` when damage is reduced
   - `[DAMAGE] PARRIED` when damage is fully negated (within parry window)

---

## 2. Channeled Weapons

Channeled weapons apply continuous effects while the fire button is held (healing beams, drain life, etc.).

### 2.1 Creating a Channeled Weapon Prefab

1. **Create a new GameObject** for your channeled weapon (staff, wand, etc.)
2. **Add Component:** `WeaponAuthoring`
3. **Set Weapon Type:** `Channel`

### 2.2 Channel Settings

In the `WeaponAuthoring` component, configure these Channel Settings:

| Setting | Description | Recommended Value |
|---------|-------------|-------------------|
| **Channel Tick Interval** | Seconds between effect applications | 0.1 - 0.5 |
| **Channel Resource Per Tick** | Mana/stamina cost per tick (future feature) | 5 - 15 |
| **Channel Effect Per Tick** | Damage (or heal) applied each tick | 5 - 20 |
| **Channel Max Time** | Maximum duration in seconds (0 = unlimited) | 0 - 10 |
| **Channel Range** | Maximum distance for the effect | 10 - 30 |
| **Channel Is Healing** | ✓ = heals target, ✗ = damages target | Toggle |
| **Channel Beam VFX Index** | Index of beam VFX prefab (for future VFX system) | 0+ |

### 2.3 Example Configurations

**Healing Staff:**
```
Tick Interval: 0.25
Effect Per Tick: 15
Max Time: 0 (unlimited)
Range: 20
Is Healing: ✓
```

**Drain Life Wand:**
```
Tick Interval: 0.2
Effect Per Tick: 10
Max Time: 5
Range: 15
Is Healing: ✗
```

**Flame Thrower:**
```
Tick Interval: 0.1
Effect Per Tick: 5
Max Time: 3
Range: 10
Is Healing: ✗
```

### 2.4 Testing Channeled Weapons

1. Enter Play Mode
2. Equip the channeled weapon
3. Hold left-click to channel
4. Observe in Console:
   - `[CHANNEL] START` when channeling begins
   - `[CHANNEL] TICK` for each effect application
   - `[CHANNEL] END` when channeling stops

---

## 3. Dual Wield Setup

Players can now use main-hand and off-hand weapons simultaneously.

### 3.1 Slot System

| Slot | Index | Input | Use Case |
|------|-------|-------|----------|
| **Main Hand** | 0 | Left-Click (Use) | Primary weapon |
| **Off Hand** | 1 | Right-Click (Aim) | Secondary weapon, shield, or torch |

### 3.2 Setting Up Dual-Wieldable Weapons

Any weapon can be placed in the off-hand slot. The weapon type determines behavior:

| Weapon Type | Off-Hand Behavior |
|-------------|-------------------|
| **Shield** | Blocks damage on right-click hold |
| **Shootable** | Fires on right-click |
| **Melee** | Attacks on right-click |
| **Channel** | Channels on right-click hold |

### 3.3 Weapon Category Configuration

To mark a weapon as dual-wieldable in the item database:

1. Open your **WeaponCategoryDefinition** asset
2. Enable **Can Dual Wield** checkbox
3. Weapons of this category can now be equipped in either hand

### 3.4 Testing Dual Wield

1. Enter Play Mode
2. Equip a weapon in main hand (slot 0)
3. Equip a weapon in off-hand (slot 1)
4. Left-click uses main hand
5. Right-click uses off-hand
6. Both can be used simultaneously

---

## 4. Melee Combo Configuration

Melee weapons support combo chains with per-step configuration. This section covers both the **data configuration** and the **animation event hookup**.

### 4.1 Understanding the Combo System Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         COMBO SYSTEM FLOW                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Animation Clip ──► Animation Event ──► WeaponAnimationEventRelay      │
│        │                   │                      │                     │
│        │                   │                      ▼                     │
│        │                   │            WeaponAnimationEvents (Queue)   │
│        │                   │                      │                     │
│        │                   │                      ▼                     │
│        │                   │          WeaponAnimationEventSystem (ECS)  │
│        │                   │                      │                     │
│        │                   │                      ▼                     │
│        │                   └───────────► MeleeState / MeleeAction       │
│        │                                                                │
│        ▼                                                                │
│   Animator Controller ◄─── AnimatorBridge ◄─── MeleeActionSystem       │
│   (SubState Index)              │                                       │
│                                 │                                       │
│   ComboData Buffer ◄────────────┴───── Per-step animation mapping       │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Animation Event Names

The engine recognizes these animation events for melee. Add these to your animation clips:

| Event Name | When to Place | What It Does |
|------------|---------------|--------------|
| `OnAnimatorMeleeStart` | First frame of attack | Signals attack has begun |
| `OnAnimatorMeleeHitFrame` | When blade hits (0.2-0.4 normalized) | Activates hitbox for damage |
| `OnAnimatorMeleeComplete` | Last frame of attack | Signals attack can end/transition |
| `OnAnimatorMeleeCombo` | In combo window (0.5-0.8 normalized) | Opens window for combo input |
| `OnAnimatorActiveAttackStart` | Opsive-style: when attack is "active" | Alternative hitbox activation |
| `OnAnimatorAllowChainAttack` | Opsive-style: when chaining allowed | Alternative combo window |

**Alternate Event Names:** The relay also accepts shortened versions:
- `MeleeStart`, `MeleeHit`, `HitFrame`, `MeleeComplete`, `Combo`

### 4.3 Setting Up Animation Events (Unity Animator)

1. **Select your animation clip** (e.g., `Sword_Attack_01`)
2. **Open Animation window** (Window > Animation > Animation)
3. **Expand the Events section** in the clip inspector
4. **Add events at key frames:**

```
Timeline: |----0%----20%----40%----60%----80%----100%----|
Events:   [Start]   [HitFrame]       [Combo]   [Complete]
                         ▲              ▲
                    Hitbox ON      Combo window
```

**Example: 3-Hit Katana Combo**

| Clip Name | Start | HitFrame | Combo Window | Complete |
|-----------|-------|----------|--------------|----------|
| `Katana_Combo_01` | 0.0 | 0.25 | 0.5-0.8 | 1.0 |
| `Katana_Combo_02` | 0.0 | 0.20 | 0.4-0.75 | 1.0 |
| `Katana_Combo_03` | 0.0 | 0.35 | N/A (final) | 1.0 |

### 4.4 Adding Events to Animation Clips

#### Method 1: Animation Workstation (Fastest) ⭐

1. Open: **Window > DIG > Animation Workstation**
2. Select the **Events** tab
3. Select animation clip(s) in Project window
4. Click **Refresh Selection from Project**
5. Choose preset from dropdown:
   - **MeleeBasic** - HitFrame + Start/Complete
   - **MeleeCombo** - HitFrame + Combo window + Start/Complete
   - **ShieldBlock** - BlockStart/End
   - **ShieldParry** - Block + Parry window
   - **BowShot** - Draw + Nock + Release
   - **Throw** - ChargeStart + Release + Complete
   - **Fire/Reload/MagazineReload** - Shootable presets
6. Adjust frame positions if needed (% slider or manual frame)
7. Click **Apply Events to Selected Clips**

> 💡 **Tip:** Can apply to multiple clips at once for batch setup!

#### Method 2: Animation Window (Manual)
1. Select animation clip in Project
2. Open Animation window
3. Scrub to desired frame
4. Click **Add Event** (diamond icon)
5. In Inspector, set Function to `ExecuteEvent`
6. Set String parameter to event name (e.g., `OnAnimatorMeleeHitFrame`)

#### Method 3: Animation Inspector
1. Select animation clip
2. In Inspector, expand **Events**
3. Click **+** to add event
4. Set Time (normalized 0-1)
5. Set Function: `ExecuteEvent`
6. Set String: event name

### 4.5 WeaponAnimationEventRelay Setup

The relay component must be on the **same GameObject as the Animator**:

1. **Select your character model** (the one with the Animator)
2. **Add Component:** `WeaponAnimationEventRelay`
3. **Enable Debug Logging** (optional, for testing)

```
Character Root
└── Character Model (Animator + WeaponAnimationEventRelay) ◄── HERE
    └── Armature
        └── Bones...
```

### 4.6 Animator Controller Setup for Combos

The Animator Controller must have substates for each combo step:

```
Base Layer
└── Item
    └── Attack (BlendTree or SubStateMachine)
        ├── Attack_0 (first hit)   ◄── SubState Index 0
        ├── Attack_1 (second hit)  ◄── SubState Index 1
        └── Attack_2 (third hit)   ◄── SubState Index 2
```

**Setting SubState Index:**
1. Select each attack state
2. Add **State Machine Behaviour** or use Opsive's built-in ItemSubstate
3. Or use animation event to signal which combo step is playing

### 4.7 Creating a WeaponConfig Asset

1. **Right-click in Project:** `Create > DIG > Items > Weapon Config`
2. Name it (e.g., `Katana_Config`)

### 4.8 Configuring Combo Chain

In the WeaponConfig inspector, expand **Combo Chain** and add entries:

| Field | Description | How Engine Uses It |
|-------|-------------|-------------------|
| **Animator Sub State Index** | Which animation plays (0, 1, 2...) | Sets `ItemSubstate` animator param |
| **Duration** | Length of this attack in seconds | Determines when attack ends |
| **Input Window Start** | Normalized time (0-1) when combo input window opens | When `QueuedAttack` can be set |
| **Input Window End** | Normalized time (0-1) when combo input window closes | When queue window ends |
| **Damage Multiplier** | Damage multiplier for this step (1.0 = base damage) | Applied to `MeleeDamage` |
| **Knockback Force** | Force applied to hit targets | Applied on hit |

### 4.9 How the Engine Knows Which Animation to Play

```
1. Player presses Attack
   └─► MeleeActionSystem sets CurrentCombo = 0
       └─► AnimatorBridge reads CurrentCombo
           └─► Sets "ItemSubstate" parameter = ComboData[0].AnimatorSubStateIndex
               └─► Animator transitions to Attack_0 state
                   └─► Animation plays, fires events
                       └─► WeaponAnimationEventRelay receives "HitFrame"
                           └─► MeleeActionSystem activates hitbox

2. Player presses Attack again (during combo window)
   └─► QueuedAttack = true
       └─► When Attack_0 completes, CurrentCombo = 1
           └─► AnimatorBridge sets ItemSubstate = ComboData[1].AnimatorSubStateIndex
               └─► Animator transitions to Attack_1
                   └─► Process repeats...
```

### 4.10 Example: Complete 3-Hit Katana Combo Setup

**Step 1: WeaponConfig Asset**
| Step | SubState | Duration | Window Start | Window End | Damage | Knockback |
|------|----------|----------|--------------|------------|--------|-----------|
| 1 | 0 | 0.4 | 0.5 | 0.9 | 1.0 | 5 |
| 2 | 1 | 0.35 | 0.4 | 0.85 | 1.2 | 7 |
| 3 | 2 | 0.6 | 0.6 | 0.95 | 1.8 | 15 |

**Step 2: Animation Clips**
```
Katana_Combo_01.anim
├── Event @ 0.00: ExecuteEvent("OnAnimatorMeleeStart")
├── Event @ 0.25: ExecuteEvent("OnAnimatorMeleeHitFrame")  
├── Event @ 0.50: ExecuteEvent("OnAnimatorMeleeCombo")
└── Event @ 0.95: ExecuteEvent("OnAnimatorMeleeComplete")

Katana_Combo_02.anim
├── Event @ 0.00: ExecuteEvent("OnAnimatorMeleeStart")
├── Event @ 0.20: ExecuteEvent("OnAnimatorMeleeHitFrame")
├── Event @ 0.40: ExecuteEvent("OnAnimatorMeleeCombo")
└── Event @ 0.90: ExecuteEvent("OnAnimatorMeleeComplete")

Katana_Combo_03.anim
├── Event @ 0.00: ExecuteEvent("OnAnimatorMeleeStart")
├── Event @ 0.35: ExecuteEvent("OnAnimatorMeleeHitFrame")
└── Event @ 0.95: ExecuteEvent("OnAnimatorMeleeComplete")
```

**Step 3: Animator Controller**
```
Parameters:
- ItemSubstate (Int)

States:
- Katana_Combo_01 (plays when ItemSubstate == 0)
- Katana_Combo_02 (plays when ItemSubstate == 1)  
- Katana_Combo_03 (plays when ItemSubstate == 2)
```

**Step 4: WeaponAuthoring**
```
Type: Melee
Config: Katana_Config (assigned)
Melee Damage: 50
Attack Speed: 2
Hitbox Active Start: 0.2
Hitbox Active End: 0.5
```

### 4.11 Assigning Config to Weapon

1. Select your melee weapon prefab
2. In `WeaponAuthoring`, find **Configuration (Data-Driven)**
3. Assign your `WeaponConfig` asset to the **Config** field

### 4.12 Melee Authoring Settings

Also configure these base melee settings in `WeaponAuthoring`:

| Setting | Description |
|---------|-------------|
| **Melee Damage** | Base damage (multiplied by combo step) |
| **Melee Range** | Attack reach distance |
| **Attack Speed** | Default attacks per second (overridden by combo Duration) |
| **Hitbox Active Start** | Default normalized time hitbox activates |
| **Hitbox Active End** | Default normalized time hitbox deactivates |
| **Combo Count** | Max combo steps (fallback if no Config) |
| **Combo Window** | Seconds allowed between attacks to continue combo |
| **Hitbox Offset** | Position offset from weapon origin |
| **Hitbox Size** | Dimensions of the attack hitbox |

### 4.6 Testing Combos

1. Enter Play Mode
2. Equip melee weapon with combo config
3. Click to attack → watch for combo step in console
4. Click again within combo window → advances to next step
5. Wait too long → combo resets to step 0

Console output:
- `[ATTACK_REPLICATION] MELEE_ATTACK_START Combo=0` (first hit)
- `[ATTACK_REPLICATION] MELEE_ATTACK_START Combo=1` (second hit)
- `[ATTACK_REPLICATION] MELEE_ATTACK_START Combo=2` (third hit)

### 4.14 Configurable Combo System (Advanced)

The combo system supports different input modes for different game styles.

#### Creating a Combo System Config

1. **Right-click in Project:** `Create > DIG > Weapons > Combo System Config`
2. **Apply a preset** or configure manually

#### Input Modes

| Mode | Behavior | Best For |
|------|----------|----------|
| **InputPerSwing** | Each attack requires a new button press | Souls-like, Monster Hunter |
| **HoldToCombo** | Hold button to auto-advance chain | Devil May Cry, Bayonetta |
| **RhythmBased** | Timed inputs with bonus for perfect timing | Batman Arkham, Spider-Man |

#### Cancel Settings

| Setting | Options | Description |
|---------|---------|-------------|
| **Cancel Policy** | None / RecoveryOnly / Anytime | When attacks can be interrupted |
| **Cancel Priority** | Dodge, Jump, Block, Ability, Movement | What actions can cancel attacks |
| **Queue Depth** | 0-5 | How many attacks can be buffered ahead |

#### Quick Presets

Open **DIG > Combat Workstation** and select the **Combo System** tab:

| Preset | Settings |
|--------|----------|
| **Souls-like** | InputPerSwing, Queue=1, RecoveryOnly cancel, Dodge priority |
| **Character Action** | HoldToCombo, Queue=3, Anytime cancel, Dodge+Jump+Ability |
| **Brawler** | RhythmBased, Queue=2, Anytime cancel, All priorities |

#### Per-Weapon Override

To make a specific weapon behave differently:

1. Select weapon prefab
2. In `WeaponAuthoring`, find **Combo System Override**
3. **Uncheck** "Use Global Combo Config"
4. Configure override settings for this weapon only

---

## 5. Opsive Parity Analysis Tool

A utility window for comparing ECS implementations against Opsive features.

### 5.1 Opening the Tool

**Menu:** `DIG > Opsive Parity Analysis`

### 5.2 Features

| Tab | Purpose |
|-----|---------|
| **Locomotion** | Compare gravity zones, moving platforms, etc. |
| **Interaction** | Compare weapons, abilities, item actions |
| **Clean-Up** | Audit for conflicting Opsive components |

### 5.3 Using the Tool

1. Open via menu
2. Click **Refresh Analysis** to scan codebase
3. Review status indicators:
   - ✅ **COMPLETE** - Feature fully implemented
   - ⚠️ **PARTIAL** - Some aspects missing
   - ❌ **NOT IMPLEMENTED** - Feature not yet built
4. Click **Export Report** to generate markdown summary

---

## Quick Reference

### Input Mapping

| Action | Input | System |
|--------|-------|--------|
| Fire/Attack (Main Hand) | Left-Click | `PlayerToItemInputSystem` |
| Block/Fire (Off Hand) | Right-Click | `OffHandToShieldInputSystem` |
| Reload | R | `PlayerToItemInputSystem` |

### Weapon Types

| Type | Authoring Enum | Primary Use |
|------|----------------|-------------|
| Shootable | `WeaponType.Shootable` | Guns, bows |
| Melee | `WeaponType.Melee` | Swords, axes |
| Shield | `WeaponType.Shield` | Blocking |
| Channel | `WeaponType.Channel` | Beams, sustained magic |
| Throwable | `WeaponType.Throwable` | Grenades |
| Bow | `WeaponType.Bow` | Charge-and-release |

### Slot Indices

| Slot Name | Index | Description |
|-----------|-------|-------------|
| Main Hand | 0 | Primary weapon slot |
| Off Hand | 1 | Secondary/shield slot |

---

## Troubleshooting

### Shield Not Blocking

1. Verify weapon type is `Shield`
2. Ensure shield is in **Slot 1** (off-hand)
3. Check `Block Angle` is not 0
4. Verify `PlayerBlockingState` is on player prefab

### Channel Not Ticking

1. Verify weapon type is `Channel`
2. Check `Tick Interval` is > 0
3. Ensure weapon has `UseRequest` component (added by baker)

### Combos Not Chaining

1. Verify `WeaponConfig` is assigned
2. Check `Combo Chain` has multiple entries
3. Ensure `Combo Window` is long enough (0.3-0.5s recommended)
4. Input must occur during attack for queuing

### Animation Events Not Firing

1. Verify `WeaponAnimationEventRelay` is on the **same GameObject as the Animator**
2. Check animation clip has events (select clip → Inspector → Events)
3. Ensure event Function is `ExecuteEvent` with String parameter
4. Enable Debug Logging on the relay to see received events
5. Verify animation clip is actually playing (check Animator window)

### Wrong Animation Playing for Combo Step

1. Check `AnimatorSubStateIndex` in ComboData matches Animator state
2. Verify Animator has transitions based on `ItemSubstate` parameter
3. Check AnimatorBridge is setting parameters correctly
4. Enable debug logging: `[OpsiveAnimatorBridge]` messages in console

### Hitbox Not Activating

1. Check `Hitbox Active Start/End` values in WeaponAuthoring
2. Verify animation fires `OnAnimatorMeleeHitFrame` event
3. Check hitbox size is not zero
4. Look for `[MELEE_HITBOX]` debug messages

### Combo Resets Too Fast

1. Increase `Combo Window` in WeaponAuthoring (try 0.5-1.0s)
2. Check `Input Window End` in ComboData isn't too early
3. Verify player is pressing attack before window closes

### Animation Events Naming Issues

The relay accepts multiple naming conventions:
- Opsive-style: `OnAnimatorMeleeHitFrame`
- Short form: `MeleeHit` or `HitFrame`
- Custom: `ExecuteEvent("YourEventName")` → add case to relay

### Off-Hand Not Firing

1. Ensure weapon is in **Slot 1**
2. Verify weapon has one of: `ShieldAction`, `WeaponFireComponent`, `MeleeAction`, or `ChannelAction`
3. Check right-click input is working (`PlayerInputState.Aim`)

---

## All Animation Event Names Reference

> **Important:** These are events the relay **recognizes**—you only add the ones your animation needs. Required events are marked with ⚠️.

### Melee Events
| Event | Purpose | Required? |
|-------|---------|-----------|
| `OnAnimatorMeleeHitFrame` / `MeleeHit` / `HitFrame` | Hitbox activates, deals damage | ⚠️ **Yes** |
| `OnAnimatorMeleeStart` / `MeleeStart` | Attack begins (for sounds/VFX) | Optional |
| `OnAnimatorMeleeComplete` / `MeleeComplete` | Attack ends (cleanup) | Optional |
| `OnAnimatorMeleeCombo` / `Combo` | Combo window opens | Optional (code uses timing if missing) |

### Shootable Events
| Event | Purpose | Required? |
|-------|---------|-----------|
| `OnAnimatorItemFire` / `Fire` | Projectile spawns | ⚠️ **Yes** |
| `OnAnimatorItemFireComplete` / `FireComplete` | Fire animation ends | Optional |
| `OnAnimatorReloadStart` / `ReloadStart` | Reload begins | Optional (if weapon has ammo) |
| `OnAnimatorReloadComplete` / `ReloadComplete` | Reload finishes, ammo restored | Optional (if weapon has ammo) |
| `OnAnimatorShellEject` / `ShellEject` | Shell casing spawns | Optional (polish) |
| `OnAnimatorBoltPull` / `BoltPull` / `SlidePull` | Bolt/slide sound | Optional (polish) |
| `DetachClip` / `DropClip` / `AttachClip` | Magazine reload visuals | Optional (magazine weapons) |

### Shield Events
| Event | Purpose | Required? |
|-------|---------|-----------|
| `OnAnimatorBlockStart` / `BlockStart` | Block begins | Optional (code uses input) |
| `OnAnimatorBlockEnd` / `BlockEnd` | Block ends | Optional |
| `OnAnimatorParryWindow` / `ParryWindow` | Parry window opens | Optional (if parry system used) |
| `OnAnimatorParryComplete` / `ParryComplete` | Parry window closes | Optional |
| `OnAnimatorBlockImpact` / `BlockImpact` / `ShieldHit` | Hit while blocking (stagger/VFX) | Optional |
| `OnAnimatorParrySuccess` / `ParrySuccess` | Successful parry (riposte trigger) | Optional |

### Bow Events
| Event | Purpose | Required? |
|-------|---------|-----------|
| `OnAnimatorBowRelease` / `BowRelease` / `BowFire` | Arrow releases | ⚠️ **Yes** |
| `OnAnimatorBowDraw` / `BowDraw` / `DrawBow` | Draw begins (for tension) | Optional |
| `OnAnimatorArrowNock` / `ArrowNock` / `NockArrow` | Arrow nocked (sound) | Optional |
| `OnAnimatorBowCancel` / `BowCancel` | Draw cancelled | Optional |

### Throwable Events
| Event | Purpose | Required? |
|-------|---------|-----------|
| `OnAnimatorThrowRelease` / `ThrowRelease` / `Release` | Object thrown | ⚠️ **Yes** |
| `OnAnimatorThrowChargeStart` / `ChargeStart` | Charge begins | Optional |
| `OnAnimatorThrowComplete` / `ThrowComplete` | Animation ends | Optional |

### Equip/Unequip Events
| Event | Purpose | Required? |
|-------|---------|-----------|
| `OnAnimatorEquipComplete` / `EquipComplete` | Weapon ready to use | Optional (code uses timing) |
| `OnAnimatorEquipStart` / `EquipStart` | Equip begins | Optional |
| `OnAnimatorUnequipStart` / `UnequipStart` | Unequip begins | Optional |
| `OnAnimatorUnequipComplete` / `UnequipComplete` | Weapon holstered | Optional |

### Minimal Setup Examples

**Simple Sword (1 event):**
- `HitFrame` at frame 12 → That's it!

**Pistol with Reload (3 events):**
- `Fire` at frame 5
- `ReloadStart` at frame 0 of reload clip
- `ReloadComplete` at frame 30 of reload clip

**Shield (0 events):**
- Block is input-driven, no events needed
- Add `ParryWindow` only if implementing parry mechanic

---

*Last Updated: EPIC 15.7 - January 2026*
