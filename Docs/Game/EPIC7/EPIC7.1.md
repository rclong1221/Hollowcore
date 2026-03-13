# EPIC 7.1: Strife Card Data Model

**Status**: Planning
**Epic**: EPIC 7 — Strife System
**Priority**: Critical — Foundation for all Strife mechanics
**Dependencies**: None (definition only)

---

## Overview

Defines the 12 Strife cards from GDD §8.1 as ScriptableObject data. Each card represents a macro-scale galactic or local crisis that modifies an entire expedition. A card carries a Map Rule (global gameplay modifier), an Enemy Mutation (AI behavior change), a Boss Clause (boss encounter alteration), and three District Interactions (amplification or mitigation effects in thematically linked districts). All 12 cards are enumerated here with their complete effect descriptions.

---

## Component Definitions

### StrifeCardId Enum

```csharp
// File: Assets/Scripts/Strife/Components/StrifeCardId.cs
namespace Hollowcore.Strife
{
    /// <summary>
    /// Unique identifier for each of the 12 Strife cards. Byte-sized for
    /// compact storage in ECS components and serialization.
    /// </summary>
    public enum StrifeCardId : byte
    {
        None = 0,
        SuccessionWar = 1,
        SignalSchism = 2,
        PlagueArmada = 3,
        GravityStorm = 4,
        QuietCrusade = 5,
        DataFamine = 6,
        BlackBudget = 7,
        MarketPanic = 8,
        MemeticWild = 9,
        NanoforgeBloom = 10,
        SovereignRaid = 11,
        TimeFracture = 12
    }
}
```

### StrifeMapRule Enum

```csharp
// File: Assets/Scripts/Strife/Components/StrifeMapRule.cs
namespace Hollowcore.Strife
{
    /// <summary>
    /// Global map-level modifier applied to all districts while the Strife is active.
    /// Exactly one per active Strife card.
    /// </summary>
    public enum StrifeMapRule : byte
    {
        None = 0,
        CrossfireEvents = 1,       // Succession War: random NPC faction skirmishes erupt
        HudHacks = 2,              // Signal Schism: periodic HUD corruption/false readings
        InfectionClouds = 3,       // Plague Armada: toxic zones spawn and drift across map
        FloatPockets = 4,          // Gravity Storm: low-gravity bubbles appear randomly
        DeadZones = 5,             // Quiet Crusade: areas where abilities are silenced
        LootScarcity = 6,          // Data Famine: vendor inventories halved, chest rates reduced
        StealthRoutes = 7,         // Black Budget: hidden passages open, main corridors patrolled
        LootVolatility = 8,        // Market Panic: loot rarity swings wildly per room
        ThoughtZones = 9,          // Memetic Wild: areas that inflict random status via proximity
        SurfaceReconfigure = 10,   // Nanoforge Bloom: floor/wall surfaces shift material type
        ThirdPartyRaiders = 11,    // Sovereign Raid: neutral hostile squads invade districts
        RewindPockets = 12         // Time Fracture: localized time-rewind fields
    }
}
```

### StrifeEnemyMutation Enum

```csharp
// File: Assets/Scripts/Strife/Components/StrifeEnemyMutation.cs
namespace Hollowcore.Strife
{
    /// <summary>
    /// Faction-wide enemy behavior modification. Applied as AI parameter overrides
    /// to all enemies spawned while the Strife is active.
    /// </summary>
    public enum StrifeEnemyMutation : byte
    {
        None = 0,
        StrikeTeams = 1,           // Succession War: enemies attack in coordinated 3-packs
        SharedAwareness = 2,       // Signal Schism: alert one enemy = alert the room
        AdaptiveResistance = 3,    // Plague Armada: enemies gain resistance to repeated damage types
        MobilityBursts = 4,        // Gravity Storm: enemies periodically dash/leap unpredictably
        EmpWeapons = 5,            // Quiet Crusade: enemy attacks drain ability cooldowns
        TougherElites = 6,         // Data Famine: elite enemies have +50% HP and damage
        Ambushers = 7,             // Black Budget: enemies spawn cloaked, decloak on attack
        MercSideSwaps = 8,         // Market Panic: some enemies switch sides mid-fight (temporary ally)
        StatusViaAudioVisual = 9,  // Memetic Wild: enemies inflict status by proximity/sound
        Reassemble = 10,           // Nanoforge Bloom: enemies rebuild after death (one-time revive)
        MixedFactions = 11,        // Sovereign Raid: enemy rooms contain mixed faction compositions
        ResetOnce = 12             // Time Fracture: each enemy resets to full HP once when killed
    }
}
```

