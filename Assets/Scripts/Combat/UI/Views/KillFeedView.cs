// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · KillFeedView
// UI View for displaying kill feed entries
// ════════════════════════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace DIG.Combat.UI.Views
{
    /// <summary>
    /// EPIC 15.9: View component for kill feed display.
    /// Shows recent kills with weapon icons, victim names, and kill type indicators.
    /// </summary>
    public class KillFeedView : DIG.UI.Core.MVVM.UIView<ViewModels.KillFeedViewModel>
    {
        // ─────────────────────────────────────────────────────────────────
        // UXML References
        // ─────────────────────────────────────────────────────────────────
        private VisualElement _container;
        private List<VisualElement> _entryElements = new();
        
        private const int MaxDisplayedEntries = 5;
        private const string EntryClass = "kill-feed-entry";
        private const string HeadshotClass = "kill-feed-headshot";
        private const string ExplosionClass = "kill-feed-explosion";
        private const string MeleeClass = "kill-feed-melee";
        
        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────
        protected override void OnEnable()
        {
            base.OnEnable();
            
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null) return;
            
            _container = root.Q<VisualElement>("kill-feed-container");
            if (_container == null)
            {
                _container = new VisualElement { name = "kill-feed-container" };
                _container.AddToClassList("kill-feed-container");
                root.Add(_container);
            }
            
            // Pre-create entry elements
            for (int i = 0; i < MaxDisplayedEntries; i++)
            {
                var entry = CreateEntryElement();
                entry.style.display = DisplayStyle.None;
                _container.Add(entry);
                _entryElements.Add(entry);
            }
        }
        
        protected override void OnBind()
        {
            if (ViewModel == null) return;
            ViewModel.OnEntryAdded += HandleKillAdded;
            ViewModel.OnEntriesChanged += RefreshDisplay;
        }
        
        protected override void OnUnbind()
        {
            if (ViewModel == null) return;
            ViewModel.OnEntryAdded -= HandleKillAdded;
            ViewModel.OnEntriesChanged -= RefreshDisplay;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Entry Creation
        // ─────────────────────────────────────────────────────────────────
        private VisualElement CreateEntryElement()
        {
            var entry = new VisualElement();
            entry.AddToClassList(EntryClass);
            
            var killerLabel = new Label { name = "killer" };
            killerLabel.AddToClassList("kill-feed-killer");
            entry.Add(killerLabel);
            
            var weaponIcon = new VisualElement { name = "weapon-icon" };
            weaponIcon.AddToClassList("kill-feed-weapon");
            entry.Add(weaponIcon);
            
            var victimLabel = new Label { name = "victim" };
            victimLabel.AddToClassList("kill-feed-victim");
            entry.Add(victimLabel);
            
            var typeIcon = new VisualElement { name = "type-icon" };
            typeIcon.AddToClassList("kill-feed-type");
            entry.Add(typeIcon);
            
            return entry;
        }
        
        // ─────────────────────────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────────────────────────
        private void HandleKillAdded(KillFeedEntry kill)
        {
            // Animate new entry in
            RefreshDisplay();
            
            if (_entryElements.Count > 0)
            {
                var newest = _entryElements[0];
                newest.AddToClassList("kill-feed-entry-new");
                
                // Remove animation class after delay
                newest.schedule.Execute(() => newest.RemoveFromClassList("kill-feed-entry-new")).StartingIn(300);
            }
        }
        
        private void RefreshDisplay()
        {
            if (ViewModel == null) return;
            
            var entries = ViewModel.Entries;
            
            for (int i = 0; i < _entryElements.Count; i++)
            {
                var element = _entryElements[i];
                
                if (i < entries.Count)
                {
                    var entry = entries[i];
                    PopulateEntry(element, entry);
                    element.style.display = DisplayStyle.Flex;
                }
                else
                {
                    element.style.display = DisplayStyle.None;
                }
            }
        }
        
        private void PopulateEntry(VisualElement element, KillFeedEntry entry)
        {
            var killerLabel = element.Q<Label>("killer");
            var victimLabel = element.Q<Label>("victim");
            var weaponIcon = element.Q<VisualElement>("weapon-icon");
            var typeIcon = element.Q<VisualElement>("type-icon");
            
            if (killerLabel != null) killerLabel.text = entry.KillerName;
            if (victimLabel != null) victimLabel.text = entry.VictimName;
            
            // Set weapon icon based on weapon name
            if (weaponIcon != null)
            {
                weaponIcon.ClearClassList();
                weaponIcon.AddToClassList("kill-feed-weapon");
                weaponIcon.AddToClassList($"weapon-{entry.WeaponName?.ToLowerInvariant()?.Replace(" ", "-") ?? "default"}");
            }
            
            // Set kill type styling
            element.RemoveFromClassList(HeadshotClass);
            element.RemoveFromClassList(ExplosionClass);
            element.RemoveFromClassList(MeleeClass);
            
            switch (entry.Type)
            {
                case KillType.Headshot:
                    element.AddToClassList(HeadshotClass);
                    if (typeIcon != null) typeIcon.AddToClassList("icon-headshot");
                    break;
                    
                case KillType.Explosive:
                    element.AddToClassList(ExplosionClass);
                    if (typeIcon != null) typeIcon.AddToClassList("icon-explosion");
                    break;
                    
                case KillType.Melee:
                    element.AddToClassList(MeleeClass);
                    if (typeIcon != null) typeIcon.AddToClassList("icon-melee");
                    break;
                    
                default:
                    if (typeIcon != null) typeIcon.ClearClassList();
                    break;
            }
        }
    }
}
