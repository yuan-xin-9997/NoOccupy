# 安装 / 卸载验收报告

- 日期：2026-10-22
- 系统：Windows 11 家庭中文版，Build 26300
- 运行时：.NET Framework 4.8.1（4.8.10008）
- 显示：3840 × 2160，150% 缩放（DPI 144）
- 权限：管理员会话

## 1. 构建产物

| 项目 | 值 |
| --- | --- |
| 安装包 | `E:\dev\NoOccupy\NoOccupy-Setup.exe` |
| 大小 | 193,536 字节（189.0 KB） |
| SHA256 | `AF34B1259C0F524E730B71CA6654BC382B3422282365CBA04EBC68078C74CFB9` |
| 主程序 | 116,736 字节 |
| 安装包内卸载器 | 与安装包同一文件，复制为 `NoOccupyUninstall.exe` |

最终 SHA256：

```text
AF34B1259C0F524E730B71CA6654BC382B3422282365CBA04EBC68078C74CFB9
```

主程序 SHA256：

```text
25E3620BB36E0418ECF19731A2F3DFFDC97EA7403F36497B6284899970F80E8E
```

## 2. 安装器界面（150% DPI）

- 窗口实际尺寸 1162 × 836，布局完整，没有按钮或中文被截断。
- 默认安装目录为 `C:\Program Files\NoOccupy`。
- 安装按钮、取消按钮、路径输入框、说明文字和内容卡片均正常显示。
- 截图：`tests/setup-ui-new.png`

修复记录：安装器原先使用像素字体，手动调用 `Scale()` 只缩放控件边界、不缩放字体，导致 150% 下文字偏小、信息卡片与页脚重叠。现在的实现会在缩放边界后同步按比例缩放字体，并限制说明文字宽度、压缩卡片高度。

## 3. 图形界面安装

使用 `tests/setup-ui-install.ps1` 启动安装器、点击“安装”、关闭成功提示：

- 安装器进程退出码：`0`
- 安装成功提示出现并正常关闭
- 安装结果：`installed=True`

## 4. 静默安装与注册表验收

执行：

```powershell
NoOccupy-Setup.exe /S
```

退出码 `0`。默认安装目录内容：

```text
NoOccupy.exe           116,736 字节
NoOccupy.exe.config        414 字节
NoOccupyUninstall.exe  193,536 字节
```

已安装并验证：

- 右键菜单键 `HKLM\Software\Classes\*\shell\NoOccupy`
  - 默认值：`解除占用`
  - `Icon`：`"C:\Program Files\NoOccupy\NoOccupy.exe",0`
  - `Position`：`top`
  - `MultiSelectModel`：`Single`
  - `command`：`"C:\Program Files\NoOccupy\NoOccupy.exe" "%1"`
- 文件夹菜单键 `HKLM\Software\Classes\Directory\shell\NoOccupy`，command 同上
- 文件夹空白处菜单键 `HKLM\Software\Classes\Directory\Background\shell\NoOccupy`，command 使用 `"%V"`
- 开始菜单快捷方式：`C:\ProgramData\Microsoft\Windows\Start Menu\Programs\解除占用.lnk`
- 系统卸载项：`HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\NoOccupy`
  - 显示名称：`解除占用 (NoOccupy)`
  - 版本：`1.0.0`
  - `UninstallString`：`"...\NoOccupyUninstall.exe" /uninstall`
  - `QuietUninstallString`：`"...\NoOccupyUninstall.exe" /S /uninstall`
  - `EstimatedSize`：约 300 KB

静默安装默认不创建桌面快捷方式，与安装器默认状态一致。

## 5. 主程序功能验收

用 PowerShell 进程以独占方式锁定测试文件，再启动已安装的主程序：

```powershell
"C:\Program Files\NoOccupy\NoOccupy.exe" "E:\...\locked-final.txt"
```

结果：

- 界面显示“发现 1 个占用进程”
- 进程名：`powershell.exe`
- PID：`30400`
- 来源：`句柄扫描`
- 程序路径列显示 `C:\Windows\System32\WindowsPowerShell\v1.0\...`
- 截图：`tests/ui-installed-final.png`

命令行复验（安装目录中的 exe）：

```text
scanExit=0
count=1
pid=31044
name=powershell.exe
source=HandleScan
```

图形界面结束进程验收（`tests/ui-kill6.ps1`）：

- 鼠标勾选进程行
- 点击“结束选中进程”
- 确认框选择“是”
- 占用进程 `powershell.exe` 被结束
- 结果提示：`成功 1 个 / 失败 0 个`
- 随后测试文件删除成功：`delete=OK`

## 6. 卸载验收

执行安装目录中的：

```powershell
"C:\Program Files\NoOccupy\NoOccupyUninstall.exe" /S /uninstall
```

- 卸载器退出码：`0`
- 原安装器把自身复制到 `%TEMP%\NoOccupyUninstall_<pid>.exe`
- 临时卸载器删除安装目录后，再通过延迟命令删除自身

另验证了直接执行 `NoOccupyUninstall.exe /S`（不带 `/uninstall`）也能正确卸载：程序会根据文件名自动进入卸载流程。直接双击该文件时会显示卸载确认框。

验收结果：

| 检查项 | 结果 |
| --- | --- |
| `C:\Program Files\NoOccupy` | 已删除 |
| `HKLM\...\*\shell\NoOccupy` | 已删除 |
| `HKLM\...\Directory\shell\NoOccupy` | 已删除 |
| `HKLM\...\Directory\Background\shell\NoOccupy` | 已删除 |
| ARP 卸载项 | 已删除 |
| 开始菜单快捷方式 | 已删除 |
| `%TEMP%\NoOccupyUninstall_*.exe` | 无残留 |

## 7. 最终状态

卸载完成后再次执行 `NoOccupy-Setup.exe /S`：

- 退出码 `0`
- 程序重新安装到默认目录
- 右键菜单、开始菜单、ARP 卸载项均已恢复
- 最终保持“已安装”状态

最终安装包位于：

```text
E:\dev\NoOccupy\NoOccupy-Setup.exe
```

## 8. 已知限制

Windows 11 默认右键菜单只展示打包应用（MSIX/COM）注册的项。本项目使用兼容 Windows 10/11 的 `HKLM\Software\Classes\...\shell` 传统注册方式，因此在 Windows 11 上需要点击“显示更多选项”，或直接按 `Shift + F10`。Windows 10 上会直接出现在右键菜单中。

这是 Windows 11 的菜单机制限制；若必须显示在一级菜单，需要额外制作 MSIX 包并实现 `IExplorerCommand`，会显著增加安装包体积和签名/部署复杂度。

## 9. 测试脚本

- `tests/setup-ui-install.ps1`：启动安装器、点击安装、关闭提示并检查结果
- `tests/ui-kill6.ps1`：勾选进程、点击结束、确认并检查文件是否可删除
- `tests/lock-helper.ps1`：在后台以独占方式锁定测试文件
- `tests/capture-screen.ps1`：按窗口矩形截图（DPI 感知）
- `tests/capture-window.ps1`：PrintWindow 截图备用

测试脚本按本机 150% DPI 的控件坐标编写，换机器运行前需要重新校准坐标。
