# EPIC 15.8: Core Architecture & UI Overhaul (AAA Platinum)

## Goal
To implement foundational "Game Engine" features and establish a **AAA-grade, professional UI architecture** using **Unity UI Toolkit** and **MVVM**. This ensures the game has a cohesive visual identity, is fully accessible, supports all input devices seamlessly, and decouples logic from presentation.

> [!IMPORTANT]
> **Mandate:** All new UI MUST use **UI Toolkit** (UXML/USS).
> **Strict MVVM:** UI Logic (Views) MUST NOT reference Gameplay Scripts (Monobehaviours) directly. All communication must occur via **ViewModels** and **Data Binding**.
> **Performance:** `Update()` polling in UI Views is **STRICTLY PROHIBITED**. Use `BindableProperty<T>` or Events.

---

## 1. UI Architecture (MVVM & DOTS)
*   **Status:** **FRAGMENTED/LEGACY**.
*   **Requirement:** Decoupled, data-driven architecture.
*   **Core Systems:**
    *   **MVVM Pattern:**
        *   **Model:** ECS Components / Data.
        *   **ViewModel:** Plain C# classes (`HealthViewModel`, `InventoryViewModel`) that expose reactive properties (`BindableProperty<T>`).
        *   **View:** UXML/C# that binds to ViewModel properties.
    *   **Style Sheets (USS):** Global design system variables (Colors, Spacing, Typography).
    *   **Navigation Stack:** History-based navigation (`Push`, `Pop`, `Back`) ensuring `Escape`/`B-Button` always behaves correctly.

## 2. Input & Feedback Abstraction
*   **Status:** **PARTIAL**. `GameplayFeedbackManager` exists for Combat/Movement, but UI Feedback is missing.
*   **Requirement:** Console certification readiness.
*   **System:** `InputGlyphProvider` (**MISSING**)
    *   **Dynamic Parsing:** Transforms text like `"Press <Action:Interact>"` into icons at runtime.
    *   **Device Awareness:** Swaps icons instantly when switching between Keyboard, Xbox, PlayStation, or Switch controllers.
*   **System:** `UIAudioManager` (**MISSING / INTEGRATE**)
    *   **Centralized Feedback:** Global handling of Hover, Click, Submit, Cancel, and Error sounds.
    *   **Integration:** Must hook into existing `GameplayFeedbackManager` or use separate `MMF_Player` instances for UI events.

## 3. Localization, Accessibility & Settings
*   **System:** `LocalizationManager`
    *   **Key-Value:** All text fields bind to a Localization Key, never raw strings.
    *   **Smart Strings:** interpolation support (`"Picked up {0} x{1}"`).
*   **System:** `AccessibilityService`
    *   **TTS:** Hooks for UI-to-Speech synthesis.
    *   **Scaling:** Runtime UI Scale factor (100% - 200%).
    *   **Daltonization:** Colorblind mode filters.
*   **System:** `UserPreferences`
    *   **Centralized:** One file for Video, Audio, Input, Gameplay.
    *   **Auto-Save:** Writes to disk on modification (debounced).

---

## Implementation Tasks

### Core Architecture (AAA)
- [x] **Establish MVVM Framework:** ✅ *Completed in 15.8.1*
    - [x] Create `ViewModelBase` class (implement `INotifyPropertyChanged`). → `Assets/Scripts/UI/Core/MVVM/ViewModelBase.cs`
    - [x] Create `BindableProperty<T>` wrapper for reactive updates. → `Assets/Scripts/UI/Core/MVVM/BindableProperty.cs`
    - [x] Create `UIView<TViewModel>` base class for binding setup/teardown. → `Assets/Scripts/UI/Core/MVVM/UIView.cs`
- [x] **Implement Navigation Stack:** ✅ *Completed in 15.8.1*
    - [x] `NavigationManager`: Maintains `Stack<UIWindow>`. → `Assets/Scripts/UI/Core/Navigation/NavigationManager.cs`
    - [x] Handle "Back" input globally. → Escape key handling built-in
    - [x] Support "Modal" vs "Screen" layers. → `UILayer` enum (Screen, Modal, HUD, Tooltip)
- [ ] **Implement Input Glyph System:**
    - [ ] Create ScriptableObject database for Input Icons (Texture2D per Key/Button).
    - [ ] Create `RichTextTagProcessor` to convert `<Action:Jump>` to sprite tags.
- [ ] **Integrate UI Audio:**
    - [ ] Create `UIAudioManager` wrapper around `GameplayFeedbackManager` or new MMF instances.
    - [ ] Define standard UI Feedback events (Hover, Click, Submit, Cancel, Error).

### Design System (USS)
- [x] **Define Global Styles:** ✅ *Completed in 15.8.1*
    - [x] `Variables.uss`: Colors (Primary, Secondary, Accent, Danger), Fonts, Spacing Metrics. → `Assets/UI/Styles/Variables.uss`
    - [x] `Components.uss`: Reusable classes (`.pro-button`, `.panel-glass`, `.text-header`). → `Assets/UI/Styles/Components.uss`
    - [ ] `ThemeManager`: Runtime swapping (e.g., High Contrast Mode).

### Legacy Editor UI Rewrite (IMGUI -> UI Toolkit)
> [!WARNING]
> Refactor these to use the new MVVM patterns where applicable, or at least clean VisualElement composition.
- [ ] **Character Workstation:** Port to `Toolbar` and `TabView` using `ListView` for performance.
- [ ] **Equipment Workstation:** Create custom Property Drawers in UI Toolkit.
- [ ] **Debug / Combat / VFX / Utilities Workstations:** Port to UXML.

