using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class ResultsUI : MonoBehaviour
    {
        public Text Title;
        // Orden lógico 1, 2, 3; la escena los dispone centro, izquierda, derecha.
        public Text[] Names, Scores, Places;
        public GameObject[] Podiums;
        public Image[] Colors;
        public Text Fourth;
        void Start()
        {
            Session session = Session.Ensure(); State state = session.State;
            if (state == null) { session.Go("MenuInicio"); return; }
            Title.text = state.Result;
            var players = state.Players.Where(p => p.Connected).OrderByDescending(p => p.Score).ThenBy(p => p.Slot).ToList();
            for (int i = 0; i < 3; i++)
            {
                Podiums[i].SetActive(i < players.Count);
                if (i >= players.Count) continue;
                Player p = players[i];
                int place = 1 + players.Count(q => q.Score > p.Score);
                bool tie = players.Count(q => q.Score == p.Score) > 1;
                Names[i].text = p.Name; Scores[i].text = p.Score + " puntos";
                Places[i].text = place + ".º" + (tie ? " · empate" : "");
                Colors[i].color = Palette.Colors[p.AuraColor];
            }
            if (players.Count == 4)
            {
                Player p = players[3]; int place = 1 + players.Count(q => q.Score > p.Score);
                Fourth.text = place + ".º  " + p.Name + "  ·  " + p.Score + " puntos";
            }
            else Fourth.text = "";
        }
        public void Lobby() { Session.Ensure().BackToLobby(); }
        public void Menu() { Session.Ensure().LeaveToMenu(); }
    }
}
