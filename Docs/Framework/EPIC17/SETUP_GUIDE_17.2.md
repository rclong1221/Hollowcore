# SETUP GUIDE 17.2: Party & Group System

**Status:** Implemented
**Last Updated:** February 23, 2026
**Requires:** Progression System (EPIC 16.14), Loot & Economy (EPIC 16.6), Quest System (EPIC 16.12), Save/Load (EPIC 16.15)

This guide covers Unity Editor setup for the party and group system. After setup, players can form 2-6 member parties with invite/accept/kick/leave/promote operations, share XP and kill credit, use configurable loot distribution modes, and see party member status frames.

---

## What Changed

Previously, all combat rewards were solo-only. The killing blow determined who received XP, loot was free-for-all with no designation, quest kill objectives only credited the killer, and there was no way to see allied player status.

Now:

- **Party formation** -- server-authoritative 2-6 player parties via invite/accept RPCs
- **Party roles** -- leader can kick, promote, and change loot mode; other members can leave
- **XP sharing** -- party members within range share kill XP with a configurable group bonus
- **Kill credit distribution** -- party members within range receive quest kill credit
- **Loot modes** -- FreeForAll, RoundRobin, NeedGreed, and MasterLoot
- **Loot designation** -- designated loot has exclusive pickup timer before reverting to FFA
- **NeedGreed voting** -- vote UI with Need/Greed/Pass, auto-Pass on timeout, deterministic tie-breaking
- **Gold splitting** -- gold drops split equally among in-range members
- **Party UI bridge** -- provider interface for party frames, invite dialogs, loot roll UI, loot mode selector
- **Proximity tracking** -- per-member XP range, loot range, and kill credit range flags
- **Party Workstation** -- editor window with live party state inspector
- **Save/load** -- preferred loot mode persisted across sessions (parties themselves are session-scoped)

---

## What's Automatic (No Setup Required)

| Feature | How It Works |
|---------|-------------|
| Party entity lifecycle | PartyFormationSystem creates/destroys party entities when players accept invites or when party size drops below 2 |
| RPC validation | PartyRpcReceiveSystem validates all operations server-side (leader-only operations, party membership, target validity) |
| Proximity tracking | PartyProximitySystem updates InXPRange/InLootRange/InKillCreditRange flags every frame per party |
| Kill credit sharing | PartyKillCreditSystem distributes KillCredited to all in-range party members when any member gets a kill |
| XP sharing modifier | PartyXPSharingSystem writes PartyXPModifier to each member; XPAwardSystem applies it automatically |
| Quest kill credit | Distributed KillCredited triggers quest kill objectives for the entire party (via existing KillQuestEventEmitterSystem) |
| Loot designation | PartyLootSystem applies loot mode rules before DeathLootSystem runs |
| NeedGreed resolution | PartyNeedGreedResolveSystem resolves votes on completion or timeout, sets LootDesignation on winner |
| Invite expiration | PartyInviteTimeoutSystem destroys expired invites and notifies clients |
| Disconnect handling | PartyCleanupSystem removes disconnected members, auto-promotes new leader, disbands empty parties |
| Stale transient cleanup | PartyCleanupSystem removes orphaned PartyXPModifier, PartyKillTag, expired LootDesignation |
| Ghost replication | PartyLink (on player), PartyState, and PartyMemberElement are ghost-replicated to all clients |
| Visual queue | Party events (join, leave, loot roll results) are enqueued to PartyVisualQueue for UI consumption |

---

## 1. ScriptableObject Configuration

The party system requires **one** ScriptableObject asset placed in `Assets/Resources/`. An optional set of loot mode assets provides UI display data.

### 1.1 Party Config

Controls all party behavior: size limits, invite timeouts, XP sharing, loot distribution, and kill credit ranges.

1. Right-click in Project window > **Create > DIG > Party > Party Config**
2. Name it exactly `PartyConfig`
3. Place at `Assets/Resources/PartyConfig.asset`

