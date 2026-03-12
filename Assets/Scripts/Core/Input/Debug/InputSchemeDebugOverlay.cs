using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using DIG.Targeting;

namespace DIG.Core.Input.Debugging
{
    /// <summary>
    /// OnGUI overlay displaying input scheme state for debugging.
    /// Toggle with backtick (`) key in development builds.
    ///
    /// EPIC 15.18
    /// </summary>
    public class InputSchemeDebugOverlay : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private Key _toggleKey = Key.Backquote;

        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _activeStyle;
        private GUIStyle _inactiveStyle;

        private Entity _playerEntity;
        private EntityManager _entityManager;
        private bool _isInitialized;
        private World _cachedWorld;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
                _showOverlay = !_showOverlay;

            if (!_isInitialized)
                TryAutoInitialize();
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;

            InitStyles();

            float width = 280f;
            float height = 240f;
            float x = 10f;
            float y = Screen.height - height - 10f;

            var rect = new Rect(x, y, width, height);
            GUI.Box(rect, "", _boxStyle);

            GUILayout.BeginArea(rect);
            GUILayout.Space(5);

            GUILayout.Label("Input Scheme Debug", _headerStyle);
            GUILayout.Space(5);

            var schemeManager = InputSchemeManager.Instance;
            if (schemeManager == null)
            {
                GUILayout.Label("InputSchemeManager: NOT FOUND", _inactiveStyle);
                GUILayout.EndArea();
                return;
            }

            // Scheme state
            GUILayout.Label($"Active Scheme: {schemeManager.ActiveScheme}", _labelStyle);
            GUILayout.Label($"Cursor Free: {schemeManager.IsCursorFree}", 
                schemeManager.IsCursorFree ? _activeStyle : _inactiveStyle);
            GUILayout.Label($"Temp Cursor (Alt): {schemeManager.IsTemporaryCursorActive}", 
                schemeManager.IsTemporaryCursorActive ? _activeStyle : _inactiveStyle);

            GUILayout.Space(5);
            GUILayout.Label("Cursor State", _headerStyle);
            GUILayout.Label($"Lock: {Cursor.lockState}", _labelStyle);
            GUILayout.Label($"Visible: {Cursor.visible}", _labelStyle);

            // Hover result
            GUILayout.Space(5);
            GUILayout.Label("Hover Result", _headerStyle);

            if (_isInitialized && _entityManager.Exists(_playerEntity) && 
                _entityManager.HasComponent<CursorHoverResult>(_playerEntity))
            {
                var hover = _entityManager.GetComponentData<CursorHoverResult>(_playerEntity);
                GUILayout.Label($"Valid: {hover.IsValid}", hover.IsValid ? _activeStyle : _inactiveStyle);
                if (hover.IsValid)
                {
                    GUILayout.Label($"Entity: {hover.HoveredEntity.Index}", _labelStyle);
                    GUILayout.Label($"Category: {hover.Category}", _labelStyle);
                    GUILayout.Label($"Hit: ({hover.HitPoint.x:F1}, {hover.HitPoint.y:F1}, {hover.HitPoint.z:F1})", _labelStyle);
                }
            }
            else
            {
                GUILayout.Label("No hover data", _inactiveStyle);
            }

            GUILayout.EndArea();
        }

        private void TryAutoInitialize()
        {
            // Find client world
            _cachedWorld = null;
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.Name == "ClientWorld")
                {
                    _cachedWorld = world;
                    break;
                }
                if (world.IsCreated && world.Name == "LocalWorld" && _cachedWorld == null)
                {
                    _cachedWorld = world;
                }
            }
            if (_cachedWorld == null)
                _cachedWorld = World.DefaultGameObjectInjectionWorld;

            if (_cachedWorld == null || !_cachedWorld.IsCreated) return;

            _entityManager = _cachedWorld.EntityManager;

            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CursorHoverResult>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>());

            if (query.IsEmpty) return;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length > 0)
            {
                _playerEntity = entities[0];
                _isInitialized = true;
            }
            entities.Dispose();
        }

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f)) }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = Color.white }
            };

            _activeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 1f, 0.3f) }
            };

            _inactiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
