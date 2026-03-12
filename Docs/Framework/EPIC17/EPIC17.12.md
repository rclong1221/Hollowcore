# EPIC 17.12: Localization Framework

**Status:** PLANNED
**Priority:** Medium (Infrastructure / Quality of Life)
**Dependencies:**
- `DialogueLocalization` static class (existing -- `Assets/Scripts/Dialogue/Bridges/DialogueLocalization.cs`, passthrough stub returning raw key)
- `DialogueNode.SpeakerName` / `DialogueNode.Text` string fields (existing -- `Assets/Scripts/Dialogue/Definitions/DialogueStructs.cs`, raw English strings)
- `DialogueChoice.Text` string field (existing -- `Assets/Scripts/Dialogue/Definitions/DialogueStructs.cs`)
- `BarkLine.Text` string field (existing -- `Assets/Scripts/Dialogue/Definitions/DialogueStructs.cs`)
- `DialogueUIBridgeSystem` / `DialogueUIRegistry` (existing -- `Assets/Scripts/Dialogue/Bridges/`, EPIC 16.16, managed bridge pattern)
- `CombatUIBridgeSystem` / `CombatUIRegistry` (existing -- `Assets/Scripts/Combat/UI/CombatUIBridgeSystem.cs`, `CombatUIRegistry.cs`, static registry + provider pattern)
- `FloatingTextManager` (existing -- `Assets/Scripts/Combat/UI/FloatingText/FloatingTextManager.cs`, hardcoded English strings)
- `EnemyHealthBar` (existing -- `Assets/Scripts/Combat/UI/WorldSpace/EnemyHealthBar.cs`, hardcoded label strings)
- `ItemEntrySO.DisplayName` / `ItemEntrySO.Description` string fields (existing -- `Assets/Scripts/Items/Definitions/ItemEntrySO.cs`, EPIC 16.6)
- `QuestDefinitionSO.DisplayName` / `QuestDefinitionSO.Description` / `ObjectiveDefinition.Description` string fields (existing -- `Assets/Scripts/Quest/Definitions/QuestDefinitionSO.cs`, EPIC 16.12)
- `RecipeDefinitionSO.DisplayName` / `RecipeDefinitionSO.Description` string fields (existing -- `Assets/Scripts/Crafting/Definitions/RecipeDefinitionSO.cs`, EPIC 16.13)
- `ProgressionUIBridgeSystem` / `ProgressionUIRegistry` (existing -- `Assets/Scripts/Progression/UI/`, EPIC 16.14, level-up and XP text)
- `QuestUIBridgeSystem` / `QuestUIRegistry` (existing -- `Assets/Scripts/Quest/UI/`, EPIC 16.12, quest tracker text)
- `SaveUIBridgeSystem` / `SaveUIRegistry` (existing -- `Assets/Scripts/Persistence/Bridges/`, EPIC 16.15, save/load notification text)
- TextMeshPro (existing -- project dependency, TMP_FontAsset used across all UI)

**Feature:** A hybrid localization framework providing runtime string resolution for all game text (dialogue, items, quests, crafting, combat UI, system messages), locale-aware font switching for CJK/RTL scripts, pluralization rules per locale, formatted string support with positional and named arguments, and designer-friendly editor tooling for string table management, coverage analysis, and pseudo-localization testing. Uses custom ScriptableObject-based string tables for game content (fast iteration) with optional Unity Localization package integration for platform-level strings. Zero player entity archetype impact -- locale is a client-only singleton.

---

## Codebase Audit Findings

### What Already Exists

| System | File | Status | Notes |
|--------|------|--------|-------|
| `DialogueLocalization.Resolve()` | `DialogueLocalization.cs` | Stub (passthrough) | Returns input string unchanged. `// TODO: Unity Localization package integration` comment |
| Dialogue string keys | `DialogueStructs.cs` | Raw English strings | `SpeakerName`, `Text`, `DialogueChoice.Text`, `BarkLine.Text` -- all plain `string` fields on SOs |
| Combat UI text | `Assets/Scripts/Combat/UI/` | Hardcoded English | Damage numbers, kill feed entries, health bar labels, combo counter, status effect names |
| Item text | `ItemEntrySO.cs` | Raw English strings | `DisplayName`, `Description` -- string fields on ScriptableObjects |
| Quest text | `QuestDefinitionSO.cs` | Raw English strings | `DisplayName`, `Description`, `ObjectiveDefinition.Description` |
| Crafting text | `RecipeDefinitionSO.cs` | Raw English strings | `DisplayName`, `Description` |
| Progression UI text | `Assets/Scripts/Progression/UI/` | Hardcoded English | "LEVEL UP!", XP bar labels, stat names |
| Save UI text | `Assets/Scripts/Persistence/Bridges/` | Hardcoded English | Save/load notification strings |
| TextMeshPro fonts | Project-wide | Single font set | No per-locale font switching |
| Unity Localization package | N/A | NOT installed | Not in project manifest |
| `CombatUIRegistry` pattern | `CombatUIRegistry.cs` | Fully implemented | Static provider registry -- reference pattern for `LocalizationUIRegistry` |

### What's Missing

- **No localization framework** -- `DialogueLocalization.Resolve()` is a passthrough that returns the input key unchanged
- **No string table management** -- all text is inline English strings on ScriptableObjects and MonoBehaviours
- **No locale detection or switching** -- no mechanism to detect system locale or persist player preference
- **No font asset switching** -- no per-locale TMP_FontAsset mapping for CJK (Chinese/Japanese/Korean) or RTL (Arabic/Hebrew) scripts
- **No pluralization rules** -- no language-aware singular/plural/dual/few/many/other forms
- **No formatted string support** -- no `{0}` positional or `{PlayerName}` named argument substitution with locale-aware number/date formatting
- **No localization workflow for designers** -- no string table editor, no missing key detection, no CSV/XLIFF export for translators
- **No pseudo-localization** -- no testing mode to catch truncation, concatenation, and hardcoded string bugs

---

