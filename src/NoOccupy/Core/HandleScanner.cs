using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NoOccupy.Core
{
    /// <summary>
    /// NT 系统句柄表深度扫描。
    /// 通过 NtQuerySystemInformation(SystemExtendedHandleInformation) 枚举全系统句柄，
    /// DuplicateHandle 到本进程后只处理磁盘文件句柄，再用 GetFinalPathNameByHandle 取得路径并匹配目标。
    /// 路径查询放在带超时的后台线程执行，避免个别虚拟盘/网络盘句柄导致整个扫描卡死。
    /// </summary>
    internal static class HandleScanner
    {
        private const int SystemExtendedHandleInformation = 64;
        private const uint StatusInfoLengthMismatch = 0xC0000004;

        private const uint FileTypeDisk = 1;
        private const uint DuplicateSameAccess = 0x00000002;

        private const uint InitialBufferSize = 1u << 20; // 1 MB
        private const uint MaxBufferSize = 512u << 20;   // 512 MB

        /// <summary>单个句柄路径查询的超时时间（毫秒）。</summary>
        private const int PathQueryTimeoutMs = 150;

        /// <summary>允许放弃的卡死查询线程数量上限，超过后跳过剩余路径查询。</summary>
        private const int MaxAbandonedQueries = 8;

        /// <summary>x64: SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX = 40 字节；x86: 28 字节。</summary>
        private static int EntrySize
        {
            get { return IntPtr.Size == 8 ? 40 : 28; }
        }

        [DllImport("ntdll.dll")]
        private static extern uint NtQuerySystemInformation(
            int systemInformationClass,
            IntPtr systemInformation,
            uint systemInformationLength,
            out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateHandle(
            IntPtr sourceProcessHandle,
            IntPtr sourceHandle,
            IntPtr targetProcessHandle,
            out IntPtr targetHandle,
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint options);

        [DllImport("kernel32.dll")]
        private static extern uint GetFileType(IntPtr fileHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            IntPtr fileHandle,
            StringBuilder filePath,
            uint filePathLength,
            uint flags);

        /// <summary>
        /// 扫描全系统句柄，返回持有目标文件/文件夹（含其下任意路径）句柄的进程。
        /// 无法打开的进程和句柄会被静默忽略。
        /// </summary>
        internal static List<LockingProcess> Scan(
            string targetPath,
            bool isDirectory,
            List<string> warnings,
            IProgress<string> progress,
            CancellationToken ct)
        {
            var results = new List<LockingProcess>();
            var target = new PathTarget(targetPath, isDirectory);

            int selfPid;
            using (Process self = Process.GetCurrentProcess())
            {
                selfPid = self.Id;
            }

            ProcessUtil.EnableDebugPrivilege();

            IntPtr buffer = IntPtr.Zero;
            uint bufferSize = InitialBufferSize;
            uint returnLength = 0;

            // 缓冲区不足时按系统返回的长度（或翻倍）重试。
            for (int attempt = 0; ; attempt++)
            {
                buffer = Marshal.AllocHGlobal((int)bufferSize);
                uint status = NtQuerySystemInformation(
                    SystemExtendedHandleInformation, buffer, bufferSize, out returnLength);

                if (status == 0)
                {
                    break;
                }

                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;

                if (status != StatusInfoLengthMismatch || attempt >= 8)
                {
                    throw new InvalidOperationException(
                        "NtQuerySystemInformation 失败，状态码 0x" + status.ToString("X8"));
                }

                long next = Math.Max((long)returnLength + 65536, (long)bufferSize * 2);
                if (next <= bufferSize || next > MaxBufferSize)
                {
                    throw new InvalidOperationException("系统句柄表过大，无法枚举");
                }

                bufferSize = (uint)next;
            }

            var processHandles = new Dictionary<int, IntPtr>();
            PathQueryWorker worker = null;
            var workers = new List<PathQueryWorker>();
            int abandonedQueries = 0;
            bool skipPathQueries = false;

            try
            {
                int headerSize = IntPtr.Size * 2;
                int entrySize = EntrySize;

                // 成功时 returnLength 通常是实际需要的字节数；为 0 时退回到缓冲区大小。
                long validBytes = bufferSize;
                if (returnLength > 0 && (long)returnLength < validBytes)
                {
                    validBytes = returnLength;
                }

                long handleCount = IntPtr.Size == 8
                    ? Marshal.ReadInt64(buffer, 0)
                    : Marshal.ReadInt32(buffer, 0);

                // 防御异常数据，避免越界读取。
                long available = (validBytes - headerSize) / entrySize;
                if (available < 0)
                {
                    available = 0;
                }
                if (handleCount > available)
                {
                    handleCount = available;
                }
                if (handleCount < 0)
                {
                    handleCount = 0;
                }

                if (progress != null)
                {
                    progress.Report(string.Format(
                        "正在扫描系统句柄表（共 {0:N0} 个句柄）...", handleCount));
                }

                var matchedPids = new HashSet<int>();
                IntPtr currentProcess = ProcessUtil.GetCurrentProcess();
                long bufferBase = buffer.ToInt64();

                for (long i = 0; i < handleCount; i++)
                {
                    if (skipPathQueries)
                    {
                        break;
                    }

                    if ((i & 0x7FF) == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                    }

                    long entryAddress = bufferBase + headerSize + i * entrySize;
                    IntPtr entry = new IntPtr(entryAddress);

                    int pid = IntPtr.Size == 8
                        ? (int)Marshal.ReadInt64(entry, 8)
                        : Marshal.ReadInt32(entry, 4);

                    // 跳过 Idle/System 内核句柄、自身以及已经确认匹配的进程。
                    if (pid <= 4 || pid == selfPid || matchedPids.Contains(pid))
                    {
                        continue;
                    }

                    IntPtr handleValue = IntPtr.Size == 8
                        ? new IntPtr(Marshal.ReadInt64(entry, 16))
                        : new IntPtr(Marshal.ReadInt32(entry, 8));

                    if (handleValue == IntPtr.Zero || handleValue == new IntPtr(-1))
                    {
                        continue;
                    }

                    IntPtr processHandle;
                    if (!processHandles.TryGetValue(pid, out processHandle))
                    {
                        processHandle = ProcessUtil.OpenProcess(
                            ProcessUtil.ProcessDupHandle, false, pid);
                        processHandles[pid] = processHandle;
                    }

                    if (processHandle == IntPtr.Zero)
                    {
                        // 受保护或已退出的进程，忽略。
                        continue;
                    }

                    IntPtr duplicated;
                    if (!DuplicateHandle(
                            processHandle,
                            handleValue,
                            currentProcess,
                            out duplicated,
                            0,
                            false,
                            DuplicateSameAccess))
                    {
                        continue;
                    }

                    try
                    {
                        // 先过滤类型，避免对命名管道等句柄调用可能阻塞的路径查询。
                        if (GetFileType(duplicated) != FileTypeDisk)
                        {
                            continue;
                        }

                        if (skipPathQueries)
                        {
                            continue;
                        }

                        if (worker == null)
                        {
                            worker = new PathQueryWorker();
                            workers.Add(worker);
                        }

                        string filePath;
                        if (!worker.TryQuery(duplicated, PathQueryTimeoutMs, out filePath))
                        {
                            // 该句柄的路径查询卡住：关闭副本、放弃该线程，换新线程继续。
                            ProcessUtil.CloseHandle(duplicated);
                            duplicated = IntPtr.Zero;
                            worker.Stop();
                            worker = null;
                            abandonedQueries++;

                            if (abandonedQueries >= MaxAbandonedQueries)
                            {
                                skipPathQueries = true;
                                AddWarning(warnings,
                                    "多个文件句柄的路径查询超时，已跳过剩余句柄的深度扫描。");
                            }

                            continue;
                        }

                        if (filePath != null && target.Matches(filePath))
                        {
                            matchedPids.Add(pid);
                        }
                    }
                    catch
                    {
                        // 单个句柄查询失败不影响整体扫描。
                    }
                    finally
                    {
                        if (duplicated != IntPtr.Zero)
                        {
                            ProcessUtil.CloseHandle(duplicated);
                        }
                    }

                    if (progress != null && i > 0 && (i % 16384) == 0)
                    {
                        progress.Report(string.Format(
                            "句柄扫描：已检查 {0:N0}/{1:N0} 个句柄，匹配 {2} 个进程...",
                            i,
                            handleCount,
                            matchedPids.Count));
                    }
                }

                foreach (int pid in matchedPids)
                {
                    try
                    {
                        results.Add(ProcessUtil.CreateLockingProcess(pid, LockSource.HandleScan));
                    }
                    catch
                    {
                        // 进程可能刚好退出，忽略。
                    }
                }
            }
            finally
            {
                foreach (PathQueryWorker queryWorker in workers)
                {
                    queryWorker.Stop();
                }

                foreach (IntPtr handle in processHandles.Values)
                {
                    if (handle != IntPtr.Zero)
                    {
                        ProcessUtil.CloseHandle(handle);
                    }
                }

                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return results;
        }

        private static void AddWarning(List<string> warnings, string message)
        {
            if (warnings != null && warnings.Count < 200)
            {
                warnings.Add(message);
            }
        }

        private static string GetFinalPath(IntPtr handle, StringBuilder buffer)
        {
            buffer.Length = 0;

            uint length = GetFinalPathNameByHandle(
                handle, buffer, (uint)buffer.Capacity, 0);

            if (length == 0)
            {
                return null;
            }

            if (length >= buffer.Capacity)
            {
                if (length > 32768)
                {
                    return null;
                }

                buffer.Capacity = (int)length + 1;
                length = GetFinalPathNameByHandle(
                    handle, buffer, (uint)buffer.Capacity, 0);

                if (length == 0 || length >= buffer.Capacity)
                {
                    return null;
                }
            }

            return buffer.ToString();
        }

        /// <summary>
        /// 在独立后台线程执行 GetFinalPathNameByHandle。
        /// 某些虚拟盘/已断开的网络盘句柄可能让该 API 长时间阻塞，此时主线程超时后放弃该线程。
        /// </summary>
        private sealed class PathQueryWorker
        {
            private readonly AutoResetEvent _request = new AutoResetEvent(false);
            private readonly AutoResetEvent _completed = new AutoResetEvent(false);
            private readonly object _sync = new object();
            private readonly StringBuilder _buffer = new StringBuilder(512);

            private IntPtr _handle;
            private string _result;
            private volatile bool _abandoned;
            private volatile bool _stop;

            internal PathQueryWorker()
            {
                var thread = new Thread(Run);
                thread.IsBackground = true;
                thread.Name = "NoOccupy.HandlePath";
                thread.Start();
            }

            internal bool TryQuery(IntPtr handle, int timeoutMs, out string result)
            {
                result = null;
                lock (_sync)
                {
                    _handle = handle;
                    _result = null;
                }

                _completed.Reset();
                _request.Set();

                if (!_completed.WaitOne(timeoutMs))
                {
                    _abandoned = true;
                    return false;
                }

                lock (_sync)
                {
                    result = _result;
                }

                return true;
            }

            internal void Stop()
            {
                _stop = true;
                _request.Set();
            }

            private void Run()
            {
                while (true)
                {
                    _request.WaitOne();

                    if (_stop)
                    {
                        return;
                    }

                    IntPtr handle;
                    lock (_sync)
                    {
                        handle = _handle;
                    }

                    string path = null;
                    try
                    {
                        path = GetFinalPath(handle, _buffer);
                    }
                    catch
                    {
                    }

                    if (_abandoned)
                    {
                        // 主线程已放弃此线程，不再写回结果。
                        continue;
                    }

                    lock (_sync)
                    {
                        _result = path;
                    }
                    _completed.Set();
                }
            }
        }
    }
}
