# EPIC 17.10: PvP Arena & Ranking System — Setup Guide

## Overview

The PvP Arena & Ranking System provides server-authoritative competitive PvP with multiple game modes (FreeForAll, TeamDeathmatch, CapturePoint, Duel), match lifecycle management, Elo-based ranking, spawn protection, optional equipment normalization, and anti-grief measures. It integrates with the existing collision, damage, and kill attribution pipelines.

**Key design**: All match logic runs on the server. Clients see replicated match state (Ghost:All) and own stats (Ghost:AllPredicted). No modifications to `DamageApplySystem` — PvP damage flows through the existing pipeline via `CollisionGameSettings.FriendlyFireEnabled`. Spawn protection works by clearing `DamageEvent` buffers before `DamageApplySystem` runs.

**Player archetype impact**: ~72 bytes on the player entity. Ranking data lives on a non-ghost child entity (24 bytes), same pattern as `SaveStateLink`.

---

## Quick Start

### 1. Create the Arena Config

1. **Right-click** in the Project window: **Create > DIG > PvP > Arena Config**
2. Name it `PvPArenaConfig` and place it in `Assets/Resources/`
3. Configure match parameters:

| Field | Default | Description |
|-------|---------|-------------|
| WarmupDuration | 30 | Warmup countdown in seconds |
| ResultsDuration | 15 | Post-match results display in seconds |
| OvertimeDuration | 60 | Overtime duration (0 = sudden death) |
| RespawnDelay | 5 | Seconds before respawn after death |
| SpawnProtectionDuration | 3 | Invulnerability after spawn in seconds |
| FreeForAllKillLimit | 20 | Kill limit for FFA mode |
| TeamDeathmatchKillLimit | 50 | Combined kill limit for TDM |
| CapturePointScoreLimit | 1000 | Score limit for CapturePoint mode |
| DuelRounds | 5 | Best of N for Duel mode |
| NormalizationEnabled | false | Enable stat equalization for competitive fairness |
| AFKTimeoutSeconds | 60 | Seconds without input before AFK warning |
| AFKWarningsBeforeKick | 3 | Warnings before auto-kick |
| LeaverPenaltyCooldown | 300 | Queue ban for leavers in seconds |
| PvPKillXPMultiplier | 0.5 | PvP kill XP relative to PvE kills |
| PvPWinBonusXP | 500 | Bonus XP for match winner |
| PvPLossBonusXP | 100 | Consolation XP for losers |

> **Important:** The asset must be named exactly `PvPArenaConfig` and placed in a `Resources/` folder.

### 2. Create the Ranking Config

1. **Right-click** > **Create > DIG > PvP > Ranking Config**
2. Name it `PvPRankingConfig` and place it in `Assets/Resources/`
3. Configure Elo parameters:

| Field | Default | Description |
|-------|---------|-------------|
| StartingElo | 1200 | Initial Elo for new players |
| KFactor | 32 | Standard Elo K-factor |
| KFactorHighRating | 16 | Reduced K above high rating threshold |
| HighRatingThreshold | 2400 | Elo threshold for reduced K |
| PlacementMatchCount | 10 | Matches before stable ranking |
| PlacementKMultiplier | 2.0 | K multiplier during placement matches |
| TierThresholds | [0,1000,1500,2000,2500,3000] | Elo thresholds for Bronze/Silver/Gold/Platinum/Diamond/Master |
| WinStreakBonus | 5 | Extra Elo per win in streak of 3+ |
| MaxWinStreakBonus | 25 | Cap on streak bonus |

> **Important:** The asset must be named exactly `PvPRankingConfig` and placed in a `Resources/` folder.

### 3. Create Map Definitions

For each PvP arena map:

1. **Right-click** > **Create > DIG > PvP > Map Definition**
2. Place in `Assets/Resources/PvPMaps/`
3. Fill in the fields:

| Field | Description |
|-------|-------------|
| **MapId** | Unique byte identifier (1-255) |
| **MapName** | Display name (e.g., "Gladiator Pit") |
| MapDescription | UI description text |
| **MaxPlayers** | Max concurrent players (2/8/12/16) |
| **TeamCount** | 0 = FFA, 2 or 4 = team modes |
| **SupportedModes** | Array of PvPGameMode values this map supports |
| **SpawnPoints** | Array of team spawn positions/rotations |
| CaptureZones | Array of capture point positions (CapturePoint mode only) |
| ArenaSubscenePath | Path to arena subscene asset |

