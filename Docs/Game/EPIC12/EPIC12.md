# EPIC 12: Scar Map

**Status**: Planning
**Priority**: High — Expedition narrative & decision support
**Dependencies**: Framework: Roguelite/ (RunStatistics), Analytics/, UI/; EPIC 4 (Districts), EPIC 2 (Death), EPIC 5 (Echoes), EPIC 3 (Front)
**GDD Sections**: 11.1-11.3 The Scar Map

---

## Problem

Players need a persistent visual record of their expedition — every body left behind, every echo spawned, every Front phase, every decision. The Scar Map serves two roles: tactical tool (where should I go next? what's waiting in that district?) and narrative artifact (end-of-run story, shareable, memorable). It's the player's expedition autobiography.

---

## Overview

The Scar Map is a stylized graph view of the expedition showing all districts visited, overlaid with event markers. It updates in real-time as the player makes decisions, dies, completes objectives, and triggers events. At expedition's end, it becomes a summary artifact preserved in the Compendium.

---

## Sub-Epics

### 12.1: Scar Map Data Model
What the map tracks.

- **ScarMapState** (per-expedition):
  - List of **ScarMapMarker** entries: (DistrictId, ZoneId, MarkerType, Metadata, Timestamp)
- **MarkerType enum** (GDD §11.1):
  - **Skull**: body left behind (metadata: gear inventory preview)
  - **EchoSpiral**: active echo mission (metadata: reward type, difficulty)
  - **FrontGradient**: current Front phase per zone (green→yellow→orange→red)
  - **BleedTendril**: district bleed connections (source→target)
  - **Star**: seeded events (merchants, vaults, legendary limbs)
  - **RevivalNode**: available bodies for resurrection (metadata: quality tier)
  - **RivalMarker**: rival operator last known position and status (EPIC 11)
  - **Completed**: completed objectives (for narrative record)
  - **Death**: where player died (distinct from Skull — shows the death event)
- **Marker persistence**: stored in DistrictSaveState (EPIC 4.2)
- **Real-time updates**: markers added/removed as events occur during gameplay

### 12.2: Scar Map Rendering
Visual presentation.

- **Graph view**: stylized district silhouettes connected by gate lines
  - Not a full map — recognizable district shapes with icons overlaid (GDD §11.2)
  - Districts colored by overall Front phase (dominant zone state)
  - Gates shown as connection lines (open vs locked)
- **Marker rendering**: icons at approximate zone positions within district silhouette
  - Skull icons: hover shows gear inventory preview
  - Echo spirals: animated, pulsing glow
  - Front gradient: color wash over district zones
  - Bleed tendrils: animated lines between districts
  - Star markers: twinkle animation
- **Zoom levels**: expedition overview (all districts) → district detail (zone-level markers)
- **Art style**: data-punk aesthetic — circuitry lines, holographic overlay feel

### 12.3: Scar Map as Tactical Tool
Using the map for decision-making.

- **Gate Screen integration** (EPIC 6): mini Scar Map on gate selection screen
  - Shows at-a-glance: where are my bodies? What echoes are active? Which Fronts are bad?
  - Helps inform forward vs backtrack decisions
- **In-game access**: pause menu → Scar Map (full view)
- **District hover**: shows summary — Front phase, active echoes, bodies, events
- **Route planning**: player can see the consequences of going forward vs back
  - "District 3 has my best weapon on a body, but it's at Front Phase 3"
  - "District 5 has 2 echoes guarding a boss counter token I need"

### 12.4: End-of-Run Scar Map
The narrative artifact.

- **Expedition summary screen** (GDD §11.3):
  - Full Scar Map with timeline of events
  - Deaths, revivals, backtracking paths, echo completions — all visualized
  - Run statistics overlay (kills, time, districts cleared, deaths)
- **Scar Map preservation**: saved to Compendium as record of past expeditions
  - Players can screenshot/share — every one tells a different story
  - View past Scar Maps from Compendium UI
- **Cross-expedition use**: past Scar Map data feeds meta-expedition rivals (EPIC 11.4)

### 12.5: Scar Map Procgen Integration
How it works with deterministic generation.

- **Seed-based rendering** (GDD §11.2):
  - Districts generate from fixed seed → deterministic zone IDs
  - Scar Map stores markers at zone IDs, not world coordinates
  - Re-entering district: same layout from seed → markers land in correct zones
- **Storage is lightweight**: list of event markers per district + Front phase state
  - No geometry stored — only events at (district_id, zone_id, event_type, metadata)

---

## Framework Integration Points

| Framework System | Integration |
|---|---|
| Roguelite/ (RunStatistics) | Kill counts, time, zones cleared feed summary |
| Analytics/ | Scar Map data doubles as analytics events |
| UI/ | Full-screen overlay UI system |
| Persistence/ | Scar Map saved with expedition and in Compendium |
| Voxel/ (scar mapping) | May leverage existing voxel scar map for visual rendering |

---

## Sub-Epic Dependencies

| Sub-Epic | Requires | Optional |
|---|---|---|
| 12.1 (Data Model) | EPIC 4 (districts as foundation) | — |
| 12.2 (Rendering) | 12.1 | — |
| 12.3 (Tactical Tool) | 12.1, 12.2 | EPIC 6 (gate integration) |
| 12.4 (End-of-Run) | 12.1, 12.2 | EPIC 9 (Compendium storage) |
| 12.5 (Procgen Integration) | 12.1, EPIC 4.6 (seed) | — |

---

## Vertical Slice Scope

- 12.1 (data model) + 12.2 (rendering) basic version required for GDD §17.4
- 12.3 (tactical) at least gate screen mini-map
- 12.4 (end-of-run) basic summary screen
- 12.5 (procgen) required for cross-district marker placement

---

## Tooling & Quality

| Sub-Epic | BlobAsset | Validation | Editor Tool | Live Tuning | Debug Viz | Sim/Test |
|----------|-----------|------------|-------------|-------------|-----------|----------|
| 12.1 Data Model | ScarMapMarkerRenderBlob (icon atlas indices, tints, pulse speeds) | Marker bounds, duplicate hash, metadata range | Scar Map Preview Window (render without play mode, marker placement with snap-to-zone) | — | Per-layer overlay toggles (F5–F10) | 100-marker rendering perf test (<2ms) |
| 12.2 Rendering | (shares 12.1 blob) | — | (shares 12.1 preview window) | Icon scale, zoom scale, district spacing, Front colors, circuit opacity, flicker rate | Layout AABB wireframes, zone positions, icon stacking offsets | Force-directed layout overlap/determinism, icon stacking |
| 12.3 Tactical | — | — | — | Mini-map rect, hop radius, icon scale | Viewport rect, route highlight path, risk assessment breakdown | — |
| 12.4 End-of-Run | — | — | — | — | Timeline entry inspector, chapter breaks, route replay | Timeline ordering, chapter breaks, statistics accuracy, Compendium cap |
| 12.5 Procgen | — | Zone position bounds, graph hash consistency, CRC32 tamper, persistence round-trip | — | — | — | Determinism, seed variation, persistence round-trip, CRC32, storage budget, re-entry preservation |
