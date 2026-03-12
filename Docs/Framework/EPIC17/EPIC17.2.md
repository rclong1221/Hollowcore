# EPIC 17.2: Party & Group System

**Status:** PLANNED
**Priority:** High (Multiplayer Social Loop)
**Dependencies:**
- `CharacterAttributes` IComponentData with `Level` field (existing -- `DIG.Combat.Components.CombatStatComponents.cs`, Ghost:All, 20 bytes)
- `PlayerProgression` IComponentData (existing -- `DIG.Progression.Components.PlayerProgression.cs`, Ghost:AllPredicted, CurrentXP/TotalXPEarned/UnspentStatPoints/RestedXP, 16 bytes)
- `KillCredited` IComponentData event (existing -- `Player.Components.KillAttribution.cs`, AllPredicted, ephemeral on killer entity, Victim/VictimPosition/ServerTick)
- `AssistCredited` IComponentData event (existing -- `Player.Components.KillAttribution.cs`, AllPredicted, Victim/DamageDealt/ServerTick)
- `XPAwardSystem` (existing -- `DIG.Progression.Systems.XPAwardSystem.cs`, reads KillCredited, computes XP with diminishing/rested/gear)
- `DeathLootSystem` (existing -- `DIG.Loot.Systems.DeathLootSystem.cs`, reads DiedEvent, creates PendingLootSpawn, reads killer Level)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, AllPredicted, Gold/Premium/Crafting)
- `CurrencyTransaction` IBufferElementData (existing -- `DIG.Economy`, pending currency operations)
- `InteractionVerb` enum (existing -- `DIG.Interaction.Components.InteractableComponents.cs`, values 0-14, **no Trade verb**)
- `CommandTarget` IComponentData (existing -- `Unity.NetCode`, connection entity -> player entity resolution)
- `GhostOwnerIsLocal` tag (existing -- `Unity.NetCode`, identifies local player's ghost)
- `ReceiveRpcCommandRequest` (existing -- `Unity.NetCode`, RPC receive pattern)
- `StatAllocationRpcReceiveSystem` (existing -- `DIG.Progression.Systems.StatAllocationRpcReceiveSystem.cs`, server-only RPC validation + CommandTarget resolution pattern)
- `ProgressionBootstrapSystem` (existing -- `DIG.Progression.Systems.ProgressionBootstrapSystem.cs`, bootstrap singleton from Resources/ pattern)
- `SaveStateLink` child entity pattern (existing -- `DIG.Persistence.Components.SaveStateComponents.cs`, 8 bytes on player -> child entity)
- `CombatUIBridgeSystem` / `CombatUIRegistry` (existing -- static registry + provider interface pattern)
- `LevelUpVisualQueue` (existing -- `DIG.Progression.UI.LevelUpVisualQueue.cs`, static NativeQueue bridge pattern)
- `ISaveModule` interface (existing -- `DIG.Persistence.Core.ISaveModule.cs`, TypeId + Serialize/Deserialize pattern)
- `SaveModuleTypeIds` (existing -- `DIG.Persistence.Core.SaveModuleTypeIds.cs`, TypeIds 1-10 assigned, 11 reserved for Talents)
- `DeathTransitionSystem` (existing -- `Player.Systems.DeathTransitionSystem.cs`, creates KillCredited via EndSimulationECB)
- `PlayerTag` IComponentData (existing -- `Player.Components`, identifies player entities)

**Feature:** A server-authoritative party system enabling 2-6 player groups with invite/accept/kick/leave/promote RPCs, configurable loot distribution modes (FreeForAll, RoundRobin, NeedGreed, MasterLoot), proximity-based XP sharing with group bonus, party kill credit distribution, and a UI bridge for party frames. Uses child entity pattern -- only 8 bytes (`PartyLink`) added to the player archetype. Party state lives on a separate party entity. All clients see party membership via Ghost:All replication.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `KillCredited` event | `KillAttribution.cs` | Fully implemented, AllPredicted | Ephemeral, Victim + VictimPosition. **Only added to the actual killer** -- no party credit distribution |
| `AssistCredited` event | `KillAttribution.cs` | Fully implemented, AllPredicted | Victim + DamageDealt. Only for direct combat assists |
| `XPAwardSystem` | `XPAwardSystem.cs` | Fully implemented | Reads KillCredited, computes XP, writes PlayerProgression. **No party awareness** -- single killer gets all XP |
| `DeathLootSystem` | `DeathLootSystem.cs` | Fully implemented | Creates PendingLootSpawn on enemy death. **No loot mode logic** -- single loot entity spawned, first-come-first-served |
| `CurrencyInventory` | `DIG.Economy` | Fully implemented, AllPredicted | Gold/Premium/Crafting currencies. Party loot gold splitting uses this |
| `CurrencyTransaction` | `DIG.Economy` | Fully implemented | Pending currency operations buffer |
| `InteractionVerb` enum | `InteractableComponents.cs` | Values 0-14 | **No Trade verb** (needed for NeedGreed item trading) |
| `CommandTarget` | `Unity.NetCode` | Built-in | Connection -> player entity resolution, used by `StatAllocationRpcReceiveSystem` |
| `StatAllocationRpcReceiveSystem` | `StatAllocationRpcReceiveSystem.cs` | Server-only RPC pattern | `ReceiveRpcCommandRequest.SourceConnection -> CommandTarget -> playerEntity` pattern -- Party RPCs follow identical structure |
| `ProgressionBootstrapSystem` | `ProgressionBootstrapSystem.cs` | Bootstrap singleton | Loads SO from Resources/, builds BlobAsset, creates singleton, self-disables |
| `SaveStateLink` child entity | `SaveStateComponents.cs` | 8 bytes on player | Child entity pattern reference for PartyLink |
| `CombatUIRegistry` | `CombatUIBridgeSystem.cs` | Static provider registry | UI bridge pattern reference for PartyUIBridgeSystem |
| `ISaveModule` | `ISaveModule.cs` | Interface + context | TypeId registry, TypeIds 1-11 assigned |
| `PlayerTag` | `Player.Components` | Marker tag | Identifies player entities for party member queries |

### What's Missing

- **No party entity** -- no ECS entity represents a group of players
- **No party membership** -- no component links a player to a party
- **No party RPCs** -- no invite/accept/kick/leave/promote messages
- **No party XP sharing** -- `XPAwardSystem` awards XP only to the killer
- **No party kill credit** -- `DeathTransitionSystem` only credits the actual killer via `KillCredited`
- **No loot distribution modes** -- `DeathLootSystem` creates a single loot entity for anyone to pick up
- **No party proximity tracking** -- no spatial query for "which party members are nearby"
- **No party UI** -- no party frames, invite dialogs, or loot mode selectors
- **No Trade interaction verb** -- `InteractionVerb` enum stops at 14 (Deactivate)

---

## Problem

DIG is a multiplayer action RPG where 2-6 players explore and fight together, but there is no formal party system. Players who cooperate get no mechanical benefit: only the killing blow awards XP (via `KillCredited`), loot spawns as a free-for-all pickup, there is no shared kill credit for quest objectives, and there is no way to see allied health bars or coordinate roles. Specific gaps:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `KillCredited` on killer entity after enemy death | No distribution to nearby party members |
| `XPAwardSystem` computes XP from kill | No XP split/bonus for party members in range |
| `DeathLootSystem` creates PendingLootSpawn | No loot mode (RoundRobin, NeedGreed, MasterLoot) |
| `AssistCredited` for damage assists | No party-specific assist tracking |
| `CurrencyInventory` Gold/Premium/Crafting | No gold split from shared loot |
| `InteractionVerb` enum (0-14) | No Trade verb for NeedGreed item exchange |
| `CommandTarget` for connection -> player | No party invite/accept/kick/leave RPCs |
| `CombatUIRegistry` UI bridge pattern | No party frames UI (health, mana, buffs) |
| `SaveStateLink` child entity (8 bytes) | No party membership persistence |
| `QuestObjectiveProgress` tracking | No shared party credit for kill/gather objectives |

**The gap:** Two players fight a boss together for 5 minutes. The player who lands the killing blow gets 100% of the XP and the loot drops at their feet. The other player gets nothing. There is no way to form a party, share rewards, assign roles, or see allied status. This fundamentally breaks cooperative play.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  PartyConfigSO                  LootModeSO
  (MaxSize, InviteTimeout,       (Per-mode rules:
   XPShareRange, XPShareBonus,    FFA/RoundRobin/
   LootRange, KillCreditRange)    NeedGreed/MasterLoot)
           |                           |
           └────── PartyBootstrapSystem ───────┘
                   (loads from Resources/, creates
                    PartyConfigSingleton, self-disables)
                              |
                    ECS DATA LAYER
  Player Entity:
    PartyLink (8 bytes) ──→ Party Entity:
                              PartyState (LeaderEntity, LootMode, MaxSize, CreationTick)
                              PartyMemberElement buffer (PlayerEntity, ConnectionEntity, JoinTick)
                              PartyProximityState buffer (PlayerEntity, InXPRange, InLootRange)
                              |
  Transient Entities:
    PartyInvite (InviterEntity, InviteeEntity, ExpirationTick)
    PartyLootClaim (ClaimantEntity, LootEntity, ClaimTick)
                              |
                    SYSTEM PIPELINE
                              |
  InitializationSystemGroup (Server|Client|Local):
    PartyBootstrapSystem                — loads config, creates singleton (runs once)

  SimulationSystemGroup (Server|Local):
    PartyRpcReceiveSystem (ServerOnly)  — validates invite/accept/kick/leave/promote RPCs
    PartyFormationSystem                — creates/destroys party entities, manages membership
    PartyInviteTimeoutSystem            — expires stale invites
    PartyProximitySystem                — spatial query: which members in XP/loot range
    PartyKillCreditSystem               — distributes KillCredited to party members in range
      [UpdateBefore(typeof(XPAwardSystem))]
    PartyXPSharingSystem                — modifies XP awards for party members
      [UpdateBefore(typeof(XPAwardSystem))]
    PartyLootSystem                     — distributes loot based on mode
      [UpdateBefore(typeof(DeathLootSystem))]
    PartyCleanupSystem                  — handles disconnects, empty parties
                              |
  PresentationSystemGroup (Client|Local):
    PartyUIBridgeSystem                 — managed, reads party state for UI
      → PartyUIRegistry → IPartyUIProvider → PartyFrameView (MonoBehaviour)
```

### Data Flow (Enemy Dies -> Party XP + Loot Distribution)

```
Frame N (Server):
  1. DeathTransitionSystem: Enemy dies, creates KillCredited on killer via EndSimulationECB

Frame N+1 (Server):
  2. PartyKillCreditSystem: [UpdateBefore(XPAwardSystem)]
     - Reads KillCredited on killer entity
     - Checks killer has PartyLink → resolves PartyEntity
     - Reads PartyMemberElement buffer
     - Reads PartyProximityState: which members are in KillCreditRange
     - For each in-range member (excluding killer): adds KillCredited via ECB
       (Victim=same, VictimPosition=same, ServerTick=same)
     - Tags distributed kills with PartyKillTag (prevents re-distribution)

  3. PartyXPSharingSystem: [UpdateBefore(XPAwardSystem)]
     - Reads all entities with KillCredited + PartyLink
     - Counts in-range party members from PartyProximityState
     - Writes PartyXPModifier on each: XPMultiplier = (1 / memberCount) * (1 + GroupBonus)
       GroupBonus = PartyConfig.XPShareBonus * (memberCount - 1)
       e.g., 2 players: each gets 60% (0.5 * 1.2), 3 players: each gets 46.7% (0.333 * 1.4)

  4. XPAwardSystem (MODIFIED): Reads KillCredited + optional PartyXPModifier
     - If PartyXPModifier present: finalXP *= PartyXPModifier.XPMultiplier
     - Removes PartyXPModifier + PartyKillTag after processing
     - Existing XP formula otherwise unchanged

  5. PartyLootSystem: [UpdateBefore(DeathLootSystem)]
     - Reads DiedEvent on enemy
     - Checks if killer has PartyLink → resolves PartyEntity → reads PartyState.LootMode
     - Based on mode:
       FreeForAll(0):  No modification (DeathLootSystem handles normally)
       RoundRobin(1):  Sets PendingLootSpawn.DesignatedOwner = next party member in rotation
       NeedGreed(2):   Creates PartyLootClaim transient entities, waits for votes
       MasterLoot(3):  Sets PendingLootSpawn.DesignatedOwner = PartyState.LeaderEntity
     - Enqueues loot events to PartyLootVisualQueue for UI

Frame N+1 (Client):
  6. PartyUIBridgeSystem: Reads PartyState + PartyMemberElement
     - Updates party frames (health bars, names, roles)
     - Shows loot roll UI for NeedGreed mode
     - Dequeues visual events for notifications
```

### Critical System Ordering Chain

```
DeathTransitionSystem (EndSimulationECB creates KillCredited)
    | (next frame, ECB playback)
PartyKillCreditSystem [UpdateBefore(typeof(PartyXPSharingSystem))]
    |
PartyXPSharingSystem [UpdateBefore(typeof(XPAwardSystem))]
    |
XPAwardSystem (MODIFIED: reads optional PartyXPModifier)
    |
LevelUpSystem (existing, unchanged)
    |
PartyLootSystem [UpdateBefore(typeof(DeathLootSystem))]
    |
DeathLootSystem (MODIFIED: reads optional PendingLootSpawn.DesignatedOwner)
    |
PartyInviteTimeoutSystem [UpdateAfter(typeof(PartyFormationSystem))]
    |
PartyCleanupSystem [OrderLast in SimulationSystemGroup]
```

---

## ECS Components

### On Player Entity (MINIMAL -- 8 bytes only)

**File:** `Assets/Scripts/Party/Components/PartyLink.cs`

```csharp
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: 8 bytes on player entity. Points to the party entity that
    /// holds all membership/state data. Entity.Null = not in a party.
    /// Follows SaveStateLink/TalentLink child entity pattern.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PartyLink : IComponentData
    {
        [GhostField] public Entity PartyEntity; // 8 bytes (Entity = int Index + int Version)
    }
}
```

**Archetype impact:** 8 bytes on player entity. `PartyEntity == Entity.Null` means not in a party.

### On Party Entity (SEPARATE entity, NOT on player archetype)

**File:** `Assets/Scripts/Party/Components/PartyComponents.cs`

```csharp
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace DIG.Party
{
    /// <summary>
    /// Tag to identify party entities in queries.
    /// </summary>
    public struct PartyTag : IComponentData { } // 0 bytes

    /// <summary>
    /// Core party state. 28 bytes.
    /// Ghost:All so all clients can see party metadata (leader, loot mode).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PartyState : IComponentData
    {
        [GhostField] public Entity LeaderEntity;      // 8 bytes — player entity of leader
        [GhostField] public LootMode LootMode;        // 1 byte
        [GhostField] public byte MaxSize;              // 1 byte (2-6, default 6)
        [GhostField] public byte MemberCount;          // 1 byte — cached count
        // Padding: 1 byte
        [GhostField] public uint CreationTick;         // 4 bytes — server tick when created
        [GhostField] public int RoundRobinIndex;       // 4 bytes — for RoundRobin loot mode
        [GhostField] public Entity PartyOwnerConnection; // 8 bytes — connection of party creator
    }                                                   // Total: 28 bytes

    /// <summary>
    /// Loot distribution mode.
    /// </summary>
    public enum LootMode : byte
    {
        FreeForAll = 0,    // Anyone can pick up loot
        RoundRobin = 1,    // Loot assigned in rotation
        NeedGreed = 2,     // Vote: Need/Greed/Pass per item
        MasterLoot = 3     // Leader assigns loot
    }

    /// <summary>
    /// Buffer element tracking each party member. 20 bytes per entry.
    /// Ghost:All so all clients see party composition.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    [InternalBufferCapacity(6)]
    public struct PartyMemberElement : IBufferElementData
    {
        [GhostField] public Entity PlayerEntity;       // 8 bytes
        [GhostField] public Entity ConnectionEntity;   // 8 bytes
        [GhostField] public uint JoinTick;             // 4 bytes
    }                                                   // Total: 20 bytes per entry

    /// <summary>
    /// Proximity tracking per member. Updated by PartyProximitySystem.
    /// NOT ghost-replicated (server-only spatial data).
    /// 12 bytes per entry.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct PartyProximityState : IBufferElementData
    {
        public Entity PlayerEntity;     // 8 bytes
        public bool InXPRange;          // 1 byte
        public bool InLootRange;        // 1 byte
        public bool InKillCreditRange;  // 1 byte
        // Padding: 1 byte
    }                                    // Total: 12 bytes per entry

    /// <summary>
    /// Ephemeral tag added to KillCredited events distributed by PartyKillCreditSystem.
    /// Prevents re-distribution of already-distributed kills.
    /// </summary>
    public struct PartyKillTag : IComponentData { } // 0 bytes

    /// <summary>
    /// Ephemeral modifier written by PartyXPSharingSystem, read by XPAwardSystem.
    /// Removed after XPAwardSystem processes.
    /// </summary>
    public struct PartyXPModifier : IComponentData
    {
        public float XPMultiplier; // 4 bytes — pre-computed share (e.g., 0.6 for 2-man party)
    }
}
```

### Transient Entities

**File:** `Assets/Scripts/Party/Components/PartyTransients.cs`

```csharp
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace DIG.Party
{
    /// <summary>
    /// Transient entity representing a pending party invite.
    /// Created by PartyRpcReceiveSystem, destroyed by accept/decline/timeout.
    /// 24 bytes.
    /// </summary>
    public struct PartyInvite : IComponentData
    {
        public Entity InviterEntity;     // 8 bytes — player who sent invite
        public Entity InviteeEntity;     // 8 bytes — player who received invite
        public uint ExpirationTick;      // 4 bytes — server tick when invite expires
        public Entity InviterParty;      // 4 bytes — party entity (if inviter already has one)
    }                                     // Total: 24 bytes (Entity is 8, but InviterParty stores just the reference)

    /// <summary>
    /// Transient entity for NeedGreed loot claims.
    /// Created by PartyLootSystem, collected by PartyNeedGreedResolveSystem.
    /// 20 bytes.
    /// </summary>
    public struct PartyLootClaim : IComponentData
    {
        public Entity LootEntity;        // 8 bytes — the PendingLootSpawn entity
        public Entity PartyEntity;       // 8 bytes — which party
        public uint ExpirationTick;      // 4 bytes — vote deadline
    }

    /// <summary>
    /// Buffer on PartyLootClaim entity tracking each member's vote.
    /// 12 bytes per entry.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct LootVoteElement : IBufferElementData
    {
        public Entity PlayerEntity;      // 8 bytes
        public LootVoteType Vote;        // 1 byte
        // Padding: 3 bytes
    }                                     // Total: 12 bytes per entry

    /// <summary>
    /// Vote types for NeedGreed loot mode.
    /// </summary>
    public enum LootVoteType : byte
    {
        Pending = 0,    // Has not voted yet
        Need = 1,       // Needs this item (highest priority)
        Greed = 2,      // Wants for secondary use
        Pass = 3        // Does not want
    }

    /// <summary>
    /// Ephemeral component on PendingLootSpawn. Designates which player
    /// is allowed to pick up the loot (for RoundRobin/MasterLoot/NeedGreed winner).
    /// Entity.Null = anyone (FreeForAll mode).
    /// </summary>
    public struct LootDesignation : IComponentData
    {
        public Entity DesignatedOwner;   // 8 bytes
        public uint ExpirationTick;      // 4 bytes — reverts to FFA after timeout
    }                                     // Total: 12 bytes
}
```

### RPCs

**File:** `Assets/Scripts/Party/Components/PartyRpcs.cs`

```csharp
using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace DIG.Party
{
    /// <summary>
    /// RPC types for party operations. Byte-sized for network efficiency.
    /// </summary>
    public enum PartyRpcType : byte
    {
        Invite = 0,         // Invite a player to party
        AcceptInvite = 1,   // Accept pending invite
        DeclineInvite = 2,  // Decline pending invite
        Leave = 3,          // Leave current party
        Kick = 4,           // Kick a member (leader only)
        Promote = 5,        // Promote member to leader (leader only)
        SetLootMode = 6,    // Change loot mode (leader only)
        LootVote = 7        // NeedGreed vote (Need/Greed/Pass)
    }

    /// <summary>
    /// Client -> Server RPC for all party operations.
    /// 16 bytes.
    /// </summary>
    public struct PartyRpc : IRpcCommand
    {
        public PartyRpcType Type;          // 1 byte
        // Padding: 3 bytes
        public Entity TargetPlayer;        // 8 bytes — target for invite/kick/promote
        public byte Payload;               // 1 byte — LootMode for SetLootMode, LootVoteType for LootVote
        // Padding: 3 bytes
    }                                       // Total: 16 bytes

    /// <summary>
    /// Server -> Client notification RPC.
    /// Sent to inform clients of party events (invite received, member joined, etc.).
    /// 16 bytes.
    /// </summary>
    public struct PartyNotifyRpc : IRpcCommand
    {
        public PartyNotifyType Type;       // 1 byte
        // Padding: 3 bytes
        public Entity SourcePlayer;        // 8 bytes — who triggered the event
        public byte Payload;               // 1 byte
        // Padding: 3 bytes
    }

    public enum PartyNotifyType : byte
    {
        InviteReceived = 0,
        InviteExpired = 1,
        MemberJoined = 2,
        MemberLeft = 3,
        MemberKicked = 4,
        LeaderChanged = 5,
        LootModeChanged = 6,
        PartyDisbanded = 7,
        LootRollStart = 8,
        LootRollResult = 9,
        InviteDeclined = 10
    }
}
```

### Singleton (Config)

**File:** `Assets/Scripts/Party/Config/PartyConfigSingleton.cs`

```csharp
using Unity.Entities;

