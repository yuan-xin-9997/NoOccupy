param([Parameter(Mandatory=$true)][int]$ProcessId)
Add-Type @"
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
public static class WinEnum {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static string[] Get(int pid) {
        var list = new List<string>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == (uint)pid) {
                var t = new StringBuilder(512); GetWindowText(h, t, 512);
                var c = new StringBuilder(512); GetClassName(h, c, 512);
                RECT r; GetWindowRect(h, out r);
                list.Add(string.Format("hwnd={0} visible={1} class={2} title={3} rect={4},{5},{6},{7}", h, IsWindowVisible(h), c, t, r.Left, r.Top, r.Right-r.Left, r.Bottom-r.Top));
            }
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }
}
"@
[WinEnum]::Get($ProcessId)
