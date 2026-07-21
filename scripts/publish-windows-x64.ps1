[CmdletBinding()]
param(
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts\windows-x64'
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

Push-Location $root
try {
    & "$PSScriptRoot\build-and-test.ps1" -Configuration Release

    dotnet publish .\src\TrainerStudio.App\TrainerStudio.App.csproj -c Release -r win-x64 `
        --self-contained $selfContained -p:PublishSingleFile=true `
        -o (Join-Path $output 'TrainerStudio')
    if ($LASTEXITCODE -ne 0) { throw 'Trainer Studio publish failed.' }

    dotnet publish .\src\TrainerStudio.TestGame\TrainerStudio.TestGame.csproj -c Release -r win-x64 `
        --self-contained $selfContained -p:PublishSingleFile=true `
        -o (Join-Path $output 'TestGame')
    if ($LASTEXITCODE -ne 0) { throw 'Test game publish failed.' }

    Write-Host "Published Windows x64 files to $output" -ForegroundColor Green
}
finally {
    Pop-Location
}
