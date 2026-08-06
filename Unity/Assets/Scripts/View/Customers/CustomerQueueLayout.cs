// 客の行列・退店経路の配置をシーン上のアンカーだけで決めるレイアウト定義。表示のみ（経営ロジックは持たない）。
// 客1体ごとに座標データを持たせないため、位置と大きさはすべてここで算出して配る。

using UnityEngine;

namespace Takoda99.View.Customers
{
    /// <summary>行列上の1点における客の見え方（位置・大きさ・重なり順）。</summary>
    public readonly struct CustomerPose
    {
        /// <summary>コンテナ（アンカーと同じ親）のローカル座標。</summary>
        public Vector2 AnchoredPosition { get; }

        /// <summary>奥行き表現のための大きさ倍率。</summary>
        public float Scale { get; }

        /// <summary>0..1 の進行度。手前ほど大きい値。UI の重なり順に使う。</summary>
        public float Depth { get; }

        public CustomerPose(Vector2 anchoredPosition, float scale, float depth)
        {
            AnchoredPosition = anchoredPosition;
            Scale = scale;
            Depth = depth;
        }
    }

    /// <summary>
    /// 主画面左の行列と、右へ抜ける退店経路の配置を一括で決める。
    /// 先頭・末尾のアンカーをシーンビューでドラッグすると、間の客は等間隔に自動配置される。
    /// </summary>
    /// <remarks>
    /// アンカー（<see cref="_queueHead"/> 等）と客は<b>同じ親</b>の下に置くこと。
    /// 算出値は親のローカル座標（<c>anchoredPosition</c>）として返す。
    /// </remarks>
    public sealed class CustomerQueueLayout : MonoBehaviour
    {
        [Header("行列（主画面左）")]
        [Tooltip("行列の先頭。対応中の客が立つ位置。")]
        [SerializeField] private RectTransform _queueHead;

        [Tooltip("行列の末尾。ここが「最大描画人数」番目のスロットになる。")]
        [SerializeField] private RectTransform _queueTail;

        [Tooltip("同時に描画する最大人数。サーバー側の行列がこれより長くても、あふれた客は描画しない。")]
        [Range(1, 20)]
        [SerializeField] private int _visibleCapacity = 5;

        [Tooltip("先頭の客の大きさ倍率（手前）。")]
        [Min(0.01f)]
        [SerializeField] private float _queueHeadScale = 1f;

        [Tooltip("末尾の客の大きさ倍率（奥）。")]
        [Min(0.01f)]
        [SerializeField] private float _queueTailScale = 0.6f;

        [Header("退店（主画面右へ）")]
        [Tooltip("退店の始点。提供・離脱した客がここから歩き出す。")]
        [SerializeField] private RectTransform _leaveStart;

        [Tooltip("退店の終点。画面外に置く。ここに着いたらプールへ返す。")]
        [SerializeField] private RectTransform _leaveEnd;

        [Min(0.01f)]
        [SerializeField] private float _leaveStartScale = 1f;

        [Min(0.01f)]
        [SerializeField] private float _leaveEndScale = 1.2f;

        [Header("動き")]
        [Tooltip("行列が1つ詰まるときの移動時間（秒）。")]
        [Min(0f)]
        [SerializeField] private float _advanceDuration = 0.35f;

        [Tooltip("退店の移動時間（秒）。")]
        [Min(0f)]
        [SerializeField] private float _leaveDuration = 0.8f;

        [Tooltip("喜び（提供直後）を見せてから歩き出すまでの時間（秒）。")]
        [Min(0f)]
        [SerializeField] private float _delightedDuration = 0.5f;

        [Header("ギズモ")]
        [SerializeField] private bool _drawGizmos = true;

        /// <summary>同時に描画する最大人数。行列がこれを超えたぶんは客オブジェクトを作らない。</summary>
        public int VisibleCapacity => _visibleCapacity;

        public float AdvanceDuration => _advanceDuration;

        public float LeaveDuration => _leaveDuration;

        public float DelightedDuration => _delightedDuration;

        /// <summary>
        /// 客を生成する親。アンカーと同じ親でなければ <c>anchoredPosition</c> の基準がズレるため、
        /// 個別に設定させず先頭アンカーの親から引く。
        /// </summary>
        public RectTransform Container => _queueHead != null ? _queueHead.parent as RectTransform : null;

        /// <summary>
        /// アンカー基準で計算した座標を、<paramref name="target"/> のローカル座標へ移す。
        /// 客の親がアンカーの親と違っていても位置が合うようにする。
        /// </summary>
        public Vector2 ConvertToSpaceOf(RectTransform target, Vector2 anchorSpacePosition)
        {
            var anchorParent = Container;
            if (anchorParent == null || target == null || target == anchorParent)
            {
                return anchorSpacePosition;
            }

            var world = anchorParent.TransformPoint(anchorSpacePosition);
            return target.InverseTransformPoint(world);
        }

