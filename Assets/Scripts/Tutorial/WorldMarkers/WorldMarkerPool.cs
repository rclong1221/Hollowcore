using System.Collections.Generic;
using UnityEngine;

namespace DIG.Tutorial.WorldMarkers
{
    /// <summary>
    /// EPIC 18.4: Simple pool of WorldTutorialMarker instances.
    /// Pre-warms on creation, provides Acquire/Release API.
    /// </summary>
    public class WorldMarkerPool
    {
        private readonly Queue<WorldTutorialMarker> _available = new();
        private readonly List<WorldTutorialMarker> _active = new();
        private readonly Transform _parent;

        public WorldMarkerPool(Transform parent, int prewarmCount = 3)
        {
            _parent = parent;
            for (int i = 0; i < prewarmCount; i++)
            {
                var marker = CreateMarker();
                marker.Deactivate();
                _available.Enqueue(marker);
            }
        }

        public WorldTutorialMarker Acquire(Vector3 worldPosition, float edgeMargin = 40f)
        {
            WorldTutorialMarker marker;
            if (_available.Count > 0)
            {
                marker = _available.Dequeue();
            }
            else
            {
                marker = CreateMarker();
            }

            marker.Activate(worldPosition, edgeMargin);
            _active.Add(marker);
            return marker;
        }

        public void Release(WorldTutorialMarker marker)
        {
            if (marker == null) return;
            marker.Deactivate();
            _active.Remove(marker);
            _available.Enqueue(marker);
        }

        public void ReleaseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var marker = _active[i];
                marker.Deactivate();
                _available.Enqueue(marker);
            }
            _active.Clear();
        }

        private WorldTutorialMarker CreateMarker()
        {
            var go = new GameObject("TutorialMarker");
            go.transform.SetParent(_parent);
            return go.AddComponent<WorldTutorialMarker>();
        }
    }
}
