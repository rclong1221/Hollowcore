#if DIG_DEV_CONSOLE
using System;

namespace DIG.DebugConsole
{
    /// <summary>
    /// EPIC 18.9: Flags controlling command visibility and execution context.
    /// </summary>
    [Flags]
    public enum ConCommandFlags
    {
        None = 0,
        /// <summary>Command requires an active ECS world (play mode).</summary>
        RequiresPlayMode = 1 << 0,
        /// <summary>Command modifies server-authoritative state.</summary>
        ServerOnly = 1 << 1,
        /// <summary>Hidden from help listing (internal/alias).</summary>
        Hidden = 1 << 2,
        /// <summary>Command is safe to call repeatedly (no side effects).</summary>
        ReadOnly = 1 << 3
    }

    /// <summary>
    /// EPIC 18.9: Mark a static method as a dev console command.
    /// DevConsoleService scans for these via reflection at startup.
    /// Method signature must be: static void MethodName(ConCommandArgs args)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConCommandAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public string Usage { get; }
        public ConCommandFlags Flags { get; }

        public ConCommandAttribute(string name, string description, string usage = "", ConCommandFlags flags = ConCommandFlags.None)
        {
            Name = name;
            Description = description;
            Usage = usage;
            Flags = flags;
        }
    }
}
#endif
