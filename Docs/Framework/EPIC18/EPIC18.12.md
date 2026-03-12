# EPIC 18.12: Accessibility Framework

**Status:** PLANNED
**Priority:** Medium-High (Regulatory compliance + inclusive design + player reach)
**Dependencies:**
- `AudioAccessibilityConfig` (existing — `DIG.Audio.Accessibility`, `Assets/Scripts/Audio/Accessibility/AudioAccessibilityConfig.cs`, subtitle/radar settings)
- `DirectionalSubtitleManager` (existing — `DIG.Audio.Accessibility`, `Assets/Scripts/Audio/Accessibility/DirectionalSubtitleManager.cs`, directional subtitle display)
- `SoundRadarRenderer` / `SoundRadarSystem` (existing — `DIG.Audio.Accessibility`, `Assets/Scripts/Audio/Accessibility/SoundRadarRenderer.cs`, visual sound radar HUD)
- `InputGlyphProvider` / `InputGlyphDatabase` (existing — `DIG.UI.Core.Input`, input glyph swapping per device)
- `MotionIntensitySettings` (existing — `DIG.Core.Settings`, `Assets/Scripts/Core/Settings/MotionIntensitySettings.cs`, camera shake/bob reduction)
- `InputParadigmProfile` (existing — `DIG.Core.Input.Paradigm`, input paradigm configuration)
- `SettingsService` (EPIC 18.2 — centralized settings storage)
- `AccessibilitySettingsPage` (EPIC 18.2 — accessibility settings UI page)
- `NotificationService` (EPIC 18.3 — notification display)

**Feature:** A comprehensive accessibility framework implementing CVAA/WCAG guidelines with colorblind simulation and correction filters, text scaling and high-contrast modes, screen reader integration (platform TTS APIs), remappable controls with one-handed presets, motor accessibility (auto-aim assist, hold-to-toggle conversion, input timing adjustments), cognitive accessibility (difficulty modifiers, simplified UI mode, objective markers), and audio accessibility (visual sound indicators, subtitle customization, mono audio). All features are opt-in, stored in `SettingsService`, and testable via editor tooling.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|--------|--------|-------|
| `AudioAccessibilityConfig` | `Assets/Scripts/Audio/Accessibility/AudioAccessibilityConfig.cs` | Implemented | Subtitle size/color, sound radar enable/disable |
| `DirectionalSubtitleManager` | `Assets/Scripts/Audio/Accessibility/DirectionalSubtitleManager.cs` | Implemented | Positional subtitles with direction indicators |
| `SoundRadarRenderer` + `SoundRadarSystem` | `Assets/Scripts/Audio/Accessibility/SoundRadar*.cs` | Implemented | HUD overlay showing sound source directions |
| `InputGlyphProvider` | `Assets/Scripts/UI/Core/Input/InputGlyphProvider.cs` | Implemented | Auto-switching input glyphs (keyboard/controller) |
| `MotionIntensitySettings` | `Assets/Scripts/Core/Settings/MotionIntensitySettings.cs` | Implemented | Camera shake/bob intensity control |
| `KeybindService` | `Assets/Scripts/Core/Input/Keybinds/KeybindService.cs` | Fully implemented | Full control remapping |

### What's Missing

- **No colorblind filters** — no Protanopia/Deuteranopia/Tritanopia simulation or correction
- **No text scaling** — no global text size adjustment beyond default
- **No high-contrast mode** — no toggle for simplified, high-contrast UI
- **No screen reader integration** — no TTS for UI elements, menu navigation, notifications
- **No one-handed presets** — keybind service supports remapping but no pre-built one-handed layouts
- **No hold-to-toggle conversion** — all hold-based actions (sprint, aim) must be held; no toggle option
- **No input timing adjustments** — no configurable input buffer window, double-tap delay, hold duration
- **No auto-aim assist** — no aim magnetism or bullet curve for motor-impaired players
- **No difficulty modifiers** — no way to independently adjust enemy HP, damage, timing windows
- **No cognitive accessibility** — no simplified HUD mode, no persistent objective markers
- **No mono audio** — no stereo-to-mono downmix for single-ear hearing
- **No centralized accessibility manager** — audio accessibility is in Audio/, visual in Settings/, input in Core/Input/ — no unified framework

---

## Problem

