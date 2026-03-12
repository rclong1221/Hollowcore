# SETUP GUIDE 18.5: Dialogue System Enhancement — Visual Graph Editor & Runtime Polish

**Status:** Implemented
**Last Updated:** February 25, 2026
**Requires:** DialogueDatabaseSO (EPIC 16.16), DialogueConfigSO (EPIC 16.16), dialogue system fully set up per EPIC 16.16

This guide covers Unity Editor setup for the EPIC 18.5 dialogue enhancements: visual graph editor, speaker profiles, typewriter effect, dialogue history, and priority/interrupt system.

---

## What Changed

Previously, dialogue trees were authored via flat arrays in the inspector (manually wiring NodeIds), text appeared all at once, there were no speaker portraits, no voice-over linkage, no dialogue history, and no priority system for interrupts.

Now:

- **Visual graph editor** for node-based dialogue authoring with color-coded nodes, drag-and-drop edges, minimap, and auto-layout
- **Speaker profiles** with per-expression portrait sprites, text colors, and voice mumble banks
- **Typewriter text reveal** with configurable speed and punctuation pauses
- **Dialogue history** ring buffer for player review of past lines
- **Priority & interrupt system** — higher priority dialogue interrupts lower, with resume/restart/discard behavior
- **Enhanced validator** — circular loop detection, missing VO warnings, VO/expression coverage stats
- **Workstation overview** — tree browser with validation badges, speaker registry, and statistics panel

---

## 1. Configure DialogueConfigSO (Typewriter & History)

Open your existing `Assets/Resources/DialogueConfig.asset` in the inspector. New fields added under EPIC 18.5 headers:

### Typewriter Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Typewriter Chars Per Second** | float (min 0) | 40 | Characters revealed per second. 0 = instant (typewriter disabled) |
| **Pause Period** | float (min 0) | 0.3 | Pause duration in seconds after a `.` character |
| **Pause Comma** | float (min 0) | 0.15 | Pause duration in seconds after a `,` character |
| **Pause Exclamation** | float (min 0) | 0.25 | Pause duration in seconds after `!` or `?` |

### History Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **History Capacity** | int (10–200) | 50 | Maximum dialogue lines stored in history for player review |

> **Tip:** Set Typewriter Chars Per Second to 0 during development to skip typewriter animations and speed up testing.

---

## 2. Create Speaker Profiles

Speaker profiles define portraits, text colors, and voice banks for named speakers.

### Creating a Profile

1. **Project window** → right-click → **Create > DIG > Dialogue > Speaker Profile**
2. Save in `Assets/Resources/SpeakerProfiles/` (the bootstrap system loads from this path at runtime)
3. Configure the profile in the inspector

### Inspector Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| **Speaker Name** | string | **Yes** | Display name — must **exactly match** the `SpeakerName` field used in dialogue nodes |
| **Speaker Name Hash** | int | Auto | Computed automatically from SpeakerName on asset save — **do not edit manually** |
| **Portraits** | SpeakerPortrait[] | No | Expression → Sprite mapping array (see below) |
| **Default Portrait** | Sprite | No | Fallback portrait when an expression key has no match |
| **Text Color** | Color | White | Text color for this speaker's dialogue lines in the UI |
| **Name Plate Color** | Color | Blue | Accent color for the speaker name plate in the dialogue UI |
| **Voice Bank** | AudioClip[] | No | Random mumble clips played per character during typewriter reveal |

### Portraits Array

Each entry maps an expression key to a portrait sprite:

| Field | Type | Description |
|-------|------|-------------|
| **Expression** | string | Expression key (e.g., `"neutral"`, `"happy"`, `"angry"`, `"sad"`, `"surprised"`) |
| **Sprite** | Sprite | Portrait sprite for this expression |

The expression key is matched (case-insensitive) against the `Expression` field on each `DialogueNode`. If no match is found, `DefaultPortrait` is used.

### Example Setup

```
Assets/Resources/SpeakerProfiles/
  Guard_Profile.asset          Speaker Name: "Guard"
  Merchant_Profile.asset       Speaker Name: "Merchant"
  Boss_Profile.asset           Speaker Name: "Boss"
```

> **Important:** The `SpeakerName` on the profile must exactly match the `SpeakerName` value written into your dialogue nodes. If they don't match, the system gracefully falls back to no portrait (no crash), but the speaker won't have expression portraits or voice mumble.

---

## 3. Configure DialogueNode Presentation Fields

Each dialogue node now has additional presentation fields in the **Presentation (EPIC 18.5)** header group in the inspector:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Expression** | string | `""` | Portrait expression key for this line (matches SpeakerPortrait.Expression) |
| **Voice Clip** | AudioClip | null | Voice-over audio clip for this specific dialogue line |
| **Typewriter Speed** | float (min 0) | 0 | Override typewriter speed in chars/sec. **0 = use global default** from DialogueConfigSO |

