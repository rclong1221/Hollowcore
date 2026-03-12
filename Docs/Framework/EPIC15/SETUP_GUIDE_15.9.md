# SETUP GUIDE 15.9: Combat Feedback & Floating UI Systems

**Status**: ✅ FULLY IMPLEMENTED (MeshRenderer + URP Shaders)  
**Last Updated**: January 26, 2026 (Centralized Setup & Mesh UI Pivot)
**Requires**: Unity 6.2+ (URP)

This guide covers Unity Editor setup for EPIC 15.9 combat feedback systems. All code is complete - this guide is for scene configuration only.

---

## Quick Start: Use the Setup Tool

**Menu:** `DIG → Setup → Combat UI`

The setup tool automates most of this guide. It will:
- ✅ Create all folder structures
- ✅ Create config ScriptableObjects
- ✅ Create shader materials (URP)
- ✅ Create MeshRenderer prefabs (replaces UI Toolkit world-space)
- ✅ Add CombatUIManager to scene
- ✅ Clean up old UI Toolkit & UGUI files

**After running the tool, see sections 2-6 for manual configuration steps.**

---

**Menu:** `DIG → Setup → Combat UI` (Consolidated)

The primary setup tool now handles the Mesh-based health bar system.
1. Folder structure: `Assets/Content/Combat/UI/WorldSpace/`
2. Prefab: `Assets/Prefabs/UI/WorldSpace/EnemyHealthBar.prefab` (Mesh Mode)
3. Scene object: `EnemyHealthBarManager` with `EnemyHealthBarPool`

**Usage after setup:**
1. Add `DamageableAuthoring` to any entity
2. Check ☑️ **Show Health Bar** option
3. Health bars appear automatically when entities take damage

---

## Technology Overview

**The system uses a hybrid approach for performance and stability:**

| Layer | Technology | Notes |
|-------|------------|-------|
| Screen-Space UI | UI Toolkit (UIDocument) | Views for HUD elements (Combo, Killfeed) |
| World-Space UI | **MeshRenderer (Quad)** | Health bars (Direct quad rendering for stability) |
| Visual Effects | Custom URP Shaders | Procedural fill, trails, and glow |

**Shaders provided:**
- `CombatUI_HealthBar.shader` - Health/trail fill with gradient + glow
- `CombatUI_RadialFill.shader` - Radial progress with edge glow
- `CombatUI_Glow.shader` - General glow/pulse effects
- `CombatUI_Hitmarker.shader` - Animated hitmarker effects

---

## Prerequisites

| Requirement | Where to Get |
|-------------|--------------|
| Unity 6.2+ | Required for UI Toolkit WorldSpace |
| Damage Numbers Pro | Asset Store (already at `Assets/DamageNumbersPro/`) |
| FEEL (More Mountains) | Asset Store (already installed) |
| URP | Project render pipeline |

---

## 1. Scene Setup - Combat UI Manager

### 1.1 Create the Manager GameObject

1. **Hierarchy** → Right-click → Create Empty
2. Rename to `CombatUIManager`
3. Add Component: `CombatUIBootstrap`
4. Check ☑️ **Auto Find Views** (recommended)

### 1.2 CombatUIBootstrap Inspector Settings

| Field | Assignment |
|-------|------------|
| Hitmarker View | Auto-found or drag from scene |
| Directional Damage View | Auto-found or drag from scene |
| Combo Counter View | Auto-found or drag from scene |
| Kill Feed View | Auto-found or drag from scene |
| Combat Log View | Auto-found or drag from scene |
| Status Effect View | Auto-found or drag from scene |
| Boss Health Bar View | Auto-found or drag from scene |

---

## 2. Shader Materials Setup

### 2.1 Verify Shaders Exist

Check `Assets/Shaders/UI/` contains:
- `CombatUI_HealthBar.shader`
- `CombatUI_RadialFill.shader`
- `CombatUI_Glow.shader`
- `CombatUI_Hitmarker.shader`

### 2.2 Create Materials

1. **DIG → Setup → Combat UI** → Click "Create Shader Materials"
2. Or manually create in `Assets/Materials/UI/`:

