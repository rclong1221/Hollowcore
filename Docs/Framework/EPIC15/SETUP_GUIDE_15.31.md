# SETUP GUIDE 15.31: Enemy AI Brain — Vertical Slice

**Status:** Implemented
**Last Updated:** February 13, 2026
**Requires:** EPIC 15.19 setup complete (see `SETUP_GUIDE_15.19.md`)

This guide covers Unity Editor setup for the AI brain system. After setup, enemies patrol near their spawn, chase players on sight, attack in melee range, and return home when the player flees or dies.

---

## What Changed

Before EPIC 15.31, enemies had full perception (vision, hearing, aggro, threat tracking, leashing) but zero behavior. They detected players, tracked threats, selected targets... and stood still.

Now enemies with the AI Brain component:
- **Patrol** randomly near their spawn point
- **Chase** the player when aggroed (vision/hearing/pack alert)
- **Attack** in melee range with telegraphed wind-up → hit → recovery
- **Deal damage** through the existing combat resolution pipeline (crit rolls, damage numbers, health reduction)
- **Return home** when the player dies, flees beyond leash distance, or breaks line of sight long enough

---

## What's Automatic (No Setup Required)

These features work immediately once `AI Brain` is added:

| Feature | How It Works |
|---------|-------------|
| State machine (Idle/Patrol/Combat/ReturnHome) | AIStateTransitionSystem reads AggroState automatically |
| Target selection | Uses existing aggro pipeline — AI Brain never picks targets directly |
| Damage numbers on player | PendingCombatHit flows through CombatResolutionSystem → existing damage visual pipeline |
| Pack aggro | AggroShareSystem alerts nearby allies — all with AI Brain will chase |
| Leash/return home | LeashSystem clears aggro → AI transitions to ReturnHome automatically |
| Death cleanup | DeathTransitionSystem disables entity → removed from all AI queries |
| Movement | MoveTowardsAbility → MoveTowardsSystem applies PhysicsVelocity |

---

## 1. Adding AI Brain to an Enemy Prefab

### 1.1 Prerequisites

The enemy prefab must already have these components (BoxingJoe has all of them):

| Required Component | Purpose | Setup Guide |
|-------------------|---------|-------------|
| `Damageable Authoring` | Health, death state, damage buffers | — |
| `Physics Shape Authoring` | Collision for raycasts and movement | — |
| `Physics Body Authoring` | Dynamic body for PhysicsVelocity | — |
| `Detection Sensor Authoring` | Vision cone + hearing detection | SETUP_GUIDE_15.19 |
| `Aggro Authoring` | Threat tracking, leash, pack sharing | SETUP_GUIDE_15.19 |
| `Ghost Authoring Component` | Network replication | — |
| `Linked Entity Group Authoring` | Entity reference remapping on spawn | — |

### 1.2 Add the Component

1. Open the enemy prefab (e.g., `Assets/Prefabs/BoxingJoe.prefab`)
2. Select the **root GameObject** (e.g., "BoxingJoe")
3. Click **Add Component**
4. Search for **AI Brain** (menu path: `DIG > AI > AI Brain`)
5. The component appears in the Inspector with four header sections

### 1.3 What the Baker Adds

The AI Brain authoring component automatically bakes these ECS components — you do not need to add them manually:

| Baked Component | Purpose |
|----------------|---------|
| `AIBrain` | Config values from Inspector |
| `AIState` | Runtime state machine (starts Idle) |
| `AIAttackState` | Attack lifecycle tracking |
| `MoveTowardsAbility` | Movement target/speed (starts stopped) |
| `AttackStats` | Crit chance, attack power (for combat resolution) |
| `DefenseStats` | Defense, evasion (for incoming damage) |
| `CombatState` | Combat state integration |

> **Note:** If `CombatStateAuthoring` is already on the prefab (BoxingJoe has it), the baked `CombatState` from AI Brain will coexist. This is harmless — ECS deduplicates identical component types.

---

## 2. Inspector Reference

### Archetype

| Property | Description | Default |
|----------|-------------|---------|
| **Archetype** | Enemy behavior category (Melee, Ranged, Swarm, Elite, Boss) | Melee |

> Phase 1 only implements Melee behavior. Other archetypes are reserved for future phases.

### Movement

| Property | Description | Default | Range |
|----------|-------------|---------|-------|
| **Chase Speed** | Movement speed when pursuing target (m/s) | 5.0 | 2.0–10.0 |
| **Patrol Speed** | Movement speed when wandering (m/s) | 1.5 | 0.5–3.0 |
| **Patrol Radius** | Wander radius from spawn position (meters) | 8.0 | 2.0–20.0 |

