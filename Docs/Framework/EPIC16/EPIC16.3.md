# EPIC 16.3: Enemy Death Lifecycle & Corpse Management

**Status:** **IMPLEMENTED**
**Priority:** Medium (Performance & Polish)
**Dependencies:**
- `DeathTransitionSystem` (existing)
- `DeathState` / `DeathPhase` (existing)
- `RagdollTransitionSystem` (existing)
- `SimpleDamageApplySystem` (existing)
- `EnemySeparationSystem` (EPIC 15.23)

**Feature:** A multi-phase corpse lifecycle that replaces the current instant-`Disabled` death with configurable ragdoll persistence, component stripping, fade-out, and despawn — allowing games to keep bodies on the floor performantly or clean them up aggressively.

---

## Problem

Currently `DeathTransitionSystem` adds the `Disabled` component to dead non-player entities immediately. This:
- Hides the corpse from **all** systems including rendering — enemies just vanish
- Leaves the entity in memory forever (never destroyed)
- No ragdoll plays for enemies (ragdoll system checks `IsRagdolled` but Disabled removes them from queries first)
- No loot drop window, no death animation, no visual feedback
- Over time, invisible Disabled entities accumulate with all their components still allocated

AAA games use a staged lifecycle: death animation → corpse persistence → component stripping → fade → destroy/recycle.

---

## Current Death Flow (What Exists)

| Step | System | What Happens |
|------|--------|-------------|
| 1 | `SimpleDamageApplySystem` | Health reaches 0 |
| 2 | `DeathTransitionSystem` | `WillDieEvent` fires (cancellable) |
| 3 | `DeathTransitionSystem` | `DeathState.Phase = Downed`, `DiedEvent` fires |
| 4 | `DeathTransitionSystem` | **`Disabled` added immediately** (non-player only) |
| 5 | `RagdollTransitionSystem` | Ragdoll activates (players only — enemies already Disabled) |
| 6 | Kill attribution | `KillCredited` / `AssistCredited` added to attacker |
| 7 | Never | Entity sits in memory with `Disabled` forever |

**Existing components we build on:**
- `DeathState` (DeathPhase: Alive/Downed/Dead/Respawning) — ghost-replicated
- `WillDieEvent` / `DiedEvent` — enableable one-frame events
- `RagdollTransitionSystem` — unparents bones, applies physics, already works for players
- `DeathSpawnElement` buffer — exists in authoring but no system consumes it yet
- `DeathLayerSystem` — exists but logs warning (incomplete collision filter change)

---

## Target Death Flow (What We Build)

| Step | Phase | Duration | What Happens |
|------|-------|----------|-------------|
| 1 | **Death** | Frame 0 | Health <= 0 → WillDieEvent → DiedEvent (unchanged) |
| 2 | **Ragdoll** | 0-2s | Enemy ragdolls, death VFX spawn, loot drops |
| 3 | **Corpse** | 2s-Ns | Ragdoll settles → strip expensive components, keep mesh |
| 4 | **Fade** | N to N+2s | Alpha dissolve or sink-into-ground |
| 5 | **Destroy** | End | Entity destroyed (or recycled to pool) |

All timings configurable per-enemy and via global `CorpseConfig` singleton.

---

## Phase 1: Core Lifecycle Infrastructure

### 1.1 CorpseConfig Singleton
- [x] Create `CorpseConfig` IComponentData singleton with global defaults
  - `float RagdollDuration` (default 2.0s — time before ragdoll settles)
  - `float CorpseLifetime` (default 15.0s — time corpse stays visible)
  - `float FadeOutDuration` (default 1.5s — visual fade time)
  - `int MaxCorpses` (default 30 — global cap, oldest despawns when exceeded)
  - `bool PersistentBosses` (default true — bosses/elites skip auto-despawn)
  - `bool EnableRagdoll` (default true — global ragdoll toggle)
  - `bool SinkOnFade` (default true — corpse sinks into ground vs alpha fade)
- [x] Create `CorpseConfigAuthoring` MonoBehaviour for subscene placement
- [x] Create `CorpseConfigBootstrapSystem` that creates default singleton if none exists

