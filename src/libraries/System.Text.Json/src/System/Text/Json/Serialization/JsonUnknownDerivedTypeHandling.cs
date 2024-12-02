// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Defines how objects of a derived runtime type that has not been explicitly declared for polymorphic serialization should be handled.
    ///定义如何处理未明确声明用于多态序列化的派生运行时类型的对象
    /// </summary>
    public enum JsonUnknownDerivedTypeHandling
    {
        /// <summary>
        /// An object of undeclared runtime type will fail polymorphic serialization.
        /// 未声明运行时类型的对象将无法进行多态序列化。
        /// </summary>
        FailSerialization = 0,
        /// <summary>
        /// An object of undeclared runtime type will fall back to the serialization contract of the base type.
        /// 未声明的运行时类型的对象将回退到基类型的序列化契约。
        /// </summary>
        FallBackToBaseType = 1,
        /// <summary>
        /// An object of undeclared runtime type will revert to the serialization contract of the nearest declared ancestor type.
        /// 未声明的运行时类型的对象将还原为最近声明的祖先类型的序列化契约。
        /// Certain interface hierarchies are not supported due to diamond ambiguity constraints.
        /// 由于菱形模糊约束，某些接口层次结构不受支持。
        /// </summary>
        FallBackToNearestAncestor = 2
    }
}
