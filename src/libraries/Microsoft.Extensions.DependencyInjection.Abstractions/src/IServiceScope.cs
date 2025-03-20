// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// The <see cref="System.IDisposable.Dispose"/> method ends the scope lifetime.
    /// <see cref="System.IDisposable.Dispose"/> 方法结束作用域生存期。
    /// Once Dispose is called, any scoped services that have been resolved from
    /// <see cref="Microsoft.Extensions.DependencyInjection.IServiceScope.ServiceProvider"/> will be disposed.
    /// 一旦调用了Dispose，所有已从<see cref="Microsoft.Extensions.DependencyInjection.IServiceScope.ServiceProvider"/>解析的作用域服务都将被处理。
    /// </summary>
    public interface IServiceScope : IDisposable
    {
        /// <summary>
        /// The <see cref="System.IServiceProvider"/> used to resolve dependencies from the scope.
        /// </summary>
        IServiceProvider ServiceProvider { get; }
    }
}
