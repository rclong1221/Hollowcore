# EPIC 17.11: Anti-Cheat & Server Validation

**Status:** PLANNED
**Priority:** High (Infrastructure / Security)
**Dependencies:**
- `DialogueRpcReceiveSystem` (existing -- `DIG.Dialogue`, `Assets/Scripts/Dialogue/Systems/DialogueRpcReceiveSystem.cs`, ServerSimulation, ghost-to-entity resolution via NativeParallelHashMap, stale-check pattern)
- `CraftValidationSystem` (existing -- `DIG.Crafting`, `Assets/Scripts/Crafting/Systems/CraftValidationSystem.cs`, Server|Local, recipe/ingredient/currency/station validation)
- `CurrencyTransactionSystem` (existing -- `DIG.Economy.Systems`, `Assets/Scripts/Economy/Systems/CurrencyTransactionSystem.cs`, Server|Local, processes CurrencyTransaction buffer, clamps to 0)
- `DamageApplySystem` (existing -- `Player.Systems`, `Assets/Scripts/Player/Systems/DamageApplySystem.cs`, ServerSimulation, Burst-compiled, ghost-aware, server-only damage application)
- `ReceiveRpcCommandRequest` pattern (existing -- NetCode, used by DialogueRpcReceiveSystem, CraftRpcReceiveSystem, StatAllocationRpcReceiveSystem, VoxelDamageRpc, RagdollInteractionSystem, etc.)
- `CollisionGameSettings` singleton (existing -- `Player.Components`, `Assets/Scripts/Player/Components/CollisionGameSettings.cs`, game rules config)
- `GhostOwner.NetworkId` (existing -- NetCode, player identity)
- `NetworkId` IComponentData (existing -- NetCode, on connection entities)
- `PlayerTag` IComponentData (existing -- `Player.Components`, player marker)
- `CharacterAttributes` IComponentData (existing -- `DIG.Combat.Components`, Ghost:All, includes Level)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, Ghost:AllPredicted, Gold/Premium/Crafting)
- `ResourcePool` IComponentData (existing -- `DIG.Combat.Resources`, EPIC 16.8, AllPredicted, 64 bytes)
- `PlayerProgression` IComponentData (existing -- `DIG.Progression`, EPIC 16.14, AllPredicted, CurrentXP/TotalXPEarned/UnspentStatPoints/RestedXP)
- `CurrencyTransaction` IBufferElementData (existing -- `DIG.Economy`, `Assets/Scripts/Economy/Components/CurrencyTransaction.cs`)
- `LocalTransform` (existing -- Unity.Transforms, position data for movement validation)
- `PhysicsVelocity` (existing -- Unity.Physics, velocity data for speed validation)
- `NetworkStreamConnection` (existing -- NetCode, for disconnect/kick operations)

**Feature:** A server-authoritative anti-cheat framework providing unified RPC rate limiting (token bucket per player per RPC type), server-side movement validation (speed hack detection), economy audit trail (transaction log with balance reconciliation), ability cooldown enforcement, weighted violation accumulation with automatic penalty escalation (warn, kick, temp ban, perma ban), and telemetry bridging for suspicious activity monitoring. All validation state is ephemeral (resets on disconnect) -- ban lists are external. Only 8 bytes added to player archetype via `ValidationLink` pointing to a child entity.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Anti-Cheat Relevance |
|--------|------|--------|----------------------|
| `DialogueRpcReceiveSystem` | `Assets/Scripts/Dialogue/Systems/DialogueRpcReceiveSystem.cs` | ServerSimulation | Ghost-to-entity O(1) resolution via NativeParallelHashMap. No rate limiting. No violation tracking. |
| `CraftValidationSystem` | `Assets/Scripts/Crafting/Systems/CraftValidationSystem.cs` | Server\|Local | Validates recipe existence, station type/tier, queue capacity, recipe knowledge, ingredients, currency. **No rate limiting on craft requests.** |
| `CraftRpcReceiveSystem` | `Assets/Scripts/Crafting/Systems/CraftRpcReceiveSystem.cs` | ServerSimulation | Receives craft RPCs, resolves ghost IDs. No rate limiting. |
| `StatAllocationRpcReceiveSystem` | `Assets/Scripts/Progression/Systems/StatAllocationRpcReceiveSystem.cs` | ServerSimulation | Receives stat allocation RPCs. Validates points available. No rate limiting. |
| `CurrencyTransactionSystem` | `Assets/Scripts/Economy/Systems/CurrencyTransactionSystem.cs` | Server\|Local | Clamps balance to 0 (no negative). **No audit trail -- transactions are consumed and discarded.** |
| `DamageApplySystem` | `Assets/Scripts/Player/Systems/DamageApplySystem.cs` | ServerSimulation, Burst | Server-only damage. Already cheat-resistant by design (clients cannot write damage). |
| `CollisionGameSettings` singleton | `Assets/Scripts/Player/Components/CollisionGameSettings.cs` | Server\|Local | Game rules config. Could host validation thresholds but currently unrelated. |
| `VoxelDamageRpc` | `Assets/Scripts/Voxel/Systems/Network/VoxelDamageRpc.cs` | Server | Receives voxel damage RPCs. No rate limiting. |
| `GoInGameSystem` | `Assets/Scripts/Systems/Network/GoInGameSystem.cs` | Server | Connection lifecycle. No ban list check. |

### Existing Validation Patterns (Reusable)

1. **Ghost-to-entity resolution**: `DialogueRpcReceiveSystem` builds `NativeParallelHashMap<int, Entity>` per frame for O(1) ghost ID lookup. All RPC receive systems use `ReceiveRpcCommandRequest.SourceConnection` to identify the sending player.

2. **Server-side ingredient validation**: `CraftValidationSystem` atomically validates and consumes ingredients before queuing a craft. This is the correct pattern -- validate-then-consume -- but has no rate limiting wrapper.

3. **Balance clamping**: `CurrencyTransactionSystem` clamps to 0 via `math.max(0, ...)`. Prevents negative balances but does not log the transaction for auditing.

4. **Server-only damage**: `DamageApplySystem` (Burst, ServerSimulation) ensures clients cannot directly write damage. All damage flows through server-authoritative systems (`DamageEvent` pipeline or `CombatResultEvent` pipeline).

---

## Problem Statement

DIG's server-authoritative architecture means clients cannot directly modify ECS state. However, the RPC interface is the attack surface -- a malicious client can:

| Attack Vector | Current Defense | Gap |
|---------------|-----------------|-----|
| RPC flooding (craft, dialogue, stat allocation, voxel) | None | A client can send 1000 craft RPCs per frame. Each triggers full validation logic, wasting server CPU. |
| Speed hacking (modified movement speed) | None | No server-side movement position validation. Client-predicted movement is trusted. |
| Economy duplication | Balance clamp to 0 | No audit trail. No reconciliation. No detection of duplicate transaction injection. |
| Ability cooldown bypass | Client prediction | No server-side cooldown enforcement. Client could fire abilities at 10x normal rate. |
| Teleportation | None | No position delta validation. A client can report position 1km away between ticks. |
| XP injection via fake KillCredited | Server-only (KillCredited via EndSimulationECB) | Safe today, but no generic RPC validation for future XP grant RPCs. |
| Automated bot play | None | No telemetry for detecting repetitive input patterns. |
| Known bad actors rejoining | None | No ban list checked at connection time. |
| Suspicious activity correlation | None | No centralized violation scoring. Each system silently rejects bad requests with no cross-system awareness. |

**The gap:** Individual systems reject invalid requests, but there is no unified framework to: (a) limit request rates, (b) track violations across systems, (c) escalate penalties, (d) audit economic transactions, (e) validate movement, or (f) export telemetry for analysis.

---

## Architecture Overview

```
                    CONFIGURATION LAYER
  ValidationProfile SO      PenaltyConfig SO       MovementLimits SO
  (per-RPC rate limits,     (violation thresholds,  (max speed per state,
   max burst, refill rate)   ban durations)          teleport threshold)
           |                      |                      |
           └────── ValidationBootstrapSystem ────────────┘
                   (loads from Resources/, creates
                    ValidationConfig singleton,
                    creates BanListManager)
                              |
                    ECS DATA LAYER (on ValidationChild entity)
  PlayerValidationState       RateLimitEntry buffer     MovementValidationState
  (ViolationScore, Penalty    (per-RPC token buckets)   (LastValidatedPos, Error)
   Level, LastWarningTick)
           |                      |                          |
  Player ──→ ValidationLink (8 bytes, Entity ref to child)
                              |
                    SYSTEM PIPELINE
                              |
  ┌─────────────────────────────────────────────────────────────────┐
  │  RPC VALIDATION MIDDLEWARE (opt-in pattern)                     │
  │                                                                 │
  │  Before processing any RPC:                                     │
  │    1. Resolve SourceConnection → player entity                  │
  │    2. Follow ValidationLink → child entity                      │
  │    3. Call RateLimitHelper.CheckAndConsume(child, rpcTypeId)     │
  │    4. If denied → create ViolationEvent + discard RPC           │
  │    5. If allowed → proceed with normal processing               │
  │                                                                 │
  │  Existing systems add ~5 lines to integrate:                    │
  │    CraftRpcReceiveSystem, DialogueRpcReceiveSystem,             │
  │    StatAllocationRpcReceiveSystem, VoxelDamageRpc, etc.         │
  └─────────────────────────────────────────────────────────────────┘
                              |
  InitializationSystemGroup:
    ValidationBootstrapSystem         — loads SOs, creates singleton, ban list (runs once)
                              |
  SimulationSystemGroup:
    RateLimitRefillSystem             — refill token buckets for all players
    MovementValidationSystem          — position delta vs max speed * dt
    CooldownValidationSystem          — verify ability usage respects cooldowns
    EconomyAuditSystem                — log transactions, reconcile balances
    ViolationAccumulatorSystem        — aggregate violations, decay over time
                              |
  EndSimulationSystemGroup:
    PenaltyExecutionSystem            — execute penalties (warn, kick, ban)
                              |
  PresentationSystemGroup:
    ValidationTelemetryBridgeSystem   — bridge violations to telemetry/logging
```

### Data Flow (RPC Flood Detection → Kick)

```
Frame N:
  1. Malicious client sends 50 CraftRpc in one frame
  2. CraftRpcReceiveSystem: for each RPC:
     - RateLimitHelper.CheckAndConsume(validationChild, CRAFT_RPC_TYPE_ID)
     - First 5 RPCs succeed (token bucket allows burst of 5)
     - RPCs 6-50 fail → each creates ViolationEvent(RateLimit, severity=0.5)

Frame N (same frame, later in SimulationSystemGroup):
  3. ViolationAccumulatorSystem:
     - Reads 45 ViolationEvents for this player
     - Accumulates: ViolationScore += 45 * 0.5 * RateLimitWeight = 22.5
     - Decays existing score: ViolationScore *= (1 - DecayRate * dt)
     - Checks thresholds: 22.5 > KickThreshold(20.0)
     - Sets PenaltyLevel = Kick
     - Destroys ViolationEvent entities

Frame N (EndSimulationSystemGroup):
  4. PenaltyExecutionSystem:
     - Reads PenaltyLevel == Kick
     - Sends disconnect command via NetCode (NetworkStreamRequestDisconnect)
     - Logs: "[AntiCheat] Kicked player {NetworkId} - RateLimit violation score 22.5"
     - Records in BanListManager (temp ban if repeated)

Frame N (PresentationSystemGroup):
  5. ValidationTelemetryBridgeSystem:
     - Exports violation event to telemetry sink
```

---

## ECS Components

### On Player Entity (8 bytes only)

**File:** `Assets/Scripts/Validation/Components/ValidationLink.cs`

```
ValidationLink (IComponentData, Ghost:AllPredicted)
  ValidationChild : Entity    // link to child entity with all validation state
```

**Archetype impact:** 8 bytes. Same child entity pattern as `SaveStateLink`, `TalentLink`, `CraftingKnowledgeLink`.

### On Validation Child Entity (NOT on player archetype)

**File:** `Assets/Scripts/Validation/Components/ValidationComponents.cs`

```
ValidationChildTag (IComponentData)
  -- marker tag, zero bytes

ValidationOwner (IComponentData)
  Owner : Entity    // back-reference to player entity (8 bytes)

PlayerValidationState (IComponentData, 24 bytes)
  ViolationScore     : float     // weighted violation accumulator (decays over time)
  LastWarningTick    : uint      // server tick of last warning issued
  PenaltyLevel       : byte      // current penalty: None(0), Warn(1), Kick(2), TempBan(3), PermaBan(4)
  ConsecutiveKicks   : byte      // escalation counter (resets after clean session)
  WarningCount       : byte      // warnings issued this session
  Padding            : byte
  SessionStartTick   : uint      // tick when player connected (for session duration checks)
  LastViolationTick  : uint      // tick of most recent violation (for decay timing)

RateLimitEntry (IBufferElementData, InternalBufferCapacity=8, 12 bytes per element)
  RpcTypeId      : ushort    // stable identifier per RPC type
  TokenCount     : float     // current available tokens (refilled over time)
  LastRefillTick : uint      // server tick of last refill
  BurstConsumed  : ushort    // tokens consumed this frame (reset per frame)

MovementValidationState (IComponentData, 24 bytes)
  LastValidatedPosition : float3    // last server-accepted position (12 bytes)
  LastValidatedTick     : uint      // server tick of last validation (4 bytes)
  AccumulatedError      : float     // cumulative position error (for gradual drift detection) (4 bytes)
  TeleportCooldownTick  : uint      // server-granted teleport immunity expiry (4 bytes)

EconomyAuditEntry (IBufferElementData, InternalBufferCapacity=16, 20 bytes per element)
  TransactionType : byte      // CurrencyType (Gold=0, Premium=1, Crafting=2)
  SourceSystem    : byte      // which system initiated (Craft=0, Trade=1, Loot=2, Quest=3, Admin=4)
  Amount          : int       // signed delta (+income, -expense)
  BalanceBefore   : int       // balance before this transaction
  BalanceAfter    : int       // balance after this transaction
  ServerTick      : uint      // when this transaction occurred
```

