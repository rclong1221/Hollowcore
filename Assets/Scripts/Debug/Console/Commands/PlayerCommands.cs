#if DIG_DEV_CONSOLE
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Player.Components;
using DIG.Progression;

namespace DIG.DebugConsole.Commands
{
    /// <summary>
    /// EPIC 18.9: Player-related console commands.
    /// </summary>
    public static class PlayerCommands
    {
        [ConCommand("god", "Toggle god mode (invincibility)", "god [on|off]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdGod(ConCommandArgs args)
        {
            var world = DevConsoleService.FindAuthoritativeWorld();
            var player = DevConsoleService.FindLocalPlayer(world);
            if (player == Entity.Null) { DevConsoleService.Instance.LogWarning("No local player found."); return; }

            var em = world.EntityManager;
            if (!em.HasComponent<GodMode>(player)) { DevConsoleService.Instance.LogWarning("Player missing GodMode component."); return; }

            var gm = em.GetComponentData<GodMode>(player);
            bool target = args.Count > 0 ? args.GetBool(0, !gm.Enabled) : !gm.Enabled;
            gm.Enabled = target;
            em.SetComponentData(player, gm);

            DevConsoleService.Instance.Log($"God mode: {(target ? "ON" : "OFF")}");
        }

        [ConCommand("heal", "Heal player to full or specified amount", "heal [amount]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdHeal(ConCommandArgs args)
        {
            var world = DevConsoleService.FindAuthoritativeWorld();
            var player = DevConsoleService.FindLocalPlayer(world);
            if (player == Entity.Null) { DevConsoleService.Instance.LogWarning("No local player found."); return; }

            var em = world.EntityManager;
            if (!em.HasComponent<Health>(player)) { DevConsoleService.Instance.LogWarning("Player missing Health component."); return; }

            var hp = em.GetComponentData<Health>(player);
            float amount = args.Count > 0 ? args.GetFloat(0, hp.Max) : hp.Max;
            float before = hp.Current;
            hp.Current = Mathf.Min(hp.Current + amount, hp.Max);
            em.SetComponentData(player, hp);

            DevConsoleService.Instance.Log($"Healed {hp.Current - before:F0} HP ({before:F0} -> {hp.Current:F0}/{hp.Max:F0})");
        }

        [ConCommand("tp", "Teleport player to coordinates", "tp <x> <y> <z>",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdTeleport(ConCommandArgs args)
        {
            if (args.Count < 3) { DevConsoleService.Instance.LogWarning("Usage: tp <x> <y> <z>"); return; }

            var world = DevConsoleService.FindAuthoritativeWorld();
            var player = DevConsoleService.FindLocalPlayer(world);
            if (player == Entity.Null) { DevConsoleService.Instance.LogWarning("No local player found."); return; }

            var em = world.EntityManager;
            float3 pos = new float3(args.GetFloat(0), args.GetFloat(1), args.GetFloat(2));

            // Use TeleportEvent if available (coordinates with fall system)
            if (em.HasComponent<TeleportEvent>(player))
            {
                em.SetComponentData(player, new TeleportEvent
                {
                    TargetPosition = pos,
                    TargetRotation = em.GetComponentData<LocalTransform>(player).Rotation,
                    SnapAnimator = true
                });
                em.SetComponentEnabled<TeleportEvent>(player, true);
            }
            else
            {
                // Fallback: direct position write
                var lt = em.GetComponentData<LocalTransform>(player);
                lt.Position = pos;
                em.SetComponentData(player, lt);
            }

            DevConsoleService.Instance.Log($"Teleported to ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
        }

        [ConCommand("speed", "Set movement speed multiplier", "speed <multiplier>",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdSpeed(ConCommandArgs args)
        {
            if (args.Count < 1) { DevConsoleService.Instance.LogWarning("Usage: speed <multiplier>"); return; }

            var world = DevConsoleService.FindAuthoritativeWorld();
            var player = DevConsoleService.FindLocalPlayer(world);
            if (player == Entity.Null) { DevConsoleService.Instance.LogWarning("No local player found."); return; }

            var em = world.EntityManager;
            if (!em.HasComponent<CharacterControllerSettings>(player))
            { DevConsoleService.Instance.LogWarning("Player missing CharacterControllerSettings."); return; }

            float mult = args.GetFloat(0, 1f);
            var defaults = CharacterControllerSettings.Default;
            var settings = em.GetComponentData<CharacterControllerSettings>(player);
            settings.WalkSpeed = defaults.WalkSpeed * mult;
            settings.RunSpeed = defaults.RunSpeed * mult;
            em.SetComponentData(player, settings);

            DevConsoleService.Instance.Log($"Speed multiplier: {mult:F1}x (Walk={settings.WalkSpeed:F1}, Run={settings.RunSpeed:F1})");
        }

        [ConCommand("xp", "Grant XP to local player", "xp <amount>",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdXP(ConCommandArgs args)
        {
            if (args.Count < 1) { DevConsoleService.Instance.LogWarning("Usage: xp <amount>"); return; }

            var world = DevConsoleService.FindAuthoritativeWorld();
            var player = DevConsoleService.FindLocalPlayer(world);
            if (player == Entity.Null) { DevConsoleService.Instance.LogWarning("No local player found."); return; }

            int amount = args.GetInt(0, 100);
            XPGrantAPI.GrantXP(world.EntityManager, player, amount, XPSourceType.Bonus);

            DevConsoleService.Instance.Log($"Granted {amount} XP.");
        }

        [ConCommand("give", "Give item to player (stub)", "give <itemId> [count]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdGive(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'give' is not yet implemented. Requires Inventory system integration.");
        }

        [ConCommand("noclip", "Toggle noclip mode (stub)", "noclip [on|off]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdNoclip(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'noclip' is not yet implemented. Requires custom movement override.");
        }

        [ConCommand("level", "Set player level (stub)", "level <level>",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ServerOnly)]
        public static void CmdLevel(ConCommandArgs args)
        {
            DevConsoleService.Instance.LogWarning("'level' is not yet implemented. Use 'xp' to grant XP instead.");
        }
    }
}
#endif
