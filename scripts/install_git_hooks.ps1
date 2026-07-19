param(
    [switch]$Unset
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot
try {
    if ($Unset) {
        git config --unset core.hooksPath
        Write-Host "Removed local core.hooksPath override."
        return
    }

    git config core.hooksPath .githooks
    Write-Host "Configured local Git hooks path: .githooks"
    Write-Host "Pre-push checks are advisory. Use git push --no-verify to bypass in emergencies."
}
finally {
    Pop-Location
}