## Problem

DIG has six content systems (Dialogue, Items, Quests, Crafting, Progression, Combat UI) that all embed English text directly in ScriptableObjects and MonoBehaviours. `DialogueLocalization.Resolve()` was designed as the integration point for future localization but currently returns its input unchanged. There is no string table, no locale concept, no way to switch languages.

| What Exists (Functional) | What's Missing |
|--------------------------|----------------|
| `DialogueLocalization.Resolve(key)` entry point | No actual localization -- returns key unchanged |
| String fields on ItemEntrySO, QuestDefinitionSO, RecipeDefinitionSO | No key-based lookup, no per-locale variants |
| TextMeshPro across all UI | No font asset switching for CJK/RTL |
| CombatUIRegistry static pattern | No equivalent for localization UI refresh |
| PlayerPrefs available on all platforms | No locale persistence mechanism |
| Unity Localization package exists | Not installed, not integrated |

**The gap:** A Japanese-speaking player sees only English text everywhere. Translators have no workflow -- there are no string tables to translate, no export format, no coverage metrics. Designers embedding text directly in SOs means any localization effort requires touching hundreds of asset files. The `DialogueLocalization.Resolve()` stub was placed specifically to avoid this -- but it was never connected to a real backend.

---

## Architecture Overview

```
                    DESIGNER DATA LAYER
  LocaleDefinition          StringTableSO              FontMappingSO
  (locale code, display     (Dictionary<key, string>    (locale -> TMP_FontAsset
   name, font ref,           per locale, fallback        per text style: Body,
   text direction,            chain support)              Header, Tooltip, Combat)
   plural rule set)
         |                        |                           |
         └──── LocalizationDatabase SO (root registry) ───────┘
               (List<StringTableSO>, List<LocaleDefinition>,
                List<FontMappingSO>, Resources/LocalizationDatabase)
                              |
               LocalizationBootstrapSystem (InitializationSystemGroup)
               +-- Resources.Load LocalizationDatabase
               +-- creates LocaleConfig ECS singleton
               +-- calls LocalizationManager.Initialize()
                              |
                    RUNTIME MANAGED LAYER (static, no ECS per-frame cost)
                              |
  LocalizationManager (static singleton class)
  +-- activeLocale : LocaleDefinition
  +-- activeTable  : Dictionary<string, string> (merged from all StringTableSOs)
  +-- fallbackTable: Dictionary<string, string> (fallback locale, e.g. en-US)
  +-- pluralRules  : IPluralRule (per-locale)
  +-- formatCache  : Dictionary<string, CompiledFormat>
  +-- fontMap      : FontMappingSO (active locale)
  +-- Initialize(db)        -- loads tables, detects system locale
  +-- SetLocale(code)       -- switches locale, rebuilds tables, fires event
  +-- Get(key)              -- O(1) dictionary lookup
  +-- GetFormatted(key, args) -- positional/named arg substitution
  +-- GetPlural(key, count) -- locale-aware plural form selection
  +-- GetFont(style)        -- returns TMP_FontAsset for current locale
                              |
                    ECS LAYER (minimal, event-driven only)
                              |
  LocaleConfig singleton      LocaleChangedTag
  (CurrentLocaleId byte,      (zero-size IComponentData,
   FallbackLocaleId byte)      added on locale switch,
                               triggers UI refresh)
                              |
                    SYSTEM PIPELINE (Client|Local only)
                              |
  LocalizationBootstrapSystem (InitializationSystemGroup)
      -- loads DB, creates singleton, initializes manager (runs once)
  LocaleChangeSystem (SimulationSystemGroup)
      -- detects LocaleConfig changes, adds LocaleChangedTag
  LocalizedTextRefreshSystem (PresentationSystemGroup)
      -- when LocaleChangedTag present, signals all ILocalizableUI providers
      -- removes tag after dispatch
                              |
                    UI INTEGRATION LAYER
                              |
  LocalizationUIRegistry (static, follows CombatUIRegistry pattern)
  +-- List<ILocalizableUI> providers
  +-- RegisterProvider / UnregisterProvider
  +-- NotifyLocaleChanged()  -- iterates all providers
                              |
  ILocalizableUI interface -> implemented by:
    DialogueUIAdapter, CombatUIAdapter, QuestUIAdapter,
    InventoryUIAdapter, CraftingUIAdapter, ProgressionUIAdapter,
    SaveUIAdapter, SettingsUIAdapter
                              |
  LocalizedText (MonoBehaviour, auto-resolve component)
  +-- StringKey : string      -- key to resolve
  +-- FallbackText : string   -- displayed if key not found
  +-- OnEnable: resolves key, subscribes to locale changes
  +-- OnLocaleChanged: re-resolves key
                              |
                    DIALOGUE INTEGRATION
                              |
  DialogueLocalization.Resolve(key)
      -- MODIFIED: calls LocalizationManager.Get(key)
      -- fallback: returns key if manager not initialized
```

### Data Flow (Locale Switch)

```
Frame N (Client):
  1. Player opens Settings UI, selects "Japanese" from locale dropdown
     -> UI calls LocalizationManager.SetLocale("ja-JP")

  2. LocalizationManager:
     - Looks up LocaleDefinition for "ja-JP"
     - Rebuilds activeTable from all StringTableSOs (merges ja-JP entries)
     - Loads fallback chain (ja-JP -> en-US for missing keys)
     - Selects IPluralRule for Japanese
     - Selects FontMappingSO for ja-JP
     - Updates LocaleConfig singleton: CurrentLocaleId = ja-JP index
     - Saves "ja-JP" to PlayerPrefs
     - Fires OnLocaleChanged event

  3. LocaleChangeSystem (SimulationSystemGroup):
     - Detects LocaleConfig change filter
     - Adds LocaleChangedTag entity

Frame N+1 (Client):
  4. LocalizedTextRefreshSystem (PresentationSystemGroup):
     - Reads LocaleChangedTag
     - Calls LocalizationUIRegistry.NotifyLocaleChanged()
     - Each ILocalizableUI provider re-resolves all visible strings
     - Each LocalizedText MonoBehaviour re-resolves its key
     - Removes LocaleChangedTag
```

