# 解除占用（NoOccupy）

一个轻量的 Windows 工具：右键点击被占用的文件或文件夹，查看占用它的进程，勾选后一键结束，随后即可正常删除、移动或重命名。

- 主程序：C# / .NET Framework 4.8 / WinForms，约 114 KB
- 安装包：单文件 EXE，约 189 KB
- 默认安装目录：`C:\Program Files\NoOccupy`
- 支持 Windows 10 / Windows 11（64 位）

## 安装

1. 双击根目录的 `NoOccupy-Setup.exe`。
2. 在 UAC 提示中允许管理员权限。
3. 确认安装目录（默认 `C:\Program Files\NoOccupy`），点击“安装”。
4. 安装完成后，右键菜单、开始菜单和“设置 → 应用”中会出现“解除占用”。

静默安装：

```powershell
NoOccupy-Setup.exe /S
NoOccupy-Setup.exe /S /D="D:\Tools\NoOccupy"
```

静默安装默认不创建桌面快捷方式；安装器界面中的“创建桌面快捷方式”复选框可手动开启。

## 使用

1. 右键点击文件或文件夹，选择“解除占用”。
2. 程序会自动检测占用进程，列出进程名称、PID、程序路径、检测来源等信息。
3. 勾选一个或多个进程（或点击“全选”），点击“结束选中进程”。
4. 确认后进程被结束，文件即可删除或移动。

> Windows 11：传统右键菜单项位于“显示更多选项”（Shift + F10 可直接打开旧版菜单）。这是 Windows 11 对非打包右键菜单的限制，不是程序故障。

支持检测：

- 文件被其他进程打开（Restart Manager）
- 文件夹内文件被占用（递归、分批注册）
- 目录本身被占用（NT 句柄扫描）
- 已加载模块占用（模块路径扫描）

为避免误杀，系统关键进程会被标记并跳过；结束前始终有确认提示。

## 卸载

任选一种：

- Windows“设置 → 应用 → 已安装的应用 → 解除占用 → 卸载”
- 控制面板“程序和功能”
- 开始菜单右键“解除占用”快捷方式 → 打开文件所在位置 → 运行 `NoOccupyUninstall.exe`

静默卸载：

```powershell
"C:\Program Files\NoOccupy\NoOccupyUninstall.exe" /S /uninstall
```

卸载会清理程序目录、右键菜单、开始菜单快捷方式和系统卸载信息。

## 命令行（测试/自动化）

主程序保留了一个隐藏的命令行接口，便于验收和批量排查：

```powershell
NoOccupy.exe --scan "C:\path\to\file" --json --out result.json
NoOccupy.exe --kill "C:\path\to\file" --all --json
NoOccupy.exe --kill "C:\path\to\file" --pid 1234,5678
NoOccupy.exe --version
NoOccupy.exe --help
```

退出码：`0` 成功，`2` 部分或全部结束失败。

## 构建

要求：.NET SDK 8+、.NET Framework 4.8 Developer Pack。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

脚本会构建主程序、将其嵌入安装器，并把最终安装包复制到仓库根目录：

```text
NoOccupy-Setup.exe
```

## 目录结构

```text
src/NoOccupy/             主程序
  Core/                   占用检测、RM / NT 句柄、结束进程
  MainForm.cs             主界面
  Cli.cs                  命令行测试接口
  app.manifest            requireAdministrator
src/NoOccupy.Setup/       单文件安装器（嵌入主程序）
assets/                   图标源文件与预览
scripts/build.ps1         一键构建
tests/                    验收脚本、截图与报告
docs/PLAN.md              设计与接口约定
```

## 说明

- 程序不联网、不写入用户数据、不驻留后台。
- 检测和结束进程需要管理员权限，因此启动时会触发 UAC。
- 受系统保护的进程可能无法结束，程序会显示失败原因。
- Windows 11 的一级右键菜单需要 MSIX/COM 打包；本项目采用兼容 Win10/11 的传统注册方式。
