# EPIC 17.13: Analytics & Telemetry

**Status:** PLANNED
**Priority:** Low (Operational Intelligence & Balance Tuning)
**Dependencies:**
- `VFXTelemetry` static debug counters (existing -- `DIG.VFX`, EPIC 16.7, per-frame/session VFX spawn/cull counters, `Assets/Scripts/VFX/Debug/VFXTelemetry.cs`)
- `AudioTelemetry` static debug counters (existing -- `Audio.Systems`, EPIC 15.27, footstep/landing/voice/cache counters, `Assets/Scripts/Audio/AudioTelemetry.cs`)
- `DamageVisualQueue` static NativeQueue bridge (existing -- `DIG.Combat`, ECS-to-UI event pattern, `Assets/Scripts/Combat/UI/DamageVisualQueue.cs`)
- `LevelUpVisualQueue` static NativeQueue bridge (existing -- `DIG.Progression.UI`, EPIC 16.14, `Assets/Scripts/Progression/UI/LevelUpVisualQueue.cs`)
- `SaveFileWriter` async background thread + ConcurrentQueue pattern (existing -- `DIG.Persistence.IO`, EPIC 16.15, `Assets/Scripts/Persistence/IO/SaveFileWriter.cs`)
- `KillCredited` IComponentData event (existing -- `Player.Components`, ephemeral on killer entity, created by `DeathTransitionSystem` via EndSimulationECB)
- `DiedEvent` IEnableableComponent (existing -- `Player.Components`, enabled by DeathTransitionSystem)
- `LevelUpEvent` IComponentData + IEnableableComponent (existing -- `DIG.Progression.Components`, EPIC 16.14, baked disabled, toggled by LevelUpSystem)
- `CombatResultEvent` IComponentData (existing -- `DIG.Combat`, transient entities with resolved damage data)
- `QuestEventQueue` static NativeQueue (existing -- `DIG.Quest.UI.QuestEventQueue`, EPIC 16.12, quest completion/accept/fail events)
- `CraftOutputGenerationSystem` (existing -- `DIG.Crafting.Systems`, EPIC 16.13, crafting output events)
- `CurrencyInventory` IComponentData (existing -- `DIG.Economy`, EPIC 16.6, Gold/Premium/Crafting)
- `PlayerProgression` IComponentData (existing -- `DIG.Progression.Components`, EPIC 16.14, CurrentXP/TotalXPEarned/UnspentStatPoints/RestedXP)
- `CharacterAttributes` IComponentData with `Level` field (existing -- `DIG.Combat.Components`, Ghost:All, 20 bytes)
- `GhostOwner.NetworkId` (existing -- NetCode player identity)
- `PersistenceBootstrapSystem` pattern (existing -- `DIG.Persistence.Systems`, EPIC 16.15, Resources.Load + singleton creation + self-disable)
- `CombatUIBridgeSystem` / `CombatUIRegistry` pattern (existing -- static registry + provider interface)

**Feature:** A unified, privacy-aware analytics and telemetry pipeline that collects structured gameplay events (combat, economy, progression, crafting, quests, performance) into a managed ring buffer, dispatches them asynchronously to configurable targets (local JSON lines file, HTTP endpoint, Unity Analytics), supports A/B testing variant assignment, and provides editor tooling for live event inspection. Uses a ConcurrentQueue background thread pattern (following `SaveFileWriter` from EPIC 16.15) for zero main-thread I/O cost. Global singletons only -- no player entity components -- zero 16KB archetype impact.

---

## Codebase Audit Findings

### What Already Exists (Confirmed by Deep Audit)

| System | File | Status | Notes |
|--------|------|--------|-------|
| `VFXTelemetry` counters | `Assets/Scripts/VFX/Debug/VFXTelemetry.cs` | Fully implemented | Per-frame + session totals. Static ints reset by VFXExecutionSystem. 7 categories, 4 LOD tiers (EPIC 16.7) |
| `AudioTelemetry` counters | `Assets/Scripts/Audio/AudioTelemetry.cs` | Fully implemented | Session counters for footsteps, landings, actions, cache misses, voice management. `GetSummary()` for debug overlay. No structured export |
| `DamageVisualQueue` bridge | `Assets/Scripts/Combat/UI/DamageVisualQueue.cs` | Fully implemented | Static NativeQueue pattern -- enqueue from ECS, dequeue from managed UI. Could be extended for analytics event bridging |
| `LevelUpVisualQueue` bridge | `Assets/Scripts/Progression/UI/LevelUpVisualQueue.cs` | Fully implemented | Same static NativeQueue pattern (EPIC 16.14) |
| `SaveFileWriter` async I/O | `Assets/Scripts/Persistence/IO/SaveFileWriter.cs` | Fully implemented | ConcurrentQueue + background thread + FlushBlocking() on shutdown. Reference pattern for analytics dispatcher |
| `KillCredited` event | `DeathTransitionSystem.cs` | Fully implemented | Ephemeral on killer entity, created via EndSimulationECB |
| `DiedEvent` (player death) | `DeathTransitionSystem.cs` | Fully implemented | IEnableableComponent on player |
| `LevelUpEvent` | `LevelUpSystem.cs` | Fully implemented | IEnableableComponent, toggled per level-up (EPIC 16.14) |
| `QuestEventQueue` | `QuestEventQueue.cs` | Fully implemented | Static NativeQueue, quest completion/accept/fail events (EPIC 16.12) |
| `CraftOutputGenerationSystem` | `CraftOutputGenerationSystem.cs` | Fully implemented | Generates crafted items (EPIC 16.13) |
| `CurrencyInventory` | `CurrencyInventory.cs` | Fully implemented | Gold/Premium/Crafting currency (EPIC 16.6) |
| `PlayerProgression` | `PlayerProgression.cs` | Fully implemented | CurrentXP, TotalXPEarned, UnspentStatPoints, RestedXP (EPIC 16.14) |

### What's Missing

- **No unified analytics event pipeline** -- VFXTelemetry and AudioTelemetry are isolated debug counters with no shared event schema, no export, no aggregation
- **No structured event logging** -- no JSON/binary event stream, no category taxonomy, no property bags
- **No session lifecycle tracking** -- no session IDs, no join/leave timestamps, no play duration recording, no disconnect reason capture
- **No gameplay event recording** -- kills, deaths, loot, crafting, quests, trades all have ECS events but nothing records them to persistent storage
- **No performance metrics collection** -- no FPS sampling, no frame time histograms, no memory tracking, no load time measurement
- **No A/B testing framework** -- no variant assignment, no feature flags, no experiment tracking
- **No backend dispatch** -- all telemetry is in-memory debug counters; nothing writes to disk or network
- **No privacy controls** -- no GDPR consent model, no data minimization, no PII scrubbing, no opt-out mechanism
- **No editor tooling** -- VFXTelemetry feeds the VFX Workstation, AudioTelemetry feeds Audio Workstation, but no unified analytics viewer

---

## Problem