### 4. Add PvP Player Authoring to Player Prefab

1. Select the **player prefab** (Warrok_Server)
2. **Add Component** > **DIG > PvP > PvP Player**
3. Configure starting Elo (default: 1200)

This adds ~72 bytes to the player entity:
- `PvPPlayerStats` (24 bytes) — per-match K/D/A
- `PvPTeam` (4 bytes) — team assignment
- `PvPAntiGriefState` (8 bytes) — AFK/leaver tracking
- `PvPSpawnProtection` (4 bytes, IEnableableComponent)
- `PvPRespawnTimer` (4 bytes, IEnableableComponent)
- `PvPStatOverride` (20 bytes, IEnableableComponent)
- `PvPKillMarker` (0 bytes, tag)
- `PvPRankingLink` (8 bytes) — link to ranking child entity

A child entity is created for ranking data (`PvPRanking`, 24 bytes) to keep it off the player archetype.

> **After adding**: Reimport the subscene containing the player prefab to regenerate ghost serialization.

---

## Arena Setup (Subscene)

### Spawn Points

1. Create empty GameObjects in your arena subscene
2. **Add Component** > **DIG > PvP > Spawn Point**
3. Configure:

| Field | Description |
|-------|-------------|
| TeamId | 0 = any team, 1-4 = specific team |
| SpawnIndex | Index within team's spawn array |

4. Position and rotate each spawn point
5. Scene view gizmos show team-colored spheres with forward arrows

**Recommended**: At least `MaxPlayers` spawn points per map. For team modes, distribute evenly across teams.

### Capture Zones (CapturePoint Mode)

1. Create GameObjects in your arena subscene
2. **Add Component** > **DIG > PvP > Capture Zone**
3. Configure:

| Field | Description |
|-------|-------------|
| ZoneId | Unique zone identifier |
| PointsPerSecond | Score rate when controlled |
| Radius | Capture zone radius |

4. Scene view gizmos show yellow spheres indicating the zone area

**Recommended**: 3-5 capture zones per map for CapturePoint mode.

### Match State (Optional Subscene Singleton)

If you want a pre-configured match in a subscene:

1. Create an empty GameObject
2. **Add Component** > **DIG > PvP > Match State**
3. Configure default match duration and overtime setting

> **Note**: Matches can also be created at runtime via `PvPMatchRequest` entities, without a pre-baked singleton.

---

## Game Modes

| Mode | Players | Scoring | Description |
|------|---------|---------|-------------|
| **FreeForAll** | 2-16 | Kill limit per player | Every player for themselves |
| **TeamDeathmatch** | 4-16 | Combined team kills | 2 teams, first to kill limit wins |
| **CapturePoint** | 4-16 | Point accumulation | Control zones to score points |
| **Duel** | 2 | Best of N rounds | 1v1 competitive |

## Match Lifecycle

```
WaitingForPlayers → Warmup (30s) → Active → [Overtime] → Results (15s) → Ended
                                            ↑
                                    Timer=0 and tied scores
```

- **WaitingForPlayers**: Assigns teams, waits for minimum players
- **Warmup**: Countdown timer, all players invulnerable
- **Active**: Combat enabled, scoring active, timer counting down
- **Overtime**: Triggered when scores tied at timer expiry (sudden death or extended time)
- **Results**: Post-match display, Elo calculated, XP awarded
- **Ended**: All PvP state reset, collision settings restored

---

## Equipment Normalization

When `NormalizationEnabled = true` in Arena Config:

1. On warmup start, each player's original stats are saved in `PvPStatOverride`
2. Stats are overridden with normalized values (Health, Attack, Spell, Defense, Armor)
3. Health is set to max
4. On match end, original stats are restored

This ensures competitive fairness regardless of gear level.

---

## Ranking System

### Elo Formula

```
expectedWin = 1.0 / (1.0 + 10^((opponentElo - playerElo) / 400))
eloDelta = K * (actualResult - expectedWin)
```

- **K-factor**: 32 (standard), 16 (above 2400 Elo), 64 (during placement x2.0)
- **Win streak bonus**: +5 Elo per win after 3-streak (capped at 25)
- **Elo floor**: 0 (never negative)
- **Starting Elo**: 1200

### Tiers

| Tier | Elo Range |
|------|-----------|
| Bronze | 0 - 999 |
| Silver | 1000 - 1499 |
| Gold | 1500 - 1999 |
| Platinum | 2000 - 2499 |
| Diamond | 2500 - 2999 |
| Master | 3000+ |

