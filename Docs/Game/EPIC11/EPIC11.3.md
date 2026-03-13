# EPIC 11.3: Live Rival Encounters

**Status**: Planning
**Epic**: EPIC 11 — Rival Operators
**Priority**: Medium — High-impact emergent moments
**Dependencies**: EPIC 11.1 (Rival Definition & Simulation); Framework: AI/, Dialogue/, Trading/; Optional: EPIC 8 (Trace for hostile triggers), EPIC 10 (trade rewards)

---

## Overview

When the player enters a district containing a living rival team, there is a probability-based chance of a live encounter. Encounters fall into three categories: Neutral (trade, intel sharing, body shop services), Competitive (racing for objectives, territory disputes, loot conflicts), and Hostile (contracted hunts at high Trace, desperate last-stand attacks). The encounter type is determined by rival personality, player Trace level, and contextual factors. Dialogue integration uses the existing Dialogue/ framework for negotiation and trade conversations. Combat encounters use the AI/ framework for real-time NPC combat behavior.

---

## Component Definitions

### EncounterType Enum

```csharp
// File: Assets/Scripts/Rivals/Components/RivalEncounterComponents.cs
namespace Hollowcore.Rivals
{
    public enum EncounterType : byte
    {
        // Neutral
        Trade = 0,              // Swap limbs, ammo, currency
        Intel = 1,              // Share Front status, district info → updates Scar Map
        BodyShop = 2,           // Rival medic offers revival services for a price

        // Competitive
        Race = 3,               // Both teams want same echo reward or boss token
        Territory = 4,          // Rival claims a vendor or safe zone
        LootConflict = 5,       // They found your old body first and took your gear

        // Hostile
        Contracted = 6,         // Paid to hunt you (Trace 4+)
        Desperate = 7           // Low resources, attacking out of desperation
    }

    public enum EncounterPhase : byte
    {
        None = 0,               // No active encounter
        Approach = 1,           // Rival spotted, not yet engaged
        Dialogue = 2,           // In conversation (trade/negotiation)
        Combat = 3,             // Fighting
        Resolution = 4,         // Encounter outcome being applied
        Complete = 5            // Encounter finished, cleanup
    }
}
```

### RivalEncounterState (IComponentData)

Tracks the active encounter between player and a rival team.

```csharp
// File: Assets/Scripts/Rivals/Components/RivalEncounterComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Singleton component tracking the current live rival encounter.
    /// Only one encounter active at a time. Created by RivalEncounterSystem,
    /// consumed by dialogue/combat/resolution systems.
    /// </summary>
    public struct RivalEncounterState : IComponentData
    {
        /// <summary>Entity carrying the RivalSimState for the encountered team.</summary>
        public Entity RivalEntity;

        /// <summary>Resolved encounter type.</summary>
        public EncounterType Type;

        /// <summary>Current phase of the encounter.</summary>
        public EncounterPhase Phase;

        /// <summary>Rival definition ID for SO lookup.</summary>
        public int RivalDefinitionId;

        /// <summary>District where encounter is happening.</summary>
        public int DistrictId;

        /// <summary>Zone where encounter spawns.</summary>
        public int ZoneId;

        /// <summary>Dialogue tree ID for this encounter (from RivalOperatorSO).</summary>
        public int DialogueTreeId;

        /// <summary>Whether player initiated combat (vs rival initiated).</summary>
        public bool PlayerInitiatedHostility;

        /// <summary>Elapsed time in current phase (for timeouts).</summary>
        public float PhaseTimer;

        /// <summary>Cached team name for UI display.</summary>
        public FixedString64Bytes RivalTeamName;
    }
}
```

### RivalEncounterConfig (IComponentData)

```csharp
// File: Assets/Scripts/Rivals/Components/RivalEncounterComponents.cs
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Singleton tuning for encounter generation.
    /// </summary>
    public struct RivalEncounterConfig : IComponentData
    {
        /// <summary>Base probability of encounter when rival is alive in same district.</summary>
        public float BaseEncounterChance;

        /// <summary>Trace level at which hostile encounters become possible.</summary>
        public int HostileTraceThreshold;

        /// <summary>Probability multiplier for hostile encounter per Trace level above threshold.</summary>
        public float HostileChancePerTrace;

        /// <summary>Member count below which rival becomes Desperate.</summary>
        public int DesperateThreshold;

        /// <summary>Rival surviving member ratio below which they flee instead of fight.</summary>
        public float FleeThreshold;

        /// <summary>Max seconds before unresolved encounter times out.</summary>
        public float EncounterTimeout;
    }
}
```

