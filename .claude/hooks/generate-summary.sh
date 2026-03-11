#!/usr/bin/env bash
set -euo pipefail

# Generate a structured session summary from a JSONL transcript.
# Usage: generate-summary.sh <transcript.jsonl> <output.md> <agent_type> <session_id>
# No LLM — pure jq + bash extraction.

TRANSCRIPT="$1"
OUTPUT="$2"
AGENT_TYPE="${3:-unknown}"
SESSION_ID="${4:-unknown}"

[ ! -f "$TRANSCRIPT" ] && echo "Transcript not found: $TRANSCRIPT" >&2 && exit 1

DATE=$(date -Iseconds)

# --- Extract task (first user message, truncated to 200 chars) ---
TASK=$(jq -r '
  select(.type == "user")
  | .message.content
  | if type == "array" then
      map(select(.type == "text") | .text) | join(" ")
    elif type == "string" then .
    else empty
    end
' "$TRANSCRIPT" | head -1 | cut -c1-200)

[ -z "$TASK" ] && TASK="_Could not extract task from transcript._"

# --- Extract files modified (Edit/Write tool_use blocks) ---
FILES_MODIFIED=$(jq -r '
  select(.type == "assistant")
  | .message.content[]?
  | select(.type == "tool_use")
  | select(.name == "Edit" or .name == "Write")
  | .input.file_path // empty
' "$TRANSCRIPT" 2>/dev/null | sort -u)

# Determine created vs edited by checking which files appear in Write vs Edit
FILES_WRITTEN=$(jq -r '
  select(.type == "assistant")
  | .message.content[]?
  | select(.type == "tool_use" and .name == "Write")
  | .input.file_path // empty
' "$TRANSCRIPT" 2>/dev/null | sort -u)

FILES_EDITED=$(jq -r '
  select(.type == "assistant")
  | .message.content[]?
  | select(.type == "tool_use" and .name == "Edit")
  | .input.file_path // empty
' "$TRANSCRIPT" 2>/dev/null | sort -u)

# --- Extract build/test results ---
# Look for dotnet build/test commands in Bash tool_use blocks and their results
# The transcript interleaves tool_use (in assistant messages) and tool_result (in user messages)

BUILD_RESULTS=$(jq -r '
  select(.type == "assistant")
  | .message.content[]?
  | select(.type == "tool_use" and .name == "Bash")
  | select(.input.command | test("dotnet\\s+build"))
  | .input.command
' "$TRANSCRIPT" 2>/dev/null | head -3)

TEST_RESULTS=$(jq -r '
  select(.type == "assistant")
  | .message.content[]?
  | select(.type == "tool_use" and .name == "Bash")
  | select(.input.command | test("dotnet\\s+test"))
  | .input.command
' "$TRANSCRIPT" 2>/dev/null | head -3)

# --- Extract errors (tool_use with Bash that might have failed) ---
# Look for PostToolUseFailure events or Bash results with errors
ERRORS=$(jq -r '
  select(.type == "tool_result" or .type == "result")
  | .content // .message.content // empty
  | if type == "array" then
      map(select(.type == "text") | .text) | join("\n")
    elif type == "string" then .
    else empty
    end
  | select(test("error|Error|ERROR|FAILED|Build FAILED"; "i"))
' "$TRANSCRIPT" 2>/dev/null | head -20)

# --- Write summary ---
{
  echo "# Session Summary"
  echo ""
  echo "**Date**: ${DATE}"
  echo "**Agent type**: ${AGENT_TYPE}"
  echo "**Session ID**: ${SESSION_ID}"
  echo ""
  echo "## Task"
  echo "> ${TASK}"
  echo ""

  echo "## Files Modified"
  if [ -n "$FILES_WRITTEN" ] || [ -n "$FILES_EDITED" ]; then
    # Show written files as (created), edited as (edited)
    # Files that appear in both are shown as (edited) — Edit after Write means modification
    while IFS= read -r f; do
      [ -z "$f" ] && continue
      if echo "$FILES_EDITED" | grep -qxF "$f"; then
        echo "- \`${f}\` (edited)"
      else
        echo "- \`${f}\` (created)"
      fi
    done <<< "$FILES_WRITTEN"

    while IFS= read -r f; do
      [ -z "$f" ] && continue
      # Skip files already listed from Write
      if ! echo "$FILES_WRITTEN" | grep -qxF "$f"; then
        echo "- \`${f}\` (edited)"
      fi
    done <<< "$FILES_EDITED"
  else
    echo "_No files modified._"
  fi
  echo ""

  echo "## Build/Test Results"
  if [ -n "$BUILD_RESULTS" ] || [ -n "$TEST_RESULTS" ]; then
    [ -n "$BUILD_RESULTS" ] && echo "- Build commands run: $(echo "$BUILD_RESULTS" | wc -l | tr -d ' ')"
    [ -n "$TEST_RESULTS" ] && echo "- Test commands run: $(echo "$TEST_RESULTS" | wc -l | tr -d ' ')"
  else
    echo "_No build or test commands detected._"
  fi
  echo ""

  echo "## Errors Encountered"
  if [ -n "$ERRORS" ]; then
    echo '```'
    echo "$ERRORS"
    echo '```'
  else
    echo "_None_"
  fi
} > "$OUTPUT"
