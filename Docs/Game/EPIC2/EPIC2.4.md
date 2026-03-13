# EPIC 2.4: Body Reanimation

**Status**: Planning
**Epic**: EPIC 2 — Soul Chip, Death & Revival
**Dependencies**: EPIC 2.2 (Body Persistence); Framework: AI/, Combat/, CorpseLifecycle; EPIC 3 (Front, optional trigger)

---

## Overview

The district does not waste resources. When a player dies, the local power structure claims the body and converts it into something hostile. Each of the 15 districts has a unique reanimation type rooted in its narrative identity — Necrospire raises recursive specters, Old Growth assimilates augments into root networks, Mirrortown steals your face. The reanimated enemy uses the player's equipped weapons and limb stats, scaled by difficulty, and is classified as a mini-boss. Defeating it recovers your gear plus district-modified bonus loot. In co-op, teammates fight "you."

---

## Component Definitions

```csharp
// File: Assets/Scripts/SoulChip/Components/ReanimationComponents.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Current reanimation state of a dead body. Added to DeadBodyState entities
    /// when the district begins claiming them.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct ReanimationState : IComponentData
    {
        /// <summary>Reanimation type id (maps to ReanimationDefinitionSO).</summary>
        public int ReanimationTypeId;

        /// <summary>District performing the reanimation.</summary>
        public int DistrictId;

        /// <summary>Progress toward full reanimation (0.0 to 1.0).</summary>
        public float Progress;

        /// <summary>Time (elapsed seconds) when reanimation started.</summary>
        public double StartTime;

        /// <summary>Duration in seconds for full conversion.</summary>
        public float Duration;

        /// <summary>Phase of the reanimation process.</summary>
        public ReanimationPhase Phase;

        /// <summary>SoulId of the original player (for loadout lookup).</summary>
        public int OriginalSoulId;

        /// <summary>Entity of the spawned reanimated enemy (Entity.Null until spawn).</summary>
        public Entity ReanimatedEntity;
    }

    /// <summary>
    /// Phases of body reanimation. Visual and gameplay progression.
    /// </summary>
    public enum ReanimationPhase : byte
    {
        /// <summary>Body is unclaimed. Normal dead body.</summary>
        Unclaimed = 0,
        /// <summary>District forces are approaching/affecting the body. Visual cues begin.</summary>
        Claiming = 1,
        /// <summary>Active transformation. Body visibly changes. Interruptible window.</summary>
        Transforming = 2,
        /// <summary>Reanimation complete. Enemy entity spawned. Body entity consumed.</summary>
        Complete = 3,
    }

    /// <summary>
    /// Placed on the reanimated enemy entity. Links back to the original body data
    /// for gear recovery on defeat.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct ReanimatedEnemy : IComponentData
    {
        /// <summary>SoulId of the player whose body this was.</summary>
        public int OriginalSoulId;

        /// <summary>District reanimation type (for visual theming and loot mods).</summary>
        public int ReanimationTypeId;

        /// <summary>Difficulty scaling multiplier applied to base stats.</summary>
        public float DifficultyScale;

        /// <summary>Whether this reanimated enemy has been defeated.</summary>
        public bool IsDefeated;
    }

    /// <summary>
    /// Buffer on the reanimated enemy storing the original player's equipped loadout.
    /// Used to configure the enemy's weapons and abilities.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ReanimatedLoadoutEntry : IBufferElementData
    {
        public int ItemDefinitionId;
        public int SlotIndex;
        public float StatScale;  // Difficulty-adjusted stat multiplier
    }

    /// <summary>
    /// Buffer on the reanimated enemy storing bonus loot dropped on defeat.
    /// District-specific modifications to the original gear.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ReanimationBonusLoot : IBufferElementData
    {
        public int ItemDefinitionId;
        public int Quantity;
    }

    /// <summary>
    /// Enableable marker on dead body entities. Enabled when the body enters
    /// the reanimation pipeline. Prevents interaction/looting during transformation.
    /// </summary>
    public struct ReanimationInProgress : IComponentData, IEnableableComponent { }
}
```

---

## ScriptableObject Definition

