# SETUP GUIDE 16.11: Combat UI Pipeline Wiring

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Damage Numbers Pro (Asset Store), CombatUIBootstrap scene setup (EPIC 15.9)

This guide covers Unity Editor setup for the combat UI pipeline. After setup, all combat feedback works end-to-end -- damage numbers, hitmarkers, directional damage indicators, combo counter, kill feed, camera shake, and hit stop. Surface damage zones also show floating DOT numbers.

---

## What Changed

Previously, the combat UI pipeline was architecturally complete but electrically disconnected. Damage numbers appeared for enemies but all player-specific feedback was dead:

| Before (EPIC 15.9--15.30) | After (EPIC 16.11) |
|---------------------------|---------------------|
| Player shoots enemy -- no hitmarker | Hitmarker appears on crosshair |
| Player crits enemy -- no hit stop | 0.05s hit stop on crit, 0.1s on execute |
| Player kills enemy -- no kill marker | Kill marker + kill feed entry |
| Player takes damage -- no direction indicator | Directional damage indicator from hit source |
| Player takes damage -- combo doesn't break | Combo counter breaks |
| Player takes crit -- no screen shake | Camera shake proportional to damage |
| Standing in lava -- no damage numbers | DOT damage numbers appear (IsDOT style) |
| Missing scene components -- silent failure | Console warnings identify what's missing |

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Player entity binding | `CombatUIPlayerBindingSystem` finds local player via `GhostOwnerIsLocal` and calls `SetPlayerEntity()` automatically |
| Player position tracking | Updated every frame for directional damage indicators |
| Diagnostic warnings | One-time console warnings after ~1 second if scene components are missing |
| Surface DOT visuals | `SurfaceDamageSystem` enqueues to `DamageVisualQueue` whenever damage ticks |
| Remote client DOT | DOT damage RPCs sent to remote clients automatically |
| DamageEvent visuals | `DamageEventVisualBridgeSystem` bridges grenade/AOE/hazard damage to floating numbers |
| CRE visuals | `CombatResolutionSystem` enqueues hitscan/melee hits to floating numbers |
| Defensive text | Block, parry, immune results show via `CombatResultEvent` path |

---

## 1. Required Scene Components

All combat UI requires specific MonoBehaviours in the scene. Use the automated setup or add manually.

### 1.1 Quick Setup (Recommended)

1. **Menu:** DIG > Setup > Combat UI
2. Click **FULL SETUP**
3. This creates all folders, config assets, prefabs, shader materials, and adds a `CombatUIManager` GameObject to the scene

### 1.2 Manual Setup

If you prefer manual control, add these GameObjects to your scene:

#### CombatUIBootstrap (Required)

Central hub for all combat UI views. Without this, hitmarkers, combo, kill feed, and directional damage are disabled.

1. Create an empty GameObject, name it `CombatUIManager`
2. Add Component > **CombatUIBootstrap**
3. Set **Auto Find Views** to `true` (default) -- it will locate view components in the scene automatically
4. Alternatively, drag view references into the inspector slots:

| Slot | Component | Purpose |
|------|-----------|---------|
| **Hitmarker View** | EnhancedHitmarkerView | Crosshair hit/kill markers |
| **Directional Damage View** | DirectionalDamageIndicatorView | Edge-of-screen damage direction |
| **Combo Counter View** | ComboCounterView | Hit combo display |
| **Kill Feed View** | KillFeedView | Kill notification list |
| **Combat Log View** | CombatLogView | Scrolling damage history |
| **Status Effect View** | StatusEffectBarView | Buff/debuff icons |
| **Boss Health Bar View** | BossHealthBarView | Boss encounter bar |

#### DamageNumbersProAdapter (Required for Damage Numbers)

Bridges the `DamageVisualQueue` to Damage Numbers Pro for floating numbers.

