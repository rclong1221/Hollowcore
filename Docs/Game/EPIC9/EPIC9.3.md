# EPIC 9.3: Compendium UI

**Status**: Planning
**Epic**: EPIC 9 — Compendium & Meta Progression
**Priority**: Medium — Player-facing surface for 9.1 and 9.2
**Dependencies**: EPIC 9.1 (Pages), EPIC 9.2 (Entries); Framework UI toolkit

---

## Overview

The Compendium UI is the player's interface into both run-consumable pages and permanent meta-progression entries. It has three modes: the encyclopedia browser (category tabs, discovered/locked entries, completion percentage), the page management panel (slot, view, activate pages), and the extraction summary overlay (new entries highlighted at end-of-district). The UI is entirely managed-side MonoBehaviour code reading ECS state through a bridge system, following the CombatUIBridgeSystem pattern from the framework.

---

## Component Definitions

### CompendiumUIState (Managed — MonoBehaviour)

```csharp
// File: Assets/Scripts/Compendium/UI/CompendiumUIState.cs
using UnityEngine;

namespace Hollowcore.Compendium.UI
{
    /// <summary>
    /// Tracks which tab/page the compendium screen is currently showing.
    /// Pure UI state — no ECS component needed.
    /// </summary>
    public class CompendiumUIState : MonoBehaviour
    {
        public CompendiumEntryCategory ActiveCategory;
        public int SelectedEntryIndex = -1;
        public bool IsPageManagementOpen;
        public bool IsExtractionSummaryShowing;
        public float CompletionPercentTotal;
        public float[] CompletionPercentByCategory; // Indexed by (int)CompendiumEntryCategory
    }
}
```

### CompendiumUIData (Bridge Payload)

```csharp
// File: Assets/Scripts/Compendium/UI/CompendiumUIData.cs
using System.Collections.Generic;

namespace Hollowcore.Compendium.UI
{
    /// <summary>
    /// Snapshot of compendium state pushed from ECS bridge to UI each frame the compendium is open.
    /// Avoids per-frame ECS queries from MonoBehaviour code.
    /// </summary>
    public struct CompendiumEntryUIData
    {
        public int EntryDefinitionId;
        public CompendiumEntryCategory Category;
        public string DisplayName;
        public string Description;
        public string LoreText;
        public string MechanicalHint;
        public string RewardDescription;
        public bool IsNewThisExpedition;
        public bool IsUnlocked;
    }

    public struct CompendiumPageUIData
    {
        public int PageDefinitionId;
        public CompendiumPageType PageType;
        public string DisplayName;
        public string Description;
        public sbyte SlotIndex;
        public bool IsSlotted;
    }

    public class CompendiumUISnapshot
    {
        public List<CompendiumEntryUIData> Entries = new();
        public List<CompendiumPageUIData> Pages = new();
        public int TotalEntryCount;          // All definitions in database
        public int UnlockedEntryCount;       // Currently unlocked
        public int NewThisExpeditionCount;   // Unlocked during current expedition
        public int ActivePageSlotCount;      // Slotted pages
        public int TotalPageCount;           // All carried pages
        public byte MaxActiveSlots;
    }
}
```

---

## Systems

### CompendiumUIBridgeSystem

```csharp
// File: Assets/Scripts/Compendium/Bridges/CompendiumUIBridgeSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Managed SystemBase — reads ECS, pushes to CompendiumUIRegistry (static managed singleton).
// Only runs when compendium UI is open (checked via CompendiumUIRegistry.IsActive).
//
// Flow:
//   1. Resolve local player's CompendiumLink → compendium child entity
//   2. Read CompendiumEntryState buffer → build entry list
//   3. Read CompendiumEntryNewFlag buffer → mark entries as new
//   4. Cross-reference against CompendiumEntryDatabase (all definitions) to build locked entries
//   5. Read CompendiumPageState buffer → build page list
//   6. Read CompendiumPageConfig → slot limits
//   7. Compute completion percentages (total and per-category)
//   8. Push CompendiumUISnapshot to CompendiumUIRegistry
//   9. CompendiumUIRegistry notifies active UI panels via OnSnapshotUpdated event
```

### CompendiumUIRegistry

```csharp
// File: Assets/Scripts/Compendium/Bridges/CompendiumUIRegistry.cs
// Static managed singleton (follows CombatUIRegistry pattern).
//
// - IsActive: true when compendium screen or page panel is open
// - CurrentSnapshot: latest CompendiumUISnapshot from bridge system
// - OnSnapshotUpdated: event fired each frame when snapshot is refreshed
// - RegisterPanel / UnregisterPanel: called by UI panels on enable/disable
// - RequestPageActivation(int pageDefinitionId, sbyte slotIndex): sends activation request to ECS
// - RequestPageSlotChange(int pageDefinitionId, sbyte newSlotIndex): reorder page slots
```

