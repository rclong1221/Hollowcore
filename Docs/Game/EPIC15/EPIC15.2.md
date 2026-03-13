# EPIC 15.2: The Architect (Infrastructure Boss)

**Status**: Planning
**Epic**: EPIC 15 — Final Bosses & Endgame
**Dependencies**: EPIC 15.1 (InfluenceMeterState, faction selection); EPIC 14 (Boss Definition Template, Variant Clauses, Arena System); Framework: Combat/, AI/

---

## Overview

The Architect is the Infrastructure faction's final boss -- a city planner who fights by weaponizing the city itself. Triggered when the player has disrupted Infrastructure districts (Lattice, Burn, Auction) more than the other factions. The arena is the city's control room where walls move, floors shift, and infrastructure becomes a weapon. The fight progresses from defensive environmental control through offensive infrastructure attacks to a desperate phase where the city begins destroying itself. Variant clauses draw from infrastructure district side goals.

---

## Component Definitions

### ArchitectPhaseState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/FinalBoss/ArchitectComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss.FinalBoss
{
    public enum ArchitectPhase : byte
    {
        Defensive = 0,     // Phase 1: environmental control — uses city systems as shields
        Offensive = 1,     // Phase 2: infrastructure weaponized — conveyors, vents, collapse
        Desperate = 2      // Phase 3: city self-destructs — everything is lethal
    }

    /// <summary>
    /// Architect-specific combat state layered on top of BossPhaseState.
    /// Tracks environmental control mechanics unique to this boss.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ArchitectPhaseState : IComponentData
    {
        [GhostField] public ArchitectPhase CurrentPhase;

        /// <summary>Number of active infrastructure systems the Architect is controlling.</summary>
        [GhostField] public byte ActiveSystemCount;

        /// <summary>Bitmask of infrastructure systems currently weaponized (up to 8).</summary>
        [GhostField] public byte WeaponizedSystemsMask;

        /// <summary>
        /// Structural integrity of the control room (1.0 = intact, 0.0 = collapsing).
        /// Decreases in Desperate phase. At 0, arena becomes fully lethal.
        /// </summary>
        [GhostField(Quantization = 100)] public float StructuralIntegrity;

        /// <summary>Cooldown for the Architect's infrastructure command ability.</summary>
        [GhostField(Quantization = 100)] public float CommandCooldown;
    }
}
```

### ArchitectInfraSystem (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/FinalBoss/ArchitectComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Boss.FinalBoss
{
    public enum InfraSystemType : byte
    {
        ConveyorTrap = 0,     // Conveyor belts push player into hazards
        HeatVent = 1,         // Directional heat blasts
        StructuralCollapse = 2, // Ceiling/wall sections fall
        ShieldWall = 3,        // Movable walls for Architect cover
        FloorPanel = 4,        // Floor opens/closes over pits
        PistonStrike = 5,      // Piston extends for crushing damage
        SlagFlood = 6,         // Molten slag floods arena sections
        PowerSurge = 7         // Electrical grid activates in zones
    }

    /// <summary>
    /// Each infrastructure system the Architect can control.
    /// Buffer on the Architect boss entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ArchitectInfraSystem : IBufferElementData
    {
        public InfraSystemType SystemType;
        public Entity ArenaEntity; // The arena entity this system controls

        /// <summary>Whether the Architect has activated this system.</summary>
        public bool IsActive;

        /// <summary>Damage per activation (scales with boss phase).</summary>
        public float BaseDamage;

        /// <summary>Activation pattern timer.</summary>
        public float CycleTimer;
        public float CycleDuration;

        /// <summary>
        /// Which cleared infrastructure districts enhance this system.
        /// If player cleared Burn, ConveyorTrap and HeatVent are stronger.
        /// </summary>
        public int EnhancedByDistrictId;
        public float EnhancementMultiplier;
    }
}
```

---

## ScriptableObject Definitions

### ArchitectDefinitionSO

```csharp
// File: Assets/Scripts/Boss/Definitions/FinalBoss/ArchitectDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Boss.Definitions.FinalBoss
{
    [CreateAssetMenu(fileName = "TheArchitect", menuName = "Hollowcore/Boss/Final/The Architect")]
    public class ArchitectDefinitionSO : BossDefinitionSO
    {
        [Header("Architect-Specific")]
        [Tooltip("Which infrastructure systems are available per phase")]
        public List<PhaseInfraConfig> PhaseInfraConfigs = new();

        [Tooltip("Structural integrity drain rate in Desperate phase (per second)")]
        public float IntegrityDrainRate = 0.02f;

        [Tooltip("How much each cleared infra district boosts infrastructure systems")]
        public float DistrictEnhancementBase = 0.25f;

        [Tooltip("Dialogue lines keyed to infrastructure districts the player cleared")]
        public List<ArchitectDistrictDialogue> DistrictDialogues = new();
    }

    [System.Serializable]
    public class PhaseInfraConfig
    {
        public ArchitectPhase Phase;
        [Tooltip("Which InfraSystemType indices are active in this phase")]
        public List<InfraSystemType> ActiveSystems = new();
        [Tooltip("Command cooldown override for this phase")]
        public float CommandCooldown = 5f;
    }

    [System.Serializable]
    public class ArchitectDistrictDialogue
    {
        public int DistrictId;
        [TextArea] public string DialogueLine;
    }
}
```

