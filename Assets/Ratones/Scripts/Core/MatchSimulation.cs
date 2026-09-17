using System;
using System.Collections.Generic;
using System.Linq;

namespace Ratones.Core
{
    public sealed class MatchSimulation
    {
        public readonly MatchState State = new MatchState();
        readonly Random random;
        readonly float roundLength;
        int nextId;
        int tick;

        public MatchSimulation(int seed, float duration = GameRules.RoundSeconds)
        {
            random = new Random(seed);
            roundLength = Math.Max(1f, duration);
            State.TimeLeft = roundLength;
        }

        public int ConnectedCount { get { return State.Players.Count(p => p.Connected); } }

        public int Join(string name)
        {
            if (State.Phase != MatchPhase.Lobby || ConnectedCount >= GameRules.MaxPlayers) return -1;
            var player = new PlayerState { Id = nextId++, Name = CleanName(name) };
            player.ColorIndex = Enumerable.Range(0, 4).First(c => !State.Players.Any(p => p.Connected && p.ColorIndex == c));
            State.Players.Add(player);
            if (State.HostId < 0) State.HostId = player.Id;
            return player.Id;
        }

        static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Ratón";
            string s = new string(name.Trim().Where(c => !char.IsControl(c) && c != '<' && c != '>').ToArray());
            return s.Length > 18 ? s.Substring(0, 18) : (s.Length == 0 ? "Ratón" : s);
        }

        public void Leave(int id)
        {
            PlayerState p = State.Player(id);
            if (p == null) return;
            p.Connected = false; p.InputX = p.InputZ = 0;
            if (State.Phase == MatchPhase.Lobby) State.Players.Remove(p);
            else if ((State.Phase == MatchPhase.Playing || State.Phase == MatchPhase.Countdown) && ConnectedCount < 2)
            {
                State.Phase = MatchPhase.Results;
                State.Winners.Clear();
                State.ResultMessage = "Ronda cancelada: se necesitan al menos 2 jugadores.";
            }
        }

        public bool Start(int requester)
        {
            if (requester != State.HostId || ConnectedCount < 2 || ConnectedCount > 4 || State.Phase != MatchPhase.Lobby)
                return false;
            State.Round++; State.Winners.Clear(); State.Items.Clear();
            State.ResultMessage = ""; State.TimeLeft = roundLength; State.CountdownLeft = 3f;
            State.Players.RemoveAll(p => !p.Connected);
            List<Point> occupied = new List<Point>();
            foreach (PlayerState p in State.Players)
            {
                p.Position = Spawn(occupied, 14f);
                occupied.Add(p.Position);
                p.Score = 0; p.LastScoreAt = 0; p.Heading = 0;
                p.BoostLeft = p.StickyLeft = p.InputX = p.InputZ = 0;
                p.BoostCharges = p.StickyCharges = 0;
                p.RequestBoost = p.RequestSticky = false;
                p.InputAge = 0; p.NoticeLeft = 0; p.Notice = "";
            }
            for (int i = 0; i < GameRules.CheeseCount + 8; i++)
            {
                Point position = Spawn(occupied, 3.2f);
                occupied.Add(position);
                State.Items.Add(new ItemState {
                    Id = i, Position = position,
                    Kind = i < GameRules.CheeseCount ? ItemKind.Cheese : (i % 2 == 0 ? ItemKind.Sugar : ItemKind.Sticky)
                });
            }
            State.Phase = MatchPhase.Countdown;
            return true;
        }

        Point Spawn(List<Point> occupied, float separation)
        {
            for (int i = 0; i < 20000; i++)
            {
                Point p = new Point((float)random.NextDouble() * 92 - 46, (float)random.NextDouble() * 92 - 46);
                if (!KitchenMap.Walkable(p, 2.2f)) continue;
                bool overlaps = occupied.Any(q => Point.DistanceSquared(p, q) < separation * separation);
                if (!overlaps) return p;
            }
            throw new InvalidOperationException("El mapa no tiene espacio suficiente para generar los objetos.");
        }

        public bool ReturnToLobby(int requester)
        {
            if (requester != State.HostId || State.Phase != MatchPhase.Results) return false;
            State.Phase = MatchPhase.Lobby;
            State.Players.RemoveAll(p => !p.Connected);
            State.Winners.Clear(); State.Items.Clear(); State.ResultMessage = "";
            foreach (PlayerState p in State.Players) { p.Score = 0; p.InputX = p.InputZ = 0; }
            return true;
        }

