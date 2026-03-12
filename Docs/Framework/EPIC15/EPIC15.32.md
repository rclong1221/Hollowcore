# EPIC 15.32: Enemy Ability & Encounter Framework

**Status:** Planned
**Last Updated:** February 13, 2026
**Priority:** Critical (Combat Core)
**Dependencies:**
- EPIC 15.31 (Enemy AI Brain — Vertical Slice — complete)
- EPIC 15.28 (Unified Combat Resolution Pipeline — complete)
- EPIC 15.29 (Weapon Modifiers & On-Hit Effects — complete)
- EPIC 15.30 (Damage Pipeline Visual Unification — complete)

**Feature:** Data-driven ability system that replaces hardcoded melee attacks with composable ability definitions, telegraph/AOE zones, boss phases, encounter scripting, and a visual editor tool. Individual abilities are **standalone ScriptableObjects** shared across enemy types. Encounter logic supports **conditional triggers** beyond HP thresholds — timers, add deaths, position checks, player count. Abilities apply **status effects** through the existing `WeaponModifier` → `StatusEffectRequest` pipeline. A **standalone Encounter Designer Editor Window** lets designers visually author boss fights without code. All damage flows through the existing `PendingCombatHit` → `CombatResolutionSystem` pipeline unchanged.

---

## Problem Statement

EPIC 15.31 proved the AI brain architecture works end-to-end: BoxingJoe patrols, chases, attacks, deals damage, and returns home. But the attack system is **hardcoded to a single melee swing**:

```
BUILT (15.31)                              MISSING
──────────────                             ───────
HFSM State Machine ✓                       Multiple abilities per enemy
Single melee attack (AIAttackState) ✓      Ability selection / rotation
Chase + approach ✓                         Ranged attacks, projectiles
Fixed attack timing ✓                      Cast times, channels, combos
No visual telegraph                        Ground AOE indicators
No boss phases                             HP-threshold phase transitions
No add spawning                            Encounter orchestration
No animation bridge                        Visual feedback pipeline
No status effects from attacks             DOTs, debuffs, slows, stuns
No reusable abilities                      Shareable ability SOs
No designer tooling                        Visual encounter editor
```

**AIAttackExecutionSystem** (EPIC 15.31) creates `PendingCombatHit` with hardcoded values:
- `ResolverType = Hybrid` (always)
- `WeaponData` built from `AIBrain.BaseDamage` (single source)
- `HitRegion = Torso`, `HitboxMultiplier = 1.0` (always)
- Distance check: `MeleeRange × 1.3` (always melee)
- No telegraph, no cast time variation, no ability-specific behavior
- No status effects (no WeaponModifier integration)

Every future enemy type — ranged casters, AOE bosses, summoners — needs a different attack pattern. Without a framework, each becomes a one-off system.

---

## Architecture Decision

### Why Data-Driven Abilities (not code-per-enemy)

| Approach | Pros | Cons |
|----------|------|------|
| **Code per enemy** | Simple, explicit | N systems for N enemy types, no reuse |
| **Behavior tree** | Flexible, visual | Managed/OOP, cache-unfriendly, overkill for ability execution |
| **Data-driven abilities** | One system handles all, designer-tunable, Burst-compatible | Larger struct, more upfront design |

**Decision:** Abilities are **standalone ScriptableObjects** (`AbilityDefinitionSO`) that can be shared across any number of enemy types. An `AbilityProfileSO` references a list of these shared abilities to form an enemy's rotation. A single execution system handles all ability types by branching on data fields. This matches the project's existing patterns:
- `WeaponModifier` (IBufferElementData, InternalBufferCapacity(4)) — per-weapon effect list
- `WeaponConfig` (ScriptableObject) → baked to components — designer-facing authoring
- `CombatResolverType` enum → resolver factory — behavior branching on data

### Why Individual AbilityDefinitionSO (not inline entries)

| Approach | Pros | Cons |
|----------|------|------|
| **Inline ability data in profile SO** | Single asset per enemy, simple | Duplicate data across enemies, change 1 ability = edit N profiles |
| **Individual ability SOs** | Define once, reference everywhere. Balance passes update one asset. Library browsable in Project window | Extra asset management, reference tracking |

**Decision:** Each ability is its own `AbilityDefinitionSO` asset. An `AbilityProfileSO` holds an ordered list of references to these assets. Benefits:
- "Fireball" defined once, used by Fire Mage, Dragon Boss, and Fire Elemental
- Balance change to Fireball's damage updates all enemies simultaneously
- Encounter Designer can show an ability library with drag-and-drop
- Abilities are browsable, searchable, and filterable in the Unity Project window

### Why Telegraphs Are Spawned Entities (not components on the caster)

Telegraphs must persist independently of the caster (boss dies mid-cast → ground fire persists). They need their own transform, collider, visual. Spawned entities are the only clean solution in ECS. The pattern matches `PendingCombatHit` (spawned entity, consumed by system, destroyed after processing).

### Why Phases Are Buffers (not state machine extensions)

The HFSM from 15.31 handles behavioral states (Idle/Combat/ReturnHome). Boss phases are **combat sub-modes** — they modify which abilities are available and how the boss behaves within Combat state. Adding Phase1/Phase2/Phase3 to `AIBehaviorState` would bloat the state machine. Instead, phases are a **parallel data layer** read by the ability selection system.

### Why Encounter Triggers (not just HP thresholds)

AAA bosses need triggers beyond "HP dropped below X%":
- **Timer:** Enrage after 8 minutes regardless of HP
- **Add death:** "When all 4 totems are destroyed, boss becomes vulnerable"
- **Position:** "When boss reaches center of arena, transition phase"
- **Player count:** "When 3+ players are in melee range, do cleave"
- **Ability count:** "After 3 casts of Fireball, force Meteor"
- **Composite:** "HP below 30% AND timer > 5m → hard enrage"

A **Trigger → Action** system handles all of these with a single evaluation pipeline.

### Why Status Effects From Abilities (reusing existing pipeline)

The project already has a complete status effect pipeline (EPIC 15.29):
```
WeaponModifier → CombatResolutionSystem → StatusEffectRequest → StatusEffectSystem → DamageEvent
```
Abilities carry modifier data identical to weapon modifiers. When `PendingCombatHit` is created, the ability's modifiers are attached. `CombatResolutionSystem` processes them through the existing path — no new systems needed for effect application.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        ENCOUNTER LAYER                                   │
│  EncounterTriggerSystem → PhaseTransitionSystem → AddSpawnSystem         │
│  (Condition→Action)       (HP + triggers)         (Spawn groups)         │
├───────────────────────────┬──────────────────────────────────────────────┤
│     ABILITY LAYER         │        TELEGRAPH LAYER                       │
│                           │                                              │
│  AbilitySelectionSystem   │  TelegraphSpawnHelper (from abilities)       │
│  (Pick next ability)      │  TelegraphVisualBridge (ground decal)        │
│         ↓                 │  TelegraphDamageSystem (on expire)           │
│  AbilityExecutionSystem   │         ↓                                    │
│  (Cast lifecycle)         │  PendingCombatHit (AOE targets)              │
│         ↓                 │         ↓                                    │
│  PendingCombatHit ────────┴─→ CombatResolutionSystem (EXISTING)         │
│  + AbilityModifiers ─────────→ StatusEffectRequest (EXISTING pipeline)  │
│                                       ↓                                  │
│                           CombatResultEvent → DamageApplication          │
├──────────────────────────────────────────────────────────────────────────┤
│                    EXISTING FOUNDATION (15.31)                           │
│  HFSM · Aggro/Threat · Health · DamageVisualQueue · HealthBars          │
├──────────────────────────────────────────────────────────────────────────┤
│                       DESIGNER TOOLING                                   │
│  Encounter Designer Editor Window                                        │
│  (Ability library · Phase timeline · Trigger editor · Telegraph preview) │
└──────────────────────────────────────────────────────────────────────────┘
```

### Data Flow (One Frame)

```
1. EncounterTriggerSystem evaluates all triggers (HP, timer, add-death, position, etc.)
   → Fires matching actions (phase transition, force ability, spawn adds, set invuln)
2. PhaseTransitionSystem applies phase changes (from triggers or direct HP checks)
   → Updates CurrentPhase, applies modifiers, handles invulnerability
3. AbilitySelectionSystem reads:
   - AbilityDefinition buffer (filtered by CurrentPhase, cooldown, range, cooldown group)
   - AIBrain.Archetype → selection mode (Priority vs Utility)
   - Target distance, HP%, threat data
   → Writes: AbilityExecutionState.SelectedAbilityIndex
4. AbilityExecutionSystem advances cast lifecycle:
   - Telegraph → Casting → Active → Recovery
   - At Active: creates PendingCombatHit (single target) with ability modifiers
              OR spawns TelegraphZone entity (AOE, delayed damage)
5. TelegraphDamageSystem (later frame): on timer expiry,
   spatial query for targets → creates PendingCombatHit per target
6. CombatResolutionSystem resolves all PendingCombatHit → CombatResultEvent
   → Processes ability modifiers → StatusEffectRequest (DOT, slow, stun, etc.)
7. DamageApplicationSystem applies health changes
8. StatusEffectSystem ticks active effects → DamageEvent for DOTs
9. CombatUIBridgeSystem shows damage numbers + status text ("BURNING!", "STUNNED!")
```

---

## Phase 0: Foundation Refactor

**Goal:** Replace hardcoded `AIAttackState` / `AIAttackExecutionSystem` with generic ability execution that supports the same BoxingJoe melee attack but through the new data path. Zero behavior change — pure refactor.

### Task 0.1: AbilityExecutionState Component

**File:** `Assets/Scripts/AI/Components/AbilityExecutionState.cs` (NEW)

Replaces `AIAttackState`. Tracks which ability is being cast and its lifecycle phase.

```csharp
public enum AbilityCastPhase : byte
{
    Idle = 0,        // No ability active, ready for selection
    Telegraph = 1,   // Ground indicator visible, warning players
    Casting = 2,     // Wind-up / cast bar (interruptible window)
    Active = 3,      // Damage delivery / effect application
    Recovery = 4     // Post-attack cooldown animation
}
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Phase | AbilityCastPhase | Idle | Current lifecycle phase |
| PhaseTimer | float | 0 | Elapsed time in current phase |
| SelectedAbilityIndex | int | -1 | Index into AbilityDefinition buffer (-1 = none) |
| TargetEntity | Entity | Null | Locked target for this cast |
| TargetPosition | float3 | zero | Locked ground position (for AOE) |
| CastDirection | float3 | zero | Locked aim direction |
| DamageDealt | bool | false | One-shot flag (single-target abilities) |
| TicksDelivered | int | 0 | For channeled abilities |
| TelegraphEntity | Entity | Null | Spawned telegraph (if any) |

### Task 0.2: AbilityCooldownState Buffer

**File:** `Assets/Scripts/AI/Components/AbilityCooldownState.cs` (NEW)

Per-ability runtime state. One entry per ability in the definition buffer.

| Field | Type | Purpose |
|-------|------|---------|
| CooldownRemaining | float | Time until ability is available |
| GlobalCooldownRemaining | float | Shared cooldown across all abilities |
| CooldownGroupRemaining | float | Shared cooldown within a group (see Phase 1) |
| ChargesRemaining | int | For charge-based abilities |
| MaxCharges | int | Charge cap (baked from definition) |
| ChargeRegenTimer | float | Time until next charge regenerates |

```csharp
[InternalBufferCapacity(4)]
public struct AbilityCooldownState : IBufferElementData
```

### Task 0.3: Refactor AIAttackExecutionSystem → AbilityExecutionSystem

