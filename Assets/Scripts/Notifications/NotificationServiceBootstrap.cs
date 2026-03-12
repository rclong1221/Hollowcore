using DIG.Notifications.Channels;
using DIG.Notifications.Config;
using DIG.UI.Core.Navigation;
using DIG.UI.Core.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Notifications
{
    /// <summary>
    /// EPIC 18.3: Bootstrap MonoBehaviour that initializes the notification service.
    /// Place on a persistent GameObject alongside UIServiceBootstrap.
    /// Creates NotificationService, loads config, injects containers into UIToolkitService layers.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class NotificationServiceBootstrap : MonoBehaviour
    {
        private static NotificationServiceBootstrap _instance;

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
            // Delay to Start so UIServiceBootstrap.Awake (-300) has already run
            Initialize();
        }

        private void Initialize()
        {
            // Load config
            var config = Resources.Load<NotificationConfigSO>("NotificationConfig");
            if (config == null)
            {
                Debug.LogWarning("[NotificationServiceBootstrap] No NotificationConfig found in Resources. " +
                    "Create one via Assets > Create > DIG > Notifications > Config and place it in a Resources/ folder.");
                config = ScriptableObject.CreateInstance<NotificationConfigSO>();
            }

            // Get UIToolkitService for layer injection
            var uiService = UIServices.Screen as UIToolkitService;
            if (uiService == null)
            {
                Debug.LogError("[NotificationServiceBootstrap] UIServices.Screen is not initialized or is not UIToolkitService. " +
                    "Ensure UIServiceBootstrap runs before this bootstrap (execution order -300 vs -250).");
                return;
            }

            // Load UXML templates
            var toastTemplate = Resources.Load<VisualTreeAsset>("UI/Notifications/Toast");
            var bannerTemplate = Resources.Load<VisualTreeAsset>("UI/Notifications/Banner");
            var centerTemplate = Resources.Load<VisualTreeAsset>("UI/Notifications/CenterScreen");

            if (toastTemplate == null) Debug.LogWarning("[NotificationServiceBootstrap] Toast.uxml not found in Resources/UI/Notifications/");
            if (bannerTemplate == null) Debug.LogWarning("[NotificationServiceBootstrap] Banner.uxml not found in Resources/UI/Notifications/");
            if (centerTemplate == null) Debug.LogWarning("[NotificationServiceBootstrap] CenterScreen.uxml not found in Resources/UI/Notifications/");

            // Load stylesheet
            var notifStyles = Resources.Load<StyleSheet>("UI/Notifications/Notifications");

            // Create channels
            var toastChannel = new ToastChannel(config.ToastConfig, toastTemplate);
            var bannerChannel = new BannerChannel(config.BannerConfig, bannerTemplate);
            var centerChannel = new CenterScreenChannel(config.CenterConfig, centerTemplate);

            // Inject into UIToolkitService layers
            // Toast + Banner → Tooltip layer (topmost, non-blocking)
            var tooltipLayer = uiService.GetLayerRoot(UILayer.Tooltip);
            // CenterScreen → Modal layer
            var modalLayer = uiService.GetLayerRoot(UILayer.Modal);

            // Apply stylesheet to layer roots so notification elements inherit styles
            if (notifStyles != null)
            {
                tooltipLayer.styleSheets.Add(notifStyles);
                modalLayer.styleSheets.Add(notifStyles);
            }

            toastChannel.AttachTo(tooltipLayer);
            bannerChannel.AttachTo(tooltipLayer);
            centerChannel.AttachTo(modalLayer);

            // Create service
            var serviceGO = new GameObject("NotificationService");
            serviceGO.transform.SetParent(transform);
            var service = serviceGO.AddComponent<NotificationService>();
            service.Initialize(config, toastChannel, bannerChannel, centerChannel);

            Debug.Log("[NotificationServiceBootstrap] Notification service initialized.");
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
