using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace NoOccupy.Core
{
    /// <summary>
    /// 结束占用进程。关键系统进程会被拒绝，服务进程按普通进程处理。
    /// </summary>
    public static class ProcessKiller
    {
        private const string CriticalMessage = "系统关键进程，已阻止结束";

        public static KillResult Kill(IEnumerable<int> pids, IProgress<string> progress)
        {
            var result = new KillResult();
            if (pids == null)
            {
                return result;
            }

            ProcessUtil.EnableDebugPrivilege();

            int selfPid;
            using (Process self = Process.GetCurrentProcess())
            {
                selfPid = self.Id;
            }

            // 去重，保持调用方传入的顺序。
            var seen = new HashSet<int>();

            foreach (int pid in pids)
            {
                if (!seen.Add(pid))
                {
                    continue;
                }

                KillOutcome outcome = KillOne(pid, selfPid, progress);
                result.Outcomes.Add(outcome);

                if (outcome.Success)
                {
                    result.Succeeded++;
                }
                else
                {
                    result.Failed++;
                }
            }

            return result;
        }

        private static KillOutcome KillOne(int pid, int selfPid, IProgress<string> progress)
        {
            var outcome = new KillOutcome
            {
                Pid = pid,
                Name = string.Empty,
                Success = false,
                Error = string.Empty
            };

            if (pid <= 0)
            {
                outcome.Name = "PID " + pid;
                outcome.Error = "无效的进程 ID";
                return outcome;
            }

            if (pid <= 4)
            {
                outcome.Name = pid == 4 ? "System" : "PID " + pid;
                outcome.Error = CriticalMessage;
                return outcome;
            }

            if (pid == selfPid)
            {
                outcome.Name = ProcessUtil.GetProcessName(pid, null);
                outcome.Error = "不能结束当前程序自身进程";
                return outcome;
            }

            Process process = null;
            try
            {
                process = Process.GetProcessById(pid);
                outcome.Name = GetProcessName(process, pid);

                // 先判断关键进程，避免受保护进程的 HasExited 抛出访问拒绝而给出错误原因。
                if (ProcessUtil.IsCriticalProcess(pid, outcome.Name))
                {
                    outcome.Error = CriticalMessage;
                    return outcome;
                }

                if (process.HasExited)
                {
                    // 进程在结束前已经退出，目标已达成。
                    outcome.Success = true;
                    return outcome;
                }

                Report(progress, string.Format(
                    "正在结束 {0} (PID {1})...", outcome.Name, pid));

                process.Kill();
                if (!process.WaitForExit(3000))
                {
                    outcome.Error = "结束进程超时（3 秒），进程可能受保护";
                    return outcome;
                }

                outcome.Success = true;
                Report(progress, string.Format(
                    "已结束 {0} (PID {1})", outcome.Name, pid));
            }
            catch (ArgumentException)
            {
                // 进程在结束前已经退出，目标已达成。
                outcome.Name = "PID " + pid;
                outcome.Success = true;
            }
            catch (InvalidOperationException)
            {
                // 进程已退出。
                outcome.Success = true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 5)
                {
                    outcome.Error = "拒绝访问：权限不足或进程受保护";
                }
                else
                {
                    outcome.Error = "结束进程失败：" + ex.Message;
                }
            }
            catch (Exception ex)
            {
                outcome.Error = "结束进程失败：" + ex.Message;
            }
            finally
            {
                if (process != null)
                {
                    try
                    {
                        process.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            return outcome;
        }

        private static string GetProcessName(Process process, int pid)
        {
            string imagePath = ProcessUtil.GetProcessImagePath(pid);
            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    string fileName = Path.GetFileName(imagePath);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        return fileName;
                    }
                }
                catch
                {
                }
            }

            try
            {
                string name = process.ProcessName;
                if (!string.IsNullOrEmpty(name))
                {
                    return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? name
                        : name + ".exe";
                }
            }
            catch
            {
            }

            return "PID " + pid;
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
                // UI 已关闭时忽略进度回调异常。
            }
        }
    }
}
