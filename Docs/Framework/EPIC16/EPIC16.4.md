# EPIC 16.4: Comprehensive Aggro & Threat Framework

**Status:** Complete
**Last Updated:** February 14, 2026
**Priority:** Critical (AI Core)
**Dependencies:**
- EPIC 15.19 (Aggro/Threat Foundation — complete)
- EPIC 15.28 (Unified Combat Resolution Pipeline — complete)
- EPIC 15.31 (Enemy AI Brain — Vertical Slice — complete)
- EPIC 15.32 (Enemy Ability & Encounter Framework — complete)
- EPIC 16.3 (Enemy Death Lifecycle & Corpse Management — complete)

**Feature:** AAA-quality, genre-agnostic aggro framework that fixes broken damage-to-threat pipelines, adds sound-based hearing propagation, proximity body pulls, social/group aggro (linked pulls, call-for-help, ally death reactions, defender aggro), threat manipulation (fixate, encounter triggers, healing threat), 7-mode target selection, 5-level stealth alert system with guard communication and body discovery, and comprehensive debug tooling. All new features default to disabled for full backward compatibility.

> **Code references:** Internal code comments reference this work as `EPIC 15.33`.

---

## Problem Statement

Enemies only aggro when they visually detect the player. Shooting an enemy from behind deals damage but generates no threat — the enemy continues patrolling. Additionally, the hearing pipeline exists but nothing emits sound events, social aggro is limited to a one-shot share on initial detection, and there is no proximity pull, no threat manipulation for encounters, no target selection variety, and no stealth alert cascade.

```
BUILT (15.19)                              MISSING
──────────────                             ───────
Vision-based threat (ThreatFromVisionSystem) ✓     Damage → Threat bridge (both pipelines)
ThreatEntry buffer ✓                               Sound emission for weapons/explosions
HearingEvent buffer ✓                              Nothing emits HearingEvents
AggroShareSystem (one-shot) ✓                      Ongoing call-for-help, linked pulls
AlertState (IDLE/SUSPICIOUS/COMBAT) ✓              CURIOUS/SEARCHING levels, 5-level model
Highest-threat target selection ✓                  Multiple selection modes (7 total)
ThreatDecaySystem ✓                                Proximity threat, body pull
LeashSystem ✓                                      Social/group aggro framework
ThreatModifierEvent ✓                              ThreatFixate, encounter integration
No debug gizmos                                    Scene-view aggro visualization
```

---

## Root Cause Analysis

### 1. Damage → Threat Disconnect

Two independent failures:

**CRE path broken:** `ThreatFromDamageSystem` reads `CombatResultEvent` but `CRE.TargetEntity` points to the CHILD entity (via HitboxOwnerLink redirect). The CHILD entity does NOT have a `ThreatEntry` buffer — that lives on ROOT (baked by `AggroAuthoring`). The `HasBuffer<ThreatEntry>(targetEntity)` check always fails.

**DamageEvent path missing:** Hitscan/explosion damage flows through `DamageEvent` → `SimpleDamageApplySystem`, which processes the damage but never creates CombatResultEvents. No system bridges `DamageEvent` to the threat table.

```
HITSCAN HIT
  ↓
WeaponFireSystem → DamageEvent on CHILD entity
  ↓
SimpleDamageApplySystem → applies damage, clears buffer
  ↓
(nothing) → NO threat generated ← ROOT CAUSE
```

### 2. Silent Hearing Pipeline

`HearingDetectionSystem` + `HearingEvent` buffer exist and function correctly. But nothing in the project creates `HearingEvent` entries — no weapon sound emission, no explosion sound emission. The hearing system processes an always-empty buffer.

### 3. One-Shot Social Aggro

`AggroShareSystem` fires once when an entity transitions to aggroed. No ongoing call-for-help, no linked encounter pulls, no ally death reactions, no pack hierarchy, no guard communication, no body discovery.

---

## Architecture Decision

### Why Separate SocialAggroConfig (not extending AggroConfig)

| Approach | Pros | Cons |
|----------|------|------|
| **Add social fields to AggroConfig** | Single component, simple queries | All enemies pay archetype cost for social fields, even solo enemies |
| **Separate SocialAggroConfig** | Solo enemies have smaller archetype, systems skip non-social enemies via WithAll filter | Two components to manage, authoring split |

**Decision:** Separate `IComponentData`. Solo enemies (the majority) don't carry social data. Social systems use `WithAll<SocialAggroConfig>` to skip irrelevant entities entirely. Matches AAA practice of composable behavior via component presence.

### Why Request-Entity Pattern for Sound (not buffers on player)

Player entity archetype is near the 16KB chunk limit. Adding `SoundEmissionBuffer` would risk exceeding it. Instead, source systems create transient entities with `SoundEventRequest` (IComponentData). `SoundDistributionSystem` reads all requests, distributes `HearingEvent` to nearby AI, then destroys the request entities. Pattern matches existing `PendingCombatHit`.

### Why IEnableableComponent for ThreatFixate (not add/remove)

Encounter triggers need to enable/disable fixate at runtime without structural changes. `IEnableableComponent` avoids chunk migration, is Burst-compatible, and can be baked disabled by `AggroAuthoring` so all entities are pre-wired.

---

## Architecture Overview

