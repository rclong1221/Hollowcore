using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace DIG.Analytics
{
    /// <summary>
    /// Static, thread-safe analytics API. All gameplay systems funnel events through here.
    /// Uses ConcurrentQueue internally via AnalyticsDispatcher for zero main-thread I/O.
    /// </summary>
    public static class AnalyticsAPI
    {
        private static AnalyticsDispatcher _dispatcher;
        private static PrivacyFilter _privacyFilter;
        private static AnalyticsProfile _profile;
        private static Dictionary<string, object> _superProperties;
        private static Dictionary<AnalyticsCategory, float> _categorySampleRates;
        private static FixedString64Bytes _sessionId;
        private static FixedString64Bytes _playerId;
        private static bool _initialized;
        private static bool _debugLogging;
        private static uint _enabledCategories;
        private static float _globalSampleRate;
        private static readonly System.Random _sampler = new();
        private static FileTarget _fileTarget;

        public static bool IsInitialized => _initialized;
        public static AnalyticsDispatcher Dispatcher => _dispatcher;
        public static PrivacyFilter Privacy => _privacyFilter;
        public static FixedString64Bytes CurrentSessionId => _sessionId;
        public static bool DebugLogging => _debugLogging;

        private static readonly AnalyticsEvent[] _recentRing = new AnalyticsEvent[MaxRecentEvents];
        private static readonly object _recentLock = new();
        private const int MaxRecentEvents = 500;
        private static int _recentHead;
        private static int _recentCount;
        private static int _recentVersion;
        private static AnalyticsEvent[] _recentSnapshot = Array.Empty<AnalyticsEvent>();
        private static int _snapshotVersion = -1;

        public static IReadOnlyList<AnalyticsEvent> RecentEvents
        {
            get
            {
                lock (_recentLock)
                {
                    if (_snapshotVersion == _recentVersion)
                        return _snapshotVersion < 0 ? Array.Empty<AnalyticsEvent>() : _recentSnapshot;

                    var snap = new AnalyticsEvent[_recentCount];
                    int start = (_recentHead - _recentCount + MaxRecentEvents) % MaxRecentEvents;
                    for (int i = 0; i < _recentCount; i++)
                        snap[i] = _recentRing[(start + i) % MaxRecentEvents];

                    _recentSnapshot = snap;
                    _snapshotVersion = _recentVersion;
                    return snap;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
            _dispatcher = null;
            _privacyFilter = null;
            _profile = null;
            _superProperties = null;
            _categorySampleRates = null;
            _sessionId = default;
            _playerId = default;
            _initialized = false;
            _debugLogging = false;
            _enabledCategories = 0;
            _globalSampleRate = 1f;
            _fileTarget = null;
            lock (_recentLock)
            {
                Array.Clear(_recentRing, 0, _recentRing.Length);
                _recentHead = 0;
                _recentCount = 0;
                _recentVersion = 0;
                _recentSnapshot = Array.Empty<AnalyticsEvent>();
                _snapshotVersion = -1;
            }
        }

        public static void Initialize(AnalyticsProfile profile)
        {
            if (_initialized) return;
            if (profile == null)
            {
                Debug.LogWarning("[Analytics] No AnalyticsProfile provided; analytics disabled.");
                return;
            }

            _profile = profile;
            _enabledCategories = (uint)profile.EnabledCategories;
            _globalSampleRate = profile.GlobalSampleRate;
            _debugLogging = profile.EnableDebugLogging;

            _categorySampleRates = new Dictionary<AnalyticsCategory, float>();
            if (profile.CategorySampleRates != null)
            {
                foreach (var entry in profile.CategorySampleRates)
                    _categorySampleRates[entry.Category] = entry.SampleRate;
            }

            _superProperties = new Dictionary<string, object>();

            // Build dispatch targets
            var targets = new List<IAnalyticsTarget>();
            _fileTarget = new FileTarget();
            bool hasFileTarget = false;

            if (profile.DispatchTargets != null)
            {
                foreach (var cfg in profile.DispatchTargets)
                {
                    if (cfg == null || !cfg.Enabled) continue;
                    IAnalyticsTarget target = cfg.TargetType switch
                    {
                        DispatchTargetType.File => CreateFileTarget(cfg, out hasFileTarget),
                        DispatchTargetType.Http => CreateTarget<HttpTarget>(cfg),
                        DispatchTargetType.UnityAnalytics => CreateTarget<UnityAnalyticsTarget>(cfg),
                        _ => null
                    };
                    if (target != null) targets.Add(target);
                }
            }

            if (!hasFileTarget)
            {
                _fileTarget.Initialize(ScriptableObject.CreateInstance<DispatchTargetConfig>());
                targets.Insert(0, _fileTarget);
            }

            _initialized = true;

            // Privacy filter is created here but consent is applied by bootstrap after reading PlayerPrefs
            _privacyFilter = new PrivacyFilter(null, true);

            _dispatcher = new AnalyticsDispatcher();
            _dispatcher.Start(
                targets.ToArray(),
                _privacyFilter,
                profile.BatchSize,
                (int)(profile.FlushIntervalSeconds * 1000),
                profile.RingBufferCapacity
            );

            int enabledCount = CountBits(_enabledCategories);
            Debug.Log($"[Analytics] Initialized with {enabledCount} categories, sample rate {_globalSampleRate}, {targets.Count} dispatch targets");
        }

        private static IAnalyticsTarget CreateFileTarget(DispatchTargetConfig cfg, out bool wasFile)
        {
            _fileTarget.Initialize(cfg);
            wasFile = true;
            return _fileTarget;
        }

        private static IAnalyticsTarget CreateTarget<T>(DispatchTargetConfig cfg) where T : IAnalyticsTarget, new()
        {
            var t = new T();
            t.Initialize(cfg);
            return t;
        }

        public static void StartSession(string playerId)
        {
            _sessionId = new FixedString64Bytes(Guid.NewGuid().ToString("N").Substring(0, 24));
            _playerId = new FixedString64Bytes(playerId ?? "local");

            _fileTarget?.SetSession(_sessionId.ToString(), 90);

            TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Session,
                Action = new FixedString64Bytes("session_start"),
                SessionId = _sessionId,
                PlayerId = _playerId,
                TimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        public static void EndSession(string reason)
        {
            if (!_initialized) return;

            var props = new FixedString512Bytes();
            props.Append("{\"reason\":\"");
            props.Append(reason ?? "unknown");
            props.Append("\",\"eventsRecorded\":");
            props.Append((int)(_dispatcher?.EventsEnqueued ?? 0));
            props.Append(",\"eventsDropped\":");
            props.Append((int)(_dispatcher?.EventsDropped ?? 0));
            props.Append('}');

            TrackEvent(new AnalyticsEvent
            {
                Category = AnalyticsCategory.Session,
                Action = new FixedString64Bytes("session_end"),
                SessionId = _sessionId,
                PlayerId = _playerId,
                TimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PropertiesJson = props
            });
        }

        public static void TrackEvent(string category, string action, Dictionary<string, object> properties)
        {
            if (!_initialized) return;

            if (!Enum.TryParse<AnalyticsCategory>(category, true, out var cat))
                cat = AnalyticsCategory.Custom;

            if (!IsCategoryEnabled(cat)) return;
            if (!PassesSampling(cat)) return;

            var evt = new AnalyticsEvent
            {
                Category = cat,
                Action = new FixedString64Bytes(action),
                SessionId = _sessionId,
                PlayerId = _playerId,
                TimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PropertiesJson = SerializeProperties(properties)
            };

            EnqueueInternal(evt);
        }

        public static void TrackEvent(AnalyticsEvent evt)
        {
            if (!_initialized) return;
            if (!IsCategoryEnabled(evt.Category)) return;
            if (!PassesSampling(evt.Category)) return;

            if (evt.SessionId.Length == 0) evt.SessionId = _sessionId;
            if (evt.PlayerId.Length == 0) evt.PlayerId = _playerId;
            if (evt.TimestampUtcMs == 0) evt.TimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            EnqueueInternal(evt);
        }

        private static void EnqueueInternal(AnalyticsEvent evt)
        {
            _dispatcher?.Enqueue(evt);

            lock (_recentLock)
            {
                _recentRing[_recentHead] = evt;
                _recentHead = (_recentHead + 1) % MaxRecentEvents;
                if (_recentCount < MaxRecentEvents) _recentCount++;
                _recentVersion++;
            }

#if UNITY_EDITOR
            if (_debugLogging)
                Debug.Log($"[Analytics] {evt.Category}:{evt.Action} | {evt.PropertiesJson}");
#endif
        }

        public static bool IsCategoryEnabled(AnalyticsCategory category)
        {
            return (_enabledCategories & (uint)category) != 0;
        }

        public static void SetSuperProperties(Dictionary<string, object> props)
        {
            if (props == null) return;
            _superProperties ??= new Dictionary<string, object>();
            foreach (var kv in props)
                _superProperties[kv.Key] = kv.Value;
        }

        public static void SetPrivacyConsent(bool analytics, bool crashReports, bool personalData)
        {
            var level = PrivacyConsentLevel.Essential;
            if (analytics) level |= PrivacyConsentLevel.Analytics;
            if (crashReports) level |= PrivacyConsentLevel.CrashReports;
            if (personalData) level |= PrivacyConsentLevel.PersonalData;
            _privacyFilter?.SetConsent(level);
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;

            _dispatcher?.Stop();
            _dispatcher?.FlushBlocking();
        }

        private static bool PassesSampling(AnalyticsCategory category)
        {
            float rate = _globalSampleRate;
            if (_categorySampleRates != null && _categorySampleRates.TryGetValue(category, out float catRate))
                rate = catRate;

            if (rate >= 1f) return true;
            if (rate <= 0f) return false;

            lock (_sampler)
            {
                return _sampler.NextDouble() < rate;
            }
        }

        private static FixedString512Bytes SerializeProperties(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
                return default;

            var fs = new FixedString512Bytes();
            fs.Append('{');
            bool first = true;
            foreach (var kv in properties)
            {
                if (!first) fs.Append(',');
                first = false;
                fs.Append('"');
                fs.Append(kv.Key);
                fs.Append("\":");
                if (kv.Value is string s)
                {
                    fs.Append('"');
                    fs.Append(s);
                    fs.Append('"');
                }
                else if (kv.Value is int i)
                    fs.Append(i);
                else if (kv.Value is float f)
                    fs.Append((int)(f * 100) / 100f);
                else if (kv.Value is bool b)
                    fs.Append(b ? "true" : "false");
                else
                    fs.Append(kv.Value?.ToString() ?? "null");
            }
            fs.Append('}');
            return fs;
        }

        private static int CountBits(uint v)
        {
            int c = 0;
            while (v != 0) { c += (int)(v & 1); v >>= 1; }
            return c;
        }
    }
}
