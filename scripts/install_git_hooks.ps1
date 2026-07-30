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

    if (-not (Test-Path (Join-Path $repoRoot ".githooks"))) {
        Write-Warning ".githooks/ directory not found - hooks directory must exist"
    }

    $hooks = @("pre-push", "pre-commit")
    foreach ($hook in $hooks) {
        $path = Join-Path $repoRoot ".githooks/$hook"
        if (Test-Path $path) {
            Write-Host "  [OK] .githooks/$hook exists"
        } else {
            Write-Warning "  [MISSING] .githooks/$hook - hooks may not run"
        }
    }

    $currentHooksPath = git config core.hooksPath
    if ($currentHooksPath -eq ".githooks") {
        Write-Host "  [OK] core.hooksPath = .githooks"
    } else {
        Write-Warning "  [WARN] core.hooksPath = '$currentHooksPath' (expected '.githooks')"
    }

    Write-Host "Pre-commit checks formatting; pre-push runs build and advisory checks. Use git push --no-verify to bypass in emergencies."
}
finally {
    Pop-Location
}
