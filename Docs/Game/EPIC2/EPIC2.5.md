# EPIC 2.5: Death Spiral & Full Wipe

**Status**: Planning
**Epic**: EPIC 2 — Soul Chip, Death & Revival
**Dependencies**: EPIC 2.1 (SoulChip), EPIC 2.2 (Body Persistence), EPIC 2.3 (Revival); EPIC 2.4 (Reanimation, optional)

---

## Overview

Each death in Hollowcore makes the next death worse. Revival bodies near your last death get consumed, previous bodies reanimate and turn hostile, your soul chip degrades further, and the spatial search for a viable body pushes deeper into dangerous territory or back through cleared districts. The expedition becomes a body-recovery roguelike within the roguelite. When all revival options are exhausted — no bodies, no drone insurance, no continuity caches — the expedition ends in a full wipe. Run-level progress is lost, but Compendium entries from completed districts survive as meta-progression.

---

## Component Definitions

```csharp
// File: Assets/Scripts/SoulChip/Components/DeathSpiralComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Tracks death statistics for the current expedition.
    /// On the player entity. Drives revival body spawn logic and spiral escalation.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DeathSpiralState : IComponentData
    {
        /// <summary>Total deaths this expedition.</summary>
        [GhostField] public int TotalDeaths;

        /// <summary>Deaths in the current district.</summary>
        [GhostField] public int DistrictDeaths;

        /// <summary>Current district id (reset DistrictDeaths on change).</summary>
        [GhostField] public int CurrentDistrictId;

        /// <summary>Number of revival bodies remaining in the expedition (across all districts).</summary>
        [GhostField] public int RemainingRevivalBodies;

        /// <summary>Number of drone insurance charges remaining.</summary>
        [GhostField] public int RemainingDroneCharges;

        /// <summary>Number of continuity caches remaining.</summary>
        [GhostField] public int RemainingCaches;

        /// <summary>Chip degradation tier (mirrored from SoulChipState for UI convenience).</summary>
        [GhostField] public byte ChipDegradationTier;

        /// <summary>Whether the player is in a wipe state (no revival options).</summary>
        [GhostField] public bool IsWiped;
    }

    /// <summary>
    /// Buffer tracking death locations for the expedition. Used by spiral logic
    /// to exclude previous revival locations and by Scar Map for skull icons.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DeathLocationEntry : IBufferElementData
    {
        public int DistrictId;
        public int ZoneId;
        public Unity.Mathematics.float3 Position;
        public double DeathTime;
        /// <summary>Whether a revival body at this location was used.</summary>
        public bool RevivalUsed;
        /// <summary>Whether the body at this location was reanimated.</summary>
        public bool WasReanimated;
    }

    /// <summary>
    /// Per-district death counter. Buffer on the player entity.
    /// Used to determine revival body placement and spiral escalation per district.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct DistrictDeathCounter : IBufferElementData
    {
        public int DistrictId;
        public int DeathCount;
        /// <summary>Number of revival bodies consumed in this district.</summary>
        public int BodiesUsed;
        /// <summary>Number of bodies reanimated in this district.</summary>
        public int BodiesReanimated;
    }

    /// <summary>
    /// Transient event entity. Created when a full wipe is detected.
    /// Consumed by WipeExecutionSystem.
    /// </summary>
    public struct WipeEvent : IComponentData
    {
        /// <summary>SoulId of the wiped player (solo) or -1 for party wipe.</summary>
        public int SoulId;

        /// <summary>Party id if co-op wipe (-1 for solo).</summary>
        public int PartyId;

        /// <summary>Total deaths at time of wipe.</summary>
        public int TotalDeaths;

        /// <summary>District where the final death occurred.</summary>
        public int FinalDistrictId;
    }
}
```

---

## Spiral Escalation Rules

