// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace System.Runtime.CompilerServices
{
    // This Enum matches the miImpl flags defined in corhdr.h. It is used to specify
    // certain method properties.
    //InternalCall:这通常意味着该方法是由 .NET 基类库的非托管C++代码实现的，并且在托管的中间语言(IL)中不存在该方法的实现
    public enum MethodImplOptions
    {
        Unmanaged = 0x0004,
        NoInlining = 0x0008,
        NoOptimization = 0x0040,
        AggressiveInlining = 0x0100,
        AggressiveOptimization = 0x200,
        InternalCall = 0x1000,
    }

    // Custom attribute to specify additional method properties.
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
    public sealed class MethodImplAttribute : Attribute
    {
        internal MethodImplOptions _val;

        public MethodImplAttribute(MethodImplOptions methodImplOptions)
        {
            _val = methodImplOptions;
        }

        public MethodImplAttribute(short value)
        {
            _val = (MethodImplOptions)value;
        }

        public MethodImplAttribute()
        {
        }

        public MethodImplOptions Value { get { return _val; } }
    }
}
