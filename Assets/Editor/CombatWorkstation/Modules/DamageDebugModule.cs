using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DIG.Editor.CombatWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CB-03: Damage Debug module.
    /// Damage calculation breakdown, modifier stack view, hit registration log.
    /// </summary>
    public class DamageDebugModule : ICombatModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _logScrollPosition;
        
        // Log entries
        private List<DamageLogEntry> _damageLog = new List<DamageLogEntry>();
        private int _maxLogEntries = 100;
        private bool _autoScroll = true;
        private bool _isPaused = false;
        
        // Filters
        private bool _showHits = true;
        private bool _showMisses = false;
        private bool _showCriticals = true;
        private bool _showKills = true;
        private float _minDamageFilter = 0f;
        private string _sourceFilter = "";
        private string _targetFilter = "";
        
        // Stats
        private float _totalDamageDealt = 0f;
        private int _totalHits = 0;
        private int _totalCriticals = 0;
        private int _totalKills = 0;
        private float _averageDamage = 0f;
        private float _dps = 0f;
        private float _sessionStartTime = 0f;
        
        // Damage calculation preview
        private float _baseDamage = 50f;
        private float _armorValue = 25f;
        private float _armorEffectiveness = 0.01f;
        private float _hitboxMultiplier = 1.0f;
        private float _criticalMultiplier = 2.0f;
        private float _damageResistance = 0f;
        private bool _isCritical = false;

        private class DamageLogEntry
        {
            public float Timestamp;
            public string Source;
            public string Target;
            public float BaseDamage;
            public float FinalDamage;
            public string HitRegion;
            public bool IsCritical;
            public bool IsKill;
            public bool IsHit;
            public List<DamageModifier> Modifiers = new List<DamageModifier>();
        }

        private class DamageModifier
        {
            public string Name;
            public float Value;
            public ModifierType Type;
        }

        private enum ModifierType { Additive, Multiplicative, Override }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Damage Debugger", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Monitor damage events in real-time. View modifier stacks and analyze damage calculations.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - damage log
            EditorGUILayout.BeginVertical(GUILayout.Width(400));
            DrawDamageLog();
            EditorGUILayout.EndVertical();

            // Right panel - stats and calculator
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawStats();
            EditorGUILayout.Space(10);
            DrawFilters();
            EditorGUILayout.Space(10);
            DrawDamageCalculator();
            EditorGUILayout.Space(10);
            DrawActions();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Poll for damage events in play mode
            if (Application.isPlaying && !_isPaused)
            {
                PollDamageEvents();
            }
        }

        private void DrawDamageLog()
        {
            EditorGUILayout.LabelField("Damage Log", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            // Controls
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _isPaused ? Color.green : Color.red;
            if (GUILayout.Button(_isPaused ? "▶ Resume" : "⏸ Pause", GUILayout.Width(80)))
            {
                _isPaused = !_isPaused;
            }
            GUI.backgroundColor = prevColor;
            
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                ClearLog();
            }
            
            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", GUILayout.Width(80));
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{_damageLog.Count} entries", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Log entries
            _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition);

            var filteredLog = GetFilteredLog();
            
            foreach (var entry in filteredLog)
            {
                DrawLogEntry(entry);
            }

            if (_autoScroll && filteredLog.Count > 0)
            {
                // Would scroll to bottom
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawLogEntry(DamageLogEntry entry)
        {
            Color bgColor = entry.IsKill ? new Color(0.5f, 0.2f, 0.2f) :
                           entry.IsCritical ? new Color(0.5f, 0.5f, 0.2f) :
                           entry.IsHit ? new Color(0.2f, 0.3f, 0.2f) :
                           new Color(0.3f, 0.3f, 0.3f);

            Rect rect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), 
                entry.IsKill ? Color.red : entry.IsCritical ? Color.yellow : Color.green);

            EditorGUILayout.LabelField($"[{entry.Timestamp:F2}s]", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField($"{entry.Source} → {entry.Target}", GUILayout.Width(150));
            
            string damageText = entry.IsHit ? $"{entry.FinalDamage:F0}" : "MISS";
            Color prevColor = GUI.color;
            GUI.color = entry.IsKill ? Color.red : entry.IsCritical ? Color.yellow : Color.white;
            EditorGUILayout.LabelField(damageText, EditorStyles.boldLabel, GUILayout.Width(50));
            GUI.color = prevColor;
            
            EditorGUILayout.LabelField(entry.HitRegion, EditorStyles.miniLabel, GUILayout.Width(60));
            
            if (entry.IsCritical) EditorGUILayout.LabelField("CRIT", EditorStyles.miniBoldLabel, GUILayout.Width(35));
            if (entry.IsKill) EditorGUILayout.LabelField("KILL", EditorStyles.miniBoldLabel, GUILayout.Width(35));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStats()
        {
            EditorGUILayout.LabelField("Session Stats", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            DrawStatBox("Total Hits", _totalHits.ToString(), Color.green);
            DrawStatBox("Criticals", _totalCriticals.ToString(), Color.yellow);
            DrawStatBox("Kills", _totalKills.ToString(), Color.red);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawStatBox("Total Damage", $"{_totalDamageDealt:F0}", Color.cyan);
            DrawStatBox("Avg Damage", $"{_averageDamage:F1}", Color.cyan);
            DrawStatBox("DPS", $"{_dps:F1}", Color.magenta);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStatBox(string label, string value, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
            
            Color prevColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(value, new GUIStyle(EditorStyles.boldLabel) 
            { 
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16 
            });
            GUI.color = prevColor;
            
            EditorGUILayout.EndVertical();
        }

        private void DrawFilters()
        {
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _showHits = EditorGUILayout.Toggle("Hits", _showHits, GUILayout.Width(60));
            _showMisses = EditorGUILayout.Toggle("Misses", _showMisses, GUILayout.Width(70));
            _showCriticals = EditorGUILayout.Toggle("Criticals", _showCriticals, GUILayout.Width(80));
            _showKills = EditorGUILayout.Toggle("Kills", _showKills, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            _minDamageFilter = EditorGUILayout.Slider("Min Damage", _minDamageFilter, 0f, 100f);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source:", GUILayout.Width(50));
            _sourceFilter = EditorGUILayout.TextField(_sourceFilter);
            EditorGUILayout.LabelField("Target:", GUILayout.Width(50));
            _targetFilter = EditorGUILayout.TextField(_targetFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDamageCalculator()
        {
            EditorGUILayout.LabelField("Damage Calculator", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Input Values", EditorStyles.miniLabel);
            _baseDamage = EditorGUILayout.FloatField("Base Damage", _baseDamage);
            _hitboxMultiplier = EditorGUILayout.Slider("Hitbox Multiplier", _hitboxMultiplier, 0.5f, 3f);
            _isCritical = EditorGUILayout.Toggle("Is Critical Hit", _isCritical);
            if (_isCritical)
            {
                _criticalMultiplier = EditorGUILayout.Slider("Critical Multiplier", _criticalMultiplier, 1.5f, 5f);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Target Defenses", EditorStyles.miniLabel);
            _armorValue = EditorGUILayout.FloatField("Armor Value", _armorValue);
            _armorEffectiveness = EditorGUILayout.Slider("Armor Effectiveness", _armorEffectiveness, 0.001f, 0.05f);
            _damageResistance = EditorGUILayout.Slider("Damage Resistance %", _damageResistance, 0f, 0.9f);

            EditorGUILayout.Space(10);

            // Calculate and show breakdown
            EditorGUILayout.LabelField("Damage Breakdown", EditorStyles.boldLabel);
            
            float damage = _baseDamage;
            EditorGUILayout.LabelField($"Base Damage: {damage:F1}");
            
            damage *= _hitboxMultiplier;
            EditorGUILayout.LabelField($"After Hitbox ({_hitboxMultiplier:F2}x): {damage:F1}");
            
            if (_isCritical)
            {
                damage *= _criticalMultiplier;
                EditorGUILayout.LabelField($"After Critical ({_criticalMultiplier:F1}x): {damage:F1}");
            }

            float armorReduction = _armorValue * _armorEffectiveness;
            damage *= (1f - armorReduction);
            EditorGUILayout.LabelField($"After Armor ({armorReduction * 100:F1}% reduction): {damage:F1}");

            damage *= (1f - _damageResistance);
            EditorGUILayout.LabelField($"After Resistance ({_damageResistance * 100:F0}% reduction): {damage:F1}");

            EditorGUILayout.Space(5);
            
            Color prevColor = GUI.color;
            GUI.color = Color.green;
            EditorGUILayout.LabelField($"FINAL DAMAGE: {damage:F1}", EditorStyles.boldLabel);
            GUI.color = prevColor;

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Log (CSV)"))
            {
                ExportLogToCSV();
            }
            
            if (GUILayout.Button("Add Test Entry"))
            {
                AddTestEntry();
            }
            
            if (GUILayout.Button("Reset Stats"))
            {
                ResetStats();
            }
            
            EditorGUILayout.EndHorizontal();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play mode to capture live damage events.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private List<DamageLogEntry> GetFilteredLog()
        {
            return _damageLog.Where(e =>
            {
                if (!_showHits && e.IsHit && !e.IsCritical && !e.IsKill) return false;
                if (!_showMisses && !e.IsHit) return false;
                if (!_showCriticals && e.IsCritical) return false;
                if (!_showKills && e.IsKill) return false;
                if (e.FinalDamage < _minDamageFilter) return false;
                if (!string.IsNullOrEmpty(_sourceFilter) && 
                    !e.Source.ToLower().Contains(_sourceFilter.ToLower())) return false;
                if (!string.IsNullOrEmpty(_targetFilter) && 
                    !e.Target.ToLower().Contains(_targetFilter.ToLower())) return false;
                return true;
            }).ToList();
        }

        private void PollDamageEvents()
        {
            // In a real implementation, this would subscribe to damage events from ECS
            // For now, this is a placeholder that would hook into the damage system
        }

        private void AddTestEntry()
        {
            float timestamp = Application.isPlaying ? Time.time : _damageLog.Count * 0.5f;
            bool isCrit = Random.value > 0.8f;
            bool isKill = Random.value > 0.95f;
            float damage = Random.Range(20f, 100f) * (isCrit ? 2f : 1f);

            var entry = new DamageLogEntry
            {
                Timestamp = timestamp,
                Source = "Player",
                Target = $"Enemy_{Random.Range(1, 10)}",
                BaseDamage = damage / (isCrit ? 2f : 1f),
                FinalDamage = damage,
                HitRegion = isCrit ? "Head" : "Torso",
                IsCritical = isCrit,
                IsKill = isKill,
                IsHit = true,
                Modifiers = new List<DamageModifier>
                {
                    new DamageModifier { Name = "Hitbox", Value = isCrit ? 2f : 1f, Type = ModifierType.Multiplicative }
                }
            };

            AddLogEntry(entry);
        }

        private void AddLogEntry(DamageLogEntry entry)
        {
            _damageLog.Add(entry);
            
            if (_damageLog.Count > _maxLogEntries)
            {
                _damageLog.RemoveAt(0);
            }

            // Update stats
            if (entry.IsHit)
            {
                _totalHits++;
                _totalDamageDealt += entry.FinalDamage;
                _averageDamage = _totalDamageDealt / _totalHits;
                
                if (entry.IsCritical) _totalCriticals++;
                if (entry.IsKill) _totalKills++;

                float sessionDuration = entry.Timestamp - _sessionStartTime;
                if (sessionDuration > 0)
                {
                    _dps = _totalDamageDealt / sessionDuration;
                }
            }
        }

        private void ClearLog()
        {
            _damageLog.Clear();
        }

        private void ResetStats()
        {
            _totalDamageDealt = 0;
            _totalHits = 0;
            _totalCriticals = 0;
            _totalKills = 0;
            _averageDamage = 0;
            _dps = 0;
            _sessionStartTime = Application.isPlaying ? Time.time : 0;
        }

        private void ExportLogToCSV()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Source,Target,BaseDamage,FinalDamage,HitRegion,IsCritical,IsKill");

            foreach (var entry in _damageLog)
            {
                sb.AppendLine($"{entry.Timestamp:F2},{entry.Source},{entry.Target}," +
                    $"{entry.BaseDamage:F1},{entry.FinalDamage:F1},{entry.HitRegion}," +
                    $"{entry.IsCritical},{entry.IsKill}");
            }

            string path = EditorUtility.SaveFilePanel("Export Damage Log", "", 
                $"DamageLog_{System.DateTime.Now:yyyyMMdd_HHmmss}", "csv");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, sb.ToString());
                Debug.Log($"[DamageDebug] Exported {_damageLog.Count} entries to {path}");
            }
        }
    }
}