---

## Design Decision: Unity Localization vs Custom

### Recommendation: Hybrid Approach

| Layer | Solution | Rationale |
|-------|----------|-----------|
| Game content (dialogue, items, quests, crafting, combat) | **Custom StringTableSO** | Faster iteration, designer-friendly inspector, no package dependency, version-controlled in project |
| Platform strings (achievements, store descriptions, console certification) | **Unity Localization package** (optional future integration) | Required by platform certification, handles platform-specific workflows |
| Translator workflow | **CSV/XLIFF import/export** from custom StringTableSOs | Industry-standard formats, compatible with Crowdin/Transifex/memoQ |

### Why Not Unity Localization for Everything

1. **Package overhead** -- Unity Localization adds Addressables dependency, async loading complexity, and ~2MB of managed assemblies
2. **Iteration speed** -- ScriptableObject string tables are immediately serialized, no async loading pipeline, no smart string compilation at build time
3. **Designer UX** -- Custom editor window with search, filter, bulk edit, and inline preview is faster than navigating Unity's Localization Tables window
4. **ECS compatibility** -- Unity Localization is fully managed (no Burst, no jobs). Custom static class with pre-built dictionary is the same cost but avoids the package's async overhead
5. **DIG already uses SO patterns** -- every other system (ItemEntrySO, QuestDefinitionSO, RecipeDefinitionSO) uses SO-based data. String tables fit the existing pattern

### Unity Localization Integration Point (Future)

If platform certification requires Unity Localization (e.g., PlayStation Store descriptions), `LocalizationManager.Get()` can fall back to `LocalizationSettings.StringDatabase.GetLocalizedString()` for keys prefixed with `platform:`. This is a <20 line change, deferred until a platform certification requirement appears.

---

## ECS Components

### LocaleConfig Singleton (~4 bytes)

**File:** `Assets/Scripts/Localization/Components/LocaleComponents.cs`

```
LocaleConfig (IComponentData)
  CurrentLocaleId  : byte    // Index into LocalizationDatabase.Locales[]
  FallbackLocaleId : byte    // Fallback locale index (typically en-US = 0)
  Reserved         : ushort  // Alignment padding, future flags
```

Created by `LocalizationBootstrapSystem` as a singleton entity. No ghost replication -- locale is client-only.

### LocaleChangedTag (zero bytes)

```
LocaleChangedTag (IComponentData)
  -- zero-size tag, added when locale switches, removed after UI refresh
```

Added by `LocaleChangeSystem` when `LocaleConfig` changes. `LocalizedTextRefreshSystem` removes it after dispatching to all `ILocalizableUI` providers.

### No Player Entity Components

Locale is a per-client setting stored in PlayerPrefs, not per-player game state. The `LocaleConfig` singleton lives on a standalone entity. **Zero bytes added to the player archetype.**

---

## ScriptableObjects

### StringTableSO

**File:** `Assets/Scripts/Localization/StringTableSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Localization/String Table")]
```

| Field | Type | Purpose |
|-------|------|---------|
| TableId | string | Unique table identifier (e.g., "Dialogue", "Items", "Quests", "Combat", "UI") |
| Description | string | Human-readable description for editor display |
| Entries | StringTableEntry[] | Array of key-value pairs per locale |

```
StringTableEntry [Serializable]
  Key        : string     // Unique within this table (e.g., "quest_001_name", "item_sword_desc")
  Locale     : string     // Locale code (e.g., "en-US", "ja-JP", "de-DE")
  Value      : string     // Translated text (supports {0}, {1} positional args and {PlayerName} named args)
  PluralForm : PluralCategory  // None(0), One(1), Few(2), Many(3), Other(4)
  Notes      : string     // Translator context notes (max chars, tone, gender hints)
```

Design: Entries are stored flat (key + locale pairs) rather than nested dictionaries for Unity serialization compatibility. At runtime, `LocalizationManager.Initialize()` builds per-locale dictionaries from the flat array.

### LocaleDefinition

**File:** `Assets/Scripts/Localization/LocaleDefinition.cs`

```
[CreateAssetMenu(menuName = "DIG/Localization/Locale Definition")]
```

| Field | Type | Purpose |
|-------|------|---------|
| LocaleCode | string | IETF BCP 47 code (e.g., "en-US", "ja-JP", "ar-SA") |
| DisplayName | string | Native name (e.g., "English", "Japanese", "Arabic") |
| EnglishName | string | English name for editor display |
| TextDirection | TextDirection enum | LTR(0), RTL(1) |
| PluralRuleSet | PluralRuleSet enum | English(0), French(1), Japanese(2), Arabic(3), Polish(4), Russian(5) |
| DefaultFont | TMP_FontAsset | Primary font for this locale |
| FallbackFonts | TMP_FontAsset[] | Fallback chain for missing glyphs |
| LineSpacingMultiplier | float | 1.0 default, 1.2 for CJK vertical density |
| CharacterSpacingMultiplier | float | 1.0 default, adjustable for dense scripts |
| IsComplete | bool | Editor flag: true when all string tables fully translated |

### LocalizationDatabase

**File:** `Assets/Scripts/Localization/LocalizationDatabase.cs`

```
[CreateAssetMenu(menuName = "DIG/Localization/Localization Database")]
```

| Field | Type | Purpose |
|-------|------|---------|
| Locales | List\<LocaleDefinition\> | All supported locales (index 0 = default/fallback) |
| StringTables | List\<StringTableSO\> | All string tables across all content domains |
| FontMappings | List\<FontMappingSO\> | Per-locale font asset mappings |
| DefaultLocaleCode | string | Default locale if detection fails (default "en-US") |
| EnablePseudoLocalization | bool | Editor-only: generate test strings |
| PseudoLocaleCode | string | "pseudo" -- synthetic locale for testing |