namespace DIG.Party
{
    /// <summary>
    /// Singleton loaded by PartyBootstrapSystem from Resources/PartyConfig.
    /// 40 bytes.
    /// </summary>
    public struct PartyConfigSingleton : IComponentData
    {
        public byte MaxPartySize;              // 1 byte (2-6, default 6)
        // Padding: 3 bytes
        public int InviteTimeoutTicks;         // 4 bytes (default ~1800 = 60s at 30Hz)
        public float XPShareRange;             // 4 bytes (default 50.0 units)
        public float XPShareBonusPerMember;    // 4 bytes (default 0.10 = 10% bonus per extra member)
        public float LootRange;                // 4 bytes (default 60.0 units)
        public float KillCreditRange;          // 4 bytes (default 50.0 units — matches XP range)
        public int LootDesignationTimeoutTicks; // 4 bytes (default ~900 = 30s at 30Hz)
        public int NeedGreedVoteTimeoutTicks;  // 4 bytes (default ~450 = 15s at 30Hz)
        public float LootGoldSplitPercent;     // 4 bytes (default 1.0 = equal split)
        public bool AllowLootModeVote;         // 1 byte (default false — leader decides)
        // Padding: 3 bytes
    }                                           // Total: 40 bytes
}
```

---

## ScriptableObjects

### PartyConfigSO

**File:** `Assets/Scripts/Party/Config/PartyConfigSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Party/Party Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| MaxPartySize | int [Range(2,6)] | 6 | Maximum players per party |
| InviteTimeoutSeconds | float | 60 | Seconds before pending invite expires |
| XPShareRange | float | 50.0 | Distance (units) within which members share XP |
| XPShareBonusPerMember | float [0-1] | 0.10 | Per-member XP bonus (e.g., 0.10 = +10% per extra member) |
| LootRange | float | 60.0 | Distance within which members are eligible for loot |
| KillCreditRange | float | 50.0 | Distance for party kill credit distribution |
| LootDesignationTimeoutSeconds | float | 30 | Seconds before designated loot reverts to FFA |
| NeedGreedVoteTimeoutSeconds | float | 15 | Seconds for NeedGreed vote before auto-Pass |
| LootGoldSplitPercent | float [0-1] | 1.0 | Fraction of gold split equally (1.0 = full split) |
| AllowLootModeVote | bool | false | Allow non-leaders to request loot mode change |
| DefaultLootMode | LootMode | FreeForAll | Initial loot mode when party is formed |

