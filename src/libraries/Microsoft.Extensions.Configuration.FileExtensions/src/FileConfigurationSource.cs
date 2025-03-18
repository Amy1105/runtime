// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Represents a base class for file based <see cref="IConfigurationSource"/>.
    /// </summary>
    public abstract class FileConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// 用于访问文件的内容。
        /// </summary>
        public IFileProvider? FileProvider { get; set; }

        /// <summary>
        /// Set to true when <see cref="FileProvider"/> was not provided by user and can be safely disposed.
        /// </summary>
        internal bool OwnsFileProvider { get; private set; }

        /// <summary>
        /// The path to the file.
        /// </summary>
        [DisallowNull]
        public string? Path { get; set; }

        /// <summary>
        /// Determines if loading the file is optional.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// 确定在基础文件更改时是否加载源。
        /// </summary>
        public bool ReloadOnChange { get; set; }

        /// <summary>
        /// 调用Load之前重新加载将等待的毫秒数。 
        /// 这有助于避免在文件完全写入之前触发重新加载。默认值为250。
        /// </summary>
        public int ReloadDelay { get; set; } = 250;

        /// <summary>
        /// 如果FileConfigurationProvider中发生未捕获的异常，将被调用。加载。
        /// </summary>
        public Action<FileLoadExceptionContext>? OnLoadException { get; set; }

        /// <summary>
        /// Builds the <see cref="IConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="IConfigurationProvider"/></returns>
        public abstract IConfigurationProvider Build(IConfigurationBuilder builder);

        /// <summary>
        /// Called to use any default settings on the builder like the FileProvider or FileLoadExceptionHandler.
        /// 调用以使用构建器上的任何默认设置，如FileProvider或FileLoadExceptionHandler
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        public void EnsureDefaults(IConfigurationBuilder builder)
        {
            if (FileProvider is null && builder.GetUserDefinedFileProvider() is null)
            {
                OwnsFileProvider = true;
            }

            FileProvider ??= builder.GetFileProvider();
            OnLoadException ??= builder.GetFileLoadExceptionHandler();
        }

        /// <summary>
        /// If no file provider has been set, for absolute Path, this will creates a physical file provider  for the nearest existing directory.
        /// 如果没有设置文件提供程序，对于绝对路径，这将为最近的现有目录创建一个物理文件提供程序
        /// </summary>
        public void ResolveFileProvider()
        {
            if (FileProvider == null &&
                !string.IsNullOrEmpty(Path) &&
                System.IO.Path.IsPathRooted(Path))
            {
                string? directory = System.IO.Path.GetDirectoryName(Path);
                string? pathToFile = System.IO.Path.GetFileName(Path);
                while (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    pathToFile = System.IO.Path.Combine(System.IO.Path.GetFileName(directory), pathToFile);
                    directory = System.IO.Path.GetDirectoryName(directory);
                }
                if (Directory.Exists(directory))
                {
                    OwnsFileProvider = true;
                    FileProvider = new PhysicalFileProvider(directory);
                    Path = pathToFile;
                }
            }
        }
    }
}
