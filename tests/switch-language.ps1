param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [int]$Index = -1,
    [int]$Down = 1
)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NoOccupyLangSwitch {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
}
"@
$p = Get-Process -Id $ProcessId -ErrorAction Stop
for ($i = 0; $i -lt 40 -and $p.MainWindowHandle -eq 0; $i++) {
    Start-Sleep -Milliseconds 250
    $p.Refresh()
}
$h = $p.MainWindowHandle
if ($h -eq 0) { throw "no main window" }
[NoOccupyLangSwitch]::ShowWindow($h, 9) | Out-Null
[NoOccupyLangSwitch]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 300
$rect = New-Object NoOccupyLangSwitch+RECT
[NoOccupyLangSwitch]::GetWindowRect($h, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$scale = $w / 836.0
$x = $rect.Right - [int](145 * $scale)
$y = $rect.Top + [int](78 * $scale)
[NoOccupyLangSwitch]::SetCursorPos($x, $y) | Out-Null
[NoOccupyLangSwitch]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
[NoOccupyLangSwitch]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 350
if ($Index -ge 0) {
    [System.Windows.Forms.SendKeys]::SendWait("{HOME}")
    Start-Sleep -Milliseconds 120
    for ($i = 0; $i -lt $Index; $i++) {
        [System.Windows.Forms.SendKeys]::SendWait("{DOWN}")
        Start-Sleep -Milliseconds 120
    }
}
else {
    for ($i = 0; $i -lt $Down; $i++) {
        [System.Windows.Forms.SendKeys]::SendWait("{DOWN}")
        Start-Sleep -Milliseconds 120
    }
}
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 1800
"clicked=$x,$y scale=$scale"