### Persistence

Ranking data (Elo, Tier, W/L, Streak, Peak) is persisted via `PvPRankingSaveModule` (TypeId=15). Match stats (K/D/A) reset each match and are NOT persisted.

---

## Editor Tooling

### PvP Workstation

Open via **DIG > PvP Workstation** menu.

| Module | Purpose |
|--------|---------|
| **Match Inspector** | Live match state: phase, timer, scores. Test match creation buttons |
| **Player Inspector** | Per-player K/D/A, team, spawn protection, Elo, tier |
| **Map Editor** | Visual spawn point placement, capture zone preview, team colors, map validation |
| **Ranking Simulator** | Simulate N matches with configurable win rates. Elo distribution and K-factor analysis |
| **Balance Analyzer** | Normalization settings overview, anti-grief config, Elo-based expected win rates, map validation |

---

## Architecture

```
              ScriptableObjects (Resources/)
  PvPArenaConfigSO    PvPMapDefinitionSO[]    PvPRankingConfigSO
         |                    |                       |
         └── PvPBootstrapSystem (loads, builds BlobAsset) ──┘
                        |
                 PvPConfigSingleton (BlobRef)
                        |
  ┌─────────────────────┼──────────────────────────┐
  │                     │                          │
PvPMatchState      PvPPlayerStats              PvPRanking
(singleton,        (on player,                 (on CHILD entity,
 Ghost:All)         Ghost:AllPredicted)         via PvPRankingLink)
  │                     │                          │
  └── System Pipeline ──┘──────────────────────────┘

  PvPMatchSystem ──→ PvPCollisionSystem ──→ PvPNormalizationSystem
       │
  [DamageApplySystem UNCHANGED]
  [DeathTransitionSystem → KillCredited/AssistCredited]
       │
  PvPScoringSystem ──→ PvPSpawnSystem ──→ PvPObjectiveSystem
       │
  PvPAntiGriefSystem ──→ PvPRankingSystem (Server only)
       │
  PvPUIBridgeSystem ──→ PvPUIRegistry ──→ IPvPUIProvider (MonoBehaviours)
```

---

## Multiplayer Considerations

- **PvPMatchState** is `Ghost:All` — all clients see phase, timer, scores
- **PvPPlayerStats** is `Ghost:AllPredicted` — owning client sees own K/D/A
- **PvPRanking** is on a non-ghost child entity — changes only visible via save/load
- **Stat normalization** is server-side; changes replicate via existing `AttackStats`/`DefenseStats`/`Health` (all Ghost:All)
- **CollisionGameSettings.FriendlyFireEnabled** toggle enables existing damage pipeline for PvP
- **No new IBufferElementData** on ghost-replicated entities (host-time safety)
- **DamageApplySystem** is UNCHANGED — PvP damage flows through existing pipeline

---

## Spawn Protection

Spawn protection prevents damage for a configurable duration after respawn:

1. `PvPSpawnSystem` enables `PvPSpawnProtection` with expiration tick
2. `PvPSpawnProtectionCheckSystem` runs in `DamageSystemGroup` (before `DamageApplySystem`)
3. While protected: all `DamageEvent` buffers are cleared each prediction tick
4. Protection expires at `ExpirationTick` or when player deals damage

---

## Common Patterns

### Creating a Test Match (Play Mode)

In the PvP Workstation > Match Inspector, click "Create Test Match (TDM)" or "Create Test Match (FFA)".

Or via code:
```csharp
var entity = EntityManager.CreateEntity();
EntityManager.AddComponentData(entity, new PvPMatchRequest
{
    GameMode = PvPGameMode.TeamDeathmatch,
    MapId = 1,
    MaxPlayers = 8,
    MaxScore = 50,
    MatchDuration = 600f
});
```

### Implementing a Custom UI Provider

```csharp
public class MyPvPUI : MonoBehaviour, IPvPUIProvider
{
    void OnEnable() => PvPUIRegistry.Register(this);
    void OnDisable() => PvPUIRegistry.Unregister(this);

    public void UpdateMatchState(PvPMatchUIState state) { /* ... */ }
    public void UpdateScoreboard(PvPScoreboardEntry[] entries) { /* ... */ }
    public void OnKillFeedEvent(PvPKillFeedUIEntry entry) { /* ... */ }
    public void OnMatchPhaseChange(PvPMatchPhase old, PvPMatchPhase next) { /* ... */ }
    public void OnMatchResult(PvPMatchResultUI result) { /* ... */ }
    public void UpdateRanking(PvPRankingUI ranking) { /* ... */ }
}
```

