# Epic 15.17 Setup Guide: Vision / Line-of-Sight System

This guide covers the Unity Editor setup for the **AI Vision & Detection System**, the foundation for AI awareness of players and targets.

---

## Overview

EPIC 15.17 provides the low-level detection layer that feeds into the Aggro system (EPIC 15.19):

- **Vision Cones** – Configurable horizontal/vertical FOV with range
- **Line-of-Sight Raycasts** – Accurate occlusion testing through geometry
- **Proximity Detection** – 360° close-range awareness
- **Stealth Support** – Per-target visibility modifiers
- **Memory System** – Targets persist briefly after breaking LOS

---

## Quick Start

### 1. Scene Setup (Required Once)

Create a singleton settings object:

1. Create empty GameObject: `_DetectionSettings`
2. Add Component: `DIG > Detection > Detection Settings`

**Menu Path:** `Add Component > DIG > Detection > Detection Settings`

### 2. AI Entity Setup

Add to any AI that needs to see targets:

| Component | Purpose |
|-----------|---------|
| `Detection Sensor Authoring` | Vision cone + hearing configuration |

**Menu Path:** `Add Component > DIG > Detection > Detection Sensor Authoring`

### 3. Target Setup

Add to entities AI should be able to detect (player, allies):

| Component | Purpose |
|-----------|---------|
| `Detectable Authoring` | Makes entity visible to AI sensors |

**Menu Path:** `Add Component > DIG > Detection > Detectable Authoring`

---

## Component Reference

### Detection Settings (Singleton)

Place on **one GameObject per scene**. Controls global vision system behavior.

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Global Update Interval** | Default scan rate for sensors (seconds) | 0.2 (5 Hz) |
| **Memory Duration** | How long sensors remember targets after LOS break | 5s |
| **Max Raycasts Per Frame** | Performance cap for occlusion checks | 64 |
| **Enable Stealth Modifiers** | Master toggle for stealth system | ✓ (true) |

**Tips:**
- Lower `Global Update Interval` for responsive AI (costs more CPU)
- Increase `Max Raycasts Per Frame` for large enemy counts
- Disable stealth modifiers if not using stealth mechanics

---

### Detection Sensor Authoring

Add to **AI entities** (enemies, turrets, guards).

| Property | Description | Recommended |
|----------|-------------|-------------|
| **View Distance** | Maximum sight range in meters | 20–40m |
| **View Angle** | Horizontal FOV half-angle (45° = 90° total cone) | 45–60° |
| **Vertical View Angle** | Up/down FOV half-angle | 30° (humans) |
| **Eye Height** | Vertical offset for raycast origin | 1.6m (humanoid) |
| **Proximity Radius** | 360° close-range detection, bypasses cone | 2–3m or 0 |
| **Hearing Radius** | 360° sound detection range | 15–25m |
| **Update Interval** | Override for this sensor (0 = use global) | 0 |

**Creature Presets:**

| Type | View Distance | View Angle | Proximity | Notes |
|------|---------------|------------|-----------|-------|
| Human Guard | 25m | 50° | 2m | Standard humanoid |
| Sniper | 60m | 20° | 0 | Long range, narrow cone |
| Beast | 15m | 80° | 4m | Wide peripheral vision |
| Turret | 40m | 180° | 0 | Full forward hemisphere |
| Blind Creature | 0 | – | 5m | Relies on hearing only |

---

### Detectable Authoring

Add to **targets** that AI should be able to see.

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Detection Height Offset** | Raycast target point above origin | 1.0m (center mass) |
| **Stealth Multiplier** | 1.0 = visible, 0.5 = half range, 0.0 = invisible | 1.0 |
| **Start Enabled** | Whether detection starts active | ✓ (true) |

**Tips:**
- Set `Stealth Multiplier = 0` for true invisibility abilities
- Toggle `Detectable` component enabled/disabled for stealth mechanics
- `Detection Height Offset` should target center mass, not feet

---

## Physics Layer Configuration

The vision system uses `CollisionLayers` for filtering. Ensure your project has these layers configured:

### Required Layers

| Layer Name | Purpose |
|------------|---------|
| `Default` | Static geometry that blocks LOS |
| `Environment` | Level geometry, walls, floors |
| `Ship` | Spaceship hull, interior walls |
| `Player` | Player entity collider |

### Collision Matrix

For LOS raycasts to work correctly:

- **Occlusion checks** collide with: `Default`, `Environment`, `Ship`
- **Detectable query** collides with: `Player`

Ensure player entities have colliders on the `Player` layer.

---

## Scene Requirements

```
Scene Root
├── _DetectionSettings (VisionSettingsAuthoring)  ← Required singleton
├── Player
│   └── [Detectable Authoring]  ← Add to player
└── Enemies
    └── Enemy_Guard
        └── [Detection Sensor Authoring]  ← Add to each AI
```

---

## Runtime Debugging

### Detection Debug Tester

Add `VisionDebugTester` to any GameObject for runtime visualization:

**Menu Path:** `Add Component > DIG > Detection > Debug > Detection Debug Tester`

| Feature | Description |
|---------|-------------|
| **Draw Vision Cones** | Gizmo wireframes in Scene view |
| **Draw Seen Target Lines** | Lines from sensor to detected targets |
| **Override Update Interval** | Test faster/slower scan rates |
| **Override Stealth Multiplier** | Test stealth values globally |

### Gizmo Colors

| Color | Meaning |
|-------|---------|
| Yellow | Vision cone wireframe |
| Green | Line to currently visible target |
| Yellow | Line to remembered (not visible) target |

---

## Health Bar Integration

The vision system integrates with enemy health bars for LOS-based visibility:

### Setup

1. On `EnemyHealthBarPool`, set visibility mode to `WhenInLineOfSight`
2. The system automatically uses vision raycasts from camera to enemy
3. Health bars only appear when player has clear LOS

### Testing

Use `HealthBarVisibilityTester` component:
- Context menu: **Apply LineOfSight Preset (EPIC 15.17)**

---

## Performance Tuning

| Scenario | Adjustment |
|----------|------------|
| Many AI, low-end hardware | Increase `Global Update Interval` to 0.3–0.5s |
| Few elite enemies | Decrease sensor `Update Interval` to 0.1s |
| Large open levels | Increase `Max Raycasts Per Frame` to 128+ |
| Stealth not used | Disable `Enable Stealth Modifiers` |

### Profiling

In Unity Profiler, look for:
- `DetectionSystem` – Main detection logic
- `VisionDecaySystem` – Memory cleanup

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| AI doesn't see player | Verify `Detectable Authoring` on player, check `Player` layer assignment |
| AI sees through walls | Ensure walls have colliders on `Default`/`Environment`/`Ship` layers |
| Vision cones not visible | Enable Gizmos in Scene view, add `VisionDebugTester` |
| Detection feels laggy | Reduce `Update Interval` on important sensors |
| Too many raycasts | Increase `Global Update Interval` or reduce sensor count |

---

## Related Systems

| System | Relationship |
|--------|--------------|
| **Aggro (EPIC 15.19)** | Consumes `SeenTargetElement` buffer to generate threat |
| **Health Bars** | Uses LOS checks for visibility modes |
| **Stealth Abilities** | Modify `Detectable.StealthMultiplier` at runtime |

---

## Next Steps

After setting up vision, configure the threat system:
- [EPIC 15.19 Setup Guide – Aggro & Threat System](SETUP_GUIDE_15.19.md)
