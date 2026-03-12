# SETUP GUIDE 15.22: Floating Damage Text & Combat Feedback

**Status**: Implemented (Phases 1-3 + EPIC 15.28 Unified Pipeline)
**Last Updated**: February 10, 2026
**Requires**: Unity 6.2+ (URP), Damage Numbers Pro asset

This guide covers Unity Editor setup for EPIC 15.22 floating damage text and the EPIC 15.28 unified combat resolution pipeline. All code is complete - this guide is for scene configuration and asset creation only.

---

## Quick Start

1. Run **DIG > Setup > Combat UI** to auto-create folder structures and default configs
2. Create Damage Number prefabs (Section 2)
3. Set up hitbox colliders on enemies (Section 4)
4. Assign stat profiles to characters/enemies (Section 5)
5. Verify with the checklist (Section 8)

---

## 1. Scene Setup

### 1.1 CombatUIBootstrap (Required)

The `CombatUIBootstrap` component is the central manager that connects all combat UI views.

1. **Hierarchy** > Right-click > Create Empty > Rename to `CombatUIManager`
2. Add Component: **CombatUIBootstrap**
3. Enable **Auto Find Views** to automatically locate views in the scene

| Inspector Field | What to Assign |
|-----------------|----------------|
| Hitmarker View | `EnhancedHitmarkerView` in scene |
| Directional Damage View | `DirectionalDamageIndicatorView` in scene |
| Combo Counter View | `ComboCounterView` in scene |
| Kill Feed View | `KillFeedView` in scene |
| Combat Log View | `CombatLogView` in scene |
| Status Effect View | `StatusEffectBarView` in scene |
| Boss Health Bar View | `BossHealthBarView` in scene |

> With **Auto Find Views** enabled, these are populated automatically at runtime.

### 1.2 DamageNumbersProAdapter (Required)

1. On the same `CombatUIManager` GameObject, Add Component: **DamageNumbersProAdapter**
2. Configure the adapter settings:

| Inspector Field | Recommended Value | Description |
|-----------------|-------------------|-------------|
| Feedback Profile | Assign your `DamageFeedbackProfile` asset | Data-driven visual config |
| Spawn Offset | `(0, 1.5, 0)` | Vertical offset above hit point |
| Random Offset Range | `0.3` | Horizontal scatter for stacked numbers |
| Stack Window | `0.1` | Seconds to combine rapid hits into one number |
| Min Display Threshold | `0.1` | Minimum damage to show a number |
| Enable Frustum Culling | Enabled | Skip numbers behind the camera |

---

## 2. Damage Number Prefabs

### 2.1 Create Prefabs

1. **Assets** > Right-click > Create > **Damage Numbers Pro** > Damage Number (Mesh)
2. Create the following variants and save to `Assets/Prefabs/UI/DamageNumbers/`:

| Prefab Name | Color | Scale | Pool Size | Notes |
|-------------|-------|-------|-----------|-------|
| `DamageNumber_Normal` | White | 1.0x | 30 | Standard hits |
| `DamageNumber_Critical` | Yellow/Orange | 1.5x | 15 | Crits and headshots |
| `DamageNumber_Heal` | Green | 1.0x | 10 | Healing numbers |
| `DamageNumber_Miss` | Gray | 0.8x | 10 | Missed/dodged attacks |
| `DamageNumber_Block` | Blue | 1.0x | 10 | Blocked attacks |
| `DamageNumber_Absorb` | Cyan | 1.0x | 5 | Shield absorption |
| `DamageNumber_Parried` | Gold | 1.2x | 5 | Perfect parry |
| `DamageNumber_Immune` | White/Silver | 1.0x | 5 | Immune targets |
| `DamageNumber_Execute` | Red-Gold | 1.8x | 5 | Killing blows |

3. On each prefab, enable **Pooling** and set the pool size as listed above.

### 2.2 Assign to DamageNumberConfig

1. **Assets** > Right-click > Create > DIG > Combat > **Damage Number Config**
2. Save as `Assets/Data/Config/DamageNumberConfig.asset`
3. Assign prefabs:

