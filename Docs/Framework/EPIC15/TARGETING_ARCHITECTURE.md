# Targeting System Architecture

## Overview

The DIG targeting system is designed to support multiple camera modes with consistent target selection but mode-specific lock behaviors.

## Lock-On Paradigms

### Hard Lock (3rd Person Combat Mode)
```
┌─────────────────────────────────────────────────────────────────┐
│ Camera TRACKS target • Player STRAFES around target            │
│                                                                 │
│              ┌───────────┐                                      │
│              │  TARGET   │                                      │
│              │    ◆      │                                      │
│              └─────┬─────┘                                      │
│                    │                                            │
│         Camera Yaw │ forced to face                             │
│                    │                                            │
│              ┌─────▼─────┐                                      │
│              │  PLAYER   │                                      │
│              │    ●──────┼──▶ Movement = strafe                 │
│              └───────────┘                                      │
│                                                                 │
│ Use case: Boss fights, duels, melee combat                      │
│ Examples: Dark Souls, Elden Ring, Monster Hunter                │
└─────────────────────────────────────────────────────────────────┘
```

### Soft Lock (Action Adventure Mode)
```
┌─────────────────────────────────────────────────────────────────┐
│ Camera stays FREE • Character ROTATES toward target             │
│                                                                 │
│              ┌───────────┐                                      │
│              │  TARGET   │                                      │
│              │    ◆      │                                      │
│              └───────────┘                                      │
│                    ▲                                            │
│     Aim magnetism  │  Character faces                           │
│              ┌─────┴─────┐                                      │
│              │  PLAYER   │                                      │
│              │    ●      │   Camera: player controlled          │
│              └─────┬─────┘                                      │
│                    │                                            │
│         ┌─────────┐│┌─────────┐                                 │
│         │ Attack  │││  Move   │◀── Both work independently      │
│         │ → target││→ free   │                                  │
│         └─────────┘│└─────────┘                                 │
│                                                                 │
│ Use case: Multiple enemies, ranged combat, exploration          │
│ Examples: God of War (2018), Horizon, Assassin's Creed          │
└─────────────────────────────────────────────────────────────────┘
```

### Isometric Lock (Top-Down Mode)
```
┌─────────────────────────────────────────────────────────────────┐
│ Camera FIXED overhead • Character FACES target                  │
│                                                                 │
│         ┌───────────────────────────────────────┐               │
│         │              CAMERA VIEW              │               │
│         │                  ▼                    │               │
│         │    ◆ Enemy    ● Player               │               │
│         │                  └──▶ Faces enemy    │               │
│         │                                       │               │
│         └───────────────────────────────────────┘               │
│                                                                 │
│ Target Selection:                                               │
│ - Click-to-target (mouse)                                       │
│ - Auto-nearest to cursor                                        │
│ - Cycle with Q/E or shoulder buttons                            │
│                                                                 │
│ Use case: Tactical combat, AoE awareness, many enemies          │
│ Examples: Diablo, Hades, LoL, XCOM                              │
└─────────────────────────────────────────────────────────────────┘
```

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              TARGETING PIPELINE                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  INPUT                                                                      │
│    │                                                                        │
│    ▼                                                                        │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ TARGET SELECTION SYSTEM (Shared - runs for all modes)                │  │
│  │                                                                       │  │
│  │  - Find nearest LockOnTarget in range                                 │  │
│  │  - Respect priority (bosses > elites > normal)                        │  │
│  │  - FOV check for acquisition                                          │  │
│  │  - Line of sight (optional)                                           │  │
│  │  - Target cycling (next/previous)                                     │  │
│  │                                                                       │  │
│  │  OUTPUT: TargetingState.CurrentTarget                                 │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│    │                                                                        │
│    ▼                                                                        │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ LOCK BEHAVIOR DISPATCHER (Routes to appropriate system)              │  │
│  │                                                                       │  │
│  │  Check ActiveLockBehavior.BehaviorType:                               │  │
│  │    - HardLock → HardLockSystem                                        │  │
│  │    - SoftLock → SoftLockSystem                                        │  │
│  │    - IsometricLock → IsometricLockSystem                              │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│    │               │               │                                        │
│    ▼               ▼               ▼                                        │
│  ┌────────────┐ ┌────────────┐ ┌────────────────┐                          │
│  │ HARD LOCK  │ │ SOFT LOCK  │ │ ISOMETRIC LOCK │                          │
│  │   SYSTEM   │ │   SYSTEM   │ │     SYSTEM     │                          │
│  ├────────────┤ ├────────────┤ ├────────────────┤                          │
│  │ Override   │ │ Character  │ │ Character      │                          │
│  │ camera yaw │ │ rotation   │ │ rotation       │                          │
│  │            │ │ + aim mag  │ │ (no camera)    │                          │
│  │ Strafe     │ │            │ │                │                          │
│  │ movement   │ │ Free move  │ │ Click-target   │                          │
│  └────────────┘ └────────────┘ └────────────────┘                          │
│    │               │               │                                        │
│    ▼               ▼               ▼                                        │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ LOCK INDICATOR SYSTEM (Shared - UI reticle on target)               │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Component Layout

