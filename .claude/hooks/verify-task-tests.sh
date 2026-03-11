#!/usr/bin/env bash
set -euo pipefail

INPUT=$(cat)
TASK_SUBJECT=$(echo "$INPUT" | jq -r '.task_subject')

if ! dotnet test 2>&1; then
  echo "Test suite is not passing. Fix all failing tests before completing: $TASK_SUBJECT" >&2
  exit 2
fi

exit 0