#### Party Size

| Field | Default | Description |
|-------|---------|-------------|
| **Max Party Size** | 6 | Maximum players per party (slider range: 2-6) |

#### Invites

| Field | Default | Description |
|-------|---------|-------------|
| **Invite Timeout Seconds** | 60 | Seconds before a pending invite automatically expires and is destroyed |

#### XP Sharing

| Field | Default | Description |
|-------|---------|-------------|
| **XP Share Range** | 50 | Distance (world units) within which members share XP from kills |
| **XP Share Bonus Per Member** | 0.10 | Per-member XP bonus multiplier (0.10 = +10% per extra member) |

The XP sharing formula for `N` members in range:

| Party Size | Per-Member XP | Total XP Generated |
|------------|---------------|-------------------|
| 1 (solo) | 100% | 100% |
| 2 | 55% | 110% |
| 3 | 40% | 120% |
| 4 | 32.5% | 130% |
| 5 | 28% | 140% |
| 6 | 25% | 150% |

> Tip: Increasing **XP Share Bonus Per Member** makes grouping more rewarding. At 0.20 (+20% per member), a full 6-player party generates 200% total XP. At 0.0, XP is split evenly with no bonus.

#### Loot

| Field | Default | Description |
|-------|---------|-------------|
| **Loot Range** | 60 | Distance within which members are eligible for loot distribution |
| **Loot Designation Timeout Seconds** | 30 | Seconds before designated loot (RoundRobin/MasterLoot/NeedGreed winner) reverts to FFA pickup |
| **NeedGreed Vote Timeout Seconds** | 15 | Seconds for NeedGreed vote before auto-Pass. All pending votes become Pass when the timer expires |
| **Loot Gold Split Percent** | 1.0 | Fraction of gold split equally among in-range members (1.0 = full split, 0.0 = no split) |

#### Kill Credit

| Field | Default | Description |
|-------|---------|-------------|
| **Kill Credit Range** | 50 | Distance for party kill credit distribution. Members within this range receive KillCredited for quest objectives |

#### Options

| Field | Default | Description |
|-------|---------|-------------|
| **Allow Loot Mode Vote** | false | If true, any member can request loot mode changes. If false, only the leader can change loot mode |
| **Default Loot Mode** | FreeForAll | Initial loot mode when a new party is formed |

---

### 1.2 Loot Mode Assets (Optional)

Per-mode descriptors for UI display. The system works without these -- they only provide display data for tooltips and mode selection UI.

1. Right-click > **Create > DIG > Party > Loot Mode**
2. Create one asset per mode you want UI descriptions for

| Field | Description |
|-------|-------------|
| **Mode Name** | Display name (e.g., "Round Robin") |
| **Mode Type** | Enum: FreeForAll, RoundRobin, NeedGreed, MasterLoot |
| **Description** | Tooltip text explaining mode behavior |
| **Icon Path** | Path to icon sprite for mode selector UI |
| **Required Min Members** | Minimum party size for this mode (default: 2) |
| **Allow Gold Split** | Whether gold splitting is active in this mode |
| **Allow Currency Loot** | Whether currency drops follow this mode's rules |

---

## 2. Player Prefab Setup

### 2.1 Add Party Authoring

1. Open the player prefab (e.g., `Warrok_Server`)
2. Select the **root** GameObject
3. Click **Add Component** > search for **Party Member** (listed under `DIG/Party/Party Member`)
4. No configuration fields -- the baker adds `PartyLink` (8 bytes) to the player entity

> This goes on the root player entity alongside PlayerAuthoring, ProgressionAuthoring, etc. All party state lives on separate party entities, NOT on the player. Only the 8-byte `PartyLink` pointing to `Entity.Null` is added to the player archetype.

### 2.2 Reimport SubScene

After modifying the player prefab:

1. Right-click the SubScene containing the player spawn point > **Reimport**
2. Wait for baking to complete

