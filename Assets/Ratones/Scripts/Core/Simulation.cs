using System;
using System.Collections.Generic;
using System.Linq;

namespace Ratones.Basic
{
    // Mismas reglas para PC y móvil. No utiliza ninguna clase de Unity.
    public sealed class Simulation
    {
        public readonly State State = new State();
        readonly Random random;
        readonly float duration;
        int nextId, turn;
        public int Connected { get { return State.Players.Count(p => p.Connected); } }
        public Simulation(int seed, float roundDuration = Rules.RoundTime)
        { random = new Random(seed); duration = Math.Max(1, roundDuration); State.TimeLeft = duration; State.HostId = -1; }
        public int Join(string name, int key, int aura)
        {
            if (State.Phase != Phase.Lobby || Connected >= Rules.MaxPlayers) return -1;
            var p = new Player { Id = nextId++, Name = CleanName(name), KeyColor = ColorIndex(key), AuraColor = ColorIndex(aura) };
            p.Slot = Enumerable.Range(0, 4).First(i => !State.Players.Any(q => q.Connected && q.Slot == i));
            State.Players.Add(p); if (State.HostId < 0) State.HostId = p.Id;
            return p.Id;
        }
        public static string CleanName(string name)
        {
            string text = new string((name ?? "").Trim().Where(c => !char.IsControl(c) && c != '<' && c != '>').ToArray());
            if (text.Length == 0) return "Jugador";
            return text.Length > 18 ? text.Substring(0, 18) : text;
        }
        public static int ColorIndex(int n) { return Math.Max(0, Math.Min(Rules.ColorCount - 1, n)); }
        public void Leave(int id)
        {
            Player p = State.Player(id); if (p == null) return;
            p.Connected = false; p.InputX = p.InputZ = 0;
            if (State.Phase == Phase.Lobby) State.Players.Remove(p);
            else if ((State.Phase == Phase.Playing || State.Phase == Phase.Countdown) && Connected < 2)
            {
                State.Phase = Phase.Results; State.Winners.Clear();
                State.Result = "Partida cancelada: queda menos de 2 jugadores.";
            }
        }
        public bool Start(int requester)
        {
            if (requester != State.HostId || State.Phase != Phase.Lobby || Connected < 2) return false;
            State.Players.RemoveAll(p => !p.Connected);
            State.Round++; State.Items.Clear(); State.Winners.Clear(); State.Result = "";
            State.TimeLeft = duration; State.CountdownLeft = 3;
            var used = new List<Position>();
            foreach (Player p in State.Players)
            {
                p.Position = Spawn(used, 12); used.Add(p.Position);
                p.Score = p.Collected = 0; p.Inventory = Power.None;
                p.BoostLeft = p.FreezeLeft = p.InputX = p.InputZ = p.InputAge = p.Angle = 0;
                p.WantsSpeed = p.WantsFreeze = false;
            }
            // Los valores altos son menos frecuentes: 30 quesos, 15 fresas y 5 pies.
            for (int i = 0; i < Rules.FoodCount + Rules.PowerCount; i++)
            {
                ItemKind kind = i < Rules.CheeseCount ? ItemKind.Queso :
                    i < Rules.CheeseCount + Rules.StrawberryCount ? ItemKind.Fresa :
                    i < Rules.FoodCount ? ItemKind.Pie : i % 2 == 0 ? ItemKind.Chocolate : ItemKind.Trampa;
                Position position = Spawn(used, 3.4f); used.Add(position);
                State.Items.Add(new Item { Id = i, Kind = kind, Position = position });
            }
            State.Phase = Phase.Countdown; return true;
        }
        Position Spawn(List<Position> occupied, float distance)
        {
            for (int attempt = 0; attempt < 20000; attempt++)
            {
                var p = new Position((float)random.NextDouble() * 90 - 45, (float)random.NextDouble() * 90 - 45);
                if (occupied.All(q => Position.Distance2(p, q) >= distance * distance)) return p;
            }
            throw new InvalidOperationException("No hay espacio para generar los objetos.");
        }
        public bool Lobby(int requester)
        {
            if (requester != State.HostId || State.Phase != Phase.Results) return false;
            State.Phase = Phase.Lobby; State.Players.RemoveAll(p => !p.Connected);
            State.Items.Clear(); return true;
        }
        public void Move(int id, float x, float z)
        {
            Player p = State.Player(id);
            if (p == null || !p.Connected || State.Phase != Phase.Playing || !Finite(x) || !Finite(z)) return;
            x = Math.Max(-1, Math.Min(1, x)); z = Math.Max(-1, Math.Min(1, z));
            float magnitude = (float)Math.Sqrt(x * x + z * z);
            if (magnitude > 1) { x /= magnitude; z /= magnitude; }
            p.InputX = x; p.InputZ = z; p.InputAge = 0;
        }
        static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        public void Use(int id, Power power)
        {
            Player p = State.Player(id);
            if (p == null || !p.Connected || State.Phase != Phase.Playing || p.Inventory != power || p.FreezeLeft > 0) return;
            if (power == Power.Speed) p.WantsSpeed = true;
            else if (power == Power.Freeze) p.WantsFreeze = true;
        }
        public void Tick(float dt)
        {
            if (!Finite(dt) || dt <= 0) return;
            if (State.Phase == Phase.Countdown)
            {
                State.CountdownLeft = Math.Max(0, State.CountdownLeft - dt);
                if (State.CountdownLeft == 0) State.Phase = Phase.Playing;
                return;
            }
            if (State.Phase != Phase.Playing) return;
            dt = Math.Min(dt, State.TimeLeft);
            foreach (Player p in State.Players)
            {
                p.BoostLeft = Math.Max(0, p.BoostLeft - dt); p.FreezeLeft = Math.Max(0, p.FreezeLeft - dt);
                p.InputAge += dt; if (p.InputAge > Rules.InputTimeout) p.InputX = p.InputZ = 0;
            }
            var active = State.Players.Where(p => p.Connected).ToList();
            for (int i = 0; i < active.Count; i++)
            {
                Player p = active[(i + turn) % active.Count];
                if (p.FreezeLeft <= 0 && p.WantsSpeed && p.Inventory == Power.Speed && p.BoostLeft <= 0)
                { p.Inventory = Power.None; p.BoostLeft = Rules.BoostTime; }
                if (p.FreezeLeft <= 0 && p.WantsFreeze && p.Inventory == Power.Freeze)
                {
                    p.Inventory = Power.None;
                    // La trampa afecta a TODOS los rivales; no hay rango ni selección.
                    foreach (Player q in active) if (q.Id != p.Id) q.FreezeLeft = Rules.FreezeTime;
                }
                p.WantsSpeed = p.WantsFreeze = false;
            }
            foreach (Player p in active)
            {
                if (p.FreezeLeft > 0) continue;
                float step = Rules.Speed * (p.BoostLeft > 0 ? Rules.BoostFactor : 1) * Math.Min(dt, .1f);
                float limit = Rules.ArenaSize / 2 - Rules.Radius;
                p.Position = new Position(Math.Max(-limit, Math.Min(limit, p.Position.X + p.InputX * step)),
                    Math.Max(-limit, Math.Min(limit, p.Position.Z + p.InputZ * step)));
                if (Math.Abs(p.InputX) + Math.Abs(p.InputZ) > .01f) p.Angle = (float)(Math.Atan2(p.InputX, p.InputZ) * 180 / Math.PI);
            }
            foreach (Item item in State.Items)
            {
                if (!item.Active)
                {
                    if (!Rules.Food(item.Kind)) { item.RespawnLeft -= dt; if (item.RespawnLeft <= 0) item.Active = true; }
                    continue;
                }
                Player collector = null; float nearest = Rules.PickupRadius * Rules.PickupRadius;
                for (int i = 0; i < active.Count; i++)
                {
                    Player p = active[(i + turn) % active.Count];
                    if (p.FreezeLeft > 0 || (!Rules.Food(item.Kind) && p.Inventory != Power.None)) continue;
                    float d = Position.Distance2(p.Position, item.Position);
                    if (d < nearest) { nearest = d; collector = p; }
                }
                if (collector == null) continue;
                item.Active = false; item.RespawnLeft = Rules.PowerRespawn;
                if (Rules.Food(item.Kind)) { collector.Collected++; collector.Score += Rules.Points(item.Kind); }
                else collector.Inventory = item.Kind == ItemKind.Chocolate ? Power.Speed : Power.Freeze;
            }
            turn = (turn + 1) % 100000;
            State.TimeLeft = Math.Max(0, State.TimeLeft - dt);
            if (State.TimeLeft <= 0) Finish();
        }
        void Finish()
        {
            State.Phase = Phase.Results; State.Winners.Clear();
            var active = State.Players.Where(p => p.Connected).ToList();
            if (active.Count == 0) { State.Result = "No quedan jugadores."; return; }
            int best = active.Max(p => p.Score);
            State.Winners.AddRange(active.Where(p => p.Score == best).Select(p => p.Id));
            State.Result = State.Winners.Count > 1 ? "Empate" : "Ganador: " + active.First(p => p.Id == State.Winners[0]).Name;
        }
    }
}
