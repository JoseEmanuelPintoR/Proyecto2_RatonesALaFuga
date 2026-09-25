using System;
using System.Globalization;
using System.Text;

namespace Ratones.Basic
{
    public static class Protocol
    {
        public const string Version = "RF4";
        public static string Encode(string text) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? "")); }
        public static string Decode(string text) { return Encoding.UTF8.GetString(Convert.FromBase64String(text)); }
        public static string Num(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
        public static float Float(string text)
        {
            float result;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result) || float.IsNaN(result) || float.IsInfinity(result))
                throw new FormatException("Número no válido.");
            return result;
        }
        public static string Snapshot(State state, bool includeItems = true)
        {
            var s = new StringBuilder(includeItems ? 3000 : 600);
            s.Append("S|").Append((int)state.Phase).Append('|').Append(state.Round).Append('|').Append(state.HostId).Append('|')
                .Append(Num(state.TimeLeft)).Append('|').Append(Num(state.CountdownLeft)).Append('|').Append(Encode(state.Result)).Append('|');
            for (int i = 0; i < state.Winners.Count; i++) { if (i > 0) s.Append(','); s.Append(state.Winners[i]); }
            s.Append('|');
            foreach (Player p in state.Players)
                s.Append(p.Id).Append(',').Append(p.Slot).Append(',').Append(p.KeyColor).Append(',').Append(p.AuraColor).Append(',')
                    .Append(Encode(p.Name)).Append(',').Append(p.Connected ? 1 : 0).Append(',')
                    .Append(Num(p.Position.X)).Append(',').Append(Num(p.Position.Z)).Append(',').Append(Num(p.Angle)).Append(',')
                    .Append(p.Score).Append(',').Append(p.Collected).Append(',').Append((int)p.Inventory).Append(',')
                    .Append(Num(p.BoostLeft)).Append(',').Append(Num(p.FreezeLeft)).Append(',').Append(p.LastInputSequence).Append(';');
            s.Append('|').Append(state.WorldRevision).Append('|');
            if (!includeItems) s.Append('~');
            else foreach (Item i in state.Items)
                s.Append(i.Id).Append(',').Append((int)i.Kind).Append(',').Append(Num(i.Position.X)).Append(',').Append(Num(i.Position.Z))
                    .Append(',').Append(i.Active ? 1 : 0).Append(';');
            return s.ToString();
        }
        public static State Parse(string line, State previous = null)
        {
            string[] a = line.Split('|');
            if (a.Length != 11 || a[0] != "S") throw new FormatException("Estado incompleto.");
            int phase = int.Parse(a[1]); if (phase < 0 || phase > 3) throw new FormatException("Fase incorrecta.");
            var state = new State { Phase = (Phase)phase, Round = int.Parse(a[2]), HostId = int.Parse(a[3]),
                TimeLeft = Float(a[4]), CountdownLeft = Float(a[5]), Result = Decode(a[6]), WorldRevision = int.Parse(a[9]) };
            foreach (string id in a[7].Split(',')) if (id.Length > 0) state.Winners.Add(int.Parse(id));
            foreach (string row in a[8].Split(';'))
            {
                if (row.Length == 0) continue;
                string[] p = row.Split(',');
                if (p.Length != 15 || state.Players.Count >= 4) throw new FormatException("Jugador incorrecto.");
                int slot = int.Parse(p[1]), inv = int.Parse(p[11]);
                if (slot < 0 || slot > 3 || inv < 0 || inv > 3) throw new FormatException("Estado de jugador incorrecto.");
                state.Players.Add(new Player { Id = int.Parse(p[0]), Slot = slot, KeyColor = Simulation.ColorIndex(int.Parse(p[2])),
                    AuraColor = Simulation.ColorIndex(int.Parse(p[3])), Name = Decode(p[4]), Connected = p[5] == "1",
                    Position = new Position(Float(p[6]), Float(p[7])), Angle = Float(p[8]), Score = int.Parse(p[9]),
                    Collected = int.Parse(p[10]), Inventory = (Power)inv, BoostLeft = Float(p[12]), FreezeLeft = Float(p[13]), LastInputSequence = int.Parse(p[14]) });
            }
            if (a[10] == "~")
            {
                if (previous == null || previous.Round != state.Round || previous.WorldRevision != state.WorldRevision)
                    throw new FormatException("Falta el estado inicial de los objetos.");
                state.Items.AddRange(previous.Items); return state;
            }
            foreach (string row in a[10].Split(';'))
            {
                if (row.Length == 0) continue;
                string[] i = row.Split(','); int kind;
                if (i.Length != 5 || state.Items.Count >= Rules.FoodCount + Rules.PowerCount) throw new FormatException("Objeto incorrecto.");
                kind = int.Parse(i[1]); if (kind < 0 || kind > 4) throw new FormatException("Tipo incorrecto.");
                state.Items.Add(new Item { Id = int.Parse(i[0]), Kind = (ItemKind)kind,
                    Position = new Position(Float(i[2]), Float(i[3])), Active = i[4] == "1" });
            }
            return state;
        }
    }
}
