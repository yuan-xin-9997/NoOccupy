param(
    [Parameter(Mandatory=$true)][string]$Path,
    [int]$Seconds = 120
)
$fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
try {
    Start-Sleep -Seconds $Seconds
} finally {
    $fs.Dispose()
}
