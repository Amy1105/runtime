// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.ComponentModel
{
    /// <summary>
    /// Provides a simple list of delegates. This class cannot be inherited.
    /// 提供一个简单的代理列表。这个类不能被继承。
    /// </summary>
    public sealed class EventHandlerList : IDisposable
    {
        private ListEntry? _head;
        private readonly Component? _parent;

        /// <summary>
        /// Creates a new event handler list. The parent component is used to check the component's CanRaiseEvents property.
        /// 创建新的事件处理程序列表。父组件用于检查组件的CanRaiseEvents属性。
        /// </summary>
        internal EventHandlerList(Component parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Creates a new event handler list.
        /// 创建新的事件处理程序列表。
        /// </summary>
        public EventHandlerList()
        {
        }

        /// <summary>
        /// Gets or sets the delegate for the specified key.
        /// 获取或设置指定键的委托。
        /// </summary>
        public Delegate? this[object key]
        {
            get
            {
                ListEntry? e = null;
                if (_parent == null || _parent.CanRaiseEventsInternal)
                {
                    e = Find(key);
                }

                return e?._handler;
            }
            set
            {
                ListEntry? e = Find(key);
                if (e != null)
                {
                    e._handler = value;
                }
                else
                {
                    _head = new ListEntry(key, value, _head);
                }
            }
        }

        public void AddHandler(object key, Delegate? value)
        {
            ListEntry? e = Find(key);
            if (e != null)
            {
                e._handler = Delegate.Combine(e._handler, value);
            }
            else
            {
                _head = new ListEntry(key, value, _head);
            }
        }

        public void AddHandlers(EventHandlerList listToAddFrom)
        {
            ArgumentNullException.ThrowIfNull(listToAddFrom);

            ListEntry? currentListEntry = listToAddFrom._head;
            while (currentListEntry != null)
            {
                AddHandler(currentListEntry._key, currentListEntry._handler);
                currentListEntry = currentListEntry._next;
            }
        }

        public void Dispose() => _head = null;

        private ListEntry? Find(object key)
        {
            ListEntry? found = _head;
            while (found != null)
            {
                if (found._key == key)
                {
                    break;
                }
                found = found._next;
            }
            return found;
        }

        public void RemoveHandler(object key, Delegate? value)
        {
            ListEntry? e = Find(key);
            if (e != null)
            {
                e._handler = Delegate.Remove(e._handler, value);
            }
        }

        private sealed class ListEntry
        {
            internal readonly ListEntry? _next;
            internal readonly object _key;
            internal Delegate? _handler;

            public ListEntry(object key, Delegate? handler, ListEntry? next)
            {
                _next = next;
                _key = key;
                _handler = handler;
            }
        }
    }
}
