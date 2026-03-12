# Item VFX & Animation System Setup Guide (EPIC 14.17)

This guide covers setting up weapon visual effects and animation-driven actions.

---

## Part 1: VFX Configuration

### 1.1: Add ItemVFXAuthoring Component
1. Open your weapon prefab
2. Add `ItemVFXAuthoring` component to root
3. Configure VFX definitions:

| ID | Type | Description |
|----|------|-------------|
| `Fire` | Muzzle flash at fire point |
| `ShellEject` | Shell casing spawn at eject point |

---

## Part 2: Visual Action System (NEW)

The **WeaponVisualActionController** is a generic system that works for ANY weapon type.
Animation events send commands, the controller executes them.

### 2.1: Add Components

**For ALL weapons:**
- Add `WeaponVisualActionController`

**For magazine-style weapons (optional):**
- Also add `MagazineReloadController` + `WeaponReloadAuthoring`

### 2.2: Configure Visual Parts

In `WeaponVisualActionController`, add entries to the **Parts** list:

| Part ID | Part Transform | Description |
|---------|---------------|-------------|
| `Magazine` | `FirstPersonAssaultRifle/Clip` | Magazine mesh |
| `Rocket` | `RocketMesh` | Rocket projectile |
| `Bolt` | `CrossbowBolt` | Crossbow bolt |

### 2.3: Configure Spawnables

Add entries to the **Spawnables** list:

| Object ID | Prefab | Spawn Point | Description |
|-----------|--------|-------------|-------------|
| `DropMag` | `MagazineDrop.prefab` | Magazine transform | Dropped mag |
| `Casing` | `ShellCasing.prefab` | Eject point | Spent casing |

### 2.4: Configure Reparent Targets (Optional)

For magazine detach/attach animations:

| Target ID | Target Transform |
|-----------|------------------|
| `LeftHand` | *(Set at runtime)* |
| `RightHand` | *(Set at runtime)* |

---

## Part 3: Animation Events

### 3.1: Generic Visual Actions

Animation events use format: `Action:Argument`

| Animation Event String | Effect |
|------------------------|--------|
| `ShowPart:Magazine` | Shows the magazine mesh |
| `HidePart:Magazine` | Hides the magazine mesh |
| `Spawn:DropMag` | Spawns the drop mag prefab |
| `Reparent:Magazine:LeftHand` | Parents magazine to left hand |
| `Restore:Magazine` | Returns magazine to original parent |

**How to add:**
1. Open Animation window (`Ctrl+6`)
2. Select reload clip
3. Scrub to desired frame
4. Add Animation Event:
   - **Function:** `ExecuteEvent`
   - **String:** `ShowPart:Magazine` (etc.)

### 3.2: Magazine Reload Example (Assault Rifle)

| Frame | String Parameter | Effect |
|-------|-----------------|--------|
| ~10% | `Reparent:Magazine:LeftHand` | Mag moves to hand |
| ~30% | `HidePart:Magazine` | Mag disappears |
| ~30% | `Spawn:DropMag` | Physics mag falls |
| ~60% | `ShowPart:Magazine` | New mag visible |
| ~80% | `Restore:Magazine` | Mag back on weapon |

### 3.3: Simple Projectile Example (Crossbow)

| Frame | String Parameter | Effect |
|-------|-----------------|--------|
| Fire | `HidePart:Bolt` | Bolt disappears (fired) |
| Reload | `ShowPart:Bolt` | New bolt appears |

### 3.4: Opsive-Style Events (Legacy Support)

These are also supported for backwards compatibility:

| Event Name | Method Called |
|------------|---------------|
| `OnAnimatorReloadStart` | `StartReload()` |
| `OnAnimatorItemReloadDetachClip` | `DetachMagazine()` |
| `OnAnimatorItemReloadDropClip` | `DropMagazine()` |
| `OnAnimatorItemReactivateClip` | `ShowFreshMagazine()` |
| `OnAnimatorItemReloadAttachClip` | `AttachMagazine()` |
| `OnAnimatorReloadComplete` | `CompleteReload()` |

---

## Part 4: Quick Reference

### Component Summary

| Component | Purpose |
|-----------|---------|
| `ItemVFXAuthoring` | Muzzle flash, shell eject VFX |
| `WeaponVisualActionController` | Generic show/hide/spawn actions |
| `MagazineReloadController` | Magazine-specific logic (optional) |
| `WeaponReloadAuthoring` | Magazine transform config (optional) |

