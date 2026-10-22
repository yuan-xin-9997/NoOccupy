param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [Parameter(Mandatory=$true)][string]$OutPath
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NoOccupyCaptureScreen {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
"@
try { [NoOccupyCaptureScreen]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null } catch { }
try { [NoOccupyCaptureScreen]::SetProcessDPIAware() | Out-Null } catch { }
$p = Get-Process -Id $ProcessId -ErrorAction Stop
for ($i = 0; $i -lt 40 -and $p.MainWindowHandle -eq 0; $i++) {
    Start-Sleep -Milliseconds 250
    $p.Refresh()
}
$h = $p.MainWindowHandle
if ($h -eq 0) { throw "no main window for pid $ProcessId" }
[NoOccupyCaptureScreen]::ShowWindow($h, 9) | Out-Null
[NoOccupyCaptureScreen]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 1500
$rect = New-Object NoOccupyCaptureScreen+RECT
[NoOccupyCaptureScreen]::GetWindowRect($h, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$hh = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap($w, $hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
try {
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $hh)))
    $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $g.Dispose()
    $bmp.Dispose()
}
"captured=$OutPath size=${w}x${hh}"
