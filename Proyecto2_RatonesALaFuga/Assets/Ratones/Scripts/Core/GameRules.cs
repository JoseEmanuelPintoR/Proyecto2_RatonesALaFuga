using System;
using System.Collections.Generic;

namespace Ratones.Core
{
    // La simulación no depende de Unity: el servidor es la única autoridad.
    public static class GameRules
    {
        public const int MinPlayers = 2, MaxPlayers = 4, CheeseCount = 50;
        public const float MapSize = 100f, Radius = 1.05f, BaseSpeed = 12f;
        public const float RoundSeconds = 120f, BoostSeconds = 4f, BoostMultiplier = 1.5f;
        public const float StickySeconds = 3f, StickyRange = 14f, PickupRadius = 1.8f;
        public const float PowerRespawnSeconds = 14f, InputTimeout = 0.4f;
        public const int Port = 7777, TickRate = 30, SnapshotRate = 15;
    }

    public struct Point
    {
        public float X, Z;
        public Point(float x, float z) { X = x; Z = z; }
        public static float DistanceSquared(Point a, Point b)
        { float x = a.X - b.X, z = a.Z - b.Z; return x * x + z * z; }
    }

    public struct Block
    {
        public float X, Z, Width, Depth, Height;
        public int Kind;
        public Block(float x, float z, float width, float depth, float height, int kind)
        { X = x; Z = z; Width = width; Depth = depth; Height = height; Kind = kind; }
        public bool Intersects(Point p, float radius)
        {
            float x = Math.Max(X - Width / 2, Math.Min(X + Width / 2, p.X));
            float z = Math.Max(Z - Depth / 2, Math.Min(Z + Depth / 2, p.Z));
            return Point.DistanceSquared(p, new Point(x, z)) < radius * radius;
        }
    }

    public static class KitchenMap
    {
        // Compartido por movimiento, generación de objetos y geometría visual.
        // Kind: 0 mueble, 1 nevera, 2 caja, 3 pata de mesa, 4 isla baja.
        public static readonly Block[] Blocks = {
            new Block(-36, 40, 22, 12, 15, 0),
            new Block(-8, 40, 22, 12, 15, 0),
            new Block(30, 39, 16, 16, 22, 1),
            new Block(-39, 10, 13, 18, 8, 2),
            new Block(-34, -30, 17, 13, 9, 2),
            new Block(33, -30, 17, 13, 8, 2),
            new Block(38, 7, 12, 18, 11, 0),
            new Block(-12, -10, 4, 4, 13, 3),
            new Block(12, -10, 4, 4, 13, 3),
            new Block(-12, 12, 4, 4, 13, 3),
            new Block(12, 12, 4, 4, 13, 3),
            new Block(0, -32, 14, 7, 3, 4)
        };

        public static bool Walkable(Point p, float radius)
        {
            float limit = GameRules.MapSize / 2 - radius;
            if (Math.Abs(p.X) > limit || Math.Abs(p.Z) > limit) return false;
            foreach (Block b in Blocks) if (b.Intersects(p, radius)) return false;
            return true;
        }

        public static Point Move(Point p, float dx, float dz)
        {
            // Subpasos: evita atravesar obstáculos incluso con un frame largo.
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dz)) / 0.35f));
            dx /= steps; dz /= steps;
            for (int i = 0; i < steps; i++)
            {
                Point next = new Point(p.X + dx, p.Z);
                if (Walkable(next, GameRules.Radius)) p = next;
                next = new Point(p.X, p.Z + dz);
                if (Walkable(next, GameRules.Radius)) p = next;
            }
            return p;
        }
    }

    public enum MatchPhase { Lobby, Countdown, Playing, Results }
    public enum ItemKind { Cheese, Sugar, Sticky }

    public sealed class PlayerState
    {
        public int Id, ColorIndex, Score, BoostCharges, StickyCharges;
        public string Name = "Ratón";
        public bool Connected = true;
        public Point Position;
        public float Heading, BoostLeft, StickyLeft, LastScoreAt;
        public float InputX, InputZ, InputAge;
        public bool RequestBoost, RequestSticky;
        public string Notice = "";
        public float NoticeLeft;
    }

    public sealed class ItemState
    {
        public int Id;
        public ItemKind Kind;
        public Point Position;
        public bool Active = true;
        public float RespawnLeft;
    }

    public sealed class MatchState
    {
        public MatchPhase Phase;
        public int Round, HostId = -1;
        public float TimeLeft = GameRules.RoundSeconds, CountdownLeft;
        public string ResultMessage = "";
        public List<PlayerState> Players = new List<PlayerState>();
        public List<ItemState> Items = new List<ItemState>();
        public List<int> Winners = new List<int>();
        public PlayerState Player(int id) { return Players.Find(p => p.Id == id); }
    }
}