**File:** `Assets/Scripts/AI/Systems/AbilityExecutionSystem.cs` (NEW, replaces AIAttackExecutionSystem)

Same lifecycle logic (WindUp → Active → Recovery) but reads timing from the selected ability definition rather than hardcoded `AIBrain` fields. For Phase 0, the "ability definition" is still constructed from `AIBrain` fields — the buffer doesn't exist yet.

**Reads:** AbilityExecutionState, AIState, AIBrain, LocalTransform
**Writes:** AbilityExecutionState, creates PendingCombatHit via ECB

Guard: only runs when `AIState.CurrentState == Combat` and `AbilityExecutionState.Phase != Idle`.

Phase transitions:
- **Casting** → `PhaseTimer >= CastTime` → **Active**
- **Active** → deliver damage (same hit check as 15.31: distance ≤ MeleeRange × 1.3, dot > 0.5) → **Recovery**
- **Recovery** → `PhaseTimer >= RecoveryTime` → **Idle** (set cooldown)

PendingCombatHit creation: identical to current AIAttackExecutionSystem (WeaponData from AIBrain, ResolverType = Hybrid, HitRegion = Torso).

### Task 0.4: Update AICombatBehaviorSystem

**File:** `Assets/Scripts/AI/Systems/AICombatBehaviorSystem.cs` (MODIFY)

Change attack initiation from writing `AIAttackState` to writing `AbilityExecutionState`:

```csharp
// Before (15.31):
attackState.ValueRW = new AIAttackState { Phase = AIAttackPhase.WindUp, ... };

// After (15.32):
abilityExec.ValueRW = new AbilityExecutionState
{
    Phase = AbilityCastPhase.Casting,
    SelectedAbilityIndex = 0, // Default melee (Phase 0)
    TargetEntity = target,
    CastDirection = dirToTarget,
    DamageDealt = false
};
```

Guard check changes from `AIAttackState.Phase != None` to `AbilityExecutionState.Phase != Idle`.

### Task 0.5: Update AIStateTransitionSystem Guard

**File:** `Assets/Scripts/AI/Systems/AIStateTransitionSystem.cs` (MODIFY)

Change the "never transition during attack" guard from `AIAttackState.Phase != AIAttackPhase.None` to `AbilityExecutionState.Phase != AbilityCastPhase.Idle`.

### Task 0.6: Delete AIAttackState and AIAttackExecutionSystem

**Files:**
- `Assets/Scripts/AI/Components/AIAttackState.cs` (DELETE)
- `Assets/Scripts/AI/Systems/AIAttackExecutionSystem.cs` (DELETE)

After verifying Phase 0 works identically, remove the old files.

### Task 0.7: Update AIBrainAuthoring Baker

**File:** `Assets/Scripts/AI/Authoring/AIBrainAuthoring.cs` (MODIFY)

Replace `AIAttackState` with `AbilityExecutionState` and `AbilityCooldownState` buffer (initially empty — Phase 0 reads timing from AIBrain directly).

---

## Phase 1: Ability Definition System

**Goal:** Define abilities as standalone, shareable ScriptableObjects. Designers create individual `AbilityDefinitionSO` assets (e.g., "Fireball", "Melee Jab", "Ground Slam"), then compose `AbilityProfileSO` assets that reference them. Bakers convert profiles to ECS buffers. BoxingJoe gets 2-3 abilities to prove the system.

### Task 1.1: AbilityDefinition Buffer (ECS)

**File:** `Assets/Scripts/AI/Components/AbilityDefinition.cs` (NEW)

```csharp
[InternalBufferCapacity(4)]
public struct AbilityDefinition : IBufferElementData
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| **Identity** | | | |
| AbilityId | ushort | 0 | Unique type ID (for logging/debug/trigger refs) |
| **Targeting** | | | |
| TargetingMode | AbilityTargetingMode | CurrentTarget | How to pick targets |
| Range | float | 2.5 | Max engagement distance |
| Radius | float | 0 | AOE radius (0 = single target) |
| Angle | float | 360 | Cone angle (degrees, 360 = full circle) |
| MaxTargets | int | 1 | Cap for multi-target |
| RequiresLineOfSight | bool | true | LOS check before cast |
| **Timing** | | | |
| CastTime | float | 0.4 | Wind-up / interruptible window |
| ActiveDuration | float | 0.15 | Hit window / channel duration |
| RecoveryTime | float | 0.5 | Post-attack lockout |
| Cooldown | float | 1.5 | Per-ability cooldown |
| GlobalCooldown | float | 0.5 | Shared cooldown (prevents ability spam) |
| TelegraphDuration | float | 0 | Warning time before cast starts (0 = no telegraph) |
| TickInterval | float | 0 | For channeled/DoT (0 = single hit) |
| **Charges** | | | |
| MaxCharges | int | 0 | 0 = no charge system. >0 = charge-based ability |
| ChargeRegenTime | float | 0 | Seconds per charge regen |
| **Cooldown Group** | | | |
| CooldownGroupId | byte | 0 | 0 = no group. 1-255 = shared cooldown group ID |
| CooldownGroupDuration | float | 0 | Duration applied to all abilities in this group |
| **Damage** | | | |
| DamageBase | float | 15 | Center of damage range |
| DamageVariance | float | 5 | ± random range |
| DamageType | DamageType | Physical | Theming.DamageType |
| HitCount | int | 1 | Hits per activation |
| CanCrit | bool | true | Eligible for crit rolls |
| HitboxMultiplier | float | 1.0 | Override (1.0 = use actual hitbox) |
| ResolverType | CombatResolverType | Hybrid | Which resolver processes hits |
| **Status Effects (Modifiers)** | | | |
| Modifier0Type | ModifierType | None | Primary on-hit effect (uses existing enum) |
| Modifier0Chance | float | 0 | Proc chance 0-1 |
| Modifier0Duration | float | 0 | Effect duration |
| Modifier0Intensity | float | 0 | Effect severity |
| Modifier1Type | ModifierType | None | Secondary on-hit effect |
| Modifier1Chance | float | 0 | |
| Modifier1Duration | float | 0 | |
| Modifier1Intensity | float | 0 | |
| **Conditions** | | | |
| PhaseMin | byte | 0 | Available from this phase |
| PhaseMax | byte | 255 | Available until this phase |
| HPThresholdMin | float | 0 | Only usable above this HP% |
| HPThresholdMax | float | 1 | Only usable below this HP% |
| MinTargetsInRange | int | 0 | Minimum targets within Radius to cast |
| **Behavior** | | | |
| MovementDuringCast | AbilityMovement | Locked | Free / Locked / SlowTo50 |
| Interruptible | bool | false | Can be interrupted during Casting |
| PriorityWeight | float | 1.0 | Selection weight (higher = preferred) |
| **Telegraph** | | | |
| TelegraphShape | TelegraphShape | None | None / Circle / Cone / Line / Ring / Cross |
| TelegraphDamageOnExpire | bool | false | AOE damage when telegraph completes |
| **Animation** | | | |
| AnimationTriggerHash | int | 0 | Animator.StringToHash (0 = no anim) |

**Why inline modifiers instead of a sub-buffer:** `IBufferElementData` cannot contain nested buffers. Two modifier slots (primary + secondary) cover 95% of abilities. Abilities needing 3+ effects can use multiple ability entries that fire as a combo sequence.

**Supporting Enums:**

```csharp
public enum AbilityTargetingMode : byte
{
    Self = 0,             // Buff/heal on self
    CurrentTarget = 1,    // Whoever aggro system selected
    HighestThreat = 2,    // Highest threat (may differ from current target)
    LowestHP = 3,         // Weakest player
    RandomPlayer = 4,     // Random valid target
    AllInRange = 5,       // Every entity within Range
    GroundAtTarget = 6,   // AOE centered on target position
    GroundAtSelf = 7,     // AOE centered on self
    Cone = 8,             // Cone in facing direction
    Line = 9,             // Line from self toward target
    Ring = 10             // Donut around self
}

public enum AbilityMovement : byte
{
    Free = 0,             // Can move during cast
    Locked = 1,           // Rooted during cast
    Slowed = 2            // 50% speed during cast
}

public enum TelegraphShape : byte
{
    None = 0, Circle = 1, Cone = 2, Line = 3, Ring = 4, Cross = 5
}
```

### Task 1.2: AbilityDefinitionSO — Individual Ability ScriptableObject

**File:** `Assets/Scripts/AI/Authoring/AbilityDefinitionSO.cs` (NEW)

Each ability is a standalone asset. Created via `[CreateAssetMenu(menuName = "DIG/AI/Ability Definition")]`. Stored in `Assets/Data/AI/Abilities/`.

```csharp
[CreateAssetMenu(fileName = "NewAbility", menuName = "DIG/AI/Ability Definition")]
public class AbilityDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string AbilityName;
    [Tooltip("Unique numeric ID — auto-assigned if 0")]
    public ushort AbilityId;
    [TextArea(2, 4)]
    public string Description;
    [Tooltip("Icon for Encounter Designer tool")]
    public Sprite Icon;

    [Header("Targeting")]
    public AbilityTargetingMode TargetingMode = AbilityTargetingMode.CurrentTarget;
    [Tooltip("Max engagement distance")]
    public float Range = 2.5f;
    [Tooltip("AOE radius (0 = single target)")]
    public float Radius = 0f;
    [Tooltip("Cone angle in degrees (360 = full circle)")]
    public float Angle = 360f;
    public int MaxTargets = 1;
    public bool RequiresLineOfSight = true;

    [Header("Timing")]
    [Tooltip("Wind-up / interruptible window")]
    public float CastTime = 0.4f;
    [Tooltip("Hit window / channel duration")]
    public float ActiveDuration = 0.15f;
    [Tooltip("Post-attack lockout")]
    public float RecoveryTime = 0.5f;
    [Tooltip("Per-ability cooldown")]
    public float Cooldown = 1.5f;
    [Tooltip("Shared cooldown across all abilities")]
    public float GlobalCooldown = 0.5f;
    [Tooltip("Warning time before cast (0 = no telegraph)")]
    public float TelegraphDuration = 0f;
    [Tooltip("For channeled/DoT (0 = single hit)")]
    public float TickInterval = 0f;

    [Header("Charges")]
    [Tooltip("0 = no charges. >0 = charge-based ability")]
    public int MaxCharges = 0;
    [Tooltip("Seconds per charge regeneration")]
    public float ChargeRegenTime = 0f;

    [Header("Cooldown Group")]
    [Tooltip("0 = no group. 1-255 = shared cooldown group")]
    public byte CooldownGroupId = 0;
    [Tooltip("Duration applied to all abilities sharing this group")]
    public float CooldownGroupDuration = 0f;

    [Header("Damage")]
    public float DamageBase = 15f;
    public float DamageVariance = 5f;
    public DIG.Targeting.Theming.DamageType DamageType;
    public int HitCount = 1;
    public bool CanCrit = true;
    public float HitboxMultiplier = 1.0f;
    public CombatResolverType ResolverType = CombatResolverType.Hybrid;

    [Header("On-Hit Status Effects")]
    [Tooltip("Uses existing WeaponModifier pipeline")]
    public AbilityModifierEntry PrimaryEffect;
    public AbilityModifierEntry SecondaryEffect;

    [Header("Conditions")]
    [Tooltip("Available from this encounter phase")]
    public byte PhaseMin = 0;
    [Tooltip("Available until this encounter phase")]
    public byte PhaseMax = 255;
    [Tooltip("Only usable above this HP%")]
    [Range(0, 1)] public float HPThresholdMin = 0f;
    [Tooltip("Only usable below this HP%")]
    [Range(0, 1)] public float HPThresholdMax = 1f;
    public int MinTargetsInRange = 0;

    [Header("Behavior")]
    public AbilityMovement MovementDuringCast = AbilityMovement.Locked;
    public bool Interruptible = false;
    [Tooltip("Higher = preferred in selection")]
    public float PriorityWeight = 1.0f;

    [Header("Telegraph")]
    public TelegraphShape TelegraphShape = TelegraphShape.None;
    public bool TelegraphDamageOnExpire = false;

    [Header("Animation")]
    [Tooltip("Animator trigger name (hashed during bake)")]
    public string AnimationTriggerName;

    /// <summary>
    /// Convert to ECS AbilityDefinition struct for baking.
    /// </summary>
    public AbilityDefinition ToDefinition()
    {
        return new AbilityDefinition
        {
            AbilityId = AbilityId,
            TargetingMode = TargetingMode,
            Range = Range,
            Radius = Radius,
            Angle = Angle,
            MaxTargets = MaxTargets,
            RequiresLineOfSight = RequiresLineOfSight,
            CastTime = CastTime,
            ActiveDuration = ActiveDuration,
            RecoveryTime = RecoveryTime,
            Cooldown = Cooldown,
            GlobalCooldown = GlobalCooldown,
            TelegraphDuration = TelegraphDuration,
            TickInterval = TickInterval,
            MaxCharges = MaxCharges,
            ChargeRegenTime = ChargeRegenTime,
            CooldownGroupId = CooldownGroupId,
            CooldownGroupDuration = CooldownGroupDuration,
            DamageBase = DamageBase,
            DamageVariance = DamageVariance,
            DamageType = DamageType,
            HitCount = HitCount,
            CanCrit = CanCrit,
            HitboxMultiplier = HitboxMultiplier,
            ResolverType = ResolverType,
            Modifier0Type = PrimaryEffect.Type,
            Modifier0Chance = PrimaryEffect.Chance,
            Modifier0Duration = PrimaryEffect.Duration,
            Modifier0Intensity = PrimaryEffect.Intensity,
            Modifier1Type = SecondaryEffect.Type,
            Modifier1Chance = SecondaryEffect.Chance,
            Modifier1Duration = SecondaryEffect.Duration,
            Modifier1Intensity = SecondaryEffect.Intensity,
            PhaseMin = PhaseMin,
            PhaseMax = PhaseMax,
            HPThresholdMin = HPThresholdMin,
            HPThresholdMax = HPThresholdMax,
            MinTargetsInRange = MinTargetsInRange,
            MovementDuringCast = MovementDuringCast,
            Interruptible = Interruptible,
            PriorityWeight = PriorityWeight,
            TelegraphShape = TelegraphShape,
            TelegraphDamageOnExpire = TelegraphDamageOnExpire,
            AnimationTriggerHash = string.IsNullOrEmpty(AnimationTriggerName)
                ? 0 : Animator.StringToHash(AnimationTriggerName)
        };
    }
}

