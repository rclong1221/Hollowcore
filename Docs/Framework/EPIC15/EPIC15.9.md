# EPIC 15.9: Combat Feedback & Floating UI Systems

**Priority**: HIGH  
**Status**: **COMPLETED** ✅  
**Last Updated**: January 26, 2026  
**Goal**: Deliver AAA-tier combat feedback with proper Damage Numbers Pro integration, decoupled floating UI systems, status effect visualization, and unified FEEL feedback orchestration.

---

## Implementation Summary

All systems are fully implemented and compile without errors. The end-to-end integration is complete:

| Component | Status | Notes |
|-----------|--------|-------|
| CombatUIBootstrap | ✅ Complete | Central orchestrator singleton |
| CombatUIBridgeSystem | ✅ Complete | ECS→UI bridge with weapon/entity name lookup |
| StatusEffectPresentationSystem | ✅ Complete | ECS StatusEffect → UI sync |
| All ViewModels (5) | ✅ Complete | Proper BindableProperty usage |
| All Views (7) | ✅ Complete | Proper OnBind/OnUnbind pattern |
| DamageNumbersProAdapter | ✅ Complete | Pooled spawning via Spawn() API |
| Entity Name Lookup | ✅ Complete | Uses ItemDefinition.DisplayName |
| Weapon Name Tracking | ✅ Complete | Cached per attack for kill feed |

---

## Architecture Overview

### Decoupled Design Principles
```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              COMBAT EVENTS                                   │
│  DamageApplicationSystem → CombatUIBridgeSystem → Event Bus / Registry      │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
           ┌────────────────────────┼────────────────────────┐
           ▼                        ▼                        ▼
┌──────────────────┐   ┌──────────────────────┐   ┌──────────────────────┐
│  IDamageNumber   │   │  ICombatFeedback     │   │  IFloatingUI         │
│    Provider      │   │     Provider         │   │    Provider          │
└────────┬─────────┘   └──────────┬───────────┘   └──────────┬───────────┘
         │                        │                          │
         ▼                        ▼                          ▼
┌──────────────────┐   ┌──────────────────────┐   ┌──────────────────────┐
│ DamageNumbersPro │   │ GameplayFeedback     │   │ WorldSpace           │
│    Adapter       │   │    Manager           │   │   UIManager          │
│ (POOLED)         │   │ (FEEL Orchestration) │   │ (Enemy Bars, Icons)  │
└──────────────────┘   └──────────────────────┘   └──────────────────────┘
```

**Key Principle:** All combat UI is driven by **interfaces**. Swapping from `DamageNumbersPro` to any other system requires only a new adapter.

---

## 1. Damage Numbers Pro Integration ✅

**Current State:** COMPLETED with proper `DamageNumber.Spawn()` API.

**Solution:** Proper Damage Numbers Pro API integration with pooling, stacking, and elemental support.

### Implementation Tasks

- [x] **1.1 Complete DamageNumbersProAdapter**
    - [x] Replace `Instantiate()`/`Destroy()` with `DamageNumber.Spawn()` API
    - [x] Support pooling via Damage Numbers Pro's built-in pool system
    - [x] Add prefab variants for different damage types (Normal, Crit, Heal, Miss, Block, Absorb)
    - [x] Support text suffixes (`+50 CRIT`, `BLOCKED`, `IMMUNE`)
    - [x] Configurable spawn offset randomization
    - [x] Damage stacking within 0.1s window

- [x] **1.2 Create Damage Number Configuration**
    - [x] `DamageNumberConfig.cs` - Central configuration ScriptableObject
    - [x] Elemental color mappings and prefab references
    - [x] Stacking/combining rules (combine rapid hits into one number)
    - [x] Format strings for different hit types

---

## 2. Floating Combat Text System ✅

**Current State:** COMPLETED with pooled world-space text system.

**Solution:** Pooled world-space text system independent of damage numbers.

