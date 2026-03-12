using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Analytics;
using DIG.Progression;

/// <summary>
/// Reads LevelUpEvent and PlayerProgression changes to record progression analytics.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class ProgressionAnalyticsBridgeSystem : SystemBase
{
    private Dictionary<Entity, int> _previousXP = new();
    private float _xpAggregateTimer;
    private int _xpAggregateGained;

    private const float XPFlushInterval = 30f;

    protected override void OnUpdate()
    {
        if (!AnalyticsAPI.IsInitialized) return;
        if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Progression)) return;

        CompleteDependency();

        ProcessLevelUps();
        ProcessXPChanges();
    }

    private static readonly FixedString64Bytes ActionLevelUp = new("level_up");
    private static readonly FixedString64Bytes ActionXpGain = new("xp_gain");

    private void ProcessLevelUps()
    {
        // SystemAPI.Query on IEnableableComponent automatically filters to enabled entities
        foreach (var (levelUp, attrs, entity) in
            SystemAPI.Query<RefRO<LevelUpEvent>, RefRO<DIG.Combat.Components.CharacterAttributes>>()
                .WithAll<PlayerTag>()
                .WithEntityAccess())
        {
            var props = new FixedString512Bytes();
            props.Append("{\"newLevel\":");
            props.Append(levelUp.ValueRO.NewLevel);
            props.Append(",\"previousLevel\":");
            props.Append(levelUp.ValueRO.PreviousLevel);
            props.Append('}');

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Progression,
                Action = ActionLevelUp,
                PropertiesJson = props
            });
        }
    }

    private void ProcessXPChanges()
    {
        foreach (var (progression, entity) in
            SystemAPI.Query<RefRO<PlayerProgression>>()
                .WithAll<PlayerTag>()
                .WithEntityAccess())
        {
            int currentXP = progression.ValueRO.TotalXPEarned;

            if (_previousXP.TryGetValue(entity, out int prevXP))
            {
                int delta = currentXP - prevXP;
                if (delta > 0)
                    _xpAggregateGained += delta;
            }

            _previousXP[entity] = currentXP;
        }

        _xpAggregateTimer += SystemAPI.Time.DeltaTime;
        if (_xpAggregateTimer >= XPFlushInterval && _xpAggregateGained > 0)
        {
            var props = new FixedString512Bytes();
            props.Append("{\"xpGained\":");
            props.Append(_xpAggregateGained);
            props.Append('}');

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Progression,
                Action = ActionXpGain,
                PropertiesJson = props
            });

            _xpAggregateGained = 0;
            _xpAggregateTimer = 0f;
        }
    }

    protected override void OnDestroy()
    {
        _previousXP.Clear();
    }
}
