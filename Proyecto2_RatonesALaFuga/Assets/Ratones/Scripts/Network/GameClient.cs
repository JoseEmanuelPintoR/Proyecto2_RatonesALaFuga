using System;
using System.Net.Sockets;
using System.Threading;
using Ratones.Core;

namespace Ratones.Network
{
    public sealed class GameClient : IDisposable
    {
        readonly object gate = new object();
        SocketConnection socket;
        TcpClient pending;
        volatile bool disposed;
        public volatile bool Connecting = true;
        public volatile string Error;
        public int PlayerId { get; private set; } = -1;
        public MatchState State { get; private set; }
        float silence, heartbeat, connectAge;

        public GameClient(string address, int port, string name)
        {
            new Thread(() => Connect(address, port, name)) { IsBackground = true, Name = "Ratones connect" }.Start();
        }
        void Connect(string address, int port, string name)
        {
            try
            {
                var tcp = new TcpClient();
                lock (gate) { if (disposed) { tcp.Close(); return; } pending = tcp; }
                var result = tcp.BeginConnect(address, port, null, null);
                using (result.AsyncWaitHandle)
                { if (!result.AsyncWaitHandle.WaitOne(4500)) throw new TimeoutException("No se encontró la sala. Revisa IP, puerto y Wi-Fi."); }
                tcp.EndConnect(result);
                lock (gate)
                {
                    if (disposed) { tcp.Close(); return; }
                    socket = new SocketConnection(tcp); pending = null;
                    socket.Send("HELLO|" + WireProtocol.Version + "|" + WireProtocol.Encode(name));
                }
            }
            catch (Exception e)
            {
                if (!disposed) { Error = "No se pudo conectar: " + e.Message; Connecting = false; }
                lock (gate) { if (pending != null) pending.Close(); pending = null; }
            }
        }
        public void Update(float dt)
        {
            connectAge += dt;
            SocketConnection s; lock (gate) s = socket;
            if (s == null) return;
            silence += dt; heartbeat += dt;
            if (heartbeat >= 1) { heartbeat = 0; s.Send("PING"); }
            string line; int count = 0;
            while (count++ < 128 && s.TryRead(out line))
            {
                silence = 0;
                try
                {
                    if (line.StartsWith("WELCOME|")) { PlayerId = int.Parse(line.Split('|')[1]); Connecting = false; }
                    else if (line.StartsWith("S|")) State = WireProtocol.ParseSnapshot(line);
                    else if (line.StartsWith("ERROR|")) { Error = WireProtocol.Decode(line.Split('|')[1]); Connecting = false; }
                }
                catch { Error = "La sala envió datos incompatibles. Usa la misma versión del juego."; }
            }
            if (!s.Alive && Error == null) Error = "Se perdió la conexión con el anfitrión.";
            if (silence > 8 && Error == null) Error = "La sala dejó de responder.";
            if (Connecting && connectAge > 8 && Error == null) Error = "El anfitrión no respondió a la solicitud.";
        }
        public void Input(float x, float z, bool boost, bool sticky)
        {
            lock (gate) if (socket != null && PlayerId >= 0)
                socket.Send(WireProtocol.Input(x, z, boost, sticky), !(boost || sticky));
        }
        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                if (socket != null) socket.Dispose();
                if (pending != null) pending.Close();
                socket = null; pending = null;
            }
        }
    }
}
