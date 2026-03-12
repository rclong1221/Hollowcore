# EPIC 18.9: Console & Debug Command System — Setup Guide

## Prerequisites

- Unity 2022.3+ with Entities, NetCode, and Input System packages installed
- DIG project with existing ECS infrastructure

---

## 1. Enable the Console

The console is **conditionally compiled** — it produces zero code in release builds.

1. Open **Edit > Project Settings > Player > Other Settings > Scripting Define Symbols**
2. Add `DIG_DEV_CONSOLE` (semicolon-separated from any existing defines)
3. Click **Apply** — Unity will recompile

> **For release builds**: Remove `DIG_DEV_CONSOLE` from defines. Every console file is wrapped in `#if DIG_DEV_CONSOLE` and will be stripped entirely.

---

## 2. Scene Setup

Add a single persistent GameObject to your **boot scene** (or any scene that loads first):

1. Create an empty GameObject, name it `[DevConsole]`
2. Add these three components via the Inspector:
   - **DevConsoleService** — singleton service (auto-calls `DontDestroyOnLoad`)
   - **DevConsoleView** — the IMGUI console overlay
   - **StatOverlayView** — the FPS/memory/entity-count overlay

The GameObject will survive all subsequent scene loads automatically.

> If you already have a boot/persistent-objects pattern (like `[AudioService]`), place `[DevConsole]` alongside it in the same scene.

---

## 3. Runtime Controls

| Key | Action |
|-----|--------|
| `` ` `` (Backtick) | Toggle console overlay on/off |
| **F3** | Toggle stat overlay (FPS, frame time, entity count, memory) |
| **Enter** | Execute typed command, or accept highlighted autocomplete |
| **Tab** | Accept first autocomplete suggestion |
| **Up / Down** | Navigate command history, or scroll autocomplete |
| **Escape** | Dismiss autocomplete, or close console |

When the console is open, keyboard input is consumed and will **not** pass through to game systems.

---

## 4. Command Reference

### Player

| Command | Description | Usage | Notes |
|---------|-------------|-------|-------|
| `god` | Toggle invincibility | `god [on\|off]` | Writes `GodMode.Enabled` on player entity |
| `heal` | Heal to full (or specific amount) | `heal [amount]` | |
| `tp` | Teleport to world coordinates | `tp <x> <y> <z>` | Uses TeleportEvent if available |
| `speed` | Set movement speed multiplier | `speed <multiplier>` | `speed 1` restores defaults |
| `xp` | Grant XP to local player | `xp <amount>` | Uses XPGrantAPI |
| `give` | Give item *(stub)* | `give <itemId> [count]` | Not yet implemented |
| `noclip` | Toggle noclip *(stub)* | `noclip [on\|off]` | Not yet implemented |
| `level` | Set level *(stub)* | `level <level>` | Not yet implemented |

### World / Environment

| Command | Description | Usage | Notes |
|---------|-------------|-------|-------|
| `time` | Set or query time of day | `time [hours]` | 0–23.99, e.g. `time 14.5` = 2:30 PM |
| `weather` | Set weather type | `weather <type>` | Clear, PartlyCloudy, Cloudy, LightRain, HeavyRain, Thunderstorm, LightSnow, HeavySnow, Fog, Sandstorm |
| `fog` | Set fog density | `fog <0-1>` | |

### Network

| Command | Description | Usage |
|---------|-------------|-------|
| `netstat` | Show server/client world info, tick counts, connections | `netstat` |
| `ping` | Show ping *(stub)* | `ping` |
| `lag` | Simulate lag *(stub)* | `lag <ms>` |
| `kick` | Kick player *(stub)* | `kick <id>` |

### System / Diagnostics

| Command | Description | Usage |
|---------|-------------|-------|
| `fps` | Toggle the stat overlay | `fps [on\|off]` |
| `memory` | Show detailed memory breakdown | `memory` |
| `gc` | Force garbage collection | `gc` |
| `screenshot` | Save screenshot to persistent data | `screenshot [filename]` |
| `quit` | Quit application (stops play mode in editor) | `quit` |
| `timescale` | Set or query `Time.timeScale` | `timescale [value]` |

### ECS Introspection

| Command | Description | Usage | Notes |
|---------|-------------|-------|-------|
| `ecs.worlds` | List all ECS worlds with entity counts | `ecs.worlds` | |
| `ecs.count` | Count entities with a specific component | `ecs.count <ComponentName> [world]` | Case-insensitive name match |
| `ecs.systems` | List enabled systems in a world | `ecs.systems [world]` | Shows up to 50 |
| `ecs.inspect` | Show all components on an entity | `ecs.inspect <entityIndex> [world]` | May hitch on large worlds (>5K entities) |

### Utility

| Command | Description | Usage |
|---------|-------------|-------|
| `help` | List all commands, or show details for one | `help [command]` |
| `clear` | Clear console output log | `clear` |

### Spawn *(stubs — not yet implemented)*

| Command | Description | Usage |
|---------|-------------|-------|
| `spawn` | Spawn entity by prefab name | `spawn <prefabName> [count]` |
| `kill` | Kill targeted/nearby enemies | `kill [all\|radius]` |
| `despawn` | Remove all debug-spawned entities | `despawn` |

---

## 5. Adding Custom Commands

Any team member can add new commands. The system discovers them automatically via reflection at startup.

**Requirements:**
- Wrap in `#if DIG_DEV_CONSOLE`
- Method must be `static` with signature `void MethodName(ConCommandArgs args)`
- Add the `[ConCommand]` attribute with name, description, and optional usage/flags