| Config Field | Prefab |
|--------------|--------|
| Default Prefab | `DamageNumber_Normal` |
| Critical Prefab | `DamageNumber_Critical` |
| Heal Prefab | `DamageNumber_Heal` |
| Blocked Prefab | `DamageNumber_Block` |
| Parried Prefab | `DamageNumber_Parried` |
| Immune Prefab | `DamageNumber_Immune` |
| Execute Prefab | `DamageNumber_Execute` |
| Fire Prefab | *(optional)* Tinted orange variant |
| Ice Prefab | *(optional)* Tinted blue variant |
| Lightning Prefab | *(optional)* Tinted yellow variant |
| Poison Prefab | *(optional)* Tinted purple variant |

4. Configure format strings (displayed text patterns):

| Format Field | Value | Result |
|--------------|-------|--------|
| Normal Format | `{0:0}` | `42` |
| Critical Format | `{0:0}!` | `128!` |
| Blocked Format | `BLOCKED` | `BLOCKED` |
| Parried Format | `PARRY!` | `PARRY!` |
| Immune Format | `IMMUNE` | `IMMUNE` |
| Execute Format | `{0:0}` | `256` |

5. Configure culling:

| Culling Field | Recommended | Description |
|---------------|-------------|-------------|
| Cull Distance | `50` | Don't spawn numbers beyond this range |
| Max Active Numbers | `50` | Hard cap before low-priority culling |

---

## 3. Damage Feedback Profile (Data-Driven Visuals)

This ScriptableObject controls how each hit type looks. It's the primary designer tool for tuning combat "feel".

1. **Assets** > Right-click > Create > DIG > Combat > **Damage Feedback Profile**
2. Save as `Assets/Data/Config/DefaultDamageFeedbackProfile.asset`
3. Assign to the `DamageNumbersProAdapter` component's **Feedback Profile** field

### 3.1 Hit Severity Profiles

Each profile has: **Prefab**, **Scale Multiplier**, **Color Override**, **Use Color Override**

| Profile | Prefab | Scale | Color | Notes |
|---------|--------|-------|-------|-------|
| Normal Hit | `DamageNumber_Normal` | 1.0 | White | Standard damage |
| Critical Hit | `DamageNumber_Critical` | 1.5 | Yellow | Crits and headshots |
| Graze Hit | `DamageNumber_Normal` | 0.8 | Gray (50% alpha) | Partial dodges |
| Miss Hit | `DamageNumber_Miss` | 0.9 | Gray | Complete misses |
| Execute Hit | `DamageNumber_Execute` | 1.8 | Red-Gold | Killing blows |

### 3.2 Defensive Feedback Profiles

| Profile | Prefab | Scale | Color | Notes |
|---------|--------|-------|-------|-------|
| Blocked Hit | `DamageNumber_Block` | 1.1 | Blue | Shield blocks |
| Parried Hit | `DamageNumber_Parried` | 1.2 | Gold | Perfect parries |
| Immune Hit | `DamageNumber_Immune` | 1.0 | White/Silver | Invulnerable states |

### 3.3 Context Event Profiles

| Profile | Prefab | Notes |
|---------|--------|-------|
| Headshot Text | `DamageNumber_Critical` | Shown alongside "HEADSHOT" tag |
| Backstab Text | `DamageNumber_Critical` | Shown alongside "BACKSTAB" tag |

### 3.4 Damage Type Profiles (Elemental Colors)

Add entries to the **Damage Types** list:

| Type | Display Name | Color | Size Multiplier |
|------|-------------|-------|-----------------|
| Physical | Physical | White | 1.0 |
| Fire | Fire | Orange | 1.0 |
| Ice | Ice | Light Blue | 1.0 |
| Lightning | Lightning | Yellow | 1.1 |
| Poison | Poison | Purple/Green | 0.9 |
| Holy | Holy | Gold | 1.0 |
| Shadow | Shadow | Dark Purple | 1.0 |
| Arcane | Arcane | Magenta | 1.0 |

### 3.5 Culling Settings

| Field | Recommended | Description |
|-------|-------------|-------------|
| Cull Distance | `50` | Max spawn distance from camera |
| Max Active Numbers | `50` | Priority culling kicks in above this |

> **Culling priority** (low to high): Graze (0) < Normal (1) < Blocked (2) < Critical (3) < Execute (4). Graze numbers are culled first when at capacity. Critical and Execute are never culled.

