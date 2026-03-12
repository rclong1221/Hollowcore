using UnityEngine;
using UnityEngine.UIElements;
using DIG.UI.Core.MVVM;
using DIG.UI.Core.Input;
using DIG.UI.ViewModels;

namespace DIG.UI.Views
{
    /// <summary>
    /// View for displaying interaction prompts.
    /// Automatically updates when input device changes.
    /// 
    /// EPIC 15.8: Input Glyph System Integration
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InteractionPromptView : UIView<InteractionPromptViewModel>
    {
        [Header("Auto Create")]
        [SerializeField] private bool _autoCreateViewModel = true;
        
        private VisualElement _container;
        private Label _promptLabel;
        private VisualElement _iconContainer;
        private Image _glyphImage;
        
        protected override void Start()
        {
            base.Start();
            
            if (_autoCreateViewModel && !IsBound)
            {
                Bind(new InteractionPromptViewModel());
            }
        }
        
        protected override void OnBind()
        {
            _container = Root.Q<VisualElement>("prompt-container");
            _promptLabel = Root.Q<Label>("prompt-text");
            _iconContainer = Root.Q<VisualElement>("glyph-container");
            _glyphImage = Root.Q<Image>("glyph-icon");
            
            // Bind visibility
            BindVisibility("prompt-container", ViewModel.IsVisible);
            
            // Bind prompt text (updates on any change)
            ViewModel.PromptText.OnChanged += _ => RefreshPrompt();
            ViewModel.ObjectName.OnChanged += _ => RefreshPrompt();
            ViewModel.ActionName.OnChanged += _ => RefreshPrompt();
            
            // Listen for device changes to update glyphs
            InputGlyphProvider.OnDeviceChanged += OnDeviceChanged;
            
            // Initial refresh
            RefreshPrompt();
        }
        
        protected override void OnUnbind()
        {
            InputGlyphProvider.OnDeviceChanged -= OnDeviceChanged;
        }
        
        private void OnDeviceChanged(InputDeviceType newDevice)
        {
            RefreshPrompt();
        }
        
        private void RefreshPrompt()
        {
            if (_promptLabel != null)
            {
                _promptLabel.text = ViewModel.FormattedText;
            }
            
            if (_glyphImage != null && !string.IsNullOrEmpty(ViewModel.ActionName.Value))
            {
                var icon = InputGlyphProvider.GetIcon(ViewModel.ActionName.Value);
                if (icon != null)
                {
                    _glyphImage.sprite = icon;
                    _glyphImage.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _glyphImage.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
