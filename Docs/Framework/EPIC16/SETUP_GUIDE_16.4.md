# SETUP GUIDE 16.4: Comprehensive Aggro & Threat Framework

**Status:** Implemented
**Last Updated:** February 22, 2026
**Requires:** AI Brain (EPIC 15.31), Detection System (EPIC 15.19)

This guide covers Unity Editor setup for the aggro and threat framework. After setup, enemies react to damage, sound, proximity, and social bonds — with 7 target selection modes, 5-level stealth alerts, group aggro behaviors, and comprehensive debug tools.

> **Code references:** Internal code comments reference this as `EPIC 15.33`.

---

## What Changed

Previously, enemies only aggroed when they visually detected the player. Shooting an enemy from behind generated no threat. The hearing pipeline existed but nothing emitted sounds. Social aggro was a one-shot share on initial detection.

Now:

- **Damage generates threat** — both hitscan (DamageEvent) and combat resolution (CombatResultEvent) pipelines create threat entries
- **Sound propagation** — weapon fire and explosions emit sound events that AI can hear through walls (with LOS check for attenuation)
- **Proximity body pulls** — enemies aggro when players approach even without line of sight
- **Social/group aggro** — linked encounter pulls, call-for-help, ally death reactions (avenge, enrage, flee)
- **7 target selection modes** — HighestThreat, WeightedScore, Nearest, LastAttacker, LowestHealth, Random, Defender
- **5-level stealth alerts** — IDLE → CURIOUS → SUSPICIOUS → SEARCHING → COMBAT with guard communication cascade
- **Body discovery** — guards find dead allies and raise alert
- **Full debug tooling** — AI Workstation with 4 tabs, scene gizmos, debug tester

---

## What's Automatic (No Setup Required)

If your enemies already have `AggroAuthoring`, all new threat sources (damage, sound, proximity) work with zero changes. New features default to disabled/zero for full backward compatibility.

| Feature | How It Works |
|---------|-------------|
| Damage → Threat | Both DamageEvent and CRE pipelines now generate threat automatically |
| Weapon sounds | Gunfire creates SoundEventRequest entities → distributed to nearby AI |
| Explosion sounds | Explosions emit loud sound events (Loudness scales with blast radius) |
| Alert escalation/de-escalation | AlertStateSystem handles 5-level model automatically |
| Threat decay | Existing ThreatDecaySystem + memory duration, unchanged |
| Target selection | Default HighestThreat mode, unchanged from before |

---

## 1. Base Aggro Setup (AggroAuthoring)

Every enemy that should have threat tracking needs `AggroAuthoring`. This was already required before EPIC 16.4 — the new fields extend the existing component.

### 1.1 Add the Component

1. Select your enemy prefab root
2. Click **Add Component** > search for **Aggro Authoring**

### 1.2 Inspector Fields

#### Threat Multipliers

| Field | Description | Default |
|-------|-------------|---------|
| **Damage Threat Multiplier** | Raw damage × this = threat added | 1.0 |
| **Sight Threat Value** | Initial threat when first spotted | 10.0 |
| **Hearing Threat Value** | Base threat from heard sounds (before attenuation) | 3.0 |

#### Decay Settings

| Field | Description | Default |
|-------|-------------|---------|
| **Visible Decay Rate** | Threat/sec lost while target is visible | 0.5 |
| **Hidden Decay Rate** | Threat/sec lost while target is hidden | 0.5 |
| **Memory Duration** | Seconds before a hidden target is completely forgotten | 30.0 |

#### Target Selection

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Hysteresis Ratio** | New threat must exceed current × this to trigger a switch | 1.1 | 1.0–2.0 |
| **Max Tracked Targets** | Max threat table entries per entity | 8 | 1–16 |
| **Minimum Threat** | Entries below this are pruned | 0.1 | — |

#### Leashing & Territory

| Field | Description | Default |
|-------|-------------|---------|
| **Leash Distance** | Max distance from spawn before reset. 0 = no leash (boss) | 50.0 |

#### Social Behavior

| Field | Description | Default |
|-------|-------------|---------|
| **Aggro Share Radius** | One-shot aggro share range on first detection. 0 = lone wolf | 20.0 |
| **Alert State Multiplier** | Detection boost when in alert state | 1.5 |

#### Proximity (New)

| Field | Description | Default |
|-------|-------------|---------|
| **Proximity Threat Radius** | 360-degree, no LOS threat range. 0 = disabled | 0.0 |
| **Proximity Threat Per Second** | Threat generated per second when player is in proximity radius | 5.0 |

