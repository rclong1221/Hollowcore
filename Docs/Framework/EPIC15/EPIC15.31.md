# EPIC 15.31: Enemy AI Brain — Vertical Slice

**Status:** Complete
**Last Updated:** February 13, 2026
**Priority:** Critical (Combat Core)
**Dependencies:**
- EPIC 15.19 (Aggro/Threat System — complete)
- EPIC 15.28 (Unified Combat Resolution Pipeline — complete)
- EPIC 15.30 (Damage Pipeline Visual Unification — complete)
- Vision/Detection System (complete)

**Feature:** HFSM-based AI brain that reads the existing aggro/targeting pipeline and makes behavioral decisions — patrol, chase, attack, return home. Vertical slice using BoxingJoe as reference implementation. All damage flows through the existing combat resolution pipeline with zero new damage systems.

---

## Problem Statement

The project has a **complete perception-to-targeting pipeline** but **zero behavior/decision-making**:

```
BUILT (12 aggro systems, vision, targeting)          MISSING
─────────────────────────────────────────          ──────────
Vision/Detection → Threat Evaluation → Target →     ??? → Action Execution
  DetectionSystem    ThreatFrom*Systems  Selector        MoveTowardsAbility ✓
  Cone FOV ✓         Sight/Damage/Hearing ✓              SweptMeleeHitboxSystem ✓
  Proximity ✓        Decay/Leash/Share ✓                 PendingCombatHit → CRS ✓
  LOS raycasts ✓     Hysteresis ✓                        WeaponFireSystem ✓
  Alert states ✓     → TargetData ✓
```

Enemies detect players, track threats, select targets... then stand there. The missing "brain" layer reads `AggroState`/`TargetData` and decides: move, attack, flee, or return home.

---

## Architecture Decision

**HFSM (Hierarchical Finite State Machine) + Utility-based action selection within combat state.**

**Why HFSM:**
- Maps directly to existing `AlertState` levels (IDLE → SUSPICIOUS → COMBAT)
- State transitions are enum comparisons + float thresholds — fully Burst-compatible
- Simple enough for swarm creatures (3 states), extensible enough for elites
- Zero managed code, zero allocations, zero pointer chasing

**Why Utility Scoring (within combat):**
- Score available actions based on existing component data (distance, health %, cooldowns)
- Just `float` math on `ComponentLookup` data — Burst-native
- Different enemy archetypes get different weight profiles via baked config

**Why NOT behavior trees:** Managed/OOP, tree traversal is cache-unfriendly for 200+ entities, overkill for this game type.

**Why NOT GOAP:** Planning algorithm — expensive per-entity, designed for complex multi-step goals (Sims), not reactive combat.

---

## Implemented Architecture

```
Aggro Pipeline (existing, previous frame)
  └→ AggroState.IsAggroed, TargetData.TargetEntity

AI Brain (NEW, this EPIC)
  ├→ AIStateTransitionSystem: HFSM state selection
  ├→ AIIdleBehaviorSystem: patrol/wander
  ├→ AICombatBehaviorSystem: chase/attack decision (utility scoring)
  ├→ AIAttackExecutionSystem: attack lifecycle → PendingCombatHit
  └→ AIReturnHomeBehaviorSystem: go home on leash

Downstream (existing, same frame)
  ├→ CombatResolutionSystem: resolves PendingCombatHit → CombatResultEvent
  ├→ DamageApplicationSystem: CRE → Health subtraction → damage numbers
  └→ MoveTowardsSystem: MoveTowardsAbility → PhysicsVelocity
```

**Update group rationale:** Aggro systems run in `LateSimulationSystemGroup`. AI systems in `SimulationSystemGroup` read **previous frame's** aggro data — one frame of perception latency is imperceptible and standard for reactive AI. The upside: AI-created `PendingCombatHit` entities get processed by `CombatResolutionSystem` in the **same frame** (both in SimulationSystemGroup), giving instant damage feedback.

---

## Implementation Phases

### Phase 0: Bug Fix (Prerequisite) [COMPLETE]

#### Task 0.1: Fix MoveTowardsSystem Multi-Entity Bug
- [x] **File:** `Assets/Scripts/Player/Systems/Abilities/MoveTowardsSystem.cs` line 22
- [x] Change `return` → `continue`
- [x] Bug: `return` exits the entire system when the first entity in the query has `IsMoving == false`. No other entities get processed that frame. Breaks multi-entity AI movement.

