// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json
{
    internal enum PolymorphicSerializationState : byte
    {
        None,

        /// <summary>
        /// Dispatch to a derived converter has been initiated.
        /// 已启动对衍生转换器的调度。
        /// </summary>
        PolymorphicReEntryStarted,

        /// <summary>
        /// Current frame is a continuation using a suspended derived converter.
        /// 当前帧是使用挂起的派生转换器的延续。
        /// </summary>
        PolymorphicReEntrySuspended,

        /// <summary>
        /// Current frame is a polymorphic converter that couldn't resolve a derived converter.
        /// 当前帧是一个多态转换器，无法解析派生转换器。
        /// (E.g. because the runtime type matches the declared type).
        /// </summary>
        PolymorphicReEntryNotFound
    }
}
