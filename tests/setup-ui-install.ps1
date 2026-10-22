param(
    [Parameter(Mandatory=$true)][string]$SetupPath
)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class NoOccupySetupUi {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    public const uint LEFTDOWN = 0x0002;
    public const uint LEFTUP = 0x0004;
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
    public static IntPtr FindDialog(uint pid) {
        IntPtr result = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hwnd, IntPtr lParam) {
            uint owner;
            GetWindowThreadProcessId(hwnd, out owner);
            if (owner != pid) return true;
            var sb = new StringBuilder(128);
            GetClassName(hwnd, sb, sb.Capacity);
            if (sb.ToString() == "#32770") { result = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
"@
try { [NoOccupySetupUi]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null } catch { }
try { [NoOccupySetupUi]::SetProcessDPIAware() | Out-Null } catch { }

$p = Start-Process -FilePath $SetupPath -PassThru
for ($i = 0; $i -lt 40 -and $p.MainWindowHandle -eq 0; $i++) {
    Start-Sleep -Milliseconds 250
    $p.Refresh()
}
if ($p.MainWindowHandle -eq 0) { throw 'setup window not found' }
[NoOccupySetupUi]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 800
$rect = New-Object NoOccupySetupUi+RECT
[NoOccupySetupUi]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
$installX = $rect.Left + 849
$installY = $rect.Top + 757
[NoOccupySetupUi]::Click($installX, $installY)
Start-Sleep -Milliseconds 700
[NoOccupySetupUi]::Click($installX, $installY)
Start-Sleep -Milliseconds 500
[NoOccupySetupUi]::Click($installX, $installY)
"clicked-install=$installX,$installY rect=$($rect.Left),$($rect.Top)"

$dialog = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
    $dialog = [NoOccupySetupUi]::FindDialog([uint32]$p.Id)
    if ($dialog -ne [IntPtr]::Zero) { break }
    if ($p.HasExited) { break }
    Start-Sleep -Milliseconds 300
}
if ($dialog -ne [IntPtr]::Zero) {
    [NoOccupySetupUi]::SetForegroundWindow($dialog) | Out-Null
    Start-Sleep -Milliseconds 500
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    "dismissed-result"
}
if (-not $p.WaitForExit(15000)) {
    $p.Kill()
    "killed-after-timeout"
}
"setupExit=$($p.ExitCode)"
"installed=" + (Test-Path -LiteralPath 'C:\Program Files\NoOccupy\NoOccupy.exe')