```
// Death spiral escalation (driven by DeathSpiralState.TotalDeaths):
//
// DEATH 1:
//   - Revival bodies: 1 Cheap (nearby), 1 Mid (moderate distance), 0-1 Premium
//   - Chip degradation: Tier 0 (no penalty, transfers 1-2 are free)
//   - Body at death location: normal lootable corpse
//   - Tension: low. "You died, here's a body nearby."
//
// DEATH 2:
//   - Revival bodies: Cheap pushed further out, Mid may be in previous district
//   - Previous Cheap location: empty (consumed)
//   - Death 1 body: begins reanimation timer (if not looted)
//   - Chip degradation: Tier 0 (still within grace period)
//   - Tension: moderate. "Bodies are further away now."
//
// DEATH 3:
//   - Revival bodies: Cheap only in previous districts, Mid in current (contested)
//   - Death 1 body: likely reanimated (hostile mini-boss)
//   - Death 2 body: begins reanimation timer
//   - Chip degradation: Tier 1 (-5% stats)
//   - Tension: high. "You're losing ground."
//
// DEATH 4:
//   - Revival bodies: few remaining, all in dangerous/distant locations
//   - Multiple reanimated bodies in previous districts
//   - Chip degradation: Tier 2 (-10% stats + input delay)
//   - Tension: critical. "One more death and options are thin."
//
// DEATH 5+:
//   - Revival bodies: 0-1 remaining, Premium only (if any)
//   - All previous bodies reanimated
//   - Chip degradation: Tier 3 (-15% + glitches + memory loss)
//   - Wipe is imminent without extreme skill or luck
//
// SPATIAL ELEMENT:
//   - Nearby bodies get used first (player takes path of least resistance)
//   - Each used body is removed from the pool
//   - Reanimated bodies block previous routes (hostile mini-bosses guarding paths)
//   - The expedition map fills with skull icons and hostile versions of yourself
```

---

## Full Wipe Conditions

```
// SOLO WIPE:
//   Player dies AND all of the following are true:
//     - DroneInsuranceState.ChargesRemaining == 0
//     - No unclaimed RevivalBodyState entities exist (any tier, any district)
//     - No active ContinuityCache entities exist
//     - No activated Revival Terminals available
//   → WipeEvent created with SoulId
//
// CO-OP WIPE (Party Wipe):
//   ALL party members are dead AND:
//     - No living member can recover any chip
//     - No drone charges remain on any member
//     - No revival bodies available for any member
//   → WipeEvent created with PartyId
//
// CO-OP PARTIAL (NOT a wipe):
//   Some members dead, at least one alive:
//     - Alive members can still carry chips to bodies
//     - Alive members can clear reanimated enemies blocking revival routes
//     - Run continues as long as one member is alive with options
//
// WIPE CONSEQUENCES:
//   1. Expedition ends immediately
//   2. Run-level progress lost:
//      - Current district progress reset
//      - Carried inventory/gear lost (except soul-bound items if any)
//      - Currency on hand lost
//   3. Meta-progression preserved:
//      - Compendium entries from COMPLETED districts kept
//      - Account-level unlocks retained
//      - Expedition statistics recorded (deaths, districts, time)
//   4. Return to hub: player starts fresh expedition planning
```

---

## Systems

### DeathSpiralTrackingSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/DeathSpiralTrackingSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: SoulChipEjectionSystem
//
// Maintains death counters and spiral state:
//   1. On player death (SoulChipState.IsEmbodied transitions false):
//      a. Increment DeathSpiralState.TotalDeaths
//      b. Increment DeathSpiralState.DistrictDeaths
//      c. Append DeathLocationEntry to buffer (position, district, zone, time)
//      d. Update DistrictDeathCounter for current district
//   2. On district change:
//      a. Reset DistrictDeaths to 0
//      b. Update CurrentDistrictId
//   3. Each frame (when player is dead):
//      a. Count remaining RevivalBodyState entities → RemainingRevivalBodies
//      b. Read DroneInsuranceState.ChargesRemaining → RemainingDroneCharges
//      c. Count active ContinuityCache entities → RemainingCaches
//      d. Mirror SoulChipState.DegradationTier → ChipDegradationTier
```

### WipeDetectionSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/WipeDetectionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DeathSpiralTrackingSystem
//
// Checks for full wipe conditions:
//
// Solo check:
//   1. Player is dead (SoulChipState.IsEmbodied == false)
//   2. DeathSpiralState.RemainingRevivalBodies == 0
//   3. DeathSpiralState.RemainingDroneCharges == 0
//   4. DeathSpiralState.RemainingCaches == 0
//   5. No activated Revival Terminals with available bodies
//   6. If all true: set IsWiped = true, create WipeEvent entity
//
// Co-op check:
//   1. Query all party members via Party/ framework
//   2. If ALL members have SoulChipState.IsEmbodied == false:
//      a. Check combined revival resources across all members
//      b. If no member has any recovery option: create WipeEvent with PartyId
//   3. If at least one member alive: no wipe (partial death, run continues)
//
// Debounce: only check wipe conditions 1 second after last death
//   (allows drone insurance or emergency systems to activate first)
```

### WipeExecutionSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/WipeExecutionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: WipeDetectionSystem
//
// Processes WipeEvent entities:
//   1. For each WipeEvent:
//      a. Record expedition statistics to meta-persistence:
//         - Total deaths, districts visited, time elapsed
//         - Enemies killed, gear found, currency earned
//      b. Preserve Compendium entries from completed districts
//      c. Clear run-level state:
//         - Destroy all RevivalBodyState entities
//         - Destroy all DeadBody entities
//         - Destroy all ReanimatedEnemy entities
//         - Clear player inventory (except soul-bound)
//      d. Trigger WipeUISequence (EPIC 2.6):
//         - Show wipe screen with expedition summary
//         - Offer "Return to Hub" action
//      e. Destroy WipeEvent entity
//   2. On "Return to Hub" confirmation:
//      - Transition to hub scene
//      - Reset DeathSpiralState on all party members
//      - Player begins fresh expedition planning
```

### SpiralEscalationSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/SpiralEscalationSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DeathSpiralTrackingSystem
//
// Applies escalating consequences based on death count:
//   1. After each death, evaluate spiral effects:
//      a. Reanimation acceleration:
//         - Bodies from deaths > 1 ago: multiply reanimation speed by 1.5
//         - Bodies from deaths > 2 ago: multiply by 2.0 (near-instant claim)
//      b. Revival body quality shift:
//         - If TotalDeaths >= 3: remove all Cheap bodies in current district
//         - If TotalDeaths >= 4: reduce Mid body count by 1
//         - If DistrictDeaths >= 2: shift all remaining bodies one tier down
//      c. Enemy awareness:
//         - Reanimated enemies from recent deaths are marked on enemy radar
//         - Nearby enemies gain aggro boost (drawn to death sites)
//      d. Environmental pressure:
//         - If FrontAccelerated: death sites become Front waypoints (EPIC 3)
//         - District hazards increase near death cluster zones
//   2. These effects stack — repeated death in one district is punishing
```

---

## Near-Death Tension

```
// UI elements that escalate with spiral state (fed to DeathUIBridge, EPIC 2.6):
//
// REMAINING OPTIONS COUNTER:
//   Persistent HUD element when TotalDeaths >= 1:
//   "[drone icon] x2  [body icon] x3  [cache icon] x1"
//   Numbers decrease in real-time as options are consumed.
//   Turns red when total remaining < 3.
//
// CHIP DEGRADATION METER:
//   Visible after first transfer:
//   Small chip icon with fill bar (green → yellow → orange → red)
//   Pulses at Tier 2+. Glitches at Tier 3.
//
// BODY STATUS NOTIFICATIONS:
//   "[DistrictName] is claiming a body..." — when reanimation begins
//   "[DistrictName] has reanimated your body!" — when complete
//   "A revival body has been destroyed" — if body lost to Front/environmental
//
// SCAR MAP INTEGRATION:
//   Skull icons at death locations (color-coded):
//     White skull: lootable body
//     Red skull: reanimated (hostile)
//     Grey skull: looted and empty
//     Gold skull: revival body location
//   Hover: shows body contents, reanimation status, danger rating
//
// AUDIO ESCALATION:
//   Death 1: normal ambient
//   Death 3+: tension music layer fades in
//   Death 5+: heartbeat audio, distorted ambient, chip glitch sounds
```

---

## Setup Guide

1. Add `DeathSpiralState` to player entity baker (TotalDeaths=0, all counters zeroed)
2. Add `DeathLocationEntry` buffer and `DistrictDeathCounter` buffer to player entity
3. Configure spiral escalation thresholds in gameplay settings SO
4. Hook `WipeDetectionSystem` to Party/ framework for co-op wipe detection
5. Configure wipe debounce timer (default: 1 second post-death)
6. Create expedition statistics recorder for meta-persistence on wipe
7. Configure Compendium preservation rules: only entries from fully completed districts survive
8. Add HUD elements: remaining options counter, chip degradation meter
9. Create notification system for body reanimation status changes
10. Configure audio layers: tension music triggers at death thresholds

