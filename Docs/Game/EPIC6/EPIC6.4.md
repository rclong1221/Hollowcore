# EPIC 6.4: Party Choice Flow

**Status**: Planning
**Epic**: EPIC 6 — Gate Selection & Navigation
**Dependencies**: 6.1 (ForwardGateOption, GateSelectionState); 6.2 (BacktrackGateInfo); Framework: Party/ (PartyState, NetworkId); NetCode (ghost replication, RPCs)

---

## Overview

In co-op, the gate selection becomes a group decision. Each player votes on their preferred gate (forward or backtrack). Majority wins. Ties resolve to the host. A countdown timer auto-selects the host's choice on timeout, preventing indefinite stalling. Solo play bypasses the vote entirely — selection is instant. The vote UI shows player portraits next to their chosen gate with a visible countdown.

---

## Component Definitions

```csharp
// File: Assets/Scripts/Gate/Components/PartyVoteComponents.cs
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace Hollowcore.Gate
{
    /// <summary>
    /// Whether the voted gate is forward or backtrack.
    /// </summary>
    public enum GateDirection : byte
    {
        None = 0,
        Forward = 1,
        Backtrack = 2
    }

    /// <summary>
    /// A single player's gate vote. One per connected player.
    /// Replicated to all clients for UI display.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct GateVote : IComponentData
    {
        [GhostField] public int VoterNetworkId;
        [GhostField] public GateDirection Direction;   // Forward or Backtrack
        [GhostField] public int VotedGateIndex;        // ForwardGateOption.GateIndex or BacktrackGateInfo.DistrictId
        [GhostField] public bool HasVoted;             // False until player commits
        [GhostField] public bool IsHost;               // Used for tiebreak
    }

    /// <summary>
    /// Singleton tracking the overall vote state. Server-authoritative.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct VoteState : IComponentData
    {
        public float TimerSecondsRemaining;       // Counts down from VoteTimerDuration
        public float VoteTimerDuration;           // 30-60 seconds (configurable)
        public byte TotalVoters;                  // Number of connected players
        public byte VotesCast;                    // Number of players who have voted
        public bool VoteComplete;                 // True when resolved (majority or timeout)
        public GateDirection WinningDirection;
        public int WinningGateIndex;              // Resolved gate index or district ID
    }

    /// <summary>
    /// RPC: client sends vote to server.
    /// </summary>
    public struct GateVoteRpc : IRpcCommand
    {
        public GateDirection Direction;
        public int GateIndex;
    }

    /// <summary>
    /// RPC: server broadcasts vote result to all clients.
    /// </summary>
    public struct VoteResultRpc : IRpcCommand
    {
        public GateDirection WinningDirection;
        public int WinningGateIndex;
    }

    /// <summary>
    /// Configuration for vote timer and rules. Singleton.
    /// </summary>
    public struct VoteConfig : IComponentData
    {
        public float DefaultTimerSeconds;         // 45 seconds default
        public float MinTimerSeconds;             // 30 seconds minimum
        public float MaxTimerSeconds;             // 60 seconds maximum
        public bool HostBreaksTie;                // True = host wins ties; False = random
    }
}
```

---

## Systems

### PartyVoteSystem

```csharp
// File: Assets/Scripts/Gate/Systems/PartyVoteSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: BacktrackGateSystem
//
// Solo mode fast path:
//   1. If TotalVoters == 1 and a GateVoteRpc is received:
//      - Immediately set VoteState.VoteComplete = true
//      - Set WinningDirection and WinningGateIndex from the vote
//      - Send VoteResultRpc to client
//      - Return (no timer, no tally)
//
// Co-op vote loop (each frame while GateSelectionState.IsActive && !VoteComplete):
//   1. Process incoming GateVoteRpc entities:
//      a. Find GateVote entity matching sender's NetworkId
//      b. Validate voted gate exists (forward index in range, or backtrack district exists)
//      c. Update GateVote: Direction, VotedGateIndex, HasVoted = true
//      d. Increment VoteState.VotesCast
//      e. Players may change their vote before timer expires (update, don't double-count)
//   2. Check for early resolution:
//      - If VotesCast == TotalVoters → tally immediately
//   3. Tick timer:
//      - TimerSecondsRemaining -= deltaTime
//      - If TimerSecondsRemaining <= 0 → force resolve
//   4. Tally votes when resolving:
//      a. Count votes per unique (Direction, GateIndex) pair
//      b. Majority (> 50%) wins
//      c. Tie resolution:
//         - If HostBreaksTie: use host player's vote
//         - Else: random selection among tied options (seeded)
//      d. Set VoteState: WinningDirection, WinningGateIndex, VoteComplete = true
//   5. Send VoteResultRpc to all clients
//   6. Destroy all GateVoteRpc entities
```

### GateVoteInitSystem

```csharp
// File: Assets/Scripts/Gate/Systems/GateVoteInitSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateBefore: PartyVoteSystem
//
// When GateSelectionState.IsActive transitions to true:
//   1. Count connected players from PartyState / NetworkStreamConnection
//   2. Create one GateVote entity per player:
//      - VoterNetworkId = player's NetworkId
//      - HasVoted = false
//      - IsHost = true for host player
//   3. Create VoteState singleton:
//      - TimerSecondsRemaining = VoteConfig.DefaultTimerSeconds
//      - TotalVoters = connected player count
//      - VotesCast = 0
//      - VoteComplete = false
```

### GateVoteCleanupSystem

