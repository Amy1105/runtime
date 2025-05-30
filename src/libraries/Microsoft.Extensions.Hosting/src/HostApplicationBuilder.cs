﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
<<<<<<< HEAD
    /// Represents a hosted applications and services builder which helps manage configuration, logging, lifetime, and more.
    /// 表示一个托管的应用程序和服务构建器，它有助于管理配置、日志记录、生命周期等。
=======
    /// Represents a hosted applications and services builder that helps manage configuration, logging, lifetime, and more.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
    /// </summary>
    public sealed class HostApplicationBuilder : IHostApplicationBuilder
    {
        private readonly HostBuilderContext _hostBuilderContext;
        private readonly ServiceCollection _serviceCollection = new();
        private readonly IHostEnvironment _environment;
        private readonly LoggingBuilder _logging;
        private readonly MetricsBuilder _metrics;

        private Func<IServiceProvider> _createServiceProvider;
        private Action<object> _configureContainer = _ => { };
        private HostBuilderAdapter? _hostBuilderAdapter;

        private IServiceProvider? _appServices;
        private bool _hostBuilt;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostApplicationBuilder"/> class with preconfigured defaults.
        /// </summary>
        /// <remarks>
        ///   The following defaults are applied to the returned <see cref="HostApplicationBuilder"/>:
        ///   <list type="bullet">
        ///     <item><description>set the <see cref="IHostEnvironment.ContentRootPath"/> to the result of <see cref="Directory.GetCurrentDirectory()"/></description></item>
        ///     <item><description>load host <see cref="IConfiguration"/> from "DOTNET_" prefixed environment variables</description></item>
        ///     <item><description>load host <see cref="IConfiguration"/> from supplied command line args</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from 'appsettings.json' and 'appsettings.[<see cref="IHostEnvironment.EnvironmentName"/>].json'</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from User Secrets when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development' using the entry assembly</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from environment variables</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from supplied command line args</description></item>
        ///     <item><description>configure the <see cref="ILoggerFactory"/> to log to the console, debug, and event source output</description></item>
        ///     <item><description>enables scope validation on the dependency injection container when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development'</description></item>
        ///   </list>
        /// </remarks>
        public HostApplicationBuilder()
            : this(args: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HostApplicationBuilder"/> class with preconfigured defaults.
        /// </summary>
        /// <remarks>
        ///   The following defaults are applied to the returned <see cref="HostApplicationBuilder"/>:
        ///   <list type="bullet">
        ///     <item><description>set the <see cref="IHostEnvironment.ContentRootPath"/> to the result of <see cref="Directory.GetCurrentDirectory()"/></description></item>
        ///     <item><description>load host <see cref="IConfiguration"/> from "DOTNET_" prefixed environment variables</description></item>
        ///     <item><description>load host <see cref="IConfiguration"/> from supplied command line args</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from 'appsettings.json' and 'appsettings.[<see cref="IHostEnvironment.EnvironmentName"/>].json'</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from User Secrets when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development' using the entry assembly</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from environment variables</description></item>
        ///     <item><description>load app <see cref="IConfiguration"/> from supplied command line args</description></item>
        ///     <item><description>configure the <see cref="ILoggerFactory"/> to log to the console, debug, and event source output</description></item>
        ///     <item><description>enables scope validation on the dependency injection container when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development'</description></item>
        ///   </list>
        /// </remarks>
        /// <param name="args">The command line args.</param>
        public HostApplicationBuilder(string[]? args)
            : this(new HostApplicationBuilderSettings { Args = args })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HostApplicationBuilder"/>.
        /// </summary>
        /// <param name="settings">Settings controlling initial configuration and whether default settings should be used.</param>
        public HostApplicationBuilder(HostApplicationBuilderSettings? settings)
        {
            //接收传入的参数
            settings ??= new HostApplicationBuilderSettings();
            Configuration = settings.Configuration ?? new ConfigurationManager();

            if (!settings.DisableDefaults)
            {
                if (settings.ContentRootPath is null && Configuration[HostDefaults.ContentRootKey] is null)
                {
                    HostingHostBuilderExtensions.SetDefaultContentRoot(Configuration);
                }

                Configuration.AddEnvironmentVariables(prefix: "DOTNET_");
            }

            Initialize(settings, out _hostBuilderContext, out _environment, out _logging, out _metrics);

            ServiceProviderOptions? serviceProviderOptions = null;
            if (!settings.DisableDefaults)
            {
                HostingHostBuilderExtensions.ApplyDefaultAppConfiguration(_hostBuilderContext, Configuration, settings.Args);
                //添加默认服务service
                HostingHostBuilderExtensions.AddDefaultServices(_hostBuilderContext, Services);

                serviceProviderOptions = HostingHostBuilderExtensions.CreateDefaultServiceProviderOptions(_hostBuilderContext);
            }

            _createServiceProvider = () =>
            {
                // 如果有人通过HostBuilderAdapter添加回调，请调用_configureContainer。在生成过程中配置容器<IServiceCollection>（）。
                // 否则，就没有行动了。
                // Call _configureContainer in case anyone adds callbacks via HostBuilderAdapter.ConfigureContainer<IServiceCollection>() during build. Otherwise, this no-ops.
                _configureContainer(Services);
                return serviceProviderOptions is null ? Services.BuildServiceProvider() : Services.BuildServiceProvider(serviceProviderOptions);
            };
        }

        internal HostApplicationBuilder(HostApplicationBuilderSettings? settings, bool empty)
        {
            Debug.Assert(empty, "should only be called with empty: true");

            settings ??= new HostApplicationBuilderSettings();
            Configuration = settings.Configuration ?? new ConfigurationManager();

            Initialize(settings, out _hostBuilderContext, out _environment, out _logging, out _metrics);

            _createServiceProvider = () =>
            {
                // Call _configureContainer in case anyone adds callbacks via HostBuilderAdapter.ConfigureContainer<IServiceCollection>() during build.
                // Otherwise, this no-ops.
                _configureContainer(Services);
                return Services.BuildServiceProvider();
            };
        }

        private void Initialize(HostApplicationBuilderSettings settings, out HostBuilderContext hostBuilderContext, out IHostEnvironment environment, out LoggingBuilder logging, out MetricsBuilder metrics)
        {
            // Command line args are added even when settings.DisableDefaults == true. If the caller didn't want settings.Args applied,
            // they wouldn't have set them on the settings.
            HostingHostBuilderExtensions.AddCommandLineConfig(Configuration, settings.Args);

            // HostApplicationBuilderSettings override all other config sources.
            List<KeyValuePair<string, string?>>? optionList = null;
            if (settings.ApplicationName is not null)
            {
                optionList ??= new List<KeyValuePair<string, string?>>();
                optionList.Add(new KeyValuePair<string, string?>(HostDefaults.ApplicationKey, settings.ApplicationName));
            }
            if (settings.EnvironmentName is not null)
            {
                optionList ??= new List<KeyValuePair<string, string?>>();
                optionList.Add(new KeyValuePair<string, string?>(HostDefaults.EnvironmentKey, settings.EnvironmentName));
            }
            if (settings.ContentRootPath is not null)
            {
                optionList ??= new List<KeyValuePair<string, string?>>();
                optionList.Add(new KeyValuePair<string, string?>(HostDefaults.ContentRootKey, settings.ContentRootPath));
            }
            if (optionList is not null)
            {
                Configuration.AddInMemoryCollection(optionList);
            }

            (HostingEnvironment hostingEnvironment, PhysicalFileProvider physicalFileProvider) = HostBuilder.CreateHostingEnvironment(Configuration);

            Configuration.SetFileProvider(physicalFileProvider);

            hostBuilderContext = new HostBuilderContext(new Dictionary<object, object>())
            {
                HostingEnvironment = hostingEnvironment,
                Configuration = Configuration,
            };

            environment = hostingEnvironment;

            HostBuilder.PopulateServiceCollection(
                Services,
                hostBuilderContext,
                hostingEnvironment,
                physicalFileProvider,
                Configuration,
                () => _appServices!);

            logging = new LoggingBuilder(Services);
            metrics = new MetricsBuilder(Services);
        }

        IDictionary<object, object> IHostApplicationBuilder.Properties => _hostBuilderContext.Properties;

        /// <inheritdoc />
        public IHostEnvironment Environment => _environment;

        /// <summary>
        /// Gets the set of key/value configuration properties.
        /// </summary>
        /// <remarks>
        /// This can be mutated by adding more configuration sources, which will update its current view.
        /// </remarks>
        public ConfigurationManager Configuration { get; }

        IConfigurationManager IHostApplicationBuilder.Configuration => Configuration;

        /// <inheritdoc />
        public IServiceCollection Services => _serviceCollection;

        /// <inheritdoc />
        public ILoggingBuilder Logging => _logging;

        /// <inheritdoc />
        public IMetricsBuilder Metrics => _metrics;

        /// <inheritdoc />
        public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null) where TContainerBuilder : notnull
        {
            _createServiceProvider = () =>
            {
                TContainerBuilder containerBuilder = factory.CreateBuilder(Services);
                // Call _configureContainer in case anyone adds more callbacks via HostBuilderAdapter.ConfigureContainer<TContainerBuilder>() during build.
                // Otherwise, this is equivalent to configure?.Invoke(containerBuilder).
                _configureContainer(containerBuilder);
                return factory.CreateServiceProvider(containerBuilder);
            };

            // Store _configureContainer separately so it can replaced individually by the HostBuilderAdapter.
            _configureContainer = containerBuilder => configure?.Invoke((TContainerBuilder)containerBuilder);
        }

        /// <summary>
        /// Builds the host. This method can only be called once.
        /// </summary>
        /// <returns>An initialized <see cref="IHost"/>.</returns>
        public IHost Build()
        {
            if (_hostBuilt)
            {
                throw new InvalidOperationException(SR.BuildCalled);
            }
            _hostBuilt = true;

            using DiagnosticListener diagnosticListener = HostBuilder.LogHostBuilding(this);
            _hostBuilderAdapter?.ApplyChanges();

            _appServices = _createServiceProvider();

            // Prevent further modification of the service collection now that the provider is built.
            _serviceCollection.MakeReadOnly();

            return HostBuilder.ResolveHost(_appServices, diagnosticListener);
        }

        // Lazily allocate HostBuilderAdapter so the allocations can be avoided if there's nothing observing the events.
        //懒惰地分配HostBuilderAdapter，这样如果没有任何东西观察到事件，就可以避免分配
        internal IHostBuilder AsHostBuilder() => _hostBuilderAdapter ??= new HostBuilderAdapter(this);

        private sealed class HostBuilderAdapter : IHostBuilder
        {
            private readonly HostApplicationBuilder _hostApplicationBuilder;

            private readonly List<Action<IConfigurationBuilder>> _configureHostConfigActions = new();
            private readonly List<Action<HostBuilderContext, IConfigurationBuilder>> _configureAppConfigActions = new();
            private readonly List<IConfigureContainerAdapter> _configureContainerActions = new();
            private readonly List<Action<HostBuilderContext, IServiceCollection>> _configureServicesActions = new();

            private IServiceFactoryAdapter? _serviceProviderFactory;

            public HostBuilderAdapter(HostApplicationBuilder hostApplicationBuilder)
            {
                _hostApplicationBuilder = hostApplicationBuilder;
            }

            public void ApplyChanges()
            {
                ConfigurationManager config = _hostApplicationBuilder.Configuration;

                if (_configureHostConfigActions.Count > 0)
                {
                    string? previousApplicationName = config[HostDefaults.ApplicationKey];
                    string? previousEnvironment = config[HostDefaults.EnvironmentKey];
                    string? previousContentRootConfig = config[HostDefaults.ContentRootKey];
                    string previousContentRootPath = _hostApplicationBuilder._hostBuilderContext.HostingEnvironment.ContentRootPath;

                    foreach (Action<IConfigurationBuilder> configureHostAction in _configureHostConfigActions)
                    {
                        configureHostAction(config);
                    }

                    // Disallow changing any host settings this late in the cycle. The reasoning is that we've already loaded the default configuration
                    // and done other things based on environment name, application name or content root.
                    // 不允许在周期的后期更改任何主机设置。原因是，我们已经加载了默认配置，并根据环境名称、应用程序名称或内容根做了其他事情。
                    if (!string.Equals(previousApplicationName, config[HostDefaults.ApplicationKey], StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException(SR.Format(SR.ApplicationNameChangeNotSupported, previousApplicationName, config[HostDefaults.ApplicationKey]));
                    }
                    if (!string.Equals(previousEnvironment, config[HostDefaults.EnvironmentKey], StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException(SR.Format(SR.EnvironmentNameChangeNotSupoprted, previousEnvironment, config[HostDefaults.EnvironmentKey]));
                    }
                    // It's okay if the ConfigureHostConfiguration callbacks either left the config unchanged or set it back to the real ContentRootPath.
                    // Setting it to anything else indicates code intends to change the content root via HostFactoryResolver which is unsupported.
                    string? currentContentRootConfig = config[HostDefaults.ContentRootKey];
                    if (!string.Equals(previousContentRootConfig, currentContentRootConfig, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(previousContentRootPath, HostBuilder.ResolveContentRootPath(currentContentRootConfig, AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException(SR.Format(SR.ContentRootChangeNotSupported, previousContentRootConfig, currentContentRootConfig));
                    }
                }

                foreach (Action<HostBuilderContext, IConfigurationBuilder> configureAppAction in _configureAppConfigActions)
                {
                    configureAppAction(_hostApplicationBuilder._hostBuilderContext, config);
                }
                foreach (Action<HostBuilderContext, IServiceCollection> configureServicesAction in _configureServicesActions)
                {
                    configureServicesAction(_hostApplicationBuilder._hostBuilderContext, _hostApplicationBuilder.Services);
                }

                if (_configureContainerActions.Count > 0)
                {
                    Action<object> previousConfigureContainer = _hostApplicationBuilder._configureContainer;

                    _hostApplicationBuilder._configureContainer = containerBuilder =>
                    {
                        previousConfigureContainer(containerBuilder);

                        foreach (IConfigureContainerAdapter containerAction in _configureContainerActions)
                        {
                            containerAction.ConfigureContainer(_hostApplicationBuilder._hostBuilderContext, containerBuilder);
                        }
                    };
                }
                if (_serviceProviderFactory is not null)
                {
                    _hostApplicationBuilder._createServiceProvider = () =>
                    {
                        object containerBuilder = _serviceProviderFactory.CreateBuilder(_hostApplicationBuilder.Services);
                        _hostApplicationBuilder._configureContainer(containerBuilder);
                        return _serviceProviderFactory.CreateServiceProvider(containerBuilder);
                    };
                }
            }

            public IDictionary<object, object> Properties => _hostApplicationBuilder._hostBuilderContext.Properties;

            public IHost Build() => throw new NotSupportedException();

            public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
            {
                ArgumentNullException.ThrowIfNull(configureDelegate);

                _configureHostConfigActions.Add(configureDelegate);
                return this;
            }

            public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
            {
                ArgumentNullException.ThrowIfNull(configureDelegate);

                _configureAppConfigActions.Add(configureDelegate);
                return this;
            }

            public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
            {
                ArgumentNullException.ThrowIfNull(configureDelegate);

                _configureServicesActions.Add(configureDelegate);
                return this;
            }

            public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
            {
                ArgumentNullException.ThrowIfNull(factory);

                _serviceProviderFactory = new ServiceFactoryAdapter<TContainerBuilder>(factory);
                return this;

            }

            public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory) where TContainerBuilder : notnull
            {
                ArgumentNullException.ThrowIfNull(factory);

                _serviceProviderFactory = new ServiceFactoryAdapter<TContainerBuilder>(() => _hostApplicationBuilder._hostBuilderContext, factory);
                return this;
            }

            public IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate)
            {
                ArgumentNullException.ThrowIfNull(configureDelegate);

                _configureContainerActions.Add(new ConfigureContainerAdapter<TContainerBuilder>(configureDelegate));
                return this;
            }
        }

        private sealed class LoggingBuilder : ILoggingBuilder
        {
            public LoggingBuilder(IServiceCollection services)
            {
                Services = services;
            }

            public IServiceCollection Services { get; }
        }

        private sealed class MetricsBuilder(IServiceCollection services) : IMetricsBuilder
        {
            public IServiceCollection Services { get; } = services;
        }
    }
}
