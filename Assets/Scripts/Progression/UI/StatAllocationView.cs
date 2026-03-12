using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Stub stat allocation panel MonoBehaviour.
    /// Shows Str/Dex/Int/Vit values and unspent points.
    /// Full implementation should have +/- buttons that send StatAllocationRpc.
    /// </summary>
    public class StatAllocationView : MonoBehaviour, IStatAllocationProvider
    {
        private int _unspentPoints;
        private int _str, _dex, _int, _vit;

        private void OnEnable() => ProgressionUIRegistry.RegisterStatAllocation(this);
        private void OnDisable() => ProgressionUIRegistry.UnregisterStatAllocation(this);

        public void UpdateStatAllocation(int unspentPoints, int strength, int dexterity, int intelligence, int vitality)
        {
            _unspentPoints = unspentPoints;
            _str = strength;
            _dex = dexterity;
            _int = intelligence;
            _vit = vitality;
        }
    }
}
