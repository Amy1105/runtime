// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
<<<<<<< HEAD
    /// A factory for creating instances of <see cref="IServiceScope"/>, which is used to create services within a scope.
    /// 创建<see cref="IServiceScope"/>实例的工厂，用于在作用域内创建服务。
=======
    /// Creates instances of <see cref="IServiceScope"/>, which is used to create
    /// services within a scope.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
    /// </summary>
    public interface IServiceScopeFactory
    {
        /// <summary>
        /// Create an <see cref="Microsoft.Extensions.DependencyInjection.IServiceScope"/> that
        /// contains an <see cref="System.IServiceProvider"/> used to resolve dependencies from a
        /// newly created scope.
        /// </summary>
        /// <returns>
        /// An <see cref="Microsoft.Extensions.DependencyInjection.IServiceScope"/> controlling the
        /// lifetime of the scope. Once this is disposed, any scoped services that have been resolved
        /// from the <see cref="Microsoft.Extensions.DependencyInjection.IServiceScope.ServiceProvider"/>
        /// will also be disposed.
        /// </returns>
        IServiceScope CreateScope();
    }
}
