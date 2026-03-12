# EPIC 17.10: PvP Arena & Ranking System

**Status:** PLANNED
**Priority:** Medium (Competitive Endgame)
**Dependencies:**
- `CollisionGameSettings` IComponentData singleton (existing -- `DIG.Player.Components.CollisionGameSettings.cs`, FriendlyFireEnabled/TeamCollisionEnabled/SoftCollisionForceMultiplier)
- `CollisionGameSettingsAuthoring` MonoBehaviour + Baker (existing -- `DIG.Player.Authoring.CollisionGameSettingsAuthoring.cs`)
- `TeamId` IComponentData (existing -- `DIG.Player.Components.CollisionGameSettings.cs`, byte Value, team-based collision filtering)
- `IndicatorThemeContext.IsPvPMode` + `FriendlyFireEnabled` (existing -- `DIG.Targeting.Theming.IndicatorThemeContext.cs`, game state flags for combat indicator theming)
- `TargetFaction.Hostile` enum value (existing -- `DIG.Targeting.Theming.IndicatorThemeContext.cs`, value=3, PvP opponent faction)
- `KillCredited` IComponentData event (existing -- `Player.Components.KillAttribution.cs`, Ghost:AllPredicted, Victim/VictimPosition/ServerTick, ephemeral)
- `AssistCredited` IComponentData event (existing -- `Player.Components.KillAttribution.cs`, Ghost:AllPredicted, Victim/DamageDealt/ServerTick, ephemeral)
- `DeathTransitionSystem` (existing -- creates KillCredited/AssistCredited via EndSimulationECB)
- `DamageApplySystem` (existing -- `Assets/Scripts/Player/Systems/DamageApplySystem.cs`, server-only Burst, player damage pipeline)
- `DamageEvent` IBufferElementData (existing -- ghost-replicated AllPredicted damage pipeline)
- `CombatResultEvent` pipeline (existing -- PendingCombatHit -> CombatResolutionSystem -> CombatResultEvent -> DamageApplicationSystem)
- `Health` IComponentData (existing -- `Player.Components`, Ghost:All, 8 bytes)
- `PlayerTag` IComponentData (existing -- player entity marker)
- `CharacterAttributes` IComponentData (existing -- `DIG.Combat.Components`, Ghost:All, Str/Dex/Int/Vit/Level, 20 bytes)
- `PlayerProgression` IComponentData (existing -- EPIC 16.14, Ghost:AllPredicted, CurrentXP/TotalXPEarned/UnspentStatPoints/RestedXP, 16 bytes)
- `AttackStats` / `DefenseStats` IComponentData (existing -- `DIG.Combat.Components`, Ghost:All, quantized floats)
- `PlayerEquippedStats` IComponentData (existing -- `DIG.Items`, EPIC 16.6, 32 bytes)
- `CombatUIBridgeSystem` / `CombatUIRegistry` pattern (existing -- managed bridge pattern reference)
- `DamageVisualQueue` (existing -- static NativeQueue bridge for ECS -> UI events)
- `SaveStateLink` child entity pattern (existing -- `DIG.Persistence.Components.SaveStateComponents.cs`, 8 bytes on player)
- `ISaveModule` persistence interface (existing -- EPIC 16.15, TypeId-based modular serialization)
- `PlayerProximityCollisionSystem` (existing -- reads CollisionGameSettings for player-vs-player collision filtering)
- `GhostOwner.NetworkId` (existing -- NetCode player identity)
- `ItemRegistryBootstrapSystem` (existing -- bootstrap singleton pattern reference)
- `SurfaceGameplayConfigSystem` (existing -- BlobAsset bootstrap pattern reference)

**Feature:** A server-authoritative PvP arena system supporting multiple game modes (FreeForAll, TeamDeathmatch, CapturePoint, Duel), match lifecycle management with warmup/overtime/results phases, per-player kill/death/assist statistics, Elo-based ranking with tier progression, team-based spawn points with invulnerability protection, optional equipment normalization for competitive fairness, and anti-grief measures (AFK detection, leaver penalty, spawn camping prevention). Integrates with existing `CollisionGameSettings.FriendlyFireEnabled` for damage toggling and `KillCredited`/`AssistCredited` for scoring.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `CollisionGameSettings` singleton | `CollisionGameSettings.cs` | Fully implemented | FriendlyFireEnabled, TeamCollisionEnabled, SoftCollisionForceMultiplier. Controls player-vs-player collision globally |
| `TeamId` IComponentData | `CollisionGameSettings.cs` | Fully implemented | byte Value, `IsSameTeam()` helper. Used by PlayerProximityCollisionSystem |
| `CollisionGameSettingsAuthoring` | `CollisionGameSettingsAuthoring.cs` | Fully implemented | Bakes singleton |
| `IndicatorThemeContext.IsPvPMode` | `IndicatorThemeContext.cs` | Field defined | Never written to (always false) |
| `IndicatorThemeContext.FriendlyFireEnabled` | `IndicatorThemeContext.cs` | Field defined | Never written to |
| `TargetFaction.Hostile` | `IndicatorThemeContext.cs` | Enum value=3 | Never assigned to any entity |
| `KillCredited` event | `KillAttribution.cs` | Fully implemented | Ghost:AllPredicted, Victim+Position+Tick. Created by DeathTransitionSystem via EndSimulationECB |
| `AssistCredited` event | `KillAttribution.cs` | Fully implemented | Ghost:AllPredicted, Victim+DamageDealt+Tick |
| `RecentAttackerElement` buffer | `KillAttribution.cs` | Fully implemented | InternalBufferCapacity=8, tracks damage sources for assist attribution |
| `PlayerProximityCollisionSystem` | `PlayerProximityCollisionSystem.cs` | Fully implemented | Reads CollisionGameSettings, filters by TeamId |
| `DamageApplySystem` | `DamageApplySystem.cs` | Burst, server-only | Player damage pipeline. **DO NOT MODIFY** |
| `Health` | `Player.Components` | Ghost:All | Replicated to all clients |
| `CharacterAttributes.Level` | `CombatStatComponents.cs` | Ghost:All | Level-based matchmaking possible |
| `AttackStats` / `DefenseStats` | `CombatStatComponents.cs` | Ghost:All, quantized | Stat normalization target |
| `XPAwardSystem` | `XPAwardSystem.cs` | Reads KillCredited | Awards XP on kill -- PvP kills should use different XP formula |
| `SaveStateLink` child entity | `SaveStateComponents.cs` | 8 bytes on player | Pattern for PvPRankingLink |

### What's Missing

- **No match lifecycle** -- no warmup, active phase, overtime, or results screen
- **No game mode logic** -- no FreeForAll, TeamDeathmatch, CapturePoint, or Duel mode
- **No PvP scoring** -- KillCredited exists but no system tracks PvP kills/deaths/assists
- **No ranking system** -- no Elo, MMR, tier, or ranked/unranked distinction
- **No spawn system** -- no team-based spawn points, no respawn timer, no invulnerability
- **No equipment normalization** -- no stat scaling mode for competitive fairness
- **No anti-grief measures** -- no AFK detection, leaver penalty, or spawn camping prevention
- **No PvP-specific collision toggle** -- CollisionGameSettings exists but nothing sets it for PvP mode
- **No PvP matchmaking** -- no queue, no lobby, no match creation
- **No PvP UI** -- no scoreboard, kill feed, match timer, ranking display
- **No PvP save data** -- Elo/tier/win-loss not persisted

---

## Problem

DIG has a complete damage pipeline, kill attribution, team collision filtering, and even PvP-related fields (`IsPvPMode`, `FriendlyFireEnabled`, `TargetFaction.Hostile`) -- but none of them are wired into an actual PvP game mode. `CollisionGameSettings.FriendlyFireEnabled` defaults to true but there is no match system to toggle it contextually. `KillCredited` fires on every kill but nothing distinguishes PvP kills from PvE kills. There is no competitive loop.

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `CollisionGameSettings.FriendlyFireEnabled` toggle | No system sets it for PvP mode entry/exit |
| `TeamId` on player entities | No system assigns teams for PvP matches |
| `KillCredited` / `AssistCredited` events | No system tracks PvP-specific K/D/A stats |
| `IndicatorThemeContext.IsPvPMode` | Never set to true -- PvP indicators never activate |
| `TargetFaction.Hostile` enum | Never assigned -- PvP opponents use default faction |
| `DamageApplySystem` / `CombatResolutionSystem` | No PvP damage scaling, no spawn protection filtering |
| `PlayerProximityCollisionSystem` reads TeamId | No match system assigns teams |
| `XPAwardSystem` reads KillCredited | No PvP-specific XP formula (should differ from PvE) |
| `RecentAttackerElement` for assist tracking | No PvP assist scoring |

