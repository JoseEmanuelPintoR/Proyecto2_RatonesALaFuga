using UnityEngine;

namespace Ratones.Gameplay
{
    public sealed class GameAudio : MonoBehaviour
    {
        AudioSource source, music;
        AudioClip cheese, boost, sticky, tick, victory, defeat;
        public void Build()
        {
            source = gameObject.AddComponent<AudioSource>(); source.spatialBlend = 0; source.volume = .38f;
            music = gameObject.AddComponent<AudioSource>(); music.spatialBlend = 0; music.volume = .12f;
            cheese = Tone("Queso", new float[] { 880, 1175, 1568 }, .065f);
            boost = Tone("Azúcar", new float[] { 392, 523, 659, 784, 1047 }, .055f);
            sticky = Tone("Pegamento", new float[] { 350, 270, 190, 120 }, .07f);
            tick = Tone("Reloj", new float[] { 740 }, .055f);
            victory = Tone("Victoria", new float[] { 523, 659, 784, 1047, 784, 1047 }, .15f);
            defeat = Tone("Final", new float[] { 392, 330, 262 }, .2f);
            music.clip = Tone("Cocina nocturna", new float[] { 196, 0, 294, 0, 330, 294, 247, 0, 220, 0, 330, 0, 392, 330, 294, 0 }, .24f);
            music.loop = true; music.Play();
        }
        static AudioClip Tone(string label, float[] notes, float step)
        {
            const int rate = 22050;
            float[] samples = new float[Mathf.CeilToInt(notes.Length * step * rate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                int note = Mathf.Min(notes.Length - 1, (int)(t / step));
                float local = t - note * step;
                float envelope = Mathf.Min(1, local * 100) * Mathf.Pow(Mathf.Max(0, 1 - local / step), 1.8f);
                samples[i] = notes[note] <= 0 ? 0 : Mathf.Sin(t * notes[note] * Mathf.PI * 2) * envelope * .28f;
            }
            AudioClip clip = AudioClip.Create(label, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        public void SetEnabled(bool on) { source.mute = !on; music.mute = !on; }
        public void Cheese() { source.PlayOneShot(cheese); }
        public void Boost() { source.PlayOneShot(boost); }
        public void Sticky() { source.PlayOneShot(sticky); }
        public void Tick() { source.PlayOneShot(tick); }
        public void Finish(bool won) { source.PlayOneShot(won ? victory : defeat); }
    }
}
