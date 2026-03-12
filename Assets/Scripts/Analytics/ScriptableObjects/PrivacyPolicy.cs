using UnityEngine;

namespace DIG.Analytics
{
    [CreateAssetMenu(menuName = "DIG/Analytics/Privacy Policy")]
    public class PrivacyPolicy : ScriptableObject
    {
        public bool DefaultAnalyticsConsent;
        public bool DefaultCrashReportConsent;
        public bool DefaultPersonalDataConsent;

        public bool EssentialEventsAlwaysOn = true;

        [Min(1)]
        public int DataRetentionDays = 90;

        public string[] PiiFields = { "playerId", "playerName", "ip" };

        public bool RequireExplicitConsent = true;
        public GameObject ConsentDialogPrefab;
    }
}
