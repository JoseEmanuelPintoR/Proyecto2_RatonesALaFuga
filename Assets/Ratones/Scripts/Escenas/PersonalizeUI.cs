using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class PersonalizeUI : MonoBehaviour
    {
        public InputField Name;
        public Image KeyPreview, AuraPreview;
        public Text Selected;
        public MousePreview Preview;
        public Sprite[] AuraSprites;
        readonly Button[] colorButtons = new Button[Rules.ColorCount];
        // Aspecto original de los botones de color (círculo teñido) para el modo LLAVE.
        readonly Sprite[] colorSprites = new Sprite[Rules.ColorCount];
        readonly Color[] colorTints = new Color[Rules.ColorCount];
        Sprite auraCircle;
        int key, aura;
        bool editingAura;
        string warning = "";
        void Start()
        {
            Session session = Session.Ensure(); Name.text = session.PlayerName;
            key = session.KeyColor; aura = session.AuraColor;
            // En una sala se usan los colores del servidor: pudo cambiar la llave si estaba repetida.
            Player own = session.State != null ? session.State.Player(session.LocalId) : null;
            if (session.Connected && own != null) { key = own.KeyColor; aura = own.AuraColor; }
            Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            if (canvas != null)
                foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
                {
                    int index;
                    if (button.name.StartsWith("Color") && int.TryParse(button.name.Substring(5), out index) && index >= 0 && index < colorButtons.Length)
                    {
                        colorButtons[index] = button;
                        if (button.image != null) { colorSprites[index] = button.image.sprite; colorTints[index] = button.image.color; }
                    }
                }
            auraCircle = AuraPreview.sprite;
            Refresh();
        }
        void Update() { RefreshTaken(); }
        public void SelectKey() { editingAura = false; warning = ""; Refresh(); }
        public void SelectAura() { editingAura = true; warning = ""; Refresh(); }
        public void ChooseColor(int index)
        {
            if (editingAura) { if (Owner(key, index) != null) return; aura = Simulation.ColorIndex(index); }
            else { if (Owner(index, aura) != null) return; key = Simulation.ColorIndex(index); }
            warning = ""; Refresh();
        }
        void Refresh()
        {
            KeyPreview.color = Palette.Colors[key];
            if (!Palette.SetAura(AuraPreview, AuraSprites, aura)) { AuraPreview.sprite = auraCircle; AuraPreview.color = Palette.Colors[aura]; }
            // En modo AURA los botones muestran la imagen de cada aura; en modo LLAVE, el círculo de color.
            for (int i = 0; i < colorButtons.Length; i++)
            {
                Image image = colorButtons[i] != null ? colorButtons[i].image : null;
                if (image == null) continue;
                if (!editingAura || !Palette.SetAura(image, AuraSprites, i)) { image.sprite = colorSprites[i]; image.color = colorTints[i]; }
            }
            Selected.text = warning != "" ? warning : "Color para: " + (editingAura ? "AURA" : "LLAVE");
            if (Preview != null) Preview.Show(key, aura);
            RefreshTaken();
        }
        // Desactiva los colores que harían la misma llave y aura que otro jugador de la sala.
        void RefreshTaken()
        {
            for (int i = 0; i < colorButtons.Length; i++)
                if (colorButtons[i] != null) colorButtons[i].interactable = (editingAura ? Owner(key, i) : Owner(i, aura)) == null;
        }
        Player Owner(int keyColor, int auraColor)
        {
            Session session = Session.Ensure();
            if (!session.Connected || session.State == null) return null;
            return session.State.Players.FirstOrDefault(p => p.Connected && p.Id != session.LocalId && p.KeyColor == keyColor && p.AuraColor == auraColor);
        }
        public void Save()
        {
            Player owner = Owner(key, aura);
            if (owner != null) { warning = "Esa llave y aura ya las usa " + owner.Name; Refresh(); return; }
            Session.Ensure().SaveProfile(Name.text, key, aura); Return();
        }
        public void Back() { Return(); }
        void Return()
        {
            Session session = Session.Ensure();
            if (!session.Connected || session.State == null) { session.Go("MenuInicio"); return; }
            // La sesión sigue activa durante la personalización.
            if (session.State.Phase == Phase.Lobby) session.Go("Lobby");
            else if (session.State.Phase == Phase.Results) session.Go("Resultados");
            else session.Go("Juego");
        }
    }
}
