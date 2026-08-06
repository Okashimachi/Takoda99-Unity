// 客1体ぶんの表示。画像データは持たず、SO への参照1本だけを持ち、
// サーバーから届いた属性 + 現在の状態で毎回そこから引く。
// SO 未設定でも成立する（Image の既定の白い矩形が出る）ので、画像なしで配置調整ができる。

using System;
using DG.Tweening;
using Takoda99.Proto;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View.Customers
{
    /// <summary>
    /// 行列に並ぶ客1体。<see cref="CustomerQueueView"/> がプールして使い回す。
    /// 自分が持つのは「誰か（Id・属性）」「今どう見えるか（状態）」と SO への参照だけで、
    /// 画像の実体も座標表も持たない。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CustomerActor : MonoBehaviour
    {
        [Tooltip("全キャラ・全状態の画像を持つ SO。プレハブに1回設定すれば全インスタンスが共有する。")]
        [SerializeField] private CustomerSpriteLibrary _library;

        [Tooltip("客の絵を出す Image。未設定なら子から探す（このプレハブでは子の Panel が持つ）。")]
        [SerializeField] private Image _image;

        private RectTransform _rect;
        private Tween _tween;
        private bool _warnedNoLibrary;
        private bool _warnedNoSprite;

        /// <summary>この客の CustomerId。プールに戻ると空になる。</summary>
        public string CustomerId { get; private set; } = string.Empty;

        public CustomerAttribute Attribute { get; private set; }

        public CustomerVisualState State { get; private set; }

        public RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = (RectTransform)transform;
                }

                return _rect;
            }
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;

            if (_image == null)
            {
                // 絵はルートではなく子（Panel）が持つ構成なので、子まで含めて探す。
                // 非アクティブでプールされている状態でも取れるよう includeInactive: true。
                _image = GetComponentInChildren<Image>(true);
            }

            if (_image == null)
            {
                Debug.LogError(
                    $"{nameof(CustomerActor)}: Image が見つかりません。プレハブの Image 欄に、絵を出す Image を設定してください。",
                    this);
            }
        }

        private void OnDisable()
        {
            KillTween();
        }

        /// <summary>
        /// プールから取り出したときに呼ぶ。サーバーから届いた <paramref name="attribute"/> を受け取り、
        /// 対応する見た目をこの場で SO から引いて適用する。
        /// </summary>
        public void Initialize(string customerId, CustomerAttribute attribute)
        {
            KillTween();
            CustomerId = customerId;
            Attribute = attribute;
            State = CustomerVisualState.Queued;
            ApplySprite();
        }

        /// <summary>プールに戻すときに呼ぶ。</summary>
        public void Release()
        {
            KillTween();
            CustomerId = string.Empty;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 状態を変え、対応する画像を SO から引いて適用する。
        /// 状態が変わったときだけ引くので、毎フレーム呼んでも無駄がない。
        /// </summary>
        public void SetState(CustomerVisualState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            ApplySprite();
        }

        /// <summary>
        /// (属性, 状態) から画像を引いて <see cref="Image"/> に渡す。
        /// Sprite はフィールドに保持しない。SO 未設定なら白い矩形のままにする。
        /// </summary>
        private void ApplySprite()
        {
            if (_image == null)
            {
                return;
            }

            if (_library == null)
            {
                if (!_warnedNoLibrary)
                {
                    _warnedNoLibrary = true;
                    Debug.LogWarning(
                        $"{nameof(CustomerActor)}: Library（{nameof(CustomerSpriteLibrary)}）が未設定のため白い矩形のままです。" +
                        "プレハブの Library に Resources/CustomerSpriteLibrary.asset を設定してください。",
                        this);
                }

                return;
            }

            var sprite = _library.Resolve(Attribute, State);
            if (sprite == null && !_warnedNoSprite)
            {
                _warnedNoSprite = true;
                Debug.LogWarning(
                    $"{nameof(CustomerActor)}: {Attribute} / {State} に対応する画像が SO に設定されていません。",
                    this);
            }

            _image.sprite = sprite;
        }

        /// <summary>補間なしで即座にポーズを適用する（湧き出し時など）。</summary>
        public void ApplyPose(CustomerPose pose)
        {
            Rect.anchoredPosition = pose.AnchoredPosition;
            Rect.localScale = new Vector3(pose.Scale, pose.Scale, 1f);
        }

        /// <summary>指定ポーズまで移動する。進行中の移動があれば破棄して張り直す。</summary>
        public void MoveTo(CustomerPose pose, float duration, Ease ease)
        {
            KillTween();

            if (duration <= 0f)
            {
                ApplyPose(pose);
                return;
            }

            _tween = DOTween.Sequence()
                .Join(Rect.DOAnchorPos(pose.AnchoredPosition, duration))
                .Join(Rect.DOScale(new Vector3(pose.Scale, pose.Scale, 1f), duration))
                .SetEase(ease)
                .SetLink(gameObject);
        }

        /// <summary>
        /// 経路関数に沿って移動する（退店用）。位置・倍率が1本の Tween で同期して動く。
        /// </summary>
        public void MoveAlong(Func<float, CustomerPose> path, float duration, Ease ease, Action onComplete)
        {
            KillTween();

            if (duration <= 0f)
            {
                ApplyPose(path(1f));
                onComplete?.Invoke();
                return;
            }

            _tween = DOVirtual.Float(0f, 1f, duration, t => ApplyPose(path(t)))
                .SetEase(ease)
                .SetLink(gameObject)
                .OnComplete(() => onComplete?.Invoke());
        }

        public void KillTween()
        {
            _tween?.Kill();
            _tween = null;
        }
    }
}
