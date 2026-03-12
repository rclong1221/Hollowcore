# Epic 15.14 Setup Guide: Health Bar Visibility System

**Status:** ✅ Complete — All 15 visibility modes implemented

This guide covers the Unity Editor setup for the **Health Bar Visibility System** - a data-driven system that controls when and how enemy health bars appear.

---

## Overview

The visibility system supports multiple display modes:
- **Always** - Health bars always visible
- **Never** - Health bars never visible (hardcore mode)
- **WhenDamaged** - Show when HP < Max (stays visible until healed)
- **WhenDamagedWithTimeout** - Show when damaged, hides after X seconds
- **WhenPlayerDealtDamage** - Show only for enemies the player has hit
- **WhenPlayerDealtDamageWithTimeout** - Same as above with timeout
- **WhenInProximity** - Show when within X distance
- **WhenBelowHealthThreshold** - Show when HP below X%

### Modes Requiring External Systems (All Complete ✅)

| Mode | Dependency | Status |
|------|------------|--------|
| WhenTargeted | Target Lock System (15.16) | ✅ Complete |
| WhenTargetedOrDamaged | Target Lock System (15.16) | ✅ Complete |
| WhenInCombat | Combat State System (15.15) | ✅ Complete |
| WhenInCombatWithTimeout | Combat State System (15.15) | ✅ Complete |
| WhenInLineOfSight | Vision/LOS System (15.17) | ✅ Complete |
| WhenAggroed | Aggro/Threat System (15.19) | ✅ Complete |
| WhenHovered | Cursor Hover System (15.18) | ✅ Complete |

---

## Quick Start

### 1. Verify EnemyHealthBarPool Settings

Select the **EnemyHealthBarPool** GameObject in your scene and configure:

| Field | Recommended Value | Description |
|-------|-------------------|-------------|
| Use Visibility System | ✅ Enabled | Enable the new visibility system |
| Debug Visibility | ☐ Disabled | Enable for console logging during testing |
| Fade In Speed | 5 | How fast bars appear |
| Fade Out Speed | 3 | How fast bars disappear |
| Max Show Distance | 50 | Maximum distance to show bars |
| Always Show Targeted | ✅ Enabled | Override visibility for targeted enemy |

### 2. Using the Visibility Tester (Development Only)

Add `HealthBarVisibilityTester` to any GameObject for testing:

1. Create empty GameObject: **_HealthBarVisibilityTester**
2. Add Component: `HealthBarVisibilityTester`
3. Configure in Inspector:

| Field | Description |
|-------|-------------|
| Show Debug Overlay | Shows current mode info on screen |
| Use Direct Override | ✅ Enable to test modes directly |
| Direct Mode | Select visibility mode to test |
| Direct Flags | Toggle modifier flags |
| Direct Fade Timeout | Seconds before hiding (for timeout modes) |
| Direct Proximity Range | Distance threshold (for proximity modes) |

> **Note:** Remove or disable the tester before shipping. It's for development/QA only.

---

## Multiplayer Setup (Required)

For health bars to work correctly in multiplayer, enemy prefabs **must be configured as ghosts**.

### Enemy Prefab Requirements

1. Open each enemy prefab (e.g., `BoxingJoe`)
2. Add Component: **Ghost Authoring Component**
3. Configure:
   - **Default Ghost Mode:** Interpolated
   - **Supported Ghost Modes:** Interpolated
   - **Has Owner:** ❌ Unchecked
4. Save prefab and **re-bake any SubScenes** containing the enemy

### Why This Is Required

Without ghost replication:
- Server updates enemy health → Client never sees it
- `ShowHealthBarTag`, `HasAggroOn`, `Health` only exist on server

With ghost replication:
- `Health.Current` syncs automatically to all clients
- Visibility components replicate correctly

