# Setup Guide 15.16: Target Locking System

## Overview

The Target Locking System allows players to lock onto enemies using the **Tab** key (or Grab input). When locked, the camera orbits behind the player facing the target, and movement becomes strafing relative to the target. This guide covers how to set up enemies as lock-on targets and configure the system.

### Lock Mode Status

| Mode | Status | Description |
|------|--------|-------------|
| **Hard Lock** | ✅ Working | Dark Souls style - camera forced to target, strafe movement |
| **Soft Lock** | ✅ Working | God of War style - camera tracks until mouse moves |
| **Over-the-Shoulder** | ✅ Working | RE4/Gears style - shoulder offset + ADS zoom |
| **Isometric Lock** | 🔒 Deferred | Requires isometric camera mode |
| **Twin-Stick** | 🔒 Deferred | Requires isometric camera + aim visualization |
| **First Person** | 🔒 Deferred | Requires FPS camera mode |

---

## Part 1: Making Enemies Targetable

### Adding Lock-On Target to Enemy Prefabs

1. Open your enemy prefab in the Project window
2. Select the root GameObject
3. **Add Component** → search for **"Lock-On Target"** (or navigate to DIG/Targeting/Lock-On Target)
4. Configure the settings:

| Field | Description | Recommended Values |
|-------|-------------|-------------------|
| **Priority** | Higher = preferred when multiple targets available | 0 = Normal, 10 = Elite, 100 = Boss |
| **Indicator Height Offset** | Meters above origin for the lock indicator | 1.5 for humanoids, 2.5+ for large enemies |
| **Start Enabled** | Whether this target can be locked immediately | ✅ Usually checked |

### Priority Guidelines

| Enemy Type | Priority Value |
|------------|---------------|
| Normal enemies | 0 |
| Elite/Mini-boss | 10 |
| Boss | 100 |
| Story-critical NPC | 50 |
| Destructible object | -10 (low priority) |

> **Note:** When multiple targets are in range, the player will lock onto the one with the highest priority that's closest to their crosshair direction.

---

## Part 2: Testing Target Lock Settings

### Adding the Debug Tester

1. In your test scene, right-click in Hierarchy → **Create Empty**
2. Name it `TargetLockTester`
3. **Add Component** → **DIG/Debug/Target Lock Tester**

### TargetLockTester Inspector Reference

| Field | Description |
|-------|-------------|
| **Allow Target Lock** | Master toggle - enables/disables Tab lock-on |
| **Allow Aim Assist** | Toggle for soft-targeting (future feature) |
| **Show Indicator** | Toggle lock-on UI reticle visibility |
| **Preset** | Quick presets: Normal, Hardcore, LockOnOnly, AimAssistOnly |
| **Is Currently Locked** | *(Read-only)* Shows if player is locked on |
| **Locked Target Name** | *(Read-only)* Shows current target entity |

### Quick Presets

| Preset | Target Lock | Aim Assist | Indicator |
|--------|-------------|------------|-----------|
| **Normal** | ✅ On | ✅ On | ✅ On |
| **Hardcore** | ❌ Off | ❌ Off | ❌ Off |
| **Lock On Only** | ✅ On | ❌ Off | ✅ On |
| **Aim Assist Only** | ❌ Off | ✅ On | ✅ On |

### Context Menu Actions

Right-click on the TargetLockTester component header for:
- **Force Unlock All Targets** - Immediately breaks current lock
- **Reset to Defaults** - Applies Normal preset

---

## Part 2b: Targeting Mode Tester

For more advanced testing of lock-on modes and behaviors, use the **Targeting Mode Tester**.

### Adding the Mode Tester

1. In your test scene, right-click in Hierarchy → **Create Empty**
2. Name it `TargetingModeTester`
3. **Add Component** → **DIG/Targeting/Targeting Mode Tester**

### Key Fields

| Field | Description |
|-------|-------------|
| **Current Mode** | Lock behavior: HardLock, SoftLock, OverTheShoulder, etc. |
| **Input Mode** | Toggle, Hold, AutoNearest, ClickTarget, HoverTarget |
| **Input Handler** | Which system handles lock input (CameraLockOnSystem or LockInputModeSystem) |
| **Max Lock Range** | Maximum distance to acquire lock (meters) |
| **Max Lock Angle** | Maximum angle from crosshair to acquire lock (degrees) |

