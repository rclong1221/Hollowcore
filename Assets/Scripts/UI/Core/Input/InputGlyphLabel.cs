using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.Input
{
    /// <summary>
    /// UI Toolkit Label that automatically processes action tags and updates on device change.
    /// 
    /// EPIC 15.8: Input Glyph System
    /// 
    /// Usage in UXML:
    ///   This is a code-only component. Use via C#:
    ///   
    ///   var label = new InputGlyphLabel("Press <Action:Interact> to open");
    ///   container.Add(label);
    ///   
    /// Or wrap existing labels:
    ///   label.text = InputGlyphProvider.ProcessText("Press <Action:Jump>");
    /// </summary>
    public class InputGlyphLabel : Label
    {
        private string _rawText;
        
        /// <summary>
        /// Creates an InputGlyphLabel with the given text.
        /// Action tags will be automatically processed.
        /// </summary>
        public InputGlyphLabel(string rawText = "") : base()
        {
            RawText = rawText;
            InputGlyphProvider.OnDeviceChanged += OnDeviceChanged;
        }
        
        /// <summary>
        /// The raw text with action tags (e.g., "Press &lt;Action:Jump&gt;").
        /// Setting this will automatically process and display the formatted text.
        /// </summary>
        public string RawText
        {
            get => _rawText;
            set
            {
                _rawText = value;
                RefreshText();
            }
        }
        
        /// <summary>
        /// Refreshes the displayed text based on current device.
        /// </summary>
        public void RefreshText()
        {
            text = InputGlyphProvider.ProcessText(_rawText);
        }
        
        private void OnDeviceChanged(InputDeviceType newDevice)
        {
            RefreshText();
        }
        
        /// <summary>
        /// Call this when the label is being destroyed.
        /// </summary>
        public void Dispose()
        {
            InputGlyphProvider.OnDeviceChanged -= OnDeviceChanged;
        }
    }
    
    /// <summary>
    /// MonoBehaviour helper for initializing InputGlyphProvider with a database.
    /// Add to a GameObject in your startup scene.
    /// 
    /// EPIC 15.8: Input Glyph System
    /// </summary>
    [AddComponentMenu("DIG/UI/Input Glyph Initializer")]
    public class InputGlyphInitializer : MonoBehaviour
    {
        [SerializeField] 
        [Tooltip("The glyph database to use. If null, attempts to load from Resources/InputGlyphDatabase")]
        private InputGlyphDatabase _database;
        
        private void Awake()
        {
            if (_database != null)
            {
                InputGlyphProvider.Initialize(_database);
                Debug.Log("[InputGlyph] Initialized with database: " + _database.name);
            }
            else
            {
                // Provider will auto-load from Resources if available
                Debug.Log("[InputGlyph] No database assigned, will attempt Resources load");
            }
        }
    }
}
