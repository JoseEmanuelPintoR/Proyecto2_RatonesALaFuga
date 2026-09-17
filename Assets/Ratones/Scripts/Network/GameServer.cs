using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Ratones.Core;

namespace Ratones.Network
{
    public sealed class GameServer : IDisposable
    {
        sealed class Peer
        {
            public SocketConnection Socket;
            public int Id = -1;
            public float Age, Silence, CloseAfter = -1;
        }
        public readonly MatchSimulation Simulation;
        public int HostPlayerId { get; private set; }
        readonly TcpListener listener;
        readonly ConcurrentQueue<TcpClient> accepted = new ConcurrentQueue<TcpClient>();
        readonly List<Peer> peers = new List<Peer>();
        volatile bool alive = true;
        float accumulator, snapshotClock;
        public int BoundPort { get { return ((IPEndPoint)listener.LocalEndpoint).Port; } }

        public GameServer(string hostName, int port, int seed, float duration = GameRules.RoundSeconds)
        {
            Simulation = new MatchSimulation(seed, duration);
            HostPlayerId = Simulation.Join(hostName);
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start(8);
            new Thread(AcceptLoop) { IsBackground = true, Name = "Ratones host" }.Start();
        }
        void AcceptLoop()
        {
            while (alive)
            {
                try
                {
                    TcpClient tcp = listener.AcceptTcpClient();
                    if (accepted.Count >= 8) tcp.Close(); else accepted.Enqueue(tcp);
                }
                catch { if (!alive) return; }
            }
        }
        public void Update(float dt)
        {
            TcpClient tcp;
            while (accepted.TryDequeue(out tcp))
            {
                if (peers.Count >= 8) { tcp.Close(); continue; }
                peers.Add(new Peer { Socket = new SocketConnection(tcp) });
            }
            for (int i = peers.Count - 1; i >= 0; i--)
            {
                Peer p = peers[i]; p.Age += dt; p.Silence += dt;
                string line; int count = 0;
                while (p.CloseAfter < 0 && count++ < 64 && p.Socket.TryRead(out line))
                {
                    p.Silence = 0;
                    try { Handle(p, line); }
                    catch { Reject(p, "El mensaje recibido no es válido."); }
                }
                if (p.CloseAfter >= 0) { p.CloseAfter -= dt; if (p.CloseAfter <= 0) p.Socket.Dispose(); }
                if ((p.Id < 0 && p.Age > 5) || p.Silence > 10) p.Socket.Dispose();
                if (!p.Socket.Alive) { if (p.Id >= 0) Simulation.Leave(p.Id); p.Socket.Dispose(); peers.RemoveAt(i); }
            }
            accumulator += Math.Min(dt, 0.5f);
            const float step = 1f / GameRules.TickRate;
            while (accumulator >= step) { Simulation.Tick(step); accumulator -= step; }
            snapshotClock += dt;
            if (snapshotClock >= 1f / GameRules.SnapshotRate)
            {
                snapshotClock = 0;
                string snapshot = WireProtocol.Snapshot(Simulation.State);
                foreach (Peer p in peers) if (p.Id >= 0 && p.CloseAfter < 0) p.Socket.Send(snapshot, true);
            }
        }
        void Handle(Peer peer, string line)
        {
            if (line.Length > 512) { Reject(peer, "Mensaje demasiado largo."); return; }
            string[] parts = line.Split('|');
            if (peer.Id < 0)
            {
                if (parts.Length != 3 || parts[0] != "HELLO" || parts[1] != WireProtocol.Version)
                { Reject(peer, "La versión del juego no coincide."); return; }
                int id = Simulation.Join(WireProtocol.Decode(parts[2]));
                if (id < 0) { Reject(peer, "La sala está llena o la ronda ya empezó."); return; }
                peer.Id = id;
                peer.Socket.Send("WELCOME|" + id);
                peer.Socket.Send(WireProtocol.Snapshot(Simulation.State), true);
                return;
            }
            if (parts[0] == "PING") { peer.Socket.Send("PONG"); return; }
            if (parts[0] == "BYE") { peer.Socket.Dispose(); return; }
            if (parts[0] == "I" && parts.Length == 5)
                Simulation.Input(peer.Id, WireProtocol.Float(parts[1]), WireProtocol.Float(parts[2]), parts[3] == "1", parts[4] == "1");
            // Los clientes no envían posiciones, puntajes, ganadores ni orden de inicio.
        }
        static void Reject(Peer p, string reason)
        { p.Socket.Send("ERROR|" + WireProtocol.Encode(reason)); p.CloseAfter = 0.35f; }
        public void Dispose()
        {
            if (!alive) return; alive = false; listener.Stop();
            foreach (Peer p in peers) p.Socket.Dispose();
            TcpClient tcp; while (accepted.TryDequeue(out tcp)) tcp.Close();
            peers.Clear();
        }
    }
}
