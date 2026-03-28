# Filesystem MCP Tools — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 6 filesystem MCP tools to `tools/DevTools/` so agents can operate without Bash access.

**Architecture:** Single new file `FileSystemTools.cs` in `tools/DevTools/`. Static class with `[McpServerToolType]`. Shared `RepoRoot` resolution via `git rev-parse --show-toplevel` cached in a static field. All path parameters resolved and validated against repo root before any IO.

**Tech Stack:** C# / .NET 10 / `System.IO` / `ModelContextProtocol` 1.1.0 (existing)

---

### Task 1: Path Resolution Helper

**Files:**
- Create: `tools/DevTools/FileSystemTools.cs`

- [ ] **Step 1: Create `FileSystemTools.cs` with repo root resolution and path validation**

```csharp
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace DevTools;

[McpServerToolType]
public static class FileSystemTools
{
    static readonly string RepoRoot;
    static readonly string[] SensitivePatterns = [".git", ".claude", ".csproj", ".sln"];

    static FileSystemTools()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse --show-toplevel",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        RepoRoot = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(TimeSpan.FromSeconds(5));
        if (process.ExitCode != 0 || string.IsNullOrEmpty(RepoRoot))
            throw new InvalidOperationException("Failed to resolve git repo root.");
    }

    /// Returns resolved absolute path, or null + error message if invalid.
    static (string? path, string? error) ResolvePath(string input)
    {
        string absolute;
        if (Path.IsPathRooted(input))
            absolute = Path.GetFullPath(input);
        else
            absolute = Path.GetFullPath(Path.Combine(RepoRoot, input));

        // Resolve symlinks
        absolute = Path.GetFullPath(absolute);

        if (!absolute.StartsWith(RepoRoot + Path.DirectorySeparatorChar) && absolute != RepoRoot)
            return (null, $"Error: path '{input}' resolves to '{absolute}' which is outside repo root '{RepoRoot}'.");

        return (absolute, null);
    }

    static bool IsSensitivePath(string absolutePath)
    {
        var relative = Path.GetRelativePath(RepoRoot, absolutePath);
        foreach (var pattern in SensitivePatterns)
        {
            if (relative.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                relative.Contains(Path.DirectorySeparatorChar + pattern, StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build tools/DevTools/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add tools/DevTools/FileSystemTools.cs
git commit -m "feat(dev-tools): add FileSystemTools with path resolution helper"
```

---

### Task 2: `list_files` Tool

**Files:**
- Modify: `tools/DevTools/FileSystemTools.cs`

- [ ] **Step 1: Add C# Range parser helper**

Add this method to `FileSystemTools`:

```csharp
    /// Parses a C# Range string ("0..30", "60..", "^60..") into skip/take for a collection of `total` items.
    static (int skip, int take, string? error) ParseWindow(string window, int total)
    {
        if (string.IsNullOrWhiteSpace(window))
            return (0, 30, null);

        var parts = window.Split("..", 2, StringSplitOptions.None);
        if (parts.Length != 2)
            return (0, 0, $"Error: invalid window syntax '{window}'. Expected C# Range format: '0..30', '60..', '^60..'.");

        var startStr = parts[0].Trim();
        var endStr = parts[1].Trim();

        int start;
        if (string.IsNullOrEmpty(startStr))
        {
            start = 0;
        }
        else if (startStr.StartsWith('^'))
        {
            if (!int.TryParse(startStr.AsSpan(1), out var fromEnd))
                return (0, 0, $"Error: invalid start index '{startStr}'.");
            start = Math.Max(0, total - fromEnd);
        }
        else
        {
            if (!int.TryParse(startStr, out start))
                return (0, 0, $"Error: invalid start index '{startStr}'.");
        }

        int end;
        if (string.IsNullOrEmpty(endStr))
        {
            end = total;
        }
        else if (endStr.StartsWith('^'))
        {
            if (!int.TryParse(endStr.AsSpan(1), out var fromEnd))
                return (0, 0, $"Error: invalid end index '{endStr}'.");
            end = Math.Max(0, total - fromEnd);
        }
        else
        {
            if (!int.TryParse(endStr, out end))
                return (0, 0, $"Error: invalid end index '{endStr}'.");
        }

        start = Math.Clamp(start, 0, total);
        end = Math.Clamp(end, start, total);
        return (start, end - start, null);
    }
```

