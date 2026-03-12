# EPIC 18.3: Notification / Toast / Popup System — Setup Guide

**Status:** Implemented
**Last Updated:** March 4, 2026
**Requires:** EPIC 18.1 (IUIService, UIServiceBootstrap, UIToolkitService)

---

## Overview

The Notification system provides 3 channels for player-facing messages:

| Channel | Behavior | Default Position |
|---------|----------|-----------------|
| **Toast** | Corner slide-in, stacks up to 3 | Top Right |
| **Banner** | Full-width bar, one at a time | Top Center |
| **CenterScreen** | Centered modal with backdrop, persists until dismissed | Center |

Key behaviors:
- **Priority queuing** — Critical shows before Low when slots are full
- **Deduplication** — Same `DeduplicationKey` updates in-place instead of spawning duplicates
- **History ring buffer** — Recent notifications queryable via `GetHistory()`
- **Audio** — Optional per-style sound clip played on show
- **ECS bridge** — `NotificationVisualQueue` for ECS→UI, plus opt-in draining of Achievement/LevelUp/Quest queues

---

## Step 1: Create NotificationConfig Asset

1. Right-click in Project → **Create > DIG > Notifications > Config**
2. Name it `NotificationConfig`
3. Move to `Assets/Resources/NotificationConfig.asset`

| Field | Default | Description |
|-------|---------|-------------|
| Toast MaxVisible | 3 | Max simultaneous toasts |
| Toast MaxQueueSize | 10 | Max queued toasts |
| Banner MaxVisible | 1 | Always 1 banner at a time |
| Center MaxVisible | 1 | Always 1 center notification |
| History Ring Size | 50 | How many records to keep |
| Default Toast Style | (optional) | Default `NotificationStyleSO` for toasts |
| Default Banner Style | (optional) | Default `NotificationStyleSO` for banners |
| Default Center Style | (optional) | Default `NotificationStyleSO` for center |
| Use Unified Achievements | false | When true, drains AchievementVisualQueue via unified system |
| Use Unified Level Up | false | When true, drains LevelUpVisualQueue via unified system |
| Use Unified Quests | false | When true, drains QuestEventQueue via unified system |

---

## Step 2: Create Notification Styles (Optional)

1. Right-click in Project → **Create > DIG > Notifications > Style**
2. Name it descriptively (e.g., `DefaultToast`, `Achievement`, `LevelUp`, `Quest`)
3. Configure colors, duration, animation speed, sound, and default priority
4. Assign to `NotificationConfig`'s Default Style fields, or reference by name via `StyleId`

Style assets referenced by `StyleId` should be placed in `Assets/Resources/NotificationStyles/` (e.g., `Assets/Resources/NotificationStyles/Achievement.asset` for `StyleId = "Achievement"`).

---

## Step 3: Add NotificationServiceBootstrap to Scene

1. Find the persistent/boot scene (same one that has `UIServiceBootstrap`)
2. Add a `NotificationServiceBootstrap` component to a persistent GameObject
   - Can be on the same GameObject as `UIServiceBootstrap` or a new one
3. The bootstrap:
   - Runs at execution order `-250` (after `UIServiceBootstrap` at `-300`)
   - Loads `NotificationConfig` from Resources
   - Creates UXML channel containers in UIToolkitService layers
   - Creates `NotificationService` singleton

---

## Step 4: Using the API

### Show a Toast

```csharp
using DIG.Notifications;

NotificationService.Instance.Show(new NotificationData
{
    Channel = NotificationChannel.Toast,
    Title = "Item Acquired",
    Body = "You found a Legendary Sword!",
    Priority = NotificationPriority.Normal,
});
```

### Show a Banner

```csharp
NotificationService.Instance.Show(new NotificationData
{
    Channel = NotificationChannel.Banner,
    Title = "Zone Discovered",
    Body = "The Frozen Wastes",
});
```

### Show a Center Screen Notification

```csharp
var handle = NotificationService.Instance.Show(new NotificationData
{
    Channel = NotificationChannel.CenterScreen,
    Title = "Level Up!",
    Body = "You reached Level 10!",
    ActionButtonLabel = "View Stats",
    OnAction = () => OpenStatsPanel(),
    OnDismiss = () => Debug.Log("Dismissed"),
});
```

### Deduplication

