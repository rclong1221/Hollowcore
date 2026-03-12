using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.DebugWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 DW-04: A/B Testing module.
    /// Compare two weapon configs side-by-side.
    /// </summary>
    public class ABTestingModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        
        // Config slots
        private WeaponConfig _configA = new WeaponConfig();
        private WeaponConfig _configB = new WeaponConfig();
        
        // Comparison results
        private List<ComparisonResult> _comparisons = new List<ComparisonResult>();
        
        // Test settings
        private int _testIterations = 1000;
        private float _testDistance = 20f;
        private bool _includeRecoil = true;
        private bool _includeSpread = true;

        [System.Serializable]
        private class WeaponConfig
        {
            public string Name = "Weapon";
            public Object SourceAsset;
            
            // Stats
            public float Damage = 35f;
            public float FireRate = 600f;
            public float ReloadTime = 2.5f;
            public int MagazineSize = 30;
            public float Range = 100f;
            public float Spread = 2f;
            public float RecoilStrength = 5f;
            public float HeadshotMultiplier = 2f;
            
            // Calculated
            public float DPS => (Damage * (FireRate / 60f));
            public float TTK100HP => 100f / DPS;
            public float DamagePerMag => Damage * MagazineSize;
        }

        [System.Serializable]
        private class ComparisonResult
        {
            public string StatName;
            public float ValueA;
            public float ValueB;
            public float Difference;
            public float DifferencePercent;
            public ComparisonWinner Winner;
        }

        private enum ComparisonWinner
        {
            None,
            A,
            B,
            Equal
        }

        public ABTestingModule()
        {
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            _configA = new WeaponConfig
            {
                Name = "AK-47",
                Damage = 35f,
                FireRate = 600f,
                ReloadTime = 2.5f,
                MagazineSize = 30,
                Range = 100f,
                Spread = 2.5f,
                RecoilStrength = 6f,
                HeadshotMultiplier = 2f
            };
            
            _configB = new WeaponConfig
            {
                Name = "M4A1",
                Damage = 28f,
                FireRate = 750f,
                ReloadTime = 2.2f,
                MagazineSize = 30,
                Range = 100f,
                Spread = 1.8f,
                RecoilStrength = 4f,
                HeadshotMultiplier = 2f
            };
            
            RunComparison();
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("A/B Testing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Compare two weapon configurations side-by-side to analyze differences.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawConfigInputs();
            EditorGUILayout.Space(10);
            DrawComparisonResults();
            EditorGUILayout.Space(10);
            DrawSimulationSettings();
            EditorGUILayout.Space(10);
            DrawSimulationResults();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigInputs()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Config A
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(300));
            DrawConfigPanel(_configA, "Config A", new Color(0.3f, 0.5f, 0.8f));
            EditorGUILayout.EndVertical();
            
            // VS label
            EditorGUILayout.BeginVertical(GUILayout.Width(40));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("VS", new GUIStyle(EditorStyles.boldLabel) 
            { 
                fontSize = 24, 
                alignment = TextAnchor.MiddleCenter 
            }, GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            
            // Config B
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(300));
            DrawConfigPanel(_configB, "Config B", new Color(0.8f, 0.4f, 0.3f));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigPanel(WeaponConfig config, string label, Color labelColor)
        {
            GUI.color = labelColor;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            GUI.color = Color.white;
            
            config.Name = EditorGUILayout.TextField("Name", config.Name);
            config.SourceAsset = EditorGUILayout.ObjectField("Source", config.SourceAsset, typeof(Object), false);
            
            EditorGUILayout.Space(5);
            
            config.Damage = EditorGUILayout.FloatField("Damage", config.Damage);
            config.FireRate = EditorGUILayout.FloatField("Fire Rate (RPM)", config.FireRate);
            config.ReloadTime = EditorGUILayout.FloatField("Reload Time", config.ReloadTime);
            config.MagazineSize = EditorGUILayout.IntField("Magazine Size", config.MagazineSize);
            config.Range = EditorGUILayout.FloatField("Range", config.Range);
            config.Spread = EditorGUILayout.FloatField("Spread", config.Spread);
            config.RecoilStrength = EditorGUILayout.FloatField("Recoil Strength", config.RecoilStrength);
            config.HeadshotMultiplier = EditorGUILayout.FloatField("Headshot Multi", config.HeadshotMultiplier);
            
            EditorGUILayout.Space(5);
            
            // Calculated stats
            EditorGUILayout.LabelField("Calculated:", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField("DPS", config.DPS);
            EditorGUILayout.FloatField("TTK (100HP)", config.TTK100HP);
            EditorGUILayout.FloatField("Damage/Mag", config.DamagePerMag);
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Load from Asset"))
            {
                LoadFromAsset(config);
            }
        }

        private void DrawComparisonResults()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Comparison Results", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RunComparison();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Stat", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField(_configA.Name, EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Diff", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField(_configB.Name, EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Winner", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            foreach (var result in _comparisons)
            {
                DrawComparisonRow(result);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComparisonRow(ComparisonResult result)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(result.StatName, GUILayout.Width(120));
            
            // Value A
            GUI.color = result.Winner == ComparisonWinner.A ? Color.green : Color.white;
            EditorGUILayout.LabelField($"{result.ValueA:F1}", GUILayout.Width(80));
            
            // Difference
            GUI.color = Color.white;
            string diffText = result.Difference >= 0 ? $"+{result.Difference:F1}" : $"{result.Difference:F1}";
            string percentText = $"({result.DifferencePercent:F0}%)";
            
            Color diffColor = result.Difference > 0 ? Color.green : 
                             result.Difference < 0 ? Color.red : Color.gray;
            GUI.color = diffColor;
            EditorGUILayout.LabelField($"{diffText} {percentText}", GUILayout.Width(100));
            
            // Value B
            GUI.color = result.Winner == ComparisonWinner.B ? Color.green : Color.white;
            EditorGUILayout.LabelField($"{result.ValueB:F1}", GUILayout.Width(80));
            
            // Winner indicator
            GUI.color = Color.white;
            string winnerText = result.Winner switch
            {
                ComparisonWinner.A => $"← {_configA.Name}",
                ComparisonWinner.B => $"{_configB.Name} →",
                ComparisonWinner.Equal => "=",
                _ => "-"
            };
            
            Color winnerColor = result.Winner == ComparisonWinner.A ? new Color(0.3f, 0.5f, 0.8f) :
                               result.Winner == ComparisonWinner.B ? new Color(0.8f, 0.4f, 0.3f) : Color.gray;
            GUI.color = winnerColor;
            EditorGUILayout.LabelField(winnerText);
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSimulationSettings()
        {
            EditorGUILayout.LabelField("Simulation Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _testIterations = EditorGUILayout.IntSlider("Test Iterations", _testIterations, 100, 10000);
            _testDistance = EditorGUILayout.Slider("Test Distance (m)", _testDistance, 5f, 100f);
            _includeRecoil = EditorGUILayout.Toggle("Include Recoil", _includeRecoil);
            _includeSpread = EditorGUILayout.Toggle("Include Spread", _includeSpread);

            EditorGUILayout.Space(5);
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Run Simulation", GUILayout.Height(30)))
            {
                RunSimulation();
            }
            
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndVertical();
        }

        private void DrawSimulationResults()
        {
            EditorGUILayout.LabelField("Simulation Results", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Placeholder visualization
            EditorGUILayout.BeginHorizontal();
            
            // Config A results
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            GUI.color = new Color(0.3f, 0.5f, 0.8f);
            EditorGUILayout.LabelField(_configA.Name, EditorStyles.boldLabel);
            GUI.color = Color.white;
            
            EditorGUILayout.LabelField($"Avg DPS: {_configA.DPS:F1}");
            EditorGUILayout.LabelField($"TTK (100HP): {_configA.TTK100HP:F2}s");
            EditorGUILayout.LabelField($"Accuracy: {85f - _configA.Spread * 3:F1}%");
            
            // Accuracy bar
            Rect barRectA = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(20), GUILayout.ExpandWidth(true));
            DrawAccuracyBar(barRectA, 85f - _configA.Spread * 3f, new Color(0.3f, 0.5f, 0.8f));
            
            EditorGUILayout.EndVertical();
            
            // Config B results
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            GUI.color = new Color(0.8f, 0.4f, 0.3f);
            EditorGUILayout.LabelField(_configB.Name, EditorStyles.boldLabel);
            GUI.color = Color.white;
            
            EditorGUILayout.LabelField($"Avg DPS: {_configB.DPS:F1}");
            EditorGUILayout.LabelField($"TTK (100HP): {_configB.TTK100HP:F2}s");
            EditorGUILayout.LabelField($"Accuracy: {85f - _configB.Spread * 3:F1}%");
            
            // Accuracy bar
            Rect barRectB = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(20), GUILayout.ExpandWidth(true));
            DrawAccuracyBar(barRectB, 85f - _configB.Spread * 3f, new Color(0.8f, 0.4f, 0.3f));
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            // Verdict
            EditorGUILayout.Space(10);
            
            float scoreA = CalculateOverallScore(_configA);
            float scoreB = CalculateOverallScore(_configB);
            
            string verdict = scoreA > scoreB ? $"Winner: {_configA.Name}" :
                            scoreB > scoreA ? $"Winner: {_configB.Name}" : "Tie";
            
            Color verdictColor = scoreA > scoreB ? new Color(0.3f, 0.5f, 0.8f) :
                                scoreB > scoreA ? new Color(0.8f, 0.4f, 0.3f) : Color.gray;
            
            GUI.color = verdictColor;
            EditorGUILayout.LabelField(verdict, new GUIStyle(EditorStyles.boldLabel) 
            { 
                fontSize = 16, 
                alignment = TextAnchor.MiddleCenter 
            });
            GUI.color = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawAccuracyBar(Rect rect, float accuracy, Color color)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float ratio = Mathf.Clamp01(accuracy / 100f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), color);
            
            GUI.Label(rect, $"{accuracy:F0}%", 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
        }

        private void RunComparison()
        {
            _comparisons.Clear();
            
            AddComparison("Damage", _configA.Damage, _configB.Damage, true);
            AddComparison("Fire Rate", _configA.FireRate, _configB.FireRate, true);
            AddComparison("DPS", _configA.DPS, _configB.DPS, true);
            AddComparison("TTK (100HP)", _configA.TTK100HP, _configB.TTK100HP, false); // Lower is better
            AddComparison("Reload Time", _configA.ReloadTime, _configB.ReloadTime, false);
            AddComparison("Magazine", _configA.MagazineSize, _configB.MagazineSize, true);
            AddComparison("Spread", _configA.Spread, _configB.Spread, false); // Lower is better
            AddComparison("Recoil", _configA.RecoilStrength, _configB.RecoilStrength, false);
            AddComparison("HS Multi", _configA.HeadshotMultiplier, _configB.HeadshotMultiplier, true);
            AddComparison("Dmg/Mag", _configA.DamagePerMag, _configB.DamagePerMag, true);
        }

        private void AddComparison(string name, float valueA, float valueB, bool higherIsBetter)
        {
            float diff = valueA - valueB;
            float percent = valueB != 0 ? (diff / valueB) * 100f : 0f;
            
            ComparisonWinner winner;
            if (Mathf.Approximately(valueA, valueB))
            {
                winner = ComparisonWinner.Equal;
            }
            else if (higherIsBetter)
            {
                winner = valueA > valueB ? ComparisonWinner.A : ComparisonWinner.B;
            }
            else
            {
                winner = valueA < valueB ? ComparisonWinner.A : ComparisonWinner.B;
            }
            
            _comparisons.Add(new ComparisonResult
            {
                StatName = name,
                ValueA = valueA,
                ValueB = valueB,
                Difference = diff,
                DifferencePercent = percent,
                Winner = winner
            });
        }

        private float CalculateOverallScore(WeaponConfig config)
        {
            // Simple scoring formula
            return config.DPS * 0.4f + 
                   (100f - config.Spread * 10f) * 0.3f + 
                   (100f - config.RecoilStrength * 5f) * 0.3f;
        }

        private void LoadFromAsset(WeaponConfig config)
        {
            Debug.Log($"[ABTesting] Load from asset: {config.SourceAsset?.name}");
        }

        private void RunSimulation()
        {
            Debug.Log($"[ABTesting] Running {_testIterations} iterations at {_testDistance}m");
        }
    }
}