**The gap:** Players cannot engage in structured PvP combat. There is no way to start a match, track scores, determine winners, or progress through a competitive ranking. The infrastructure for player-vs-player damage exists but has no orchestration layer.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  PvPArenaConfigSO       PvPMapDefinitionSO       PvPRankingConfigSO
  (game mode params,     (per-map spawn points,   (Elo K-factor,
   score limits,          capture points,           tier thresholds,
   timers, normalization) team zones)               placement matches)
           |                    |                         |
           └────── PvPBootstrapSystem ────────────────────┘
                   (loads from Resources/, builds BlobAssets,
                    creates PvPConfigSingleton,
                    follows SurfaceGameplayConfigSystem pattern)
                              |
                    ECS DATA LAYER
  PvPMatchState (singleton)    PvPPlayerStats        PvPTeam
  (Ghost:All, MatchPhase,     (on player entity,     (on player entity,
   Timer, Scores, GameMode)    Ghost:AllPredicted)    TeamId + SpawnIndex)
           |                           |                    |
  PvPConfigSingleton           PvPSpawnProtection     PvPRanking
  (BlobRef to arena config)    (IEnableableComponent) (on CHILD entity
                                                       via PvPRankingLink)
                              |
                    SYSTEM PIPELINE (SimulationSystemGroup, Server|Local)
                              |
  PvPMatchSystem ─── match lifecycle state machine
  (Waiting → Warmup → Active → Overtime → Results → Ended)
           |
  PvPCollisionSystem ─── toggles CollisionGameSettings for PvP mode
  (enables player-vs-player damage, sets IsPvPMode flag)
           |
  PvPScoringSystem ─── reads KillCredited/AssistCredited
  (updates PvPPlayerStats, checks win conditions)
           |
  PvPSpawnSystem ─── handles respawn with protection
  (team-based spawn selection, invulnerability timer)
           |
  PvPObjectiveSystem ─── capture point logic (CapturePoint mode)
  (zone control scoring, point accumulation)
           |
  PvPNormalizationSystem ─── optional stat equalization
  (overrides AttackStats/DefenseStats/Health.Max for fair play)
           |
  PvPAntiGriefSystem ─── AFK detection, leaver penalty
  (input monitoring, disconnect tracking)
           |
  PvPRankingSystem (Server only) ─── post-match Elo calculation
  (reads final stats, computes Elo delta, updates tier)
                              |
                    PRESENTATION LAYER (PresentationSystemGroup)
                              |
  PvPUIBridgeSystem → PvPUIRegistry → IPvPUIProvider
  (managed, reads match state + player stats + rankings)
           |
  ScoreboardView / MatchTimerView / KillFeedView / RankingView (MonoBehaviours)
```

### Data Flow (Match Start -> Kill -> Score -> Match End -> Ranking)

```
Match Creation (Server):
  1. PvPMatchSystem: Creates PvPMatchState singleton
     - Sets MatchPhase = WaitingForPlayers
     - Assigns GameMode, MapId, MaxScore, MatchDuration
     - PvPCollisionSystem: Sets CollisionGameSettings.FriendlyFireEnabled = true
     - PvPCollisionSystem: Sets IndicatorThemeContext.IsPvPMode = true

  2. Player joins → PvPMatchSystem assigns PvPTeam.TeamId
     - Writes TeamId component (existing) for collision filtering
     - Assigns PvPSpawnPoint based on team

  3. All players ready → MatchPhase = Warmup (30s countdown)
     - PvPSpawnSystem: Teleports players to team spawn points
     - PvPSpawnProtection enabled (invulnerable during warmup)
     - PvPNormalizationSystem: If enabled, overrides stats

Frame N (Active Match):
  4. MatchPhase = Active, Timer counts down
  5. Player A kills Player B:
     - DeathTransitionSystem: KillCredited on A, AssistCredited on assisters
     - PvPScoringSystem (next frame after ECB playback):
       - Reads KillCredited where Victim has PlayerTag (PvP kill, not PvE)
       - A.PvPPlayerStats.Kills++
       - B.PvPPlayerStats.Deaths++
       - Assist contributors: PvPPlayerStats.Assists++
       - TeamScores[A.team]++ (TeamDeathmatch)
       - Checks MaxScore → if reached, MatchPhase = Results
     - PvPSpawnSystem: Queues respawn for B with SpawnDelay (5s)
       - After delay: teleports to spawn point, enables PvPSpawnProtection (3s)

  6. Timer reaches 0:
     - If scores tied and OvertimeEnabled: MatchPhase = Overtime
     - Otherwise: MatchPhase = Results

Match End (Server):
  7. MatchPhase = Results (15s display)
     - PvPRankingSystem: Computes Elo delta for each player
       - Winner: Elo += K * (1 - expectedWin)
       - Loser: Elo -= K * (expectedWin)
       - Updates PvPTier based on new Elo thresholds
     - PvPUIBridgeSystem: Shows match results screen

  8. MatchPhase = Ended
     - PvPMatchSystem: Clears PvPPlayerStats, resets PvPTeam
     - PvPCollisionSystem: Restores CollisionGameSettings defaults
     - PvPNormalizationSystem: Removes stat overrides
```

### Critical System Ordering Chain

```
PvPMatchSystem [SimulationSystemGroup, OrderFirst]
    ↓
PvPCollisionSystem [UpdateAfter(typeof(PvPMatchSystem))]
    ↓
PvPNormalizationSystem [UpdateAfter(typeof(PvPCollisionSystem))]
    ↓
[existing] DamageApplySystem / CombatResolutionSystem (SimulationSystemGroup)
    ↓
[existing] DeathTransitionSystem → KillCredited/AssistCredited via EndSimulationECB
    ↓ (next frame, ECB playback)
PvPScoringSystem [UpdateAfter(typeof(DeathTransitionSystem))]
    ↓
PvPSpawnSystem [UpdateAfter(typeof(PvPScoringSystem))]
    ↓
PvPObjectiveSystem [UpdateAfter(typeof(PvPSpawnSystem))]
    ↓
PvPAntiGriefSystem [UpdateAfter(typeof(PvPObjectiveSystem))]
    ↓
PvPRankingSystem (Server only) [UpdateAfter(typeof(PvPAntiGriefSystem))]
```

---

## ECS Components

### Singleton: Match State

**File:** `Assets/Scripts/PvP/Components/PvPMatchState.cs`

```csharp
/// <summary>
/// Singleton controlling the active PvP match. Ghost-replicated to all clients
/// so every player sees match phase, timer, and scores.
/// 32 bytes.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct PvPMatchState : IComponentData
{
    [GhostField] public PvPMatchPhase Phase;    // byte (enum)
    [GhostField] public PvPGameMode GameMode;   // byte (enum)
    [GhostField] public byte MapId;             // Arena map identifier
    [GhostField] public byte OvertimeEnabled;   // 0 = no overtime, 1 = enabled
    [GhostField(Quantization = 100)] public float Timer;   // Countdown seconds remaining
    [GhostField] public float MatchDuration;    // Total match time (seconds)
    [GhostField] public int MaxScore;           // Score limit (kills or points)
    [GhostField] public int TeamScore0;         // FFA: player 0 score, TDM: team 0
    [GhostField] public int TeamScore1;         // FFA: player 1 score, TDM: team 1
    [GhostField] public int TeamScore2;         // FFA: player 2 score / unused
    [GhostField] public int TeamScore3;         // FFA: player 3 score / unused
}
// Total: 1+1+1+1+4+4+4+4+4+4+4 = 32 bytes
```

**File:** `Assets/Scripts/PvP/Components/PvPMatchPhase.cs`

```csharp
public enum PvPMatchPhase : byte
{
    WaitingForPlayers = 0,
    Warmup            = 1,  // 30s countdown, players invulnerable
    Active            = 2,  // Combat enabled, scoring active
    Overtime          = 3,  // Tied at time limit, sudden death or extended
    Results           = 4,  // 15s post-match display, ranking calculated
    Ended             = 5   // Cleanup, return to lobby
}

public enum PvPGameMode : byte
{
    FreeForAll       = 0,   // Every player for themselves, kill limit
    TeamDeathmatch   = 1,   // 2 teams, combined kill score
    CapturePoint     = 2,   // Control zones, point accumulation
    Duel             = 3    // 1v1, best of N rounds
}

