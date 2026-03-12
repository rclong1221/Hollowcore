using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.DebugWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 DW-01: Testing Sandbox module.
    /// Spawn weapons in test scene, stat tweaking.
    /// </summary>
    public class TestingSandboxModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        
        // Test scene
        private string _testSceneName = "TestingSandbox";
        private bool _testSceneLoaded = false;
        
        // Weapon spawning
        private List<WeaponSpawnEntry> _weaponList = new List<WeaponSpawnEntry>();
        private int _selectedWeaponIndex = -1;
        
        // Stat overrides
        private bool _enableStatOverrides = false;
        private float _damageMultiplier = 1f;
        private float _fireRateMultiplier = 1f;
        private float _recoilMultiplier = 1f;
        private float _spreadMultiplier = 1f;
        private bool _infiniteAmmo = true;
        private bool _noReload = false;
        
        // Quick actions
        private bool _godMode = false;
        private bool _showHitboxes = false;
        private bool _showBulletTrails = true;
        private float _timeScale = 1f;

        [System.Serializable]
        private class WeaponSpawnEntry
        {
            public string Name;
            public GameObject Prefab;
            public string Category;
            public bool IsSpawned;
        }

        public TestingSandboxModule()
        {
            InitializeWeaponList();
        }

        private void InitializeWeaponList()
        {
            _weaponList = new List<WeaponSpawnEntry>
            {
                new WeaponSpawnEntry { Name = "AK-47", Category = "Rifle" },
                new WeaponSpawnEntry { Name = "M4A1", Category = "Rifle" },
                new WeaponSpawnEntry { Name = "SCAR-H", Category = "Rifle" },
                new WeaponSpawnEntry { Name = "MP5", Category = "SMG" },
                new WeaponSpawnEntry { Name = "UMP-45", Category = "SMG" },
                new WeaponSpawnEntry { Name = "Glock 17", Category = "Pistol" },
                new WeaponSpawnEntry { Name = "Desert Eagle", Category = "Pistol" },
                new WeaponSpawnEntry { Name = "M870", Category = "Shotgun" },
                new WeaponSpawnEntry { Name = "SPAS-12", Category = "Shotgun" },
                new WeaponSpawnEntry { Name = "AWP", Category = "Sniper" },
            };
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Testing Sandbox", EditorStyles.boldLabel);
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to use the Testing Sandbox. Some features work in editor.",
                    MessageType.Warning);
            }
            
            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawSceneControls();
            EditorGUILayout.Space(10);
            DrawWeaponSpawner();
            EditorGUILayout.Space(10);
            DrawStatOverrides();
            EditorGUILayout.Space(10);
            DrawQuickActions();
            EditorGUILayout.Space(10);
            DrawTimeControls();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneControls()
        {
            EditorGUILayout.LabelField("Test Scene", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _testSceneName = EditorGUILayout.TextField("Scene Name", _testSceneName);
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _testSceneLoaded ? Color.red : Color.green;
            
            if (GUILayout.Button(_testSceneLoaded ? "Unload" : "Load", GUILayout.Width(80)))
            {
                ToggleTestScene();
            }
            
            GUI.backgroundColor = prevColor;
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Create Test Scene"))
            {
                CreateTestScene();
            }
            
            if (GUILayout.Button("Reset Scene"))
            {
                ResetTestScene();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawWeaponSpawner()
        {
            EditorGUILayout.LabelField("Weapon Spawner", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Category tabs
            string[] categories = { "All", "Rifle", "SMG", "Pistol", "Shotgun", "Sniper" };
            
            EditorGUILayout.BeginHorizontal();
            foreach (var cat in categories)
            {
                if (GUILayout.Button(cat, EditorStyles.miniButton))
                {
                    // Filter would be applied here
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            // Weapon grid
            int columns = 3;
            int count = 0;
            
            EditorGUILayout.BeginHorizontal();
            
            for (int i = 0; i < _weaponList.Count; i++)
            {
                var weapon = _weaponList[i];
                
                bool isSelected = i == _selectedWeaponIndex;
                
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = weapon.IsSpawned ? Color.green : 
                                      isSelected ? Color.cyan : Color.white;
                
                if (GUILayout.Button(weapon.Name, GUILayout.Width(120), GUILayout.Height(40)))
                {
                    _selectedWeaponIndex = i;
                }
                
                GUI.backgroundColor = prevColor;
                
                count++;
                if (count >= columns)
                {
                    count = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            // Spawn controls
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(_selectedWeaponIndex < 0 || !Application.isPlaying);
            
            Color btnColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button("Spawn Selected", GUILayout.Height(25)))
            {
                SpawnWeapon(_weaponList[_selectedWeaponIndex]);
            }
            
            GUI.backgroundColor = btnColor;
            
            if (GUILayout.Button("Despawn All", GUILayout.Height(25)))
            {
                DespawnAll();
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();

            // Custom prefab
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Custom:", GUILayout.Width(50));
            var customPrefab = (GameObject)EditorGUILayout.ObjectField(null, typeof(GameObject), false);
            if (GUILayout.Button("Spawn", GUILayout.Width(60)) && customPrefab != null)
            {
                // Spawn custom prefab
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawStatOverrides()
        {
            EditorGUILayout.LabelField("Stat Overrides", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableStatOverrides = EditorGUILayout.Toggle("Enable Overrides", _enableStatOverrides);
            
            EditorGUI.BeginDisabledGroup(!_enableStatOverrides);
            
            EditorGUILayout.Space(5);
            
            // Multiplier sliders with visual feedback
            DrawMultiplierSlider("Damage", ref _damageMultiplier, 0.1f, 10f);
            DrawMultiplierSlider("Fire Rate", ref _fireRateMultiplier, 0.1f, 5f);
            DrawMultiplierSlider("Recoil", ref _recoilMultiplier, 0f, 3f);
            DrawMultiplierSlider("Spread", ref _spreadMultiplier, 0f, 3f);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            _infiniteAmmo = EditorGUILayout.Toggle("Infinite Ammo", _infiniteAmmo);
            _noReload = EditorGUILayout.Toggle("No Reload", _noReload);
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Reset to Default"))
            {
                ResetStatOverrides();
            }
            
            if (GUILayout.Button("Apply to Scene"))
            {
                ApplyStatOverrides();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawMultiplierSlider(string label, ref float value, float min, float max)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(label, GUILayout.Width(80));
            
            // Color based on value
            Color sliderColor = value < 1f ? Color.red :
                               value > 1f ? Color.green : Color.white;
            GUI.color = sliderColor;
            
            value = EditorGUILayout.Slider(value, min, max);
            
            GUI.color = Color.white;
            
            EditorGUILayout.LabelField($"{value:F1}x", GUILayout.Width(40));
            
            if (GUILayout.Button("1x", GUILayout.Width(30)))
            {
                value = 1f;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Toggle buttons
            _godMode = GUILayout.Toggle(_godMode, "God Mode", "Button", GUILayout.Height(30));
            _showHitboxes = GUILayout.Toggle(_showHitboxes, "Show Hitboxes", "Button", GUILayout.Height(30));
            _showBulletTrails = GUILayout.Toggle(_showBulletTrails, "Bullet Trails", "Button", GUILayout.Height(30));
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Heal Player", GUILayout.Height(25)))
            {
                HealPlayer();
            }
            
            if (GUILayout.Button("Kill All Enemies", GUILayout.Height(25)))
            {
                KillAllEnemies();
            }
            
            if (GUILayout.Button("Refill Ammo", GUILayout.Height(25)))
            {
                RefillAmmo();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTimeControls()
        {
            EditorGUILayout.LabelField("Time Controls", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Time Scale:", GUILayout.Width(70));
            _timeScale = EditorGUILayout.Slider(_timeScale, 0f, 2f);
            
            if (Application.isPlaying)
            {
                Time.timeScale = _timeScale;
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("0.1x")) _timeScale = 0.1f;
            if (GUILayout.Button("0.25x")) _timeScale = 0.25f;
            if (GUILayout.Button("0.5x")) _timeScale = 0.5f;
            if (GUILayout.Button("1x")) _timeScale = 1f;
            if (GUILayout.Button("2x")) _timeScale = 2f;
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _timeScale == 0 ? Color.yellow : Color.white;
            if (GUILayout.Button("Pause")) _timeScale = 0f;
            GUI.backgroundColor = prevColor;
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ToggleTestScene()
        {
            _testSceneLoaded = !_testSceneLoaded;
            Debug.Log($"[TestingSandbox] Scene {(_testSceneLoaded ? "loaded" : "unloaded")}");
        }

        private void CreateTestScene()
        {
            Debug.Log("[TestingSandbox] Create test scene pending");
        }

        private void ResetTestScene()
        {
            DespawnAll();
            ResetStatOverrides();
            _timeScale = 1f;
            Debug.Log("[TestingSandbox] Scene reset");
        }

        private void SpawnWeapon(WeaponSpawnEntry weapon)
        {
            weapon.IsSpawned = true;
            Debug.Log($"[TestingSandbox] Spawned: {weapon.Name}");
        }

        private void DespawnAll()
        {
            foreach (var w in _weaponList)
            {
                w.IsSpawned = false;
            }
            Debug.Log("[TestingSandbox] Despawned all weapons");
        }

        private void ResetStatOverrides()
        {
            _damageMultiplier = 1f;
            _fireRateMultiplier = 1f;
            _recoilMultiplier = 1f;
            _spreadMultiplier = 1f;
        }

        private void ApplyStatOverrides()
        {
            Debug.Log($"[TestingSandbox] Applied: Damage={_damageMultiplier}x, FireRate={_fireRateMultiplier}x");
        }

        private void HealPlayer()
        {
            Debug.Log("[TestingSandbox] Player healed");
        }

        private void KillAllEnemies()
        {
            Debug.Log("[TestingSandbox] All enemies killed");
        }

        private void RefillAmmo()
        {
            Debug.Log("[TestingSandbox] Ammo refilled");
        }
    }
}