[System.Serializable]
public class AbilityModifierEntry
{
    [Tooltip("Effect type (uses existing ModifierType enum from WeaponModifier)")]
    public ModifierType Type = ModifierType.None;
    [Range(0, 1)]
    [Tooltip("Proc chance (0 = never, 1 = always)")]
    public float Chance = 0f;
    [Tooltip("Effect duration in seconds")]
    public float Duration = 0f;
    [Tooltip("Effect severity/intensity")]
    public float Intensity = 0f;
}
```

**Usage:** Designers create ability assets in `Assets/Data/AI/Abilities/`:
```
Assets/Data/AI/Abilities/
├── Melee/
│   ├── Jab.asset
│   ├── HeavySlam.asset
│   ├── UpperCut.asset
│   └── AutoAttack.asset
├── Ranged/
│   ├── Fireball.asset
│   ├── IceLance.asset
│   └── PoisonSpit.asset
├── AOE/
│   ├── GroundSlam.asset
│   ├── FlameBreath.asset
│   └── PoisonCloud.asset
└── Boss/
    ├── PhaseRoar.asset
    ├── Enrage.asset
    └── SummonAdds.asset
```

### Task 1.3: AbilityProfileSO — Enemy Ability Rotation

**File:** `Assets/Scripts/AI/Authoring/AbilityProfileSO.cs` (NEW)

References a list of `AbilityDefinitionSO` assets. This is what gets attached to enemy prefabs.

```csharp
[CreateAssetMenu(fileName = "NewAbilityProfile", menuName = "DIG/AI/Ability Profile")]
public class AbilityProfileSO : ScriptableObject
{
    [Header("Selection Mode")]
    [Tooltip("Priority = first valid wins. Utility = weighted scoring.")]
    public AbilitySelectionMode SelectionMode = AbilitySelectionMode.Priority;

    [Header("Abilities (Priority Order — first valid is chosen in Priority mode)")]
    [Tooltip("Drag AbilityDefinitionSO assets here. Order matters for Priority mode.")]
    public List<AbilityDefinitionSO> Abilities = new();
}

public enum AbilitySelectionMode : byte
{
    Priority = 0,   // First valid ability wins (ordered list)
    Utility = 1     // Weighted score, highest wins (adaptive)
}
```

**Usage:** Designers compose profiles from shared abilities:
```
Assets/Data/AI/Profiles/
├── BoxingJoe_Abilities.asset     → [HeavySlam, Jab, AutoAttack]
├── FireMage_Abilities.asset      → [FlameBreath, Fireball, AutoAttack]
├── PoisonSpider_Abilities.asset  → [PoisonSpit, PoisonCloud, AutoAttack]
└── DragonBoss_Abilities.asset    → [FlameBreath, Fireball, GroundSlam, PhaseRoar, AutoAttack]
```

### Task 1.4: AbilityProfileAuthoring + Baker

**File:** `Assets/Scripts/AI/Authoring/AbilityProfileAuthoring.cs` (NEW)

MonoBehaviour that references an `AbilityProfileSO`. Baker converts entries to `AbilityDefinition` buffer and initializes `AbilityCooldownState` buffer.

```csharp
public class AbilityProfileAuthoring : MonoBehaviour
{
    public AbilityProfileSO Profile;
}

class Baker : Baker<AbilityProfileAuthoring>
{
    public override void Bake(AbilityProfileAuthoring authoring)
    {
        if (authoring.Profile == null) return;
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        var abilityBuffer = AddBuffer<AbilityDefinition>(entity);
        var cooldownBuffer = AddBuffer<AbilityCooldownState>(entity);

        foreach (var abilitySO in authoring.Profile.Abilities)
        {
            if (abilitySO == null) continue;
            abilityBuffer.Add(abilitySO.ToDefinition());
            cooldownBuffer.Add(new AbilityCooldownState
            {
                CooldownRemaining = 0,
                GlobalCooldownRemaining = 0,
                CooldownGroupRemaining = 0,
                ChargesRemaining = abilitySO.MaxCharges,
                MaxCharges = abilitySO.MaxCharges,
                ChargeRegenTimer = 0
            });
        }
    }
}
```

### Task 1.5: Update AIBrainAuthoring Fallback

**File:** `Assets/Scripts/AI/Authoring/AIBrainAuthoring.cs` (MODIFY)

If no `AbilityProfileAuthoring` is present on the prefab, the `AIBrainAuthoring` baker auto-generates a single melee ability from `AIBrain` fields (BaseDamage, MeleeRange, AttackCooldown, AttackWindUp, AttackActiveDuration, AttackRecovery). This preserves backward compatibility — existing enemies work without an SO.

### Task 1.6: Create BoxingJoe Ability Assets

**Files:** (NEW)
- `Assets/Data/AI/Abilities/Melee/HeavySlam.asset`
- `Assets/Data/AI/Abilities/Melee/Jab.asset`
- `Assets/Data/AI/Abilities/Melee/AutoAttack.asset`
- `Assets/Data/AI/Profiles/BoxingJoe_Abilities.asset`

```
HeavySlam.asset:
  TargetingMode: CurrentTarget
  Range: 3.0, CastTime: 0.8, ActiveDuration: 0.2, Recovery: 0.6
  Cooldown: 8.0, DamageBase: 40, DamageVariance: 10
  TelegraphShape: Circle, TelegraphDuration: 0.6, Radius: 3.0
  TelegraphDamageOnExpire: true (AOE)
  MovementDuringCast: Locked
  PriorityWeight: 3.0
  PrimaryEffect: Stun, Chance: 0.3, Duration: 1.5, Intensity: 0.8

Jab.asset:
  TargetingMode: CurrentTarget
  Range: 2.5, CastTime: 0.3, ActiveDuration: 0.1, Recovery: 0.3
  Cooldown: 3.0, DamageBase: 20, DamageVariance: 5
  TelegraphShape: None
  MovementDuringCast: Locked
  PriorityWeight: 2.0
  CooldownGroupId: 1 (shares cooldown with AutoAttack)
  CooldownGroupDuration: 1.0

AutoAttack.asset:
  TargetingMode: CurrentTarget
  Range: 2.5, CastTime: 0.4, ActiveDuration: 0.15, Recovery: 0.5
  Cooldown: 1.5, DamageBase: 15, DamageVariance: 5
  TelegraphShape: None
  PriorityWeight: 1.0
  CooldownGroupId: 1 (shares cooldown with Jab)
  CooldownGroupDuration: 1.0

BoxingJoe_Abilities.asset:
  SelectionMode: Priority
  Abilities: [HeavySlam, Jab, AutoAttack]
```

---

## Phase 2: Ability Execution Pipeline

**Goal:** `AbilityExecutionSystem` handles all ability types generically — melee, AOE, channeled — by reading `AbilityDefinition` fields. Status effects are applied through the existing `WeaponModifier` → `CombatResolutionSystem` pipeline.

### Task 2.1: Rewrite AbilityExecutionSystem for Buffer Reads

**File:** `Assets/Scripts/AI/Systems/AbilityExecutionSystem.cs` (MODIFY)

When `SelectedAbilityIndex >= 0`, read the ability from the buffer:

```csharp
var abilities = SystemAPI.GetBuffer<AbilityDefinition>(entity);
var ability = abilities[execState.SelectedAbilityIndex];
```

Phase transitions now use ability-specific timing:
- **Telegraph** → `PhaseTimer >= ability.TelegraphDuration` → **Casting**
- **Casting** → `PhaseTimer >= ability.CastTime` → **Active**
- **Active** → damage delivery logic (branched by TargetingMode) → **Recovery**
- **Recovery** → `PhaseTimer >= ability.RecoveryTime` → **Idle** (set cooldown)

### Task 2.2: Status Effect Integration (Modifier Passthrough)

When creating `PendingCombatHit`, the system attaches ability modifiers as `WeaponModifier` buffer elements on the **weapon entity** (which for AI is the AI entity itself):

```csharp
// In AbilityExecutionSystem, during Active phase:
// Create PendingCombatHit with WeaponEntity = aiEntity
var hitEntity = ecb.CreateEntity();
ecb.AddComponent(hitEntity, new PendingCombatHit
{
    AttackerEntity = aiEntity,
    TargetEntity = target,
    WeaponEntity = aiEntity,  // AI is its own "weapon"
    // ... other fields from ability definition
});

