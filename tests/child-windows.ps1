param([Parameter(Mandatory=$true)][long]$Parent,[string]$ClickText,[long]$ClickHwnd=0)
Add-Type @"
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
public static class WinChild {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static string[] Get(IntPtr parent, string clickText) {
        var list = new List<string>();
        EnumChildWindows(parent, (h, l) => {
            var t = new StringBuilder(512); GetWindowText(h, t, 512);
            var c = new StringBuilder(512); GetClassName(h, c, 512);
            RECT r; GetWindowRect(h, out r);
            bool clicked = false;
            if (!string.IsNullOrEmpty(clickText) && t.ToString() == clickText && c.ToString().Contains("Button")) {
                SendMessage(h, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                clicked = true;
            }
            list.Add(string.Format("hwnd={0} class={1} text={2} rect={3},{4},{5},{6} clicked={7}", h, c, t, r.Left, r.Top, r.Right-r.Left, r.Bottom-r.Top, clicked));
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }
    public static void Click(IntPtr hWnd) {
        SendMessage(hWnd, 0x00F5, IntPtr.Zero, IntPtr.Zero);
    }
}
"@
[WinChild]::Get([IntPtr]$Parent, $ClickText)
if ($ClickHwnd -ne 0) { [WinChild]::Click([IntPtr]$ClickHwnd); "clickedHwnd=$ClickHwnd" }
