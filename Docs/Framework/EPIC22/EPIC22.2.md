# EPIC 22.2: Game-Specific Code Extraction

**Status**: 🔲 NOT STARTED  
**Priority**: CRITICAL  
**Estimated Effort**: 1 week  
**Dependencies**: 22.1 (Assembly Definitions)

---

## Goal

Remove all DIG-specific game code from the character controller to make it a standalone, reusable package.

---

## Current Problem

Found `DIG.Survival` and game-specific references in 10+ files:

| File | Game Dependency |
|------|-----------------|
| `ShipLocalSpaceZoneSystem.cs` | Ship/vehicle systems |
| `KillZoneSystem.cs` | Game hazards |
| `DarknessStressSystem.cs` | Game stress mechanic |
| `SurvivalDamageAdapterSystem.cs` | Game damage integration |
| `PushInteractionSystem.cs` | Game interactions |
| `PushMovementSystem.cs` | Game interactions |
| `RagdollTransitionSystem.cs` | DIG.Survival references |
| `RagdollRecoverySystem.cs` | DIG.Survival references |
| `DamageDebugSystem.cs` | DIG.Survival references |
| `DamageBridgeSystems.cs` | Game-specific bridges |

---

## Tasks

### Phase 1: Audit All Dependencies
- [ ] Run `grep -r "DIG.Survival" --include="*.cs"` on Player folder
- [ ] Run `grep -r "DIG.Ship" --include="*.cs"` on Player folder
- [ ] Document every external reference
- [ ] Classify as "movable" or "needs interface"

### Phase 2: Create Interface Abstractions
For systems that interact with game systems, create interfaces:

```csharp
// Instead of direct DIG.Survival reference
public interface IDamageHandler
{
    void ApplyDamage(Entity target, float amount, DamageType type);
}

public interface IEnvironmentZone
{
    bool IsInZone(float3 position);
    EnvironmentType GetZoneType();
}
```

### Phase 3: Move Game-Specific Files
Move to DIG game project (outside Player package):

- [ ] `ShipLocalSpaceZoneSystem.cs` → `DIG.Ships/Systems/`
- [ ] `KillZoneSystem.cs` → `DIG.Hazards/Systems/`
- [ ] `DarknessStressSystem.cs` → `DIG.Survival/Systems/`
- [ ] `SurvivalDamageAdapterSystem.cs` → `DIG.Survival/Bridges/`
- [ ] Game-specific bridges → `DIG.Core/Bridges/`

### Phase 4: Update Ragdoll References
The ragdoll system has DIG.Survival references:
- [ ] Abstract survival damage events
- [ ] Create generic death/ragdoll triggers
- [ ] Move game-specific ragdoll logic out

### Phase 5: Create Adapter Layer in DIG
- [ ] Create `DIG.PlayerIntegration` assembly in game project
- [ ] Implement interface adapters
- [ ] Wire up game systems to player interfaces

### Phase 6: Verification
- [ ] Player package compiles with zero DIG.* references
- [ ] Game project compiles with adapters
- [ ] All features still work in-game
- [ ] Document integration pattern for users

---

## Interface Design

```csharp
// In Player.Core - interfaces for game integration
namespace Player.Interfaces
{
    public interface IPlayerDamageReceiver
    {
        void OnDamageReceived(float amount, Entity source);
    }
    
    public interface IPlayerEnvironmentAdapter
    {
        bool IsInSafeZone(float3 position);
        float GetEnvironmentModifier(float3 position);
    }
    
    public interface IPlayerShipAdapter
    {
        bool IsInLocalSpace(Entity player);
        float4x4 GetLocalToWorld();
    }
}
```

---

## Files to Move (Complete List)

| Current Path | New Path (DIG Project) |
|--------------|------------------------|
| `Systems/ShipLocalSpaceZoneSystem.cs` | `DIG.Ships/PlayerIntegration/` |
| `Systems/Hazards/KillZoneSystem.cs` | `DIG.Core/Hazards/` |
| `Systems/DarknessStressSystem.cs` | `DIG.Survival/Stress/` |
| `Bridges/Survival/*` | `DIG.Survival/PlayerBridges/` |
| `Components/StressComponents.cs` | `DIG.Survival/Components/` |

---

## Success Criteria

- [ ] Zero references to DIG.* namespaces in player code
- [ ] Player package compiles standalone
- [ ] All game features work via adapters
- [ ] Integration pattern documented
- [ ] Clean separation of concerns