### StrifeBossClause Enum

```csharp
// File: Assets/Scripts/Strife/Components/StrifeBossClause.cs
namespace Hollowcore.Strife
{
    /// <summary>
    /// Modification to the boss encounter at the end of the expedition.
    /// Stored on expedition start, applied when the boss fight begins.
    /// </summary>
    public enum StrifeBossClause : byte
    {
        None = 0,
        Reinforcements = 1,        // Succession War: boss buys reinforcement waves with phase damage
        SystemPossession = 2,      // Signal Schism: boss hijacks arena systems (turrets, traps)
        AdaptiveSkin = 3,          // Plague Armada: boss gains resistance to last damage type each phase
        GravityShifts = 4,         // Gravity Storm: arena gravity direction rotates per phase
        CooldownTax = 5,           // Quiet Crusade: player ability cooldowns +30% during boss fight
        FewerAddsDeadlier = 6,     // Data Famine: no adds, but boss deals +40% damage
        PreparedDefenses = 7,      // Black Budget: boss arena has pre-placed traps and turrets
        BuyoutDeals = 8,           // Market Panic: boss offers mid-fight bargains (risk/reward)
        FakePhases = 9,            // Memetic Wild: boss feigns death / fake phase transitions
        RegenNodes = 10,           // Nanoforge Bloom: boss spawns regen nodes that must be destroyed
        RaidInterruption = 11,     // Sovereign Raid: third-party raiders attack during boss fight
        PhaseRewind = 12           // Time Fracture: boss rewinds to previous phase once at 25% HP
    }
}
```

### StrifeDistrictInteraction (Struct)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeDistrictInteraction.cs
using Unity.Collections;

namespace Hollowcore.Strife
{
    /// <summary>
    /// A single district-specific interaction for a Strife card.
    /// Each card defines exactly 3 of these.
    /// </summary>
    [System.Serializable]
    public struct StrifeDistrictInteraction
    {
        /// <summary>District type ID this interaction targets.</summary>
        public int DistrictId;

        /// <summary>Human-readable effect description for UI.</summary>
        public string EffectDescription;

        /// <summary>
        /// Whether this interaction amplifies the Strife (harder) or mitigates it (easier).
        /// Amplification increases challenge + reward. Mitigation provides relief.
        /// </summary>
        public StrifeInteractionType InteractionType;

        /// <summary>Modifier set ID referencing a RunModifierDefinitionSO for the effect.</summary>
        public int ModifierSetId;

        /// <summary>Bonus reward multiplier for completing the district under this interaction.</summary>
        public float BonusRewardMultiplier;
    }

