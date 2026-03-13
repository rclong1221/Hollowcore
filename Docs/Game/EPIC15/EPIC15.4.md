# EPIC 15.4: The Board (Market Boss)

**Status**: Planning
**Epic**: EPIC 15 — Final Bosses & Endgame
**Dependencies**: EPIC 15.1 (InfluenceMeterState, faction selection); EPIC 14 (Boss Definition Template, Variant Clauses, Arena System); Framework: Combat/, AI/

---

## Overview

The Board is the Market faction's final boss -- a collective of corporate executives operating from an impossible boardroom that defies physics. Triggered when the player has disrupted Market districts (Auction, Wetmarket, Mirrortown) more than the other factions. The fight uses economic warfare mechanics: buyout attempts that lock down player abilities, contract traps that penalize specific actions, and mercenary waves purchased with a visible "budget" resource. The fight progresses from negotiation (offering deals that are traps) through hostile takeover (direct economic combat) to liquidation (scorched earth, everything burns).

---

## Component Definitions

### BoardPhaseState (IComponentData)

```csharp
// File: Assets/Scripts/Boss/Components/FinalBoss/BoardComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Boss.FinalBoss
{
    public enum BoardPhase : byte
    {
        Negotiation = 0,  // Phase 1: offers deals (traps), contract mechanics
        Takeover = 1,     // Phase 2: hostile combat, buyout attacks, merc waves
        Liquidation = 2   // Phase 3: scorched earth, everything on fire
    }

    /// <summary>
    /// Board-specific combat state layered on top of BossPhaseState.
    /// Tracks economic warfare mechanics unique to this boss.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BoardPhaseState : IComponentData
    {
        [GhostField] public BoardPhase CurrentPhase;

        /// <summary>
        /// The Board's visible budget resource (displayed to player).
        /// Spent on mercenary waves, buyout attacks, contract traps.
        /// Regenerates over time. Player can drain it by destroying "assets" in the arena.
        /// </summary>
        [GhostField(Quantization = 10)] public float Budget;
        [GhostField(Quantization = 10)] public float MaxBudget;
        [GhostField(Quantization = 10)] public float BudgetRegenRate;

        /// <summary>Number of active contracts binding the player.</summary>
        [GhostField] public byte ActiveContractCount;

        /// <summary>Bitmask of player abilities currently "bought out" (locked).</summary>
        [GhostField] public ushort BuyoutMask;

        /// <summary>Number of board members remaining (multi-target boss).</summary>
        [GhostField] public byte BoardMembersAlive;

        /// <summary>Liquidation progress (0-1). At 1.0, arena is fully ablaze.</summary>
        [GhostField(Quantization = 100)] public float LiquidationProgress;
    }
}
```

### BoardContractElement (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/FinalBoss/BoardComponents.cs (continued)
using Unity.Collections;

namespace Hollowcore.Boss.FinalBoss
{
    public enum ContractType : byte
    {
        MovementPenalty = 0,  // "Non-compete": taking certain paths deals damage
        AttackTax = 1,        // "Revenue share": each attack costs player health
        HealingBlock = 2,     // "Exclusivity clause": healing items locked for duration
        AreaRestriction = 3,  // "Zoning permit": player restricted to arena section
        TimeBomb = 4          // "Deadline clause": must break contract within timer or take massive damage
    }

    /// <summary>
    /// Active contracts imposed on the player by The Board.
    /// Each contract penalizes specific player actions until broken.
    /// Breaking a contract: destroy the contract "document" entity in the arena.
    /// Buffer on the Board boss entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct BoardContractElement : IBufferElementData
    {
        public ContractType Type;
        public Entity ContractDocumentEntity; // Destroyable arena object

        /// <summary>Duration remaining. Contract expires naturally or when document destroyed.</summary>
        public float TimeRemaining;

        /// <summary>Penalty magnitude (damage per tick, heal block duration, etc.).</summary>
        public float PenaltyValue;

        /// <summary>Display text for UI.</summary>
        public FixedString64Bytes ContractName;
    }
}
```

### BoardAssetElement (IBufferElementData)

```csharp
// File: Assets/Scripts/Boss/Components/FinalBoss/BoardComponents.cs (continued)
namespace Hollowcore.Boss.FinalBoss
{
    public enum BoardAssetType : byte
    {
        SafeDeposit = 0,     // Destroying drains Board's Budget
        MercContract = 1,    // Destroying prevents next merc wave
        InsurancePolicy = 2, // Destroying weakens Board member defenses
        GoldenParachute = 3  // Must destroy to prevent Board member from "escaping" (healing)
    }

