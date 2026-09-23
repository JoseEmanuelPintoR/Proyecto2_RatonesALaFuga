using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class PersonalizeUI : MonoBehaviour
    {
        public InputField Name;
        public Image KeyPreview, AuraPreview;
        public Text Selected;
        int key, aura;
        bool editingAura;
        void Start()
        {
            Session session = Session.Ensure(); Name.text = session.PlayerName;
            key = session.KeyColor; aura = session.AuraColor; Refresh();
        }
        public void SelectKey() { editingAura = false; Refresh(); }
        public void SelectAura() { editingAura = true; Refresh(); }
        public void ChooseColor(int index)
        { if (editingAura) aura = Simulation.ColorIndex(index); else key = Simulation.ColorIndex(index); Refresh(); }
        void Refresh()
        { KeyPreview.color = Palette.Colors[key]; AuraPreview.color = Palette.Colors[aura]; Selected.text = "Color para: " + (editingAura ? "AURA" : "LLAVE"); }
        public void Save() { Session.Ensure().SaveProfile(Name.text, key, aura); Session.Ensure().Go("MenuInicio"); }
        public void Back() { Session.Ensure().Go("MenuInicio"); }
    }
}
