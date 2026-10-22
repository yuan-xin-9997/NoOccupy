# NoOccupy（解除占用）项目说明

## 目标

Windows 10/11 轻量工具：右键文件/文件夹 → “解除占用” → 列出占用进程（名称、PID、路径、来源等）→ 多选/全选 → 一键结束，随后即可删除、移动或重命名。提供单文件安装包，默认安装到 `C:\Program Files\NoOccupy`，最终产物放在仓库根目录。

## 技术选择

- C# / .NET Framework 4.8 / WinForms：Windows 10 1903+ 与 Windows 11 自带运行时，主程序体积约 114 KB。
- 文件占用：Restart Manager（`RmStartSession` / `RmRegisterResources` / `RmGetList`）。
- 文件夹占用：递归枚举并分批注册 Restart Manager；目录本身使用 NT 句柄扫描（`NtQuerySystemInformation` + `DuplicateHandle` + `GetFinalPathNameByHandle`）；补充进程模块路径扫描。
- 结束进程：`Process.Kill`；主程序 manifest 使用 `requireAdministrator`。
- 安装器：自制单文件 WinForms EXE，内嵌主程序；写 `HKLM\Software\Classes\...\shell` 右键菜单、开始菜单快捷方式、ARP 卸载项。
- DPI：150% 等非 100% 缩放下手动缩放窗体与字体，避免按钮/中文截断。
- 图标：蓝色文件夹 + 绿色开启挂锁，16–256 多尺寸 ICO。

## 目录

```text
src/NoOccupy/             主程序
  Core/Models.cs          数据模型与接口契约
  Core/LockFinder.cs      占用检测编排
  Core/RestartManager.cs  Restart Manager P/Invoke
  Core/HandleScanner.cs   NT 句柄深度扫描
  Core/ProcessKiller.cs   结束进程与关键进程保护
  Cli.cs                  命令行测试接口
  MainForm.cs             主界面
  Program.cs              入口
  app.manifest / app.ico
src/NoOccupy.Setup/       单文件安装器
assets/                   图标源文件、预览
scripts/build.ps1         一键构建并复制安装包到根目录
tests/                    验收脚本、截图、报告
docs/PLAN.md              本文件
NoOccupy-Setup.exe        最终安装包（构建产物）
```

## 接口

```csharp
namespace NoOccupy.Core {
  public enum LockSource { RestartManager, HandleScan, ModuleScan }
  public class LockingProcess {
    int Pid; string Name; string ExecutablePath; string ServiceName;
    string UserName; LockSource Source; bool IsCritical;
  }
  public class ScanResult {
    string TargetPath; bool IsDirectory; List<LockingProcess> Processes;
    List<string> Warnings; int ScannedFiles; long ElapsedMs;
  }
  public class KillOutcome { int Pid; string Name; bool Success; string Error; }
  public class KillResult { List<KillOutcome> Outcomes; int Succeeded; int Failed; }
  public static class LockFinder {
    static ScanResult Scan(string path, bool deepScan, IProgress<string> progress, CancellationToken ct);
  }
  public static class ProcessKiller {
    static KillResult Kill(IEnumerable<int> pids, IProgress<string> progress);
  }
}
```

## CLI（测试/自动化）

- `NoOccupy.exe --scan "<path>" [--no-deep] [--json] [--out file]`
- `NoOccupy.exe --kill "<path>" (--all | --pid 12,34) [--no-deep] [--json] [--out file]`
- JSON 字段：`ok,target,isDirectory,elapsedMs,scannedFiles,processes[{pid,name,path,service,source,critical}],warnings,killed[{pid,name,success,error}]`
- 退出码：0 成功；kill 部分/全部失败为 2。

## 安装器

- 默认目录：`C:\Program Files\NoOccupy`。
- 文件：`NoOccupy.exe`、`NoOccupy.exe.config`、`NoOccupyUninstall.exe`（安装器自身副本）。
- 注册：
  - `HKLM\Software\Classes\*\shell\NoOccupy`（`%1`）
  - `HKLM\Software\Classes\Directory\shell\NoOccupy`（`%1`）
  - `HKLM\Software\Classes\Directory\Background\shell\NoOccupy`（`%V`）
  - `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\NoOccupy`
- 开始菜单/可选桌面快捷方式“解除占用”。
- 支持 `/S`、`/D=路径`、`/uninstall`；卸载器复制到 `%TEMP%` 后延迟清理安装目录和自身。
- Windows 11 传统菜单项位于“显示更多选项”（或在桌面按 `Shift + F10`）；一级菜单需 MSIX/COM 打包，本项目不采用。

## 验收标准

1. 安装/卸载正常，默认目录正确；ARP、开始菜单、右键菜单全部清理。
2. Win11 右键文件/文件夹能打开 GUI，正确列出占用进程（PID/路径）。
3. 列表支持多选/全选，结束选中进程后目标文件可删除。
4. 150% DPI 下界面无截断；安装器与主程序均通过实机验证。

详细结果见 `tests/setup-report.md` 与 `tests/core-report.md`。
