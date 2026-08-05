# Playbook: Refactor

Use this playbook when refactoring code within `hello-world`.

---

## Workflow Steps

1. **Verify Existing Tests:**
   - Run subproject tests before editing to confirm baseline behavior.

2. **Refactor Code:**
   - Preserve public contracts, API route signatures (`/api/upload`, `/api/classify`, `/api/similar`), and MobX state definitions.
   - Do not leak subproject abstractions across directory boundaries.

3. **Re-Validate:**
   - Run test suites and verify compilation (`npm test`, `python3 -m py_compile`, etc.).
