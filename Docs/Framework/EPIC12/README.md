# EPIC 12: Advanced Traversal Systems

## Overview

This Epic covers advanced player traversal mechanics beyond basic movement, including climbing and swimming.

## Sub-Epics

| Epic | Status | Description |
|------|--------|-------------|
| [EPIC 12.1](./EPIC12.1.md) | COMPLETE | Core Free Climbing System (Surface Detection, Movement, Ledge Grab) |
| [EPIC 12.2](./EPIC12.2.md) | NOT STARTED | Advanced Free Climbing Features (Animation, Wall Jump, IK, Stamina) |
| [EPIC 12.3](./EPIC12.3.md) | NOT STARTED | Physics-Based Swimming System |

## Dependencies

- EPIC 1: Character Controller (base movement)
- EPIC 3: Environment Zones (trigger detection patterns)

## Reference Implementation

Both climbing sub-epics adapt proven algorithms from the Invector Third Person Controller Add-ons:
- **FreeClimb:** `Assets/Invector-3rdPersonController/Add-ons/FreeClimb/`
- **Swimming:** `Assets/Invector-3rdPersonController/Add-ons/Swimming/`

## Architecture Notes

### ECS Adaptation Strategy

The reference implementations use MonoBehaviour patterns. For DIG's DOTS architecture:
1. Component state replaces instance variables
2. Systems replace Update/LateUpdate methods
3. Physics queries use Unity.Physics instead of UnityEngine.Physics
4. Animation integration through IJobAnimatedCharacter or managed bridge
