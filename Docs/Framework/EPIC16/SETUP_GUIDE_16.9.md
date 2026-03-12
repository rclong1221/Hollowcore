# SETUP GUIDE 16.9: Knockback & Physics Force System

**Status:** Implemented
**Last Updated:** February 22, 2026
**Requires:** Damageable Authoring on target prefabs (standard since EPIC 15.28)

This guide covers Unity Editor setup for the knockback system. After setup, explosions push enemies outward, weapon modifiers stagger targets, environmental hazards launch players, and all knockback respects resistance, immunity, and surface friction.

---

## What Changed

Previously, knockback existed only as data — `ModifierType.Knockback` on weapons was a no-op, `ExplosiveStats.PhysicsForce` was stored but never read, and nothing in the game produced physical displacement. Entities could be damaged but never moved by combat.

Now:

- **Explosions push** — entities in blast radius are pushed radially outward with distance falloff
- **Weapon modifiers work** — weapons with `ModifierType.Knockback` push targets on hit
- **4 knockback types** — Push, Launch, Pull, Stagger — each with distinct movement behavior
- **Resistance system** — per-entity resistance, super armor threshold, and post-knockback immunity
- **Surface friction** — knockback slides further on ice, shorter on mud
- **Visual feedback** — dust impacts on knockback start/end, animator integration
- **Environmental hazards** — steam vents, push traps, gravity wells via trigger volumes

---

## What's Automatic (No Setup Required)

If you change nothing, existing combat systems already produce knockback via the weapon modifier pipeline and explosion system. The only requirement is that target entities have `KnockbackStateAuthoring` on their prefab.

| Feature | How It Works |
|---------|-------------|
| Weapon knockback | Weapons with `ModifierType.Knockback` automatically create knockback requests via CombatResolutionSystem |
| Explosion knockback | Explosions create radial knockback requests for all entities in blast radius |
| Default config | If no `KnockbackConfig` singleton exists, all systems use built-in defaults at runtime |
| Surface friction | Reads existing `GroundSurfaceState` already on player/enemy entities — no new component needed |
| Client prediction | Knockback is predicted on the owning client, server-authoritative with rollback |

### Default Config Values (No Singleton Needed)

| Parameter | Default | Meaning |
|-----------|---------|---------|
| Push Duration | 0.4s | How long a Push knockback lasts |
| Launch Duration | 0.6s | How long a Launch arc lasts |
| Pull Duration | 0.5s | How long a Pull toward source lasts |
| Stagger Duration | 0.2s | How long a Stagger micro-displacement lasts |
| Force Divisor | 100 | Velocity = Force / 100. A 500N hit = 5 m/s knockback |
| Max Velocity | 25 m/s | Hard velocity cap regardless of force |
| Min Effective Force | 50N | Forces below this produce no knockback |

---

## 1. Making an Entity Knockback-Capable

Any entity that should react to knockback needs `KnockbackStateAuthoring`. Without it, all knockback requests targeting that entity are silently ignored.

### 1.1 Add the Component

1. Open your entity prefab (player, enemy, NPC, destructible)
2. Select the **root** GameObject (the one with Damageable Authoring)
3. Click **Add Component** > search for **Knockback State Authoring**

That's it. No fields to configure — the component bakes a zeroed `KnockbackState` that the systems write to at runtime.

### 1.2 Which Prefabs Need It

| Prefab Type | Add KnockbackStateAuthoring? | Notes |
|-------------|:----------------------------:|-------|
| Player | Yes | Knockback delivered via ExternalForceSystem → CharacterController |
| AI Enemies | Yes | Knockback via direct position writes (kinematic body pattern) |
| Bosses | Yes | Combine with KnockbackResistanceAuthoring for super armor |
| Turrets / Stationary | No | Or add with `StartImmune = true` on resistance if you want conditional knockback |
| Destructibles | Optional | Only if you want them pushed by explosions |

---

## 2. Configuring Knockback Resistance (Optional)

