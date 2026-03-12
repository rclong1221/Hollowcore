# EPIC 18.1: UI Framework — Screen Lifecycle & Service Layer

**Status:** IMPLEMENTED
**Priority:** Critical (Foundation for all UI-dependent systems)
**Dependencies:**
- `UIView<TViewModel>` base class (existing — `DIG.UI.Core.MVVM`, EPIC 15.8, `Assets/Scripts/UI/Core/MVVM/UIView.cs`)
- `ViewModelBase` / `ECSViewModelBase` (existing — `DIG.UI.Core.MVVM`, EPIC 15.8, `Assets/Scripts/UI/Core/MVVM/ViewModelBase.cs`)
- `BindableProperty<T>` reactive property (existing — `DIG.UI.Core.MVVM`, EPIC 15.8, `Assets/Scripts/UI/Core/MVVM/BindableProperty.cs`)
- `NavigationManager` screen/modal stack (existing — `DIG.UI.Core.Navigation`, EPIC 15.8, `Assets/Scripts/UI/Core/Navigation/NavigationManager.cs`)
- Unity UI Toolkit (`UnityEngine.UIElements`)

**Constraints:**
- **No UniTask** — not in project. Uses `Action` callbacks for async completion.
- **No Addressables** — not in project. Uses `Resources.Load<VisualTreeAsset>()` matching existing patterns.
- **No DOTween** — uses USS transitions + `VisualElement.schedule` for timing.
- **Unity 2022.3** — no `SetCustomProperty` API. UIThemeSO applies inline styles on layer roots.

**Feature:** A production-grade UI service layer that manages screen lifecycles (creation, pooling, transitions, disposal), provides a type-safe screen registry with Resources-backed loading, standardizes transition animations (fade, slide, scale), implements focus management for gamepad/keyboard navigation, and exposes an editor workstation for live screen hierarchy inspection, asset creation, theme editing, and layout debugging. Built entirely on UI Toolkit with MVVM. Replaces ad-hoc screen show/hide scattered across subsystems with a single, swappable `IUIService` interface.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  UIThemeSO                  ScreenManifestSO           TransitionProfileSO
  (inline style colors,      (all registered screens,   (duration, delay,
   font overrides)            Resources paths,           type: fade/slide/scale,
                              layer assignments)         USS class names)
        |                         |                          |
        └──── UIServiceBootstrap (MonoBehaviour, DontDestroyOnLoad) ────┘
              (loads manifest from Resources, creates UIToolkitService,
               applies default theme, sets UIServices.Screen)
                         |
                 IUIService (swappable interface)
                         |
              UIToolkitService : IUIService
              (manages layered VisualElement hierarchy,
               screen pooling, transition player,
               focus manager, topmost tracking)
                         |
        ┌────────────────┼────────────────┐
        |                |                |
  Resources.Load   TransitionPlayer   FocusManager
  + UXML/USS cache  (USS class toggle,   (focus stack,
  + pool per screen  schedule timing)    auto-focus on
                                         push/pop)
        |                |                |
        └────────────────┼────────────────┘
                         |
              NavigationManager (existing, enhanced)
              (Push/Pop with ScreenHandle field on
               NavigationEntry for service integration)
                         |
                 EDITOR TOOLING
                         |
  UIWorkstationWindow ── 5 tabs
  (Setup wizard, catalog editor, theme editor,
   live screen stack, focus debug)
```

### Screen Lifecycle

```
  Request OpenScreen("Inventory")
    |
    ├─ ScreenManifestSO.TryGetScreen("Inventory")
    |    ├─ If pooled: reuse existing VisualElement tree (O(1) dequeue)
    |    ├─ If cached UXML: Instantiate from _uxmlCache
    |    └─ Else: Resources.Load<VisualTreeAsset> → cache → Instantiate
    |
    ├─ TransitionPlayer.Prepare(screenRoot, profile)
    |    └─ Adds USS classes: screen-transition-base + start position class
    |
    ├─ TransitionPlayer.PlayIn(screenRoot, profile, onComplete)
    |    ├─ Sets display:flex
    |    ├─ Schedules active class after 1 frame (triggers CSS transition)
    |    └─ Schedules onComplete after Duration + Delay
    |
    ├─ NavigationManager.Push(entry)
    |    └─ Standard stack management (existing logic preserved)
    |
    ├─ FocusManager.PushFocus(screenRoot, initialFocusName)
    |    ├─ Saves currently focused element to stack
    |    └─ Sets focus on named element or first focusable
    |
    └─ Fires OnScreenOpened event + onOpened callback
