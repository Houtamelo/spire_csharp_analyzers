---
name: on-completion
description: Verify completed work, update related documentation, and commit when the user asks to wrap up the session.
---

# On Completion

The user invokes this skill when they believe your work is complete and they are ready to wrap up the session.

# Check if your work is actually complete

Check the completed work and run the relevant build and integration tests. If work remains, complete it before wrapping up.
If there are any unfinished tasks, communicate this to the user before proceeding.

# Update the docs

Use the skill `$update-docs` to ensure the changes made in the current session are reflected in the documentation.

# Commit

Invoking this skill authorizes one commit for the completed task. Do not rewrite unrelated history. The documentation changes should be in the same commit.
