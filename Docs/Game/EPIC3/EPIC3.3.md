# EPIC 3.3: Zone Restriction & Traversal Changes

**Status**: Planning
**Epic**: EPIC 3 — The Front (District Pressure System)
**Priority**: High — The Front changes how you move, not just difficulty
**Dependencies**: EPIC 3.1 (FrontState, FrontZoneState, FrontZoneData); Framework: Environment/ (hazard damage), Roguelite/ (zone topology); Optional: EPIC 1 (gear-based restriction bypass)

---

## Overview

As the Front converts zones, it imposes traversal restrictions that fundamentally change how the player navigates the district. Each district has a unique restriction type (RadStorm, AcidicFlood, NanobotSwarm, etc.) that determines what gear, abilities, or tactics are needed to survive in Hostile and Overrun zones. The ZoneRestrictionSystem checks the player's current zone against its FrontZoneState and restriction type, warns the player before entry, and applies escalating penalties. Some restrictions are hard gates (cannot enter without specific equipment), others are soft penalties (DOT, ability lockout, movement reduction).

---

## Component Definitions

### FrontRestriction Enum

```csharp
// File: Assets/Scripts/Front/Components/FrontRestrictionComponents.cs
namespace Hollowcore.Front
{
    /// <summary>
    /// Types of zone restrictions imposed by the Front.
    /// Each district has one primary restriction type defined in FrontDefinitionSO.
    /// </summary>
    public enum FrontRestriction : byte
    {
        None = 0,
        RadStorm = 1,        // Underground only — open-air zones become lethal
        AcidicFlood = 2,     // Vertical movement only — ground level flooded
        NanobotSwarm = 3,    // Stealth or sealed armor required
        HunterPacks = 4,     // Avoid open ground — elite packs patrol
        Firestorm = 5,       // Narrow survival windows between fire waves
        EMPZone = 6,         // Analog gear only — augment abilities disabled
        FloodedZone = 7,     // Swimming/boat required — submerged traversal
        CollapseDebris = 8,  // Grapple/glider only — ground routes blocked
        CognitiveStatic = 9, // Hallucinations, false enemies, input delay
        BiohazardFog = 10    // Inoculation or contamination timer
    }
}
```

### ZoneRestrictionPenalty Enum

```csharp
// File: Assets/Scripts/Front/Components/FrontRestrictionComponents.cs (continued)
namespace Hollowcore.Front
{
    /// <summary>
    /// Types of penalties applied when a player enters a restricted zone
    /// without meeting the bypass condition.
    /// </summary>
    [System.Flags]
    public enum ZoneRestrictionPenalty : ushort
    {
        None = 0,
        DamageOverTime = 1 << 0,       // Continuous HP drain
        AbilityLockout = 1 << 1,       // Specific ability categories disabled
        MovementReduction = 1 << 2,    // Move speed reduced
        VisionReduction = 1 << 3,      // Fog/blur/reduced draw distance
        AugmentDisable = 1 << 4,       // Cybernetic augment abilities offline
        StaminaDrain = 1 << 5,         // Stamina drains faster
        HealingReduction = 1 << 6,     // Healing effectiveness reduced
        InputDelay = 1 << 7,           // Artificial input lag (CognitiveStatic)
        Hallucinations = 1 << 8,       // False enemies/geometry (CognitiveStatic)
        ContaminationTimer = 1 << 9    // Countdown to forced retreat (BiohazardFog)
    }
}
```

### PlayerFrontExposure (IComponentData)

```csharp
// File: Assets/Scripts/Front/Components/FrontRestrictionComponents.cs (continued)
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Front
{
    /// <summary>
    /// Tracks the player's current exposure to Front restrictions.
    /// On the player entity (small — 20 bytes, safe for player archetype).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerFrontExposure : IComponentData
    {
        /// <summary>The restriction type of the player's current zone (None if zone is Safe).</summary>
        [GhostField] public FrontRestriction ActiveRestriction;

        /// <summary>Current FrontZoneState of the zone the player is in.</summary>
        [GhostField] public FrontZoneState CurrentZoneState;

        /// <summary>Active penalties being applied this frame.</summary>
        [GhostField] public ZoneRestrictionPenalty ActivePenalties;

        /// <summary>
        /// True if the player meets bypass conditions for the active restriction.
        /// Checked against equipment, abilities, chassis state.
        /// </summary>
        [GhostField] public bool HasBypass;

        /// <summary>Contamination timer (BiohazardFog only). Seconds remaining before forced retreat.</summary>
        [GhostField(Quantization = 100)] public float ContaminationTimer;

        /// <summary>DOT accumulator — damage per second applied by zone hazard.</summary>
        [GhostField(Quantization = 100)] public float HazardDPS;
    }
}
```

