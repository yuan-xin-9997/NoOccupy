# NoOccupy（解除占用）项目说明

## 目标

Windows 10/11 轻量工具：右键文件/文件夹 → “解除占用” → 列出占用进程（名称、PID、路径、来源等）→ 多选/全选 → 一键结束，随后即可删除、移动或重命名。提供单文件安装包，默认安装到 `C:\Program Files\NoOccupy`，最终产物放在仓库根目录。

界面支持中文、English、日本語、한국어，安装器和主程序均可在软件内切换语言。

## 技术选择

- C# / .NET Framework 4.8 / WinForms：Windows 10 1903+ 与 Windows 11 自带运行时，主程序体积约 143 KB。
- 文件占用：Restart Manager（`RmStartSession` / `RmRegisterResources` / `RmGetList`）。
- 文件夹占用：递归枚举并分批注册 Restart Manager；目录本身使用 NT 句柄扫描（`NtQuerySystemInformation` + `DuplicateHandle` + `GetFinalPathNameByHandle`）；补充进程模块路径扫描。
- 结束进程：`Process.Kill`；主程序 manifest 使用 `requireAdministrator`。
- 安装器：自制单文件 WinForms EXE，内嵌主程序；写 `HKLM\Software\Classes\...\shell` 右键菜单、开始菜单快捷方式、ARP 卸载项。
- 本地化：以中文原文为查询键，`en-US` / `ja-JP` / `ko-KR` 翻译表；缺失翻译自动回退中文。动态文案使用 `{0}` 占位符。
- 语言设置：保存在 `HKCU\Software\NoOccupy\Language`，主程序与安装器共用；无设置时按系统 UI 语言选择，非中/日/韩回退英文。
- 对话框：使用自绘 `UiDialog`，避免系统 `MessageBox` 的标准按钮跟随 Windows 显示语言。
- DPI：150% 等非 100% 缩放下手动缩放窗体与字体，避免按钮/文字截断。
- 图标：蓝色文件夹 + 绿色开启挂锁，16–256 多尺寸 ICO。

## 目录

```text
src/NoOccupy/             主程序
  Core/Models.cs          数据模型与接口契约
  Core/LockFinder.cs      占用检测编排
  Core/RestartManager.cs  Restart Manager P/Invoke
  Core/HandleScanner.cs   NT 句柄深度扫描
  Core/ProcessKiller.cs   结束进程与关键进程保护
  Localization.cs         主程序四语言文案与语言切换
  Cli.cs                  命令行测试接口（输出固定中文）
  MainForm.cs             主界面、语言下拉框
  Program.cs              入口
  app.manifest / app.ico
src/NoOccupy.Setup/       单文件安装器
  SetupLocalization.cs    安装器四语言文案
src/Shared/               主程序与安装器共享文件
  UiLanguage.cs           语言检测、HKCU 持久化、右键菜单更新
  UiDialog.cs             本地化确认框/提示框
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

## 本地化

- 语言枚举：`Chinese = 0`、`English = 1`、`Japanese = 2`、`Korean = 3`，同时用作下拉框索引。
- 文案键：主程序 `L.T("中文原文")`，安装器 `S.T("中文原文")`；`{0}` 等占位符在翻译中保留。
- 持久化：`HKCU\Software\NoOccupy\Language`，值 `zh-CN` / `en-US` / `ja-JP` / `ko-KR`。
- 系统检测：读取 `CultureInfo.CurrentUICulture`；中文、日文、韩文分别匹配，其余回退英文。
- 切换入口：
  - 主界面右上角“界面语言”下拉框；切换后刷新静态文案、列头、右键菜单、空状态/无效路径状态，并重新扫描当前目标。
  - 安装器顶部“界面语言”下拉框；切换后刷新说明、卡片、按钮和状态栏。
- 右键菜单与 ARP 显示名：三处 HKLM 菜单及卸载项显示名随语言更新；主程序切换时写入，安装器安装时按当前语言写入。ARP 格式为 `当前语言菜单名 + (NoOccupy)`，中文环境为 `解除占用 (NoOccupy)`。
- 对话框：`UiDialog` 提供本地化 Info / Warning / Error / Confirm；确认框默认选中“否/No”，避免误结束。
- 快捷方式：文件名固定 `NoOccupy.lnk`，描述在安装时按当前语言写入；安装/卸载会清理旧版 `解除占用.lnk`。

## CLI（测试/自动化）

- `NoOccupy.exe --scan "<path>" [--no-deep] [--json] [--out file]`
- `NoOccupy.exe --kill "<path>" (--all | --pid 12,34) [--no-deep] [--json] [--out file]`
- JSON 字段：`ok,target,isDirectory,elapsedMs,scannedFiles,processes[{pid,name,path,service,source,critical}],warnings,killed[{pid,name,success,error}]`
- 退出码：0 成功；kill 部分/全部失败为 2。
- 命令行输出固定中文，避免自动化脚本受界面语言影响。

## 安装器

- 默认目录：`C:\Program Files\NoOccupy`。
- 文件：`NoOccupy.exe`、`NoOccupy.exe.config`、`NoOccupyUninstall.exe`（安装器自身副本）。
- 语言：安装器顶部选择，写入 HKCU，主程序安装后沿用。
- 注册：
  - `HKLM\Software\Classes\*\shell\NoOccupy`（`%1`）
  - `HKLM\Software\Classes\Directory\shell\NoOccupy`（`%1`）
  - `HKLM\Software\Classes\Directory\Background\shell\NoOccupy`（`%V`）
  - `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\NoOccupy`
- 开始菜单/可选桌面快捷方式：`NoOccupy.lnk`（不再使用中文文件名）。
- 支持 `/S`、`/D=路径`、`/uninstall`；卸载器复制到 `%TEMP%` 后延迟清理安装目录和自身。
- 卸载确认框使用本地化对话框；`/S` 静默卸载不弹窗。
- Windows 11 传统菜单项位于“显示更多选项”（或在桌面按 `Shift + F10`）；一级菜单需 MSIX/COM 打包，本项目不采用。

## 验收标准

1. 安装/卸载正常，默认目录正确；ARP、开始菜单、三处右键菜单全部清理。
2. Win11 右键文件/文件夹能打开 GUI，正确列出占用进程（PID/路径）。
3. 列表支持多选/全选，结束选中进程后目标文件可删除。
4. 中文、英文、日文、韩文界面均可显示和切换；主界面、安装器、确认框/提示框无截断，按钮文字跟随软件语言。
5. 150% DPI 下界面无截断；安装器与主程序均通过实机验证。

详细结果见 `tests/setup-report.md` 与 `tests/core-report.md`。
