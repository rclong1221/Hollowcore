#if DIG_DEV_CONSOLE
using UnityEngine;
using UnityEngine.InputSystem;

namespace DIG.DebugConsole.UI
{
    /// <summary>
    /// EPIC 18.9: IMGUI overlay for the developer console.
    /// Toggle with backtick (`). Renders output log, input field, and autocomplete.
    /// Consumes keyboard input when visible to prevent game input passthrough.
    /// </summary>
    public sealed class DevConsoleView : MonoBehaviour
    {
        public bool IsVisible { get; private set; }

        private string _inputText = "";
        private Vector2 _scrollPos;
        private bool _focusInput;
        private bool _scrollToBottom;
        private readonly AutoCompleteDropdown _autocomplete = new();

        // Layout constants
        private const float ConsoleHeightRatio = 0.4f;
        private const float TitleBarHeight = 22f;
        private const float InputHeight = 25f;
        private const float Padding = 4f;
        private const float LineHeight = 16f;

        // Styles (lazy init)
        private GUIStyle _logStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bgStyle;
        private bool _stylesInitialized;

        private const string InputControlName = "DevConsoleInput";

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[Key.Backquote].wasPressedThisFrame)
            {
                IsVisible = !IsVisible;
                if (IsVisible)
                {
                    _focusInput = true;
                    _scrollToBottom = true;
                }
            }
        }

        private void OnGUI()
        {
            if (!IsVisible || DevConsoleService.Instance == null) return;

            EnsureStyles();
            _autocomplete.EnsureStyles();

            // Handle keyboard events: let HandleInputEvents process specific keys,
            // then consume remaining key events to block game input passthrough.
            if (Event.current.type == EventType.KeyDown)
            {
                // HandleInputEvents will Use() recognized keys (Enter, Up, Down, Tab, Esc).
                // For all other keys, let the TextField consume them naturally.
            }

            float consoleHeight = Screen.height * ConsoleHeightRatio;
            var consoleRect = new Rect(0, 0, Screen.width, consoleHeight);

            // Background
            GUI.Box(consoleRect, GUIContent.none, _bgStyle);

            // Title bar
            var titleRect = new Rect(Padding, 2, Screen.width - Padding * 2, TitleBarHeight);
            GUI.Label(titleRect, "DIG Dev Console", _titleStyle);

            // Close button
            var closeRect = new Rect(Screen.width - 60, 2, 56, TitleBarHeight);
            if (GUI.Button(closeRect, "Close")) IsVisible = false;

            // Log area
            float logTop = TitleBarHeight + 2;
            float logHeight = consoleHeight - logTop - InputHeight - Padding * 2;
            var logOuterRect = new Rect(Padding, logTop, Screen.width - Padding * 2, logHeight);

            var service = DevConsoleService.Instance;
            float contentHeight = service.OutputCount * LineHeight;
            var viewRect = new Rect(0, 0, logOuterRect.width - 20, Mathf.Max(contentHeight, logHeight));

            if (_scrollToBottom)
            {
                _scrollPos.y = Mathf.Max(0, contentHeight - logHeight);
                _scrollToBottom = false;
            }

            // Visible-range culling: only render lines within the scroll viewport
            _scrollPos = GUI.BeginScrollView(logOuterRect, _scrollPos, viewRect);
            int firstVisible = Mathf.Max(0, Mathf.FloorToInt(_scrollPos.y / LineHeight) - 1);
            int lastVisible = Mathf.Min(service.OutputCount - 1,
                Mathf.CeilToInt((_scrollPos.y + logHeight) / LineHeight) + 1);
            for (int i = firstVisible; i <= lastVisible; i++)
            {
                var entry = service.GetOutput(i);
                var lineRect = new Rect(4, i * LineHeight, viewRect.width - 8, LineHeight);
                var style = entry.Type switch
                {
                    LogType.Warning => _warningStyle,
                    LogType.Error or LogType.Exception or LogType.Assert => _errorStyle,
                    _ => _logStyle
                };
                GUI.Label(lineRect, entry.Text, style);
            }
            GUI.EndScrollView();

            // Input field
            var inputRect = new Rect(Padding, consoleHeight - InputHeight - Padding, Screen.width - Padding * 2, InputHeight);

            HandleInputEvents(ref inputRect);

            GUI.SetNextControlName(InputControlName);
            _inputText = GUI.TextField(inputRect, _inputText, _inputStyle);

            if (_focusInput)
            {
                GUI.FocusControl(InputControlName);
                _focusInput = false;
            }

            // Autocomplete (only recompute when input changes)
            _autocomplete.UpdateIfChanged(_inputText);
            _autocomplete.Draw(inputRect);

            // Block game input: consume any remaining keyboard events inside the console rect
            // so they don't propagate to game systems (Input System reads from OS, but
            // legacy Input.GetKey and UI EventSystem check Event.current).
            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp)
            {
                if (Event.current.keyCode != KeyCode.BackQuote)
                    Event.current.Use();
            }
        }

        private void HandleInputEvents(ref Rect inputRect)
        {
            if (!Event.current.isKey || Event.current.type != EventType.KeyDown) return;

            switch (Event.current.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // Check autocomplete selection first
                    var selected = _autocomplete.GetSelected();
                    if (selected != null)
                    {
                        _inputText = selected + " ";
                        _autocomplete.Invalidate();
                    }
                    else if (!string.IsNullOrWhiteSpace(_inputText))
                    {
                        DevConsoleService.Instance.Execute(_inputText);
                        _inputText = "";
                        _scrollToBottom = true;
                    }
                    Event.current.Use();
                    break;

                case KeyCode.UpArrow:
                    if (_autocomplete.HasSuggestions)
                        _autocomplete.MoveUp();
                    else
                        _inputText = DevConsoleService.Instance.History.NavigateUp(_inputText);
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    if (_autocomplete.HasSuggestions)
                        _autocomplete.MoveDown();
                    else
                        _inputText = DevConsoleService.Instance.History.NavigateDown(_inputText);
                    Event.current.Use();
                    break;

                case KeyCode.Tab:
                    if (_autocomplete.HasSuggestions)
                    {
                        var tabSelected = _autocomplete.GetSelected();
                        if (tabSelected != null)
                            _inputText = tabSelected + " ";
                        else if (_autocomplete.Count > 0)
                        {
                            _autocomplete.MoveDown();
                            tabSelected = _autocomplete.GetSelected();
                            if (tabSelected != null) _inputText = tabSelected + " ";
                        }
                    }
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    if (_autocomplete.HasSuggestions)
                        _autocomplete.Invalidate();
                    else
                        IsVisible = false;
                    Event.current.Use();
                    break;
            }
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _bgStyle = new GUIStyle(GUI.skin.box);
            _bgStyle.normal.background = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.12f, 0.95f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                normal = { textColor = new Color(0.9f, 0.7f, 0.2f) }
            };

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = false,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            _warningStyle = new GUIStyle(_logStyle) { normal = { textColor = new Color(1f, 0.9f, 0.3f) } };
            _errorStyle = new GUIStyle(_logStyle) { normal = { textColor = new Color(1f, 0.35f, 0.3f) } };

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
        }

        internal static Texture2D MakeTex(int width, int height, Color col)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
#endif
