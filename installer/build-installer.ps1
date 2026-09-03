<#
    Builds the AeroDial installer.

        .\installer\build-installer.ps1            # publish, then compile the installer
        .\installer\build-installer.ps1 -NoPublish # compile from the existing publish output

    Output: dist\AeroDial-<version>-Setup.exe (version comes from the published exe).

    The publish output is verified before it is packaged. A failed or skipped publish
    used to leave an older exe sitting in the publish folder, and the installer was
    built from it without complaint, producing a setup that silently shipped stale
    code. Both checks below exist to make that impossible.
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

# A copy running out of the build tree locks AeroDial.Core.dll, so any publish that
# rebuilds Core fails with MSB3027. Say so up front instead of leaving the developer
# to decode an MSBuild retry wall.
$running = Get-Process AeroDial -ErrorAction SilentlyContinue |
           Where-Object { $_.Path -and $_.Path.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase) }
if ($running) {
    Write-Warning "AeroDial is running from this repo ($($running[0].Path)). It locks the build output; quit it from the tray if the publish fails to copy AeroDial.Core.dll."
}

if (-not $NoPublish) {
    Write-Host 'Publishing Release build...' -ForegroundColor Cyan
    dotnet publish $project -c Release -r win-x64
    if ($LASTEXITCODE -ne 0) {
        $hint = if ($running) { " The running AeroDial in this repo is the likely cause: quit it and retry." } else { '' }
        throw "dotnet publish failed ($LASTEXITCODE).$hint"
    }
}

if (-not (Test-Path $exe)) {
    throw "Published exe not found at $exe. Run without -NoPublish."
}

# ── Verify the publish output is actually current ────────────────────────────
$exeItem = Get-Item $exe

$newestSource = Get-ChildItem (Join-Path $repo 'src') -Recurse -File |
    Where-Object { $_.Extension -in '.cs', '.csproj', '.xaml', '.manifest', '.ico', '.json' -and
                   $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($newestSource -and $exeItem.LastWriteTime -lt $newestSource.LastWriteTime) {
    throw ("Published exe is STALE: {0} was written {1}, but {2} changed at {3}. " +
           "Publish again (drop -NoPublish); packaging this would ship old code.") -f `
           $exeItem.Name, $exeItem.LastWriteTime, $newestSource.Name, $newestSource.LastWriteTime
}

# The .iss takes its version from this exe, so a mismatch would also mislabel the setup.
$csprojVersion = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$exeVersion    = ($exeItem.VersionInfo.FileVersion -split '\.')[0..2] -join '.'
if ($csprojVersion -and $exeVersion -ne $csprojVersion) {
    throw "Published exe reports version $exeVersion but the csproj says $csprojVersion. Publish again so the two agree."
}

Write-Host ("Publish output verified: {0} v{1}, built {2}" -f $exeItem.Name, $exeVersion, $exeItem.LastWriteTime) -ForegroundColor Green

# ── Compile the installer ────────────────────────────────────────────────────
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
