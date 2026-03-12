# EPIC 17.8: Weather & Day-Night Cycle System

**Status:** PLANNED
**Priority:** Medium (World Atmosphere & Gameplay Modifiers)
**Dependencies:**
- `VisionSettings` IComponentData (existing -- `Assets/Scripts/Vision/Components/VisionSettings.cs`, detection range modifiers)
- `DetectionSystem` (existing -- `Assets/Scripts/Vision/Systems/DetectionSystem.cs`, reads VisionSettings for AI sight)
- `SurfaceMovementModifier` IComponentData (existing -- `Assets/Scripts/Surface/Components/SurfaceMovementModifier.cs`, movement speed modifiers)
- `SurfaceSlipSystem` (existing -- `Assets/Scripts/Surface/Systems/SurfaceSlipSystem.cs`, friction/slip modifiers)
- `SurfaceGameplayConfig` ScriptableObject (existing -- `Assets/Scripts/Surface/Config/SurfaceGameplayConfig.cs`, surface gameplay tuning)
- `SurfaceGameplayConfigSystem` (existing -- `Assets/Scripts/Surface/Systems/SurfaceGameplayConfigSystem.cs`, BlobAsset bootstrap pattern)
- `GroundSurfaceState` IComponentData (existing -- `Assets/Scripts/Surface/Components/GroundSurfaceState.cs`, current surface under player)
- `BarkCollectionSO` (existing -- `Assets/Scripts/Dialogue/Definitions/BarkCollectionSO.cs`, bark definitions)
- `BarkCategory` enum (existing -- `Assets/Scripts/Dialogue/Definitions/DialogueEnums.cs`, Alert category for weather barks)
- `AudioManager` (existing -- `Assets/Scripts/Audio/AudioManager.cs`, MasterMixer with exposed parameters)
- `VFXRequest` / `VFXExecutionSystem` (existing -- `Assets/Scripts/VFX/`, EPIC 16.7, particle VFX pipeline)
- `VFXCategory` enum (existing -- `Assets/Scripts/VFX/`, Environment category)
- URP custom shaders (existing -- `Assets/Shaders/`, URP pipeline)
- `ItemRegistryBootstrapSystem` (existing -- bootstrap singleton pattern reference)
- `SurfaceGameplayConfigSystem` (existing -- BlobAsset bootstrap pattern reference: loads SO, builds BlobAsset, creates singleton, self-disables)

**Feature:** A server-authoritative weather and day-night cycle system where the server owns a deterministic world clock and weather state machine, ghosts them to all clients, and clients drive local-only lighting, shader properties, audio, and VFX from the replicated state. Weather types include Clear, PartlyCloudy, Cloudy, LightRain, HeavyRain, Thunderstorm, LightSnow, HeavySnow, Fog, and Sandstorm with weighted transition probabilities per season. Gameplay hooks modify AI vision range, surface friction, and movement speed. All state lives in singletons -- zero player archetype impact.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `VisionSettings` (SensorSpreadFrames, BaseRange, etc.) | `VisionSettings.cs` | Fully implemented | No external modifier input -- always uses BaseRange |
| `DetectionSystem` (AI sight raycasts) | `DetectionSystem.cs` | Fully implemented | Reads VisionSettings.BaseRange, no weather scaling |
| `SurfaceMovementModifier` (speed modifiers) | `SurfaceMovementModifier.cs` | Fully implemented | Surface-driven only, no weather input |
| `SurfaceSlipSystem` (friction/slip) | `SurfaceSlipSystem.cs` | Fully implemented | Reads SurfaceGameplayBlob friction values, no wet modifier |
| `GroundSurfaceState` (current surface) | `GroundSurfaceState.cs` | Fully implemented | Surface ID under player, no wetness flag |
| `BarkCategory.Alert` | `DialogueEnums.cs` | Enum value exists | No weather-specific bark triggers |
| `AudioManager.MasterMixer` | `AudioManager.cs` | Fully implemented | Exposed parameters exist but no ambient weather loops |
| `VFXRequest` / `VFXExecutionSystem` | `Assets/Scripts/VFX/` | Fully implemented (EPIC 16.7) | VFXCategory.Environment exists, no weather VFX producers |
| URP shaders | `Assets/Shaders/` | Combat UI + SwarmVAT | No weather global keywords, no rain/snow/fog shaders |
| Directional light | Scene | Single directional light | Static rotation, no time-of-day animation |

### What's Missing

- **No world clock** -- no time-of-day, no day/night, no season progression
- **No weather state machine** -- no transitions, no probabilities, no biome overrides
- **No lighting animation** -- directional light angle/color/intensity are static
- **No skybox blending** -- no dawn/day/dusk/night skybox transitions
- **No global shader keywords** -- no `_TimeOfDay`, `_RainIntensity`, `_SnowIntensity`, `_FogDensity`
- **No weather-driven gameplay modifiers** -- no rain wetness reducing friction, no fog reducing vision
- **No ambient audio loops** -- no rain/wind/thunder audio layers
- **No weather VFX** -- no rain/snow particle systems, no lightning
- **No weather zone volumes** -- no per-biome weather override regions
- **No editor tooling** -- no weather preview, timeline scrubber, or transition graph visualizer

---

## Problem

DIG has a complete surface gameplay system (EPIC 16.10), AI vision system, VFX pipeline (EPIC 16.7), and audio infrastructure, but the world has no passage of time and no dynamic weather. The directional light is static, the skybox is unchanging, and the atmosphere is perpetually identical. This undermines immersion and eliminates entire categories of gameplay variety. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `VisionSettings.BaseRange` on AI entities | No weather-driven range reduction (fog, rain) |
| `SurfaceSlipSystem` with friction modifiers | No wet surface modifier from rain |
| `SurfaceMovementModifier` for speed scaling | No snow/storm movement penalty |
| `VFXCategory.Environment` in VFX pipeline | No weather VFX producers (rain, snow, lightning) |
| `AudioManager.MasterMixer` with exposed params | No ambient rain/wind/thunder audio loops |
| `BarkCategory.Alert` for contextual dialogue | No weather-triggered bark conditions |
| URP rendering pipeline | No time-of-day global shader keywords |
| Single directional light in scene | No sun angle animation, no color gradients |
| Skybox material | No dawn/day/dusk/night blending |
| Ghost-replicated singletons pattern | No world time or weather singletons |

**The gap:** Players experience the same static atmosphere regardless of play duration. Designers cannot create "rain reduces visibility" encounters, "snowstorm slows movement" challenges, or "night ambush" scenarios. There is no environmental variety to break up gameplay monotony.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  DayNightConfigSO         WeatherConfigSO          WeatherZoneConfigSO
  (Day length, sun         (Transition probs,        (Per-biome weather
   gradients, ambient       weather params,            overrides, priority)
   curves, skybox mats)     season modifiers)
           |                      |                         |
           └────── WeatherBootstrapSystem ──────────────────┘
                   (loads from Resources/, builds BlobAssets,
                    creates WorldTimeState + WeatherState singletons,
                    follows SurfaceGameplayConfigSystem pattern)
                              |
                    ECS DATA LAYER (Ghost-Replicated Singletons)
  WorldTimeState (Ghost:All)          WeatherState (Ghost:All)
  (TimeOfDay 0-24, DayCount,          (CurrentWeather, NextWeather,
   Season, TimeScale, IsPaused)         TransitionProgress, Wind,
                                        RainIntensity, SnowIntensity,
                                        FogDensity, LightningTimer,
                                        Temperature)
                              |
                    SYSTEM PIPELINE
                              |
  SERVER/LOCAL (SimulationSystemGroup):
  WorldTimeSystem ─── ticks clock, advances seasons
  WeatherTransitionSystem ─── weather state machine, weighted transitions
  WeatherGameplaySystem ─── vision/movement/surface modifiers
                              |
  CLIENT/LOCAL (PresentationSystemGroup):
  WeatherZoneSystem ─── per-biome override volumes (local player only)
  WeatherLightingSystem ─── sun angle, directional light, ambient color
  WeatherShaderSystem ─── Shader.SetGlobalFloat for all weather keywords
  WeatherSkyboxSystem ─── skybox material property blending
  WeatherAudioSystem ─── rain/wind/thunder ambient loops via AudioManager
  WeatherVFXSystem ─── rain/snow/dust particle systems, lightning bolts
