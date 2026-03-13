# EPIC 2.1: Soul Chip Core

**Status**: Planning
**Epic**: EPIC 2 — Soul Chip, Death & Revival
**Dependencies**: Framework: Combat/DeathState, Persistence/

---

## Overview

The Soul Chip is the player's consciousness — the one thing that persists through body destruction. It tracks transfer count, degradation level, and identity. On death, the chip is ejected from the body and must be recovered (solo: auto-eject or manual; co-op: teammate recovery). Degradation accumulates after 3+ transfers, applying escalating penalties.

---

## Component Definitions

```csharp
// File: Assets/Scripts/SoulChip/Components/SoulChipComponents.cs
using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.SoulChip
{
    /// <summary>
    /// Core soul chip state. On the player entity (this IS the player's identity).
    /// Persists across bodies — when player dies and revives, this component transfers.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SoulChipState : IComponentData
    {
        /// <summary>Unique ID for this soul (persistent across bodies).</summary>
        [GhostField] public int SoulId;

        /// <summary>How many times this chip has been transferred to a new body.</summary>
        [GhostField] public int TransferCount;

        /// <summary>Current degradation tier (0 = none, computed from TransferCount).</summary>
        [GhostField] public byte DegradationTier;

        /// <summary>Whether the chip is currently in a body (false = ejected/in transit).</summary>
        [GhostField] public bool IsEmbodied;
    }

    /// <summary>
    /// Degradation penalties applied based on transfer count.
    /// Recalculated on each transfer. Fed into EquippedStatsSystem.
    /// </summary>
    public struct SoulChipDegradation : IComponentData
    {
        /// <summary>Multiplicative stat penalty (1.0 = none, 0.85 = 15% reduction).</summary>
        public float StatMultiplier;
        /// <summary>Whether memory glitches are active (visual/audio distortion).</summary>
        public bool MemoryGlitches;
        /// <summary>Whether input delay is active.</summary>
        public bool InputDelay;
        /// <summary>Number of Compendium pages lost on this transfer.</summary>
        public int CompendiumPagesLost;
    }

    /// <summary>
    /// Ejected soul chip in the world. Interactable entity at death location.
    /// Only exists when player is dead and chip hasn't been recovered.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct EjectedSoulChip : IComponentData
    {
        public int SoulId;
        public int TransferCount;
        /// <summary>Entity of the dead body this chip came from.</summary>
        public Entity SourceBody;
    }
}
```

---

## Degradation Tiers

```
// File: Assets/Scripts/SoulChip/Systems/SoulChipDegradationTable.cs
// Transfer 1-2: Tier 0 — No penalty
// Transfer 3:   Tier 1 — StatMultiplier = 0.95 (5% reduction)
// Transfer 4:   Tier 2 — StatMultiplier = 0.90 (10% reduction), InputDelay = true
// Transfer 5+:  Tier 3 — StatMultiplier = 0.85 (15% reduction), InputDelay = true,
//                         MemoryGlitches = true, CompendiumPagesLost = TransferCount - 4
```

---

## Systems

### SoulChipEjectionSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/SoulChipEjectionSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: DeathTransitionSystem (framework)
//
// On player death:
//   1. Detect DeathState transition to Dead on player entity
//   2. Set SoulChipState.IsEmbodied = false
//   3. Create EjectedSoulChip entity at player death position
//      - Copy SoulId, TransferCount from SoulChipState
//      - Set SourceBody to the dead body entity (EPIC 2.2)
//   4. Solo mode: optionally auto-eject to nearest safe point (drone insurance)
//      - If player has drone insurance charge: teleport chip entity to safe zone
//      - Else: chip stays at body
//   5. Co-op mode: chip stays at body — teammate must physically pick it up
```

### SoulChipRecoverySystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/SoulChipRecoverySystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Handles chip recovery (pickup by self via drone or teammate in co-op).
//
// For each EjectedSoulChip:
//   If interacted with by teammate (co-op):
//     1. Add ChipCarrier component to teammate (they're carrying the chip)
//     2. Destroy EjectedSoulChip world entity
//     3. Chip must be brought to a revival body (EPIC 2.3)
//
//   If solo drone recovery activated:
//     1. Animate chip moving to revival body location
//     2. Begin revival process (EPIC 2.3)
```