// Ensure AI entity has WeaponModifier buffer with ability's effects
// (Populated from AbilityDefinition.Modifier0/Modifier1 fields)
```

**How this works with existing pipeline:**
1. `AbilityExecutionSystem` creates `PendingCombatHit` with `WeaponEntity = aiEntity`
2. Before creating the hit, it writes the ability's modifier entries to a `WeaponModifier` buffer on the AI entity (cleared each frame)
3. `CombatResolutionSystem` reads `WeaponModifier` buffer from `WeaponEntity` (unchanged code)
4. CRS rolls proc chance per modifier → creates `StatusEffectRequest` on target (unchanged code)
5. `StatusEffectSystem` processes requests → applies DOT/debuff/stun (unchanged code)

**Alternative approach (if writing WeaponModifier buffer is problematic for ghosts):** Create a separate `AbilityModifierBuffer` that `CombatResolutionSystem` also checks. This avoids touching the ghost-replicated `WeaponModifier`. Decision: prefer the temporary-write approach first; fall back to separate buffer only if ghost issues arise.

### Task 2.3: Targeting Mode Dispatch

Within **Active** phase, damage delivery branches on `ability.TargetingMode`:

| TargetingMode | Active Phase Behavior |
|---------------|----------------------|
| CurrentTarget | Distance + facing check → single PendingCombatHit (same as 15.31) |
| AllInRange | Spatial query within Radius → PendingCombatHit per target (up to MaxTargets) |
| GroundAtTarget | Spawn TelegraphZone entity at TargetPosition (delayed AOE) |
| GroundAtSelf | Spawn TelegraphZone entity at self position |
| Cone | Spatial query within Range + Angle → PendingCombatHit per target |
| Line | Raycast in CastDirection → PendingCombatHit per hit |
| Self | Apply buff/effect to self (no PendingCombatHit) |

**Spatial queries** use `PhysicsWorldSingleton.CollisionWorld.OverlapSphere()` or `OverlapAabb()` with faction filtering.

### Task 2.4: Movement Override During Cast

When `ability.MovementDuringCast == Locked`, the system sets a `MovementOverride` tag component on the entity. `AICombatBehaviorSystem` checks for this tag and skips movement when present.

```csharp
public struct MovementOverride : IComponentData, IEnableableComponent { }
```

Enable on cast start, disable on Idle transition.

### Task 2.5: Cooldown Tick System

**File:** `Assets/Scripts/AI/Systems/AbilityCooldownSystem.cs` (NEW)

```
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AIStateTransitionSystem))]
[WorldSystemFilter(ServerSimulation | LocalSimulation)]
```

Each frame:
1. Decrement `AbilityCooldownState.CooldownRemaining` by deltaTime, clamp to 0
2. Decrement `GlobalCooldownRemaining` by deltaTime, clamp to 0
3. Decrement `CooldownGroupRemaining` by deltaTime, clamp to 0
4. If `ChargesRemaining < MaxCharges` AND `ChargeRegenTimer > 0`:
   - Decrement `ChargeRegenTimer` by deltaTime
   - If `<= 0`: increment `ChargesRemaining`, reset timer to `ChargeRegenTime`
5. Also decrement `AIState.AttackCooldownRemaining` (backward compat until fully migrated)

### Task 2.6: Cooldown Group Enforcement

When `AbilityExecutionSystem` sets a cooldown on ability completion:
```csharp
// Set per-ability cooldown
cooldowns[selectedIndex] = new AbilityCooldownState
{
    CooldownRemaining = ability.Cooldown,
    GlobalCooldownRemaining = ability.GlobalCooldown,
    // ... charges unchanged
};

// Set cooldown group (affects all abilities sharing the group)
if (ability.CooldownGroupId > 0)
{
    for (int i = 0; i < cooldowns.Length; i++)
    {
        var cd = cooldowns[i];
        var def = abilities[i];
        if (def.CooldownGroupId == ability.CooldownGroupId)
        {
            cd.CooldownGroupRemaining = math.max(
                cd.CooldownGroupRemaining,
                ability.CooldownGroupDuration
            );
            cooldowns[i] = cd;
        }
    }
}
```

`AbilitySelectionSystem` checks all three cooldown sources:
```csharp
if (cooldown.CooldownRemaining > 0) continue;      // Per-ability
if (cooldown.GlobalCooldownRemaining > 0) continue; // Global
if (cooldown.CooldownGroupRemaining > 0) continue;  // Group
```

---

## Phase 3: Ability Selection (AI Decision Making)

**Goal:** Smart ability picking. Priority mode for simple enemies (ordered list, pick first valid). Utility mode for elites/bosses (weighted scoring).

### Task 3.1: AbilitySelectionSystem

**File:** `Assets/Scripts/AI/Systems/AbilitySelectionSystem.cs` (NEW)

```
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(AIStateTransitionSystem))]
[UpdateBefore(typeof(AbilityExecutionSystem))]
[WorldSystemFilter(ServerSimulation | LocalSimulation)]
```

**Runs when:** `AIState.CurrentState == Combat` AND `AbilityExecutionState.Phase == Idle`

**Priority mode:**
```
for each ability in buffer (index order):
    if cooldown > 0 → skip
    if globalCooldown > 0 → skip
    if cooldownGroup > 0 → skip
    if charges required AND chargesRemaining <= 0 → skip
    if distance to target > ability.Range → skip
    if currentPhase < ability.PhaseMin || > ability.PhaseMax → skip
    if HP% < ability.HPThresholdMin || > ability.HPThresholdMax → skip
    if ability.MinTargetsInRange > 0 AND targets_in_radius < min → skip
    → SELECT this ability, break
```

**Utility mode:**
```
for each ability in buffer:
    if fails hard conditions (cooldown, range, phase) → skip
    score = ability.PriorityWeight
    score *= (1.0 + 0.5 * (ability.Cooldown / maxCooldownInPool))  // Favor high-CD abilities
    score *= (1.0 + 0.3 * targets_in_range / ability.MaxTargets)   // Favor AOE when targets stacked
    score *= randomJitter(0.9, 1.1)                                 // Slight randomness
    → Track highest score
SELECT highest-scoring ability
```

**Output:** Writes `AbilityExecutionState.SelectedAbilityIndex` and transitions to `Telegraph` or `Casting` phase (depending on `TelegraphDuration > 0`).

### Task 3.2: Update AICombatBehaviorSystem

**File:** `Assets/Scripts/AI/Systems/AICombatBehaviorSystem.cs` (MODIFY)

Remove attack initiation logic (now handled by AbilitySelectionSystem). AICombatBehaviorSystem focuses only on:
1. Movement (approach/orbit/flee based on distance and selected ability range)
2. Facing target
3. SubState tracking

The attack cooldown check (`AttackCooldownRemaining <= 0`) is replaced by the selection system's cooldown evaluation.

---

## Phase 4: Telegraph / AOE Zone System

**Goal:** Ground indicators that warn players before damage. Any ability with `TelegraphShape != None` and `TelegraphDuration > 0` gets a visual warning zone, followed by damage on expiry.

### Task 4.1: TelegraphZone Component

**File:** `Assets/Scripts/Combat/Components/TelegraphZone.cs` (NEW)

| Field | Type | Purpose |
|-------|------|---------|
| Shape | TelegraphShape | Circle, Cone, Line, Ring, Cross |
| Position | float3 | World-space center |
| Rotation | quaternion | Orientation (for cone/line) |
| Radius | float | Outer radius |
| InnerRadius | float | Inner radius (for Ring shape, 0 = solid) |
| Angle | float | Cone angle (degrees) |
| Length | float | Line length |
| Width | float | Line width |
| WarningDuration | float | Time before damage (visual warning) |
| DamageDelay | float | Time from spawn to first damage tick |
| LingerDuration | float | How long zone persists after first damage (0 = one-shot) |
| TickInterval | float | Damage repeat interval (0 = single hit) |
| Timer | float | Elapsed time since spawn |
| DamageBase | float | Damage per tick |
| DamageVariance | float | ± random range |
| DamageType | DamageType | Element |
| OwnerEntity | Entity | Who spawned this (for stat lookups) |
| MaxTargets | int | Cap per tick |
| ResolverType | CombatResolverType | How to resolve damage |
| IsSafeZone | bool | Inverted: damage OUTSIDE zone |
| HasDealtDamage | bool | One-shot flag (for non-lingering zones) |
| Modifier0Type | ModifierType | Status effect applied by zone |
| Modifier0Chance | float | Proc chance |
| Modifier0Duration | float | Effect duration |
| Modifier0Intensity | float | Effect severity |

### Task 4.2: TelegraphSpawnHelper

**File:** `Assets/Scripts/Combat/Systems/TelegraphSpawnHelper.cs` (NEW)

Static helper used by `AbilityExecutionSystem` to spawn a `TelegraphZone` entity:

```csharp
public static Entity SpawnTelegraph(
    EntityCommandBuffer ecb,
    in AbilityDefinition ability,
    float3 position, quaternion rotation,
    Entity owner)
```

Creates an entity with `TelegraphZone` + `LocalTransform` + `LocalToWorld`. Copies modifier data from ability definition to zone. No ghost replication — telegraphs are server-authoritative, visual bridge sends positions to client.

### Task 4.3: TelegraphDamageSystem

**File:** `Assets/Scripts/Combat/Systems/TelegraphDamageSystem.cs` (NEW)

```
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(CombatResolutionSystem))]
[WorldSystemFilter(ServerSimulation | LocalSimulation)]
```

Each frame:
1. Increment `Timer` by deltaTime
2. When `Timer >= DamageDelay` AND `!HasDealtDamage` (or `Timer >= last_tick + TickInterval` for lingering):
   - Spatial query based on Shape:
     - **Circle**: `OverlapSphere(Position, Radius)` → filter by distance
     - **Cone**: OverlapSphere → filter by angle to forward
     - **Line**: Raycast or AABB → filter by width
     - **Ring**: OverlapSphere(Radius) → exclude OverlapSphere(InnerRadius)
   - For each target (up to MaxTargets):
     - Create `PendingCombatHit` via ECB
     - Set `WasPhysicsHit = true`, `ResolverType` from zone
   - If `LingerDuration <= 0`: set `HasDealtDamage = true`
3. When `Timer >= DamageDelay + LingerDuration` (or `DamageDelay` if no linger):
   - Destroy the telegraph entity via ECB

**Status effects from zones:** Telegraph zones carry modifier data. When creating `PendingCombatHit`, the zone's modifier fields are attached the same way as ability modifiers (Task 2.2).

### Task 4.4: TelegraphVisualBridge

**File:** `Assets/Scripts/Combat/Bridges/TelegraphVisualBridge.cs` (NEW)

Managed system in `PresentationSystemGroup` (client-side). Reads `TelegraphZone` entities from ServerWorld, renders ground decals using a projector or world-space UI:

- **Circle**: Scaled disc decal at Position, Radius
- **Cone**: Cone mesh or projector
- **Ring**: Two concentric circles
- Color: Red (danger) during warning, bright pulse at damage time
- Fade in over first 0.2s, pulse at damage time

Uses object pooling — reuse decal GameObjects rather than instantiate/destroy.

---

## Phase 5: Boss Phase & Encounter Trigger System

**Goal:** Multi-phase encounters driven by a comprehensive trigger system. Bosses have **conditions** (HP%, timer, add deaths, position, ability count, player count, composite) that fire **actions** (transition phase, force ability, spawn adds, set invulnerable, teleport, play VFX, modify stats). This replaces simple HP-threshold-only phase transitions with a general-purpose scripting layer.

### Task 5.1: Encounter Trigger Types

**File:** `Assets/Scripts/AI/Components/EncounterTrigger.cs` (NEW)

```csharp
public enum TriggerConditionType : byte
{
    HPBelow = 0,           // Boss HP% drops below threshold
    HPAbove = 1,           // Boss HP% rises above threshold (heal mechanics)
    TimerElapsed = 2,      // Seconds since encounter start or phase start
    AddsDead = 3,          // N adds from a spawn group have died
    AddsAlive = 4,         // N adds from a spawn group are still alive
    PlayerCountInRange = 5,// N+ players within specified range
    AbilityCastCount = 6,  // Ability X has been cast N times this phase
    PhaseIs = 7,           // Current phase equals value (for composite gates)
    BossAtPosition = 8,    // Boss within range of a world position
    Composite_AND = 9,     // All referenced sub-triggers must be true
    Composite_OR = 10,     // Any referenced sub-trigger must be true
    Manual = 11            // Fired by other systems / external scripts
}

