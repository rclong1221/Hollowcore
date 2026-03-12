# EPIC 18.2: Settings & Options Menu — Setup Guide

**Status:** Implemented
**Last Updated:** March 4, 2026
**Requires:** EPIC 18.1 (IUIService, UIServiceBootstrap, ScreenManifest, NavigationManager)

---

## Overview

The Settings system is a tabbed modal screen built on UI Toolkit with 5 built-in pages:

| Tab | Bridges To | SortOrder |
|-----|-----------|-----------|
| **Graphics** | `Screen`, `QualitySettings`, `PlayerPrefs` | 0 |
| **Audio** | `AudioManager.MasterMixer` | 1 |
| **Controls** | `TargetLockSettingsManager`, `KeybindPanel` | 2 |
| **Gameplay** | `MotionIntensitySettings`, `HealthBarSettingsManager` | 3 |
| **Accessibility** | `WidgetAccessibilityManager`, `AudioAccessibilityConfig` | 4 |

Key behaviors:
- **Live preview** — slider/toggle changes preview immediately
- **Apply/Revert** — Apply persists to PlayerPrefs and managers; Revert restores the snapshot taken when the screen opened
- **Reset to Defaults** — confirmation dialog, then factory-resets all pages
- **Unsaved changes guard** — closing with dirty state shows a 3-option dialog (Cancel / Discard / Apply & Close)
- **ESC guard** — PauseMenu defers ESC to NavigationManager when Settings is open

---

## Step 1: Add "Settings" to the Screen Manifest

1. Open **Assets/Resources/ScreenManifest** in the Inspector
2. Click **+** on the Screens list
3. Fill in:

| Field | Value |
|-------|-------|
| Screen Id | `Settings` |
| Layer | `Modal` |
| UXML Path | `UI/Screens/Settings` |
| USS Path | `UI/Styles/Settings` |
| Open Transition | SlideUp profile (or leave null for manifest default) |
| Close Transition | SlideDown profile (or leave null for manifest default) |
| Poolable | `true` |
| Blocks Input | `true` |
| Initial Focus Element | _(leave empty)_ |

> The UXML and USS files are at `Assets/Resources/UI/Screens/Settings.uxml` and `Assets/Resources/UI/Styles/Settings.uss`. They load automatically via the manifest paths above.

---

## Step 2: Ensure Required Managers Are in the Scene

These singleton managers must exist in your persistent/boot scene. The pages gracefully fall back to defaults if a manager is missing, but live preview won't work for that section.

| Manager | Required By | How to Add |
|---------|------------|------------|
| **WidgetAccessibilityManager** | Accessibility page | Add component to a persistent GameObject |
| **HealthBarSettingsManager** | Gameplay page | Auto-creates on first access, or add to scene |
| **MotionIntensitySettings** | Gameplay page | Add component to a persistent GameObject |
| **AudioManager** | Audio page | Must have `MasterMixer` field assigned in Inspector |

**No GameObject needed:**
- `TargetLockSettingsManager` — plain C# singleton, auto-creates
- `KeybindPanel` — existing UGUI panel in scene (the "Customize Keybinds" button activates its GameObject)

---

## Step 3: Expose AudioMixer Parameters

The Audio page reads and writes volume via `AudioMixer.SetFloat()`. Each exposed parameter name must match **exactly**:

| Exposed Parameter | Controls |
|-------------------|----------|
| `MasterVolume` | Master bus |
| `MusicVolume` | Music bus |
| `SFXVolume` | SFX bus |
| `VoiceVolume` | Voice bus |
| `AmbientVolume` | Ambient bus |

**How to expose:**
1. Open your AudioMixer asset in the Unity Editor
2. Select a group (e.g., Master)
3. Right-click the **Volume** parameter in the Inspector → **Expose '...' to script**
4. Open the **Exposed Parameters** panel (top-right of AudioMixer window)
5. Rename each entry to match the names above exactly

> If a parameter is not exposed, the slider still appears and reads from PlayerPrefs, but live preview and apply won't route through the mixer.

---

## Step 4: AudioAccessibilityConfig Asset

The Accessibility page loads a ScriptableObject from Resources:

1. Check if `Assets/Resources/AudioAccessibilityConfig.asset` exists
2. If not: right-click in Project → **Create > DIG > Audio > Accessibility Config**
3. Name it `AudioAccessibilityConfig` and move to `Assets/Resources/`

This asset stores Sound Radar, Directional Subtitles, and Subtitle Font Scale settings.

---

## Step 5: KeybindPanel (Controls Tab)

The Controls page has a "Customize Keybinds..." button that activates the existing UGUI `KeybindPanel`:

1. Ensure a GameObject with the `KeybindPanel` component (`DIG.Core.Input.Keybinds.UI.KeybindPanel`) exists in the scene
2. It can start **inactive** — the button calls `SetActive(true)` on click
3. If the panel is not found, a warning logs: `[ControlsSettingsPage] KeybindPanel not found in scene.`

---

## Step 6: PauseMenu — Adding a Settings Button

Add a button to your PauseMenu that opens Settings:

```csharp
using DIG.Settings.Core;

// In your button click handler:
SettingsService.Open();
```

The ESC guard in `PauseMenu.Update()` checks `NavigationManager.StackDepth > 0`. When Settings is open, pressing ESC closes Settings (via NavigationManager) instead of toggling the PauseMenu.

---

## Step 7: Editor Tooling — Settings Workstation

Open via **DIG > Settings Workstation** in the menu bar.

| Tab | What It Shows | Requires Play Mode? |
|-----|---------------|---------------------|
| **Pages** | Registered ISettingsPage list with dirty/clean status. "Open Settings" button to launch the screen. | Yes |
| **Managers** | Live values from WidgetAccessibilityManager, TargetLockSettingsManager, HealthBarSettingsManager, MotionIntensitySettings | Yes |
| **PlayerPrefs** | All known settings keys with current values. "Clear All Settings PlayerPrefs" button for test resets. | No (values only meaningful after play) |

---

## Adding a Custom Settings Page

To add a new tab (e.g., "Network"):

### 1. Create the page class

```csharp
using DIG.Settings.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Settings.Pages
{
    public class NetworkSettingsPage : ISettingsPage
    {
        public string PageId => "Network";
        public string DisplayName => "Network";
        public int SortOrder => 5; // After Accessibility (4)

        // Snapshot + current values
        private int _snapRegion;
        private int _currentRegion;

        public bool HasUnsavedChanges => _currentRegion != _snapRegion;

        public void TakeSnapshot()
        {
            _snapRegion = PlayerPrefs.GetInt("Net_Region", 0);
            _currentRegion = _snapRegion;
        }

        public void BuildUI(VisualElement container)
        {
            container.Add(SettingsScreenController.CreateSectionHeader("Connection"));
            container.Add(SettingsScreenController.CreateDropdownRow(
                "Region",
                new List<string> { "Auto", "US East", "US West", "EU", "Asia" },
                _currentRegion,
                idx => _currentRegion = idx));
        }

        public void OnPageShown() { }

        public void ApplyChanges()
        {
            PlayerPrefs.SetInt("Net_Region", _currentRegion);
            PlayerPrefs.Save();
        }

        public void RevertChanges() => _currentRegion = _snapRegion;

        public void ResetToDefaults() => _currentRegion = 0;
    }
}
```

### 2. Register it in SettingsScreenController

Open `Assets/Scripts/Settings/Core/SettingsScreenController.cs` and add to `RegisterPages()`:

```csharp
private void RegisterPages()
{
    SettingsService.ClearPages();
    SettingsService.RegisterPage(new GraphicsSettingsPage());
    SettingsService.RegisterPage(new AudioSettingsPage());
    SettingsService.RegisterPage(new ControlsSettingsPage());
    SettingsService.RegisterPage(new GameplaySettingsPage());
    SettingsService.RegisterPage(new AccessibilitySettingsPage());
    SettingsService.RegisterPage(new NetworkSettingsPage()); // <-- add here
}
```

Pages are sorted by `SortOrder`, so tab position is determined by the value you return from that property.

### UI helper methods available

`SettingsScreenController` provides static helpers for building consistent rows:

| Method | Use Case |
|--------|----------|
| `CreateSectionHeader(string text)` | Bold divider label for grouping settings |
| `CreateSliderRow(label, min, max, value, onChanged, format)` | Slider with live value label |
| `CreateToggleRow(label, value, onChanged)` | On/off toggle |
| `CreateDropdownRow(label, choices, selectedIndex, onChanged)` | Dropdown selector |
| `CreateSettingsRow(label, control)` | Generic row with any VisualElement as the control |

---

## PlayerPrefs Keys Reference

All settings are persisted via PlayerPrefs. These are the keys used by built-in pages:

### Graphics
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `WindowMode` | int | 0 | 0 = Fullscreen, 1 = Windowed |
| `Settings_FOV` | float | 90 | Field of view (degrees) |

### Audio
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Audio_MasterVolume` | float | 0.8 | Master volume (linear 0–1) |
| `Audio_MusicVolume` | float | 0.8 | Music volume (linear 0–1) |
| `Audio_SFXVolume` | float | 0.8 | SFX volume (linear 0–1) |
| `Audio_VoiceVolume` | float | 0.8 | Voice volume (linear 0–1) |
| `Audio_AmbientVolume` | float | 0.8 | Ambient volume (linear 0–1) |

### Controls
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Settings_MouseSensitivity` | float | 5.0 | Mouse look sensitivity |
| `Settings_InvertY` | int | 0 | 0 = normal, 1 = inverted |

### Gameplay
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Settings_ShowDamageNumbers` | int | 1 | 0 = hidden, 1 = shown |

### Accessibility (via managers)
Accessibility settings are persisted by `WidgetAccessibilityManager` and `AudioAccessibilityConfig` through their own PlayerPrefs keys (prefixed `Widget_` and `AudioAccess_`).

---

## Troubleshooting

### Settings screen doesn't open
- Check the console for `[SettingsService] UIServices.Screen is not initialized.`
  - Ensure `UIServiceBootstrap` exists in the scene and runs before Settings is opened
- Check for `Failed to open Settings screen. Ensure 'Settings' is registered in the ScreenManifest.`
  - Verify ScreenManifest has an entry with Screen Id = `Settings` (case-sensitive)

### Tabs appear but page is blank
- The page's `BuildUI()` may be failing silently. Check the console for exceptions.
- Verify the required manager singleton exists in the scene (see Step 2).

### Audio sliders don't affect volume
- Open the AudioMixer and verify parameters are **exposed** with the exact names listed in Step 3
- Verify `AudioManager` has its `MasterMixer` field assigned in the Inspector

### ESC doesn't close Settings
- Verify `NavigationManager` exists in the scene
- Verify `PauseMenu.Update()` has the ESC guard that checks `NavigationManager.StackDepth > 0`

### "Customize Keybinds" button does nothing
- Ensure a `KeybindPanel` component exists somewhere in the scene hierarchy (can be on an inactive GameObject)

### Settings Workstation shows "No settings pages registered"
- Pages are only registered when the Settings screen is opened. Click "Open Settings" in the workstation toolbar first.

---

## Verification Checklist

- [ ] `Settings` entry exists in ScreenManifest with Layer = Modal
- [ ] In Play Mode: `SettingsService.Open()` opens the settings screen
- [ ] 5 tabs visible: Graphics, Audio, Controls, Gameplay, Accessibility
- [ ] Tab switching updates content and highlights the active tab
- [ ] **Graphics:** Resolution and Window Mode dropdowns work
- [ ] **Audio:** Volume sliders move and audio changes in real time
- [ ] **Controls:** Aim Assist / Target Lock toggles update `TargetLockSettingsManager`
- [ ] **Controls:** "Customize Keybinds" button opens the KeybindPanel
- [ ] **Gameplay:** Camera Shake slider updates `MotionIntensitySettings`
- [ ] **Gameplay:** Show Enemy Names / Levels toggles update `HealthBarSettingsManager`
- [ ] **Accessibility:** Colorblind Mode dropdown updates `WidgetAccessibilityManager`
- [ ] Apply button persists changes; Revert restores previous state
- [ ] Reset to Defaults shows confirmation dialog, then resets all pages
- [ ] Closing with unsaved changes shows 3-option dialog (Cancel / Discard / Apply & Close)
- [ ] ESC closes Settings (not PauseMenu) when Settings is open
- [ ] Settings Workstation opens via **DIG > Settings Workstation**
