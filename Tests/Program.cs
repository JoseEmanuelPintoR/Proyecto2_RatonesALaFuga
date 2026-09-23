using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ratones.Basic;

static class Program
{
    static int passed;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Near(float a, float b, string message, float tolerance = .002f) { Check(Math.Abs(a-b) < tolerance, message + ": " + a + " != " + b); }
    static void Test(string name, Action body) { body(); passed++; Console.WriteLine("OK " + name); }
    static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Faltó excepción " + typeof(T).Name); }
    static Simulation Playing(int count = 4, float duration = 120)
    {
        var s = new Simulation(123, duration);
        for (int i=0;i<count;i++) s.Join("Ratón " + i, i, 11-i);
        Check(s.Start(0), "iniciar"); s.Tick(3); Check(s.State.Phase == Phase.Playing, "cuenta atrás");
        s.State.Items.Clear();
        for (int i=0;i<count;i++) s.State.Players[i].Position = new Position(i*10, i*10);
        return s;
    }
    static void Tick(Simulation s, float seconds) { while (seconds > .00001f) { float dt = Math.Min(seconds, 1f/30); s.Tick(dt); seconds -= dt; } }
    static Item Add(Simulation s, ItemKind kind, Position position)
    { var item = new Item { Id=s.State.Items.Count,Kind=kind,Position=position }; s.State.Items.Add(item); return item; }
    static void Pump(GameServer server, IList<GameClient> clients, Func<bool> done, int timeout = 5000, float dt = .005f)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.ElapsedMilliseconds < timeout)
        { server.Update(dt); foreach (var c in clients) c.Update(dt); Thread.Sleep(2); }
        Check(done(), "Tiempo agotado: " + string.Join(";", clients.Select(c => c.Error ?? c.Status)));
    }
    static void Main()
    {
        Test("lobby: mínimo, máximo, solo anfitrión y huecos libres", () => {
            var s = new Simulation(1); Check(s.Join("  Ana<>\n  ", -5, 99) == 0, "host");
            Check(s.State.Player(0).Name == "Ana" && s.State.Player(0).KeyColor == 0 && s.State.Player(0).AuraColor == 11, "perfil");
            Check(!s.Start(0), "solo no empieza"); s.Join("B",1,2);
            Check(!s.Start(1), "cliente no inicia"); s.Join("C",2,3); s.Join("D",3,4); Check(s.Join("E",0,0) < 0, "lleno");
            s.Leave(2); int id=s.Join("E",4,5); Check(s.State.Player(id).Slot == 2, "reutiliza puesto");
            Check(s.Start(0) && s.Join("F",0,0) < 0, "cerrar ingreso durante partida");
        });
        Test("generación: 50 alimentos, 8 poderes y posiciones separadas", () => {
            for (int seed=0; seed<40; seed++) {
                var s=new Simulation(seed); for(int i=0;i<4;i++)s.Join("P",0,0); s.Start(0);
                Check(s.State.Items.Count == 58,"total");
                Check(s.State.Items.Count(i=>i.Kind==ItemKind.Queso)==30,"quesos");
                Check(s.State.Items.Count(i=>i.Kind==ItemKind.Fresa)==15,"fresas");
                Check(s.State.Items.Count(i=>i.Kind==ItemKind.Pie)==5,"pies");
                var positions=s.State.Players.Select(p=>p.Position).Concat(s.State.Items.Select(i=>i.Position)).ToArray();
                for(int i=0;i<positions.Length;i++) {
                    Check(Math.Abs(positions[i].X)<=45 && Math.Abs(positions[i].Z)<=45,"límite");
                    for(int j=0;j<i;j++) Check(Position.Distance2(positions[i],positions[j]) >= (i<4?144:11.55f),"separación");
                }
            }
        });
        Test("puntos 100/200/500 y recogida única entre rivales", () => {
            var s=Playing(); var p=s.State.Player(0); var q=s.State.Player(1); q.Position=p.Position;
            foreach(var kind in new[]{ItemKind.Queso,ItemKind.Fresa,ItemKind.Pie})Add(s,kind,p.Position);
            s.Tick(.01f); Check(p.Score+q.Score==800 && p.Collected+q.Collected==3,"reparto sin duplicación");
            Tick(s,1); Check(p.Score+q.Score==800 && s.State.Items.All(i=>!i.Active),"sin respawn de comida");
        });
        Test("un solo poder guardado y activación manual", () => {
            var s=Playing(); var p=s.State.Player(0);
            Add(s,ItemKind.Chocolate,p.Position); var trap=Add(s,ItemKind.Trampa,p.Position);
            s.Tick(.01f); Check(p.Inventory==Power.Speed && trap.Active && p.BoostLeft==0,"solo chocolate guardado");
            s.Use(0,Power.Freeze); s.Tick(.01f); Check(p.Inventory==Power.Speed,"no activa otro tipo");
            p.Position=new Position(-10,-10); s.Use(0,Power.Speed); s.Tick(.01f);
            Check(p.Inventory==Power.None && p.BoostLeft==4,"consumir manualmente");
            p.Position=trap.Position; s.Tick(.01f); Check(p.Inventory==Power.Freeze,"nuevo poder después de consumir");
        });
        Test("velocidad: multiplicador, cuatro segundos, límites y entrada vencida", () => {
            var s=Playing(); var p=s.State.Player(0); p.Inventory=Power.Speed;
            s.Move(0,1,0); s.Use(0,Power.Speed); s.Tick(.05f); Near(p.Position.X,.9f,"velocidad x1.5");
            s.Move(0,0,0); Tick(s,4.02f); Check(p.BoostLeft==0,"dura cuatro segundos");
            s.Move(0,1,1); var before=p.Position; s.Tick(.05f); Near((float)Math.Sqrt(Position.Distance2(p.Position,before)),.6f,"diagonal normalizada");
            s.Move(0,0,0); s.Move(0,float.NaN,1); Tick(s,.5f); before=p.Position; Tick(s,.5f); Near(Position.Distance2(before,p.Position),0,"entrada inválida/vencida");
            p.Position=new Position(48.9f,0); s.Move(0,1,0); s.Tick(.1f); Near(p.Position.X,49,"borde");
        });
        Test("trampa: todos los rivales tres segundos, sin rango y sin congelar al dueño", () => {
            var s=Playing(); var p=s.State.Player(0); p.Inventory=Power.Freeze;
            s.State.Player(3).Position=new Position(48,48);
            for(int i=1;i<4;i++){s.State.Player(i).Inventory=Power.Speed;s.Move(i,1,0);}
            var before=s.State.Players.Select(x=>x.Position).ToArray();
            s.Use(0,Power.Freeze); s.Tick(.01f);
            Check(p.Inventory==Power.None && p.FreezeLeft==0,"dueño libre");
            for(int i=1;i<4;i++){Near(s.State.Player(i).FreezeLeft,3,"duración");Near(Position.Distance2(before[i],s.State.Player(i).Position),0,"inmóvil");s.Use(i,Power.Speed);}
            Tick(s,2.9f); Check(s.State.Players.Skip(1).All(x=>x.FreezeLeft>0 && x.Inventory==Power.Speed && x.BoostLeft==0),"no usa poder congelado");
            Tick(s,.12f); Check(s.State.Players.All(x=>x.FreezeLeft==0),"liberación");
        });
        Test("poder reaparece a los 15 segundos", () => {
            var s=Playing(); var p=s.State.Player(0); var item=Add(s,ItemKind.Chocolate,p.Position);s.Tick(.01f);
            p.Position=new Position(-30,-30);Tick(s,14.9f);Check(!item.Active,"no antes");Tick(s,.12f);Check(item.Active,"reaparece");
        });
        Test("120 segundos, empate y segunda ronda", () => {
            var s=Playing(); s.State.Player(0).Score=500;s.State.Player(1).Score=500;s.State.Player(2).Score=100;
            Tick(s,119.8f);Check(s.State.Phase==Phase.Playing,"duración mínima");Tick(s,.3f);
            Check(s.State.Phase==Phase.Results && s.State.Winners.SequenceEqual(new[]{0,1}),"empate sin desempate por tiempo");
            Check(!s.Lobby(1) && s.Lobby(0),"solo host vuelve toda la sala");
            Check(s.Start(0) && s.State.Round==2 && s.State.Players.All(p=>p.Score==0 && p.Inventory==Power.None),"reinicio completo");
        });
        Test("cancelación si quedan menos de dos", () => {
            var s=Playing(2);s.Leave(1);Check(s.State.Phase==Phase.Results && s.State.Winners.Count==0,"cancelada");
            Check(s.Lobby(0) && !s.Start(0),"esperar otro jugador");s.Join("Nuevo",2,2);Check(s.Start(0),"recuperar sala");
        });
        Test("protocolo: Unicode, colores, inventario y números independientes del idioma", () => {
            var s=Playing();s.State.Player(0).Name="José | ; , 🐭";s.State.Player(0).Inventory=Power.Freeze;
            s.State.Player(0).Position=new Position(-1.25f,2.75f);Add(s,ItemKind.Pie,new Position(4,5));
            var old=Thread.CurrentThread.CurrentCulture;
            try { Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo("es-CO");
                var state=Protocol.Parse(Protocol.Snapshot(s.State));Check(state.Player(0).Name==s.State.Player(0).Name,"unicode");
                Check(state.Player(0).Inventory==Power.Freeze && state.Player(0).AuraColor==11,"perfil/inventario");Near(state.Player(0).Position.X,-1.25f,"decimal");
            } finally {Thread.CurrentThread.CurrentCulture=old;}
            Throws<FormatException>(()=>Protocol.Float("NaN"));Throws<FormatException>(()=>Protocol.Parse("S|1"));
        });
        Test("código de seis dígitos y descubrimiento UDP real", () => {
            Check(!LanDiscovery.ValidCode("12a456") && !LanDiscovery.ValidCode("12345"),"validación");
            for(int i=0;i<100;i++)Check(LanDiscovery.ValidCode(LanDiscovery.NewCode()),"código");
            using(var beacon=new LanBeacon("654321",23456,0)) {
                var found=LanDiscovery.Find("654321",CancellationToken.None,beacon.BoundPort,1500,new[]{IPAddress.Loopback});
                Check(found.Port==23456 && IPAddress.IsLoopback(found.Address),"sala correcta");
                Throws<TimeoutException>(()=>LanDiscovery.Find("654320",CancellationToken.None,beacon.BoundPort,250,new[]{IPAddress.Loopback}));
                using(var c=new CancellationTokenSource()){c.Cancel();Throws<OperationCanceledException>(()=>LanDiscovery.Find("654321",c.Token,beacon.BoundPort));}
            }
        });
        Test("TCP real: fragmentos de mensajes y varias líneas", () => {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            using(var sender=new TcpClient()) {
                sender.Connect((IPEndPoint)listener.LocalEndpoint);
                using(var receiver=new SocketConnection(listener.AcceptTcpClient())) {
                    byte[] one=Encoding.UTF8.GetBytes("uno\ndos\nJosé\n");
                    sender.GetStream().Write(one,0,3);Thread.Sleep(10);sender.GetStream().Write(one,3,one.Length-3);
                    var lines=new List<string>();var watch=Stopwatch.StartNew();
                    while(lines.Count<3 && watch.ElapsedMilliseconds<1000){string line;while(receiver.TryRead(out line))lines.Add(line);Thread.Sleep(2);}
                    Check(lines.SequenceEqual(new[]{"uno","dos","José"}),"rearmado");
                }
            }listener.Stop();
        });
        Test("LAN por código + TCP: cuatro jugadores, quinto/código incorrecto, juego, podio y revancha", () => {
            using(var server=new GameServer("321654","Anfitrión",1,2,123,duration:2))
            using(var beacon=new LanBeacon("321654",server.Port)) {
                var clients=new List<GameClient>();
                try {
                    clients.Add(new GameClient("321654","José",4,10)); // Descubre sin IP, igual que la interfaz.
                    Pump(server,clients,()=>clients[0].Ready);
                    var ep=new IPEndPoint(IPAddress.Loopback,server.Port);
                    clients.Add(new GameClient("321654","Ana",5,7,ep));clients.Add(new GameClient("321654","Luis",8,11,ep));
                    Pump(server,clients,()=>clients.All(c=>c.Ready && c.State.Players.Count==4));
                    Check(server.Simulation.Connected==4 && clients.All(c=>c.State.Players.Any(p=>p.Name=="José" && p.KeyColor==4 && p.AuraColor==10)),"perfil sincronizado");
                    using(var fifth=new GameClient("321654","Quinto",0,0,ep)) {
                        var all=clients.Concat(new[]{fifth}).ToList();Pump(server,all,()=>fifth.Error!=null);Check(fifth.Error.Contains("llena"),"quinto rechazado");
                    }
                    using(var wrong=new GameClient("111111","Intruso",0,0,ep)) {
                        var all=clients.Concat(new[]{wrong}).ToList();Pump(server,all,()=>wrong.Error!=null);Check(wrong.Error.Contains("incorrectos"),"código rechazado");
                    }
                    Check(server.Simulation.Start(server.HostId),"host inicia");
                    Pump(server,clients,()=>clients.All(c=>c.State.Phase==Phase.Playing),5000,.03f);
                    server.Simulation.State.Items.Clear();var moving=server.Simulation.State.Player(clients[0].Id);moving.Position=new Position(0,0);
                    clients[0].Move(1,0);Pump(server,clients,()=>moving.Position.X>.1f);clients[0].Move(0,0);
                    moving.Inventory=Power.Freeze;clients[0].Use(Power.Freeze);
                    Pump(server,clients,()=>server.Simulation.State.Player(0).FreezeLeft>0);
                    Check(server.Simulation.State.Players.Where(p=>p.Id!=moving.Id).All(p=>p.FreezeLeft>0),"poder en servidor");
                    foreach(var p in server.Simulation.State.Players)p.Score=p.Id==moving.Id?500:100;
                    Pump(server,clients,()=>clients.All(c=>c.State.Phase==Phase.Results),4000,.03f);
                    Check(clients.All(c=>c.State.Winners.SequenceEqual(new[]{moving.Id})),"mismo resultado");
                    Check(server.Simulation.Lobby(server.HostId),"volver lobby");Pump(server,clients,()=>clients.All(c=>c.State.Phase==Phase.Lobby));
                    Check(server.Simulation.Start(server.HostId),"revancha");Pump(server,clients,()=>clients.All(c=>c.State.Round==2 && c.State.Phase==Phase.Countdown));
                    clients[2].Dispose();clients[1].Dispose();
                    Pump(server,new[]{clients[0]},()=>server.Simulation.Connected==2);
                    clients[0].Dispose();Pump(server,Array.Empty<GameClient>(),()=>server.Simulation.State.Phase==Phase.Results);
                } finally {foreach(var c in clients)c.Dispose();}
            }
        });
        Test("cliente informa cierre del anfitrión", () => {
            var server=new GameServer("123456","A",0,0,1);
            using(var client=new GameClient("123456","B",1,1,new IPEndPoint(IPAddress.Loopback,server.Port))) {
                Pump(server,new[]{client},()=>client.Ready);server.Dispose();var watch=Stopwatch.StartNew();
                while(client.Error==null && watch.ElapsedMilliseconds<1500){client.Update(.01f);Thread.Sleep(2);}
                Check(client.Error!=null && client.Error.Contains("conexión"),"desconexión detectada");
            }
        });
        Console.WriteLine("PASS " + passed + " grupos de pruebas; reglas y sockets reales. Unity Editor no ejecutado.");
    }
}
