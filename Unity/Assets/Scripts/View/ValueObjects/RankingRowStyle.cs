// 仕様書: Unity/docs/.sdd/value-objects/12-ranking-row-style.md
// 順位・帯からランキング行の見た目（寸法・フォントサイズ・配色区分）を決める。
// 順位を計算しない。RectTransform / Image を触らない（適用は View の仕事）。

using System;
using UnityEngine;

namespace Takoda99.View.ValueObjects
{
    /// <summary>行の配色区分。Panel の色をこれで引く。</summary>
    public enum RankingRowTone
    {
        Gold,      // 1位
        Silver,    // 2位
        Bronze,    // 3位
        Upper,     // 4〜10位
        Normal,    // 帯なしの下位
        AtRisk,    // 警告帯（次の足切りで落ちる可能性がある）
        Doomed,    // 脱落確定帯（CutStoreIds に入っている）
        Dead,      // 脱落済み
    }

    /// <summary>1行が取るべき見た目。座標は含まない（スロットが持つ）。</summary>
    public readonly struct RankingRowStyle : IEquatable<RankingRowStyle>
    {
        public Vector2 Size { get; }          // RectTransform.sizeDelta

        public float RankFontSize { get; }

        public float NameFontSize { get; }

        public float ScoreFontSize { get; }

        // RankText/NameText/ScoreText 各自の RectTransform.anchoredPosition / sizeDelta。
        // パネルの Size だけ変えてもテキストの位置・幅は追従しないため、順位段階ごとに個別に持つ
        // （もともとシーンの非アクティブな参照パネル Slots/Slot04〜10 に採寸されていた値）。
        public Vector2 RankOffset { get; }
        public Vector2 RankSize { get; }
        public Vector2 NameOffset { get; }
        public Vector2 NameSize { get; }
        public Vector2 ScoreOffset { get; }
        public Vector2 ScoreSize { get; }

        public RankingRowTone Tone { get; }

        private RankingRowStyle(
            Vector2 size,
            float rankFontSize, float nameFontSize, float scoreFontSize,
            Vector2 rankOffset, Vector2 rankSize,
            Vector2 nameOffset, Vector2 nameSize,
            Vector2 scoreOffset, Vector2 scoreSize,
            RankingRowTone tone)
        {
            Size = size;
            RankFontSize = rankFontSize;
            NameFontSize = nameFontSize;
            ScoreFontSize = scoreFontSize;
            RankOffset = rankOffset;
            RankSize = rankSize;
            NameOffset = nameOffset;
            NameSize = nameSize;
            ScoreOffset = scoreOffset;
            ScoreSize = scoreSize;
            Tone = tone;
        }

        /// <summary>
        /// フォントを「一回り」小さくする係数。**「一回り小さく」＝1回、「二回り」＝2回掛ける。**
        ///
        /// <para>
        /// 段ごとの比（1位が大きく7位が小さい／上段:下段 = 2:3）を崩さずに全体だけ縮めるため、
        /// 各値を個別に書き換えるのではなく**係数を1つ**にしている。
        /// もう一回り縮める指示が来たら、掛ける回数を増やすだけで比は保たれる。
        /// </para>
        /// </summary>
        private const float FontStepDown = 0.9f;

        /// <summary>
        /// フォントサイズ1に対して1文字が取る幅（em）。**Noto Sans JP の実測値**
        /// （`NotoSansJP-Bold SDF.asset` の UnitsPerEM 1000 / PointSize 61 から換算）に
        /// 少しだけ余裕を足したもの。
        ///
        /// <para>
        /// 枠に入るかどうかを TMP に聞かずに決めているのは、<see cref="RankingRowStyle"/> が
        /// <c>UnityEngine.UI</c> に触らない純粋な値だから。実測（半角数字 0.590em・"nd" 1.285em・
        /// 全角 1.000em）から**最悪の文字列**を積んだ上限だけを持つ。
        /// </para>
        /// </summary>
        private const float RankTextEm = 2.6f;

        /// <summary>スコア欄の最悪ケース "-9999"（2.73em）。</summary>
        private const float ScoreTextEm = 3.0f;

        /// <summary>屋号の最悪ケース。**全角6文字**（6.00em）。</summary>
        private const float NameTextEm = 6.3f;

        /// <summary>
        /// 枠に収まる最大のフォントサイズ。<paramref name="cap"/> より大きくはしない
        /// （枠が余っても勝手に太らせない）。
        /// **これを通していない値をそのまま TMP に渡さない。** はみ出した文字は
        /// 折り返される（"16th" が "16t" ＋ "h" になる）か、隣の列に重なる。
        /// </summary>
        private static float FitFont(float cap, float boxWidth, float em)
        {
            if (boxWidth <= 0f || em <= 0f)
            {
                return cap;
            }

            return Mathf.Min(cap, boxWidth / em);
        }

        // ★RankOffset / NameOffset / ScoreOffset は「中心からの距離」ではなく
        //   **その Prefab のアンカーからの距離**（anchoredPosition にそのまま入る）。
        //   Prefab ごとにアンカーが違うので、値を書き換えるときは必ず確認する。
        //
        //   | Prefab             | RankText | NameText | ScoreText |
        //   |--------------------|----------|----------|-----------|
        //   | TopRanker.prefab   | 左       | 中央     | 右        |
        //   | BottomRanker.prefab| 左       | 右       | 右        |
        //   | LostRanker.prefab  | 中央     | 中央     | 中央      |
        //
        //   中心基準のつもりで書くと、行の幅を変えた瞬間に中身だけが横へずれる。

        /// <summary>
        /// 上位パネル用。順位だけで決まる（value-objects/12 §3.2・§4.1）。
        /// フォントサイズは Prefab 採寸の基準値へ <see cref="FontStepDown"/> を1回掛けた値
        /// （＝一回り小さい）。
        /// </summary>
        public static RankingRowStyle ForTopRank(int rank)
        {
            if (rank == 1 || rank == 2 || rank == 3)
            {
                var tone = rank == 1 ? RankingRowTone.Gold : rank == 2 ? RankingRowTone.Silver : RankingRowTone.Bronze;
                var rankSize = new Vector2(60f, 40f);
                var nameSize = new Vector2(130f, 40f);
                var scoreSize = new Vector2(60f, 40f);
                return new RankingRowStyle(
                    new Vector2(230f, 44f),
                    FitFont(20f * FontStepDown * FontStepDown, rankSize.x, RankTextEm),
                    FitFont(24f * FontStepDown * FontStepDown, nameSize.x, NameTextEm),
                    FitFont(20f * FontStepDown * FontStepDown, scoreSize.x, ScoreTextEm),
                    new Vector2(35f, 0f), rankSize,
                    new Vector2(-5f, 0f), nameSize,
                    new Vector2(-35f, 0f), scoreSize,
                    tone);
            }

            if (rank >= 4 && rank <= 6)
            {
                // シーン参照 Slot04〜06 採寸：箱が縦長(130x66)になるぶん、Rank/Score を上下に振り分ける。
                var rankSize = new Vector2(60f, 40f);
                var nameSize = new Vector2(110f, 40f);
                var scoreSize = new Vector2(60f, 40f);
                return new RankingRowStyle(
                    new Vector2(130f, 66f),
                    FitFont(16f * FontStepDown * FontStepDown, rankSize.x, RankTextEm),
                    FitFont(20f * FontStepDown * FontStepDown, nameSize.x, NameTextEm),
                    FitFont(14f * FontStepDown * FontStepDown, scoreSize.x, ScoreTextEm),
                    new Vector2(35f, 10f), rankSize,
                    new Vector2(0f, 0f), nameSize,
                    new Vector2(-35f, -10f), scoreSize,
                    RankingRowTone.Upper);
            }

            // 7〜10位、11位以上、0位以下（不明）はすべて同じ見た目（§4.1）。シーン参照 Slot07〜10 採寸。
            {
                var rankSize = new Vector2(60f, 40f);
                var nameSize = new Vector2(110f, 40f);
                var scoreSize = new Vector2(60f, 40f);
                return new RankingRowStyle(
                    new Vector2(100f, 50f),
                    FitFont(12f * FontStepDown * FontStepDown, rankSize.x, RankTextEm),
                    FitFont(16f * FontStepDown * FontStepDown, nameSize.x, NameTextEm),
                    FitFont(12f * FontStepDown * FontStepDown, scoreSize.x, ScoreTextEm),
                    new Vector2(35f, 3.5f), rankSize,
                    new Vector2(0f, 0f), nameSize,
                    new Vector2(-35f, -3.5f), scoreSize,
                    RankingRowTone.Upper);
            }
        }

        /// <summary>
        /// 下位パネル全体の縮尺。**行の寸法・余白・フォントをまとめてこの倍率で縮める。**
        ///
        /// <para>
        /// 一度 3列 × 122px（＝366px）まで広げたが `BottomRrankers`（370px）に収まりきらなかったため、
        /// 全体を 2/3 にする。**係数を1つにしてあるのは、順位と屋号の収まり（§3.2.2）を崩さずに
        /// 大きさだけを変えられるようにするため。** 幅も文字も同じ倍率で縮むので、
        /// 「全角6文字の屋号が入る」関係はこの値をいくつにしても保たれる。
        /// </para>
        /// <para>
        /// ★この値を変えたら <c>BottomRankingPanelView</c> の <c>rowHeight</c> と
        /// <c>columnSpacing</c>（シーンの値）も揃えること。
        /// </para>
        /// </summary>
        private const float BottomPanelScale = 2f / 3f;

        /// <summary>
        /// 行の高さだけに追加で掛ける倍率。**横幅とフォントには掛からない。**
        ///
        /// <para>
        /// 幅は詰まっているがもう少し縦にゆとりが欲しい、という調整のための独立した係数。
        /// <see cref="BottomPanelScale"/>（2/3）と合わせて 29 × 2/3 × 1.5 ＝ 29px になる。
        /// </para>
        /// <para>
        /// ★上げすぎると 10行ぶんの高さが画面から出る。現在の 1.5 で 10 行＝290px、
        /// `BottomRrankers` の中心 y=70 から上下 ±145 ＝ y[-75, 215]。
        /// 参照解像度 800×600・`MatchWidthOrHeight = 0`（幅合わせ）なので 16:9 では y の上端が 225、
        /// 自店パネル（`SelfRankNeonPanel`・上端 y=-85）との隙間が 10px。**どちらも余白は10pxしかない。**
        /// </para>
        /// </summary>
        private const float BottomRowHeightScale = 1.5f;

        /// <summary>下位パネル1行の寸法（縮尺適用前）。</summary>
        private static readonly Vector2 BottomBaseRowSize = new Vector2(118f, 29f);

        /// <summary>下位パネル1行の寸法。<c>BottomRankingPanelView.rowHeight</c> と一致させる。</summary>
        public static Vector2 BottomRowSize => new Vector2(
            BottomBaseRowSize.x * BottomPanelScale,
            BottomBaseRowSize.y * BottomPanelScale * BottomRowHeightScale);

        /// <summary>列と列の中心間の距離。<c>BottomRankingPanelView.columnSpacing</c> と一致させる。</summary>
        public static float BottomColumnSpacing => 122f * BottomPanelScale;

        /// <summary>下位パネルの基準フォントサイズ（<c>BottomRanker.prefab</c> の authored 値）。</summary>
        private const float BottomBaseFontSize = 12f;

        /// <summary>下位パネルのセル内側に空ける余白（px・縮尺適用前）。文字が枠線に触らないための最小限。</summary>
        private const float BottomCellPadding = 3f * BottomPanelScale;

        /// <summary>屋号と順位のあいだに空ける隙間（px・縮尺適用前）。</summary>
        private const float BottomColumnGap = 4f * BottomPanelScale;

        /// <summary>
        /// 下位パネル用。寸法は固定で、色だけが帯で変わる。
        ///
        /// <para>
        /// フォントは基準値へ <see cref="FontStepDown"/> を**2回**掛けた値（＝二回り小さい）。
        /// 順位が左・屋号が右で、どちらも <see cref="FitFont"/> を通してから枠を採るため、
        /// 全角6文字の屋号も "22nd" も**必ず行の内側に収まる**。
        /// </para>
        /// <para>
        /// ★オフセットは <b>Prefab のアンカー基準</b>（§3.2.3）。
        /// <c>BottomRanker.prefab</c> は <c>RankText</c> が左アンカー・<c>NameText</c>／<c>ScoreText</c> が
        /// 右アンカーなので、順位は左端から、屋号は右端からの距離で置く。
        /// 中心基準のつもりで書くと、行の幅を変えたときに中身だけが横へずれる。
        /// </para>
        /// </summary>
        public static RankingRowStyle ForBottomBand(RankingRowTone tone)
        {
            var cap = BottomBaseFontSize * FontStepDown * FontStepDown * BottomPanelScale;

            var rowSize = BottomRowSize;
            var inner = rowSize.x - BottomCellPadding * 2f;
            var rankWidth = RankTextEm * cap;
            var nameWidth = inner - rankWidth - BottomColumnGap;

            var rankFontSize = FitFont(cap, rankWidth, RankTextEm);
            var nameFontSize = FitFont(cap, nameWidth, NameTextEm);

            // 順位は左アンカーからの距離（正）、屋号は右アンカーからの距離（負）。
            var rankOffsetX = BottomCellPadding + rankWidth * 0.5f;
            var nameOffsetX = -(BottomCellPadding + nameWidth * 0.5f);

            return new RankingRowStyle(
                rowSize, rankFontSize, nameFontSize, 0f,
                new Vector2(rankOffsetX, 0f), new Vector2(rankWidth, rowSize.y),
                new Vector2(nameOffsetX, 0f), new Vector2(nameWidth, rowSize.y),
                new Vector2(nameOffsetX, 0f), new Vector2(nameWidth, rowSize.y),
                tone);
        }

        /// <summary>セルの左右に空ける余白（px）。文字が枠線に触らないための最小限。</summary>
        private const float AudienceCellPadding = 2f;

        /// <summary>
        /// セルの高さのうち上段（順位＋スコア）が取る比率。下段（屋号）は残り。
        /// **2:3（上段0.4・下段0.6）が要件**（ranking-view/07 §5.3）。屋号を大きく見せるための配分。
        /// </summary>
        private const float AudienceTopRowRatio = 0.4f;

        /// <summary>
        /// 各段の高さに対するフォントサイズの割合。段の比率がそのままフォントの比率になる。
        /// 基準 0.7 に <see cref="FontStepDown"/> を**2回**掛けた値（＝二回り小さい）。
        /// </summary>
        private const float AudienceFontFillRatio = 0.7f * FontStepDown * FontStepDown;

        /// <summary>
        /// オーディエンスパネル（ranking-view/07 §5.3）の1セル用。**上段に順位とスコアを並べ、その下に屋号**の2段。
        ///
        /// <para>
        /// すべて <paramref name="cellSize"/> からの比率で出す。**固定値を持たない**ので、
        /// `AudiencePanel` の寸法が変わってもセルの中身がそのまま追従する（07 §5.2）。
        /// </para>
        /// <para>
        /// 3つのテキストは中央 (0.5, 0.5) アンカーが前提（`LostRanker.prefab` 側で揃えてある）。
        /// 上段の左右振り分けは Prefab 側の水平アラインメント（順位=左・スコア=右）に任せ、
        /// ここでは幅を半分ずつ与えるだけにする。
        /// </para>
        /// </summary>
        public static RankingRowStyle ForAudienceCell(Vector2 cellSize, RankingRowTone tone)
        {
            var innerWidth = Mathf.Max(1f, cellSize.x - AudienceCellPadding * 2f);
            var topHeight = cellSize.y * AudienceTopRowRatio;
            var nameHeight = cellSize.y - topHeight;

            // 上段は箱の上辺に、下段は下辺に寄せる（中心からの offset で置く）。
            var topY = cellSize.y * 0.5f - topHeight * 0.5f;
            var nameY = nameHeight * 0.5f - cellSize.y * 0.5f;

            // 上段は順位とスコアで半分ずつ。中央から左右へ 1/4 幅ずつずらすと隙間なく二分割になる。
            var halfWidth = innerWidth * 0.5f;
            var halfOffset = innerWidth * 0.25f;

            // 段の高さから出したサイズは**縦にしか収まりを保証しない**。
            // 9列に割ると1マスは 61px ほどしかなく、全角6文字の屋号はそのままでは必ずはみ出す
            // （屋号が隣のマスへ流れる不具合の原因はこれ）。横幅でも頭打ちにする。
            var topFontSize = topHeight * AudienceFontFillRatio;
            var nameFontSize = nameHeight * AudienceFontFillRatio;

            return new RankingRowStyle(
                cellSize,
                FitFont(topFontSize, halfWidth, RankTextEm),
                FitFont(nameFontSize, innerWidth, NameTextEm),
                FitFont(topFontSize, halfWidth, ScoreTextEm),
                new Vector2(-halfOffset, topY), new Vector2(halfWidth, topHeight),
                new Vector2(0f, nameY), new Vector2(innerWidth, nameHeight),
                new Vector2(halfOffset, topY), new Vector2(halfWidth, topHeight),
                tone);
        }

        /// <summary>同じ寸法・フォントサイズ・テキスト配置のまま Tone だけ差し替える。</summary>
        public RankingRowStyle WithTone(RankingRowTone tone)
        {
            return new RankingRowStyle(
                Size, RankFontSize, NameFontSize, ScoreFontSize,
                RankOffset, RankSize, NameOffset, NameSize, ScoreOffset, ScoreSize,
                tone);
        }

        /// <summary>
        /// 寸法だけシーンのスロットの値で差し替える（§3.2 の注記・ranking-view/04 §5.2.1）。
        /// フォントサイズ・テキスト配置・Tone は表の値のまま残す。
        /// </summary>
        public RankingRowStyle WithSize(Vector2 size)
        {
            return new RankingRowStyle(
                size, RankFontSize, NameFontSize, ScoreFontSize,
                RankOffset, RankSize, NameOffset, NameSize, ScoreOffset, ScoreSize,
                Tone);
        }

        /// <summary>
        /// 帯（Tone）の決め方（value-objects/12 §4.2）。優先順に最初に当たったもので確定する。
        /// クライアントは Rank と CutLineRank を比較しない。
        /// </summary>
        /// <param name="alive">RankingRow.Alive。</param>
        /// <param name="isCutTarget">ForcedEliminationWarning.CutStoreIds に storeId が含まれるか。</param>
        /// <param name="isInBottomPanel">下位パネルに表示されているか（上位パネルでは false を渡す）。</param>
        public static RankingRowTone ResolveTone(bool alive, bool isCutTarget, bool isInBottomPanel)
        {
            if (!alive)
            {
                return RankingRowTone.Dead;
            }

            if (isCutTarget)
            {
                return RankingRowTone.Doomed;
            }

            if (isInBottomPanel)
            {
                return RankingRowTone.AtRisk;
            }

            return RankingRowTone.Normal;
        }

        /// <summary>
        /// 自店HUD（<c>SelfRankView</c> の順位テキスト）の配色区分。
        /// 色そのものは持たない（<c>RankingRowPalette</c> から引く。§3.1）。
        ///
        /// <para>
        /// **一覧の行（§4.2）とは優先順位が1点だけ違う。** メダル順位（1〜3位）を生死より先に見る。
        /// 一覧では「脱落した金色の行」が並ぶと生存者と見分けが付かないため <c>Dead</c> を最優先にしているが、
        /// 自店HUDは1つしかなく、**順位そのものが主役**（hud/01 §5）。
        /// 3位で脱落したら銅色が残るほうが確定順位の表現として正しく、
        /// 優勝者の順位が灰色になる事故も避けられる。
        /// </para>
        /// <para>
        /// 順位と <c>CutLineRank</c> の比較はここでも行わない。危険の根拠は
        /// <paramref name="isCutTarget"/>（サーバーの <c>CutStoreIds</c>）と
        /// <paramref name="isInBottomRange"/>（下位パネルの表示範囲）だけ（§4.2）。
        /// </para>
        /// </summary>
        /// <param name="rank">自店の順位（サーバー権威）。0 以下は未確定。</param>
        /// <param name="alive">自店が生存中か。</param>
        /// <param name="isCutTarget"><c>ForcedEliminationWarning.CutStoreIds</c> に自店が含まれるか。</param>
        /// <param name="isInBottomRange">下位パネルの表示範囲に自店が入っているか。</param>
        public static RankingRowTone ResolveSelfRankTone(
            int rank, bool alive, bool isCutTarget, bool isInBottomRange)
        {
            // 順位未確定（試合前・未受信）。色を付けない。
            if (rank <= 0)
            {
                return RankingRowTone.Normal;
            }

            // メダル順位は生死を問わず残す（上の注記）。
            if (rank == 1)
            {
                return RankingRowTone.Gold;
            }

            if (rank == 2)
            {
                return RankingRowTone.Silver;
            }

            if (rank == 3)
            {
                return RankingRowTone.Bronze;
            }

            // ここから §4.2 と同じ優先順位。
            if (!alive)
            {
                return RankingRowTone.Dead;
            }

            if (isCutTarget)
            {
                return RankingRowTone.Doomed;
            }

            if (isInBottomRange)
            {
                return RankingRowTone.AtRisk;
            }

            // 4〜10位は上位扱い、11位以下は帯なし。
            // ★ForTopRank(rank).Tone を流用しない（あちらは 11 位以上も Upper を返すため）。
            return rank <= 10 ? RankingRowTone.Upper : RankingRowTone.Normal;
        }

        public bool Equals(RankingRowStyle other)
        {
            return Size == other.Size
                && RankFontSize.Equals(other.RankFontSize)
                && NameFontSize.Equals(other.NameFontSize)
                && ScoreFontSize.Equals(other.ScoreFontSize)
                && RankOffset == other.RankOffset
                && RankSize == other.RankSize
                && NameOffset == other.NameOffset
                && NameSize == other.NameSize
                && ScoreOffset == other.ScoreOffset
                && ScoreSize == other.ScoreSize
                && Tone == other.Tone;
        }

        public override bool Equals(object obj) => obj is RankingRowStyle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Size.GetHashCode();
                hash = (hash * 397) ^ RankFontSize.GetHashCode();
                hash = (hash * 397) ^ NameFontSize.GetHashCode();
                hash = (hash * 397) ^ ScoreFontSize.GetHashCode();
                hash = (hash * 397) ^ RankOffset.GetHashCode();
                hash = (hash * 397) ^ RankSize.GetHashCode();
                hash = (hash * 397) ^ NameOffset.GetHashCode();
                hash = (hash * 397) ^ NameSize.GetHashCode();
                hash = (hash * 397) ^ ScoreOffset.GetHashCode();
                hash = (hash * 397) ^ ScoreSize.GetHashCode();
                hash = (hash * 397) ^ (int)Tone;
                return hash;
            }
        }
    }
}