```csharp
// BEFORE (broken):
if (!moveTowards.ValueRO.IsMoving) return;

// AFTER (fixed):
if (!moveTowards.ValueRO.IsMoving) continue;
```

---

### Phase 1: AI Components [COMPLETE]
*Data-driven configuration and runtime state. All components are unmanaged structs for Burst compatibility.*

#### Task 1.1: AIBrain Component + Enums
- [x] **New file:** `Assets/Scripts/AI/Components/AIBrain.cs`
- [x] `AIBrainArchetype` enum: `Melee, Ranged, Swarm, Elite, Boss` (byte, for future Phase 2+)
- [x] `AIBehaviorState` enum: `Idle, Patrol, Investigate, Combat, Flee, ReturnHome` (byte)
- [x] `AICombatSubState` enum: `Approach, Attack, CircleStrafe, Retreat` (byte)
- [x] `AIBrain : IComponentData` — baked config, designer-tunable per enemy type:

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `Archetype` | AIBrainArchetype | Melee | Enemy category |
| `MeleeRange` | float | 2.5 | Attack reach (meters) |
| `ChaseSpeed` | float | 5.0 | Movement speed when pursuing (m/s) |
| `PatrolSpeed` | float | 1.5 | Movement speed when wandering (m/s) |
| `PatrolRadius` | float | 8.0 | Wander radius from SpawnPosition |
| `AttackCooldown` | float | 1.5 | Seconds between attacks |
| `AttackWindUp` | float | 0.4 | Telegraph time before hit (player readability) |
| `AttackActiveDuration` | float | 0.15 | Hit window duration |
| `AttackRecovery` | float | 0.5 | Vulnerable period after attack |
| `FleeHealthPercent` | float | 0.2 | Flee below this HP % (Phase 2) |
| `BaseDamage` | float | 15 | Base attack damage |
| `DamageVariance` | float | 5 | ± variance (Min/Max = Base ∓ Variance) |
| `DamageType` | DIG.Targeting.Theming.DamageType | Physical | Elemental type |

#### Task 1.2: AIState Component
- [x] **New file:** `Assets/Scripts/AI/Components/AIState.cs`
- [x] `AIState : IComponentData` — runtime state managed by AIStateTransitionSystem:

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `CurrentState` | AIBehaviorState | Idle | Current HFSM state |
| `SubState` | AICombatSubState | Approach | Active combat sub-state |
| `StateTimer` | float | 0 | Time in current state |
| `SubStateTimer` | float | 0 | Time in current sub-state |
| `AttackCooldownRemaining` | float | 0 | Countdown to next attack |
| `PatrolTarget` | float3 | zero | Current wander destination |
| `HasPatrolTarget` | bool | false | Whether patrol target is set |
| `RandomSeed` | uint | entity hash | Deterministic per-entity random |

#### Task 1.3: AIAttackState Component
- [x] **New file:** `Assets/Scripts/AI/Components/AIAttackState.cs`
- [x] `AIAttackPhase` enum: `None, WindUp, Active, Recovery` (byte)
- [x] `AIAttackState : IComponentData` — attack lifecycle tracking:

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `Phase` | AIAttackPhase | None | Current attack phase |
| `PhaseTimer` | float | 0 | Time in current phase |
| `TargetEntity` | Entity | Null | Locked at attack start |
| `AttackDirection` | float3 | zero | Locked at WindUp → Active |
| `DamageDealt` | bool | false | Prevent multi-hit per swing |

---

### Phase 2: Authoring [COMPLETE]

#### Task 2.1: AIBrainAuthoring
- [x] **New file:** `Assets/Scripts/AI/Authoring/AIBrainAuthoring.cs`
- [x] MonoBehaviour with `[AddComponentMenu("DIG/AI/AI Brain")]`
- [x] Inspector fields for all `AIBrain` config values with `[Header]` groups
- [x] Inner `Baker<AIBrainAuthoring>` class bakes:
  - `AIBrain` (from Inspector values)
  - `AIState` (default Idle, random seed from entity index)
  - `AIAttackState` (default Phase=None)
  - `MoveTowardsAbility` (IsMoving=false, StopDistance=0.5f)
  - `AttackStats` (defaults: AttackPower=5, CritChance=0.1, CritMultiplier=1.5, Accuracy=1.0)
  - `DefenseStats` (defaults: Defense=5, Evasion=0.05)
  - `CombatState` (for CombatReactionSystem integration)