| Material | Shader |
|----------|--------|
| `CombatUI_HealthBar.mat` | DIG/UI/CombatUI_HealthBar |
| `CombatUI_RadialFill.mat` | DIG/UI/CombatUI_RadialFill |
| `CombatUI_Glow.mat` | DIG/UI/CombatUI_Glow |
| `CombatUI_Hitmarker.mat` | DIG/UI/CombatUI_Hitmarker |

### 2.3 Assign to Prefabs

1. Select `EnemyHealthBar` prefab
2. Assign `CombatUI_HealthBar.mat` to **Health Bar Shader Material** field
3. Repeat for `InteractionRing` with `CombatUI_RadialFill.mat`

---

## 3. Damage Numbers Pro Setup

### 3.1 Create Damage Number Prefabs

1. **Assets** → Right-click → Create → **Damage Numbers Pro** → Damage Number (Mesh)
2. Create 6 variants and save to `Assets/Prefabs/UI/DamageNumbers/`:

| Prefab Name | Configuration |
|-------------|---------------|
| `DamageNumber_Normal` | White, Scale 1x, Enable Pooling ☑️, Pool Size: 30 |
| `DamageNumber_Critical` | Yellow/Orange, Scale 1.5x, Enable Pooling ☑️ |
| `DamageNumber_Heal` | Green, Enable Pooling ☑️ |
| `DamageNumber_Miss` | Gray, Scale 0.8x, Enable Pooling ☑️ |
| `DamageNumber_Block` | Blue, Enable Pooling ☑️ |
| `DamageNumber_Absorb` | Cyan, Enable Pooling ☑️ |

### 3.2 Configure Adapter

1. Select `CombatUIManager` in Hierarchy
2. Add Component: `DamageNumbersProAdapter`
3. Assign prefabs:

| Field | Prefab |
|-------|--------|
| Normal Prefab | `DamageNumber_Normal` |
| Critical Prefab | `DamageNumber_Critical` |
| Heal Prefab | `DamageNumber_Heal` |
| Miss Prefab | `DamageNumber_Miss` |
| Block Prefab | `DamageNumber_Block` |
| Absorb Prefab | `DamageNumber_Absorb` |

4. Set **Spawn Offset**: `(0, 1.5, 0)`
5. Set **Random Offset Range**: `0.3`

---

## 4. UI Toolkit Views Setup

### 4.1 Create UI Document

1. **Hierarchy** → Right-click → UI Toolkit → **UI Document**
2. Rename to `CombatUI`
3. Set **Panel Settings** to your project's default
4. Set **Sort Order** to `100` (above game UI)

### 4.2 Add View Components

Add these components to GameObjects under your UI Document:

| Component | Parent Object | Notes |
|-----------|---------------|-------|
| `EnhancedHitmarkerView` | Center of screen | Screen-space overlay |
| `DirectionalDamageIndicatorView` | Full screen overlay | Screen-space |
| `ComboCounterView` | UI Document | Top-center |
| `KillFeedView` | UI Document | Top-right corner |
| `StatusEffectBarView` | UI Document | Below health bar |
| `CombatLogView` | UI Document | Bottom-left, collapsible |
| `BossHealthBarView` | UI Document | Top-center |

### 4.3 Create UXML Templates (Designer Task)

Create these files in `Assets/UI/Combat/`:

| File | Purpose |
|------|---------|
| `ComboCounter.uxml` | Combo count + timer bar |
| `KillFeed.uxml` | Kill entry list |
| `StatusEffectBar.uxml` | Status icon row |
| `CombatLog.uxml` | Scrollable log panel |
| `BossHealthBar.uxml` | Boss name + health + phases |

### 4.4 Create USS Stylesheets (Designer Task)

Create `Assets/UI/Combat/CombatUI.uss` with styles for:
- `.hitmarker`, `.hitmarker-critical`, `.hitmarker-kill`
- `.combo-counter`, `.combo-timer`
- `.kill-feed-entry`
- `.status-effect-icon`
- `.boss-health-bar`

---

## 5. Hitmarker Configuration

### 5.1 Create Hitmarker Config

1. **Assets** → Right-click → Create → DIG → Combat → **Hitmarker Config**
2. Save as `Assets/Data/Config/HitmarkerConfig.asset`

