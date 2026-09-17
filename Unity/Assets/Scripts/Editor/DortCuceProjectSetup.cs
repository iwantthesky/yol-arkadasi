using System;
using System.IO;
using DortCuce.UnityGame;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DortCuce.UnityGame.Editor
{
    public static class DortCuceProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/DortCuce.unity";
        private const string BuildPath = "Builds/WindowsUnity/Dort-Cuce-Bir-Motor-Unity.exe";

        [MenuItem("Dört Cüce/Generate Scene")]
        public static void GenerateScene()
        {
            DortCuceTrackDefinition.Validate(DortCuceTrackDefinition.Create());
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Dört Cüce Runtime");
            bootstrap.AddComponent<DortCuceGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DORT_CUCE_SCENE_READY");
        }

        [MenuItem("Dört Cüce/Generate And Build Windows")]
        public static void GenerateAndBuild()
        {
            try
            {
                GenerateScene();
                PlayerSettings.companyName = "Engin";
                PlayerSettings.productName = "Dört Cüce Bir Motor Unity";
                PlayerSettings.bundleVersion = "0.1.0";
                PlayerSettings.defaultScreenWidth = 1440;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.runInBackground = false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

                Directory.CreateDirectory(Path.GetDirectoryName(BuildPath) ?? "Builds/WindowsUnity");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = BuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
                }

                Debug.Log($"DORT_CUCE_UNITY_BUILD_READY path={BuildPath} bytes={report.summary.totalSize}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
        }
    }
}
