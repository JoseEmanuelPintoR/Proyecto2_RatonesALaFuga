using System;
using System.Collections.Generic;

namespace Ratones.Basic
{
    public static class Rules
    {
        public const int MinPlayers = 2, MaxPlayers = 4;
        public const int CheeseCount = 30, StrawberryCount = 15, PieCount = 5;
        public const int FoodCount = CheeseCount + StrawberryCount + PieCount;
        public const int PowerCount = 8, ColorCount = 12;
        public const float ArenaSize = 100, Radius = 1, Speed = 12, RoundTime = 120;
        public const float BoostTime = 4, BoostFactor = 1.5f, FreezeTime = 3, PickupRadius = 1.65f;
        public const float PowerRespawn = 15, InputTimeout = .4f;
        public const int TickRate = 30, SnapshotRate = 15, DiscoveryPort = 47777;
        public static int Points(ItemKind kind)
        { return kind == ItemKind.Queso ? 100 : kind == ItemKind.Fresa ? 200 : kind == ItemKind.Pie ? 500 : 0; }
        public static bool Food(ItemKind kind) { return (int)kind <= (int)ItemKind.Pie; }
    }
    public enum Phase { Lobby, Countdown, Playing, Results }
    public enum ItemKind { Queso, Fresa, Pie, Chocolate, Trampa }
    public enum Power { None, Speed, Freeze }
    public struct Position
    {
        public float X, Z;
        public Position(float x, float z) { X = x; Z = z; }
        public static float Distance2(Position a, Position b)
        { float x = a.X - b.X, z = a.Z - b.Z; return x * x + z * z; }
    }
    public sealed class Player
    {
        public int Id, Slot, KeyColor, AuraColor, Score, Collected;
        public string Name = "Jugador";
        public bool Connected = true;
        public Position Position;
        public float Angle, BoostLeft, FreezeLeft;
        public Power Inventory;
        // Solo el servidor mantiene estas entradas; no acepta posiciones del cliente.
        public float InputX, InputZ, InputAge;
        public bool WantsSpeed, WantsFreeze;
    }
    public sealed class Item
    {
        public int Id;
        public ItemKind Kind;
        public Position Position;
        public bool Active = true;
        public float RespawnLeft;
    }
    public sealed class State
    {
        public Phase Phase;
        public int Round, HostId;
        public float TimeLeft = Rules.RoundTime, CountdownLeft;
        public string Result = "";
        public readonly List<Player> Players = new List<Player>();
        public readonly List<Item> Items = new List<Item>();
        public readonly List<int> Winners = new List<int>();
        public Player Player(int id) { return Players.Find(p => p.Id == id); }
    }
}
