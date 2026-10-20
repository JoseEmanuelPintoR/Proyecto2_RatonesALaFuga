using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

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
        public readonly LocalPrediction Prediction = new LocalPrediction();
        public float LatencyMilliseconds { get; private set; }
        int worldAck = -1, pingSequence;
        int powerRound = -1, powerSequence, freezeRequest;
        public bool FreezePending
        {
            get { Player p = State == null ? null : State.Player(Id); return p != null && freezeRequest > p.LastPowerRequest; }
        }
        long pingStarted;
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
            if (heartbeat >= 1)
            { heartbeat = 0; pingStarted = Stopwatch.GetTimestamp(); s.Send("PING|" + (++pingSequence)); }
            string line; int count = 0;
            while (count++ < 128 && s.TryRead(out line))
            {
                silence = 0;
                try
                {
                    if (line.StartsWith("WELCOME|")) Id = int.Parse(line.Split('|')[1]);
                    else if (line.StartsWith("S|"))
                    {
                        State = Protocol.Parse(line, State);
                        if (powerRound != State.Round)
                        { powerRound = State.Round; powerSequence = freezeRequest = 0; }
                        Prediction.Reconcile(State.Player(Id), State.Round, State.Phase == Phase.Playing);
                        if (worldAck != State.WorldRevision)
                        { worldAck = State.WorldRevision; s.Send("ACK_WORLD|" + worldAck); }
                    }
                    else if (line == "PONG|" + pingSequence)
                        LatencyMilliseconds = (float)((Stopwatch.GetTimestamp() - pingStarted) * 1000.0 / Stopwatch.Frequency);
                    else if (line.StartsWith("ERROR|")) Error = Protocol.Decode(line.Split('|')[1]);
                }
                catch { Error = "La sala usa otra versión del juego."; }
            }
            if (Error == null && (!s.Alive || silence > 8)) Error = "Se perdió la conexión con el anfitrión.";
        }
        public void Move(float x, float z)
        {
            lock (gate) if (socket != null && Ready && State.Phase == Phase.Playing && Prediction.Ready)
            {
                InputFrame frame = Prediction.Push(x,z);
                socket.Send("MOVE|" + State.Round + "|" + frame.Sequence + "|" + Protocol.Num(frame.X) + "|" + Protocol.Num(frame.Z));
            }
        }
        public bool Use(Power power, int targetId = Rules.AllRivals)
        {
            lock (gate)
            {
                if (socket == null || !Ready || State.Phase != Phase.Playing) return false;
                Player own = State.Player(Id);
                if (own == null || !own.Connected || own.FreezeLeft > 0 || !own.Has(power)) return false;
                if (power == Power.Freeze && FreezePending) return false;
                int sequence = ++powerSequence;
                if (power == Power.Freeze) freezeRequest = sequence;
                socket.Send("POWER|" + State.Round + "|" + sequence + "|" + (int)power + "|" + targetId);
                return true;
            }
        }
        public void UpdateProfile(string name, int key, int aura)
        {
            lock (gate) if (socket != null && Ready)
                socket.Send("PROFILE|" + Protocol.Encode(Simulation.CleanName(name)) + "|" + Simulation.ColorIndex(key) + "|" + Simulation.ColorIndex(aura));
        }
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