- [ ] **Step 2: Add `list_files` tool method**

```csharp
    [McpServerTool(Name = "list_files")]
    [Description("List files in a directory with optional filename regex filtering and pagination. Returns paths relative to the listed directory, directories suffixed with '/'.")]
    public static string ListFiles(
        [Description("Directory path (relative to repo root or absolute)")] string path,
        [Description("Regex matched against filename only (not path). Empty = match all.")] string match_filename = "",
        [Description("Recurse into subdirectories")] bool recurse = true,
        [Description("C# Range syntax for output pagination. '0..30' = first 30, '60..' = skip 60, '^60..' = last 60.")] string window = "0..30")
    {
        var (resolved, error) = ResolvePath(path);
        if (error != null) return error;

        if (!Directory.Exists(resolved))
            return $"Error: directory '{path}' does not exist.";

        Regex? regex = null;
        if (!string.IsNullOrEmpty(match_filename))
        {
            try { regex = new Regex(match_filename, RegexOptions.Compiled); }
            catch (ArgumentException ex) { return $"Error: invalid regex '{match_filename}': {ex.Message}"; }
        }

        var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var entries = new System.Collections.Generic.List<string>();

        // Directories first
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(resolved, "*", searchOption))
            {
                var name = Path.GetFileName(dir);
                if (regex != null && !regex.IsMatch(name)) continue;
                entries.Add(Path.GetRelativePath(resolved, dir) + "/");
            }
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible dirs */ }

        entries.Sort(StringComparer.OrdinalIgnoreCase);
        var dirCount = entries.Count;

        var files = new System.Collections.Generic.List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(resolved, "*", searchOption))
            {
                var name = Path.GetFileName(file);
                if (regex != null && !regex.IsMatch(name)) continue;
                files.Add(Path.GetRelativePath(resolved, file));
            }
        }
        catch (UnauthorizedAccessException) { /* skip inaccessible files */ }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        entries.AddRange(files);

        var (skip, take, windowError) = ParseWindow(window, entries.Count);
        if (windowError != null) return windowError;

        var paged = entries.GetRange(skip, Math.Min(take, entries.Count - skip));

        var header = $"Total: {entries.Count} ({dirCount} dirs, {entries.Count - dirCount} files). Showing {skip}..{skip + paged.Count}.";
        return header + "\n" + string.Join("\n", paged);
    }
```

- [ ] **Step 3: Build to verify compilation**