Accessibility is both a moral imperative and increasingly a legal requirement (CVAA in US, EAA in EU). DIG has strong foundations — directional subtitles, sound radar, input glyphs, key remapping, and motion intensity settings — but these are scattered across subsystems with no unified framework. Critical features are missing: colorblind support (affects 8% of male players), text scaling, screen reader support, hold-to-toggle, and cognitive accessibility aids. A unified accessibility framework ensures consistent implementation, easier testing, and complete coverage.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  AccessibilityProfileSO         ColorblindProfileSO
  (all accessibility settings    (LUT textures for each
   as structured data, with       colorblind type, simulation
   per-setting descriptions       and correction matrices)
   and categories)
        |                            |
        └──── AccessibilityService (MonoBehaviour singleton) ─┘
              (central coordinator, reads from SettingsService,
               applies settings to subsystems, provides unified API)
                         |
        ┌────────────────┼──────────────────────────┐
        |                |                          |
  Visual Module      Motor Module           Cognitive Module
  (colorblind,       (hold-to-toggle,       (difficulty
   text scale,        input timing,          modifiers,
   high contrast,     auto-aim, one-         simplified UI,
   screen reader)     handed presets)        objective markers)
        |                |                          |
  Audio Module (enhanced)
  (existing subtitle/radar +
   mono audio, visual sound
   indicators, caption
   customization)
                         |
              Post-Process Integration
              (colorblind filter as URP
               Renderer Feature or
               fullscreen shader pass)
                         |
                 EDITOR TOOLING
                         |
  AccessibilityWorkstationModule
  (colorblind simulation preview,
   text scale preview, screen reader
   test, checklist against CVAA/WCAG)