---

## Systems

### ArchitectAISystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/ArchitectAISystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossEncounterSystem
//
// Boss AI for The Architect. Uses infrastructure systems as primary attack vector.
//
// Phase 1 — Defensive:
//   1. Architect stays behind ShieldWalls, repositioning them
//   2. Activates ConveyorTraps to push player into hazard zones
//   3. HeatVents fire periodically in player's direction
//   4. Architect has low direct damage — relies on environment
//   5. Player must destroy ShieldWalls to expose Architect
//   6. Transition to Offensive at 65% health
//
// Phase 2 — Offensive:
//   1. All infrastructure systems become attack-capable
//   2. PistonStrike targets player position with 1.5s telegraph
//   3. FloorPanels open in sequence, forcing constant movement
//   4. SlagFlood begins filling low areas — player must stay on high ground
//   5. Architect gains direct melee attack (industrial arm slam)
//   6. Transition to Desperate at 30% health
//
// Phase 3 — Desperate:
//   1. StructuralIntegrity begins draining (city destroying itself)
//   2. ALL systems activate simultaneously at max intensity
//   3. StructuralCollapse becomes continuous — arena shrinks
//   4. PowerSurge electrifies random floor sections
//   5. Architect moves erratically, gaining speed but losing accuracy
//   6. Race against StructuralIntegrity reaching 0 (wipe if arena collapses)
//
// Infrastructure district scaling:
//   - Each cleared infra district (Lattice, Burn, Auction) enhances specific systems
//   - Burn cleared → HeatVent and SlagFlood deal 25% more damage
//   - Lattice cleared → StructuralCollapse and PistonStrike have shorter telegraph
//   - Auction cleared → PowerSurge covers larger area
//   - Narrative: "You destroyed my Burn district? Let me show you what it could REALLY do."
```

### ArchitectInfraControlSystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/ArchitectInfraControlSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ArchitectAISystem
//
// Drives the arena's infrastructure systems based on Architect commands.
//
// For each ArchitectInfraSystem element in the boss entity buffer:
//   1. If IsActive:
//      a. Update CycleTimer
//      b. On cycle trigger: execute system effect via ArenaHazardSystem (EPIC 14.3)
//      c. Apply EnhancementMultiplier if player cleared the linked district
//   2. Check WeaponizedSystemsMask against ArchitectPhaseState phase
//   3. Activate/deactivate systems based on phase config
//
// Infrastructure command (on CommandCooldown):
//   1. Architect targets a system to activate/reconfigure
//   2. Visual telegraph (control panel glow, warning klaxon)
//   3. After telegraph: system activates with full effect
```

---

## Variant Clause Examples

| Clause | Trigger | Effect |
|---|---|---|
| Full Infrastructure Network | SideGoalSkipped (Lattice) | +2 additional infrastructure systems active in Phase 1 |
| Emergency Protocols | SideGoalSkipped (Burn) | ShieldWalls regenerate after destruction |
| Market Leverage | StrifeCard (Economic Pressure) | Architect summons merc reinforcements |
| Deep Foundations | FrontPhase >= 3 | +25% health, StructuralIntegrity drains 50% slower |
| Warden Garrison | TraceLevel >= 4 | Warden reinforcements at 50% and 25% health |
| Lattice Override Key | CounterToken | Disables StructuralCollapse telegraph reduction |

---

## Setup Guide

1. Create `Assets/Scripts/Boss/Components/FinalBoss/` subfolder
2. Create ArchitectComponents.cs with ArchitectPhaseState and ArchitectInfraSystem
3. Create ArchitectDefinitionSO asset in `Assets/Data/Boss/Final/TheArchitect.asset`
4. Build arena subscene: City Control Room
   - Central control platform (Architect starting position)
   - 4 ShieldWall entities (movable, destructible)
   - ConveyorTrap lane entities with directional movement
   - HeatVent entities with cone damage zones
   - Floor panel entities (open/close states)
   - Piston entities with telegraph visual
   - SlagFlood volume with rising level
   - PowerSurge grid overlay
5. Configure phase-to-system mapping in ArchitectDefinitionSO
6. Create variant clauses for infrastructure districts
7. Wire ArchitectAISystem to framework AI/ behavior tree pattern
8. Test district enhancement scaling (clear Burn, verify HeatVent damage boost)

---

## Verification

