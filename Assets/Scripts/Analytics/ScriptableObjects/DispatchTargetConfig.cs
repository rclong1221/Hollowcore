using UnityEngine;

namespace DIG.Analytics
{
    public enum DispatchTargetType : byte
    {
        File = 0,
        Http = 1,
        UnityAnalytics = 2
    }

    [CreateAssetMenu(menuName = "DIG/Analytics/Dispatch Target")]
    public class DispatchTargetConfig : ScriptableObject
    {
        public DispatchTargetType TargetType = DispatchTargetType.File;
        public bool Enabled = true;

        [Header("HTTP Target")]
        public string EndpointUrl = "";
        public string ApiKeyEncrypted = "";

        [Min(1)]
        public int BatchSize = 50;

        [Range(0, 5)]
        public int MaxRetries = 3;

        [Min(100)]
        public int RetryBaseDelayMs = 1000;

        [Min(1000)]
        public int TimeoutMs = 5000;

        [Header("File Target")]
        public string FileNamePattern = "analytics_{sessionId}.jsonl";
    }
}
