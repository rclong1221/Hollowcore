# SETUP GUIDE 15.13: Compositional Projectile System

This guide explains how to create and configure projectile prefabs using the compositional behavior system.

---

## Overview

The compositional system allows you to create any projectile type by mixing and matching behavior components on prefabs. No code changes required.

**Key Principle**: Spawn systems only set position, rotation, velocity, and owner. All other behavior comes from the prefab's components.

---

## Quick Reference: Behavior Components

| Component | Add Component Menu | What It Does | Use For |
|-----------|-------------------|--------------|---------|
| `DamageOnImpactAuthoring` | `DIG/Projectiles/Damage On Impact` | Deals damage when hitting something | Throwing knives, arrows, rocks |
| `StickOnImpactAuthoring` | `DIG/Projectiles/Stick On Impact` | Embeds into surfaces, stops moving | Throwing knives, arrows |
| `BounceOnImpactAuthoring` | `DIG/Projectiles/Bounce On Impact` | Bounces off surfaces | Grenades, rubber balls |
| `DamageOnDetonateAuthoring` | `DIG/Projectiles/Explosion Damage` | Area damage to entities when exploding | Grenades, rockets, bombs |
| `ProjectileExplosionAuthoring` | `DIG/Projectiles/Projectile Explosion Config` | Voxel destruction + detonation triggers | Any explosive |
| `ApplyStatusOnHitAuthoring` | `DIG/Projectiles/Apply Status On Hit` | Apply status effects on hit | Poison arrows, fire arrows |
| `CreateAreaOnDetonateAuthoring` | `DIG/Projectiles/Create Area On Detonate` | Create persistent area effect | Molotov, smoke grenade |

---

## Understanding Explosion Components

There are **two different explosion-related components** that serve different purposes:

### ProjectileExplosionAuthoring (Voxel Destruction + Triggers)
**Add Component Menu**: `DIG/Projectiles/Projectile Explosion Config`

This component handles:
- **Voxel terrain destruction** (crater creation)
- **Detonation triggers** (when does it explode?)

| Field | Description | Typical Values |
|-------|-------------|----------------|
| **Explosion Radius** | Size of voxel crater | 3-8 |
| **Explosion Damage** | Damage dealt to voxels | 50-200 |
| **Spawn Loot** | Drop items from destroyed voxels? | ✅ or ❌ |
| **Fuse Time** | Seconds until timer detonation | 3-5s |
| **Detonate On Impact** | Explode on contact? | ✅ for rockets |
| **Detonate On Timer** | Explode after fuse time? | ✅ for grenades |

> **Note**: You can enable both impact AND timer - whichever happens first triggers detonation.

### DamageOnDetonateAuthoring (Entity Damage)
**Add Component Menu**: `DIG/Projectiles/Explosion Damage`

This component handles:
- **Entity damage** (players, enemies, NPCs)
- **Damage falloff** calculation

| Field | Description | Typical Values |
|-------|-------------|----------------|
| **Damage** | Maximum damage at center | 50-100 |
| **Radius** | Damage radius in meters | 5-8 |
| **Damage Type** | Type for resistance calc | Explosion |
| **Falloff Exponent** | 1.0 = linear, 2.0 = realistic | 1.5-2.0 |
| **Edge Damage Multiplier** | Minimum damage at edge | 0.1 |

### When to Use Each

| Scenario | Use ProjectileExplosionAuthoring | Use DamageOnDetonateAuthoring |
|----------|----------------------------------|-------------------------------|
| Destroy terrain only | ✅ Yes | ❌ No |
| Damage players only | ❌ No | ✅ Yes |
| Destroy terrain AND damage players | ✅ Yes | ✅ Yes (both) |

**Example**: A frag grenade should have BOTH components:
- `ProjectileExplosionAuthoring` - Creates crater, configures fuse timer
- `DamageOnDetonateAuthoring` - Damages nearby players

---

## Adding Components in Unity

