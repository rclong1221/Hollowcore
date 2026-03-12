# EPIC 14.31: Opsive Prefab ECS Migration

## Goal
To systematically analyze and migrate the contents of `Assets/Prefabs/Opsive` into a pure ECS/DOTS pipeline. Currently, these prefabs contain "Invisible Walls" caused by Logic Triggers (Monobehaviours) that are being baked as static physics bodies, and rely on legacy scripts (`StateTrigger`, `GravityZone`) that do not function in ECS.

## Analysis of "Invisible Walls"
The "Invisible Walls" are primarily caused by:
1.  **Logic Triggers:** Large `BoxCollider` or `SphereCollider` components marked `IsTrigger` used for logic (e.g., "Entering this zone enables Double Jump").
2.  **Baking Mismatch:** The default ECS baking process often treats these as static physics bodies if the accompanying Logic MonoBehaviour is unknown or ignored, creating solid barriers.
3.  **Invisible Geometry:** Some prefabs use meshes without renderers for collision optimization, which need to be explicitly handled or removed.

---

## 1. Component Porting Plan

### A. Gravity Zones
*   **Source:** `SphericalGravityZone.cs` (GUID: `33f9...`)
*   **Prefab:** `DynamicGravity.prefab`
*   **ECS Replacement:**
    *   **Component:** `GravityZoneComponent` (Float3 Center, Float Radius, Float Strength).
    *   **System:** `GravityZoneSystem` (Jobified distance check).
    *   **Authoring:** `GravityZoneAuthoring` (Replaces `SphericalGravityZone`).

### B. Tutorial/UI Triggers
*   **Source:** `DemoTextTrigger.cs` (GUID: `296e...`)
*   **Prefab:** `Locomotion.prefab` (e.g., "Double Jump Description").
*   **ECS Replacement:**
    *   **Component:** `TutorialTriggerComponent` (BlobString Message, Bool OneTime).
    *   **System:** `TutorialTriggerSystem` (Toggles UI when player enters).
    *   **Authoring:** `TutorialTriggerAuthoring`.

### C. State/Ability Triggers
*   **Source:** `StateTrigger.cs` (GUID: `64f2...`)
*   **Prefab:** `Locomotion.prefab`, `Agiility.prefab` (e.g., "Enable Double Jump", "Disable Stop Anim").
*   **ECS Replacement:**
    *   **Component:** `AbilityUnlockZone` (Enum AbilityID).
    *   **System:** `AbilityUnlockSystem` (Grants abilities while inside).
    *   **Authoring:** `AbilityUnlockAuthoring`.

### D. Moving Platforms
*   **Source:** `MovingPlatform.cs` (Various scripts).
*   **Prefab:** `MovingPlatforms.prefab`.
*   **ECS Replacement:**
    *   **Component:** `MovingPlatform` (already exists in project? Verify parity).
    *   **Authoring:** Ensure Opsive's paths (Waypoints) specific logic is converted to our `MovingPlatform` system.

---

## 2. Prefab Migration & Cleanup Tasks
For each prefab in `Assets/Prefabs/Opsive`, perform the following:

- [ ] **`Locomotion.prefab`**
    - [ ] Replace `DemoTextTrigger` with `TutorialTriggerAuthoring`.
    - [ ] Replace `StateTrigger` with `AbilityUnlockAuthoring` (Double Jump).
    - [ ] Ensure all Trigger Colliders are baked with `PhysicsShapeBehavior.Trigger` or `IsTrigger` flag respected.

- [ ] **`DynamicGravity.prefab`**
    - [ ] Replace `SphericalGravityZone` with `GravityZoneAuthoring`.
    - [ ] Ensure the huge sphere collider is a Trigger.

- [ ] **`MovingPlatforms.prefab`**
    - [ ] Replace platform logic with ECS `MovingPlatform` components.
    - [ ] Ensure "Boat" and "Elevator" colliders are solid but movable (Kinematic).

- [ ] **`Agiility.prefab`** (Check spelling: *Agility*)
    - [ ] Identify and replace "Hang" or "Ledge" zones.

- [ ] **`ClimbingRoom.prefab`**
    - [ ] Verify "Free Climb" zones (if any) or ledge markers.

- [ ] **`Drive.prefab` / `Ride.prefab`**
    - [ ] Identify vehicle interaction zones.

- [ ] **General Cleanup**
    - [ ] Delete valid-but-unused `MeshRenderer`s (optimization).
    - [ ] Ensure all nested GameObjects have appropriate Authoring components.
    - [ ] **Final Step:** Verify no `Assets/OPSIVE` scripts remain referenced.

---

## 3. Implementation Priorities
1.  **Stop the Walls:** Create a generic `ZoneTriggerAuthoring` that simply sets `IsTrigger` correctly for ECS, even if logic is missing. This unblocks movement.
2.  **Port Gravity:** Essential for `DynamicGravity`.
3.  **Port Tutorial:** Essential for explaining mechanics.