---

## 4. Hitbox Setup (Headshots, Limb Damage)

EPIC 15.28 routes weapon hits through the combat resolution pipeline, which uses **hitbox regions** for headshot detection, damage multipliers, and contextual flags.

### 4.1 Root Character Setup

1. Select the **root GameObject** of your character/enemy prefab
2. Add Component: **HitboxOwnerMarker**

> This marks which entity "owns" the hitboxes and receives the damage.

### 4.2 Per-Region Hitbox Setup

For each body region that should have separate hit detection:

1. Create a **child GameObject** under the character root
2. Add a **Physics Shape** (or Collider) sized to the body region
3. Add Component: **HitboxAuthoring**
4. Configure:

| Inspector Field | Description |
|-----------------|-------------|
| Damage Multiplier | Damage scaling for this region (0.1 - 5.0) |
| Region | Body region enum for feedback routing |

### 4.3 Recommended Multipliers

| Region | Enum Value | Damage Multiplier | Effect |
|--------|------------|-------------------|--------|
| Head | `Head` | `2.0` | 2x damage, triggers HEADSHOT flag, bonus crit chance |
| Torso | `Torso` | `1.0` | Normal damage |
| Arms | `Arms` | `0.75` | Reduced damage |
| Legs | `Legs` | `0.5` | Half damage |
| Hands | `Hands` | `0.5` | Half damage |
| Feet | `Feet` | `0.25` | Minimal damage |

### 4.4 Hitbox Hierarchy Example

```
EnemyPrefab (Root)
  + HitboxOwnerMarker
  |
  +-- Head_Hitbox
  |   + SphereCollider
  |   + HitboxAuthoring (Region: Head, Multiplier: 2.0)
  |
  +-- Torso_Hitbox
  |   + CapsuleCollider
  |   + HitboxAuthoring (Region: Torso, Multiplier: 1.0)
  |
  +-- Legs_Hitbox
      + CapsuleCollider
      + HitboxAuthoring (Region: Legs, Multiplier: 0.5)
```

### 4.5 What the Pipeline Does with Hitboxes

When a weapon (projectile, melee sweep, or hitscan) hits a hitbox collider:

1. The **damage multiplier** scales the damage for health subtraction
2. The **region** is passed to combat resolvers which set contextual flags:
   - `Head` region sets the **Headshot** flag and provides bonus crit chance
   - Attack direction vs target facing detects **Backstab** (attacker behind target)
3. The UI pipeline reads these flags and shows contextual text ("HEADSHOT", "BACKSTAB") alongside the damage number
4. Damage numbers for crits/headshots use the **Critical Hit** profile (larger, colored, different prefab)

> **No hitboxes?** If an entity has no `HitboxAuthoring` children, hits default to `Torso` region with `1.0` multiplier. Everything still works, you just won't get headshot/limb feedback.

---

## 5. Combat Stat Profiles (Enemy/Character Stats)

Combat resolution uses attacker and target stats for damage calculation, crit rolls, and elemental resistance. Without stat components, resolvers use default values (no scaling, no crits).

### 5.1 Create a Stat Profile

1. **Assets** > Right-click > Create > DIG > Combat > **Stat Profile**
2. Save to `Assets/Data/Stats/` (e.g., `SkeletonWarrior_Stats.asset`)

| Section | Fields | Description |
|---------|--------|-------------|
| **Offensive** | Attack Power, Spell Power, Crit Chance (0-1), Crit Multiplier (1.0+), Accuracy | How hard this character hits |
| **Defensive** | Defense, Armor, Evasion (0-1) | Damage mitigation |
| **Attributes** | Strength, Dexterity, Intelligence, Vitality | Stat scaling factors |
| **Per-Level Scaling** | Attack/Spell/Defense Per Level | Auto-scales with character level |
| **Elemental Resistances** | Physical, Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane (0-1 each) | Damage type reduction |

### 5.2 Elemental Resistance Effects on UI

Resistances affect both damage calculation and visual feedback:

| Resistance Value | UI Effect |
|-----------------|-----------|
| > 0.25 | Damage number shows **Resistance** flag (down arrow, grayed) |
| < -0.1 (weakness) | Damage number shows **Weakness** flag (up arrow, pulsing) |
| 0.0 - 0.25 | Normal damage number |

