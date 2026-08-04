# 07-pureC# の参照方法（DLL連携）

> 参照する上流：[Takoda99-Docs 03_Unity仕様書](https://github.com/Okashimachi/Takoda99-Docs/blob/main/04_クライアント仕様/03_Unity仕様書.md)（WebGL/IL2CPP制約）/ [docs/rules/02-Unity実装ルール.md](../../../docs/rules/02-Unity実装ルール.md)（`Unity/` と `pureC#/` の役割分担）。矛盾したら上流優先。

`pureC#/` で実装した Unity非依存ロジックを、Unity 側から使えるようにするための連携方法。

## 1. 責務

**する**こと：

- `pureC#` のビルド成果物（DLL）を Unity が認識できる場所へ配置する
- ビルドと同時に自動でコピーし、**古いDLLのまま動く事故を構造的に防ぐ**
- `System.Text.Json` 依存の解決方法を定める

**しない**こと：

- `pureC#` 側のロジックをUnity向けに書き換えない（DLLはそのまま使う）
- Unity側で `pureC#` のソースを複製しない（二重管理を作らない）

## 2. 方式の決定：DLL参照

`pureC#` を **DLL としてビルドし、`Unity/Assets/Plugins/Takoda99/` に配置する**。ソースをUnity側へ取り込む方式は採らない。

### 決定理由

| 観点 | 判断 |
|---|---|
| **`pureC#` の純粋性** | DLL方式なら `pureC#` 側に Unity 用の設定ファイル（`package.json` / `asmdef`）を一切置かずに済む。領域分けが構造的に保たれる |
| **Unity非依存の担保** | DLLは Unity のコンパイル対象外なので、`using UnityEngine` を書く余地が構造的に無い。設定やレビューに頼らない |
| **ビルド時間** | 実測 **1秒前後**（30ファイル・2,511行）。障害にならない |
| **コピー忘れ** | ビルド時の自動コピーで解消（§3） |

ソース取り込み方式は、Unity が `Assets/` 配下しかコンパイルできない都合上、`pureC#` を Unity のローカルパッケージにする必要があり、`pureC#` 側に Unity 固有の設定が混入する。領域を明確に分ける本リポジトリの方針と噛み合わないため採用しない。

## 3. ビルドとコピーの自動化

`pureC#/src/Takoda99.Client/Takoda99.Client.csproj` にコピー用のターゲットを持たせる。**別途スクリプトを用意したり、手順を人が覚えたりする必要は無い。**

```xml
<Target Name="CopyToUnity" AfterTargets="Build">
  <Copy SourceFiles="$(TargetPath)"
        DestinationFolder="$(MSBuildProjectDirectory)\..\..\..\Unity\Assets\Plugins\Takoda99"
        SkipUnchangedFiles="true" />
</Target>
```

### この方式の要点

- **`dotnet test` を実行するだけでもコピーされる。** テストプロジェクトは `Takoda99.Client` を参照しているため、テスト実行時に本体がビルドされ、コピーまで走る。**普段どおりテストを回しているだけでUnity側は最新に保たれる**
- `SkipUnchangedFiles="true"` により、変更が無ければ書き込まない（Unityの不要な再インポートを避ける）
- `.pdb`（デバッグシンボル）はローカルでは配置してよいが、`.gitignore` の `*.pdb` によりコミットされない。Unityから `pureC#` のコードへステップインしたい場合のみローカルに置く

### コピーは Debug 構成に限定する

コピーのターゲットには `Condition="'$(Configuration)' == 'Debug'"` を付ける。**Release ビルドではコピーしない。**

限定しない場合、同じファイルに対して構成の異なるバイナリが交互に書き込まれ、**`dotnet test` を実行しただけで作業ツリーが汚れる**（`dotnet test` は Debug、`dotnet build -c Release` は Release でビルドするため）。DLLはバイナリなので、差分が出ても人が読めず、コンフリクトしても手で解決できない。

Debug を採用する理由は、**最も高頻度で実行される `dotnet test` がそのまま最新化を兼ねる**ため。Release ビルドは稀なので、そちらを除外する方が事故が少ない。

WebGLビルドでは IL2CPP が managed アセンブリをC++へ再変換するため、**managed側の Debug/Release の差は最終成果物にほとんど影響しない**。リリースビルドのために構成を切り替える必要はない。

## 4. 配置するファイル

```
Unity/Assets/Plugins/Takoda99/
  Takoda99.Client.dll    # 自動コピー（ビルドのたびに更新）
```

**配置するのはこの1つだけ。第三者DLLは置かない。**

### 第三者DLLを置かない理由（実機確認済み）

Proto の `Messages.cs` は DTO の定義そのものに `[JsonPropertyName]` / `[JsonConverter]` を持つため `System.Text.Json` が必須になるが、**Unity 6 はこれを標準搭載している**。

```
<Unityインストール先>/Editor/Data/BCLExtensions/
  TargetingPacks/netstandard2.1/ref/   # コンパイル時の参照アセンブリ
  runtime/netstandard2.1/              # 実行時の実装アセンブリ
    System.Text.Json.dll / System.Text.Encodings.Web.dll / Microsoft.Bcl.AsyncInterfaces.dll
```

ref と runtime の両方が揃っているため、こちらで配置する必要はない。実際、`Assets/Plugins/` へ置いても**コンパイラには渡らない**ことを `Unity/Logs/Editor.log` で確認済み（`-r:` に現れるのは Unity 自身の TargetingPacks のみ）。

`dotnet publish` が出力する他の依存（`System.Buffers` / `System.Memory` / `System.Numerics.Vectors` / `System.Runtime.CompilerServices.Unsafe` / `System.Threading.Tasks.Extensions`）も .NET Standard 2.1 に含まれるため同様に置かない。

> 将来 Unity のバージョンを上げ下げした際は、`BCLExtensions/runtime/` の有無を再確認すること。無くなった場合のみ、必要なDLLを `Assets/Plugins/Takoda99/` へ手動配置する。

## 5. Git 管理の方針

**DLLはリポジトリにコミットする。**

| | 採用（コミットする） | 不採用（`.gitignore`） |
|---|---|---|
| Unityだけを触る人 | **クローンしてUnityを開けば動く** | 先に `dotnet build` が必要 |
| リポジトリ | バイナリが増える（合計約 770 KB） | きれい |

Unity側の作業者が `pureC#` のビルド環境や、領域分割の前提を必ずしも把握していない状況を優先する。「Unityを開いたら動かない」状態を作らないことを重視した判断。

`.meta` ファイルも同様にコミットする（Unityが生成する。§7）。

## 6. Unity側の設定

- 配置先は `Assets/Plugins/Takoda99/`。`Plugins/` 配下はUnityが managed plugin として自動認識する
- Unity側のスクリプトからは `using Takoda99.Client;` で参照できる。Assembly Definition（`asmdef`）は必須ではない
- API Compatibility Level は **.NET Standard 2.1**（`pureC#` の `TargetFramework` と一致させる）。確認済み（`ProjectSettings.asset` の `apiCompatibilityLevel: 6`）

### `Assets/` 配下で使えるC#の機能は C# 9 まで

**Unity（6000.5 時点）のコンパイラは C# 9 まで**しか受け付けない。`pureC#` 側は `LangVersion 10` でビルドしているため、両者で書ける構文が異なる。

`Assets/` 配下では以下が**使えない**。

| 機能 | エラー |
|---|---|
| `record` / `record struct`（C# 10） | `CS8773` |
| `init` アクセサ・`with` 式 | `CS0518`（`IsExternalInit` が無い） |

`View/ValueObjects` はこの制約により `readonly struct` ＋ 明示コンストラクタで書いている。**「`record` にすれば短くなる」と直すとUnityでコンパイルが通らなくなる**ため、各ファイル冒頭に注記を置いている。

DLL側（`Takoda99.Client`）が `record struct` を公開している分には問題ない。**宣言できないだけで、参照・利用はできる**。

## 7. 更新手順・確認方法

### 日常の更新

```bash
dotnet test "pureC#/Takoda99.Client.slnx"
```

これだけでよい。テストが通り、かつ Unity 側のDLLが最新になる。

### DLLが変更扱いになったときの判断

同じソースからでも、**ビルドの入口が違うとDLLのバイト列が変わる**（`dotnet test` 経由と、`Takoda99.Client.csproj` を直接 `dotnet build` した場合で異なる。それぞれの中では再現性がある）。中身の挙動は同じで、差は MVID 等のメタデータ。

そのため、`git status` に `Takoda99.Client.dll` が出ても**必ずしもソースが変わったとは限らない**。

- **`pureC#/src` を変更した** → DLLの更新も一緒にコミットする
- **`pureC#/src` を変更していない**（テストを流しただけ等） → `git checkout -- <DLLのパス>` で戻してよい

余計な差分を避けるため、**DLLの更新は `dotnet test`（正典コマンド）の出力で揃える**。個別プロジェクトの `dotnet build` 結果はコミットしない。

> この煩わしさは、ビルド成果物をコミットすることの代償。「Unityだけを触る人がクローン直後に動かせる」ことを優先した判断（§5）とのトレードオフであり、churn が問題になるようなら DLL を `.gitignore` して「Unityを開く前に `dotnet test` を1回実行する」運用へ切り替える。

### 初回セットアップ（完了済み）

`Takoda99.Client.dll` と `.meta` はリポジトリにコミット済みのため、**クローン後の追加作業は不要**。Unityを開けばそのまま参照できる。

### DLLが古くなっていないかの確認

タイムスタンプを比較する。

```bash
ls -la Unity/Assets/Plugins/Takoda99/Takoda99.Client.dll pureC#/src/Takoda99.Client/bin/Debug/netstandard2.1/Takoda99.Client.dll
```

自動コピーが効いていれば一致する。ズレていたらビルドが走っていない。

## 8. ふるまいの詳細（エッジケース）

| ケース | 挙動 |
|---|---|
| コピー先ディレクトリが無い | `Copy` タスクが自動生成する |
| Unityがエディタで実行中にDLLを更新 | Unityがファイルをロックし、コピーが失敗することがある。**Unityを停止してからビルドする** |
| `pureC#` に変更が無い | `SkipUnchangedFiles` により書き込まれない。Unityの再インポートも走らない |
| WebGLビルド | DLLは IL2CPP でAOTコンパイルされる。`System.Text.Json` はリフレクションを使うため、**コード削除（managed stripping）で必要な型が消える可能性がある**（§10） |

## 9. 依存関係

- 依存する `pureC#` モジュール：`Takoda99.Client` 全体（`01-contract` 〜 `06-match-client-controller`）
- 依存されるUnity側モジュール：`01-network-client` / `02-input-source` / `04-renderer` など、`pureC#` の型を使うすべて
- この仕様書は**ビルド連携のみ**を定める。各モジュールが `pureC#` の何をどう使うかは、それぞれの仕様書に書く

## 10. 未確定事項

- **WebGL/IL2CPP でのコード削除対策が未検証。** `System.Text.Json` はリフレクションでDTOを解決するため、`link.xml` で `Takoda99.Proto` の型を保護する必要があるかもしれない。WebGLビルドを最初に通すときに確認する
- Unity側から `pureC#` のコードへステップイン（デバッグ）する手順。`.pdb` はコミットしない方針のため、必要な人がローカルで配置する運用でよいか
