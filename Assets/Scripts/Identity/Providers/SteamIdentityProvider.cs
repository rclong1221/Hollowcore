#if HAS_STEAMWORKS
using System;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace DIG.Identity.Providers
{
    /// <summary>
    /// EPIC 17.14: Steam identity via Steamworks.NET.
    /// Only compiled when HAS_STEAMWORKS is defined.
    /// </summary>
    public class SteamIdentityProvider : IIdentityProvider
    {
        private readonly PlatformIdentityConfigSO _config;

        public IdentityProviderType ProviderType => IdentityProviderType.Steam;
        public IdentityState State { get; private set; } = IdentityState.Uninitialized;

        public string PlatformId { get; private set; }
        public string DisplayName { get; private set; }

        public bool SupportsAvatars => true;
        public bool SupportsFriends => true;
        public bool SupportsPresence => true;
        public bool SupportsInvites => true;

        public SteamIdentityProvider(PlatformIdentityConfigSO config)
        {
            _config = config;
        }

        public Task<bool> InitializeAsync()
        {
            State = IdentityState.Initializing;

            try
            {
                if (_config != null && _config.SteamAppId > 0)
                    Environment.SetEnvironmentVariable("SteamAppId", _config.SteamAppId.ToString());

                if (!SteamAPI.Init())
                {
                    Debug.LogWarning("[Identity:Steam] SteamAPI.Init() failed. Is Steam running?");
                    State = IdentityState.Failed;
                    return Task.FromResult(false);
                }

                PlatformId = SteamUser.GetSteamID().m_SteamID.ToString();
                DisplayName = SteamFriends.GetPersonaName();

                State = IdentityState.Ready;
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Identity:Steam] Init exception: {e.Message}");
                State = IdentityState.Failed;
                return Task.FromResult(false);
            }
        }

        public void Shutdown()
        {
            if (State == IdentityState.Ready)
                SteamAPI.Shutdown();
            State = IdentityState.Uninitialized;
        }

        public void Tick()
        {
            if (State == IdentityState.Ready)
                SteamAPI.RunCallbacks();
        }

        public Task<Texture2D> GetAvatarAsync(string platformId, AvatarSize size = AvatarSize.Medium)
        {
            if (!ulong.TryParse(platformId, out ulong steamId64))
                return Task.FromResult<Texture2D>(null);

            var cSteamId = new CSteamID(steamId64);
            int imageHandle = size switch
            {
                AvatarSize.Small => SteamFriends.GetSmallFriendAvatar(cSteamId),
                AvatarSize.Large => SteamFriends.GetLargeFriendAvatar(cSteamId),
                _ => SteamFriends.GetMediumFriendAvatar(cSteamId),
            };

            if (imageHandle <= 0)
                return Task.FromResult<Texture2D>(null);

            if (!SteamUtils.GetImageSize(imageHandle, out uint width, out uint height))
                return Task.FromResult<Texture2D>(null);

            int bufferSize = (int)(width * height * 4);
            byte[] rgbaData = new byte[bufferSize];
            if (!SteamUtils.GetImageRGBA(imageHandle, rgbaData, bufferSize))
                return Task.FromResult<Texture2D>(null);

            FlipRgbaVertically(rgbaData, (int)width, (int)height);
            var tex = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaData);
            tex.Apply(false, false);
            return Task.FromResult(tex);
        }

        public Task<Texture2D> GetLocalAvatarAsync(AvatarSize size = AvatarSize.Medium)
            => GetAvatarAsync(PlatformId, size);

        public FriendInfo[] GetFriends()
        {
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            if (count <= 0) return Array.Empty<FriendInfo>();

            var friends = new FriendInfo[count];
            for (int i = 0; i < count; i++)
            {
                var friendId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                var state = SteamFriends.GetFriendPersonaState(friendId);
                int appId = SteamFriends.GetFriendGamePlayed(friendId, out var gameInfo)
                    ? (int)gameInfo.m_gameID.AppID().m_AppId : 0;

                friends[i] = new FriendInfo
                {
                    PlatformId = friendId.m_SteamID.ToString(),
                    DisplayName = SteamFriends.GetFriendPersonaName(friendId),
                    IsOnline = state != EPersonaState.k_EPersonaStateOffline,
                    IsInGame = _config != null && appId == (int)_config.SteamAppId,
                    LobbyCode = null
                };
            }
            return friends;
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
            SteamFriends.SetRichPresence(key, value);
        }

        public void ClearRichPresence()
        {
            SteamFriends.ClearRichPresence();
        }

        public void InviteToLobby(string friendPlatformId, string joinCode)
        {
            if (!ulong.TryParse(friendPlatformId, out ulong steamId64)) return;
            SteamFriends.InviteUserToGame(new CSteamID(steamId64), $"+connect {joinCode}");
        }

        private static void FlipRgbaVertically(byte[] rgba, int width, int height)
        {
            int stride = width * 4;
            byte[] rowBuffer = new byte[stride];
            for (int y = 0; y < height / 2; y++)
            {
                int topOffset = y * stride;
                int bottomOffset = (height - 1 - y) * stride;
                System.Buffer.BlockCopy(rgba, topOffset, rowBuffer, 0, stride);
                System.Buffer.BlockCopy(rgba, bottomOffset, rgba, topOffset, stride);
                System.Buffer.BlockCopy(rowBuffer, 0, rgba, bottomOffset, stride);
            }
        }
    }
}
#endif
