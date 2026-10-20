namespace Ratones.Basic
{
    // Observa al jugador local. El contador evita perder recogidas si se usa y se
    // vuelve a recoger un poder entre dos snapshots, aunque el inventario sea igual.
    public sealed class PickupFeedback
    {
        int round = -1, playerId = -1, pickups;
        public bool Poll(State state, int localId)
        {
            Player own = state == null ? null : state.Player(localId);
            if (own == null || !own.Connected) { Reset(); return false; }
            if (round != state.Round || playerId != localId)
            {
                // Si se saltó la cuenta atrás por retraso de red, la primera recogida
                // de la ronda nueva sigue siendo nueva para un jugador ya observado.
                bool newPickup = playerId == localId && round >= 0 && round != state.Round && own.Pickups > 0 &&
                    (state.Phase == Phase.Playing || state.Phase == Phase.Results);
                round = state.Round; playerId = localId; pickups = own.Pickups; return newPickup;
            }
            bool collected = own.Pickups > pickups && (state.Phase == Phase.Playing || state.Phase == Phase.Results);
            pickups = own.Pickups;
            return collected;
        }
        public void Reset() { round = playerId = -1; pickups = 0; }
    }
}
