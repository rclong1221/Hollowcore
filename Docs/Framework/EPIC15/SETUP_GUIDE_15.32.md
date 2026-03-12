# SETUP GUIDE 15.32: Enemy Ability & Encounter Framework

**Status:** Implemented
**Last Updated:** February 13, 2026
**Requires:** EPIC 15.31 setup complete (see `SETUP_GUIDE_15.31.md`)

This guide covers Unity Editor setup for the data-driven ability system, telegraph/AOE zones, boss phase encounters, and the Encounter Designer editor window. After setup, enemies use configurable ability rotations instead of hardcoded melee attacks, and bosses support multi-phase encounters with triggers.

---

## What Changed from 15.31

In EPIC 15.31, every enemy had a single hardcoded melee attack defined by Inspector fields on AI Brain. Now:

- **Abilities are ScriptableObjects** — define once, reuse across any enemy type
- **Ability Profiles** group abilities into rotations with priority-based or utility-based selection
- **Telegraphs/AOE zones** show ground indicators before damage lands (player readability)
- **Status effects on abilities** — abilities can apply Burn, Stun, Poison, etc. on hit
- **Cooldown groups** — using one ability can lock out related abilities
- **Charge-based abilities** — abilities with limited charges that regenerate over time
- **Boss encounters** — multi-phase fights with HP thresholds, timed triggers, add spawns, and enrage timers
- **Encounter Designer** — editor window for visually authoring boss encounters

---

## What's Automatic (No Setup Required)

If you change nothing on an existing enemy prefab, everything works exactly as before. The AI Brain baker auto-generates a single fallback melee ability from the existing Inspector fields when no Ability Profile is present.

| Feature | How It Works |
|---------|-------------|
| Backward compatibility | AI Brain without Ability Profile = same behavior as 15.31 |
| Ability selection | AbilitySelectionSystem picks the first valid ability (respects cooldowns, range, phase) |
| Ability execution | AbilityExecutionSystem manages Telegraph > Casting > Active > Recovery lifecycle |
| Cooldown tracking | AbilityCooldownSystem ticks per-ability, global, and group cooldowns |
| Status effect passthrough | Ability modifiers flow through existing CombatResolutionSystem > StatusEffectSystem |
| Movement lock during casts | MovementOverride enableable component pauses chase during casts |
| Telegraph damage | TelegraphDamageSystem handles spatial queries and damage when zones expire |

---

## 1. Creating an Ability (ScriptableObject)

### 1.1 Create the Asset

1. In the **Project** window, right-click in your desired folder (e.g., `Assets/Data/AI/Abilities/`)
2. Select **Create > DIG > AI > Ability Definition**
3. Name it descriptively (e.g., `HeavySlam`, `Fireball`, `PoisonSpit`)

### 1.2 Inspector Fields

#### Identity

| Field | Description | Example |
|-------|-------------|---------|
| **Ability Name** | Display name (used in editor tools and logging) | "Heavy Slam" |
| **Ability Id** | Unique numeric ID (used by triggers to force abilities) | 101 |
| **Description** | Designer notes (not used at runtime) | "AOE slam with stun chance" |
| **Icon** | Sprite for the Encounter Designer window (optional) | — |

#### Targeting

| Field | Description | Default |
|-------|-------------|---------|
| **Targeting Mode** | How targets are selected (see table below) | CurrentTarget |
| **Range** | Max engagement distance in meters | 2.5 |
| **Radius** | AOE radius (0 = single target) | 0 |
| **Angle** | Cone angle in degrees (360 = full circle) | 360 |
| **Max Targets** | Maximum entities hit per use | 1 |
| **Requires Line Of Sight** | Must see target to cast | true |

**Targeting Modes:**

| Mode | Behavior |
|------|----------|
| Self | Buff/heal on self |
| CurrentTarget | Whoever the aggro system selected |
| HighestThreat | Highest threat player (may differ from current target) |
| LowestHP | Weakest player in range |
| RandomPlayer | Random valid target |
| AllInRange | Every entity within Range |
| GroundAtTarget | AOE centered on target's position |
| GroundAtSelf | AOE centered on self |
| Cone | Cone in facing direction |
| Line | Line from self toward target |
| Ring | Donut around self |