> Ghost prefab changes require SubScene reimport to regenerate ghost serialization variants. If you skip this step, you may see ghost bake errors at runtime.

---

## 3. Loot Distribution Modes

Designers should understand how each mode works to configure the party experience:

| Mode | Enum Value | Behavior |
|------|-----------|----------|
| **FreeForAll** | 0 | Default. Anyone can pick up any loot. No pickup restrictions |
| **RoundRobin** | 1 | Loot is assigned to party members in rotation. The designated member has exclusive pickup for `LootDesignationTimeoutSeconds`, then reverts to FFA |
| **NeedGreed** | 2 | A vote UI appears for all in-range members. Need (highest priority) beats Greed beats Pass. Ties are broken deterministically. Votes auto-Pass after `NeedGreedVoteTimeoutSeconds` |
| **MasterLoot** | 3 | All loot is designated to the party leader for manual distribution |

Gold from enemy drops is split equally among in-range members when `LootGoldSplitPercent > 0`. The split happens regardless of loot mode.

---

## 4. UI Setup

The party system provides four stub MonoBehaviour views as starting points. Designers and UI programmers extend them with actual visuals, animations, and layout.

### 4.1 Party Frames (PartyFrameView)

Shows party member status (health, mana, level, leader indicator, range dimming, alive state).

1. Create a UI Canvas or panel for party frames
2. Add the **PartyFrameView** component to a root GameObject
3. Assign Inspector references:

| Field | Description |
|-------|-------------|
| **Root Panel** | GameObject shown when in a party, hidden when solo |
| **Frame Container** | Parent Transform where member frame slots are instantiated |

4. `PartyFrameView` auto-registers with the UI bridge on enable, auto-unregisters on disable

The bridge system pushes state every frame with per-member data:

| Member Data | Description |
|-------------|-------------|
| Level | Player's current level |
| Health (current/max) | Health bar values |
| Mana (current/max) | Mana bar values |
| IsLeader | Show crown or leader indicator |
| IsInRange | Dim frames for out-of-range members |
| IsAlive | Gray out dead members |

### 4.2 Invite Dialog (PartyInviteDialogView)

Popup for accepting or declining party invites.

1. Create a popup dialog UI panel
2. Add the **PartyInviteDialogView** component
3. Assign Inspector references:

| Field | Description |
|-------|-------------|
| **Dialog Panel** | GameObject shown/hidden for invite popups |

4. Wire buttons in the Inspector:
   - Accept button OnClick > `OnAcceptClicked()`
   - Decline button OnClick > `OnDeclineClicked()`

The dialog receives invite data with inviter name, level, and time remaining.

### 4.3 NeedGreed Voting (LootRollView)

Vote UI for NeedGreed loot mode with Need/Greed/Pass buttons and a countdown timer.

1. Create a voting panel UI
2. Add the **LootRollView** component
3. Assign Inspector references:

| Field | Description |
|-------|-------------|
| **Roll Panel** | GameObject shown/hidden for loot roll voting |

4. Wire buttons in the Inspector:
   - Need button OnClick > `OnNeedClicked()`
   - Greed button OnClick > `OnGreedClicked()`
   - Pass button OnClick > `OnPassClicked()`

The roll UI receives loot roll data with item name, type ID, and time remaining.

### 4.4 Loot Mode Selector (LootModeSelector)

Dropdown or button set for the party leader to change loot mode.

1. Create a dropdown/button group UI
2. Add the **LootModeSelector** component
3. Assign Inspector references:

| Field | Description |
|-------|-------------|
| **Selector Panel** | GameObject auto-shown only when the local player is party leader |

4. Wire mode selection:
   - Dropdown or button OnValueChanged/OnClick > `OnLootModeSelected(int modeIndex)`
   - Mode indices: 0 = FreeForAll, 1 = RoundRobin, 2 = NeedGreed, 3 = MasterLoot