```csharp
// File: Assets/Scripts/SoulChip/Definitions/ReanimationDefinitionSO.cs
using UnityEngine;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Per-district reanimation configuration. Defines how a district
    /// transforms dead player bodies into enemies.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollowcore/Revival/Reanimation Definition")]
    public class ReanimationDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int ReanimationTypeId;
        public string ReanimationName;
        public int DistrictId;

        [Header("Timing")]
        [Tooltip("Seconds before the district begins claiming a dead body")]
        public float ClaimDelay = 30f;
        [Tooltip("Seconds for the transformation phase")]
        public float TransformDuration = 15f;
        [Tooltip("If true, Front proximity accelerates reanimation")]
        public bool FrontAccelerated = true;

        [Header("Enemy Configuration")]
        [Tooltip("Base enemy prefab for this reanimation type")]
        public GameObject EnemyPrefab;
        [Tooltip("Difficulty scaling multiplier (1.0 = match player stats)")]
        public float DifficultyScale = 1.0f;
        [Tooltip("Whether the enemy uses the player's actual weapons")]
        public bool UsesPlayerWeapons = true;
        [Tooltip("Whether the enemy uses the player's limb stats")]
        public bool UsesPlayerLimbs = true;

        [Header("Loot")]
        [Tooltip("Bonus loot table dropped in addition to recovered gear")]
        public LootTableReference BonusLootTable;

        [Header("Visuals")]
        [Tooltip("VFX played during Claiming phase")]
        public GameObject ClaimingVFX;
        [Tooltip("VFX played during Transforming phase")]
        public GameObject TransformingVFX;
        [Tooltip("Material override applied to the reanimated body mesh")]
        public Material ReanimationMaterial;

        [Header("Narrative")]
        [TextArea(2, 4)]
        public string FlavorText;
    }
}
```

---

## District Reanimation Types

```
// 15 district-specific reanimation types:
//
// 1. NECROSPIRE — Recursive Specter
//    Wearing your face, knows your loadout. Flickers between solid and spectral.
//    Special: phases through walls briefly, immune to physical during phase.
//
// 2. OLD GROWTH — Root Runner
//    Augments assimilated into the Garden. Organic tendrils replace limbs.
//    Special: regenerates health near vegetation, leaves root trail that slows.
//
// 3. THE NURSERY — Pattern Learner
//    AI learns your combat patterns from neural data. Mimics your playstyle.
//    Special: adapts to repeated attack patterns, increasing dodge chance.
//
// 4. THE QUARANTINE — Plague Mutant
//    Plague mutates through your body, wearing your armor.
//    Special: melee attacks apply infection DoT, explodes plague cloud on death.
//
// 5. CHROME CATHEDRAL — Ascended Faithful
//    Faithful "ascend" your corpse. Fights alongside Seraphim constructs.
//    Special: gains holy shield periodically, summons 1-2 Seraphim adds.
//
// 6. MIRRORTOWN — Hollow One (Face Thief)
//    Takes your face. In co-op, teammates may not recognize it on radar.
//    Special: appears as friendly on minimap until within 15m, ambush opener.
//
// 7. GLITCH QUARTER — Loop Echo
//    Body caught in temporal loop. Multiple slightly-wrong copies.
//    Special: spawns 2-3 copies at 50% stats, real one has full stats + loot.
//
// 8. THE UNDERGRID — Wireframe Revenant
//    Infrastructure absorbs the body. Electrical conduit weaponry.
//    Special: area denial with electric ground patches, immune to shock damage.
//
// 9. RUSTFIELD — Corroded Hulk
//    Corrosion fuses with augments. Slow but extremely durable.
//    Special: armor increases over time, corrosive melee reduces target armor.
//
// 10. THE SINK — Pressure Wraith
//     Pressure differential animates the corpse. Distorted proportions.
//     Special: pulls nearby players toward it (gravity well), crush damage.
//
// 11. CANAL DISTRICT — Drowned Operative
//     Submerged remains rebuilt by canal machinery. Waterlogged movement.
//     Special: creates water hazard zones, gains speed buff in water tiles.
//
// 12. NEON SPRAWL — Signal Ghost
//     Advertising AIs hijack the neural implants. Broadcasts your location.
//     Special: periodically reveals all players on map, flashbang burst attack.
//
// 13. BLACKSITE OMEGA — Classified Asset
//     Military protocols activate. Full tactical AI, coordinated movement.
//     Special: calls reinforcement drones, uses cover system aggressively.
//
// 14. EDEN PARK — Overgrown Vessel
//     Bioengineered flora claims the body as a host.
//     Special: area heal for nearby enemies, releases spore cloud on death.
//
// 15. THE HOLLOWS — Void Shell
//     The emptiness fills the body. Silent, no footsteps, minimal visual tell.
//     Special: invisible outside of 10m range, one-shot ambush attack from stealth.
```

---

## Systems

### ReanimationTimerSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/ReanimationTimerSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DeadBodyCreationSystem
//
// Manages the reanimation timer on dead bodies:
//   1. For each DeadBodyState where IsReanimated == false:
//      a. If no ReanimationState exists yet:
//         - Look up district's ReanimationDefinitionSO
//         - If elapsed time since DeathTime > ClaimDelay:
//           Add ReanimationState (Phase = Claiming, Progress = 0)
//           Enable ReanimationInProgress
//      b. If ReanimationState exists:
//         - Advance Progress based on elapsed time / Duration
//         - If FrontAccelerated and Front is within proximity: multiply rate by 2x
//         - Transition phases:
//           Progress 0.0-0.3: Claiming (visual cues, interruptible)
//           Progress 0.3-1.0: Transforming (active conversion, visual distortion)
//           Progress >= 1.0: Complete → trigger ReanimationSpawnSystem
//   2. Bodies that are IsLooted still get reanimated (district uses the frame)
//   3. Set DeadBodyState.IsReanimated = true when phase reaches Complete
```

### ReanimationSpawnSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/ReanimationSpawnSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ReanimationTimerSystem
//
// Spawns the reanimated enemy when transformation completes:
//   1. For each ReanimationState where Phase == Complete and ReanimatedEntity == Entity.Null:
//      a. Look up ReanimationDefinitionSO by ReanimationTypeId
//      b. Instantiate EnemyPrefab at DeadBodyState.WorldPosition
//      c. Add ReanimatedEnemy component:
//         - OriginalSoulId from ReanimationState
//         - ReanimationTypeId, DifficultyScale from definition
//      d. Populate ReanimatedLoadoutEntry buffer:
//         - Read DeadBodyInventoryEntry from the dead body
//         - Copy equipped weapons, apply DifficultyScale to stats
//      e. If UsesPlayerLimbs: configure chassis from DeadBodyLimbEntry
//      f. Populate ReanimationBonusLoot from definition's BonusLootTable
//      g. Configure AI brain:
//         - Set as mini-boss (higher aggro range, no leash)
//         - District-specific special abilities from definition
//      h. Store spawned entity in ReanimationState.ReanimatedEntity
//      i. Disable the dead body entity (consumed by reanimation)
//      j. Fire ReanimationEvent for UI notification + co-op callout
```

### ReanimationDefeatSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/ReanimationDefeatSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DeathTransitionSystem
//
// Handles loot and recovery when a reanimated enemy is killed:
//   1. For each ReanimatedEnemy where DeathState == Dead and !IsDefeated:
//      a. Set IsDefeated = true
//      b. Drop original player gear from ReanimatedLoadoutEntry buffer:
//         - Spawn loot entities at death position
//         - Gear is the original quality (not difficulty-scaled)
//      c. Drop ReanimationBonusLoot:
//         - District-specific bonus items
//      d. Fire ReanimationDefeatedEvent:
//         - UI notification: "You reclaimed your body's gear"
//         - Co-op: notify all party members
//      e. Grant XP bonus for defeating a reanimated mini-boss
//      f. Update DeadBodyState.IsReanimated (for Scar Map icon change)
```

---

## Interruptible Window

```
// During the Claiming phase (Progress 0.0-0.3), players can interrupt reanimation:
//   - Approach the body and interact: "Reclaim Body"
//   - Cancels reanimation, removes ReanimationState
//   - Body returns to normal dead body state (lootable)
//   - Costs nothing but requires reaching the body in time
//
// During Transforming phase (0.3-1.0):
//   - Body cannot be interacted with (ReanimationInProgress blocks interaction)
//   - Dealing enough damage to the transforming body can stagger it:
//     * Damage threshold: 30% of the reanimated enemy's projected max health
//     * Stagger resets Progress by 0.15, giving more time
//     * Can only stagger once per transformation
//
// Once Complete: reanimated enemy must be fought and killed normally.
```

---

## Setup Guide

1. Create `ReanimationDefinitionSO` assets for each district (15 total)
2. Create reanimated enemy prefab variants per district: base enemy prefab + visual overrides
3. Configure each definition: claim delay, transform duration, difficulty scale, special abilities
4. Add `ReanimationInProgress` (IEnableableComponent) to dead body entity baker, baked disabled
5. Create VFX for Claiming phase (district-themed tendrils/energy approaching body)
6. Create VFX for Transforming phase (body visibly changing, district material override)
7. Hook ReanimationTimerSystem to Front proximity data (EPIC 3) for accelerated timing
8. Configure mini-boss AI profile: extended aggro range, no leash distance, boss health bar
9. Add reanimation loot tables to each district's ReanimationDefinitionSO
10. Create co-op callout: "Warning: [PlayerName]'s body is being claimed by [DistrictName]"

---

## Verification

- [ ] Dead body begins Claiming phase after ClaimDelay seconds
- [ ] Claiming phase shows district-appropriate VFX
- [ ] Player can interrupt during Claiming phase via interaction
- [ ] Transforming phase blocks body interaction
- [ ] Damage stagger during Transforming resets progress (once only)
- [ ] Front proximity accelerates reanimation timer (2x rate)
- [ ] Reanimated enemy spawns at body location on completion
- [ ] Enemy uses player's equipped weapons from DeadBodyInventoryEntry
- [ ] Enemy uses player's limb stats from DeadBodyLimbEntry (if configured)
- [ ] Enemy scaled by DifficultyScale from ReanimationDefinitionSO
- [ ] Enemy classified as mini-boss (boss health bar, no leash)
- [ ] Defeating reanimated enemy drops original gear + bonus loot
- [ ] XP bonus granted for reanimated mini-boss kill
- [ ] Looted bodies still get reanimated (district uses the frame)
- [ ] District-specific special abilities function (at least 1 tested)
- [ ] Co-op: Mirrortown Hollow One appears friendly on minimap
- [ ] Co-op: Glitch Quarter Loop Echo spawns decoy copies
- [ ] Scar Map icon updates when body is reanimated/defeated
- [ ] ReanimationDefinitionSO per-district timing is respected

---

## BlobAsset Pipeline

```csharp
// File: Assets/Scripts/SoulChip/Definitions/ReanimationBlob.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Burst-compatible reanimation config baked from ReanimationDefinitionSO.
    /// Indexed by ReanimationTypeId at runtime.
    /// </summary>
    public struct ReanimationBlob
    {
        public int ReanimationTypeId;
        public int DistrictId;
        public float ClaimDelay;
        public float TransformDuration;
        public bool FrontAccelerated;
        public float DifficultyScale;
        public bool UsesPlayerWeapons;
        public bool UsesPlayerLimbs;
        public BlobString ReanimationName;
        public BlobString FlavorText;
    }

    /// <summary>
    /// Database blob containing all reanimation definitions.
    /// Singleton BlobAssetReference on a bootstrap entity.
    /// </summary>
    public struct ReanimationDatabase
    {
        public BlobArray<ReanimationBlob> Entries; // Indexed by ReanimationTypeId
    }
}

// ReanimationDefinitionSO.BakeToBlob() populates one ReanimationBlob entry.
// Baker collects all 15 district definitions into a single ReanimationDatabase blob.
```

---

## Validation

```csharp
// File: Assets/Editor/Validation/ReanimationValidation.cs
// OnValidate() rules for ReanimationDefinitionSO:
//
// - ReanimationTypeId must be unique across all definitions (1-15)
// - DistrictId must match a valid district
// - ClaimDelay must be > 0 (minimum 5s recommended)
// - TransformDuration must be > 0 (minimum 5s)
// - DifficultyScale must be in [0.5, 3.0]
// - EnemyPrefab must not be null
// - BonusLootTable must not be null
// - ClaimingVFX and TransformingVFX should not be null (warning, not error)
// - ReanimationMaterial should not be null (warning)
//
// Build-time scan:
// - Verify exactly 15 ReanimationDefinitionSO assets (one per district)
// - Verify no duplicate ReanimationTypeId or DistrictId values
// - Verify all EnemyPrefab references have required AI components (AIBrain, Health, etc.)
```

---

## Editor Tooling

**Reanimation Preview Inspector**:
- Custom Inspector for `ReanimationDefinitionSO`: shows district name header with themed color
- Timeline preview: visual bar showing ClaimDelay -> Claiming (30%) -> Transforming (70%) -> Complete with labeled durations
- Enemy stat preview: projected HP/damage based on DifficultyScale applied to reference player stats
- Loot table preview: inline expansion showing BonusLootTable items and drop chances

---

## Debug Visualization

**Reanimation State Overlay** (toggle via debug menu):
- In-world gizmo at each dead body with active ReanimationState:
  - Progress ring: fills from 0% to 100% around the body position
  - Phase color: blue (Unclaimed) -> yellow (Claiming) -> orange (Transforming) -> red (Complete)
  - Timer text: remaining seconds until next phase
- Spawn prediction marker: shows where the reanimated enemy will appear
- Front proximity indicator: line from body to nearest Front boundary with distance label

**Activation**: Debug menu toggle `Death/Reanimation/ShowProgress`