Placed in `Resources/LocalizationDatabase.asset` for `Resources.Load<LocalizationDatabase>()` by bootstrap system.

### FontMappingSO

**File:** `Assets/Scripts/Localization/FontMappingSO.cs`

```
[CreateAssetMenu(menuName = "DIG/Localization/Font Mapping")]
```

| Field | Type | Purpose |
|-------|------|---------|
| LocaleCode | string | Which locale this mapping applies to |
| BodyFont | TMP_FontAsset | Body text font |
| HeaderFont | TMP_FontAsset | Header/title font |
| TooltipFont | TMP_FontAsset | Tooltip text font |
| CombatFont | TMP_FontAsset | Damage numbers, combat text |
| ButtonFont | TMP_FontAsset | Button labels |
| MonoFont | TMP_FontAsset | Monospace (debug, technical text) |

If a specific style font is null, falls back to `LocaleDefinition.DefaultFont`.

---

## Core API (Static, Managed)

**File:** `Assets/Scripts/Localization/LocalizationManager.cs`

```
public static class LocalizationManager

  // ==================== INITIALIZATION ====================

  Initialize(LocalizationDatabase db)
    -- Called by LocalizationBootstrapSystem on startup
    -- Builds per-locale dictionaries from all StringTableSO entries
    -- Detects system locale via Application.systemLanguage + CultureInfo
    -- Loads saved locale from PlayerPrefs("dig_locale")
    -- Falls back to db.DefaultLocaleCode if saved/system locale unsupported
    -- Sets activeLocale, builds activeTable + fallbackTable
    -- IsInitialized = true

  // ==================== LOCALE SWITCHING ====================

  SetLocale(string localeCode)
    -- Validates localeCode exists in database
    -- Rebuilds activeTable dictionary from matching StringTableEntries
    -- Loads fallbackTable from fallback locale (en-US)
    -- Updates pluralRules to target locale's PluralRuleSet
    -- Updates fontMap to target locale's FontMappingSO
    -- Saves to PlayerPrefs("dig_locale")
    -- Fires OnLocaleChanged static event
    -- Returns bool success

  string CurrentLocaleCode { get; }
  LocaleDefinition CurrentLocale { get; }
  bool IsInitialized { get; }

  // ==================== STRING LOOKUP ====================

  string Get(string key)
    -- O(1) dictionary lookup in activeTable
    -- Falls back to fallbackTable if key missing
    -- Returns key itself if both miss (with [MISSING:key] prefix in editor)
    -- Logs warning on first miss per key (deduplicated)

  string GetFormatted(string key, params object[] args)
    -- Resolves key via Get(), then applies string.Format()
    -- Supports positional: "{0} damage dealt" -> "50 damage dealt"
    -- Future: named args via regex replacement {PlayerName} -> player.Name

  string GetPlural(string key, int count)
    -- Selects plural form based on locale rules + count
    -- Key suffixes: key_one, key_few, key_many, key_other
    -- Example: "enemy_killed_one" = "1 enemy killed"
    --          "enemy_killed_other" = "{0} enemies killed"

  string GetFormatted(string key, int count, params object[] args)
    -- Combines plural selection with format arguments

  // ==================== FONT ACCESS ====================

  TMP_FontAsset GetFont(FontStyle style)
    -- Returns font for current locale and requested style
    -- Falls back to LocaleDefinition.DefaultFont if style not mapped
    -- FontStyle enum: Body(0), Header(1), Tooltip(2), Combat(3), Button(4), Mono(5)

  TextDirection GetTextDirection()
    -- Returns LTR or RTL for current locale

  // ==================== EVENTS ====================

  static event Action OnLocaleChanged
    -- Fired after SetLocale() completes
    -- LocalizedText MonoBehaviours subscribe to this

  // ==================== EDITOR UTILITIES ====================

  #if UNITY_EDITOR
  string[] GetAllKeys()
  string[] GetMissingKeys(string localeCode)
  int GetCoveragePercent(string localeCode)
  string GeneratePseudoLocalized(string input)
    -- Accented chars, doubled length, bracketed: "[Accented Doubled Text]"
  #endif
```

### DialogueLocalization.Resolve() Update

**File:** `Assets/Scripts/Dialogue/Bridges/DialogueLocalization.cs` (MODIFY)

```
// BEFORE (current):
public static string Resolve(string key)
{
    if (string.IsNullOrEmpty(key)) return string.Empty;
    return key;
}

// AFTER:
public static string Resolve(string key)
{
    if (string.IsNullOrEmpty(key)) return string.Empty;

    if (LocalizationManager.IsInitialized)
        return LocalizationManager.Get(key);

    return key; // Fallback: no localization framework loaded
}
```

---

## Pluralization Engine

**File:** `Assets/Scripts/Localization/PluralRules.cs`

```
PluralCategory enum: One(0), Few(1), Many(2), Other(3)

IPluralRule interface
  PluralCategory Evaluate(int count)

// Built-in rules:

EnglishPluralRule: count == 1 -> One, else Other
FrenchPluralRule: count <= 1 -> One, else Other (0 is singular in French)
JapanesePluralRule: always Other (no plural forms)
ArabicPluralRule: 0->Other, 1->One, 2->One, 3-10->Few, 11-99->Many, 100+->Other
PolishPluralRule: Slavic rules (1=One, 2-4=Few, 5-21=Many, then repeats)
RussianPluralRule: Similar to Polish with %10/%100 branching
```

PluralRuleSet enum maps to IPluralRule implementations. `LocalizationManager.GetPlural()` appends `_one`, `_few`, `_many`, or `_other` suffix to the base key and resolves. If the suffixed key is missing, falls back to `_other`.

---

## ECS Systems

### System Execution Order

```
InitializationSystemGroup (Client|Local):
  LocalizationBootstrapSystem         -- loads DB, creates singleton, initializes manager (runs once)

SimulationSystemGroup (Client|Local):
  LocaleChangeSystem                  -- detects LocaleConfig changes, adds LocaleChangedTag

PresentationSystemGroup (Client|Local):
  LocalizedTextRefreshSystem          -- dispatches locale change to all ILocalizableUI providers
```

