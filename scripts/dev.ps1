<#
.SYNOPSIS
  One-command local start: checks prerequisites, frees the API port, applies migrations, runs the API.

.DESCRIPTION
  1. dotnet SDK, dotnet-ef (local tool) and required user-secrets present
  2. PostgreSQL service running and accepting connections (host/port read from your connection string)
  3. HTTPS dev certificate present (warns if not trusted)
  4. Ports 5129 / 7129 free - a stale WebApi process of THIS repo is stopped; anything else is reported, never killed
  5. dotnet ef database update
  6. dotnet run (https profile: https://localhost:7129 and http://localhost:5129)

.PARAMETER LaunchProfile
  Launch profile: 'https' (default, serves both URLs) or 'http' (http://localhost:5129 only).
.PARAMETER NoMigrate
  Skip applying EF Core migrations.
.PARAMETER Watch
  Use 'dotnet watch' (hot reload) instead of 'dotnet run'.
.PARAMETER CheckOnly
  Run the checks (tooling, secrets, PostgreSQL, certificate, ports) and report; change nothing and do not start the API.

.EXAMPLE
  .\scripts\dev.ps1
  .\scripts\dev.ps1 -LaunchProfile http -NoMigrate
#>
[CmdletBinding()]
param(
    [Alias("Profile")][ValidateSet("https", "http")][string]$LaunchProfile = 'https',
    [switch]$NoMigrate,
    [switch]$Watch,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiRoot = Join-Path $repoRoot 'apps\api'
$webApi = Join-Path $apiRoot 'WebApi'
$ports = if ($LaunchProfile -eq 'https') { @(5129, 7129) } else { @(5129) }

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "    OK   $text" -ForegroundColor Green }
function Warn($text) { Write-Host "    WARN $text" -ForegroundColor Yellow }
function Fail($text) { Write-Host "    FAIL $text" -ForegroundColor Red; exit 1 }

# --- 1. Tooling and secrets --------------------------------------------------------------------
Step 'Checking tooling'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'dotnet SDK not found on PATH (need .NET 10 SDK).' }
Ok "dotnet $(dotnet --version)"

Push-Location $apiRoot
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail 'dotnet tool restore failed (dotnet-ef is a local tool, see apps/api/dotnet-tools.json).' }
    Ok 'dotnet-ef restored'
}
finally { Pop-Location }

Step 'Checking user-secrets'
Push-Location $webApi
try {
    $secrets = @{}
    foreach ($line in (dotnet user-secrets list)) {
        $i = $line.IndexOf(' = ')
        if ($i -gt 0) { $secrets[$line.Substring(0, $i)] = $line.Substring($i + 3) }
    }
}
finally { Pop-Location }

$required = 'ConnectionStrings:DefaultConnection', 'JWT_ACCESS_SECRET', 'JWT_REFRESH_SECRET', 'ENCRYPTION_KEY',
            'GOOGLE_CLIENT_ID', 'GOOGLE_CLIENT_SECRET', 'PUBLIC_URL'
$missing = $required | Where-Object { -not $secrets.ContainsKey($_) -or [string]::IsNullOrWhiteSpace($secrets[$_]) }
if ($missing) {
    Fail ("Missing user-secrets: $($missing -join ', ')`n         See README.md / apps\api\WebApi\user-secrets.example.json")
}
Ok 'all required keys are set'

# --- 2. PostgreSQL -----------------------------------------------------------------------------
Step 'Checking PostgreSQL'
$dbHost = 'localhost'; $dbPort = 5432
if ($secrets['ConnectionStrings:DefaultConnection'] -match '(?i)(?:Server|Host)=([^;]+)') { $dbHost = $Matches[1] }
if ($secrets['ConnectionStrings:DefaultConnection'] -match '(?i)Port=(\d+)') { $dbPort = [int]$Matches[1] }

