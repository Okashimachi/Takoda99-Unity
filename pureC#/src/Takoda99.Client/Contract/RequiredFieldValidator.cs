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
    /// 欠落・`null` で届くことが契約上ありうるフィールド（`型のFullName.jsonName`）。
    /// ここに載っているフィールドは、無くてもメッセージ全体を破棄しない。
    /// </summary>
    /// <remarks>
    /// Proto v0.8.0 は複数のコレクションに「⚠ **null で届き得る**」と明記している。
    /// これを「必須フィールドの欠落」と見なすとメッセージが丸ごと捨てられ、
    /// 例えば足切り直後の `RankingSnapshot` を1本落として観戦画面の順位が固まる。
    /// 実際の空リストへの正規化は `Dispatcher.OrEmpty` が行う
    /// （pureC#/docs/.sdd/contract/01-proto-v0.8.0-migration.md §5）。
    ///
    /// 一律に「参照型は任意」とはしない。`RankingEntry.storeId` のような識別子は
    /// 空で届いた時点で不正であり、破棄されるべきだから（05-dispatcher.md のテスト参照）。
    /// </remarks>
    private static readonly System.Collections.Generic.HashSet<string> OptionalFields = new()
    {
        "Takoda99.Proto.RankingSnapshot.entries",
        "Takoda99.Proto.RankingDelta.entries",
        "Takoda99.Proto.StoreEliminatedBatch.entries",
        "Takoda99.Proto.ForcedEliminationWarning.cutStoreIds",
        "Takoda99.Proto.GameParametersPublicSubset.cullSchedule",
        // 個人成績の統計。null で届いたら Decode 側で new MatchStats() へ正規化する
        // （result/01-personal-result.md §3.1 エッジケース）。
        "Takoda99.Proto.PersonalResult.stats",
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
            var isOptional = IsOptional(type, jsonName);

            if (!hasProperty)
            {
                // 数値・bool・enum は欠落時に既定値(0/false/先頭メンバー)へフォールバックしても実害がない
                // ため、必須チェックの対象は「値が来ないと意味を成さない」参照型（string・入れ子DTO・配列）
                // に限定する（Delta のような補助フィールドの省略まで null 扱いにしないため）。
                if (RequiresPresence(prop.PropertyType) && !isOptional)
                {
                    return false;
                }

                continue;
            }

            // 明示的な null は「欠落」と同じ扱い（任意フィールドなら通す）。
            if (value.ValueKind == JsonValueKind.Null)
            {
                if (RequiresPresence(prop.PropertyType) && !isOptional)
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

    private static bool IsOptional(Type type, string jsonName)
    {
        return OptionalFields.Contains($"{type.FullName}.{jsonName}");
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