### Transient Entities

**File:** `Assets/Scripts/Validation/Components/ViolationEvent.cs`

```
ViolationEvent (IComponentData, 20 bytes)
  PlayerEntity   : Entity         // the offending player (8 bytes)
  ViolationType  : byte           // RateLimit(0), Movement(1), Economy(2), Cooldown(3), Generic(4)
  Severity       : float          // 0.0-1.0, weighted by violation type
  ServerTick     : uint           // when violation occurred
  DetailCode     : ushort         // sub-type for telemetry (e.g., which RPC type was rate-limited)
  Padding        : byte
```

### Singleton

**File:** `Assets/Scripts/Validation/Components/ValidationConfig.cs`

```
ValidationConfig (IComponentData, ~64 bytes)
  -- Rate Limiting
  DefaultTokensPerSecond   : float     // default refill rate for unregistered RPC types
  DefaultMaxBurst          : float     // default max token bucket capacity
  -- Movement
  MaxPositionErrorPerTick  : float     // meters of acceptable error per tick
  TeleportThreshold        : float     // meters -- position delta above this is teleport
  ErrorDecayRate           : float     // accumulated error decay per second
  MaxAccumulatedError      : float     // error cap before violation
  -- Violations
  ViolationDecayRate       : float     // score decay per second (e.g., 0.5 = halves in 2s)
  WarnThreshold            : float     // score threshold for warning
  KickThreshold            : float     // score threshold for kick
  TempBanThreshold         : float     // score threshold for temp ban (consecutive kicks)
  -- Weights
  RateLimitWeight          : float     // multiplier for rate-limit violations
  MovementWeight           : float     // multiplier for movement violations
  EconomyWeight            : float     // multiplier for economy violations
  CooldownWeight           : float     // multiplier for cooldown violations
  -- Penalty
  TempBanDurationMinutes   : int       // default temp ban length
  ConsecutiveKicksForBan   : int       // kicks before escalating to temp ban
```

### Enums

**File:** `Assets/Scripts/Validation/Components/ValidationEnums.cs`

```
PenaltyLevel enum : byte
  None(0), Warn(1), Kick(2), TempBan(3), PermaBan(4)

ViolationType enum : byte
  RateLimit(0), Movement(1), Economy(2), Cooldown(3), Generic(4)

TransactionSourceSystem enum : byte
  Craft(0), Trade(1), Loot(2), Quest(3), Admin(4), Death(5), Vendor(6), Reward(7)

RpcTypeIds static class (ushort constants)
  DIALOGUE_CHOICE  = 1
  DIALOGUE_SKIP    = 2
  CRAFT_REQUEST    = 3
  STAT_ALLOCATION  = 4
  TALENT_ALLOCATION = 5
  TALENT_RESPEC    = 6
  VOXEL_DAMAGE     = 7
  TRADE_REQUEST    = 8
  RESPAWN          = 9
  // 10-255: reserved for future RPCs
```

---

## ScriptableObjects

### ValidationProfile

**File:** `Assets/Scripts/Validation/Definitions/ValidationProfile.cs`

```
[CreateAssetMenu(menuName = "DIG/Validation/Validation Profile")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| RpcRateLimits | RpcRateLimitEntry[] | (see below) | Per-RPC-type rate limit configuration |
| DefaultTokensPerSecond | float | 2.0 | Fallback refill rate for unregistered RPC types |
| DefaultMaxBurst | float | 5.0 | Fallback max token count |

```
[Serializable]
RpcRateLimitEntry
  RpcTypeId        : ushort    // matches RpcTypeIds constant
  DisplayName      : string    // human-readable name for editor
  TokensPerSecond  : float     // token refill rate
  MaxBurst         : float     // max token bucket capacity
  ViolationSeverity: float     // severity when rate limit exceeded (0.0-1.0)