DIG has a rich event ecosystem -- kills, deaths, quest completions, level-ups, crafting outputs, currency transactions, loot drops, VFX spawns, audio events -- but no system records these events to a persistent, queryable format. `VFXTelemetry` and `AudioTelemetry` are subsystem-specific debug counters that reset on session end and cannot be exported or aggregated. There is no way to answer fundamental questions about the game:

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `VFXTelemetry` per-frame counters (spawn/cull/pool) | No export to file or network; lost on session end |
| `AudioTelemetry` session counters (footsteps/landings/voices) | No structured event log; `GetSummary()` is debug-only |
| `KillCredited` event on killer entity | No system records kills to analytics (who killed what, when, where) |
| `DiedEvent` on player entity | No death analytics (cause of death, location, time since last death) |
| `QuestEventQueue` completion events | No quest funnel analysis (accept rate, completion rate, abandon rate, time-to-complete) |
| `CraftOutputGenerationSystem` crafting events | No craft success/failure rate tracking |
| `CurrencyInventory` gold/premium changes | No economy flow analysis (sources vs. sinks, inflation tracking) |
| `PlayerProgression` XP/level data | No leveling curve analysis (time-to-level, XP sources breakdown) |
| `SaveFileWriter` async I/O pattern | No analytics dispatch using same pattern |
| Unity Profiler (manual) | No automated FPS/frame-time/memory collection |

**The gap:** A designer cannot answer "What is the average time to reach level 10?", "What percentage of players complete the first quest?", "Where do players die most often?", or "Is weapon A overpowered compared to weapon B?". Playtesters generate valuable gameplay data that vanishes when the session ends. There is no A/B testing infrastructure to compare game balance changes. There are no privacy controls for any future data collection.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  AnalyticsProfile SO       DispatchTargetConfig SO      PrivacyPolicy SO
  (enabled categories,      (endpoint URL, API key,      (default consent,
   sample rates, flush)      batch size, retry)           retention period)
           |                       |                           |
           └────── AnalyticsBootstrapSystem ───────────────────┘
                   (loads from Resources/, creates singletons,
                    inits AnalyticsAPI + AnalyticsDispatcher,
                    follows PersistenceBootstrapSystem pattern)
                              |
                    MANAGED DATA LAYER (no ECS player components)
  AnalyticsConfig singleton   SessionState singleton
  (EnabledCategories,         (SessionId, StartTick,
   SampleRate, FlushInterval)  PlayerCount, Duration)
                              |
                    EVENT SOURCES (read-only -- no modifications to existing systems)
  KillCredited ──────────────+
  DiedEvent ─────────────────+
  CombatResultEvent ─────────+
  QuestEventQueue ───────────+
  CraftOutputGeneration ─────+──> Bridge Systems (read existing events)
  CurrencyInventory changes ─+        |
  LevelUpEvent ──────────────+        v
  VFXTelemetry ──────────────+   AnalyticsAPI.TrackEvent()
  AudioTelemetry ────────────+   (thread-safe ConcurrentQueue enqueue)
  FPS / Memory / Frame Time ─+
                              |
                    PRIVACY FILTER LAYER
                              |
  PrivacyFilter ─── checks consent level per event
  (strips PII if PersonalData consent not given,
   drops event if Analytics consent not given,
   Essential events always pass through)
                              |
                    DISPATCH LAYER (background thread)
                              |
  AnalyticsDispatcher ─── ConcurrentQueue<AnalyticsEvent>
  (follows SaveFileWriter pattern: background thread, batch flush)
           |
           ├── FileTarget (JSON lines → Application.persistentDataPath)  [always active as fallback]
           ├── HttpTarget (POST batches → configurable endpoint)         [optional]
           └── UnityAnalyticsTarget (Unity Analytics SDK wrapper)        [optional]
                              |
                    EDITOR TOOLING
                              |
  AnalyticsWorkstationModule ── live event stream viewer
  (filterable by category, session timeline, queue status, privacy sim)
```

### Data Flow (Kill Enemy --> Analytics Event --> Dispatch)

```
Frame N (Server):
  1. DeathTransitionSystem: Enemy dies, fires KillCredited on killer via EndSimulationECB

Frame N+1 (Server):
  2. XPAwardSystem: Reads KillCredited, awards XP (existing, unchanged)
  3. CombatAnalyticsBridgeSystem (PresentationSystemGroup, runs AFTER event processing):
     - Reads KillCredited on player entities via manual EntityQuery
     - Builds AnalyticsEvent { Category=Combat, Action="kill", Properties={enemyType, playerLevel, weapon, position} }
     - Calls AnalyticsAPI.TrackEvent(event)
     - AnalyticsAPI: checks AnalyticsConfig.EnabledCategories & Combat bit
     - If enabled: enqueues to ConcurrentQueue<AnalyticsEvent>

Background Thread (asynchronous):
  4. AnalyticsDispatcher: Wakes on flush interval (default 30s) or batch threshold (default 100 events)
     - Dequeues batch from ConcurrentQueue
     - Runs PrivacyFilter.ScrubBatch(events, consentLevel)
     - For each IAnalyticsTarget: target.SendBatch(scrubbedEvents)
       - FileTarget: appends JSON lines to analytics_{sessionId}.jsonl
       - HttpTarget: POST /v1/events with JSON body, retry on failure
       - UnityAnalyticsTarget: calls Unity.Services.Analytics.CustomEvent() per event
```

### Critical: No Existing System Modifications

All bridge systems are READ-ONLY observers. They read existing ECS events (`KillCredited`, `DiedEvent`, `CombatResultEvent`, `LevelUpEvent`, etc.) after the systems that produce and consume them have already run. No existing system is modified, reordered, or given new dependencies.

---

## ECS Components

### Singletons Only -- No Player Entity Components

**File:** `Assets/Scripts/Analytics/Components/AnalyticsComponents.cs`

```
AnalyticsConfig (IComponentData singleton)
  EnabledCategories   : uint      // Bitmask of AnalyticsCategory flags (default 0xFFF = all)
  SampleRate          : float     // Global sample rate 0.0-1.0 (default 1.0 = 100%)
  FlushIntervalSec    : float     // Seconds between background flush (default 30.0)

Total: 12 bytes
```

```
SessionState (IComponentData singleton)
  SessionId           : FixedString64Bytes  // UUID generated at session start
  StartTick           : uint                // Server tick at session start
  PlayerCount         : int                 // Current connected player count

Total: 76 bytes
```

**No player entity components.** Analytics is global/per-session state. All analytics data flows through managed `AnalyticsAPI` static class, not through ECS component data on individual entities. This means **zero impact on the player archetype** and **zero ghost replication overhead**.

---

## Core API

### AnalyticsAPI (Static, Managed, Thread-Safe)

**File:** `Assets/Scripts/Analytics/AnalyticsAPI.cs`

```csharp
public static class AnalyticsAPI
{
    // Internal: ConcurrentQueue<AnalyticsEvent> -- follows SaveFileWriter pattern
    // Internal: AnalyticsDispatcher background thread reference
    // Internal: Dictionary<string, object> _superProperties -- attached to every event

    /// <summary>
    /// Record a gameplay event. Thread-safe via ConcurrentQueue.Enqueue().
    /// Category string is converted to AnalyticsCategory bitmask for filtering.
    /// </summary>
    public static void TrackEvent(string category, string action, Dictionary<string, object> properties);

    /// <summary>
    /// Record a gameplay event using pre-built struct. Preferred for ECS bridge systems
    /// to avoid Dictionary allocation (properties encoded as FixedString512Bytes JSON).
    /// </summary>
    public static void TrackEvent(AnalyticsEvent evt);

    /// <summary>
    /// Begin a new analytics session. Generates SessionId, records start time.
    /// Called by AnalyticsBootstrapSystem on world creation.
    /// </summary>
    public static void StartSession(string playerId);

    /// <summary>
    /// End the current session. Records duration, disconnect reason.
    /// Called by AnalyticsFlushSystem.OnDestroy or explicit disconnect.
    /// </summary>
    public static void EndSession(string reason);