---

## UI Panels

### CompendiumScreenPanel

```csharp
// File: Assets/Scripts/Compendium/UI/CompendiumScreenPanel.cs
// MonoBehaviour on the compendium screen prefab.
//
// Layout:
//   - Left sidebar: category tabs (Mission, Vendor, Traversal, Lore, Enemy, District, Boss)
//     - Each tab shows count: "Vendors (3/8)" format
//     - Active tab highlighted
//   - Center: scrollable grid of entry cards
//     - Unlocked entries: icon + name, clickable for detail view
//     - Locked entries: silhouette icon + "???" name, shows unlock hint on hover
//     - New entries: pulsing border / "NEW" badge (from CompendiumEntryNewFlag)
//   - Right panel: selected entry detail
//     - Full description, lore text, mechanical hint (if any), reward description
//   - Bottom bar: total completion percentage + per-category mini-bars
//
// Data source: CompendiumUIRegistry.CurrentSnapshot.Entries
// Filtered by: CompendiumUIState.ActiveCategory
```

### PageManagementPanel

```csharp
// File: Assets/Scripts/Compendium/UI/PageManagementPanel.cs
// MonoBehaviour on the page management overlay.
//
// Layout:
//   - Top row: active page slots (MaxActiveSlots boxes, drag-drop targets)
//     - Filled slots: page icon + name + type color (Scout=blue, Suppression=red, Insight=gold)
//     - Empty slots: dashed outline + "Empty Slot"
//     - Tap slotted page: confirmation prompt to use (calls CompendiumUIRegistry.RequestPageActivation)
//   - Below: overflow inventory (unslotted pages)
//     - Drag from overflow → active slot to equip
//     - Drag from active → overflow to unslot
//   - Page detail tooltip on hover: full description, effect preview, type-specific info
//
// Data source: CompendiumUIRegistry.CurrentSnapshot.Pages
// Slot operations: CompendiumUIRegistry.RequestPageSlotChange
```

### ExtractionSummaryOverlay

```csharp
// File: Assets/Scripts/Compendium/UI/ExtractionSummaryOverlay.cs
// MonoBehaviour shown at extraction (end-of-district).
//
// Layout:
//   - "New Compendium Entries" header (only shown if NewThisExpeditionCount > 0)
//   - Animated reveal of each new entry: icon slides in, name + category fades in
//   - Unlock reward text shown below each entry ("New vendor unlocked in Chrome Cathedral")
//   - "View in Compendium" button → opens CompendiumScreenPanel filtered to new entries
//   - If no new entries: "No new discoveries" with subtle encouragement text
//
// Data source: CompendiumUIRegistry.CurrentSnapshot (filtered to IsNewThisExpedition)
// Triggered by: ExtractionEvent → CompendiumUIRegistry.ShowExtractionSummary()
```

---

## Setup Guide

1. **Create `Assets/Scripts/Compendium/UI/`** and `Assets/Scripts/Compendium/Bridges/` folders
2. Create `CompendiumUIRegistry` as a static managed class (no MonoBehaviour — follows CombatUIRegistry)
3. Create `CompendiumUIBridgeSystem` in the Bridges/ folder
4. Build UI prefabs under `Assets/Prefabs/UI/Compendium/`:
   - `CompendiumScreen.prefab` — full-screen panel with category tabs and entry grid
   - `PageManagementPanel.prefab` — overlay for page slot management
   - `ExtractionSummaryOverlay.prefab` — post-district new entry reveal
5. Wire `CompendiumScreenPanel` and `PageManagementPanel` to register with `CompendiumUIRegistry` on Enable
6. Hook `ExtractionSummaryOverlay` to the district extraction flow (after ExtractionEvent processed)
7. Create a `CompendiumEntryDatabase` ScriptableObject that indexes all CompendiumEntryDefinitionSO assets for the bridge system's locked-entry display
8. Test: open compendium screen → verify category tabs filter correctly, locked entries show silhouettes, completion % updates

---

## Verification

