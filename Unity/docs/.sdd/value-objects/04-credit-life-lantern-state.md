# 04-CreditLifeLanternState

> 参照する上流：[pureC#/docs/.sdd/value-objects/02-store-state.md](../../../../pureC%23/docs/.sdd/value-objects/02-store-state.md)（`StoreState.CreditLife`）、[用語集 6章「信用システム(ライフ)」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

## 1. 責務

- 画面左上の提灯3つ（信用ライフ残数）を、提灯1つごとの点灯/消灯という表示用状態に変換する
- **しない**こと：`CreditLife` の増減判定（サーバー権威。`CreditUpdate` を受けて`pureC#`側`StoreState`が更新するだけ）

## 2. データ定義

```csharp
public enum LanternState { Lit, Unlit }

public readonly record struct CreditLifeLanternState(
    IReadOnlyList<LanternState> Lanterns // 長さ = initialLife（例:3）
);
```

## 3. 変換処理

入力：`StoreState.CreditLife`、`initialLife`（`GameParameters`由来。試合開始時の信用値）

```
for i in 0..initialLife-1:
    Lanterns[i] = i < CreditLife ? Lit : Unlit
```

- 添字の小さい方から点灯とみなす単純な対応。どの提灯が消えるか（左から順か、特定の1個が消えるか）に演出上の意味はない前提。意味を持たせたい場合は未確定事項へ

## 4. Unity構成

- 左上の提灯UIコンポーネントが `CreditLifeLanternState.Lanterns` を購読し、各提灯のスプライトを点灯/消灯で切り替える
- `CreditLife`が減った**瞬間**の「消える演出（割れる等）」はViewローカルの一時状態であり、この値オブジェクトには含めない。トリガーとしては「直前フレームの`Lanterns[i]`が`Lit`から`Unlit`に変わった」ことをViewが検知して演出を再生する

## 5. 未確定な演出との境界

- ここまで：点灯数と`CreditLife`の対応、提灯の総数(`initialLife`)
- ここから先（未確定）：消灯時の演出（割れる/消える等）、`CreditLife`が0になった瞬間（脱落と同時）の提灯の最終見た目

## 6. テスト観点

- `CreditLife == initialLife`のとき全点灯、`CreditLife == 0`のとき全消灯になるか
- `initialLife`と`Lanterns.Count`が常に一致するか（`GameParameters`変更時にも追従するか）

## 7. 未確定事項

- 消灯順序・演出の要否
- `initialLife`が`GameParameters`で試合ごとに変わり得る場合、提灯の表示レイアウト（横並びの最大数）が動的に変わってよいか
