# EPIC 17.13: Analytics & Telemetry — Setup Guide

## Overview

The Analytics & Telemetry system provides a unified, privacy-aware event pipeline that records structured gameplay events (combat, economy, progression, crafting, quests, performance) and dispatches them asynchronously to configurable targets (local JSON lines file, HTTP endpoint, Unity Analytics). It includes an A/B testing framework with deterministic variant assignment, GDPR-compliant privacy controls, and a live editor workstation for inspecting events in real time.

All analytics systems are **read-only observers** — they do not modify any existing gameplay systems. The entire pipeline runs with zero main-thread I/O cost (background thread dispatch) and zero impact on the player entity archetype. All data is global singletons and static managed classes.

---

## 1. Quick Start: Creating Config Assets

Three config assets must exist in `Resources` folders for the bootstrap to load them at runtime.

### Analytics Profile

1. Right-click in the Project window: **Create → DIG → Analytics → Analytics Profile**.
2. Name it `AnalyticsProfile` and place it in `Assets/Resources/AnalyticsProfile.asset`.
3. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| Enabled Categories | All (0xFFF) | Bitmask of which event categories are active. Uncheck categories to disable them entirely. |
| Global Sample Rate | 1.0 | Probability of recording any event. 1.0 = 100% of events recorded. 0.5 = ~50%. |
| Category Sample Rates | (empty) | Per-category overrides. E.g., set Performance to 0.1 to only sample 10% of performance frames. |
| Flush Interval Seconds | 30 | Seconds between background flush cycles. Lower = more frequent writes. Min 1. |
| Batch Size | 100 | Maximum events per dispatch batch. Min 10. |
| Ring Buffer Capacity | 10000 | Maximum events held in memory. When full, oldest events are dropped. Min 100. |
| Dispatch Targets | (see below) | Ordered list of where events are sent. A FileTarget is always added as fallback. |
| Include Super Properties | true | Attach build version, platform, Unity version, and A/B test variants to every event. |
| Enable Debug Logging | false | Editor-only: logs every tracked event to the Unity console. **Disable for builds.** |

#### Category Sample Rates

Click **+** to add per-category rate overrides. Useful for high-frequency categories:

| Category | Recommended Rate | Reason |
|----------|-----------------|--------|
| Performance | 0.1 | Sampled every 60 frames already; 10% further reduces volume |
| Combat | 1.0 | Important for balance — keep at 100% |
| Economy | 1.0 | Critical for inflation tracking |
| UI | 0.5 | High volume, statistical sampling sufficient |

### Privacy Policy

1. Right-click: **Create → DIG → Analytics → Privacy Policy**.
2. Name it `PrivacyPolicy` and place it in `Assets/Resources/PrivacyPolicy.asset`.
3. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| Default Analytics Consent | false | GDPR requires opt-in. Leave false for production builds. |
| Default Crash Report Consent | false | Same — must be explicitly granted by the player. |
| Default Personal Data Consent | false | When false, player IDs are SHA256-hashed in all events. |
| Essential Events Always On | true | Session start/end always recorded regardless of consent level. |
| Data Retention Days | 90 | Local `.jsonl` files older than this are auto-deleted on startup. |
| PII Fields | playerId, playerName, ip | Fields stripped when Personal Data consent is not given. |
| Require Explicit Consent | true | Blocks all non-essential analytics until consent dialog is shown. |
| Consent Dialog Prefab | (optional) | Reference to a UI prefab shown on first launch for consent collection. |

> **Important:** For development/playtesting, you can set Default Analytics Consent to `true` and Require Explicit Consent to `false`. **Always revert these for release builds.**

### Dispatch Target Configs

Create one or more dispatch targets to control where events are sent.

1. Right-click: **Create → DIG → Analytics → Dispatch Target**.
2. Name descriptively (e.g., `FileTarget_Default`, `HttpTarget_Staging`).
3. Assign to the **Dispatch Targets** array on your `AnalyticsProfile`.

| Field | Default | Description |
|-------|---------|-------------|
| Target Type | File | `File`, `Http`, or `UnityAnalytics`. |
| Enabled | true | Toggle without removing from the list. |

#### File Target Settings

| Field | Default | Description |
|-------|---------|-------------|
| File Name Pattern | `analytics_{sessionId}.jsonl` | `{sessionId}` is replaced at runtime. One file per session. |

