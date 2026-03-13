# EPIC 11.4 Setup Guide: Meta-Expedition Rivals (Past-Run Ghosts)

**Status:** Planned
**Requires:** EPIC 11.1 (Rival Definition & Simulation), EPIC 12.1 (Scar Map Data Model), Framework Persistence/ (RunHistorySaveModule), Roguelite/ (RunStatistics)

---

## Overview

Past expeditions echo forward as rival teams in future runs. The player's own previous expedition data -- routes, equipment, objectives, death location -- is loaded from the Scar Map persistence layer and converted into a rival team that follows the historical route exactly. The result: you encounter ghosts of your past decisions, fight your old loadouts, and see your old failures as echoes. This creates a deeply personal meta-narrative.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 11.1 | Rival system (spawn, simulation) | Ghost rivals register in the same pipeline |
| EPIC 12.1 | ScarMapState persistence | Source data for ghost route generation |
| Persistence/ | RunHistorySaveModule | Stores completed expedition records |
| Roguelite/ | RunStatistics | Run count, history management |

### New Setup Required
- 1 `PastRunRivalConfigAuthoring` singleton in subscene
- 1 `RivalNPC_Ghost.prefab` with ghost shader material
- 3 ghost dialogue trees (Death, Extraction, Abandonment)
- `GhostShader.mat` translucent material
- Assembly references to `Hollowcore.Persistence`, `Hollowcore.Roguelite`

---

## 1. Create the PastRunRivalConfig Singleton
**Create:** Add `PastRunRivalConfigAuthoring` MonoBehaviour to the expedition manager prefab.

### 1.1 Configuration Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| MinRunsBeforeGhosts | Minimum completed expeditions before ghosts appear | 3 | 1-10 |
| RecencyWeight | Preference for recent runs (higher = more recent) | 0.6 | 0.0-1.0 |
| DeathRunWeight | Preference for runs that ended in death | 0.4 | 0.0-1.0 |
| MaxGhostsPerExpedition | Cap on past-run rivals per expedition | 1 | 0-2 |

**Tuning tip:** MinRunsBeforeGhosts at 3 prevents ghosts from appearing before the player has enough history for it to be meaningful. Increase to 5+ if you want ghosts to feel more like a late-game surprise.

**Tuning tip:** DeathRunWeight at 0.4 slightly favors death runs over extractions, because finding your own corpse is more emotionally impactful than seeing a ghost that successfully extracted.

---

## 2. Create the Ghost NPC Prefab
**Create:** `Assets/Prefabs/Rivals/RivalNPC_Ghost.prefab`

### 2.1 Differences from Normal Rival NPCs
| Aspect | Normal Rival | Ghost Rival |
|--------|-------------|-------------|
| Material | Standard character shader | GhostShader.mat (translucent, chromatic aberration) |
| Member count | 1-4 | Always 1 (solo ghost) |
| Dialogue | Personality-based | End-reason-specific ("I died here last time...") |
| Combat AI | Standard rival behavior | Mimics player's historical build/playstyle |
| Loot | Based on EquipmentTier | Matches player's historical loadout at that point |

### 2.2 Required Components
| Component | Purpose |
|-----------|---------|
| All standard rival NPC components | AIBrain, AIState, Health, Physics, etc. |
| PastRunRivalTag (added at spawn) | Marks as ghost for special handling |
| Visual mesh | Same player model base with ghost material override |

---

## 3. Create the Ghost Shader Material
**Create:** `Assets/Materials/Rivals/GhostShader.mat`

### 3.1 Material Properties
| Property | Value | Description |
|----------|-------|-------------|
| Render queue | Transparent | Renders after opaque geometry |
| Base alpha | 0.6 | Semi-transparent appearance |
| Chromatic aberration | Subtle edge fringing | Indicates temporal anomaly |
| Emission | Low cyan glow | Distinguishes from living NPCs |
| Rim lighting | Stronger than normal | Silhouette visible in dark areas |

**Tuning tip:** The ghost should be unmistakably different from a living rival at first glance. The translucency + chromatic aberration combo communicates "echo of the past" without being cartoonish.

---

## 4. Create Ghost Dialogue Trees
**Recommended location:** `Assets/Data/Dialogue/Rivals/`

### 4.1 Death Ghost Dialogue
**File:** `PastRunGhost_Death.asset`

| Branch | Text | Outcome |
|--------|------|---------|
| Approach | "I remember this place. I died here." | — |
| Threat | "Maybe I can change things this time." | Combat (Desperate type) |
| Warn | "Don't go deeper. Trust me." | Intel about death district |

### 4.2 Extraction Ghost Dialogue
**File:** `PastRunGhost_Extract.asset`

| Branch | Text | Outcome |
|--------|------|---------|
| Approach | "I made it out last time. Barely." | — |
| Intel | "Let me show you what I learned." | Reveals route info on Scar Map |
| Trade | "I brought supplies. Want to trade?" | Opens trade UI |

### 4.3 Abandonment Ghost Dialogue
**File:** `PastRunGhost_Abandon.asset`

| Branch | Text | Outcome |
|--------|------|---------|
| Approach | "I gave up too early last time." | — |
| Challenge | "Race me to the objective this time." | Race encounter |
| Plead | "Help me finish what I started." | Cooperative objective hint |

