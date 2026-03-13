# EPIC 3.5: Pulses

**Status**: Planning
**Epic**: EPIC 3 — The Front (District Pressure System)
**Priority**: High — Memorable dramatic moments at phase thresholds
**Dependencies**: EPIC 3.1 (FrontState, FrontPhaseChangedTag, FrontPhase); Framework: Roguelite/ (RunModifierStack — pulse effects as temporary modifiers), AI/ (EnemySpawnRequest — enemy surge pulses)
**Optional**: EPIC 3.3 (zone restrictions during pulses)

---

## Overview

Pulses are district-wide dramatic events triggered at Front phase thresholds. When the Front crosses a phase boundary, the PulseSystem selects and fires one or more Pulses from the district's PulseDefinitionSO pool. Pulses are announced with readable warnings (screen flash, audio cue, countdown timer), then apply temporary zone-wide effects: enemy surges, hazard intensification, route closures, or boss previews. Pulses are designed to be "oh shit" moments — memorable and impactful, not just difficulty spikes. Each district has 6-10 unique pulse types defined by its PulseDefinitionSO.

---

## Component Definitions

### PulseEffectType Enum

```csharp
// File: Assets/Scripts/Front/Components/PulseComponents.cs
namespace Hollowcore.Front
{
    /// <summary>
    /// Categories of pulse effects. Each pulse can combine multiple effects.
    /// </summary>
    [System.Flags]
    public enum PulseEffectType : ushort
    {
        None = 0,
        EnemySurge = 1 << 0,          // Spawn wave of enemies
        HazardIntensification = 1 << 1, // Existing hazards become more lethal
        RouteClosure = 1 << 2,          // Specific routes become impassable
        RouteOpening = 1 << 3,          // New shortcut routes temporarily open
        BossPreview = 1 << 4,           // Mini-encounter with district boss variant
        EnvironmentShift = 1 << 5,      // Gravity, weather, lighting change
        EliteSpawn = 1 << 6,            // Spawn elite/mini-boss enemies
        ResourceDrain = 1 << 7,         // Player resources deplete faster
        CommunicationJam = 1 << 8,      // UI elements disabled (minimap, markers)
        AmbienceShift = 1 << 9          // Audio/visual atmosphere changes dramatically
    }
}
```

