# SETUP GUIDE 15.3 - Movement & Polish Systems

## 1. Procedural Mantling

Mantling allows players to automatically climb over obstacles or pull up onto ledges from a climb.

### Requirements
1. **Server-Side:**
    - **Prefab:** `Player (Server)`
    - **Component:** Add `MantleAuthoring`. This controls the logic and physics.
2. **Client-Side:**
    - **Prefab:** `Player (Client)` (The one with the meshes)
    - **Component:** Add `MantleAnimatorBridge`. This drives the animations.
    - **Connection:** Ensure the `Animator` field on `MantleAnimatorBridge` is assigned (drag the player's Animator into it).
3. **Physics:** Layers must be set correctly (Climbable, Default).

### Configuration (`MantleAuthoring`)
| Setting | Recommended | Description |
|---------|-------------|-------------|
| MaxMantleHeightStanding | 2.0 | Max height to mantle from ground (Chest high) |
| MaxMantleHeightCrouching | 1.0 | Max height from crouch (Window height) |
| MantleReachDistance | 0.5 | Forward reach for ledge detection |
| MinLedgeWidth | 0.3 | Minimum ledge depth to stand on |
| MantleDuration | 0.5 | Duration of the lerp animation |

**Troubleshooting:**
- **Not Mantling?** Check if obstacle layer is included in Physics Collision Matrix for Player.
- **Teleporting?** `MantleDuration` might be too short.

---

## 2. Advanced Air Control (Damped Acceleration)
 
 Implemented "Opsive Style" air control. This uses **Air Drag** to naturally slow the player down, while adding **Air Acceleration** based on input.
 
 ### Tuning (`PlayerMovementSettings`)
 - **AirAcceleration:** How fast you can change direction / gain speed in air.
     - **10-15:** Highly responsive (Arcade).
     - **1.5 (Default):** Heavy, realistic momentum.
 - **AirDrag:** How much air resistance exists (Damping).
     - **0.0:** No resistance (Ice physics / Space).
     - **0.3 (Default):** Standard atmospheric drag. Smooth deceleration.
     - **1.0+:** Very thick atmosphere (Underwater/Mud feel).
 
 ---

## 3. Physical Crouching and Height

The player capsule now resizes dynamically based on Stance.

### Configuration (`PlayerStanceConfig`)
| Setting | Height (m) | Description |
|---------|------------|-------------|
| StandingHeight | 1.8 | Standard character height |
| CrouchingHeight | 1.0 | Crouched height (Half cover) |
| ProneHeight | 0.4 | Prone height (Crawling) |

**Notes:**
- `CameraViewConfig` handle the Camera Height separately. Ensure they match roughly (Eye Height < Capsule Height).

---

## 4. Movement Feedback (FEEL Integration)

Replaced legacy `AudioManager` movement calls with `GameplayFeedbackManager`. This system uses More Mountains FEEL for haptics/screen shake while preserving `SurfaceMaterial` logic for audio/vfx selection.

### Setup (Automated)
Instead of manually creating objects, use the provided Editor Tool:
1. **Menu:** Go to `Tools > DIG > Setup Gameplay Feedback`.
2. **Result:** This will automatically:
    - Create the `GameplayFeedbackManager` singleton if missing.
    - Find and assign the `SurfaceMaterialRegistry`.
    - Create child GameObjects for all feedback types (`Footsteps`, `Jump`, etc.).
    - Add `MMF_Player` components and pre-configure them with Audio/VFX drivers.
    - Link everything together.

### Manual Configuration (Optional)
After running the tool, you can customize the feedbacks:
- **Audio:** The `MMF_Sound` feedback is pre-configured to receive surface clips. You can adjust volume/pitch ranges here.
- **VFX:** The `MMF_ParticlesInstantiation` feedback is pre-configured. You can adjust offsets/scaling here.
- **Juice:** 
    - **Screen Shake:** `MMF_CinemachineImpulse` is added to Jumps/Lands. Ensure your Camera has an Impulse Listener.
    - **Squash & Stretch:** `MMF_Wiggle` is added. **Important:** You must add an `MMWiggle` component to your Player Visuals and assign it to the `TargetWiggle` field on the feedback.
- **Add More:** You can freely add Haptics, Screen Shakes, or Wiggle feedbacks to any of the `MMF_Player` components to enhance the feel.