These fields are editable in both the flat inspector and the visual graph editor.

### Setting Expressions

Use the same expression keys defined in your speaker profiles. Common convention:

| Key | Use Case |
|-----|----------|
| `neutral` | Default/idle expression |
| `happy` | Positive reactions, greetings |
| `angry` | Threats, confrontation |
| `sad` | Loss, disappointment |
| `surprised` | Revelations, shock |

You can define any custom expression key — just ensure it matches an entry in the speaker's Portraits array.

---

## 4. Configure Tree Priority & Interrupt Behavior

Each `DialogueTreeSO` now has priority settings in the **Priority (EPIC 18.5)** header:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Priority** | DialoguePriority | Exploration | Priority level for this dialogue tree |
| **Interrupt Behavior** | InterruptBehavior | Discard | What happens when this dialogue is interrupted by higher priority |

### Priority Levels

| Level | Value | Use Case |
|-------|-------|----------|
| **Ambient** | 0 | Environmental barks, NPC idle chatter |
| **Exploration** | 50 | Player-initiated NPC conversations (default) |
| **Story** | 100 | Scripted story beats, cutscenes |
| **Combat** | 150 | Combat barks, callouts |
| **System** | 200 | System messages, tutorial override |

### Interrupt Behavior

| Behavior | Effect |
|----------|--------|
| **Discard** | Interrupted dialogue is lost — cannot resume |
| **Resume** | Interrupted dialogue resumes from the last node when the interrupting dialogue ends |
| **Restart** | Interrupted dialogue restarts from the beginning when the interrupting dialogue ends |

### Priority Rules

- **Higher priority always interrupts lower priority** — the current dialogue is paused or discarded
- **Same or lower priority is silently dropped** — the current dialogue continues uninterrupted
- After the interrupting dialogue completes, the system attempts to resume/restart the interrupted dialogue (unless Discard was set)

### Recommended Priority Assignments

| Content Type | Priority | Interrupt Behavior |
|--------------|----------|--------------------|
| NPC idle barks | Ambient | Discard |
| Shop / vendor dialogue | Exploration | Discard |
| Quest-critical NPC talk | Story | Resume |
| Cutscene dialogue | Story | Discard |
| Boss taunt mid-fight | Combat | Discard |
| Tutorial popups | System | Discard |

---

## 5. Visual Graph Editor

### Opening the Graph Editor

| Method | How |
|--------|-----|
| Menu | **DIG > Dialogue > Graph Editor** |
| Double-click | Double-click any `DialogueTreeSO` asset in the Project window |
| Inspector | Click **"Open in Graph Editor"** button on any DialogueTreeSO inspector |

### Toolbar

| Button | Action |
|--------|--------|
| **Tree field** | Drag a DialogueTreeSO asset to load it into the editor |
| **Save** | Write current graph state back to the DialogueTreeSO asset (supports Undo) |
| **Auto-Layout** | Arrange all nodes in a top-to-bottom BFS layout for readability |
| **+ Add Node** | Dropdown menu to add Speech, Choice, Condition, Action, Random, or End nodes |
| **Validate** | Save and run the enhanced validator, displaying results in a dialog |

### Node Types (Color-Coded)

| Node Type | Color | Output Ports | Description |
|-----------|-------|-------------|-------------|
| Speech | Blue | Next | A single line of dialogue from a speaker |
| PlayerChoice | Green | One per choice option | Presents choices to the player |
| Condition | Orange | True, False | Branches based on a condition |
| Action | Purple | Next | Triggers a game action |
| Random | Yellow | One per weighted entry | Random branch selection |
| End | Red | None (terminal) | Terminates the dialogue tree |
| Hub | Gray | Next | Passthrough node (displays like Speech) |

### Interactions

| Input | Action |
|-------|--------|
| **Right-click** canvas | Context menu to add nodes at cursor position |
| **Drag** from output port to input port | Create a connection (edge) |
| **Select + Delete key** | Remove selected nodes |
| **Scroll wheel** | Zoom in/out |
| **Middle-click + drag** | Pan the canvas |
| **Minimap** (bottom-left) | Click to navigate large trees |

### Visual Indicators

- Start node is marked with a green **START** badge
- Node IDs shown in title bar (e.g., `Speech (5)`)
- Speech nodes show editable Speaker, Text, Expression, Duration, and Typewriter Speed fields

### Save Workflow

1. Edit nodes and connections in the graph
2. Click **Save** (or close and reopen — unsaved changes are lost)
3. Save writes all nodes, edges, and node positions back to the `DialogueTreeSO` asset
4. The Save operation is registered with Unity's Undo system — use **Ctrl+Z** to undo

> **Note:** Individual graph edits (moving nodes, creating edges) do not register Undo steps. Only the Save operation is undoable.

