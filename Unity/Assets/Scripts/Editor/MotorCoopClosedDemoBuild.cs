using System;
using System.IO;
using DortCuce.UnityGame;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DortCuce.UnityGame.Editor
{
    public static class MotorCoopClosedDemoBuild
    {
        private const string ScenePath = "Assets/Scenes/YolArkadasi.unity";
        private const string OutputPath = "Builds/ClosedDemo/Roadmates-Closed-Playtest.exe";
        private const string DemoVersion = "0.6.0-demo.1";

        [MenuItem("Roadmates/03 Build Closed IL2CPP Playtest")]
        public static void GenerateAndBuild()
        {
            Directory.CreateDirectory("../qa");
            MotorSimulationChecks.Run();
            CoopRoleChecks.Run();

            if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Motorcycle/EnginMotor.fbx"))
                throw new Exception("Motor FBX import missing");

            MotorCoopBuild.GenerateScene();
            ConfigureClosedDemo();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Closed demo build failed: " + report.summary.result);

            string result =
                "MOTOR_COOP_CLOSED_DEMO_PASS\n" +
                "BuildId=" + ClosedDemoMark.BuildId + "\n" +
                "Version=" + DemoVersion + "\n" +
                "Backend=IL2CPP\n" +
                "ManagedStripping=High\n" +
                "DevelopmentBuild=False\n" +
                "Bytes=" + report.summary.totalSize + "\n" +
                "Seconds=" + report.summary.totalTime.TotalSeconds;

            File.WriteAllText("../qa/closed-demo-build-result.txt", result);
            Debug.Log(result);
        }

        private static void ConfigureClosedDemo()
        {
            PlayerSettings.companyName = "Engin";
            PlayerSettings.productName = "Roadmates - Closed Playtest";
            PlayerSettings.bundleVersion = DemoVersion;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;

            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
        }
    }
}
