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
<<<<<<< HEAD
        /// 在给定路径中查找文件
=======
        /// Locates a file at the given path.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
        /// </summary>
        /// <param name="subpath">The relative path that identifies the file.</param>
        /// <returns>The file information. Caller must check Exists property.</returns>
        IFileInfo GetFileInfo(string subpath);

        /// <summary>
<<<<<<< HEAD
        /// 枚举给定路径下的目录（如果有的话）。
=======
        /// Enumerates a directory at the given path, if any.
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
        /// </summary>
        /// <param name="subpath">The relative path that identifies the directory.</param>
        /// <returns>The contents of the directory.</returns>
        IDirectoryContents GetDirectoryContents(string subpath);

        /// <summary>
        /// Creates an <see cref="IChangeToken"/> for the specified <paramref name="filter"/>.
        /// </summary>
<<<<<<< HEAD
        /// <param name="filter">Filter string used to determine what files or folders to monitor. Example: **/*.cs, *.*, subFolder/**/*.cshtml.</param>
        /// <returns>An <see cref="IChangeToken"/> 当文件匹配时，会收到通知 <paramref name="filter"/> is added, modified or deleted.</returns>
=======
        /// <param name="filter">A filter string used to determine what files or folders to monitor. Examples: **/*.cs, *.*, subFolder/**/*.cshtml.</param>
        /// <returns>An <see cref="IChangeToken"/> that is notified when a file matching <paramref name="filter"/> is added, modified, or deleted.</returns>
>>>>>>> be6751023bf7837fa2f58bf1f7f6e7f6507c9798
        IChangeToken Watch(string filter);
    }
}
