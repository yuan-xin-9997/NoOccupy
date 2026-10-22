using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NoOccupy.Core
{
    /// <summary>进程与路径相关的公共工具（供核心各组件复用）。</summary>
    internal static class ProcessUtil
    {
        internal const uint ProcessQueryLimitedInformation = 0x1000;
        internal const uint ProcessDupHandle = 0x0040;

        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const uint SePrivilegeEnabled = 0x00000002;

        private static readonly object PrivilegeLock = new object();
        private static bool _privilegeAttempted;

        /// <summary>
        /// 关键系统进程名单。这些进程被结束后会导致系统蓝屏、注销或无法登录，
        /// UI 会标记提示，ProcessKiller 会拒绝结束。
        /// </summary>
        private static readonly HashSet<string> CriticalNames = new HashSet<string>(
            new[]
            {
                "system.exe",
                "system idle process.exe",
                "idle.exe",
                "registry.exe",
                "secure system.exe",
                "memory compression.exe",
                "memcompression.exe",
                "smss.exe",
                "csrss.exe",
                "wininit.exe",
                "winlogon.exe",
                "services.exe",
                "lsass.exe",
                "lsaiso.exe",
                "fontdrvhost.exe"
            },
            StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(
            IntPtr processHandle,
            int flags,
            StringBuilder exeName,
            ref uint size);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(
            IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(
            string systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr tokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
            ref TOKEN_PRIVILEGES newState,
            uint bufferLength,
            IntPtr previousState,
            IntPtr returnLength);

        /// <summary>启用 SeDebugPrivilege，便于打开其他用户的进程。</summary>
        internal static void EnableDebugPrivilege()
        {
            lock (PrivilegeLock)
            {
                if (_privilegeAttempted)
                {
                    return;
                }
                _privilegeAttempted = true;

                IntPtr token = IntPtr.Zero;
                try
                {
                    if (!OpenProcessToken(
                            GetCurrentProcess(),
                            TokenAdjustPrivileges | TokenQuery,
                            out token))
                    {
                        return;
                    }

                    LUID luid;
                    if (!LookupPrivilegeValue(null, "SeDebugPrivilege", out luid))
                    {
                        return;
                    }

                    var privileges = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = SePrivilegeEnabled
                    };

                    AdjustTokenPrivileges(
                        token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero);
                }
                catch
                {
                    // 权限提升失败不影响后续按常规权限尝试。
                }
                finally
                {
                    if (token != IntPtr.Zero)
                    {
                        CloseHandle(token);
                    }
                }
            }
        }

        /// <summary>去掉引号、\\?\ 等前缀，规范为完整路径并去掉末尾分隔符。</summary>
        internal static string NormalizePath(string path)
        {
            if (path == null)
            {
                return null;
            }

            string result = path.Trim();
            if (result.Length >= 2 && result[0] == '"' && result[result.Length - 1] == '"')
            {
                result = result.Substring(1, result.Length - 2).Trim();
            }

            result = StripPathPrefix(result);

            try
            {
                result = Path.GetFullPath(result);
            }
            catch
            {
                // 非法路径保持原样，后续由存在性检查报错。
            }

            if (result.Length > 3)
            {
                result = result.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return result;
        }

        /// <summary>去掉 NT 路径前缀，转换为普通 Win32 路径。</summary>
        internal static string StripPathPrefix(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                return @"\\" + path.Substring(8);
            }
            if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(4);
            }

            return path;
        }

        /// <summary>获取进程完整 exe 路径；失败返回 null。</summary>
        internal static string GetProcessImagePath(int pid)
        {
            if (pid <= 0)
            {
                return null;
            }

            IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                int capacity = 1024;
                while (capacity <= 32768)
                {
                    var buffer = new StringBuilder(capacity);
                    uint size = (uint)capacity;

                    if (QueryFullProcessImageName(handle, 0, buffer, ref size))
                    {
                        string path = buffer.ToString();
                        return string.IsNullOrEmpty(path) ? null : NormalizePath(path);
                    }

                    // ERROR_INSUFFICIENT_BUFFER
                    if (Marshal.GetLastWin32Error() != 122)
                    {
                        return null;
                    }

                    capacity *= 2;
                }
            }
            catch
            {
                // 忽略查询失败。
            }
            finally
            {
                CloseHandle(handle);
            }

            return null;
        }

        internal static string GetProcessName(int pid, string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    string fileName = Path.GetFileName(imagePath);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        return fileName;
                    }
                }
                catch
                {
                }
            }

            if (pid == 0)
            {
                return "Idle";
            }
            if (pid == 4)
            {
                return "System";
            }

            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    string name = process.ProcessName;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            ? name
                            : name + ".exe";
                    }
                }
            }
            catch
            {
            }

            return "PID " + pid;
        }

        internal static LockingProcess CreateLockingProcess(int pid, LockSource source)
        {
            string imagePath = GetProcessImagePath(pid);
            string name = GetProcessName(pid, imagePath);

            return new LockingProcess
            {
                Pid = pid,
                Name = name ?? string.Empty,
                ExecutablePath = imagePath ?? string.Empty,
                ServiceName = string.Empty,
                UserName = string.Empty,
                Source = source,
                IsCritical = IsCriticalProcess(pid, name)
            };
        }

        internal static bool IsCriticalProcess(int pid, string name)
        {
            if (pid == 0 || pid == 4)
            {
                return true;
            }

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string normalized = name.Trim();
            if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                normalized += ".exe";
            }

            return CriticalNames.Contains(normalized);
        }

        internal static bool IsSameOrUnder(string candidate, string target, bool targetIsDirectory)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(target))
            {
                return false;
            }

            var comparison = new PathTarget(target, targetIsDirectory);
            return comparison.Matches(candidate);
        }
    }

    /// <summary>目标路径匹配器（大小写不敏感；文件夹匹配其自身及所有子路径）。</summary>
    internal sealed class PathTarget
    {
        internal string Target { get; private set; }
        internal bool IsDirectory { get; private set; }

        private readonly string _prefix;

        internal PathTarget(string path, bool isDirectory)
        {
            Target = ProcessUtil.NormalizePath(path);
            IsDirectory = isDirectory;
            _prefix = Target.EndsWith("\\", StringComparison.Ordinal)
                ? Target
                : Target + "\\";
        }

        internal bool Matches(string candidate)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(Target))
            {
                return false;
            }

            string normalized = ProcessUtil.StripPathPrefix(candidate.Trim());
            if (normalized.Length > 3)
            {
                normalized = normalized.TrimEnd('\\', '/');
            }

            if (string.Equals(normalized, Target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsDirectory &&
                   normalized.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 占用检测编排：Restart Manager + NT 句柄扫描 + 模块扫描，结果去重合并。
    /// </summary>
    public static class LockFinder
    {
        public static ScanResult Scan(
            string path,
            bool deepScan,
            IProgress<string> progress,
            CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("路径不能为空。", "path");
            }

            string targetPath = ProcessUtil.NormalizePath(path);
            bool isDirectory;

            if (Directory.Exists(targetPath))
            {
                isDirectory = true;
            }
            else if (File.Exists(targetPath))
            {
                isDirectory = false;
            }
            else
            {
                throw CreateNotFoundException(targetPath);
            }

            var result = new ScanResult
            {
                TargetPath = targetPath,
                IsDirectory = isDirectory
            };

            int selfPid;
            using (Process self = Process.GetCurrentProcess())
            {
                selfPid = self.Id;
            }

            ProcessUtil.EnableDebugPrivilege();

            var merged = new Dictionary<int, LockingProcess>();

            // 1. 文件枚举：文件夹递归展开，文件场景只有一个目标。
            List<string> files;
            if (isDirectory)
            {
                files = EnumerateFiles(targetPath, result, progress, ct);
                result.ScannedFiles = files.Count;
                Report(progress, string.Format(
                    "已找到 {0} 个文件，开始检测占用...", files.Count));
            }
            else
            {
                files = new List<string> { targetPath };
                result.ScannedFiles = 1;
                Report(progress, "正在检测文件占用...");
            }

            ct.ThrowIfCancellationRequested();

            // 2. Restart Manager：文件直接注册，文件夹使用分批注册。
            if (files.Count > 0)
            {
                try
                {
                    foreach (LockingProcess item in RestartManager.FindLockers(
                        files, result.Warnings, progress, ct))
                    {
                        Merge(merged, item, selfPid);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AddWarning(result, "重启管理器检测失败：" + ex.Message);
                }
            }

            // 3. NT 句柄深度扫描（文件、文件夹、文件夹内所有文件及根目录自身句柄）。
            if (deepScan)
            {
                ct.ThrowIfCancellationRequested();
                Report(progress, isDirectory
                    ? "正在深度扫描句柄（文件夹及子文件）..."
                    : "正在深度扫描句柄...");

                try
                {
                    foreach (LockingProcess item in HandleScanner.Scan(
                        targetPath, isDirectory, result.Warnings, progress, ct))
                    {
                        Merge(merged, item, selfPid);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AddWarning(result, "句柄扫描失败：" + ex.Message);
                }
            }

            // 4. 模块扫描：文件夹场景下，exe 或已加载模块位于目标文件夹的进程也视为占用。
            if (isDirectory)
            {
                ct.ThrowIfCancellationRequested();
                Report(progress, "正在扫描进程模块路径...");

                try
                {
                    foreach (LockingProcess item in ModuleScan(
                        targetPath, result, progress, ct))
                    {
                        Merge(merged, item, selfPid);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AddWarning(result, "模块扫描失败：" + ex.Message);
                }
            }

            // 5. 补全路径、名称与关键进程标记。
            foreach (LockingProcess item in merged.Values)
            {
                if (string.IsNullOrEmpty(item.ExecutablePath))
                {
                    string imagePath = ProcessUtil.GetProcessImagePath(item.Pid);
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        item.ExecutablePath = imagePath;
                    }
                }

                if (string.IsNullOrEmpty(item.Name) ||
                    item.Name.StartsWith("PID ", StringComparison.Ordinal))
                {
                    item.Name = ProcessUtil.GetProcessName(item.Pid, item.ExecutablePath);
                }

                if (ProcessUtil.IsCriticalProcess(item.Pid, item.Name))
                {
                    item.IsCritical = true;
                }
            }

            result.Processes = merged.Values
                .OrderBy(p => p.IsCritical ? 1 : 0)
                .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(p => p.Pid)
                .ToList();

            result.ElapsedMs = stopwatch.ElapsedMilliseconds;

            Report(progress, string.Format(
                "扫描完成：{0} 个文件，{1} 个占用进程，用时 {2} 毫秒",
                result.ScannedFiles,
                result.Processes.Count,
                result.ElapsedMs));

            return result;
        }

        private static Exception CreateNotFoundException(string path)
        {
            bool directoryLike = path.EndsWith("\\", StringComparison.Ordinal) ||
                                 path.EndsWith("/", StringComparison.Ordinal) ||
                                 !Path.HasExtension(path);

            if (directoryLike)
            {
                return new DirectoryNotFoundException("文件夹不存在：" + path);
            }

            return new FileNotFoundException("文件不存在：" + path, path);
        }

        /// <summary>用显式栈递归枚举文件，跳过符号链接/联接点，避免死循环。</summary>
        private static List<string> EnumerateFiles(
            string root,
            ScanResult result,
            IProgress<string> progress,
            CancellationToken ct)
        {
            var files = new List<string>();
            var stack = new Stack<string>();
            stack.Push(root);
            int lastReportedCount = 0;

            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                string directory = stack.Pop();
                DirectoryInfo info;
                try
                {
                    info = new DirectoryInfo(directory);
                }
                catch (Exception ex)
                {
                    AddWarning(result, "无法读取文件夹 " + directory + "：" + ex.Message);
                    continue;
                }

                try
                {
                    foreach (FileInfo file in info.EnumerateFiles())
                    {
                        files.Add(file.FullName);

                        if ((files.Count & 0x3FF) == 0)
                        {
                            ct.ThrowIfCancellationRequested();
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    AddWarning(result, "无权限访问文件夹：" + directory);
                }
                catch (DirectoryNotFoundException)
                {
                    AddWarning(result, "文件夹已不存在：" + directory);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AddWarning(result, "枚举文件失败 " + directory + "：" + ex.Message);
                }

                try
                {
                    foreach (DirectoryInfo subDirectory in info.EnumerateDirectories())
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            if ((subDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                AddWarning(result,
                                    "已跳过符号链接/联接点：" + subDirectory.FullName);
                                continue;
                            }
                        }
                        catch
                        {
                            // 无法读取属性时保守跳过，避免潜在死循环。
                            continue;
                        }

                        stack.Push(subDirectory.FullName);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    AddWarning(result, "无权限访问文件夹：" + directory);
                }
                catch (DirectoryNotFoundException)
                {
                    AddWarning(result, "文件夹已不存在：" + directory);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AddWarning(result, "枚举子文件夹失败 " + directory + "：" + ex.Message);
                }

                if (progress != null && files.Count - lastReportedCount >= 500)
                {
                    lastReportedCount = files.Count;
                    Report(progress, string.Format(
                        "正在枚举文件... 已找到 {0:N0} 个", files.Count));
                }
            }

            return files;
        }

        /// <summary>遍历进程主模块与已加载模块，匹配目标文件夹。</summary>
        private static List<LockingProcess> ModuleScan(
            string folder,
            ScanResult result,
            IProgress<string> progress,
            CancellationToken ct)
        {
            var list = new List<LockingProcess>();
            var target = new PathTarget(folder, true);

            int selfPid;
            using (Process self = Process.GetCurrentProcess())
            {
                selfPid = self.Id;
            }

            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (Exception ex)
            {
                AddWarning(result, "无法枚举进程列表：" + ex.Message);
                return list;
            }

            try
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    Process process = processes[i];
                    if (process == null)
                    {
                        continue;
                    }

                    int pid;
                    try
                    {
                        pid = process.Id;
                    }
                    catch
                    {
                        continue;
                    }

                    if (pid <= 4 || pid == selfPid)
                    {
                        continue;
                    }

                    bool matched = false;
                    try
                    {
                        // 主模块；部分受保护进程会抛出异常，此时退回镜像路径查询。
                        try
                        {
                            ProcessModule main = process.MainModule;
                            if (main != null && target.Matches(main.FileName))
                            {
                                matched = true;
                            }
                        }
                        catch
                        {
                            if (target.Matches(ProcessUtil.GetProcessImagePath(pid)))
                            {
                                matched = true;
                            }
                        }

                        if (!matched)
                        {
                            try
                            {
                                foreach (ProcessModule module in process.Modules)
                                {
                                    if (module == null ||
                                        string.IsNullOrEmpty(module.FileName))
                                    {
                                        continue;
                                    }

                                    if (target.Matches(module.FileName))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }
                            }
                            catch
                            {
                                // 访问拒绝或进程已退出：静默跳过。
                            }
                        }
                    }
                    catch
                    {
                        // 单个进程失败不影响整体扫描。
                    }
                    finally
                    {
                        try
                        {
                            process.Dispose();
                        }
                        catch
                        {
                        }
                    }

                    if (matched)
                    {
                        try
                        {
                            list.Add(ProcessUtil.CreateLockingProcess(pid, LockSource.ModuleScan));
                        }
                        catch
                        {
                            // 进程可能刚好退出，忽略。
                        }
                    }

                    if (progress != null &&
                        (i % 20 == 19 || i == processes.Length - 1))
                    {
                        Report(progress, string.Format(
                            "模块扫描 {0}/{1}...", i + 1, processes.Length));
                    }
                }
            }
            finally
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    if (processes[i] != null)
                    {
                        try
                        {
                            processes[i].Dispose();
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return list;
        }

        private static void Merge(
            Dictionary<int, LockingProcess> map, LockingProcess item, int selfPid)
        {
            if (item == null || item.Pid <= 0 || item.Pid == selfPid)
            {
                return;
            }

            LockingProcess existing;
            if (!map.TryGetValue(item.Pid, out existing))
            {
                map[item.Pid] = item;
                return;
            }

            // 名称：优先保留带 .exe 的完整名称，避免 "PID 123" 覆盖真实名称。
            if ((string.IsNullOrEmpty(existing.Name) ||
                 existing.Name.StartsWith("PID ", StringComparison.Ordinal)) &&
                !string.IsNullOrEmpty(item.Name))
            {
                existing.Name = item.Name;
            }

            if (string.IsNullOrEmpty(existing.ExecutablePath) &&
                !string.IsNullOrEmpty(item.ExecutablePath))
            {
                existing.ExecutablePath = item.ExecutablePath;
            }
            if (string.IsNullOrEmpty(existing.ServiceName) &&
                !string.IsNullOrEmpty(item.ServiceName))
            {
                existing.ServiceName = item.ServiceName;
            }
            if (string.IsNullOrEmpty(existing.UserName) &&
                !string.IsNullOrEmpty(item.UserName))
            {
                existing.UserName = item.UserName;
            }

            // 句柄扫描是直接证据，优先级最高；模块扫描次之；系统检测最低。
            if (SourceRank(item.Source) > SourceRank(existing.Source))
            {
                existing.Source = item.Source;
            }

            existing.IsCritical = existing.IsCritical || item.IsCritical;
        }

        private static int SourceRank(LockSource source)
        {
            switch (source)
            {
                case LockSource.HandleScan:
                    return 2;
                case LockSource.ModuleScan:
                    return 1;
                default:
                    return 0;
            }
        }

        private static void AddWarning(ScanResult result, string message)
        {
            const int maxWarnings = 200;

            if (result.Warnings.Count < maxWarnings)
            {
                result.Warnings.Add(message);
            }
            else if (result.Warnings.Count == maxWarnings)
            {
                result.Warnings.Add("警告过多，后续警告已省略。");
            }
        }

        private static void Report(IProgress<string> progress, string message)
        {
            if (progress == null)
            {
                return;
            }

            try
            {
                progress.Report(message);
            }
            catch
            {
                // UI 已关闭等情况下忽略进度回调异常。
            }
        }
    }
}