Files are written to `Application.persistentDataPath/analytics/`. Each line is a JSON object:

```
{"ts":1708700000000,"sid":"abc123","cat":"Combat","act":"kill","pid":"player_1","tick":54321,"props":{"playerLevel":5,"pos":"12.5,0.0,8.3"}}
```

#### HTTP Target Settings

| Field | Default | Description |
|-------|---------|-------------|
| Endpoint URL | (empty) | Full URL to POST batches to (e.g., `https://analytics.example.com/v1/events`). |
| API Key Encrypted | (empty) | API key for Authorization header. |
| Batch Size | 50 | Events per HTTP request. |
| Max Retries | 3 | Retry attempts on server error (5xx) or timeout. |
| Retry Base Delay Ms | 1000 | Exponential backoff base. Delay = base × 2^attempt, capped at 30s. |
| Timeout Ms | 5000 | HTTP request timeout. |

#### Unity Analytics Target

Set Target Type to `UnityAnalytics`. Requires the Unity Analytics package installed. If the package is absent, this target compiles out and does nothing.

---

## 2. A/B Testing Setup

### Creating Tests

1. Create a folder: `Assets/Resources/ABTests/`.
2. Right-click inside it: **Create → DIG → Analytics → AB Test Config**.
3. Name each config after its test ID (e.g., `weapon_balance_v2`).
4. Configure:

| Field | Default | Description |
|-------|---------|-------------|
| Test Id | (empty) | Unique identifier. Used in code and analytics events. |
| Is Active | true | Disable to deactivate without deleting the asset. |
| Variants | 2 entries | Array of variant definitions. |
| Start Date | (empty) | ISO 8601 date when the test starts. Empty = always active. |
| End Date | (empty) | ISO 8601 date when the test ends. Empty = never expires. |

### Variant Configuration

Each variant entry has:

| Field | Default | Description |
|-------|---------|-------------|
| Variant Name | `control` | Identifier (e.g., `control`, `variant_a`, `variant_b`). |
| Weight | 1.0 | Relative weight for random assignment. Equal weights = equal distribution. |
| Feature Flags | (empty) | String keys that can be checked at runtime with `ABTestManager.IsFeatureEnabled("key")`. |

### How Assignment Works

- Players are assigned a variant deterministically based on a hash of `playerId + testId`.
- The same player always gets the same variant for the same test, across sessions.
- Assignments are persisted in `PlayerPrefs` for consistency.
- Every analytics event automatically includes active A/B test variant assignments as super properties.

### Overriding Variants (QA/Design)

Use the **Analytics Workstation → A/B Test Override** tab during Play Mode to:
- See all active tests and current assignments.
- Click a variant button to force a specific assignment.
- Click **Randomize All** to shuffle assignments.
- Click **Clear All Overrides** to revert to deterministic assignment.

---

## 3. Event Categories Reference

All events are categorized using a bitmask. Each category can be independently enabled/disabled on the `AnalyticsProfile`.

| Category | Flag | Events Recorded |
|----------|------|-----------------|
| Session | 0x1 | `session_start`, `session_end`, `player_join`, `player_leave` |
| Combat | 0x2 | `kill`, `player_death`, `damage_summary` (aggregated every 5s) |
| Economy | 0x4 | `currency_gain`, `currency_spend` (Gold, Premium, Crafting deltas) |
| Progression | 0x8 | `level_up`, `xp_gain` (aggregated every 30s) |
| Quest | 0x10 | `quest_accept`, `quest_complete`, `quest_abandon` |
| Crafting | 0x20 | `craft_success` (recipe ID, item type, quantity) |
| Social | 0x40 | Reserved for party/chat events (future) |
| Performance | 0x80 | `fps_drop` (<20 FPS), `memory_spike` (>2GB), `frame_hitch` (>50ms) |
| UI | 0x100 | Reserved for menu/screen tracking (future) |
| World | 0x200 | Reserved for zone transitions, environmental events (future) |
| PvP | 0x400 | Reserved for match/ranking events (future) |
| Custom | 0x800 | Designer-defined events via `AnalyticsAPI.TrackEvent("Custom", ...)` |

> **Tip:** To disable all analytics entirely, set Enabled Categories to `None` on the AnalyticsProfile. This causes every bridge system to early-out with <0.001ms cost.

---

## 4. Privacy & Consent

### Consent Levels