```

---

## Accessibility Categories

### 1. Visual Accessibility

#### Colorblind Support

**File:** `Assets/Scripts/Accessibility/Visual/ColorblindFilter.cs`

- URP Renderer Feature or fullscreen blit shader
- Modes: Protanopia, Deuteranopia, Tritanopia (simulation for testing, correction for gameplay)
- Uses Daltonization algorithm (color matrix transform) — GPU-side, zero CPU cost
- Configurable intensity slider (0-100%)

```csharp
public enum ColorblindMode
{
    None,
    ProtanopiaCorrect,     // Red-green (correct for gameplay)
    DeuteranopiaCorrect,   // Red-green variant
    TritanopiaCorrect,     // Blue-yellow
    ProtanopiaSimulate,    // Simulate for testing (editor-only)
    DeuteranopiaSimulate,
    TritanopiaSimulate
}
```

#### Text Scaling

**File:** `Assets/Scripts/Accessibility/Visual/TextScaleService.cs`

- Global text scale multiplier (80% to 200%)
- Applied via USS custom property `--text-scale` propagated to root UIDocument
- UGUI: override TMP_Text.fontSize on all active text components via tag search
- Preserves relative sizes (headers remain larger than body)

#### High-Contrast Mode

**File:** `Assets/Scripts/Accessibility/Visual/HighContrastMode.cs`

- Swaps UIThemeSO to a high-contrast variant (black/white/yellow)
- Increases border widths, adds outlines to interactive elements
- Game world: increases enemy outline shader thickness
- HUD: removes decorative elements, increases icon sizes

#### Screen Reader

**File:** `Assets/Scripts/Accessibility/Visual/ScreenReaderBridge.cs`

- Platform TTS integration:
  - Windows: SAPI5 (`System.Speech.Synthesis`)
  - macOS: NSSpeechSynthesizer (native plugin)
  - Fallback: no-op with visual text emphasis instead
- Reads aloud: focused UI element labels, button text, notification content
- `ScreenReaderBridge.Speak(string text, SpeechPriority priority)` API
- Interrupt on new high-priority speech
- Rate/pitch/volume configurable

### 2. Motor Accessibility

#### Hold-to-Toggle Conversion

**File:** `Assets/Scripts/Accessibility/Motor/HoldToToggleService.cs`

- Configurable per-action: Sprint, Aim, Crouch, Block
- When enabled: first press activates, second press deactivates
- Integration with existing InputAction system via processor injection

#### Input Timing

**File:** `Assets/Scripts/Accessibility/Motor/InputTimingService.cs`

- Adjustable parameters:
  - Double-tap window: 0.1s to 1.0s (default 0.3s)
  - Hold threshold: 0.1s to 1.0s (default 0.4s)
  - Input buffer window: 0 to 500ms (default 100ms)
  - Repeat delay/rate for held buttons

#### Auto-Aim Assist

**File:** `Assets/Scripts/Accessibility/Motor/AimAssistService.cs`

- Configurable strength (0 = off, 100 = full lock-on):
  - **Magnetism:** Cursor/reticle snaps toward nearest enemy within cone
  - **Bullet Curve:** Projectiles curve slightly toward nearest enemy
  - **Slowdown:** Aim sensitivity reduces when crosshair is near enemy
- Only active when explicitly enabled — does not affect competitive modes

#### One-Handed Presets

**File:** `Assets/Scripts/Accessibility/Motor/OneHandedPresets.cs`

- Pre-built keybind profiles:
  - Left-hand only (all bindings on left side of keyboard + mouse)
  - Right-hand only (numpad + mouse)
  - Controller one-handed (remapped to single Joy-Con style)
- Applied via `KeybindService.LoadProfile()`

### 3. Cognitive Accessibility

#### Difficulty Modifiers

**File:** `Assets/Scripts/Accessibility/Cognitive/DifficultyModifiers.cs`

- Independent sliders (not a single "easy/hard" toggle):
  - Enemy HP multiplier (0.25x to 2x)
  - Enemy damage multiplier (0.25x to 2x)
  - Timing window multiplier (0.5x to 2x) — for dodge/parry
  - Resource gain multiplier (0.5x to 3x)
  - Respawn penalty (none, light, normal, hardcore)
- Applied via ECS singleton component read by combat/progression systems

#### Simplified HUD

**File:** `Assets/Scripts/Accessibility/Cognitive/SimplifiedHUD.cs`

- Reduces HUD clutter:
  - Hides non-essential widgets (minimap decorations, XP bar particles)
  - Enlarges critical info (HP bar, ammo count)
  - Reduces animation (no pulsing, no shaking numbers)
- Toggle via settings

#### Persistent Objective Markers

**File:** `Assets/Scripts/Accessibility/Cognitive/ObjectiveMarkerService.cs`

- Always-visible 3D waypoints for active quest objectives
- Screen-edge indicators when objective is off-screen
- Distance labels
- Can be toggled independently of quest compass

### 4. Audio Accessibility (Enhanced)

#### Mono Audio

**File:** `Assets/Scripts/Accessibility/Audio/MonoAudioService.cs`

- Downmixes stereo to mono for single-ear hearing
- Applied via AudioMixer parameter
- Preserves volume levels

#### Enhanced Subtitles

- Builds on existing `DirectionalSubtitleManager`:
  - Background opacity slider
  - Font size (Small/Medium/Large/Extra Large)
  - Speaker name coloring
  - Sound effect captions (e.g., "[gunshot nearby]", "[footsteps behind]")
  - Timing: subtitles persist longer for slow readers (configurable WPM)

#### Visual Sound Indicators

- Enhanced `SoundRadarSystem`:
  - Color-coded by sound type (red = danger, green = ally, yellow = ambient)
  - Intensity scaling by volume
  - Optional screen flash on loud sounds

---

## AccessibilityService

**File:** `Assets/Scripts/Accessibility/AccessibilityService.cs`

- MonoBehaviour singleton, `DontDestroyOnLoad`
- Reads all accessibility settings from `SettingsService` (EPIC 18.2)
- Coordinates all accessibility modules
- API:
  - `void ApplyAllSettings()` — refresh all modules from current settings
  - `void SetColorblindMode(ColorblindMode mode)` — apply colorblind filter
  - `void SetTextScale(float scale)` — apply text scaling
  - `void Speak(string text)` — screen reader shortcut
  - `bool IsFeatureEnabled(string featureKey)` — query feature state
  - `AccessibilityReport GenerateReport()` — CVAA compliance checklist

---

## ScriptableObjects

### AccessibilityProfileSO

**File:** `Assets/Scripts/Accessibility/Config/AccessibilityProfileSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| ColorblindMode | ColorblindMode | None | Active colorblind filter |
| ColorblindIntensity | float [0-1] | 1.0 | Filter strength |
| TextScale | float [0.8-2.0] | 1.0 | Global text multiplier |
| HighContrast | bool | false | High contrast mode |
| ScreenReaderEnabled | bool | false | TTS for UI |
| HoldToToggleActions | string[] | empty | Actions using toggle mode |
| DoubleTapWindow | float | 0.3 | Double-tap timing |
| HoldThreshold | float | 0.4 | Hold detection threshold |
| InputBufferMs | int | 100 | Input buffer window |
| AimAssistStrength | float [0-1] | 0 | Aim assist intensity |
| MonoAudio | bool | false | Stereo to mono |
| SubtitleSize | SubtitleSize | Medium | Caption text size |
| SubtitleBackground | float [0-1] | 0.7 | Caption background opacity |
| ReducedMotion | bool | false | Reduce screen shake and animations |
| SimplifiedHUD | bool | false | Simplified HUD mode |
| EnemyHPMultiplier | float | 1.0 | Difficulty: enemy HP |
| EnemyDamageMultiplier | float | 1.0 | Difficulty: enemy damage |
| TimingWindowMultiplier | float | 1.0 | Difficulty: dodge/parry timing |

