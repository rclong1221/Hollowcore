#if DIG_DEV_CONSOLE
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace DIG.DebugConsole.UI
{
    /// <summary>
    /// EPIC 18.9: Compact stat overlay in top-left corner.
    /// Shows FPS, frame time, entity count, memory usage.
    /// Toggle: F3 key or 'fps' console command. Updates at 2Hz.
    /// Formatted strings are cached at update frequency to avoid per-OnGUI allocations.
    /// </summary>
    public sealed class StatOverlayView : MonoBehaviour
    {
        public static bool IsVisible { get; set; }

        // Cached formatted strings (rebuilt at 2Hz, reused across OnGUI events)
        private string _fpsText = "";
        private string _entityText = "";
        private string _memoryText = "";
        private string _gcText = "";
        private float _fps;
        private float _updateTimer;
        private const float UpdateInterval = 0.5f; // 2Hz

        private GUIStyle _bgStyle;
        private GUIStyle _textStyle;
        private bool _stylesInit;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[Key.F3].wasPressedThisFrame)
                IsVisible = !IsVisible;

            if (!IsVisible) return;

            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer < UpdateInterval) return;
            _updateTimer = 0f;

            float frameTime = Time.unscaledDeltaTime * 1000f;
            _fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            long totalMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            long gcMemoryMB = System.GC.GetTotalMemory(false) / (1024 * 1024);

            // Entity count from authoritative world (no throwaway query — use UniversalQuery directly)
            int entityCount = 0;
            var world = DevConsoleService.FindAuthoritativeWorld();
            if (world != null && world.IsCreated)
                entityCount = world.EntityManager.UniversalQuery.CalculateEntityCount();

            // Cache formatted strings — reused across multiple OnGUI events until next update
            _fpsText = $"FPS: {_fps:F0}  ({frameTime:F1} ms)";
            _entityText = $"Entities: {entityCount:N0}";
            _memoryText = $"Memory: {totalMemoryMB} MB (alloc)";
            _gcText = $"GC Heap: {gcMemoryMB} MB";
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            if (!_stylesInit)
            {
                _stylesInit = true;
                _bgStyle = new GUIStyle(GUI.skin.box);
                _bgStyle.normal.background = DevConsoleView.MakeTex(1, 1, new Color(0, 0, 0, 0.7f));
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            float width = 220f;
            float lineHeight = 18f;
            float lines = 4;
            float height = lines * lineHeight + 8;
            var rect = new Rect(4, 4, width, height);

            GUI.Box(rect, GUIContent.none, _bgStyle);

            float y = rect.y + 4;
            Color fpsColor = _fps >= 55 ? Color.green : _fps >= 30 ? Color.yellow : Color.red;
            var prevColor = _textStyle.normal.textColor;

            _textStyle.normal.textColor = fpsColor;
            GUI.Label(new Rect(8, y, width, lineHeight), _fpsText, _textStyle);
            _textStyle.normal.textColor = prevColor;

            y += lineHeight;
            GUI.Label(new Rect(8, y, width, lineHeight), _entityText, _textStyle);

            y += lineHeight;
            GUI.Label(new Rect(8, y, width, lineHeight), _memoryText, _textStyle);

            y += lineHeight;
            GUI.Label(new Rect(8, y, width, lineHeight), _gcText, _textStyle);
        }
    }
}
#endif
