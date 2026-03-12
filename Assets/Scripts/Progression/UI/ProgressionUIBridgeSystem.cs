using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Managed bridge system that reads ECS progression data
    /// and pushes to UI providers via ProgressionUIRegistry.
    /// Drains LevelUpVisualQueue for popup/animation events.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class ProgressionUIBridgeSystem : SystemBase
    {
        private int _diagnosticFrameCounter;
        private bool _diagnosticsRun;

        protected override void OnCreate()
        {
            LevelUpVisualQueue.Initialize();
        }

        protected override void OnDestroy()
        {
            LevelUpVisualQueue.Dispose();
        }

        protected override void OnUpdate()
        {
            // Drain XP gain visual queue
            while (LevelUpVisualQueue.TryDequeueXP(out var xpEvt))
            {
                ProgressionUIRegistry.XPGain?.ShowXPGain(xpEvt.Amount, xpEvt.Source);
            }

            // Drain level-up visual queue
            while (LevelUpVisualQueue.TryDequeueLevelUp(out var lvlEvt))
            {
                ProgressionUIRegistry.LevelUpPopup?.ShowLevelUp(lvlEvt.NewLevel, lvlEvt.PreviousLevel, lvlEvt.StatPointsAwarded);
            }

            // Find local player's progression data
            foreach (var (prog, attrs, ghostOwner) in
                     SystemAPI.Query<RefRO<PlayerProgression>, RefRO<CharacterAttributes>, RefRO<GhostOwner>>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                int level = attrs.ValueRO.Level;
                int currentXP = prog.ValueRO.CurrentXP;
                int xpToNext = GetXPToNextLevel(level);
                float percent = xpToNext > 0 ? (float)currentXP / xpToNext : 1f;
                int unspent = prog.ValueRO.UnspentStatPoints;
                float rested = prog.ValueRO.RestedXP;

                ProgressionUIRegistry.XPBar?.UpdateXPBar(level, currentXP, xpToNext, percent, unspent, rested);

                // Stat allocation panel
                ProgressionUIRegistry.StatAllocation?.UpdateStatAllocation(
                    unspent,
                    attrs.ValueRO.Strength,
                    attrs.ValueRO.Dexterity,
                    attrs.ValueRO.Intelligence,
                    attrs.ValueRO.Vitality);
            }

            // Startup diagnostics (60 frame grace period)
            if (!_diagnosticsRun)
            {
                _diagnosticFrameCounter++;
                if (_diagnosticFrameCounter >= 60)
                {
                    RunStartupDiagnostics();
                    _diagnosticsRun = true;
                }
            }
        }

        private int GetXPToNextLevel(int level)
        {
            if (!SystemAPI.HasSingleton<ProgressionConfigSingleton>())
                return 100;

            var config = SystemAPI.GetSingleton<ProgressionConfigSingleton>();
            ref var curve = ref config.Curve.Value;
            int index = level - 1;
            if (index < 0 || index >= curve.XPPerLevel.Length)
                return 0;
            return curve.XPPerLevel[index];
        }

        private void RunStartupDiagnostics()
        {
            if (ProgressionUIRegistry.XPBar == null)
                UnityEngine.Debug.LogWarning("[ProgressionUIBridge] No IXPBarProvider registered. XP bar will not display.");
            if (ProgressionUIRegistry.LevelUpPopup == null)
                UnityEngine.Debug.LogWarning("[ProgressionUIBridge] No ILevelUpPopupProvider registered. Level-up popups will not display.");
            if (ProgressionUIRegistry.XPGain == null)
                UnityEngine.Debug.LogWarning("[ProgressionUIBridge] No IXPGainProvider registered. Floating XP numbers will not display.");
        }
    }
}
