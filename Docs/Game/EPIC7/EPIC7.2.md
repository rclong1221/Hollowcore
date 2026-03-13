# EPIC 7.2: Strife Application System

**Status**: Planning
**Epic**: EPIC 7 — Strife System
**Priority**: High — Core runtime pipeline for Strife effects
**Dependencies**: EPIC 7.1 (Strife Card Data Model); Framework: Roguelite/RunModifierStack; Optional: EPIC 4 (Districts), EPIC 3 (Front modifiers)

---

## Overview

Translates the selected Strife card into live gameplay modifiers. On expedition start, a seed-deterministic selection picks one of the 12 cards. The card's Map Rule, Enemy Mutation, and Boss Clause are decomposed into RunModifierStack entries and applied through the existing modifier pipeline. In higher ascension loops, the card rotates every 2 maps (no repeats within an expedition), creating compounding modifier pressure.

---

## Component Definitions

### ActiveStrifeState (IComponentData)

```csharp
// File: Assets/Scripts/Strife/Components/ActiveStrifeState.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Singleton tracking the currently active Strife card for the expedition.
    /// Lives on a dedicated expedition-state entity (not the player).
    /// Replicated to all clients for UI display.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ActiveStrifeState : IComponentData
    {
        /// <summary>Currently active Strife card.</summary>
        [GhostField] public StrifeCardId ActiveCardId;

        /// <summary>
        /// Bitmask of cards already used in this expedition (for no-repeat rotation).
        /// Bit index = (StrifeCardId - 1). Supports all 12 cards in a ushort.
        /// </summary>
        [GhostField] public ushort UsedCardsMask;

        /// <summary>
        /// Maps completed since last card rotation. Resets to 0 on rotation.
        /// Card rotates when this reaches RotationInterval.
        /// </summary>
        [GhostField] public byte MapsSinceLastRotation;

        /// <summary>
        /// Number of maps between card rotations. 0 = no rotation (single card for entire expedition).
        /// Set to 2 in higher ascension loops per GDD §8.
        /// </summary>
        [GhostField] public byte RotationInterval;

        /// <summary>Current ascension loop (0 = first playthrough). Drives rotation behavior.</summary>
        [GhostField] public byte AscensionLoop;

        /// <summary>Expedition seed used for deterministic card selection.</summary>
        [GhostField] public uint ExpeditionSeed;

        public bool IsCardUsed(StrifeCardId cardId) =>
            (UsedCardsMask & (ushort)(1 << ((int)cardId - 1))) != 0;

        public void MarkCardUsed(StrifeCardId cardId) =>
            UsedCardsMask |= (ushort)(1 << ((int)cardId - 1));

        public bool ShouldRotate => RotationInterval > 0 && MapsSinceLastRotation >= RotationInterval;
    }
}
```

### StrifeModifierTag (IComponentData)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeModifierTag.cs
using Unity.Entities;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Tag placed on RunModifierStack entries that were injected by the Strife system.
    /// Allows StrifeModifierBridge to find and remove them on card rotation.
    /// </summary>
    public struct StrifeModifierTag : IComponentData
    {
        /// <summary>Which card created this modifier. Used for cleanup on rotation.</summary>
        public StrifeCardId SourceCardId;
    }
}
```

### StrifeBossClauseReady (IEnableableComponent)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeBossClauseReady.cs
using Unity.Entities;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Enableable component on the expedition-state entity. Baked disabled.
    /// Enabled by StrifeActivationSystem when a boss clause is stored.
    /// Read by the boss encounter system to apply the clause on fight start.
    /// </summary>
    public struct StrifeBossClauseReady : IComponentData, IEnableableComponent
    {
        public StrifeBossClause Clause;
        public int BossClauseModifierHash;
    }
}
```

---

## Systems

### StrifeActivationSystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeActivationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: InitializationSystemGroup
// UpdateAfter: ExpeditionBootstrapSystem (framework)
//
// Runs once on expedition start (when ActiveStrifeState singleton is created).
//
// Algorithm:
//   1. Read ExpeditionSeed from ActiveStrifeState
//   2. Create a Unity.Mathematics.Random from seed
//   3. Determine ascension loop → set RotationInterval (0 for loop 0, 2 for loop 1+)
//   4. Build candidate list: all 12 cards minus any in UsedCardsMask
//   5. Select card: random.NextInt(candidateCount) → set ActiveCardId
//   6. MarkCardUsed(selectedCardId)
//   7. Resolve StrifeCardBlob from StrifeCardDatabase singleton
//   8. Call StrifeModifierBridge.ApplyCard() to inject modifiers
//   9. Store boss clause in StrifeBossClauseReady (enable the component)
//  10. Reset MapsSinceLastRotation = 0
//
// Determinism: same seed + same UsedCardsMask = same card selection.
// Shareable expedition seeds produce identical Strife experiences.
```

### StrifeRotationSystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeRotationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DistrictTransitionSystem (EPIC 4)
//
// Triggered when a map/district is completed (MapsSinceLastRotation incremented
// by DistrictTransitionSystem or equivalent).
//
// Algorithm:
//   1. Read ActiveStrifeState singleton
//   2. If !ShouldRotate → early out
//   3. Call StrifeModifierBridge.RemoveCard(currentActiveCardId) to strip old modifiers
//   4. Select new card from remaining pool (same seed-based algorithm, advanced RNG state)
//   5. If all 12 cards used → reset UsedCardsMask (allow repeats in marathon runs)
//   6. Call StrifeModifierBridge.ApplyCard(newCardId) to inject new modifiers
//   7. Update StrifeBossClauseReady with new boss clause
//   8. Reset MapsSinceLastRotation = 0
//   9. Fire StrifeRotatedEvent for UI notification
//
// NOTE: Previous card's modifiers fully removed before new ones applied.
// No stacking of old + new Strife effects.
```

### StrifeModifierBridge

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeModifierBridge.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: RunModifierApplySystem (framework)
//
// Static utility + managed system that translates Strife card data into
// RunModifierStack entries.
//
// ApplyCard(StrifeCardId cardId, ref StrifeCardBlob blob, EntityCommandBuffer ecb):
//   1. MAP RULE → Create RunModifier entity:
//      - ModifierHash = blob.MapRuleModifierHash
//      - Scope = Global (applies to all districts)
//      - Add StrifeModifierTag { SourceCardId = cardId }
//      - Modifier type depends on MapRule enum:
//        - CrossfireEvents → SpawnRate modifier on NPC faction skirmish spawner
//        - HudHacks → Enable HudCorruptionModifier on client
//        - InfectionClouds → Enable ToxicCloudSpawnerModifier
//        - FloatPockets → Enable GravityBubbleSpawnerModifier
//        - DeadZones → Enable AbilitySilenceZoneModifier
//        - LootScarcity → LootRate multiplier (0.6x), VendorStock multiplier (0.5x)
//        - StealthRoutes → Enable HiddenPassageModifier, PatrolDensity multiplier (1.5x)
//        - LootVolatility → LootRarity variance amplifier
//        - ThoughtZones → Enable StatusProximityZoneModifier
//        - SurfaceReconfigure → Enable SurfaceShiftTimerModifier
//        - ThirdPartyRaiders → Enable RaiderSpawnTimerModifier
//        - RewindPockets → Enable RewindFieldSpawnerModifier
//
//   2. ENEMY MUTATION → Create RunModifier entity:
//      - ModifierHash = blob.EnemyMutationModifierHash
//      - Scope = EnemyAI (AI parameter overrides)
//      - Add StrifeModifierTag
//      - Mutation type drives AI parameter overrides:
//        - StrikeTeams → PackSize = 3, CoordinatedEngagement = true
//        - SharedAwareness → AlertRadius = Infinite (room-wide)
//        - AdaptiveResistance → DamageTypeResistanceGain = 0.15, MaxStacks = 3
//        - MobilityBursts → DashInterval = Random(6, 10), DashForce = 1.5x
//        - EmpWeapons → OnHitCooldownDrain = 2s (4s melee)
//        - TougherElites → EliteHPMultiplier = 1.5, EliteDamageMultiplier = 1.3
//        - Ambushers → CloakChance = 0.4, DecloakRange = 5
//        - MercSideSwaps → SideSwapChance = 0.15, SideSwapHPThreshold = 0.75
//        - StatusViaAudioVisual → ProximityStatusChance = 1.0, StatusRange = 8
//        - Reassemble → ReviveCount = 1, ReviveDelay = 3, ReviveHP = 0.4
//        - MixedFactions → FactionMixEnabled = true
//        - ResetOnce → HPResetCount = 1
//
//   3. BOSS CLAUSE → Stored only (not applied to RunModifierStack yet):
//      - Written to StrifeBossClauseReady component
//      - BossEncounterSystem reads this when boss fight begins
//      - Applied as encounter-scoped modifier at fight start
//
//   4. DISTRICT INTERACTIONS → Per-district conditional modifiers:
//      - For each of the 3 DistrictInteractions:
//        - Create RunModifier entity with DistrictScope = interaction.DistrictId
//        - ModifierHash = interaction.ModifierSetHash
//        - Add StrifeModifierTag
//        - RunModifierApplySystem activates these only when entering matching district
//
// RemoveCard(StrifeCardId cardId, EntityCommandBuffer ecb):
//   1. Query all entities with StrifeModifierTag where SourceCardId == cardId
//   2. Destroy all matched modifier entities via ECB
//   3. Disable StrifeBossClauseReady
```

### StrifeEnemyMutationApplySystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeEnemyMutationApplySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: EnemySpawnSystem (AI framework)
//
// Applies the active Enemy Mutation to newly spawned enemies.
//
// Algorithm:
//   1. Read ActiveStrifeState singleton → ActiveCardId → EnemyMutation
//   2. Query newly spawned enemy entities (NewEnemyTag or equivalent)
//   3. For each enemy, apply mutation-specific AI parameter overrides:
//      - Write directly to AIBrain / AIState / relevant AI components
//      - Some mutations add new components (e.g., CloakState for Ambushers)
//      - Some mutations modify existing values (e.g., EliteHPMultiplier)
//   4. Remove NewEnemyTag after processing
//
// NOTE: Does NOT retroactively modify already-spawned enemies on card rotation.
// Only new spawns after rotation get the new mutation. Existing enemies keep
// their original mutation until death.
```

