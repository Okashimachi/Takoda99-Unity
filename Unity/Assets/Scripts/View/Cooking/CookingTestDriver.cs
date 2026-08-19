// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md §7
// 調理アニメーションをサーバー無しで確認するための動作確認用ドライバ。
//
// **本番の経路ではない。** 本番は Renderer が TypingJudge の結果を配る。
// これは尺・振れ幅を企画が実機で詰めるための手回しであり、シーンに常設しない
// （確認するときだけ有効にし、コミット時は無効にしておく）。

using UnityEngine;

namespace Takoda99.View.Cooking
{
    /// <summary>調理アニメーションの手動確認用。空の GameObject にアタッチして使う。</summary>
    public sealed class CookingTestDriver : MonoBehaviour
    {
        [SerializeField] private TakoyakiStandView takoyakiStand;
        [SerializeField] private HandView hand;

        [Tooltip("1単語あたりの打鍵数。この回数で1個が完成する。")]
        [SerializeField] private int keysPerWord = 7;

        [Tooltip("注文個数。この個数で提供演出が出る。")]
        [SerializeField] private int orderCount = 8;

        [Tooltip("自動打鍵の間隔（ミリ秒）。企画書の想定は100〜130ms。")]
        [SerializeField] private int keyIntervalMs = 130;

        [Tooltip("自動打鍵でミスを混ぜる確率（0〜1）。")]
        [SerializeField, Range(0f, 1f)] private float missRate = 0.1f;

        [Tooltip("有効にすると keyIntervalMs ごとに自動で打鍵する。切ると Space キーで1打ずつ進む。")]
        [SerializeField] private bool autoType = true;

        private int keysInWord;
        private int wordsDone;
        private float nextKeyAt;

        private void Start()
        {
            if (takoyakiStand == null)
            {
                Debug.LogError($"{nameof(CookingTestDriver)}.{nameof(takoyakiStand)} が未割り当てです。", this);
                enabled = false;
                return;
            }

            takoyakiStand.BeginOrder(orderCount);
        }

        private void Update()
        {
            if (autoType)
            {
                if (Time.time < nextKeyAt)
                {
                    return;
                }

                nextKeyAt = Time.time + CookingAnimationSettings.ToSeconds(keyIntervalMs);
                TypeOnce(Random.value < missRate);
                return;
            }

            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TypeOnce(UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed);
            }
        }

        /// <summary>1打ぶん進める。<paramref name="isMiss"/> が true ならミス打鍵として扱う。</summary>
        private void TypeOnce(bool isMiss)
        {
            if (isMiss)
            {
                // ミスは単語を進めない（本番の TypingJudge も同じ。バッファは巻き戻さない）。
                // 鉄板の見た目は変わらず、盛り付けの出来にだけ効く。
                hand?.PlayMissReaction();
                takoyakiStand.OnKeyTyped(true, Progress());
                return;
            }

            keysInWord++;
            hand?.PlayKeyReaction();

            if (keysInWord >= keysPerWord)
            {
                takoyakiStand.OnKeyTyped(false, 1f);
                // 注文ぶん打ち切ると TakoyakiStandView 側が一斉盛り付け→提供へ入る。
                takoyakiStand.OnWordCleared();
                keysInWord = 0;
                wordsDone++;

                // 提供が終わったら次の客として仕切り直す（BeginOrder は演出の完了を待つ）。
                if (wordsDone >= orderCount)
                {
                    wordsDone = 0;
                    takoyakiStand.BeginOrder(orderCount);
                }

                return;
            }

            takoyakiStand.OnKeyTyped(false, Progress());
        }

        private float Progress() => keysPerWord <= 0 ? 1f : (float)keysInWord / keysPerWord;
    }
}
