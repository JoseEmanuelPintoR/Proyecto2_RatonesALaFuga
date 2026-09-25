using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ratones.Basic.Editor
{
    public static class LobbyTools
    {
        [MenuItem("Ratones/Preparar botón Personalizar del lobby")]
        public static void Prepare()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de preparar el botón."); return; }
            Scene scene=SceneManager.GetActiveScene();
            if (scene.name != "Lobby") { Debug.LogWarning("Abre la escena Lobby antes de preparar el botón."); return; }
            LobbyUI lobby=null;
            foreach (GameObject root in scene.GetRootGameObjects())
            { lobby=root.GetComponentInChildren<LobbyUI>(true); if (lobby != null) break; }
            if (lobby == null || lobby.StartButton == null)
            { Debug.LogError("Falta LobbyUI o su referencia Start Button en esta escena."); return; }
            Undo.SetCurrentGroupName("Preparar Personalizar en lobby");
            int group=Undo.GetCurrentGroup();
            Button previous=lobby.FindPersonalizeButton();
            Undo.RecordObject(lobby,"Asignar botón Personalizar");
            Button button=lobby.EnsurePersonalizeButton();
            if (previous == null) Undo.RegisterCreatedObjectUndo(button.gameObject,"Crear botón Personalizar");
            else Undo.RecordObject(button,"Conectar botón Personalizar");
            bool linked=false;
            for (int i=button.onClick.GetPersistentEventCount()-1; i>=0; i--)
            {
                if (button.onClick.GetPersistentTarget(i)!=lobby) continue;
                string method=button.onClick.GetPersistentMethodName(i);
                if (method==nameof(LobbyUI.StartGame) || method==nameof(LobbyUI.Back))
                    UnityEventTools.RemovePersistentListener(button.onClick,i);
                else if (method==nameof(LobbyUI.OpenPersonalize))
                {
                    if (linked) UnityEventTools.RemovePersistentListener(button.onClick,i);
                    else { button.onClick.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.RuntimeOnly); linked=true; }
                }
            }
            if (!linked) UnityEventTools.AddPersistentListener(button.onClick,lobby.OpenPersonalize);
            button.interactable=true;
            EditorUtility.SetDirty(button); EditorUtility.SetDirty(lobby);
            if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            if (PrefabUtility.IsPartOfPrefabInstance(lobby)) PrefabUtility.RecordPrefabInstancePropertyModifications(lobby);
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            Selection.activeGameObject=button.gameObject;
            Debug.Log("Personalizar conectado. Guarda Lobby con Ctrl+S. Puedes mover el botón y cambiar su diseño desde Hierarchy; Guardar o Volver en Personalizar regresan a esta sala.");
        }
    }
}
