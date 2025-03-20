// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Specifies the contract for a collection of service descriptors.
    /// 指定服务描述符集合的契约。
    /// </summary>
    public interface IServiceCollection : IList<ServiceDescriptor>
    {
    }
}
