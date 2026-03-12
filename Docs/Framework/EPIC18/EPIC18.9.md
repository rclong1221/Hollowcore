# EPIC 18.9: Console & Debug Command System

**Status:** PLANNED
**Priority:** Medium (Developer productivity — accelerates testing and debugging)
**Dependencies:**
- `DebugLog` (existing — `DIG.Core`, `Assets/Scripts/Core/DebugLog.cs`, centralized logging)
- `InputSchemeDebugOverlay` / `InputSchemeDebugTester` (existing — `DIG.Core.Input.Debug`, input debug UI)
- `AudioQAController` (existing — `Audio.Systems`, `Assets/Scripts/Audio/AudioQAController.cs`, audio testing tools)
- `FootstepTester` (existing — `Audio.Systems`, `Assets/Scripts/Audio/FootstepTester.cs`)
- `TargetSpawnerModule` / `TestingSandboxModule` (existing — `Assets/Editor/DebugWorkstation/Modules/`, editor-only testing tools)
- `ServiceLocator` (existing — `DIG.Core.Services`, `Assets/Scripts/Core/Services/ServiceLocator.cs`)
- Unity Input System (`UnityEngine.InputSystem`)

**Feature:** A runtime developer console with a command registration system, auto-complete, argument parsing, persistent command history, and a plugin architecture where any system can register its own commands via attributes. Includes built-in commands for common debug tasks (god mode, teleport, spawn entity, set time, toggle systems, query ECS state) and a visual overlay for real-time stat monitoring (FPS, memory, network, entity count). Fully strippable from release builds via conditional compilation.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `DebugLog` | `Assets/Scripts/Core/DebugLog.cs` | Basic | Centralized logging utility |
| `InputSchemeDebugOverlay` | `Assets/Scripts/Core/Input/Debug/InputSchemeDebugOverlay.cs` | Implemented | Input-specific debug overlay |
| `AudioQAController` | `Assets/Scripts/Audio/AudioQAController.cs` | Implemented | Audio testing/QA tools |
| `TargetSpawnerModule` | `Assets/Editor/DebugWorkstation/Modules/TargetSpawnerModule.cs` | Editor-only | Target spawning in editor |
| `TestingSandboxModule` | `Assets/Editor/DebugWorkstation/Modules/TestingSandboxModule.cs` | Editor-only | General testing sandbox |

### What's Missing

- **No runtime console** — all debug tools are editor-only (Workstation modules) or hardcoded overlays; no in-game console for runtime debugging
- **No command system** — no way to type commands at runtime (e.g., `/god`, `/spawn Skeleton 5`, `/tp 100 50 200`)
- **No auto-complete** — no tab-completion for commands and arguments
- **No persistent history** — no command history across sessions
- **No plugin architecture** — debug commands would need to be centralized in one file instead of registered by each subsystem
- **No stat overlay** — no always-visible FPS/memory/entity count overlay (separate from editor profiler)
- **No conditional stripping** — debug commands would ship in release builds without a stripping mechanism

---

## Problem

During development and QA, testers need to quickly manipulate game state: spawn enemies, teleport, toggle invincibility, adjust time of day, trigger quests, grant items. Currently, this requires editor Workstation modules (unavailable at runtime) or custom debug scripts. In live multiplayer testing, there's no way to execute debug commands on a running client/server without pausing and modifying values in the inspector. A runtime console dramatically accelerates iteration and bug reproduction.

---

## Architecture Overview

```
                    RUNTIME LAYER
  DevConsoleView (UI Toolkit overlay)
  (input field, scrollable output log,
   auto-complete dropdown, stat bar,
   resizable, draggable, dockable)
        |
  DevConsoleService (MonoBehaviour singleton)
  (command registry, parser, executor,
   output buffer, history manager)
        |
  Command Registry
  (Dictionary<string, ConsoleCommand>,
   populated via [ConCommand] attribute scan
   and manual registration)
        |
  Built-in Command Modules
        |
  ┌─────┼─────────┬──────────┬───────────┬────────────┐
  |     |         |          |           |            |
Player  World   Spawn     Network    System      ECS
Cmds    Cmds    Cmds      Cmds       Cmds        Cmds
(god,   (time,  (spawn,   (netstat,  (toggle,    (query,
 noclip, weather, kill,    lag sim,   fps,        count,
 heal,   fog)    clear)    kick)      memory)     inspect)
  tp)
        |
  #if DIG_DEV_CONSOLE (conditional compilation)
  (entire console system stripped from release builds)
```

---

## Core Types

### ConCommand Attribute

**File:** `Assets/Scripts/Debug/Console/ConCommandAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[Conditional("DIG_DEV_CONSOLE")]
public class ConCommandAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public string Usage { get; }
    public ConCommandFlags Flags { get; }

    public ConCommandAttribute(string name, string description = "", string usage = "", ConCommandFlags flags = ConCommandFlags.None) { ... }
}

[Flags]
public enum ConCommandFlags
{
    None = 0,
    ServerOnly = 1,       // Only executable on server
    Cheat = 2,            // Requires cheats enabled
    Hidden = 4,           // Not shown in help/autocomplete
    Persistent = 8        // Persists across scene loads
}
```