```

### Data Flow (Server Clock Tick -> Client Presentation)

```
Frame N (Server):
  1. WorldTimeSystem: TimeOfDay += (deltaTime / DayLengthSeconds) * 24 * TimeScale
     - If TimeOfDay >= 24: DayCount++, TimeOfDay -= 24
     - Season advances every SeasonLengthDays

  2. WeatherTransitionSystem:
     - If TransitionProgress >= 1.0: CurrentWeather = NextWeather, pick new NextWeather
     - TransitionProgress += deltaTime / TransitionDuration
     - NextWeather chosen by weighted random from WeatherTransitionBlob[CurrentWeather][Season]
     - Update RainIntensity/SnowIntensity/FogDensity/WindSpeed via lerp toward target values
     - LightningTimer: countdown for next lightning strike (Thunderstorm only)

  3. WeatherGameplaySystem:
     - Reads WeatherState.FogDensity -> modifies VisionSettings range multiplier
     - Reads WeatherState.RainIntensity -> modifies surface wetness / friction
     - Reads WeatherState.SnowIntensity -> modifies movement speed

Frame N (Client, same frame via ghost replication):
  4. WeatherZoneSystem: Player inside WeatherZone volume? -> override local weather params
  5. WeatherLightingSystem: Sun euler = f(TimeOfDay), light color = gradient.Evaluate(t)
  6. WeatherShaderSystem: Shader.SetGlobalFloat("_TimeOfDay", ...) etc.
  7. WeatherSkyboxSystem: Lerp skybox properties (dawn/day/dusk/night)
  8. WeatherAudioSystem: Set AudioManager mixer exposed params for rain/wind volume
  9. WeatherVFXSystem: Enable/disable/scale rain/snow particle systems
```

### Server-Authoritative Time Model

The server owns `WorldTimeState` and `WeatherState` as ghost-replicated singletons (`Ghost:All`). Clients receive these via NetCode ghost serialization every tick. Client presentation systems read the replicated values and drive local-only effects (lighting, shaders, audio, VFX). Clients never write to these singletons. Weather transitions are deterministic given the server's random seed -- reconnecting clients instantly sync to the correct state.

---

## ECS Components

### Enums

**File:** `Assets/Scripts/Weather/Components/WeatherEnums.cs`

```csharp
public enum WeatherType : byte
{
    Clear = 0,
    PartlyCloudy = 1,
    Cloudy = 2,
    LightRain = 3,
    HeavyRain = 4,
    Thunderstorm = 5,
    LightSnow = 6,
    HeavySnow = 7,
    Fog = 8,
    Sandstorm = 9
}

public enum Season : byte
{
    Spring = 0,
    Summer = 1,
    Autumn = 2,
    Winter = 3
}

public enum TimeOfDayPeriod : byte
{
    Night = 0,       // 0:00 - 5:00
    Dawn = 1,        // 5:00 - 7:00
    Morning = 2,     // 7:00 - 10:00
    Midday = 3,      // 10:00 - 14:00
    Afternoon = 4,   // 14:00 - 17:00
    Dusk = 5,        // 17:00 - 19:00
    Evening = 6,     // 19:00 - 22:00
    LateNight = 7    // 22:00 - 0:00
}
```

### Singletons (Ghost-Replicated)

**File:** `Assets/Scripts/Weather/Components/WorldTimeState.cs`

```csharp
// 16 bytes -- server-authoritative world clock
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct WorldTimeState : IComponentData
{
    [GhostField(Quantization = 100)] public float TimeOfDay;  // 0.0 - 23.99 (hours)
    [GhostField] public int DayCount;                          // Days elapsed since world start
    [GhostField] public Season Season;                         // Current season (byte)
    [GhostField(Quantization = 100)] public float TimeScale;  // 1.0 = normal, 0 = paused
    [GhostField] public bool IsPaused;                         // Explicit pause flag
    // Padding: 2 bytes (struct alignment)
}
```

| Field | Type | Bytes | GhostField | Notes |
|-------|------|-------|------------|-------|
| TimeOfDay | float | 4 | Quantization=100 | 0.0-23.99, wraps at 24 |
| DayCount | int | 4 | default | Monotonically increasing |
| Season | Season (byte) | 1 | default | Advances every SeasonLengthDays |
| TimeScale | float | 4 | Quantization=100 | 0=frozen, 1=normal, 2=fast-forward |
| IsPaused | bool | 1 | default | Admin/cutscene pause |
| Padding | -- | 2 | -- | Struct alignment |
| **Total** | | **16** | | |

**File:** `Assets/Scripts/Weather/Components/WeatherState.cs`

```csharp
// 40 bytes -- server-authoritative weather state
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct WeatherState : IComponentData
{
    [GhostField] public WeatherType CurrentWeather;              // Active weather type
    [GhostField] public WeatherType NextWeather;                 // Transitioning toward
    [GhostField(Quantization = 1000)] public float TransitionProgress; // 0.0 - 1.0
    [GhostField(Quantization = 100)] public float WindDirectionX;  // Wind dir X
    [GhostField(Quantization = 100)] public float WindDirectionY;  // Wind dir Y
    [GhostField(Quantization = 100)] public float WindSpeed;       // m/s
    [GhostField(Quantization = 1000)] public float RainIntensity;  // 0.0 - 1.0
    [GhostField(Quantization = 1000)] public float SnowIntensity;  // 0.0 - 1.0
    [GhostField(Quantization = 1000)] public float FogDensity;     // 0.0 - 1.0
    [GhostField(Quantization = 100)] public float LightningTimer;  // Seconds until next strike
    [GhostField(Quantization = 10)] public float Temperature;      // Celsius, cosmetic + future gameplay
}
```

| Field | Type | Bytes | GhostField | Notes |
|-------|------|-------|------------|-------|
| CurrentWeather | WeatherType (byte) | 1 | default | Active weather enum |
| NextWeather | WeatherType (byte) | 1 | default | Target weather enum |
| TransitionProgress | float | 4 | Quantization=1000 | 0-1 lerp between current/next |
| WindDirectionX | float | 4 | Quantization=100 | Wind vector X component |
| WindDirectionY | float | 4 | Quantization=100 | Wind vector Y component |
| WindSpeed | float | 4 | Quantization=100 | Wind magnitude in m/s |
| RainIntensity | float | 4 | Quantization=1000 | 0=none, 1=maximum |
| SnowIntensity | float | 4 | Quantization=1000 | 0=none, 1=maximum |
| FogDensity | float | 4 | Quantization=1000 | 0=clear, 1=pea soup |
| LightningTimer | float | 4 | Quantization=100 | Countdown to next strike |
| Temperature | float | 4 | Quantization=10 | Celsius, cosmetic baseline |
| Padding | -- | 2 | -- | Struct alignment to 40 |
| **Total** | | **40** | | |

### Weather Zone (Volume Entities)

**File:** `Assets/Scripts/Weather/Components/WeatherZoneComponents.cs`

```csharp
// 12 bytes -- placed on trigger volume entities in the world
public struct WeatherZone : IComponentData
{
    public byte BiomeType;          // Designer-defined biome index (0-255)
    public WeatherType WeatherOverride; // Forced weather in this zone (255 = no override)
    public byte Priority;           // Higher priority zones win on overlap
    public byte Padding;
    public float Radius;            // Zone radius for distance falloff (0 = use collider only)
}
```

| Field | Type | Bytes | Notes |
|-------|------|-------|-------|
| BiomeType | byte | 1 | Indexes into season/transition probability tables |
| WeatherOverride | WeatherType (byte) | 1 | 255 = use global weather |
| Priority | byte | 1 | Zone stacking resolution |
| Padding | byte | 1 | Alignment |
| Radius | float | 4 | Falloff radius, 0 = collider bounds |
| **Total** | | **8** | |

```csharp
// Tag for weather zone volume entities
public struct WeatherZoneTag : IComponentData { }

