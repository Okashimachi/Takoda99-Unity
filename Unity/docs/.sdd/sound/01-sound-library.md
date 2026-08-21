# 01-sound-library

> 参照する上流：[Takoda99-Docs 02_共通仕様/01_全体仕様.md]() の試合進行、[Takoda99-Proto]() の `CullWarning` / `StoreEliminated` / `PersonalResult`。矛盾したら上流優先。

## 1. 責務

**する**

- 全SEの `AudioClip` と音量を `SoundLibrary`（ScriptableObject）1つで一括管理する
- 音量を「マスター × カテゴリ × 個別」の3段のスライダーで持つ
- `SoundId` を受け取って1回鳴らす窓口（`SoundPlayer`）をシーンをまたいで1つだけ持つ

**しない**

- 経営ロジックの判断（誰が脱落するか・順位がいくつか）をSEの都合で行わない。鳴らす契機は**すでに画面が描いている状態の変化**だけを根拠にする
- 順位と `CutLineRank` を比較して自店の危険を判定しない（[ranking-view/02](../ranking-view/02-cull-countdown-panel.md) §1）。淘汰圏内の権威はサーバー（`CutStoreIds` / `SelfAtRisk`）
- BGM を扱わない（本仕様の対象はSEのみ）

## 2. 公開インターフェース

```csharp
namespace Takoda99.Sound
{
    public enum SoundId { None = 0, ButtonTap = 100, /* ... */ }
    public enum SoundCategory { Ui, Matchmaking, MatchFlow, Typing, Cull, Ranking, Result }

    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Takoda99/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        public bool TryResolve(SoundId id, out AudioClip clip, out float volume);
        public bool Contains(SoundId id);
    }

    public sealed class SoundPlayer : MonoBehaviour
    {
        public static void Play(SoundId id, float volumeScale = 1f);
    }
}
```

判定の純粋なルールは値オブジェクトに分ける（[value-objects/](../value-objects/README.md) と同じ扱い）。

- `TypingWordSoundRule.From(correctCount, missCount, threshold) → TypingWordOutcome`
- `RankSoundRule.CullBandCount(aliveCount, cutLineRank, marginRatio) → int`
- `RankSoundRule.Resolve(alive, rank, topThreshold, isCutTarget, isInCullBand) → RankSoundBand`
- `ResultRankSoundRule.From(finalRank, topCount, bottomCount, storeCount) → ResultRankSound`

## 3. Unity構成