Entities without `KnockbackResistanceAuthoring` receive full knockback with no resistance. Add this component to give enemies super armor, damage reduction, or post-knockback immunity windows.

### 2.1 Add the Component

1. On the same root GameObject as Knockback State Authoring
2. Click **Add Component** > search for **Knockback Resistance Authoring**

### 2.2 Inspector Fields

#### Resistance

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Resistance Percent** | Force multiplier reduction. 0 = full knockback, 0.5 = half force, 1.0 = immune | 0 | 0–1 |
| **Super Armor Threshold** | Forces below this value (in Newtons) are ignored entirely. 0 = any force works | 0 | 0+ |

#### Immunity

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Immunity Duration** | Seconds of knockback immunity after a knockback ends. Prevents stunlock chains | 0.2 | 0+ |
| **Start Immune** | Entity starts fully immune to knockback (for boss prefabs, turrets, stationary objects) | false | — |

### 2.3 Resistance Presets by Enemy Type

| Enemy Type | Resistance % | Super Armor | Immunity | Start Immune |
|------------|:-----------:|:-----------:|:--------:|:------------:|
| Trash mob | 0 | 0 | 0.2s | No |
| Standard enemy | 0.2 | 0 | 0.3s | No |
| Heavy enemy | 0.4 | 200N | 0.5s | No |
| Elite | 0.5 | 400N | 1.0s | No |
| Mini-boss | 0.6 | 500N | 1.5s | No |
| Boss | 0.8 | 800N | 2.0s | No |
| Immovable (turret) | 1.0 | — | — | Yes |

---

## 3. Global Knockback Config (Optional Singleton)

To override the default knockback tuning for the entire game, place a **Knockback Config** singleton in your SubScene.

### 3.1 Add the Component

1. In your gameplay SubScene, create an empty GameObject (or use an existing config holder alongside `PhysicsConfig`, `CorpseConfig`, etc.)
2. Click **Add Component** > search for **Knockback Config Authoring**

### 3.2 Inspector Fields

#### Duration Defaults (seconds)

| Field | Description | Default |
|-------|-------------|---------|
| **Push Duration** | Base duration for Push knockback | 0.4 |
| **Launch Duration** | Base duration for Launch arc | 0.6 |
| **Pull Duration** | Base duration for Pull toward source | 0.5 |
| **Stagger Duration** | Base duration for Stagger hit-reaction | 0.2 |

#### Force-to-Velocity Conversion

| Field | Description | Default |
|-------|-------------|---------|
| **Force Divisor** | `Velocity = Force / ForceDivisor`. Higher = slower knockback. Lower = snappier | 100 |
| **Max Velocity** | Hard cap on knockback speed (m/s). Prevents absurd launches | 25 |
| **Minimum Effective Force** | Force below this (after resistance) produces zero knockback | 50 |

#### Launch Tuning

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Default Launch Vertical Ratio** | How much of Launch velocity goes upward (0 = flat, 1 = straight up) | 0.4 | 0–1 |
| **Launch Gravity Multiplier** | Gravity applied during Launch arc descent. Higher = snappier arcs | 1.5 | 0+ |

#### Stagger Tuning

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Stagger Force Multiplier** | Force multiplier for Stagger type. Typically 0.2 = small displacement | 0.2 | 0–1 |
| **Stagger Freeze Frames** | Fixed timesteps of freeze at stagger start before displacement begins | 2 | 0+ |

#### Surface Friction

| Field | Description | Default |
|-------|-------------|---------|
| **Enable Surface Friction** | When enabled, knockback slide distance is modified by surface material | true |

Surface friction multipliers (hardcoded):

| Surface | Multiplier | Effect |
|---------|:----------:|--------|
| Ice | 1.8x | Slides much further |
| Shallow Water | 1.4x | Slides further |
| Metal | 1.1x | Slightly further |
| Stone / Default | 1.0x | Baseline |
| Sand | 0.6x | Slides shorter |
| Mud | 0.5x | Slides much shorter |
| Grass | 0.85x | Slightly shorter |

#### Interrupt