---

## Troubleshooting

| Issue | Cause | Fix |
|-------|-------|-----|
| Players can't damage each other | `CollisionGameSettings.FriendlyFireEnabled` not set | Verify PvPCollisionSystem is running and match is in Warmup+ phase |
| Ghost bake error after adding PvPPlayerAuthoring | Player archetype exceeds 16KB | Check total component size; move PvPStatOverride to child entity if needed |
| No PvP kill scoring | KillCredited entities missing PvPPlayerStats | Verify PvPPlayerAuthoring is on the player prefab |
| Spawn protection not working | PvPSpawnProtectionCheckSystem not in DamageSystemGroup | Check system ordering: must run before DamageApplySystem |
| Ranking not saved | PvPRankingSaveModule not registered | Verify SaveModuleTypeIds.PvPRanking = 15 and module is registered with SaveManager |
| Normalization not applying | NormalizationEnabled = false | Set to true in PvPArenaConfigSO |
| No PvP XP multiplier | PvPKillMarker not on player | Verify PvPPlayerAuthoring bakes PvPKillMarker (disabled) on entity |

---

## Setup Checklist

- [ ] Created `PvPArenaConfig.asset` in `Resources/`
- [ ] Created `PvPRankingConfig.asset` in `Resources/`
- [ ] Created at least one `PvPMapDefinitionSO` in `Resources/PvPMaps/`
- [ ] Added `PvPPlayerAuthoring` to player prefab
- [ ] Reimported player subscene
- [ ] Placed spawn points in arena subscene (>= MaxPlayers per map)
- [ ] Placed capture zones if using CapturePoint mode
- [ ] Verified PvP Workstation shows correctly (DIG > PvP Workstation)
- [ ] Test match creation works in Play Mode
- [ ] Players can damage each other during Active phase
- [ ] Spawn protection prevents damage after respawn
- [ ] K/D/A updates correctly in Player Inspector
- [ ] Elo updates after match ends (Results phase)
- [ ] Rankings persist across save/load

---

## File Manifest

### Components (13 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/PvP/Components/PvPMatchPhase.cs` | Enums: PvPMatchPhase, PvPGameMode, PvPTier |
| `Assets/Scripts/PvP/Components/PvPMatchState.cs` | Singleton (Ghost:All, 32 bytes) |
| `Assets/Scripts/PvP/Components/PvPPlayerStats.cs` | Per-match K/D/A (Ghost:AllPredicted, 24 bytes) |
| `Assets/Scripts/PvP/Components/PvPTeam.cs` | Team assignment (Ghost:AllPredicted, 4 bytes) |
| `Assets/Scripts/PvP/Components/PvPSpawnPoint.cs` | Arena spawn locations (8 bytes) |
| `Assets/Scripts/PvP/Components/PvPSpawnProtection.cs` | Invulnerability (IEnableableComponent, 4 bytes) |
| `Assets/Scripts/PvP/Components/PvPRespawnTimer.cs` | Respawn delay (IEnableableComponent, 4 bytes) |
| `Assets/Scripts/PvP/Components/PvPStatOverride.cs` | Original stats backup (IEnableableComponent, 20 bytes) |
| `Assets/Scripts/PvP/Components/PvPAntiGriefState.cs` | AFK/leaver tracking (8 bytes) |
| `Assets/Scripts/PvP/Components/PvPCaptureZone.cs` | Capture point zone (Ghost:All, 16 bytes) |
| `Assets/Scripts/PvP/Components/PvPRankingComponents.cs` | Link + child: PvPRankingLink, PvPRankingTag, PvPRankingOwner, PvPRanking |
| `Assets/Scripts/PvP/Components/PvPRequestComponents.cs` | Transient requests + RPCs |
| `Assets/Scripts/PvP/Components/PvPKillMarker.cs` | PvP kill tag (IEnableableComponent, 0 bytes) |

### Config (4 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/PvP/Config/PvPConfigBlob.cs` | BlobAsset structs + PvPConfigSingleton |
| `Assets/Scripts/PvP/Definitions/PvPArenaConfigSO.cs` | Arena match parameters SO |
| `Assets/Scripts/PvP/Definitions/PvPMapDefinitionSO.cs` | Per-map definition SO |
| `Assets/Scripts/PvP/Definitions/PvPRankingConfigSO.cs` | Elo ranking parameters SO |