    /// <summary>
    /// Set properties attached to every subsequent event (e.g., buildVersion, platform, ABTestVariants).
    /// Merged into event properties at enqueue time.
    /// </summary>
    public static void SetSuperProperties(Dictionary<string, object> props);

    /// <summary>
    /// Set user privacy consent levels. Controls what data is collected and dispatched.
    /// Persisted to PlayerPrefs. Checked on every TrackEvent call.
    /// </summary>
    public static void SetPrivacyConsent(bool analytics, bool crashReports, bool personalData);

    /// <summary>
    /// Initialize the API. Called by AnalyticsBootstrapSystem.
    /// Creates ConcurrentQueue, starts AnalyticsDispatcher background thread.
    /// </summary>
    public static void Initialize(AnalyticsProfile profile);

    /// <summary>
    /// Shutdown. Flushes remaining events, stops background thread.
    /// Called on application quit (follows SaveFileWriter.FlushBlocking pattern).
    /// </summary>
    public static void Shutdown();

    /// <summary>
    /// Check if a specific category is enabled in the current config.
    /// Bridge systems call this before building expensive event structs.
    /// </summary>
    public static bool IsCategoryEnabled(AnalyticsCategory category);
}
```

### AnalyticsEvent (Struct)

**File:** `Assets/Scripts/Analytics/AnalyticsEvent.cs`

```csharp
public struct AnalyticsEvent
{
    public AnalyticsCategory Category;            // Bitmask category
    public FixedString64Bytes Action;             // e.g., "kill", "death", "level_up", "quest_complete"
    public FixedString64Bytes SessionId;          // Current session
    public FixedString64Bytes PlayerId;           // Player who triggered event (empty for global events)
    public long TimestampUtcMs;                   // Unix epoch UTC milliseconds
    public uint ServerTick;                       // NetworkTime.ServerTick at event time
    public FixedString512Bytes PropertiesJson;    // Compact JSON string for event-specific data
}
```

Using `FixedString512Bytes` for properties avoids managed Dictionary allocation in ECS bridge systems. The JSON is pre-formatted by the bridge system (e.g., `{"enemyType":3,"weapon":"Sword","pos":"12.5,0.0,8.3"}`). For managed callers using the `Dictionary<string, object>` overload, `AnalyticsAPI` serializes to JSON internally.

---

## Event Categories

**File:** `Assets/Scripts/Analytics/AnalyticsCategory.cs`

```csharp
[Flags]
public enum AnalyticsCategory : uint
{
    None         = 0x0,
    Session      = 0x1,       // Join, leave, duration, disconnect reason
    Combat       = 0x2,       // Kill, death, damage dealt/taken, weapon usage
    Economy      = 0x4,       // Currency gain/spend, trade, vendor transactions
    Progression  = 0x8,       // Level up, XP gain, talent/stat allocation
    Quest        = 0x10,      // Accept, complete, abandon, objective progress
    Crafting     = 0x20,      // Craft attempt, success, failure, recipe discovery
    Social       = 0x40,      // Party join/leave, chat (future), friend interactions
    Performance  = 0x80,      // FPS drops, memory spikes, load times, frame time
    UI           = 0x100,     // Menu opens, button clicks, screen time (future)
    World        = 0x200,     // Zone transitions, environmental deaths, voxel edits
    PvP          = 0x400,     // Match start/end, ranking changes (future EPIC 17.10)
    Custom       = 0x800,     // Game-specific events, designer-defined triggers

