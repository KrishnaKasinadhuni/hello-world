# Playbook: Bugfix

Use this playbook when debugging and fixing issues in `hello-world`.

---

## Workflow Steps

1. **Locate Root Cause:**
   - Inspect error logs, tracebacks, or failing test output.
   - Trace cross-service issues (e.g. proxy issues between `frontend/` port 3001 and `services/nodejs/imageClassification/` port 3000).

2. **Apply Minimal Fix:**
   - Modify only the affected subproject files.
   - Avoid broad rewrites or symptom-masking try/except blocks.

3. **Verify Resolution:**
   - Execute the specific validation tests for the subproject from `docs/validation.md`.
