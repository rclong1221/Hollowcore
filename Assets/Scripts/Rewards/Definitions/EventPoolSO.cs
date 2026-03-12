using System.Collections.Generic;
using UnityEngine;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: Pool of run events for event zones.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Roguelite/Event Pool")]
    public class EventPoolSO : ScriptableObject
    {
        public List<RunEventDefinitionSO> Events = new();
    }
}