**Example:**

```csharp
#if DIG_DEV_CONSOLE
using DIG.DebugConsole;

public static class MyGameplayCommands
{
    [ConCommand("setgold", "Set player gold amount", "setgold <amount>",
        ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
    public static void CmdSetGold(ConCommandArgs args)
    {
        if (args.Count < 1)
        {
            DevConsoleService.Instance.LogWarning("Usage: setgold <amount>");
            return;
        }

        int amount = args.GetInt(0, 0);
        // ... your game logic here ...
        DevConsoleService.Instance.Log($"Gold set to {amount}.");
    }
}
#endif
```

### Argument API

| Method | Returns | Example |
|--------|---------|---------|
| `args.GetString(0, "default")` | string | First positional arg |
| `args.GetInt(1, 10)` | int | Second positional arg, default 10 |
| `args.GetFloat(0, 1.0f)` | float | InvariantCulture parsing |
| `args.GetBool(0)` | bool | Accepts: on/off, true/false, yes/no, 1/0 |
| `args.GetEnum<WeatherType>(0)` | T | Case-insensitive enum parse |
| `args.HasFlag("verbose")` | bool | Checks for `--verbose` |
| `args.GetFlag("output", "log")` | string | Value of `--output <value>` |
| `args.Count` | int | Number of positional args |

### Command Flags

| Flag | Effect |
|------|--------|
| `RequiresPlayMode` | Command is rejected with a warning if not in play mode |
| `ServerOnly` | Indicates the command modifies server-authoritative state |
| `Hidden` | Command is excluded from the `help` listing |
| `ReadOnly` | Informational marker — no game state modifications |

Flags can be combined: `ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly`

### Output Methods

Use these inside your command handler to write to the console:

| Method | Color |
|--------|-------|
| `DevConsoleService.Instance.Log("message")` | White |
| `DevConsoleService.Instance.LogWarning("message")` | Yellow |
| `DevConsoleService.Instance.LogError("message")` | Red |

### ECS World Helpers

For commands that interact with ECS, these static helpers are available:

| Helper | Returns |
|--------|---------|
| `DevConsoleService.FindServerWorld()` | Server world, or null |
| `DevConsoleService.FindClientWorld()` | Client world, or null |
| `DevConsoleService.FindAuthoritativeWorld()` | Server world, falling back to DefaultGameObjectInjectionWorld |
| `DevConsoleService.FindLocalPlayer(world)` | Local player entity (PlayerTag + GhostOwnerIsLocal), or Entity.Null |

