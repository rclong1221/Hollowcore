using System;

namespace DIG.UI.Core.MVVM
{
    /// <summary>
    /// A reactive property wrapper that notifies subscribers when the value changes.
    /// Used in MVVM pattern to enable data binding between ViewModels and Views.
    /// 
    /// EPIC 15.8: Core MVVM Framework
    /// 
    /// Usage:
    ///   public BindableProperty&lt;float&gt; Health { get; } = new(100f);
    ///   Health.OnValueChanged += (old, newVal) => UpdateUI(newVal);
    ///   Health.Value = 50f; // Triggers callback
    /// </summary>
    public class BindableProperty<T>
    {
        private T _value;
        
        /// <summary>
        /// Event fired when Value changes. Provides old and new values.
        /// </summary>
        public event Action<T, T> OnValueChanged;
        
        /// <summary>
        /// Event fired when Value changes. Only provides new value.
        /// </summary>
        public event Action<T> OnChanged;
        
        /// <summary>
        /// The current value. Setting this will trigger change events if the value differs.
        /// </summary>
        public T Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value)) return;
                
                T oldValue = _value;
                _value = value;
                
                OnValueChanged?.Invoke(oldValue, _value);
                OnChanged?.Invoke(_value);
            }
        }
        
        /// <summary>
        /// Creates a new BindableProperty with default value.
        /// </summary>
        public BindableProperty() : this(default) { }
        
        /// <summary>
        /// Creates a new BindableProperty with initial value.
        /// </summary>
        public BindableProperty(T initialValue)
        {
            _value = initialValue;
        }
        
        /// <summary>
        /// Forces notification even if value hasn't changed.
        /// Useful for initial UI setup.
        /// </summary>
        public void NotifyChange()
        {
            OnValueChanged?.Invoke(_value, _value);
            OnChanged?.Invoke(_value);
        }
        
        /// <summary>
        /// Sets value without triggering change events.
        /// Use for initialization or batch updates.
        /// </summary>
        public void SetSilent(T value)
        {
            _value = value;
        }
        
        /// <summary>
        /// Removes all event subscribers.
        /// Call when the ViewModel is disposed.
        /// </summary>
        public void ClearListeners()
        {
            OnValueChanged = null;
            OnChanged = null;
        }
        
        /// <summary>
        /// Implicit conversion to T for convenience.
        /// </summary>
        public static implicit operator T(BindableProperty<T> property) => property.Value;
        
        public override string ToString() => _value?.ToString() ?? "null";
    }
    
    /// <summary>
    /// Extension methods for BindableProperty.
    /// </summary>
    public static class BindablePropertyExtensions
    {
        /// <summary>
        /// Binds this property to another, creating a one-way binding.
        /// When source changes, target is updated.
        /// </summary>
        public static void BindTo<T>(this BindableProperty<T> source, BindableProperty<T> target)
        {
            source.OnChanged += value => target.Value = value;
        }
        
        /// <summary>
        /// Creates a two-way binding between properties.
        /// Changes to either will update the other.
        /// </summary>
        public static void BindTwoWay<T>(this BindableProperty<T> first, BindableProperty<T> second)
        {
            bool updating = false;
            
            first.OnChanged += value =>
            {
                if (updating) return;
                updating = true;
                second.Value = value;
                updating = false;
            };
            
            second.OnChanged += value =>
            {
                if (updating) return;
                updating = true;
                first.Value = value;
                updating = false;
            };
        }
        
        /// <summary>
        /// Creates a derived property that transforms the source value.
        /// </summary>
        public static BindableProperty<TResult> Select<TSource, TResult>(
            this BindableProperty<TSource> source, 
            Func<TSource, TResult> selector)
        {
            var result = new BindableProperty<TResult>(selector(source.Value));
            source.OnChanged += value => result.Value = selector(value);
            return result;
        }
    }
}
