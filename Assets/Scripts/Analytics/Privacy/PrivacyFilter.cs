using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;

namespace DIG.Analytics
{
    [Flags]
    public enum PrivacyConsentLevel : byte
    {
        None         = 0x0,
        Essential    = 0x1,
        Analytics    = 0x2,
        CrashReports = 0x4,
        PersonalData = 0x8
    }

    public class PrivacyFilter
    {
        private PrivacyConsentLevel _currentConsent;
        private readonly HashSet<string> _piiFields;
        private bool _essentialAlwaysOn;
        private readonly SHA256 _sha = SHA256.Create();
        private readonly byte[] _hashInputBuffer = new byte[256];
        private string _cachedRawId;
        private FixedString64Bytes _cachedHashedId;

        public PrivacyConsentLevel CurrentConsent => _currentConsent;

        public PrivacyFilter(string[] piiFields, bool essentialAlwaysOn)
        {
            _piiFields = new HashSet<string>(piiFields ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _essentialAlwaysOn = essentialAlwaysOn;
        }

        public void SetConsent(PrivacyConsentLevel consent)
        {
            _currentConsent = consent;
        }

        public AnalyticsEvent[] ScrubBatch(AnalyticsEvent[] events)
        {
            return ScrubBatch(events, events?.Length ?? 0);
        }

        public AnalyticsEvent[] ScrubBatch(AnalyticsEvent[] events, int count)
        {
            if (events == null || count == 0) return Array.Empty<AnalyticsEvent>();

            var filtered = new List<AnalyticsEvent>(count);

            for (int i = 0; i < count; i++)
            {
                ref var evt = ref events[i];
                bool isEssential = evt.Category == AnalyticsCategory.Session;

                if (isEssential && _essentialAlwaysOn)
                {
                    filtered.Add(ScrubPii(evt));
                    continue;
                }

                if ((_currentConsent & PrivacyConsentLevel.Analytics) == 0)
                    continue;

                filtered.Add(ScrubPii(evt));
            }

            return filtered.ToArray();
        }

        private AnalyticsEvent ScrubPii(AnalyticsEvent evt)
        {
            if ((_currentConsent & PrivacyConsentLevel.PersonalData) != 0)
                return evt;

            if (evt.PlayerId.Length > 0)
            {
                evt.PlayerId = HashPlayerId(evt.PlayerId.ToString());
            }

            return evt;
        }

        private FixedString64Bytes HashPlayerId(string rawId)
        {
            if (string.Equals(rawId, _cachedRawId, StringComparison.Ordinal))
                return _cachedHashedId;

            int byteCount = Encoding.UTF8.GetByteCount(rawId);
            byte[] inputBytes = byteCount <= _hashInputBuffer.Length
                ? _hashInputBuffer
                : new byte[byteCount];
            Encoding.UTF8.GetBytes(rawId, 0, rawId.Length, inputBytes, 0);

            byte[] hash = _sha.ComputeHash(inputBytes, 0, byteCount);
            var result = new FixedString64Bytes();
            for (int i = 0; i < 8; i++)
            {
                byte b = hash[i];
                result.Append("0123456789abcdef"[b >> 4]);
                result.Append("0123456789abcdef"[b & 0xF]);
            }

            _cachedRawId = rawId;
            _cachedHashedId = result;
            return result;
        }
    }
}
