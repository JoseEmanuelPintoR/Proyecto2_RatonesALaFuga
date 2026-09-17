using System;
using System.Globalization;
using System.Text;
using Ratones.Core;

namespace Ratones.Network
{
    // Mensajes UTF-8 terminados en salto de línea. Textos libres en Base64.
    // No usa BinaryFormatter ni deserialización de tipos arbitrarios.
    public static class WireProtocol
    {
        public const string Version = "RATONES1";
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        public static string Number(float f) { return f.ToString("0.###", CI); }
        public static float Float(string s)
        {
            float f;
            if (!float.TryParse(s, NumberStyles.Float, CI, out f) || float.IsNaN(f) || float.IsInfinity(f))
                throw new FormatException("Número no válido.");
            return f;
        }
        public static string Encode(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? "")); }
        public static string Decode(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        public static string Input(float x, float z, bool boost, bool sticky)
        { return "I|" + Number(x) + "|" + Number(z) + "|" + (boost ? "1" : "0") + "|" + (sticky ? "1" : "0"); }

        public static string Snapshot(MatchState state)
        {
            var b = new StringBuilder(7000);
            b.Append("S|").Append((int)state.Phase).Append('|').Append(state.Round).Append('|')
                .Append(state.HostId).Append('|').Append(Number(state.TimeLeft)).Append('|')
                .Append(Number(state.CountdownLeft)).Append('|').Append(Encode(state.ResultMessage)).Append('|');
            for (int i = 0; i < state.Winners.Count; i++)
            { if (i > 0) b.Append(','); b.Append(state.Winners[i]); }
            b.Append('|');
            foreach (PlayerState p in state.Players)
            {
                b.Append(p.Id).Append(',').Append(Encode(p.Name)).Append(',').Append(p.Connected ? '1' : '0').Append(',')
                    .Append(Number(p.Position.X)).Append(',').Append(Number(p.Position.Z)).Append(',').Append(Number(p.Heading)).Append(',')
                    .Append(p.Score).Append(',').Append(p.BoostCharges).Append(',').Append(p.StickyCharges).Append(',')
                    .Append(Number(p.BoostLeft)).Append(',').Append(Number(p.StickyLeft)).Append(',').Append(Number(p.LastScoreAt)).Append(',')
                    .Append(Number(p.NoticeLeft)).Append(',').Append(Encode(p.Notice)).Append(',').Append(p.ColorIndex).Append(';');
            }
            b.Append('|');
            foreach (ItemState item in state.Items)
                b.Append(item.Id).Append(',').Append((int)item.Kind).Append(',').Append(Number(item.Position.X)).Append(',')
                    .Append(Number(item.Position.Z)).Append(',').Append(item.Active ? '1' : '0').Append(';');
            return b.ToString();
        }

        public static MatchState ParseSnapshot(string message)
        {
            string[] a = message.Split('|');
            if (a.Length != 10 || a[0] != "S") throw new FormatException("Estado no válido.");
            int phase = int.Parse(a[1]);
            if (phase < 0 || phase > 3) throw new FormatException("Fase no válida.");
            var state = new MatchState {
                Phase = (MatchPhase)phase, Round = int.Parse(a[2]), HostId = int.Parse(a[3]),
                TimeLeft = Float(a[4]), CountdownLeft = Float(a[5]), ResultMessage = Decode(a[6])
            };
            foreach (string w in a[7].Split(',')) if (w.Length > 0) state.Winners.Add(int.Parse(w));
            foreach (string row in a[8].Split(';'))
            {
                if (row.Length == 0) continue;
                string[] p = row.Split(',');
                if (p.Length != 15 || state.Players.Count >= 4) throw new FormatException("Jugador no válido.");
                state.Players.Add(new PlayerState {
                    Id = int.Parse(p[0]), Name = Decode(p[1]), Connected = p[2] == "1",
                    Position = new Point(Float(p[3]), Float(p[4])), Heading = Float(p[5]), Score = int.Parse(p[6]),
                    BoostCharges = int.Parse(p[7]), StickyCharges = int.Parse(p[8]), BoostLeft = Float(p[9]),
                    StickyLeft = Float(p[10]), LastScoreAt = Float(p[11]), NoticeLeft = Float(p[12]), Notice = Decode(p[13]),
                    ColorIndex = Math.Max(0, Math.Min(3, int.Parse(p[14])))
                });
            }
            foreach (string row in a[9].Split(';'))
            {
                if (row.Length == 0) continue;
                string[] p = row.Split(',');
                if (p.Length != 5 || state.Items.Count >= 58) throw new FormatException("Objeto no válido.");
                int kind = int.Parse(p[1]);
                if (kind < 0 || kind > 2) throw new FormatException("Tipo no válido.");
                state.Items.Add(new ItemState { Id = int.Parse(p[0]), Kind = (ItemKind)kind,
                    Position = new Point(Float(p[2]), Float(p[3])), Active = p[4] == "1" });
            }
            return state;
        }
    }
}
