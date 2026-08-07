# Takoda99-Unity

**たこ焼き店経営 × 日本語タイピング対戦**（最大99人）の Unity クライアント実装。

サーバー権威型のリアルタイム対戦を、**ロジックの大半を Unity の外に出す**構成で作っている。Unity エディタを起動せずに `dotnet test` だけで **208 件の単体テストが 0.2 秒で回る**。

```bash
dotnet test "pureC#/Takoda99.Client.slnx"   # 156 passed
dotnet test "Unity/tests/Takoda99.View.Tests/Takoda99.View.Tests.csproj"  # 52 passed
```

---

## 技術的なポイント

### 1. Unity 依存を境界の外に押し出した MVU アーキテクチャ

ゲームロジックは `pureC#/` 側の **`netstandard2.1` クラスライブラリ**として実装し、Unity へは **DLL 参照**で持ち込む。`Unity/Assets/` 側に残るのは「描画」「入力の正規化」「WebSocket の実体」だけ。

```
              ┌─────────────── pureC#/ （UnityEngine 参照ゼロ）────────────────┐
  WebSocket   │                                                               │
  ──raw JSON──▶ Dispatcher ──Action──▶ Store / Reducer ──state──┐             │
              │      │                （ClientState の唯一の保持者）│           │
              │      │                                            │           │
              │  SendQueue ◀──OrderServed──── TypingJudge ◀──char──┼───────────┼── UnityInputSource
              │      │                       （唯一のローカル判定）  │           │
              └──────┼────────────────────────────────────────────┼───────────┘
                     ▼                                            ▼
                 NetworkClient                                 IRenderer
                （WebGLNetworkClient）                        （Renderer / View 群）
```

- **状態は単一方向**。`Store` が `ClientState` を持ち、View は購読して描くだけ。View から状態を書き換える経路が存在しない。
- **`IRenderer` / `INetworkClient` / `IInputSource` / `IClock` はすべてインターフェース**。統括層 `MatchClientController` は Unity の型を一切知らない。
- 結果として、対戦ロジック全体を **xUnit で Fake 実装に差し替えてテストできる**（`FakeRenderer` / `FakeNetworkClient` / `FakeInputSource` / `FakeClock`）。

### 2. サーバー権威 — クライアントが計算してよいのは打鍵判定だけ

客の分配・評価・信用・脱落判定・お題生成はすべてサーバーが決める。クライアントは受信した state を描くだけで、**勝敗に関わる数値を自前で算出しない**。我慢ゲージ（`PatienceTimer`）も表示専用のカウントダウンで、離脱の確定はサーバーの `CustomerLeft` を待つ。

この境界は口約束ではなく [docs/rules/01-責務と絶対原則.md](./docs/rules/01-責務と絶対原則.md) に明文化し、CI で機械的に検査している（後述）。

### 3. 日本語タイピング判定オートマトン

唯一クライアントに置いたドメインロジック。かな列を「受理可能なローマ字パターンの集合」に分解する `IRomajiTable` と、打鍵ごとに前方一致で状態遷移する `TypingJudge` に分けている。

- **複数入力を許容**：`し` → `si` / `shi` / `ci`、`ふ` → `fu` / `hu` のように、同じかなを複数の打ち方で受理する。
- **促音「っ」の文脈解決**：後続の子音を見て `tta` / `xtu` / `ltu` を動的に生成する。
- **撥音「ん」の文脈解決**：次が母音・な行・「ん」なら `n` 単独を不許可にし、`nn` / `xn` のみ受理する（`na` と `んあ` を取り違えない）。
- 分解結果は `KanaUnit { Kana, Patterns }` として持ち、**元のかな表記も保持する**ためハイライト幅をローマ字ではなくかな単位で描ける。

判定器は入力に対して純粋な状態機械なので、`TypingJudgeTests` / `DefaultRomajiTableTests` でエッジケースを網羅的に固定している。

### 4. 決定論的なシナリオ再生機（`ScenarioPlayer`）

`INetworkClient` / `IInputSource` / `IClock` を**1クラスで同時に実装**し、「何 ms 時点でサーバーからこのメッセージが来て、何 ms 時点でこのキーが押される」というシナリオを JSON で流し込む。サーバーに接続せずに、対戦1試合ぶんの挙動を再現できる。

- `AdvanceTo(ms)` で仮想時刻を進める。**実時間の待機がないためテストがミリ秒で終わる**。
- `WallClockUnixMs` は固定基準時刻を返す。`clientTimestamp` が毎回変わってテストが不安定になるのを防ぐ。
- クライアントが送信した C2S メッセージを `Sent` に記録するので、「この入力列ならこの `OrderServed` が飛ぶ」まで検証できる。

### 5. 壊れた入力でクライアントを落とさない契約レイヤ

