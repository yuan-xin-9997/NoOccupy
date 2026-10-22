using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NoOccupy.Core
{
    /// <summary>
    /// Restart Manager（重启管理器）P/Invoke 封装。
    /// 用于检测占用指定文件的进程；文件夹场景会把所有文件分批注册到同一个会话。
    /// </summary>
    internal static class RestartManager
    {
        private const int CchRmMaxAppName = 255;
        private const int CchRmMaxSvcName = 63;
        private const int CchRmSessionKey = 32;

        private const int ErrorSuccess = 0;
        private const int ErrorMoreData = 234;

        /// <summary>每批最多注册的文件数，避免单次调用资源过大。</summary>
        private const int BatchSize = 1024;

        /// <summary>RmGetList 在进程数变化时最多重试的次数。</summary>
        private const int MaxListAttempts = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int ProcessId;

            // FILETIME 由两个 DWORD 组成。
            public uint ProcessStartTimeLow;
            public uint ProcessStartTimeHigh;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
            public string AppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
            public string ServiceShortName;

            public int ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;

            [MarshalAs(UnmanagedType.Bool)]
            public bool Restartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(
            out uint sessionHandle,
            int sessionFlags,
            StringBuilder sessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(
            uint sessionHandle,
            uint fileCount,
            string[] fileNames,
            uint applicationCount,
            RM_UNIQUE_PROCESS[] applications,
            uint serviceCount,
            string[] serviceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(
            uint sessionHandle,
            out uint procInfoNeeded,
            ref uint procInfoCount,
            [In, Out] RM_PROCESS_INFO[] affectedApps,
            out uint rebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint sessionHandle);

        /// <summary>
        /// 查询一批文件的占用进程。单批失败不会影响其他批次，失败原因写入 warnings。
        /// 返回结果已按 PID 合并（同一进程只保留一条，补全服务名）。
        /// </summary>
        internal static List<LockingProcess> FindLockers(
            IList<string> files,
            List<string> warnings,
            IProgress<string> progress,
            CancellationToken ct)
        {
            var merged = new Dictionary<int, LockingProcess>();
            if (files == null || files.Count == 0)
            {
                return new List<LockingProcess>();
            }

            int totalBatches = (files.Count + BatchSize - 1) / BatchSize;
            for (int offset = 0; offset < files.Count; offset += BatchSize)
            {
                ct.ThrowIfCancellationRequested();

                int count = Math.Min(BatchSize, files.Count - offset);
                Report(progress, string.Format(
                    L.T("正在通过系统重启管理器检测第 {0}/{1} 批（本批 {2} 个文件）..."),
                    offset / BatchSize + 1,
                    totalBatches,
                    count));

                var batch = new string[count];
                for (int i = 0; i < count; i++)
                {
                    batch[i] = files[offset + i];
                }

                try
                {
                    foreach (LockingProcess item in QuerySession(batch))
                    {
                        Merge(merged, item);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AddWarning(warnings, string.Format(
                        L.T("重启管理器检测第 {0} 批失败：{1}"),
                        offset / BatchSize + 1,
                        ex.Message));
                }
            }

            return new List<LockingProcess>(merged.Values);
        }

        private static List<LockingProcess> QuerySession(string[] files)
        {
            var list = new List<LockingProcess>();
            uint sessionHandle;
            var sessionKey = new StringBuilder(CchRmSessionKey + 1);

            int rc = RmStartSession(out sessionHandle, 0, sessionKey);
            if (rc != ErrorSuccess)
            {
                throw new InvalidOperationException(L.T("RmStartSession 失败，错误码 ") + rc);
            }

            try
            {
                rc = RmRegisterResources(sessionHandle, (uint)files.Length, files, 0, null, 0, null);
                if (rc != ErrorSuccess)
                {
                    throw new InvalidOperationException(L.T("RmRegisterResources 失败，错误码 ") + rc);
                }

                uint needed = 0;
                uint count = 0;
                uint reasons = 0;

                // 第一次调用只取所需数量。没有占用进程时直接返回。
                rc = RmGetList(sessionHandle, out needed, ref count, null, out reasons);
                if (rc == ErrorSuccess)
                {
                    return list;
                }

                if (rc != ErrorMoreData)
                {
                    throw new InvalidOperationException(L.T("RmGetList 失败，错误码 ") + rc);
                }

                for (int attempt = 0; attempt < MaxListAttempts; attempt++)
                {
                    if (needed == 0)
                    {
                        return list;
                    }

                    // 进程可能在两次调用之间增加，预留少量余量。
                    count = needed + 8;
                    var buffer = new RM_PROCESS_INFO[count];

                    rc = RmGetList(sessionHandle, out needed, ref count, buffer, out reasons);
                    if (rc == ErrorMoreData)
                    {
                        // 需要的数量变大了，按新的数量再试。
                        continue;
                    }

                    if (rc != ErrorSuccess)
                    {
                        throw new InvalidOperationException(L.T("RmGetList 失败，错误码 ") + rc);
                    }

                    int actual = (int)Math.Min(count, (uint)buffer.Length);
                    for (int i = 0; i < actual; i++)
                    {
                        RM_PROCESS_INFO info = buffer[i];
                        int pid = info.Process.ProcessId;
                        if (pid <= 0)
                        {
                            continue;
                        }

                        LockingProcess process = ProcessUtil.CreateLockingProcess(
                            pid, LockSource.RestartManager);

                        string appName = (info.AppName ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(appName) &&
                            (string.IsNullOrEmpty(process.Name) ||
                             process.Name.StartsWith("PID ", StringComparison.Ordinal)))
                        {
                            // 优先使用可执行文件名；RM 的应用名只在拿不到路径时兜底。
                            process.Name = appName;
                        }

                        string serviceName = (info.ServiceShortName ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(serviceName))
                        {
                            process.ServiceName = serviceName;
                        }

                        list.Add(process);
                    }

                    return list;
                }

                throw new InvalidOperationException(L.T("RmGetList 多次重试后仍报告缓冲区不足"));
            }
            finally
            {
                RmEndSession(sessionHandle);
            }
        }

        private static void Merge(Dictionary<int, LockingProcess> map, LockingProcess item)
        {
            if (item == null || item.Pid <= 0)
            {
                return;
            }

            LockingProcess existing;
            if (!map.TryGetValue(item.Pid, out existing))
            {
                map[item.Pid] = item;
                return;
            }

            if (string.IsNullOrEmpty(existing.Name) && !string.IsNullOrEmpty(item.Name))
            {
                existing.Name = item.Name;
            }
            if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(item.ExecutablePath))
            {
                existing.ExecutablePath = item.ExecutablePath;
            }
            if (string.IsNullOrEmpty(existing.ServiceName) && !string.IsNullOrEmpty(item.ServiceName))
            {
                existing.ServiceName = item.ServiceName;
            }
            if (string.IsNullOrEmpty(existing.UserName) && !string.IsNullOrEmpty(item.UserName))
            {
                existing.UserName = item.UserName;
            }

            existing.IsCritical = existing.IsCritical || item.IsCritical;
        }

        private static void AddWarning(List<string> warnings, string message)
        {
            if (warnings == null)
            {
                return;
            }

            if (warnings.Count < 200)
            {
                warnings.Add(message);
            }
        }

        private static void Report(IProgress<string> progress, string message)
        {
            if (progress == null)
            {
                return;
            }

            try
            {
                progress.Report(message);
            }
            catch
            {
                // UI 已关闭等情况下忽略进度回调异常。
            }
        }
    }
}