### LootModeSO

**File:** `Assets/Scripts/Party/Config/LootModeSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Party/Loot Mode")]
```

| Field | Type | Purpose |
|-------|------|---------|
| ModeName | string | Display name (e.g., "Round Robin") |
| ModeType | LootMode | Enum value |
| Description | string | Tooltip text for UI |
| IconPath | string | Sprite path for loot mode icon |
| RequiredMinMembers | int | Minimum party size (NeedGreed requires 2+) |
| AllowGoldSplit | bool | Whether gold from drops is auto-split |
| AllowCurrencyLoot | bool | Whether currency drops are split or individual |

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Server|Client|Local):
  PartyBootstrapSystem                    — loads PartyConfigSO from Resources/, creates singleton (runs once)

SimulationSystemGroup (Server|Local):
  PartyRpcReceiveSystem (ServerOnly)      — validates all party RPCs, creates transient entities
    [UpdateBefore(typeof(PartyFormationSystem))]
  PartyFormationSystem                    — creates/destroys party entities, adds/removes members
    [UpdateBefore(typeof(PartyProximitySystem))]
  PartyInviteTimeoutSystem               — destroys expired PartyInvite entities
    [UpdateAfter(typeof(PartyFormationSystem))]
  PartyProximitySystem                   — spatial distance check, updates PartyProximityState
    [UpdateBefore(typeof(PartyKillCreditSystem))]
  PartyKillCreditSystem                  — distributes KillCredited to nearby party members
    [UpdateBefore(typeof(PartyXPSharingSystem))]
  PartyXPSharingSystem                   — writes PartyXPModifier on party members with KillCredited
    [UpdateBefore(typeof(XPAwardSystem))]
  [existing] XPAwardSystem              — MODIFIED: reads optional PartyXPModifier
  PartyLootSystem                        — intercepts loot drops, applies loot mode
    [UpdateBefore(typeof(DeathLootSystem))]
  [existing] DeathLootSystem            — MODIFIED: reads optional LootDesignation
  PartyNeedGreedResolveSystem            — resolves NeedGreed votes after timeout or all voted
    [UpdateAfter(typeof(DeathLootSystem))]
  PartyCleanupSystem                     — handles disconnects, empty parties, stale transients
    [OrderLast]

