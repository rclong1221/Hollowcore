# EPIC 6.4 Setup Guide: Party Vote Flow

**Status:** Planned
**Requires:** EPIC 6.1 (ForwardGateOption, GateSelectionState), EPIC 6.2 (BacktrackGateInfo), Framework: Party/ (PartyState), NetCode (RPCs)

---

## Overview

In co-op, the gate selection becomes a group decision. Each player votes on a gate (forward or backtrack), majority wins, ties go to the host, and a countdown timer prevents indefinite stalling. Solo play bypasses the vote entirely. This guide covers setting up the VoteConfig singleton, the vote UI prefab with player portraits, RPC registration, and solo detection.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Gate Subscene | GateSelectionState singleton (EPIC 6.1) | Triggers vote initialization |
| Party system | PartyState / NetworkStreamConnection | Player count, host identification |
| NetCode RPC collection | RPC registration | GateVoteRpc and VoteResultRpc must be registered |

### New Setup Required
1. Create VoteConfig singleton via authoring in Gate subscene
2. Build the Vote UI overlay prefab
3. Register RPCs in the NetCode collection
4. Set up player portrait assets

---

## 1. VoteConfig Singleton

**Create:** Add a `VoteConfigAuthoring` component to the `GateConfig` GameObject in the Gate subscene.

### 1.1 Inspector Fields

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **DefaultTimerSeconds** | Countdown duration for the vote | 45 | 30-60 |
| **MinTimerSeconds** | Minimum allowed timer (for live tuning floor) | 30 | 15-45 |
| **MaxTimerSeconds** | Maximum allowed timer (for live tuning ceiling) | 60 | 45-120 |
| **HostBreaksTie** | If true, host's vote wins ties; if false, seeded random | true | true/false |

**Tuning tip:** 45 seconds is a good default. Shorter timers (30s) increase urgency but may frustrate deliberate players. For playtesting, set `HostBreaksTie = true` to ensure deterministic outcomes.

---

## 2. Vote UI Prefab

**Create:** `Assets/Prefabs/UI/Gate/VoteOverlay.prefab`
**Parent:** Child of the `GateScreen` prefab, overlaid on top of gate cards.

### 2.1 Prefab Structure

```
VoteOverlay (RectTransform, hidden in solo)
  +-- TimerBar
  |     +-- TimerBackground (Image)
  |     +-- TimerFill (Image, filled)
  |     +-- TimerText (TextMeshProUGUI, "00:32")
  +-- VotePromptText ("Select a gate to vote")
  +-- PlayerPortraitContainer
  |     +-- PlayerPortrait_0 (prefab instance)
  |     |     +-- PortraitImage (Image)
  |     |     +-- NameText (TextMeshProUGUI)
  |     |     +-- HostBadge (Image, hidden if not host)
  |     +-- PlayerPortrait_1
  |     +-- PlayerPortrait_2
  |     +-- PlayerPortrait_3
  +-- TallyBar (per gate, child of each GateCard)
  |     +-- VoteCountText ("2 votes")
  +-- ResultBanner (hidden until resolved)
        +-- ResultText ("MAJORITY: The Burn selected!")
```

### 2.2 Portrait Placement

Player portraits snap to the gate card they voted for. The vote UI adapter should:

1. Read all `GateVote` entities each frame
2. For each `GateVote` where `HasVoted == true`:
   - Find the matching gate card by `Direction + VotedGateIndex`
   - Parent the portrait to that card's portrait anchor point
   - Show the portrait with player name and optional host crown icon
3. For uncast votes: show portrait in a "pending" area with a grey tint

### 2.3 Timer Display

| Timer Range | Color | Behavior |
|-------------|-------|----------|
| > 15s remaining | White | Normal countdown |
| 5-15s remaining | Yellow | Pulse animation |
| < 5s remaining | Red | Fast pulse + audio warning tick |

---

## 3. RPC Registration

Both vote RPCs must be registered in the NetCode RPC collection. Depending on your project's RPC registration pattern:

### 3.1 GateVoteRpc

**File:** `Assets/Scripts/Gate/Components/PartyVoteComponents.cs`

```csharp
public struct GateVoteRpc : IRpcCommand
{
    public GateDirection Direction;  // Forward or Backtrack
    public int GateIndex;            // ForwardGateOption.GateIndex or BacktrackGateInfo.DistrictId
}
```

**Client sends** when the player clicks a gate card. Players can re-vote (new RPC overwrites previous).

### 3.2 VoteResultRpc

```csharp
public struct VoteResultRpc : IRpcCommand
{
    public GateDirection WinningDirection;
    public int WinningGateIndex;
}
```

**Server broadcasts** to all clients when the vote resolves.

