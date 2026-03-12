using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Async file writer using a background thread.
    /// Implements write-ahead protocol: write to .tmp, atomic rename to final path.
    /// </summary>
    public static class SaveFileWriter
    {
        private struct WriteRequest
        {
            public string FilePath;
            public byte[] Data;
            public string MetadataJson;
            public string MetadataPath;
        }

        private static readonly ConcurrentQueue<WriteRequest> _queue = new();
        private static Thread _thread;
        private static volatile bool _running;
        private static readonly ManualResetEventSlim _signal = new(false);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Stop();
            while (_queue.TryDequeue(out _)) { }
        }

        public static void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(WorkerLoop)
            {
                Name = "DIG_SaveWriter",
                IsBackground = true
            };
            _thread.Start();
        }

        public static void Stop()
        {
            _running = false;
            _signal.Set();
            if (_thread != null && _thread.IsAlive)
            {
                _thread.Join(2000);
                _thread = null;
            }
        }

        public static void EnqueueWrite(string filePath, byte[] data, string metadataJson = null, string metadataPath = null)
        {
            _queue.Enqueue(new WriteRequest
            {
                FilePath = filePath,
                Data = data,
                MetadataJson = metadataJson,
                MetadataPath = metadataPath
            });
            _signal.Set();
        }

        /// <summary>
        /// Drains the write queue synchronously. Only used on application shutdown.
        /// </summary>
        public static void FlushBlocking()
        {
            while (_queue.TryDequeue(out var req))
            {
                ProcessWrite(req);
            }
        }

        private static void WorkerLoop()
        {
            while (_running)
            {
                _signal.Wait(500);
                _signal.Reset();

                while (_queue.TryDequeue(out var req))
                {
                    ProcessWrite(req);
                }
            }

            // Drain remaining on shutdown
            while (_queue.TryDequeue(out var req))
            {
                ProcessWrite(req);
            }
        }

        private static void ProcessWrite(WriteRequest req)
        {
            string tmpPath = req.FilePath + ".tmp";
            try
            {
                string dir = Path.GetDirectoryName(req.FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Step 1: Write to .tmp
                File.WriteAllBytes(tmpPath, req.Data);

                // Step 2: Atomic rename (overwrite if exists)
                if (File.Exists(req.FilePath))
                    File.Delete(req.FilePath);
                File.Move(tmpPath, req.FilePath);

                // Step 3: Write metadata sidecar (non-critical)
                if (!string.IsNullOrEmpty(req.MetadataJson) && !string.IsNullOrEmpty(req.MetadataPath))
                {
                    File.WriteAllText(req.MetadataPath, req.MetadataJson);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveFileWriter] Failed to write {req.FilePath}: {e.Message}");

                // Clean up .tmp if it exists
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); }
                catch { /* ignore cleanup failure */ }
            }
        }
    }
}
