# EPIC 5.2: Visual Effects (HUD & Visor)

**Priority**: LOW  
**Status**: **IMPLEMENTED** (See Assets/Scripts/Visuals/)  
**Goal**: Implement diegetic visual feedback (HUD, Visor damage) that enhances immersion and claustrophobia.
**Dependencies**: Epic 2.1 (Oxygen), Epic 4.1 (Health), Epic 3.1 (Environment)

## Design Notes
1.  **Diegetic HUD**:
    *   **Implementation**: `HudSwaySystem` manages swy/lag. `DiegeticHUD` component stores config.
2.  **Visor Damage**:
    *   **Cracks**: Driven by `DamageEvent` buffer via `VisorDamageSystem`. Updates `_CrackLevel` on material.
    *   **Condensation**: Driven by `Hypoxia`/`Frostbite` status effect via `HelmetEnvironmentEffectSystem`. Updates `_IceLevel`.
3.  **Flashlight**:
    *   **Logic**: `F` key toggles light. `FlashlightData` tracks battery.
    *   **Bridge**: `FlashlightSystem` updates Unity `Light` component directly.

## Implemented Components
- `HelmetVisor`: Tracks visual damage state.
- `DiegeticHUD`: Tracks opacity/sway state.
- `FlashlightData`: Tracks battery and state.
- `FlashlightReference`, `VisorReference`: Managed links to GameObject components.

## Implemented Systems
- `VisorDamageSystem`: Maps Damage -> Cracks.
- `FlashlightSystem`: Handles Input (F), Battery drain, and Light intensity/flicker.
- `HudSwaySystem`: Manages HUD offset logic (Basic implementation).
- `HelmetEnvironmentEffectSystem`: Maps Hypoxia -> Ice.

## Final Integration Guide

### 1. Player Prefab Setup
1.  Add `VisorAuthoring` component to the **Main Ghost Player Prefab**.
2.  **Flashlight Setup**:
    - **In Scene**: Add a Spotlight child to `Main Camera`, name it "Flashlight". (This handles Local Player view).
    - **In Prefab**: Add a Spotlight child to your Player Head/Body. (This handles Remote Player view).
    - Assign the Prefab light to `VisorAuthoring.Flashlight`. (Note: Local Player system will lazy-load the Camera light instead).
3.  **HUD**: Create a generic HUD (Canvas or Mesh) as a child of the Camera. Assign its root to `HudRoot` field.
4.  **Visor Material**: Assign a Renderer to `VisorRenderer` field. This Renderer's material will receive `SetFloat` calls for `_CrackLevel` and `_IceLevel`.

### 2. Shader Setup
1.  Ensure your Visor Material uses a shader that exposes `_CrackLevel` (Float 0-1) and `_IceLevel` (Float 0-1).
2.  If using Standard Shader, these properties won't do anything visible unless you modify the shader or use a custom Shader Graph.

### 3. Temporary Flashlight HUD
For testing battery levels:
1.  Add the `FlashlightHUDBuilder` component to any persistent GameObject (e.g., GameManager).
2.  Play the game. The HUD will auto-create in the bottom-right corner.
3.  Press `F` to toggle flashlight. Watch the battery bar drain.

**For a Real HUD**: Create a custom Canvas with your own styling and use the `FlashlightHUD` component directly, assigning your custom UI elements.

## Tasks Status
- [x] Create `VisorComponents` data structs.
- [x] Create `VisorAuthoring` baker.
- [x] Implement `FlashlightSystem` (Input/Battery).
- [x] Implement `VisorDamageSystem` (Cracks).
- [x] Implement `HelmetEnvironmentEffectSystem` (Fog).
- [x] Map `F` Key in `PlayerInputSystem`.
- [x] Create `FlashlightHUD` temporary UI.
