# Changelog / 更新日志

## v1.0.1 — 2026-10-23

Maintenance release.

- Keep the Windows uninstall entry name in sync when you switch the interface language.
- Rebuild the installer so its file version matches the application (`1.0.1`).

维护版本。切换界面语言时，同步更新“设置 → 应用”中的卸载项名称；同时重新构建安装包，使安装器版本与主程序一致（`1.0.1`）。

## v1.0.0 — 2026-10-22

First tagged release of NoOccupy for Windows 10/11 (64-bit).

- Detect file and folder locks using Restart Manager, NT handle scanning, and module scanning.
- Review locking processes, select which to end, and confirm in the graphical interface.
- Install Explorer menus, a Start menu shortcut, and an uninstaller through a single EXE.
- Use English, Simplified Chinese, Japanese, or Korean throughout the app, installer, and dialogs.
- Automate scans and process termination with JSON output and documented exit codes.
- Download `NoOccupy-Setup.exe` and `SHA256SUMS.txt` from GitHub Releases.

NoOccupy 首个带版本标签的发布，适用于 Windows 10/11（64 位）。

- 通过 Restart Manager、NT 句柄扫描和模块扫描检测文件与文件夹占用。
- 查看占用进程，在图形界面中选择并确认结束。
- 单文件 EXE 安装器，集成右键菜单、开始菜单快捷方式和卸载程序。
- 主程序、安装器和对话框支持中文、英文、日文、韩文。
- 命令行支持扫描、结束进程、JSON 输出和退出码。
- GitHub Release 提供 `NoOccupy-Setup.exe` 和 `SHA256SUMS.txt` 下载。
