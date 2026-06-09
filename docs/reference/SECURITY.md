# Security — Interactive World Map

Project-specific security guidance for agents. This is a local Windows desktop app with no network API.

## Threat Model (brief)

- **Assets:** User content in `Images&Content/`, Excel coordinates, local log files
- **Trust boundary:** Local filesystem only; no remote auth
- **Attack surface:** File path handling, JSON deserialization, Excel parsing

## Rules for Agents

### Credentials

- **Never** hardcode passwords, API keys, tokens, or private keys in source
- Use environment variables or external config files excluded from git (`.env`, local secrets)

### File Handling

- Resolve paths through `Path.Combine` and validated base directories (`ContentFolderPath`)
- Do not construct paths from unsanitized user input without validation
- Content loads only from `Images&Content/` subfolders named after known locations

### Deserialization

- Parse `visual-config.json` and `locations.json` into typed `Models/` classes
- Do not use `JObject` or dynamic parsing in Views
- Validate coordinate ranges (see `StartupValidator` pixel bounds)

### Logging

- Do not log full file paths containing user PII in production builds
- Log levels: ERROR for failures, INFO for operations — avoid logging raw Excel cell values at INFO

### Dependencies

- Minimize NuGet additions; evaluate security advisories before adding packages
- Current sole runtime dependency: Newtonsoft.Json 13.0.4
- CI runs a NuGet vulnerability gate (`scripts/verify_nuget_vulnerabilities.py`) after restore — fails on **High** and **Critical** advisories (direct and transitive)
- Dependabot opens weekly PRs for NuGet and GitHub Actions updates (see `.github/dependabot.yml`)

### Secrets in CI

- **Gitleaks** runs on every push/PR to `main` (`.github/workflows/gitleaks.yml`)
- Never commit passwords, API keys, tokens, or private keys — use environment variables or gitignored local config
- If Gitleaks flags a finding: rotate the exposed secret, remove it from history if needed, or add a narrow `.gitleaks.toml` allowlist only for documented false positives

## When to Escalate

- Adding network features (API calls, telemetry upload)
- Reading files outside `Images&Content/` or app data dirs
- Executing external processes or shell commands from app code