**No server-side systems.** Localization is entirely client-side. The server never needs to know what language a client is displaying.

### LocalizationBootstrapSystem

**File:** `Assets/Scripts/Localization/Systems/LocalizationBootstrapSystem.cs`

- `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]`
- `[UpdateInGroup(typeof(InitializationSystemGroup))]`
- Managed `SystemBase` (needs `Resources.Load`)
- **OnCreate:** Load `LocalizationDatabase` from `Resources/LocalizationDatabase`
- Create `LocaleConfig` singleton entity (4 bytes)
- Call `LocalizationManager.Initialize(database)` -- builds dictionaries, detects locale
- Set `LocaleConfig.CurrentLocaleId` from resolved locale
- `Enabled = false` (self-disables after first run)
- Follows `ItemRegistryBootstrapSystem` / `ProgressionBootstrapSystem` pattern

### LocaleChangeSystem

**File:** `Assets/Scripts/Localization/Systems/LocaleChangeSystem.cs`

- `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]`
- `[UpdateInGroup(typeof(SimulationSystemGroup))]`
- Managed `SystemBase`
- Reads `LocaleConfig` singleton with change filter
- When `LocaleConfig.CurrentLocaleId` changes (written by `LocalizationManager.SetLocale()` via EntityManager):
  - Creates transient entity with `LocaleChangedTag`
- No per-frame cost when locale is stable (change filter short-circuits)

### LocalizedTextRefreshSystem

**File:** `Assets/Scripts/Localization/Systems/LocalizedTextRefreshSystem.cs`

- `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]`
- `[UpdateInGroup(typeof(PresentationSystemGroup))]`
- Managed `SystemBase`
- Queries for `LocaleChangedTag` entities
- When found:
  - Calls `LocalizationUIRegistry.NotifyLocaleChanged()`
  - Each registered `ILocalizableUI` provider re-resolves all visible strings
  - Destroys `LocaleChangedTag` entities via ECB
- No per-frame cost in steady state (query returns empty)

---

## UI Integration Pattern

### ILocalizableUI Interface

**File:** `Assets/Scripts/Localization/UI/ILocalizableUI.cs`

```
public interface ILocalizableUI
{
    /// Called when the active locale changes.
    /// Implementations should re-resolve all visible localized strings.
    void OnLocaleChanged();
}
```

### LocalizationUIRegistry

**File:** `Assets/Scripts/Localization/UI/LocalizationUIRegistry.cs`

Static registry following `CombatUIRegistry` pattern:

```
public static class LocalizationUIRegistry

  RegisterProvider(ILocalizableUI provider)
  UnregisterProvider(ILocalizableUI provider)
  NotifyLocaleChanged()    -- iterates all registered providers, calls OnLocaleChanged()
  int ProviderCount { get; }
```

All UI adapters (DialogueUIAdapter, CombatUIAdapter, QuestUIAdapter, InventoryUIAdapter, CraftingUIAdapter, ProgressionUIAdapter, SaveUIAdapter) register on `OnEnable`, unregister on `OnDisable`.

### LocalizedText MonoBehaviour

**File:** `Assets/Scripts/Localization/UI/LocalizedText.cs`

```
[AddComponentMenu("DIG/Localization/Localized Text")]
[RequireComponent(typeof(TMP_Text))]
```

| Field | Type | Purpose |
|-------|------|---------|
| StringKey | string | Key to resolve from string tables |
| FallbackText | string | Displayed if key not found or manager not initialized |
| FontStyle | FontStyle enum | Which font style to apply (Body, Header, etc.) |
| AutoResolveOnEnable | bool | True (default): resolve key immediately on enable |

Subscribes to `LocalizationManager.OnLocaleChanged`. On locale change or enable:
1. Resolves `StringKey` via `LocalizationManager.Get(StringKey)`
2. Applies result to `TMP_Text.text`
3. Applies font from `LocalizationManager.GetFont(FontStyle)` to `TMP_Text.font`
4. Applies `enableAutoSizing` adjustments for CJK character density

Drag onto any UI text element in the scene. Replace hardcoded text assignments with `LocalizedText` components for static UI labels (menu buttons, headers, tooltips).

---

## String Key Conventions

### Key Naming Format

```
{domain}_{identifier}_{field}

Examples:
  dialogue_npc_blacksmith_greeting_text     -- dialogue node text
  dialogue_npc_blacksmith_greeting_speaker  -- speaker name
  item_iron_sword_name                      -- item display name
  item_iron_sword_desc                      -- item description
  quest_kill_wolves_name                    -- quest display name
  quest_kill_wolves_desc                    -- quest description
  quest_kill_wolves_obj_001                 -- objective description
  recipe_iron_ingot_name                    -- recipe display name
  ui_level_up                              -- "LEVEL UP!"
  ui_save_complete                         -- "Save Complete"
  ui_damage_dealt                          -- "{0} damage dealt"
  combat_enemy_killed_one                  -- "1 enemy killed"
  combat_enemy_killed_other                -- "{0} enemies killed"
```

### Recommended String Tables

| TableId | Domain | Approximate Key Count |
|---------|--------|----------------------|
| `Dialogue` | NPC dialogue, barks, choices | ~500-2000 |
| `Items` | Item names, descriptions | ~200-500 |
| `Quests` | Quest names, descriptions, objectives | ~100-300 |
| `Crafting` | Recipe names, descriptions | ~50-200 |
| `Combat` | Damage text, status effects, kill feed | ~50-100 |
| `UI` | Menu labels, buttons, tooltips, system messages | ~100-300 |
| `Tutorial` | Tutorial text, hints | ~50-100 |

---

## Designer Workflow

### 1. Creating Localized Content

**Before (current workflow):**
1. Designer creates QuestDefinitionSO, types English text directly into `DisplayName` and `Description` fields
2. Text is embedded in the SO asset file
3. No way to translate without modifying the SO

