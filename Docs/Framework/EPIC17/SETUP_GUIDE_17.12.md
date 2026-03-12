# EPIC 17.12: Localization Framework — Setup Guide

## Overview

The Localization Framework provides runtime string resolution for all game text (dialogue, items, quests, crafting, combat UI, system messages), locale-aware font switching for CJK/RTL scripts, pluralization rules per locale, and formatted string support. It uses custom ScriptableObject-based string tables for fast iteration and designer-friendly workflows.

The system is entirely client-side. The server never needs to know which language a client is displaying. Locale preference is stored in PlayerPrefs, not in save data.

---

## 1. Quick Start: Creating the Database

Three types of assets make up the localization data. They must be created in this order.

### Step 1: Create Locale Definitions

Create one asset per supported language.

1. Right-click in the Project window: **Create → DIG → Localization → Locale Definition**.
2. Name it after the locale code (e.g., `Locale_en-US`, `Locale_ja-JP`).
3. Configure each locale:

| Field | Example (English) | Example (Japanese) | Description |
|-------|--------------------|--------------------|-------------|
| Locale Code | `en-US` | `ja-JP` | IETF BCP 47 code. Must be unique. |
| Display Name | `English` | `日本語` | Native name shown in language selection UI. |
| English Name | `English` | `Japanese` | For editor display only. |
| Text Direction | LTR | LTR | Set to RTL for Arabic (`ar-SA`), Hebrew (`he-IL`). |
| Plural Rule Set | English | Japanese | Determines singular/plural logic. See Plural Rules below. |
| Default Font | (your TMP font) | (CJK TMP font) | Primary `TMP_FontAsset` for this locale. |
| Fallback Fonts | (optional) | (optional) | Additional fonts for missing glyphs. |
| Line Spacing Multiplier | 1.0 | 1.2 | Increase for CJK to improve vertical readability. |
| Character Spacing Multiplier | 1.0 | 1.0 | Adjust for dense scripts. |
| Is Complete | (check when done) | (uncheck until done) | Editor-only tracking flag. |

#### Available Plural Rule Sets

| Rule Set | Languages | Behavior |
|----------|-----------|----------|
| English | English, German, Dutch, etc. | 1 = singular, everything else = plural |
| French | French, Portuguese, etc. | 0 and 1 = singular, 2+ = plural |
| Japanese | Japanese, Chinese, Korean, Thai, Vietnamese | No plural forms (always "other") |
| Arabic | Arabic | Special forms for 0, 1, 2, 3-10, 11-99, 100+ |
| Polish | Polish, Czech, etc. | Slavic rules with Few/Many forms based on last digit |
| Russian | Russian, Ukrainian, etc. | Similar to Polish with %10/%100 branching |

### Step 2: Create String Tables

Create one string table per content domain.

1. Right-click: **Create → DIG → Localization → String Table**.
2. Recommended tables:

| Asset Name | Table Id | Content |
|------------|----------|---------|
| `ST_Dialogue` | `Dialogue` | NPC dialogue, barks, choices |
| `ST_Items` | `Items` | Item names and descriptions |
| `ST_Quests` | `Quests` | Quest names, descriptions, objectives |
| `ST_Crafting` | `Crafting` | Recipe names and descriptions |
| `ST_Combat` | `Combat` | Damage text, status effects, kill feed |
| `ST_UI` | `UI` | Menu labels, buttons, tooltips, system messages |
| `ST_Tutorial` | `Tutorial` | Tutorial text, hints |

3. Add entries to each table. Each entry has:

| Field | Description |
|-------|-------------|
| Key | Unique string key (e.g., `item_iron_sword_name`). See Key Naming below. |
| Locale | Locale code this entry is for (e.g., `en-US`). |
| Value | The translated text. Supports `{0}`, `{1}` positional arguments. |
| Plural Form | Set to the plural category if this is a plural variant (One, Few, Many, Other). |
| Notes | Context for translators (max characters, tone, gender hints). |

> **Tip:** You don't need to add entries directly in the Inspector. Use the **Localization Workstation** (see section 5) for a much faster editing experience with search, filter, and inline editing.

### Step 3: Create Font Mappings (Optional)

Only needed if a locale requires different fonts per text style (e.g., a special CJK combat font).

1. Right-click: **Create → DIG → Localization → Font Mapping**.
2. Set the `Locale Code` to match the locale definition.
3. Assign `TMP_FontAsset` references for each style slot:

| Slot | Used For |
|------|----------|
| Body Font | Default body text |
| Header Font | Section headers, titles |
| Tooltip Font | Item/ability tooltips |
| Combat Font | Damage numbers, combat text |
| Button Font | Button labels |
| Mono Font | Debug/technical text |

If a slot is left empty, the locale's `Default Font` is used as fallback.

### Step 4: Create the Localization Database

This is the root registry that ties everything together.

1. Right-click: **Create → DIG → Localization → Localization Database**.
2. Name it `LocalizationDatabase` and place it in a `Resources` folder: `Assets/Resources/LocalizationDatabase.asset`.
3. Configure:

