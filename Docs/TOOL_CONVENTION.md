# DIG Editor Tools Convention

This document defines the organization and naming conventions for Unity Editor tools in the DIG project.

---

## Tool Categories

### 1. Universal Tools (Permanent)
**Purpose:** Tools designers/developers use repeatedly in normal workflows.

**Menu Location:** `DIG/<Category>/<ToolName>`
- `DIG/Debug/Equipment System Debugger`
- `DIG/Debug/Transition Inspector`

**File Location:** `Assets/Scripts/<Module>/Editor/`

**Naming:** Descriptive of function
- `EquipmentSystemDebuggerWindow.cs`
- `TransitionInspector.cs`

---

### 2. Bootstrap/Setup Tools (One-Time)
**Purpose:** Tools to initialize systems or create default assets. Run once during setup.

**Menu Location:** `DIG/Setup/<ToolName>`
- `DIG/Setup/Create Default Equipment Assets`

**File Location:** `Assets/Scripts/<Module>/Editor/Setup/`

**Naming:** Prefixed with `Setup_` or descriptive setup name
- `Setup_EquipmentDefaults.cs`
- `EquipmentDefinitionCreator.cs` (current - should be moved)

**Deprecation:** Safe to remove once assets exist and system is stable.

---

### 3. Migration Tools (One-Time, Versioned)
**Purpose:** Tools to migrate data/prefabs from old format to new format.

**Menu Location:** `DIG/Migration/<VersionOrEpic>/<ToolName>`
- `DIG/Migration/14.5/Migrate Weapon Prefabs`
- `DIG/Migration/14.5/Convert AnimationType to Category`

**File Location:** `Assets/Scripts/<Module>/Editor/Migration/`

**Naming:** Prefixed with `Migration_` and version
- `Migration_14_5_WeaponPrefabs.cs`
- `Migration_14_5_CategoryConversion.cs`

**Deprecation:** Safe to remove after all content is migrated and validated.

---

## File Header Convention

All one-time tools should include this header:

```csharp
/// <summary>
/// [MIGRATION] or [SETUP] Tool - EPIC XX.X
/// 
/// Purpose: [What this tool does]
/// When to use: [One-time during setup / When migrating from vX to vY]
/// Safe to remove: [After condition is met]
/// </summary>
```

---

## Current Tool Inventory

| Tool | Type | Location | Status |
|------|------|----------|--------|
| Equipment System Debugger | Universal | `DIG/Debug/` | ✅ Keep |
| Transition Inspector | Universal | `DIG/Debug/` | ✅ Keep |
| Animator Dumper | Universal | `DIG/Debug/` | ✅ Keep |
| Create Default Assets | Setup | `DIG/Equipment/` | ⚠️ Move to `DIG/Setup/` |

---

## Refactoring Actions

1. **Move** `EquipmentDefinitionCreator.cs` to `Editor/Setup/`
2. **Rename** menu from `DIG/Equipment/Create Default Assets/` to `DIG/Setup/Equipment Defaults/`
3. **Add** header comments to one-time tools
4. **Create** `Editor/Migration/` folder for future migration scripts
