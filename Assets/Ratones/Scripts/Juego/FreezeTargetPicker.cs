using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    // Usa las fuentes y sprites del diálogo de salida actual. También se puede
    // crear en edición con Ratones/Preparar selector de trampa y acomodar a mano.
    public sealed class FreezeTargetPicker : MonoBehaviour
    {
        public GameUI Owner;
        public RectTransform Panel;
        public Text Title, Hint;
        public Button[] RivalButtons;
        public Text[] RivalLabels;
        public Button AllButton, CancelButton;
        readonly int[] targets = new int[Rules.MaxPlayers - 1];
        bool bound;
        int openedRound;

        public void Open(State state, int localId)
        {
            Bind(); openedRound = state.Round;
            Player[] rivals = state.Players.Where(p => p.Connected && p.Id != localId).OrderBy(p => p.Slot).ToArray();
            for (int i = 0; i < targets.Length; i++) targets[i] = i < rivals.Length ? rivals[i].Id : -1;
            transform.SetAsLastSibling(); gameObject.SetActive(true);
            Refresh(state, localId);
        }
        public void Hide() { gameObject.SetActive(false); }
        public void Refresh(State state, int localId)
        {
            if (!gameObject.activeSelf) return;
            Player own = state == null ? null : state.Player(localId);
            if (own == null || !own.Connected || state.Round != openedRound || state.Phase != Phase.Playing ||
                own.FreezeLeft > 0 || !own.Has(Power.Freeze) || Session.Ensure().FreezePending)
            { Hide(); return; }
            int connected = state.Players.Count(p => p.Connected && p.Id != localId);
            Hint.text = connected > 0 ? "Elige a quién detener.\nLa partida continúa mientras eliges." : "No hay rivales conectados.";
            // Los botones conservan sus IDs mientras el panel está abierto:
            // si alguien sale no se mueve otro rival debajo del dedo del usuario.
            for (int i = 0; i < targets.Length; i++)
            {
                Player rival = state.Player(targets[i]);
                RivalButtons[i].gameObject.SetActive(targets[i] >= 0);
                RivalButtons[i].interactable = rival != null && rival.Connected;
                RivalLabels[i].text = rival == null ? "Salió de la partida" :
                    "J" + (rival.Slot + 1) + " · " + rival.Name + (rival.Connected ? " — 5 s" : " · salió");
            }
            AllButton.interactable = connected > 0;
        }
        void Bind()
        {
            if (bound) return;
            bound = true;
            for (int i = 0; i < RivalButtons.Length; i++)
            {
                int index = i;
                RivalButtons[i].onClick.AddListener(() => Owner.ConfirmFreeze(targets[index]));
                RivalButtons[i].onClick.AddListener(Session.Ensure().PlayClick);
            }
            AllButton.onClick.AddListener(() => Owner.ConfirmFreeze(Rules.AllRivals));
            CancelButton.onClick.AddListener(Owner.CancelFreeze);
            AllButton.onClick.AddListener(Session.Ensure().PlayClick);
            CancelButton.onClick.AddListener(Session.Ensure().PlayClick);
        }

        public static FreezeTargetPicker Ensure(GameUI game)
        {
            if (game.FreezePicker != null) return game.FreezePicker;
            Transform parent = game.ExitDialog != null ? game.ExitDialog.transform.parent :
                game.FreezeButton.GetComponentInParent<Canvas>().transform;
            var existing = parent.GetComponentInChildren<FreezeTargetPicker>(true);
            if (existing != null) { existing.Owner = game; game.FreezePicker = existing; return existing; }

            RectTransform root = Rect("SelectorTrampa", parent);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.SetActive(false);
            Image shade = root.gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .55f);
            var picker = root.gameObject.AddComponent<FreezeTargetPicker>();
            picker.Owner = game; game.FreezePicker = picker;

            Font font = game.Timer != null ? game.Timer.font : null;
            if (font == null && game.FreezeLabel != null) font = game.FreezeLabel.font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Transform oldPanel = game.ExitDialog != null ? game.ExitDialog.transform.Find("Panel") : null;
            Image panelStyle = oldPanel != null ? oldPanel.GetComponent<Image>() : null;
            Button buttonStyle = game.ExitDialog != null ? game.ExitDialog.GetComponentInChildren<Button>(true) : null;

            picker.Panel = Rect("Panel", root);
            picker.Panel.anchorMin = picker.Panel.anchorMax = new Vector2(.5f, .5f);
            picker.Panel.sizeDelta = new Vector2(600, 568);
            Image background = picker.Panel.gameObject.AddComponent<Image>();
            CopyImage(background, panelStyle, new Color(.87f, .79f, .61f));
            picker.Title = Label("Titulo", picker.Panel, font, "DETENER", 28, -60, 340, 50);
            picker.Title.color = new Color(1, .95f, .85f);
            Outline titleOutline = picker.Title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(.24f, .12f, .05f); titleOutline.effectDistance = new Vector2(2, -2);
            picker.Hint = Label("Indicacion", picker.Panel, font, "Elige a quién detener.", 20, -125, 500, 52);
            picker.RivalButtons = new Button[Rules.MaxPlayers - 1];
            picker.RivalLabels = new Text[Rules.MaxPlayers - 1];
            for (int i = 0; i < picker.RivalButtons.Length; i++)
            {
                picker.RivalButtons[i] = MakeButton("Rival" + (i + 1), picker.Panel, font, buttonStyle,
                    "Rival " + (i + 1) + " — 5 s", -195 - i * 70, out picker.RivalLabels[i]);
            }
            Text unused;
            picker.AllButton = MakeButton("Todos", picker.Panel, font, buttonStyle, "Todos los rivales — 2 s", -411, out unused);
            picker.CancelButton = MakeButton("Cancelar", picker.Panel, font, buttonStyle, "Cancelar", -487, out unused);
            return picker;
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }
        static Text Label(string name, Transform parent, Font font, string value, int size, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent); rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(width, height);
            var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = new Color(.24f, .12f, .05f); text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false; text.supportRichText = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        static Button MakeButton(string name, Transform parent, Font font, Button style, string value, float y, out Text label)
        {
            RectTransform rect = Rect(name, parent); rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(484, 60);
            Image image = rect.gameObject.AddComponent<Image>(); CopyImage(image, style != null ? style.image : null, new Color(1, .78f, .25f));
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            if (style != null) button.colors = style.colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            label = Label("Texto", rect, font, value, 24, 0, 456, 54);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            return button;
        }
        static void CopyImage(Image image, Image style, Color fallback)
        {
            if (style != null && style.sprite != null)
            {
                image.sprite = style.sprite; image.type = style.type; image.color = style.color;
                image.pixelsPerUnitMultiplier = style.pixelsPerUnitMultiplier;
            }
            else image.color = fallback;
            image.preserveAspect = false; image.raycastTarget = true;
        }
    }
}
