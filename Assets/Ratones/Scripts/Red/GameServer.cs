using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Ratones.Basic
{
    public sealed class GameServer : IDisposable
    {
        sealed class Peer { public SocketConnection Socket; public int Id = -1; public float Age, Silence, Closing = -1; }
        public readonly Simulation Simulation;
        public readonly string Code;
        public readonly int HostId;
        public int Port { get { return ((IPEndPoint)listener.LocalEndpoint).Port; } }
        readonly TcpListener listener;
        readonly ConcurrentQueue<TcpClient> accepted = new ConcurrentQueue<TcpClient>();
        readonly List<Peer> peers = new List<Peer>();
        volatile bool running = true;
        float accumulator, snapshotClock;
        public GameServer(string code, string name, int key, int aura, int seed, int port = 0, float duration = Rules.RoundTime)
        {
            Code = code; Simulation = new Simulation(seed, duration); HostId = Simulation.Join(name, key, aura);
            listener = new TcpListener(IPAddress.Any, port); listener.Start(8);
            new Thread(Accept) { IsBackground = true, Name = "Ratones servidor" }.Start();
        }
        void Accept()
        {
            while (running)
                try
                {
                    TcpClient socket = listener.AcceptTcpClient();
                    if (!running || accepted.Count >= 8) socket.Close(); else accepted.Enqueue(socket);
                }
                catch (SocketException) { if (!running) return; }
                catch (ObjectDisposedException) { return; }
        }
        public void Update(float dt)
        {
            TcpClient incoming;
            while (accepted.TryDequeue(out incoming))
            {
                if (peers.Count >= 8) { incoming.Close(); continue; }
                try { peers.Add(new Peer { Socket = new SocketConnection(incoming) }); } catch { incoming.Close(); }
            }
            for (int i = peers.Count - 1; i >= 0; i--)
            {
                Peer peer = peers[i]; peer.Age += dt; peer.Silence += dt;
                string line; int count = 0;
                while (peer.Closing < 0 && count++ < 64 && peer.Socket.TryRead(out line))
                {
                    peer.Silence = 0;
                    try { Message(peer, line); } catch { Reject(peer, "Mensaje incompatible."); }
                }
                if (peer.Closing >= 0) { peer.Closing -= dt; if (peer.Closing <= 0) peer.Socket.Dispose(); }
                if ((peer.Id < 0 && peer.Age > 5) || peer.Silence > 10) peer.Socket.Dispose();
                if (!peer.Socket.Alive) { if (peer.Id >= 0) Simulation.Leave(peer.Id); peer.Socket.Dispose(); peers.RemoveAt(i); }
            }
            accumulator += Math.Min(dt, .5f);
            float step = 1f / Rules.TickRate;
            while (accumulator >= step) { Simulation.Tick(step); accumulator -= step; }
            snapshotClock += dt;
            if (snapshotClock >= 1f / Rules.SnapshotRate)
            {
                snapshotClock = 0; string state = Protocol.Snapshot(Simulation.State);
                foreach (Peer peer in peers) if (peer.Id >= 0 && peer.Closing < 0) peer.Socket.Send(state, true);
            }
        }
        void Message(Peer peer, string line)
        {
            if (line.Length > 512) { Reject(peer, "Mensaje demasiado largo."); return; }
            string[] p = line.Split('|');
            if (peer.Id < 0)
            {
                if (p.Length != 6 || p[0] != "HI" || p[1] != Protocol.Version || p[2] != Code)
                { Reject(peer, "Código o versión del juego incorrectos."); return; }
                int id = Simulation.Join(Protocol.Decode(p[3]), int.Parse(p[4]), int.Parse(p[5]));
                if (id < 0) { Reject(peer, "La sala está llena o la partida ya empezó."); return; }
                peer.Id = id; peer.Socket.Send("WELCOME|" + id); return;
            }
            if (p[0] == "PING") { peer.Socket.Send("PONG"); return; }
            if (p[0] == "MOVE" && p.Length == 3) Simulation.Move(peer.Id, Protocol.Float(p[1]), Protocol.Float(p[2]));
            if (p[0] == "POWER" && p.Length == 2)
            { int power = int.Parse(p[1]); if (power == 1 || power == 2) Simulation.Use(peer.Id, (Power)power); }
        }
        static void Reject(Peer peer, string reason)
        { peer.Socket.Send("ERROR|" + Protocol.Encode(reason)); peer.Closing = .35f; }
        public void Dispose()
        {
            if (!running) return; running = false; listener.Stop();
            foreach (Peer peer in peers) peer.Socket.Dispose(); peers.Clear();
            TcpClient socket; while (accepted.TryDequeue(out socket)) socket.Close();
        }
    }
}