// Client-only: effective local weather after zone overrides
public struct LocalWeatherOverride : IComponentData
{
    public bool HasOverride;
    public WeatherType OverrideWeather;
    public float BlendWeight;       // 0 = global weather, 1 = full zone override
    public byte BiomeType;
}
```

### Singleton (Managed, NOT Ghost-Replicated)

**File:** `Assets/Scripts/Weather/Components/WeatherManagerSingleton.cs`

```csharp
// Managed singleton for references that cannot be ghost-replicated
public class WeatherManagerSingleton : IComponentData
{
    public BlobAssetReference<WeatherTransitionBlob> TransitionTable;
    public BlobAssetReference<DayNightBlob> DayNightConfig;
    public BlobAssetReference<WeatherParamsBlob> WeatherParams;
    public uint RandomSeed;         // Deterministic weather seed
    public float TimeSinceLastTransition;
    public float NextTransitionInterval;
    public bool IsInitialized;
}
```

### Weather Gameplay Modifier (On Player Entity, Client-Only)

**File:** `Assets/Scripts/Weather/Components/WeatherGameplayModifier.cs`

```csharp
// Ephemeral modifiers applied by WeatherGameplaySystem, read by existing systems
// NOT ghost-replicated -- server computes and applies directly
public struct WeatherVisionModifier : IComponentData
{
    public float RangeMultiplier;   // 1.0 = full range, 0.5 = halved by fog/rain
}

public struct WeatherMovementModifier : IComponentData
{
    public float SpeedMultiplier;   // 1.0 = full speed, 0.7 = slowed by snow/storm
}
```

---

## BlobAsset Definitions

**File:** `Assets/Scripts/Weather/Data/WeatherBlobs.cs`

```csharp
public struct WeatherTransitionBlob
{
    // TransitionWeights[fromWeather * SeasonCount + season] = BlobArray of 10 floats (one per WeatherType)
    public BlobArray<float> TransitionWeights;  // Flattened [10 * 4] x 10 = 400 floats
    public int WeatherTypeCount;   // 10
    public int SeasonCount;        // 4
}

public struct DayNightBlob
{
    public float DayLengthSeconds;           // Real seconds per game day (default 1200 = 20 min)
    public int SeasonLengthDays;             // Game days per season (default 7)
    public float SunriseHour;                // 6.0
    public float SunsetHour;                 // 18.0
    public float SunPitchMax;                // Maximum sun elevation (90 = directly overhead)
    public float MoonPitchMax;               // Moon elevation at midnight
    public float NightAmbientIntensity;      // Ambient light at midnight (0.05)
    public float DayAmbientIntensity;        // Ambient light at noon (1.0)

    // Color gradient keyframes (sampled by time-of-day 0-1)
    public BlobArray<ColorKeyframe> SunColorGradient;       // 8 keyframes
    public BlobArray<ColorKeyframe> AmbientColorGradient;   // 8 keyframes
    public BlobArray<float> SunIntensityCurve;              // 24 samples (one per hour)
}

public struct ColorKeyframe
{
    public float Time;    // 0.0 - 1.0 (fraction of day)
    public float R, G, B; // Linear color
}

public struct WeatherParamsBlob
{
    // Per-WeatherType target values
    public BlobArray<WeatherTargetParams> TargetParams; // 10 entries

    // Gameplay modifiers
    public BlobArray<WeatherGameplayParams> GameplayParams; // 10 entries
}

public struct WeatherTargetParams
{
    public float TargetRainIntensity;
    public float TargetSnowIntensity;
    public float TargetFogDensity;
    public float TargetWindSpeed;
    public float TargetTemperatureOffset;  // Added to season base temperature
    public float TransitionDurationMin;    // Min seconds to transition into this weather
    public float TransitionDurationMax;    // Max seconds
    public float MinDuration;              // Min seconds this weather lasts before next transition
    public float MaxDuration;              // Max seconds
    public float LightningIntervalMin;     // 0 = no lightning
    public float LightningIntervalMax;
}

public struct WeatherGameplayParams
{
    public float VisionRangeMultiplier;    // 1.0 = no change, 0.3 = severe fog
    public float MovementSpeedMultiplier;  // 1.0 = no change, 0.7 = heavy snow
    public float SurfaceFrictionMultiplier;// 1.0 = no change, 0.6 = wet/icy
    public float NoiseMultiplier;          // 1.0 = no change, 0.5 = rain masks footsteps
    public float ProjectileSpeedMultiplier;// 1.0 = no change, 0.9 = strong wind
}
```

---

## ScriptableObjects

### DayNightConfigSO

**File:** `Assets/Scripts/Weather/Config/DayNightConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Weather/Day-Night Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| DayLengthSeconds | float [Min(60)] | 1200 | Real seconds per game day (1200 = 20 minutes) |
| SeasonLengthDays | int [Min(1)] | 7 | Game days per season cycle |
| SunriseHour | float [Range(0,12)] | 6.0 | Hour when sun rises above horizon |
| SunsetHour | float [Range(12,24)] | 18.0 | Hour when sun sets below horizon |
| SunPitchMax | float [Range(30,90)] | 75.0 | Maximum sun elevation at solar noon |
| MoonPitchMax | float [Range(10,60)] | 40.0 | Moon elevation at midnight |
| NightAmbientIntensity | float [Range(0,0.5)] | 0.05 | Ambient light intensity at night |
| DayAmbientIntensity | float [Range(0.5,2)] | 1.0 | Ambient light intensity at noon |
| SunColorGradient | Gradient | warm sunrise -> white noon -> warm sunset | Directional light color over day |
| AmbientColorGradient | Gradient | dark blue night -> warm day -> orange dusk | Ambient light color |
| SunIntensityCurve | AnimationCurve | 0 at night, 1 at noon | Directional light intensity |
| SkyboxDawnMaterial | Material | -- | Skybox material for dawn period |
| SkyboxDayMaterial | Material | -- | Skybox material for day period |
| SkyboxDuskMaterial | Material | -- | Skybox material for dusk period |
| SkyboxNightMaterial | Material | -- | Skybox material for night period |
| StarVisibilityCurve | AnimationCurve | 1 at night, 0 during day | Star layer opacity |

### WeatherConfigSO

