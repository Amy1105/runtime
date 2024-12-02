// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization.Converters
{
    [Flags]
    internal enum EnumConverterOptions
    {
        /// <summary>
        /// Allow string values.
        /// 允许字符串值。
        /// </summary>
        AllowStrings = 0b0001,

        /// <summary>
        /// Allow number values.
        /// 允许数字值
        /// </summary>
        AllowNumbers = 0b0010
    }
}
