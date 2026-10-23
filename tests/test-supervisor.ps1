$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $PSScriptRoot ('tmp/supervisor-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$exe = Join-Path $work 'Supervisor.exe'
$file = Join-Path $work 'locked.txt'
Set-Content -LiteralPath $file -Value 'test'
$source = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
public class Supervisor {
 public static void Main(string[] args) {
  if(args.Length > 1) {
   using(var f = File.Open(args[0], FileMode.Open, FileAccess.Read, FileShare.Read)) Thread.Sleep(60000);
  } else {
   for(;;) {
    var p = Process.Start(new ProcessStartInfo(typeof(Supervisor).Assembly.Location, "\""+args[0]+"\" helper") { CreateNoWindow=true, UseShellExecute=false });
    p.WaitForExit();
   }
  }
 }
}
'@
$sourcePath = Join-Path $work 'Supervisor.cs'
[IO.File]::WriteAllText($sourcePath, $source)
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$exe" $sourcePath
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
$supervisor = Start-Process -FilePath $exe -ArgumentList ('"'+$file+'"') -WindowStyle Hidden -PassThru
try {
 Start-Sleep -Seconds 1
 $child = Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $supervisor.Id }
 Stop-Process -Id $child.ProcessId -Force
 Start-Sleep -Seconds 1
 $replacement = Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $supervisor.Id }
 if (!$replacement -or $replacement.ProcessId -eq $child.ProcessId) { throw 'Respawn fixture failed' }
 try { [IO.File]::Move($file, "$file.renamed"); throw 'Expected sharing violation' }
 catch [System.Management.Automation.MethodInvocationException] { if (!$_.Exception.InnerException.Message.Contains('another process')) { throw } }
 [void][Reflection.Assembly]::LoadFrom((Join-Path $root 'src/NoOccupy/bin/Release/net48/NoOccupy.exe'))
 $expanded = [NoOccupy.Core.ApplicationProcesses]::Expand([int[]]@($replacement.ProcessId))
 if ($expanded[0] -ne $supervisor.Id) { throw 'Supervisor must be stopped first' }
 $report = Join-Path $work 'unlock.json'
 $app = Join-Path $root 'src/NoOccupy/bin/Release/net48/NoOccupy.exe'
 $arguments = '--kill "' + $file + '" --pid ' + $replacement.ProcessId + ' --json --out "' + $report + '"'
 $command = Start-Process -FilePath $app -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
 if ($command.ExitCode -ne 0) { throw "Software unlock failed: $($command.ExitCode)" }
 $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
 if ($result.killedFail -ne 0 -or $result.killedOk -lt 2 -or $result.processes.Count -ne 0) { throw 'Unlock verification failed' }
 Start-Sleep -Seconds 2
 if (Get-Process -Id $supervisor.Id -ErrorAction SilentlyContinue) { throw 'Supervisor still running' }
 [IO.File]::Move($file, "$file.renamed")
 'PASS: helper respawn reproduced; software CLI stopped supervisor and helper; recheck clean; actual rename succeeded.'
} finally {
 Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $exe } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
