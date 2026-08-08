// root/CustomerCanvas/Order の注文吹き出し。行列の先頭が入れ替わった瞬間に注文文句を出し、
// 一定時間で引っ込める。客ごとに生成せず、この1つを次の客へ使い回す。

using TMPro;
using UnityEngine;

namespace Takoda99.View.Customers
{
    /// <summary>先頭客の注文文句（「8こください」等）を出す吹き出し。1つを使い回す。</summary>
    public sealed class CustomerOrderBubbleView : MonoBehaviour
    {
        [Tooltip("注文文句を出すテキスト（Order/Text）。")]
        [SerializeField] private TextMeshProUGUI text;

        [Tooltip("出したまま表示し続ける秒数。")]
        [SerializeField] private float visibleDurationSec = 2f;

        [Tooltip("サーバーから文面が来ないときのひな形。{0} が注文個数に置き換わる。")]
        [SerializeField] private string fallbackFormat = "{0}こください";

        /// <summary>表示中の客。同じ客で二重に出し直さないために持つ。</summary>
        private string shownCustomerId;

        private float remainingSec;

        private void Awake()
        {
            if (text == null)
            {
                text = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (text == null)
            {
                Debug.LogError($"{nameof(CustomerOrderBubbleView)}.{nameof(text)} が未設定です。注文文句は表示されません。", this);
            }

            Hide();
        }

        private void Update()
        {
            if (remainingSec <= 0f)
            {
                return;
            }

            remainingSec -= Time.deltaTime;
            if (remainingSec <= 0f)
            {
                // 客はまだ先頭に居るが、吹き出しだけ引っ込める（次の客で shownCustomerId が変わる）。
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 先頭客の注文を表示する。同じ客で繰り返し呼んでも出し直さない。
        /// </summary>
        /// <param name="customerId">先頭客。null なら <see cref="Hide"/> と同じ。</param>
        /// <param name="orderCount">注文個数。<paramref name="orderText"/> が無いときの文面に使う。</param>
        /// <param name="orderText">サーバーが文面を配信していればそれをそのまま出す。無ければ null を渡す。</param>
        public void Show(string customerId, int orderCount, string orderText = null)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                Hide();
                return;
            }

            if (customerId == shownCustomerId)
            {
                return;
            }

            shownCustomerId = customerId;

            if (text != null)
            {
                // 契約に注文文句のフィールドが増えるまでは常にひな形側を通る。
                text.text = string.IsNullOrEmpty(orderText)
                    ? string.Format(fallbackFormat, orderCount)
                    : orderText;
            }

            remainingSec = visibleDurationSec;
            gameObject.SetActive(true);
        }

        /// <summary>吹き出しを消す。行列が空になったときや脱落時に呼ぶ。</summary>
        public void Hide()
        {
            shownCustomerId = null;
            remainingSec = 0f;
            gameObject.SetActive(false);
        }
    }
}