```

---

## Core Interfaces

### IUIService

**File:** `Assets/Scripts/UI/Core/Services/IUIService.cs`

```csharp
public interface IUIService
{
    ScreenHandle OpenScreen(string screenId, object navigationData = null, Action<ScreenHandle> onOpened = null);
    void CloseScreen(ScreenHandle handle, Action onClosed = null);
    void CloseTop(Action onClosed = null);
    bool IsOpen(string screenId);
    bool IsOpen(ScreenHandle handle);
    ScreenHandle GetHandle(string screenId);
    void CloseAll(bool keepHUD = true);
    void SetTheme(UIThemeSO theme);
    event Action<ScreenHandle> OnScreenOpened;
    event Action<ScreenHandle> OnScreenClosed;
}
```

### ScreenHandle (Value Type)

**File:** `Assets/Scripts/UI/Core/Services/ScreenHandle.cs`

```csharp
public readonly struct ScreenHandle : IEquatable<ScreenHandle>
{
    public readonly int Id;
    public readonly string ScreenId;
    public static readonly ScreenHandle Invalid = default;
    public bool IsValid => Id > 0;
}
```

### ScreenDefinition

**File:** `Assets/Scripts/UI/Core/Services/ScreenDefinition.cs`

```csharp
[Serializable]
public class ScreenDefinition
{
    public string ScreenId;        // Unique key
    public UILayer Layer;          // Screen, Modal, HUD, Tooltip
    public string UXMLPath;        // Resources path (no extension)
    public string USSPath;         // Optional per-screen style
    public TransitionProfileSO OpenTransition;   // Nullable, falls back to manifest default
    public TransitionProfileSO CloseTransition;  // Nullable, falls back to manifest default
    public bool Poolable = true;   // Pool VisualElement tree on close
    public bool BlocksInput;       // Modal behavior
    public string InitialFocusElement;  // USS name for auto-focus
}
```

---

## ScriptableObjects

### ScreenManifestSO

**File:** `Assets/Scripts/UI/Core/Services/ScreenManifestSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Screens | `List<ScreenDefinition>` | empty | All registered screen definitions |
| DefaultOpenTransition | `TransitionProfileSO` | null | Fallback enter transition |
| DefaultCloseTransition | `TransitionProfileSO` | null | Fallback exit transition |
| DefaultTheme | `UIThemeSO` | null | Applied at startup |

Lazy `Dictionary<string, ScreenDefinition>` lookup cache, invalidated on `OnValidate()`/`OnEnable()`.

### TransitionProfileSO

**File:** `Assets/Scripts/UI/Core/Services/TransitionProfileSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| Type | `TransitionType` enum | Fade | None, Fade, SlideLeft, SlideRight, SlideUp, SlideDown, Scale |
| Duration | float [Range(0,2)] | 0.2 | Transition duration in seconds |
| EasingCurve | AnimationCurve | null | Optional custom easing (null = USS ease-out) |
| Delay | float [Range(0,1)] | 0 | Delay before transition starts |

`ActiveClass` and `StartClass` string properties are cached on first access, invalidated on `OnEnable()`/`OnValidate()`.

### UIThemeSO

**File:** `Assets/Scripts/UI/Core/Services/UIThemeSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| ThemeName | string | "Default" | Display name |
| Primary | Color | #2196F3 | Main accent color |
| PrimaryLight | Color | #64B5F6 | Lighter variant |
| PrimaryDark | Color | #1A76D2 | Darker variant |
| Background | Color | #1A1A2E | Main background |
| Surface | Color | #16213E | Card/panel surface |
| BackgroundPanel | Color | #1F1F33 (95%) | Semi-transparent panel overlay |
| TextPrimary | Color | #E0E0E0 | Main body text |
| TextSecondary | Color | #9E9E9E | Muted/label text |
| Success | Color | #4CAF50 | Success states |
| Warning | Color | #FF9800 | Warning states |
| Error | Color | #F44336 | Error/danger states |
| PrimaryFont | Font | null | Global font override |
| HeadingFont | Font | null | Heading font override |

Themes apply as inline styles on layer root elements (Unity 2022.3 lacks public `SetCustomProperty` API).

---

## Runtime Systems

### UIServiceBootstrap

**File:** `Assets/Scripts/UI/Core/Services/UIServiceBootstrap.cs`

- MonoBehaviour, `[DefaultExecutionOrder(-300)]`, `DontDestroyOnLoad`, singleton
- Loads `ScreenManifestSO` from `Resources/ScreenManifest`
- Creates or finds `UIDocument` on same GameObject (sortingOrder 100)
- Creates `UIToolkitService` instance with host root
- Applies `manifest.DefaultTheme`
- Sets `UIServices.Screen = service`

### UIServices (Static Accessor)

**File:** `Assets/Scripts/UI/Core/Services/UIServices.cs`