### 5.3 Damage Formula (Optional Customization)

1. **Assets** > Right-click > Create > DIG > Combat > **Damage Formula**
2. Configure expressions for damage calculation, crit chance, mitigation
3. Enable/disable graze mechanics, level scaling, elemental modifiers

---

## 6. Additional Config Assets

### 6.1 Combat Feedback Config

**Create**: Assets > Create > DIG > Combat > **Combat Feedback Config**
**Save to**: `Assets/Data/Config/CombatFeedbackConfig.asset`

Key designer-facing settings:

| Section | Notable Fields |
|---------|---------------|
| **Damage Numbers** | Stack Window (0.05-0.5s), Cull Distance (10-100m) |
| **Floating Text** | Pool Size, Lifetime (0.5-3s), Rise Speed, Spam Cooldown |
| **Directional Damage** | Max Indicators (4-12), Duration, Normal/Critical colors |
| **Combo Counter** | Timeout (1-5s), Milestones (5, 10, 25, 50, 100) |
| **Kill Feed** | Max Entries (3-10), Entry Duration, Show Headshot Icons |

### 6.2 Hitmarker Config

**Create**: Assets > Create > DIG > Combat > **Hitmarker Config**
**Save to**: `Assets/Data/Config/HitmarkerConfig.asset`

| Section | Notable Fields |
|---------|---------------|
| **Sprites** | Default, Critical, Kill sprites |
| **Colors** | Normal (White), Critical (Yellow), Kill (Red), Armor (Gray), Shield (Cyan) |
| **Animation** | Scale Punch (1.0-2.0), Rotation Shake (0-15 degrees) |
| **Audio** | Normal/Critical/Kill hit sounds, Volume |

### 6.3 Floating Text Style Config

**Create**: Assets > Create > DIG > Combat > **Floating Text Style Config**
**Save to**: `Assets/Data/Config/FloatingTextStyleConfig.asset`

Defines visual style per text category (each has Color, Font Size, Duration, Rise Speed, Fade Start, Scale Curve):

| Style | Default Color | Default Size | Use Case |
|-------|---------------|-------------|----------|
| Normal | White | 24px | Generic floating text |
| Important | Yellow | 32px | "HEADSHOT", "BACKSTAB", "BROKEN" |
| Warning | Orange | 28px | Low health warnings |
| Success | Green | 30px | Quest/interaction feedback |
| Failure | Gray | 22px | Failed actions |

### 6.4 Enemy UI Config

**Create**: Assets > Create > DIG > Combat > **Enemy UI Config**
**Save to**: `Assets/Data/Config/EnemyUIConfig.asset`

| Section | Notable Fields |
|---------|---------------|
| **Health Bar Colors** | Full (Green), Low (Red), Trail (Dark Red), Background, Border |
| **Size by Tier** | Trash Mob, Elite, Mini-Boss (Vector2 each) |
| **Shield/Armor Bars** | Show toggle, colors, height ratios |
| **Nameplates** | Show toggle, font size, elite vs normal colors |
| **Billboard** | Lock Y-axis, scale with distance, min scale |

---

## 7. Pipeline Overview (How It All Connects)

Understanding the data flow helps with debugging. No code changes needed - this is reference only.

### Two Damage Pathways

| Pathway | Source | Route | UI Output |
|---------|--------|-------|-----------|
| **Combat Resolution** | Projectiles, Melee Sweeps, Hitscan | Weapon > `PendingCombatHit` > `CombatResolutionSystem` > `CombatResultEvent` > `CombatUIBridgeSystem` | Rich numbers (crits, headshots, backstabs, elemental) |
| **Direct Damage** | Grenades, Hazards, AOE, Fall Damage | Source > `DamageEvent` > `DamageEventVisualBridgeSystem` > `DamageVisualQueue` > `CombatUIBridgeSystem` | Simple damage numbers |

### What Triggers Rich Feedback

| Feature | Requires |
|---------|----------|
| Headshot text | `HitboxAuthoring` with `Region = Head` on target |
| Backstab text | Attack from behind target (automatic, no setup needed) |
| Crit numbers | `AttackStats` with `CritChance > 0` on attacker |
| Elemental colors | `WeaponStats.DamageType` set on weapon |
| Weakness/Resistance arrows | `ElementalResistances` on target via stat profile |
| Block/Parry/Immune text | Combat resolver returns defensive HitType |

