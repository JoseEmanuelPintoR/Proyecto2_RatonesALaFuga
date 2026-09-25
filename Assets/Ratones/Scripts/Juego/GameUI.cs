using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class GameUI : MonoBehaviour
    {
        public DirectionButton[] Directions;
        public Camera GameCamera;
        public Text[] PlayerLabels;
        public Text Timer, OwnScore, Inventory, Countdown, SpeedLabel, FreezeLabel;
        public Button SpeedButton, FreezeButton;
        public GameObject ExitDialog;
        float sendClock;
        bool dialogOpen;
        void Update()
        {
            Session session = Session.Ensure(); State state = session.State;
            if (state == null) return;
            Player own = state.Player(session.LocalId); if (own == null) return;
            for (int i = 0; i < PlayerLabels.Length; i++)
            {
                Player p = state.Players.FirstOrDefault(x => x.Slot == i);
                PlayerLabels[i].text = p == null ? "—" : p.Name + "\n" + p.Score + (p.Connected ? "" : " · salió");
            }
            int seconds = Mathf.CeilToInt(state.TimeLeft);
            Timer.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            OwnScore.text = "Puntos: " + own.Score + "\nAlimentos: " + own.Collected;
            Inventory.text = own.FreezeLeft > 0 ? "Detenido: " + own.FreezeLeft.ToString("0.0") + " s" :
                own.Inventory == Power.None ? "Sin poderes guardados" : own.Inventory == Power.Both ? "Chocolate y trampa guardados" : own.Has(Power.Speed) ? "Chocolate guardado" : "Trampa guardada";
            Countdown.text = state.Phase == Phase.Countdown ? Mathf.CeilToInt(state.CountdownLeft).ToString() : "";
            bool canAct = state.Phase == Phase.Playing && own.FreezeLeft <= 0 && !dialogOpen;
            SpeedButton.interactable = canAct && own.Has(Power.Speed) && own.BoostLeft <= 0;
            FreezeButton.interactable = canAct && own.Has(Power.Freeze);
            SpeedLabel.text = own.BoostLeft > 0 ? "VELOCIDAD\n" + own.BoostLeft.ToString("0.0") + " s" : "VELOCIDAD\n" + (own.Has(Power.Speed) ? "Usar" : "Sin carga");
            FreezeLabel.text = "DETENER\n" + (own.Has(Power.Freeze) ? "Usar" : "Sin carga");
            Vector2 input = Vector2.zero;
            if (state.Phase == Phase.Playing && !dialogOpen && Application.isFocused)
            {
                foreach (DirectionButton direction in Directions) if (direction.Held) input += direction.Direction;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1;
                if (Input.GetKeyDown(KeyCode.E)) session.UseSpeed(); if (Input.GetKeyDown(KeyCode.Q)) session.UseFreeze();
                if (Input.GetKeyDown(KeyCode.Escape)) ShowExit();
            }
            Vector3 right = GameCamera.transform.right; right.y = 0; right.Normalize();
            Vector3 forward = GameCamera.transform.forward; forward.y = 0; forward.Normalize();
            Vector3 world = right * input.x + forward * input.y;
            float interval = 1f / Rules.TickRate;
            sendClock = Mathf.Min(sendClock + Time.unscaledDeltaTime, interval * 3);
            while (sendClock >= interval) { sendClock -= interval; session.Move(new Vector2(world.x, world.z)); }
        }
        public void Speed() { Session.Ensure().UseSpeed(); }
        public void Freeze() { Session.Ensure().UseFreeze(); }
        public void ShowExit()
        { dialogOpen = true; foreach (DirectionButton d in Directions) d.Clear(); Session.Ensure().Move(Vector2.zero); ExitDialog.SetActive(true); }
        public void CancelExit() { dialogOpen = false; ExitDialog.SetActive(false); }
        public void ConfirmExit() { Session.Ensure().LeaveToMenu(); }
        void OnDisable() { if (Session.Instance != null) Session.Instance.Move(Vector2.zero); }
    }
}