#### Advanced Target Selection (New)

| Field | Description | Default |
|-------|-------------|---------|
| **Selection Mode** | Target selection algorithm (see table below) | HighestThreat |
| **Distance Weight** | Weight for distance in WeightedScore mode | 0 |
| **Health Weight** | Weight for target health in WeightedScore mode | 0 |
| **Recency Weight** | Weight for how recently target was seen | 0 |
| **Target Switch Cooldown** | Seconds before allowing another target switch | 0 |
| **Random Switch Chance** | Per-evaluation chance to switch randomly (0-1) | 0 |

### 1.3 Target Selection Modes

| Mode | Behavior | Best For |
|------|----------|----------|
| **HighestThreat** (0) | Always attacks highest threat. Default, backward compatible | Standard enemies |
| **WeightedScore** (1) | Composite of threat + distance + health + recency weights | Elite enemies, diverse targeting |
| **Nearest** (2) | Always attacks closest player | Melee rushers, swarm enemies |
| **LastAttacker** (3) | Attacks whoever hit last | Reactive/punishment enemies |
| **LowestHealth** (4) | Prioritizes weakest player | Assassin/predator AI |
| **Random** (5) | Random target from threat table (deterministic seed) | Chaotic enemies |
| **Defender** (6) | Prioritizes players attacking allies | Tank/protector AI, MOBA towers |

---

## 2. Social Aggro Setup (Optional)

Solo enemies (lone wolves, animals) should NOT have this component. Add `SocialAggroAuthoring` only to enemies that coordinate in groups.

### 2.1 Add the Component

1. On the same root GameObject as Aggro Authoring
2. Click **Add Component** > search for **Social Aggro Authoring**

### 2.2 Inspector Fields

#### Group

| Field | Description | Default |
|-------|-------------|---------|
| **Encounter Group ID** | Shared group identifier. Aggro one = aggro all with same ID. 0 = no linked pull | 0 |
| **Linked Pull** | When one entity in the group enters combat, all others instantly share threat | false |

#### Call For Help

| Field | Description | Default |
|-------|-------------|---------|
| **Call For Help** | Periodically emit help signals to nearby allies | true |
| **Respond To Help** | React to other entities' help signals | true |
| **Call For Help Radius** | Range of help signal | 25m |
| **Call For Help Cooldown** | Seconds between help emissions | 3s |
| **Call For Help Threat Share** | Fraction of leader threat shared to responders (0-1) | 0.5 |

#### Ally Reactions

| Field | Description | Default |
|-------|-------------|---------|
| **Ally Death Avenge** | Gain bonus threat on the killer when an ally dies | true |
| **Ally Death Threat Bonus** | Flat threat added to the killer | 50 |
| **Ally Death Enrage** | Multiply all threat when ally dies | false |
| **Ally Death Rage Multiplier** | Threat multiplier on enrage (1-3) | 1.5 |
| **Share Damage Info** | Continuously share damage threat with nearby allies | false |
| **Ally Damaged Threat Share** | Fraction of damage threat shared (0-1) | 0 |
| **Ally Death Flee** | Flee when an ally dies (cowardly enemies) | false |

#### Pack

| Field | Description | Default |
|-------|-------------|---------|
| **Role** | None, Alpha, or Member. Alpha death doubles pack threat | None |
| **Pack Behavior** | Enable pack hierarchy logic | false |

#### Stealth

| Field | Description | Default |
|-------|-------------|---------|
| **Guard Communication** | Cascade alert levels to nearby guards | false |
| **Body Discovery** | Detect dead allies and raise alert | false |

#### Defender

| Field | Description | Default |
|-------|-------------|---------|
| **Defender Aggro** | MOBA-tower behavior: auto-target players attacking nearby allies | false |

---

## 3. Preset Configurations

### 3.1 Standard Melee Enemy

Only needs `AggroAuthoring` with defaults. No social aggro.

### 3.2 Guard Patrol Group

| Component | Field | Value |
|-----------|-------|-------|
| AggroAuthoring | Proximity Threat Radius | 8m |
| AggroAuthoring | Selection Mode | HighestThreat |
| SocialAggroAuthoring | Encounter Group ID | (shared per group, e.g., 101) |
| SocialAggroAuthoring | Linked Pull | true |
| SocialAggroAuthoring | Call For Help | true |
| SocialAggroAuthoring | Guard Communication | true |
| SocialAggroAuthoring | Body Discovery | true |

