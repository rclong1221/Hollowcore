using UnityEngine;

namespace DIG.Identity
{
    [CreateAssetMenu(menuName = "DIG/Identity/Platform Identity Config")]
    public class PlatformIdentityConfigSO : ScriptableObject
    {
        [Header("Provider Selection")]
        public IdentityProviderType ActiveProviderType = IdentityProviderType.Auto;
        public bool AllowFallback = true;

        [Header("Avatar")]
        [Range(8, 128)]
        public int AvatarCacheSize = 32;
        public AvatarSize AvatarResolution = AvatarSize.Medium;
        public Sprite DefaultAvatarSprite;

        [Header("Steam")]
        public uint SteamAppId;

        [Header("Epic Online Services")]
        public string EpicProductId = "";
        public string EpicSandboxId = "";
        public string EpicDeploymentId = "";
        public string EpicClientId = "";
        public string EpicClientSecret = "";

        [Header("GOG Galaxy")]
        public string GogClientId = "";
        public string GogClientSecret = "";

        [Header("Discord")]
        public long DiscordApplicationId;

        [Header("Rich Presence")]
        public bool RichPresenceEnabled = true;
        public string RichPresenceInLobby = "In Lobby ({0}/{1})";
        public string RichPresenceInGame = "In Game - {0}";
    }
}
