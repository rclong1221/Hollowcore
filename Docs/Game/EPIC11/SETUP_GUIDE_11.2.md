# EPIC 11.2 Setup Guide: Rival Trail Markers

**Status:** Planned
**Requires:** EPIC 11.1 (Rival Definition & Simulation), EPIC 4 (Districts), EPIC 2 (Death/body persistence), EPIC 3 (Front system)

---

## Overview

Trail markers are the physical evidence rivals leave in the world. When the player enters a district, the `TrailMarkerSystem` checks rival simulation history and stamps markers: lootable bodies, cleared enemy paths, already-looted containers, rival-spawned echoes, and cosmetic trail signs. Rivals also advance the Front via alarm triggers. This is the primary way players perceive rival activity without direct encounters.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 11.1 | RivalSimState, RivalOutcomeEntry | Rival history data to convert to markers |
| EPIC 4 | District entities, zone system | Zone positions for marker placement |
| EPIC 2 | Body persistence pattern | Rival bodies follow same loot pattern |
| EPIC 3 | FrontSystem | Alarm markers advance Front phase |
| Framework `Loot/` | Loot table system | Generates loot on rival bodies |

### New Setup Required
- 1 `TrailMarkerConfigAuthoring` singleton in subscene
- 1 `RivalBody.prefab` with interactable + loot components
- 3-4 trail sign prefab variants (cosmetic props)
- Assembly references to `Hollowcore.Front`, `Hollowcore.Loot`

---

## 1. Create the TrailMarkerConfig Singleton
**Create:** Add `TrailMarkerConfigAuthoring` MonoBehaviour to the expedition manager prefab or subscene.

### 1.1 Configuration Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| MaxBodiesPerDistrict | Max rival corpses placed per district | 3 | 0-10 |
| ClearedPathEnemyReduction | Percentage of enemies removed in cleared zones | 0.3 | 0.0-1.0 |
| EchoSpawnChance | Probability that a rival failure generates an echo | 0.25 | 0.0-1.0 |
| MaxTrailSignsPerDistrict | Cosmetic trail sign cap | 5 | 0-20 |
| FrontAdvancePerAlarm | Front phases advanced per alarm trigger | 1 | 0-3 |

**Tuning tip:** ClearedPathEnemyReduction at 0.3 means 30% fewer enemies in zones a rival has cleared. This is enough to be noticeable ("something already came through here") without trivializing combat. Values above 0.5 risk making rival-cleared districts too easy.

**Tuning tip:** FrontAdvancePerAlarm at 1 means each rival alarm advances the Front by one phase. With the default AlarmTriggerRate of 0.15, expect 0-2 phases advanced per expedition by all rivals combined. Values above 2 can feel punishing since the player has no control over rival behavior.

---

## 2. Create the Rival Body Prefab
**Create:** `Assets/Prefabs/Rivals/RivalBody.prefab`

### 2.1 Required Components
| Component | Purpose |
|-----------|---------|
| RivalBodyLoot (IComponentData) | Tags as rival corpse with equipment tier and definition ID |
| Interactable (Framework) | Player can interact to loot |
| LootSource (Framework) | Generates drops based on EquipmentTier |
| PhysicsShapeAuthoring | Collision for interaction raycasts |
| Visual mesh | Dead NPC model (can share with EPIC 2 corpse models) |

### 2.2 RivalBodyLoot Fields (Set at Spawn)
| Field | Source | Description |
|-------|--------|-------------|
| RivalDefinitionId | From RivalOutcomeEntry | Links to RivalOperatorSO |
| EquipmentTier | From RivalOperatorSO | Determines loot quality (1-5) |
| RivalTeamName | From RivalSimState | Display name for UI |
| ZoneId | From marker placement | Scar Map positioning |
| IsLooted | Starts false | Set true when player loots |

**Tuning tip:** EquipmentTier directly controls loot quality. Tier 1 bodies drop Common gear; Tier 5 bodies drop Rare+ with chance of Epic. This means the "Scalpel Unit" (Tier 5) bodies are worth seeking out, while "The Brokers" (Tier 2) offer less exciting loot.

---

## 3. Create Trail Sign Prefab Variants
**Recommended location:** `Assets/Prefabs/Rivals/TrailSigns/`

Create 3-4 cosmetic prop prefabs:

| Variant | Prefab | Description |
|---------|--------|-------------|
| 0 | `TrailSign_SpentAmmo.prefab` | Scattered bullet casings and clips |
| 1 | `TrailSign_Campsite.prefab` | Abandoned campfire, sleeping bags |
| 2 | `TrailSign_Graffiti.prefab` | Team tag/symbol spraypainted on wall |
| 3 | `TrailSign_BloodTrail.prefab` | Blood smears indicating passage |

Each variant has a spawn weight in the `TrailSignDatabaseRef` blob for weighted random selection.