### 3.3 Registration

Add both RPC types to your project's `RpcCollection` registration system. If using auto-generated RPC registration, ensure the `Hollowcore.Gate` assembly is included in the scan.

---

## 4. Solo Mode Detection

Solo play must bypass the vote entirely. The `PartyVoteSystem` checks:

```
If TotalVoters == 1 and GateVoteRpc received:
    -> Immediately resolve (no timer, no tally)
    -> Set VoteState.VoteComplete = true
```

The Vote UI adapter should:
1. On gate screen open, check connected player count from `PartyState` or `NetworkStreamConnection`
2. If count == 1: hide `VoteOverlay` entirely
3. Gate card click directly sends `GateVoteRpc` and transitions immediately

---

## 5. Host Identification

The system needs to know which player is the host for tiebreak resolution:

| Method | Component | Check |
|--------|-----------|-------|
| Listen server | NetworkStreamConnection | Server's own NetworkId |
| Dedicated server | PartyState | Designated host NetworkId |

The `GateVoteInitSystem` marks `GateVote.IsHost = true` for the host player when creating vote entities.

---

## 6. Tiebreak Resolution

When votes are tied (e.g., 2v2 in a 4-player game):

| Setting | Resolution |
|---------|------------|
| `HostBreaksTie = true` | Use the host player's vote as the winner |
| `HostBreaksTie = false` | Seeded random: `RunSeedUtility.Hash(expeditionSeed, rerollOffset, "tiebreak")` |

**Tuning tip:** `HostBreaksTie = true` is recommended for most game modes. Seeded random tiebreak is better for competitive/ranked co-op where host advantage should be minimized.

---

## 7. Scene & Subscene Checklist

- [ ] `VoteConfigAuthoring` on Gate subscene `GateConfig` GameObject
- [ ] `VoteOverlay.prefab` exists at `Assets/Prefabs/UI/Gate/VoteOverlay.prefab`
- [ ] Vote overlay is a child of the `GateScreen` prefab
- [ ] Player portrait prefab exists with PortraitImage + NameText + HostBadge
- [ ] `GateVoteRpc` and `VoteResultRpc` registered in NetCode RPC collection
- [ ] `PartyVoteSystem.cs`, `GateVoteInitSystem.cs`, `GateVoteCleanupSystem.cs` in `Assets/Scripts/Gate/Systems/`
- [ ] Solo detection logic hides vote overlay when player count == 1

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| RPCs not registered | Votes never reach server; vote never resolves | Add both RPC types to NetCode RPC collection |
| Solo mode not detected | Solo player sees timer and waits 45s for nothing | Check player count and skip vote UI + timer |
| Vote change double-counted | VotesCast exceeds TotalVoters | PartyVoteSystem must check `HasVoted` before incrementing `VotesCast` on re-vote |
| Timer not paused after early resolution | Timer continues ticking after all players voted | Check `VotesCast == TotalVoters` before timer tick |
| Host player not identified | Tiebreak falls through to random even with `HostBreaksTie = true` | Verify `GateVoteInitSystem` correctly sets `IsHost` flag from `NetworkStreamConnection` |
| Player disconnect not handled | Phantom vote persists, TotalVoters wrong | Detect disconnect events and decrement TotalVoters, remove stale GateVote entity |
| Backtrack gates not valid vote targets | Players can only vote for forward gates | Ensure `PartyVoteSystem` accepts `GateDirection.Backtrack` and validates against `BacktrackGateInfo` entities |
| Vote entities leak after gate screen closes | Orphaned GateVote entities persist in ECS | `GateVoteCleanupSystem` must destroy all GateVote + VoteState when `GateSelectionState.IsActive` goes false |

---

## Verification

- [ ] Solo play: gate selection is instant, no vote UI or timer shown
- [ ] Co-op: GateVote entities created for each connected player on gate screen open
- [ ] Clicking a forward gate sends GateVoteRpc and shows portrait on that card
- [ ] Clicking a backtrack gate sends GateVoteRpc with Direction=Backtrack
- [ ] Changing vote moves portrait to new card (no double-count)
- [ ] Timer counts down from DefaultTimerSeconds
- [ ] All players voted early: vote resolves immediately
- [ ] Timer expiry: auto-resolves with current tallied votes
- [ ] Majority (3 of 4) wins correctly
- [ ] Tie with `HostBreaksTie=true`: host's choice wins
- [ ] VoteResultRpc received by all clients with correct winning gate
- [ ] Player disconnect mid-vote: TotalVoters decrements
- [ ] Vote entities cleaned up when gate screen closes
- [ ] Run `Hollowcore > Simulation > Party Vote Resolution` with all 10 test cases passing
