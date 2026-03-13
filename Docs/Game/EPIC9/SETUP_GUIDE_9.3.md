# EPIC 9.3 Setup Guide: Compendium UI

**Status:** Planned
**Requires:** EPIC 9.1 (Compendium Pages), EPIC 9.2 (Compendium Entries), Framework UI toolkit

---

## Overview

The Compendium UI is the player's interface into both run-consumable pages and permanent meta-progression entries. It has three modes: the encyclopedia browser (category tabs, discovered/locked entries, completion percentage), the page management panel (slot, view, activate pages), and the extraction summary overlay (new entries highlighted at end-of-district). All data flows through a CompendiumUIBridgeSystem following the CombatUIBridgeSystem pattern.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| EPIC 9.1 | CompendiumPageState buffer, CompendiumLink | Page inventory data |
| EPIC 9.2 | CompendiumEntryState buffer, CompendiumEntryNewFlag | Entry unlock data |
| Framework UI | CombatUIBridgeSystem pattern | ECS-to-MonoBehaviour bridge architecture |

### New Setup Required
1. Create `Assets/Scripts/Compendium/UI/` and `Assets/Scripts/Compendium/Bridges/` folders
2. Create `CompendiumUIRegistry` static managed class
3. Create `CompendiumUIBridgeSystem` in Bridges/
4. Build 3 UI prefabs: CompendiumScreen, PageManagementPanel, ExtractionSummaryOverlay
5. Wire prefabs to register with CompendiumUIRegistry on Enable
6. Hook ExtractionSummaryOverlay to extraction flow

---

## 1. CompendiumUIRegistry

**File:** `Assets/Scripts/Compendium/Bridges/CompendiumUIRegistry.cs`

Static managed singleton (follows CombatUIRegistry pattern). NOT a MonoBehaviour.

### 1.1 Public API
| Member | Description |
|--------|-------------|
| `static bool IsActive` | True when any compendium UI panel is open |
| `static CompendiumUISnapshot CurrentSnapshot` | Latest data from bridge system |
| `static event Action OnSnapshotUpdated` | Fired each frame when snapshot refreshes |
| `RegisterPanel(ICompendiumPanel)` | Called by UI panels on enable |
| `UnregisterPanel(ICompendiumPanel)` | Called by UI panels on disable |
| `RequestPageActivation(int pageDefinitionId, sbyte slotIndex)` | Sends activation request to ECS |
| `RequestPageSlotChange(int pageDefinitionId, sbyte newSlotIndex)` | Reorders page slots |

---

## 2. CompendiumUIBridgeSystem

**File:** `Assets/Scripts/Compendium/Bridges/CompendiumUIBridgeSystem.cs`
**Filter:** ClientSimulation | LocalSimulation
**Group:** PresentationSystemGroup

Managed SystemBase that only runs when `CompendiumUIRegistry.IsActive == true`.

### 2.1 Per-Frame Flow
1. Resolve local player's CompendiumLink to child entity
2. Read `CompendiumEntryState` buffer -- build entry list
3. Read `CompendiumEntryNewFlag` buffer -- mark entries as new
4. Cross-reference against `CompendiumEntryDatabase` (all definitions) to include locked entries
5. Read `CompendiumPageState` buffer -- build page list
6. Read `CompendiumPageConfig` -- slot limits
7. Compute completion percentages (total and per-category)
8. Push `CompendiumUISnapshot` to `CompendiumUIRegistry`

---

## 3. CompendiumScreen Prefab (Encyclopedia Browser)

**Create:** `Assets/Prefabs/UI/Compendium/CompendiumScreen.prefab`

### 3.1 Prefab Structure
```
CompendiumScreen (Canvas, full-screen panel)
  +-- LeftSidebar
  |     +-- CategoryTab_Mission ("Missions (3/8)")
  |     +-- CategoryTab_Vendor ("Vendors (1/4)")
  |     +-- CategoryTab_Traversal
  |     +-- CategoryTab_Lore
  |     +-- CategoryTab_Enemy
  |     +-- CategoryTab_District
  |     +-- CategoryTab_Boss
  +-- CenterGrid (ScrollRect)
  |     +-- EntryCard_Template (prefab, pooled)
  |           +-- EntryIcon (Image)
  |           +-- EntryName (TextMeshProUGUI)
  |           +-- NewBadge (Image, pulsing, hidden by default)
  +-- RightPanel (Detail view)
  |     +-- DetailIcon (Image)
  |     +-- DetailName (TextMeshProUGUI)
  |     +-- DetailDescription (TextMeshProUGUI)
  |     +-- LoreText (TextMeshProUGUI, scrollable)
  |     +-- MechanicalHint (TextMeshProUGUI, styled differently)
  |     +-- RewardDescription (TextMeshProUGUI)
  +-- BottomBar
        +-- TotalCompletionBar (Slider + percentage text)
        +-- PerCategoryMiniBars (7 small bars)
```