---

## Verification

- [ ] DeathSpiralState.TotalDeaths increments on each death
- [ ] DistrictDeaths resets on district transition
- [ ] DeathLocationEntry buffer records each death position
- [ ] DistrictDeathCounter tracks per-district death and body stats
- [ ] Death 1: Cheap revival body spawns nearby
- [ ] Death 2: previous Cheap location empty, bodies further
- [ ] Death 3+: Cheap bodies only in previous districts
- [ ] Reanimation speed accelerates for older bodies (1.5x, 2.0x)
- [ ] Revival body quality degrades with repeated deaths
- [ ] RemainingRevivalBodies count accurate across all districts
- [ ] Solo wipe: death with zero options triggers WipeEvent
- [ ] Co-op wipe: all members dead with zero options triggers WipeEvent
- [ ] Co-op partial: one alive member prevents wipe
- [ ] Wipe debounce: 1-second delay before checking conditions
- [ ] Wipe preserves Compendium entries from completed districts
- [ ] Wipe clears run-level progress (inventory, currency, district state)
- [ ] Expedition statistics recorded on wipe
- [ ] HUD: remaining options counter visible after first death
- [ ] HUD: chip degradation meter visible after first transfer
- [ ] Notifications fire for reanimation start/complete events
- [ ] Scar Map shows correct skull icon colors per body status
- [ ] Audio tension layers escalate with death count

---

## Validation

Validation rules for `DeathSpiralState`:
- `TotalDeaths` must be >= 0 and <= a sane cap (e.g., 20; more indicates a bug or extreme edge case)
- `DistrictDeaths` must be <= `TotalDeaths`
- `RemainingRevivalBodies` must match actual count of unclaimed `RevivalBodyState` entities (cross-validated each frame in debug builds)
- `RemainingDroneCharges` must match `DroneInsuranceState.ChargesRemaining`
- `ChipDegradationTier` must match `SoulChipState.DegradationTier`
- `IsWiped` must only be true when all remaining counters are 0

Validation rules for `DeathLocationEntry` buffer:
- Buffer length must equal `TotalDeaths`
- Each entry's `DistrictId` must be a valid district
- Entries must be in chronological order by `DeathTime`

---

## Debug Visualization

**Death Spiral Overlay** (toggle via debug menu):
- HUD panel showing full spiral state:
  - `TotalDeaths` / `DistrictDeaths` counters
  - Remaining resources bar: drone charges, revival bodies, caches (color-coded by urgency)
  - Chip degradation tier with visual meter
- Timeline graph: X-axis = expedition time, Y-axis = death events, with phase markers
- Scar Map debug layer: all death locations with numbered markers, reanimation status icons, revival body positions

**Death Spiral Phase Indicator**:
- Subtle HUD border tint that shifts as spiral deepens: none (0 deaths) -> faint blue (1) -> amber (2-3) -> red pulse (4+)

**Activation**: Debug menu toggle `Death/Spiral/ShowState`

---

## Simulation & Testing

**Death Spiral Curve Analysis**:
- Automated test: simulate expedition with forced deaths at fixed intervals (every 120s). Record spiral state after each death. Verify:
  - Death 1: RemainingRevivalBodies >= 2
  - Death 3: RemainingRevivalBodies <= 2, ChipDegradationTier == 1
  - Death 5: RemainingRevivalBodies <= 1, ChipDegradationTier == 3

**Revival Body Economy Simulation**:
- Monte Carlo (N=1000): for each seed, simulate random play patterns with 0-7 deaths per expedition
  - Track: time-to-wipe distribution, mean deaths before wipe, revival body utilization rate
  - Balance targets:
    - Mean deaths before wipe: 4-6 (enough to feel the spiral without being trivial)
    - Wipe rate at death 3: < 5% (spiral shouldn't be lethal this early)
    - Wipe rate at death 6: > 60% (spiral should be near-terminal by this point)

**Co-op Wipe Edge Cases**:
- Test: 2-player party, player A dies with 0 options, player B alive -> no wipe
- Test: 2-player party, both die simultaneously with 0 options -> wipe
- Test: 2-player party, A dies, B picks up chip, B dies while carrying -> chip drops, no wipe until both have 0 options