| Level | What's Collected | Player ID in Events |
|-------|-----------------|---------------------|
| None (default) | Essential events only (if configured) | Stripped |
| Analytics only | All gameplay events | Hashed (SHA256, 16 chars) |
| Analytics + Personal Data | All gameplay events | Raw ID included |
| All | Gameplay + crash reports | Raw ID included |

### How Consent Is Stored

- Three `PlayerPrefs` keys: `analytics_consent`, `crash_consent`, `personal_consent` (int: 0 or 1).
- When `Require Explicit Consent` is true and no stored consent exists, all non-essential analytics are blocked until the player grants permission.
- Consent changes take effect immediately — no restart required.

### Local-Only Mode

When analytics consent is not granted:
- `AnalyticsAPI.TrackEvent()` returns immediately (zero cost).
- Essential events (session start/end) are still recorded locally if `Essential Events Always On` is checked.
- No HTTP dispatch occurs — data stays on-device.
- Performance metrics still collect for local debug display but do NOT route through the analytics pipeline.

### Data Retention

Local `.jsonl` files in `Application.persistentDataPath/analytics/` are automatically cleaned up on startup. Any file older than `Data Retention Days` is deleted.

---

## 5. Analytics Workstation (Editor Tool)

Open via: **DIG → Analytics Workstation** (menu bar).

The workstation provides 6 tabs for live analytics inspection during Play Mode.

### Tab 1: Live Event Stream

- Real-time scrolling list of analytics events.
- **Color-coded** by category (red = Combat, blue = Progression, gold = Economy, green = Session, etc.).
- **Filter** by category bitmask dropdown.
- **Search** by action string (e.g., type "kill" to see only kill events).
- **Copy JSON** button per event — copies the event as a JSON object to clipboard.
- **Auto-scroll** toggle — keeps the view pinned to the latest event.
- **Pause** toggle — freezes the display without stopping collection.
- Max 500 events displayed (ring buffer — oldest scroll off).

### Tab 2: Session Timeline

- Horizontal timeline bar showing the full session duration.
- Event markers are color-coded by category.
- **Hover** over a marker to see event details (category, action, properties, timestamp).
- **Zoom** slider for long sessions.
- Summary panel shows duration and event count per category.

### Tab 3: Event Frequency

- Bar chart showing event counts per category.
- Configurable time window: **1 min**, **5 min**, **15 min**, or **Session**.
- Useful for identifying event storms or dead periods.

### Tab 4: Dispatch Queue

- **Queue Depth** with color indicator (green < 1000, yellow < 5000, red ≥ 5000).
- Counters: Events Enqueued, Events Dispatched, Events Dropped.
- **Queue Utilization** bar showing fill percentage of the 10,000-event ring buffer.
- **Force Flush** button — triggers an immediate background dispatch cycle.

### Tab 5: Privacy Simulator

- Toggle **Analytics**, **Crash Reports**, and **Personal Data** consent checkboxes.
- Changes take effect immediately in the running session.
- **Reset Stored Consent** button — clears all PlayerPrefs consent keys.
- **Scrub Preview** — shows the last 5 events with before/after comparison:
  - Events that would be blocked show as **BLOCKED** (red).
  - Events that pass show the scrubbed Player ID (green).

### Tab 6: A/B Test Override

- Lists all active A/B tests loaded from `Resources/ABTests/`.
- Shows current variant assignment per test.
- Click variant buttons to **force override** during the session.
- **Randomize All** — randomly reassigns all test variants.
- **Clear All Overrides** — reverts to deterministic assignment.
- **Export JSON** — copies all current assignments to clipboard as JSON.
- Shows active **feature flags** for each variant.

---

## 6. Resource Asset Checklist

The following assets must be in `Resources` folders for the analytics system to initialize:

| # | Path | Required | Notes |
|---|------|----------|-------|
| 1 | `Assets/Resources/AnalyticsProfile.asset` | **Yes** | System will not initialize without this. |
| 2 | `Assets/Resources/PrivacyPolicy.asset` | Recommended | Falls back to defaults if missing (all consent off). |
| 3 | `Assets/Resources/ABTests/*.asset` | Optional | Only needed if running A/B tests. Folder can be empty. |

Dispatch Target Config assets can live anywhere in the project — they are referenced by the AnalyticsProfile, not loaded from Resources.

---

## 7. Custom Events (Designer/Gameplay Scripter)

To record a custom event from any managed MonoBehaviour or class:

```csharp
AnalyticsAPI.TrackEvent("Custom", "my_event_name", new Dictionary<string, object>
{
    { "key1", "value" },
    { "key2", 42 },
    { "key3", true }
});
```

Or from an ECS system (avoids Dictionary allocation):

```csharp
var props = new FixedString512Bytes();
props.Append("{\"key1\":\"value\",\"key2\":42}");

AnalyticsAPI.TrackEvent(new AnalyticsEvent
{
    Category = AnalyticsCategory.Custom,
    Action = new FixedString64Bytes("my_event_name"),
    PropertiesJson = props
});
```

Both approaches are thread-safe and route through the same pipeline (category check → sampling → privacy filter → background dispatch).

> **Note:** Custom events require the `Custom` category to be enabled in the AnalyticsProfile.

---

## 8. Feature Flags (A/B Test Integration)

Designers can gate gameplay features behind A/B test variants using feature flags:

1. On an `ABTestConfig` asset, add feature flag strings to a variant's **Feature Flags** array (e.g., `"new_loot_table"`, `"double_xp"`).
2. In gameplay code, check the flag:

```csharp
if (ABTestManager.IsFeatureEnabled("new_loot_table"))
{
    // Use alternate loot table
}
```

Feature flags are checked as a simple `HashSet.Contains` — effectively free at runtime.

---

## 9. Multiplayer Considerations

| Aspect | Behavior |
|--------|----------|
| Server analytics | All bridge systems (Combat, Economy, Progression, Quest, Crafting) run server-side. Events reflect authoritative game state. |
| Client analytics | `PerformanceMetricsSystem` runs client-side only (FPS, memory, frame time). |
| Network impact | **Zero.** No RPCs, no ghost components, no network traffic for analytics. |
| Session scope | Single session per server. All player events attributed via PlayerId. |
| Single player | Same pipeline. PlayerCount always 1. |

---

## 10. Performance Budget

| Operation | Cost | Frequency |
|-----------|------|-----------|
| No-events frame (all early-outs) | < 0.03ms | Every frame |
| Kill event frame (worst case) | < 0.15ms | Rare |
| PerformanceMetrics sample frame | < 0.02ms | Every 60th frame |
| PerformanceMetrics skip frame | < 0.001ms | 59 of 60 frames |
| Background dispatch (FileTarget) | ~1-5ms | Every 30s (background thread) |
| Background dispatch (HttpTarget) | ~10-100ms | Every 30s (background thread) |
| Memory steady-state | < 1 MB | Constant |

---

## 11. Backward Compatibility

| Scenario | Behavior |
|----------|----------|
| No `AnalyticsProfile` in Resources | Bootstrap logs warning, disables all systems. Zero overhead. |
| Analytics consent not granted | Essential events only (or none). Gameplay completely unaffected. |
| HTTP endpoint unreachable | Retries exhausted → batch dropped. FileTarget always succeeds as fallback. |
| Unity Analytics package not installed | `UnityAnalyticsTarget` compiles out. No errors. |
| No AB tests configured | Empty assignments. Super properties have no AB test data. |
| Existing VFX/Audio telemetry | Completely unaffected. They continue serving their own workstations. |

---

## 12. Troubleshooting

| Symptom | Cause | Solution |
|---------|-------|----------|
| `[Analytics] No AnalyticsProfile found` in console | Missing Resources asset | Create `AnalyticsProfile` in `Assets/Resources/` |
| No events appearing in workstation | Analytics not initialized or consent blocked | Check console for init log; verify consent settings on PrivacyPolicy |
| Events recorded but no `.jsonl` file | FileTarget session not set | Ensure `AnalyticsProfile` has at least one Dispatch Target, or let the fallback FileTarget handle it |
| All events show hashed Player ID | Personal Data consent not granted | Expected behavior. Grant Personal Data consent or set default to true (dev only) |
| Queue depth growing indefinitely | Dispatch thread not running | Check console for dispatcher errors. Use **Force Flush** in Dispatch Queue tab. |
| A/B test always assigns same variant | Expected — assignment is deterministic | Use **A/B Test Override** tab to force a different variant for testing |
| HTTP target dropping all batches | Endpoint URL misconfigured or server down | Check URL in DispatchTargetConfig. Verify server is accepting POST requests. |
| `Enable Debug Logging` flooding console | Every event logs to console | Disable on AnalyticsProfile. Only use during targeted debugging. |
