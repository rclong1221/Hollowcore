using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.PvP.UI
{
    /// <summary>
    /// EPIC 17.10: Scrolling kill notifications displayed in the top-right corner.
    /// Entries fade out after a configurable duration.
    /// </summary>
    public class KillFeedView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _entryContainer;
        [SerializeField] private GameObject _entryPrefab;

        [Header("Settings")]
        [SerializeField] private float _entryLifetime = 5f;
        [SerializeField] private int _maxEntries = 6;

        private readonly List<KillFeedEntryInstance> _activeEntries = new List<KillFeedEntryInstance>();

        private void Update()
        {
            // Remove expired entries
            for (int i = _activeEntries.Count - 1; i >= 0; i--)
            {
                _activeEntries[i].TimeRemaining -= Time.deltaTime;
                if (_activeEntries[i].TimeRemaining <= 0f)
                {
                    Destroy(_activeEntries[i].GameObject);
                    _activeEntries.RemoveAt(i);
                }
                else if (_activeEntries[i].TimeRemaining < 1f)
                {
                    // Fade out
                    var cg = _activeEntries[i].CanvasGroup;
                    if (cg != null) cg.alpha = _activeEntries[i].TimeRemaining;
                }
            }
        }

        public void OnKillFeedEvent(PvPKillFeedUIEntry entry)
        {
            if (_entryContainer == null || _entryPrefab == null) return;

            // Remove oldest if at capacity
            while (_activeEntries.Count >= _maxEntries)
            {
                Destroy(_activeEntries[0].GameObject);
                _activeEntries.RemoveAt(0);
            }

            var go = Instantiate(_entryPrefab, _entryContainer);
            var texts = go.GetComponentsInChildren<Text>();
            if (texts.Length >= 1)
            {
                string killerColor = GetTeamColorHex(entry.KillerTeam);
                string victimColor = GetTeamColorHex(entry.VictimTeam);
                texts[0].text = $"<color={killerColor}>{entry.KillerName}</color> eliminated <color={victimColor}>{entry.VictimName}</color>";
            }

            _activeEntries.Add(new KillFeedEntryInstance
            {
                GameObject = go,
                CanvasGroup = go.GetComponent<CanvasGroup>(),
                TimeRemaining = _entryLifetime
            });
        }

        private static string GetTeamColorHex(byte teamId)
        {
            return teamId switch
            {
                1 => "#FF6666",
                2 => "#6666FF",
                3 => "#66FF66",
                4 => "#FFFF66",
                _ => "#FFFFFF"
            };
        }

        private class KillFeedEntryInstance
        {
            public GameObject GameObject;
            public CanvasGroup CanvasGroup;
            public float TimeRemaining;
        }
    }
}