### Context Menu Presets

Right-click the component header for quick presets:
- **Set Hard Lock Mode** - Dark Souls style (camera forced to target, strafe movement)
- **Set Soft Lock Mode** - God of War style (camera tracks until mouse moves)
- **Set Over-the-Shoulder Mode** - RE4/Gears style (shoulder offset, ADS zoom, auto-swap)
- **Set First Person Mode** - FPS style (aim assist only) *(Deferred)*

---

## Part 3: Health Bar Integration (WhenTargeted Mode)

The target lock system integrates with the Health Bar Visibility System (EPIC 15.14). When using **WhenTargeted** or **WhenTargetedOrDamaged** visibility modes, only the locked-on enemy's health bar will appear.

### Setting Up WhenTargeted Health Bars

1. Find or create **HealthBarVisibilityTester** in your scene (or use the one on GameManager)
2. In Inspector, set the **Current Preset** to:
   - **Target Only** - Shows health bar ONLY for locked target
   - **Target And Damaged** - Shows for locked target OR any damaged enemy

### Testing the Integration

1. Enter Play Mode
2. Set health bar preset to "Target Only"
3. Approach an enemy - **no health bar should appear**
4. Press **Tab** to lock on - **health bar should appear**
5. Press **Tab** again to unlock - **health bar should fade out**

---

## Part 4: Lock-On Behavior Configuration

### Input Handler (New in EPIC 15.16)

Two systems can handle lock input. You can choose which one via the **Input Handler** setting:

| Handler | Description | Best For |
|---------|-------------|----------|
| **CameraLockOnSystem** (Default) | Runs in ClientWorld, full camera integration, **supports Soft Lock break detection** | Most games, Dark Souls style |
| **LockInputModeSystem** | Runs on both client/server, simpler logic | Server-authoritative locking |

> **Recommendation:** Use **CameraLockOnSystem** (default) unless you need server-authoritative lock state.

### Input Mode (via ActiveLockBehavior)

The lock system supports different input modes:

| Mode | Behavior |
|------|----------|
| **Toggle** | Press Tab to lock, press again to unlock |
| **Hold** | Hold Tab to lock, release to unlock |
| **AutoNearest** | Always locks to nearest valid target |
| **ClickTarget** | Click on enemy to lock (isometric games) |
| **HoverTarget** | Target under cursor (FPS style) |

### Behavior Type

| Type | Camera Behavior | Movement | Mouse Breaks Lock |
|------|-----------------|----------|-------------------|
| **HardLock** | Camera orbits behind player facing target | WASD = strafe | ❌ No |
| **SoftLock** | Camera orbits initially, until mouse moves | WASD = strafe until break | ✅ Immediately |
| **IsometricLock** | ⏳ *Deferred* | Requires isometric camera, click-to-target input | N/A |

> **Note:** Default is Toggle + HardLock for Dark Souls-style gameplay.
> 
> **Deferred Modes:** IsometricLock, TwinStick, and FirstPerson require additional camera/input systems not yet implemented.

---

## Part 4b: Soft Lock Details (God of War Style)

Soft Lock is a hybrid mode that starts like Hard Lock but **breaks when the player moves the mouse** - but only **after the camera has arrived at the target**.

### Lock Phase State Machine

The lock system uses a **3-phase state machine** to prevent false breaks during camera transitions:

```
Unlocked → Locking → Locked → Unlocked
```

| Phase | Description | Mouse Breaks Lock? |
|-------|-------------|-------------------|
| **Unlocked** | No target locked, free camera | N/A |
| **Locking** | Target acquired, camera en route | ❌ **Disabled** |
| **Locked** | Camera has arrived at target | ✅ **Enabled** |

> **Architecture:** `CameraLockOnSystem` runs in `SimulationSystemGroup` (once per frame, after prediction). It queries `CinemachineCameraController.HasCameraArrivedAtTarget` to detect camera arrival and transition phases. This unified approach ensures consistent state updates.

### How It Works

