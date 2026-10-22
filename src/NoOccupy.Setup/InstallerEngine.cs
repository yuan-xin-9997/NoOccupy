using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace NoOccupy.Setup
{
    internal sealed class SetupOptions
    {
        public bool Silent;
        public bool Uninstall;
        public bool FromTemp;
        public bool CreateDesktop;
        public string InstallDir;

        public static SetupOptions Parse(string[] args)
        {
            var options = new SetupOptions();
            if (args == null)
            {
                return options;
            }

            foreach (string raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string arg = raw.Trim();
                if (arg.Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/SILENT", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                {
                    options.Silent = true;
                }
                else if (arg.Equals("/UNINSTALL", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    options.Uninstall = true;
                }
                else if (arg.Equals("/FROM-TEMP", StringComparison.OrdinalIgnoreCase))
                {
                    options.FromTemp = true;
                }
                else if (arg.Equals("/DESKTOP", StringComparison.OrdinalIgnoreCase))
                {
                    options.CreateDesktop = true;
                }
                else if (arg.StartsWith("/D=", StringComparison.OrdinalIgnoreCase))
                {
                    options.InstallDir = arg.Substring(3).Trim('"');
                }
                else if (arg.StartsWith("/DIR=", StringComparison.OrdinalIgnoreCase))
                {
                    options.InstallDir = arg.Substring(5).Trim('"');
                }
            }

            return options;
        }
    }

    internal sealed class InstallException : Exception
    {
        public InstallException(string message, Exception inner) : base(message, inner) { }
    }

    internal static class InstallerEngine
    {
        public static string ProductVersion
        {
            get { return typeof(InstallerEngine).Assembly.GetName().Version.ToString(3); }
        }
        private const string AppExeName = "NoOccupy.exe";
        private const string UninstallerName = "NoOccupyUninstall.exe";
        private const string ShortcutName = "NoOccupy.lnk";
        private const string LegacyShortcutName = "解除占用.lnk";
        private const string ArpKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\NoOccupy";

        private static readonly string[] MenuKeys =
        {
            @"Software\Classes\*\shell\NoOccupy",
            @"Software\Classes\Directory\shell\NoOccupy",
            @"Software\Classes\Directory\Background\shell\NoOccupy"
        };

        private static readonly object LogLock = new object();

        public static string LogPath
        {
            get { return Path.Combine(Path.GetTempPath(), "NoOccupy-setup.log"); }
        }

        public static string DefaultInstallDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "NoOccupy");
            }
        }

        public static void Log(string message)
        {
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(
                        LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // 日志失败不影响安装。
            }
        }

        public static string NormalizeDir(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = DefaultInstallDir;
            }

            dir = Environment.ExpandEnvironmentVariables(dir.Trim().Trim('"'));
            dir = Path.GetFullPath(dir);

            string root = Path.GetPathRoot(dir);
            if (!string.IsNullOrEmpty(root) &&
                string.Equals(dir.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(S.T("安装目录不能是磁盘根目录。"));
            }

            return dir.TrimEnd('\\');
        }

        public static void Install(string requestedDir, bool createDesktop)
        {
            string dir = NormalizeDir(requestedDir);
            bool dirExisted = Directory.Exists(dir);
            Log("安装开始：dir=" + dir + " desktop=" + createDesktop + " existed=" + dirExisted);

            try
            {
                Directory.CreateDirectory(dir);

                StopRunningApp();

                ExtractResource("NoOccupy.exe", Path.Combine(dir, AppExeName));
                ExtractResource("NoOccupy.exe.config", Path.Combine(dir, "NoOccupy.exe.config"));

                string exePath = Path.Combine(dir, AppExeName);
                if (!File.Exists(exePath) || new FileInfo(exePath).Length < 20000)
                {
                    throw new InvalidOperationException(S.T("主程序释放失败，安装包可能不完整。"));
                }

                CopyUninstaller(dir);

                CreateMenuRegistry(dir);
                CreateShortcuts(dir, createDesktop);
                CreateArpEntry(dir);

                Log("安装完成：" + dir);
            }
            catch (Exception ex)
            {
                Log("安装失败：" + ex);
                TryRollback(dir, dirExisted);
                throw new InstallException(S.T("安装失败：") + ex.Message, ex);
            }
        }

        public static void Uninstall(string requestedDir, bool fromTemp)
        {
            string dir = ResolveInstallDir(requestedDir);
            Log("卸载开始：dir=" + dir + " fromTemp=" + fromTemp);

            StopRunningApp();

            RemoveMenuRegistry();
            RemoveArpEntry();
            RemoveShortcuts();

            string self = Path.GetFullPath(Application.ExecutablePath);
            bool selfInsideDir = IsInsideDirectory(self, dir);

            if (selfInsideDir && !fromTemp)
            {
                string tempExe = Path.Combine(
                    Path.GetTempPath(),
                    "NoOccupyUninstall_" + Process.GetCurrentProcess().Id + ".exe");

                try
                {
                    File.Copy(self, tempExe, true);
                    var psi = new ProcessStartInfo
                    {
                        FileName = tempExe,
                        Arguments = "/uninstall /S /from-temp /dir \"" + dir + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi);
                    Log("已启动临时卸载进程：" + tempExe);
                }
                catch (Exception ex)
                {
                    Log("启动临时卸载进程失败：" + ex);
                    throw new InstallException(S.T("无法完成卸载：") + ex.Message, ex);
                }

                return;
            }

            // 延迟删除安装目录与临时卸载器，避免文件被当前进程占用。
            var cleanup = new StringBuilder();
            cleanup.Append("/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"").Append(dir).Append("\"");
            if (selfInsideDir || fromTemp)
            {
                cleanup.Append(" & del /f /q \"").Append(self).Append("\"");
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cleanup.ToString(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                Log("已计划删除目录：" + dir);
            }
            catch (Exception ex)
            {
                Log("计划删除失败：" + ex);
                throw new InstallException(S.T("卸载信息已移除，但安装目录删除失败：") + ex.Message, ex);
            }
        }

        public static string ResolveInstallDir(string requestedDir)
        {
            if (!string.IsNullOrWhiteSpace(requestedDir))
            {
                return NormalizeDir(requestedDir);
            }

            string exe = Path.GetFullPath(Application.ExecutablePath);
            if (string.Equals(Path.GetFileName(exe), UninstallerName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(exe);
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(ArpKeyPath))
                {
                    if (key != null)
                    {
                        string location = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(location))
                        {
                            return NormalizeDir(location);
                        }
                    }
                }
            }
            catch
            {
            }

            return DefaultInstallDir;
        }

        private static void StopRunningApp()
        {
            foreach (Process process in Process.GetProcessesByName("NoOccupy"))
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id)
                    {
                        continue;
                    }

                    Log("结束正在运行的主程序：PID " + process.Id);
                    if (!process.CloseMainWindow() || !process.WaitForExit(1500))
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                }
                catch (Exception ex)
                {
                    Log("结束进程失败 PID " + SafePid(process) + "：" + ex.Message);
                }
            }
        }

        private static int SafePid(Process process)
        {
            try { return process.Id; } catch { return -1; }
        }

        private static void ExtractResource(string resourceName, string targetPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("." + resourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        stream = assembly.GetManifestResourceStream(name);
                        break;
                    }
                }
            }

            if (stream == null)
            {
                throw new InvalidOperationException(S.T("安装包内缺少资源：") + resourceName);
            }

            using (stream)
            using (var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(output);
            }

            Log("释放资源：" + resourceName + " -> " + targetPath + " (" + new FileInfo(targetPath).Length + " bytes)");
        }

        private static void CopyUninstaller(string dir)
        {
            string self = Path.GetFullPath(Application.ExecutablePath);
            string target = Path.Combine(dir, UninstallerName);
            if (string.Equals(self, target, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Copy(self, target, true);
            Log("复制卸载器：" + target);
        }

        private static void CreateMenuRegistry(string dir)
        {
            string exe = Path.Combine(dir, AppExeName);
            string icon = "\"" + exe + "\",0";

            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(MenuKeys[0]))
            {
                key.SetValue(null, S.MenuLabel);
                key.SetValue("Icon", icon);
                key.SetValue("Position", "top");
                key.SetValue("MultiSelectModel", "Single");
                using (RegistryKey command = key.CreateSubKey("command"))
                {
                    command.SetValue(null, "\"" + exe + "\" \"%1\"");
                }
            }

            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(MenuKeys[1]))
            {
                key.SetValue(null, S.MenuLabel);
                key.SetValue("Icon", icon);
                key.SetValue("Position", "top");
                key.SetValue("MultiSelectModel", "Single");
                using (RegistryKey command = key.CreateSubKey("command"))
                {
                    command.SetValue(null, "\"" + exe + "\" \"%1\"");
                }
            }

            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(MenuKeys[2]))
            {
                key.SetValue(null, S.MenuLabel);
                key.SetValue("Icon", icon);
                key.SetValue("Position", "top");
                key.SetValue("MultiSelectModel", "Single");
                using (RegistryKey command = key.CreateSubKey("command"))
                {
                    command.SetValue(null, "\"" + exe + "\" \"%V\"");
                }
            }

            Log("右键菜单已注册。");
        }

        private static void RemoveMenuRegistry()
        {
            foreach (string keyPath in MenuKeys)
            {
                try
                {
                    Registry.LocalMachine.DeleteSubKeyTree(keyPath, false);
                    Log("删除注册表：" + keyPath);
                }
                catch (Exception ex)
                {
                    Log("删除注册表失败 " + keyPath + "：" + ex.Message);
                }
            }
        }

        private static void CreateShortcuts(string dir, bool createDesktop)
        {
            string exe = Path.Combine(dir, AppExeName);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            if (!string.IsNullOrWhiteSpace(programs))
            {
                DeleteShortcut(programs);
                CreateShortcut(Path.Combine(programs, ShortcutName), exe, S.T("检测并结束占用文件的进程"));
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (createDesktop && !string.IsNullOrWhiteSpace(desktop))
            {
                DeleteShortcut(desktop);
                CreateShortcut(Path.Combine(desktop, ShortcutName), exe, S.T("检测并结束占用文件的进程"));
            }
            else
            {
                DeleteShortcut(desktop);
            }

            Log("快捷方式处理完成。");
        }

        private static void RemoveShortcuts()
        {
            DeleteShortcut(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
            DeleteShortcut(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
        }

        private static void DeleteShortcut(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            foreach (string name in new[] { ShortcutName, LegacyShortcutName })
            {
                try
                {
                    string path = Path.Combine(directory, name);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        Log("删除快捷方式：" + path);
                    }
                }
                catch (Exception ex)
                {
                    Log("删除快捷方式失败：" + ex.Message);
                }
            }
        }

        private static void CreateShortcut(string path, string target, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    throw new InvalidOperationException(S.T("系统未提供 WScript.Shell。"));
                }

                object shell = Activator.CreateInstance(shellType);
                try
                {
                    dynamic shortcut = shellType.InvokeMember(
                        "CreateShortcut",
                        BindingFlags.InvokeMethod,
                        null,
                        shell,
                        new object[] { path });

                    shortcut.TargetPath = target;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(target);
                    shortcut.IconLocation = target + ",0";
                    shortcut.Description = description;
                    shortcut.Save();

                    Marshal.FinalReleaseComObject(shortcut);
                }
                finally
                {
                    if (shell != null && Marshal.IsComObject(shell))
                    {
                        Marshal.FinalReleaseComObject(shell);
                    }
                }

                Log("创建快捷方式：" + path);
            }
            catch (Exception ex)
            {
                Log("创建快捷方式失败 " + path + "：" + ex);
                throw;
            }
        }

        private static void CreateArpEntry(string dir)
        {
            string exe = Path.Combine(dir, AppExeName);
            string uninstaller = Path.Combine(dir, UninstallerName);

            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(ArpKeyPath))
            {
                key.SetValue("DisplayName", S.MenuLabel + " (NoOccupy)");
                key.SetValue("DisplayVersion", ProductVersion);
                key.SetValue("Publisher", "NoOccupy");
                key.SetValue("DisplayIcon", exe + ",0");
                key.SetValue("InstallLocation", dir);
                key.SetValue("UninstallString", "\"" + uninstaller + "\" /uninstall");
                key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" /S /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("EstimatedSize", EstimateSizeKb(dir), RegistryValueKind.DWord);
            }

            Log("卸载信息已写入：" + ArpKeyPath);
        }

        private static void RemoveArpEntry()
        {
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(ArpKeyPath, false);
                Log("删除卸载信息：" + ArpKeyPath);
            }
            catch (Exception ex)
            {
                Log("删除卸载信息失败：" + ex.Message);
            }
        }

        private static int EstimateSizeKb(string dir)
        {
            long total = 0;
            try
            {
                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch
            {
            }

            return Math.Max(1, (int)(total / 1024));
        }

        private static bool IsInsideDirectory(string file, string dir)
        {
            try
            {
                string fullFile = Path.GetFullPath(file);
                string fullDir = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
                return fullFile.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void TryRollback(string dir, bool dirExisted)
        {
            try
            {
                RemoveMenuRegistry();
                RemoveArpEntry();
                RemoveShortcuts();

                if (!dirExisted && Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                    Log("已回滚安装目录：" + dir);
                }
            }
            catch (Exception ex)
            {
                Log("回滚失败：" + ex.Message);
            }
        }
    }
}