public enum PvPTier : byte
{
    Bronze   = 0,   // Elo 0-999
    Silver   = 1,   // Elo 1000-1499
    Gold     = 2,   // Elo 1500-1999
    Platinum = 3,   // Elo 2000-2499
    Diamond  = 4,   // Elo 2500-2999
    Master   = 5    // Elo 3000+
}
```

### On Player Entity

**File:** `Assets/Scripts/PvP/Components/PvPPlayerStats.cs`

```csharp
/// <summary>
/// Per-match PvP statistics on the player entity. AllPredicted so the owning
/// client sees their own K/D/A without round-trip latency.
/// Reset at match start, frozen at match end for results display.
/// 24 bytes.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PvPPlayerStats : IComponentData
{
    [GhostField] public short Kills;           // 2 bytes
    [GhostField] public short Deaths;          // 2 bytes
    [GhostField] public short Assists;         // 2 bytes
    [GhostField] public short ObjectiveScore;  // 2 bytes (capture points held, etc.)
    [GhostField(Quantization = 10)] public float DamageDealt;    // 4 bytes
    [GhostField(Quantization = 10)] public float DamageReceived; // 4 bytes
    [GhostField(Quantization = 10)] public float HealingDone;    // 4 bytes
    [GhostField] public int MatchScore;        // 4 bytes (composite score for leaderboard)
}
// Total: 2+2+2+2+4+4+4+4 = 24 bytes
```

**File:** `Assets/Scripts/PvP/Components/PvPTeam.cs`

```csharp
/// <summary>
/// PvP team assignment on the player entity. TeamId mirrors the existing
/// TeamId component for collision filtering. SpawnPointIndex selects
/// which spawn point this player uses.
/// 4 bytes.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PvPTeam : IComponentData
{
    [GhostField] public byte TeamId;           // 0=no team (FFA), 1-4=team assignment
    [GhostField] public byte SpawnPointIndex;  // Index into team's spawn point array
    public byte Padding0;
    public byte Padding1;
}
// Total: 4 bytes
```

**File:** `Assets/Scripts/PvP/Components/PvPSpawnProtection.cs`

```csharp
/// <summary>
/// IEnableableComponent for spawn invulnerability. Baked disabled.
/// Enabled by PvPSpawnSystem on respawn, disabled when ExpirationTick
/// is reached or player deals damage (whichever comes first).
/// 4 bytes.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PvPSpawnProtection : IComponentData, IEnableableComponent
{
    [GhostField] public uint ExpirationTick;   // NetworkTick when protection expires
}
// Total: 4 bytes
```

**File:** `Assets/Scripts/PvP/Components/PvPRespawnTimer.cs`

```csharp
/// <summary>
/// IEnableableComponent tracking respawn delay after death. Baked disabled.
/// Enabled by PvPScoringSystem on PvP death, PvPSpawnSystem reads it.
/// 4 bytes.
/// </summary>
public struct PvPRespawnTimer : IComponentData, IEnableableComponent
{
    public uint RespawnAtTick;   // NetworkTick when respawn triggers
}
// Total: 4 bytes
```

**File:** `Assets/Scripts/PvP/Components/PvPStatOverride.cs`

```csharp
/// <summary>
/// Stores the player's original stats before normalization. Used to restore
/// stats when leaving PvP mode. IEnableableComponent -- enabled only when
/// normalization is active.
/// 20 bytes.
/// </summary>
public struct PvPStatOverride : IComponentData, IEnableableComponent
{
    public float OriginalMaxHealth;
    public float OriginalAttackPower;
    public float OriginalSpellPower;
    public float OriginalDefense;
    public float OriginalArmor;
}
// Total: 20 bytes
```

**File:** `Assets/Scripts/PvP/Components/PvPAntiGriefState.cs`

```csharp
/// <summary>
/// Tracks AFK detection and leaver penalty state per player.
/// 8 bytes.
/// </summary>
public struct PvPAntiGriefState : IComponentData
{
    public float TimeSinceLastInput;   // Seconds without movement/attack input
    public byte AFKWarningCount;       // Warnings issued (kicked at 3)
    public byte LeaverPenaltyCount;   // Historical leaves (affects queue cooldown)
    public byte IsAFK;                 // 0 = active, 1 = AFK flagged
    public byte Padding;
}
// Total: 8 bytes
```

### On Spawn Point Entities (Placed in Arena Subscene)

**File:** `Assets/Scripts/PvP/Components/PvPSpawnPoint.cs`

```csharp
/// <summary>
/// Placed on entities in arena subscenes marking valid spawn locations.
/// PvPSpawnSystem queries these to teleport respawning players.
/// 8 bytes.
/// </summary>
public struct PvPSpawnPoint : IComponentData
{
    public byte TeamId;           // Which team uses this spawn (0 = any)
    public byte SpawnIndex;       // Index within team's spawn array
    public byte IsActive;         // 0 = disabled (occupied/camping), 1 = available
    public byte Padding;
    public uint LastUsedTick;     // NetworkTick of last use (anti-camping rotation)
}
// Total: 8 bytes
```

### On Capture Point Entities (CapturePoint Mode)

**File:** `Assets/Scripts/PvP/Components/PvPCaptureZone.cs`

```csharp
/// <summary>
/// Capture point zone entity. PvPObjectiveSystem reads overlapping
/// player collisions to determine control.
/// 16 bytes.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct PvPCaptureZone : IComponentData
{
    [GhostField] public byte ZoneId;              // Unique zone identifier
    [GhostField] public byte ControllingTeam;     // 0 = neutral, 1-4 = team
    [GhostField] public byte ContestingTeam;      // Team currently contesting
    [GhostField] public byte PlayersInZone;       // Count of players inside
    [GhostField(Quantization = 100)] public float CaptureProgress;  // 0.0 - 1.0
    [GhostField(Quantization = 10)] public float PointsPerSecond;   // Score rate when controlled
    public int Padding;
}
// Total: 16 bytes
```

### On Player Child Entity (Ranking Data)

**File:** `Assets/Scripts/PvP/Components/PvPRankingComponents.cs`

```csharp
/// <summary>
/// Link from player entity to child entity holding ranking data.
/// Same pattern as SaveStateLink and TalentLink.
/// 8 bytes on player entity.
/// </summary>
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PvPRankingLink : IComponentData
{
    [GhostField] public Entity RankingChild;
}
// Total: 8 bytes on player entity

/// <summary>
/// Tag marking ranking child entities.
/// </summary>
public struct PvPRankingTag : IComponentData { }
// Total: 0 bytes

/// <summary>
/// Back-reference from ranking child to player entity.
/// </summary>
public struct PvPRankingOwner : IComponentData
{
    public Entity Owner;
}
// Total: 8 bytes

/// <summary>
/// Persistent ranking data on child entity. NOT on player archetype.
/// Persisted via PvPRankingSaveModule (TypeId=15).
/// 24 bytes.
/// </summary>
public struct PvPRanking : IComponentData
{
    public int Elo;            // Current Elo rating (starts at 1200)
    public PvPTier Tier;       // Derived from Elo thresholds
    public byte Padding0;
    public byte Padding1;
    public byte Padding2;
    public int Wins;           // Total ranked wins
    public int Losses;         // Total ranked losses
    public int WinStreak;      // Current win streak (resets on loss)
    public int HighestElo;     // Peak Elo achieved
}
// Total: 4+1+3+4+4+4+4 = 24 bytes on CHILD entity
```

### Transient Request Components

**File:** `Assets/Scripts/PvP/Components/PvPRequestComponents.cs`

```csharp
/// <summary>
/// Transient entity requesting a PvP match start.
/// Created by matchmaking or manual lobby trigger.
/// </summary>
public struct PvPMatchRequest : IComponentData
{
    public PvPGameMode GameMode;
    public byte MapId;
    public byte MaxPlayers;       // 2 (Duel), 8 (FFA), 12 (TDM), 16 (CapturePoint)
    public byte NormalizationMode; // 0 = off, 1 = full normalization
    public int MaxScore;
    public float MatchDuration;   // Seconds
}

/// <summary>
/// Transient entity created when a match ends. Read by PvPUIBridgeSystem
/// for results display, then destroyed.
/// </summary>
public struct PvPMatchResult : IComponentData
{
    public PvPGameMode GameMode;
    public byte WinningTeam;      // 0 = draw, 1-4 = winning team
    public byte PlayerCount;
    public byte Padding;
    public float MatchDurationActual;
}

/// <summary>
/// RPC: Client requests to join PvP queue.
/// </summary>
public struct PvPJoinQueueRpc : IRpcCommand
{
    public PvPGameMode GameMode;
    public byte PreferredMapId;   // 0 = any
}

/// <summary>
/// RPC: Client requests to leave PvP queue or forfeit match.
/// </summary>
public struct PvPLeaveRpc : IRpcCommand
{
    public byte Reason;           // 0 = voluntary, 1 = disconnect
}
```

### Singleton (BlobAssets)

**File:** `Assets/Scripts/PvP/Config/PvPConfigBlob.cs`

```csharp
public struct PvPConfigSingleton : IComponentData
{
    public BlobAssetReference<PvPConfigBlob> Config;
}

public struct PvPConfigBlob
{
    // Match Timers
    public float WarmupDuration;          // Default 30s
    public float ResultsDuration;         // Default 15s
    public float OvertimeDuration;        // Default 60s (0 = sudden death)
    public float RespawnDelay;            // Default 5s
    public float SpawnProtectionDuration; // Default 3s

