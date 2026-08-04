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

### 配置されるのは「最後にビルドした構成」のDLL

`dotnet test` は Debug 構成でビルドするため、テストを回した直後は **Debug ビルドのDLL**が配置される。`dotnet build -c Release` を実行すれば Release に置き換わる。

開発中はどちらでも動作するが（WebGLビルドでは IL2CPP がC++へ再変換するため、managed側の最適化差は最終成果物にほとんど影響しない）、**リリース用のWebGLビルドを作る前には Release 構成でビルドし直す**こと。

## 4. 配置するファイル

```
Unity/Assets/Plugins/Takoda99/
  Takoda99.Client.dll              # 自動コピー（ビルドのたびに更新）
  Microsoft.Bcl.AsyncInterfaces.dll  # 手動配置（1回だけ）
  System.Text.Encodings.Web.dll      # 手動配置（1回だけ）
  System.Text.Json.dll               # 手動配置（1回だけ）
```

### 自動コピーの対象は `Takoda99.Client.dll` のみ

第三者DLLはバージョンが変わらない限り更新不要なため、自動コピーの対象にしない。毎回上書きすると、Unity側で手当てした設定（Platform settings 等）が失われる可能性がある。

### `System.Text.Json` が必要な理由

Proto の `Messages.cs` が DTO の定義そのものに `[JsonPropertyName]` / `[JsonConverter]` を持つため、**この属性の型が無いとコンパイルできない**。こちらの都合では外せない（契約の変更は Proto 側の作業）。

### Unity が標準で持つため配置しないDLL

`dotnet publish` は以下も出力するが、**これらは .NET Standard 2.1 に含まれ Unity が標準で提供する**。配置すると型の重複でコンパイルエラーになるため、置かない。

- `System.Buffers.dll`
- `System.Memory.dll`
- `System.Numerics.Vectors.dll`
- `System.Runtime.CompilerServices.Unsafe.dll`
- `System.Threading.Tasks.Extensions.dll`

> **要検証**：この切り分けは .NET Standard 2.1 のAPI表面に基づく想定であり、Unityエディタでの実機確認をまだ行っていない。型が解決できないエラーが出た場合は、該当DLLのみ追加配置する。逆に重複エラーが出た場合は、配置済みのDLLを削る。**初回のUnity起動時に必ず確認すること**（§7）。

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
- API Compatibility Level は **.NET Standard 2.1**（`pureC#` の `TargetFramework` と一致させる）

## 7. 更新手順・確認方法

### 日常の更新

```bash
dotnet test "pureC#/Takoda99.Client.slnx"
```

これだけでよい。テストが通り、かつ Unity 側のDLLが最新になる。

### 初回セットアップ時の確認（1回だけ）

1. `dotnet publish -c Release` で第三者DLLを取得し、§4 の3つを `Assets/Plugins/Takoda99/` へ配置する
2. Unityエディタを開く。`.meta` ファイルが生成される
3. **Consoleにコンパイルエラーが出ていないことを確認する**（§4 の「要検証」参照）
4. 生成された `.meta` ファイルをコミットする

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

- **§4 の第三者DLLの切り分けが実機未検証。** 初回のUnity起動時に確認し、結果をこの仕様書へ反映する
- **WebGL/IL2CPP でのコード削除対策が未検証。** `System.Text.Json` はリフレクションでDTOを解決するため、`link.xml` で `Takoda99.Proto` の型を保護する必要があるかもしれない。WebGLビルドを最初に通すときに確認する
- Unity側から `pureC#` のコードへステップイン（デバッグ）する手順。`.pdb` はコミットしない方針のため、必要な人がローカルで配置する運用でよいか