```
┌───────────────────────────────────────────────────────────────────────────┐
│                     THREAT SOURCE LAYER                                    │
│                                                                            │
│  DamageSystemGroup (PredictedFixedStep):                                  │
│    ThreatFromDamageEventSystem → reads DamageEvent BEFORE clear           │
│                                                                            │
│  SimulationSystemGroup:                                                    │
│    ThreatFromVisionSystem     → from DetectionSystem SeenTargets          │
│    ThreatFromProximitySystem  → 360° no-LOS body pull                     │
│    ThreatFromDamageSystem     → from CombatResultEvent (CRE path)         │
│    ThreatFromHealingSystem    → healer threat from HealEvent              │
│                                                                            │
│  Sound Pipeline:                                                           │
│    WeaponSoundEmitterSystem ─┐                                             │
│    ExplosionSoundEmitterSystem─→ SoundDistributionSystem → HearingEvent   │
│                                       ↓                                    │
│                              HearingDetectionSystem → ThreatEntry         │
├───────────────────────────────────────────────────────────────────────────┤
│                     SOCIAL / GROUP LAYER                                   │
│                                                                            │
│  AggroShareSystem      → one-shot initial aggro share                     │
│  LinkedPullSystem      → instant EncounterGroupId propagation             │
│  CallForHelpSystem     → ongoing cooldown-based help calls                │
│  AllyDeathReactionSystem → avenge, enrage, flee, pack alpha               │
│  DefenderAggroSystem   → MOBA tower: protect allies                       │
├───────────────────────────────────────────────────────────────────────────┤
│                  THREAT MANIPULATION LAYER                                 │
│                                                                            │
│  ThreatModifierSystem  → ThreatModifierEvent (Add/Multiply/Set)           │
│  ThreatFixateSystem    → forced target lock (IEnableableComponent)         │
│  EncounterTriggerSystem → ThreatWipe / ThreatMultiply / ThreatFixate      │
├───────────────────────────────────────────────────────────────────────────┤
│                   TARGET SELECTION LAYER                                    │
│                                                                            │
│  ThreatDecaySystem     → decay visible/hidden, remove expired             │
│  AggroTargetSelectorSystem → 7 modes + hysteresis + cooldown + fixate     │
│  AggroCombatStateIntegration → write CombatState from aggro               │
├───────────────────────────────────────────────────────────────────────────┤
│                    STEALTH / ALERT LAYER                                   │
│                                                                            │
│  AlertStateSystem          → 5-level: IDLE→CURIOUS→SUSPICIOUS→SEARCHING→COMBAT │
│  GuardCommunicationSystem  → cascading alert wave between guards           │
│  BodyDiscoverySystem       → dead ally detection → SUSPICIOUS alert        │
├───────────────────────────────────────────────────────────────────────────┤
│                      DEBUG LAYER                                           │
│                                                                            │
│  AggroPipelineDebug    → console logging with source flags + alert names  │
│  AggroGizmoRenderer    → scene-view gizmos: threat lines, radii, groups   │
└───────────────────────────────────────────────────────────────────────────┘
```

### Data Flow (One Frame)

```
1. DamageSystemGroup (PredictedFixedStep, Server|Local):
   a. ThreatFromDamageEventSystem reads DamageEvent buffers on CHILD entities
      → resolves CHILD→ROOT via DamageableLink/Parent walk
      → adds damage * DamageThreatMultiplier to ROOT's ThreatEntry buffer
      → sets SourceFlags |= Damage, increments DamageThreat
   b. SimpleDamageApplySystem processes DamageEvent → clears buffers (existing)

2. SimulationSystemGroup (Server|Local):
   a. AlertStateSystem evaluates aggro + threat state → sets 5-level alert
   b. DetectionSystem runs vision cone queries (existing)
   c. ThreatFromVisionSystem → SourceFlags |= Vision
   d. ThreatFromProximitySystem → 360° body pull → SourceFlags |= Proximity
   e. WeaponSoundEmitterSystem detects WeaponFireState rising edge → creates SoundEventRequest entity
   f. ExplosionSoundEmitterSystem reads ModifierExplosionRequest → creates SoundEventRequest entity
   g. SoundDistributionSystem reads all SoundEventRequest entities, distributes HearingEvents within range, destroys requests
   h. HearingDetectionSystem reads HearingEvent buffers → ThreatEntry → SourceFlags |= Hearing
   i. AggroShareSystem (initial aggro share) → SourceFlags |= Social
   j. LinkedPullSystem → instant group-wide threat propagation
   k. CallForHelpSystem → ongoing call-for-help on cooldown
   l. AllyDeathReactionSystem → avenge/enrage/flee reactions
   m. DefenderAggroSystem → boost threat for attackers of allies
   n. GuardCommunicationSystem → cascading alert wave
   o. BodyDiscoverySystem → detect dead allies, raise alert
   p. ThreatFromDamageSystem (CRE path) → SourceFlags |= Damage
   q. ThreatFromHealingSystem → healer threat → SourceFlags |= Healing
   r. ThreatModifierSystem → Add/Multiply/Set → SourceFlags |= Taunt
   s. ThreatFixateSystem → countdown timer, disable when expired

3. LateSimulationSystemGroup:
   a. ThreatDecaySystem → decay visible/hidden rates, remove expired entries
   b. AggroTargetSelectorSystem → dispatch on SelectionMode (7 modes)
      → ThreatFixate override → hysteresis → TargetSwitchCooldown → RandomSwitchChance
      → writes TargetData + HasAggroOn
   c. AggroCombatStateIntegration → CombatState from aggro (existing)
```

---

## Phase 0: Fix Broken Pipelines (Critical)

