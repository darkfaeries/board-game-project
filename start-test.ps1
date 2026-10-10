$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    docker compose up -d --build
    if ($LASTEXITCODE -ne 0) { throw 'Docker build/start failed. Start Docker Desktop with Linux containers.' }
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        try {
            $health = Invoke-RestMethod 'http://localhost:5285/health' -TimeoutSec 2
            if ($health.status -eq 'ok') { $ready = $true; break }
        } catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { docker compose logs gameserver; throw 'GameServer did not become ready.' }
    Push-Location (Join-Path $PSScriptRoot 'Sandbox')
    try {
        dotnet run --project Sandbox.csproj
        if ($LASTEXITCODE -ne 0) { throw 'Sandbox failed to build or run.' }
    } finally { Pop-Location }
} finally { Pop-Location }