### Finding Components

1. **Select** your projectile prefab in the Hierarchy or Project window
2. Click **Add Component** button in the Inspector
3. **Search** using these terms:

| To Add | Search For |
|--------|------------|
| Direct hit damage | `Damage On Impact` |
| Stick to surfaces | `Stick On Impact` |
| Bounce behavior | `Bounce On Impact` |
| Entity explosion damage | `Explosion Damage` |
| Voxel destruction + triggers | `Projectile Explosion` |
| Status effects | `Apply Status` |
| Area effects (fire/smoke) | `Create Area` |

> **Tip**: All projectile components are under the `DIG/Projectiles` menu category.

---

## Creating a New Projectile Prefab

### Step 1: Create Base Prefab

1. Create a new empty GameObject
2. Add visual mesh (the projectile model)
3. Add required base components:

| Component | Required | Purpose |
|-----------|----------|---------|
| `GhostAuthoring` | ✅ Yes | Network replication |
| `ProjectileAuthoring` | ✅ Yes | Core projectile data (lifetime, physics) |
| Physics Collider | ✅ Yes | Collision detection |

### Step 2: Configure ProjectileAuthoring

This is the **core component** every projectile needs.

| Field | Description | Example Values |
|-------|-------------|----------------|
| **Damage** | Base damage on direct hit | 25 (knife), 50 (grenade) |
| **Lifetime** | Seconds before auto-destroy | 5-10s |
| **Gravity** | Gravity strength | 9.81 (realistic), 0 (rocket) |
| **Drag** | Air resistance | 0.1 (grenade), 0 (bullet) |
| **Has Gravity** | Enable gravity? | ✅ for thrown, ❌ for rockets |
| **Bounce On Impact** | Should it bounce? (legacy) | Use BounceOnImpactAuthoring instead |
| **Max Bounces** | How many bounces (legacy) | Use BounceOnImpactAuthoring instead |

### Step 3: Add Behavior Components

Add **only the components you need** for the desired behavior.

---

## Behavior Component Configuration

### DamageOnImpactAuthoring
**Add Component Menu**: `DIG/Projectiles/Damage On Impact`

**Use for**: Projectiles that deal damage when they hit something (before exploding or sticking).

| Field | Description | Typical Values |
|-------|-------------|----------------|
| **Damage** | Damage on direct hit | 25-50 |
| **Damage Type** | Type of damage | Physical |
| **Apply To Hit Entity** | Damage the thing we hit? | ✅ Usually yes |
| **Damage Radius** | Splash damage radius | 0 (no splash), 2-3m (splash) |
| **Damage Falloff** | How damage decreases with distance | 1.0 (linear), 2.0 (quadratic) |

**Example**: Throwing Knife
- Damage: 35
- Apply To Hit Entity: ✅
- Damage Radius: 0 (no splash)

---

### StickOnImpactAuthoring
**Add Component Menu**: `DIG/Projectiles/Stick On Impact`

**Use for**: Projectiles that embed into surfaces instead of bouncing.

| Field | Description | Typical Values |
|-------|-------------|----------------|
| **Stick To Entities** | Stick to moving targets? | ✅ |
| **Stick To World** | Stick to static surfaces? | ✅ |
| **Penetration Depth** | How deep to embed (meters) | 0.05-0.15 |
| **Align To Surface** | Rotate to match surface normal | ✅ for knives/arrows |

**Example**: Arrow
- Stick To Entities: ✅
- Stick To World: ✅
- Penetration Depth: 0.1
- Align To Surface: ✅

---

### BounceOnImpactAuthoring
**Add Component Menu**: `DIG/Projectiles/Bounce On Impact`

**Use for**: Projectiles that bounce off surfaces.

| Field | Description | Typical Values |
|-------|-------------|----------------|
| **Bounciness** | Energy retained per bounce (0-1) | 0.6 |
| **Max Bounces** | Maximum bounces before stopping | 3 |

---