### RivalTradeOffer (IBufferElementData)

```csharp
// File: Assets/Scripts/Rivals/Components/RivalEncounterComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Items a rival is willing to trade during a Trade encounter.
    /// Generated by encounter system based on rival's equipment and personality.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct RivalTradeOffer : IBufferElementData
    {
        /// <summary>Item definition ID being offered.</summary>
        public int ItemId;

        /// <summary>Quantity available.</summary>
        public int Quantity;

        /// <summary>Price in primary currency.</summary>
        public int Price;

        /// <summary>Display name for UI.</summary>
        public FixedString64Bytes ItemName;
    }
}
```

---

## Systems

### RivalEncounterSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/RivalEncounterSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: TrailMarkerSystem (EPIC 11.2)
//
// Encounter trigger logic — runs on district entry:
//   1. Check if any alive rival occupies the same district as the player
//   2. If yes, roll encounter probability:
//      - Base: BaseEncounterChance (default 0.4)
//      - Modifier: +0.1 per rival district visit overlap (they've been here a while)
//      - Modifier: +0.15 if rival personality is Aggressive
//      - Modifier: -0.1 if rival personality is Cautious
//   3. If encounter triggers, determine type:
//      a. Check hostile first:
//         - Trace >= HostileTraceThreshold → Contracted (probability scales with Trace)
//         - SurvivingMembers <= DesperateThreshold → Desperate
//      b. If not hostile, check competitive:
//         - Both in same zone with active objective → Race
//         - Rival occupying vendor/safe zone → Territory
//         - Rival has looted player's previous body → LootConflict
//      c. Default to neutral:
//         - Mercantile personality → Trade
//         - Else weighted random: Trade(40%), Intel(40%), BodyShop(20%)
//   4. Create RivalEncounterState singleton with Phase=Approach
//   5. Spawn rival NPC entities at encounter zone for visual/combat
//   6. Fire RivalEncounterStartedEvent
```

### RivalEncounterDialogueSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/RivalEncounterDialogueSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: RivalEncounterSystem
//
// Manages dialogue phase of non-combat encounters:
//   1. When Phase == Approach and Type is neutral/competitive:
//      a. Resolve DialogueTreeId from RivalOperatorSO (NeutralDialogueId or HostileDialogueId)
//      b. Initiate dialogue via Dialogue/ framework (DialogueStartRequest)
//      c. Transition Phase → Dialogue
//   2. During Dialogue phase:
//      a. Monitor dialogue events for trade/negotiate/refuse outcomes
//      b. Trade outcome → populate RivalTradeOffer buffer, open Trading/ UI
//      c. Intel outcome → reveal rival's district history on Scar Map
//      d. BodyShop outcome → open revival services menu
//      e. Refuse outcome → rival leaves (Phase → Resolution)
//      f. Attack outcome → Phase → Combat (PlayerInitiatedHostility = true)
//   3. Handle dialogue completion → Phase → Resolution
```

### RivalEncounterCombatSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/RivalEncounterCombatSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: RivalEncounterDialogueSystem
//
// Manages combat phase of hostile encounters:
//   1. When Phase == Approach and Type is hostile (Contracted/Desperate):
//      a. Spawn rival combat entities using AI/ framework
//      b. Set AI behavior from RivalOperatorSO.CombatBehaviorId
//      c. Transition Phase → Combat
//   2. During Combat phase:
//      a. Monitor rival NPC health — if all dead: player wins
//      b. Monitor flee condition: if surviving ratio < FleeThreshold, rivals retreat
//      c. Track combat timer for timeout
//   3. On resolution:
//      a. Player wins: loot rival bodies, update RivalSimState (member loss or wipe)
//      b. Rivals flee: they move to adjacent district in sim
//      c. Timeout: rivals disengage, no loot
//   4. Phase → Resolution
```

### RivalEncounterResolutionSystem

```csharp
// File: Assets/Scripts/Rivals/Systems/RivalEncounterResolutionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: RivalEncounterCombatSystem
//
// Applies encounter outcomes:
//   1. When Phase == Resolution:
//      a. Trade: finalize inventory changes via Trading/ framework
//      b. Intel: write revealed markers to ScarMapState (EPIC 12.1)
//      c. BodyShop: process revival if purchased
//      d. Race: award or deny objective based on outcome
//      e. Combat win: spawn loot from defeated rivals, update RivalSimState
//      f. Combat loss/flee: rival escapes, possible Trace increase
//   2. Clean up spawned rival NPC entities
//   3. Destroy RivalEncounterState singleton
//   4. Phase → Complete
//   5. Fire RivalEncounterCompletedEvent for analytics
```

---

## Setup Guide

1. **Add RivalEncounterComponents.cs** to `Assets/Scripts/Rivals/Components/`
2. **Add RivalEncounterConfigAuthoring** to expedition manager prefab:
   - BaseEncounterChance: 0.4
   - HostileTraceThreshold: 4
   - HostileChancePerTrace: 0.15
   - DesperateThreshold: 1
   - FleeThreshold: 0.3
   - EncounterTimeout: 120.0
3. **Create rival NPC prefabs**: `Assets/Prefabs/Rivals/RivalNPC_Heavy.prefab`, `RivalNPC_Stealth.prefab`, etc.
   - Each has AI/ components (AIBrain, AIState) configured for rival combat behavior
   - Attach visual meshes appropriate to BuildStyle
4. **Create dialogue trees** for each RivalOperatorSO in `Assets/Data/Dialogue/Rivals/`:
   - NeutralDialogue: branches for Trade, Intel, BodyShop, Refuse, Attack
   - HostileDialogue: threat/demand dialogue before combat
5. **Wire Dialogue/ integration**: RivalEncounterDialogueSystem references dialogue tree IDs from SO
6. **Wire Trading/ integration**: RivalTradeOffer buffer feeds into existing trade UI
7. **Add assembly references** to `Hollowcore.Dialogue`, `Hollowcore.Trading`, `Hollowcore.AI`

---

## Verification

- [ ] Encounter triggers with correct probability on district entry when rival is present
- [ ] Encounter type correctly resolved based on Trace, personality, and context
- [ ] Hostile encounters only trigger at Trace >= 4
- [ ] Desperate encounters trigger when rival member count is low
- [ ] Neutral encounters open dialogue via Dialogue/ framework
- [ ] Trade encounters populate RivalTradeOffer buffer and open trade UI
- [ ] Intel encounters reveal rival district history on Scar Map
- [ ] Combat encounters spawn rival NPCs with correct AI behavior
- [ ] Rival NPCs flee when surviving ratio drops below FleeThreshold
- [ ] Combat victory produces loot and updates RivalSimState
- [ ] Encounter timeout resolves correctly after EncounterTimeout seconds
- [ ] Only one encounter active at a time (singleton pattern)
- [ ] Spawned rival NPC entities cleaned up on encounter completion
- [ ] RivalEncounterCompletedEvent fires for analytics
- [ ] Player-initiated hostility during dialogue transitions correctly to combat

---

## BlobAsset Pipeline

RivalEncounterConfig is already a singleton IComponentData. Encounter probability tables benefit from blob storage for Burst-compatible weighted selection.

```csharp
// File: Assets/Scripts/Rivals/Blobs/RivalEncounterConfigBlob.cs
using Unity.Entities;

namespace Hollowcore.Rivals
{
    /// <summary>
    /// Blob for encounter type probability weights per personality.
    /// Indexed by (int)RivalPersonality, contains weighted distribution of EncounterType.
    /// </summary>
    public struct EncounterProbabilityBlob
    {
        public RivalPersonality Personality;
        /// <summary>8 floats indexed by (int)EncounterType. Normalized weights.</summary>
        public BlobArray<float> TypeWeights;
    }

    public struct EncounterProbabilityDatabase
    {
        /// <summary>5 entries indexed by (int)RivalPersonality.</summary>
        public BlobArray<EncounterProbabilityBlob> PerPersonality;
    }

