# 02-足切り秒読みパネル（常設UI）

> 参照する上流：[本選企画書 3.3・3.6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／[12_差分_クライアント §5.1](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[30_通信シーケンス 3-B](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)／`pureC#` [match-state/03-cull-warning.md](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)。矛盾したら上流優先。

**予選の「予告時だけ出るポップアップ」を、常設UIへ格上げする。** 20秒等間隔なので、秒読みは常に 0〜20 の範囲に収まり、数字の意味が直感的になる。

## 1. 責務

**する**

- 次の足切りまでの残り秒を、**毎フレーム、ローカル補間して**描く
- 次に脱落する予定の店の名前を並べる
- 自分が対象圏内（`SelfAtRisk`）なら強く警告する

**しない**

- 残り時間を `ClientState` へ書き戻さない（毎フレームの `Store` 通知を作らない）
- `Rank` と `CutLineRank` を比較して自分が危険かを判定しない（**`SelfAtRisk` がサーバーから届く**）
- 秒読みが0になったことで脱落させない（脱落の確定は `StoreEliminatedBatch` の到着）

## 2. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/CullCountdownPanelView.cs
namespace Takoda99.View.Ranking
{
    public sealed class CullCountdownPanelView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;   // 01 と同じ行Prefabを再利用
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private int maxCutRows = 5;

        /// <summary>受信値の差し替え。Renderer が state 変化のたびに呼ぶ。</summary>
        public void SetWarning(CullWarning? warning, ClientState state);

        /// <summary>受信の瞬間だけ必要な演出の契機（IRenderer.OnCullWarning から）。</summary>
        public void OnWarningReceived(CullWarning warning);

        // 秒読みの数字は Update() で毎フレーム更新する（§3）
    }

    /// <summary>残り5秒の中央カウントダウン（§6）。EffectCanvas/CountDown にアタッチする。</summary>
    public sealed class CullFinalCountdownView : MonoBehaviour
    {
        /// <summary>毎フレーム CullCountdownPanelView から押し込まれる。自分では時計を持たない。</summary>
        public void SetState(CullAlertTier tier, long remainingMs);
    }
}
```

## 3. ★秒読みのローカル補間

```
受信時   : cull.UntilMs, cull.ReceivedAtLocalMs（Dispatcher が入れたローカル単調時刻）
毎フレーム: 残りms = cull.RemainingMsAt(nowLocalMs)
           表示秒 = ceil(残りms / 1000)
次の受信で cull ごと差し替わる
```

`nowLocalMs` は `Renderer` と同じ基準を使う：

```csharp
var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);
```

> **`Time.time` を使わない。** タイムスケールの影響を受けるため。既存 `Renderer.HandleStateChangedCore` と同じ式に揃える。

| # | 規則 |
|---|---|
| C1 | **`Update()` で数字だけ更新する。** `ClientState` を触らない |
| C2 | 表示秒が変わったフレームだけ `TMP.text` に代入する（毎フレームの `ToString` を避ける） |
| C3 | 新しい `CullWarning` が来たら**即座に上書き**する。補間中の値を優先しない（サーバー値が常に正） |
| C4 | 0 に達したら 0 のまま止める。**負数を出さない**（`RemainingMsAt` が `Math.Max(0, …)` で吸収済み） |
| C5 | `warning == null`（未受信）の間はパネルを非表示にする。**0秒と区別する** |
| C6 | 定期更新が途絶えても、最後の値から補間を続ける（[30_通信シーケンス §8](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)） |

## 4. 表示要素

| 要素 | 供給元 | 表示 |
|---|---|---|
| 残り秒 | `RemainingMsAt` の補間 | 大きく。0〜20 の範囲 |
| 段階 | `cull.StageIndex` / `cull.StageTotal` | 「3 / 6」 |
| カットライン | `cull.CutLineRank` | 「◯位以下が脱落」 |
| 脱落予定の店 | `cull.CutStoreIds` | 名前を最大 `maxCutRows` 件。`state.DisplayNames` で解決。**現行レイアウトでは出さない**（§4.2） |
| 自分が対象か | `cull.SelfAtRisk` | **画面全体アラート**（§5） |

### 4.0 ★`rowsRoot` はこのパネル専用の親にする（一度事故った）

行は `RankingRowPool` が `rowsRoot` の下に **`Instantiate` で生やす**。
したがって `rowsRoot` に**他のパネルの `RowsRoot` を割り当てると、そちらのパネルの中に行が湧く。**

> **実際に起きたこと：** `CullPanel` の `rowsRoot` に**下位パネル（`BottomRankers/RowsRoot`）**が
> 割り当てられていた。結果、脱落予定の行が下位30行のパネルの**中央に重なって出た**。
>
> 1行に見えたのは、`ApplyCutRows` が `SetSiblingIndex` しか呼んでおらず（親に `LayoutGroup` が無いため）
> **5行すべてが行Prefabの authored 位置＝親の中央にぴたりと重なっていた**から。
> 名前だけの行（`SetNameOnly` で順位・スコアは空）なので、下位パネルの行とも見た目が違っていた。

対策を2つ入れてある。

| 対策 | 内容 |
|---|---|
| 位置を自分で決める | `ApplyCutRows` が `anchoredPosition = (0, -i × cutRowHeight)` を必ず入れる。`LayoutGroup` に依存しない |
| 共有を検知する | `Awake` で `rowsRoot.childCount > 0` なら警告を出す（専用の親は編集時には空のはず） |

### 4.2 現行レイアウトでは行リストを出さない（`rowsRoot` 未割り当て）

`CullPanel` は 525×30 の帯（`InfoPanel`：カットライン・残り秒・段階）だけで、**行を並べる場所が無い。**
`rowsRoot` は**未割り当て**にしてあり、その場合 `RankingRowPool.Acquire` が `null` を返すので行は1つも作られない。

**脱落確定の店は下位パネルが `Doomed` の帯で示している**（[../value-objects/12 §4.2](../value-objects/12-ranking-row-style.md)）ので、
同じ情報を2か所に出す必要が無い。出すと決めたら**このパネル専用の空の親**を作って割り当てる（§4.0）。

### 4.1 名前の解決

```csharp
private string ResolveName(ClientState state, string storeId)
    => state.DisplayNames.TryGetValue(storeId, out var n) && !string.IsNullOrEmpty(n)
        ? n
        : storeId;   // 解決できなければ storeId をそのまま出す（空欄にしない）