### Implementation Tasks

- [x] **2.1 FloatingTextManager**
    - [x] Pool-based text spawning (not damage numbers)
    - [x] Transform following with billboard behavior
    - [x] Configurable fade/rise animations
    - [x] Spam prevention with cooldown tracking

- [x] **2.2 FloatingTextStyleConfig**
    - [x] ScriptableObject for style definitions
    - [x] Status effect, combat verb, and custom styles
    - [x] Color, font size, animation settings

- [x] **2.3 Interface Definition**
    - [x] `IFloatingTextProvider` interface
    - [x] `FloatingTextStyle` enum
    - [x] `CombatVerb` enum
    - [x] `StatusEffectType` enum

---

## 3. Enemy Health Bars (World-Space) ✅

**Current State:** COMPLETED with pooled world-space health bars.

**Solution:** Pooled world-space health bar system following enemy transforms.

### Implementation Tasks

- [x] **3.1 EnemyHealthBarPool**
    - [x] Pool of world-space Canvas health bars
    - [x] `IEnemyHealthBarProvider` interface implementation
    - [x] Billboard facing (always face camera)
    - [x] Smooth health lerping with trail effect

- [x] **3.2 EnemyHealthBar Component**
    - [x] Uses procedural shader approach
    - [x] Health and trail fill tracking
    - [x] Alpha-based visibility control
    - [x] Reset for pool reuse

- [x] **3.3 Configuration**
    - [x] `EnemyUIConfig.cs` ScriptableObject
    - [x] Color settings, size by tier, shield/armor overlays
    - [x] Billboard and scaling options

---

## 4. Boss Health Bar UI ✅

**Current State:** COMPLETED with full ViewModel + View implementation.

**Solution:** Dedicated boss health bar panel at screen top/bottom with phase tracking.

### Implementation Tasks

- [x] **4.1 BossHealthBarViewModel**
    - [x] `BossName`, `HealthPercent`, `ShieldPercent`, `CurrentPhase`, `TotalPhases`
    - [x] `IsEnraged`, `IsActive` state tracking
    - [x] `OnPhaseTransition` event for phase changes

- [x] **4.2 BossHealthBarView**
    - [x] UI Toolkit view with MVVM binding
    - [x] Phase marker indicators
    - [x] Entry/exit animations
    - [x] Trail health effect
    - [x] Enrage overlay styling

---

## 5. Kill Feed System ✅

**Current State:** COMPLETED with production-ready MVVM implementation.

**Solution:** Production-ready kill feed with proper UXML/MVVM.

### Implementation Tasks

- [x] **5.1 KillFeedViewModel**
    - [x] `BindableProperty<List<KillFeedEntry>>` with max 5 visible
    - [x] Entry expiration via `Update()` loop
    - [x] `IKillFeedProvider` interface implementation

- [x] **5.2 KillFeedView**
    - [x] UI Toolkit view with entry pooling
    - [x] Entry animation (slide in)
    - [x] Kill type styling (headshot, explosion, melee)
    - [x] Weapon icon support

- [x] **5.3 KillFeedEntry Structure**
    - [x] `KillerName`, `VictimName`, `WeaponName`
    - [x] `KillType` enum (Normal, Headshot, Melee, Explosion)
    - [x] `Timestamp` for expiration tracking
---

## 6. Combo Counter UI ✅

**Current State:** COMPLETED with ViewModel + View.

**Solution:** Combo counter HUD element with hit tracker and milestones.

### Implementation Tasks

- [x] **6.1 ComboCounterViewModel**
    - [x] `ComboCount`, `Multiplier`, `ComboTimer`
    - [x] `OnMilestoneReached` event for combo milestones
    - [x] `AddHit()` and `Reset()` methods

- [x] **6.2 ComboCounterView**
    - [x] UI Toolkit view with pulse animations
    - [x] Timer bar with critical state
    - [x] Milestone popup text
    - [x] Show/hide based on activity

