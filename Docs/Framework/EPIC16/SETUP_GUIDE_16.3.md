# SETUP GUIDE 16.3: Enemy Death Lifecycle & Corpse Management

**Status:** Implemented
**Last Updated:** February 14, 2026
**Requires:** Damageable Authoring on enemy prefabs (standard since EPIC 15.28)

This guide covers Unity Editor setup for the corpse lifecycle system. After setup, dead enemies play a ragdoll, persist as visible corpses, then sink into the ground and are destroyed — instead of vanishing instantly on death.

---

## What Changed

Previously, dead enemies received the `Disabled` component instantly in `DeathTransitionSystem`, making them vanish from the world. Entities were never destroyed and sat in memory forever.

Now:
- **Ragdoll plays** — dead enemies stay visible and ragdoll for a configurable duration
- **Corpses persist** — after ragdoll settles, the body stays in place with AI/combat components stripped
- **Sink-into-ground fade** — after persistence time, the corpse sinks 1.5m into the ground
- **Entity destroyed** — after sinking, the entity is fully destroyed (freed from memory, ghost auto-despawned on clients)
- **MaxCorpses cap** — oldest non-boss corpses are auto-evicted when the cap is exceeded
- **AI stops on death** — all AI systems skip dead entities (no more zombies chasing you)

---

## What's Automatic (No Setup Required)

If you change nothing, all enemies with `Damageable Authoring` automatically use the corpse lifecycle with default timings. No new components need to be added to existing prefabs.

| Feature | How It Works |
|---------|-------------|
| CorpseState baked on all damageables | `DamageableAuthoring` bakes `CorpseState` (disabled) on every entity with health |
| Default timings | If no `CorpseConfig` singleton exists, the system creates one with defaults at runtime |
| AI death guard | All 6 AI systems skip entities with `Health.Current <= 0` |
| Client-side sink | `CorpseSinkSystem` derives timing from replicated `DeathState` — works on remote clients without extra replication |
| Ghost auto-despawn | When the server destroys the entity, NetCode removes it from all clients automatically |

### Default Timings (No Config Needed)

| Phase | Duration | What Happens |
|-------|----------|-------------|
| Ragdoll | 2.0s | Body ragdolls, all components still present |
| Settled | 15.0s | Body at rest, AI/combat components stripped, physics frozen |
| Fading | 1.5s | Body sinks 1.5m into ground, physics collider removed |
| Destroy | — | Entity destroyed, memory freed |

---

## 1. Configuring Global Corpse Settings (Optional)

To override the default timings, place a **Corpse Config** singleton in your subscene.

### 1.1 Add the Component

1. In your gameplay subscene, create an empty GameObject (or use an existing config holder)
2. Click **Add Component** > search for **Corpse Config** (menu path: `DIG/Combat/Corpse Config`)
3. Adjust the Inspector fields

### 1.2 Inspector Fields

#### Ragdoll

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Ragdoll Duration** | Seconds the ragdoll plays before settling. Set to 0 to skip ragdoll. | 2.0 | 0–10 |

#### Corpse Persistence

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Corpse Lifetime** | Seconds the corpse stays visible after ragdoll settles | 15.0 | 1–120 |
| **Max Corpses** | Maximum corpses in the world. Oldest non-boss corpse is removed when exceeded | 30 | 5–200 |
| **Persistent Bosses** | Boss/elite corpses are never auto-evicted by the MaxCorpses cap | true | — |

#### Fade Out

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Fade Out Duration** | Seconds for the corpse to sink into the ground before being destroyed | 1.5 | 0.5–5.0 |

#### Distance Culling

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| **Distance Cull Range** | Corpses beyond this distance from any player skip fade and are destroyed instantly | 100 | 20–500 |

### 1.3 Only One Per World

`CorpseConfig` is a singleton. If multiple `CorpseConfigAuthoring` components exist in a subscene, only one will be active. Place it once on a dedicated config GameObject (alongside other singletons like `PhysicsConfig`, etc.).

---

## 2. Per-Prefab Timing Overrides (Advanced)

Individual enemy types can override the global timings using `CorpseSettingsOverride`. This is useful for boss enemies that should have longer ragdoll/persistence times.

> **Note:** There is currently no Inspector authoring component for `CorpseSettingsOverride`. To use per-prefab overrides, add the component via a custom baker or at runtime. Values of `-1` mean "use global CorpseConfig default".

| Field | Description | Default |
|-------|-------------|---------|
| **Ragdoll Duration** | Per-prefab ragdoll override (-1 = use global) | -1 |
| **Corpse Lifetime** | Per-prefab persistence override (-1 = use global) | -1 |
| **Fade Out Duration** | Per-prefab fade override (-1 = use global) | -1 |
| **Is Boss** | If true, this corpse is never evicted by MaxCorpses cap | false |

### Example: Boss with Extended Persistence

A boss baker could add:

```
CorpseSettingsOverride:
  RagdollDuration: 4.0    (longer ragdoll for dramatic death)
  CorpseLifetime: 60.0    (visible for 1 minute)
  FadeOutDuration: 3.0    (slow sink)
  IsBoss: true            (never auto-evicted)
```

