# EPIC 11.3 Setup Guide: Live Rival Encounters

**Status:** Planned
**Requires:** EPIC 11.1 (Rival Definition & Simulation), Framework AI/, Dialogue/, Trading/; Optional: EPIC 8 (Trace), EPIC 10 (trade rewards)

---

## Overview

When the player enters a district containing a living rival team, there is a probability-based chance of a live encounter. Encounters fall into three tiers: Neutral (trade, intel, body shop), Competitive (race, territory, loot conflict), and Hostile (contracted hunts, desperate attacks). The encounter type is determined by rival personality, player Trace level, and context. Dialogue uses the Dialogue/ framework; combat uses the AI/ framework.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 11.1 | RivalSimState, RivalOperatorSO | Rival data and simulation state |
| Framework `AI/` | AIBrain, AIState | Combat AI for hostile encounters |
| Framework `Dialogue/` | DialogueStartRequest | Conversation system for trade/negotiation |
| Framework `Trading/` | Trade UI | Item exchange for trade encounters |
| EPIC 8 (optional) | Trace system | Trace level triggers hostile encounters |

### New Setup Required
- 1 `RivalEncounterConfigAuthoring` singleton in subscene
- Rival NPC prefabs per BuildStyle (for live encounters)
- Dialogue trees per rival (neutral + hostile)
- Assembly references to `Hollowcore.Dialogue`, `Hollowcore.Trading`, `Hollowcore.AI`

---

## 1. Create the RivalEncounterConfig Singleton
**Create:** Add `RivalEncounterConfigAuthoring` MonoBehaviour to the expedition manager prefab.

### 1.1 Configuration Fields
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| BaseEncounterChance | Base probability when rival is in same district | 0.4 | 0.0-1.0 |
| HostileTraceThreshold | Trace level for hostile encounters | 4 | 1-5 |
| HostileChancePerTrace | Per-Trace-level hostile probability bonus | 0.15 | 0.0-0.5 |
| DesperateThreshold | Member count below which rival becomes Desperate | 1 | 1-4 |
| FleeThreshold | Surviving ratio at which rivals retreat | 0.3 | 0.0-1.0 |
| EncounterTimeout | Max seconds before unresolved encounter ends | 120.0 | 30-300 |

**Tuning tip:** BaseEncounterChance of 0.4 means roughly 1 encounter per 2-3 districts when a rival is present. With 2 rivals alive, the chance of encountering at least one rises to ~0.64 per district.

**Tuning tip:** HostileTraceThreshold at 4 means hostile encounters are a late-game escalation. Players at Trace 1-3 will only see neutral/competitive encounters. This keeps hostility feeling like a consequence of reckless play, not random bad luck.

---

## 2. Create Rival NPC Prefabs
**Recommended location:** `Assets/Prefabs/Rivals/`

Create one prefab per BuildStyle for live encounter spawning.

### 2.1 Prefab List
| Prefab | BuildStyle | Visual Notes |
|--------|-----------|--------------|
| `RivalNPC_Heavy.prefab` | Heavy | Bulky armor, large weapons |
| `RivalNPC_Stealth.prefab` | Stealth | Slim, hooded, suppressed weapons |
| `RivalNPC_Balanced.prefab` | Balanced | Medium gear, versatile loadout |
| `RivalNPC_Specialist.prefab` | Specialist | Distinctive specialist equipment |

### 2.2 Required Components Per Prefab
| Component | Purpose |
|-----------|---------|
| AIBrain (Framework) | Behavior tree root for combat |
| AIState (Framework) | AI state machine |
| Health / DamageableAuthoring | Can take damage in combat encounters |
| PhysicsShapeAuthoring | Collision for combat |
| Visual mesh + animations | NPC appearance |
| GhostComponent | NetCode replication for multiplayer |

**Tuning tip:** Rival NPC combat stats should scale with EquipmentTier from their RivalOperatorSO. Tier 1 rivals fight like weak enemies; Tier 5 rivals are mini-boss difficulty. The `CombatBehaviorId` on the SO maps to specific AI behavior profiles.

---

## 3. Create Dialogue Trees
**Recommended location:** `Assets/Data/Dialogue/Rivals/`

### 3.1 Neutral Dialogue Tree (Per Rival)
Each rival needs a neutral dialogue tree with these branches:

| Branch | Outcome | Description |
|--------|---------|-------------|
| Greet | — | Opening line reflecting personality |
| Trade | Opens Trading/ UI | "I've got some gear to move. Interested?" |
| Intel | Reveals district info on Scar Map | "Let me tell you what I've seen out there." |
| BodyShop | Opens revival services | "Our medic can patch you up... for a price." |
| Refuse | Rival leaves peacefully | "Suit yourself. See you around." |
| Attack | Transitions to Combat phase | Player-initiated hostility during dialogue |

**File naming:** `Neutral_[TeamName].asset` (e.g., `Neutral_RustHammers.asset`)

### 3.2 Hostile Dialogue Tree (Per Rival)
Short threat/demand dialogue before combat begins:

| Branch | Outcome | Description |
|--------|---------|-------------|
| Threat | Combat begins | "Someone's paying well for your head." |
| Demand | Pay-to-avoid option | "Hand over your gear and walk away." |
| Surrender | Player gives items, rival leaves | Only available if player has demanded items |

**File naming:** `Hostile_[TeamName].asset`

### 3.3 Dialogue ID Assignment
Set the dialogue tree IDs on each RivalOperatorSO:

| SO Field | Value |
|----------|-------|
| NeutralDialogueId | ID from neutral dialogue tree asset |
| HostileDialogueId | ID from hostile dialogue tree asset |

---

## 4. Encounter Type Resolution

The encounter type is resolved in priority order:

### 4.1 Hostile Check (First Priority)
| Condition | Encounter Type |
|-----------|---------------|
| Trace >= HostileTraceThreshold | Contracted |
| SurvivingMembers <= DesperateThreshold | Desperate |

### 4.2 Competitive Check (Second Priority)
| Condition | Encounter Type |
|-----------|---------------|
| Both teams in same zone with active objective | Race |
| Rival occupying vendor or safe zone | Territory |
| Rival has looted player's previous body | LootConflict |

### 4.3 Neutral Default (Fallback)
| Personality | Encounter Type |
|-------------|---------------|
| Mercantile | Always Trade |
| Others | Weighted: Trade(40%), Intel(40%), BodyShop(20%) |

### 4.4 Probability Modifiers
| Modifier | Effect |
|----------|--------|
| Per-district overlap | +0.10 per district both have visited |
| Aggressive personality | +0.15 to base chance |
| Cautious personality | -0.10 from base chance |

---

## 5. Encounter Phase Flow

```
District Entry
  |
  v
[Probability Roll] -- fails --> No encounter
  |
  success
  v
Phase: Approach
  |
  +-- Neutral/Competitive --> Phase: Dialogue
  |                            |
  |                            +-- Trade --> Trading/ UI
  |                            +-- Intel --> Scar Map update
  |                            +-- BodyShop --> Revival menu
  |                            +-- Refuse --> Phase: Resolution
  |                            +-- Attack --> Phase: Combat
  |
  +-- Hostile -----------------> Phase: Combat
                                   |
                                   +-- Player wins --> Loot + RivalSimState update
                                   +-- Rivals flee --> Rival moves to adjacent district
                                   +-- Timeout --> Disengage, no loot
                                   |
                                   v
                              Phase: Resolution
                                   |
                                   v
                              Phase: Complete (cleanup)
```

---

## 6. Trade Offer Generation

For Trade encounters, `RivalTradeOffer` buffer is populated based on rival equipment:

| EquipmentTier | Offered Items |
|---------------|---------------|
| 1-2 | Common ammo, basic healing, cheap currency |
| 3 | Uncommon weapons, useful consumables |
| 4-5 | Rare augments, limb salvage, valuable currency |

**Tuning tip:** Trade offers should feel like genuine opportunities, not charity. The rival's personality affects pricing: Mercantile offers fair prices, Aggressive demands premium, Desperate offers discounts.

---

## 7. Scene & Subscene Checklist

- [ ] `RivalEncounterConfigAuthoring` on expedition manager prefab
- [ ] Rival NPC prefabs exist for all 4 BuildStyles
- [ ] Each NPC prefab has AI, Health, Physics, and Visual components
- [ ] Neutral dialogue trees created for each rival in the pool
- [ ] Hostile dialogue trees created for each rival in the pool
- [ ] Dialogue IDs assigned on RivalOperatorSO assets
- [ ] CombatBehaviorId assigned on RivalOperatorSO assets
- [ ] Assembly references include Dialogue, Trading, AI

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| BaseEncounterChance too high (> 0.7) | Encounters every district; feels forced | Keep at 0.3-0.5 for ~1 per 2-3 districts |
| HostileTraceThreshold too low (1-2) | Hostile encounters from the start; frustrating | Keep at 4+ so hostility is a late consequence |
| Missing dialogue tree for a rival | Null reference during Dialogue phase; encounter hangs | Every rival in the pool needs both neutral and hostile trees |
| Missing combat behavior for a rival | Hostile encounters spawn NPCs with no AI; they stand still | Set CombatBehaviorId on every RivalOperatorSO |
| Multiple active encounters | Race condition; singleton pattern violated | RivalEncounterState is a singleton -- only one at a time |
| Not cleaning up NPC entities after encounter | Rival NPCs persist in world after encounter resolves | RivalEncounterResolutionSystem must destroy spawned entities |
| FleeThreshold too high (> 0.5) | Rivals flee before any meaningful combat | Keep at 0.2-0.3 for enough combat before retreat |
| EncounterTimeout too short (< 60s) | Encounters cut off mid-dialogue or mid-fight | Keep at 120s minimum |

---

## Verification

- [ ] Encounter triggers with correct probability on district entry
- [ ] Encounter probability increases with rival district overlap and Aggressive personality
- [ ] Hostile encounters only trigger at Trace >= HostileTraceThreshold (4)
- [ ] Desperate encounters trigger when rival SurvivingMembers <= DesperateThreshold
- [ ] Neutral encounters open dialogue via Dialogue/ framework
- [ ] Trade encounters populate RivalTradeOffer buffer and open trade UI
- [ ] Trade offers scale with rival EquipmentTier
- [ ] Intel encounters reveal rival district history on Scar Map (EPIC 12)
- [ ] BodyShop encounters open revival services menu
- [ ] Combat encounters spawn rival NPCs with correct AI behavior
- [ ] Rival NPCs flee when surviving ratio < FleeThreshold
- [ ] Combat victory produces loot and updates RivalSimState
- [ ] Encounter timeout resolves after EncounterTimeout seconds
- [ ] Only one encounter active at a time (singleton)
- [ ] Spawned NPC entities cleaned up on completion
- [ ] Player-initiated attack during dialogue transitions to Combat phase
- [ ] RivalEncounterCompletedEvent fires for analytics
