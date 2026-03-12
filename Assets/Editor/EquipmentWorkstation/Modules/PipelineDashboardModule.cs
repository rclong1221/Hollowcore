using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Items.Definitions;
using DIG.Items.Authoring;
using DIG.Weapons.Authoring;

namespace DIG.Editor.EquipmentWorkstation
{
    public class PipelineDashboardModule : IEquipmentModule
    {
        private Vector2 _scrollPos;
        private List<AssetStatus> _assets = new List<AssetStatus>();
        private bool _needsScan = true;

        private struct AssetStatus
        {
            public string Name;
            public string Path;
            public bool HasPrefab;
            public bool HasSocket;  // Only for Characters
            public bool HasGrip;    // Only for Weapons
            public bool HasConfig;  // Only for Weapons
            public bool IsValid;    // Overall status
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Pipeline Matrix Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Visualizes the health of the entire asset library. Green = Ready, Red = Broken/Incomplete.", MessageType.Info);

            if (GUILayout.Button("Refresh Matrix", GUILayout.Height(30)))
            {
                ScanAssets();
            }

            if (_assets.Count > 0)
            {
                DrawMatrix();
            }
            else if (!_needsScan)
            {
                EditorGUILayout.HelpBox("No assets found.", MessageType.Warning);
            }
        }

        private void ScanAssets()
        {
            _assets.Clear();
            _needsScan = false;

            // Only scan weapon-specific folders instead of entire Assets folder
            string[] searchFolders = new[] {
                "Assets/Content/Weapons",
                "Assets/Prefabs/Weapons",
                "Assets/DIG/Weapons"
            };

            // Filter to only valid existing folders
            var validFolders = new List<string>();
            foreach (var folder in searchFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    validFolders.Add(folder);
            }

            if (validFolders.Count == 0)
            {
                Debug.LogWarning("[PipelineDashboard] No weapon folders found. Expected: Assets/Content/Weapons or Assets/Prefabs/Weapons");
                return;
            }

            string[] weaponGUIDs = AssetDatabase.FindAssets("t:Prefab", validFolders.ToArray());
            foreach (var guid in weaponGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                var weaponAuth = prefab.GetComponent<WeaponAuthoring>();
                if (weaponAuth != null)
                {
                    bool hasGrip = prefab.GetComponent<ItemGripAuthoring>() != null;
                    bool hasConfig = weaponAuth.Config != null;

                    // Validation Logic
                    bool isValid = hasGrip && hasConfig;
                    if (hasConfig && weaponAuth.Config.ItemID <= 0) isValid = false;

                    _assets.Add(new AssetStatus
                    {
                        Name = prefab.name,
                        Path = path,
                        HasPrefab = true,
                        HasSocket = false, // Not applicable
                        HasGrip = hasGrip,
                        HasConfig = hasConfig,
                        IsValid = isValid
                    });
                }
            }
        }

        private void DrawMatrix()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Asset Name", GUILayout.Width(200));
            GUILayout.Label("Grip (Vis)", GUILayout.Width(80));
            GUILayout.Label("Config (Log)", GUILayout.Width(80));
            GUILayout.Label("Status", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            foreach (var asset in _assets)
            {
                GUI.backgroundColor = asset.IsValid ? Color.green : Color.red;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;

                GUILayout.Label(asset.Name, GUILayout.Width(200));
                
                // Grip
                Color oldColor = GUI.color;
                GUI.color = asset.HasGrip ? Color.green : Color.red;
                GUILayout.Label(asset.HasGrip ? "YES" : "NO", GUILayout.Width(80));
                
                // Config
                GUI.color = asset.HasConfig ? Color.green : Color.red;
                GUILayout.Label(asset.HasConfig ? "YES" : "NO", GUILayout.Width(80));
                GUI.color = oldColor;

                // Status
                if (GUILayout.Button(asset.IsValid ? "READY" : "FIX", GUILayout.Width(100)))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(asset.Path);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