Run: `dotnet build tools/DevTools/`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add tools/DevTools/FileSystemTools.cs
git commit -m "feat(dev-tools): add list_files MCP tool"
```

---

### Task 3: `create_directory` Tool

**Files:**
- Modify: `tools/DevTools/FileSystemTools.cs`

- [ ] **Step 1: Add `create_directory` tool method**

```csharp
    [McpServerTool(Name = "create_directory")]
    [Description("Create a directory including intermediate directories. Path must be within repo root.")]
    public static string CreateDirectory(
        [Description("Directory path to create (relative to repo root or absolute)")] string path)
    {
        var (resolved, error) = ResolvePath(path);
        if (error != null) return error;

        if (File.Exists(resolved))
            return $"Error: '{path}' already exists as a file.";

        if (Directory.Exists(resolved))
            return $"Directory already exists: {Path.GetRelativePath(RepoRoot, resolved)}";

        Directory.CreateDirectory(resolved);
        return $"Created: {Path.GetRelativePath(RepoRoot, resolved)}";
    }
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build tools/DevTools/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add tools/DevTools/FileSystemTools.cs
git commit -m "feat(dev-tools): add create_directory MCP tool"
```

---

### Task 4: `remove` Tool

**Files:**
- Modify: `tools/DevTools/FileSystemTools.cs`

- [ ] **Step 1: Add `remove` tool method**

```csharp
    [McpServerTool(Name = "remove")]
    [Description("Delete a single file or an empty directory. Rejects non-empty directories (use remove_recursive). Prompts for confirmation on sensitive paths (.git/, .claude/, .csproj, .sln).")]
    public static string Remove(
        [Description("File or empty directory to delete (relative to repo root or absolute)")] string path)
    {
        var (resolved, error) = ResolvePath(path);
        if (error != null) return error;

        if (Directory.Exists(resolved))
        {
            if (Directory.EnumerateFileSystemEntries(resolved).Any())
                return $"Error: directory '{path}' is not empty. Delete contents individually or use `remove_recursive`.";

            if (IsSensitivePath(resolved))
                return $"Error: '{path}' is a sensitive path. Agent must ask user for permission before deleting.";

            Directory.Delete(resolved);
            return $"Deleted empty directory: {Path.GetRelativePath(RepoRoot, resolved)}";
        }

        if (File.Exists(resolved))
        {
            if (IsSensitivePath(resolved))
                return $"Error: '{path}' is a sensitive path. Agent must ask user for permission before deleting.";

            File.Delete(resolved);
            return $"Deleted: {Path.GetRelativePath(RepoRoot, resolved)}";
        }

        return $"Error: '{path}' does not exist.";
    }
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build tools/DevTools/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add tools/DevTools/FileSystemTools.cs
git commit -m "feat(dev-tools): add remove MCP tool"
```

---

### Task 5: `remove_recursive` Tool

**Files:**
- Modify: `tools/DevTools/FileSystemTools.cs`

- [ ] **Step 1: Add `remove_recursive` tool method**

```csharp
    [McpServerTool(Name = "remove_recursive")]
    [Description("Delete a directory and all its contents. Always requires user confirmation. Rejects if fewer than 10 files (use `remove` individually instead).")]
    public static string RemoveRecursive(
        [Description("Directory to delete recursively (relative to repo root or absolute)")] string path,
        [Description("Set to true after user has explicitly approved deletion")] bool confirmed = false)
    {
        var (resolved, error) = ResolvePath(path);
        if (error != null) return error;

        if (!Directory.Exists(resolved))
            return $"Error: directory '{path}' does not exist.";

        var fileCount = Directory.EnumerateFiles(resolved, "*", SearchOption.AllDirectories).Count();

        if (fileCount < 10)
            return $"Error: only {fileCount} files in '{path}'. Use `remove` on each file individually.";

        if (!confirmed)
            return $"Error: {fileCount} files would be deleted at '{Path.GetRelativePath(RepoRoot, resolved)}'. Agent must ask user for permission, then retry with confirmed=true.";

        Directory.Delete(resolved, recursive: true);
        return $"Deleted '{Path.GetRelativePath(RepoRoot, resolved)}' ({fileCount} files).";
    }
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build tools/DevTools/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add tools/DevTools/FileSystemTools.cs
git commit -m "feat(dev-tools): add remove_recursive MCP tool"
```

---

### Task 6: `copy` Tool

**Files:**
- Modify: `tools/DevTools/FileSystemTools.cs`

- [ ] **Step 1: Add `copy` tool method with directory copy helper**

```csharp
    [McpServerTool(Name = "copy")]
    [Description("Copy a file or directory. Directories are copied recursively by default.")]
    public static string Copy(
        [Description("Source path (relative to repo root or absolute)")] string source,
        [Description("Destination path (relative to repo root or absolute)")] string destination,
        [Description("Copy directories recursively")] bool recursive = true)
    {
        var (srcResolved, srcError) = ResolvePath(source);
        if (srcError != null) return srcError;

        var (dstResolved, dstError) = ResolvePath(destination);
        if (dstError != null) return dstError;

        if (Directory.Exists(srcResolved))
        {
            if (!recursive)
                return $"Error: '{source}' is a directory and recursive=false.";

            if (File.Exists(dstResolved))
                return $"Error: destination '{destination}' exists as a file.";

            CopyDirectory(srcResolved, dstResolved);
            return $"Copied directory: {Path.GetRelativePath(RepoRoot, srcResolved)} -> {Path.GetRelativePath(RepoRoot, dstResolved)}";
        }

        if (File.Exists(srcResolved))
        {
            if (Directory.Exists(dstResolved))
                return $"Error: destination '{destination}' exists as a directory.";

            var dstDir = Path.GetDirectoryName(dstResolved);
            if (dstDir != null && !Directory.Exists(dstDir))
                Directory.CreateDirectory(dstDir);

            File.Copy(srcResolved, dstResolved, overwrite: false);
            return $"Copied: {Path.GetRelativePath(RepoRoot, srcResolved)} -> {Path.GetRelativePath(RepoRoot, dstResolved)}";
        }

        return $"Error: source '{source}' does not exist.";
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: false);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build tools/DevTools/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add tools/DevTools/FileSystemTools.cs
git commit -m "feat(dev-tools): add copy MCP tool"
```

---

### Task 7: `move` Tool

**Files:**
- Modify: `tools/DevTools/FileSystemTools.cs`

- [ ] **Step 1: Add `move` tool method**

```csharp
    [McpServerTool(Name = "move")]
    [Description("Move or rename a file or directory. Both source and destination must be within repo root.")]
    public static string Move(
        [Description("Source path (relative to repo root or absolute)")] string source,
        [Description("Destination path (relative to repo root or absolute)")] string destination)
    {
        var (srcResolved, srcError) = ResolvePath(source);
        if (srcError != null) return srcError;

        var (dstResolved, dstError) = ResolvePath(destination);
        if (dstError != null) return dstError;

        if (!File.Exists(srcResolved) && !Directory.Exists(srcResolved))
            return $"Error: source '{source}' does not exist.";

        if (File.Exists(dstResolved) || Directory.Exists(dstResolved))
            return $"Error: destination '{destination}' already exists.";

        var dstDir = Path.GetDirectoryName(dstResolved);
        if (dstDir != null && !Directory.Exists(dstDir))
            Directory.CreateDirectory(dstDir);

        if (Directory.Exists(srcResolved))
            Directory.Move(srcResolved, dstResolved);
        else
            File.Move(srcResolved, dstResolved);

        return $"Moved: {Path.GetRelativePath(RepoRoot, srcResolved)} -> {Path.GetRelativePath(RepoRoot, dstResolved)}";
    }
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build tools/DevTools/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add tools/DevTools/FileSystemTools.cs
git commit -m "feat(dev-tools): add move MCP tool"
```

---

### Task 8: Manual Smoke Test

- [ ] **Step 1: Restart DevTools MCP server and verify tools register**

Restart Claude Code or the MCP server connection. Verify `list_files`, `create_directory`, `remove`, `remove_recursive`, `copy`, `move` appear in available tools.

- [ ] **Step 2: Test `list_files`**

Call `list_files` with `path = "src"`, `window = "0..10"`. Verify it returns directory listing with dirs first, files second, header with counts.

- [ ] **Step 3: Test `create_directory` + `remove`**

Call `create_directory` with `path = "tmp/test-fs-tools"`. Verify directory created.
Call `remove` with `path = "tmp/test-fs-tools"`. Verify empty directory deleted.

- [ ] **Step 4: Test `copy` + `move`**

Call `copy` with `source = "tools/DevTools/Program.cs"`, `destination = "tmp/test-copy.cs"`. Verify file copied.
Call `move` with `source = "tmp/test-copy.cs"`, `destination = "tmp/test-moved.cs"`. Verify file moved.
Call `remove` with `path = "tmp/test-moved.cs"`. Clean up.

- [ ] **Step 5: Test safety — outside repo root**

Call `remove` with `path = "/etc/hosts"`. Verify error: "outside repo root".

- [ ] **Step 6: Test safety — sensitive path**

Call `remove` with `path = ".git/config"`. Verify error: "sensitive path".

- [ ] **Step 7: Test `remove_recursive` — under threshold**

Create `tmp/test-few/` with 3 files. Call `remove_recursive`. Verify error: "only 3 files, use `remove` individually".

- [ ] **Step 8: Commit final state**

```bash
git add -A
git commit -m "feat(dev-tools): filesystem MCP tools — all 6 tools implemented and tested"
```
