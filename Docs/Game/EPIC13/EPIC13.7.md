# EPIC 13.7: Vertical Slice — THE BURN

**Status**: Planning
**Epic**: EPIC 13 — District Content Pipeline
**Priority**: Critical — Second vertical slice district, showcases heat mechanics
**Dependencies**: 13.1-13.5 (all pipeline sub-epics), EPIC 3 (Front system), EPIC 5 (Echoes, optional), EPIC 14 (Boss: The Foreman)

---

## Overview

The Burn is District 6 — a perpetual industrial hell of slag rivers, smokestacks, and corporate prisoner workers. The defining mechanic is **heat management**: players accumulate heat from environmental hazards and enemy attacks, requiring coolant resources and strategic pathing. The Front type is Overheat Cascade — furnaces overload and spread lethal heat zones outward. The topology is linear-branching conveyor corridors connecting furnace chambers, with moving belts, vent timing puzzles, and coolant gates as traversal mechanics. Four factions themed around industrial apocalypse. 5 side goals focused on sabotage and liberation.

---

## Component Definitions

### HeatState (IComponentData)

```csharp
// File: Assets/Scripts/District/Burn/HeatComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.District.Burn
{
    /// <summary>
    /// Player heat accumulation state. Added to player entity on Burn district load.
    /// Heat is the district's primary environmental pressure mechanic.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct HeatState : IComponentData
    {
        /// <summary>Current heat level (0.0 = cool, 1.0 = overheating).</summary>
        [GhostField(Quantization = 1000)] public float CurrentHeat;

        /// <summary>Heat dissipation rate per second (base, modified by gear).</summary>
        [GhostField(Quantization = 100)] public float DissipationRate;

        /// <summary>Threshold above which player takes continuous heat damage.</summary>
        [GhostField(Quantization = 100)] public float OverheatThreshold;

        /// <summary>DPS when above OverheatThreshold.</summary>
        [GhostField(Quantization = 100)] public float OverheatDPS;
    }

    /// <summary>
    /// Burn district singleton config. Created at district load.
    /// </summary>
    public struct BurnConfig : IComponentData
    {
        public float AmbientHeatRate;       // Base heat gain per second in all zones
        public float FurnaceHeatRate;       // Heat gain near furnaces
        public float SlagRiverDPS;          // Direct damage from slag rivers
        public float CoolantGateReduction;  // Heat reduced when passing through coolant gate
        public float ConveyorSpeed;         // Moving belt speed
        public float VentCycleDuration;     // Seconds per vent open/close cycle
    }

    /// <summary>
    /// Zone-level heat modifier. Buffer on DistrictState entity.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ZoneHeatEntry : IBufferElementData
    {
        public int ZoneIndex;
        public float HeatMultiplier;     // 1.0 = ambient, 2.0+ = furnace zones
        public bool HasCoolantGate;
        public bool FurnaceActive;       // Disabled by sabotage side goal
    }
}
```

---

## District Topology

**9 zones** in linear-branching conveyor layout:

| Zone | Name | Type | Primary Faction | Connections |
|------|------|------|-----------------|-------------|
| 0 | Loading Docks | Combat | Waste Management | 1 |
| 1 | Conveyor Alpha | Combat | Slag Walkers | 0, 2, 3 |
| 2 | Smelting Floor | Elite | Slag Walkers | 1, 5 |
| 3 | Worker Barracks | Rest | Waste Management | 1, 4 |
| 4 | Recycler Pit | Combat | Scrap Hives | 3, 5 |
| 5 | Central Furnace | Elite | The Ashborn | 2, 4, 6 |
| 6 | Coolant Reservoir | Shop | Waste Management | 5, 7 |
| 7 | Ashborn Temple | Event | The Ashborn | 6, 8 |
| 8 | Furnace Heart | Boss | All factions | 7 |

**Topology Variants** (2):
- **Variant A (Direct)**: Linear path through conveyor Alpha, branch at zone 1
- **Variant B (Detour)**: Loading Docks collapse blocks zone 1 access, must route through Worker Barracks first; Smelting Floor has additional heat vent shortcut to zone 5

**Traversal Mechanics**:
- **Conveyor belts**: Moving platforms that push players along fixed paths. Can be ridden for speed or fought against
- **Coolant gates**: Environmental interactables that reduce player heat when activated (limited charges)
- **Vent timing**: Periodic steam vents block corridors on a cycle. Memorize timing or take damage
- **Slag rivers**: Lethal terrain — instant high DPS, no crossing without heat-resistant limbs

