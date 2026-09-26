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
        // Desplegable de "Unirse": código de sala y botón Entrar.
        public GameObject JoinPanel;
        // Elementos que suben al abrir el desplegable. Su posición en la escena es la de "abierto";
        // cerrado quedan ClosedOffset más abajo, centrados sin el código.
        public RectTransform[] Slide;
        public float ClosedOffset = -65;
        Vector2[] openPositions;
        CanvasGroup panelGroup;
        bool joinOpen;
        float open;
        Session Session { get { return Ratones.Basic.Session.Ensure(); } }
        void Start()
        {
            if (Slide != null)
            {
                openPositions = new Vector2[Slide.Length];
                for (int i = 0; i < Slide.Length; i++) if (Slide[i] != null) openPositions[i] = Slide[i].anchoredPosition;
            }
            if (JoinPanel != null)
            {
                panelGroup = JoinPanel.GetComponent<CanvasGroup>();
                if (panelGroup == null) panelGroup = JoinPanel.AddComponent<CanvasGroup>();
                Animate(true);
            }
        }
        void Update()
        {
            if (Status != null) Status.text = Session.Status;
            if (Name != null) Name.text = "Nombre: " + Session.PlayerName;
            if (CreateButton != null) CreateButton.interactable = !Session.IsConnecting;
            if (JoinButton != null) JoinButton.interactable = !Session.IsConnecting;
            Animate(false);
        }
        void Animate(bool instant)
        {
            if (JoinPanel == null) return;
            float target = joinOpen ? 1 : 0;
            open = instant ? target : Mathf.MoveTowards(open, target, Time.unscaledDeltaTime * 5);
            float t = Mathf.SmoothStep(0, 1, open);
            if (openPositions != null)
                for (int i = 0; i < Slide.Length; i++)
                    if (Slide[i] != null) Slide[i].anchoredPosition = openPositions[i] + new Vector2(0, ClosedOffset * (1 - t));
            JoinPanel.SetActive(open > 0);
            panelGroup.alpha = t; panelGroup.interactable = panelGroup.blocksRaycasts = joinOpen;
        }
        public void Play() { Session.Go("Conexion"); }
        public void Customize() { Session.Go("Personalizar"); }
        public void Settings() { Session.Go("Configuracion"); }
        public void MainMenu() { Session.LeaveToMenu(); }
        public void Quit() { Session.Quit(); }
        public void Host() { Session.CreateRoom(); }
        public void ToggleJoin() { joinOpen = !joinOpen; }
        public void Join() { if (Code != null) Session.JoinRoom(Code.text); }
    }
}