---

## Strife Selection Algorithm (Detail)

```csharp
// Pseudocode for deterministic card selection
//
// Input: uint seed, ushort usedCardsMask
// Output: StrifeCardId
//
// var rng = new Unity.Mathematics.Random(seed);
//
// // Build candidate list
// var candidates = new NativeList<StrifeCardId>(12, Allocator.Temp);
// for (byte i = 1; i <= 12; i++)
// {
//     if ((usedCardsMask & (1 << (i - 1))) == 0)
//         candidates.Add((StrifeCardId)i);
// }
//
// // Fallback: if all used (marathon run), reset
// if (candidates.Length == 0)
// {
//     usedCardsMask = 0;
//     for (byte i = 1; i <= 12; i++)
//         candidates.Add((StrifeCardId)i);
// }
//
// int index = rng.NextInt(0, candidates.Length);
// return candidates[index];
```

---

## Rotation Timeline (Higher Ascension Loops)

```
Ascension Loop 0 (first playthrough):
  RotationInterval = 0 → single card for entire expedition
  [Card A] ────────────────────────────────────────────→

Ascension Loop 1+:
  RotationInterval = 2 → card rotates every 2 maps
  [Card A] ── Map 1 ── Map 2 ──┤ [Card B] ── Map 3 ── Map 4 ──┤ [Card C] ...
                                 ↑ rotation                       ↑ rotation

  - Previous modifiers fully stripped
  - New modifiers applied
  - Boss clause updated to new card's clause
  - No stacking of old + new effects
```

---

## Setup Guide

