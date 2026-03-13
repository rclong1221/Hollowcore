# EPIC 12.4: End-of-Run Scar Map

**Status**: Planning
**Epic**: EPIC 12 — Scar Map
**Priority**: Medium — Narrative artifact and run summary
**Dependencies**: EPIC 12.1 (Scar Map Data Model), EPIC 12.2 (Scar Map Rendering), Framework: Roguelite/ (RunStatistics), UI/, Persistence/; Optional: EPIC 9 (Compendium storage)

---

## Overview

When an expedition ends — through death, extraction, or abandonment — the Scar Map transforms into a narrative summary screen. The full expedition map displays with a timeline of events, statistics overlay, and the complete marker history. This end-of-run screen serves as the player's expedition autobiography: every death, every echo completed, every district traversed, every rival encountered. The Scar Map is preserved in the Compendium for future viewing and can be captured as a screenshot for sharing. Past Scar Map data also feeds the meta-expedition rival system (EPIC 11.4).

---

## Component Definitions

### ScarMapSummaryState (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapSummaryComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.ScarMap
{
    public enum ExpeditionEndReason : byte
    {
        Death = 0,           // Total party wipe
        Extraction = 1,      // Successfully reached extraction point
        Abandonment = 2      // Player quit/disconnected
    }

    /// <summary>
    /// State for the end-of-run Scar Map summary screen.
    /// Created when expedition ends, consumed by summary renderer.
    /// </summary>
    public struct ScarMapSummaryState : IComponentData
    {
        /// <summary>How the expedition ended.</summary>
        public ExpeditionEndReason EndReason;

        /// <summary>Expedition ID for persistence.</summary>
        public int ExpeditionId;

        /// <summary>Expedition seed for Compendium recreation.</summary>
        public uint ExpeditionSeed;

        /// <summary>Total real-world time of the expedition in seconds.</summary>
        public float TotalTimeSeconds;

        /// <summary>Total gate transitions (expedition length).</summary>
        public int TotalTransitions;

        /// <summary>Whether the summary has been saved to Compendium.</summary>
        public bool IsSavedToCompendium;

        /// <summary>Whether the player has dismissed the summary screen.</summary>
        public bool IsDismissed;

        /// <summary>Current timeline playback position (0-1 normalized).</summary>
        public float TimelinePosition;

        /// <summary>Whether timeline is auto-playing.</summary>
        public bool TimelineAutoPlay;
    }
}
```

### ScarMapRunStatistics (IComponentData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapSummaryComponents.cs
using Unity.Entities;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Aggregated statistics for the end-of-run display.
    /// Computed from RunStatistics (framework) + Scar Map markers.
    /// </summary>
    public struct ScarMapRunStatistics : IComponentData
    {
        // From RunStatistics (Roguelite/ framework)
        public int TotalKills;
        public int TotalDeaths;
        public int DistrictsVisited;
        public int DistrictsCleared;

        // From Scar Map markers
        public int EchoesCompleted;
        public int EchoesIgnored;
        public int BodiesRecovered;
        public int BodiesLeft;
        public int RivalEncounters;
        public int ObjectivesCompleted;

        // Computed
        public int PeakFrontPhase;
        public int BacktrackCount;    // Times player returned to a previous district
        public int UniqueZonesExplored;
        public float CompletionPercentage; // Objectives completed / total available
    }
}
```

### ScarMapTimelineEntry (IBufferElementData)

```csharp
// File: Assets/Scripts/ScarMap/Components/ScarMapSummaryComponents.cs
using Unity.Entities;
using Unity.Collections;

namespace Hollowcore.ScarMap
{
    /// <summary>
    /// Ordered timeline of significant expedition events for playback.
    /// Built from ScarMapMarker buffer sorted by Timestamp.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ScarMapTimelineEntry : IBufferElementData
    {
        /// <summary>Gate transition number when this event occurred.</summary>
        public int Timestamp;

        /// <summary>District where event happened.</summary>
        public int DistrictId;

        /// <summary>Marker type of the event.</summary>
        public MarkerType EventType;

        /// <summary>Brief description for timeline display.</summary>
        public FixedString128Bytes Description;

        /// <summary>Whether this is a "chapter break" (district change = new chapter).</summary>
        public bool IsChapterBreak;
    }
}
```

---

## Systems

### ScarMapSummarySystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapSummarySystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Assembles the end-of-run summary when expedition ends:
//   1. On expedition end event:
//      a. Create ScarMapSummaryState with EndReason and expedition metadata
//      b. Read RunStatistics from Roguelite/ framework → populate ScarMapRunStatistics
//      c. Read ScarMapMarker buffer → compute marker-derived statistics
//      d. Build ScarMapTimelineEntry buffer:
//         - Sort markers by Timestamp
//         - Generate descriptions per MarkerType:
//           * Skull: "Left a body in District 3 — Tier 2 gear"
//           * EchoSpiral: "Echo discovered in District 5 — Rare reward"
//           * Completed: "Completed echo in District 5"
//           * Death: "Died in District 7 — Front Phase 3"
//           * RivalMarker: "Encountered Chrome Dogs in District 4"
//         - Insert chapter breaks at district transitions
//      e. Set TimelineAutoPlay = true (auto-scroll through events)
//   2. Configure ScarMapViewState for summary mode:
//      a. ZoomLevel = ExpeditionOverview
//      b. IsVisible = true
//      c. Full interactivity enabled
```

### ScarMapTimelineRenderer

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapTimelineRenderer.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: ScarMapSummarySystem
//
// Renders the timeline overlay on the end-of-run Scar Map:
//   1. Display timeline bar at bottom of screen
//   2. Timeline scrubbing:
//      a. Auto-play: advance TimelinePosition over TotalTransitions duration
//      b. Manual: click/drag on timeline bar
//      c. At each position, highlight markers up to that Timestamp
//         - Earlier markers: full opacity
//         - Future markers: hidden or ghosted
//      d. Current chapter label shows district name
//   3. Event callouts:
//      a. As timeline passes each entry, show brief callout animation
//      b. Callout appears near the marker's district on the Scar Map
//      c. Description text fades in, holds, fades out
//   4. Path tracing:
//      a. Draw animated line showing player's route through districts
//      b. Line grows as timeline advances
//      c. Backtrack segments drawn in different color (amber)
//   5. Controls:
//      a. Play/Pause button
//      b. Speed: 1x, 2x, 4x
//      c. Skip to end
//      d. Click on timeline entry to jump to that moment
```

### ScarMapCompendiumSaveSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapCompendiumSaveSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: ScarMapSummarySystem
//
// Persists the Scar Map to the Compendium:
//   1. On summary screen display (ScarMapSummaryState.IsSavedToCompendium == false):
//      a. Serialize ScarMapState + ScarMapMarker buffer to compact binary
//      b. Include ScarMapRunStatistics and ScarMapTimelineEntry buffer
//      c. Include ExpeditionSeed for potential graph recreation
//      d. Write to Compendium storage via Persistence/ framework
//      e. Set IsSavedToCompendium = true
//   2. Compendium storage format:
//      a. ExpeditionId (key)
//      b. ExpeditionSeed
//      c. EndReason
//      d. RunStatistics blob
//      e. MarkerArray blob (variable length)
//      f. TimelineArray blob (variable length)
//   3. Storage cap: keep last 20 expedition Scar Maps (oldest evicted)
```

### ScarMapScreenshotSystem

```csharp
// File: Assets/Scripts/ScarMap/Systems/ScarMapScreenshotSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Screenshot/share capability:
//   1. On screenshot keybind (during summary screen):
//      a. Set ScarMapViewState.AnimationsEnabled = false (freeze for clean capture)
//      b. Set timeline to end position (all markers visible)
//      c. Render Scar Map to RenderTexture at high resolution
//      d. Overlay run statistics text
//      e. Save to platform screenshot folder
//      f. Re-enable animations
//   2. Share integration (platform-dependent):
//      a. Copy screenshot path to clipboard
//      b. Fire platform share intent if available
```

---

## Setup Guide

1. **Add ScarMapSummaryComponents.cs** to `Assets/Scripts/ScarMap/Components/`
2. **Create summary screen prefab**: `Assets/Prefabs/UI/ScarMap/ScarMapSummary.prefab`
   - Full-screen overlay with Scar Map (reuses EPIC 12.2 renderer)
   - Timeline bar at bottom: scrubber, play/pause, speed controls
   - Statistics panel: left sidebar with run stats
   - "Save to Compendium" button (auto-saves but manual button for confirmation)
   - "Screenshot" button
   - "Continue" button to dismiss
3. **Wire expedition end event**: subscribe to death/extraction/abandonment events
4. **Wire RunStatistics integration**: read from Roguelite/ framework RunStatistics singleton
5. **Wire Compendium integration**: Persistence/ framework Compendium save slot
6. **Configure screenshot path**: platform-specific (Application.persistentDataPath/Screenshots/)
7. **Add assembly references** to `Hollowcore.Roguelite`, `Hollowcore.Persistence`, `Hollowcore.UI`

---

## Verification

- [ ] Summary screen appears on expedition death
- [ ] Summary screen appears on expedition extraction
- [ ] Summary screen appears on expedition abandonment
- [ ] ScarMapRunStatistics correctly aggregates kills, deaths, districts, echoes
- [ ] Timeline entries generated in correct chronological order
- [ ] Chapter breaks inserted at district transitions
- [ ] Timeline auto-play advances through events at readable pace
- [ ] Timeline scrubbing shows/hides markers based on position
- [ ] Event callouts animate near correct districts
- [ ] Route path traces player's expedition journey
- [ ] Backtrack segments rendered in amber/distinct color
- [ ] Timeline controls (play/pause/speed/skip) functional
- [ ] Statistics overlay displays correctly alongside Scar Map
- [ ] Scar Map saved to Compendium on summary display
- [ ] Compendium caps at 20 stored Scar Maps (oldest evicted)
- [ ] Screenshot captures clean image with statistics overlay
- [ ] "Continue" button dismisses summary and returns to meta-game
- [ ] Past Scar Maps viewable from Compendium UI

---

## Debug Visualization

```csharp
// File: Assets/Scripts/ScarMap/Debug/ScarMapSummaryDebug.cs
// Development builds only:
//   - Timeline entry inspector: list all ScarMapTimelineEntry entries with timestamps
//   - Statistics validation: show computed vs expected values side-by-side
//   - Chapter break markers: vertical lines on timeline bar
//   - Route path replay: step through path one segment at a time
```

---

## Simulation & Testing

```csharp
// File: Assets/Tests/ScarMap/ScarMapSummaryTest.cs
// [Test] TimelineGeneration_OrderedByTimestamp
//   Create 20 markers with randomized timestamps, run ScarMapSummarySystem,
//   verify ScarMapTimelineEntry buffer is sorted ascending by Timestamp.
//
// [Test] ChapterBreaks_InsertedAtDistrictTransitions
//   Create markers spanning 4 districts, verify IsChapterBreak=true at each
//   district boundary in the timeline.
//
// [Test] RunStatistics_AggregationAccuracy
//   Create known marker set (3 skulls, 2 echoes completed, 1 rival),
//   verify ScarMapRunStatistics matches exactly.
//
// [Test] CompendiumStorage_Cap20
//   Save 25 expeditions, verify only the 20 most recent remain.
```
