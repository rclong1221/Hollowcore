# EPIC 14.17: Item VFX Architecture (Industry Standard)

## Executive Summary
This specific implementation adopts the **"Driver-Receiver" Pattern** used in AAA shooters (Call of Duty, Destiny). It creates a strict separation between **Timing** (Animation), **Assets** (Prefab), and **Logic** (Code), ensuring high visual fidelity without hardcoding effects.

## The Architecture

### 1. The Strategy: Hybrid Component + Events
*   **Driver (Timing):** `AnimationEvents` in `.anim` clips drive the timing. This ensures perfect frame-sync.
*   **Receiver (Assets):** `ItemVFXAuthoring` component on the Weapon Prefab holds the references.
*   **Bridge (Logic):** A lightweight Relay connects the two.

### 2. The Component: `ItemVFXAuthoring`
This is the "Smart Receiver" that sits on every weapon prefab.

**Key Features (Industry Standard):**
1.  **Attached Spawning (Local Space):**
    *   *Examples:* Muzzle Flash, Energy Charge, Barrel Heat.
    *   *Behavior:* Moves *with* the weapon.
2.  **Detached Spawning (World Space):**
    *   *Examples:* Smoke Puffs, Shell Ejections, Rocket Backblast.
    *   *Behavior:* Spawns at the socket but *stays* in the world (does not drag with gun movement).
3.  **Variability (Randomization):**
    *   *Rotation:* Random Z-rotation (0-360) to prevent "stamp effect" on muzzle flashes.
    *   *Scale:* Random scale (0.9-1.2) for organic feel.
4.  **Pooling (Performance):**
    *   *Future Proofing:* System should use an Object Pool instead of `Instantiate`/`Destroy`.

## Data Structure

```csharp
[Serializable]
public struct VFXDefinition
{
    [Tooltip("Event ID sent by Animation (e.g., 'Fire', 'ShellEject').")]
    public string ID;

    [Header("Assets")]
    public ParticleSystem AttachedParticle; // for Muzzle Flashes
    public GameObject DetachedPrefab;       // for Smoke/Shells

    [Header("Configuration")]
    public bool SpawnDetachedInWorld;       // TRUE for Smoke, FALSE for Trails
    public bool RandomizeRotation;          // 0-360 deg z-rotation
    public Vector2 ScaleVariation;          // e.g., (0.9, 1.2)
    public Transform SpawnSocket;           // Where to spawn?
    public float AutoDestroyTime;           // Cleanup
}
```

## Integration Workflow

1.  **Animator Work:**
    *   Animator opens `Attack.anim`.
    *   Adds Animation Event at Frame 12: `function: OnItemFire`, `string: "Fire"`.
2.  **Code Flow:**
    *   `WeaponAnimationEventRelay` catches `OnItemFire`.
    *   It queries `WeaponEquipVisualBridge.CurrentItemVFX`.
    *   It calls `CurrentItemVFX.PlayVFX("Fire")`.
3.  **Visual Result:**
    *   `ItemVFXAuthoring` looks up ID "Fire".
    *   Found: "MuzzleFlash" Particle.
    *   Action: `Stop()` -> `Randomize Rotation` -> `Play()`.

## Implementation Tasks

### Phase 1: Core System (Complete)
- [x] **Create `ItemVFXAuthoring.cs`**
    - [x] Implement struct `VFXDefinition` with Attached/Detached logic.
    - [x] Implement `PlayVFX(id)` with Randomization/Parenting logic.
- [x] **Update `WeaponEquipVisualBridge.cs`**
    - [x] Add `_currentItemVFX` cache field.
    - [x] Update `UpdateWeaponVisuals()` to cache component on equip.
    - [x] Clear cache on unequip/holster.
- [x] **Update `WeaponAnimationEventRelay.cs`**
    - [x] Add `TriggerVFX(id)` helper.
    - [x] Inject calls into `OnAnimatorItemFire`, `OnAnimatorReload`, etc.

### Phase 2: Content Setup (User)
- [ ] **Setup Rifle Prefab**
    - [ ] Add `ItemVFXAuthoring`.
    - [ ] Create Entry: ID="Fire", Attached=MuzzleFlash.
    - [ ] Create Entry: ID="ShellEject", Detached=ShellCasingPrefab, WorldSpace=True.
- [ ] **Verify**
    - [ ] Shoot rifle in-game.
    - [ ] Confirm Muzzle Flash moves with gun.
    - [ ] Confirm Shell Casing separates from gun.

