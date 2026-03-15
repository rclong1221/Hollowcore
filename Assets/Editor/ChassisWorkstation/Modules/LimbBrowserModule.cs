using System.Collections.Generic;
using System.Linq;
using Hollowcore.Chassis;
using Hollowcore.Chassis.Definitions;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Editor.ChassisWorkstation.Modules
{
    /// <summary>
    /// Searchable/filterable grid of all LimbDefinitionSO assets.
    /// Filter by SlotType, Rarity, DistrictAffinity. Sort by any stat, name, or LimbId.
    /// </summary>
    public class LimbBrowserModule : IChassisModule
    {
        private List<LimbDefinitionSO> _allLimbs = new();
        private List<LimbDefinitionSO> _filteredLimbs = new();
        private string _searchText = "";
        private ChassisSlot _slotFilter = (ChassisSlot)255;
        private LimbRarity _rarityFilter = (LimbRarity)255;
        private int _sortColumn;
        private bool _sortAscending = true;
        private float _lastRefreshTime;

        private static readonly string[] SortOptions =
            { "Name", "LimbId", "Rarity", "Slot", "Damage", "Armor", "MoveSpd", "MaxHP", "Integrity" };

        public void OnGUI()
        {
            RefreshIfNeeded();

            // Filters
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            _searchText = EditorGUILayout.TextField(_searchText, GUILayout.Width(200));

            EditorGUILayout.LabelField("Slot:", GUILayout.Width(30));
            _slotFilter = (ChassisSlot)EditorGUILayout.EnumFlagsField(_slotFilter, GUILayout.Width(100));

            EditorGUILayout.LabelField("Rarity:", GUILayout.Width(45));
            _rarityFilter = (LimbRarity)EditorGUILayout.EnumFlagsField(_rarityFilter, GUILayout.Width(100));

            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                RefreshLimbList();

            EditorGUILayout.EndHorizontal();

            // Sort
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sort:", GUILayout.Width(30));
            int newSort = EditorGUILayout.Popup(_sortColumn, SortOptions, GUILayout.Width(100));
            if (newSort != _sortColumn)
            {
                _sortColumn = newSort;
                ApplyFilter();
            }
            if (GUILayout.Button(_sortAscending ? "Asc" : "Desc", GUILayout.Width(40)))
            {
                _sortAscending = !_sortAscending;
                ApplyFilter();
            }
            EditorGUILayout.LabelField($"{_filteredLimbs.Count} / {_allLimbs.Count} limbs");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Grid
            foreach (var limb in _filteredLimbs)
            {
                DrawLimbRow(limb);
            }
        }

        private void DrawLimbRow(LimbDefinitionSO limb)
        {
            var rect = EditorGUILayout.GetControlRect(false, 22);
            var bgColor = (int)limb.Rarity switch
            {
                0 => new Color(0.3f, 0.3f, 0.3f, 0.2f),
                1 => new Color(0.5f, 0.5f, 0.5f, 0.1f),
                2 => new Color(0.2f, 0.6f, 0.2f, 0.15f),
                3 => new Color(0.2f, 0.4f, 0.8f, 0.15f),
                4 => new Color(0.5f, 0.2f, 0.7f, 0.15f),
                5 => new Color(0.8f, 0.7f, 0.0f, 0.15f),
                _ => Color.clear
            };
            EditorGUI.DrawRect(rect, bgColor);

            float x = rect.x;
            EditorGUI.LabelField(new Rect(x, rect.y, 30, rect.height), limb.LimbId.ToString(), EditorStyles.miniLabel);
            x += 32;
            EditorGUI.LabelField(new Rect(x, rect.y, 30, rect.height), limb.SlotType.ToString().Substring(0, System.Math.Min(2, limb.SlotType.ToString().Length)), EditorStyles.miniBoldLabel);
            x += 32;
            EditorGUI.LabelField(new Rect(x, rect.y, 150, rect.height), limb.DisplayName ?? limb.name, EditorStyles.miniLabel);
            x += 154;
            EditorGUI.LabelField(new Rect(x, rect.y, 60, rect.height), limb.Rarity.ToString(), EditorStyles.miniLabel);
            x += 64;
            EditorGUI.LabelField(new Rect(x, rect.y, 40, rect.height), $"D:{limb.BonusDamage:F0}", EditorStyles.miniLabel);
            x += 44;
            EditorGUI.LabelField(new Rect(x, rect.y, 40, rect.height), $"A:{limb.BonusArmor:F0}", EditorStyles.miniLabel);
            x += 44;
            EditorGUI.LabelField(new Rect(x, rect.y, 50, rect.height), $"HP:{limb.BonusMaxHealth:F0}", EditorStyles.miniLabel);

            // Click to select asset
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Selection.activeObject = limb;
                EditorGUIUtility.PingObject(limb);
                Event.current.Use();
            }
        }

        private void RefreshIfNeeded()
        {
            if (Time.realtimeSinceStartup - _lastRefreshTime > 2f || _allLimbs.Count == 0)
                RefreshLimbList();
        }

        private void RefreshLimbList()
        {
            _lastRefreshTime = Time.realtimeSinceStartup;
            _allLimbs.Clear();

            var guids = AssetDatabase.FindAssets("t:LimbDefinitionSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<LimbDefinitionSO>(path);
                if (def != null) _allLimbs.Add(def);
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _filteredLimbs = _allLimbs.Where(l =>
            {
                if (!string.IsNullOrEmpty(_searchText) &&
                    !(l.DisplayName ?? l.name).ToLower().Contains(_searchText.ToLower()))
                    return false;

                if ((byte)_slotFilter != 255 && ((1 << (int)l.SlotType) & (int)_slotFilter) == 0)
                    return false;

                if ((byte)_rarityFilter != 255 && ((1 << (int)l.Rarity) & (int)_rarityFilter) == 0)
                    return false;

                return true;
            }).ToList();

            _filteredLimbs.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    0 => string.Compare(a.DisplayName ?? a.name, b.DisplayName ?? b.name, System.StringComparison.Ordinal),
                    1 => a.LimbId.CompareTo(b.LimbId),
                    2 => a.Rarity.CompareTo(b.Rarity),
                    3 => a.SlotType.CompareTo(b.SlotType),
                    4 => a.BonusDamage.CompareTo(b.BonusDamage),
                    5 => a.BonusArmor.CompareTo(b.BonusArmor),
                    6 => a.BonusMoveSpeed.CompareTo(b.BonusMoveSpeed),
                    7 => a.BonusMaxHealth.CompareTo(b.BonusMaxHealth),
                    8 => a.MaxIntegrity.CompareTo(b.MaxIntegrity),
                    _ => 0
                };
                return _sortAscending ? cmp : -cmp;
            });
        }

        public void OnSceneGUI(SceneView sceneView) { }
        public void OnEntityChanged(Entity entity, EntityManager entityManager) { }
    }
}
