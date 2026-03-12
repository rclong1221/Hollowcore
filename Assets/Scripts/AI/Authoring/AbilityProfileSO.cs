using System.Collections.Generic;
using UnityEngine;
using DIG.AI.Components;

namespace DIG.AI.Authoring
{
    /// <summary>
    /// EPIC 15.32: Enemy ability rotation profile.
    /// References a list of AbilityDefinitionSO assets (shareable, define-once).
    /// Attached to enemy prefabs via AbilityProfileAuthoring.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbilityProfile", menuName = "DIG/AI/Ability Profile")]
    public class AbilityProfileSO : ScriptableObject
    {
        [Header("Selection Mode")]
        [Tooltip("Priority = first valid wins. Utility = weighted scoring.")]
        public AbilitySelectionMode SelectionMode = AbilitySelectionMode.Priority;

        [Header("Abilities (Priority Order — first valid is chosen in Priority mode)")]
        [Tooltip("Drag AbilityDefinitionSO assets here. Order matters for Priority mode.")]
        public List<AbilityDefinitionSO> Abilities = new();
    }
}