PresentationSystemGroup (Client|Local):
  PartyUIBridgeSystem                    — managed, reads party state + member data for UI
```

### PartyBootstrapSystem

**File:** `Assets/Scripts/Party/Systems/PartyBootstrapSystem.cs`

- `[WorldSystemFilter(ServerSimulation | ClientSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(InitializationSystemGroup))]`
- Loads `PartyConfigSO` from `Resources/PartyConfig`
- Converts `InviteTimeoutSeconds` to ticks using `NetworkTime.ServerTick` rate
- Creates `PartyConfigSingleton` entity
- `Enabled = false` (self-disables after first run)
- Follows `ProgressionBootstrapSystem` pattern exactly

### PartyRpcReceiveSystem

**File:** `Assets/Scripts/Party/Systems/PartyRpcReceiveSystem.cs`

- `[WorldSystemFilter(ServerSimulation)]`
- `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- `[UpdateBefore(typeof(PartyFormationSystem))]`
- Receives `PartyRpc` entities with `ReceiveRpcCommandRequest`
- Resolves `SourceConnection -> CommandTarget -> playerEntity` (same as `StatAllocationRpcReceiveSystem`)
- Validation per RPC type:

| Type | Validation | Action |
|------|-----------|--------|
| Invite | Player not in a full party, target not already in a party, target not self, no duplicate pending invite | Create `PartyInvite` transient entity, send `PartyNotifyRpc(InviteReceived)` to invitee |
| AcceptInvite | Matching `PartyInvite` exists, not expired, invitee matches source | Write `PartyJoinRequest` component on invite entity |
| DeclineInvite | Matching `PartyInvite` exists | Destroy invite, send `PartyNotifyRpc(InviteDeclined)` to inviter |
| Leave | Player has `PartyLink` | Write `PartyLeaveRequest` component on player |
| Kick | Source is leader (`PartyState.LeaderEntity`), target in same party | Write `PartyKickRequest` component targeting member |
| Promote | Source is leader, target in same party | Write `PartyPromoteRequest` component |
| SetLootMode | Source is leader (or `AllowLootModeVote` is true) | Write `PartyLootModeRequest` component |
| LootVote | Active `PartyLootClaim` exists for this player's party | Write `LootVoteElement` into claim's buffer |

All invalid RPCs: destroy RPC entity, no action.

### PartyFormationSystem

**File:** `Assets/Scripts/Party/Systems/PartyFormationSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- Processes request components written by `PartyRpcReceiveSystem`:

**Create Party (on accepted invite, inviter has no party):**
1. Create new entity with: `PartyTag`, `PartyState`, `PartyMemberElement` buffer, `PartyProximityState` buffer
2. Set `PartyState.LeaderEntity = inviter`, `LootMode = DefaultLootMode`, `MaxSize = config.MaxPartySize`
3. Add inviter + invitee to `PartyMemberElement` buffer
4. Set `PartyLink.PartyEntity` on both players
5. Destroy `PartyInvite` transient

**Join Existing Party (inviter already has party):**
1. Validate member count < MaxSize
2. Append invitee to `PartyMemberElement` buffer
3. Add `PartyProximityState` entry for invitee
4. Set `PartyLink.PartyEntity` on invitee
5. Increment `PartyState.MemberCount`
6. Destroy `PartyInvite`, send `PartyNotifyRpc(MemberJoined)` to all members

**Leave:**
1. Remove member from `PartyMemberElement` buffer
2. Remove from `PartyProximityState` buffer
3. Set `PartyLink.PartyEntity = Entity.Null` on leaving player
4. Decrement `PartyState.MemberCount`
5. If leaving player was leader: auto-promote longest-standing member
6. If party has < 2 members: disband (destroy party entity, clear all PartyLinks)
7. Send `PartyNotifyRpc(MemberLeft)` to remaining members

**Kick:** Same as Leave but source must be leader. Send `PartyNotifyRpc(MemberKicked)`.

**Promote:** Set `PartyState.LeaderEntity = target`. Send `PartyNotifyRpc(LeaderChanged)`.

**SetLootMode:** Set `PartyState.LootMode = payload`. Send `PartyNotifyRpc(LootModeChanged)`.

### PartyInviteTimeoutSystem

**File:** `Assets/Scripts/Party/Systems/PartyInviteTimeoutSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(PartyFormationSystem))]`
- Queries `PartyInvite` where `ExpirationTick <= currentServerTick`
- Destroys expired invites via ECB
- Sends `PartyNotifyRpc(InviteExpired)` to both inviter and invitee

### PartyProximitySystem

**File:** `Assets/Scripts/Party/Systems/PartyProximitySystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateBefore(typeof(PartyKillCreditSystem))]`
- Runs every frame (lightweight -- max 6 members * 5 distance checks = 30 `math.distancesq`)
- For each party entity:
  1. Read `PartyMemberElement` buffer -> get all member entities
  2. Look up `LocalTransform.Position` for each member via `ComponentLookup`
  3. For each pair: compute `math.distancesq`
  4. Update `PartyProximityState` buffer:
     - `InXPRange = distanceSq <= XPShareRange * XPShareRange`
     - `InLootRange = distanceSq <= LootRange * LootRange`
     - `InKillCreditRange = distanceSq <= KillCreditRange * KillCreditRange`
  5. Proximity is relative to the killer/looter, not pairwise -- each member's range is checked from the event source position

### PartyKillCreditSystem

**File:** `Assets/Scripts/Party/Systems/PartyKillCreditSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- `[UpdateBefore(typeof(PartyXPSharingSystem))]`
- Query: entities with `KillCredited` + `PartyLink` + `WithNone<PartyKillTag>`
- For each killer with a party:
  1. Resolve `PartyLink.PartyEntity`
  2. Read `PartyProximityState` buffer for in-range members
  3. For each member in `KillCreditRange` (excluding the killer):
     - Add `KillCredited { Victim=same, VictimPosition=same, ServerTick=same }` via ECB
     - Add `PartyKillTag` on distributed kills (prevents infinite re-distribution)
  4. Add `PartyKillTag` on original killer's `KillCredited` too

