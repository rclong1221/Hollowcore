// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · CombatLogView
// UI View for displaying scrollable combat log
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using DIG.Combat.UI; // For CombatLogEntry

namespace DIG.Combat.UI.Views
{
    /// <summary>
    /// EPIC 15.9: View component for combat log display.
    /// Scrollable log of combat events with filtering capabilities.
    /// </summary>
    public class CombatLogView : DIG.UI.Core.MVVM.UIView<ViewModels.CombatLogViewModel>
    {
        // ─────────────────────────────────────────────────────────────────
        // Configuration
        // ─────────────────────────────────────────────────────────────────
        [SerializeField] private int _maxVisibleEntries = 50;
        [SerializeField] private bool _showTimestamps = true;
        [SerializeField] private bool _autoScroll = true;
        
        // ─────────────────────────────────────────────────────────────────
        // UXML References
        // ─────────────────────────────────────────────────────────────────
        private VisualElement _container;
        private ScrollView _scrollView;
        private VisualElement _filterBar;
        private Dictionary<CombatLogCategory, Toggle> _filterToggles = new();
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        protected override void OnEnable()
        {
            base.OnEnable();
            
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;
            
            _container = root.Q<VisualElement>("combat-log-container");
            if (_container == null)
            {
                _container = CreateDefaultLayout();
                root.Add(_container);
            }
            else
            {
                _scrollView = _container.Q<ScrollView>("combat-log-scroll");
                _filterBar = _container.Q<VisualElement>("combat-log-filters");
            }
            
            SetupFilters();
        }
        
        private VisualElement CreateDefaultLayout()
        {
            var container = new VisualElement { name = "combat-log-container" };
            container.AddToClassList("combat-log-container");
            
            // Header with title and collapse button
            var header = new VisualElement();
            header.AddToClassList("combat-log-header");
            
            var title = new Label { text = "Combat Log" };
            title.AddToClassList("combat-log-title");
            header.Add(title);
            
            var collapseBtn = new Button { text = "▼" };
            collapseBtn.AddToClassList("combat-log-collapse");
            collapseBtn.clicked += ToggleCollapse;
            header.Add(collapseBtn);
            
            container.Add(header);
            
            // Filter bar
            _filterBar = new VisualElement { name = "combat-log-filters" };
            _filterBar.AddToClassList("combat-log-filters");
            container.Add(_filterBar);
            
            // Scroll view for entries
            _scrollView = new ScrollView { name = "combat-log-scroll" };
            _scrollView.AddToClassList("combat-log-scroll");
            _scrollView.mode = ScrollViewMode.Vertical;
            container.Add(_scrollView);
            
            return container;
        }
        
        private void SetupFilters()
        {
            if (_filterBar == null) return;
            _filterBar.Clear();
            
            // Create toggle for each category
            foreach (CombatLogCategory category in System.Enum.GetValues(typeof(CombatLogCategory)))
            {
                var toggle = new Toggle(category.ToString());
                toggle.value = true;
                toggle.AddToClassList($"filter-{category.ToString().ToLowerInvariant()}");
                toggle.RegisterValueChangedCallback(evt => OnFilterChanged(category, evt.newValue));
                _filterBar.Add(toggle);
                _filterToggles[category] = toggle;
            }
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Binding
        // ─────────────────────────────────────────────────────────────────
        protected override void OnBind()
        {
            if (ViewModel == null) return;
            
            ViewModel.OnEntryAdded += HandleEntryAdded;
            ViewModel.OnLogCleared += RefreshDisplay;
            
            RefreshDisplay();
        }
        
        protected override void OnUnbind()
        {
            if (ViewModel == null) return;
            
            ViewModel.OnEntryAdded -= HandleEntryAdded;
            ViewModel.OnLogCleared -= RefreshDisplay;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────────────────────────
        private void HandleEntryAdded(DIG.Combat.UI.CombatLogEntry entry)
        {
            if (_scrollView == null) return;
            
            var element = CreateEntryElement(entry);
            _scrollView.Add(element);
            
            // Trim old entries
            while (_scrollView.childCount > _maxVisibleEntries)
            {
                _scrollView.RemoveAt(0);
            }
            
            // Auto-scroll to bottom
            if (_autoScroll)
            {
                _scrollView.schedule.Execute(() => _scrollView.scrollOffset = new Vector2(0, _scrollView.contentContainer.layout.height));
            }
        }
        
        private VisualElement CreateEntryElement(DIG.Combat.UI.CombatLogEntry entry)
        {
            var element = new VisualElement();
            element.AddToClassList("combat-log-entry");
            element.AddToClassList($"log-{entry.HitType.ToString().ToLowerInvariant()}");
            
            // Timestamp
            if (_showTimestamps)
            {
                var timestamp = new Label { text = FormatTimestamp(entry.Timestamp) };
                timestamp.AddToClassList("log-timestamp");
                element.Add(timestamp);
            }
            
            // Message
            string message = FormatLogMessage(entry);
            var messageLabel = new Label { text = message };
            messageLabel.AddToClassList("log-message");
            element.Add(messageLabel);
            
            // Damage value
            if (entry.Damage > 0)
            {
                var value = new Label { text = entry.Damage.ToString("F0") };
                value.AddToClassList("log-value");
                value.AddToClassList(entry.TargetKilled ? "log-value-kill" : "log-value-damage");
                element.Add(value);
            }
            
            return element;
        }
        
        private string FormatTimestamp(float gameTime)
        {
            int minutes = Mathf.FloorToInt(gameTime / 60f);
            int seconds = Mathf.FloorToInt(gameTime % 60f);
            return $"{minutes:D2}:{seconds:D2}";
        }
        
        private string FormatLogMessage(DIG.Combat.UI.CombatLogEntry entry)
        {
            if (entry.TargetKilled)
                return $"{entry.AttackerName} killed {entry.TargetName}";
            return $"{entry.AttackerName} → {entry.TargetName}";
        }
        
        private void RefreshDisplay()
        {
            if (_scrollView == null || ViewModel == null) return;
            
            _scrollView.Clear();
            
            foreach (var entry in ViewModel.Entries)
            {
                var element = CreateEntryElement(entry);
                _scrollView.Add(element);
            }
        }
        
        private void OnFilterChanged(CombatLogCategory category, bool enabled)
        {
            // Filtering not supported with current data model
            RefreshDisplay();
        }
        
        private void ToggleCollapse()
        {
            if (_scrollView == null) return;
            
            bool isCollapsed = _scrollView.style.display == DisplayStyle.None;
            _scrollView.style.display = isCollapsed ? DisplayStyle.Flex : DisplayStyle.None;
            _filterBar.style.display = isCollapsed ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Update button text
            var collapseBtn = _container?.Q<Button>("combat-log-collapse");
            if (collapseBtn != null)
            {
                collapseBtn.text = isCollapsed ? "▼" : "▲";
            }
        }
    }
}
