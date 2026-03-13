# EPIC 14.2 Setup Guide: Boss Encounter Flow & Phase Transitions

**Status:** Planned
**Requires:** EPIC 14.1 (BossDefinitionSO, BossVariantState); Framework: Combat/ (EncounterState, EncounterTriggerDefinition), AI/

---

## Overview

The boss encounter flow covers the full lifecycle of a boss fight: arena entrance, variant clause evaluation, multi-phase combat with health-threshold transitions, mid-fight events (Strife interruptions, reinforcement waves), and victory or failure states. Boss health persists across player death attempts. This guide covers configuring encounter triggers, phase transitions, enrage timers, and the pre-fight modifier display.

---

## Quick Start

### Prerequisites

| Object | Component | Purpose |
|--------|-----------|---------|
| BossDefinitionSO (14.1) | Configured boss definition | Phase thresholds, variant clauses |
| Boss arena subscene (14.3) | Arena with trigger volume | Encounter activation |
| BossEncounterComponents.cs | ECS components | BossPhaseState, BossEncounterLink, BossInvulnerable |
| BossPreFightUI prefab | UI panel | Modifier display before combat |

### New Setup Required

1. Create boss encounter trigger volume in the arena subscene.
2. Configure `EncounterTriggerDefinition` on the trigger volume with `BossEncounterTag`.
3. Create `BossPreFightUI` prefab in `Assets/Prefabs/UI/Boss/`.
4. Configure enrage timer (if applicable) in the encounter blob data.
5. Wire `BossEncounterSystem` to framework EncounterState transitions.

---

## 1. Encounter Trigger Volume

**Create:** A trigger collider in the boss arena subscene entrance.
**Recommended:** Box or sphere collider with `Is Trigger = true`.

### 1.1 Trigger Configuration

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `EncounterTriggerDefinition` | Framework component on the trigger entity | -- | Standard EncounterTrigger setup |
| Tag | Use custom `BossEncounterTag` | -- | Distinguishes boss encounters from regular encounters |
| One-shot | `true` | -- | Boss encounters trigger once per visit |

**Tuning tip:** Place the trigger volume inside the arena entrance doorway so the player crosses it naturally. Avoid placing it too deep inside the arena (player may be attacked before the pre-fight UI appears).

---

## 2. Boss Encounter State Machine

`BossEncounterSystem` drives the `BossEncounterPhase` state machine:

### 2.1 Phase Flow

| Phase | Description | Duration | Transition To |
|-------|-------------|----------|---------------|
| `Inactive` (0) | No encounter active | Until player enters trigger | PreFight |
| `PreFight` (1) | Clause evaluation, modifier display, arena intro | UI dismiss or 10s auto-timeout | Combat |
| `Combat` (2) | Active fight, health monitoring | Until health threshold or boss death | PhaseTransition, MidFightEvent, Victory, Failure |
| `PhaseTransition` (3) | Brief invulnerability + cinematic | `EventTimer` (1-3s typical) | Combat |
| `MidFightEvent` (4) | Strife interruption or reinforcement spawn | Until event resolves | Combat |
| `Victory` (5) | Boss dead, reward sequence | Immediate | (EPIC 14.5 takes over) |
| `Failure` (6) | Player dead, respawn flow | Immediate | (EPIC 2 takes over, then Inactive with persistent HP) |

### 2.2 BossPhaseState Fields

| Field | Description | Default | Range / Notes |
|-------|-------------|---------|---------------|
| `EncounterPhase` | Current state in the encounter state machine | Inactive | See flow above |
| `CurrentPhaseIndex` | Boss combat phase (0-based, maps to BossDefinitionSO.Phases) | 0 | Increments on phase transition |
| `TotalPhases` | Total phases for this boss | -- | Set from BossDefinitionSO at encounter start |
| `NextPhaseThreshold` | Health fraction for next transition (0 = no more) | -- | Read from BossDefinitionSO.Phases[CurrentPhaseIndex+1].HealthThreshold |
| `EventTimer` | Countdown for transitions and events | 0 | Driven by PhaseTransitionDuration or event duration |
| `PlayerDeathCount` | Deaths this encounter | 0 | Increments on Failure |
| `PersistentBossHealthPercent` | Stored boss HP for respawn persistence | 1.0 | Boss HP does NOT reset on player death |

---

## 3. Phase Transition Configuration

### 3.1 Transition Timing

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `PhaseTransitionDuration` | Seconds of invulnerability during transition | 2.0 | 1.0-5.0. Warn if > 5s (long invulnerability frustrates players) |

During `PhaseTransition`:
1. `BossInvulnerable` enabled on boss entity (prevents all damage).
2. Phase transition cinematic or animation plays.
3. New attack patterns from the next phase definition are activated.
4. Arena events from the next phase definition fire (layout changes, new hazards).
5. When `EventTimer` expires: `BossInvulnerable` disabled, return to Combat.

**Tuning tip:** Keep phase transitions between 1.5-3.0 seconds. Shorter than 1.5s feels abrupt; longer than 3.0s breaks combat flow. If the boss has a dramatic phase change (arena reconfigures), 3-4s is acceptable.

### 3.2 Health Threshold Monitoring

