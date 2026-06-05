# Cursor Hooks

No hooks are active in this project.

Harness verification (`.\scripts\verify.ps1` or `./scripts/verify.sh`) runs at **task completion**, per [AGENTS.md](../../AGENTS.md) and [docs/agent-workflows.md](../../docs/agent-workflows.md) — not on every agent turn.

A previous `stop` hook that injected a verify reminder each turn was removed as too noisy on Windows.
