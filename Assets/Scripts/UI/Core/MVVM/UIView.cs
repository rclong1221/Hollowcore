using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.MVVM
{
    /// <summary>
    /// Base class for UI Toolkit Views in the MVVM pattern.
    /// Handles ViewModel binding, lifecycle, and cleanup.
    /// 
    /// EPIC 15.8: Core MVVM Framework
    /// 
    /// Usage:
    ///   1. Inherit from UIView&lt;YourViewModel&gt;
    ///   2. Override OnBind() to set up UI bindings
    ///   3. Override OnUnbind() to clean up bindings
    ///   4. Call Bind(viewModel) to connect View to ViewModel
    /// </summary>
    public abstract class UIView<TViewModel> : MonoBehaviour where TViewModel : ViewModelBase
    {
        [Header("UI Document")]
        [SerializeField] protected UIDocument _uiDocument;
        
        [Header("Debug")]
        [SerializeField] protected bool _debugLogging = false;
        
        /// <summary>
        /// The bound ViewModel instance.
        /// </summary>
        public TViewModel ViewModel { get; private set; }
        
        /// <summary>
        /// The root VisualElement from the UIDocument.
        /// </summary>
        protected VisualElement Root => _uiDocument?.rootVisualElement;
        
        /// <summary>
        /// Whether a ViewModel is currently bound.
        /// </summary>
        public bool IsBound => ViewModel != null;
        
        protected virtual void Awake()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }
        }
        
        protected virtual void Start()
        {
            // Subclasses can override to auto-create ViewModel
        }
        
        protected virtual void OnEnable()
        {
            if (IsBound)
            {
                ViewModel.OnActivate();
            }
        }
        
        protected virtual void OnDisable()
        {
            if (IsBound)
            {
                ViewModel.OnDeactivate();
            }
        }
        
        protected virtual void Update()
        {
            if (IsBound)
            {
                ViewModel.Tick(Time.deltaTime);
            }
        }
        
        protected virtual void OnDestroy()
        {
            Unbind();
        }
        
        /// <summary>
        /// Binds this View to a ViewModel.
        /// </summary>
        public void Bind(TViewModel viewModel)
        {
            if (viewModel == null)
            {
                Debug.LogError($"[UIView] Cannot bind null ViewModel to {GetType().Name}");
                return;
            }
            
            // Unbind existing ViewModel if any
            if (IsBound)
            {
                Unbind();
            }
            
            ViewModel = viewModel;
            
            if (_debugLogging)
            {
                Debug.Log($"[UIView] {GetType().Name} bound to {typeof(TViewModel).Name}");
            }
            
            OnBind();
            
            if (gameObject.activeInHierarchy)
            {
                ViewModel.OnActivate();
            }
        }
        
        /// <summary>
        /// Unbinds the current ViewModel.
        /// </summary>
        public void Unbind()
        {
            if (!IsBound) return;
            
            if (_debugLogging)
            {
                Debug.Log($"[UIView] {GetType().Name} unbinding from {typeof(TViewModel).Name}");
            }
            
            OnUnbind();
            
            ViewModel.OnDeactivate();
            ViewModel = null;
        }
        
        /// <summary>
        /// Override to set up bindings between ViewModel properties and UI elements.
        /// Called after Bind() is successful.
        /// </summary>
        protected abstract void OnBind();
        
        /// <summary>
        /// Override to clean up bindings.
        /// Called before ViewModel is set to null.
        /// </summary>
        protected virtual void OnUnbind() { }
        
        #region Query Helpers
        
        /// <summary>
        /// Queries for a VisualElement by name.
        /// </summary>
        protected VisualElement Q(string name) => Root?.Q(name);
        
        /// <summary>
        /// Queries for a typed VisualElement by name.
        /// </summary>
        protected T Q<T>(string name) where T : VisualElement => Root?.Q<T>(name);
        
        /// <summary>
        /// Queries for a VisualElement by class.
        /// </summary>
        protected VisualElement QClass(string className) => Root?.Q(className: className);
        
        /// <summary>
        /// Queries for all elements matching a class.
        /// </summary>
        protected UQueryBuilder<VisualElement> QAll(string className) => Root?.Query(className: className) ?? default;
        
        #endregion
        
        #region Binding Helpers
        
        /// <summary>
        /// Binds a Label's text to a BindableProperty.
        /// </summary>
        protected void BindLabel(string elementName, BindableProperty<string> property)
        {
            var label = Q<Label>(elementName);
            if (label == null)
            {
                LogMissingElement(elementName);
                return;
            }
            
            label.text = property.Value;
            property.OnChanged += value => label.text = value;
        }
        
        /// <summary>
        /// Binds a Label's text to a formatted BindableProperty.
        /// </summary>
        protected void BindLabel<T>(string elementName, BindableProperty<T> property, Func<T, string> formatter)
        {
            var label = Q<Label>(elementName);
            if (label == null)
            {
                LogMissingElement(elementName);
                return;
            }
            
            label.text = formatter(property.Value);
            property.OnChanged += value => label.text = formatter(value);
        }
        
        /// <summary>
        /// Binds a ProgressBar to a BindableProperty (0-1 range).
        /// </summary>
        protected void BindProgressBar(string elementName, BindableProperty<float> property)
        {
            var bar = Q<ProgressBar>(elementName);
            if (bar == null)
            {
                LogMissingElement(elementName);
                return;
            }
            
            bar.value = property.Value * 100f;
            property.OnChanged += value => bar.value = value * 100f;
        }
        
        /// <summary>
        /// Binds a Button click to an action.
        /// </summary>
        protected void BindButton(string elementName, Action onClick)
        {
            var button = Q<Button>(elementName);
            if (button == null)
            {
                LogMissingElement(elementName);
                return;
            }
            
            button.clicked += onClick;
        }
        
        /// <summary>
        /// Binds visibility to a bool property.
        /// </summary>
        protected void BindVisibility(string elementName, BindableProperty<bool> property)
        {
            var element = Q(elementName);
            if (element == null)
            {
                LogMissingElement(elementName);
                return;
            }
            
            element.style.display = property.Value ? DisplayStyle.Flex : DisplayStyle.None;
            property.OnChanged += value => element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        /// <summary>
        /// Binds enabled state to a bool property.
        /// </summary>
        protected void BindEnabled(string elementName, BindableProperty<bool> property)
        {
            var element = Q(elementName);
            if (element == null)
            {
                LogMissingElement(elementName);
                return;
            }
            
            element.SetEnabled(property.Value);
            property.OnChanged += value => element.SetEnabled(value);
        }
        
        private void LogMissingElement(string elementName)
        {
            if (_debugLogging)
            {
                Debug.LogWarning($"[UIView] {GetType().Name}: Element '{elementName}' not found");
            }
        }
        
        #endregion
    }
}
