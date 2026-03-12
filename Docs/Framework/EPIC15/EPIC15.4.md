# EPIC 15.4: Camera System Juice

## Goal
To implement features standardized in modern 3rd Person Action games: Camera Collision (Anti-Clip), Lock-On Targeting, and cinematic Shakes using **FEEL**.

---

## 1. Smart Occlusion (Anti-Clip)
*   **Problem:** Camera currently clips through walls, showing backfaces/void.
*   **Solution:**
    *   **SphereCast:** From Character Head to Camera Ideal Pos.
    *   **Reaction:** If hit, pull Camera `Distance` in to the Hit Point (minus buffer).
    *   **Smoothing:** Use `SmoothDamp` to prevent jitter on jagged geometry.

## 2. Z-Targeting (Lock-On)
*   **Problem:** Melee combat is difficult without keeping the enemy centered.
*   **Solution:**
    *   **Input:** Middle Click / R3 to toggle.
    *   **Logic:** Find closest enemy to screen center.
    *   **Orbit:** Camera orbits the *midpoint* between Player and Target.
    *   **Rotation:** Character automatically faces Target when attacking.

## 3. FEEL Integration (Shakes & Impulse)
*   **Replacement:** Replace custom `CameraShakeEffect` with **FEEL**.
*   **Architecture:**
    *   Add `MMCameraShaker` (FEEL component) to Main Camera.
    *   **Impulses:**
        *   *Explosion:* High Amplitude, Low Frequency.
        *   *Damage:* Sharp, Short Jolt.
        *   *Recoil:* Tiny vertical bump per shot.

---

## Implementation Tasks
- [ ] Implement `CameraCollisionSolver` (SphereCast logic).
- [ ] Create `CameraTargetLockState` component and selection logic.
- [ ] Import `FEEL` package and setup `MMCameraShaker`.
- [ ] Create FEEL Feedbacks for Explosion, Damage, and Land.
