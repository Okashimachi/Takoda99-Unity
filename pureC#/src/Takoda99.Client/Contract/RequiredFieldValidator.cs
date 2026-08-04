using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Takoda99.Client.Contract;

/// <summary>
/// Proto の DTO には `required` 注釈が無いため、非 nullable なプロパティが
/// JSON 上に存在するかをリフレクションで確認する（01-contract.md §3.1 の
/// 「必須フィールド欠落 → null」を実現するための補助）。
/// </summary>
internal static class RequiredFieldValidator
{
    public static bool HasAllRequiredFields(JsonElement element, Type type)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            var hasProperty = element.TryGetProperty(jsonName, out var value);

            if (!hasProperty)
            {
                // 数値・bool・enum は欠落時に既定値(0/false/先頭メンバー)へフォールバックしても実害がない
                // ため、必須チェックの対象は「値が来ないと意味を成さない」参照型（string・入れ子DTO・配列）
                // に限定する（Delta のような補助フィールドの省略まで null 扱いにしないため）。
                if (RequiresPresence(prop.PropertyType))
                {
                    return false;
                }

                continue;
            }

            if (TryGetListElementType(prop.PropertyType, out var elementType))
            {
                if (value.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                if (IsNestedDto(elementType))
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        if (!HasAllRequiredFields(item, elementType))
                        {
                            return false;
                        }
                    }
                }
            }
            else if (IsNestedDto(prop.PropertyType))
            {
                if (!HasAllRequiredFields(value, prop.PropertyType))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool RequiresPresence(Type type)
    {
        if (type == typeof(string))
        {
            return true;
        }

        return IsNestedDto(type) || TryGetListElementType(type, out _);
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