### Runtime UI Rewrite (UGUI -> UI Toolkit)
> [!IMPORTANT]
> Rewrite `HealthHUD.cs` to remove `Update()` polling. Use ECS Systems to push updates to the ViewModel.
- [x] **Health/Status HUD:** ✅ *Completed in 15.8.1*
    - [x] `HealthViewModel`: Exposes `CurrentHealth`, `MaxHealth`, `Shields`. → `Assets/Scripts/UI/ViewModels/HealthViewModel.cs`
    - [x] `HealthHUDView`: UI Toolkit View with reactive bindings. → `Assets/Scripts/UI/Views/HealthHUDView.cs`
    - [x] `HealthViewModelSyncSystem`: ECS push pattern. → `Assets/Scripts/UI/Systems/HealthViewModelSyncSystem.cs`
    - [x] `HealthHUD.uxml`: UXML template. → `Assets/UI/Templates/HealthHUD.uxml`
    - [ ] `DamageFeedback`: World-space floating numbers (pooled VisualElements).
- [ ] **FPS Display:** Port to dedicated debug layer.

### New Essential UIs
- [ ] **Interaction Prompts (World Space):**
    - [ ] `FloatingElement`: Follows transform position.
    - [ ] Uses `InputGlyphProvider` for dynamic prompt (e.g., [F] vs (X)).
- [ ] **Combat Reticle:**
    - [ ] Dynamic expansion based on weapon spread (ViewModel property).
    - [ ] Hit confirmation markers.
- [ ] **Inventory & Loadout:**
    - [ ] Grid-based `ListView` with item inspection side-panel.
    - [ ] Drag-and-drop support (using UI Toolkit Manipulators).
- [ ] **Menus (Main/Pause):**
    - [ ] Full keyboard/gamepad navigation support (Focus Engine).

### Procedural UI Bars System (15.8.2)
> GPU-accelerated bars with shader-based rendering and MVVM pattern.

#### Shaders
- [x] `UI_ProceduralHealthBar.shader` - Critical pulse, damage flash
- [x] `UI_ProceduralShieldBar.shader` - Hexagon pattern, recharge shimmer
- [x] `UI_ProceduralStaminaBar.shader` - Drain/recovery pulse
- [x] `UI_ProceduralBatteryBar.shader` - Cell segments, flicker effect
- [x] `UI_ProceduralOxygenBar.shader` - Bubble animation, suffocation pulse
- [x] `UI_ProceduralHungerBar.shader` - Stomach growl shake
- [x] `UI_ProceduralThirstBar.shader` - Water droplet particles
- [x] `UI_ProceduralSanityBar.shader` - Distortion, color desaturation
- [x] `UI_ProceduralInfectionBar.shader` - Spreading veins, green pulse
- [x] `UI_ProceduralCooldownBar.shader` - Circular cooldown sweep
- [x] `UI_ProceduralChargesBar.shader` - Discrete segments (3/3 style)
- [x] `UI_ProceduralDurabilityBar.shader` - Crack overlay when low
- [x] `UI_ProceduralNoiseMeter.shader` - Sound wave ripples
- [x] `UI_ProceduralDetectionBar.shader` - Eye-opening animation
- [x] `UI_ProceduralInteractionBar.shader` - Circular progress ring

#### ECS Components
- [x] `PlayerShield.cs` - Energy shield with recharge
- [x] `PlayerOxygen.cs` - Hazard zone breathing
- [x] `PlayerHunger.cs` - Survival hunger (inverted: 0=full)
- [x] `PlayerThirst.cs` - Survival thirst (inverted: 0=hydrated)
- [x] `PlayerSanity.cs` - Horror mental state
- [x] `PlayerInfection.cs` - Poison/infection spreading
- [x] `PlayerNoise.cs` - Stealth noise level
- [x] `PlayerDetection.cs` - Enemy awareness
- [x] `InteractionProgress.cs` - Hold-to-interact
- [x] `AbilityCharges.cs` - Discrete ability charges
- [x] `WeaponDurability.cs` - Weapon condition

#### ViewModels & Views
- [x] All 12 ViewModels in `Assets/Scripts/Player/UI/ViewModels/`
- [x] All 12 ShaderViews in `Assets/Scripts/Player/UI/Views/`

#### Authoring Components (Optional Features)
- [x] `SurvivalNeedsAuthoring.cs` - Hunger, Thirst, Oxygen
- [x] `HorrorStatusAuthoring.cs` - Sanity, Infection
- [x] `ShieldAuthoring.cs` - Player energy shield
- [x] `AbilityChargesAuthoring.cs` - Discrete ability charges
- [x] `WeaponDurabilityAuthoring.cs` - Weapon durability (on weapon entities)

#### Gameplay Systems
- [x] `OxygenSystem.cs` - Drains in hazard zones, suffocation damage
- [x] `SurvivalNeedsSystem.cs` - Hunger/thirst passive drain, starvation damage
- [x] `SanitySystem.cs` - Darkness/horror drain, safe zone recovery
- [x] `InfectionSystem.cs` - Infection spreading, scaled damage
- [x] `ShieldRechargeSystem.cs` - Shield regeneration after delay
- [x] `AbilityChargeSystem.cs` - Sequential/parallel charge recharge
- [x] `WeaponDurabilitySystem.cs` - Break state, destroy on break

#### Editor Tools
- [x] UISetupWizard scrollability + bar creation buttons
- [x] EditorWindowUtilities shared styling class