    All          = 0xFFF      // All categories enabled
}
```

Each bridge system checks `AnalyticsAPI.IsCategoryEnabled(category)` before building events. Disabled categories short-circuit at <0.001ms cost (single bitmask AND operation).

---

## ScriptableObjects

### AnalyticsProfile

**File:** `Assets/Scripts/Analytics/ScriptableObjects/AnalyticsProfile.cs`

```
[CreateAssetMenu(menuName = "DIG/Analytics/Analytics Profile")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| EnabledCategories | AnalyticsCategory | All (0xFFF) | Bitmask of active categories |
| GlobalSampleRate | float [Range(0,1)] | 1.0 | Probability of recording any event (1.0 = 100%) |
| CategorySampleRates | CategorySampleEntry[] | empty | Per-category override sample rates (e.g., Performance at 0.1 = 10% of frames) |
| FlushIntervalSeconds | float [Min(1)] | 30.0 | Background thread flush interval |
| BatchSize | int [Min(10)] | 100 | Max events per dispatch batch |
| RingBufferCapacity | int [Min(100)] | 10000 | Max events in memory before oldest dropped |
| DispatchTargets | DispatchTargetConfig[] | 1 FileTarget | Ordered list of dispatch targets |
| IncludeSuperProperties | bool | true | Attach build version, platform, AB variants to every event |
| EnableDebugLogging | bool | false | Log every tracked event to Unity console (editor only) |

### CategorySampleEntry

```
[Serializable]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Category | AnalyticsCategory | Session | Which category to override |
| SampleRate | float [Range(0,1)] | 1.0 | Category-specific sample rate |

### DispatchTargetConfig

**File:** `Assets/Scripts/Analytics/ScriptableObjects/DispatchTargetConfig.cs`

```
[CreateAssetMenu(menuName = "DIG/Analytics/Dispatch Target")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| TargetType | DispatchTargetType enum | File | File, Http, UnityAnalytics |
| Enabled | bool | true | Toggle without removing from list |
| EndpointUrl | string | "" | HTTP target URL (ignored for File/UnityAnalytics) |
| ApiKeyEncrypted | string | "" | Encrypted API key for HTTP auth (XOR with build-time salt) |
| BatchSize | int [Min(1)] | 50 | Events per HTTP request |
| MaxRetries | int [Range(0,5)] | 3 | Retry attempts on HTTP failure |
| RetryBaseDelayMs | int [Min(100)] | 1000 | Base delay for exponential backoff |
| TimeoutMs | int [Min(1000)] | 5000 | HTTP request timeout |
| FileNamePattern | string | "analytics_{sessionId}.jsonl" | File target output filename pattern |

### PrivacyPolicy

**File:** `Assets/Scripts/Analytics/ScriptableObjects/PrivacyPolicy.cs`

```
[CreateAssetMenu(menuName = "DIG/Analytics/Privacy Policy")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| DefaultAnalyticsConsent | bool | false | Default analytics opt-in state (GDPR: must default to false) |
| DefaultCrashReportConsent | bool | false | Default crash report opt-in state |
| DefaultPersonalDataConsent | bool | false | Default personal data opt-in state |
| EssentialEventsAlwaysOn | bool | true | Session start/end always recorded regardless of consent |
| DataRetentionDays | int [Min(1)] | 90 | Days before local analytics files auto-deleted |
| PiiFields | string[] | {"playerId","playerName","ip"} | Fields stripped when PersonalData consent not given |
| RequireExplicitConsent | bool | true | Block all non-essential analytics until consent dialog shown |
| ConsentDialogPrefab | GameObject | null | Reference to consent UI prefab (shown on first launch) |

---

## Systems

### System Execution Order

```
InitializationSystemGroup (Server|Local):
  AnalyticsBootstrapSystem               -- loads SOs from Resources/, creates singletons,
                                            inits AnalyticsAPI + AnalyticsDispatcher (runs once)

SimulationSystemGroup (Server|Local):
  [All gameplay systems run normally -- no analytics systems here to avoid ordering issues]
  SessionTrackingSystem                  -- tracks session duration, player count changes
                                            (OrderLast to read final state of frame)

PresentationSystemGroup (Server|Local):
  [After CombatEventCleanupSystem, after all event consumers]
  CombatAnalyticsBridgeSystem            -- reads kill/death/damage events -> AnalyticsAPI
  EconomyAnalyticsBridgeSystem           -- reads currency transactions -> AnalyticsAPI
  ProgressionAnalyticsBridgeSystem       -- reads level ups, XP grants -> AnalyticsAPI

PresentationSystemGroup (Client|Local):
  PerformanceMetricsSystem               -- FPS, frame time, memory sampling (every 60 frames)

EndSimulationSystemGroup (Server|Local):
  AnalyticsFlushSystem                   -- periodic flush trigger (timer check -> dispatcher signal)
```

### AnalyticsBootstrapSystem

**File:** `Assets/Scripts/Analytics/Systems/AnalyticsBootstrapSystem.cs`

- Managed SystemBase, `InitializationSystemGroup`, `ServerSimulation | LocalSimulation`
- Runs once, then `Enabled = false` (self-disables)
- Load `AnalyticsProfile` from `Resources/AnalyticsProfile`
- Load `PrivacyPolicy` from `Resources/PrivacyPolicy`
- Create `AnalyticsConfig` singleton entity with values from profile
- Create `SessionState` singleton entity with generated UUID
- Call `AnalyticsAPI.Initialize(profile)` -- starts background thread
- Call `AnalyticsAPI.StartSession(playerId)` -- records session start event
- Set super properties: `{ "buildVersion": Application.version, "platform": Application.platform, "unityVersion": Application.unityVersion }`
- Read privacy consent from `PlayerPrefs` (key: `"analytics_consent"`, `"crash_consent"`, `"personal_consent"`)
- Apply consent via `AnalyticsAPI.SetPrivacyConsent()`
- If `PrivacyPolicy.RequireExplicitConsent` and no stored consent: analytics remains disabled until consent UI grants permission
- Log: `"[Analytics] Initialized with {enabledCount} categories, sample rate {rate}, {targetCount} dispatch targets"`
- Follows `PersistenceBootstrapSystem` pattern exactly

### SessionTrackingSystem

**File:** `Assets/Scripts/Analytics/Systems/SessionTrackingSystem.cs`

- Managed SystemBase, `SimulationSystemGroup`, `ServerSimulation | LocalSimulation`, `[UpdateLast]`
- Reads `SessionState` singleton each frame
- Tracks elapsed time since session start (accumulated deltaTime)
- Detects player count changes (query `PlayerTag` entities, compare to `SessionState.PlayerCount`)
- On player join: `AnalyticsAPI.TrackEvent("Session", "player_join", { playerId, playerCount, sessionDuration })`
- On player leave: `AnalyticsAPI.TrackEvent("Session", "player_leave", { playerId, playerCount, disconnectReason, sessionDuration })`
- Updates `SessionState.PlayerCount` via `SetSingleton<SessionState>()`
- Performance: one EntityQuery count per frame (<0.01ms)

### CombatAnalyticsBridgeSystem

**File:** `Assets/Scripts/Analytics/Systems/CombatAnalyticsBridgeSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `ServerSimulation | LocalSimulation`
- `[UpdateAfter(typeof(CombatEventCleanupSystem))]` -- reads BEFORE cleanup destroys CRE entities (NOTE: must verify ordering; if CREs are already destroyed, read from cumulative stat trackers instead)
- Early-out: `if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Combat)) return;`
- Requires `CompleteDependency()` at top (async job safety from combat systems)

**Events recorded:**

| Event Source | Action | Properties |
|-------------|--------|------------|
| `KillCredited` on player | `"kill"` | enemyType, enemyLevel, playerLevel, weaponType, position, killStreak |
| `DiedEvent` on player | `"player_death"` | playerLevel, causeOfDeath (from last DamageEvent source), position, timeSinceLastDeath |
| `CombatResultEvent` entities (aggregated) | `"damage_summary"` | totalDamageDealt, totalDamageTaken, hitCount, missCount (sampled: 1 summary per 5 seconds, not per-hit) |

**Key pattern:** Uses manual `EntityQuery.ToEntityArray()` + `EntityQuery.ToComponentDataArray<T>()` -- NEVER `SystemAPI.Query` for transient types (lesson from CombatResultEvent/PendingCombatHit source-gen issues).

### EconomyAnalyticsBridgeSystem

**File:** `Assets/Scripts/Analytics/Systems/EconomyAnalyticsBridgeSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `ServerSimulation | LocalSimulation`
- Early-out: `if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Economy)) return;`
- Uses ECS change filter on `CurrencyInventory` to detect gold/premium/crafting changes
- On change: reads previous value (cached in managed dictionary by Entity) and current value
- Computes delta, records event:
  - `"currency_gain"` if delta > 0: { currencyType, amount, newBalance, source (inferred from frame context) }
  - `"currency_spend"` if delta < 0: { currencyType, amount, newBalance, spendType }

### ProgressionAnalyticsBridgeSystem

**File:** `Assets/Scripts/Analytics/Systems/ProgressionAnalyticsBridgeSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `ServerSimulation | LocalSimulation`
- Early-out: `if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Progression)) return;`
- Reads `LevelUpEvent` (IEnableableComponent) on player entities:
  - `"level_up"`: { newLevel, previousLevel, totalPlaytime, totalXPEarned }
- Uses change filter on `PlayerProgression` to detect XP changes:
  - `"xp_gain"`: sampled, records aggregate XP gained per flush interval (not per-kill)
- Reads stat allocation changes:
  - `"stat_allocate"`: { attribute, points, totalAllocated }

### PerformanceMetricsSystem

**File:** `Assets/Scripts/Analytics/Systems/PerformanceMetricsSystem.cs`

- Managed SystemBase, `PresentationSystemGroup`, `ClientSimulation | LocalSimulation`
- Early-out: `if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Performance)) return;`
- Frame-spread: only samples every 60 frames (`frameCount % 60 == 0`) to minimize overhead
- Reads `Unity.Profiling.Recorder` for frame time, or falls back to `Time.unscaledDeltaTime`
- Records:
  - Current FPS (1.0 / deltaTime)
  - Frame time in milliseconds
  - `System.GC.GetTotalMemory(false)` for managed memory
  - `Unity.Profiling.Profiler.GetTotalReservedMemoryLong()` for total memory
- Only tracks to analytics when anomalous:
  - FPS drops below threshold (default 20 FPS): `"fps_drop"` event
  - Memory exceeds threshold (default 2GB): `"memory_spike"` event
  - Frame time exceeds threshold (default 50ms): `"frame_hitch"` event
- Maintains rolling averages internally for smooth threshold comparison
- Performance: <0.02ms when sampling (every 60 frames), <0.001ms on skip frames

### AnalyticsFlushSystem

**File:** `Assets/Scripts/Analytics/Systems/AnalyticsFlushSystem.cs`

- Managed SystemBase, `EndSimulationSystemGroup`, `ServerSimulation | LocalSimulation`
- Accumulates timer (`_timeSinceFlush += deltaTime`)
- When timer >= `AnalyticsConfig.FlushIntervalSec`: signals `AnalyticsDispatcher` to flush
- Signal is a `ManualResetEventSlim.Set()` on the background thread (zero allocation)
- On `OnDestroy()`: calls `AnalyticsAPI.EndSession("shutdown")` + `AnalyticsAPI.Shutdown()` (blocking flush, same as `SaveFileWriter.FlushBlocking()`)
- Reads `SessionState` to record final session summary event on shutdown:
  - `"session_end"`: { totalDuration, playerCount, eventsRecorded, eventsDropped }

### Quest & Crafting Bridge Systems