- [x] Uses `TransformUsageFlags.Dynamic`
- [x] Pattern follows `AggroAuthoring.cs`

---

### Phase 3: AI Systems [COMPLETE]
*All systems: `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`, `[UpdateInGroup(SimulationSystemGroup)]`, `[UpdateBefore(CombatResolutionSystem)]`*

#### Task 3.1: AIStateTransitionSystem — HFSM State Machine
- [x] **New file:** `Assets/Scripts/AI/Systems/AIStateTransitionSystem.cs`
- [x] `[BurstCompile] partial struct AIStateTransitionSystem : ISystem`
- [x] Reads: `AggroState`, `Health`, `SpawnPosition`, `AIBrain`, `AIAttackState`, `LocalTransform`, `MoveTowardsAbility`
- [x] Writes: `AIState`
- [x] Increments `StateTimer` and `SubStateTimer` by `deltaTime` each frame
- [x] Decrements `AttackCooldownRemaining` by `deltaTime` (clamp to 0)
- [x] State transitions:

| From | To | Condition |
|------|----|-----------|
| Idle | Patrol | `StateTimer > random(5-15s)` |
| Patrol | Idle | `!MoveTowardsAbility.IsMoving` (arrived) OR `StateTimer > 20s` |
| **ANY** | **Combat** | `AggroState.IsAggroed == true` |
| Combat | ReturnHome | `!AggroState.IsAggroed` (leash/target died/fled) |
| ReturnHome | Idle | `distance(pos, SpawnPosition) < 1.5m` |

- [x] **Guard:** Never transition during active attack (`AIAttackState.Phase != None`)
- [x] On COMBAT entry: reset SubState=Approach, clear HasPatrolTarget, reset SubStateTimer
- [x] On RETURN_HOME entry: clear AIAttackState to Phase=None
- [x] On any state change: reset StateTimer=0

#### Task 3.2: AIIdleBehaviorSystem — Patrol/Wander
- [x] **New file:** `Assets/Scripts/AI/Systems/AIIdleBehaviorSystem.cs`
- [x] `[BurstCompile] partial struct AIIdleBehaviorSystem : ISystem`
- [x] `[UpdateAfter(AIStateTransitionSystem)]`
- [x] Active when: `CurrentState == Idle || CurrentState == Patrol`
- [x] **Idle behavior:** Stop movement (`MoveTowardsAbility.IsMoving = false`)
- [x] **Patrol behavior:**
  - If `!HasPatrolTarget`: pick random XZ point within `PatrolRadius` of `SpawnPosition.Position` using `Unity.Mathematics.Random(RandomSeed++)`. Maintain Y coordinate.
  - Write `MoveTowardsAbility.TargetPosition`, `MoveSpeed = PatrolSpeed`, `IsMoving = true`, `StopDistance = 0.5f`
  - When `MoveTowardsAbility.IsMoving` becomes false (arrived): clear `HasPatrolTarget` (next frame picks new target)

#### Task 3.3: AICombatBehaviorSystem — Chase & Attack Decisions
- [x] **New file:** `Assets/Scripts/AI/Systems/AICombatBehaviorSystem.cs`
- [x] `[BurstCompile] partial struct AICombatBehaviorSystem : ISystem`
- [x] `[UpdateAfter(AIStateTransitionSystem)]`
- [x] Active when: `CurrentState == Combat`
- [x] Requires `ComponentLookup<LocalTransform>` to read target position
- [x] **Sub-state logic:**
  1. Get target position from `TargetData.TargetEntity` via `LocalTransform` lookup
  2. If `AIAttackState.Phase != None` → stop movement, skip sub-state selection (attack in progress)
  3. If `distance > MeleeRange` → **Approach**: write `MoveTowardsAbility.TargetPosition = targetPos`, `MoveSpeed = ChaseSpeed`, `IsMoving = true`, `StopDistance = MeleeRange * 0.8`
  4. If `distance <= MeleeRange && AttackCooldownRemaining <= 0` → **Attack**: stop movement, set `AIAttackState = { Phase=WindUp, TargetEntity=target, PhaseTimer=0, DamageDealt=false }`
  5. If `distance <= MeleeRange && AttackCooldownRemaining > 0` → stand facing target (CircleStrafe placeholder for Phase 2)
