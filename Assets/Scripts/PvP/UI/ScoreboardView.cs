using UnityEngine;
using UnityEngine.UI;

namespace DIG.PvP.UI
{
    /// <summary>
    /// EPIC 17.10: Tab-toggled scoreboard displaying K/D/A columns for all players.
    /// Toggle with Tab key. Sorted by match score descending.
    /// </summary>
    public class ScoreboardView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _entryContainer;
        [SerializeField] private GameObject _entryPrefab;

        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        private PvPScoreboardEntry[] _currentEntries;
        private bool _visible;

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                SetVisible(!_visible);
            if (Input.GetKeyUp(_toggleKey))
                SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_panel != null)
                _panel.SetActive(visible);
        }

        public void UpdateScoreboard(PvPScoreboardEntry[] entries)
        {
            _currentEntries = entries;
            if (!_visible || _entryContainer == null || _entryPrefab == null) return;
            RefreshEntries();
        }

        private void RefreshEntries()
        {
            // Clear existing entries
            for (int i = _entryContainer.childCount - 1; i >= 0; i--)
                Destroy(_entryContainer.GetChild(i).gameObject);

            if (_currentEntries == null) return;

            for (int i = 0; i < _currentEntries.Length; i++)
            {
                var entry = _currentEntries[i];
                var go = Instantiate(_entryPrefab, _entryContainer);

                var texts = go.GetComponentsInChildren<Text>();
                if (texts.Length >= 4)
                {
                    texts[0].text = entry.PlayerName ?? "Player";
                    texts[1].text = entry.Kills.ToString();
                    texts[2].text = entry.Deaths.ToString();
                    texts[3].text = entry.Assists.ToString();
                }

                if (entry.IsLocalPlayer)
                {
                    var bg = go.GetComponent<Image>();
                    if (bg != null) bg.color = new Color(1f, 1f, 0.8f, 0.15f);
                }
            }
        }
    }
}
