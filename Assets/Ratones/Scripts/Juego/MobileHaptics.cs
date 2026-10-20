using UnityEngine;

namespace Ratones.Basic
{
    public static class MobileHaptics
    {
        public static void Pickup()
        {
            // La duración la controla el sistema del teléfono. En PC y Editor no vibra.
            // Unity añade VIBRATE al manifiesto de Android al detectar Handheld.Vibrate.
            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (Application.isFocused) Handheld.Vibrate();
            #endif
        }
    }
}
