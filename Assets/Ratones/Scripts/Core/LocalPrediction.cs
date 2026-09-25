using System;
using System.Collections.Generic;

namespace Ratones.Basic
{
    // El invitado muestra su movimiento sin esperar el viaje de ida y vuelta.
    // Al recibir un estado, repite solamente entradas que el servidor todavía no confirmó.
    public sealed class LocalPrediction
    {
        readonly List<InputFrame> pending = new List<InputFrame>();
        Player authoritative;
        int round=-1, sequence;
        public Position Position { get; private set; }
        public bool Ready { get { return authoritative != null; } }
        public int PendingCount { get { return pending.Count; } }
        public void Reconcile(Player player, int currentRound, bool playing)
        {
            if (round != currentRound) { pending.Clear(); sequence=0; round=currentRound; }
            authoritative=player;
            if (player == null) { pending.Clear(); return; }
            pending.RemoveAll(frame=>frame.Sequence<=player.LastInputSequence);
            if (!playing) pending.Clear();
            Replay();
        }
        public InputFrame Push(float x, float z)
        {
            x=Math.Max(-1,Math.Min(1,x)); z=Math.Max(-1,Math.Min(1,z));
            float length=(float)Math.Sqrt(x*x+z*z); if(length>1) { x/=length; z/=length; }
            var frame=new InputFrame(++sequence,x,z);
            if (pending.Count>=60) pending.RemoveAt(0);
            pending.Add(frame); Replay(); return frame;
        }
        void Replay()
        {
            if (authoritative==null) return;
            Position=authoritative.Position;
            float elapsed=0, dt=1f/Rules.TickRate;
            int count=Math.Min(pending.Count,Rules.MaxPredictedInputs);
            for(int i=0;i<count;i++)
            {
                elapsed+=dt; if(authoritative.FreezeLeft>elapsed)continue;
                float step=Rules.Speed*(authoritative.BoostLeft>elapsed?Rules.BoostFactor:1)*dt;
                Position=ArenaLayout.Move(Position,pending[i].X*step,pending[i].Z*step);
            }
        }
    }
}