### Usage Example

```csharp
public static class PlayerCommands
{
    [ConCommand("god", "Toggle god mode", "god [on|off]", ConCommandFlags.Cheat)]
    public static void GodMode(ConCommandArgs args)
    {
        bool enabled = args.GetBool(0, true); // default true if no arg
        // Toggle invincibility...
        DevConsole.Log($"God mode: {(enabled ? "ON" : "OFF")}");
    }

    [ConCommand("tp", "Teleport to position", "tp <x> <y> <z>")]
    public static void Teleport(ConCommandArgs args)
    {
        float x = args.GetFloat(0);
        float y = args.GetFloat(1);
        float z = args.GetFloat(2);
        // Teleport player...
        DevConsole.Log($"Teleported to ({x}, {y}, {z})");
    }
}
```

### ConCommandArgs

```csharp
public class ConCommandArgs
{
    public string RawInput { get; }
    public int Count { get; }
    public string GetString(int index, string defaultValue = "");
    public int GetInt(int index, int defaultValue = 0);
    public float GetFloat(int index, float defaultValue = 0);
    public bool GetBool(int index, bool defaultValue = false);
    public Entity GetEntity(int index); // Parse entity index
    public T GetEnum<T>(int index, T defaultValue = default) where T : struct, Enum;
}
```

---

## DevConsoleService

**File:** `Assets/Scripts/Debug/Console/DevConsoleService.cs`

- MonoBehaviour singleton, `DontDestroyOnLoad`, `[DefaultExecutionOrder(-400)]`
- `#if DIG_DEV_CONSOLE` wrapping entire class
- On Awake: scans all assemblies for `[ConCommand]` methods via reflection (cached, one-time)
- API:
  - `void Execute(string input)` — parse and execute command
  - `void RegisterCommand(string name, Action<ConCommandArgs> handler, string desc, string usage)` — manual registration
  - `void UnregisterCommand(string name)` — remove command
  - `void Log(string message)` — output to console
  - `void LogWarning(string message)` — warning output
  - `void LogError(string message)` — error output
  - `string[] GetCompletions(string partial)` — auto-complete suggestions
  - `string[] GetHistory(int count)` — recent commands
  - `bool CheatsEnabled` — toggle for cheat commands

### Command Parsing

```
Input: "spawn Skeleton 5 --level 10"
Parsed: command="spawn", args=["Skeleton", "5"], flags={level: "10"}
```

- Supports positional arguments and `--flag value` named arguments
- Quoted strings for args with spaces: `say "Hello World"`

### Output Buffer

- Ring buffer of last 500 log entries (configurable)
- Each entry: timestamp, severity (Info/Warning/Error), message, source command
- Serialized to `Application.persistentDataPath/console_history.txt` on flush

---

## Built-in Command Modules

### PlayerCommands

| Command | Description | Usage |
|---------|-------------|-------|
| `god` | Toggle invincibility | `god [on\|off]` |
| `noclip` | Toggle no-clip flying | `noclip [on\|off]` |
| `heal` | Heal to full HP | `heal [amount]` |
| `tp` | Teleport to position | `tp <x> <y> <z>` |
| `speed` | Set movement speed multiplier | `speed <multiplier>` |
| `give` | Give item by ID | `give <itemId> [count]` |
| `xp` | Grant XP | `xp <amount>` |
| `level` | Set player level | `level <level>` |

### WorldCommands

| Command | Description | Usage |
|---------|-------------|-------|
| `time` | Set time of day | `time <0-24>` |
| `weather` | Set weather | `weather <clear\|rain\|storm\|snow>` |
| `fog` | Toggle fog | `fog [on\|off] [density]` |

### SpawnCommands

| Command | Description | Usage |
|---------|-------------|-------|
| `spawn` | Spawn entity | `spawn <prefabName> [count] [--level N]` |
| `kill` | Kill all enemies | `kill [type]` |
| `clear` | Despawn all spawned entities | `clear` |

### NetworkCommands

| Command | Description | Usage |
|---------|-------------|-------|
| `netstat` | Show network statistics | `netstat` |
| `ping` | Show latency to server | `ping` |
| `lag` | Simulate network latency | `lag <ms>` |
| `kick` | Kick player by ID | `kick <playerId>` |

### SystemCommands

| Command | Description | Usage |
|---------|-------------|-------|
| `fps` | Toggle FPS overlay | `fps [on\|off]` |
| `memory` | Show memory usage | `memory` |
| `gc` | Force garbage collection | `gc` |
| `screenshot` | Capture screenshot | `screenshot [filename]` |
| `quit` | Quit application | `quit` |

### ECSCommands