### 0.1 Fix ThreatFromDamageSystem Entity Resolution
- [x] Add `ComponentLookup<DamageableLink>` and `ComponentLookup<Parent>` lookups
- [x] Create `ResolveToThreatHolder` method that walks CHILD→ROOT via DamageableLink, then Parent (up to 3 levels)
- [x] When `threatBufferLookup.HasBuffer(targetEntity)` fails, resolve and retry on ROOT
- [x] Set `SourceFlags |= Damage` and increment `DamageThreat` on all threat writes

**File:** `Assets/Scripts/Aggro/Systems/ThreatFromDamageSystem.cs` (MODIFY)

### 0.2 Create ThreatFromDamageEventSystem
- [x] New Burst ISystem in `DamageSystemGroup`, `[UpdateBefore(typeof(SimpleDamageApplySystem))]`
- [x] `CompleteDependency()` at top for job safety (SimpleDamageApplySystem schedules async jobs)
- [x] Read `DamageEvent` buffers BEFORE `SimpleDamageApplySystem` clears them
- [x] CHILD→ROOT resolution via `DamageableLink`/`Parent` walk to find `ThreatEntry` buffer
- [x] For each DamageEvent with valid SourceEntity: add `Amount * DamageThreatMultiplier` to threat table
- [x] Set `ThreatSourceFlags.Damage`, `IsCurrentlyVisible = true`, `TimeSinceVisible = 0`

**File:** `Assets/Scripts/Aggro/Systems/ThreatFromDamageEventSystem.cs` (NEW)

### 0.3 Sound Emission Pipeline
- [x] Create `SoundEventRequest` IComponentData with Position, SourceEntity, Loudness, MaxRange, Category
- [x] Create `SoundCategory` enum: Gunfire=0, Explosion=1, Movement=2, Combat=3, Ability=4, Environmental=5
- [x] Create `SoundDistributionSystem` — reads all SoundEventRequest entities, distributes HearingEvents to AI within `MaxRange * Loudness`, destroys request entities
- [x] Create `WeaponSoundEmitterSystem` — detects `WeaponFireState.IsFiring` rising edge (TimeSinceLastShot < dt*1.5), creates SoundEventRequest (Loudness=1.5, MaxRange=80, Category=Gunfire)
- [x] Create `ExplosionSoundEmitterSystem` — reads `ModifierExplosionRequest`, creates SoundEventRequest (Loudness scaled by radius, MaxRange=max(radius*4, 120), Category=Explosion)

**Files:**
- `Assets/Scripts/Aggro/Components/SoundEventRequest.cs` (NEW)
- `Assets/Scripts/Aggro/Systems/SoundDistributionSystem.cs` (NEW)
- `Assets/Scripts/Aggro/Systems/WeaponSoundEmitterSystem.cs` (NEW)
- `Assets/Scripts/Aggro/Systems/ExplosionSoundEmitterSystem.cs` (NEW)

### Key Data Structures

```
SoundEventRequest : IComponentData
{
    float3 Position;          // World position of the sound source
    Entity SourceEntity;      // Who made the sound (for threat attribution)
    float Loudness;           // 0.3=footstep, 1.0=combat, 1.5=gunfire, 3.0=explosion
    float MaxRange;           // Base audible range in meters
    SoundCategory Category;   // For AI filtering (some enemies ignore certain sounds)
}
```

---

## Phase 1: Threat Source Categorization

### 1.1 ThreatSourceFlags Enum
- [x] Create `[Flags] enum ThreatSourceFlags : byte` with 7 source types
- [x] Each flag represents a distinct way threat was generated for provenance tracking

**File:** `Assets/Scripts/Aggro/Components/ThreatSourceFlags.cs` (NEW)

```
[Flags] enum ThreatSourceFlags : byte
{
    None        = 0,
    Damage      = 1 << 0,   // Took damage from this source
    Vision      = 1 << 1,   // Currently/recently seen
    Hearing     = 1 << 2,   // Heard a sound from this source
    Social      = 1 << 3,   // Allied unit shared this threat
    Proximity   = 1 << 4,   // Body pull — too close
    Taunt       = 1 << 5,   // Forced via ability (ThreatModifierEvent)
    Healing     = 1 << 6,   // Healed an entity we're fighting
}
```

