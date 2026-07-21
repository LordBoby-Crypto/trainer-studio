[CmdletBinding()]
param(
    [switch]$FrameworkDependent,
    [switch]$SkipBuildAndTest
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts\windows-x64'
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

Push-Location $root
try {
    if (-not $SkipBuildAndTest) {
        & "$PSScriptRoot\build-and-test.ps1" -Configuration Release
    }

    if (Test-Path $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    dotnet publish .\src\TrainerStudio.App\TrainerStudio.App.csproj -c Release -r win-x64 `
        --self-contained $selfContained -p:PublishSingleFile=true `
        -o (Join-Path $output 'TrainerStudio')
    if ($LASTEXITCODE -ne 0) { throw 'Trainer Studio publish failed.' }

    dotnet publish .\src\TrainerStudio.TestGame\TrainerStudio.TestGame.csproj -c Release -r win-x64 `
        --self-contained $selfContained -p:PublishSingleFile=true `
        -o (Join-Path $output 'TestGame')
    if ($LASTEXITCODE -ne 0) { throw 'Test game publish failed.' }

    Copy-Item -LiteralPath .\README.md -Destination (Join-Path $output 'README.md')
    Copy-Item -LiteralPath .\docs\TESTING.md -Destination (Join-Path $output 'TESTING.md')

    Write-Host "Published Windows x64 files to $output" -ForegroundColor Green
}
finally {
    Pop-Location
}