    // Scoring
    public int FreeForAllKillLimit;       // Default 20
    public int TeamDeathmatchKillLimit;   // Default 50
    public int CapturePointScoreLimit;    // Default 1000
    public int DuelRounds;               // Default 5 (best of)

    // Normalization
    public float NormalizedMaxHealth;     // Default 1000
    public float NormalizedAttackPower;   // Default 50
    public float NormalizedSpellPower;    // Default 50
    public float NormalizedDefense;       // Default 30
    public float NormalizedArmor;         // Default 20

    // Anti-grief
    public float AFKTimeoutSeconds;       // Default 60s
    public int AFKWarningsBeforeKick;     // Default 3
    public float LeaverPenaltyCooldown;   // Default 300s (5min queue ban)
    public int SpawnCampingRadius;        // Default 15 (meters from spawn)
    public float SpawnCampingWindow;      // Default 5s (kills within window = camping)

    // Ranking
    public int EloStarting;              // Default 1200
    public int EloKFactor;               // Default 32
    public int EloKFactorHighRating;     // Default 16 (above 2400)
    public int EloHighRatingThreshold;   // Default 2400
    public int PlacementMatchCount;      // Default 10
    public float PlacementKMultiplier;   // Default 2.0 (higher K during placements)
    public BlobArray<int> TierThresholds; // [0]=Bronze, [1]=Silver, etc.

    // XP
    public float PvPKillXPMultiplier;    // Default 0.5 (PvP kills worth 50% of PvE)
    public float PvPWinBonusXP;          // Default 500 (bonus XP for winning)
    public float PvPLossBonusXP;         // Default 100 (consolation XP)

    // Map Data
    public BlobArray<PvPMapBlob> Maps;
}

public struct PvPMapBlob
{
    public byte MapId;
    public BlobString MapName;
    public byte MaxPlayers;
    public byte TeamCount;                // 0 = FFA, 2/4 = team modes
    public BlobArray<PvPSpawnPointBlob> SpawnPoints;
    public BlobArray<PvPCaptureZoneBlob> CaptureZones;
}

public struct PvPSpawnPointBlob
{
    public byte TeamId;
    public byte SpawnIndex;
    public float3 Position;
    public quaternion Rotation;
}

public struct PvPCaptureZoneBlob
{
    public byte ZoneId;
    public float3 Position;
    public float Radius;
    public float PointsPerSecond;
}
```

---

## ScriptableObjects

### PvPArenaConfigSO

**File:** `Assets/Scripts/PvP/Definitions/PvPArenaConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/PvP/Arena Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| WarmupDuration | float | 30 | Warmup phase countdown (seconds) |
| ResultsDuration | float | 15 | Post-match results display (seconds) |
| OvertimeDuration | float | 60 | Overtime duration (0 = sudden death) |
| RespawnDelay | float | 5 | Seconds before respawn after death |
| SpawnProtectionDuration | float | 3 | Invulnerability after spawn (seconds) |
| FreeForAllKillLimit | int | 20 | Kill limit for FFA mode |
| TeamDeathmatchKillLimit | int | 50 | Combined kill limit for TDM |
| CapturePointScoreLimit | int | 1000 | Score limit for CapturePoint mode |
| DuelRounds | int | 5 | Best of N for Duel mode |
| NormalizationEnabled | bool | false | Enable stat normalization |
| NormalizedMaxHealth | float | 1000 | Normalized health value |
| NormalizedAttackPower | float | 50 | Normalized attack power |
| NormalizedSpellPower | float | 50 | Normalized spell power |
| NormalizedDefense | float | 30 | Normalized defense value |
| NormalizedArmor | float | 20 | Normalized armor value |
| AFKTimeoutSeconds | float | 60 | AFK detection threshold |
| AFKWarningsBeforeKick | int | 3 | Warnings before auto-kick |
| LeaverPenaltyCooldown | float | 300 | Queue ban for leavers (seconds) |
| PvPKillXPMultiplier | float | 0.5 | PvP kill XP relative to PvE |
| PvPWinBonusXP | float | 500 | Bonus XP for match winner |
| PvPLossBonusXP | float | 100 | Consolation XP for losers |

### PvPMapDefinitionSO

**File:** `Assets/Scripts/PvP/Definitions/PvPMapDefinitionSO.cs`

```
[CreateAssetMenu(menuName = "DIG/PvP/Map Definition")]
```

| Field | Type | Purpose |
|-------|------|---------|
| MapId | byte | Unique map identifier |
| MapName | string | Display name |
| MapDescription | string | UI description |
| MaxPlayers | byte | Max concurrent players (2/8/12/16) |
| TeamCount | byte | 0 = FFA, 2 or 4 = team modes |
| SupportedModes | PvPGameMode[] | Which game modes this map supports |
| SpawnPoints | PvPSpawnPointEntry[] | Team spawn positions/rotations |
| CaptureZones | PvPCaptureZoneEntry[] | Capture point positions (CapturePoint mode) |
| ArenaSubscenePath | string | Path to arena subscene asset |

```
[Serializable]
PvPSpawnPointEntry: { TeamId (byte), SpawnIndex (byte), Position (Vector3), Rotation (Quaternion) }
PvPCaptureZoneEntry: { ZoneId (byte), Position (Vector3), Radius (float), PointsPerSecond (float) }
```

### PvPRankingConfigSO

**File:** `Assets/Scripts/PvP/Definitions/PvPRankingConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/PvP/Ranking Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| StartingElo | int | 1200 | Initial Elo for new players |
| KFactor | int | 32 | Elo K-factor for standard matches |
| KFactorHighRating | int | 16 | Reduced K above high rating threshold |
| HighRatingThreshold | int | 2400 | Elo threshold for reduced K |
| PlacementMatchCount | int | 10 | Matches before stable ranking |
| PlacementKMultiplier | float | 2.0 | K multiplier during placements |
| TierThresholds | int[] | [0,1000,1500,2000,2500,3000] | Elo thresholds for Bronze/Silver/Gold/Platinum/Diamond/Master |
| WinStreakBonus | int | 5 | Extra Elo per win in streak of 3+ |
| MaxWinStreakBonus | int | 25 | Cap on streak bonus |

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Client|Local):
  PvPBootstrapSystem                      — loads SOs from Resources/, builds BlobAssets (runs once)

SimulationSystemGroup (Server|Local):
  [OrderFirst]
  PvPMatchSystem                          — match lifecycle state machine (Waiting→Warmup→Active→...)
  PvPCollisionSystem                      — toggles CollisionGameSettings, sets IsPvPMode
  PvPNormalizationSystem                  — overrides/restores player stats for fair play
  PvPSpawnProtectionCheckSystem           — disables expired spawn protection, or on attack
  [existing] DamageApplySystem            — player damage (UNCHANGED, reads DamageEvent)
  [existing] CombatResolutionSystem       — combat hit resolution (UNCHANGED)
  [existing] DeathTransitionSystem        — fires KillCredited/AssistCredited (UNCHANGED)
  [after ECB playback]
  PvPScoringSystem                        — reads KillCredited, updates PvPPlayerStats, checks win
  PvPSpawnSystem                          — handles respawn timers + teleport + protection
  PvPObjectiveSystem                      — capture point zone logic (CapturePoint mode only)
  PvPAntiGriefSystem                      — AFK detection, leaver tracking
  PvPRankingSystem (ServerSimulation)     — post-match Elo calculation (runs only in Results phase)

PresentationSystemGroup (Client|Local):
  PvPUIBridgeSystem                       — scoreboard, timer, kill feed, ranking display
```

### PvPBootstrapSystem

**File:** `Assets/Scripts/PvP/Systems/PvPBootstrapSystem.cs`

- Managed SystemBase, InitializationSystemGroup, runs once
- `[WorldSystemFilter(ServerSimulation | ClientSimulation | LocalSimulation)]`
- Loads `PvPArenaConfigSO`, `PvPRankingConfigSO`, `PvPMapDefinitionSO[]` from `Resources/`
- Builds `BlobAssetReference<PvPConfigBlob>` via `BlobBuilder`
- Creates `PvPConfigSingleton` entity
- `Enabled = false` (self-disables after first run)
- Follows `SurfaceGameplayConfigSystem` bootstrap pattern

### PvPMatchSystem

**File:** `Assets/Scripts/PvP/Systems/PvPMatchSystem.cs`

- Managed SystemBase, SimulationSystemGroup, OrderFirst
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- State machine driving `PvPMatchState.Phase`:

```
WaitingForPlayers:
  - Waits for PvPMatchRequest entity
  - On request: creates PvPMatchState singleton, assigns GameMode/MapId
  - Assigns PvPTeam to all players (round-robin for TDM, sequential for FFA)
  - Writes TeamId component (existing) to match PvPTeam.TeamId
  - Transitions to Warmup when MinPlayers reached

Warmup (30s):
  - Timer counts down from WarmupDuration
  - All players have PvPSpawnProtection enabled
  - At Timer == 0: transition to Active

Active:
  - Timer counts down from MatchDuration
  - PvPScoringSystem processes kills
  - Win condition: TeamScore >= MaxScore OR Timer <= 0
  - If MaxScore reached: transition to Results
  - If Timer <= 0 and scores tied and OvertimeEnabled: transition to Overtime
  - If Timer <= 0 and not tied: transition to Results

Overtime:
  - Extended timer or sudden death
  - First kill wins (sudden death) or OvertimeDuration countdown
  - Transition to Results on resolution

Results (15s):
  - PvPRankingSystem computes Elo changes
  - Timer counts down from ResultsDuration
  - Creates PvPMatchResult transient entity
  - At Timer == 0: transition to Ended

Ended:
  - Resets all PvPPlayerStats to zero
  - Disables PvPSpawnProtection on all players
  - Restores normalized stats via PvPNormalizationSystem
  - Destroys PvPMatchState singleton
  - Restores CollisionGameSettings defaults
```

### PvPScoringSystem

**File:** `Assets/Scripts/PvP/Systems/PvPScoringSystem.cs`

- Managed SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(DeathTransitionSystem))]`
- Only active when `PvPMatchState.Phase == Active` or `Overtime`
- Uses manual EntityQuery (NOT SystemAPI.Query -- follows CombatResolutionSystem pattern for transient entities)

```
foreach KillCredited on entities with PlayerTag:
  1. Check: Victim also has PlayerTag (PvP kill, not PvE)
  2. Killer.PvPPlayerStats.Kills++
  3. Killer.PvPPlayerStats.DamageDealt += killDamage
  4. Victim.PvPPlayerStats.Deaths++
  5. Update TeamScores based on GameMode
  6. Check win condition (MaxScore)

foreach AssistCredited on entities with PlayerTag:
  1. Check: Victim has PlayerTag (PvP assist)
  2. Assister.PvPPlayerStats.Assists++

  7. Enable PvPRespawnTimer on dead player (RespawnAtTick = currentTick + delay)
  8. Enqueue kill event to PvPKillFeedQueue (static NativeQueue, same pattern as DamageVisualQueue)
```

### PvPSpawnSystem

**File:** `Assets/Scripts/PvP/Systems/PvPSpawnSystem.cs`

- Managed SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(PvPScoringSystem))]`
- Handles respawn after PvP death:

```
foreach entity with PvPRespawnTimer (enabled) + PvPTeam:
  if currentTick >= RespawnAtTick:
    1. Select spawn point: query PvPSpawnPoint where TeamId matches
       - Prefer points not recently used (LastUsedTick oldest)
       - Random selection among top 3 least-recent (anti-camping)
    2. Teleport: write LocalTransform.Position = spawnPoint.Position
    3. Reset Health to Max
    4. Enable PvPSpawnProtection (ExpirationTick = currentTick + protectionDuration)
    5. Disable PvPRespawnTimer
    6. Clear DamageEvent buffer
    7. Update spawnPoint.LastUsedTick
```

### PvPCollisionSystem

**File:** `Assets/Scripts/PvP/Systems/PvPCollisionSystem.cs`

- SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(PvPMatchSystem))]`
- Toggles `CollisionGameSettings` singleton for PvP mode:

```
On PvPMatchState.Phase == Warmup (entry):
  - CollisionGameSettings.FriendlyFireEnabled = true (enable PvP damage)
  - CollisionGameSettings.TeamCollisionEnabled = (GameMode == FreeForAll)
  - Set IsPvPMode = true on local IndicatorThemeContext

On PvPMatchState.Phase == Ended:
  - Restore CollisionGameSettings to defaults
  - Set IsPvPMode = false
```

- Also handles spawn protection filtering: entities with `PvPSpawnProtection` (enabled) skip damage in `DamageApplySystem` via a `PvPSpawnProtectionCheckSystem` that zeroes out `DamageEvent` buffers on protected entities before `DamageApplySystem` runs

### PvPSpawnProtectionCheckSystem

**File:** `Assets/Scripts/PvP/Systems/PvPSpawnProtectionCheckSystem.cs`

- ISystem (Burst), SimulationSystemGroup
- `[UpdateBefore(typeof(DamageApplySystem))]`
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- Queries entities with `PvPSpawnProtection` (enabled) + `DamageEvent` buffer:
  - If `currentTick >= ExpirationTick`: disable PvPSpawnProtection
  - If still protected: clear DamageEvent buffer (prevents damage while invulnerable)
- Also disables protection if player fires a weapon (detected by checking if entity has KillCredited or CombatResultEvent as source -- simplified: if DamageEvent buffer has outgoing entries this frame)

### PvPNormalizationSystem

**File:** `Assets/Scripts/PvP/Systems/PvPNormalizationSystem.cs`

- SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(PvPCollisionSystem))]`
- Only activates when PvPConfigBlob.NormalizationEnabled == true:

```
On match start (Warmup entry):
  foreach player with AttackStats + DefenseStats + Health + PvPStatOverride:
    1. Save originals: PvPStatOverride = { Health.Max, AttackStats.Power, ... }
    2. Enable PvPStatOverride (IEnableableComponent)
    3. Override stats: Health.Max = NormalizedMaxHealth, etc.
    4. Set Health.Current = Health.Max (full health)

On match end (Ended entry):
  foreach player with PvPStatOverride (enabled):
    1. Restore: Health.Max = PvPStatOverride.OriginalMaxHealth, etc.
    2. Disable PvPStatOverride
```

### PvPObjectiveSystem

**File:** `Assets/Scripts/PvP/Systems/PvPObjectiveSystem.cs`

- Managed SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(PvPSpawnSystem))]`
- Only active when `PvPMatchState.GameMode == CapturePoint`
- Queries `PvPCaptureZone` entities + nearby players via physics overlap:

```
foreach PvPCaptureZone:
  1. Count players per team within zone radius (OverlapSphere)
  2. If single team present: increment CaptureProgress toward that team
  3. If multiple teams: contested, progress paused
  4. If CaptureProgress >= 1.0: ControllingTeam = capturing team
  5. While controlled: TeamScore[ControllingTeam] += PointsPerSecond * dt
```

### PvPAntiGriefSystem

**File:** `Assets/Scripts/PvP/Systems/PvPAntiGriefSystem.cs`

- SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(PvPObjectiveSystem))]`

```
AFK Detection:
  foreach player with PvPAntiGriefState + PvPPlayerStats:
    1. If player has no movement/input delta this frame:
       PvPAntiGriefState.TimeSinceLastInput += dt
    2. If TimeSinceLastInput > AFKTimeoutSeconds:
       - IsAFK = 1, AFKWarningCount++
       - If AFKWarningCount >= AFKWarningsBeforeKick: queue disconnect

Leaver Detection:
  - On player disconnect during Active/Overtime phase:
    - LeaverPenaltyCount++ (persisted via save)
    - Apply queue cooldown: LeaverPenaltyCooldown * LeaverPenaltyCount

Spawn Camping Prevention:
  - Track kills near enemy spawn points (within SpawnCampingRadius)
  - If killer gets 3+ kills within SpawnCampingWindow near same spawn:
    - Rotate spawn points (disable camped spawn, enable alternates)
```

### PvPRankingSystem

**File:** `Assets/Scripts/PvP/Systems/PvPRankingSystem.cs`

- Managed SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation)]` (server-only, authoritative ranking)
- `[UpdateAfter(typeof(PvPAntiGriefSystem))]`
- Only runs when `PvPMatchState.Phase == Results` (exactly once per match transition)

```
Elo Calculation (per player):
  1. Determine match result: Win(1.0), Loss(0.0), Draw(0.5)
  2. For team modes: compare player's team score vs opponent team
  3. For FFA: rank players by score, top half = win, bottom = loss

  expectedWin = 1.0 / (1.0 + pow(10, (opponentElo - playerElo) / 400.0))

  K = EloKFactor
  if playerElo > EloHighRatingThreshold: K = EloKFactorHighRating
  if player.PlacementMatchesPlayed < PlacementMatchCount: K *= PlacementKMultiplier

  eloDelta = K * (actualResult - expectedWin)
  if WinStreak >= 3: eloDelta += min(WinStreakBonus * (WinStreak - 2), MaxWinStreakBonus)

  PvPRanking.Elo += (int)eloDelta
  PvPRanking.Elo = max(0, PvPRanking.Elo)  // Floor at 0
  if Elo > PvPRanking.HighestElo: PvPRanking.HighestElo = Elo

  4. Update PvPTier based on TierThresholds[]
  5. Update Wins/Losses/WinStreak
  6. Mark save dirty for PvPRankingSaveModule

XP Awards:
  7. Winning team: XPGrantAPI.GrantXP(player, PvPWinBonusXP, XPSourceType.Bonus)
  8. Losing team: XPGrantAPI.GrantXP(player, PvPLossBonusXP, XPSourceType.Bonus)
  9. PvP kills: XPAwardSystem already handles (but PvPKillXPMultiplier applied)
