using System;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Readonly token representing an open screen instance.
    /// Returned by IUIService.OpenScreen(), passed to CloseScreen().
    /// </summary>
    public readonly struct ScreenHandle : IEquatable<ScreenHandle>
    {
        public readonly int Id;
        public readonly string ScreenId;

        public static readonly ScreenHandle Invalid = default;
        public bool IsValid => Id > 0;

        public ScreenHandle(int id, string screenId)
        {
            Id = id;
            ScreenId = screenId;
        }

        public bool Equals(ScreenHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ScreenHandle h && Equals(h);
        public override int GetHashCode() => Id;
        public override string ToString() => IsValid ? $"ScreenHandle({ScreenId}#{Id})" : "ScreenHandle(Invalid)";

        public static bool operator ==(ScreenHandle a, ScreenHandle b) => a.Id == b.Id;
        public static bool operator !=(ScreenHandle a, ScreenHandle b) => a.Id != b.Id;
    }
}
