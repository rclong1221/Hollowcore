using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Bootstrap MonoBehaviour that initializes the UI service layer.
    /// Place on a GameObject in the startup scene or let it auto-create.
    /// Loads ScreenManifestSO from Resources, creates UIToolkitService,
    /// applies default theme, and sets UIServices.Screen.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class UIServiceBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Override manifest. If null, loads from Resources/ScreenManifest.")]
        [SerializeField] private ScreenManifestSO _manifestOverride;

        [Tooltip("Override UIDocument. If null, creates one automatically.")]
        [SerializeField] private UIDocument _uiDocumentOverride;

        private static UIServiceBootstrap _instance;
        private UIToolkitService _service;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Initialize()
        {
            // Load manifest
            var manifest = _manifestOverride;
            if (manifest == null)
            {
                manifest = Resources.Load<ScreenManifestSO>("ScreenManifest");
            }
            if (manifest == null)
            {
                Debug.LogWarning("[UIServiceBootstrap] No ScreenManifestSO found. UI service will have no registered screens. " +
                    "Create one via Assets > Create > DIG/UI/Screen Manifest and place it in a Resources/ folder.");
                // Create an empty manifest so the service can still function
                manifest = ScriptableObject.CreateInstance<ScreenManifestSO>();
            }

            // Get or create UIDocument
            var uiDoc = _uiDocumentOverride;
            if (uiDoc == null)
            {
                uiDoc = GetComponent<UIDocument>();
            }
            if (uiDoc == null)
            {
                uiDoc = gameObject.AddComponent<UIDocument>();
                uiDoc.sortingOrder = 100; // Above most other UIDocuments
            }

            // Ensure the UIDocument has a root element
            var hostRoot = uiDoc.rootVisualElement;
            if (hostRoot == null)
            {
                Debug.LogError("[UIServiceBootstrap] UIDocument has no rootVisualElement. Cannot initialize UI service.");
                return;
            }

            // Create the service
            _service = new UIToolkitService(manifest, hostRoot);

            // Apply default theme
            if (manifest.DefaultTheme != null)
            {
                _service.SetTheme(manifest.DefaultTheme);
            }

            // Register as the global service
            UIServices.Screen = _service;

            Debug.Log($"[UIServiceBootstrap] UI service initialized. {manifest.AllScreens.Count} screens registered.");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                UIServices.Screen = null;
                _instance = null;
            }
        }
    }
}
