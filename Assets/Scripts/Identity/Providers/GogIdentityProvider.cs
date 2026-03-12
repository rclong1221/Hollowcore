#if HAS_GOG_GALAXY
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Galaxy.Api;
using UnityEngine;

namespace DIG.Identity.Providers
{
    /// <summary>
    /// EPIC 17.14: GOG Galaxy identity provider.
    /// Only compiled when HAS_GOG_GALAXY is defined.
    /// </summary>
    public class GogIdentityProvider : IIdentityProvider
    {
        private readonly PlatformIdentityConfigSO _config;
        private static readonly HttpClient _httpClient = new();

        public IdentityProviderType ProviderType => IdentityProviderType.GOG;
        public IdentityState State { get; private set; } = IdentityState.Uninitialized;

        public string PlatformId { get; private set; }
        public string DisplayName { get; private set; }

        public bool SupportsAvatars => true;
        public bool SupportsFriends => true;
        public bool SupportsPresence => true;
        public bool SupportsInvites => false;

        public GogIdentityProvider(PlatformIdentityConfigSO config)
        {
            _config = config;
        }

        public Task<bool> InitializeAsync()
        {
            State = IdentityState.Initializing;

            try
            {
                GalaxyInstance.Init(new InitParams(_config.GogClientId, _config.GogClientSecret));

                var user = GalaxyInstance.User();
                if (!user.SignedIn())
                {
                    user.SignInGalaxy();
                }

                if (!user.IsLoggedOn())
                {
                    State = IdentityState.Failed;
                    return Task.FromResult(false);
                }

                PlatformId = user.GetGalaxyID().ToUint64().ToString();
                DisplayName = GalaxyInstance.Friends().GetPersonaName();

                State = IdentityState.Ready;
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Identity:GOG] Init exception: {e.Message}");
                State = IdentityState.Failed;
                return Task.FromResult(false);
            }
        }

        public void Shutdown()
        {
            try { GalaxyInstance.Shutdown(true); } catch { }
            State = IdentityState.Uninitialized;
        }

        public void Tick()
        {
            if (State == IdentityState.Ready)
                GalaxyInstance.ProcessData();
        }

        public async Task<Texture2D> GetAvatarAsync(string platformId, AvatarSize size = AvatarSize.Medium)
        {
            try
            {
                if (!ulong.TryParse(platformId, out ulong gogId))
                    return null;

                var galaxyId = new GalaxyID(gogId);
                string avatarUrl = GalaxyInstance.Friends().GetFriendAvatarUrl(galaxyId,
                    size == AvatarSize.Large ? AvatarType.AVATAR_TYPE_LARGE :
                    size == AvatarSize.Medium ? AvatarType.AVATAR_TYPE_MEDIUM :
                    AvatarType.AVATAR_TYPE_SMALL);

                if (string.IsNullOrEmpty(avatarUrl)) return null;

                byte[] data = await _httpClient.GetByteArrayAsync(avatarUrl);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(data))
                    return tex;

                UnityEngine.Object.Destroy(tex);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Identity:GOG] Avatar load failed: {e.Message}");
                return null;
            }
        }

        public Task<Texture2D> GetLocalAvatarAsync(AvatarSize size = AvatarSize.Medium)
            => GetAvatarAsync(PlatformId, size);

        public FriendInfo[] GetFriends()
        {
            try
            {
                var friends = GalaxyInstance.Friends();
                uint count = friends.GetFriendCount();
                if (count == 0) return Array.Empty<FriendInfo>();

                var results = new FriendInfo[count];
                for (uint i = 0; i < count; i++)
                {
                    var friendId = friends.GetFriendByIndex(i);
                    results[i] = new FriendInfo
                    {
                        PlatformId = friendId.ToUint64().ToString(),
                        DisplayName = friends.GetFriendPersonaName(friendId),
                        IsOnline = friends.IsFriendAvatarImageRGBAAvailable(friendId, AvatarType.AVATAR_TYPE_SMALL),
                        IsInGame = false
                    };
                }
                return results;
            }
            catch
            {
                return Array.Empty<FriendInfo>();
            }
        }

        public FriendInfo[] GetOnlineFriends()
        {
            var all = GetFriends();
            int onlineCount = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].IsOnline) onlineCount++;

            if (onlineCount == all.Length) return all;

            var online = new FriendInfo[onlineCount];
            int idx = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].IsOnline) online[idx++] = all[i];
            return online;
        }

        public void SetRichPresence(string key, string value)
        {
            try { GalaxyInstance.Friends().SetRichPresence(key, value); } catch { }
        }

        public void ClearRichPresence()
        {
            try { GalaxyInstance.Friends().DeleteRichPresence("status"); } catch { }
        }

        public void InviteToLobby(string friendPlatformId, string joinCode) { }
    }
}
#endif
