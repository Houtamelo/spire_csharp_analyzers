#!/usr/bin/env bash
set -euo pipefail

INPUT=$(cat)
TASK_SUBJECT=$(echo "$INPUT" | jq -r '.task_subject')

# Run test projects individually to avoid .NET 10 preview MSBuild node crash (MSB4166)
if ! dotnet test tests/Spire.SourceGenerators.Tests/ 2>&1; then
  echo "Test suite is not passing. Fix all failing tests before completing: $TASK_SUBJECT" >&2
  exit 2
fi

if ! dotnet test tests/Spire.Analyzers.Tests/ 2>&1; then
  echo "Test suite is not passing. Fix all failing tests before completing: $TASK_SUBJECT" >&2
  exit 2
fi

exit 0
