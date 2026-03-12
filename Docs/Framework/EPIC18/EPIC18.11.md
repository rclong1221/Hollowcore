# EPIC 18.11: Modding & Content Pipeline

**Status:** PLANNED
**Priority:** Low-Medium (Community longevity — massive engagement multiplier for shipped games)
**Dependencies:**
- Unity Addressables (`UnityEngine.AddressableAssets`, existing)
- `SurfaceMaterial` / `SurfaceMaterialRegistry` (existing — `Audio.Systems`, demonstrates SO-based content registration)
- `DialogueDatabaseSO` / `DialogueTreeSO` (existing — `DIG.Dialogue`, demonstrates scriptable content assets)
- `AchievementDatabaseSO` / `AchievementDefinitionSO` (existing — `DIG.Achievement`, demonstrates definition databases)
- `SpawnTableSO` (EPIC 18.7 — content-driven spawning)
- `AudioEventSO` (EPIC 18.8 — content-driven audio)
- `ItemDefinitionSO` (existing — DIG.Inventory, item definitions)
- ScriptableObject pipeline (existing — all config SOs across the project)

**Feature:** A secure, sandboxed modding framework that allows community creators to add new content (items, enemies, dialogue, maps, audio, textures) via mod packages loaded at runtime. Provides a Mod SDK with documentation generator, a runtime mod loader with dependency resolution and version checking, a sandboxed scripting environment (Lua or C# source generators) for gameplay logic mods, and editor tooling for mod packaging, validation, and testing.

---

## Codebase Audit Findings

### What Already Exists

| System | Status | Notes |
|--------|--------|-------|
| Addressables | Available | Unity's content delivery system — can load assets by address at runtime |
| ScriptableObject databases | Extensively used | `DialogueDatabaseSO`, `AchievementDatabaseSO`, `SurfaceMaterialRegistry` — pattern for registering content at runtime |
| Asset bundles | Unity built-in | Low-level asset loading available |
| `Resources.Load()` pattern | Used throughout | Many systems load config from Resources — could be extended to check mod folders |

### What's Missing

- **No mod loader** — no system to discover, validate, and load mod packages at runtime
- **No mod package format** — no defined structure for mod content (manifest, assets, scripts)
- **No sandboxed scripting** — no way for mods to add gameplay logic without full C# access (security risk)
- **No content registration API** — no way for mods to register new items, enemies, dialogue, etc. into existing databases
- **No dependency resolution** — no mod dependency graph, version checking, or conflict detection
- **No mod manager UI** — no in-game mod browser, enable/disable, load order management
- **No Mod SDK** — no tooling for mod creators to package and test their mods

---

## Problem

Games with modding support see dramatically longer engagement (Skyrim, Minecraft, Rimworld). DIG's ScriptableObject-based content system is naturally mod-friendly — new items, dialogue, audio events, and spawn tables are all data-driven. But there's no infrastructure to load this content from external sources at runtime, no security sandbox for logic mods, and no tools for mod creators. Building this now ensures the framework is designed for extensibility from the start.

---

## Architecture Overview

```
                    MOD CREATOR LAYER
  Mod SDK (Unity Editor Tools)
  (project template, content validators,
   packaging tool, local testing)
        |
  Mod Package (.digmod file)
  (zip containing manifest.json + AssetBundles
   + optional Lua scripts + metadata)
        |
                    RUNTIME LAYER
  ModLoader (MonoBehaviour singleton)
  (discovers mods in Mods/ folder,
   validates manifests, resolves dependencies,
   determines load order, loads AssetBundles)
        |
  ModRegistry
  (tracks loaded mods, provides API for
   querying mod status, enabling/disabling)
        |
  ContentInjector
  (registers mod assets into existing databases:
   new items → ItemDatabase, new dialogue →
   DialogueDatabase, new surfaces → SurfaceRegistry)
        |
  ScriptSandbox (optional)
  (Lua interpreter or Roslyn-compiled C# with
   restricted API surface — no IO, no reflection,
   no network, timeout enforcement)
        |
                    SECURITY LAYER
  ModValidator
  (manifest schema validation, asset type whitelist,
   script API surface check, hash verification,
   file size limits)
        |
                    UI & EDITOR
  ModManagerView (in-game UI)
  (mod browser, enable/disable, load order,
   conflict warnings, mod details)
        |
  ModSDKWorkstationModule (Editor)
  (mod project wizard, asset validator,
   package builder, test loader)
```

---

## Mod Package Format (.digmod)

```
my-mod.digmod (zip archive):
├── manifest.json           // Mod metadata, dependencies, content declarations
├── content/
│   ├── items/              // Item definition JSON files
│   ├── dialogue/           // Dialogue tree JSON files
│   ├── audio/              // AudioClip assets (ogg/wav)
│   ├── textures/           // Texture assets (png/jpg)
│   ├── prefabs/            // AssetBundle with prefabs
│   └── maps/               // AssetBundle with scenes
├── scripts/                // Optional Lua scripts
│   ├── init.lua            // Entry point
│   └── abilities/          // Custom ability scripts
├── localization/           // Localization CSV/JSON files
├── preview.png             // Mod preview image
└── README.md               // Mod description
```

### manifest.json

```json
{
  "id": "com.modder.awesome-weapons",
  "name": "Awesome Weapons Pack",
  "version": "1.2.0",
  "author": "ModderName",
  "description": "Adds 10 new weapons with custom VFX",
  "gameVersionMin": "0.9.0",
  "gameVersionMax": "1.x",
  "dependencies": [
    { "id": "com.modder.base-textures", "version": ">=1.0.0" }
  ],
  "conflicts": ["com.other.weapon-overhaul"],
  "content": {
    "items": ["content/items/*.json"],
    "audio": ["content/audio/*.ogg"],
    "prefabs": ["content/prefabs/weapons.bundle"]
  },
  "scripts": {
    "entryPoint": "scripts/init.lua",
    "apiLevel": 1
  },
  "permissions": ["items.add", "audio.add", "vfx.add"],
  "checksum": "sha256:abc123..."
}
```

---

## Core Systems

### ModLoader

**File:** `Assets/Scripts/Modding/ModLoader.cs`

- MonoBehaviour singleton, `DontDestroyOnLoad`
- On startup: scans `Application.persistentDataPath/Mods/` for `.digmod` files
- Validates each manifest against schema
- Builds dependency graph, topological sort for load order
- Detects conflicts and missing dependencies
- Loads AssetBundles from mod packages
- API:
  - `void DiscoverMods()` — rescan mod folder
  - `bool LoadMod(string modId)` — load a specific mod
  - `void UnloadMod(string modId)` — unload mod and its assets
  - `void SetLoadOrder(string[] modIds)` — set mod priority order
  - `ModInfo[] GetAvailableMods()` — list all discovered mods
  - `ModInfo[] GetLoadedMods()` — list currently loaded mods
  - `ModConflict[] GetConflicts()` — list detected conflicts

### ContentInjector

**File:** `Assets/Scripts/Modding/ContentInjector.cs`

- Registers mod content into existing game databases:
  - Items: creates `ItemDefinitionSO` from JSON → adds to ItemDatabase
  - Dialogue: creates `DialogueTreeSO` from JSON → adds to DialogueDatabaseSO
  - Audio: loads AudioClip from bundle → creates AudioEventSO → registers
  - Surfaces: creates SurfaceMaterial from JSON → adds to SurfaceMaterialRegistry
  - Spawn Tables: creates SpawnTableSO from JSON → registers with SpawnManager
- Content is tagged with source mod ID for clean unloading
- Validates content against game schema before injection

### ScriptSandbox

**File:** `Assets/Scripts/Modding/Scripting/ScriptSandbox.cs`

- Embeds MoonSharp (Lua interpreter for .NET) or similar
- Restricted API surface:
  - `game.spawn(prefabName, position)` — spawn entities
  - `game.damage(target, amount)` — deal damage
  - `game.notify(message)` — show notification
  - `game.getPlayerPosition()` — read player pos
  - `game.onEvent(eventName, callback)` — subscribe to game events
  - `game.timer(seconds, callback)` — delayed execution
- **NOT** exposed: file IO, network, reflection, raw ECS access, System.Diagnostics
- Execution timeout: 10ms per frame per mod (configurable)
- Error isolation: script errors don't crash the game

### ModValidator

**File:** `Assets/Scripts/Modding/Security/ModValidator.cs`

- Manifest schema validation (required fields, version format)
- Asset type whitelist (only allowed asset types: AudioClip, Texture2D, Mesh, Material, Prefab, ScriptableObject)
- File size limits (per-file and total)
- Checksum verification (integrity check against manifest hash)
- Script API surface validation (no forbidden API calls)
- Dependency version range checking (SemVer)

---

## ScriptableObjects

### ModRegistrySO

**File:** `Assets/Scripts/Modding/Config/ModRegistrySO.cs`

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| EnableModding | bool | false | Master toggle |
| ModsPath | string | "Mods/" | Subdirectory in persistentDataPath |
| MaxModSize | long | 104857600 | 100MB max per mod |
| MaxTotalModSize | long | 1073741824 | 1GB total |
| AllowScripts | bool | false | Enable Lua scripting |
| ScriptTimeoutMs | int | 10 | Per-frame script execution limit |
| AutoLoadMods | bool | true | Load enabled mods on startup |
| ContentWhitelist | string[] | defaults | Allowed content types |

---

## Mod Manager UI

### ModManagerView

**File:** `Assets/Scripts/Modding/UI/ModManagerView.cs`

- In-game UI accessible from main menu or settings
- **Mod Browser:** Lists installed mods with preview image, name, author, version, status
- **Enable/Disable:** Toggle mods on/off with immediate feedback
- **Load Order:** Drag-and-drop reorder for load priority
- **Conflict Display:** Red warnings for conflicting mods
- **Dependency Display:** Shows required mods and their status
- **Mod Details:** Full description, changelog, permissions, file size

---

## Editor Tooling

### ModSDKWorkstationModule

**File:** `Assets/Editor/ModWorkstation/Modules/ModSDKWorkstationModule.cs`

- **Project Wizard:** Create new mod project with folder structure and manifest template
- **Content Validator:** Validate mod content against game schemas
- **Package Builder:** Build .digmod file from mod project
- **Test Loader:** Load mod in editor for testing without packaging
- **Documentation Generator:** Auto-generate mod API docs from game ScriptableObject schemas
- **Manifest Editor:** GUI editor for manifest.json

---

## File Manifest

| File | Type | Lines (est.) |
|------|------|-------------|
| `Assets/Scripts/Modding/ModLoader.cs` | MonoBehaviour | ~300 |
| `Assets/Scripts/Modding/ModRegistry.cs` | Class | ~100 |
| `Assets/Scripts/Modding/ContentInjector.cs` | Class | ~250 |
| `Assets/Scripts/Modding/Security/ModValidator.cs` | Class | ~150 |
| `Assets/Scripts/Modding/Scripting/ScriptSandbox.cs` | Class | ~200 |
| `Assets/Scripts/Modding/Scripting/ModAPI.cs` | Class | ~150 |
| `Assets/Scripts/Modding/Data/ModManifest.cs` | Class | ~60 |
| `Assets/Scripts/Modding/Data/ModInfo.cs` | Class | ~30 |
| `Assets/Scripts/Modding/Data/ModConflict.cs` | Class | ~20 |
| `Assets/Scripts/Modding/Config/ModRegistrySO.cs` | ScriptableObject | ~35 |
| `Assets/Scripts/Modding/UI/ModManagerView.cs` | UIView | ~200 |
| `Assets/Scripts/Modding/UI/ModManagerViewModel.cs` | ViewModel | ~100 |
| `Assets/Editor/ModWorkstation/Modules/ModSDKWorkstationModule.cs` | Editor | ~250 |

**Total estimated:** ~1,845 lines

---

## Performance Considerations

- Mod AssetBundles are loaded asynchronously — no main thread stall
- Content injection happens at startup only — zero per-frame cost
- Script sandbox execution is time-bounded (10ms/frame cap) — prevents mod scripts from causing frame drops
- Mod assets share memory pools with base game (AudioSourcePool, texture cache)
- Unloaded mods fully release AssetBundle memory via `AssetBundle.Unload(true)`
- Content lookup after injection uses same Dictionary-based registries as base game — O(1) access

---

## Testing Strategy

- Unit test manifest validation: valid/invalid manifests → verify accept/reject
- Unit test dependency resolution: diamond dependencies, circular dependencies, version conflicts
- Unit test content injection: inject item JSON → verify appears in ItemDatabase
- Unit test script sandbox: execute Lua → verify API calls work, forbidden calls rejected, timeout enforced
- Integration test: package mod → load at runtime → verify content available in-game
- Integration test: enable/disable mod → verify content added/removed
- Integration test: conflicting mods → verify conflict UI displayed
- Editor test: ModSDKWorkstationModule packages valid .digmod file
