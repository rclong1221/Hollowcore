# EPIC 15.3: Movement System Polish

## Goal
To elevate the custom ECS Movement System to professional standards by adding "Missing" traversal mechanics (Mantling, Air Strafe) and "Game Feel" (Grounding, Weight).

---

## 1. Procedural Ledge Mantling
*   **Missing:** Players get stuck on waist-high walls.
*   **Solution:**
    *   **Raycast logic:** Detect "Ledge Top" and "Wall Face".
    *   **State:** `FreeClimbState.Mantling`.
    *   **Execution:** Disable Physics -> Lerp Character to Top Position -> Play Animation -> Re-enable Physics.

## 2. Advanced Air Control (Air Strafing)
*   **Missing:** Air movement feels "floaty" or "on rails". No ability to correct jumps.
*   **Solution:**
    *   Implement "Air Strafe" logic (Source Engine style).
    *   Allow redirection of velocity *perpendicular* to motion, but limit acceleration *parallel* to motion.
    *   Result: Players can curve around corners in mid-air.

## 3. Physical Crouching & Stealth
*   **Missing:** Collision capsule size often doesn't match visual crouch (clipping).
*   **Missing:** Stealth mechanics (AI detection based on speed).
*   **Solution:**
    *   **Dynamic Capsule:** Resize physics collider in `CharacterControllerSystem` based on Stance (Stand/Crouch/Prone).
    *   **Noise Warning:** Emit "Noise Events" based on Speed * Surface * Stance. (Sprint = Loud, Crouch = Silent).

## 4. Movement Feedback (FEEL Integration)

### Concept
Use **More Mountains FEEL** to handle all non-gameplay feedback. Remove custom audio/particle spawners.

### Channels
1.  **Footsteps:**
    *   **Input:** Surface Material ID (Grass, Stone).
    *   **Action:** `MMF_Player` plays Random Sound + Dust Particle.
2.  **Jump/Land:**
    *   **Action:** Camera Shake (Y-Axis), Land Sound (Volume varies by Fall Distance).
    *   **Haptics:** Short rumble on Land.
3.  **Sliding:**
    *   **Action:** Loop Sound + Sparks/Dust Particle trail.

---

## Implementation Tasks
- [ ] Prototype Ledge Detection Raycasts in Editor.
- [ ] Implement `Mantling` state in `FreeClimbMovementSystem`.
- [x] Refactor Air Physics in `PlayerMovementSystem` for Strafing. (Source-style air strafe implemented)
- [ ] Verify Capsule resizing with debug visualization.
- [ ] Integrate FEEL `MMF_Player` for Footsteps (replace legacy AudioSource).
