# EPIC 1.7: Chassis Persistence

**Status**: Planning
**Epic**: EPIC 1 — Chassis & Limb System
**Dependencies**: EPIC 1.1; Framework: Persistence/ (ISaveModule)

---

## Overview

Chassis state must persist across district transitions (within an expedition) and optionally across sessions (save/resume). Follows the framework's ISaveModule pattern with a dedicated TypeId. Serializes equipped limb definitions, durability, memory data, and destroyed slot state.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Chassis/Persistence/ChassisSaveModule.cs
using System.IO;
using Hollowcore.Chassis;

namespace Hollowcore.Chassis.Persistence
{
    /// <summary>
    /// ISaveModule implementation for chassis state.
    /// TypeId: 20 (first Hollowcore game module, after framework's 1-18).
    /// </summary>
    public class ChassisSaveModule : ISaveModule
    {
        public int TypeId => 20;
        public string ModuleName => "Chassis";

        // Serialized data per player:
        // - DestroyedSlotsMask (byte)
        // - Per slot (6 slots):
        //   - HasLimb (bool)
        //   - If HasLimb:
        //     - LimbDefinitionId (int)
        //     - CurrentIntegrity (float)
        //     - DurabilityType (byte)
        //     - ElapsedTime (float, for temporaries)
        //     - ExpirationTime (float, for temporaries)

        public void Serialize(BinaryWriter writer, /* entity context */)
        {
            // Write DestroyedSlotsMask
            // For each of 6 slots:
            //   Write hasLimb bool
            //   If hasLimb: write LimbDefinitionId, CurrentIntegrity,
            //               DurabilityType, ElapsedTime, ExpirationTime
        }

        public void Deserialize(BinaryReader reader, /* entity context */)
        {
            // Read DestroyedSlotsMask → set on ChassisState
            // For each of 6 slots:
            //   Read hasLimb
            //   If hasLimb: create limb entity from LimbDefinitionId,
            //               restore CurrentIntegrity, DurabilityType, timers
            //   Set ChassisState slot
        }
    }
}
```

---

## Persistence Rules

| Event | What Happens to Chassis |
|---|---|
| District transition (forward) | Full chassis preserved — ChassisSaveModule serializes |
| District transition (backtrack) | Full chassis preserved |
| Temporary limb on district exit | **Destroyed** — temporaries don't survive transitions |
| DistrictLife limb on district exit | **Destroyed** — only lasts within the district |
| Permanent limb on district exit | Preserved |
| Player death | Chassis snapshot stored on dead body (EPIC 2.2) |
| Revival in new body | New body's default chassis — NOT the old one |
| Session save/resume | Full chassis serialized if expedition save supported |
| Expedition end (victory) | Chassis data recorded in run history for meta-rivals |
| Full wipe | Chassis lost — new expedition starts fresh |

---

## Setup Guide

1. Register ChassisSaveModule with SaveModuleRegistry (TypeId = 20)
2. On district transition: ChassisSaveModule.Serialize → store in expedition save data
3. On district load: ChassisSaveModule.Deserialize → recreate chassis entities
4. Temporary/DistrictLife limb cleanup: before serialization, destroy any non-Permanent limbs
5. Dead body chassis snapshot: separate serialization path (EPIC 2.2 handles body inventory)

---

## Verification

- [ ] Chassis persists across forward district transition
- [ ] Chassis persists across backtrack district transition
- [ ] Temporary limbs destroyed on district exit
- [ ] DistrictLife limbs destroyed on district exit
- [ ] Permanent limbs survive district transitions
- [ ] Dead body stores chassis snapshot at time of death
- [ ] Revival body has its own default chassis (not the dead body's)
- [ ] ChassisSaveModule TypeId=20 doesn't conflict with framework modules (1-18)

---

## Validation

### Save/Load Integrity Validation

```csharp
// File: Assets/Editor/Chassis/ChassisSaveValidator.cs
// Build-time and runtime validation for chassis persistence:
//
// Build-time:
// 1. Verify ChassisSaveModule.TypeId (20) doesn't collide with any other ISaveModule
//    - Scan all ISaveModule implementations via reflection, flag duplicates
// 2. Verify all LimbDefinitionSO.LimbId values used in subscenes exist in the
//    LimbDefinition asset set (forward-compat: if a limb SO is deleted, saved data
//    referencing that ID would fail to deserialize)
// 3. Warn if any LimbDefinitionSO has LimbId == 0 (reserved for "no limb")
//
// Runtime (on deserialize):
// 1. Unknown LimbDefinitionId → log warning, leave slot empty (graceful degradation)
// 2. Integrity values out of range [0, MaxIntegrity] → clamp
// 3. Temporary limbs with ElapsedTime >= ExpirationTime → skip (already expired)
// 4. DestroyedSlotsMask has bits set for non-existent slots (>5) → mask off
```

### Round-Trip Test

```csharp
// File: Assets/Tests/Chassis/ChassisSaveRoundTripTest.cs
// Test: serialize → deserialize → re-serialize → compare bytes
// Cases:
//   - Full chassis (6 limbs, various rarities)
//   - Partial chassis (3 limbs, 2 destroyed, 1 empty)
//   - Empty chassis (new expedition start)
//   - Chassis with temporary limb near expiration
//   - Chassis with maximum-length DisplayName (FixedString64Bytes boundary)
// Verify: byte-exact round-trip for all cases
```

---

## Simulation & Testing

### District Transition Persistence Test

```
// Test: ChassisDistrictTransitionTest
// Scenario: 5 sequential district transitions with evolving chassis state
//   District 1: Start with 6 common limbs
//   District 2: Rip temporary arm (should NOT survive transition)
//   District 3: Equip rare permanent leg (should survive)
//   District 4: Lose head (death → revival with default chassis)
//   District 5: Verify fresh default chassis, no carryover from pre-death
// Verify at each transition:
//   - Serialize slot count matches expectation
//   - Temporary/DistrictLife limbs stripped before serialization
//   - Permanent limbs preserved with correct integrity
//   - DestroyedSlotsMask cleared on revival
```

### Save File Size Test

```
// Test: ChassisSaveFileSizeTest
// Measure serialized byte count for:
//   - Full chassis: should be < 256 bytes (6 slots × ~40 bytes max)
//   - Empty chassis: should be < 16 bytes (mask + 6 × bool)
// Purpose: validate save module stays within expected budget for expedition save size
```
