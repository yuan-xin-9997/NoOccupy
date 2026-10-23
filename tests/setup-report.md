# 安装 / 卸载与四语言验收报告

- 日期：2026-10-22
- 系统：Windows 11 家庭中文版，Build 26300
- 运行时：.NET Framework 4.8.1（4.8.10008）
- 显示：3840 × 2160，150% 缩放（DPI 144）
- 权限：管理员会话

## 1. 构建产物

| 项目 | 值 |
| --- | --- |
| 安装包 | `E:\dev\NoOccupy\NoOccupy-Setup.exe` |
| 大小 | 240,128 字节（234.5 KB） |
| SHA256 | `A954CD0E39E74D0E47BF357F55CE67D2211E4FA9BC50D294C6A6E8FF77034FA3` |
| 主程序 | 146,944 字节 |
| 主程序 SHA256 | `ED02388C39F95D165E6B4A2E44830D7554DA4D81A16CD5D74D35C788942DF7A4` |
| 安装包内卸载器 | 与安装包同一文件，复制为 `NoOccupyUninstall.exe` |

最终安装目录中的 `NoOccupyUninstall.exe` 与根目录安装包 SHA256 一致，说明已安装的是当前构建产物。

## 2. 四语言主界面

主界面右上角新增“界面语言”下拉框，支持中文、English、日本語、한국어。切换后标题、副标题、按钮、列头、右键菜单、空状态/无效路径状态和结果提示立即刷新，并重新扫描当前目标。

| 语言 | 截图 | 验证内容 |
| --- | --- | --- |
| 中文 | `tests/l10n-zh.png` | 1 个占用进程，路径/来源/按钮完整 |
| English | `tests/l10n-en.png` | “Release Lock”，列头与底部按钮完整 |
| 日本語 | `tests/l10n-ja.png` | 路径按钮、来源列“ハンドルスキャン”完整 |
| 한국어 | `tests/l10n-ko.png` | 标题、状态、路径按钮、列头完整 |

150% DPI 下修复了两个实际截断问题：

- 路径行按钮加宽到 136 / 156，日文“ファイルを選択…”和“フォルダーを選択…”完整显示。
- “来源”列加宽到 120，日文“ハンドルスキャン”不再显示为“ハンドルス…”。

启动时焦点移到“选择文件”按钮，避免语言下拉框获得焦点后把当前语言显示成蓝色选中状态。

空状态和无效路径状态也按语言刷新；例如韩文空状态、日文无效路径均通过截图确认。

切换语言时，三处右键菜单和“设置 → 应用”中的 ARP 显示名也会同步更新。实测中文 → English → 中文，菜单与 ARP 名称分别变为 `Release Lock / Release Lock (NoOccupy)`，再恢复为 `解除占用 / 解除占用 (NoOccupy)`。

## 3. 安装器四语言

安装器顶部同样提供语言下拉框，安装器会读取/写入同一个 HKCU 设置，安装后的主程序直接沿用。

| 语言 | 截图 |
| --- | --- |
| 中文 | `tests/l10n-setup-zh.png` |
| English | `tests/l10n-setup-en.png` |
| 日本語 | `tests/l10n-setup-ja.png` |
| 한국어 | `tests/l10n-setup-ko.png` |

日文说明文字原先在 150% DPI 下换成三行，已缩短为：

```text
インストール後、ファイルとフォルダーの右クリックメニューに追加されます。
Windows 11 では「その他のオプションを表示」から開けます。
```

日文安装成功提示也使用自绘对话框，按钮为 `OK`，见 `tests/l10n-setup-success-ja.png`。

## 4. 自绘对话框

系统 `MessageBox` 的“是/否/确定”按钮跟随 Windows 显示语言，在中文系统上即使软件切换为英文/日文，按钮仍会显示中文。为消除这一问题，主程序和安装器改用共享的 `UiDialog`：

- Info / Warning / Error / Confirm 四种类型；
- 按钮文字来自当前语言的翻译表；
- 确认框默认选中“否/No”，避免误结束进程；
- 对话框居中于主窗口，并限制在屏幕工作区内。

验证截图：

- 中文结束进程确认框：`tests/l10n-dialog-zh.png`
- 中文操作完成提示：`tests/l10n-result-zh.png`
- 日文卸载确认框（“はい / いいえ”）：`tests/l10n-uninstall-ja.png`

