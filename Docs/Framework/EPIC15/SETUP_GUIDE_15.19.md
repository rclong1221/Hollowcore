# Epic 15.19 Setup Guide: Aggro & Threat System

This guide covers the Unity Editor setup for the **AI Aggro & Threat System**, including detection, threat tables, leashing, pack behavior, and alert states.

---

## Overview

The EPIC 15.19 system gives AI entities intelligent threat-based targeting:

- **Vision & Hearing Detection** – Cone-based LOS + 360° proximity/sound
- **Threat Tables** – Track multiple targets with threat values
- **Alert States** – IDLE → SUSPICIOUS → COMBAT with detection modifiers
- **Leashing** – AI returns home when chasing too far
- **Pack Behavior** – Nearby allies share aggro when one is attacked

---

## Quick Start

### 1. AI Entity Setup (Enemies, NPCs)

Add these authoring components to any AI prefab:

| Component | Purpose |
|-----------|---------|
| `Detection Sensor Authoring` | Vision cone + hearing range |
| `Aggro Authoring` | Threat tracking + behavior tuning |

**Menu Path:** `Add Component > DIG > Detection > Detection Sensor Authoring`  
**Menu Path:** `Add Component > DIG > Aggro > Aggro Authoring`

### 2. Player/Target Setup

Add this to entities that AI should detect:

| Component | Purpose |
|-----------|---------|
| `Detectable Authoring` | Marks entity as visible to AI |

**Menu Path:** `Add Component > DIG > Detection > Detectable Authoring`

---

## Component Reference

### Detection Sensor Authoring

Add to **AI entities** (enemies, guards, creatures).

| Property | Description | Recommended |
|----------|-------------|-------------|
| **View Distance** | Max sight range in meters | 20–40m |
| **View Angle** | Horizontal FOV half-angle (45° = 90° total) | 45–60° |
| **Vertical View Angle** | Up/down FOV half-angle | 30° (humans), 180° (all-seeing) |
| **Eye Height** | Vertical offset for raycast origin | 1.6m (humanoid) |
| **Proximity Radius** | 360° close-range detection (bypasses cone) | 2–3m or 0 (disabled) |
| **Hearing Radius** | 360° sound detection range | 15–25m |
| **Update Interval** | Seconds between detection scans | 0.2s (5Hz) |

**Tips:**
- Lower `Update Interval` for elite enemies (0.1s)
- Use `Proximity Radius` to prevent backstab cheese
- Blind creatures: set `View Distance = 0`, rely on hearing

---

### Detectable Authoring

Add to **targets** (player, allies, destructibles).

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Detection Height Offset** | Raycast target point above origin | 1.0m (center mass) |
| **Stealth Multiplier** | 1.0 = visible, 0.5 = half range, 0.0 = invisible | 1.0 (default) |
| **Start Enabled** | Whether detection starts active | ✓ (true) |

**Tips:**
- Toggle `Detectable` component enabled/disabled for stealth abilities
- Use `Stealth Multiplier` for crouching, cloaking, etc.

---

### Aggro Authoring

Add to **AI entities** alongside Detection Sensor.

#### Threat Multipliers

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Damage Threat Multiplier** | How much damage → threat (1 damage = X threat) | 1.0 |
| **Sight Threat Value** | Base threat when first spotting a target | 10 |
| **Hearing Threat Value** | Base threat from hearing sounds | 3 |

#### Decay Settings

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Visible Decay Rate** | Threat/sec reduction for visible targets | 0.5 |
| **Hidden Decay Rate** | Threat/sec reduction for hidden targets | 0.5 |
| **Memory Duration** | Seconds before forgetting hidden target | 30s |

#### Target Selection

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Hysteresis Ratio** | Only switch if new threat > current × ratio | 1.1 (10% buffer) |
| **Max Tracked Targets** | Size of threat table | 8 |
| **Minimum Threat** | Threshold to remain in table | 0.1 |

#### Leashing & Territory

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Leash Distance** | Max chase distance from spawn before reset | 50m (normal), 0 (boss) |

#### Social Behavior

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Aggro Share Radius** | Alert allies within this range when aggroed | 20m (pack), 0 (solo) |
| **Alert State Multiplier** | Detection boost when suspicious/combat | 1.5 (50% better) |

