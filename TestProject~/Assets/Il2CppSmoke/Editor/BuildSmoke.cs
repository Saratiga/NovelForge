using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NovelForgeSmoke
{
    // Builds a Windows IL2CPP player with High managed stripping that runs SmokeRunner.
    // Usage: unity build TestProject~ --target StandaloneWindows64
    //        --execute-method NovelForgeSmoke.BuildSmoke.Build --output-path <exe path>
    // Player settings are restored afterwards so the host project stays unchanged.
    public static class BuildSmoke
    {
        private const string ScenePath = "Assets/Il2CppSmoke/Smoke.unity";

        public static void Build()
        {
            string output = ArgValue("-buildOutput") ?? Path.GetFullPath("Build/Il2CppSmoke/NovelForgeSmoke.exe");
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            ScriptingImplementation oldBackend = PlayerSettings.GetScriptingBackend(target);
            ManagedStrippingLevel oldStripping = PlayerSettings.GetManagedStrippingLevel(target);

            BuildResult result;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("SmokeRunner").AddComponent<SmokeRunner>();
                EditorSceneManager.SaveScene(scene, ScenePath);

                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                result = report.summary.result;
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, oldBackend);
                PlayerSettings.SetManagedStrippingLevel(target, oldStripping);
                AssetDatabase.DeleteAsset(ScenePath);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"NOVELFORGE_SMOKE_BUILD {result} -> {output}");
            if (result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
