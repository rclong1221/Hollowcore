using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;
using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Optional debug overlay for progression data.
    /// Shows local player's XP, level, stat points in editor console.
    /// Only active when PROGRESSION_DEBUG scripting define is set.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class ProgressionDebugSystem : SystemBase
    {
        private float _nextLogTime;

        protected override void OnCreate()
        {
#if !PROGRESSION_DEBUG
            Enabled = false;
#endif
        }

        protected override void OnUpdate()
        {
#if PROGRESSION_DEBUG
            if (Time.ElapsedTime < _nextLogTime) return;
            _nextLogTime = (float)Time.ElapsedTime + 5f;

            foreach (var (prog, attrs) in
                     SystemAPI.Query<RefRO<PlayerProgression>, RefRO<CharacterAttributes>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                Debug.Log($"[ProgressionDebug] Level={attrs.ValueRO.Level} " +
                          $"XP={prog.ValueRO.CurrentXP} Total={prog.ValueRO.TotalXPEarned} " +
                          $"Points={prog.ValueRO.UnspentStatPoints} Rested={prog.ValueRO.RestedXP:F0} " +
                          $"Str={attrs.ValueRO.Strength} Dex={attrs.ValueRO.Dexterity} " +
                          $"Int={attrs.ValueRO.Intelligence} Vit={attrs.ValueRO.Vitality}");
            }
#endif
        }
    }
}