### ApplyStatusOnHitAuthoring
**Add Component Menu**: `DIG/Projectiles/Apply Status On Hit`

**Use for**: Projectiles that apply status effects on impact.

| Field | Description | Typical Values |
|-------|-------------|----------------|
| **Status Type** | Effect to apply | Burning, Poisoned, Slowed, Stunned, Bleeding |
| **Duration** | How long effect lasts (seconds) | 3-10 |
| **Intensity** | Strength of the effect | 1.0 |

**Example**: Poison Arrow
- Status Type: Poisoned
- Duration: 8
- Intensity: 1.5

---

### CreateAreaOnDetonateAuthoring
**Add Component Menu**: `DIG/Projectiles/Create Area On Detonate`

**Use for**: Projectiles that create persistent ground effects.

| Field | Description | Typical Values |
|-------|-------------|----------------|
| **Area Type** | Type of area | Fire, Smoke, Gas, Light |
| **Radius** | Size of the area | 2-5m |
| **Duration** | How long area persists | 5-15s |
| **Area Prefab** | Optional prefab to spawn | FirePool, SmokeCloud |

**Example**: Molotov Cocktail
- Area Type: Fire
- Radius: 3
- Duration: 10

**Gizmo**: Colored wireframe sphere shows the area radius in Scene view.

---

## Example Prefab Recipes

### Frag Grenade (Bounces, Timed Explosion)

```
FragGrenade_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 10 (contact damage)
│   ├── Lifetime: 10
│   ├── Gravity: 9.81
│   ├── Drag: 0.1
│   └── Has Gravity: ✅
├── BounceOnImpactAuthoring          ← Search: "Bounce On Impact"
│   ├── Bounciness: 0.6
│   └── Max Bounces: 3
├── ProjectileExplosionAuthoring     ← Search: "Projectile Explosion"
│   ├── Explosion Radius: 4 (voxel crater)
│   ├── Spawn Loot: ✅
│   ├── Fuse Time: 3.5
│   ├── Detonate On Timer: ✅
│   └── Detonate On Impact: ❌
└── DamageOnDetonateAuthoring        ← Search: "Explosion Damage"
    ├── Damage: 75
    ├── Radius: 6
    └── Falloff Exponent: 1.5
```

---

### Impact Grenade (Explodes on Contact)

```
ImpactGrenade_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 0
│   ├── Lifetime: 10
│   ├── Gravity: 9.81
│   └── Has Gravity: ✅
├── ProjectileExplosionAuthoring     ← Search: "Projectile Explosion"
│   ├── Explosion Radius: 3
│   ├── Detonate On Impact: ✅
│   └── Detonate On Timer: ❌
└── DamageOnDetonateAuthoring        ← Search: "Explosion Damage"
    ├── Damage: 60
    ├── Radius: 5
    └── Falloff Exponent: 1.0
```

---

### Throwing Knife (Sticks, Direct Damage)

```
ThrowingKnife_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 35
│   ├── Lifetime: 30
│   ├── Gravity: 9.81
│   ├── Drag: 0.05
│   └── Has Gravity: ✅
├── DamageOnImpactAuthoring          ← Search: "Damage On Impact"
│   ├── Damage: 35
│   ├── Apply To Hit Entity: ✅
│   └── Damage Radius: 0
└── StickOnImpactAuthoring           ← Search: "Stick On Impact"
    ├── Penetration Depth: 0.08
    ├── Align To Surface: ✅
    ├── Stick To Entities: ✅
    └── Stick To World: ✅
```

---

### Arrow (Sticks, Direct Damage)

```
Arrow_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 30
│   ├── Lifetime: 60
│   ├── Gravity: 4.0 (lighter arc)
│   ├── Drag: 0.02
│   └── Has Gravity: ✅
├── DamageOnImpactAuthoring          ← Search: "Damage On Impact"
│   ├── Damage: 30
│   └── Apply To Hit Entity: ✅
└── StickOnImpactAuthoring           ← Search: "Stick On Impact"
    ├── Penetration Depth: 0.15
    └── Align To Surface: ✅
```

