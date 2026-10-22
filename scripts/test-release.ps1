$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$expectedVersion = [Version]"$version.0"
$app = Join-Path $root 'src/NoOccupy/bin/Release/net48/NoOccupy.exe'
$setup = Join-Path $root 'dist/NoOccupy-Setup.exe'

foreach ($file in @($app, $setup)) {
    if ([Reflection.AssemblyName]::GetAssemblyName($file).Version -ne $expectedVersion) {
        throw "Unexpected assembly version: $file"
    }
}

$assembly = [Reflection.Assembly]::LoadFile($setup)
$sha = [Security.Cryptography.SHA256]::Create()
try {
    foreach ($name in @('NoOccupy.exe', 'NoOccupy.exe.config')) {
        $stream = $assembly.GetManifestResourceStream($name)
        if ($null -eq $stream) { throw "Missing installer payload: $name" }
        try {
            $embeddedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
        } finally {
            $stream.Dispose()
        }
        $source = Join-Path (Split-Path $app) $name
        if ($embeddedHash -ne (Get-FileHash $source -Algorithm SHA256).Hash) {
            throw "Installer payload differs from build: $name"
        }
    }
} finally {
    $sha.Dispose()
}

$checksum = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = (Get-Content (Join-Path $root 'dist/SHA256SUMS.txt') -Raw).Trim()
if ($manifest -cne "$checksum  NoOccupy-Setup.exe") { throw 'Release checksum mismatch' }
if ((Get-FileHash (Join-Path $root 'NoOccupy-Setup.exe')).Hash -ne $checksum) {
    throw 'Root installer differs from release asset'
}
Write-Host "Release $version verified: assembly versions, embedded application/config, installer and SHA256SUMS.txt."
