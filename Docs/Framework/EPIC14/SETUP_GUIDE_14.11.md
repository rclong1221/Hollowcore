# EPIC 14.11 - Footstep Trigger System Setup Guide

## Overview

The Footstep Trigger system provides accurate, physics-based footstep detection for DIG characters. It bridges Unity's physics triggers (Monobehaviour) with the ECS audio pipeline (`FootstepEvent`), removing dependencies on external assets like Opsive while utilizing DIG's existing `SurfaceDetectionService`.

---

## Files Created

| File | Purpose | Location |
|------|---------|----------|
| `FootstepTrigger.cs` | Detects ground collisions on feet | `Assets/Scripts/Player/Bridges/` |
| `CharacterFootEffects.cs` | Handles logic and dispatches ECS events | `Assets/Scripts/Player/Bridges/` |

---

## Quick Setup

### 1. Character Root Setup

1.  Select your **Character Root** GameObject (e.g., `Atlas_Client`).
2.  Add the `CharacterFootEffects` component.
3.  **Configuration:**
    *   `Ground Layers`: Select layers that should trigger footsteps (e.g., `Default`, `Ground`, `Terrain`).

### 2. Foot Bone Setup

1.  Navigate to your character's skeleton hierarchy.
2.  Locate the **Left Toe** and **Right Toe** bones (or Foot bones if Toes aren't available).
    *   *Example:* `Root/Hips/.../LeftFoot/LeftToeBase`
3.  Add the `FootstepTrigger` component to **BOTH** toe bones.

### 3. Physics Setup (Critical)

For `OnTriggerEnter` to fire, specific conditions must be met:

1.  **Add Colliders:**
    *   Add a **Sphere Collider** (radius ~0.1) or **Box Collider** to each Toe bone.
    *   **IMPORTANT:** Check `Is Trigger` = **True**.

2.  **Rigidbody Requirement:**
    *   Unity mechanics require at least one object in a collision to have a Rigidbody.
    *   **If your Character Root has a Rigidbody:** You are likely set.
    *   **If not (or if triggers are inconsistent):** Add a `Rigidbody` to each Toe bone.
        *   `Use Gravity`: **False**
        *   `Is Kinematic`: **True**

---

## Component Reference

### CharacterFootEffects

| Field | Type | Description |
|-------|------|-------------|
| `Ground Layers` | LayerMask | Layers that constitute valid ground surfaces. Ignored layers will not trigger sounds. |
| `Provider` | (Auto) | Auto-finds `DIGEquipmentProvider` to access the ECS Entity. |

### FootstepTrigger

*No public fields. Automatically finds `CharacterFootEffects` in parent.*

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **No Sound detected** | 1. Check `Ground Layers` mask includes the floor layer.<br>2. Verify `AudioReference` is set up in `AudioPlaybackSystem` (check Console for errors). |
| **Triggers not firing** | 1. Ensure Toes have `Collider` (`Is Trigger`=True).<br>2. Ensure valid `Rigidbody` chain (add Kinematic RB to toes if needed). |
| **Material is "Default"** | 1. Ensure ground object has a `PhysicMaterial` or `Texture` mapped in `SurfaceMaterialMapping`.<br>2. Check `SurfaceDetectionService` debug logs. |
| **Double Sounds** | 1. Check if legacy `FootstepSystem` is also running (disable via `AudioSettings.UseAnimatorForFootsteps = true`). |
