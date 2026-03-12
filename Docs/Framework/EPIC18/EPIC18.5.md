# EPIC 18.5: Dialogue System Enhancement — Visual Graph Editor & Runtime Polish

**Status:** PLANNED
**Priority:** Medium-High (Content creation velocity for narrative games)
**Dependencies:**
- `DialogueTreeSO` (existing — `DIG.Dialogue`, `Assets/Scripts/Dialogue/Definitions/DialogueTreeSO.cs`, branching tree with BlobAsset baking)
- `DialogueTreeSOEditor` (existing — `DIG.Dialogue.Editor`, `Assets/Scripts/Dialogue/Editor/DialogueTreeSOEditor.cs`, basic custom inspector)
- `DialogueNode` / `DialogueStructs` / `DialogueEnums` (existing — `DIG.Dialogue.Definitions`, node types, choices, conditions, actions)
- `DialogueBlobs` (existing — `DIG.Dialogue.Data`, Burst-compatible blob traversal)
- `DialogueInitiationSystem` / `DialogueAdvanceSystem` (existing — `DIG.Dialogue.Systems`, ECS runtime systems)
- `DialogueUIBridgeSystem` / `IDialogueUIProviders` (existing — `DIG.Dialogue.Bridges`, ECS-to-UI bridge)
- `DialogueCameraSystem` (existing — `DIG.Dialogue.Systems`, camera modes during dialogue)
- `DialogueLocalization` (existing — `DIG.Dialogue.Bridges`, localization support)
- `BarkCollectionSO` / `BarkTimerSystem` / `BarkDisplaySystem` (existing — `DIG.Dialogue`, ambient bark system)
- `DialogueDatabaseSO` (existing — `DIG.Dialogue.Definitions`, centralized dialogue registry)
- `DialogueConfigSO` (existing — `DIG.Dialogue.Definitions`, global dialogue configuration)
- `EncounterDialogueBridgeSystem` (existing — `DIG.Dialogue.Systems`, encounter integration)

**Feature:** A professional visual graph editor for authoring dialogue trees (replacing the flat inspector array), with node-based editing, live preview, localization table integration, voice-over clip assignment, conditional branch visualization, and runtime enhancements including typewriter text effect, portrait animations, dialogue history log, and interrupt/priority system.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `DialogueTreeSO` | `Assets/Scripts/Dialogue/Definitions/DialogueTreeSO.cs` | Fully implemented | Branching tree, BakeToBlob(), node types (Dialogue, Choice, Condition, Action, Random), NodeEditorPositions array for graph layout |
| `DialogueNode` | `Assets/Scripts/Dialogue/Definitions/DialogueStructs.cs` | Fully implemented | Speaker, text, choices, conditions, actions, camera mode |
| `DialogueTreeSOEditor` | `Assets/Scripts/Dialogue/Editor/DialogueTreeSOEditor.cs` | Basic | Custom inspector (not a graph editor) |
| `DialogueInitiationSystem` | `Assets/Scripts/Dialogue/Systems/DialogueInitiationSystem.cs` | Fully implemented | Starts dialogue from ECS triggers |
| `DialogueAdvanceSystem` | `Assets/Scripts/Dialogue/Systems/DialogueAdvanceSystem.cs` | Fully implemented | Advances through nodes, processes choices |
| `DialogueConditionSystem` | `Assets/Scripts/Dialogue/Systems/DialogueConditionSystem.cs` | Fully implemented | Evaluates conditions for branching |
| `DialogueActionSystem` | `Assets/Scripts/Dialogue/Systems/DialogueActionSystem.cs` | Fully implemented | Executes actions on nodes |
| `DialogueUIBridgeSystem` | `Assets/Scripts/Dialogue/Bridges/DialogueUIBridgeSystem.cs` | Fully implemented | ECS → UI event bridge |
| `DialogueCameraSystem` | `Assets/Scripts/Dialogue/Systems/DialogueCameraSystem.cs` | Fully implemented | Camera modes during dialogue |
| `DialogueLocalization` | `Assets/Scripts/Dialogue/Bridges/DialogueLocalization.cs` | Implemented | Text localization |
| `BarkCollectionSO` | `Assets/Scripts/Dialogue/Definitions/BarkCollectionSO.cs` | Fully implemented | Ambient barks |
| `DialogueDatabaseSO` | `Assets/Scripts/Dialogue/Definitions/DialogueDatabaseSO.cs` | Fully implemented | Central dialogue registry |
| `NodeEditorPositions` | `DialogueTreeSO.cs` | Partially used | Vector2[] array for node positions — infrastructure for graph editor exists but no graph editor uses it |

