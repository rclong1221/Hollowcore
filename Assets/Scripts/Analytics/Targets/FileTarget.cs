using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DIG.Analytics
{
    public class FileTarget : IAnalyticsTarget
    {
        public string TargetName => "FileTarget";

        private string _directory;
        private string _filePattern;
        private string _currentFilePath;
        private StreamWriter _writer;
        private int _retentionDays;
        private readonly StringBuilder _lineBuilder = new(1024);

        public void Initialize(DispatchTargetConfig config)
        {
            _directory = Path.Combine(Application.persistentDataPath, "analytics");
            _filePattern = string.IsNullOrEmpty(config.FileNamePattern)
                ? "analytics_{sessionId}.jsonl"
                : config.FileNamePattern;
            _retentionDays = 90;

            if (!Directory.Exists(_directory))
                Directory.CreateDirectory(_directory);

            CleanOldFiles();
        }

        public void SetSession(string sessionId, int retentionDays)
        {
            _retentionDays = retentionDays;
            string fileName = _filePattern.Replace("{sessionId}", sessionId);
            _currentFilePath = Path.Combine(_directory, fileName);

            _writer?.Dispose();
            _writer = new StreamWriter(_currentFilePath, append: true, Encoding.UTF8) { AutoFlush = false };
        }

        public void SendBatch(AnalyticsEvent[] events)
        {
            if (_writer == null || events == null) return;

            try
            {
                for (int i = 0; i < events.Length; i++)
                {
                    ref var evt = ref events[i];
                    _lineBuilder.Clear();
                    _lineBuilder.Append("{\"ts\":");
                    _lineBuilder.Append(evt.TimestampUtcMs);
                    _lineBuilder.Append(",\"sid\":\"");
                    _lineBuilder.Append(evt.SessionId.ToString());
                    _lineBuilder.Append("\",\"cat\":\"");
                    _lineBuilder.Append(evt.Category.ToString());
                    _lineBuilder.Append("\",\"act\":\"");
                    _lineBuilder.Append(evt.Action.ToString());
                    _lineBuilder.Append("\",\"pid\":\"");
                    _lineBuilder.Append(evt.PlayerId.ToString());
                    _lineBuilder.Append("\",\"tick\":");
                    _lineBuilder.Append(evt.ServerTick);

                    string props = evt.PropertiesJson.ToString();
                    if (!string.IsNullOrEmpty(props) && props.Length > 2)
                    {
                        _lineBuilder.Append(",\"props\":");
                        _lineBuilder.Append(props);
                    }

                    _lineBuilder.Append('}');
                    _writer.WriteLine(_lineBuilder.ToString());
                }
                _writer.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics:FileTarget] Write failed: {e.Message}");
            }
        }

        public void Shutdown()
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics:FileTarget] Shutdown error: {e.Message}");
            }
        }

        private void CleanOldFiles()
        {
            if (_retentionDays <= 0 || !Directory.Exists(_directory)) return;

            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
                foreach (var file in Directory.GetFiles(_directory, "*.jsonl"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics:FileTarget] Cleanup error: {e.Message}");
            }
        }
    }
}
