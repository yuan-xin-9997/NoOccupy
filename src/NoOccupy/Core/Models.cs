using System;
using System.Collections.Generic;

namespace NoOccupy.Core
{
    public enum LockSource
    {
        RestartManager = 0,
        HandleScan = 1,
        ModuleScan = 2
    }

    public class LockingProcess
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public string ServiceName { get; set; }
        public string UserName { get; set; }
        public LockSource Source { get; set; }
        public bool IsCritical { get; set; }

        public string SourceText
        {
            get
            {
                switch (Source)
                {
                    case LockSource.HandleScan: return "句柄扫描";
                    case LockSource.ModuleScan: return "模块扫描";
                    default: return "系统检测";
                }
            }
        }
    }

    public class ScanResult
    {
        public string TargetPath { get; set; }
        public bool IsDirectory { get; set; }
        public List<LockingProcess> Processes { get; set; }
        public List<string> Warnings { get; set; }
        public int ScannedFiles { get; set; }
        public long ElapsedMs { get; set; }

        public ScanResult()
        {
            Processes = new List<LockingProcess>();
            Warnings = new List<string>();
        }
    }

    public class KillOutcome
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class KillResult
    {
        public List<KillOutcome> Outcomes { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }

        public KillResult()
        {
            Outcomes = new List<KillOutcome>();
        }
    }
}
