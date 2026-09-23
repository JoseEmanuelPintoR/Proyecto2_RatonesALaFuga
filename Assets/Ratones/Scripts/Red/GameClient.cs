using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Ratones.Basic
{
    public sealed class GameClient : IDisposable
    {
        readonly object gate = new object();
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        SocketConnection socket;
        TcpClient pending;
        volatile bool disposed;
        public volatile string Error;
        public volatile string Status = "Buscando sala…";
        public int Id { get; private set; } = -1;
        public State State { get; private set; }
        float silence, heartbeat;
        public bool Ready { get { return Id >= 0 && State != null; } }
        public GameClient(string code, string name, int key, int aura, IPEndPoint directEndpoint = null)
        {
            new Thread(() => Connect(code, name, key, aura, directEndpoint)) { IsBackground = true, Name = "Ratones cliente" }.Start();
        }
        void Connect(string code, string name, int key, int aura, IPEndPoint direct)
        {
            try
            {
                IPEndPoint endpoint = direct ?? LanDiscovery.Find(code, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested(); Status = "Conectando…";
                var tcp = new TcpClient(AddressFamily.InterNetwork);
                lock (gate) { if (disposed) { tcp.Close(); return; } pending = tcp; }
                var result = tcp.BeginConnect(endpoint.Address, endpoint.Port, null, null);
                using (var wait = result.AsyncWaitHandle)
                { if (!wait.WaitOne(4500)) throw new TimeoutException("El anfitrión no responde."); }
                tcp.EndConnect(result);
                lock (gate)
                {
                    if (disposed) { tcp.Close(); return; }
                    socket = new SocketConnection(tcp); pending = null;
                    socket.Send("HI|" + Protocol.Version + "|" + code + "|" + Protocol.Encode(name) + "|" + key + "|" + aura);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (!disposed) Error = e.Message;
                lock (gate) { if (pending != null) pending.Close(); pending = null; }
            }
        }
        public void Update(float dt)
        {
            SocketConnection s; lock (gate) s = socket; if (s == null) return;
            silence += dt; heartbeat += dt;
            if (heartbeat >= 1) { heartbeat = 0; s.Send("PING"); }
            string line; int count = 0;
            while (count++ < 128 && s.TryRead(out line))
            {
                silence = 0;
                try
                {
                    if (line.StartsWith("WELCOME|")) Id = int.Parse(line.Split('|')[1]);
                    else if (line.StartsWith("S|")) State = Protocol.Parse(line);
                    else if (line.StartsWith("ERROR|")) Error = Protocol.Decode(line.Split('|')[1]);
                }
                catch { Error = "La sala usa otra versión del juego."; }
            }
            if (Error == null && (!s.Alive || silence > 8)) Error = "Se perdió la conexión con el anfitrión.";
        }
        public void Move(float x, float z)
        { lock (gate) if (socket != null && Ready) socket.Send("MOVE|" + Protocol.Num(x) + "|" + Protocol.Num(z), true); }
        public void Use(Power power)
        { lock (gate) if (socket != null && Ready) socket.Send("POWER|" + (int)power); }
        public void Dispose()
        {
            cancellation.Cancel();
            lock (gate)
            {
                disposed = true;
                if (socket != null) socket.Dispose(); if (pending != null) pending.Close();
                socket = null; pending = null;
            }
        }
    }
}
