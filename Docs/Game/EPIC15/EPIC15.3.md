# EPIC 15.3: The Signal (Transmission Boss)

**Status**: Planning
**Epic**: EPIC 15 — Final Bosses & Endgame
**Dependencies**: EPIC 15.1 (InfluenceMeterState, faction selection); EPIC 14 (Boss Definition Template, Variant Clauses, Arena System); Framework: Combat/, AI/

---

## Overview

The Signal is the Transmission faction's final boss -- an AI that believes it can save humanity by absorbing every consciousness into its network. Triggered when the player has disrupted Transmission districts (Chrome Cathedral, Nursery, Synapse Row) more than the other factions. The arena is a network/digital space where signals become terrain and reality warps. The fight progresses from persuasion (the Signal tries to convince you to join) through force (direct absorption attempts) to desperation (lashing out with corrupted data). Variant clauses draw from transmission district side goals.

---

## Component Definitions

### SignalPhaseState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/FinalBoss/SignalComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss.FinalBoss
{
    public enum SignalPhase : byte
    {
        Persuasion = 0,   // Phase 1: tries to convince player — non-lethal attacks, dialogue-heavy
        Force = 1,         // Phase 2: tries to absorb player — digital attacks, possession mechanics
        Desperation = 2    // Phase 3: lashes out — corrupted data, reality tears
    }

    /// <summary>
    /// Signal-specific combat state layered on top of BossPhaseState.
    /// Tracks digital attack mechanics unique to this boss.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SignalPhaseState : IComponentData
    {
        [GhostField] public SignalPhase CurrentPhase;

        /// <summary>
        /// Signal strength (1.0 = full, 0.0 = weakened).
        /// Decreases when player destroys signal nodes. Low strength = weaker abilities.
        /// </summary>
        [GhostField(Quantization = 100)] public float SignalStrength;

        /// <summary>Number of active signal nodes in the arena (destroyable targets).</summary>
        [GhostField] public byte ActiveNodeCount;

        /// <summary>
        /// Player corruption level (0.0-1.0). Increases from absorption attacks.
        /// At 1.0: instant kill (absorbed). Decays slowly over time.
        /// </summary>
        [GhostField(Quantization = 100)] public float PlayerCorruption;

        /// <summary>Cooldown for the Signal's possession attempt ability.</summary>
        [GhostField(Quantization = 100)] public float PossessionCooldown;

        /// <summary>Whether the Signal is currently in a dialogue sequence (Phase 1).</summary>
        [GhostField] public bool InDialogue;
    }
}
```

### SignalNodeElement (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/FinalBoss/SignalComponents.cs (continued)
using Unity.Mathematics;

namespace Hollowcore.Boss.FinalBoss
{
    public enum SignalNodeType : byte
    {
        Relay = 0,          // Amplifies Signal's abilities — destroy to weaken
        Scrambler = 1,      // Causes UI distortion for player
        Absorber = 2,       // Pulls player toward it, increases corruption
        Projector = 3,      // Spawns holographic minions
        DataStream = 4      // Moving beam that deals damage on contact
    }

    /// <summary>
    /// Signal nodes scattered around the arena. Destroying them weakens the Signal.
    /// Buffer on the Signal boss entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SignalNodeElement : IBufferElementData
    {
        public SignalNodeType NodeType;
        public Entity WorldEntity;
        public float3 Position;
        public float Health;
        public float MaxHealth;
        public bool IsActive;

        /// <summary>Which cleared transmission district enhances this node type.</summary>
        public int EnhancedByDistrictId;
    }
}
```

---

## ScriptableObject Definitions

### SignalDefinitionSO

