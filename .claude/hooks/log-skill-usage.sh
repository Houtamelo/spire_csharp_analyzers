#!/usr/bin/env bash
set -euo pipefail

# Log skill invocation to feedback/skill-usage.log.
# Called by skill Stop hooks with skill name as $1.

INPUT=$(cat)
SKILL_NAME="${1:-unknown}"
SESSION_ID=$(echo "$INPUT" | jq -r '.session_id // "unknown"')
PROJECT_ROOT="$(git -C "$(echo "$INPUT" | jq -r '.cwd // "."')" rev-parse --show-toplevel 2>/dev/null || echo "$INPUT" | jq -r '.cwd // "."')"
LOG_FILE="${PROJECT_ROOT}/feedback/skill-usage.log"

mkdir -p "$(dirname "$LOG_FILE")"
echo "$(date -Iseconds) | skill=${SKILL_NAME} | session=${SESSION_ID}" >> "$LOG_FILE"
