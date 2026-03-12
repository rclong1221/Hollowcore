# EPIC 18.1: UI Framework — Screen Lifecycle & Service Layer — Setup Guide

**Status:** Implemented
**Last Updated:** March 4, 2026
**Requires:** EPIC 15.8 (UIView, ViewModelBase, BindableProperty, NavigationManager), Unity UI Toolkit

This guide covers the Unity Editor setup for the **UI Service Layer** — the unified screen lifecycle system that manages screen opening/closing with transitions, pooling, focus management, and theming. All future UI systems (Settings, Notifications, Tutorial, Accessibility) build on this foundation.

---

## Overview

The UI Service Layer provides:

- **IUIService** — a single interface for opening/closing screens by ID, with transition animations
- **Screen Manifest** — a ScriptableObject registry of all available screens with their UXML paths, layer assignments, and transition profiles
- **Screen Pooling** — frequently-used screens are kept in memory on close and reused on re-open (no repeated UXML instantiation)
- **Transition Profiles** — ScriptableObject-defined fade/slide/scale animations driven by USS classes
- **Focus Management** — automatic gamepad/keyboard focus when screens open, restored when they close
- **Theme System** — ScriptableObject-defined color palettes applied at runtime
- **UI Workstation** — editor window for live debugging (`DIG > UI Workstation`)

---

## Step 1: Scene Setup — UIServiceBootstrap

The `UIServiceBootstrap` component initializes the UI service on startup. It must exist in your persistent/boot scene.

### 1.1 Create the Bootstrap GameObject

1. In your persistent scene (or boot scene), create an empty GameObject named `UIServiceBootstrap`
2. Add the `UIServiceBootstrap` component
3. The component uses `DontDestroyOnLoad` — it persists across scene loads

| Field | Description | Default |
|-------|-------------|---------|
| **Manifest Override** | Optional direct reference to a `ScreenManifestSO`. If null, loads from `Resources/ScreenManifest` automatically | None (auto-loads) |
| **UI Document Override** | Optional existing UIDocument to host the service UI. If null, a UIDocument is created automatically on the same GameObject | None (auto-creates) |

> **Note:** The bootstrap sets `[DefaultExecutionOrder(-300)]` to ensure the service initializes before any system that calls `UIServices.Screen`.

### 1.2 Scene Hierarchy

```
Scene Root
 ├── NavigationManager              (existing EPIC 15.8, auto-creates if missing)
 │
 ├── UIServiceBootstrap             ← NEW (EPIC 18.1)
 │   └── [UIDocument]               (auto-added if not present)
 │
 ├── AudioManager                   (existing)
 └── ...
```

---

## Step 2: Create a Screen Manifest

The Screen Manifest is the central registry that defines all screens the service knows about.

### 2.1 Create the Asset

1. Right-click in Project → **Create > DIG/UI/Screen Manifest**
2. Name it `ScreenManifest`
3. Move it to **`Assets/Resources/`** (required for auto-loading)

> The bootstrap loads this via `Resources.Load<ScreenManifestSO>("ScreenManifest")`. If the asset is missing, the service still initializes but with no registered screens.

### 2.2 Manifest Inspector Reference

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Screens** | `ScreenDefinition[]` | Empty | All registered screen definitions (see below) |
| **Default Open Transition** | `TransitionProfileSO` | None | Fallback transition when a screen's own OpenTransition is null |
| **Default Close Transition** | `TransitionProfileSO` | None | Fallback transition when a screen's own CloseTransition is null |
| **Default Theme** | `UIThemeSO` | None | Theme applied automatically at startup |

### 2.3 Adding a Screen Entry

Click the `+` button on the Screens list to add a new entry. Each entry has:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Screen Id** | string | — | Unique identifier used in code: `UIServices.Screen.OpenScreen("Inventory")` |
| **Layer** | `UILayer` enum | Screen | `Screen` = full replacement, `Modal` = stacks on top, `HUD` = always visible, `Tooltip` = above all |
| **UXML Path** | string | — | Path to the `VisualTreeAsset` under `Resources/` **without** the `.uxml` extension. Example: `UI/Screens/Inventory` for `Assets/Resources/UI/Screens/Inventory.uxml` |
| **USS Path** | string | — | Optional per-screen stylesheet path under `Resources/` (without extension). Applied on top of global styles |
| **Open Transition** | `TransitionProfileSO` | None | Override open transition for this screen. Falls back to manifest default |
| **Close Transition** | `TransitionProfileSO` | None | Override close transition for this screen. Falls back to manifest default |
| **Poolable** | bool | true | If true, the screen's VisualElement tree is kept in memory on close and reused on next open |
| **Blocks Input** | bool | false | If true, input to screens below is blocked (typical for modals) |
| **Initial Focus Element** | string | — | USS name of the element to auto-focus on open (for gamepad/keyboard). Leave empty for automatic first-focusable |

---

## Step 3: Create Transition Profiles

