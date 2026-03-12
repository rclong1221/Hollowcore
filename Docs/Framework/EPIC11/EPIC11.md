# Epic 11: DIG.Items System
**(Universal Inventory & Item Framework)**

**Status**: Planning  
**Priority**: CRITICAL  
**Version**: 2.0 (Asset Store Ready)

## Overview

**DIG.Items** is a generic, high-performance Inventory and Item system for Unity DOTS. It is designed to be the "Inventory Standard" for ECS-based games.

**Product Pillars:**
1.  **Type-Agnostic**: Can handle ANY item type (Weapons, Resources, Armor) via a flexible Property system.
2.  **Networked ECS**: Built on `NetCode for Entities`. Fully server-authoritative.
3.  **UI Decoupled**: The backend (Inventory Buffers) knows nothing about the frontend (Canvas).
4.  **Designer Friendly**: Includes a Custom Editor Window for managing the Item Database without touching JSON or code.

## Objectives
1.  **Universal Database**: A ScriptableObject-based registry (`ItemDatabase.asset`) that can be loaded/unloaded.
2.  **Runtime Inventory**: `DynamicBuffer<InventorySlot>` attached to Entities.
3.  **Hotbar / Equipment**: Generic "Slot Groups" concept (e.g., "Hand", "Head", "Belt").
4.  **Loot Integration**: Easy API for other systems (like DIG.Voxel) to request drops.

## Sub-Epics
- **[EPIC 11.1: Item Database & Definitions](EPIC11.1.md)**: The "Static" data layer. ScriptableObjects and Registry.
- **[EPIC 11.2: Inventory Backend (ECS)](EPIC11.2.md)**: The "Runtime" data layer. Buffers, Network Sync, Pickup/Drop logic.
- **[EPIC 11.3: UI & Hotbar](EPIC11.3.md)**: The Visual layer. Drag-and-drop, Hotbar selection.
- **[EPIC 11.4: Loot Tables & Spawning](EPIC11.4.md)**: Connecting Voxel destruction to Item Drops.

## Definition of Done
- [ ] Designer can create `NewItem.asset`, assign icon/mesh, and see it in game.
- [ ] Player can mine a voxel -> see item drop -> pick it up -> see it in UI.
- [ ] Player can drag item to Hotbar -> Equip it (if tool).
- [ ] Inventory state replicates correctly to server and persists (session-based).
