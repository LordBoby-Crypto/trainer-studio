[CmdletBinding()]
param(
    [string]$BundlePath = "artifacts\windows-x64",
    [int]$StartupSeconds = 8
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$bundle = Join-Path $root $BundlePath
$testGamePath = Join-Path $bundle 'TestGame\TrainerStudio.TestGame.exe'
$trainerPath = Join-Path $bundle 'TrainerStudio\TrainerStudio.App.exe'
$startupLogPath = Join-Path $env:LOCALAPPDATA 'Trainer Studio\Logs\startup.log'
$processes = @()

foreach ($path in @($testGamePath, $trainerPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Published executable was not found: $path"
    }
}

if (Test-Path -LiteralPath $startupLogPath) {
    Remove-Item -LiteralPath $startupLogPath -Force
}

try {
    $testGame = Start-Process -FilePath $testGamePath -WorkingDirectory (Split-Path $testGamePath) -PassThru
    $processes += $testGame
    Start-Sleep -Seconds 2
    if ($testGame.HasExited) {
        throw "The controlled test game exited during startup with code $($testGame.ExitCode)."
    }

    $trainer = Start-Process -FilePath $trainerPath -WorkingDirectory (Split-Path $trainerPath) -PassThru
    $processes += $trainer
    Start-Sleep -Seconds $StartupSeconds
    if ($trainer.HasExited) {
        throw "Trainer Studio exited during startup with code $($trainer.ExitCode)."
    }

    if (-not (Test-Path -LiteralPath $startupLogPath)) {
        throw "Trainer Studio remained running but did not create its startup log: $startupLogPath"
    }

    $startupLog = Get-Content -LiteralPath $startupLogPath -Raw
    if ($startupLog -match 'Workspace initialization failed\.') {
        throw "Trainer Studio remained running but workspace initialization failed.`n$startupLog"
    }

    if ($startupLog -notmatch 'Workspace initialization completed\.') {
        throw "Trainer Studio remained running but did not complete workspace initialization.`n$startupLog"
    }

    Write-Host 'Both Windows applications launched and the Trainer Studio workspace initialized.' -ForegroundColor Green
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}