### What's Missing

- **No visual graph editor** — `DialogueTreeSOEditor` is a flat inspector; designers must manage node IDs and connections manually via arrays. `NodeEditorPositions` exists but is unused
- **No typewriter text effect** — dialogue text appears all at once; no character-by-character reveal
- **No portrait system** — no speaker portraits/expressions during dialogue
- **No voice-over clip linkage** — no way to assign audio clips to dialogue lines for VO playback
- **No dialogue history** — player cannot scroll back to re-read previous lines
- **No interrupt/priority** — no way for combat barks to interrupt exploration dialogue or vice versa
- **No preview mode** — cannot test dialogue flow without entering Play mode
- **No validation tooling** — no way to detect orphaned nodes, dead ends, unreachable branches, or missing localization keys

---

## Problem

DIG has a sophisticated ECS-backed dialogue runtime (initiation, advance, conditions, actions, camera, barks, localization) with BlobAsset-based traversal, but authoring is painful. Designers edit dialogue by manipulating flat arrays of `DialogueNode` in the inspector, manually wiring node IDs. This is error-prone (wrong IDs create broken branches) and slow (no visual overview of conversation flow). The `NodeEditorPositions` array shows the intent for a graph editor but none exists. Additionally, the runtime lacks polish expected in AAA dialogue: typewriter reveal, speaker portraits, VO playback, and conversation history.

---

## Architecture Overview

```
                    EDITOR LAYER
  DialogueGraphEditorWindow (EditorWindow + GraphView)
  (visual node graph for authoring DialogueTreeSO,
   node types color-coded, edge connections via drag,
   minimap, search, validation panel, preview playback)
        |
        ├── DialogueNodeView (per node type)
        |    ├── DialogueNodeView (text + speaker + VO clip)
        |    ├── ChoiceNodeView (choice buttons listed)
        |    ├── ConditionNodeView (if/else branch)
        |    ├── ActionNodeView (action type + params)
        |    └── RandomNodeView (weighted random)
        |
        ├── DialogueGraphSerializer
        |    (reads/writes DialogueTreeSO.Nodes[] and
        |     DialogueTreeSO.NodeEditorPositions[])
        |
        └── DialogueValidator
             (dead-end detection, orphan nodes, missing
              localization, circular loops, empty text)
                         |
                    RUNTIME ENHANCEMENTS
                         |
  DialogueUIView (enhanced managed view)
  (typewriter text reveal, speaker portrait display,
   VO audio playback, history scroll, choice highlighting,
   skip/fast-forward on input)
        |
  DialogueSpeakerProfileSO (new ScriptableObject)
  (speaker name, portrait sprites per expression,
   default VO bank, text color, name plate color)
        |
  DialogueHistoryBuffer (managed ring buffer)
  (stores last N dialogue lines for player review,
   accessible via scroll or dedicated panel)
        |
  DialoguePrioritySystem (new ECS system)
  (priority levels: Ambient < Exploration < Story < Combat,
   higher priority interrupts lower, lower queues for later)
```

---

## Visual Graph Editor

### DialogueGraphEditorWindow

**File:** `Assets/Editor/DialogueGraph/DialogueGraphEditorWindow.cs`

