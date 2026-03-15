using Hollowcore.Chassis;
using Hollowcore.Chassis.Definitions;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Editor.ChassisWorkstation.Modules
{
    /// <summary>
    /// 6-slot body diagram for previewing chassis loadouts.
    /// Drag limbs into slots, see aggregated stats in real-time.
    /// </summary>
    public class LoadoutPreviewerModule : IChassisModule
    {
        private readonly LimbDefinitionSO[] _slots = new LimbDefinitionSO[6];
        private static readonly string[] SlotNames = { "Head", "Torso", "Left Arm", "Right Arm", "Left Leg", "Right Leg" };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Loadout Previewer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assign limbs to slots to preview aggregated stats.", MessageType.Info);

            EditorGUILayout.Space(4);

            // Slot assignments
            for (int i = 0; i < 6; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(SlotNames[i], GUILayout.Width(80));
                _slots[i] = (LimbDefinitionSO)EditorGUILayout.ObjectField(
                    _slots[i], typeof(LimbDefinitionSO), false);

                if (_slots[i] != null && (int)_slots[i].SlotType != i)
                {
                    EditorGUILayout.LabelField("Wrong slot!", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            DrawAggregatedStats();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Clear All"))
            {
                for (int i = 0; i < 6; i++) _slots[i] = null;
            }
        }

        private void DrawAggregatedStats()
        {
            EditorGUILayout.LabelField("Aggregated Stats", EditorStyles.boldLabel);

            float totalDamage = 0, totalArmor = 0, totalMoveSpeed = 0, totalMaxHealth = 0;
            float totalAttackSpeed = 0, totalStamina = 0, totalIntegrity = 0;
            float totalHeat = 0, totalToxin = 0, totalFall = 0;
            int equipped = 0;

            foreach (var limb in _slots)
            {
                if (limb == null) continue;
                equipped++;
                totalDamage += limb.BonusDamage;
                totalArmor += limb.BonusArmor;
                totalMoveSpeed += limb.BonusMoveSpeed;
                totalMaxHealth += limb.BonusMaxHealth;
                totalAttackSpeed += limb.BonusAttackSpeed;
                totalStamina += limb.BonusStamina;
                totalIntegrity += limb.MaxIntegrity;
                totalHeat += limb.HeatResistance;
                totalToxin += limb.ToxinResistance;
                totalFall += limb.FallDamageReduction;
            }

            EditorGUILayout.LabelField($"Equipped: {equipped}/6");
            EditorGUILayout.LabelField($"Total Integrity: {totalIntegrity:F0}");

            EditorGUILayout.Space(2);
            DrawStatRow("Damage", totalDamage);
            DrawStatRow("Armor", totalArmor);
            DrawStatRow("Move Speed", totalMoveSpeed);
            DrawStatRow("Max Health", totalMaxHealth);
            DrawStatRow("Attack Speed", totalAttackSpeed);
            DrawStatRow("Stamina", totalStamina);
            DrawStatRow("Heat Resist", totalHeat);
            DrawStatRow("Toxin Resist", totalToxin);
            DrawStatRow("Fall Reduction", totalFall);
        }

        private static void DrawStatRow(string label, float value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            var color = value > 0 ? Color.green : value < 0 ? Color.red : Color.white;
            var style = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
            EditorGUILayout.LabelField($"{(value >= 0 ? "+" : "")}{value:F1}", style);
            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(SceneView sceneView) { }
        public void OnEntityChanged(Entity entity, EntityManager entityManager) { }
    }
}
