# EPIC 18.2: Settings & Options Menu System

**Status:** IMPLEMENTED
**Priority:** High (Player Retention — first thing players look for)
**Dependencies:**
- `GraphicsSettingsPanel` (existing — `DIG.Settings`, `Assets/Scripts/Settings/GraphicsSettingsPanel.cs`, UGUI TMP_Dropdown, PlayerPrefs-only window mode)
- `GraphicsSettingsApplier` (existing — `DIG.Settings`, `Assets/Scripts/Settings/GraphicsSettingsApplier.cs`)
- `PauseMenu` tab system (existing — `DIG.Settings`, `Assets/Scripts/Settings/PauseMenu.cs`, tab buttons + panels)
- `InputParadigmSettingsUI` (existing — `DIG.Settings`, `Assets/Scripts/Settings/InputParadigmSettingsUI.cs`)
- `KeybindPanel` / `KeybindService` (existing — `DIG.Core.Input.Keybinds`, `Assets/Scripts/Core/Input/Keybinds/`)
- `AudioManager` mixer control (existing — `Audio.Systems`, `Assets/Scripts/Audio/AudioManager.cs`, MasterMixer reference)
- `AudioSettings` (existing — `Assets/Scripts/Audio/AudioSettings.cs`)
- `MotionIntensitySettings` (existing — `DIG.Core.Settings`, `Assets/Scripts/Core/Settings/MotionIntensitySettings.cs`)
- `AudioAccessibilityConfig` (existing — `DIG.Audio.Accessibility`, `Assets/Scripts/Audio/Accessibility/AudioAccessibilityConfig.cs`)
- `IUIService` (EPIC 18.1 — screen lifecycle management)

**Feature:** A comprehensive, modular settings system with a registry-based architecture where each settings category (Graphics, Audio, Controls, Gameplay, Accessibility) is a self-contained `ISettingsPage` plugin. Settings are stored in a single serialized `SettingsProfileSO` with automatic dirty-tracking, apply/revert/reset-to-defaults workflow, and platform-specific defaults. Includes an editor tool for defining setting schemas, validating ranges, and previewing the settings UI.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `GraphicsSettingsPanel` | `Assets/Scripts/Settings/GraphicsSettingsPanel.cs` | Basic | UGUI TMP_Dropdown, only window mode (fullscreen/windowed), PlayerPrefs persistence |
| `GraphicsSettingsApplier` | `Assets/Scripts/Settings/GraphicsSettingsApplier.cs` | Basic | Applies graphics presets |
| `PauseMenu` | `Assets/Scripts/Settings/PauseMenu.cs` | Working | Tab switching, ESC toggle, cursor management, UGUI-based |
| `KeybindPanel` / `KeybindService` | `Assets/Scripts/Core/Input/Keybinds/` | Fully implemented | Rebinding UI, conflict detection, save/load, InputAction-based |
| `AudioManager` | `Assets/Scripts/Audio/AudioManager.cs` | Working | MasterMixer reference, exposed params for volume control |
| `MotionIntensitySettings` | `Assets/Scripts/Core/Settings/MotionIntensitySettings.cs` | Implemented | Camera shake/bob intensity configuration |
| `AudioAccessibilityConfig` | `Assets/Scripts/Audio/Accessibility/AudioAccessibilityConfig.cs` | Implemented | Subtitle settings, sound radar config |
| `SettingsMenuGenerator` | `Assets/Scripts/Settings/Editor/SettingsMenuGenerator.cs` | Editor tool | Auto-generates settings menu UI |

### What's Missing

- **No unified settings data model** — graphics uses PlayerPrefs, keybinds use InputActionAsset overrides, audio presumably uses PlayerPrefs, no shared schema
- **No apply/revert workflow** — changes apply immediately with no way to revert; no "Apply" / "Cancel" / "Reset to Defaults" buttons
- **No settings profiles** — no way to save/load different settings configurations (e.g., "Performance" vs "Quality" graphics presets)
- **No platform-specific defaults** — no differentiation between PC, console, mobile defaults
- **No resolution selection** — GraphicsSettingsPanel only has fullscreen/windowed toggle; no resolution dropdown
- **No quality preset system** — no Low/Medium/High/Ultra presets that adjust multiple settings at once
- **No audio channel sliders** — no master/music/SFX/voice/ambient volume sliders despite having AudioMixer infrastructure
- **No FOV slider** — common requirement for FPS/TPS games
- **No sensitivity settings** — no mouse sensitivity, controller deadzone, aim assist toggle
- **No confirmation dialogs** — resolution changes have no "Keep these settings?" countdown timer
- **No settings search** — large settings menus need search/filter capability

