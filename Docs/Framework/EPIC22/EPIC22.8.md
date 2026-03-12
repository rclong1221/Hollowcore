# EPIC 22.8: Input System Abstraction

**Status**: 🔲 NOT STARTED  
**Priority**: MEDIUM  
**Estimated Effort**: 2-3 days  
**Dependencies**: 22.1 (Assembly Definitions)

---

## Goal

Support both Unity Input Systems (Old and New) through an abstraction layer.

---

## Current Problem

The player systems currently use the new Input System directly. Some customers may want to use the old Input Manager or a custom input solution.

---

## Target Design

```csharp
public interface IPlayerInputProvider
{
    float2 MovementInput { get; }
    float2 LookInput { get; }
    bool JumpPressed { get; }
    bool SprintHeld { get; }
    bool CrouchPressed { get; }
    // ... all inputs
}

// Implementations
public class NewInputSystemProvider : IPlayerInputProvider { }
public class LegacyInputProvider : IPlayerInputProvider { }
public class CustomInputProvider : IPlayerInputProvider { }
```

---

## Tasks

### Phase 1: Interface Design
- [ ] Create `IPlayerInputProvider` interface
- [ ] Define all required inputs
- [ ] Create input state struct for ECS

### Phase 2: New Input System Implementation
- [ ] Create `NewInputSystemProvider`
- [ ] Map all actions to interface
- [ ] Handle action callbacks

### Phase 3: Legacy Input Implementation
- [ ] Create `LegacyInputProvider`
- [ ] Use `Input.GetAxis` and `Input.GetButton`
- [ ] Support axis remapping

### Phase 4: System Integration
- [ ] Update `PlayerInputSystem` to use interface
- [ ] Add provider selection in authoring
- [ ] Support runtime switching

### Phase 5: Documentation
- [ ] Document input configuration
- [ ] Provide examples for custom providers
- [ ] Migration guide from direct input

---

## Input Mapping

| Action | New Input System | Legacy Input |
|--------|------------------|--------------|
| Move | `Move` Vector2 | `Horizontal/Vertical` |
| Look | `Look` Vector2 | `Mouse X/Y` |
| Jump | `Jump` Button | `Jump` |
| Sprint | `Sprint` Value | `left shift` |
| Crouch | `Crouch` Button | `c` |
| Climb | `Interact` Button | `e` |
| Dodge | `Dodge` Button | `left alt` |

---

## Success Criteria

- [ ] Works with new Input System (default)
- [ ] Works with legacy Input Manager
- [ ] Custom providers supported
- [ ] No Input System compile errors when disabled
- [ ] Documentation for all options
