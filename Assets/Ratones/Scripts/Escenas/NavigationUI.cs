using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class NavigationUI : MonoBehaviour
    {
        public InputField Code;
        public Text Status;
        public Text Name;
        public Button CreateButton, JoinButton;
        Session Session { get { return Ratones.Basic.Session.Ensure(); } }
        void Update()
        {
            if (Status != null) Status.text = Session.Status;
            if (Name != null) Name.text = "Nombre: " + Session.PlayerName;
            if (CreateButton != null) CreateButton.interactable = !Session.IsConnecting;
            if (JoinButton != null) JoinButton.interactable = !Session.IsConnecting;
        }
        public void Play() { Session.Go("Conexion"); }
        public void Customize() { Session.Go("Personalizar"); }
        public void Settings() { Session.Go("Configuracion"); }
        public void MainMenu() { Session.LeaveToMenu(); }
        public void Quit() { Session.Quit(); }
        public void Host() { Session.CreateRoom(); }
        public void Join() { if (Code != null) Session.JoinRoom(Code.text); }
    }
}