        public void Input(int id, float x, float z, bool boost, bool sticky)
        {
            PlayerState p = State.Player(id);
            if (p == null || !p.Connected || State.Phase != MatchPhase.Playing) return;
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z)) return;
            x = Math.Max(-1f, Math.Min(1f, x)); z = Math.Max(-1f, Math.Min(1f, z));
            float length = (float)Math.Sqrt(x * x + z * z);
            if (length > 1) { x /= length; z /= length; }
            p.InputX = x; p.InputZ = z; p.InputAge = 0;
            p.RequestBoost |= boost; p.RequestSticky |= sticky;
        }

        public void Tick(float dt)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (State.Phase == MatchPhase.Countdown)
            {
                State.CountdownLeft = Math.Max(0, State.CountdownLeft - dt);
                if (State.CountdownLeft <= 0) State.Phase = MatchPhase.Playing;
                return;
            }
            if (State.Phase != MatchPhase.Playing) return;
            dt = Math.Min(dt, State.TimeLeft);
            foreach (PlayerState p in State.Players)
            {
                p.BoostLeft = Math.Max(0, p.BoostLeft - dt);
                p.StickyLeft = Math.Max(0, p.StickyLeft - dt);
                p.NoticeLeft = Math.Max(0, p.NoticeLeft - dt);
                p.InputAge += dt;
                if (p.InputAge > GameRules.InputTimeout) p.InputX = p.InputZ = 0;
            }
            // Rotar el orden evita favorecer siempre al host en acciones del mismo tick.
            var active = State.Players.Where(p => p.Connected).ToList();
            for (int i = 0; i < active.Count; i++)
            {
                PlayerState p = active[(i + tick) % active.Count];
                if (p.RequestBoost) UseBoost(p);
                if (p.RequestSticky) UseSticky(p);
                p.RequestBoost = p.RequestSticky = false;
            }
            float movementDt = Math.Min(dt, 0.1f);
            foreach (PlayerState p in active)
            {
                if (p.StickyLeft > 0) continue;
                float speed = GameRules.BaseSpeed * (p.BoostLeft > 0 ? GameRules.BoostMultiplier : 1f);
                p.Position = KitchenMap.Move(p.Position, p.InputX * speed * movementDt, p.InputZ * speed * movementDt);
                if (Math.Abs(p.InputX) + Math.Abs(p.InputZ) > 0.01f)
                    p.Heading = (float)(Math.Atan2(p.InputX, p.InputZ) * 180 / Math.PI);
            }
            foreach (ItemState item in State.Items)
            {
                if (!item.Active)
                {
                    if (item.Kind != ItemKind.Cheese)
                    { item.RespawnLeft -= dt; if (item.RespawnLeft <= 0) item.Active = true; }
                    continue;
                }
                PlayerState closest = null;
                float distance = GameRules.PickupRadius * GameRules.PickupRadius;
                for (int i = 0; i < active.Count; i++)
                {
                    PlayerState p = active[(i + tick) % active.Count];
                    if (p.StickyLeft > 0 || (item.Kind == ItemKind.Sugar && p.BoostCharges > 0)
                        || (item.Kind == ItemKind.Sticky && p.StickyCharges > 0)) continue;
                    float d = Point.DistanceSquared(p.Position, item.Position);
                    if (d < distance) { distance = d; closest = p; }
                }
                if (closest == null) continue;
                item.Active = false; item.RespawnLeft = GameRules.PowerRespawnSeconds;
                if (item.Kind == ItemKind.Cheese)
                { closest.Score++; closest.LastScoreAt = roundLength - State.TimeLeft; }
                else if (item.Kind == ItemKind.Sugar) { closest.BoostCharges = 1; Notice(closest, "¡Subidón de Azúcar listo!"); }
                else { closest.StickyCharges = 1; Notice(closest, "¡Trampa Pegajosa lista!"); }
            }
            tick = (tick + 1) % 100000;
            State.TimeLeft = Math.Max(0, State.TimeLeft - dt);
            // La ronda dura el tiempo completo, aunque se recojan todos los quesos.
            if (State.TimeLeft <= 0) Finish();
        }

        void UseBoost(PlayerState p)
        {
            if (p.StickyLeft > 0) { Notice(p, "¡Estás pegado!"); return; }
            if (p.BoostCharges < 1) { Notice(p, "Busca un caramelo azul."); return; }
            if (p.BoostLeft > 0) return;
            p.BoostCharges--; p.BoostLeft = GameRules.BoostSeconds;
            Notice(p, "¡Subidón de Azúcar!");
        }

        void UseSticky(PlayerState p)
        {
            if (p.StickyLeft > 0) { Notice(p, "¡Estás pegado!"); return; }
            if (p.StickyCharges < 1) { Notice(p, "Busca un frasco violeta."); return; }
            PlayerState target = null;
            float best = GameRules.StickyRange * GameRules.StickyRange;
            foreach (PlayerState rival in State.Players)
            {
                if (!rival.Connected || rival.Id == p.Id || rival.StickyLeft > 0) continue;
                float d = Point.DistanceSquared(p.Position, rival.Position);
                if (d < best && LineClear(p.Position, rival.Position)) { best = d; target = rival; }
            }
            if (target == null) { Notice(p, "Acércate a un rival visible (14 m)."); return; }
            p.StickyCharges--; target.StickyLeft = GameRules.StickySeconds;
            Notice(p, "¡Atrapaste a " + target.Name + "!");
            Notice(target, "¡" + p.Name + " te dejó pegado!");
        }

        static bool LineClear(Point a, Point b)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Point.DistanceSquared(a, b)) * 2));
            for (int i = 1; i < steps; i++)
            {
                float t = (float)i / steps;
                if (!KitchenMap.Walkable(new Point(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t), 0.15f)) return false;
            }
            return true;
        }

        static void Notice(PlayerState p, string text) { p.Notice = text; p.NoticeLeft = 2.5f; }

        void Finish()
        {
            State.Phase = MatchPhase.Results;
            var ranked = State.Players.Where(p => p.Connected)
                .OrderByDescending(p => p.Score).ThenBy(p => p.LastScoreAt).ToList();
            State.Winners.Clear();
            if (ranked.Count == 0) { State.ResultMessage = "No quedan jugadores conectados."; return; }
            PlayerState best = ranked[0];
            foreach (PlayerState p in ranked)
                if (p.Score == best.Score && Math.Abs(p.LastScoreAt - best.LastScoreAt) < 0.001f) State.Winners.Add(p.Id);
            State.ResultMessage = State.Winners.Count > 1 ? "¡Victoria compartida!" : "¡" + best.Name + " se lleva el queso!";
        }
    }
}
