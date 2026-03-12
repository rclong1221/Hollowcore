using System;
using Unity.Collections;

namespace DIG.Analytics
{
    [Flags]
    public enum AnalyticsCategory : uint
    {
        None         = 0x0,
        Session      = 0x1,
        Combat       = 0x2,
        Economy      = 0x4,
        Progression  = 0x8,
        Quest        = 0x10,
        Crafting     = 0x20,
        Social       = 0x40,
        Performance  = 0x80,
        UI           = 0x100,
        World        = 0x200,
        PvP          = 0x400,
        Custom       = 0x800,

        All          = 0xFFF
    }

    public struct AnalyticsEvent
    {
        public AnalyticsCategory Category;
        public FixedString64Bytes Action;
        public FixedString64Bytes SessionId;
        public FixedString64Bytes PlayerId;
        public long TimestampUtcMs;
        public uint ServerTick;
        public FixedString512Bytes PropertiesJson;
    }
}