**Critical:** Uses `EndSimulationEntityCommandBufferSystem` ECB. The distributed `KillCredited` components are available next frame -- but `PartyXPSharingSystem` and `XPAwardSystem` run AFTER this system in the same frame on the original killer. The distributed kills are processed next frame. This is acceptable because:
- Original killer gets XP this frame (with party modifier)
- Party members get XP next frame (with party modifier)
- 1-frame delay for party XP is imperceptible

### PartyXPSharingSystem

**File:** `Assets/Scripts/Party/Systems/PartyXPSharingSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- `[UpdateBefore(typeof(XPAwardSystem))]`
- Query: entities with `KillCredited` + `PartyLink` + `PlayerProgression`
- For each entity:
  1. Resolve `PartyLink.PartyEntity`
  2. Read `PartyProximityState` -> count members in XP range
  3. If memberCount <= 1: skip (solo, full XP)
  4. Compute multiplier:
     ```
     shareBase = 1.0 / memberCount
     groupBonus = XPShareBonusPerMember * (memberCount - 1)
     XPMultiplier = shareBase * (1.0 + groupBonus)
     ```
     Examples at default 10% bonus:
     - 2 players: 0.5 * 1.1 = 0.55 (55% each, 110% total)
     - 3 players: 0.333 * 1.2 = 0.40 (40% each, 120% total)
     - 4 players: 0.25 * 1.3 = 0.325 (32.5% each, 130% total)
     - 6 players: 0.167 * 1.5 = 0.25 (25% each, 150% total)
  5. Add `PartyXPModifier { XPMultiplier }` to entity via ECB (same frame, structural change)

### PartyLootSystem

**File:** `Assets/Scripts/Party/Systems/PartyLootSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- `[UpdateBefore(typeof(DeathLootSystem))]`
- Intercepts enemy death loot distribution based on party loot mode
- Reads `DiedEvent` + resolves killer -> `PartyLink` -> `PartyState.LootMode`

**FreeForAll (0):** No action. `DeathLootSystem` handles normally.

**RoundRobin (1):**
1. Read `PartyState.RoundRobinIndex`
2. Get next in-range member from `PartyMemberElement[index % count]`
3. Add `LootDesignation { DesignatedOwner = member, ExpirationTick = now + timeout }` to `PendingLootSpawn`
4. Increment `RoundRobinIndex`
5. Gold from drop: split equally among in-range members via `CurrencyTransaction`

**NeedGreed (2):**
1. Create `PartyLootClaim` transient entity
2. Add `LootVoteElement` buffer with one entry per in-range member (all `Pending`)
3. Set `PartyLootClaim.ExpirationTick = now + NeedGreedVoteTimeoutTicks`
4. Hold loot until resolved by `PartyNeedGreedResolveSystem`
5. Send `PartyNotifyRpc(LootRollStart)` to party members

**MasterLoot (3):**
1. Add `LootDesignation { DesignatedOwner = PartyState.LeaderEntity, ExpirationTick = now + timeout }`
2. Leader can then use a "distribute" UI action to assign to specific member
3. Gold: leader decides split (or auto-split if configured)

### PartyNeedGreedResolveSystem

**File:** `Assets/Scripts/Party/Systems/PartyNeedGreedResolveSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateAfter(typeof(DeathLootSystem))]`
- Queries `PartyLootClaim` entities
- Resolves when: all members voted OR `ExpirationTick` reached (pending votes become Pass)
- Resolution priority: Need > Greed > Pass
- Ties: random winner among same-priority voters (deterministic from `ServerTick + entityIndex`)
- Winner gets `LootDesignation.DesignatedOwner` set on the loot entity
- Gold: split among all Need/Greed voters proportionally
- Destroy `PartyLootClaim` after resolution
- Send `PartyNotifyRpc(LootRollResult)` with winner info

### PartyCleanupSystem

**File:** `Assets/Scripts/Party/Systems/PartyCleanupSystem.cs`

- `[WorldSystemFilter(ServerSimulation | LocalSimulation)]`
- `[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]`
- Handles:
  1. **Disconnected members:** Query players with `PartyLink` but no `PlayerTag` (destroyed on disconnect) -- remove from party
  2. **Empty parties:** Destroy party entities with 0-1 members
  3. **Stale PartyXPModifier:** Remove `PartyXPModifier` and `PartyKillTag` components left over from processed kills
  4. **Expired LootDesignation:** Revert to FFA (set `DesignatedOwner = Entity.Null`)
  5. **Orphaned PartyLootClaim:** Destroy claims for parties that no longer exist

### PartyUIBridgeSystem

**File:** `Assets/Scripts/Party/Bridges/PartyUIBridgeSystem.cs`

- Managed `SystemBase`, `PresentationSystemGroup`, `Client|Local`
- Reads local player's `PartyLink` -> `PartyState` + `PartyMemberElement` buffer
- For each party member: reads `Health`, `CharacterAttributes.Level`, `PlayerProgression` via `ComponentLookup`
- Pushes to `PartyUIRegistry` -> `IPartyUIProvider`:
  - `UpdatePartyState(PartyUIState)` -- full party info
  - `OnMemberJoined(PartyMemberUIState)` -- new member notification
  - `OnMemberLeft(Entity)` -- member departed
  - `OnInviteReceived(Entity inviter, string inviterName)` -- show invite dialog
  - `OnLootRollStart(LootRollUIState)` -- show NeedGreed voting UI
  - `OnLootRollResult(Entity winner, string itemName)` -- show roll result
- Dequeues `PartyVisualQueue` for notification events

---

## Authoring

**File:** `Assets/Scripts/Party/Authoring/PartyAuthoring.cs`

```
[AddComponentMenu("DIG/Party/Party Member")]
```

- Place on player prefab (Warrok_Server) alongside existing `PlayerAuthoring`, `ProgressionAuthoring`, `SaveStateAuthoring`
- Baker adds: `PartyLink { PartyEntity = Entity.Null }` to player entity
- No child entity needed (party data lives on separate party entities, not on player)
- Minimal baker -- only adds the 8-byte link component

```csharp
using Unity.Entities;

namespace DIG.Party
{
    public class PartyAuthoring : UnityEngine.MonoBehaviour
    {
        public class Baker : Baker<PartyAuthoring>
        {
            public override void Bake(PartyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PartyLink { PartyEntity = Entity.Null });
            }
        }
    }
}
```

---

## UI Bridge

**File:** `Assets/Scripts/Party/Bridges/PartyUIRegistry.cs`

Static singleton registry (same pattern as `CombatUIRegistry`):

```csharp
public static class PartyUIRegistry
{
    private static IPartyUIProvider _provider;
    public static bool HasProvider => _provider != null;
    public static IPartyUIProvider Provider => _provider;

    public static void Register(IPartyUIProvider provider) => _provider = provider;
    public static void Unregister(IPartyUIProvider provider)
    {
        if (_provider == provider) _provider = null;
    }
}
```

