# Hollowcore

**Hollowcore** is a single-player or co-op action roguelite built on Unity's Data-Oriented Tech Stack (DOTS). Players are modular cybernetic operatives exploring interconnected, persistent districts in expeditions of 5-7 maps. The project uses ECS (Entities 1.2), NetCode for Entities, and a modular assembly architecture for high-performance gameplay at scale.

---

## Core Gameplay

- **Modular Chassis:** Players swap limbs, weapons, and subsystems mid-run. Limbs carry memory of past encounters and can be stolen from enemies.
- **Living Graph Expeditions:** Districts form an interconnected graph, not a linear chain. Previous maps evolve as the run progresses — death seeds dangerous content in cleared zones.
- **Scar Map:** Procedurally generated terrain remembers player actions via deterministic seeds. Destruction, deaths, and events leave persistent marks.
- **Strife System:** 12 escalating difficulty cards that stack modifiers across the run.
- **Rival Operators:** AI-driven enemy operatives that compete for the same objectives.

---

## Tech Stack

- **Unity 2022.3 LTS** — Entities 1.2 / NetCode 1.2
- **Pure DOTS Gameplay:** All movement, combat, AI, and progression systems are data-oriented
- **NetCode for Entities:** Client prediction, server reconciliation, ghost replication
- **Modular Assemblies:** `DIG.Shared`, `DIG.Roguelite`, `DIG.Voxel`, `DIG.Weather`, `DIG.UI`, `DIG.Interaction`, `DIG.Localization`, `DIG.Survival` — each independently removable
- **Burst-compiled** systems throughout for predictable performance

> **Note:** Assembly names still use the `DIG.*` prefix from the original framework. These are internal and won't be renamed.

---

## Repository Layout

```
Assets/
 ├─ Scripts/
 │   ├─ Shared/              # Core framework (DIG.Shared assembly)
 │   ├─ Roguelite/           # Run lifecycle, meta-progression, rewards
 │   ├─ Player/              # Input, movement, camera, combat
 │   ├─ Combat/              # Damage pipelines, knockback, VFX events
 │   ├─ Aggro/               # Threat, alert states, social aggro
 │   ├─ Progression/         # XP, leveling, stat allocation
 │   ├─ Loot/                # Loot tables, drop systems
 │   ├─ Items/               # Equipment, inventory
 │   ├─ Economy/             # Currency, transactions
 │   ├─ Crafting/            # Crafting recipes, stations
 │   ├─ Quest/               # Quest definitions, tracking
 │   ├─ Dialogue/            # Dialogue trees, bridges
 │   ├─ Voxel/               # Procedural terrain generation
 │   ├─ Weather/             # Weather systems
 │   ├─ VFX/                 # VFX event pipeline, shaders
 │   ├─ Surface/             # Surface material gameplay
 │   ├─ UI/                  # UI framework
 │   ├─ Interaction/         # Interaction systems
 │   └─ Localization/        # Localization framework
 ├─ Editor/                  # Workstation editors (AI, VFX, Dialogue, etc.)
 ├─ Prefabs/                 # Player, enemy, environment prefabs
 └─ Scenes/                  # Gameplay and test scenes
Docs/
 ├─ Game Design Document.docx  # Full game design document
 ├─ Framework/                 # EPIC 1-23 framework documentation
 └─ *.md                       # Technical docs (audio, logging, strategy)
ProjectSettings/
Packages/
```

---

## Key Systems

| System | Description |
|--------|-------------|
| **Run Lifecycle** | State machine driving roguelite loop (Lobby → Zone → Boss → Meta) |
| **Meta-Progression** | Persistent unlocks, currency, and run history across sessions |
| **Zone Generation** | Interface-based (`IZoneProvider`) — games plug in their own level tech |
| **Modifiers & Difficulty** | Stackable run modifiers, ascension tiers, dynamic difficulty scaling |
| **Rewards & Choices** | Zone-clear rewards, shops, risk-reward events |
| **Combat Resolution** | Dual pipeline: low-level `DamageEvent` + high-level `CombatResultEvent` |
| **Aggro & Threat** | 5-level alert states, social aggro, threat scoring, target selection |
| **Save/Load** | Modular `ISaveModule` pattern, binary format with CRC32 |

---

## Quick Start

1. Install **Unity 2022.3 LTS** via Unity Hub
2. Clone the repo and open the project
3. Load a scene from `Assets/Scenes/`
4. Play mode controls: WASD movement, mouse aim, Shift sprint, Space jump

---

## Development

- **Branching:** Feature branches (`feature/<name>`) off `main`
- **Coding:** Small struct components, Burst-compiled systems, no managed allocations in `OnUpdate`
- **Testing:** DOTS playmode tests, NetCode client/server testing
- **Editor Tools:** Workstation windows under the DIG menu (AI, VFX, Dialogue, Quest, Run Config, etc.)

---

## License

Copyright. All rights reserved. Redistribution or commercial use requires permission.
