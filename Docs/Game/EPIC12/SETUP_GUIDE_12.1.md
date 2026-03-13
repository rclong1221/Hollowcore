# EPIC 12.1 Setup Guide: Scar Map Data Model & Marker Configuration

**Status:** Planned
**Requires:** EPIC 4 (Districts as foundation), Framework: Roguelite/ (RunStatistics), Persistence/

---

## Overview

Configure the Scar Map data model that tracks every significant event during an expedition. Each event is stored as a `ScarMapMarker` on the expedition singleton entity. This guide covers creating the component files, authoring the expedition entity, wiring event listeners, configuring the marker render blob, and validating the data pipeline.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| Expedition Manager prefab | `ScarMapAuthoring` | Baker creates expedition entity with ScarMapState + ScarMapMarker buffer + ScarMapDistrictSummary buffer |
| District definitions | `DistrictDefinitionSO` (EPIC 13.1) | Provides DistrictId range for marker validation |
| Framework systems | Death, Echo, Front, Rival, Quest event buses | Source events that create markers |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| ScarMapComponents.cs | `Assets/Scripts/ScarMap/Components/` | C# (ECS components) |
| ScarMapBlobs.cs | `Assets/Scripts/ScarMap/Blobs/` | C# (BlobAsset) |
| ScarMapMarkerSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (ISystem) |
| ScarMapAggregatorSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (ISystem) |
| ScarMapTransitionSystem.cs | `Assets/Scripts/ScarMap/Systems/` | C# (ISystem) |
| Hollowcore.ScarMap.asmdef | `Assets/Scripts/ScarMap/` | Assembly Definition |
| ScarMapRenderConfigSO | `Assets/Data/ScarMap/` | ScriptableObject |
| ScarMapDataValidator.cs | `Assets/Editor/ScarMap/` | Editor validator |

---

## 1. Create Assembly and Folder Structure
**Create:** manually in Project window
**Recommended location:** `Assets/Scripts/ScarMap/`

### 1.1 Folder Layout
```
Assets/Scripts/ScarMap/
  Components/
    ScarMapComponents.cs          (MarkerType, ScarMapMarker, ScarMapState, ScarMapDistrictSummary)
    ScarMapViewComponents.cs      (render-side, see EPIC 12.2)
  Systems/
    ScarMapMarkerSystem.cs
    ScarMapAggregatorSystem.cs
    ScarMapTransitionSystem.cs
  Blobs/
    ScarMapBlobs.cs               (ScarMapMarkerRenderBlob, MarkerRenderEntry)
  Bridges/
  Debug/
    ScarMapDebugOverlay.cs
  Authoring/
    ScarMapAuthoring.cs
    ScarMapRenderConfigAuthoring.cs
```

### 1.2 Assembly Definition
**Create:** `Assets > Create > Assembly Definition`
**Name:** `Hollowcore.ScarMap`

| Reference | Why |
|-----------|-----|
| `DIG.Shared` | Shared types |
| `Unity.Entities` | ECS core |
| `Unity.Collections` | NativeArray, NativeList |
| `Unity.Mathematics` | math, float4 |
| `Unity.NetCode` | GhostField (if replicated) |
| `Unity.Burst` | Burst compilation |

---

## 2. Configure the MarkerType Enum

**File:** `Assets/Scripts/ScarMap/Components/ScarMapComponents.cs`

| Value | Name | Metadata Interpretation | Layer (debug viz) |
|-------|------|------------------------|-------------------|
| 0 | Skull | Gear inventory hash (for loot preview tooltip) | F5 |
| 1 | EchoSpiral | `(rewardType << 8) \| difficulty` | F7 |
| 2 | FrontGradient | Phase value 0-4 | F8 |
| 3 | BleedTendril | Target district ID | F9 |
| 4 | Star | Event type ID: 0=merchant, 1=vault, 2=legendary, 3=quest | F5 |
| 5 | RevivalNode | Quality tier 1-5 | F5 |
| 6 | RivalMarker | Rival definition ID | F10 |
| 7 | Completed | Objective type ID | F5 |
| 8 | Death | Death cause enum value | F5 |

**Tuning tip:** The `Metadata` field is a single `int`. Pack multiple values using bit-shifting for EchoSpiral; all other types use the full int for a single value.

---

## 3. Configure the ScarMapMarker Buffer

### 3.1 InternalBufferCapacity
| Field | Default | Range | Notes |
|-------|---------|-------|-------|
| InternalBufferCapacity | 32 | 16-64 | 32 covers most runs without dynamic allocation. Buffer grows automatically beyond this. |

### 3.2 ScarMapMarker Fields
| Field | Type | Description | Default |
|-------|------|-------------|---------|
| DistrictId | int | District where event occurred | (from event) |
| ZoneId | int | Zone within district (-1 = district-level) | -1 |
| Type | MarkerType | Category of event | (from event) |
| Metadata | int | Type-specific data (see table above) | 0 |
| Timestamp | int | Gate transition count when created | (auto) |
| MarkerHash | int | Hash(DistrictId, ZoneId, Type, Metadata) for dedup | (auto-computed) |
| IsActive | bool | False when resolved (echo completed, body consumed) | true |

**Tuning tip:** Inactive markers remain in the buffer for timeline display at end-of-run. They render differently (dimmed) but are never removed.

---

## 4. Author the ScarMapAuthoring Component