- **アセット**：`Assets/Resources/SoundLibrary.asset`。`Resources` に置くのは、SE を Title / MatchMaking / MainGame / Result の**全シーンから鳴らす**ため。シーンごとに参照を結線すると4シーンぶん外れる余地ができる
- **`SoundPlayer`**：`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で自分を生成し `DontDestroyOnLoad`。**シーンにもInspectorにも結線対象を持たない**。Boot シーンを経由しないエディタ再生（Result 単体の確認など）でも鳴る
- **同時発音**：`AudioSource` を8本持ち、鳴らすたびに順に回す（同じ Source に `PlayOneShot` を積むと濁る）。`spatialBlend = 0`（UI音なので距離減衰を掛けない）
- **音量**：`マスター × カテゴリ × 個別 × その場の倍率`。素材ごとの録音レベル差は個別スライダーで均し、「打鍵音がうるさい」のような意味単位の調整はカテゴリ1本で済ませる

## 4. ふるまいの詳細（イベント割り当て）

| SE | 契機 | 実装箇所 |
|---|---|---|
| ボタンタップ | 各ボタンの押下 | `TitleScreenView` / `MatchmakingScreenView`（Decide）/ `EliminationResultView`（Next）/ `ResultScreenView`（Title・X） |
| マッチング完了 | カウントダウンが尽きて `MatchingComplete` が出た瞬間（1回） | `MatchmakingScreenView.ApplyCountdown` |
| 試合前カウントダウン | 待機の秒読みで**数字が変わるたび**（5→4→3→2→1 の5回） | `GameBeforeView.ApplyText` |
| 試合開始合図 | 待機が明けた瞬間（サーバーの `MatchStart` 到達） | `GameBeforeView.TryFinish` |
| 試合終了 | `MatchEnd` 受信 | `Renderer.OnMatchEnd` |
| 打鍵 通常／パーフェクト／ミス多発 | **1単語を打ち終えるごとに1回**。ノーミス＝パーフェクト、ミス率が閾値以下＝通常、超えたら＝ミス多発 | `Renderer.OnKeyFeedback` / `Renderer.OnOrderServed` |
| 下位淘汰カウントダウン 通常／警告 | 淘汰直前の秒読みで数字が変わるたび。自店が淘汰圏内（`Danger`）なら警告、ぎりぎり圏外（`Caution`）なら通常 | `CullFinalCountdownView.Apply` |
| 脱落 | `StoreEliminatedBatch` 1回につき1回。段階と自店の有無を音量差で表す | `MassEliminationEffect.Play` |
| 上位ランク入り | 自店の順位が上位圏（既定10位以内）に**入った瞬間** | `SelfRankView.PlayBandSe` |
| 下位ランク入り（淘汰圏内／ぎりぎり圏外） | 自店が淘汰圏、またはその直前の帯に**入った瞬間**。2つは別の `SoundId` として音量を独立に振れる | `SelfRankView.PlayBandSe` |
| リザルトたこ焼き生成 | たこ焼き1個の生成ごと | `TakoyakiCreator.Spawn` |
| リザルトランク表示 上位／下位／通常 | **順位・成績・次へボタンが出そろった瞬間**（1回）。既定で3位までが上位、50位以下（99店の下半分＝下位50店）が下位 | `TakoyakiCreator.RevealCompleted` → `ResultScreenView.OnRevealCompleted` |

### エッジケース

- **打鍵SEは1打ごとに鳴らさない。** 毎秒数打の音が鳴り続けると、秒読みや淘汰のSEを覆い隠す
- **注文の最終単語**は `KeyResult.OrderCleared` になり `OnKeyFeedback` に届かない（`MatchClientController` は代わりに `OnOrderServed` を呼ぶ）。ここで鳴らさないと注文の最後の1単語だけ無音になる
- **客が入れ替わったら打鍵の数え上げを捨てる。** 中断された注文（`AbortOrder`）のミスを次の客の1単語目へ持ち越さない
- **順位帯のSEは初回の反映では鳴らさない。** 試合開始直後は「入った」のではなく最初の順位が届いただけで、全店に一斉に鳴ってしまう
- **順位帯の判定は淘汰側を上位圏より優先する。** 最終段階は `CutLineRank` が 2 になり上位圏と淘汰圏が重なるため、そこで祝福の音は明らかに誤り
- **順位帯のSEは `SelfRankView` に一本化する。** 上位入り・淘汰圏入り・ぎりぎり圏外入りを複数箇所で判定すると同じ状況で二重に鳴る（`CullCountdownPanelView` の自前 `AudioSource` は撤去した）
- **`AudioClip` 未割り当て・音量0・`muted` のときは何もしない。** 音が無いだけで進行は止めない
- **`Resources/SoundLibrary` が見つからないとき**は警告を1回出して無音で続行する

### WebGL

`AudioSource.PlayOneShot` のみを使う（`AudioClip` のストリーミング再生・`AudioSettings` の操作はしない）。ブラウザの自動再生制限により、**最初のユーザー操作より前に鳴らすSEは無音になり得る**。最初に鳴るのは Title のボタン押下なので実害は無い。

## 5. 依存関係

- 依存する `pureC#` モジュール：`ClientState`（`Rank` / `AliveCount` / `Alive` / `Cull`）、`KeyResult`、`PersonalResultState`
- 依存するUnity側モジュール：なし（`SoundPlayer` は誰にも依存しない）
- 依存されるモジュール：`Renderer` と各View、`TakoyakiCreator`、`ResultScreenView`

## 6. テスト・確認観点

- `TypingWordSoundRule` / `RankSoundRule` / `ResultRankSoundRule` は Unity 非依存の純関数なので `Unity/tests` で単体テストできる
- エディタ実行：`SoundLibrary.asset` の `muted` を切り替えて、全SEが1回ずつ鳴ることを確認する
- リザルトは `ResultScreenView.testMode` と `testTakoyakiCount` で順位別のSEを個別に確認する

## 7. 未確定事項

- 打鍵SEのミス率の境目（既定 0.15）は実機で詰める。`Renderer` の Inspector 公開値
- ぎりぎり圏外の幅（淘汰件数の 25%）も同様に `SelfRankView` の Inspector 公開値
- BGM とその音量スライダーは本仕様の対象外