See [SETUP_GUIDE_15.18.md](../../SETUP_GUIDE_15.18.md#multiplayer-setup-netcode) for detailed ghost component configuration.

---

## Scene Requirements

```
Scene Root
├── _EnemyHealthBarPool (EnemyHealthBarPool)
│   └── Health Bar Prefab reference
└── _HealthBarVisibilityTester (optional, dev only)
```

---

## Configuration Assets

### Creating Player Settings Asset

1. **Right-click** in Project → Create → DIG → Combat → Health Bar Player Settings
2. Configure the simplified player-facing options:

| Field | Description |
|-------|-------------|
| Visibility Preset | Quick preset dropdown (see below) |
| Fade Timeout | Seconds before hiding (for timeout presets) |
| Max Distance | Proximity range |
| Use Fade Transitions | Enable smooth fade in/out |
| Show Enemy Names | Display name above bar |
| Show Enemy Levels | Display level indicator |
| Show Status Effects | Show buff/debuff icons |
| Scale Boss Bars | Make boss bars larger |
| Elite And Boss Only | Hide bars for normal enemies |
| Show Friendly Bars | Include allied NPCs |
| Show Neutral Bars | Include neutral NPCs |

### Player Visibility Presets

| Preset | Behavior |
|--------|----------|
| AlwaysShow | Always visible |
| Never | Never visible |
| WhenDamaged | Show when HP < Max, no timeout |
| WhenDamagedWithFade | Show when damaged, fade after timeout |
| TargetOnly | Only show for targeted enemy |
| TargetAndDamaged | Target OR damaged |
| NearbyOnly | Show within proximity range |
| NearbyAndDamaged | Nearby AND damaged |
| Custom | Use custom config asset |

### Creating Custom Visibility Config (Advanced)

For advanced scenarios requiring full control:

1. **Right-click** in Project → Create → DIG → Combat → Health Bar Visibility Config
2. Configure all options:

| Section | Fields |
|---------|--------|
| Primary Mode | Full mode selection (17 options) |
| Flags | All modifier flags (see Modifier Flags below) |
| Timing | hideAfterSeconds, fadeInDuration, fadeOutDuration, showDelay |
| Distance | proximityDistance, fadeStartDistance, maxVisibleDistance |
| Thresholds | healthThreshold, minimumTier |
| Scaling | normalScale, eliteScaleMultiplier, bossScaleMultiplier |

---

## Modifier Flags Reference

| Flag | Effect |
|------|--------|
| UseFadeTransitions | Smooth alpha fade instead of instant show/hide |
| HideAtFullHealth | Hide bar even if other conditions are met when at full HP |
| HostileOnly | Only show for hostile faction |
| IncludeFriendlies | Also show for friendly NPCs |
| IncludeNeutrals | Also show for neutral NPCs |
| BossesOnly | Only show for Boss tier and above |
| ElitesOnly | Only show for Elite tier and above |
| NamedOnly | Only show for named entities |
| RequireDiscovered | Requires enemy to be in bestiary |
| RequireScanned | Requires player to use scan ability first |
| RequireSkillUnlock | Requires specific perk/skill |
| ShowName | Display enemy name |
| ShowLevel | Display enemy level |
| ShowStatusEffects | Show buff/debuff icons |
| ColorByThreatLevel | Color bar by difficulty relative to player |
| ScaleByImportance | Scale bar size by entity tier |

---

## Runtime API (For UI Integration)

When building a settings menu, use the singleton manager:

```csharp
// Get the manager
var manager = HealthBarSettingsManager.Instance;

// Apply a preset
manager.ApplyPreset(HealthBarPlayerSettings.PlayerVisibilityPreset.WhenDamaged);

// Or set mode directly
manager.SetMode(HealthBarVisibilityMode.Always);

// Configure options
manager.SetFadeTimeout(5f);
manager.SetProximityRange(20f);
manager.SetUseFadeTransitions(true);
manager.SetShowName(true);
manager.SetShowLevel(true);

// Save/Load from PlayerPrefs
manager.SaveSettings();
manager.LoadSettings();
```

> **Note:** Changes are applied immediately. The pool automatically subscribes to `OnSettingsChanged` events.

---

## Troubleshooting

### Health bars not appearing at all
1. Check `EnemyHealthBarPool.useVisibilitySystem` is enabled
2. Check the current mode isn't `Never`
3. Enable `debugVisibility` on the pool to see console logs
4. Verify the health bar prefab is assigned

### Health bars disappearing too quickly
1. Check if using a `*WithTimeout` mode when you want a non-timeout mode
2. Increase `hideAfterSeconds` in the config
3. For `WhenDamaged` mode, bars stay visible as long as HP < Max

### Fade transitions not working
1. Ensure `UseFadeTransitions` flag is enabled
2. Check `fadeInSpeed` and `fadeOutSpeed` on the pool are > 0

### Mode changes not applying
1. If using the tester, ensure `Use Direct Override` is checked
2. Verify the pool's `OnSettingsChanged` is subscribed (check console for "[HealthBarPool] Settings changed!" logs)

---

## Previous Versions

- [15.7 - Opsive Parity Analysis](SETUP_GUIDE_15.7.md)
- [15.5 - Weapon System Completeness](SETUP_GUIDE_15.5.md)