### PulseState (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/PulseComponents.cs (continued)
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace Hollowcore.Front
{
    /// <summary>
    /// Current pulse state for a district. One per district entity.
    /// Tracks the active pulse (if any), its remaining duration, and warning countdown.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PulseState : IComponentData
    {
        /// <summary>True if a pulse is currently active or in warning phase.</summary>
        [GhostField] public bool IsActive;

        /// <summary>True if pulse is in warning countdown (not yet executing effects).</summary>
        [GhostField] public bool IsWarning;

        /// <summary>ID of the active pulse definition (from PulseDefinitionSO).</summary>
        [GhostField] public int ActivePulseId;

        /// <summary>Effects applied by the current pulse.</summary>
        [GhostField] public PulseEffectType ActiveEffects;

        /// <summary>Remaining warning time in seconds before pulse fires.</summary>
        [GhostField(Quantization = 100)] public float WarningTimeRemaining;

        /// <summary>Remaining duration of the active pulse in seconds.</summary>
        [GhostField(Quantization = 100)] public float DurationRemaining;

        /// <summary>Display name for UI warning banner.</summary>
        [GhostField] public FixedString64Bytes PulseDisplayName;

        /// <summary>Number of pulses fired in this district so far (for escalation).</summary>
        [GhostField] public int PulsesFired;
    }
}
```

### PulseActiveTag (IEnableableComponent)

```csharp
// File: Assets/Scripts/Front/Components/PulseComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Enableable tag active while a pulse is executing (warning phase excluded).
    /// Systems that respond to pulse effects query for this tag.
    /// Baked disabled on district entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PulseActiveTag : IComponentData, IEnableableComponent { }
}
```

### PulseHistory (IBufferElementData)

```csharp
// File: Assets/Scripts/Front/Components/PulseComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Record of fired pulses. Prevents repeats and enables escalation.
    /// Buffer on district entity.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PulseHistory : IBufferElementData
    {
        /// <summary>Pulse definition ID that was fired.</summary>
        public int PulseId;

        /// <summary>Phase at which it was fired.</summary>
        public FrontPhase FiredAtPhase;

        /// <summary>Network tick when fired.</summary>
        public uint FiredAtTick;
    }
}
```

---

## ScriptableObject Definitions

### PulseDefinitionSO

```csharp
// File: Assets/Scripts/Front/Definitions/PulseDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Front.Definitions
{
    [CreateAssetMenu(fileName = "NewPulseDef", menuName = "Hollowcore/Front/Pulse Definition")]
    public class PulseDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int PulseId;
        public string DisplayName;
        [TextArea] public string WarningText;
        public Sprite WarningIcon;

        [Header("Trigger")]
        [Tooltip("Phase at which this pulse can trigger.")]
        public FrontPhase TriggerPhase;
        [Tooltip("SpreadProgress within the phase at which this pulse fires (0-1).")]
        [Range(0f, 1f)]
        public float TriggerProgress = 0.5f;
        [Tooltip("Priority when multiple pulses qualify. Higher = fires first.")]
        public int Priority;
        [Tooltip("If true, this pulse can fire multiple times across different phases.")]
        public bool Repeatable;

        [Header("Timing")]
        [Tooltip("Warning countdown in seconds before pulse effects begin.")]
        public float WarningDuration = 8f;
        [Tooltip("How long pulse effects last in seconds.")]
        public float EffectDuration = 30f;

        [Header("Effects")]
        public PulseEffectType Effects;

        [Header("Enemy Surge")]
        [Tooltip("Number of enemies to spawn (0 if no EnemySurge effect).")]
        public int SurgeEnemyCount;
        [Tooltip("Spawn table for surge enemies. Null = district default.")]
        public Object SurgeSpawnTable; // Reference to spawn table SO

        [Header("Elite Spawn")]
        [Tooltip("Number of elites/mini-bosses to spawn (0 if no EliteSpawn effect).")]
        public int EliteCount;

        [Header("Route Changes")]
        [Tooltip("Zone IDs that become impassable during this pulse.")]
        public int[] ClosedRouteZoneIds;
        [Tooltip("Zone IDs that temporarily open as shortcuts.")]
        public int[] OpenedRouteZoneIds;

        [Header("Hazard Intensification")]
        [Tooltip("DPS multiplier applied to existing zone hazards during pulse.")]
        public float HazardDPSMultiplier = 2f;

        [Header("Boss Preview")]
        [Tooltip("Boss variant prefab for preview encounter. Null if no BossPreview effect.")]
        public GameObject BossPreviewPrefab;
        [Tooltip("Boss is tethered — retreats after this much HP lost (0-1 fraction).")]
        [Range(0f, 1f)]
        public float BossRetreatThreshold = 0.3f;

        [Header("Run Modifier")]
        [Tooltip("Temporary RunModifier applied during pulse. Null = none.")]
        public Object RunModifierOverride; // Reference to RunModifierSO

        [Header("Visual/Audio")]
        [Tooltip("Screen flash color on pulse trigger.")]
        public Color ScreenFlashColor = Color.red;
        [Tooltip("Warning audio event name.")]
        public string WarningAudioEvent;
        [Tooltip("Pulse active audio event name.")]
        public string ActiveAudioEvent;
        [Tooltip("Ambient VFX prefab active during pulse.")]
        public GameObject PulseVFXPrefab;
    }
}
```

### DistrictPulseConfigSO

```csharp
// File: Assets/Scripts/Front/Definitions/DistrictPulseConfigSO.cs
using UnityEngine;