### 5.2 Configure Settings

| Section | Settings |
|---------|----------|
| **Sprites** | Assign hitmarker sprites (normal, critical, kill) |
| **Colors** | Normal: White, Critical: Yellow, Kill: Red |
| **Animation** | Scale Punch: 1.2, Fade Duration: 0.3s |
| **Audio** | Assign hit sounds (optional) |
| **Shader Material** | (Optional) Assign `CombatUI_Hitmarker.mat` for glow effects |

### 5.3 Assign to View

1. Select the `EnhancedHitmarkerView` GameObject
2. Drag `HitmarkerConfig.asset` to the **Config** field

---

## 6. FEEL Feedback Setup

### 6.1 Create Feedback Prefabs

Create these MMF_Player prefabs in `Assets/Data/Feedback/Combat/`:

| Prefab | Feedbacks to Add |
|--------|------------------|
| `MMF_CriticalHit` | MMF_ScreenFlash (yellow), MMF_CameraShake, MMF_FreezeFrame (0.05s) |
| `MMF_KillConfirm` | MMF_ScreenFlash (red), MMF_CameraShake (large), MMF_FreezeFrame (0.1s) |
| `MMF_ShieldBreak` | MMF_ScreenFlash (cyan), MMF_CameraShake (small) |
| `MMF_Parry` | MMF_ScreenFlash (gold), MMF_TimeScale (0.3 for 0.1s) |
| `MMF_LowHealth` | MMF_Vignette (red, pulsing), MMF_AudioSource (heartbeat loop) |

### 6.2 Assign to GameplayFeedbackManager

1. Find `GameplayFeedbackManager` in scene
2. Assign prefabs to the Combat Feedback section:

| Field | Prefab |
|-------|--------|
| Critical Hit Feedback | `MMF_CriticalHit` |
| Kill Confirm Feedback | `MMF_KillConfirm` |
| Shield Break Feedback | `MMF_ShieldBreak` |
| Parry Feedback | `MMF_Parry` |
| Low Health Feedback | `MMF_LowHealth` |

---

## 7. World-Space UI Setup (UI Toolkit)

### 7.1 PanelSettings for WorldSpace

1. **Assets** → Right-click → Create → UI Toolkit → **Panel Settings Asset**
2. Name it `WorldSpacePanelSettings`
3. Configure:
   - **Render Mode**: WorldSpace
   - **Target Texture**: None (direct rendering)
   - **Scale**: 0.01 (adjust based on world units)

### 7.2 Enemy Health Bars

1. Verify prefab exists at `Assets/Prefabs/UI/WorldSpace/EnemyHealthBar.prefab`
2. Prefab should have:
   - `UIDocument` component
   - `EnemyHealthBar` component
   - Assign `WorldSpacePanelSettings` to UIDocument

3. Configure `EnemyHealthBar` component:
   - **Health Bar Shader Material**: `CombatUI_HealthBar.mat` (optional for glow)
   - **Position Offset**: `(0, 2, 0)`
   - Colors: Green (healthy) → Yellow → Red (critical)

4. Add `EnemyHealthBarPool` to `CombatUIManager`:
   - Assign prefab
   - Pool Size: `30`
   - Show Distance: `25`
   - Fade After Damage Time: `3`

### 7.3 Floating Text

1. Verify prefab exists at `Assets/Prefabs/UI/FloatingText/FloatingTextElement.prefab`
2. Prefab should have:
   - `UIDocument` component
   - `FloatingTextElement` component
   - Assign `WorldSpacePanelSettings` to UIDocument

3. Configure `FloatingTextElement` component:
   - **Glow Material**: `CombatUI_Glow.mat` (optional)
   - Default Duration: `1.5`
   - Default Rise Speed: `1.0`

4. Add `FloatingTextManager` to `CombatUIManager`:
   - Assign prefab
   - Pool Size: `30`
   - Assign FloatingTextStyleConfig

### 7.4 Interaction Progress Ring

1. Verify prefab exists at `Assets/Prefabs/UI/WorldSpace/InteractionRing.prefab`
2. Prefab should have:
   - `UIDocument` component
   - `InteractionProgressRing` component
   - Assign `WorldSpacePanelSettings` to UIDocument