**Note:** Quest and crafting analytics are handled by the `CombatAnalyticsBridgeSystem` pattern but listening to different event sources:

- **QuestAnalyticsBridge** (embedded in `CombatAnalyticsBridgeSystem` or separate system if complexity warrants):
  - Peeks `QuestEventQueue` (same pattern as AchievementTrackingSystem in EPIC 17.7)
  - Records: `"quest_accept"`, `"quest_complete"`, `"quest_abandon"` with questId, playerLevel, timeSinceAccept
- **CraftingAnalyticsBridge**:
  - Reads craft output entities with `CraftedItemTag`
  - Records: `"craft_success"` with recipeId, itemRarity, playerLevel, craftingSkill

These may be split into dedicated systems if the bridge logic becomes complex, but the initial implementation keeps them lightweight by sharing the same PresentationSystemGroup execution slot.

---

## Dispatch Pipeline

### AnalyticsDispatcher (Managed, Background Thread)

**File:** `Assets/Scripts/Analytics/AnalyticsDispatcher.cs`

```
AnalyticsDispatcher (class)
  ConcurrentQueue<AnalyticsEvent> _eventQueue     -- thread-safe, lock-free
  ManualResetEventSlim _flushSignal                -- wakes background thread
  IAnalyticsTarget[] _targets                      -- configured dispatch targets
  PrivacyFilter _privacyFilter                     -- scrubs events before dispatch
  bool _running                                    -- background thread loop flag
  Thread _backgroundThread                         -- dedicated dispatch thread

  Start(IAnalyticsTarget[] targets, PrivacyFilter filter)
    -- starts background thread loop
  Stop()
    -- sets _running = false, signals thread, joins with timeout
  FlushBlocking()
    -- drains queue synchronously on calling thread (shutdown only)
  SignalFlush()
    -- sets _flushSignal to wake background thread

  Background thread loop:
    while (_running):
      1. _flushSignal.Wait(timeout: FlushIntervalMs)
      2. Drain _eventQueue into List<AnalyticsEvent> batch (up to BatchSize)
      3. _privacyFilter.ScrubBatch(batch, currentConsentLevel)
      4. foreach target in _targets:
           try { target.SendBatch(batch.ToArray()); }
           catch { log warning, continue to next target }
      5. _flushSignal.Reset()
```

Follows `SaveFileWriter` pattern: ConcurrentQueue for thread-safe enqueue from main thread, dedicated background thread for I/O, `FlushBlocking()` for graceful shutdown.

### IAnalyticsTarget Interface

**File:** `Assets/Scripts/Analytics/Targets/IAnalyticsTarget.cs`

```csharp
public interface IAnalyticsTarget
{
    /// <summary>
    /// Human-readable name for logging and editor display.
    /// </summary>
    string TargetName { get; }

    /// <summary>
    /// Send a batch of events to this target. Called from background thread.
    /// Must be thread-safe. Should not throw -- failures logged internally.
    /// </summary>
    void SendBatch(AnalyticsEvent[] events);

    /// <summary>
    /// Initialize the target with configuration. Called once from main thread.
    /// </summary>
    void Initialize(DispatchTargetConfig config);

    /// <summary>
    /// Shutdown the target. Flush any internal buffers. Called from main thread.
    /// </summary>
    void Shutdown();
}
```

### FileTarget

**File:** `Assets/Scripts/Analytics/Targets/FileTarget.cs`

- Writes JSON lines format (one JSON object per line) to `Application.persistentDataPath/analytics/`
- Filename from `DispatchTargetConfig.FileNamePattern` with `{sessionId}` token replacement
- Append-only: opens `StreamWriter` with `append: true`, flushes after each batch
- File rotation: new file per session (sessionId in filename)
- Auto-cleanup: on Initialize, deletes files older than `PrivacyPolicy.DataRetentionDays`
- **Always active as fallback** -- even if HTTP target fails, local file captures all events

JSON line format:
```json
{"ts":1708700000000,"sid":"abc-123","cat":"Combat","act":"kill","pid":"player_1","tick":54321,"props":{"enemyType":3,"pos":"12.5,0.0,8.3"}}
```

### HttpTarget

**File:** `Assets/Scripts/Analytics/Targets/HttpTarget.cs`

- POST batches as JSON array to `DispatchTargetConfig.EndpointUrl`
- Headers: `Content-Type: application/json`, `Authorization: Bearer {decryptedApiKey}`
- Retry with exponential backoff: delay = `RetryBaseDelayMs * 2^attempt` (capped at 30s)
- Max retries from config (default 3)
- Timeout from config (default 5000ms)
- Uses `System.Net.Http.HttpClient` (shared, long-lived instance)
- On persistent failure (all retries exhausted): logs error, drops batch, increments `_droppedBatches` counter
- Connection errors do NOT block the dispatch thread -- fire-and-forget with retry

### UnityAnalyticsTarget

**File:** `Assets/Scripts/Analytics/Targets/UnityAnalyticsTarget.cs`

- Wrapper around `Unity.Services.Analytics` SDK (if present)
- Conditional compilation: `#if UNITY_ANALYTICS_AVAILABLE`
- Maps `AnalyticsEvent` to `CustomEvent(action, properties)`
- Falls back to no-op if Unity Analytics package not installed
- Useful for studios already using Unity dashboard

---

## A/B Testing Framework

### ABTestConfig

**File:** `Assets/Scripts/Analytics/ABTest/ABTestConfig.cs`

```
[CreateAssetMenu(menuName = "DIG/Analytics/AB Test Config")]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| TestId | string | "" | Unique test identifier (e.g., "weapon_balance_v2") |
| IsActive | bool | true | Enable/disable test without removing config |
| Variants | ABTestVariant[] | 2 entries | Variant definitions with weights |
| StartDate | string | "" | ISO 8601 date when test starts (empty = always active) |
| EndDate | string | "" | ISO 8601 date when test ends (empty = never expires) |

### ABTestVariant

```
[Serializable]
```

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| VariantName | string | "control" | Variant identifier (e.g., "control", "variant_a", "variant_b") |
| Weight | float [Min(0)] | 1.0 | Relative weight for random assignment |
| FeatureFlags | string[] | empty | Feature flag keys enabled for this variant |

### ABTestManager

**File:** `Assets/Scripts/Analytics/ABTest/ABTestManager.cs`

```csharp
public static class ABTestManager
{
    // Internal: Dictionary<string, string> _assignments -- testId -> variantName
    // Internal: ABTestConfig[] _activeTests -- loaded from Resources/

    /// <summary>
    /// Initialize with test configs. Assigns variants based on player ID hash.
    /// Persists assignments in PlayerPrefs for consistency across sessions.
    /// Called by AnalyticsBootstrapSystem.
    /// </summary>
    public static void Initialize(ABTestConfig[] tests, string playerId);

    /// <summary>
    /// Check if a player is in a specific variant of a test.
    /// Returns false if test is inactive or player not assigned.
    /// </summary>
    public static bool IsVariant(string testId, string variantName);

    /// <summary>
    /// Get the assigned variant for a test. Returns null if test inactive.
    /// </summary>
    public static string GetVariant(string testId);

    /// <summary>
    /// Get all active test assignments as a dictionary.
    /// Used by AnalyticsAPI.SetSuperProperties() to attach to every event.
    /// </summary>
    public static Dictionary<string, string> GetAllAssignments();

    /// <summary>
    /// Force a specific variant for a test (editor override / QA testing).
    /// Persists in PlayerPrefs with "_override" suffix.
    /// </summary>
    public static void ForceVariant(string testId, string variantName);

