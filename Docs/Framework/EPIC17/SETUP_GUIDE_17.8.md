# EPIC 17.8: Weather & Day-Night Cycle System — Setup Guide

## Overview

The Weather & Day-Night Cycle System is a server-authoritative framework that drives a world clock, seasonal weather state machine, and client-side atmosphere (lighting, skybox, shaders, audio, VFX). The server owns time and weather state as ghost-replicated singletons; clients drive local presentation from the replicated data. Gameplay systems modify AI vision range, surface friction, and movement speed based on active weather conditions.

For designers and developers, the system provides three ScriptableObject config assets, three authoring components, a **Weather Workstation** editor window, and a global shader include for custom material integration.

---

## 1. Quick Start: Creating Config Assets

Two config assets must exist in a `Resources` folder for the bootstrap to load them at runtime.

### Day-Night Config

1. Right-click in the Project window: **Create → DIG → Weather → Day-Night Config**.
2. Name it `DayNightConfig` and place it in `Assets/Resources/DayNightConfig.asset`.
3. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| Day Length Seconds | 1200 (20 min) | Real-world seconds per full game day. Min 60. |
| Season Length Days | 7 | Game days before the season advances. |
| Sunrise Hour | 6.0 | Hour (0-12) when the sun appears above the horizon. |
| Sunset Hour | 18.0 | Hour (12-24) when the sun drops below the horizon. |
| Sun Pitch Max | 75 | Maximum sun elevation in degrees at solar noon. |
| Moon Pitch Max | 40 | Moon elevation at midnight. |
| Night Ambient Intensity | 0.05 | Ambient light intensity at midnight. |
| Day Ambient Intensity | 1.0 | Ambient light intensity at noon. |
| Sun Color Gradient | warm sunrise → white noon → orange dusk | Directional light color over the 24-hour cycle. |
| Ambient Color Gradient | dark blue → warm noon → dark blue | Ambient light color over the 24-hour cycle. |
| Sun Intensity Curve | sine curve | Directional light intensity (0-1) over 24 hours. |
| Star Visibility Curve | 1 at night, 0 during day | Controls star layer opacity on the skybox shader. |
| Start Time Of Day | 8.0 | Time (hours) when the world clock begins. |

**Skybox materials** (optional): Assign up to four materials for Dawn, Day, Dusk, and Night. The system cross-fades between them based on time of day. If left unassigned, the current scene skybox is used as-is and only shader properties are driven.

> **Tip:** When you first create a DayNightConfig asset, click it in the Inspector and use the gear menu → **Reset** to populate all gradients and curves with sensible defaults.

### Weather Config

1. Right-click: **Create → DIG → Weather → Weather Config**.
2. Name it `WeatherConfig` and place it in `Assets/Resources/WeatherConfig.asset`.
3. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| Weather Change Interval Min/Max | 180 / 600 | Seconds between weather transitions. |
| Default Weather | Clear | Starting weather type. |
| Default Season | Summer | Starting season. |
| Random Seed | 0 (time-based) | Deterministic seed for reproducible weather sequences. Set to 0 for random. |
| Base Temperature | [15, 25, 12, -2] | Celsius baseline per season (Spring, Summer, Autumn, Winter). |

#### Transition Probabilities

Under **Transition Probabilities**, add entries to define weighted random transitions. Each entry specifies:
- **From Weather**: The current weather type.
- **Season**: Which season this row applies to.
- **Weights**: An array of 10 floats, one per target weather type (Clear through Sandstorm). Higher weight = higher probability.

Example: To make Clear weather in Summer transition to PartlyCloudy 40% and stay Clear 60%, add an entry with `FromWeather = Clear`, `Season = Summer`, and `Weights = [0.6, 0.4, 0, 0, 0, 0, 0, 0, 0, 0]`.

If a row is left empty or all-zero, the system defaults to 50% stay in current weather + 50% transition to Clear.

#### Per-Weather Target Parameters

Under **Weather Target Params**, add entries to define the target intensity values for each weather type. Each entry controls what the weather "looks like" at full intensity:

