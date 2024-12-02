// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// This class defines how the <see cref="JsonSerializer"/> deals with references on serialization and deserialization.
    /// ���ඨ����<see-cref=��JsonSerializer��/>��δ������л��ͷ����л������á�
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="ReferenceResolver"/> to create on each serialization or deserialization call.
    /// ÿ�����л������л�����ʱҪ������<see cref=��ReferenceResolver��/>������
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