function Test-Port($h, $p) {
    $c = New-Object System.Net.Sockets.TcpClient
    try { $c.ConnectAsync($h, $p).Wait(2000) -and $c.Connected } catch { $false } finally { $c.Dispose() }
}

if (-not (Test-Port $dbHost $dbPort)) {
    if ($dbHost -in 'localhost', '127.0.0.1') {
        $svc = Get-Service | Where-Object { $_.Name -like 'postgresql*' } | Select-Object -First 1
        if ($svc -and $svc.Status -ne 'Running') {
            Warn "Service '$($svc.Name)' is $($svc.Status); trying to start it (needs an elevated shell)..."
            try { Start-Service $svc.Name; Start-Sleep 3 } catch { Warn "Could not start it: $($_.Exception.Message)" }
        }
    }
    if (-not (Test-Port $dbHost $dbPort)) { Fail "PostgreSQL is not reachable at ${dbHost}:${dbPort}. Start the PostgreSQL service and retry." }
}
Ok "PostgreSQL reachable at ${dbHost}:${dbPort}"

# --- 3. HTTPS dev certificate ------------------------------------------------------------------
if ($LaunchProfile -eq 'https') {
    Step 'Checking HTTPS development certificate'
    dotnet dev-certs https --check --trust 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Ok 'certificate is valid and trusted' }
    else {
        dotnet dev-certs https --check 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { Warn 'certificate exists but is not trusted. Run once: dotnet dev-certs https --trust' }
        else { Warn 'no dev certificate. Run once: dotnet dev-certs https --trust' }
    }
}

# --- 4. Free the ports -------------------------------------------------------------------------
Step "Checking ports $($ports -join ', ')"
foreach ($port in $ports) {
    $owners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($ownerId in $owners) {
        $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $ownerId" -ErrorAction SilentlyContinue
        $cmd = if ($proc) { "$($proc.ExecutablePath) $($proc.CommandLine)" } else { '' }
        # Only stop what is demonstrably this repo's API; never kill an unrelated program.
        if ($cmd -like "*$repoRoot*" -and $cmd -match 'WebApi') {
            if ($CheckOnly) { Warn "Port $port is held by this repo's API (PID $ownerId); a normal run would stop it."; continue }
            Warn "Port $port is held by a stale API process (PID $ownerId); stopping it."
            Stop-Process -Id $ownerId -Force
            Start-Sleep 1
        }
        elseif ($CheckOnly) { Warn "Port $port is held by PID $ownerId ($($proc.Name)), not this repo's API; a normal run would refuse to start." }
        else {
            Fail "Port $port is in use by PID $ownerId ($($proc.Name)), which is not this repo's API. Stop it yourself or free the port."
        }
    }
    if (-not $owners) { Ok "port $port is free" }
}

if ($CheckOnly) { Write-Host "`nCheck-only run finished; nothing was changed." -ForegroundColor Green; exit 0 }

# --- 5. Migrations -----------------------------------------------------------------------------
Push-Location $apiRoot
try {
    if (-not $NoMigrate) {
        Step 'Applying EF Core migrations'
        dotnet ef database update --project Infrastructure --startup-project WebApi
        if ($LASTEXITCODE -ne 0) { Fail 'Migrations failed (see output above).' }
        Ok 'database is up to date'
    }

    # --- 6. Run --------------------------------------------------------------------------------
    Step "Starting API (profile: $LaunchProfile)"
    $verb = if ($Watch) { 'watch' } else { 'run' }
    $logFile = Join-Path $webApi ("logs\dev-{0}.log" -f (Get-Date -Format 'yyyyMMdd'))
    Write-Host "    Log file (Development): $logFile" -ForegroundColor DarkGray
    Write-Host "    Tail it with: Get-Content '$logFile' -Wait -Tail 50" -ForegroundColor DarkGray
    dotnet $verb --project WebApi --launch-profile $LaunchProfile
}
finally { Pop-Location }