namespace Hollowcore.Front.Definitions
{
    /// <summary>
    /// Per-district pulse pool. Referenced by FrontDefinitionSO.
    /// Each district has 6-10 pulse definitions across its 4 phases.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDistrictPulses", menuName = "Hollowcore/Front/District Pulse Config")]
    public class DistrictPulseConfigSO : ScriptableObject
    {
        [Tooltip("All pulse definitions available in this district.")]
        public PulseDefinitionSO[] Pulses;

        [Tooltip("Maximum simultaneous active pulses (usually 1, boss districts may allow 2).")]
        public int MaxSimultaneousPulses = 1;

        [Tooltip("Minimum cooldown between pulses in seconds.")]
        public float PulseCooldown = 45f;

        [Tooltip("If true, escalate pulse intensity on repeat visits to this district.")]
        public bool EscalateOnRevisit;
    }
}
```

---

## Systems

### PulseSystem

```csharp
// File: Assets/Scripts/Front/Systems/PulseSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontPhaseEvaluationSystem (EPIC 3.1)
//
// Monitors FrontState for phase threshold crossings and triggers pulses.
//
// For each district entity with FrontState + PulseState + PulseHistory:
//   1. If PulseState.IsActive:
//      a. If IsWarning: decrement WarningTimeRemaining
//         - If <= 0: IsWarning = false, enable PulseActiveTag, begin effects
//      b. If !IsWarning: decrement DurationRemaining
//         - If <= 0: end pulse (disable PulseActiveTag, clear ActiveEffects)
//      c. Early out (pulse in progress)
//   2. Check FrontPhaseChangedTag (one-frame event from EPIC 3.1):
//      a. If not enabled: also check SpreadProgress against per-pulse TriggerProgress
//   3. Query DistrictPulseConfigSO for eligible pulses:
//      a. Filter: TriggerPhase matches current phase
//      b. Filter: TriggerProgress <= current SpreadProgress within phase
//      c. Filter: not already fired (check PulseHistory) unless Repeatable
//      d. Filter: cooldown elapsed since last pulse
//      e. Sort by Priority (highest first)
//   4. Select top pulse:
//      a. Set PulseState: IsActive=true, IsWarning=true
//      b. Set WarningTimeRemaining, DurationRemaining, PulseDisplayName, ActiveEffects
//      c. Add entry to PulseHistory buffer
//      d. Increment PulsesFired
```

### PulseEffectExecutionSystem

```csharp
// File: Assets/Scripts/Front/Systems/PulseEffectExecutionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: PulseSystem
//
// Executes pulse effects for districts with enabled PulseActiveTag.
//
// For each district entity with enabled PulseActiveTag + PulseState:
//   1. If ActiveEffects has EnemySurge:
//      a. Create EnemySpawnRequest entities (framework AI/ pattern)
//      b. Spawn at predefined pulse spawn points in converted zones
//      c. One-shot: only spawn on first frame of pulse activation
//   2. If ActiveEffects has EliteSpawn:
//      a. Create elite EnemySpawnRequest entities
//      b. Spawn near player location
//   3. If ActiveEffects has HazardIntensification:
//      a. Add FrontAdvanceModifier with Source=Pulse and positive rate modifier
//      b. Multiply PlayerFrontExposure.HazardDPS by HazardDPSMultiplier
//   4. If ActiveEffects has RouteClosure:
//      a. Mark specified zones as temporarily impassable (write FrontZoneData override)
//   5. If ActiveEffects has RouteOpening:
//      a. Mark specified zones as temporarily accessible
//   6. If ActiveEffects has BossPreview:
//      a. Spawn boss preview entity at designated arena point
//      b. Set retreat threshold — boss retreats (despawns) at HP threshold
//   7. If ActiveEffects has EnvironmentShift:
//      a. Push temporary RunModifier from PulseDefinitionSO.RunModifierOverride
//   8. If ActiveEffects has CommunicationJam:
//      a. Set flag read by UI systems to hide minimap/markers
//   9. If ActiveEffects has ResourceDrain:
//      a. Apply resource drain modifier to player resource systems (EPIC 16.8 framework)
```

### PulseUIBridgeSystem

```csharp
// File: Assets/Scripts/Front/Systems/PulseUIBridgeSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Bridges PulseState to managed UI for warning banners, countdowns, screen effects.
//
// For each district entity with PulseState where IsActive == true:
//   1. If IsWarning:
//      a. Show warning banner: PulseDisplayName + WarningTimeRemaining countdown
//      b. Play warning audio event
//      c. Apply screen flash / vignette
//   2. If !IsWarning (effects active):
//      a. Show active pulse indicator with DurationRemaining
//      b. Apply ambient VFX (instantiate PulseVFXPrefab)
//      c. Play active audio event
//   3. When pulse ends (IsActive goes false):
//      a. Clean up VFX instances
//      b. Hide UI elements
//      c. Play "pulse ended" audio sting
```

---

## Setup Guide

1. **Create PulseComponents.cs** in `Assets/Scripts/Front/Components/`
2. **Create PulseDefinitionSO.cs and DistrictPulseConfigSO.cs** in `Assets/Scripts/Front/Definitions/`
3. **Create pulse definitions** in `Assets/Data/Front/Pulses/` — start with 2-3 per district:
   - Example (Necrospire Phase 2): "Screaming Broadcast" — EnemySurge + AmbienceShift, WarningDuration 8s, EffectDuration 25s
   - Example (Necrospire Phase 3): "Grief-Link Resonance" — HazardIntensification + CommunicationJam, WarningDuration 5s, EffectDuration 40s
   - Example (The Burn Phase 2): "Core Venting" — HazardIntensification + RouteClosure, WarningDuration 10s, EffectDuration 20s
4. **Create DistrictPulseConfigSO** per district, assign 6-10 PulseDefinitionSOs
5. **Add DistrictPulseConfigSO reference** to FrontDefinitionSO (add a field)
6. **Add PulseState, PulseActiveTag (baked disabled), PulseHistory buffer** to district entity in FrontAuthoring baker
7. **Create PulseSystem, PulseEffectExecutionSystem, PulseUIBridgeSystem** in `Assets/Scripts/Front/Systems/`
8. **Configure pulse spawn points** in district scenes — tagged transform markers where surge enemies appear
9. **Test**: advance Front to Phase 2 threshold, verify warning appears, effects execute, duration expires

---

## Verification

- [ ] PulseSystem detects FrontPhaseChangedTag and selects eligible pulse
- [ ] PulseState.IsWarning = true during warning countdown
- [ ] WarningTimeRemaining decrements each frame during warning phase
- [ ] PulseActiveTag enables when warning countdown reaches zero
- [ ] DurationRemaining decrements during active phase
- [ ] PulseActiveTag disables and PulseState.IsActive = false when duration expires
- [ ] EnemySurge spawns correct number of enemies via EnemySpawnRequest
- [ ] EliteSpawn spawns elite entities near player
- [ ] HazardIntensification multiplies existing zone hazard DPS
- [ ] RouteClosure makes specified zones impassable during pulse
- [ ] RouteOpening temporarily opens shortcut zones
- [ ] BossPreview spawns boss variant that retreats at HP threshold
- [ ] PulseHistory prevents non-Repeatable pulses from firing twice
- [ ] PulseCooldown prevents pulses from firing in rapid succession
- [ ] Pulse priority ordering selects highest-priority eligible pulse
- [ ] UI warning banner shows correct name and countdown
- [ ] Screen flash and audio events fire at correct times
- [ ] VFX cleanup occurs when pulse ends
- [ ] Multiple district pulse pools work independently (no cross-district interference)

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Front/Definitions/PulseBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Front
{
    /// <summary>
    /// Burst-compatible pulse definition baked from PulseDefinitionSO.
    /// </summary>
    public struct PulseBlob
    {
        public int PulseId;
        public byte TriggerPhase;          // FrontPhase cast
        public float TriggerProgress;
        public int Priority;
        public bool Repeatable;
        public float WarningDuration;
        public float EffectDuration;
        public ushort Effects;             // PulseEffectType cast
        public int SurgeEnemyCount;
        public int EliteCount;
        public float HazardDPSMultiplier;
        public float BossRetreatThreshold;
        public BlobArray<int> ClosedRouteZoneIds;
        public BlobArray<int> OpenedRouteZoneIds;
        public BlobString DisplayName;
        public BlobString WarningText;
    }

    /// <summary>
    /// Per-district pulse config baked from DistrictPulseConfigSO.
    /// </summary>
    public struct DistrictPulseBlob
    {
        public BlobArray<PulseBlob> Pulses;
        public int MaxSimultaneousPulses;
        public float PulseCooldown;
        public bool EscalateOnRevisit;
    }
}

// DistrictPulseConfigSO.BakeToBlob() iterates Pulses array,
// bakes each PulseDefinitionSO into a PulseBlob entry.
// Baker stores BlobAssetReference<DistrictPulseBlob> on the district entity
// alongside FrontDefinitionBlob.
```

