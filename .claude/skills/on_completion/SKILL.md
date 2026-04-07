---
name: on_completion
description: Invoke when you believe the current session work is complete. This prompts two skills: `/verification-before-completion` and `update-docs`, then a git commit.
user-invocable: true
allowed-tools: Read, Write, Glob, Grep, AskUserQuestion
---

# On Completion

The user invokes this skill when they believe your work is complete and they are ready to wrap up the session.

# Check if your work is actually complete

The user may be mistaken, use the skill `/verification-before-completion` and ensure that no pending work remains.
If there are any unfinished tasks, communicate this to the user before proceeding.

# Update the docs

Use the skill `/update-docs` to ensure the changes made in the current session are reflected in the documentation.

# Commit

Make a git commit, try squashing commits related to the same task - one feature = one commit. The documentation changes should be in the same commit.

# Version check

Ask the user if they want to bump the project version and push a tagged release to GitHub (triggers CI -> Nuget publishing).
