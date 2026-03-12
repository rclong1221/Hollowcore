#if DIG_DEV_CONSOLE
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DIG.DebugConsole
{
    /// <summary>
    /// EPIC 18.9: Persistent command history with Up/Down navigation.
    /// History is saved to Application.persistentDataPath between sessions.
    /// </summary>
    public sealed class CommandHistory
    {
        private const int MaxHistory = 128;
        private const string FileName = "dev_console_history.txt";

        private readonly List<string> _entries = new(MaxHistory);
        private int _cursor;
        private string _savedInput;

        public int Count => _entries.Count;

        public void Load()
        {
            string path = GetPath();
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path);
            _entries.Clear();
            int start = lines.Length > MaxHistory ? lines.Length - MaxHistory : 0;
            for (int i = start; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    _entries.Add(lines[i]);
            }
            _cursor = _entries.Count;
        }

        public void Save()
        {
            try { File.WriteAllLines(GetPath(), _entries); }
            catch (System.Exception e) { Debug.LogWarning($"[DevConsole] History save failed: {e.Message}"); }
        }

        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            // Deduplicate consecutive
            if (_entries.Count > 0 && _entries[_entries.Count - 1] == command) { ResetCursor(); return; }

            _entries.Add(command);
            if (_entries.Count > MaxHistory)
                _entries.RemoveAt(0);
            ResetCursor();
        }

        public void ResetCursor()
        {
            _cursor = _entries.Count;
            _savedInput = null;
        }

        /// <summary>Navigate Up. Returns previous command or current input if at top.</summary>
        public string NavigateUp(string currentInput)
        {
            if (_entries.Count == 0) return currentInput;
            if (_cursor == _entries.Count) _savedInput = currentInput;
            if (_cursor > 0) _cursor--;
            return _entries[_cursor];
        }

        /// <summary>Navigate Down. Returns next command or saved input if at bottom.</summary>
        public string NavigateDown(string currentInput)
        {
            if (_cursor >= _entries.Count - 1)
            {
                _cursor = _entries.Count;
                return _savedInput ?? "";
            }
            _cursor++;
            return _entries[_cursor];
        }

        private static string GetPath() => Path.Combine(Application.persistentDataPath, FileName);
    }
}
#endif
