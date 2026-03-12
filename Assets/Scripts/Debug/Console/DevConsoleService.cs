#if DIG_DEV_CONSOLE
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
namespace DIG.DebugConsole
{
    /// <summary>
    /// EPIC 18.9: Dev Console singleton. Scans for [ConCommand] methods,
    /// manages command registry, output ring buffer, and ECS world helpers.
    /// </summary>
    public sealed class DevConsoleService : MonoBehaviour
    {
        public static DevConsoleService Instance { get; private set; }

        // Command registry
        public struct CommandEntry
        {
            public string Name;
            public string Description;
            public string Usage;
            public ConCommandFlags Flags;
            public Action<ConCommandArgs> Execute;
        }

        private readonly Dictionary<string, CommandEntry> _commands = new(64);
        public IReadOnlyDictionary<string, CommandEntry> Commands => _commands;

        // Output ring buffer
        public struct ConsoleLogEntry
        {
            public string Text;
            public LogType Type;
        }

        private const int OutputCapacity = 500;
        private readonly ConsoleLogEntry[] _output = new ConsoleLogEntry[OutputCapacity];
        private int _outputHead;
        private int _outputCount;
        public int OutputCount => _outputCount;

        // History
        public CommandHistory History { get; } = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterBuiltinCommands();
            ScanForCommandAttributes();
            History.Load();

            // Register with ServiceLocator
            DIG.Core.ServiceLocator.Register(this);

            Log($"Dev Console initialized. {_commands.Count} commands registered. Type 'help' for a list.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                History.Save();
                DIG.Core.ServiceLocator.Unregister<DevConsoleService>();
                Instance = null;
            }
        }

        #region Command Registration

        private void RegisterBuiltinCommands()
        {
            RegisterCommand("help", "List all commands or get help for a specific command", "help [command]",
                ConCommandFlags.ReadOnly, CmdHelp);
            RegisterCommand("clear", "Clear console output", "", ConCommandFlags.None, CmdClear);
        }

        public void RegisterCommand(string name, string description, string usage, ConCommandFlags flags, Action<ConCommandArgs> execute)
        {
            _commands[name.ToLowerInvariant()] = new CommandEntry
            {
                Name = name.ToLowerInvariant(),
                Description = description,
                Usage = usage,
                Flags = flags,
                Execute = execute
            };
        }

        private void ScanForCommandAttributes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = assembly.GetName().Name;
                if (asmName.StartsWith("System") || asmName.StartsWith("Unity.") ||
                    asmName.StartsWith("UnityEngine") || asmName.StartsWith("UnityEditor") ||
                    asmName.StartsWith("mscorlib") || asmName.StartsWith("netstandard") ||
                    asmName.StartsWith("Mono.") || asmName.StartsWith("nunit"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var attr = method.GetCustomAttribute<ConCommandAttribute>();
                            if (attr == null) continue;

                            try
                            {
                                var del = (Action<ConCommandArgs>)Delegate.CreateDelegate(typeof(Action<ConCommandArgs>), method);
                                RegisterCommand(attr.Name, attr.Description, attr.Usage, attr.Flags, del);
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning($"[DevConsole] Failed to register '{attr.Name}': {e.Message}");
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException) { /* skip assemblies with unloadable types */ }
            }
        }

        #endregion

        #region Execution

        public void Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            History.Add(input);
            Log($"> {input}");

            var args = CommandParser.Parse(input);

            if (!_commands.TryGetValue(args.CommandName, out var cmd))
            {
                LogWarning($"Unknown command: '{args.CommandName}'. Type 'help' for a list.");
                return;
            }

            if ((cmd.Flags & ConCommandFlags.RequiresPlayMode) != 0 && !Application.isPlaying)
            {
                LogWarning($"'{cmd.Name}' requires play mode.");
                return;
            }

            try
            {
                cmd.Execute(args);
            }
            catch (Exception e)
            {
                LogError($"Error executing '{cmd.Name}': {e.Message}");
            }
        }

        #endregion

        #region Output

        public void Log(string text) => AddOutput(text, LogType.Log);
        public void LogWarning(string text) => AddOutput(text, LogType.Warning);
        public void LogError(string text) => AddOutput(text, LogType.Error);

        private void AddOutput(string text, LogType type)
        {
            int index = (_outputHead + _outputCount) % OutputCapacity;
            if (_outputCount == OutputCapacity)
            {
                _outputHead = (_outputHead + 1) % OutputCapacity;
            }
            else
            {
                _outputCount++;
            }

            _output[index] = new ConsoleLogEntry { Text = text, Type = type };
        }

        public ConsoleLogEntry GetOutput(int i)
        {
            if (i < 0 || i >= _outputCount) return default;
            return _output[(_outputHead + i) % OutputCapacity];
        }

        public void ClearOutput()
        {
            // Null out string references so GC can collect them
            System.Array.Clear(_output, 0, OutputCapacity);
            _outputHead = 0;
            _outputCount = 0;
        }

        #endregion

        #region ECS World Helpers

        public static World FindServerWorld()
        {
            foreach (var w in World.All)
                if (w.IsServer()) return w;
            return null;
        }

        public static World FindClientWorld()
        {
            foreach (var w in World.All)
                if (w.IsClient()) return w;
            return null;
        }

        /// <summary>Returns ServerWorld if available, otherwise DefaultGameObjectInjectionWorld.</summary>
        public static World FindAuthoritativeWorld()
        {
            return FindServerWorld() ?? World.DefaultGameObjectInjectionWorld;
        }

        /// <summary>Find local player entity in the given world (has PlayerTag + GhostOwnerIsLocal).</summary>
        public static Entity FindLocalPlayer(World world)
        {
            if (world == null || !world.IsCreated) return Entity.Null;
            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<global::PlayerTag>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            if (query.IsEmpty) return Entity.Null;
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var result = entities.Length > 0 ? entities[0] : Entity.Null;
            entities.Dispose();
            return result;
        }

        #endregion

        #region Builtin Commands

        private void CmdHelp(ConCommandArgs args)
        {
            if (args.Count > 0)
            {
                string name = args.GetString(0).ToLowerInvariant();
                if (_commands.TryGetValue(name, out var cmd))
                {
                    Log($"{cmd.Name}: {cmd.Description}");
                    if (!string.IsNullOrEmpty(cmd.Usage))
                        Log($"  Usage: {cmd.Usage}");
                    return;
                }
                LogWarning($"Unknown command: '{name}'");
                return;
            }

            Log("--- Available Commands ---");
            var sorted = new List<CommandEntry>(_commands.Values);
            sorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            foreach (var cmd in sorted)
            {
                if ((cmd.Flags & ConCommandFlags.Hidden) != 0) continue;
                Log($"  {cmd.Name,-20} {cmd.Description}");
            }
        }

        private void CmdClear(ConCommandArgs args)
        {
            ClearOutput();
        }

        #endregion
    }
}
#endif