---

## 6. Editor Integration — Debug Workstation

The console adds a **Console** tab to the existing Debug Workstation window:

1. Open **DIG > Debug Workstation** from the menu bar
2. Click the **Console** tab (last item in the sidebar)

The tab provides three sub-views:

| Sub-tab | Description |
|---------|-------------|
| **Commands** | Browse all registered commands with descriptions, usage, and flags. Filter by name. Click **Run** to populate the execute bar. |
| **History** | View previously executed commands. Click **Re-run** to execute again. |
| **Output** | View the console output log. Click **Clear** to reset. |

A **Quick Execute** bar at the top lets you type and run commands directly from the editor during play mode.

> The Console tab only appears when `DIG_DEV_CONSOLE` is defined. It requires play mode to show command data.

---

## 7. Command History

- Command history persists between sessions in `Application.persistentDataPath/dev_console_history.txt`
- Up to 128 entries are saved
- Consecutive duplicate commands are deduplicated
- Use **Up/Down Arrow** in the console to navigate history

---

## 8. Stat Overlay Details

The stat overlay (toggled with **F3** or `fps` command) displays four metrics:

| Metric | Source | Update Rate |
|--------|--------|-------------|
| **FPS** | `1 / Time.unscaledDeltaTime` | 2 Hz |
| **Frame time** | `Time.unscaledDeltaTime * 1000` | 2 Hz |
| **Entity count** | `EntityManager.UniversalQuery.CalculateEntityCount()` | 2 Hz |
| **Memory** | `Profiler.GetTotalAllocatedMemoryLong()` + `GC.GetTotalMemory()` | 2 Hz |

FPS color coding: Green (55+), Yellow (30–54), Red (<30).

---

## 9. Verification Checklist

After setup, verify everything works:

1. [ ] `DIG_DEV_CONSOLE` added to **Player Settings > Scripting Define Symbols**
2. [ ] `[DevConsole]` GameObject exists in boot scene with all 3 components attached
3. [ ] Enter Play Mode — console log should print "Dev Console initialized. X commands registered."
4. [ ] Press `` ` `` — console overlay appears at top of screen
5. [ ] Type `help` + Enter — lists all registered commands
6. [ ] Type `god on` + Enter — god mode toggles (confirm in inspector or with damage test)
7. [ ] Type `tp 100 50 200` + Enter — player teleports to those coordinates
8. [ ] Type `time 6` + Enter — time of day changes to 6:00 AM
9. [ ] Type `weather Thunderstorm` + Enter — weather changes
10. [ ] Press **F3** — stat overlay appears in top-left corner
11. [ ] Press `` ` `` to close console — verify game input resumes (WASD, mouse, etc.)
12. [ ] Open **DIG > Debug Workstation > Console** tab — verify command browser works
13. [ ] Remove `DIG_DEV_CONSOLE` from defines — project compiles cleanly with zero console code

---

## 10. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Console doesn't open with backtick | Missing `DevConsoleView` component | Add `DevConsoleView` to `[DevConsole]` GameObject |
| "No local player found" on commands | Player entity doesn't have `PlayerTag` + `GhostOwnerIsLocal` | Ensure player prefab has both components; check you're running a listen server or client+server |
| Commands say "requires play mode" | Tried to run ECS commands outside play mode | Enter play mode first |
| Stat overlay shows 0 entities | No authoritative world found | Ensure server or default world is running |
| Console doesn't compile | `DIG_DEV_CONSOLE` not in defines | Add to Player Settings > Scripting Define Symbols |
| History not persisting | File write permission issue | Check `Application.persistentDataPath` is writable |
| `ecs.count` hitches on first use | Component type cache being built | First call scans assemblies; subsequent calls are instant |