**File:** `Assets/Scripts/Weather/Config/WeatherConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Weather/Weather Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| WeatherChangeIntervalMin | float | 180 | Min seconds between weather transitions |
| WeatherChangeIntervalMax | float | 600 | Max seconds between weather transitions |
| DefaultWeather | WeatherType | Clear | Starting weather on server boot |
| DefaultSeason | Season | Summer | Starting season |
| RandomSeed | uint | 0 | 0 = time-based seed |
| BaseTemperature | float[] (4) | [15, 25, 12, -2] | Celsius per season [Spr,Sum,Aut,Win] |
| TransitionProbabilities | WeatherTransitionEntry[] | see below | Per-weather per-season transition weights |
| WeatherTargetParams | WeatherTargetParamsEntry[] (10) | see below | Per-WeatherType target intensities |
| GameplayModifiers | WeatherGameplayEntry[] (10) | see below | Per-WeatherType gameplay multipliers |

**WeatherTransitionEntry (Serializable):**

| Field | Type | Purpose |
|-------|------|---------|
| FromWeather | WeatherType | Source weather |
| Season | Season | Season context |
| Weights | float[10] | Probability weight per target WeatherType |

Default transition weight examples (Summer):
- Clear -> PartlyCloudy: 0.4, Cloudy: 0.1, Clear: 0.5
- Cloudy -> LightRain: 0.3, Clear: 0.2, PartlyCloudy: 0.3, Fog: 0.1, Cloudy: 0.1
- HeavyRain -> Thunderstorm: 0.3, LightRain: 0.4, Cloudy: 0.2, HeavyRain: 0.1
- Thunderstorm -> HeavyRain: 0.5, Cloudy: 0.3, Thunderstorm: 0.2

Default transition weight examples (Winter):
- Clear -> PartlyCloudy: 0.3, LightSnow: 0.2, Fog: 0.1, Clear: 0.4
- Cloudy -> LightSnow: 0.4, HeavySnow: 0.1, Fog: 0.2, Cloudy: 0.2, Clear: 0.1
- HeavySnow -> LightSnow: 0.4, Cloudy: 0.3, HeavySnow: 0.3

**WeatherTargetParamsEntry (Serializable):**

| Field | Type | Default (HeavyRain example) | Purpose |
|-------|------|---------|---------|
| Weather | WeatherType | HeavyRain | Target weather |
| RainIntensity | float [0-1] | 0.9 | Target rain intensity |
| SnowIntensity | float [0-1] | 0.0 | Target snow intensity |
| FogDensity | float [0-1] | 0.2 | Target fog density |
| WindSpeed | float [0-30] | 12.0 | Target wind speed (m/s) |
| TemperatureOffset | float | -3.0 | Temperature offset from season base |
| TransitionDurationMin | float | 30 | Min seconds to transition in |
| TransitionDurationMax | float | 90 | Max seconds to transition in |
| MinDuration | float | 120 | Min seconds this weather lasts |
| MaxDuration | float | 480 | Max seconds this weather lasts |
| LightningIntervalMin | float | 0 | Min seconds between lightning (0 = none) |
| LightningIntervalMax | float | 0 | Max seconds between lightning |

**WeatherGameplayEntry (Serializable):**

| Field | Type | Default (Fog example) | Purpose |
|-------|------|---------|---------|
| Weather | WeatherType | Fog | Weather type |
| VisionRangeMultiplier | float | 0.3 | AI detection range multiplier |
| MovementSpeedMultiplier | float | 0.95 | Player/NPC movement speed |
| SurfaceFrictionMultiplier | float | 1.0 | Surface friction modifier |
| NoiseMultiplier | float | 0.8 | Footstep noise modifier |
| ProjectileSpeedMultiplier | float | 1.0 | Projectile speed modifier |

### WeatherZoneConfigSO

**File:** `Assets/Scripts/Weather/Config/WeatherZoneConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Weather/Weather Zone Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| BiomeType | byte | 0 | Biome index for transition table lookup |
| WeatherOverride | WeatherType | 255 | Forced weather (255 = follow global) |
| Priority | byte | 0 | Overlap resolution (higher wins) |
| BlendRadius | float | 20 | Transition blend distance at zone edges |

---

## System Execution Order

```
InitializationSystemGroup (Server|Client|Local):
  WeatherBootstrapSystem                  -- loads SOs from Resources/, builds BlobAssets,
                                             creates WorldTimeState + WeatherState singletons,
                                             creates WeatherManagerSingleton (managed)
                                             (runs once, self-disables)

SimulationSystemGroup (Server|Local):
  WorldTimeSystem                         -- ticks TimeOfDay, advances DayCount/Season
  WeatherTransitionSystem                 -- weather state machine, weighted random transitions,
                                             lerps intensity params toward targets
  WeatherGameplaySystem                   -- reads WeatherState, writes vision/movement/surface
                                             modifiers to player entities

SimulationSystemGroup (Client|Local):
  WeatherZoneSystem                       -- checks local player overlap with WeatherZone volumes,
                                             writes LocalWeatherOverride on local player

PresentationSystemGroup (Client|Local):
  WeatherLightingSystem                   -- sun angle from TimeOfDay, directional light
                                             color/intensity from gradient, ambient color lerp
  WeatherSkyboxSystem                     -- skybox material property blending (dawn/day/dusk/night),
                                             star visibility
  WeatherShaderSystem                     -- Shader.SetGlobalFloat for all weather keywords:
                                             _TimeOfDay, _RainIntensity, _SnowIntensity,
                                             _FogDensity, _WindDirection, _WindSpeed
  WeatherAudioSystem                      -- AudioManager exposed params for rain/wind/thunder volume,
                                             crossfade between ambient loops
  WeatherVFXSystem                        -- enable/disable/scale rain/snow/dust particle systems,
                                             lightning bolt VFX via VFXRequest pipeline
```

### Critical System Ordering Chain

```
SERVER TIMELINE:
WeatherBootstrapSystem (once)
    |
WorldTimeSystem [SimulationSystemGroup, OrderFirst]
    |
WeatherTransitionSystem [UpdateAfter(typeof(WorldTimeSystem))]
    |
WeatherGameplaySystem [UpdateAfter(typeof(WeatherTransitionSystem))]
    |
--- Ghost replication to clients ---

CLIENT TIMELINE (same frame after ghost sync):
WeatherZoneSystem [SimulationSystemGroup]
    |
WeatherLightingSystem [PresentationSystemGroup, OrderFirst]
    |
WeatherSkyboxSystem [UpdateAfter(typeof(WeatherLightingSystem))]
    |
WeatherShaderSystem [UpdateAfter(typeof(WeatherSkyboxSystem))]
    |
WeatherAudioSystem [UpdateAfter(typeof(WeatherShaderSystem))]
    |
WeatherVFXSystem [UpdateAfter(typeof(WeatherAudioSystem))]
```

---

## ECS Systems

### WeatherBootstrapSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherBootstrapSystem.cs`

- Managed SystemBase, `InitializationSystemGroup`, `Server|Client|Local`
- Runs once, self-disables after init
- Loads `DayNightConfigSO` from `Resources/DayNightConfig`
- Loads `WeatherConfigSO` from `Resources/WeatherConfig`
- Builds BlobAssets via `BlobBuilder`:
  - `WeatherTransitionBlob` from transition probability arrays
  - `DayNightBlob` from gradient/curve samples
  - `WeatherParamsBlob` from target params + gameplay modifiers
- Creates `WorldTimeState` singleton entity with initial values (TimeOfDay=8.0, DayCount=0, Season=DefaultSeason)
- Creates `WeatherState` singleton entity with initial values (CurrentWeather=DefaultWeather)
- Creates `WeatherManagerSingleton` managed singleton with BlobAsset references + random seed
- Follows `SurfaceGameplayConfigSystem` pattern exactly

### WorldTimeSystem

**File:** `Assets/Scripts/Weather/Systems/WorldTimeSystem.cs`

