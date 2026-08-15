// 客の入退店だけを、サーバー・Bootstrap なしで確認するためのテストモード。
// 座標・拡大倍率・アニメーションの調整用であり、製品の挙動（客の生成規則・評価）を再現するものではない。
// このコンポーネントが居るシーンで再生すると、CustomerQueueView はこちらから駆動される。

using System.Collections.Generic;
using Takoda99.Proto;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Takoda99.View.Customers
{
    /// <summary>
    /// <see cref="CustomerQueueView"/> をサーバーなしで動かすテストドライバ。
    /// サーバーから来る <c>CustomerArrived</c> / <c>OrderServed</c> に相当する操作を
    /// キーボードと自動送りで再現する。
    /// </summary>
    /// <remarks>
    /// 客の属性は順番に巡回させるだけで、実際の出現規則（フェーズによるクレーマー解禁など）は再現しない。
    /// あくまで「4種類すべての見た目と動きを確認する」ためのもの。
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    public sealed class CustomerQueueTestDriver : MonoBehaviour
    {
        [SerializeField] private CustomerQueueView _queueView;

        [Header("自動送り")]
        [Tooltip("再生と同時に自動で客を来店させ続ける。")]
        [SerializeField] private bool _autoArrive = true;

        [Tooltip("来店の間隔（秒）。")]
        [Min(0.1f)]
        [SerializeField] private float _arriveIntervalSeconds = 1.5f;

        [Tooltip("先頭の客を自動で提供して帰す。0 以下なら自動提供しない（手動のみ）。")]
        [Min(0f)]
        [SerializeField] private float _autoServeSeconds = 3f;

        [Header("客のパラメータ")]
        [Tooltip("注文を受けてから提供待機の絵に変わるまでの時間（秒）。サーバーの LocalOrderBegan 相当。")]
        [Min(0f)]
        [SerializeField] private float _orderingSeconds = 0.8f;

        [Tooltip("巡回させる属性。空なら全属性を順に出す。")]
        [SerializeField] private CustomerAttribute[] _attributeCycle = new CustomerAttribute[0];

        [Header("表示")]
        [Tooltip("画面左上に操作方法と行列の中身を出す。")]
        [SerializeField] private bool _showOverlay = true;

        private readonly List<CustomerQueueItem> _queue = new List<CustomerQueueItem>();

        private string _servingCustomerId;
        private int _nextCustomerNumber;
        private int _attributeCursor;
        private float _arriveTimer;
        private float _headTimer;
        private bool _paused;

        private void Awake()
        {
            // ここが出なければ、このコンポーネントが載った GameObject がシーンに無いか非アクティブ。
            Debug.Log($"{nameof(CustomerQueueTestDriver)}: Awake（{gameObject.name}）", this);
        }

        private void Start()
        {
            // 本番の試合中は絶対に動かさない。GameBootstrapper が生きている＝サーバーに繋がった実試合であり、
            // ここで客を出すとサーバー由来の行列とテスト用の客が混ざる（実際に「脱落後も客が流れ続ける」
            // 不具合の原因になっていた）。MainGameViewSampleDriver と同じガード。
            if (Bootstrap.GameBootstrapper.Instance != null)
            {
                Debug.LogWarning(
                    $"{nameof(CustomerQueueTestDriver)}: 実試合中のため自身を無効化します。" +
                    "このコンポーネントは開発用で、本番シーンでは非アクティブにしておくこと。", this);
                gameObject.SetActive(false);
                return;
            }

            if (_queueView == null)
            {
                _queueView = FindAnyObjectByType<CustomerQueueView>(FindObjectsInactive.Include);
            }

            if (_queueView == null)
            {
                Debug.LogError($"{nameof(CustomerQueueTestDriver)}: {nameof(CustomerQueueView)} が見つかりません。", this);
                enabled = false;
                return;
            }

            Debug.LogWarning(
                $"{nameof(CustomerQueueTestDriver)}: 客テストモードで起動しました（対象: {_queueView.name}）。サーバーには接続しません。",
                this);

            // デバッグパネルにも「今はテストドライバが客を出している」と残す。
            // Console を見ない実機/WebGL では、これが唯一の手掛かりになる。
            _queueView.DriverName = nameof(CustomerQueueTestDriver);
            DebugUI.ClientEventLog.Add(
                DebugUI.ClientEventSource.View,
                "TEST_DRIVER",
                $"{nameof(CustomerQueueTestDriver)} が {_queueView.name} を駆動中（サーバー非接続）");

            if (Keyboard.current == null)
            {
                Debug.LogWarning($"{nameof(CustomerQueueTestDriver)}: キーボードが検出できません（Input System）。キー操作は効きません。", this);
            }

            Push();
        }

        private void Update()
        {
            HandleKeys();

            if (_paused)
            {
                return;
            }

            var dt = Time.deltaTime;

            if (_autoArrive)
            {
                _arriveTimer += dt;
                if (_arriveTimer >= _arriveIntervalSeconds)
                {
                    _arriveTimer = 0f;
                    Arrive();
                }
            }

            if (_queue.Count == 0)
            {
                _headTimer = 0f;
                return;
            }

            // 先頭の客だけ「注文 → 提供待機 → 提供」を時間で進める。
            _headTimer += dt;

            if (_servingCustomerId == null && _headTimer >= _orderingSeconds)
            {
                _servingCustomerId = _queue[0].CustomerId;
                Push();
                return;
            }

            if (_autoServeSeconds > 0f && _servingCustomerId != null && _headTimer >= _orderingSeconds + _autoServeSeconds)
            {
                Serve();
            }
        }

        // ── 操作 ─────────────────────────────────────────────────────

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.aKey.wasPressedThisFrame)
            {
                Arrive();
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                Serve();
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                ClearAll();
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                _paused = !_paused;
            }
        }

        /// <summary>来店（<c>CustomerArrived</c> 相当）。</summary>
        public void Arrive()
        {
            var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);
            _queue.Add(new CustomerQueueItem(
                $"test-{_nextCustomerNumber++:D3}",
                NextAttribute(),
                nowMs));

            Push();
        }

        /// <summary>提供完了（<c>OrderServed</c> 相当）。先頭が喜んでから帰る。</summary>
        public void Serve()
        {
            if (_queue.Count == 0)
            {
                return;
            }

            var id = _queue[0].CustomerId;
            _queue.RemoveAt(0);
            _servingCustomerId = null;
            _headTimer = 0f;

            // 本番と同じ順序：先に行列から抜き、そのあとで「提供された」と伝える。
            Push();
            _queueView.MarkServed(id);
        }

        public void ClearAll()
        {
            _queue.Clear();
            _servingCustomerId = null;
            _headTimer = 0f;
            _queueView.ClearAll();
        }

        private void Push()
        {
            _queueView.Apply(_queue, _servingCustomerId);
        }

        private CustomerAttribute NextAttribute()
        {
            if (_attributeCycle != null && _attributeCycle.Length > 0)
            {
                return _attributeCycle[_attributeCursor++ % _attributeCycle.Length];
            }

            // 既定は全属性を順番に。4種類の見た目をひととおり確認できる。
            return (CustomerAttribute)(_attributeCursor++ % 4);
        }

        // ── 画面表示 ─────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_showOverlay)
            {
                return;
            }

            const float width = 380f;
            var height = 130f + _queue.Count * 18f;
            GUILayout.BeginArea(new Rect(10f, 10f, width, height), GUI.skin.box);

            GUILayout.Label("客テストモード" + (_paused ? "（一時停止中）" : string.Empty));
            GUILayout.Label("A: 来店 / Space: 提供して帰す");
            GUILayout.Label("C: 全消し / P: 自動送りの一時停止");
            GUILayout.Label($"行列: {_queue.Count} 人（対応中: {_servingCustomerId ?? "なし"}）");

            for (var i = 0; i < _queue.Count; i++)
            {
                GUILayout.Label($"  {i}: {_queue[i].CustomerId}  {_queue[i].Attribute}");
            }

            GUILayout.EndArea();
        }
    }
}
