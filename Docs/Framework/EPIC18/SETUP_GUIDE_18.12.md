# EPIC 18.12: Accessibility Framework — Setup Guide

**Status:** Implemented
**Last Updated:** March 4, 2026
**Requires:** EPIC 18.1 (UIToolkitService), EPIC 18.2 (AccessibilitySettingsPage), URP Renderer Asset

---

## Overview

The Accessibility Framework provides a unified CVAA/WCAG-compliant accessibility layer. `AccessibilityService` is a MonoBehaviour singleton coordinator that delegates to existing managers (WidgetAccessibilityManager, AudioAccessibilityConfig) and new feature modules.

| Feature | Module | Description |
|---------|--------|-------------|
| **Colorblind Filter** | ColorblindFilter (URP) | GPU Daltonization for Protanopia, Deuteranopia, Tritanopia |
| **Text Scaling** | TextScaleService | Global font size multiplier (0.8x–2.0x) |
| **High Contrast** | HighContrastMode | Theme swap + increased outline thickness |
| **Screen Reader** | ScreenReaderBridge | Platform TTS (macOS: `say`, Windows: SAPI5) |
| **Hold-to-Toggle** | HoldToToggleService | Convert hold actions (Sprint, Aim, Crouch, Block) to toggles |
| **Input Timing** | InputTimingService | Configurable double-tap window, hold threshold, input buffer |
| **Aim Assist** | AimAssistService | Strength, magnetism, slowdown multipliers |
| **One-Handed Presets** | OneHandedPresets | Left-hand / right-hand keybind profiles |
| **Difficulty Modifiers** | DifficultyModifiers (ECS) | Independent enemy HP, damage, timing, resource multipliers |
| **Simplified HUD** | SimplifiedHUD | Hide non-essential HUD elements |
| **Mono Audio** | MonoAudioService | Stereo-to-mono downmix via AudioMixer |

Key behaviors:
- **Opt-in** — All features default to off (zero cost when disabled)
- **Dual persistence** — ScriptableObject defaults + PlayerPrefs overrides
- **ECS bridge** — `DifficultyModifiers` singleton synced by `DifficultyModifierBridgeSystem` (dirty check, no per-frame writes)
- **Settings UI** — All features configurable from DIG > Settings > Accessibility

---

## Step 1: Create AccessibilityProfile Asset

1. Right-click in Project > **Create > DIG > Accessibility > Profile**
2. Name it `AccessibilityProfile`
3. Move to `Assets/Resources/AccessibilityProfile.asset`

This SO defines the factory defaults for all accessibility settings. Players override these via the Settings page (stored in PlayerPrefs).

### Visual Settings

| Field | Default | Range | Description |
|-------|---------|-------|-------------|
| Colorblind Mode | None | Enum | Protanopia, Deuteranopia, Tritanopia, None |
| Colorblind Intensity | 1.0 | 0.0–1.0 | GPU filter strength (0 = off) |
| Text Scale | 1.0 | 0.8–2.0 | Global font size multiplier |
| High Contrast | false | bool | Enable high-contrast theme + outlines |
| Reduced Motion | false | bool | Delegated to MotionIntensitySettings |

### Screen Reader Settings

| Field | Default | Range | Description |
|-------|---------|-------|-------------|
| Screen Reader Enabled | false | bool | Enable platform TTS |
| Speech Rate | 1.0 | 0.5–2.0 | TTS speed multiplier |
| Speech Volume | 0.8 | 0.0–1.0 | TTS volume |

### Motor Settings

| Field | Default | Range | Description |
|-------|---------|-------|-------------|
| Hold To Toggle Actions | (empty) | string[] | Actions converted to toggle: Sprint, Aim, Crouch, Block |
| Double Tap Window | 0.3 | 0.1–1.0s | Time window for double-tap detection |
| Hold Threshold | 0.4 | 0.1–1.0s | Duration to distinguish tap from hold |
| Input Buffer Ms | 100 | 0–500 | Input buffer duration in milliseconds |
| Aim Assist Strength | 0.0 | 0.0–1.0 | Aim assist intensity (0 = off, 1 = max) |

### Audio Settings

