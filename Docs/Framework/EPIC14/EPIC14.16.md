# EPIC 14.16: DIG Asset Pipeline (D.A.P.)

## 1. Objective
Refactor the weapon and character asset pipeline to solve the "N*M Scaling Problem" (N Characters x M Weapons) while maintaining visual precision, IK fidelity, and zero downtime for the development team.

## 2. The Problem
Currently, weapon positioning relies on specific `Parent` Transform hierarchies (e.g., `RightHand/Items/PistolParent`) on *every* character prefab.
*   **Scaling Issue**: Adding a new character requires manually creating and positioning 15+ parent objects.
*   **Maintenance**: Adjusting a weapon's grip requires updating it on every character.
*   **Networking**: Excessive ECS entity overhead for static transform hierarchies.

## 3. The Solution Architecture

### 3.1. The Socket System (Standardization)
Instead of weapon-specific parents, characters will have standardized **Sockets**.
*   **MainHand_Socket**: Located at the center of the palm.
*   **OffHand_Socket**: Located at the center of the off-hand palm.
*   **Back_Socket**: Standard spine mount point.

### 3.2. Universal Grip (Data-Driven Offsets)
Positioning logic moves from the **Character Hierarchy** to the **Weapon Prefab**.
*   **Authoring**: We introduce `ItemGripAuthoring` on weapon prefabs.
*   **Data**: Stores `Position` and `Rotation` offsets relative to a standard "Ghost Hand".
*   **Runtime**: The equipping system applies `Socket.Position + Weapon.Offset`.

### 3.3. Embedded IK Targets
IK Targets move from the **Character Hierarchy** (`WeaponParentConfig`) to the **Weapon Prefab**.
*   **Structure**: Weapons will have a child Transform named `LeftHandAttach` (or similar).
*   **Runtime**: The `WeaponEquipVisualBridge` will snap the Left Hand IK effector to this transform.

---

## 4. Implementation Plan & Deliverables

### Phase 1: Tooling (The "Visual Foundation")

#### 1. The Socket Rigger
A wizard that automates character setup.
*   **Input**: Character GameObject.
*   **Action**: Finds Humanoid Bones (RightHand, LeftHand, Spine).
*   **Output**: Creates `Socket` GameObjects at the correct hierarchy depth.
*   **Safeguard**: Prevents duplicate sockets.

#### 2. The Alignment Bench
A visual editor tool for authoring weapon offsets.
*   **View**: Renders a "Ghost Hand" (Wireframe mesh) at (0,0,0).
*   **Action**: Development drags a weapon prefab into the scene.
*   **Edit**: Developer moves/rotates the weapon to fit the Ghost Hand.
*   **Save**: Tool writes the inverse transform to `ItemGripAuthoring` on the prefab.

### Phase 2: Runtime Bridge (Refactor w/ Safety)

We will modify `WeaponEquipVisualBridge.cs` to support a **Hybrid/Fallback Mode**. This ensures existing content does NOT break while we migrate.

**Logic Flow:**
1.  **Try Universal**: Does the Character have a `Socket_MainHand`?
2.  **Try Legacy**: If NO, look for `PistolParent` / `WeaponParentConfig`.
3.  **Apply**:
    *   If Universal: Parenting to Socket. Apply `ItemGripAuthoring` offset locally.
    *   If Legacy: Parent to `PistolParent`. (Offset is 0).

**IK Logic Flow:**
1.  **Try Embedded**: Does the Weapon instance have a child named `LeftHandAttach`?
2.  **Try Config**: If NO, check `WeaponParentConfig.LeftHandIKTarget`.
3.  **Apply**: Set `HandIK.LeftHandIKTarget`.

---

## 5. Migration Strategy (Zero Downtime)

We will not break the game. Migration will happen per-weapon or per-character.

1.  **Step 1**: Implement `SocketAuthoring` and updated `WeaponEquipVisualBridge`.
2.  **Step 2**: Create **Alignment Bench**.
3.  **Step 3 (Pilot)**: Pilot on *Assault Rifle*.
4.  **Step 4 (Rollout)**: Migrate remaining inventory.

## 6. Inventory for Migration

The following items are currently managed by the Legacy Parent system and must be processed through the **Alignment Bench**:

### Category: Shooter (Opsive Demo)
1.  **Assault Rifle**
    *   `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/AssaultRifle/AssaultRifleWeapon.prefab`
    *   `Assets/Prefabs/Items/Converted/AssaultRifleWeapon_ECS.prefab`
2.  **Pistol**
    *   `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/Pistol/PistolWeaponBase.prefab` (and L/R variants)
    *   `Assets/Prefabs/Items/Converted/PistolWeaponBase_ECS.prefab`
3.  **Sniper Rifle**
    *   `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Shooter/SniperRifle/SniperRifleWeapon.prefab`
    *   `Assets/Prefabs/Items/Converted/SniperRifleWeapon_ECS.prefab`
4.  **Shotgun** (Based on `SlotItemIDs` mapping, ID 3)
5.  **Rocket Launcher** (ID 6)
6.  **Grenade** (ID 41)

### Category: Melee
1.  **Sword**
    *   `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Melee/Sword/SwordWeapon.prefab`
    *   `Assets/Prefabs/Items/Converted/SwordWeapon_ECS.prefab`
2.  **Katana**
    *   `Assets/OPSIVE/com.opsive.ultimatecharactercontroller/Samples/Demo/Prefabs/Items/Melee/Katana/KatanaWeapon.prefab`
    *   `Assets/Prefabs/Items/Converted/KatanaWeapon_ECS.prefab`
3.  **Knife** (ID 23)

## 7. Migration Checklist
- [ ] **Assault Rifle**: Offset Baked? `[ ]` IK Target Added? `[ ]`
- [ ] **Pistol**: Offset Baked? `[ ]` IK Target Added? `[ ]`
- [ ] **Sniper Rifle**: Offset Baked? `[ ]` IK Target Added? `[ ]`
- [ ] **Shotgun**: Offset Baked? `[ ]` IK Target Added? `[ ]`
- [ ] **Rocket Launcher**: Offset Baked? `[ ]` IK Target Added? `[ ]`
- [ ] **Sword**: Offset Baked? `[ ]` IK Target Added? `[ ]`
- [ ] **Katana**: Offset Baked? `[ ]` IK Target Added? `[ ]`
- [ ] **Knife**: Offset Baked? `[ ]` IK Target Added? `[ ]`

## 8. Networking Implications
*   **Server**: Sockets are baked into `BonePoint` entities. Grip offsets are baked into `ItemDefinition` blobs.
*   **Bandwidth**: No runtime cost. All static data.
