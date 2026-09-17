using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Ratones.Core;

namespace Ratones.Gameplay
{
    public sealed class GameHud : MonoBehaviour
    {
        readonly Color ink = new Color(.04f, .09f, .13f, .96f);
        readonly Color panel = new Color(.08f, .16f, .20f, .96f);
        readonly Color gold = new Color(1f, .75f, .2f);
        readonly Color muted = new Color(.70f, .81f, .83f);
        Font font;
        Sprite circle;
        RectTransform safe, screen;
        RatonesGame game;
        int page = -1;
        Text score, clock, ranking, remaining, notice, lobbyList, lobbyInfo, big, resultTitle, resultRows, status;
        Button start, boost, sticky, join, host, soundOption, shadowsOption;
        Text boostText, stickyText;
        InputField nameInput, ipInput, portInput;
        GameObject options;
        public VirtualJoystick Joystick { get; private set; }
        readonly Dictionary<int, Text> nameTags = new Dictionary<int, Text>();
        Vector2 lastScreen;
        Rect lastSafe;

        public void Build(RatonesGame app)
        {
            game = app;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var events = new GameObject("Controles de interfaz", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform);
            safe = NewRect("Área segura", transform); Stretch(safe);
            circle = MakeCircle();
            RefreshSafeArea();
        }