    public struct EncounterProbabilityDatabaseRef : IComponentData
    {
        public BlobAssetReference<EncounterProbabilityDatabase> Value;
    }
}
```

---

## Validation

```csharp
// File: Assets/Scripts/Rivals/Components/RivalEncounterComponents.cs (append validation)
// Add to RivalEncounterConfigAuthoring MonoBehaviour:

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (BaseEncounterChance < 0f || BaseEncounterChance > 1f)
            Debug.LogError($"[RivalEncounterConfig] BaseEncounterChance must be 0-1.", this);
        if (HostileTraceThreshold < 1)
            Debug.LogWarning($"[RivalEncounterConfig] HostileTraceThreshold < 1 means hostiles at any Trace.", this);
        if (HostileChancePerTrace < 0f)
            Debug.LogError($"[RivalEncounterConfig] HostileChancePerTrace cannot be negative.", this);
        if (FleeThreshold <= 0f || FleeThreshold > 1f)
            Debug.LogError($"[RivalEncounterConfig] FleeThreshold must be (0,1].", this);
        if (EncounterTimeout < 30f)
            Debug.LogWarning($"[RivalEncounterConfig] EncounterTimeout < 30s may feel too rushed.", this);

        // Validate dialogue tree references exist for each rival in pool
        // (cross-reference with dialogue asset database at build time — see RivalBuildValidator)
    }
#endif
```

---

## Editor Tooling — Encounter Behavior Preview

Integrated into the Rival Operator Designer (EPIC 11.1). When a rival is selected, the Encounter tab shows:
- **Probability breakdown**: given current Trace level slider (0-5), show pie chart of encounter type probabilities
- **Dialogue flow preview**: tree visualization of neutral/hostile dialogue branches with outcome labels
- **Trade offer preview**: given equipment tier, show expected trade inventory (items, prices)
- **Combat outcome estimator**: player tier vs rival tier → win/flee/timeout probability bar

---

## Live Tuning

```csharp
// File: Assets/Scripts/Rivals/Debug/EncounterLiveTuning.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Runtime-tunable encounter parameters:
    ///   - RivalEncounterConfig.BaseEncounterChance (float, 0-1)
    ///   - RivalEncounterConfig.HostileTraceThreshold (int)
    ///   - RivalEncounterConfig.HostileChancePerTrace (float)
    ///   - RivalEncounterConfig.DesperateThreshold (int)
    ///   - RivalEncounterConfig.FleeThreshold (float)
    ///   - RivalEncounterConfig.EncounterTimeout (float)
    ///
    /// Pattern: EncounterLiveTuningSystem writes to RivalEncounterConfig singleton.
    /// Changes apply on next district entry encounter roll.
    /// Console: /tune encounter.chance 0.6, /tune encounter.hostile_trace 3
    /// </summary>
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Rivals/Debug/EncounterDebugOverlay.cs
namespace Hollowcore.Rivals.Debug
{
    /// <summary>
    /// Debug overlay for rival encounters (development builds):
    /// - Encounter trigger radius: when in rival's district, show sphere gizmo
    ///   at rival spawn zone with radius proportional to encounter probability
    /// - Encounter state HUD: current phase (Approach/Dialogue/Combat/Resolution)
    ///   + timer + rival team info
    /// - Aggression meter: bar showing rival's current hostility level
    ///   (affected by Trace, personality, surviving members)
    /// - Dialogue path highlight: during dialogue phase, show which branch
    ///   player is on and available outcomes
    /// - Combat stats: during combat phase, show rival NPC health bars,
    ///   AI state, flee threshold indicator
    /// - Toggle: /debug rivals encounters
    /// </summary>
}
```

---

## Simulation & Testing

Encounter testing is part of the Rival Simulation Tester (EPIC 11.1). Specific metrics for 11.3:

- **Encounter frequency distribution**: across 1000 expeditions, histogram of encounters per run. Target: mean ~2 encounters per 5-district expedition, with high variance (0-4 range)
- **Type distribution**: percentage breakdown of Trade/Intel/BodyShop/Race/Territory/LootConflict/Contracted/Desperate across all triggered encounters. Validate hostile types only appear when Trace conditions met
- **Player-vs-rival win rate projections**: for each equipment tier matchup (player 1-5 vs rival 1-5), estimate combat outcome probability using AI combat heuristic. Highlight matchups where win rate < 30% (frustrating) or > 90% (trivial)
- **Encounter duration analysis**: average time spent in each phase. Validate dialogue encounters resolve in 30-60s, combat in 30-120s
- **Trade value analysis**: average value of rival trade offers vs player inventory value. Ensures trades feel fair (neither exploitative nor charity)
