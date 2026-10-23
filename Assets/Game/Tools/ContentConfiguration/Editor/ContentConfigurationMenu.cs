using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tools.ContentConfiguration.Editor
{
    /// <summary>内容创作工具：Excel和Python依赖只存在于编辑器侧，不进入领域或Player。</summary>
    public static class ContentConfigurationMenu
    {
        private const string InterpreterKey = "game-td.ConfigurationPython";
        private static bool _exporting;

        [MenuItem("Game/内容配置/打开 Excel")]
        public static void OpenWorkbook() => EditorUtility.OpenWithDefaultApp(Path.Combine(ProjectRoot, "Config/GameContent.xlsx"));

        [MenuItem("Game/内容配置/选择 Python 解释器")]
        public static void ChoosePython()
        {
            string path = EditorUtility.OpenFilePanel("选择 Python 3 解释器", "", "");
            if (!string.IsNullOrEmpty(path))
                EditorPrefs.SetString(InterpreterKey, path);
        }

        [MenuItem("Game/内容配置/导出 Excel")]
        public static async void Export()
        {
            if (_exporting)
                return;
            _exporting = true;
            try
            {
                string python = FindPython();
                var start = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = "\"" + Path.Combine(ProjectRoot, "Tools/ContentConfiguration/export_config.py") + "\"",
                    WorkingDirectory = ProjectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                start.EnvironmentVariables["PYTHONUTF8"] = "1";
                using var process = Process.Start(start);
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                string message = await output;
                string failure = await errors;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(failure + message);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Excel 配置导出完成。重新启动试玩使配置生效。\n" + message);
            }
            catch (Exception error)
            {
                Debug.LogError("配置导出失败，保留上次生成结果：\n" + error.Message);
            }
            finally { _exporting = false; }
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string FindPython()
        {
            string selected = Environment.GetEnvironmentVariable("GAME_TD_PYTHON");
            if (string.IsNullOrWhiteSpace(selected))
                selected = EditorPrefs.GetString(InterpreterKey, "");
            if (!string.IsNullOrWhiteSpace(selected) && File.Exists(selected))
                return selected;
            // 开发工具可复用本机已有运行环境，不把此路径写入游戏加载逻辑。
            string bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe");
            return File.Exists(bundled) ? bundled : "python";
        }
    }
}
