// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;

namespace System
{
    /// <summary>
    /// Provides an IProgress{T} that invokes callbacks for each reported progress value.
    /// 提供一个IProgress｛T｝，它为每个报告的进度值调用回调
    /// </summary>
    /// <typeparam name="T">Specifies the type of the progress report value.</typeparam>
    /// <remarks>
    /// Any handler provided to the constructor or event handlers registered with
    /// the <see cref="ProgressChanged"/> event are invoked through a
    /// <see cref="SynchronizationContext"/> instance captured
    /// when the instance is constructed.  If there is no current SynchronizationContext
    /// at the time of construction, the callbacks will be invoked on the ThreadPool.
    /// </remarks>
    public class Progress<T> : IProgress<T>
    {
        /// <summary>The synchronization context captured upon construction.  This will never be null.</summary>
        private readonly SynchronizationContext _synchronizationContext;
        /// <summary>The handler specified to the constructor.  This may be null.</summary>
        private readonly Action<T>? _handler;
        /// <summary>A cached delegate used to post invocation to the synchronization context.</summary>
        private readonly SendOrPostCallback _invokeHandlers;

        /// <summary>Initializes the <see cref="Progress{T}"/>.</summary>
        public Progress()
        {
            // Capture the current synchronization context.
            // If there is no current context, we use a default instance targeting the ThreadPool.
            _synchronizationContext = SynchronizationContext.Current ?? ProgressStatics.DefaultContext;
            Debug.Assert(_synchronizationContext != null);
            _invokeHandlers = new SendOrPostCallback(InvokeHandlers);
        }

        /// <summary>Initializes the <see cref="Progress{T}"/> with the specified callback.</summary>
        /// <param name="handler">
        /// A handler to invoke for each reported progress value.  This handler will be invoked
        /// in addition to any delegates registered with the <see cref="ProgressChanged"/> event.
        /// Depending on the <see cref="SynchronizationContext"/> instance captured by
        /// the <see cref="Progress{T}"/> at construction, it's possible that this handler instance
        /// could be invoked concurrently with itself.
        /// </param>
        /// <exception cref="ArgumentNullException">The <paramref name="handler"/> is null (<see langword="Nothing" /> in Visual Basic).</exception>
        public Progress(Action<T> handler) : this()
        {
            ArgumentNullException.ThrowIfNull(handler);

            _handler = handler;
        }

        /// <summary>Raised for each reported progress value.</summary>
        /// <remarks>
        /// Handlers registered with this event will be invoked on the
        /// <see cref="SynchronizationContext"/> captured when the instance was constructed.
        /// </remarks>
        public event EventHandler<T>? ProgressChanged;

        /// <summary>Reports a progress change.</summary>
        /// <param name="value">The value of the updated progress.</param>
        protected virtual void OnReport(T value)
        {
            // If there's no handler, don't bother going through the sync context.
            // Inside the callback, we'll need to check again, in case
            // an event handler is removed between now and then.
            Action<T>? handler = _handler;
            EventHandler<T>? changedEvent = ProgressChanged;
            if (handler != null || changedEvent != null)
            {
                // Post the processing to the sync context.
                // (If T is a value type, it will get boxed here.)
                _synchronizationContext.Post(_invokeHandlers, value);
            }
        }

        /// <summary>Reports a progress change.</summary>
        /// <param name="value">The value of the updated progress.</param>
        void IProgress<T>.Report(T value) { OnReport(value); }

        /// <summary>Invokes the action and event callbacks.</summary>
        /// <param name="state">The progress value.</param>
        private void InvokeHandlers(object? state)
        {
            T value = (T)state!;

            Action<T>? handler = _handler;
            EventHandler<T>? changedEvent = ProgressChanged;

            handler?.Invoke(value);
            changedEvent?.Invoke(this, value);
        }
    }

    /// <summary>Holds static values for <see cref="Progress{T}"/>.</summary>
    /// <remarks>This avoids one static instance per type T.</remarks>
    internal static class ProgressStatics
    {
        /// <summary>A default synchronization context that targets the ThreadPool.</summary>
        internal static readonly SynchronizationContext DefaultContext = new SynchronizationContext();
    }
}