The selector panel auto-hides when the local player is not the leader.

---

## 5. Custom UI Integration

For custom UI implementations beyond the stub views, create a MonoBehaviour implementing `IPartyUIProvider` and register it with the UI bridge.

### 5.1 Provider Interface Callbacks

| Callback | When Called | Data Provided |
|----------|-----------|---------------|
| `UpdatePartyState` | Every frame while in a party | Full snapshot: member list, leader status, loot mode, member count |
| `OnMemberJoined` | New member joins the party | New member's entity, level, health |
| `OnMemberLeft` | Member leaves or is kicked | Departing member's entity |
| `OnInviteReceived` | Local player receives a party invite | Inviter entity, name, level, time remaining |
| `OnInviteExpired` | Pending invite expired without response | None |
| `OnLootRollStart` | NeedGreed vote begins | Loot entity, item name, time remaining |
| `OnLootRollResult` | NeedGreed vote resolved | Winner entity, winner name, item name, winning vote type |
| `OnPartyDisbanded` | Party destroyed | None |
| `OnLootModeChanged` | Leader changed the loot mode | New LootMode enum value |
| `OnLeaderChanged` | Leadership was transferred | New leader's entity |

### 5.2 Registering a Provider

Register your MonoBehaviour on enable, unregister on disable:

```
OnEnable:  PartyUIRegistry.RegisterPartyUI(this);
OnDisable: PartyUIRegistry.UnregisterPartyUI(this);
```

Stub views (`PartyFrameView`, `PartyInviteDialogView`, `LootRollView`, `LootModeSelector`) are provided as starting points in `Assets/Scripts/Party/UI/`.

---

## 6. Party Operations Reference

All party operations are sent as RPCs from client to server. The server validates every operation before processing.

| Operation | Who Can Send | TargetPlayer | Payload | What Happens |
|-----------|-------------|-------------|---------|--------------|
| **Invite** | Any player | Entity to invite | Unused | Creates a pending invite. If inviter has no party, one is created on accept |
| **AcceptInvite** | Invited player | Unused | Unused | Joins the inviter's party (or creates a new one) |
| **DeclineInvite** | Invited player | Unused | Unused | Destroys the invite, notifies inviter |
| **Leave** | Any member | Unused | Unused | Removes self from party. If leader leaves, next member is auto-promoted |
| **Kick** | Leader only | Member to kick | Unused | Removes target from party |
| **Promote** | Leader only | Member to promote | Unused | Transfers leadership to target |
| **SetLootMode** | Leader (or any, if AllowLootModeVote) | Unused | LootMode byte (0-3) | Changes the party's loot mode |
| **LootVote** | Any in-range member | Unused | Vote byte (1=Need, 2=Greed, 3=Pass) | Casts vote in active NeedGreed roll |

> All RPCs are validated server-side. Invalid operations (wrong role, target not in party, etc.) are silently ignored.

---

## 7. Kill Credit & XP Distribution Flow

When a party member kills an enemy:

1. **Kill detected** -- the killing player receives `KillCredited` (from DeathTransitionSystem, same as solo)
2. **Kill credit shared** -- `PartyKillCreditSystem` adds `KillCredited` to all party members within `KillCreditRange`
3. **Quest credit** -- shared `KillCredited` triggers quest kill objectives for the entire party
4. **XP modifier applied** -- `PartyXPSharingSystem` adds `PartyXPModifier` with the group-bonus multiplier
5. **XP awarded** -- `XPAwardSystem` processes each member's XP independently, applying the party modifier

> Note: The original killer receives XP on the same frame. Party members receive `KillCredited` via ECB and get their XP on the next frame. This 1-frame delay is imperceptible.

---

## 8. Editor Tooling

### 8.1 Party Workstation

**Menu:** DIG > Party Workstation

An editor window for inspecting live party state during Play Mode.

#### Party Inspector Tab (Play Mode only)

Displays all active party entities with:

