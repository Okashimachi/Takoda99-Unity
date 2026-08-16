// 仕様書: Unity/docs/.sdd/match-view/06-view-sample-data.md
//         Unity/docs/.sdd/cleanup/01-removed-views.md §6（本選のサンプルへ差し替え）
// 開発用。サンプル値で本選HUDを駆動する。本番シーンには置かない。
// サーバーの挙動を模したシミュレーション（スコア計算・順位決定・脱落判定）は書かない。
// 値はすべて「サーバーから届いたことにする」固定値であり、ここで導出しない。

using System.Collections.Generic;
using Takoda99.Client.State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Takoda99.View.Sample
{
    /// <summary>開発用。サンプル値で本選HUDを駆動する。本番シーンには置かない。</summary>
    public sealed class MainGameViewSampleDriver : MonoBehaviour
    {
        [SerializeField] private MainStoreView mainStore;
        [SerializeField] private TakoyakiStandView takoyakiStand;

        [Header("本選 HUD")]
        [SerializeField] private SelfRankView selfRank;
        [SerializeField] private Ranking.RankingPanelView rankingPanel;
        [SerializeField] private Ranking.BottomRankingPanelView bottomRankingPanel;  // ranking-view/05
        [SerializeField] private Ranking.CullCountdownPanelView cullPanel;
        [SerializeField] private Ranking.SpectatorRankingView spectatorRanking;
        [SerializeField] private Elimination.MassEliminationEffect massElim;

        [Header("お題のサンプル値")]
        [SerializeField] private string sampleHiragana = "たこやき";
        [SerializeField] private string sampleRoma = "takoyaki";
        [SerializeField] private int typedHiraganaLength;
        [SerializeField] private int typedRomaLength;
        [SerializeField] private int typedWordCount;
        [SerializeField] private int orderCount = 6;

        [Header("自店のサンプル値")]
        [SerializeField] private string selfStoreId = "store-050";
        [SerializeField] private int selfRankValue = 50;
        [SerializeField] private int selfScore = 1200;
        [SerializeField] private int aliveCount = 99;

        [Header("足切りのサンプル値")]
        [SerializeField] private int stageIndex = 1;
        [SerializeField] private int stageTotal = 6;
        [SerializeField] private int untilMs = 20_000;
        [SerializeField] private int cutLineRank = 51;
        [SerializeField] private bool selfAtRisk;

        [Header("ストレステスト")]
        [Tooltip("ON にすると毎フレーム全店のスコアをランダムに入れ替え、行の生成破棄が起きないかを見る。")]
        [SerializeField] private bool shuffleStress;

        /// <summary>サンプルの99行。実試合ではサーバーの RankingSnapshot 由来。</summary>
        private readonly List<RankingRow> rows = new List<RankingRow>(99);

        private CullWarning cull;

        /// <summary>
        /// 淘汰の各段階を抜けた直後の生存数（ranking-view/05 §5.1 の表）。
        /// 下位パネルの start = Max(0, aliveCount - 30) が全段階で正しいかを見るために使う。
        /// </summary>
        private static readonly int[] StageAliveCounts = { 99, 75, 55, 35, 20, 10 };

        private int stageAliveIndex;

        private void Start()
        {
            // 本番の試合中は絶対に動かさない。GameBootstrapper が生きている＝サーバーに繋がった実試合であり、
            // ここでサンプル値を流すと Renderer の描いた内容を上書きしてしまう
            // （Start は Renderer.OnEnable より後に走る）。
            if (Bootstrap.GameBootstrapper.Instance != null)
            {
                Debug.LogWarning(
                    $"{nameof(MainGameViewSampleDriver)}: 実試合中のため自身を無効化します。" +
                    "このコンポーネントは開発用で、本番シーンでは非アクティブにしておくこと。", this);
                gameObject.SetActive(false);
                return;
            }

            BuildRanking();
            ApplyCull();
            Apply();
        }

        /// <summary>99行を作る。1位が最高スコアになるよう単調に散らす。</summary>
        private void BuildRanking()
        {
            rows.Clear();
            for (var i = 1; i <= 99; i++)
            {
                var id = $"store-{i:000}";
                rows.Add(new RankingRow
                {
                    StoreId = id,
                    DisplayName = id == selfStoreId ? "じぶん" : $"店{i:000}",
                    Rank = i,
                    Score = (100 - i) * 120,
                    Alive = true,
                });
            }
        }

        private void Update()
        {
            if (shuffleStress)
            {
                ShuffleScores();
                Apply();
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                // 自分の順位を上げる（1位側へ）。
                selfRankValue = Mathf.Max(1, selfRankValue - 5);
                selfScore += 500;
                Apply();
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                selfRankValue = Mathf.Min(99, selfRankValue + 5);
                selfScore -= 500;
                Apply();
            }

            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                // 自分が淘汰圏内に入った／出た。画面全体アラートの確認。
                selfAtRisk = !selfAtRisk;
                ApplyCull();
            }

            if (keyboard.digit4Key.wasPressedThisFrame)
            {
                // 次のステージへ。秒読みがリセットされることの確認。
                stageIndex = Mathf.Min(stageTotal, stageIndex + 1);
                ApplyCull();
            }

            if (keyboard.digit5Key.wasPressedThisFrame)
            {
                typedWordCount = Mathf.Max(0, typedWordCount - 1);
                Apply();
            }

            if (keyboard.digit6Key.wasPressedThisFrame)
            {
                typedWordCount++;
                Apply();
            }

            if (keyboard.digit7Key.wasPressedThisFrame)
            {
                AdjustTypedRomaLength(-1);
                Apply();
            }

            if (keyboard.digit8Key.wasPressedThisFrame)
            {
                AdjustTypedRomaLength(1);
                Apply();
            }

            if (keyboard.digit9Key.wasPressedThisFrame)
            {
                // 24件の一斉脱落。**Play が1回・SEが1回**であることの確認。
                EliminateBatch(24, includesSelf: false);
            }

            if (keyboard.digit0Key.wasPressedThisFrame)
            {
                // 自店を含む一斉脱落 → 脱落モーダル → 観戦の全員順位。
                EliminateBatch(10, includesSelf: true);
                spectatorRanking?.Open(BuildState());
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                // 淘汰の段階を1つ進める（99→75→55→35→20→10）。
                // 下位パネルが「生存30」→「生存20＋脱落10」→「生存10＋脱落20」へ
                // 連続的に変わることを見る（ranking-view/05 §5.1・§6）。
                AdvanceCullStage();
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                // 秒読みを残り5秒へ縮める。中央カウントダウン（ranking-view/02 §6）の確認。
                // 3キーで selfAtRisk を切り替えると Danger / Caution の出し分けも見られる。
                untilMs = ValueObjects.CullFinalCountdownState.DefaultWindowMs;
                ApplyCull();

                Debug.Log($"{nameof(MainGameViewSampleDriver)}: 秒読みを残り5秒に縮めました（selfAtRisk={selfAtRisk}）。");
            }
        }

        /// <summary>
        /// 淘汰の次の段階へ進める（ranking-view/05 §8 のテスト観点11）。
        /// 生存数を段階どおりに落とし、下位パネルの表示内容が自然に切り替わるかを見る。
        /// </summary>
        private void AdvanceCullStage()
        {
            if (stageAliveIndex >= StageAliveCounts.Length - 1)
            {
                Debug.Log($"{nameof(MainGameViewSampleDriver)}: 最終段階（生存{aliveCount}）に到達済みです。");
                return;
            }

            stageAliveIndex++;
            var target = StageAliveCounts[stageAliveIndex];
            var toKill = Mathf.Max(0, aliveCount - target);

            if (toKill > 0)
            {
                // EliminateBatch が aliveCount を減らし、集約演出を1回だけ再生し、Apply する。
                EliminateBatch(toKill, includesSelf: false);
            }

            stageIndex = Mathf.Min(stageTotal, stageIndex + 1);
            ApplyCull();

            Debug.Log($"{nameof(MainGameViewSampleDriver)}: 段階 {stageIndex}/{stageTotal} → 生存 {aliveCount}");
        }

        /// <summary>行の生成破棄が起きないかを見るためのストレス。実試合では起こらない頻度で動かす。</summary>
        private void ShuffleScores()
        {
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i] = new RankingRow
                {
                    StoreId = rows[i].StoreId,
                    DisplayName = rows[i].DisplayName,
                    Rank = rows[i].Rank,
                    Score = Random.Range(0, 12_000),
                    Alive = rows[i].Alive,
                };
            }

            rows.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i] = new RankingRow
                {
                    StoreId = rows[i].StoreId,
                    DisplayName = rows[i].DisplayName,
                    Rank = i + 1,
                    Score = rows[i].Score,
                    Alive = rows[i].Alive,
                };
            }
        }

        /// <summary>下位から指定件数を脱落させ、集約演出を1回だけ再生する。</summary>
        private void EliminateBatch(int count, bool includesSelf)
        {
            var eliminated = 0;
            for (var i = rows.Count - 1; i >= 0 && eliminated < count; i--)
            {
                if (!rows[i].Alive)
                {
                    continue;
                }

                rows[i] = new RankingRow
                {
                    StoreId = rows[i].StoreId,
                    DisplayName = rows[i].DisplayName,
                    Rank = rows[i].Rank,
                    Score = rows[i].Score,
                    Alive = false,
                };
                eliminated++;
            }

            aliveCount = Mathf.Max(0, aliveCount - eliminated);

            // ★件数だけを渡す。1件ずつループして演出を呼ばない。
            massElim?.Play(stageIndex, eliminated, includesSelf);
            Apply();
        }

        private void AdjustTypedRomaLength(int delta)
        {
            var maxRoma = string.IsNullOrEmpty(sampleRoma) ? 0 : sampleRoma.Length;
            typedRomaLength = Mathf.Clamp(typedRomaLength + delta, 0, maxRoma);

            // ひらがなの確定文字数はローマ字との比率で連動させる（打鍵判定はしない）。
            var maxHiragana = string.IsNullOrEmpty(sampleHiragana) ? 0 : sampleHiragana.Length;
            typedHiraganaLength = maxRoma > 0
                ? Mathf.Clamp(Mathf.RoundToInt((float)typedRomaLength / maxRoma * maxHiragana), 0, maxHiragana)
                : 0;
        }

        /// <summary>サーバーから届いたことにする ClientState を組む。</summary>
        private ClientState BuildState()
        {
            return new ClientState
            {
                Phase = ClientPhase.InMatch,
                SelfStoreId = selfStoreId,
                Rank = selfRankValue,
                Score = selfScore,
                AliveCount = aliveCount,
                Alive = true,
                Ranking = new RankingTable { Rows = new List<RankingRow>(rows) },
                Cull = cull,
            };
        }

        private void ApplyCull()
        {
            cull = new CullWarning
            {
                UntilMs = untilMs,
                ReceivedAtLocalMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d),
                StageIndex = stageIndex,
                StageTotal = stageTotal,
                CutLineRank = cutLineRank,
                SelfAtRisk = selfAtRisk,

                // サーバーは淘汰圏内の店を CutStoreIds にも入れて送る。自店が圏内のときは
                // 自店IDを混ぜておかないと、自店の色（Doomed）を確認できない。
                CutStoreIds = selfAtRisk
                    ? new[] { selfStoreId, "store-097", "store-098", "store-099" }
                    : new[] { "store-097", "store-098", "store-099" },
            };

            var state = BuildState();
            cullPanel?.SetWarning(cull, state);
            cullPanel?.OnWarningReceived(cull);
        }

        private void Apply()
        {
            if (mainStore != null)
            {
                mainStore.SetWord(sampleHiragana, sampleRoma);
                mainStore.SetTypedProgress(typedHiraganaLength, typedRomaLength);
                mainStore.SetOrderProgress(typedWordCount, orderCount);
            }

            if (takoyakiStand != null)
            {
                takoyakiStand.SetOrderCount(orderCount);
                takoyakiStand.SetTypedWordCount(typedWordCount);
            }

            var state = BuildState();
            // 順位テキストの色もサンプル state から決まる（1キーで1〜3位＝金銀銅、3キーで淘汰確定＝赤）。
            selfRank?.Apply(state);
            rankingPanel?.Apply(state);
            bottomRankingPanel?.Apply(state);
            spectatorRanking?.Apply(state);
        }
    }
}
