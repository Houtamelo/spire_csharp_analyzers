# Filesystem MCP Tools — Design Spec

**Date:** 2026-03-28
**Goal:** Replace Bash tool usage in sub-agents with structured MCP tools for filesystem operations. Agents lose Bash access entirely; these tools cover the remaining gaps not handled by built-in `Read`/`Write`/`Edit`/`Glob`/`Grep`.

## Tools

### `list_files`

List files in a directory with optional filtering and pagination.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `path` | `string` | required | Directory path (relative to repo root or absolute) |
| `match_filename` | `string` | `""` | Regex pattern matched against filename only (not full path). Empty = match all. |
| `recurse` | `bool` | `true` | Recurse into subdirectories |
| `window` | `string` | `"0..30"` | C# Range syntax for output pagination. `"0..30"` = first 30, `"60.."` = skip first 60, `"^60.."` = last 60. |

**Returns:** Newline-separated list of relative paths (relative to `path`). Directories suffixed with `/`. Sorted alphabetically, directories first.

**Errors:**
- Path resolves outside repo root
- Path does not exist or is not a directory
- Invalid regex in `match_filename`
- Invalid range syntax in `window`

### `create_directory`

Create a directory including intermediate directories.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `path` | `string` | required | Directory path to create |

**Returns:** Confirmation message with resolved path.

**Errors:**
- Path resolves outside repo root
- Path already exists as a file

### `remove`

Delete a single file or an empty directory.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `path` | `string` | required | File or empty directory to delete |

**Returns:** Confirmation message.

**Safety rules:**
- Rejects paths resolving outside repo root
- Rejects non-empty directories (error: "directory not empty, delete contents first or use `remove_recursive`")
- Sensitive-path prompt: returns an error requesting user confirmation when path matches `.git/`, `.claude/`, `*.csproj`, `*.sln`. The error message includes the path and asks the agent to confirm with the user before retrying.

### `remove_recursive`

Delete a directory and all its contents. Always requires user confirmation.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `path` | `string` | required | Directory to delete recursively |

**Returns:** Confirmation message with count of deleted files.

**Safety rules:**
- Rejects paths resolving outside repo root
- Counts files in target directory first:
  - **<10 files:** Returns error: "Only N files in directory. Use `remove` on each file individually."
  - **>=10 files:** Returns error: "N files would be deleted at `path`. Agent must ask user for permission before proceeding." Agent is expected to relay this to the user and not retry without explicit approval.

### `copy`

Copy a file or directory.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `source` | `string` | required | Source path |
| `destination` | `string` | required | Destination path |
| `recursive` | `bool` | `true` | Copy directories recursively |

**Returns:** Confirmation message.

**Errors:**
- Source or destination resolves outside repo root
- Source does not exist
- Source is a directory and `recursive` is false
- Destination already exists (as file when copying dir, or vice versa)

### `move`

Move or rename a file or directory.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `source` | `string` | required | Source path |
| `destination` | `string` | required | Destination path |

**Returns:** Confirmation message.

**Errors:**
- Source or destination resolves outside repo root
- Source does not exist
- Destination already exists

## Shared Behavior

### Path Resolution

All paths are resolved relative to the repo root (detected via `git rev-parse --show-toplevel` at startup, cached). Absolute paths are accepted but still validated against repo root.

Symlinks are resolved before validation — a symlink inside the repo pointing outside is rejected.

### Error Format

All errors return a plain text message prefixed with `Error: `. No exceptions thrown to MCP layer.

## Implementation

All tools go into `tools/DevTools/FileSystemTools.cs` as a single `[McpServerToolType]` static class. Repo root resolved once at class initialization.

No new dependencies required — all operations use `System.IO`.
