<#
    Builds the AeroDial installer.

        .\installer\build-installer.ps1            # publish, then compile the installer
        .\installer\build-installer.ps1 -NoPublish # compile from the existing publish output

    Output: dist\AeroDial-<version>-Setup.exe (version comes from the published exe).
#>
[CmdletBinding()]
param(
    [switch]$NoPublish
)

$ErrorActionPreference = 'Stop'

$repo    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\AeroDial\AeroDial.csproj'
$publish = Join-Path $repo 'src\AeroDial\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish'
$exe     = Join-Path $publish 'AeroDial.exe'
$script  = Join-Path $PSScriptRoot 'AeroDial.iss'

if (-not $NoPublish) {
    Write-Host 'Publishing Release build...' -ForegroundColor Cyan
    dotnet publish $project -c Release -r win-x64
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
}

if (-not (Test-Path $exe)) {
    throw "Published exe not found at $exe. Run without -NoPublish."
}

# Inno Setup 6 installs per user by default, per machine when elevated.
$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php, or add ISCC.exe to one of: $($candidates -join '; ')"
}

Write-Host "Compiling installer with $iscc" -ForegroundColor Cyan
& $iscc $script
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setup = Get-ChildItem (Join-Path $repo 'dist') -Filter 'AeroDial-*-Setup.exe' |
         Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ("Built {0} ({1:N1} MB)" -f $setup.FullName, ($setup.Length / 1MB)) -ForegroundColor Green