```

Default rate limits:

| RPC Type | Tokens/s | MaxBurst | Severity | Rationale |
|----------|----------|----------|----------|-----------|
| DialogueChoice | 2.0 | 3 | 0.3 | Dialogue is slow-paced |
| DialogueSkip | 2.0 | 3 | 0.3 | Cannot skip faster than dialogue plays |
| CraftRequest | 3.0 | 5 | 0.5 | Rapid crafting is suspicious |
| StatAllocation | 1.0 | 5 | 0.7 | Stat allocation is rare |
| TalentAllocation | 1.0 | 5 | 0.7 | Talent allocation is rare |
| TalentRespec | 0.1 | 1 | 0.9 | Respec is very rare |
| VoxelDamage | 10.0 | 20 | 0.3 | Mining is rapid but legitimate |
| TradeRequest | 1.0 | 3 | 0.8 | Trade exploits are high-value |
| Respawn | 0.5 | 2 | 0.5 | Respawn spam |

### PenaltyConfig

**File:** `Assets/Scripts/Validation/Definitions/PenaltyConfig.cs`

```
[CreateAssetMenu(menuName = "DIG/Validation/Penalty Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| WarnThreshold | float | 5.0 | Violation score to issue warning |
| KickThreshold | float | 20.0 | Violation score to kick |
| ConsecutiveKicksForTempBan | int | 3 | Kicks within decay window before temp ban |
| TempBanDurationMinutes | int | 30 | Duration of temp ban |
| TempBansForPermaBan | int | 3 | Temp bans before permanent ban |
| ViolationDecayRate | float | 0.5 | Score decay per second |
| RateLimitWeight | float | 1.0 | Multiplier for rate-limit violations |
| MovementWeight | float | 2.0 | Multiplier for movement violations (high -- speed hacks are obvious) |
| EconomyWeight | float | 3.0 | Multiplier for economy violations (highest -- money exploits are damaging) |
| CooldownWeight | float | 1.5 | Multiplier for cooldown violations |
| WarnCooldownSeconds | float | 10.0 | Min seconds between warnings to same player |

### MovementLimits

**File:** `Assets/Scripts/Validation/Definitions/MovementLimits.cs`

```
[CreateAssetMenu(menuName = "DIG/Validation/Movement Limits")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| MaxSpeedStanding | float | 5.0 | m/s standing walk |
| MaxSpeedSprinting | float | 10.0 | m/s sprinting |
| MaxSpeedCrouching | float | 2.5 | m/s crouching |
| MaxSpeedProne | float | 1.0 | m/s prone |
| MaxSpeedSwimming | float | 4.0 | m/s swimming |
| MaxSpeedFalling | float | 50.0 | m/s terminal velocity (generous for physics) |
| SpeedToleranceMultiplier | float | 1.3 | 30% tolerance above max (network jitter) |
| TeleportThreshold | float | 20.0 | m -- position delta above this is flagged as teleport |
| ErrorAccumulationRate | float | 1.0 | How fast accumulated error grows |
| ErrorDecayRate | float | 2.0 | How fast accumulated error decays per second |
| MaxAccumulatedError | float | 10.0 | Error cap before violation fires |
| TeleportGraceTicks | uint | 10 | Ticks of immunity after server-granted teleport |

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Local):
  ValidationBootstrapSystem                    -- loads SOs, creates config singleton, ban list (runs once)

SimulationSystemGroup (Server|Local):
  [OrderFirst]
  RateLimitRefillSystem                        -- refill all token buckets per tick
    |
  [after PlayerMovementSystem]
  MovementValidationSystem                     -- position delta validation per player per tick

  [after CurrencyTransactionSystem]
  EconomyAuditSystem                           -- log transactions, reconcile balances

  CooldownValidationSystem                     -- verify ability usage respects server-side cooldowns

  [after MovementValidation, EconomyAudit, CooldownValidation]
  ViolationAccumulatorSystem                   -- aggregate violations, apply decay, check thresholds

EndSimulationSystemGroup (Server|Local):
  PenaltyExecutionSystem                       -- execute warn/kick/ban penalties

PresentationSystemGroup (Server|Local):
  ValidationTelemetryBridgeSystem              -- bridge violations to telemetry/logging
```

### System Execution Order (ASCII Diagram)

```
                    InitializationSystemGroup
                              |
                    ValidationBootstrapSystem (runs once)
                              |
                    SimulationSystemGroup
                              |
  ┌───────────────────────────┼───────────────────────────┐
  |                           |                           |
  RateLimitRefillSystem   [PlayerMovementSystem]   [CurrencyTransactionSystem]
  (OrderFirst)             (existing, unchanged)    (existing, unchanged)
  |                           |                           |
  |                   MovementValidationSystem     EconomyAuditSystem
  |                   (UpdateAfter:                (UpdateAfter:
  |                    PlayerMovementSystem)        CurrencyTransactionSystem)
  |                           |                           |
  |                   CooldownValidationSystem            |
  |                           |                           |
  └───────────────────────────┼───────────────────────────┘
                              |
                    ViolationAccumulatorSystem
                    (UpdateAfter: MovementValidation,
                     EconomyAudit, CooldownValidation)
                              |
                    EndSimulationSystemGroup
                              |
                    PenaltyExecutionSystem
                              |
                    PresentationSystemGroup
                              |
                    ValidationTelemetryBridgeSystem
```

### Key System Details

**ValidationBootstrapSystem** (managed SystemBase, InitializationSystemGroup, Server|Local, runs once):

**File:** `Assets/Scripts/Validation/Systems/ValidationBootstrapSystem.cs`

1. Load `ValidationProfile` from `Resources/ValidationProfile`
2. Load `PenaltyConfig` from `Resources/PenaltyConfig`
3. Load `MovementLimits` from `Resources/MovementLimits`
4. Create `ValidationConfig` singleton from SO values
5. Initialize `BanListManager` (loads ban list from file/database)
6. `Enabled = false` (self-disables after first run)

**RateLimitRefillSystem** (ISystem, Burst-compatible, SimulationSystemGroup OrderFirst, Server|Local):

**File:** `Assets/Scripts/Validation/Systems/RateLimitRefillSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- Iterates all `ValidationChildTag` entities with `RateLimitEntry` buffer
- Per entry: `TokenCount = min(MaxBurst, TokenCount + TokensPerSecond * dt)`
- Resets `BurstConsumed = 0` per frame
- O(1) per player per RPC type. Budget: <0.05ms for 64 players * 9 RPC types = 576 entries.

**MovementValidationSystem** (ISystem, SimulationSystemGroup, UpdateAfter PlayerMovementSystem, Server|Local):

**File:** `Assets/Scripts/Validation/Systems/MovementValidationSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]`
- `[UpdateAfter(typeof(PlayerMovementSystem))]`
- Per player entity with `ValidationLink`:
  1. Read `LocalTransform.Position` (current predicted position)
  2. Follow `ValidationLink → MovementValidationState` on child
  3. Compute `delta = distance(currentPos, lastValidatedPos)`
  4. Compute `maxAllowedDelta = maxSpeedForState * SpeedToleranceMultiplier * dt`
  5. If in `TeleportCooldownTick > currentTick`: skip (server-granted teleport)
  6. If `delta > TeleportThreshold`: create `ViolationEvent(Movement, severity=1.0)`, snap position back
  7. Else if `delta > maxAllowedDelta`: accumulate error, if `AccumulatedError > MaxAccumulatedError`: create `ViolationEvent(Movement, severity=0.6)`
  8. Else: decay `AccumulatedError *= (1 - ErrorDecayRate * dt)`
  9. Update `LastValidatedPosition = currentPos`, `LastValidatedTick = currentTick`
- Budget: <0.1ms (one position check per player per tick, ~64 players max)

**EconomyAuditSystem** (managed SystemBase, SimulationSystemGroup, UpdateAfter CurrencyTransactionSystem, Server|Local):

**File:** `Assets/Scripts/Validation/Systems/EconomyAuditSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(CurrencyTransactionSystem))]`
- Per player with `CurrencyInventory` + `ValidationLink`:
  1. Read `CurrencyInventory` (post-transaction values)
  2. Follow `ValidationLink → EconomyAuditEntry` buffer on child
  3. For each transaction processed this frame (detected via change filter on CurrencyInventory):
     - Record `EconomyAuditEntry` with before/after balances
  4. **Reconciliation check**: if `BalanceAfter` of last audit entry does not match current `CurrencyInventory` values, create `ViolationEvent(Economy, severity=1.0)`
  5. Ring buffer: if `EconomyAuditEntry.Length > InternalCapacity * 4`, trim oldest half
- Budget: <0.05ms (only runs on frames where transactions occur)

**CooldownValidationSystem** (managed SystemBase, SimulationSystemGroup, Server|Local):

**File:** `Assets/Scripts/Validation/Systems/CooldownValidationSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- Per player with ability execution state:
  1. Track server-side cooldown timestamps per ability type
  2. When ability activation RPC arrives, verify `currentTick - lastUseTick >= cooldownTicks`
  3. If violated: create `ViolationEvent(Cooldown, severity=0.5)`, reject activation