---

## Problem

Players expect a polished settings menu with graphics presets, resolution selection, audio sliders, control rebinding, and accessibility options — all with apply/revert semantics. DIG has fragments: a window mode dropdown, keybind rebinding, and audio mixer params, but no cohesive system tying them together. Settings changes are scattered across PlayerPrefs keys with no validation, no dirty tracking, and no way to revert mistakes. A designer cannot add a new gameplay setting (e.g., "Show Damage Numbers") without writing custom UI and persistence code.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  SettingsSchemaDB SO        SettingsProfileSO         PlatformDefaultsSO
  (all setting definitions:  (serialized current       (per-platform default
   key, type, range, label,   values as Dictionary      overrides: console
   category, tooltip,         <string, SettingValue>,   defaults differ from
   platform flags)            JSON-serializable)        PC defaults)
        |                         |                          |
        └──── SettingsService (singleton MonoBehaviour) ─────┘
              (loads schema, current profile, applies diffs,
               dirty tracking, apply/revert/reset workflow)
                         |
              ISettingsPage (plugin interface per category)
                         |
        ┌────────────────┼──────────────────┐
        |                |                  |
  GraphicsPage      AudioPage         ControlsPage
  (resolution,      (master/music/    (sensitivity,
   quality preset,   SFX/voice/        deadzone, aim
   vsync, fov,       ambient sliders,  assist, keybind
   shadow quality,   subtitle toggle,  link to existing
   AA, texture)      spatial audio)    KeybindPanel)
        |                |                  |
  GameplayPage      AccessibilityPage
  (damage numbers,  (colorblind mode,
   auto-aim,         text scale, HUD
   minimap rotate,   opacity, motion
   language)         reduction)
                         |
              SettingsMenuView (UIToolkit)
              (tab bar + scrollable page content,
               search bar, apply/revert/defaults buttons,
               resolution change countdown timer)
                         |
                 EDITOR TOOLING
                         |
  SettingsWorkstationModule ── schema editor
  (add/edit setting definitions, preview UI, validate ranges,
   test platform defaults, diff profiles)
```

---

## Core Interfaces

### ISettingsPage

**File:** `Assets/Scripts/Settings/Core/ISettingsPage.cs`

```csharp
public interface ISettingsPage
{
    string PageId { get; }
    string DisplayName { get; }
    int SortOrder { get; }
    Sprite Icon { get; }
    void BuildUI(VisualElement container, SettingsService service);
    void OnApply(SettingsService service);
    void OnRevert(SettingsService service);
    void OnResetDefaults(SettingsService service);
    bool HasUnsavedChanges { get; }
}
```

### SettingDefinition

**File:** `Assets/Scripts/Settings/Core/SettingDefinition.cs`

```csharp
[Serializable]
public class SettingDefinition
{
    public string Key;                    // "graphics.resolution", "audio.masterVolume"
    public string DisplayName;           // "Resolution", "Master Volume"
    public string Tooltip;               // Hover description
    public string Category;              // "Graphics", "Audio", etc.
    public SettingType Type;             // Toggle, Slider, Dropdown, Keybind, Color
    public SettingValue DefaultValue;    // Default for current platform
    public SettingValue MinValue;        // For sliders
    public SettingValue MaxValue;        // For sliders
    public float SliderStep;            // Snap step for sliders (0 = continuous)
    public string[] DropdownOptions;    // For dropdown type
    public string[] DropdownValues;     // Backing values for dropdown options
    public PlatformFlags Platforms;     // Which platforms show this setting
    public bool RequiresRestart;        // Shows "requires restart" badge
    public bool RequiresConfirmation;   // Resolution-change-style confirmation
    public float ConfirmationTimeout;   // Seconds before auto-revert (default 15)
    public string DependsOnKey;         // Only visible if dependency key is truthy
}
```

---

## ScriptableObjects

### SettingsSchemaDB

**File:** `Assets/Scripts/Settings/Config/SettingsSchemaDB.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Settings | SettingDefinition[] | empty | All setting definitions |
| Categories | SettingCategory[] | empty | Category metadata (name, icon, sort order) |
| Version | int | 1 | Schema version for migration |