- `ISystem`, `[BurstCompile]`, `SimulationSystemGroup`, `Server|Local`, `[UpdateBefore(typeof(WeatherTransitionSystem))]`
- Reads `DayNightBlob` via `WeatherManagerSingleton` (managed access in OnCreate, cache BlobRef)
- Each frame:
  ```
  if (!state.IsPaused)
      state.TimeOfDay += (deltaTime / DayLengthSeconds) * 24.0f * state.TimeScale;
  if (state.TimeOfDay >= 24.0f)
      state.DayCount++;
      state.TimeOfDay -= 24.0f;
  if (state.DayCount % SeasonLengthDays == 0 && dayJustChanged)
      state.Season = (Season)(((int)state.Season + 1) % 4);
  ```
- **Note:** Not Burst-compiled at top level because it accesses managed `WeatherManagerSingleton`. The time arithmetic itself is trivial (3 adds, 1 modulo). If perf matters, cache BlobRef in unmanaged singleton.

### WeatherTransitionSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherTransitionSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`, `[UpdateAfter(typeof(WorldTimeSystem))]`
- Reads `WeatherState`, `WeatherManagerSingleton`
- Each frame:
  1. Accumulate `TimeSinceLastTransition += deltaTime`
  2. If transitioning (`TransitionProgress < 1.0`):
     - `TransitionProgress += deltaTime / currentTransitionDuration`
     - Lerp `RainIntensity/SnowIntensity/FogDensity/WindSpeed` toward NextWeather target values
     - Lerp `Temperature` toward season base + weather offset
  3. If transition complete (`TransitionProgress >= 1.0`):
     - `CurrentWeather = NextWeather`
     - `TransitionProgress = 1.0`
  4. If `TimeSinceLastTransition >= NextTransitionInterval`:
     - Pick `NextWeather` from `WeatherTransitionBlob[CurrentWeather][Season]` weights
     - Weighted random: sum weights, random float, scan prefix sums
     - `TransitionProgress = 0.0`
     - `TransitionDuration = Random.Range(targetParams.TransitionDurationMin, Max)`
     - `NextTransitionInterval = Random.Range(WeatherChangeIntervalMin, Max)`
     - Reset `TimeSinceLastTransition = 0`
  5. If `CurrentWeather == Thunderstorm`:
     - `LightningTimer -= deltaTime`
     - If `LightningTimer <= 0`: reset timer, mark lightning event (read by VFX system)

### WeatherGameplaySystem

**File:** `Assets/Scripts/Weather/Systems/WeatherGameplaySystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Server|Local`, `[UpdateAfter(typeof(WeatherTransitionSystem))]`
- Reads `WeatherState`, `WeatherParamsBlob`
- Queries all entities with `VisionSettings`:
  - Computes effective weather (lerp between CurrentWeather and NextWeather gameplay params)
  - `VisionRangeMultiplier = lerp(currentGameplay.VisionRange, nextGameplay.VisionRange, TransitionProgress)`
  - Writes `WeatherVisionModifier.RangeMultiplier` on entities that have it
- Queries all entities with `SurfaceMovementModifier` + `PlayerTag`:
  - `SpeedMultiplier = effectiveGameplay.MovementSpeedMultiplier`
  - Writes `WeatherMovementModifier.SpeedMultiplier`
- Surface friction: writes a global `WeatherWetness` singleton float for `SurfaceSlipSystem` to read
  - `Wetness = max(RainIntensity * 0.8, SnowIntensity * 0.3)` -- rain makes surfaces more slippery than snow
- **Integration with existing systems:**
  - `DetectionSystem` modified to read `WeatherVisionModifier.RangeMultiplier` and scale effective range
  - `SurfaceSlipSystem` modified to read `WeatherWetness` singleton and scale friction

### WeatherZoneSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherZoneSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `Client|Local`
- Queries `WeatherZone` + `LocalToWorld` entities, and local player entity
- For each zone: distance check from player to zone center
  - If inside: compute blend weight from distance and BlendRadius
  - Highest-priority overlapping zone wins
- Writes `LocalWeatherOverride` on local player entity
- Client presentation systems read `LocalWeatherOverride` to determine effective local weather
  - If `HasOverride && BlendWeight > 0`: blend between global WeatherState and zone override

### WeatherLightingSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherLightingSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`, `[UpdateBefore(typeof(WeatherShaderSystem))]`
- Reads `WorldTimeState`, `WeatherState`, `DayNightBlob`
- Calculates sun position:
  ```
  float t = TimeOfDay / 24.0;  // 0-1 normalized
  float sunAngle = CalculateSunAngle(TimeOfDay, SunriseHour, SunsetHour, SunPitchMax);
  // sunAngle: -SunPitchMax at midnight, 0 at sunrise/sunset, +SunPitchMax at noon
  ```
- Sets directional light via `Object.FindAnyObjectByType<Light>()` (cached on first call):
  - `light.transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0)`
  - `light.color = SunColorGradient.Evaluate(t)` (sampled from BlobAsset)
  - `light.intensity = SunIntensityCurve.Evaluate(t) * weatherDimFactor`
  - `weatherDimFactor = 1.0 - (CloudCoverage * 0.4 + RainIntensity * 0.3 + SnowIntensity * 0.2)`
- Sets ambient light:
  - `RenderSettings.ambientLight = AmbientColorGradient.Evaluate(t)`
  - `RenderSettings.ambientIntensity = lerp(NightAmbientIntensity, DayAmbientIntensity, sunFactor)`

### WeatherSkyboxSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherSkyboxSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`, `[UpdateAfter(typeof(WeatherLightingSystem))]`
- Reads `WorldTimeState`, `DayNightBlob`
- Determines current `TimeOfDayPeriod` from `TimeOfDay`
- Blends skybox material properties between period materials:
  - Dawn (5-7), Day (7-17), Dusk (17-19), Night (19-5)
  - At transition boundaries: `RenderSettings.skybox.Lerp(fromMat, toMat, blendT)`
  - Alternatively: single skybox material with `_SunDirection`, `_AtmosphereColor`, `_HorizonColor` properties driven by time
- Sets star layer: `skyboxMat.SetFloat("_StarVisibility", StarVisibilityCurve.Evaluate(t))`
- Weather overlay: `skyboxMat.SetFloat("_CloudDensity", cloudFactor)`

### WeatherShaderSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherShaderSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`, `[UpdateAfter(typeof(WeatherSkyboxSystem))]`
- Reads `WorldTimeState`, `WeatherState`, `LocalWeatherOverride` (if present)
- Sets global shader properties every frame:
  ```csharp
  float effectiveRain = hasOverride ? lerp(ws.RainIntensity, overrideRain, blendWeight) : ws.RainIntensity;
  // Same for snow, fog, wind

  Shader.SetGlobalFloat("_TimeOfDay", timeState.TimeOfDay);
  Shader.SetGlobalFloat("_NormalizedTime", timeState.TimeOfDay / 24.0f);
  Shader.SetGlobalFloat("_RainIntensity", effectiveRain);
  Shader.SetGlobalFloat("_SnowIntensity", effectiveSnow);
  Shader.SetGlobalFloat("_FogDensity", effectiveFog);
  Shader.SetGlobalFloat("_WindDirectionX", ws.WindDirectionX);
  Shader.SetGlobalFloat("_WindDirectionY", ws.WindDirectionY);
  Shader.SetGlobalFloat("_WindSpeed", ws.WindSpeed);
  Shader.SetGlobalFloat("_Temperature", ws.Temperature);
  Shader.SetGlobalFloat("_WeatherTransition", ws.TransitionProgress);
  ```
- Existing and future shaders can read these keywords without any system dependency
- **No structural changes to existing shaders** -- keywords are additive, default 0.0 means "no effect"

### WeatherAudioSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherAudioSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`, `[UpdateAfter(typeof(WeatherShaderSystem))]`
- Reads `WeatherState`, `LocalWeatherOverride`
- Uses `AudioManager.MasterMixer` exposed parameters:
  - `"RainVolume"`: 0 dB at RainIntensity=1.0, -80 dB at 0.0 (logarithmic curve)
  - `"WindVolume"`: scaled by WindSpeed / MaxWindSpeed
  - `"ThunderVolume"`: spike on lightning event, fast decay
  - `"AmbientNightVolume"`: scaled by (1 - sunFactor) for cricket/owl loops
- Audio crossfade: lerps dB values over 2 seconds to avoid pops
- Lightning audio: when `LightningTimer` resets (detected by value jump), play thunder oneshot with random delay (distance simulation)

### WeatherVFXSystem

**File:** `Assets/Scripts/Weather/Systems/WeatherVFXSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `Client|Local`, `[UpdateAfter(typeof(WeatherAudioSystem))]`
- Reads `WeatherState`, camera position
- Rain particles:
  - Spawns/manages a persistent rain particle system attached to camera
  - `particleSystem.emission.rateOverTime = RainIntensity * MaxRainParticles`
  - Wind influence: `particleSystem.velocityOverLifetime.x = WindDirectionX * WindSpeed * 0.5`
  - Disable when `RainIntensity < 0.01`
- Snow particles:
  - Same pattern as rain, slower fall speed, lateral drift
  - `particleSystem.emission.rateOverTime = SnowIntensity * MaxSnowParticles`
- Sandstorm particles:
  - Horizontal particle sheet, scaled by `CurrentWeather == Sandstorm`
- Lightning bolts:
  - When `LightningTimer` resets: create `VFXRequest` (VFXCategory.Environment)
  - Random position on skybox dome, brief flash
  - Screen flash via post-process volume exposure spike (0.1 second)
- Fog:
  - `RenderSettings.fog = FogDensity > 0.01`
  - `RenderSettings.fogDensity = FogDensity * MaxFogDensity`
  - `RenderSettings.fogColor` blended with ambient color

---

## Authoring

### WeatherBootstrapAuthoring

**File:** `Assets/Scripts/Weather/Authoring/WeatherBootstrapAuthoring.cs`

```
[AddComponentMenu("DIG/Weather/Weather Bootstrap")]
```

- Place on a GameObject in the subscene (not on player prefab)
- Baker creates singleton entity with `WorldTimeState` + `WeatherState` + ghost component
- Fields: reference to `DayNightConfigSO` and `WeatherConfigSO` (for baker to embed initial values)
- Baker sets `WorldTimeState.TimeOfDay = config.StartTimeOfDay` (default 8.0 = morning)
- Baker sets `WeatherState.CurrentWeather = config.DefaultWeather`

### WeatherZoneAuthoring

**File:** `Assets/Scripts/Weather/Authoring/WeatherZoneAuthoring.cs`

```
[AddComponentMenu("DIG/Weather/Weather Zone")]
[RequireComponent(typeof(PhysicsShapeAuthoring))]
```

- Place on trigger volume GameObjects in the subscene
- Fields: `WeatherZoneConfigSO` reference (or inline BiomeType, WeatherOverride, Priority, BlendRadius)
- Baker adds `WeatherZone` + `WeatherZoneTag` to entity

### WeatherGameplayModifierAuthoring

**File:** `Assets/Scripts/Weather/Authoring/WeatherGameplayModifierAuthoring.cs`

```
[AddComponentMenu("DIG/Weather/Weather Gameplay Modifier")]
```

- Place on player prefab alongside existing authoring components
- Baker adds `WeatherVisionModifier` (default RangeMultiplier=1.0) + `WeatherMovementModifier` (default SpeedMultiplier=1.0)
- Baked as lightweight defaults -- systems write actual values at runtime
- **Archetype impact:** 8 bytes total (2 floats). Acceptable within headroom.

---

## Global Shader Keywords

**File:** `Assets/Shaders/Include/WeatherGlobals.hlsl`

```hlsl
// Include in any shader that needs weather data
// All default to 0.0 when WeatherShaderSystem is not running

