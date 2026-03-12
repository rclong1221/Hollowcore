#if DIG_DEV_CONSOLE
using System;
using System.Collections.Generic;

namespace DIG.DebugConsole
{
    /// <summary>
    /// EPIC 18.9: Parsed argument wrapper for console commands.
    /// Provides typed accessors for positional args and named flags.
    /// </summary>
    public sealed class ConCommandArgs
    {
        public string RawInput { get; }
        public string CommandName { get; }
        private readonly string[] _positional;
        private readonly Dictionary<string, string> _flags;

        public int Count => _positional.Length;

        public ConCommandArgs(string rawInput, string commandName, string[] positional, Dictionary<string, string> flags)
        {
            RawInput = rawInput;
            CommandName = commandName;
            _positional = positional;
            _flags = flags;
        }

        public string GetString(int index, string defaultValue = "")
        {
            return index >= 0 && index < _positional.Length ? _positional[index] : defaultValue;
        }

        public int GetInt(int index, int defaultValue = 0)
        {
            if (index < 0 || index >= _positional.Length) return defaultValue;
            return int.TryParse(_positional[index], out int v) ? v : defaultValue;
        }

        public float GetFloat(int index, float defaultValue = 0f)
        {
            if (index < 0 || index >= _positional.Length) return defaultValue;
            return float.TryParse(_positional[index], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : defaultValue;
        }

        public bool GetBool(int index, bool defaultValue = false)
        {
            if (index < 0 || index >= _positional.Length) return defaultValue;
            var s = _positional[index].ToLowerInvariant();
            if (s == "1" || s == "true" || s == "on" || s == "yes") return true;
            if (s == "0" || s == "false" || s == "off" || s == "no") return false;
            return defaultValue;
        }

        public T GetEnum<T>(int index, T defaultValue = default) where T : struct, Enum
        {
            if (index < 0 || index >= _positional.Length) return defaultValue;
            return Enum.TryParse<T>(_positional[index], true, out var v) ? v : defaultValue;
        }

        public bool HasFlag(string flagName)
        {
            return _flags.ContainsKey(flagName);
        }

        public string GetFlag(string flagName, string defaultValue = "")
        {
            return _flags.TryGetValue(flagName, out var v) ? v : defaultValue;
        }
    }
}
#endif
