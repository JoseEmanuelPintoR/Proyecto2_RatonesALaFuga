using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ratones.Basic.Editor
{
    // Herramientas opcionales. No regeneran escenas ni sobrescriben el trabajo de arte.
    public static class PrototypeTools
    {
        static readonly string[] Scenes = { "MenuInicio", "Conexion", "Lobby", "Juego", "Resultados", "Configuracion", "Personalizar" };
        static string Path(string name) { return "Assets/Ratones/Scenes/" + name + ".unity"; }

        [MenuItem("Ratones/Abrir menú de inicio")]
        public static void OpenMenu()
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Path("MenuInicio"));
        }

        [MenuItem("Ratones/Verificar escenas y botones")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int errors = 0, buttons = 0;
            try
            {
                foreach (string name in Scenes)
                {
                    if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == Path(name)))
                    { Debug.LogError(name + ": falta en la lista de escenas de compilación."); errors++; }
                    Scene scene = EditorSceneManager.OpenScene(Path(name), OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        {
                            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                            if (missing > 0) { Debug.LogError(name + "/" + t.name + ": script faltante.", t); errors += missing; }
                        }
                        foreach (var b in root.GetComponentsInChildren<Button>(true))
                        {
                            buttons++;
                            if (b.GetComponent<DirectionButton>() != null) continue; // Pulsación sostenida por IPointerDown/Up.
                            errors += CheckEvent(name, b.name, b.onClick);
                        }
                        foreach (var s in root.GetComponentsInChildren<Slider>(true)) errors += CheckEvent(name, s.name, s.onValueChanged);
                        foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            if (b == null || b.GetType().Namespace != "Ratones.Basic") continue;
                            var data = new SerializedObject(b); var property = data.GetIterator();
                            while (property.NextVisible(true))
                                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                                { Debug.LogError(name + "/" + b.name + ": referencia rota en " + property.propertyPath, b); errors++; }
                        }
                    }
                    foreach (string type in new[] { "Canvas", "EventSystem" })
                        if (!scene.GetRootGameObjects().Any(g => g.name == type)) { Debug.LogError(name + ": falta " + type); errors++; }
                }
            }
            catch (Exception e) { Debug.LogException(e); errors++; }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            if (errors == 0) Debug.Log("Ratones: 7 escenas y " + buttons + " botones revisados. Sin referencias rotas detectadas. Realiza una partida con dos dispositivos para comprobar la red Wi-Fi.");
            else Debug.LogError("Ratones: se detectaron " + errors + " problemas. Consulta la consola.");
        }

        static int CheckEvent(string scene, string name, UnityEventBase ev)
        {
            if (ev.GetPersistentEventCount() == 0) { Debug.LogError(scene + "/" + name + ": evento sin acción."); return 1; }
            int errors = 0;
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            {
                var target = ev.GetPersistentTarget(i); string method = ev.GetPersistentMethodName(i);
                if (target == null || !target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m => m.Name == method))
                { Debug.LogError(scene + "/" + name + ": destino o método faltante: " + method); errors++; }
            }
            return errors;
        }
    }
}
