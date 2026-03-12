# Setup Guide: EPIC 13.7 (Weapons Framework)

## Overview
EPIC 13.7 implements weapon action types for combat: firearms, melee, throwables, shields, and projectiles.

## Step 1: Create Weapon Prefab

1. Create new prefab for your weapon.
2. Add `WeaponAuthoring` component.
3. Select **Weapon Type**: Shootable, Melee, Throwable, or Shield.
4. Configure type-specific settings.

## Weapon Types

### Shootable (Firearms)
| Setting | Description | Default |
|---------|-------------|---------|
| Fire Rate | Rounds per second | 10 |
| Damage | Damage per hit | 20 |
| Range | Max range (meters) | 100 |
| Spread Angle | Base spread (degrees) | 2 |
| Recoil Amount | Camera kick | 5 |
| Is Automatic | Hold to fire | true |
| Use Hitscan | Instant hit vs projectile | true |

### Melee
| Setting | Description | Default |
|---------|-------------|---------|
| Melee Damage | Per hit | 50 |
| Attack Speed | Attacks/second | 2 |
| Combo Count | Chain hits | 3 |
| Hitbox Active | Normalized time (0.2-0.6) | - |

### Throwable
| Setting | Description | Default |
|---------|-------------|---------|
| Min/Max Force | Throw speed | 10-30 |
| Charge Time | Time to max charge | 1s |
| Throw Arc | Arc angle (degrees) | 15 |

### Shield
| Setting | Description | Default |
|---------|-------------|---------|
| Block Reduction | Damage reduction (0.7 = 70%) | 0.7 |
| Parry Window | Perfect parry time | 0.15s |
| Block Angle | Coverage (degrees) | 120 |

## Step 2: Equip Weapon to Player

1. Player must have `EquipmentAuthoring` (from EPIC 13.6).
2. Create weapon entity and assign to player's equipment slot.
3. Set `EquipRequest.Pending = true` to equip.

## Verification

1. Spawn weapon entity with `WeaponAuthoring`.
2. Equip to player.
3. Test fire/attack input.
4. **Verify:** Cooldowns, ammo, recoil, and damage work correctly.
