using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DIG.Analytics
{
    public class HttpTarget : IAnalyticsTarget
    {
        public string TargetName => "HttpTarget";

        private static readonly HttpClient _client = new();

        private string _endpointUrl;
        private string _apiKey;
        private int _maxRetries;
        private int _retryBaseDelayMs;
        private int _timeoutMs;
        private long _droppedBatches;

        public long DroppedBatches => _droppedBatches;

        public void Initialize(DispatchTargetConfig config)
        {
            _endpointUrl = config.EndpointUrl;
            _apiKey = DecryptApiKey(config.ApiKeyEncrypted);
            _maxRetries = config.MaxRetries;
            _retryBaseDelayMs = config.RetryBaseDelayMs;
            _timeoutMs = config.TimeoutMs;
            _client.Timeout = TimeSpan.FromMilliseconds(_timeoutMs);
        }

        public void SendBatch(AnalyticsEvent[] events)
        {
            if (string.IsNullOrEmpty(_endpointUrl) || events == null || events.Length == 0)
                return;

            string json = BuildBatchJson(events);

            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrEmpty(_apiKey))
                        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");

                    var response = _client.SendAsync(request).GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                        return;

                    if ((int)response.StatusCode < 500)
                        return; // Client error, don't retry

                }
                catch (HttpRequestException)
                {
                    // Network error, will retry
                }
                catch (TaskCanceledException)
                {
                    // Timeout, will retry
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Analytics:HttpTarget] Unexpected error: {e.Message}");
                    return;
                }

                if (attempt < _maxRetries)
                {
                    int delay = Math.Min(_retryBaseDelayMs * (1 << attempt), 30000);
                    Thread.Sleep(delay);
                }
            }

            Interlocked.Increment(ref _droppedBatches);
            Debug.LogWarning($"[Analytics:HttpTarget] Batch dropped after {_maxRetries + 1} attempts");
        }

        public void Shutdown()
        {
            // HttpClient is shared static; don't dispose
        }

        private static string BuildBatchJson(AnalyticsEvent[] events)
        {
            var sb = new StringBuilder(events.Length * 256);
            sb.Append('[');
            for (int i = 0; i < events.Length; i++)
            {
                if (i > 0) sb.Append(',');
                ref var evt = ref events[i];
                sb.Append("{\"ts\":");
                sb.Append(evt.TimestampUtcMs);
                sb.Append(",\"sid\":\"");
                sb.Append(evt.SessionId.ToString());
                sb.Append("\",\"cat\":\"");
                sb.Append(evt.Category.ToString());
                sb.Append("\",\"act\":\"");
                sb.Append(evt.Action.ToString());
                sb.Append("\",\"pid\":\"");
                sb.Append(evt.PlayerId.ToString());
                sb.Append("\",\"tick\":");
                sb.Append(evt.ServerTick);

                string props = evt.PropertiesJson.ToString();
                if (!string.IsNullOrEmpty(props) && props.Length > 2)
                {
                    sb.Append(",\"props\":");
                    sb.Append(props);
                }
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string DecryptApiKey(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            // XOR with build-time salt; for now pass through as-is
            return encrypted;
        }
    }
}