**File:** `Assets/Scripts/Party/Bridges/IPartyUIProvider.cs`

```csharp
public interface IPartyUIProvider
{
    void UpdatePartyState(PartyUIState state);
    void OnMemberJoined(PartyMemberUIState member);
    void OnMemberLeft(Entity memberEntity);
    void OnInviteReceived(PartyInviteUIState invite);
    void OnInviteExpired();
    void OnLootRollStart(LootRollUIState roll);
    void OnLootRollResult(LootRollResultUIState result);
    void OnPartyDisbanded();
    void OnLootModeChanged(LootMode newMode);
    void OnLeaderChanged(Entity newLeader);
}
```

**File:** `Assets/Scripts/Party/Bridges/PartyUIState.cs`

```csharp
public struct PartyUIState
{
    public bool InParty;
    public bool IsLeader;
    public LootMode CurrentLootMode;
    public int MemberCount;
    public int MaxSize;
    public PartyMemberUIState[] Members;
}

public struct PartyMemberUIState
{
    public Entity PlayerEntity;
    public string DisplayName;
    public int Level;
    public float HealthCurrent;
    public float HealthMax;
    public float ManaCurrent;
    public float ManaMax;
    public bool IsLeader;
    public bool IsInRange;
    public bool IsAlive;
}

public struct PartyInviteUIState
{
    public Entity InviterEntity;
    public string InviterName;
    public int InviterLevel;
    public float TimeRemainingSeconds;
}

public struct LootRollUIState
{
    public Entity LootEntity;
    public string ItemName;
    public int ItemTypeId;
    public float TimeRemainingSeconds;
}

public struct LootRollResultUIState
{
    public Entity WinnerEntity;
    public string WinnerName;
    public string ItemName;
    public LootVoteType WinningVote;
}
```

**File:** `Assets/Scripts/Party/Bridges/PartyVisualQueue.cs`

Static NativeQueue bridge (same pattern as `DamageVisualQueue`, `LevelUpVisualQueue`):

```csharp
public static class PartyVisualQueue
{
    // Queue entries for party events bridged to UI
    public struct PartyVisualEvent
    {
        public PartyNotifyType Type;
        public Entity SourcePlayer;
        public byte Payload;
    }

    private static NativeQueue<PartyVisualEvent> _queue;

    public static void Initialize() { _queue = new NativeQueue<PartyVisualEvent>(Allocator.Persistent); }
    public static void Dispose() { if (_queue.IsCreated) _queue.Dispose(); }
    public static void Enqueue(PartyVisualEvent evt) { _queue.Enqueue(evt); }
    public static bool TryDequeue(out PartyVisualEvent evt) => _queue.TryDequeue(out evt);
}
```

**File:** `Assets/Scripts/Party/UI/PartyFrameView.cs`

MonoBehaviour implementing `IPartyUIProvider`. Shows 2-5 party member frames with:
- Health bar (read from `PartyMemberUIState.HealthCurrent/Max`)
- Mana bar (read from `PartyMemberUIState.ManaCurrent/Max`)
- Level badge
- Leader crown icon
- Out-of-range dimming
- Dead state grayed out

**File:** `Assets/Scripts/Party/UI/PartyInviteDialogView.cs`

MonoBehaviour for invite accept/decline popup. Sends `PartyRpc(AcceptInvite)` or `PartyRpc(DeclineInvite)`.

**File:** `Assets/Scripts/Party/UI/LootRollView.cs`

MonoBehaviour for NeedGreed voting UI. Shows item icon + Need/Greed/Pass buttons. Timer bar. Sends `PartyRpc(LootVote, payload=vote)`.

---

## Save Integration

**File:** `Assets/Scripts/Persistence/Modules/PartySaveModule.cs`

```
ISaveModule implementation:
  TypeId = 12
  DisplayName = "Party"
  ModuleVersion = 1
```

Party membership is inherently session-based (parties disband on server shutdown), so persistence is minimal:

Serializes:
- `PartyLink.PartyEntity != Entity.Null` -> `bool InParty` (1 byte)
- `LootMode` preference (1 byte) -- restored as default when re-forming party
- Last known party members' `PlayerSaveId` strings (for reconnection matching)

```
InParty              : byte (0 or 1)
PreferredLootMode    : byte (LootMode enum)
LastPartyMemberCount : byte (0-6)
foreach member:
  PlayerSaveId       : FixedString64Bytes (64 bytes)
```

On deserialize:
- Does NOT recreate party entities (parties are session-scoped)
- Stores preferred loot mode in a managed cache for `PartyFormationSystem` to read when next party is formed
- Stores last party member IDs for "rejoin last party" UI feature

**Dirty check:** `IsDirty()` returns true only when `PartyLink` changes (join/leave) -- very rare writes.

---

## Editor Tooling

### PartyWorkstationWindow

**File:** `Assets/Editor/PartyWorkstation/PartyWorkstationWindow.cs`

- Menu: `DIG/Party Workstation`
- Sidebar + `IPartyWorkstationModule` interface pattern (matches ProgressionWorkstation)

### Modules

| Module | File | Purpose |
|--------|------|---------|
| Party Inspector | `Modules/PartyInspectorModule.cs` | Play-mode: live party entity state, member list with health/level, leader indicator, loot mode, proximity flags per member |
| Config Editor | `Modules/PartyConfigEditorModule.cs` | Edit `PartyConfigSO`: range sliders with preview spheres, XP sharing formula preview table (2-6 members), loot mode descriptions |
| XP Simulator | `Modules/XPSharingSimulatorModule.cs` | "Kill enemy" button with configurable enemy level + party size. Shows per-member XP before/after party sharing. Compares solo vs party XP over N kills |
| Loot Simulator | `Modules/LootSimulatorModule.cs` | Simulate loot drops with each mode. RoundRobin rotation display, NeedGreed vote mock, MasterLoot assignment mock. Item distribution fairness histogram over 100 drops |
| Network Debugger | `Modules/NetworkDebugModule.cs` | Play-mode: RPC log (sent/received party RPCs), latency per member, invite state machine visualization |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `PartyLink` | 8 bytes | Player entity |
| **Total on player** | **8 bytes** | |

All party state lives on SEPARATE party entities (PartyState=28, PartyMemberElement buffer=120 max, PartyProximityState buffer=72 max). The player entity only gains the 8-byte `PartyLink`. This follows the same pattern as `SaveStateLink` (8 bytes), `TalentLink` (8 bytes), and `CraftingKnowledgeLink`.

Party entity archetype breakdown:
| Component | Size | Notes |
|-----------|------|-------|
| `PartyTag` | 0 bytes | Marker tag |
| `PartyState` | 28 bytes | Leader, LootMode, counters |
| `PartyMemberElement` buffer header | ~16 bytes | InternalBufferCapacity=6, 20 bytes/entry |
| `PartyMemberElement` inline data | 120 bytes | 6 entries * 20 bytes |
| `PartyProximityState` buffer header | ~16 bytes | InternalBufferCapacity=6, 12 bytes/entry |
| `PartyProximityState` inline data | 72 bytes | 6 entries * 12 bytes |
| `GhostComponent` (NetCode) | ~24 bytes | Ghost replication metadata |
| **Total party entity** | **~276 bytes** | Well within 16KB |

---

## Performance Budget

