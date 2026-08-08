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
    /// <summary>
    /// 「空文字のとき JSON から消える」フィールド（`型のFullName.jsonName`）。
    /// </summary>
    /// <remarks>
    /// Go 正典の `omitempty` は**空文字なら出力しない**（docs/server-sync/05-表示名の実装指示.md §omitempty）。
    /// そのため空文字が正当な値であるフィールドは、その値のときだけ payload から丸ごと消える。
    /// これを「必須フィールドの欠落」と見なすとメッセージ全体が破棄される。
    ///
    /// `MatchEnd.reason` は「自店がどう終わったか。**優勝（最後まで残った）なら空文字**」と
    /// Proto に明記されている。つまり優勝時に限り `reason` が消え、MatchEnd が丸ごと
    /// `payload-decode-failed` で捨てられていた（＝1位だけリザルトへ進めない）。
    /// 脱落時は `SelfCollapse`/`Cull` が入るので消えず、2位以下では再現しない。
    ///
    /// 一律に「string は任意」とはしない。`CustomerLeft.customerId` のような識別子は
    /// 空で届いた時点で不正であり、破棄されるべきだから（05-dispatcher.md のテスト参照）。
    /// </remarks>
    private static readonly System.Collections.Generic.HashSet<string> OptionalWhenEmpty = new()
    {
        "Takoda99.Proto.MatchEnd.reason",
    };

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
                if (RequiresPresence(prop.PropertyType) && !IsOptionalWhenEmpty(type, jsonName))
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

    private static bool IsOptionalWhenEmpty(Type type, string jsonName)
    {
        return OptionalWhenEmpty.Contains($"{type.FullName}.{jsonName}");
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