```csharp
// File: Assets/Scripts/Boss/Definitions/FinalBoss/SignalDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Boss.Definitions.FinalBoss
{
    [CreateAssetMenu(fileName = "TheSignal", menuName = "Hollowcore/Boss/Final/The Signal")]
    public class SignalDefinitionSO : BossDefinitionSO
    {
        [Header("Signal-Specific")]
        [Tooltip("Number of signal nodes per phase")]
        public List<PhaseNodeConfig> PhaseNodeConfigs = new();

        [Tooltip("Corruption decay rate per second when not being attacked")]
        public float CorruptionDecayRate = 0.03f;

        [Tooltip("Corruption threshold for UI distortion effects")]
        public float UIDistortionThreshold = 0.3f;

        [Tooltip("Corruption threshold for control inversion")]
        public float ControlInversionThreshold = 0.7f;

        [Tooltip("Persuasion dialogue sequences for Phase 1")]
        public List<SignalDialogueSequence> PersuasionDialogues = new();

        [Tooltip("How much each cleared transmission district boosts node types")]
        public float DistrictEnhancementBase = 0.25f;
    }

    [System.Serializable]
    public class PhaseNodeConfig
    {
        public SignalPhase Phase;
        public int NodeCount;
        public List<SignalNodeType> AvailableNodeTypes = new();
        public float PossessionCooldown = 8f;
    }

    [System.Serializable]
    public class SignalDialogueSequence
    {
        [TextArea] public string DialogueLine;
        [Tooltip("Attack pattern during this dialogue beat")]
        public BossAttackPatternSO AccompanyingAttack;
    }
}
```

---

## Systems

### SignalAISystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/SignalAISystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossEncounterSystem
//
// Boss AI for The Signal. Uses digital/psychic attacks and possession mechanics.
//
// Phase 1 — Persuasion:
//   1. Signal speaks to the player through dialogue sequences
//   2. Attacks are non-lethal but increase PlayerCorruption
//   3. Scrambler nodes distort player UI (HUD flickers, false health values)
//   4. Signal teleports between Relay nodes — cannot be directly attacked
//   5. Player must destroy Relay nodes to force Signal into physical form
//   6. Transition to Force at 65% health
//
// Phase 2 — Force:
//   1. Signal abandons persuasion — direct absorption attacks
//   2. Possession attempt: on cooldown, targets player
//      - Telegraph: player sees "CONNECTING..." UI overlay
//      - If not dodged: controls invert for 3 seconds + corruption spike
//      - Counter: destroy an Absorber node to interrupt
//   3. Projector nodes spawn holographic minions (visual clones of the Signal)
//      - Clones deal reduced damage but add corruption
//      - Destroying the Projector despawns all its clones
//   4. DataStream beams sweep across arena — dodge or take damage + corruption
//   5. Transition to Desperation at 30% health
//
// Phase 3 — Desperation:
//   1. Signal destabilizes — arena geometry begins warping
//   2. Reality tears: random sections of arena become damaging void zones
//   3. All remaining nodes activate at maximum intensity
//   4. Possession attempts no longer have cooldown — constant pressure
//   5. Corruption decay rate halved
//   6. Signal becomes physically vulnerable (no more node teleportation)
//   7. Narrative: "I only wanted to save everyone. You just wouldn't listen."
//
// Transmission district scaling:
//   - Cathedral cleared → Scrambler nodes cause more severe UI distortion
//   - Nursery cleared → Projector clones have more health
//   - Synapse cleared → Possession duration increased, corruption spike higher
//   - Narrative: Signal uses what you destroyed against you
```

### SignalCorruptionSystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/SignalCorruptionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: SignalAISystem
//
// Manages the player corruption mechanic.
//
// Each frame:
//   1. Read SignalPhaseState.PlayerCorruption
//   2. Apply decay: corruption -= CorruptionDecayRate * deltaTime (if not under attack)
//   3. Check thresholds:
//      a. >= UIDistortionThreshold (0.3): fire UIDistortionEvent
//         - HUD elements flicker, false health/ammo values shown briefly
//      b. >= ControlInversionThreshold (0.7): fire ControlInversionEvent
//         - Movement controls inverted for brief pulses
//      c. >= 1.0: instant death (absorbed by Signal)
//   4. Clamp corruption to [0.0, 1.0]
//   5. Fire CorruptionChangedEvent for UI corruption meter
```

### SignalNodeSystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/SignalNodeSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: SignalAISystem
//
// Manages signal node lifecycle and effects.
//
// For each SignalNodeElement:
//   1. If IsActive and Health > 0:
//      a. Execute node effect based on type (Relay/Scrambler/Absorber/Projector/DataStream)
//      b. Check node health — if damaged below 0, destroy:
//         - Set IsActive = false
//         - Decrement ActiveNodeCount
//         - Reduce SignalStrength proportionally
//         - Fire node destruction VFX
//   2. When ActiveNodeCount == 0 in a phase: Signal becomes directly targetable
//   3. On phase transition: spawn new nodes from PhaseNodeConfig
```

---

## Variant Clause Examples

| Clause | Trigger | Effect |
|---|---|---|
| Full Network Coverage | SideGoalSkipped (Cathedral) | +3 additional signal nodes in all phases |
| Nursery Protocol | SideGoalSkipped (Nursery) | Projector clones regenerate health |
| Synaptic Override | StrifeCard (Signal Schism) | Possession can target during dodge frames |
| Deep Transmission | FrontPhase >= 3 | +25% health, corruption decay halved |
| Cathedral Guard | TraceLevel >= 4 | Digital sentinel reinforcements at 50%/25% |
| Signal Jammer Token | CounterToken | Disables possession during Phase 1 |

---

## Setup Guide

1. Create SignalComponents.cs in `Assets/Scripts/Boss/Components/FinalBoss/`
2. Create SignalDefinitionSO asset in `Assets/Data/Boss/Final/TheSignal.asset`
3. Build arena subscene: Network Space
   - Central processing core (Signal starting position)
   - 6-8 signal node positions (Relay, Scrambler, Absorber, Projector, DataStream)
   - DataStream beam paths (rail entities with moving damage zones)
   - Reality tear spawn points (Phase 3 void zones)
   - Digital terrain platforms with signal-themed visual effects
4. Create corruption UI overlay:
   - Corruption meter (bar or radial, red gradient)
   - UI distortion shader for HUD flickering
   - "CONNECTING..." possession telegraph overlay
   - Control inversion indicator
5. Create holographic clone prefab (visual clone of Signal, reduced stats)
6. Configure phase-to-node mapping in SignalDefinitionSO
7. Wire SignalCorruptionSystem to player input for control inversion
8. Create persuasion dialogue sequences for Phase 1

---

## Verification

- [ ] Signal spawns when Transmission is dominant faction
- [ ] Phase 1: Signal speaks, attacks are non-lethal but corrupt
- [ ] Phase 1: Signal teleports between Relay nodes, not directly attackable
- [ ] Destroying all Relay nodes forces Signal into physical form
- [ ] Phase 1 → 2 transition at 65% health
- [ ] Phase 2: Possession attempts fire with telegraph
- [ ] Phase 2: Dodging possession prevents corruption spike
- [ ] Destroying Absorber node interrupts active possession
- [ ] Projector spawns holographic clones, destroying Projector despawns them
- [ ] DataStream beams sweep arena and deal damage + corruption
- [ ] Phase 2 → 3 transition at 30% health
- [ ] Phase 3: Arena warps, void zones appear
- [ ] Corruption at 0.3: UI distortion effects trigger
- [ ] Corruption at 0.7: Control inversion triggers
- [ ] Corruption at 1.0: Player death (absorbed)
- [ ] Corruption decays when not under attack
- [ ] District enhancement: clearing Cathedral boosts Scrambler distortion
- [ ] Variant clauses activate/deactivate correctly

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Boss/Authoring/FinalBoss/SignalBlobBaker.cs
namespace Hollowcore.Boss.Authoring.FinalBoss
{
    // SignalDefinitionSO extends BossDefinitionSO → BossBlob (14.1) + SignalBlob
    //
    // SignalBlob (additional blob on Signal boss entity):
    //   BlobArray<PhaseNodeBlob> PhaseNodeConfigs
    //     - SignalPhase Phase
    //     - int NodeCount
    //     - BlobArray<SignalNodeType> AvailableNodeTypes
    //     - float PossessionCooldown
    //   float CorruptionDecayRate
    //   float UIDistortionThreshold
    //   float ControlInversionThreshold
    //   float DistrictEnhancementBase
    //   BlobArray<SignalDialogueBlob> PersuasionDialogues
    //     - FixedString128Bytes DialogueLine
    //     - int AccompanyingAttackPatternId
    //
    // SignalNodeElement buffer bakes node type, position, MaxHealth, EnhancedByDistrictId from SO.
    // WorldEntity resolved at runtime via subscene entity map.
}
```

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/SignalValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Signal-specific validation (extends boss validation from 14.1):
    //
    // 1. Phase health thresholds: Persuasion→Force at ~65%, Force→Desperation at ~30%.
    //    Warning if thresholds deviate > 10% from design intent.
    //
    // 2. PhaseNodeConfigs must cover all three SignalPhase values.
    //    Error if any phase missing.
    //
    // 3. Corruption thresholds must be ordered:
    //    0 < UIDistortionThreshold < ControlInversionThreshold < 1.0.
    //    Error if out of order.
    //
    // 4. CorruptionDecayRate must be > 0 and < 0.5.
    //    Warning if decay is so fast corruption mechanic is trivial (> 0.2/s)
    //    or so slow it's unavoidable (< 0.01/s).
    //
    // 5. Node count per phase must be >= 1.
    //    Warning if Phase 3 has > 12 nodes (performance concern).
    //
    // 6. Persuasion dialogues: Phase 1 should have >= 3 dialogue sequences.
    //    Warning if < 3 (Phase 1 feels empty).
    //
    // 7. District enhancement: verify EnhancedByDistrictId values match valid
    //    transmission districts (Cathedral, Nursery, Synapse IDs).
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/SignalLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Signal live tuning (extends BossLiveTuning):
    //
    //   float PlayerCorruptionOverride    // -1 = normal, [0,1] = force corruption level
    //   float CorruptionDecayRateOverride // -1 = use baked, >0 = override
    //   float PossessionCooldownOverride  // -1 = use per-phase, >0 = global override
    //   bool DisableCorruptionDeath       // corruption clamped at 0.99 (can't die from absorption)
    //   bool DisableUIDistortion          // suppress HUD flickering effects
    //   bool DisableControlInversion      // suppress control inversion
    //   float SignalStrengthOverride      // -1 = normal, [0,1] = force signal strength
    //   bool ForceAllNodesActive          // prevent node destruction
    //   bool DisableDistrictScaling       // ignore cleared district enhancements
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/SignalDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Signal debug overlay (extends BossDebugOverlay):
    //
    // [1] Corruption Meter
    //     - Vertical bar: 0% (clear) → 100% (absorbed)
    //     - Horizontal markers at UIDistortionThreshold and ControlInversionThreshold
    //     - Decay rate displayed: "decay: -0.03/s"
    //     - Flash effect when corruption spikes
    //
    // [2] Signal Node Map
    //     - Arena minimap showing all signal node positions
    //     - Node icons by type (Relay, Scrambler, Absorber, Projector, DataStream)
    //     - HP bars per node, destroyed nodes grayed out
    //     - SignalStrength value: "Signal: 72%"
    //
    // [3] Possession Status
    //     - Cooldown timer for next possession attempt
    //     - "POSSESSION ACTIVE" warning when possession in progress
    //     - Duration remaining counter
    //
    // [4] Signal State Machine
    //     - Current SignalPhase and ActiveNodeCount
    //     - InDialogue flag status
    //     - Node teleportation target (which Relay the Signal is at)
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/BossWorkstation/SignalSimulator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Signal-specific simulation extensions:
    //
    // Additional simulation inputs:
    //   bool[] clearedTransmissionDistricts  // which of Cathedral/Nursery/Synapse were cleared
    //   float possessionDodgeRate            // probability of dodging possession attempt
    //   float nodeTargetingPriority          // 0 = ignore nodes, 1 = always prioritize nodes
    //
    // Additional outputs:
    //   float meanPeakCorruption            // highest corruption reached before kill/wipe
    //   float corruptionDeathRate           // fraction of deaths from corruption reaching 1.0
    //   float meanNodesDestroyedPerPhase
    //   float meanPossessionsPerFight       // how many times player was possessed
    //   float wipeRateFromCorruption        // wipes caused specifically by absorption
    //   float meanPhase1Duration            // how long persuasion phase lasts (dialogue pacing)
    //   float districtEnhancementImpact     // wipe rate delta: with vs without enhancements
    //
    // Key balance questions answered:
    //   "Is corruption decay rate sufficient to make the mechanic tense but survivable?"
    //   "Does Phase 1 last long enough for dialogue to feel meaningful?"
    //   "Are nodes destroyable quickly enough to weaken the Signal before Phase 3?"
}