    /// <summary>
    /// Clear a forced variant override.
    /// </summary>
    public static void ClearOverride(string testId);

    /// <summary>
    /// Check if a feature flag is enabled for the current player (from any active test variant).
    /// </summary>
    public static bool IsFeatureEnabled(string featureFlagKey);
}
```

**Assignment algorithm:** Deterministic hash of `playerId + testId` mapped to variant by cumulative weight distribution. Same player always gets same variant for same test. Persisted in PlayerPrefs for session consistency.

**Super properties integration:** `AnalyticsBootstrapSystem` calls `AnalyticsAPI.SetSuperProperties({ "ab_tests": ABTestManager.GetAllAssignments() })` so every analytics event includes active test variant assignments.

---

## Privacy & Compliance

### Consent Levels

**File:** `Assets/Scripts/Analytics/Privacy/PrivacyFilter.cs`

```csharp
[Flags]
public enum PrivacyConsentLevel : byte
{
    None         = 0x0,   // Nothing collected (except Essential if configured)
    Essential    = 0x1,   // Session start/end only (always on if PrivacyPolicy.EssentialEventsAlwaysOn)
    Analytics    = 0x2,   // Gameplay events (kills, quests, economy) -- anonymized
    CrashReports = 0x4,   // Error and crash data with stack traces
    PersonalData = 0x8    // Player IDs, names, and other PII retained in events
}
```

### PrivacyFilter

```csharp
public class PrivacyFilter
{
    private PrivacyConsentLevel _currentConsent;
    private HashSet<string> _piiFields;    // From PrivacyPolicy.PiiFields

    /// <summary>
    /// Set current consent level. Called when player updates privacy preferences.
    /// </summary>
    public void SetConsent(PrivacyConsentLevel consent);

    /// <summary>
    /// Scrub a batch of events based on current consent level.
    /// - No Analytics consent: drops all non-Essential events
    /// - No PersonalData consent: strips PII fields (playerId, playerName, ip)
    /// - Essential events always pass through
    /// Returns filtered batch (may be smaller than input).
    /// </summary>
    public AnalyticsEvent[] ScrubBatch(AnalyticsEvent[] events);
}
```

### Privacy Behavior Matrix

| Consent Level | Session Events | Gameplay Events | Player ID in Events | Crash Reports |
|---------------|----------------|-----------------|---------------------|---------------|
| None (default) | Essential only (if configured) | BLOCKED | STRIPPED | BLOCKED |
| Analytics only | YES | YES (anonymized) | STRIPPED (hashed) | BLOCKED |
| Analytics + PersonalData | YES | YES | INCLUDED | BLOCKED |
| Analytics + CrashReports | YES | YES (anonymized) | STRIPPED | YES |
| All | YES | YES | INCLUDED | YES |

### Data Minimization

- When `PersonalData` consent is NOT given, `PrivacyFilter` replaces `PlayerId` with a one-way hash (SHA256 truncated to 16 chars) -- sufficient for aggregate analysis but not personally identifiable
- Player names are never included in analytics events (not stored in `AnalyticsEvent` struct)
- IP addresses are not collected by the analytics system (HTTP target does not log source IP)
- Local file target stores data on-device only -- no network transmission without explicit HTTP target configuration

### Local-Only Mode

When `Analytics` consent is not given:
- `AnalyticsAPI.TrackEvent()` immediately returns without enqueuing (zero cost)
- Essential events (session start/end) still recorded if `PrivacyPolicy.EssentialEventsAlwaysOn == true`
- All data stays on device in local file -- no HTTP dispatch
- `PerformanceMetricsSystem` still collects FPS/memory for local debug display but does NOT route through analytics pipeline

---

## Editor Tooling

### AnalyticsWorkstationModule

**File:** `Assets/Editor/AnalyticsWorkstation/AnalyticsWorkstationModule.cs`

- Menu: `DIG/Analytics Workstation`
- Sidebar + `IAnalyticsWorkstationModule` pattern (matches ProgressionWorkstation, PersistenceWorkstation, VFXWorkstation)

### Modules (6 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| Live Event Stream | `Modules/LiveEventStreamModule.cs` | Play-mode only: scrolling list of real-time analytics events. Category color coding. Filter by category bitmask dropdown. Search by action string. Pause/resume stream. "Copy JSON" button per event. Max 500 displayed events (ring buffer). Auto-scroll toggle. |
| Session Timeline | `Modules/SessionTimelineModule.cs` | Play-mode only: horizontal timeline bar showing session duration. Event markers color-coded by category. Hover to see event details. Zoom/pan controls. Kill/death/level-up events highlighted. Session summary panel: duration, event count, events per category. |
| Event Frequency | `Modules/EventFrequencyModule.cs` | Bar chart showing events per category over time. Configurable time window (1min, 5min, 15min, session). Useful for identifying event storms or dead periods. Refresh rate toggle (1s, 5s, manual). |
| Dispatch Queue | `Modules/DispatchQueueModule.cs` | Queue depth graph over time. Events pending, events dispatched, events dropped. Per-target status: last flush time, last error, batch count. Manual "Force Flush" button. Target health indicators (green/yellow/red). |
| Privacy Simulator | `Modules/PrivacySimulatorModule.cs` | Toggle consent levels in editor. Preview which events pass/fail privacy filter. Show PII scrubbing in action (before/after comparison). Test consent dialog flow. Reset stored consent. |
| A/B Test Override | `Modules/ABTestOverrideModule.cs` | List active tests with current variant assignment. Override dropdown per test. "Randomize All" button. Show feature flags enabled for current variant. Clear overrides. Export assignment JSON. |

---

## No ISaveModule

Analytics is fire-and-forget telemetry data. It is NOT game state and does NOT participate in the save/load pipeline (EPIC 16.15). Rationale:

- Analytics events are append-only -- no need to restore previous state on load
- Session data is per-session by design -- new session = new analytics stream
- A/B test assignments persist via PlayerPrefs (lightweight, not save-file dependent)
- Privacy consent persists via PlayerPrefs (same as above)
- Local analytics files persist independently in `Application.persistentDataPath/analytics/`

---

## Performance Budget

| System / Operation | Target | Burst | Notes |
|-------------------|--------|-------|-------|
| `AnalyticsBootstrapSystem` | N/A | No | Runs once at startup |
| `AnalyticsAPI.TrackEvent()` (per event) | < 0.01ms | No | ConcurrentQueue.Enqueue -- single atomic operation |
| `SessionTrackingSystem` | < 0.01ms | No | One EntityQuery count + timer comparison per frame |
| `CombatAnalyticsBridgeSystem` | < 0.05ms | No | Reads existing event queries (KillCredited, DiedEvent), builds 0-5 events per frame. Manual EntityQuery, not SystemAPI.Query |
| `EconomyAnalyticsBridgeSystem` | < 0.03ms | No | Change filter on CurrencyInventory (rarely dirty) |
| `ProgressionAnalyticsBridgeSystem` | < 0.03ms | No | Change filter on PlayerProgression + LevelUpEvent check |
| `PerformanceMetricsSystem` | < 0.02ms | No | Only samples every 60 frames. Skip frames: <0.001ms |
| `AnalyticsFlushSystem` | < 0.01ms | No | Timer comparison + ManualResetEventSlim.Set() |
| Background dispatch (FileTarget) | N/A | No | Background thread: ~1-5ms per batch write (zero main-thread cost) |
| Background dispatch (HttpTarget) | N/A | No | Background thread: ~10-100ms per HTTP request (zero main-thread cost) |
| **Total main-thread** | **< 0.15ms** | | All systems combined, worst-case frame with events |
| **Typical frame (no events)** | **< 0.03ms** | | Early-outs on empty EntityQueries + disabled categories |

### Memory Budget

- Ring buffer: 10,000 `AnalyticsEvent` structs at ~720 bytes each = ~7.2 MB max
- In practice, events flush every 30 seconds -- buffer typically holds <500 events (~360 KB)
- Background thread stack: ~1 MB
- FileTarget StreamWriter buffer: ~8 KB
- **Total steady-state: < 1 MB**

---

## File Summary

### New Files (14)

| # | Path | Type |
|---|------|------|
| 1 | `Assets/Scripts/Analytics/AnalyticsAPI.cs` | Static managed API (thread-safe, ConcurrentQueue) |
| 2 | `Assets/Scripts/Analytics/AnalyticsEvent.cs` | Event struct + AnalyticsCategory flags enum |
| 3 | `Assets/Scripts/Analytics/AnalyticsDispatcher.cs` | Background thread dispatcher (follows SaveFileWriter pattern) |
| 4 | `Assets/Scripts/Analytics/Targets/IAnalyticsTarget.cs` | Dispatch target interface |
| 5 | `Assets/Scripts/Analytics/Targets/FileTarget.cs` | JSON lines file writer |
| 6 | `Assets/Scripts/Analytics/Targets/HttpTarget.cs` | HTTP POST batch dispatcher with retry |
| 7 | `Assets/Scripts/Analytics/ABTest/ABTestManager.cs` | Static A/B test variant manager |
| 8 | `Assets/Scripts/Analytics/Privacy/PrivacyFilter.cs` | Consent-based event scrubbing + PrivacyConsentLevel enum |
| 9 | `Assets/Scripts/Analytics/Components/AnalyticsComponents.cs` | AnalyticsConfig + SessionState singletons (12 + 76 bytes) |
| 10 | `Assets/Scripts/Analytics/Systems/AnalyticsBootstrapSystem.cs` | SystemBase (InitializationSystemGroup, runs once) |
| 11 | `Assets/Scripts/Analytics/Systems/SessionTrackingSystem.cs` | SystemBase (SimulationSystemGroup, UpdateLast) |
| 12 | `Assets/Scripts/Analytics/Systems/CombatAnalyticsBridgeSystem.cs` | SystemBase (PresentationSystemGroup, reads kill/death/damage) |
| 13 | `Assets/Scripts/Analytics/ScriptableObjects/AnalyticsProfile.cs` | ScriptableObject (categories, sample rates, targets, flush interval) |
| 14 | `Assets/Editor/AnalyticsWorkstation/AnalyticsWorkstationModule.cs` | EditorWindow (6 tabs: live stream, timeline, frequency, queue, privacy, AB test) |

### Additional Files (if split for clarity)

| # | Path | Type |
|---|------|------|
| 15 | `Assets/Scripts/Analytics/Systems/EconomyAnalyticsBridgeSystem.cs` | SystemBase (PresentationSystemGroup, reads currency changes) |
| 16 | `Assets/Scripts/Analytics/Systems/ProgressionAnalyticsBridgeSystem.cs` | SystemBase (PresentationSystemGroup, reads level-ups/XP) |
| 17 | `Assets/Scripts/Analytics/Systems/PerformanceMetricsSystem.cs` | SystemBase (PresentationSystemGroup, Client only, FPS/memory) |
| 18 | `Assets/Scripts/Analytics/Systems/AnalyticsFlushSystem.cs` | SystemBase (EndSimulationSystemGroup, timer + flush signal) |
| 19 | `Assets/Scripts/Analytics/Targets/UnityAnalyticsTarget.cs` | Optional Unity Analytics SDK wrapper |
| 20 | `Assets/Scripts/Analytics/ABTest/ABTestConfig.cs` | ScriptableObject (test definitions + variants) |
| 21 | `Assets/Scripts/Analytics/ScriptableObjects/DispatchTargetConfig.cs` | ScriptableObject (endpoint config) |
| 22 | `Assets/Scripts/Analytics/ScriptableObjects/PrivacyPolicy.cs` | ScriptableObject (consent defaults, PII fields, retention) |
| 23 | `Assets/Scripts/Analytics/DIG.Analytics.asmdef` | Assembly definition |

### Modified Files

None. All analytics systems are read-only observers of existing events. No existing system, component, or prefab is modified.

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/AnalyticsProfile.asset` |
| 2 | `Resources/PrivacyPolicy.asset` |
| 3 | `Resources/ABTests/` (folder for ABTestConfig assets) |