- [ ] Architect spawns when Infrastructure is dominant faction
- [ ] Phase 1: ShieldWalls protect Architect, ConveyorTraps push player
- [ ] Phase 1 → 2 transition at 65% health
- [ ] Phase 2: all infrastructure systems weaponized
- [ ] Phase 2 → 3 transition at 30% health
- [ ] Phase 3: StructuralIntegrity drains, arena shrinks
- [ ] Phase 3: wipe condition if StructuralIntegrity reaches 0
- [ ] Infrastructure systems damage both player AND Architect
- [ ] District enhancement: clearing Burn boosts HeatVent/SlagFlood
- [ ] Variant clauses activate/deactivate correctly
- [ ] Counter tokens disable correct clauses
- [ ] CommandCooldown respects per-phase configuration
- [ ] Architect dialogue references cleared districts
- [ ] Victory triggers reward sequence (EPIC 14.5 / 15.5)

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Boss/Authoring/FinalBoss/ArchitectBlobBaker.cs
namespace Hollowcore.Boss.Authoring.FinalBoss
{
    // ArchitectDefinitionSO extends BossDefinitionSO → BossBlob (14.1) + ArchitectBlob
    //
    // ArchitectBlob (additional blob on Architect boss entity):
    //   BlobArray<PhaseInfraBlob> PhaseInfraConfigs
    //     - ArchitectPhase Phase
    //     - BlobArray<InfraSystemType> ActiveSystems
    //     - float CommandCooldown
    //   float IntegrityDrainRate
    //   float DistrictEnhancementBase
    //   BlobArray<ArchitectDialogueBlob> DistrictDialogues
    //     - int DistrictId
    //     - FixedString128Bytes DialogueLine
    //
    // InfraSystem buffer elements bake BaseDamage, CycleDuration, EnhancedByDistrictId,
    // EnhancementMultiplier from SO. ArenaEntity resolved at runtime via subscene entity map.
}
```

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/ArchitectValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Architect-specific validation (extends boss validation from 14.1):
    //
    // 1. Phase health thresholds: Defensive→Offensive at ~65%, Offensive→Desperate at ~30%.
    //    Warning if thresholds deviate > 10% from design intent (configurable tolerance).
    //
    // 2. PhaseInfraConfigs must cover all three ArchitectPhase values.
    //    Error if any phase missing from config list.
    //
    // 3. Infrastructure system coverage:
    //    Every InfraSystemType referenced in PhaseInfraConfigs must have a matching
    //    ArchitectInfraSystem buffer element in the arena subscene.
    //    Warning if system referenced but no arena entity backs it.
    //
    // 4. IntegrityDrainRate validation:
    //    At drain rate, Phase 3 should last at least 30s: 1.0 / IntegrityDrainRate >= 30.
    //    Warning if < 30s (fight ends too abruptly) or > 120s (trivial DPS check).
    //
    // 5. District enhancement: verify EnhancedByDistrictId values match valid infra districts
    //    (Lattice, Burn, Auction IDs). Error on unknown district ID.
    //
    // 6. EnhancementMultiplier in [1.0, 2.0]. Warning if > 1.5 (large spike from one district).
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/ArchitectLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Architect live tuning (extends BossLiveTuning):
    //
    //   float StructuralIntegrityOverride  // -1 = normal drain, [0,1] = force value
    //   float IntegrityDrainRateOverride   // -1 = use baked, >0 = override
    //   float CommandCooldownOverride      // -1 = use per-phase, >0 = global override
    //   bool ForceAllInfraSystems          // activate all 8 systems regardless of phase
    //   bool DisableInfraDistrictScaling   // ignore cleared district enhancements
    //   float InfraDamageMultiplier        // global scale on infrastructure system damage (default 1.0)
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/ArchitectDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Architect debug overlay (extends BossDebugOverlay):
    //
    // [1] Infrastructure System Status
    //     - Grid of 8 system icons (ConveyorTrap, HeatVent, etc.)
    //     - Active = bright, Inactive = dim, Weaponized = red border
    //     - CycleTimer shown as circular progress per system
    //     - Enhanced systems have district icon overlay (Burn flame, Lattice grid, Auction gavel)
    //
    // [2] Structural Integrity Gauge
    //     - Vertical bar showing 1.0 → 0.0
    //     - Drain rate displayed as "X per second"
    //     - Time remaining estimate: "Arena collapse in Xs"
    //     - Flashes red below 0.3
    //
    // [3] ShieldWall Health
    //     - Mini HP bars for each ShieldWall entity
    //     - Position on arena minimap
    //
    // [4] Command Cooldown
    //     - Circular cooldown indicator for Architect's infrastructure command
    //     - Shows which system will be targeted next (AI decision preview)
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/BossWorkstation/ArchitectSimulator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Architect-specific simulation extensions:
    //
    // Additional simulation inputs:
    //   bool[] clearedInfraDistricts   // which of Lattice/Burn/Auction were cleared
    //   float hazardAvoidanceRate      // probability of avoiding infrastructure system damage
    //
    // Additional outputs:
    //   float meanStructuralIntegrityAtKill  // how much arena was left when boss died
    //   float wipeRateFromArenaCollapse      // fraction of wipes from integrity reaching 0
    //   float[] infraSystemDamageContribution // % of total player damage from each system type
    //   float meanShieldWallDestroyTime       // how long Phase 1 lasts due to shield wall HP
    //   float districtEnhancementImpact       // wipe rate delta: with vs without enhancements
    //
    // Key balance questions answered:
    //   "Is Phase 3 survivable at minimum DPS before integrity reaches 0?"
    //   "Does clearing Burn make HeatVent damage oppressive?"
    //   "Are ShieldWalls destroyable in reasonable time?"
}
