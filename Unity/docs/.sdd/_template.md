# {番号}-{モジュール名}

> 参照する上流：[Takoda99-Client-Docs {章番号}]() / [Takoda99-Docs 03_Unity仕様書 {節}]() / [Takoda99-Proto]() の該当メッセージ・型。矛盾したら上流優先。

## 1. 責務

- このモジュールが**する**こと（箇条書き）
- このモジュールが**しない**こと（箇条書き。[docs/rules/01-責務と絶対原則.md](../../../docs/rules/01-責務と絶対原則.md) の「持たないもの」と矛盾しないこと）

## 2. 公開インターフェース

```csharp
// C# の具体的なシグネチャで書く。疑似コードで終わらせない。
// pureC# 側のインターフェースを実装する場合は、どのIFのどのメンバに対応するかを明記する。
public sealed class Example : MonoBehaviour
{
}
```

## 3. Unity構成

- **MonoBehaviour のライフサイクル**：`Awake` / `Start` / `Update` / `OnDestroy` で何をするか
- **シーン・Prefab**：必要なGameObject構成・Prefab・UI要素
- **Inspector 公開値**：外部から差し替える設定（接続先URLは直書きせずここで持つ等）
- **使用するUnityパッケージ**：Input System / NativeWebSocket 等

## 4. ふるまいの詳細

- 正常系の処理の流れ
- エッジケース（空入力・境界値・重複呼び出し・フレーム跨ぎ等）
- エラー時の挙動（例外を投げるか、既定値を返すか）
- WebGL固有の注意点（Thread/Task制約に触れる場合）

## 5. 依存関係

- 依存する `pureC#` モジュール：
- 依存するUnity側モジュール：
- 依存されるモジュール：
- （[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) の依存方向図と矛盾しないこと。特に「`Renderer`/`InputSource` に依存してよいモジュールは無い」）

## 6. テスト・確認観点

- 仕様書の「4. ふるまいの詳細」に対応する確認方法（Unity Test Runner / エディタ実行 / WebGLビルドでの確認）

## 7. 未確定事項

- 実装しながら決めることになりそうな点
