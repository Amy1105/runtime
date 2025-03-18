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
        /// ���ڷ����ļ������ݡ�
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
        /// ȷ���ڻ����ļ�����ʱ�Ƿ����Դ��
        /// </summary>
        public bool ReloadOnChange { get; set; }

        /// <summary>
        /// ����Load֮ǰ���¼��ؽ��ȴ��ĺ������� 
        /// �������ڱ������ļ���ȫд��֮ǰ�������¼��ء�Ĭ��ֵΪ250��
        /// </summary>
        public int ReloadDelay { get; set; } = 250;

        /// <summary>
        /// ���FileConfigurationProvider�з���δ������쳣���������á����ء�
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
        /// ������ʹ�ù������ϵ��κ�Ĭ�����ã���FileProvider��FileLoadExceptionHandler
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
        /// ���û�������ļ��ṩ���򣬶��ھ���·�����⽫Ϊ���������Ŀ¼����һ�������ļ��ṩ����
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
