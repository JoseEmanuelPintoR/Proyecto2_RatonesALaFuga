using System;

namespace Ratones.Basic
{
    public sealed class ArenaBlock
    {
        public readonly float X, Z, Width, Depth, Height;
        public ArenaBlock(float x, float z, float width, float depth, float height)
        { X=x; Z=z; Width=width; Depth=depth; Height=height; }
    }
    public sealed class RaisedArea
    {
        public readonly float X, Z, Width, FlatLength, RampLength, Height;
        public readonly bool AlongX;
        public float HalfLength { get { return FlatLength / 2 + RampLength; } }
        public RaisedArea(float x, float z, bool alongX, float width=12, float flatLength=10, float rampLength=10, float height=4)
        { X=x; Z=z; AlongX=alongX; Width=width; FlatLength=flatLength; RampLength=rampLength; Height=height; }
        public float HeightAt(Position point)
        {
            float length = Math.Abs(AlongX ? point.X-X : point.Z-Z);
            float across = Math.Abs(AlongX ? point.Z-Z : point.X-X);
            if (across > Width/2 || length >= HalfLength) return 0;
            return length <= FlatLength/2 ? Height : Height*(HalfLength-length)/RampLength;
        }
    }
    // La misma geometría alimenta los objetos de Unity y las colisiones del servidor.
    // Si cambias esta distribución, reconstruye el mapa y actualiza todas las copias del juego.
    public static class ArenaLayout
    {
        public static readonly RaisedArea[] Platforms = {
            new RaisedArea(-22,-12,false), new RaisedArea(22,18,true)
        };
        public static readonly ArenaBlock[] Blocks = {
            new ArenaBlock(-12,18,10,4,3), new ArenaBlock(9,-17,6,8,3),
            new ArenaBlock(-32,29,8,8,4), new ArenaBlock(33,-31,10,5,3),
            new ArenaBlock(-3,-35,4,10,3), new ArenaBlock(1,32,10,4,3)
        };
        const float Substep = .15f, MaxStepHeight = .25f;
        static readonly Position[] Probes = {
            new Position(1,0),new Position(.7071068f,.7071068f),new Position(0,1),new Position(-.7071068f,.7071068f),
            new Position(-1,0),new Position(-.7071068f,-.7071068f),new Position(0,-1),new Position(.7071068f,-.7071068f)
        };
        public static float HeightAt(Position point)
        {
            float height=0;
            foreach (RaisedArea area in Platforms) height=Math.Max(height,area.HeightAt(point));
            return height;
        }
        public static bool Walkable(Position point, float radius=Rules.Radius)
        {
            float limit=Rules.ArenaSize/2-radius;
            if (Math.Abs(point.X)>limit || Math.Abs(point.Z)>limit) return false;
            foreach (ArenaBlock block in Blocks)
            {
                float dx=Math.Max(Math.Abs(point.X-block.X)-block.Width/2,0);
                float dz=Math.Max(Math.Abs(point.Z-block.Z)-block.Depth/2,0);
                if (dx*dx+dz*dz < radius*radius-.00001f) return false;
            }
            // Evita atravesar los costados altos o salir por los bordes de las plataformas.
            // En las pendientes, la diferencia permitida depende de la inclinación de la rampa.
            float slope=0; foreach(RaisedArea a in Platforms) slope=Math.Max(slope,a.Height/a.RampLength);
            float height=HeightAt(point), tolerance=radius*slope+.1f;
            foreach (Position direction in Probes)
            {
                var probe=new Position(point.X+direction.X*radius,point.Z+direction.Z*radius);
                if (Math.Abs(HeightAt(probe)-height)>tolerance) return false;
            }
            return true;
        }
        static bool CanStep(Position from, Position to)
        { return Walkable(to) && Math.Abs(HeightAt(to)-HeightAt(from))<=MaxStepHeight; }
        static float Clamp(float value)
        { float limit=Rules.ArenaSize/2-Rules.Radius; return Math.Max(-limit,Math.Min(limit,value)); }
        public static Position Move(Position from, float dx, float dz)
        {
            // Subpasos incluso con velocidad extra: no se puede saltar un bloque entre ticks.
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dz*dz)/Substep));
            dx/=steps; dz/=steps;
            for(int i=0;i<steps;i++)
            {
                var next=new Position(Clamp(from.X+dx),Clamp(from.Z+dz));
                if(CanStep(from,next)) { from=next; continue; }
                // Deslizar por los lados del obstáculo en vez de atascar el movimiento diagonal.
                next=new Position(Clamp(from.X+dx),from.Z); if(CanStep(from,next))from=next;
                next=new Position(from.X,Clamp(from.Z+dz)); if(CanStep(from,next))from=next;
            }
            return from;
        }
    }
}