1. **Requires** EPIC 7.1 complete (all enum types and StrifeCardDefinitionSO)
2. Create `ActiveStrifeState.cs`, `StrifeModifierTag.cs`, `StrifeBossClauseReady.cs` in `Assets/Scripts/Strife/Components/`
3. Create `StrifeActivationSystem.cs` in `Assets/Scripts/Strife/Systems/`
4. Create `StrifeRotationSystem.cs` in `Assets/Scripts/Strife/Systems/`
5. Create `StrifeModifierBridge.cs` in `Assets/Scripts/Strife/Systems/`
6. Create `StrifeEnemyMutationApplySystem.cs` in `Assets/Scripts/Strife/Systems/`
7. **Create authoring**: `StrifeStateAuthoring` MonoBehaviour on the expedition-state prefab that bakes `ActiveStrifeState` (default None) and `StrifeBossClauseReady` (baked disabled)
8. Ensure `RunModifierStack` framework supports `StrifeModifierTag`-tagged entries (tag is additive, does not interfere with existing modifier pipeline)
9. Wire `StrifeActivationSystem` to run after `ExpeditionBootstrapSystem` in `InitializationSystemGroup`
10. Wire `StrifeRotationSystem` to run after `DistrictTransitionSystem` in `SimulationSystemGroup`

---

## Verification

- [ ] `ActiveStrifeState` singleton created on expedition start with correct seed
- [ ] Deterministic selection: same seed always produces same card
- [ ] Card modifiers appear as RunModifierStack entries with `StrifeModifierTag`
- [ ] Map Rule modifier active globally across all districts
- [ ] Enemy Mutation parameters applied to newly spawned enemies
- [ ] Boss Clause stored in `StrifeBossClauseReady` (enabled)
- [ ] In ascension loop 0: no rotation occurs (single card for entire expedition)
- [ ] In ascension loop 1+: card rotates every 2 completed maps
- [ ] On rotation: old modifiers fully stripped, new modifiers applied, no stacking
- [ ] `UsedCardsMask` prevents repeat cards within an expedition
- [ ] When all 12 cards used (marathon), mask resets and pool reopens
- [ ] `StrifeModifierBridge.RemoveCard()` destroys all modifier entities for the old card
- [ ] Ghost replication: `ActiveStrifeState` fields replicate to all clients
- [ ] No new components on the player entity (all state on expedition-state singleton)

---

## Live Tuning