---

### Explosive Arrow (Sticks, Then Explodes)

```
ExplosiveArrow_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 15
│   ├── Lifetime: 10
│   └── Gravity: 4.0
├── DamageOnImpactAuthoring          ← Search: "Damage On Impact"
│   ├── Damage: 15
│   └── Apply To Hit Entity: ✅
├── StickOnImpactAuthoring           ← Search: "Stick On Impact"
│   └── Penetration Depth: 0.1
├── ProjectileExplosionAuthoring     ← Search: "Projectile Explosion"
│   ├── Explosion Radius: 2
│   ├── Fuse Time: 1.5 (explodes 1.5s after sticking)
│   └── Detonate On Timer: ✅
└── DamageOnDetonateAuthoring        ← Search: "Explosion Damage"
    ├── Damage: 40
    └── Radius: 4
```

---

### Rock (Bounces, Minor Damage)

```
Rock_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 10
│   ├── Lifetime: 15
│   ├── Gravity: 9.81
│   └── Has Gravity: ✅
├── BounceOnImpactAuthoring          ← Search: "Bounce On Impact"
│   ├── Bounciness: 0.5
│   └── Max Bounces: 5
└── DamageOnImpactAuthoring          ← Search: "Damage On Impact"
    ├── Damage: 10
    └── Apply To Hit Entity: ✅
```

---

### Poison Arrow (Sticks, Status Effect)

```
PoisonArrow_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 20
│   ├── Lifetime: 60
│   └── Gravity: 4.0
├── DamageOnImpactAuthoring          ← Search: "Damage On Impact"
│   ├── Damage: 20
│   └── Apply To Hit Entity: ✅
├── StickOnImpactAuthoring           ← Search: "Stick On Impact"
│   └── Penetration Depth: 0.12
└── ApplyStatusOnHitAuthoring        ← Search: "Apply Status"
    ├── Status Type: Poisoned
    ├── Duration: 8
    └── Intensity: 1.0
```

---

### Molotov Cocktail (Breaks on Impact, Creates Fire)

```
Molotov_Projectile
├── GhostAuthoring
├── ProjectileAuthoring
│   ├── Damage: 5
│   ├── Lifetime: 10
│   └── Has Gravity: ✅
├── ProjectileExplosionAuthoring     ← Search: "Projectile Explosion"
│   ├── Explosion Radius: 1 (small crater)
│   └── Detonate On Impact: ✅
├── DamageOnDetonateAuthoring        ← Search: "Explosion Damage"
│   ├── Damage: 15
│   ├── Radius: 3
│   └── Damage Type: Heat
└── CreateAreaOnDetonateAuthoring    ← Search: "Create Area"
    ├── Area Type: Fire
    ├── Radius: 3
    ├── Duration: 10
    └── Area Prefab: FirePool
```

---

## Linking Projectile to Throwable Weapon

After creating your projectile prefab:

1. Open your **throwable weapon prefab** (e.g., `Grenade_Weapon`)
2. Find the `WeaponAuthoring` component
3. In **Throwable Settings**, set:
   - **Throwable Projectile Prefab**: Drag your projectile prefab here
   - **Min Throw Force**: 10-15
   - **Max Throw Force**: 25-35
   - **Charge Time**: 1-2 seconds

The deprecated fields (Projectile Lifetime, Projectile Damage) are ignored - values come from the projectile prefab.

---

## Troubleshooting

### Components Not Appearing in Add Component Menu

| Symptom | Solution |
|---------|----------|
| Can't find any DIG components | Check Unity Console for compile errors first |
| Specific component missing | Search using the menu name (e.g., "Explosion Damage" not "DamageOnDetonate") |
| Components appear then disappear | Force reimport: Assets → Reimport All |