---

## 6. Workstation Overview Tab

Open via **DIG > Dialogue Workstation** — a new **Overview** tab has been added as the first tab.

### Sub-Tabs

#### Trees

- Assign a `DialogueDatabaseSO` in the database field at the top
- Click **Validate All** to run the enhanced validator on every tree
- Each tree row shows:
  - Validation badge (see below)
  - Tree ID and display name
  - Node count and priority level
  - **Graph** button — opens tree in the visual graph editor
  - **Select** button — selects the asset in the Project window
- Click **Refresh** to clear validation cache

#### Speakers

- Click **Load Speaker Profiles** to scan for all `DialogueSpeakerProfileSO` assets
- Searches `Resources/SpeakerProfiles/` first, then falls back to `AssetDatabase.FindAssets`
- Each profile row shows:
  - Default portrait preview (32×32)
  - Speaker name (bold)
  - Expression count and voice clip count
  - Name plate color swatch
  - **Select** button

#### Statistics

- Click **Recompute Statistics** to calculate (not computed on every repaint — no editor lag)
- Shows:
  - Total trees, total nodes, speech/choice/condition breakdown
  - Max tree depth (BFS-calculated, safe for diamond DAGs)
  - Average nodes per tree
  - **VO Coverage** — percentage of speech nodes with a VoiceClip or AudioClipPath
  - **Expression Coverage** — percentage of speech nodes with a non-empty Expression

### Validation Badges

After clicking **Validate All** on the Trees sub-tab:

| Badge | Color | Meaning |
|-------|-------|---------|
| **OK** | Green | No issues found |
| **W:N** | Yellow | N warnings (orphan nodes, missing End, empty text, circular loops) |
| **E:N** | Red | N errors (broken references, no nodes, missing start node) |

---

## 7. Enhanced Validator

The validator is accessible from:
- **Graph Editor** → Validate button (validates the currently loaded tree)
- **Workstation** → Overview tab → Validate All (validates all trees in a database)
- **Workstation** → Validator tab (existing EPIC 16.16 validator, also enhanced)

### Validation Checks

| Check | Severity | Description |
|-------|----------|-------------|
| Missing StartNodeId | Error | Start node ID doesn't match any node in the tree |
| No nodes | Error | Tree has zero nodes |
| Broken NextNodeId | Error | A node references a non-existent target node |
| Broken TrueNodeId / FalseNodeId | Error | Condition branch targets don't exist |
| Broken Choice target | Error | A choice option targets a non-existent node (unconnected choices ignored) |
| Broken Random target | Error | A random entry targets a non-existent node (unconnected entries ignored) |
| Dead end | Error | Non-End node with no outgoing connection |
| No choices | Error | PlayerChoice node with empty choices array |
| No random entries | Error | Random node with empty entries array |
| Duplicate TreeId | Error | Two or more trees in the database share the same TreeId |
| Unreachable node | Warning | Node not reachable from Start via any path (orphan) |
| Empty text | Warning | Speech node with no text content |
| No End node | Warning | Tree has no terminal End node |
| Circular loop | Warning | Node is part of a cycle — ensure there's an exit condition to prevent infinite loops |
| Missing VO | Info | Speech node has a speaker name but no VoiceClip or AudioClipPath assigned |

> **Note:** Unconnected choice and random entry ports (NextNodeId/NodeId = 0) are treated as "not yet wired" and do **not** trigger broken-reference errors. This prevents false positives while authoring.

---

## 8. Programmatic Priority API

To queue a priority dialogue from gameplay code (encounter scripts, triggers, cutscene systems):

```csharp
using DIG.Dialogue;

// Queue a Story-priority dialogue on an NPC for a specific player
DialoguePrioritySystem.QueueDialogue(npcEntity, playerEntity, treeId, DialoguePriority.Story);
```

The priority system evaluates the request on the next frame:
- **Higher priority** → interrupts current dialogue, saves state if the interrupted tree's InterruptBehavior is Resume or Restart
- **Same or lower priority** → silently drops the request (current dialogue continues)

---

## 9. Resources Folder Layout

After setup, your Resources folder should include:

```
Assets/Resources/
  DialogueDatabase.asset            (existing, EPIC 16.16)
  DialogueConfig.asset              (existing, updated with 18.5 typewriter/history fields)
  BarkCollections/                  (existing, EPIC 16.16)
  SpeakerProfiles/                  (NEW — one .asset per speaker)
    Guard_Profile.asset
    Merchant_Profile.asset
    Boss_Profile.asset
    ...
```

---

## 10. What's Automatic (No Setup Required)

These features work out of the box after the above configuration:

| Feature | How It Works |
|---------|-------------|
| Speaker profile loading | `DialogueBootstrapSystem` loads all profiles from `Resources/SpeakerProfiles/` on startup (runs on server, client, and local worlds) |
| Speaker name hash | Computed automatically via `OnValidate` when the profile asset is saved. At runtime, profiles with a zero hash are defensively recomputed |
| Typewriter config propagation | `DialogueBootstrapSystem` copies typewriter settings from `DialogueConfigSO` to the ECS `DialogueConfig` singleton |
| History capacity | `DialogueBootstrapSystem` copies history capacity setting to ECS |
| Priority system scheduling | `DialoguePrioritySystem` auto-registers before `DialogueInitiationSystem` in `SimulationSystemGroup` |
| UI bridge new fields | `DialogueUIBridgeSystem` automatically populates Expression, VoiceClip, TypewriterSpeed, and Priority on `DialogueUIState` |
| UI bridge guard | `DialogueUIBridgeSystem` skips updates until the `DialogueRegistryManaged` singleton exists (no wasted frames before bootstrap) |
| Graph position persistence | `NodeEditorPositions[]` on `DialogueTreeSO` is automatically saved/loaded by the graph editor |
| Typewriter text caching | `DialogueTypewriter.RevealedText` caches the string and only regenerates when characters are revealed (no per-frame allocation) |

---

## 11. Architecture Notes

- **Zero runtime cost from editor tools** — graph editor, validator, overview module are editor-only (`#if UNITY_EDITOR`)
- **Zero new ECS components on player** — all enhancements are in managed bridge/UI layer or on NPC entities
- **No player archetype changes** — safe for the 16KB ghost archetype limit
- **DialogueTypewriter** uses pre-allocated StringBuilder with dirty-flag caching — no per-character or per-frame string allocation
- **DialogueHistoryBuffer** is a fixed-size ring buffer — zero GC after initialization
- **DialoguePrioritySystem** is a managed SystemBase (not Burst) — priority evaluation is O(queue_size) per frame, typically 0–1 requests
- **Speaker profile lookup** uses `Animator.StringToHash` for O(1) retrieval — no string comparisons at runtime
- **Validator** uses iterative BFS for reachability and iterative DFS (white/gray/black coloring) for cycle detection — no stack overflow risk on deep trees, O(N+E)
- **Statistics** are cached and recomputed only on button press — no per-repaint computation (prevents editor hang on large databases)
- **Backward compatible** — if no speaker profiles exist, the system gracefully falls back to null portraits. If typewriter speed is 0, text appears instantly. If no priority is set, Exploration is the default

---

## Verification Checklist

### DialogueConfigSO

- [ ] Open `Assets/Resources/DialogueConfig.asset` in inspector
- [ ] Typewriter Chars Per Second, Pause Period, Pause Comma, Pause Exclamation fields are visible
- [ ] History Capacity field is visible (default: 50)

### Speaker Profiles

- [ ] At least one `DialogueSpeakerProfileSO` created in `Assets/Resources/SpeakerProfiles/`
- [ ] Speaker profile's `SpeakerName` matches a `SpeakerName` used in dialogue nodes
- [ ] `SpeakerNameHash` auto-populates when you save the profile (non-zero value)
- [ ] Default portrait assigned (optional but recommended)
- [ ] At least one expression entry in the Portraits array

### Visual Graph Editor

- [ ] **DIG > Dialogue > Graph Editor** opens correctly
- [ ] Drag a DialogueTreeSO to the tree field — graph populates with color-coded nodes
- [ ] Right-click canvas → context menu shows all node types
- [ ] Drag edges between output and input ports
- [ ] Save + close + reopen preserves node positions and connections
- [ ] **Auto-Layout** arranges nodes in readable top-to-bottom flow
- [ ] **Validate** button reports issues (test with an intentionally broken tree)

### DialogueTreeSO Inspector

- [ ] "Open in Graph Editor" and "Open in Workstation" buttons visible
- [ ] Priority dropdown shows: Ambient, Exploration, Story, Combat, System
- [ ] Interrupt Behavior dropdown shows: Discard, Resume, Restart

### Workstation Overview

- [ ] **DIG > Dialogue Workstation** opens with Overview as the first tab
- [ ] Trees sub-tab: assign database, click "Validate All" — badges appear
- [ ] Speakers sub-tab: click "Load Speaker Profiles" — profiles listed with preview
- [ ] Statistics sub-tab: click "Recompute Statistics" — counts and coverage percentages shown

### Runtime Verification

- [ ] Enter Play Mode → initiate dialogue → console shows `[DialogueBootstrap] Loaded X dialogue trees, Y bark collections, Z speaker profiles.`
- [ ] Speaker profile count matches expected number of profiles in Resources/SpeakerProfiles/
- [ ] If typewriter speed > 0, dialogue text reveals character-by-character
- [ ] If voice bank assigned, mumble SFX plays during typewriter reveal
