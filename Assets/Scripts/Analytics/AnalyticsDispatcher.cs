using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DIG.Analytics
{
    /// <summary>
    /// Background thread dispatcher following SaveFileWriter pattern.
    /// Drains ConcurrentQueue, applies privacy filter, and dispatches to targets.
    /// </summary>
    public class AnalyticsDispatcher
    {
        private readonly ConcurrentQueue<AnalyticsEvent> _eventQueue = new();
        private readonly ManualResetEventSlim _flushSignal = new(false);
        private IAnalyticsTarget[] _targets;
        private PrivacyFilter _privacyFilter;
        private volatile bool _running;
        private Thread _backgroundThread;
        private int _batchSize;
        private int _flushIntervalMs;
        private int _ringBufferCapacity;

        private List<AnalyticsEvent> _batchBuffer;
        private AnalyticsEvent[] _dispatchArray;

        private long _eventsEnqueued;
        private long _eventsDispatched;
        private long _eventsDropped;

        public long EventsEnqueued => Interlocked.Read(ref _eventsEnqueued);
        public long EventsDispatched => Interlocked.Read(ref _eventsDispatched);
        public long EventsDropped => Interlocked.Read(ref _eventsDropped);
        public int QueueDepth => _eventQueue.Count;

        public void Start(IAnalyticsTarget[] targets, PrivacyFilter filter, int batchSize, int flushIntervalMs, int ringBufferCapacity)
        {
            if (_running) return;

            _targets = targets ?? Array.Empty<IAnalyticsTarget>();
            _privacyFilter = filter;
            _batchSize = Math.Max(10, batchSize);
            _flushIntervalMs = Math.Max(1000, flushIntervalMs);
            _ringBufferCapacity = Math.Max(100, ringBufferCapacity);

            _batchBuffer = new List<AnalyticsEvent>(_batchSize);
            _dispatchArray = new AnalyticsEvent[_batchSize];

            _running = true;
            _backgroundThread = new Thread(WorkerLoop)
            {
                Name = "DIG_AnalyticsDispatch",
                IsBackground = true
            };
            _backgroundThread.Start();
        }

        public void Enqueue(AnalyticsEvent evt)
        {
            if (_eventQueue.Count >= _ringBufferCapacity)
            {
                _eventQueue.TryDequeue(out _);
                Interlocked.Increment(ref _eventsDropped);
            }

            _eventQueue.Enqueue(evt);
            Interlocked.Increment(ref _eventsEnqueued);
        }

        public void SignalFlush()
        {
            _flushSignal.Set();
        }

        public void Stop()
        {
            _running = false;
            _flushSignal.Set();
            if (_backgroundThread != null && _backgroundThread.IsAlive)
            {
                _backgroundThread.Join(3000);
                _backgroundThread = null;
            }
        }

        public void FlushBlocking()
        {
            DrainAndDispatch();
        }

        private void WorkerLoop()
        {
            while (_running)
            {
                _flushSignal.Wait(_flushIntervalMs);
                _flushSignal.Reset();
                DrainAndDispatch();
            }

            DrainAndDispatch();
        }

        private void DrainAndDispatch()
        {
            _batchBuffer.Clear();

            while (_eventQueue.TryDequeue(out var evt))
            {
                _batchBuffer.Add(evt);

                if (_batchBuffer.Count >= _batchSize)
                {
                    DispatchBatch();
                    _batchBuffer.Clear();
                }
            }

            if (_batchBuffer.Count > 0)
            {
                DispatchBatch();
                _batchBuffer.Clear();
            }
        }

        private void DispatchBatch()
        {
            int count = _batchBuffer.Count;
            if (count == 0) return;

            if (_dispatchArray.Length < count)
                _dispatchArray = new AnalyticsEvent[count];
            _batchBuffer.CopyTo(_dispatchArray);

            AnalyticsEvent[] scrubbed = _privacyFilter != null
                ? _privacyFilter.ScrubBatch(_dispatchArray, count)
                : CopyToSized(_dispatchArray, count);

            if (scrubbed == null || scrubbed.Length == 0) return;

            for (int i = 0; i < _targets.Length; i++)
            {
                try
                {
                    _targets[i].SendBatch(scrubbed);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Analytics:Dispatcher] Target '{_targets[i].TargetName}' failed: {e.Message}");
                }
            }

            Interlocked.Add(ref _eventsDispatched, scrubbed.Length);
        }

        private static AnalyticsEvent[] CopyToSized(AnalyticsEvent[] source, int count)
        {
            if (source.Length == count) return source;
            var result = new AnalyticsEvent[count];
            Array.Copy(source, result, count);
            return result;
        }
    }
}