**Tips:**
- Chase Speed should be close to or slightly below player run speed (creates tension without being unfair)
- Patrol Radius determines how far an enemy wanders from its spawn — larger values make patrols look more natural but increase territory overlap
- Setting Patrol Radius to 0 disables patrol (enemy stays at spawn, enters combat on aggro)

### Melee Attack

| Property | Description | Default | Range |
|----------|-------------|---------|-------|
| **Melee Range** | Attack reach in meters | 2.5 | 1.0–5.0 |
| **Attack Cooldown** | Seconds between attacks | 1.5 | 0.5–5.0 |
| **Attack Wind Up** | Telegraph time before hit (seconds) | 0.4 | 0.1–2.0 |
| **Attack Active Duration** | Hit window duration (seconds) | 0.15 | 0.05–0.5 |
| **Attack Recovery** | Vulnerable period after attack (seconds) | 0.5 | 0.1–2.0 |

**Attack Timeline:**

```
|--- WindUp (0.4s) ---|--- Active (0.15s) ---|--- Recovery (0.5s) ---|--- Cooldown (1.5s) ---|
   Telegraph/Tell         Damage applied          Can't act              Waiting to attack
   Facing locks           Distance + facing       again
   at end                 check required
```

**Design Notes:**
- **Wind Up** is the player's reaction window — increase for fairer combat, decrease for harder enemies
- **Active Duration** is how long the hit check stays active — short values feel snappy, long values are forgiving to the AI
- **Recovery** is the player's punish window — increase to reward dodging
- **Cooldown** controls attack frequency — the total cycle is `WindUp + Active + Recovery + Cooldown`
- An attack with defaults takes **2.55 seconds** per cycle (~0.39 attacks/second)

### Damage

| Property | Description | Default |
|----------|-------------|---------|
| **Base Damage** | Base attack damage per hit | 15 |
| **Damage Variance** | Random variance (± this value) | 5 |
| **Damage Type** | Elemental type (Physical, Fire, Ice, etc.) | Physical |

The actual damage dealt per hit is `random(BaseDamage - Variance, BaseDamage + Variance)`, so with defaults: **10–20 damage per hit**.