### 3.3 Wolf Pack

| Component | Field | Value (Alpha) | Value (Member) |
|-----------|-------|--------------|---------------|
| AggroAuthoring | Selection Mode | HighestThreat | Nearest |
| SocialAggroAuthoring | Linked Pull | true | true |
| SocialAggroAuthoring | Role | Alpha | Member |
| SocialAggroAuthoring | Pack Behavior | true | true |
| SocialAggroAuthoring | Ally Death Avenge | true | true |
| SocialAggroAuthoring | Ally Death Enrage | true | true |

### 3.4 Boss (Solo)

| Component | Field | Value |
|-----------|-------|-------|
| AggroAuthoring | Leash Distance | 0 (no leash) |
| AggroAuthoring | Selection Mode | WeightedScore |
| AggroAuthoring | Distance Weight | 0.3 |
| AggroAuthoring | Health Weight | 0.2 |
| AggroAuthoring | Target Switch Cooldown | 3s |
| (No SocialAggroAuthoring) | — | — |

### 3.5 MOBA Tower

| Component | Field | Value |
|-----------|-------|-------|
| AggroAuthoring | Selection Mode | Defender |
| SocialAggroAuthoring | Defender Aggro | true |

---

## 4. Alert System (5 Levels)

The alert system runs automatically on all entities with `AggroAuthoring`. No additional setup needed.

```
IDLE (white)
  ↓ old signals nearby
CURIOUS (yellow)
  ↓ hearing/social/proximity threat
SUSPICIOUS (orange)
  ↓ strong signals, body discovery
SEARCHING (dark orange)
  ↓ direct combat engagement
COMBAT (red)
```

**Escalation** is immediate. **De-escalation** steps down one level at a time:
- COMBAT → SEARCHING after 3s without visible target
- SEARCHING → SUSPICIOUS after 10s search timer
- SUSPICIOUS → CURIOUS after 8s
- CURIOUS → IDLE after 5s

Guards with **Guard Communication** enabled cascade alerts to nearby allies: COMBAT pushes neighbors to SEARCHING, SEARCHING pushes to SUSPICIOUS, etc.

Guards with **Body Discovery** enabled detect dead NPCs within `CallForHelpRadius` and raise their own alert to SUSPICIOUS.

---

## 5. Editor Tools

### 5.1 AI Workstation

**Menu:** DIG > AI Workstation

A 4-tab editor window for live AI inspection during Play Mode.

**Entity Selector** (top bar):
- "Pick Entity" button — click an enemy in Scene view
- Manual entity index field
- State filter dropdown (All/Idle/Patrol/Combat/ReturnHome/Investigate/Flee)

#### Brain Inspector Tab

| Section | What It Shows |
|---------|--------------|
| **HFSM State** | Current state, sub-state, timers, ability guard blocking |
| **Resource Pool** | Two ResourceSlot progress bars (EPIC 16.8 integration) |
| **Threat Table** | AGGROED/PASSIVE status, per-entry progress bars (red=leader, green=visible, gray=hidden), threat values |
| **Ability Cooldowns** | Per-ability ready/cooling bars, charges, GCD |
| **Target Info** | Target entity, threat value, distance, melee range indicator |
| **Leash Gauge** | Distance from spawn (green→yellow→red), warning at 85% |
| **Config Summary** | Archetype, damage type, ranges, speeds |

#### Dashboard Tab

Aggregate stats refreshed at ~4Hz:
- State distribution (Idle/Patrol/Combat/ReturnHome/Investigate/Flee counts)
- Combat stats (aggroed count, average threat, casting count)
- Proportional distribution bar with legend

#### Overlay Tab

Configures world-space debug labels above AI heads:
- Master ON/OFF toggle
- Display options: State, SubState, ThreatValue, TargetEntity, ActiveAbility/Phase, Health%
- Filters: OnlyCombat, OnlyAggroed, MaxCameraDistance
- Visual: FontSize, BackgroundAlpha

#### Scene Tools Tab

Scene view handles:
- Patrol radius (blue disc at spawn)
- Leash radius (green→yellow→red disc, white spawn marker)
- Detection range (vision cone wireframe)
- Melee range (red disc)
- Threat lines: red (3px) to current leader, yellow (1.5px) to visible entries, gray to hidden
- All Threat Lines (global): faint red lines from ALL aggroed enemies to their targets

### 5.2 Aggro Gizmo Renderer