### Systems (11 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/PvP/Systems/PvPBootstrapSystem.cs` | Loads SOs, builds BlobAsset (runs once) |
| `Assets/Scripts/PvP/Systems/PvPMatchSystem.cs` | Match lifecycle state machine |
| `Assets/Scripts/PvP/Systems/PvPCollisionSystem.cs` | Toggles CollisionGameSettings for PvP |
| `Assets/Scripts/PvP/Systems/PvPNormalizationSystem.cs` | Optional stat equalization |
| `Assets/Scripts/PvP/Systems/PvPSpawnProtectionCheckSystem.cs` | Clears DamageEvent on protected entities |
| `Assets/Scripts/PvP/Systems/PvPScoringSystem.cs` | Reads KillCredited, updates K/D/A |
| `Assets/Scripts/PvP/Systems/PvPSpawnSystem.cs` | Respawn with protection |
| `Assets/Scripts/PvP/Systems/PvPObjectiveSystem.cs` | Capture point logic |
| `Assets/Scripts/PvP/Systems/PvPAntiGriefSystem.cs` | AFK detection, leaver tracking |
| `Assets/Scripts/PvP/Systems/PvPRankingSystem.cs` | Post-match Elo calculation (Server only) |
| `Assets/Scripts/PvP/Systems/PvPXPModifierSystem.cs` | Tags PvP kills for XP multiplier |

### Authoring (4 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/PvP/Authoring/PvPPlayerAuthoring.cs` | Player prefab (child entity for ranking) |
| `Assets/Scripts/PvP/Authoring/PvPSpawnPointAuthoring.cs` | Arena spawn points |
| `Assets/Scripts/PvP/Authoring/PvPCaptureZoneAuthoring.cs` | Capture point zones |
| `Assets/Scripts/PvP/Authoring/PvPMatchStateAuthoring.cs` | Match state singleton |

### Bridges (5 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/PvP/Bridges/PvPUIBridgeSystem.cs` | ECS → UI bridge (PresentationSystemGroup) |
| `Assets/Scripts/PvP/Bridges/PvPUIRegistry.cs` | Static provider registry |
| `Assets/Scripts/PvP/Bridges/IPvPUIProvider.cs` | UI provider interface |
| `Assets/Scripts/PvP/Bridges/PvPUIState.cs` | UI state structs |
| `Assets/Scripts/PvP/Bridges/PvPKillFeedQueue.cs` | Static queue for kill feed events |

### UI (6 files)
| File | Description |
|------|-------------|
| `Assets/Scripts/PvP/UI/ScoreboardView.cs` | Tab-toggled K/D/A scoreboard |
| `Assets/Scripts/PvP/UI/MatchTimerView.cs` | Countdown timer + phase text |
| `Assets/Scripts/PvP/UI/KillFeedView.cs` | Scrolling kill notifications |
| `Assets/Scripts/PvP/UI/RankingView.cs` | Elo/Tier with progress bar |
| `Assets/Scripts/PvP/UI/MatchResultsView.cs` | Post-match results overlay |
| `Assets/Scripts/PvP/UI/PvPQueueView.cs` | Queue/lobby for joining matches |

### Persistence (1 file)
| File | Description |
|------|-------------|
| `Assets/Scripts/Persistence/Modules/PvPRankingSaveModule.cs` | ISaveModule (TypeId=15, 23 bytes) |

### Editor (7 files)
| File | Description |
|------|-------------|
| `Assets/Editor/PvPWorkstation/PvPWorkstationWindow.cs` | DIG > PvP Workstation |
| `Assets/Editor/PvPWorkstation/IPvPWorkstationModule.cs` | Module interface |
| `Assets/Editor/PvPWorkstation/Modules/MatchInspectorModule.cs` | Live match state + test buttons |
| `Assets/Editor/PvPWorkstation/Modules/PlayerInspectorModule.cs` | Per-player PvP state |
| `Assets/Editor/PvPWorkstation/Modules/MapEditorModule.cs` | Visual spawn/zone editor |
| `Assets/Editor/PvPWorkstation/Modules/RankingSimulatorModule.cs` | Elo simulation |
| `Assets/Editor/PvPWorkstation/Modules/BalanceAnalyzerModule.cs` | Settings analysis |

### Modified Files (2)
| File | Change |
|------|--------|
| `Assets/Scripts/Progression/Systems/XPAwardSystem.cs` | +PvPKillMarker check for PvPKillXPMultiplier (~8 lines) |
| `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs` | +PvPRanking = 15 |
