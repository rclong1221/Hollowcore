using System;
using System.Threading.Tasks;
using UnityEngine;

namespace DIG.Identity.Providers
{
    /// <summary>
    /// EPIC 17.14: Fallback provider using PlayerPrefs GUID + display name.
    /// Always available, zero platform dependencies.
    /// </summary>
    public class LocalIdentityProvider : IIdentityProvider
    {
        private const string PlayerIdKey = "DIG_PlayerId";
        private const string DisplayNameKey = "DIG_DisplayName";

        public IdentityProviderType ProviderType => IdentityProviderType.Local;
        public IdentityState State { get; private set; } = IdentityState.Uninitialized;

        public string PlatformId { get; private set; }
        public string DisplayName { get; private set; }

        public bool SupportsAvatars => false;
        public bool SupportsFriends => false;
        public bool SupportsPresence => false;
        public bool SupportsInvites => false;

        public Task<bool> InitializeAsync()
        {
            PlatformId = PlayerPrefs.GetString(PlayerIdKey, "");
            if (string.IsNullOrEmpty(PlatformId))
            {
                PlatformId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(PlayerIdKey, PlatformId);
                PlayerPrefs.Save();
            }

            DisplayName = PlayerPrefs.GetString(DisplayNameKey, $"Player_{PlatformId[..6]}");

            State = IdentityState.Ready;
            return Task.FromResult(true);
        }

        public void Shutdown() => State = IdentityState.Uninitialized;
        public void Tick() { }

        public Task<Texture2D> GetAvatarAsync(string platformId, AvatarSize size = AvatarSize.Medium)
            => Task.FromResult<Texture2D>(null);

        public Task<Texture2D> GetLocalAvatarAsync(AvatarSize size = AvatarSize.Medium)
            => Task.FromResult<Texture2D>(null);

        public FriendInfo[] GetFriends() => Array.Empty<FriendInfo>();
        public FriendInfo[] GetOnlineFriends() => Array.Empty<FriendInfo>();

        public void SetRichPresence(string key, string value) { }
        public void ClearRichPresence() { }
        public void InviteToLobby(string friendPlatformId, string joinCode) { }
    }
}