| Field | Description | Default |
|-------|-------------|---------|
| **Interrupt Force Threshold** | Force above which knockback triggers an InterruptRequest on the target | 300 |

> **Note:** Interrupt integration requires EPIC 16.1 (not yet implemented). The threshold is stored but the InterruptRequest is not yet created.

### 3.3 Only One Per World

`KnockbackConfig` is a singleton. Place it once on a dedicated config GameObject alongside other singletons. If multiple exist, only one is active.

---

## 4. Environmental Knockback Sources (Traps, Vents, Hazards)

Steam vents, push traps, wind walls, and gravity wells use `KnockbackSourceAuthoring` on a trigger volume to automatically knock back entities that enter.

### 4.1 Setup

1. Create a GameObject in your SubScene
2. Add a **Physics Shape** (trigger volume) — set **Is Trigger** to true
3. Add a **Physics Body** (static or kinematic)
4. Click **Add Component** > search for **Knockback Source Authoring**
5. Configure force, type, and cooldown

### 4.2 Inspector Fields

| Field | Description | Default |
|-------|-------------|---------|
| **Force** | Knockback force in Newtons. 200 = light push, 500 = medium, 1000+ = heavy | 500 |
| **Type** | Push, Launch, Pull, or Stagger | Push |
| **Easing** | Velocity curve over time (Linear, EaseOut, Bounce, Sharp) | EaseOut |
| **Falloff** | Distance-based force reduction within Radius (None, Linear, Quadratic, Cubic) | None |
| **Radius** | 0 = contact-only (trigger volume shape), >0 = area effect from trigger center | 0 |
| **Triggers Interrupt** | Whether this knockback interrupts the target's current action | false |
| **Cooldown** | Seconds between knockbacks on the same target (prevents rapid re-triggers) | 1.0 |

### 4.3 Knockback Types Explained

| Type | Direction | Vertical | Best For |
|------|-----------|----------|----------|
| **Push** | Away from source | Horizontal only (Y zeroed) | Explosions, traps, wind walls |
| **Launch** | Away + upward arc | Adds configurable vertical component | Geysers, boss slams, jump pads |
| **Pull** | Toward source | Horizontal only | Gravity wells, vortex grenades |
| **Stagger** | From hit direction | Small displacement + freeze | Heavy melee hits, shield bash |

### 4.4 Easing Curves Explained

| Easing | Behavior | Best For |
|--------|----------|----------|
| **Linear** | Constant speed | Simple, predictable knockback |
| **EaseOut** | Fast start, gradual stop | Most natural-feeling (default) |
| **Bounce** | Decelerate then small bounce at end | Comedic or exaggerated hits |
| **Sharp** | Instant spike then rapid exponential decay | Snappy hit reactions, stagger |

### 4.5 Example Setups

#### Steam Vent (Periodic Upward Launch)

| Setting | Value |
|---------|-------|
| Force | 800 |
| Type | Launch |
| Easing | EaseOut |
| Falloff | None |
| Radius | 0 |
| Cooldown | 3.0 |

#### Wind Wall (Continuous Push)

| Setting | Value |
|---------|-------|
| Force | 300 |
| Type | Push |
| Easing | Linear |
| Falloff | None |
| Radius | 0 |
| Cooldown | 0.5 |

#### Gravity Well (Pull Toward Center)

| Setting | Value |
|---------|-------|
| Force | 600 |
| Type | Pull |
| Easing | EaseOut |
| Falloff | Quadratic |
| Radius | 8 |
| Cooldown | 2.0 |

---

## 5. Knockback from Weapons

Weapons produce knockback via the existing `WeaponModifier` system. No new authoring is needed — just configure the weapon's modifier list.

### 5.1 How It Works

1. Weapon has a modifier with `ModifierType = Knockback` and a `Force` value
2. On hit, `CombatResolutionSystem` creates a `KnockbackRequest` targeting the hit entity
3. Direction is calculated from the attacker's position toward the hit point
4. If Force >= 500N, the knockback also triggers an interrupt on the target

### 5.2 Recommended Force Values for Weapons