| System | Target | Burst | Notes |
|--------|--------|-------|-------|
| `PartyBootstrapSystem` | N/A | No | Runs once at startup |
| `PartyRpcReceiveSystem` | < 0.01ms | No | Only on player input (rare RPCs) |
| `PartyFormationSystem` | < 0.01ms | No | Only on join/leave (very rare) |
| `PartyInviteTimeoutSystem` | < 0.01ms | No | Max ~10 pending invites |
| `PartyProximitySystem` | < 0.02ms | No | Max 6 members * 5 distance checks = 30 distancesq. Could Burst but unnecessary |
| `PartyKillCreditSystem` | < 0.02ms | No | Only when kills occur (~1-5/sec). Max 5 credit distributions per kill |
| `PartyXPSharingSystem` | < 0.01ms | No | Only when kills occur. Single lookup + multiply |
| `PartyLootSystem` | < 0.02ms | No | Only on enemy death (~1-5/sec). Loot mode branch |
| `PartyNeedGreedResolveSystem` | < 0.01ms | No | Only when votes resolve (rare) |
| `PartyCleanupSystem` | < 0.01ms | No | Disconnect check + stale cleanup |
| `PartyUIBridgeSystem` | < 0.05ms | No | Managed, reads 2-6 member components, pushes to UI |
| **Total** | **< 0.17ms** | | All systems combined, during active combat |

Idle party (no kills, no loot): `PartyProximitySystem` (0.02ms) + `PartyUIBridgeSystem` (0.03ms) = ~0.05ms/frame.

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| Entity without PartyLink | No party data | All party systems skip. XPAwardSystem unchanged (no PartyXPModifier = full XP) |
| PartyLink with Entity.Null | Not in party | PartyKillCreditSystem/PartyXPSharingSystem/PartyLootSystem skip. Existing behavior preserved |
| No PartyConfigSO in Resources/ | Warning at startup | PartyBootstrapSystem logs error, does not create singleton. All systems RequireForUpdate and remain dormant |
| No IPartyUIProvider registered | Warning at frame 120 | PartyUIBridgeSystem runs, no UI displayed. ECS systems unaffected |
| XPAwardSystem without PartyXPModifier | Full XP (multiplier=1.0) | Solo kills work identically to pre-party behavior |
| DeathLootSystem without LootDesignation | FreeForAll loot | Single loot entity, anyone picks up. Existing behavior |
| Old save without PartySaveModule (TypeId=12) | Module skipped on load | LoadSystem skips unknown TypeId. PartyLink defaults to Entity.Null |

---

## Modified Existing Systems

### XPAwardSystem (MODIFY -- ~15 lines)

**File:** `Assets/Scripts/Progression/Systems/XPAwardSystem.cs`

Add `ComponentLookup<PartyXPModifier>` read. After computing `finalXP`, check:

```csharp
// Party XP sharing modifier
float partyMult = 1.0f;
if (partyXPModLookup.HasComponent(entities[i]))
    partyMult = partyXPModLookup[entities[i]].XPMultiplier;
finalXP *= partyMult;

// Remove ephemeral party components
if (partyXPModLookup.HasComponent(entities[i]))
    ecb.RemoveComponent<PartyXPModifier>(entities[i]);
if (partyKillLookup.HasComponent(entities[i]))
    ecb.RemoveComponent<PartyKillTag>(entities[i]);
```

### DeathLootSystem (MODIFY -- ~20 lines)

**File:** `Assets/Scripts/Loot/Systems/DeathLootSystem.cs`

After creating `PendingLootSpawn`, check for `LootDesignation`:

```csharp
// Party loot designation (set by PartyLootSystem)
if (lootDesignLookup.HasComponent(lootEntity))
{
    var designation = lootDesignLookup[lootEntity];
    // Set pickup restriction on spawned loot
    ecb.AddComponent(spawnedEntity, new LootPickupRestriction
    {
        DesignatedOwner = designation.DesignatedOwner,
        ExpirationTick = designation.ExpirationTick
    });
}
```

### DeathTransitionSystem (MODIFY -- ~10 lines)

**File:** `Assets/Scripts/Player/Systems/DeathTransitionSystem.cs`

No direct changes needed. `PartyKillCreditSystem` reads the `KillCredited` component that `DeathTransitionSystem` already creates. Party credit distribution happens in the party system layer, not here.

### InteractionVerb (MODIFY -- +1 enum value)

**File:** `Assets/Scripts/Interaction/Components/InteractableComponents.cs`

Add `Trade = 15` to `InteractionVerb` enum for future NeedGreed item trading interactions.

---

## Multiplayer

- **Server-authoritative:** All party operations (create, join, leave, kick, promote, loot distribute) processed on server only. Clients send RPCs, server validates.
- **Ghost-replicated:** `PartyLink` is `Ghost:AllPredicted` -- owning client sees party entity reference. `PartyState` and `PartyMemberElement` are `Ghost:All` -- ALL clients see party composition (needed for party frames on other players, nameplate group indicators).
- **RPC validation:** `PartyRpcReceiveSystem` validates every RPC (same pattern as `StatAllocationRpcReceiveSystem`). Invalid RPCs silently destroyed.
- **No new IBufferElementData on ghost-replicated player entities.** `PartyMemberElement` lives on the PARTY entity, not on individual players.
- **Disconnect handling:** `PartyCleanupSystem` detects missing player entities (destroyed on disconnect) and removes them from the party. Leader auto-promotion if leader disconnects.
- **Reconnection:** `PartySaveModule` stores last party member IDs. On reconnect, if the same party still exists (other members stayed connected), the server can auto-rejoin the reconnecting player.

---

## File Summary