Attach `AggroGizmoRenderer` MonoBehaviour to any scene GameObject for Scene view gizmos.

| Toggle | What It Draws |
|--------|--------------|
| **Draw Threat Lines** | Lines from AI to threat targets, colored by source flag |
| **Draw Radii** | Proximity (purple), aggro share (cyan), call-for-help (orange) spheres |
| **Draw Alert Capsules** | Color-coded capsules: white=IDLE, yellow=CURIOUS, orange=SUSPICIOUS, red=COMBAT |
| **Draw Linked Pull Groups** | Yellow lines between entities with same EncounterGroupId |

### 5.3 Aggro Pipeline Debug

Attach `AggroPipelineDebug` MonoBehaviour for console logging.

| Field | Default |
|-------|---------|
| Enable Logging | true |
| Log Interval | 1.0s |

Filter by `[AGGRO]` in Console to see: vision stats, detection details, threat table aggregates, alert levels.

### 5.4 Aggro Debug Tester

Attach `AggroDebugTester` MonoBehaviour for runtime testing.

- **TargetEntityIndex** — select entity (0 = auto-detect)
- Read-only runtime fields: IsAggroed, CurrentTargetName, ThreatTableSize, ThreatEntries
- Context menu: **Add Test Threat**, **Wipe Threat Table**, **Taunt (+1000)**

---

## 6. After Setup: Reimport SubScene

After adding or modifying aggro authoring components on prefabs in a SubScene:

1. Right-click the SubScene > **Reimport**
2. Wait for baking to complete

---

## 7. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Damage threat | Shoot enemy from behind | Enemy turns and attacks you |
| 3 | Sound hearing | Fire weapon near enemies around a corner | Nearby enemies investigate the sound |
| 4 | Proximity | Walk near enemy with ProximityThreatRadius > 0 | Enemy detects you without LOS |
| 5 | Social linked pull | Aggro one enemy in a group (same EncounterGroupId) | All group members enter combat |
| 6 | Call for help | Let one enemy see you, wait for cooldown | Nearby allies start investigating |
| 7 | Ally death avenge | Kill enemy with AllyDeathAvenge allies nearby | Allies gain bonus threat on you |
| 8 | Alert cascade | Engage guard with GuardCommunication | Nearby guards escalate through alert levels |
| 9 | Body discovery | Kill guard, watch nearby guard with BodyDiscovery | Discoverer goes SUSPICIOUS, investigates corpse |
| 10 | Target selection | Set mode to Nearest, approach with 2 players | Enemy always targets closest player |
| 11 | Leash | Kite enemy beyond LeashDistance | Enemy resets, returns to spawn |
| 12 | AI Workstation | Open DIG > AI Workstation, pick entity | Live threat table, state, ability data visible |
| 13 | Scene gizmos | Attach AggroGizmoRenderer | Alert capsules, threat lines, radii visible in Scene view |

---

## 8. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Enemy ignores gunfire | No SoundEmitter on weapon or HearingThreatValue = 0 | WeaponSoundEmitterSystem runs automatically; check HearingThreatValue > 0 |
| Damage doesn't generate threat | Entity resolution failed (CHILD→ROOT) | Ensure AggroAuthoring is on ROOT entity (same as DamageableAuthoring) |
| Social aggro not working | Missing SocialAggroAuthoring | Add to group enemies; solo enemies intentionally skip it |
| Linked pull only pulls some | EncounterGroupId mismatch | All group members must share the exact same nonzero ID |
| Guard doesn't discover bodies | BodyDiscovery flag not set | Enable on SocialAggroAuthoring |
| Alert stuck at COMBAT | Enemy still sees target | Normal — de-escalation only begins when target is no longer visible |
| AI Workstation shows no entity | Wrong world selected | Workstation prefers ServerWorld; ensure server is running |
| Threat lines not visible | AggroGizmoRenderer not in scene | Attach to any scene GameObject |
| Target switching too fast | No hysteresis or cooldown | Increase HysteresisRatio or set TargetSwitchCooldown > 0 |

---

## 9. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| AI Brain states, patrol, combat | SETUP_GUIDE_15.31 |
| Ability framework, encounters | SETUP_GUIDE_15.32 |
| Detection system, vision sensors | SETUP_GUIDE_15.19 |
| Enemy death, corpse lifecycle | SETUP_GUIDE_16.3 |
| Knockback system | SETUP_GUIDE_16.9 |
| **Aggro & threat framework** | **This guide (16.4)** |
