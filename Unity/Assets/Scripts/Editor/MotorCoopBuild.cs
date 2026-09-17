using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DortCuce.UnityGame.Editor
{
    [InitializeOnLoad]
    public static class MotorCoopBuild
    {
        const string ScenePath="Assets/Scenes/YolArkadasi.unity";
        static bool busy;
        static double nextPoll;
        static MotorCoopBuild(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+2;
            var command=Path.GetFullPath("../qa/build.request");
            if(!File.Exists(command))return;
            File.Delete(command);busy=true;
            try{GenerateAndBuild();}catch(Exception e){Debug.LogException(e);File.WriteAllText("../qa/build-result.txt","FAILED\n"+e);}
            finally{busy=false;}
        }
        [MenuItem("Roadmates/01 Generate Scene")]
        public static void GenerateScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Roadmates • Co-op");root.AddComponent<MotorCoopGame>();
            Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("MOTOR_COOP_SCENE_READY "+ScenePath);
        }
        [MenuItem("Roadmates/02 Test and Build Windows")]
        public static void GenerateAndBuild()
        {
            Directory.CreateDirectory("../qa");
            MotorSimulationChecks.Run();CoopRoleChecks.Run();
            if(!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Motorcycle/EnginMotor.fbx"))throw new Exception("Motor FBX import missing");
            GenerateScene();
            PlayerSettings.companyName="Engin";PlayerSettings.productName="Roadmates";
            PlayerSettings.bundleVersion="0.6.0";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            string output="Builds/Windows/Roadmates.exe";Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            string result="MOTOR_COOP_BUILD_PASS\nBytes="+report.summary.totalSize+"\nSeconds="+report.summary.totalTime.TotalSeconds;
            File.WriteAllText("../qa/build-result.txt",result);Debug.Log(result);
        }
    }
}