```csharp
// On PLAYER entities:
TargetingState          // Current target, lock status, cycle index
TargetSelectionConfig   // Range, FOV, LOS settings
// (already existing):
PlayerCameraSettings    // Camera yaw/pitch
PlayerInput             // Lock button, cycle input

// On TARGETABLE entities (enemies, destructibles):
LockOnTarget            // Priority, indicator height offset

// SINGLETON (determines active mode):
ActiveLockBehavior      // HardLock / SoftLock / IsometricLock + params
TargetLockSettings      // AllowTargetLock, ShowIndicator, etc.
```

## Mode Switching

When camera mode changes (3rd person → Isometric):

1. `CameraModeSystem` detects mode change
2. Updates `ActiveLockBehavior` singleton:
   ```csharp
   // Switching to isometric
   singleton.BehaviorType = LockBehaviorType.IsometricLock;
   singleton.CharacterRotationStrength = 0.25f;
   ```
3. Lock behavior systems check `ActiveLockBehavior` before running
4. Only the matching system processes the lock

## Input Mapping

| Action          | 3rd Person (Hard/Soft) | Isometric            |
|-----------------|------------------------|----------------------|
| Lock Toggle     | Tab / L3               | —                    |
| Target Nearest  | Tab / L3               | Left Click           |
| Cycle Next      | Right Stick →          | Q / Scroll Down      |
| Cycle Previous  | Right Stick ←          | E / Scroll Up        |
| Clear Target    | Tab (toggle off)       | Click empty space    |

## Files

```
Assets/Scripts/Targeting/
├── Core/
│   ├── LockBehaviorType.cs         # Enum + ActiveLockBehavior singleton
│   ├── TargetingState.cs           # Per-player target state
│   └── TargetSelectionConfig.cs    # Selection parameters
├── Components/
│   ├── LockOnTarget.cs             # On targetable entities
│   └── TargetLockSettings.cs       # Global toggle settings
├── Systems/
│   ├── TargetSelectionSystem.cs    # Finds targets (shared)
│   ├── HardLockSystem.cs           # Camera lock behavior
│   ├── SoftLockSystem.cs           # Aim assist behavior
│   ├── IsometricLockSystem.cs      # Top-down behavior
│   └── LockIndicatorSystem.cs      # UI reticle (shared)
├── Settings/
│   └── TargetLockSettingsManager.cs
└── Debug/
    └── TargetLockTester.cs
```

## Migration Plan

1. **Phase 1**: Create `TargetingState` component, migrate from `CameraTargetLockState`
2. **Phase 2**: Extract target selection into `TargetSelectionSystem`
3. **Phase 3**: Create `HardLockSystem` (current behavior)
4. **Phase 4**: Add `SoftLockSystem` with aim assist
5. **Phase 5**: Add `IsometricLockSystem` when isometric camera is implemented
6. **Phase 6**: Add `ActiveLockBehavior` singleton and mode switching

---

## Additional Camera Modes

### Over-the-Shoulder (RE4, Gears, TLOU)
```
┌─────────────────────────────────────────────────────────────────┐
│ Camera OFFSET to one side • ADS brings camera CLOSER/CENTERED  │
│                                                                 │
│    Normal View:              Aiming (ADS):                      │
│    ┌─────────────┐           ┌─────────────┐                    │
│    │         ◆   │           │      ◆      │                    │
│    │   ●━━━━━━▶  │           │    ●━━▶     │                    │
│    │   ↑         │           │    ↑        │                    │
│    │ shoulder    │           │ centered    │                    │
│    └─────────────┘           └─────────────┘                    │
│                                                                 │
│ Features:                                                       │
│ - Shoulder swap for visibility (around corners)                 │
│ - Tighter zoom when aiming                                      │
│ - Lock can auto-swap shoulder when target is occluded           │
│                                                                 │
│ Use case: Cover shooters, stealth, precision aiming             │
│ Examples: RE4, Gears of War, The Last of Us                     │
└─────────────────────────────────────────────────────────────────┘
```