float _TimeOfDay;           // 0.0 - 23.99
float _NormalizedTime;      // 0.0 - 1.0 (TimeOfDay / 24)
float _RainIntensity;       // 0.0 - 1.0
float _SnowIntensity;       // 0.0 - 1.0
float _FogDensity;          // 0.0 - 1.0
float _WindDirectionX;      // -1.0 to 1.0
float _WindDirectionY;      // -1.0 to 1.0
float _WindSpeed;           // 0.0 - 30.0 m/s
float _Temperature;         // Celsius
float _WeatherTransition;   // 0.0 - 1.0 (current transition progress)
```

Usage in existing/new shaders:
- Terrain: `_RainIntensity` -> wet specular boost, puddle height threshold
- Vegetation: `_WindDirectionX/Y + _WindSpeed` -> vertex displacement amplitude
- Particles: `_WindDirectionX/Y` -> drift direction
- Post-process: `_FogDensity` -> volumetric fog parameter

---

## Editor Tooling

### WeatherWorkstationWindow

**File:** `Assets/Editor/WeatherWorkstation/WeatherWorkstationWindow.cs`
- Menu: `DIG/Weather Workstation`
- Sidebar + `IWeatherWorkstationModule` pattern (same as ProgressionWorkstation)

### Modules (5 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| Time Controls | `Modules/TimeControlModule.cs` | Play-mode: TimeOfDay slider (0-24), TimeScale slider (0-10x), Pause toggle, Season dropdown, "Skip to Dawn/Noon/Dusk/Midnight" buttons. Displays current TimeOfDayPeriod, DayCount, Season. |
| Weather Controls | `Modules/WeatherControlModule.cs` | Play-mode: Force any WeatherType dropdown, RainIntensity/SnowIntensity/FogDensity/WindSpeed sliders, "Trigger Lightning" button, Temperature override. Overrides server weather state temporarily. |
| Transition Graph | `Modules/TransitionGraphModule.cs` | Visualizes WeatherConfigSO transition probabilities as a directed graph. Nodes = WeatherTypes, edges = transition weights (thickness proportional). Season selector. Click edge to edit weight. |
| Lighting Preview | `Modules/LightingPreviewModule.cs` | Timeline scrubber showing sun color/intensity gradient preview. Skybox thumbnail strip across 24 hours. Ambient color bar. Edit-mode preview (temporarily sets scene lighting without play mode). |
| Gameplay Inspector | `Modules/GameplayInspectorModule.cs` | Play-mode: shows effective VisionRangeMultiplier, MovementSpeedMultiplier, SurfaceFrictionMultiplier, NoiseMultiplier for each active weather. Table of current weather zone overlaps for local player. |

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `WeatherBootstrapSystem` | N/A | No | Runs once at startup |
| `WorldTimeSystem` | < 0.005ms | No | 3 float adds, 1 modulo, 1 comparison |
| `WeatherTransitionSystem` | < 0.01ms | No | State machine, 4 lerps, rare random pick |
| `WeatherGameplaySystem` | < 0.02ms | No | Per-player query (1-4 players), 3 float writes |
| `WeatherZoneSystem` | < 0.02ms | No | Per-zone distance check (typically 0-5 zones) |
| `WeatherLightingSystem` | < 0.03ms | No | Managed, light + RenderSettings writes |
| `WeatherSkyboxSystem` | < 0.02ms | No | Material property writes |
| `WeatherShaderSystem` | < 0.01ms | No | 10x Shader.SetGlobalFloat |
| `WeatherAudioSystem` | < 0.01ms | No | 4x AudioMixer.SetFloat |
| `WeatherVFXSystem` | < 0.03ms | No | Particle system emission rate + velocity updates |
| **Total** | **< 0.17ms** | | All systems combined |

Additional GPU cost from weather VFX:

| VFX Type | Max Particles | GPU Budget |
|----------|---------------|------------|
| Rain (heavy) | 3000 | < 0.5ms |
| Snow (heavy) | 2000 | < 0.3ms |
| Sandstorm | 1500 | < 0.3ms |
| Lightning flash | 1 per event | Negligible |
| Fog | RenderSettings only | Built-in URP fog |

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| No WeatherBootstrapAuthoring in scene | No singletons created | All weather systems early-out (RequireForUpdate fails) |
| No WeatherGameplayModifierAuthoring on player | No modifier components | WeatherGameplaySystem skips player, no movement/vision changes |
| No WeatherZone entities | No zone overrides | WeatherZoneSystem skips, global weather applies everywhere |
| No WeatherWorkstation module registered | No editor window | Systems run normally, no debug UI |
| Existing shaders without `_RainIntensity` reads | Default 0.0 | Shader.SetGlobalFloat is no-op for shaders that don't reference the property |
| No `RainVolume`/`WindVolume` AudioMixer params | AudioManager unchanged | WeatherAudioSystem logs warning, skips audio |
| Server without weather config | DefaultWeather=Clear | Perpetual clear weather, TimeOfDay still ticks |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `WeatherVisionModifier` | 4 bytes | Player entity |
| `WeatherMovementModifier` | 4 bytes | Player entity |
| **Total on player** | **8 bytes** | |
| `WorldTimeState` | 16 bytes | Singleton entity (NOT player) |
| `WeatherState` | 40 bytes | Singleton entity (NOT player) |
| `WeatherZone` | 8 bytes | Zone volume entities (NOT player) |

All heavy state lives on singleton or zone entities. Player archetype impact is 8 bytes for optional gameplay modifier components. If even 8 bytes is too tight, `WeatherGameplaySystem` can write modifiers to a singleton and existing systems read the singleton instead of per-player components.

---

## File Summary

### New Files (30)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Weather/Components/WeatherEnums.cs` | Enums (WeatherType, Season, TimeOfDayPeriod) |
| 2 | `Assets/Scripts/Weather/Components/WorldTimeState.cs` | IComponentData singleton (Ghost:All) |
| 3 | `Assets/Scripts/Weather/Components/WeatherState.cs` | IComponentData singleton (Ghost:All) |
| 4 | `Assets/Scripts/Weather/Components/WeatherZoneComponents.cs` | IComponentData + tag for zone volumes |
| 5 | `Assets/Scripts/Weather/Components/WeatherManagerSingleton.cs` | Managed singleton (BlobRefs + seed) |
| 6 | `Assets/Scripts/Weather/Components/WeatherGameplayModifier.cs` | IComponentData (vision + movement modifiers) |
| 7 | `Assets/Scripts/Weather/Data/WeatherBlobs.cs` | BlobAsset structs (transition, day-night, params) |
| 8 | `Assets/Scripts/Weather/Config/DayNightConfigSO.cs` | ScriptableObject |
| 9 | `Assets/Scripts/Weather/Config/WeatherConfigSO.cs` | ScriptableObject |
| 10 | `Assets/Scripts/Weather/Config/WeatherZoneConfigSO.cs` | ScriptableObject |
| 11 | `Assets/Scripts/Weather/Systems/WeatherBootstrapSystem.cs` | SystemBase (init, runs once) |
| 12 | `Assets/Scripts/Weather/Systems/WorldTimeSystem.cs` | SystemBase (ticks clock) |
| 13 | `Assets/Scripts/Weather/Systems/WeatherTransitionSystem.cs` | SystemBase (state machine) |
| 14 | `Assets/Scripts/Weather/Systems/WeatherGameplaySystem.cs` | SystemBase (gameplay modifiers) |
| 15 | `Assets/Scripts/Weather/Systems/WeatherZoneSystem.cs` | SystemBase (zone overrides) |
| 16 | `Assets/Scripts/Weather/Systems/WeatherLightingSystem.cs` | SystemBase (sun/ambient) |
| 17 | `Assets/Scripts/Weather/Systems/WeatherSkyboxSystem.cs` | SystemBase (skybox blending) |
| 18 | `Assets/Scripts/Weather/Systems/WeatherShaderSystem.cs` | SystemBase (global shader props) |
| 19 | `Assets/Scripts/Weather/Systems/WeatherAudioSystem.cs` | SystemBase (ambient audio loops) |
| 20 | `Assets/Scripts/Weather/Systems/WeatherVFXSystem.cs` | SystemBase (rain/snow/lightning VFX) |
| 21 | `Assets/Scripts/Weather/Authoring/WeatherBootstrapAuthoring.cs` | Baker (singleton creation) |
| 22 | `Assets/Scripts/Weather/Authoring/WeatherZoneAuthoring.cs` | Baker (zone volumes) |
| 23 | `Assets/Scripts/Weather/Authoring/WeatherGameplayModifierAuthoring.cs` | Baker (player modifier) |
| 24 | `Assets/Scripts/Weather/DIG.Weather.asmdef` | Assembly definition |
| 25 | `Assets/Shaders/Include/WeatherGlobals.hlsl` | HLSL include (global properties) |
| 26 | `Assets/Editor/WeatherWorkstation/WeatherWorkstationWindow.cs` | EditorWindow (5 tabs) |
| 27 | `Assets/Editor/WeatherWorkstation/IWeatherWorkstationModule.cs` | Interface |
| 28 | `Assets/Editor/WeatherWorkstation/Modules/TimeControlModule.cs` | Editor module |
| 29 | `Assets/Editor/WeatherWorkstation/Modules/WeatherControlModule.cs` | Editor module |
| 30 | `Assets/Editor/WeatherWorkstation/Modules/TransitionGraphModule.cs` | Editor module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Vision/Systems/DetectionSystem.cs` | Read `WeatherVisionModifier.RangeMultiplier`, scale effective detection range (~5 lines) |
| 2 | `Assets/Scripts/Surface/Systems/SurfaceSlipSystem.cs` | Read `WeatherWetness` singleton, apply friction multiplier (~5 lines) |
| 3 | `Assets/Scripts/Audio/AudioManager.cs` | Add exposed mixer parameters: `RainVolume`, `WindVolume`, `ThunderVolume`, `AmbientNightVolume` (~10 lines) |
| 4 | Player prefab (Warrok_Server) | Add `WeatherGameplayModifierAuthoring` |
| 5 | Subscene root | Add `WeatherBootstrapAuthoring` on a new GameObject |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/DayNightConfig.asset` |
| 2 | `Resources/WeatherConfig.asset` |

---

## Cross-EPIC Integration

| Source | EPIC | Integration |
|--------|------|-------------|
| `SurfaceSlipSystem` | 16.10 | Reads `WeatherWetness` singleton -> wet friction multiplier on rain/snow |
| `SurfaceMovementModifierSystem` | 16.10 | Reads `WeatherMovementModifier` -> snow/storm movement penalty |
| `DetectionSystem` | 15.33 | Reads `WeatherVisionModifier` -> fog/rain reduces AI detection range |
| `BarkTimerSystem` | 16.16 | Weather-specific bark triggers: rain start, storm warning, sunrise/sunset |
| `VFXExecutionSystem` | 16.7 | Lightning VFX via `VFXRequest(VFXCategory.Environment)` |
| `AudioManager` | Existing | Exposed mixer params for ambient weather audio loops |
| Global shaders | Existing | `_RainIntensity`, `_SnowIntensity` for terrain wetness, vegetation wind |
| `SaveSystem` | 16.15 | NOT integrated -- weather is ephemeral. Time-of-day resets or continues from server |
| `QuestConditionType` | 16.12 | Future: `WeatherCondition` (quest objectives requiring specific weather) |
| `StatusEffectSystem` | Existing | Future: `Frostbite` status effect during prolonged HeavySnow exposure |