**After (localized workflow):**
1. Designer creates QuestDefinitionSO, enters string key `quest_kill_wolves_name` in `DisplayName`
2. Opens Localization Workstation, navigates to Quests string table
3. Adds entry: key=`quest_kill_wolves_name`, en-US="Kill the Wolves", notes="Quest title, max 40 chars"
4. Runtime: `LocalizationManager.Get("quest_kill_wolves_name")` returns the localized string
5. Translator opens exported CSV, adds ja-JP column, translates

### 2. Translator Handoff

```
Export: Localization Workstation -> Export CSV/XLIFF
  - One file per StringTableSO
  - Columns: Key | en-US | ja-JP | de-DE | ... | Notes | MaxChars
  - XLIFF 1.2 format for professional CAT tools

Import: Translator returns CSV/XLIFF -> Import via Workstation
  - Validates key existence
  - Reports new/removed keys since last export
  - Warns on string length violations
```

### 3. Pseudo-Localization Testing

Toggle in Localization Workstation or `LocalizationDatabase.EnablePseudoLocalization`:

```
Input:  "Kill the Wolves"
Output: "[Kkiillll  tthhee  WWoollvveess]"
         ^-- brackets catch truncation
         ^-- doubled length catches overflow
         ^-- accented chars catch encoding issues
```

All `LocalizationManager.Get()` calls return pseudo-localized strings when active. Immediately reveals:
- Truncated text (UI elements too narrow)
- Concatenated strings (broken by word order changes)
- Hardcoded strings (not going through localization)
- Font rendering issues (accented characters)

---

## Editor Tooling

### LocalizationWorkstationModule

**File:** `Assets/Editor/LocalizationWorkstation/LocalizationWorkstationModule.cs`

- Menu: `DIG/Localization Workstation`
- Sidebar + tab pattern (matches ProgressionWorkstation, PersistenceWorkstation)

### Modules (6 Tabs)

| Tab | File | Purpose |
|-----|------|---------|
| String Browser | `Modules/StringBrowserModule.cs` | Table view: search by key/value, filter by table/locale, inline edit values. Bulk edit (find/replace across all tables). Add/remove keys. Sort by key, table, or completion status |
| Coverage Heatmap | `Modules/CoverageHeatmapModule.cs` | Grid: rows = StringTableSOs, columns = locales. Cell color: green (100%), yellow (50-99%), red (<50%). Click cell to show missing keys. Overall % per locale |
| Missing Key Scanner | `Modules/MissingKeyScannerModule.cs` | Scans all SOs (ItemEntrySO, QuestDefinitionSO, RecipeDefinitionSO, DialogueTreeSO, BarkCollectionSO) for string fields. Reports: keys referenced but not in any string table, keys in tables but not referenced by any SO (orphaned), hardcoded English strings that should be keys |
| Font Preview | `Modules/FontPreviewModule.cs` | Select locale, see sample text rendered with that locale's fonts for each FontStyle. Test CJK, RTL, accented characters. Preview line spacing and character spacing multipliers |
| Import/Export | `Modules/ImportExportModule.cs` | Export: CSV or XLIFF 1.2 per StringTableSO. Import: CSV/XLIFF with validation report (new keys, removed keys, changed values, length warnings). Batch export all tables. Diff view before import commit |
| Pseudo-Loc | `Modules/PseudoLocModule.cs` | Toggle pseudo-localization on/off. Preview pseudo-localized strings. Generate pseudo locale entries for all tables. Custom rules: expansion ratio (1.3-3.0x), accent style (accented Latin, CJK placeholder, RTL mirrored) |

---

## Persistence

### No ISaveModule

Locale preference is **not** game state. It is a client-side display preference stored in `PlayerPrefs`:

```
PlayerPrefs key: "dig_locale"
Value: locale code string (e.g., "ja-JP")
```

Rationale:
- Locale is per-device, not per-character (a player with 3 characters wants all in Japanese)
- No server involvement -- the server never needs to know the client's display language
- PlayerPrefs survives game uninstall/reinstall on most platforms
- No serialization complexity, no version migration, no save file bloat

---

## Performance Budget

| Operation | Budget | Method | Notes |
|-----------|--------|--------|-------|
| String lookup `Get(key)` | < 0.001ms | O(1) Dictionary.TryGetValue | ~50ns per call |
| Formatted string `GetFormatted(key, args)` | < 0.01ms | string.Format on resolved string | Allocation per call (unavoidable) |
| Plural lookup `GetPlural(key, count)` | < 0.002ms | Rule evaluation + dictionary lookup | Two lookups worst case |
| Locale switch `SetLocale(code)` | ~5ms (one-time) | Rebuild activeTable dictionary | ~2000 entries, Dictionary constructor |
| Font swap (one-time per switch) | ~2ms | TMP_FontAsset reassignment | Triggers TMP atlas rebuild if font not cached |
| Bootstrap initialization | ~10ms (startup) | Parse all StringTableSO entries | One-time, InitializationSystemGroup |
| Per-frame ECS overhead (steady state) | 0.000ms | Change filter short-circuits | No work unless locale changes |
| Per-frame ECS overhead (locale change frame) | < 0.1ms | Iterate ILocalizableUI providers | One frame only |

### Memory Budget

| Data | Estimated Size | Notes |
|------|---------------|-------|
| activeTable (Dictionary) | ~200 KB for 2000 keys | Key strings + value strings + hash table overhead |
| fallbackTable (Dictionary) | ~200 KB for 2000 keys | Always loaded (en-US) |
| StringTableSO assets (editor) | ~1 MB total | Serialized flat arrays, loaded by bootstrap |
| Font assets per locale | ~2-10 MB | TMP_FontAsset with atlas textures, varies by glyph count |

---

## 16KB Archetype Impact

| Addition | Size | Location |
|----------|------|----------|
| `LocaleConfig` singleton | 4 bytes | Standalone entity (NOT player) |
| `LocaleChangedTag` | 0 bytes | Transient entity (destroyed same frame) |
| **Total on player entity** | **0 bytes** | **NONE** |