### 3.2 Entry Card States
| State | Icon | Name | Border |
|-------|------|------|--------|
| Unlocked | Full icon | Full name | Normal |
| Locked | Silhouette (LockedIcon) | "???" | Dimmed |
| New this expedition | Full icon | Full name + "NEW" badge | Pulsing gold |

### 3.3 Category Tab Display
Format: `"{CategoryName} ({unlocked}/{total})"`

Each tab filters the center grid to show only entries of that category.

**Tuning tip:** Category tabs should show unlock progress to motivate completionists. The bottom bar shows overall completion. These create the "play more, discover more" retention loop.

---

## 4. PageManagementPanel Prefab

**Create:** `Assets/Prefabs/UI/Compendium/PageManagementPanel.prefab`

### 4.1 Prefab Structure
```
PageManagementPanel (RectTransform, overlay)
  +-- ActiveSlotRow
  |     +-- PageSlot_0 (drag-drop target)
  |     |     +-- PageIcon (Image)
  |     |     +-- PageName (TextMeshProUGUI)
  |     |     +-- TypeBadge (Image, colored by type)
  |     +-- PageSlot_1
  |     +-- PageSlot_2
  |     +-- PageSlot_3
  |     +-- PageSlot_4
  |     +-- PageSlot_5
  +-- OverflowGrid (ScrollRect)
  |     +-- OverflowPageCard_Template (prefab)
  +-- PageDetailTooltip (hidden, shown on hover)
        +-- TooltipName
        +-- TooltipDescription
        +-- TooltipEffectPreview
```

