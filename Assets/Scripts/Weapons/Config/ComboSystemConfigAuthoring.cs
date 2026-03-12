using Unity.Entities;
using UnityEngine;

namespace DIG.Weapons.Config
{
    /// <summary>
    /// Authoring component to bake ComboSystemConfig into ECS singleton.
    /// Place on a GameObject in a subscene or bootstrap scene.
    /// </summary>
    public class ComboSystemConfigAuthoring : MonoBehaviour
    {
        [Tooltip("Reference to the ComboSystemConfig ScriptableObject.")]
        public ComboSystemConfig Config;

        public class Baker : Baker<ComboSystemConfigAuthoring>
        {
            public override void Bake(ComboSystemConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                if (authoring.Config != null)
                {
                    AddComponent(entity, new ComboSystemSettings
                    {
                        InputMode = authoring.Config.InputMode,
                        QueueDepth = authoring.Config.QueueDepth,
                        CancelPolicy = authoring.Config.CancelPolicy,
                        CancelPriority = authoring.Config.CancelPriority,
                        QueueClearPolicy = authoring.Config.QueueClearPolicy,
                        RhythmWindowStart = authoring.Config.RhythmWindowStart,
                        RhythmWindowEnd = authoring.Config.RhythmWindowEnd,
                        RhythmPerfectBonus = authoring.Config.RhythmPerfectBonus,
                        AllowPerWeaponOverride = authoring.Config.AllowPerWeaponOverride
                    });
                }
                else
                {
                    // Use defaults if no config assigned
                    AddComponent(entity, ComboSystemSettings.Default);
                }
            }
        }
    }
}
