# EPIC 22.1: Assembly Definitions & Modularization

**Status**: 🔲 NOT STARTED  
**Priority**: CRITICAL  
**Estimated Effort**: 1 week  
**Dependencies**: None

---

## Goal

Create modular assembly definitions that allow the character controller to be distributed as a UPM package with optional feature modules.

---

## Current Problem

```
/Assets/Scripts/Player/   # No .asmdef file!
```

The Player folder has no assembly definition, meaning:
- Cannot be distributed as a UPM package
- Entire project recompiles on any change
- No namespace isolation from game code

---

## Target Structure

```
Runtime/
├── Core/
│   ├── Player.Core.asmdef
│   ├── Components/       # PlayerInput, PlayerState, Health, etc.
│   ├── Systems/          # Movement, GroundCheck, Camera, Stamina
│   └── Jobs/             # CharacterController jobs
│
├── Extended/
│   ├── Player.Extended.asmdef
│   ├── Components/       # ClimbingState, MantleState, SlideState, ProneState
│   └── Systems/          # Climb*, Mantle*, Slide*, Prone*
│
├── Combat/
│   ├── Player.Combat.asmdef
│   ├── Components/       # DamageEvent, TackleState, DeathState
│   └── Systems/          # Damage*, Tackle*, Ragdoll*, StatusEffect*
│
├── Networking/
│   ├── Player.Networking.asmdef (requires Unity.NetCode)
│   ├── Components/       # Network-specific components
│   └── Systems/          # Prediction, reconciliation systems
│
└── Audio/
    ├── Player.Audio.asmdef
    ├── Components/       # FootstepTimer, audio events
    └── Systems/          # Footstep*, Audio*
```

---

## Tasks

### Phase 1: Core Assembly
- [ ] Create `Player.Core.asmdef`
- [ ] Move essential components: PlayerInput, PlayerState, Health, CharacterControllerSettings
- [ ] Move essential systems: PlayerMovementSystem, CharacterControllerSystem, PlayerGroundCheckSystem
- [ ] Move jobs folder
- [ ] Define references: Unity.Entities, Unity.Physics, Unity.Burst, Unity.Mathematics

### Phase 2: Extended Actions Assembly
- [ ] Create `Player.Extended.asmdef`
- [ ] Move climbing components and systems (7+ files)
- [ ] Move mantling components and systems
- [ ] Move sliding components and systems
- [ ] Move prone components and systems
- [ ] Add reference to Player.Core

### Phase 3: Combat Assembly
- [ ] Create `Player.Combat.asmdef`
- [ ] Move damage components and systems
- [ ] Move tackle components and systems
- [ ] Move ragdoll components and systems
- [ ] Move status effect components and systems
- [ ] Add reference to Player.Core

### Phase 4: Networking Assembly
- [ ] Create `Player.Networking.asmdef`
- [ ] Add conditional reference to Unity.NetCode
- [ ] Move prediction/reconciliation systems
- [ ] Use `#if NETCODE_PRESENT` defines
- [ ] Add reference to Player.Core

### Phase 5: Audio Assembly
- [ ] Create `Player.Audio.asmdef`
- [ ] Move footstep system and components
- [ ] Move surface material integration
- [ ] Add reference to Player.Core

### Phase 6: Verification
- [ ] Verify each assembly compiles independently
- [ ] Core works without Extended/Combat/Networking
- [ ] Test all combinations of assemblies
- [ ] Update all namespace references

---

## Assembly Reference Matrix

| Assembly | References |
|----------|------------|
| Player.Core | Unity.Entities, Unity.Physics, Unity.Burst |
| Player.Extended | Player.Core |
| Player.Combat | Player.Core |
| Player.Networking | Player.Core, Unity.NetCode |
| Player.Audio | Player.Core |
| Player.Editor | All runtime assemblies |

---

## Success Criteria

- [ ] Each assembly compiles in isolation
- [ ] Core works without any optional assemblies
- [ ] No circular dependencies
- [ ] Clean namespace organization
- [ ] Compile time reduced by 50%+