### Phase 3: Optimizations (Future)
- [ ] **Implement Object Pooling**
    - [ ] Replace `Instantiate` with `PoolManager.Spawn`.
    - [ ] Replace `Destroy` with `PoolManager.Despawn`.

---

## Magazine Reload Animation System

### Overview (Based on Opsive's GenericReloader Pattern)
The magazine reload system handles the visual choreography of reloading:
1. **Detach Clip** - Magazine moves from weapon to character's hand
2. **Drop Clip** - Old magazine spawned as physics object and dropped
3. **Add Clip** - Fresh magazine appears in hand
4. **Attach Clip** - Magazine moves from hand back to weapon

**Key Transforms:**
- `ReloadableClip` - The magazine mesh on the weapon
- `ReloadableClipAttachment` - Hand bone to parent magazine during animation

**Animation Events (triggered by animations):**
- `OnAnimatorItemReloadDetachClip` - Reparent mag from weapon to hand
- `OnAnimatorItemReloadDropClip` - Spawn physics drop prefab, hide original
- `OnAnimatorItemReactivateClip` - Show fresh magazine on weapon
- `OnAnimatorItemReloadAttachClip` - Reparent mag from hand back to weapon

### Phase 4: Magazine Reload Animations

#### 4.1: Reload Configuration Component
- [x] **Create `WeaponReloadAuthoring.cs`**
    - [x] `Transform MagazineClip` - The magazine mesh transform on weapon
    - [x] `Transform MagazineClipAttachment` - Hand bone for attachment during reload
    - [x] `GameObject DropMagazinePrefab` - Physics prefab spawned when dropping old mag
    - [x] `GameObject FreshMagazinePrefab` - Optional prefab for new magazine (or reuse existing)
    - [x] `bool DetachAttachClip` - Whether to reparent clip during animation
    - [x] `bool ResetClipTransformOnDetach` - Reset local pos/rot when detaching

#### 4.2: Reload VFX Integration
- [ ] **Extend `ItemVFXAuthoring.cs`**
    - [ ] Add VFX Definition for `ReloadMagDrop` (detached prefab with physics)
    - [ ] Add VFX Definition for `ReloadMagInsert` (optional sound/particle)
    - [ ] Leverage existing ECS shell spawning for dropped magazines

#### 4.3: Animation Event Handlers
- [x] **Extend `WeaponAnimationEventRelay.cs`**
    - [x] Add `OnAnimatorItemReloadDetachClip()` - Triggers magazine detach
    - [x] Add `OnAnimatorItemReloadDropClip()` - Spawns physics magazine drop
    - [x] Add `OnAnimatorItemReloadAttachClip()` - Re-attaches magazine to weapon
    - [x] Add `OnAnimatorItemReactivateClip()` - Shows fresh magazine

#### 4.4: Magazine Animation Controller
- [x] **Create `MagazineReloadController.cs`**
    - [x] Cache original parent/position/rotation of magazine
    - [x] `DetachMagazine()` - Reparent mag to hand bone
    - [x] `DropMagazine()` - Instantiate physics drop prefab, hide original
    - [x] `ShowFreshMagazine()` - Enable fresh mag visual on hand
    - [x] `AttachMagazine()` - Reparent back to weapon, restore transform
    - [x] Support first-person and third-person perspectives

#### 4.5: Physics Drop Integration
- [ ] **Leverage Existing Shell Spawning**
    - [ ] Register magazine drop prefabs in `ShellPrefabRegistryAuthoring`
    - [ ] Or create parallel `MagazinePrefabRegistryAuthoring` if needed
    - [ ] Ensure dropped mags have `ShellPhysicsAuthoring` + Collider

#### 4.6: Animator Setup (Per-Weapon)
- [ ] **Add Animation Events to Reload Clips**
    - [ ] `OnAnimatorItemReloadDetachClip` at detach frame
    - [ ] `OnAnimatorItemReloadDropClip` at drop frame
    - [ ] `OnAnimatorItemReloadAttachClip` at insert frame
- [ ] **Test with Assault Rifle Reload Animation**

### Phase 5: Advanced Features (Optional)
- [ ] **Chambering Animation**
    - [ ] Support empty vs tactical reload (chamber check)
    - [ ] Different substates for empty/partial magazine
- [ ] **Dual Magazine Reload**
    - [ ] Support weapons with dual/extended mags
- [ ] **Ammo Counter Integration**
    - [ ] Sync visual magazine fullness with ammo count

---

## Phase 6: Firearm Setup Automation

