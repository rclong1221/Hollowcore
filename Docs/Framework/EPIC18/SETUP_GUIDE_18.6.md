# SETUP GUIDE 18.6: Scene Management & Level Flow System

**Status:** Implemented  
**Last Updated:** February 25, 2026  
**Requires:** GameBootstrap, LobbyToGameTransition, InputContextManager (existing)

> **Note:** Debug.Log output appears only in Editor and Development builds. Release builds have no scene-management logs.

This guide covers Unity Editor setup for the EPIC 18.6 scene management system: state-machine-driven game flow, async scene loading with progress, configurable loading screens, and designer tooling.

---

## Prerequisites

Before the scene management system works at runtime, you must:

1. **Create the GameFlowDefinition asset** (via Setup Wizard)
2. **Add SceneService and LoadingScreenManager to your main scene** (or ensure they are instantiated at runtime)

Without these steps, `SceneService.Instance` and `LoadingScreenManager.Instance` will be null. The game will still run (LobbyManager uses optional chaining), but the EPIC 18.6 loading screen and lifecycle events will not execute.

---

## 1. Open the Scene Workstation

| Method | How |
|--------|-----|
| Menu | **DIG > Scene Workstation** |

The Scene Workstation window has five modules:

| Module | Purpose |
|--------|---------|
| **Setup Wizard** | One-click creation of GameFlowDefinition, LoadingScreenProfile, LoadingScreen prefab, and SceneDefinitions |
| **Flow Graph** | Visual state machine editor — states as nodes, transitions as arrows |
| **Scene Assignment** | Inspect and validate scene assignments per state |
| **Loading Preview** | Preview loading screen profiles (background, tips, progress bar style) |
| **Transition Tester** | Trigger transitions and fire events in Play Mode |

---

## 2. Create Required Assets (Setup Wizard)

The Setup Wizard creates all assets needed for the scene management system.

### Step 1: Run the Setup Wizard

1. Open **DIG > Scene Workstation**
2. Select the **Setup Wizard** module in the sidebar
3. Use **Refresh Status** to update the asset checklist (refreshes automatically every 2 seconds, or click the button)
4. Click **Create All Missing** — or create each item individually:

| Asset | Location | Created By |
|-------|----------|------------|
| **Game Flow Definition** | `Assets/Resources/GameFlowDefinition.asset` | "Create Game Flow Definition" button |
| **Default Loading Screen Profile** | `Assets/Resources/DefaultLoadingScreen.asset` | "Create Default Loading Screen Profile" button |
| **Loading Screen Prefab** | `Assets/Prefabs/SceneManagement/LoadingScreen.prefab` | "Create Loading Screen Prefab" button |
| **Scene Definitions** | `Assets/Resources/SceneManagement/*SceneDef.asset` | "Create" buttons under Scene Definitions (MainMenu, Lobby, Gameplay) |

### Step 2: Verify Creation

- **Green checkmarks** in the Setup Wizard indicate assets exist
- **Red X** indicates the asset is missing — use the corresponding button to create it
- Click **Refresh Status** if you created assets outside the wizard (e.g., via Project window)

> **Important:** The `GameFlowDefinition` asset **must** exist at `Assets/Resources/GameFlowDefinition.asset`. SceneService loads it from Resources at Awake. If it is missing, SceneService logs a warning (in Editor/Development builds) and disables flow management.

---

## 3. Add SceneService and LoadingScreenManager to the Scene

SceneService and LoadingScreenManager are MonoBehaviour singletons. They must exist in the scene hierarchy for the system to work.

### Option A: Add the Loading Screen Prefab (Recommended)

1. Open your **main scene** (e.g., `Assets/Scenes/Scene.unity`)
2. In the **Project** window, locate `Assets/Prefabs/SceneManagement/LoadingScreen.prefab`
3. **Drag** the prefab into the **Hierarchy**
4. The prefab contains:
   - **LoadingScreenManager** (root) — singleton, DontDestroyOnLoad
   - **Canvas** child with **LoadingScreenView** — progress bar, phase text, tips, background

### Option B: Add SceneService Separately

The LoadingScreen prefab does **not** include SceneService. You must add it manually:

1. In the **Hierarchy**, create an empty GameObject: **Right-click > Create Empty**
2. Rename it to **SceneService**
3. With the GameObject selected, click **Add Component**
4. Search for **SceneService** and add it
5. SceneService will load `GameFlowDefinition` from Resources at Awake — no inspector fields to configure