### ZoneRestrictionWarning (IComponentData, IEnableableComponent)

```csharp
// File: Assets/Scripts/Front/Components/FrontRestrictionComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Front
{
    /// <summary>
    /// Enableable component toggled when player approaches a restricted zone boundary.
    /// UI reads this to show warning prompt. Baked disabled on player entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ZoneRestrictionWarning : IComponentData, IEnableableComponent
    {
        /// <summary>Restriction type the player is about to enter.</summary>
        [GhostField] public FrontRestriction UpcomingRestriction;

        /// <summary>FrontZoneState of the zone ahead.</summary>
        [GhostField] public FrontZoneState UpcomingZoneState;

        /// <summary>True if this is a hard gate (cannot enter without bypass).</summary>
        [GhostField] public bool IsHardGate;
    }
}
```

### FrontRestrictionDefinitionSO

```csharp
// File: Assets/Scripts/Front/Definitions/FrontRestrictionDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Front.Definitions
{
    [CreateAssetMenu(fileName = "NewRestrictionDef", menuName = "Hollowcore/Front/Restriction Definition")]
    public class FrontRestrictionDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public FrontRestriction RestrictionType;
        public string DisplayName;
        [TextArea] public string WarningMessage;
        public Sprite WarningIcon;

        [Header("Gating")]
        [Tooltip("True = cannot enter without bypass. False = penalties applied but entry allowed.")]
        public bool IsHardGate;
        [Tooltip("FrontZoneState at which restriction activates. Hostile = most, Contested = aggressive.")]
        public FrontZoneState ActivationThreshold = FrontZoneState.Hostile;

        [Header("Bypass Conditions")]
        [Tooltip("Equipment tag required to bypass (e.g., 'SealedArmor', 'AnalogGear'). Empty = no gear bypass.")]
        public string RequiredEquipmentTag;
        [Tooltip("Ability tag required to bypass (e.g., 'Stealth', 'Grapple'). Empty = no ability bypass.")]
        public string RequiredAbilityTag;
        [Tooltip("Chassis slot requirements (e.g., specific limb type). -1 = none.")]
        public int RequiredLimbDefinitionId = -1;

        [Header("Penalties — Contested Zone")]
        public ZoneRestrictionPenalty ContestedPenalties;
        public float ContestedDPS = 2f;
        public float ContestedMoveSpeedMultiplier = 0.85f;

        [Header("Penalties — Hostile Zone")]
        public ZoneRestrictionPenalty HostilePenalties;
        public float HostileDPS = 8f;
        public float HostileMoveSpeedMultiplier = 0.6f;

        [Header("Penalties — Overrun Zone")]
        public ZoneRestrictionPenalty OverrunPenalties;
        public float OverrunDPS = 20f;
        public float OverrunMoveSpeedMultiplier = 0.3f;

        [Header("BiohazardFog Specific")]
        [Tooltip("Contamination timer duration in seconds (0 = not applicable).")]
        public float ContaminationDuration;

        [Header("CognitiveStatic Specific")]
        [Tooltip("Input delay in seconds (0 = not applicable).")]
        public float InputDelaySeconds;
        [Tooltip("Hallucination spawn rate per minute (0 = not applicable).")]
        public float HallucinationRate;

        [Header("Visuals")]
        public Color ZoneTintOverride;
        public GameObject HazardVFXPrefab;
        public string HazardAudioEvent;
    }
}
```

---

## Systems

### ZoneRestrictionSystem

```csharp
// File: Assets/Scripts/Front/Systems/ZoneRestrictionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: FrontZoneConversionSystem (EPIC 3.1)
//
// Checks player position against zone restriction state and applies penalties.
//
// For each player entity with PlayerFrontExposure:
//   1. Determine player's current zone (from zone tracking system / spatial query)
//   2. Look up FrontZoneData for that zone on the district entity
//   3. If FrontZoneState == Safe:
//      a. Clear all penalties, set ActiveRestriction = None
//      b. Early out
//   4. Look up FrontRestrictionDefinitionSO for this district's restriction type
//   5. If zone state < ActivationThreshold: apply no penalties (restriction not yet active)
//   6. Check bypass conditions:
//      a. Query player equipment for RequiredEquipmentTag
//      b. Query player abilities for RequiredAbilityTag
//      c. Query chassis for RequiredLimbDefinitionId
//      d. Set HasBypass = true if any condition met
//   7. If HasBypass: apply reduced penalties (25% of normal DPS, no lockouts)
//   8. If !HasBypass and IsHardGate:
//      a. Push player back to zone boundary (reject entry)
//      b. Enable ZoneRestrictionWarning
//      c. Early out
//   9. If !HasBypass and !IsHardGate:
//      a. Apply penalties based on zone state tier (Contested/Hostile/Overrun)
//      b. Set ActivePenalties flags
//      c. Set HazardDPS
//      d. If BiohazardFog: decrement ContaminationTimer
//      e. If CognitiveStatic: queue hallucination spawns
```

