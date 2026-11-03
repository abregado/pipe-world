using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PipeWorld.EditorTools
{
    public static class BuildScript
    {
        const string OutputDir = "Builds/Windows";
        const string ExeName = "PipeWorld.exe";

        [MenuItem("PipeWorld/Build Windows")]
        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] No enabled scenes in Build Settings.");
                Exit(1);
                return;
            }

            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.StrictMode,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] Result: {summary.result}, errors: {summary.totalErrors}, " +
                      $"warnings: {summary.totalWarnings}, size: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"time: {summary.totalTime.TotalSeconds:F0}s");
            Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
