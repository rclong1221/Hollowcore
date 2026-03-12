# Epic 15.5 Setup Guide: Weapon System Completeness

This guide covers the Unity Editor setup for **Swept Melee Hitboxes**, **Projectile Pooling**, **Recoil Patterns**, and **Predicted Hitmarkers**.

---

## 1. Swept Melee Hitboxes (Anti-Tunneling)

Prevents fast-moving melee weapons from passing through enemies between frames.

### Quick Setup

1. Select your **melee weapon prefab** in the Project window
2. Add the **`SweptMeleeAuthoring`** component
3. Choose a **Preset** from the dropdown:
   - `Sword` - Standard one-handed sword
   - `Greatsword` - Large two-handed weapon
   - `Dagger` - Short, fast weapon
   - `Fist` - Unarmed attacks
   - `Spear` - Long reach, thin hitbox
   - `Axe` - Wide head, medium reach
   - `Custom` - Define your own geometry

4. For **Custom** presets:
   - **Tip Offset**: Local position of blade tip (forward is Z)
   - **Handle Offset**: Local position of handle/grip
   - **Capsule Radius**: Width of the swept volume
   - **Max Hits Per Swing**: 0=unlimited, 3=typical cleave

### Testing

1. Enter Play Mode
2. Attack with a fast weapon near an enemy
3. The enemy should be hit even during fast swings
4. Check Console for `[SWEPT_MELEE]` debug logs (if enabled)

---

## 2. Projectile Pooling

Eliminates GC spikes from bullet instantiation.

### Setup

1. Create an empty GameObject: **`_ProjectilePoolManager`**
2. Add the **`ProjectilePoolManager`** component
3. Configure settings:
   - **Initial Pool Size**: 20 (start with this many per type)
   - **Max Pool Size**: 100 (cap to prevent memory bloat)
   - **Max Spawns Per Frame**: 10 (rate limit to prevent spikes)

### Usage

The pool manager is accessed automatically by weapon systems.

For manual spawning:
```csharp
ProjectilePoolManager.Instance.SpawnProjectile(
    prefabIndex: 0,
    position: firePoint.position,
    rotation: firePoint.rotation,
    velocity: aimDirection * speed,
    damage: 25f,
    owner: playerEntity
);
```

### Pre-Warming (Optional)

Call during scene load to avoid initial allocation hitches:
```csharp
ProjectilePoolManager.Instance.PreWarmPool(prefabIndex, prefabEntity, world, count: 20);
```

---

## 3. Recoil Patterns

Skill-based spray patterns that players can learn and control.

### Creating a Pattern

1. In Project window: **Right-click > Create > DIG > Weapons > Recoil Pattern**
2. Name it appropriately (e.g., `RecoilPattern_AssaultRifle`)
3. Use **Context Menu** for quick presets:
   - **Generate Assault Rifle Pattern** - 12-step pattern with horizontal drift
   - **Generate Pistol Pattern** - 3-step high-kick pattern
   - **Generate SMG Pattern** - 8-step fast spray

### Pattern Settings

| Setting | Description |
|---------|-------------|
| **Pattern** | Array of Vector2 offsets (X=horizontal, Y=vertical degrees) |
| **Overflow Mode** | What happens after pattern ends (RepeatLast, Loop, PingPong, Random) |
| **Random Spread** | Random variation added to each shot (degrees) |
| **Pattern Scale** | Overall multiplier for pattern intensity |
| **Recovery Time Per Step** | How fast pattern resets when not firing |
| **Recovery Delay** | Delay before recovery starts |
| **First Shot Accurate** | No recoil on first shot of burst |
| **Visual Kick Strength** | Camera kick intensity (purely cosmetic) |
| **FOV Punch** | FOV reduction on fire (degrees) |

### Setting Up the Registry

1. Create an empty GameObject: **`_RecoilPatternRegistry`**
2. Add **`RecoilPatternRegistry`** component
3. Drag your pattern assets into the **Patterns** list
4. (Optional) Set a **Default Pattern** for fallback

### Linking to Weapons

Weapons need `PatternRecoil` and `PatternRecoilState` components.
The pattern index corresponds to the position in the Registry list.

---

## 4. Predicted Hitmarkers (FEEL Integration)

Instant hit feedback using client-side prediction.

### Prerequisites

