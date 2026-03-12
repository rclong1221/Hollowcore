# EPIC 18.19: Unified Targeting, Ability & Attack System — Per-Paradigm Combat Pipeline

**Status:** IN PROGRESS (Phases 1-8 implemented)
**Priority:** Critical (core gameplay — combat must work correctly across all 6 paradigms)
**Dependencies:**
- EPIC 15.20: Input Paradigm Framework (`ParadigmStateMachine`, `IParadigmConfigurable`, profiles)
- EPIC 15.21: Input Action Layer (`ParadigmInputManager`, action maps)
- EPIC 15.18: Cursor Hover & Click-to-Select (`CursorHoverSystem`, `CursorClickTargetSystem`)
- EPIC 18.15: Click-to-Move & WASD Gating (movement per paradigm)
- EPIC 18.18: Targeting & Attack Coverage (targeting mode audit)
- EPIC 16.8: Player Resource Framework (`PlayerResource`, resource cost deduction)
- EPIC 16.7: Unified VFX Event Pipeline (`VFXRequest`, telegraph spawning)
- `TargetData` / `TargetingMode` / `TargetingConfig` (existing — `Assets/Scripts/Targeting/`)
- `ActiveLockBehavior` / `LockBehaviorType` / `LockBehaviorHelper` (existing — `Assets/Scripts/Targeting/Core/`)
- `WeaponFireSystem` / `MeleeActionSystem` (existing — `Assets/Scripts/Weapons/Systems/`)
- `CombatResolutionSystem` / `PendingCombatHit` (existing — `Assets/Scripts/Combat/Systems/`)
- AI ability system (reference architecture — `Assets/Scripts/AI/Components/`, `Assets/Scripts/AI/Systems/`)
- `AbilityCharges` / `AbilityChargeSystem` (existing — `Assets/Scripts/Player/Components/`, `Assets/Scripts/Player/Systems/`)
- Unity NetCode (`GhostComponent`, `AllPredicted`, prediction rollback safety)

**Feature:** Build a unified, modular, per-paradigm combat pipeline that:
1. Bridges paradigm switching to targeting mode and lock behavior (currently completely disconnected)
2. Provides a full player combat ability system (currently only locomotion scaffolding exists)
3. Delivers designer-facing tooling for rapid ability authoring and paradigm tuning
4. Runs at AAA performance via Burst-compiled ECS systems with zero managed allocations in hot paths

---

## Problem

### 1. Paradigm–Targeting Disconnect

`InputParadigmProfile` defines cursor, movement, camera, and facing behavior — but has **zero targeting fields**. `TargetingConfig` is a separate ScriptableObject not referenced by profiles. At runtime, `TargetDataAuthoring.InitialMode` (baked at startup) sets the targeting mode and it **never changes**. `LockBehaviorHelper.SetMode()` exists but is **never called** outside the debug `TargetingModeTester`. Switching from Shooter to ARPG via `ParadigmStateMachine` changes cursor, movement, and camera — but targeting stays at whatever was baked.

**Result:** All paradigms use the same targeting mode (default `CameraRaycast`) regardless of whether the player is in Shooter, MMO, ARPG, MOBA, or TwinStick mode.

### 2. No Player Combat Ability System

The player-side ability framework in `Assets/Scripts/Player/Abilities/` contains only locomotion scaffolding:
- `AbilityState` — tracks active ability index and timing (ghost-replicated)
- `AbilityDefinition` — buffer element with priority, start/stop types, blocking masks
- `AbilityTriggerSystem` — evaluates start/stop conditions (Jump, Crouch, Sprint, Fall only)
- `AbilityPrioritySystem` — resolves which ability wins (locomotion priorities)
- `AbilityLifecycleSystem` — pending→active timing transitions