| Weapon Type | Force (N) | Knockback Effect |
|-------------|:---------:|------------------|
| Light melee (dagger) | 100–200 | Slight push, no interrupt |
| Standard melee (sword) | 300–500 | Medium push, interrupts at 500+ |
| Heavy melee (hammer) | 600–1000 | Strong push with interrupt |
| Shotgun (close range) | 400–600 | Medium push |
| Explosive launcher | Uses explosion system | Radial push with falloff |

---

## 6. Knockback from Explosions

Explosions automatically produce knockback for all entities in the blast radius. There are two sources:

### 6.1 Placed Explosives (Grenades, C4, Barrels)

`ExplosionKnockbackSystem` reads `ExplosionEvent` entities created by the detonation pipeline.

- **Force** is approximated as `BlastRadius * 200` (a 3m grenade = 600N, a 6m barrel = 1200N)
- **Falloff** is Quadratic — entities at the edge receive much less knockback than those at center
- **Direction** is radial outward from detonation point

No setup required — if the explosive exists and targets have `KnockbackStateAuthoring`, it works.

### 6.2 Modifier Explosions (Weapon Proc Effects)

`ModifierExplosionSystem` reads the `KnockbackForce` field on `ModifierExplosionRequest`. To enable:

1. Set `KnockbackForce > 0` on the modifier explosion configuration
2. Force, direction, and falloff are handled automatically

---

## 7. Editor Tools

### 7.1 Knockback Tester Window

**Menu:** DIG > Combat > Knockback Tester

A play-mode tool for testing knockback on live entities without needing weapon hits or explosions.

**How to use:**

1. Enter Play Mode
2. Open **DIG > Combat > Knockback Tester**
3. Configure the knockback parameters:
   - **Type** — Push, Launch, Pull, or Stagger
   - **Force** — 50 to 5000 Newtons (slider)
   - **Easing** — Linear, EaseOut, Bounce, or Sharp
   - **Falloff** — None, Linear, Quadratic, or Cubic
   - **Vertical Ratio** — 0 to 1 (Launch type only)
   - **Ignore SuperArmor** — bypass resistance threshold
   - **Triggers Interrupt** — force interrupt on target
4. Click **Fire Knockback (All Entities)** — applies knockback to every entity with `KnockbackState`, directed from the Scene camera's forward direction
5. Click **Reset All Knockback** — zeroes out all active knockback states

### 7.2 Debug Overlay

Toggle **Debug Overlay** in the Knockback Tester window to enable Scene view gizmos:

- **Colored arrows** show active knockback velocity direction and magnitude
  - Cyan = Push
  - Yellow = Launch
  - Magenta = Pull
  - Red = Stagger
- **Blue wire circles** show entities with active knockback immunity (fades as timer expires)

### 7.3 Knockback Inspector (AI/Combat Workstation)

The AI/Combat Workstation includes a **Knockback** tab showing live ECS data for the selected entity:

**KnockbackState:**
- Active status
- Current type
- Velocity vector
- Speed (m/s)
- Duration progress bar (elapsed / total)
- Easing curve

**KnockbackResistance:**
- Resistance percentage
- Super armor threshold
- Immunity timer (remaining / total)
- Current immune status

---

## 8. After Setup: Reimport SubScene

After adding any knockback authoring components to prefabs or config objects in a SubScene:

1. Open the Scene window
2. Right-click the SubScene > **Reimport**
3. Wait for baking to complete

> If you skip this and no `KnockbackConfig` singleton is baked, systems use built-in defaults at runtime. No errors — just default tuning.

---

