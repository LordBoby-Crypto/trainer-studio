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
$processes = @()

foreach ($path in @($testGamePath, $trainerPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Published executable was not found: $path"
    }
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

    Write-Host 'Both Windows applications remained running after startup.' -ForegroundColor Green
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}