### SoulChipTransferSystem

```csharp
// File: Assets/Scripts/SoulChip/Systems/SoulChipTransferSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
//
// Handles the actual consciousness transfer to a new body.
//
// On revival (chip + body paired):
//   1. Increment SoulChipState.TransferCount
//   2. Recalculate DegradationTier from TransferCount
//   3. Compute SoulChipDegradation penalties
//   4. Set SoulChipState.IsEmbodied = true
//   5. Apply degradation penalties as EquippedStatsSystem modifiers
//   6. If CompendiumPagesLost > 0: remove random pages from inventory (EPIC 9)
//   7. If MemoryGlitches: enable visual/audio distortion post-process
//   8. Fire SoulChipTransferEvent for UI notification
```

---

## Setup Guide

1. Add `SoulChipState` + `SoulChipDegradation` to player entity baker (SoulId generated from player session)
2. Create EjectedSoulChip prefab: small glowing object, interactable
3. Configure drone insurance as consumable item (limited charges per expedition)
4. Hook SoulChipEjectionSystem to fire after framework's DeathTransitionSystem
5. Add degradation visual effects: screen distortion shader, audio warping
6. UI: soul chip status indicator showing TransferCount and current degradation tier

---

## Verification

- [ ] Player death ejects soul chip at death location
- [ ] EjectedSoulChip entity visible and interactable in world
- [ ] Co-op: teammate can pick up ejected chip
- [ ] Solo: drone insurance auto-recovers chip (if charges available)
- [ ] Transfer count increments on each revival
- [ ] Degradation tier 0 (transfers 1-2): no penalties
- [ ] Degradation tier 1 (transfer 3): 5% stat reduction
- [ ] Degradation tier 2 (transfer 4): 10% + input delay
- [ ] Degradation tier 3 (transfer 5+): 15% + glitches + page loss
- [ ] Degradation penalties appear in EquippedStatsSystem

---

## Validation

```csharp
// File: Assets/Scripts/SoulChip/Components/SoulChipComponents.cs
// Add to SoulChipState or a dedicated validator
namespace Hollowcore.SoulChip
{
    // Build-time validation (Editor only):
    // - SoulId must be > 0 at runtime (0 = uninitialized sentinel)
    // - TransferCount must be >= 0
    // - DegradationTier must be <= 3 (max tier)
    // - StatMultiplier must be in [0.0, 1.0] range
    // - CompendiumPagesLost must be >= 0
}
```

Validation rules:
- `SoulChipState.SoulId == 0` after initialization indicates a bug in session setup
- `DegradationTier` must match the transfer-count-to-tier lookup table (1-2 -> 0, 3 -> 1, 4 -> 2, 5+ -> 3)
- `SoulChipDegradation.StatMultiplier` must equal `1.0 - (tier * 0.05)` within float epsilon
- `CompendiumPagesLost` must equal `max(0, TransferCount - 4)` at tier 3

---

## Debug Visualization

**Soul Chip State Overlay** (toggle via debug menu):
- HUD overlay showing: `SoulId`, `TransferCount`, `DegradationTier`, `IsEmbodied`
- Degradation tier color band: green (T0) / yellow (T1) / orange (T2) / red (T3)
- In-world gizmo: glowing sphere at EjectedSoulChip entity position when chip is ejected
- Transfer history log: timestamps of each transfer with body location

**Activation**: Debug menu toggle `Front/SoulChip/ShowChipState`

---

## Simulation & Testing

**Degradation Curve Verification**:
- Automated test: for TransferCount 0..10, verify DegradationTier and StatMultiplier match the lookup table exactly
- Seed-deterministic test: given expedition seed S, simulate 5 sequential deaths, verify degradation output matches expected tier sequence [0, 0, 1, 2, 3]

**Economy Impact Simulation**:
- Monte Carlo (N=1000): simulate random expedition runs with 0-7 deaths each. Record distribution of final DegradationTier. Verify tier 3 occurs in < 15% of runs (balance target: most players should not hit tier 3)