1. **Press Tab** → Phase = `Locking`, camera starts moving toward target
2. **Camera En Route** → Mouse input is **ignored** (prevents accidental break)
3. **Camera Arrives** → Phase = `Locked`, break detection **enabled**
4. **Move Mouse** → Lock breaks, Phase = `Unlocked`
5. **Press Tab Again** → Re-acquire lock

### When To Use Soft Lock

| Use Case | Recommendation |
|----------|----------------|
| Boss fights with long animations | Hard Lock (stay locked) |
| Fighting multiple enemies | Soft Lock (break and retarget) |
| Ranged combat | Soft Lock (manual aim after initial lock) |
| Pure melee (Dark Souls) | Hard Lock (circle strafing) |

### Soft Lock vs Hard Lock Comparison

| Feature | Hard Lock | Soft Lock |
|---------|-----------|-----------|
| Camera tracks target | ✅ Always | ✅ Until mouse moves |
| Circle-strafe movement | ✅ Always | ✅ Until mouse moves |
| Character faces target | ✅ Always | ✅ Until mouse moves |
| Mouse breaks lock | ❌ Never | ✅ Only after camera arrives |
| Tab breaks lock | ✅ Toggle | ✅ Toggle |
| Uses Lock Phase | ✅ Yes | ✅ Yes (critical) |
| Best for | 1v1 duels, bosses | Multiple enemies, ranged |

---

## Part 5: Camera Settings During Lock-On

When locked on, the Cinemachine camera automatically:
- Orbits behind the player
- Keeps the target in view
- Allows minor adjustments with mouse/stick

### Adjustable Parameters (via CinemachineThirdPersonFollow)

| Parameter | Effect | Recommended |
|-----------|--------|-------------|
| **Camera Distance** | How far behind player | 4-6m |
| **Shoulder Offset** | Left/right camera offset | (0.5, 0, 0) |
| **Damping** | Camera smoothing | 0.1-0.3 for responsive |

---

## Part 6: Settings Persistence

All target lock settings are saved to **PlayerPrefs** automatically.

**PlayerPrefs Keys:**
- `Targeting_AllowLock`
- `Targeting_AllowAssist`  
- `Targeting_ShowIndicator`

Settings persist across play sessions and builds.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Can't lock onto enemy | Ensure enemy has **Lock-On Target** component and it's enabled |
| Lock works but no health bar | Set health bar mode to "WhenTargeted" or "WhenTargetedOrDamaged" |
| Camera doesn't orbit | Check Cinemachine camera has **CinemachineThirdPersonFollow** |
| Settings don't save | Check PlayerPrefs aren't being cleared elsewhere |
| Lock breaks immediately | Verify target is within range (default 30m) and has LOS |
| Wrong target selected | Adjust **Priority** values on enemy prefabs |
| Soft Lock not breaking on mouse | Ensure **Current Mode** is set to **SoftLock** in TargetingModeTester |
| Soft Lock breaks before camera arrives | This was fixed by Lock Phase state machine - ensure you have the latest code |
| Mode doesn't change at runtime | Check **Apply On Change** is enabled in TargetingModeTester |
| Input not registering | Verify **Input Handler** is set correctly (default: CameraLockOnSystem) |
| Screen flickers when breaking Soft Lock | This is fixed - ensure you have latest CinemachineCameraController |
| Camera doesn't signal arrival | Check CinemachineCameraController is in scene and initialized |

---

## Component Quick Reference

| Component | Location | Purpose |
|-----------|----------|---------|
| **Lock-On Target** | DIG/Targeting | Add to enemies to make them targetable |
| **Target Lock Tester** | DIG/Debug | Runtime toggle for testing (on/off) |
| **Targeting Mode Tester** | DIG/Targeting | Test different lock modes and behaviors |
| **Health Bar Visibility Tester** | (Combat UI) | Test WhenTargeted mode |

---

## Related Documentation

- [EPIC15.16.md](EPIC15.16.md) - Full technical specification
- [EPIC15.14.md](EPIC15.14.md) - Health Bar Visibility System
- [SETUP_GUIDE_15.15.md](../SETUP_GUIDE_15.15.md) - Combat State Health Bar Setup