---

## Cross-EPIC Integration

| Source | EPIC | Event Observed | Analytics Response |
|--------|------|----------------|-------------------|
| `VFXTelemetry` | 16.7 | Per-frame/session VFX counters | PerformanceMetricsSystem samples VFXTelemetry.TotalCulled for budget overrun alerts |
| `AudioTelemetry` | 15.27 | Session audio counters | PerformanceMetricsSystem samples AudioTelemetry.PlaybackFailuresThisSession for error rate tracking |
| `ResourcePool` / `ResourceModifierApplySystem` | 16.8 | Resource consumption changes | EconomyAnalyticsBridgeSystem tracks resource gain/drain rates via change filter |
| `QuestEventQueue` | 16.12 | Quest accept/complete/fail events | CombatAnalyticsBridgeSystem reads queue for quest funnel analysis (accept rate, completion rate, time-to-complete) |
| `CraftOutputGenerationSystem` | 16.13 | Craft output entities | CombatAnalyticsBridgeSystem reads crafted items for craft success/failure rate tracking |
| `PlayerProgression` / `LevelUpEvent` | 16.14 | XP gains, level-ups | ProgressionAnalyticsBridgeSystem records leveling curve data (time-to-level, XP sources) |
| Party system | 17.2 | Party join/leave events | SessionTrackingSystem records social events (party size, duration, group composition) |
| Trading system | 17.3 | Trade completion events | EconomyAnalyticsBridgeSystem records trade volume, item values, price trends |
| PvP system | 17.10 | Match start/end, ranking changes | Dedicated PvP analytics bridge (future: match balance data, win rates, weapon usage distribution) |
| Anti-Cheat system | 17.11 | Suspicious activity flags | CombatAnalyticsBridgeSystem records flagged events with elevated severity for manual review |
| Achievement system | 17.7 | Achievement unlock events | ProgressionAnalyticsBridgeSystem records achievement completion rates and unlock timing |

---

## Multiplayer

### Server-Authoritative Analytics

- ALL analytics bridge systems run on server (`ServerSimulation | LocalSimulation`). Server has authoritative game state -- analytics data is trustworthy.
- `PerformanceMetricsSystem` runs on client (`ClientSimulation | LocalSimulation`) -- client-side performance data is inherently local.
- Client-side events (UI interactions, local performance) use `AnalyticsAPI.TrackEvent()` directly from MonoBehaviours -- no ECS bridge needed.

### Session Scope

- **Listen server:** Single session tracking all players. SessionId generated by host. All kill/death/economy events attributed to specific players via PlayerId.
- **Dedicated server:** Same model -- server tracks all connected players.
- **Single player:** Same pipeline, PlayerCount always 1.

### Network Zero-Footprint

- Analytics adds ZERO network traffic between client and server.
- No RPCs for analytics events.
- No ghost-replicated analytics components.
- Server records its own events; clients record their own local events (performance only).
- Dispatch to HTTP targets is server-side only (clients dispatch to local file only, unless explicitly configured).

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `AnalyticsConfig` singleton | 12 bytes | Singleton entity (NOT on player) |
| `SessionState` singleton | 76 bytes | Singleton entity (NOT on player) |
| **Total on player entity** | **0 bytes** | **NONE** |