### New Files (32)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Party/Components/PartyLink.cs` | IComponentData (8 bytes on player) |
| 2 | `Assets/Scripts/Party/Components/PartyComponents.cs` | IComponentData + Buffers (on party entity) |
| 3 | `Assets/Scripts/Party/Components/PartyTransients.cs` | Transient entity components |
| 4 | `Assets/Scripts/Party/Components/PartyRpcs.cs` | IRpcCommand + enums |
| 5 | `Assets/Scripts/Party/Config/PartyConfigSingleton.cs` | Singleton IComponentData |
| 6 | `Assets/Scripts/Party/Config/PartyConfigSO.cs` | ScriptableObject |
| 7 | `Assets/Scripts/Party/Config/LootModeSO.cs` | ScriptableObject |
| 8 | `Assets/Scripts/Party/Systems/PartyBootstrapSystem.cs` | SystemBase (Init, runs once) |
| 9 | `Assets/Scripts/Party/Systems/PartyRpcReceiveSystem.cs` | SystemBase (ServerSimulation) |
| 10 | `Assets/Scripts/Party/Systems/PartyFormationSystem.cs` | SystemBase (Server|Local) |
| 11 | `Assets/Scripts/Party/Systems/PartyInviteTimeoutSystem.cs` | SystemBase (Server|Local) |
| 12 | `Assets/Scripts/Party/Systems/PartyProximitySystem.cs` | SystemBase (Server|Local) |
| 13 | `Assets/Scripts/Party/Systems/PartyKillCreditSystem.cs` | SystemBase (Server|Local) |
| 14 | `Assets/Scripts/Party/Systems/PartyXPSharingSystem.cs` | SystemBase (Server|Local) |
| 15 | `Assets/Scripts/Party/Systems/PartyLootSystem.cs` | SystemBase (Server|Local) |
| 16 | `Assets/Scripts/Party/Systems/PartyNeedGreedResolveSystem.cs` | SystemBase (Server|Local) |
| 17 | `Assets/Scripts/Party/Systems/PartyCleanupSystem.cs` | SystemBase (Server|Local, OrderLast) |
| 18 | `Assets/Scripts/Party/Authoring/PartyAuthoring.cs` | Baker (minimal, adds PartyLink) |
| 19 | `Assets/Scripts/Party/Bridges/PartyUIBridgeSystem.cs` | SystemBase (Presentation, managed) |
| 20 | `Assets/Scripts/Party/Bridges/PartyUIRegistry.cs` | Static class |
| 21 | `Assets/Scripts/Party/Bridges/IPartyUIProvider.cs` | Interface |
| 22 | `Assets/Scripts/Party/Bridges/PartyUIState.cs` | UI data structs |
| 23 | `Assets/Scripts/Party/Bridges/PartyVisualQueue.cs` | Static NativeQueue bridge |
| 24 | `Assets/Scripts/Party/UI/PartyFrameView.cs` | MonoBehaviour (party frames) |
| 25 | `Assets/Scripts/Party/UI/PartyInviteDialogView.cs` | MonoBehaviour (invite popup) |
| 26 | `Assets/Scripts/Party/UI/LootRollView.cs` | MonoBehaviour (NeedGreed voting) |
| 27 | `Assets/Scripts/Party/UI/LootModeSelector.cs` | MonoBehaviour (loot mode dropdown) |
| 28 | `Assets/Scripts/Persistence/Modules/PartySaveModule.cs` | ISaveModule (TypeId=12) |
| 29 | `Assets/Scripts/Party/DIG.Party.asmdef` | Assembly definition |
| 30 | `Assets/Editor/PartyWorkstation/PartyWorkstationWindow.cs` | EditorWindow |
| 31 | `Assets/Editor/PartyWorkstation/IPartyWorkstationModule.cs` | Interface |
| 32 | `Assets/Editor/PartyWorkstation/Modules/PartyInspectorModule.cs` | Module |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Progression/Systems/XPAwardSystem.cs` | Add `PartyXPModifier` lookup, multiply finalXP, remove ephemeral components (~15 lines) |
| 2 | `Assets/Scripts/Loot/Systems/DeathLootSystem.cs` | Add `LootDesignation` check, apply pickup restriction (~20 lines) |
| 3 | `Assets/Scripts/Interaction/Components/InteractableComponents.cs` | Add `Trade = 15` to `InteractionVerb` enum (+1 line) |
| 4 | `Assets/Scripts/Persistence/Core/SaveModuleTypeIds.cs` | Add `Party = 12` constant (+1 line) |
| 5 | Player prefab (Warrok_Server) | Add `PartyAuthoring` component |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/PartyConfig.asset` |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `XPAwardSystem` | 16.14 | MODIFIED: reads `PartyXPModifier` for party XP sharing |
| `DeathLootSystem` | 16.6 | MODIFIED: reads `LootDesignation` for party loot modes |
| `DeathTransitionSystem` | Core | Creates `KillCredited` -- `PartyKillCreditSystem` distributes to party |
| `KillQuestEventEmitterSystem` | 16.12 | Distributed `KillCredited` triggers quest kill objectives for party members |
| `CurrencyInventory` | 16.6 | Gold splitting via `CurrencyTransaction` buffer |
| `InteractionVerb` | 16.1 | Trade verb added for NeedGreed item exchange |
| `SaveModuleTypeIds` | 16.15 | TypeId=12 reserved for `PartySaveModule` |
| `PartySaveModule` | 16.15 | Persists loot mode preference + last party members for reconnect |
| `TalentLink` | 17.1 | Party members can inspect each other's talent trees (future) |
| `CharacterAttributes.Level` | 16.14 | Read by `PartyUIBridgeSystem` for party frame level display |
| `Health` / `ResourcePool` | Core / 16.8 | Read by `PartyUIBridgeSystem` for party frame health/mana bars |
| `AssistCredited` | Core | Future: party assist XP for members who damaged but didn't kill |

---

## Verification Checklist

### Party Formation
- [ ] Player A invites Player B: `PartyInvite` transient entity created with correct `ExpirationTick`
- [ ] Player B accepts: party entity created with both members, `PartyLink` set on both
- [ ] Player B declines: invite destroyed, no party formed
- [ ] Invite expires after timeout: invite destroyed, `PartyNotifyRpc(InviteExpired)` sent
- [ ] Player A (in party) invites Player C: C joins existing party (no new party entity)
- [ ] Invite to player already in a party: rejected
- [ ] Invite to self: rejected
- [ ] Duplicate invite to same player: rejected
- [ ] Party at max size (6): invite rejected

### Party Management
- [ ] Leader kicks member: member removed, `PartyLink` cleared, notify sent
- [ ] Non-leader kicks: rejected
- [ ] Member leaves voluntarily: removed from party, notify sent
- [ ] Leader leaves: next longest member auto-promoted, notify sent
- [ ] Last two members -- one leaves: party disbanded, both `PartyLink` cleared
- [ ] Leader promotes member: `PartyState.LeaderEntity` updated, notify sent
- [ ] Non-leader promotes: rejected
- [ ] Leader changes loot mode: `PartyState.LootMode` updated, notify sent

### XP Sharing
- [ ] 2-man party, both in range: each gets 55% XP (at 10% bonus per member)
- [ ] 3-man party, all in range: each gets 40% XP
- [ ] 6-man party, all in range: each gets 25% XP
- [ ] Party member out of XP range: excluded from sharing, killer gets more
- [ ] Solo player (no party): 100% XP, no `PartyXPModifier` applied
- [ ] Party member with rested XP: rested bonus applies AFTER party split
- [ ] Party member with XP gear bonus: gear bonus applies AFTER party split
- [ ] Max level member in party: excluded from XP split (other members get proportionally more)

### Kill Credit Distribution
- [ ] Killer in party: `KillCredited` distributed to in-range members
- [ ] Distributed `KillCredited` has `PartyKillTag`: prevents re-distribution
- [ ] Quest kill objectives credited to party members in range
- [ ] Member out of `KillCreditRange`: no kill credit
- [ ] Solo kill (no party): `KillCredited` only on killer (unchanged behavior)

### Loot Distribution
- [ ] FreeForAll mode: loot spawns normally, anyone can pick up
- [ ] RoundRobin mode: loot designated to next member in rotation
- [ ] RoundRobin: designated member can pick up; others cannot (until timeout)
- [ ] RoundRobin: timeout reverts to FFA
- [ ] NeedGreed mode: loot held, vote UI shown to all members
- [ ] NeedGreed: Need wins over Greed wins over Pass
- [ ] NeedGreed: tie broken deterministically
- [ ] NeedGreed: timeout auto-Passes for non-voters
- [ ] NeedGreed: winner gets loot designation
- [ ] MasterLoot mode: loot designated to leader
- [ ] Gold split: currency divided equally among in-range members
- [ ] Member out of loot range: excluded from loot designation

### Multiplayer & Networking
- [ ] All party operations server-authoritative (RPCs validated on server)
- [ ] `PartyState` Ghost:All: all clients see party metadata
- [ ] `PartyMemberElement` Ghost:All: all clients see party composition
- [ ] `PartyLink` Ghost:AllPredicted: owning client sees party reference
- [ ] Invalid RPCs silently destroyed (no crash, no error to client)
- [ ] Disconnect: member removed from party, party notified
- [ ] Leader disconnect: auto-promote next member
- [ ] Reconnect: server can re-add player to existing party
- [ ] 6 concurrent parties on server: no performance degradation

### UI
- [ ] Party frames show for each member (health, mana, level, name)
- [ ] Leader crown icon displayed on leader's frame
- [ ] Out-of-range members dimmed
- [ ] Dead members grayed out
- [ ] Invite dialog: Accept/Decline buttons, countdown timer
- [ ] NeedGreed voting UI: item icon, Need/Greed/Pass buttons, timer
- [ ] Loot roll result: winner name + vote type displayed
- [ ] Loot mode selector (leader only): dropdown with 4 modes

### Persistence
- [ ] Save with party: `PartySaveModule` writes loot mode preference
- [ ] Load without party: `PartyLink` defaults to `Entity.Null`, no error
- [ ] Old save without TypeId=12: LoadSystem skips module, no error
- [ ] Preferred loot mode restored when forming new party

### Backward Compatibility
- [ ] Entity without `PartyLink`: zero overhead, all systems skip
- [ ] `PartyLink.PartyEntity == Entity.Null`: treated as solo, existing behavior
- [ ] No `PartyConfigSO` in Resources: bootstrap logs error, systems dormant
- [ ] No `IPartyUIProvider` registered: systems run, no UI crash
- [ ] Existing combat, loot, progression unchanged for solo players
- [ ] Archetype: only +8 bytes on player entity, no ghost bake errors
