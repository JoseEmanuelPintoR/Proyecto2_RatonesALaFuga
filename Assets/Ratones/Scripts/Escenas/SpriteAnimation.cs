using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    // Reproduce una secuencia de sprites en un Image (p. ej. el confeti de Resultados, que viene de un GIF).
    [RequireComponent(typeof(Image))]
    public sealed class SpriteAnimation : MonoBehaviour
    {
        public Sprite[] Frames;
        public float Fps = 20;
        public bool Loop;
        Image image;
        float time;
        void OnEnable()
        {
            image = GetComponent<Image>(); time = 0;
            if (Frames != null && Frames.Length > 0) image.sprite = Frames[0];
        }
        void Update()
        {
            if (Frames == null || Frames.Length == 0 || Fps <= 0) return;
            time += Time.unscaledDeltaTime;
            int frame = Mathf.FloorToInt(time * Fps);
            if (frame >= Frames.Length)
            {
                if (!Loop) { gameObject.SetActive(false); return; }
                frame %= Frames.Length;
            }
            image.sprite = Frames[frame];
        }
    }
}
