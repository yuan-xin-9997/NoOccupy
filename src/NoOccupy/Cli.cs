using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using NoOccupy.Core;

namespace NoOccupy
{
    /// <summary>
    /// 命令行接口（供自动化验收使用）。
    /// 参数格式：
    ///   --scan "&lt;path&gt;" [--no-deep] [--json] [--out file]
    ///   --kill "&lt;path&gt;" [--all | --pid 12,34] [--no-deep] [--json] [--out file]
    ///   --version | --help
    /// </summary>
    internal static class Cli
    {
        private const int AttachParentProcess = -1;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        private sealed class CommandOptions
        {
            public string Path;
            public bool Deep = true;
            public bool Json;
            public string OutFile;
            public bool All;
            public readonly List<int> Pids = new List<int>();
        }

        public static bool TryRun(string[] args, out int exitCode)
        {
            exitCode = 0;

            if (args == null || args.Length == 0)
            {
                return false;
            }

            string first = args[0];
            if (string.IsNullOrEmpty(first) ||
                !first.StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }

            PrepareConsole();

            switch (first.ToLowerInvariant())
            {
                case "--help":
                    WriteOut(GetHelpText());
                    exitCode = 0;
                    return true;

                case "--version":
                    WriteOut("NoOccupy " + GetVersion());
                    exitCode = 0;
                    return true;

                case "--scan":
                    exitCode = RunScan(args);
                    return true;

                case "--kill":
                    exitCode = RunKill(args);
                    return true;

                default:
                    WriteError("未知参数：" + first);
                    WriteError(GetHelpText());
                    exitCode = 1;
                    return true;
            }
        }

        private static int RunScan(string[] args)
        {
            bool jsonRequested;
            string outRequested;
            PreScanOutput(args, 1, out jsonRequested, out outRequested);

            CommandOptions options;
            string error;
            if (!ParseOptions(args, 1, false, out options, out error))
            {
                return Fail(
                    options.Json || jsonRequested,
                    options.OutFile ?? outRequested,
                    error);
            }

            if (string.IsNullOrWhiteSpace(options.Path))
            {
                return Fail(options.Json, options.OutFile,
                    "缺少扫描路径，例如：--scan \"C:\\path\"");
            }

            try
            {
                ScanResult result = LockFinder.Scan(
                    options.Path, options.Deep, null, CancellationToken.None);

                string output = options.Json
                    ? ToJson(BuildScanPayload(result))
                    : FormatScanText(result);

                bool emitted = Emit(output, options.OutFile);
                return emitted ? 0 : 1;
            }
            catch (OperationCanceledException)
            {
                return Fail(options.Json, options.OutFile, "扫描已取消");
            }
            catch (Exception ex)
            {
                return Fail(options.Json, options.OutFile, ex.Message);
            }
        }