1. On the same `CombatUIManager` (or a Canvas child), Add Component > **DamageNumbersProAdapter**
2. Assign a **Damage Feedback Profile** (see Section 2)
3. Registers itself with `CombatUIRegistry` on `OnEnable` -- no manual wiring needed

| Field | Default | Description |
|-------|---------|-------------|
| **Feedback Profile** | -- | DamageFeedbackProfile ScriptableObject (colors, scales, prefabs) |
| **Spawn Offset** | (0, 1.5, 0) | Vertical offset from hit position |
| **Random Offset Range** | 0.3 | Horizontal spread for multiple hits |
| **Stack Window** | 0.1s | Combine rapid damage into single number |
| **Min Display Threshold** | 1 | Minimum damage to show (ignores tiny ticks) |
| **Enable Frustum Culling** | true | Skip numbers behind the camera |

#### ShaderHealthBarSync (Required for Player Health Bar)

Shader-driven player health bar. Reads `Player.Components.Health` from the local player entity.

1. On your HUD Canvas, select the health bar Image
2. Add Component > **ShaderHealthBarSync**
3. Assign the health bar **Image** reference (or it auto-finds on the same GameObject)

| Field | Default | Description |
|-------|---------|-------------|
| **Bar Image** | (auto) | UI Image that displays the health bar |
| **Shader** | (auto) | Falls back to `DIG/UI/ProceduralHealthBar` if null |
| **Critical Threshold** | 0.25 | Health percent below which bar shows critical state |

**World discovery:** Automatically finds the best ECS world (ClientWorld > ServerWorld > DefaultWorld). Handles world transitions (e.g., menu to gameplay).

#### ShaderStaminaBarSync (Optional -- Player Stamina)

Same pattern as ShaderHealthBarSync but reads `PlayerStamina`.

| Field | Default | Description |
|-------|---------|-------------|
| **Bar Image** | (auto) | Stamina bar Image |
| **Shader** | (auto) | Falls back to `DIG/UI/ProceduralStaminaBar` |
| **Empty Threshold** | 0.05 | Threshold for "empty" visual state |
| **Smooth Fill** | true | Continuous fill vs chunky segments |

#### ShaderBatteryBarSync (Optional -- Flashlight Battery)

Same pattern but reads `FlashlightState` / `FlashlightConfig`.

| Field | Default | Description |
|-------|---------|-------------|
| **Bar Image** | (auto) | Battery bar Image |
| **Shader** | (auto) | Falls back to `DIG/UI/ProceduralBatteryBar` |
| **Low Battery Threshold** | 0.2 | Warning state |
| **Flicker Threshold** | 0.1 | Flicker state |
| **Smooth Fill** | true | Fill mode |

#### EnemyHealthBarPool (Required for Enemy Health Bars)

Floating health bars above enemies.

1. Create an empty GameObject, name it `EnemyHealthBarPool`
2. Add Component > **EnemyHealthBarPool**
3. Assign the **Health Bar Prefab** (created by the setup tool at `Assets/Prefabs/UI/WorldSpace/EnemyHealthBar.prefab`)

---

## 2. Damage Feedback Profile

Controls all visual properties of damage numbers -- colors, scales, prefabs per hit type, elemental tinting.

### 2.1 Create via Setup Tool (Recommended)

1. **Menu:** DIG > Setup > Create Damage Feedback System
2. Creates 11 DamageNumberMesh prefabs + DefaultDamageFeedbackProfile with all wired up
3. Assign the profile to your `DamageNumbersProAdapter` component

### 2.2 Manual Creation

1. **Right-click** in Project window > Create > ScriptableObject > search **DamageFeedbackProfile**
2. Configure hit type profiles:

| Hit Type | Suggested Color | Scale | Use Case |
|----------|----------------|-------|----------|
| **Normal** | Element color | 1.0 | Standard hits |
| **Critical** | Orange-red tint | 1.3 | Crits (tints element color) |
| **Graze** | Dimmed | 0.8 | Partial hits |
| **Execute** | Gold tint | 1.5 | Finishing blows |
| **Blocked** | Grey | 0.9 | Shield blocks |
| **Parried** | Cyan | 1.0 | Parry ripostes |
| **Immune** | White | 0.7 | Immune targets |
| **Miss** | Grey | 0.6 | Whiffed attacks |

3. Configure damage type (element) colors:

| Element | Suggested Color | Suffix |
|---------|----------------|--------|
| Physical | White | -- |
| Fire | Orange-red | fire emoji |
| Ice | Light blue | snowflake |
| Lightning | Yellow | lightning bolt |
| Poison | Green | skull |
| Holy | Warm white | star |
| Shadow | Purple | diamond |
| Arcane | Magenta | sparkle |

4. Assign a DamageNumberMesh prefab to each hit type slot
5. Assign the profile to your `DamageNumbersProAdapter`

---

## 3. Verification

### 3.1 Editor Diagnostic Tool

**Menu:** DIG > Diagnostics > Verify Combat UI Setup

Checks the active scene for all required components and reports pass/fail:

| Check | Required For |
|-------|-------------|
| DamageNumberAdapterBase subclass | Floating damage numbers |
| DamageFeedbackProfile assigned | Damage number colors/scales |
| CombatUIBootstrap | Hitmarkers, combo, kill feed, directional damage |
| ShaderHealthBarSync | Player health bar |
| EnemyHealthBarPool | Enemy floating health bars |

### 3.2 Runtime Console Diagnostics

After ~1 second of gameplay, `CombatUIBridgeSystem` logs one-time warnings for any missing component:

```
[CombatUI] No IDamageNumberProvider registered. Add DamageNumbersProAdapter to scene Canvas.
[CombatUI] CombatUIBootstrap.Instance is null. Add CombatUIBootstrap for hitmarkers, combo, kill feed.
[CombatUI] No ICombatFeedbackProvider registered. Hit stop and camera shake will not function.
[CombatUI] _playerEntity is Entity.Null. CombatUIPlayerBindingSystem should set this when local player spawns.
```

If you see no warnings, the pipeline is fully connected.

### 3.3 Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Player health bar | Take damage from enemy | Health bar decreases smoothly |
| 3 | Enemy damage numbers | Shoot enemy with hitscan | Floating damage number at hit point |
| 4 | AOE damage numbers | Throw grenade near enemy | Floating numbers from DamageEvent pipeline |
| 5 | Hitmarker | Shoot enemy | Crosshair hitmarker flashes |
| 6 | Crit hit stop | Land a critical hit | Brief freeze-frame (0.05s) |
| 7 | Kill marker | Kill enemy | Kill confirmation marker + kill feed entry |
| 8 | Directional damage | Get hit by enemy | Edge-of-screen indicator pointing at attacker |
| 9 | Combo counter | Hit enemy multiple times | Combo count increments |
| 10 | Combo break | Take damage during combo | Combo counter resets |
| 11 | Camera shake | Take critical hit | Screen shakes proportional to damage |
| 12 | Surface DOT | Stand in lava damage zone | DOT-styled floating numbers appear |
| 13 | DOT remote client | Observe DOT on another player (multiplayer) | Damage numbers visible via RPC |
| 14 | Defensive text | Block/parry an attack | "BLOCKED" / "PARRY!" text appears |
| 15 | Missing adapter warning | Remove DamageNumbersProAdapter, enter Play Mode | Console warning after ~1s |
| 16 | Enemy health bars | Damage any enemy | Floating health bar appears above enemy |
| 17 | Diagnostics tool | DIG > Diagnostics > Verify Combat UI Setup | All checks pass |

---

## 4. System Execution Order

