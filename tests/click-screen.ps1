param(
    [Parameter(Mandatory=$true)][int]$X,
    [Parameter(Mandatory=$true)][int]$Y
)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NoOccupyClickScreen {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
try { [NoOccupyClickScreen]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null } catch { }
try { [NoOccupyClickScreen]::SetProcessDPIAware() | Out-Null } catch { }
[NoOccupyClickScreen]::SetCursorPos($X, $Y) | Out-Null
[NoOccupyClickScreen]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
[NoOccupyClickScreen]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
"clicked=$X,$Y"
