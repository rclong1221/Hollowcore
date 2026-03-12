# EPIC 18.3: Notification / Toast / Popup System

**Status:** IMPLEMENTED
**Priority:** High (Cross-cutting UX — used by every gameplay system)
**Dependencies:**
- `AchievementToastView` (existing — `DIG.Achievement`, `Assets/Scripts/Achievement/UI/AchievementToastView.cs`, slide-in queue system)
- `MapNotificationView` (existing — `DIG.Map.UI`, `Assets/Scripts/Map/UI/MapNotificationView.cs`)
- `LevelUpPopupView` (existing — `DIG.Progression.UI`, `Assets/Scripts/Progression/UI/LevelUpPopupView.cs`)
- `AchievementVisualQueue` (existing — `DIG.Achievement.Bridges`, static NativeQueue pattern)
- `LevelUpVisualQueue` (existing — `DIG.Progression.UI`, static NativeQueue bridge)
- `DamageVisualQueue` (existing — `DIG.Combat.UI`, static NativeQueue bridge)
- `IUIService` (EPIC 18.1 — screen lifecycle, layer management)
- `UIThemeSO` (EPIC 18.1 — theme color variables)

**Feature:** A unified, priority-aware notification system that replaces all one-off toast/popup implementations with a single service. Supports multiple notification channels (Toast, Banner, CenterScreen, WorldSpace), automatic queue management with priority/deduplication, configurable visual styles via ScriptableObject, ECS-to-UI bridging for game events, and designer tooling for previewing/testing notifications without entering Play mode.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `AchievementToastView` | `Assets/Scripts/Achievement/UI/AchievementToastView.cs` | Fully implemented | Queue-based, slide-in/out animation, max 5 pending, tier coloring. Achievement-specific only |
| `MapNotificationView` | `Assets/Scripts/Map/UI/MapNotificationView.cs` | Implemented | Map-specific notifications |
| `LevelUpPopupView` | `Assets/Scripts/Progression/UI/LevelUpPopupView.cs` | Implemented | Level-up specific center-screen popup |
| `AchievementVisualQueue` | `Assets/Scripts/Achievement/Bridges/AchievementVisualQueue.cs` | Fully implemented | Static NativeQueue, ECS → managed UI bridge |
| `LevelUpVisualQueue` | `Assets/Scripts/Progression/UI/LevelUpVisualQueue.cs` | Fully implemented | Same NativeQueue pattern |
| `DamageVisualQueue` | `Assets/Scripts/Combat/UI/DamageVisualQueue.cs` | Fully implemented | Same NativeQueue pattern |

### What's Missing

- **No generic notification service** — each subsystem builds its own toast/popup with its own animation, queue, and styling
- **No priority system** — if achievement and level-up fire simultaneously, they compete; no priority ordering
- **No deduplication** — same notification can queue multiple times (e.g., rapid quest progress updates)
- **No notification channels** — no concept of "this is a toast vs. a banner vs. a center-screen alert"
- **No designer-configurable styles** — each toast hardcodes its own colors, animation timing, layout
- **No notification history** — once a toast fades, it's gone; no "notification center" to review missed items
- **No world-space notifications** — no floating labels above entities (e.g., "Quest Available" over NPC heads)
- **No sound integration** — toasts don't play a sound effect on appear

---

## Problem

Every subsystem that needs to communicate with the player (achievements, level-ups, quest updates, loot pickups, trade offers, system messages) independently implements its own notification UI. This leads to:

1. **Visual inconsistency** — each toast looks different, animates differently, and appears in different positions
2. **Notification stomping** — two toasts can appear simultaneously and overlap
3. **Code duplication** — the queue + slide animation pattern is repeated in `AchievementToastView`, and will need to be repeated for every new notification type
4. **No priority** — a critical "Server Shutting Down" message has no way to preempt achievement toasts
5. **No history** — players miss notifications during intense gameplay with no way to review them

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  NotificationStyleSO           NotificationChannelConfig SO
  (icon, colors, animation,     (channel type: Toast/Banner/Center,
   sound, duration, priority     max visible, queue size, position,
   class, UXML template)        stacking behavior)
        |                            |
        └──── NotificationService (singleton MonoBehaviour) ─────┘
              (central dispatch, priority queue, deduplication,
               channel routing, history buffer, ECS bridge)
                         |
        ┌────────────────┼────────────────┐
        |                |                |
  ToastChannel      BannerChannel    CenterChannel
  (slide-in from    (full-width bar   (modal-like center
   corner, queue     at top/bottom,    popup, blocks input
   with stacking,    auto-dismiss)     optionally, has
   max N visible)                      confirm/dismiss)
        |                |                |
        └────────────────┼────────────────┘
                         |
              ECS Bridge Layer
                         |
  NotificationBridgeSystem (PresentationSystemGroup)
  (drains NativeQueues from achievement, progression,
   quest, combat subsystems → NotificationService.Show())
                         |
                 EDITOR TOOLING
                         |
  NotificationPreviewModule ── preview & test
  (send test notifications, preview styles,
   adjust channel configs, view history log)
