# EPIC 6.2: Backtrack Gate Presentation

**Status**: Planning
**Epic**: EPIC 6 — Gate Selection & Navigation
**Dependencies**: EPIC 4.1 (ExpeditionGraphState); EPIC 4.2 (DistrictSaveState); EPIC 3 (Front phase data); EPIC 5 (Echo definitions); 6.1 (GateSelectionState)

---

## Overview

Below the forward gate options, the gate screen shows every previously visited district as a backtrack gate. Each backtrack gate presents the district's current state: how far the Front has advanced since the player left, what echoes and rewards remain, any seeded events triggered in the interim, District Bleed status, and a danger delta. A Scar Map mini-view gives spatial context. The core tension: "Is what's back there worth fighting through Phase 4?"

---

## Component Definitions

```csharp
// File: Assets/Scripts/Gate/Components/BacktrackGateComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace Hollowcore.Gate
{
    /// <summary>
    /// Danger delta relative to the player's last visit.
    /// </summary>
    public enum DangerDelta : byte
    {
        Safer = 0,         // Rare — only if Front somehow retreated (Echo effect)
        Unchanged = 1,     // Same phase, same density
        SlightlyWorse = 2, // Same phase, higher density
        Dangerous = 3,     // Advanced 1 phase since last visit
        Critical = 4       // Advanced 2+ phases — near Phase 4
    }

    /// <summary>
    /// Flags for seeded events present in a previously visited district.
    /// Multiple can be active simultaneously.
    /// </summary>
    [System.Flags]
    public enum BacktrackEventFlags : ushort
    {
        None            = 0,
        BodyHere        = 1 << 0,   // "Your body is here" — death loot recovery
        RareMerchant    = 1 << 1,   // Rare merchant spawned since departure
        VaultUnlocked   = 1 << 2,   // Vault became accessible (Front progression)
        EchoMutated     = 1 << 3,   // An echo changed form or reward
        FactionShift    = 1 << 4,   // Dominant faction replaced by another
        BleedSource     = 1 << 5,   // This district is bleeding into neighbors
        BleedTarget     = 1 << 6    // This district is receiving bleed from neighbor
    }

    /// <summary>
    /// A backtrack gate option for a previously visited district.
    /// Transient — created during gate screen, destroyed on selection.
    /// </summary>
    public struct BacktrackGateInfo : IComponentData
    {
        public int DistrictId;
        public FixedString64Bytes DistrictName;
        public byte CurrentFrontPhase;           // 1-4
        public byte FrontPhaseWhenLeft;          // 1-4 (snapshot from last visit)
        public DangerDelta Danger;
        public int ActiveEchoCount;              // Number of echoes still alive
        public int PendingRewardValue;           // Aggregate reward score of remaining echoes
        public BacktrackEventFlags SeededEvents;
        public bool IsBleedingOut;               // District Bleed active outward
        public bool IsBleedTarget;               // Receiving bleed from adjacent
        public int BleedSourceDistrictId;        // -1 if not a bleed target
        public float DangerScoreNormalized;      // 0.0-1.0 for UI bar/color
    }

    /// <summary>
    /// Per-echo summary attached as buffer element to BacktrackGateInfo entity.
    /// Gives the player a preview of what echoes guard and their reward type.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct BacktrackEchoPreview : IBufferElementData
    {
        public int EchoDefinitionId;
        public FixedString32Bytes EchoName;
        public int GuardedRewardCategory;        // RewardCategory enum value
        public bool IsElite;                     // Elite echo = higher danger + reward
    }

    /// <summary>
    /// Singleton: Scar Map snapshot for the gate screen mini-view.
    /// Contains indices of visited districts and their state for spatial rendering.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct ScarMapSnapshot : IComponentData
    {
        public int VisitedDistrictCount;
        public int CurrentDistrictId;            // District just extracted from
    }

    /// <summary>
    /// Buffer on ScarMapSnapshot entity: one entry per visited district for mini-map rendering.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ScarMapEntry : IBufferElementData
    {
        public int DistrictId;
        public byte FrontPhase;                  // 1-4 for color coding
        public float2 MapPosition;               // Normalized 2D position on scar map
        public bool IsBleedActive;
    }
}
```

