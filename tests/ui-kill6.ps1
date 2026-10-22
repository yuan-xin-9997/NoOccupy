param(
    [Parameter(Mandatory=$true)][int]$AppPid,
    [Parameter(Mandatory=$true)][int]$HolderPid,
    [Parameter(Mandatory=$true)][string]$LockedFile
)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class NoOccupyUiTest {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    public const uint LEFTDOWN = 0x0002;
    public const uint LEFTUP = 0x0004;

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

    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }
}
"@
try { [NoOccupyUiTest]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null } catch { }
try { [NoOccupyUiTest]::SetProcessDPIAware() | Out-Null } catch { }

$app = Get-Process -Id $AppPid -ErrorAction Stop
$h = $app.MainWindowHandle
if ($h -eq 0) { throw 'app main window not found' }
[NoOccupyUiTest]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500

$rect = New-Object NoOccupyUiTest+RECT
[NoOccupyUiTest]::GetWindowRect($h, [ref]$rect) | Out-Null
"window=$($rect.Left),$($rect.Top) size=$($rect.Right-$rect.Left)x$($rect.Bottom-$rect.Top)"

# row checkbox and kill button at 150% DPI
$checkX = $rect.Left + 58
$checkY = $rect.Top + 377
$killX = $rect.Left + 1098
$killY = $rect.Top + 835

[NoOccupyUiTest]::Click($checkX, $checkY)
Start-Sleep -Milliseconds 600
"clicked-checkbox=$checkX,$checkY"

[NoOccupyUiTest]::Click($killX, $killY)
Start-Sleep -Milliseconds 1200
"clicked-kill=$killX,$killY"

$dialog = [NoOccupyUiTest]::FindDialog([uint32]$AppPid)
if ($dialog -eq [IntPtr]::Zero) { throw 'confirmation dialog not found' }
[NoOccupyUiTest]::SetForegroundWindow($dialog) | Out-Null
Start-Sleep -Milliseconds 500
[System.Windows.Forms.SendKeys]::SendWait('{LEFT}')
Start-Sleep -Milliseconds 150
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
"confirmed"

$deadline = (Get-Date).AddSeconds(20)
while ((Get-Date) -lt $deadline) {
    if (-not (Get-Process -Id $HolderPid -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Milliseconds 500
}
$holderAlive = [bool](Get-Process -Id $HolderPid -ErrorAction SilentlyContinue)
"holderAlive=$holderAlive"

if (-not $holderAlive) {
    try {
        Remove-Item -LiteralPath $LockedFile -Force -ErrorAction Stop
        "delete=OK"
    } catch {
        "delete=FAIL $($_.Exception.Message)"
    }
}
