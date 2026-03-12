using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Analytics
{
    /// <summary>
    /// Loads AnalyticsProfile + PrivacyPolicy from Resources, creates singletons,
    /// initializes AnalyticsAPI + ABTestManager. Runs once then self-disables.
    /// Follows PersistenceBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class AnalyticsBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            var profile = Resources.Load<AnalyticsProfile>("AnalyticsProfile");
            if (profile == null)
            {
                Debug.LogWarning("[Analytics] No AnalyticsProfile found at Resources/AnalyticsProfile. Analytics disabled.");
                Enabled = false;
                return;
            }

            var privacyPolicy = Resources.Load<PrivacyPolicy>("PrivacyPolicy");
            if (privacyPolicy == null)
            {
                privacyPolicy = ScriptableObject.CreateInstance<PrivacyPolicy>();
                Debug.LogWarning("[Analytics] No PrivacyPolicy found at Resources/PrivacyPolicy. Using defaults.");
            }

            AnalyticsAPI.Initialize(profile);

            if (AnalyticsAPI.Privacy != null)
            {
                bool analyticsConsent = PlayerPrefs.GetInt("analytics_consent", privacyPolicy.DefaultAnalyticsConsent ? 1 : 0) == 1;
                bool crashConsent = PlayerPrefs.GetInt("crash_consent", privacyPolicy.DefaultCrashReportConsent ? 1 : 0) == 1;
                bool personalConsent = PlayerPrefs.GetInt("personal_consent", privacyPolicy.DefaultPersonalDataConsent ? 1 : 0) == 1;

                if (privacyPolicy.RequireExplicitConsent && !PlayerPrefs.HasKey("analytics_consent"))
                {
                    analyticsConsent = false;
                    crashConsent = false;
                    personalConsent = false;
                }

                AnalyticsAPI.SetPrivacyConsent(analyticsConsent, crashConsent, personalConsent);
            }

            // A/B test initialization
            var abTests = Resources.LoadAll<ABTestConfig>("ABTests");
            string playerId = "local";
            ABTestManager.Initialize(abTests, playerId);

            AnalyticsAPI.StartSession(playerId);

            // Super properties
            if (profile.IncludeSuperProperties)
            {
                var superProps = new Dictionary<string, object>
                {
                    { "buildVersion", Application.version },
                    { "platform", Application.platform.ToString() },
                    { "unityVersion", Application.unityVersion }
                };

                var abAssignments = ABTestManager.GetAllAssignments();
                if (abAssignments.Count > 0)
                    superProps["ab_tests"] = abAssignments;

                AnalyticsAPI.SetSuperProperties(superProps);
            }

            // Create ECS singletons
            var configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(configEntity, new AnalyticsConfig
            {
                EnabledCategories = (uint)profile.EnabledCategories,
                SampleRate = profile.GlobalSampleRate,
                FlushIntervalSec = profile.FlushIntervalSeconds
            });

            var sessionEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(sessionEntity, new SessionState
            {
                SessionId = AnalyticsAPI.CurrentSessionId,
                StartTick = 0,
                PlayerCount = 0
            });

            Debug.Log("[Analytics] Bootstrap complete.");
            Enabled = false;
        }
    }
}
