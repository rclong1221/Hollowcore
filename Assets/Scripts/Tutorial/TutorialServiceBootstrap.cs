using DIG.Tutorial.Config;
using DIG.Tutorial.UI;
using DIG.UI.Core.Navigation;
using DIG.UI.Core.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Tutorial
{
    /// <summary>
    /// EPIC 18.4: Bootstrap MonoBehaviour that initializes the tutorial service.
    /// Place on a persistent GameObject alongside UIServiceBootstrap.
    /// Follows NotificationServiceBootstrap pattern exactly.
    /// </summary>
    [DefaultExecutionOrder(-240)]
    public class TutorialServiceBootstrap : MonoBehaviour
    {
        private static TutorialServiceBootstrap _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Load config
            var config = Resources.Load<TutorialConfigSO>("TutorialConfig");
            if (config == null)
            {
                Debug.LogWarning("[TutorialServiceBootstrap] No TutorialConfig found in Resources. " +
                    "Create one via Assets > Create > DIG > Tutorial > Config and place it in a Resources/ folder.");
                config = ScriptableObject.CreateInstance<TutorialConfigSO>();
            }

            // Get UIToolkitService for layer injection
            var uiService = UIServices.Screen as UIToolkitService;
            if (uiService == null)
            {
                Debug.LogError("[TutorialServiceBootstrap] UIServices.Screen is not initialized or is not UIToolkitService. " +
                    "Ensure UIServiceBootstrap runs before this bootstrap (execution order -300 vs -240).");
                return;
            }

            // Load UXML template and stylesheet
            var tutorialTemplate = Resources.Load<VisualTreeAsset>("UI/Tutorial/Tutorial");
            var tutorialStyles = Resources.Load<StyleSheet>("UI/Tutorial/Tutorial");

            if (tutorialTemplate == null)
                Debug.LogWarning("[TutorialServiceBootstrap] Tutorial.uxml not found in Resources/UI/Tutorial/");

            // Inject into Modal layer (overlay above gameplay)
            var modalLayer = uiService.GetLayerRoot(UILayer.Modal);

            if (tutorialStyles != null)
                modalLayer.styleSheets.Add(tutorialStyles);

            // Create overlay controller
            var overlay = new TutorialOverlayController(modalLayer, tutorialTemplate, config);

            // Create service
            var serviceGO = new GameObject("TutorialService");
            serviceGO.transform.SetParent(transform);
            var service = serviceGO.AddComponent<TutorialService>();
            service.Initialize(config, overlay);

            Debug.Log("[TutorialServiceBootstrap] Tutorial service initialized.");
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