- `public static IUIService Screen { get; internal set; }`
- `public static bool IsInitialized => Screen != null;`
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` resets to null

### UIToolkitService : IUIService

**File:** `Assets/Scripts/UI/Core/Services/UIToolkitService.cs`

Core orchestrator. Manages a root `VisualElement` hierarchy with 4 layer containers:

```
ui-service-root (PickingMode.Ignore)
  ├── ui-layer-hud        (bottom)
  ├── ui-layer-screen
  ├── ui-layer-modal
  └── ui-layer-tooltip     (top)
```

**State:**
- `Dictionary<string, Queue<VisualElement>> _pool` — per-screenId pool (capped at 1 per screen)
- `Dictionary<int, OpenScreenState> _openScreens` — handle.Id → state
- `Dictionary<string, int> _openByName` — screenId → handle.Id
- `Dictionary<string, VisualTreeAsset> _uxmlCache` — cached Resources.Load results
- `Dictionary<string, StyleSheet> _ussCache` — cached USS loads
- `ScreenHandle _topmostModal / _topmostScreen` — O(1) CloseTop tracking
- `List<ScreenHandle> _closeAllBuffer` — reusable buffer for CloseAll

**OpenScreen flow:**
1. Look up `ScreenDefinition` from manifest
2. Check duplicate open (`_openByName`) — returns existing handle
3. Check pool for reusable VisualElement, else `Resources.Load<VisualTreeAsset>().Instantiate()`
4. Assign to correct layer container
5. Create `ScreenHandle` with `_nextHandleId++`
6. Update topmost tracking
7. Resolve transition profile (def override → manifest default → None)
8. `TransitionPlayer.Prepare()` then `TransitionPlayer.PlayIn()`
9. Push `NavigationEntry` to `NavigationManager`
10. `FocusManager.PushFocus()` (delayed 50ms for layout completion if named)
11. Fire `OnScreenOpened` event, invoke `onOpened` callback

**CloseScreen flow:**
1. Validate handle, check not mid-transition
2. `TransitionPlayer.PlayOut()` with completion callback:
   - Remove from layer container
   - Return to pool (if Poolable, max 1 per screen)
   - Remove from tracking dictionaries
   - Refresh topmost tracking
   - Pop from NavigationManager
   - `FocusManager.PopFocus()`
   - Fire `OnScreenClosed`, invoke `onClosed`

**CloseTop:** O(1) via tracked `_topmostModal` / `_topmostScreen` (no dictionary iteration).

**CloseAll:** Reuses `_closeAllBuffer` to avoid allocation. Instant close (no transition).

### TransitionPlayer

**File:** `Assets/Scripts/UI/Core/Services/TransitionPlayer.cs`

- Static utility class, no MonoBehaviour
- USS-class-based: toggles classes to trigger CSS transitions defined in `Transitions.uss`
- `Prepare()` → adds base + start classes (opacity:0, off-screen position)
- `PlayIn()` → display:flex, schedules active class after 1 frame, schedules onComplete after Duration
- `PlayOut()` → removes active class, schedules display:none after Duration
- Reuses a static `List<TimeValue>` buffer for `transitionDuration` to avoid per-call allocation
- If `TransitionType.None` or null profile: instant show/hide, synchronous callback

### FocusManager

**File:** `Assets/Scripts/UI/Core/Services/FocusManager.cs`

- Static utility with `Stack<FocusEntry>` tracking
- `PushFocus(screenRoot, initialFocusName)` — saves current focus, sets new via `.Focus()`
- `PopFocus()` — restores previous focus from stack
- `FindFirstFocusable()` — depth-first search for first visible, enabled, focusable descendant
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` resets stack

### Transitions.uss

**File:** `Assets/UI/Styles/Transitions.uss`

USS classes for transition animations:
- `.screen-transition-base` — opacity:0, transition-property: opacity/translate/scale
- `.screen-transition--fade-in` — opacity:1
- `.screen-transition--slide-*-start` / `.screen-transition--slide-*-in` — translate offsets
- `.screen-transition--scale-start` / `.screen-transition--scale-in` — scale(0.8→1)
- Duration overridden inline by TransitionPlayer from profile

### NavigationManager Enhancement

**File:** `Assets/Scripts/UI/Core/Navigation/NavigationManager.cs`

Added `ScreenHandle Handle { get; set; }` property to `NavigationEntry` class (+3 lines). No other changes to existing logic.

---

## Editor Tooling

### UIWorkstationWindow

**File:** `Assets/Editor/UIWorkstation/UIWorkstationWindow.cs`

Menu: **DIG > UI Workstation**