    /// <summary>
    /// Destroyable assets in the arena that weaken The Board when eliminated.
    /// Buffer on the Board boss entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct BoardAssetElement : IBufferElementData
    {
        public BoardAssetType AssetType;
        public Entity WorldEntity;
        public float Health;
        public float MaxHealth;
        public bool IsActive;

        /// <summary>Budget drain when destroyed (for SafeDeposit type).</summary>
        public float BudgetDrainAmount;
    }
}
```

---

## ScriptableObject Definitions

### BoardDefinitionSO

```csharp
// File: Assets/Scripts/Boss/Definitions/FinalBoss/BoardDefinitionSO.cs
using System.Collections.Generic;
using UnityEngine;

namespace Hollowcore.Boss.Definitions.FinalBoss
{
    [CreateAssetMenu(fileName = "TheBoard", menuName = "Hollowcore/Boss/Final/The Board")]
    public class BoardDefinitionSO : BossDefinitionSO
    {
        [Header("Board-Specific")]
        [Tooltip("Number of board members (each a sub-target with own health)")]
        public int BoardMemberCount = 3;
        [Tooltip("Health per board member")]
        public float MemberHealth = 3000f;

        [Tooltip("Starting budget")]
        public float StartingBudget = 100f;
        [Tooltip("Budget regen per second")]
        public float BudgetRegenRate = 2f;

        [Tooltip("Cost to summon a merc wave")]
        public float MercWaveCost = 30f;
        [Tooltip("Cost to attempt a buyout attack")]
        public float BuyoutCost = 20f;
        [Tooltip("Cost to impose a contract")]
        public float ContractCost = 15f;

        [Tooltip("Liquidation burn rate per second in Phase 3")]
        public float LiquidationRate = 0.04f;

        [Tooltip("Arena asset configuration per phase")]
        public List<PhaseAssetConfig> PhaseAssetConfigs = new();

        [Tooltip("Board member personalities affect attack patterns")]
        public List<BoardMemberProfile> MemberProfiles = new();
    }

    [System.Serializable]
    public class PhaseAssetConfig
    {
        public BoardPhase Phase;
        public int SafeDepositCount;
        public int MercContractCount;
        public int InsurancePolicyCount;
    }

    [System.Serializable]
    public class BoardMemberProfile
    {
        public string MemberName;
        [TextArea] public string Personality;
        public BossAttackPatternSO PreferredAttack;
        [Tooltip("This member's specialization")]
        public ContractType SpecialtyContract;
    }
}
```

---

## Systems

### BoardAISystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/BoardAISystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BossEncounterSystem
//
// Boss AI for The Board. Multi-target boss using economic warfare.
//
// Phase 1 — Negotiation:
//   1. Board members are seated, protected by contracts and assets
//   2. Board "offers deals" — actually contract traps disguised as dialogue choices
//      - Accepting: contract imposed with hidden penalty
//      - Refusing: Board attacks directly (telegraphed, dodgeable)
//   3. Board members take turns making offers (rotating aggro)
//   4. Player can attack board members directly (low damage through defenses)
//   5. Destroying InsurancePolicy assets strips board member defenses
//   6. Transition to Takeover when first board member defeated
//
// Phase 2 — Hostile Takeover:
//   1. Remaining board members become aggressive
//   2. Budget-driven attack loop:
//      a. If Budget >= MercWaveCost: spawn merc wave (3-5 generic enemies)
//      b. If Budget >= BuyoutCost: buyout attack (locks one player ability for 10s)
//      c. If Budget >= ContractCost: impose random contract
//   3. Player can destroy SafeDeposit assets to drain Budget
//   4. Destroying MercContract assets prevents next merc wave
//   5. Each board member killed reduces Budget regen rate
//   6. Transition to Liquidation when one board member remains
//
// Phase 3 — Liquidation:
//   1. Last board member declares "total liquidation"
//   2. LiquidationProgress starts rising — arena catches fire
//   3. Board member becomes direct combatant (no more economic attacks)
//   4. All remaining assets explode, dealing damage to everything
//   5. Fire zones expand — arena shrinks
//   6. Board member fights desperately, high damage but reckless
//   7. Narrative: "Everything has a price. Even you. Especially you."
//
// Market district scaling:
//   - Auction cleared → Budget regen rate +25%, higher quality mercs
//   - Wetmarket cleared → Contract penalties more severe
//   - Mirrortown cleared → Board members can "mirror" player attacks back
```

### BoardContractSystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/BoardContractSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BoardAISystem
//
// Manages active contracts imposed on the player.
//
// For each BoardContractElement:
//   1. Decrement TimeRemaining by deltaTime
//   2. If TimeRemaining <= 0 OR ContractDocumentEntity destroyed:
//      a. Remove contract effects from player
//      b. Remove element from buffer
//      c. Decrement ActiveContractCount
//   3. If active, apply penalty:
//      MovementPenalty: damage when player enters restricted paths
//      AttackTax: subtract health on player attack (small amount per hit)
//      HealingBlock: prevent healing item use
//      AreaRestriction: damage when player leaves allowed zone
//      TimeBomb: if TimeRemaining <= 0 without breaking → massive damage burst
//   4. Fire ContractUIEvent for contract status display
```

### BoardBudgetSystem

```csharp
// File: Assets/Scripts/Boss/Systems/FinalBoss/BoardBudgetSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BoardAISystem
//
// Manages The Board's budget resource.
//
// Each frame:
//   1. Regenerate Budget: += BudgetRegenRate * deltaTime * (BoardMembersAlive / TotalMembers)
//   2. Clamp to MaxBudget
//   3. When SafeDeposit asset destroyed: Budget -= BudgetDrainAmount
//   4. Fire BudgetChangedEvent for UI display
//
// Budget is visible to the player — strategic information:
//   "They're about to afford another merc wave — destroy that safe deposit!"
```

---

## Variant Clause Examples

| Clause | Trigger | Effect |
|---|---|---|
| Market Monopoly | SideGoalSkipped (Auction) | Starting Budget doubled |
| Black Market Ties | SideGoalSkipped (Wetmarket) | Contracts cannot expire naturally (must be broken) |
| Mirror Defense | StrifeCard (Economic Pressure) | Board members reflect 20% of damage taken |
| Deep Reserves | FrontPhase >= 3 | +25% health, Budget regen +50% |
| Private Military | TraceLevel >= 4 | Merc waves include elite enemies |
| Market Insider Token | CounterToken | Disables one board member's specialty contract |

---

## Setup Guide

1. Create BoardComponents.cs in `Assets/Scripts/Boss/Components/FinalBoss/`
2. Create BoardDefinitionSO asset in `Assets/Data/Boss/Final/TheBoard.asset`
3. Build arena subscene: Impossible Boardroom
   - Central boardroom table (board member positions)
   - SafeDeposit entities (destructible, scattered around room edges)
   - MercContract entities (destroyable documents on pedestals)
   - InsurancePolicy entities (glowing shields near board members)
   - GoldenParachute entities (escape pods behind each member)
   - Contract document spawn points
   - Fire zone expansion volumes for Phase 3
4. Create 3 board member sub-prefabs (each with own health pool, attack pattern)
5. Create contract UI overlay:
   - Active contract list with icons, penalties, and timers
   - Contract document highlight (destroyable target indicator)
6. Create Budget UI bar (visible to player, shows Board's spending resource)
7. Configure merc wave prefab (3-5 generic combat enemies)
8. Wire BoardBudgetSystem to drive AI spending decisions

---

## Verification

- [ ] Board spawns when Market is dominant faction
- [ ] Phase 1: Board members make "deal" offers with hidden penalties
- [ ] Accepting a deal imposes a contract with correct penalty
- [ ] Refusing a deal triggers telegraphed attack
- [ ] Destroying InsurancePolicy strips board member defenses
- [ ] Phase 1 → 2 transition when first board member defeated
- [ ] Phase 2: Budget drives merc waves, buyouts, and contracts
- [ ] Destroying SafeDeposit drains Budget
- [ ] Destroying MercContract prevents merc wave
- [ ] Buyout attack locks player ability for correct duration
- [ ] Contract penalties apply correctly (movement, attack tax, healing block, area, time bomb)
- [ ] Contract breaks when document entity destroyed OR timer expires
- [ ] TimeBomb contract deals massive damage if not broken in time
- [ ] Phase 2 → 3 transition when one board member remains
- [ ] Phase 3: LiquidationProgress rises, fire zones expand
- [ ] Budget UI visible to player throughout fight
- [ ] Board member death reduces Budget regen rate
- [ ] District enhancement: clearing Auction boosts Budget regen
- [ ] Variant clauses activate/deactivate correctly

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/Boss/Authoring/FinalBoss/BoardBlobBaker.cs
namespace Hollowcore.Boss.Authoring.FinalBoss
{
    // BoardDefinitionSO extends BossDefinitionSO → BossBlob (14.1) + BoardBlob
    //
    // BoardBlob (additional blob on Board boss entity):
    //   int BoardMemberCount
    //   float MemberHealth
    //   float StartingBudget, MaxBudget (= StartingBudget), BudgetRegenRate
    //   float MercWaveCost, BuyoutCost, ContractCost
    //   float LiquidationRate
    //   BlobArray<PhaseAssetBlob> PhaseAssetConfigs
    //     - BoardPhase Phase
    //     - int SafeDepositCount, MercContractCount, InsurancePolicyCount
    //   BlobArray<BoardMemberBlob> MemberProfiles
    //     - FixedString64Bytes MemberName
    //     - int PreferredAttackPatternId
    //     - ContractType SpecialtyContract
    //
    // BoardContractElement and BoardAssetElement buffers baked from arena subscene.
    // Budget costs and regen baked into blob for Burst-compatible AI reads.
}
```