---

## Systems

### BacktrackGateSystem

```csharp
// File: Assets/Scripts/Gate/Systems/BacktrackGateSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: ForwardGateGenerationSystem
//
// Triggered when GateSelectionState.IsActive transitions to true:
//   1. Read ExpeditionGraphState to enumerate all visited district IDs
//   2. For each visited district:
//      a. Read DistrictSaveState (EPIC 4.2) for persistent district data
//      b. Retrieve FrontPhaseWhenLeft from visit snapshot
//      c. Compute CurrentFrontPhase by simulating Front advance since departure:
//         - Elapsed = CurrentExpeditionTurn - LastVisitTurn
//         - Phase = min(4, SnapshotPhase + FrontAdvanceCurve.Evaluate(Elapsed))
//      d. Compute DangerDelta from phase difference
//      e. Query DistrictSaveState.ActiveEchoes for echo count + reward preview
//      f. Roll BacktrackEventFlags from seed + elapsed turns:
//         - BodyHere: set if player died in this district (from DeathRecord)
//         - RareMerchant: seeded chance increases with elapsed turns
//         - VaultUnlocked: triggers at Phase 3+ if vault was locked on departure
//         - EchoMutated: seeded chance per echo per elapsed turn
//         - FactionShift: triggers if Front advanced 2+ phases
//      g. Check District Bleed: read BleedState from DistrictSaveState
//      h. Compute DangerScoreNormalized: weighted sum of phase, echo count, faction level
//   3. Create BacktrackGateInfo entity per visited district
//   4. Attach BacktrackEchoPreview buffer with up to 4 echo summaries per gate
//   5. Create or update ScarMapSnapshot singleton + ScarMapEntry buffer
```

### BacktrackGateCleanupSystem

```csharp
// File: Assets/Scripts/Gate/Systems/BacktrackGateCleanupSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: GateTransitionSystem (EPIC 6.5)
//
// When GateSelectionState.IsActive transitions to false:
//   1. Destroy all entities with BacktrackGateInfo
//   2. Destroy ScarMapSnapshot singleton entity
```

---

## Scar Map Mini-View

```
┌──────────────────────────┐
│  ╔═══╗       ╔═══╗       │
│  ║ 1 ║───────║ 2 ║       │   Phase Colors:
│  ╚═══╝       ╚═══╝       │   Green  = Phase 1
│    │           │          │   Yellow = Phase 2
│    │       ╔═══╗          │   Orange = Phase 3
│    └───────║ 3 ║←(bleed)  │   Red    = Phase 4
│            ╚═══╝          │
│              │            │   ★ = Current extraction
│          ╔═══╗            │   ⚠ = Body recovery
│          ║★4 ║            │
│          ╚═══╝            │
└──────────────────────────┘
```

## Backtrack Gate Card Layout

```
┌─────────────────────────────────┐
│ [Scar Map Pin Icon]             │
│ THE BURN — Phase 3 (was 1)      │
│─────────────────────────────────│
│ Danger: ████████░░ Dangerous ▲  │
│ Echoes: 2 active (Elite×1)     │
│   → Furnace Core (Augment)      │
│   → Molten Cache (Currency)     │
│─────────────────────────────────│
│ ⚠ Your body is here            │
│ 🏪 Rare merchant appeared       │
│ 🩸 Bleeding into Ashvein        │
└─────────────────────────────────┘
```

---

## Setup Guide

