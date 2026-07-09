using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BokeGameJam.EditorTools
{
    public static class AndroidBuildScript
    {
        private const string OutputPath = "Builds/Android/BokeGameJam-Android-Test.apk";

        public static void BuildTestApk()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("[AndroidBuildScript] No enabled build scenes were found.");

            string fullOutputPath = Path.GetFullPath(OutputPath);
            string directory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayerOptions options = new()
            {
                scenes = scenes,
                locationPathName = fullOutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"[AndroidBuildScript] Android build failed: {summary.result}");

            UnityEngine.Debug.Log($"[AndroidBuildScript] Android APK built: {fullOutputPath}");
        }
    }
}
