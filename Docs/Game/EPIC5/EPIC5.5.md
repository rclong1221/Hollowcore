# EPIC 5.5: Echo UI & Discovery

**Status**: Planning
**Epic**: EPIC 5 — Echo Missions
**Dependencies**: EPIC 5.1; EPIC 6 (Gate Screen), EPIC 12 (Scar Map)

---

## Overview

Players need to discover, track, and make risk/reward decisions about echoes. The UI surfaces echo information at three points: the Gate Screen (strategic planning), in-district zone markers (tactical awareness), and the Scar Map (expedition overview). Wrongness is audible before visible — creepy audio cues signal echo proximity.

---

## UI Components

### Gate Screen Echo Info (EPIC 6 integration)
- Backtrack gates show per-district echo summary:
  - Echo count: "2 active echoes"
  - Reward previews: "[Rare Limb], [Boss Counter Token]"
  - Difficulty indicator: skull count (1-4) based on DifficultyMultiplier
  - Legendary marker: special glow for Legendary+ echoes
- Helps inform the backtrack decision: "Is the rare limb worth Phase 3 + 2 echoes?"

### In-District Echo Markers
- Zone map: echo spiral icon at echo zone locations
- Approach warning: 30m radius — audio distortion starts
- 15m radius: visual distortion (screen edge warping, color shift)
- Zone entrance: full wrongness overlay + echo objective display
- Echo prompt: "Echo Mission: [Name] — [Objective] — Difficulty: [skulls]"

### Echo Log
- Accessible from pause menu
- Lists all known echoes across the expedition
- Per echo: source quest, district, mutation type, difficulty, reward preview, status
- Filter by: district, reward type, difficulty, status (active/completed/legendary)

### Scar Map Integration (EPIC 12)
- Echo spiral icons at zone coordinates
- Color-coded by tier: white (normal), blue (persistent), gold (legendary), red (mythic)
- Hover: shows reward preview and difficulty
- Completed echoes shown as faded spirals (narrative record)

---

## Audio Design

```
// Echo proximity audio layers:
// 30m: subtle wrongness — reversed ambient sounds, pitch-shifted environmental audio
// 15m: heartbeat layer — low thrum matching echo intensity
// Zone entry: full echo audio — distorted music, temporal stuttering, faction-specific cues
//   Necrospire echoes: layered dead voices, data corruption static
//   Burn echoes: reversed furnace roar, cooling metal pings
//   Lattice echoes: structural groaning played backwards, wind tunnel distortion
```

---

## Systems

### EchoDiscoverySystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoDiscoverySystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Manages echo proximity detection for UI/audio:
//   For each active echo zone:
//     Calculate distance from player to echo center
//     If < 30m: trigger audio layer 1
//     If < 15m: trigger visual distortion + audio layer 2
//     If in zone: full wrongness overlay
```

### EchoUIBridgeSystem

```csharp
// File: Assets/Scripts/Echo/Bridges/EchoUIBridgeSystem.cs
// Managed system, PresentationSystemGroup
//
// Bridges ECS echo state to UI elements:
//   - Updates zone map markers
//   - Populates echo log panel data
//   - Feeds Gate Screen echo previews
//   - Updates Scar Map echo markers
```

---

## Setup Guide

1. Create echo UI prefabs: zone markers, proximity warning overlay, echo log panel
2. Configure audio layers: wrongness ambient, heartbeat, full echo per district
3. Integrate echo info into Gate Screen backtrack gate cards (EPIC 6)
4. Add echo spiral icons to Scar Map renderer (EPIC 12)
5. Echo log accessible from pause menu

---

## Verification

- [ ] Gate Screen shows echo count + rewards on backtrack gates
- [ ] In-district: echo zone markers visible on zone map
- [ ] Audio wrongness starts at 30m, intensifies at 15m
- [ ] Visual distortion on zone approach
- [ ] Echo log lists all known echoes with correct data
- [ ] Scar Map echo spirals color-coded by tier
- [ ] Completed echoes shown as faded on Scar Map

---

## Editor Tooling

```csharp
// File: Assets/Editor/EchoWorkstation/EchoUIPreviewPanel.cs
// Editor panel for previewing echo UI elements:
//
// Features:
//   - Gate Screen mock: simulated gate card with echo info
//     - Editable: echo count, reward types, difficulty skulls, legendary flag
//     - Preview rendering of the actual UI prefab in editor
//   - Zone marker preview: shows echo spiral icon at different sizes/colors
//     - Tier color preview: Normal(white), Persistent(blue), Legendary(gold), Mythic(red)
//   - Proximity warning preview: simulated screen-edge warping at different intensities
//   - Echo log mock: populated with sample data, sortable/filterable
//   - Scar Map mock: minimap with echo spiral icons placed at sample coordinates
//   - Audio preview: play wrongness audio layers at different proximity thresholds
//     (requires AudioSource in editor scene)
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Echo/Components/EchoUIRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Echo
{
    /// <summary>
    /// Runtime-tunable echo UI and discovery parameters.
    /// </summary>
    public struct EchoUIRuntimeConfig : IComponentData
    {
        /// <summary>Distance at which audio wrongness begins (meters). Default 30.</summary>
        public float AudioWrongnessRadius;

        /// <summary>Distance at which visual distortion begins (meters). Default 15.</summary>
        public float VisualDistortionRadius;

        /// <summary>Maximum screen-edge warping intensity (0-1). Default 0.3.</summary>
        public float MaxVisualDistortionIntensity;

        /// <summary>Whether to show reward previews on Gate Screen (spoiler control). Default true.</summary>
        public bool ShowRewardPreviews;

        /// <summary>Whether to show difficulty skulls on Gate Screen. Default true.</summary>
        public bool ShowDifficultySkulls;

        public static EchoUIRuntimeConfig Default => new()
        {
            AudioWrongnessRadius = 30f,
            VisualDistortionRadius = 15f,
            MaxVisualDistortionIntensity = 0.3f,
            ShowRewardPreviews = true,
            ShowDifficultySkulls = true,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Echo/Debug/EchoDiscoveryDebugOverlay.cs
// In-game overlay for echo discovery system debugging:
//
//   - Proximity rings: visible circles at 30m and 15m around each echo zone center
//     - 30m ring: dashed yellow (audio threshold)
//     - 15m ring: solid purple (visual threshold)
//   - Distance readout: "Echo 'Drowned Memory': 22.4m" updating in real-time
//   - Audio layer status: "Layer 1: ON | Layer 2: OFF | Full: OFF"
//   - Visual distortion intensity: numeric value (0.00-1.00) displayed on screen
//   - Zone detection: "Player in echo zone: YES/NO" with zone ID
//   - Scar Map debug: all echo positions shown as world-space vertical beams (visible from anywhere)
//   - Echo log bridge status: "EchoUIBridgeSystem: running, last update frame N, echoes tracked: K"
//   - Toggle: always on when inside echo proximity in debug builds
```