- Uses `NativeParallelHashMap<int, uint>` on managed singleton (abilityTypeId -> lastUseTick per player)
- Budget: <0.02ms (only on ability activation frames)

**ViolationAccumulatorSystem** (ISystem, SimulationSystemGroup, Server|Local):

**File:** `Assets/Scripts/Validation/Systems/ViolationAccumulatorSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(MovementValidationSystem))]`
- `[UpdateAfter(typeof(EconomyAuditSystem))]`
- `[UpdateAfter(typeof(CooldownValidationSystem))]`
- Query `ViolationEvent` entities:
  1. For each: resolve `PlayerEntity → ValidationLink → PlayerValidationState`
  2. Apply weight: `score += Severity * TypeWeight`
  3. Destroy `ViolationEvent` entity
- Per player `PlayerValidationState`:
  1. Decay: `ViolationScore *= (1 - DecayRate * dt)`
  2. Floor at 0
  3. Check thresholds: if `ViolationScore >= KickThreshold` and `PenaltyLevel < Kick`: set `PenaltyLevel = Kick`
  4. Escalation: if `ConsecutiveKicks >= ConsecutiveKicksForBan`: set `PenaltyLevel = TempBan`
- Budget: <0.02ms (violation events are sparse)

**PenaltyExecutionSystem** (managed SystemBase, EndSimulationSystemGroup, Server|Local):

**File:** `Assets/Scripts/Validation/Systems/PenaltyExecutionSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(EndSimulationECBSystem.Singleton))]` -- no, just EndSimulationSystemGroup
- Per `ValidationChildTag` entity with `PlayerValidationState` where `PenaltyLevel > None`:
  - **Warn**: Add `NetworkStreamRequestSendRpc` with warning message. Increment `WarningCount`. Reset `PenaltyLevel = None`. Cooldown: skip if `currentTick - LastWarningTick < WarnCooldownTicks`.
  - **Kick**: Request disconnect via `NetworkStreamRequestDisconnect` on connection entity. Increment `ConsecutiveKicks`. Log event.
  - **TempBan**: Request disconnect + add to `BanListManager` with expiry timestamp. Log event.
  - **PermaBan**: Request disconnect + add to `BanListManager` with permanent flag. Log event.
- Budget: <0.01ms (penalties are very rare)

**ValidationTelemetryBridgeSystem** (managed SystemBase, PresentationSystemGroup, Server|Local):

**File:** `Assets/Scripts/Validation/Systems/ValidationTelemetryBridgeSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- Reads `PlayerValidationState` for all players each frame (only logs when state changed)
- Bridges to `ValidationTelemetryQueue` (static ConcurrentQueue for async export)
- Supports future integration with EPIC 17.13 (Analytics) telemetry sinks
- Budget: <0.01ms (change-filter skips unchanged players)

---

## RPC Middleware Pattern

### Integration Pattern for Existing Systems

Each existing RPC receive system adds approximately 5 lines to integrate rate limiting. The pattern is opt-in -- systems that do not integrate continue to work without rate limiting.

**Helper class:**

**File:** `Assets/Scripts/Validation/Core/RateLimitHelper.cs`

```
RateLimitHelper (static class)
  CheckAndConsume(EntityManager em, Entity validationChild, ushort rpcTypeId) -> bool
    1. Get RateLimitEntry buffer from validationChild
    2. Find entry matching rpcTypeId (linear scan, <10 entries)
    3. If TokenCount >= 1.0: TokenCount -= 1.0, return true
    4. Else: return false

  CreateViolation(EntityManager em, EntityCommandBuffer ecb, Entity player, ViolationType type, float severity, ushort detail)
    - Creates transient ViolationEvent entity via ECB
```

### Example Integration: CraftRpcReceiveSystem

```
// BEFORE (existing code):
for (int i = 0; i < rpcs.Length; i++)
{
    var rpc = rpcs[i];
    // ... process craft request
}

// AFTER (with rate limiting, ~5 lines added):
for (int i = 0; i < rpcs.Length; i++)
{
    var rpc = rpcs[i];
    var player = ResolvePlayer(receivers[i].SourceConnection);

    // --- ANTI-CHEAT: Rate limit check ---
    if (EntityManager.HasComponent<ValidationLink>(player))
    {
        var valChild = EntityManager.GetComponentData<ValidationLink>(player).ValidationChild;
        if (!RateLimitHelper.CheckAndConsume(EntityManager, valChild, RpcTypeIds.CRAFT_REQUEST))
        {
            RateLimitHelper.CreateViolation(EntityManager, _ecb, player,
                ViolationType.RateLimit, 0.5f, RpcTypeIds.CRAFT_REQUEST);
            EntityManager.DestroyEntity(rpcEntities[i]);
            continue;
        }
    }
    // --- END ANTI-CHEAT ---

    // ... existing processing unchanged
}
```

### Systems Requiring Integration

| System | File | RPC Type ID | Priority |
|--------|------|-------------|----------|
| `CraftRpcReceiveSystem` | `Assets/Scripts/Crafting/Systems/CraftRpcReceiveSystem.cs` | CRAFT_REQUEST(3) | High |
| `DialogueRpcReceiveSystem` | `Assets/Scripts/Dialogue/Systems/DialogueRpcReceiveSystem.cs` | DIALOGUE_CHOICE(1), DIALOGUE_SKIP(2) | Medium |
| `StatAllocationRpcReceiveSystem` | `Assets/Scripts/Progression/Systems/StatAllocationRpcReceiveSystem.cs` | STAT_ALLOCATION(4) | High |
| `VoxelDamageRpc` (receive path) | `Assets/Scripts/Voxel/Systems/Network/VoxelDamageRpc.cs` | VOXEL_DAMAGE(7) | Medium |
| `RespawnDebugSystem` | `Assets/Scripts/Player/Systems/RespawnDebugSystem.cs` | RESPAWN(9) | Low |
| `RagdollInteractionSystem` | `Assets/Scripts/Player/Systems/RagdollInteractionSystem.cs` | (future ID) | Low |
| `TalentRpcReceiveSystem` (EPIC 17.1) | `Assets/Scripts/SkillTree/Systems/TalentRpcReceiveSystem.cs` | TALENT_ALLOCATION(5), TALENT_RESPEC(6) | High |
| Trade RPC (EPIC 17.3) | `Assets/Scripts/Trading/Systems/TradeRpcReceiveSystem.cs` | TRADE_REQUEST(8) | High |

---

## Ban List Management

### BanListManager

**File:** `Assets/Scripts/Validation/Core/BanListManager.cs`

```
BanListManager (static managed class, NOT ECS)
  Load(filePath) -> reads ban entries from JSON file
  Save(filePath) -> writes ban entries to JSON file
  IsBanned(networkId) -> bool (checks active bans, respects expiry)
  AddTempBan(networkId, durationMinutes, reason)
  AddPermaBan(networkId, reason)
  RemoveBan(networkId)
  GetActiveBans() -> List<BanEntry> (for editor tooling)