Locale is a client-only singleton. No components are added to the player entity. No ghost replication. No archetype impact whatsoever.

---

## File Summary

### New Files (12)

| # | Path | Type | Phase |
|---|------|------|-------|
| 1 | `Assets/Scripts/Localization/LocalizationManager.cs` | Static class (managed) | 0 |
| 2 | `Assets/Scripts/Localization/StringTableSO.cs` | ScriptableObject | 0 |
| 3 | `Assets/Scripts/Localization/LocaleDefinition.cs` | ScriptableObject | 0 |
| 4 | `Assets/Scripts/Localization/LocalizationDatabase.cs` | ScriptableObject | 0 |
| 5 | `Assets/Scripts/Localization/FontMappingSO.cs` | ScriptableObject | 0 |
| 6 | `Assets/Scripts/Localization/PluralRules.cs` | IPluralRule interface + implementations | 0 |
| 7 | `Assets/Scripts/Localization/Components/LocaleComponents.cs` | IComponentData (singleton + tag) | 1 |
| 8 | `Assets/Scripts/Localization/Systems/LocalizationBootstrapSystem.cs` | SystemBase (Client\|Local, Initialization) | 1 |
| 9 | `Assets/Scripts/Localization/Systems/LocaleChangeSystem.cs` | SystemBase (Client\|Local, Simulation) | 1 |
| 10 | `Assets/Scripts/Localization/Systems/LocalizedTextRefreshSystem.cs` | SystemBase (Client\|Local, Presentation) | 1 |
| 11 | `Assets/Scripts/Localization/UI/LocalizedText.cs` | MonoBehaviour | 2 |
| 12 | `Assets/Scripts/Localization/UI/LocalizationUIRegistry.cs` | Static class (follows CombatUIRegistry pattern) | 2 |

### Editor Files (7)

| # | Path | Type | Phase |
|---|------|------|-------|
| 13 | `Assets/Editor/LocalizationWorkstation/LocalizationWorkstationWindow.cs` | EditorWindow | 3 |
| 14 | `Assets/Editor/LocalizationWorkstation/ILocalizationWorkstationModule.cs` | Interface | 3 |
| 15 | `Assets/Editor/LocalizationWorkstation/Modules/StringBrowserModule.cs` | Module | 3 |
| 16 | `Assets/Editor/LocalizationWorkstation/Modules/CoverageHeatmapModule.cs` | Module | 3 |
| 17 | `Assets/Editor/LocalizationWorkstation/Modules/MissingKeyScannerModule.cs` | Module | 3 |
| 18 | `Assets/Editor/LocalizationWorkstation/Modules/ImportExportModule.cs` | Module | 3 |
| 19 | `Assets/Editor/LocalizationWorkstation/Modules/PseudoLocModule.cs` | Module | 3 |

### Modified Files

| # | Path | Change |
|---|------|--------|
| 1 | `Assets/Scripts/Dialogue/Bridges/DialogueLocalization.cs` | `Resolve()` calls `LocalizationManager.Get(key)` instead of returning raw key (~3 lines) |

### Resource Assets

| # | Path |
|---|------|
| 1 | `Resources/LocalizationDatabase.asset` |

---

## Cross-EPIC Integration

| System | EPIC | Integration |
|--------|------|-------------|
| `DialogueLocalization.Resolve()` | 16.16 (Dialogue) | Becomes real localization lookup. All `DialogueNode.Text`, `DialogueChoice.Text`, `BarkLine.Text` pass through `Resolve()` which now calls `LocalizationManager.Get()`. Dialogue trees authored with string keys instead of raw text |
| `QuestUIBridgeSystem` | 16.12 (Quest) | Quest tracker, quest log, objective descriptions resolve via `LocalizationManager.Get()`. `QuestDefinitionSO.DisplayName` and `Description` become string keys |
| `CraftOutputGenerationSystem` / Crafting UI | 16.13 (Crafting) | Recipe names and descriptions resolve via `LocalizationManager.Get()`. `RecipeDefinitionSO.DisplayName` and `Description` become string keys |
| `ProgressionUIBridgeSystem` | 16.14 (Progression) | "LEVEL UP!", stat names, XP labels resolve via `LocalizationManager.Get()` |
| Achievement UI | 17.7 (Achievement) | Achievement titles, descriptions, progress text resolve via `LocalizationManager.Get()` |
| Minimap UI | 17.6 (Minimap) | POI names, zone names, compass labels resolve via `LocalizationManager.Get()` |
| `CombatUIBridgeSystem` | 15.9 (Combat UI) | Kill feed text, status effect names, combo counter labels resolve via `LocalizationManager.Get()` |
| `SaveUIBridgeSystem` | 16.15 (Persistence) | Save/load notification strings ("Save Complete", "Load Failed") resolve via `LocalizationManager.Get()` |
| Item tooltips / Inventory UI | 16.6 (Loot/Items) | `ItemEntrySO.DisplayName` and `Description` become string keys. Tooltip UI resolves via `LocalizationManager.Get()` |
| All UI MonoBehaviours | All UI EPICs | Implement `ILocalizableUI` interface for automatic refresh on locale change |

---

## Backward Compatibility

| Scenario | Behavior | Notes |
|----------|----------|-------|
| `LocalizationManager` not initialized | `DialogueLocalization.Resolve()` returns raw key (same as today) | Zero regression for existing content |
| String key not found in any table | Returns the key itself (with `[MISSING:key]` prefix in editor builds) | Graceful degradation |
| `LocalizationDatabase` asset missing from Resources/ | Bootstrap logs error, manager stays uninitialized | All text displays as raw keys (English) |
| No `ILocalizableUI` providers registered | `LocalizedTextRefreshSystem` completes instantly, no warnings | UI just doesn't update on locale change |
| Old SOs with raw English text (not string keys) | `LocalizationManager.Get("Kill the Wolves")` misses, returns "Kill the Wolves" | Works as before until SO is migrated to use keys |
| Font mapping missing for locale | Falls back to `LocaleDefinition.DefaultFont`, then TMP default | Never crashes, may show wrong font |

