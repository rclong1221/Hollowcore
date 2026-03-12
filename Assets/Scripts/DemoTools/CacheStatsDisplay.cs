#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Player.Systems;
using Unity.Entities;

namespace DIG.DemoTools
{
    /// <summary>
    /// Simple runtime debug overlay that shows capsule cache stats.
    /// Attach to any GameObject to see live hits/misses in the Game view.
    /// </summary>
    public class CacheStatsDisplay : MonoBehaviour
    {
        public Vector2 position = new Vector2(10, 10);
        public int fontSize = 14;
        public Color textColor = Color.white;
        public bool showInEditor = false;
        public bool showButtons = true;

        GUIStyle _style;
        CharacterControllerSystem _cachedSystem;

        void Awake()
        {
            _style = new GUIStyle();
            _style.fontSize = fontSize;
            _style.normal.textColor = textColor;
        }

        CharacterControllerSystem GetSystem()
        {
            if (_cachedSystem == null || !_cachedSystem.World.IsCreated)
            {
                _cachedSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CharacterControllerSystem>();
            }
            return _cachedSystem;
        }

        void OnGUI()
        {
            if (!Application.isPlaying && !showInEditor)
                return;

            var system = GetSystem();
            if (system == null)
            {
                GUI.Label(new Rect(position.x, position.y, 300, 100), "CharacterControllerSystem not found", _style);
                return;
            }

            var stats = system.GetCacheStats();
            long hits = stats.hits;
            long misses = stats.misses;
            float rate = (hits + misses) > 0 ? (float)hits / (hits + misses) : 0f;

            string text = $"Capsule Cache\nHits: {hits}\nMisses: {misses}\nHit Rate: {rate:P1}";
            Rect r = new Rect(position.x, position.y, 300, 100);
            if (_style == null)
            {
                _style = new GUIStyle() { fontSize = fontSize };
                _style.normal.textColor = textColor;
            }
            GUI.Label(r, text, _style);

            if (showButtons)
            {
                Rect bRect = new Rect(position.x, position.y + 100, 300, 22);
                if (GUI.Button(bRect, "Reset Counters"))
                {
                    system.ResetCacheStats();
                }

                Rect bRect2 = new Rect(position.x + 104, position.y + 100, 96, 22);
                if (GUI.Button(bRect2, "Clear Cache"))
                {
                    system.ClearCapsuleCache();
                }

                Rect bRect3 = new Rect(position.x + 204, position.y + 100, 96, 22);
                if (GUI.Button(bRect3, "Reset All"))
                {
                    system.ResetCache(true);
                }
            }
        }
    }
}
#endif