**Create:** `Assets > Create > C# Script` named `ScarMapAuthoring.cs`
**Attach to:** Expedition Manager prefab (the singleton entity that persists across districts)

### 4.1 Baker Output
The Baker should create an entity with:
- `ScarMapState` (IComponentData)
- `ScarMapMarker` (DynamicBuffer)
- `ScarMapDistrictSummary` (DynamicBuffer)

### 4.2 ScarMapState Inspector Fields
| Field | Type | Description | Default |
|-------|------|-------------|---------|
| ExpeditionSeed | uint | Set from expedition seed at run start | 0 |

All other ScarMapState fields (CurrentTransition, TotalMarkerCount, counters) initialize to 0 and are managed by systems at runtime.

---

## 5. Configure the Marker Render Blob

**Create:** `ScarMapRenderConfigAuthoring` MonoBehaviour on a subscene singleton.

### 5.1 MarkerRenderEntry Fields (per MarkerType, 9 entries)
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| IconSpriteIndex | Index into shared marker icon atlas | (per type) | 0-8 |
| TintColor | RGBA tint | (per type) | float4 |
| PulseSpeed | Animation pulse rate (0 = static) | 0.0 | 0.0-6.0 |
| BaseScale | Icon scale multiplier | 1.0 | 0.1-3.0 |
| RenderLayer | Sorting layer for overlap | (per type) | 0-10 |

### 5.2 Recommended Defaults per MarkerType

| MarkerType | TintColor (hex) | PulseSpeed | BaseScale | RenderLayer |
|------------|-----------------|------------|-----------|-------------|
| Skull | #E74C3C (red) | 0.0 | 1.0 | 5 |
| EchoSpiral | #9B59B6 (purple) | 2.0 | 1.1 | 6 |
| FrontGradient | (varies by phase) | 0.0 | 1.0 | 1 |
| BleedTendril | #E67E22 (orange) | 1.5 | 0.8 | 2 |
| Star | #F1C40F (gold) | 3.0 | 1.2 | 7 |
| RevivalNode | #2ECC71 (green) | 1.0 | 1.0 | 5 |
| RivalMarker | #E67E22 (orange) | 0.0 | 1.0 | 8 |
| Completed | #F1C40F (gold) | 0.0 | 0.9 | 4 |
| Death | #C0392B (dark red) | 0.0 | 1.3 | 9 |

**Tuning tip:** Set PulseSpeed to 0 for markers that should not animate. EchoSpiral and Star markers benefit most from animation to draw the player's eye.

---

## 6. Wire Event Listeners in ScarMapMarkerSystem

The `ScarMapMarkerSystem` is the central intake. It must listen for gameplay events from other EPICs:

| Event Source | EPIC | Creates MarkerType |
|--------------|------|-------------------|
| Player death | EPIC 2 (Death) | Skull + Death |
| Echo spawned | EPIC 5 (Echoes) | EchoSpiral |
| Echo completed | EPIC 5 | Sets EchoSpiral.IsActive=false, adds Completed |
| Front phase change | EPIC 3 (Front) | FrontGradient (update/add) |
| District bleed | EPIC 3 | BleedTendril |
| Seeded event discovered | EPIC 4 | Star |
| Body available for revival | EPIC 2 | RevivalNode |
| Rival activity | EPIC 11.2 | RivalMarker |
| Objective completed | Quest systems | Completed |

### 6.1 Deduplication
Before adding a marker, compute `MarkerHash = Hash(DistrictId, ZoneId, Type, Metadata)` and scan the existing buffer. Skip if hash already exists.

---

## Scene & Subscene Checklist

- [ ] Expedition Manager prefab has `ScarMapAuthoring` component
- [ ] Subscene contains singleton with `ScarMapRenderConfigAuthoring` (blob bake target)
- [ ] `Hollowcore.ScarMap.asmdef` references all required assemblies
- [ ] All 9 MarkerRenderEntry entries populated in the render config SO

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| Missing ScarMapAuthoring on expedition prefab | Null reference when ScarMapMarkerSystem queries for singleton | Add ScarMapAuthoring to Expedition Manager prefab |
| MarkerHash not computed before append | Duplicate markers on district re-entry (same skull at same location twice) | Always compute and check MarkerHash before buffer.Add |
| FrontGradient metadata out of range | Render colors incorrect, invalid phase display | Validate phase in [0..4] before creating marker |
| InternalBufferCapacity too small | Frequent dynamic buffer allocation (GC pressure) | Increase to 32-64 if markers exceed 16 per expedition |
| Timestamp not incremented | End-of-run timeline shows all events at t=0 | Ensure ScarMapTransitionSystem increments CurrentTransition on gate events |
| ScarMapState.IsDirty not set after marker add | Aggregator never recomputes district summaries | Set `IsDirty = true` after every marker add/modify |

---

## Verification

- [ ] `ScarMapState` singleton created on expedition start with correct seed
- [ ] Manually fire 3-4 marker events (death, echo, Front change) and verify buffer population in Entity Inspector
- [ ] MarkerHash deduplication prevents duplicate markers on district re-entry
- [ ] Each MarkerType stores correct metadata format
- [ ] Timestamps increment with gate transitions
- [ ] IsActive toggled correctly (EchoSpiral -> false on completion)
- [ ] ScarMapAggregatorSystem produces accurate district summaries
- [ ] IsDirty flag prevents unnecessary aggregator recalculation
- [ ] 100-marker rendering performance test completes under 2ms