---

## Factions (Detailed)

### Faction 1: Slag Walkers (FactionId = 60)

```
Aggression: Berserker
Patrol: Stationary (fused to environment, activate on proximity)
Alarm Radius: 10m (localized, no coordination)

Enemies:
  - Molten Worker (Common, Cost 1): Melee, dripping slag. On-hit: applies heat
    to player (+0.05 per hit). Slow, predictable patterns.
  - Slag Spitter (Common, Cost 2): Ranged, launches molten projectiles. Creates
    small slag pools on impact (5s duration, heat + damage).
  - Furnace Amalgam (Elite, Cost 5): Large, multiple fused workers. 3 health
    bars (one per fused body). Each bar depleted detaches a body that fights
    independently as a Molten Worker.
  - Heat Core (Special, Cost 3): Immobile. Radiates heat in 10m radius. Must
    be destroyed to make zone traversable. Explodes on death (dodge window).
```

### Faction 2: Waste Management (FactionId = 61)

```
Aggression: Defensive
Patrol: Waypoint (enforcer routes)
Alarm Radius: 25m (radio network)

Enemies:
  - Enforcer (Common, Cost 2): Ranged, incendiary rounds. Wears heat armor
    (50% heat damage reduction). Standard cover-shooter AI.
  - Incendiary Drone (Common, Cost 1): Flying, drops fire bombs.
    Low HP but hard to hit. Prioritizes area denial.
  - Heat Warden (Elite, Cost 4): Heavy armor, flamethrower. Creates fire
    walls that block corridors. Must be flanked.
  - Quota Master (Special, Cost 5): Non-combat commander. Buffs all nearby
    Waste Management: +20% damage, +30% fire rate. Kill priority target.
```

### Faction 3: The Ashborn (FactionId = 62)

```
Aggression: Aggressive
Patrol: Roaming (fire-worship circuits)
Alarm Radius: 20m (flame signal)

Enemies:
  - Ash Acolyte (Common, Cost 1): Melee, self-immolating. On death: fire
    explosion (small AOE). Charges in groups.
  - Flame Preacher (Common, Cost 2): Ranged, throws fire orbs that leave
    burning ground. Chants buff nearby Ashborn (heal 2% HP/s).
  - Reforged (Elite, Cost 5): Heavily augmented, fire-immune. Uses forge
    hammer with shockwave attack. At 25% HP: enters Crucible state
    (immune for 5s, heals to 50%, then vulnerable with no armor).
  - Pyre Master (Special, Cost 6): Summons fire pillars in patterns.
    Creates zone-wide heat pulse every 30s. Must be killed quickly.
```

### Faction 4: Scrap Hives (FactionId = 63)

```
Aggression: Aggressive
Patrol: Swarm (erratic, hive-directed)
Alarm Radius: 35m (hive mind signal)

Enemies:
  - Scrap Drone (Common, Cost 1): Tiny, fast, swarm AI. Low damage individually
    but attacks in groups of 5-8. Rippable: salvage parts.
  - Recycler (Common, Cost 2): Medium, melee. Absorbs scrap from destroyed
    allies, gaining armor per absorbed drone. Max 5 stacks.
  - Hive Node (Elite, Cost 4): Stationary spawner. Produces 2 Scrap Drones
    every 10s. Must be destroyed to stop reinforcements. Protected by
    Recycler guards.
  - Scrap Colossus (Special, Cost 7): Boss-tier swarm entity. Massive construct
    of fused scrap. Sheds Scrap Drones when damaged. Final form: exposed
    core with high damage but low HP.
```

---

## Front: Overheat Cascade

### Phase Definitions

```
Phase 1 — Warm (0:00 - 3:00)
  - Ambient heat +50% in zones adjacent to Central Furnace (zone 5)
  - Slag Walker spawn rate +20% in furnace zones
  - Pulse: Furnace Vent (random zone, 10s of intense heat)

Phase 2 — Hazard (3:00 - 6:00)
  - Slag rivers expand: previously safe corridors now have slag patches
  - Coolant gate charges reduced by 50%
  - Waste Management aggression escalates to Aggressive
  - Pulse: Quota Crackdown (Waste Management elites spawn in 2 zones)

Phase 3 — Furnace Heart Exposed (6:00 - 9:00)
  - Central Furnace (zone 5) ambient heat = lethal without heat-resistant gear
  - Ashborn receive fire immunity
  - Conveyor belts speed doubles (harder to traverse)
  - Pulse: Recycler Burst (Scrap Hive mass spawn in 3 zones)

Phase 4 — Flashover (9:00+)
  - All zones: ambient heat maximal, constant overheat damage
  - All factions: Berserker aggression
  - Slag rivers spread to cover 40% of traversable area
  - Boss zone (8) unlocks regardless of main chain progress
  - Pulse: Continuous (escalating heat + enemy spawns)
```

