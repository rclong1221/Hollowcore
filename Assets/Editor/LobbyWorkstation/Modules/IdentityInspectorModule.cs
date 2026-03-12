using DIG.Identity;
using UnityEditor;
using UnityEngine;

namespace DIG.Lobby.Editor
{
    /// <summary>
    /// EPIC 17.14: Editor module for inspecting identity provider state,
    /// avatar cache diagnostics, and mock provider overrides.
    /// </summary>
    public class IdentityInspectorModule : ILobbyWorkstationModule
    {
        public string ModuleName => "Identity";

        private bool _showProviderFold = true;
        private bool _showAvatarFold = true;
        private bool _showConfigFold = true;
        private bool _showMockFold;

        private string _mockDisplayName = "MockPlayer";
        private string _mockPlatformId = "MOCK_12345";
        private IdentityProviderType _mockProviderType = IdentityProviderType.Steam;

        private PlatformIdentityConfigSO _cachedConfig;
        private bool _configLookedUp;
        private FriendInfo[] _cachedFriends;
        private double _friendsCacheTime;
        private const double FriendsCacheIntervalSec = 2.0;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Platform Identity Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                DrawEditModeUI();
                return;
            }

            DrawPlayModeUI();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawEditModeUI()
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see live identity state.", MessageType.Info);
            EditorGUILayout.Space(8);