- [x] **Always face target:** slerp `LocalTransform.Rotation` toward target direction
- [x] Edge case: `TargetData.TargetEntity == Entity.Null` or target `LocalTransform` not found → skip (aggro will drop next frame → state transitions to ReturnHome)

#### Task 3.4: AIAttackExecutionSystem — Attack Lifecycle
- [x] **New file:** `Assets/Scripts/AI/Systems/AIAttackExecutionSystem.cs`
- [x] `partial class AIAttackExecutionSystem : SystemBase` (NOT Burst — uses ECB for PendingCombatHit structural changes)
- [x] `[UpdateAfter(AICombatBehaviorSystem)]`
- [x] Manages attack phases:

| Phase | Duration | Action |
|-------|----------|--------|
| **WindUp** | `AttackWindUp` (0.4s) | Increment timer. On completion: lock `AttackDirection = normalize(targetPos - selfPos)`, transition to Active |
| **Active** | `AttackActiveDuration` (0.15s) | If `!DamageDealt`: distance check (`<= MeleeRange * 1.3`) + facing check (`dot > 0.5`). If pass: create `PendingCombatHit` via ECB, set `DamageDealt = true`. On timer complete: transition to Recovery |
| **Recovery** | `AttackRecovery` (0.5s) | Increment timer. On completion: set `AttackCooldownRemaining = brain.AttackCooldown`, transition to None |

- [x] **PendingCombatHit creation:**
```csharp
var hitEntity = ecb.CreateEntity();
ecb.AddComponent(hitEntity, new PendingCombatHit
{
    AttackerEntity = aiEntity,
    TargetEntity = attackState.TargetEntity,
    WeaponEntity = aiEntity,                    // AI IS the weapon
    HitPoint = targetPos,
    HitNormal = new float3(0, 1, 0),
    HitDistance = distance,
    WasPhysicsHit = true,
    ResolverType = CombatResolverType.Hybrid,   // Uses AttackStats/DefenseStats
    WeaponData = new WeaponStats
    {
        BaseDamage = brain.BaseDamage,
        DamageMin = brain.BaseDamage - brain.DamageVariance,
        DamageMax = brain.BaseDamage + brain.DamageVariance,
        DamageType = brain.DamageType,
        CanCrit = true
    },
    HitRegion = HitboxRegion.Torso,
    HitboxMultiplier = 1.0f,
    DamagePreApplied = false,                   // Let CRS + DAS handle health
    AttackDirection = attackState.AttackDirection
});
```
- [x] ECB uses `Allocator.Temp` with synchronous `Playback(EntityManager)` — PendingCombatHit exists before CRS runs
- [x] Edge case — target dies during wind-up: Active phase distance check fails, attack whiffs. Aggro drops → ReturnHome next frame
- [x] Edge case — AI dies during attack: `DeathTransitionSystem` adds `Disabled` → entity removed from all queries. No cleanup needed
- [x] Edge case — target out of range at Active phase: distance check fails, attack whiffs (no damage), timer still completes normally

#### Task 3.5: AIReturnHomeBehaviorSystem — Go Home
- [x] **New file:** `Assets/Scripts/AI/Systems/AIReturnHomeBehaviorSystem.cs`
- [x] `[BurstCompile] partial struct AIReturnHomeBehaviorSystem : ISystem`
- [x] `[UpdateAfter(AIStateTransitionSystem)]`
- [x] Active when: `CurrentState == ReturnHome`
- [x] Write `MoveTowardsAbility.TargetPosition = SpawnPosition.Position`, `MoveSpeed = ChaseSpeed`, `IsMoving = true`
- [x] AIStateTransitionSystem handles transition to Idle on arrival (`distance < 1.5m`)

---

## System Execution Order

