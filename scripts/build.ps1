$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host '=== 1/3 构建主程序 ==='
dotnet build "$root\src\NoOccupy\NoOccupy.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw '主程序构建失败' }

$appExe = "$root\src\NoOccupy\bin\Release\net48\NoOccupy.exe"
$appConfig = "$root\src\NoOccupy\bin\Release\net48\NoOccupy.exe.config"
if (-not (Test-Path $appExe)) { throw "主程序产物不存在：$appExe" }
if (-not (Test-Path $appConfig)) { throw "主程序配置不存在：$appConfig" }

Write-Host '=== 2/3 构建安装程序 ==='
dotnet build "$root\src\NoOccupy.Setup\NoOccupy.Setup.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw '安装程序构建失败' }

$setupSource = "$root\src\NoOccupy.Setup\bin\Release\net48\NoOccupy.Setup.exe"
if (-not (Test-Path $setupSource)) { throw "安装程序产物不存在：$setupSource" }

$target = "$root\NoOccupy-Setup.exe"
Copy-Item $setupSource $target -Force

Write-Host '=== 3/3 完成 ==='
$hash = Get-FileHash $target -Algorithm SHA256
$size = (Get-Item $target).Length
Write-Host ("安装包：{0}" -f $target)
Write-Host ("大小：{0:N0} 字节 ({1:N1} KB)" -f $size, ($size / 1KB))
Write-Host ("SHA256：{0}" -f $hash.Hash)
