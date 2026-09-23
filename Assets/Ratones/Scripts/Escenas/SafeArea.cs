using UnityEngine;

namespace Ratones.Basic
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        RectTransform rect;
        Rect previous;
        Vector2 screen;
        void Awake() { rect = GetComponent<RectTransform>(); }
        void Update()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            if (area.width <= 0 || area.height <= 0) area = new Rect(0, 0, Screen.width, Screen.height);
            if (area == previous && screen == new Vector2(Screen.width, Screen.height)) return;
            previous = area; screen = new Vector2(Screen.width, Screen.height);
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