### Event Format Reference

```
ShowPart:PartID       - Show a part
HidePart:PartID       - Hide a part
Spawn:ObjectID        - Spawn physics object
Reparent:PartID:TargetID - Reparent to bone
Restore:PartID        - Restore to original parent
```

---

## Part 5: Concrete Example - AssaultRifleWeapon_ECS

### 5.1: Prefab Hierarchy
```
AssaultRifleWeapon_ECS (root)
├── FirstPersonAssaultRifle
│   ├── Clip                ← FP Magazine mesh
│   ├── MuzzleFlash
│   ├── ShellEjectPoint
│   └── WeaponRig/ClipBone
└── ThirdPersonAssaultRifle
    ├── Clip                ← TP Magazine mesh
    ├── MuzzleFlash
    └── ShellEjectPoint
```

### 5.2: WeaponVisualActionController Setup

**Parts List:**
| # | Part ID | Part Transform |
|---|---------|----------------|
| 0 | `FPMagazine` | `FirstPersonAssaultRifle/Clip` |
| 1 | `TPMagazine` | `ThirdPersonAssaultRifle/Clip` |

**Spawnables List:**
| # | Object ID | Prefab | Spawn Point |
|---|-----------|--------|-------------|
| 0 | `DropMag` | `AssaultRifleClip_ECS.prefab` | `FirstPersonAssaultRifle/Clip` |

**Reparent Targets:**
| # | Target ID | Target Transform |
|---|-----------|------------------|
| 0 | `LeftHand` | *(Set at runtime via code)* |

### 5.3: Animation Events for Reload Clip

Add these events to the Assault Rifle reload animation:

| Frame | Function | String Parameter |
|-------|----------|-----------------|
| 10% | `ExecuteEvent` | `Reparent:FPMagazine:LeftHand` |
| 10% | `ExecuteEvent` | `Reparent:TPMagazine:LeftHand` |
| 30% | `ExecuteEvent` | `HidePart:FPMagazine` |
| 30% | `ExecuteEvent` | `HidePart:TPMagazine` |
| 30% | `ExecuteEvent` | `Spawn:DropMag` |
| 60% | `ExecuteEvent` | `ShowPart:FPMagazine` |
| 60% | `ExecuteEvent` | `ShowPart:TPMagazine` |
| 80% | `ExecuteEvent` | `Restore:FPMagazine` |
| 80% | `ExecuteEvent` | `Restore:TPMagazine` |

### 5.4: Runtime Setup (for LeftHand target)

In your weapon equip code (e.g., `WeaponEquipVisualBridge`):

```csharp
// After equipping weapon, set up the reparent target
var visualController = weapon.GetComponent<WeaponVisualActionController>();
if (visualController != null)
{
    // Find character's left hand bone
    var leftHand = FindBone(characterRoot, "LeftHand", "mixamorig:LeftHand");
    visualController.SetReparentTarget("LeftHand", leftHand);
}
```

### 5.5: ItemVFXAuthoring Setup

| # | VFX ID | Socket |
|---|--------|--------|
| 0 | `Fire` | `FirstPersonAssaultRifle/MuzzleFlash` |
| 1 | `ShellEject` | `FirstPersonAssaultRifle/ShellEjectPoint` |

---

## Part 6: Extending to Other Weapon Types (Launchers, Crossbows, Bows)

### 6.1: Why You DON'T Need Code Changes

The system is **DATA-DRIVEN**, not code-driven.

**Key Insight:** The animation event strings like `ShowPart:Arrow` are **NOT** pre-registered in code. The system simply:
1. Receives any string (e.g., `ShowPart:MyCustomPart`)
2. Parses it into `Command` + `Argument` (e.g., `ShowPart` + `MyCustomPart`)
3. Looks up `MyCustomPart` in the prefab's Parts list
4. Executes the action

**This means:** If you add a part called `Rocket` to your prefab, you can immediately use `ShowPart:Rocket` in animations. No code changes.

### 6.2: Launcher Example

**Prefab Setup:**
```
RocketLauncher_ECS (root)
├── LauncherBody
├── RocketMesh              ← The visible rocket
└── ExhaustPoint            ← Backblast spawn location
```

**WeaponVisualActionController Config:**
| Part ID | Part Transform |
|---------|----------------|
| `Rocket` | `RocketMesh` |