```

---

## Core API

### NotificationService

**File:** `Assets/Scripts/Notifications/NotificationService.cs`

```csharp
public class NotificationService : MonoBehaviour
{
    public static NotificationService Instance { get; private set; }

    NotificationHandle Show(NotificationData data);
    NotificationHandle Show(string styleId, string title, string body, Sprite icon = null);
    void Dismiss(NotificationHandle handle);
    void DismissAll(string channel = null);
    void ClearHistory();
    IReadOnlyList<NotificationRecord> GetHistory(int maxCount = 50);

    event Action<NotificationRecord> OnNotificationShown;
    event Action<NotificationHandle> OnNotificationDismissed;
}
```

### NotificationData

**File:** `Assets/Scripts/Notifications/NotificationData.cs`

```csharp
public struct NotificationData
{
    public string StyleId;           // References NotificationStyleSO
    public string Channel;           // "Toast", "Banner", "Center"
    public string Title;
    public string Body;
    public Sprite Icon;
    public int Priority;             // Higher = shown first (0 = normal, 100 = critical)
    public float Duration;           // 0 = use style default, -1 = sticky (manual dismiss)
    public string DeduplicationKey;  // Same key = replace existing instead of queuing new
    public string ActionButtonText;  // Optional action button label
    public Action OnAction;          // Callback for action button
    public Action OnDismiss;         // Callback when dismissed
    public AudioClip Sound;          // Override style default sound
}
```

### NotificationHandle

```csharp
public readonly struct NotificationHandle : IEquatable<NotificationHandle>
{
    public readonly int Id;
    public readonly string Channel;
    public bool IsValid { get; }
}
```

---

## ScriptableObjects

### NotificationStyleSO

**File:** `Assets/Scripts/Notifications/Config/NotificationStyleSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| StyleId | string | "" | Unique identifier |
| DisplayName | string | "" | Editor label |
| Channel | string | "Toast" | Default channel |
| IconTint | Color | white | Tint applied to icon |
| BackgroundColor | Color | rgba(0,0,0,0.85) | Background color |
| BorderColor | Color | #2196F3 | Left/top accent border |
| TitleColor | Color | white | Title text color |
| BodyColor | Color | #CCCCCC | Body text color |
| DefaultDuration | float | 4.0 | Seconds to display |
| Priority | int | 0 | Default priority |
| SoundClip | AudioClip | null | Play on show |
| EnterAnimation | NotificationAnimation | SlideRight | Entrance animation |
| ExitAnimation | NotificationAnimation | FadeOut | Exit animation |
| AnimationDuration | float | 0.3 | Transition duration |
| UxmlOverride | VisualTreeAsset | null | Custom layout template |

### NotificationChannelConfig