        private static int RunKill(string[] args)
        {
            bool jsonRequested;
            string outRequested;
            PreScanOutput(args, 1, out jsonRequested, out outRequested);

            CommandOptions options;
            string error;
            if (!ParseOptions(args, 1, true, out options, out error))
            {
                return Fail(
                    options.Json || jsonRequested,
                    options.OutFile ?? outRequested,
                    error);
            }

            if (string.IsNullOrWhiteSpace(options.Path))
            {
                return Fail(options.Json, options.OutFile,
                    "缺少扫描路径，例如：--kill \"C:\\path\" --all");
            }

            if (options.All && options.Pids.Count > 0)
            {
                return Fail(options.Json, options.OutFile, "--all 与 --pid 不能同时使用");
            }

            if (!options.All && options.Pids.Count == 0)
            {
                return Fail(options.Json, options.OutFile, "请指定 --all 或 --pid");
            }

            try
            {
                ScanResult result = LockFinder.Scan(
                    options.Path, options.Deep, null, CancellationToken.None);

                List<int> pids;
                if (options.All)
                {
                    pids = result.Processes
                        .Where(p => !p.IsCritical)
                        .Select(p => p.Pid)
                        .Distinct()
                        .ToList();
                }
                else
                {
                    // --pid 允许传入扫描结果之外的 PID，但进程必须存在。
                    foreach (int pid in options.Pids)
                    {
                        if (!ProcessExists(pid))
                        {
                            return Fail(options.Json, options.OutFile,
                                "进程不存在：" + pid);
                        }
                    }

                    pids = options.Pids.Distinct().ToList();
                }

                KillResult killResult = ProcessKiller.Kill(pids, null);

                string output = options.Json
                    ? ToJson(BuildKillPayload(result, killResult))
                    : FormatKillText(result, killResult);

                bool emitted = Emit(output, options.OutFile);
                if (!emitted || killResult.Failed > 0)
                {
                    return emitted ? 2 : 1;
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                return Fail(options.Json, options.OutFile, "扫描已取消");
            }
            catch (Exception ex)
            {
                return Fail(options.Json, options.OutFile, ex.Message);
            }
        }

        /// <summary>在正式解析前找出 --json / --out，保证参数错误时也能按要求的格式输出。</summary>
        private static void PreScanOutput(
            string[] args, int start, out bool json, out string outFile)
        {
            json = false;
            outFile = null;

            for (int i = start; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (arg.Equals("--json", StringComparison.OrdinalIgnoreCase))
                {
                    json = true;
                }
                else if (arg.Equals("--out", StringComparison.OrdinalIgnoreCase) &&
                         i + 1 < args.Length)
                {
                    outFile = args[++i];
                }
            }
        }

        private static bool ParseOptions(
            string[] args,
            int start,
            bool allowKillOptions,
            out CommandOptions options,
            out string error)        {
            options = new CommandOptions();
            error = null;

            for (int i = start; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;

                if (arg.Equals("--no-deep", StringComparison.OrdinalIgnoreCase))
                {
                    options.Deep = false;
                }
                else if (arg.Equals("--json", StringComparison.OrdinalIgnoreCase))
                {
                    options.Json = true;
                }
                else if (arg.Equals("--out", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        error = "--out 缺少文件路径";
                        return false;
                    }

                    options.OutFile = args[++i];
                }
                else if (allowKillOptions &&
                         arg.Equals("--all", StringComparison.OrdinalIgnoreCase))
                {
                    options.All = true;
                }
                else if (allowKillOptions &&
                         arg.Equals("--pid", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        error = "--pid 缺少 PID 列表";
                        return false;
                    }

                    List<int> pids;
                    if (!TryParsePidList(args[++i], out pids))
                    {
                        error = "无效的 PID 列表：" + args[i];
                        return false;
                    }

                    options.Pids.AddRange(pids);
                }
                else if (arg.Length > 0 && arg[0] != '-')
                {
                    if (options.Path == null)
                    {
                        options.Path = arg;
                    }
                    else
                    {
                        error = "多余参数：" + arg;
                        return false;
                    }
                }
                else
                {
                    error = "未知参数：" + arg;
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, object> BuildScanPayload(ScanResult result)
        {
            var processes = new List<object>();
            foreach (LockingProcess process in result.Processes)
            {
                processes.Add(new Dictionary<string, object>
                {
                    { "pid", process.Pid },
                    { "name", process.Name ?? string.Empty },
                    { "path", process.ExecutablePath ?? string.Empty },
                    { "service", process.ServiceName ?? string.Empty },
                    { "source", process.Source.ToString() },
                    { "critical", process.IsCritical }
                });
            }

            return new Dictionary<string, object>
            {
                { "ok", true },
                { "target", result.TargetPath ?? string.Empty },
                { "isDirectory", result.IsDirectory },
                { "elapsedMs", result.ElapsedMs },
                { "scannedFiles", result.ScannedFiles },
                { "processes", processes },
                { "warnings", result.Warnings }
            };
        }

        private static Dictionary<string, object> BuildKillPayload(
            ScanResult result, KillResult killResult)
        {
            Dictionary<string, object> payload = BuildScanPayload(result);

            var killed = new List<object>();
            foreach (KillOutcome outcome in killResult.Outcomes)
            {
                killed.Add(new Dictionary<string, object>
                {
                    { "pid", outcome.Pid },
                    { "name", outcome.Name ?? string.Empty },
                    { "success", outcome.Success },
                    { "error", outcome.Error ?? string.Empty }
                });
            }

            payload["killed"] = killed;
            payload["killedOk"] = killResult.Succeeded;
            payload["killedFail"] = killResult.Failed;
            return payload;
        }

        private static string FormatScanText(ScanResult result)
        {
            var text = new StringBuilder();
            text.AppendLine("扫描结果");
            text.AppendLine("目标：" + result.TargetPath);
            text.AppendLine("类型：" + (result.IsDirectory ? "文件夹" : "文件"));
            text.AppendLine("扫描文件数：" + result.ScannedFiles);
            text.AppendLine("耗时：" + result.ElapsedMs + " 毫秒");
            text.AppendLine("占用进程（" + result.Processes.Count + "）：");

            foreach (LockingProcess process in result.Processes)
            {
                text.AppendFormat(
                    "  PID {0}  {1}  {2}{3}  [来源：{4}]{5}",
                    process.Pid,
                    process.Name,
                    process.ExecutablePath,
                    string.IsNullOrEmpty(process.ServiceName)
                        ? string.Empty
                        : "  [服务：" + process.ServiceName + "]",
                    process.SourceText,
                    process.IsCritical ? "  [系统关键进程]" : string.Empty);
                text.AppendLine();
            }

            if (result.Warnings.Count > 0)
            {
                text.AppendLine("警告（" + result.Warnings.Count + "）：");
                foreach (string warning in result.Warnings)
                {
                    text.AppendLine("  " + warning);
                }
            }

            return text.ToString();
        }

        private static string FormatKillText(ScanResult result, KillResult killResult)
        {
            var text = new StringBuilder(FormatScanText(result));
            text.AppendLine();
            text.AppendLine("结束结果：");

            foreach (KillOutcome outcome in killResult.Outcomes)
            {
                text.AppendFormat(
                    "  PID {0}  {1}  {2}{3}",
                    outcome.Pid,
                    outcome.Name,
                    outcome.Success ? "成功" : "失败",
                    string.IsNullOrEmpty(outcome.Error)
                        ? string.Empty
                        : "（" + outcome.Error + "）");
                text.AppendLine();
            }

            text.AppendFormat(
                "成功 {0} 个，失败 {1} 个", killResult.Succeeded, killResult.Failed);
            text.AppendLine();
            return text.ToString();
        }

        private static bool TryParsePidList(string value, out List<int> pids)
        {
            pids = new List<int>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Split(
                new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            foreach (string part in parts)
            {
                int pid;
                if (!int.TryParse(
                        part,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out pid) ||
                    pid <= 0)
                {
                    return false;
                }

                if (!pids.Contains(pid))
                {
                    pids.Add(pid);
                }
            }

            return pids.Count > 0;
        }

        private static bool ProcessExists(int pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    GC.KeepAlive(process);
                }
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch
            {
                // 其他异常（例如权限不足）说明进程大概率存在。
                return true;
            }
        }

        private static int Fail(bool json, string outFile, string message)
        {
            if (json)
            {
                string output = ToJson(new Dictionary<string, object>
                {
                    { "ok", false },
                    { "error", message ?? string.Empty }
                });

                Emit(output, outFile);
            }
            else
            {
                WriteError("错误：" + message);
                if (!string.IsNullOrEmpty(outFile))
                {
                    TryWriteFile(outFile, "错误：" + message);
                }
            }

            return 1;
        }

        private static bool Emit(string text, string outFile)
        {
            bool fileOk = true;

            if (!string.IsNullOrEmpty(outFile))
            {
                try
                {
                    File.WriteAllText(outFile, text, Utf8NoBom);
                }
                catch (Exception ex)
                {
                    WriteError("写入输出文件失败：" + ex.Message);
                    fileOk = false;
                }
            }

            WriteOut(text);
            return fileOk;
        }

        private static void WriteOut(string text)
        {
            WriteToStandardStream(false, text);
        }

        private static void WriteError(string text)
        {
            WriteToStandardStream(true, text);
        }

        private static void WriteToStandardStream(bool error, string text)
        {
            try
            {
                if (text == null)
                {
                    return;
                }

                byte[] bytes = Utf8NoBom.GetBytes(text + Environment.NewLine);
                Stream stream = error
                    ? Console.OpenStandardError()
                    : Console.OpenStandardOutput();

                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch
            {
                // 无控制台或输出管道已关闭时忽略。
            }
        }

        private static void TryWriteFile(string path, string text)
        {
            try
            {
                File.WriteAllText(path, text, Utf8NoBom);
            }
            catch
            {
            }
        }

        private static string ToJson(object payload)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return EscapeNonAscii(serializer.Serialize(payload));
        }

        /// <summary>
        /// 把非 ASCII 字符转成 \uXXXX，保证 JSON 在任何控制台代码页下都是纯 ASCII 的合法 UTF-8，
        /// 避免自动化读取时因代码页不同出现乱码。
        /// </summary>
        private static string EscapeNonAscii(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            StringBuilder builder = null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < 128)
                {
                    if (builder != null)
                    {
                        builder.Append(c);
                    }
                    continue;
                }

                if (builder == null)
                {
                    builder = new StringBuilder(text.Length + 64);
                    builder.Append(text, 0, i);
                }

                builder.Append("\\u");
                builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }

            return builder == null ? text : builder.ToString();
        }

        private static string GetVersion()
        {
            try
            {
                Version version = typeof(Cli).Assembly.GetName().Version;
                return version == null ? "1.0.1" : version.ToString();
            }
            catch
            {
                return "1.0.1";
            }
        }

        private static string GetHelpText()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "NoOccupy 解除占用 - 命令行",
                string.Empty,
                "用法：",
                "  NoOccupy.exe --scan \"<路径>\" [--no-deep] [--json] [--out <文件>]",
                "  NoOccupy.exe --kill \"<路径>\" [--all | --pid 12,34] [--no-deep] [--json] [--out <文件>]",
                "  NoOccupy.exe --version",
                "  NoOccupy.exe --help",
                string.Empty,
                "选项：",
                "  --no-deep      跳过 NT 句柄深度扫描",
                "  --json         输出 JSON（UTF-8）",
                "  --out <文件>   同时把结果写入 UTF-8 文件",
                "  --all          结束全部非关键占用进程",
                "  --pid          仅结束指定 PID（逗号分隔）",
                string.Empty,
                "退出码：0 成功；1 参数或扫描错误；2 结束进程存在失败。"
            });
        }

        private static void PrepareConsole()
        {
            try
            {
                if (GetConsoleWindow() == IntPtr.Zero)
                {
                    AttachConsole(AttachParentProcess);
                }
            }
            catch
            {
            }

            try
            {
                // 控制台使用 UTF-8；输出文件始终使用无 BOM 的 UTF-8。
                Console.OutputEncoding = Utf8NoBom;
                Console.InputEncoding = Utf8NoBom;
            }
            catch
            {
                // 无控制台（重定向或从资源管理器启动）时忽略。
            }
        }
    }
}