```csharp
// File: Assets/Scripts/Strife/Components/StrifeLiveTuning.cs
using Unity.Entities;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Singleton for runtime-tunable Strife modifier values.
    /// Modified by the RunWorkstation live tuning panel (EPIC 23.7).
    /// </summary>
    public struct StrifeLiveTuning : IComponentData
    {
        // === Enemy Mutation Overrides ===
        /// <summary>Adaptive Resistance gain per hit (default 0.15).</summary>
        public float AdaptiveResistanceGain;
        /// <summary>Max adaptive resistance stacks (default 3).</summary>
        public int AdaptiveResistanceMaxStacks;
        /// <summary>Elite HP multiplier for Data Famine (default 1.5).</summary>
        public float EliteHPMultiplier;
        /// <summary>Elite damage multiplier for Data Famine (default 1.3).</summary>
        public float EliteDamageMultiplier;
        /// <summary>Ambusher cloak chance for Black Budget (default 0.4).</summary>
        public float AmbusherCloakChance;
        /// <summary>Merc side-swap chance for Market Panic (default 0.15).</summary>
        public float MercSideSwapChance;
        /// <summary>Enemy reassemble HP fraction for Nanoforge Bloom (default 0.4).</summary>
        public float ReassembleHPFraction;
        /// <summary>EMP cooldown drain per hit in seconds (default 2.0).</summary>
        public float EmpCooldownDrain;

        // === Map Rule Overrides ===
        /// <summary>Loot rate multiplier for Data Famine (default 0.6).</summary>
        public float LootScarcityMultiplier;
        /// <summary>Vendor stock multiplier for Data Famine (default 0.5).</summary>
        public float VendorStockMultiplier;
        /// <summary>Patrol density multiplier for Black Budget (default 1.5).</summary>
        public float PatrolDensityMultiplier;

        // === Boss Clause Overrides ===
        /// <summary>Boss damage bonus for Data Famine clause (default 0.4 = +40%).</summary>
        public float BossFewerAddsBonus;
        /// <summary>Cooldown tax for Quiet Crusade clause (default 0.3 = +30%).</summary>
        public float BossCooldownTax;
    }
}

// Live tuning integration:
// - StrifeLiveTuning singleton created by StrifeStateAuthoring with defaults from blob
// - StrifeEnemyMutationApplySystem reads tuning values instead of hardcoded constants
// - StrifeModifierBridge reads tuning values when creating RunModifier entries
// - RunWorkstation (EPIC 23.7) exposes grouped sliders:
//   [Enemy Mutations]
//     Adaptive Resist Gain: [0.05 ──●── 0.50]
//     Elite HP Mult:        [1.0 ──●── 3.0]
//     Cloak Chance:         [0.1 ──●── 0.8]
//   [Map Rules]
//     Loot Scarcity:        [0.2 ──●── 1.0]
//     Patrol Density:       [1.0 ──●── 3.0]
//   [Boss Clauses]
//     Boss Damage Bonus:    [0.1 ──●── 1.0]
//     Cooldown Tax:         [0.1 ──●── 0.8]
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Strife/Debug/StrifeActiveDebugOverlay.cs
// Managed SystemBase, ClientSimulation | LocalSimulation, PresentationSystemGroup
//
// Active Strife Effects HUD — toggled via debug console: `strife.debug`
//
// Displays:
//   1. Active Card header:
//      - StrifeCardId name + icon (theme color border)
//      - AscensionLoop, RotationInterval, MapsSinceLastRotation
//      - UsedCardsMask (expanded: which cards already used)
//
//   2. Active Modifiers panel:
//      - Map Rule: enum name + modifier description + active RunModifier entity count
//      - Enemy Mutation: enum name + parameter values (from StrifeLiveTuning)
//      - Boss Clause: enum name + stored/applied status
//
//   3. District Interaction status:
//      - Current district: is interaction active? (StrifeDistrictActive flag)
//      - If active: InteractionType (Amplify/Mitigate), BonusRewardMultiplier
//      - StrifeDistrictModifier component values
//
//   4. Modifier entity list:
//      - All entities with StrifeModifierTag, showing:
//        SourceCardId, ModifierHash, scope (Global/EnemyAI/District)
//
//   5. Rotation forecast:
//      - Maps until next rotation
//      - Remaining unused cards (from UsedCardsMask)
//
// Color coding: red=Amplify active, blue=Mitigate active, white=neutral.
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/StrifeWorkstation/StrifeApplicationSimulation.cs
using UnityEditor;

namespace Hollowcore.Strife.Editor
{
    /// <summary>
    /// Strife application simulation: validates modifier injection and rotation.
    /// Menu: Hollowcore > Simulation > Strife Application
    /// </summary>
    public static class StrifeApplicationSimulation
    {
        [MenuItem("Hollowcore/Simulation/Strife Application")]
        public static void RunApplicationTests()
        {
            // 1. Selection determinism:
            //    - 50 seeds: verify same seed → same card
            //    - Verify all 12 cards are selected across seeds 1..1000
            //      (uniform distribution within 15% tolerance)

            // 2. Rotation no-repeat:
            //    - Simulate 12-map expedition with RotationInterval=2
            //    - Verify no card repeats until all 12 used
            //    - Verify UsedCardsMask resets after all 12 used

            // 3. Modifier injection:
            //    - For each card, verify 3 RunModifier entities created (map+enemy+district×3... wait, 5 total):
            //      1 MapRule + 1 EnemyMutation + 3 DistrictInteractions = 5 modifier entities
            //    - Verify each has StrifeModifierTag with correct SourceCardId

            // 4. Modifier cleanup on rotation:
            //    - Inject card A modifiers (5 entities)
            //    - Rotate to card B
            //    - Verify all card A StrifeModifierTag entities destroyed
            //    - Verify card B's 5 modifier entities exist

            // 5. Boss clause storage:
            //    - Verify StrifeBossClauseReady enabled after activation
            //    - Verify clause matches card's BossClause enum
            //    - Verify hash matches card's BossClauseModifierHash
        }
    }
}
```