---

## Side Goals (5)

| # | Name | GoalType | Objectives | Boss Insurance | Front Counterplay |
|---|------|----------|------------|----------------|-------------------|
| 1 | Free the Chain Gang | Rescue | Interact with 3 prisoner locks in zones 0,1,3 | RemoveAdd: Prisoner-shield phase in boss fight | None |
| 2 | Heat-Cracked Chassis | Collect | Collect 2 heat-resistant limbs from Slag Walker elites | RevealWeakpoint: Foreman coolant port | SlowSpread (40s) |
| 3 | The Firewalker's Arm | Collect | Kill the Reforged in zone 7, rip its arm | DisableAbility: Forge Hammer shockwave | None |
| 4 | Cool the Core | Destroy | Activate 4 emergency coolant valves in zones 2,4,5,6 | ReduceHealth: Boss -20% max HP | PurgeZone (zone 5) |
| 5 | The Whistleblower | Stealth | Retrieve corporate data from zone 3 without alerting Waste Management | DisablePhase: Quota Crackdown escalation | DelayPhase (90s) |

---

## Main Chain: Overload the Central Furnace

| Step | Objective | Zone |
|------|-----------|------|
| 0 | Reach the Smelting Floor (zone 2) — survey the furnace complex | 2 |
| 1 | Retrieve the Override Key from the Quota Master (zone 0 or 3) | 0/3 |
| 2 | Sabotage the Central Furnace coolant regulators | 5 |
| 3 | Enter the Furnace Heart and confront The Foreman | 8 |

---

## Boss: THE FOREMAN

Defined fully in EPIC 14. Summary:
- Arena: Furnace Heart (zone 8) — circular chamber with slag moat, conveyor platforms
- Phase 1: Industrial machinery attacks (crane swings, conveyor traps, furnace blasts)
- Phase 2: Calls prisoner workers as shields (disabled by goal 1)
- Phase 3: Forge Hammer combat (shockwave disabled by goal 3), coolant port exposed (revealed by goal 2)
- Insurance effects remove specific mechanics for a cleaner fight

---

## Echo Theme: Heat Debt

- **Debuff echoes**: Scorch Curse — persistent heat vulnerability, player takes +25% heat damage on subsequent runs in any district
- **Reward echoes**: Tempered Materials — heat-resistant crafting materials drop from any enemy in future runs (unique to Burn completion)
- **Skipped goal echoes**: Freed prisoners become hostile ghosts in future runs; Firewalker's Arm appears as an enemy-wielded weapon

---

## Reanimation

When the player dies in The Burn, the Ashborn forge their body into a heat-servant:
- Fire-immune enemy with the player's weapon loadout
- Appears in the zone of death, patrols near slag rivers
- Drops the player's lost gear on defeat (heat-damaged, reduced durability)

---

## POIs

| Landmark | Zone | Interaction | Description |
|----------|------|-------------|-------------|
| Coolant Cache | 6 | Vendor | Emergency coolant supply. Buy heat-reduction consumables |
| The Smelter | 2 | None | Massive industrial set piece — molten metal waterfalls, ambient heat |
| Quota Board | 3 | LoreTerminal | Corporate production quotas. Lore about prisoner labor system |
| Ashborn Temple | 7 | HealStation | Fire-worship altar. Heals but adds heat (+0.2 CurrentHeat) |
| Furnace Heart | 8 | None | Boss arena entrance — visible from multiple zones as orientation landmark |

---

## Setup Guide

