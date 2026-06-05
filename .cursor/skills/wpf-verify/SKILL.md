---
name: wpf-verify
description: Build, test, and validate Interactive World Map on Windows and macOS. Use when verifying WPF changes, running harness checks, querying logs, or validating startup without UI.
---

# WPF Verify Skill

## Quick verify

```bash
# macOS/Linux (no UI)
./scripts/verify.sh

# Windows (full)
.\scripts\verify.ps1
```

## Individual steps

```bash
dotnet restore InteractiveWorldMap.sln
dotnet build InteractiveWorldMap.sln --configuration Release
dotnet test Tests/InteractiveWorldMap.Tests.csproj --configuration Release --no-build
python3 scripts/verify_doc_links.py
python3 scripts/verify_taste.py
```

Windows only:

```powershell
.\scripts\validate_startup.ps1
.\scripts\query_logs.ps1 -Filter "ERROR" -Last 50
.\run-demo.bat
```

## Headless startup (no UI)

```powershell
dotnet test --filter "FullyQualifiedName~StartupValidationHarness"
```

## Platform limits

| Platform | Build/Test | UI | Logs |
|----------|------------|-----|------|
| Windows | Yes | Yes | Yes |
| macOS | Yes* | No | No |

*Requires .NET 6 SDK. WPF targets `net6.0-windows` — build may require Windows; tests compile on macOS if SDK supports it.

## On failure

Read REMEDIATION lines in script output. See [docs/agent-workflows.md](../../docs/agent-workflows.md).