| Command | Description | Usage |
|---------|-------------|-------|
| `ecs.count` | Count entities with component | `ecs.count <ComponentName>` |
| `ecs.inspect` | Inspect entity components | `ecs.inspect <entityIndex>` |
| `ecs.worlds` | List active ECS worlds | `ecs.worlds` |
| `ecs.systems` | List running systems | `ecs.systems [world]` |

---

## DevConsoleView

**File:** `Assets/Scripts/Debug/Console/UI/DevConsoleView.cs`

- UI Toolkit overlay, toggled by backtick (`` ` ``) key (configurable)
- **Input Field:** Single-line text input with caret
- **Auto-Complete Dropdown:** Shows matching commands as user types, navigable with arrow keys, Tab to accept
- **Output Log:** Scrollable log with color-coded severity (white=info, yellow=warn, red=error)
- **Stat Bar:** Always-visible bottom bar showing FPS, entity count, memory, ping (when console is closed, can be toggled independently)
- **Resize/Drag:** Draggable title bar, resizable from edges
- **History Navigation:** Up/Down arrows navigate command history

---

## Stat Overlay

### StatOverlayView

**File:** `Assets/Scripts/Debug/Console/UI/StatOverlayView.cs`

- Compact always-on overlay (independent of console open/close)
- Shows: FPS (color-coded: green >60, yellow >30, red <30), frame time, entity count, memory (managed + native), ping
- Toggle with `fps` command or F3 key
- Positioned top-left by default, configurable
- Updates every 0.5 seconds (not every frame) to minimize overhead

---

## Conditional Compilation

All console code is wrapped in `#if DIG_DEV_CONSOLE`:

- Define `DIG_DEV_CONSOLE` in development builds, remove for release
- `[Conditional("DIG_DEV_CONSOLE")]` on attribute ensures no attribute metadata in release
- Console-related MonoBehaviours self-destroy if `DIG_DEV_CONSOLE` not defined
- Zero code, zero memory, zero performance cost in release builds

---

## Editor Tooling

### ConsoleWorkstationModule

**File:** `Assets/Editor/DebugWorkstation/Modules/ConsoleWorkstationModule.cs`

- **Command Registry Browser:** Lists all registered commands with descriptions, usage, flags
- **Quick Execute:** Run console commands from editor inspector
- **History Viewer:** Browse persistent command history
- **Custom Command Generator:** Wizard to generate new command stub code

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Debug/Console/DevConsoleService.cs` | MonoBehaviour | ~250 |
| `Assets/Scripts/Debug/Console/ConCommandAttribute.cs` | Attribute | ~30 |
| `Assets/Scripts/Debug/Console/ConCommandArgs.cs` | Class | ~60 |
| `Assets/Scripts/Debug/Console/CommandParser.cs` | Class | ~80 |
| `Assets/Scripts/Debug/Console/CommandHistory.cs` | Class | ~50 |
| `Assets/Scripts/Debug/Console/UI/DevConsoleView.cs` | UIView | ~200 |
| `Assets/Scripts/Debug/Console/UI/StatOverlayView.cs` | UIView | ~80 |
| `Assets/Scripts/Debug/Console/UI/AutoCompleteDropdown.cs` | Class | ~60 |
| `Assets/Scripts/Debug/Console/Commands/PlayerCommands.cs` | Static | ~120 |
| `Assets/Scripts/Debug/Console/Commands/WorldCommands.cs` | Static | ~60 |
| `Assets/Scripts/Debug/Console/Commands/SpawnCommands.cs` | Static | ~80 |
| `Assets/Scripts/Debug/Console/Commands/NetworkCommands.cs` | Static | ~60 |
| `Assets/Scripts/Debug/Console/Commands/SystemCommands.cs` | Static | ~80 |
| `Assets/Scripts/Debug/Console/Commands/ECSCommands.cs` | Static | ~100 |
| `Assets/Editor/DebugWorkstation/Modules/ConsoleWorkstationModule.cs` | Editor | ~150 |

**Total estimated:** ~1,460 lines

---

## Performance Considerations

- Console is hidden by default — zero rendering cost when not visible
- Attribute scanning happens once at startup (cached in Dictionary)
- Output ring buffer is fixed-size — zero GC
- Stat overlay updates at 2Hz (every 0.5s), not per-frame
- All console code stripped from release builds via `#if DIG_DEV_CONSOLE`
- Auto-complete uses prefix tree (trie) for O(prefix length) completion

---

## Testing Strategy

- Unit test command parsing: positional args, named flags, quoted strings
- Unit test auto-complete: partial input → verify correct suggestions
- Unit test history: execute commands → navigate history → verify order
- Unit test conditional compilation: build without DIG_DEV_CONSOLE → verify zero console code
- Integration test: execute `god` → verify player becomes invincible
- Integration test: execute `spawn Skeleton 5` → verify 5 skeletons appear
- Integration test: execute `tp 100 50 200` → verify player teleported
- Editor test: ConsoleWorkstationModule lists all registered commands
