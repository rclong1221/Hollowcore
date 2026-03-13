# EPIC 9: Compendium & Meta Progression

**Status**: Planning
**Priority**: Medium — Retention loop
**Dependencies**: Framework: Roguelite/ (MetaUnlockTreeSO, MetaBank), Persistence/; EPIC 4 (Districts)
**GDD Sections**: 10.1-10.2 The Compendium

---

## Problem

Players need progression that survives expedition wipes. The Compendium serves two roles: run-level tactical resources (Pages) consumed during play, and permanent meta-progression (Entries) that unlock new content across all future expeditions. The framework's MetaUnlockTreeSO handles the permanent unlock tree; this epic adds the run-consumable Pages and the Hollowcore-specific unlock content.

---

## Overview

The Compendium is both a consumable toolkit and a permanent encyclopedia. Pages are earned during runs, slotted into modifier slots, and consumed on use. Entries are earned at extraction and permanently unlock new mission types, vendors, traversal options, and lore. Together they create a "play more, unlock more variety" retention loop.

---

## Sub-Epics

### 9.1: Compendium Pages (Run Consumables)
Tactical modifiers consumed during play.

- **CompendiumPageDefinitionSO**: PageId, PageType, DisplayName, Description, Icon, Effect
- **Page types** (GDD §10.1):
  - **Scout**: reveal gates, Front patterns, hidden POIs, echo locations
  - **Suppression**: slow Front advance, weaken specific threat factions
  - **Insight**: reveal boss weaknesses, echo mutation types, reward contents
- **Page inventory**: player carries N pages (4-6 active slots)
- **Page use**: consume from slot → immediate effect for current district or gate screen
- **Page sources**: district exploration, quest rewards, echo rewards, vendor purchases
- **CompendiumPageSystem**: manages page inventory, consumption, and effect application
- **Maps to**: Framework RunModifierStack for effect application, but pages are single-use

### 9.2: Compendium Entries (Permanent Unlocks)
Meta-progression knowledge base.

- **CompendiumEntryDefinitionSO**: EntryId, Category, UnlockCondition, UnlockReward
- **Entry categories** (GDD §10.2):
  - New mission types and events (unlocks quest pool entries)
  - New services and vendors (unlocks NPC spawns in districts)
  - New traversal options (unlocks movement abilities or zone shortcuts)
  - Lore with mechanical hints (flavor text with embedded gameplay tips)
- **Unlock conditions**: complete specific districts, defeat specific bosses, complete echoes, reach Trace thresholds, find hidden content
- **CompendiumEntryPersistence**: permanent save data — survives wipes
- **Maps to**: Framework MetaUnlockTreeSO pattern exactly — costs replaced by unlock conditions
- **CompendiumEntrySystem**: checks unlock conditions at extraction → awards new entries

### 9.3: Compendium UI
The player's growing encyclopedia.

- **Compendium screen**: accessible from menu, shows all discovered entries
  - Categories: Districts, Enemies, Bosses, Factions, Lore, Mechanics
  - Undiscovered entries shown as locked silhouettes (incentivizes exploration)
- **Page management UI**: view, slot, and track available pages
- **Extraction summary**: new entries highlighted at end-of-district screen
- **Completion percentage**: per-district and total completion tracking

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (MetaUnlockTreeSO) | Compendium entries ARE meta unlocks |
| Roguelite/ (MetaBank) | Page inventory and entry state stored in MetaBank |
| Roguelite/ (RunModifierStack) | Page effects applied as temporary run modifiers |
| Persistence/ (ISaveModule) | Entries persist permanently, pages persist within expedition |
| Quest/ | Entry unlock conditions reference quest completions |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 9.1 (Pages) | None | EPIC 3 (suppression pages), EPIC 6 (scout at gate) |
| 9.2 (Entries) | EPIC 4 (extraction triggers) | All content epics (unlock conditions) |
| 9.3 (UI) | 9.1, 9.2 | — |

---

## Vertical Slice Scope

- 9.1 (pages) basic Scout + Suppression types for vertical slice
- 9.2 (entries) at least district-completion entries
- 9.3 (UI) basic page slot UI, entry list deferred

---

## Tooling & Quality

| Sub-Epic | BlobAsset Pipeline | Validation | Editor Tooling | Live Tuning | Debug Visualization | Simulation |
|---|---|---|---|---|---|---|
| 9.1 (Pages) | CompendiumPageBlob + CompendiumPageDatabase (BlobArray, Burst-safe page lookup) | PageId uniqueness, type-specific flag checks, ModifierKey non-empty, icon not null | Page Editor module in Compendium Workstation (card grid, rich text preview, effect preview) | MaxActiveSlots, MaxTotalPages, duration multiplier, grant/clear pages | Active page effects with duration countdown, page inventory display | — |
| 9.2 (Entries) | CompendiumEntryBlob + CompendiumEntryDatabase (BlobArray, Burst-safe condition eval) | EntryId uniqueness, cross-reference validation (district/boss/quest IDs), category completeness, orphan detection | Entry Browser module (tree view, inline editing), Completion Tracker module, Validation module | Unlock/lock all, unlock by ID, bypass conditions, simulate extraction | Completion summary bars, recently-unlocked highlight, unlock condition progress | 50-expedition completion curve, per-category progression, stall detection |
| 9.3 (UI) | — | — | Compendium Workstation: Book Layout module (visual page layout, drag-drop reorder, locked/unlocked preview) | — | Compendium debug overlay (completion %, new flag count, page slots) | — |