1. **Create `Assets/Data/Districts/Burn/` folder** with subfolders: Factions/, Goals/, POIs/, Encounters/, Rooms/
2. **Implement HeatState system**: `HeatAccumulationSystem` (accumulates heat from environment), `HeatDamageSystem` (applies damage above threshold), `HeatDissipationSystem` (passive cooling)
3. **Author DistrictDefinitionSO**: `Burn_District.asset`
4. **Author 4 FactionDefinitionSOs** per faction detail above
5. **Create 12+ enemy prefabs** (3 per faction):
   - Slag Walkers: heat-on-hit mechanic via custom damage modifier
   - Scrap Hives: swarm AI behavior profile
   - Ashborn: fire immunity component, self-immolation death effect
6. **Author 5 QuestDefinitionSOs** for side goals + 1 main chain (4 steps)
7. **Author FrontDefinitionSO**: Overheat Cascade with 4 phases
8. **Create conveyor belt system**: moving platform entities with configurable speed and direction
9. **Create coolant gate interactables**: limited-charge heat reduction using Interaction/ framework
10. **Create 2 TopologyVariants** with zone graph overrides

---

## Verification

- [ ] HeatState accumulates heat from environment at correct rate per zone
- [ ] Overheat threshold triggers continuous damage
- [ ] Coolant gates reduce heat and consume charges correctly
- [ ] Conveyor belts move player at configured speed
- [ ] Vent timing blocks corridors on correct cycle
- [ ] Slag rivers deal lethal DPS on contact
- [ ] 4 factions spawn in assigned zones with correct behaviors
- [ ] Slag Walkers apply heat on melee hit
- [ ] Scrap Hive swarm AI spawns from Hive Nodes
- [ ] Ashborn fire immunity and self-immolation death work
- [ ] Overheat Cascade Front spreads heat from Central Furnace outward
- [ ] Phase transitions modify environment (slag expansion, belt speed, coolant reduction)
- [ ] All 5 side goals completable
- [ ] Boss insurance effects recorded correctly
- [ ] Main chain unlocks Furnace Heart boss zone
- [ ] Heat-themed echo effects apply on subsequent runs
- [ ] Reanimation produces heat-servant enemy with player's loadout

---

## Live Tuning

| Parameter | Singleton | Range | Default |
|-----------|-----------|-------|---------|
| AmbientHeatRate | BurnConfig | 0.01–0.2/s | 0.05 |
| FurnaceHeatRate | BurnConfig | 0.1–1.0/s | 0.3 |
| SlagRiverDPS | BurnConfig | 10–100 | 50 |
| CoolantGateReduction | BurnConfig | 0.1–0.5 | 0.3 |
| ConveyorSpeed | BurnConfig | 1.0–10.0 | 3.0 |
| VentCycleDuration | BurnConfig | 2.0–15.0s | 5.0 |
| OverheatThreshold | HeatState | 0.5–1.0 | 0.8 |
| OverheatDPS | HeatState | 1.0–20.0 | 5.0 |
| DissipationRate | HeatState | 0.01–0.2/s | 0.05 |

---

## Debug Visualization

```csharp
// File: Assets/Scripts/District/Burn/Debug/BurnDebugOverlay.cs
// Development builds:
//   - Heat level per zone: color-coded text (blue=cool, yellow=warm, red=hot)
//   - Player heat bar: large floating bar above player (in addition to HUD)
//   - Slag river boundaries: red wireframe on lethal terrain
//   - Conveyor belt vectors: arrows showing direction and speed
//   - Vent timing: countdown timers above each vent
//   - Coolant gate charges: "Charges: 2/5" text above each gate
//   - Zone heat multiplier: floating number per zone "x2.0"
//   - Furnace active/disabled state: icon on zone centroid
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/District/BurnTest.cs
// [Test] HeatAccumulation_AmbientRate
//   Place player in zone with HeatMultiplier=1.0, verify CurrentHeat increases
//   at AmbientHeatRate per second (within 5% tolerance over 10s).
//
// [Test] HeatDamage_AboveThreshold
//   Set CurrentHeat to 0.9 (above OverheatThreshold=0.8),
//   verify health decreases at OverheatDPS rate.
//
// [Test] CoolantGate_Reduction
//   Trigger coolant gate at CurrentHeat=0.7, verify heat drops
//   by CoolantGateReduction (0.3 → 0.4).
//
// [Test] OverheatCascade_PhaseProgression
//   Verify Front phases transition at correct timing,
//   slag expansion occurs at Phase 2, belt speed doubles at Phase 3.
//
// [Test] TopologyVariants_2Distinct
//   Generate Burn with seeds selecting variants A and B,
//   verify Variant B blocks zone 1 access initially.
```
