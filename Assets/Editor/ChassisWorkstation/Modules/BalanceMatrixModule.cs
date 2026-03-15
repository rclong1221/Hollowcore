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
    /// Spreadsheet of all limbs with sortable stat columns.
    /// Highlights outliers (> 2 sigma from mean) in red.
    /// Per-rarity stat budget validation.
    /// </summary>
    public class BalanceMatrixModule : IChassisModule
    {
        private List<LimbDefinitionSO> _limbs = new();
        private Vector2 _scrollPos;
        private float _lastRefresh;

        private static readonly string[] Headers =
            { "Name", "Slot", "Rarity", "Integ", "Dmg", "Armor", "Spd", "HP", "AtkSpd", "Stam", "Heat", "Toxin", "Fall", "Budget" };

        public void OnGUI()
        {
            RefreshIfNeeded();

            EditorGUILayout.LabelField("Balance Matrix", EditorStyles.boldLabel);

            if (_limbs.Count == 0)
            {
                EditorGUILayout.HelpBox("No LimbDefinitionSO assets found.", MessageType.Info);
                return;
            }

            // Compute stats for outlier detection
            var budgets = _limbs.Select(GetStatBudget).ToList();
            float mean = budgets.Average();
            float stdDev = Mathf.Sqrt(budgets.Select(b => (b - mean) * (b - mean)).Average());

            // Header row
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            foreach (var h in Headers)
                EditorGUILayout.LabelField(h, EditorStyles.toolbarButton, GUILayout.Width(h.Length < 5 ? 45 : 65));
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _limbs.Count; i++)
            {
                var limb = _limbs[i];
                float budget = budgets[i];
                bool isOutlier = Mathf.Abs(budget - mean) > 2f * stdDev && stdDev > 0.01f;

                var bgColor = isOutlier ? new Color(0.8f, 0.2f, 0.2f, 0.2f) : Color.clear;
                var rect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.DrawRect(rect, bgColor);

                float x = rect.x;
                void Col(string text, float w)
                {
                    EditorGUI.LabelField(new Rect(x, rect.y, w, rect.height), text, EditorStyles.miniLabel);
                    x += w + 2;
                }

                Col(limb.DisplayName ?? limb.name, 63);
                Col(limb.SlotType.ToString().Substring(0, System.Math.Min(4, limb.SlotType.ToString().Length)), 43);
                Col(limb.Rarity.ToString().Substring(0, System.Math.Min(5, limb.Rarity.ToString().Length)), 43);
                Col(limb.MaxIntegrity.ToString("F0"), 43);
                Col(limb.BonusDamage.ToString("F1"), 43);
                Col(limb.BonusArmor.ToString("F1"), 43);
                Col(limb.BonusMoveSpeed.ToString("F1"), 43);
                Col(limb.BonusMaxHealth.ToString("F1"), 43);
                Col(limb.BonusAttackSpeed.ToString("F1"), 43);
                Col(limb.BonusStamina.ToString("F1"), 43);
                Col(limb.HeatResistance.ToString("F1"), 43);
                Col(limb.ToxinResistance.ToString("F1"), 43);
                Col(limb.FallDamageReduction.ToString("F1"), 43);
                Col(budget.ToString("F1") + (isOutlier ? " !" : ""), 50);
            }

            EditorGUILayout.EndScrollView();

            // Per-rarity summary
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Per-Rarity Budget Summary", EditorStyles.boldLabel);
            var grouped = _limbs.GroupBy(l => l.Rarity).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                var avgBudget = group.Select(GetStatBudget).Average();
                EditorGUILayout.LabelField($"  {group.Key}: {group.Count()} limbs, avg budget = {avgBudget:F1}");
            }
        }

        private static float GetStatBudget(LimbDefinitionSO limb)
        {
            return Mathf.Abs(limb.BonusDamage) + Mathf.Abs(limb.BonusArmor) +
                   Mathf.Abs(limb.BonusMoveSpeed) + Mathf.Abs(limb.BonusMaxHealth) +
                   Mathf.Abs(limb.BonusAttackSpeed) + Mathf.Abs(limb.BonusStamina) +
                   Mathf.Abs(limb.HeatResistance) + Mathf.Abs(limb.ToxinResistance) +
                   Mathf.Abs(limb.FallDamageReduction);
        }

        private void RefreshIfNeeded()
        {
            if (Time.realtimeSinceStartup - _lastRefresh > 3f || _limbs.Count == 0)
            {
                _lastRefresh = Time.realtimeSinceStartup;
                _limbs.Clear();
                foreach (var guid in AssetDatabase.FindAssets("t:LimbDefinitionSO"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var def = AssetDatabase.LoadAssetAtPath<LimbDefinitionSO>(path);
                    if (def != null) _limbs.Add(def);
                }
                _limbs = _limbs.OrderBy(l => l.LimbId).ToList();
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }
        public void OnEntityChanged(Entity entity, EntityManager entityManager) { }
    }
}
