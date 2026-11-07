using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>
    /// WebSocket connection to the Node bridge (ws://127.0.0.1:8787/game). Runs on a background task,
    /// reconnects forever, hands received text frames to the main thread through a queue.
    /// </summary>
    public class BridgeClient : IDisposable
    {
        readonly string url;
        readonly float reconnectSec;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        volatile bool connected;
        public bool Connected => connected;
        public string LastError { get; private set; }
        public int Received { get; private set; }

        public BridgeClient(string url, float reconnectSec)
        {
            this.url = url;
            this.reconnectSec = Mathf.Max(0.5f, reconnectSec);
            Task.Run(() => Loop(cts.Token));
        }

        public bool TryDequeue(out string msg) => inbox.TryDequeue(out msg);

        public void Send(string json)
        {
            if (!connected) return;
            if (outbox.Count > 200) return; // never pile up while the bridge is slow
            outbox.Enqueue(json);
        }

        async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                using (var ws = new ClientWebSocket())
                {
                    try
                    {
                        await ws.ConnectAsync(new Uri(url), token);
                        connected = true;
                        LastError = null;
                        outbox.Enqueue("{\"t\":\"hello\",\"app\":\"game\"}");
                        var sender = Task.Run(() => SendLoop(ws, token));
                        await ReceiveLoop(ws, token);
                    }
                    catch (Exception e)
                    {
                        LastError = e.Message;
                    }
                    finally
                    {
                        connected = false;
                    }
                }
                try { await Task.Delay(TimeSpan.FromSeconds(reconnectSec), token); } catch { }
            }
        }

        async Task ReceiveLoop(ClientWebSocket ws, CancellationToken token)
        {
            var buffer = new byte[64 * 1024];
            var sb = new StringBuilder();
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var seg = new ArraySegment<byte>(buffer);
                var res = await ws.ReceiveAsync(seg, token);
                if (res.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                if (res.EndOfMessage)
                {
                    inbox.Enqueue(sb.ToString());
                    Received++;
                    sb.Clear();
                }
            }
        }

        async Task SendLoop(ClientWebSocket ws, CancellationToken token)
        {
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                if (outbox.TryDequeue(out var msg))
                {
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token); }
                    catch { break; }
                }
                else await Task.Delay(15, token);
            }
        }

        public void Dispose()
        {
            cts.Cancel();
        }
    }
}