### Option C: Combined Bootstrap GameObject

Create a single root GameObject that holds both:

1. Create empty GameObject named **SceneManagementBootstrap**
2. Add **SceneService** component
3. Drag the **LoadingScreen** prefab as a child (or instantiate it at runtime)

> **Note:** SceneService and LoadingScreenManager both use `DontDestroyOnLoad`, so they persist across scene loads. Place them in your first/boot scene.

---

## 4. Configure the Game Flow Definition

Open `Assets/Resources/GameFlowDefinition.asset` in the inspector.

### Flow-Level Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Flow Name** | string | "Default" | Display name for this flow |
| **Initial State** | string | "MainMenu" | State entered on boot |
| **States** | GameFlowState[] | — | All game phases (MainMenu, Lobby, Gameplay, etc.) |
| **Transitions** | GameFlowTransition[] | — | Valid state changes and their triggers |
| **Default Loading Screen** | LoadingScreenProfileSO | null | Fallback when a state has none assigned |
| **Default Transition Animation** | enum | Fade | Fade, Dissolve, SlideLeft, SlideRight |
| **Default Transition Duration** | float | 0.5 | Seconds for transition animation |

### Per-State Settings (GameFlowState)

| Field | Type | Description |
|-------|------|-------------|
| **State Id** | string | Unique ID (e.g., "MainMenu", "Lobby", "Gameplay") |
| **Scene** | SceneDefinitionSO | Primary scene to load (null for network-only states) |
| **Additive Scenes** | SceneDefinitionSO[] | Additional scenes loaded alongside primary |
| **Loading Screen** | LoadingScreenProfileSO | Override for this state (null = use default) |
| **Requires Network** | bool | If true, uses GameBootstrap/LobbyToGameTransition for ECS worlds |
| **Input Context** | enum | InputContext to activate when entering this state |
| **On Enter Event** | string | Event name fired on enter (for external listeners) |
| **On Exit Event** | string | Event name fired on exit |

### Per-Transition Settings (GameFlowTransition)

| Field | Type | Description |
|-------|------|-------------|
| **From State** | string | Source state ID |
| **To State** | string | Target state ID |
| **Condition** | enum | Event, Immediate, or SceneLoaded |
| **Trigger Event** | string | Event name that triggers this transition (when Condition = Event) |
| **Animation** | enum | Visual transition (Fade, Dissolve, etc.) |
| **Animation Duration** | float | Seconds |

---

## 5. Configure Scene Definitions

Scene definitions tell the system which scene to load and how.

**Create via:** Setup Wizard → Scene Definitions section, or **Create > DIG > Scene Management > Scene Definition**

| Field | Type | Description |
|-------|------|-------------|
| **Scene Id** | string | Unique identifier |
| **Display Name** | string | Shown in loading screen |
| **Scene Name** | string | **Exact name** as in Build Settings (e.g., "Scene", "Lobby") |
| **Load Mode** | enum | Single, Additive, or SubScene |
| **Sub Scene Guids** | string[] | SubScene GUIDs when LoadMode = SubScene |
| **Required Sub Scenes** | string[] | Must load before scene is "ready" |
| **Unload Previous** | bool | Unload previous scene (Single mode) |
| **Min Load Time Seconds** | float | Minimum loading screen display time |

> **Important:** For **Single** or **Additive** modes, the scene **must** be in **File > Build Settings > Scenes In Build**. The Scene Assignment module validates this.

---

## 6. Configure Loading Screen Profiles

**Create via:** Setup Wizard, or **Create > DIG > Scene Management > Loading Screen Profile**

| Field | Type | Description |
|-------|------|-------------|
| **Background Sprites** | Sprite[] | Random background art per load |
| **Tips** | string[] | Tip text pool — one chosen at random, rotated every 5 seconds |
| **Show Progress Bar** | bool | Display loading progress |
| **Progress Bar Style** | enum | Continuous, Stepped, or Indeterminate |
| **Min Display Seconds** | float | Minimum time loading screen stays visible |
| **Fade In Duration** | float | Fade-in time |
| **Fade Out Duration** | float | Fade-out time |
| **Music Clip** | AudioClip | Music during loading (null = keep current) |

Assign the profile to a state's **Loading Screen** field, or set it as **Default Loading Screen** on the GameFlowDefinition.