During Combat phase, `BossEncounterSystem` checks each tick:
```
if (bossHealth / bossMaxHealth <= NextPhaseThreshold)
    → transition to PhaseTransition
```

Phase thresholds come from `BossDefinitionSO.Phases[i].HealthThreshold`. They must be strictly decreasing (validated at bake time).

---

## 4. Enrage Timer

### 4.1 Configuration

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| `EnrageTimerSeconds` | Total fight time before enrage (0 = no enrage) | 0 | 60-600. Warn if < 60s (too short) or > 600s (effectively none) |

### 4.2 Enrage Behavior

When the timer expires:
- Boss gains significant stat buffs (damage, speed).
- Arena hazards may intensify.
- Fight becomes effectively a DPS check -- wipe condition if players cannot finish soon.

**Tuning tip:** Cross-check enrage timer with the DPS Calculator in Boss Workstation. The "minimum viable DPS" should be achievable by a reasonably geared player. If average player DPS at the expected gear level cannot beat the enrage, the timer is too tight.

---

## 5. Mid-Fight Events

### 5.1 Reinforcement Waves

Triggered by variant clauses with `BossClauseEffectType.SpawnReinforcements`:

| Setting | Description | Range |
|---------|-------------|-------|
| Health trigger threshold | Boss HP percentage to spawn wave | 0.75, 0.50, 0.25 typical |
| ReinforcementWaveSO | Enemy wave definition | From framework AI/ spawning |
| One-shot per clause | Each reinforcement clause fires at most once | Prevents infinite waves |

### 5.2 Strife Interruptions

Triggered by active Strife variant clauses during combat. Execute the Strife card's boss-specific effect (e.g., UI possession, gravity shift, arena modification).

---

## 6. Pre-Fight UI

### 6.1 BossPreFightUI Prefab

**Location:** `Assets/Prefabs/UI/Boss/BossPreFightUI.prefab`

| Element | Purpose |
|---------|---------|
| Boss portrait + name | Identity display |
| Active clause list | Icons + descriptions of all active variant clauses |
| "Insurance" highlight | Clauses that COULD have been disabled (missed side goals) shown in red |
| Disabled clause list | Clauses disabled by counter tokens shown struck-through |
| Boss health bar | Shows persistent health if re-attempt |
| Dismiss button / auto-timer | Player dismisses or 10s auto-timeout |

### 6.2 Bridge Setup

`BossPreFightUIBridge` (managed MonoBehaviour) listens for `BossPreFightUIEvent` from ECS:
- Reads `ActiveBossClauseBuffer` for clause display data.
- Reads `BossVariantState` for health multiplier and clause count.
- Fires `PreFightDismissed` signal back to ECS when player dismisses or timer expires.

---

## Scene & Subscene Checklist

- [ ] Boss arena subscene has encounter trigger volume with EncounterTriggerDefinition
- [ ] Trigger volume uses BossEncounterTag
- [ ] BossPreFightUI prefab exists in `Assets/Prefabs/UI/Boss/`
- [ ] BossPreFightUIBridge MonoBehaviour is on the UI prefab
- [ ] Boss prefab has BossInvulnerable (IEnableableComponent, baked disabled)
- [ ] Phase transition cinematic assets exist (animation clips, VFX)

---

## Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Trigger volume placed too deep in arena | Boss attacks before pre-fight UI shows | Move trigger to arena entrance |
| PhaseTransitionDuration = 0 | No cinematic, instant skip, breaks visual | Set to at least 1.0s |
| EnrageTimer too short for average DPS | Most players wipe every time | Use DPS Calculator to validate; increase timer |
| Mid-fight event health trigger overlaps phase threshold (within 0.02) | Reinforcement wave and phase transition fire simultaneously | Offset by at least 0.05 |
| Forgetting BossInvulnerable on prefab | Boss takes damage during phase transitions | Ensure BossDefinitionAuthoring bakes it |
| Not checking PersistentBossHealthPercent on re-attempt | Boss appears at full HP after player death | BossEncounterSystem uses PersistentBossHealthPercent on resume |
| BossEncounterLink not populated | NPC entity not connected to encounter state | Wire encounter trigger to link NPC entity to encounter entity |

---

## Verification

- [ ] Boss encounter activates when player enters arena trigger volume
- [ ] PreFight phase displays active variant clauses in UI
- [ ] Combat phase: boss AI is active, health monitored for phase transitions
- [ ] Phase transition: boss becomes invulnerable, cinematic plays, new attacks enabled
- [ ] PhaseTransition timer expires and returns to Combat correctly
- [ ] Boss Health <= 0 triggers Victory state
- [ ] Player death triggers Failure state without resetting boss health
- [ ] PersistentBossHealthPercent carries across death/respawn
- [ ] PlayerDeathCount increments on each failure
- [ ] Mid-fight reinforcement waves spawn at correct health thresholds
- [ ] Strife interruptions fire during combat when matching Strife card is active
- [ ] Victory fires reward and extraction events
- [ ] BossInvulnerable prevents damage during phase transitions only
- [ ] Enrage timer triggers at correct time (if configured)
- [ ] Pre-fight UI auto-dismisses after timeout
