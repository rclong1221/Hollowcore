using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DIG.Weapons.Authoring;
using DIG.Items.Authoring;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 EW-04: Weapon Stats Dashboard module.
    /// Side-by-side comparison, DPS/TTK calculators, balance heatmap, CSV export.
    /// </summary>
    public class StatsDashboardModule : IEquipmentModule
    {
        private Vector2 _scrollPosition;
        private List<WeaponStats> _weaponList = new List<WeaponStats>();
        private bool _hasLoaded = false;
        
        // Comparison
        private int _selectedWeapon1 = -1;
        private int _selectedWeapon2 = -1;
        
        // Analysis settings
        private float _targetHealth = 100f;
        private float _targetArmor = 0f;
        private float _headshotPercentage = 0.2f;
        private float _hitAccuracy = 0.7f;
        private float _targetDistance = 20f;
        
        // Sorting
        private enum SortMode { Name, DPS, TTK, Damage, FireRate }
        private SortMode _sortMode = SortMode.DPS;
        private bool _sortAscending = false;

        private struct WeaponStats
        {
            public string Name;
            public GameObject Prefab;
            public float Damage;
            public int FireRate;
            public int ClipSize;
            public float ReloadTime;
            public float Range;
            public float BaseSpread;
            public bool IsHitscan;
            
            // Calculated
            public float TheoreticalDPS;
            public float EffectiveDPS;
            public float TTK;
            public float SustainedDPS;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Weapon Stats Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Analyze and compare weapon statistics. Calculate DPS, TTK, and identify balance issues.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            DrawToolbar();
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (!_hasLoaded)
            {
                EditorGUILayout.HelpBox("Click 'Load Weapons' to scan for weapon prefabs.", MessageType.Info);
            }
            else if (_weaponList.Count == 0)
            {
                EditorGUILayout.HelpBox("No weapons found. Make sure prefabs have WeaponAuthoring components.", MessageType.Warning);
            }
            else
            {
                DrawAnalysisSettings();
                EditorGUILayout.Space(10);
                DrawWeaponTable();
                EditorGUILayout.Space(10);
                DrawComparison();
                EditorGUILayout.Space(10);
                DrawBalanceHeatmap();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Load Weapons", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                LoadWeapons();
            }

            if (GUILayout.Button("Refresh Stats", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                RecalculateStats();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Sort:", GUILayout.Width(35));
            _sortMode = (SortMode)EditorGUILayout.EnumPopup(_sortMode, EditorStyles.toolbarPopup, GUILayout.Width(80));
            
            if (GUILayout.Button(_sortAscending ? "↑" : "↓", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _sortAscending = !_sortAscending;
                SortWeapons();
            }

            if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ExportCSV();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAnalysisSettings()
        {
            EditorGUILayout.LabelField("Analysis Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _targetHealth = EditorGUILayout.FloatField("Target Health", _targetHealth, GUILayout.Width(200));
            _targetArmor = EditorGUILayout.FloatField("Target Armor", _targetArmor, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _headshotPercentage = EditorGUILayout.Slider("Headshot %", _headshotPercentage, 0f, 1f, GUILayout.Width(200));
            _hitAccuracy = EditorGUILayout.Slider("Hit Accuracy", _hitAccuracy, 0.1f, 1f, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            _targetDistance = EditorGUILayout.Slider("Target Distance (m)", _targetDistance, 1f, 100f);

            if (GUILayout.Button("Recalculate", GUILayout.Width(100)))
            {
                RecalculateStats();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWeaponTable()
        {
            EditorGUILayout.LabelField($"Weapons ({_weaponList.Count})", EditorStyles.boldLabel);
            
            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("", GUILayout.Width(20)); // Selection
            GUILayout.Label("Name", EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label("DMG", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("RPM", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Clip", EditorStyles.boldLabel, GUILayout.Width(40));
            GUILayout.Label("Reload", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Range", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("DPS", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Eff DPS", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("TTK", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Sustain", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Rows
            for (int i = 0; i < _weaponList.Count; i++)
            {
                var w = _weaponList[i];
                bool isSelected = i == _selectedWeapon1 || i == _selectedWeapon2;
                
                GUI.backgroundColor = isSelected ? new Color(0.3f, 0.5f, 0.8f) : Color.white;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;

                // Selection checkbox
                bool sel1 = i == _selectedWeapon1;
                bool sel2 = i == _selectedWeapon2;
                
                EditorGUILayout.BeginVertical(GUILayout.Width(20));
                if (GUILayout.Toggle(sel1, "1", EditorStyles.miniButton, GUILayout.Width(18)))
                {
                    _selectedWeapon1 = sel1 ? -1 : i;
                }
                if (GUILayout.Toggle(sel2, "2", EditorStyles.miniButton, GUILayout.Width(18)))
                {
                    _selectedWeapon2 = sel2 ? -1 : i;
                }
                EditorGUILayout.EndVertical();

                // Data columns
                if (GUILayout.Button(w.Name, EditorStyles.label, GUILayout.Width(120)))
                {
                    Selection.activeGameObject = w.Prefab;
                }
                GUILayout.Label($"{w.Damage:F0}", GUILayout.Width(50));
                GUILayout.Label($"{w.FireRate}", GUILayout.Width(50));
                GUILayout.Label($"{w.ClipSize}", GUILayout.Width(40));
                GUILayout.Label($"{w.ReloadTime:F1}s", GUILayout.Width(50));
                GUILayout.Label($"{w.Range:F0}m", GUILayout.Width(50));
                
                // Highlight extreme values
                GUI.color = GetDPSColor(w.TheoreticalDPS);
                GUILayout.Label($"{w.TheoreticalDPS:F0}", GUILayout.Width(60));
                GUI.color = GetDPSColor(w.EffectiveDPS);
                GUILayout.Label($"{w.EffectiveDPS:F0}", GUILayout.Width(60));
                GUI.color = GetTTKColor(w.TTK);
                GUILayout.Label($"{w.TTK:F2}s", GUILayout.Width(50));
                GUI.color = GetDPSColor(w.SustainedDPS);
                GUILayout.Label($"{w.SustainedDPS:F0}", GUILayout.Width(60));
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawComparison()
        {
            if (_selectedWeapon1 < 0 && _selectedWeapon2 < 0) return;

            EditorGUILayout.LabelField("Comparison", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_selectedWeapon1 >= 0 && _selectedWeapon2 >= 0)
            {
                var w1 = _weaponList[_selectedWeapon1];
                var w2 = _weaponList[_selectedWeapon2];

                DrawComparisonRow("Weapon", w1.Name, w2.Name, false);
                DrawComparisonRow("Damage", $"{w1.Damage:F0}", $"{w2.Damage:F0}", w1.Damage > w2.Damage);
                DrawComparisonRow("Fire Rate", $"{w1.FireRate}", $"{w2.FireRate}", w1.FireRate > w2.FireRate);
                DrawComparisonRow("DPS", $"{w1.TheoreticalDPS:F0}", $"{w2.TheoreticalDPS:F0}", w1.TheoreticalDPS > w2.TheoreticalDPS);
                DrawComparisonRow("Effective DPS", $"{w1.EffectiveDPS:F0}", $"{w2.EffectiveDPS:F0}", w1.EffectiveDPS > w2.EffectiveDPS);
                DrawComparisonRow("TTK", $"{w1.TTK:F2}s", $"{w2.TTK:F2}s", w1.TTK < w2.TTK);
                DrawComparisonRow("Sustained DPS", $"{w1.SustainedDPS:F0}", $"{w2.SustainedDPS:F0}", w1.SustainedDPS > w2.SustainedDPS);
                DrawComparisonRow("Clip Size", $"{w1.ClipSize}", $"{w2.ClipSize}", w1.ClipSize > w2.ClipSize);
                DrawComparisonRow("Reload", $"{w1.ReloadTime:F1}s", $"{w2.ReloadTime:F1}s", w1.ReloadTime < w2.ReloadTime);
                DrawComparisonRow("Range", $"{w1.Range:F0}m", $"{w2.Range:F0}m", w1.Range > w2.Range);
            }
            else
            {
                int idx = _selectedWeapon1 >= 0 ? _selectedWeapon1 : _selectedWeapon2;
                var w = _weaponList[idx];
                
                EditorGUILayout.LabelField($"Selected: {w.Name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Theoretical DPS: {w.TheoreticalDPS:F0}");
                EditorGUILayout.LabelField($"Effective DPS (with accuracy/headshots): {w.EffectiveDPS:F0}");
                EditorGUILayout.LabelField($"Time to Kill: {w.TTK:F2}s");
                EditorGUILayout.LabelField($"Sustained DPS (with reloads): {w.SustainedDPS:F0}");
                
                EditorGUILayout.HelpBox("Select a second weapon to compare.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComparisonRow(string label, string val1, string val2, bool firstIsBetter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            
            GUI.color = firstIsBetter ? Color.green : Color.white;
            GUILayout.Label(val1, EditorStyles.boldLabel, GUILayout.Width(80));
            
            GUI.color = !firstIsBetter ? Color.green : Color.white;
            GUILayout.Label(val2, EditorStyles.boldLabel, GUILayout.Width(80));
            
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBalanceHeatmap()
        {
            EditorGUILayout.LabelField("Balance Heatmap", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_weaponList.Count < 2)
            {
                EditorGUILayout.HelpBox("Need at least 2 weapons for heatmap analysis.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // Calculate averages
            float avgDPS = _weaponList.Average(w => w.TheoreticalDPS);
            float avgTTK = _weaponList.Average(w => w.TTK);
            
            float dpsStdDev = Mathf.Sqrt(_weaponList.Average(w => Mathf.Pow(w.TheoreticalDPS - avgDPS, 2)));
            float ttkStdDev = Mathf.Sqrt(_weaponList.Average(w => Mathf.Pow(w.TTK - avgTTK, 2)));

            EditorGUILayout.LabelField($"Average DPS: {avgDPS:F0} (σ={dpsStdDev:F0})");
            EditorGUILayout.LabelField($"Average TTK: {avgTTK:F2}s (σ={ttkStdDev:F2})");
            EditorGUILayout.Space(5);

            // Find outliers
            var outliers = _weaponList.Where(w => 
                Mathf.Abs(w.TheoreticalDPS - avgDPS) > dpsStdDev * 1.5f ||
                Mathf.Abs(w.TTK - avgTTK) > ttkStdDev * 1.5f
            ).ToList();

            if (outliers.Count > 0)
            {
                EditorGUILayout.LabelField("⚠️ Potential Balance Issues:", EditorStyles.boldLabel);
                foreach (var o in outliers)
                {
                    float dpsDiff = (o.TheoreticalDPS - avgDPS) / avgDPS * 100;
                    string status = dpsDiff > 0 ? "OVERPOWERED" : "UNDERPOWERED";
                    GUI.color = dpsDiff > 0 ? new Color(1, 0.5f, 0.5f) : new Color(0.5f, 0.5f, 1f);
                    EditorGUILayout.LabelField($"  • {o.Name}: {status} ({dpsDiff:+0;-0}% DPS)");
                    GUI.color = Color.white;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("All weapons are within balanced range.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private Color GetDPSColor(float dps)
        {
            if (_weaponList.Count == 0) return Color.white;
            float max = _weaponList.Max(w => w.TheoreticalDPS);
            float min = _weaponList.Min(w => w.TheoreticalDPS);
            float t = (dps - min) / Mathf.Max(1, max - min);
            return Color.Lerp(new Color(0.5f, 0.5f, 1f), new Color(1f, 0.5f, 0.5f), t);
        }

        private Color GetTTKColor(float ttk)
        {
            if (_weaponList.Count == 0) return Color.white;
            float max = _weaponList.Max(w => w.TTK);
            float min = _weaponList.Min(w => w.TTK);
            float t = (ttk - min) / Mathf.Max(0.01f, max - min);
            return Color.Lerp(new Color(1f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 1f), t);
        }

        private void LoadWeapons()
        {
            _weaponList.Clear();
            
            string[] searchFolders = new[] { "Assets/Content/Weapons", "Assets/DIG/Items", "Assets/Prefabs" };
            var validFolders = searchFolders.Where(f => System.IO.Directory.Exists(f)).ToArray();
            
            if (validFolders.Length == 0)
            {
                validFolders = new[] { "Assets" };
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", validFolders);

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;
                
                var authoring = prefab.GetComponent<WeaponAuthoring>();
                if (authoring == null) continue;

                var stats = new WeaponStats
                {
                    Name = prefab.name,
                    Prefab = prefab,
                    Damage = authoring.Damage,
                    FireRate = (int)authoring.FireRate,
                    ClipSize = authoring.ClipSize,
                    ReloadTime = authoring.ReloadTime,
                    Range = authoring.Range,
                    BaseSpread = authoring.SpreadAngle,
                    IsHitscan = authoring.UseHitscan
                };

                _weaponList.Add(stats);
            }

            RecalculateStats();
            SortWeapons();
            _hasLoaded = true;

            Debug.Log($"[StatsDashboard] Loaded {_weaponList.Count} weapons");
        }

        private void RecalculateStats()
        {
            for (int i = 0; i < _weaponList.Count; i++)
            {
                var w = _weaponList[i];

                // Theoretical DPS (perfect accuracy, no reloads)
                float rps = w.FireRate / 60f;
                w.TheoreticalDPS = w.Damage * rps;

                // Effective DPS (with accuracy and headshots)
                float headshotMultiplier = 2f;
                float avgDamagePerShot = w.Damage * (1 - _headshotPercentage) + 
                                          w.Damage * headshotMultiplier * _headshotPercentage;
                w.EffectiveDPS = avgDamagePerShot * rps * _hitAccuracy;

                // TTK (time to kill target)
                float effectiveHealth = _targetHealth + _targetArmor;
                float shotsToKill = Mathf.Ceil(effectiveHealth / (avgDamagePerShot * _hitAccuracy));
                w.TTK = (shotsToKill - 1) / rps; // First shot is instant

                // Sustained DPS (with reloads)
                float clipDuration = w.ClipSize / rps;
                float cycleDuration = clipDuration + w.ReloadTime;
                float damagePerCycle = w.Damage * w.ClipSize;
                w.SustainedDPS = damagePerCycle / cycleDuration;

                _weaponList[i] = w;
            }
        }

        private void SortWeapons()
        {
            switch (_sortMode)
            {
                case SortMode.Name:
                    _weaponList = _sortAscending 
                        ? _weaponList.OrderBy(w => w.Name).ToList()
                        : _weaponList.OrderByDescending(w => w.Name).ToList();
                    break;
                case SortMode.DPS:
                    _weaponList = _sortAscending 
                        ? _weaponList.OrderBy(w => w.TheoreticalDPS).ToList()
                        : _weaponList.OrderByDescending(w => w.TheoreticalDPS).ToList();
                    break;
                case SortMode.TTK:
                    _weaponList = _sortAscending 
                        ? _weaponList.OrderBy(w => w.TTK).ToList()
                        : _weaponList.OrderByDescending(w => w.TTK).ToList();
                    break;
                case SortMode.Damage:
                    _weaponList = _sortAscending 
                        ? _weaponList.OrderBy(w => w.Damage).ToList()
                        : _weaponList.OrderByDescending(w => w.Damage).ToList();
                    break;
                case SortMode.FireRate:
                    _weaponList = _sortAscending 
                        ? _weaponList.OrderBy(w => w.FireRate).ToList()
                        : _weaponList.OrderByDescending(w => w.FireRate).ToList();
                    break;
            }
        }

        private void ExportCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Weapon Stats", "", "weapon_stats", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Name,Damage,FireRate,ClipSize,ReloadTime,Range,Spread,TheoreticalDPS,EffectiveDPS,TTK,SustainedDPS");

            foreach (var w in _weaponList)
            {
                sb.AppendLine($"{w.Name},{w.Damage},{w.FireRate},{w.ClipSize},{w.ReloadTime},{w.Range},{w.BaseSpread},{w.TheoreticalDPS:F1},{w.EffectiveDPS:F1},{w.TTK:F2},{w.SustainedDPS:F1}");
            }

            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"[StatsDashboard] Exported to: {path}");
        }
    }
}
