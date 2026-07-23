[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/8.0'
}

Push-Location $root
try {
    dotnet restore .\TrainerStudio.sln
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet build .\TrainerStudio.sln -c $Configuration --no-restore -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    dotnet run --project .\tests\TrainerStudio.Core.Tests\TrainerStudio.Core.Tests.csproj `
        -c $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }

    dotnet run --project .\tests\TrainerStudio.Windows.Tests\TrainerStudio.Windows.Tests.csproj `
        -c $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows pointer integration test failed.' }

    Write-Host 'Trainer Studio build, core tests, and Windows integration test passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