Transition profiles control how screens animate in and out.

### 3.1 Create a Profile

1. Right-click in Project → **Create > DIG/UI/Transition Profile**
2. Name it descriptively (e.g., `Fade_200ms`, `SlideLeft_300ms`)
3. Place it anywhere in your project (suggestion: `Assets/UI/Transitions/`)

### 3.2 Profile Inspector Reference

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Type** | `TransitionType` enum | Fade | `None`, `Fade`, `SlideLeft`, `SlideRight`, `SlideUp`, `SlideDown`, `Scale` |
| **Duration** | float [0–2] | 0.2 | Transition duration in seconds |
| **Easing Curve** | AnimationCurve | None | Optional custom easing. If null, USS `ease-out` is used |
| **Delay** | float [0–1] | 0.0 | Delay before the transition starts |

### 3.3 Recommended Presets

| Preset | Type | Duration | Use Case |
|--------|------|----------|----------|
| **Fade_Fast** | Fade | 0.15 | Tooltips, quick overlays |
| **Fade_Normal** | Fade | 0.25 | General screens (recommended default) |
| **SlideUp_Modal** | SlideUp | 0.3 | Modal dialogs sliding up from bottom |
| **Scale_Popup** | Scale | 0.2 | Confirmation popups, alerts |
| **None** | None | — | Instant show/hide (debug, HUD elements) |

> **Tip:** Assign `Fade_Normal` as both `Default Open Transition` and `Default Close Transition` on the manifest. Individual screens can then override only when they need something different.

---

## Step 4: Create a Theme (Optional)

Themes define color palettes that can be swapped at runtime.

### 4.1 Create a Theme

1. Right-click in Project → **Create > DIG/UI/Theme**
2. Name it (e.g., `Theme_Default`, `Theme_Dark`, `Theme_HighContrast`)
3. Assign it as `Default Theme` on the Screen Manifest, or apply at runtime

### 4.2 Theme Inspector Reference

| Section | Field | Default | Description |
|---------|-------|---------|-------------|
| **Identity** | Theme Name | `"Default"` | Display name for editor tooling |
| **Primary Colors** | Primary | `#2196F3` | Main accent color |
| | Primary Light | `#64B5F6` | Lighter variant |
| | Primary Dark | `#1A76D2` | Darker variant |
| **Background** | Background | `#1A1A2E` | Main background |
| | Surface | `#16213E` | Card/panel surface |
| | Background Panel | `#1F1F33 (95% alpha)` | Semi-transparent panel overlay |
| **Text** | Text Primary | `#E0E0E0` | Main body text |
| | Text Secondary | `#9E9E9E` | Muted/label text |
| **Semantic** | Success | `#4CAF50` | Success states |
| | Warning | `#FF9800` | Warning states |
| | Error | `#F44336` | Error/danger states |
| **Font Overrides** | Primary Font | None | Global font override (null = stylesheet default) |
| | Heading Font | None | Heading font override |

> **Note:** Themes apply as inline styles on the service's layer root elements. They override USS custom property defaults from `Variables.uss` for any screens managed by the service.

---

## Step 5: Create UXML Screen Templates

Each screen registered in the manifest needs a corresponding UXML file.

### 5.1 File Location

Place UXML files under `Assets/Resources/` so the service can load them. Example directory structure:

```
Assets/
 └── Resources/
     ├── ScreenManifest.asset
     └── UI/
         └── Screens/
             ├── Inventory.uxml
             ├── Settings.uxml
             ├── ConfirmDialog.uxml
             └── ...
```

The `UXML Path` in the ScreenDefinition would be `UI/Screens/Inventory` (no extension).

### 5.2 UXML Requirements

- The UXML root element will be stretched to fill the screen layer automatically
- For gamepad focus: ensure interactive elements (Buttons, etc.) have `focusable="true"`
- Name the element you want auto-focused with a USS name matching `Initial Focus Element` in the ScreenDefinition (e.g., `name="first-button"`)

### 5.3 Per-Screen Stylesheets

If a screen needs its own USS:

1. Place the USS file under `Assets/Resources/` (e.g., `Assets/Resources/UI/Styles/Inventory.uss`)
2. Set the `USS Path` field to `UI/Styles/Inventory` (no extension)

The global `Transitions.uss` (at `Assets/UI/Styles/Transitions.uss`) is loaded automatically by the service — you do not need to reference it in your UXML.

---

## Step 6: Using the Service in Code

### 6.1 Opening a Screen

```csharp
// Simple open
UIServices.Screen.OpenScreen("Inventory");

// With data and callback
UIServices.Screen.OpenScreen("ItemDetail", itemData, handle =>
{
    Debug.Log($"Screen opened: {handle}");
});
```

### 6.2 Closing a Screen

