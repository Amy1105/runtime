// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring various behaviors of the default <see cref="IServiceProvider"/> implementation.
    /// 用于配置默认<see cref="IServiceProvider"/>实现的各种行为的选项。
    /// </summary>
    public class ServiceProviderOptions
    {
        // Avoid allocating objects in the default case
        // 避免在默认情况下分配对象
        internal static readonly ServiceProviderOptions Default = new ServiceProviderOptions();

        /// <summary>
        /// <c>true</c> to perform check verifying that scoped services never gets resolved from root provider; otherwise <c>false</c>. Defaults to <c>false</c>.
        /// </summary>
        public bool ValidateScopes { get; set; }

        /// <summary>
        /// <c>true</c> to perform check verifying that all services can be created during <c>BuildServiceProvider</c> call; otherwise <c>false</c>. Defaults to <c>false</c>.
        /// NOTE: this check doesn't verify open generics services.
        /// </summary>
        public bool ValidateOnBuild { get; set; }
    }
}
