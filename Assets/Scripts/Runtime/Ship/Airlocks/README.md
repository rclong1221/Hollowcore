# Epic 3.1: Airlocks, Pressurization, and EVA Transitions

This module implements seamless transitions between ship interior (pressurized) and EVA (vacuum), with clear interaction prompts and authoritative NetCode behavior.

## Architecture Overview

### Components (`Components/AirlockComponents.cs`)

| Component | Purpose |
|-----------|---------|
| `Airlock` | Main state machine for airlock cycling. Tracks spawn points, cycle progress, and current user. |
| `AirlockDoor` | Individual door state (open/closed/locked) with reference to parent airlock. |
| `AirlockInteractable` | Interaction configuration (range, prompt text). |
| `AirlockUseRequest` | Buffer element for client requests to use airlock. |
| `AirlockTransitionPending` | Tracks player currently in an airlock transition. |
| `AirlockPromptState` | Client-side prompt UI state. |
| `AirlockLocked` | Tag component for locking airlocks (damage, power failure). |
| `AirlockInteractDebounce` | Client-side input debounce state. |
| `AirlockDoorAnimation` | Animation state for smooth door transitions. |

### Systems

| System | World | Group | Purpose |
|--------|-------|-------|---------|
| `AirlockPromptSystem` | Client | PresentationSystemGroup | Shows interaction prompts for nearby airlocks. |
| `AirlockUseRequestSystem` | Client | PredictedSimulationSystemGroup | Creates requests when player interacts. |
| `AirlockCycleSystem` | Server | SimulationSystemGroup | Validates requests, runs state machine, teleports players. |
| `AirlockDoorAnimationSystem` | Client | PresentationSystemGroup | Animates doors based on replicated state. |

### Authoring

| Component | Purpose |
|-----------|---------|
| `AirlockAuthoring` | Main airlock setup with spawn points and doors. |
| `AirlockPlayerAuthoring` | Add to player prefab to enable airlock interactions. |
| `AirlockDoorAuthoring` | Custom door setup with animation configuration. |

## Server Authority Model

1. **Requests go through buffer**: Client appends `AirlockUseRequest` to player entity buffer
2. **Server validates**: Range check, airlock availability, player state, anti-spam
3. **Server executes**: Updates airlock state, progresses cycle, teleports player
4. **Client reconciles**: Predicts UI/animation, reconciles if server rejects

## State Machine

```
         ┌─────────────────────────────────────────────┐
         │                                             │
         ▼                                             │
    ┌─────────┐   Request + Validate   ┌──────────────┴───────────┐
    │  Idle   │ ─────────────────────► │ CyclingToInterior        │
    │         │                        │ or CyclingToExterior     │
    └────┬────┘                        └──────────────┬───────────┘
         │                                            │
         │      Abort (death/disconnect/despawn)      │ Progress >= CycleTime
         ◄────────────────────────────────────────────┤
         │                                            │
         │                                            ▼
         │                              ┌─────────────────────────┐
         │                              │ Complete:               │
         │                              │ - Teleport player       │
         │                              │ - Update PlayerMode     │
         │                              │ - Open destination door │
         └──────────────────────────────┴─────────────────────────┘
```

## Door Safety Rules

- **Both doors lock** when cycle starts
- **Only destination door opens** when cycle completes
- Interior + exterior doors **cannot both be open** during cycling

## Usage

### Setting Up an Airlock

1. Create an empty GameObject with a collider (trigger)
2. Add `AirlockAuthoring` component
3. Set up interior and exterior spawn point transforms (child objects)
4. Optionally assign door GameObjects with `AirlockDoorAuthoring`

### Player Setup

1. Add `AirlockPlayerAuthoring` to the player prefab
2. Configure debounce tick count (default: 10)

### Locking an Airlock

Add `AirlockLocked` component to the airlock entity:
```csharp
entityManager.AddComponentData(airlockEntity, new AirlockLocked 
{ 
    LockReason = "No Power" 
});
```

## Edge Cases Handled

- ✅ Player dies mid-cycle → abort, clear CurrentUser
- ✅ Player disconnects mid-cycle → clear CurrentUser
- ✅ Airlock entity despawns mid-cycle → remove pending transition from player
- ✅ Multiple players request same airlock → first valid request wins
- ✅ Request spam → debounce + rate limiting

## Dependencies

- **Epic 1.4**: `PlayerState` / `PlayerMode` for mode transitions
- **Epic 2.1**: Oxygen system (uses `CurrentEnvironmentZone.OxygenRequired`)
- **Survival**: `CurrentEnvironmentZone` for environment detection

## Files

```
Assets/Scripts/Runtime/Ship/Airlocks/
├── Components/
│   └── AirlockComponents.cs
├── Systems/
│   ├── AirlockPromptSystem.cs
│   ├── AirlockUseRequestSystem.cs
│   ├── AirlockCycleSystem.cs
│   └── AirlockDoorAnimationSystem.cs
└── Authoring/
    └── AirlockAuthoring.cs
```

## Testing Checklist

- [ ] Enter/exit spam: verify only one request accepted
- [ ] Two players attempt same airlock: verify deterministic winner
- [ ] Simulated latency 50/100/200ms: verify no rubber-banding
- [ ] Oxygen drain flips only when zone says `OxygenRequired == true`
- [ ] Player ends in correct `PlayerMode` and environment zone