---

## Editor Tooling

### AccessibilityWorkstationModule

**File:** `Assets/Editor/AccessibilityWorkstation/Modules/AccessibilityWorkstationModule.cs`

- **Colorblind Preview:** View Game view through Protanopia/Deuteranopia/Tritanopia simulation filters in real-time
- **Text Scale Preview:** Preview UI at different text scales without entering Play mode
- **CVAA Checklist:** Interactive checklist of CVAA/WCAG requirements with compliance status
- **Screen Reader Test:** Type text and hear TTS output
- **Motor Preset Preview:** Show keybind layout diagrams for one-handed presets
- **Difficulty Tuner:** Adjust difficulty modifiers and see projected values

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Accessibility/AccessibilityService.cs` | MonoBehaviour | ~200 |
| `Assets/Scripts/Accessibility/Config/AccessibilityProfileSO.cs` | ScriptableObject | ~60 |
| `Assets/Scripts/Accessibility/Visual/ColorblindFilter.cs` | RendererFeature | ~120 |
| `Assets/Scripts/Accessibility/Visual/TextScaleService.cs` | Class | ~60 |
| `Assets/Scripts/Accessibility/Visual/HighContrastMode.cs` | Class | ~80 |
| `Assets/Scripts/Accessibility/Visual/ScreenReaderBridge.cs` | Class | ~100 |
| `Assets/Scripts/Accessibility/Motor/HoldToToggleService.cs` | Class | ~80 |
| `Assets/Scripts/Accessibility/Motor/InputTimingService.cs` | Class | ~60 |
| `Assets/Scripts/Accessibility/Motor/AimAssistService.cs` | Class | ~120 |
| `Assets/Scripts/Accessibility/Motor/OneHandedPresets.cs` | Class | ~50 |
| `Assets/Scripts/Accessibility/Cognitive/DifficultyModifiers.cs` | Class | ~60 |
| `Assets/Scripts/Accessibility/Cognitive/SimplifiedHUD.cs` | Class | ~50 |
| `Assets/Scripts/Accessibility/Cognitive/ObjectiveMarkerService.cs` | MonoBehaviour | ~80 |
| `Assets/Scripts/Accessibility/Audio/MonoAudioService.cs` | Class | ~30 |
| `Assets/Shaders/Accessibility/ColorblindCorrection.shader` | Shader | ~80 |
| `Assets/Editor/AccessibilityWorkstation/Modules/AccessibilityWorkstationModule.cs` | Editor | ~250 |

**Total estimated:** ~1,480 lines

---

## Performance Considerations

- Colorblind filter is a single fullscreen shader pass — ~0.1ms on modern GPUs
- Text scaling modifies USS variables (not per-element updates) — propagates via UI Toolkit's style system in O(1)
- Screen reader TTS is async platform API call — no main thread blocking
- Hold-to-toggle injects an InputProcessor — processed within Input System pipeline, no additional Update cost
- Aim assist uses spatial hash for enemy lookup — O(1) nearest-enemy query
- Difficulty modifiers are ECS singleton reads — already happening in combat systems, just multiplied by modifier values
- All modules are opt-in — disabled features have zero CPU cost

---

## Testing Strategy

- Unit test colorblind matrix: verify Daltonization output for known input colors
- Unit test text scaling: verify all text elements scale proportionally
- Unit test hold-to-toggle: press → verify ON, press again → verify OFF
- Unit test input timing: verify double-tap detection at various window sizes
- Unit test difficulty modifiers: verify combat calculations use multipliers
- Integration test: enable colorblind mode → verify shader applied to game view
- Integration test: enable screen reader → verify TTS speaks UI element on focus
- Integration test: enable aim assist → verify cursor magnetism near enemies
- Editor test: colorblind simulation preview in AccessibilityWorkstationModule
- Compliance test: run CVAA checklist → verify all items passing