1. Add `BacktrackGateComponents.cs` to `Assets/Scripts/Gate/Components/`
2. Add `BacktrackGateSystem.cs` and `BacktrackGateCleanupSystem.cs` to `Assets/Scripts/Gate/Systems/`
3. Ensure `DistrictSaveState` (EPIC 4.2) exposes: `ActiveEchoes`, `FrontPhaseSnapshot`, `LastVisitTurn`, `BleedState`, `DeathRecord`
4. Create `BacktrackGateCardUI` prefab in `Assets/Prefabs/UI/Gate/` — layout per card diagram above
5. Create `ScarMapMiniView` UI component that reads `ScarMapSnapshot` + `ScarMapEntry` buffer
6. Wire `BacktrackGateSystem` ordering: must run after `ForwardGateGenerationSystem` (both populate the same gate screen)
7. Front advance simulation: reuse `FrontAdvanceCurve` from EPIC 3 — do NOT duplicate the curve; read from `FrontDefinitionSO`
8. Seeded event rolls: use `RunSeedUtility.Hash(districtId, expeditionSeed, turnElapsed)` for determinism

---

## Verification

- [ ] Backtrack gates appear for every previously visited district
- [ ] CurrentFrontPhase >= FrontPhaseWhenLeft (Front never retreats unless Echo effect)
- [ ] DangerDelta correctly reflects phase difference
- [ ] Echo previews match DistrictSaveState.ActiveEchoes (count and types)
- [ ] "Your body is here" flag set only when player died in that district
- [ ] RareMerchant and VaultUnlocked events are seed-deterministic
- [ ] District Bleed status matches BleedState from EPIC 4.2
- [ ] DangerScoreNormalized in [0,1] range; UI bar renders correctly
- [ ] Scar Map mini-view shows all visited districts with correct phase colors
- [ ] Scar Map highlights current extraction point
- [ ] Backtrack gate entities destroyed when gate screen closes
- [ ] No backtrack gates shown on first district (nothing to backtrack to)

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Gate/Debug/BacktrackGateDebugOverlay.cs
// Managed SystemBase, ClientSimulation | LocalSimulation, PresentationSystemGroup
//
// Backtrack Gate State Overlay — toggled via debug console: `gate.backtrack.debug`
//
// Displays per backtrack gate:
//   - DistrictId + DistrictName
//   - FrontPhaseWhenLeft → CurrentFrontPhase (with arrow and phase delta)
//   - DangerDelta enum value + DangerScoreNormalized as progress bar
//   - BacktrackEventFlags bitmask expanded (checkboxes for each flag)
//   - ActiveEchoCount + PendingRewardValue
//   - Bleed status: IsBleedingOut, IsBleedTarget, BleedSourceDistrictId
//   - BacktrackEchoPreview buffer contents (EchoName, GuardedRewardCategory, IsElite)
//
// Scar Map debug overlay:
//   - ScarMapSnapshot.VisitedDistrictCount, CurrentDistrictId
//   - ScarMapEntry buffer: all entries with MapPosition, FrontPhase, IsBleedActive
//   - Wire-frame rendering of map connections
//
// Rendered as IMGUI overlay, bottom-left corner.
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/GateWorkstation/BacktrackSimulation.cs
using UnityEditor;

namespace Hollowcore.Gate.Editor
{
    /// <summary>
    /// Backtrack gate simulation: validates Front advance simulation and danger scoring.
    /// Menu: Hollowcore > Simulation > Backtrack Gates
    /// </summary>
    public static class BacktrackSimulation
    {
        [MenuItem("Hollowcore/Simulation/Backtrack Gate Danger Curves")]
        public static void RunDangerCurveTest()
        {
            // Test: Front advance simulation accuracy
            //
            // For each FrontDefinitionSO advance curve:
            //   1. Simulate departure at Phase 1, turns elapsed 1..20
            //   2. Verify CurrentFrontPhase never exceeds 4
            //   3. Verify DangerDelta classification matches phase difference
            //   4. Plot danger score curve (turns elapsed vs DangerScoreNormalized)
            //
            // Test: Seeded event determinism
            //   - Run 50 seeds × 10 elapsed turns
            //   - Verify same seed + same elapsed = same BacktrackEventFlags
            //   - Verify RareMerchant probability increases with elapsed turns
            //
            // Output: CSV report + Unity Console summary.
        }
    }
}
```
