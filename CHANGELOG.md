# Changelog / 更新日志

## v1.1.0 (preview) — 2026-11-08

Activation release.

- Require an offline activation code on first launch. Machine-bound codes are verified with an embedded ECDSA P-256 public key; no licensing server or network request is used.
- Add a four-language activation window with machine-code copy, license details, and an activation status link in the main window.
- Add an in-app purchase entry: the activation window button and the main-window **Buy** link open the store at <https://nooccupy.yuan-xin.top/>.
- Gate the GUI, core scanning/termination, and CLI behind a valid license; unlicensed use returns exit code `3`.
- Add `--activate`, `--license`, and `--deactivate` CLI commands.
- Add the vendor-only `tools/NoOccupy.LicenseTool` for key generation, issuance, inspection, verification, and self-test.
- Support silent installation with `/ACTIVATE="NOOC-..."`; the app activates on first launch. The license is kept across uninstall/reinstall.
- Document key custody, issuance, storage, security boundaries, and online-activation options in `docs/LICENSING.md`.

本次为激活授权版本。

- 首次启动必须输入离线激活码；单机码用内嵌 ECDSA P-256 公钥验证，不访问授权服务器。
- 新增四语言激活窗口、机器码复制、授权详情，以及主界面“已激活 · 版本”入口。
- 激活窗口和主界面新增“购买激活码/购买”入口，跳转到 <https://nooccupy.yuan-xin.top/>。
- 主界面、核心扫描/结束进程和命令行均校验激活状态；未激活返回退出码 `3`。
- 新增 `--activate`、`--license`、`--deactivate` 命令行命令。
- 新增销售方内部使用的 `tools/NoOccupy.LicenseTool`，支持生成密钥、签发、检查、验证和自检。
- 支持静默安装 `/ACTIVATE="NOOC-..."`，首次启动自动激活；卸载重装默认保留激活信息。
- 在 `docs/LICENSING.md` 中记录私钥保管、签发流程、存储位置、安全边界和在线激活扩展方案。

## v1.0.2 — 2026-10-23

- Stop same-application parent processes before locking helpers to prevent immediate respawning, including Thunder download components. The confirmation dialog lists the processes to end.
- Recheck locks before showing the result. The GUI warns about remaining locks or scan warnings; the CLI reports the post-operation scan and returns a nonzero exit code when verification is incomplete.
- Add a regression test that reproduces helper respawning and verifies a real rename after unlocking through the CLI. Manual acceptance passed.

- 修复迅雷等应用重新拉起占用组件的问题：先结束同一应用目录中的父进程，再结束占用组件，确认框列出将结束的进程。
- 结束后先复查再显示结果；仍有占用或扫描警告时明确提示，命令行返回复查结果和非零退出码。
- 新增自动重启占用组件的回归测试，通过软件解除后验证实际改名；人工验收通过。结束对应应用会中断下载及未保存任务。

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