- **FEEL** asset installed (MoreMountains.Feedbacks)
- **Cinemachine** configured with Impulse Listener

### Setup

1. Create an empty GameObject: **`_HitmarkerFeedbackBridge`**
2. Add **`HitmarkerFeedbackBridge`** component
3. Create FEEL Feedback Players:

#### Hit Feedback (Regular)
Create a child object with `MMF_Player` containing:
- `MMF_Sound` - Thwack sound
- `MMF_CinemachineImpulse` - Small screen shake
- (Optional) `MMF_ImageAlpha` - Flash the hitmarker

#### Critical Hit Feedback
Same as above but more intense:
- `MMF_Sound` - Distinctive critical sound
- `MMF_CinemachineImpulse` - Bigger shake
- `MMF_FreezeFrame` - Brief pause (0.03s)

#### Kill Feedback
- `MMF_Sound` - Kill confirm sound
- `MMF_ImageAlpha` - Screen flash
- `MMF_CinemachineImpulse` - Dramatic shake

4. Assign the feedback players to `HitmarkerFeedbackBridge`:
   - **Hit Feedback**
   - **Critical Hit Feedback**
   - **Kill Feedback**

5. Configure Audio Clips:
   - **Hit Sound** - Quick thwack/ding
   - **Critical Sound** - Crunch/headshot
   - **Kill Sound** - Satisfying confirm

6. Configure Hitstop:
   - **Enable Hitstop**: ✓
   - **Hitstop Duration**: 0.05 (seconds)
   - **Critical Hitstop Duration**: 0.08 (seconds)

### Weapon HUD Setup

Ensure your HUD prefab has:
- **Hit Marker Image** assigned in `WeaponHUD` component
- Image should be a small "X" or crosshair centered on screen
- Set to disabled by default (enabled by code on hit)

---

## 5. Full Integration Checklist

### Scene Setup
- [ ] `_ProjectilePoolManager` with `ProjectilePoolManager`
- [ ] `_RecoilPatternRegistry` with `RecoilPatternRegistry`
- [ ] `_HitmarkerFeedbackBridge` with `HitmarkerFeedbackBridge`
- [ ] Camera has `CinemachineImpulseListener`

### Per-Weapon Setup
- [ ] Melee: Add `SweptMeleeAuthoring` with appropriate preset
- [ ] Ranged: Weapon has `PatternRecoil` referencing pattern index
- [ ] All: Hitbox components on target characters

### FEEL Feedback Setup
- [ ] Hit feedback player created and assigned
- [ ] Critical feedback player created and assigned
- [ ] Kill feedback player created and assigned
- [ ] Audio clips assigned

### UI Setup
- [ ] `WeaponHUD` has hitmarker image reference
- [ ] Hitmarker image disabled by default

---

## Troubleshooting

### Melee hits not registering
- Check `SweptMeleeAuthoring` is on the weapon entity (not just parent)
- Verify collision mask includes target layers
- Ensure `MeleeState.HitboxActive` is true during swing

### Recoil not applying
- Verify `RecoilPatternRegistry` is in scene and patterns are assigned
- Check weapon has both `PatternRecoil` and `PatternRecoilState`
- Confirm pattern index matches registry position

### No hitmarker feedback
- Ensure `HitmarkerFeedbackBridge` is in scene
- Verify FEEL feedback players are assigned and not null
- Check that hits are against entities with `Hitbox` or `HasHitboxes`

### GC spikes on shooting
- Increase `ProjectilePoolManager.initialPoolSize`
- Call `PreWarmPool` during scene load
- Reduce `maxSpawnsPerFrame` if frame rate matters more than responsiveness

---

## Performance Notes

| Feature | Impact | Optimization |
|---------|--------|--------------|
| Swept Melee | Low | Uses single ColliderCast per active weapon |
| Projectile Pool | Positive | Eliminates instantiation GC |
| Recoil Patterns | Negligible | Simple array lookups |
| Hit Prediction | Low | Single raycast per shot (client only) |
| FEEL Feedback | Medium | Pre-instantiate feedback players |

---

## Next Steps

After setting up Epic 15.5, consider:
- **Epic 15.6**: Weapon Switching & Holstering animations
- **Epic 15.7**: Dual wielding support
- **Epic 15.8**: Weapon attachment/modification system
