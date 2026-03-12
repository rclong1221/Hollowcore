using System;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Behavior flags for social/group aggro features.
    /// Enemies without SocialAggroConfig skip all social systems entirely.
    /// </summary>
    [Flags]
    public enum SocialAggroFlags : ushort
    {
        None                = 0,
        LinkedPull          = 1 << 0,
        CallForHelp         = 1 << 1,
        RespondToHelp       = 1 << 2,
        ShareDamageInfo     = 1 << 3,
        AllyDeathAvenge     = 1 << 4,
        AllyDeathEnrage     = 1 << 5,
        AllyDeathFlee       = 1 << 6,
        PackBehavior        = 1 << 7,
        GuardCommunication  = 1 << 8,
        BodyDiscovery       = 1 << 9,
        DefenderAggro       = 1 << 10,
    }

    /// <summary>
    /// EPIC 15.33: Role within a pack hierarchy.
    /// Alpha death triggers special reactions on Members.
    /// </summary>
    public enum PackRole : byte
    {
        None   = 0,
        Alpha  = 1,
        Member = 2,
    }
}