### 3.1 Trail Sign Blob Configuration
| Field | Description | Default |
|-------|-------------|---------|
| VariantIndex | Matches prefab array index | 0-3 |
| SpawnWeight | Weighted random selection | 1.0 each |
| MinDistFromPrevious | Minimum spacing between same-type signs | 5.0 units |

---

## 4. Marker Type Reference

| TrailMarkerType | Trigger Outcome | World Effect | Metadata |
|-----------------|-----------------|--------------|----------|
| Body | TeamWiped / LostMember | Spawns lootable rival corpse | EquipmentTier |
| ClearedPath | ClearedEnemies | Reduces enemy count in zone | Enemy reduction % (0-100) |
| LootedPOI | LootedPOIs | Containers pre-opened, vendor stock reduced | POI type ID |
| RivalEcho | Failed objective (probability) | Spawns rival echo encounter (EPIC 5) | Echo definition ID |
| TrailSign | Any traversal | Cosmetic prop along rival path | Sign variant index |
| AlarmTriggered | TriggeredAlarm | Advances Front phase in district | Phases advanced |

---

## 5. System Execution Order

```
DistrictEntrySystem (EPIC 4)
  |
  v
TrailMarkerSystem        -- Reads rival history, stamps marker entries
  |
  +-> TrailMarkerSpawnSystem    -- Converts markers to world entities
  |
  +-> TrailMarkerFrontImpactSystem  -- Advances Front from alarms
  |                                     (runs BEFORE FrontAdvanceSystem)
  v
FrontAdvanceSystem (EPIC 3)
```

---

## 6. Wire to Zone Generation

TrailMarkerSpawnSystem integrates with zone generation:

| Marker Type | Zone Integration |
|-------------|-----------------|
| Body | Spawn at seed-deterministic position within EventZoneId |
| ClearedPath | Set `EnemySpawnReduction` component on affected zones |
| LootedPOI | Set container entities to already-opened state |
| TrailSign | Spawn along rival traversal path between zones |
| AlarmTriggered | Write FrontAdvanceRequest for the district |

---

## 7. Scene & Subscene Checklist

- [ ] `TrailMarkerConfigAuthoring` exists on expedition manager or in subscene
- [ ] `RivalBody.prefab` exists with interactable + loot source + physics
- [ ] Trail sign prefab variants exist in `Assets/Prefabs/Rivals/TrailSigns/`
- [ ] Assembly references include `Hollowcore.Front` and `Hollowcore.Loot`
- [ ] TrailMarkerSystem runs after DistrictEntrySystem
- [ ] TrailMarkerFrontImpactSystem runs before FrontAdvanceSystem

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| MaxBodiesPerDistrict too high | Districts cluttered with corpses; feels artificial | Keep at 3-5; remember a 4-member team wipe adds up to 4 bodies |
| ClearedPathEnemyReduction > 0.5 | Rival-cleared districts feel empty; no combat tension | Keep at 0.3 or below |
| FrontAdvancePerAlarm > 2 | Players feel punished by rival actions they can't control | Keep at 1; it's a surprise, not a catastrophe |
| Missing MarkerHash deduplication | Re-entering a district duplicates all markers | TrailMarkerSystem must check MarkerHash before adding entries |
| Trail signs spawn outside zone geometry | Floating props, props inside walls | Validate position bounds during placement |
| RivalBody.prefab missing PhysicsShape | Player interaction raycasts miss the body | Add PhysicsShapeAuthoring (capsule or box) |
| Not wiring to FrontSystem | AlarmTriggered markers have no gameplay effect | TrailMarkerFrontImpactSystem must call FrontSystem advance |
| EchoSpawnChance too high | Every rival failure generates an echo; too many echoes | Keep at 0.25 -- expect ~0.5 rival echoes per expedition |

---

## Verification

- [ ] TrailMarkerEntry buffer populated on district entity when rival has visited
- [ ] Body markers spawn at correct zone positions (seed-deterministic)
- [ ] Rival body entities have RivalBodyLoot with correct tier and definition
- [ ] Bodies are interactable and produce loot matching EquipmentTier
- [ ] ClearedPath zones have reduced enemy count (30% default)
- [ ] LootedPOI containers show as already-opened
- [ ] Trail signs spawn as cosmetic props along rival traversal path
- [ ] Alarm-triggered districts have advanced Front phase on entry
- [ ] Front bleed propagates from rival-alarmed districts to adjacent
- [ ] Markers are deduplicated on district re-entry (no duplicate bodies)
- [ ] RivalEcho markers spawn with configured probability (~25%)
- [ ] No markers generated for districts with no rival history
- [ ] Trail sign variants selected by weighted random from seed
- [ ] MinDistFromPrevious spacing enforced between same-type signs
- [ ] Scar Map receives marker events (EPIC 12.1 integration)
