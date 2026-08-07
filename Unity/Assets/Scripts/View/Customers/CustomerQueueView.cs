// 客の行列全体の表示を1つで束ねる。ClientState.Queue を見て、客オブジェクトの生成・配置・退店を行う。
// 「どこに居るか」はここと Layout の担当で、「どう見えるか」は CustomerActor が SO から自分で引く。

using System.Collections.Generic;
using DG.Tweening;
using Takoda99.Client.State;
using Takoda99.Proto;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Customers
{
    /// <summary>
    /// 行列1人ぶんの、表示に必要な最小限の受信値。
    /// <c>ClientState.CustomerEntry</c> をここへ写すことで、表示側がサーバー状態の型に依存しなくなる
    /// （テストモードから同じ経路を叩けるようにするため）。
    /// </summary>
    public readonly struct CustomerQueueItem
    {
        public string CustomerId { get; }

        public CustomerAttribute Attribute { get; }

        /// <summary>我慢の最大値（ms）。怒り表示の推定にだけ使う。</summary>
        public int PatienceMaxMs { get; }

        /// <summary>来店を受信したローカル時刻（<c>Time.realtimeSinceStartupAsDouble</c> 基準の ms）。</summary>
        public long ArrivedAtLocalMs { get; }

        public CustomerQueueItem(string customerId, CustomerAttribute attribute, int patienceMaxMs, long arrivedAtLocalMs)
        {
            CustomerId = customerId;
            Attribute = attribute;
            PatienceMaxMs = patienceMaxMs;
            ArrivedAtLocalMs = arrivedAtLocalMs;
        }
    }

    /// <summary>
    /// 自店の行列の表示。<see cref="CustomerQueueLayout"/> が決めた位置へ
    /// プールした <see cref="CustomerActor"/> を配置し、属性と状態を渡す。画像そのものには触れない。
    /// </summary>
    public sealed class CustomerQueueView : MonoBehaviour
    {
        [Tooltip("行列と退店経路の配置。")]
        [SerializeField] private CustomerQueueLayout _layout;

        [Tooltip("客1体のプレハブ。画像の SO 参照はプレハブ側（CustomerActor）が持つ。")]
        [SerializeField] private CustomerActor _actorPrefab;

        [Tooltip("客オブジェクトを生成する親。未設定なら Layout のアンカーの親を使う。\n" +
                 "アンカーと違う親を指定すると anchoredPosition の基準がズレるので注意。")]
        [SerializeField] private RectTransform _container;

        [Header("イージング")]
        [SerializeField] private Ease _advanceEase = Ease.OutCubic;
        [SerializeField] private Ease _leaveEase = Ease.InOutSine;

        /// <summary>表示中の客。行列の並び順そのもの（添字0 = 先頭）。</summary>
        private readonly List<CustomerActor> _visible = new List<CustomerActor>();

        /// <summary>表示中の客に対応する受信データ。我慢ゲージの推定に使う（客側には持たせない）。</summary>
        private readonly List<CustomerQueueItem> _visibleEntries = new List<CustomerQueueItem>();

        /// <summary><see cref="ClientState"/> からの写し取り用。毎回の確保を避けて使い回す。</summary>
        private readonly List<CustomerQueueItem> _scratch = new List<CustomerQueueItem>();

        private readonly Dictionary<string, CustomerActor> _byId = new Dictionary<string, CustomerActor>();
        private readonly Stack<CustomerActor> _pool = new Stack<CustomerActor>();

        /// <summary>行列から消えたが、まだ退店演出を始めていない客。値は「提供済み（＝喜んでから帰る）」か。</summary>
        private readonly Dictionary<string, bool> _pendingExits = new Dictionary<string, bool>();

        private readonly List<CustomerActor> _exiting = new List<CustomerActor>();

        /// <summary>
        /// 客を生成する親。インスペクタ指定が最優先で、未設定なら Layout のアンカーの親、
        /// それも取れなければ自分自身。
        /// </summary>
        private RectTransform Container
        {
            get
            {
                if (_container != null)
                {
                    return _container;
                }

                var fromLayout = _layout != null ? _layout.Container : null;
                return fromLayout != null ? fromLayout : (RectTransform)transform;
            }
        }

        private void Awake()
        {
            // 未設定のまま黙って何も出ないのが一番デバッグしづらいので、起動時に必ず知らせる。
            if (_layout == null)
            {
                Debug.LogError($"{nameof(CustomerQueueView)}: Layout（{nameof(CustomerQueueLayout)}）が未設定です。客は1人も表示されません。", this);
            }

            if (_actorPrefab == null)
            {
                Debug.LogError($"{nameof(CustomerQueueView)}: Actor Prefab（{nameof(CustomerActor)}）が未設定です。客は1人も表示されません。", this);
            }

            if (_layout != null && !_layout.IsConfigured)
            {
                Debug.LogError($"{nameof(CustomerQueueView)}: {nameof(CustomerQueueLayout)} のアンカー（QueueHead / QueueTail / LeaveStart / LeaveEnd）が未設定です。客が全員原点に重なります。", this);
            }

            Prewarm();
        }

        /// <summary>
        /// レイアウトが返すアンカー基準のポーズを、客の親（<see cref="Container"/>）の座標系へ移す。
        /// 客の親とアンカーの親が違っていてもよいのはこの変換のため。
        /// </summary>
        private CustomerPose Localize(CustomerPose pose)
        {
            return _layout.ConvertToSpaceOf(Container, pose);
        }

        /// <summary>行列の最大人数ぶん＋退店中のぶんを、あらかじめ作っておく（試合中に Instantiate しない）。</summary>
        private void Prewarm()
        {
            if (_actorPrefab == null || _layout == null)
            {
                return;
            }

            var count = _layout.VisibleCapacity + 2;
            for (var i = 0; i < count; i++)
            {
                _pool.Push(CreateActor());
            }
        }

        private CustomerActor CreateActor()
        {
            var actor = Instantiate(_actorPrefab, Container);
            actor.gameObject.SetActive(false);
            return actor;
        }

        /// <summary>
        /// 調査用。この行列を今どこが駆動しているか（Renderer か TestDriver か）。
        /// ClientEventLog に載せて「画面の客の出所」を判別するためだけに持つ。
        /// </summary>
        public string DriverName { get; set; } = "unknown";

        // ── 状態の反映 ────────────────────────────────────────────────

        /// <summary>
        /// <c>Renderer</c> から毎 state 変化で呼ぶ。
        /// <see cref="ClientState"/> を表示に必要な最小限へ写して <see cref="Apply(IReadOnlyList{CustomerQueueItem}, string)"/> に渡す。
        /// </summary>
        public void Apply(ClientState state)
        {
            DriverName = "Renderer(server)";
            _scratch.Clear();
            foreach (var entry in state.Queue)
            {
                _scratch.Add(new CustomerQueueItem(
                    entry.View.CustomerId,
                    entry.View.Attribute,
                    entry.View.PatienceMaxMs,
                    entry.ArrivedAtLocalMs));
            }

            Apply(_scratch, state.CurrentOrder?.CustomerId);
        }

        /// <summary>
        /// 行列そのものを渡す表示の本体。サーバー状態に依存しないので、
        /// <see cref="CustomerQueueTestDriver"/> からも同じ経路で駆動できる。
        /// </summary>
        /// <param name="queue">行列。添字0が先頭。</param>
        /// <param name="servingCustomerId">対応中（注文を受け終わって提供待ち）の客。居なければ null。</param>
        public void Apply(IReadOnlyList<CustomerQueueItem> queue, string servingCustomerId)
        {
            if (_layout == null || _actorPrefab == null)
            {
                return;
            }

            var limit = Mathf.Min(queue.Count, _layout.VisibleCapacity);

            // 1. 消えた客を拾う。行列自体からいなくなったのか、表示上限からあふれただけなのかを区別する。
            for (var i = _visible.Count - 1; i >= 0; i--)
            {
                var actor = _visible[i];
                var index = IndexInQueue(queue, actor.CustomerId);

                if (index >= 0 && index < limit)
                {
                    continue;
                }

                _visible.RemoveAt(i);
                _visibleEntries.RemoveAt(i);
                _byId.Remove(actor.CustomerId);

                if (index >= 0)
                {
                    // 行列には残っているが表示枠外。演出なしで静かに引っ込める。
                    ReturnToPool(actor);
                    continue;
                }

                DebugUI.ClientEventLog.Add(
                    DebugUI.ClientEventSource.View,
                    "LEAVE",
                    $"customerId={actor.CustomerId} driver={DriverName}");

                // 行列から消えた ＝ 提供完了か離脱。どちらかは Renderer からの通知で決まるので、
                // この時点では退店待ちに積むだけにして LateUpdate まで判断を遅らせる。
                _exiting.Add(actor);
                if (!_pendingExits.ContainsKey(actor.CustomerId))
                {
                    _pendingExits.Add(actor.CustomerId, false);
                }
            }

            // 2. 新しく現れた客をプールから起こす。
            for (var i = 0; i < limit; i++)
            {
                var item = queue[i];

                if (_byId.ContainsKey(item.CustomerId))
                {
                    continue;
                }

                // 調査用: 実際に画面へ増えた客をログに残す。サーバー由来（NET ARRIVE）が
                // 無いのにここだけ出るなら、客を出しているのは表示側の駆動元である。
                DebugUI.ClientEventLog.Add(
                    DebugUI.ClientEventSource.View,
                    "ARRIVE",
                    $"customerId={item.CustomerId} attr={item.Attribute} driver={DriverName}");

                // 属性を渡した時点で、Actor が自分で SO から見た目を引く。
                var actor = Rent();
                actor.Initialize(item.CustomerId, item.Attribute);
                actor.ApplyPose(Localize(_layout.QueueEntryPose())); // 行列の外から歩いてくる。
                _byId.Add(item.CustomerId, actor);
            }

            // 3. 行列の並びどおりに整列し直す。
            _visible.Clear();
            _visibleEntries.Clear();
            for (var i = 0; i < limit; i++)
            {
                var item = queue[i];
                if (!_byId.TryGetValue(item.CustomerId, out var actor))
                {
                    continue;
                }

                _visible.Add(actor);
                _visibleEntries.Add(item);
                actor.MoveTo(Localize(_layout.QueuePose(i)), _layout.AdvanceDuration, _advanceEase);
            }

            ApplyStates(servingCustomerId);
            ApplySiblingOrder();
        }

        /// <summary>
        /// 行列内の位置と現在の注文から、各客の表示状態を決める。
        /// <see cref="CustomerVisualState.Angry"/> は我慢ゲージ推定なので <see cref="Update"/> 側で上書きする。
        /// </summary>
        private void ApplyStates(string servingCustomerId)
        {
            for (var i = 0; i < _visible.Count; i++)
            {
                var actor = _visible[i];
                CustomerVisualState next;

                if (i > 0)
                {
                    next = CustomerVisualState.Queued;
                }
                else if (servingCustomerId != null && servingCustomerId == actor.CustomerId)
                {
                    next = CustomerVisualState.WaitingForServe;
                }
                else
                {
                    next = CustomerVisualState.Ordering;
                }

                actor.SetState(next);
            }
        }

        /// <summary>uGUI は階層順が描画順なので、手前（Depth 大）の客ほど後ろの兄弟に置く。</summary>
        private void ApplySiblingOrder()
        {
            // 行列は先頭が一番手前。退店中の客はさらにその手前。
            for (var i = 0; i < _visible.Count; i++)
            {
                _visible[i].transform.SetSiblingIndex(_visible.Count - 1 - i);
            }

            foreach (var actor in _exiting)
            {
                actor.transform.SetAsLastSibling();
            }
        }

        private void Update()
        {
            // 表示中は最大でも数人なので、毎フレーム回しても問題にならない。
            if (_layout == null || _visible.Count == 0)
            {
                return;
            }

            var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);

            for (var i = 0; i < _visible.Count; i++)
            {
                var actor = _visible[i];
                if (actor.State == CustomerVisualState.Delighted || actor.State == CustomerVisualState.Leaving)
                {
                    continue;
                }

                var item = _visibleEntries[i];
                var mood = CustomerMoodState.From(
                    actor.CustomerId,
                    item.PatienceMaxMs,
                    item.ArrivedAtLocalMs,
                    nowMs,
                    CustomerMoodThresholds.Default);

                if (mood.Mood == CustomerMood.Angry || mood.Mood == CustomerMood.TurnedAway)
                {
                    actor.SetState(CustomerVisualState.Angry);
                }
            }
        }

        /// <summary>
        /// 退店演出の開始は 1 フレーム遅らせる。<c>Apply</c>（行列から消えた）と
        /// <c>Renderer.OnOrderServed</c>（提供された）が同じフレームの別タイミングで届くため、
        /// 両方を受け取ってから「喜んで帰る」か「怒って帰る」かを決める。
        /// </summary>
        private void LateUpdate()
        {
            if (_pendingExits.Count == 0)
            {
                return;
            }

            for (var i = _exiting.Count - 1; i >= 0; i--)
            {
                var actor = _exiting[i];
                if (!_pendingExits.TryGetValue(actor.CustomerId, out var served))
                {
                    continue;
                }

                StartExit(actor, served);
            }

            // 対応する客がもういない通知は捨てる（積み残さない）。
            _pendingExits.Clear();
        }

        private void StartExit(CustomerActor actor, bool served)
        {
            actor.SetState(served ? CustomerVisualState.Delighted : CustomerVisualState.Angry);
            actor.ApplyPose(Localize(_layout.LeavePose(0f)));
            actor.transform.SetAsLastSibling();

            var delay = served ? _layout.DelightedDuration : 0f;
            DOVirtual.DelayedCall(delay, () =>
            {
                if (actor == null || !actor.gameObject.activeSelf)
                {
                    return;
                }

                actor.SetState(CustomerVisualState.Leaving);
                actor.MoveAlong(t => Localize(_layout.LeavePose(t)), _layout.LeaveDuration, _leaveEase, () =>
                {
                    _exiting.Remove(actor);
                    ReturnToPool(actor);
                });
            }).SetLink(actor.gameObject);
        }

        // ── Renderer からの通知 ───────────────────────────────────────

        /// <summary>提供完了。この客は「喜び → 退店」で帰る。</summary>
        public void MarkServed(string customerId)
        {
            _pendingExits[customerId] = true;
        }

        /// <summary>我慢切れによる離脱。この客は「怒り → 退店」で帰る。</summary>
        public void MarkLeft(string customerId)
        {
            if (!_pendingExits.ContainsKey(customerId))
            {
                _pendingExits.Add(customerId, false);
            }
        }

        /// <summary>脱落・試合終了で行列を消す。</summary>
        public void ClearAll()
        {
            foreach (var actor in _visible)
            {
                ReturnToPool(actor);
            }

            foreach (var actor in _exiting)
            {
                ReturnToPool(actor);
            }

            _visible.Clear();
            _visibleEntries.Clear();
            _exiting.Clear();
            _byId.Clear();
            _pendingExits.Clear();
        }

        // ── 小物 ─────────────────────────────────────────────────────

        private CustomerActor Rent()
        {
            var actor = _pool.Count > 0 ? _pool.Pop() : CreateActor();
            actor.gameObject.SetActive(true);
            return actor;
        }

        private void ReturnToPool(CustomerActor actor)
        {
            actor.Release();
            _pool.Push(actor);
        }

        private static int IndexInQueue(IReadOnlyList<CustomerQueueItem> queue, string customerId)
        {
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i].CustomerId == customerId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