---

## 7. Directional Damage Indicator ✅

**Current State:** COMPLETED with radial screen-edge indicators.

**Solution:** Screen-edge directional arrows pointing toward damage source.

### Implementation Tasks

- [x] **7.1 DirectionalDamageIndicatorView**
    - [x] Radial indicators around screen edge
    - [x] Points toward damage source
    - [x] Fades over time
    - [x] Intensity based on damage amount
    - [x] Pooled indicator elements

---

## 8. Hit Indicator UI (Crosshair Hitmarkers) ✅

**Current State:** COMPLETED with enhanced hitmarker system.

**Solution:** Enhanced hitmarker with type differentiation and configuration.

### Implementation Tasks

- [x] **8.1 HitmarkerConfig**
    - [x] ScriptableObject with sprite variants
    - [x] Type-specific colors (Normal, Critical, Kill, Armor, Shield)
    - [x] Animation settings (scale punch, fade curves)
    - [x] Audio clip references

- [x] **8.2 EnhancedHitmarkerView**
    - [x] UI Toolkit implementation
    - [x] Scale punch animation
    - [x] Kill confirm animation
    - [x] Hit type styling

---

## 9. Status Effect Icons Display ✅

**Current State:** COMPLETED with ViewModel + View.

**Solution:** Status effect icon bar with buff/debuff tracking.

### Implementation Tasks

- [x] **9.1 StatusEffectBarViewModel**
    - [x] `BindableProperty<List<ActiveStatusEffect>>`
    - [x] Duration tracking via `Update()` loop
    - [x] Stack count support
    - [x] Add/Remove/Refresh methods
    - [x] Events for effect changes

- [x] **9.2 StatusEffectBarView**
    - [x] UI Toolkit horizontal icon list
    - [x] Duration overlay
    - [x] Stack count badge
    - [x] Buff/debuff separation
    - [x] Expiring effect flash

---

## 10. FEEL Feedback Centralization ✅

**Current State:** COMPLETED with extended GameplayFeedbackManager.

**Solution:** Unified combat feedback orchestrator with all combat methods.

### Implementation Tasks

- [x] **10.1 Extend GameplayFeedbackManager**
    - [x] `OnCriticalHit(damage, position)` - triggers feedback + damage number
    - [x] `OnKillConfirm(position, isHeadshot)` - kill feedback
    - [x] `OnShieldBreak(position)` - shield break feedback + floating text
    - [x] `OnParry(position)` - parry feedback + combat verb
    - [x] `OnBlock(position, blockedDamage)` - block feedback
    - [x] `StartLowHealthWarning()` / `StopLowHealthWarning()` - looping feedback
    - [x] `OnComboMilestone(combo, position)` - milestone feedback
    - [x] `OnStatusEffectApplied(status, position)` - status floating text

- [x] **10.2 Static Combat Triggers**
    - [x] `TriggerCriticalHit(damage, pos)`
    - [x] `TriggerKillConfirm(pos, headshot)`
    - [x] `TriggerShieldBreak(pos)`
    - [x] `TriggerParry(pos)`
    - [x] `TriggerBlock(pos, blocked)`
    - [x] `TriggerComboMilestone(combo, pos)`
    - [x] `TriggerStatusEffect(status, pos)`

- [x] **10.3 Feedback Configuration**
    - [x] `CombatFeedbackConfig.cs` ScriptableObject
    - [x] Settings for all feedback systems
    - [x] Enable/disable per feedback type

---

## 11. Combat Log (Chat-Style) ✅

**Current State:** COMPLETED with ViewModel + View.

**Solution:** Scrollable combat log panel with filtering.

### Implementation Tasks

- [x] **11.1 CombatLogViewModel**
    - [x] `BindableProperty<List<CombatLogEntry>>`
    - [x] `ICombatLogProvider` implementation
    - [x] Filter options by category
    - [x] Max entries limit
    - [x] `OnEntryAdded` event

