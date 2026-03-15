using Hollowcore.Chassis;
using Hollowcore.Chassis.Definitions;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Editor.Chassis
{
    /// <summary>
    /// Custom inspector for LimbDefinitionSO with stat visualization,
    /// rarity color coding, and slot type badges.
    /// </summary>
    [CustomEditor(typeof(LimbDefinitionSO))]
    public class LimbDefinitionSOEditor : UnityEditor.Editor
    {
        private static readonly string[] StatNames =
        {
            "Damage", "Armor", "MoveSpd", "MaxHP", "AtkSpd", "Stamina",
            "HeatRes", "ToxinRes", "FallRes"
        };

        private static readonly Color[] RarityColors =
        {
            new Color(0.5f, 0.5f, 0.5f), // Junk - gray
            Color.white,                    // Common - white
            new Color(0.2f, 0.8f, 0.2f),  // Uncommon - green
            new Color(0.3f, 0.5f, 1.0f),  // Rare - blue
            new Color(0.7f, 0.3f, 0.9f),  // Epic - purple
            new Color(1.0f, 0.84f, 0.0f)  // Legendary - gold
        };

        private static readonly string[] SlotIcons = { "H", "T", "LA", "RA", "LL", "RL" };

        public override void OnInspectorGUI()
        {
            var limb = (LimbDefinitionSO)target;

            DrawHeader(limb);
            EditorGUILayout.Space(4);
            DrawStatRadar(limb);
            EditorGUILayout.Space(8);

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            DrawCompareButton();
        }

        private void DrawHeader(LimbDefinitionSO limb)
        {
            var rarityColor = RarityColors[(int)limb.Rarity];
            var slotLabel = SlotIcons[(int)limb.SlotType];

            var rect = EditorGUILayout.GetControlRect(false, 30);

            // Rarity border
            EditorGUI.DrawRect(rect, rarityColor * 0.3f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), rarityColor);

            // Slot badge
            var badgeRect = new Rect(rect.x + 10, rect.y + 4, 28, 22);
            EditorGUI.DrawRect(badgeRect, new Color(0.2f, 0.2f, 0.2f));
            var badgeStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = rarityColor }
            };
            EditorGUI.LabelField(badgeRect, slotLabel, badgeStyle);

            // Name and rarity
            var labelRect = new Rect(rect.x + 44, rect.y + 4, rect.width - 50, 22);
            var nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = rarityColor }
            };
            EditorGUI.LabelField(labelRect, $"{limb.DisplayName ?? limb.name}  [{limb.Rarity}]", nameStyle);
        }

        private void DrawStatRadar(LimbDefinitionSO limb)
        {
            float[] stats =
            {
                limb.BonusDamage, limb.BonusArmor, limb.BonusMoveSpeed,
                limb.BonusMaxHealth, limb.BonusAttackSpeed, limb.BonusStamina,
                limb.HeatResistance, limb.ToxinResistance, limb.FallDamageReduction
            };

            float maxStat = 0f;
            foreach (var s in stats)
                if (Mathf.Abs(s) > maxStat) maxStat = Mathf.Abs(s);

            if (maxStat < 0.01f) return;

            EditorGUILayout.LabelField("Stat Overview", EditorStyles.boldLabel);

            var barColor = RarityColors[(int)limb.Rarity] * 0.8f;

            for (int i = 0; i < stats.Length; i++)
            {
                var rect = EditorGUILayout.GetControlRect(false, 16);
                var labelRect = new Rect(rect.x, rect.y, 70, rect.height);
                var barRect = new Rect(rect.x + 74, rect.y + 2, rect.width - 120, rect.height - 4);
                var valueRect = new Rect(rect.xMax - 42, rect.y, 42, rect.height);

                EditorGUI.LabelField(labelRect, StatNames[i], EditorStyles.miniLabel);

                // Background
                EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));

                // Fill
                float normalizedWidth = Mathf.Abs(stats[i]) / maxStat;
                var fillRect = new Rect(barRect.x, barRect.y, barRect.width * normalizedWidth, barRect.height);
                EditorGUI.DrawRect(fillRect, stats[i] >= 0 ? barColor : Color.red * 0.6f);

                EditorGUI.LabelField(valueRect, stats[i].ToString("F1"), EditorStyles.miniLabel);
            }
        }

        private void DrawCompareButton()
        {
            if (GUILayout.Button("Compare With..."))
            {
                EditorGUIUtility.ShowObjectPicker<LimbDefinitionSO>(null, false, "", GUIUtility.GetControlID(FocusType.Passive));
            }
        }
    }
}