BanEntry (Serializable class)
  NetworkId       : string    // player identity (stable across sessions)
  BanType         : byte      // Temp(0), Permanent(1)
  ExpiryUtcMs     : long      // Unix epoch UTC ms (0 = permanent)
  Reason          : string    // human-readable
  IssuedUtcMs     : long      // when ban was issued
  ViolationScore  : float     // score at time of ban (for review)
```

### Connection Gate

**File:** `Assets/Scripts/Validation/Systems/ConnectionValidationSystem.cs` (MODIFY `GoInGameSystem`)

- In `GoInGameSystem.OnUpdate()`, before spawning player:
  1. Read `NetworkId` from connection entity
  2. Check `BanListManager.IsBanned(networkId)`
  3. If banned: send disconnect with ban reason, do NOT spawn player
- Alternative (cleaner): separate `ConnectionValidationSystem` that runs UpdateBefore `GoInGameSystem`, adds `BannedConnectionTag` to connection entity. `GoInGameSystem` skips entities with `BannedConnectionTag`.

---

## Movement Validation: Server-Granted Teleport Immunity

Legitimate teleportation scenarios (respawn, portal, admin TP) must not trigger movement violations. The pattern:

1. System performing teleport (e.g., `RespawnSystem`, `PortalSystem`) writes `MovementValidationState.TeleportCooldownTick = currentTick + TeleportGraceTicks` on the validation child entity
2. `MovementValidationSystem` checks `TeleportCooldownTick > currentTick` and skips validation for that player during the grace period
3. After grace period expires, validation resumes from the new position

**File:** `Assets/Scripts/Validation/Core/TeleportImmunityHelper.cs`

```
TeleportImmunityHelper (static class)
  GrantImmunity(EntityManager em, Entity player, uint graceTicks)
    - Resolves ValidationLink → child → MovementValidationState
    - Sets TeleportCooldownTick and updates LastValidatedPosition to current position