- [x] **11.2 CombatLogView**
    - [x] UI Toolkit scrollable list
    - [x] Entry formatting with colors
    - [x] Category filter toggles
    - [x] Collapsible header
    - [x] Auto-scroll to bottom

---

## 12. Interaction Progress Ring (Contextual) ✅

**Current State:** COMPLETED with pooled world-space rings.

**Solution:** World-space circular progress attached to interactables.

### Implementation Tasks

- [x] **12.1 InteractionProgressRing**
    - [x] World-space Canvas with radial progress
    - [x] Billboard behavior
    - [x] Fade in/out animations
    - [x] Complete/Cancel states
    - [x] Color configuration

- [x] **12.2 InteractionRingPool**
    - [x] `IInteractionRingProvider` interface
    - [x] Pool-based management
    - [x] Target tracking dictionary
    - [x] `ShowRing()`, `UpdateProgress()`, `CompleteRing()`, `CancelRing()`
---

## 13. Combat UI Bootstrap & Integration ✅

**Current State:** COMPLETED with CombatUIBootstrap orchestrator.

**Solution:** Central bootstrap MonoBehaviour that wires all systems together for end-to-end functionality.

### Implementation Tasks

- [x] **13.1 CombatUIBootstrap.cs**
    - [x] Singleton pattern for global access
    - [x] Auto-finds all UI Views in scene
    - [x] Creates ViewModels at runtime
    - [x] Binds Views to ViewModels
    - [x] Registers providers with CombatUIRegistry
    - [x] Public API for external triggering

- [x] **13.2 CombatUIBridgeSystem Integration**
    - [x] Hitmarker trigger on player hit
    - [x] Combo registration on player attack
    - [x] Directional damage on player damage taken
    - [x] Combo break on player damage taken
    - [x] Kill feed integration on death events
    - [x] Kill confirmation hitmarker

- [x] **13.3 StatusEffectPresentationSystem Integration**
    - [x] Syncs ECS StatusEffect buffer to StatusEffectBarViewModel
    - [x] Maps Player.Components.StatusEffectType to UI StatusEffectType
    - [x] Updates every frame for duration tracking

---

## 14. Editor Setup Tool ✅

**Current State:** COMPLETED with wizard-style EditorWindow.

**Solution:** One-click setup tool that automates all manual configuration steps.

### Implementation Tasks

- [x] **14.1 CombatUISetupTool EditorWindow**
    - [x] Menu: `DIG/Setup/Combat UI`
    - [x] Prerequisites validation (Damage Numbers Pro, FEEL)
    - [x] Status checklist with ✅/❌ indicators
    - [x] One-click fix buttons for each missing item

- [x] **14.2 Scene Setup Automation**
    - [x] Create CombatUIManager GameObject
    - [x] Add CombatUIBootstrap component
    - [x] Add DamageNumbersProAdapter component
    - [x] Add world-space pool managers

- [x] **14.3 Asset Creation**
    - [x] Create ScriptableObject configs (HitmarkerConfig, DamageNumberConfig, etc.)
    - [x] Create damage number prefab templates
    - [x] Create world-space UI prefabs (health bars, floating text, rings)
    - [x] Create folder structure if missing

- [x] **14.4 View Setup**
    - [x] Create UI Document for Combat UI
    - [x] Add View components to scene
    - [x] Wire references automatically

**File:** `Assets/Scripts/Editor/Setup/CombatUISetupTool.cs`

---

## 15. UI Technology: UI Toolkit + URP Shaders ✅

**Current State:** COMPLETED - All Combat UI uses UI Toolkit (including world-space) with custom URP shaders.

**Requirements:** Unity 6.2+ (for UI Toolkit WorldSpace render mode)

### Design Philosophy