| Tab | Purpose | Requires Play Mode? |
|-----|---------|---------------------|
| **Setup** | One-click creation of UIServiceBootstrap, ScreenManifest, TransitionProfiles (recommended presets), Themes. "Run Full Setup" button. Validates Resources path. | No |
| **Catalog** | Lists all ScreenManifestSO entries, editable default transitions/theme via SerializedObject, add new screen entries via form. Test Open/Close buttons in Play Mode. UXML validation cached to avoid Resources.Load in OnGUI. | Catalog: No. Buttons: Yes |
| **Theme** | Full theme editor via cached SerializedObject, color swatches, live Apply button, "Set as Default Theme on Manifest". | Preview: No. Apply: Yes |
| **Stack** | Live view of NavigationManager — active screen, active modal, IDs, layers, visibility, handles. | Yes |
| **Focus** | FocusManager stack depth display. | Yes |

---

## Performance Characteristics

- **Zero per-frame cost** when no transitions are playing — `TransitionPlayer` only schedules work during active transitions
- **Screen pooling** eliminates GC from repeated UXML instantiation — pool capped at 1 per screen to prevent unbounded memory growth
- **UXML/USS caching** — `Resources.Load` results cached in dictionaries, never called twice for the same path
- **O(1) CloseTop** — topmost modal/screen handles tracked directly, no dictionary iteration
- **Zero-allocation CloseAll** — reuses a cached `List<ScreenHandle>` buffer
- **Zero-allocation transitions** — `List<TimeValue>` buffer reused across all PlayIn/PlayOut calls
- **Cached USS class strings** — `TransitionProfileSO.ActiveClass`/`StartClass` resolved once per enable, not per property access
- **Editor optimizations** — `SerializedObject` for theme tab cached across repaints; UXML validation results cached to avoid `Resources.Load` in OnGUI loop
- **USS-based transitions** leverage UI Toolkit's retained-mode rendering — no Transform manipulation, no Canvas rebuild
- **FocusManager** only runs logic on Push/Pop events, not per-frame
- **DOTS/ECS/Burst not applicable** — UI Toolkit's `VisualElement` API is managed, reference-type, main-thread-only

---

## File Manifest

| File | Type | Lines |
|------|------|-------|
| `Assets/Scripts/UI/Core/Services/IUIService.cs` | Interface | ~44 |
| `Assets/Scripts/UI/Core/Services/ScreenHandle.cs` | Struct | ~31 |
| `Assets/Scripts/UI/Core/Services/ScreenDefinition.cs` | Class | ~42 |
| `Assets/Scripts/UI/Core/Services/TransitionProfileSO.cs` | ScriptableObject | ~92 |
| `Assets/Scripts/UI/Core/Services/UIThemeSO.cs` | ScriptableObject | ~70 |
| `Assets/Scripts/UI/Core/Services/ScreenManifestSO.cs` | ScriptableObject | ~82 |
| `Assets/Scripts/UI/Core/Services/TransitionPlayer.cs` | Static class | ~170 |
| `Assets/Scripts/UI/Core/Services/FocusManager.cs` | Static class | ~135 |
| `Assets/Scripts/UI/Core/Services/UIToolkitService.cs` | Class | ~444 |
| `Assets/Scripts/UI/Core/Services/UIServices.cs` | Static class | ~28 |
| `Assets/Scripts/UI/Core/Services/UIServiceBootstrap.cs` | MonoBehaviour | ~98 |
| `Assets/UI/Styles/Transitions.uss` | Stylesheet | ~62 |
| `Assets/Editor/UIWorkstation/UIWorkstationWindow.cs` | EditorWindow | ~766 |

**Total:** ~2,064 lines

---

## Migration Path

### Phase 1: Core Service (This Epic) — COMPLETE
- Implemented `IUIService`, `UIToolkitService`, `TransitionPlayer`, `FocusManager`
- Created `ScreenManifestSO`, `TransitionProfileSO`, `UIThemeSO`
- Wired `UIServiceBootstrap` with existing `NavigationManager`
- Editor workstation with setup wizard, catalog editor, theme editor

### Phase 2: Subsystem Migration (Future)
- Migrate `PauseMenu` from UGUI to UIToolkit via `IUIService`
- Migrate `LobbyUIManager` screens to use screen manifest
- Migrate achievement/quest/progression panels to use screen lifecycle hooks
- This phase is **not** part of EPIC 18.1 — each subsystem migrates independently

---

## Verification Checklist

- [x] `UIServiceBootstrap` exists in the boot/persistent scene
- [x] `ScreenManifest.asset` exists at `Assets/Resources/ScreenManifest`
- [x] At least one `TransitionProfileSO` created and assigned as manifest default
- [x] At least one screen entry added to the manifest with a valid UXML path
- [x] In Play Mode: `UIServices.Screen.OpenScreen("YourScreenId")` opens with transition
- [x] In Play Mode: pressing Escape closes the topmost modal/screen
- [x] In Play Mode: opening the same screen twice reuses the pooled VisualElement
- [x] UI Workstation (`DIG > UI Workstation`) shows screen stack and catalog correctly
- [x] Setup tab can create all assets in one click via "Run Full Setup"
