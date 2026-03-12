using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class StressAuthoring : MonoBehaviour
    {
        public float MaxStress = 100f;
        public float DefaultStressRate = 5.0f; // Gain per sec
        public float DefaultRecoveryRate = 10.0f; // Loss per sec
        
        public class Baker : Baker<StressAuthoring>
        {
            public override void Bake(StressAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new PlayerStressState
                {
                    CurrentStress = 0f,
                    MaxStress = authoring.MaxStress,
                    StressRate = authoring.DefaultStressRate,
                    RecoveryRate = authoring.DefaultRecoveryRate,
                    TimeInDarkness = 0f
                });
            }
        }
    }
}