サーバーとクライアントのデプロイタイミングはずれる。前提として**未知の値が来る**設計にしている。

- **`EnumFallbackSanitizer`**：`System.Text.Json` は未知の enum 文字列でデシリアライズ全体を投げる。DTO をリフレクションで走査し（ネストした DTO・配列も再帰）、定義に無い enum 値だけを既定値へ書き換えてから通す。**サーバーが enum を1つ増やしただけでクライアントが全断する事故を潰している。**
- **`RequiredFieldValidator`**：必須フィールド欠落は握り潰さず、そのメッセージだけを捨てる。
- **`SendQueue`**：切断中の送信をキューに退避し、再接続時に flush。ただしメッセージ種別ごとに扱いを変える。`OrderServed` は**遅れて届いても意味がないので破棄**、`MatchmakingJoin` / `MatchmakingLeave` は**最新の意思だけ残す**（キュー内の古い同種を除去）。容量上限を超えたら古いものから落とす。

### 6. WebGL 制約と正面から向き合う

ブラウザで動かす前提のため `System.Net.WebSockets` は使えず、`Thread` と一部 `Task` にも制約がかかる。

- WebSocket は **NativeWebSocket**（jslib 経由）で実装し、非同期は `await` に頼らず**コールバック＋更新ループ駆動**で書いている。
- 接続先 URL はコードに直書きせず、ビルド設定側から注入する。WebGL では `config.json` を読まずに URL を検証する経路を用意している。

### 7. CI で「設計ルールそのもの」を検証する

Unity ライセンス不要の検証だけで **1〜2分**で回る。テストを通すだけでなく、**アーキテクチャの前提が崩れていないか**を機械的に見張っているのが要点。

| # | 検証内容 | 何を防ぐか |
|---|---|---|
| 1-2 | pureC# / View 派生状態の単体テスト | 通常のリグレッション |
| 3 | **`LangVersion 9` / `netstandard2.1` でのコンパイル確認** | テストは `net8.0` で通るため C# 10 構文に気づけない。**Unity でだけビルドが落ちる事故**を検知する |
| 4 | `pureC#/src` の `using UnityEngine` 混入検査 | 責務境界の侵食（条件付きコンパイルで型検査をすり抜ける経路も潰す） |
| 5 | **Proto 手ミラーと上流のバイト一致検査** | 「develop から未変更か」ではなく「上流と一致するか」を見るため、正規のバージョン追従は素通しつつ、**手による契約の改変を恒久的に検知できる** |

### 8. View 用の派生状態を純粋関数に切り出す

`Assets/Scripts/View/ValueObjects/`（`CustomerMoodState` / `CreditLifeLanternState` / `TakoyakiStandState` 等）は「state → 見た目の分類」を担う**純粋関数のみ**で、`UnityEngine` に依存しない。テストプロジェクトが同ソースを**コピーではなくリンク参照**するため、Unity エディタを起動せずに 52 件のテストで表示ロジックを固定できている。

### 9. 仕様書駆動開発（.sdd）

各モジュールは実装前に `docs/.sdd/` へ仕様書を書き、コードと食い違ったら**まず仕様書を直す**。実装コード中のコメントは `03-typing-judge.md §3.5` のように仕様書の節番号を指しており、「なぜこの分岐があるか」がコードから追跡できる。

---

## リポジトリ構成

| 領域 | 内容 | Unity依存 |
|---|---|---|
| [`pureC#/`](./pureC%23/README.md) | `Contract` / `Dispatcher` / `Store`+`Reducer` / `TypingJudge` / `RomajiTable` / `MatchClientController` / `ScenarioPlayer` | **なし（CI で強制）** |
| [`Unity/`](./Unity/README.md) | 描画（Prefab/UI/シーン）・Input System・`WebGLNetworkClient`・`PatienceTimer`・デバッグパネル | あり |

## セットアップ

```bash
dotnet test "pureC#/Takoda99.Client.slnx"
```

`Takoda99.Client.dll` が `Unity/Assets/Plugins/Takoda99/` へ自動コピーされる（DLL は `.gitignore` 対象。リビルドのたびにバイト列が変わり作業ツリーが汚れるため）。以降は Unity エディタで `Unity/` を開く。

## ドキュメント

- [AGENTS.md](./AGENTS.md) — AIコーディングエージェント向けの入口（ルールの索引）
- [docs/rules/](./docs/rules/README.md) — 責務・Git運用・PRレビューのルール本体
- 上流の正典：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto)（契約） / [Takoda99-Client-Docs](https://github.com/Okashimachi/Takoda99-Client-Docs)（クライアント設計） / [Takoda99-Docs](https://github.com/Okashimachi/Takoda99-Docs)（企画・ゲーム仕様）
