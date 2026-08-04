using System;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Takoda99.Client.Contract;

/// <summary>
/// 未知の enum 文字列値でデシリアライズ全体を失敗させないためのフォールバック処理
/// （01-contract.md §3.3）。JSON ツリー中の enum に対応する文字列値が、対象 enum の
/// 定義済み名称に一致しなければ、既定値（0番目のメンバー）の名称に書き換える。
/// </summary>
internal static class EnumFallbackSanitizer
{
    public static void Fix(JsonNode? node, Type type)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            if (!obj.TryGetPropertyValue(jsonName, out var value) || value is null)
            {
                continue;
            }

            var propertyType = prop.PropertyType;

            if (propertyType.IsEnum)
            {
                if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var raw)
                    && !TryParseExact(propertyType, raw, out _))
                {
                    obj[jsonName] = Enum.GetNames(propertyType)[0];
                }

                continue;
            }

            if (TryGetListElementType(propertyType, out var elementType) && IsNestedDto(elementType))
            {
                if (value is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        Fix(item, elementType);
                    }
                }

                continue;
            }

            if (IsNestedDto(propertyType))
            {
                Fix(value, propertyType);
            }
        }
    }

    private static bool TryParseExact(Type enumType, string raw, out object? result)
    {
        foreach (var name in Enum.GetNames(enumType))
        {
            if (string.Equals(name, raw, StringComparison.Ordinal))
            {
                result = Enum.Parse(enumType, name);
                return true;
            }
        }

        result = null;
        return false;
    }

    private static bool IsNestedDto(Type type)
    {
        return type.Namespace == "Takoda99.Proto" && type.IsClass;
    }

    private static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }
}
