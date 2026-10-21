using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ratones.Basic.Editor
{
    public static class FreezePickerTools
    {
        [MenuItem("Ratones/Preparar selector de trampa")]
        public static void Prepare()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play y abre la escena Juego."); return; }
            GameUI game = Object.FindFirstObjectByType<GameUI>();
            if (game == null) { Debug.LogWarning("Abre la escena Juego para crear el selector."); return; }
            Undo.RecordObject(game, "Preparar selector de trampa");
            bool existed = game.FreezePicker != null || (game.ExitDialog != null &&
                game.ExitDialog.transform.parent.GetComponentInChildren<FreezeTargetPicker>(true) != null);
            FreezeTargetPicker picker = FreezeTargetPicker.Ensure(game);
            if (!existed) Undo.RegisterCreatedObjectUndo(picker.gameObject, "Crear selector de trampa");
            picker.Hide(); EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Selection.activeGameObject = picker.gameObject;
            Debug.Log("Selector listo en Canvas/AreaSegura/SelectorTrampa. Actívalo para acomodarlo, desactívalo y guarda con Ctrl+S. Al jugar se abre con DETENER o Q.");
        }
    }
}