## 9. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Tester window | Play Mode > DIG > Combat > Knockback Tester > Fire | All entities with KnockbackState slide in camera direction |
| 3 | Push type | Set Type=Push, Force=500, Fire | Entity slides horizontally away, stops after ~0.4s |
| 4 | Launch type | Set Type=Launch, Force=800, Fire | Entity arcs upward then lands |
| 5 | Pull type | Set Type=Pull, Force=500, Fire | Entity slides toward camera |
| 6 | Stagger type | Set Type=Stagger, Force=300, Fire | Small displacement with brief freeze |
| 7 | Resistance | Entity with SuperArmor=500, Fire at Force=300 | No knockback (below threshold) |
| 8 | Resistance bypass | Same entity, Fire at Force=600 | Knockback applied (above threshold) |
| 9 | Immunity | Fire knockback, then immediately fire again | Second knockback ignored during immunity window |
| 10 | Explosion | Detonate grenade near enemies | All enemies in radius pushed radially outward |
| 11 | Weapon modifier | Hit with weapon that has Knockback modifier | Target pushed in hit direction |
| 12 | Debug overlay | Toggle Debug Overlay ON in Tester | Colored arrows visible on knockback entities in Scene view |
| 13 | Surface friction | Knock entity on ice vs mud | Ice slides further, mud slides shorter |
| 14 | No console errors | Run all above tests | No exceptions or warnings in Console |

---

## 10. Tuning Guide

### Fast & Punchy Combat

| Setting | Value |
|---------|-------|
| Force Divisor | 80 (faster velocity) |
| Push Duration | 0.3s |
| Max Velocity | 30 |
| Easing | Sharp |
| Immunity Duration | 0.1s |

### Weighty & Deliberate Combat

| Setting | Value |
|---------|-------|
| Force Divisor | 150 (slower velocity) |
| Push Duration | 0.6s |
| Max Velocity | 15 |
| Easing | EaseOut |
| Immunity Duration | 0.5s |

### Boss Encounters

Give bosses high resistance so only heavy attacks produce visible knockback:

| Setting | Value |
|---------|-------|
| Resistance Percent | 0.6–0.8 |
| Super Armor Threshold | 500–800N |
| Immunity Duration | 1.5–2.0s |

### Horde Mode (Many Enemies)

Knockback is frame-rate independent and Burst-compiled. Performance cost is minimal even with 100+ enemies. No special tuning needed.

---

## 11. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Entity doesn't react to knockback | Missing `KnockbackStateAuthoring` on prefab | Add component to root GameObject, reimport SubScene |
| Knockback too weak / no visible movement | Force too low or Resistance too high | Increase force, lower ResistancePercent, check SuperArmorThreshold |
| Knockback too strong / flies off screen | MaxVelocity too high or ForceDivisor too low | Increase ForceDivisor or lower MaxVelocity in KnockbackConfig |
| Rapid knockback stunlock | Immunity duration too short | Increase ImmunityDuration on KnockbackResistanceAuthoring |
| Explosion doesn't push | Target entity lacks KnockbackStateAuthoring | Add to prefab and reimport |
| Weapon knockback modifier does nothing | ModifierType set but Force is 0 | Set Force > 0 on the weapon modifier |
| Environmental trap doesn't trigger | Missing trigger volume or Physics Body | Ensure Physics Shape (Is Trigger = true) and Physics Body are on the same GameObject |
| Trap re-triggers too fast | Cooldown too short | Increase Cooldown on KnockbackSourceAuthoring |
| No dust VFX on knockback | Entity lacks GroundSurfaceState | Ensure entity has ground surface detection (standard on players/enemies) |
| Debug overlay not showing | Debug Overlay toggle off | Enable in Knockback Tester window during Play Mode |
| Knockback on ice same as stone | Surface Friction disabled | Enable `EnableSurfaceFriction` in KnockbackConfig |

---

## 12. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Health, damage, death components | Damageable Authoring (EPIC 15.28) |
| Explosion detonation pipeline | Explosive system (EPIC 15.29) |
| Weapon modifiers, combat resolution | SETUP_GUIDE_15.29 |
| Surface materials, ground detection | SETUP_GUIDE_16.10 |
| Corpse lifecycle after death | SETUP_GUIDE_16.3 |
| VFX feedback (dust, impacts) | EPIC 16.7 VFX Pipeline |
| Physics optimization, collision filters | SETUP_GUIDE_15.23 |
| **Knockback & physics force** | **This guide (16.9)** |