**File:** `Assets/Scripts/Notifications/Config/NotificationChannelConfig.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| ChannelId | string | "Toast" | Channel identifier |
| MaxVisible | int | 3 | Max simultaneous notifications |
| MaxQueueSize | int | 10 | Max pending in queue |
| Position | ChannelPosition enum | TopRight | Screen position |
| StackDirection | StackDirection enum | Down | How multiples stack |
| StackSpacing | float | 8 | Pixels between stacked items |
| OverflowBehavior | OverflowBehavior enum | DropOldest | What to do when queue is full |

---

## Notification Channels

### ToastChannel

- Corner-anchored slide-in notifications (like AchievementToastView but generic)
- Supports stacking up to `MaxVisible` simultaneously with vertical offset
- Each toast auto-dismisses after `Duration` seconds
- Priority ordering: higher priority toasts jump the queue
- Deduplication: if a notification with the same `DeduplicationKey` is visible, update it in-place

### BannerChannel

- Full-width horizontal bar at top or bottom of screen
- One banner at a time (new banner replaces current)
- Used for system messages ("Server shutting down in 5 minutes"), zone transitions ("Entering: Dark Forest")
- Supports progress bar mode (e.g., download progress)

### CenterScreenChannel

- Modal-like centered panel
- Optionally blocks gameplay input
- Has confirm/dismiss buttons
- Used for critical alerts, reward summaries, confirmation dialogs
- Auto-sizes to content

---

## ECS Bridge

### NotificationBridgeSystem

**File:** `Assets/Scripts/Notifications/Systems/NotificationBridgeSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`
- Drains existing NativeQueues each frame:
  - `AchievementVisualQueue` → Toast with "Achievement" style
  - `LevelUpVisualQueue` → CenterScreen with "LevelUp" style
  - `QuestEventQueue` → Toast with "Quest" style
  - `DamageVisualQueue` → skipped (already has DamageNumber system)
- Does NOT modify existing queue producers — purely a consumer alongside existing UI providers
- Existing UI providers (`AchievementToastView`, etc.) continue to work; this system provides a unified fallback

---

## Notification History

### NotificationRecord

```csharp
public class NotificationRecord
{
    public int Id;
    public string StyleId;
    public string Channel;
    public string Title;
    public string Body;
    public DateTime Timestamp;
    public bool WasSeen;    // True if displayed long enough to be "seen"
    public bool WasActioned; // True if action button was clicked
}
```

- Circular buffer of last N notifications (configurable, default 100)
- Accessible via `NotificationService.GetHistory()`
- Future: notification center UI panel (not part of this epic)

---

## Editor Tooling

### NotificationPreviewModule

**File:** `Assets/Editor/NotificationWorkstation/Modules/NotificationPreviewModule.cs`

- **Style Browser:** Grid of all `NotificationStyleSO` assets with visual preview
- **Test Sender:** Send test notifications with custom title/body/priority/channel
- **Channel Visualizer:** Shows queue state for each channel (queued, visible, history)
- **Animation Tester:** Preview enter/exit animations without entering Play mode
- **Priority Simulator:** Send multiple notifications with different priorities to test ordering

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Notifications/NotificationService.cs` | MonoBehaviour | ~250 |
| `Assets/Scripts/Notifications/NotificationData.cs` | Struct | ~30 |
| `Assets/Scripts/Notifications/NotificationHandle.cs` | Struct | ~20 |
| `Assets/Scripts/Notifications/NotificationRecord.cs` | Class | ~20 |
| `Assets/Scripts/Notifications/Config/NotificationStyleSO.cs` | ScriptableObject | ~50 |
| `Assets/Scripts/Notifications/Config/NotificationChannelConfig.cs` | ScriptableObject | ~35 |
| `Assets/Scripts/Notifications/Channels/INotificationChannel.cs` | Interface | ~20 |
| `Assets/Scripts/Notifications/Channels/ToastChannel.cs` | Class | ~200 |
| `Assets/Scripts/Notifications/Channels/BannerChannel.cs` | Class | ~120 |
| `Assets/Scripts/Notifications/Channels/CenterScreenChannel.cs` | Class | ~150 |
| `Assets/Scripts/Notifications/Systems/NotificationBridgeSystem.cs` | SystemBase | ~100 |
| `Assets/Editor/NotificationWorkstation/Modules/NotificationPreviewModule.cs` | Editor | ~200 |

**Total estimated:** ~1,195 lines

---

## Performance Considerations

- Notification queue management is O(N log N) for priority sort but N ≤ 10 per channel — negligible
- UI elements are pooled per channel — no allocation on repeated show/dismiss cycles
- `NotificationBridgeSystem` runs in PresentationSystemGroup, drains at most a few events per frame
- Deduplication uses `Dictionary<string, NotificationHandle>` — O(1) lookup
- History ring buffer uses fixed-size array with index wrapping — zero GC

---

## Testing Strategy

- Unit test priority ordering: send 5 notifications with mixed priorities → verify display order
- Unit test deduplication: send 3 notifications with same key → verify only 1 visible, content updated
- Unit test queue overflow: exceed MaxQueueSize → verify DropOldest behavior
- Integration test: fire achievement unlock in ECS → verify toast appears via NotificationBridgeSystem
- Integration test: BannerChannel replaces current → verify previous banner dismissed
- Editor test: NotificationPreviewModule sends test notification in Edit mode