### Projectile Doesn't Spawn
- Check `GhostAuthoring` is on the prefab
- Verify `ProjectileAuthoring` has valid Lifetime > 0
- Ensure weapon's `Throwable Projectile Prefab` is assigned

### Projectile Doesn't Move
- Check `Has Gravity` is enabled (or Gravity = 0 for rockets)
- Verify physics collider is properly configured

### Projectile Doesn't Deal Damage
- For direct hits: Add `DamageOnImpactAuthoring` (search: "Damage On Impact")
- For explosions: Add `DamageOnDetonateAuthoring` (search: "Explosion Damage")
- Verify Damage value > 0

### Projectile Doesn't Explode
Need BOTH a trigger AND damage/destruction config:
- **Trigger**: Enable `Detonate On Timer` or `Detonate On Impact` in `ProjectileExplosionAuthoring`
- **Voxel Destruction**: Configure radius in `ProjectileExplosionAuthoring`
- **Entity Damage**: Add `DamageOnDetonateAuthoring` separately

### Terrain Not Destroyed But Players Take Damage
- Missing `ProjectileExplosionAuthoring` (voxel destruction)
- Only have `DamageOnDetonateAuthoring` (entity damage)
- Add both for full explosion behavior

### Players Not Damaged But Terrain Destroyed
- Missing `DamageOnDetonateAuthoring` (entity damage)
- Only have `ProjectileExplosionAuthoring` (voxel destruction)
- Add both for full explosion behavior

### Projectile Passes Through Things
- Check physics collider is enabled
- Verify collision layers are correct

### Projectile Bounces When It Should Stick
- Remove `BounceOnImpactAuthoring`
- Add `StickOnImpactAuthoring`
- Check ProjectileAuthoring's legacy Bounce settings are disabled

### Double Damage Being Applied
- Check you don't have duplicate authoring components
- Verify `DamageOnImpactAuthoring` damage matches intended direct hit damage
- Verify `DamageOnDetonateAuthoring` damage matches intended explosion damage

---

## Component Compatibility Matrix

| Behavior | Compatible With | Incompatible With |
|----------|-----------------|-------------------|
| `StickOnImpact` | `DamageOnImpact`, `ProjectileExplosion` | `BounceOnImpact` |
| `BounceOnImpact` | `ProjectileExplosion`, `DamageOnImpact` | `StickOnImpact` |
| `DamageOnImpact` | Everything | - |
| `DamageOnDetonate` | `ProjectileExplosion` (needs trigger) | Nothing |
| `ProjectileExplosion` | Everything | - |
| `ApplyStatusOnHit` | Everything | - |
| `CreateAreaOnDetonate` | `ProjectileExplosion` (needs trigger) | Nothing |

---

## Visual Debugging

The authoring components include **Scene View gizmos**:

- **ProjectileExplosionAuthoring**: Orange wireframe sphere (voxel radius), Red inner sphere (full damage core)
- **DamageOnDetonateAuthoring**: Orange wireframe sphere (damage radius), Red inner sphere (full damage core)
- **CreateAreaOnDetonateAuthoring**: Colored wireframe sphere based on area type

Select the projectile prefab in Scene view to see these visualizations.

---

## File Structure

```
Assets/Scripts/Weapons/
├── Authoring/
│   ├── ProjectileBehaviorAuthoring.cs    (DamageOnImpact, ApplyStatusOnHit, CreateAreaOnDetonate)
│   ├── StickOnImpactAuthoring.cs         (StickOnImpact)
│   ├── BounceOnImpactAuthoring.cs        (BounceOnImpact → ProjectileBounce component)
│   ├── DamageOnDetonateAuthoring.cs      (Entity explosion damage)
│   └── ProjectileExplosionAuthoring.cs   (Voxel destruction + triggers)
├── Components/
│   └── ProjectileBehaviorComponents.cs   (ECS component structs)
└── Systems/
    └── ProjectileBehaviorSystem.cs       (Runtime behavior processing)
```
