# EPIC 15.8 Setup Guide: UI Architecture (MVVM & DOTS)

Step-by-step instructions to set up the UI systems in Unity Editor.

---

## Table of Contents

1. [Health HUD Setup](#1-health-hud-setup)
2. [Creating a New ViewModel + View](#2-creating-a-new-viewmodel--view)
3. [Navigation System Setup](#3-navigation-system-setup)
4. [Design System (USS)](#4-design-system-uss)
5. [Input Glyph System Setup](#5-input-glyph-system-setup)
6. [Procedural UI Bars Setup](#6-procedural-ui-bars-setup)
7. [Shader Effects Reference](#7-shader-effects-reference)

---

## 1. Health HUD Setup

### Editor Steps

1. **Create UI Document GameObject:**
   - Hierarchy → Right-click → **UI Toolkit → UI Document**
   - Name it `HealthHUD`

2. **Assign UXML Template:**
   - Select the UI Document
   - In Inspector, set **Source Asset**: `Assets/UI/Templates/HealthHUD.uxml`

3. **Add View Component:**
   - Click **Add Component** → search `HealthHUDView`
   - ✅ `Auto Create View Model` (default on)

4. **Configure Panel Settings:**
   - Assign your PanelSettings asset to the UI Document
   - Set **Sort Order** for layering (higher = on top)

### Customizing Colors (Optional)

Edit `Assets/UI/Styles/Variables.uss`:

```css
--color-health-full: #2ECC71;
--color-health-critical: #922B21;
```

---

## 2. Creating a New ViewModel + View

### Step 1: Create ViewModel

Create `Assets/Scripts/Player/UI/ViewModels/MyViewModel.cs`:

```csharp
using DIG.UI.Core.MVVM;

public class MyViewModel : ViewModelBase
{
    public BindableProperty<int> Score { get; } = new(0);
    public BindableProperty<string> Message { get; } = new("");
}
```

### Step 2: Create UXML Template

Create `Assets/UI/Templates/MyUI.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="project://database/Assets/UI/Styles/Variables.uss" />
    <Style src="project://database/Assets/UI/Styles/Components.uss" />
    
    <ui:VisualElement class="panel">
        <ui:Label name="score-label" text="Score: 0" class="text-header" />
        <ui:Label name="message-label" text="" class="text-body" />
    </ui:VisualElement>
</ui:UXML>
```

### Step 3: Create View

Create `Assets/Scripts/Player/UI/Views/MyView.cs`:

```csharp
using DIG.UI.Core.MVVM;

public class MyView : UIView<MyViewModel>
{
    [SerializeField] private bool _autoCreate = true;
    
    protected override void Start()
    {
        if (_autoCreate) Bind(new MyViewModel());
    }
    
    protected override void OnBind()
    {
        BindLabel("score-label", ViewModel.Score, s => $"Score: {s}");
        BindLabel("message-label", ViewModel.Message);
    }
}
```

### Step 4: Setup in Scene

1. Create **UI Document** GameObject
2. Assign `MyUI.uxml` as Source Asset
3. Add **MyView** component

---

## 3. Navigation System Setup

### Using Navigation in Code

```csharp
using DIG.UI.Core.Navigation;

// Push a screen
NavigationManager.Instance.Push("Inventory", inventoryRoot, UILayer.Screen);

// Push a modal on top
NavigationManager.Instance.Push("Confirm", confirmRoot, UILayer.Modal);

// Go back (Escape key works automatically)
NavigationManager.Instance.Pop();
```

### Layer Types

| Layer | Behavior |
|-------|----------|
| `Screen` | Replaces previous screen |
| `Modal` | Stacks on top, previous visible |
| `HUD` | Always visible |
| `Tooltip` | Highest layer |

---

## 4. Design System (USS)

### Adding Styles to UXML

```xml
<Style src="project://database/Assets/UI/Styles/Variables.uss" />
<Style src="project://database/Assets/UI/Styles/Components.uss" />
```

### Common Classes

| Class | Usage |
|-------|-------|
| `.pro-button` | Standard button |
| `.pro-button--danger` | Destructive action |
| `.panel` | Container with border |
| `.panel-glass` | Glass-morphism panel |
| `.text-header` | Large heading |
| `.text-body` | Body text |
| `.health-bar` | Health bar container |
| `.row` / `.col` | Flex direction |
| `.gap-sm/md/lg` | Spacing between children |
| `.p-sm/md/lg` | Padding |

### Color Variables

| Variable | Usage |
|----------|-------|
| `--color-primary` | Brand color |
| `--color-accent` | Highlights |
| `--color-success` | Positive feedback |
| `--color-danger` | Errors, destructive |
| `--color-health-full/critical` | Health states |

---

## 5. Input Glyph System Setup

Shows device-appropriate input prompts (keyboard vs gamepad icons).

### Step 1: Create Glyph Database

1. **Create folder:** `Assets/Resources/` (if it doesn't exist)
2. **Create asset:** Right-click → **Create → DIG/UI → Input Glyph Database**
3. **Name it exactly:** `InputGlyphDatabase`

### Step 2: Configure Entries

In Inspector, add entries for each action:

| Field | Example Value |
|-------|---------------|
| Action Name | `Interact` |
| Keyboard Icon | (assign F_Key sprite) |
| Keyboard Text | `[F]` |
| Xbox Icon | (assign Xbox_X sprite) |
| Xbox Text | `(X)` |
| PlayStation Icon | (assign PS_Square sprite) |
| PlayStation Text | `(□)` |

**Tip:** Right-click the database → **Add Common Actions** to pre-populate.

### Step 3: Use in Code

```csharp
using DIG.UI.Core.Input;

// Get text for current device
string text = InputGlyphProvider.GetText("Interact"); // "[F]" or "(X)"

// Process a prompt string
string prompt = InputGlyphProvider.ProcessText("Press <Action:Interact> to open");
// Result: "Press [F] to open" (keyboard) or "Press (X) to open" (gamepad)

// Get icon sprite
Sprite icon = InputGlyphProvider.GetIcon("Jump");
```

### Supported Devices

| Device | Detection |
|--------|-----------|
| Keyboard/Mouse | Default |
| Xbox | XInput controllers |
| PlayStation | DualShock/DualSense |
| Switch | Nintendo controllers |

---

## File Locations Reference

```
Assets/
├── Scripts/
│   ├── UI/Core/MVVM/           # Framework (BindableProperty, UIView)
│   ├── UI/Core/Navigation/     # NavigationManager
│   ├── UI/Core/Input/          # Input Glyph System
│   └── Player/UI/              # Game ViewModels & Views
│
├── UI/
│   ├── Styles/Variables.uss    # Color/spacing variables
│   ├── Styles/Components.uss   # Reusable classes
│   └── Templates/*.uxml        # UI templates
│
└── Resources/
    └── InputGlyphDatabase.asset # Input icons (auto-loaded)
```

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| UI not updating | Check `Bind(viewModel)` is called |
| Element not found | Element name is case-sensitive |
| Glyphs not loading | Database must be at `Resources/InputGlyphDatabase` |
| Escape not working | NavigationManager handles it automatically |

---

## Status

- ✅ Core MVVM Framework
- ✅ Input Glyph System
- ✅ Procedural UI Bars System
- ⬚ UI Audio Manager
- ⬚ Inventory/Loadout UI

---

## 6. Procedural UI Bars Setup

GPU-accelerated bars with shader-based rendering and MVVM pattern.

### Quick Setup (Recommended)

1. **Tools → DIG → UI Setup Wizard**
2. Scroll to **Shader Effects** section
3. Click the bar type you want (e.g., "Create Health Bar")
4. Bar auto-creates with ViewModel + View + Material

### Manual Setup

```csharp
// 1. Add ViewModel to a parent GameObject
var vmGO = new GameObject("PlayerHUD");
var staminaVM = vmGO.AddComponent<StaminaViewModel>();

// 2. Add View to bar Image
var barGO = new GameObject("StaminaBar");
barGO.AddComponent<UnityEngine.UI.Image>();
var view = barGO.AddComponent<StaminaShaderView>();
view.transform.SetParent(vmGO.transform);
// View auto-finds ViewModel in parent hierarchy
```

### Adding Optional Player Features

#### Survival Mechanics (Hunger/Thirst/Oxygen)

1. Select **Player Prefab**
2. Add Component → **DIG → Player → Survival Needs Authoring**
3. Configure settings in Inspector:

| Setting | Description |
|---------|-------------|
| Enable Hunger | Toggle hunger system |
| Hunger Increase Rate | How fast hunger grows (per second) |
| Starvation Damage | Damage per second when starving |
| Enable Thirst | Toggle thirst system |
| Enable Oxygen | Toggle oxygen for hazard zones |

#### Horror Mechanics (Sanity/Infection)

1. Select **Player Prefab**
2. Add Component → **DIG → Player → Horror Status Authoring**
3. Configure:

| Setting | Description |
|---------|-------------|
| Enable Sanity | Toggle sanity system |
| Darkness Drain Rate | Sanity lost per second in dark |
| Distortion Threshold | Sanity % where effects start |
| Enable Infection | Toggle infection system |

#### Energy Shield

1. Select **Player Prefab**
2. Add Component → **DIG → Player → Shield Authoring**
3. Configure:

| Setting | Description |
|---------|-------------|
| Max Shield | Maximum shield capacity |
| Recharge Delay | Seconds before regen starts |
| Recharge Rate | Shield per second when recharging |
| Can Break | If true, 0 shield = broken state |

#### Weapon Durability

1. Select **Weapon Prefab** (not player!)
2. Add Component → **DIG → Weapons → Weapon Durability Authoring**
3. Configure:

| Setting | Description |
|---------|-------------|
| Degrade Per Use | Durability lost per attack |
| Destroy On Break | Remove weapon when broken |

### Trigger Zone Setup

Systems respond to these tag components. Add them via zone triggers:

```csharp
// Example: Gas zone that drains oxygen
public class GasZoneAuthoring : MonoBehaviour
{
    class Baker : Baker<GasZoneAuthoring>
    {
        public override void Bake(GasZoneAuthoring authoring)
        {
            // Zone triggers add InOxygenHazard to player on enter
        }
    }
}
```

| Tag Component | When to Add |
|---------------|-------------|
| `InOxygenHazard` | Gas, smoke, space, underwater (if not using SwimmingAuthoring) |
| `InDarkness` | Light level below threshold |
| `NearHorrorEntity` | Proximity to monsters |
| `InSafeZone` | Safe rooms, checkpoints |

### Helper Utilities

Use static helpers from your gameplay systems:

```csharp
// Player eats food
SurvivalNeedsHelper.Eat(ref hunger, 50f);

// Player drinks
SurvivalNeedsHelper.Drink(ref thirst, 40f);

// Apply antidote
InfectionHelper.Cure(ref infection, 25f);

// Damage goes through shield first
float healthDamage = ShieldHelper.ApplyDamageToShield(ref shield, incomingDamage);
health.Current -= healthDamage;

// Use an ability charge
if (AbilityChargeHelper.TryConsumeCharge(ref charges))
{
    // Execute ability
}

// Degrade weapon on attack
if (WeaponDurabilityHelper.DegradeOnUse(ref durability))
{
    // Weapon still usable
}
```

### File Locations

```
Assets/Scripts/Player/
├── Components/           # ECS Components (PlayerHunger, PlayerShield, etc.)
├── Authoring/            # Authoring components (SurvivalNeedsAuthoring, etc.)
├── Systems/              # Gameplay systems (OxygenSystem, SanitySystem, etc.)
└── UI/
    ├── ViewModels/       # MVVM data layer
    └── Views/            # Shader view adapters

Assets/UI/Shaders/        # Procedural bar shaders
```

---

## 7. Shader Effects Reference

GPU-accelerated procedural shaders for AAA-quality HUD bars. No textures required.

### Features
- **Rounded corners** via SDF (Signed Distance Fields)
- **Smooth gradients** that change with fill amount
- **Animated shine** sweeping across bars
- **Glow effects** around edges and fill boundary
- **Critical/low state pulses**
- **Flickering** for battery effects

### Health Bar Shader (`DIG/UI/ProceduralHealthBar`)

| Property | Type | Description |
|----------|------|-------------|
| `_FillAmount` | Float 0-1 | Current health percentage |
| `_IsCritical` | Float | 1.0 for critical pulse effect |
| `_GlowIntensity` | Float 0-2 | Intensity of glow effects |
| `_ShowShine` | Float | 1.0 to enable shine sweep |
| `_ColorFull/Mid/Low/Critical` | Color | Gradient colors |
| `_CornerRadius` | Float 0-0.5 | Rounded corner size |
| `_ShineSpeed` | Float | Speed of shine animation |
| `_PulseSpeed` | Float | Speed of critical pulse |

### Battery Bar Shader (`DIG/UI/ProceduralBatteryBar`)

| Property | Type | Description |
|----------|------|-------------|
| `_FillAmount` | Float 0-1 | Battery percentage |
| `_IsOn` | Float | 1.0 = on, 0.0 = off (dim) |
| `_IsFlickering` | Float | 1.0 for flicker effect |
| `_IsLowBattery` | Float | 1.0 for warning pulse |
| `_CellCount` | Float 1-20 | Number of battery segments |
| `_CellGap` | Float | Gap between cells |
| `_FlickerSpeed` | Float | Speed of flicker animation |

### Integration Methods

#### Method A: Unity UI (Recommended)

```csharp
// ShaderHealthBar/ShaderBatteryBar auto-create material instances
var healthBar = GetComponent<ShaderHealthBar>();
healthBar.HealthPercent = currentHealth / maxHealth;

var batteryBar = GetComponent<ShaderBatteryBar>();
batteryBar.BatteryPercent = battery / maxBattery;
batteryBar.IsOn = flashlightEnabled;
```

#### Method B: UI Toolkit with RenderTexture

```csharp
var renderer = go.AddComponent<ShaderBarRenderer>();
renderer.BarMaterial = healthBarMaterial;
renderer.ApplyToElement(root.Q("health-bar-fill"));
renderer.FillAmount = healthPercent;
```

#### Method C: Direct Shader Control

```csharp
private static readonly int FillId = Shader.PropertyToID("_FillAmount");
_healthMat.SetFloat(FillId, healthPercent);
```

### Color Presets

**Default (Green to Red):**
```
ColorFull: (0.2, 0.9, 0.3)
ColorMid: (0.9, 0.9, 0.2)
ColorLow: (0.9, 0.3, 0.2)
ColorCritical: (1.0, 0.1, 0.1)
```

**Blue Energy:**
```
ColorFull: (0.2, 0.6, 1.0)
ColorMid: (0.4, 0.4, 0.9)
ColorLow: (0.6, 0.2, 0.8)
ColorCritical: (0.8, 0.1, 0.6)
```

**Cyan Tech (Battery):**
```
ColorFull: (0.4, 0.9, 1.0)
ColorMid: (0.3, 0.7, 0.8)
ColorLow: (0.9, 0.5, 0.1)
CellCount: 10
```

### Troubleshooting

| Problem | Solution |
|---------|----------|
| Bar not visible | Ensure Image has white sprite or none |
| Colors washed out | Set Image color to white (1,1,1,1) |
| Animations not playing | Check `_ShineSpeed`/`_PulseSpeed` values |
| Multiple bars affecting each other | Use component wrappers (auto material instances) |

### Performance Notes

- **Fully procedural** - no texture sampling
- **Resolution independent** - SDF-based rendering
- **Cheap updates** - just `SetFloat` calls
- Use **material property blocks** for many instances
