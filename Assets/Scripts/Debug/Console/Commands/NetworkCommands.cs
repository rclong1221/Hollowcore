#if DIG_DEV_CONSOLE
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.DebugConsole.Commands
{
    /// <summary>
    /// EPIC 18.9: Network diagnostics console commands.
    /// </summary>
    public static class NetworkCommands
    {
        [ConCommand("netstat", "Show network connection statistics", "",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ReadOnly)]
        public static void CmdNetstat(ConCommandArgs args)
        {
            var svc = DevConsoleService.Instance;
            var server = DevConsoleService.FindServerWorld();
            var client = DevConsoleService.FindClientWorld();

            svc.Log("--- Network Status ---");
            svc.Log($"  Server World: {(server != null ? server.Name : "None")}");
            svc.Log($"  Client World: {(client != null ? client.Name : "None")}");

            if (client != null && client.IsCreated)
            {
                var em = client.EntityManager;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
                svc.Log($"  Client connections: {q.CalculateEntityCount()}");

                using var tickQ = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>());
                if (!tickQ.IsEmpty)
                {
                    var nt = tickQ.GetSingleton<NetworkTime>();
                    svc.Log($"  Server tick: {nt.ServerTick.TickIndexForValidTick}");
                    svc.Log($"  Interpolation tick: {nt.InterpolationTick.TickIndexForValidTick}");
                }
            }

            if (server != null && server.IsCreated)
            {
                var em = server.EntityManager;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
                svc.Log($"  Server connections: {q.CalculateEntityCount()}");
            }
        }

        [ConCommand("ping", "Show estimated ping (stub)", "",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ReadOnly)]
        public static void CmdPing(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'ping' requires NetworkSnapshotAck integration. Not yet implemented.");
        }

        [ConCommand("lag", "Simulate network lag (stub)", "lag <ms>",
            ConCommandFlags.RequiresPlayMode)]
        public static void CmdLag(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'lag' simulation is not yet implemented. Use Unity's Network Simulation tool.");
        }

        [ConCommand("kick", "Kick a connected player (stub)", "kick <playerId>",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdKick(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'kick' is not yet implemented.");
        }
    }
}
#endif