## 5. 安装 / 卸载验收

### 5.1 英文安装

使用安装器英文界面安装到 `C:\Program Files\NoOccupy`：

- 三处右键菜单显示名均为 `Release Lock`；
- 开始菜单快捷方式：`NoOccupy.lnk`；
- 旧版 `解除占用.lnk` 被自动清理；
- ARP 显示名：`Release Lock (NoOccupy)`；
- 已安装主程序启动后显示英文界面。

### 5.2 日文卸载

使用新构建的卸载器，在 HKCU 为 `ja-JP` 时直接运行：

- 显示日文自绘确认框，按钮为“はい / いいえ”；
- 选择“はい”后卸载完成；
- 程序目录、三处右键菜单、ARP 卸载项和 `NoOccupy.lnk` 全部删除；
- 无临时卸载器残留。

### 5.3 最终中文安装

最后恢复 `HKCU\Software\NoOccupy\Language = zh-CN`，使用根目录 `NoOccupy-Setup.exe /S` 重新安装，保持“已安装”状态：

| 检查项 | 结果 |
| --- | --- |
| `C:\Program Files\NoOccupy\NoOccupy.exe` | 146,944 字节，哈希匹配当前构建 |
| `NoOccupyUninstall.exe` | 240,128 字节，哈希匹配根目录安装包 |
| `HKLM\...\*\shell\NoOccupy` | `解除占用` |
| `HKLM\...\Directory\shell\NoOccupy` | `解除占用` |
| `HKLM\...\Directory\Background\shell\NoOccupy` | `解除占用` |
| 开始菜单 | `NoOccupy.lnk` |
| ARP 显示名 | `解除占用 (NoOccupy)` |
| HKCU 语言 | `zh-CN` |

## 6. 主程序功能验收（最终中文安装）

用 PowerShell 进程以独占方式锁定测试文件，再启动已安装的主程序：

```powershell
"C:\Program Files\NoOccupy\NoOccupy.exe" "E:\...\locked.txt"
```

- 界面显示“发现 1 个占用进程”；
- 进程名：`powershell.exe`；
- PID：`83440`；
- 来源：`句柄扫描`；
- 勾选进程后点击“结束选中进程”；
- 自绘确认框显示中文，选择“是”；
- 结果提示：`成功 1 个 / 失败 0 个。`；
- 锁定进程被结束，状态变为“未发现占用进程，当前可以正常删除/移动”。

## 7. 已知限制

Windows 11 默认右键菜单只展示打包应用（MSIX/COM）注册的项。本项目使用兼容 Windows 10/11 的 `HKLM\Software\Classes\...\shell` 传统注册方式，因此在 Windows 11 上需要点击“显示更多选项”，或直接按 `Shift + F10`。Windows 10 上会直接出现在右键菜单中。

这是 Windows 11 的菜单机制限制；若必须显示在一级菜单，需要额外制作 MSIX 包并实现 `IExplorerCommand`，会显著增加安装包体积和签名/部署复杂度。

命令行接口的输出固定为中文，以保证自动化脚本稳定；图形界面、安装器和自绘对话框均支持四语言。

## 8. 测试脚本

- `tests/check-l10n.py`：检查主程序翻译表是否缺失/重复键。
- `tests/check-setup-l10n.py`：检查安装器翻译表是否缺失/重复键。
- `tests/switch-language.ps1`：通过界面切换语言并验证 HKCU 持久化。
- `tests/lock-helper.ps1`：在后台以独占方式锁定测试文件。
- `tests/capture-screen.ps1`：按窗口矩形截图（DPI 感知）。
- `tests/capture-window.ps1`：PrintWindow 截图备用。
- `tests/capture-uia-window.ps1`：按 UI Automation 窗口矩形截图。
- `tests/child-windows.ps1`、`tests/find-windows.ps1`：定位对话框/按钮句柄，用于自绘对话框验收。
- `tests/setup-ui-install.ps1`、`tests/ui-kill6.ps1`：旧版中文安装/结束进程自动化脚本。

测试脚本中的鼠标坐标按本机 150% DPI 校准，换机器运行前需要重新校准。
