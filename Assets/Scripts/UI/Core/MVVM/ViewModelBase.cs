using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DIG.UI.Core.MVVM
{
    /// <summary>
    /// Base class for all ViewModels in the MVVM architecture.
    /// Implements INotifyPropertyChanged for data binding support.
    /// 
    /// EPIC 15.8: Core MVVM Framework
    /// 
    /// Design Principles:
    /// - ViewModels expose data as BindableProperty&lt;T&gt; for reactive updates
    /// - ViewModels NEVER reference Views or MonoBehaviours directly
    /// - ViewModels can reference ECS World through a service locator
    /// - ViewModels handle validation and business logic
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Standard .NET property changed event for compatibility.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// Indicates whether this ViewModel has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }
        
        /// <summary>
        /// Raises PropertyChanged event.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// Sets a field value and raises PropertyChanged if changed.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        
        /// <summary>
        /// Called when the ViewModel is first created or activated.
        /// Override to initialize data, subscribe to events, etc.
        /// </summary>
        public virtual void OnActivate() { }
        
        /// <summary>
        /// Called when the ViewModel is being hidden but not destroyed.
        /// Override to pause updates, save state, etc.
        /// </summary>
        public virtual void OnDeactivate() { }
        
        /// <summary>
        /// Called every frame if the ViewModel needs continuous updates.
        /// Default implementation does nothing.
        /// Prefer reactive updates via BindableProperty when possible.
        /// </summary>
        public virtual void Tick(float deltaTime) { }
        
        /// <summary>
        /// Disposes the ViewModel and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            
            OnDispose();
            PropertyChanged = null;
        }
        
        /// <summary>
        /// Override to clean up ViewModel-specific resources.
        /// </summary>
        protected virtual void OnDispose() { }
    }
    
    /// <summary>
    /// Base class for ViewModels that synchronize with ECS data.
    /// Provides utilities for querying ECS World.
    /// </summary>
    public abstract class ECSViewModelBase : ViewModelBase
    {
        /// <summary>
        /// The ECS World this ViewModel reads data from.
        /// Set by the View or a service locator.
        /// </summary>
        protected Unity.Entities.World World { get; private set; }
        
        /// <summary>
        /// Whether this ViewModel has a valid World reference.
        /// </summary>
        public bool HasWorld => World != null && World.IsCreated;
        
        /// <summary>
        /// Sets the ECS World for this ViewModel.
        /// </summary>
        public void SetWorld(Unity.Entities.World world)
        {
            World = world;
            OnWorldSet();
        }
        
        /// <summary>
        /// Called after World is set. Override to initialize queries.
        /// </summary>
        protected virtual void OnWorldSet() { }
        
        /// <summary>
        /// Convenience method to get a system from the World.
        /// </summary>
        protected T GetSystem<T>() where T : Unity.Entities.ComponentSystemBase
        {
            return World?.GetExistingSystemManaged<T>();
        }
        
        /// <summary>
        /// Convenience method to get EntityManager.
        /// </summary>
        protected Unity.Entities.EntityManager EntityManager => World?.EntityManager ?? default;
    }
}
