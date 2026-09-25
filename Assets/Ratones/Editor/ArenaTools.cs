using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ratones.Basic.Editor
{
    public static class ArenaTools
    {
        const string Folder = "Assets/Ratones/Geometria";
        [MenuItem("Ratones/Preparar rampas y obstáculos")]
        public static void Prepare()
        {
            if (Application.isPlaying) { Debug.LogWarning("Detén Play antes de preparar las rampas."); return; }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Juego") { Debug.LogWarning("Abre la escena Juego antes de preparar las rampas."); return; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Ratones","Geometria");
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ArenaView previous in root.GetComponentsInChildren<ArenaView>(true))
                    Undo.DestroyObjectImmediate(previous.gameObject);
            Material floor = LoadMaterial("Suelo",new Color(.66f,.66f,.66f));
            Material blocks = LoadMaterial("Borde",new Color(.38f,.38f,.38f));
            ArenaView view = ArenaView.Create(floor,blocks,SaveMesh);
            Undo.RegisterCreatedObjectUndo(view.gameObject,"Preparar rampas y obstáculos");
            Selection.activeGameObject=view.gameObject;
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            Debug.Log("Listo: 4 rampas, 2 plataformas y 6 obstáculos. Guarda Juego con Ctrl+S. Para cambiar posiciones o tamaños, edita ArenaLayout.cs y vuelve a preparar el mapa.");
        }
        static Material LoadMaterial(string name, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ratones/Materiales/"+name+".mat");
            if (material != null) return material;
            string path=Folder+"/"+name+".mat";
            material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Standard"));material.color=color;AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path=Folder+"/"+name+".asset";
            Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
            EditorUtility.CopySerialized(mesh,existing); EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh); return existing;
        }
    }
}
