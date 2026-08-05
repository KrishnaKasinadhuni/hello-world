# Playbook: Update Documentation

Use this playbook when updating documentation across the `hello-world` repository.

---

## Workflow Steps

1. **Inspect Repo State:**
   - Run `git status` to identify modified, added, or deleted files/directories.
   - Check if subproject statuses (scaffold vs working) have changed.

2. **Update Core Files:**
   - Update `README.md` with new features, services, or commands.
   - Update `AGENTS.md` and `CLAUDE.md` to reflect new folder structures or boundaries.
   - Update the "Last Reviewed" section in `AGENTS.md` with current date and inspected directories.

3. **Update Domain Architecture:**
   - If folder relations changed, update `docs/architecture.md`.
   - If validation commands changed, update `docs/validation.md`.

4. **Verification:**
   - Ensure markdown syntax is valid and links use correct relative paths.
   - Verify all documented commands match actual repo files (`package.json`, `requirements.txt`, etc.).
