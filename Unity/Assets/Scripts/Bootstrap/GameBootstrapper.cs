// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md
// pureC# の各モジュール（Contract/Store/Dispatcher/TypingJudge）と Unity 側の実体
// （WebGLNetworkClient/UnityInputSource）を結線し、MatchClientController を起動する。
// 画面はシーン単位で分かれているため、このオブジェクト自身は DontDestroyOnLoad で
// シーン遷移をまたいで生存し、シーンの切り替えを一手に引き受ける（02-scene-composition.md §3）。
// 接続先URLはコード直書きせず、Assets/StreamingAssets/config.json から実行時に読み込む
// （見つからない場合は Inspector の既定値にフォールバックする。02-scene-composition.md §4.1）。

using System.Collections;
using Takoda99.Client.Contract;
using Takoda99.Client.Lifecycle;
using Takoda99.Client.Net;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.DebugUI;
using Takoda99.InputSource;
using Takoda99.Net;
using Takoda99.Proto;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Takoda99.Bootstrap
{
    /// <summary>結線とシーン遷移の最上位（02-scene-composition.md）。シーンをまたいで唯一生存する。</summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private string webSocketUrl = "";
        [SerializeField] private bool devMode = true;

        [Header("シーン名（Build Settings に登録した名前と一致させる）")]
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private string matchmakingSceneName = "MatchMaking";
        [SerializeField] private string matchSceneName = "MainGame";
        [SerializeField] private string resultSceneName = "Result";

        [Header("Boot シーン内の実体（BootStrap の子）")]
        [SerializeField] private WebGLNetworkClient networkClient;
        [SerializeField] private UnityInputSource inputSource;
        [SerializeField] private DebugPanel debugPanel;

        private readonly RendererProxy rendererProxy = new();
        private IMatchClientController controller;
        private System.IDisposable storeSubscription;
        private ClientPhase lastRoutedPhase = ClientPhase.Boot;

        public static GameBootstrapper Instance { get; private set; }

        public IStore Store { get; private set; }
        public IDispatcher Dispatcher { get; private set; }
        public ITypingJudge TypingJudge { get; private set; }
        public IEnvelopeLog Log { get; private set; }

        /// <summary>
        /// WriteNameModal で確定した表示名。接続確立直後の MatchmakingJoin にそのまま乗せる
        /// （Proto v0.4.0 で `MatchmakingJoin.displayName` が追加された。REQ-01 対応）。
        /// </summary>
        public string DisplayName { get; private set; } = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            var codec = new EnvelopeCodec();
            var log = new EnvelopeLog();
            var store = new Store();
            var clock = new UnityClock();
            var dispatcher = new Dispatcher(codec, store, log, clock);
            var sendQueue = new SendQueue(networkClient, codec, log);
            var romajiTable = new DefaultRomajiTable();
            var typingJudge = new TypingJudge(romajiTable, clock);

            Store = store;
            Dispatcher = dispatcher;
            TypingJudge = typingJudge;
            Log = log;

            if (debugPanel != null)
            {
                debugPanel.Bind(log);
            }

            controller = new MatchClientController(
                networkClient,
                dispatcher,
                store,
                typingJudge,
                sendQueue,
                rendererProxy,
                inputSource);

            networkClient.OnConnectionChanged += (state, _) =>
            {
                if (state == ConnectionState.Disconnected || state == ConnectionState.Failed)
                {
                    sendQueue.OnDisconnected();
                }
            };

            storeSubscription = store.Subscribe(HandlePhaseRouting);
        }

        private void Start()
        {
            StartCoroutine(LoadConfigAndStart());
        }

        /// <summary>
        /// StreamingAssets/config.json から webSocketUrl を読み込んでから controller.Start() を呼ぶ。
        /// これは設定ファイルの取得であり WebSocket 接続ではないため、§4「Boot では接続しない」に
        /// 抵触しない（02-scene-composition.md §4.1）。読み込みに失敗した場合は Inspector の
        /// webSocketUrl（既定値）にフォールバックする。
        ///
        /// ただし WebGL では config.json を読みに行かない。config.json は .gitignore 済みの
        /// ローカル設定であり、unityroom などの配信先には存在しないため、必ず 404 になって
        /// ブラウザのコンソールをエラーで汚すだけだからである。WebGL の接続先は Inspector の
        /// webSocketUrl を正とする。
        /// </summary>
        private IEnumerator LoadConfigAndStart()
        {
            var resolvedUrl = webSocketUrl;

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                StartController(resolvedUrl);
                yield break;
            }

            // Windows/Mac/Linux/Editor では streamingAssetsPath がスキーム無しのファイルパスのため
            // UnityWebRequest には file:// を付けて渡す。Android/WebGL では既にURLとして扱えるためそのまま。
            var configPath = System.IO.Path.Combine(Application.streamingAssetsPath, "config.json");
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.WebGLPlayer)
            {
                configPath = "file://" + configPath;
            }
            using (var request = UnityWebRequest.Get(configPath))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var config = JsonUtility.FromJson<BootstrapFileConfig>(request.downloadHandler.text);
                        if (config != null && !string.IsNullOrEmpty(config.webSocketUrl))
                        {
                            resolvedUrl = config.webSocketUrl;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"{nameof(GameBootstrapper)}: config.json の解析に失敗したため既定値にフォールバックします。{ex.Message}", this);
                    }
                }
                else
                {
                    Debug.LogWarning($"{nameof(GameBootstrapper)}: config.json を読み込めなかったため既定値にフォールバックします（{request.error}）。開発時は Assets/StreamingAssets/config.example.json をコピーして config.json を作成してください。", this);
                }
            }

            StartController(resolvedUrl);
        }

        /// <summary>
        /// Boot が行うのは「生成」だけ。**ここで接続してはいけない。**
        /// サーバーは接続後の最初の1メッセージを最大3秒しか待たず、それを過ぎると表示名が失われる。
        /// したがって接続は「名前確定後」（DecideDisplayName）まで遅らせる
        /// （matchmaking/02-display-name.md §5 ★「名前入力 → 接続 → 即送信」の順）。
        /// </summary>
        private void StartController(string resolvedUrl)
        {
            if (string.IsNullOrEmpty(resolvedUrl))
            {
                Debug.LogError($"{nameof(GameBootstrapper)}: 接続先URLが空です。Boot シーンの {nameof(GameBootstrapper)} の webSocketUrl を設定してください。", this);
            }

            controller.Start(new BootstrapConfig
            {
                WebSocketUrl = resolvedUrl,
                ProtoVersion = "v0.5.0",
                DevMode = devMode,
            });
        }

        [System.Serializable]
        private sealed class BootstrapFileConfig
        {
            public string webSocketUrl;
        }

        private void OnDestroy()
        {
            storeSubscription?.Dispose();
            controller?.Dispose();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ── 画面からの操作 ────────────────────────────────────────────

        /// <summary>Title の Start ボタンから呼ぶ。**まだ接続しない**（名前入力が先）。</summary>
        public void GoToMatchmaking()
        {
            SceneManager.LoadScene(matchmakingSceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// WriteNameModal の Decide ボタンから呼ぶ。表示名を確定し、**その直後に接続する**。
        /// 接続確立と同時に <c>MatchmakingJoin</c> が送られる（MatchClientController.HandleConnectionChanged）。
        /// </summary>
        public void DecideDisplayName(string displayName)
        {
            DisplayName = displayName ?? "";
            controller.BeginPlay(DisplayName);
        }

        /// <summary>キュー離脱（MatchmakingLeave 送信 → Title へ）。</summary>
        public void LeaveMatchmaking() => controller.LeaveMatchmaking();

        /// <summary>Result の Title ボタンから呼ぶ（Result → Title）。</summary>
        public void BackToTitle() => controller.BackToTitle();

        /// <summary>MainGame の ResultCanvas（脱落モーダル）の NextButton から呼ぶ（MainGame → Result）。</summary>
        public void GoToResult()
        {
            SceneManager.LoadScene(resultSceneName, LoadSceneMode.Single);
        }

        // ── Renderer の自己登録 ───────────────────────────────────────

        /// <summary>試合画面の <see cref="View.Renderer"/> が自分自身を登録する（01-renderer.md）。</summary>
        public void AttachRenderer(View.Renderer renderer) => rendererProxy.Active = renderer;

        public void DetachRenderer(View.Renderer renderer)
        {
            if (rendererProxy.Active == renderer)
            {
                rendererProxy.Active = null;
            }
        }

        // ── シーン遷移 ────────────────────────────────────────────────

        /// <summary>
        /// ClientPhase の変化でシーンを切り替える。
        /// **Connecting / Matchmaking ではシーンをロードしない。** これらのフェーズに入る時点で
        /// 既にマッチングシーンにいる（Title の Start ボタンでロード済み）ため、ここでロードすると
        /// 入力済みの名前ごと画面が作り直されてしまう（02-scene-composition.md §3）。
        /// </summary>
        private void HandlePhaseRouting(ClientState state)
        {
            if (state.Phase == lastRoutedPhase)
            {
                return;
            }

            var previous = lastRoutedPhase;
            lastRoutedPhase = state.Phase;

            switch (state.Phase)
            {
                case ClientPhase.Title:
                    SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
                    break;

                case ClientPhase.InMatch:
                    SceneManager.LoadScene(matchSceneName, LoadSceneMode.Single);
                    break;

                case ClientPhase.Result:
                    SceneManager.LoadScene(resultSceneName, LoadSceneMode.Single);
                    break;

                case ClientPhase.Spectating:
                    // 自店が脱落しても接続は保持し、観戦として試合画面に留まる
                    // （matchmaking/01-matchmaking-flow.md §8.3）。シーンは切り替えない。
                    break;

                default:
                    // Connecting / Matchmaking / Boot：シーン遷移なし（上のコメント参照）。
                    _ = previous;
                    break;
            }
        }

        /// <summary>
        /// IRenderer をシーン非依存に保つための転送先切替プロキシ。試合シーンが未ロードの間は
        /// Active が null のため、すべての通知が無害に捨てられる（02-scene-composition.md §4）。
        /// </summary>
        private sealed class RendererProxy : IRenderer
        {
            public View.Renderer Active;

            public void OnCustomerArrived(CustomerView customer) => Active?.OnCustomerArrived(customer);
            public void OnCustomerLeft(string customerId, LeaveReason reason) => Active?.OnCustomerLeft(customerId, reason);
            public void OnKeyFeedback(KeyResult result) => Active?.OnKeyFeedback(result);
            public void OnOrderServed(string customerId) => Active?.OnOrderServed(customerId);
            public void OnPhaseChanged(Phase phase) => Active?.OnPhaseChanged(phase);
            public void OnForcedEliminationWarning(int untilTick, double thresholdPct) => Active?.OnForcedEliminationWarning(untilTick, thresholdPct);
            public void OnStoreEliminated(string storeId, EliminationReason reason, int finalRank) => Active?.OnStoreEliminated(storeId, reason, finalRank);
            public void OnMatchEnd(int finalRank, MatchStats stats) => Active?.OnMatchEnd(finalRank, stats);
            public void OnLifecycleChanged(ClientPhase from, ClientPhase to) => Active?.OnLifecycleChanged(from, to);
            public void OnConnectionTrouble(string kind) => Active?.OnConnectionTrouble(kind);
        }
    }
}
