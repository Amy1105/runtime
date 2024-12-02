// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Converter to convert enums to and from numeric values.
    /// 转换器，用于将枚举与数值进行转换。
    /// </summary>
    /// <typeparam name="TEnum">The enum type that this converter targets.</typeparam>
    /// 此转换器所针对的枚举类型
    /// <remarks>
    /// This is the default converter for enums and can be used to override
    /// <see cref="JsonSourceGenerationOptionsAttribute.UseStringEnumConverter"/>
    /// on individual types or properties.
    /// 这是枚举的默认转换器，可用于覆盖单个类型或属性。
    /// </remarks>
    public sealed class JsonNumberEnumConverter<TEnum> : JsonConverterFactory
        where TEnum : struct, Enum
    {
        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TEnum);

        /// <inheritdoc />
        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(TEnum))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_JsonConverterFactory_TypeNotSupported(typeToConvert);
            }

            return new EnumConverter<TEnum>(EnumConverterOptions.AllowNumbers, options);
        }
    }
}
