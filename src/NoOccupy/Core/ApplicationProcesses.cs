using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NoOccupy.Core
{
    // Only include a parent application when the selected helper lives beneath
    // that application's directory. Never follow Explorer or shell ancestors.
    public static class ApplicationProcesses
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Entry
        {
            public uint Size, Usage, Pid;
            public IntPtr Heap;
            public uint Module, Threads, Parent;
            public int Priority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string Name;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref Entry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Process32NextW(IntPtr snapshot, ref Entry entry);

        public static List<int> Expand(IEnumerable<int> selected)
        {
            var result = selected.Distinct().ToList();
            var parents = new Dictionary<int, int>();
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1)) return result;
            try
            {
                var entry = new Entry { Size = (uint)Marshal.SizeOf(typeof(Entry)) };
                if (Process32FirstW(snapshot, ref entry))
                    do { parents[(int)entry.Pid] = (int)entry.Parent; }
                    while (Process32NextW(snapshot, ref entry));
            }
            finally { ProcessUtil.CloseHandle(snapshot); }

            foreach (int selectedPid in result.ToArray())
            {
                int current = selectedPid;
                string childPath = ProcessUtil.GetProcessImagePath(current);
                var visited = new HashSet<int>();
                int parent;
                while (childPath != null && visited.Add(current) &&
                       parents.TryGetValue(current, out parent) && parent > 4)
                {
                    string parentPath = ProcessUtil.GetProcessImagePath(parent);
                    if (parentPath == null || ProcessUtil.IsCriticalProcess(parent, Path.GetFileName(parentPath))) break;
                    string directory = Path.GetDirectoryName(parentPath);
                    if (string.IsNullOrEmpty(directory) ||
                        directory.Equals(Environment.SystemDirectory, StringComparison.OrdinalIgnoreCase) ||
                        directory.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase) ||
                        !childPath.StartsWith(directory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) break;
                    // Stop the supervisor before its helper so it cannot respawn it.
                    result.Remove(parent);
                    result.Insert(0, parent);
                    current = parent;
                    childPath = parentPath;
                }
            }
            return result;
        }
    }
}
