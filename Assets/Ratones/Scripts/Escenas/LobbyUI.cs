using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class LobbyUI : MonoBehaviour
    {
        public Text CodeText, Message;
        public Text[] Names;
        public Image[] Auras, Keys;
        public Button StartButton;
        void Start() { if (!Session.Ensure().Connected) Session.Ensure().Go("Conexion"); }
        void Update()
        {
            Session session = Session.Ensure(); State state = session.State;
            if (state == null) return;
            CodeText.text = "Código: " + session.RoomCode;
            for (int i = 0; i < 4; i++)
            {
                Player p = state.Players.FirstOrDefault(x => x.Connected && x.Slot == i);
                Names[i].text = p == null ? "Esperando…" : p.Name + (p.Id == session.LocalId ? " (tú)" : "") + (p.Id == state.HostId ? "\nAnfitrión" : "");
                Auras[i].color = p == null ? Color.gray : Palette.Colors[p.AuraColor];
                Keys[i].color = p == null ? Color.gray : Palette.Colors[p.KeyColor];
            }
            int count = state.Players.Count(p => p.Connected);
            StartButton.interactable = session.IsHost && count >= 2 && state.Phase == Phase.Lobby;
            Message.text = count < 2 ? "Se necesitan al menos 2 jugadores." :
                state.Phase == Phase.Results ? "Esperando a que el anfitrión vuelva al lobby." :
                session.IsHost ? count + "/4 jugadores. Ya puedes iniciar." : "Esperando que el anfitrión inicie.";
        }
        public void StartGame() { Session.Ensure().StartRound(); }
        public void Back() { Session.Ensure().LeaveToConnection(); }
    }
}