public enum TriggerActionType : byte
{
    TransitionPhase = 0,   // Move to specified phase
    ForceAbility = 1,      // Immediately select and begin casting ability by ID
    SpawnAddGroup = 2,     // Spawn a group of adds by SpawnGroupId
    SetInvulnerable = 3,   // Toggle invulnerability for duration
    Teleport = 4,          // Move boss to world position
    ModifyStats = 5,       // Apply speed/damage multiplier
    PlayVFX = 6,           // Spawn VFX entity (presentation bridge)
    PlayDialogue = 7,      // Queue dialogue/yell text
    SetEnrage = 8,         // Enable hard enrage mode
    DestroyAdds = 9,       // Kill all adds from a spawn group
    ResetCooldowns = 10,   // Reset all ability cooldowns
    EnableTrigger = 11,    // Enable another trigger by index (chaining)
    DisableTrigger = 12    // Disable another trigger by index
}
```

### Task 5.2: EncounterTriggerDefinition Buffer

**File:** `Assets/Scripts/AI/Components/EncounterTriggerDefinition.cs` (NEW)

```csharp
[InternalBufferCapacity(8)]
public struct EncounterTriggerDefinition : IBufferElementData
{
    // Condition
    public TriggerConditionType ConditionType;
    public float ConditionValue;         // Threshold (HP%, seconds, count, etc.)
    public byte ConditionParam;          // SpawnGroupId, AbilityIndex, etc.
    public float ConditionRange;         // Distance for position/player-count checks

    // For composite triggers
    public byte SubTriggerIndex0;        // Index of first sub-trigger
    public byte SubTriggerIndex1;        // Index of second sub-trigger
    public byte SubTriggerIndex2;        // Index of third sub-trigger (255 = unused)

    // Action
    public TriggerActionType ActionType;
    public float ActionValue;            // Phase number, duration, multiplier, etc.
    public byte ActionParam;             // AbilityId, SpawnGroupId, etc.
    public float3 ActionPosition;        // Teleport destination, VFX position

    // State
    public bool Enabled;                 // Can be enabled/disabled at runtime
    public bool FireOnce;                // If true, auto-disables after first fire
    public bool HasFired;                // Runtime: has this trigger fired?
    public float Delay;                  // Seconds to wait before executing action
}
```

### Task 5.3: PhaseDefinition Buffer

**File:** `Assets/Scripts/AI/Components/PhaseDefinition.cs` (NEW)

```csharp
[InternalBufferCapacity(4)]
public struct PhaseDefinition : IBufferElementData
```

| Field | Type | Purpose |
|-------|------|---------|
| PhaseIndex | byte | 0, 1, 2, 3... |
| HPThresholdEntry | float | Enter this phase when HP% drops below (e.g., 0.7 = 70%). -1 = trigger-only |
| SpeedMultiplier | float | Movement speed modifier (1.0 = normal) |
| DamageMultiplier | float | Damage output modifier |
| GlobalCooldownOverride | float | Override GCD for this phase (-1 = no override) |
| InvulnerableDuration | float | Immune window during phase transition |
| TransitionAbilityId | ushort | Ability to cast on phase entry (0 = none) |
| SpawnGroupId | byte | Add group to spawn on entry (0 = none) |

### Task 5.4: EncounterState Component

**File:** `Assets/Scripts/AI/Components/EncounterState.cs` (NEW)

Per-boss runtime state.

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| CurrentPhase | byte | 0 | Active phase index |
| PhaseTimer | float | 0 | Time in current phase |
| EncounterTimer | float | 0 | Time since combat started |
| EnrageTimer | float | -1 | Countdown to hard enrage (-1 = no enrage) |
| IsTransitioning | bool | false | Currently in invulnerability window |
| TransitionTimer | float | 0 | Elapsed transition time |
| IsEnraged | bool | false | Hard enrage active |
| AddTracker0Alive | byte | 0 | Living adds in group 0 |
| AddTracker1Alive | byte | 0 | Living adds in group 1 |
| AddTracker2Alive | byte | 0 | Living adds in group 2 |
| AddTracker3Alive | byte | 0 | Living adds in group 3 |
| AbilityCastCount0 | byte | 0 | Cast count for tracked ability 0 |
| AbilityCastCount1 | byte | 0 | Cast count for tracked ability 1 |

### Task 5.5: EncounterTriggerSystem

**File:** `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` (NEW)

```
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PhaseTransitionSystem))]
[WorldSystemFilter(ServerSimulation | LocalSimulation)]
```

Not Burst-compiled (uses ECB for structural changes, entity lookups).

Each frame for each entity with `EncounterState` + `EncounterTriggerDefinition` buffer:

1. Increment `EncounterTimer` and `PhaseTimer` by deltaTime
2. For each trigger in buffer where `Enabled == true` and `HasFired == false` (or `!FireOnce`):
   - Evaluate condition:
     - **HPBelow**: `currentHP / maxHP <= ConditionValue`
     - **TimerElapsed**: `EncounterTimer >= ConditionValue` (or `PhaseTimer` if `ConditionParam == 1`)
     - **AddsDead**: `addGroupSpawned - addGroupAlive >= (int)ConditionValue`
     - **AddsAlive**: `addGroupAlive <= (int)ConditionValue`
     - **PlayerCountInRange**: count players within `ConditionRange` of boss `>= (int)ConditionValue`
     - **AbilityCastCount**: `abilityCastCount[ConditionParam] >= (int)ConditionValue`
     - **PhaseIs**: `CurrentPhase == (byte)ConditionValue`
     - **BossAtPosition**: `distance(bossPos, triggerPos) <= ConditionRange`
     - **Composite_AND**: all sub-triggers have `HasFired == true`
     - **Composite_OR**: any sub-trigger has `HasFired == true`
   - If condition met:
     - If `Delay > 0` and no delay timer started → start delay timer, continue
     - If delay elapsed or no delay:
       - Execute action:
         - **TransitionPhase**: set `EncounterState.PendingPhase = (byte)ActionValue`
         - **ForceAbility**: write `AbilityExecutionState.SelectedAbilityIndex` with matching ability
         - **SpawnAddGroup**: create add spawn request entity
         - **SetInvulnerable**: set `IsTransitioning = true`, `TransitionTimer = 0`, duration = `ActionValue`
         - **Teleport**: write `LocalTransform.Position = ActionPosition`
         - **ModifyStats**: write to phase multiplier fields
         - **PlayVFX**: create VFX request entity
         - **PlayDialogue**: enqueue to dialogue queue
         - **SetEnrage**: `IsEnraged = true`, apply enrage multipliers
         - **DestroyAdds**: mark all adds in group for death
         - **ResetCooldowns**: zero all `AbilityCooldownState.CooldownRemaining`
         - **EnableTrigger/DisableTrigger**: set `Enabled` on trigger at `ActionParam` index
       - If `FireOnce`: set `HasFired = true`

### Task 5.6: PhaseTransitionSystem

**File:** `Assets/Scripts/AI/Systems/PhaseTransitionSystem.cs` (NEW)

```
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AbilitySelectionSystem))]
[WorldSystemFilter(ServerSimulation | LocalSimulation)]
```

Each frame:
1. Read `Health.Current / Health.Max` → HP%
2. Scan `PhaseDefinition` buffer for highest PhaseIndex where `HP% <= HPThresholdEntry` (skip entries with `HPThresholdEntry == -1` — those are trigger-only)
3. Also check `EncounterState.PendingPhase` (set by EncounterTriggerSystem)
4. Determine target phase = max(HP-based phase, trigger-based phase)
5. If target phase > CurrentPhase:
   - Set `IsTransitioning = true`, `TransitionTimer = 0`
   - If `InvulnerableDuration > 0`: entity becomes immune
   - If `TransitionAbilityId > 0`: force-select that ability
   - If `SpawnGroupId > 0`: create spawn request
   - Reset `PhaseTimer = 0`, reset ability cast counters
6. During transition: increment TransitionTimer, when `>= InvulnerableDuration` → transition complete, update `CurrentPhase`

### Task 5.7: AddSpawnSystem

**File:** `Assets/Scripts/AI/Systems/AddSpawnSystem.cs` (NEW)

Handles spawning add groups. Reads spawn request entities, instantiates from prefab references stored in `SpawnGroupDefinition` buffer on the boss entity.

```csharp
[InternalBufferCapacity(4)]
public struct SpawnGroupDefinition : IBufferElementData
{
    public byte GroupId;              // Referenced by triggers and phases
    public Entity PrefabEntity;       // Ghost prefab to spawn
    public byte Count;                // Number of adds to spawn
    public float3 SpawnOffset;        // Offset from boss position
    public float SpawnRadius;         // Random scatter radius
    public bool TetherToBoss;         // Adds leash to boss, not spawn point
}
```

### Task 5.8: EncounterProfileSO

**File:** `Assets/Scripts/AI/Authoring/EncounterProfileSO.cs` (NEW)

```csharp
[CreateAssetMenu(menuName = "DIG/AI/Encounter Profile")]
public class EncounterProfileSO : ScriptableObject
{
    [Header("Encounter Settings")]
    public float EnrageTimer = -1f; // -1 = no hard enrage
    public float EnrageDamageMultiplier = 3f;

    [Header("Phases")]
    public List<PhaseEntry> Phases = new();

    [Header("Triggers")]
    public List<TriggerEntry> Triggers = new();

    [Header("Add Groups")]
    public List<SpawnGroupEntry> SpawnGroups = new();
}

[System.Serializable]
public class PhaseEntry
{
    public string PhaseName;
    [Tooltip("-1 = trigger-only (no HP threshold)")]
    public float HPThresholdEntry = 1.0f;
    public float SpeedMultiplier = 1.0f;
    public float DamageMultiplier = 1.0f;
    public float InvulnerableDuration = 0f;
    [Tooltip("Ability to cast on phase entry")]
    public AbilityDefinitionSO TransitionAbility;
    [Tooltip("Add group to spawn on phase entry (0 = none)")]
    public byte SpawnGroupId = 0;
}

[System.Serializable]
public class TriggerEntry
{
    public string TriggerName;
    public TriggerConditionType Condition;
    public float ConditionValue;
    public byte ConditionParam;
    public float ConditionRange;

    [Header("Composite (for AND/OR triggers)")]
    public int SubTriggerIndex0 = -1;
    public int SubTriggerIndex1 = -1;
    public int SubTriggerIndex2 = -1;

    public TriggerActionType Action;
    public float ActionValue;
    public byte ActionParam;
    public Vector3 ActionPosition;

    public bool FireOnce = true;
    public float Delay = 0f;
}

