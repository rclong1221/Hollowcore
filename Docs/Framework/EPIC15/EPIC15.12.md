# EPIC 15.12: New Player Experience (NPE)

## Goal
To implement the "First User Experience" systems. A professional game needs more than mechanics; it needs a way to teach them (Tutorials) and a reason to use them (Quests/Objectives).

---

## 1. Objective System (Quests)
*   **Status:** **MISSING**. No quest logic found.
*   **Requirement:** Track player progress (e.g., "Collect 10 Wood", "Go to Location X").
*   **Architecture:**
    *   **Components:** `ObjectiveComponent`, `QuestState`.
    *   **Data:** `QuestDefinition` (ScriptableObject) describing steps.
    *   **UI:** `QuestHUD` to show active objectives.
    *   **Events:** `ObjectiveEventSystem` listening for gameplay actions (Kill, Collect, Travel).

## 2. Tutorialization
*   **Status:** **MISSING**. No onboarding flow.
*   **Requirement:** Context-sensitive help popups.
*   **System:** `TutorialManager`
    *   **Triggers:**
        *   *First Time Pickup:* Pause -> Show Item Info.
        *   *First Time Dark:* Prompt "Press F for Flashlight".
        *   *First Time Low Health:* Prompt "Use Medkit".
    *   **Persistence:** Save `TutorialFlags` so veterans don't see prompts twice.

## 3. Codex / Encyclopedia
*   **Status:** **MISSING**.
*   **Goal:** A database of discovered items/creatures.
*   **Implementation:** UI Window populated by `CodexDefinition` assets, unlocked via valid scans or pickups.

---

## Implementation Tasks
- [ ] Implement `QuestManager` (ECS System for tracking progress).
- [ ] Create `TutorialTriggerSystem` (Context-aware prompts).
- [ ] detailed design for `ObjectiveDefinition` ScriptableObjects.