### SettingsProfileSO

**File:** `Assets/Scripts/Settings/Config/SettingsProfileSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| ProfileName | string | "Default" | Display name |
| Values | SerializableDictionary<string, string> | empty | Key → JSON-serialized value |
| SchemaVersion | int | 1 | Version this profile was saved under |
| LastModifiedUtc | string | "" | ISO 8601 timestamp |

### PlatformDefaultsSO

**File:** `Assets/Scripts/Settings/Config/PlatformDefaultsSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Platform | RuntimePlatform | WindowsPlayer | Target platform |
| Overrides | SerializableDictionary<string, string> | empty | Key → default value overrides |

---

## SettingsService

**File:** `Assets/Scripts/Settings/Core/SettingsService.cs`

- MonoBehaviour singleton, `DontDestroyOnLoad`, `[DefaultExecutionOrder(-250)]`
- Loads `SettingsSchemaDB` from Resources at `Awake`
- Loads or creates `SettingsProfileSO` from `Application.persistentDataPath/settings.json`
- Applies platform defaults for any missing keys
- Exposes:
  - `T Get<T>(string key)` — type-safe setting read
  - `void Set<T>(string key, T value)` — stages a change (dirty flag)
  - `void Apply()` — commits all staged changes, fires `OnSettingsApplied`, saves to disk
  - `void Revert()` — discards staged changes, restores from last-applied profile
  - `void ResetToDefaults(string category = null)` — resets a category or all
  - `bool IsDirty` — true if unsaved changes exist
  - `event Action<string, object> OnSettingChanged` — per-key change notification
  - `event Action OnSettingsApplied` — batch apply notification
- Bridges to existing systems:
  - `AudioMixer.SetFloat()` for volume params
  - `QualitySettings` for graphics presets
  - `Screen.SetResolution()` for resolution changes
  - `InputParadigmProfile` for control sensitivity

### Settings Persistence Format

```json
{
  "schemaVersion": 1,
  "lastModified": "2026-02-25T12:00:00Z",
  "values": {
    "graphics.resolution": "1920x1080",
    "graphics.fullscreenMode": "ExclusiveFullScreen",
    "graphics.qualityPreset": "High",
    "graphics.vsync": "true",
    "graphics.fov": "90",
    "audio.masterVolume": "0.8",
    "audio.musicVolume": "0.6",
    "audio.sfxVolume": "1.0",
    "audio.voiceVolume": "0.9",
    "controls.mouseSensitivity": "5.0",
    "controls.controllerDeadzone": "0.15",
    "gameplay.showDamageNumbers": "true",
    "accessibility.colorblindMode": "None"
  }
}
```

---

## Built-In Settings Pages

### GraphicsSettingsPage : ISettingsPage

- Resolution dropdown (enumerates `Screen.resolutions`, deduplicates, sorts)
- Fullscreen mode dropdown (ExclusiveFullScreen, FullScreenWindow, Windowed)
- Quality preset dropdown (Low, Medium, High, Ultra, Custom) — changing preset fills individual settings
- VSync toggle
- FOV slider (60–120, default 90)
- Shadow Quality dropdown (Off, Low, Medium, High, Ultra)
- Anti-Aliasing dropdown (None, FXAA, SMAA, TAA)
- Texture Quality dropdown (Low, Medium, High)
- View Distance slider (50–500)
- Resolution change triggers 15-second confirmation countdown

### AudioSettingsPage : ISettingsPage

- Master Volume slider (0–100%)
- Music Volume slider (0–100%)
- SFX Volume slider (0–100%)
- Voice Volume slider (0–100%)
- Ambient Volume slider (0–100%)
- All sliders map to AudioMixer exposed parameters
- Subtitle toggle (bridges to existing `DirectionalSubtitleManager`)
- Spatial Audio toggle

### ControlsSettingsPage : ISettingsPage