Damage flows through `CombatResolutionSystem` which applies:
- Crit rolls (uses AttackStats.CritChance)
- Hitbox multipliers (always Torso = 1.0x for AI melee)
- Defense reduction (uses target's DefenseStats)
- Damage type colors (see `SETUP_GUIDE_15.30.md`)

### Behavior Thresholds

| Property | Description | Default |
|----------|-------------|---------|
| **Flee Health Percent** | HP % threshold to flee (0.0–1.0) | 0.2 |

> **Phase 2 feature.** This value is stored but not read in Phase 1. Flee behavior will use it in EPIC 15.32.

### Combat Stats

| Property | Description | Default |
|----------|-------------|---------|
| **Attack Power** | Base attack power for combat resolution formulas | 5 |
| **Crit Chance** | Probability of critical hit (0.0–1.0) | 0.1 (10%) |
| **Crit Multiplier** | Damage multiplier on critical hit | 1.5 (150%) |
| **Accuracy** | Hit accuracy for resolution formulas | 1.0 (100%) |
| **Defense** | Damage reduction for incoming hits | 5 |
| **Evasion** | Dodge probability for incoming hits (0.0–1.0) | 0.05 (5%) |

These feed into the existing `CombatResolutionSystem` resolver (same stats used by player weapons).

---

## 3. Enemy Presets

### BoxingJoe (Standard Melee)

```
Archetype: Melee

Movement:
  Chase Speed: 5.0
  Patrol Speed: 1.5
  Patrol Radius: 8.0

Melee Attack:
  Melee Range: 2.5
  Attack Cooldown: 1.5
  Attack Wind Up: 0.4
  Attack Active Duration: 0.15
  Attack Recovery: 0.5

Damage:
  Base Damage: 15
  Damage Variance: 5
  Damage Type: Physical

Combat Stats:
  Attack Power: 5
  Crit Chance: 0.1
  Crit Multiplier: 1.5
  Accuracy: 1.0
  Defense: 5
  Evasion: 0.05
```

### Slow Brute (Tanky, Telegraphed)

```
Archetype: Melee

Movement:
  Chase Speed: 3.0
  Patrol Speed: 1.0
  Patrol Radius: 5.0

Melee Attack:
  Melee Range: 3.5
  Attack Cooldown: 3.0
  Attack Wind Up: 1.0
  Attack Active Duration: 0.3
  Attack Recovery: 1.0

Damage:
  Base Damage: 40
  Damage Variance: 10
  Damage Type: Physical

Combat Stats:
  Attack Power: 10
  Crit Chance: 0.05
  Crit Multiplier: 2.0
  Defense: 15
  Evasion: 0.0
```

### Fast Swarm (Quick, Fragile)

```
Archetype: Melee

Movement:
  Chase Speed: 7.0
  Patrol Speed: 2.5
  Patrol Radius: 12.0

Melee Attack:
  Melee Range: 1.5
  Attack Cooldown: 0.8
  Attack Wind Up: 0.2
  Attack Active Duration: 0.1
  Attack Recovery: 0.3

Damage:
  Base Damage: 5
  Damage Variance: 2
  Damage Type: Physical

Combat Stats:
  Attack Power: 3
  Crit Chance: 0.15
  Crit Multiplier: 1.5
  Defense: 2
  Evasion: 0.15
```

### Fire Elemental

```
Archetype: Melee

Movement:
  Chase Speed: 4.5
  Patrol Speed: 1.5
  Patrol Radius: 6.0

Melee Attack:
  Melee Range: 2.0
  Attack Cooldown: 2.0
  Attack Wind Up: 0.5
  Attack Active Duration: 0.2
  Attack Recovery: 0.6

Damage:
  Base Damage: 20
  Damage Variance: 8
  Damage Type: Fire

Combat Stats:
  Attack Power: 7
  Crit Chance: 0.1
  Crit Multiplier: 1.5
  Defense: 8
  Evasion: 0.1
```

> Fire damage numbers will appear in orange-red. See `SETUP_GUIDE_15.30.md` for element color configuration.

---

## 4. Interaction with Aggro Settings

AI Brain reads from the aggro pipeline — the two systems must be tuned together.

| AI Brain Setting | Aggro Setting (SETUP_GUIDE_15.19) | Relationship |
|-----------------|-----------------------------------|-------------|
| Chase Speed | Leash Distance | Chase Speed determines how fast the enemy closes distance. If Chase Speed is fast but Leash Distance is short, the enemy may leash before reaching the player. |
| Melee Range | — | Must be less than Leash Distance or the enemy can never attack. |
| Patrol Radius | Leash Distance | Patrol should stay within leash range. If PatrolRadius > LeashDistance, the enemy could patrol beyond its leash and immediately try to return. |
| — | Aggro Share Radius | All nearby enemies with AI Brain will chase simultaneously. Balance pack size against difficulty. |
| — | Memory Duration | Longer memory = enemy stays in Combat longer after losing sight. Short memory = faster transition to ReturnHome. |

### Recommended Pairings

| Enemy Type | Leash Distance | Chase Speed | Melee Range | Aggro Share Radius |
|-----------|---------------|-------------|-------------|-------------------|
| Standard guard | 40–50m | 5.0 | 2.5 | 20m |
| Territory defender | 25m | 6.0 | 2.5 | 15m |
| Persistent hunter | 100m+ | 5.5 | 2.5 | 30m |
| Stationary sentinel | 15m | 4.0 | 3.0 | 0 (solo) |

---

## 5. State Machine Reference

The AI Brain cycles through these states automatically based on aggro and position:

```
                    ┌──────────────────────────────────┐
                    │          AggroState.IsAggroed     │
                    ▼                                   │
┌──────┐  5-15s  ┌────────┐              ┌──────────┐  │
│ IDLE │────────▶│ PATROL │              │  COMBAT  │◀─┘
│      │◀────────│        │              │          │
└──┬───┘ arrived └────┬───┘              └────┬─────┘
   │     or 20s       │                      │
   │                  │                      │ !IsAggroed
   │                  │                      ▼
   │                  │              ┌──────────────┐
   └──────────────────┴──────────────│ RETURN HOME  │
              arrived < 1.5m         └──────────────┘
```

| State | Behavior | Exits When |
|-------|----------|-----------|
| **Idle** | Stands still at current position | 5–15 seconds elapsed (random) → Patrol |
| **Patrol** | Wanders randomly within PatrolRadius of spawn | Arrived at target or 20s timeout → Idle |
| **Combat** | Chases target, attacks when in range | Aggro drops (leash/target dead/fled) → ReturnHome |
| **ReturnHome** | Walks back to spawn position at ChaseSpeed | Arrives within 1.5m of spawn → Idle |

> **Any state → Combat** when `AggroState.IsAggroed` becomes true (highest priority transition).

> **Attack guard:** State transitions are blocked while an attack is in progress (WindUp/Active/Recovery). The attack must complete its full lifecycle before the AI can change state.

---

## 6. After Setup: Reimport Subscene

After adding AI Brain to a prefab that exists in a subscene:

1. Open the Scene window
2. Find the subscene containing your enemy instances (e.g., `Subscene.unity`)
3. Right-click the subscene → **Reimport**
4. Wait for baking to complete

This ensures the new ECS components (AIBrain, AIState, AIAttackState, MoveTowardsAbility, AttackStats, DefenseStats) are baked onto all instances.

---

## 7. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Patrol | Enter play mode, observe BoxingJoe from distance | BoxingJoe wanders near spawn every 5–15 seconds |
| 3 | Chase | Walk into detection range | BoxingJoe runs toward player at ChaseSpeed |
| 4 | Attack | Stand still at melee range | BoxingJoe pauses (WindUp), then deals damage. Damage number appears on player |
| 5 | Damage amount | Check damage number | Between 10–20 (BaseDamage 15 ± Variance 5) |
| 6 | Attack cooldown | Stay in range after first hit | Next attack comes ~1.5s after recovery ends |
| 7 | Return home | Run beyond LeashDistance (50m) | BoxingJoe walks back to spawn, resumes idle |
| 8 | Pack aggro | Place 3+ BoxingJoes with AggroShareRadius=20, approach one | All nearby BoxingJoes chase |
| 9 | Kill enemy | Reduce BoxingJoe health to 0 | BoxingJoe dies (DeathTransitionSystem), disappears from AI queries |
| 10 | Player death | Die while BoxingJoe is attacking | BoxingJoe stops attacking, returns home |
| 11 | Multiple enemies | 5+ BoxingJoes simultaneously chasing | All move and attack independently, no freezing |
| 12 | Attack whiff | Move away during WindUp telegraph | Attack misses (no damage), BoxingJoe resumes chase |

---

## 8. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Enemy doesn't move at all | Missing `Physics Body Authoring` or `Physics Shape Authoring` | Add both to root GameObject. Body must be Dynamic |
| Enemy detects player but doesn't chase | AI Brain not added, or subscene not reimported | Add `AI Brain` component, reimport subscene |
| Enemy chases but never attacks | MeleeRange too small or player position above/below | Increase MeleeRange. Distance check is XZ-plane only |
| Enemy attacks but no damage appears | Target missing `DamageableAuthoring` | Ensure player has `DamageableAuthoring` component |
| Enemy gets stuck during attack forever | Attack phase timer bug | Check that AttackWindUp, AttackActiveDuration, AttackRecovery are all > 0 |
| Enemy wanders into walls | No NavMesh/pathfinding integration in Phase 1 | Reduce PatrolRadius or ensure spawn position has open space |
| Only one enemy moves, others frozen | MoveTowardsSystem bug (old code) | Verify line 22 of `MoveTowardsSystem.cs` uses `continue` not `return` |
| Enemy doesn't return home after chase | Aggro not clearing | Check LeashDistance in AggroAuthoring (0 = no leash). Check MemoryDuration |
| Enemy teleports on attack | Physics velocity spike | Reduce ChaseSpeed. Ensure PhysicsBody has reasonable damping |
| No crit damage numbers | CritChance too low or resolver not evaluating | Increase CritChance to 1.0 for testing |
| Changes not taking effect | Stale subscene bake | Reimport the subscene (right-click > Reimport) |

---

## 9. Known Limitations (Phase 1)

| Limitation | Workaround | Future Phase |
|-----------|-----------|-------------|
| No attack animations | Damage still applies, just no visual telegraph | Phase 2 (EPIC 15.32) |
| No circle-strafe during cooldown | Enemy stands facing target between attacks | Phase 2 |
| No flee behavior | FleeHealthPercent is stored but unused | Phase 2 |
| No hit reactions/stagger | Enemy continues attacking when hit | Phase 2 |
| No attack throttling | Multiple enemies can attack the same frame | Phase 3 |
| No pathfinding integration | Enemy moves in straight lines (uses PhysicsVelocity) | Phase 2+ |
| No ranged attacks | Only melee | Phase 3 |
| Approximate hit point | Melee uses target center, not physics raycast | Acceptable for melee |

---

## 10. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Vision, hearing, detection sensors | SETUP_GUIDE_15.19 |
| Aggro, threat tables, leashing, pack behavior | SETUP_GUIDE_15.19 |
| Combat resolution, damage formulas, resolvers | SETUP_GUIDE_15.28 |
| Damage number colors, DOT visuals, status text | SETUP_GUIDE_15.30 |
| Weapon modifiers (BonusDamage, DOT, Explosion) | SETUP_GUIDE_15.29 |
| **Enemy AI brain, patrol, chase, attack** | **This guide (15.31)** |