---

## Validation

```csharp
// File: Assets/Editor/Validation/PulseValidation.cs
// OnValidate() rules for PulseDefinitionSO:
//
// - PulseId must be unique within the parent DistrictPulseConfigSO
// - DisplayName and WarningText must not be null/empty
// - TriggerPhase must be Phase1-Phase4
// - TriggerProgress must be in [0.0, 1.0]
// - WarningDuration must be >= 3.0 (minimum readable warning)
// - EffectDuration must be > 0
// - If EnemySurge set: SurgeEnemyCount must be > 0
// - If EliteSpawn set: EliteCount must be > 0
// - If BossPreview set: BossPreviewPrefab must not be null, BossRetreatThreshold in (0, 1)
// - If RouteClosure set: ClosedRouteZoneIds must not be empty
// - If RouteOpening set: OpenedRouteZoneIds must not be empty
// - HazardDPSMultiplier must be >= 1.0 (intensification, not reduction)
// - Effects flags must have at least one bit set
//
// OnValidate() for DistrictPulseConfigSO:
// - Pulses array must not be null or empty
// - Pulses array length must be 6-10 (GDD requirement)
// - MaxSimultaneousPulses must be >= 1
// - PulseCooldown must be > 0
// - Each TriggerPhase must have at least 1 pulse assigned
// - No duplicate PulseId values within the array
```