[System.Serializable]
public class SpawnGroupEntry
{
    public byte GroupId;
    public GameObject AddPrefab;
    public byte Count = 1;
    public Vector3 SpawnOffset;
    public float SpawnRadius = 3f;
    public bool TetherToBoss = false;
}
```

### Task 5.9: EncounterProfileAuthoring + Baker

**File:** `Assets/Scripts/AI/Authoring/EncounterProfileAuthoring.cs` (NEW)

References `EncounterProfileSO`. Baker adds:
- `PhaseDefinition` buffer
- `EncounterState` component
- `EncounterTriggerDefinition` buffer
- `SpawnGroupDefinition` buffer

Only added to enemies that have an encounter profile — regular enemies skip phases and triggers entirely.

### Task 5.10: Integrate Phases into AbilitySelectionSystem

**Modify:** `Assets/Scripts/AI/Systems/AbilitySelectionSystem.cs`

Add phase filtering to ability selection:
```csharp
if (currentPhase < ability.PhaseMin || currentPhase > ability.PhaseMax) continue;
```

Read `EncounterState.CurrentPhase` (default 0 if component doesn't exist — all abilities with PhaseMin=0 are always available).

Also apply phase multipliers to damage:
```csharp
effectiveDamage = ability.DamageBase * phaseDefinition.DamageMultiplier;
```

---

## Phase 6: Encounter Designer Editor Window

**Goal:** A standalone Unity Editor window that lets designers visually author boss encounters without touching code or raw ScriptableObject inspectors. Operates entirely on `EncounterProfileSO` + `AbilityProfileSO` + `AbilityDefinitionSO` assets.

### Task 6.1: Editor Window Layout

**File:** `Assets/Scripts/AI/Editor/EncounterDesignerWindow.cs` (NEW)

```
[MenuItem("DIG/Encounter Designer")]
public class EncounterDesignerWindow : EditorWindow
```

**Window Layout (4 panels):**

```
┌────────────────────────────────────────────────────────────────────────┐
│  [Encounter: DragonBoss_Encounter ▼]  [New] [Save] [Validate] [Test] │
├────────────────┬───────────────────────────────────────────────────────┤
│                │                                                       │
│  ABILITY       │  ENCOUNTER TIMELINE                                   │
│  LIBRARY       │                                                       │
│                │  HP: 100% ──── 70% ──── 40% ──── 15% ──── 0%        │
│  [Search: ___] │       │ Phase 1  │ Phase 2  │ Phase 3  │ Enrage     │
│                │       │          │          │          │              │
│  ┌───────────┐ │  Abilities:                                          │
│  │ 🗡 Jab    │ │  Phase 1: [Fireball] [IceLance] [AutoAttack]        │
│  │ 💥 Slam   │ │  Phase 2: [FlameBreath] [Fireball] [GroundSlam]     │
│  │ 🔥 Fire.. │ │  Phase 3: [Enrage] [FlameBreath] [Meteor] [Summon]  │
│  │ ❄ Ice..   │ │                                                       │
│  │ ☠ Poison  │ │  Triggers:                                            │
│  │ 📢 Roar   │ │  [⏱ Timer 300s → Enrage]                             │
│  └───────────┘ │  [💀 Adds Dead x4 → Phase 3]                         │
│                │  [👥 3+ Melee → Cleave]                               │
│  [+ New Ability│                                                       │
├────────────────┼───────────────────────────────────────────────────────┤
│  TRIGGER       │  ABILITY INSPECTOR                                    │
│  EDITOR        │                                                       │
│                │  [Fireball]                                            │
│  Condition:    │  ┌─────────┬──────────┬──────────┬──────────┐        │
│  [HPBelow ▼]   │  │Targeting │ Timing   │ Damage   │ Effects  │        │
│  Value: [0.7]  │  ├─────────┴──────────┴──────────┴──────────┤        │
│                │  │ Mode: CurrentTarget                       │        │
│  Action:       │  │ Range: 25.0m                              │        │
│  [TransPhase▼] │  │ Cast: 1.2s  Active: 0.1s  Recovery: 0.5s │        │
│  Phase: [2]    │  │ Cooldown: 4.0s  Group: Fire (ID: 2)      │        │
│                │  │ Damage: 30 ± 8 (Fire)                     │        │
│  [+ Add Trigger│  │ Effect: Burn (80% chance, 3s, 0.6 sev)   │        │
│                │  │ Telegraph: Circle (R: 2.0, 0.8s warning)  │        │
│  ☐ Fire Once   │  └──────────────────────────────────────────┘        │
│  Delay: [0.0s] │                                                       │
└────────────────┴───────────────────────────────────────────────────────┘
```

### Task 6.2: Ability Library Panel

Left panel showing all `AbilityDefinitionSO` assets in the project.

- **Search/filter**: By name, damage type, targeting mode, ability ID
- **Category grouping**: Melee, Ranged, AOE, Boss (via folder structure or tags)
- **Drag-and-drop**: Drag ability card onto phase in timeline to add it
- **Preview**: Hover shows tooltip with key stats
- **Create new**: Button opens creation wizard that scaffolds a new `AbilityDefinitionSO`
- **Color coding**: By DamageType (red=Fire, blue=Ice, green=Poison, etc.)

```csharp
private void DrawAbilityLibrary()
{
    var allAbilities = AssetDatabase.FindAssets("t:AbilityDefinitionSO")
        .Select(guid => AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(
            AssetDatabase.GUIDToAssetPath(guid)))
        .Where(a => a != null)
        .ToList();

    // Filter, group, draw cards with drag support
}
```

### Task 6.3: Encounter Timeline Panel

Top-right panel showing the HP bar divided into phases.

- **HP bar**: Horizontal bar from 100% (left) to 0% (right)
- **Phase dividers**: Draggable markers on the HP bar at phase thresholds
- **Phase labels**: Editable names per phase
- **Ability slots per phase**: Shows which abilities are active in each phase
- **Drag-drop zones**: Drop abilities from library to assign to phases
- **Phase properties**: Click phase to edit speed/damage multipliers, transition ability
- **Color bands**: Visual distinction per phase (green → yellow → orange → red)

```csharp
private void DrawTimeline()
{
    // Draw HP bar with gradient
    // Draw phase dividers (draggable)
    // For each phase, draw ability cards in that phase's range
    // Handle drag-drop from library
}
```

### Task 6.4: Trigger Editor Panel

Bottom-left panel for authoring encounter triggers.

- **Trigger list**: All triggers in the encounter
- **Condition dropdown**: Select from `TriggerConditionType` enum
- **Dynamic fields**: UI changes based on condition type:
  - HPBelow: float slider (0-100%)
  - TimerElapsed: float field (seconds)
  - AddsDead: group selector + count field
  - PlayerCountInRange: count + range fields
  - Composite: multi-select of other trigger indices
- **Action dropdown**: Select from `TriggerActionType` enum
- **Dynamic action fields**: Based on action type:
  - TransitionPhase: phase selector dropdown
  - ForceAbility: ability asset field
  - SpawnAddGroup: group selector
  - Teleport: Vector3 field + scene pick button
- **Fire once checkbox**, **Delay field**
- **Enable/disable chains**: Visual lines showing trigger dependencies

### Task 6.5: Ability Inspector Panel

Bottom-right panel showing detailed view of selected ability.

- **Tabbed interface**: Targeting | Timing | Damage | Effects | Conditions | Telegraph
- **Live preview**: Editing values updates the SO asset immediately
- **Validation indicators**: Red/yellow warnings for invalid configurations
  - "Telegraph duration set but TelegraphShape is None"
  - "Cooldown group set but duration is 0"
  - "Radius > 0 but TargetingMode is CurrentTarget (should be AllInRange?)"
- **Usage tracker**: Shows which profiles reference this ability

### Task 6.6: Telegraph Preview (Scene View)

When an ability with a telegraph is selected in the editor, draw a wireframe preview in the Scene view:

```csharp
[InitializeOnLoad]
public static class TelegraphScenePreview
{
    static TelegraphScenePreview()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView view)
    {
        if (selectedAbility == null) return;
        if (selectedAbility.TelegraphShape == TelegraphShape.None) return;

        var boss = Selection.activeGameObject; // or preview position
        if (boss == null) return;

        Handles.color = new Color(1, 0, 0, 0.3f);
        switch (selectedAbility.TelegraphShape)
        {
            case TelegraphShape.Circle:
                Handles.DrawSolidDisc(boss.transform.position,
                    Vector3.up, selectedAbility.Radius);
                break;
            case TelegraphShape.Cone:
                // Draw cone arc
                break;
            // ... etc
        }
    }
}
```

### Task 6.7: Validation System

**File:** `Assets/Scripts/AI/Editor/EncounterValidator.cs` (NEW)

Automated checks run on Save and via Validate button:

| Check | Severity | Description |
|-------|----------|-------------|
| Ability gap | Error | Phase has no abilities available (boss stands idle) |
| Unreachable phase | Warning | Phase threshold can't be reached (e.g., Phase 2 at 10% but Phase 1 kills boss at 15%) |
| Infinite loop | Error | Trigger chains create circular dependency |
| Missing references | Error | Ability SO is null in profile |
| Cooldown gap | Warning | All abilities on cooldown simultaneously (boss idles) — compute worst-case rotation |
| Telegraph without shape | Warning | `TelegraphDuration > 0` but `TelegraphShape == None` |
| Phase overlap | Warning | Two phases have same HP threshold |
| Orphan triggers | Warning | Trigger references SpawnGroupId that doesn't exist |
| Composite trigger references | Error | Sub-trigger indices out of bounds |

```csharp
public static class EncounterValidator
{
    public static List<ValidationResult> Validate(
        EncounterProfileSO encounter,
        AbilityProfileSO abilities)
    {
        var results = new List<ValidationResult>();
        // Run all checks, return sorted by severity
        return results;
    }
}

public struct ValidationResult
{
    public ValidationSeverity Severity; // Error, Warning, Info
    public string Message;
    public string Context; // "Phase 2, Ability 'Fireball'"
}
```

### Task 6.8: Test Mode (Simulation)

**File:** `Assets/Scripts/AI/Editor/EncounterSimulator.cs` (NEW)

Editor-only simulation that runs through the encounter without entering play mode:

```csharp
public class EncounterSimulator
{
    public float SimulatedHP = 1.0f;          // Decreases over time
    public float SimulatedTime = 0f;
    public float DPSEstimate = 50f;           // Configurable
    public int SimulatedPlayerCount = 4;

    public List<SimulationEvent> Simulate(
        EncounterProfileSO encounter,
        AbilityProfileSO abilities,
        float duration = 600f)
    {
        var events = new List<SimulationEvent>();
        float dt = 0.1f; // 100ms ticks

        while (SimulatedTime < duration && SimulatedHP > 0)
        {
            SimulatedHP -= (DPSEstimate * dt) / estimatedBossHP;
            SimulatedTime += dt;

            // Check phase transitions
            // Check triggers
            // Simulate ability selection (priority mode)
            // Record events: phase changes, ability casts, trigger fires
        }
        return events;
    }
}
```

Outputs:
- **Timeline log**: "0:00 → Phase 1 starts. 0:45 → Phase 2 (HP: 70%). 1:12 → Adds spawn. 2:30 → Phase 3..."
- **Ability rotation**: Shows which abilities fire and when (validates no gaps)
- **DPS timeline**: Estimated boss DPS output per phase
- **Trigger timeline**: When each trigger fires
- **Warnings**: "Boss is idle for 3.2s at 1:15 — all abilities on cooldown"

---

## Future Phases (Spec'd but Not Implemented in 15.32)

| Phase | Feature | Description |
|-------|---------|-------------|
| **7** | Movement Patterns | Charge, Teleport, Orbit, Flee, ScriptedPath. MovementOverride component with duration + pattern enum. Movement system branches per pattern. |
| **8** | Animation Bridge | EnemyAnimationState component. Managed bridge system maps AbilityCastPhase + locomotion → Animator parameters. Animation event → ECS event for damage timing sync. |
| **9** | VFX Bridge | VfxRequest buffer (append-only, consumed per frame). VfxSpawnSystem reads requests → instantiates from pool. Ability execution emits requests at phase transitions. |
| **10** | UI (Cast Bars, Boss Frame) | EnemyCastBarBridge reads AbilityExecutionState → shows cast progress. BossFrameBridge reads EncounterState → phase indicator + enrage timer. |
| **11** | Audio Bridge | AbilitySfxRequest buffer. SfxSpawnSystem reads → plays spatial audio. Warning sounds for telegraphs. |
| **12** | Runtime Debug | AIDebugOverlay managed MonoBehaviour. Shows current state, ability cooldowns, phase, target. Telegraph wireframe visualization. |
| **13** | Projectile System | ProjectileSpawnRequest from AbilityExecutionSystem. Projectile entities with velocity, homing, AOE-on-impact. Travel time replaces instant hit for ranged abilities. |
| **14** | Combo System | AbilitySequenceDefinition buffer. After ability X completes, next selection is forced to Y. Enables 3-hit melee combos, cast-then-detonate patterns. |

---

## System Execution Order

```
SimulationSystemGroup:
  │
  ├── AbilityCooldownSystem (NEW — tick all cooldowns, charges)
  │
  ├── [Existing Aggro Systems — LateSimulationSystemGroup]
  │   └── AggroTargetSelectorSystem → writes TargetData
  │
  ├── EncounterTriggerSystem (NEW — evaluate conditions, fire actions)
  │
  ├── PhaseTransitionSystem (NEW — apply phase changes from HP + triggers)
  │
  ├── AIStateTransitionSystem (15.31 — HFSM state transitions)
  │   Guard: never transition if AbilityExecutionState.Phase != Idle
  │
  ├── AbilitySelectionSystem (NEW — pick next ability, respects phases + groups)
  │   Reads: AbilityDefinition buffer, cooldowns, phase, distance, HP
  │   Writes: AbilityExecutionState.SelectedAbilityIndex
  │
  ├── AIIdleBehaviorSystem (15.31 — patrol/wander)
  ├── AICombatBehaviorSystem (15.31, MODIFIED — movement only, no attack init)
  ├── AIReturnHomeBehaviorSystem (15.31 — go home)
  │
  ├── AbilityExecutionSystem (NEW — cast lifecycle + damage delivery + modifiers)
  │   Creates: PendingCombatHit (with WeaponModifier for status effects)
  │   Creates: TelegraphZone entities
  │
  ├── TelegraphDamageSystem (NEW — spatial query → PendingCombatHit on expiry)
  │
  ├── AddSpawnSystem (NEW — instantiate add entities from spawn requests)
  │
  ├── CombatResolutionSystem (EXISTING — resolves PendingCombatHit)
  │   → Processes WeaponModifier → StatusEffectRequest (EXISTING — unchanged)
  ├── DamageApplicationSystem (EXISTING — CRE → Health subtraction)
  ├── CombatReactionSystem (EXISTING — CRE → CombatState)
  │
  PresentationSystemGroup:
  ├── TelegraphVisualBridge (NEW — ground decal rendering)
  ├── CombatUIBridgeSystem (EXISTING — damage numbers)
  ├── EnemyHealthBarBridgeSystem (EXISTING — health bars)
  ├── StatusEffectVisualBridgeSystem (EXISTING — "BURNING!" text)
  └── CombatEventCleanupSystem (EXISTING — destroy CREs)
```

---

## File Summary

### Files Created (Runtime)

| # | File | Purpose | Phase |
|---|------|---------|-------|
| 1 | `Assets/Scripts/AI/Components/AbilityExecutionState.cs` | Cast lifecycle state | 0 |
| 2 | `Assets/Scripts/AI/Components/AbilityCooldownState.cs` | Per-ability cooldown buffer (with groups + charges) | 0 |
| 3 | `Assets/Scripts/AI/Systems/AbilityExecutionSystem.cs` | Generic ability execution + modifier passthrough | 0 |
| 4 | `Assets/Scripts/AI/Components/AbilityDefinition.cs` | Ability data buffer + enums + modifier slots | 1 |
| 5 | `Assets/Scripts/AI/Authoring/AbilityDefinitionSO.cs` | Individual ability ScriptableObject (shareable) | 1 |
| 6 | `Assets/Scripts/AI/Authoring/AbilityProfileSO.cs` | Enemy ability rotation (references ability SOs) | 1 |
| 7 | `Assets/Scripts/AI/Authoring/AbilityProfileAuthoring.cs` | Baker: profile SO → ECS buffers | 1 |
| 8 | `Assets/Data/AI/Abilities/Melee/HeavySlam.asset` | BoxingJoe ability | 1 |
| 9 | `Assets/Data/AI/Abilities/Melee/Jab.asset` | BoxingJoe ability | 1 |
| 10 | `Assets/Data/AI/Abilities/Melee/AutoAttack.asset` | BoxingJoe ability | 1 |
| 11 | `Assets/Data/AI/Profiles/BoxingJoe_Abilities.asset` | BoxingJoe ability profile | 1 |
| 12 | `Assets/Scripts/AI/Systems/AbilityCooldownSystem.cs` | Cooldown tick + charge regen + group enforcement | 2 |
| 13 | `Assets/Scripts/AI/Systems/AbilitySelectionSystem.cs` | AI ability picking (priority + utility) | 3 |
| 14 | `Assets/Scripts/Combat/Components/TelegraphZone.cs` | AOE zone component + modifier fields | 4 |
| 15 | `Assets/Scripts/Combat/Systems/TelegraphSpawnHelper.cs` | Zone entity creation | 4 |
| 16 | `Assets/Scripts/Combat/Systems/TelegraphDamageSystem.cs` | Spatial query + damage + status effects | 4 |
| 17 | `Assets/Scripts/Combat/Bridges/TelegraphVisualBridge.cs` | Ground decal rendering | 4 |
| 18 | `Assets/Scripts/AI/Components/EncounterTrigger.cs` | Trigger condition + action enums | 5 |
| 19 | `Assets/Scripts/AI/Components/EncounterTriggerDefinition.cs` | Trigger definition buffer | 5 |
| 20 | `Assets/Scripts/AI/Components/PhaseDefinition.cs` | Boss phase buffer | 5 |
| 21 | `Assets/Scripts/AI/Components/EncounterState.cs` | Boss runtime state (phases + adds + cast counts) | 5 |
| 22 | `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs` | Condition evaluation + action dispatch | 5 |
| 23 | `Assets/Scripts/AI/Systems/PhaseTransitionSystem.cs` | HP-threshold + trigger-based phase system | 5 |
| 24 | `Assets/Scripts/AI/Systems/AddSpawnSystem.cs` | Add group instantiation | 5 |
| 25 | `Assets/Scripts/AI/Authoring/EncounterProfileSO.cs` | Boss encounter SO (phases + triggers + spawns) | 5 |
| 26 | `Assets/Scripts/AI/Authoring/EncounterProfileAuthoring.cs` | Baker: encounter SO → ECS | 5 |

### Files Created (Editor Tooling)

| # | File | Purpose | Phase |
|---|------|---------|-------|
| 27 | `Assets/Scripts/AI/Editor/EncounterDesignerWindow.cs` | Main editor window (4-panel layout) | 6 |
| 28 | `Assets/Scripts/AI/Editor/EncounterValidator.cs` | Validation checks | 6 |
| 29 | `Assets/Scripts/AI/Editor/EncounterSimulator.cs` | Dry-run simulation | 6 |
| 30 | `Assets/Scripts/AI/Editor/TelegraphScenePreview.cs` | Scene view telegraph wireframes | 6 |

### Files Modified

| # | File | Change | Phase |
|---|------|--------|-------|
| 1 | `Assets/Scripts/AI/Systems/AICombatBehaviorSystem.cs` | Write AbilityExecutionState instead of AIAttackState | 0 |
| 2 | `Assets/Scripts/AI/Systems/AIStateTransitionSystem.cs` | Guard on AbilityExecutionState.Phase | 0 |
| 3 | `Assets/Scripts/AI/Authoring/AIBrainAuthoring.cs` | Add AbilityExecutionState + fallback ability generation | 0,1 |

### Files Deleted

| # | File | Reason | Phase |
|---|------|--------|-------|
| 1 | `Assets/Scripts/AI/Components/AIAttackState.cs` | Replaced by AbilityExecutionState | 0 |
| 2 | `Assets/Scripts/AI/Systems/AIAttackExecutionSystem.cs` | Replaced by AbilityExecutionSystem | 0 |

### Files NOT Modified

| File | Reason |
|------|--------|
| `Assets/Scripts/Player/Systems/DamageApplySystem.cs` | Burst-compiled, server-only — NEVER modify |
| `Assets/Scripts/Player/Components/DamageEvent.cs` | Ghost-replicated buffer — NEVER modify |
| `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` | Already processes WeaponModifier → StatusEffectRequest. No changes needed. |
| `Assets/Scripts/Combat/Systems/DamageApplicationSystem.cs` | Reads CRE as-is |
| `Assets/Scripts/Combat/Resolvers/*.cs` | Resolvers handle new abilities without modification |
| `Assets/Scripts/Player/Systems/StatusEffectSystem.cs` | Already ticks DOTs, applies damage. No changes needed. |
| `Assets/Scripts/Weapons/Components/WeaponModifier.cs` | Reused as-is for ability effects |

---

## Verification Traces

### Trace 1: BoxingJoe Melee Auto-Attack (Phase 0 — Backward Compatible)

```
Frame N: BoxingJoe in Combat state, target at distance 2.0m
  AbilityCooldownSystem: all cooldowns at 0
  AbilitySelectionSystem: Phase == Idle, scan buffer
    → Ability 2 "Auto-Attack": cooldown=0 ✓, GCD=0 ✓, group=0 ✓, range=2.5 > 2.0 ✓
    → SELECT index 2, set Phase = Casting
  AbilityExecutionSystem: Phase = Casting, PhaseTimer = 0
    → Ability has MovementDuringCast = Locked → enable MovementOverride

Frame N+24 (0.4s later): PhaseTimer >= CastTime (0.4)
  AbilityExecutionSystem: transition to Active
    → Distance = 2.0 ≤ 2.5×1.3 = 3.25 ✓
    → Dot(forward, toTarget) = 0.95 > 0.5 ✓
    → Create PendingCombatHit (BaseDamage=15, Variance=5, Hybrid resolver)
    → No modifiers (Modifier0Type == None) → no WeaponModifier written
    → DamageDealt = true
  CombatResolutionSystem: resolve PendingCombatHit → CombatResultEvent
    → No WeaponModifier on weapon entity → no status effects
  DamageApplicationSystem: subtract FinalDamage from target Health

Frame N+33 (0.15s after Active): PhaseTimer >= ActiveDuration
  AbilityExecutionSystem: transition to Recovery

Frame N+63 (0.5s after Recovery): PhaseTimer >= RecoveryTime
  AbilityExecutionSystem: transition to Idle
    → Set AbilityCooldownState[2].CooldownRemaining = 1.5
    → Set AbilityCooldownState[2].GlobalCooldownRemaining = 0.5
    → CooldownGroupId = 1 → set CooldownGroupRemaining = 1.0 on indices [1, 2]
    → Disable MovementOverride
  Result: Identical to 15.31 behavior. No visible change.
```

### Trace 2: BoxingJoe "Heavy Slam" with Stun Effect

```
Frame N: BoxingJoe in Combat, target at 2.5m, HeavySlam cooldown = 0
  AbilitySelectionSystem: scan buffer (priority order)
    → Ability 0 "Heavy Slam": cooldown=0 ✓, range=3.0 > 2.5 ✓
    → SELECT index 0, TelegraphDuration=0.6 > 0 → set Phase = Telegraph
  AbilityExecutionSystem: Phase = Telegraph
    → Spawn TelegraphZone entity at target position
      (Shape=Circle, Radius=3.0, WarningDuration=0.6, DamageBase=40)
      Zone carries: Modifier0Type=Stun, Chance=0.3, Duration=1.5, Intensity=0.8
    → Store TelegraphEntity reference
    → Lock movement

Frame N+36 (0.6s): PhaseTimer >= TelegraphDuration
  AbilityExecutionSystem: transition to Casting (wind-up animation)

Frame N+84 (0.8s cast): PhaseTimer >= CastTime
  AbilityExecutionSystem: transition to Active
    → TelegraphDamageOnExpire = true → NO direct PendingCombatHit
    → Telegraph handles damage independently

  TelegraphDamageSystem: TelegraphZone Timer >= DamageDelay
    → OverlapSphere(center, 3.0m) → finds 2 players
    → Create PendingCombatHit for each (DamageBase=40, Hybrid resolver)
    → Write WeaponModifier[0] on zone owner: Stun, 30% chance, 1.5s
    → Destroy TelegraphZone entity

  CombatResolutionSystem: resolve both PendingCombatHit → 2 CombatResultEvents
    → For player 1: roll stun → 0.22 < 0.30 → PROC! → StatusEffectRequest(Stun, 1.5s, 0.8)
    → For player 2: roll stun → 0.67 > 0.30 → no proc

  StatusEffectSystem: process StatusEffectRequest on player 1
    → Add Stun to StatusEffect buffer (duration=1.5s, severity=0.8)
  StatusEffectVisualBridgeSystem: detect new Stun → enqueue "STUNNED!" to StatusVisualQueue
  CombatUIBridgeSystem: show 2 damage numbers + "STUNNED!" floating text on player 1
  Result: AOE hit both players. Player 1 stunned for 1.5s. Player 2 only took damage.
```

### Trace 3: Boss Encounter with Trigger System

```
Setup: DragonBoss encounter profile:
  Phase 0: HP 100-70% (normal)
  Phase 1: HP 70-40% (speed 1.2x, damage 1.1x)
  Phase 2: HP 40-15% (speed 1.5x, damage 1.3x)
  Phase 3: HP 15-0% (enrage, damage 2.0x)

  Trigger 0: TimerElapsed 300s → SetEnrage (hard enrage at 5min)
  Trigger 1: AddsDead 4 (group 1) → TransitionPhase 2 (skip to phase 2)
  Trigger 2: HPBelow 0.7 → SpawnAddGroup 1 (spawn 4 totems)
  Trigger 3: Composite_AND [1, PhaseIs==1] → ForceAbility "PhaseRoar"
  Trigger 4: PlayerCountInRange 3 (range 5m) → ForceAbility "Cleave"

Frame N: Boss at 72% HP
  EncounterTriggerSystem: scan triggers
    → Trigger 0: 45s < 300s → skip
    → Trigger 2: 0.72 > 0.70 → skip
    → Trigger 4: 2 players in range < 3 → skip

Frame N+K: Boss takes hit, HP drops to 68%
  EncounterTriggerSystem:
    → Trigger 2: 0.68 ≤ 0.70 → FIRE! SpawnAddGroup 1 → create spawn request
    → Mark Trigger 2 HasFired = true (FireOnce)
  PhaseTransitionSystem: HP ≤ 0.70 → Phase 1
    → Set IsTransitioning, SpeedMultiplier=1.2, DamageMultiplier=1.1
  AddSpawnSystem: spawn 4 totem adds from group 1

Frame N+K+M: Players kill 4th totem
  EncounterTriggerSystem:
    → Trigger 1: AddsDead(group1) = 4 >= 4 → FIRE! TransitionPhase 2
    → Trigger 3: Composite_AND [Trigger1=fired ✓, PhaseIs==1 ✓] → FIRE! ForceAbility "PhaseRoar"
  PhaseTransitionSystem: forced to Phase 2
    → Speed 1.5x, Damage 1.3x, InvulnerableDuration = 2.0s
  AbilityExecutionSystem: forced "PhaseRoar" (self-targeted VFX + dialogue)
  Result: Boss skipped to Phase 2 because adds died, not because HP hit 40%.

Frame 300s: 5 minutes elapsed
  EncounterTriggerSystem:
    → Trigger 0: 300s >= 300s → FIRE! SetEnrage
    → EncounterState.IsEnraged = true
    → DamageMultiplier = EnrageDamageMultiplier (3.0x)
  Result: Hard enrage regardless of current phase.
```

### Trace 4: Cooldown Groups in Action

```
BoxingJoe abilities: Jab (groupId=1), AutoAttack (groupId=1), HeavySlam (groupId=0)

Frame N: Jab selected (groupId=1, groupDuration=1.0)
  ... Jab executes ...

Frame N+30: Jab completes
  AbilityExecutionSystem: set cooldowns
    → Jab cooldown = 3.0s
    → Jab GCD = 0.5s
    → Group 1 cooldown: scan all abilities with groupId=1
      → Jab[1].CooldownGroupRemaining = 1.0s
      → AutoAttack[2].CooldownGroupRemaining = 1.0s  ← blocked too!

Frame N+31: AbilitySelectionSystem scans
    → HeavySlam[0]: cooldown=8.0 (from last use) → skip
    → Jab[1]: cooldown=3.0 → skip
    → AutoAttack[2]: cooldown=0 BUT groupCooldown=1.0 → skip
    → No ability available → boss waits (CircleStrafe)

Frame N+91 (1.0s later): Group cooldown expires
    → AutoAttack[2]: cooldown=0, GCD=0, group=0 → SELECT
  Result: Jab and AutoAttack share a 1s group cooldown. Using one locks out the other.
```

### Trace 5: Backward Compatibility (Enemy Without AbilityProfile)

```
BoxingJoe prefab WITHOUT AbilityProfileAuthoring:
  AIBrainAuthoring baker detects no AbilityProfileAuthoring on entity
    → Auto-generates single AbilityDefinition from AIBrain fields:
      Range = MeleeRange, CastTime = AttackWindUp, etc.
      Modifier0Type = None (no status effects)
      CooldownGroupId = 0 (no group)
    → Adds 1-entry AbilityDefinition buffer + AbilityCooldownState buffer
    → Adds AbilityExecutionState (Phase = Idle)

  Runtime: identical behavior to EPIC 15.31 melee attack
  Result: Existing enemies work without any prefab changes.
```

---

## Critical Safety Rules

1. **NEVER modify `DamageApplySystem.cs`** — Burst-compiled, server-only, ghost-aware
2. **NEVER modify `DamageEvent.cs`** — ghost-replicated buffer
3. **NEVER create `IBufferElementData` on ghost-replicated entities at runtime** — structural changes cause ghost desync
4. **All new AI/ability systems:** `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
5. **AbilityDefinition and AbilityCooldownState** are baked buffers, NOT ghost-replicated — they only exist on server/local world. Clients don't need ability definitions.
6. **TelegraphZone entities** are NOT ghost-replicated — they're server-side only. The visual bridge queries ServerWorld from the client presentation system (same pattern as EnemyHealthBarBridgeSystem).
7. **PendingCombatHit creation** must use synchronous ECB playback (same frame) — use `EntityCommandBuffer` from `EndSimulationEntityCommandBufferSystem`, NOT async jobs.
8. **Phase transitions** must complete before AbilitySelectionSystem runs — enforce via `[UpdateBefore]`.
9. **Ability definitions are read-only at runtime** — never modify the buffer. Only `AbilityCooldownState` is writable.
10. **EncounterState invulnerability** must be checked in `DamageApplicationSystem` — add `WithNone<Invulnerable>` or check enable-flag.
11. **Status effects from abilities flow through EXISTING pipeline** — ability modifiers → WeaponModifier on PendingCombatHit → CombatResolutionSystem → StatusEffectRequest → StatusEffectSystem. Do NOT create a parallel status effect path.
12. **Cooldown group enforcement** happens in AbilityExecutionSystem on ability completion AND in AbilityCooldownSystem for time decay — never modify group cooldowns elsewhere.
13. **EncounterTriggerSystem** runs BEFORE PhaseTransitionSystem — triggers can set PendingPhase, and PhaseTransitionSystem applies it.
14. **Editor tools** (Phase 6) are editor-only — no runtime dependencies. All files in `Assets/Scripts/AI/Editor/` with `#if UNITY_EDITOR` guards.

---

## Known Limitations (Phase 1-6 Scope)

| # | Limitation | Workaround | Addressed In |
|---|-----------|------------|--------------|
| 1 | No animation integration — attacks have no visual feedback beyond damage numbers | Timer-based phases approximate animation timing | Phase 8 |
| 2 | No projectile spawning — ranged abilities hit instantly | Use telegraph with delay to simulate travel time | Phase 13 |
| 3 | No combo chains — abilities are independent | Sequence via Priority order + short cooldowns | Phase 14 |
| 4 | No interrupt system — player can't interrupt casts | Interruptible flag exists but no player ability triggers it | Future |
| 5 | No enrage timer UI — players can't see countdown | Use dialogue trigger to warn ("The dragon grows restless!") | Phase 10 |
| 6 | Telegraph visuals are basic — no particle effects | Ground decal only | Phase 9 (VFX) |
| 7 | Max 2 status effects per ability | Chain abilities as combo or use telegraph zone for additional effects | Phase 14 |
| 8 | Add spawning limited to 4 groups per encounter | Sufficient for 95% of encounters. Extend buffer if needed. | Future |
| 9 | Encounter Designer simulation is estimate-based | Not a real ECS simulation — uses heuristic DPS/timing | Future |
| 10 | No movement patterns (charge, teleport, orbit) | Boss stays in place or uses normal chase movement | Phase 7 |

---

## Key Files Referenced

| File | Role |
|------|------|
| `Assets/Scripts/AI/Components/AIBrain.cs` | Archetype + config, selection mode field |
| `Assets/Scripts/AI/Components/AIState.cs` | HFSM state (CombatBehavior reads) |
| `Assets/Scripts/AI/Systems/AIAttackExecutionSystem.cs` | **REPLACED** by AbilityExecutionSystem |
| `Assets/Scripts/AI/Systems/AICombatBehaviorSystem.cs` | Movement + target tracking (attack init removed) |
| `Assets/Scripts/AI/Authoring/AIBrainAuthoring.cs` | Baker fallback for ability generation |
| `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` | Consumes PendingCombatHit, processes WeaponModifier → StatusEffectRequest (unchanged) |
| `Assets/Scripts/Combat/Resolvers/CombatContext.cs` | WeaponStats, StatBlock definitions |
| `Assets/Scripts/Combat/Resolvers/CombatResolverType.cs` | Resolver enum (Hybrid, StatBasedDirect, etc.) |
| `Assets/Scripts/Combat/Components/CombatState.cs` | Combat enter/exit tracking |
| `Assets/Scripts/Player/Abilities/MovementPolishComponents.cs` | MoveTowardsAbility definition |
| `Assets/Scripts/Aggro/Components/AggroState.cs` | IsAggroed flag for state transitions |
| `Assets/Scripts/Targeting/TargetData.cs` | Current target from aggro system |
| `Assets/Scripts/Targeting/Theming/IndicatorThemeContext.cs` | DamageType, HitType, ResultFlags enums |
| `Assets/Scripts/Weapons/Components/WeaponModifier.cs` | ModifierType enum, WeaponModifier buffer (reused for ability effects) |
| `Assets/Scripts/Player/Components/StatusEffect.cs` | StatusEffectType enum, StatusEffect + StatusEffectRequest buffers |
| `Assets/Scripts/Player/Systems/StatusEffectSystem.cs` | Existing DOT/debuff tick system (unchanged) |
| `Assets/Scripts/Combat/Systems/StatusEffectVisualBridgeSystem.cs` | "BURNING!" text detection (unchanged) |