### 4.2 Slot Type Colors
| Type | Color |
|------|-------|
| Scout | Blue (#3B82F6) |
| Suppression | Red (#EF4444) |
| Insight | Gold (#F59E0B) |
| Empty | Grey outline, dashed border |

### 4.3 Interactions
| Action | Result |
|--------|--------|
| Tap slotted page | Confirmation prompt to use (consume) |
| Drag overflow to active slot | Equip page in that slot |
| Drag active to overflow | Unslot page to overflow inventory |
| Hover any page | Show detail tooltip with effect preview |

**Tuning tip:** The confirmation prompt prevents accidental activation. Show the effect preview in the confirmation dialog so players make informed decisions.

---

## 5. ExtractionSummaryOverlay Prefab

**Create:** `Assets/Prefabs/UI/Compendium/ExtractionSummaryOverlay.prefab`

### 5.1 Prefab Structure
```
ExtractionSummaryOverlay (Canvas, overlay)
  +-- Header ("New Compendium Entries")
  +-- EntryRevealContainer
  |     +-- EntryReveal_Template (animated)
  |           +-- EntryIcon (Image, slides in from left)
  |           +-- EntryName (TextMeshProUGUI, fades in)
  |           +-- EntryCategory (TextMeshProUGUI)
  |           +-- RewardText (TextMeshProUGUI, "New vendor unlocked in Chrome Cathedral")
  +-- NoDiscoveriesText ("No new discoveries", hidden when entries found)
  +-- ViewInCompendiumButton
  +-- ContinueButton
```

### 5.2 Display Rules
| Condition | Behavior |
|-----------|----------|
| NewThisExpeditionCount > 0 | Show header + animated entry reveals + ViewInCompendium button |
| NewThisExpeditionCount == 0 | Show "No new discoveries" with subtle encouragement text |

### 5.3 Animation Sequence
Each new entry reveals sequentially with a 0.5s stagger:
1. Icon slides in from left (0.3s)
2. Name + category fade in (0.2s)
3. Reward text appears below (0.2s)

### 5.4 Trigger
Shown by the extraction flow (EPIC 6.5) after ExtractionEvent is processed by CompendiumEntrySystem.

**Tuning tip:** The extraction summary should feel rewarding even for a single new entry. The "View in Compendium" button lets curious players dig deeper immediately.

---

## 6. Compendium Workstation (Editor Tool)

**Open:** `DIG > Compendium Workstation`
**File:** `Assets/Editor/CompendiumWorkstation/CompendiumWorkstationWindow.cs`

| Tab | Module | Purpose |
|-----|--------|---------|
| Entry Browser | CompendiumEntryBrowserModule | Tree view, inline editing, bulk operations |
| Page Editor | CompendiumPageEditorModule | Grid view, visual card preview, effect display |
| Book Layout | CompendiumBookLayoutModule | Visual page layout preview, drag-drop reorder |
| Completion Tracker | CompendiumCompletionModule | Per-category bars, missing content warnings |
| Validation | CompendiumValidationModule | Error/warning table, cross-reference report |
| Simulation | CompendiumSimulationModule | Meta-progression simulation (50 expeditions) |

---

## Scene & Subscene Checklist
| Scene/Subscene | What to Add | Notes |
|----------------|-------------|-------|
| `Assets/Prefabs/UI/Compendium/` | CompendiumScreen.prefab | Full-screen encyclopedia browser |
| `Assets/Prefabs/UI/Compendium/` | PageManagementPanel.prefab | Overlay for page slot management |
| `Assets/Prefabs/UI/Compendium/` | ExtractionSummaryOverlay.prefab | Post-district new entry reveal |
| `Assets/Scripts/Compendium/Bridges/` | CompendiumUIRegistry.cs | Static managed singleton |
| `Assets/Scripts/Compendium/Bridges/` | CompendiumUIBridgeSystem.cs | ECS-to-UI bridge (PresentationSystemGroup) |
| `Assets/Scripts/Compendium/UI/` | CompendiumScreenPanel.cs | MonoBehaviour on screen prefab |
| `Assets/Scripts/Compendium/UI/` | PageManagementPanel.cs | MonoBehaviour on page panel prefab |
| `Assets/Scripts/Compendium/UI/` | ExtractionSummaryOverlay.cs | MonoBehaviour on summary prefab |
| Extraction flow | Hook ExtractionSummaryOverlay to show after ExtractionEvent | EPIC 6.5 integration |

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| CompendiumUIBridgeSystem runs every frame | Performance waste when compendium is closed | Gate on `CompendiumUIRegistry.IsActive` check |
| UI directly queries ECS | Race conditions, managed/unmanaged boundary issues | ALL data flows through CompendiumUIBridgeSystem -- never query ECS from MonoBehaviour |
| Locked entries show full name | Spoiler leakage (players see content names before unlocking) | Show "???" for locked entry names, use LockedIcon silhouette |
| ExtractionSummaryOverlay shown before EntrySystem runs | No new entries detected, always shows "No new discoveries" | Ensure CompendiumEntrySystem processes BEFORE summary overlay triggers |
| Page activation from UI sends multiple requests | Page consumed multiple times, inventory desyncs | Disable activation button immediately on click, re-enable on response |
| Category completion counts wrong | "Vendors (5/3)" or negative counts | Verify TotalEntryCount per category matches authored definition count |
| Drag-drop between slots fires no event | Pages visually move but ECS state unchanged | Wire drag-drop completion to CompendiumUIRegistry.RequestPageSlotChange() |
| CompendiumUIRegistry not reset between sessions | Stale IsActive flag, phantom panels | Clear state on session start |

---

## Verification

- [ ] CompendiumUIBridgeSystem only runs when CompendiumUIRegistry.IsActive is true
- [ ] Category tabs filter entries correctly; counts show "unlocked/total" format
- [ ] Unlocked entries display icon, name, full detail on selection
- [ ] Locked entries display silhouette icon and "???" -- no spoiler leakage
- [ ] New entries from current expedition have visible "NEW" pulsing badge
- [ ] Completion percentage accurate: per-category and total
- [ ] Page management shows correct slot count (6 default)
- [ ] Drag-drop between active slots and overflow works correctly
- [ ] Page activation from slot triggers PageActivationRequest in ECS
- [ ] Extraction summary overlay appears when new entries unlocked at extraction
- [ ] Extraction summary hidden when no new entries discovered
- [ ] "View in Compendium" button opens screen filtered to new entries
- [ ] UI performs NO direct ECS queries -- all data via bridge system
- [ ] Compendium Workstation Entry Browser shows all authored entries with correct categories