```csharp
// File: Assets/Scripts/Gate/Systems/GateVoteCleanupSystem.cs
// WorldSystemFilter: ServerSimulation | LocalSimulation
// UpdateInGroup: SimulationSystemGroup
// UpdateAfter: GateTransitionSystem (EPIC 6.5)
//
// When GateSelectionState.IsActive transitions to false:
//   1. Destroy all GateVote entities
//   2. Destroy VoteState singleton
```

---

## Vote UI Layout

```
┌────────────────────────────────────────────────────┐
│                  CHOOSE YOUR PATH                   │
│                  Timer: 00:32                        │
│                                                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐          │
│  │ FORWARD  │  │ FORWARD  │  │ FORWARD  │          │
│  │ The Burn │  │ Ashvein  │  │ Hollows  │          │
│  │          │  │          │  │          │          │
│  │ [P1][P3] │  │   [P2]   │  │          │          │
│  └──────────┘  └──────────┘  └──────────┘          │
│                                                      │
│  ┌──────────────────────────────────────┐           │
│  │ BACKTRACK                             │           │
│  │ Rustyard (Phase 3) — 2 echoes        │ [P4]     │
│  └──────────────────────────────────────┘           │
│                                                      │
│  [P1]=Host  [P2]=You  [P3]=Ally  [P4]=Ally          │
│                                                      │
│  Majority wins • Tie → Host decides                  │
└────────────────────────────────────────────────────┘
```

---

## Setup Guide

1. Add `PartyVoteComponents.cs` to `Assets/Scripts/Gate/Components/`
2. Add `PartyVoteSystem.cs`, `GateVoteInitSystem.cs`, `GateVoteCleanupSystem.cs` to `Assets/Scripts/Gate/Systems/`
3. Create `VoteConfig` singleton entity via authoring on the Gate subscene with default timer of 45 seconds
4. RPC registration: ensure `GateVoteRpc` and `VoteResultRpc` are registered in the NetCode RPC collection
5. Client-side: Gate UI sends `GateVoteRpc` when player clicks a gate card; allow re-voting (new RPC overwrites previous)
6. Vote UI prefab: player portrait icons that snap to the selected gate card; countdown timer display
7. Solo detection: check `PartyState` or connection count; if 1, skip timer and vote UI entirely
8. Host identification: use `NetworkStreamConnection` with `IsHandshakeComplete` and compare to server's own `NetworkId`
9. Tiebreak randomness: use `RunSeedUtility.Hash(expeditionSeed, rerollOffset, "tiebreak")` for deterministic tie resolution

---

## Verification

- [ ] Solo play: gate selection is instant with no vote UI or timer
- [ ] Co-op: GateVote entities created for each connected player
- [ ] Clicking a gate sends GateVoteRpc and updates local GateVote
- [ ] Player portraits appear next to the voted gate card on all clients
- [ ] Changing vote before timer expires updates the portrait position
- [ ] Timer counts down from configured duration
- [ ] All players voted early: vote resolves immediately without waiting for timer
- [ ] Timer expiration: auto-resolves with current tallied votes
- [ ] Majority vote wins (3 of 4 players on same gate)
- [ ] Tie: host's vote wins when HostBreaksTie is true
- [ ] VoteResultRpc received by all clients with correct winning gate
- [ ] VoteState.VoteComplete set to true after resolution
- [ ] Vote entities cleaned up when gate screen closes
- [ ] Player disconnect mid-vote: TotalVoters decrements, vote recalculated
- [ ] Backtrack gates are valid vote targets alongside forward gates

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Gate/Debug/VoteDebugOverlay.cs
// Managed SystemBase, ClientSimulation | LocalSimulation, PresentationSystemGroup
//
// Party Vote State Overlay — toggled via debug console: `gate.vote.debug`
//
// Displays:
//   1. VoteState singleton:
//      - TimerSecondsRemaining (countdown bar)
//      - TotalVoters / VotesCast
//      - VoteComplete flag
//      - WinningDirection + WinningGateIndex (when resolved)
//   2. Per-player GateVote table:
//      - VoterNetworkId | Direction | VotedGateIndex | HasVoted | IsHost
//      - Color: green=voted, grey=pending, gold=host
//   3. Vote tally:
//      - Per-gate vote count bar chart
//      - Majority threshold line
//      - Tie indicator
//   4. VoteConfig: DefaultTimerSeconds, HostBreaksTie
//
// Useful for debugging co-op vote synchronization and tiebreak logic.
```

---

## Simulation & Testing

```csharp
// File: Assets/Editor/GateWorkstation/VoteSimulation.cs
using UnityEditor;

namespace Hollowcore.Gate.Editor
{
    /// <summary>
    /// Vote resolution simulation: validates tiebreak and timeout logic.
    /// Menu: Hollowcore > Simulation > Party Vote
    /// </summary>
    public static class VoteSimulation
    {
        [MenuItem("Hollowcore/Simulation/Party Vote Resolution")]
        public static void RunVoteTests()
        {
            // Test cases:
            // 1. Solo: 1 voter, instant resolution, no timer
            // 2. Unanimous: 4 voters, all same gate, immediate resolve
            // 3. Majority: 3 of 4 voters on same gate, 1 different
            // 4. Tie (2v2): HostBreaksTie=true → host's choice wins
            // 5. Tie (2v2): HostBreaksTie=false → seeded random
            // 6. Timeout with partial votes: 2 of 4 voted → tally existing
            // 7. Timeout with no votes: → host's default (first forward gate)
            // 8. Vote change: voter switches, tally updated correctly
            // 9. Player disconnect mid-vote: TotalVoters decrements
            // 10. Backtrack + forward mix: verify both directions are valid
            //
            // Determinism: same seed + same votes → same tiebreak result
        }
    }
}
```