---

## Migration Strategy

Converting existing hardcoded English text to string keys is a gradual, non-breaking process:

### Phase 1: Framework (this EPIC)
- Install localization framework, bootstrap system, `LocalizationManager`
- Update `DialogueLocalization.Resolve()` to use `LocalizationManager.Get()`
- Create empty string tables for each domain
- All existing text continues to work unchanged (passthrough on key miss)

### Phase 2: Dialogue Migration
- Replace `DialogueNode.Text` values with string keys in existing DialogueTreeSOs
- Add corresponding entries to Dialogue string table (en-US)
- Barks and choices follow same pattern

### Phase 3: Content SO Migration
- Replace `ItemEntrySO.DisplayName/Description` with string keys
- Replace `QuestDefinitionSO.DisplayName/Description/ObjectiveDescription` with string keys
- Replace `RecipeDefinitionSO.DisplayName/Description` with string keys
- Missing Key Scanner identifies remaining hardcoded strings

### Phase 4: UI Migration
- Replace hardcoded UI labels with `LocalizedText` components
- Add `ILocalizableUI` to all UI adapters
- Combat UI, Progression UI, Save UI strings moved to UI string table

### Phase 5: First Translation
- Export all string tables as CSV
- Send to translators for target locale (e.g., ja-JP)
- Import translations, create FontMappingSO for CJK fonts
- Test with pseudo-localization, then real Japanese

---

## Verification Checklist

### Framework Core
- [ ] `LocalizationBootstrapSystem` loads `LocalizationDatabase` from Resources and creates `LocaleConfig` singleton
- [ ] `LocalizationManager.Initialize()` builds per-locale dictionaries from StringTableSO entries
- [ ] `LocalizationManager.Get("existing_key")` returns correct translated string
- [ ] `LocalizationManager.Get("missing_key")` returns key with `[MISSING:]` prefix in editor, raw key in build
- [ ] `LocalizationManager.GetFormatted("key", 50)` substitutes `{0}` correctly
- [ ] `LocalizationManager.GetPlural("enemy_killed", 1)` returns singular form
- [ ] `LocalizationManager.GetPlural("enemy_killed", 5)` returns plural form
- [ ] `LocalizationManager.GetFont(FontStyle.Body)` returns correct TMP_FontAsset for current locale

### Locale Switching
- [ ] `LocalizationManager.SetLocale("ja-JP")` rebuilds active table with Japanese entries
- [ ] Missing Japanese keys fall back to en-US entries
- [ ] `LocaleConfig.CurrentLocaleId` updates on locale switch
- [ ] `LocaleChangeSystem` detects config change, adds `LocaleChangedTag`
- [ ] `LocalizedTextRefreshSystem` dispatches to all `ILocalizableUI` providers
- [ ] `LocaleChangedTag` removed after dispatch (no per-frame cost afterward)
- [ ] Locale preference persisted to PlayerPrefs("dig_locale")
- [ ] Locale restored from PlayerPrefs on next session start

### Dialogue Integration
- [ ] `DialogueLocalization.Resolve("dialogue_key")` returns localized string
- [ ] `DialogueLocalization.Resolve("dialogue_key")` falls back to raw key when manager not initialized
- [ ] Dialogue UI displays translated text for active locale
- [ ] Switching locale mid-conversation updates visible text

### LocalizedText Component
- [ ] `LocalizedText` on TMP_Text resolves key on enable
- [ ] `LocalizedText` re-resolves on locale change
- [ ] `LocalizedText` applies locale-specific font
- [ ] `LocalizedText` with missing key shows FallbackText

### Font Switching
- [ ] Switching to ja-JP locale applies Japanese font to all text styles
- [ ] Switching to ar-SA locale applies Arabic font with RTL text direction
- [ ] Font fallback chain works when primary font lacks specific glyphs
- [ ] LineSpacingMultiplier applied correctly for CJK locales

### Pluralization
- [ ] English: 1 = "1 wolf", 5 = "5 wolves"
- [ ] French: 0 = "0 loup" (singular, French rule), 2 = "2 loups"
- [ ] Japanese: always "other" form (no plural distinction)
- [ ] Arabic: correct One/Few/Many/Other forms for 0, 1, 2, 5, 11, 100

### Editor Tooling
- [ ] Localization Workstation: String Browser lists all keys with search/filter
- [ ] Localization Workstation: Coverage Heatmap shows % translated per locale per table
- [ ] Localization Workstation: Missing Key Scanner identifies unlocalized SO strings
- [ ] Localization Workstation: Font Preview renders sample text per locale
- [ ] Localization Workstation: CSV Export produces valid file with all keys and locales
- [ ] Localization Workstation: CSV Import adds translations without data loss
- [ ] Localization Workstation: Pseudo-Loc toggle generates accented/doubled strings

### Pseudo-Localization
- [ ] Pseudo-loc enabled: all `Get()` calls return accented/doubled/bracketed strings
- [ ] Pseudo-loc reveals truncated UI elements (text cut off)
- [ ] Pseudo-loc reveals hardcoded strings (not going through localization)
- [ ] Pseudo-loc disabled: normal strings resume

### Performance
- [ ] String lookup < 0.001ms (verify in Profiler)
- [ ] Locale switch completes in < 10ms
- [ ] Zero per-frame ECS overhead in steady state
- [ ] No GC allocations from `Get()` calls (returns existing string references)
- [ ] `GetFormatted()` allocations are from `string.Format()` only (unavoidable)

### Backward Compatibility
- [ ] Existing content with raw English text works unchanged (key miss returns raw text)
- [ ] No `LocalizationDatabase` asset: bootstrap logs error, all text displays as English
- [ ] No regression in dialogue, quest, crafting, combat UI systems
- [ ] No player entity archetype change (verify: 0 bytes added)
