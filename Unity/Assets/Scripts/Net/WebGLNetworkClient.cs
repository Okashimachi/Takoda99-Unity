// 仕様書: Unity/docs/.sdd/platform/01-network-client.md
// INetworkClient の Unity 実体。NativeWebSocket で実際の WebSocket 通信を行う。
// Envelope のエンコード/デコードは行わない（送信JSONの組み立てのみ EnvelopeCodec を使う）。

using System;
using System.Text;
using NativeWebSocket;
using Takoda99.Client.Contract;
using Takoda99.Client.Net;
using Takoda99.Client.State;
using UnityEngine;

namespace Takoda99.Net
{
    /// <summary><see cref="INetworkClient"/> の Unity 実体（01-network-client.md）。</summary>
    public sealed class WebGLNetworkClient : MonoBehaviour, INetworkClient
    {
        [SerializeField] private int reconnectDelayMs = 2000;
        [SerializeField] private int maxReconnectAttempts = 5;

        private readonly IEnvelopeCodec codec = new EnvelopeCodec();
        private WebSocket socket;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event Action<string> OnReceiveRaw;
        public event Action<ConnectionState, string> OnConnectionChanged;

        public void Connect(string url)
        {
            DisposeSocket();

            State = ConnectionState.Connecting;
            socket = new WebSocket(url);

            socket.OnOpen += HandleOpen;
            socket.OnMessage += HandleMessage;
            socket.OnError += HandleError;
            socket.OnClose += HandleClose;

            // WebGL / エディタとも await せずに投げっぱなしにする（01-network-client.md §4.4）。
            _ = socket.Connect();
        }

        public void Disconnect()
        {
            DisposeSocket();
            State = ConnectionState.Disconnected;
        }

        public void Send(string type, object payload)
        {
            if (socket == null || socket.State != WebSocketState.Open)
            {
                return;
            }

            var json = codec.EncodeEnvelope(type, payload);
            _ = socket.SendText(json);
        }

        private void Update()
        {
            // WebGL では no-op、エディタ/スタンドアロンでは受信キューの消化に必要（01-network-client.md §4.4）。
#if !UNITY_WEBGL || UNITY_EDITOR
            socket?.DispatchMessageQueue();
#endif
        }

        private void OnDestroy()
        {
            DisposeSocket();
        }

        private void HandleOpen()
        {
            State = ConnectionState.Connected;
            OnConnectionChanged?.Invoke(State, null);
        }

        private void HandleMessage(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            OnReceiveRaw?.Invoke(json);
        }

        private void HandleError(string message)
        {
            State = ConnectionState.Failed;
            OnConnectionChanged?.Invoke(State, message);
        }

        private void HandleClose(WebSocketCloseCode code)
        {
            State = ConnectionState.Disconnected;
            OnConnectionChanged?.Invoke(State, code.ToString());
        }

        private void DisposeSocket()
        {
            if (socket == null)
            {
                return;
            }

            socket.OnOpen -= HandleOpen;
            socket.OnMessage -= HandleMessage;
            socket.OnError -= HandleError;
            socket.OnClose -= HandleClose;

            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.Connecting)
            {
                _ = socket.Close();
            }

            socket = null;
        }
    }
}
