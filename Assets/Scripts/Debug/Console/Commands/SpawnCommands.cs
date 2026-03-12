#if DIG_DEV_CONSOLE
namespace DIG.DebugConsole.Commands
{
    /// <summary>
    /// EPIC 18.9: Spawn/kill console commands (stubs for future integration).
    /// </summary>
    public static class SpawnCommands
    {
        [ConCommand("spawn", "Spawn an entity by prefab name (stub)", "spawn <prefabName> [count]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdSpawn(ConCommandArgs args)
        {
            if (args.Count == 0) { DevConsoleService.Instance.LogWarning("Usage: spawn <prefabName> [count]"); return; }

            string prefab = args.GetString(0);
            int count = args.GetInt(1, 1);
            DevConsoleService.Instance.LogWarning(
                $"'spawn {prefab} x{count}' is not yet implemented. Requires GhostPrefab registry integration.");
        }

        [ConCommand("kill", "Kill targeted or all nearby enemies (stub)", "kill [all|radius]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdKill(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'kill' is not yet implemented. Requires target selection system integration.");
        }

        [ConCommand("despawn", "Despawn all debug-spawned entities (stub)", "despawn",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdDespawn(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'despawn' is not yet implemented.");
        }
    }
}
#endif