    public enum StrifeInteractionType : byte
    {
        Amplify = 0,
        Mitigate = 1
    }
}
```

---

## ScriptableObject Definitions

### StrifeCardDefinitionSO

```csharp
// File: Assets/Scripts/Strife/Definitions/StrifeCardDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.Strife.Definitions
{
    [CreateAssetMenu(fileName = "NewStrifeCard", menuName = "Hollowcore/Strife/Card Definition")]
    public class StrifeCardDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public StrifeCardId CardId;
        public string DisplayName;
        [TextArea(2, 4)] public string FlavorText;
        [TextArea(2, 6)] public string MechanicalDescription;
        public Sprite Icon;
        public Sprite CardArt;

        [Header("Map Rule")]
        public StrifeMapRule MapRule;
        [TextArea(1, 3)] public string MapRuleDescription;
        [Tooltip("RunModifierDefinitionSO that implements this map rule")]
        public ScriptableObject MapRuleModifierDef;

        [Header("Enemy Mutation")]
        public StrifeEnemyMutation EnemyMutation;
        [TextArea(1, 3)] public string EnemyMutationDescription;
        [Tooltip("RunModifierDefinitionSO that implements this enemy mutation")]
        public ScriptableObject EnemyMutationModifierDef;

        [Header("Boss Clause")]
        public StrifeBossClause BossClause;
        [TextArea(1, 3)] public string BossClauseDescription;
        [Tooltip("RunModifierDefinitionSO that implements this boss clause")]
        public ScriptableObject BossClauseModifierDef;

        [Header("District Interactions (exactly 3)")]
        public StrifeDistrictInteraction[] DistrictInteractions = new StrifeDistrictInteraction[3];

        [Header("Visual Identity")]
        public Color ThemeColor = Color.white;
        public GameObject ParticleEffectPrefab;
        public Sprite UIBorderSprite;

        [Header("Audio")]
        [Tooltip("Ambient audio layer that plays throughout the expedition")]
        public AudioClip AmbientLayer;
        [Tooltip("Sting that plays when the Strife card is revealed")]
        public AudioClip RevealSting;
    }
}
```

---

## The 12 Strife Cards (GDD §8.1)

### 1. Succession War
- **Map Rule — Crossfire Events**: Random NPC faction skirmishes erupt in rooms. Players caught in crossfire take collateral damage. Skirmish zones marked on minimap.
- **Enemy Mutation — Strike Teams**: Enemies attack in coordinated 3-packs with staggered engagement timing. Solo enemies no longer exist.
- **Boss Clause — Reinforcements**: Boss spends accumulated phase damage to buy reinforcement waves. More damage dealt to the boss = larger reinforcement waves.
- **District Interactions**:
  - **Auction (Amplify)**: Auditors are hyperactive — every purchase triggers an Auditor ambush. Prices +20% but loot quality +1 tier.
  - **Garrison (Amplify)**: Double faction patrols, but defeated patrols drop military-grade loot.
  - **Bazaar (Mitigate)**: War refugees sell discounted supplies. Vendor prices -30%.

### 2. Signal Schism
- **Map Rule — HUD Hacks**: Periodic HUD corruption — minimap scrambles, false enemy markers, inverted health display for 8s bursts every 45s.
- **Enemy Mutation — Shared Awareness**: Alerting one enemy instantly alerts every enemy in the room. No stealth takedowns unless entire group eliminated simultaneously.
- **Boss Clause — System Possession**: Boss hijacks arena turrets, force fields, and traps. Environmental hazards become boss-controlled weapons.
- **District Interactions**:
  - **Cathedral (Amplify)**: Hymn pulses are stronger — wider AoE, shorter intervals. Navigating Cathedral becomes a timing puzzle.
  - **Neon Strip (Mitigate)**: Signal noise from advertisements provides cover — HUD hacks less frequent here.
  - **Datawell (Amplify)**: Data corruption spreads to player inventory — random item stats shuffle on district entry.

### 3. Plague Armada
- **Map Rule — Infection Clouds**: Toxic cloud zones spawn and drift across the map. Standing in them accumulates a Plague stack (max 10). At 10 stacks, rapid health drain until cleansed.
- **Enemy Mutation — Adaptive Resistance**: Enemies gain +15% resistance to the last damage type that hit them, stacking up to 3 times. Forces weapon switching.
- **Boss Clause — Adaptive Skin**: Boss gains resistance to the damage type used most in the current phase. Forces build diversity.
- **District Interactions**:
  - **Quarantine (Amplify)**: Plague stacks accumulate 2x faster but district completion grants permanent Plague immunity for the run.
  - **Garden (Mitigate)**: Natural remedies reduce Plague stacks by 3 on district entry. Herb pickups remove 1 stack each.
  - **Clinic (Amplify)**: Medical supplies are contaminated — healing items have 30% chance to add a Plague stack.

### 4. Gravity Storm
- **Map Rule — Float Pockets**: Low-gravity bubbles appear randomly (8m radius, 15s duration). Inside: jump height 3x, fall speed 0.3x, projectile arcs altered.
- **Enemy Mutation — Mobility Bursts**: Enemies periodically dash or leap unpredictably (every 6-10s). Movement prediction becomes unreliable.
- **Boss Clause — Gravity Shifts**: Arena gravity direction rotates 90 degrees per boss phase. Walls become floors. Reorientation window of 3s.
- **District Interactions**:
  - **Skyfall (Amplify)**: Signature vertical district gets permanent low-gravity. Enemies have adapted — player has not. Completion bonus: Gravity Boots relic.
  - **Foundry (Mitigate)**: Heavy industrial gravity anchors negate float pockets entirely in this district.
  - **Lattice (Amplify)**: Platforming sections gain moving gravity fields. One misstep = fall damage x2.

### 5. Quiet Crusade
- **Map Rule — Dead Zones**: Areas where all active abilities are silenced (no activation, existing buffs paused). Dead zones cover 30% of each room, visible as grey static overlays.
- **Enemy Mutation — EMP Weapons**: Enemy attacks drain 2s from ability cooldowns on hit. Melee enemies drain 4s. Getting swarmed effectively locks abilities.
- **Boss Clause — Cooldown Tax**: All player ability cooldowns +30% during the boss fight. Passive effects unaffected.
- **District Interactions**:
  - **Deadwave (Mitigate)**: District is already dead-zone themed — no additional penalty. Analog weapons deal +25% damage here.
  - **Cathedral (Amplify)**: Hymn pulses extend dead zone duration by 3s on contact. Cathedral becomes a dead-zone maze.
  - **Arsenal (Amplify)**: Weapon mods that rely on active abilities are disabled. Pure stat-stick builds favored.

### 6. Data Famine
- **Map Rule — Loot Scarcity**: Vendor inventories halved. Chest spawn rate -40%. Credit drops -25%. Information is currency — intel pickups worth 3x.
- **Enemy Mutation — Tougher Elites**: Elite enemies gain +50% HP and +30% damage. Standard enemies unchanged. Elite spawn rate unchanged.
- **Boss Clause — Fewer Adds, Deadlier**: Boss spawns zero add waves but deals +40% damage. Pure mechanical check.
- **District Interactions**:
  - **Bazaar (Amplify)**: Vendors may refuse to trade. Each purchase has a 20% chance of vendor closing shop permanently. Remaining stock discounted 50%.
  - **Datawell (Amplify)**: Data nodes give less intel per hack. But successfully hacking all nodes in a room reveals the entire floor map.
  - **Clinic (Mitigate)**: Medical supplies are untouched by famine. Full vendor stock, normal healing rates.

### 7. Black Budget
- **Map Rule — Stealth Routes**: Hidden passages open throughout districts (vent shafts, maintenance tunnels). Main corridors have increased patrol density.
- **Enemy Mutation — Ambushers**: 40% of enemies spawn cloaked (invisible until they attack or take damage). Cloak shimmer visible within 5m.
- **Boss Clause — Prepared Defenses**: Boss arena has pre-placed turrets, mine fields, and barrier walls. Arena must be navigated as well as fought.
- **District Interactions**:
  - **Garrison (Amplify)**: Military security at maximum — camera drones, laser grids, automated lockdowns on alert.
  - **Neon Strip (Mitigate)**: Crowds provide natural cover. Cloaked enemies cannot ambush while in crowd zones.
  - **Auction (Amplify)**: Black market items available (powerful but cursed). Purchasing triggers no Auditors but items have hidden debuffs.

### 8. Market Panic
- **Map Rule — Loot Volatility**: Every room rolls a loot multiplier (0.5x to 3x). Displayed on room entry. Some rooms are jackpots, others are empty.
- **Enemy Mutation — Merc Side-Swaps**: 15% of enemies are mercenaries who switch to player's side for 20s if the player is winning (>75% HP). They revert if player drops below 50%.
- **Boss Clause — Buyout Deals**: Boss offers mid-fight bargains at each phase transition — trade HP/resources for the boss skipping a mechanic. Risk/reward.
- **District Interactions**:
  - **Auction (Amplify)**: Bidding wars escalate — prices double but rare items appear 3x more frequently. Auditors offer protection for a fee.
  - **Bazaar (Amplify)**: Flash sales every 30s — random item at 90% off but only 5s window to purchase.
  - **Foundry (Mitigate)**: Industrial economy is stable. Standard loot rates, no volatility.

### 9. Memetic Wild
- **Map Rule — Thought Zones**: Areas that inflict random status effects by proximity (confusion, fear, rage, euphoria). Visual distortion and audio cues warn of entry. 6m radius, 10s duration.
- **Enemy Mutation — Status via Audio/Visual**: Enemies inflict status effects through their attack sounds and visual tells. Muting game audio does not help — ECS applies the debuff regardless of client audio state.
- **Boss Clause — Fake Phases**: Boss feigns death at each phase transition (3s false victory screen). Attacking during the feign deals reflected damage. Players must wait.
- **District Interactions**:
  - **Cathedral (Amplify)**: Hymn pulses carry memetic payloads — each pulse applies a random 5s status effect.
  - **Neon Strip (Amplify)**: Advertisement holograms become memetic traps. Walking through any hologram triggers a thought zone.
  - **Datawell (Mitigate)**: Data analysis provides memetic filters. Completing a hack grants 30s thought-zone immunity.

### 10. Nanoforge Bloom
- **Map Rule — Surface Reconfigure**: Floor and wall surface materials shift every 60s. Metal becomes organic, stone becomes crystal, etc. Surface-dependent abilities and resistances change dynamically.
- **Enemy Mutation — Reassemble**: Each enemy has a one-time revive. Upon first death, they collapse for 3s then reassemble at 40% HP. Destroying the corpse during the 3s window prevents revive.
- **Boss Clause — Regen Nodes**: Boss spawns 3 regen nodes at each phase. Each heals boss for 2% HP/s. Nodes must be destroyed before meaningful damage.
- **District Interactions**:
  - **Foundry (Amplify)**: Nanoforge overload — surface shifts every 30s (2x speed). Foundry-specific hazards double.
  - **Garden (Amplify)**: Organic overgrowth — vegetation actively attacks. Vine traps, spore clouds, animated root barriers.
  - **Quarantine (Mitigate)**: Containment protocols slow nanoforge spread. Surface shifts every 120s (half speed). Enemy reassemble takes 6s.

### 11. Sovereign Raid
- **Map Rule — Third-Party Raiders**: Neutral hostile squads (3-5 enemies) invade districts every 90s. They fight both the player and resident enemies. Killing them drops high-tier loot.
- **Enemy Mutation — Mixed Factions**: Enemy rooms contain mixed faction compositions. Faction abilities can interact unpredictably (friendly fire between factions).
- **Boss Clause — Raid Interruption**: At 50% boss HP, a raider squad invades the arena. Three-way fight for 30s, then raiders retreat.
- **District Interactions**:
  - **Garrison (Amplify)**: Raiders and garrison forces engage in full-scale battles. Massive firefights, collateral damage everywhere, but exceptional loot.
  - **Bazaar (Mitigate)**: Raiders avoid the bazaar (neutral commerce zone). Safe haven for restocking.
  - **Skyfall (Amplify)**: Raiders arrive via drop pods — vertical invasion. Falling debris becomes an additional hazard.

### 12. Time Fracture
- **Map Rule — Rewind Pockets**: Localized time-rewind fields (5m radius, 8s duration). Entering one rewinds player position 4s. Useful for dodging or disastrous if unaware. Visible as shimmering blue distortions.
- **Enemy Mutation — Reset Once**: Each enemy, upon reaching 0 HP for the first time, rewinds to full HP and resets position. Effectively doubles enemy count per room.
- **Boss Clause — Phase Rewind**: At 25% HP, boss rewinds to previous phase (50% HP, phase N-1 mechanics). Only triggers once.
- **District Interactions**:
  - **Datawell (Amplify)**: Hacking progress can be rewound — 25% chance per hack that it resets to 0%. Successful hacks grant double intel.
  - **Clinic (Amplify)**: Healing can be rewound — 20% chance that healing received in the last 5s is undone.
  - **Deadwave (Mitigate)**: Time fractures cannot form in dead zones. Deadwave is a safe district with no rewind pockets.

---

## ECS Blob Asset (Runtime)

### StrifeCardBlob

```csharp
// File: Assets/Scripts/Strife/Components/StrifeCardBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Blob asset baked from StrifeCardDefinitionSO for Burst-compatible runtime access.
    /// One blob per card, referenced from a singleton database entity.
    /// </summary>
    public struct StrifeCardBlob
    {
        public StrifeCardId CardId;
        public StrifeMapRule MapRule;
        public StrifeEnemyMutation EnemyMutation;
        public StrifeBossClause BossClause;

        /// <summary>Exactly 3 entries — one per district interaction.</summary>
        public BlobArray<StrifeDistrictInteractionBlob> DistrictInteractions;

        /// <summary>RunModifierDefinition hash for the map rule modifier.</summary>
        public int MapRuleModifierHash;
        /// <summary>RunModifierDefinition hash for the enemy mutation modifier.</summary>
        public int EnemyMutationModifierHash;
        /// <summary>RunModifierDefinition hash for the boss clause modifier.</summary>
        public int BossClauseModifierHash;
    }

    public struct StrifeDistrictInteractionBlob
    {
        public int DistrictId;
        public StrifeInteractionType InteractionType;
        public int ModifierSetHash;
        public float BonusRewardMultiplier;
    }

    /// <summary>
    /// Singleton holding blob references to all 12 Strife cards.
    /// Baked once, read by StrifeActivationSystem.
    /// </summary>
    public struct StrifeCardDatabase : IComponentData
    {
        public BlobAssetReference<StrifeCardDatabaseBlob> Blob;
    }

    public struct StrifeCardDatabaseBlob
    {
        /// <summary>Indexed by (StrifeCardId - 1). Length = 12.</summary>
        public BlobArray<StrifeCardBlob> Cards;
    }
}
```

---

## Setup Guide

1. **Create `Assets/Scripts/Strife/` folder** with subfolders: Components/, Definitions/, Systems/, Authoring/, Bridges/
2. **Create assembly definition** `Hollowcore.Strife.asmdef` referencing `DIG.Shared`, `Unity.Entities`, `Unity.NetCode`, `Unity.Collections`, `Unity.Burst`
3. Create all enum files in `Assets/Scripts/Strife/Components/`
4. Create `StrifeCardDefinitionSO.cs` in `Assets/Scripts/Strife/Definitions/`
5. Create `StrifeCardBlob.cs` in `Assets/Scripts/Strife/Components/`
6. **Create card assets**: `Assets/Data/Strife/Cards/` — one SO per card (12 total), named `StrifeCard_01_SuccessionWar.asset` through `StrifeCard_12_TimeFracture.asset`
7. Populate each SO with the data from the card definitions above
8. Create a `StrifeCardDatabaseAuthoring` MonoBehaviour that references all 12 SOs and bakes into the `StrifeCardDatabase` singleton blob

---

## Verification

- [ ] All 12 `StrifeCardId` enum values compile and match GDD §8.1 card list
- [ ] All 12 `StrifeMapRule`, `StrifeEnemyMutation`, `StrifeBossClause` enums have matching entries
- [ ] `StrifeCardDefinitionSO` inspector shows all fields and validates 3 district interactions
- [ ] 12 card SO assets created in `Assets/Data/Strife/Cards/` with correct data
- [ ] `StrifeCardBlob` and `StrifeCardDatabaseBlob` compile with Burst compatibility
- [ ] `StrifeCardDatabase` singleton entity baked from authoring
- [ ] Blob database indexed by `(StrifeCardId - 1)` returns correct card for all 12 IDs
- [ ] `StrifeDistrictInteraction` correctly categorizes each as Amplify or Mitigate
- [ ] No new components added to the player entity (all Strife data is singleton or expedition-scoped)

---

## Validation

```csharp
// File: Assets/Editor/StrifeWorkstation/StrifeCardValidator.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Hollowcore.Strife.Editor
{
    public static class StrifeCardValidator
    {
        [MenuItem("Hollowcore/Validation/Strife Card Definitions")]
        public static void ValidateAll()
        {
            // 1. District interaction matrix — detect over-concentration:
            //    - Build a Dictionary<int, List<StrifeCardId>> mapping DistrictId → cards
            //    - For each district: if referenced by > 3 cards, emit warning:
            //      "District {name} referenced by {count} Strife cards — may cause
            //       player fatigue in that district."
            //    - Expected: each district appears in 2-3 cards (36 interactions / ~10 districts)

            // 2. Modifier hash reference validation:
            //    - For each card, verify MapRuleModifierHash != 0
            //    - Verify EnemyMutationModifierHash != 0
            //    - Verify BossClauseModifierHash != 0
            //    - For each DistrictInteraction, verify ModifierSetHash != 0
            //    - Cross-reference: each hash must match an existing RunModifierDefinitionSO
            //      in Assets/Data/Modifiers/

            // 3. Card completeness:
            //    - All 12 StrifeCardId values must have a matching SO in Assets/Data/Strife/Cards/
            //    - Each card must have exactly 3 DistrictInteractions (not 2, not 4)
            //    - Each card must have non-None MapRule, EnemyMutation, BossClause

            // 4. Enum alignment:
            //    - StrifeMapRule enum count must match StrifeCardId count (12 + None)
            //    - StrifeEnemyMutation enum count must match
            //    - StrifeBossClause enum count must match
            //    - No duplicate enum values

            // 5. Visual asset completeness:
            //    - Each card SO must have: Icon, CardArt, AmbientLayer, RevealSting
            //    - ThemeColor must not be default white (indicates unset)

            Debug.Log("[StrifeCardValidator] Validation complete.");
        }
    }
}
```

---

## Editor Tooling

### Strife Card Designer Workstation

```csharp
// File: Assets/Editor/StrifeWorkstation/StrifeWorkstationWindow.cs
// EditorWindow — Window > Hollowcore > Strife Card Designer
//
// CRITICAL workstation for Strife card authoring and balancing.
// Follows DIG workstation pattern (sidebar tabs, IWorkstationModule).
//
// === Sidebar Tabs (IWorkstationModule) ===
//
// 1. "Card Editor" — StrifeCardEditorModule
//    - Visual card editor showing card art, effects, district interactions
//      in a card-game style layout (card art top, stats below, interactions at bottom)
//    - Inline editing of all StrifeCardDefinitionSO fields
//    - Live preview: card renders exactly as it will appear in the reveal animation
//    - Theme color picker with live vignette preview
//    - Audio preview: play AmbientLayer and RevealSting inline
//
// 2. "Interaction Matrix" — StrifeInteractionMatrixModule
//    - Heatmap grid: rows=12 Strife cards, columns=all districts
//    - Cell color: red=Amplify, blue=Mitigate, grey=no interaction
//    - Cell intensity: BonusRewardMultiplier mapped to opacity
//    - Column totals: how many cards interact with each district
//    - Row totals: always 3 per card (validation error if not)
//    - Click cell: jump to StrifeDistrictEffectSO inspector
//    - Over-concentration warning: column highlighted yellow if > 3 cards reference it
//
// 3. "Balance Rating" — StrifeBalanceModule
//    - Per-card difficulty rating (computed):
//      * MapRule severity score (0-10 scale based on gameplay impact)
//      * EnemyMutation severity score
//      * BossClause severity score
//      * Average district interaction multiplier
//      * Composite difficulty rating: weighted sum
//    - Bar chart comparing all 12 cards by difficulty
//    - Outlier detection: cards > 1.5 std dev from mean flagged
//    - Reward-to-difficulty ratio per card
//
// 4. "Card Gallery" — StrifeGalleryModule
//    - All 12 cards rendered side by side in mini card format
//    - Filter by: Amplify count, difficulty rating, affected district
//    - Drag to reorder (visual only, for presentation planning)
//
// === Shared Features ===
// - All modules operate on StrifeCardDefinitionSO assets
// - Changes are saved via Undo-aware SerializedObject editing
// - "Validate All" button runs StrifeCardValidator inline