- **Party entity ID** and version
- **Leader** entity index
- **Loot Mode** and **Round Robin Index**
- **Member Count** / Max Size
- **Creation Tick** (server tick when party was formed)
- **Member List** per party:
  - Entity index with `[LEADER]` tag on the leader
  - Health: Current / Max
  - Level
- **Proximity Flags** per member:
  - `XP` -- within XP Share Range
  - `Loot` -- within Loot Range
  - `Kill` -- within Kill Credit Range
  - `(out of range)` when no flags are active

> Outside Play Mode, the inspector shows "Enter Play Mode to inspect party data."

---

## 9. Save/Load Integration

The party system uses `PartySaveModule` (TypeId=12):

| What's Saved | Description |
|-------------|-------------|
| In Party (byte) | Whether the player was in a party at save time |
| Preferred Loot Mode (byte) | Player's preferred loot mode, applied when they next form or join a party |
| Last Member Count + PlayerSaveIds | For potential "rejoin last party" UI feature |

**Important:** Parties are **session-scoped** and are NOT recreated on load. When a player reconnects, `PartyLink` defaults to `Entity.Null` (solo). Only the preferred loot mode preference carries over.

**Backward compatibility:** Old save files without TypeId=12 are handled gracefully. The LoadSystem skips unknown type IDs with no errors, and `PartyLink` defaults to `Entity.Null`.

---

## 10. Backward Compatibility

| Scenario | Behavior |
|----------|----------|
| Player prefab **without** PartyAuthoring | No `PartyLink` component. All party systems skip this player entirely |
| Player **with** `PartyLink.PartyEntity == Entity.Null` | Treated as solo. Existing XP, loot, and quest behavior unchanged |
| No `PartyConfig.asset` in Resources | Bootstrap logs a warning and uses hard-coded defaults. All systems function normally |
| No `IPartyUIProvider` registered | Systems run, party logic works, no UI displayed, no errors |
| Solo kills (not in a party) | `XPAwardSystem` has no `PartyXPModifier`, awards full XP as before |
| Old save files (no TypeId=12) | LoadSystem skips unknown TypeId. `PartyLink` defaults to `Entity.Null` |

---

## 11. Example: Full Party Setup

### Step 1: Create Config

1. Create `PartyConfig.asset` at `Assets/Resources/PartyConfig`
   - Set Max Party Size = 4
   - Set XP Share Range = 50, XP Share Bonus Per Member = 0.10
   - Set Kill Credit Range = 50, Loot Range = 60
   - Set Default Loot Mode = FreeForAll

### Step 2: Player Prefab

1. Open `Warrok_Server` prefab
2. Add Component > **Party Member** (under DIG/Party)
3. Reimport SubScene

### Step 3: Party UI (Minimal)

1. Create a UI Canvas named "PartyUI"
2. Add a child panel named "PartyFrames"
3. Add **PartyFrameView** component to PartyFrames
4. Assign the panel as Root Panel and a child layout group as Frame Container
5. Add a child popup named "InviteDialog"
6. Add **PartyInviteDialogView** component
7. Assign the popup as Dialog Panel
8. Wire Accept and Decline button OnClick events

### Step 4: Verify

1. Enter Play Mode with 2+ clients (or listen server)
2. Open DIG > Party Workstation > Party Inspector
3. Send an Invite RPC from one client to another
4. Accept the invite on the target client
5. Verify: Party entity appears in workstation with both members listed
6. Kill an enemy -- both members should receive XP (check Progression Workstation)
7. Verify proximity flags update as members move apart

---

