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
        public Button PersonalizeButton;
        void Start()
        {
            if (!Session.Ensure().Connected) { Session.Ensure().Go("Conexion"); return; }
            Button button = EnsurePersonalizeButton();
            if (button == null) return;
            // El clon de Iniciar nunca debe conservar su acción. Reutiliza un botón
            // que ya exista y conserva los demás eventos que haya añadido el equipo.
            bool linked = false;
            for (int i=0; i<button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) != this) continue;
                string method=button.onClick.GetPersistentMethodName(i);
                if (method == nameof(StartGame) || method == nameof(Back))
                    button.onClick.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.Off);
                if (method == nameof(OpenPersonalize))
                { button.onClick.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.RuntimeOnly); linked=true; }
            }
            if (!linked) button.onClick.AddListener(OpenPersonalize);
        }
        public Button FindPersonalizeButton()
        {
            if (PersonalizeButton != null && PersonalizeButton != StartButton) return PersonalizeButton;
            if (StartButton == null) return null;
            Canvas canvas = StartButton.GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : StartButton.transform.parent;
            if (root == null) return null;
            foreach (Button candidate in root.GetComponentsInChildren<Button>(true))
            {
                if (candidate == StartButton) continue;
                for (int i=0; i<candidate.onClick.GetPersistentEventCount(); i++)
                    if (candidate.onClick.GetPersistentTarget(i)==this && candidate.onClick.GetPersistentMethodName(i)==nameof(OpenPersonalize))
                        return candidate;
                Text label = candidate.GetComponentInChildren<Text>(true);
                if (string.Equals(candidate.name,"Personalizar",System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.name,"PersonalizarLobby",System.StringComparison.OrdinalIgnoreCase) ||
                    (label != null && string.Equals((label.text ?? "").Trim(),"Personalizar",System.StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }
            return null;
        }
        public Button EnsurePersonalizeButton()
        {
            PersonalizeButton = FindPersonalizeButton();
            if (PersonalizeButton != null || StartButton == null) return PersonalizeButton;
            GameObject copy = Instantiate(StartButton.gameObject,StartButton.transform.parent,false);
            copy.name="Personalizar"; copy.SetActive(true);
            PersonalizeButton=copy.GetComponent<Button>();
            PersonalizeButton.onClick=new Button.ButtonClickedEvent();
            PersonalizeButton.interactable=true;
            Text label=copy.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text="Personalizar";
                if (label.font == null) label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            RectTransform source=StartButton.GetComponent<RectTransform>();
            RectTransform rect=copy.GetComponent<RectTransform>();
            rect.anchoredPosition=source.anchoredPosition+new Vector2(Mathf.Max(source.rect.width,260)+24,0);
            return PersonalizeButton;
        }
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
            if (PersonalizeButton != null) PersonalizeButton.interactable = state.Phase == Phase.Lobby;
            Message.text = count < 2 ? "Se necesitan al menos 2 jugadores." :
                state.Phase == Phase.Results ? "Esperando a que el anfitrión vuelva al lobby." :
                session.IsHost ? count + "/4 jugadores. Ya puedes iniciar." : "Esperando que el anfitrión inicie.";
        }
        public void StartGame() { Session.Ensure().StartRound(); }
        public void OpenPersonalize()
        {
            Session session = Session.Ensure();
            if (session.Connected && session.State != null && session.State.Phase == Phase.Lobby)
                session.Go("Personalizar");
        }
        public void Back() { Session.Ensure().LeaveToConnection(); }
    }
}