| Field | Description |
|-------|-------------|
| Locales | Drag in all Locale Definition assets. **Index 0 is the default/fallback locale** (typically `en-US`). |
| String Tables | Drag in all String Table assets. |
| Font Mappings | Drag in any Font Mapping assets. |
| Default Locale Code | `en-US` (or whichever locale to use when system detection fails). |
| Enable Pseudo Localization | Editor-only toggle. See section 6. |

> **Important:** The database must be at `Resources/LocalizationDatabase` (with that exact name) for the bootstrap system to find it via `Resources.Load`.

---

## 2. String Key Conventions

All localized text is referenced by string keys, not raw English text. Use this naming format:

```
{domain}_{identifier}_{field}
```

### Examples

| Key | Content |
|-----|---------|
| `item_iron_sword_name` | Item display name |
| `item_iron_sword_desc` | Item description |
| `quest_kill_wolves_name` | Quest title |
| `quest_kill_wolves_desc` | Quest description |
| `quest_kill_wolves_obj_001` | Objective description |
| `recipe_iron_ingot_name` | Recipe display name |
| `dialogue_npc_blacksmith_greeting_text` | Dialogue node text |
| `dialogue_npc_blacksmith_greeting_speaker` | Speaker name |
| `ui_level_up` | "LEVEL UP!" |
| `ui_save_complete` | "Save Complete" |
| `ui_damage_dealt` | "{0} damage dealt" |

### Plural Keys

For strings that need plural forms, add entries with suffix variants:

| Key | Locale | Value |
|-----|--------|-------|
| `combat_enemy_killed_one` | en-US | `1 enemy killed` |
| `combat_enemy_killed_other` | en-US | `{0} enemies killed` |
| `combat_enemy_killed_other` | ja-JP | `{0}体の敵を倒した` |

The system selects the correct form based on the locale's plural rules and the count argument.

---

## 3. Using Localized Text in UI

### Static UI Labels (No Code Required)

For any TextMeshPro text element that displays a fixed localized string:

1. Select the GameObject with the `TextMeshPro - Text (UI)` or `TextMeshPro` component.
2. **Add Component → DIG → Localization → Localized Text**.
3. Fill in:

| Field | Description |
|-------|-------------|
| String Key | The key from your string table (e.g., `ui_settings_title`). |
| Fallback Text | Displayed if the key isn't found or localization isn't initialized. Good for pre-localization development. |
| Font Style | Which font style to pull from the locale's font mapping (Body, Header, Tooltip, Combat, Button, Mono). |
| Auto Resolve On Enable | Leave checked. Resolves the key immediately when the component is enabled. |

The component automatically:
- Resolves the key and sets `TMP_Text.text` on enable
- Re-resolves when the player switches language
- Applies the locale's font, line spacing, character spacing, and RTL direction

### Dynamic UI Text (Code)

For text that changes at runtime (e.g., damage numbers, quest progress), call the API directly:

```csharp
// Simple lookup
string text = LocalizationManager.Get("ui_save_complete");

// With format arguments
string text = LocalizationManager.GetFormatted("ui_damage_dealt", damageAmount);

// Plural-aware
string text = LocalizationManager.GetPluralFormatted("combat_enemy_killed", killCount, killCount);

// Get locale-appropriate font
TMP_FontAsset font = LocalizationManager.GetFont(FontStyle.Combat);
```

### Dialogue Integration

`DialogueLocalization.Resolve()` automatically uses the localization framework. Dialogue trees authored with string keys in the `Text` field will resolve through the active locale's string table. If the localization system isn't initialized (e.g., no `LocalizationDatabase` asset), dialogue falls back to displaying the raw key string -- identical to pre-localization behavior.

---

## 4. Adding a New Language

1. **Create a Locale Definition** asset for the new language (see Step 1 above).
2. **Add it to the Localization Database** `Locales` list.
3. **Add entries to each String Table** for the new locale code. Each existing key needs a new entry with the new locale code and translated value.
4. **(Optional) Create a Font Mapping** if the language needs different fonts (CJK, Arabic, etc.).
5. **(Optional) Import fonts**: For CJK locales, import a `TMP_FontAsset` with the required glyph coverage. Assign it to the locale's `Default Font` and/or Font Mapping slots.

### Translator Workflow

1. Open **DIG → Localization Workstation**.
2. Go to the **Import / Export** tab.
3. Click **Export All Tables** to generate CSV files (one per string table).
4. Send CSV files to translators. Format: `Key, en-US, ja-JP, de-DE, ..., Notes`.
5. When translations arrive, use **Import CSV into Selected Table** to merge them back.
6. Check the **Coverage Heatmap** tab to verify translation completeness.

---

## 5. Localization Workstation (Editor Tooling)

Open via **DIG → Localization Workstation** from the top menu bar. The window has six tabbed modules.

### String Browser

Browse, search, and edit all string table entries in one place.
- **Search** by key or value text (case-insensitive).
- **Filter** by string table and/or locale.
- Click **Edit** on any entry to modify its value inline.
- Click **Add Entry** to create a new key in the selected (or first) table.