#### Timing

| Field | Description | Default |
|-------|-------------|---------|
| **Cast Time** | Wind-up before hit (interruptible window) | 0.4s |
| **Active Duration** | Hit window / channel duration | 0.15s |
| **Recovery Time** | Post-attack lockout | 0.5s |
| **Cooldown** | Per-ability cooldown | 1.5s |
| **Global Cooldown** | Shared cooldown across ALL abilities | 0.5s |
| **Telegraph Duration** | Warning time before cast begins (0 = no telegraph) | 0s |
| **Tick Interval** | For channeled/DoT zones (0 = single hit) | 0s |

**Ability Lifecycle:**

```
|-- Telegraph --|-- Cast Time --|-- Active --|-- Recovery --|-- Cooldown --|
   Ground decal    Wind-up          Damage      Post-attack    Waiting
   visible         animation        applied     lockout
```

> If Telegraph Duration > 0, a ground indicator appears BEFORE the Cast Time begins.

#### Charges

| Field | Description | Default |
|-------|-------------|---------|
| **Max Charges** | 0 = normal cooldown. >0 = charge-based | 0 |
| **Charge Regen Time** | Seconds to regenerate one charge | 0 |

Charge-based abilities can be used multiple times in succession, then must wait for charges to regenerate. Example: a dash with 2 charges and 5s regen time.

#### Cooldown Group

| Field | Description | Default |
|-------|-------------|---------|
| **Cooldown Group Id** | 0 = no group. 1-255 = shared group | 0 |
| **Cooldown Group Duration** | Lockout applied to ALL abilities sharing this group | 0 |

When an ability with Group ID 1 fires, ALL other abilities with Group ID 1 receive the group cooldown. Use this to prevent enemies from spamming similar abilities (e.g., group all melee attacks so "Jab" and "AutoAttack" share a 1s lockout).

#### Damage

| Field | Description | Default |
|-------|-------------|---------|
| **Damage Base** | Base damage per hit | 15 |
| **Damage Variance** | Random variance (+/-) | 5 |
| **Damage Type** | Element (Physical, Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane) | Physical |
| **Hit Count** | Number of hits per activation | 1 |
| **Can Crit** | Whether this ability can critically strike | true |
| **Hitbox Multiplier** | Damage multiplier (1.0 = normal) | 1.0 |
| **Resolver Type** | Combat resolution formula (Hybrid, StatBasedDirect, StatBasedRoll, PhysicsHitbox) | Hybrid |

#### On-Hit Status Effects

Each ability has two modifier slots:

| Field | Description |
|-------|-------------|
| **Primary/Secondary Effect > Type** | Effect type: Bleed, Burn, Freeze, Shock, Poison, Lifesteal, Stun, Slow, Weaken, Knockback, Explosion, Chain, Cleave, BonusDamage |
| **Chance** | Proc probability (0.0 = never, 1.0 = always) |
| **Duration** | Effect duration in seconds |
| **Intensity** | Effect severity (damage per tick, slow %, etc.) |

Example: A "Flame Strike" ability with Primary Effect = Burn, Chance = 0.8, Duration = 3.0, Intensity = 0.6 has an 80% chance to apply a 3-second burn.

#### Conditions

| Field | Description | Default |
|-------|-------------|---------|
| **Phase Min / Phase Max** | Encounter phase range (0-255, use 0/255 for "always available") | 0 / 255 |
| **HP Threshold Min / Max** | Only usable within this HP% range (0.0–1.0) | 0.0 / 1.0 |
| **Min Targets In Range** | Minimum targets within Range to be selectable | 0 |

Use Phase conditions to restrict abilities to specific encounter phases (e.g., a "Meteor" ability only available in Phase 3).

#### Behavior

