#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Roguelite.Editor
{
    /// <summary>
    /// EPIC 23.6: Run Configuration Workstation.
    /// Central editor window for rogue-lite content design: zone sequences, encounter pools,
    /// rewards, modifiers, meta-tree, and run simulation.
    /// Follows CombatWorkstation pattern (sidebar tabs, module dispatch).
    /// EPIC 23.7: Added shared RogueliteDataContext, 6 new modules (Blueprint, Balance,
    /// Coverage, Live Tuning, Dependency Graph, Templates).
    /// </summary>
    public class RunWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private string[] _tabNames;
        private Dictionary<string, IRunWorkstationModule> _modules;
        private Vector2 _sidebarScroll;

        // EPIC 23.7: Shared data context for all modules
        private RogueliteDataContext _context;

        [MenuItem("DIG/Run Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<RunWorkstationWindow>("Run Workstation");
            window.minSize = new Vector2(750, 600);
        }

        private void OnEnable()
        {
            _context = new RogueliteDataContext();
            _context.Build();
            RogueliteAssetPostprocessor.SharedContext = _context;
            InitializeModules();
        }

        private void OnDisable()
        {
            if (_modules == null) return;
            foreach (var module in _modules.Values)
                module.OnDisable();
            RogueliteAssetPostprocessor.SharedContext = null;
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, IRunWorkstationModule>();

            // EPIC 23.6 modules
            AddModule(new Modules.ZoneSequenceModule());
            AddModule(new Modules.EncounterPoolModule());
            AddModule(new Modules.RewardModule());
            AddModule(new Modules.ModifierModule());
            AddModule(new Modules.MetaTreeModule());

            // EPIC 23.7 modules
            AddModule(new Modules.RunBlueprintModule());
            AddModule(new Modules.BalanceDashboardModule());
            AddModule(new Modules.ContentCoverageModule());
            AddModule(new Modules.LiveTuningModule());
            AddModule(new Modules.DependencyGraphModule());
            AddModule(new Modules.TemplateLibraryModule());

            _tabNames = new string[_modules.Count];
            int i = 0;
            foreach (var name in _modules.Keys)
                _tabNames[i++] = name;

            foreach (var module in _modules.Values)
            {
                module.OnEnable();
                module.SetContext(_context);
            }
        }

        private void AddModule(IRunWorkstationModule module)
        {
            _modules[module.TabName] = module;
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(140), GUILayout.ExpandHeight(true));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            _selectedTab = GUILayout.SelectionGrid(_selectedTab, _tabNames, 1, EditorStyles.miniButton);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Content
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            if (_modules != null && _selectedTab < _tabNames.Length)
            {
                string currentTab = _tabNames[_selectedTab];
                if (_modules.TryGetValue(currentTab, out var module))
                    module.OnGUI();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("DIG Run Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Rebuild context if invalidated
            if (_context != null && !_context.IsBuilt)
            {
                _context.Build();
                foreach (var module in _modules.Values)
                    module.SetContext(_context);
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
            {
                if (_modules != null)
                    foreach (var module in _modules.Values)
                        module.OnDisable();
                _context?.Invalidate();
                _context?.Build();
                RogueliteAssetPostprocessor.SharedContext = _context;
                InitializeModules();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
