### Epic 7.5: Networked Collision Synchronization
**Priority**: HIGH  
**Goal**: Ensure collision interactions are properly predicted and replicated in NetCode

**Design Notes (Post-7.4 Implementation)**:
- Collision detection uses proximity-based `PlayerProximityCollisionSystem` (not Unity Physics collision events)
- All collision systems run in `PredictedFixedStepSimulationSystemGroup` for proper prediction/rollback
- Audio/VFX/haptics run in client-only `PresentationSystemGroup` — no replication needed
- `PlayerCollisionState`, `Staggered`, `KnockedDown`, `Evading` are ghost-replicated components
- Authority is server-authoritative: server runs same simulation, ghosts replicate authoritative state

**Sub-Epic 7.5.1: Prediction & Rollback** *(Code Complete)*
**Goal**: Collision outcomes feel responsive locally while server remains authoritative
**Tasks**:
- [X] All collision systems run in `PredictedFixedStepSimulationSystemGroup`
- [X] `PlayerProximityCollisionSystem` queries all players (including remote ghosts without `Simulate`)
- [X] Collision state components (`PlayerCollisionState`, stagger/knockdown timers) are `[GhostField]` replicated
- [X] Enableable tags (`Staggered`, `KnockedDown`, `Evading`) work with NetCode prediction
- [X] Add prediction smoothing for collision corrections:
  - [X] `CollisionReconcile` component stores velocity/timer adjustments
  - [X] `CollisionReconciliationSystem` applies corrections gradually over 0.1s
  - [X] `CollisionPredictionCaptureSystem` captures pre-snapshot state for comparison
  - [X] `CollisionMispredictionDetectionSystem` detects server corrections and triggers smoothing
- [ ] Test collision response matches on client prediction and server authority (QA)
- [ ] Test collision behavior with 50ms, 100ms, 200ms simulated latency (QA)
- [ ] Verify no phantom forces or ghost collisions after rollback (QA)

**Files Added**:
- `Assets/Scripts/Player/Components/CollisionReconcile.cs` - Stores adjustment deltas and smoothing timer
- `Assets/Scripts/Player/Systems/CollisionReconciliationSystem.cs` - Applies corrections over multiple frames
- `Assets/Scripts/Player/Systems/CollisionMispredictionDetectionSystem.cs` - Detects mispredictions, triggers reconciliation

**Sub-Epic 7.5.2: Authority & Ownership** *(Code Complete)*
**Goal**: Server is authoritative for collision outcomes; clients predict but defer to server
**Tasks**:
- [X] Collision authority model: server-authoritative via NetCode ghost replication
- [X] Both server and client run `PlayerProximityCollisionSystem` (server authoritative, client predictive)
- [X] Push forces applied via `PlayerCollisionState.StaggerVelocity` which is ghost-replicated
- [X] Audio/VFX triggers are client-only (consume local `CollisionEvent` buffer, no replication needed)
- [X] Add collision cooldown reconciliation:
  - [X] `CollisionReconcile.CooldownAdjustment` tracks server vs client cooldown differences
  - [X] `CapturedCollisionState` captures `CollisionCooldown` for comparison
  - [X] `CollisionMispredictionDetectionSystem` detects cooldown divergence
  - [X] `CollisionReconciliationSystem` smoothly applies cooldown corrections
- [ ] Test collision between two client-predicted players (host + remote client) (QA)
- [ ] Verify no double-hit when both clients predict same collision (QA)

**Sub-Epic 7.5.3: Bandwidth Optimization** *(Code Complete)*
**Goal**: Minimize network traffic from collision state replication
**Tasks**:
- [X] Collision forces are implicit in physics state (no explicit force replication)
- [X] `CollisionEvent` buffer is local-only (not replicated — presentation consumes it client-side)
- [X] Stagger/knockdown state replicated via compact `[GhostField]` on existing components
- [X] Add `[GhostField(Quantization=...)]` to all replicated fields:
  - [X] Timer fields: Quantization=100 (0.01s precision, ~7 bits vs 32-bit float)
  - [X] StaggerVelocity: Quantization=1000 (0.001 precision per axis)
  - [X] LastPowerRatio: Quantization=1000 (0.001 precision for ratio accuracy)
  - [X] LastHitDirection: byte (2 bits effective for 0-3 values)
- [X] Add `Smoothing` attribute for client-side interpolation:
  - [X] StaggerVelocity: InterpolateAndExtrapolate (smooth remote player movement)
  - [X] StaggerIntensity: Interpolate (smooth animation blending)
  - [X] KnockdownImpactSpeed: Interpolate (smooth animation blending)
- [ ] Profile ghost snapshot size with collision components enabled (QA)
- [ ] Test network bandwidth with 10+ simultaneous player collisions (QA)

**Implementation Status (Dec 12, 2025)**:
All networking infrastructure is in place with bandwidth optimizations:

| Component | Replication Attribute | Status |
|-----------|----------------------|--------|
| `PlayerCollisionState` | `[GhostField]` with quantization on all fields | ✅ Done |
| `StaggerVelocity` | Quantization=1000 + InterpolateAndExtrapolate | ✅ Done |
| `StaggerIntensity` | Quantization=100 + Interpolate | ✅ Done |
| `KnockdownImpactSpeed` | Quantization=100 + Interpolate | ✅ Done |
| `Staggered` tag | `[GhostComponent(PrefabType = AllPredicted)]` | ✅ Done |
| `KnockedDown` tag | `[GhostComponent(PrefabType = AllPredicted)]` | ✅ Done |
| `Evading` tag | `[GhostComponent(PrefabType = AllPredicted)]` | ✅ Done |
| `CollisionReconcile` | Smoothing component (client-only) | ✅ Done |
| `CollisionReconcile.CooldownAdjustment` | Cooldown reconciliation (7.5.2) | ✅ Done |
| `CollisionReconciliationSystem` | Applies smooth corrections | ✅ Done |
| `CollisionMispredictionDetectionSystem` | Detects server corrections | ✅ Done |

**Bandwidth Optimizations Applied**:
- Timer fields: ~7 bits instead of 32-bit float (Quantization=100)
- Velocity: ~10 bits per axis instead of 32-bit float (Quantization=1000)
- Smoothing: Client-side interpolation reduces visual jitter without extra bandwidth
- LastHitDirection: byte (2 bits effective) instead of int

**No setup required** — remaining work is QA testing only:
1. Profile ghost snapshot size in Unity Profiler
2. Test bandwidth with 10+ simultaneous player collisions