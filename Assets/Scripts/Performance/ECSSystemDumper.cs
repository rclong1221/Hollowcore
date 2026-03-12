using Unity.Entities;
using UnityEngine;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

namespace DIG.Performance
{
    public class ECSSystemDumper : MonoBehaviour
    {
        public bool DumpOnStart = true;
        public bool DumpToLog = true;

        void Start()
        {
            if (DumpOnStart)
            {
                // Call the main dump method that iterates all worlds
                DumpSystems();
            }
        }

        [ContextMenu("Dump ECS Systems")]
        public void DumpSystems()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Dumping {World.All.Count} Worlds...");

            foreach (var world in World.All)
            {
                DumpSystems(world, sb);
            }
            
            string path = Path.Combine(Application.dataPath, "ECSSystemsDump.txt");
            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log($"Dumped systems to {path}");
        }

        private void DumpSystems(World world, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine($"=== ECS WORLD SYSTEM HIERARCHY ({world.Name}) ===");
            
            // In some versions, World.Systems is a list, in others an aggregation
            // We'll iterate the known top-level systems
            foreach (var system in world.Systems)
            {
                PrintSystemRecursively(system, sb, 0);
            }
        }

        private void PrintSystemRecursively(ComponentSystemBase system, StringBuilder sb, int depth)
        {
            string indent = new string(' ', depth * 2);
            string typeName = system.GetType().Name;
            
            // Check if it's a group
            bool isGroup = system is ComponentSystemGroup;
            string marker = isGroup ? "[GROUP] " : "";
            
            sb.AppendLine($"{indent}{marker}{typeName}");

            if (isGroup)
            {
                var group = system as ComponentSystemGroup;
                
                // Use reflection to get Systems list if public property fails
                // Or try to cast to IEnumerable
                IEnumerable<ComponentSystemBase> children = null;

                // Try public property
                var systemsProp = typeof(ComponentSystemGroup).GetProperty("Systems");
                if (systemsProp != null)
                {
                    children = systemsProp.GetValue(group) as IEnumerable<ComponentSystemBase>;
                }
                
                // Fallback to field
                if (children == null)
                {
                    var systemsField = typeof(ComponentSystemGroup).GetField("m_systems", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (systemsField != null)
                    {
                        children = systemsField.GetValue(group) as IEnumerable<ComponentSystemBase>;
                    }
                }

                if (children != null)
                {
                    foreach (var child in children)
                    {
                        PrintSystemRecursively(child, sb, depth + 1);
                    }
                }
            }
        }
    }
}