---

## 3. Lifecycle Phases Explained

```
DEATH
  │
  ▼
┌──────────────────────────────────────────────────────────┐
│  RAGDOLL (default 2.0s)                                  │
│  - Body ragdolls with full physics                       │
│  - All components still present (AI, combat, physics)    │
│  - RagdollTransitionSystem activates ragdoll animation   │
└───────────────────────┬──────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────┐
│  SETTLED (default 15.0s)                                 │
│  - Body at rest in final ragdoll position                │
│  - AI components stripped: AIBrain, AIState,             │
│    AbilityExecutionState, EnemySeparationConfig          │
│  - Combat stats stripped: AttackStats, DefenseStats      │
│  - PhysicsVelocity zeroed (frozen in place)              │
│  - MaxCorpses eviction can force-skip to Fading          │
└───────────────────────┬──────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────┐
│  FADING (default 1.5s)                                   │
│  - PhysicsCollider removed (exits broadphase)            │
│  - CorpseSinkSystem sinks Y position by 1.5m            │
│  - No physics interactions possible                      │
└───────────────────────┬──────────────────────────────────┘
                        │
                        ▼
                   ENTITY DESTROYED
                   (NetCode ghost auto-despawned on clients)
```

---

## 4. Tuning Guide

### High Enemy Density (Horde Mode)

Lower corpse persistence to avoid entity bloat:

| Setting | Value |
|---------|-------|
| Corpse Lifetime | 3–5s |
| Max Corpses | 15–20 |
| Fade Out Duration | 0.5–1.0s |

### Boss Encounters

Use per-prefab `CorpseSettingsOverride` with `IsBoss = true` to ensure boss corpses are never evicted. Set longer timings for dramatic effect:

| Setting | Value |
|---------|-------|
| Ragdoll Duration | 3–5s |
| Corpse Lifetime | 30–120s |
| Fade Out Duration | 2–3s |

### Performance-Conscious (Low-End Hardware)

Reduce MaxCorpses and persistence aggressively:

| Setting | Value |
|---------|-------|
| Corpse Lifetime | 2–3s |
| Max Corpses | 10 |
| Ragdoll Duration | 1.0s |

---

## 5. After Setup: Reimport Subscene

After placing or modifying `CorpseConfigAuthoring` in a subscene:

1. Open the Scene window
2. Right-click the subscene > **Reimport**
3. Wait for baking to complete

> If you skip this step and no `CorpseConfig` singleton is baked, the system auto-creates one with defaults at runtime. No errors, just default timings.

---

## 6. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Ragdoll plays | Kill an enemy | Body ragdolls, stays visible (not instant vanish) |
| 3 | Corpse settles | Wait ~2s after death | Body stops moving, stays in place |
| 4 | Corpse sinks | Wait ~17s after death (2s ragdoll + 15s persist) | Body sinks into the ground over ~1.5s |
| 5 | Entity destroyed | Check Entity Debugger after sink completes | Entity count decreases |
| 6 | MaxCorpses cap | Kill 35 enemies | Only ~30 corpses visible, oldest auto-removed |
| 7 | Boss persistence | Kill a boss with IsBoss=true, then kill 35 regular enemies | Boss corpse remains, regular corpses evicted |
| 8 | Remote client | Kill enemy on host, observe on remote client | Remote client sees death + corpse staying visible |
| 9 | AI stops on death | Kill an enemy mid-chase | Enemy stops moving immediately on death |
| 10 | No console errors | Kill several enemies, wait through full lifecycle | No exceptions or warnings in Console |

---

## 7. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| Enemies still vanish instantly | CorpseState not baked on entity | Verify Damageable Authoring is on the prefab root. Reimport subscene |
| Corpse sinks too fast / too slow | Default timings don't match your needs | Place CorpseConfigAuthoring and adjust FadeOutDuration |
| Too many corpses causing lag | MaxCorpses too high for enemy density | Lower MaxCorpses in CorpseConfigAuthoring (try 10–15) |
| Dead enemies still move briefly | DeathTransitionSystem 1-frame delay | Normal — Health reaches 0 on frame N, DeathPhase set on frame N+1. AI health check stops movement on frame N+1 |
| Boss corpse disappeared early | IsBoss not set on the CorpseSettingsOverride | Add CorpseSettingsOverride with IsBoss=true via custom baker |
| Corpses pile up and never despawn | CorpseLifecycleSystem not running | Check Entity Debugger > Systems. Must be ServerSimulation or LocalSimulation world |
| Corpse doesn't sink on remote client | CorpseSinkSystem reads DeathState (replicated) | Verify DeathState has GhostField on Phase and StateStartTime |

---

## 8. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Health, damage, death components | Damageable Authoring (EPIC 15.28) |
| Ragdoll activation on death | RagdollTransitionSystem (EPIC 15.28) |
| AI brain, patrol, chase, combat | SETUP_GUIDE_15.31 |
| Ability system, telegraphs, encounters | SETUP_GUIDE_15.32 |
| Physics optimization, collision filters | SETUP_GUIDE_15.23 |
| **Corpse lifecycle, sink-to-ground, MaxCorpses** | **This guide (16.3)** |
