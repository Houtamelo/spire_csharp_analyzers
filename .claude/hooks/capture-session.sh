#!/usr/bin/env bash
set -euo pipefail

# Capture session transcript and generate a structured summary.
# Called by SubagentStop and SessionEnd hooks.
#
# Produces:
#   feedback/transcripts/<timestamp>_<agent>.jsonl  (gitignored, raw)
#   feedback/summaries/<timestamp>_<agent>.md       (git-tracked, structured)

INPUT=$(cat)

TRANSCRIPT_PATH=$(echo "$INPUT" | jq -r '.agent_transcript_path // .transcript_path // empty')
AGENT_TYPE=$(echo "$INPUT" | jq -r '.agent_type // "main-session"')
SESSION_ID=$(echo "$INPUT" | jq -r '.session_id // "unknown"')

[ -z "$TRANSCRIPT_PATH" ] && exit 0
[ ! -f "$TRANSCRIPT_PATH" ] && exit 0

PROJECT_ROOT="$(git -C "$(echo "$INPUT" | jq -r '.cwd')" rev-parse --show-toplevel 2>/dev/null || echo "$INPUT" | jq -r '.cwd')"
FEEDBACK_DIR="${PROJECT_ROOT}/feedback"
TRANSCRIPT_DIR="${FEEDBACK_DIR}/transcripts"
SUMMARY_DIR="${FEEDBACK_DIR}/summaries"
mkdir -p "$TRANSCRIPT_DIR" "$SUMMARY_DIR"

TIMESTAMP=$(date +%Y-%m-%d_%H-%M-%S)
TRANSCRIPT_COPY="${TRANSCRIPT_DIR}/${TIMESTAMP}_${AGENT_TYPE}.jsonl"
SUMMARY_FILE="${SUMMARY_DIR}/${TIMESTAMP}_${AGENT_TYPE}.md"

# Copy raw transcript (gitignored)
cp "$TRANSCRIPT_PATH" "$TRANSCRIPT_COPY"

# Generate structured summary (git-tracked)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
bash "${SCRIPT_DIR}/generate-summary.sh" \
  "$TRANSCRIPT_COPY" \
  "$SUMMARY_FILE" \
  "$AGENT_TYPE" \
  "$SESSION_ID"

exit 0