```
LateSimulationSystemGroup (previous frame):
  ├── ThreatDecaySystem (existing — passive threat reduction)
  ├── AggroTargetSelectorSystem (existing — picks highest threat → TargetData)
  ├── AggroCombatStateIntegration (existing — aggro → CombatState)
  └── LeashSystem (existing — clears aggro beyond distance)

SimulationSystemGroup (current frame):
  ├── DetectionSystem (existing — vision/proximity/hearing)
  ├── ThreatFrom*Systems (existing — accumulate threat from sight/damage/sound)
  │
  ├── AIStateTransitionSystem (NEW — reads prev-frame aggro, HFSM transitions)
  ├── AIIdleBehaviorSystem (NEW — patrol/wander when idle)
  ├── AICombatBehaviorSystem (NEW — chase + attack decisions)
  ├── AIAttackExecutionSystem (NEW — attack lifecycle → PendingCombatHit)
  ├── AIReturnHomeBehaviorSystem (NEW — navigate to SpawnPosition)
  │
  ├── CombatResolutionSystem (existing — resolves PendingCombatHit same frame)
  ├── DamageApplicationSystem (existing — CRE → Health → damage numbers)
  └── CombatReactionSystem (existing — CRE → CombatState)

PredictedFixedStepSimulationSystemGroup:
  └── AbilitySystemGroup:
      └── MoveTowardsSystem (existing, BUGFIX — MoveTowardsAbility → PhysicsVelocity)
```

---

## File Summary

### Files Created (9)

| # | File | Purpose | Phase |
|---|------|---------|-------|
| 1 | `Assets/Scripts/AI/Components/AIBrain.cs` | Brain config + enums (AIBrainArchetype, AIBehaviorState, AICombatSubState) | 1 |
| 2 | `Assets/Scripts/AI/Components/AIState.cs` | Runtime HFSM state + timers | 1 |
| 3 | `Assets/Scripts/AI/Components/AIAttackState.cs` | Attack lifecycle state + AIAttackPhase enum | 1 |
| 4 | `Assets/Scripts/AI/Authoring/AIBrainAuthoring.cs` | MonoBehaviour + Baker for enemy prefabs | 2 |
| 5 | `Assets/Scripts/AI/Systems/AIStateTransitionSystem.cs` | HFSM state machine (Burst) | 3 |
| 6 | `Assets/Scripts/AI/Systems/AIIdleBehaviorSystem.cs` | Patrol/wander behavior (Burst) | 3 |
| 7 | `Assets/Scripts/AI/Systems/AICombatBehaviorSystem.cs` | Chase + attack decisions (Burst) | 3 |
| 8 | `Assets/Scripts/AI/Systems/AIAttackExecutionSystem.cs` | Attack lifecycle + PendingCombatHit creation (SystemBase) | 3 |
| 9 | `Assets/Scripts/AI/Systems/AIReturnHomeBehaviorSystem.cs` | Return to SpawnPosition (Burst) | 3 |

### Files Modified (1)

| # | File | Change | Phase |
|---|------|--------|-------|
| 10 | `Assets/Scripts/Player/Systems/Abilities/MoveTowardsSystem.cs` | Line 22: `return` → `continue` (multi-entity bug) | 0 |

### Files NOT Modified (confirmed safe)

| File | Reason |
|------|--------|
| `DamageApplySystem.cs` | **NEVER MODIFY.** Burst-compiled, server-only, ghost-aware. |
| `SimpleDamageApplySystem.cs` | Already handles NPC damage correctly |
| `CombatResolutionSystem.cs` | Consumes PendingCombatHit without changes — AI hits flow through same pipeline |
| `DamageApplicationSystem.cs` | Processes CRE → Health without changes |
| `AggroTargetSelectorSystem.cs` | Writes TargetData — AI reads only, never writes |
| `LeashSystem.cs` | Clears aggro — AI reads IsAggroed flag, never modifies |
| `WeaponFireSystem.cs` | Player weapon system — AI uses PendingCombatHit directly |
| `SweptMeleeHitboxSystem.cs` | Player melee — AI bypasses with direct PendingCombatHit |

---

## Verification Traces

### Trace 1: BoxingJoe Detects, Chases, Attacks Player

