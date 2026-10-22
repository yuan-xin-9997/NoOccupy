param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [Parameter(Mandatory=$true)][int]$XBase,
    [Parameter(Mandatory=$true)][int]$YBase
)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NoOccupyClick {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
try { [NoOccupyClick]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null } catch { }
try { [NoOccupyClick]::SetProcessDPIAware() | Out-Null } catch { }
$p = Get-Process -Id $ProcessId -ErrorAction Stop
$h = $p.MainWindowHandle
if ($h -eq 0) { throw "no main window" }
[NoOccupyClick]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 250
$rect = New-Object NoOccupyClick+RECT
[NoOccupyClick]::GetWindowRect($h, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$scale = $w / 776.0
$x = $rect.Left + [int]($XBase * $scale)
$y = $rect.Top + [int]($YBase * $scale)
[NoOccupyClick]::SetCursorPos($x, $y) | Out-Null
[NoOccupyClick]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
[NoOccupyClick]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
"clicked=$x,$y scale=$scale"
