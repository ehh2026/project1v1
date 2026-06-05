#!/usr/bin/env bash
# Remind agent to run harness verification before claiming completion.
# Hook: stop — runs when agent finishes a turn.

cat <<'EOF'
{
  "followup_message": "Before marking this task complete, run harness verification: ./scripts/verify.sh (macOS/Linux) or .\\scripts\\verify.ps1 (Windows). Update CHANGELOG.md and exec plan if applicable."
}
EOF