---

## Validation

```csharp
// File: Assets/Editor/BossWorkstation/BoardValidator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Board-specific validation (extends boss validation from 14.1):
    //
    // 1. Phase health thresholds:
    //    Negotiation→Takeover when first board member dies.
    //    Takeover→Liquidation when one member remains.
    //    Implicit: total boss HP = BoardMemberCount * MemberHealth.
    //    Warning if MemberHealth * BoardMemberCount differs from BossDefinitionSO.BaseHealth by > 10%.
    //
    // 2. Budget economy balance:
    //    BudgetRegenRate * 60s should not exceed MercWaveCost * 3 (can't afford unlimited waves).
    //    Warning if regen is too high relative to costs (trivializes budget pressure).
    //    Warning if regen is too low (Board never uses abilities).
    //
    // 3. Contract duration bounds:
    //    All contract durations should be in [5s, 60s].
    //    Warning if TimeBomb duration < 10s (unfair) or > 45s (trivial to break).
    //
    // 4. BoardMemberCount must be >= 2 (multi-target is the core mechanic).
    //    Error if < 2.
    //
    // 5. LiquidationRate: at rate, Phase 3 should last at least 30s: 1.0 / LiquidationRate >= 30.
    //    Warning if too fast (< 30s) or too slow (> 120s).
    //
    // 6. PhaseAssetConfigs must cover all three BoardPhase values.
    //    Error if any phase missing.
    //
    // 7. District enhancement: verify cleared market district IDs
    //    (Auction, Wetmarket, Mirrortown) are valid.
}
```

