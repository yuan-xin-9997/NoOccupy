# NoOccupy 核心引擎自测报告

- 测试日期：2026-10-21
- 环境：Windows（x64），.NET Framework 4.6.2，Release 构建，管理员权限
- 构建命令：

```powershell
dotnet build E:\dev\NoOccupy\src\NoOccupy\NoOccupy.csproj -c Release `
  '-p:BaseIntermediateOutputPath=obj-core\' `
  '-p:BaseOutputPath=bin-core\' `
  '-p:DefaultItemExcludes=**/obj*/**'
```

- 构建结果：成功，0 警告，0 错误
- 产物：`E:\dev\NoOccupy\src\NoOccupy\bin-core\Release\net462\NoOccupy.exe`

> `-p:DefaultItemExcludes=**/obj*/**` 仅用于排除仓库中遗留的 `obj-ui` 生成文件，避免默认 glob 引发 CS0579；与核心代码无关。

## 结果总览

| 场景 | 命令 | 退出码 | 结果 |
| --- | --- | --- | --- |
| 文件被深度扫描 | `--scan quick.txt --json` | 0 | 通过句柄扫描发现 PowerShell 占用 |
| 文件夹句柄扫描 | `--scan folder --json` | 0 | 发现持有目录句柄的 PowerShell |
| 模块扫描 | `--scan folder --json` | 0 | 发现从目标目录运行的 `powershell-copy.exe` |
| 结束占用进程 | `--kill quick.txt --pid <pid> --json` | 0 | `killedOk=1`，进程退出，文件可删除 |
| 1100 文件 RM 分批 | `--scan many --json --no-deep` | 0 | `scannedFiles=1100`，跨 2 个 RM 批次 |
| 联接点循环 | `--scan loop --json --no-deep` | 0 | 跳过联接点并给出警告，只统计 2 个文件 |
| 服务名提取 | `--scan System.evtx --json --no-deep` | 0 | `svchost.exe` / 服务 `EventLog` |
| 无占用扫描 | `--scan plain.txt --json --no-deep` | 0 | 0 个进程，仍为成功 |
| 关键进程保护 | `--kill plain.txt --pid 4 --json` | 2 | 返回“系统关键进程，已阻止结束” |
| 不存在的 PID | `--kill plain.txt --pid 999999 --json` | 1 | 参数/存在性校验失败 |
| 不存在的路径 | `--scan nope.txt --json` | 1 | 文件不存在 |
| 未知参数 | `--scan plain.txt --json --bogus` | 1 | 未知参数 |
| 缺路径 | `--scan --json` | 1 | 缺少扫描路径 |
| `--all` 无占用 | `--kill plain.txt --all --json --no-deep` | 0 | 空结果，成功 |
| `--out` 文件 | `--scan plain.txt --json --out utf8-out.json` | 0 | UTF-8 无 BOM |
| 版本/帮助 | `--version` / `--help` | 0 | 正常输出 |
| 取消扫描 | 反射调用 `Scan(..., CancelAfter(30))` | — | 及时抛出 `OperationCanceledException` |

## 实际 JSON 输出

### 1. 文件深度扫描（句柄扫描命中）

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\final\\quick.txt","isDirectory":false,"elapsedMs":1401,"scannedFiles":1,"processes":[{"pid":72288,"name":"powershell.exe","path":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","service":"","source":"HandleScan","critical":false}],"warnings":[]}
```

### 2. 结束文件占用进程

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\final\\quick.txt","isDirectory":false,"elapsedMs":1399,"scannedFiles":1,"processes":[{"pid":72288,"name":"powershell.exe","path":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","service":"","source":"HandleScan","critical":false}],"warnings":[],"killed":[{"pid":72288,"name":"powershell.exe","success":true,"error":""}],"killedOk":1,"killedFail":0}
```

结束进程后文件可立即删除（`DELETE=OK`）。

### 3. 文件夹句柄扫描与结束

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\folder","isDirectory":true,"elapsedMs":7523,"scannedFiles":3,"processes":[{"pid":42964,"name":"powershell.exe","path":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","service":"","source":"HandleScan","critical":false}],"warnings":[]}
```

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\folder","isDirectory":true,"elapsedMs":7948,"scannedFiles":3,"processes":[{"pid":42964,"name":"powershell.exe","path":"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe","service":"","source":"HandleScan","critical":false}],"warnings":[],"killed":[{"pid":42964,"name":"powershell.exe","success":true,"error":""}],"killedOk":1,"killedFail":0}
```

### 4. 模块扫描（进程 exe 位于目标文件夹）

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\folder","isDirectory":true,"elapsedMs":7995,"scannedFiles":4,"processes":[{"pid":40720,"name":"powershell-copy.exe","path":"E:\\dev\\NoOccupy\\tests\\tmp\\folder\\powershell-copy.exe","service":"","source":"ModuleScan","critical":false}],"warnings":[]}
```

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\folder","isDirectory":true,"elapsedMs":8207,"scannedFiles":4,"processes":[{"pid":40720,"name":"powershell-copy.exe","path":"E:\\dev\\NoOccupy\\tests\\tmp\\folder\\powershell-copy.exe","service":"","source":"ModuleScan","critical":false}],"warnings":[],"killed":[{"pid":40720,"name":"powershell-copy.exe","success":true,"error":""}],"killedOk":1,"killedFail":0}
```

### 5. 1100 个文件的 Restart Manager 分批

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\final\\many","isDirectory":true,"elapsedMs":6490,"scannedFiles":1100,"processes":[],"warnings":[]}
```

### 6. 联接点循环跳过

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\loop","isDirectory":true,"elapsedMs":6164,"scannedFiles":2,"processes":[],"warnings":["\u5df2\u8df3\u8fc7\u7b26\u53f7\u94fe\u63a5/\u8054\u63a5\u70b9\uff1aE:\\dev\\NoOccupy\\tests\\tmp\\loop\\back"]}
```

警告解码：`已跳过符号链接/联接点：E:\dev\NoOccupy\tests\tmp\loop\back`。

### 7. 服务名提取

```json
{"ok":true,"target":"C:\\Windows\\System32\\winevt\\Logs\\System.evtx","isDirectory":false,"elapsedMs":170,"scannedFiles":1,"processes":[{"pid":4680,"name":"svchost.exe","path":"C:\\Windows\\System32\\svchost.exe","service":"EventLog","source":"RestartManager","critical":false}],"warnings":[]}
```

### 8. 无占用扫描 / `--all` 空结果

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\final\\plain.txt","isDirectory":false,"elapsedMs":20,"scannedFiles":1,"processes":[],"warnings":[]}
```

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\final\\plain.txt","isDirectory":false,"elapsedMs":19,"scannedFiles":1,"processes":[],"warnings":[],"killed":[],"killedOk":0,"killedFail":0}
```

### 9. 关键进程保护（退出码 2）

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\final\\plain.txt","isDirectory":false,"elapsedMs":19,"scannedFiles":1,"processes":[],"warnings":[],"killed":[{"pid":4,"name":"System","success":false,"error":"\u7cfb\u7edf\u5173\u952e\u8fdb\u7a0b\uff0c\u5df2\u963b\u6b62\u7ed3\u675f"}],"killedOk":0,"killedFail":1}
```

错误文本解码：`系统关键进程，已阻止结束`（与要求完全一致）。

### 10. 参数与路径错误（退出码 1）

```json
{"ok":false,"error":"\u8fdb\u7a0b\u4e0d\u5b58\u5728\uff1a999999"}
```

```json
{"ok":false,"error":"\u672a\u77e5\u53c2\u6570\uff1a--bogus"}
```

```json
{"ok":false,"error":"\u6587\u4ef6\u4e0d\u5b58\u5728\uff1aE:\\dev\\NoOccupy\\tests\\tmp\\final\\nope.txt"}
```

```json
{"ok":false,"error":"\u7f3a\u5c11\u626b\u63cf\u8def\u5f84\uff0c\u4f8b\u5982\uff1a--scan \"C:\\path\""}
```

### 11. `--out` 文件

`utf8-out.json` 共 151 字节，首字节不是 `EF BB BF`（无 BOM），内容与 stdout 的 JSON 一致：

```json
{"ok":true,"target":"E:\\dev\\NoOccupy\\tests\\tmp\\final\\plain.txt","isDirectory":false,"elapsedMs":19,"scannedFiles":1,"processes":[],"warnings":[]}
```

### 12. 版本与帮助

```
NoOccupy 1.0.1.0
```

`--help` 返回完整用法、选项和退出码说明，退出码 0。

## 行为核对

- Restart Manager 使用 `RmStartSession`/`RmRegisterResources`/`RmGetList`，正确处理 `ERROR_MORE_DATA`；文件夹按最多 1024 个文件分批注册，1100 文件场景通过。
- 文件夹递归使用显式栈，跳过重解析点并记录警告；联接点循环不会无限递归。
- NT 句柄扫描使用 `SystemExtendedHandleInformation`（64），x64 条目按 40 字节对齐，处理 `STATUS_INFO_LENGTH_MISMATCH`；`GetFileType` 只保留磁盘句柄，`GetFinalPathNameByHandle` 解析路径。
- 深度扫描包含模块扫描；结果按 PID 去重并合并来源/服务等字段，关键进程排后，自身 PID 被排除。
- 结束进程前启用 `SeDebugPrivilege`；去重、跳过 PID ≤ 4 与自身；关键进程在 `HasExited` 之前拦截；`Kill()` 后 `WaitForExit(3000)`，错误信息为中文。
- CLI 支持 `--scan` / `--kill` / `--version` / `--help`，`--json`、`--out`、`--all`、`--pid`、`--no-deep`；成功 0，结束失败 2，参数/扫描错误 1；输出为 UTF-8（JSON 中非 ASCII 统一转义为 `\uXXXX`，避免代码页影响）。
- `TryRun` 仅在首参数以 `--` 开头时接管 CLI，否则返回 `false` 交给 GUI；`--json` 在参数错误时也会生效。

## 已知限制

1. 单个句柄路径查询超时为 150 ms；累计放弃 8 个查询后，会跳过剩余句柄并写入警告。若机器上存在大量会阻塞 `GetFinalPathNameByHandle` 的虚拟盘/网络盘句柄，可能漏报后续占用。
2. 受保护进程可能无法 `OpenProcess`，其句柄会被静默跳过；关键进程保护依赖进程名列表，列表外的系统进程仍可能被结束。
3. 深度扫描文件夹时模块扫描会遍历系统进程，实测增加约 6 秒（整体 7.4–8.2 秒）；文件深度扫描约 1.3–1.4 秒。
4. 跳过符号链接/联接点意味着链接目标内部的占用不会被扫描。
5. 服务进程按普通进程结束；`svchost.exe` 等宿主可能承载多个服务，且自动恢复的服务可能在结束后重启。
6. 句柄扫描需要管理员权限（应用清单已声明 `requireAdministrator`）；本次仅在 x64 环境验证，x86 条目布局虽已实现但未实测。
7. `--pid` 允许结束扫描结果之外的已存在进程，这是 CLI 的设计行为，调用方需自行确认 PID。
8. `--out` 在写文件的同时仍会向 stdout 输出同一份内容，便于交互查看。

## 清理

测试使用的临时目录、锁定脚本、复制进程和扫描输出均已删除，仅保留本报告。
