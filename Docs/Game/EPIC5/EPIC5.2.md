# EPIC 5.2: Echo Encounters

**Status**: Planning
**Epic**: EPIC 5 — Echo Missions
**Dependencies**: EPIC 5.1; Framework: Quest/, AI/, Combat/

---

## Overview

Echoes are mechanically distinct from their originals — not just harder enemies but fundamentally different encounters. "Wrongness" manifests as new enemy variants, altered objectives, layout distortions, audio/visual anomalies, and temporal effects. Each district has its own echo flavor that defines how wrongness looks and feels.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Echo/Components/EchoEncounterComponents.cs
using Unity.Entities;

namespace Hollowcore.Echo
{
    /// <summary>
    /// Marks a zone as having an active echo encounter.
    /// Applied to zone entities when echo activates.
    /// </summary>
    public struct EchoZoneActive : IComponentData, IEnableableComponent
    {
        public int EchoId;
        public EchoMutationType MutationType;
        public float DifficultyMultiplier;
    }

    /// <summary>
    /// Applied to echo-spawned enemies. Modifies their behavior/appearance.
    /// </summary>
    public struct EchoEnemy : IComponentData
    {
        public int EchoId;
        public EchoMutationType SourceMutation;
        /// <summary>For TemporalAnomaly: can reset to full HP once after death.</summary>
        public bool HasDeathReset;
        public bool DeathResetUsed;
    }

    /// <summary>
    /// Echo objective override. Replaces the original quest objective.
    /// </summary>
    public struct EchoObjective : IComponentData
    {
        public int EchoId;
        public int OriginalQuestId;
        public EchoObjectiveType OverrideType;
    }

    public enum EchoObjectiveType : byte
    {
        Original = 0,    // Same objective, harder enemies
        Escort = 1,      // Rescue → escort to extraction
        Survive = 2,     // Kill → survive for duration
        Reverse = 3,     // Objective is inverted (destroy → protect, collect → disperse)
        Stealth = 4,     // Must complete without alerting echo entities
        Purge = 5        // Kill ALL echo entities in zone (no survivors)
    }
}
```

---

## Mutation Type Details

### EnemyUpgrade
- Original enemies replaced with elite/boss variants
- Additional enemy spawns (1.5x count)
- Enemies have echo visual shader (distortion, color shift)
- Aggro radius increased — echoes are aggressive

### MechanicChange
- Objective type changes per EchoObjectiveType table
- Timer may be added or removed
- Completion conditions altered
- Example: "Rescue the prisoner" → "Escort the prisoner through hostile echo zone"

### LayoutDistortion
- New environmental hazards in the zone
- Paths blocked/opened differently than original
- Environmental traps repositioned
- Zone feels familiar but wrong — uncanny valley for level design

### FactionSwap
- Original faction replaced by a different district's faction
- Cross-contamination feel: "Why are Cathedral enemies in the Necrospire?"
- Creates unfamiliar tactical situations in familiar spaces

### TemporalAnomaly
- Enemies reset to full HP once after death (EchoEnemy.HasDeathReset)
- Time distortion VFX (slowdown pockets, rewind particles)
- Objective may loop: complete once, then must complete again differently

---

## Systems

### EchoEncounterSpawnSystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoEncounterSpawnSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// When player enters a zone with EchoZoneActive:
//   1. Read EchoMissionEntry for this echo
//   2. Based on MutationType:
//      a. EnemyUpgrade: swap enemy prefabs for elite variants, add EchoEnemy tag
//      b. MechanicChange: replace quest objective, apply EchoObjective
//      c. LayoutDistortion: activate/deactivate zone hazard entities
//      d. FactionSwap: spawn different faction's enemies with EchoEnemy tag
//      e. TemporalAnomaly: spawn normal enemies with HasDeathReset = true
//   3. Apply EchoFlavorSO visual/audio overrides to zone
//   4. Scale all enemy stats by DifficultyMultiplier
```

### EchoDeathResetSystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoDeathResetSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// For TemporalAnomaly echoes: enemies reset once after death.
//
// For each EchoEnemy where HasDeathReset && !DeathResetUsed:
//   If entity enters DeathState:
//     1. Set DeathResetUsed = true
//     2. Reset Health to max
//     3. Clear DeathState
//     4. Play rewind VFX
//     5. Brief invulnerability window (0.5s)
```

### EchoCompletionSystem

```csharp
// File: Assets/Scripts/Echo/Systems/EchoCompletionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Detects echo mission completion.
//
// For each active echo:
//   Check completion condition (based on EchoObjectiveType):
//     - Purge: all EchoEnemy entities in zone dead
//     - Survive: timer expired with player alive
//     - Escort: escort target reached destination
//     - etc.
//   On completion:
//     1. Mark EchoMissionEntry.IsCompleted = true
//     2. Spawn echo rewards (EPIC 5.3)
//     3. Disable EchoZoneActive
//     4. Clear echo visual/audio overrides
//     5. If all echoes in district completed: disable HasActiveEchoes
```

---

## Setup Guide

1. Create echo-variant enemy prefabs (or runtime modifier system for echo visual shader)
2. Configure per-mutation-type encounter override templates
3. Create echo zone visual effects: distortion post-process, wrongness particles, audio reverb
4. Create EchoObjective quest variants for each base side goal
5. Temporal anomaly VFX: rewind particle effect, time-warp audio cue

---

## Verification

- [ ] EnemyUpgrade: enemies visually distinct, harder stats, more spawns
- [ ] MechanicChange: objective changes from original (e.g., rescue → escort)
- [ ] LayoutDistortion: zone paths/hazards differ from original visit
- [ ] FactionSwap: wrong faction's enemies appear in zone
- [ ] TemporalAnomaly: enemies reset once after death with rewind VFX
- [ ] Echo zone has visible/audible wrongness on approach
- [ ] Echo completion clears zone effects and marks mission done
- [ ] Difficulty scales correctly with DifficultyMultiplier

---

## Validation

```csharp
// File: Assets/Scripts/Echo/Definitions/EchoEncounterDefinitionSO.cs (OnValidate)
namespace Hollowcore.Echo.Definitions
{
    public partial class EchoEncounterDefinitionSO
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Every mutation type that can be assigned must have encounter data
            if (EnemyReplacements == null || EnemyReplacements.Count == 0)
                Debug.LogWarning($"[EchoEncounter] No enemy replacements defined for '{name}'", this);

            // Objective mutation must have valid EchoObjectiveType
            if (ObjectiveMutation != null && ObjectiveMutation.OverrideType == EchoObjectiveType.Original)
                Debug.LogWarning($"[EchoEncounter] ObjectiveMutation is Original — no actual mutation", this);

            // TemporalAnomaly encounters must reference enemies with DeathReset capability
            // LayoutDistortion encounters must have at least one hazard modifier
            // FactionSwap must reference a different faction than the district's primary
        }
#endif
    }
}
```

---

## Editor Tooling

```csharp
// File: Assets/Editor/EchoWorkstation/EchoEncounterPreview.cs
// Editor panel for previewing echo encounters before testing in-game:
//
// Features:
//   - Select base QuestDefinitionSO + EchoFlavorSO
//   - For each mutation type, shows side-by-side:
//     Left: original encounter (enemy list, objective, layout)
//     Right: echo encounter (replacements, mutations, modifications)
//   - Enemy diff: which enemies are swapped, stat multiplier applied
//   - Objective mutation: original type → echo type with description
//   - Layout changes: list of hazard additions/removals
//   - TemporalAnomaly: highlights which enemies get DeathReset
//   - "Preview in Scene" button: spawns echo encounter in a test scene (editor play mode)

// File: Assets/Editor/EchoWorkstation/EchoObjectiveMutationMatrix.cs
// Matrix view: rows = original quest objective types, columns = echo objective types
// Shows which mutations are configured, which are missing
// Highlights gaps where a quest type has no echo variant defined
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Echo/Components/EchoEncounterRuntimeConfig.cs
using Unity.Entities;

namespace Hollowcore.Echo
{
    /// <summary>
    /// Runtime-tunable encounter parameters for active echo zones.
    /// </summary>
    public struct EchoEncounterRuntimeConfig : IComponentData
    {
        /// <summary>Enemy spawn count multiplier for EnemyUpgrade type. Default 1.5.</summary>
        public float EnemyUpgradeSpawnMultiplier;

        /// <summary>TemporalAnomaly invulnerability window after death reset (seconds). Default 0.5.</summary>
        public float DeathResetInvulnSeconds;

        /// <summary>Whether LayoutDistortion can block the zone's primary path. Default true.</summary>
        public bool AllowPrimaryPathBlock;

        /// <summary>Echo aggro radius multiplier (echoes are more aggressive). Default 1.5.</summary>
        public float EchoAggroRadiusMultiplier;

        /// <summary>If true, echo enemies drop their normal loot in addition to echo rewards. Default false.</summary>
        public bool EchoEnemiesDropNormalLoot;

        public static EchoEncounterRuntimeConfig Default => new()
        {
            EnemyUpgradeSpawnMultiplier = 1.5f,
            DeathResetInvulnSeconds = 0.5f,
            AllowPrimaryPathBlock = true,
            EchoAggroRadiusMultiplier = 1.5f,
            EchoEnemiesDropNormalLoot = false,
        };
    }
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Echo/Debug/EchoEncounterDebugOverlay.cs
// In-game overlay for active echo encounter debugging:
//
//   - Echo zone boundary: tinted area overlay (purple translucent) covering echo zone bounds
//   - Enemy markers: echo enemies shown with mutation-type icon above health bar
//     - EnemyUpgrade: red up-arrow, MechanicChange: blue gear, FactionSwap: orange swap icon
//   - TemporalAnomaly: DeathReset status per enemy ("RESET AVAILABLE" / "RESET USED")
//   - Objective tracker: echo objective type + progress (e.g., "SURVIVE: 45s remaining")
//   - Wrongness intensity meter: 0-100% based on distance to zone center
//   - Layout changes: highlighted paths (blocked=red X, new=green arrow)
//   - Active modifier list: all stat multipliers applied to echo enemies
//   - Toggle: visible when inside echo zone in debug mode
```
