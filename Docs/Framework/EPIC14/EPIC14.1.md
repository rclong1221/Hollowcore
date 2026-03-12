# EPIC14.1 - Animator Hookups (Proof of Concept)

**Status:** In Progress (Hardcoded Implementation)
**Target File:** `Assets/Art/AddOns/Climbing/ClimbingDemo.controller`
**Goal:** Hook up Magic, Dual Pistol, Sword, and Shield animations by aligning C# logic with this precise Animator structure.

> **Note:** This epic uses hardcoded constants for rapid prototyping. EPIC14.3 will refactor to data-driven configuration.

---

## EPIC14 Roadmap

| Epic | Name | Purpose | Status |
|:-----|:-----|:--------|:-------|
| **14.1** | Animator Hookups | Get animations WORKING with hardcoded values | In Progress |
| **14.2** | Equipment System | Off-hand slot support for dual-wield/shield | Planned |
| **14.3** | Data-Driven Refactor | `ItemAnimationConfig` component for scalability | Planned |
| **14.4** | New Content | Add weapons/spells without code changes | Planned |

---

## 1. Animator Parameters (Complete List)
*Verified via forensic scan. Case-sensitive.*

### Weapon/Item Logic
| Parameter | Type | Default | Usage |
| :--- | :--- | :--- | :--- |
| `Slot0ItemID` | Int | 0 | **Main Hand Item ID**. Drivers entry to weapon sub-states. |
| `Slot1ItemID` | Int | 0 | **Off Hand Item ID**. Triggers Dual Wield/Shield logic. |
| `Slot0ItemStateIndex` | Int | 0 | **Action State**. 0=Idle, 1=Equip, 2=Aim, 3=Use/Fire, 4=Sec. Use, 6=Reload. |
| `Slot0ItemSubstateIndex` | Int | 0 | **Variations**. Spell types (Magic), Combo steps (Sword), Attack types. |
| `Slot1ItemStateIndex` | Int | 0 | **Off Hand Action**. 0=Idle, 1=Equip, 2=Aim, 3=Block/Use. |
| `Slot0ItemStateIndexChange` | Trigger | - | **Critical Trigger**. Must fire alongside `StateIndex` updates to force transitions. |
| `AbilityIndex` | Int | 0 | **Generic Abilities**. Used for Shield Bubble, etc. |
| `AbilityChange` | Trigger | - | **Ability Trigger**. Forces ability transitions. |

### Locomotion & State
| Parameter | Type | Usage |
| :--- | :--- | :--- |
| `Speed` | Float | Blend Tree driver (0-1). |
| `Height` | Float | Jump/Fall height logic. |
| `Moving` | Bool | Moving vs Idle. |
| `Aiming` | Bool | **Critical**. Separates Hip vs ADS states. |
| `MovementSetID` | Int | Animation style set (0=Combat, 1=Adventure, etc). |
| `Yaw`, `Pitch` | Float | Look offsets. |

---

## 2. Layer Structure
*Forensic verification of layer indices and purpose.*

| Index | Name | Blending | Mask | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| 0 | Base Layer | Override | None | Locomotion, Jump, Climb. |
| 1 | Left Hand Layer | Override | UpperBody | IK/Hand overrides. |
| 2 | Right Hand Layer | Override | UpperBody | IK/Hand overrides. |
| 3 | Arms Layer | Override | UpperBody | Generic arm actions. |
| **4** | **Upperbody Layer** | **Override** | **UpperBody** | **MAIN WEAPON LOGIC. This is the target layer.** |
| 5 | Left Arm Layer | Additive | LeftArm | Off-hand support. |
| 6 | Right Arm Layer | Additive | RightArm | Main-hand constraints. |
| 11 | Full Body Layer | Override | None | Death, Revive, Heavy Interactions. |

---

## 3. Unhooked Systems: Deep Dive & Roadmap

### A. Magic (Item Set 61-65)
**Status:** Valid Sub-State Machine found. Default state is **Aggressive**.
**Parent Layer:** `Upperbody Layer` -> `Magic` Sub-State Machine (ID: `1107227305321281938`)

#### Internal State Map
*   **Default State:** `Fireball Light` (ID: `1102447647165723732`)
    *   *Behavior:* Entering Magic immediately preps a light fireball state.
*   **`Fireball Heavy`** (ID: `1102931094135222906`)
    *   *Logic:* Likely `Slot0ItemStateIndex == 3` (Use) + High Substate?
*   **`Particle Stream Start`** (ID: `1102705456553839610`)
*   **`Particle Stream`** (Loop) (ID: `1102331485392378110`)
*   **`Particle Stream End`** (ID: `1102360315761630120`)
    *   *Logic:* Continuous beam spell. Needs `Slot0ItemStateIndex == 3` to start, `== 4` or `0` to end.
*   **`Heal`** (ID: `1102160214678373132`)
*   **`Ricochet`** (ID: `1102869516284965044`)
*   **`Shield Bubble Start`** (ID: `1102204776720064476`)
    *   *Note:* Magic has its OWN shield bubble varation!

#### Implementation Requirements
1.  **Bridge Code (`WeaponEquipVisualBridge.cs`)**:
    *   **Mapping:** Must set `Slot0ItemID` to `61` (or 62-65) when Magic is equipped.
    *   **Trigger:** Must fire `Slot0ItemStateIndexChange` on equip.
