using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ratones.Core;
using Ratones.Network;

static class GameTests
{
    static int assertions, suites;
    static void Assert(bool value, string message)
    { assertions++; if (!value) throw new Exception(message); }
    static void Near(float a, float b, float tolerance, string label)
    { Assert(Math.Abs(a - b) <= tolerance, label + ": " + a + " != " + b); }
    static MatchSimulation Playing(float duration = 120)
    {
        var sim = new MatchSimulation(46, duration);
        int host = sim.Join("José"), second = sim.Join("Laura");
        Assert(host == 0 && second == 1, "Identidades iniciales");
        Assert(sim.Start(host), "Inicio válido");
        sim.Tick(3.01f);
        Assert(sim.State.Phase == MatchPhase.Playing, "Cuenta atrás");
        return sim;
    }
    static void Suite(string name, Action body)
    { body(); suites++; Console.WriteLine("PASS " + name); }
    public static int Main()
    {
        try
        {
            Suite("Sala: mínimo, máximo, autoridad e inicio", Lobby);
            Suite("Generación: 250 mapas válidos, 50 quesos y jugadores separados", Spawns);
            Suite("Movimiento: velocidad, diagonales, límites y obstáculos", Movement);
            Suite("Recolección: un queso suma una única vez", Collection);
            Suite("Poderes: cargas, alcance, línea de visión y duración", Powers);
            Suite("Final: tiempo, desempate, desconexión y revancha", Ending);
            Suite("Protocolo: textos Unicode, estado y números inválidos", Protocol);
            Suite("Sockets TCP: mensajes fragmentados y concatenados", Framing);
            Suite("Red real: cuatro jugadores, quinta conexión, movimientos y reinicio", Network);
            Console.WriteLine("\n" + suites + " grupos correctos · " + assertions + " comprobaciones.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + e); return 1; }
    }
    static void Lobby()
    {
        var sim = new MatchSimulation(1);
        int a = sim.Join("A"); Assert(!sim.Start(a), "No inicia con un jugador");
        int b = sim.Join("B"); Assert(!sim.Start(b), "Solo inicia el anfitrión");
        sim.Join("C"); sim.Join("D"); Assert(sim.Join("E") == -1, "Máximo cuatro");
        Assert(sim.Start(a), "Cuatro pueden iniciar"); Assert(sim.Join("F") == -1, "Sin entrada a mitad de ronda");
        var named = new MatchSimulation(1); named.Join(" <b>José\n|,; ");
        Assert(!named.State.Players[0].Name.Contains("<"), "Nombre sin etiquetas");
        named.Join("B"); named.Join("C"); named.Join("D"); named.Leave(1); named.Join("E");
        Assert(named.State.Players.Select(p => p.ColorIndex).Distinct().Count() == 4, "Colores diferentes después de reconectar");
    }
    static void Spawns()
    {
        for (int seed = 0; seed < 250; seed++)
        {
            var sim = new MatchSimulation(seed);
            for (int i = 0; i < 4; i++) sim.Join("P" + i);
            sim.Start(0);
            Assert(sim.State.Items.Count(i => i.Kind == ItemKind.Cheese) == 50, "Exactamente 50 quesos");
            Assert(sim.State.Items.Select(i => i.Id).Distinct().Count() == 58, "IDs únicos");
            foreach (PlayerState p in sim.State.Players)
            {
                Assert(KitchenMap.Walkable(p.Position, 2.2f), "Ratón transitable");
                foreach (PlayerState q in sim.State.Players)
                    if (p.Id != q.Id) Assert(Point.DistanceSquared(p.Position, q.Position) >= 14 * 14, "Ratones separados");
            }
            var positions = sim.State.Items.Select(i => i.Position).Concat(sim.State.Players.Select(p => p.Position)).ToList();
            foreach (ItemState item in sim.State.Items) Assert(KitchenMap.Walkable(item.Position, 2.2f), "Objeto transitable");
            for (int i = 0; i < positions.Count; i++) for (int j = i + 1; j < positions.Count; j++)
                Assert(Point.DistanceSquared(positions[i], positions[j]) >= 3.2f * 3.2f, "Objetos separados");
        }
    }
    static void Movement()
    {
        var sim = Playing(); sim.State.Items.Clear();
        PlayerState p = sim.State.Player(0); p.Position = new Point(0, 0);
        sim.Input(0, 1, 0, false, false); sim.Tick(.1f); Near(p.Position.X, 1.2f, .001f, "Velocidad base");
        p.Position = new Point(0, 0); sim.Input(0, 1, 1, false, false); sim.Tick(.1f);
        Near((float)Math.Sqrt(Point.DistanceSquared(p.Position, new Point(0, 0))), 1.2f, .001f, "Diagonal normalizada");
        p.Position = new Point(0, 0); sim.Input(0, 999, 0, false, false); sim.Tick(.1f); Near(p.Position.X, 1.2f, .001f, "Antivelocidad manipulada");
        sim.Input(0, float.NaN, 0, false, false); Assert(!float.IsNaN(p.InputX), "NaN rechazado");
        Point stopped = p.Position; sim.Tick(.5f); Near(p.Position.X, stopped.X, .001f, "Input expirado");
        Point corner = KitchenMap.Move(new Point(48, -44), 20, -20);
        Assert(Math.Abs(corner.X) <= 48.95f && Math.Abs(corner.Z) <= 48.95f, "Límite del terreno");
        Point wall = KitchenMap.Move(new Point(0, -22), 0, -35);
        Assert(wall.Z > -27.6f, "No atraviesa tabla con desplazamiento grande");
    }
    static void Collection()
    {
        var sim = Playing(); sim.State.Items.Clear();
        sim.State.Items.Add(new ItemState { Id = 0, Kind = ItemKind.Cheese, Position = new Point(0, 0) });
        sim.State.Player(0).Position = sim.State.Player(1).Position = new Point(0, 0);
        sim.Tick(.033f); Assert(sim.State.Players.Sum(p => p.Score) == 1, "Un único ganador por queso");
        for (int i = 0; i < 30; i++) sim.Tick(.033f);
        Assert(sim.State.Players.Sum(p => p.Score) == 1 && !sim.State.Items[0].Active, "No hay doble puntaje ni reaparición");
        Assert(sim.State.Phase == MatchPhase.Playing, "La ronda espera al temporizador aunque no queden quesos");
    }
    static void Powers()
    {
        var sim = Playing(); sim.State.Items.Clear();
        PlayerState a = sim.State.Player(0), b = sim.State.Player(1);
        a.Position = new Point(0, 0); b.Position = new Point(4, 0);
        sim.State.Items.Add(new ItemState { Id = 50, Kind = ItemKind.Sugar, Position = a.Position });
        sim.Tick(.033f); Assert(a.BoostCharges == 1 && !sim.State.Items[0].Active, "Caramelo carga azúcar");
        sim.Input(0, 1, 0, true, false); sim.Tick(.1f);
        Near(a.Position.X, 1.8f, .001f, "Velocidad 1.5x"); Near(a.BoostLeft, 4, .001f, "Azúcar dura cuatro segundos");
        Assert(a.BoostCharges == 0, "Consumió carga");
        a.StickyCharges = 1; sim.Input(0, 0, 0, false, true); sim.Tick(.1f);
        Assert(b.StickyLeft == 3 && a.StickyCharges == 0, "Bloquea rival cercano");
        Point before = b.Position; sim.Input(1, 1, 0, false, false); sim.Tick(.1f);
        Near(b.Position.X, before.X, .001f, "Bloqueado no se mueve");
        for (int i = 0; i < 125; i++) sim.Tick(.033f);
        Assert(a.BoostLeft == 0 && b.StickyLeft == 0, "Efectos expiran");
        a.StickyCharges = 1; a.Position = new Point(-20, 0); b.Position = new Point(20, 0);
        sim.Input(0, 0, 0, false, true); sim.Tick(.033f);
        Assert(a.StickyCharges == 1 && b.StickyLeft == 0, "No consume fuera de alcance");
        a.Position = new Point(0, -26); b.Position = new Point(0, -38);
        sim.Input(0, 0, 0, false, true); sim.Tick(.033f);
        Assert(a.StickyCharges == 1 && b.StickyLeft == 0, "No bloquea a través de muebles");
        a.Position = new Point(20, 20);
        for (int i = 0; i < 500; i++) sim.Tick(.033f);
        Assert(sim.State.Items[0].Active, "El caramelo reaparece");
        var cap = Playing(); cap.State.Items.Clear();
        a = cap.State.Player(0); a.Position = new Point(0, 0); cap.State.Player(1).Position = new Point(30, 20);
        a.BoostCharges = 1;
        cap.State.Items.Add(new ItemState { Kind = ItemKind.Sugar, Position = a.Position });
        cap.Tick(.033f); Assert(a.BoostCharges == 1 && cap.State.Items[0].Active, "Máximo una carga; no desperdicia objetos");
    }
    static void Ending()
    {
        var sim = Playing(1); sim.State.Items.Clear();
        sim.State.Player(0).Score = 9; sim.State.Player(1).Score = 9;
        sim.State.Player(0).LastScoreAt = .6f; sim.State.Player(1).LastScoreAt = .3f;
        sim.Tick(1.1f); Assert(sim.State.Phase == MatchPhase.Results, "Finaliza al llegar a cero");
        Assert(sim.State.Winners.SequenceEqual(new[] { 1 }), "Desempata por tiempo de alcanzar puntaje");
        Assert(!sim.ReturnToLobby(1) && sim.ReturnToLobby(0), "Revancha controlada por host");
        Assert(sim.Start(0) && sim.State.Items.Count(i => i.Kind == ItemKind.Cheese) == 50, "Reinicia con 50 quesos");
        Assert(sim.State.Players.All(p => p.Score == 0 && p.BoostCharges == 0), "Reinicia puntajes y poderes");
        var tie = Playing(1); tie.State.Items.Clear(); tie.Tick(1); Assert(tie.State.Winners.Count == 2, "Victoria compartida");
        var left = Playing(); left.Leave(1); Assert(left.State.Phase == MatchPhase.Results && left.State.Winners.Count == 0, "Cancelar al quedar uno");
    }
    static void Protocol()
    {
        var sim = Playing(); sim.State.Player(0).Name = "José|,; 🐭"; sim.State.Player(0).Notice = "¡Azúcar!";
        string wire = WireProtocol.Snapshot(sim.State); var copy = WireProtocol.ParseSnapshot(wire);
        Assert(copy.Players[0].Name == "José|,; 🐭", "Unicode y delimitadores preservados");
        Assert(copy.Items.Count == 58 && copy.Phase == sim.State.Phase && copy.HostId == 0, "Estado completo");
        for (int i = 0; i < 58; i++) Near(copy.Items[i].Position.X, sim.State.Items[i].Position.X, .001f, "Precisión de posiciones");
        bool rejected = false; try { WireProtocol.Float("NaN"); } catch (FormatException) { rejected = true; }
        Assert(rejected, "NaN en protocolo rechazado");
        rejected = false; try { WireProtocol.ParseSnapshot("S|4|x"); } catch (FormatException) { rejected = true; }
        Assert(rejected, "Mensaje incompleto rechazado");
    }
    static void Framing()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using (var tcp = new TcpClient())
        {
            tcp.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            using (var receiver = new SocketConnection(listener.AcceptTcpClient()))
            {
                byte[] bytes = Encoding.UTF8.GetBytes("uno\nJosé 🐭\ntres\n");
                foreach (byte value in bytes) tcp.GetStream().Write(new byte[] { value }, 0, 1);
                var received = new List<string>();
                var end = DateTime.UtcNow.AddSeconds(2);
                while (received.Count < 3 && DateTime.UtcNow < end)
                { string line; while (receiver.TryRead(out line)) received.Add(line); Thread.Sleep(2); }
                Assert(received.SequenceEqual(new[] { "uno", "José 🐭", "tres" }), "Framing TCP conserva límites y UTF-8");
            }
        }
        listener.Stop();
    }
    static void Network()
    {
        using (var server = new GameServer("Host", 0, 31, 3))
        {
            var clients = new List<GameClient>();
            try
            {
                for (int i = 0; i < 3; i++) clients.Add(new GameClient("127.0.0.1", server.BoundPort, "Ratón" + i));
                Pump(server, clients, 220);
                Assert(server.Simulation.ConnectedCount == 4 && clients.All(c => c.PlayerId >= 1 && c.State != null), "Cuatro conectados por sockets");
                Assert(clients.Select(c => c.PlayerId).Distinct().Count() == 3, "Identidades independientes");
                using (var fifth = new GameClient("127.0.0.1", server.BoundPort, "Quinto"))
                {
                    clients.Add(fifth); Pump(server, clients, 120);
                    Assert(fifth.Error != null && fifth.PlayerId == -1, "Quinta conexión rechazada"); clients.Remove(fifth);
                }
                Assert(server.Simulation.Start(server.HostPlayerId), "Anfitrión inicia");
                Pump(server, clients, 320);
                Assert(clients.All(c => c.State.Phase == MatchPhase.Playing), "Inicio sincronizado");
                var c0 = clients[0]; var player = server.Simulation.State.Player(c0.PlayerId);
                Point old = player.Position;
                for (int i = 0; i < 55; i++) { c0.Input(.2f, .2f, false, false); Pump(server, clients, 1); }
                Assert(Point.DistanceSquared(old, player.Position) > .01f, "Intención de movimiento llega al servidor");
                Pump(server, clients, 280);
                Assert(clients.All(c => c.State.Phase == MatchPhase.Results), "Resultados sincronizados");
                int[] winners = server.Simulation.State.Winners.ToArray();
                Assert(clients.All(c => c.State.Winners.SequenceEqual(winners)), "Mismos ganadores en clientes");
                Assert(server.Simulation.ReturnToLobby(0), "Volver a sala"); Pump(server, clients, 50);
                Assert(clients.All(c => c.State.Phase == MatchPhase.Lobby), "Revancha sincronizada");
                clients[0].Dispose(); Pump(server, clients.Skip(1).ToList(), 60);
                Assert(server.Simulation.ConnectedCount == 3, "Desconexión libera cupo");
            }
            finally { foreach (GameClient client in clients) client.Dispose(); }
        }
    }
    static void Pump(GameServer server, List<GameClient> clients, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        { server.Update(.01f); foreach (GameClient client in clients) client.Update(.01f); Thread.Sleep(2); }
    }
}