3. Configure `InteractionProgressRing` component:
   - **Radial Fill Material**: `CombatUI_RadialFill.mat` (optional for glow)
   - Active Color: Green
   - Completed Color: White
   - Cancelled Color: Red

4. Add `InteractionRingPool` to `CombatUIManager`:
   - Assign prefab
   - Pool Size: `10`

---

## 8. Configuration Assets

### 8.1 Create ScriptableObject Configs

| Asset | Menu Path | Save Location |
|-------|-----------|---------------|
| DamageNumberConfig | Create → DIG → Combat → Damage Number Config | `Assets/Data/Config/` |
| EnemyUIConfig | Create → DIG → Combat → Enemy UI Config | `Assets/Data/Config/` |
| FloatingTextStyleConfig | Create → DIG → Combat → Floating Text Style Config | `Assets/Data/Config/` |
| CombatFeedbackConfig | Create → DIG → Combat → Combat Feedback Config | `Assets/Data/Config/` |

### 8.2 Assign Configs

Assign these configs to their respective Manager/Adapter components in the scene.

---

## 9. Verification Checklist

After setup, verify each system works:

| Test | How to Verify |
|------|---------------|
| Damage Numbers | Attack an enemy - numbers should appear and pool correctly |
| Hitmarker | Hit an enemy - crosshair hitmarker should flash |
| Combo Counter | Chain hits quickly - counter should increment |
| Directional Damage | Take damage - edge indicator should point to source |
| Kill Feed | Kill an enemy - entry should appear top-right |
| Status Effects | Apply burn/poison - icon should appear in bar |
| Enemy Health Bars | Damage an enemy - world-space bar should appear (UI Toolkit) |
| Health Bar Trail | Rapid damage - red trail should lag behind green health |
| Interaction Ring | Hold interact key - radial progress should fill |
| Shader Glow | Health bar edges should have subtle glow (if material assigned) |

---

## 10. Troubleshooting

| Issue | Solution |
|-------|----------|
| Damage numbers not appearing | Check prefabs assigned to DamageNumbersProAdapter |
| Views not binding | Ensure CombatUIBootstrap is in scene with Auto Find enabled |
| Status effects not syncing | Verify StatusEffectPresentationSystem is running (ECS auto-creates) |
| Kill feed empty | Check CombatUIBridgeSystem is processing DeathEvents |
| No hitmarker | Verify EnhancedHitmarkerView has HitmarkerConfig assigned |
| World-space UI not visible | Check UIDocument has WorldSpace PanelSettings assigned |
| Shader not rendering | Ensure material uses correct URP shader (DIG/UI/*) |
| Health bar not billboarding | Check camera reference in EnemyHealthBar component |

---

## 11. Migration from UGUI

If you have old UGUI-based prefabs:

1. **DIG → Setup → Combat UI** → Click "🧹 Clean Old UGUI Files"
2. Or manually delete:
   - `EnemyHealthBar_UGUI.prefab`
   - `FloatingTextElement_UGUI.prefab`
   - `InteractionRing_UGUI.prefab`

3. Run "Create UI Toolkit Prefabs" to create new versions

---

## 12. File Reference

All implementation code is located at:

| System | Script Location |
|--------|-----------------|
| Bootstrap | `Assets/Scripts/Combat/UI/CombatUIBootstrap.cs` |
| Bridge System | `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` |
| ViewModels | `Assets/Scripts/Combat/UI/ViewModels/` |
| Views | `Assets/Scripts/Combat/UI/Views/` |
| Adapters | `Assets/Scripts/Combat/UI/Adapters/` |
| Configs | `Assets/Scripts/Combat/UI/Config/` |
| **Shaders** | `Assets/Shaders/UI/` |
| **Materials** | `Assets/Materials/UI/` |
| **Setup Tool** | `Assets/Scripts/Editor/Setup/CombatUISetupTool.cs` |
| **Menu State** | `Assets/Scripts/Visuals/UI/MenuState.cs` |
| **Input Reader** | `Assets/Scripts/Core/Input/PlayerInputReader.cs` |