---

## Multiplayer

### Server-Authoritative Model

- `WorldTimeState` and `WeatherState` are `Ghost:All` singletons -- server writes, all clients read
- Weather transitions are deterministic given seed + elapsed time -- late-joining clients receive current state via ghost snapshot
- **No client prediction** of weather -- purely cosmetic/modifier systems read replicated state
- `WeatherGameplaySystem` runs on `Server|Local` -- server applies vision/movement modifiers authoritatively
- Client presentation systems (`Client|Local`) are cosmetic only -- lighting, shaders, audio, VFX

### Weather Zone Authority

- `WeatherZoneSystem` runs on `Client|Local` because zone overrides are purely visual (lighting/shader/audio/VFX)
- Gameplay modifiers (vision, movement) are computed from the global `WeatherState` on the server, NOT from zone overrides
- If per-zone gameplay modifiers are needed in the future, a server-side `WeatherZoneGameplaySystem` can be added

### Listen Server vs. Dedicated Server

- Listen server: ServerWorld ticks `WorldTimeSystem` + `WeatherTransitionSystem`, ClientWorld receives via ghost replication, both worlds run presentation systems
- Dedicated server: server has no presentation systems (no lighting, no shaders, no VFX). Only gameplay modifier systems run. Clients handle all visuals.

---

## Verification Checklist

### Core Clock
- [ ] Server boots: `WorldTimeState` singleton exists with `TimeOfDay=8.0`, `DayCount=0`
- [ ] Time advances: `TimeOfDay` increases each frame, reaches 24 and wraps to 0
- [ ] Day counter: `DayCount` increments when `TimeOfDay` wraps past 24
- [ ] Season progression: Season advances after `SeasonLengthDays` game days
- [ ] Full season cycle: Spring -> Summer -> Autumn -> Winter -> Spring
- [ ] Pause: `IsPaused=true` freezes `TimeOfDay`, unpausing resumes
- [ ] TimeScale: `TimeScale=2.0` doubles clock speed, `0.5` halves it
- [ ] Ghost replication: client `WorldTimeState` matches server within 1 tick

### Weather State Machine
- [ ] Default weather: server starts with `DefaultWeather` from config
- [ ] Weather transition: after interval, `NextWeather` is picked from weighted probabilities
- [ ] Transition progress: `TransitionProgress` lerps 0 to 1 over `TransitionDuration`
- [ ] Rain intensity lerp: `RainIntensity` smoothly transitions between weather types
- [ ] Snow intensity lerp: same for `SnowIntensity`
- [ ] Fog density lerp: same for `FogDensity`
- [ ] Wind changes: `WindDirection` and `WindSpeed` transition smoothly
- [ ] Season affects transitions: winter favors snow, summer favors rain
- [ ] Thunderstorm lightning: `LightningTimer` counts down and resets during storms
- [ ] Ghost replication: client `WeatherState` matches server

### Lighting & Skybox
- [ ] Sun angle: directional light rotates with TimeOfDay (rises at sunrise, sets at sunset)
- [ ] Sun color: warm orange at dawn/dusk, white at noon
- [ ] Sun intensity: zero at night, peak at noon
- [ ] Weather dimming: cloudy/rain reduces sun intensity proportionally
- [ ] Ambient color: dark blue at night, warm tone during day
- [ ] Skybox transitions: dawn/day/dusk/night materials blend at boundaries
- [ ] Star visibility: stars visible at night, fade at dawn, invisible during day
- [ ] Night is visually dark: ambient intensity drops to NightAmbientIntensity

### Shader Integration
- [ ] `Shader.SetGlobalFloat("_TimeOfDay")` updates every frame
- [ ] `_RainIntensity` matches `WeatherState.RainIntensity`
- [ ] `_SnowIntensity` matches `WeatherState.SnowIntensity`
- [ ] `_FogDensity` matches `WeatherState.FogDensity`
- [ ] `_WindDirectionX/Y` + `_WindSpeed` match WeatherState
- [ ] Existing shaders unaffected (properties default to 0.0 when not referenced)

### Gameplay Modifiers
- [ ] Fog: AI detection range reduced (VisionRangeMultiplier < 1.0)
- [ ] Heavy rain: AI detection range reduced
- [ ] Rain: surface friction reduced (wet surfaces more slippery)
- [ ] Snow: movement speed reduced
- [ ] Sandstorm: both vision and movement reduced
- [ ] Clear weather: all multipliers at 1.0 (no change from baseline)
- [ ] Transition blending: modifiers lerp during weather transition (no pop)

### Audio
- [ ] Rain start: rain ambient loop fades in proportional to RainIntensity
- [ ] Rain stop: rain ambient loop fades out over 2 seconds
- [ ] Wind: wind loop volume scales with WindSpeed
- [ ] Thunder: oneshot plays on lightning event with random delay
- [ ] Night ambient: cricket/owl loops fade in at dusk, out at dawn
- [ ] No audio pops: all volume changes use smooth dB lerp

### VFX
- [ ] Rain particles: visible during LightRain/HeavyRain, scaled by intensity
- [ ] Rain wind influence: particles drift in wind direction
- [ ] Snow particles: visible during LightSnow/HeavySnow, slower than rain
- [ ] Lightning flash: screen flash + skybox bolt on lightning event
- [ ] Fog: `RenderSettings.fog` enabled/disabled by FogDensity threshold
- [ ] Particles disabled when intensity < 0.01 (no ghost particles)

### Weather Zones
- [ ] Enter zone: local weather overrides blend in over BlendRadius
- [ ] Exit zone: local weather blends back to global
- [ ] Zone priority: higher-priority zone wins on overlap
- [ ] Zone override 255: zone follows global weather (no override)
- [ ] Server gameplay unaffected: zones are client-only visual override

### Multiplayer
- [ ] Late join: new client receives correct TimeOfDay and WeatherState on connect
- [ ] All clients see same sun angle (within interpolation tolerance)
- [ ] All clients see same weather type (rain/snow/fog consistent)
- [ ] Dedicated server: no presentation systems allocated, no GPU cost
- [ ] Listen server: both server and client worlds have correct state

### Editor Tooling
- [ ] Weather Workstation: Time Controls scrubs TimeOfDay in play mode
- [ ] Weather Workstation: Weather Controls forces weather type override
- [ ] Weather Workstation: Transition Graph displays probabilities as weighted edges
- [ ] Weather Workstation: Lighting Preview shows gradient across 24 hours
- [ ] Weather Workstation: Gameplay Inspector shows active modifier values

### Performance
- [ ] All weather systems combined: < 0.2ms CPU
- [ ] Rain particles (heavy): < 0.5ms GPU
- [ ] No frame hitch on weather transition start
- [ ] No memory allocation per frame (no managed allocs in update loops)
- [ ] WeatherBootstrapSystem runs once, self-disables
- [ ] Scenes without WeatherBootstrapAuthoring: zero overhead (systems early-out)

### Backward Compatibility
- [ ] Existing scenes without weather: no errors, no visual changes
- [ ] Player prefab without WeatherGameplayModifierAuthoring: no gameplay modifier, no crash
- [ ] Existing shaders without weather keyword reads: unchanged rendering
- [ ] AudioManager without exposed weather params: warning logged, no crash
