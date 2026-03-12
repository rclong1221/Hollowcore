#if DIG_DEV_CONSOLE
using System.Collections.Generic;
using UnityEngine;

namespace DIG.DebugConsole.UI
{
    /// <summary>
    /// EPIC 18.9: IMGUI autocomplete dropdown rendered below the input field.
    /// Prefix-matches against registered command names.
    /// Caches results — only recomputes when input text changes.
    /// </summary>
    public sealed class AutoCompleteDropdown
    {
        private readonly List<DevConsoleService.CommandEntry> _suggestions = new(16);
        private int _selectedIndex = -1;
        private string _lastInput;

        public int Count => _suggestions.Count;
        public bool HasSuggestions => _suggestions.Count > 0;

        /// <summary>Only recompute if input has changed since last call.</summary>
        public void UpdateIfChanged(string input)
        {
            if (input == _lastInput) return;
            _lastInput = input;
            Recompute(input);
        }

        /// <summary>Force clear cached state (e.g., after accepting a suggestion).</summary>
        public void Invalidate()
        {
            _lastInput = null;
            _suggestions.Clear();
            _selectedIndex = -1;
        }

        private void Recompute(string input)
        {
            _suggestions.Clear();
            _selectedIndex = -1;

            if (string.IsNullOrWhiteSpace(input) || DevConsoleService.Instance == null) return;

            string prefix = input.Trim().ToLowerInvariant();
            // Only autocomplete first token (no space yet)
            if (prefix.Contains(' ')) return;

            foreach (var cmd in DevConsoleService.Instance.Commands.Values)
            {
                if (cmd.Name.StartsWith(prefix) && cmd.Name != prefix)
                    _suggestions.Add(cmd);
            }

            _suggestions.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal));
            if (_suggestions.Count > 8) _suggestions.RemoveRange(8, _suggestions.Count - 8);
        }

        public void MoveUp()
        {
            if (_suggestions.Count == 0) return;
            _selectedIndex = _selectedIndex <= 0 ? _suggestions.Count - 1 : _selectedIndex - 1;
        }

        public void MoveDown()
        {
            if (_suggestions.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _suggestions.Count;
        }

        /// <summary>Returns selected suggestion name, or null if none selected.</summary>
        public string GetSelected()
        {
            return _selectedIndex >= 0 && _selectedIndex < _suggestions.Count
                ? _suggestions[_selectedIndex].Name
                : null;
        }

        public void Draw(Rect inputRect)
        {
            if (_suggestions.Count == 0) return;

            float itemHeight = 20f;
            float dropdownHeight = _suggestions.Count * itemHeight;
            var dropdownRect = new Rect(inputRect.x, inputRect.y + inputRect.height, inputRect.width, dropdownHeight);

            GUI.Box(dropdownRect, GUIContent.none);

            for (int i = 0; i < _suggestions.Count; i++)
            {
                var itemRect = new Rect(dropdownRect.x, dropdownRect.y + i * itemHeight, dropdownRect.width, itemHeight);
                bool isSelected = i == _selectedIndex;

                if (isSelected)
                    GUI.DrawTexture(itemRect, Texture2D.grayTexture);

                GUI.Label(new Rect(itemRect.x + 4, itemRect.y, 120, itemHeight), _suggestions[i].Name,
                    isSelected ? _boldStyle : _normalStyle);
                GUI.Label(new Rect(itemRect.x + 130, itemRect.y, itemRect.width - 134, itemHeight),
                    _suggestions[i].Description, _hintStyle);
            }
        }

        // Lazy-init styles (IMGUI styles must be created during OnGUI)
        private GUIStyle _normalStyle;
        private GUIStyle _boldStyle;
        private GUIStyle _hintStyle;

        public void EnsureStyles()
        {
            if (_normalStyle != null) return;
            _normalStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _boldStyle = new GUIStyle(_normalStyle) { fontStyle = FontStyle.Bold };
            _hintStyle = new GUIStyle(_normalStyle)
            {
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                fontSize = 11
            };
        }
    }
}
#endif
