# EPIC 13.22: Advanced Game Feel & System Parity

## Overview
This epic specifically targets the "Game Feel" and "Systemic Depth" gaps identified between our engine and Opsive UCC. While we have basic movement and interaction, we lack the cohesive "Surface System" (impacts, footsteps, decals) and the generic "Attribute System" that makes Opsive-based games feel polished and extensible.

## 1. The Unity Surface System (Impacts & FX)
**Current Status**: Scattered logic. Audio has `SurfaceDetectionService`, but Visuals (Decals, Particles) are ad-hoc.
**Opsive Standard**: A unified pipeline where `Physics Material` + `Impact Type` = `Surface Effect` (Audio + VFX + Footprint + Decal).
**Gap**: We need a centralized `SurfaceManager` that handles ALL output from a physical interaction.

### Implementation Plan
-   **`SurfaceDefinition` (SO)**: Defines properties for a surface (Metal, Dirt, Flesh).
    -   `FootstepSounds`: List of audio clips.
    -   `BulletImpactFX`: Particle prefab.
    -   `SlashImpactFX`: Particle prefab.
    -   `Decal`: Texture/Material.
-   **`SurfaceImpactSystem`**: Listens for `CollisionEvents` and `TriggerEvents`.
    -   Raycasts to get Texture/Material.
    -   Looks up `SurfaceDefinition`.
    -   Spawns VFX and plays Audio.

## 2. Generic Attribute System
**Current Status**: Health, Stamina, Oxygen likely hardcoded in separate components.
**Opsive Standard**: A generic `AttributeManager` that handles ANY float resource (Health, Shield, Battery, Hunger).
**Gap**: Lack of extensibility for new survival stats without writing new systems.

### Implementation Plan
-   **`Attribute` Component**:
    -   `CurrentValue`, `MinValue`, `MaxValue`.
    -   `RegenRate` (auto-refill).
    -   `DecayRate` (auto-drain).
-   **`AttributeSystem`**:
    -   Processes Regen/Decay.
    -   Handles "Events" (OnEmpty, OnFull).
-   **Integration**:
    -   UI binds to `DynamicBuffer<AttributeData>`.
    -   Items specify `AttributeModifier` (e.g., Medkit adds +50 to "Health").

## 3. Modular Item Actions
**Current Status**: 'Tools' are likely rigid. 'Weapons' might be their own thing.
**Opsive Standard**: Separation of `Item` (the object) and `ItemAction` (the logic).
-   *Example*: A Rifle has a `ShootableAction` (Fire bullets) and a `MeleeAction` (Butt stroke).
-   *Example*: A Flashlight has a `ToggleAction` and a `BatteryConsumeAction`.

### Implementation Plan
-   **`ItemAction` Component**:
    -   Abstract ID/Type for action logic.
-   **`ItemUseSystem`**:
    -   Delegates input (`PrimaryUse`, `SecondaryUse`) to the active ItemAction.
-   **Parity Goals**:
    -   Recoil profiles per action.
    -   Spread/Accuracy blooping.
    -   Ammo/Reload logic parity (Chambered rounds vs Mag size).

## 4. Technical Roadmap

### Phase 1: The Unified Surface System
1.  Define `SurfaceDefinition` asset workflow with Authoring.
2.  Implement `ImpactManagerSystem` for unified Audio/VFX spawning.
3.  Implement `DecalSystem` for rendering bullet holes/scars (using URP Decals or Mesh quads).

### Phase 2: Attribute Refactor
4.  Refactor `Health` and `Stamina` into generic `Attribute` components.
5.  Create `AttributeRegenSystem`.

### Phase 3: Item Action Modernization
6.  Refactor `WeaponSystem` into generic `ItemAction`s.
7.  Implement `Recoil` and `Spread` as generic modifiers on Actions.

## Success Criteria
- [ ] Shooting a wall plays a sound AND prompts a particle effect/decal specific to that wall's material.
- [ ] Walking on different surfaces changes footstep sounds.
- [ ] Adding a "Hunger" bar requires ZERO code changes (just config).
- [ ] Weapons support multiple actions (e.g., Fire + Melee Bash).

## Test Environment
To verify these features, the following test objects should be added to the `TraversalObjectCreator`:

### 13.22.T1: Attribute Stress Zone
- **Goal**: Verify Generic Attribute System (Health, Hunger, Stamina).
- **Setup**:
    - **Damage Zone**: Trigger volume draining "Health".
    - **Heal Station**: Trigger volume regenerating "Health".
    - **Fatigue Floor**: Surface that drains "Stamina" faster when walking.
- **Success**: UI bars update in real-time, death triggered when Health=0.

### 13.22.T2: Ballistics Range
- **Goal**: Verify Item Actions (Recoil, Spread) and Surface Impacts.
- **Setup**:
    - (Extends 13.18 references)
    - **Recoil Target**: Validates crosshair climb.
    - **Spread Wall**: Distance markers to visualize bullet spread patterns.