### Coverage Heatmap

At-a-glance view of translation completeness.
- **Rows** = string tables, **Columns** = locales.
- Color-coded cells: Green = 100%, Yellow = 50-99%, Red = <50%.
- **Overall Coverage** section shows progress bars per locale across all tables.

Use this to track which languages are ready for release.

### Missing Key Scanner

Find gaps in your translations.
- Select a locale and click **Run Scan**.
- **Missing Keys**: keys that exist in other locales but have no entry for the selected locale.
- **Orphaned Keys**: keys in string tables that aren't referenced by any `LocalizedText` component in loaded scenes (heuristic -- only checks loaded objects).

### Font Preview

Verify that fonts render correctly for each locale.
- Select a locale to see its configuration (direction, plural rules, spacing, default font).
- Preview sample text rendered at each font style size (Body, Header, Tooltip, Combat, Button, Mono).
- Test CJK, RTL, and accented character samples.

### Import / Export

Transfer data to and from translators.
- **Export Selected Table as CSV**: one CSV file for the selected `StringTableSO`.
- **Export All Tables**: batch export all tables to a chosen folder.
- **Import CSV into Selected Table**: merge translations back. Reports how many entries were added vs. updated.

CSV format: `Key, en-US, ja-JP, de-DE, ..., Notes` (first row is headers).

### Pseudo-Loc

Test your UI for localization readiness without real translations.
- Toggle **Enable Pseudo-Localization** on the database asset.
- When active, all `LocalizationManager.Get()` calls return transformed strings: `"Kill the Wolves"` becomes `"[Kkìììllll  tthhèè  WWòòllvvèèšš]"`.
- **Preview** any string to see its pseudo-localized output.
- The doubled length reveals truncation bugs. Brackets reveal hardcoded strings. Accented characters reveal font coverage gaps.

> **Remember to turn pseudo-loc off** before building or playtesting normally.

---

## 6. Locale Persistence

The player's language preference is stored in `PlayerPrefs` under the key `dig_locale`. It is **not** part of save data.

- On first launch, the system auto-detects the OS locale via `CultureInfo.CurrentUICulture` and falls back to `Application.systemLanguage`.
- If the detected locale isn't in the database, it falls back to `DefaultLocaleCode` (typically `en-US`).
- When the player selects a language in your settings UI, call `LocalizationManager.SetLocale("ja-JP")`. This saves to PlayerPrefs automatically.
- On subsequent launches, the saved preference is loaded.

---

## 7. Migrating Existing Content

Converting existing hardcoded English text to string keys is gradual and non-breaking.

### Before Localization (Current State)

A `QuestDefinitionSO` has `DisplayName = "Kill the Wolves"` as a raw English string. This works as-is because the localization system returns any unrecognized key as-is.

### After Migration

1. Change the field to a string key: `DisplayName = "quest_kill_wolves_name"`.
2. Add an entry to the Quests string table:
   - Key: `quest_kill_wolves_name`
   - Locale: `en-US`
   - Value: `Kill the Wolves`
3. The system now resolves the key to the English value. When Japanese translations are added, it resolves to Japanese for `ja-JP` players.

### Key Point: Zero Regression Risk

If a string key has no matching entry in any table, the system returns the key itself. So `"Kill the Wolves"` (a raw English string, not a key) simply displays as `"Kill the Wolves"`. Existing content continues to work unchanged until you migrate it.

---

## 8. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| All text shows raw keys or English | No `LocalizationDatabase` at `Resources/LocalizationDatabase` | Create the database asset and place it in a `Resources` folder with that exact name |
| `[MISSING:key_name]` in editor | Key not found in any string table for the active locale | Add the key to the appropriate string table with the current locale code |
| Text shows English but player selected Japanese | Missing `ja-JP` entry for that key | Add the `ja-JP` entry. The Coverage Heatmap tab shows which keys are missing |
| Font looks wrong for CJK locale | No `TMP_FontAsset` assigned for that locale | Assign a CJK font to the locale's `Default Font` or create a Font Mapping |
| Text is not RTL for Arabic | `TextDirection` not set on locale definition | Set `Text Direction = RTL` on the `Locale_ar-SA` asset |
| Locale resets every launch | PlayerPrefs not persisting | Verify `PlayerPrefs.Save()` is being called (the system does this automatically on `SetLocale`) |
| Pseudo-loc strings appear in builds | `EnablePseudoLocalization` only works in the editor | This flag is `#if UNITY_EDITOR` guarded and has no effect in builds |
| Console spam about missing keys | Keys referenced in code but not in tables | Use the Missing Key Scanner in the Workstation to identify and add them |
| Locale switch doesn't update all UI | UI component doesn't implement `ILocalizableUI` or use `LocalizedText` | Add `LocalizedText` to static labels, or implement `ILocalizableUI` and register with `LocalizationUIRegistry` for dynamic UI |
| No localization database warning on startup | Expected if you haven't created the asset yet | Create the database when ready. All text displays as raw keys until then (zero regression) |
