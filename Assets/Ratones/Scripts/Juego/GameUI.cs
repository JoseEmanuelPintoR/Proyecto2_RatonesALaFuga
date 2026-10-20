using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class GameUI : MonoBehaviour
    {
        // Joystick de movimiento: el queso es la perilla.
        public Joystick Stick;
        public Camera GameCamera;
        public Text[] PlayerLabels;
        // Aura de cada jugador dentro de su cuadro en la barra de participantes (mismo orden que PlayerLabels).
        public Image[] PlayerAuras;
        public Sprite[] AuraSprites;
        public Text Timer, OwnScore, Inventory, Countdown, SpeedLabel, FreezeLabel;
        public Button SpeedButton, FreezeButton;
        public GameObject ExitDialog;
        public FreezeTargetPicker FreezePicker;
        float sendClock;
        bool dialogOpen;
        bool ChoosingFreeze { get { return FreezePicker != null && FreezePicker.gameObject.activeSelf; } }
        void Start() { FreezeTargetPicker.Ensure(this).Hide(); }
        void Update()
        {
            Session session = Session.Ensure(); State state = session.State;
            if (state == null) { CancelFreeze(); return; }
            Player own = state.Player(session.LocalId); if (own == null) { CancelFreeze(); return; }
            if (ChoosingFreeze) FreezePicker.Refresh(state, session.LocalId);
            for (int i = 0; i < PlayerLabels.Length; i++)
            {
                Player p = state.Players.FirstOrDefault(x => x.Slot == i);
                PlayerLabels[i].text = p == null ? "—" : p.Name + "\n" + p.Score + (p.Connected ? "" : " · salió");
                Image aura = PlayerAuras != null && i < PlayerAuras.Length ? PlayerAuras[i] : null;
                if (aura != null)
                {
                    aura.enabled = p != null && p.Connected;
                    if (aura.enabled && !Palette.SetAura(aura, AuraSprites, p.AuraColor)) aura.color = Palette.Colors[p.AuraColor];
                }
            }
            int seconds = Mathf.CeilToInt(state.TimeLeft);
            Timer.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            OwnScore.text = "Puntos: " + own.Score + "\nAlimentos: " + own.Collected;
            float frozen = own.FreezeLeft;
            Inventory.text = frozen > 0 ? "¡DETENIDO! " + frozen.ToString("0.0") + " s" : "";
            Countdown.text = state.Phase == Phase.Countdown ? Mathf.CeilToInt(state.CountdownLeft).ToString() : "";
            bool canAct = state.Phase == Phase.Playing && own.FreezeLeft <= 0 && !dialogOpen && !ChoosingFreeze;
            SpeedButton.interactable = canAct && own.Has(Power.Speed) && own.BoostLeft <= 0;
            FreezeButton.interactable = canAct && own.Has(Power.Freeze) && !session.FreezePending;
            SpeedLabel.text = own.BoostLeft > 0 ? "VELOCIDAD\n" + own.BoostLeft.ToString("0.0") + " s" : "VELOCIDAD\n" + (own.Has(Power.Speed) ? "Usar" : "Sin carga");
            FreezeLabel.text = "DETENER\n" + (session.FreezePending ? "Enviando…" : own.Has(Power.Freeze) ? "Elegir" : "Sin carga");
            Vector2 input = Vector2.zero;
            if (Application.isFocused && Input.GetKeyDown(KeyCode.Escape))
            {
                if (ChoosingFreeze) CancelFreeze();
                else if (dialogOpen) CancelExit();
                else ShowExit();
            }
            if (state.Phase == Phase.Playing && !dialogOpen && !ChoosingFreeze && Application.isFocused)
            {
                if (Stick != null) input += Stick.Value;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1;
                if (Input.GetKeyDown(KeyCode.E)) Speed(); if (Input.GetKeyDown(KeyCode.Q)) Freeze();
            }
            if (ChoosingFreeze || dialogOpen) input = Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1);
            Vector3 right = GameCamera.transform.right; right.y = 0; right.Normalize();
            Vector3 forward = GameCamera.transform.forward; forward.y = 0; forward.Normalize();
            Vector3 world = right * input.x + forward * input.y;
            float interval = 1f / Rules.TickRate;
            sendClock = Mathf.Min(sendClock + Time.unscaledDeltaTime, interval * 3);
            while (sendClock >= interval) { sendClock -= interval; session.Move(new Vector2(world.x, world.z)); }
        }
        public void Speed() { if (!dialogOpen && !ChoosingFreeze) Session.Ensure().UseSpeed(); }
        public void Freeze()
        {
            Session session = Session.Ensure();
            Player own = session.State != null ? session.State.Player(session.LocalId) : null;
            if (own == null || !own.Connected || session.State.Phase != Phase.Playing || !own.Has(Power.Freeze) ||
                own.FreezeLeft > 0 || session.FreezePending || dialogOpen || ChoosingFreeze) return;
            if (Stick != null) Stick.Clear(); session.Move(Vector2.zero);
            FreezeTargetPicker.Ensure(this).Open(session.State, session.LocalId);
        }
        public void ConfirmFreeze(int targetId)
        {
            if (!ChoosingFreeze) return;
            Session session = Session.Ensure();
            Player target = session.State == null ? null : session.State.Player(targetId);
            if (targetId == Rules.AllRivals || (target != null && target.Connected && target.Id != session.LocalId))
                session.UseFreeze(targetId);
            CancelFreeze();
        }
        public void CancelFreeze()
        {
            if (!ChoosingFreeze) return;
            FreezePicker.Hide(); if (Stick != null) Stick.Clear();
        }
        public void ShowExit()
        { CancelFreeze(); dialogOpen = true; if (Stick != null) Stick.Clear(); Session.Ensure().Move(Vector2.zero); ExitDialog.SetActive(true); }
        public void CancelExit() { dialogOpen = false; ExitDialog.SetActive(false); }
        public void ConfirmExit() { Session.Ensure().LeaveToMenu(); }
        void OnDisable() { CancelFreeze(); if (Session.Instance != null) Session.Instance.Move(Vector2.zero); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelFreeze(); }
    }
}
