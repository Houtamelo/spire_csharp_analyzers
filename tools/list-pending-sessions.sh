#!/usr/bin/env bash
set -euo pipefail

# List session summary files that haven't been referenced in any commit message yet.
# Useful when preparing a commit — shows which sessions to reference.
#
# Usage: bash tools/list-pending-sessions.sh

PROJECT_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
SUMMARY_DIR="${PROJECT_ROOT}/feedback/summaries"

if [ ! -d "$SUMMARY_DIR" ]; then
  echo "No summaries directory found."
  exit 0
fi

# Get all summary files
SUMMARIES=$(find "$SUMMARY_DIR" -name '*.md' -type f 2>/dev/null | sort)

if [ -z "$SUMMARIES" ]; then
  echo "No session summaries found."
  exit 0
fi

echo "Session summaries not yet referenced in a commit:"
echo ""

FOUND=0
while IFS= read -r summary; do
  REL_PATH=$(realpath --relative-to="$PROJECT_ROOT" "$summary")
  # Check if this summary path appears in any commit message
  if ! git log --all --grep="$REL_PATH" --oneline 2>/dev/null | grep -q .; then
    echo "  - $REL_PATH"
    # Print the task line for context
    TASK=$(grep -A1 "^## Task" "$summary" 2>/dev/null | tail -1 | sed 's/^> //')
    [ -n "$TASK" ] && echo "    Task: $TASK"
    FOUND=$((FOUND + 1))
  fi
done <<< "$SUMMARIES"

if [ "$FOUND" -eq 0 ]; then
  echo "  All summaries are referenced in commits."
fi

echo ""
echo "Total pending: $FOUND"