        void RefreshSafeArea()
        {
            if (Screen.width == 0 || Screen.height == 0) return;
            Rect area = Screen.safeArea;
            if (area.width <= 0 || area.height <= 0) area = new Rect(0, 0, Screen.width, Screen.height);
            if (lastScreen == new Vector2(Screen.width, Screen.height) && lastSafe == area) return;
            lastScreen = new Vector2(Screen.width, Screen.height); lastSafe = area;
            safe.anchorMin = new Vector2(area.x / Screen.width, area.y / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
        static void Stretch(RectTransform t)
        { t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero; }
        static void Place(RectTransform t, float x, float y, float w, float h, Vector2 anchor)
        { t.anchorMin = t.anchorMax = anchor; t.pivot = new Vector2(.5f, .5f); t.anchoredPosition = new Vector2(x, y); t.sizeDelta = new Vector2(w, h); }
        Image Card(string name, Transform parent, float x, float y, float w, float h, Color color, Vector2 anchor)
        {
            RectTransform rt = NewRect(name, parent); Place(rt, x, y, w, h, anchor);
            Image im = rt.gameObject.AddComponent<Image>(); im.color = color; return im;
        }
        Text Label(string value, Transform parent, float x, float y, float w, float h, int size,
            Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false, Vector2? anchor = null)
        {
            RectTransform rt = NewRect("Texto", parent); Place(rt, x, y, w, h, anchor ?? new Vector2(.5f, .5f));
            Text t = rt.gameObject.AddComponent<Text>(); t.font = font; t.text = value; t.fontSize = size;
            t.color = color; t.alignment = alignment; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.raycastTarget = false; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
        Button Button(string value, Transform parent, float x, float y, float w, float h, Color color,
            Action action, Vector2? anchor = null)
        {
            var card = Card(value, parent, x, y, w, h, color, anchor ?? new Vector2(.5f, .5f));
            var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f); colors.pressedColor = new Color(.75f, .82f, .85f);
            colors.disabledColor = new Color(.43f, .48f, .51f, .8f); button.colors = colors;
            Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            Label(value, card.transform, 0, 0, w - 18, h - 4, 23, color == gold ? ink : Color.white, TextAnchor.MiddleCenter, true);
            button.onClick.AddListener(() => action()); return button;
        }
        InputField Field(Transform parent, string value, float x, float y, float w, InputField.ContentType type)
        {
            Image card = Card("Entrada", parent, x, y, w, 46, new Color(.13f, .24f, .28f), new Vector2(.5f, .5f));
            var field = card.gameObject.AddComponent<InputField>();
            Text text = Label(value, card.transform, 0, 0, w - 24, 44, 23, Color.white);
            text.supportRichText = false; field.textComponent = text;
            field.contentType = type; field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = type == InputField.ContentType.IntegerNumber ? 5 : 64;
            field.text = value;
            return field;
        }
        RectTransform Modal(float width, float height)
        {
            Card("Velo", screen, 0, 0, 4000, 2000, new Color(.02f, .045f, .07f, .42f), new Vector2(.5f, .5f));
            return Card("Panel", screen, 0, 0, width, height, ink, new Vector2(.5f, .5f)).rectTransform;
        }
        public void RefreshPage() { page = -1; }
        void NewPage(int value)
        {
            if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
            screen = NewRect("Pantalla", safe); Stretch(screen); page = value; Joystick = null;
            nameTags.Clear();
            if (value == 0) Menu(); else if (value == 1) Lobby(); else if (value == 2) Gameplay(); else Results();
        }
        void Menu()
        {
            RectTransform card = Modal(630, 650);
            Label("RATONES", card, 0, 253, 570, 64, 56, Color.white, TextAnchor.MiddleCenter, true);
            Label("A LA FUGA", card, 0, 198, 570, 62, 56, gold, TextAnchor.MiddleCenter, true);
            Label("2–4 ratones  ·  50 quesos  ·  2 minutos", card, 0, 143, 560, 34, 22, muted, TextAnchor.MiddleCenter);
            Label("TU NOMBRE", card, 0, 100, 530, 26, 17, muted, bold:true);
            nameInput = Field(card, PlayerPrefs.GetString("Ratones.Name", "José"), 0, 61, 530, InputField.ContentType.Standard);
            nameInput.characterLimit = 18;
            Label("IP DEL ANFITRIÓN", card, -67, 17, 396, 28, 17, muted, bold:true);
            Label("PUERTO", card, 211, 17, 108, 28, 17, muted, bold:true);
            ipInput = Field(card, PlayerPrefs.GetString("Ratones.IP", "127.0.0.1"), -67, -21, 396, InputField.ContentType.Standard);
            portInput = Field(card, GameRules.Port.ToString(), 211, -21, 108, InputField.ContentType.IntegerNumber);
            host = Button("Crear sala", card, -137, -92, 256, 57, gold, () => game.CreateRoom(nameInput.text, portInput.text));
            join = Button("Unirme", card, 137, -92, 256, 57, new Color(.13f, .39f, .45f), () => game.JoinRoom(nameInput.text, ipInput.text, portInput.text));
            status = Label("Conecta los dispositivos a la misma red Wi-Fi.", card, 0, -160, 535, 66, 21, muted, TextAnchor.MiddleCenter);
            Button("Opciones", card, -185, -230, 165, 42, panel, () => options.SetActive(!options.activeSelf));
            Button("Cancelar", card, 0, -230, 165, 42, panel, () => game.LeaveRoom());
            Button("Salir", card, 185, -230, 165, 42, panel, () => game.Quit());
            Label("PC: WASD / flechas · E: azúcar · Q: bloqueo", card, 0, -288, 555, 34, 19, muted, TextAnchor.MiddleCenter);
            options = Card("Opciones", screen, 0, 0, 540, 335, panel, new Vector2(.5f, .5f)).gameObject;
            Label("OPCIONES", options.transform, 0, 110, 450, 50, 34, gold, TextAnchor.MiddleCenter, true);
            soundOption = Button("Sonido", options.transform, 0, 35, 440, 55, ink, () => game.ToggleSound());
            shadowsOption = Button("Sombras", options.transform, 0, -35, 440, 55, ink, () => game.ToggleShadows());
            Button("Listo", options.transform, 0, -113, 440, 50, gold, () => options.SetActive(false));
            options.SetActive(false);
        }
        void Lobby()
        {
            RectTransform card = Modal(680, 600);
            Label("LA BANDA DEL QUESO", card, 0, 229, 615, 62, 38, gold, TextAnchor.MiddleCenter, true);
            lobbyInfo = Label("", card, 0, 159, 615, 74, 23, muted, TextAnchor.MiddleCenter);
            lobbyList = Label("", card, 0, 18, 535, 220, 27, Color.white);
            start = Button("Iniciar partida", card, 0, -151, 550, 60, gold, () => game.StartRound());
            Label("Recoge caramelos azules y frascos violetas para cargar tus poderes.", card, 0, -212, 550, 56, 20, muted, TextAnchor.MiddleCenter);
            Button("Volver al menú", card, 0, -265, 300, 36, panel, () => game.LeaveRoom());
        }
        void Gameplay()
        {
            var topLeft = new Vector2(0, 1); var top = new Vector2(.5f, 1); var topRight = new Vector2(1, 1);
            var left = Card("Puntaje", screen, 167, -63, 294, 94, ink, topLeft);
            score = Label("", left.transform, 0, 10, 255, 43, 29, gold, bold:true);
            remaining = Label("", left.transform, 0, -24, 255, 27, 18, muted);
            var timer = Card("Reloj", screen, 0, -57, 184, 83, ink, top);
            clock = Label("02:00", timer.transform, 0, 6, 160, 54, 40, Color.white, TextAnchor.MiddleCenter, true);
            Label("TIEMPO", timer.transform, 0, -24, 160, 22, 15, muted, TextAnchor.MiddleCenter);
            var ranks = Card("Clasificación", screen, -166, -92, 292, 150, ink, topRight);
            ranking = Label("", ranks.transform, 0, 0, 260, 135, 20, Color.white);
            notice = Label("", screen, 0, -155, 580, 64, 24, gold, TextAnchor.MiddleCenter, true, top);
            big = Label("", screen, 0, 30, 900, 200, 96, gold, TextAnchor.MiddleCenter, true);
            var baseImage = Card("Joystick", screen, 136, 126, 174, 174, new Color(.055f, .13f, .18f, .72f), new Vector2(0, 0));
            baseImage.sprite = circle;
            var handle = Card("Palanca", baseImage.transform, 0, 0, 72, 72, new Color(.76f, .85f, .86f, .9f), new Vector2(.5f, .5f));
            handle.sprite = circle; handle.raycastTarget = false;
            Joystick = baseImage.gameObject.AddComponent<VirtualJoystick>(); Joystick.Handle = handle.rectTransform;
            Label("MOVER", screen, 136, 27, 160, 27, 17, muted, TextAnchor.MiddleCenter, false, new Vector2(0, 0));
            boost = Button("AZÚCAR\nE", screen, -260, 102, 170, 92, new Color(.06f, .53f, .65f), () => game.RequestBoost(), new Vector2(1, 0));
            sticky = Button("BLOQUEO\nQ", screen, -77, 102, 142, 92, new Color(.44f, .20f, .64f), () => game.RequestSticky(), new Vector2(1, 0));
            boostText = boost.GetComponentInChildren<Text>(); stickyText = sticky.GetComponentInChildren<Text>();
            Button("Menú", screen, -74, 28, 120, 38, ink, () => game.ToggleLeaveDialog(), new Vector2(1, 0));
        }
        void Results()
        {
            RectTransform card = Modal(740, 610);
            Label("BOTÍN FINAL", card, 0, 239, 650, 56, 44, gold, TextAnchor.MiddleCenter, true);
            resultTitle = Label("", card, 0, 166, 645, 76, 28, Color.white, TextAnchor.MiddleCenter, true);
            resultRows = Label("", card, 0, 8, 605, 217, 27, Color.white);
            Label("Si hay empate, gana quien alcanzó primero su puntaje.", card, 0, -142, 630, 48, 19, muted, TextAnchor.MiddleCenter);
            start = Button("Volver a la sala", card, 0, -208, 610, 57, gold, () => game.ReturnLobby());
            Button("Menú principal", card, 0, -270, 320, 39, panel, () => game.LeaveRoom());
        }

        public void Draw(MatchState state, int ownId, string message)
        {
            RefreshSafeArea();
            int target = state == null ? 0 : state.Phase == MatchPhase.Lobby ? 1 : state.Phase == MatchPhase.Results ? 3 : 2;
            if (page != target) NewPage(target);
            if (page == 0)
            {
                if (!string.IsNullOrEmpty(message)) status.text = message;
                host.interactable = join.interactable = !game.Connecting;
                soundOption.GetComponentInChildren<Text>().text = "Sonido: " + (game.SoundEnabled ? "activado" : "desactivado");
                shadowsOption.GetComponentInChildren<Text>().text = "Sombras: " + (game.ShadowsEnabled ? "activadas" : "desactivadas");
                return;
            }
            var ordered = state.Players.OrderByDescending(p => p.Score).ThenBy(p => p.LastScoreAt).ToList();
            PlayerState own = state.Player(ownId);
            if (page == 1)
            {
                int count = state.Players.Count(p => p.Connected);
                lobbyInfo.text = game.IsHost ? "Comparte esta IP: " + game.RoomAddress + "\nPuerto: " + game.RoomPort : "Conectado a " + game.RoomAddress + ":" + game.RoomPort;
                lobbyList.text = string.Join("\n\n", state.Players.Select((p, i) => (i + 1) + ". " + p.Name + (p.Id == ownId ? "  (tú)" : "") + (p.Id == state.HostId ? "  · anfitrión" : "")));
                start.interactable = game.IsHost && count >= 2;
                start.GetComponentInChildren<Text>().text = count < 2 ? "Esperando otro ratón… " + count + "/4" : game.IsHost ? "Iniciar partida · " + count + "/4" : "Esperando al anfitrión · " + count + "/4";
                return;
            }
            if (page == 3)
            {
                resultTitle.text = state.ResultMessage;
                resultRows.text = string.Join("\n\n", ordered.Select((p, i) => (i + 1) + ". " + p.Name + (p.Id == ownId ? " (tú)" : "") + "   ·   " + p.Score + " quesos" + (!p.Connected ? " · salió" : "")));
                start.interactable = game.IsHost;
                start.GetComponentInChildren<Text>().text = game.IsHost ? "Volver a la sala" : "Esperando al anfitrión";
                return;
            }
            int seconds = Mathf.CeilToInt(state.TimeLeft);
            clock.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            clock.color = seconds <= 10 ? new Color(1f, .33f, .27f) : Color.white;
            score.text = (own == null ? "0" : own.Score.ToString()) + "  QUESOS";
            remaining.text = state.Items.Count(i => i.Kind == ItemKind.Cheese && i.Active) + " por recoger en la cocina";
            ranking.text = string.Join("\n", ordered.Select((p, i) => (i + 1) + ". " + p.Name + (p.Id == ownId ? " (tú)" : "") + "  ·  " + p.Score));
            big.text = state.Phase == MatchPhase.Countdown ? Mathf.CeilToInt(state.CountdownLeft).ToString() : "";
            notice.text = own == null ? "" : own.StickyLeft > 0 ? "¡PEGADO! " + own.StickyLeft.ToString("0.0") + " s" : own.NoticeLeft > 0 ? own.Notice : "";
            bool active = state.Phase == MatchPhase.Playing && own != null && own.StickyLeft <= 0;
            boost.interactable = active && own.BoostCharges > 0 && own.BoostLeft <= 0;
            sticky.interactable = active && own.StickyCharges > 0;
            boostText.text = own != null && own.BoostLeft > 0 ? "AZÚCAR\n" + own.BoostLeft.ToString("0.0") + " s" : "AZÚCAR [E]\n" + (own != null && own.BoostCharges > 0 ? "LISTO" : "SIN CARGA");
            stickyText.text = "BLOQUEO [Q]\n" + (own != null && own.StickyCharges > 0 ? "LISTO" : "SIN CARGA");
            foreach (PlayerState p in state.Players)
            {
                Text tag;
                if (!nameTags.TryGetValue(p.Id, out tag))
                {
                    tag = Label(p.Name, screen, 0, 0, 230, 34, 18, KitchenView.PlayerColors[p.ColorIndex], TextAnchor.MiddleCenter, true);
                    tag.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .9f);
                    nameTags[p.Id] = tag;
                }
                Vector3 point = game.World.ScreenPoint(p.Position);
                tag.gameObject.SetActive(p.Connected && point.z > 0);
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(screen, point, null, out local);
                tag.rectTransform.anchoredPosition = local;
            }
        }

        public void LeaveDialog()
        {
            var overlay = Card("Confirmar salida", screen, 0, 0, 4000, 2000, new Color(0, 0, 0, .55f), new Vector2(.5f, .5f));
            var card = Card("Salir", overlay.transform, 0, 0, 580, 290, ink, new Vector2(.5f, .5f));
            Label(game.IsHost ? "¿Cerrar la sala para todos?" : "¿Abandonar la partida?", card.transform, 0, 73, 520, 64, 28, Color.white, TextAnchor.MiddleCenter, true);
            Label("La partida continúa mientras decides.", card.transform, 0, 5, 520, 46, 20, muted, TextAnchor.MiddleCenter);
            Button("Seguir jugando", card.transform, -137, -84, 250, 55, gold, () => { game.ResumeInput(); Destroy(overlay.gameObject); });
            Button("Salir", card.transform, 137, -84, 250, 55, panel, () => game.LeaveRoom());
        }
        static Sprite MakeCircle()
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f));
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(63.5f - distance)));
            }
            texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
        }
        void OnDestroy() { if (circle != null) { Destroy(circle.texture); Destroy(circle); } }
    }
}
