using System;
using UnityEngine;

namespace DIG.Analytics
{
    [CreateAssetMenu(menuName = "DIG/Analytics/AB Test Config")]
    public class ABTestConfig : ScriptableObject
    {
        public string TestId = "";
        public bool IsActive = true;
        public ABTestVariant[] Variants = new ABTestVariant[]
        {
            new() { VariantName = "control", Weight = 1f },
            new() { VariantName = "variant_a", Weight = 1f }
        };
        public string StartDate = "";
        public string EndDate = "";
    }

    [Serializable]
    public class ABTestVariant
    {
        public string VariantName = "control";

        [Min(0f)]
        public float Weight = 1.0f;

        public string[] FeatureFlags = Array.Empty<string>();
    }
}