### 1.2 Per-Entity Death Settings Override
- [x] Create `CorpseSettingsOverride` IComponentData (optional, per-prefab)
  - `float RagdollDuration` (-1 = use global)
  - `float CorpseLifetime` (-1 = use global)
  - `float FadeOutDuration` (-1 = use global)
  - `bool IsBoss` (never auto-despawned if PersistentBosses=true)
  - `bool SkipRagdoll` (some enemies should just collapse)
- [x] Baked via `DamageableAuthoring` (CorpseState baked disabled, override resolved at death)

### 1.3 CorpseState Component
- [x] Create `CorpseState` IComponentData + IEnableableComponent (baked disabled, enabled on death)
  - `CorpsePhase Phase` (Ragdoll / Settled / Fading)
  - `float PhaseStartTime`
  - `float RagdollDuration` (resolved from override or global)
  - `float CorpseLifetime` (resolved from override or global)
  - `float FadeOutDuration` (resolved from override or global)
  - `bool IsBoss`
- [x] Create `CorpsePhase` enum: `Ragdoll = 0, Settled = 1, Fading = 2`

---

## Phase 2: Death Transition Rework

### 2.1 Modify DeathTransitionSystem
- [x] **Remove** the `Disabled` component addition for non-player entities — replaced with CorpseState enable
- [x] **Instead**, enable `CorpseState` (IEnableableComponent) with phase=Ragdoll and resolved timings
- [x] Keep kill attribution logic unchanged (KillCredited/AssistCredited)
- [x] Keep WillDieEvent/DiedEvent unchanged (cancellation still works)

### 2.2 Enable Enemy Ragdoll
- [x] Verify `RagdollTransitionSystem` works for non-player entities once `Disabled` is no longer added
- [x] If ragdoll bones don't exist on enemy prefabs, `Disabled` fallback used (collapse in place)
- [x] Enemies without CorpseState: fallback to `Disabled` (DeathTransitionSystem line 160)

### 2.3 Combat System Exclusion
- [x] CorpseState is IEnableableComponent — enabled corpses filtered from normal queries automatically
  - `SimpleDamageApplySystem` — skips `DeathPhase != Alive` (verified)
  - `EnemySeparationSystem` — strips EnemySeparationConfig in Settled phase
  - AI components stripped in Settled phase (AIBrain, AIState, AbilityExecutionState)
- [x] Health bar bridge already skips health <= 0 (no change needed)

---

## Phase 3: Corpse Lifecycle System

### 3.1 CorpseLifecycleSystem ✅
- [x] Create `CorpseLifecycleSystem` (ServerSimulation | LocalSimulation, SimulationSystemGroup, OrderLast)
- [x] **Ragdoll → Settled transition** (after RagdollDuration):
  - Zero PhysicsVelocity (freeze in place)
  - Strip AI components: `AIBrain`, `AIState`, `AbilityExecutionState`, `EnemySeparationConfig`
  - Strip combat components: `AttackStats`, `DefenseStats`
  - Disable `MovementOverride` (enableable)
  - Keep: mesh/rendering, `LocalTransform`, `LocalToWorld`, `DeathState`, `CorpseState`, `Health`
- [x] **Settled → Fading transition** (after CorpseLifetime):
  - Set CorpsePhase = Fading
  - Strip `PhysicsCollider` (exits broadphase)
- [x] **Fading → Destroy** (after FadeOutDuration):
  - Destroy entity via ECB

### 3.2 MaxCorpses Cap Enforcement ✅
- [x] Track corpse count via EntityQuery on `CorpseState`
- [x] When count exceeds `MaxCorpses`, force oldest non-boss corpses to Fading phase immediately
- [x] Oldest = lowest `PhaseStartTime` on CorpseState
- [x] Use `NativeArray` sort by time, skip bosses if `PersistentBosses` enabled

### 3.3 Distance-Based Optimization
- [x] `DistanceCullRange` field exists in CorpseConfig (default 100m)
- [x] Distance check logic implemented in CorpseLifecycleSystem — Settled corpses beyond DistanceCullRange from all players force-transitioned to Fading

---

## Phase 4: Client Presentation

