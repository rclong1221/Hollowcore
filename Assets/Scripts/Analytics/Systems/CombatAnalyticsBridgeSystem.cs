using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Analytics;

/// <summary>
/// Reads kill/death/damage events and quest/crafting queues, then records analytics events.
/// Uses manual EntityQuery (not SystemAPI.Query) for transient types.
/// Runs in PresentationSystemGroup BEFORE CombatEventCleanupSystem destroys events.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(DIG.Combat.Systems.CombatEventCleanupSystem))]
public partial class CombatAnalyticsBridgeSystem : SystemBase
{
    private EntityQuery _killQuery;
    private EntityQuery _combatResultQuery;
    private readonly Dictionary<Entity, int> _craftBufferLengths = new();

    private float _damageSummaryTimer;
    private float _totalDamageDealt;
    private float _totalDamageTaken;
    private int _hitCount;
    private int _missCount;

    private const float DamageSummaryInterval = 5f;

    private static readonly FixedString64Bytes ActionKill = new("kill");
    private static readonly FixedString64Bytes ActionPlayerDeath = new("player_death");
    private static readonly FixedString64Bytes ActionDamageSummary = new("damage_summary");
    private static readonly FixedString64Bytes ActionQuestAccept = new("quest_accept");
    private static readonly FixedString64Bytes ActionQuestComplete = new("quest_complete");
    private static readonly FixedString64Bytes ActionQuestAbandon = new("quest_abandon");
    private static readonly FixedString64Bytes ActionCraftSuccess = new("craft_success");

    protected override void OnCreate()
    {
        _killQuery = GetEntityQuery(
            ComponentType.ReadOnly<Player.Components.KillCredited>(),
            ComponentType.ReadOnly<PlayerTag>()
        );
        _combatResultQuery = GetEntityQuery(
            ComponentType.ReadOnly<DIG.Combat.Systems.CombatResultEvent>()
        );
    }