### Problem Statement
Adding new firearms currently requires:
- Manual setup of multiple components (ECS authoring, visual bridge, animation relay, VFX, reload controller)
- Animation clips must have correctly-named events (fragile, no validation)
- Missing any component causes subtle runtime bugs (e.g., `IsReloading` stuck forever)
- No editor-time validation of weapon configuration

### Solution: 4-Part Automation System

#### 6.1: Weapon Template ScriptableObjects
- [x] **Create `WeaponTemplateAsset.cs`**
    - [x] Define base templates: `Pistol`, `Rifle`, `Shotgun`, `SMG`, `Sniper`, `LMG`
    - [x] Default values per template:
        - [x] `ClipSize` (Pistol: 12, Rifle: 30, Shotgun: 8, etc.)
        - [x] `FireRate` (rounds per second)
        - [x] `ReloadTime` (seconds)
        - [x] `AutoReload` (bool)
        - [x] `FireMode` (Semi, Auto, Burst, BoltAction)
    - [x] VFX preset references (muzzle flash, shell casing prefab)
    - [x] Audio preset references (fire sound, reload sounds)
- [x] **Create Template Assets**
    - [x] `Assets/Data/WeaponTemplates/PistolTemplate.asset` (via menu: Tools > DIG > Create Default Weapon Templates)
    - [x] `Assets/Data/WeaponTemplates/RifleTemplate.asset`
    - [x] `Assets/Data/WeaponTemplates/ShotgunTemplate.asset`
    - [x] `Assets/Data/WeaponTemplates/SMGTemplate.asset`
    - [x] `Assets/Data/WeaponTemplates/SniperTemplate.asset`
    - [x] `Assets/Data/WeaponTemplates/LMGTemplate.asset`
- [x] **Add "Apply Template" button in Inspector**
    - [x] Quick template buttons (Pistol, Rifle, SMG, Shotgun, Sniper, LMG)
    - [x] When clicked, populates component values from selected template

#### 6.2: Weapon Prefab Validation Tool
- [x] **Create `WeaponPrefabValidator.cs` (Editor Script)**
    - [x] Menu: `Tools > DIG > Validate Weapon Prefab`
    - [x] Validation Checks:
        - [x] Has `WeaponAmmoAuthoring` with valid values (ClipSize > 0, ReloadTime > 0)
        - [x] Has `ItemVFXAuthoring` with at least "Fire" entry
        - [x] Has `WeaponAnimationEventRelay` (or relay exists on character)
        - [x] If reloadable: Has `MagazineReloadController` or compatible setup
        - [x] Transform checks for Muzzle and EjectionPort sockets
    - [x] Output: 
        - [x] Green checkmarks for valid items
        - [x] Yellow warnings for missing optional components
        - [x] Red errors for critical missing pieces
    - [x] Optional: "Fix" buttons that auto-add missing components with defaults
- [ ] **Play Mode Validation** (Future)
    - [ ] On first equip, log warnings if weapon is misconfigured
    - [ ] Catch "IsReloading stuck" scenario with specific error message

#### 6.3: Auto-Wire Equipment Manager
- [x] **Update `WeaponEquipVisualBridge.cs`**
    - [x] On equip, auto-discover components on item prefab:
        - [x] `ItemVFXAuthoring` (already cached as `_currentItemVFX`)
        - [x] `MagazineReloadController` (auto-cache as `_currentMagazineController`)
    - [x] Clear caches on unequip/holster
    - [x] Debug logging when components are discovered
- [x] **Update `WeaponAnimationEventRelay.cs`**
    - [x] Auto-discover `WeaponEquipVisualBridge` if not assigned
    - [x] `AutoDiscoverVisualBridge()` searches parent, root, then deferred
    - [x] `EnsureVisualBridge()` called before triggering events
    - [x] Improved warning messages for missing bridge

