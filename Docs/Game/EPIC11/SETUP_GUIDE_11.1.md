# EPIC 11.1 Setup Guide: Rival Definition

**Status:** Planned
**Requires:** Framework: AI/, Loot/; EPIC 4 (Districts, graph structure), EPIC 3 (Front system)

---

## Overview

Rival operator teams are ScriptableObject-driven definitions with team composition, build preferences, risk tolerance, and personality. A lightweight simulation system advances rival positions through the district graph on each gate transition, resolving outcomes probabilistically. Rivals exist as Alive (exploring), Dead (bodies as loot), or Extracted (left expedition).

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 4 | District graph | Rival pathing |
| EPIC 3 | Front system | Risk calculation |
| Framework AI/ | Behavior profiles | Combat encounter AI |
| Framework Loot/ | LootTableSO | Body loot |

### New Setup Required
1. Create `Assets/Scripts/Rivals/` with Components/, Definitions/, Systems/, Authoring/, Blobs/, Debug/
2. Create `Hollowcore.Rivals.asmdef` referencing DIG.Shared, Unity.Entities, Unity.Collections, Unity.Mathematics, Hollowcore.Districts
3. Author 4-6 `RivalOperatorSO` assets covering each BuildStyle
4. Create `RivalPoolSO` referencing available rivals
5. Place `RivalDatabaseAuthoring` on expedition manager prefab

---

## 1. RivalOperatorSO
**Create:** `Assets > Create > Hollowcore/Rivals/Rival Operator`
**Recommended location:** `Assets/Data/Rivals/`

### 1.1 Identity
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| RivalId | Unique ID | — | >= 0 |
| TeamName | Display name | — | non-empty |
| Description | Lore text | — | TextArea |
| TeamIcon | Emblem sprite | — | required |

### 1.2 Composition
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| MemberCount | Team size | 3 | 1-4 |
| BuildStyle | Heavy/Stealth/Balanced/Specialist | Balanced | enum |

### 1.3 Behavior
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| PreferredDistricts | District IDs team prefers | — | List<int> |
| RiskTolerance | 0=conservative, 1=reckless | 0.5 | 0.0-1.0 |
| Personality | Aggressive/Cautious/Mercantile/Desperate/Professional | Professional | enum |

### 1.4 Equipment
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| EquipmentTier | Loot quality and combat power | 2 | 1-5 |
| EquippedLimbIds | Limb defs for body loot | — | List<int> |

### 1.5 Simulation Tuning
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| BaseSurvivalRate | Survive a district traversal | 0.85 | 0.0-1.0 |
| AlarmTriggerRate | Advance the Front per district | 0.15 | 0.0-1.0 |
| LootRate | Loot POIs per district | 0.4 | 0.0-1.0 |
| TargetExpeditionDepth | Districts before extraction | 5 | 1-10 |

### 1.6 Encounter
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| NeutralDialogueId | Dialogue tree for peace | — | int > 0 |
| HostileDialogueId | Dialogue tree for combat | — | int > 0 |
| CombatBehaviorId | AI behavior profile | — | int > 0 |

**Tuning tip:** RiskTolerance strongly affects lifespan. 0.8+ = dive deep, die fast. 0.2 = safe, often extracts.

---

## 2. RivalPoolSO
**Create:** `Assets > Create > Hollowcore/Rivals/Rival Pool`
**Recommended location:** `Assets/Data/Rivals/DefaultRivalPool.asset`

| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| AvailableRivals | All RivalOperatorSO refs | — | List |
| MinRivals | Min teams per expedition | 1 | 1-4 |
| MaxRivals | Max teams per expedition | 3 | 1-4 |

---

## 3. Suggested Starting Rivals

| Name | BuildStyle | Personality | RiskTolerance | Tier | Survival |
|------|-----------|-------------|---------------|------|----------|
| Chrome Wolves | Heavy | Aggressive | 0.8 | 3 | 0.90 |
| Ghost Circuit | Stealth | Cautious | 0.2 | 2 | 0.80 |
| Freeport Crew | Balanced | Mercantile | 0.5 | 2 | 0.85 |
| The Expendables | Specialist | Desperate | 0.9 | 1 | 0.70 |
| Meridian Solutions | Balanced | Professional | 0.4 | 4 | 0.92 |

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| Expedition manager prefab | RivalDatabaseAuthoring | References DefaultRivalPool |
| Expedition singleton entity | RivalTeamEntry buffer | Populated by RivalSpawnSystem |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Duplicate RivalId | Build validator error | Unique IDs |
| BaseSurvivalRate <= 0 | Instant death | Keep > 0 |
| Empty PreferredDistricts | Random pathing | Populate for predictability |
| Missing CombatBehaviorId | Default AI in combat | Assign behavior ID |
| MemberCount outside 1-4 | Simulation anomalies | Clamp to range |

---

## Verification
- [ ] RivalOperatorSO assets for all 4 BuildStyle variants
- [ ] RivalPoolSO references available rivals
- [ ] RivalSpawnSystem creates 1-3 entities per expedition (seed-deterministic)
- [ ] Each entity has RivalSimState + RivalOutcomeEntry buffer
- [ ] Simulation advances on gate transition
- [ ] PreferredDistricts weighting respected
- [ ] High-Front districts avoided at low RiskTolerance
- [ ] State=Dead when SurvivingMembers=0
- [ ] Same seed = identical behavior across runs
