using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Full-screen achievement panel with category tabs, progress bars, and filters.
    /// Implements IAchievementUIProvider for panel functionality.
    /// Uses object pooling to avoid Instantiate/Destroy churn on refresh.
    /// </summary>
    public class AchievementPanelView : MonoBehaviour, IAchievementUIProvider
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _completionText;

        [Header("Category Tabs")]
        [SerializeField] private Button[] _categoryButtons;
        [SerializeField] private string[] _categoryLabels;

        [Header("Achievement List")]
        [SerializeField] private RectTransform _listContent;
        [SerializeField] private GameObject _entryPrefab;

        [Header("Filter")]
        [SerializeField] private TMP_Dropdown _filterDropdown;

        private AchievementPanelData _latestData;
        private AchievementCategory? _selectedCategory;
        private FilterMode _filterMode = FilterMode.All;

        // Object pool
        private readonly List<GameObject> _activeEntries = new();
        private readonly Queue<GameObject> _entryPool = new();

        private enum FilterMode
        {
            All,
            InProgress,
            Completed,
            Hidden
        }

        private void OnEnable()
        {
            AchievementUIRegistry.Register(this);
            if (_panelRoot != null) _panelRoot.SetActive(false);

            if (_filterDropdown != null)
            {
                _filterDropdown.ClearOptions();
                _filterDropdown.AddOptions(new List<string> { "All", "In Progress", "Completed", "Hidden" });
                _filterDropdown.onValueChanged.AddListener(OnFilterChanged);
            }

            // Wire up category buttons
            if (_categoryButtons != null)
            {
                for (int i = 0; i < _categoryButtons.Length; i++)
                {
                    int categoryIndex = i;
                    _categoryButtons[i].onClick.AddListener(() => OnCategorySelected(categoryIndex));
                }
            }
        }

        private void OnDisable()
        {
            AchievementUIRegistry.Unregister(this);
        }

        /// <summary>Toggle panel visibility.</summary>
        public void TogglePanel()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(!_panelRoot.activeSelf);
            if (_panelRoot.activeSelf)
                RefreshDisplay();
        }

        private void OnCategorySelected(int index)
        {
            if (index < 0 || index > 6)
                _selectedCategory = null; // "All" tab
            else
                _selectedCategory = (AchievementCategory)index;
            RefreshDisplay();
        }

        private void OnFilterChanged(int value)
        {
            _filterMode = (FilterMode)value;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            // Return active entries to pool
            for (int i = 0; i < _activeEntries.Count; i++)
            {
                var go = _activeEntries[i];
                if (go != null)
                {
                    go.SetActive(false);
                    _entryPool.Enqueue(go);
                }
            }
            _activeEntries.Clear();

            if (_latestData.Entries == null) return;

            // Update header
            if (_completionText != null)
            {
                _completionText.text = $"{_latestData.TotalUnlocked} / {_latestData.TotalAchievements} Achievements ({_latestData.CompletionPercent:F0}%)";
            }

            // Filter and display
            for (int i = 0; i < _latestData.Entries.Length; i++)
            {
                var entry = _latestData.Entries[i];

                // Category filter
                if (_selectedCategory.HasValue && entry.Category != _selectedCategory.Value)
                    continue;

                // Filter mode
                switch (_filterMode)
                {
                    case FilterMode.InProgress when entry.IsComplete || entry.CurrentValue == 0:
                        continue;
                    case FilterMode.Completed when !entry.IsComplete:
                        continue;
                    case FilterMode.Hidden when !entry.IsHidden:
                        continue;
                }

                SetupEntryUI(GetPooledEntry(), entry);
            }
        }

        private GameObject GetPooledEntry()
        {
            if (_entryPrefab == null || _listContent == null) return null;

            GameObject go;
            if (_entryPool.Count > 0)
            {
                go = _entryPool.Dequeue();
                go.SetActive(true);
                go.transform.SetAsLastSibling();
            }
            else
            {
                go = Instantiate(_entryPrefab, _listContent);
            }

            _activeEntries.Add(go);
            return go;
        }

        private static void SetupEntryUI(GameObject go, AchievementEntryUI entry)
        {
            if (go == null) return;

            // Name
            var nameText = go.transform.Find("NameText")?.GetComponent<TMP_Text>();
            if (nameText != null) nameText.text = entry.Name;

            // Description
            var descText = go.transform.Find("DescriptionText")?.GetComponent<TMP_Text>();
            if (descText != null) descText.text = entry.Description;

            // Progress
            var progressText = go.transform.Find("ProgressText")?.GetComponent<TMP_Text>();
            if (progressText != null)
            {
                if (entry.IsComplete)
                    progressText.text = "Complete!";
                else if (entry.NextThreshold > 0)
                    progressText.text = $"{entry.CurrentValue} / {entry.NextThreshold}";
                else
                    progressText.text = "";
            }

            // Progress bar
            var progressBar = go.transform.Find("ProgressBar")?.GetComponent<Slider>();
            if (progressBar != null)
            {
                progressBar.minValue = 0f;
                progressBar.maxValue = 1f;
                progressBar.value = entry.ProgressPercent;
            }

            // Icon
            var iconImage = go.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null && entry.Icon != null)
                iconImage.sprite = entry.Icon;

            // Tier badge
            var tierText = go.transform.Find("TierText")?.GetComponent<TMP_Text>();
            if (tierText != null)
                tierText.text = entry.HighestTier != AchievementTier.None ? entry.HighestTier.ToString() : "";
        }

        // --- IAchievementUIProvider ---

        public void ShowToast(AchievementToastData data)
        {
            // Panel view doesn't handle toasts -- separate toast view
        }

        public void UpdatePanel(AchievementPanelData data)
        {
            _latestData = data;
            if (_panelRoot != null && _panelRoot.activeSelf)
                RefreshDisplay();
        }

        public void UpdateProgress(ushort achievementId, int currentValue, int nextThreshold)
        {
            // Could update individual entry in-place for efficiency
            // For now, full panel refresh handles this
        }

        public void HideToast()
        {
            // Not handled by panel view
        }
    }
}