```
PresentationSystemGroup (Client|Local):
  CombatUIPlayerBindingSystem        <- Finds local player, calls SetPlayerEntity + SetPlayerPosition
  DamageVisualRpcReceiveSystem       <- Receives RPCs from server, enqueues to DamageVisualQueue
  CombatUIBridgeSystem               <- Dequeues DamageVisualQueue -> damage numbers
                                        Reads CombatResultEvent -> hitmarkers, combo, kill feed
                                        Reads DeathEvent -> kill feed, feedback
  CombatEventCleanupSystem           <- Destroys CombatResultEvent + DeathEvent entities
  EnemyHealthBarBridgeSystem         <- Floating health bars above enemies

DamageSystemGroup (PredictedFixedStep, Server|Local):
  DamageEventVisualBridgeSystem      <- DamageEvent buffers -> DamageVisualQueue + RPCs
  DamageApplySystem                  <- Clears DamageEvent buffers (applies player damage)
  SimpleDamageApplySystem            <- Clears DamageEvent buffers (applies NPC damage)

SimulationSystemGroup (Server|Local):
  CombatResolutionSystem             <- PendingCombatHit -> CombatResultEvent + DamageVisualQueue
  DamageApplicationSystem            <- CombatResultEvent -> Health writes + DeathEvent + RPCs
  SurfaceDamageSystem                <- Surface DOT -> Health writes + DamageVisualQueue + RPCs
  ServerCombatCleanupSystem          <- Destroys CRE + DeathEvent on server (no PresentationGroup)
```

---

## 5. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| No damage numbers at all | DamageNumbersProAdapter missing from scene | Add to Canvas. Check DIG > Diagnostics > Verify Combat UI Setup |
| Damage numbers but no hitmarkers | CombatUIBootstrap missing from scene | Add CombatUIBootstrap with views |
| Hitmarkers/combo/kill feed dead | `_playerEntity` not bound (Entity.Null) | Check console for binding warning. Ensure player has `PlayerTag` component |
| No directional damage indicator | CombatUIBootstrap missing DirectionalDamageIndicatorView | Assign view or enable Auto Find Views |
| No camera shake on hits | No ICombatFeedbackProvider registered | Register a feedback provider with CombatUIRegistry |
| No surface DOT numbers | SurfaceDamageSystem not running | Ensure SurfaceGameplayToggles.EnableSurfaceDamageZones is true |
| DOT numbers invisible on remote client | RPC not received | Check DamageVisualRpcReceiveSystem is in ClientSimulation world |
| Health bar not updating | ShaderHealthBarSync querying wrong world | Component auto-discovers worlds. Check console for errors |
| Health bar shows full despite damage | Wrong Health component type | ShaderHealthBarSync reads `Player.Components.Health` (not `HealthComponent`) |
| Enemy health bars invisible | EnemyHealthBarPool missing or prefab unassigned | Add pool + assign prefab. Use DIG > Setup > Fix Health Bar |
| Console spam about missing providers | Scene lacks adapters | Add missing components per warning message |
| Damage numbers use wrong colors | DamageFeedbackProfile not assigned | Assign profile to DamageNumbersProAdapter |
| "MISS" text everywhere | Enemies have no Health component | Ensure target entities have `Player.Components.Health` |

---

## 6. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Damage Numbers Pro integration, hit severity tiers | SETUP_GUIDE_15.9 |
| Combat resolution pipeline (CRE, PendingCombatHit) | EPIC 15.22 |
| DamageVisualQueue, DamageVisualRpc, DOT routing | EPIC 15.30 |
| Enemy health bars, EnemyHealthBarBridgeSystem | SETUP_GUIDE_15.9 |
| Surface damage zones (lava, acid, DOT) | SETUP_GUIDE_16.10 |
| VFX pipeline (combat VFX, dissolve) | SETUP_GUIDE_16.7 |
| Corpse lifecycle (death -> ragdoll -> dissolve) | SETUP_GUIDE_16.3 |
| Aggro / threat framework | SETUP_GUIDE_15.33 |
| **Combat UI pipeline wiring** | **This guide (16.11)** |
