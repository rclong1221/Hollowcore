using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace DIG.Localization
{
    public static class LocalizationManager
    {
        private static LocalizationDatabase _database;
        private static LocaleDefinition _activeLocale;
        private static Dictionary<string, string> _activeTable;
        private static Dictionary<string, string> _fallbackTable;
        private static IPluralRule _pluralRule;
        private static FontMappingSO _fontMap;
        private static readonly HashSet<string> _loggedMissingKeys = new();

        public static bool IsInitialized { get; private set; }
        public static string CurrentLocaleCode => _activeLocale != null ? _activeLocale.LocaleCode : "en-US";
        public static LocaleDefinition CurrentLocale => _activeLocale;

        public static event Action OnLocaleChanged;

        public static void Initialize(LocalizationDatabase database)
        {
            if (database == null)
            {
                Debug.LogError("[LocalizationManager] LocalizationDatabase is null. Localization disabled.");
                return;
            }

            _database = database;
            _loggedMissingKeys.Clear();

            string savedLocale = PlayerPrefs.GetString("dig_locale", "");
            string systemLocale = DetectSystemLocale();
            string targetLocale = "";

            if (!string.IsNullOrEmpty(savedLocale) && FindLocaleDefinition(savedLocale) != null)
                targetLocale = savedLocale;
            else if (FindLocaleDefinition(systemLocale) != null)
                targetLocale = systemLocale;
            else
                targetLocale = database.DefaultLocaleCode;

            BuildFallbackTable(database.DefaultLocaleCode);
            SetLocaleInternal(targetLocale, fireEvent: false);
            IsInitialized = true;
        }

        public static bool SetLocale(string localeCode)
        {
            if (_database == null || !IsInitialized) return false;

            var def = FindLocaleDefinition(localeCode);
            if (def == null)
            {
                Debug.LogWarning($"[LocalizationManager] Locale '{localeCode}' not found in database.");
                return false;
            }

            SetLocaleInternal(localeCode, fireEvent: true);
            PlayerPrefs.SetString("dig_locale", localeCode);
            PlayerPrefs.Save();
            return true;
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            #if UNITY_EDITOR
            if (_database != null && _database.EnablePseudoLocalization)
                return GeneratePseudoLocalized(GetRaw(key));
            #endif

            return GetRaw(key);
        }

        public static string GetFormatted(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public static string GetPlural(string key, int count)
        {
            if (_pluralRule == null) return Get(key);

            var category = _pluralRule.Evaluate(count);
            string suffix = category switch
            {
                PluralCategory.One => "_one",
                PluralCategory.Few => "_few",
                PluralCategory.Many => "_many",
                _ => "_other"
            };

            string pluralKey = string.Concat(key, suffix);

            // Try direct lookup in active table first (avoids GetRaw's fallback/logging)
            if (_activeTable != null && _activeTable.TryGetValue(pluralKey, out var val))
                return val;
            if (_fallbackTable != null && _fallbackTable.TryGetValue(pluralKey, out val))
                return val;

            // Fallback to _other form (skip if we already tried _other)
            if (category != PluralCategory.Other)
            {
                string otherKey = string.Concat(key, "_other");
                if (_activeTable != null && _activeTable.TryGetValue(otherKey, out val))
                    return val;
                if (_fallbackTable != null && _fallbackTable.TryGetValue(otherKey, out val))
                    return val;
            }

            // Final fallback to the base key
            return Get(key);
        }

        public static string GetPluralFormatted(string key, int count, params object[] args)
        {
            var template = GetPlural(key, count);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public static TMP_FontAsset GetFont(FontStyle style)
        {
            TMP_FontAsset fallback = _activeLocale != null ? _activeLocale.DefaultFont : null;
            if (_fontMap != null)
                return _fontMap.GetFont(style, fallback);
            return fallback;
        }

        public static TextDirection GetTextDirection()
        {
            return _activeLocale != null ? _activeLocale.TextDirection : TextDirection.LTR;
        }

        #if UNITY_EDITOR
        public static string[] GetAllKeys()
        {
            if (_activeTable == null) return Array.Empty<string>();
            var keys = new string[_activeTable.Count];
            _activeTable.Keys.CopyTo(keys, 0);
            Array.Sort(keys, StringComparer.Ordinal);
            return keys;
        }

        public static string[] GetMissingKeys(string localeCode)
        {
            if (_database == null) return Array.Empty<string>();

            var allKeys = new HashSet<string>();
            var localeKeys = new HashSet<string>();

            foreach (var table in _database.StringTables)
            {
                if (table == null) continue;
                foreach (var entry in table.Entries)
                {
                    allKeys.Add(entry.Key);
                    if (entry.Locale == localeCode)
                        localeKeys.Add(entry.Key);
                }
            }

            allKeys.ExceptWith(localeKeys);
            var result = new string[allKeys.Count];
            allKeys.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        public static int GetCoveragePercent(string localeCode)
        {
            if (_database == null) return 0;

            var allKeys = new HashSet<string>();
            var localeKeys = new HashSet<string>();

            foreach (var table in _database.StringTables)
            {
                if (table == null) continue;
                foreach (var entry in table.Entries)
                {
                    allKeys.Add(entry.Key);
                    if (entry.Locale == localeCode && !string.IsNullOrEmpty(entry.Value))
                        localeKeys.Add(entry.Key);
                }
            }

            if (allKeys.Count == 0) return 100;
            return (int)(localeKeys.Count / (float)allKeys.Count * 100f);
        }

        public static string GeneratePseudoLocalized(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder("[");
            foreach (char c in input)
            {
                sb.Append(PseudoChar(c));
                sb.Append(PseudoChar(c));
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static char PseudoChar(char c)
        {
            return c switch
            {
                'a' => 'à', 'e' => 'è', 'i' => 'ì', 'o' => 'ò', 'u' => 'ù',
                'A' => 'À', 'E' => 'È', 'I' => 'Ì', 'O' => 'Ò', 'U' => 'Ù',
                'c' => 'ç', 'n' => 'ñ', 's' => 'š', 'y' => 'ÿ', 'z' => 'ž',
                _ => c
            };
        }
        #endif

        // ==================== INTERNAL ====================

        private static string GetRaw(string key)
        {
            if (_activeTable != null && _activeTable.TryGetValue(key, out var val))
                return val;

            if (_fallbackTable != null && _fallbackTable.TryGetValue(key, out var fallback))
                return fallback;

            #if UNITY_EDITOR
            if (!_loggedMissingKeys.Contains(key))
            {
                _loggedMissingKeys.Add(key);
                Debug.LogWarning($"[LocalizationManager] Missing key: '{key}' (locale: {CurrentLocaleCode})");
            }
            return $"[MISSING:{key}]";
            #else
            return key;
            #endif
        }

        private static void SetLocaleInternal(string localeCode, bool fireEvent)
        {
            var def = FindLocaleDefinition(localeCode);
            if (def == null) return;

            _activeLocale = def;
            _activeTable = BuildTable(localeCode);
            _pluralRule = PluralRuleFactory.Create(def.PluralRuleSet);
            _fontMap = FindFontMapping(localeCode);
            _loggedMissingKeys.Clear();

            if (fireEvent)
                OnLocaleChanged?.Invoke();
        }

        private static Dictionary<string, string> BuildTable(string localeCode)
        {
            int estimatedCapacity = EstimateEntryCount();
            var table = new Dictionary<string, string>(estimatedCapacity, StringComparer.Ordinal);
            if (_database == null) return table;

            foreach (var st in _database.StringTables)
            {
                if (st == null) continue;
                foreach (var entry in st.Entries)
                {
                    if (entry.Locale != localeCode) continue;
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    table[entry.Key] = entry.Value ?? string.Empty;
                }
            }
            return table;
        }

        private static int EstimateEntryCount()
        {
            if (_database == null) return 64;
            int total = 0;
            foreach (var st in _database.StringTables)
            {
                if (st != null) total += st.Entries.Count;
            }
            // Entries are per-locale, so divide by locale count for per-locale estimate
            int localeCount = _database.Locales.Count > 0 ? _database.Locales.Count : 1;
            return Math.Max(total / localeCount, 64);
        }

        private static void BuildFallbackTable(string fallbackLocaleCode)
        {
            _fallbackTable = new Dictionary<string, string>(EstimateEntryCount(), StringComparer.Ordinal);
            if (_database == null) return;

            foreach (var st in _database.StringTables)
            {
                if (st == null) continue;
                foreach (var entry in st.Entries)
                {
                    if (entry.Locale != fallbackLocaleCode) continue;
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    _fallbackTable[entry.Key] = entry.Value ?? string.Empty;
                }
            }
        }

        private static LocaleDefinition FindLocaleDefinition(string localeCode)
        {
            if (_database == null) return null;
            foreach (var locale in _database.Locales)
            {
                if (locale != null && locale.LocaleCode == localeCode)
                    return locale;
            }
            return null;
        }

        private static FontMappingSO FindFontMapping(string localeCode)
        {
            if (_database == null) return null;
            foreach (var fm in _database.FontMappings)
            {
                if (fm != null && fm.LocaleCode == localeCode)
                    return fm;
            }
            return null;
        }

        private static string DetectSystemLocale()
        {
            try
            {
                var culture = CultureInfo.CurrentUICulture;
                return culture.Name;
            }
            catch
            {
                return MapUnityLanguage(Application.systemLanguage);
            }
        }

        private static string MapUnityLanguage(SystemLanguage lang)
        {
            return lang switch
            {
                SystemLanguage.English => "en-US",
                SystemLanguage.Japanese => "ja-JP",
                SystemLanguage.German => "de-DE",
                SystemLanguage.French => "fr-FR",
                SystemLanguage.Spanish => "es-ES",
                SystemLanguage.Italian => "it-IT",
                SystemLanguage.Portuguese => "pt-BR",
                SystemLanguage.Russian => "ru-RU",
                SystemLanguage.Korean => "ko-KR",
                SystemLanguage.Chinese => "zh-CN",
                SystemLanguage.ChineseSimplified => "zh-CN",
                SystemLanguage.ChineseTraditional => "zh-TW",
                SystemLanguage.Arabic => "ar-SA",
                SystemLanguage.Polish => "pl-PL",
                SystemLanguage.Dutch => "nl-NL",
                SystemLanguage.Turkish => "tr-TR",
                SystemLanguage.Thai => "th-TH",
                SystemLanguage.Vietnamese => "vi-VN",
                _ => "en-US"
            };
        }

        internal static void Shutdown()
        {
            _database = null;
            _activeLocale = null;
            _activeTable = null;
            _fallbackTable = null;
            _pluralRule = null;
            _fontMap = null;
            _loggedMissingKeys.Clear();
            IsInitialized = false;
            OnLocaleChanged = null;
        }

        internal static LocalizationDatabase GetDatabase() => _database;
    }
}