```
Frame 0: BoxingJoe spawned → AIState.CurrentState = Idle
Frame 5-15: StateTimer exceeds random threshold → Idle → Patrol
            AIIdleBehaviorSystem picks random point → MoveTowardsAbility.IsMoving = true
            MoveTowardsSystem applies PhysicsVelocity → BoxingJoe wanders

Frame N: Player enters DetectionSensor range
         DetectionSystem adds SeenTargetElement
         ThreatFromVisionSystem adds ThreatEntry (sight=10.0)
         AggroTargetSelectorSystem sets AggroState.IsAggroed = true, TargetData.TargetEntity = player

Frame N+1: AIStateTransitionSystem reads IsAggroed=true → CurrentState = Combat
           AICombatBehaviorSystem: distance > MeleeRange → SubState = Approach
           MoveTowardsAbility.TargetPosition = player pos, MoveSpeed = 5.0
           MoveTowardsSystem applies velocity → BoxingJoe chases

Frame N+k: AICombatBehaviorSystem: distance <= MeleeRange, cooldown ready
           SubState = Attack, AIAttackState.Phase = WindUp (0.4s telegraph)

Frame N+k+7: WindUp complete → Phase = Active
             Distance check passes, facing check passes
             AIAttackExecutionSystem creates PendingCombatHit (ECB playback)
             CombatResolutionSystem resolves → CombatResultEvent (crit roll, damage calc)
             DamageApplicationSystem → player Health reduced
             DamageVisualQueue → damage number appears
             CombatReactionSystem → player CombatState.IsInCombat = true

Frame N+k+10: Recovery complete → Phase = None
              AttackCooldownRemaining = 1.5s
              Cooldown ticks down... next attack in 1.5s
```

### Trace 2: Player Kills BoxingJoe

```
Player hits BoxingJoe → DamageEvent → SimpleDamageApplySystem → Health.Current decreases
Health.Current <= 0 → DeathTransitionSystem:
  1. DeathState.Phase = Dead
  2. DiedEvent enabled
  3. Disabled component added (non-PlayerTag entity)
  4. Entity removed from ALL queries
  → AI systems never process this entity again
  → Health bar disappears (EnemyHealthBarBridgeSystem no longer matches)
  → No cleanup needed in AI systems
```

### Trace 3: Player Runs Away (Leash)

```
Player moves beyond LeashDistance (50m)
LeashSystem: clears all ThreatEntry buffer, AggroState.IsAggroed = false

Next frame: AIStateTransitionSystem reads IsAggroed=false
  → CurrentState = ReturnHome
  AIReturnHomeBehaviorSystem: MoveTowardsAbility.TargetPosition = SpawnPosition
  BoxingJoe walks home

On arrival (distance < 1.5m):
  AIStateTransitionSystem → CurrentState = Idle
  AIIdleBehaviorSystem → stops movement
  BoxingJoe resumes idle/patrol cycle
```

### Trace 4: Multiple BoxingJoes — Pack Aggro

```
BoxingJoe_A detects player → ThreatFromVisionSystem → IsAggroed = true
AggroShareSystem: nearby allies within AggroShareRadius (20m) get 50% threat
  → BoxingJoe_B, BoxingJoe_C get ThreatEntry for player
  → AggroTargetSelectorSystem → all three IsAggroed = true

Next frame: All three AIStateTransitionSystem → Combat
  All three AICombatBehaviorSystem → Approach (chase player simultaneously)
  First to reach MeleeRange starts attacking
  Others attack independently (no turn-taking in Phase 1)
```

### Trace 5: Target Dies Mid-Attack

```
BoxingJoe in WindUp phase (0.4s telegraph)
Frame N: Player dies → Entity gets Disabled or DeathState changes
Frame N+1: AggroTargetSelectorSystem clears target → IsAggroed = false
Frame N+2: AIAttackState.Phase is still WindUp (guard prevents state transition)
           WindUp completes → Active phase
           Active phase: target LocalTransform lookup fails → DamageDealt stays false
           Active timer completes → Recovery → None
           AIAttackState.Phase = None → guard releases
Frame N+3: AIStateTransitionSystem: IsAggroed=false, Phase=None → ReturnHome
           BoxingJoe returns to spawn
```

---

## Critical Safety Rules