// File: Assets/Editor/StrifeWorkstation/IStrifeWorkstationModule.cs
namespace Hollowcore.Strife.Editor
{
    public interface IStrifeWorkstationModule
    {
        string TabName { get; }
        void OnGUI(UnityEngine.Rect area);
        void OnSelectionChanged(Definitions.StrifeCardDefinitionSO card);
    }
}
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/StrifeWorkstation/StrifeBalanceSimulation.cs
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Strife.Editor
{
    /// <summary>
    /// Strife card balance simulation.
    /// Menu: Hollowcore > Simulation > Strife Card Balance
    /// </summary>
    public static class StrifeBalanceSimulation
    {
        [MenuItem("Hollowcore/Simulation/Strife Card Balance")]
        public static void RunBalanceSimulation()
        {
            // 1. Expected difficulty delta per card across all districts:
            //    - For each of the 12 cards:
            //      a. Base difficulty = MapRule severity + EnemyMutation severity
            //      b. For each district in the game:
            //         - If Amplify interaction: difficulty += interaction modifier strength
            //         - If Mitigate interaction: difficulty -= interaction modifier strength
            //         - If no interaction: difficulty unchanged
            //      c. Output: matrix of card × district → effective difficulty delta
            //    - Report districts that become excessively hard (>2x base) under any card

            // 2. Compound effect analysis (Strife + Front phase interaction):
            //    - For each card × district × Front phase (1-4):
            //      a. Compute combined modifier pressure:
            //         Map rule + enemy mutation + district interaction + Front phase scaling
            //      b. Flag combinations where compound difficulty > threshold:
            //         e.g., Plague Armada + Quarantine (Amplify) at Phase 4 = extreme
            //      c. Verify at least 1 Mitigate district exists per card for relief

            // 3. Strife rotation compound analysis (ascension loops):
            //    - Simulate 3-card sequences (cards A→B→C across 6 maps)
            //    - Flag sequences where 3 consecutive high-difficulty cards occur
            //    - Verify the used-card-mask prevents this for common seed ranges

            // 4. Reward balance:
            //    - Sum all BonusRewardMultiplier values per card
            //    - Verify Amplify interactions consistently reward > Mitigate
            //    - Verify total reward budget per card is within 10% of mean
        }
    }
}
```