### Twin-Stick (Helldivers, Enter the Gungeon)
```
┌─────────────────────────────────────────────────────────────────┐
│ Move with LEFT STICK • Aim with RIGHT STICK                    │
│                                                                 │
│              ┌───────────────────────────────┐                  │
│              │         Top-Down View         │                  │
│              │                               │                  │
│              │   ◆ ←aim━━●                  │                  │
│              │             ↘                 │                  │
│              │              move             │                  │
│              │                               │                  │
│              └───────────────────────────────┘                  │
│                                                                 │
│ Lock Behavior:                                                  │
│ - Sticky Aim: Aim slows when crossing over targets              │
│ - Auto-Target: Nearest enemy in aim direction                   │
│ - No explicit lock button (always "soft locked")                │
│                                                                 │
│ Use case: Fast arcade action, many projectiles, chaotic combat  │
│ Examples: Helldivers 2, Enter the Gungeon, Geometry Wars        │
└─────────────────────────────────────────────────────────────────┘
```

### First Person (Halo, Destiny, CoD)
```
┌─────────────────────────────────────────────────────────────────┐
│ Camera IS the view • Lock = AIM ASSIST only                    │
│                                                                 │
│    ┌──────────────────────────────────────┐                     │
│    │                                      │                     │
│    │              ◆ ← target              │                     │
│    │            ╱                         │                     │
│    │       ═══╳═══ ← crosshair            │                     │
│    │          ╲     (magnetism pulls      │                     │
│    │           ╲     toward target)       │                     │
│    │                                      │                     │
│    └──────────────────────────────────────┘                     │
│                                                                 │
│ Aim Assist Types:                                               │
│ - Sticky Aim: Aim slows when on target                          │
│ - Magnetism: Crosshair pulls toward target center               │
│ - Bullet Bending: Shots curve slightly toward target            │
│ - Hitbox Expansion: Target hitbox larger when near crosshair    │
│                                                                 │
│ Use case: Console FPS, accessibility                            │
│ Examples: Halo, Destiny, Call of Duty                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Lock-On Variations

### Multi-Lock (Ace Combat, Zone of the Enders)
```
┌─────────────────────────────────────────────────────────────────┐
│ Lock MULTIPLE targets simultaneously • Fire missile salvo      │
│                                                                 │
│    Hold Lock Button:        Release:                            │
│    ┌─────────────┐          ┌─────────────┐                     │
│    │ ◆₁  ◆₂  ◆₃ │          │ ◆←─ ◆←─ ◆←─│                     │
│    │    ╲  │  ╱  │          │    missiles │                     │
│    │     ╲ │ ╱   │          │             │                     │
│    │       ●     │          │       ●     │                     │
│    └─────────────┘          └─────────────┘                     │
│                                                                 │
│ Implementation:                                                 │
│ - LockedTargetElement buffer (up to 8 targets)                  │
│ - Hold to accumulate, release to fire                           │
│ - Each target gets one missile                                  │
│                                                                 │
│ Use case: Mech games, flight sims, AoE attacks                  │
│ Examples: Ace Combat, Zone of the Enders, Armored Core          │
└─────────────────────────────────────────────────────────────────┘
```

### Part Targeting (Monster Hunter, Fallout VATS)
```
┌─────────────────────────────────────────────────────────────────┐
│ Target SPECIFIC BODY PARTS • Break parts for bonus drops       │
│                                                                 │
│       ┌─────────────────┐                                       │
│       │    HEAD (2.0x)  │←─ Current target                      │
│       │      ◆₁        │                                       │
│       │    ╱    ╲       │                                       │
│       │ ◇ ARM   ARM ◇  │←─ Other targetable parts              │
│       │    │BODY│       │                                       │
│       │    ◇    ◇       │                                       │
│       │   LEG  LEG      │                                       │
│       │   ◇    ◇        │                                       │
│       └─────────────────┘                                       │
│                                                                 │
│ Implementation:                                                 │
│ - TargetablePartElement buffer on enemy                         │
│ - Cycle between parts with input                                │
│ - Each part has damage multiplier, break threshold              │
│                                                                 │
│ Use case: Boss fights, precision combat, loot farming           │
│ Examples: Monster Hunter, Fallout VATS, The Surge               │
└─────────────────────────────────────────────────────────────────┘
```

### Predictive Aim (Flight Sims, Space Games)
```
┌─────────────────────────────────────────────────────────────────┐
│ Show LEAD INDICATOR • Where to aim to hit moving target        │
│                                                                 │
│    ┌──────────────────────────────────────┐                     │
│    │                    ◇ ← lead indicator│                     │
│    │                  ╱                   │                     │
│    │       ◆━━━━━━━━▶ ← target moving     │                     │
│    │                                      │                     │
│    │           ╳ ← player crosshair       │                     │
│    │                                      │                     │
│    └──────────────────────────────────────┘                     │
│                                                                 │
│ Calculation:                                                    │
│ - Track target velocity                                         │
│ - Calculate projectile travel time                              │
│ - Predict intercept point                                       │
│ - Display lead indicator at predicted position                  │
│                                                                 │
│ Use case: Dogfighting, space combat, artillery                  │
│ Examples: War Thunder, Elite Dangerous, MechWarrior             │
└─────────────────────────────────────────────────────────────────┘
```

### Priority Auto-Switch
```
┌─────────────────────────────────────────────────────────────────┐
│ Auto-switch to HIGHER PRIORITY target when current dies        │
│                                                                 │
│    Before:              Enemy Dies:           After:            │
│    ┌─────────┐          ┌─────────┐          ┌─────────┐        │
│    │ ◆ Boss  │          │ ◆ Boss  │          │ ◆ Boss ←│        │
│    │         │          │         │          │ locked  │        │
│    │ ◇ Elite │          │ ◇ Elite │          │ ◇ Elite │        │
│    │    ↑    │          │         │          │         │        │
│    │ locked  │          │ ✗ died  │          │         │        │
│    └─────────┘          └─────────┘          └─────────┘        │
│                                                                 │
│ Priority Order:                                                 │
│ 1. Boss (highest priority)                                      │
│ 2. Elite enemies                                                │
│ 3. Normal enemies                                               │
│ 4. Destructibles (lowest priority)                              │
│                                                                 │
│ Use case: Seamless combat flow, boss fights with adds           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Input Methods Summary

