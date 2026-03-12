#if DIG_DEV_CONSOLE
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DIG.DebugConsole.Commands
{
    /// <summary>
    /// EPIC 18.9: ECS introspection console commands.
    /// </summary>
    public static class ECSCommands
    {
        // Cached component name → ComponentType map (built lazily on first ecs.count call)
        private static Dictionary<string, ComponentType> _componentTypeCache;

        [ConCommand("ecs.worlds", "List all ECS worlds", "",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ReadOnly)]
        public static void CmdWorlds(ConCommandArgs args)
        {
            var svc = DevConsoleService.Instance;
            svc.Log("--- ECS Worlds ---");
            foreach (var w in World.All)
            {
                if (!w.IsCreated) continue;
                int count = w.EntityManager.UniversalQuery.CalculateEntityCount();
                string flags = "";
                if (w.IsServer()) flags += " [Server]";
                if (w.IsClient()) flags += " [Client]";
                if (w.IsThinClient()) flags += " [ThinClient]";
                svc.Log($"  {w.Name}: {count:N0} entities{flags}");
            }
        }

        [ConCommand("ecs.count", "Count entities matching a component name", "ecs.count <ComponentName> [world]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ReadOnly)]
        public static void CmdCount(ConCommandArgs args)
        {
            if (args.Count == 0) { DevConsoleService.Instance.LogWarning("Usage: ecs.count <ComponentName> [world]"); return; }

            string componentName = args.GetString(0);
            string worldName = args.Count > 1 ? args.GetString(1) : null;

            var compType = FindComponentTypeCached(componentName);
            if (compType == null)
            {
                DevConsoleService.Instance.LogWarning($"Component '{componentName}' not found.");
                return;
            }

            foreach (var w in World.All)
            {
                if (!w.IsCreated) continue;
                if (worldName != null && !w.Name.Contains(worldName, StringComparison.OrdinalIgnoreCase)) continue;

                using var q = w.EntityManager.CreateEntityQuery(compType.Value);
                int count = q.CalculateEntityCount();
                DevConsoleService.Instance.Log($"  {w.Name}: {count:N0} entities with {componentName}");
            }
        }

        [ConCommand("ecs.systems", "List running systems in a world", "ecs.systems [world]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ReadOnly)]
        public static void CmdSystems(ConCommandArgs args)
        {
            var svc = DevConsoleService.Instance;
            string worldName = args.Count > 0 ? args.GetString(0) : null;

            foreach (var w in World.All)
            {
                if (!w.IsCreated) continue;
                if (worldName != null && !w.Name.Contains(worldName, StringComparison.OrdinalIgnoreCase)) continue;

                svc.Log($"--- Systems in {w.Name} ---");
                int shown = 0;
                foreach (var sys in w.Systems)
                {
                    if (sys == null) continue;
                    if (!sys.Enabled) continue;
                    string name = sys.GetType().Name;
                    svc.Log($"  {name}");
                    shown++;
                    if (shown >= 50)
                    {
                        svc.Log($"  ... ({w.Systems.Count - shown} more)");
                        break;
                    }
                }
            }
        }

        [ConCommand("ecs.inspect", "Show components on an entity by index", "ecs.inspect <entityIndex> [world]",
            ConCommandFlags.RequiresPlayMode | ConCommandFlags.ReadOnly)]
        public static void CmdInspect(ConCommandArgs args)
        {
            if (args.Count == 0) { DevConsoleService.Instance.LogWarning("Usage: ecs.inspect <entityIndex> [world]"); return; }

            int entityIndex = args.GetInt(0, -1);
            if (entityIndex < 0) { DevConsoleService.Instance.LogWarning("Invalid entity index."); return; }

            string worldName = args.Count > 1 ? args.GetString(1) : null;
            var world = worldName != null ? FindWorldByName(worldName) : DevConsoleService.FindAuthoritativeWorld();

            if (world == null || !world.IsCreated)
            {
                DevConsoleService.Instance.LogWarning("World not found.");
                return;
            }

            var em = world.EntityManager;

            // Warn about cost on large worlds
            int totalEntities = em.UniversalQuery.CalculateEntityCount();
            if (totalEntities > 5000)
                DevConsoleService.Instance.LogWarning($"Scanning {totalEntities:N0} entities — this may cause a brief hitch.");

            // Search for matching entity index across all entities
            var entities = em.GetAllEntities(Unity.Collections.Allocator.Temp);

            Entity found = Entity.Null;
            foreach (var e in entities)
            {
                if (e.Index == entityIndex) { found = e; break; }
            }
            entities.Dispose();

            if (found == Entity.Null)
            {
                DevConsoleService.Instance.LogWarning($"Entity with index {entityIndex} not found in {world.Name}.");
                return;
            }

            var types = em.GetComponentTypes(found, Unity.Collections.Allocator.Temp);
            DevConsoleService.Instance.Log($"--- Entity {found.Index}:{found.Version} in {world.Name} ({types.Length} components) ---");
            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                string typeName = t.GetManagedType()?.Name ?? $"TypeIndex:{t.TypeIndex}";
                string accessStr = t.IsZeroSized ? " [Tag]" : "";
                DevConsoleService.Instance.Log($"  {typeName}{accessStr}");
            }
            types.Dispose();
        }

        /// <summary>
        /// Cached component type lookup. Builds a name→ComponentType dictionary on first call
        /// by scanning all assemblies once. Subsequent calls are O(1) dictionary lookups.
        /// </summary>
        private static ComponentType? FindComponentTypeCached(string name)
        {
            if (_componentTypeCache == null)
                BuildComponentTypeCache();

            return _componentTypeCache.TryGetValue(name.ToLowerInvariant(), out var ct) ? ct : null;
        }

        private static void BuildComponentTypeCache()
        {
            _componentTypeCache = new Dictionary<string, ComponentType>(256);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsValueType) continue;
                        if (typeof(IComponentData).IsAssignableFrom(type) ||
                            typeof(IBufferElementData).IsAssignableFrom(type) ||
                            typeof(ICleanupComponentData).IsAssignableFrom(type))
                        {
                            string key = type.Name.ToLowerInvariant();
                            if (!_componentTypeCache.ContainsKey(key))
                                _componentTypeCache[key] = ComponentType.ReadOnly(TypeManager.GetTypeIndex(type));
                        }
                    }
                }
                catch { /* skip unloadable assemblies */ }
            }
        }

        private static World FindWorldByName(string name)
        {
            foreach (var w in World.All)
                if (w.IsCreated && w.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return w;
            return null;
        }
    }
}
#endif