```csharp
// First call shows the notification
NotificationService.Instance.Show(new NotificationData
{
    Channel = NotificationChannel.Toast,
    Title = "Quest Progress",
    Body = "Wolves: 1/5",
    DeduplicationKey = "quest_wolf_progress",
});

// Second call with same key updates in-place (no duplicate)
NotificationService.Instance.Show(new NotificationData
{
    Channel = NotificationChannel.Toast,
    Title = "Quest Progress",
    Body = "Wolves: 2/5",
    DeduplicationKey = "quest_wolf_progress",
});
```

### Dismiss

```csharp
var handle = NotificationService.Instance.Show(...);
NotificationService.Instance.Dismiss(handle);

// Or dismiss all
NotificationService.Instance.DismissAll();
```

### ECS → UI Bridge

From an ECS system, enqueue to `NotificationVisualQueue`:

```csharp
using DIG.Notifications;
using DIG.Notifications.Bridge;

NotificationVisualQueue.Enqueue(new NotificationVisualEvent
{
    Channel = NotificationChannel.Toast,
    Priority = NotificationPriority.High,
    Title = "Boss Defeated",
    Body = "The Ancient Guardian has fallen!",
});
```

The `NotificationBridgeSystem` drains this queue each frame in `PresentationSystemGroup`.

---

## Step 5: Editor Tooling — Notification Workstation

Open via **DIG > Notification Workstation** in the menu bar.

| Tab | What It Does | Requires Play Mode? |
|-----|-------------|---------------------|
| **Preview** | Fire test notifications with configurable channel, priority, title, body, style, dedup key, burst count | Yes |
| **Style Browser** | Browse all `NotificationStyleSO` assets, inspect colors/timing/audio, view master config | No |

---

## ECS Bridge — Migrating Existing Queues

The system can optionally drain existing visual queues instead of their dedicated bridge systems:

| Flag | Queue Drained | Replaces |
|------|--------------|----------|
| `UseUnifiedAchievements` | `AchievementVisualQueue` | `AchievementUIBridgeSystem` toast logic |
| `UseUnifiedLevelUp` | `LevelUpVisualQueue` | `ProgressionUIBridgeSystem` popup logic |
| `UseUnifiedQuests` | `QuestEventQueue` | Quest-specific UI bridge |

**Important:** When enabling these flags, the original bridge systems will still run but find empty queues (the notification system drains them first). No code changes needed in existing systems.

---

## Troubleshooting

### Notifications don't appear
- Check console for `[NotificationServiceBootstrap] UIServices.Screen is not initialized`
  - Ensure `UIServiceBootstrap` is in the scene and has execution order `-300`
- Check for `NotificationConfig not found in Resources`
  - Create the config asset and place it in `Assets/Resources/`

### Toast slides in but never disappears
- Check that `Duration` is > 0 (either on the `NotificationData`, the `NotificationStyleSO`, or the channel config's `DefaultDuration`)
- CenterScreen channel defaults to duration 0 (persists until dismissed) — this is by design

### No sound plays
- Assign an `AudioClip` to the `NotificationStyleSO.Sound` field, or pass `Sound` in `NotificationData`
- Ensure `NotificationService` GameObject has an `AudioSource` (auto-added by service)

### Deduplication not working
- Ensure both Show() calls use the **exact same** `DeduplicationKey` string
- Dedup only works while the original notification is still visible

### ECS notifications don't appear
- `NotificationBridgeSystem` requires `ClientSimulation` or `LocalSimulation` world
- Call `NotificationVisualQueue.Initialize()` before enqueueing (auto-initializes on first Enqueue, but explicit init avoids race conditions)

---

## Verification Checklist

- [ ] `NotificationConfig.asset` exists in `Assets/Resources/`
- [ ] `NotificationServiceBootstrap` component on a persistent scene GameObject
- [ ] In Play Mode: `NotificationService.Instance` is not null
- [ ] Toast: appears top-right, slides in, auto-dismisses after duration
- [ ] Banner: appears full-width at top, slides down, auto-dismisses
- [ ] CenterScreen: centered panel with backdrop, dismiss button works
- [ ] Fire 5 rapid toasts → max 3 visible, 2 queued, shown as slots free up
- [ ] Fire toast with same DeduplicationKey twice → updates in-place
- [ ] Fire Critical + Low priority → Critical shows first
- [ ] History: `NotificationService.Instance.GetHistory()` returns records
- [ ] Editor: **DIG > Notification Workstation** opens, Preview tab fires notifications
- [ ] Style Browser shows all NotificationStyleSO assets