---

## 5. Ghost Route Generation

`PastRunRivalSystem` converts Scar Map data into a rival simulation:

### 5.1 Data Extraction from RunHistorySaveModule
| Source Data | Derived Rival Field |
|-------------|---------------------|
| District visit order | PastRunRouteEntry buffer |
| Equipment at each gate | EquipmentSnapshotHash per route entry |
| Run end reason | PastRunEndReason |
| Average gear level | DerivedEquipmentTier |
| Front exposure history | Derived RiskTolerance |
| Equipped limb types | Derived BuildStyle |
| Total districts visited | SourceDistrictsVisited |

### 5.2 Route Following (Not Probabilistic)
Unlike normal rivals, ghosts follow the exact historical route:
1. On gate transition, advance to next `PastRunRouteEntry`
2. Outcomes are determined by history, not probability
3. Death ghosts die at the exact historical district/zone
4. Extraction ghosts extract at the historical extraction point

### 5.3 Candidate Selection Algorithm
```
1. Get all completed expeditions from RunHistorySaveModule
2. Exclude current expedition seed
3. Score each candidate:
   Score = (RecencyWeight * recencyScore) + (DeathRunWeight * deathScore)
   Where:
     recencyScore = 1.0 - (runAge / totalRuns)  [0-1]
     deathScore = endReason == Death ? 1.0 : 0.3
4. Select highest-scoring candidate
5. Cap at MaxGhostsPerExpedition
```

---

## 6. Encounter Type Overrides

Ghost encounters override the normal type resolution from EPIC 11.3:

| PastRunEndReason | Encounter Type | Rationale |
|------------------|---------------|-----------|
| Death | Desperate (hostile) | Ghost is reliving its final fight |
| Extraction | Intel (neutral) | Ghost shares "what it learned" |
| Abandonment | Race (competitive) | Ghost is trying to do better this time |

---

## 7. Ghost Body Loot

When a ghost dies (or its historical death point is reached), the body carries equipment matching the player's historical loadout:

| Route Entry | Loot Source |
|-------------|-------------|
| EquipmentSnapshotHash | Maps to specific limb IDs, weapon IDs, consumables |
| Historical loadout | Player's actual gear at that point in the source run |

This means finding your ghost's body lets you recover gear you previously had, creating a satisfying loop.

---

## 8. Scene & Subscene Checklist

- [ ] `PastRunRivalConfigAuthoring` on expedition manager prefab
- [ ] `RivalNPC_Ghost.prefab` exists with ghost shader material
- [ ] `GhostShader.mat` exists with translucent + chromatic aberration properties
- [ ] 3 ghost dialogue trees exist (Death, Extract, Abandon)
- [ ] Assembly references include `Hollowcore.Persistence`, `Hollowcore.Roguelite`
- [ ] `RunHistorySaveModule` stores required expedition data (route, equipment, end reason)
- [ ] `PastRunRivalSimOverrideSystem` runs before `RivalSimulationSystem`

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| MinRunsBeforeGhosts = 0 | Ghost on first expedition with no history | Set to 3+ minimum |
| Missing ghost dialogue trees | Null reference during encounter; dialogue hangs | Create all 3 dialogue assets |
| Missing GhostShader.mat | Ghost renders with default material; looks like normal NPC | Create translucent material |
| Not excluding current seed from candidates | Player fights their own current run as a ghost | Filter out matching expedition seed |
| Double-processing ghost in RivalSimulationSystem | Ghost moves probabilistically instead of following route | PastRunRivalSimOverrideSystem sets LastSimulatedTransition |
| RunHistorySaveModule missing equipment snapshots | Ghost bodies have no loot or wrong loot | Ensure equipment snapshots saved at each gate transition |
| MaxGhostsPerExpedition too high | Multiple ghosts clutter the experience | Keep at 1; ghosts are special, not common |
| Ghost loot too valuable | Players farm ghosts for gear | Scale ghost loot to match the run's difficulty, not current run |

---

## Verification

- [ ] No past-run rivals spawn on first 2 expeditions (MinRunsBeforeGhosts=3)
- [ ] Ghost correctly selected from expedition history (recency + death weighted)
- [ ] PastRunRouteEntry buffer matches source expedition's district visit order
- [ ] Ghost follows historical route exactly (not probabilistic)
- [ ] Death ghost dies at historical district/zone
- [ ] Extraction ghost extracts at historical district
- [ ] Failed objectives from source run generate guaranteed rival echoes
- [ ] Ghost bodies carry equipment matching player's historical loadout
- [ ] DerivedEquipmentTier correctly computed from gear snapshots
- [ ] Encounter type override: Death=Desperate, Extraction=Intel, Abandonment=Race
- [ ] Ghost-specific dialogue references run number and end reason
- [ ] Ghost shader material applied to NPC model (translucent + chromatic)
- [ ] Only 1 ghost per expedition (MaxGhostsPerExpedition cap)
- [ ] Current expedition seed excluded from candidate pool
- [ ] RivalSimulationSystem does not double-process ghosts
- [ ] Ghost Route Visualizer opens via `Hollowcore > Ghost Route Visualizer` menu