| Field | Description | Default |
|-------|-------------|---------|
| **Movement During Cast** | Free (can move), Locked (rooted), or Slowed (50%) | Locked |
| **Interruptible** | Whether the cast can be interrupted (future feature) | false |
| **Priority Weight** | Higher = preferred by selection system | 1.0 |

#### Telegraph

| Field | Description | Default |
|-------|-------------|---------|
| **Telegraph Shape** | None, Circle, Cone, Line, Ring, or Cross | None |
| **Telegraph Damage On Expire** | If true, telegraph zone deals damage (not the ability directly) | false |

> Set Telegraph Duration > 0 AND a Telegraph Shape to show a ground indicator. If you set duration without a shape, the validator will warn you.

#### Animation

| Field | Description |
|-------|-------------|
| **Animation Trigger Name** | Animator trigger parameter name (hashed during bake). Leave empty if no animation. |

---

## 2. Creating an Ability Profile

An Ability Profile groups abilities into a rotation for a specific enemy type.

### 2.1 Create the Asset

1. Right-click in Project window (e.g., `Assets/Data/AI/Profiles/`)
2. Select **Create > DIG > AI > Ability Profile**
3. Name it (e.g., `BoxingJoe_Abilities`, `FireDragon_Abilities`)

### 2.2 Inspector Fields

