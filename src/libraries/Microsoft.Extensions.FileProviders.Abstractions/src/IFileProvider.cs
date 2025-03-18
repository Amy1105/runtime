// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.FileProviders
{
    /// <summary>
    /// 只读文件提供程序抽象。
    /// </summary>
    public interface IFileProvider
    {
        /// <summary>
        /// 在给定路径中查找文件
        /// </summary>
        /// <param name="subpath">Relative path that identifies the file.</param>
        /// <returns>The file information. Caller must check Exists property.</returns>
        IFileInfo GetFileInfo(string subpath);

        /// <summary>
        /// 枚举给定路径下的目录（如果有的话）。
        /// </summary>
        /// <param name="subpath">Relative path that identifies the directory.</param>
        /// <returns>Returns the contents of the directory.</returns>
        IDirectoryContents GetDirectoryContents(string subpath);

        /// <summary>
        /// Creates a <see cref="IChangeToken"/> for the specified <paramref name="filter"/>.
        /// </summary>
        /// <param name="filter">Filter string used to determine what files or folders to monitor. Example: **/*.cs, *.*, subFolder/**/*.cshtml.</param>
        /// <returns>An <see cref="IChangeToken"/> 当文件匹配时，会收到通知 <paramref name="filter"/> is added, modified or deleted.</returns>
        IChangeToken Watch(string filter);
    }
}