---

## Editor Tooling

**Pulse Timeline Editor** (`Window > Hollowcore > Pulse Timeline`):
- Workstation-style EditorWindow following DIG workstation pattern (sidebar tabs, `IWorkstationModule`)
- Modules:
  - **Pulse Pool**: list all PulseDefinitionSOs for selected district, filterable by phase
  - **Timeline View**: horizontal timeline showing pulse trigger points along SpreadProgress (0-1), phase boundaries as vertical dividers, pulse durations as colored bars
  - **Preview**: select a pulse to see warning text, effects summary, spawn counts, route changes
  - **Balance View**: total surge enemy count per phase, aggregate DPS multiplier, route change summary
- No play mode required: reads SO data directly

---

## Debug Visualization

**Pulse State Overlay** (toggle via debug menu):
- HUD panel:
  - Active pulse name, warning countdown or effect duration remaining
  - Active effects flags as icon row (lit = active)
  - PulsesFired counter, cooldown timer until next eligible pulse
- In-world: pulse spawn points highlighted with enemy count labels during EnemySurge
- Closed/opened routes: zone boundaries drawn in red (closed) or green (opened) during RouteClosure/RouteOpening effects
- Boss preview: health bar and retreat threshold indicator on spawned boss

**Activation**: Debug menu toggle `Front/Pulses/ShowState`
