#if HAS_EOS
using System;
using System.Threading.Tasks;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.UserInfo;
using Epic.OnlineServices.Friends;
using Epic.OnlineServices.Presence;
using UnityEngine;

namespace DIG.Identity.Providers
{
    /// <summary>
    /// EPIC 17.14: Epic Online Services identity provider.
    /// Only compiled when HAS_EOS is defined.
    /// </summary>
    public class EpicIdentityProvider : IIdentityProvider
    {
        private readonly PlatformIdentityConfigSO _config;
        private PlatformInterface _platform;
        private EpicAccountId _localUserId;

        public IdentityProviderType ProviderType => IdentityProviderType.Epic;
        public IdentityState State { get; private set; } = IdentityState.Uninitialized;

        public string PlatformId { get; private set; }
        public string DisplayName { get; private set; }

        public bool SupportsAvatars => false;
        public bool SupportsFriends => true;
        public bool SupportsPresence => true;
        public bool SupportsInvites => false;

        public EpicIdentityProvider(PlatformIdentityConfigSO config)
        {
            _config = config;
        }

        public async Task<bool> InitializeAsync()
        {
            State = IdentityState.Initializing;

            try
            {
                var initOptions = new InitializeOptions
                {
                    ProductName = Application.productName,
                    ProductVersion = Application.version
                };
                var initResult = PlatformInterface.Initialize(ref initOptions);
                if (initResult != Result.Success && initResult != Result.AlreadyConfigured)
                {
                    Debug.LogWarning($"[Identity:EOS] Init failed: {initResult}");
                    State = IdentityState.Failed;
                    return false;
                }

                var options = new Options
                {
                    ProductId = _config.EpicProductId,
                    SandboxId = _config.EpicSandboxId,
                    DeploymentId = _config.EpicDeploymentId,
                    ClientCredentials = new ClientCredentials
                    {
                        ClientId = _config.EpicClientId,
                        ClientSecret = _config.EpicClientSecret
                    }
                };
                _platform = PlatformInterface.Create(ref options);
                if (_platform == null)
                {
                    State = IdentityState.Failed;
                    return false;
                }

                var loginTcs = new TaskCompletionSource<bool>();
                var authInterface = _platform.GetAuthInterface();
                var loginOptions = new LoginOptions
                {
                    Credentials = new Credentials
                    {
                        Type = LoginCredentialType.AccountPortal
                    }
                };
                authInterface.Login(ref loginOptions, null, (ref LoginCallbackInfo info) =>
                {
                    if (info.ResultCode == Result.Success)
                    {
                        _localUserId = info.LocalUserId;
                        PlatformId = _localUserId.ToString();
                        loginTcs.TrySetResult(true);
                    }
                    else
                    {
                        loginTcs.TrySetResult(false);
                    }
                });

                bool loginSuccess = await loginTcs.Task;
                if (!loginSuccess)
                {
                    State = IdentityState.Failed;
                    return false;
                }

                // Query display name
                var userInfoTcs = new TaskCompletionSource<bool>();
                var userInfoInterface = _platform.GetUserInfoInterface();
                var queryOptions = new QueryUserInfoOptions
                {
                    LocalUserId = _localUserId,
                    TargetUserId = _localUserId
                };
                userInfoInterface.QueryUserInfo(ref queryOptions, null, (ref QueryUserInfoCallbackInfo info) =>
                {
                    if (info.ResultCode == Result.Success)
                    {
                        var copyOptions = new CopyUserInfoOptions
                        {
                            LocalUserId = _localUserId,
                            TargetUserId = _localUserId
                        };
                        if (userInfoInterface.CopyUserInfo(ref copyOptions, out var outInfo) == Result.Success)
                        {
                            DisplayName = outInfo?.DisplayName ?? PlatformId;
                        }
                    }
                    userInfoTcs.TrySetResult(true);
                });

                await userInfoTcs.Task;
                if (string.IsNullOrEmpty(DisplayName))
                    DisplayName = PlatformId;

                State = IdentityState.Ready;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Identity:EOS] Init exception: {e.Message}");
                State = IdentityState.Failed;
                return false;
            }
        }

        public void Shutdown()
        {
            _platform?.Release();
            _platform = null;
            State = IdentityState.Uninitialized;
        }

        public void Tick()
        {
            _platform?.Tick();
        }

        public Task<Texture2D> GetAvatarAsync(string platformId, AvatarSize size = AvatarSize.Medium)
            => Task.FromResult<Texture2D>(null);

        public Task<Texture2D> GetLocalAvatarAsync(AvatarSize size = AvatarSize.Medium)
            => Task.FromResult<Texture2D>(null);

        public FriendInfo[] GetFriends()
        {
            if (_platform == null) return Array.Empty<FriendInfo>();

            var friendsInterface = _platform.GetFriendsInterface();
            int count = friendsInterface.GetFriendsCount(new GetFriendsCountOptions { LocalUserId = _localUserId });
            if (count <= 0) return Array.Empty<FriendInfo>();

            var results = new FriendInfo[count];
            for (int i = 0; i < count; i++)
            {
                var friendId = friendsInterface.GetFriendAtIndex(new GetFriendAtIndexOptions
                {
                    LocalUserId = _localUserId,
                    Index = i
                });
                results[i] = new FriendInfo
                {
                    PlatformId = friendId?.ToString() ?? "",
                    DisplayName = friendId?.ToString() ?? "",
                    IsOnline = false,
                    IsInGame = false
                };
            }
            return results;
        }

        public FriendInfo[] GetOnlineFriends() => GetFriends();

        public void SetRichPresence(string key, string value)
        {
            if (_platform == null) return;
            var presenceInterface = _platform.GetPresenceInterface();
            var modHandle = new CreatePresenceModificationOptions { LocalUserId = _localUserId };
            if (presenceInterface.CreatePresenceModification(ref modHandle, out var modification) == Result.Success)
            {
                var setOptions = new PresenceModificationSetRawRichTextOptions { RichText = value };
                modification.SetRawRichText(ref setOptions);
                var setPresOptions = new SetPresenceOptions { LocalUserId = _localUserId, PresenceModificationHandle = modification };
                presenceInterface.SetPresence(ref setPresOptions, null, (ref SetPresenceCallbackInfo info) => { });
                modification.Release();
            }
        }

        public void ClearRichPresence()
        {
            SetRichPresence("status", "");
        }

        public void InviteToLobby(string friendPlatformId, string joinCode) { }
    }
}
#endif
