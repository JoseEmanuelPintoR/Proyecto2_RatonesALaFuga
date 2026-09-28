using UnityEngine;
using UnityEngine.UI;

namespace Ratones.Basic
{
    public static class Palette
    {
        public static readonly Color[] Colors = {
            new Color(.8f,.05f,.03f), new Color(.15f,.2f,.87f), new Color(.1f,.49f,.14f),
            new Color(.9f,.25f,.68f), new Color(.94f,.46f,0), new Color(1f,.92f,.05f),
            new Color(.12f,.16f,.18f), new Color(.76f,.86f,1f), new Color(.43f,.13f,.78f),
            new Color(.45f,.26f,.11f), new Color(.32f,.94f,.9f), new Color(.31f,.94f,.06f)
        };
        // Pone la imagen del aura (UI/AURAS, mismo orden que Colors). Devuelve false si no hay imagen
        // para que quien llama tiña el círculo como antes.
        public static bool SetAura(Image image, Sprite[] sprites, int index)
        {
            if (image == null || sprites == null || index < 0 || index >= sprites.Length || sprites[index] == null) return false;
            image.sprite = sprites[index]; image.color = Color.white; image.preserveAspect = true;
            return true;
        }
    }
}
