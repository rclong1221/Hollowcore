using System.Collections.Generic;
using Hollowcore.Chassis;
using Hollowcore.Chassis.Definitions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hollowcore.Editor.Chassis
{
    /// <summary>
    /// Build-time validation for LimbDefinitionSO assets.
    /// Checks for duplicate IDs, missing references, and data integrity.
    /// </summary>
    [InitializeOnLoad]
    public class LimbDefinitionValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        static LimbDefinitionValidator()
        {
            // Run validation on domain reload in editor
            EditorApplication.delayCall += ValidateAll;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateAll();
        }

        [MenuItem("Hollowcore/Chassis/Validate Limb Definitions")]
        public static void ValidateAll()
        {
            var guids = AssetDatabase.FindAssets("t:LimbDefinitionSO");
            var definitions = new List<LimbDefinitionSO>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<LimbDefinitionSO>(path);
                if (def != null)
                    definitions.Add(def);
            }

            if (definitions.Count == 0)
                return;

            // Check for duplicate LimbId values
            var idMap = new Dictionary<int, LimbDefinitionSO>();
            foreach (var def in definitions)
            {
                if (idMap.TryGetValue(def.LimbId, out var existing))
                {
                    Debug.LogError(
                        $"[LimbValidator] Duplicate LimbId {def.LimbId}: '{def.name}' and '{existing.name}'",
                        def);
                }
                else
                {
                    idMap[def.LimbId] = def;
                }
            }

            // Check every ChassisSlot has at least one definition
            var slotCoverage = new HashSet<ChassisSlot>();
            foreach (var def in definitions)
                slotCoverage.Add(def.SlotType);

            for (int i = 0; i <= (int)ChassisSlot.RightLeg; i++)
            {
                var slot = (ChassisSlot)i;
                if (!slotCoverage.Contains(slot))
                    Debug.LogWarning($"[LimbValidator] No LimbDefinitionSO found for slot: {slot}");
            }

            // Check VisualPrefab for non-Junk rarity
            foreach (var def in definitions)
            {
                if (def.Rarity > LimbRarity.Junk && def.VisualPrefab == null)
                    Debug.LogWarning(
                        $"[LimbValidator] '{def.name}' (Rarity={def.Rarity}) has no VisualPrefab assigned",
                        def);
            }

            // Check StumpPrefab coverage per slot type
            var stumpSlots = new HashSet<ChassisSlot>();
            foreach (var def in definitions)
            {
                if (def.StumpPrefab != null)
                    stumpSlots.Add(def.SlotType);
            }
            foreach (var slot in slotCoverage)
            {
                if (!stumpSlots.Contains(slot))
                    Debug.LogWarning($"[LimbValidator] No StumpPrefab assigned for any {slot} limb definition");
            }
        }
    }
}