| Spawnable ID | Prefab | Spawn Point |
|--------------|--------|-------------|
| `Backblast` | `BackblastVFX.prefab` | `ExhaustPoint` |

**Animation Events:**
| Frame | String | Effect |
|-------|--------|--------|
| Fire | `HidePart:Rocket` | Rocket disappears (launched) |
| Fire | `Spawn:Backblast` | Exhaust VFX spawns |
| Reload End | `ShowPart:Rocket` | New rocket appears |

### 6.3: Crossbow Example

**Prefab Setup:**
```
Crossbow_ECS (root)
├── CrossbowBody
├── BoltMesh                ← The bolt/arrow
├── StringMesh              ← Bowstring (optional animation)
└── QuiverAttach            ← Where hand grabs bolt
```

**WeaponVisualActionController Config:**
| Part ID | Part Transform |
|---------|----------------|
| `Bolt` | `BoltMesh` |
| `String` | `StringMesh` |

| Spawnable ID | Prefab | Spawn Point |
|--------------|--------|-------------|
| `SpentBolt` | `BoltProjectile.prefab` | `BoltMesh` |

**Animation Events:**
| Frame | String | Effect |
|-------|--------|--------|
| Fire | `HidePart:Bolt` | Bolt disappears |
| Reload 50% | `ShowPart:Bolt` | New bolt appears |
| _(Optional)_ Reload 30% | `Reparent:Bolt:RightHand` | Bolt in hand during reload |
| _(Optional)_ Reload 70% | `Restore:Bolt` | Bolt back on crossbow |

### 6.4: Bow Example

**Prefab Setup:**
```
Bow_ECS (root)
├── BowBody
├── ArrowMesh               ← Nocked arrow
├── BowstringMesh           ← String (animated via Animator)
└── QuiverSocket            ← For hand-to-quiver animation
```

**WeaponVisualActionController Config:**
| Part ID | Part Transform |
|---------|----------------|
| `Arrow` | `ArrowMesh` |

| Reparent Target ID | Target |
|--------------------|--------|
| `RightHand` | *(Set at runtime)* |

**Animation Events:**
| Frame | String | Effect |
|-------|--------|--------|
| Draw Start | `ShowPart:Arrow` | Arrow appears nocked |
| Release/Fire | `HidePart:Arrow` | Arrow disappears (fired) |
| _(Optional)_ Draw | `Reparent:Arrow:RightHand` | Arrow follows aiming |

### 6.5: The Five Universal Commands

These work for ANY weapon without code changes:

| Command | Format | What It Does |
|---------|--------|--------------|
| `ShowPart` | `ShowPart:PartID` | Sets the Part GameObject active |
| `HidePart` | `HidePart:PartID` | Sets the Part GameObject inactive |
| `Spawn` | `Spawn:SpawnableID` | Instantiates the prefab at spawn point |
| `Reparent` | `Reparent:PartID:TargetID` | Parents Part to Target bone |
| `Restore` | `Restore:PartID` | Returns Part to original parent + position |

### 6.6: Legacy Event Bridging

If you're using existing animation clips with Opsive-style events (e.g., `OnAnimatorItemReloadDetachClip`), these are automatically mapped to visual actions for Magazine weapons:

| Legacy Event | Auto-Triggered Actions |
|--------------|------------------------|
| `OnAnimatorItemReloadDetachClip` | `Reparent:TPMagazine:LeftHand`, `Reparent:FPMagazine:LeftHand` |
| `OnAnimatorItemReloadDropClip` | `Spawn:DropMag`, `HidePart:TPMagazine`, `HidePart:FPMagazine` |
| `OnAnimatorItemReloadAttachClip` | `Restore:TPMagazine`, `Restore:FPMagazine` |
| `OnAnimatorItemReactivateClip` | `ShowPart:TPMagazine`, `ShowPart:FPMagazine` |

For **non-magazine weapons** (Bows, Crossbows, Launchers), use the generic `VisualAction` events directly in animations rather than relying on legacy mapping.

---

## Summary: Adding a New Weapon Type

1. **Create prefab** with visible parts as child GameObjects
2. **Add `WeaponVisualActionController`** component
3. **Register Parts** (ID → Transform)
4. **Register Spawnables** if needed (ID → Prefab)
5. **Add animation events** using `ShowPart:YourPartID`, etc.
6. **Done** - no code changes required