```

### PvP XP Integration

**File:** `Assets/Scripts/PvP/Systems/PvPXPModifierSystem.cs`

- SystemBase, SimulationSystemGroup
- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateBefore(typeof(XPAwardSystem))]`
- When PvPMatchState exists and Phase == Active:
  - Tags KillCredited entities that killed a PlayerTag victim with `PvPKillMarker` (IEnableableComponent)
  - XPAwardSystem checks: if `PvPKillMarker` enabled, multiply XP by `PvPKillXPMultiplier`

---

## UI Bridge

**File:** `Assets/Scripts/PvP/Bridges/PvPUIBridgeSystem.cs`

- Managed SystemBase, PresentationSystemGroup, Client|Local
- Reads `PvPMatchState` singleton (Ghost:All -- available on all clients)
- Reads local player `PvPPlayerStats`, `PvPRanking` via `PvPRankingLink`
- Dequeues `PvPKillFeedQueue` (static NativeQueue, same pattern as `DamageVisualQueue`)
- Dispatches to `PvPUIRegistry` -> `IPvPUIProvider`

**File:** `Assets/Scripts/PvP/Bridges/PvPUIRegistry.cs`

Static registry (same pattern as `CombatUIRegistry`):

```csharp
public static class PvPUIRegistry
{
    private static IPvPUIProvider _provider;
    public static void Register(IPvPUIProvider provider) { _provider = provider; }
    public static void Unregister(IPvPUIProvider provider) { if (_provider == provider) _provider = null; }
    public static bool HasProvider => _provider != null;
    public static IPvPUIProvider Provider => _provider;
}
```

**File:** `Assets/Scripts/PvP/Bridges/IPvPUIProvider.cs`

```csharp
public interface IPvPUIProvider
{
    void UpdateMatchState(PvPMatchUIState state);
    void UpdateScoreboard(PvPScoreboardEntry[] entries);
    void OnKillFeedEvent(PvPKillFeedEntry entry);
    void OnMatchPhaseChange(PvPMatchPhase oldPhase, PvPMatchPhase newPhase);
    void OnMatchResult(PvPMatchResultUI result);
    void UpdateRanking(PvPRankingUI ranking);
}
```

**File:** `Assets/Scripts/PvP/Bridges/PvPUIState.cs`

```csharp
public struct PvPMatchUIState
{
    public PvPMatchPhase Phase;
    public PvPGameMode GameMode;
    public float TimeRemaining;
    public int[] TeamScores;      // Length 4
    public int MaxScore;
    public int LocalPlayerKills;
    public int LocalPlayerDeaths;
    public int LocalPlayerAssists;
}

public struct PvPScoreboardEntry
{
    public string PlayerName;
    public byte TeamId;
    public short Kills;
    public short Deaths;
    public short Assists;
    public float DamageDealt;
    public float HealingDone;
    public int MatchScore;
    public bool IsLocalPlayer;
}

public struct PvPKillFeedEntry
{
    public string KillerName;
    public string VictimName;
    public byte KillerTeam;
    public byte VictimTeam;
    public bool IsLocalPlayerKiller;
    public bool IsLocalPlayerVictim;
}

public struct PvPMatchResultUI
{
    public PvPGameMode GameMode;
    public byte WinningTeam;
    public bool LocalPlayerWon;
    public PvPScoreboardEntry[] FinalScoreboard;
    public int EloDelta;
    public PvPTier NewTier;
    public PvPTier OldTier;
    public bool TierChanged;
    public float BonusXP;
}

public struct PvPRankingUI
{
    public int Elo;
    public PvPTier Tier;
    public int Wins;
    public int Losses;
    public int WinStreak;
    public int HighestElo;
    public float WinRate;
}
```

**File:** `Assets/Scripts/PvP/Bridges/PvPKillFeedQueue.cs`

- Static `NativeQueue<PvPKillFeedEntry>` bridge (same pattern as `DamageVisualQueue`)
- `PvPScoringSystem` enqueues kill events
- `PvPUIBridgeSystem` dequeues for display

**MonoBehaviour Views:**

| File | Purpose |
|------|---------|
| `Assets/Scripts/PvP/UI/ScoreboardView.cs` | Tab-toggled scoreboard with K/D/A columns |
| `Assets/Scripts/PvP/UI/MatchTimerView.cs` | Countdown timer + phase indicator |
| `Assets/Scripts/PvP/UI/KillFeedView.cs` | Scrolling kill notifications (top right) |
| `Assets/Scripts/PvP/UI/RankingView.cs` | Elo/Tier display with progress bar |
| `Assets/Scripts/PvP/UI/MatchResultsView.cs` | Post-match results overlay |
| `Assets/Scripts/PvP/UI/PvPQueueView.cs` | Queue/lobby UI for joining matches |

---

## Save Integration

**File:** `Assets/Scripts/Persistence/Modules/PvPRankingSaveModule.cs`

```
ISaveModule implementation:
  TypeId = 15
  DisplayName = "PvP Ranking"
  ModuleVersion = 1
```

Serializes PvPRanking from child entity via PvPRankingLink:

| Order | Field | Type | Bytes |
|-------|-------|------|-------|
| 1 | Elo | int32 | 4 |
| 2 | Tier | byte | 1 |
| 3 | Wins | int32 | 4 |
| 4 | Losses | int32 | 4 |
| 5 | WinStreak | int32 | 4 |
| 6 | HighestElo | int32 | 4 |
| 7 | LeaverPenaltyCount | byte | 1 |
| 8 | PlacementMatchesPlayed | byte | 1 |
| **Total** | | | **23 bytes** |

On deserialize: locate child entity via `PvPRankingLink`, `SetComponentData<PvPRanking>`. If component absent (old save without PvP data), skip gracefully.

**Match stats (PvPPlayerStats) are NOT persisted** -- they reset each match. Only ranking (Elo/Tier/W/L) survives across sessions.

---

## Authoring

**File:** `Assets/Scripts/PvP/Authoring/PvPPlayerAuthoring.cs`

```
[AddComponentMenu("DIG/PvP/PvP Player")]
```

- Place on Player prefab (Warrok_Server) alongside existing authoring components
- Baker creates child entity for ranking (same pattern as `SaveStateAuthoring` / `TalentAuthoring`):
  - Player entity: `PvPRankingLink` (8 bytes), `PvPPlayerStats` (24 bytes), `PvPTeam` (4 bytes), `PvPSpawnProtection` (disabled, 4 bytes), `PvPRespawnTimer` (disabled, 4 bytes), `PvPStatOverride` (disabled, 20 bytes), `PvPAntiGriefState` (8 bytes)
  - Child entity: `PvPRankingTag`, `PvPRankingOwner`, `PvPRanking` (initialized: Elo=1200, Tier=Bronze)

**File:** `Assets/Scripts/PvP/Authoring/PvPSpawnPointAuthoring.cs`

```
[AddComponentMenu("DIG/PvP/Spawn Point")]
```

- Place on empty GameObjects in arena subscene
- Fields: TeamId (byte), SpawnIndex (byte)
- Baker adds: `PvPSpawnPoint` + `LocalTransform` (position from transform)

**File:** `Assets/Scripts/PvP/Authoring/PvPCaptureZoneAuthoring.cs`

```
[AddComponentMenu("DIG/PvP/Capture Zone")]
```

- Place on trigger collider GameObjects in arena subscene
- Fields: ZoneId (byte), PointsPerSecond (float), Radius (float)
- Baker adds: `PvPCaptureZone` + `PhysicsShapeAuthoring` (sphere trigger)

**File:** `Assets/Scripts/PvP/Authoring/PvPMatchStateAuthoring.cs`

```
[AddComponentMenu("DIG/PvP/Match State")]
```

- Place on a singleton entity in arena subscene
- Baker adds: `PvPMatchState` (Ghost:All) with Phase = WaitingForPlayers

---

## Editor Tooling

### PvPWorkstationWindow