### 1.2 Extend ThreatEntry
- [x] Add `ThreatSourceFlags SourceFlags` field (bitmask, OR'd by contributing systems)
- [x] Add `float DamageThreat` field (threat specifically from damage, for analytics/UI)
- [x] Backward compatible: default 0/None doesn't change existing behavior

**File:** `Assets/Scripts/Aggro/Components/ThreatEntry.cs` (MODIFY)

### 1.3 Update All Threat-Writing Systems
- [x] `ThreatFromVisionSystem` → `SourceFlags |= Vision`
- [x] `ThreatFromDamageSystem` / `ThreatFromDamageEventSystem` → `SourceFlags |= Damage`, increment `DamageThreat`
- [x] `HearingDetectionSystem` → `SourceFlags |= Hearing`
- [x] `AggroShareSystem` → `SourceFlags |= Social`
- [x] `ThreatModifierSystem` → `SourceFlags |= Taunt`

**Files (MODIFY):**
- `Assets/Scripts/Aggro/Systems/ThreatFromVisionSystem.cs`
- `Assets/Scripts/Aggro/Systems/HearingDetectionSystem.cs`
- `Assets/Scripts/Aggro/Systems/AggroShareSystem.cs`
- `Assets/Scripts/Aggro/Systems/ThreatModifierSystem.cs`

---

## Phase 2: Proximity Threat (Body Pull)

### 2.1 Extend AggroConfig
- [x] Add `float ProximityThreatRadius` (0 = disabled, default 0)
- [x] Add `float ProximityThreatPerSecond` (default 5.0)

**File:** `Assets/Scripts/Aggro/Components/AggroConfig.cs` (MODIFY)

### 2.2 Create ThreatFromProximitySystem
- [x] Burst ISystem, `[UpdateAfter(typeof(DetectionSystem))]`, Server|Local
- [x] For each AI with ProximityThreatRadius > 0: query all `Detectable` entities within radius
- [x] 360-degree, no line-of-sight required (through-wall for close range)
- [x] `Detectable.StealthMultiplier` reduces effective radius
- [x] Adds `ProximityThreatPerSecond * dt` per frame, sets `SourceFlags |= Proximity`

**File:** `Assets/Scripts/Aggro/Systems/ThreatFromProximitySystem.cs` (NEW)

### 2.3 Update AggroAuthoring
- [x] Add `[Header("Proximity")]` section with ProximityThreatRadius, ProximityThreatPerSecond fields
- [x] Baker bakes new AggroConfig fields

**File:** `Assets/Scripts/Aggro/Authoring/AggroAuthoring.cs` (MODIFY)

---

## Phase 3: Social/Group Aggro Framework

### 3.1 Social Aggro Components
- [x] Create `SocialAggroFlags` — `[Flags] enum : ushort` with 11 behavior flags
- [x] Create `PackRole` enum — None=0, Alpha=1, Member=2
- [x] Create `SocialAggroConfig` IComponentData with group ID, flags, call-for-help settings, ally reaction settings, pack role
- [x] Create `SocialAggroState` IComponentData with CallForHelpTimer, AllyDeathCount, LastDeadAlly, RageTimer

**Files (NEW):**
- `Assets/Scripts/Aggro/Components/SocialAggroFlags.cs`
- `Assets/Scripts/Aggro/Components/SocialAggroConfig.cs`
- `Assets/Scripts/Aggro/Components/SocialAggroState.cs`

```
[Flags] enum SocialAggroFlags : ushort
{
    None                = 0,
    LinkedPull          = 1 << 0,    // Aggro one = aggro all in EncounterGroupId
    CallForHelp         = 1 << 1,    // Emit call-for-help when taking damage
    RespondToHelp       = 1 << 2,    // React to nearby ally calls
    ShareDamageInfo     = 1 << 3,    // Continuously share damage threat
    AllyDeathAvenge     = 1 << 4,    // Bonus threat on killer of ally
    AllyDeathEnrage     = 1 << 5,    // Multiply all threat when ally dies
    AllyDeathFlee       = 1 << 6,    // Flee when ally/alpha dies
    PackBehavior        = 1 << 7,    // Follow alpha, react to alpha death
    GuardCommunication  = 1 << 8,    // Relay alert states to other guards
    BodyDiscovery       = 1 << 9,    // Discover dead allies, raise alert
    DefenderAggro       = 1 << 10,   // Prioritize attackers of allied entities
}
```

| SocialAggroConfig Field | Type | Default | Purpose |
|-------------------------|------|---------|---------|
| EncounterGroupId | int | 0 | Group ID for linked pull (0 = no group) |
| Flags | SocialAggroFlags | None | Bitmask of enabled behaviors |
| CallForHelpRadius | float | 25 | Radius for call-for-help |
| CallForHelpCooldown | float | 3 | Seconds between emissions |
| CallForHelpThreatShare | float | 0.5 | Fraction of threat shared |
| AllyDeathThreatBonus | float | 50 | Flat threat on killer |
| AllyDeathRageMultiplier | float | 1.0 | Multiply all threat on ally death |
| AllyDamagedThreatShare | float | 0 | Fraction of damage threat shared |
| Role | PackRole | None | Pack hierarchy role |

### 3.2 Social Aggro Systems
- [x] **LinkedPullSystem** — when entity with EncounterGroupId > 0 transitions to aggroed, inject threat into ALL entities with same group ID. Instant full-threat propagation.
- [x] **CallForHelpSystem** — when aggroed entity with CallForHelp flag has Damage-flagged ThreatEntry, and cooldown expired: share threat leader to nearby entities with RespondToHelp flag. Sets SourceFlags.Social.
- [x] **AllyDeathReactionSystem** — detects death transitions (DeathState.Phase != Alive). AllyDeathAvenge: bonus on killer. AllyDeathEnrage: multiply all threat. PackBehavior + dead Alpha: members enrage.
- [x] **DefenderAggroSystem** — entities with DefenderAggro flag monitor nearby allies for damage, give massive threat boost (100) to attackers.

**Files (NEW):**
- `Assets/Scripts/Aggro/Systems/LinkedPullSystem.cs`
- `Assets/Scripts/Aggro/Systems/CallForHelpSystem.cs`
- `Assets/Scripts/Aggro/Systems/AllyDeathReactionSystem.cs`
- `Assets/Scripts/Aggro/Systems/DefenderAggroSystem.cs`

### 3.3 Social Aggro Authoring
- [x] Create `SocialAggroAuthoring` MonoBehaviour with inspector sections: Group, Call For Help, Ally Reactions, Pack, Stealth, Defender
- [x] Baker builds `SocialAggroFlags` from boolean toggles, bakes `SocialAggroConfig` + `SocialAggroState`
- [x] Separate from `AggroAuthoring` — only add to enemies that need social behavior

**File:** `Assets/Scripts/Aggro/Authoring/SocialAggroAuthoring.cs` (NEW)

### 3.4 Update AggroShareSystem
- [x] Add `SourceFlags |= Social` to shared ThreatEntry entries
- [x] No functional change — remains one-shot initial-aggro share. CallForHelpSystem handles ongoing.

**File:** `Assets/Scripts/Aggro/Systems/AggroShareSystem.cs` (MODIFY)

---

## Phase 4: Advanced Threat Manipulation

### 4.1 Threat Fixate
- [x] Create `ThreatFixate` — IComponentData + IEnableableComponent with FixatedTarget, Duration, Timer
- [x] Create `ThreatFixateSystem` — counts down Timer, disables component when expired
- [x] Baked disabled by `AggroAuthoring` — encounter triggers enable at runtime
- [x] `AggroTargetSelectorSystem` checks ThreatFixate first: if enabled, always returns FixatedTarget

**Files (NEW):**
- `Assets/Scripts/Aggro/Components/ThreatFixate.cs`
- `Assets/Scripts/Aggro/Systems/ThreatFixateSystem.cs`

### 4.2 Encounter Trigger Integration
- [x] Add `TriggerActionType` values to `EncounterTrigger`:
  - `ThreatWipeAll = 13` — clear ALL ThreatEntry buffers + reset AggroState
  - `ThreatMultiplyAll = 14` — multiply all ThreatValue entries by ActionValue
  - `ThreatFixateRandom = 15` — pick random player from threat table, enable ThreatFixate for ActionValue seconds
- [x] Add action handlers in `EncounterTriggerSystem` for all three types

**Files (MODIFY):**
- `Assets/Scripts/AI/Components/EncounterTrigger.cs`
- `Assets/Scripts/AI/Systems/EncounterTriggerSystem.cs`

### 4.3 Healing Threat
- [x] Create `HealingThreatConfig` IComponentData — ThreatPerHealPoint (default 0.5), SplitAcrossEnemies (default true)
- [x] Create `ThreatFromHealingSystem` — reads HealEvent buffers, finds enemies fighting the healed entity, adds heal threat for healer, sets SourceFlags |= Healing

**Files (NEW):**
- `Assets/Scripts/Aggro/Components/HealingThreatConfig.cs`
- `Assets/Scripts/Aggro/Systems/ThreatFromHealingSystem.cs`

### 4.4 Update AggroAuthoring for ThreatFixate
- [x] Baker adds `ThreatFixate` component (disabled via `SetComponentEnabled<ThreatFixate>(entity, false)`)
- [x] Encounter triggers can enable at runtime without structural changes

**File:** `Assets/Scripts/Aggro/Authoring/AggroAuthoring.cs` (MODIFY)

---

## Phase 5: Advanced Target Selection

### 5.1 TargetSelectionMode Enum
- [x] Create enum with 7 genre-spanning modes

**File:** `Assets/Scripts/Aggro/Components/TargetSelectionMode.cs` (NEW)

```
enum TargetSelectionMode : byte
{
    HighestThreat  = 0,   // Classic MMO (default, backward compatible)
    WeightedScore  = 1,   // Composite: threat*W + distance*W + health*W + recency*W
    Nearest        = 2,   // Souls-like: always target closest in threat table
    LastAttacker   = 3,   // Shooter: whoever hit you most recently
    LowestHealth   = 4,   // ARPG: focus down weakest
    Random         = 5,   // Swarm: random target from table
    Defender       = 6,   // MOBA tower: priority list (attacking ally > nearest > threat)
}
```

### 5.2 Extend AggroConfig for Target Selection
- [x] Add `TargetSelectionMode SelectionMode` (default HighestThreat)
- [x] Add `DistanceWeight`, `HealthWeight`, `RecencyWeight` (all default 0, for WeightedScore mode)
- [x] Add `TargetSwitchCooldown` (default 0, minimum seconds between switches)
- [x] Add `RandomSwitchChance` (default 0, per-second probability of random switch)

**File:** `Assets/Scripts/Aggro/Components/AggroConfig.cs` (MODIFY)

### 5.3 Extend AggroState
- [x] Add `float TimeSinceLastSwitch` field for TargetSwitchCooldown tracking

**File:** `Assets/Scripts/Aggro/Components/AggroState.cs` (MODIFY)

### 5.4 Refactor AggroTargetSelectorSystem
- [x] ThreatFixate override check at top of selection pipeline
- [x] RandomSwitchChance roll before normal selection (per-second probability, picks non-current entry)
- [x] 7-mode dispatch via switch on `SelectionMode`:
  - `SelectHighestThreat` — score = ThreatValue
  - `SelectNearest` — score = -distance (using LocalTransform positions)
  - `SelectLastAttacker` — score = -TimeSinceVisible
  - `SelectLowestHealth` — score = -health% (via `ComponentLookup<Health>`)
  - `SelectRandom` — random index from threat table
  - `SelectWeightedScore` — composite: `threat*threatW + (1/dist)*distW*100 + (1-hp%)*healthW*100 + recency*recW*100`
  - `Defender` — falls back to HighestThreat (DefenderAggroSystem already boosted threat)
- [x] Hysteresis only for HighestThreat, WeightedScore, Defender modes
- [x] TargetSwitchCooldown enforcement (skipped if current leader gone from table)
- [x] `ComponentLookup<Health>` for LowestHealth and WeightedScore modes

**File:** `Assets/Scripts/Aggro/Systems/AggroTargetSelectorSystem.cs` (MODIFY)

### 5.5 Update AggroAuthoring for Target Selection
- [x] Add `[Header("Advanced Target Selection")]` inspector section
- [x] SelectionMode dropdown, weight sliders, switch cooldown, random chance
- [x] Baker bakes all new AggroConfig fields

**File:** `Assets/Scripts/Aggro/Authoring/AggroAuthoring.cs` (MODIFY)

---

## Phase 6: Stealth Extensions

### 6.1 Extended Alert Levels (5-State Model)
- [x] Expand from 3-level (IDLE/SUSPICIOUS/COMBAT) to 5-level model
- [x] Add `InvestigatePosition`, `SearchDuration`, `SearchTimer`, `HasInvestigated` fields
- [x] Backward compatible: `IDLE=0` unchanged, systems checking `AlertLevel > IDLE` still work

**File:** `Assets/Scripts/Aggro/Components/AlertState.cs` (MODIFY)

```
IDLE(0)      → Normal state, standard detection range
CURIOUS(1)   → Faint signal (decaying threats, distant hearing)
SUSPICIOUS(2)→ Strong signal (recent hearing, social aggro, proximity)
SEARCHING(3) → Aggroed but lost sight, investigating last known position
COMBAT(4)    → Active engagement, target visible
```

| De-escalation Timer | Duration | Description |
|--------------------|----------|-------------|
| COMBAT → SEARCHING | 3s | Lost sight, start searching quickly |
| SEARCHING → SUSPICIOUS | 10s | Searched area, heightened awareness |
| SUSPICIOUS → CURIOUS | 8s | Fading awareness |
| CURIOUS → IDLE | 5s | Return to normal |

### 6.2 Update AlertStateSystem
- [x] COMBAT if aggroed + current leader visible in threat table
- [x] SEARCHING if aggroed but leader not visible (lost sight)
- [x] SUSPICIOUS if has threats with recent hearing/social/proximity flags (TimeSinceVisible < 5s)
- [x] CURIOUS if has threats but only from old/faint sources
- [x] IDLE if no threats
- [x] Set `InvestigatePosition` from leader's LastKnownPosition when entering SEARCHING
- [x] Track `SearchTimer` in SEARCHING state for AI behavior systems
- [x] Escalation immediate, de-escalation steps down one level at a time with timers

**File:** `Assets/Scripts/Aggro/Systems/AlertStateSystem.cs` (MODIFY)

### 6.3 GuardCommunicationSystem
- [x] Entities with `GuardCommunication` flag propagate alert levels to nearby guards
- [x] Guard at COMBAT → nearby guards become SEARCHING
- [x] Guard at SEARCHING → nearby guards become SUSPICIOUS
- [x] Guard at SUSPICIOUS → nearby guards become CURIOUS
- [x] Communication range = `CallForHelpRadius` from `SocialAggroConfig`
- [x] Uses EntityQuery → ToComponentDataArray → for loop → SetComponentData pattern (avoids nested SystemAPI.Query)
- [x] Creates cascading alert wave across guard network within a single frame

**File:** `Assets/Scripts/Aggro/Systems/GuardCommunicationSystem.cs` (NEW)

### 6.4 BodyDiscoverySystem
- [x] Entities with `BodyDiscovery` flag detect dead allies (DeathState.Phase != Alive) within `CallForHelpRadius`
- [x] Only triggers when guard is at IDLE or CURIOUS level (already-alerted guards skip)
- [x] Raises alert to SUSPICIOUS, sets InvestigatePosition to corpse location
- [x] Chains with GuardCommunicationSystem for alert relay
- [x] One discovery per frame per guard (prevents redundant processing)

**File:** `Assets/Scripts/Aggro/Systems/BodyDiscoverySystem.cs` (NEW)

---

## Phase 7: Authoring & Debug Polish

### 7.1 AggroPipelineDebug Enhancements
- [x] Log `ThreatSourceFlags` breakdown per entry (formatted as `[Dmg|Vis|Hear|...]`)
- [x] Log `DamageThreat` (damage-specific threat for analytics)
- [x] Log `SelectionMode` for first entity
- [x] Log alert state with 5-level names (`IDLE`, `CURIOUS`, `SUSPICIOUS`, `SEARCHING`, `COMBAT`)
- [x] Log `SearchTimer` for SEARCHING state tracking
- [x] Log `socialCount` — number of entities with SocialAggroConfig

**File:** `Assets/Scripts/Aggro/Debug/AggroPipelineDebug.cs` (MODIFY)

### 7.2 AggroGizmoRenderer (Scene-View Debug)
- [x] Create MonoBehaviour that draws gizmos in Scene view during play mode
- [x] **Threat lines**: colored by ThreatSourceFlags priority (red=damage, blue=vision, yellow=hearing, green=social, purple=proximity, magenta=taunt, cyan=healing)
- [x] **Threat spheres**: wire sphere at target end, sized by ThreatValue
- [x] **Proximity radius**: purple wireframe sphere (ProximityThreatRadius)
- [x] **Aggro share radius**: cyan wireframe sphere (AggroShareRadius)
- [x] **Call-for-help radius**: orange wireframe sphere (CallForHelpRadius)
- [x] **Alert level capsule**: colored wire capsule on each AI entity (white→yellow→orange→dark orange→red)
- [x] **LinkedPull group lines**: yellow lines between entities with same EncounterGroupId
- [x] Toggle flags for each visualization category

**File:** `Assets/Scripts/Aggro/Debug/AggroGizmoRenderer.cs` (NEW)

---

## System Execution Order (Final)

```
DamageSystemGroup (PredictedFixedStep, Server|Local):
  ThreatFromDamageEventSystem  [NEW, UpdateBefore SimpleDamage]
  SimpleDamageApplySystem      [existing]

SimulationSystemGroup (Server|Local):
  AlertStateSystem             [before DetectionSystem]
  DetectionSystem              [existing]
  ThreatFromVisionSystem       [after DetectionSystem]
  ThreatFromProximitySystem    [NEW, after DetectionSystem]
  WeaponSoundEmitterSystem     [NEW]
  ExplosionSoundEmitterSystem  [NEW]
  SoundDistributionSystem      [NEW, after emitters]
  HearingDetectionSystem       [after SoundDistributionSystem]
  AggroShareSystem             [after ThreatFromVisionSystem]
  LinkedPullSystem             [NEW, after AggroShareSystem]
  CallForHelpSystem            [NEW, after LinkedPullSystem]
  AllyDeathReactionSystem      [NEW, after CallForHelpSystem]
  DefenderAggroSystem          [NEW, after AllyDeathReactionSystem]
  GuardCommunicationSystem     [NEW, after AlertStateSystem]
  BodyDiscoverySystem          [NEW, after GuardCommunicationSystem]
  ThreatFromDamageSystem       [after CombatResolutionSystem]
  ThreatFromHealingSystem      [NEW, after CombatResolutionSystem]
  ThreatModifierSystem         [after ThreatFromDamageSystem]
  ThreatFixateSystem           [NEW, after ThreatModifierSystem]
  LeashSystem                  [after ThreatDecaySystem]

LateSimulationSystemGroup:
  ThreatDecaySystem
  AggroTargetSelectorSystem    [modified, after ThreatDecaySystem]
  AggroCombatStateIntegration  [after AggroTargetSelector]
```

---

## File Summary

### New Files (23)

| # | File | Type | Phase |
|---|------|------|-------|
| 1 | `Aggro/Systems/ThreatFromDamageEventSystem.cs` | ISystem, Burst | 0 |
| 2 | `Aggro/Components/SoundEventRequest.cs` | IComponentData + SoundCategory enum | 0 |
| 3 | `Aggro/Systems/SoundDistributionSystem.cs` | ISystem, Burst | 0 |
| 4 | `Aggro/Systems/WeaponSoundEmitterSystem.cs` | SystemBase (managed) | 0 |
| 5 | `Aggro/Systems/ExplosionSoundEmitterSystem.cs` | SystemBase (managed) | 0 |
| 6 | `Aggro/Components/ThreatSourceFlags.cs` | Flags enum | 1 |
| 7 | `Aggro/Systems/ThreatFromProximitySystem.cs` | ISystem, Burst | 2 |
| 8 | `Aggro/Components/SocialAggroConfig.cs` | IComponentData | 3 |
| 9 | `Aggro/Components/SocialAggroFlags.cs` | Flags enum + PackRole enum | 3 |
| 10 | `Aggro/Components/SocialAggroState.cs` | IComponentData | 3 |
| 11 | `Aggro/Systems/LinkedPullSystem.cs` | ISystem, Burst | 3 |
| 12 | `Aggro/Systems/CallForHelpSystem.cs` | ISystem, Burst | 3 |
| 13 | `Aggro/Systems/AllyDeathReactionSystem.cs` | ISystem, Burst | 3 |
| 14 | `Aggro/Systems/DefenderAggroSystem.cs` | ISystem, Burst | 3 |
| 15 | `Aggro/Authoring/SocialAggroAuthoring.cs` | Baker (MonoBehaviour) | 3 |
| 16 | `Aggro/Components/ThreatFixate.cs` | IComponentData, IEnableableComponent | 4 |
| 17 | `Aggro/Systems/ThreatFixateSystem.cs` | ISystem, Burst | 4 |
| 18 | `Aggro/Components/HealingThreatConfig.cs` | IComponentData | 4 |
| 19 | `Aggro/Systems/ThreatFromHealingSystem.cs` | SystemBase (managed) | 4 |
| 20 | `Aggro/Components/TargetSelectionMode.cs` | Enum | 5 |
| 21 | `Aggro/Systems/GuardCommunicationSystem.cs` | ISystem, Burst | 6 |
| 22 | `Aggro/Systems/BodyDiscoverySystem.cs` | ISystem, Burst | 6 |
| 23 | `Aggro/Debug/AggroGizmoRenderer.cs` | MonoBehaviour | 7 |

### Modified Files (14)

| # | File | Changes | Phase |
|---|------|---------|-------|
| 1 | `Aggro/Systems/ThreatFromDamageSystem.cs` | CHILD→ROOT entity resolution, SourceFlags | 0, 1 |
| 2 | `Aggro/Components/ThreatEntry.cs` | Add SourceFlags + DamageThreat fields | 1 |
| 3 | `Aggro/Systems/ThreatFromVisionSystem.cs` | Set SourceFlags.Vision | 1 |
| 4 | `Aggro/Systems/HearingDetectionSystem.cs` | Set SourceFlags.Hearing | 1 |
| 5 | `Aggro/Systems/AggroShareSystem.cs` | Set SourceFlags.Social | 1 |
| 6 | `Aggro/Systems/ThreatModifierSystem.cs` | Set SourceFlags.Taunt | 1 |
| 7 | `Aggro/Components/AggroConfig.cs` | Add Proximity + TargetSelection fields | 2, 5 |
| 8 | `Aggro/Components/AggroState.cs` | Add TimeSinceLastSwitch | 5 |
| 9 | `Aggro/Authoring/AggroAuthoring.cs` | Add inspector fields + bake ThreatFixate | 2, 4, 5 |
| 10 | `Aggro/Components/AlertState.cs` | 5-level model + InvestigatePosition | 6 |
| 11 | `Aggro/Systems/AlertStateSystem.cs` | 5-state logic with de-escalation timers | 6 |
| 12 | `Aggro/Systems/AggroTargetSelectorSystem.cs` | 7-mode scoring + ThreatFixate + cooldown | 5 |
| 13 | `Aggro/Debug/AggroPipelineDebug.cs` | Source flags, alert names, social count | 7 |
| 14 | `AI/Components/EncounterTrigger.cs` + `AI/Systems/EncounterTriggerSystem.cs` | ThreatWipe/Multiply/Fixate actions | 4 |

---

## Backward Compatibility

All new features default to disabled — existing enemies behave identically without any prefab changes:

| Feature | Default | Effect |
|---------|---------|--------|
| ProximityThreatRadius | 0 | No proximity body pull |
| SelectionMode | HighestThreat (0) | Same as before |
| SocialAggroConfig | Not present | Solo enemies skip all social systems |
| ThreatSourceFlags | None (0) | Flags are additive, old entries work |
| ThreatFixate | IEnableableComponent, baked disabled | No effect until encounter trigger |
| Alert levels | IDLE=0, COMBAT=4 | `AlertLevel > IDLE` still matches all alert states |
| DistanceWeight/HealthWeight/RecencyWeight | 0 | WeightedScore reduces to pure ThreatValue |
| TargetSwitchCooldown | 0 | No cooldown, instant switching |
| RandomSwitchChance | 0 | Deterministic selection |
| HealingThreatConfig | Not present | No healing threat |

---

## Verification

| # | Test | Steps | Expected |
|---|------|-------|----------|
| 1 | Damage aggro | Shoot enemy from behind (not visible) | Enemy turns and attacks you |
| 2 | Sound aggro | Fire weapon near idle enemies (miss all) | Nearby enemies investigate gun position |
| 3 | Proximity pull | Walk close to enemy with ProximityThreatRadius > 0 | Enemy aggros without seeing/hearing you |
| 4 | Linked pull | Attack one enemy in group (same EncounterGroupId) | Entire group aggros instantly |
| 5 | Call for help | Attack enemy with CallForHelp flag | Nearby allies converge on attacker |
| 6 | Ally death avenge | Kill one enemy with AllyDeathAvenge allies nearby | Surviving enemies focus you harder |
| 7 | Threat fixate | Boss encounter trigger fires ThreatFixateRandom | Boss ignores other players for duration |
| 8 | Target mode: Nearest | Set enemy to Nearest mode | Always chases closest, even if distant player deals more damage |
| 9 | Target mode: LowestHealth | Set enemy to LowestHealth mode | Focuses weakest player |
| 10 | Stealth alert cascade | Guard reaches SUSPICIOUS | Nearby guards become CURIOUS |
| 11 | Body discovery | Guard patrols near dead NPC | Guard raises to SUSPICIOUS, sets investigate position |
| 12 | Leash still works | Kite enemy beyond LeashDistance | Enemy drops aggro, returns home |
| 13 | Existing enemies unchanged | Play with BoxingJoe (no SocialAggroAuthoring) | Identical behavior to before |
| 14 | Debug gizmos | Enter play mode with AggroGizmoRenderer in scene | Threat lines, radius spheres, alert colors visible |
| 15 | ThreatWipeAll | Boss phase transition trigger fires wipe | All threat cleared, boss re-evaluates targets |
| 16 | Hysteresis | Two players deal similar damage | Boss doesn't ping-pong between targets |
| 17 | Target switch cooldown | Set TargetSwitchCooldown = 3s | Boss stays on target minimum 3s |

---

## Genre Coverage

| Genre | Target Selection Mode | Social Features | Alert Model |
|-------|----------------------|-----------------|-------------|
| **MMO** | HighestThreat | CallForHelp, LinkedPull, AllyDeathAvenge | COMBAT only |
| **Souls-like** | Nearest | None (solo enemies) | IDLE → COMBAT |
| **Shooter** | LastAttacker | CallForHelp | Full 5-level |
| **ARPG** | LowestHealth or WeightedScore | AllyDeathEnrage, PackBehavior | IDLE → COMBAT |
| **MOBA** | Defender | DefenderAggro | IDLE → COMBAT |
| **Stealth** | HighestThreat | GuardCommunication, BodyDiscovery | Full 5-level |
| **Horde** | Random | LinkedPull | IDLE → COMBAT |
| **Boss Fight** | HighestThreat + ThreatFixate | LinkedPull, AllyDeathEnrage | COMBAT + encounter triggers |

---

## Key Patterns & Lessons

### CHILD→ROOT Entity Resolution
DamageEvent targets CHILD entities (via HitboxOwnerLink redirect), but ThreatEntry buffers live on ROOT entities (baked by AggroAuthoring). All threat-writing systems must resolve CHILD→ROOT via `DamageableLink.DamageableRoot` or `Parent` walk (up to 3 levels).

### Request-Entity Pattern
Sound emission uses transient entities with `SoundEventRequest` rather than buffers on player entities. This avoids the 16KB player archetype limit. Pattern: source system creates entity → distribution system reads all → distributes to AI buffers → destroys request entities.

### O(N^2) Guard Systems
`GuardCommunicationSystem` and `BodyDiscoverySystem` use manual `EntityQuery` → `ToComponentDataArray` → nested for loop → `SetComponentData` pattern. This avoids issues with nested `SystemAPI.Query` calls and is acceptable for N < 100 guard entities.

### Job Safety in DamageSystemGroup
`ThreatFromDamageEventSystem` requires `CompleteDependency()` at top of OnUpdate because `SimpleDamageApplySystem` schedules async jobs writing `DamageEvent` buffers across prediction ticks.