**Missing entirely:**
- Input-to-ability trigger mapping (no system reads `PlayerInput` to fire combat abilities)
- Cast phases (Telegraph → Casting → Active → Recovery — the AI has this, players don't)
- Cooldown tracking per ability (AI has `AbilityCooldownState`, players don't)
- GCD (Global Cooldown) system
- Ability-to-weapon integration (melee and ranged are separate from abilities)
- Ability animation/VFX triggering
- Server-side cooldown validation

Meanwhile, the **AI ability system** (`Assets/Scripts/AI/`) is fully mature: 40+ fields per ability definition, 5-phase lifecycle, per-ability cooldowns + GCD + cooldown groups + charges, 10 targeting modes, modifier slots, resource costs, and conditional selection. This is the reference architecture.

### 3. No Per-Paradigm Attack Semantics

Each paradigm has distinct attack expectations:
- **Shooter**: Crosshair aim, hold-to-fire, ADS
- **MMO**: Tab-target, hotbar abilities on selected target, auto-attack
- **ARPG**: Cursor-aim, click-to-attack, ground-target abilities
- **MOBA**: Cursor-aim, attack-move, smartcast/quickcast abilities
- **TwinStick**: Stick/cursor aim, rapid fire, auto-target assist

Currently `WeaponFireSystem` is paradigm-agnostic (reads `TargetData.AimDirection` with a 3-tier fallback). `MeleeActionSystem` is entirely separate. No system coordinates which attack behavior is active per paradigm.

---

## Root Cause Analysis

### Why Targeting Is Disconnected From Paradigm

```
ParadigmStateMachine.TransitionTo(profile):
  Phase 1: CaptureSnapshots()      — all IParadigmConfigurables
  Phase 2: ValidateAll()            — CanConfigure() checks
  Phase 3: DisableCurrentMaps()     — input action map swap
  Phase 4: ConfigureAll()           — ordered Configure() calls
  Phase 5: EnableNewMaps()          — new input action maps
  Phase 6: SyncToECS()              — ParadigmSettingsSyncSystem

Registered IParadigmConfigurables (ConfigurationOrder):
  CursorController        (0)    — cursor lock/visibility
  CameraOrbitController   (10)   — orbit mode
  MovementRouter          (100)  — WASD/click-to-move
  ClickToMoveHandler      (110)  — pathfinding
  AttackMoveHandler       (115)  — MOBA attack-move
  FacingController        (200)  — character facing mode

NO targeting configurable exists.
NO lock behavior configurable exists.
NO ability system configurable exists.
```

The `IParadigmConfigurable` plugin system is designed for exactly this — register a `TargetingConfigurable` and it will be called during paradigm transitions. The infrastructure exists; nobody plugged targeting into it.

### Why Player Abilities Are Locomotion-Only

The player ability system was built for locomotion state management (Jump, Crouch, Sprint, Fall) where abilities are essentially state flags with priority resolution. Combat abilities require a fundamentally different lifecycle:

```
Locomotion ability:  Input pressed → active → input released → inactive
                     (instantaneous, no phases, no cooldown, no cost)

Combat ability:      Input pressed → validate (cost, cooldown, GCD)
                     → Telegraph (optional visual warning)
                     → Casting (interruptible, rooted/slowed)
                     → Active (damage delivery, hitbox windows)
                     → Recovery (post-cast lockout)
                     → Cooldown starts
```

The AI system implemented the full lifecycle. The player system never evolved past the locomotion pattern.

---

## Architecture Overview

### Module Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     PARADIGM LAYER (Managed)                     │
│                                                                  │
│  InputParadigmProfile                                            │
│    + targetingMode: TargetingMode           ← NEW FIELD          │
│    + lockBehaviorPreset: LockBehaviorType   ← NEW FIELD          │
│    + targetingConfig: TargetingConfig        ← NEW REF (optional)│
│    + abilityBarLayout: AbilityBarLayout      ← NEW REF           │
│                                                                  │
│  IParadigmConfigurable plugins:                                  │
│    TargetingConfigurable  (250)  ← NEW — sets TargetData.Mode    │
│    LockBehaviorConfigurable (255) ← NEW — calls SetMode()        │
│    AbilityBarConfigurable (300)  ← NEW — swaps UI layout         │
│                                                                  │
│  ParadigmStateMachine.TransitionTo() calls them in order         │
└────────────────────┬────────────────────────────────────────────┘
                     │ Configure() writes ECS singletons
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                     TARGETING LAYER (ECS)                        │
│                                                                  │
│  ParadigmSettings singleton                                      │
│    + ActiveTargetingMode: TargetingMode     ← NEW FIELD          │
│                                                                  │
│  ActiveLockBehavior singleton (existing)                         │
│    + BehaviorType, Features, InputMode, etc.                     │
│                                                                  │
│  TargetData per-entity (existing)                                │
│    + TargetEntity, TargetPoint, AimDirection, Mode               │
│                                                                  │
│  Systems:                                                        │
│    TargetingModeDispatcherSystem ← NEW — applies paradigm mode   │
│    PlayerFacingSystem (existing) — writes AimDirection            │
│    CursorHoverSystem (existing)  — cursor-free targeting          │
│    CursorClickTargetSystem (existing) — click-to-select          │
│    CameraRaycastTargetingSystem  ← NEW ECS replacement           │
│    CursorAimTargetingSystem      ← NEW ECS replacement           │
└────────────────────┬────────────────────────────────────────────┘
                     │ TargetData.AimDirection + TargetEntity
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                     ABILITY LAYER (ECS, Burst)                   │
│                                                                  │
│  PlayerAbilitySlot buffer (AllPredicted)     ← NEW               │
│    + AbilityId, CooldownRemaining, ChargesRemaining              │
│    + CooldownDuration, MaxCharges, ResourceCost, CastTime        │
│                                                                  │
│  PlayerAbilityState component (AllPredicted) ← NEW               │
│    + ActiveSlotIndex, Phase, PhaseElapsed, GCDRemaining          │
│    + QueuedSlotIndex (input queueing during GCD)                 │
│                                                                  │
│  AbilityDatabase (BlobAsset)                 ← NEW               │
│    + Full ability definitions (mirrors AI AbilityDefinition)     │
│    + Paradigm-specific overrides (cast behavior per mode)        │
│                                                                  │
│  Systems (PlayerAbilitySystemGroup — PredictedFixedStep):        │
│    PlayerAbilityInputSystem     ← NEW — reads input → triggers   │
│    PlayerAbilityCooldownSystem  ← NEW — ticks cooldowns + GCD    │
│    PlayerAbilityValidationSystem ← NEW — cost/cooldown/range     │
│    PlayerAbilityExecutionSystem ← NEW — phase lifecycle          │
│    PlayerAbilityCostSystem      ← NEW — resource deduction       │
│    PlayerAbilityEffectSystem    ← NEW — PendingCombatHit/VFX     │
└────────────────────┬────────────────────────────────────────────┘
                     │ PendingCombatHit entities
                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                     RESOLUTION LAYER (existing)                  │
│                                                                  │
│  CombatResolutionSystem → CombatResultEvent                      │
│  DamageApplicationSystem → health damage                         │
│  WeaponFireSystem → hitscan (unchanged, reads TargetData)        │
│  MeleeActionSystem → hitbox combos (unchanged)                   │
│  VFXRequest pipeline → telegraph/impact VFX                      │
└─────────────────────────────────────────────────────────────────┘
```

### Data Flow Per Paradigm

| Paradigm | TargetingMode | LockBehavior | AimDirection Source | Attack Input | Ability Input |
|----------|---------------|--------------|---------------------|--------------|---------------|
| Shooter | CameraRaycast | HardLock | Camera yaw/pitch | LMB=Fire | 1-9 hotkeys |
| ShooterHybrid | CameraRaycast (locked) / ClickSelect (Alt) | HardLock / SoftLock | Camera / cursor | LMB=Fire | 1-9 hotkeys |
| MMO | ClickSelect | SoftLock | Player→target | Hotbar abilities | 1-9 hotkeys, auto-attack |
| ARPG | CursorAim | IsometricLock | Cursor-to-world | LMB=AttackAtCursor | 1-4 + Q/W/E/R |
| MOBA | CursorAim | IsometricLock | Cursor-to-world | LMB=AttackAtCursor | Q/W/E/R |
| TwinStick | CursorAim | TwinStick | Mouse/stick position | LMB=Fire | 1-4 hotkeys |

---

## Core Types

### New ECS Components

#### PlayerAbilitySlot (Buffer, AllPredicted)

```csharp
// Assets/Scripts/Combat/Abilities/Components/PlayerAbilitySlot.cs
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
[InternalBufferCapacity(6)] // 6 ability slots — fits within 16KB budget
public struct PlayerAbilitySlot : IBufferElementData
{
    [GhostField] public int AbilityId;             // Index into AbilityDatabase blob
    [GhostField] public byte SlotIndex;             // 0-5 position in ability bar
    [GhostField] public float CooldownRemaining;    // Ticks down per frame
    [GhostField] public byte ChargesRemaining;      // Current charges (0 = on cooldown)
    [GhostField] public float ChargeRechargeElapsed; // Progress toward next charge
}
```

**16KB budget note:** `InternalBufferCapacity(6)` × ~24 bytes = 144 bytes inline. The player entity already has headroom from EPIC 15.25 buffer reductions (CollisionEvent=2, StatusEffectRequest=4, ReviveRequest=2). 6 slots is well within budget.

#### PlayerAbilityState (Component, AllPredicted)

```csharp
// Assets/Scripts/Combat/Abilities/Components/PlayerAbilityState.cs
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PlayerAbilityState : IComponentData
{
    [GhostField] public byte ActiveSlotIndex;   // 255 = none
    [GhostField] public byte QueuedSlotIndex;   // 255 = none (input queue during GCD)
    [GhostField] public AbilityCastPhase Phase; // Idle/Telegraph/Casting/Active/Recovery
    [GhostField] public float PhaseElapsed;     // Time in current phase
    [GhostField] public float GCDRemaining;     // Global Cooldown timer
    [GhostField] public byte Flags;             // Interruptible, Rooted, CanCancel
}

public enum AbilityCastPhase : byte
{
    Idle = 0,
    Telegraph = 1,   // Visual warning (optional, 0-duration skips)
    Casting = 2,     // Interruptible wind-up
    Active = 3,      // Damage/effect delivery
    Recovery = 4     // Post-cast lockout before next ability
}
```

#### AbilityDef (BlobAsset)

```csharp
// Assets/Scripts/Combat/Abilities/Data/AbilityDef.cs
public struct AbilityDef
{
    // Identity
    public int AbilityId;
    public FixedString64Bytes Name;
    public AbilityCategory Category;        // Attack, Heal, Buff, Debuff, Utility, Movement

    // Timing
    public float TelegraphDuration;          // 0 = instant (no telegraph phase)
    public float CastDuration;              // 0 = instant cast
    public float ActiveDuration;            // Hitbox/effect active window
    public float RecoveryDuration;          // Post-cast lockout
    public float CooldownDuration;          // Per-ability cooldown
    public float GCDDuration;              // GCD contribution (0 = off-GCD)
    public byte MaxCharges;                // 1 = standard, 2+ = charge-based
    public float ChargeRechargeDuration;    // Time per charge regeneration

    // Cost
    public ResourceType CostResource;       // Mana, Stamina, Energy, etc.
    public float CostAmount;
    public AbilityCostTiming CostTiming;    // OnCast, PerTick, OnComplete

    // Targeting
    public AbilityTargetType TargetType;    // Self, SingleTarget, GroundTarget, Cone, Line, AoE
    public float Range;                     // Max cast range
    public float Radius;                    // AoE radius (if applicable)
    public float ConeAngle;                // Cone half-angle (if applicable)
    public bool RequiresTarget;             // Must have TargetEntity (MMO-style)
    public bool RequiresLineOfSight;

    // Damage/Effect
    public float BaseDamage;
    public DamageType DamageType;           // Physical, Fire, Ice, Lightning, etc.
    public int HitCount;                    // 1 = single hit, 2+ = multi-hit
    public float HitInterval;              // Time between multi-hits

    // Movement During Cast
    public AbilityCastMovement CastMovement; // Free, Slowed, Rooted
    public float SlowFactor;                // Movement speed multiplier during Slowed

    // Animation
    public int AnimatorTriggerHash;
    public int AnimatorStateHash;

    // VFX
    public int TelegraphVFXTypeId;          // VFXTypeDatabase reference
    public int CastVFXTypeId;
    public int ImpactVFXTypeId;

    // Modifiers (on-hit effects)
    public ModifierType Modifier1Type;
    public float Modifier1Value;
    public ModifierType Modifier2Type;
    public float Modifier2Value;

    // Paradigm Overrides
    public AbilityParadigmFlags AllowedParadigms; // Bitmask: which paradigms can use this ability
    public AbilityTargetType ShooterTargetOverride; // Override target type when in Shooter mode
    public AbilityTargetType ARPGTargetOverride;    // Override target type when in ARPG mode
}

[Flags]
public enum AbilityParadigmFlags : byte
{
    Shooter    = 1 << 0,
    MMO        = 1 << 1,
    ARPG       = 1 << 2,
    MOBA       = 1 << 3,
    TwinStick  = 1 << 4,
    All        = 0xFF
}

public enum AbilityCastMovement : byte { Free = 0, Slowed = 1, Rooted = 2 }
public enum AbilityCostTiming : byte { OnCast = 0, PerTick = 1, OnComplete = 2, OnHit = 3 }
public enum AbilityTargetType : byte
{
    Self = 0,           // No targeting needed
    SingleTarget = 1,   // Requires TargetEntity
    GroundTarget = 2,   // Uses TargetPoint (cursor position)
    Cone = 3,           // AimDirection + ConeAngle
    Line = 4,           // AimDirection + Range (skillshot)
    AoE = 5,            // TargetPoint + Radius
    Cleave = 6,         // AimDirection + arc (melee swing)
    Projectile = 7      // AimDirection + Range (fires projectile entity)
}
```

#### AbilityDatabase (BlobAsset, Singleton)

```csharp
// Assets/Scripts/Combat/Abilities/Data/AbilityDatabase.cs
public struct AbilityDatabaseBlob
{
    public BlobArray<AbilityDef> Abilities;  // Indexed by AbilityId
}

// Singleton component holding the blob reference
public struct AbilityDatabaseRef : IComponentData
{
    public BlobAssetReference<AbilityDatabaseBlob> Value;
}
```

### Modified ECS Components

#### ParadigmSettings (Singleton — add field)

```csharp
// Assets/Scripts/Core/Input/Paradigm/Components/ParadigmSettings.cs
// ADD:
[GhostField] public TargetingMode ActiveTargetingMode;  // Set by TargetingConfigurable on paradigm switch
```

#### InputParadigmProfile (SO — add fields)

```csharp
// Assets/Scripts/Core/Input/Paradigm/InputParadigmProfile.cs
// ADD under new [Header("Targeting")] section:
public TargetingMode defaultTargetingMode = TargetingMode.CameraRaycast;
public LockBehaviorType defaultLockBehavior = LockBehaviorType.HardLock;
public TargetingConfig targetingConfig;  // Optional advanced config override

// ADD under new [Header("Abilities")] section:
public AbilityBarLayout abilityBarLayout;  // Which UI layout to show (hotbar vs quickcast)
```

### New Managed Types

#### TargetingConfigurable (IParadigmConfigurable)

```csharp
// Assets/Scripts/Core/Input/Paradigm/Subsystems/TargetingConfigurable.cs
public class TargetingConfigurable : MonoBehaviour, IParadigmConfigurable
{
    public int ConfigurationOrder => 250;  // After FacingController(200), before UI
    public string SubsystemName => "Targeting";

    private TargetingMode _previousMode;

    public IConfigSnapshot CaptureSnapshot() => new TargetingSnapshot(_previousMode);
    public bool CanConfigure(InputParadigmProfile p, out string err) { err = null; return true; }

    public void Configure(InputParadigmProfile profile)
    {
        _previousMode = GetCurrentMode();

        // 1. Set TargetData.Mode on local player entity
        SetTargetingMode(profile.defaultTargetingMode);

        // 2. Apply TargetingConfig overrides if present
        if (profile.targetingConfig != null)
            ApplyTargetingConfig(profile.targetingConfig);

        // 3. Clear stale target when switching away from ClickSelect
        if (_previousMode == TargetingMode.ClickSelect &&
            profile.defaultTargetingMode != TargetingMode.ClickSelect)
            ClearCurrentTarget();
    }

    public void Rollback(IConfigSnapshot snap)
    {
        SetTargetingMode(((TargetingSnapshot)snap).Mode);
    }
}
```

#### LockBehaviorConfigurable (IParadigmConfigurable)

```csharp
// Assets/Scripts/Core/Input/Paradigm/Subsystems/LockBehaviorConfigurable.cs
public class LockBehaviorConfigurable : MonoBehaviour, IParadigmConfigurable
{
    public int ConfigurationOrder => 255;  // Right after targeting
    public string SubsystemName => "LockBehavior";

    public void Configure(InputParadigmProfile profile)
    {
        LockBehaviorHelper.SetMode(profile.defaultLockBehavior);
    }
}
```

### Designer Tooling

#### AbilityDefinitionSO (ScriptableObject — player version)

```csharp
// Assets/Scripts/Combat/Abilities/Authoring/AbilityDefinitionSO.cs
[CreateAssetMenu(menuName = "DIG/Combat/Ability Definition")]
public class AbilityDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string abilityName;
    public Sprite icon;
    public AbilityCategory category;
    public string tooltip;

    [Header("Timing")]
    public float telegraphDuration;
    public float castDuration;
    public float activeDuration;
    public float recoveryDuration;
    public float cooldownDuration;
    public float gcdDuration = 1.5f;
    public int maxCharges = 1;
    public float chargeRechargeDuration;

    [Header("Cost")]
    public ResourceType costResource;
    public float costAmount;
    public AbilityCostTiming costTiming;

    [Header("Targeting")]
    public AbilityTargetType targetType;
    public float range = 10f;
    public float radius;
    public float coneAngle;
    public bool requiresTarget;
    public bool requiresLineOfSight = true;

    [Header("Damage")]
    public float baseDamage;
    public DamageType damageType;
    public int hitCount = 1;
    public float hitInterval;

    [Header("Cast Movement")]
    public AbilityCastMovement castMovement;
    [Range(0f, 1f)] public float slowFactor = 0.5f;

    [Header("Animation")]
    public string animatorTrigger;
    public string animatorState;

    [Header("VFX")]
    public VFXTypeSO telegraphVFX;
    public VFXTypeSO castVFX;
    public VFXTypeSO impactVFX;

    [Header("On-Hit Modifiers")]
    public ModifierType modifier1Type;
    public float modifier1Value;
    public ModifierType modifier2Type;
    public float modifier2Value;

    [Header("Paradigm Rules")]
    public AbilityParadigmFlags allowedParadigms = AbilityParadigmFlags.All;
    public AbilityTargetType shooterTargetOverride;
    public AbilityTargetType arpgTargetOverride;

    public AbilityDef BakeToBlob() { /* ... */ }
}
```

#### AbilityLoadoutSO (ScriptableObject)

```csharp
// Assets/Scripts/Combat/Abilities/Authoring/AbilityLoadoutSO.cs
[CreateAssetMenu(menuName = "DIG/Combat/Ability Loadout")]
public class AbilityLoadoutSO : ScriptableObject
{
    [Header("Ability Slots")]
    public AbilityDefinitionSO[] slots = new AbilityDefinitionSO[6];

    [Header("Auto-Attack")]
    public AbilityDefinitionSO autoAttack;  // Slot -1, triggered by attack input when no ability queued

    public BlobAssetReference<AbilityDatabaseBlob> BakeToBlob() { /* ... */ }
}
```

---

## System Design

### System Execution Order

```
InitializationSystemGroup:
  LockBehaviorDispatcherSystem (existing) — ensures ActiveLockBehavior singleton
  AbilityDatabaseBootstrapSystem ← NEW — loads AbilityDatabaseRef from Resources

PredictedFixedStepSimulationSystemGroup:
  PlayerAbilitySystemGroup ← NEW GROUP
    PlayerAbilityInputSystem      ← NEW — reads PlayerInput → sets QueuedSlotIndex
    PlayerAbilityCooldownSystem   ← NEW — ticks CooldownRemaining, GCDRemaining, charges
    PlayerAbilityValidationSystem ← NEW — validates cost/cooldown/range/target
    PlayerAbilityExecutionSystem  ← NEW — phase state machine (Idle→Telegraph→Cast→Active→Recovery)
    PlayerAbilityCostSystem       ← NEW — deducts resources at CostTiming
    PlayerAbilityEffectSystem     ← NEW — creates PendingCombatHit + VFXRequest entities

  TargetingModeDispatcherSystem ← NEW — applies ActiveTargetingMode from ParadigmSettings
  CursorAimTargetingSystem      ← NEW — cursor-to-world for CursorAim mode
  PlayerFacingSystem (existing) — writes AimDirection

  DamageSystemGroup (existing):
    CombatResolutionSystem — resolves PendingCombatHit → CRE
    DamageApplicationSystem — applies health damage

PresentationSystemGroup:
  AbilityUIBridgeSystem ← NEW — PlayerAbilityState/Slot → UI (cooldowns, GCD, cast bar)
```

### System Details

#### 1. TargetingModeDispatcherSystem

```csharp
// Assets/Scripts/Targeting/Systems/TargetingModeDispatcherSystem.cs — NEW
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerFacingSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
```

Reads `ParadigmSettings.ActiveTargetingMode` each frame. When mode changes (detected via cached previous value):
- Writes `TargetData.Mode` on the local player entity
- Clears stale `TargetData.TargetEntity` when switching away from ClickSelect/LockOn
- Enables/disables `CursorHoverSystem` and `CursorClickTargetSystem` based on whether mode requires cursor interaction

This system replaces the current "baked at startup, never changes" pattern with reactive paradigm-driven targeting.

#### 2. CursorAimTargetingSystem

```csharp
// Assets/Scripts/Targeting/Systems/CursorAimTargetingSystem.cs — NEW
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(TargetingModeDispatcherSystem))]
[UpdateBefore(typeof(PlayerFacingSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
```

When `TargetData.Mode == CursorAim`:
- Performs screen-to-world raycast from cursor position (via `Camera.main.ScreenPointToRay`)
- Writes `TargetData.TargetPoint` (world hit position) and `TargetData.AimDirection` (player→hit direction)
- Ground plane fallback if raycast misses geometry
- Runs as managed `SystemBase` (needs `Camera.main` access — same pattern as existing MonoBehaviour targeting implementations)

This replaces the `CursorAimTargeting` MonoBehaviour with a system that only runs when the paradigm requires it.

#### 3. PlayerAbilityInputSystem

```csharp
// Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityInputSystem.cs — NEW
[UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
[UpdateBefore(typeof(PlayerAbilityCooldownSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                   WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.LocalSimulation)]
```

Reads `PlayerInput` ability input fields (Ability1-6 + AutoAttack) and writes `PlayerAbilityState.QueuedSlotIndex`:
- If `Phase == Idle` and `GCDRemaining <= 0`: immediate queue
- If `Phase != Idle`: input queueing (stores in `QueuedSlotIndex`, processed when current ability completes)
- Respects `AbilityParadigmFlags` — filters out abilities not allowed in current paradigm

**Input mapping per paradigm:**
- Shooter/TwinStick: 1-6 keys, auto-attack on LMB
- MMO: 1-9 keys, auto-attack on RMB (when target selected)
- ARPG: Q/W/E/R + 1-2, auto-attack on LMB
- MOBA: Q/W/E/R + D/F, auto-attack on LMB

Input mapping is configured via the `Combat_*` action maps that `ParadigmInputManager` already swaps per paradigm.

#### 4. PlayerAbilityCooldownSystem (Burst)

```csharp
// Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityCooldownSystem.cs — NEW
[UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
[BurstCompile]
```

Per-frame work:
- Decrements `PlayerAbilityState.GCDRemaining` by `deltaTime`
- For each `PlayerAbilitySlot`: decrements `CooldownRemaining` by `deltaTime`, increments `ChargeRechargeElapsed`, grants charge when recharged
- Zero managed allocations — pure arithmetic on ghost-replicated data

#### 5. PlayerAbilityValidationSystem (Burst)

```csharp
// Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityValidationSystem.cs — NEW
[UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
[UpdateAfter(typeof(PlayerAbilityCooldownSystem))]
[BurstCompile]
```

When `QueuedSlotIndex != 255`:
1. Look up `AbilityDef` from `AbilityDatabaseRef` blob
2. Check `ChargesRemaining > 0` (or `CooldownRemaining <= 0` for non-charge abilities)
3. Check `GCDRemaining <= 0` (unless ability is off-GCD)
4. Check resource cost (`PlayerResource` sufficient)
5. Check range (`TargetData.TargetDistance <= Range`, if `RequiresTarget`)
6. Check target exists (if `RequiresTarget` and `TargetData.TargetEntity == Entity.Null` → reject)
7. Check line of sight (if `RequiresLineOfSight` — uses a pre-computed LOS flag, not per-frame raycast)
8. If all pass: transition `Phase` from `Idle → Telegraph` (or `Casting` if `TelegraphDuration == 0`)
9. If fail: clear `QueuedSlotIndex`, optionally emit `AbilityFailReason` for UI feedback

All checks are Burst-compatible — reads from blob asset + ECS components only.

#### 6. PlayerAbilityExecutionSystem (Burst)

```csharp
// Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityExecutionSystem.cs — NEW
[UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
[UpdateAfter(typeof(PlayerAbilityValidationSystem))]
[BurstCompile]
```

Phase state machine (mirrors AI `AbilityExecutionSystem` architecture):

```
Idle → [validation passes] → Telegraph
  PhaseElapsed += dt
  if PhaseElapsed >= TelegraphDuration → Casting

Telegraph → Casting
  PhaseElapsed += dt
  if Interruptible && interrupted → Idle (cancel)
  if CastMovement == Rooted → zero movement input
  if PhaseElapsed >= CastDuration → Active

Casting → Active
  PhaseElapsed += dt
  Execute hit(s) at intervals (HitInterval)
  if PhaseElapsed >= ActiveDuration → Recovery

Active → Recovery
  PhaseElapsed += dt
  if PhaseElapsed >= RecoveryDuration → Idle
  Start per-ability cooldown + GCD

Recovery → Idle
  Check QueuedSlotIndex for chained ability
```

Movement gating: When `CastMovement == Rooted`, system writes a flag that `PlayerMovementSystem` reads to zero movement (same pattern as existing stun/root in `CombatState`). When `CastMovement == Slowed`, writes a speed multiplier.

#### 7. PlayerAbilityEffectSystem

```csharp
// Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityEffectSystem.cs — NEW
[UpdateInGroup(typeof(PlayerAbilitySystemGroup))]
[UpdateAfter(typeof(PlayerAbilityExecutionSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                   WorldSystemFilterFlags.LocalSimulation)]
```

When `Phase == Active` and hit timing triggers:
- Creates `PendingCombatHit` entities (same pipeline as existing weapon/melee systems)
- Target resolution per `AbilityTargetType`:
  - `SingleTarget`: reads `TargetData.TargetEntity`
  - `GroundTarget`/`AoE`: creates hit entities for all enemies within `Radius` of `TargetData.TargetPoint`
  - `Cone`/`Cleave`: overlap query with angle check from `TargetData.AimDirection`
  - `Line`/`Projectile`: creates projectile entity with `AimDirection` + `Range`
- Sets `PendingCombatHit.Modifier1/2` from ability definition (flows through existing `CombatResolutionSystem`)
- Creates `VFXRequest` entities for impact VFX (flows through existing VFX pipeline)

Runs on Server|Local only — damage must be authoritative. Client predicts the phase transitions and timing (via `PlayerAbilityState` AllPredicted) but not the damage creation.

#### 8. AbilityUIBridgeSystem

```csharp
// Assets/Scripts/Combat/Abilities/UI/AbilityUIBridgeSystem.cs — NEW
[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                   WorldSystemFilterFlags.LocalSimulation)]
```

Managed system that reads `PlayerAbilityState` and `PlayerAbilitySlot` buffer from the local player entity and dispatches to UI:
- Per-slot cooldown progress (for cooldown sweep animation)
- Charge count per slot
- GCD progress (for global cooldown overlay)
- Active ability cast bar (Phase + PhaseElapsed / PhaseDuration)
- Ability fail reasons (insufficient resource, out of range, no target)

Uses the existing `CombatUIRegistry` → `IAbilityUIProvider` adapter pattern (same as damage numbers).

---

## Per-Paradigm Profile Defaults

### Targeting Mode + Lock Behavior Mapping

| Profile | `defaultTargetingMode` | `defaultLockBehavior` | Rationale |
|---------|------------------------|-----------------------|-----------|
| Shooter | `CameraRaycast` | `HardLock` | Standard TPS — crosshair aim, tab-lock for focus targets |
| ShooterHybrid | `CameraRaycast` | `HardLock` | Same as Shooter; Alt-free cursor allows ClickSelect temporarily |
| MMO | `ClickSelect` | `SoftLock` | Tab-target tradition — click enemy, abilities use TargetEntity |
| ARPG Classic | `CursorAim` | `IsometricLock` | Isometric aim-at-cursor, character faces cursor direction |
| ARPG Hybrid | `CursorAim` | `IsometricLock` | Same targeting as Classic, WASD movement instead of click-to-move |
| MOBA | `CursorAim` | `IsometricLock` | Cursor-aim abilities, click-to-move with RMB |
| TwinStick | `CursorAim` | `TwinStick` | Mouse/stick controls aim direction, rapid-fire with sticky aim |

### Ability Bar Layout Mapping

| Paradigm | Layout | Slot Count | Input Map |
|----------|--------|------------|-----------|
| Shooter | Compact (bottom-right) | 4+2 | 1-4 + Q/E |
| MMO | Full hotbar (bottom-center) | 12 (2 rows of 6) | 1-6 + Shift+1-6 |
| ARPG | Diamond (bottom-center) | 6 | Q/W/E/R + 1/2 |
| MOBA | QWER bar (bottom-center) | 6 | Q/W/E/R + D/F |
| TwinStick | Compact (bottom-right) | 4 | 1-4 |

---

## Implementation Phases

### Phase 1: Paradigm–Targeting Bridge (4 files)

**Goal:** Paradigm switching changes targeting mode and lock behavior. No new components — wires existing infrastructure.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Core/Input/Paradigm/InputParadigmProfile.cs` | SO | Add `defaultTargetingMode`, `defaultLockBehavior`, `targetingConfig` fields |
| `Assets/Scripts/Core/Input/Paradigm/Subsystems/TargetingConfigurable.cs` | MonoBehaviour | **NEW** — `IParadigmConfigurable` that writes `TargetData.Mode` on paradigm switch |
| `Assets/Scripts/Core/Input/Paradigm/Subsystems/LockBehaviorConfigurable.cs` | MonoBehaviour | **NEW** — `IParadigmConfigurable` that calls `LockBehaviorHelper.SetMode()` |
| `Assets/Scripts/Core/Input/Paradigm/Components/ParadigmSettings.cs` | Singleton | Add `ActiveTargetingMode` field |
| `Assets/Data/Input/Profiles/*.asset` | Assets | Set per-profile targeting mode + lock behavior values |

**Verification:**
- Switch Shooter → ARPG via `ParadigmDemoUI` → targeting mode changes from CameraRaycast to CursorAim
- Switch ARPG → MMO → targeting mode changes to ClickSelect
- Round-trip all 6 paradigms → no stale targeting state

### Phase 2: Cursor-Aim Targeting System (2 files)

**Goal:** ARPG/MOBA/TwinStick paradigms get proper cursor-to-world aiming.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Targeting/Systems/CursorAimTargetingSystem.cs` | SystemBase | **NEW** — screen-to-world raycast when `TargetData.Mode == CursorAim` |
| `Assets/Scripts/Targeting/Systems/TargetingModeDispatcherSystem.cs` | SystemBase | **NEW** — applies `ParadigmSettings.ActiveTargetingMode` to `TargetData.Mode`, manages system enable/disable |

**Verification:**
- In ARPG mode: move cursor over terrain → `TargetData.TargetPoint` updates in real-time
- Fire weapon → projectile/hitscan goes toward cursor position, not screen center
- In Shooter mode: cursor-aim system is inactive (no per-frame raycast cost)

### Phase 3: Player Ability Components + Database (5 files)

**Goal:** Define the data model for player combat abilities.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Combat/Abilities/Components/PlayerAbilitySlot.cs` | Buffer | **NEW** — per-slot cooldown/charge state |
| `Assets/Scripts/Combat/Abilities/Components/PlayerAbilityState.cs` | Component | **NEW** — active ability phase/timing |
| `Assets/Scripts/Combat/Abilities/Data/AbilityDef.cs` | Struct | **NEW** — blob-baked ability definition |
| `Assets/Scripts/Combat/Abilities/Data/AbilityDatabase.cs` | BlobAsset | **NEW** — singleton ability lookup |
| `Assets/Scripts/Combat/Abilities/Systems/AbilityDatabaseBootstrapSystem.cs` | SystemBase | **NEW** — loads database from Resources |

**Verification:**
- Enter play mode → `AbilityDatabaseRef` singleton exists with loaded abilities
- Player entity has `PlayerAbilitySlot` buffer and `PlayerAbilityState` component
- No archetype size regression (verify < 16KB with ability components added)

### Phase 4: Ability Execution Pipeline (5 files)

**Goal:** Full ability lifecycle — input → validation → cast phases → effect delivery.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityInputSystem.cs` | SystemBase | **NEW** — reads input, writes QueuedSlotIndex |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityCooldownSystem.cs` | ISystem (Burst) | **NEW** — ticks cooldowns + GCD |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityValidationSystem.cs` | ISystem (Burst) | **NEW** — validates cost/cooldown/range/target |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityExecutionSystem.cs` | ISystem (Burst) | **NEW** — phase state machine |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityCostSystem.cs` | ISystem (Burst) | **NEW** — resource deduction |

**Verification:**
- Press ability key → ability cast bar appears → phases progress
- Insufficient mana → ability rejected with UI feedback
- On cooldown → ability rejected
- During GCD → input queued, fires when GCD expires
- Cast interrupted (by stun) → ability cancelled, partial cooldown applied

### Phase 5: Ability Effect Delivery (2 files)

**Goal:** Abilities deal damage and spawn VFX through existing pipelines.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityEffectSystem.cs` | SystemBase | **NEW** — creates PendingCombatHit + VFXRequest |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilitySystemGroup.cs` | ComponentSystemGroup | **NEW** — system group ordering |

**Verification:**
- SingleTarget ability on locked target → damage applied, damage number shown
- GroundTarget ability at cursor → AoE damage to enemies in radius
- Cone ability → hits enemies in front arc
- Line/Projectile ability → skillshot fires in aim direction
- Impact VFX spawns at hit positions

### Phase 6: Ability Authoring & Tooling (4 files)

**Goal:** Designer-facing ScriptableObject authoring and editor tooling.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityDefinitionSO.cs` | SO | **NEW** — per-ability config with inspector |
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityLoadoutSO.cs` | SO | **NEW** — ability bar loadout (references abilities) |
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityLoadoutAuthoring.cs` | Baker | **NEW** — bakes loadout → PlayerAbilitySlot buffer + AbilityDatabaseBlob |
| `Assets/Editor/AbilityWorkstation/AbilityWorkstationWindow.cs` | EditorWindow | **NEW** — visual ability editor with live preview |

**AbilityWorkstationWindow features:**
- Tree view of all `AbilityDefinitionSO` assets in project
- Inline property editor with color-coded timing visualization
- Timeline preview: Telegraph → Cast → Active → Recovery with draggable phase durations
- Paradigm compatibility matrix checkbox grid
- "Test in Play Mode" button: assigns ability to slot 1 and enters play mode
- Batch operations: find all abilities using a specific VFX, bulk-adjust cooldowns
- Validation warnings: missing VFX references, zero damage on Attack-category abilities, cooldown < GCD

### Phase 7: Ability UI Bridge (3 files)

**Goal:** Cast bars, cooldown sweeps, ability failure feedback in the HUD.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Combat/Abilities/UI/AbilityUIBridgeSystem.cs` | SystemBase | **NEW** — ECS → managed UI bridge |
| `Assets/Scripts/Combat/Abilities/UI/IAbilityUIProvider.cs` | Interface | **NEW** — adapter pattern for UI framework |
| `Assets/Scripts/Combat/Abilities/UI/AbilityBarAdapter.cs` | MonoBehaviour | **NEW** — concrete UI implementation |

Uses existing `CombatUIRegistry` singleton for registration.

### Phase 8: Per-Paradigm Ability Targeting Resolution (6 files modified)

**Goal:** Fill targeting data gaps so abilities and weapons fire correctly across all paradigms without regressions.

**Problem:** Phases 1-7 built the ability system, but targeting data population varies by mode:
- **CursorAim** (ARPG/MOBA/TwinStick): Writes `TargetPoint` + `AimDirection` but never populates `TargetEntity`. SingleTarget and Projectile abilities silently fail.
- **ClickSelect** (MMO): Populates `TargetEntity` but not `AimDirection` toward target. Cone/Line/Cleave abilities fire in stale/default direction.
- **Shooter/AutoTarget/LockOn**: Already work correctly via MonoBehaviour implementations. Must not regress.
- **All CursorAim modes (weapons)**: `WeaponFireSystem` hitscan runs server-only. The server never runs `CursorAimTargetingSystem` (client-only), so `TargetData.AimDirection` stays at default `(0,0,1)` = north on the server. All hitscan weapons fire north regardless of cursor position.

| File | Type | Change |
|------|------|--------|
| `Assets/Scripts/Targeting/Systems/CursorAimTargetingSystem.cs` | SystemBase | Modified — soft-target `LockOnTarget` entity query near cursor hit point (3m radius) |
| `Assets/Scripts/Targeting/Systems/TargetingModeDispatcherSystem.cs` | SystemBase | Modified — per-frame mode enforcement + `AimDirection` toward `TargetEntity` for ClickSelect |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityEffectSystem.cs` | SystemBase | Modified — projectile skillshot fallback when no `TargetEntity` (fires toward `AimDirection`) |
| `Assets/Scripts/Player/Systems/PlayerFacingSystem.cs` | ISystem | Modified — CursorAim skip guard + server-side replicated aim direction write |
| `Assets/Scripts/Shared/Player/PlayerInput_Global.cs` | IInputComponentData | Modified — added `CursorAimDirection` + `CursorAimValid` for client→server replication |
| `Assets/Scripts/Player/Systems/PlayerInputSystem.cs` | SystemBase | Modified — copies `TargetData.AimDirection` into `PlayerInput.CursorAimDirection` when in CursorAim mode |

**Change A — CursorAim Soft-Target Entity Resolution:**
After computing `TargetPoint` from cursor raycast, queries all entities with `LockOnTarget` + `LocalToWorld` (same pattern as `TwinStickAimSystem`). Picks nearest within `SoftTargetRadius` (3m) on XZ plane. Writes `TargetData.TargetEntity` and `HasValidTarget`. Guarded by `Mode == CursorAim` — other modes untouched.

**Change B — ClickSelect AimDirection Resolution:**
Per-frame block (not just on mode change) reads `TargetEntity`'s position via `LocalToWorld`, computes `AimDirection = normalize(targetPos - playerPos)` on XZ plane. Guarded by `_cachedMode == ClickSelect` + valid target.

**Change C — Projectile Skillshot Fallback:**
Projectile case now fires as skillshot toward `AimDirection` when `TargetEntity == Entity.Null`, instead of silently failing. Targeted projectile (entity lock-on) still takes priority when target exists.

**Change D — Per-Frame Mode Enforcement (TargetingModeDispatcherSystem):**
`TargetData.Mode` is now written every frame (not just on paradigm change). Previously the dispatcher cached `_cachedMode` and only wrote on change — but the player ghost entity spawns AFTER the paradigm switches, so the write found zero entities and `TargetData.Mode` stayed at default `CameraRaycast(0)` forever. Now checks `targetData.Mode != expectedMode` per-frame and corrects.

**Change E — CursorAim AimDirection Overwrite Prevention (PlayerFacingSystem):**
`PlayerFacingSystem` (PredictedFixedStepSimulationSystemGroup) was overwriting `TargetData.AimDirection` with `math.forward(transform.Rotation)` for `CursorDirection`/`MovementDirection` facing modes. This stomped the cursor-computed AimDirection from `CursorAimTargetingSystem` (SimulationSystemGroup, runs earlier). Added skip guard: `if (targetData.Mode == CursorAim) continue`.

**Change F — Server-Side CursorAim Replication (PlayerInput + PlayerFacingSystem):**
`WeaponFireSystem` hitscan fires server-only (`isServer` guard). The server has no camera, no cursor, and `CursorAimTargetingSystem` is client-only — so `TargetData.AimDirection` stays at default north. Fix: added `float3 CursorAimDirection` + `byte CursorAimValid` to `PlayerInput` (`IInputComponentData`, auto-replicated client→server). `PlayerInputSystem` copies the client's cursor-computed AimDirection into these fields. `PlayerFacingSystem` writes it to `TargetData.AimDirection` on the server (guarded by `Mode != CursorAim` to avoid overwriting the client's fresh value). Same pattern as `CameraYaw`/`CameraPitch` replication for Shooter mode.

**Per-mode ability coverage after Phase 8:**

| Ability Type | Shooter | MMO (ClickSelect) | ARPG/MOBA/TwinStick (CursorAim) |
|---|---|---|---|
| SingleTarget | MonoBehaviour TargetEntity | Click TargetEntity | Soft-target nearest to cursor |
| GroundTarget | Works | Works | Works (TargetPoint from cursor) |
| Cone/Line/Cleave | Works (AimDir from camera) | AimDir toward selected enemy | Works (AimDir toward cursor) |
| AoE (self) | Works | Works | Works |
| Projectile | Works | Works | Skillshot toward cursor / soft-target |
| Self | Works | Works | Works |

---

## File Manifest

| File | Type | Phase | Status |
|------|------|-------|--------|
| `Assets/Scripts/Core/Input/Paradigm/InputParadigmProfile.cs` | SO | 1 | Modified |
| `Assets/Scripts/Core/Input/Paradigm/Components/ParadigmSettings.cs` | Singleton | 1 | Modified |
| `Assets/Scripts/Core/Input/Paradigm/Subsystems/TargetingConfigurable.cs` | MonoBehaviour | 1 | **NEW** |
| `Assets/Scripts/Core/Input/Paradigm/Subsystems/LockBehaviorConfigurable.cs` | MonoBehaviour | 1 | **NEW** |
| `Assets/Scripts/Targeting/Systems/TargetingModeDispatcherSystem.cs` | SystemBase | 2 | **NEW** |
| `Assets/Scripts/Targeting/Systems/CursorAimTargetingSystem.cs` | SystemBase | 2 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Components/PlayerAbilitySlot.cs` | Buffer | 3 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Components/PlayerAbilityState.cs` | Component | 3 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Data/AbilityDef.cs` | Struct | 3 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Data/AbilityDatabase.cs` | BlobAsset | 3 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/AbilityDatabaseBootstrapSystem.cs` | SystemBase | 3 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilitySystemGroup.cs` | Group | 4 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityInputSystem.cs` | SystemBase | 4 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityCooldownSystem.cs` | ISystem (Burst) | 4 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityValidationSystem.cs` | ISystem (Burst) | 4 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityExecutionSystem.cs` | ISystem (Burst) | 4 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityCostSystem.cs` | ISystem (Burst) | 4 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityEffectSystem.cs` | SystemBase | 5 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityDefinitionSO.cs` | SO | 6 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityLoadoutSO.cs` | SO | 6 | **NEW** |
| `Assets/Scripts/Combat/Abilities/Authoring/AbilityLoadoutAuthoring.cs` | Baker | 6 | **NEW** |
| `Assets/Editor/AbilityWorkstation/AbilityWorkstationWindow.cs` | EditorWindow | 6 | **NEW** |
| `Assets/Scripts/Combat/Abilities/UI/AbilityUIBridgeSystem.cs` | SystemBase | 7 | **NEW** |
| `Assets/Scripts/Combat/Abilities/UI/IAbilityUIProvider.cs` | Interface | 7 | **NEW** |
| `Assets/Scripts/Combat/Abilities/UI/AbilityBarAdapter.cs` | MonoBehaviour | 7 | **NEW** |
| `Assets/Data/Input/Profiles/*.asset` | Assets | 1 | Modified (7 profiles) |
| `Assets/Scripts/Targeting/Systems/CursorAimTargetingSystem.cs` | SystemBase | 8 | Modified (soft-target query) |
| `Assets/Scripts/Targeting/Systems/TargetingModeDispatcherSystem.cs` | SystemBase | 8 | Modified (per-frame mode enforcement + ClickSelect AimDirection) |
| `Assets/Scripts/Combat/Abilities/Systems/PlayerAbilityEffectSystem.cs` | SystemBase | 8 | Modified (projectile skillshot) |
| `Assets/Scripts/Player/Systems/PlayerFacingSystem.cs` | ISystem | 8 | Modified (CursorAim skip guard + server aim replication) |
| `Assets/Scripts/Shared/Player/PlayerInput_Global.cs` | IInputComponentData | 8 | Modified (CursorAimDirection + CursorAimValid) |
| `Assets/Scripts/Player/Systems/PlayerInputSystem.cs` | SystemBase | 8 | Modified (cursor aim → PlayerInput copy) |

---

## Design Decisions

| Decision | Chosen | Alternative | Rationale |
|----------|--------|-------------|-----------|
| Paradigm–targeting bridge | `IParadigmConfigurable` plugin | Singleton observer | Plugin system already exists with ordered config + rollback; zero new infrastructure |
| Ability data storage | BlobAsset + buffer per-entity | SO references at runtime | BlobAsset is Burst-readable, zero GC, fits prediction model; SO requires managed access |
| Ability state replication | `AllPredicted` ghost fields | Server-authoritative only | Client needs to predict cast phases for responsive UI (cast bars, input queueing) |
| Cooldown system | Per-slot buffer on player entity | Separate singleton | Per-entity allows different loadouts per player; buffer stays within 16KB (6 slots × 24 bytes) |
| Phase lifecycle | 5-phase (Idle→Telegraph→Cast→Active→Recovery) | 3-phase (Idle→Cast→Active) | Matches AI system; Telegraph enables visual tells for PvP fairness; Recovery prevents ability spam |
| Cursor-aim implementation | ECS SystemBase (managed) | MonoBehaviour (existing pattern) | Centralized system with paradigm-gated enable/disable; MonoBehaviour runs always (wasted cycles) |
| Effect delivery | Reuse `PendingCombatHit` pipeline | New ability-specific damage path | All damage should flow through one resolution pipeline (CombatResolutionSystem) for consistent modifiers/stats |
| GCD model | Per-ability GCD contribution | Fixed global GCD | Some abilities should be off-GCD (movement abilities, defensives); per-ability field allows designer control |
| Paradigm target-type override | Per-ability SO field | Runtime resolver | Designer explicitly marks "this ability targets differently in Shooter mode" — no magic |
| Ability input mapping | Reuse paradigm combat action maps | New ability-specific action map | Combat maps (`Combat_Shooter`, `Combat_MMO`, etc.) already swap per paradigm; abilities map to existing actions |
| Editor tooling | Dedicated `AbilityWorkstationWindow` | Inspector-only | Follows established pattern (VFXWorkstation, DialogueWorkstation, ProgressionWorkstation); timeline preview requires custom editor |
| Ability archetype budget | `InternalBufferCapacity(6)` for slots | External buffer (capacity 0) | 6 slots × 24 bytes = 144 bytes inline — well within 16KB budget; external would add chunk fragmentation |

---

## Performance Considerations

### Hot Path (Every Predicted Tick)

All systems in `PlayerAbilitySystemGroup` run per predicted tick (potentially 4× per frame at 30Hz with MaxSteps=4):

| System | Burst? | Allocation? | Per-Entity Work |
|--------|--------|-------------|-----------------|
| `PlayerAbilityInputSystem` | Yes | None | Read 1 byte input, write 1 byte |
| `PlayerAbilityCooldownSystem` | Yes | None | 6 float decrements + 1 GCD decrement |
| `PlayerAbilityValidationSystem` | Yes | None | 1 blob lookup + 5 conditional checks |
| `PlayerAbilityExecutionSystem` | Yes | None | 1 phase transition + 1 float increment |
| `PlayerAbilityCostSystem` | Yes | None | 1 resource lookup + 1 float subtract |
| `PlayerAbilityEffectSystem` | No (ECB entity creation) | ECB only | 0-N PendingCombatHit creates per hit timing |

**Total per-tick cost:** ~6 systems × 1 entity (local player) = negligible. The entire pipeline processes exactly 1 entity per client. Server processes N players but each is independent — no cross-entity dependencies, no sync points.

### CursorAimTargetingSystem

One `Physics.CastRay` per frame (not per predicted tick — runs in `SimulationSystemGroup`, client-only). Same cost as the existing `CameraRaycastTargeting` MonoBehaviour it replaces. Phase 8 adds a soft-target `LockOnTarget` entity query — linear scan over targetable entities within 3m of cursor hit point. Typical scene has <100 LockOnTarget entities; cost is negligible compared to the physics raycast.

### CursorAim Server Replication

`PlayerInput.CursorAimDirection` (float3, 12 bytes) + `CursorAimValid` (byte, 1 byte) added to `IInputComponentData`. Replicated every tick via NetCode's input replication (same mechanism as `CameraYaw`/`CameraPitch`). Bandwidth: 13 bytes/tick — negligible in the context of the full PlayerInput struct (~200+ bytes). Only populated when `TargetData.Mode == CursorAim` (ARPG/MOBA/TwinStick); zero for all other paradigms.

### Blob Asset Lookup

`AbilityDef` lookup from `AbilityDatabaseRef` is a single indexed array access — O(1), cache-friendly, zero GC.

### AoE Target Resolution

`PlayerAbilityEffectSystem` performs `OverlapSphere` or `OverlapAabb` queries for AoE abilities. These are bounded by `Radius` (typically 3-8m) and run on Server|Local only (not client-predicted). Same physics query pattern as `MeleeActionSystem` hitbox detection.

---

## 16KB Archetype Budget Analysis

Current player entity footprint (from MEMORY.md): ~60+ components + 11 buffers ≈ near 16,320 byte limit.

New components added:
| Component | Size (bytes) | Type |
|-----------|-------------|------|
| `PlayerAbilityState` | 16 | IComponentData |
| `PlayerAbilitySlot` | 144 inline (6 × 24) | IBufferElementData |
| **Total** | **160** | |

**Mitigation:** The EPIC 15.25 buffer reductions freed ~200 bytes (CollisionEvent 8→2 capacity, StatusEffectRequest 8→4, ReviveRequest 8→2). 160 bytes fits within that headroom. If tight, reduce `InternalBufferCapacity` to 4 (96 bytes) — 4 ability slots is sufficient for ARPG/MOBA layouts.

**Validation step:** Before implementation, run `EntityManager.GetChunkComponentTypes()` on the player archetype and sum sizes. If > 15,800 bytes after adding ability components, use the TargetingModule child entity pattern (store `PlayerAbilitySlot` buffer on a child entity linked via `AbilityModuleLink`).

---

## Modularity & Swappability

### Plugin Architecture

Each layer is independently replaceable:

1. **Targeting layer:** Replace `CursorAimTargetingSystem` with a custom implementation — as long as it writes `TargetData.AimDirection` and `TargetData.TargetPoint`, the rest of the pipeline works.

2. **Ability layer:** The `AbilityDef` blob asset is a pure data definition. Swap `PlayerAbilityExecutionSystem` for a different lifecycle model (e.g., charge-up instead of phases) without touching input, validation, or effect delivery.

3. **Effect layer:** `PlayerAbilityEffectSystem` creates `PendingCombatHit` entities — any system that creates these entities integrates automatically. A custom melee-ability hybrid that uses hitbox timing instead of PendingCombatHit can be swapped in.

4. **UI layer:** `IAbilityUIProvider` interface means the ability bar can be replaced (React-style, IMGUI, UIToolkit) without touching ECS systems.

5. **Paradigm bridge:** `IParadigmConfigurable` plugins can be added/removed without modifying `ParadigmStateMachine`. Adding a new paradigm (e.g., `SideScroller2D`) requires only a new `InputParadigmProfile` asset with appropriate defaults — no code changes.

### Extension Points

| Extension | How |
|-----------|-----|
| New targeting mode | Add enum value to `TargetingMode`, create system, add to `TargetingModeDispatcherSystem` switch |
| New lock behavior | Add enum value to `LockBehaviorType`, create factory method in `ActiveLockBehavior`, create per-mode system |
| New ability target type | Add enum value to `AbilityTargetType`, add case in `PlayerAbilityEffectSystem` |
| New paradigm | Create `InputParadigmProfile` asset, set defaults — bridge systems read from profile |
| New ability category | Add enum value to `AbilityCategory`, optionally add category-specific validation in `PlayerAbilityValidationSystem` |
| Boss-specific lock params | Add `LockBehaviorOverride` IComponentData to boss entity — lock systems check per-entity before singleton |

---

## Verification

### Phase 1: Paradigm–Targeting Bridge
1. Switch to each paradigm via `ParadigmDemoUI` → verify `TargetData.Mode` matches expected targeting mode
2. Switch Shooter → ARPG → Shooter → verify no stale cursor-aim state
3. Switch to MMO → LMB click enemy → `TargetData.TargetEntity` is set (ClickSelect mode active)
4. Switch to Shooter → `TargetData.TargetEntity` cleared (CameraRaycast doesn't need TargetEntity)
5. `ActiveLockBehavior.BehaviorType` matches profile's `defaultLockBehavior` after each switch

### Phase 2: Cursor-Aim
6. In ARPG mode: move cursor over terrain → `TargetData.TargetPoint` tracks cursor world position
7. Fire ranged weapon → projectile/hitscan aims at cursor position (not screen center)
8. In Shooter mode: `CursorAimTargetingSystem` does NOT run (verified via system profiler)
9. Cursor at screen edge → ground-plane fallback produces valid aim direction (no NaN)

### Phase 3: Ability Data
10. Enter play mode → `AbilityDatabaseRef` singleton exists with correct ability count
11. Player entity has `PlayerAbilitySlot` buffer with 6 entries
12. Player entity archetype size < 16KB (verified via `EntityManager` inspection)
13. Ability blob asset survives hot-reload (domain reload in editor)

### Phase 4: Ability Execution
14. Press ability key → `Phase` transitions: Idle → Telegraph → Casting → Active → Recovery → Idle
15. Press ability during GCD → queued → fires when GCD expires
16. Press ability with insufficient mana → rejected with UI feedback
17. Press ability on cooldown → rejected
18. Interrupt during Casting phase → ability cancelled, partial cooldown
19. `CastMovement == Rooted` → player cannot move during Casting phase
20. Prediction: ability phases appear responsive on client, server validates authoritatively

### Phase 5: Effect Delivery
21. SingleTarget ability on locked target → `PendingCombatHit` created → damage resolves
22. AoE ability at cursor → all enemies within `Radius` take damage
23. Cone ability → only enemies in front arc take damage
24. Multi-hit ability → correct number of hits at correct intervals
25. Ability modifiers flow through `CombatResolutionSystem` (e.g., on-hit slow)

### Phase 6: Tooling
26. Open `AbilityWorkstationWindow` → all `AbilityDefinitionSO` assets listed
27. Modify ability timing → save → blob regenerated on next play mode
28. Timeline preview shows correct phase durations
29. Paradigm compatibility matrix flags correctly filter abilities per paradigm

### Phase 7: UI
30. Cooldown sweep animation on ability slot icons
31. GCD overlay on all ability slots simultaneously
32. Cast bar shows during Casting phase with progress
33. "Out of range" / "Not enough mana" text feedback on ability rejection
34. Ability bar layout changes when switching paradigm (hotbar → QWER bar)

### Phase 8: Per-Paradigm Ability Targeting
35. ARPG mode: hover cursor near enemy → SingleTarget ability hits that enemy (soft-target)
36. ARPG mode: hover cursor on empty ground → Projectile ability fires toward cursor (skillshot)
37. ARPG mode: GroundTarget/Cone/AoE abilities still work as before (no regression)
38. MMO mode: select enemy → Cone ability fires toward selected enemy (AimDirection resolved)
39. Shooter mode: all abilities work exactly as before (no regression — MonoBehaviour path untouched)
40. AutoTarget/LockOn modes: all abilities work exactly as before (no regression)
41. TwinStick/ARPG mode: weapon hitscan fires toward cursor (not north) — server receives replicated CursorAimDirection
42. TwinStick/ARPG mode: `[TARGETING] CursorAimSystem` debug logs show aimDir tracking cursor movement
43. TwinStick/ARPG mode: `[TARGETING] PlayerFacingSystem | SKIP AimDir overwrite` confirms no AimDirection stomping
44. TwinStick/ARPG mode: `[TARGETING] ModeDispatcher | Enforced Mode=CursorAim` confirms late-spawn player gets correct mode
45. Shooter mode: CursorAimValid=0 in PlayerInput — no interference with CameraYaw/CameraPitch aim path

---

## What This Does NOT Cover (Future Work)

### Requires Separate EPICs
- **Talent/skill trees** — ability unlock progression (builds on EPIC 16.14 Progression)
- **Ability combos** — chaining specific abilities for bonus effects
- **PvP ability balance** — separate damage/cooldown tables for PvP contexts
- **Ability crafting/modification** — socket system for ability augmentation
- **Pet/summon abilities** — persistent entities with their own ability sets
- **Vehicle abilities** — mounted combat with different ability loadouts

### Quality Improvements (Post-MVP)
- **Ability preview indicators** — ground circles, cone visualizations during targeting phase
- **Smartcast / quickcast toggle** — MOBA-style instant-fire vs aim-then-fire per ability
- **Ability macro system** — chain multiple abilities with one key press
- **Server-side cooldown validation** — `CooldownValidationSystem` (existing placeholder) for anti-cheat
- **Ability animation blending** — upper-body ability animations while running (animation layers)
- **Per-enemy lock parameter overrides** — `LockBehaviorOverride` component for bosses

---

## References

- EPIC 15.20: Input Paradigm Framework (ParadigmStateMachine, IParadigmConfigurable)
- EPIC 15.21: Input Action Layer (ParadigmInputManager, combat action maps)
- EPIC 15.18: Cursor Hover & Click-to-Select
- EPIC 18.15: Click-to-Move & WASD Gating
- EPIC 18.18: Targeting & Attack Coverage (per-paradigm audit)
- EPIC 16.7: Unified VFX Event Pipeline (VFXRequest pattern)
- EPIC 16.8: Player Resource Framework (ResourceType, PlayerResource)
- EPIC 16.14: Progression & XP (ability unlocks)
- `Assets/Scripts/Targeting/` — targeting implementations
- `Assets/Scripts/Targeting/Core/` — lock behavior types and ActiveLockBehavior
- `Assets/Scripts/Core/Input/Paradigm/` — paradigm system
- `Assets/Scripts/AI/Components/AbilityDefinition.cs` — reference ability architecture
- `Assets/Scripts/AI/Systems/` — reference ability execution systems
- `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` — damage resolution pipeline
- `Assets/Scripts/Weapons/Systems/WeaponFireSystem.cs` — weapon fire integration
- `Assets/Scripts/Player/Abilities/` — existing locomotion ability scaffolding
