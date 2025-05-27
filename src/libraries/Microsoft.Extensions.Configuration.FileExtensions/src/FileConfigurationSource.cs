// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Provides the base class for file-based <see cref="IConfigurationSource"/>.
    /// </summary>
    public abstract class FileConfigurationSource : IConfigurationSource
    {
        /// <summary>
<<<<<<< HEAD
        /// 用于访问文件的内容。
=======
        /// Gets or sets the provider used to access the contents of the file.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
        /// </summary>
        public IFileProvider? FileProvider { get; set; }

        /// <summary>
        /// Gets or sets the path to the file.
        /// </summary>
        [DisallowNull]
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether loading the file is optional.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
<<<<<<< HEAD
        /// 确定在基础文件更改时是否加载源。
=======
        /// Gets or sets a value that indicates whether the source will be loaded if the underlying file changes.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
        /// </summary>
        public bool ReloadOnChange { get; set; }

        /// <summary>
<<<<<<< HEAD
        /// 调用Load之前重新加载将等待的毫秒数。 
        /// 这有助于避免在文件完全写入之前触发重新加载。默认值为250。
=======
        /// Gets or sets the number of milliseconds that reload will wait before calling Load.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
        /// </summary>
        /// <value>
        /// The number of milliseconds that reload waits before calling Load. The default is 250.
        /// </value>
        /// <remarks>
        /// This delay helps avoid triggering reload before a file is completely written.
        /// </remarks>
        public int ReloadDelay { get; set; } = 250;

        /// <summary>
<<<<<<< HEAD
        /// 如果FileConfigurationProvider中发生未捕获的异常，将被调用。加载。
=======
        /// Gets or sets the action that's called if an uncaught exception occurs in FileConfigurationProvider.Load.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
        /// </summary>
        public Action<FileLoadExceptionContext>? OnLoadException { get; set; }

        /// <summary>
        /// Builds the <see cref="IConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>To be added.</returns>
        public abstract IConfigurationProvider Build(IConfigurationBuilder builder);

        /// <summary>
        /// Called to use any default settings on the builder like the FileProvider or FileLoadExceptionHandler.
        /// 调用以使用构建器上的任何默认设置，如FileProvider或FileLoadExceptionHandler
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        public void EnsureDefaults(IConfigurationBuilder builder)
        {
            FileProvider ??= builder.GetFileProvider();
            OnLoadException ??= builder.GetFileLoadExceptionHandler();
        }

        /// <summary>
<<<<<<< HEAD
        /// If no file provider has been set, for absolute Path, this will creates a physical file provider  for the nearest existing directory.
        /// 如果没有设置文件提供程序，对于绝对路径，这将为最近的现有目录创建一个物理文件提供程序
=======
        /// Creates a physical file provider for the nearest existing directory if no file provider has been set, for absolute Path.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
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
                    FileProvider = new PhysicalFileProvider(directory);
                    Path = pathToFile;
                }
            }
        }
    }
}