- Mouse Sensitivity slider (0.1–20)
- Controller Sensitivity slider (0.1–20)
- Controller Deadzone slider (0–0.5)
- Invert Y-Axis toggle
- Aim Assist toggle
- Keybind section: embeds existing `KeybindPanel` component
- Input Paradigm selector: links to existing `InputParadigmSettingsUI`

### GameplaySettingsPage : ISettingsPage

- Show Damage Numbers toggle
- Minimap Rotation toggle
- Auto-Loot toggle
- Camera Shake Intensity slider (bridges to `MotionIntensitySettings`)
- Language dropdown (bridges to localization system)

### AccessibilitySettingsPage : ISettingsPage

- Colorblind Mode dropdown (None, Protanopia, Deuteranopia, Tritanopia)
- Text Scale slider (80–150%)
- HUD Opacity slider (0–100%)
- Reduced Motion toggle
- Screen Reader Mode toggle
- Subtitle Size dropdown (Small, Medium, Large)
- Sound Radar toggle (bridges to existing `SoundRadarSystem`)

---

## Editor Tooling

### SettingsWorkstationModule

**File:** `Assets/Editor/SettingsWorkstation/Modules/SettingsWorkstationModule.cs`

- **Schema Editor:** Add/edit/remove `SettingDefinition` entries in `SettingsSchemaDB`
- **UI Preview:** Renders settings page as it would appear at runtime
- **Validation:** Warns about missing defaults, invalid ranges, orphaned keys
- **Profile Diff:** Compare two `SettingsProfileSO` instances side-by-side
- **Platform Preview:** Switch platform dropdown to see which settings appear/hide

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Settings/Core/ISettingsPage.cs` | Interface | ~25 |
| `Assets/Scripts/Settings/Core/SettingDefinition.cs` | Class | ~50 |
| `Assets/Scripts/Settings/Core/SettingsService.cs` | MonoBehaviour | ~300 |
| `Assets/Scripts/Settings/Core/SettingValue.cs` | Struct | ~60 |
| `Assets/Scripts/Settings/Config/SettingsSchemaDB.cs` | ScriptableObject | ~30 |
| `Assets/Scripts/Settings/Config/SettingsProfileSO.cs` | ScriptableObject | ~40 |
| `Assets/Scripts/Settings/Config/PlatformDefaultsSO.cs` | ScriptableObject | ~25 |
| `Assets/Scripts/Settings/Pages/GraphicsSettingsPage.cs` | ISettingsPage | ~200 |
| `Assets/Scripts/Settings/Pages/AudioSettingsPage.cs` | ISettingsPage | ~120 |
| `Assets/Scripts/Settings/Pages/ControlsSettingsPage.cs` | ISettingsPage | ~100 |
| `Assets/Scripts/Settings/Pages/GameplaySettingsPage.cs` | ISettingsPage | ~80 |
| `Assets/Scripts/Settings/Pages/AccessibilitySettingsPage.cs` | ISettingsPage | ~100 |
| `Assets/Scripts/Settings/UI/SettingsMenuView.cs` | UIView | ~200 |
| `Assets/Scripts/Settings/UI/SettingsMenuViewModel.cs` | ViewModel | ~120 |
| `Assets/Scripts/Settings/UI/ConfirmationCountdownView.cs` | UIView | ~60 |
| `Assets/Editor/SettingsWorkstation/Modules/SettingsWorkstationModule.cs` | Editor | ~250 |

**Total estimated:** ~1,760 lines

---

## Performance Considerations

- `SettingsService.Get<T>()` uses `Dictionary<string, string>` lookup — O(1) amortized
- Settings UI only builds on open (no per-frame cost when closed)
- Apply batches all mixer/quality/screen changes in a single frame to avoid intermediate states
- JSON serialization uses `JsonUtility` (no Newtonsoft dependency) — sub-1ms save/load

---

## Testing Strategy

- Unit test `SettingsService` get/set/apply/revert/reset cycle
- Unit test `SettingDefinition` validation (range, type, platform flags)
- Unit test profile serialization round-trip (save → load → compare)
- Integration test: change resolution → verify confirmation countdown → auto-revert on timeout
- Integration test: change volume slider → verify AudioMixer param updates
- Editor test: `SettingsWorkstationModule` schema editor CRUD operations