| Per-Weather Field | Description |
|-------------------|-------------|
| Rain / Snow Intensity | 0-1 target for particle density and shader wetness. |
| Fog Density | 0-1 target for RenderSettings fog. |
| Wind Speed | Target wind speed in m/s (affects particles + shaders). |
| Temperature Offset | Added to season base temperature. |
| Transition Duration Min/Max | Seconds to lerp from previous weather into this one. |
| Min/Max Duration | How long this weather persists before rolling the next transition. |
| Lightning Interval Min/Max | Seconds between lightning strikes. 0 = no lightning (only relevant for Thunderstorm). |

If no entry exists for a weather type, hardcoded defaults are used.

#### Gameplay Modifiers

Under **Gameplay Modifiers**, add entries per weather type to tune gameplay impact:

| Field | Default (Clear) | Description |
|-------|-----------------|-------------|
| Vision Range Multiplier | 1.0 | Multiplied into AI detection range. 0.3 = severe fog. |
| Movement Speed Multiplier | 1.0 | Applied to entities with the weather modifier component. |
| Surface Friction Multiplier | 1.0 | Wetness factor for SurfaceSlipSystem. |
| Noise Multiplier | 1.0 | Ambient noise scaling (future use). |
| Projectile Speed Multiplier | 1.0 | Wind effect on projectile velocity (future use). |

---

## 2. Setting Up the Subscene

### Weather Bootstrap (Required)

One object in your subscene must have the bootstrap authoring component.

1. Create an empty GameObject in your gameplay subscene (e.g., name it `WeatherBootstrap`).
2. **Add Component → DIG → Weather → Weather Bootstrap**.
3. Assign the `DayNightConfig` and `WeatherConfig` assets to the corresponding slots.

At runtime the bootstrap system loads these configs from `Resources/`, builds BlobAssets for O(1) lookups, and creates the `WorldTimeState`, `WeatherState`, and related singletons. It runs once and then disables itself.

> **Important:** Only one WeatherBootstrap should exist in the world. If you have multiple subscenes, place it in your persistent/core subscene.

### Player Prefab (Required for Gameplay Modifiers)

For weather to affect a player or AI entity's vision range and movement speed:

1. Open your player/AI prefab (e.g., `Warrok_Server`).
2. **Add Component → DIG → Weather → Weather Gameplay Modifier**.

This adds three lightweight components at bake time:
- `WeatherVisionModifier` (read by `DetectionSystem` to scale AI sight range)
- `WeatherMovementModifier` (available for movement systems to read)
- `LocalWeatherOverride` (used by the zone system for per-biome overrides)

No configuration is needed on this component -- the system writes values automatically based on the active weather state.

### Weather Zones (Optional)

Weather zones let you override the global weather in specific areas (e.g., a desert biome that always has Sandstorm, a cave that is always Clear).

1. Create a GameObject in your subscene where you want the zone.
2. Position and scale it to define the zone center and size.
3. **Add Component → DIG → Weather → Weather Zone**.
4. Configure either by assigning a **Weather Zone Config** ScriptableObject (create via **Create → DIG → Weather → Weather Zone Config**) or by using the inline fields:

| Field | Description |
|-------|-------------|
| Biome Type | Byte index for future per-biome transition tables. |
| Weather Override | The forced weather type in this zone. Leave at the default (255) to use global weather with no override. |
| Priority | When zones overlap, the highest priority zone wins. |
| Blend Radius | Edge blend distance in meters. 20% of the zone radius is used as a smooth transition band. |

The `Radius` field on the baked `WeatherZone` component determines the zone's effective area (measured from the GameObject's position).

---

## 3. Audio Mixer Setup

The weather audio system drives four exposed parameters on the `MasterMixer`. These must be set up in your AudioMixer asset.

1. Open the **Audio Mixer** window (**Window → Audio → Audio Mixer**).
2. On your `MasterMixer` (or whichever mixer is assigned to `AudioManager.MasterMixer`), create four groups or use existing groups and **expose the following parameters** by right-clicking each volume → **Expose**:

| Exposed Parameter Name | Purpose |
|------------------------|---------|
| `RainVolume` | Controls rain ambient loop volume (dB). |
| `WindVolume` | Controls wind ambient loop volume (dB). |
| `ThunderVolume` | Controls thunder hit/crack volume (dB). |
| `AmbientNightVolume` | Controls nighttime ambient sounds (dB). |

The system sets these from -80 dB (silent) to 0 dB (full) based on current rain intensity, wind speed, lightning events, and time of day. Crossfades are smoothed at 2 dB/s.

> **Note:** The parameter names are configurable on the `AudioManager` component under the **Weather Audio (EPIC 17.8)** header. Make sure the strings there match the exposed parameter names exactly.

If no AudioMixer is found in the scene at runtime, the weather audio system logs a warning and disables itself. The rest of the weather system continues to function.

---

## 4. Shader Integration

The system sets the following global shader properties every frame on clients. Any shader can read them without additional setup.

| Shader Property | Type | Range | Description |
|----------------|------|-------|-------------|
| `_TimeOfDay` | float | 0-24 | Current hour. |
| `_NormalizedTime` | float | 0-1 | TimeOfDay / 24. |
| `_RainIntensity` | float | 0-1 | Current rain intensity. |
| `_SnowIntensity` | float | 0-1 | Current snow intensity. |
| `_FogDensity` | float | 0-1 | Current fog density. |
| `_WindDirectionX` | float | -1 to 1 | Wind vector X. |
| `_WindDirectionY` | float | -1 to 1 | Wind vector Y. |
| `_WindSpeed` | float | 0-30 | Wind magnitude (m/s). |
| `_Temperature` | float | -30 to 50 | Current temperature (Celsius). |
| `_WeatherTransition` | float | 0-1 | Transition progress between weather types. |

An HLSL include file is available at `Assets/Shaders/Include/WeatherGlobals.hlsl`. Add it to your custom shaders:

```hlsl
#include "Assets/Shaders/Include/WeatherGlobals.hlsl"
```

The skybox system also drives `_StarVisibility`, `_CloudDensity`, and `_SunDirection` on the active skybox material (if those properties exist on it).

---

## 5. Weather Workstation (Editor Tooling)

Open via **DIG → Weather Workstation** from the top menu bar. The window has five tabbed modules on the left sidebar.

### Time Controls (Play Mode Only)

Manipulate the world clock in real-time:
- **Time of Day slider**: Drag to jump to any hour.
- **Time Scale slider**: 0x (frozen) to 10x (fast-forward).
- **Paused toggle**: Halt the clock without changing TimeScale.
- **Season dropdown**: Force-switch the current season.
- **Quick Jump buttons**: Dawn (6:00), Noon (12:00), Dusk (18:00), Midnight (0:00).

### Weather Controls (Play Mode Only)

Override weather state live:
- **Force Weather dropdown**: Instantly switch weather type.
- **Intensity sliders**: Manually set Rain, Snow, Fog, Wind, Temperature values.
- **Trigger Lightning button**: Fire an immediate lightning flash (even outside Thunderstorm).

### Transition Graph

Visualize transition probability weights from your `WeatherConfigSO`:
- Select a **Season** to see the 10x10 transition matrix.
- Color-coded cells: Green = high probability (>0.3), Yellow = medium (>0.1), Orange = low (>0), Dash = no transition.
- Drag-assign any `WeatherConfigSO` directly, or it auto-loads from `Resources/WeatherConfig`.

### Lighting Preview

Preview day-night lighting without entering Play mode:
- Scrub the **Preview Time** slider to see sun color, ambient color, sun intensity, and star visibility values.
- Uses your `DayNightConfigSO` gradients and curves.
- Click **Apply Preview to Scene** to temporarily set the directional light and ambient to match the preview time (edit-mode only, non-destructive).

### Gameplay Inspector (Play Mode Only)

Inspect live gameplay modifiers:
- **Active Weather State**: Current/next weather, intensities, wind, temperature, surface wetness.
- **Player Modifiers**: Lists all entities with `WeatherVisionModifier` or `WeatherMovementModifier` and their current multiplier values.
- **Weather Zone Overrides**: Shows which players have active zone overrides and blend weights.
- **Gameplay Params Table**: Full BlobAsset data dump showing vision, speed, friction, noise, and projectile multipliers for all 10 weather types.