### 4.1 CorpseSinkSystem (Client-side) ✅
- [x] Create `CorpseSinkSystem` running on ClientSimulation | LocalSimulation (PresentationSystemGroup)
- [x] Uses replicated `DeathState.StateStartTime` + global `CorpseConfig` timings (no CorpseState replication needed)
- [x] **Sink mode** (default): sinks `LocalTransform.Position.y` downward by 1.5m over FadeOutDuration
- [x] **Alpha mode** (alternative): implemented via EPIC 16.7 Phase 5 — `DIG/URP/Dissolve` shader + `CorpseDissolveSystem` drives `_DissolveAmount` via MaterialPropertyBlock. `CorpseSinkSystem` skips dissolve-capable entities (`.WithNone<DissolveCapable>()`)
- [x] Sink mode works with any shader, zero art dependency

### 4.2 Death VFX Integration Point ✅
- [x] Process `DeathSpawnElement` buffer — implemented via `DeathSpawnProcessingSystem` (EPIC 16.6, Phase 2)
- [x] On `DiedEvent`: spawn prefabs from buffer at corpse position + offset
- [x] Apply explosive force if `ApplyExplosiveForce` flag set (PhysicsVelocity with upward + radial scatter)
- [x] Enables per-enemy death particles, gibs, blood splatter, soul effects, etc.
- **File:** `Assets/Scripts/Loot/Systems/DeathSpawnProcessingSystem.cs`

### 4.3 Loot Drop Hook ✅
- [x] On `DiedEvent`, check for `LootTable` component — `DeathLootSystem` (EPIC 16.6) queries `LootTableRef` + `DiedEvent`
- [x] If present, spawn loot entities at corpse position — `DeathLootSystem` → `LootSpawnSystem` pipeline
- [x] Corpse lifetime must exceed loot pickup window — `DeathLootSystem` enforces 60s minimum `CorpseState.CorpseLifetime` when loot drops
- [x] Full loot system implemented in EPIC 16.6 (40 files: loot tables, item registry, pickup, affixes, currency, containers)

---

## Phase 5: Ghost Replication & Remote Clients

### 5.1 CorpseState Replication
- [x] CorpseSinkSystem uses `DeathState.StateStartTime` (already ghost-replicated) instead of replicating CorpseState directly
- [x] Remote clients derive sink timing from `DeathState` + global `CorpseConfig` — no additional ghost fields needed

### 5.2 Ragdoll on Remote Clients
- [ ] Evaluate: replicate ragdoll bone positions (expensive) vs play canned death animation (cheap)
- [ ] **Recommended**: remote clients play a death animation, only local/server does true ragdoll
- [ ] Add `DeathAnimationType` field to CorpseState for client-side animation selection

### 5.3 Corpse Destruction Sync
- [x] Server destroys entity → ghost despawns on clients automatically (NetCode handles this)
- [x] Client sink system handles ghost despawn gracefully (entity disappears mid-fade = OK)

---

## Phase 6: Performance & Polish

### 6.1 Corpse LOD Reduction
- [ ] On Settled phase, force corpse mesh to lowest LOD level
- [ ] Reduces triangle count for static corpses that players aren't looking at closely
- [ ] Requires LODGroup or equivalent on enemy prefabs (art pipeline dependency)

### 6.2 Physics Cleanup ✅
- [x] On Fading phase, strip `PhysicsCollider` from corpse root entity
- [x] Removes corpse from broadphase entirely — raycasts, overlap queries skip it
- [ ] **DeathLayerSystem integration**: complete the existing incomplete system (deferred)

### 6.3 Component Stripping Optimization ✅
- [x] Settled phase strips: AIBrain, AIState, AbilityExecutionState, EnemySeparationConfig, AttackStats, DefenseStats
- [x] Settled phase zeroes PhysicsVelocity (freeze in place)
- [x] Fading phase strips PhysicsCollider

### 6.4 Configurable Quality Presets
- [ ] "Maximum" — long corpse life, ragdoll, LOD reduction, fade
- [ ] "Balanced" — medium corpse life, ragdoll, instant despawn at distance
- [ ] "Performance" — short corpse life, no ragdoll (collapse animation), instant destroy
- [ ] Expose via Graphics Settings panel (EPIC Settings Menu integration)

---

## Verification Checklist

### Core Lifecycle
- [x] Enemy dies → ragdoll plays (not instant-disappear)
- [x] Ragdoll settles → corpse stays visible, AI/combat components stripped
- [x] Corpse timer expires → fade-out plays (sink into ground)
- [x] Fade completes → entity fully destroyed, not just Disabled
- [x] Entity count in Entity Debugger decreases after destroy