| Field | Description |
|-------|-------------|
| **Selection Mode** | **Priority** = first valid ability wins (order matters). **Utility** = weighted scoring, highest wins (order doesn't matter). |
| **Abilities** | Drag `AbilityDefinitionSO` assets here. In Priority mode, the list order is the priority order. |

### 2.3 Design Tips

- **Priority mode:** Put high-impact, long-cooldown abilities FIRST. The system checks top-to-bottom and picks the first one that passes all checks (off cooldown, in range, correct phase, etc.). AutoAttack-style filler goes LAST.
- **Utility mode:** Order doesn't matter. `PriorityWeight` on each ability determines relative preference.
- Every phase should have at least one ability available. The Encounter Designer validator checks for "ability gaps" where all abilities are unavailable.

### 2.4 Example: BoxingJoe Profile (Priority Order)

```
1. Heavy Slam   — CD: 8s, Range: 3.0m, Damage: 40, Stun 30%, Phase 0-255
2. Jab           — CD: 3s, Range: 2.5m, Damage: 12, Group 1, Phase 0-255
3. Auto Attack   — CD: 1.5s, Range: 2.5m, Damage: 15, Group 1, Phase 0-255
```

With this setup: Heavy Slam fires whenever off cooldown (high damage, long CD). While it's on cooldown, Jab is preferred over AutoAttack. But Jab and AutoAttack share Group 1, so using Jab locks out AutoAttack for 1s.

---

## 3. Adding Ability Profile to an Enemy Prefab

### 3.1 Add the Component

1. Open the enemy prefab
2. Select the **root GameObject**
3. Click **Add Component** > search for **Ability Profile** (menu: `DIG > AI > Ability Profile`)
4. Drag your `AbilityProfileSO` asset into the **Profile** slot

### 3.2 What the Baker Adds

| Baked Component | Purpose |
|----------------|---------|
| `AbilityDefinition` buffer | One entry per ability in the profile (read-only at runtime) |
| `AbilityCooldownState` buffer | Parallel array tracking per-ability cooldown state |

> **If Ability Profile is present**, AI Brain's fallback melee ability is NOT generated (Ability Profile takes full control). If Ability Profile is absent, the fallback from AI Brain Inspector fields kicks in.

### 3.3 What AI Brain Now Bakes (Updated in 15.32)

| Baked Component | Purpose | Changed |
|----------------|---------|---------|
| `AIBrain` | Config values | No |
| `AIState` | Runtime state machine | No |
| `AbilityExecutionState` | Cast lifecycle tracking (replaces AIAttackState) | **New** |
| `MovementOverride` | Enableable tag for movement lock during casts | **New** |
| `MoveTowardsAbility` | Movement target/speed | No |
| `AttackStats` | Crit, attack power | No |
| `DefenseStats` | Defense, evasion | No |
| `CombatState` | Combat state integration | No |
| `WeaponModifier` buffer | Status effect passthrough for abilities | **New** |

---

## 4. Creating a Boss Encounter

Boss encounters add phase transitions, triggers, and add spawns on top of the ability system. Only add this to bosses — regular enemies don't need it.

### 4.1 Create the Encounter Profile

1. Right-click in Project window (e.g., `Assets/Data/AI/Encounters/`)
2. Select **Create > DIG > AI > Encounter Profile**
3. Name it (e.g., `FireDragon_Encounter`)

### 4.2 Encounter Settings

| Field | Description | Default |
|-------|-------------|---------|
| **Enrage Timer** | Seconds until hard enrage (-1 = no enrage) | -1 |
| **Enrage Damage Multiplier** | Damage multiplier after enrage | 3.0 |

### 4.3 Defining Phases

Click **+** on the Phases list to add phases. Phases are indexed by their list position (Phase 0, Phase 1, etc.).

| Field | Description | Default |
|-------|-------------|---------|
| **Phase Name** | Display name for editor tools | "Phase" |
| **HP Threshold Entry** | Boss transitions to this phase when HP% drops below this value. -1 = trigger-only (no HP threshold) | 1.0 |
| **Speed Multiplier** | Boss movement/attack speed multiplier in this phase | 1.0 |
| **Damage Multiplier** | Boss damage multiplier in this phase | 1.0 |
| **Global Cooldown Override** | Override GCD for this phase (-1 = use ability default) | -1 |
| **Invulnerable Duration** | Seconds of invulnerability on phase entry (transition window) | 0 |
| **Transition Ability** | Ability to force-cast on phase entry (e.g., a roar animation) | None |
| **Spawn Group Id** | Add group to spawn on phase entry (0 = none) | 0 |

**Example 3-Phase Dragon:**

| Phase | Name | HP Threshold | Speed | Damage | Invuln |
|-------|------|-------------|-------|--------|--------|
| 0 | Normal | 1.0 (100%) | 1.0x | 1.0x | 0s |
| 1 | Enraged | 0.7 (70%) | 1.2x | 1.1x | 2.0s |
| 2 | Desperate | 0.4 (40%) | 1.5x | 1.3x | 1.5s |
| 3 | Final Stand | 0.15 (15%) | 1.5x | 2.0x | 0s |

### 4.4 Defining Triggers

Click **+** on the Triggers list. Triggers are condition > action pairs that fire during the encounter.

#### Conditions

| Condition Type | Parameters | Description |
|---------------|-----------|-------------|
| **HPBelow** | Value (0.0-1.0) | Boss HP% <= value |
| **HPAbove** | Value (0.0-1.0) | Boss HP% >= value |
| **TimerElapsed** | Value (seconds), Param (0=encounter, 1=phase) | Timer >= value |
| **AddsDead** | Param (group ID), Value (count) | Dead adds in group >= count |
| **AddsAlive** | Param (group ID), Value (count) | Living adds in group <= count |
| **AbilityCastCount** | Value (count) | Total ability casts >= count |
| **PhaseIs** | Value (phase index) | Current phase == value |
| **BossAtPosition** | Position (Vector3), Range (float) | Boss within range of position |
| **Composite_AND** | Sub-triggers (up to 3 indices) | ALL referenced triggers have fired |
| **Composite_OR** | Sub-triggers (up to 3 indices) | ANY referenced trigger has fired |

#### Actions

| Action Type | Parameters | Description |
|------------|-----------|-------------|
| **TransitionPhase** | Value (phase index) | Force phase transition |
| **ForceAbility** | Param (ability ID) | Force-cast a specific ability |
| **SetInvulnerable** | Value (duration) | Apply invulnerability window |
| **Teleport** | Position (Vector3) | Move boss to position |
| **SetEnrage** | — | Activate enrage immediately |
| **ResetCooldowns** | — | Reset all ability cooldowns to 0 |
| **SpawnAddGroup** | Param (group ID) | Spawn an add group |
| **EnableTrigger** | Param (trigger index) | Enable another trigger |
| **DisableTrigger** | Param (trigger index) | Disable another trigger |

#### Trigger Options

| Field | Description | Default |
|-------|-------------|---------|
| **Fire Once** | Only fire once, then permanently disable | true |
| **Delay** | Seconds to wait after condition is met before executing action | 0 |

**Example Triggers:**

```
Trigger 0: TimerElapsed 300s (encounter) > SetEnrage       [FireOnce]
Trigger 1: HPBelow 0.70 > SpawnAddGroup 1                  [FireOnce]
Trigger 2: AddsDead group=1 count=4 > TransitionPhase 2    [FireOnce]
Trigger 3: Composite_AND [1, 2] > ForceAbility "PhaseRoar" [FireOnce]
```

This creates: 5-minute hard enrage. At 70% HP, spawn 4 adds. When all 4 die, skip to Phase 2 and cast "PhaseRoar".

### 4.5 Defining Spawn Groups

Click **+** on the Spawn Groups list.

| Field | Description | Default |
|-------|-------------|---------|
| **Group Id** | Unique ID (0-3, referenced by triggers and phases) | 0 |
| **Add Prefab** | The enemy prefab to spawn (must be a ghost prefab) | None |
| **Count** | Number to spawn | 1 |
| **Spawn Offset** | Offset from boss position | (0,0,0) |
| **Spawn Radius** | Random scatter radius around offset | 3.0 |
| **Tether To Boss** | If true, adds leash to boss instead of their spawn point | false |

> Add spawning is currently a stub — full ghost prefab instantiation requires NetCode spawning infrastructure. The data structure and trigger pipeline are fully functional.

---

## 5. Adding Encounter Profile to a Boss Prefab

1. Open the boss prefab
2. Select the **root GameObject** (must already have AI Brain + Ability Profile)
3. Click **Add Component** > search for **Encounter Profile** (menu: `DIG > AI > Encounter Profile`)
4. Drag your `EncounterProfileSO` asset into the **Profile** slot

### Component Stack for a Boss

A fully configured boss has these authoring components on the root:

```
[Damageable Authoring]          — Health, damage, death
[Physics Shape Authoring]       — Collision
[Physics Body Authoring]        — Movement
[Ghost Authoring Component]     — Network replication
[Linked Entity Group Authoring] — Entity reference remapping
[Detection Sensor Authoring]    — Vision, hearing (SETUP_GUIDE_15.19)
[Aggro Authoring]               — Threat, leash, pack (SETUP_GUIDE_15.19)
[AI Brain]                      — Behavior, movement, combat stats (SETUP_GUIDE_15.31)
[Ability Profile]               — Ability rotation (15.32, this guide)
[Encounter Profile]             — Phases, triggers, spawns (15.32, this guide)
```

Regular enemies only need up to AI Brain (and optionally Ability Profile). Encounter Profile is boss-only.

---

## 6. Using the Encounter Designer Window

### 6.1 Open the Window

**Menu: DIG > Encounter Designer**

### 6.2 Layout

The window has 4 panels:

```
+------------------+----------------------------------------------+
|                  |                                              |
|  ABILITY         |  ENCOUNTER TIMELINE                          |
|  LIBRARY         |  (HP bar, phases, abilities, triggers,       |
|                  |   validation results)                        |
|  (search,        |                                              |
|   browse all     |                                              |
|   ability SOs)   |                                              |
+------------------+----------------------------------------------+
|                  |                                              |
|  TRIGGER         |  ABILITY INSPECTOR                           |
|  EDITOR          |  (tabbed detail view of selected ability)    |
|                  |                                              |
|  (condition/     |  Tabs: Targeting | Timing | Damage |        |
|   action editor) |        Effects | Conditions | Telegraph     |
+------------------+----------------------------------------------+
```

### 6.3 Assigning Profiles

At the top toolbar:
- **Encounter** field: Drag an `EncounterProfileSO` to view/edit phases and triggers
- **Abilities** field: Drag an `AbilityProfileSO` to see the ability rotation

### 6.4 Ability Library (Top-Left)

- Shows ALL `AbilityDefinitionSO` assets in the project
- **Search**: Filter by name or damage type
- **Color coded**: By element type (red = Fire, blue = Ice, green = Poison, etc.)
- Click an ability to select it and view it in the Ability Inspector
- **+ New Ability**: Creates a new `AbilityDefinitionSO` asset

### 6.5 Encounter Timeline (Top-Right)

- **HP bar**: Visual representation of phase boundaries with color bands
- **Phase list**: Click to expand and edit phase properties inline
- **Ability rotation**: Shows all abilities in the profile with their phase ranges and cooldowns
- **Validation results**: Errors and warnings appear here after clicking Validate

### 6.6 Trigger Editor (Bottom-Left)

- **Trigger list**: All triggers in the encounter
- Click a trigger to expand and edit condition/action inline
- Fields change dynamically based on the selected condition and action types
- **+ Add Trigger**: Appends a new trigger

### 6.7 Ability Inspector (Bottom-Right)

- **Tabbed interface**: 6 tabs covering all ability fields
- **Live editing**: Changes save to the SO asset immediately
- **Inline warnings**: Validates the selected ability in real-time:
  - "Telegraph duration set but TelegraphShape is None"
  - "Cooldown group set but duration is 0"
  - "Radius > 0 but targeting is CurrentTarget — should it be AllInRange?"
  - "MaxCharges > 0 but ChargeRegenTime is 0"

### 6.8 Toolbar Actions

| Button | Action |
|--------|--------|
| **New** | Create a new EncounterProfileSO |
| **Save** | Save all modified assets |
| **Validate** | Run automated validation checks (see Section 7) |
| **Test** | Run dry-run simulation (see Section 8) |

---

## 7. Validation

Click **Validate** in the Encounter Designer (or call the validator from script). The system checks for:

| Check | Severity | Description |
|-------|----------|-------------|
| Ability gap | Error | A phase has no abilities available (boss stands idle) |
| Null ability reference | Error | Ability slot in profile is null |
| Composite trigger out of bounds | Error | Sub-trigger index exceeds trigger count |
| Trigger self-reference | Error | Composite trigger references itself |
| Circular trigger chain | Error | Enable/Disable trigger chains create infinite loops |
| Missing spawn group prefab | Error | Spawn group has no prefab assigned |
| Duplicate HP thresholds | Warning | Two phases share the same HP threshold |
| Telegraph without shape | Warning | Telegraph duration > 0 but shape is None |
| Cooldown group without duration | Warning | Group ID > 0 but group duration is 0 |
| Charges without regen | Warning | MaxCharges > 0 but ChargeRegenTime is 0 |
| Cooldown gap | Warning | Minimum cooldown much longer than cast times (boss may idle) |
| Zero spawn count | Warning | Spawn group count is 0 |
| Zero-damage offensive ability | Info | DamageBase is 0 on a non-Self targeting ability |

---

## 8. Encounter Simulation (Test Mode)

Click **Test** in the Encounter Designer to run a dry-run simulation without entering play mode.

### Configuration

The simulator uses these estimates (logged to Console):
- **DPS Estimate**: Configurable incoming DPS (default: 50)
- **Boss HP Estimate**: Configurable total boss HP (default: 10,000)
- **Simulation Duration**: Max 600 seconds (10 minutes)

### Output

The simulation logs a timeline to the Console:

```
=== Encounter Simulation ===
0:00 — HP:100% — Encounter started
0:02 — HP:99% — Cast: Auto Attack (dmg:15, cast:1.1s)
0:04 — HP:98% — Cast: Jab (dmg:12, cast:1.0s)
0:12 — HP:94% — Cast: Heavy Slam (dmg:40, cast:1.5s)
1:40 — HP:70% — Phase transition > Enraged (HP: 70%)
1:40 — HP:70% — Trigger [1] 'Spawn Totems' fired > SpawnAddGroup
2:50 — HP:50% — Cast: Heavy Slam (dmg:40, cast:1.5s)
3:20 — HP:40% — Phase transition > Desperate (HP: 40%)
5:00 — HP:15% — Phase transition > Final Stand (HP: 15%)
5:00 — HP:15% — Trigger [0] 'Hard Enrage' fired > SetEnrage

=== Warnings ===
  Boss idle for 2.1s at 1:42 (all abilities on cooldown after phase change)
  Trigger [4] 'PlayerCountInRange' never fired (not simulated)
```

### Limitations

- Cannot simulate AddsDead, AddsAlive, BossAtPosition, or PlayerCountInRange triggers (require runtime state)
- DPS is constant — doesn't account for player skill, downtime, or healing
- Ability selection uses Priority mode only
- Does not simulate combat resolution (crits, damage reduction)

---

## 9. Telegraph Scene Preview

When you select an `AbilityDefinitionSO` in the Project window (or via the Encounter Designer) and then select a GameObject in the Scene view:

- A **wireframe telegraph** appears at the GameObject's position
- Shape matches the ability's Telegraph Shape (Circle, Cone, Line, Ring, Cross)
- Size matches the ability's Radius/Range/Angle
- Semi-transparent red fill with wireframe outline
- Label shows ability name and radius

This helps designers visualize ability coverage without entering play mode.

---

## 10. Enemy Presets

### BoxingJoe with Ability Profile

```
AI Brain:
  (same as SETUP_GUIDE_15.31 — movement, stats, fallback values)

Ability Profile: BoxingJoe_Abilities (Priority mode)
  1. Heavy Slam  — CD:8s, Range:3.0, Dmg:40+/-10, Stun 30%/1.5s, Group:0
  2. Jab          — CD:3s, Range:2.5, Dmg:12+/-3,  Group:1 (1.0s lockout)
  3. Auto Attack  — CD:1.5s, Range:2.5, Dmg:15+/-5, Group:1 (1.0s lockout)
```

### Fire Dragon Boss

```
AI Brain:
  Archetype: Boss
  Chase Speed: 4.0
  Melee Range: 4.0

Ability Profile: FireDragon_Abilities (Priority mode)
  1. Meteor (Phase 2+) — CD:15s, GroundAtTarget, R:5.0, Dmg:80, Telegraph:Circle 2.0s
  2. Flame Breath       — CD:8s, Cone 60deg R:8.0, Dmg:35, Burn 80%/3s, Phase 0-255
  3. Tail Swipe          — CD:5s, Ring R:4.0 inner:1.5, Dmg:25, Knockback 100%
  4. Claw Attack         — CD:2s, CurrentTarget R:4.0, Dmg:20, Phase 0-255

Encounter Profile: FireDragon_Encounter
  Enrage Timer: 300s (5 min)
  Phase 0: Normal (HP >= 70%), Speed 1.0x, Damage 1.0x
  Phase 1: Enraged (HP 70%), Speed 1.2x, Damage 1.1x, Invuln 2.0s
  Phase 2: Desperate (HP 40%), Speed 1.5x, Damage 1.3x, SpawnGroup 1
  Phase 3: Final Stand (HP 15%), Damage 2.0x

  Triggers:
    [0] Timer 300s > SetEnrage (FireOnce)
    [1] HPBelow 0.70 > SpawnAddGroup 1 (FireOnce)
    [2] AddsDead group=1 count=4 > TransitionPhase 2 (FireOnce)

  Spawn Groups:
    Group 1: FireTotem prefab, Count=4, Radius=8.0, TetherToBoss=true
```

---

## 11. After Setup: Reimport Subscene

After adding or modifying Ability Profile or Encounter Profile on a prefab in a subscene:

1. Open the Scene window
2. Right-click the subscene > **Reimport**
3. Wait for baking to complete

This ensures AbilityDefinition buffers, cooldown states, phase definitions, and trigger definitions are baked onto all instances.

> Changing an `AbilityDefinitionSO` or `AbilityProfileSO` asset triggers automatic rebake via `DependsOn()`. You only need manual reimport when adding/removing authoring components.

---

## 12. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Backward compat | Enter play mode with BoxingJoe (no Ability Profile) | Same behavior as 15.31 — patrol, chase, melee attack |
| 3 | Ability Profile | Add Ability Profile to BoxingJoe, enter play mode | BoxingJoe uses ability rotation instead of hardcoded attack |
| 4 | Priority selection | Give BoxingJoe 3 abilities with different cooldowns | Boss uses highest-priority available ability |
| 5 | Cooldown groups | Give two abilities the same CooldownGroupId | Using one locks out the other for the group duration |
| 6 | Status effect | Give an ability a Burn modifier (Chance=1.0 for testing) | Target receives Burn DOT after hit |
| 7 | Telegraph | Set TelegraphDuration=1.0 and TelegraphShape=Circle | Red circle appears on ground before damage |
| 8 | Phase transition | Set up 2-phase encounter (Phase 1 at 70% HP) | Boss transitions at 70% HP, invulnerability window if configured |
| 9 | Trigger fire | Set HPBelow 0.5 > SetEnrage trigger | Enrage activates when boss reaches 50% HP |
| 10 | Encounter Designer | Open DIG > Encounter Designer, assign profiles | 4-panel window displays phases, triggers, abilities |
| 11 | Validation | Click Validate with a misconfigured encounter | Errors/warnings appear in timeline panel |
| 12 | Simulation | Click Test with valid encounter | Timeline log appears in Console |
| 13 | Scene preview | Select an ability SO with TelegraphShape=Circle, select a GameObject | Red wireframe circle appears in Scene view |

---

## 13. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Enemy still uses old melee attack | AbilityProfileAuthoring not added or Profile field is empty | Add Ability Profile component and assign an AbilityProfileSO |
| Enemy never attacks with abilities | AbilitySelectionSystem can't find valid ability | Check: cooldowns, range, phase conditions. Run Validation in Encounter Designer |
| Boss doesn't transition phases | EncounterProfileAuthoring not added, or HP thresholds wrong | Add Encounter Profile. Thresholds are 0.0-1.0 (0.7 = 70% HP) |
| Triggers never fire | Encounter not started (requires aggro) | EncounterTriggerSystem waits for IsAggroed before ticking timers |
| Telegraph appears but no damage | TelegraphDamageOnExpire is false AND ability has 0 direct damage | Set TelegraphDamageOnExpire=true for telegraph-only abilities |
| Cooldown group not working | CooldownGroupDuration is 0 | Set CooldownGroupDuration > 0 on the ability that fires |
| Charges don't regenerate | ChargeRegenTime is 0 | Set ChargeRegenTime > 0 |
| Status effect doesn't apply | Modifier Chance is 0, or Type is None | Set Chance > 0 and pick a valid Type |
| Changes to ability SO not reflected | Subscene cache | Reimport subscene, or modify in play mode for immediate testing |
| Encounter Designer shows no abilities | No AbilityDefinitionSO assets exist | Create abilities via Create > DIG > AI > Ability Definition |
| Multiple abilities fire simultaneously | Not possible — one at a time | AbilityExecutionState ensures only one ability is active. GCD prevents instant chaining |

---

## 14. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Vision, hearing, detection sensors | SETUP_GUIDE_15.19 |
| Aggro, threat tables, leashing, pack behavior | SETUP_GUIDE_15.19 |
| Combat resolution, damage formulas, resolvers | SETUP_GUIDE_15.28 |
| Weapon modifiers, status effects | SETUP_GUIDE_15.29 |
| Damage number colors, DOT visuals, status text | SETUP_GUIDE_15.30 |
| Enemy AI brain, patrol, chase, basic attack | SETUP_GUIDE_15.31 |
| **Ability system, telegraphs, encounters, editor tools** | **This guide (15.32)** |