```

`cutStoreIds` の件数はサーバーが上限を切っているが、**それが `maxCutRows` より多い可能性がある**。多い場合は先頭 `maxCutRows` 件だけ描き、「他◯店」と添える。

## 5. `SelfAtRisk` の演出

警告は**画面端のビネット**で出す。中央は円形に素通しのまま残し、打鍵の視認性を落とさない。
段階は2つあり、判定は [../value-objects/13-cull-alert-state.md](../value-objects/13-cull-alert-state.md) の純関数が持つ。

| 段階 | 条件 | 色 | 強さ |
|---|---|---|---|
| `None` | 未受信／**脱落後**／残りが窓（10秒）より多い／下記どちらにも当たらない | — | 出さない |
| `Caution` | `SelfAtRisk == false` かつ**自店が下位パネルの表示範囲に入っている** | 淡い黄〜橙 | 軽く（最大 α 0.3） |
| `Danger` | `SelfAtRisk == true`（サーバー権威） | 赤 | 強め（最大 α 0.75） |

「ぎりぎり圏外」の根拠に**順位と `CutLineRank` の比較は使わない**（§1・[docs/rules/01](../../../../docs/rules/01-責務と絶対原則.md)）。
下位パネルの表示範囲に入っているかで決める。これは [../value-objects/12](../value-objects/12-ranking-row-style.md) §4.2 の
`AtRisk`（「今は圏外だが落ち得る人」）と同じ根拠であり、画面上でも下位パネルの警告帯と一致する。

範囲から外れたら `None` に落ちて**完全に消える**。逆に `Caution → Danger` へ上がることもある。
段階が切り替わるときは色と濃さを補間して繋ぐ（飛ばさない）。

### 5.1 ★明滅の速さは安全要件（緩めてはいけない）

**速い点滅は光過敏性発作（いわゆるポリゴンショック）を起こし得る。**
アラートは「点滅」ではなく、**ゆっくりしたフェードイン・フェードアウト**として実装する。

| 規則 | 値 |
|---|---|
| 明滅は正弦波（山も谷もなめらか。ハードな切り替えを作らない） | `0.5 + 0.5·sin(2π·f·t)` |
| 既定の周期 | `f = 0.33Hz`（約3秒で1往復） |
| **絶対上限** | `MaxPulseHz = 0.8Hz` でコード側から頭を押さえる |
| 危険度が上がっても**速くしない** | 変えるのは濃さだけ。速さは一定 |

一般に 3Hz 以上の点滅が危険とされる。上限 0.8Hz はそこから桁で余裕を取った値であり、
**演出上の理由で緩めない。** Inspector の `pulseHz` に大きな値を入れても `MaxPulseHz` で頭打ちになる。

`Danger` は谷でも消しきらない（下限 0.35 を残す）。完全に消えると「危険が去った」と誤読されるため。

### 5.2 実装の注意（一度ハマった）

> `AtRiskOverlay` の `Image.color` の alpha を 0 のまま置くと、`CanvasGroup.alpha` をいくら上げても
> 掛け算で常に 0 になり、**警告が画面に一切出ない**。
> `Image.color` は**常に alpha = 1** で持ち、可視・不可視は必ず `CanvasGroup.alpha` 側だけで制御する。

ビネットのスプライトは**実行時に生成する**（アート素材もシェーダーも使わない）。
半径は**画面の短辺の半分を 1.0** として測る。こうすると縦画面でも横画面でも、
テクスチャを引き伸ばしたときに中央の素通し部分が画面上で正しく「円」になる。
画面比率が変わったら作り直し、`OnDestroy` でテクスチャを破棄する（実行時生成物は自動回収されない）。

全画面を覆うため `Image.raycastTarget` は **0**（入力を食わせない）。

| 状態 | 演出 |
|---|---|
| `false → true` に変わった瞬間 | `OnWarningReceived` を契機に一度だけSE。**脱落後は鳴らさない** |
| `true → false` に変わった瞬間 | 警告を解除（「逃げ切った」ことが分かる） |

| 禁止 | 理由 |
|---|---|
| 打鍵の視認性を下げる警告 | 急がせるための警告が、急ぐ手段（打鍵）を妨げては本末転倒。**中央は必ず素通しにする** |
| 速い点滅・ストロボ | 光過敏性発作の危険（§5.1）。**例外なし** |
| 画面全体のベタ塗り | 上と同じ理由。ビネット（周辺だけ）に留める |
| 毎フレーム鳴るSE | `SelfAtRisk` は1〜2Hz で届き続ける。**状態が変わった瞬間だけ**鳴らす |
| 脱落後も演出を続ける | 観戦中に自分向けの警告が出ると混乱する。`selfAlive == false` で全部止める |

## 6. 残り5秒の中央カウントダウン（`EffectCanvas/CountDown`）

淘汰の直前だけ、画面中央に大きな数字を出す。**対象は §5 と同じ**（淘汰圏内＝`Danger`／ぎりぎり圏外＝`Caution`）。
安全圏の店には一切出さない。誰に出すかを**この演出が独自に決めることはしない**（`CullAlertTier` をそのまま受ける）。

| 項目 | 決め |
|---|---|
| 置き場所 | `EffectCanvas/CountDown`（`SortingOrder: 1000`）。盤面Canvasに置くと打鍵ごとの再描画に巻き込まれる |
| 出す条件 | `CullAlertTier != None` かつ 残り `5000ms` 以下 |
| 消す条件 | 残り 0ms（「0」を出したまま残さない）／段階が `None` に落ちた瞬間 |
| 数字 | 秒の切り上げ（`CullCountdownState` と同じ規則）。5 → 4 → 3 → 2 → 1 |
| 入力 | `CanvasGroup.blocksRaycasts = false`。最前面の全画面Canvasなので**絶対に入力を食わせない** |
| 初期状態 | **シーンでは非アクティブ**。窓に入ったフレームに `SetActive(true)`、消えるフレームに `SetActive(false)` |

### 6.1 出現アニメーション

数字が変わるたびに、**少し小さいところから等倍へ広がりながらフェードイン**し、次の数字へ移る前にフェードアウトする。

| 段階 | 既定値 | 式 |
|---|---|---|
| 拡大 | `0.6 → 1.0` を `0.28秒` | EaseOut（`1-(1-t)³`）。勢いよく広がって静かに止まる |
| フェードイン | `0.12秒` | `SmoothStep` |
| フェードアウト | 次の数字までの残り `0.3秒` | 同上（残り時間側から測るので、頭のフェードインを潰さない） |

**進行はすべて `CullFinalCountdownState.SecondProgress`（秒読みの進み具合）から引く。**
`Time.deltaTime` を自前で積まないため、フレームが落ちても数字と演出がずれない。

拡大は `CountDown` 自身の `localScale` に**係数として**掛ける（Inspector で組んだ大きさが `1.0`）。
子（縁取り用に 1.1 倍で重ねたテキスト等）の相対スケールはそのまま保たれる。

### 6.2 §5 の安全要件との関係

§5.1 の `MaxPulseHz`（0.8Hz）は**画面全体を覆うビネットの明滅**に対する上限であり、ここは対象外。
中央カウントダウンは 1秒に1回、画面のごく一部の要素がフェードイン・アウトするだけで、
画面全体の輝度を振らない。**ただしビネット側の上限を、この演出に合わせて緩めてはいけない。**

### 6.3 ★シーンでは非アクティブに置く（`Awake` が走らない前提で書く）

`EffectCanvas/CountDown` は**非アクティブで置いてある**。淘汰の窓に入ったときだけ現れるものを、
編集中ずっと画面中央に出したままにしないため。

このため **`Awake` は初回表示まで走らない**。参照の解決と初期値は `Awake` に置かず、
`SetState` の先頭から呼ぶ `EnsureInitialized()`（べき等）で行う。

| 制約 | 対応 |
|---|---|
| `Awake` が走らない | 初期化は `EnsureInitialized()` に置き、`Awake` からも `SetState` からも通す |
| `Update()` を持てない（非アクティブ中は回らない） | **駆動は外からの `SetState` だけ**。非アクティブな GameObject でもコンポーネントのメソッド呼び出しは届く |
| `localScale` を書き換えてから等倍を採ると縮んだまま出る | `baseScale` は**必ず初期化時に**採る（アニメーション適用前） |

> `CanvasGroup.alpha = 0` では TMP のメッシュが描かれ続けるため、出していない間は**根ごと**切る。

### 6.4 時計を2本にしない

残り時間を持つのは `CullCountdownPanelView` だけで、`CullFinalCountdownView` は
毎フレーム `SetState(tier, remainingMs)` で押し込まれる側に徹する。
**`CullWarning` を自分で購読しないこと**（同じ秒読みが2つの時計で微妙にずれる）。

## 7. 最終ステージ（120秒）の特別な見え方

最終ステージでは `CutLineRank == 2` が届く（Proto コメント参照）。処理上は1位も脱落するが、**表示は「1位以外が脱落対象」**とするのが企画意図。

```
決勝の10人 → 9人が脱落対象 → 生き残るのは1人だけ → 緊張が最大化する
```

**クライアントは `CutLineRank` をそのまま「◯位以下」として描けばよい。** 特別な分岐を書かない。

## 8. 待機中・脱落後の扱い

| 状況 | 扱い |
|---|---|
| `GameBeforeView` 保持中 | 描かない |
| `Cull == null` | パネル非表示 |
| 自店が脱落（`Spectating`） | **描き続ける。** 他店がいつ切られるかは観戦の見どころ。ただし予告そのものが届かなくなるため、**パネルは最後に受けた値で凍る**（[07-脱落後の淘汰予告の配信.md](../../../../docs/server-sync/07-脱落後の淘汰予告の配信.md)。サーバー担当と協議中で、クライアント側の回避実装は入れない） |
| `Phase == Result` | 畳む |

## 9. 依存関係

- 依存する：`pureC#` [match-state/03](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)、[../value-objects/09](../value-objects/09-cull-countdown-state.md)、[../value-objects/13](../value-objects/13-cull-alert-state.md)、[../value-objects/14](../value-objects/14-cull-final-countdown-state.md)、[01-ranking-panel.md](./01-ranking-panel.md)（`RankingRowView` の再利用）
- 依存される：なし