**File:** `Assets/Editor/PvPWorkstation/PvPWorkstationWindow.cs`
- Menu: `DIG/PvP Workstation`
- Sidebar + `IPvPWorkstationModule` pattern (matches other workstations)

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Match Inspector | `Modules/MatchInspectorModule.cs` | Play-mode: live match state (phase, timer, scores, player count), phase transition buttons for testing |
| Player Inspector | `Modules/PlayerInspectorModule.cs` | Live K/D/A, team assignment, spawn protection status, Elo/Tier, stat override state |
| Map Editor | `Modules/MapEditorModule.cs` | Visual spawn point placement, capture zone preview, team color coding, map validation |
| Ranking Simulator | `Modules/RankingSimulatorModule.cs` | "Simulate N matches" button, Elo distribution graph, tier progression chart, K-factor analysis |
| Balance Analyzer | `Modules/BalanceAnalyzerModule.cs` | Win rate by Elo bracket, normalization impact analysis, spawn point fairness heatmap |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `PvPPlayerStats` | 24 bytes | Player entity |
| `PvPTeam` | 4 bytes | Player entity |
| `PvPSpawnProtection` | 4 bytes | Player entity (IEnableableComponent, baked disabled) |
| `PvPRespawnTimer` | 4 bytes | Player entity (IEnableableComponent, baked disabled) |
| `PvPStatOverride` | 20 bytes | Player entity (IEnableableComponent, baked disabled) |
| `PvPAntiGriefState` | 8 bytes | Player entity |
| `PvPRankingLink` | 8 bytes | Player entity (Entity ref to child) |
| `PvPKillMarker` | 0 bytes | Player entity (IEnableableComponent, tag only) |
| **Total on player** | **~72 bytes** | |
| `PvPRankingTag` | 0 bytes | Child entity |
| `PvPRankingOwner` | 8 bytes | Child entity |
| `PvPRanking` | 24 bytes | Child entity |

72 bytes on the player entity. For reference, `ResourcePool` (EPIC 16.8) added 80 bytes. The player archetype has headroom from buffer capacity reductions (CollisionEvent(2), StatusEffectRequest(4), ReviveRequest(2)). 72 bytes is within budget but should be validated during bake. If tight, `PvPStatOverride` (20 bytes) can be moved to the ranking child entity since it is only needed during active matches.

**No new IBufferElementData on ghost-replicated entities.** PvPSpawnProtection and PvPRespawnTimer are IEnableableComponent (zero structural change to toggle). PvPRanking lives on a non-ghost child entity.

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `PvPBootstrapSystem` | N/A | No | Runs once at startup |
| `PvPMatchSystem` | < 0.01ms | No | State machine, rare transitions |
| `PvPCollisionSystem` | < 0.01ms | No | Singleton write, rare transitions |
| `PvPNormalizationSystem` | < 0.01ms | No | Only on match start/end |
| `PvPSpawnProtectionCheckSystem` | < 0.02ms | Yes | Iterates protected players (0-16) |
| `PvPScoringSystem` | < 0.02ms | No | Processes kill events (rare, ~0-3 per second) |
| `PvPSpawnSystem` | < 0.02ms | No | Respawn events (rare) |
| `PvPObjectiveSystem` | < 0.05ms | No | Physics overlap per capture zone (2-5 zones) |
| `PvPAntiGriefSystem` | < 0.01ms | No | Per-player timer check |
| `PvPRankingSystem` | < 0.10ms | No | Elo calculation, runs once per match (Results phase) |
| `PvPXPModifierSystem` | < 0.01ms | No | Tag kill events |
| `PvPUIBridgeSystem` | < 0.03ms | No | Managed, queue drain |
| **Total (steady state)** | **< 0.15ms** | | All systems combined during active match |

---

## File Summary

### New Files (42)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/PvP/Components/PvPMatchState.cs` | IComponentData (singleton, Ghost:All) |
| 2 | `Assets/Scripts/PvP/Components/PvPMatchPhase.cs` | Enums (PvPMatchPhase, PvPGameMode, PvPTier) |
| 3 | `Assets/Scripts/PvP/Components/PvPPlayerStats.cs` | IComponentData (Ghost:AllPredicted) |
| 4 | `Assets/Scripts/PvP/Components/PvPTeam.cs` | IComponentData (Ghost:AllPredicted) |
| 5 | `Assets/Scripts/PvP/Components/PvPSpawnPoint.cs` | IComponentData (arena entities) |
| 6 | `Assets/Scripts/PvP/Components/PvPSpawnProtection.cs` | IEnableableComponent |
| 7 | `Assets/Scripts/PvP/Components/PvPRespawnTimer.cs` | IEnableableComponent |
| 8 | `Assets/Scripts/PvP/Components/PvPStatOverride.cs` | IEnableableComponent |
| 9 | `Assets/Scripts/PvP/Components/PvPAntiGriefState.cs` | IComponentData |
| 10 | `Assets/Scripts/PvP/Components/PvPCaptureZone.cs` | IComponentData (Ghost:All) |
| 11 | `Assets/Scripts/PvP/Components/PvPRankingComponents.cs` | Link + child components |
| 12 | `Assets/Scripts/PvP/Components/PvPRequestComponents.cs` | Transient requests + RPCs |
| 13 | `Assets/Scripts/PvP/Components/PvPKillMarker.cs` | IEnableableComponent (tag) |
| 14 | `Assets/Scripts/PvP/Config/PvPConfigBlob.cs` | BlobAsset structs + Singleton |
| 15 | `Assets/Scripts/PvP/Definitions/PvPArenaConfigSO.cs` | ScriptableObject |
| 16 | `Assets/Scripts/PvP/Definitions/PvPMapDefinitionSO.cs` | ScriptableObject |
| 17 | `Assets/Scripts/PvP/Definitions/PvPRankingConfigSO.cs` | ScriptableObject |
| 18 | `Assets/Scripts/PvP/Systems/PvPBootstrapSystem.cs` | SystemBase (bootstrap) |
| 19 | `Assets/Scripts/PvP/Systems/PvPMatchSystem.cs` | SystemBase (lifecycle) |
| 20 | `Assets/Scripts/PvP/Systems/PvPCollisionSystem.cs` | SystemBase |
| 21 | `Assets/Scripts/PvP/Systems/PvPNormalizationSystem.cs` | SystemBase |
| 22 | `Assets/Scripts/PvP/Systems/PvPSpawnProtectionCheckSystem.cs` | ISystem (Burst) |
| 23 | `Assets/Scripts/PvP/Systems/PvPScoringSystem.cs` | SystemBase |
| 24 | `Assets/Scripts/PvP/Systems/PvPSpawnSystem.cs` | SystemBase |
| 25 | `Assets/Scripts/PvP/Systems/PvPObjectiveSystem.cs` | SystemBase |
| 26 | `Assets/Scripts/PvP/Systems/PvPAntiGriefSystem.cs` | SystemBase |
| 27 | `Assets/Scripts/PvP/Systems/PvPRankingSystem.cs` | SystemBase (Server) |
| 28 | `Assets/Scripts/PvP/Systems/PvPXPModifierSystem.cs` | SystemBase |
| 29 | `Assets/Scripts/PvP/Authoring/PvPPlayerAuthoring.cs` | Baker (child entity) |
| 30 | `Assets/Scripts/PvP/Authoring/PvPSpawnPointAuthoring.cs` | Baker (arena) |
| 31 | `Assets/Scripts/PvP/Authoring/PvPCaptureZoneAuthoring.cs` | Baker (arena) |
| 32 | `Assets/Scripts/PvP/Authoring/PvPMatchStateAuthoring.cs` | Baker (singleton) |
| 33 | `Assets/Scripts/PvP/Bridges/PvPUIBridgeSystem.cs` | SystemBase (managed) |
| 34 | `Assets/Scripts/PvP/Bridges/PvPUIRegistry.cs` | Static class |
| 35 | `Assets/Scripts/PvP/Bridges/IPvPUIProvider.cs` | Interface |
| 36 | `Assets/Scripts/PvP/Bridges/PvPUIState.cs` | UI state structs |
| 37 | `Assets/Scripts/PvP/Bridges/PvPKillFeedQueue.cs` | Static NativeQueue |
| 38 | `Assets/Scripts/PvP/UI/ScoreboardView.cs` | MonoBehaviour |
| 39 | `Assets/Scripts/PvP/UI/MatchTimerView.cs` | MonoBehaviour |
| 40 | `Assets/Scripts/PvP/UI/KillFeedView.cs` | MonoBehaviour |
| 41 | `Assets/Scripts/PvP/UI/RankingView.cs` | MonoBehaviour |
| 42 | `Assets/Scripts/PvP/UI/MatchResultsView.cs` | MonoBehaviour |

