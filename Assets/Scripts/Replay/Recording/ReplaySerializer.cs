using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Async file writer for replay data using a background thread.
    /// Follows SaveFileWriter pattern: ConcurrentQueue + Thread + ManualResetEventSlim.
    /// Supports append-mode writes for frame data and header overwrites for finalization.
    /// </summary>
    public static class ReplaySerializer
    {
        private struct FlushRequest
        {
            public string FilePath;
            public byte[] Data;
            public bool IsHeader;
            public bool IsFinalFlush;
        }

        private static readonly ConcurrentQueue<FlushRequest> _queue = new();
        private static Thread _thread;
        private static volatile bool _running;
        private static readonly ManualResetEventSlim _signal = new(false);

        // CRC32 lookup table (precomputed, ~8x faster than bit-by-bit)
        private static readonly uint[] CRC32Table = GenerateCRC32Table();

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
                Name = "DIG_ReplayWriter",
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

        /// <summary>
        /// Enqueue data to be written. If isHeader=true, writes at file start (overwrites).
        /// If isFinal=true, computes CRC32 and renames .tmp to .digreplay.
        /// </summary>
        public static void EnqueueFlush(string filePath, byte[] data, bool isHeader = false, bool isFinal = false)
        {
            _queue.Enqueue(new FlushRequest
            {
                FilePath = filePath,
                Data = data,
                IsHeader = isHeader,
                IsFinalFlush = isFinal
            });
            _signal.Set();
        }

        /// <summary>
        /// Drain the write queue synchronously. Only used on application shutdown.
        /// </summary>
        public static void FlushBlocking()
        {
            while (_queue.TryDequeue(out var req))
            {
                ProcessFlush(req);
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
                    ProcessFlush(req);
                }
            }

            // Drain remaining on shutdown
            while (_queue.TryDequeue(out var req))
            {
                ProcessFlush(req);
            }
        }

        private static void ProcessFlush(FlushRequest req)
        {
            try
            {
                string dir = Path.GetDirectoryName(req.FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tmpPath = req.FilePath + ".tmp";

                if (req.IsFinalFlush)
                {
                    // Final flush: write remaining data, compute CRC via streaming, rename
                    if (req.Data != null && req.Data.Length > 0)
                    {
                        using var fs = new FileStream(tmpPath, FileMode.OpenOrCreate, FileAccess.Write);
                        fs.Seek(0, SeekOrigin.End);
                        fs.Write(req.Data, 0, req.Data.Length);
                    }

                    // Compute CRC32 via streaming read (avoids File.ReadAllBytes allocation)
                    if (File.Exists(tmpPath))
                    {
                        uint crc = ComputeCRC32Streaming(tmpPath);

                        // Patch CRC into header at offset 31 (after Magic+Version+TickRate+Timestamp+Duration+TotalFrames+PeakEntity+PlayerCount+MapHash)
                        using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.Write))
                        {
                            if (fs.Length >= 35)
                            {
                                fs.Seek(31, SeekOrigin.Begin);
                                byte[] crcBytes = BitConverter.GetBytes(crc);
                                fs.Write(crcBytes, 0, 4);
                            }
                        }

                        // Rename .tmp to final path
                        if (File.Exists(req.FilePath))
                            File.Delete(req.FilePath);
                        File.Move(tmpPath, req.FilePath);
                    }
                }
                else if (req.IsHeader)
                {
                    // Header write: overwrite from start
                    using var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write);
                    fs.Write(req.Data, 0, req.Data.Length);
                }
                else
                {
                    // Append frame data
                    using var fs = new FileStream(tmpPath, FileMode.OpenOrCreate, FileAccess.Write);
                    fs.Seek(0, SeekOrigin.End);
                    fs.Write(req.Data, 0, req.Data.Length);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplaySerializer] Failed to write {req.FilePath}: {e.Message}");
            }
        }

        /// <summary>
        /// Compute CRC32 via streaming file read to avoid loading entire file into memory.
        /// Uses 64KB buffer for efficient I/O.
        /// </summary>
        private static uint ComputeCRC32Streaming(string filePath)
        {
            uint crc = 0xFFFFFFFF;
            byte[] buffer = new byte[65536]; // 64KB read buffer

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            int bytesRead;
            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    crc = (crc >> 8) ^ CRC32Table[(crc ^ buffer[i]) & 0xFF];
                }
            }

            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>
        /// CRC32 computation using precomputed lookup table (~8x faster than bit-by-bit).
        /// </summary>
        public static uint ComputeCRC32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc = (crc >> 8) ^ CRC32Table[(crc ^ data[i]) & 0xFF];
            }
            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>
        /// Generate the 256-entry CRC32 lookup table at static init time.
        /// </summary>
        private static uint[] GenerateCRC32Table()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320;
                    else
                        crc >>= 1;
                }
                table[i] = crc;
            }
            return table;
        }
    }
}
