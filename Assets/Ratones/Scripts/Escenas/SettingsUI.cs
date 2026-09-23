using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public sealed class SettingsUI : MonoBehaviour
    {
        public Slider Music, Effects;
        public Text MusicValue, EffectsValue;
        void Start()
        {
            Session.Ensure(); Music.SetValueWithoutNotify(PlayerPrefs.GetFloat("RF2.Music", .5f));
            Effects.SetValueWithoutNotify(PlayerPrefs.GetFloat("RF2.Effects", .7f)); Refresh();
        }
        public void ChangeMusic(float value) { PlayerPrefs.SetFloat("RF2.Music", value); Session.Ensure().ApplyVolume(); Refresh(); }
        public void ChangeEffects(float value) { PlayerPrefs.SetFloat("RF2.Effects", value); Session.Ensure().ApplyVolume(); Refresh(); }
        void Refresh() { MusicValue.text = Mathf.RoundToInt(Music.value * 100) + "%"; EffectsValue.text = Mathf.RoundToInt(Effects.value * 100) + "%"; }
        public void Back() { PlayerPrefs.Save(); Session.Ensure().Go("MenuInicio"); }
    }
}