| Field | Default | Range | Description |
|-------|---------|-------|-------------|
| Mono Audio | false | bool | Collapse stereo to mono |
| Subtitle Size | Medium | Enum | Small, Medium, Large, ExtraLarge |
| Subtitle Background | 0.7 | 0.0–1.0 | Subtitle background opacity |

### Difficulty Settings

| Field | Default | Range | Description |
|-------|---------|-------|-------------|
| Enemy HP Multiplier | 1.0 | 0.25–2.0 | Scale enemy health |
| Enemy Damage Multiplier | 1.0 | 0.25–2.0 | Scale enemy damage output |
| Timing Window Multiplier | 1.0 | 0.5–2.0 | Scale dodge/parry windows |
| Resource Gain Multiplier | 1.0 | 0.5–3.0 | Scale XP/resource rewards |
| Respawn Penalty | Normal | Enum | None, Light, Normal, Hardcore |
| Simplified HUD | false | bool | Hide non-essential HUD elements |

---

## Step 2: Add AccessibilityService to Boot Scene

1. Find the persistent/boot scene (same one that has `UIServiceBootstrap`)
2. Create a new GameObject named `AccessibilityService`
3. Add the `AccessibilityService` component
4. The service:
   - Runs as a `DontDestroyOnLoad` singleton
   - Loads `AccessibilityProfile` from Resources automatically at Awake
   - Applies all settings at Start
   - Coordinates all accessibility modules

> **Note:** No inspector fields need to be assigned — the profile is loaded from `Resources/AccessibilityProfile` by convention.

---

## Step 3: Add ColorblindFilter to URP Renderer

1. Select your **URP Renderer Asset** (e.g., `UniversalRendererData`)
2. In the Inspector, click **Add Renderer Feature > Colorblind Filter**
3. Configure the feature settings:

| Field | Default | Description |
|-------|---------|-------------|
| Mode | None | Colorblind simulation mode (set at runtime by AccessibilityService) |
| Intensity | 1.0 | Filter strength 0.0–1.0 |
| Correction Material | (required) | Material using `DIG/Accessibility/ColorblindCorrection` shader |

### Create the Correction Material

1. Right-click in Project > **Create > Material**
2. Name it `ColorblindCorrectionMat`
3. Set the shader to **DIG > Accessibility > ColorblindCorrection**
4. Assign this material to the ColorblindFilter's **Correction Material** slot

> **Important:** The filter renders at `AfterRenderingPostProcessing`. It is a fullscreen blit that skips entirely when Mode is set to None.

---

## Step 4: Create High-Contrast Theme (Optional)

If you want high-contrast mode to swap the UI theme:

1. Right-click in Project > **Create > DIG > UI > Theme** (or duplicate your existing theme)
2. Name it `HighContrast`
3. Move to `Assets/Resources/Themes/HighContrast.asset`
4. Configure the theme with higher contrast colors, bolder borders, etc.

`HighContrastMode` loads this theme via `Resources.Load<UIThemeSO>("Themes/HighContrast")` when enabled.

---

## Step 5: Configure AudioMixer for Mono Audio

For mono audio to work, the master AudioMixer needs an exposed parameter:

1. Open your **MasterMixer** AudioMixer asset
2. Add a **Stereo Width** effect to the Master group (or use an existing channel strip)
3. Right-click the stereo width parameter > **Expose Parameter**
4. Rename the exposed parameter to exactly: `StereoWidth`

`MonoAudioService` sets this parameter to `0` (mono) or `1` (full stereo).

---

## Step 6: Register Non-Essential HUD Elements

`SimplifiedHUD` hides elements by USS name when simplified mode is enabled. The following defaults are pre-registered:

- `minimap-decorations`
- `xp-bar-particles`
- `damage-shake-overlay`
- `pulse-indicator`
- `combo-counter`
- `kill-feed`
- `ambient-effects-overlay`

To register additional elements from your systems:

```csharp
SimplifiedHUD.RegisterNonEssential("my-cosmetic-element");
```

Elements must match the USS `name` attribute set on the `VisualElement` in your UXML or code.

---

## Step 7: Difficulty Modifiers — ECS Integration

`DifficultyModifierBridgeSystem` automatically creates a `DifficultyModifiers` ECS singleton at world creation and syncs values from `AccessibilityService` each frame (with dirty check).

Combat systems read the singleton:

```csharp
if (SystemAPI.TryGetSingleton<DifficultyModifiers>(out var mods))
{
    float scaledHP = baseHP * mods.EnemyHPMultiplier;
    float scaledDmg = baseDmg * mods.EnemyDamageMultiplier;
    float scaledWindow = baseWindow * mods.TimingWindowMultiplier;
}
```

| Field | Type | Range | Effect |
|-------|------|-------|--------|
| EnemyHPMultiplier | float | 0.25–2.0 | Scales enemy max HP |
| EnemyDamageMultiplier | float | 0.25–2.0 | Scales enemy damage output |
| TimingWindowMultiplier | float | 0.5–2.0 | Scales dodge/parry timing windows |
| ResourceGainMultiplier | float | 0.5–3.0 | Scales XP/resource rewards |
| RespawnPenalty | enum | None–Hardcore | Respawn penalty level |

> **Architecture:** The singleton lives on a dedicated entity (not the player entity) to avoid the 16KB archetype limit. The bridge system runs in `SimulationSystemGroup` on `ServerSimulation | LocalSimulation`.

---

## Editor Tooling: Accessibility Workstation

Open via **DIG > Accessibility Workstation** in the menu bar.

### Tab 1: Colorblind Preview

- **Simulation Mode** dropdown — Select Protanopia, Deuteranopia, or Tritanopia
- **Intensity** slider — Adjust filter strength
- **Apply to Game View** — Pushes settings to the active ColorblindFilter (requires Play Mode)
- **Reset to None** — Clears the filter
- **Color Palette Preview** — 8 game-relevant color swatches (Red/Green/Blue/Yellow/Orange/Cyan/Purple/White) to visually compare under different modes

### Tab 2: Screen Reader Test

- **Text to Speak** field — Enter custom text
- **Speak** button — Trigger TTS with the entered text
- **Quick Tests** — Pre-built buttons: "Menu opened", "Level Up! You reached level 10.", "Item acquired: Legendary Sword"

> **Platform:** macOS uses the `say` command. Windows uses PowerShell SAPI5. No additional setup required.

### Tab 3: CVAA Checklist

Static 15-point compliance checklist showing pass/fail status for each accessibility feature:

Colorblind Support, Text Scaling, High Contrast Mode, Screen Reader / TTS, Remappable Controls, One-Handed Presets, Hold-to-Toggle, Input Timing Adjustments, Aim Assist, Difficulty Modifiers, Subtitles & Captions, Sound Radar / Visual Audio, Mono Audio, Reduced Motion, Simplified HUD

### Tab 4: Difficulty Tuner

- **Load from AccessibilityService** — Pull current values (Play Mode only)
- **Sliders** — Enemy HP, Enemy Damage, Timing Window, Resource Gain multipliers
- **Respawn Penalty** dropdown
- **Projected Values** — Preview calculations (e.g., "Enemy with 100 HP -> 50 HP")
- **Apply to AccessibilityService** — Push values to live service (Play Mode only)

---

## Settings Page

All accessibility features are exposed in **DIG > Settings > Accessibility**. The page is divided into sections:

| Section | Controls |
|---------|----------|
| **Visual** | Colorblind mode dropdown, colorblind intensity slider, font scale slider, high contrast toggle, reduced motion toggle |
| **Screen Reader** | Enable toggle, speech rate slider, speech volume slider |
| **Motor Accessibility** | Hold-to-toggle toggles (Sprint, Aim, Crouch, Block), double-tap window slider, hold threshold slider, input buffer slider, aim assist strength slider, one-handed preset dropdown |
| **Audio Accessibility** | Mono audio toggle, subtitle background opacity slider |
| **Difficulty & Cognitive** | Enemy HP/damage/timing/resource sliders, respawn penalty dropdown, simplified HUD toggle |

All changes follow the snapshot/apply/revert pattern and persist to PlayerPrefs.

---

## PlayerPrefs Reference

All keys use the `Access_` prefix:

| Key | Type | Default | Module |
|-----|------|---------|--------|
| `Access_ColorblindIntensity` | float | 1.0 | AccessibilityService |
| `Access_ScreenReader` | int (bool) | 0 | AccessibilityService |
| `Access_SpeechRate` | float | 1.0 | AccessibilityService |
| `Access_SpeechVolume` | float | 0.8 | AccessibilityService |
| `Access_MonoAudio` | int (bool) | 0 | AccessibilityService |
| `Access_SubtitleBgOpacity` | float | 0.7 | AccessibilityService |
| `Access_AimAssist` | float | 0.0 | AccessibilityService |
| `Access_SimplifiedHUD` | int (bool) | 0 | AccessibilityService |
| `Access_EnemyHP` | float | 1.0 | AccessibilityService |
| `Access_EnemyDamage` | float | 1.0 | AccessibilityService |
| `Access_TimingWindow` | float | 1.0 | AccessibilityService |
| `Access_ResourceGain` | float | 1.0 | AccessibilityService |
| `Access_RespawnPenalty` | int (enum) | 2 | AccessibilityService |
| `Access_AimAssistStrength` | float | 0.0 | AimAssistService |
| `Access_DoubleTapWindow` | float | 0.3 | InputTimingService |
| `Access_HoldThreshold` | float | 0.4 | InputTimingService |
| `Access_InputBufferMs` | int | 100 | InputTimingService |
| `Access_Toggle_Sprint` | int (bool) | 0 | HoldToToggleService |
| `Access_Toggle_Aim` | int (bool) | 0 | HoldToToggleService |
| `Access_Toggle_Crouch` | int (bool) | 0 | HoldToToggleService |
| `Access_Toggle_Block` | int (bool) | 0 | HoldToToggleService |

---

## Troubleshooting

### Colorblind filter not visible
- Verify ColorblindFilter is added to the URP Renderer Asset
- Verify Correction Material is assigned (shader: `DIG/Accessibility/ColorblindCorrection`)
- Mode must be set to something other than None
- Filter only renders in Play Mode

### Screen reader no sound
- macOS: Verify `/usr/bin/say` exists (standard on all macOS)
- Windows: Verify PowerShell is available and `System.Speech` assembly is installed
- Check that Screen Reader is enabled in Settings > Accessibility

### Mono audio not working
- Verify AudioMixer has an exposed parameter named exactly `StereoWidth`
- Verify `AudioManager` MonoBehaviour exists in scene with `MasterMixer` assigned

### High contrast theme not loading
- Verify `HighContrast.asset` is at `Assets/Resources/Themes/HighContrast.asset`
- Must be a `UIThemeSO` ScriptableObject

### Difficulty modifiers not affecting gameplay
- `DifficultyModifierBridgeSystem` runs automatically — no setup needed
- Combat systems must read `DifficultyModifiers` singleton via `SystemAPI.TryGetSingleton`
- Check the Difficulty Tuner tab in Accessibility Workstation (Play Mode)

### Simplified HUD not hiding elements
- Elements must have matching USS `name` attributes
- Default names are pre-registered; custom elements need `SimplifiedHUD.RegisterNonEssential()`
- Call `SimplifiedHUD.Refresh()` after HUD rebuilds

### Hold-to-toggle not working
- Verify the action is enabled in Settings > Accessibility > Motor
- Supported actions: Sprint, Aim, Crouch, Block
- Toggle state resets on scene transitions

---

## Verification Checklist

- [ ] `AccessibilityProfile` asset exists at `Assets/Resources/AccessibilityProfile.asset`
- [ ] `AccessibilityService` component on a DontDestroyOnLoad GameObject in boot scene
- [ ] ColorblindFilter added to URP Renderer with correction material assigned
- [ ] Enter Play Mode > `AccessibilityService.Instance` is not null (check via Accessibility Workstation)
- [ ] Accessibility Workstation > Colorblind Preview > set Deuteranopia > Apply > verify filter visible in Game View
- [ ] Accessibility Workstation > Screen Reader > Speak > verify TTS audio output
- [ ] Accessibility Workstation > Difficulty Tuner > set Enemy HP to 0.5 > Apply > verify projected values
- [ ] Settings > Accessibility > toggle Hold-to-Toggle Sprint > verify sprint toggles on key press
- [ ] Settings > Accessibility > adjust text scale slider > verify UI text resizes
- [ ] Settings > Accessibility > enable Mono Audio > verify stereo collapses to center
- [ ] CVAA Checklist tab shows 15/15 features implemented