---

## 8. Verification Checklist

After setup, test each feature:

| Test | Steps | Expected Result |
|------|-------|-----------------|
| Basic damage number | Shoot/hit an enemy | White number floats up from target |
| Headshot | Shoot enemy in the head (Head hitbox) | Larger yellow number + "HEADSHOT" tag |
| Critical hit | Hit enemies repeatedly | Occasional larger yellow/orange numbers |
| Backstab | Attack enemy from behind | "BACKSTAB" tag on damage number |
| Miss | *(Requires StatBasedRoll resolver)* | Gray "MISS" text |
| Block | *(Requires defensive combat)* | Blue "BLOCKED" text |
| Kill | Kill an enemy | Kill feed entry appears, kill hitmarker flashes |
| Hitmarker | Hit any enemy | Crosshair hitmarker flash |
| Combo | Chain rapid hits | Combo counter increments |
| Directional damage | Take damage from enemy | Edge indicator points toward damage source |
| Elemental weakness | Hit fire-weak enemy with fire | Damage number shows up-arrow |
| No double numbers | Any weapon hit | Exactly ONE damage number per hit (not two) |
| AOE damage | Grenade/hazard damage | Simple white numbers (direct damage path) |
| Distance culling | Hit enemy beyond cull distance | No number spawned |
| Pool stress test | AoE 50+ enemies | Stable framerate, low-priority numbers culled |

---

## 9. Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| No damage numbers at all | Adapter not registered | Ensure `DamageNumbersProAdapter` is on an active GameObject with prefabs assigned |
| Numbers appear but all look the same (no crits/headshots) | No hitboxes or stat components | Add `HitboxAuthoring` to enemy collider children; add `CombatStatProfile` |
| "HEADSHOT" never appears | Head hitbox not set up | Add child collider with `HitboxAuthoring` (Region: Head, Multiplier: 2.0) |
| Double damage numbers on same hit | Dedup not working | Check that `DamageEventVisualBridgeSystem` is active (auto-created by ECS) |
| Numbers appear with wrong color | Feedback profile not assigned | Assign `DamageFeedbackProfile` to adapter's Feedback Profile field |
| Hitmarker not showing | Config not assigned | Assign `HitmarkerConfig` to `EnhancedHitmarkerView` |
| Combo counter not working | Bootstrap missing | Ensure `CombatUIBootstrap` is in scene |
| Kill feed empty | No `DeathEvent` being created | Verify enemies have `HealthComponent`; check console for errors |
| Console: "No DamageNumbers provider registered!" | Registration timing | `DamageNumbersProAdapter` must be active before first combat event |
| Numbers only on grenades, not on weapon hits | Weapon not creating `PendingCombatHit` | Verify EPIC 15.28 code is compiled (check for errors in Console) |

---

## 10. Designer Tips

### Tuning Combat Feel

- **Crit frequency**: Adjust `CritChance` in the attacker's stat profile (0.05 = 5% chance)
- **Headshot reward**: Adjust `DamageMultiplier` on the Head hitbox (2.0 = double damage)
- **Number readability**: Increase `Random Offset Range` if numbers overlap; decrease if too scattered
- **Visual hierarchy**: Critical/Execute numbers should be noticeably larger than normal hits. Use scale multipliers of 1.5x+ in the feedback profile
- **Culling aggressiveness**: Lower `Max Active Numbers` for cleaner screens in AoE-heavy combat

### Adding a New Enemy Type

1. Create a **CombatStatProfile** with appropriate stats and resistances
2. Add **HitboxOwnerMarker** to the root prefab
3. Add **HitboxAuthoring** to collider children (at minimum: Head + Torso)
4. The combat UI pipeline picks up everything automatically - no scene wiring needed

### Adding a New Elemental Damage Type

1. Add entry to the **Damage Type Profiles** list in your `DamageFeedbackProfile`
2. Set the color, display name, and size multiplier
3. Optionally create a dedicated Damage Numbers Pro prefab with custom particles/effects
4. Set `WeaponStats.DamageType` on weapons that should deal this element