- [ ] CompendiumUIBridgeSystem only runs when CompendiumUIRegistry.IsActive is true
- [ ] Category tabs filter entries correctly; counts show "unlocked/total" format
- [ ] Unlocked entries display icon, name, and full detail on selection
- [ ] Locked entries display silhouette and "???" — no spoiler leakage
- [ ] New entries from current expedition have visible "NEW" badge
- [ ] Completion percentage accurate: per-category and total
- [ ] Page management shows correct slot count from CompendiumPageConfig
- [ ] Drag-drop between active slots and overflow works
- [ ] Page activation from slot triggers PageActivationRequest in ECS
- [ ] Extraction summary overlay appears when new entries unlocked at extraction
- [ ] Extraction summary hidden when no new entries discovered
- [ ] "View in Compendium" button from extraction summary opens screen filtered to new entries
- [ ] UI performs no direct ECS queries — all data flows through CompendiumUIBridgeSystem → Registry

---

## Editor Tooling — Compendium Authoring Workstation

```csharp
// File: Assets/Editor/CompendiumWorkstation/CompendiumWorkstationWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Hollowcore.Compendium.Editor
{
    /// <summary>
    /// Central editor window for Compendium content authoring.
    /// Follows DIG workstation pattern (sidebar tabs, module dispatch).
    /// Menu: DIG > Compendium Workstation
    /// </summary>
    // Modules (sidebar tabs):
    //
    // 1. "Entry Browser" — CompendiumEntryBrowserModule
    //    - Tree view: category → entries, filterable by category/unlock condition
    //    - Inline editing: select entry → inspector-like panel for all SO fields
    //    - Bulk operations: "Create Entry" button, "Duplicate", "Delete with confirmation"
    //    - Completion tracker: per-category bar showing authored/planned count
    //
    // 2. "Page Editor" — CompendiumPageEditorModule
    //    - Grid of all page definitions, filterable by PageType and Rarity
    //    - Visual card preview: icon + name + type color badge + effect summary
    //    - Rich text preview: renders Description with markup formatting
    //    - Effect preview: for Scout pages shows which flags are set, for Suppression shows
    //      multiplier values, for Insight shows reveal toggles
    //
    // 3. "Book Layout" — CompendiumBookLayoutModule
    //    - Visual book/page layout preview
    //    - Categories as "chapters", entries as "pages" within chapters
    //    - Drag-drop reordering within categories
    //    - Shows locked silhouette vs unlocked state side by side
    //    - Preview of the in-game CompendiumScreenPanel layout
    //
    // 4. "Completion Tracker" — CompendiumCompletionModule
    //    - Per-category completion bars: "Mission: 12/20 entries authored (60%)"
    //    - Per-category planned entries (from a CompendiumPlanSO or inline count)
    //    - Total completion: "Total: 45/100 entries (45%)"
    //    - Missing content warnings: categories with < 3 entries flagged
    //    - Orphan detection: entries referencing nonexistent districts/bosses/quests
    //
    // 5. "Validation" — CompendiumValidationModule
    //    - Runs CompendiumEntryValidation + CompendiumPageValidation
    //    - Results table: severity (Error/Warning), rule name, asset name, fix suggestion
    //    - "Fix All" button for auto-fixable issues (e.g., duplicate IDs → reassign)
    //    - Cross-reference report: all external IDs (district, boss, quest) and their status
    //
    // 6. "Simulation" — CompendiumSimulationModule (see EPIC 9.2 Simulation & Testing)
    //
    // Window setup:
    //   [MenuItem("DIG/Compendium Workstation")]
    //   public static void ShowWindow()
    //   {
    //       var window = GetWindow<CompendiumWorkstationWindow>("Compendium Workstation");
    //       window.minSize = new Vector2(800, 600);
    //   }
    //
    // Shared context: CompendiumDataContext loads all entry/page SOs on enable,
    // refreshes on AssetPostprocessor callback (follows RogueliteDataContext pattern).
}
#endif
```

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Compendium/Debug/CompendiumDebugOverlay.cs
namespace Hollowcore.Compendium.Debug
{
    /// <summary>
    /// In-game debug overlay for compendium state.
    /// </summary>
    // Overlay elements:
    //   1. Completion summary: total % unlocked, per-category mini-bars
    //   2. Recently unlocked entries: last 5 entries unlocked this expedition,
    //      highlighted with pulsing border for 30 seconds after unlock
    //   3. Page inventory: current page slots with type/name, overflow count
    //   4. Active page effects: list of currently active RunModifierStack entries from pages,
    //      with remaining duration countdown
    //   5. Unlock condition preview: for each locked entry in current district,
    //      show condition type and progress (e.g., "Kill 3/5 Stalkers")
    //   6. New flag indicator: badge count of CompendiumEntryNewFlag entries
    //
    // Toggle: debug console "compendium_debug" or CompendiumWorkstation debug tab
    // Implementation: MonoBehaviour on debug canvas, reads via CompendiumUIBridgeSystem
    // (reuses existing bridge, no additional ECS system needed)
}
```
