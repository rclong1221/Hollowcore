using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Localization
{
    [CreateAssetMenu(menuName = "DIG/Localization/String Table")]
    public class StringTableSO : ScriptableObject
    {
        [Tooltip("Unique table identifier (e.g., Dialogue, Items, Quests, Combat, UI).")]
        public string TableId;

        [Tooltip("Human-readable description for editor display.")]
        public string Description;

        public List<StringTableEntry> Entries = new();
    }

    [Serializable]
    public struct StringTableEntry
    {
        [Tooltip("Unique key within this table.")]
        public string Key;

        [Tooltip("Locale code (e.g., en-US, ja-JP, de-DE).")]
        public string Locale;

        [Tooltip("Translated text. Supports {0}, {1} positional and {PlayerName} named args.")]
        [TextArea(1, 4)]
        public string Value;

        [Tooltip("Plural form this entry applies to. Use None for non-plural strings.")]
        public PluralCategory PluralForm;

        [Tooltip("Translator context notes (max chars, tone, gender hints).")]
        public string Notes;
    }
}