```

Systems that must call `TeleportImmunityHelper.GrantImmunity()`:
- `RespawnDebugSystem` / future `RespawnSystem`
- Future `PortalSystem`
- Future `AdminTeleportSystem`
- `LoadSystem` (EPIC 16.15, position restored from save)

---

## Authoring

**File:** `Assets/Scripts/Validation/Authoring/ValidationAuthoring.cs`

```
[AddComponentMenu("DIG/Validation/Player Validation")]
```

- Place on player prefab (Warrok_Server) alongside existing authorings
- Baker creates child entity with: `ValidationChildTag`, `ValidationOwner`, `PlayerValidationState`, `RateLimitEntry` buffer, `MovementValidationState`, `EconomyAuditEntry` buffer
- Baker adds `ValidationLink` to parent player entity (8 bytes)
- `RateLimitEntry` buffer pre-populated from `ValidationProfile` SO (one entry per configured RPC type)

---

## Editor Tooling

### ValidationWorkstationWindow

**File:** `Assets/Editor/ValidationWorkstation/ValidationWorkstationWindow.cs`

- Menu: `DIG/Validation Workstation`
- Sidebar + `IValidationWorkstationModule` pattern (matches Persistence/Progression/SkillTree Workstations)

### Modules (6 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| Player Monitor | `Modules/PlayerMonitorModule.cs` | Live violation score per connected player (color-coded: green < warn, yellow < kick, red >= kick). PenaltyLevel indicator. ConsecutiveKicks counter. Session duration. |
| Rate Limits | `Modules/RateLimitModule.cs` | Per-player per-RPC-type token bucket visualization. Bar graph showing current token count / max burst. Highlight RPCs that are being rate-limited. |
| Movement Trail | `Modules/MovementTrailModule.cs` | SceneView overlay: draw player movement trail (last 120 positions). Color-code by validation result (green = valid, yellow = error accumulating, red = violation). Shows teleport immunity windows. |
| Economy Audit | `Modules/EconomyAuditModule.cs` | Per-player transaction log (EconomyAuditEntry history). Sortable by tick, amount, type. Balance reconciliation check (current balance vs expected from audit trail). Export to CSV. |
| Ban Manager | `Modules/BanManagerModule.cs` | List active bans. Manual ban/kick/unban controls. Ban reason editor. Expiry timer display. Import/export ban list JSON. |
| Violation Timeline | `Modules/ViolationTimelineModule.cs` | Horizontal timeline per player showing violation events as colored dots (by type). Zoom/pan. Click event for details (type, severity, detail code, server tick). Useful for replaying suspicious sessions. |

---

## No ISaveModule

Anti-cheat validation state is **ephemeral** -- all per-player data (`PlayerValidationState`, `RateLimitEntry` buffer, `MovementValidationState`, `EconomyAuditEntry` buffer) resets when the player disconnects. There is no `ISaveModule` for validation.

**Ban list is external:** `BanListManager` reads/writes a JSON file on disk (or future database). This persists across server restarts but is not part of the ECS save system.

**Rationale:**
- Violation scores should not carry across sessions (fresh start on reconnect)
- Rate limit tokens should not persist (prevents stale bucket state)
- Economy audit trail is only useful for the current session (historical analysis via telemetry export, not save files)
- Movement validation state must match live physics state (restoring stale positions would cause false positives)

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `ValidationBootstrapSystem` | N/A | No | Runs once at startup |
| `RateLimitRefillSystem` | < 0.05ms | Yes | O(1) per player per RPC type. 64 players * 9 types = 576 entries |
| `MovementValidationSystem` | < 0.1ms | Yes | One position check per player per tick. ~64 players max |
| `EconomyAuditSystem` | < 0.05ms | No | Only runs on transaction frames (sparse). Managed due to change filter |
| `CooldownValidationSystem` | < 0.02ms | No | Only on ability activation frames (sparse) |
| `ViolationAccumulatorSystem` | < 0.02ms | Yes | Violation events are rare. Decay + threshold = O(N) where N = connected players |
| `PenaltyExecutionSystem` | < 0.01ms | No | Penalties are very rare. Managed for NetCode disconnect API |
| `ValidationTelemetryBridgeSystem` | < 0.01ms | No | Change-filter skips unchanged players |
| **RPC middleware overhead** | < 0.005ms per RPC | No | RateLimitHelper.CheckAndConsume is O(K) where K < 10 |
| **Total (steady state)** | **< 0.27ms** | | All systems combined, worst case |
| **Total (no violations)** | **< 0.15ms** | | Typical frame with no suspicious activity |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `ValidationLink` | 8 bytes | Player entity |
| **Total on player** | **8 bytes** | |

All validation data lives on the child entity:

| Component | Size | Location |
|-----------|------|----------|
| `ValidationChildTag` | 0 bytes | Child entity |
| `ValidationOwner` | 8 bytes | Child entity |
| `PlayerValidationState` | 24 bytes | Child entity |
| `RateLimitEntry` buffer (InternalCapacity=8) | 96 bytes header + 8 * 12 = 96 data | Child entity |
| `MovementValidationState` | 24 bytes | Child entity |
| `EconomyAuditEntry` buffer (InternalCapacity=16) | 96 bytes header + 16 * 20 = 320 data | Child entity |
| **Total on child** | **~564 bytes** | |

**Analysis:** The player entity archetype is near the 16KB limit (~60+ components + 11 buffers). Adding 8 bytes via `ValidationLink` is safe -- it is the same cost as `SaveStateLink` (EPIC 16.15) and `TalentLink` (EPIC 17.1). The child entity pattern keeps all validation data off the player archetype. The child entity's ~564 bytes is well below the 16KB chunk limit for its own archetype.

**Why not put ValidationConfig fields on the player entity directly?** `PlayerValidationState` alone is 24 bytes, plus `MovementValidationState` (24 bytes), plus buffer headers for `RateLimitEntry` and `EconomyAuditEntry` (~192 bytes). Total would be ~240+ bytes on the player archetype -- unacceptable given the existing budget pressure. The child entity pattern costs only 8 bytes.

---

## File Summary

### New Files (14)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Validation/Components/ValidationLink.cs` | IComponentData (8 bytes on player) |
| 2 | `Assets/Scripts/Validation/Components/ValidationComponents.cs` | IComponentData + IBufferElementData (child entity) |
| 3 | `Assets/Scripts/Validation/Components/ViolationEvent.cs` | IComponentData (transient entities) |
| 4 | `Assets/Scripts/Validation/Components/ValidationConfig.cs` | IComponentData singleton (~64 bytes) |
| 5 | `Assets/Scripts/Validation/Components/ValidationEnums.cs` | Enums + RpcTypeIds constants |
| 6 | `Assets/Scripts/Validation/Core/RateLimitHelper.cs` | Static helper (CheckAndConsume, CreateViolation) |
| 7 | `Assets/Scripts/Validation/Core/BanListManager.cs` | Managed static class (ban list I/O) |
| 8 | `Assets/Scripts/Validation/Core/TeleportImmunityHelper.cs` | Static helper (GrantImmunity) |
| 9 | `Assets/Scripts/Validation/Definitions/ValidationProfile.cs` | ScriptableObject (per-RPC rate limits) |
| 10 | `Assets/Scripts/Validation/Definitions/PenaltyConfig.cs` | ScriptableObject (violation thresholds, ban durations) |
| 11 | `Assets/Scripts/Validation/Definitions/MovementLimits.cs` | ScriptableObject (max speed per state, teleport threshold) |
| 12 | `Assets/Scripts/Validation/Authoring/ValidationAuthoring.cs` | Baker (child entity pattern) |
| 13 | `Assets/Scripts/Validation/Systems/ValidationBootstrapSystem.cs` | SystemBase (InitializationSystemGroup, runs once) |
| 14 | `Assets/Scripts/Validation/Systems/RateLimitRefillSystem.cs` | ISystem, Burst (SimulationSystemGroup, OrderFirst) |
| 15 | `Assets/Scripts/Validation/Systems/MovementValidationSystem.cs` | ISystem (PredictedFixedStepSimulation, after PlayerMovementSystem) |
| 16 | `Assets/Scripts/Validation/Systems/EconomyAuditSystem.cs` | SystemBase (SimulationSystemGroup, after CurrencyTransactionSystem) |
| 17 | `Assets/Scripts/Validation/Systems/CooldownValidationSystem.cs` | SystemBase (SimulationSystemGroup) |
| 18 | `Assets/Scripts/Validation/Systems/ViolationAccumulatorSystem.cs` | ISystem (SimulationSystemGroup, after audit/cooldown/movement) |
| 19 | `Assets/Scripts/Validation/Systems/PenaltyExecutionSystem.cs` | SystemBase (EndSimulationSystemGroup) |
| 20 | `Assets/Scripts/Validation/Systems/ValidationTelemetryBridgeSystem.cs` | SystemBase (PresentationSystemGroup) |
| 21 | `Assets/Scripts/Validation/Systems/ConnectionValidationSystem.cs` | SystemBase (SimulationSystemGroup, UpdateBefore GoInGameSystem) |
| 22 | `Assets/Scripts/Validation/DIG.Validation.asmdef` | Assembly definition |
| 23 | `Assets/Editor/ValidationWorkstation/ValidationWorkstationWindow.cs` | EditorWindow (6 tabs) |
| 24 | `Assets/Editor/ValidationWorkstation/IValidationWorkstationModule.cs` | Interface |
| 25 | `Assets/Editor/ValidationWorkstation/Modules/PlayerMonitorModule.cs` | Editor module |
| 26 | `Assets/Editor/ValidationWorkstation/Modules/RateLimitModule.cs` | Editor module |
| 27 | `Assets/Editor/ValidationWorkstation/Modules/MovementTrailModule.cs` | Editor module |
| 28 | `Assets/Editor/ValidationWorkstation/Modules/EconomyAuditModule.cs` | Editor module |
| 29 | `Assets/Editor/ValidationWorkstation/Modules/BanManagerModule.cs` | Editor module |
| 30 | `Assets/Editor/ValidationWorkstation/Modules/ViolationTimelineModule.cs` | Editor module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | Player prefab (Warrok_Server) | Add `ValidationAuthoring` |
| 2 | `Assets/Scripts/Crafting/Systems/CraftRpcReceiveSystem.cs` | Add rate limit check (~5 lines) |
| 3 | `Assets/Scripts/Dialogue/Systems/DialogueRpcReceiveSystem.cs` | Add rate limit check (~5 lines per RPC type) |
| 4 | `Assets/Scripts/Progression/Systems/StatAllocationRpcReceiveSystem.cs` | Add rate limit check (~5 lines) |
| 5 | `Assets/Scripts/Voxel/Systems/Network/VoxelDamageRpc.cs` | Add rate limit check (~5 lines) |
| 6 | `Assets/Scripts/Player/Systems/RespawnDebugSystem.cs` | Add rate limit check + teleport immunity grant (~8 lines) |
| 7 | `Assets/Scripts/Systems/Network/GoInGameSystem.cs` | Add ban list check before player spawn (~10 lines) |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/ValidationProfile.asset` |
| 2 | `Resources/PenaltyConfig.asset` |
| 3 | `Resources/MovementLimits.asset` |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `CurrencyTransactionSystem` | 16.6 (Economy) | EconomyAuditSystem runs UpdateAfter, logs all transactions with before/after balances |
| `CraftValidationSystem` | 16.13 (Crafting) | CraftRpcReceiveSystem adds rate limit check. Existing validation unchanged. |
| `CraftRpcReceiveSystem` | 16.13 (Crafting) | Rate limit middleware integration (~5 lines) |
| `ResourcePool` | 16.8 (Resources) | Future: resource transaction validation (e.g., mana spend rate capping) |
| `XPAwardSystem` | 16.14 (Progression) | XP grants are server-only (KillCredited via ECB). Safe today. Future: validate XPGrantAPI calls via rate limiter |
| `StatAllocationRpcReceiveSystem` | 16.14 (Progression) | Rate limit middleware integration (~5 lines) |
| `TalentRpcReceiveSystem` | 17.1 (Skill Trees) | Rate limit middleware integration when EPIC 17.1 ships |
| `TradeRpcReceiveSystem` | 17.3 (Trading) | Rate limit middleware + economy audit integration when EPIC 17.3 ships |
| `PvP match systems` | 17.10 (PvP) | Movement validation + cooldown validation critical for match integrity |
| `Telemetry export` | 17.13 (Analytics) | ValidationTelemetryBridgeSystem feeds violation data to analytics pipeline |
| `LoadSystem` | 16.15 (Persistence) | Must call `TeleportImmunityHelper.GrantImmunity()` after restoring player position |

---

## Backward Compatibility

| Scenario | Behavior |
|----------|----------|
| Entity without `ValidationLink` | All validation systems skip. RateLimitHelper.CheckAndConsume returns `true` (allow). Zero overhead. |
| No `ValidationProfile` in Resources | `ValidationBootstrapSystem` uses hardcoded defaults (2 tokens/s, burst 5). Logs warning. |
| Old player prefab without `ValidationAuthoring` | No child entity created. Rate limiting disabled for that player. Other players still protected. |
| Existing RPC systems without rate limit integration | Continue to work exactly as before. No rate limiting, no violation tracking. Integration is opt-in. |

---

## Verification Checklist

### Rate Limiting
- [ ] Send 10 CraftRpcs in one frame: first 5 accepted, last 5 rejected
- [ ] Rate-limited RPCs create ViolationEvent entities
- [ ] Token bucket refills over time: after 2 seconds, 4 more craft RPCs accepted (at 2/s)
- [ ] Unregistered RPC type uses default rate limit (2/s, burst 5)
- [ ] ValidationProfile SO changes reflected after reimport

### Movement Validation
- [ ] Normal movement: no violations (position delta within tolerance)
- [ ] Sprint speed: 10 m/s * 1.3 tolerance = 13 m/s max, no violation
- [ ] Speed hack (20 m/s while standing): accumulated error grows, violation fires
- [ ] Teleport (100m jump): immediate ViolationEvent(Movement, severity=1.0)
- [ ] Server-granted teleport (respawn): TeleportImmunityHelper prevents false positive
- [ ] Teleport immunity expires after grace period

### Economy Audit
- [ ] Gold purchase: EconomyAuditEntry logged with correct before/after
- [ ] Craft cost deduction: audit entry recorded
- [ ] Reconciliation: artificially desync balance → ViolationEvent(Economy) fires
- [ ] Audit buffer ring: 65+ entries → oldest half trimmed
- [ ] Editor: audit log shows transaction history sortable by tick

### Violation Accumulator
- [ ] Single mild violation (severity=0.3, weight=1.0): score = 0.3
- [ ] Score decays over time toward 0
- [ ] 20 rapid rate-limit violations: score crosses KickThreshold
- [ ] Movement violation with MovementWeight=2.0: score increases at 2x rate
- [ ] Economy violation with EconomyWeight=3.0: score increases at 3x rate

### Penalty Execution
- [ ] Score crosses WarnThreshold: warning message sent to client
- [ ] Warning cooldown: no duplicate warnings within WarnCooldownSeconds
- [ ] Score crosses KickThreshold: player disconnected
- [ ] ConsecutiveKicks reaches threshold: temp ban applied
- [ ] Temp-banned player attempts reconnect: rejected at connection gate
- [ ] Ban expires: player can reconnect after TempBanDurationMinutes

### Ban List
- [ ] BanListManager.IsBanned() returns true for active bans
- [ ] Temp ban expiry: returns false after duration
- [ ] Perma ban: never expires
- [ ] Ban list persists across server restart (JSON file)
- [ ] Editor: Ban Manager tab shows active bans with manual controls

### Integration
- [ ] CraftRpcReceiveSystem rate limit: 6th craft in burst rejected
- [ ] DialogueRpcReceiveSystem rate limit: rapid dialogue skipping rejected
- [ ] StatAllocationRpcReceiveSystem rate limit: stat spam rejected
- [ ] GoInGameSystem ban check: banned player cannot join
- [ ] LoadSystem teleport immunity: no false positive after save load

### Editor Tooling
- [ ] Validation Workstation: Player Monitor shows live scores (color-coded)
- [ ] Validation Workstation: Rate Limit tab shows token bucket bars per RPC type
- [ ] Validation Workstation: Movement Trail overlay visible in SceneView
- [ ] Validation Workstation: Economy Audit log sortable, exportable to CSV
- [ ] Validation Workstation: Ban Manager allows manual kick/ban/unban
- [ ] Validation Workstation: Violation Timeline shows events as colored dots

### Performance
- [ ] RateLimitRefillSystem: < 0.05ms in Profiler with 64 connected players
- [ ] MovementValidationSystem: < 0.1ms in Profiler with 64 players
- [ ] EconomyAuditSystem: < 0.05ms on transaction frames
- [ ] Total validation overhead: < 0.3ms worst case
- [ ] No frame hitch from validation systems

### Archetype Safety
- [ ] Only +8 bytes on player entity (`ValidationLink`)
- [ ] No ghost bake errors after adding `ValidationAuthoring`
- [ ] Child entity created correctly with all validation components
- [ ] Subscene reimport succeeds without archetype size errors
