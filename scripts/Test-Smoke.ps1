param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$logDirectory = Join-Path $root 'artifacts/smoke'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

if (-not $NoBuild) {
    dotnet build (Join-Path $root 'VantaiViet.sln')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

try {
    foreach ($target in @(
        @{ Name = 'VantaiViet.CoreApi'; Port = 15001 },
        @{ Name = 'VantaiViet.MatchingApi'; Port = 15002 }
    )) {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $target.Port)
        try { $listener.Start() } finally { $listener.Stop() }

        $dll = Join-Path $root "src/$($target.Name)/bin/Debug/net10.0/$($target.Name).dll"
        $url = "http://127.0.0.1:$($target.Port)"
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @(
            "`"$dll`"", '--urls', $url, '--environment', 'Development'
        ) -WorkingDirectory (Join-Path $root "src/$($target.Name)") -WindowStyle Hidden -PassThru `
          -RedirectStandardOutput (Join-Path $logDirectory "$($target.Name).stdout.log") `
          -RedirectStandardError (Join-Path $logDirectory "$($target.Name).stderr.log")
        $processes.Add($process)

        $ready = $false
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            if ($process.HasExited) { throw "$($target.Name) exited. Check $logDirectory" }
            try {
                $response = Invoke-WebRequest "$url/health/ready" -TimeoutSec 2
                if ($response.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Milliseconds 250 }
        }
        if (-not $ready) { throw "$($target.Name) did not become ready." }
        $live = Invoke-WebRequest "$url/health/live"
        if ($live.StatusCode -ne 200) { throw 'Liveness check failed.' }
        $info = Invoke-RestMethod "$url/api/system/info"
        if ($info.service -ne $target.Name -or -not $info.version -or -not $info.timestampUtc) {
            throw "Invalid service response from $($target.Name)."
        }
        $missing = Invoke-WebRequest "$url/does-not-exist" -SkipHttpErrorCheck
        $problemJson = if ($missing.Content -is [byte[]]) {
            [System.Text.Encoding]::UTF8.GetString($missing.Content)
        } else {
            $missing.Content
        }
        $problem = $problemJson | ConvertFrom-Json
        if ($missing.StatusCode -ne 404 -or $problem.status -ne 404 -or -not $problem.traceId -or -not $problem.errorCode) {
            throw 'Expected a 404 ProblemDetails response with traceId and errorCode.'
        }
        $cors = Invoke-WebRequest "$url/api/system/info" -Method Options -Headers @{
            Origin = 'http://localhost:4200'; 'Access-Control-Request-Method' = 'GET'
        }
        if ($cors.Headers['Access-Control-Allow-Origin'] -ne 'http://localhost:4200') {
            throw 'Development CORS allowlist check failed.'
        }
        $deniedCors = Invoke-WebRequest "$url/api/system/info" -Method Options -Headers @{
            Origin = 'https://untrusted.example'; 'Access-Control-Request-Method' = 'GET'
        }
        if ($deniedCors.Headers.ContainsKey('Access-Control-Allow-Origin')) {
            throw 'Unexpected CORS access for untrusted origin.'
        }
        Write-Host "$($target.Name): health, service endpoint, ProblemDetails and CORS passed."
    }
} finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}
