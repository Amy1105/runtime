// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides an extension point for creating a container specific builder and an <see cref="IServiceProvider"/>.
    /// 提供一个扩展点，用于创建特定于容器的构建器和<see cref="IServiceProvider"/>>。
    /// </summary>
    public interface IServiceProviderFactory<TContainerBuilder> where TContainerBuilder : notnull
    {
        /// <summary>
        /// Creates a container builder from an <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The collection of services.</param>
        /// <returns>A container builder that can be used to create an <see cref="IServiceProvider"/>.</returns>
        TContainerBuilder CreateBuilder(IServiceCollection services);

        /// <summary>
        /// Creates an <see cref="IServiceProvider"/> from the container builder.
        /// </summary>
        /// <param name="containerBuilder">The container builder.</param>
        /// <returns>An <see cref="IServiceProvider"/> instance.</returns>
        IServiceProvider CreateServiceProvider(TContainerBuilder containerBuilder);
    }
}