---

## AI Archetypes

### Standard Guard
```
Detection Sensor:
  View Distance: 25
  View Angle: 50
  Proximity Radius: 2
  Hearing Radius: 20

Aggro:
  Leash Distance: 40
  Aggro Share Radius: 25
  Alert State Multiplier: 1.5
```

### Elite/Mini-Boss
```
Detection Sensor:
  View Distance: 35
  View Angle: 60
  Proximity Radius: 3
  Hearing Radius: 30
  Update Interval: 0.1

Aggro:
  Memory Duration: 60
  Leash Distance: 100
  Aggro Share Radius: 40
  Alert State Multiplier: 2.0
```

### Boss (No Leash)
```
Aggro:
  Leash Distance: 0  ← Disabled
  Memory Duration: 999
  Hysteresis Ratio: 1.0  ← Always target highest threat
```

### Pack Hunter (Wolf, Raptors)
```
Aggro:
  Aggro Share Radius: 50  ← Large pack range
  Alert State Multiplier: 2.0
```

### Lone Wolf (Assassin, Stealth Enemy)
```
Aggro:
  Aggro Share Radius: 0  ← No sharing
  Leash Distance: 200  ← Persistent pursuit
```

### Blind Creature (Sound-Based)
```
Detection Sensor:
  View Distance: 0
  View Angle: 1
  Proximity Radius: 5
  Hearing Radius: 50  ← Enhanced hearing
```

---

## Scene Requirements

No additional scene GameObjects required. The systems run automatically when entities with the authoring components are baked.

---

## Runtime Debugging

### Gizmos

Enable `Gizmos` in Scene view to see:
- Vision cones (green wireframe)
- Detection lines to seen targets
- Hearing radius (blue sphere)

### Debug Components

For development, use these debug tools:

1. **AggroDebugTester** – MonoBehaviour for testing aggro in isolation
2. **AggroPipelineDebug** – Logs the full aggro pipeline flow

---

## Alert State Flow

AI entities automatically transition through alert states:

```
IDLE ──(spot target)──▶ SUSPICIOUS ──(confirm threat)──▶ COMBAT
  ▲                                                         │
  └────────────(no threats for X seconds)───────────────────┘
```

| State | Detection Modifier | Behavior |
|-------|-------------------|----------|
| **IDLE** | 1.0× | Normal patrol, relaxed |
| **SUSPICIOUS** | AlertStateMultiplier× | Investigating, heightened awareness |
| **COMBAT** | AlertStateMultiplier× | Engaged, full aggression |

---

## Integration with Other Systems

### Combat State System
The `AggroCombatStateIntegration` system syncs aggro with combat state:
- When AI has threats → sets combat state to ENGAGED
- When threats clear → combat state to IDLE

### AI Behavior Trees / State Machines
Read `TargetData` component for current target:
```csharp
// In your AI behavior system:
if (SystemAPI.HasComponent<TargetData>(entity))
{
    var target = SystemAPI.GetComponent<TargetData>(entity);
    if (target.CurrentTarget != Entity.Null)
    {
        // AI has a valid target
    }
}
```

---

## Best Practices

1. **Start with presets** – Use the archetypes above as starting points
2. **Test leash distance** – Make sure AI can't be kited to boss rooms
3. **Balance pack size** – Large `Aggro Share Radius` can create difficulty spikes
4. **Use hearing for stealth** – Players learn to avoid sound-based detection
5. **Memory duration matters** – Short memory = easy to lose aggro, long = persistent hunters

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| AI doesn't detect player | Check `Detectable Authoring` on player, verify layers in Physics settings |
| AI sees through walls | Ensure colliders have correct layer for LOS raycasts |
| AI never loses aggro | Reduce `Memory Duration`, enable `Leash Distance` |
| Pack aggro too strong | Reduce `Aggro Share Radius` or set to 0 |
| AI switches targets erratically | Increase `Hysteresis Ratio` to 1.2+ |

---

## Previous Versions

- [15.7 – Opsive Parity Analysis](SETUP_GUIDE_15.7.md)
- [15.5 – Weapon System Completeness](SETUP_GUIDE_15.5.md)