    protected override void OnUpdate()
    {
        if (!AnalyticsAPI.IsInitialized) return;

        CompleteDependency();

        bool combatEnabled = AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Combat);
        bool questEnabled = AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Quest);
        bool craftEnabled = AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Crafting);

        if (combatEnabled)
        {
            ProcessKills();
            ProcessDeaths();
            ProcessCombatResults();
        }

        if (questEnabled)
            ProcessQuestEvents();

        if (craftEnabled)
            ProcessCraftEvents();
    }

    private void ProcessKills()
    {
        if (_killQuery.IsEmpty) return;

        var killEntities = _killQuery.ToEntityArray(Allocator.Temp);
        var killCredits = _killQuery.ToComponentDataArray<Player.Components.KillCredited>(Allocator.Temp);

        for (int i = 0; i < killEntities.Length; i++)
        {
            var kill = killCredits[i];
            int playerLevel = 0;
            if (EntityManager.HasComponent<DIG.Combat.Components.CharacterAttributes>(killEntities[i]))
            {
                var attrs = EntityManager.GetComponentData<DIG.Combat.Components.CharacterAttributes>(killEntities[i]);
                playerLevel = attrs.Level;
            }

            var pos = kill.VictimPosition;
            var props = new FixedString512Bytes();
            props.Append("{\"playerLevel\":");
            props.Append(playerLevel);
            props.Append(",\"pos\":\"");
            AppendFloat3(ref props, pos);
            props.Append("\",\"tick\":");
            props.Append((int)kill.ServerTick);
            props.Append('}');

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Combat,
                Action = ActionKill,
                ServerTick = kill.ServerTick,
                PropertiesJson = props
            });
        }

        killEntities.Dispose();
        killCredits.Dispose();
    }

    private void ProcessDeaths()
    {
        // SystemAPI.Query on IEnableableComponent automatically filters to enabled entities
        foreach (var (diedEvent, attrs, entity) in
            SystemAPI.Query<RefRO<Player.Components.DiedEvent>, RefRO<DIG.Combat.Components.CharacterAttributes>>()
                .WithAll<PlayerTag>()
                .WithEntityAccess())
        {
            var pos = float3.zero;
            if (EntityManager.HasComponent<Unity.Transforms.LocalTransform>(entity))
            {
                var t = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                pos = t.Position;
            }

            var props = new FixedString512Bytes();
            props.Append("{\"playerLevel\":");
            props.Append(attrs.ValueRO.Level);
            props.Append(",\"pos\":\"");
            AppendFloat3(ref props, pos);
            props.Append("\"}");

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Combat,
                Action = ActionPlayerDeath,
                PropertiesJson = props
            });
        }
    }

    private void ProcessCombatResults()
    {
        if (_combatResultQuery.IsEmpty)
        {
            FlushDamageSummaryIfNeeded();
            return;
        }

        var results = _combatResultQuery.ToComponentDataArray<DIG.Combat.Systems.CombatResultEvent>(Allocator.Temp);
        for (int i = 0; i < results.Length; i++)
        {
            var cr = results[i];
            if (cr.DidHit)
            {
                _totalDamageDealt += cr.FinalDamage;
                _hitCount++;
            }
            else
            {
                _missCount++;
            }
        }
        results.Dispose();

        FlushDamageSummaryIfNeeded();
    }

    private void FlushDamageSummaryIfNeeded()
    {
        _damageSummaryTimer += SystemAPI.Time.DeltaTime;
        if (_damageSummaryTimer < DamageSummaryInterval) return;
        _damageSummaryTimer = 0f;

        if (_hitCount == 0 && _missCount == 0) return;

        var props = new FixedString512Bytes();
        props.Append("{\"damageDealt\":");
        props.Append((int)_totalDamageDealt);
        props.Append(",\"hits\":");
        props.Append(_hitCount);
        props.Append(",\"misses\":");
        props.Append(_missCount);
        props.Append('}');

        AnalyticsAPI.TrackEvent(new AnalyticsEvent
        {
            Category = AnalyticsCategory.Combat,
                Action = ActionDamageSummary,
            PropertiesJson = props
        });

        _totalDamageDealt = 0f;
        _totalDamageTaken = 0f;
        _hitCount = 0;
        _missCount = 0;
    }

    private void ProcessQuestEvents()
    {
        // Dequeue + re-enqueue pattern (same as AchievementTrackingSystem)
        // so other consumers (QuestUIBridgeSystem) still receive the events.
        int peekCount = DIG.Quest.QuestEventQueue.Count;
        for (int i = 0; i < peekCount; i++)
        {
            if (!DIG.Quest.QuestEventQueue.TryDequeue(out var qEvt))
                break;

            DIG.Quest.QuestEventQueue.Enqueue(qEvt);

            FixedString64Bytes action;
            switch (qEvt.Type)
            {
                case DIG.Quest.QuestUIEventType.QuestAccepted: action = ActionQuestAccept; break;
                case DIG.Quest.QuestUIEventType.QuestCompleted: action = ActionQuestComplete; break;
                case DIG.Quest.QuestUIEventType.QuestFailed: action = ActionQuestAbandon; break;
                default: continue;
            }

            var props = new FixedString512Bytes();
            props.Append("{\"questId\":");
            props.Append(qEvt.QuestId);
            props.Append('}');

            AnalyticsAPI.TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Quest,
                Action = action,
                PropertiesJson = props
            });
        }
    }

    private void ProcessCraftEvents()
    {
        // Track buffer length changes per station entity to detect new craft outputs.
        // Only fire analytics for newly appended elements, not pre-existing ones.
        foreach (var (outputs, entity) in
            SystemAPI.Query<DynamicBuffer<DIG.Crafting.CraftOutputElement>>()
                .WithEntityAccess())
        {
            int currentLen = outputs.Length;
            _craftBufferLengths.TryGetValue(entity, out int prevLen);

            for (int i = prevLen; i < currentLen; i++)
            {
                var output = outputs[i];
                var props = new FixedString512Bytes();
                props.Append("{\"recipeId\":");
                props.Append(output.RecipeId);
                props.Append(",\"itemTypeId\":");
                props.Append(output.OutputItemTypeId);
                props.Append(",\"quantity\":");
                props.Append(output.OutputQuantity);
                props.Append('}');

                AnalyticsAPI.TrackEvent(new AnalyticsEvent
                {
                    Category = AnalyticsCategory.Crafting,
                    Action = ActionCraftSuccess,
                    PropertiesJson = props
                });
            }

            _craftBufferLengths[entity] = currentLen;
        }
    }

    private static void AppendFloat3(ref FixedString512Bytes fs, float3 v)
    {
        fs.Append((int)(v.x * 10) / 10f);
        fs.Append(',');
        fs.Append((int)(v.y * 10) / 10f);
        fs.Append(',');
        fs.Append((int)(v.z * 10) / 10f);
    }
}
