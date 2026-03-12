# EPIC 6.1: Swimming & Flooded Environments

**Priority**: LOW (Future)  
**Status**: **IMPLEMENTED**  
**Goal**: Enable traversal through liquid-filled volumes with 3D movement, viscous drag, and breath mechanics.
**Dependencies**: Epic 1.5 (Movement), Epic 3.1 (Enviro Zones), Epic 2.1 (Oxygen)

## Design Notes
1.  **State Transition**:
    *   **Entry**: When `SubmersionDepth > 0.6 * Height`. Disable Gravity (Simulated). Enable Buoyancy/Drag. Set `MovementState = Swimming`.
    *   **Exit**: When `SubmersionDepth < 0.3`.
2.  **Physics Model**:
    *   **Drag**: Viscous resistance slows velocity.
    *   **Buoyancy**: Upward force based on density.
    *   **Input**: Camera-relative 3D movement (WASD + Jump/Crouch for Up/Down).
3.  **Oxygen usage**:
    *   **Suit ON**: Uses standard Oxygen Tank drain (Epic 2.1).
    *   **Suit OFF**: Uses `BreathState`. Hold breath ~30s. Drown (Health ticks) when empty.

## Implemented Components

### SwimmingComponents.cs
Location: `Assets/Scripts/Swimming/Components/SwimmingComponents.cs`

| Component | Description |
|-----------|-------------|
| `SwimmingState` | Tracks water level, submersion depth, and IsSwimming flag |
| `WaterProperties` | Zone properties: Density, Viscosity, Current, Buoyancy |
| `BreathState` | Tracks breath holding and drowning damage timer |
| `SwimmingMovementSettings` | Configuration for swim speed, drag, buoyancy force |
| `CanSwim` | Tag for entities that can swim |

## Implemented Systems

### WaterDetectionSystem
Location: `Assets/Scripts/Swimming/Systems/WaterDetectionSystem.cs`

- Detects `EnvironmentZone` with `Type = Underwater`.
- Uses `ZoneBounds` or `WaterProperties` to determine surface level.
- Calculates submersion depth and updates `SwimmingState`.
- Handles hysteresis for entering/exiting swim mode.

### SwimmingMovementSystem
Location: `Assets/Scripts/Swimming/Systems/SwimmingMovementSystem.cs`

- Active when `IsSwimming` is true.
- Applies:
  - **Camera-Relative Thrust**: Forward/Right based on camera view.
  - **Vertical Thrust**: Jump to ascend, Crouch to descend.
  - **Viscous Drag**: Velocity decay based on viscosity.
  - **Buoyancy**: Upward force when submerged.
  - **Currents**: Adds water velocity to player velocity.

### DrowningSystem
Location: `Assets/Scripts/Swimming/Systems/DrowningSystem.cs`

- Server-side logic.
- If `IsSubmerged` AND `!HasSuit`:
  - Drains `BreathState.CurrentBreath`.
  - If breath empty -> Apply damage to `Health`.
- Recovers breath when surface reached.

## Authoring Components

### SwimmingAuthoring
Location: `Assets/Scripts/Swimming/Authoring/SwimmingAuthoring.cs`

Add to **Player Prefab**. Configures:
- Swim speeds
- Drag/Buoyancy
- Breath capacity
- Drowning damage

### WaterZoneAuthoring
Location: `Assets/Scripts/Swimming/Authoring/WaterZoneAuthoring.cs`

Add to **Environment Zone** (Underwater). Configures:
- Density/Viscosity
- Current flow
- Surface level (Auto-calculated from bounds)

## Integration Guide

### 1. Player Setup
1.  Add `SwimmingAuthoring` component to the **Main Ghost Player Prefab**.
    - Verify `Player Height` (default 1.8).
    - Adjust `Swim Speed` and `Breath Capacity`.

### 2. Scene Setup (Water Zone)
1.  Create an Environment Zone (Box/Sphere).
2.  Set `EnvironmentZoneAuthoring`:
    - **Zone Type**: Underwater.
    - **Oxygen Required**: True (usually).
3.  Add `WaterZoneAuthoring`:
    - **Viscosity**: 0.5 (Easy) to 2.0 (Thick sludge).
    - **Buoyancy**: 0.1 (Float) or -0.1 (Sink).
    - **Auto Calculate Surface**: True.

## Testing
1.  **Test Object**: Use menu `GameObject > DIG - Test Objects > Traversal > Swimming Pool` to create a test tank.
2.  Walk into the water volume.
3.  **Verify**: Movement changes to swimming when chest-deep.
4.  **Verify**: Gravity feels reduced/replaced by buoyancy.
5.  **Verify**: Space/Ctrl moves Up/Down.
6.  **Verify**: Without suit, breath depletes → Damage.
7.  **Verify**: With suit (OxygenTank), breath does NOT deplete (uses Tank).
