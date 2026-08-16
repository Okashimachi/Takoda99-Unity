// 仕様書: Unity/docs/.sdd/ 直下に対応する仕様書は無い（エディタ専用の作業支援ツール）。
// FontTheme（Assets/Resources/FontTheme.asset）を、シーン／Prefab の全 TMP_Text へ一括適用する。
//
// 実行時の挙動は一切変えない。ThemedText を付けて _theme を挿すだけで、
// 実際のフォント差し替えは ThemedText.Apply（[ExecuteAlways]）が行う。

using System.Collections.Generic;
using System.Linq;
using TMPro;
using Takoda99.View.Typography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Takoda99.EditorTools
{
    /// <summary>
    /// 日本語対応フォントを <see cref="FontTheme"/> 経由で全テキストへ適用するエディタツール。
    /// フォント本体を各テキストに直接挿さないのは、差し替えを SO 1つに集約しておくため。
    /// </summary>
    public static class FontThemeApplier
    {
        private const string MenuRoot = "Takoda99/フォント/";

        [MenuItem(MenuRoot + "開いているシーンの全テキストへ適用")]
        private static void ApplyToOpenScene()
        {
            var theme = FindTheme();
            if (theme == null)
            {
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("シーンが開かれていません。");
                return;
            }

            var added = 0;
            var rewired = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                // 非アクティブのテキスト（EffectRoot 配下など）も対象にするため true を渡す。
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    Apply(text, theme, ref added, ref rewired, useUndo: true);
                }
            }

            if (added > 0 || rewired > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log(
                $"[FontTheme] シーン '{scene.name}': ThemedText を {added} 件追加、" +
                $"{rewired} 件のテーマ参照を張り直しました。**シーンの保存を忘れずに。**");
        }

        [MenuItem(MenuRoot + "選択中の Prefab へ適用")]
        private static void ApplyToSelectedPrefabs()
        {
            var theme = FindTheme();
            if (theme == null)
            {
                return;
            }

            var paths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && p.EndsWith(".prefab"))
                .Distinct()
                .ToList();

            if (paths.Count == 0)
            {
                Debug.LogWarning("Prefab が選択されていません。Project ウィンドウで .prefab を選んでから実行してください。");
                return;
            }

            foreach (var path in paths)
            {
                // Prefab は開かずに中身だけ読み込んで書き戻す（シーン上のインスタンスに副作用を出さない）。
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var added = 0;
                    var rewired = 0;
                    foreach (var text in contents.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Apply(text, theme, ref added, ref rewired, useUndo: false);
                    }

                    if (added > 0 || rewired > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }

                    Debug.Log($"[FontTheme] {path}: {added} 件追加、{rewired} 件張り直し。");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem(MenuRoot + "ランキング行の Prefab へ適用")]
        private static void ApplyToRankingRowPrefabs()
        {
            var theme = FindTheme();
            if (theme == null)
            {
                return;
            }

            // 行 Prefab は実行時に Instantiate されるため、シーンを走査しても捕まらない。
            // 名前で探さずGUID検索にしておくと、Prefab を移動しても壊れない。
            var paths = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("TopRanker.prefab") || p.EndsWith("BottomRanker.prefab"))
                .ToList();

            if (paths.Count == 0)
            {
                Debug.LogWarning("TopRanker.prefab / BottomRanker.prefab が見つかりませんでした。");
                return;
            }

            foreach (var path in paths)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var added = 0;
                    var rewired = 0;
                    foreach (var text in contents.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Apply(text, theme, ref added, ref rewired, useUndo: false);
                    }

                    if (added > 0 || rewired > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }

                    Debug.Log($"[FontTheme] {path}: {added} 件追加、{rewired} 件張り直し。");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem(MenuRoot + "未適用のテキストを一覧表示")]
        private static void ListUnthemed()
        {
            var scene = SceneManager.GetActiveScene();
            var missing = new List<string>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.GetComponent<ThemedText>() == null)
                    {
                        missing.Add(PathOf(text.transform));
                    }
                }
            }

            if (missing.Count == 0)
            {
                Debug.Log("[FontTheme] 未適用のテキストはありません。");
                return;
            }

            Debug.LogWarning($"[FontTheme] 未適用 {missing.Count} 件:\n  " + string.Join("\n  ", missing));
        }

        private static void Apply(TMP_Text text, FontTheme theme, ref int added, ref int rewired, bool useUndo)
        {
            if (text == null)
            {
                return;
            }

            var themed = text.GetComponent<ThemedText>();
            if (themed == null)
            {
                themed = useUndo
                    ? Undo.AddComponent<ThemedText>(text.gameObject)
                    : text.gameObject.AddComponent<ThemedText>();
                added++;
            }

            // _theme は private [SerializeField] なので SerializedObject 経由で挿す。
            var so = new SerializedObject(themed);
            var prop = so.FindProperty("_theme");
            if (prop == null)
            {
                Debug.LogError($"ThemedText._theme が見つかりません: {PathOf(text.transform)}");
                return;
            }

            if (prop.objectReferenceValue != theme)
            {
                if (useUndo)
                {
                    Undo.RecordObject(themed, "Apply FontTheme");
                }

                prop.objectReferenceValue = theme;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(themed);
                rewired++;
            }
        }

        /// <summary>Resources/FontTheme.asset を優先し、無ければプロジェクト内から1つ探す。</summary>
        private static FontTheme FindTheme()
        {
            var theme = Resources.Load<FontTheme>("FontTheme");
            if (theme != null)
            {
                return theme;
            }

            var guids = AssetDatabase.FindAssets("t:FontTheme");
            if (guids.Length == 0)
            {
                Debug.LogError(
                    "FontTheme アセットが見つかりません。" +
                    "Create > Takoda99 > Font Theme で作成し、Light / Normal / Bold に " +
                    "NotoSansJP の TMP FontAsset を設定してください。");
                return null;
            }

            theme = AssetDatabase.LoadAssetAtPath<FontTheme>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (guids.Length > 1)
            {
                Debug.LogWarning($"FontTheme が複数あります。{AssetDatabase.GUIDToAssetPath(guids[0])} を使います。");
            }

            return theme;
        }

        private static string PathOf(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