---

## 6. Scene Requirements

The following scene objects are expected by the client-side presentation systems:

| Object | System | Notes |
|--------|--------|-------|
| Directional Light | WeatherLightingSystem, WeatherVFXSystem | The first `Light` component found in the scene is used. Rotation, color, and intensity are driven automatically. |
| Camera (tagged MainCamera) | WeatherVFXSystem | Particle systems follow `Camera.main`. |
| AudioSource with an AudioMixer group | WeatherAudioSystem | Any `AudioSource` whose output group has an `AudioMixer` assigned. The mixer is discovered from this. |
| Skybox material on RenderSettings | WeatherSkyboxSystem | Properties are driven if the material has `_StarVisibility`, `_CloudDensity`, or `_SunDirection`. |

If any of these are missing, the corresponding presentation system gracefully skips or disables itself. The server-side gameplay systems (time, weather transitions, gameplay modifiers) are unaffected.

---

## 7. Weather Types Reference

| Value | WeatherType | Default Visual | Default Gameplay Impact |
|-------|-------------|----------------|------------------------|
| 0 | Clear | No precipitation, low wind | Full vision, full speed |
| 1 | PartlyCloudy | Light clouds | 95% vision |
| 2 | Cloudy | Medium clouds, light fog | 90% vision |
| 3 | LightRain | Light rain particles, fog | 80% vision, 95% speed, 85% friction |
| 4 | HeavyRain | Dense rain, fog | 60% vision, 90% speed, 70% friction |
| 5 | Thunderstorm | Heavy rain, lightning, wind | 50% vision, 85% speed, 65% friction |
| 6 | LightSnow | Light snow particles | 80% vision, 85% speed, 80% friction |
| 7 | HeavySnow | Dense snow, fog, wind | 50% vision, 70% speed, 60% friction |
| 8 | Fog | Dense fog, low wind | 30% vision, 95% speed |
| 9 | Sandstorm | Sand particles, high wind | 40% vision, 80% speed, 85% friction |

All values are designer-configurable via the `WeatherConfigSO` Gameplay Modifiers list.

---

## 8. Multiplayer Considerations

- **Server-authoritative**: `WorldTimeState` and `WeatherState` are ghost-replicated singletons (`GhostPrefabType.All`). Clients never write to them.
- **Deterministic weather**: Given the same `RandomSeed`, weather sequences are identical. Reconnecting clients sync instantly via ghost snapshot.
- **Zero player archetype impact**: All weather state lives on standalone singleton entities. The only per-player components are the small modifier structs (4 bytes each), added via the `WeatherGameplayModifierAuthoring`.
- **Weather zones are client-only**: `WeatherZoneSystem` runs only on client/local simulation. The server does not process zone overrides; they are purely visual.

---

## 9. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| No day-night cycle, lighting is static | Missing `WeatherBootstrap` in subscene | Add `Weather Bootstrap` authoring to a subscene GameObject |
| Weather never changes from default | `WeatherConfigSO` has no transition probabilities | Add entries under Transition Probabilities in the Weather Config |
| No rain/snow/sand particles appear | Normal on first setup -- particles use runtime-generated systems | Verify `Camera.main` exists in the scene |
| Audio mixer warnings in console | Exposed parameter names don't match | Ensure `RainVolume`, `WindVolume`, `ThunderVolume`, `AmbientNightVolume` are exposed on the mixer with those exact names |
| AI vision range not affected by weather | Player/AI prefab missing modifier component | Add `Weather Gameplay Modifier` authoring to the prefab |
| Weather Workstation shows "No ECS World available" | Not in Play Mode, or no server world | Enter Play Mode; ensure NetCode server world is running |
| Skybox not changing | No skybox materials assigned on DayNightConfig | Assign Dawn/Day/Dusk/Night materials, or ensure your skybox material has `_StarVisibility` / `_CloudDensity` properties |
| Surface not slippery during rain | `SurfaceSlipSystem` can't find `WeatherWetness` singleton | Verify `Weather Bootstrap` exists and enable slip physics in `SurfaceGameplayToggles` |
