#!/usr/bin/env bash
set -euo pipefail

# Create session reflection file and export its path via CLAUDE_ENV_FILE.
# Called by SessionStart hook.

INPUT=$(cat)

SESSION_ID=$(echo "$INPUT" | jq -r '.session_id // "unknown"')
TRANSCRIPT_PATH=$(echo "$INPUT" | jq -r '.transcript_path // "unknown"')
CWD=$(echo "$INPUT" | jq -r '.cwd // "."')

PROJECT_ROOT="$(git -C "$CWD" rev-parse --show-toplevel 2>/dev/null || echo "$CWD")"
REVIEW_DIR="${PROJECT_ROOT}/session-reviews"
REFLECTION_FILE="${REVIEW_DIR}/${SESSION_ID}.md"

mkdir -p "$REVIEW_DIR"

cat > "$REFLECTION_FILE" <<EOF
# Session Review

- **Session ID**: ${SESSION_ID}
- **Transcript**: ${TRANSCRIPT_PATH}
- **Started at**: $(date -Iseconds)
EOF

echo "REFLECTION_FILE_PATH=${REFLECTION_FILE}" >> "$CLAUDE_ENV_FILE"

exit 0