> **Performance:** Progress bar updates are throttled (min 50ms interval or 1% change) to reduce Canvas rebuilds during loading.

---

## 7. Flow Graph Module

The Flow Graph provides a visual representation of the state machine.

1. Open **DIG > Scene Workstation** → **Flow Graph**
2. Assign a **GameFlowDefinitionSO** in the Flow Definition field
3. States appear as boxes; transitions as arrows
4. **Click** a state to inspect it in the panel below
5. **Pan** with middle-click + drag; **zoom** with scroll wheel

Use this to verify your flow structure and inspect state assignments.

---

## 8. Scene Assignment Module

Use this module to validate scene assignments and Build Settings.

1. Open **DIG > Scene Workstation** → **Scene Assignment**
2. Assign a **GameFlowDefinitionSO**
3. Each state shows its primary scene, load mode, and additive scenes
4. Click **Validate All** to check:
   - Scenes are in Build Settings
   - SubScene GUIDs are assigned when LoadMode = SubScene
   - Transitions reference valid state IDs

Warnings and errors appear in the module and in the Console.

---

## 9. Transition Tester (Play Mode)

Test transitions without going through the full lobby flow.

1. **Enter Play Mode**
2. Open **DIG > Scene Workstation** → **Transition Tester**
3. **Request Transition:** Enter a state ID (e.g., "Gameplay") and click **Go**
4. **Fire Event:** Enter an event name (e.g., "StartGame") and click **Fire** — triggers any transition with matching TriggerEvent
5. **Return to State:** Use **Return to MainMenu** or **Return to Lobby** to tear down network and transition back

> **Note:** Transition Tester requires SceneService to exist. If you see "SceneService not found", ensure SceneService is in the scene and GameFlowDefinition exists in Resources.

---

## 10. Resources Folder Layout

After setup, your Resources folder should include:

```
Assets/Resources/
  GameFlowDefinition.asset           (required — SceneService loads this at Awake)
  DefaultLoadingScreen.asset         (optional — fallback loading screen profile)
  SceneManagement/                   (optional — scene definitions)
    MainMenuSceneDef.asset
    LobbySceneDef.asset
    GameplaySceneDef.asset
```

---

## 11. Prefab Layout

The LoadingScreen prefab structure:

```
LoadingScreen (root)
├── LoadingScreenManager (component)
└── Canvas
    ├── LoadingScreenView (component)
    ├── Background (Image)
    ├── ProgressBar (Slider)
    ├── PhaseText (Text)
    └── TipText (Text)
```

The Setup Wizard creates this prefab with all references wired. Do not break the `LoadingScreenManager._view` reference.

---

## Verification Checklist

### Assets

- [ ] `Assets/Resources/GameFlowDefinition.asset` exists
- [ ] `Assets/Resources/DefaultLoadingScreen.asset` exists (or another profile assigned as default)
- [ ] `Assets/Prefabs/SceneManagement/LoadingScreen.prefab` exists
- [ ] Scene definitions exist for MainMenu, Lobby, Gameplay (or your states)

### Scene Hierarchy

- [ ] **SceneService** component is in the scene (on any GameObject)
- [ ] **LoadingScreen** prefab (or LoadingScreenManager) is in the scene
- [ ] LoadingScreenManager's `_view` reference is wired to LoadingScreenView (if using prefab)

### GameFlowDefinition

- [ ] Initial State is set (e.g., "MainMenu")
- [ ] States array has entries for MainMenu, Lobby, Gameplay
- [ ] Each state has Scene assigned (or Requires Network = true for Gameplay)
- [ ] Transitions array has Lobby → Gameplay (TriggerEvent "StartGame") and other flows
- [ ] Default Loading Screen is assigned (or each state has its own)

### Build Settings

- [ ] All scenes referenced in SceneDefinitions (Single/Additive mode) are in **File > Build Settings > Scenes In Build**
- [ ] Scene Assignment module **Validate All** reports no errors

### Runtime Verification

- [ ] Enter Play Mode → Console shows `[SceneService] Initialized: flow='Default', initial='MainMenu'` (Editor/Development builds only)
- [ ] Transition Tester shows "Current State" and allows Request Transition / Fire Event
- [ ] Lobby → Start Game → loading screen appears (if LoadingScreenManager is in scene)
- [ ] No "SceneService not found" or "No GameFlowDefinition in Resources" warnings
