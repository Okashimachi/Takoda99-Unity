// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md §7
// Result シーンの入口。テストモードの切り替えをここ1箇所に持ち、
// たこ焼き生成・成績一覧の両方へ同じ出所のデータを流す。
//
// ⚠ このシーンに来た時点で MatchEnd が届いているとは限らない。自店が脱落した場合、
// 試合はまだ続いており（自店は Spectating）、MainGame の脱落モーダルの Next ボタンから
// GameBootstrapper.GoToResult() で先にこの画面へ来る。MatchEnd はそのあと届く。
// そのため一度読むだけにせず、Store を購読して結果の到着を待つ。

using System;
using Takoda99.Client.State;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>Result シーン。成績データの注入と、Title ボタンのシーン遷移を担う。</summary>
    public sealed class ResultScreenView : MonoBehaviour
    {
        [SerializeField] private Button titleButton;
        [SerializeField] private TakoyakiCreator takoyakiCreator;
        [SerializeField] private ResultStatsBoardView statsBoard;

        [Header("テストモード")]
        [Tooltip("ON にすると、サーバーの受信値ではなく ResultSampleData のサンプルを全要素へ注入する（たこ焼き生成を含む）。")]
        [SerializeField] private bool testMode;

        [Tooltip("テストモードで生成するたこ焼きの個数（＝提供数）。成績表示もこの値を基準に組み立てる。")]
        [SerializeField] private int testTakoyakiCount = ResultSampleData.DefaultServedCount;

        private IDisposable subscription;
        private bool hasRenderedResult;
        private bool hasRenderedPending;

        private void OnEnable()
        {
            if (testMode)
            {
                ApplySample();
                BindTitleButton();
                return;
            }

            if (Bootstrap.GameBootstrapper.Instance == null)
            {
                Debug.LogError($"{nameof(ResultScreenView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が見つかりません。Boot シーンから再生するか、テストモードを ON にしてください。", this);
                if (titleButton != null)
                {
                    titleButton.interactable = false;
                }
                return;
            }

            var store = Bootstrap.GameBootstrapper.Instance.Store;
            subscription = store.Subscribe(HandleStateChanged);
            HandleStateChanged(store.State);

            BindTitleButton();
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;

            if (titleButton != null)
            {
                titleButton.onClick.RemoveListener(OnTitleClicked);
            }
        }

        private void BindTitleButton()
        {
            if (titleButton != null && Bootstrap.GameBootstrapper.Instance != null)
            {
                titleButton.onClick.AddListener(OnTitleClicked);
            }
        }

        private void ApplySample()
        {
            var result = ResultSampleData.CreateResult(testTakoyakiCount);

            if (statsBoard != null)
            {
                statsBoard.Show(result, ResultSampleData.CreateStores(), ResultSampleData.SelfStoreId);
            }

            if (takoyakiCreator != null)
            {
                takoyakiCreator.SetTakoyakiCount(result.Stats.ServedCount);
            }
        }

        /// <summary>
        /// MatchEnd がまだなら枠だけ（待ち表示）を出し、届いた時点で本番の値へ差し替える。
        /// 差し替えは1度だけ。以降の状態変化でたこ焼きの生成が最初からやり直しになるのを防ぐ。
        /// </summary>
        private void HandleStateChanged(ClientState state)
        {
            if (hasRenderedResult)
            {
                return;
            }

            var result = state.Result;

            // 待ち表示は最初の1回だけ。観戦中は他店の更新が流れ続けるので、そのたびに組み直すと
            // TakoyakiCreator の表示演出が毎回リセットされ、いつまでも何も出てこなくなる。
            if (result == null && hasRenderedPending)
            {
                return;
            }

            if (statsBoard != null)
            {
                statsBoard.Show(result, state.Stores, state.SelfStoreId);
            }

            if (takoyakiCreator != null)
            {
                // Rank / Others / Buttons の表示は TakoyakiCreator の生成完了が起点なので、
                // MatchEnd 待ちのあいだも 0 個で呼んでおく。呼ばないと Title ボタンごと出てこない。
                // スコア（提供数）が 0 の店もいるため、0 でも必ず呼ぶ点は結果到着後も同じ。
                takoyakiCreator.SetTakoyakiCount(result?.Stats.ServedCount ?? 0);
            }

            if (result == null)
            {
                hasRenderedPending = true;
            }
            else
            {
                hasRenderedResult = true;
            }
        }

        private void OnTitleClicked()
        {
            Bootstrap.GameBootstrapper.Instance.BackToTitle();
        }
    }
}