### ZoneRestrictionWarningSystem

```csharp
// File: Assets/Scripts/Front/Systems/ZoneRestrictionWarningSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ZoneRestrictionSystem
//
// Detects when player is near a restricted zone boundary and toggles warning.
//
// For each player entity with ZoneRestrictionWarning:
//   1. Check adjacent zones (within detection radius, ~15m from boundary)
//   2. For each adjacent zone with FrontZoneState >= ActivationThreshold:
//      a. If player is moving toward that zone:
//         - Enable ZoneRestrictionWarning
//         - Set UpcomingRestriction, UpcomingZoneState, IsHardGate
//   3. If no restricted adjacent zone or player moving away:
//      a. Disable ZoneRestrictionWarning
```

### FrontHazardDamageSystem

```csharp
// File: Assets/Scripts/Front/Systems/FrontHazardDamageSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: DamageSystemGroup (PredictedFixedStepSimulation)
//
// Applies damage-over-time from zone hazards to players AND AI enemies.
//
// For each entity with Health + PlayerFrontExposure:
//   1. If HazardDPS > 0:
//      a. Create DamageEvent: amount = HazardDPS * fixedDeltaTime, type = Environmental
//      b. DamageEvent goes through normal DamageApplySystem pipeline
//
// For each AI entity with Health in a restricted zone:
//   1. Look up zone's FrontZoneState from FrontZoneData
//   2. If zone is Hostile or Overrun:
//      a. Check AI's faction immunity (thematically immune factions skip)
//      b. Apply hazard DPS via DamageEvent (same as player)
//      c. This enables "Exploit" counterplay (EPIC 3.4) — lure enemies into hazards
```

---

## Setup Guide

1. **Create FrontRestrictionComponents.cs** in `Assets/Scripts/Front/Components/`
2. **Create FrontRestrictionDefinitionSO.cs** in `Assets/Scripts/Front/Definitions/`
3. **Create one FrontRestrictionDefinitionSO** per restriction type in `Assets/Data/Front/Restrictions/`:
   - RadStorm: HardGate at Overrun, Contested DPS 2, Hostile DPS 8
   - AcidicFlood: HardGate at Overrun, movement reduction primary
   - NanobotSwarm: SoftGate, RequiredEquipmentTag "SealedArmor"
   - EMPZone: SoftGate, AugmentDisable penalty, RequiredEquipmentTag "AnalogGear"
   - BiohazardFog: SoftGate, ContaminationDuration 120s
   - CognitiveStatic: SoftGate, InputDelay 0.15s, HallucinationRate 6/min
4. **Add PlayerFrontExposure** to player prefab authoring (20 bytes — safe for player archetype)
5. **Add ZoneRestrictionWarning** (baked disabled) to player prefab authoring
6. **Link FrontRestrictionDefinitionSO** reference in each FrontDefinitionSO (add a field)
7. **Create ZoneRestrictionSystem, ZoneRestrictionWarningSystem, FrontHazardDamageSystem** in `Assets/Scripts/Front/Systems/`
8. **Test restriction zones**: place player in a zone, advance Front to Hostile, verify DPS applies and warning shows on approach

---

## Verification

