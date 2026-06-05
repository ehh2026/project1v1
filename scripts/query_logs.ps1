# query_logs.ps1 — Agent-queryable log tail and filter
# Usage:
#   .\scripts\query_logs.ps1 -Last 50
#   .\scripts\query_logs.ps1 -Filter "ERROR" -Last 100
#   .\scripts\query_logs.ps1 -Filter "zoom" -Json

param(
    [int]$Last = 50,
    [string]$Filter = "",
    [switch]$Json
)

$LogPath = Join-Path $env:APPDATA "InteractiveWorldMap\logs\app.log"

if (-not (Test-Path $LogPath)) {
    Write-Error "Log file not found: $LogPath. REMEDIATION: Run the app first to generate logs."
    exit 1
}

$lines = Get-Content $LogPath -Tail ($Last * 10)
if ($Filter) {
    $lines = $lines | Where-Object { $_ -match [regex]::Escape($Filter) }
}
$lines = $lines | Select-Object -Last $Last

if ($Json) {
    $entries = $lines | ForEach-Object {
        $level = if ($_ -match '\[ERROR\]') { "ERROR" }
                 elseif ($_ -match '\[WARNING\]') { "WARNING" }
                 elseif ($_ -match '\[INFO\]') { "INFO" }
                 else { "UNKNOWN" }
        [PSCustomObject]@{ level = $level; message = $_ }
    }
    $entries | ConvertTo-Json -Compress
} else {
    $lines
}