---

## Live Tuning

```csharp
// File: Assets/Scripts/Boss/Debug/BoardLiveTuning.cs
namespace Hollowcore.Boss.Debug
{
    // Board live tuning (extends BossLiveTuning):
    //
    //   float BudgetOverride              // -1 = normal, >=0 = force budget value
    //   float BudgetRegenRateOverride     // -1 = use baked, >0 = override
    //   float LiquidationProgressOverride // -1 = normal, [0,1] = force value
    //   float LiquidationRateOverride     // -1 = use baked, >0 = override
    //   bool DisableContracts             // suppress all contract imposition
    //   bool DisableMercWaves             // suppress all merc wave spawns
    //   bool DisableBuyouts               // suppress all buyout attacks
    //   float ContractDurationMultiplier  // scale all contract durations (default 1.0)
    //   bool ForceNegotiationPhase        // lock to Phase 1
    //   bool ForceLiquidationPhase        // skip to Phase 3 immediately
    //   bool DisableDistrictScaling       // ignore cleared district enhancements
}
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Boss/Debug/BoardDebugOverlay.cs
namespace Hollowcore.Boss.Debug
{
    // Board debug overlay (extends BossDebugOverlay):
    //
    // [1] Budget Meter
    //     - Horizontal bar: 0 → MaxBudget
    //     - Tick marks at MercWaveCost, BuyoutCost, ContractCost thresholds
    //     - Regen rate displayed: "+2.0/s"
    //     - Color: green = low threat, yellow = approaching wave cost, red = can afford multiple actions
    //
    // [2] Board Member Status
    //     - Mini HP bars for each board member (labeled by name)
    //     - Specialty contract icon per member
    //     - "DEFEATED" overlay on killed members
    //     - Budget regen contribution: "2/3 members = 66% regen"
    //
    // [3] Active Contracts Panel
    //     - List of active contracts with type icon, timer, penalty description
    //     - Contract document entity position shown on arena minimap
    //     - TimeBomb contracts highlighted with urgent countdown
    //
    // [4] Arena Assets
    //     - Minimap showing SafeDeposit, MercContract, InsurancePolicy, GoldenParachute positions
    //     - HP bars for each asset
    //     - Destroyed assets shown as X marks
    //
    // [5] Liquidation Progress
    //     - Progress bar: 0% → 100% (full ablaze)
    //     - Fire zone expansion preview (wireframe of next fire zone boundary)
    //     - Time remaining estimate
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/BossWorkstation/BoardSimulator.cs
namespace Hollowcore.Editor.BossWorkstation
{
    // Board-specific simulation extensions:
    //
    // Additional simulation inputs:
    //   bool[] clearedMarketDistricts    // which of Auction/Wetmarket/Mirrortown were cleared
    //   float contractBreakRate          // probability of destroying contract document per attempt
    //   float assetTargetingPriority     // 0 = ignore assets, 1 = always prioritize asset destruction
    //   float dealAcceptRate             // probability of accepting Phase 1 deals (0 = always refuse)
    //
    // Additional outputs:
    //   float meanBudgetAtPhaseTransition   // Board budget when phase changes
    //   int meanMercWavesSpawned            // total merc waves per fight
    //   int meanContractsImposed            // total contracts imposed per fight
    //   float meanContractActiveTime        // how long contracts stay active
    //   float wipeRateFromLiquidation       // wipes from fire consuming arena
    //   float wipeRateFromTimeBomb          // wipes from expired TimeBomb contracts
    //   float assetDestructionImpact        // budget drain from destroyed SafeDeposits
    //   float districtEnhancementImpact     // wipe rate delta: with vs without enhancements
    //
    // Key balance questions answered:
    //   "Can the Board afford infinite merc waves? (budget regen vs cost)"
    //   "Is Phase 3 survivable before liquidation completes?"
    //   "Are contracts breakable in time for average players?"
    //   "Does accepting deals in Phase 1 create an unfair disadvantage?"
}