- [ ] PlayerFrontExposure updates correctly when player moves between zones
- [ ] ActiveRestriction is None when player is in a Safe zone
- [ ] Penalties escalate correctly across Contested, Hostile, Overrun tiers
- [ ] Hard gate restrictions prevent player from entering without bypass equipment
- [ ] Soft gate restrictions apply penalties but allow entry
- [ ] Bypass conditions correctly check equipment tags, ability tags, limb IDs
- [ ] Players with bypass receive reduced penalties (25% DPS, no lockouts)
- [ ] ZoneRestrictionWarning enables when player approaches restricted zone boundary
- [ ] ZoneRestrictionWarning disables when player moves away
- [ ] ZoneRestrictionWarning.IsHardGate accurately reflects restriction type
- [ ] FrontHazardDamageSystem applies DPS through standard DamageEvent pipeline
- [ ] AI enemies in restricted zones also take hazard damage
- [ ] Thematically immune AI factions skip hazard damage
- [ ] BiohazardFog contamination timer counts down and triggers forced retreat
- [ ] CognitiveStatic hallucinations spawn at configured rate
- [ ] EMPZone disables augment abilities when player lacks AnalogGear
- [ ] Movement speed reduction applies correctly per zone state tier

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Front/Definitions/FrontRestrictionBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Front
{
    /// <summary>
    /// Burst-compatible restriction definition baked from FrontRestrictionDefinitionSO.
    /// Indexed by FrontRestriction enum value at runtime.
    /// </summary>
    public struct FrontRestrictionBlob
    {
        public byte RestrictionType;       // FrontRestriction cast
        public bool IsHardGate;
        public byte ActivationThreshold;   // FrontZoneState cast

        // Bypass conditions
        public BlobString RequiredEquipmentTag;
        public BlobString RequiredAbilityTag;
        public int RequiredLimbDefinitionId;

        // Penalties per zone state tier (3 tiers: Contested, Hostile, Overrun)
        public ushort ContestedPenalties;   // ZoneRestrictionPenalty cast
        public float ContestedDPS;
        public float ContestedMoveSpeed;

        public ushort HostilePenalties;
        public float HostileDPS;
        public float HostileMoveSpeed;

        public ushort OverrunPenalties;
        public float OverrunDPS;
        public float OverrunMoveSpeed;

        // Type-specific
        public float ContaminationDuration;
        public float InputDelaySeconds;
        public float HallucinationRate;

        public BlobString DisplayName;
        public BlobString WarningMessage;
    }

    public struct FrontRestrictionDatabase
    {
        public BlobArray<FrontRestrictionBlob> Entries; // Indexed by FrontRestriction enum
    }
}
```

Baker collects all `FrontRestrictionDefinitionSO` assets into a singleton `BlobAssetReference<FrontRestrictionDatabase>`. `ZoneRestrictionSystem` reads blob entries in Burst-compiled code without managed SO access.

---

## Validation

```csharp
// File: Assets/Editor/Validation/FrontRestrictionValidation.cs
// OnValidate() rules for FrontRestrictionDefinitionSO:
//
// - RestrictionType must be a valid non-None enum value
// - DisplayName and WarningMessage must not be null/empty
// - WarningIcon should not be null (warning)
// - ActivationThreshold must be Contested or higher (Safe makes no sense)
// - ContestedDPS, HostileDPS, OverrunDPS must be >= 0
// - DPS must escalate: ContestedDPS <= HostileDPS <= OverrunDPS
// - MoveSpeed multipliers must be in (0, 1.0]
// - MoveSpeed must decrease: ContestedMoveSpeed >= HostileMoveSpeed >= OverrunMoveSpeed
// - If BiohazardFog: ContaminationDuration must be > 0
// - If CognitiveStatic: InputDelaySeconds must be > 0 and < 0.5, HallucinationRate must be > 0
// - RequiredEquipmentTag or RequiredAbilityTag should be set (warning if both empty on non-HardGate)
// - HazardVFXPrefab should not be null (warning)
//
// Build-time scan: verify one asset per FrontRestriction enum value (10 total)
```

---

## Live Tuning

Live-tunable parameters via `FrontRestrictionDefinitionSO` inspector during play mode:
- DPS values per tier (ContestedDPS, HostileDPS, OverrunDPS)
- Movement speed multipliers per tier
- ContaminationDuration, InputDelaySeconds, HallucinationRate
- IsHardGate toggle

Change propagation: `FrontRestrictionTuningBridge` (managed system, PresentationSystemGroup) detects SO changes via dirty flag set in `OnValidate()`, rebuilds the `FrontRestrictionDatabase` blob, and replaces the singleton `BlobAssetReference`. Systems reading the blob pick up new values on next frame access.

---

## Debug Visualization

**Zone Restriction Overlay** (toggle via debug menu):
- Per-zone border rendering with restriction type icon at zone boundary
- Color-coded zone fill: safe zones transparent, restricted zones tinted by restriction type
- Player exposure HUD: real-time display of `PlayerFrontExposure` fields (ActiveRestriction, penalties, HazardDPS, ContaminationTimer)
- Warning zone preview: shaded area within 15m of restricted zone boundaries showing where warning triggers

**Activation**: Debug menu toggle `Front/Restrictions/ShowOverlay`