#### 6.4: Animation Event Generator Tool
- [x] **Create `AnimationEventGenerator.cs` (Editor Script)**
    - [x] Menu: `Tools > DIG > Animation Event Generator`
    - [x] Input: Select animation clip(s) in Project window
    - [x] Presets for animation types:
        - [x] **Fire Animation**: Adds `OnAnimatorItemFire` at 5%, `ShellEject` at 15%, `FireComplete` at 95%
        - [x] **Reload Animation**: Adds `ReloadStart` at 0%, `InsertAmmo` at 70%, `ReloadComplete` at 95%
        - [x] **Magazine Reload**: Adds full sequence (DetachClip, DropClip, InsertAmmo, AttachClip, ReloadComplete)
        - [x] **DryFire Animation**: Adds `OnAnimatorDryFire` at 5%
        - [x] **Custom**: User-defined events
    - [x] Frame Position Options:
        - [x] Manual: User specifies frame number
        - [x] Percentage: User specifies % through animation
    - [x] Event Name Format:
        - [x] Use Opsive-compatible names (`OnAnimatorItemReloadComplete`, etc.)
        - [x] Uses `ExecuteEvent` function with string parameter
    - [x] Preview Mode:
        - [x] Visual timeline showing new (green) vs existing (yellow) events
    - [x] Clear Events button to remove all events from clips
- [x] **Batch Mode**
    - [ ] Select folder of animations
    - [ ] Apply preset to all matching clips (by name pattern: `*_Fire`, `*_Reload`, etc.)

---

## Firearm Setup Guide (Updated)

### Quick Start: Adding a New Firearm

#### Step 1: Prepare Assets
1. Import weapon model (FBX) with proper bone hierarchy
2. Import animations (Fire, Reload, Idle, etc.)
3. Ensure animations have animation events OR use the Animation Event Generator tool

#### Step 2: Use Weapon Template (Recommended)
1. Create new weapon prefab from model
2. Add `WeaponAmmoAuthoring` component
3. Select appropriate template (Pistol, Rifle, etc.) from dropdown
4. Click **"Apply Template"** to populate default values
5. Adjust values as needed (clip size, fire rate, etc.)

#### Step 3: Setup VFX
1. Add `ItemVFXAuthoring` component to weapon prefab
2. Add VFX entries:
   - ID: `Fire` → Attach muzzle flash particle
   - ID: `ShellEject` → Detached shell casing prefab
   - ID: `ReloadInsert` → Optional reload particle/sound
3. Assign spawn sockets (Muzzle, Ejection Port transforms)

#### Step 4: Setup Magazine Reload (if applicable)
1. Add `MagazineReloadController` component
2. Assign:
   - `MagazineClip` → Magazine mesh transform
   - `DropMagazinePrefab` → Physics magazine prefab
   - `MagazineClipAttachment` → Left hand bone

#### Step 5: Validate Prefab
1. Select weapon prefab
2. Run `Tools > DIG > Validate Weapon Prefab`
3. Fix any red errors (critical)
4. Review yellow warnings (optional)
5. Green = Ready to use!

#### Step 6: Add Animation Events (if not present)
1. Select animation clips for the weapon
2. Run `Tools > DIG > Animation Event Generator`
3. Choose preset (Fire, Reload, Magazine Reload)
4. Adjust frame positions if needed
5. Click **"Apply Events"**

#### Step 7: Test In-Game
1. Add weapon to equipment manager / inventory
2. Equip weapon
3. Test firing (muzzle flash, shell eject)
4. Test reload (magazine drop, completion)
5. Verify no console errors about stuck states

### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Can't fire after reload | `IsReloading` stuck | Animation missing `OnAnimatorItemReloadComplete` event |
| No muzzle flash | VFX not triggering | Check `ItemVFXAuthoring` has "Fire" entry with particle assigned |
| Shell ejects during dry fire | VFX not blocked | Ensure `WeaponAnimationEventRelay` checks `CurrentWeaponHasAmmo` |
| Magazine floats in air | Wrong parent | Check `MagazineClipAttachment` is assigned to hand bone |
| Reload never starts | Input not reaching ECS | Check `UseRequest.Reload` is being set from input |

### Animation Event Reference

| Event Name | When to Fire | Purpose |
|------------|--------------|---------|
| `OnAnimatorItemFire` / `Fire` | Frame 0-2 of fire animation | Trigger muzzle flash, shell eject |
| `OnAnimatorItemFireComplete` | End of fire animation | Reset trigger for semi-auto |
| `OnAnimatorItemReloadComplete` | End of reload animation | Clear `IsReloading`, enable firing |
| `OnAnimatorReloadInsertAmmo` | Magazine inserted frame | Transfer ammo from reserve to clip |
| `OnAnimatorItemReloadDetachClip` | Hand grabs magazine | Reparent magazine to hand |
| `OnAnimatorItemReloadDropClip` | Magazine leaves hand | Spawn physics drop prefab |
| `OnAnimatorItemReloadAttachClip` | New magazine inserted | Reparent magazine to weapon |
| `OnAnimatorDryFire` | Attempting fire with no ammo | Play click sound (no VFX) |

