using System.Threading.Tasks;
using UnityEngine;

namespace DIG.Identity
{
    public enum IdentityProviderType : byte
    {
        Auto = 255,
        Local = 0,
        Steam = 1,
        Epic = 2,
        GOG = 3,
        Discord = 4
    }

    public enum IdentityState : byte
    {
        Uninitialized = 0,
        Initializing = 1,
        Ready = 2,
        Failed = 3
    }

    public enum AvatarSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    public struct FriendInfo
    {
        public string PlatformId;
        public string DisplayName;
        public bool IsOnline;
        public bool IsInGame;
        public string LobbyCode;
    }

    public interface IIdentityProvider
    {
        IdentityProviderType ProviderType { get; }
        IdentityState State { get; }

        string PlatformId { get; }
        string DisplayName { get; }

        bool SupportsAvatars { get; }
        bool SupportsFriends { get; }
        bool SupportsPresence { get; }
        bool SupportsInvites { get; }

        Task<bool> InitializeAsync();
        void Shutdown();
        void Tick();

        Task<Texture2D> GetAvatarAsync(string platformId, AvatarSize size = AvatarSize.Medium);
        Task<Texture2D> GetLocalAvatarAsync(AvatarSize size = AvatarSize.Medium);

        FriendInfo[] GetFriends();
        FriendInfo[] GetOnlineFriends();

        void SetRichPresence(string key, string value);
        void ClearRichPresence();

        void InviteToLobby(string friendPlatformId, string joinCode);
    }
}