            DrawConfigValidation();
            DrawMockSection();
        }

        private void DrawPlayModeUI()
        {
            var mgr = IdentityManager.Instance;
            if (mgr == null)
            {
                EditorGUILayout.HelpBox(
                    "IdentityManager not found in scene. Add an IdentityManager component to a GameObject, " +
                    "or the system will use PlayerPrefs fallback.", MessageType.Warning);
                return;
            }

            _showProviderFold = EditorGUILayout.Foldout(_showProviderFold, "Provider State", true);
            if (_showProviderFold)
            {
                EditorGUI.indentLevel++;

                var provider = mgr.ActiveProvider;
                EditorGUILayout.LabelField("State", mgr.State.ToString());
                EditorGUILayout.LabelField("Provider", provider?.ProviderType.ToString() ?? "None");
                EditorGUILayout.LabelField("Platform ID", provider?.PlatformId ?? "-");
                EditorGUILayout.LabelField("Display Name", provider?.DisplayName ?? "-");

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Capabilities", EditorStyles.miniLabel);
                if (provider != null)
                {
                    DrawCapability("Avatars", provider.SupportsAvatars);
                    DrawCapability("Friends", provider.SupportsFriends);
                    DrawCapability("Presence", provider.SupportsPresence);
                    DrawCapability("Invites", provider.SupportsInvites);
                }

                if (provider != null && provider.SupportsFriends)
                {
                    EditorGUILayout.Space(4);
                    double now = EditorApplication.timeSinceStartup;
                    if (_cachedFriends == null || now - _friendsCacheTime > FriendsCacheIntervalSec)
                    {
                        _cachedFriends = provider.GetFriends();
                        _friendsCacheTime = now;
                    }
                    var friends = _cachedFriends;
                    EditorGUILayout.LabelField($"Friends ({friends.Length})", EditorStyles.miniLabel);
                    int displayCount = Mathf.Min(friends.Length, 20);
                    for (int i = 0; i < displayCount; i++)
                    {
                        string status = friends[i].IsInGame ? " [In Game]" :
                            friends[i].IsOnline ? " [Online]" : " [Offline]";
                        EditorGUILayout.LabelField($"  {friends[i].DisplayName}{status}");
                    }
                    if (friends.Length > displayCount)
                        EditorGUILayout.LabelField($"  ... and {friends.Length - displayCount} more");
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            _showAvatarFold = EditorGUILayout.Foldout(_showAvatarFold, "Avatar Cache", true);
            if (_showAvatarFold)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Cached Entries", AvatarCache.Count.ToString());
                EditorGUILayout.LabelField("Hit Rate", $"{AvatarCache.HitRate:P1} ({AvatarCache.Hits}H / {AvatarCache.Misses}M)");

                int approxBytes = AvatarCache.Count * 64 * 64 * 4;
                string memLabel = approxBytes > 1024 * 1024
                    ? $"{approxBytes / (1024f * 1024f):F1} MB"
                    : $"{approxBytes / 1024f:F1} KB";
                EditorGUILayout.LabelField("Approx Memory", memLabel);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Clear Cache", GUILayout.Width(120)))
                    AvatarCache.Clear();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            DrawConfigValidation();
        }

        private void DrawConfigValidation()
        {
            _showConfigFold = EditorGUILayout.Foldout(_showConfigFold, "Config Validation", true);
            if (!_showConfigFold) return;

            EditorGUI.indentLevel++;

            if (!_configLookedUp)
            {
                _cachedConfig = Resources.Load<PlatformIdentityConfigSO>("PlatformIdentityConfig");
                _configLookedUp = true;
            }
            var config = _cachedConfig;
            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "No PlatformIdentityConfig.asset found in Resources/. " +
                    "Create one via DIG > Identity > Platform Identity Config.", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUILayout.LabelField("Provider", config.ActiveProviderType.ToString());
            EditorGUILayout.LabelField("Fallback", config.AllowFallback ? "Enabled" : "Disabled");
            EditorGUILayout.LabelField("Cache Size", config.AvatarCacheSize.ToString());

            DrawDefineStatus();

            if (config.ActiveProviderType == IdentityProviderType.Steam ||
                config.ActiveProviderType == IdentityProviderType.Auto)
            {
                if (config.SteamAppId == 0)
                    EditorGUILayout.HelpBox("Steam App ID is 0. Steam provider will fail to initialize.", MessageType.Warning);
            }

            if (config.ActiveProviderType == IdentityProviderType.Epic ||
                config.ActiveProviderType == IdentityProviderType.Auto)
            {
                if (string.IsNullOrEmpty(config.EpicProductId) || string.IsNullOrEmpty(config.EpicClientId))
                    EditorGUILayout.HelpBox("EOS credentials are incomplete. Epic provider will fail.", MessageType.Warning);
            }

            if (config.ActiveProviderType == IdentityProviderType.GOG ||
                config.ActiveProviderType == IdentityProviderType.Auto)
            {
                if (string.IsNullOrEmpty(config.GogClientId))
                    EditorGUILayout.HelpBox("GOG Client ID is empty. GOG provider will fail.", MessageType.Warning);
            }

            if (config.ActiveProviderType == IdentityProviderType.Discord ||
                config.ActiveProviderType == IdentityProviderType.Auto)
            {
                if (config.DiscordApplicationId == 0)
                    EditorGUILayout.HelpBox("Discord Application ID is 0. Discord provider will fail.", MessageType.Warning);
            }

            if (config.DefaultAvatarSprite == null)
                EditorGUILayout.HelpBox("Default Avatar Sprite not assigned. Slots will show nothing when no avatar is available.", MessageType.Info);

            if (config.AvatarCacheSize > 64)
                EditorGUILayout.HelpBox($"Avatar cache size {config.AvatarCacheSize} is large. ~{config.AvatarCacheSize * 16}KB uncompressed at Medium.", MessageType.Info);

            EditorGUI.indentLevel--;
        }

        private void DrawDefineStatus()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Scripting Defines Active:", EditorStyles.miniLabel);

#if HAS_STEAMWORKS
            EditorGUILayout.LabelField("  HAS_STEAMWORKS", "YES");
#else
            EditorGUILayout.LabelField("  HAS_STEAMWORKS", "no");
#endif
#if HAS_EOS
            EditorGUILayout.LabelField("  HAS_EOS", "YES");
#else
            EditorGUILayout.LabelField("  HAS_EOS", "no");
#endif
#if HAS_GOG_GALAXY
            EditorGUILayout.LabelField("  HAS_GOG_GALAXY", "YES");
#else
            EditorGUILayout.LabelField("  HAS_GOG_GALAXY", "no");
#endif
#if HAS_DISCORD_SDK
            EditorGUILayout.LabelField("  HAS_DISCORD_SDK", "YES");
#else
            EditorGUILayout.LabelField("  HAS_DISCORD_SDK", "no");
#endif
            EditorGUILayout.Space(4);
        }

        private void DrawMockSection()
        {
            _showMockFold = EditorGUILayout.Foldout(_showMockFold, "Mock Provider (Testing)", true);
            if (!_showMockFold) return;

            EditorGUI.indentLevel++;

            _mockProviderType = (IdentityProviderType)EditorGUILayout.EnumPopup("Provider Type", _mockProviderType);
            _mockDisplayName = EditorGUILayout.TextField("Display Name", _mockDisplayName);
            _mockPlatformId = EditorGUILayout.TextField("Platform ID", _mockPlatformId);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Mock values are for editor preview only. In Play Mode, the real IdentityManager controls identity.\n" +
                "To test fallback behavior, set provider type to one without the SDK installed and enable AllowFallback.",
                MessageType.Info);

            EditorGUI.indentLevel--;
        }

        private static void DrawCapability(string label, bool supported)
        {
            EditorGUILayout.LabelField($"  {label}", supported ? "Supported" : "Not supported");
        }
    }
}