2.  **Magic Action Component**:
    *   **Casting:** Must drive `Slot0ItemStateIndex` to `3` (Use) for attacks.
    *   **Spell Selection:** Must drive `Slot0ItemSubstateIndex` to differentiate:
        *   0/1: Fireball Light/Heavy
        *   2: Particle Stream
        *   3: Ricochet/Heal
3.  **Movement Lock During Casting** (Opsive-style):
    *   **Design:** When casting channeled spells (Particle Stream), movement input is blocked.
    *   **Mechanism:** Set `_magicCastingLockMovement = true` during `HandleMagicInput()` when `_magicCasting == true`.
    *   **Effect:** The character cannot move while casting. Legs do not animate (Full Body Layer override).
    *   **Optional:** Movement input can cancel the cast (configurable via `CancelCastOnMove` flag).

### B. Dual Pistol (Item ID 2)
**Status:** Structure uses an **Exit Transition** architecture. Logic is split between Sub-SM and Parent SM.
**Parent Layer:** `Upperbody Layer` -> `Dual Pistol` Sub-State Machine (ID: `1107220818254364462`)

#### Internal State Map
*   **Default State:** `Idle` (ID: `1102989482549485932`)
    *   *Entry Condition:* `Slot1ItemID == 2` (Checks LEFT HAND item).
    *   *Implication:* You must equip a pistol in the **Left Hand** to trigger this dual-wield state.
*   **`Aim`** (ID: `1102532295956771390`)
    *   *Logic:* Activated when `Aiming == true`.
    *   *Fire Logic:* Transitions **OUT** of this state via `Exit` to the parent `Body` layer to play the `Fire` animation (ID: `1102102057562668240`, named `Fire` but lives in `Body` SM).
*   **`Reload`** (ID: `1102492227128764452`)
*   **`Equip From Idle`** / **`Unequip From Idle`**
*   **`Equip From Aim`** / **`Unequip From Aim`**
*   **`Drop`**

#### Implementation Requirements
1.  **Bridge Code (`WeaponEquipVisualBridge.cs`)**:
    *   **Crucial Fix:** Must read the **Left Hand** item. If it is ID 2, set `Slot1ItemID = 2`. Currently, it likely only sets `Slot0ItemID`.
    *   **Dual Wield:** Ensure `Slot0ItemID` is also 2 (Right Hand).
2.  **Firing Logic**:
    *   The bridge/action must handle `Aiming` state correctly. To fire, it might need to momentarily drop `Aiming` or use `Slot0ItemStateIndex == 3` which the `Body` layer picks up via AnyState or Exit transition.

### C. Shield (Item ID 26)
**Status:** Dedicated Sub-State Machine found. Simple, robust logic.
**Parent Layer:** `Upperbody Layer` -> `Shield` Sub-State Machine (ID: `1107273691241051120`)

#### Internal State Map
*   **Default State:** `Idle` (ID: `1102513299117222244`)
*   **`Equip From Idle`** (ID: `1102178693023259674`)
*   **`Unequip From Idle`** (ID: `1102008848540767586`)
*   **`Drop`** (ID: `1102480789522871070`)

#### Implementation Requirements
1.  **Bridge Code**:
    *   **Slot 1 Mapping:** Shield is an off-hand item. Must set `Slot1ItemID = 26`.
2.  **Action Logic**:
    *   **Blocking:** Likely driven by `Slot1ItemStateIndex`. Set to `2` (Aim) or `3` (Use) to trigger block/bash logic if transitions exist (not fully mapped, but `Idle` is safe default).

### D. Sword (Item ID 25)
**Status:** Partially Handled by Editor Tool (`SwordStateAdder.cs`).
**Parent Layer:** `Upperbody Layer` -> `Sword` Sub-State Machine.

#### Internal State Map
*   **Equip:** Driven by `Slot0ItemID == 25`.
*   **Attacks:** `Attack 1 Light`, `Attack 2 Light`.
*   **Combos:** Driven by `Slot0ItemSubstateIndex`.

#### Implementation Requirements
1.  **Bridge Code**:
    *   **ID Mapping:** Ensure `SlotItemIDs` array includes 25.
    *   **Combo Driver:** `MeleeAction` must increment `Slot0ItemSubstateIndex` (0 -> 1 -> 2) on subsequent clicks to advance the combo chain.

---

## 4. Immediate Action Items (Code)

### 1. `WeaponEquipVisualBridge.cs`
*   [ ] **Update `SlotItemIDs`**: Add `25` (Sword), `61` (Magic), `2` (Pistol), `26` (Shield).
*   [ ] **Implement Off-Hand Logic**: Add code to check `AspectArmor.EquipSlots` for the *secondary* slot (index 1?) and set `Slot1ItemID` accordingly. This is **blocking** for Dual Pistol and Shield.

### 2. New Component `MagicAction`
*   [ ] Create component to sit on Magic Item Entity.
*   [ ] On Fire (Input), set `Slot0ItemStateIndex = 3`.
*   [ ] Allow switching "spells" to change `Slot0ItemSubstateIndex` (0=Fireball, 2=Beam, etc).

### 3. New Component `ShieldAction`
*   [ ] Create component for Shield Item Entity.
*   [ ] On Right Click (Aim), set `Slot1ItemStateIndex = 2` (or `AbilityIndex` if it uses the Ability layer for Bubble).