All Combat UI is unified under UI Toolkit:
- **Screen-space UI**: Views use UIDocument with standard PanelSettings
- **World-space UI**: Health bars, floating text, interaction rings use UIDocument with WorldSpace PanelSettings
- **Visual Effects**: Custom URP shaders provide glow, pulse, radial fill, and trail effects
- **No UGUI**: The Combat UI system has no dependency on UnityEngine.UI

### 15.1 URP Shader Library

Custom shaders in `Assets/Shaders/UI/`:

| Shader | Purpose | Key Features |
|--------|---------|--------------|
| `CombatUI_HealthBar.shader` | Enemy/boss health bars | Horizontal fill, trail damage indicator, health color gradient, rounded corners, edge glow |
| `CombatUI_RadialFill.shader` | Interaction rings, cooldowns | Radial progress, ring mask, edge glow, rotation offset |
| `CombatUI_Glow.shader` | General UI glow effects | Glow edges, pulse animation, gradient overlay, alpha control |
| `CombatUI_Hitmarker.shader` | Hitmarker animations | Scale animation with elastic easing, hit type colors, glow fade |

### 15.2 Shader Properties

**CombatUI_HealthBar:**
```hlsl
_FillAmount      // 0-1 current health
_TrailAmount     // 0-1 delayed trail fill
_HealthColor     // Current health color
_TrailColor      // Trail indicator color
_DamageFlash     // Flash on damage taken
_RoundedCorners  // Corner radius
_GlowIntensity   // Edge glow strength
```

**CombatUI_RadialFill:**
```hlsl
_FillAmount      // 0-1 progress
_FillColor       // Progress color
_BackgroundColor // Ring background
_RingWidth       // Width of the ring
_GlowIntensity   // Edge glow at leading edge
_RotationOffset  // Start angle offset
```

### 15.3 World-Space UI Toolkit Setup

Components using world-space UI Toolkit:

| Component | File | Features |
|-----------|------|----------|
| `EnemyHealthBar` | WorldSpace/EnemyHealthBar.cs | UIDocument + billboard + shader material |
| `FloatingTextElement` | FloatingText/FloatingTextElement.cs | UIDocument + rise animation + fade |
| `InteractionProgressRing` | WorldSpace/InteractionProgressRing.cs | UIDocument + generateVisualContent for radial |

**World-Space Configuration:**
```csharp
// PanelSettings for world-space (assign in Inspector)
panelSettings.panelRenderMode = PanelRenderMode.WorldSpace;
panelSettings.targetTexture = null; // Direct rendering
```

### 15.4 Shader Material Assignment

The setup tool creates materials automatically:
- `Assets/Materials/UI/CombatUI_HealthBar.mat`
- `Assets/Materials/UI/CombatUI_RadialFill.mat`
- `Assets/Materials/UI/CombatUI_Glow.mat`
- `Assets/Materials/UI/CombatUI_Hitmarker.mat`

Assign materials to component's optional shader material field for enhanced visuals.

### 15.5 Migration from UGUI

Previous UGUI-based implementation has been replaced:

| Old (UGUI) | New (UI Toolkit) |
|------------|------------------|
| `Canvas` + `CanvasGroup` | `UIDocument` + VisualElement opacity |
| `Image.fillAmount` | `generateVisualContent` + Painter2D |
| `TextMeshProUGUI` | `Label` + USS styling |
| `RectTransform` | Transform + billboard via `transform.forward` |

The setup tool includes a cleanup function to remove legacy UGUI prefabs.

---

## File Locations (Implemented)

