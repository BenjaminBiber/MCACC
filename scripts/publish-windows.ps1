<#
.SYNOPSIS
  Builds MCACC for Windows as a single self-contained exe and zips it.

.EXAMPLE
  .\scripts\publish-windows.ps1 -Version 0.1.0
#>
[CmdletBinding()]
param(
    [string]$Version = '0.1.0',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked {
    param([string]$What, [scriptblock]$Command)
    Write-Host "==> $What" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot 'artifacts\publish\win-x64'
$artifactsDir = Join-Path $repoRoot 'artifacts'
$zipPath = Join-Path $artifactsDir "MCACC-$Version-win-x64.zip"

Push-Location $repoRoot
try {
    if (-not $SkipTests) {
        Invoke-Checked 'Tests' { dotnet test MCACC.slnx -c Release --nologo }
    }

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Invoke-Checked 'Publish' {
        dotnet publish src/Mcacc.App/Mcacc.App.csproj `
            -c Release `
            -r win-x64 `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:PublishReadyToRun=true `
            -p:DebugType=None `
            -p:Version=$Version `
            -o $publishDir `
            --nologo
    }

    # The font licenses must travel with the binary they are embedded in.
    $fontsDir = Join-Path $repoRoot 'src\Mcacc.App\Assets\Fonts'
    Copy-Item (Join-Path $fontsDir 'OFL.txt') (Join-Path $publishDir 'OFL.txt') -Force
    Copy-Item (Join-Path $fontsDir 'LICENSE-material-icons.txt') (Join-Path $publishDir 'LICENSE-material-icons.txt') -Force

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Write-Host '==> Zip' -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

    $exe = Join-Path $publishDir 'MCACC.exe'
    $exeSize = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host ''
    Write-Host "MCACC.exe  $exeSize MB  $exe"
    Write-Host "zip        $zipSize MB  $zipPath"
}
finally {
    Pop-Location
}
