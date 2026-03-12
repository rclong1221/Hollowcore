using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.DebugWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 DW-03: Damage Log module.
    /// Real-time damage event logging, export to CSV.
    /// </summary>
    public class DamageLogModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _logScrollPosition;
        
        // Logging state
        private bool _isLogging = true;
        private int _maxLogEntries = 500;
        
        // Log entries
        private List<DamageLogEntry> _logEntries = new List<DamageLogEntry>();
        
        // Filters
        private string _sourceFilter = "";
        private string _targetFilter = "";
        private DamageType _typeFilter = DamageType.All;
        private float _minDamageFilter = 0f;
        private bool _showCritsOnly = false;
        
        // Statistics
        private float _sessionStartTime = 0f;
        private float _totalDamageDealt = 0f;
        private float _totalDamageReceived = 0f;
        private int _totalHits = 0;
        private int _critHits = 0;
        private int _headshotHits = 0;

        private enum DamageType
        {
            All,
            Bullet,
            Explosion,
            Melee,
            Fall,
            Fire,
            Poison,
            Other
        }

        [System.Serializable]
        private class DamageLogEntry
        {
            public float Timestamp;
            public string Source;
            public string Target;
            public DamageType Type;
            public float RawDamage;
            public float FinalDamage;
            public string BodyPart;
            public bool IsCritical;
            public bool IsHeadshot;
            public bool IsKill;
            public float Distance;
        }

        public DamageLogModule()
        {
            InitializeSimulatedData();
            _sessionStartTime = Time.realtimeSinceStartup;
        }

        private void InitializeSimulatedData()
        {
            // Add some sample entries for demonstration
            _logEntries = new List<DamageLogEntry>
            {
                new DamageLogEntry { Timestamp = 0.5f, Source = "Player", Target = "Enemy_01", Type = DamageType.Bullet, RawDamage = 35, FinalDamage = 35, BodyPart = "Chest", Distance = 15.2f },
                new DamageLogEntry { Timestamp = 0.8f, Source = "Player", Target = "Enemy_01", Type = DamageType.Bullet, RawDamage = 35, FinalDamage = 70, BodyPart = "Head", IsCritical = true, IsHeadshot = true, Distance = 14.8f },
                new DamageLogEntry { Timestamp = 1.2f, Source = "Player", Target = "Enemy_02", Type = DamageType.Bullet, RawDamage = 35, FinalDamage = 35, BodyPart = "Arm", Distance = 22.1f },
                new DamageLogEntry { Timestamp = 2.0f, Source = "Enemy_03", Target = "Player", Type = DamageType.Bullet, RawDamage = 25, FinalDamage = 18, BodyPart = "Chest", Distance = 18.5f },
                new DamageLogEntry { Timestamp = 2.5f, Source = "Player", Target = "Enemy_02", Type = DamageType.Bullet, RawDamage = 35, FinalDamage = 35, BodyPart = "Chest", Distance = 20.3f, IsKill = true },
                new DamageLogEntry { Timestamp = 3.1f, Source = "Grenade", Target = "Enemy_04", Type = DamageType.Explosion, RawDamage = 150, FinalDamage = 120, BodyPart = "Full", Distance = 5.0f, IsKill = true },
            };
            
            UpdateStatistics();
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Damage Log", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to capture live damage events. Showing sample data.",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawLoggingControls();
            EditorGUILayout.Space(10);
            DrawStatistics();
            EditorGUILayout.Space(10);
            DrawFilters();
            EditorGUILayout.Space(10);
            DrawLogTable();
            EditorGUILayout.Space(10);
            DrawExportOptions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawLoggingControls()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isLogging ? Color.red : Color.green;
            
            if (GUILayout.Button(_isLogging ? "● Recording" : "○ Start", 
                EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                _isLogging = !_isLogging;
            }
            
            GUI.backgroundColor = prevColor;
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Clear Log", EditorStyles.toolbarButton))
            {
                ClearLog();
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField($"Entries: {_logEntries.Count}/{_maxLogEntries}", GUILayout.Width(120));
            
            _maxLogEntries = EditorGUILayout.IntField(_maxLogEntries, GUILayout.Width(60));
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatistics()
        {
            EditorGUILayout.LabelField("Session Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Damage dealt
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Damage Dealt", EditorStyles.centeredGreyMiniLabel);
            GUI.color = Color.green;
            EditorGUILayout.LabelField($"{_totalDamageDealt:F0}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Damage received
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Damage Taken", EditorStyles.centeredGreyMiniLabel);
            GUI.color = Color.red;
            EditorGUILayout.LabelField($"{_totalDamageReceived:F0}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Total hits
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Total Hits", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField(_totalHits.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            // Crit rate
            float critRate = _totalHits > 0 ? (_critHits / (float)_totalHits) * 100f : 0f;
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Crit Rate", EditorStyles.centeredGreyMiniLabel);
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField($"{critRate:F1}%", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // Headshot rate
            float hsRate = _totalHits > 0 ? (_headshotHits / (float)_totalHits) * 100f : 0f;
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("HS Rate", EditorStyles.centeredGreyMiniLabel);
            GUI.color = Color.cyan;
            EditorGUILayout.LabelField($"{hsRate:F1}%", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();
            
            // DPS
            float sessionTime = Time.realtimeSinceStartup - _sessionStartTime;
            float dps = sessionTime > 0 ? _totalDamageDealt / sessionTime : 0f;
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("DPS", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"{dps:F1}", 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawFilters()
        {
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            _sourceFilter = EditorGUILayout.TextField("Source", _sourceFilter, GUILayout.Width(150));
            _targetFilter = EditorGUILayout.TextField("Target", _targetFilter, GUILayout.Width(150));
            _typeFilter = (DamageType)EditorGUILayout.EnumPopup(_typeFilter, GUILayout.Width(100));
            
            EditorGUILayout.LabelField("Min:", GUILayout.Width(30));
            _minDamageFilter = EditorGUILayout.FloatField(_minDamageFilter, GUILayout.Width(50));
            
            _showCritsOnly = EditorGUILayout.Toggle("Crits Only", _showCritsOnly);
            
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _sourceFilter = "";
                _targetFilter = "";
                _typeFilter = DamageType.All;
                _minDamageFilter = 0f;
                _showCritsOnly = false;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLogTable()
        {
            EditorGUILayout.LabelField("Damage Events", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Time", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Body", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Dist", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Flags", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Filtered entries
            var filtered = GetFilteredEntries();

            _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition, GUILayout.Height(200));

            foreach (var entry in filtered.TakeLast(100).Reverse())
            {
                DrawLogRow(entry);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField($"Showing {Mathf.Min(filtered.Count(), 100)} of {filtered.Count()} entries", 
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private IEnumerable<DamageLogEntry> GetFilteredEntries()
        {
            return _logEntries
                .Where(e => string.IsNullOrEmpty(_sourceFilter) || 
                           e.Source.ToLower().Contains(_sourceFilter.ToLower()))
                .Where(e => string.IsNullOrEmpty(_targetFilter) || 
                           e.Target.ToLower().Contains(_targetFilter.ToLower()))
                .Where(e => _typeFilter == DamageType.All || e.Type == _typeFilter)
                .Where(e => e.FinalDamage >= _minDamageFilter)
                .Where(e => !_showCritsOnly || e.IsCritical);
        }

        private void DrawLogRow(DamageLogEntry entry)
        {
            // Row color based on source
            Color rowColor = entry.Source == "Player" ? new Color(0.2f, 0.3f, 0.2f) : new Color(0.3f, 0.2f, 0.2f);
            
            Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(18), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rowRect, rowColor);

            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField($"{entry.Timestamp:F1}s", GUILayout.Width(50));
            
            GUI.color = entry.Source == "Player" ? Color.green : Color.red;
            EditorGUILayout.LabelField(entry.Source, GUILayout.Width(80));
            GUI.color = Color.white;
            
            EditorGUILayout.LabelField(entry.Target, GUILayout.Width(80));
            
            // Type with color
            Color typeColor = entry.Type switch
            {
                DamageType.Bullet => Color.yellow,
                DamageType.Explosion => new Color(1f, 0.5f, 0f),
                DamageType.Melee => Color.red,
                DamageType.Fire => new Color(1f, 0.3f, 0f),
                _ => Color.white
            };
            GUI.color = typeColor;
            EditorGUILayout.LabelField(entry.Type.ToString(), GUILayout.Width(70));
            GUI.color = Color.white;
            
            // Damage with crit highlight
            GUI.color = entry.IsCritical ? Color.yellow : Color.white;
            EditorGUILayout.LabelField($"{entry.FinalDamage:F0}", GUILayout.Width(60));
            GUI.color = Color.white;
            
            // Body part with headshot highlight
            GUI.color = entry.IsHeadshot ? Color.cyan : Color.white;
            EditorGUILayout.LabelField(entry.BodyPart, GUILayout.Width(60));
            GUI.color = Color.white;
            
            EditorGUILayout.LabelField($"{entry.Distance:F0}m", GUILayout.Width(50));
            
            // Flags
            string flags = "";
            if (entry.IsCritical) flags += "⚡";
            if (entry.IsHeadshot) flags += "🎯";
            if (entry.IsKill) flags += "💀";
            EditorGUILayout.LabelField(flags);
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExportOptions()
        {
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export to CSV"))
            {
                ExportToCSV();
            }
            
            if (GUILayout.Button("Export Filtered"))
            {
                ExportFilteredToCSV();
            }
            
            if (GUILayout.Button("Copy to Clipboard"))
            {
                CopyToClipboard();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void UpdateStatistics()
        {
            _totalDamageDealt = _logEntries
                .Where(e => e.Source == "Player")
                .Sum(e => e.FinalDamage);
            
            _totalDamageReceived = _logEntries
                .Where(e => e.Target == "Player")
                .Sum(e => e.FinalDamage);
            
            _totalHits = _logEntries.Count(e => e.Source == "Player");
            _critHits = _logEntries.Count(e => e.Source == "Player" && e.IsCritical);
            _headshotHits = _logEntries.Count(e => e.Source == "Player" && e.IsHeadshot);
        }

        private void ClearLog()
        {
            _logEntries.Clear();
            _totalDamageDealt = 0;
            _totalDamageReceived = 0;
            _totalHits = 0;
            _critHits = 0;
            _headshotHits = 0;
            _sessionStartTime = Time.realtimeSinceStartup;
        }

        private void ExportToCSV()
        {
            string csv = "Timestamp,Source,Target,Type,RawDamage,FinalDamage,BodyPart,IsCritical,IsHeadshot,IsKill,Distance\n";
            
            foreach (var entry in _logEntries)
            {
                csv += $"{entry.Timestamp:F2},{entry.Source},{entry.Target},{entry.Type},{entry.RawDamage},{entry.FinalDamage},{entry.BodyPart},{entry.IsCritical},{entry.IsHeadshot},{entry.IsKill},{entry.Distance:F1}\n";
            }
            
            string path = EditorUtility.SaveFilePanel("Export Damage Log", "", "damage_log.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, csv);
                Debug.Log($"[DamageLog] Exported {_logEntries.Count} entries to {path}");
            }
        }

        private void ExportFilteredToCSV()
        {
            var filtered = GetFilteredEntries().ToList();
            
            string csv = "Timestamp,Source,Target,Type,RawDamage,FinalDamage,BodyPart,IsCritical,IsHeadshot,IsKill,Distance\n";
            
            foreach (var entry in filtered)
            {
                csv += $"{entry.Timestamp:F2},{entry.Source},{entry.Target},{entry.Type},{entry.RawDamage},{entry.FinalDamage},{entry.BodyPart},{entry.IsCritical},{entry.IsHeadshot},{entry.IsKill},{entry.Distance:F1}\n";
            }
            
            string path = EditorUtility.SaveFilePanel("Export Filtered Log", "", "damage_log_filtered.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, csv);
                Debug.Log($"[DamageLog] Exported {filtered.Count} filtered entries to {path}");
            }
        }

        private void CopyToClipboard()
        {
            string text = "";
            foreach (var entry in _logEntries.TakeLast(50))
            {
                text += $"[{entry.Timestamp:F1}s] {entry.Source} → {entry.Target}: {entry.FinalDamage} ({entry.Type})\n";
            }
            
            GUIUtility.systemCopyBuffer = text;
            Debug.Log("[DamageLog] Copied to clipboard");
        }
    }
}
