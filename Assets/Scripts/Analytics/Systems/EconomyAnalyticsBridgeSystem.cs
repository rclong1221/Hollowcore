using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Analytics;
using DIG.Economy;

/// <summary>
/// Detects CurrencyInventory changes via ECS change filter and records economy events.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class EconomyAnalyticsBridgeSystem : SystemBase
{
    private EntityQuery _currencyQuery;
    private Dictionary<Entity, CurrencyInventory> _previousValues = new();

    private static readonly FixedString64Bytes ActionCurrencyGain = new("currency_gain");
    private static readonly FixedString64Bytes ActionCurrencySpend = new("currency_spend");

    protected override void OnCreate()
    {
        _currencyQuery = GetEntityQuery(
            ComponentType.ReadOnly<CurrencyInventory>(),
            ComponentType.ReadOnly<PlayerTag>()
        );
        _currencyQuery.AddChangedVersionFilter(ComponentType.ReadOnly<CurrencyInventory>());
        RequireForUpdate(_currencyQuery);
    }

    protected override void OnUpdate()
    {
        if (!AnalyticsAPI.IsInitialized) return;
        if (!AnalyticsAPI.IsCategoryEnabled(AnalyticsCategory.Economy)) return;
        if (_currencyQuery.IsEmpty) return;

        CompleteDependency();

        var entities = _currencyQuery.ToEntityArray(Allocator.Temp);
        var currencies = _currencyQuery.ToComponentDataArray<CurrencyInventory>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            var current = currencies[i];

            if (_previousValues.TryGetValue(entity, out var prev))
            {
                RecordDelta("Gold", prev.Gold, current.Gold);
                RecordDelta("Premium", prev.Premium, current.Premium);
                RecordDelta("Crafting", prev.Crafting, current.Crafting);
            }

            _previousValues[entity] = current;
        }

        entities.Dispose();
        currencies.Dispose();
    }

    private static void RecordDelta(string currencyType, int oldVal, int newVal)
    {
        int delta = newVal - oldVal;
        if (delta == 0) return;

        var action = delta > 0 ? ActionCurrencyGain : ActionCurrencySpend;
        int amount = delta > 0 ? delta : -delta;

        var props = new FixedString512Bytes();
        props.Append("{\"currencyType\":\"");
        props.Append(currencyType);
        props.Append("\",\"amount\":");
        props.Append(amount);
        props.Append(",\"newBalance\":");
        props.Append(newVal);
        props.Append('}');

        AnalyticsAPI.TrackEvent(new AnalyticsEvent
        {
            Category = AnalyticsCategory.Economy,
            Action = action,
            PropertiesJson = props
        });
    }

    protected override void OnDestroy()
    {
        _previousValues.Clear();
    }
}