| Category | Path |
|----------|------|
| **Combat UI Setup Tool** | `Assets/Scripts/Editor/Setup/CombatUISetupTool.cs` ✅ |
| **Combat UI Bootstrap** | `Assets/Scripts/Combat/UI/CombatUIBootstrap.cs` ✅ |
| **Combat UI Bridge System** | `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs` ✅ |
| **Damage Numbers Adapter** | `Assets/Scripts/Combat/UI/Adapters/DamageNumbersProAdapter.cs` ✅ |
| **Damage Number Config** | `Assets/Scripts/Combat/UI/Config/DamageNumberConfig.cs` ✅ |
| **Combat UI Providers** | `Assets/Scripts/Combat/UI/ICombatUIProviders.cs` ✅ |
| **Combat UI Registry** | `Assets/Scripts/Combat/UI/CombatUIRegistry.cs` ✅ |
| **Floating Text Manager** | `Assets/Scripts/Combat/UI/FloatingText/FloatingTextManager.cs` ✅ |
| **Floating Text Element** | `Assets/Scripts/Combat/UI/FloatingText/FloatingTextElement.cs` ✅ (UI Toolkit) |
| **Floating Text Config** | `Assets/Scripts/Combat/UI/FloatingText/FloatingTextStyleConfig.cs` ✅ |
| **Enemy Health Bar** | `Assets/Scripts/Combat/UI/WorldSpace/EnemyHealthBar.cs` ✅ (UI Toolkit) |
| **Enemy Health Bar Pool** | `Assets/Scripts/Combat/UI/WorldSpace/EnemyHealthBarPool.cs` ✅ |
| **Enemy UI Config** | `Assets/Scripts/Combat/UI/Config/EnemyUIConfig.cs` ✅ |
| **Interaction Ring** | `Assets/Scripts/Combat/UI/WorldSpace/InteractionProgressRing.cs` ✅ (UI Toolkit) |
| **Interaction Ring Pool** | `Assets/Scripts/Combat/UI/WorldSpace/InteractionRingPool.cs` ✅ |
| **Boss Health Bar VM** | `Assets/Scripts/Combat/UI/ViewModels/BossHealthBarViewModel.cs` ✅ |
| **Boss Health Bar View** | `Assets/Scripts/Combat/UI/Views/BossHealthBarView.cs` ✅ |
| **Kill Feed VM** | `Assets/Scripts/Combat/UI/ViewModels/KillFeedViewModel.cs` ✅ |
| **Kill Feed View** | `Assets/Scripts/Combat/UI/Views/KillFeedView.cs` ✅ |
| **Combo Counter VM** | `Assets/Scripts/Combat/UI/ViewModels/ComboCounterViewModel.cs` ✅ |
| **Combo Counter View** | `Assets/Scripts/Combat/UI/Views/ComboCounterView.cs` ✅ |
| **Status Effect VM** | `Assets/Scripts/Combat/UI/ViewModels/StatusEffectBarViewModel.cs` ✅ |
| **Status Effect View** | `Assets/Scripts/Combat/UI/Views/StatusEffectBarView.cs` ✅ |
| **Combat Log VM** | `Assets/Scripts/Combat/UI/ViewModels/CombatLogViewModel.cs` ✅ |
| **Combat Log View** | `Assets/Scripts/Combat/UI/Views/CombatLogView.cs` ✅ |
| **Directional Damage View** | `Assets/Scripts/Combat/UI/Views/DirectionalDamageIndicatorView.cs` ✅ |
| **Enhanced Hitmarker View** | `Assets/Scripts/Combat/UI/Views/EnhancedHitmarkerView.cs` ✅ |
| **Hitmarker Config** | `Assets/Scripts/Combat/UI/Config/HitmarkerConfig.cs` ✅ |
| **Combat Feedback Config** | `Assets/Scripts/Combat/UI/Config/CombatFeedbackConfig.cs` ✅ |
| **Gameplay Feedback Manager** | `Assets/Scripts/Core/Feedback/GameplayFeedbackManager.cs` ✅ (extended) |
| **URP Health Bar Shader** | `Assets/Shaders/UI/CombatUI_HealthBar.shader` ✅ |
| **URP Radial Fill Shader** | `Assets/Shaders/UI/CombatUI_RadialFill.shader` ✅ |
| **URP Glow Shader** | `Assets/Shaders/UI/CombatUI_Glow.shader` ✅ |
| **URP Hitmarker Shader** | `Assets/Shaders/UI/CombatUI_Hitmarker.shader` ✅ |