- Extends `EditorWindow`, hosts `GraphView` child
- Opens via double-click on `DialogueTreeSO` asset or menu `DIG > Dialogue > Graph Editor`
- **Node Creation:** Right-click context menu to add Dialogue, Choice, Condition, Action, Random nodes
- **Edge Wiring:** Drag from output port to input port to set `NextNodeId` / `TrueNodeId` / `FalseNodeId`
- **Node Colors:** Dialogue (blue), Choice (green), Condition (orange), Action (purple), Random (yellow)
- **Minimap:** Bottom-right corner minimap for large trees
- **Search:** Quick-search by node text, speaker name, or node ID
- **Auto-Layout:** Dagre-style automatic layout algorithm (top-to-bottom or left-to-right)
- **Preview Playback:** Play button starts a simulated dialogue flow in the editor, stepping through nodes with condition evaluation
- **Undo/Redo:** Full integration with Unity's Undo system via `Undo.RecordObject`

### DialogueGraphSerializer

**File:** `Assets/Editor/DialogueGraph/DialogueGraphSerializer.cs`

- Reads `DialogueTreeSO.Nodes[]` and `DialogueTreeSO.NodeEditorPositions[]` into GraphView nodes
- On save: writes back to `DialogueTreeSO` arrays, marks asset dirty, triggers reimport
- Handles node ID generation (auto-increment from max existing ID)
- Migration: if `NodeEditorPositions` is empty/wrong length, auto-layouts existing nodes

### DialogueValidator

**File:** `Assets/Editor/DialogueGraph/DialogueValidator.cs`

- **Dead Ends:** Nodes with no outgoing connections (except terminal nodes)
- **Orphan Nodes:** Nodes not reachable from StartNodeId
- **Circular Loops:** Infinite loops with no exit condition
- **Empty Text:** Dialogue nodes with no text
- **Missing Localization:** Nodes with text not in localization table
- **Missing VO:** Nodes with speaker but no VO clip assigned
- Results displayed as warning/error badges on nodes + summary panel

---

## Runtime Enhancements

### DialogueSpeakerProfileSO