        /// <summary>ポーズ全体を <paramref name="target"/> のローカル座標へ移す。</summary>
        public CustomerPose ConvertToSpaceOf(RectTransform target, CustomerPose pose)
        {
            return new CustomerPose(
                ConvertToSpaceOf(target, pose.AnchoredPosition),
                pose.Scale,
                pose.Depth);
        }

        /// <summary>アンカーが1つでも未設定なら false。未設定のまま使うと原点に固まる。</summary>
        public bool IsConfigured =>
            _queueHead != null && _queueTail != null && _leaveStart != null && _leaveEnd != null;

        /// <summary>
        /// 行列 <paramref name="index"/> 番目（0 = 先頭）の見え方。
        /// 先頭〜末尾を等間隔に割り、位置と倍率を同じ比率で補間する。
        /// </summary>
        public CustomerPose QueuePose(int index)
        {
            var t = QueueRatio(index);
            return new CustomerPose(
                Vector2.Lerp(AnchoredOf(_queueHead), AnchoredOf(_queueTail), t),
                Mathf.Lerp(_queueHeadScale, _queueTailScale, t),
                1f - t);
        }

        /// <summary>行列の外（末尾のさらに1つ後ろ）。来店した客の湧き出し位置に使う。</summary>
        public CustomerPose QueueEntryPose()
        {
            return QueuePose(_visibleCapacity);
        }

        /// <summary>退店経路上の <paramref name="t"/>（0 = 始点、1 = 終点）での見え方。</summary>
        public CustomerPose LeavePose(float t)
        {
            t = Mathf.Clamp01(t);
            return new CustomerPose(
                Vector2.Lerp(AnchoredOf(_leaveStart), AnchoredOf(_leaveEnd), t),
                Mathf.Lerp(_leaveStartScale, _leaveEndScale, t),
                // 退店中の客は行列より常に手前を通す。
                1f + t);
        }

        /// <summary>
        /// <see cref="CustomerPose.Depth"/> から UI の兄弟インデックスを決める。
        /// uGUI は <c>sortingOrder</c> ではなく階層順で重なりが決まるため、手前ほど後ろの兄弟にする。
        /// </summary>
        public static int CompareDepth(CustomerPose a, CustomerPose b)
        {
            return a.Depth.CompareTo(b.Depth);
        }

        /// <summary>先頭〜末尾を 0..1 に正規化した比率。最大描画人数を超える添字は末尾のさらに外側へ伸ばす。</summary>
        private float QueueRatio(int index)
        {
            if (_visibleCapacity <= 1)
            {
                return 0f;
            }

            return Mathf.Max(0, index) / (float)(_visibleCapacity - 1);
        }

        private static Vector2 AnchoredOf(RectTransform anchor)
        {
            return anchor != null ? anchor.anchoredPosition : Vector2.zero;
        }

        // ── シーンビューでのプレビュー ────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || !IsConfigured)
            {
                return;
            }

            var parent = _queueHead.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            // 行列：スロットごとに、倍率をそのまま半径に反映した円を描く。
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(WorldOf(parent, QueuePose(0)), WorldOf(parent, QueuePose(_visibleCapacity - 1)));
            for (var i = 0; i < _visibleCapacity; i++)
            {
                var pose = QueuePose(i);
                Gizmos.DrawWireSphere(WorldOf(parent, pose), GizmoRadius(parent, pose.Scale));
            }

            // 来店の湧き出し位置。
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            var entry = QueueEntryPose();
            Gizmos.DrawWireSphere(WorldOf(parent, entry), GizmoRadius(parent, entry.Scale));

            // 退店経路：始点・中間・終点。
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(WorldOf(parent, LeavePose(0f)), WorldOf(parent, LeavePose(1f)));
            for (var i = 0; i <= 4; i++)
            {
                var pose = LeavePose(i / 4f);
                Gizmos.DrawWireSphere(WorldOf(parent, pose), GizmoRadius(parent, pose.Scale));
            }
        }

        private static Vector3 WorldOf(RectTransform parent, CustomerPose pose)
        {
            return parent.TransformPoint(pose.AnchoredPosition);
        }

        /// <summary>Canvas のスケールに左右されない見た目の半径にする。</summary>
        private static float GizmoRadius(RectTransform parent, float scale)
        {
            return 40f * scale * parent.lossyScale.x;
        }
    }
}
