# Epic 13.2 Ability System - Setup Guide

> **Version:** 1.0.0  
> **Last Updated:** 2025-12-28  
> **Status:** IMPLEMENTED

This guide explains how to set up and use the new **Ability System Architecture** introduced in Epic 13.2. This modular system replaces hardcoded state machine logic with a priority-based, data-driven entity component system (ECS).

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Ability System Concepts](#ability-system-concepts)
3. [Component Reference](#component-reference)
4. [Developer Guide](#developer-guide)
5. [Troubleshooting](#troubleshooting)

---

## Quick Start

### 1. Add Ability Support to Player

1.  **Select** your player prefab (e.g., `Assets/Prefabs/Warrok_Server.prefab`).
2.  **Add Component** → `DIG.Player.Authoring.Abilities.AbilityAuthoring`.
3.  This component handles baking:
    *   Adds `AbilityState` (tracks active ability).
    *   Adds `AbilitySystemTag`.
    *   Creates the `AbilityDefinition` buffer from your list.

### 2. Configure Basic Abilities

In the `AbilityAuthoring` inspector, find the **Abilities** list:

1.  Click **+** to add a new ability configuration.
2.  **Name**: Descriptive name (e.g., "Jump").
3.  **Ability Type Id**: Unique integer ID (consts defined in `AbilityTypes.cs` normally, use integers for now).
4.  **Priority**: Higher number = higher priority.
    *   *Example:* Jump (100) overrides Idle (0).
5.  **Start Active**: Check for toggleable abilities (like "Can Run").
6.  **Start Type**: Choose trigger method (e.g., `InputDown`, `Automatic`).
7.  **Stop Type**: Choose stop method (e.g., `Duration`, `Manual`).

---

## Ability System Concepts

### Priority Resolution

Every frame, the `AbilityPrioritySystem` determines the best candidate to run:
1.  **Evaluation**: Checks `CanStart` flags for all abilities.
2.  **Comparison**: Compares priority of candidates vs currently active ability.
3.  **Blocking**: Checks `BlockedByMask` and `BlocksMask` for explicit conflicts.

### Triggers & Detection

*   **Starters**: How an ability requests to start.
    *   `InputDown`: Triggered by Input System action.
    *   `Automatic`: Checks conditions every frame.
*   **Stoppers**: How an ability ends.
    *   `Duration`: Automatically stops after X seconds.
    *   `Manual`: Logic must explicitly set `CanStop`.
*   **Detection**: Systems that gather world data.
    *   `DetectGround`: Updates angle/normal data.
    *   `DetectObject`: Updates target ent/pos data.

---

## Component Reference

### AbilityAuthoring

The primary interface for designers.

| Property | Description |
| :--- | :--- |
| **Priority** | Determiner for execution order. Higher wins. |
| **Blocked By Mask** | Bitmask of priorities that prevent this ability. |
| **Blocks Mask** | Bitmask of priorities this ability prevents. |
| **Input Action Id** | ID mapping to the Input System (if StartType is Input). |

### Runtime Components

These are added automatically by the baker.

| Component | Purpose |
| :--- | :--- |
| **AbilityState** | Tracks `ActiveAbilityIndex`, `AbilityStartTime`, `PendingAbilityIndex`. |
| **AbilityDefinition** | DynamicBuffer storing static config for all abilities. |
| **DetectObjectAbility** | Config for raycast/overlap sensing. |
| **DetectGroundAbility** | Config for slope/ground sensing. |

---

## Developer Guide

### Update Order

The Ability System runs in the `PredictedFixedStepSimulationSystemGroup`:

```
AbilitySystemGroup
├── AbilityDetectionSystem  (Gathers data)
├── AbilityTriggerSystem    (Evaluates conditions)
├── AbilityPrioritySystem   (Resolves conflicts)
└── AbilityLifecycleSystem  (Manages state transitions)
```

### Adding a New Ability Type

1.  **Define ID**: Add a const to your Types file (e.g., `public const int JUMP_ABILITY = 1;`).
2.  **Create Logic System** (Optional):
    *   Create a system updating *after* `AbilityLifecycleSystem`.
    *   Query for `AbilityState.ActiveAbilityIndex == JUMP_ABILITY`.
    *   Execute custom move logic.

```csharp
[UpdateInGroup(typeof(AbilitySystemGroup))]
[UpdateAfter(typeof(AbilityLifecycleSystem))]
public partial struct JumpAbilitySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (abilityState, transform) in SystemAPI.Query<RefRO<AbilityState>, RefRW<LocalTransform>>())
        {
            if (activeAbilityIndex == JUMP_ABILITY)
            {
                // Execute Jump Logic
            }
        }
    }
}
```

---

## Troubleshooting

### Ability Won't Start

1.  **Check Priority**: Is a higher priority ability currently active?
2.  **Check Active Flag**: Is `IsActive` (Start Active) enabled in the config?
3.  **Check Conditions**:
    *   `Automatic`: Is the condition logic returning true?
    *   `Input`: Is the input ID correct and receiving events?

### Ability Won't Stop

1.  **Check Stop Type**:
    *   If `Manual`, ensure some system is setting `CanStop = true`.
    *   If `Duration`, ensure duration is > 0.
2.  **Check Priority**: Is a higher priority ability failing to interrupt it?

---

## Version History

| Version | Date | Changes |
| :--- | :--- | :--- |
| 1.0.0 | 2025-12-28 | Initial Release (Architecture Only) |