- **NEVER** modify `DamageApplySystem.cs` (Burst-compiled, server-only, ghost-aware)
- **NEVER** create `IBufferElementData` on ghost-replicated entities
- All AI systems: `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- PendingCombatHit with `DamagePreApplied = false` — let existing pipeline handle health reduction
- `MoveTowardsAbility` is ghost-replicated `PrefabType.All` — safe to write server-side
- Don't write `TargetData` directly — aggro system owns it via `AggroTargetSelectorSystem`
- Don't store managed references in `ISystem` structs (learned from EPIC 15.30 health bar caching failure)

---

## Known Limitations (Phase 1)

1. **No animations** — BoxingJoe moves and attacks without visual animation feedback. Phase 2 adds `EnemyAnimationState` + animation bridge.
2. **No CircleStrafe** — When attack is on cooldown at melee range, AI stands facing target. Phase 2 adds lateral movement.
3. **No Flee behavior** — `FleeHealthPercent` config exists but is not read. Phase 2 implements flee state.
4. **No hit reactions** — BoxingJoe takes damage without stagger/flinch. Phase 2 adds stagger state + animation.
5. **No attack throttling** — Multiple BoxingJoes can attack the same target simultaneously. Phase 3 adds group coordination.
6. **No ranged attacks** — Only melee. Phase 3 adds projectile spawning for ranged archetypes.
7. **No Investigate state** — When AlertState = SUSPICIOUS but not yet aggroed, AI doesn't move to AlertPosition. Phase 2.
8. **One-frame perception latency** — AI reads previous frame's aggro data. Imperceptible in practice.
9. **Approximate hit point** — PendingCombatHit uses target position, not physics raycast. Acceptable for melee; ranged will need proper raycasts.

---

## Future Phases (NOT in this implementation)

| Phase | EPIC | Features |
|-------|------|----------|
| **Phase 2** | 15.32 | EnemyAnimationState + animation bridge, ScriptableObject enemy profiles, CircleStrafe + Retreat sub-states, Flee behavior, Investigate state |
| **Phase 3** | 15.33 | Ranged enemy (projectile spawning), hit reactions/stagger, group coordination (attack throttling, surrounding) |
| **Phase 4** | TBD | Swarm system (flow fields, steering behaviors, simplified 3-state brain for 200+ entities) |
| **Phase 5** | TBD | Boss framework (phased encounters, scripted sequences, arena awareness, unique attack patterns) |

---

## Key Files Referenced

| File | Role |
|------|------|
| `Assets/Scripts/Aggro/Authoring/AggroAuthoring.cs` | Baker pattern reference |
| `Assets/Scripts/Aggro/Components/AggroState.cs` | `IsAggroed`, `CurrentThreatLeader` |
| `Assets/Scripts/Aggro/Components/SpawnPosition.cs` | Home position for leashing |
| `Assets/Scripts/Aggro/Systems/AggroTargetSelectorSystem.cs` | Writes `TargetData` (upstream, LateSimulationSystemGroup) |
| `Assets/Scripts/Aggro/Systems/AggroCombatStateIntegration.cs` | Aggro → CombatState (LateSimulationSystemGroup) |
| `Assets/Scripts/Aggro/Systems/LeashSystem.cs` | Clears aggro on distance |
| `Assets/Scripts/Combat/Systems/CombatResolutionSystem.cs` | `PendingCombatHit` definition + resolution pipeline |
| `Assets/Scripts/Combat/Resolvers/CombatContext.cs` | `WeaponStats`, `StatBlock`, `CombatContext` definitions |
| `Assets/Scripts/Combat/Resolvers/CombatResolverType.cs` | `CombatResolverType.Hybrid` |
| `Assets/Scripts/Player/Abilities/MovementPolishComponents.cs` | `MoveTowardsAbility` definition (line 224) |
| `Assets/Scripts/Player/Systems/Abilities/MoveTowardsSystem.cs` | Movement system (BUG on line 22) |
| `Assets/Scripts/Targeting/TargetData.cs` | AI reads `TargetEntity` from aggro |
| `Assets/Scripts/Combat/Components/CombatStateComponents.cs` | `CombatState`, `EnteredCombatTag` |
| `Assets/Scripts/Player/Components/DeathState.cs` | Death lifecycle (AI death = Disabled) |
