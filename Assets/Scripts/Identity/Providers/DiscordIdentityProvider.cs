#if HAS_DISCORD_SDK
using System;
using System.Threading.Tasks;
using Discord;
using UnityEngine;

namespace DIG.Identity.Providers
{
    /// <summary>
    /// EPIC 17.14: Discord Game SDK identity provider.
    /// Only compiled when HAS_DISCORD_SDK is defined.
    /// </summary>
    public class DiscordIdentityProvider : IIdentityProvider
    {
        private readonly PlatformIdentityConfigSO _config;
        private Discord.Discord _discord;

        public IdentityProviderType ProviderType => IdentityProviderType.Discord;
        public IdentityState State { get; private set; } = IdentityState.Uninitialized;

        public string PlatformId { get; private set; }
        public string DisplayName { get; private set; }

        public bool SupportsAvatars => true;
        public bool SupportsFriends => true;
        public bool SupportsPresence => true;
        public bool SupportsInvites => false;

        public DiscordIdentityProvider(PlatformIdentityConfigSO config)
        {
            _config = config;
        }

        public Task<bool> InitializeAsync()
        {
            State = IdentityState.Initializing;

            try
            {
                long appId = _config != null ? _config.DiscordApplicationId : 0;
                if (appId == 0)
                {
                    State = IdentityState.Failed;
                    return Task.FromResult(false);
                }

                _discord = new Discord.Discord(appId, (ulong)CreateFlags.NoRequireDiscord);

                var userManager = _discord.GetUserManager();
                var currentUser = userManager.GetCurrentUser();
                PlatformId = currentUser.Id.ToString();
                DisplayName = currentUser.Username;

                State = IdentityState.Ready;
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Identity:Discord] Init exception: {e.Message}");
                State = IdentityState.Failed;
                return Task.FromResult(false);
            }
        }

        public void Shutdown()
        {
            _discord?.Dispose();
            _discord = null;
            State = IdentityState.Uninitialized;
        }

        public void Tick()
        {
            if (State == IdentityState.Ready)
            {
                try { _discord?.RunCallbacks(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Identity:Discord] RunCallbacks error: {e.Message}");
                }
            }
        }

        public Task<Texture2D> GetAvatarAsync(string platformId, AvatarSize size = AvatarSize.Medium)
        {
            if (_discord == null || !long.TryParse(platformId, out long userId))
                return Task.FromResult<Texture2D>(null);

            var tcs = new TaskCompletionSource<Texture2D>();
            var imageManager = _discord.GetImageManager();

            int dimension = size switch
            {
                AvatarSize.Small => 32,
                AvatarSize.Large => 128,
                _ => 64,
            };

            var handle = new ImageHandle
            {
                Id = userId,
                Size = (uint)dimension,
                Type = ImageType.User
            };

            imageManager.Fetch(handle, false, (result, fetchedHandle) =>
            {
                if (result != Result.Ok)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                var dims = imageManager.GetTexture(fetchedHandle);
                var tex = new Texture2D((int)dims.Width, (int)dims.Height, TextureFormat.RGBA32, false);
                byte[] data = imageManager.GetData(fetchedHandle);
                tex.LoadRawTextureData(data);
                tex.Apply(false, false);
                tcs.TrySetResult(tex);
            });

            return tcs.Task;
        }

        public Task<Texture2D> GetLocalAvatarAsync(AvatarSize size = AvatarSize.Medium)
            => GetAvatarAsync(PlatformId, size);

        public FriendInfo[] GetFriends()
        {
            if (_discord == null) return Array.Empty<FriendInfo>();

            try
            {
                var relationshipManager = _discord.GetRelationshipManager();
                relationshipManager.Filter((ref Relationship r) => r.Type == RelationshipType.Friend);

                int count = relationshipManager.Count();
                if (count <= 0) return Array.Empty<FriendInfo>();

                var results = new FriendInfo[count];
                for (int i = 0; i < count; i++)
                {
                    var rel = relationshipManager.GetAt((uint)i);
                    results[i] = new FriendInfo
                    {
                        PlatformId = rel.User.Id.ToString(),
                        DisplayName = rel.User.Username,
                        IsOnline = rel.Presence.Status != Status.Offline,
                        IsInGame = rel.Presence.Activity.ApplicationId == _config?.DiscordApplicationId
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
            if (_discord == null) return;

            var activityManager = _discord.GetActivityManager();
            var activity = new Activity
            {
                State = value,
                Details = key
            };
            activityManager.UpdateActivity(activity, (result) => { });
        }

        public void ClearRichPresence()
        {
            if (_discord == null) return;
            var activityManager = _discord.GetActivityManager();
            activityManager.ClearActivity((result) => { });
        }

        public void InviteToLobby(string friendPlatformId, string joinCode) { }
    }
}
#endif
