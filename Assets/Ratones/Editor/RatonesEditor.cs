#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ratones.Gameplay;

namespace Ratones.Editor
{
    public static class RatonesEditor
    {
        const string ScenePath = "Assets/Ratones/Scenes/Ratones_a_la_fuga.unity";

        [MenuItem("Ratones a la fuga/1. Abrir escena del juego")]
        public static void OpenGame()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Ratones a la fuga/2. Configurar PC y Android")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Equipo Ratones";
            PlayerSettings.productName = "Ratones a la fuga";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "co.edu.ratones.alafuga");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; settings.ApplyModifiedProperties(); }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Ratones a la fuga configurado. Abre la escena y pulsa Play. Usa Input Manager (Old).");
        }

        [MenuItem("Ratones a la fuga/3. Crear APK Android")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            { EditorUtility.DisplayDialog("Falta Android Build Support", "Instálalo desde Unity Hub, junto con SDK, NDK y OpenJDK.", "Aceptar"); return; }
            Configure();
            string output = EditorUtility.SaveFilePanel("Guardar APK", "", "Ratones_a_la_fuga", "apk");
            if (string.IsNullOrEmpty(output)) return;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.Android, options = BuildOptions.None });
        }
    }
}
#endif