Analytics uses exclusively global singletons and static managed classes. No components are added to the player entity. No ghost replication overhead. No impact on the 16KB archetype limit.

---

## Extensibility

- **New event categories:** Add new bit to `AnalyticsCategory` enum, create bridge system, register in `AnalyticsProfile.EnabledCategories`
- **New dispatch targets:** Implement `IAnalyticsTarget`, add to `AnalyticsProfile.DispatchTargets` list. Examples: Amazon Kinesis, Google BigQuery, custom data warehouse
- **Custom events from gameplay code:** Call `AnalyticsAPI.TrackEvent("Custom", "my_event", props)` from any managed code -- no ECS system needed
- **Heatmaps:** Post-process local `.jsonl` files to extract position data from kill/death events. Future editor tool or external visualization
- **Funnel analysis:** Query `.jsonl` files for event sequences (quest_accept -> quest_complete) with timestamps. Future editor module
- **Real-time dashboards:** HttpTarget POST to Grafana/Datadog/custom dashboard backend

---

## Backward Compatibility

| Feature | Default | Effect |
|---------|---------|--------|
| No `AnalyticsProfile` in Resources | Bootstrap logs warning, disables all systems | Zero overhead -- no analytics collected |
| Analytics consent not granted | Essential events only (or none) | Gameplay unaffected, minimal local logging |
| HTTP endpoint unreachable | Retries exhausted, batch dropped, local file unaffected | FileTarget always succeeds as fallback |
| Unity Analytics package not installed | `UnityAnalyticsTarget` compiles out via `#if` | No errors, no dependency |
| No AB tests configured | `ABTestManager.GetAllAssignments()` returns empty | Super properties have no AB test data |
| Existing `VFXTelemetry` / `AudioTelemetry` | Unchanged, still functional | Analytics bridge reads them optionally; they continue serving debug workstations independently |

---

## Verification Checklist

### Core Pipeline
- [ ] `AnalyticsBootstrapSystem` loads `AnalyticsProfile` from Resources, creates singletons, inits `AnalyticsAPI`
- [ ] `AnalyticsBootstrapSystem` self-disables after first run (`Enabled = false`)
- [ ] `AnalyticsAPI.Initialize()` starts background dispatch thread
- [ ] `AnalyticsAPI.TrackEvent()` enqueues event to ConcurrentQueue (verify via queue depth counter)
- [ ] Background thread wakes on flush interval, drains queue, dispatches to targets
- [ ] `AnalyticsAPI.Shutdown()` calls `FlushBlocking()` -- all pending events written before exit

### Event Recording
- [ ] Kill enemy: `CombatAnalyticsBridgeSystem` records "kill" event with enemyType, playerLevel, position
- [ ] Player death: "player_death" event recorded with causeOfDeath, position
- [ ] Level up: `ProgressionAnalyticsBridgeSystem` records "level_up" with newLevel, totalPlaytime
- [ ] Quest complete: "quest_complete" event with questId, timeSinceAccept
- [ ] Craft item: "craft_success" event with recipeId, itemRarity
- [ ] Currency change: `EconomyAnalyticsBridgeSystem` records "currency_gain" or "currency_spend"
- [ ] Category disabled: bridge system early-outs (< 0.001ms), no event enqueued
- [ ] Global sample rate 0.5: approximately 50% of events recorded

### Dispatch Targets
- [ ] FileTarget: `.jsonl` file appears in `Application.persistentDataPath/analytics/`
- [ ] FileTarget: each line is valid JSON with ts, sid, cat, act, props fields
- [ ] FileTarget: new file created per session (sessionId in filename)
- [ ] FileTarget: files older than `DataRetentionDays` auto-deleted on startup
- [ ] HttpTarget: POST request sent with JSON array body to configured endpoint
- [ ] HttpTarget: retry with exponential backoff on 500/timeout (verify 3 attempts)
- [ ] HttpTarget: persistent failure drops batch, logs error, does not block thread
- [ ] UnityAnalyticsTarget: `CustomEvent()` called per event (if package installed)

### Session Tracking
- [ ] Session start: "session_start" event recorded with sessionId, playerId, buildVersion
- [ ] Player join: "player_join" event with updated playerCount
- [ ] Player leave: "player_leave" event with disconnectReason
- [ ] Session end: "session_end" event with totalDuration, eventsRecorded, eventsDropped
- [ ] `SessionState.PlayerCount` accurately reflects connected players

### Performance Metrics
- [ ] `PerformanceMetricsSystem` samples every 60 frames (not every frame)
- [ ] FPS drop below 20: "fps_drop" event recorded
- [ ] Memory spike above threshold: "memory_spike" event recorded
- [ ] Skip frames: system cost < 0.001ms
- [ ] Sample frames: system cost < 0.02ms

### A/B Testing
- [ ] `ABTestManager.Initialize()` assigns deterministic variant based on playerId hash
- [ ] Same playerId always gets same variant for same test
- [ ] `ABTestManager.IsVariant("test_1", "variant_a")` returns correct boolean
- [ ] `ABTestManager.IsFeatureEnabled("feature_flag_key")` checks all active test variants
- [ ] Variant assignment persisted in PlayerPrefs across sessions
- [ ] `ForceVariant()` overrides assignment (editor/QA)
- [ ] All analytics events include AB test assignments as super properties

### Privacy & Compliance
- [ ] Default consent is `None` (GDPR compliant -- no collection without explicit opt-in)
- [ ] `Essential` events (session start/end) always recorded if `EssentialEventsAlwaysOn == true`
- [ ] `Analytics` consent granted: gameplay events recorded with hashed PlayerId
- [ ] `PersonalData` consent NOT granted: PlayerId replaced with SHA256 hash in all events
- [ ] `PersonalData` consent granted: raw PlayerId included
- [ ] `AnalyticsAPI.TrackEvent()` returns immediately if no `Analytics` consent (zero cost)
- [ ] `PrivacyFilter.ScrubBatch()` strips PII fields from events before dispatch
- [ ] Consent changes take effect immediately (no restart required)
- [ ] Consent persisted in PlayerPrefs

### Editor Tooling
- [ ] Analytics Workstation: Live Event Stream shows events in real-time during play mode
- [ ] Analytics Workstation: Category filter hides/shows events by type
- [ ] Analytics Workstation: Session Timeline shows event markers on time bar
- [ ] Analytics Workstation: Dispatch Queue shows queue depth and target health
- [ ] Analytics Workstation: Privacy Simulator toggles consent and shows before/after scrubbing
- [ ] Analytics Workstation: A/B Test Override changes variant assignment during play mode

### Performance
- [ ] No-events frame: all bridge systems combined < 0.03ms (early-outs)
- [ ] Kill event frame: total analytics overhead < 0.15ms
- [ ] Ring buffer capacity 10,000 events: no crash, oldest dropped when full
- [ ] Background thread flush: zero main-thread cost (verify in Profiler)
- [ ] Memory steady-state < 1 MB

### Integration
- [ ] No existing system modified (pure observer pattern)
- [ ] No new components on player entity (0 bytes archetype impact)
- [ ] No new ghost-replicated components
- [ ] No new IBufferElementData on ghost-replicated entities
- [ ] No network traffic for analytics (no RPCs, no ghost fields)
- [ ] `VFXTelemetry` and `AudioTelemetry` continue to work independently
- [ ] Disable all analytics: `AnalyticsProfile.EnabledCategories = None` -- zero overhead