```csharp
// Close by handle (returned from OpenScreen)
var handle = UIServices.Screen.OpenScreen("Inventory");
UIServices.Screen.CloseScreen(handle);

// Close topmost (Escape key does this automatically via NavigationManager)
UIServices.Screen.CloseTop();

// Close everything
UIServices.Screen.CloseAll();
```

### 6.3 Querying State

```csharp
if (UIServices.Screen.IsOpen("Inventory"))
{
    var handle = UIServices.Screen.GetHandle("Inventory");
    // ...
}
```

### 6.4 Switching Themes at Runtime

```csharp
var darkTheme = Resources.Load<UIThemeSO>("Themes/DarkTheme");
UIServices.Screen.SetTheme(darkTheme);
```

---

## Step 7: Editor Tooling — UI Workstation

Open via the menu: **DIG > UI Workstation**

### Tabs

| Tab | Purpose | Requires Play Mode? |
|-----|---------|---------------------|
| **Screen Stack** | Live view of the NavigationManager stacks — shows active screen, active modal, their IDs, layers, visibility, and handles | Yes |
| **Catalog** | Lists all entries from the Screen Manifest. Shows pool status. **Test Open** / **Close** buttons for each screen in Play Mode | Catalog view: No. Buttons: Yes |
| **Theme** | Drag a `UIThemeSO` to preview its color swatches. **Apply Theme** button to switch at runtime | Preview: No. Apply: Yes |
| **Focus** | Shows the FocusManager stack depth for debugging gamepad/keyboard navigation | Yes |

---

## Tuning & Best Practices

### Screen Pooling

| Scenario | Poolable? | Why |
|----------|-----------|-----|
| Inventory, Map, Chat | Yes | Opened frequently — pooling avoids repeated UXML instantiation |
| Settings, one-time dialogs | Yes | Low memory cost, faster re-open |
| Confirmation popups | Yes | Extremely frequent, lightweight |
| Tutorial overlays | No | Shown once per session, no benefit from pooling |

### Transition Timing

| Duration | Feel | Use Case |
|----------|------|----------|
| **0** (None) | Instant | HUD elements, debug panels |
| **0.10–0.15** | Snappy | Tooltips, quick overlays |
| **0.20–0.25** | Balanced | General screens (recommended) |
| **0.30–0.40** | Smooth | Full-screen transitions, modals |
| **0.50+** | Cinematic | Title screen, cutscene overlays |

### Focus Management

- Always set `Initial Focus Element` for modal dialogs — gamepad users need a clear starting point
- For screens with multiple sections, focus the primary action button (e.g., "Continue", "Confirm")
- Focus is automatically restored when a modal closes (no code needed)

---

## Verification Checklist

- [ ] `UIServiceBootstrap` exists in the boot/persistent scene
- [ ] `ScreenManifest.asset` exists at `Assets/Resources/ScreenManifest`
- [ ] At least one `TransitionProfileSO` created and assigned as manifest default
- [ ] At least one screen entry added to the manifest with a valid UXML path
- [ ] In Play Mode: `UIServices.Screen.OpenScreen("YourScreenId")` opens the screen with the configured transition
- [ ] In Play Mode: pressing Escape closes the topmost modal/screen
- [ ] In Play Mode: opening the same screen twice reuses the pooled VisualElement (if Poolable)
- [ ] UI Workstation (`DIG > UI Workstation`) shows the screen stack and catalog correctly

---

## File Reference

| File | Purpose |
|------|---------|
| `Assets/Scripts/UI/Core/Services/IUIService.cs` | Public interface — all game code uses this |
| `Assets/Scripts/UI/Core/Services/UIServices.cs` | Static accessor: `UIServices.Screen` |
| `Assets/Scripts/UI/Core/Services/UIServiceBootstrap.cs` | Bootstrap MonoBehaviour (place in scene) |
| `Assets/Scripts/UI/Core/Services/ScreenManifestSO.cs` | Screen registry ScriptableObject |
| `Assets/Scripts/UI/Core/Services/ScreenDefinition.cs` | Per-screen config data |
| `Assets/Scripts/UI/Core/Services/TransitionProfileSO.cs` | Transition animation config |
| `Assets/Scripts/UI/Core/Services/UIThemeSO.cs` | Theme/color palette config |
| `Assets/Scripts/UI/Core/Services/UIToolkitService.cs` | Core service implementation |
| `Assets/Scripts/UI/Core/Services/TransitionPlayer.cs` | USS-class-based transition utility |
| `Assets/Scripts/UI/Core/Services/FocusManager.cs` | Gamepad/keyboard focus stack |
| `Assets/Scripts/UI/Core/Services/ScreenHandle.cs` | Readonly handle struct |
| `Assets/UI/Styles/Transitions.uss` | CSS transition classes (fade, slide, scale) |
| `Assets/Editor/UIWorkstation/UIWorkstationWindow.cs` | Editor debug window |
| `Assets/Scripts/UI/Core/Navigation/NavigationManager.cs` | Existing nav stack (enhanced with Handle field) |
