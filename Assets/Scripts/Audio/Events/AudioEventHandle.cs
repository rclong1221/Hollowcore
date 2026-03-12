using System;

namespace Audio.Events
{
    public readonly struct AudioEventHandle : IEquatable<AudioEventHandle>
    {
        public readonly int Id;
        public readonly int EventHash;

        internal AudioEventHandle(int id, int eventHash)
        {
            Id = id;
            EventHash = eventHash;
        }

        public bool IsValid => Id > 0;

        public static readonly AudioEventHandle Invalid = default;

        public bool Equals(AudioEventHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is AudioEventHandle h && Equals(h);
        public override int GetHashCode() => Id;
        public static bool operator ==(AudioEventHandle a, AudioEventHandle b) => a.Id == b.Id;
        public static bool operator !=(AudioEventHandle a, AudioEventHandle b) => a.Id != b.Id;
        public override string ToString() => $"AudioEventHandle({Id})";
    }
}
