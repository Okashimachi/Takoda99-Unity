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
| 脱落予定の店 | `cull.CutStoreIds` | 名前を最大 `maxCutRows` 件。`state.DisplayNames` で解決 |
| 自分が対象か | `cull.SelfAtRisk` | **画面全体アラート**（§5） |

### 4.1 名前の解決

```csharp
private string ResolveName(ClientState state, string storeId)
    => state.DisplayNames.TryGetValue(storeId, out var n) && !string.IsNullOrEmpty(n)
        ? n
        : storeId;   // 解決できなければ storeId をそのまま出す（空欄にしない）
```

`cutStoreIds` の件数はサーバーが上限を切っているが、**それが `maxCutRows` より多い可能性がある**。多い場合は先頭 `maxCutRows` 件だけ描き、「他◯店」と添える。

## 5. `SelfAtRisk` の演出

| 状態 | 演出 |
|---|---|
| `SelfAtRisk == false` | 通常表示（パネルは出ているが静か） |
| `SelfAtRisk == true` | **画面全体に届く警告**（画面全体を覆う赤いオーバーレイの明滅）。残り秒が少ないほど強くする |
| `false → true` に変わった瞬間 | `OnWarningReceived` を契機に一度だけ強い演出＋SE |
| `true → false` に変わった瞬間 | 警告を解除（「逃げ切った」ことが分かる） |

> **★実装の注意（一度ハマった）：** `AtRiskOverlay` の `Image.color` の alpha を 0 のまま置くと、
> `CanvasGroup.alpha` をいくら上げても掛け算で常に 0 になり、**警告が画面に一切出ない**。
> `Image.color` は不透明（alpha=1, 色は赤）で固定し、可視・不可視は必ず `CanvasGroup.alpha` 側で制御する。
>
> `AlertIntensity`（0..1）は単純フェードではなく**明滅（パルス）**で表現する。
> `intensity` が高いほど明滅を速く・深くする（`CullCountdownPanelView.Update` 内、`Time.unscaledTime` 基準。
> タイムスケールに影響されないが `ClientState` は触らないため C1 に反しない）。

| 禁止 | 理由 |
|---|---|
| 打鍵の視認性を下げる警告 | 急がせるための警告が、急ぐ手段（打鍵）を妨げては本末転倒 |
| 毎フレーム鳴るSE | `SelfAtRisk` は1〜2Hz で届き続ける。**状態が変わった瞬間だけ**鳴らす |

## 6. 最終ステージ（120秒）の特別な見え方

最終ステージでは `CutLineRank == 2` が届く（Proto コメント参照）。処理上は1位も脱落するが、**表示は「1位以外が脱落対象」**とするのが企画意図。

```
決勝の10人 → 9人が脱落対象 → 生き残るのは1人だけ → 緊張が最大化する
```

**クライアントは `CutLineRank` をそのまま「◯位以下」として描けばよい。** 特別な分岐を書かない。

## 7. 待機中・脱落後の扱い

| 状況 | 扱い |
|---|---|
| `GameBeforeView` 保持中 | 描かない |
| `Cull == null` | パネル非表示 |
| 自店が脱落（`Spectating`） | **描き続ける。** 他店がいつ切られるかは観戦の見どころ。ただし `SelfAtRisk` は届かなくなる想定であり、警告演出は出ない |
| `Phase == Result` | 畳む |

## 8. 依存関係

- 依存する：`pureC#` [match-state/03](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)、[../value-objects/09](../value-objects/09-cull-countdown-state.md)、[01-ranking-panel.md](./01-ranking-panel.md)（`RankingRowView` の再利用）
- 依存される：なし

## 9. テスト観点

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

## 10. 未確定事項

- `maxCutRows` の値。サーバーの `cutStoreIds` 送信件数と揃える（[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点4。サーバーと合意して決める）
