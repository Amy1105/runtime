// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.ComponentModel
{
    /// <summary>
    /// Provides the default implementation for the <see cref='System.ComponentModel.IComponent'/> interface and enables object-sharing between applications.
    /// 为<see cref='System.ComponentModel.IComponent'/>提高默认实现，并支持应用程序之间的对象共享。
    /// </summary>
    [DesignerCategory("Component")]
    public class Component : MarshalByRefObject, IComponent
    {
        /// <summary>
        /// Static hash key for the Disposed event. This field is read-only.
        /// Disposed事件的静态哈希键。此字段是只读的。
        /// </summary>
        private static readonly object s_eventDisposed = new object();

        private ISite? _site;
        private EventHandlerList? _events;

        ~Component() => Dispose(false);

        /// <summary>
        /// This property returns true if the component is in a mode that supports
        /// raising events. By default, components always support raising their events
        /// and therefore this method always returns true. You can override this method
        /// in a deriving class and change it to return false when needed. if the return
        /// value of this method is false, the EventList collection returned by the Events
        /// property will always return null for an event. Events can still be added and
        /// removed from the collection, but retrieving them through the collection's Item
        /// property will always return null.
        /// </summary>
        protected virtual bool CanRaiseEvents => true;

        /// <summary>
        /// Internal API that allows the event handler list class to access the CanRaiseEvents property.
        /// 允许事件处理程序列表类访问CanRaiseEvents属性的内部API。
        /// </summary>
        internal bool CanRaiseEventsInternal => CanRaiseEvents;

        /// <summary>
        /// Adds an event handler to listen to the Disposed event on the component.
        /// 添加一个事件处理程序来监听组件上的“已处理”事件。
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public event EventHandler? Disposed
        {
            add => Events.AddHandler(s_eventDisposed, value);
            remove => Events.RemoveHandler(s_eventDisposed, value);
        }

        /// <summary>
        /// Gets the list of event handlers that are attached to this component.
        /// 获取附加到此组件的事件处理程序的列表。
        /// </summary>
        protected EventHandlerList Events => _events ??= new EventHandlerList(this);

        /// <summary>
        /// Gets or sets the site of the <see cref='System.ComponentModel.Component'/>.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual ISite? Site
        {
            get => _site;
            set => _site = value;
        }

        /// <summary>
        /// Disposes of the <see cref='System.ComponentModel.Component'/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes all the resources associated with this component.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (this)
                {
                    _site?.Container?.Remove(this);
                    if (_events != null)
                    {
                        ((EventHandler?)_events[s_eventDisposed])?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the <see cref='System.ComponentModel.IContainer'/>
        /// that contains the <see cref='System.ComponentModel.Component'/>.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IContainer? Container => _site?.Container;

        /// <summary>
        /// Returns an object representing a service provided by
        /// the <see cref='System.ComponentModel.Component'/>.
        /// </summary>
        protected virtual object? GetService(Type service) => _site?.GetService(service);

        /// <summary>
        /// Gets a value indicating whether the <see cref='System.ComponentModel.Component'/>
        /// is currently in design mode.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        protected bool DesignMode => _site?.DesignMode ?? false;

        /// <summary>
        /// Returns a <see cref='string'/> containing the name of the
        /// <see cref='System.ComponentModel.Component'/>, if any.
        /// This method should not be overridden.
        /// </summary>
        public override string ToString()
        {
            ISite? s = _site;
            if (s == null)
            {
                return GetType().FullName!;
            }

            return s.Name + " [" + GetType().FullName + "]";
        }
    }
}