## 12. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Bootstrap | Enter Play Mode | Console: "[PartyBootstrap] Loaded PartyConfig" |
| 3 | Player entity | Entity Inspector | Player has PartyLink component with PartyEntity = Entity.Null |
| 4 | Invite flow | Send Invite RPC, accept on target | Party entity created with 2 members, both PartyLinks point to it |
| 5 | Party Workstation | Open DIG > Party Workstation in Play Mode | Shows active party with member list, health, level, leader, proximity |
| 6 | XP sharing (2 members) | Kill enemy with 2-member party | Each member receives ~55% XP (at default 10% bonus) |
| 7 | Kill credit | Member A kills enemy, member B in range | Member B receives KillCredited, quest kill objectives update |
| 8 | Loot mode change | Leader sends SetLootMode(1) | PartyState.LootMode changes to RoundRobin |
| 9 | RoundRobin | Kill with RoundRobin active | Loot spawns with LootDesignation to rotating member |
| 10 | NeedGreed voting | Kill with NeedGreed active | Vote UI appears, winner gets LootDesignation |
| 11 | Vote timeout | Let NeedGreed vote timer expire | Pending votes auto-Pass, winner resolved |
| 12 | Loot designation expiry | Wait for LootDesignationTimeoutSeconds | LootDesignation removed, loot becomes FFA |
| 13 | Leader kick | Leader sends Kick RPC | Target removed from party, PartyLink cleared |
| 14 | Leader promote | Leader sends Promote RPC | PartyState.LeaderEntity changes to target |
| 15 | Member leave | Non-leader sends Leave RPC | Member removed, party continues with remaining |
| 16 | Auto-disband | All but one member leave | Party entity destroyed, remaining member's PartyLink cleared |
| 17 | Leader disconnect | Leader client disconnects | Next member auto-promoted to leader |
| 18 | Invite expiration | Send invite, wait for timeout | Invite destroyed, InviteExpired notification sent |
| 19 | Ghost replication | Remote client joins party | PartyState and PartyMemberElement visible on remote client |
| 20 | Solo backward compat | Player without PartyAuthoring kills enemy | Full XP awarded, no party systems involved |
| 21 | Save/load | Save while in party, reload | PartyLink defaults to Entity.Null, preferred loot mode restored |

---

## 13. Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| "No PartyConfigSO found" warning at startup | Missing config asset | Create `PartyConfig.asset` at `Assets/Resources/PartyConfig` |
| Party invite does nothing | Target player has no `PartyLink` | Add **Party Member** component to target's player prefab, reimport SubScene |
| No party frames visible | No `IPartyUIProvider` registered | Add `PartyFrameView` to a UI Canvas in the scene |
| XP not splitting between members | Members out of `XPShareRange` | Increase XP Share Range in PartyConfig, or move players closer |
| Kill credit not sharing | Members out of `KillCreditRange` | Increase Kill Credit Range in PartyConfig |
| Loot not being designated | Loot mode is FreeForAll | Leader changes mode via SetLootMode (payload 1, 2, or 3) |
| NeedGreed vote never resolves | Vote timeout too long or too short | Adjust NeedGreed Vote Timeout Seconds in PartyConfig |
| Ghost bake error after adding PartyAuthoring | SubScene not reimported | Right-click SubScene > Reimport |
| Party persists after server restart | Expected behavior | Parties are session-scoped. Only loot mode preference is saved |
| Members not showing health in Workstation | Player entity missing Health | Verify player prefab has DamageableAuthoring |
| Proximity shows "(out of range)" for all | Players too far apart | Check XP/Loot/Kill Credit ranges vs actual player distances in scene |
| PartyXPModifier not cleaned up | Normal if XPAwardSystem hasn't run | PartyCleanupSystem removes stale modifiers at end of frame |

---

## 14. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Kill XP formula, leveling, stat scaling | SETUP_GUIDE_16.14 |
| Loot tables, item drops, currency | SETUP_GUIDE_16.6 |
| Quest kill objectives (shared via kill credit) | SETUP_GUIDE_16.12 |
| Corpse lifetime (extended for loot pickup window) | SETUP_GUIDE_16.3 |
| Save/load of party preferences | SETUP_GUIDE_16.15 |
| **Party & Group System** | **This guide (17.2)** |
