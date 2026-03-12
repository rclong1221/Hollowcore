# EPIC 17.7: Achievement System — Setup Guide

## Overview

The Achievement System is a data-driven, server-authoritative framework that tracks player milestones across combat, exploration, crafting, social, collection, progression, and challenge categories. It listens to existing game events (like kills, quests, crafting, loot drops, etc.) to increment player progress.

For designers and developers, the system offers an **Achievement Workstation** (with built-in validation), a robust component workflow, and pre-built UI components for toast notifications and an extensive achievement panel.

---

## 1. Quick Start: Initial Initialization

If setting up the system for the first time in a new project:

1. **Create the Database:**
   - Right-click in the Project window and go to **Create → DIG → Achievement → Achievement Database**.
   - Name it `AchievementDatabase` and place it in a `Resources` folder (e.g., `Assets/Resources/AchievementDatabase.asset`).
2. **Create the Config:**
   - Right-click and go to **Create → DIG → Achievement → Config**.
   - Name it `AchievementConfig` and place it in the same `Resources` folder (`Assets/Resources/AchievementConfig.asset`).
     - *Note: You can tweak global settings like MaxTrackedAchievements, ToastDisplayDuration, and EnableHiddenAchievements here.*

---

## 2. Setting Up the Player Prefab

To track progress for a player, the achievement components need to be added to their authoring equivalent.

1. Open your player prefab (e.g., `Warrok_Server`).
2. Attach the **DIG / Achievement / Player Achievement** component (`AchievementAuthoring`).
   - *Note: This adds only an 8-byte `AchievementLink` component to the player at runtime. All actual achievement data buffers and overall milestone stats are securely stored on a child entity created automatically by the baker to minimize archetype size impact.*

---

## 3. UI Setup

The system provides UI hookups via a static bridge (`AchievementUIRegistry`). To get toasts and the achievement panel working:

### Toast Notifications (Popups)
1. Add a UI element for toasts (e.g., sliding notification prefab) to your HUD canvas.
2. Attach the **Achievement Toast View** component (`AchievementToastView`).
3. Wire the required references in the inspector (Icon Image, Name Text, Tier Text, Reward Text).
4. The system automatically queues and plays toast animations as achievements unlock.

### Full Achievement Panel
1. Create a full-screen or tabbed panel in your UI canvas.
2. Attach the **Achievement Panel View** component (`AchievementPanelView`).
3. Wire the references for category tabs and the container for the achievement grid cards.
4. The view will automatically populate and filter unlocked/hidden achievements and display completion percentages based on the player's connection data.

---

## 4. Defining Achievements (Editor Tooling)

Use the **Achievement Workstation** for a safe and robust workflow.

1. Open **DIG → Achievement Workstation** from the top menu.
2. The workstation contains 4 primary modules:

### Definition Editor (Creating Achievements)
- Select the **Definition Editor** tab.
- Click **Create New** or use batch generation for multi-tier achievements (Bronze, Silver, Gold, Platinum).
- Define:
  - **Category**: Combat, Exploration, Crafting, Progression, etc.
  - **Condition Type**: (e.g., `EnemyKill`, `ItemCrafted`).
  - **Condition Param**: If using a specific type (e.g., `EnemyKillByType`), provide the Enemy ID here as the parameter.
  - **Hidden toggle**: Hide from the panel until unlocked.
  - **Tiers & Rewards**: Set thresholds (e.g., 100 kills) and rewards (e.g., `Gold`, `XP`, `TalentPoints`). *Note: Rewards are automatically distributed by the system upon unlock cross-referencing your configured TypeId.*

### Validator (MUST USE BEFORE BUILD)
- Run the **Validator Module** before building a release to catch duplicate IDs, missing tiers, zero thresholds, or orphan definitions. 
- Use the "Fix All" button for trivial repairs.

### Progress Inspector
- *Play-mode only.*
- Select a local player entity to view live progress of all achievement buffers in real-time. 
- Useful for debugging increments or forcing "Unlock Achievement" and testing rewards.

### Statistics Manager
- The **Statistics Module** shows aggregate data across your database (e.g., average completion rate, category spread).

---

## 5. Save Integration

Your achievement data is automatically handled!
- The system automatically hooks into `TypeId = 14`.
- The `AchievementSaveModule` correctly serializes progress buffers and cumulative stats (kills, deaths, largest kill streak/consecutive logins). It will skip data if progress hasn't changed.