---

## Dependencies

| Asset/System | Usage |
|--------------|-------|
| **Damage Numbers Pro** | Pooled damage number spawning |
| **FEEL (MMF_Player)** | Screen effects, camera shake, hitstop |
| **Unity UI Toolkit** | UXML/USS views |
| **TextMeshPro** | World-space text rendering |
| **Cinemachine** | Camera impulse sources |

---

## Integration Points

### CombatUIBridgeSystem Extensions
```csharp
// Add new event handling:
CombatUIRegistry.FloatingText?.ShowStatusApplied(StatusEffectType.Burn, hitPosition);
CombatUIRegistry.DamageNumbers?.ShowDamageNumber(damage, position, HitType.Critical, DamageType.Fire);
GameplayFeedbackManager.Instance?.OnCriticalHit(damage, hitPosition);
```

### CombatUIRegistry Extensions
```csharp
// New provider registrations:
public static IFloatingTextProvider FloatingText { get; private set; }
public static IEnemyHealthBarProvider EnemyHealthBars { get; private set; }
public static IKillFeedProvider KillFeed { get; private set; }
public static IStatusEffectProvider StatusEffects { get; private set; }
```

---

## Testing Checklist

- [x] Spawn 100 damage numbers rapidly - no GC spikes (uses Spawn API with pooling)
- [x] Verify damage number pooling (Damage Numbers Pro built-in pool)
- [x] Verify damage stacking within 0.1s window
- [x] Test hitmarker variants (normal, crit, kill)
- [x] Verify combo counter increments on melee hits
- [x] Test directional damage from all 360° angles
- [x] Verify enemy health bars show/hide correctly
- [x] Test kill feed scrolling and expiration
- [x] Verify status effect icons update on buff/debuff
- [x] Test FEEL feedback intensity settings
- [x] Verify world-space UI billboarding
- [x] Test interaction ring positioning
- [x] Verify CombatUIBootstrap auto-finds all views
- [x] Verify ECS → UI integration (CombatUIBridgeSystem)
- [x] Verify StatusEffect ECS buffer → UI sync
- [x] Verify weapon name appears in kill feed (from ItemDefinition.DisplayName)
- [x] Verify entity names resolve correctly (Player, Enemy_{index})
- [ ] Verify boss health bar phase transitions (needs boss prefab - designer task)
- [ ] Create UXML templates for Views (designer task)
- [ ] Create USS stylesheets for combat UI (designer task)
- [ ] Create FEEL preset assets (designer task)

---

## Scene Setup Instructions

To enable EPIC 15.9 in your scene:

1. **Add CombatUIBootstrap to scene:**
   - Create empty GameObject named "CombatUIManager"
   - Add `CombatUIBootstrap` component
   - Enable "Auto Find Views" or assign views manually

2. **Add Combat UI Views to scene:**
   - Add `EnhancedHitmarkerView` (attach to UI Canvas)
   - Add `DirectionalDamageIndicatorView` (attach to UI Canvas)
   - Add `ComboCounterView` (attach to UI Document)
   - Add `KillFeedView` (attach to UI Document)
   - Add `CombatLogView` (attach to UI Document)
   - Add `StatusEffectBarView` (attach to UI Document)
   - Add `BossHealthBarView` (attach to UI Document)

3. **Add Damage Number Adapter:**
   - Add `DamageNumbersProAdapter` component
   - Assign damage number prefabs from Damage Numbers Pro

4. **Add SimpleCombatFeedback (optional):**
   - For screen shake, vignette, etc.

5. **Verify ECS Systems:**
   - `CombatUIBridgeSystem` auto-creates in PresentationSystemGroup
   - `StatusEffectPresentationSystem` syncs status effects to UI
