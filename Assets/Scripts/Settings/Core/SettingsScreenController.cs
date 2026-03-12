using System;
using System.Collections.Generic;
using DIG.Settings.Pages;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Settings.Core
{
    /// <summary>
    /// EPIC 18.2: Manages the Settings screen UI lifecycle.
    /// Created by SettingsService when the Settings screen opens.
    /// Owns tab switching, footer buttons, dirty tracking, and confirmation dialogs.
    /// </summary>
    internal class SettingsScreenController : IDisposable
    {
        private readonly VisualElement _root;
        private readonly VisualElement _tabBar;
        private readonly ScrollView _pageContent;
        private readonly Button _applyBtn;
        private readonly Button _revertBtn;
        private readonly Button _defaultsBtn;
        private readonly Button _closeBtn;

        private readonly List<Button> _tabButtons = new();
        private ISettingsPage _activePage;
        private int _activeTabIndex = -1;
        private IVisualElementScheduledItem _dirtyPoll;

        public SettingsScreenController(VisualElement root)
        {
            _root = root;

            // Query UXML elements
            _tabBar = root.Q<VisualElement>("tab-bar");
            _pageContent = root.Q<ScrollView>("page-content");
            _applyBtn = root.Q<Button>("apply-btn");
            _revertBtn = root.Q<Button>("revert-btn");
            _defaultsBtn = root.Q<Button>("defaults-btn");
            _closeBtn = root.Q<Button>("close-btn");

            // Register all pages
            RegisterPages();

            // Build tab buttons
            BuildTabs();

            // Take snapshots
            foreach (var page in SettingsService.Pages)
                page.TakeSnapshot();

            // Wire footer buttons
            _applyBtn?.RegisterCallback<ClickEvent>(OnApplyClicked);
            _revertBtn?.RegisterCallback<ClickEvent>(OnRevertClicked);
            _defaultsBtn?.RegisterCallback<ClickEvent>(OnDefaultsClicked);
            _closeBtn?.RegisterCallback<ClickEvent>(OnCloseClicked);

            // Initial state: Apply/Revert disabled
            SetFooterEnabled(false);

            // Show first tab
            if (SettingsService.Pages.Count > 0)
                SwitchTab(0);

            // Start dirty polling (lightweight, 200ms interval)
            _dirtyPoll = _root.schedule.Execute(PollDirtyState).Every(200);
        }

        public void Dispose()
        {
            _dirtyPoll?.Pause();
            _dirtyPoll = null;

            _applyBtn?.UnregisterCallback<ClickEvent>(OnApplyClicked);
            _revertBtn?.UnregisterCallback<ClickEvent>(OnRevertClicked);
            _defaultsBtn?.UnregisterCallback<ClickEvent>(OnDefaultsClicked);
            _closeBtn?.UnregisterCallback<ClickEvent>(OnCloseClicked);

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                int index = i;
                // Can't easily unregister lambdas, but disposal cleans up the entire tree anyway
            }
        }

        private void RegisterPages()
        {
            SettingsService.ClearPages();
            SettingsService.RegisterPage(new GraphicsSettingsPage());
            SettingsService.RegisterPage(new AudioSettingsPage());
            SettingsService.RegisterPage(new ControlsSettingsPage());
            SettingsService.RegisterPage(new GameplaySettingsPage());
            SettingsService.RegisterPage(new AccessibilitySettingsPage());
        }

        private void BuildTabs()
        {
            if (_tabBar == null) return;
            _tabBar.Clear();
            _tabButtons.Clear();

            for (int i = 0; i < SettingsService.Pages.Count; i++)
            {
                var page = SettingsService.Pages[i];
                var btn = new Button { text = page.DisplayName };
                btn.AddToClassList("settings-tab");

                int tabIndex = i;
                btn.RegisterCallback<ClickEvent>(_ => SwitchTab(tabIndex));

                _tabBar.Add(btn);
                _tabButtons.Add(btn);
            }
        }

        private void SwitchTab(int index)
        {
            if (index < 0 || index >= SettingsService.Pages.Count) return;
            if (index == _activeTabIndex) return;

            // Deactivate previous tab
            if (_activeTabIndex >= 0 && _activeTabIndex < _tabButtons.Count)
                _tabButtons[_activeTabIndex].RemoveFromClassList("settings-tab--active");

            _activeTabIndex = index;
            _activePage = SettingsService.Pages[index];

            // Activate new tab
            _tabButtons[index].AddToClassList("settings-tab--active");

            // Rebuild page content
            if (_pageContent != null)
            {
                _pageContent.Clear();
                _activePage.BuildUI(_pageContent.contentContainer);
                _activePage.OnPageShown();
                _pageContent.scrollOffset = Vector2.zero;
            }
        }

        private void OnApplyClicked(ClickEvent _)
        {
            foreach (var page in SettingsService.Pages)
                page.ApplyChanges();

            // Re-snapshot after apply
            foreach (var page in SettingsService.Pages)
                page.TakeSnapshot();

            SetFooterEnabled(false);
        }

        private void OnRevertClicked(ClickEvent _)
        {
            foreach (var page in SettingsService.Pages)
                page.RevertChanges();

            // Rebuild current tab to reflect reverted values
            if (_activePage != null && _pageContent != null)
            {
                _pageContent.Clear();
                _activePage.BuildUI(_pageContent.contentContainer);
                _activePage.OnPageShown();
            }

            SetFooterEnabled(false);
        }

        private void OnDefaultsClicked(ClickEvent _)
        {
            ShowConfirmDialog(
                "Reset to Defaults",
                "Reset all settings to their default values?",
                "Reset",
                () =>
                {
                    foreach (var page in SettingsService.Pages)
                        page.ResetToDefaults();

                    // Re-snapshot after reset
                    foreach (var page in SettingsService.Pages)
                        page.TakeSnapshot();

                    // Rebuild current tab
                    if (_activePage != null && _pageContent != null)
                    {
                        _pageContent.Clear();
                        _activePage.BuildUI(_pageContent.contentContainer);
                        _activePage.OnPageShown();
                    }

                    SetFooterEnabled(false);
                });
        }

        private void OnCloseClicked(ClickEvent _)
        {
            bool anyDirty = false;
            foreach (var page in SettingsService.Pages)
            {
                if (page.HasUnsavedChanges)
                {
                    anyDirty = true;
                    break;
                }
            }

            if (anyDirty)
            {
                ShowUnsavedChangesDialog();
            }
            else
            {
                SettingsService.Close();
            }
        }

        private void PollDirtyState()
        {
            bool anyDirty = false;
            foreach (var page in SettingsService.Pages)
            {
                if (page.HasUnsavedChanges)
                {
                    anyDirty = true;
                    break;
                }
            }
            SetFooterEnabled(anyDirty);
        }

        private void SetFooterEnabled(bool hasChanges)
        {
            _applyBtn?.SetEnabled(hasChanges);
            _revertBtn?.SetEnabled(hasChanges);
        }

        // === Confirmation Dialogs ===

        private void ShowConfirmDialog(string title, string message, string confirmText, Action onConfirm)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("settings-confirm-overlay");

            var panel = new VisualElement();
            panel.AddToClassList("settings-confirm-panel");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("text-subheader");
            titleLabel.style.marginBottom = 8;
            panel.Add(titleLabel);

            var messageLabel = new Label(message);
            messageLabel.AddToClassList("settings-confirm-text");
            panel.Add(messageLabel);

            var buttons = new VisualElement();
            buttons.AddToClassList("settings-confirm-buttons");

            var cancelBtn = new Button(() =>
            {
                _root.Remove(overlay);
            }) { text = "Cancel" };
            cancelBtn.AddToClassList("pro-button");
            cancelBtn.AddToClassList("pro-button--ghost");
            cancelBtn.AddToClassList("pro-button--sm");
            buttons.Add(cancelBtn);

            var confirmBtn = new Button(() =>
            {
                _root.Remove(overlay);
                onConfirm?.Invoke();
            }) { text = confirmText };
            confirmBtn.AddToClassList("pro-button");
            confirmBtn.AddToClassList("pro-button--danger");
            confirmBtn.AddToClassList("pro-button--sm");
            buttons.Add(confirmBtn);

            panel.Add(buttons);
            overlay.Add(panel);
            _root.Add(overlay);
        }

        private void ShowUnsavedChangesDialog()
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("settings-unsaved-overlay");

            var panel = new VisualElement();
            panel.AddToClassList("settings-confirm-panel");

            var title = new Label("Unsaved Changes");
            title.AddToClassList("text-subheader");
            title.style.marginBottom = 8;
            panel.Add(title);

            var msg = new Label("You have unsaved changes. What would you like to do?");
            msg.AddToClassList("settings-confirm-text");
            panel.Add(msg);

            var buttons = new VisualElement();
            buttons.AddToClassList("settings-confirm-buttons");

            var cancelBtn = new Button(() =>
            {
                _root.Remove(overlay);
            }) { text = "Cancel" };
            cancelBtn.AddToClassList("pro-button");
            cancelBtn.AddToClassList("pro-button--ghost");
            cancelBtn.AddToClassList("pro-button--sm");
            buttons.Add(cancelBtn);

            var discardBtn = new Button(() =>
            {
                _root.Remove(overlay);
                foreach (var page in SettingsService.Pages)
                    page.RevertChanges();
                SettingsService.Close();
            }) { text = "Discard" };
            discardBtn.AddToClassList("pro-button");
            discardBtn.AddToClassList("pro-button--danger");
            discardBtn.AddToClassList("pro-button--sm");
            buttons.Add(discardBtn);

            var applyBtn = new Button(() =>
            {
                _root.Remove(overlay);
                foreach (var page in SettingsService.Pages)
                    page.ApplyChanges();
                SettingsService.Close();
            }) { text = "Apply & Close" };
            applyBtn.AddToClassList("pro-button");
            applyBtn.AddToClassList("pro-button--sm");
            buttons.Add(applyBtn);

            panel.Add(buttons);
            overlay.Add(panel);
            _root.Add(overlay);
        }

        // === Helpers for pages to create consistent UI rows ===

        /// <summary>Creates a standard settings row with label and control container.</summary>
        public static VisualElement CreateSettingsRow(string label, VisualElement control)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");

            var lbl = new Label(label);
            lbl.AddToClassList("settings-row-label");
            row.Add(lbl);

            var controlContainer = new VisualElement();
            controlContainer.AddToClassList("settings-row-control");
            controlContainer.Add(control);
            row.Add(controlContainer);

            return row;
        }

        /// <summary>Creates a slider row with a live value label.</summary>
        public static VisualElement CreateSliderRow(string label, float min, float max, float value,
            Action<float> onChanged, string format = "F0")
        {
            var slider = new Slider(min, max) { value = value };
            var valueLabel = new Label(value.ToString(format));
            valueLabel.AddToClassList("settings-slider-value");

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString(format);
                onChanged?.Invoke(evt.newValue);
            });

            var sliderRow = new VisualElement();
            sliderRow.AddToClassList("settings-slider-row");
            sliderRow.Add(slider);
            sliderRow.Add(valueLabel);

            return CreateSettingsRow(label, sliderRow);
        }

        /// <summary>Creates a toggle row.</summary>
        public static VisualElement CreateToggleRow(string label, bool value, Action<bool> onChanged)
        {
            var toggle = new Toggle { value = value };
            toggle.AddToClassList("settings-toggle");
            toggle.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));

            return CreateSettingsRow(label, toggle);
        }

        /// <summary>Creates a dropdown row.</summary>
        public static VisualElement CreateDropdownRow(string label, List<string> choices, int selectedIndex,
            Action<int> onChanged)
        {
            var dropdown = new DropdownField(choices, selectedIndex);
            dropdown.AddToClassList("settings-dropdown");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = choices.IndexOf(evt.newValue);
                onChanged?.Invoke(idx);
            });

            return CreateSettingsRow(label, dropdown);
        }

        /// <summary>Creates a section header label.</summary>
        public static Label CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.AddToClassList("settings-section-header");
            return header;
        }
    }
}
