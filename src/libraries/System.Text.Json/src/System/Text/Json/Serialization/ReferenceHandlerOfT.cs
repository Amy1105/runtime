// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// This class defines how the <see cref="JsonSerializer"/> deals with references on serialization and deserialization.
    /// 此类定义了<see-cref=“JsonSerializer”/>如何处理序列化和反序列化的引用。
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="ReferenceResolver"/> to create on each serialization or deserialization call.
    /// 每次序列化或反序列化调用时要创建的<see cref=“ReferenceResolver”/>的类型
    /// </typeparam>
    public sealed class ReferenceHandler<T> : ReferenceHandler
        where T: ReferenceResolver, new()
    {
        /// <summary>
        /// Creates a new <see cref="ReferenceResolver"/> of type <typeparamref name="T"/> used for each serialization call.
        /// </summary>
        /// <returns>The new resolver to use for serialization and deserialization.</returns>
        public override ReferenceResolver CreateResolver() => new T();
    }
}
