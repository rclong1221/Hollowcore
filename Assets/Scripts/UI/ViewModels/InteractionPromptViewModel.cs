using DIG.UI.Core.MVVM;
using DIG.UI.Core.Input;

namespace DIG.UI.ViewModels
{
    /// <summary>
    /// ViewModel for interaction prompts (e.g., "Press [F] to Open Door").
    /// 
    /// EPIC 15.8: Input Glyph System Integration
    /// </summary>
    public class InteractionPromptViewModel : ViewModelBase
    {
        private readonly BindableProperty<bool> _isVisible = new(false);
        private readonly BindableProperty<string> _promptText = new("");
        private readonly BindableProperty<string> _actionName = new("");
        private readonly BindableProperty<string> _objectName = new("");
        
        /// <summary>Whether the prompt is visible.</summary>
        public BindableProperty<bool> IsVisible => _isVisible;
        
        /// <summary>The raw prompt text with action tags (e.g., "Press <Action:Interact> to").</summary>
        public BindableProperty<string> PromptText => _promptText;
        
        /// <summary>The action name for the prompt (e.g., "Interact").</summary>
        public BindableProperty<string> ActionName => _actionName;
        
        /// <summary>The name of the object being interacted with.</summary>
        public BindableProperty<string> ObjectName => _objectName;
        
        /// <summary>
        /// Gets the fully formatted prompt text with resolved glyphs.
        /// Example: "Press [F] to Open Door"
        /// </summary>
        public string FormattedText
        {
            get
            {
                var processed = InputGlyphProvider.ProcessText(_promptText.Value);
                return $"{processed} {_objectName.Value}";
            }
        }
        
        /// <summary>
        /// Shows the interaction prompt.
        /// </summary>
        public void Show(string actionName, string objectName, string promptTemplate = "Press <Action:{0}> to")
        {
            _actionName.Value = actionName;
            _objectName.Value = objectName;
            _promptText.Value = string.Format(promptTemplate, actionName);
            _isVisible.Value = true;
        }
        
        /// <summary>
        /// Hides the interaction prompt.
        /// </summary>
        public void Hide()
        {
            _isVisible.Value = false;
        }
    }
}