| # | Path | Type |
|---|------|------|
| 43 | `Assets/Scripts/PvP/UI/PvPQueueView.cs` | MonoBehaviour |
| 44 | `Assets/Scripts/PvP/DIG.PvP.asmdef` | Assembly def |
| 45 | `Assets/Scripts/Persistence/Modules/PvPRankingSaveModule.cs` | ISaveModule (TypeId=15) |
| 46 | `Assets/Editor/PvPWorkstation/PvPWorkstationWindow.cs` | EditorWindow |
| 47 | `Assets/Editor/PvPWorkstation/IPvPWorkstationModule.cs` | Interface |
| 48 | `Assets/Editor/PvPWorkstation/Modules/MatchInspectorModule.cs` | Module |
| 49 | `Assets/Editor/PvPWorkstation/Modules/PlayerInspectorModule.cs` | Module |
| 50 | `Assets/Editor/PvPWorkstation/Modules/MapEditorModule.cs` | Module |
| 51 | `Assets/Editor/PvPWorkstation/Modules/RankingSimulatorModule.cs` | Module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | Player prefab (Warrok_Server) | Add `PvPPlayerAuthoring` |
| 2 | `Assets/Scripts/Progression/Systems/XPAwardSystem.cs` | Check `PvPKillMarker` to apply `PvPKillXPMultiplier` (~5 lines) |
| 3 | `Assets/Scripts/Targeting/Theming/IndicatorThemeContext.cs` | No code change -- `IsPvPMode` and `FriendlyFireEnabled` fields already exist |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/PvPArenaConfig.asset` |
| 2 | `Resources/PvPRankingConfig.asset` |
| 3 | `Resources/PvPMaps/Arena_01.asset` |
| 4 | `Resources/PvPMaps/Arena_02.asset` |

---

## Multiplayer

### Server-Authoritative Model

- **ALL match logic runs on server** (or host in listen-server). Clients NEVER modify PvPMatchState, scores, or rankings directly.
- `PvPMatchState` is `Ghost:All` -- all clients see phase, timer, scores.
- `PvPPlayerStats` is `Ghost:AllPredicted` -- owning client sees own K/D/A without round-trip latency.
- `PvPRanking` lives on a **non-ghost child entity** -- ranking changes only visible to owning client via save/load or explicit UI push.
- Team assignment via `PvPTeam` (Ghost:AllPredicted) and existing `TeamId` component (not ghost, collision only).
- Stat normalization applied server-side, replicated via existing ghost-replicated `AttackStats`/`DefenseStats`/`Health` components.
- RPCs: `PvPJoinQueueRpc` and `PvPLeaveRpc` for client requests, server validates before processing.
- **No new IBufferElementData on ghost-replicated entities** (host-time safety).

### PvP Damage Path

No modifications to `DamageApplySystem` (Burst-compiled, DO NOT MODIFY). The existing damage pipeline already handles player-vs-player damage when `CollisionGameSettings.FriendlyFireEnabled` is true. `PvPCollisionSystem` toggles this flag. Spawn protection is enforced by `PvPSpawnProtectionCheckSystem` clearing `DamageEvent` buffers BEFORE `DamageApplySystem` runs.

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `CollisionGameSettings` | 7.6 | PvPCollisionSystem toggles FriendlyFireEnabled for PvP mode |
| `TeamId` | 7.6 | PvPMatchSystem writes TeamId to match PvPTeam assignment |
| `PlayerProximityCollisionSystem` | 7.6 | Reads TeamId (unchanged, existing team collision filtering) |
| `KillCredited` / `AssistCredited` | 13.16 | PvPScoringSystem reads these events to update PvP K/D/A |
| `DeathTransitionSystem` | 13.16 | Creates kill/assist events (unchanged) |
| `DamageApplySystem` | Core | Player damage pipeline (unchanged, PvP damage via FF toggle) |
| `XPAwardSystem` | 16.14 | Modified to apply PvPKillXPMultiplier on PvP kills |
| `XPGrantAPI` | 16.14 | PvPRankingSystem calls for win/loss bonus XP |
| `IndicatorThemeContext` | 15.22 | PvPCollisionSystem sets IsPvPMode for combat indicator theming |
| `TargetFaction.Hostile` | 15.22 | PvP opponents assigned Hostile faction for indicator styling |
| `PvPRankingSaveModule` | 16.15 | ISaveModule (TypeId=15) persists Elo/Tier/W/L record |
| `CharacterAttributes.Level` | 16.14 | Level-based matchmaking bracket filter (future) |
| `EquippedStatsSystem` | 16.6 | PvPNormalizationSystem overrides stats AFTER equipment calc |
| `AttackStats` / `DefenseStats` | Core | Ghost:All -- normalization changes replicate to all clients |

---

## Verification Checklist

### Match Lifecycle
- [ ] PvPMatchRequest entity creates match with correct GameMode and MapId
- [ ] WaitingForPlayers phase: players join, PvPTeam assigned
- [ ] Warmup phase: 30s countdown, all players invulnerable
- [ ] Warmup -> Active transition: spawn protection cleared, damage enabled
- [ ] Active phase: timer counts down, kills scored
- [ ] MaxScore reached: immediate transition to Results
- [ ] Timer expires with tied scores: transition to Overtime (if enabled)
- [ ] Timer expires with clear winner: transition to Results
- [ ] Results phase: 15s display, Elo calculated
- [ ] Ended phase: all PvP state reset, CollisionGameSettings restored

### Scoring
- [ ] Player kills another player: Kills++ on killer, Deaths++ on victim
- [ ] Assist credited: Assists++ on assisting player
- [ ] PvE kill during PvP match: NOT counted in PvPPlayerStats
- [ ] Team scores update correctly for TeamDeathmatch
- [ ] FFA scores update per-player
- [ ] Kill feed events dispatched to UI

### Spawn System
- [ ] Dead player respawns after RespawnDelay (5s)
- [ ] Respawn at team-appropriate spawn point
- [ ] Spawn point rotation: least-recently-used selection with randomization
- [ ] PvPSpawnProtection enabled for 3s after spawn
- [ ] Spawn protection prevents all damage (DamageEvent cleared)
- [ ] Spawn protection removed early if player attacks
- [ ] Spawn protection removed at ExpirationTick

### Collision & Damage
- [ ] CollisionGameSettings.FriendlyFireEnabled = true during PvP match
- [ ] Player-vs-player damage works via existing DamageApplySystem
- [ ] TeamCollisionEnabled respects game mode (FFA = all collide, TDM = enemies only)
- [ ] DamageApplySystem UNCHANGED (no modifications to Burst system)
- [ ] Post-match: CollisionGameSettings restored to defaults

### Equipment Normalization
- [ ] NormalizationEnabled: all players get identical stats during match
- [ ] Original stats saved in PvPStatOverride
- [ ] Stats restored on match end
- [ ] Health set to max on normalization apply
- [ ] Normalization disabled: players keep their gear stats

### Capture Point (CapturePoint mode)
- [ ] Single team in zone: CaptureProgress increases
- [ ] Multiple teams in zone: contested, progress paused
- [ ] CaptureProgress reaches 1.0: ControllingTeam set
- [ ] Controlled zone: TeamScore accumulates at PointsPerSecond
- [ ] Zone control changes hands correctly

### Ranking & Elo
- [ ] Starting Elo: 1200 for new players
- [ ] Win: Elo increases (K * (1 - expectedWin))
- [ ] Loss: Elo decreases
- [ ] Draw: minimal Elo change
- [ ] Placement matches: higher K factor (2x)
- [ ] High-rating players: reduced K factor
- [ ] Win streak bonus: +5 Elo per win after 3-streak
- [ ] Tier thresholds: correct tier assigned (Bronze->Silver at 1000, etc.)
- [ ] HighestElo tracks peak
- [ ] Elo floor at 0 (never negative)

### Anti-Grief
- [ ] AFK detection: 60s no input -> warning
- [ ] AFK kick: 3 warnings -> disconnect
- [ ] Leaver penalty: queue cooldown applied
- [ ] Spawn camping: 3+ kills near spawn -> spawn rotation triggered

### XP Integration
- [ ] PvP kills award XP at PvPKillXPMultiplier (50% of PvE rate)
- [ ] Match winner receives PvPWinBonusXP
- [ ] Match loser receives PvPLossBonusXP
- [ ] XP award server-authoritative

### Multiplayer
- [ ] PvPMatchState replicates to all clients (Ghost:All)
- [ ] PvPPlayerStats replicates to owning client (Ghost:AllPredicted)
- [ ] Stat normalization replicates via existing AttackStats/DefenseStats (Ghost:All)
- [ ] Remote clients see match timer and scores
- [ ] Server-authoritative scoring: no client-side score manipulation
- [ ] PvPJoinQueueRpc validated server-side

### Save Integration
- [ ] PvPRankingSaveModule (TypeId=15) serializes Elo/Tier/Wins/Losses/WinStreak
- [ ] Load restores ranking correctly
- [ ] Old save without PvP data: module skipped gracefully
- [ ] Match stats (K/D/A) NOT persisted (reset per match)

### UI
- [ ] Scoreboard shows all players with K/D/A sorted by score
- [ ] Match timer displays countdown
- [ ] Kill feed scrolls recent kills
- [ ] Ranking view shows Elo, Tier, win rate
- [ ] Match results screen shows final scores + Elo delta
- [ ] Phase change triggers UI transitions (warmup countdown, match start, results overlay)

### Archetype & Performance
- [ ] Only ~72 bytes added to player entity archetype
- [ ] No ghost bake errors after adding PvPPlayerAuthoring
- [ ] No new IBufferElementData on ghost-replicated entities
- [ ] DamageApplySystem performance unchanged (no modifications)
- [ ] Total PvP system budget < 0.15ms during active match
- [ ] Entity without PvPPlayerStats: zero cost (systems skip)
- [ ] Subscene reimport succeeds after adding PvP authoring components
