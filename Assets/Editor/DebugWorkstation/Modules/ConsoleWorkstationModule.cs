#if DIG_DEV_CONSOLE
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using DIG.DebugConsole;

namespace DIG.Editor.DebugWorkstation.Modules
{
    /// <summary>
    /// EPIC 18.9: Console tab for the Debug Workstation editor window.
    /// Browse registered commands, quick-execute, view history.
    /// </summary>
    public class ConsoleWorkstationModule : IDebugModule
    {
        private string _searchFilter = "";
        private string _quickCommand = "";
        private Vector2 _commandListScroll;
        private Vector2 _historyScroll;
        private Vector2 _outputScroll;
        private int _subTab; // 0=Commands, 1=History, 2=Output
        private readonly string[] _subTabs = { "Commands", "History", "Output" };

        // Cached filtered command list — rebuilt only when filter or command count changes
        private List<DevConsoleService.CommandEntry> _cachedCommands;
        private string _cachedFilter;
        private int _cachedCommandCount;

        // Reusable StringBuilder for flag strings (avoids per-command per-frame List<string> alloc)
        private readonly System.Text.StringBuilder _flagsSb = new(32);

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Dev Console", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying || DevConsoleService.Instance == null)
            {
                EditorGUILayout.HelpBox(
                    "Dev Console is only available in Play Mode with DIG_DEV_CONSOLE defined.\n" +
                    "Add DIG_DEV_CONSOLE to Player Settings > Scripting Define Symbols.",
                    MessageType.Info);
                return;
            }

            // Quick execute bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Execute:", GUILayout.Width(55));
            _quickCommand = EditorGUILayout.TextField(_quickCommand);
            if (GUILayout.Button("Run", GUILayout.Width(50)) && !string.IsNullOrWhiteSpace(_quickCommand))
            {
                DevConsoleService.Instance.Execute(_quickCommand);
                _quickCommand = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Sub-tabs
            _subTab = GUILayout.Toolbar(_subTab, _subTabs);
            EditorGUILayout.Space(4);

            switch (_subTab)
            {
                case 0: DrawCommandBrowser(); break;
                case 1: DrawHistory(); break;
                case 2: DrawOutput(); break;
            }
        }

        private void DrawCommandBrowser()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Rebuild cached list only when filter or command count changes
            int currentCount = DevConsoleService.Instance.Commands.Count;
            if (_cachedCommands == null || _cachedFilter != _searchFilter || _cachedCommandCount != currentCount)
            {
                _cachedFilter = _searchFilter;
                _cachedCommandCount = currentCount;
                if (_cachedCommands == null)
                    _cachedCommands = new List<DevConsoleService.CommandEntry>(currentCount);
                else
                    _cachedCommands.Clear();

                foreach (var cmd in DevConsoleService.Instance.Commands.Values)
                {
                    if ((cmd.Flags & ConCommandFlags.Hidden) != 0) continue;
                    if (!string.IsNullOrEmpty(_searchFilter) &&
                        !cmd.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) &&
                        !cmd.Description.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    _cachedCommands.Add(cmd);
                }
                _cachedCommands.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            }

            EditorGUILayout.LabelField($"{_cachedCommands.Count} commands", EditorStyles.miniLabel);

            _commandListScroll = EditorGUILayout.BeginScrollView(_commandListScroll);

            foreach (var cmd in _cachedCommands)
            {
                EditorGUILayout.BeginHorizontal("box");

                // Command info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(cmd.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(cmd.Description, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrEmpty(cmd.Usage))
                    EditorGUILayout.LabelField($"Usage: {cmd.Usage}", EditorStyles.miniLabel);

                // Flags (reuse StringBuilder to avoid per-command List<string> alloc)
                _flagsSb.Clear();
                if ((cmd.Flags & ConCommandFlags.RequiresPlayMode) != 0) _flagsSb.Append("PlayMode");
                if ((cmd.Flags & ConCommandFlags.ServerOnly) != 0)
                { if (_flagsSb.Length > 0) _flagsSb.Append(", "); _flagsSb.Append("Server"); }
                if ((cmd.Flags & ConCommandFlags.ReadOnly) != 0)
                { if (_flagsSb.Length > 0) _flagsSb.Append(", "); _flagsSb.Append("ReadOnly"); }
                if (_flagsSb.Length > 0)
                    EditorGUILayout.LabelField($"[{_flagsSb}]", EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();

                // Quick-run button
                if (GUILayout.Button("Run", GUILayout.Width(40), GUILayout.Height(30)))
                {
                    _quickCommand = cmd.Name;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHistory()
        {
            var history = DevConsoleService.Instance.History;
            EditorGUILayout.LabelField($"{history.Count} entries", EditorStyles.miniLabel);

            _historyScroll = EditorGUILayout.BeginScrollView(_historyScroll);

            // Show recent history (iterate backwards through output looking for > prefixed lines)
            var svc = DevConsoleService.Instance;
            for (int i = svc.OutputCount - 1; i >= 0; i--)
            {
                var entry = svc.GetOutput(i);
                if (entry.Text != null && entry.Text.StartsWith("> "))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.Text.Substring(2), EditorStyles.label);
                    if (GUILayout.Button("Re-run", GUILayout.Width(55)))
                    {
                        DevConsoleService.Instance.Execute(entry.Text.Substring(2));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawOutput()
        {
            var svc = DevConsoleService.Instance;
            EditorGUILayout.LabelField($"{svc.OutputCount} lines", EditorStyles.miniLabel);

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                svc.ClearOutput();

            _outputScroll = EditorGUILayout.BeginScrollView(_outputScroll);

            for (int i = 0; i < svc.OutputCount; i++)
            {
                var entry = svc.GetOutput(i);
                var prevColor = GUI.color;
                GUI.color = entry.Type switch
                {
                    LogType.Warning => Color.yellow,
                    LogType.Error or LogType.Exception or LogType.Assert => Color.red,
                    _ => Color.white
                };
                EditorGUILayout.LabelField(entry.Text ?? "", EditorStyles.label);
                GUI.color = prevColor;
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