**File:** `Assets/Scripts/Dialogue/Definitions/DialogueSpeakerProfileSO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| SpeakerName | string | "" | Display name |
| SpeakerNameHash | int | 0 | Matches DialogueNode.SpeakerName hash |
| Portraits | SpeakerPortrait[] | empty | Expression → Sprite mapping |
| DefaultPortrait | Sprite | null | Fallback portrait |
| TextColor | Color | white | Text color for this speaker |
| NamePlateColor | Color | blue | Name plate accent color |
| VoiceBank | AudioClip[] | empty | Random mumble clips for typewriter (optional) |

### SpeakerPortrait

```csharp
[Serializable]
public class SpeakerPortrait
{
    public string Expression; // "neutral", "happy", "angry", "sad", "surprised"
    public Sprite Sprite;
}
```

### Typewriter Effect

- Character-by-character text reveal with configurable speed (chars/sec)
- Punctuation pauses (period = 0.3s, comma = 0.15s, configurable)
- Skip: any input advances to full text instantly
- Optional per-character mumble SFX from speaker's VoiceBank
- Rich text tag awareness (doesn't break mid-tag)

### Dialogue History

- Ring buffer of last 50 dialogue lines (configurable)
- Each entry: speaker name, text, timestamp, VO clip reference
- Scrollable panel toggled with dedicated key (configurable, default: L)
- Clears on dialogue end

### Priority & Interrupt System

```csharp
public enum DialoguePriority : byte
{
    Ambient = 0,      // Environmental barks, NPC idle chatter
    Exploration = 50, // Player-initiated NPC conversations
    Story = 100,      // Scripted story beats, cutscenes
    Combat = 150,     // Combat barks, callouts
    System = 200      // System messages, tutorial override
}
```

- Higher priority dialogue interrupts lower
- Interrupted dialogue can resume from last node or be discarded (per-tree config)
- Same priority: new dialogue queues behind current

---

## Enhanced DialogueTreeSO Fields

Additional fields added to existing `DialogueNode`:

| Field | Type | Purpose |
|-------|------|---------|
| Expression | string | Portrait expression key for this line |
| VoiceClip | AudioClip | Voice-over audio for this line |
| TypewriterSpeed | float | Override chars/sec (0 = use global default) |
| Priority | DialoguePriority | Priority level for this tree |
| InterruptBehavior | InterruptBehavior enum | Resume, Restart, Discard when interrupted |

---

## Editor Tooling

### DialogueWorkstationModule

**File:** `Assets/Editor/DialogueWorkstation/Modules/DialogueWorkstationModule.cs`

- **Tree Browser:** Lists all DialogueTreeSO assets with validation status badges
- **Speaker Registry:** Lists all DialogueSpeakerProfileSO with portrait preview
- **Statistics:** Total nodes, average tree depth, branch factor, localization coverage
- **Quick Edit:** Inline text editing for selected node without opening graph
- **Bark Manager:** Lists all BarkCollectionSO with play/preview buttons

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Editor/DialogueGraph/DialogueGraphEditorWindow.cs` | EditorWindow | ~400 |
| `Assets/Editor/DialogueGraph/Nodes/DialogueNodeView.cs` | GraphView node | ~120 |
| `Assets/Editor/DialogueGraph/Nodes/ChoiceNodeView.cs` | GraphView node | ~80 |
| `Assets/Editor/DialogueGraph/Nodes/ConditionNodeView.cs` | GraphView node | ~70 |
| `Assets/Editor/DialogueGraph/Nodes/ActionNodeView.cs` | GraphView node | ~60 |
| `Assets/Editor/DialogueGraph/Nodes/RandomNodeView.cs` | GraphView node | ~50 |
| `Assets/Editor/DialogueGraph/DialogueGraphSerializer.cs` | Serializer | ~150 |
| `Assets/Editor/DialogueGraph/DialogueValidator.cs` | Validator | ~120 |
| `Assets/Editor/DialogueGraph/DialogueAutoLayout.cs` | Layout algorithm | ~100 |
| `Assets/Scripts/Dialogue/Definitions/DialogueSpeakerProfileSO.cs` | ScriptableObject | ~45 |
| `Assets/Scripts/Dialogue/UI/DialogueTypewriter.cs` | Class | ~100 |
| `Assets/Scripts/Dialogue/UI/DialogueHistoryBuffer.cs` | Class | ~60 |
| `Assets/Scripts/Dialogue/Systems/DialoguePrioritySystem.cs` | ISystem | ~80 |
| `Assets/Editor/DialogueWorkstation/Modules/DialogueWorkstationModule.cs` | Editor | ~200 |

**Total estimated:** ~1,635 lines

---

## Performance Considerations

- Graph editor is editor-only — zero runtime cost
- `DialogueTypewriter` uses `StringBuilder` with pre-allocated capacity — no per-character allocation
- VO playback uses existing `AudioSourcePool` — no new AudioSource allocation
- `DialogueHistoryBuffer` is a fixed-size ring buffer — zero GC
- `DialoguePrioritySystem` is Burst-compatible (compares byte priority values)
- No modifications to existing BlobAsset traversal — all enhancements are in managed bridge/UI layer

---

## Testing Strategy

- Editor test: create DialogueTreeSO via graph editor → save → reload → verify nodes preserved
- Editor test: run validator on intentionally broken tree → verify all issues detected
- Unit test: typewriter reveals correct characters at configured speed
- Unit test: priority system correctly interrupts lower priority dialogue
- Unit test: history buffer stores and retrieves correct entries
- Integration test: initiate dialogue in-game → verify typewriter, portrait, VO all play correctly
- Integration test: interrupt exploration dialogue with combat bark → verify interrupt behavior