| Method | Description | Best For |
|--------|-------------|----------|
| **Toggle** | Press to lock, press again to unlock | Controller, clear intent |
| **Hold** | Hold = locked, release = free | Temporary lock, OTS aiming |
| **Click Target** | Click directly on enemy | Mouse, isometric/RTS |
| **Hover Target** | Target under cursor | PC FPS, precision |
| **Right Stick Flick** | Flick direction to cycle | Controller, multiple targets |
| **Auto-Nearest** | Always targets closest valid | Accessibility, twin-stick |
| **Radial Selection** | Hold button + direction | Many targets, slower pace |

---

## Extended Component Summary

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// CORE TARGETING (All Modes)
// ═══════════════════════════════════════════════════════════════════════════
TargetingState              // Current target, lock status
TargetSelectionConfig       // Range, FOV, settings
ActiveLockBehavior          // Current mode + parameters
LockOnTarget                // On targetable entities

// ═══════════════════════════════════════════════════════════════════════════
// MULTI-LOCK (Ace Combat, Mech Games)
// ═══════════════════════════════════════════════════════════════════════════
MultiLockState              // Locked count, accumulating flag
LockedTargetElement         // Buffer of locked targets

// ═══════════════════════════════════════════════════════════════════════════
// PART TARGETING (Monster Hunter, VATS)
// ═══════════════════════════════════════════════════════════════════════════
PartTargetingState          // Currently selected part
TargetablePartElement       // Buffer of parts on enemy

// ═══════════════════════════════════════════════════════════════════════════
// OVER-THE-SHOULDER (RE4, Gears)
// ═══════════════════════════════════════════════════════════════════════════
OverTheShoulderState        // Shoulder side, zoom level

// ═══════════════════════════════════════════════════════════════════════════
// PREDICTIVE AIM (Flight Sims)
// ═══════════════════════════════════════════════════════════════════════════
PredictiveAimState          // Target velocity, intercept point

// ═══════════════════════════════════════════════════════════════════════════
// AIM ASSIST (Console FPS)
// ═══════════════════════════════════════════════════════════════════════════
AimAssistState              // Sticky target, magnetism pull
```
