using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ratones.Basic.Editor
{
    public static class MenuTools
    {
        [MenuItem("Ratones/Preparar vista del ratón (Personalizar)")]
        public static void PreparePreview()
        {
            Scene scene = Open("Personalizar"); if (!scene.IsValid()) return;
            PersonalizeUI ui = Find<PersonalizeUI>(scene);
            if (ui == null) { Debug.LogError("Falta PersonalizeUI en esta escena."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/Ratones/Prefabs/JugadorBasico.prefab");
            if (prefab == null) { Debug.LogError("No se encontró JugadorBasico.prefab."); return; }
            Transform parent = FindNamed(scene, "AreaSegura");
            if (parent == null) { Canvas canvas = Find<Canvas>(scene); if (canvas != null) parent = canvas.transform; }
            if (parent == null) { Debug.LogError("Falta el Canvas en esta escena."); return; }

            Undo.SetCurrentGroupName("Preparar vista del ratón"); int group = Undo.GetCurrentGroup();
            MousePreview preview = ui.Preview;
            if (preview == null)
            {
                var go = new GameObject("VistaRaton", typeof(RectTransform), typeof(RawImage), typeof(MousePreview));
                Undo.RegisterCreatedObjectUndo(go, "Crear vista del ratón");
                go.transform.SetParent(parent, false); go.transform.SetAsFirstSibling();
                var rect = (RectTransform)go.transform;
                // Mitad izquierda de la pantalla, la parte blanca junto a los controles.
                rect.anchorMin = new Vector2(.02f, .1f); rect.anchorMax = new Vector2(.45f, .9f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                preview = go.GetComponent<MousePreview>();
                go.GetComponent<RawImage>().raycastTarget = false;
            }
            Undo.RecordObject(preview, "Conectar vista del ratón");
            preview.Prefab = prefab; preview.Target = preview.GetComponent<RawImage>();
            Undo.RecordObject(ui, "Conectar vista del ratón"); ui.Preview = preview;
            Finish(scene, group, preview.gameObject, "Vista del ratón lista. Guarda Personalizar con Ctrl+S. Puedes mover o cambiar el tamaño de VistaRaton desde Hierarchy.");
        }

        [MenuItem("Ratones/Preparar desplegable Unirse (Conexion)")]
        public static void PrepareJoin()
        {
            Scene scene = Open("Conexion"); if (!scene.IsValid()) return;
            NavigationUI nav = Find<NavigationUI>(scene);
            if (nav == null || nav.Code == null) { Debug.LogError("Falta NavigationUI o su campo Code en esta escena."); return; }
            // Primero ToggleJoin: si ya se preparó antes, el botón Entrar también llama a Join.
            Button join = FindButton(scene, nav, nameof(NavigationUI.ToggleJoin), "Unirse") ?? FindButton(scene, nav, nameof(NavigationUI.Join), "Unirse");
            if (join == null) { Debug.LogError("No se encontró el botón Unirse."); return; }
            Undo.SetCurrentGroupName("Preparar desplegable Unirse"); int group = Undo.GetCurrentGroup();

            Transform parent = join.transform.parent;
            GameObject panel = nav.JoinPanel;
            if (panel == null)
            {
                // El panel cubre al padre para que los hijos que se muevan adentro conserven su posición.
                panel = new GameObject("PanelUnirse", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(panel, "Crear panel Unirse");
                panel.transform.SetParent(parent, false);
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            if (panel.GetComponent<CanvasGroup>() == null) Undo.AddComponent<CanvasGroup>(panel);
            RectTransform code = (RectTransform)nav.Code.transform;
            RectTransform codeLabel = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Text label in root.GetComponentsInChildren<Text>(true))
                    if (label.GetComponentInParent<Selectable>(true) == null &&
                        string.Equals((label.text ?? "").Trim(), "Código de sala", System.StringComparison.OrdinalIgnoreCase))
                    {
                        codeLabel = label.rectTransform;
                        if (label.transform.parent != panel.transform) Undo.SetTransformParent(label.transform, panel.transform, "Mover texto del código");
                    }
            if (code.parent != panel.transform) Undo.SetTransformParent(code, panel.transform, "Mover código");

            Transform existing = panel.transform.Find("Entrar");
            Button enter = existing != null ? existing.GetComponent<Button>() : null;
            if (enter == null)
            {
                GameObject copy = Object.Instantiate(join.gameObject, panel.transform, false);
                Undo.RegisterCreatedObjectUndo(copy, "Crear botón Entrar");
                copy.name = "Entrar"; enter = copy.GetComponent<Button>(); enter.onClick = new Button.ButtonClickedEvent();
                Text text = copy.GetComponentInChildren<Text>(true); if (text != null) text.text = "Entrar";
                UnityEventTools.AddPersistentListener(enter.onClick, nav.Join);
            }

            // Posiciones con el desplegable abierto (centrado en pantalla, referencia 1280x720).
            // Cerrado, NavigationUI baja Crear partida, Unirse y el estado 65 px para dejarlos centrados.
            Place(nav.CreateButton != null ? (RectTransform)nav.CreateButton.transform : null, 120);
            Place((RectTransform)join.transform, 10);
            Place(codeLabel, -67);
            Place(code, -115);
            Place((RectTransform)enter.transform, -200);
            Place(nav.Status != null ? nav.Status.rectTransform : null, -300);
            var slide = new System.Collections.Generic.List<RectTransform>();
            if (nav.CreateButton != null) slide.Add((RectTransform)nav.CreateButton.transform);
            slide.Add((RectTransform)join.transform); slide.Add((RectTransform)panel.transform);
            if (nav.Status != null) slide.Add(nav.Status.rectTransform);

            Undo.RecordObject(join, "Unirse abre el desplegable");
            for (int i = join.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                if (join.onClick.GetPersistentTarget(i) == nav) UnityEventTools.RemovePersistentListener(join.onClick, i);
            UnityEventTools.AddPersistentListener(join.onClick, nav.ToggleJoin);
            Undo.RecordObject(nav, "Conectar desplegable"); nav.JoinPanel = panel; nav.JoinButton = enter;
            nav.Slide = slide.ToArray(); nav.ClosedOffset = -65;
            EditorUtility.SetDirty(join); EditorUtility.SetDirty(enter);
            Finish(scene, group, panel, "Desplegable Unirse listo. Guarda Conexion con Ctrl+S. En Play, Unirse muestra u oculta el código y el botón Entrar.");
        }

        static void Place(RectTransform rect, float y)
        {
            if (rect == null) return;
            Undo.RecordObject(rect, "Acomodar desplegable Unirse");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, y);
        }
        static Scene Open(string name)
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de preparar la escena."); return default(Scene); }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != name) { Debug.LogWarning("Abre la escena " + name + " antes de usar esta opción."); return default(Scene); }
            return scene;
        }
        static void Finish(Scene scene, int group, GameObject select, string message)
        {
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = select; Debug.Log(message);
        }
        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            { T found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
            return null;
        }
        static Transform FindNamed(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }
        static Button FindButton(Scene scene, Object target, string method, string name)
        {
            Button byName = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                        if (b.onClick.GetPersistentTarget(i) == target && b.onClick.GetPersistentMethodName(i) == method) return b;
                    if (byName == null && b.name == name) byName = b;
                }
            return byName;
        }
    }
}
