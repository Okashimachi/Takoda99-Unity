# 13-淘汰アラートの段階（画面端の警告）

> 参照する上流：[本選企画書 3.3・3.6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `ForcedEliminationWarning`／`pureC#` [match-state/03-cull-warning.md](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)。矛盾したら上流優先。
>
> このVOは [../ranking-view/02-cull-countdown-panel.md](../ranking-view/02-cull-countdown-panel.md) §5 の判定部分だけを、テストできる純関数として切り出したもの。**色・不透明度・明滅の速さは持たない**（それらは View 側の Inspector 値）。

## 1. 責務

**する**

- 淘汰アラートを「出さない／軽く出す／強く出す」の3段階に落とす
- 秒読みの窓（既定10秒）に入っているかを判定する
- 窓の中での進み具合（`Progress` 0..1）を返す

**しない**

- 順位と `CutLineRank` を比較しない（[docs/rules/01](../../../../docs/rules/01-責務と絶対原則.md)）
- 色・不透明度・明滅の速さを決めない（View の担当）
- 時刻を自分で取得しない（`nowLocalMs` を引数で受ける。テスト可能にするため）

## 2. データ定義

```csharp
// Assets/Scripts/View/ValueObjects/CullAlertState.cs
public enum CullAlertTier { None, Caution, Danger }

public readonly struct CullAlertState : IEquatable<CullAlertState>
{
    public const int DefaultAlertWindowMs = 10_000;

    public CullAlertTier Tier { get; }

    /// <summary>窓の中での進み具合 0..1。残りが少ないほど 1 に近づく。None なら 0。</summary>
    public float Progress { get; }

    public static CullAlertState From(
        CullWarning warning,
        long nowLocalMs,
        bool selfAlive,
        bool selfInBottomRange,
        int alertWindowMs = DefaultAlertWindowMs);
}
```

## 3. 変換処理

上から順に、**最初に当たったもので確定**する。

| 優先 | 条件 | 結果 |
|---|---|---|
| 1 | `warning == null`（未受信） | `None` |
| 2 | `selfAlive == false`（**脱落後**） | `None` |
| 3 | `RemainingMsAt(now) > alertWindowMs`（まだ余裕がある） | `None` |
| 4 | `warning.SelfAtRisk`（サーバー権威） | `Danger` |
| 5 | `selfInBottomRange`（下位パネルの表示範囲に自店が居る） | `Caution` |
| 6 | それ以外（**範囲から外れた**） | `None` |

```
Progress = 1 - clamp01(RemainingMsAt(now) / alertWindowMs)
```

### 3.1 ★「ぎりぎり圏外」の根拠

`ForcedEliminationWarning` には「淘汰圏内か」（`SelfAtRisk`）しか無く、
**「圏外だが危ない」を表すサーバー値は存在しない。**

クライアントが `Rank` と `CutLineRank` を比較して補うことは**禁止**されている
（[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `proto/messages.go`：
「rank と cutLineRank の比較をクライアントにさせない（勝敗に関わる推測をさせない原則）」）。

そこで**下位パネルの表示範囲に自店が入っているか**を根拠にする。
これは [12-ranking-row-style.md](./12-ranking-row-style.md) §4.2 の `AtRisk`
（「今は圏外だが、スコアの急変動で落ち得る人」）と**まったく同じ根拠**であり、

1. 順位比較を一切していない（範囲は `start = Max(0, aliveCount - count)` の1本の式だけで決まる）
2. 画面上でも下位パネルの警告帯と一致するので、2つのUIが食い違わない

判定は `RankingRowsBuilder.IsInBottomRange` が持つ。
**`BuildBottom` と同じ式を使う（片方だけ直さないこと）。**

### 3.2 `SelfAtRisk` を `Caution` の判定に混ぜない理由

`CutStoreIds` はサーバーが表示件数ぶんに上限を切るため、
**含まれていないからといって安全とは言えない**（[12](./12-ranking-row-style.md) §4.2 の注記）。
自店については `SelfAtRisk` が別途届くので、**赤（`Danger`）の判定は必ずこちらを使う**。
下位範囲の判定は、より弱い `Caution` の根拠に留める。

## 4. Unity構成

View（`CullCountdownPanelView`）が `Tier` から色と最大不透明度を引き、`Progress` で濃さを決める。
**明滅の速さは `Progress` に依らず一定**（[../ranking-view/02](../ranking-view/02-cull-countdown-panel.md) §5.1 の安全要件）。

| `Tier` | 色 | 最大 α | 谷の下限 |
|---|---|---|---|
| `Caution` | 淡い黄〜橙 | 0.3 | 0（完全に消えてよい） |
| `Danger` | 赤 | 0.75 | 0.35（消しきらない） |

## 5. 依存関係

- 依存する：`ClientState.Cull` / `ClientState.Alive` / `ClientState.Phase`、`RankingRowsBuilder.IsInBottomRange`
- 依存される：[../ranking-view/02-cull-countdown-panel.md](../ranking-view/02-cull-countdown-panel.md)

## 6. テスト観点

`Unity/tests/Takoda99.View.Tests/CullAlertStateTests.cs`。すべて EditMode で完結する。

| # | 観点 |
|---|---|
| 1 | 未受信で `None` |
| 2 | 残り20秒（窓の外）で `None` |
| 3 | 残り10秒ちょうどで出はじめ、`Progress == 0` |
| 4 | `SelfAtRisk` で `Danger`（下位範囲に居なくても） |
| 5 | `SelfAtRisk` でなく下位範囲に居れば `Caution` |
| 6 | どちらでもなければ `None`（範囲から外れたら完全に消える） |
| 7 | `selfAlive == false` で `None`（脱落後は全部止まる） |
| 8 | 残りが半分で `Progress == 0.5`、0で `1` |
| 9 | 窓を大きく超えて経過しても `Progress` が 1 を超えない |
| 10 | `IsInBottomRange` が `BuildBottom` と同じ範囲を返す |

## 7. 未確定事項

- `DefaultAlertWindowMs = 10_000` の妥当性。実機で短い／長いと感じたら調整する
- `Caution` の色と濃さ。下位パネルの `AtRisk` 帯の色と揃えるか、あえて変えるか
- SE を `Caution` でも鳴らすか（現状は `Danger` のみ）
