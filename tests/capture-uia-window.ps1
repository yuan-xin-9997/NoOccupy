param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [Parameter(Mandatory=$true)][string]$OutPath
)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NoOccupyScreenCapture {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
try { [NoOccupyScreenCapture]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null } catch { }
try { [NoOccupyScreenCapture]::SetProcessDPIAware() | Out-Null } catch { }
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
    $ProcessId)
$windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    $cond)
if ($windows.Count -eq 0) { throw "no window" }
$w = $windows[0]
$r = $w.Current.BoundingRectangle
$bmp = New-Object System.Drawing.Bitmap([int]$r.Width, [int]$r.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
try {
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
    $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $g.Dispose()
    $bmp.Dispose()
}
"captured=$OutPath rect=$r"
