# 10-SubStoreBoardView / SubStoreTileView

> 参照する上流：[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`Renderer`）／[用語集](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)（`Store` / `StoreId` / `CreditLife` / `StoreEliminated` / `rank`）／`StoreListUpdate`（Proto）。値の形は [value-objects/06-sub-store-tile-state.md](./value-objects/06-sub-store-tile-state.md) が正典。

小画面（`root/SubStoreCanvas`）の他店98店ぶんのミニ盤面。既存の `SubStoreCreator` を置き換える。

## 1. 責務

### `SubStoreTileView`（`SubStorePanel` Prefab）

- 1店舗ぶんのタイルの5状態（`Life3` / `Life2` / `Life1` / `JustEliminated` / `Eliminated`）を見た目に変換する
- 脱落してから `Eliminated` へ遷移するまでの**経過時間だけ**を自分で保持する（Viewローカルの一時状態）
- **しない**こと：脱落の判定・順位の算出

### `SubStoreBoardView`（`SubStoreCanvas`）

- `SubStorePanel` Prefab を `Left` / `Right` に 7×7＝49枚ずつ、計98枚生成する
- **自店を除いた98店**を `StoreId` 昇順に、`Left` の左上から順に詰め、`Left` が埋まったら `Right` へ移る
- `StoreId` ↔ タイルの対応表を保持し、更新を該当タイルへ振り分ける

## 2. 公開インターフェース

```csharp
namespace Takoda99.View
{
    public sealed class SubStoreTileView : MonoBehaviour
    {
        [SerializeField] private Image booth;              // SubStore
        [SerializeField] private GameObject rankPanel;     // SubStoreRankPanel（既定で非アクティブ）
        [SerializeField] private TextMeshProUGUI rankText; // SubStoreRankPanel/Text
        [SerializeField] private Sprite boothLife0;        // minitile_booth_life0
        [SerializeField] private Sprite boothLife1;
        [SerializeField] private Sprite boothLife2;
        [SerializeField] private Sprite boothLife3;
        [SerializeField] private float eliminationRevealDelaySec = 3f;

        public string StoreId { get; private set; }
        public SubStoreTileState State { get; private set; }

        public void Bind(string storeId);

        /// <summary>StoreListUpdate 由来の値を反映する。</summary>
        public void SetSummary(int creditLife, bool alive);

        /// <summary>完全脱落時に表示する順位。未確定なら null を渡す（順位テキストを空にする）。</summary>
        public void SetRank(int? rank);
    }

    public sealed class SubStoreBoardView : MonoBehaviour
    {
        public const int ColumnsPerSide = 7;
        public const int RowsPerSide    = 7;
        public const int TilesPerSide   = ColumnsPerSide * RowsPerSide; // 49
        public const int TileCount      = TilesPerSide * 2;             // 98

        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;
        [SerializeField] private GameObject subStorePanelPrefab;

        /// <summary>自店を除く他店の StoreId 一覧を割り当てる。昇順に整列してから左上詰めで配置する。</summary>
        public void Bind(IReadOnlyList<string> otherStoreIds);

        public void SetSummary(string storeId, int creditLife, bool alive);
        public void SetRank(string storeId, int rank);
    }
}
```

## 3. Unity構成

### 3.1 シーン階層

```
root/SubStoreCanvas          ← SubStoreBoardView
├── Left                     ← SubStorePanel ×49
└── Right                    ← SubStorePanel ×49
```

`SubStorePanel` Prefab の中身（確認済み）：

```
SubStorePanel                ← SubStoreTileView
├── BG          (Image, 半透明の下地)
├── SubStore    (Image, minitile_booth_life* を差し替える)
└── SubStoreRankPanel        ← 入れ子Prefab。m_IsActive = 0（既定で非表示）
    └── Text                 ← 現在は Text (Legacy)。TextMeshProUGUI へ差し替える
```

### 3.2 MonoBehaviour のライフサイクル

`SubStoreTileView`
- `Awake`：参照の null チェック。`rankPanel.SetActive(false)`
- `Update`：`Alive == false` かつ `State == JustEliminated` の間だけ経過時間を加算し、`eliminationRevealDelaySec` を超えたら `Eliminated` へ遷移する（それ以外のフレームでは何もしない）

`SubStoreBoardView`
- `Awake`：`Left` / `Right` に 49 枚ずつ生成する（生成レイアウトは既存 `SubStoreCreator` の等間隔グリッド計算をそのまま引き継ぐ）
- `Bind`：`otherStoreIds` を昇順に整列してタイルへ割り当てる。要素数が98未満なら余ったタイルを非アクティブにし、98を超えたら超過分を捨てて `Debug.LogWarning`
- `Update`：**使わない**

### 3.3 既存 `SubStoreCreator` の扱い

`SubStoreCreator` は `SubStoreBoardView` にリネーム・拡張する（生成ロジックは流用、`Start` での自動生成は `Awake` + `Bind` に分離）。旧クラスは残さない。

## 4. ふるまいの詳細

### 4.1 配置順

- `Left` / `Right` とも 7列×7行の等間隔グリッド、**左上原点で行優先**
- 割り当ては `StoreId` 昇順。`Left` の index 0..48 を埋めてから `Right` の index 0..48 へ移る
- 自店の `StoreId` は `otherStoreIds` に含めない（呼び出し側で除外する）

### 4.2 見た目

| 状態 | `SubStore` | `SubStoreRankPanel` |
|---|---|---|
| `Life3` | `minitile_booth_life3` | 非表示 |
| `Life2` | `minitile_booth_life2` | 非表示 |
| `Life1` | `minitile_booth_life1` | 非表示 |
| `JustEliminated` | `minitile_booth_life0` | 非表示 |
| `Eliminated` | **非表示** | 表示（順位テキスト） |

- 区分の導出規則は [value-objects/06](./value-objects/06-sub-store-tile-state.md) §3
- `Eliminated` へ遷移した時点で `SetRank` 済みなら順位を、未取得なら空文字を表示する。後から `SetRank` が来たら追記表示する
- 一度 `Eliminated` になったタイルは、以後の `SetSummary` で `Life*` へ戻らない（脱落は不可逆）

### 4.3 エッジケース

- 未知の `StoreId` に対する `SetSummary` / `SetRank` → 無視して `Debug.LogWarning`
- `Bind` の再実行（再接続時）→ 全タイルの状態を初期化してから割り当て直す

## 5. 依存関係

- 依存する `pureC#` モジュール：なし（`StoreSummaryState` の値を素の型で受け取る）
- 依存するUnity側モジュール：`Takoda99.View.ValueObjects.SubStoreTileState`
- 依存されるモジュール：`Renderer`（未作成）、`MainGameViewSampleDriver`（[11](./11-view-sample-data.md)）

## 6. テスト・確認観点

- 98枚が `Left` 49 → `Right` 49 の順で、左上から `StoreId` 昇順に並ぶか
- 信用ライフ 3→2→1 でタイル画像が切り替わるか
- `alive = false` にした瞬間に `life0` になり、**3秒後**に屋台が消えて順位テキストが出るか
- 脱落済みタイルに `SetSummary(3, true)` を送っても復活しないか
- `SubStoreRankPanel` が既定で非表示のまま、`Eliminated` のときだけ表示されるか

## 7. 未確定事項

- **他店の脱落順位の入手手段**（[SV-15](../../../docs/server-sync/01-プロトコル契約の差分.md#sv-15)）。`StoreSummary` に順位に相当するフィールドが無いため、確定するまで `SetRank` はサンプルデータからのみ呼ぶ
- `StoreId` の実体（数値の文字列か、UUID等か）。昇順整列の規則は `StoreId` の文字列順とする。数値idであることが確定したら数値順へ変更する（[SV-11](../../../docs/server-sync/01-プロトコル契約の差分.md#sv-11) と併せて確認）
- `SubStoreRankPanel/Text` を TMP へ差し替えるか、レガシー `Text` のまま使うか（本仕様書では TMP へ差し替える前提）
- 99人未満で試合が始まった場合の余りタイルの見た目
