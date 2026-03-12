# Setup Guide: Opsive Prefab Migration (EPIC 14.31)

This guide explains how to replace legacy Opsive components with their new ECS-compatible equivalents. Use this when updating Prefabs in `Assets/Prefabs/Opsive` or creating new environment assets.

## 1. Gravity Zones
**Replaces:** `SphericalGravityZone.cs`, `GravityZone.cs`
**Purpose:** Creates a spherical area that pulls the player/objects towards its center (e.g., planets, gravity wells).

### Setup Instructions
1. Select the GameObject that acts as the Gravity Zone.
2. **Remove** any existing Opsive `SphericalGravityZone` or `GravityZone` components.
3. **Add Component** -> `Gravity Zone Authoring`.
4. Configure settings:
   - **Radius:** Size of the zone (visualized by blue Gizmo).
   - **Strength:** Gravity force (Standard earth gravity is ~9.81).
   - **Falloff:**
     - `0`: Linear falloff (weaker at edges).
     - `1`: Constant strength throughout.

> [!NOTE]
> Ensure the GameObject has a standard Unity Collider (Trigger) if you want to use Unity's event system, though the ECS `GravityZoneSystem` uses spatial queries independent of the collider for gravity calculation.

---

## 2. Tutorial Triggers
**Replaces:** `DemoTextTrigger.cs`
**Purpose:** Displays a UI message when the player enters a zone.

### Setup Instructions
1. Select the Trigger GameObject.
2. **Remove** `DemoTextTrigger`.
3. **Add Component** -> `Tutorial Trigger Authoring`.
4. Configure settings:
   - **Header:** Short title (e.g., "Movement").
   - **Message:** The instruction text.
   - **One Time:** Checked = Shows once and disables. Unchecked = Shows every entry.

---

## 3. Ability Unlocks
**Replaces:** `StateTrigger.cs` (when used for unlocking abilities)
**Purpose:** Unlocks specific player capabilities (like Jetpack) when entering an area.

### Setup Instructions
1. Select the Trigger GameObject (usually the item pickup or zone).
2. **Remove** `StateTrigger`.
3. **Add Component** -> `Ability Unlock Authoring`.
4. Configure settings:
   - **Ability:** Select the ability to unlock (e.g., `Jetpack`, `Sprint`, `Weapon`).

---

## 4. General Triggers
**Replaces:** Generic Opsive Triggers
**Purpose:** Marks a collider to be baked as a generic "Zone Trigger" for custom ECS logic.

### Setup Instructions
1. **Add Component** -> `Zone Trigger Authoring`.
2. **Type:** Select the category (Generic, Damage, etc.).
   - *Note: Gravity, Tutorial, and AbilityUnlock components automatically add this, so you don't need it if you're using those.*

## Troubleshooting
- **Gizmos not showing?** Ensure Gizmos are toggled ON in the Scene View.
- **Player not affected?** Ensure the Player has a `GravityOverride` component (added by default to Player Prefab in this update).

---

## 5. Moving Platforms
**Replaces:** `MovingPlatform.cs` (Opsive)
**Purpose:** Handles simple moving platforms (elevators, floating islands) that follow a set of waypoints and carry the player.

### Setup Instructions
1. Select the Platform GameObject (root object).
2. **Remove** `MovingPlatform` (Opsive).
3. **Add Component** -> `Waypoint Platform`.
   - **Properties:**
     - `Waypoints`: Drag in child Transforms (e.g. `Waypoint1`, `Waypoint2`) or leave empty to auto-find children named "Waypoint*".
     - `Speed`: Platform movement speed.
     - `Wait Time`: Pause duration at each waypoint.
4. **Add Component** -> `Moving Platform Authoring`.
   - **Purpose:** Bakes the physics velocity data so the ECS Character Controller can "ride" it.
   - **Properties:**
     - `Sudden Stop Threshold`: Keeps player attached during deceleration.

> [!TIP]
> Ensure the Platform has a **Rigidbody** (IsKinematic = True) or correct ECS Physics setup so it pushes characters.

---

## 6. Spawn Points
**Replaces:** `SpawnPoint.cs` (Opsive)
**Purpose:** Defines where players or AI can spawn.

### Setup Instructions
1. Select the Spawn Point object.
2. **Remove** `SpawnPoint` (Opsive).
3. **Add Component** -> `Spawn Point Authoring`.
4. Configure settings:
   - **Group ID:** Use `0` for default, or specific numbers for localized respawns (e.g., Checkpoints).

---

## 7. Surface Materials for FX
**Replaces:** `SurfaceIdentifier.cs`
**Purpose:** Tags an object as "Wood", "Metal", "Flesh", etc., for Footstep and Bullet Impact effects.

### Setup Instructions
1. Select the GameObject with the visual mesh/collider.
2. **Remove** `SurfaceIdentifier`.
3. **Add Component** -> `Surface Identifier Authoring`.
4. Configure settings:
   - **Type:** Select the material type from the dropdown.