### Performance
- [x] Settled corpses have no AI/combat components (stripped by CorpseLifecycleSystem)
- [x] Fading corpses have no physics broadphase entries (PhysicsCollider stripped)
- [x] MaxCorpses cap works: oldest non-boss corpses force-transitioned to Fading

### Configuration
- [x] CorpseConfig singleton values change behavior at runtime
- [x] Per-prefab CorpseSettingsOverride overrides global timings
- [x] Boss enemies persist until explicit cleanup (PersistentBosses flag)

### Multiplayer
- [x] Remote clients see corpses (DeathState replicated, Disabled no longer added)
- [x] Remote clients see sink-fade (CorpseSinkSystem derives timing from DeathState)
- [x] Ghost despawn after server destroys entity is clean (NetCode handles it)

### Edge Cases
- [x] Killing already-dead enemy does nothing (DeathPhase check in DeathTransitionSystem)
- [x] Rapid kills don't exceed MaxCorpses (cap enforced per-frame)

---

## Design Considerations

### Remaining Items & Dependencies

| Item | Blocker | Effort |
|------|---------|--------|
| ~~Phase 4.1: Alpha fade mode~~ | ~~Shader support~~ — **RESOLVED by EPIC 16.7 Phase 5**: `DIG/URP/Dissolve` shader + `CorpseDissolveSystem` + `DissolveCapableAuthoring`. Remaining: assign dissolve material to enemy prefabs (art task). | Done |
| Phase 5.2: Remote ragdoll | Animation system — need canned death animations baked per enemy type. Recommendation: `DeathAnimationType` byte enum on CorpseState, client plays matching AnimationClip from a `DeathAnimationConfig` SO. | High |
| Phase 6.1: Corpse LOD | LODGroup on enemy prefabs (art pipeline). Fallback: skip LOD reduction if no LODGroup exists (safe no-op). | Low |
| Phase 6.2: DeathLayerSystem | Collision filter change on death — set `BelongsTo` to a `CorpseLayer` bit that player projectiles don't collide with. Prevents shooting corpses. Existing system logs "not implemented" warning. | Low |
| Phase 6.4: Quality presets | Settings menu integration. Storage: `CorpseQualityPreset` enum on `CorpseConfig` singleton (Maximum/Balanced/Performance). Each preset maps to fixed RagdollDuration/CorpseLifetime/FadeOutDuration/DistanceCullRange values. Applied by `CorpseConfigApplySystem` when preset changes. | Medium |

### DeathLayerSystem Detail

The existing `DeathLayerSystem` should:
1. On `CorpseState` enable (death): change the entity's `PhysicsCollider.Filter.BelongsTo` from `Creature` (bit 8) to `Corpse` (bit 14, currently unused).
2. Change `CollidesWith` to `Environment` only (no player, no creature, no projectile).
3. This prevents: players shooting corpses, enemies pathfinding around corpses, projectile impacts on corpses.
4. Raycasts for interaction (loot from corpse, examine) would use `InteractionFilter` which includes `Corpse` bit.

### Corpse Interaction Hooks (EPIC 16.1 Integration)

When EPIC 16.1 (Interaction Framework) is built, corpses with loot should be interactable:
1. On death with `LootTableRef`: add `Interactable` component to corpse entity (Instant type, verb=Loot)
2. Player interacts → opens loot UI (station session) or auto-picks nearby items
3. When all loot collected or corpse fades: remove `Interactable`
4. `InteractableContext.Verb = InteractionVerb.Loot`, `ActionNameKey = "interaction_loot_corpse"`
5. This is deferred until 16.1 exists — current flow spawns loot as separate world entities (EPIC 16.6)

### Entity Pooling (Future Optimization)

Current implementation destroys entities on fade. For high-kill-rate games (swarm combat, EPIC 16.2):
1. Instead of `ecb.DestroyEntity()`, move entity to a disabled pool (`ecb.AddComponent<Disabled>()` + reset all state)
2. `SwarmPromotionSystem` (16.2) pulls from pool before instantiating new entities
3. Reduces ECB structural changes and GC pressure from entity chunk recycling
4. Implementation: `CorpsePoolSystem` with `NativeQueue<Entity>`, max pool size = MaxCorpses
5. Deferred until profiling shows entity churn as a bottleneck
