using System;
using System.Threading.Tasks;
using UnityEngine;

namespace DIG.Identity
{
    /// <summary>
    /// EPIC 17.14: Singleton MonoBehaviour managing the active identity provider.
    /// DontDestroyOnLoad. Script execution order: -200 (before LobbyManager).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class IdentityManager : MonoBehaviour
    {
        private static IdentityManager _instance;
        public static IdentityManager Instance => _instance;

        public IIdentityProvider ActiveProvider { get; private set; }
        public IdentityState State { get; private set; }
        public bool IsReady => State == IdentityState.Ready;

        public event Action OnIdentityReady;
        public event Action<string> OnIdentityFailed;
        public event Action<string, Texture2D> OnAvatarLoaded;

        private PlatformIdentityConfigSO _config;
        public PlatformIdentityConfigSO Config => _config;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _config = Resources.Load<PlatformIdentityConfigSO>("PlatformIdentityConfig");
            if (_config == null)
                Debug.LogWarning("[Identity] No PlatformIdentityConfig found in Resources/. Using Local provider.");
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            State = IdentityState.Initializing;

            var type = _config != null ? _config.ActiveProviderType : IdentityProviderType.Local;
            if (type == IdentityProviderType.Auto)
                type = DetectAvailablePlatform();

            ActiveProvider = CreateProvider(type);
            bool success = false;

            try
            {
                success = await ActiveProvider.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Identity] {type} init threw: {e.Message}");
            }

            bool allowFallback = _config == null || _config.AllowFallback;
            if (!success && allowFallback && type != IdentityProviderType.Local)
            {
                Debug.LogWarning($"[Identity] {type} failed, falling back to Local");
                ActiveProvider.Shutdown();
                ActiveProvider = CreateProvider(IdentityProviderType.Local);
                success = await ActiveProvider.InitializeAsync();
            }

            if (success)
            {
                PopulatePlayerIdentity();
                State = IdentityState.Ready;
                OnIdentityReady?.Invoke();
                Debug.Log($"[Identity] Ready via {ActiveProvider.ProviderType}: {ActiveProvider.DisplayName} ({ActiveProvider.PlatformId})");
            }
            else
            {
                State = IdentityState.Failed;
                OnIdentityFailed?.Invoke("All identity providers failed");
                Debug.LogError("[Identity] All providers failed. Identity unavailable.");
            }
        }

        private void Update()
        {
            ActiveProvider?.Tick();
        }

        private void OnDestroy()
        {
            ActiveProvider?.Shutdown();
            AvatarCache.Clear();
            if (_instance == this) _instance = null;
        }

        public async void LoadAvatarForPlayer(string platformId, AvatarSize size = AvatarSize.Medium)
        {
            if (ActiveProvider == null || !ActiveProvider.SupportsAvatars) return;
            try
            {
                var tex = await AvatarCache.GetOrLoadAsync(ActiveProvider, platformId, size);
                if (tex != null)
                    OnAvatarLoaded?.Invoke(platformId, tex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Identity] Avatar load failed for {platformId}: {e.Message}");
            }
        }

        private void PopulatePlayerIdentity()
        {
            var identity = DIG.Lobby.PlayerIdentity.Local;
            identity.PlayerId = ActiveProvider.PlatformId;
            identity.DisplayName = ActiveProvider.DisplayName;
            identity.Save();
        }

        private static IdentityProviderType DetectAvailablePlatform()
        {
#if HAS_STEAMWORKS
            return IdentityProviderType.Steam;
#elif HAS_EOS
            return IdentityProviderType.Epic;
#elif HAS_GOG_GALAXY
            return IdentityProviderType.GOG;
#elif HAS_DISCORD_SDK
            return IdentityProviderType.Discord;
#else
            return IdentityProviderType.Local;
#endif
        }

        private IIdentityProvider CreateProvider(IdentityProviderType type)
        {
            switch (type)
            {
#if HAS_STEAMWORKS
                case IdentityProviderType.Steam:
                    return new Providers.SteamIdentityProvider(_config);
#endif
#if HAS_EOS
                case IdentityProviderType.Epic:
                    return new Providers.EpicIdentityProvider(_config);
#endif
#if HAS_GOG_GALAXY
                case IdentityProviderType.GOG:
                    return new Providers.GogIdentityProvider(_config);
#endif
#if HAS_DISCORD_SDK
                case IdentityProviderType.Discord:
                    return new Providers.DiscordIdentityProvider(_config);
#endif
                default:
                    return new Providers.LocalIdentityProvider();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }
    }
}