## 10. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | `UntilMs=20000` 受信の5秒後に「15」と出る | EditMode（`CullCountdownState` の純関数） |
| 2 | 経過が `UntilMs` を超えても 0 で止まり負にならない | 同上 |
| 3 | 表示秒の切り上げ（1ms 残りで「1」、0ms で「0」） | 同上 |
| 4 | 新しい予告で即座に値が飛ぶ | 手動 |
| 5 | `SelfAtRisk` が `false → true` でSEが1回だけ鳴る | 手動 |
| 6 | `cutStoreIds` が `maxCutRows` より多いとき「他◯店」が出る | EditMode |
| 7 | `DisplayNames` に無い `storeId` で `storeId` が出る（空欄にならない） | EditMode |
| 8 | `Cull == null` でパネルが非表示 | 手動 |
| 9 | 残り20秒では `None`、10秒を切ると出はじめる | EditMode（`CullAlertState`） |
| 10 | `SelfAtRisk` で `Danger`、下位範囲のみで `Caution`、どちらでもなく `None` | EditMode |
| 11 | 脱落（`selfAlive == false`）で `None` になる | EditMode |
| 12 | `IsInBottomRange` が `BuildBottom` と同じ範囲を返す | EditMode |
| 13 | **明滅が 1Hz を超えない**（`MaxPulseHz`。§5.1 の安全要件） | 目視＋コードレビュー |
| 14 | 中央の素通し部分が縦画面で円に見える（楕円に歪まない） | 手動 |
| 15 | `Cull` が null に戻ったフレームでアラートが残らない | 手動 |
| 15b | **下位パネルの中に、名前だけの行が湧いていない**（§4.0 の事故の再発検知） | 手動 |
| 16 | 残り5秒で中央に「5」が出て、0秒で消える（「0」が残らない） | EditMode（`CullFinalCountdownState`） |
| 17 | `Caution`（ぎりぎり圏外）にも中央カウントダウンが出る／安全圏には出ない | EditMode |
| 18 | 数字が変わるたびに小さいところから広がってフェードインする | 手動 |

## 11. 未確定事項

- `maxCutRows` の値。サーバーの `cutStoreIds` 送信件数と揃える（[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点4。サーバーと合意して決める）
